using System;
using System.Collections.Generic;

namespace RealPolitic.Core;

/// <summary>
/// Внешние данные, нужные механике гражданской войны. Все делегаты необязательны:
/// ядро должно работать и в тестах, где нет ни городов игры, ни имён юнитов.
/// </summary>
public sealed class CivilWarContext
{
	/// <summary>Доминирующая идеология жителей города (для выбора знамени мятежа).</summary>
	public Func<int, IdeologyType>? CityIdeology { get; set; }

	/// <summary>Недовольство жителей города 0..1 (для выбора мятежных городов).</summary>
	public Func<int, float>? CityUnrest { get; set; }

	/// <summary>Имя предводителя мятежа: (kingdomId, cityId) → имя.</summary>
	public Func<int, int, string>? RebelLeaderName { get; set; }

	/// <summary>Предводитель мятежа — женщина: (kingdomId, cityId) → bool.</summary>
	public Func<int, int, bool>? RebelLeaderIsFemale { get; set; }

	/// <summary>ID юнита-предводителя: (kingdomId, cityId) → actorId.</summary>
	public Func<int, int, int>? RebelLeaderActorId { get; set; }

	/// <summary>Сколько городов сейчас у государства (по данным мира); null — неизвестно.</summary>
	public Func<int, int>? KingdomCityCount { get; set; }

	/// <summary>Кому принадлежит город сейчас (id государства; 0 — города нет).</summary>
	public Func<int, int>? CityOwner { get; set; }
}

/// <summary>
/// Гражданские войны и расколы государств (этап 17).
///
/// Механика (0.23): при низком здоровье державы часть городов поднимает мятеж под знаменем
/// одной из причин (<see cref="CivilWarCause"/>). Мятежные города СРАЗУ образуют
/// государство повстанцев, которое объявляет войну метрополии, — дальше воюют
/// настоящие армии WorldBox. Ядро лишь наблюдает за исходом:
/// государство повстанцев исчезло — мятеж подавлен; повстанцы взяли столицу — переворот
/// (смена строя и правителя); заключён мир — отделение состоялось.
///
/// Класс чистый: мир не меняет, события публикует в шину, исход возвращает вызывающему
/// (<see cref="PoliticalWorld"/> применяет его — в том числе создаёт новое государство).
/// </summary>
public static class CivilWars
{
	/// <summary>Может ли государство вообще начать гражданскую войну (без броска кубика).</summary>
	public static bool CanStart(KingdomPolitics state, KingdomInput input, SimParams p, int year)
	{
		if (state == null || state.CivilWar != null)
		{
			return false;
		}
		// Революция уже идёт — второй внутренний конфликт не открываем.
		if (state.Revolution != null)
		{
			return false;
		}
		// Первые 50 лет после рождения государства народ не поднимает мятежи против молодой власти.
		if (state.IsYoungState(year, p?.YoungStateGraceYears ?? 50))
		{
			return false;
		}
		// Отделяться нечему: нужен хотя бы один город помимо столицы.
		if (input.Cities == null || input.Cities.Count < 2)
		{
			return false;
		}
		// Передышка после предыдущей гражданской войны.
		if (state.LastCivilWarYear > 0 && year - state.LastCivilWarYear < p.CivilWarCooldownYears)
		{
			return false;
		}
		// Отделения выключены: мятежные города не смогут образовать государство повстанцев
		// (StartRebelKingdom гасит мятеж в зародыше), поэтому и затевать восстание незачем —
		// механика честно отключается целиком одной настройкой EnableSecessions.
		if (p != null && !p.AllowSecession)
		{
			return false;
		}
		return true;
	}

	/// <summary>Годовой шанс начала мятежа (0..1) — детерминированная функция состояния.</summary>
	public static float StartChance(KingdomPolitics state, KingdomInput input, SimParams p)
	{
		// Недовольство — обязательное топливо мятежа: при спокойных подданных
		// восстанию неоткуда взяться (множитель стремится к нулю).
		float dissentFactor = Clamp(state.AvgDissent / 0.35f, 0f, 1.5f);
		// Легитимность: выше порога шанс почти гаснет, ниже — растёт вплоть до 2.5.
		float legitimacyFactor = Clamp(1f + (p.CivilWarLegitimacyLimit - state.Legitimacy) / 35f, 0.15f, 2.5f);
		float chance = p.CivilWarBaseChance * dissentFactor * legitimacyFactor;
		if (state.Starving)
		{
			chance *= 1.6f;
		}
		if (state.Economy != null && state.Economy.InCrisis)
		{
			chance *= 1f + (p.EconomicCrisisRebellionFactor - 1f) * EconomicCrises.Severity(state.Economy.Crisis) * 0.5f;
		}
		if (state.InWar)
		{
			chance *= 1.3f;
		}
		// Этап 22: изнурённость войной разбивает тыл — от 50 усталости риск мятежа растёт до ×1.5.
		if (p.EnableWarPeace)
		{
			chance *= WarFatigueDefs.RebellionFactor(state.WarFatigue);
		}
		if (state.Regency != null && state.Regency.IsActive)
		{
			chance *= 1.5f;
		}
		// Сплочённость строя гасит мятежи: у милитаризма и этнонационализма тыл крепче.
		chance *= 1.4f - state.Def.Cohesion * 0.6f;
		// 0.24: статья XII «О тайной страже» — сыск душит заговоры, вольница их множит.
		if (state.Constitution != null)
		{
			chance *= ConstitutionEffects.WatchRebellionMultiplier(state.Constitution.Watch, state.Constitution.Supremacy == SupremacyRule.Binding);
		}
		if (state.Ideology == IdeologyType.Ethnonationalism && CountForeignCities(state, input) > 0)
		{
			chance *= 1.8f;
		}
		if (TribalCivilization.NeedsCivilization(state.Ideology) && state.TribalAwakeningAnnounced && state.AvgDissent >= 0.30f)
		{
			chance = Math.Max(chance, 0.14f) * 1.45f;
		}
		if (input.Cities != null && input.Cities.Count >= p.CivilWarOverexpansionCities)
		{
			chance *= 1.25f;
		}
		if (chance < 0f)
		{
			return 0f;
		}
		return chance > p.CivilWarChanceCap ? p.CivilWarChanceCap : chance;
	}

	/// <summary>
	/// Попытка начать гражданскую войну. Возвращает true, если мятеж начался
	/// (состояние записано в <see cref="KingdomPolitics.CivilWar"/>, событие опубликовано).
	/// </summary>
	public static bool TryStart(KingdomPolitics state, KingdomInput input, SimParams p, PolEventBus bus, int year, Random rng, CivilWarContext? ctx = null)
	{
		if (!CanStart(state, input, p, year))
		{
			return false;
		}
		if (rng == null)
		{
			rng = new Random(1);
		}
		if (rng.NextDouble() >= (double)StartChance(state, input, p))
		{
			return false;
		}
		CivilWarCause cause = ChooseCause(state, input, p, year, rng, out float _);
		if (TribalCivilization.NeedsCivilization(state.Ideology) && year >= (p?.TribalDeclineYear ?? 200))
		{
			cause = CivilWarCause.IdeologicalSplit;
		}
		List<CityInput> rebelCities = PickRebelCities(state, input, p, cause, ctx);
		if (rebelCities.Count == 0)
		{
			return false;
		}
		IdeologyType rebelIdeology = PickRebelIdeology(state, input, cause, ctx, rebelCities);
		if (TribalCivilization.NeedsCivilization(rebelIdeology)
			&& (TribalCivilization.NeedsCivilization(state.Ideology) || year >= (p?.TribalDeclineYear ?? 200)))
		{
			rebelIdeology = state.Ideology == IdeologyType.Monarchy ? IdeologyType.Democracy : IdeologyType.Monarchy;
		}
		int capital = rebelCities[0].CityId;
		string leaderName = ctx?.RebelLeaderName?.Invoke(state.KingdomId, capital) ?? string.Empty;

		float rebelPop = 0f;
		float totalPop = 0f;
		if (input.Cities != null)
		{
			for (int i = 0; i < input.Cities.Count; i++)
			{
				totalPop += Math.Max(0, input.Cities[i].Population);
			}
			for (int j = 0; j < rebelCities.Count; j++)
			{
				rebelPop += Math.Max(0, rebelCities[j].Population);
			}
		}
		float rebelPopShare = totalPop > 0f ? rebelPop / totalPop : 0.5f;

		// Имя вождя мятежа — только настоящее имя юнита из игры (в ядре без игры — пусто).
		CivilWarState cw = new CivilWarState
		{
			Cause = cause,
			StartedYear = year,
			RebelIdeology = rebelIdeology,
			RebelLeaderName = leaderName ?? string.Empty,
			RebelLeaderIsFemale = ctx?.RebelLeaderIsFemale?.Invoke(state.KingdomId, capital) ?? false,
			RebelLeaderActorId = ctx?.RebelLeaderActorId?.Invoke(state.KingdomId, capital) ?? 0,
			RebelCapitalCityId = capital,
			ParentCapitalCityId = input.Cities != null && input.Cities.Count > 0 ? input.Cities[0].CityId : 0
		};
		for (int k = 0; k < rebelCities.Count; k++)
		{
			cw.AddRebelCity(rebelCities[k].CityId);
		}
		state.CivilWar = cw;
		state.Legitimacy = Clamp(state.Legitimacy - p.CivilWarLegitimacyPenalty, 0f, 100f);
		state.AvgDissent = Clamp(state.AvgDissent + 0.05f, 0f, 1f);
		state.Mechanics.ThreatLevel = Math.Min(100f, state.Mechanics.ThreatLevel + 10f);

		bus?.Publish(new PolEvent(PolEventType.CivilWarStarted, "RP_EV_CIVILWAR_STARTED")
		{
			Year = year,
			Severity = Severity.Critical,
			KingdomId = state.KingdomId,
			CityId = capital
		}.With("kingdom", input.Name ?? string.Empty)
			.With("cause", CivilWarCauses.LocKey(cause))
			.With("ideology", Ideologies.NameKey(rebelIdeology))
			.With("cities", cw.RebelCityIds.Count)
			.With("share", (int)Math.Round(rebelPopShare * 100f))
			.With("leader", cw.RebelLeaderName));
		return true;
	}

	/// <summary>Выбор причины мятежа: взвешенный случайный выбор из применимых причин.</summary>
	public static CivilWarCause ChooseCause(KingdomPolitics state, KingdomInput input, SimParams p, int year, Random rng, out float totalWeight)
	{
		List<CivilWarCause> causes = new List<CivilWarCause>();
		List<float> weights = new List<float>();
		foreach (KeyValuePair<CivilWarCause, float> item in CauseWeights(state, input, p, year))
		{
			if (item.Value > 0f)
			{
				causes.Add(item.Key);
				weights.Add(item.Value);
			}
		}
		totalWeight = 0f;
		for (int i = 0; i < weights.Count; i++)
		{
			totalWeight += weights[i];
		}
		if (causes.Count == 0)
		{
			return CivilWarCause.Tyranny;
		}
		if (causes.Count == 1)
		{
			return causes[0];
		}
		float roll = (float)((rng ?? new Random(1)).NextDouble() * (double)totalWeight);
		float acc = 0f;
		for (int j = 0; j < causes.Count; j++)
		{
			acc += weights[j];
			if (roll <= acc)
			{
				return causes[j];
			}
		}
		return causes[causes.Count - 1];
	}

	/// <summary>Веса применимых причин (для тестов и диагностики — без случайности).</summary>
	public static Dictionary<CivilWarCause, float> CauseWeights(KingdomPolitics state, KingdomInput input, SimParams p, int year)
	{
		Dictionary<CivilWarCause, float> map = new Dictionary<CivilWarCause, float>();
		// Идейный раскол: заметная часть подданных исповедует иной строй.
		float dissentShare = IdeologyThreats.DissentShare(state);
		if (dissentShare >= 0.25f && IdeologyPowers.MajorityIdeology(state) != state.Ideology)
		{
			map[CivilWarCause.IdeologicalSplit] = 1f + dissentShare * 3f;
		}
		else if (dissentShare >= 0.35f)
		{
			map[CivilWarCause.IdeologicalSplit] = 0.6f + dissentShare * 2f;
		}
		bool dynastic = state.Ideology == IdeologyType.Monarchy || state.Ideology == IdeologyType.Aristocracy;
		bool freshSuccession = state.Ruler == null || (state.Ruler.SinceYear > 0 && state.Ruler.SinceYear >= state.FoundedYear && state.Ruler.SinceYear + 1 >= LastKnownYear(state));
		if (dynastic && (state.Ruler == null || state.Legitimacy < 45f))
		{
			map[CivilWarCause.SuccessionCrisis] = freshSuccession ? 2.2f : 1.1f;
		}
		if (state.Regency != null && state.Regency.IsActive)
		{
			map[CivilWarCause.RegencyCrisis] = 3f;
		}
		int foreign = CountForeignCities(state, input);
		if (state.Ideology == IdeologyType.Ethnonationalism && foreign > 0)
		{
			map[CivilWarCause.EthnicOppression] = 2.5f + foreign * 0.5f;
		}
		if (state.Legitimacy < p.CivilWarLegitimacyLimit && state.AvgDissent > 0.3f)
		{
			map[CivilWarCause.Tyranny] = 1.5f + state.AvgDissent * 3f;
		}
		if (state.Ideology == IdeologyType.Democracy && state.Election != null && state.Election.LastElectionYear > 0 && year - state.Election.LastElectionYear <= 1)
		{
			map[CivilWarCause.ElectionDispute] = 1.2f;
		}
		int cityCount = input.Cities?.Count ?? 0;
		if (cityCount >= p.CivilWarOverexpansionCities)
		{
			map[CivilWarCause.Overexpansion] = Math.Min(3f, 0.8f + (cityCount - p.CivilWarOverexpansionCities) * 0.4f);
		}
		if (state.Starving)
		{
			map[CivilWarCause.Famine] = 2f;
		}
		// Депрессия, дефолт казны или крах торговли выводят подданных на улицы:
		// обычный спад они ещё терпят.
		if (state.Economy != null && state.Economy.InCrisis && EconomicCrises.CanSparkRebellion(state.Economy.Crisis))
		{
			float severity = EconomicCrises.Severity(state.Economy.Crisis);
			map[CivilWarCause.EconomicCrisis] = 1.2f * severity * (1f + (50f - state.Economy.Prosperity) / 60f);
		}
		return map;
	}

	/// <summary>
	/// Годовой шаг идущей гражданской войны: ядро смотрит, как идёт НАСТОЯЩАЯ война в мире,
	/// и возвращает исход, когда она решена. Пока государство повстанцев ещё не создано
	/// в мире (RebelKingdomId == 0), война не тикает.
	/// </summary>
	public static CivilWarOutcome? Tick(KingdomPolitics state, KingdomInput input, SimParams p, PolEventBus bus, int year, Random rng, CivilWarContext? ctx = null)
	{
		CivilWarState cw = state?.CivilWar;
		if (cw == null || cw.RebelKingdomId == 0)
		{
			return null;
		}
		// Война изматывает метрополию, пока идёт.
		state.Legitimacy = Clamp(state.Legitimacy - p.CivilWarLegitimacyDrainPerYear, 0f, 100f);
		state.AvgDissent = Clamp(state.AvgDissent + 0.015f, 0f, 1f);

		CivilWarResolution resolution = DecideResolution(state, input, cw, p, year, ctx);
		if (resolution == CivilWarResolution.None)
		{
			return null;
		}
		return new CivilWarOutcome
		{
			Resolution = resolution,
			RebelIdeology = cw.RebelIdeology,
			Cause = cw.Cause,
			RebelKingdomId = cw.RebelKingdomId,
			RulerDeposed = resolution == CivilWarResolution.RebelTakeover,
			YearsActive = cw.YearsActive(year)
		};
	}

	/// <summary>
	/// Исход по состоянию мира:
	/// у повстанцев не осталось городов — подавление; столица метрополии у повстанцев — переворот;
	/// метрополия и повстанцы больше не воюют — отделение состоялось;
	/// война тянется дольше срока — стороны истощены, мир (отделение) навязывается.
	/// </summary>
	public static CivilWarResolution DecideResolution(KingdomPolitics state, KingdomInput input, CivilWarState cw, SimParams p, int year, CivilWarContext? ctx)
	{
		int rebelCities = ctx?.KingdomCityCount?.Invoke(cw.RebelKingdomId) ?? -1;
		if (rebelCities == 0)
		{
			return CivilWarResolution.LoyalistVictory;
		}
		if (cw.ParentCapitalCityId != 0 && ctx?.CityOwner != null)
		{
			int owner = ctx.CityOwner(cw.ParentCapitalCityId);
			if (owner == cw.RebelKingdomId)
			{
				return CivilWarResolution.RebelTakeover;
			}
		}
		bool atWar = false;
		bool known = false;
		if (input.Relations != null)
		{
			for (int i = 0; i < input.Relations.Count; i++)
			{
				if (input.Relations[i].OtherId == cw.RebelKingdomId)
				{
					known = true;
					atWar = input.Relations[i].Relation == RelationType.War;
					break;
				}
			}
		}
		if (known && atWar)
		{
			cw.WarEstablished = true;
		}
		// Война в игре ещё не началась: это не мир. Исполнитель повторит объявление войны.
		if (!cw.WarEstablished)
		{
			return CivilWarResolution.None;
		}
		// Война была и закончилась (стороны больше не воюют) — белый мир: город становится державой.
		if (known && !atWar)
		{
			return CivilWarResolution.Secession;
		}
		int limit = p.CivilWarDurationYears + p.CivilWarFrozenExtraYears;
		if (cw.YearsActive(year) > limit)
		{
			return CivilWarResolution.Secession;
		}
		return CivilWarResolution.None;
	}

	/// <summary>Краткая сводка для UI: «Мятеж (голодный бунт) — повстанцы 42%, 3-й год».</summary>
	public static string Describe(KingdomPolitics state, int year)
	{
		CivilWarState cw = state?.CivilWar;
		if (cw == null)
		{
			return string.Empty;
		}
		return CivilWarCauses.LocKey(cw.Cause) + "|" + cw.RebelKingdomId + "|" + cw.YearsActive(year) + "|" + cw.RebelCityIds.Count;
	}

	// ---------------------------------------------------------------- внутренние

	private static List<CityInput> PickRebelCities(KingdomPolitics state, KingdomInput input, SimParams p, CivilWarCause cause, CivilWarContext? ctx)
	{
		List<CityInput> result = new List<CityInput>();
		if (input.Cities == null || input.Cities.Count < 2)
		{
			return result;
		}
		// Столица (первый город королевства) в мятеже не участвует: ей есть что терять.
		List<CityInput> pool = new List<CityInput>();
		for (int i = 1; i < input.Cities.Count; i++)
		{
			pool.Add(input.Cities[i]);
		}
		pool.Sort((a, b) =>
		{
			float sa = CityRebelScore(state, a, cause, ctx);
			float sb = CityRebelScore(state, b, cause, ctx);
			int cmp = sb.CompareTo(sa);
			return cmp != 0 ? cmp : a.CityId.CompareTo(b.CityId);
		});
		int wanted = (int)Math.Round(pool.Count * p.CivilWarRebelCityShare);
		if (wanted < 1)
		{
			wanted = 1;
		}
		if (wanted > pool.Count)
		{
			wanted = pool.Count;
		}
		for (int j = 0; j < wanted; j++)
		{
			result.Add(pool[j]);
		}
		return result;
	}

	private static float CityRebelScore(KingdomPolitics state, CityInput city, CivilWarCause cause, CivilWarContext? ctx)
	{
		float score = Unrest(ctx, city);
		if (IsForeignCity(state, city))
		{
			score += cause == CivilWarCause.EthnicOppression ? 1.5f : 0.3f;
		}
		if (cause == CivilWarCause.Overexpansion)
		{
			score += 0.2f;
		}
		if (cause == CivilWarCause.Famine && !city.FoodOk)
		{
			score += 0.8f;
		}
		return score;
	}

	private static IdeologyType PickRebelIdeology(KingdomPolitics state, KingdomInput input, CivilWarCause cause, CivilWarContext? ctx, List<CityInput> rebelCities)
	{
		if (cause == CivilWarCause.IdeologicalSplit)
		{
			IdeologyType majority = IdeologyPowers.MajorityIdeology(state);
			if (majority != state.Ideology)
			{
				return majority;
			}
			// Большинство за власть — знамя берём у самой крупной оппозиционной группы.
			IdeologyType best = state.Ideology;
			int bestCount = 0;
			foreach (KeyValuePair<IdeologyType, int> pair in state.Census)
			{
				if (pair.Key != state.Ideology && pair.Value > bestCount)
				{
					bestCount = pair.Value;
					best = pair.Key;
				}
			}
			if (best != state.Ideology)
			{
				return best;
			}
		}
		if (cause == CivilWarCause.EthnicOppression || cause == CivilWarCause.Overexpansion || cause == CivilWarCause.IdeologicalSplit)
		{
			// Знамя мятежа — взгляды большинства жителей мятежных городов.
			Dictionary<IdeologyType, int> tally = new Dictionary<IdeologyType, int>();
			IdeologyType best = state.Ideology;
			int bestCount = 0;
			if (ctx?.CityIdeology != null)
			{
				for (int i = 0; i < rebelCities.Count; i++)
				{
					IdeologyType cityIdeology = ctx.CityIdeology(rebelCities[i].CityId);
					tally.TryGetValue(cityIdeology, out int count);
					tally[cityIdeology] = count + 1;
					if (tally[cityIdeology] > bestCount)
					{
						bestCount = tally[cityIdeology];
						best = cityIdeology;
					}
				}
			}
			if (bestCount > 0 && best != state.Ideology)
			{
				return best;
			}
		}
		if (cause != CivilWarCause.SuccessionCrisis && cause != CivilWarCause.RegencyCrisis)
		{
			if (ctx?.CityIdeology != null)
			{
				Dictionary<IdeologyType, int> oppTally = new Dictionary<IdeologyType, int>();
				IdeologyType bestCityOpp = state.Ideology;
				int bestCityCount = 0;
				for (int i = 0; i < rebelCities.Count; i++)
				{
					IdeologyType cityIdeology = ctx.CityIdeology(rebelCities[i].CityId);
					if (cityIdeology != state.Ideology)
					{
						oppTally.TryGetValue(cityIdeology, out int count);
						oppTally[cityIdeology] = count + 1;
						if (oppTally[cityIdeology] > bestCityCount)
						{
							bestCityCount = oppTally[cityIdeology];
							bestCityOpp = cityIdeology;
						}
					}
				}
				if (bestCityCount > 0 && bestCityOpp != state.Ideology)
				{
					return bestCityOpp;
				}
			}
			IdeologyType bestOpp = state.Ideology;
			int bestOppCount = 0;
			foreach (KeyValuePair<IdeologyType, int> pair in state.Census)
			{
				if (pair.Key != state.Ideology && pair.Value > bestOppCount)
				{
					bestOppCount = pair.Value;
					bestOpp = pair.Key;
				}
			}
			if (bestOpp != state.Ideology)
			{
				return bestOpp;
			}
			if (TribalCivilization.NeedsCivilization(state.Ideology))
			{
				return IdeologyType.Monarchy;
			}
			if (state.Ideology == IdeologyType.Monarchy)
			{
				return IdeologyType.Aristocracy;
			}
			return state.Ideology == IdeologyType.Democracy ? IdeologyType.Monarchy : IdeologyType.Democracy;
		}
		// Династический спор, кризис регентства: строй тот же, спор о власти.
		return state.Ideology;
	}

	private static float Unrest(CivilWarContext? ctx, CityInput city)
	{
		float byLoyalty = city.Loyalty > 0 ? 1f - city.Loyalty / 100f : 0.5f;
		float byDissent = ctx?.CityUnrest?.Invoke(city.CityId) ?? 0f;
		return byLoyalty > byDissent ? byLoyalty : byDissent;
	}

	private static int CountForeignCities(KingdomPolitics state, KingdomInput input)
	{
		if (input.Cities == null)
		{
			return 0;
		}
		int count = 0;
		for (int i = 0; i < input.Cities.Count; i++)
		{
			if (IsForeignCity(state, input.Cities[i]))
			{
				count++;
			}
		}
		return count;
	}

	private static bool IsForeignCity(KingdomPolitics state, CityInput city)
	{
		if (city.IsForeign)
		{
			return true;
		}
		if (!string.IsNullOrEmpty(state.TitularRaceId))
		{
			if (!string.IsNullOrEmpty(city.RaceId) && city.RaceId != state.TitularRaceId)
			{
				return true;
			}
			if (!string.IsNullOrEmpty(city.CultureName) && city.CultureName != state.TitularRaceId)
			{
				return true;
			}
		}
		return false;
	}

	/// <summary>Последний год, о котором знает состояние (для оценки «свежести» престолонаследия).</summary>
	private static int LastKnownYear(KingdomPolitics state)
	{
		int year = state.FoundedYear;
		if (state.Mechanics.LastUsedYear > year && state.Mechanics.LastUsedYear < 100000)
		{
			year = state.Mechanics.LastUsedYear;
		}
		if (state.Mechanics.LastBreakYear > year && state.Mechanics.LastBreakYear < 100000)
		{
			year = state.Mechanics.LastBreakYear;
		}
		if (state.DynastyStartYear > year)
		{
			year = state.DynastyStartYear;
		}
		return year;
	}

	internal static float Clamp(float v, float min, float max)
	{
		if (v < min)
		{
			return min;
		}
		return v > max ? max : v;
	}
}
