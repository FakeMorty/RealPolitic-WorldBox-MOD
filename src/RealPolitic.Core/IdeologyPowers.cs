using System;
using System.Collections.Generic;

namespace RealPolitic.Core;

/// <summary>
/// Ульты идеологий: когда ИИ-правитель решает их применить и что при этом происходит
/// в ядре. Игровые последствия (статы бойцов, золото, мобилизация) применяет слой
/// src/Game, читая состояние через IsPowerActive.
///
/// Правитель применяет ульту, когда: перезарядка вышла, государство достаточно старое,
/// легитимность не ниже порога И сложилась ситуация, в которой ульта уместна
/// (см. Situational). Так действия выглядят осмысленно, а не срабатывают вхолостую.
/// </summary>
public static class IdeologyPowers
{
	/// <summary>Годовой тик: пробуем применить ульту. Возвращает true, если применили.</summary>
	public static bool Tick(KingdomPolitics state, KingdomInput input, int year, PolEventBus? bus, Random rng)
	{
		IdeologyMechanicsState m = state.Mechanics;
		IdeologyPowerDef def = IdeologyMechanics.Power(state.Ideology);
		// Срок действия вышел — гасим эффект и сообщаем об этом.
		if (m.ActiveUntilYear != 0 && year >= m.ActiveUntilYear)
		{
			m.ActiveUntilYear = 0;
			m.TargetKingdomId = null;
			bus?.Publish(new PolEvent(PolEventType.IdeologyPowerEnded, def.NameKey + "_END")
			{
				Year = year,
				Severity = Severity.Minor,
				KingdomId = state.KingdomId
			}.With("kingdom", input.Name ?? string.Empty)
				.With("power", def.NameKey)
				.With("ideology", Ideologies.NameKey(state.Ideology)));
		}
		if (!CanUse(state, input, def, year))
		{
			return false;
		}
		if (!Situational(state, input, year))
		{
			return false;
		}
		// Небольшая случайность, чтобы все государства не срабатывали синхронно.
		if (rng != null && rng.NextDouble() > 0.6)
		{
			return false;
		}
		Use(state, input, def, year, bus);
		return true;
	}

	/// <summary>Формальная готовность: перезарядка, возраст государства, легитимность.</summary>
	public static bool CanUse(KingdomPolitics state, KingdomInput input, IdeologyPowerDef def, int year)
	{
		IdeologyMechanicsState m = state.Mechanics;
		if (m.IsActive(year))
		{
			return false;
		}
		if (m.CooldownLeft(def, year) > 0)
		{
			return false;
		}
		if (state.FoundedYear > 0 && year - state.FoundedYear < def.MinAge)
		{
			return false;
		}
		return state.Legitimacy >= def.MinLegitimacy;
	}

	/// <summary>Уместна ли ульта прямо сейчас — своя проверка у каждой идеологии.</summary>
	public static bool Situational(KingdomPolitics state, KingdomInput input, int year)
	{
		int cities = input.Cities?.Count ?? 0;
		switch (state.Ideology)
		{
		case IdeologyType.Tribalism:
			// Сход собирают, когда племя невелико и надо прирасти числом.
			return cities <= 3 && !input.AtWar;
		case IdeologyType.Monarchy:
			// Брак заключают в мирное время, когда есть с кем.
			return !input.AtWar && HasNeighbor(input);
		case IdeologyType.Theocracy:
			// Поход объявляют, когда есть внутреннее напряжение — гнев наружу.
			return IdeologyThreats.DissentShare(state) > 0.25f || input.AtWar;
		case IdeologyType.Aristocracy:
			// Титул жалуют, когда городов больше одного и знать надо занять.
			return cities >= 2;
		case IdeologyType.MerchantRepublic:
			// Договор заключают в мире и при наличии соседа.
			return !input.AtWar && HasNeighbor(input);
		case IdeologyType.Militarism:
			// Мобилизация — только на войне.
			return input.AtWar;
		case IdeologyType.Egalitarianism:
			// Собрание созывают, когда мнение большинства разошлось с курсом государства.
			// После 200-го года цивилизация не может скатиться обратно к племени или каганату.
			{
				IdeologyType maj = MajorityIdeology(state);
				return maj != state.Ideology
					&& !(year >= 200 && TribalCivilization.NeedsCivilization(maj));
			}
		case IdeologyType.Technocracy:
			// Проект начинают в мире и при хорошей поддержке.
			return !input.AtWar && state.Support >= 40f;
		case IdeologyType.Liberalism:
			// Рынки открывают в мире, когда казна или поддержка проседают.
			return !input.AtWar && (state.Support < 55f || state.Legitimacy < 50f);
		case IdeologyType.Democracy:
			// Референдум проводят для сплочения при спаде поддержки или войне.
			return state.Support < 50f || state.Legitimacy < 50f || input.AtWar;
		case IdeologyType.Communism:
			// План запускают в мире при нехватке ресурсов или для индустриализации.
			return !input.AtWar && (input.Starving || cities >= 2);
		case IdeologyType.Ethnonationalism:
			// Национальный триумф объявляют на войне или при росте чужеродного населения.
			return input.AtWar || IdeologyThreats.DissentShare(state) > 0.15f;
		case IdeologyType.Khaganate:
			// Великий курултай собирают перед походом, на войне или при множестве улусов.
			return input.AtWar || cities >= 2 || state.Support < 55f;
		case IdeologyType.Empire:
			// Имперский манифест издают, когда нужно укрепить престол или успокоить провинции.
			return cities >= 2 || state.Legitimacy < 65f || state.AvgDissent > 0.18f;
		case IdeologyType.FreeTerritory:
			// Вольный клич бросают на войне или когда вольнице грозит внутренний разлад.
			return input.AtWar || state.AvgDissent > 0.20f || state.Support < 55f;
		default:
			return false;
		}
	}

	/// <summary>Применение ульты: ставим таймеры и публикуем событие.</summary>
	public static void Use(KingdomPolitics state, KingdomInput input, IdeologyPowerDef def, int year, PolEventBus? bus)
	{
		IdeologyMechanicsState m = state.Mechanics;
		m.LastUsedYear = year;
		m.ActiveUntilYear = year + Math.Max(1, def.Duration);
		m.TargetKingdomId = PickTarget(state, input);

		// Ульта — это всегда размен: она даёт силу, но чего-то стоит прямо сейчас.
		ApplyCoreCost(state, year, bus);

		// Ключ текста уведомления — отдельный (NameKey + "_USE"): сам NameKey это
		// голое название ульты («Династический брак»), без государства и смысла.
		bus?.Publish(new PolEvent(PolEventType.IdeologyPowerUsed, def.NameKey + "_USE")
		{
			Year = year,
			Severity = Severity.Major,
			KingdomId = state.KingdomId
		}.With("kingdom", input.Name ?? string.Empty)
			.With("power", def.NameKey)
			.With("ideology", Ideologies.NameKey(state.Ideology))
			.With("years", def.Duration));
	}

	/// <summary>
	/// Цена ульты в терминах ядра. Игровую часть (золото, воины) применяет src/Game,
	/// здесь — то, что относится к политической модели.
	/// </summary>
	private static void ApplyCoreCost(KingdomPolitics state, int year, PolEventBus? bus)
	{
		switch (state.Ideology)
		{
		case IdeologyType.Militarism:
			// Мобилизация бьёт по довольству людей.
			state.Support = Clamp(state.Support - 12f, 0f, 100f);
			break;
		case IdeologyType.Aristocracy:
			// Возвышение одного города злит остальные.
			state.Support = Clamp(state.Support - 8f, 0f, 100f);
			break;
		case IdeologyType.Liberalism:
			// Свободная торговля: народ доволен, казна оживает.
			state.Support = Clamp(state.Support + 6f, 0f, 100f);
			break;
		case IdeologyType.Technocracy:
			// Великий проект — долгие траты, легитимность просядет, если не завершить.
			state.Legitimacy = Clamp(state.Legitimacy - 5f, 0f, 100f);
			break;
		case IdeologyType.Egalitarianism:
			// Народное собрание: государство перенимает волю большинства.
			{
				IdeologyType majority = MajorityIdeology(state);
				if (majority != state.Ideology
					&& !(year >= 200 && TribalCivilization.NeedsCivilization(majority)))
				{
					IdeologyType oldIdeology = state.Ideology;
					state.Ideology = majority;
					state.SuccessionLaw = SuccessionLaws.GetDefaultLaw(majority);
					if (SuccessionLaws.HasPeriodicCycle(majority))
					{
						state.Election = ElectionSystem.ReformedElectoralState(state.Election, year);
						if (state.Ruler != null)
						{
							state.Election.RecordPresidentTerm(state.Ruler.ActorId, state.Ruler.Name, 1);
						}
					}
					state.Mechanics.Reset();
					state.Mechanics.LastUsedYear = int.MinValue;
					bus?.Publish(new PolEvent(PolEventType.KingdomAdoptedIdeology, "RP_EV_ADOPTED_IDEOLOGY")
					{
						Year = year,
						Severity = Severity.Major,
						KingdomId = state.KingdomId
					}.With("old", Ideologies.NameKey(oldIdeology)).With("new", Ideologies.NameKey(majority)));
				}
				break;
			}
		case IdeologyType.Theocracy:
			// Поход сплачивает своих: инакомыслие временно давится.
			state.AvgDissent = Math.Max(0f, state.AvgDissent - 0.1f);
			break;
		case IdeologyType.Democracy:
			// Референдум укрепляет поддержку и гасит раскол.
			state.Support = Clamp(state.Support + 12f, 0f, 100f);
			state.AvgDissent = Math.Max(0f, state.AvgDissent * 0.5f);
			break;
		case IdeologyType.Communism:
			// План повышает легитимность ценой строгой дисциплины.
			state.Support = Clamp(state.Support - 5f, 0f, 100f);
			state.Legitimacy = Clamp(state.Legitimacy + 8f, 0f, 100f);
			break;
		case IdeologyType.Ethnonationalism:
			// Триумф сплачивает титульный этнос ценой роста трений с инородцами.
			state.Legitimacy = Clamp(state.Legitimacy + 12f, 0f, 100f);
			state.AvgDissent = Math.Min(1f, state.AvgDissent + 0.05f);
			break;
		case IdeologyType.Khaganate:
			// Великий курултай сплачивает улусы вокруг кагана/хатун.
			state.Legitimacy = Clamp(state.Legitimacy + 14f, 0f, 100f);
			state.Support = Clamp(state.Support + 8f, 0f, 100f);
			state.AvgDissent = Math.Max(0f, state.AvgDissent - 0.10f);
			break;
		case IdeologyType.Empire:
			// Имперский манифест возвышает авторитет короны и гасит сепаратизм.
			state.Legitimacy = Clamp(state.Legitimacy + 16f, 0f, 100f);
			state.AvgDissent = Math.Max(0f, state.AvgDissent - 0.12f);
			break;
		case IdeologyType.FreeTerritory:
			// Вольный клич воодушевляет вольницу и сбивает усталость от борьбы.
			state.Support = Clamp(state.Support + 15f, 0f, 100f);
			state.AvgDissent = Math.Max(0f, state.AvgDissent - 0.15f);
			state.WarFatigue = Math.Max(0f, state.WarFatigue - 18f);
			break;
		}
	}

	/// <summary>Самая многочисленная идеология среди жителей государства.</summary>
	public static IdeologyType MajorityIdeology(KingdomPolitics state)
	{
		IdeologyType best = state.Ideology;
		int bestCount = -1;
		foreach (KeyValuePair<IdeologyType, int> pair in state.Census)
		{
			if (pair.Value > bestCount)
			{
				bestCount = pair.Value;
				best = pair.Key;
			}
		}
		return best;
	}

	/// <summary>Цель для направленных ульт (брак, договор, поход) — первый подходящий сосед.</summary>
	private static int? PickTarget(KingdomPolitics state, KingdomInput input)
	{
		if (input.Relations == null)
		{
			return null;
		}
		bool wantEnemy = state.Ideology == IdeologyType.Theocracy;
		for (int i = 0; i < input.Relations.Count; i++)
		{
			RelationInfo r = input.Relations[i];
			bool enemy = r.Relation == RelationType.War;
			if (wantEnemy == enemy)
			{
				return r.OtherId;
			}
		}
		return input.Relations.Count > 0 ? input.Relations[0].OtherId : null;
	}

	private static bool HasNeighbor(KingdomInput input)
	{
		return input.Relations != null && input.Relations.Count > 0;
	}

	private static float Clamp(float value, float min, float max)
	{
		return value < min ? min : (value > max ? max : value);
	}
}
