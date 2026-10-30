using System;
using System.Collections.Generic;
using NeoModLoader.services;
using RealPolitic.Core;

namespace RealPolitic;

/// <summary>
/// 0.46: переселение жителей между державами — исполнение в игре.
///
/// Схема (по правилу владельца от 29.10.2026):
///   1. Житель, которому плохо (притесняют род, беден, устал от войны, недоволен строем),
///      подаёт заявление на переезд в другую державу.
///   2. Заявление рассматривают ДВЕ стороны: родина и принимающая держава.
///   3. Обе согласны — обычный переезд.
///      Родина против, а принимающая за — житель сбегает; перешёл границу — стал
///      гражданином новой державы (или пойман, если не повезло).
///      Родина за, принимающая против — остаётся дома.
///   4. Малые народы бегут охотнее: при притеснении давление на них максимальное
///      (см. RaceSurvival), и они уходят туда, где их терпят.
///
/// В игре переезд делается родным вызовом Actor.setCity(city) (он же переносит
/// жителя в державу города), а если игра его не приняла — Actor.joinCity(city).
/// </summary>
public static class MigrationRuntime
{
	private static int _lastYear = -1;
	private static int _movesThisYear;
	private static readonly Dictionary<long, int> _retryAfter = new Dictionary<long, int>();
	private static readonly List<string> _pendingLog = new List<string>();

	/// <summary>Сколько переездов состоялось за последний год (для окна и журнала).</summary>
	public static int LastYearMoves { get; private set; }

	/// <summary>Сколько человек сбежало за последний год.</summary>
	public static int LastYearEscapes { get; private set; }

	public static void Reset()
	{
		_lastYear = -1;
		_retryAfter.Clear();
		_pendingLog.Clear();
		LastYearMoves = 0;
		LastYearEscapes = 0;
	}

	/// <summary>Годовой шаг переселения.</summary>
	public static void AnnualTick(PoliticalWorld world, MapBox map, int year,
		IReadOnlyDictionary<int, KingdomInput> inputs)
	{
		try
		{
			if (world == null || map == null || inputs == null || world.Params == null || !world.Params.EnableMigration)
			{
				return;
			}
			if (_lastYear == year)
			{
				return;
			}
			_lastYear = year;
			_movesThisYear = 0;
			LastYearMoves = 0;
			LastYearEscapes = 0;
			_pendingLog.Clear();
			List<Kingdom> kingdoms = GameRefs.KingdomList(map);
			if (kingdoms == null || kingdoms.Count < 2)
			{
				return;
			}
			int budget = Math.Max(1, world.Params.MigrationPerYear);
			// Сначала собираем «желающих» — по одному жителю с державы, и только тех,
			// кому действительно плохо. Такой обход дешёвый: перепись уже сделана.
			for (int k = 0; k < kingdoms.Count && budget > 0; k++)
			{
				Kingdom from = kingdoms[k];
				if (from == null)
				{
					continue;
				}
				int fromId = (int)((NanoObject)from).id;
				KingdomPolitics home = world.GetKingdom(fromId);
				KingdomInput homeInput;
				if (home == null || !inputs.TryGetValue(fromId, out homeInput))
				{
					continue;
				}
				List<MigrationCandidate> candidates = BuildCandidates(world, map, fromId, homeInput);
				if (candidates.Count == 0)
				{
					continue;
				}
				int moved = TryResettleOne(world, map, from, fromId, home, homeInput, candidates, year);
				if (moved > 0)
				{
					budget -= moved;
					_movesThisYear += moved;
				}
			}
			LastYearMoves = _movesThisYear;
			if (_pendingLog.Count > 0)
			{
				LogService.LogInfo("[Real Politic] переселение за год " + year + ": " + string.Join("; ", _pendingLog));
			}
		}
		catch (Exception ex)
		{
			LogService.LogError("[Real Politic] переселение: " + ex.Message);
		}
	}

	/// <summary>Список держав, куда жителю этой державы есть смысл податься.</summary>
	private static List<MigrationCandidate> BuildCandidates(PoliticalWorld world, MapBox map, int fromId, KingdomInput homeInput)
	{
		List<MigrationCandidate> list = new List<MigrationCandidate>();
		if (world == null || homeInput.Relations == null)
		{
			return list;
		}
		for (int r = 0; r < homeInput.Relations.Count; r++)
		{
			RelationInfo relation = homeInput.Relations[r];
			if (relation.OtherId == fromId || relation.Relation == RelationType.War)
			{
				continue;
			}
			KingdomPolitics target = world.GetKingdom(relation.OtherId);
			if (target == null)
			{
				continue;
			}
			list.Add(new MigrationCandidate
			{
				KingdomId = relation.OtherId,
				Tolerance = 0.5f, // уточняется ниже, когда известен род переселенца
				Prosperity = target.Economy?.Prosperity ?? 50f,
				Neighbouring = relation.Adjacent,
				AtWar = target.InWar
			});
		}
		return list;
	}

	/// <summary>
	/// Пробует переселить одного жителя из державы. Возвращает 1, если кто-то переехал.
	/// Выбор жителя: обходим города (начиная с разных каждый год), ищем не-воина,
	/// не правителя, кому давление выше порога.
	/// </summary>
	private static int TryResettleOne(PoliticalWorld world, MapBox map, Kingdom from, int fromId,
		KingdomPolitics home, KingdomInput homeInput, List<MigrationCandidate> candidates, int year)
	{
		RacePolicy homePolicy = RaceSurvival.PolicyOf(home);
		float homePressureBonus = 0f;
		int population = Math.Max(0, homeInput.Population);
		if (population <= 0)
		{
			return 0;
		}
		List<RaceShare> races = CountRaces(from, out int totalPopulation);
		if (races.Count > 1 && world.Params.EnableRaceSurvival)
		{
			RaceShare smallest = RaceSurvival.Smallest(races);
			if (smallest != null && RaceSurvival.IsEndangered(smallest.Population, totalPopulation))
			{
				homePressureBonus = RaceSurvival.Pressure(homePolicy, true, smallest.Population, totalPopulation) * 0.5f;
			}
		}
		List<City> cities = GameRefs.KingdomCities(from);
		if (cities == null || cities.Count == 0)
		{
			return 0;
		}
		int start = Math.Abs(year) % cities.Count;
		for (int ci = 0; ci < cities.Count; ci++)
		{
			City city = cities[(start + ci) % cities.Count];
			if (city == null)
			{
				continue;
			}
			List<Actor> units = GameRefs.ReadMember(city, "units") as List<Actor>;
			if (units == null || units.Count == 0)
			{
				continue;
			}
			int stride = Math.Max(1, units.Count / 12);
			for (int i = 0; i < units.Count; i += stride)
			{
				Actor actor = units[i];
				if (actor == null || GameRefs.IsBoatUnit(actor) || GameRefs.IsWarrior(actor))
				{
					continue;
				}
				int actorId = GameText.ActorId(actor);
				if (actorId < 0 || GameRefs.IsRulerActor(actor, from) || GameRefs.IsCityLeaderActor(actor, city))
				{
					continue;
				}
				if (_retryAfter.TryGetValue(actorId, out int retryYear) && year < retryYear)
				{
					continue;
				}
				float dissent = home.AvgDissent;
				float prosperity = home.Economy?.Prosperity ?? 50f;
				float pressure = MigrationRules.Pressure(dissent, homePressureBonus * 2f, prosperity, homeInput.AtWar);
				if (pressure < MigrationRules.WanderlustThreshold)
				{
					return 0; // в этой державе людям терпимо — заявлений не будет
				}
				string raceId = GameRefs.ActorSpeciesId(actor);
				float loyalty = Math.Max(0f, Math.Min(1f, home.Support / 100f));
				bool lastOfRace = IsLastOfRace(races, raceId);
				// Куда: среди кандидатов, которые терпят именно этот род.
				List<MigrationCandidate> tuned = new List<MigrationCandidate>(candidates.Count);
				for (int c = 0; c < candidates.Count; c++)
				{
					MigrationCandidate candidate = candidates[c];

					// Если держава лишила этот народ гражданства, въезд туда не
					// рассматривается вовсе, даже если раньше она его принимала.
					if (world.IsEntryBanned(candidate.KingdomId, raceId, year))
					{
						continue;
					}
					KingdomPolitics target = world.GetKingdom(candidate.KingdomId);
					RacePolicy policy = RaceSurvival.PolicyOf(target);
					bool sameRace = target != null && !string.IsNullOrEmpty(target.TitularRaceId)
						&& target.TitularRaceId == raceId;
					candidate.Tolerance = RaceSurvival.Tolerance(policy, sameRace, target?.Legitimacy ?? 50f);
					tuned.Add(candidate);
				}
				int destination = MigrationRules.ChooseDestination(tuned);
				if (destination <= 0)
				{
					return 0;
				}
				MigrationCandidate dest = tuned.Find(c => c.KingdomId == destination);
				KingdomPolitics host = world.GetKingdom(destination);
				Constitution homeConstitution = home.Constitution;
				BorderLaw borders = homeConstitution?.Borders ?? BorderLaw.Controlled;
				WatchLaw watch = homeConstitution?.Watch ?? WatchLaw.Patrol;

				MigrationRequest request = new MigrationRequest
				{
					ActorId = actorId,
					ActorName = GameRefs.ActorDisplayName(actor),
					RaceId = raceId ?? string.Empty,
					FromKingdom = fromId,
					ToKingdom = destination,
					Year = year,
					Pressure = pressure,
					Reason = ReasonFor(home, homeInput, homePressureBonus > 0f)
				};
				request.HomeDecision = MigrationRules.DecideHome(borders, loyalty, lastOfRace, population);
				double roll = world.Rng.NextDouble();
				request.HostDecision = MigrationRules.DecideHost(dest.Tolerance, (float)roll);
				float escapeChance = MigrationRules.EscapeChance(pressure, borders, watch, dest.Neighbouring);
				bool escapeOk = world.Rng.NextDouble() < escapeChance;
				request.Outcome = MigrationRules.Resolve(request.HomeDecision, request.HostDecision, escapeOk);

				if (request.Outcome == MigrationOutcome.Agreed || request.Outcome == MigrationOutcome.Escaped)
				{
					City targetCity = ChooseTargetCity(GameRefs.KingdomCities(KingdomById(map, destination)), destination);
					if (targetCity == null || !MoveActorToCity(actor, targetCity))
					{
						_retryAfter[actorId] = year + MigrationRules.RetryYears;
						return 0;
					}
					if (request.Outcome == MigrationOutcome.Escaped)
					{
						LastYearEscapes++;
					}
					_pendingLog.Add(request.Describe());
					Publish(world, request);
					return 1;
				}
				// Неудача: заявление можно повторить через несколько лет.
				_retryAfter[actorId] = year + MigrationRules.RetryYears;
				if (request.Outcome == MigrationOutcome.Caught)
				{
					_pendingLog.Add(request.Describe());
					Publish(world, request);
				}
				return 0;
			}
		}
		return 0;
	}

	private static Kingdom KingdomById(MapBox map, int kingdomId)
	{
		try
		{
			List<Kingdom> list = GameRefs.KingdomList(map);
			if (list == null)
			{
				return null;
			}
			for (int i = 0; i < list.Count; i++)
			{
				if (list[i] != null && (int)((NanoObject)list[i]).id == kingdomId)
				{
					return list[i];
				}
			}
		}
		catch (Exception)
		{
		}
		return null;
	}

	private static City ChooseTargetCity(List<City> cities, int kingdomId)
	{
		if (cities == null)
		{
			return null;
		}
		City best = null;
		int bestPop = -1;
		for (int i = 0; i < cities.Count; i++)
		{
			City city = cities[i];
			if (city == null)
			{
				continue;
			}
			int owner = (int)((city.kingdom == null) ? -1 : ((NanoObject)city.kingdom).id);
			if (owner != kingdomId)
			{
				continue;
			}
			if (GameRefs.ReadMember(city, "units") is List<Actor> units && units.Count > bestPop)
			{
				bestPop = units.Count;
				best = city;
			}
		}
		return best;
	}

	/// <summary>
	/// 0.47: возвращение народа (репатриация) после решения КМУД.
	/// Переселяем до <paramref name="limit"/> человек указанного рода из чужих держав
	/// в выбранную державу — настоящим переездом (setCity/joinCity), как обычная миграция.
	/// Возвращает число переехавших.
	/// </summary>
	public static int ResettleRace(PoliticalWorld world, MapBox map, string raceId, int toKingdomId, int limit)
	{
		if (world == null || map == null || string.IsNullOrEmpty(raceId) || toKingdomId <= 0 || limit <= 0)
		{
			return 0;
		}
		int moved = 0;
		try
		{
			City target = ChooseTargetCity(GameRefs.KingdomCities(KingdomById(map, toKingdomId)), toKingdomId);
			if (target == null)
			{
				return 0;
			}
			List<Kingdom> kingdoms = GameRefs.KingdomList(map);
			if (kingdoms == null)
			{
				return 0;
			}
			for (int k = 0; k < kingdoms.Count && moved < limit; k++)
			{
				Kingdom from = kingdoms[k];
				if (from == null)
				{
					continue;
				}
				int fromId = (int)((NanoObject)from).id;
				if (fromId == toKingdomId)
				{
					continue;
				}
				List<City> cities = GameRefs.KingdomCities(from);
				if (cities == null)
				{
					continue;
				}
				for (int ci = 0; ci < cities.Count && moved < limit; ci++)
				{
					City city = cities[ci];
					if (city == null || !(GameRefs.ReadMember(city, "units") is List<Actor> units))
					{
						continue;
					}
					int stride = Math.Max(1, units.Count / 20);
					for (int i = 0; i < units.Count && moved < limit; i += stride)
					{
						Actor actor = units[i];
						if (actor == null || GameRefs.IsBoatUnit(actor) || GameRefs.IsRulerActor(actor, from))
						{
							continue;
						}
						if ((GameRefs.ActorSpeciesId(actor) ?? string.Empty) != raceId)
						{
							continue;
						}
						if (MoveActorToCity(actor, target))
						{
							moved++;
						}
					}
				}
			}
			if (moved > 0)
			{
				LogService.LogInfo("[Real Politic] КМУД: возвращено человек рода " + raceId + ": " + moved);
			}
		}
		catch (Exception ex)
		{
			LogService.LogError("[Real Politic] возвращение народа: " + ex.Message);
		}
		return moved;
	}

	/// <summary>Переезд в игре: setCity (он же переносит жителя в державу города), затем joinCity.</summary>
	internal static bool MoveActorToCity(Actor actor, City city)
	{
		try
		{
			if (actor == null || city == null)
			{
				return false;
			}
			GameRefs.Invoke(actor, "setCity", city);
			object current = GameRefs.Invoke(actor, "getCity");
			if (!ReferenceEquals(current, city))
			{
				GameRefs.Invoke(actor, "joinCity", city);
				current = GameRefs.Invoke(actor, "getCity");
			}
			return ReferenceEquals(current, city);
		}
		catch (Exception ex)
		{
			LogService.LogInfo("[Real Politic] переезд не удался: " + ex.Message);
			return false;
		}
	}

	private static MigrationReason ReasonFor(KingdomPolitics home, KingdomInput input, bool racePressure)
	{
		if (racePressure)
		{
			return MigrationReason.Persecution;
		}
		if (input.AtWar || home.InWar)
		{
			return MigrationReason.War;
		}
		if ((home.Economy?.Prosperity ?? 50f) < 40f || home.Starving)
		{
			return MigrationReason.Poverty;
		}
		return MigrationReason.Dissent;
	}

	private static bool IsLastOfRace(List<RaceShare> races, string raceId)
	{
		if (races == null || string.IsNullOrEmpty(raceId))
		{
			return false;
		}
		for (int i = 0; i < races.Count; i++)
		{
			if (races[i].RaceId == raceId)
			{
				return races[i].Population <= 1;
			}
		}
		return false;
	}

	/// <summary>Сколько жителей какого рода в державе (по городам, без кораблей).</summary>
	public static List<RaceShare> CountRaces(Kingdom kingdom, out int total)
	{
		total = 0;
		Dictionary<string, int> tally = new Dictionary<string, int>(StringComparer.Ordinal);
		List<RaceShare> result = new List<RaceShare>();
		try
		{
			List<City> cities = GameRefs.KingdomCities(kingdom);
			if (cities == null)
			{
				return result;
			}
			for (int c = 0; c < cities.Count; c++)
			{
				City city = cities[c];
				if (city == null || !(GameRefs.ReadMember(city, "units") is List<Actor> units))
				{
					continue;
				}
				// На город — ограниченная выборка: точность здесь не нужна, нужен порядок цифр.
				int stride = Math.Max(1, units.Count / 40);
				for (int i = 0; i < units.Count; i += stride)
				{
					Actor actor = units[i];
					if (actor == null || GameRefs.IsBoatUnit(actor))
					{
						continue;
					}
					string species = GameRefs.ActorSpeciesId(actor) ?? string.Empty;
					tally.TryGetValue(species, out int count);
					tally[species] = count + stride;
					total += stride;
				}
			}
			foreach (KeyValuePair<string, int> pair in tally)
			{
				// Имя рода — по-русски, как его зовут в игре («лисы», «куры»), а не «fox».
				string name = GameText.Localized(pair.Key, string.Empty);
				if (string.IsNullOrEmpty(name))
				{
					name = GameText.Localized("race_" + pair.Key, pair.Key);
				}
				result.Add(new RaceShare { RaceId = pair.Key, RaceName = name, Population = pair.Value });
			}
		}
		catch (Exception)
		{
		}
		return result;
	}

	private static void Publish(PoliticalWorld world, MigrationRequest request)
	{
		try
		{
			if (world?.Bus == null)
			{
				return;
			}
			PolEvent evt = new PolEvent(PolEventType.MigrationResettled, "RP_EV_MIGRATION") { Year = request.Year };
			evt.Severity = request.Outcome == MigrationOutcome.Escaped ? Severity.Major : Severity.Minor;
			evt.KingdomId = request.FromKingdom;
			world.Bus.Publish(evt
				.With("person", request.ActorName)
				.With("from", world.KingdomNameOf(request.FromKingdom))
				.With("to", world.KingdomNameOf(request.ToKingdom))
				.With("race", request.RaceId ?? string.Empty)
				.With("outcome", request.Outcome.ToString())
				.With("why", request.Reason.ToString()));
		}
		catch (Exception ex)
		{
			LogService.LogInfo("[Real Politic] уведомление о переселении: " + ex.Message);
		}
	}
}
