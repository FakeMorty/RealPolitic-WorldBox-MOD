using System;

namespace RealPolitic.Core;

/// <summary>
/// Накопление уникальных угроз идеологий. Шкала 0..100: растёт от того, в чём
/// идеология уязвима, снижается от того, что её лечит. При достижении 100 —
/// срыв: публикуется событие, шкала обнуляется, государство несколько лет «приходит в себя».
///
/// Важно: угроза у каждой идеологии растёт ИМЕННО от её сильной стороны,
/// доведённой до крайности — так плюс и минус лежат на одной оси.
/// </summary>
public static class IdeologyThreats
{
	/// <summary>Годовой тик угрозы. Возвращает true, если произошёл срыв.</summary>
	public static bool Tick(KingdomPolitics state, KingdomInput input, int year, PolEventBus? bus)
	{
		IdeologyMechanicsState m = state.Mechanics;
		IdeologyThreatDef def = IdeologyMechanics.Threat(state.Ideology);
		if (m.IsRecovering(def, year))
		{
			// После срыва угроза не копится и понемногу спадает.
			m.ThreatLevel = Math.Max(0f, m.ThreatLevel - 5f);
			return false;
		}
		if (m.LastBreakYear != int.MinValue && def.Recovery > 0 && m.RecoveryAnnouncedFor != m.LastBreakYear)
		{
			// 0.23.3: у «смуты» есть конец — летопись сообщает, что кризис миновал.
			m.RecoveryAnnouncedFor = m.LastBreakYear;
			bus?.Publish(new PolEvent(PolEventType.IdeologyThreatRecovered, "RP_THREAT_RECOVERED")
			{
				Year = year,
				Severity = Severity.Minor,
				KingdomId = state.KingdomId
			}.With("kingdom", input.Name ?? string.Empty).With("threat", def.NameKey).With("years", def.Recovery.ToString()));
		}
		float delta = Growth(state, input, year);
		m.ThreatLevel = Clamp(m.ThreatLevel + delta, 0f, 100f);
		if (m.ThreatLevel < def.Threshold)
		{
			return false;
		}
		// Срыв.
		m.ThreatLevel = 0f;
		m.ThreatBreaks++;
		m.LastBreakYear = year;
		bus?.Publish(new PolEvent(PolEventType.IdeologyThreatBroke, def.NameKey + "_BREAK")
		{
			Year = year,
			Severity = Severity.Major,
			KingdomId = state.KingdomId
		}.With("kingdom", input.Name ?? string.Empty).With("threat", def.NameKey).With("ideology", Ideologies.NameKey(state.Ideology)));
		return true;
	}

	/// <summary>
	/// Прирост шкалы за год для текущей идеологии.
	/// Положительное — копится, отрицательное — рассасывается.
	/// </summary>
	public static float Growth(KingdomPolitics state, KingdomInput input, int year)
	{
		int cities = input.Cities?.Count ?? 0;
		float dissent = state.AvgDissent;
		bool war = input.AtWar;

		switch (state.Ideology)
		{
		case IdeologyType.Tribalism:
			// Раскол племени: племя не держит большие территории.
			// 1 город — спокойно, каждый следующий разгоняет распад.
			// После 200-го года народ внутри племени требует цивилизации.
			return -2f + Math.Max(0f, cities - 2) * 3.5f + dissent * 4f + (year >= 200 ? 3.5f : 0f);

		case IdeologyType.Monarchy:
			// Кризис престолонаследия: копится, пока нет наследника,
			// и тем быстрее, чем ниже легитимность.
			// Наследник в ядре не отслеживается, но риск тем выше, чем старше правитель
			// и чем ниже легитимность: престарелый монарх без опоры — прямая угроза,
			// тогда как высокая легитимность удерживает порядок вокруг престола.
			{
				float leg = Clamp(state.Legitimacy, 0f, 100f);
				if (state.Ruler == null)
				{
					return 4.5f + (60f - leg) * 0.15f;
				}
				int age = state.Ruler.Age;
				float aging = age >= 55
					? (age - 55) * 0.28f * (leg > 60f ? (1f - (leg - 60f) * 0.012f) : 1f)
					: -3.5f;
				return aging + (60f - leg) * 0.14f;
			}

		case IdeologyType.Theocracy:
			// Ересь: доля инакомыслящих жителей — прямая угроза.
			return -3f + DissentShare(state) * 22f + dissent * 5f;

		case IdeologyType.Aristocracy:
			// Заговор знати: знать скучает в долгом мире при сытом правителе.
			{
				int reign = RuleYears(state, year);
				float idle = war ? -4f : 3.5f;
				return idle + Math.Min(6f, reign * 0.25f) - (state.Support * 0.04f);
			}

		case IdeologyType.MerchantRepublic:
			// Долговой кризис: войны разоряют республику.
			return war ? 9f : -7f;

		case IdeologyType.Militarism:
			// Военная усталость: копится за каждый год войны, мир лечит медленно.
			return war ? 8f + dissent * 3f : -5f;

		case IdeologyType.Egalitarianism:
			// Паралич власти: чем пестрее состав жителей, тем труднее договориться.
			return -4f + Fragmentation(state) * 26f;

		case IdeologyType.Technocracy:
			// Утечка умов: несчастное и недовольное общество теряет лучших.
			return -3f + Math.Max(0f, 50f - state.Support) * 0.16f + dissent * 4f;

		case IdeologyType.Liberalism:
			// Расслоение: свободный рынок без поддержки народа копит обиду; война душит торговлю.
			return -3f + Math.Max(0f, 55f - state.Support) * 0.14f + dissent * 5f + (war ? 3f : -1.5f);

		case IdeologyType.Democracy:
			// Политическая поляризация: копится при расколе мнений и войне, спадает при высокой поддержке.
			return -3f + DissentShare(state) * 18f + (war ? 4.5f : -2.5f) - (state.Support * 0.04f);

		case IdeologyType.Communism:
			// Бюрократический застой: копится в долгом мире при спаде энтузиазма/поддержки.
			return -2f + (war ? -3.5f : 3.5f) + Math.Max(0f, 55f - state.Support) * 0.16f;

		case IdeologyType.Ethnonationalism:
			// Межэтнический бунт: копится от доли инородцев и войн.
			return -3f + (war ? 5f : 1.5f) + DissentShare(state) * 22f;

		case IdeologyType.Khaganate:
			// Междоусобица улусов: копится в долгом мире без походов и при слабой руке кагана.
			return -2.5f + (war ? -3f : 3.5f) + Math.Max(0f, cities - 2) * 1.5f + Math.Max(0f, 60f - state.Legitimacy) * 0.12f;

		case IdeologyType.Empire:
			// Дворцовый переворот: копится при затяжной войне, падении легитимности и росте смуты в провинциях.
			return -3f + (war ? 3.5f : 0.5f) + Math.Max(0f, 65f - state.Legitimacy) * 0.14f + dissent * 8f;

		case IdeologyType.FreeTerritory:
			// Атаманщина: копится при падении поддержки атамана и разбросе мнений между вольными общинами.
			return -3f + Fragmentation(state) * 18f + Math.Max(0f, 55f - state.Support) * 0.15f + dissent * 6f;

		default:
			return 0f;
		}
	}

	/// <summary>Доля жителей, чья идеология отличается от государственной (0..1).</summary>
	public static float DissentShare(KingdomPolitics state)
	{
		int total = state.Population;
		if (total <= 0)
		{
			return 0f;
		}
		int same = state.CensusOf(state.Ideology);
		return Clamp((float)(total - same) / total, 0f, 1f);
	}

	/// <summary>
	/// Раздробленность общества 0..1: 0 — все одной идеологии,
	/// ближе к 1 — жители равномерно размазаны по многим идеологиям.
	/// Считается как 1 − сумма квадратов долей (индекс Симпсона).
	/// </summary>
	public static float Fragmentation(KingdomPolitics state)
	{
		int total = state.Population;
		if (total <= 0)
		{
			return 0f;
		}
		float sum = 0f;
		foreach (var pair in state.Census)
		{
			float share = (float)pair.Value / total;
			sum += share * share;
		}
		return Clamp(1f - sum, 0f, 1f);
	}

	/// <summary>Сколько лет правит текущий правитель.</summary>
	private static int RuleYears(KingdomPolitics state, int year)
	{
		if (state.Ruler == null)
		{
			return 0;
		}
		int since = state.Ruler.SinceYear;
		return since <= 0 ? 0 : Math.Max(0, year - since);
	}

	private static float Clamp(float value, float min, float max)
	{
		return value < min ? min : (value > max ? max : value);
	}
}
