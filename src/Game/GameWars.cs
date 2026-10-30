using System;
using System.Collections.Generic;
using RealPolitic.Core;

namespace RealPolitic;

/// <summary>
/// Войны в летописи. Harmony-патчи на WarManager падали с «IL Compile Error»
/// даже с параметрами object, поэтому войны отслеживаются ОПРОСОМ состояния:
/// раз в пару секунд сверяем список World.world.wars с тем, что видели раньше.
/// Появилась война — событие начала, исчезла или hasEnded() — событие конца.
/// Публикует события WarStarted/WarEnded в шину (летопись пишет их со всеми деталями,
/// игровые уведомления — только если включена настройка NotifyWars).
/// </summary>
public static class GameWars
{
	/// <summary>Год начала каждой активной войны (id войны → год) — для длительности.</summary>
	private static readonly Dictionary<long, int> _warStartYear = new Dictionary<long, int>();

	/// <summary>Сохранённые id и имена главных атакующих и защищающихся на момент начала войны.</summary>
	private static readonly Dictionary<long, int> _warAttackerIds = new Dictionary<long, int>();
	private static readonly Dictionary<long, int> _warDefenderIds = new Dictionary<long, int>();
	private static readonly Dictionary<long, string> _warAttackerNames = new Dictionary<long, string>();
	private static readonly Dictionary<long, string> _warDefenderNames = new Dictionary<long, string>();
	private static readonly Dictionary<long, string> _warNames = new Dictionary<long, string>();

	/// <summary>Все государства-участники каждой стороны войны (id войны → (id державы → имя державы)).</summary>
	private static readonly Dictionary<long, Dictionary<int, string>> _warAttackers = new Dictionary<long, Dictionary<int, string>>();
	private static readonly Dictionary<long, Dictionary<int, string>> _warDefenders = new Dictionary<long, Dictionary<int, string>>();

	/// <summary>Войны, для которых уже записано завершение (защита от повторных endWar).</summary>
	private static readonly HashSet<long> _endedHandled = new HashSet<long>();

	/// <summary>Войны, начало которых уже записано (id → true).</summary>
	private static readonly HashSet<long> _startedHandled = new HashSet<long>();

	/// <summary>Живые войны прошлого опроса: id → участники, чтобы описать завершение.</summary>
	private static readonly Dictionary<long, War> _knownWars = new Dictionary<long, War>();

	private static float _nextPoll;

	/// <summary>Сброс при смене мира: id войн нового мира могут совпасть со старыми.</summary>
	public static void Reset()
	{
		_warStartYear.Clear();
		_warAttackerIds.Clear();
		_warDefenderIds.Clear();
		_warAttackerNames.Clear();
		_warDefenderNames.Clear();
		_warAttackers.Clear();
		_warDefenders.Clear();
		_endedHandled.Clear();
		_startedHandled.Clear();
		_knownWars.Clear();
		_nextPoll = 0f;
	}

	/// <summary>
	/// Опрос состояния войн. Вызывается из Update — заменяет патчи WarManager.
	/// </summary>
	public static void Poll()
	{
		PollInternal(false);
	}

	/// <summary>Немедленный опрос после объявления войны, без ожидания двухсекундного таймера.</summary>
	public static void PollImmediate()
	{
		PollInternal(true);
	}

	private static void PollInternal(bool immediate)
	{
		if (!immediate && UnityEngine.Time.unscaledTime < _nextPoll)
		{
			return;
		}
		_nextPoll = UnityEngine.Time.unscaledTime + 2f;
		try
		{
			MapBox map = World.world;
			if (map == null || RealPoliticMod.World == null)
			{
				return;
			}
			// Через рефлексию: в стабах сборки этих членов нет, а в игре они есть.
			object manager = GameRefs.ReadMember(map, "wars");
			object rawList = (manager == null) ? null : GameRefs.ReadMember(manager, "list");
			System.Collections.IList list = rawList as System.Collections.IList;
			if (list == null)
			{
				return;
			}
			HashSet<long> alive = new HashSet<long>();
			for (int i = 0; i < list.Count; i++)
			{
				War war = list[i] as War;
				if (war == null)
				{
					continue;
				}
				long id = (long)((NanoObject)war).id;
				bool ended = false;
				if (GameRefs.Invoke(war, "hasEnded") is bool e)
				{
					ended = e;
				}
				if (!ended)
				{
					alive.Add(id);
					_knownWars[id] = war;
					if (_startedHandled.Add(id))
					{
						// Новая война: сообщаем о начале.
						Kingdom attacker = GameRefs.Invoke(war, "getMainAttacker") as Kingdom;
						Kingdom defender = GameRefs.Invoke(war, "getMainDefender") as Kingdom;
						WarTypeAsset asset = GameRefs.Invoke(war, "getAsset") as WarTypeAsset;
						OnWarStarted(attacker, defender, asset, war);
					}
					else
					{
						Kingdom curAtt = GameRefs.Invoke(war, "getMainAttacker") as Kingdom;
						Kingdom curDef = GameRefs.Invoke(war, "getMainDefender") as Kingdom;
						RecordWarParticipants(id, curAtt, curDef, war, RealPoliticMod.World);
					}
				}
				else if (_startedHandled.Contains(id))
				{
					// Война завершилась «на месте» — победитель лежит в данных войны.
					OnWarEnded(war, ReadWinner(war));
				}
			}
			// Войны, пропавшие из списка, тоже считаем завершёнными.
			if (_startedHandled.Count > 0)
			{
				List<long> gone = new List<long>();
				foreach (long id in _startedHandled)
				{
					if (!alive.Contains(id) && !_endedHandled.Contains(id))
					{
						gone.Add(id);
					}
				}
				for (int i = 0; i < gone.Count; i++)
				{
					_knownWars.TryGetValue(gone[i], out War war);
					if (war != null)
					{
						OnWarEnded(war, ReadWinner(war));
					}
					_knownWars.Remove(gone[i]);
				}
			}
		}
		catch (Exception ex)
		{
			Patches.LogOncePublic("ошибка опроса войн: " + ex.Message);
		}
	}

	/// <summary>Победитель войны из её данных (War.data.winner).</summary>
	private static WarWinner ReadWinner(War war)
	{
		try
		{
			object data = GameRefs.ReadMember(war, "data");
			object winner = (data == null) ? null : GameRefs.ReadMember(data, "winner");
			if (winner is WarWinner w)
			{
				return w;
			}
		}
		catch (Exception)
		{
		}
		return WarWinner.Nobody;
	}

	public static void OnWarStarted(Kingdom attacker, Kingdom defender, WarTypeAsset asset, War war)
	{
		try
		{
			PoliticalWorld world = RealPoliticMod.World;
			if (world == null || attacker == null)
			{
				return;
			}
			bool total = (defender == null) || (asset != null && asset.total_war);
			string typeKey = TypeKey(asset);
			// Этап 21: если защищающийся состоит в ванильном альянсе — альянс вступается за него.
			string allies = string.Empty;
			string attackerAllies = string.Empty;
			if (defender != null)
			{
				AllianceRecord alliance = world.AlliancesBook.Of((int)((NanoObject)defender).id);
				if (alliance != null)
				{
					allies = alliance.Name;
				}
			}
			// 0.23.4: альянс нападающего — если оба в альянсах, это война альянсов.
			AllianceRecord attackerAlliance = world.AlliancesBook.Of((int)((NanoObject)attacker).id);
			if (attackerAlliance != null && attackerAlliance.Name != allies)
			{
				attackerAllies = attackerAlliance.Name;
			}
			// Довоенный состав городов — чтобы после тотального поражения запомнить
			// земли уничтоженной державы как поглощённый вассалитет победителя.
			RememberPrewar(world, attacker);
			RememberPrewar(world, defender);
			int attackerId = (int)((NanoObject)attacker).id;
			int defenderId = defender == null ? -1 : (int)((NanoObject)defender).id;
			Dictionary<int, string> startAttackers = null;
			Dictionary<int, string> startDefenders = null;
			if (war != null)
			{
				long id = (long)((NanoObject)war).id;
				_warStartYear[id] = world.Year;
				string nativeWarName = ((NanoObject)war).name;
				// 0.31.12: название по коренным именам сторон («Бульунско-Альтасеронская война»),
				// без ярлыка идеологии; если имена не определились — название игры.
				string rootTitle = RealPolitic.Core.IdeologyStateNames.WarTitle(
					attacker == null ? null : ((NanoObject)attacker).name,
					defender == null ? null : ((NanoObject)defender).name);
				if (!string.IsNullOrWhiteSpace(rootTitle))
				{
					nativeWarName = rootTitle;
				}
				if (!string.IsNullOrWhiteSpace(nativeWarName))
				{
					_warNames[id] = nativeWarName;
				}
				if (attacker != null)
				{
					_warAttackerIds[id] = attackerId;
					if (!string.IsNullOrEmpty(attacker.name))
					{
						_warAttackerNames[id] = attacker.name;
					}
				}
				if (defender != null)
				{
					_warDefenderIds[id] = defenderId;
					if (!string.IsNullOrEmpty(defender.name))
					{
						_warDefenderNames[id] = defender.name;
					}
				}
				RecordWarParticipants(id, attacker, defender, war, world);
				_warAttackers.TryGetValue(id, out startAttackers);
				_warDefenders.TryGetValue(id, out startDefenders);
			}
			else
			{
				startAttackers = new Dictionary<int, string>();
				startDefenders = new Dictionary<int, string>();
				AddKingdomAndAllies(startAttackers, startDefenders, attacker, world);
				AddKingdomAndAllies(startDefenders, startAttackers, defender, world);
			}
			string attackerSide = FormatSideParticipants(attackerId, attacker.name, startAttackers);
			string defenderSide = FormatSideParticipants(defenderId, defender == null ? string.Empty : defender.name, startDefenders);
			world.Bus.Publish(new PolEvent(PolEventType.WarStarted, "RP_CHRON_EV_WAR_START")
			{
				Year = world.Year,
				Severity = Severity.Major
			}.With("attacker", attacker.name)
				.With("defender", (defender == null ? string.Empty : defender.name))
				.With("attacker_side", attackerSide)
				.With("defender_side", defenderSide)
				.With("total", total ? "1" : "0")
				.With("war_name", war != null && _warNames.TryGetValue((long)((NanoObject)war).id, out string startedWarName) ? startedWarName : string.Empty)
				.With("type", typeKey)
				.With("allies", allies)
				.With("attacker_allies", attackerAllies));
			// Этап 26: война, объявленная игрой в обход писаной конституции державы — акт вне закона:
			// мы не гасим ванильную войну (mod_only), но народ наказывает нарушителя.
			if (defender != null)
			{
				world.ApplyConstitutionalViolation(attackerId, defenderId);
			}
		}
		catch (Exception)
		{
			// войны не должны ломать игру
		}
	}

	private static void RecordWarParticipants(long warId, Kingdom attacker, Kingdom defender, War war, PoliticalWorld world)
	{
		if (!_warAttackers.TryGetValue(warId, out Dictionary<int, string> attackers))
		{
			attackers = new Dictionary<int, string>();
			_warAttackers[warId] = attackers;
		}
		if (!_warDefenders.TryGetValue(warId, out Dictionary<int, string> defenders))
		{
			defenders = new Dictionary<int, string>();
			_warDefenders[warId] = defenders;
		}
		AddKingdomAndAllies(attackers, defenders, attacker, world);
		AddKingdomAndAllies(defenders, attackers, defender, world);
		if (war != null)
		{
			AddWarCollection(attackers, defenders, GameRefs.Invoke(war, "getAttackers") ?? GameRefs.ReadMember(war, "_list_attackers"), world);
			AddWarCollection(defenders, attackers, GameRefs.Invoke(war, "getDefenders") ?? GameRefs.ReadMember(war, "_list_defenders"), world);
		}
	}

	private static void AddKingdomAndAllies(Dictionary<int, string> targetSide, Dictionary<int, string> oppositeSide, Kingdom kingdom, PoliticalWorld world)
	{
		if (kingdom == null)
		{
			return;
		}
		int id = (int)((NanoObject)kingdom).id;
		if (id <= 0 || oppositeSide.ContainsKey(id))
		{
			return;
		}
		string name = !string.IsNullOrEmpty(kingdom.name) ? kingdom.name : (world?.KingdomNameOf(id) ?? ("#" + id));
		targetSide[id] = name;
		if (world != null)
		{
			RememberPrewar(world, kingdom);
			AllianceRecord alliance = world.AlliancesBook.Of(id);
			if (alliance?.Members != null)
			{
				for (int i = 0; i < alliance.Members.Count; i++)
				{
					int allyId = alliance.Members[i];
					if (allyId > 0 && !oppositeSide.ContainsKey(allyId) && !targetSide.ContainsKey(allyId))
					{
						Kingdom liveAlly = FindLiveKingdom(allyId);
						string allyName = liveAlly != null && !string.IsNullOrEmpty(liveAlly.name)
							? liveAlly.name
							: world.KingdomNameOf(allyId);
						if (!string.IsNullOrEmpty(allyName))
						{
							targetSide[allyId] = allyName;
						}
						if (liveAlly != null)
						{
							RememberPrewar(world, liveAlly);
						}
					}
				}
			}
		}
		try
		{
			object nativeAlliance = GameRefs.Invoke(kingdom, "getAlliance");
			System.Collections.IEnumerable membersList = AllianceSafety.Members(nativeAlliance);
			if (membersList != null)
			{
				foreach (object memberObj in membersList)
				{
					if (!(memberObj is Kingdom member)) continue;
					int memberId = (int)((NanoObject)member).id;
					if (memberId > 0 && !oppositeSide.ContainsKey(memberId) && !targetSide.ContainsKey(memberId))
					{
						targetSide[memberId] = !string.IsNullOrEmpty(member.name) ? member.name : ("#" + memberId);
						if (world != null)
						{
							RememberPrewar(world, member);
						}
					}
				}
			}
		}
		catch (Exception)
		{
		}
	}

	private static void AddWarCollection(Dictionary<int, string> targetSide, Dictionary<int, string> oppositeSide, object rawCollection, PoliticalWorld world)
	{
		if (!(rawCollection is System.Collections.IEnumerable enumerable))
		{
			return;
		}
		foreach (object item in enumerable)
		{
			if (item is Kingdom k)
			{
				AddKingdomAndAllies(targetSide, oppositeSide, k, world);
			}
		}
	}

	private static Kingdom FindLiveKingdom(int kingdomId)
	{
		try
		{
			List<Kingdom> list = World.world?.kingdoms?.list;
			if (list != null)
			{
				for (int i = 0; i < list.Count; i++)
				{
					Kingdom k = list[i];
					if (k != null && (int)((NanoObject)k).id == kingdomId)
					{
						return k;
					}
				}
			}
		}
		catch (Exception)
		{
		}
		return null;
	}

	private static bool IsKingdomAliveOnMap(int kingdomId, PoliticalWorld world)
	{
		Kingdom live = FindLiveKingdom(kingdomId);
		if (live != null)
		{
			int cities = CitiesLeft(live);
			if (cities == 0)
			{
				return false;
			}
			return true;
		}
		if (World.world?.kingdoms?.list != null)
		{
			return false;
		}
		return world?.GetKingdom(kingdomId) != null;
	}

	public static WarCoalitionOutcome BuildCoalitionOutcome(
		IReadOnlyDictionary<int, string> attackers,
		IReadOnlyDictionary<int, string> defenders,
		Func<int, bool> isAlive)
	{
		WarCoalitionOutcome outcome = new WarCoalitionOutcome();
		if (attackers != null && attackers.Count > 0)
		{
			outcome.TotalAttackers = attackers.Count;
			foreach (KeyValuePair<int, string> kv in attackers)
			{
				outcome.AttackerSideIds.Add(kv.Key);
				if (!isAlive(kv.Key))
				{
					outcome.DestroyedAttackers++;
					if (!string.IsNullOrEmpty(kv.Value))
					{
						outcome.DestroyedAttackerNames.Add(kv.Value);
					}
				}
			}
		}
		if (defenders != null && defenders.Count > 0)
		{
			outcome.TotalDefenders = defenders.Count;
			foreach (KeyValuePair<int, string> kv in defenders)
			{
				outcome.DefenderSideIds.Add(kv.Key);
				if (!isAlive(kv.Key))
				{
					outcome.DestroyedDefenders++;
					if (!string.IsNullOrEmpty(kv.Value))
					{
						outcome.DestroyedDefenderNames.Add(kv.Value);
					}
				}
			}
		}
		return outcome;
	}

	private static void RememberPrewar(PoliticalWorld world, Kingdom kingdom)
	{
		try
		{
			if (kingdom == null)
			{
				return;
			}
			List<City> cities = GameRefs.KingdomCities(kingdom);
			if (cities == null || cities.Count == 0)
			{
				return;
			}
			List<int> ids = new List<int>(cities.Count);
			for (int i = 0; i < cities.Count; i++)
			{
				if (cities[i] != null)
				{
					ids.Add((int)((NanoObject)cities[i]).id);
				}
			}
			City capital = GameRefs.KingdomCapital(kingdom);
			world.RememberPrewarCities((int)((NanoObject)kingdom).id, ids, capital == null ? 0 : (int)((NanoObject)capital).id);
		}
		catch (Exception)
		{
		}
	}

	/// <summary>Сколько городов осталось у державы (0 — её больше нет на карте).</summary>
	private static int CitiesLeft(Kingdom kingdom)
	{
		try
		{
			if (kingdom == null)
			{
				return 0;
			}
			if (GameRefs.Invoke(kingdom, "isAlive") is bool alive && !alive)
			{
				return 0;
			}
			List<City> cities = GameRefs.KingdomCities(kingdom);
			return cities == null ? 0 : cities.Count;
		}
		catch (Exception)
		{
			return -1;
		}
	}

	/// <summary>Перегрузка для Harmony-патча: победитель приходит как object (boxed enum).</summary>
	public static void OnWarEnded(War war, object winner)
	{
		WarWinner typed = (winner is WarWinner w) ? w : WarWinner.Nobody;
		OnWarEnded(war, typed);
	}

	public static void OnWarEnded(War war, WarWinner winner)
	{
		try
		{
				PoliticalWorld world = RealPoliticMod.World;
				if (world == null || war == null)
				{
					return;
				}
				long id = (long)((NanoObject)war).id;
				if (!_endedHandled.Add(id))
				{
					return;
				}
				string currentAttackerName = string.Empty;
				string currentDefenderName = string.Empty;
				int currentAttackerId = -1;
				int currentDefenderId = -1;
				int loserCitiesLeft = -1;
				Kingdom currentAttacker = null;
				Kingdom currentDefender = null;
				try
				{
					currentAttacker = GameRefs.Invoke(war, "getMainAttacker") as Kingdom;
					if (currentAttacker != null)
					{
						currentAttackerName = currentAttacker.name;
						currentAttackerId = (int)((NanoObject)currentAttacker).id;
					}
					currentDefender = GameRefs.Invoke(war, "getMainDefender") as Kingdom;
					if (currentDefender != null)
					{
						currentDefenderName = currentDefender.name;
						currentDefenderId = (int)((NanoObject)currentDefender).id;
					}
					RecordWarParticipants(id, currentAttacker, currentDefender, war, world);
				}
				catch (Exception)
				{
				}
			_warAttackerIds.TryGetValue(id, out int initialAttackerId);
			_warDefenderIds.TryGetValue(id, out int initialDefenderId);
			_warAttackerNames.TryGetValue(id, out string cachedAttacker);
			_warDefenderNames.TryGetValue(id, out string cachedDefender);
			_warAttackers.TryGetValue(id, out Dictionary<int, string> attackerMap);
			_warDefenders.TryGetValue(id, out Dictionary<int, string> defenderMap);
			_warAttackerIds.Remove(id);
			_warDefenderIds.Remove(id);
			_warAttackerNames.Remove(id);
			_warDefenderNames.Remove(id);
			_warAttackers.Remove(id);
			_warDefenders.Remove(id);

			// Сохраняем исходные названия зачинщика и главной цели войны, даже если движок после уничтожения
			// главной цели переключил getMainDefender/getMainAttacker на уцелевшего союзника.
			string attackerName = !string.IsNullOrEmpty(cachedAttacker) ? cachedAttacker : currentAttackerName;
			string defenderName = !string.IsNullOrEmpty(cachedDefender) ? cachedDefender : currentDefenderName;
			int attackerId = initialAttackerId > 0 ? initialAttackerId : currentAttackerId;
			int defenderId = initialDefenderId > 0 ? initialDefenderId : currentDefenderId;

			WarCoalitionOutcome coalition = BuildCoalitionOutcome(
				attackerMap,
				defenderMap,
				kId => IsKingdomAliveOnMap(kId, world));

			if (winner != WarWinner.Merged)
			{
				int rawSign = winner == WarWinner.Attackers ? 1 : winner == WarWinner.Defenders ? -1 : 0;
				int effectiveSign = coalition.ResolveWinnerSide(rawSign);
				if (effectiveSign > 0)
				{
					winner = WarWinner.Attackers;
				}
				else if (effectiveSign < 0)
				{
					winner = WarWinner.Defenders;
				}
			}

			try
			{
				Kingdom mainDef = FindLiveKingdom(defenderId) ?? currentDefender;
				Kingdom mainAtt = FindLiveKingdom(attackerId) ?? currentAttacker;
				if (winner == WarWinner.Attackers)
				{
					loserCitiesLeft = defenderId > 0 && !IsKingdomAliveOnMap(defenderId, world) ? 0 : CitiesLeft(mainDef);
				}
				else if (winner == WarWinner.Defenders)
				{
					loserCitiesLeft = attackerId > 0 && !IsKingdomAliveOnMap(attackerId, world) ? 0 : CitiesLeft(mainAtt);
				}
			}
			catch (Exception)
			{
			}

			// Если первоначальный лидер стороны был уничтожен и удалён из реестра до конца коалиционной войны,
			// подставляем уцелевшего представителя стороны для оформления мирного договора и перемирия.
			int settlementAttackerId = ResolveNegotiatorId(attackerId, currentAttackerId, attackerMap, world);
			int settlementDefenderId = ResolveNegotiatorId(defenderId, currentDefenderId, defenderMap, world);

			string duration = string.Empty;
			_warStartYear.TryGetValue(id, out int startYear);
			_warStartYear.Remove(id);
			if (startYear > 0 && world.Year >= startYear)
			{
				duration = (world.Year - startYear + 1).ToString();
			}
			string attackerSide = FormatSideParticipants(attackerId, attackerName, attackerMap);
			string defenderSide = FormatSideParticipants(defenderId, defenderName, defenderMap);
			_warNames.TryGetValue(id, out string endedWarName);
			_warNames.Remove(id);
				world.Bus.Publish(new PolEvent(PolEventType.WarEnded, "RP_CHRON_EV_WAR_END")
				{
					Year = world.Year,
					Severity = Severity.Major
				}.With("attacker", attackerName)
					.With("defender", defenderName)
					.With("attacker_side", attackerSide)
					.With("defender_side", defenderSide)
					.With("winner", "RP_WARWINNER_" + winner.ToString().ToUpperInvariant())
					.With("war_name", endedWarName ?? string.Empty)
					.With("duration", duration)
					.With("casualties", coalition.FormatCasualtiesSummary()));
			// Этап 22: ядро оформляет развязку — перемирие и условия победителя.
			try
			{
				RealPoliticMod.Feeder?.NotifyWarEnded(settlementAttackerId, settlementDefenderId, winner, startYear, loserCitiesLeft, coalition);
			}
			catch (Exception)
			{
				// Мирные договоры не должны ломать завершение войны.
			}
			}
		catch (Exception)
		{
		}
	}

	private static int ResolveNegotiatorId(int initialId, int currentId, Dictionary<int, string> sideParticipants, PoliticalWorld world)
	{
		if (initialId > 0 && world.GetKingdom(initialId) != null)
		{
			return initialId;
		}
		if (currentId > 0 && world.GetKingdom(currentId) != null)
		{
			return currentId;
		}
		if (sideParticipants != null)
		{
			foreach (int kId in sideParticipants.Keys)
			{
				if (kId > 0 && world.GetKingdom(kId) != null)
				{
					return kId;
				}
			}
		}
		return initialId > 0 ? initialId : currentId;
	}

	/// <summary>
	/// Формирует список всех держав стороны («Чехия, Русь, Сербия» или «Польша, Португалия, Италия»),
	/// ставя зачинщика/главную цель первым.
	/// </summary>
	public static string FormatSideParticipants(int mainId, string mainName, Dictionary<int, string> sideParticipants)
	{
		List<string> names = new List<string>();
		if (!string.IsNullOrEmpty(mainName))
		{
			names.Add(mainName);
		}
		if (sideParticipants != null)
		{
			foreach (KeyValuePair<int, string> kv in sideParticipants)
			{
				if (kv.Key == mainId || string.IsNullOrEmpty(kv.Value))
				{
					continue;
				}
				if (!names.Contains(kv.Value))
				{
					names.Add(kv.Value);
				}
			}
		}
		return string.Join(", ", names);
	}

	/// <summary>Ключ локализации типа войны из name_template ассета («war_conquest» → RP_WAR_TYPE_CONQUEST).</summary>
	private static string TypeKey(WarTypeAsset asset)
	{
		if (asset == null || string.IsNullOrEmpty(asset.name_template))
		{
			return string.Empty;
		}
		string template = asset.name_template;
		if (template.StartsWith("war_", StringComparison.Ordinal))
		{
			template = template.Substring(4);
		}
		string key = "RP_WAR_TYPE_" + template.ToUpperInvariant();
		// В ассетах игры «war_whisper», а в локалях ключ полный — RP_WAR_TYPE_WHISPER_OF_WAR.
		if (key == "RP_WAR_TYPE_WHISPER")
		{
			key = "RP_WAR_TYPE_WHISPER_OF_WAR";
		}
		return key;
	}
}
