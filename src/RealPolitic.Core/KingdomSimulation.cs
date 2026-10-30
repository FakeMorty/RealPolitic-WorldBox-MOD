using System;
using System.Collections.Generic;

namespace RealPolitic.Core;

public static class KingdomSimulation
{
	public static CivilWarOutcome? TickState(KingdomPolitics state, KingdomInput input, SimParams p, PolEventBus bus, int year, Func<int, float> neighborMissionary, Func<int, IdeologyType> neighborIdeology, Random rng, DeathRegistry? deaths = null, CivilWarContext? ctx = null)
	{
		if (state == null)
		{
			throw new ArgumentNullException("state");
		}
		if (input.Relations == null)
		{
			input.Relations = new List<RelationInfo>();
		}
		state.InWar = input.AtWar;
		state.Starving = input.Starving;
		// 0.23.11: регент (корона у регента) — не влезает в «правителя» ядра. Держим
		// законного правителя (наследника/прежнего короля), пока корона не вернётся:
		// иначе регент-узурпатор объявлялся бы правителем и новой династией
		// (трагедия дома Вевович: Аледы сменил дом Вевович на Мазекзтка).
		if (!(input.Ruler != null && input.Ruler.IsRegent)
			&& !(input.Ruler == null && state.Ruler != null && state.Ruler.SinceYear == year))
		{
			state.Ruler = input.Ruler;
		}
		TickRegency(state, bus, year);

		// Механика демократических выборов: 15-летний избирательный цикл (в вольной территории — бессрочно до смерти или снятия атамана жителями)
		TickPeriodicElections(state, bus, year, rng);
		TickFreeTerritoryAtaman(state, input, bus, year, rng);

		// Механика этнонационализма: нетерпимость к инородцам и сплочение титульного этноса
		TickEthnonationalism(state, input, bus, year);

		// 0.39: вина за нарушенные обещания тает на 3 очка в год и бьёт по поддержке.
		PromiseMemory.YearlyDecay(state);
		float supportHit = PromiseMemory.YearlySupportHit(state);
		if (supportHit > 0f)
		{
			state.Support = Clamp(state.Support - supportHit, 0f, 100f);
		}
		UpdateLegitimacy(state, p);
		// Революция (0.23): без «давления» — только настоящие подданные, их недовольство,
		// падение легитимности, голод и война. Цель — строй большинства жителей.
		TickRevolution(state, p, bus, year, rng, deaths);
		TryReform(state, p, bus, year);

		// Модовые механики идеологии: сначала копим угрозу, затем правитель решает,
		// применять ли ульту (в т. ч. чтобы сбить напряжение).
		IdeologyThreats.Tick(state, input, year, bus);
		IdeologyPowers.Tick(state, input, year, bus, rng);

		// Исход гражданской войны (если война в этом году завершилась) применяет вызывающий:
		// отделение создаёт новое государство, а это уже операция над всем миром.
		return TickCivilWar(state, input, p, bus, year, rng, ctx);
	}

	/// <summary>
	/// Гражданская война государства: начало мятежа, годовой шаг и развязка.
	/// Развязку применяет <see cref="PoliticalWorld"/>, потому что отделение создаёт новое государство.
	/// </summary>
	public static CivilWarOutcome? TickCivilWar(KingdomPolitics state, KingdomInput input, SimParams p, PolEventBus bus, int year, Random rng, CivilWarContext? ctx = null)
	{
		if (state == null)
		{
			return null;
		}
		if (state.CivilWar == null)
		{
			CivilWars.TryStart(state, input, p, bus, year, rng, ctx);
			return null;
		}
		return CivilWars.Tick(state, input, p, bus, year, rng, ctx);
	}

	private static void TickRegency(KingdomPolitics state, PolEventBus bus, int year)
	{
		// 0.23.11: когда корона у регента (CrownSitsWithRegent), срок регентства решает
		// только фиддер — вернёт корону наследнику в его 18 лет. По возрасту сидящего
		// на троне здесь НЕ завершаем: взрослый регент сразу «списал» бы регентство,
		// и временный опекун стал бы вечным узурпатором (трагедия дома Вевович).
		bool crownWithRegent = state.Regency != null && state.Regency.IsActive && state.Regency.CrownSitsWithRegent;
		if (crownWithRegent)
		{
			// Эффекты регентства держатся, пока корона не вернулась наследнику.
			if (state.Ideology == IdeologyType.Monarchy || state.Ideology == IdeologyType.Aristocracy)
			{
				state.Mechanics.ThreatLevel = Math.Min(100f, state.Mechanics.ThreatLevel + 3f);
			}
			state.AvgDissent = Math.Min(1f, state.AvgDissent + 0.02f);
			return;
		}
		if (state.Ruler == null)
		{
			return;
		}
		if (state.Regency != null && state.Regency.IsActive)
		{
			if (state.Ruler.Age >= 18)
			{
				state.Regency.End();
				state.Legitimacy = Math.Min(100f, state.Legitimacy * 1.33f);
				bus?.Publish(new PolEvent(PolEventType.RegencyEnded, "RP_EV_REGENCY_END")
				{
					Year = year,
					Severity = Severity.Major,
					KingdomId = state.KingdomId,
					ActorId = state.Ruler.ActorId
				}.With("ruler", state.Ruler.Name));
			}
			else
			{
				// При регентстве знать стремится к власти (рост аристократической угрозы и недовольства):
				if (state.Ideology == IdeologyType.Monarchy || state.Ideology == IdeologyType.Aristocracy)
				{
					state.Mechanics.ThreatLevel = Math.Min(100f, state.Mechanics.ThreatLevel + 3f);
				}
				state.AvgDissent = Math.Min(1f, state.AvgDissent + 0.02f);
			}
		}
		else if (state.Ruler.Age < 18 && state.Ruler.Age > 0)
		{
			if (state.Regency == null)
			{
				state.Regency = new RegencyState();
			}
			state.Regency.Start(year, RegencyState.CouncilKey, 0);
			state.Legitimacy = Math.Max(5f, state.Legitimacy * 0.75f);
			bus?.Publish(new PolEvent(PolEventType.RegencyStarted, "RP_EV_REGENCY_START")
			{
				Year = year,
				Severity = Severity.Major,
				KingdomId = state.KingdomId,
				ActorId = state.Ruler.ActorId
			}.With("ruler", state.Ruler.Name).With("age", state.Ruler.Age));
		}
	}

	private static void TickPeriodicElections(KingdomPolitics state, PolEventBus bus, int year, Random rng)
	{
		if (state.Ideology == IdeologyType.FreeTerritory)
		{
			if (state.Election == null)
			{
				state.Election = new ElectionState { NextElectionYear = int.MaxValue, TermsServed = 1 };
				if (state.Ruler != null)
				{
					state.Election.RecordPresidentTerm(state.Ruler.ActorId, state.Ruler.Name, 1);
				}
			}
			else if (state.Ruler != null && state.Election.FindPastPresident(state.Ruler.ActorId) == null)
			{
				state.Election.RecordPresidentTerm(state.Ruler.ActorId, state.Ruler.Name, 1);
			}
			return;
		}
        if(state.Constitution?.Electoral!=null)
        {ElectionSystem.HoldElection(state,year,bus,rng);return;}
		if (!SuccessionLaws.HasPeriodicCycle(state.SuccessionLaw) && !SuccessionLaws.HasPeriodicCycle(state.Ideology))
		{
			return;
		}
		// Этап 26: конституция без избирательного главы запрещает регулярные выборы.
		if (state.Constitution != null && !ConstitutionGates.HoldsElections(state.Constitution.StateForm))
		{
			return;
		}
		if (state.Election == null)
		{
			state.Election = new ElectionState { NextElectionYear = year + ElectionSystem.TermYears, TermsServed = 1 };
			if (state.Ruler != null)
			{
				state.Election.RecordPresidentTerm(state.Ruler.ActorId, state.Ruler.Name, 1);
			}
		}
		else if (year >= state.Election.NextElectionYear)
		{
			ElectionSystem.HoldElection(state, year, bus, rng);
		}
	}

	/// <summary>
	/// 0.31: В вольной территории (FreeTerritory) атаман правит бессрочно (без 15-летнего цикла и без лимита сроков) —
	/// до самой смерти или пока сами жители вольной территории не снимут его за потерю доверия и не изберут нового атамана.
	/// </summary>
	public static bool TickFreeTerritoryAtaman(KingdomPolitics state, KingdomInput input, PolEventBus? bus, int year, Random? rng)
	{
		if (state == null || state.Ideology != IdeologyType.FreeTerritory)
		{
			return false;
		}
		if (state.Ruler == null)
		{
			return ElectionSystem.ElectAtaman(state, year, bus, rng);
		}
		if (year - state.LastImpeachmentYear < 3)
		{
			return false;
		}
		bool residentsDissatisfied = state.Support < 40f || state.AvgDissent >= 0.50f || state.Legitimacy < 35f;
		if (!residentsDissatisfied)
		{
			return false;
		}
		string oldAtamanName = state.Ruler.Name ?? string.Empty;
		int oldAtamanId = state.Ruler.ActorId;
		bool oldIsFemale = state.Ruler.IsFemale;
		state.LastImpeachmentYear = year;
		state.Ruler = null;

		bus?.Publish(new PolEvent(PolEventType.Impeached, "RP_EV_ATAMAN_DISMISSED")
		{
			Year = year,
			Severity = Severity.Major,
			KingdomId = state.KingdomId,
			ActorId = oldAtamanId
		}.With("kingdom", input.Name ?? string.Empty)
		 .With("ruler", oldAtamanName)
		 .With("title", Ideologies.RulerTitleKey(IdeologyType.FreeTerritory, oldIsFemale)));

		ElectionSystem.ElectAtaman(state, year, bus, rng, oldAtamanId, oldAtamanName);
		return true;
	}

	private static void TickEthnonationalism(KingdomPolitics state, KingdomInput input, PolEventBus bus, int year)
	{
		if (state.Ideology != IdeologyType.Ethnonationalism)
		{
			return;
		}
		if (string.IsNullOrEmpty(state.TitularRaceId) && input.Cities != null && input.Cities.Count > 0)
		{
			state.TitularRaceId = !string.IsNullOrEmpty(input.Cities[0].RaceId) ? input.Cities[0].RaceId : input.Cities[0].CultureName;
		}
		if (input.Cities == null || input.Cities.Count == 0)
		{
			return;
		}
		int foreignCities = 0;
		int titularCities = 0;
		for (int c = 0; c < input.Cities.Count; c++)
		{
			CityInput city = input.Cities[c];
			bool foreign = city.IsForeign
				|| (!string.IsNullOrEmpty(state.TitularRaceId) && !string.IsNullOrEmpty(city.RaceId) && city.RaceId != state.TitularRaceId)
				|| (!string.IsNullOrEmpty(state.TitularRaceId) && !string.IsNullOrEmpty(city.CultureName) && city.CultureName != state.TitularRaceId);
			if (foreign)
			{
				foreignCities++;
			}
			else
			{
				titularCities++;
			}
		}
		if (foreignCities > 0)
		{
			state.Mechanics.ThreatLevel = Math.Min(100f, state.Mechanics.ThreatLevel + foreignCities * 3.5f);
			state.AvgDissent = Math.Min(1f, state.AvgDissent + foreignCities * 0.025f);
			state.Support = Math.Max(0f, state.Support - foreignCities * 1.5f);
			if (state.AvgDissent > 0.45f)
			{
				bus?.Publish(new PolEvent(PolEventType.EthnonationalistTension, "RP_EV_ETHNIC_TENSION")
				{
					Year = year,
					Severity = Severity.Major,
					KingdomId = state.KingdomId
				}.With("kingdom", input.Name ?? string.Empty));
			}
		}
		if (titularCities > 0)
		{
			// Сплочение титульного этноса даёт бонус к лояльности и легитимности:
			state.Support = Math.Min(100f, state.Support + titularCities * 0.6f);
			state.Legitimacy = Math.Min(100f, state.Legitimacy + titularCities * 0.4f);
		}
	}

	public static void UpdateLegitimacy(KingdomPolitics state, SimParams p)
	{
		float num = state.Def.Legitimacy;
		if (state.InWar)
		{
			num -= p.WarLegitimacyDrain;
		}
		if (state.Starving)
		{
			num -= p.StarveLegitimacyDrain;
		}
		num -= state.AvgDissent * p.DissentLegitimacyDrain;
		// 0.39: нарушенные обещания тянут легитимность вниз, пока жители их помнят.
		num -= PromiseMemory.YearlyLegitimacyHit(state);
		num += (state.Support - 50f) * 0.2f;
		if (state.Union != null)
		{
			// Уния влияет на легитимность: федерация/полная уния укрепляют, вассалитет ослабляет.
			UnionMember? member = state.Union.GetMember(state.KingdomId);
			float approval = (member != null) ? member.Approval : 0.5f;
			num += Unions.LoyaltyModifier(state.Union.Type, approval) * 20f;
		}

		// Регентство даёт штраф -25% к целевой легитимности:
		if (state.Regency != null && state.Regency.IsActive)
		{
			num *= 0.75f;
		}

		num = Clamp(num, 0f, 100f);
		float value = num - state.Legitimacy;
		float num2 = p.LegitimacyRecovery / 10f;
		if (Math.Abs(value) < num2)
		{
			state.Legitimacy = num;
		}
		else
		{
			state.Legitimacy += (float)Math.Sign(value) * num2;
		}
		state.Legitimacy = Clamp(state.Legitimacy, 0f, 100f);
	}

	/// <summary>
	/// Условия вспышки революции: подданные недовольны, власть нелегитимна, и есть
	/// «сила» — большинство (или крупная оппозиция) хочет другого строя. Голод и война
	/// усиливают; сплочённость строя — гасит.
	/// </summary>
	public static float RevolutionChance(KingdomPolitics state, SimParams p, out IdeologyType target)
	{
		target = IdeologyPowers.MajorityIdeology(state);
		if (target == state.Ideology)
		{
			// Большинство за власть — знамя берёт крупнейшая оппозиционная группа,
			// но только если она заметна (≥ 25% подданных).
			IdeologyType opposition = state.LargestOpposition(out int count);
			int total = 0;
			foreach (KeyValuePair<IdeologyType, int> pair in state.Census)
			{
				total += pair.Value;
			}
			if (opposition == state.Ideology || total <= 0 || count * 4 < total)
			{
				return 0f;
			}
			target = opposition;
		}
		if (state.Legitimacy >= p.RevolutionLegitimacyLimit || state.AvgDissent < p.RevolutionDissentMin)
		{
			return 0f;
		}
		float chance = p.RevolutionSparkChance;
		chance *= 1f + (p.RevolutionLegitimacyLimit - state.Legitimacy) / p.RevolutionLegitimacyLimit; // ×1..2
		chance *= 0.5f + state.AvgDissent;                                                             // ×0.85..1.5
		if (state.Starving)
		{
			chance *= 1.5f;
		}
		if (state.InWar)
		{
			chance *= 1.3f;
		}
		chance *= 1.3f - state.Def.Cohesion * 0.5f;
		return Clamp(chance, 0f, 0.6f);
	}

	public static void TickRevolution(KingdomPolitics state, SimParams p, PolEventBus bus, int year, Random rng, DeathRegistry? deaths)
	{
		if (state.Revolution == null)
		{
			if (state.CivilWar != null || state.IsYoungState(year, p?.YoungStateGraceYears ?? 50))
			{
				return; // гражданская война уже идёт или государство моложе 50 лет — революцию не открываем
			}
			float sparkChance = RevolutionChance(state, p, out IdeologyType target);
			if (year >= (p?.TribalDeclineYear ?? 200)
				&& target != state.Ideology
				&& TribalCivilization.NeedsCivilization(target))
			{
				// После 200-го года революция не возвращает державу к племени или каганату.
				return;
			}
			if (sparkChance <= 0f || rng == null || rng.NextDouble() >= sparkChance)
			{
				return;
			}
			state.Revolution = new RevolutionState
			{
				Target = target,
				TargetStance = ClampStance(state.Stance),
				StartedYear = year,
				YearsLeft = p.RevolutionDuration
			};
			bus?.Publish(new PolEvent(PolEventType.RevolutionStarted, "RP_EV_REVOLUTION_STARTED")
			{
				Year = year,
				Severity = Severity.Critical,
				KingdomId = state.KingdomId
			}.With("ideology", Ideologies.NameKey(target)).With("legitimacy", state.Legitimacy).With("dissent", (int)Math.Round(state.AvgDissent * 100f)));
			return;
		}
		RevolutionState revolution = state.Revolution;
		revolution.YearsLeft--;
		if (revolution.YearsLeft > 0)
		{
			state.AvgDissent = Clamp(state.AvgDissent + 0.05f * p.RevolutionDissentMultiplier, 0f, 1f);
			state.Legitimacy = Clamp(state.Legitimacy - 1.5f, 0f, 100f);
			return;
		}
		// Решающий год: исход зависит от того, сколько подданных за новый строй,
		// насколько власть нелегитимна и насколько люди озлоблены.
		float supporters = 0f;
		int totalActors = 0;
		foreach (KeyValuePair<IdeologyType, int> pair in state.Census)
		{
			totalActors += pair.Value;
			if (pair.Key == revolution.Target)
			{
				supporters = pair.Value;
			}
		}
		float share = totalActors > 0 ? supporters / totalActors : 0f;
		float revolutionBaseChance = p.RevolutionBaseChance;
		revolutionBaseChance += (share - 0.5f) * 0.6f;
		revolutionBaseChance -= state.Legitimacy / 300f;
		revolutionBaseChance -= state.Def.Cohesion * 0.2f;
		revolutionBaseChance += state.AvgDissent * 0.25f;
		revolutionBaseChance = Clamp(revolutionBaseChance, 0.05f, 0.9f);
		if (rng.NextDouble() < (double)revolutionBaseChance)
		{
			RulerRef ruler = state.Ruler;
			IdeologyType ideology = state.Ideology;
			state.Ideology = revolution.Target;
			state.Stance = revolution.TargetStance;
			state.SuccessionLaw = SuccessionLaws.GetDefaultLaw(state.Ideology);
			if (SuccessionLaws.HasPeriodicCycle(state.Ideology))
			{
				state.Election = ElectionSystem.ReformedElectoralState(state.Election, year);
				if (ruler != null)
				{
					state.Election.RecordPresidentTerm(ruler.ActorId, ruler.Name, 1);
				}
			}
			state.Legitimacy = Clamp((float)state.Def.Legitimacy * 0.6f, 5f, 100f);
			state.AvgDissent = Clamp(state.AvgDissent - 0.25f, 0f, 1f);
			PolEvent polEvent = new PolEvent(PolEventType.RevolutionSucceeded, "RP_EV_REVOLUTION_SUCCEEDED")
			{
				Year = year,
				Severity = Severity.Critical,
				KingdomId = state.KingdomId
			}.With("old", Ideologies.NameKey(ideology)).With("new", Ideologies.NameKey(state.Ideology)).With("stance", Ideologies.StanceKey(state.Stance));
			if (ruler != null)
			{
				DeathRecord death = RecordRulerDeath(deaths, bus, state, ruler, year, DeathCause.Executed, "RP_DEATH_NOTE_REVOLUTION");
				polEvent.Death = death;
				state.Ruler = null;
			}
			bus.Publish(polEvent);
		}
		else
		{
			PolEvent evt = new PolEvent(PolEventType.RevolutionFailed, "RP_EV_REVOLUTION_FAILED")
			{
				Year = year,
				Severity = Severity.Major,
				KingdomId = state.KingdomId
			}.With("ideology", Ideologies.NameKey(revolution.Target));
			state.Legitimacy = Clamp(state.Legitimacy + 6f, 0f, 100f);
			state.AvgDissent = Clamp(state.AvgDissent - 0.1f, 0f, 1f);
			bus.Publish(evt);
		}
		state.Revolution = null;
	}

	/// <summary>
	/// Курс реформ: строй держит позицию в допустимом для идеологии диапазоне; при высоком
	/// недовольстве правительство смещает курс к середине диапазона (уступка).
	/// </summary>
	public static void TryReform(KingdomPolitics state, SimParams p, PolEventBus bus, int year)
	{
		if (state.Revolution != null)
		{
			return;
		}
		int stance = (int)state.Stance;
		IdeologyDef def = state.Def;
		int target = stance;
		if (stance < def.MinReform)
		{
			target = stance + 1;
		}
		else if (stance > def.MaxReform)
		{
			target = stance - 1;
		}
		else if (state.AvgDissent > 0.45f && state.Support < 40f && (year % 5) == 0)
		{
			int mid = def.MinReform + (def.MaxReform - def.MinReform) / 2;
			target = stance < mid ? stance + 1 : (stance > mid ? stance - 1 : stance);
		}
		target = Math.Max(0, Math.Min(4, target));
		if (target == stance)
		{
			return;
		}
		ReformStance old = state.Stance;
		state.Stance = (ReformStance)target;
		bus?.Publish(new PolEvent(PolEventType.KingdomReformed, "RP_EV_REFORMED")
		{
			Year = year,
			Severity = Severity.Major,
			KingdomId = state.KingdomId
		}.With("old", Ideologies.StanceKey(old)).With("new", Ideologies.StanceKey(state.Stance)));
	}

	public static void UpdateCensusDerived(KingdomPolitics state, float totalDissent, int totalActors)
	{
		int population = state.Population;
		if (population > 0)
		{
			state.Support = 100f * (float)state.CensusOf(state.Ideology) / (float)population;
		}
		else
		{
			state.Support = 0f;
		}
		state.AvgDissent = ((totalActors > 0) ? Clamp(totalDissent / (float)totalActors, 0f, 1f) : 0f);
	}

	internal static DeathRecord? RecordRulerDeath(DeathRegistry? deaths, PolEventBus bus, KingdomPolitics state, RulerRef ruler, int year, DeathCause cause, string note)
	{
		if (deaths == null)
		{
			return null;
		}
		int actorId = ruler.ActorId;
		// Дедупликация: смерть уже записана (например, игровым хуком Actor.die) —
		// не публикуем второе уведомление о том же событии.
		DeathRecord? existing = deaths.Get(actorId);
		if (existing != null)
		{
			return existing;
		}
		string name = ruler.Name;
		int? kingdomId = state.KingdomId;
		int? reignYears = (ruler.SinceYear > 0 && ruler.SinceYear <= year) ? (year - ruler.SinceYear + 1) : null;
		DeathRecord deathRecord = deaths.Record(actorId, name, cause, year, null, "", "", null, kingdomId, "", wasRuler: true, wasChosen: false, 0, note, wasFemale: ruler.IsFemale, reignYears);
		bus.Publish(new PolEvent(PolEventType.RulerDeath, "RP_EV_RULER_DEATH")
		{
			Year = year,
			Severity = Severity.Critical,
			KingdomId = state.KingdomId,
			ActorId = ruler.ActorId,
			Death = deathRecord
		}.With("ruler", ruler.Name)
			.With("title", Ideologies.RulerTitleKey(state.Ideology, ruler.IsFemale))
			.With("cause", deathRecord.LocKeyFor(ruler.IsFemale)));
		return deathRecord;
	}

	internal static ReformStance ClampStance(ReformStance stance)
	{
		return (ReformStance)Math.Max(0, Math.Min(4, (int)stance));
	}

	internal static float Clamp(float v, float min, float max)
	{
		if (!(v < min))
		{
			if (!(v > max))
			{
				return v;
			}
			return max;
		}
		return min;
	}
}
