using System;
using System.Collections.Generic;
using System.Reflection;
using NeoModLoader.services;
using RealPolitic.Core;
using UnityEngine;

namespace RealPolitic;

public class WorldFeeder
{
	private readonly PoliticalWorld _world;

	/// <summary>0.23.2: эпохи, вымирания подвидов, династические расколы.</summary>
	private WorldEventsWatcher _watcher;

	private int _lastYear = -1;

	private readonly List<KingdomInput> _kingdomInputs = new List<KingdomInput>();

	private readonly List<CityInput> _cityScratch = new List<CityInput>();

	private readonly List<RelationInfo> _relationScratch = new List<RelationInfo>();

	private readonly Dictionary<int, Kingdom> _liveKingdoms = new Dictionary<int, Kingdom>();

	/// <summary>0.39: уже записанные в журнал отказы от войны (держава:цель:год).</summary>
	private readonly HashSet<string> _warRefusalLogged = new HashSet<string>();

	/// <summary>0.31.12: освобождение, ждущее подтверждения войны за независимость.</summary>
	private sealed class DeferredLiberation
	{
		public RestorationOrder Order;
		public string Name;
		public List<int> CityIds;
		public int Keep;
	}

	private readonly Dictionary<int, DeferredLiberation> _deferredLiberations = new Dictionary<int, DeferredLiberation>();
	private readonly Dictionary<int, DiplomatSnapshot> _diplomatSnapshots = new Dictionary<int, DiplomatSnapshot>();
	private readonly List<DiplomatSnapshot> _diplomatSnapshotList = new List<DiplomatSnapshot>();

		private readonly Dictionary<int, int> _lastDiplomacyYear = new Dictionary<int, int>();

		/// <summary>
		/// 0.44: сколько войн мод не дал объявить, потому что у держав нет общей границы
		/// или правитель не увидел перевеса. Число пишется в журнал — по нему видно,
		/// что «мозги» работают, а не просто молчат.
		/// </summary>
		public static int WarsBlockedBySanity;

		private readonly Dictionary<int, int> _lastStrategyYear = new Dictionary<int, int>();

		private readonly Dictionary<int, KingdomActionPlan> _lastStrategyPlans = new Dictionary<int, KingdomActionPlan>();
		private readonly Dictionary<int, GrandAiReport> _lastGrandAiReports = new Dictionary<int, GrandAiReport>();
		private readonly Dictionary<int, string> _lastStrategyResults = new Dictionary<int, string>();
		private readonly Dictionary<int, string> _diplomacySummaries = new Dictionary<int, string>();

	private int _lastErrorYear = -1;

	private bool _baselineInitialized;

	private MapBox _lastMapBox;
    private long _lastNativeRevision;
	private float _lastRegistrySyncTime = -1f;
	private float _nextWarRetryTime;
	private readonly Dictionary<string, float> _lastWarRetryLog = new Dictionary<string, float>();
	private const float RegistrySyncIntervalSeconds = 0.5f;
	private const float WarRetryIntervalSeconds = 2f;

	/// <summary>Минимальный перерыв (в годах) между дипломатическими действиями одного государства.</summary>
	private const int DiplomacyCooldownYears = 3;

	public WorldFeeder(PoliticalWorld world)
	{
		_world = world;
		// Ядро спрашивает имя вождя мятежа у игры: берём лидера мятежного города.
		// Если игра не ответила — ядро сгенерирует имя само.
		_world.RebelLeaderResolver = (kingdomId, cityId) => CivilWarExecutor.ReadCityLeader(World.world, kingdomId, cityId);
		// Кандидаты на выборах — только настоящие юниты: никаких выдуманных имён.
		ElectionSystem.CandidateResolver = ResolveElectionCandidate;
        _world.CivicSecessionRequest=(country,regions)=>CivicRegionalRuntime.RequestCrisis(_world,World.world,country,regions,RealPoliticMod.CurrentYear());
        ElectionSystem.CivicElectionHandler=(state,year,bus)=>CivicHeadElections.Tick(_world,state,year,new CivicElectionRuntime(_world));
        ElectionSystem.CandidateValidator = (kingdomId, actorId) =>
            IsEligibleElectionActor(CivilWarExecutor.FindKingdom(World.world, kingdomId), GameRefs.FindActorById(actorId));
		// Эпоха «реальности»: по событиям ядра игра реально проводит смену престолов.
		_world.Bus.Subscribe(OnPoliticalEvent);
		// Экономика считает в ядре, а золото, хлеб и настроение живут в городах игры:
		// применяем отчёт через безопасные вызовы, при недоступном API просто пропускаем.
		_world.EconomyEffectApplier = (state, report) =>
		{
			if (!_liveKingdoms.TryGetValue(state.KingdomId, out Kingdom kingdom) || kingdom == null)
			{
				return false;
			}
			return EconomyExecutor.ApplyReport(kingdom, report);
		};
	}

	/// <summary>Этап 25: реакция игры на политические события ядра.
	/// Выборный президент реально взбирается на престол (игрок видит его как короля),
	/// свергнутый уходит на городское попечение; главарь подавленного мятежа казнён.</summary>
	private void OnPoliticalEvent(PolEvent evt)
	{
		if (evt == null || evt.KingdomId == null || !_liveKingdoms.TryGetValue(evt.KingdomId.Value, out Kingdom kingdom) || kingdom == null)
		{
			return;
		}
		try
		{
			if (evt.Type == PolEventType.DemocraticElection)
			{
				TryEntruoneElect(kingdom, evt);
			}
			else if (evt.Type == PolEventType.Impeached)
			{
				// Статья XIV: свергнутый импичментом глава уходит на городское попечение,
				// а коллегия изберёт преемника в свой срок.
				Actor current = GameRefs.ReadRuler(kingdom);
				if (current != null)
				{
					GameRefs.TryAppointToCity(kingdom, current);
				}
			}
			else if (evt.Type == PolEventType.CivilWarEnded
				&& string.Equals(EventParam(evt, "resolution"), "RP_CW_RESULT_LOYALISTVICTORY", StringComparison.OrdinalIgnoreCase))
			{
				TryExecuteRebelLeader(evt);
			}
			else if (evt.Type == PolEventType.KingdomAdoptedIdeology)
			{
				string civName = _world.KingdomNameOf(evt.KingdomId.Value);
				if (!string.IsNullOrWhiteSpace(civName) && !civName.StartsWith("#"))
				{
					GameRefs.TryRenameKingdom(kingdom, civName);
				}
			}
		}
		catch (Exception)
		{
			// Никогда не ломаем игровой цикл.
		}
	}

		/// <summary>
		/// Переносит официальные названия ядра в живые королевства WorldBox после годового тика.
		/// Это обязательный второй шаг переименования: событие революции публикуется до того,
		/// как ядро в конце ProcessKingdom подберёт новое идеологическое название, поэтому одного
		/// обработчика событий недостаточно. Неудавшаяся запись остаётся в очереди и повторится.
		/// </summary>
		private void ApplyPendingKingdomRenames()
		{
			if (_world == null || _world.PendingRenameOverrides.Count == 0)
			{
				return;
			}
			List<KeyValuePair<int, string>> pending = new List<KeyValuePair<int, string>>(_world.PendingRenameOverrides);
			for (int i = 0; i < pending.Count; i++)
			{
				int kingdomId = pending[i].Key;
				string newName = pending[i].Value;
				if (!_liveKingdoms.TryGetValue(kingdomId, out Kingdom kingdom) || kingdom == null)
				{
					kingdom = CivilWarExecutor.FindKingdom(World.world, kingdomId);
					if (kingdom == null)
					{
						continue;
					}
					_liveKingdoms[kingdomId] = kingdom;
				}
				string liveName = ((NanoObject)kingdom).name ?? string.Empty;
				if (string.Equals(liveName, newName, StringComparison.Ordinal))
				{
					_world.CompletePendingRenameOverride(kingdomId, newName);
					continue;
				}
				if (!GameRefs.TryRenameKingdom(kingdom, newName))
				{
					continue;
				}
				string appliedName = ((NanoObject)kingdom).name ?? string.Empty;
				if (string.Equals(appliedName, newName, StringComparison.Ordinal))
				{
					_world.CompletePendingRenameOverride(kingdomId, newName);
					LogService.LogInfo("[Real Politic] официальное название державы #" + kingdomId + " записано в игру: «" + newName + "»");
				}
			}
		}

		private static string EventParam(PolEvent evt, string key)
		{
		string v;
		return evt != null && evt.Params != null && evt.Params.TryGetValue(key, out v) ? v : null;
	}

	/// <summary>Президент взошёл на трон: переименованный взрослый юнит народа,
	/// а свергнутый правитель уходит на попечение города.</summary>
	private ElectionSystem.ElectionCandidate? ResolveElectionCandidate(int kingdomId, int excludeActorId)
	{
		Kingdom kingdom = CivilWarExecutor.FindKingdom(World.world, kingdomId);
		if (kingdom == null)
		{
			return null;
		}
		Actor unit = null;
        int seen = 0;
        KingdomPolitics state = _world.GetKingdom(kingdomId);
        var cities = GameRefs.KingdomCities(kingdom);
        if (cities != null) foreach (City city in cities)
        {
            if (city?.units == null) continue;
            foreach (Actor candidate in city.units)
            {
                if (!IsEligibleElectionActor(kingdom, candidate)) continue;
                int id = (int)candidate.data.id;
                if (id == excludeActorId || (state?.Election?.FindPastPresident(id)?.TermsCount ?? 0) >= ElectionSystem.MaxLifetimeTerms) continue;
                // Reservoir sampling: one pass, no new list of all citizens.
                if (_world.Rng.Next(++seen) == 0) unit = candidate;
            }
        }
		if (unit == null)
		{
			return null;
		}
		return new ElectionSystem.ElectionCandidate
		{
			ActorId = GameText.ActorId(unit),
			Name = GameText.ActorName(unit) ?? string.Empty,
			IsFemale = GameText.ActorIsFemale(unit),
			Age = GameText.ActorAge(unit)
		};
	}

    private void TryEntruoneElect(Kingdom kingdom, PolEvent evt)
    {
        Actor current = GameRefs.ReadRuler(kingdom);
        long currentId = current?.data != null ? current.data.id : 0;
        int electedId = evt.ActorId ?? 0;
        // A re-election or an observed vanilla appointment is NOT a request
        // to replace the incumbent with a random adult.
        if (electedId != 0 && currentId == electedId)
        {
            AcknowledgeElection(kingdom, electedId);
            return;
        }
        Actor winner = GameRefs.FindActorById(electedId);
        // Never substitute a different person for the recorded winner.
        if (!IsEligibleElectionActor(kingdom, winner)) return;
        if (GameRefs.TrySetKingOnKingdom(kingdom, winner, quiet: true))
        {
            AcknowledgeElection(kingdom, electedId);
            if (current != null) GameRefs.TryAppointToCity(kingdom, current);
        }
    }

    private void AcknowledgeElection(Kingdom kingdom, int electedId)
    {
        int id = (int)((NanoObject)kingdom).id;
        KingdomPolitics state = _world.GetKingdom(id);
        if (state?.Ruler?.ActorId == electedId)
            // Otherwise next year's sync counts the same election AGAIN,
            // subtracts legitimacy and can start a succession feedback loop.
            _world.SetLastRuler(id, state.Ruler);
    }

    private static bool IsEligibleElectionActor(Kingdom kingdom, Actor actor)
    {
        if (kingdom == null || actor?.data == null || GameRefs.IsBoatUnit(actor) || GameText.ActorAge(actor) < 18) return false;
        if (GameRefs.Invoke(actor, "isAlive") is bool alive && !alive) return false;
        return ReferenceEquals(actor.kingdom, kingdom)
            || (GameRefs.ReadMember(kingdom, "units") is List<Actor> units && units.Contains(actor));
    }

	/// <summary>Разгром мятежа: главаря казнят, чтобы «подавлено» не было словом.</summary>
	private void TryExecuteRebelLeader(PolEvent evt)
	{
		long actorId = 0;
		string id = EventParam(evt, "leaderActorId");
		if (!string.IsNullOrEmpty(id))
		{
			long.TryParse(id, out actorId);
		}
		Actor leader = GameRefs.FindActorById(actorId);
		if (leader != null)
		{
			GameRefs.TryExecuteActor(leader);
		}
	}

	public void Tick()
	{
		try
		{
			MapBox world = World.world;
            if(!NativeWorldLifecycle.TryComplete(world,_world))return;
            bool nativeTransition=NativeWorldLifecycle.Revision!=_lastNativeRevision;
			if (world == null || world.map_stats == null || world.kingdoms == null || world.cities == null)
			{
				return;
			}
			// Новый мир (другая инстанция MapBox): сбрасываем политическое состояние.
			if (!ReferenceEquals(world, _lastMapBox) || nativeTransition)
			{
				_lastMapBox = world;
                _lastNativeRevision=NativeWorldLifecycle.Revision;
					_lastRegistrySyncTime = -1f;
					_nextWarRetryTime = 0f;
                if (!WorldPersistence.ConsumeLoadedStateFor(world) && !nativeTransition) _world.Clear();
				// Новый мир — новые жители: сбрасываем личные идеологии.
				ActorIdeologies.Clear();
				IdeologyEffects.ClearCache();
				// Статические кэши игрового слоя тоже принадлежат прошлому миру.
				History.Clear();
				GameWars.Reset();
				KmudWarBan.Reset();
				IdeologyMechanicsEffects.Reset();
				WorldPersistence.ResetSaveThrottle();
				_watcher?.Reset();
				_baselineInitialized = false;
					_lastDiplomacyYear.Clear();
						_lastStrategyYear.Clear();
						_lastStrategyPlans.Clear();
						_lastStrategyResults.Clear();
						_diplomacySummaries.Clear();
						_diplomatSnapshots.Clear();
						_diplomatSnapshotList.Clear();
				_lastYear = -1;
			}
			// Актуальный игровой год из игровых часов: Date.getCurrentYear() = getYear(getCurWorldTime()) = (int)(time/60)+1.
			// map_stats.history_current_year обновляется только в декабре и для годового цикла непригоден.
			int currentYear = (int)(world.getCurWorldTime() / 60.0) + 1;
			if (currentYear < 0)
			{
				currentYear = 0;
			}

			// UI-синхронизация реестра нужна почти в реальном времени, но полный обход
			// всех государств и городов каждый кадр слишком дорог для больших карт.
			// Новый мир синхронизируется сразу; далее — не чаще двух раз в секунду.
			float now = Time.unscaledTime;
				// 0.42: под нагрузкой реестр обновляем реже — 0,5 с обычно, 1,5 с при перегрузе.
				float syncInterval = PerfProbe.UnderPressure ? 1.5f : RegistrySyncIntervalSeconds;
				if (_lastRegistrySyncTime < 0f || now < _lastRegistrySyncTime
				|| now - _lastRegistrySyncTime >= syncInterval)
			{
				PerfProbe.Begin(PerfProbe.Step.Sync);
				SyncKingdoms(world);
				SyncCities(world);
                GovernanceRuntime.ObserveLifecycle(_world,world,currentYear);
					_lastRegistrySyncTime = now;
					PerfProbe.End();
				}
				// Война должна подтверждаться сразу, а не ждать следующего игрового года.
				GameWars.Poll();
				if (now >= _nextWarRetryTime)
				{
					_nextWarRetryTime = now + WarRetryIntervalSeconds;
					EnsureCivilWarsAtWar(world);
				}

				if (currentYear == _lastYear)
			{
				return;
			}
			_lastYear = currentYear;
			// 0.53.1: считаем, сколько длится весь годовой шаг мода — от начала до конца.
			System.Diagnostics.Stopwatch yearWatch = System.Diagnostics.Stopwatch.StartNew();
			if (!_baselineInitialized)
			{
				_baselineInitialized = true;
				// Свежий мир (год ≤ 1): уведомляем сразу, в том числе о самом первом явлении
				// религии/языка/культуры. Загруженный мир (год > 1): молча записываем базовую линию.
				_world.SetBaseline(currentYear <= 1);
			}
			// 0.23.2: часы ядра = часы игры. Готовые карты (Европа и т.п.) приходят с сотнями лет
			// истории, а ядро считало годы с нуля — летопись писала «год 142» при игровом 501,
			// сводки и возрасты династий расходились с игрой.
			if (_world.Year != currentYear)
			{
				_world.SetYear(currentYear);
			}
			SyncRulers(world);
			FamilyRegistryTick(world, currentYear);
			(_watcher ?? (_watcher = new WorldEventsWatcher(_world))).Tick(world, currentYear);
			_kingdomInputs.Clear();
			_liveKingdoms.Clear();
			List<Kingdom> list = GameRefs.KingdomList(world);
			if (list == null)
			{
				return;
			}
			PerfProbe.Begin(PerfProbe.Step.Input);
			for (int i = 0; i < list.Count; i++)
			{
				Kingdom val = list[i];
				if (val != null)
				{
					int kId = (int)((NanoObject)val).id;
					KingdomInput builtInput = BuildKingdomInput(world, val);
					if ((builtInput.Cities == null || builtInput.Cities.Count == 0) && builtInput.Population == 0 && _world.GetKingdom(kId) != null)
					{
						continue;
					}
					_liveKingdoms[kId] = val;
					_kingdomInputs.Add(builtInput);
				}
			}
			for (int i = 0; i < _kingdomInputs.Count; i++)
			{
				KingdomInput input = _kingdomInputs[i];
				_diplomacySummaries[input.KingdomId] = BuildDiplomacySummary(input);
			}
			PerfProbe.End();
			// Этап 21: снимок ванильных альянсов — ядро зафиксирует их создание/распад/состав.
			List<AllianceRecord> allianceSnapshot = ReadAllianceRecords();
			_world.SetAllianceSnapshot(allianceSnapshot);
				PerfProbe.Begin(PerfProbe.Step.Core);
				_world.YearTick(_kingdomInputs);
				PerfProbe.End();
				PerfProbe.Begin(PerfProbe.Step.Annual);
				ApplyPendingKingdomRenames();
				_world.SetYear(currentYear);
				try
				{
					RecognitionDiplomacyEngine.EnsureUniversalRecognitionBaseline(
                        _world,
                        currentYear,
                        stableWorldKey: NativeWorldLifecycle.LoadedSlotWorldKey);
					GovernanceRuntime.AnnualTick(_world, currentYear, _kingdomInputs,
						id => _liveKingdoms.TryGetValue(id, out var live) ? live : null);
					RecognitionDiplomacyEngine.Tick(_world, currentYear);
					// 0.32: КМУД — созыв всемирной мирной конференции на 200-м году.
					KmudRuntime.AnnualTick(_world, world, currentYear);
				}
				catch (Exception ex)
				{
					LogService.LogInfo("[Real Politic] 0.31: пропуск кадра признания/совета: " + ex.Message);
				}
                CivicRegionalRuntime.AnnualTick(_world,world,currentYear);
                int viableCountries = 0;
                for (int i = 0; i < _kingdomInputs.Count; i++)
                {
                    if (_kingdomInputs[i].Cities != null && _kingdomInputs[i].Cities.Count > 0 && _kingdomInputs[i].Population > 0)
                    {
                        viableCountries++;
                    }
                }
                AllianceBalance.ReviewOversized(_world, allianceSnapshot, viableCountries, currentYear);
                CivicAllianceMotions.Tick(_world,currentYear,new CivicAllianceRuntime(_world));
				// Этап 20: призывы коалиций, краденая казна и дань вассалов исполняются в игре.
				ApplyDeepDiplomacyEffects();
				// Этап 23: границы (решения ИИ + выдворение чужаков), авиаудары,
				// физическое переименование королевских кланов в фамилии династий.
				ApplyStage23Effects(world, currentYear);
				// 0.46: расселение (державы ставят города на свободной земле)
				// и переселение (жители подают заявления на переезд в другую державу).
				try
				{
					SettlementRuntime.AnnualTick(_world, world, currentYear, InputMap());
					MigrationRuntime.AnnualTick(_world, world, currentYear, InputMap());
					// 0.46: выборы в парламент — в уведомления.
					ParliamentRuntime.AnnualTick(_world, currentYear);
				}
				catch (Exception ex)
				{
					LogService.LogInfo("[Real Politic] 0.46 расселение/переселение: " + ex.Message);
				}
				// 0.23.12: корабли получают национальные имена в «кавычках» —
				// по городам, державе и известным людям их державы.
				GameRefs.ApplyBoatNames(world, _world, _world.Rng);
				// Отделения, оформленные ядром, исполняются сразу — до дипломатии и планов,
				// чтобы новые государства участвовали в том же годовом цикле.
				ExecutePendingSecessions(world);
				ExecutePendingWorldOps(world);
				EnsureCivilWarsAtWar(world);
				ExecutePendingRestorations(world);
					ApplyPendingKingdomRenames();
					ApplyCivilWarEffects(currentYear);
				PlanDiplomacy();
				PlanStrategicActions(currentYear);
				_world.SetAllianceSnapshot(ReadAllianceRecords());
				PerfProbe.End();
				double measuredYearMs = PerfProbe.MsPerYear;
				PerfProbe.YearPassed(currentYear);
				// 0.53.2: разложение включает и годовые эффекты игрового слоя, не только ядро.
				long yearMs = yearWatch.ElapsedMilliseconds;
				if (yearMs > 500)
				{
					LogService.LogInfo("[Real Politic] тяжёлый год " + currentYear + ": весь годовой шаг мода "
						+ yearMs + " мс, из них по прибору " + measuredYearMs.ToString("F0") + " мс на год.");
				}
		}
		catch (Exception ex)
		{
			// Close an in-progress measurement if an annual integration hook failed.
			PerfProbe.End();
			int num = -1;
			try
			{
				if (World.world != null && World.world.map_stats != null)
				{
					num = World.world.map_stats.history_current_year;
				}
			}
			catch (Exception)
			{
			}
			if (num != _lastErrorYear)
			{
				_lastErrorYear = num;
				LogService.LogError("[Real Politic] ошибка годового снимка мира: " + ex);
			}
		}
	}

		/// <summary>Этап 19: лучший сосед-кандидат в династические супруги (династическая держава, не в войне, свободен от уз).</summary>
		private void FindMarriageCandidate(KingdomInput input, KingdomPolitics state, out int candidateId, out bool candidateWar, out float score)
		{
			candidateId = 0;
			candidateWar = false;
			score = 0f;
			if (!_world.Params.EnableDynasties
				|| !Ideologies.CanDynasticMarriage(state.Ideology)
				|| state.Marriage != null || state.Ruler == null || state.Ruler.Age < _world.Params.MarriageMinAge)
			{
				return;
			}
			foreach (KingdomPolitics other in _world.Kingdoms)
			{
				if (other.KingdomId == state.KingdomId
					|| !Ideologies.CanDynasticMarriage(other.Ideology)
					|| other.Marriage != null || other.Ruler == null || other.Ruler.Age < _world.Params.MarriageMinAge)
				{
					continue;
				}
				if (state.Union != null && other.Union != null && state.Union.Id == other.Union.Id)
				{
					continue;
				}
				bool adjacent = false;
				bool war = false;
				if (input.Relations != null)
				{
					for (int r = 0; r < input.Relations.Count; r++)
					{
						if (input.Relations[r].OtherId == other.KingdomId)
						{
							adjacent = true;
							war = input.Relations[r].Relation == RelationType.War;
							break;
						}
					}
				}
				if (!adjacent || war)
				{
					continue;
				}
				float s = 40f + _world.GetRulerRelation(state.KingdomId, other.KingdomId) / 2f
					+ Ideologies.Affinity(state.Ideology, other.Ideology) * 10f;
				if (s > score)
				{
					score = s;
					candidateId = other.KingdomId;
				}
			}
		}

		/// <summary>Этап 20: лучший сосед для договора (пакт при угрозе, торговля при мире).</summary>
		private void FindTreatyCandidate(KingdomInput input, KingdomPolitics state, out int candidateId, out TreatyType type, out bool candidateWar, out float score)
		{
			candidateId = 0;
			type = TreatyType.NonAggression;
			candidateWar = false;
			score = 0f;
			if (!_world.Params.EnableTreaties || input.Relations == null)
			{
				return;
			}
			for (int r = 0; r < input.Relations.Count; r++)
			{
				RelationInfo relation = input.Relations[r];
				if (relation.Relation == RelationType.War || _world.GetKingdom(relation.OtherId) == null)
				{
					continue;
				}
				TreatyType wanted = HostileNeighborsOf(input) > 0 ? TreatyType.DefensePact
					: (EconomyExecutor.EconomyEnabled ? TreatyType.TradeAgreement : TreatyType.NonAggression);
				if (wanted == TreatyType.DefensePact && _world.GetRulerRelation(input.KingdomId, relation.OtherId) < 10)
				{
					wanted = TreatyType.NonAggression;
				}
				if (_world.TreatiesBook.Between(input.KingdomId, relation.OtherId, wanted) != null)
				{
					continue;
				}
				float s = 42f + _world.GetRulerRelation(input.KingdomId, relation.OtherId) / 4f
					+ (wanted == TreatyType.DefensePact ? 4f : 0f);
				if (s > score)
				{
					score = s;
					candidateId = relation.OtherId;
					type = wanted;
				}
			}
		}

		private int HostileNeighborsOf(KingdomInput input)
		{
			int hostile = 0;
			if (input.Relations != null)
			{
				for (int r = 0; r < input.Relations.Count; r++)
				{
					if (input.Relations[r].Relation == RelationType.War)
					{
						hostile++;
					}
				}
			}
			return hostile;
		}

		/// <summary>Этап 20: цель для войны по казус белли — только сосед без пакта о ненападении.</summary>
		private void FindWarTarget(KingdomInput input, KingdomPolitics state, out int targetId, out CasusBelliType cb, out float score)
		{
			targetId = 0;
			cb = CasusBelliType.Conquest;
			score = 0f;
			if (!_world.Params.EnableCasusBelli || input.Relations == null || input.AtWar || input.Starving)
			{
				return;
			}
			if (state.Regency != null && state.Regency.IsActive)
			{
				return;
			}
			// 0.39: после проигранной войны правитель 10 лет не ищет, на кого напасть.
			if (KingdomLearning.IsWarAverse(state, _world.Year))
			{
				return;
			}
			for (int r = 0; r < input.Relations.Count; r++)
			{
				RelationInfo relation = input.Relations[r];
					if (relation.Relation == RelationType.War || relation.Relation == RelationType.Alliance
						|| !relation.Adjacent || _world.GetKingdom(relation.OtherId) == null || SameUnion(input.KingdomId, relation.OtherId))
					{
						continue;
					}
				if (_world.TreatiesBook.NonAggressionActive(input.KingdomId, relation.OtherId))
				{
					continue;
				}
				// Этап 22: перемирие свято — воюющий ИИ не срывает его собственным решением.
				if (_world.TruceActiveBetween(input.KingdomId, relation.OtherId))
				{
					continue;
				}
				CasusBelliType candidateCb = _world.BestCasusBelli(input.KingdomId, relation.OtherId, relation.Claims);
				float baseScore;
				switch (candidateCb)
				{
					case CasusBelliType.Reconquest: baseScore = 44f; break;
					case CasusBelliType.BrokenTreaty: baseScore = 42f; break;
					case CasusBelliType.Revenge: baseScore = 40f; break;
					case CasusBelliType.IdeologicalLiberation: baseScore = 34f; break;
					default: baseScore = 22f; break;
				}
				float s = baseScore + _world.GetGrievance(input.KingdomId, relation.OtherId) / 5f;
				if (TryGetInput(relation.OtherId, out KingdomInput targetInput))
				{
					// 0.39: правитель считает силы — свои, защитника и его союзников —
					// и не идёт на безнадёжную войну. Раньше сравнивались только воины
					// и только для завоевания, из-за чего державы бросались на любого,
					// у кого за спиной стоял союз.
					int allyWarriors = AllianceWarriorsOf(relation.OtherId);
					WarEstimate estimate = WarChances.Estimate(
						input.Warriors,
						input.Cities == null ? 0 : input.Cities.Count,
						state.Economy?.Prosperity ?? 50f,
						state.WarFatigue,
						targetInput.Warriors,
						targetInput.Cities == null ? 0 : targetInput.Cities.Count,
						_world.GetKingdom(relation.OtherId)?.Economy?.Prosperity ?? 50f,
						_world.GetKingdom(relation.OtherId)?.WarFatigue ?? 0f,
						allyWarriors,
						candidateCb);
					if (!estimate.ShouldAttack)
					{
						NoteWarRefusal(input.KingdomId, relation.OtherId, estimate);
						continue;
					}
					s += Math.Min(15f, Math.Max(0f, estimate.Ratio - 1f) * 20f);
				}
				if (s > score)
				{
					score = s;
					targetId = relation.OtherId;
					cb = candidateCb;
				}
			}
		}

		/// <summary>
		/// 0.39: сколько воинов стоит за спиной державы — её союзники по альянсу.
		/// Считаем только живых, саму державу не считаем.
		/// </summary>
		private int AllianceWarriorsOf(int kingdomId)
		{
			if (!_liveKingdoms.TryGetValue(kingdomId, out Kingdom subject) || subject == null)
			{
				return 0;
			}
			int total = 0;
			foreach (KeyValuePair<int, Kingdom> pair in _liveKingdoms)
			{
				Kingdom other = pair.Value;
				if (other == null || pair.Key == kingdomId)
				{
					continue;
				}
				if (!AllianceSafety.Same(subject, other))
				{
					continue;
				}
				if (TryGetInput(pair.Key, out KingdomInput allyInput))
				{
					total += Math.Max(0, allyInput.Warriors);
				}
			}
			return total;
		}

		/// <summary>0.39: одна строка в журнал за год — почему правитель отказался от войны.</summary>
		private void NoteWarRefusal(int kingdomId, int targetId, WarEstimate estimate)
		{
			if (estimate == null)
			{
				return;
			}
			string key = kingdomId + ":" + targetId + ":" + _world.Year;
			if (!_warRefusalLogged.Add(key))
			{
				return;
			}
			if (_warRefusalLogged.Count > 400)
			{
				_warRefusalLogged.Clear();
			}
			LogService.LogInfo("[Real Politic] ИИ: «" + _world.KingdomNameOf(kingdomId) + "» не идёт войной на «"
				+ _world.KingdomNameOf(targetId) + "» — " + estimate.Describe());
		}

		/// <summary>Этап 20: цель для тайной операции — воюющий сосед или держав с большой обидой.</summary>
		private void FindCovertTarget(KingdomInput input, KingdomPolitics state, out int targetId, out SpyOpType op, out float score)
		{
			targetId = 0;
			op = SpyOpType.Sabotage;
			score = 0f;
			if (!_world.Params.EnableEspionage || (state.Spy?.Level ?? 0) < 1 || input.Relations == null)
			{
				return;
			}
			if (state.Spy.LastOpYear != int.MinValue && _world.Year - state.Spy.LastOpYear < _world.Params.SpyCooldownYears)
			{
				return;
			}
			for (int r = 0; r < input.Relations.Count; r++)
			{
				RelationInfo relation = input.Relations[r];
				if (!relation.Adjacent || _world.GetKingdom(relation.OtherId) == null)
				{
					continue;
				}
				float grievance = _world.GetGrievance(input.KingdomId, relation.OtherId);
				bool hostile = relation.Relation == RelationType.War
					|| grievance >= 20f
					|| _world.GetRulerRelation(input.KingdomId, relation.OtherId) <= -20;
				if (!hostile)
				{
					continue;
				}
				float s = 30f + grievance / 3f + state.Spy.Level * 4f
					+ (relation.Relation == RelationType.War ? 6f : 0f);
				if (s > score)
				{
					score = s;
					targetId = relation.OtherId;
					op = relation.Relation == RelationType.War
						? (state.Spy.Level >= 2 ? SpyOpType.StealGold : SpyOpType.Sabotage)
						: SpyOpType.InciteDissent;
				}
			}
		}

		/// <summary>Этап 20: вассальный статус державы — сюзерен, одобрение и его военное положение.</summary>
		private void GetVassalStatus(KingdomPolitics state, out int suzerainId, out float approval, out bool suzerainAtWar)
		{
			suzerainId = 0;
			approval = 1f;
			suzerainAtWar = false;
			Union union = state.Union;
			if (union == null || union.Type != UnionType.Vassal || union.LeaderId == state.KingdomId)
			{
				return;
			}
			suzerainId = union.LeaderId;
			approval = union.GetMember(state.KingdomId)?.Approval ?? 0.6f;
			if (TryGetInput(suzerainId, out KingdomInput suzerainInput))
			{
				suzerainAtWar = suzerainInput.AtWar;
			}
		}

		/// <summary>Этап 21: считывает ванильные альянсы игры через рефлексию (менеджер альянсов
		/// не имеет публичного стабильного API — пробуем несколько вариантов доступа).</summary>
		private static bool _allianceApiLogged;

		/// <summary>0.23.4: читает ванильные альянсы с запасными именами членов API (WorldBox 0.50+:
		/// AllianceManager.list, Alliance.kingdoms_list / kingdoms_hashset, имя — Alliance.name/data.name).
		/// Раньше искались только getAlliances/alliances_list — на текущей версии игры они отсутствуют,
		/// и за 140 лет в летопись не попало ни одного альянса.</summary>
		private List<AllianceRecord> ReadAllianceRecords()
		{
			List<AllianceRecord> records = new List<AllianceRecord>();
			try
			{
				object manager = World.world == null ? null : GameRefs.ReadMember(World.world, "alliances");
				if (manager == null)
				{
					LogOnce("менеджер альянсов не найден (World.world.alliances)");
					return null;
				}
				// AllianceManager : CoreSystemManager<Alliance, AllianceData> — как kingdoms/cities: поле list,
				// сам менеджер перечисляем (IEnumerable<Alliance>).
				System.Collections.IEnumerable all = FirstEnumerable(manager,
					new[] { "getAlliances", "getList", "getEnumerable" },
					new[] { "list", "_list", "alliances_list", "p_alliances", "objects" })
					?? (manager as System.Collections.IEnumerable);
				System.Collections.IDictionary dict = GameRefs.ReadMember(manager, "dict") as System.Collections.IDictionary;
				if (all == null && dict == null)
				{
					LogOnce("список альянсов недоступен у " + manager.GetType().Name);
					return null;
				}
				List<object> candidates = new List<object>();
				if (all != null)
				{
					foreach (object item in all)
					{
						if (item != null && !candidates.Contains(item)) candidates.Add(item);
					}
				}
				if (dict != null)
				{
					foreach (object item in dict.Values)
					{
						if (item != null && !candidates.Contains(item)) candidates.Add(item);
					}
				}
				foreach (object alliance in candidates)
				{
					if (alliance == null)
					{
						continue;
					}
					if (GameRefs.ReadMember(alliance, "_alive") is bool alive && !alive)
					{
						continue;
					}
					if (GameRefs.Invoke(alliance, "isAlive") is bool isAlive && !isAlive)
					{
						continue;
					}
					string name = GameRefs.ReadMember(alliance, "name") as string
						?? GameRefs.ReadMember(GameRefs.ReadMember(alliance, "data"), "name") as string
						?? GameRefs.ReadMember(alliance, "p_name") as string;
					if (string.IsNullOrEmpty(name))
					{
						name = "Альянс " + AllianceSafety.StableId(alliance);
					}
					int stableId = AllianceSafety.StableId(alliance);
                    if (stableId < 0) { LogOnce("снимок альянсов недоступен: нет устойчивого id; порядковый номер не используется как личность"); return null; }
                    AllianceRecord record = new AllianceRecord { Id = stableId, Name = name };
					System.Collections.IEnumerable members = AllianceSafety.Members(alliance);
					if (members == null) return null;
                    if (members != null)
					{
						foreach (object member in members)
						{
							if (member is NanoObject nano)
							{
								record.Members.Add((int)nano.id);
							}
						}
					}
					// AllianceData.founder_kingdom_id — держава-основатель (есть в текущей игре).
					object data = GameRefs.ReadMember(alliance, "data");
					object founder = data == null ? null : GameRefs.ReadMember(data, "founder_kingdom_id");
					if (founder is long founderLong && founderLong > 0)
					{
						record.LeaderId = (int)founderLong;
					}
					else if (founder is int founderInt && founderInt > 0)
					{
						record.LeaderId = founderInt;
					}
					object leader = record.LeaderId > 0 ? null : (GameRefs.Invoke(alliance, "getLeader") ?? GameRefs.ReadMember(alliance, "leader"));
					if (leader is NanoObject leaderNano)
					{
						record.LeaderId = (int)leaderNano.id;
					}
					else if (record.LeaderId <= 0 && record.Members.Count > 0)
					{
						record.LeaderId = StrongestMember(members);
					}
					records.Add(record);
				}
				if (!_allianceApiLogged)
				{
					_allianceApiLogged = true;
					LogService.LogInfo("[Real Politic] альянсы игры читаются: " + records.Count + " шт.");
				}
			}
			catch (Exception ex)
			{
				LogService.LogInfo("[Real Politic] чтение альянсов: " + ex.Message);
                return null;
			}
			return records;
		}

		private static void LogOnce(string text)
		{
			if (_allianceApiLogged)
			{
				return;
			}
			_allianceApiLogged = true;
			LogService.LogInfo("[Real Politic] альянсы: " + text);
		}

		private static System.Collections.IEnumerable FirstEnumerable(object target, string[] methods, string[] members)
		{
			for (int i = 0; i < methods.Length; i++)
			{
				if (GameRefs.Invoke(target, methods[i]) is System.Collections.IEnumerable e && !(e is string))
				{
					return e;
				}
			}
			for (int i = 0; i < members.Length; i++)
			{
				if (GameRefs.ReadMember(target, members[i]) is System.Collections.IEnumerable e && !(e is string))
				{
					return e;
				}
			}
			return null;
		}

		private static int StrongestMember(System.Collections.IEnumerable members)
		{
			int best = -1;
			int bestCities = -1;
			foreach (object member in members)
			{
				if (!(member is Kingdom k))
				{
					continue;
				}
				int cities = 0;
				try
				{
					cities = GameRefs.KingdomCities(k)?.Count ?? 0;
				}
				catch (Exception)
				{
				}
				if (cities > bestCities)
				{
					bestCities = cities;
					best = (int)((NanoObject)k).id;
				}
			}
			return best;
		}

		/// <summary>0.24: поглощение-соединение — все города вступающего переходят лидеру;
		/// опустевшая держава исчезает сама (в игре у неё не остаётся городов).</summary>
		private void ApplyMerger(PendingMerger merger)
		{
			if (merger == null)
			{
				return;
			}
			Kingdom leader = _liveKingdoms.TryGetValue(merger.LeaderId, out Kingdom l) ? l : null;
			Kingdom member = _liveKingdoms.TryGetValue(merger.MemberId, out Kingdom m) ? m : null;
			if (leader == null || member == null)
			{
				return;
			}
			try
			{
				List<City> cities = GameRefs.KingdomCities(member);
				if (cities == null)
				{
					return;
				}
				// 0.24: статья XV «О федеративном устройстве» — статья XI «О чужой земле» не
				// запрещает рост; поглощение — добровольное соединение, а не завоевание.
				// Если игра отказалась отдать город — он остаётся у прежней державы: мир поправит сам.
				for (int i = 0; i < cities.Count; i++)
				{
					if (cities[i] == null)
					{
						continue;
					}
					CivilWarExecutor.MoveCityToKingdom(cities[i], leader);
				}
				LogService.LogInfo("[Real Politic] поглощение: города #" + merger.MemberId + " перешли державе #" + merger.LeaderId);
			}
			catch (Exception ex)
			{
				LogService.LogError("[Real Politic] поглощение: " + ex.Message);
			}
		}

		/// <summary>Этап 21: переводит все города державы-участника в державу лидера (интеграция).</summary>
		private void TransferKingdomCities(int fromId, int toId)
		{
			if (!_liveKingdoms.TryGetValue(fromId, out Kingdom fromKingdom) || fromKingdom == null
				|| !_liveKingdoms.TryGetValue(toId, out Kingdom toKingdom) || toKingdom == null)
			{
				return;
			}
			try
			{
				List<City> cities = GameRefs.KingdomCities(fromKingdom);
				if (cities == null)
				{
					return;
				}
				for (int i = 0; i < cities.Count; i++)
				{
					if (cities[i] == null)
					{
						continue;
					}
					object res = GameRefs.Invoke(cities[i], "setKingdom", toKingdom)
						?? GameRefs.Invoke(cities[i], "changeKingdom", toKingdom)
						?? GameRefs.Invoke(cities[i], "joinKingdom", toKingdom)
						?? GameRefs.Invoke(toKingdom, "addCity", cities[i]);
					if (res == null && cities[i].kingdom != null && !ReferenceEquals(cities[i].kingdom, toKingdom))
					{
						LogService.LogInfo("[Real Politic] интеграция: не удалось перевести город #" + (int)((NanoObject)cities[i]).id);
					}
				}
				LogService.LogInfo("[Real Politic] интеграция: города #" + fromId + " переданы державе #" + toId);
			}
			catch (Exception ex)
			{
				LogService.LogError("[Real Politic] интеграция городов: " + ex.Message);
			}
		}

		/// <summary>Этап 22: игра сообщила о завершении войны (победитель из данных войны или по потерям коалиций).
		/// Ядро оформляет перемирие и условия победителя; переводы городов и золота
		/// исполняются в ближайшем годовом цикле через ApplyDeepDiplomacyEffects.</summary>
		public void NotifyWarEnded(int attackerId, int defenderId, WarWinner winner, int warStartYear, int loserCitiesLeft = -1, WarCoalitionOutcome coalition = null)
		{
			if (_world == null || attackerId <= 0 || defenderId <= 0)
			{
				return;
			}
			// Слияние держав не является победой одной стороны — считаем мирным исходом.
			int winnerId = winner == WarWinner.Attackers ? attackerId
				: winner == WarWinner.Defenders ? defenderId : 0;
			int years = warStartYear > 0 ? Math.Max(1, _world.Year - warStartYear + 1) : 1;
			_world.ResolveWarEnd(attackerId, defenderId, winnerId, years, loserCitiesLeft, coalition);
		}

		/// <summary>Исполнение приказов на отделение земель. С 0.31.2 новую державу здесь
		/// может создать только вооружённое национальное освобождение или война вассала
		/// за независимость. Мирные и старые договорные приказы превращаются в
		/// поглощённые вассальные земли: города остаются у сюзерена, страна не появляется.</summary>
		private void ExecutePendingRestorations(MapBox map)
		{
			List<RestorationOrder> orders = _world.TakePendingRestorations();
			for (int i = 0; i < orders.Count; i++)
			{
				RestorationOrder order = orders[i];
				try
				{
					if (!order.IsNationalLiberation || order.LiberationPeaceful)
					{
						bool converted = _world.ConvertRestorationToAbsorbedVassal(order);
						LogService.LogInfo("[Real Politic] мирное восстановление «" + order.DefeatedName
							+ "» заменено поглощёнными вассальными землями: " + converted
							+ "; отдельное государство не создаётся");
						continue;
					}
					Kingdom victor = CivilWarExecutor.FindKingdom(map, order.VictorId);
					if (victor == null)
					{
						LogService.LogInfo("[Real Politic] восстановление " + order.DefeatedName + ": победитель #" + order.VictorId + " исчез");
						continue;
					}
					List<City> victorCities = GameRefs.KingdomCities(victor);
					if (victorCities == null)
					{
						continue;
					}
					// Довоенные города проигравшего, которые сейчас у победителя (в довоенном порядке: столица первой).
					List<City> restorable = new List<City>();
					for (int c = 0; c < order.PrewarCityIds.Count; c++)
					{
						int cityId = order.PrewarCityIds[c];
						for (int v = 0; v < victorCities.Count; v++)
						{
							if (victorCities[v] != null && (int)((NanoObject)victorCities[v]).id == cityId)
							{
								restorable.Add(victorCities[v]);
								break;
							}
						}
					}
					int keep;
					if (order.IsNationalLiberation)
					{
						keep = Math.Max(1, order.CitiesKeptByVictor);
						int liberatedCount = restorable.Count - keep;
						if (liberatedCount < 1 || victorCities.Count - liberatedCount < 1)
						{
							LogService.LogInfo("[Real Politic] освобождение " + order.DefeatedName + ": недостаточно городов для отделения без уничтожения метрополии");
							continue;
						}
						if (!string.IsNullOrWhiteSpace(order.DefeatedName) && IsKingdomNameAlreadyLive(map, order.DefeatedName))
						{
							LogService.LogInfo("[Real Politic] освобождение " + order.DefeatedName + ": держава с таким именем уже существует на карте");
							continue;
						}
					}
					else
					{
						keep = Math.Min(order.CitiesKeptByVictor, Math.Max(0, restorable.Count - 2));
						if (restorable.Count - keep < 2 || victorCities.Count - (restorable.Count - keep) < 1)
						{
							LogService.LogInfo("[Real Politic] восстановление " + order.DefeatedName + ": у победителя лишь " + restorable.Count + " довоенных городов — аннексия");
							continue;
						}
					}
					City seat = restorable[keep];
					if (!DiplomacyExecutor.TryCarveKingdomForWar(victor, seat, out Kingdom restored) || restored == null)
					{
						LogService.LogInfo("[Real Politic] восстановление " + order.DefeatedName + ": игра не создала державу (" + (GameRefs.LastInvokeError ?? "?") + ")");
						continue;
					}
					for (int c = keep + 1; c < restorable.Count; c++)
					{
						CivilWarExecutor.MoveCityToKingdom(restorable[c], restored);
					}
					string finalRestoredName = order.DefeatedName;
					if (!string.IsNullOrEmpty(order.DefeatedName) && !IsKingdomNameAlreadyLive(map, order.DefeatedName, restored))
					{
						GameRefs.TryRenameKingdom(restored, order.DefeatedName);
					}
					else if (!string.IsNullOrEmpty(((NanoObject)restored).name))
					{
						finalRestoredName = ((NanoObject)restored).name;
					}
					Actor leader = GameRefs.ReadProperty(seat, "leader") as Actor ?? GameRefs.Invoke(seat, "getLeader") as Actor;
					if (leader != null)
					{
						GameRefs.TrySetKingOnKingdom(restored, leader, quiet: true);
						GameRefs.TryReassignKingdom(leader, restored);
					}
					int newId = (int)((NanoObject)restored).id;
					if (order.IsNationalLiberation && !order.LiberationPeaceful)
					{
						bool warConfirmed = DiplomacyExecutor.AreAtWar(victor, restored)
							|| DiplomacyExecutor.TryStartVerifiedWar(restored, victor, rebellion: true);
						if (!warConfirmed)
						{
							// 0.31.12: освобождение не объявляется, пока игра не подтвердила войну.
							// Держава остаётся в игре, а объявление откладывается до подтверждения войны.
							_world.QueueIndependenceWar(newId, order.VictorId);
							_liveKingdoms[newId] = restored;
							List<int> deferredCityIds = new List<int>();
							for (int c = 0; c < restorable.Count; c++)
							{
								deferredCityIds.Add((int)((NanoObject)restorable[c]).id);
							}
							_deferredLiberations[newId] = new DeferredLiberation
							{
								Order = order,
								Name = finalRestoredName,
								CityIds = deferredCityIds,
								Keep = keep
							};
							LogService.LogError("[Real Politic] освобождение " + order.DefeatedName
								+ ": держава создана, но игра не подтвердила войну за независимость; объявление отложено до подтверждения войны");
							continue;
						}
					}
					else if (DiplomacyExecutor.AreAtWar(victor, restored))
					{
						DiplomacyExecutor.TryMakePeace(victor, restored);
					}
					_liveKingdoms[newId] = restored;
					// Ядро должно знать города до оформления статуса.
					for (int c = keep; c < restorable.Count; c++)
					{
						int cityId = (int)((NanoObject)restorable[c]).id;
						if (_world.GetCity(cityId) == null)
						{
							_world.AddCity(cityId, newId);
						}
					}
					order.CitiesKeptByVictor = keep; // фактическое число (могло уменьшиться)
					order.PrewarCityIds.Clear();
					for (int c = 0; c < restorable.Count; c++)
					{
						order.PrewarCityIds.Add((int)((NanoObject)restorable[c]).id);
					}
					_world.CompleteRestoration(order, newId, finalRestoredName);
					LogService.LogInfo("[Real Politic] восстановление " + finalRestoredName + " (" + order.Mode + "): новая держава #" + newId + ", городов: " + (restorable.Count - keep) + ", у победителя осталось: " + keep);
				}
				catch (Exception ex)
				{
					LogService.LogError("[Real Politic] восстановление державы: " + ex.Message);
				}
			}
		}

		private bool IsKingdomNameAlreadyLive(MapBox map, string candidateName, Kingdom except = null)
		{
			if (string.IsNullOrWhiteSpace(candidateName))
			{
				return false;
			}
			List<Kingdom> list = GameRefs.KingdomList(map);
			if (list != null)
			{
				for (int i = 0; i < list.Count; i++)
				{
					Kingdom k = list[i];
					if (k == null || ReferenceEquals(k, except))
					{
						continue;
					}
					string kName = ((NanoObject)k).name;
					if (!string.IsNullOrWhiteSpace(kName) && string.Equals(kName, candidateName, StringComparison.OrdinalIgnoreCase))
					{
						return true;
					}
				}
			}
			foreach (KingdomPolitics kp in _world.Kingdoms)
			{
				if (except != null && kp.KingdomId == (int)((NanoObject)except).id)
				{
					continue;
				}
				string kpName = _world.KingdomNameOf(kp.KingdomId);
				if (!string.IsNullOrWhiteSpace(kpName) && string.Equals(kpName, candidateName, StringComparison.OrdinalIgnoreCase))
				{
					return true;
				}
			}
			return false;
		}

		/// <summary>Этап 22: передача городов по мирному договору. Берутся самые недовольные
		/// города проигравшего (бунтующие первыми ложатся под победителя), столица и
		/// последний город проигравшего неприкосновенны.</summary>
		private void TransferCededCities(int fromId, int toId, int maxCount)
		{
			if (maxCount <= 0
				|| !_liveKingdoms.TryGetValue(fromId, out Kingdom fromKingdom) || fromKingdom == null
				|| !_liveKingdoms.TryGetValue(toId, out Kingdom toKingdom) || toKingdom == null)
			{
				return;
			}
			try
			{
				List<City> cities = GameRefs.KingdomCities(fromKingdom);
				// Нельзя отобрать все города — государство не должно погибнуть от договора.
				if (cities == null || cities.Count <= 1)
				{
					return;
				}
				City capital = GameRefs.KingdomCapital(fromKingdom) ?? cities[0];
				cities.Sort((x, y) => ReadCityLoyalty(x).CompareTo(ReadCityLoyalty(y)));
				int moved = 0;
				for (int i = 0; i < cities.Count && moved < maxCount; i++)
				{
					City city = cities[i];
					if (city == null || ReferenceEquals(city, capital))
					{
						continue;
					}
					// Всегда оставляем проигравшему хотя бы один город.
					if (cities.Count - moved <= 1)
					{
						break;
					}
					// WorldBox 0.51: полные сигнатуры с параметрами по умолчанию.
					object res = GameRefs.Invoke(city, "joinAnotherKingdom", toKingdom, false, false)
						?? GameRefs.Invoke(city, "setKingdom", toKingdom, false)
						?? GameRefs.Invoke(city, "changeKingdom", toKingdom)
						?? GameRefs.Invoke(city, "joinKingdom", toKingdom)
						?? GameRefs.Invoke(toKingdom, "addCity", city);
					if (res != null || (city.kingdom != null && ReferenceEquals(city.kingdom, toKingdom)))
					{
						moved++;
					}
					else
					{
						LogService.LogInfo("[Real Politic] мирный договор: не удалось перевести город #" + (int)((NanoObject)city).id);
					}
				}
				if (moved > 0)
				{
					LogService.LogInfo("[Real Politic] мирный договор: держава #" + fromId + " уступила " + moved + " городов державе #" + toId);
				}
			}
			catch (Exception ex)
			{
				LogService.LogError("[Real Politic] уступка городов по мирному договору: " + ex.Message);
			}
		}

		private static float ReadCityLoyalty(City city)
		{
			try
			{
				// В 0.51: getLoyalty(bool pForceRecalc = false) — зовём с явным аргументом.
				object value = GameRefs.Invoke(city, "getLoyalty", false);
				if (value is int loyalty)
				{
					return loyalty;
				}
				if (value is float f)
				{
					return f;
				}
			}
			catch (Exception)
			{
			}
			return 50f;
		}

		private void PlanStrategicActions(int year)
		{
			for (int i = 0; i < _kingdomInputs.Count; i++)
			{
				KingdomInput input = _kingdomInputs[i];
					KingdomPolitics state = _world.GetKingdom(input.KingdomId);
				if (state == null) continue;
				if (!_liveKingdoms.TryGetValue(input.KingdomId, out Kingdom liveKingdom) || liveKingdom == null) continue;
				if (_lastStrategyYear.TryGetValue(input.KingdomId, out int previousStrategyYear) && year - previousStrategyYear < 3) continue;
				int peaceful = 0;
				int hostile = 0;
				bool allianceOffer = false;
				if (input.Relations != null)
				{
					for (int r = 0; r < input.Relations.Count; r++)
					{
						RelationInfo relation = input.Relations[r];
						if (relation.Relation == RelationType.War) hostile++;
						else if (relation.Relation == RelationType.Peace || relation.Relation == RelationType.None) peaceful++;
						else if (relation.Relation == RelationType.Alliance) allianceOffer = true;
					}
				}
				IdeologyPowerDef power = IdeologyMechanics.Power(state.Ideology);
				FindMarriageCandidate(input, state, out int marriageCandidate, out bool marriageCandidateWar, out float marriageScore);
				FindTreatyCandidate(input, state, out int treatyCandidate, out TreatyType treatyType, out bool treatyCandidateWar, out float treatyScore);
				FindWarTarget(input, state, out int warTarget, out CasusBelliType warCb, out float warScore);
				FindCovertTarget(input, state, out int covertTarget, out SpyOpType covertOp, out float covertScore);
					GetVassalStatus(state, out int vassalOfId, out float vassalApproval, out bool suzerainAtWar);
					KingdomStrategySnapshot snapshot = new KingdomStrategySnapshot
				{
					KingdomId = input.KingdomId,
					Year = year,
					Ideology = state.Ideology,
					SuccessionLaw = state.SuccessionLaw,
					UnderRegency = (state.Regency != null && state.Regency.IsActive),
					Population = input.Population,
					Warriors = input.Warriors,
					MaxWarriors = GameRefs.KingdomMaxWarriors(liveKingdom),
					Cities = input.Cities == null ? 0 : input.Cities.Count,
					Food = ResourceOrSafeFloor(liveKingdom, "food", input.Starving ? 0f : input.Population * 2f, out bool foodKnown),
					Gold = ResourceOrSafeFloor(liveKingdom, "gold", 100f, out bool goldKnown),
					Stone = ResourceOrSafeFloor(liveKingdom, "stone", 80f, out _),
					Legitimacy = state.Legitimacy,
					ThreatLevel = state.Mechanics.ThreatLevel,
					Support = state.Support,
					AvgDissent = state.AvgDissent,
					AtWar = input.AtWar,
					Starving = input.Starving,
					PowerReady = state.Mechanics.CooldownLeft(power, year) == 0 && !state.Mechanics.IsActive(year),
					PowerActive = state.Mechanics.IsActive(year),
					LastActionYear = _lastStrategyYear.TryGetValue(input.KingdomId, out int last) ? last : int.MinValue,
					PeacefulNeighbors = peaceful,
					HostileNeighbors = hostile,
					HasAllianceOffer = allianceOffer,
					CivilWarActive = state.CivilWar != null,
					RebelShare = state.CivilWar != null ? 0.5f : 0f,
					EconomyEnabled = EconomyExecutor.EconomyEnabled,
					Prosperity = state.Economy?.Prosperity ?? 50f,
					TradeIncome = state.Economy?.LastIncome ?? 0f,
					TradeRoutes = _world.Trade.OpenRoutesOf(input.KingdomId),
					MaxTradeRoutes = _world.Params.MaxTradeRoutes,
					DeficitYears = state.Economy?.DeficitYears ?? 0,
					InEconomicCrisis = state.Economy?.InCrisis ?? false,
					AusterityActive = state.Economy?.AusterityActive(year) ?? false,
					EmbargoCandidate = hostile > 0,
					EmbargoActive = state.Economy?.EmbargoActive(year) ?? false,
					Trait = state.Personality?.Trait ?? RulerTrait.None,
					Ambition = state.Personality?.Ambition ?? 50f,
					DynastiesEnabled = _world.Params.EnableDynasties,
					MarriageCandidateId = marriageCandidate,
					MarriageCandidateWar = marriageCandidateWar,
					MarriageScore = marriageScore,
					TreatiesEnabled = _world.Params.EnableTreaties,
					TreatyCandidateId = treatyCandidate,
					TreatyType = treatyType,
					TreatyCandidateWar = treatyCandidateWar,
					TreatyScore = treatyScore,
					CasusBelliEnabled = _world.Params.EnableCasusBelli,
					WarTargetId = warTarget,
					WarCasusBelli = warCb,
					WarScore = warScore,
					EspionageEnabled = _world.Params.EnableEspionage,
					SpyLevel = state.Spy?.Level ?? 0,
					CovertTargetId = covertTarget,
					CovertOp = covertOp,
					CovertScore = covertScore,
					SpyInvestCost = _world.Params.SpyInvestCost,
					VassalOfId = vassalOfId,
					VassalApproval = vassalApproval,
					SuzerainAtWar = suzerainAtWar,
					VassalRebellionApproval = _world.Params.VassalRebellionApproval,
					InUnion = state.Union != null,
					IsUnionLeader = state.Union != null && state.Union.LeaderId == state.KingdomId,
					UnionType = state.Union?.Type ?? UnionType.Vassal,
					UnionMemberCount = state.Union?.MemberCount ?? 0,
					WarFatigue = state.WarFatigue,
					// 0.39: после поражения держава осторожна; обещания перед жителями помнят.
					WarAversionUntilYear = state.WarAversionUntilYear,
					PromiseScore = state.PromiseScore
				};
					StrategicCampaignPlan campaign = GrandAi.ManageCampaign(state, snapshot, _world, year);
					GrandAiReport grandReport = GrandAi.Evaluate(snapshot, state.Constitution, _world.Governance.Enabled ? _world.Governance.Recognition : null, campaign);
					_lastGrandAiReports[input.KingdomId] = grandReport;
					KingdomActionPlan plan = KingdomStrategicPlanner.Choose(snapshot);
					if (plan.Action == KingdomActionType.None && grandReport.SelectedPlan.Action != KingdomActionType.None)
					{
						plan = grandReport.SelectedPlan;
					}
					_lastStrategyPlans[input.KingdomId] = plan;
				if (RealPoliticMod.VerboseAiLog) LogService.LogInfo("[Real Politic] стратегический план #" + input.KingdomId + ": " + plan.Action + " score=" + plan.Score.ToString("F1") + " — " + plan.Reason + " [еда " + snapshot.Food.ToString("F0") + (foodKnown ? " (API)" : " (оценка: API недоступен)") + "/нас. " + snapshot.Population + ", голод=" + (snapshot.Starving ? "да" : "нет") + ", золото " + snapshot.Gold.ToString("F0") + (goldKnown ? " (API)" : " (оценка: API недоступен)") + "]");
					if (plan.Action == KingdomActionType.None)
					{
						_lastStrategyResults[input.KingdomId] = plan.Reason;
						continue;
					}
					string result;
					bool executed = KingdomActionExecutor.TryExecute(new GameKingdomActionExecutor(this, input), plan, snapshot, out result);
					if (executed)
					{
						GrandAi.NotifyStepExecuted(state, plan.Action, _world, year);
					}
					_lastStrategyResults[input.KingdomId] = (executed ? GameText.Localized("RP_RES_PREFIX_OK", "успех: ") : GameText.Localized("RP_RES_PREFIX_FAIL", "отказ: ")) + result;
					_lastStrategyYear[input.KingdomId] = year;
				LogService.LogInfo("[Real Politic] стратегическое действие #" + input.KingdomId + ": " + (executed ? "выполнено" : "отложено") + " — " + result);
			}
		}

		/// <summary>Возвращает последний объяснимый план для UI окна государства.</summary>
		public bool TryGetStrategicPlan(int kingdomId, out KingdomActionPlan plan)
		{
			return _lastStrategyPlans.TryGetValue(kingdomId, out plan);
		}

		/// <summary>Возвращает последний многовекторный отчёт Большого ИИ (Grand AI) для государства.</summary>
		public bool TryGetGrandAiReport(int kingdomId, out GrandAiReport report)
		{
			return _lastGrandAiReports.TryGetValue(kingdomId, out report);
		}

		/// <summary>Возвращает результат последней попытки выполнения плана для UI и диагностики.</summary>
		public bool TryGetStrategicResult(int kingdomId, out string result)
		{
			return _lastStrategyResults.TryGetValue(kingdomId, out result);
		}

		/// <summary>Возвращает компактную внешнеполитическую сводку для окна государства.</summary>
		public bool TryGetDiplomacySummary(int kingdomId, out string summary)
		{
			return _diplomacySummaries.TryGetValue(kingdomId, out summary);
		}

		/// <summary>Возвращает подробную сводку активного союза/унии для окна государства.</summary>
		public bool TryGetUnionSummary(int kingdomId, out string summary)
		{
			summary = string.Empty;
			Union union = _world.UnionOf(kingdomId);
			if (union == null)
			{
				return false;
			}
			string unionTypeStr = GameText.Localized(Ideologies.UnionTypeKey(union.Type), union.Type.ToString());
			bool isLeader = union.LeaderId == kingdomId;
			string roleStr = isLeader
				? GameText.Localized("RP_UNION_ROLE_LEADER", "Лидер")
				: GameText.Localized("RP_UNION_ROLE_MEMBER", "Участник");
			int cohesionPct = (int)Math.Round(union.Cohesion * 100f);
			string leaderName = GameNotifier.KingdomName(World.world, union.LeaderId);

			summary = "«" + UnionDisplayName(union) + "» (" + unionTypeStr + ") | " + roleStr;
			if (!isLeader && !string.IsNullOrEmpty(leaderName))
			{
				summary += " [" + leaderName + "]";
			}
			summary += " | " + union.MemberCount + " " + GameText.Localized("RP_UNION_MEMBERS_SHORT", "уч.")
				+ " | " + cohesionPct + "%";
			return true;
		}

		/// <summary>Этап 19: личность правителя — характер, амбиции и династический союз для окна государства.</summary>
		public bool TryGetPersonalitySummary(int kingdomId, out string summary)
		{
			summary = string.Empty;
			KingdomPolitics state = _world.GetKingdom(kingdomId);
			if (state == null || state.Personality == null)
			{
				return false;
			}
			RulerTrait trait = state.Personality.Trait;
			string traitStr = GameText.Localized(RulerTraits.LocKey(trait), trait.ToString());
			summary = traitStr + " • " + GameText.Localized("RP_AMBITION_SHORT", "Амб.") + " "
				+ (int)state.Personality.Ambition + "%";
			if (state.Marriage != null)
			{
				string partner = _world.KingdomNameOf(state.Marriage.PartnerId);
				if (!string.IsNullOrEmpty(partner))
				{
					summary += " • " + GameText.Localized("RP_MARRIED_SHORT", "в браке:") + " " + partner;
				}
			}
			return true;
		}

		/// <summary>Возвращает последнее значимое политическое событие государства для UI летописи.</summary>
		public bool TryGetLatestKingdomEvent(int kingdomId, out string eventText)
		{
			eventText = string.Empty;
			IReadOnlyList<PolEvent> events = History.Events;
			if (events == null || events.Count == 0)
			{
				return false;
			}
			for (int i = events.Count - 1; i >= 0; i--)
			{
				PolEvent evt = events[i];
				if (evt == null || evt.KingdomId != kingdomId)
				{
					continue;
				}
				if (evt.Type == PolEventType.ActorConverted)
				{
					continue;
				}
				string rendered = RealPoliticMod.Notifier != null ? RealPoliticMod.Notifier.RenderEventText(evt) : string.Empty;
				if (!string.IsNullOrEmpty(rendered))
				{
					string yearLabel = GameText.Localized("RP_YEAR_LABEL", "Год");
					eventText = "[" + yearLabel + " " + evt.Year + "] " + rendered;
					return true;
				}
			}
			return false;
		}

		private string BuildDiplomacySummary(KingdomInput input)
		{
			int wars = 0;
			int alliances = 0;
			int peaceful = 0;
			if (input.Relations != null)
			{
				for (int i = 0; i < input.Relations.Count; i++)
				{
					switch (input.Relations[i].Relation)
					{
						case RelationType.War: wars++; break;
						case RelationType.Alliance: alliances++; break;
						case RelationType.Peace: peaceful++; break;
					}
				}
			}
			Union union = _world.UnionOf(input.KingdomId);
			string text = GameText.Localized("RP_DIPLOMACY_WARS", "Войны") + ": " + wars + " | "
				+ GameText.Localized("RP_DIPLOMACY_ALLIANCES", "Союзы") + ": " + alliances + " | "
				+ GameText.Localized("RP_DIPLOMACY_PEACE", "Мир") + ": " + peaceful;
			if (union != null && !RealPoliticMod.ShowUnionUI)
			{
				text += " | " + GameText.Localized("RP_DIPLOMACY_UNION", "Уния") + ": " + union.Name;
			}
			// Этап 20: договоры и разведсеть в дипломатической сводке.
			int treaties = _world.TreatiesBook.Of(input.KingdomId).Count;
			if (treaties > 0)
			{
				text += " | " + GameText.Localized("RP_DIPLOMACY_TREATIES", "Договоры") + ": " + treaties;
			}
			KingdomPolitics state = _world.GetKingdom(input.KingdomId);
			int spyLevel = state?.Spy?.Level ?? 0;
			if (spyLevel > 0)
			{
				text += " | " + GameText.Localized("RP_DIPLOMACY_SPY", "Разведка") + ": " + spyLevel;
			}
			return text;
		}

		private static float ResourceOrSafeFloor(Kingdom kingdom, string resource, float fallback,out bool known)
        {
            known=GameRefs.TryKingdomResource(kingdom,resource,out float value);
            return known?value:fallback;
        }

		private void PlanDiplomacy()
	{
		if (!Patches.DiplomacyEnabled || !GameRefs.IsDiplomacyWorldLawEnabled())
		{
			return;
		}
		_diplomatSnapshots.Clear();
		_diplomatSnapshotList.Clear();
		for (int i = 0; i < _kingdomInputs.Count; i++)
		{
			KingdomInput input = _kingdomInputs[i];
			KingdomPolitics state = _world.GetKingdom(input.KingdomId);
				if (state == null || input.Cities == null || input.Cities.Count == 0)
				{
					continue;
				}
				DiplomatSnapshot snapshot = new DiplomatSnapshot
			{
				KingdomId = input.KingdomId,
				Ideology = state.Ideology,
				Stance = state.Stance,
				Legitimacy = state.Legitimacy,
				Population = input.Population,
				Warriors = input.Warriors,
				Starving = input.Starving,
				AtWar = input.AtWar,
				ReligionId = FirstReligion(input),
				UnderRegency = (state.Regency != null && state.Regency.IsActive),
				RaceId = input.RaceId ?? string.Empty,
				Levy = state.Constitution?.Levy ?? LevyLaw.Conscription,
				BindingConstitution = state.Constitution != null && state.Constitution.Supremacy == SupremacyRule.Binding
			};
			_diplomatSnapshots[input.KingdomId] = snapshot;
			_diplomatSnapshotList.Add(snapshot);
		}
		for (int i = 0; i < _kingdomInputs.Count; i++)
		{
			KingdomInput input = _kingdomInputs[i];
			if (input.Relations == null || input.Relations.Count == 0)
			{
				continue;
			}
			if (!_diplomatSnapshots.TryGetValue(input.KingdomId, out DiplomatSnapshot self))
			{
				continue;
			}
			if (_lastDiplomacyYear.TryGetValue(input.KingdomId, out int lastYear) && _world.Year - lastYear < DiplomacyCooldownYears)
			{
				continue;
			}
			// BestMove пропускает self.KingdomId внутри советника, поэтому один общий
			// список снимков безопасен и не создаёт O(N) новых списков на каждую державу.
			DiplomaticMove diplomaticMove = DiplomaticAdvisor.BestMove(self, _diplomatSnapshotList, input.Relations);
			if (diplomaticMove != null)
			{
				if (RealPoliticMod.VerboseAiLog) LogService.LogInfo("[Real Politic] дипломатический совет: государство #" + input.KingdomId + " → " + diplomaticMove.Action.ToString() + " #" + diplomaticMove.TargetId + " (оценка " + diplomaticMove.Score.ToString("F1") + ")");
				TryExecute(input.KingdomId, diplomaticMove);
			}
		}
		}

		/// <summary>
		/// Исполняет локальный стратегический план после проверки ядром.
		/// Метод публичен только как узкий мост Game-адаптера и не раскрывает внутренние коллекции мира.
		/// </summary>
		public bool TryExecuteStrategicState(int kingdomId, KingdomActionPlan plan, KingdomStrategySnapshot snapshot, out string result)
		{
			try
			{
				if (plan == null || snapshot == null || !_liveKingdoms.TryGetValue(kingdomId, out Kingdom kingdom))
				{
					result = GameText.Localized("RP_RES_STATE_OR_PLAN_UNAVAILABLE", "государство или план недоступны");
					return false;
				}
				if (plan.IsFallback || snapshot.KingdomId != kingdomId)
				{
					result = GameText.Localized("RP_RES_ACTION_NOT_APPLICABLE", "действие не требует исполнения или относится к другому государству");
					return false;
				}
				KingdomPolitics state = _world.GetKingdom(kingdomId);
				if (state == null)
				{
					result = GameText.Localized("RP_RES_POLITICS_UNAVAILABLE", "политическое состояние государства недоступно");
					return false;
				}
				// Экономические действия работают с картой и сетью путей,
				// поэтому исполняются здесь, а не в эффектах идеологий.
				switch (plan.Action)
				{
					case KingdomActionType.DevelopTrade:
						return EconomyExecutor.DevelopTrade(World.world, kingdom, state, _world, snapshot.Year, _world.Params, out result);
					case KingdomActionType.Austerity:
						return EconomyExecutor.StartAusterity(kingdom, state, snapshot.Year, _world.Params, out result);
					case KingdomActionType.TradeEmbargo:
						return EconomyExecutor.StartEmbargo(World.world, kingdom, state, _world, snapshot.Year, _world.Params, out result);
					case KingdomActionType.ProposeMarriage:
						// Брак — чисто политическое действие: исполняется ядром без игрового API.
						return _world.TryMarry(kingdomId, snapshot.MarriageCandidateId, snapshot.MarriageCandidateWar, out result);
					case KingdomActionType.SignTreaty:
						// Договор — политическое действие: исполняется ядром.
						return _world.TrySignTreaty(kingdomId, snapshot.TreatyCandidateId, snapshot.TreatyType,
							snapshot.TreatyCandidateWar, out result);
					case KingdomActionType.InvestEspionage:
						return _world.InvestEspionage(kingdomId, snapshot.Gold, out result);
					case KingdomActionType.CovertOperation:
						// Тайная операция: последствия применяет ядро, кражу казны исполнит игровой слой.
						return _world.TryCovertOp(kingdomId, snapshot.CovertTargetId, snapshot.CovertOp, out result);
					case KingdomActionType.DeclareWar:
					{
						if (!_liveKingdoms.TryGetValue(snapshot.WarTargetId, out Kingdom targetKingdom) || targetKingdom == null)
						{
							result = GameText.Localized("RP_RES_TARGET_GONE", "держава-цель исчезла");
							return false;
						}
						if (AllianceSafety.Same(kingdom, targetKingdom) || SameUnion(kingdomId, snapshot.WarTargetId))
						{
							result = GameText.Localized("RP_RES_TARGET_ALLIED", "цель состоит в одном альянсе — сначала нужен разрыв или исключение");
							return false;
						}
						if (_world.TreatiesBook.NonAggressionActive(kingdomId, snapshot.WarTargetId))
						{
							result = GameText.Localized("RP_RES_NAP_FORBIDS_WAR", "пакт о ненападении запрещает войну");
							return false;
						}
						// 0.44: тот же замок, что и у советника — общая граница и расчёт сил.
						// План их уже проверяет (FindWarTarget 0.39–0.40), но проверка стоит
						// в одном месте для всех путей: так «дыра» не вернётся при правках.
						if (!WarSanityAllows(kingdomId, snapshot.WarTargetId, snapshot.WarCasusBelli, out string warSanityWhy))
						{
							WarsBlockedBySanity++;
							result = warSanityWhy;
							return false;
						}
						string defenderName = targetKingdom?.data?.name ?? snapshot.WarTargetId.ToString();
						if (!_world.TryConstitutionalWar(kingdomId, snapshot.WarTargetId, defenderName, out string constitutionalWhy))
						{
							result = constitutionalWhy;
							return false;
						}
						DiplomaticMove move = new DiplomaticMove
						{
							Action = DiplomaticAction.DeclareWar,
							TargetId = snapshot.WarTargetId,
							Score = plan.Score
						};
						bool declared = DiplomacyExecutor.Execute(move, kingdom, targetKingdom);
						if (!declared)
						{
							result = GameText.Localized("RP_RES_WAR_API_REFUSED", "игровой API отклонил объявление войны");
							return false;
						}
						_world.ApplyDeclareWar(kingdomId, snapshot.WarTargetId, snapshot.WarCasusBelli);
						result = GameText.Localized("RP_RES_WAR_DECLARED", "война объявлена");
						return true;
					}
					case KingdomActionType.DeclareIndependence:
					{
						// Независимость — это война за независимость. Порядок строгий: сначала мандат
						// парламента/народа, затем настоящая война в игре, и только потом разрыв унии.
						// Если войны нет — нет и «успеха»: вассал остаётся вассалом.
						if (!_liveKingdoms.TryGetValue(snapshot.VassalOfId, out Kingdom suzerainKingdom) || suzerainKingdom == null)
						{
							result = GameText.Localized("RP_ACT_SUZERAIN_MISSING", "сюзерен не найден в игре");
							return false;
						}
						string suzerainName = suzerainKingdom.data?.name ?? snapshot.VassalOfId.ToString();
						if (!_world.TryConstitutionalWar(kingdomId, snapshot.VassalOfId, suzerainName, out string independenceWhy))
						{
							result = GameText.Localized("RP_ACT_INDEPENDENCE_NO_MANDATE", "война за независимость не получила мандата") + ": " + independenceWhy;
							return false;
						}
						DiplomaticMove move = new DiplomaticMove
						{
							Action = DiplomaticAction.DeclareWar,
							TargetId = snapshot.VassalOfId
						};
						if (!DiplomacyExecutor.Execute(move, kingdom, suzerainKingdom))
						{
							result = GameText.Localized("RP_ACT_INDEPENDENCE_NO_WAR", "игра не позволила объявить войну сюзерену");
							return false;
						}
						if (!_world.LeaveUnionForIndependence(kingdomId, out result))
						{
							return false;
						}
						_world.ApplyDeclareWar(kingdomId, snapshot.VassalOfId, CasusBelliType.Independence);
						result = GameText.Localized("RP_ACT_INDEPENDENCE_WAR", "война за независимость объявлена");
						return true;
					}
					case KingdomActionType.IntegrateMember:
						// 0.24: интеграция упразднена — поглощение происходит самим вступлением в унию.
						result = CoreText.L("RP_RES_INTEGRATION_REMOVED", "поглощение происходит при вступлении в унию");
						return false;
				}
				return IdeologyMechanicsEffects.TryApplyStrategicAction(kingdom, state, plan.Action, snapshot.Year, out result);
			}
			catch (Exception ex)
			{
				result = "ошибка локального стратегического действия: " + ex.GetType().Name;
				LogService.LogError("[Real Politic] " + result);
				return false;
			}
		}

		public bool TryExecuteStrategicDiplomacy(int actorId, int targetId, DiplomaticAction action, float score)
		{
			try
			{
				if (actorId == targetId || action == DiplomaticAction.Hold
					|| !_liveKingdoms.ContainsKey(actorId) || !_liveKingdoms.ContainsKey(targetId)) return false;
				DiplomaticMove move = new DiplomaticMove { Action = action, TargetId = targetId, Score = score };
				Kingdom actor = _liveKingdoms[actorId];
				Kingdom target = _liveKingdoms[targetId];
				if (IsWild(actor) || IsWild(target)) return false;
				TryExecute(actorId, move);
				return _lastDiplomacyYear.TryGetValue(actorId, out int year) && year == _world.Year;
			}
			catch (Exception ex)
			{
				LogService.LogError("[Real Politic] стратегическая дипломатия пропущена: " + ex.Message);
				return false;
			}
		}

		private void TryExecute(int actorId, DiplomaticMove move)
	{
		if (!_liveKingdoms.TryGetValue(actorId, out Kingdom actor))
		{
			return;
		}
		if (!_liveKingdoms.TryGetValue(move.TargetId, out Kingdom target))
		{
			return;
		}
		if (IsWild(actor) || IsWild(target))
		{
			// Дикие кланы в дипломатии не участвуют.
			return;
		}
		int year = _world.Year;
		if (_lastDiplomacyYear.TryGetValue(actorId, out int lastYear) && year - lastYear < DiplomacyCooldownYears)
		{
			// Перерыв между дипломатическими шагами одного государства.
			return;
		}
		bool done;
		switch (move.Action)
		{
			case DiplomaticAction.DemandVassal:
			{
				// Вассалитет: создаём вассальную унию в ядре (с запоминанием границ вассала)
				// и вливаем города вассала в державу сюзерена.
				bool unionOk = Patches.UnionsEnabled && TryFormUnion(actorId, move.TargetId, UnionType.Vassal);
				if (unionOk)
				{
					_world.RecordMergedIntoSuzerain(move.TargetId);
					ApplyMerger(new PendingMerger
					{
						LeaderId = actorId,
						MemberId = move.TargetId,
						MemberName = _world.KingdomNameOf(move.TargetId)
					});
				}
				done = unionOk;
				break;
			}
			case DiplomaticAction.ProposeAlliance:
			{
                if(_world.Governance.Enabled)
                {
                    if (AllianceSafety.Same(actor, target))
                    {
                        bool merged = Patches.UnionsEnabled && TryFormUnion(actorId, move.TargetId, UnionType.Merger);
                        if (merged)
                        {
                            List<PendingMerger> pendingMergers = _world.TakePendingMergers();
                            for (int m = 0; m < pendingMergers.Count; m++)
                            {
                                ApplyMerger(pendingMergers[m]);
                            }
                            _lastDiplomacyYear[actorId] = _world.Year;
                        }
                        done = merged;
                        break;
                    }
                    bool requested = CivicAllianceRuntime.RequestAdmission(_world,actor,target,RealPoliticMod.CurrentYear());
                    CivicAllianceMotions.Tick(_world,RealPoliticMod.CurrentYear(),new CivicAllianceRuntime(_world));
                    if (requested || AllianceSafety.Same(actor, target))
                    {
                        _lastDiplomacyYear[actorId] = _world.Year;
                    }
                    done=false;break; // A proposed alliance never silently becomes a political merger.
                }
				// 0.24: союз между державами = соединяющая уния — вступивший тотчас поглощается.
				// Игра отражает это альянсом; ядро оформляет поглощение (статьей XV допускается).
				done = DiplomacyExecutor.Execute(move, actor, target);
				if (done && Patches.UnionsEnabled)
				{
					TryFormUnion(actorId, move.TargetId, UnionType.Merger);
				}
				break;
			}
			case DiplomaticAction.DeclareWar:
			{
				if (AllianceSafety.Same(actor, target) || SameUnion(actorId, move.TargetId))
				{
					_lastDiplomacyYear[actorId] = year;
					return;
				}
				// Этап 20: пакт о ненападении запрещает войну даже советнику.
				if (_world.TreatiesBook.NonAggressionActive(actorId, move.TargetId))
				{
					LogService.LogInfo("[Real Politic] объявление войны #" + actorId + " → #" + move.TargetId + " заблокировано пактом о ненападении");
					return;
				}
				int claims = 0;
				if (TryGetInput(actorId, out KingdomInput actorInput) && actorInput.Relations != null)
				{
					for (int r = 0; r < actorInput.Relations.Count; r++)
					{
						if (actorInput.Relations[r].OtherId == move.TargetId)
						{
							claims = actorInput.Relations[r].Claims;
							break;
						}
					}
				}
				CasusBelliType cb = _world.BestCasusBelli(actorId, move.TargetId, claims);
				// 0.44: советник больше не воюет «вслепую».
				// Владелец 29.10.2026: «где их мозг, когда они до 50-го года объявляют войны
				// хер пойми кому, у них даже общей границы нет». Проверка нашла дыру:
				// стратегический план правителя проверял и соседство, и силы (0.39–0.40),
				// а ЭТОТ путь — советник — объявлял войну без обеих проверок.
				// Даже для держав, дипломатия с которыми неизвестна (Relation.None).
				// Поэтому здесь стоят те же два замка: общая граница и расчёт сил.
				if (!WarSanityAllows(actorId, move.TargetId, cb, out string sanityWhy))
				{
					_lastDiplomacyYear[actorId] = year;
					WarsBlockedBySanity++;
					LogService.LogInfo("[Real Politic] война #" + actorId + " → #" + move.TargetId
						+ " отклонена: " + sanityWhy);
					return;
				}
				// Этап 26: советник подчиняется той же конституции, что и стратегический план.
				string targetName = target?.data?.name ?? move.TargetId.ToString();
				if (!_world.TryConstitutionalWar(actorId, move.TargetId, targetName, out string constitutionalWhy))
				{
					_lastDiplomacyYear[actorId] = year;
					LogService.LogInfo("[Real Politic] объявление войны #" + actorId + " → #" + move.TargetId + " заблокировано: " + constitutionalWhy);
					return;
				}
				done = DiplomacyExecutor.Execute(move, actor, target);
				if (done)
				{
					_world.ApplyDeclareWar(actorId, move.TargetId, cb);
				}
				break;
			}
			default:
				done = DiplomacyExecutor.Execute(move, actor, target);
				break;
		}
		if (done)
		{
			_lastDiplomacyYear[actorId] = year;
			LogService.LogInfo("[Real Politic] дипломатия выполнена: #" + actorId + " " + move.Action.ToString() + " → #" + move.TargetId);
		}
	}

	/// <summary>Этап 23: ежегодное исполнение механик границ, рейдов и династических фамилий.
	/// Решения принимает ядро (ApplyBorderPolicy/TryPowerRaid), здесь — детерминированные
	/// правила правителей-ИИ и физика игры; всё безопасно при деградации API.</summary>
	private void ApplyStage23Effects(MapBox world, int year)
	{
		try
		{
			BorderAutoPolicy(year);
			EnforceClosedBorders(world);
			RunPowerRaids(world);
			RenameSurnameClans();
		}
		catch (Exception ex)
		{
			LogService.LogError("[Real Politic] этап 23: " + ex.Message);
		}
	}

	/// <summary>ИИ-правители: война и тяжёлая обида закрывают границу; союз/торговля — открывают.</summary>
	private void BorderAutoPolicy(int year)
	{
		if (!_world.Params.EnableBorders)
		{
			return;
		}
		for (int i = 0; i < _kingdomInputs.Count; i++)
		{
			KingdomInput input = _kingdomInputs[i];
			if (input.Relations == null)
			{
				continue;
			}
			for (int j = 0; j < input.Relations.Count; j++)
			{
				RelationInfo rel = input.Relations[j];
				// Пары считаем один раз: только когда наш id меньше.
				if (rel.OtherId <= input.KingdomId || !rel.Adjacent)
				{
					continue;
				}
				int a = input.KingdomId;
				int b = rel.OtherId;
				bool hostile = rel.Relation == RelationType.War
					|| _world.GetGrievance(a, b) >= 25f || _world.GetGrievance(b, a) >= 25f;
				TradeRoute route = _world.Trade.Find(a, b);
				bool friendly = rel.Relation == RelationType.Alliance || (route != null && !route.Blocked);
				if (hostile && !_world.BordersBook.ClosedBetween(a, b))
				{
					if (_world.ApplyBorderPolicy(a, b, BorderStatus.Closed))
					{
						// Закрытие границ рвёт торговые пути пары.
						int severed = _world.Trade.BlockBetween(a, b);
						if (severed > 0)
						{
							_world.Bus.Publish(new PolEvent(PolEventType.TradeRoutesSevered, "RP_EV_TRADE_ROUTES_SEVERED")
							{
								Year = _world.Year,
								Severity = Severity.Minor,
								KingdomId = a
							}.With("kingdom", _world.KingdomNameOf(a))
								.With("partner", _world.KingdomNameOf(b))
								.With("count", severed));
						}
					}
				}
				else if (!hostile && _world.BordersBook.ClosedBetween(a, b))
				{
					// 0.23.2: открыть границу — долгие переговоры: союз/уния или торговый путь,
					// без обид, без перемирия, обе власти крепкие, N лет подряд.
					bool allied = rel.Relation == RelationType.Alliance || SameUnion(a, b);
					// Торговый путь (даже перекрытый закрытой границей) — свидетельство деловых связей.
					bool trade = route != null;
					if (_world.TryNegotiateBorderOpening(a, b, allied, rel.Relation == RelationType.War, trade))
					{
						_world.Trade.UnblockBetween(a, b);
					}
				}
			}
		}
	}

	private bool SameUnion(int a, int b)
	{
		Union ua = _world.GetKingdom(a)?.Union;
		Union ub = _world.GetKingdom(b)?.Union;
		return ua != null && ub != null && ReferenceEquals(ua, ub);
	}

	/// <summary>Чужаков из закрытых пар выдворяют домой, самих чужаков карают местные.
	/// 0.23.10: облавы подчиняются КОНСТИТУЦИИ — закон «границы закрыты» (Binding) принудительно
	/// закрывает пару и включает высылку, закон «границы открыты» (Binding) снимает облаву
	/// (раньше книга границ жила отдельно от грамоты, и «синие» гуляли по «белым» при закрытых
	/// по конституции границах — когда пара открылась торговлей ещё до принятия закона).</summary>
	private void EnforceClosedBorders(MapBox world)
	{
		if (!_world.Params.EnableBorders)
		{
			return;
		}
		// 0.23.10: писаная норма сначала возвращает пары в закрытый режим.
		SyncConstitutionalBorders();
		// 0.23.2: границы закрыты по умолчанию — обходим все соседние пары живых держав,
		// а не только те, у кого есть запись в книге границ. Далёкие (не соседи) пары пропускаем:
		// их жители друг к другу и так почти не ходят, а обход всех пар дорог.
		HashSet<long> done = new HashSet<long>();
		for (int i = 0; i < _kingdomInputs.Count; i++)
		{
			KingdomInput input = _kingdomInputs[i];
			if (input.Relations == null)
			{
				continue;
			}
			for (int j = 0; j < input.Relations.Count; j++)
			{
				RelationInfo rel = input.Relations[j];
				if (!rel.Adjacent || rel.OtherId == input.KingdomId)
				{
					continue;
				}
				long key = PairKey.Of(input.KingdomId, rel.OtherId);
				if (!done.Add(key) || !_world.BordersBook.ClosedBetween(input.KingdomId, rel.OtherId))
				{
					continue;
				}
				// Союзники и члены одной унии по закрытой границе друг друга не гоняют — только чужаки.
				if (SameUnion(input.KingdomId, rel.OtherId))
				{
					continue;
				}
				// 0.23.13: закон «границы открыты» (Binding) выключает облаву У САМОЙ ДЕРЖАВЫ,
				// которая её проводит (а не у соседа). Раньше проверка смотрела на конституцию
				// противоположной стороны: хозяин с «открытыми границами» продолжал выдворять
				// чужаков, а законный «закрытый» сосед терял облаву из-за чужой грамоты.
				// Проверка перенесена внутрь EnforceOneDirection (гасит облаву хоста).
				EnforceOneDirection(world, input.KingdomId, rel.OtherId);
				EnforceOneDirection(world, rel.OtherId, input.KingdomId);
			}
		}
		// Явно закрытые несоседние пары (решение правителя) — как раньше.
		foreach (KeyValuePair<long, BorderRecord> pair in _world.BordersBook.All)
		{
			if (pair.Value.Status != BorderStatus.Closed || !done.Add(pair.Key))
			{
				continue;
			}
			EnforceOneDirection(world, PairKey.A(pair.Key), PairKey.B(pair.Key));
			EnforceOneDirection(world, PairKey.B(pair.Key), PairKey.A(pair.Key));
		}
	}

	/// <summary>Запрещена ли державе облава конституцией «границы открыты» (Binding).
	/// Обычай (Advisory), пустая грамота, «регулируемые» и «закрытые» — облава разрешена.</summary>
	private bool BorderRaidsForbidden(int kingdomId)
	{
		return _world.Params != null && _world.Params.EnableConstitution
			&& _world.GetKingdom(kingdomId)?.Constitution is Constitution c
			&& !ConstitutionGates.BorderEnforcementAllowed(c);
	}

	/// <summary>0.23.10: норма «границы закрыты» (Binding) принудительно закрывает пару с каждым
	/// соседом — даже если книга границ помнит открытый режим от старой торговли/союза. Это и есть
	/// тот случай, когда синие гуляли по белым при прописанных закрытых границах.</summary>
	private void SyncConstitutionalBorders()
	{
		if (!_world.Params.EnableConstitution)
		{
			return;
		}
		for (int i = 0; i < _kingdomInputs.Count; i++)
		{
			KingdomInput input = _kingdomInputs[i];
			if (input.Relations == null)
			{
				continue;
			}
			KingdomPolitics state = _world.GetKingdom(input.KingdomId);
			if (state == null || state.Constitution == null || !ConstitutionGates.BorderMustBeClosed(state.Constitution))
			{
				continue;
			}
			for (int j = 0; j < input.Relations.Count; j++)
			{
				RelationInfo rel = input.Relations[j];
				if (rel.OtherId == input.KingdomId || !rel.Adjacent)
				{
					continue;
				}
				if (!_world.BordersBook.ClosedBetween(input.KingdomId, rel.OtherId))
				{
					_world.ApplyBorderPolicy(input.KingdomId, rel.OtherId, BorderStatus.Closed);
				}
			}
		}
	}

	private void EnforceOneDirection(MapBox world, int hostId, int guestId)
	{
		try
		{
			if (hostId == guestId
				|| !_liveKingdoms.TryGetValue(hostId, out Kingdom host) || host == null
				|| !_liveKingdoms.TryGetValue(guestId, out Kingdom guest) || guest == null)
			{
				return;
			}
			// 0.23.13: закон «границы открыты» (Binding) есть у ХОЗЯИНА — значит он сам
			// не проводит облаву. При «обычае», «регулируемых» и «закрытых» — облава идёт.
			if (BorderRaidsForbidden(hostId))
			{
				return;
			}
			int reported = 0;
			List<City> cities = GameRefs.KingdomCities(host);
			if (cities == null || cities.Count == 0)
			{
				return;
			}
			object homeTile = null;
			List<City> guestCities = GameRefs.KingdomCities(guest);
			if (guestCities != null && guestCities.Count > 0)
			{
				homeTile = GameRefs.CityTile(guestCities[0]);
			}
			Actor guestRuler = GameRefs.ReadRuler(guest);
			// Подданные гостя — прямым списком (дёшево). 0.23.9: пропускали тех, кто
			// после захвата их города ещё числился в чужой столице; теперь список читается
			// свежим, а воинов (армии), правителей и корабли не трогаем.
			List<Actor> guests = ((MetaObject<KingdomData>)(object)guest).units;
			if (guests == null || guests.Count == 0)
			{
				return;
			}
			for (int u = 0; u < guests.Count; u++)
			{
				Actor alien = guests[u];
				if (alien == null || ReferenceEquals(alien, guestRuler) || GameRefs.IsBoatUnit(alien) || GameRefs.IsWarrior(alien))
				{
					continue;
				}
				// 0.23.2: точная проверка — по настоящей территории игры (tile → zone → city → kingdom),
				// то есть по тем же зонам, что закрашиваются цветом державы. Радиус — только если зона недоступна.
				int onLand = TerritoryOwnerId(alien);
				if (onLand > 0)
				{
					if (onLand != hostId)
					{
						continue; // своя или третья земля — не на территории хозяина
					}
				}
				else
				{
					if (!TryGetActorTile(alien, out int ax, out int ay) || !InsideZones(cities, ax, ay))
					{
						continue;
					}
				}
				bool expelled = GameRefs.TryExpelActor(alien, homeTile, _world.Params.BorderIntruderDamage);
				if (reported < 3)
				{
					_world.ReportBorderIntrusion(hostId, guestId,
						expelled ? GameText.ActorName(alien) : (GameText.ActorName(alien) + " (возвращение не подтверждено)"));
					reported++;
				}
			}
		}
		catch (Exception)
		{
			// Границы работают только там, где работает API игры.
		}
	}

	/// <summary>Чья земля под ногами юнита: id королевства, которому принадлежит зона тайла (0 — ничья/неизвестно).
	/// Путь как в игре и в PowerBox/ClassicalEconomics: actor.current_tile.zone.city.kingdom.</summary>
	private static int TerritoryOwnerId(Actor actor)
	{
		try
		{
			object tile = GameRefs.ReadMember(actor, "current_tile") ?? GameRefs.Invoke(actor, "getCurrentTile");
			object zone = tile != null ? GameRefs.ReadMember(tile, "zone") : null;
			object city = zone != null ? (GameRefs.ReadMember(zone, "city") ?? GameRefs.ReadMember(tile, "zone_city")) : null;
			object kingdom = city != null ? GameRefs.ReadMember(city, "kingdom") : null;
			if (kingdom is Kingdom k)
			{
				return (int)((NanoObject)k).id;
			}
			return 0;
		}
		catch (Exception)
		{
			return 0;
		}
	}

	/// <summary>Запасной путь: тайл юнита в зонах какого-нибудь города державы-хозяина (радиус зоны города ~30 клеток).</summary>
	private static bool InsideZones(List<City> hostCities, int ax, int ay)
	{
		const int zoneRadius = 30;
		for (int c = 0; c < hostCities.Count; c++)
		{
			object tile = GameRefs.CityTile(hostCities[c]) ?? GameRefs.ReadMember(hostCities[c], "city_center");
			int cx = ToInt(GameRefs.ReadProperty(tile, "x"));
			int cy = ToInt(GameRefs.ReadProperty(tile, "y"));
			int dx = ax - cx;
			int dy = ay - cy;
			if (dx * dx + dy * dy <= zoneRadius * zoneRadius)
			{
				return true;
			}
		}
		return false;
	}

	/// <summary>Координаты тайла актёра (список отката: current_tile → currentTile → tile → data.x/data.y).</summary>
	private static bool TryGetActorTile(Actor actor, out int x, out int y)
	{
		x = 0;
		y = 0;
		try
		{
			object tile = GameRefs.ReadMember(actor, "current_tile")
				?? GameRefs.ReadProperty(actor, "currentTile")
				?? GameRefs.ReadProperty(actor, "tile");
			if (tile != null)
			{
				x = ToInt(GameRefs.ReadProperty(tile, "x"));
				y = ToInt(GameRefs.ReadProperty(tile, "y"));
				return x != 0 || y != 0;
			}
			object data = GameRefs.ReadMember(actor, "data");
			if (data != null)
			{
				x = ToInt(GameRefs.ReadProperty(data, "x"));
				y = ToInt(GameRefs.ReadProperty(data, "y"));
			}
		}
		catch (Exception)
		{
		}
		return false;
	}

	/// <summary>Авиаудары высокоразвитых держав во время войны (по городу с макс. населением).</summary>
	private void RunPowerRaids(MapBox world)
	{
		if (!_world.Params.EnablePowerRaids)
		{
			return;
		}
		object powers = GameRefs.ReadProperty(world, "powers");
		for (int i = 0; i < _kingdomInputs.Count; i++)
		{
			KingdomInput input = _kingdomInputs[i];
			if (input.Relations == null)
			{
				continue;
			}
			for (int j = 0; j < input.Relations.Count; j++)
			{
				RelationInfo rel = input.Relations[j];
				if (rel.Relation != RelationType.War)
				{
					continue;
				}
				if (!_world.TryPowerRaid(input.KingdomId, rel.OtherId))
				{
					continue;
				}
				if (!_liveKingdoms.TryGetValue(rel.OtherId, out Kingdom enemy) || enemy == null)
				{
					continue;
				}
				City target = ChooseRaidTarget(enemy);
				if (target == null)
				{
					continue;
				}
				object tile = GameRefs.CityTile(target);
				int x = ToInt(GameRefs.ReadProperty(tile, "x"));
				int y = ToInt(GameRefs.ReadProperty(tile, "y"));
				string town = GameRefs.ReadProperty(target, "name")?.ToString() ?? "Город";
				if (GameRefs.TryCallGodPowerBomb(powers, x, y, tile))
				{
					_world.ApplyPowerRaidOutcome(input.KingdomId, rel.OtherId, town);
				}
				else
				{
					Patches.LogOncePublic("авиаудар: игровой API бомб недоступен — рейды только документируются");
					// Даже без взрыва фиксируем в летописи (действие спецслужб) — но экономический
					// эффект ядра применяется только при реальном взрыве.
					_world.Bus.Publish(new PolEvent(PolEventType.PowerRaid, "RP_EV_POWER_RAID")
					{
						Year = _world.Year,
						Severity = Severity.Major,
						KingdomId = input.KingdomId
					}.With("kingdom", _world.KingdomNameOf(input.KingdomId))
						.With("target", _world.KingdomNameOf(rel.OtherId))
						.With("town", town));
				}
				break; // один рейд в год на державу
			}
		}
	}

	private static City ChooseRaidTarget(Kingdom enemy)
	{
		List<City> cities = GameRefs.KingdomCities(enemy);
		City best = null;
		int bestPop = -1;
		if (cities != null)
		{
				for (int i = 0; i < cities.Count; i++)
			{
				if (cities[i] == null)
				{
					continue;
				}
				int pop = ToInt(GameRefs.ReadProperty(cities[i], "population"));
				if (pop <= 0)
				{
					pop = GameRefs.CityPopulation(cities[i]);
				}
				if (pop > bestPop || best == null)
				{
					bestPop = pop;
					best = cities[i];
				}
			}
		}
		return best;
	}

	private static int ToInt(object value)
	{
		if (value is int i) return i;
		if (value is long l) return (int)l;
		if (value is float f) return (int)f;
		return 0;
	}

	/// <summary>Клан игры переименовывается в фамилию основателя — физически, один раз;
	/// членам королевской семьи фамилия дописывается к имени физически каждый год (идемпотентно).</summary>
	private void RenameSurnameClans()
	{
		if (!_world.Params.EnableDynastySurnames)
		{
			return;
		}
		for (int i = 0; i < _kingdomInputs.Count; i++)
		{
			KingdomPolitics state = _world.GetKingdom(_kingdomInputs[i].KingdomId);
			if (state == null || string.IsNullOrEmpty(state.DynastySurname))
			{
				continue;
			}
			if (!_liveKingdoms.TryGetValue(state.KingdomId, out Kingdom kingdom) || kingdom == null)
			{
				continue;
			}
			// 0.23.2: клан игры НЕ переименовываем — фамилия и есть имя клана.
			// Если фамилия разошлась с реальным кланом короля (клан сменился в игре без смены
			// правителя, старое сохранение с фамилией-именем) — пересчитываем от клана.
			string liveClan = GameRefs.ActorClanName(GameRefs.ReadRuler(kingdom)) ?? string.Empty;
			if (!string.IsNullOrEmpty(liveClan) && !string.Equals(liveClan, state.DynastySurname, StringComparison.Ordinal))
			{
				state.DynastySurname = liveClan;
				state.RoyalClanName = liveClan;
			}
			// Фамилии на персоналиях королевской семьи: идемпотентно, но новых жизней много.
			GameRefs.ApplySurnameToRoyalFamily(kingdom, state.DynastySurname);
		}
	}

	/// <summary>Этап 20: исполняет в игре решения глубокой дипломатии, оформленные ядром:
	/// вступление членов коалиций и пактов в войну, переводы краденой казны и дань вассалов.</summary>
	/// <summary>
	/// 0.31.12: завершает отложенное освобождение, когда игра подтвердила войну за независимость.
	/// Только тогда появляется запись в хронике «ОСВОБОЖДЕНИЕ НАРОДА» и держава становится в ядре.
	/// </summary>
	private void FinishDeferredLiberation(int newId)
	{
		if (!_deferredLiberations.TryGetValue(newId, out DeferredLiberation deferred))
		{
			return;
		}
		_deferredLiberations.Remove(newId);
		for (int c = deferred.Keep; c < deferred.CityIds.Count; c++)
		{
			if (_world.GetCity(deferred.CityIds[c]) == null)
			{
				_world.AddCity(deferred.CityIds[c], newId);
			}
		}
		deferred.Order.CitiesKeptByVictor = deferred.Keep;
		deferred.Order.PrewarCityIds.Clear();
		deferred.Order.PrewarCityIds.AddRange(deferred.CityIds);
		_world.CompleteRestoration(deferred.Order, newId, deferred.Name);
		_world.Bus.Publish(new PolEvent(PolEventType.NationalLiberationUprising, "RP29_EV_LIBERATION")
		{
			Year = _world.Year,
			KingdomId = deferred.Order.VictorId,
			Severity = Severity.Major
		}.With("kingdom", _world.KingdomNameOf(deferred.Order.VictorId))
			.With("homeland", deferred.Name)
			.With("cities", deferred.CityIds.Count.ToString())
			.With("mode", "RP29_LIBERATION_WAR"));
		LogService.LogInfo("[Real Politic] освобождение «" + deferred.Name + "»: война за независимость подтверждена, держава #" + newId + " объявлена");
	}

	private void PublishConfirmedVassalRebellion(int vassalId, int suzerainId)
	{
		_world.Bus.Publish(new PolEvent(PolEventType.VassalRebellion, "RP_EV_VASSAL_REBELLION")
		{
			Year = _world.Year,
			Severity = Severity.Critical,
			KingdomId = vassalId
		}.With("kingdom", _world.KingdomNameOf(vassalId))
			.With("suzerain", _world.KingdomNameOf(suzerainId)));
	}

	private void ApplyDeepDiplomacyEffects()
	{
		try
		{
			List<KeyValuePair<int, int>> defenseCalls = new List<KeyValuePair<int, int>>(_world.PendingDefenseCalls);
			List<KeyValuePair<int, int>> goldTransfers = new List<KeyValuePair<int, int>>(_world.PendingGoldTransfers);
			List<KeyValuePair<int, int>> independenceWars = _world.TakePendingIndependenceWars();
			_world.ClearPendingCalls();
			for (int i = 0; i < independenceWars.Count; i++)
			{
				int vassalId = independenceWars[i].Key;
				int suzerainId = independenceWars[i].Value;
				if (!_liveKingdoms.TryGetValue(vassalId, out Kingdom vassalKingdom) || vassalKingdom == null
					|| !_liveKingdoms.TryGetValue(suzerainId, out Kingdom suzerainKingdom) || suzerainKingdom == null
					|| IsWild(vassalKingdom) || IsWild(suzerainKingdom))
				{
					continue;
				}
					if (DiplomacyExecutor.AreAtWar(vassalKingdom, suzerainKingdom))
					{
						if (!_world.AreAtWar(vassalId, suzerainId))
						{
							_world.ApplyDeclareWar(vassalId, suzerainId, CasusBelliType.Independence);
						}
						PublishConfirmedVassalRebellion(vassalId, suzerainId);
						FinishDeferredLiberation(vassalId);
					continue;
				}
					if (DiplomacyExecutor.TryStartVerifiedWar(vassalKingdom, suzerainKingdom, rebellion: true))
					{
						_world.ApplyDeclareWar(vassalId, suzerainId, CasusBelliType.Independence);
						PublishConfirmedVassalRebellion(vassalId, suzerainId);
					FinishDeferredLiberation(vassalId);
					LogService.LogInfo("[Real Politic] война за независимость: #" + vassalId + " против бывшего сюзерена #" + suzerainId);
				}
				else
				{
					_world.QueueIndependenceWar(vassalId, suzerainId);
					LogService.LogInfo("[Real Politic] война за независимость #" + vassalId + " → #" + suzerainId + " пока не подтверждена игрой; попытка повторится");
				}
			}
			for (int i = 0; i < defenseCalls.Count; i++)
			{
				int memberId = defenseCalls[i].Key;
				int aggressorId = defenseCalls[i].Value;
				if (!_liveKingdoms.TryGetValue(memberId, out Kingdom memberKingdom) || memberKingdom == null
					|| !_liveKingdoms.TryGetValue(aggressorId, out Kingdom aggressorKingdom) || aggressorKingdom == null
					|| IsWild(memberKingdom) || IsWild(aggressorKingdom))
				{
					continue;
				}
				DiplomaticMove move = new DiplomaticMove
				{
					Action = DiplomaticAction.DeclareWar,
					TargetId = aggressorId
				};
				if (DiplomacyExecutor.Execute(move, memberKingdom, aggressorKingdom))
				{
					_world.ApplyDeclareWar(memberId, aggressorId, CasusBelliType.Defense);
					LogService.LogInfo("[Real Politic] коалиционный призыв: #" + memberId + " вступил в войну против #" + aggressorId);
				}
			}
			for (int i = 0; i < goldTransfers.Count; i++)
			{
				TransferGold(goldTransfers[i].Key, goldTransfers[i].Value, PoliticalWorld.SpyStealAmount);
			}
			List<TributeOrder> tributes = _world.CollectTributes();
			for (int i = 0; i < tributes.Count; i++)
			{
				TributeOrder tribute = tributes[i];
				TransferGold(tribute.VassalId, tribute.SuzerainId, tribute.Amount);
			}
			// Этап 22: проигравшие платят репарации победителям по мирным договорам.
			List<TributeOrder> reparations = _world.CollectReparations();
			for (int i = 0; i < reparations.Count; i++)
			{
				TributeOrder order = reparations[i];
				TransferGold(order.VassalId, order.SuzerainId, order.Amount);
			}
			// Этап 21: завершённая интеграция — города поглощённого участника переходят лидеру.
			List<KeyValuePair<int, int>> cityTransfers = new List<KeyValuePair<int, int>>(_world.PendingCityTransfers);
			_world.ClearPendingCityTransfers();
			for (int i = 0; i < cityTransfers.Count; i++)
			{
				TransferKingdomCities(cityTransfers[i].Key, cityTransfers[i].Value);
			}
			// 0.24: поглощения-соединения — перевести города в державу лидера и снять старую с доски.
			List<PendingMerger> mergers = _world.TakePendingMergers();
			for (int i = 0; i < mergers.Count; i++)
			{
				ApplyMerger(mergers[i]);
			}
			// Этап 22: проигравший уступает ограниченное число городов по мирному договору.
			List<CityDemandOrder> demands = new List<CityDemandOrder>(_world.PendingCityDemands);
			_world.ClearPendingCityDemands();
			for (int i = 0; i < demands.Count; i++)
			{
				TransferCededCities(demands[i].FromId, demands[i].ToId, demands[i].Count);
			}
		}
		catch (Exception ex)
		{
			LogService.LogError("[Real Politic] глубокая дипломатия: " + ex.Message);
		}
	}

	/// <summary>Переводит золото из столицы одной державы в столицу другой через игровой API.</summary>
	private bool TransferGold(int fromId, int toId, float amount)
	{
		if (amount <= 0f || !_liveKingdoms.TryGetValue(fromId, out Kingdom fromKingdom) || fromKingdom == null
			|| !_liveKingdoms.TryGetValue(toId, out Kingdom toKingdom) || toKingdom == null)
		{
			return false;
		}
		try
		{
			List<City> fromCities = GameRefs.KingdomCities(fromKingdom);
			List<City> toCities = GameRefs.KingdomCities(toKingdom);
			if (fromCities == null || fromCities.Count == 0 || toCities == null || toCities.Count == 0)
			{
				return false;
			}
			City fromCapital = GameRefs.KingdomCapital(fromKingdom) ?? fromCities[0];
			City toCapital = GameRefs.KingdomCapital(toKingdom) ?? toCities[0];
			if (fromCapital == null || toCapital == null)
			{
				return false;
			}
			object current = GameRefs.Invoke(fromCapital, "getResourcesAmount", "gold")
				?? GameRefs.Invoke(fromCapital, "get_amount_gold")
				?? GameRefs.ReadProperty(fromCapital, "amount_gold");
			if (!GameRefs.TryResourceValue(current, out double have) || have < amount)
			{
				return false;
			}
			int rounded = (int)Math.Round(amount);
			if (!GameRefs.TryInvoke(fromCapital, "takeResource", out _, "gold", rounded))
			{
				GameRefs.Invoke(fromCapital, "addResourcesToRandomStockpile", "gold", -rounded);
			}
			GameRefs.Invoke(toCapital, "addResourcesToRandomStockpile", "gold", rounded);
			return true;
		}
		catch (Exception)
		{
			return false;
		}
	}

	private bool TryFormUnion(int leaderId, int memberId, UnionType type)
	{
		Union? existing = _world.UnionOf(leaderId);
		if (existing != null)
		{
			// Лидер уже в унии: новую не создаём, а приглашаем соседа в существующую —
			// так федерация и вассальная держава растут дальше пары участников.
			// Тип обязан совпадать: союз в вассалитет не превращается.
			if (existing.Type != type || _world.UnionOf(memberId) != null)
			{
				return false;
			}
			return TryJoinUnion(existing, memberId);
		}
		if (_world.UnionOf(memberId) != null)
		{
			// Одна из сторон уже состоит в унии.
			return false;
		}
		KingdomPolitics memberKingdom = _world.GetKingdom(memberId);
		if (memberKingdom == null || !TryGetInput(leaderId, out KingdomInput leaderInput) || !TryGetInput(memberId, out KingdomInput memberInput))
		{
			return false;
		}
		DiplomatSnapshot memberSnap = BuildSnapshot(memberInput, memberKingdom);
		memberSnap.AdjacentToLeader = SharesBorder(leaderInput, memberId) || SharesBorder(memberInput, leaderId);
		_world.LeaderLanguageId = FirstLanguage(leaderInput);
		string name = UnionName(leaderInput, memberInput);
		Union? union = _world.TryCreateUnion(type, leaderId, memberId, name, memberSnap, out _, FirstReligion(leaderInput), leaderInput.Warriors);
		if (union != null)
		{
			LogService.LogInfo("[Real Politic] уния создана: «" + name + "» (" + type + ") лидер #" + leaderId + ", участник #" + memberId);
		}
		return union != null;
	}

	private bool TryJoinUnion(Union union, int memberId)
	{
		KingdomPolitics memberKingdom = _world.GetKingdom(memberId);
		if (memberKingdom == null || !TryGetInput(memberId, out KingdomInput memberInput))
		{
			return false;
		}
		if (!TryGetInput(union.LeaderId, out KingdomInput leaderInput))
		{
			return false;
		}
		_world.LeaderLanguageId = FirstLanguage(leaderInput);
		DiplomatSnapshot memberSnap = BuildSnapshot(memberInput, memberKingdom);
		memberSnap.AdjacentToLeader = SharesBorder(leaderInput, memberId) || SharesBorder(memberInput, union.LeaderId);
		Union? joined = _world.TryJoinUnion(union, memberId, memberSnap, out _, FirstReligion(memberInput));
		if (joined != null)
		{
			LogService.LogInfo("[Real Politic] в союз «" + UnionDisplayName(joined) + "» вошла держава #" + memberId);
		}
		return joined != null;
	}

	/// <summary>Есть ли у державы зафиксированная общая граница с указанным государством.</summary>
	/// <summary>
	/// 0.44: два обязательных условия для любой НАСТУПАТЕЛЬНОЙ войны мода:
	///  (1) у держав есть общая граница — до далёкой страны армию не довести;
	///  (2) правитель посчитал силы и считает, что перевес есть.
	/// Оборонительные поводы (Defense, Independence) проходят без этих проверок:
	/// защищаться приходится и без границы, и без перевеса.
	/// </summary>
	private bool WarSanityAllows(int attackerId, int targetId, CasusBelliType cb, out string why)
	{
		why = null;
		if (cb == CasusBelliType.Defense || cb == CasusBelliType.Independence)
		{
			return true;
		}
		if (!TryGetInput(attackerId, out KingdomInput attackerInput))
		{
			return true; // нет данных о мире — не мешаем игре
		}
		if (!SharesBorder(attackerInput, targetId))
		{
			why = "нет общей границы";
			return false;
		}
		if (!TryGetInput(targetId, out KingdomInput targetInput))
		{
			return true;
		}
		WarEstimate estimate = WarChances.Estimate(
			attackerInput.Warriors,
			attackerInput.Cities == null ? 0 : attackerInput.Cities.Count,
			_world.GetKingdom(attackerId)?.Economy?.Prosperity ?? 50f,
			_world.GetKingdom(attackerId)?.WarFatigue ?? 0f,
			targetInput.Warriors,
			targetInput.Cities == null ? 0 : targetInput.Cities.Count,
			_world.GetKingdom(targetId)?.Economy?.Prosperity ?? 50f,
			_world.GetKingdom(targetId)?.WarFatigue ?? 0f,
			AllianceWarriorsOf(targetId),
			cb);
		if (!estimate.ShouldAttack)
		{
			why = "правитель не видит перевеса (" + (estimate.Reason ?? "нет причин") + ")";
			NoteWarRefusal(attackerId, targetId, estimate);
			return false;
		}
		return true;
	}

	private bool SharesBorder(KingdomInput input, int otherId)
	{
		if (input.Relations == null)
		{
			return false;
		}
		for (int r = 0; r < input.Relations.Count; r++)
		{
			if (input.Relations[r].OtherId == otherId && input.Relations[r].Adjacent)
			{
				return true;
			}
		}
		return false;
	}

	/// <summary>Имя унии для лога и интерфейса: ключ локализации переводится, готовое имя берётся как есть.</summary>
	private static string UnionDisplayName(Union union)
	{
		if (string.IsNullOrEmpty(union.NameKey))
		{
			return union.Name;
		}
		return GameText.Localized(union.NameKey, union.Name);
	}

	private static DiplomatSnapshot BuildSnapshot(KingdomInput input, KingdomPolitics kingdom)
	{
		return new DiplomatSnapshot
		{
			KingdomId = input.KingdomId,
			Ideology = kingdom.Ideology,
			Stance = kingdom.Stance,
			Legitimacy = kingdom.Legitimacy,
			Population = input.Population,
			Warriors = input.Warriors,
			Starving = input.Starving,
			AtWar = input.AtWar,
			ReligionId = FirstReligion(input),
			Levy = kingdom.Constitution?.Levy ?? LevyLaw.Conscription,
			BindingConstitution = kingdom.Constitution != null && kingdom.Constitution.Supremacy == SupremacyRule.Binding,
			LanguageId = FirstLanguage(input)
		};
	}

	/// <summary>Язык державы: первоклассная мета-сущность игры (по городам), не культура.</summary>
	private static int FirstLanguage(KingdomInput input)
	{
		if (input.Cities == null)
		{
			return 0;
		}
		for (int i = 0; i < input.Cities.Count; i++)
		{
			if (input.Cities[i].LanguageId != 0)
			{
				return input.Cities[i].LanguageId;
			}
		}
		return 0;
	}

	private bool TryGetInput(int kingdomId, out KingdomInput input)
	{
		for (int i = 0; i < _kingdomInputs.Count; i++)
		{
			if (_kingdomInputs[i].KingdomId == kingdomId)
			{
				input = _kingdomInputs[i];
				return true;
			}
		}
		input = default(KingdomInput);
		return false;
	}

	private static string UnionName(KingdomInput leader, KingdomInput member)
	{
		string generated = TryGenerateAllianceName();
		if (!string.IsNullOrEmpty(generated))
		{
			return generated;
		}
		string text = string.IsNullOrEmpty(leader.Name) ? leader.KingdomId.ToString() : leader.Name;
		string text2 = string.IsNullOrEmpty(member.Name) ? member.KingdomId.ToString() : member.Name;
		return text + "–" + text2;
	}

	private static string TryGenerateAllianceName()
	{
		try
		{
			Type type = Patches.FindGameType("NameGenerator");
			if (type == null)
			{
				return null;
			}
			MethodInfo method = type.GetMethod("getName", BindingFlags.Static | BindingFlags.Public);
			if (method == null)
			{
				return null;
			}
			ParameterInfo[] parameters = method.GetParameters();
			object[] array = new object[parameters.Length];
			array[0] = "alliance_name";
			for (int i = 1; i < parameters.Length; i++)
			{
				if (parameters[i].ParameterType.IsValueType)
				{
					if (Nullable.GetUnderlyingType(parameters[i].ParameterType) != null)
					{
						array[i] = null;
					}
					else
					{
						array[i] = Activator.CreateInstance(parameters[i].ParameterType);
					}
				}
				else
				{
					array[i] = null;
				}
			}
			return method.Invoke(null, array) as string;
		}
		catch (Exception)
		{
			return null;
		}
	}

	private static bool IsWild(Kingdom kingdom)
	{
		return GameRefs.ReadProperty(kingdom, "wild") is bool flag && flag;
	}

	private static int FirstReligion(KingdomInput input)
	{
		if (input.Cities == null)
		{
			return 0;
		}
		for (int i = 0; i < input.Cities.Count; i++)
		{
			if (input.Cities[i].ReligionId != 0)
			{
				return input.Cities[i].ReligionId;
			}
		}
		return 0;
	}

	private void SyncKingdoms(MapBox map)
	{
			List<Kingdom> list = GameRefs.KingdomList(map);
		if (list == null)
		{
			return;
		}
		for (int i = 0; i < list.Count; i++)
		{
			Kingdom val = list[i];
			if (val != null)
			{
				int num = (int)((NanoObject)val).id;
                if (_world.ChronicleCauses.HasEvidence(num))
                    _world.ChronicleCauses.ObservePopulation(num, GameDeaths.CountLivingResidents(val));
				if (_world.GetKingdom(num) == null)
				{
					int foundedYear = _world.Governance.Enabled ? RealPoliticMod.CurrentYear() : _world.Year;
					if (GameRefs.Invoke(val, "getFoundedYear") is int nativeFounded && nativeFounded > 0 && nativeFounded <= foundedYear)
					{
						foundedYear = nativeFounded;
					}
					IdeologyType startIdeology = StartingIdeology(val);
					startIdeology = EraRules.NearestAllowed(startIdeology, EraRules.OfYear(foundedYear, _world.Params));
					if (foundedYear >= (_world.Params?.TribalDeclineYear ?? 200)
						&& TribalCivilization.NeedsCivilization(startIdeology))
					{
						startIdeology = IdeologyType.Monarchy;
					}
					KingdomPolitics added = _world.AddKingdom(num, startIdeology, foundedYear);
					City cap = GameRefs.KingdomCapital(val);
					string rawName = ((NanoObject)val).name ?? string.Empty;
					string capName = cap != null ? ((NanoObject)cap).name : rawName;
					string capBiome = cap != null ? GameRefs.CityBiomeId(cap) : string.Empty;
					string capCulture = cap != null ? GameRefs.CityCultureName(cap) : string.Empty;
					string assignedName = _world.AssignIdeologyStateName(added, rawName, capName, capBiome, capCulture);
					if (!string.IsNullOrWhiteSpace(assignedName))
					{
						GameRefs.TryRenameKingdom(val, assignedName);
					}
				}
                GovernanceRuntime.ObserveBirth(_world,val,RealPoliticMod.CurrentYear());
			}
		}
	}

	private void SyncCities(MapBox map)
	{
			List<City> list = GameRefs.CityList(map);
		if (list == null)
		{
			return;
		}
		for (int i = 0; i < list.Count; i++)
		{
			City val = list[i];
			if (val == null)
			{
				continue;
			}
			int num = (int)((NanoObject)val).id;
			int num2 = (int)((val.kingdom == null) ? (-1) : ((NanoObject)val.kingdom).id);
			if (num2 >= 0)
			{
				CityPolitics city = _world.GetCity(num);
				if (city == null)
				{
					_world.AddCity(num, num2);
				}
				else
				{
					city.KingdomId = num2;
				}
			}
		}
	}

	private void SyncRulers(MapBox map)
	{
		GameRefs.RefreshRulers(map, _world);
	}

	private KingdomInput BuildKingdomInput(MapBox map, Kingdom kingdom)
	{
		int num = (int)((NanoObject)kingdom).id;
		KingdomPolitics kingdom2 = _world.GetKingdom(num);
		// 0.43: два прохода по всем юнитам державы (население и воины) заменены одним.
		int population;
		int warriors;
		bool havePopulation = GameRefs.TryKingdomPopulation(kingdom, out population);
		bool haveWarriors = GameRefs.TryKingdomWarriors(kingdom, out warriors);
		if (!havePopulation || !haveWarriors)
		{
			int byUnitsPopulation;
			int byUnitsWarriors;
			GameRefs.KingdomCountsByUnits(kingdom, out byUnitsPopulation, out byUnitsWarriors);
			if (!havePopulation)
			{
				population = byUnitsPopulation;
			}
			if (!haveWarriors)
			{
				warriors = byUnitsWarriors;
			}
		}
		_cityScratch.Clear();
		AppendCities(kingdom, _cityScratch);
		_relationScratch.Clear();
		GameRefs.CollectRelations(map, kingdom, _relationScratch);
		string kRace = GameRefs.KingdomSpeciesId(kingdom);
		// Этап 26+: электорат конституционных голосований и реальная перепись взглядов/недовольства живых жителей.
		if (kingdom2 != null)
		{
			// 0.43: отдельный замер — сколько из «сборки держав» съедает перепись жителей.
			PerfProbe.BeginCensus();
			try
			{
				RefreshVoterSample(kingdom, kingdom2);
			}
			finally
			{
				PerfProbe.EndCensus();
			}
		}
		return new KingdomInput
		{
			KingdomId = num,
			Name = ((NanoObject)kingdom).name,
			Population = population,
			Warriors = warriors,
			AtWar = GameRefs.IsAtWar(kingdom),
			Starving = IsStarving(kingdom),
			Gold = GameRefs.TryKingdomResource(kingdom, "gold", out float treasury) ? treasury : -1f,
			Ruler = kingdom2?.Ruler,
			Relations = new List<RelationInfo>(_relationScratch),
			Cities = new List<CityInput>(_cityScratch),
			RaceId = kRace,
			RaceName = kRace
		};
	}

	/// <summary>Собирает электорат и реальную перепись (Census, Support, AvgDissent) из живых жителей:
	/// столичные голосуют двойным весом, ограничение выборки — 200 бюллетеней.</summary>
	private void RefreshVoterSample(Kingdom kingdom, KingdomPolitics state)
	{
		List<City> cities = GameRefs.KingdomCities(kingdom);
		if (cities == null || cities.Count == 0)
		{
			state.Census.Clear();
			KingdomSimulation.UpdateCensusDerived(state, 0f, 0);
			return;
		}
		// 0.42: если мод не успевает (на скорости x20 годы бегут очень часто),
		// перепись идёт через год — прошлый снимок остаётся в силе.
		if (PerfProbe.UnderPressure && (_world.Year % 2) != 0)
		{
			return;
		}
		int limit = Math.Max(40, RealPoliticMod.CensusSampleLimit);
		List<(long ActorId, IdeologyType Ideology)> sample = new List<(long, IdeologyType)>();
		Dictionary<IdeologyType, int> census = new Dictionary<IdeologyType, int>();
		Dictionary<IdeologyType, int> cityCounts = new Dictionary<IdeologyType, int>();
		float totalDissent = 0f;
		int totalCount = 0;

		// 0.41: перепись больше не обходит ВСЕХ жителей каждый год — это и было главной
		// причиной замедления на больших мирах (в партии 29.10.2026 год стоил ~5 секунд
		// на старте и ~19 секунд к 90-му году). Опрашиваем не больше `limit` человек
		// на державу, а города обходим по кругу: за несколько лет выборка обходит весь мир.
		int startCity = cities.Count > 0 ? Math.Abs(_world.Year) % cities.Count : 0;
		int surveyed = 0;
		for (int ci = 0; ci < cities.Count; ci++)
		{
			if (surveyed >= limit)
			{
				break;
			}
			int c = (startCity + ci) % cities.Count;
			City cityObj = cities[c];
			if (cityObj == null)
			{
				continue;
			}
			System.Collections.IEnumerable units;
			try
			{
				units = GameRefs.ReadMember(cityObj, "units") as System.Collections.IEnumerable;
			}
			catch
			{
				continue; // огрехи рефлексии не ломают симуляцию
			}
			if (units == null)
			{
				continue;
			}
			cityCounts.Clear();
			float cityDissent = 0f;
			int cityPop = 0;
				foreach (object unitObj in units)
				{
					if (surveyed >= limit)
					{
						break;
					}
					Actor actor = unitObj as Actor;
					if (actor == null || GameRefs.IsBoatUnit(actor))
					{
						continue;
					}
					surveyed++;
					try
					{
					int actorId = GameText.ActorId(actor);
					ActorIdeologies.Faith faith = ActorIdeologies.Get(actor) ?? ActorIdeologies.Peek(actorId);
				IdeologyType citizenIdeology = (faith != null) ? faith.Ideology : state.Ideology;
				float citizenDissent = (faith != null) ? faith.Dissent : 0f;

				census.TryGetValue(citizenIdeology, out int cur);
				census[citizenIdeology] = cur + 1;
				totalDissent += citizenDissent;
				totalCount++;

				cityCounts.TryGetValue(citizenIdeology, out int cCur);
				cityCounts[citizenIdeology] = cCur + 1;
				cityDissent += citizenDissent;
				cityPop++;

					if (sample.Count < 200)
					{
						sample.Add((actorId, citizenIdeology));
					}
					}
					catch (Exception ex)
					{
						LogService.LogError("[Real Politic] пропущен житель в выборке убеждений: " + ex.Message);
					}
				}

			if (cityPop > 0)
			{
				int cityId = (int)((NanoObject)cityObj).id;
				CityPolitics cp = _world.GetCity(cityId);
				if (cp != null)
				{
					IdeologyType dom = state.Ideology;
					int domCount = -1;
					foreach (KeyValuePair<IdeologyType, int> kv in cityCounts)
					{
						if (kv.Value > domCount)
						{
							domCount = kv.Value;
							dom = kv.Key;
						}
					}
					cp.Dominant = dom;
					cp.Share = (float)domCount / cityPop;
					cp.AvgDissent = KingdomSimulation.Clamp(cityDissent / cityPop, 0f, 1f);
				}
			}
		}

		state.LastVoterSample = sample;
		PerfProbe.CountCitizens(totalCount);
		if (totalCount > 0)
		{
			float prevSupport = state.Support;
			float prevDissent = state.AvgDissent;
			bool hadPriorCensus = state.Population > 0;
			state.Census.Clear();
			foreach (KeyValuePair<IdeologyType, int> kv in census)
			{
				state.Census[kv.Key] = kv.Value;
			}
			KingdomSimulation.UpdateCensusDerived(state, totalDissent, totalCount);
			if (hadPriorCensus)
			{
				state.Support = KingdomSimulation.Clamp(state.Support * 0.65f + prevSupport * 0.35f, 0f, 100f);
				state.AvgDissent = KingdomSimulation.Clamp(state.AvgDissent * 0.65f + prevDissent * 0.35f, 0f, 1f);
			}
		}
	}

	private IdeologyType StartingIdeology(Kingdom kingdom)
	{
		bool pastTribalEra = _world.Year >= (_world.Params?.TribalDeclineYear ?? 200);
		if (GameRefs.IsClan(kingdom))
		{
			return pastTribalEra ? IdeologyType.Monarchy : IdeologyType.Tribalism;
		}
		// При отделении восставшего города (Rebellion): наследуем идеологию правителя или восставшей столицы
		Actor king = GameRefs.ReadRuler(kingdom);
		if (king != null)
		{
			ActorIdeologies.Faith faith = ActorIdeologies.Peek(GameText.ActorId(king));
			if (faith != null && faith.Ideology != IdeologyType.Monarchy)
			{
				if (pastTribalEra && TribalCivilization.NeedsCivilization(faith.Ideology))
				{
					return IdeologyType.Monarchy;
				}
				// 0.31.12: в ранней эпохе взгляд короля не делает государство военным или монархией при старте.
				if (!pastTribalEra && !Ideologies.IsStartingIdeology(faith.Ideology))
				{
					return Ideologies.RollStarting(_world.Rng);
				}
				return faith.Ideology;
			}
		}
		City capitalCity = GameRefs.KingdomCapital(kingdom);
		if (capitalCity != null)
		{
			int cityId = (int)((NanoObject)capitalCity).id;
			CityPolitics cp = _world.GetCity(cityId);
			if (cp != null && cp.Dominant != IdeologyType.Monarchy)
			{
				if (pastTribalEra && TribalCivilization.NeedsCivilization(cp.Dominant))
				{
					return IdeologyType.Monarchy;
				}
				// 0.31.12: доминирующий строй столицы при старте — только племя или каганат.
				if (!pastTribalEra && !Ideologies.IsStartingIdeology(cp.Dominant))
				{
					return Ideologies.RollStarting(_world.Rng);
				}
				return cp.Dominant;
			}
		}
		// Ни король, ни столица подсказку не дали — бросаем стартовый строй:
		// 0.31.12 — только племя или каганат.
		IdeologyType rolled = Ideologies.RollStarting(_world.Rng);
		if (pastTribalEra && TribalCivilization.NeedsCivilization(rolled))
		{
			return IdeologyType.Monarchy;
		}
		return rolled;
	}

	/// <summary>0.46: входные данные держав за этот год — словарём, для расселения и переселения.
	/// Собирается из уже готового списка (лишний раз мир не обходим).</summary>
	private Dictionary<int, KingdomInput> InputMap()
	{
		Dictionary<int, KingdomInput> map = new Dictionary<int, KingdomInput>(_kingdomInputs.Count);
		for (int i = 0; i < _kingdomInputs.Count; i++)
		{
			map[_kingdomInputs[i].KingdomId] = _kingdomInputs[i];
		}
		return map;
	}

	private static int CountPopulation(Kingdom kingdom)
	{
		return GameRefs.KingdomPopulation(kingdom);
	}

	private static int CountWarriors(Kingdom kingdom)
	{
		return GameRefs.KingdomWarriors(kingdom);
	}

	private void AppendCities(Kingdom kingdom, List<CityInput> target)
	{
		List<City> list = GameRefs.KingdomCities(kingdom);
		if (list == null)
		{
			return;
		}
		string kRace = GameRefs.KingdomSpeciesId(kingdom);
		// Столица — первой: ядро и гражданские войны полагаются на Cities[0] == столица,
		// а порядок Kingdom.cities этого не гарантирует (зависит от истории завоеваний).
		City cap = GameRefs.KingdomCapital(kingdom);
		List<City> ordered = new List<City>(list.Count);
		if (cap != null && list.Contains(cap))
		{
			ordered.Add(cap);
		}
		for (int i = 0; i < list.Count; i++)
		{
			if (!ReferenceEquals(list[i], cap))
			{
				ordered.Add(list[i]);
			}
		}
		for (int i = 0; i < ordered.Count; i++)
		{
			City val = ordered[i];
			if (val != null)
			{
				string cRace = GameRefs.CitySpeciesId(val);
				target.Add(new CityInput
				{
					CityId = (int)((NanoObject)val).id,
					KingdomId = (int)((NanoObject)kingdom).id,
					Name = ((NanoObject)val).name ?? string.Empty,
					BiomeId = GameRefs.CityBiomeId(val),
					Population = GameRefs.CityPopulation(val),
					FoodOk = !GameRefs.CityStarving(val),
					ReligionId = GameRefs.CityReligionId(val),
					LanguageId = GameRefs.CityLanguageId(val),
					CultureId = GameRefs.CityCultureId(val),
					ReligionName = GameRefs.CityReligionName(val),
					LanguageName = GameRefs.CityLanguageName(val),
					CultureName = GameRefs.CityCultureName(val),
					RaceId = cRace,
					RaceName = cRace,
					IsForeign = !string.IsNullOrEmpty(kRace) && !string.IsNullOrEmpty(cRace) && !string.Equals(kRace, cRace, StringComparison.OrdinalIgnoreCase),
					Loyalty = CityLoyalty(val)
				});
			}
		}
	}

	private static bool IsStarving(Kingdom kingdom)
	{
		return GameRefs.KingdomStarving(kingdom);
	}

	/// <summary>Лояльность города 0..100 (0 — игра не отдала значение).</summary>
	private static int CityLoyalty(City city)
	{
		try
		{
			return ((GameRefs.Invoke(city, "getLoyalty", false) ?? GameRefs.Invoke(city, "getLoyalty")) is int loyalty) ? loyalty : 0;
		}
		catch (Exception)
		{
			return 0;
		}
	}

	// ------------------------------------------------------------ гражданские войны

	/// <summary>
	/// По правилу задумки метрополия обязана воевать с мятежниками сразу. Если игра
	/// ещё не подтвердила войну, объявляем её снова каждый год. Ядро до подтверждения
	/// войны не считает отделение миром (CivilWarState.WarEstablished).
	/// </summary>
		private void EnsureCivilWarsAtWar(MapBox map)
		{
		List<KingdomPolitics> states = new List<KingdomPolitics>(_world.Kingdoms);
		for (int i = 0; i < states.Count; i++)
		{
			CivilWarState cw = states[i]?.CivilWar;
			if (cw == null || cw.RebelKingdomId == 0 || cw.WarEstablished)
			{
				continue;
			}
			if (!_liveKingdoms.TryGetValue(states[i].KingdomId, out Kingdom parent) || parent == null
				|| !_liveKingdoms.TryGetValue(cw.RebelKingdomId, out Kingdom rebels) || rebels == null)
			{
				continue;
			}
			if (DiplomacyExecutor.AreAtWar(parent, rebels))
			{
				cw.WarEstablished = true;
				continue;
			}
			if (DiplomacyExecutor.TryStartVerifiedWar(rebels, parent, rebellion: true))
			{
				cw.WarEstablished = true;
				LogService.LogInfo("[Real Politic] гражданская война: метрополия #" + states[i].KingdomId + " и повстанцы #" + cw.RebelKingdomId + " — война объявлена");
			}
			else
			{
				// Повтор идёт каждые пару секунд: писать об этом каждый раз нельзя — журнал
				// превратится в тысячи одинаковых строк. Пишем не чаще раза в 30 секунд.
				string key = "cw:" + states[i].KingdomId + ":" + cw.RebelKingdomId;
				float now = Time.unscaledTime;
				if (!_lastWarRetryLog.TryGetValue(key, out float was) || now - was >= 30f)
				{
					_lastWarRetryLog[key] = now;
					LogService.LogError("[Real Politic] гражданская война #" + states[i].KingdomId
						+ ": игра ещё не подтвердила войну с повстанцами #" + cw.RebelKingdomId
						+ "; повторяю каждые 2 секунды (подробности — в строках «война/API»)");
				}
			}
		}
	}

	/// <summary>
	/// Исполняет в игре отделения, которые оформило ядро: создаёт государство повстанцев
	/// и переносит ядро на настоящий id королевства WorldBox.
	/// Если игровой API не сработал — ядро само уберёт новое государство на следующем
	/// годовом снимке (города вернутся к родителю), так что расхождения не накапливаются.
	/// </summary>
	private void ExecutePendingSecessions(MapBox map)
	{
		List<PendingSecession> pending = _world.TakePendingSecessions();
		for (int i = 0; i < pending.Count; i++)
		{
			PendingSecession secession = pending[i];
			int gameKingdomId;
			bool created = CivilWarExecutor.ExecuteSecession(secession, map, out gameKingdomId);
			if (created && gameKingdomId != secession.ProvisionalKingdomId)
			{
				_world.RemapKingdomId(secession.ProvisionalKingdomId, gameKingdomId);
			}
			if (created)
			{
				Kingdom createdKingdom = CivilWarExecutor.FindKingdom(map, gameKingdomId);
				if (createdKingdom != null)
				{
					_liveKingdoms[gameKingdomId] = createdKingdom;
					string kName = ((NanoObject)(object)createdKingdom).name;
					if (!string.IsNullOrWhiteSpace(kName))
					{
						_world.RememberKingdomName(gameKingdomId, kName);
					}
				}
			}
			if (!created)
			{
				// Игровой API отказал: убираем «призрачное» государство из ядра сразу.
				_world.AbortSecession(secession.ProvisionalKingdomId, secession.ParentKingdomId, secession.CityIds);
			}
			LogService.LogInfo("[Real Politic] отделение #" + secession.ParentKingdomId + ": "
				+ (created ? ("новое государство " + gameKingdomId + ", городов: " + secession.CityIds.Count) : "игровой API отказал, ядро откатит на следующем снимке"));
		}
	}

	/// <summary>Итоги гражданских войн: переворот (города — повстанцам) и мир (отделение).</summary>
	private void ExecutePendingWorldOps(MapBox map)
	{
		List<PendingWorldOp> ops = _world.TakePendingWorldOps();
		for (int i = 0; i < ops.Count; i++)
		{
			bool ok = CivilWarExecutor.ExecuteWorldOp(ops[i], map);
			LogService.LogInfo("[Real Politic] операция " + ops[i].Type + " " + ops[i].FromKingdomId + " → " + ops[i].ToKingdomId + ": " + (ok ? "исполнена" : "игровой API отказал"));
		}
	}

	/// <summary>Этап 25: родословная мира. Раз в год замечаем каждого жителя:
	/// кто родился, кто умер — реестр помнит всех, и родственники короля не теряются даже после смерти.</summary>
	private void FamilyRegistryTick(MapBox map, int year)
	{
		PerfProbe.Begin(PerfProbe.Step.Family);
		try
		{
			if (map?.cities == null)
			{
				return;
			}
			System.Collections.IEnumerable cities = GameRefs.CityList(map) ?? (map.cities as System.Collections.IEnumerable);
			if (cities == null)
			{
				return;
			}
			foreach (object c in cities)
			{
				City city = c as City;
				if (city == null)
				{
					continue;
				}
				System.Collections.IEnumerable units = GameRefs.ReadMember(city, "units") as System.Collections.IEnumerable;
				if (units == null)
				{
					continue;
				}
				int kingdomId = city.kingdom != null ? (int)((NanoObject)city.kingdom).id : 0;
				foreach (object u in units)
				{
					Actor a = u as Actor;
					if (a?.data == null)
					{
						continue;
					}
					long id = ((BaseSystemData)a.data).id;
					// Existing citizens only need a cheap presence/ownership refresh. Name,
					// ancestry and clan are effectively static and must not trigger several
					// reflection calls for every resident at every year boundary.
					bool known = _world.Family.TryGet(id, out FamilyRecord knownRecord);
					if (known)
					{
						if (knownRecord.DeathYear != null)
						{
							continue;
						}
						// Refresh mutable names/clans occasionally, not for every resident every year.
						if (year % 10 != 0)
						{
							_world.Family.ObservePresence(id, year, kingdomId);
							continue;
						}
					}
					if (!known && GameRefs.IsBoatUnit(a))
					{
						continue;
					}
					long[] parents = GameRefs.ActorParentIds(a);
					object clan = GameRefs.ReadProperty(a, "clan") ?? GameRefs.Invoke(a, "getClan");
					object clanIdObj = GameRefs.ReadProperty(clan, "id") ?? GameRefs.ReadMember(GameRefs.ReadMember(clan, "data"), "id");
					long clanId = ToLong(clanIdObj);
					object clanNameObj = GameRefs.ReadProperty(clan, "name") ?? GameRefs.ReadMember(GameRefs.ReadMember(clan, "data"), "name");
					_world.Family.Observe(id, GameText.ActorName(a), GameText.ActorIsFemale(a), GameText.ActorAge(a), year,
						parents[0], parents[1], clanNameObj?.ToString() ?? string.Empty, clanId, kingdomId);
				}
			}
			int markedDead = _world.Family.MarkMissingDead(year);
			if (markedDead > 0)
			{
				LogService.LogInfo("[Real Politic] родословная: за год ушли из мира " + markedDead);
			}
		}
		catch (Exception ex)
		{
			LogService.LogError("[Real Politic] ошибка обхода родословной: " + ex.Message);
		}
		finally
		{
			PerfProbe.End();
		}
	}

	private static long ToLong(object value)
	{
		if (value == null)
		{
			return 0;
		}
		if (value is long l) return l;
		if (value is int i) return i;
		try
		{
			return Convert.ToInt64(value);
		}
		catch (Exception)
		{
			return 0;
		}
	}

	/// <summary>Годовые эффекты идущих гражданских войн (лояльность, настроение, снабжение).</summary>
	private void ApplyCivilWarEffects(int year)
	{
		if (!CivilWarExecutor.CivilWarsEnabled)
		{
			return;
		}
		foreach (KeyValuePair<int, Kingdom> live in _liveKingdoms)
		{
			KingdomPolitics state = _world.GetKingdom(live.Key);
			if (state?.CivilWar == null)
			{
				continue;
			}
			CivilWarExecutor.ApplyWarEffects(live.Value, state, year);
		}
	}

	/// <summary>Сводка о гражданской войне для окна государства.</summary>
	public bool TryGetEconomySummary(int kingdomId, out string summary)
	{
		summary = string.Empty;
		KingdomPolitics state = _world.GetKingdom(kingdomId);
		if (state == null)
		{
			return false;
		}
		return EconomyExecutor.TryGetSummary(state, _world, _world.Params, RealPoliticMod.CurrentYear(), out summary);
	}

	public bool TryGetCivilWarSummary(int kingdomId, out string summary)
	{
		summary = string.Empty;
		KingdomPolitics state = _world.GetKingdom(kingdomId);
		CivilWarState cw = state?.CivilWar;
		if (cw == null)
		{
			return false;
		}
		string cause = GameText.Localized(CivilWarCauses.LocKey(cw.Cause), cw.Cause.ToString());
		string rebelName = _world.KingdomNameOf(cw.RebelKingdomId);
		summary = cause + " • " + GameText.Localized("RP_CW_SIDE_REBELS", "повстанцы") + (string.IsNullOrEmpty(rebelName) ? string.Empty : ": " + rebelName)
			+ " • " + cw.YearsActive(RealPoliticMod.CurrentYear()) + GameText.Localized("RP_YEARS_SHORT", "л.")
			+ " • " + cw.RebelCityIds.Count + " " + GameText.Localized("RP_CW_CITIES_SHORT", "гор.");
		return true;
	}
}
