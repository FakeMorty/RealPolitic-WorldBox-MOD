using System;
using System.Collections.Generic;

namespace RealPolitic.Core;

/// <summary>Почему житель хочет уехать (для летописи и решения стран).</summary>
public enum MigrationReason
{
	None = 0,
	Persecution = 1,
	Poverty = 2,
	War = 3,
	Dissent = 4,
	Family = 5
}

/// <summary>Решение одной стороны по заявлению на переезд.</summary>
public enum MigrationVerdict
{
	Undecided = 0,
	Allow = 1,
	Deny = 2
}

/// <summary>Чем кончилось заявление.</summary>
public enum MigrationOutcome
{
	/// <summary>Заявление ещё рассматривается.</summary>
	Pending = 0,

	/// <summary>Обе стороны согласны — обычный переезд, никто не рискует.</summary>
	Agreed = 1,

	/// <summary>Родина не отпустила, принимающая сторона согласна — житель сбегает.</summary>
	Escaped = 2,

	/// <summary>Родина отпустила, принимающая сторона не берёт — житель остаётся.</summary>
	StayedHome = 3,

	/// <summary>Обе стороны против.</summary>
	RefusedBoth = 4,

	/// <summary>Родина не отпустила, а побег не удался — пойман на границе.</summary>
	Caught = 5
}

/// <summary>
/// Одно заявление на переселение (0.46).
///
/// Правило владельца: «Жители могут мигрировать в другие страны. Подавать заявление
/// на миграцию. Это заявление должны оформить с двух сторон. Если со стороны Родины
/// не одобрили, а с другой одобрили — челик сбегает из своей страны в другую. Как только
/// он пересекает границу, он становится гражданином страны, в которую сбежал».
/// </summary>
public sealed class MigrationRequest
{
	public long ActorId { get; set; }

	public string ActorName { get; set; } = string.Empty;

	public string RaceId { get; set; } = string.Empty;

	public int FromKingdom { get; set; }

	public int ToKingdom { get; set; }

	public int Year { get; set; }

	public MigrationReason Reason { get; set; }

	public MigrationVerdict HomeDecision { get; set; }

	public MigrationVerdict HostDecision { get; set; }

	public MigrationOutcome Outcome { get; set; }

	/// <summary>Насколько сильно житель хотел уехать (0..1) — влияет на шанс побега.</summary>
	public float Pressure { get; set; }

	/// <summary>Готовая строка для журнала и летописи.</summary>
	public string Describe()
	{
		string why = Reason switch
		{
			MigrationReason.Persecution => "притесняют по роду",
			MigrationReason.Poverty => "живёт в нужде",
			MigrationReason.War => "устал от войны",
			MigrationReason.Dissent => "не согласен со строем",
			MigrationReason.Family => "хочет к родне",
			_ => "ищет лучшей доли"
		};
		string result = Outcome switch
		{
			MigrationOutcome.Agreed => "переехал с согласия обеих сторон",
			MigrationOutcome.Escaped => "сбежал через границу и стал гражданином новой державы",
			MigrationOutcome.StayedHome => "остался дома — принимающая сторона не приняла",
			MigrationOutcome.RefusedBoth => "остался дома — отказали обе стороны",
			MigrationOutcome.Caught => "пытался сбежать, но был пойман на границе",
			_ => "заявление рассматривается"
		};
		return ActorName + " (" + why + "): " + result;
	}
}

/// <summary>
/// Правила переселения. Чистое ядро: ни одной ссылки на игру, всё проверяется тестами.
///
/// Кто и как решает:
///   • РОДИНА. Открытые границы — отпускает почти всех. Регулируемые — отпускает,
///     если житель не последний носитель своего рода и страна не теряет рабочих рук.
///     Закрытые — не отпускает никого, но житель может сбежать.
///   • ПРИНИМАЮЩАЯ СТОРОНА. Смотрит, терпит ли она этот род. Этнонационализм принимает
///     только «свой» род и отказывает чужим — именно поэтому притесняемые бегут
///     в страны с другими порядками, а не куда угодно.
///   • ПОБЕГ. Возможен только когда родина против, а принимающая сторона за.
///     Шанс тем выше, чем ближе граница (соседняя держава), чем сильнее давление
///     и чем свободнее порядки на родине. На закрытой границе с тайным сыском
///     побег чаще заканчивается поимкой.
/// </summary>
public static class MigrationRules
{
	/// <summary>Сколько лет житель не подаёт новое заявление после неудачи.</summary>
	public const int RetryYears = 3;

	/// <summary>Давление, при котором житель вообще задумывается о переезде.</summary>
	public const float WanderlustThreshold = 0.35f;

	/// <summary>Максимум заявлений на державу за год — чтобы не забивать летопись.</summary>
	public const int MaxRequestsPerKingdom = 2;

	/// <summary>
	/// Насколько житель хочет уехать (0..1). Складывается из притеснения рода,
	/// недовольства, нужды и войны. Чем больше — тем вероятнее и заявление, и побег.
	/// </summary>
	public static float Pressure(float dissent, float racePressure, float prosperity, bool atWar)
	{
		float pressure = racePressure * 0.5f;
		pressure += Clamp01(dissent) * 0.25f;
		pressure += Clamp01((50f - prosperity) / 50f) * 0.15f;
		if (atWar)
		{
			pressure += 0.15f;
		}
		return Clamp01(pressure);
	}

	/// <summary>Решение родной державы по заявлению.</summary>
	public static MigrationVerdict DecideHome(BorderLaw borders, float loyaltyOfCitizen, bool isLastOfRace, int population)
	{
		// Закрытая граница: не отпускает никого. Дальше решает только побег.
		if (borders == BorderLaw.Closed)
		{
			return MigrationVerdict.Deny;
		}
		// Последнего носителя рода держава не отпускает никогда: это её народ.
		if (isLastOfRace && population > 0)
		{
			return MigrationVerdict.Deny;
		}
		if (borders == BorderLaw.Open)
		{
			return MigrationVerdict.Allow;
		}
		// Регулируемые: держава не отпускает недовольных (их уход — признание провала),
		// а довольных отпускает спокойно.
		return loyaltyOfCitizen >= 0.5f ? MigrationVerdict.Allow : MigrationVerdict.Deny;
	}

	/// <summary>
	/// Решение принимающей державы: терпит ли она этот род.
	/// tolerance 0..1 — насколько держава вообще открыта чужим (см. RaceSurvival.Tolerance).
	/// </summary>
	public static MigrationVerdict DecideHost(float tolerance, float rngRoll01)
	{
		return rngRoll01 < tolerance ? MigrationVerdict.Allow : MigrationVerdict.Deny;
	}

	/// <summary>Чем кончилось заявление при известных решениях двух сторон.</summary>
	public static MigrationOutcome Resolve(MigrationVerdict home, MigrationVerdict host, bool escapeSucceeded)
	{
		if (home == MigrationVerdict.Allow && host == MigrationVerdict.Allow)
		{
			return MigrationOutcome.Agreed;
		}
		if (home == MigrationVerdict.Allow && host == MigrationVerdict.Deny)
		{
			return MigrationOutcome.StayedHome;
		}
		if (home == MigrationVerdict.Deny && host == MigrationVerdict.Allow)
		{
			return escapeSucceeded ? MigrationOutcome.Escaped : MigrationOutcome.Caught;
		}
		return MigrationOutcome.RefusedBoth;
	}

	/// <summary>
	/// Шанс побега 0..1. Растёт с давлением, падает с закрытостью границы и сыском.
	/// На открытой границе сбежать легко, на закрытой — почти невозможно.
	/// </summary>
	public static float EscapeChance(float pressure, BorderLaw borders, WatchLaw watch, bool neighbouring)
	{
		float chance = 0.20f + Clamp01(pressure) * 0.45f;
		if (neighbouring)
		{
			chance += 0.15f;
		}
		switch (borders)
		{
			case BorderLaw.Open:
				chance += 0.20f;
				break;
			case BorderLaw.Controlled:
				chance -= 0.10f;
				break;
			case BorderLaw.Closed:
				chance -= 0.25f;
				break;
		}
		switch (watch)
		{
			case WatchLaw.SecretPolice:
				chance -= 0.25f;
				break;
			case WatchLaw.Patrol:
				chance -= 0.08f;
				break;
			case WatchLaw.Liberty:
				chance += 0.08f;
				break;
		}
		return Clamp01(chance);
	}

	/// <summary>
	/// Куда житель хотел бы уехать: среди держав, терпимых к его роду, предпочитает
	/// соседа, высокое благосостояние и мир. Возвращает -1, если ехать некуда.
	/// </summary>
	public static int ChooseDestination(IReadOnlyList<MigrationCandidate> candidates)
	{
		int best = -1;
		float bestScore = 0f;
		for (int i = 0; i < candidates.Count; i++)
		{
			MigrationCandidate c = candidates[i];
			// В воюющую державу не бегут: там сейчас не безопаснее, а страшнее.
			if (c.KingdomId <= 0 || c.Tolerance < 0.35f || c.AtWar)
			{
				continue;
			}
			float score = c.Tolerance * 40f + Clamp01(c.Prosperity / 100f) * 20f + (c.Neighbouring ? 25f : 0f);
			if (score > bestScore)
			{
				bestScore = score;
				best = c.KingdomId;
			}
		}
		return bestScore > 15f ? best : -1;
	}

	private static float Clamp01(float value)
	{
		return value < 0f ? 0f : (value > 1f ? 1f : value);
	}
}

/// <summary>Держава-кандидат для переезда (считается игровым слоем).</summary>
public struct MigrationCandidate
{
	public int KingdomId;

	/// <summary>Насколько держава терпима к роду переселенца (0..1).</summary>
	public float Tolerance;

	public float Prosperity;

	public bool Neighbouring;

	public bool AtWar;
}
