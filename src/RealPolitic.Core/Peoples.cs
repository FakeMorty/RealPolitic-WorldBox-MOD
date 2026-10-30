using System;
using System.Collections.Generic;

namespace RealPolitic.Core;

/// <summary>
/// Насколько народ ещё держится за свой язык.
///
/// Владелец 30.10.2026: «пусть ассимиляция будет культурной... в России есть азиаты,
/// ещё с похода Ермака, но они не стали европеоидами: говорят на русском и своём,
/// у них свои традиции. Наверное потому, что мы их насильно не ассимилировали».
///
/// Поэтому ступеней всего три, и внешность в них не участвует вообще:
///   Own    — говорит только на своём, обычаи свои;
///   Shared — два языка: дома свой, в делах общий;
///   Common — своего языка не знает, обычаи забыты (по крови — тот же народ).
/// </summary>
public enum LanguageStage
{
	Own = 0,
	Shared = 1,
	Common = 2
}

/// <summary>Один народ в одной державе: когда пришёл и на каком языке говорит.</summary>
public sealed class PeopleRecord
{
	public string RaceId { get; set; } = string.Empty;

	public string RaceName { get; set; } = string.Empty;

	/// <summary>Год, с которого мод видит этот народ в этой державе.</summary>
	public int FirstSeenYear { get; set; }

	/// <summary>Ступень языка.</summary>
	public LanguageStage Stage { get; set; } = LanguageStage.Own;

	/// <summary>Год, когда ступень сменилась.</summary>
	public int StageSinceYear { get; set; }

	/// <summary>Сколько душ в этом году (обновляется каждый год).</summary>
	public int Population { get; set; }

	/// <summary>Доля от населения державы (0..1).</summary>
	public float Share { get; set; }

	/// <summary>Народ — коренной для этой державы.</summary>
	public bool Titular { get; set; }

	/// <summary>С какого года у народа просят свой устав (0 — не просят).</summary>
	public int RequestedYear { get; set; }
}

/// <summary>
/// 0.52: культурная ассимиляция. Народ не меняется — меняется язык и обычай.
///
/// Правило владельца, из которого всё выведено:
///   • дали право на язык и свой суд  → две культуры рядом живут ВЕЧНО;
///   • не дали, но и не притесняют     → язык тихо угасает через 200 лет;
///   • притесняют                     → вдвое быстрее, зато злоба и бунт.
/// </summary>
public static class AssimilationEngine
{
	/// <summary>Сколько лет нужно, чтобы пришлый народ заговорил на двух языках.</summary>
	public const int YearsToShared = 50;

	/// <summary>Сколько лет нужно, чтобы свой язык забылся совсем.</summary>
	public const int YearsToCommon = 200;

	/// <summary>Сколько лет при притеснении (вдвое быстрее).</summary>
	public const int YearsToCommonOppressed = 100;

	/// <summary>С такой доли народ перенимает общий язык быстрее: вокруг сплошь чужие.</summary>
	public const float SmallMinorityShare = 0.15f;

	/// <summary>С такой доли народ свой язык не теряет: он тут не гость, а хозяин.</summary>
	public const float StrongShare = 0.45f;

	/// <summary>
	/// Следующая ступень языка. Возвращает текущую, если меняться нечему.
	///
	/// languageFree — есть ли закон о своём языке; courtOwn — есть ли свой суд;
	/// oppressed — притесняют ли народ;
	/// yearsInStage — сколько лет народ на нынешней ступени.
	/// </summary>
	public static LanguageStage NextStage(LanguageStage stage, float share, bool languageFree, bool courtOwn, bool oppressed, int yearsInStage)
	{
		if (stage == LanguageStage.Common)
		{
			return LanguageStage.Common; // забытый язык не возвращается сам
		}
		// Свой язык и свой суд — народ живёт двумя культурами и дальше не идёт.
		if (languageFree && courtOwn && stage == LanguageStage.Shared)
		{
			return LanguageStage.Shared;
		}
		// Крупный народ, живущий не гостем, свой язык не теряет.
		if (share >= StrongShare && !oppressed)
		{
			return stage;
		}
		int speed = 1;
		if (oppressed)
		{
			speed = 2;
		}
		else if (share < SmallMinorityShare)
		{
			speed = 2; // вокруг одни чужие — язык уходит быстрее
		}
		if (stage == LanguageStage.Own)
		{
			int need = Math.Max(1, YearsToShared / speed);
			// Если язык в школе свой и суд свой — народ всё равно выучит общий язык
			// державы: иначе он не сможет ни торговать, ни служить. Но через Shared
			// он не пойдёт.
			return yearsInStage >= need ? LanguageStage.Shared : LanguageStage.Own;
		}
		int need2 = (oppressed ? YearsToCommonOppressed : YearsToCommon) / (share < SmallMinorityShare && !oppressed ? 2 : 1);
		return yearsInStage >= Math.Max(1, need2) ? LanguageStage.Common : LanguageStage.Shared;
	}

	/// <summary>Сколько лет осталось до следующей ступени (для окна и летописи).</summary>
	public static int YearsUntilNextStage(LanguageStage stage, float share, bool languageFree, bool courtOwn, bool oppressed)
	{
		if (stage == LanguageStage.Common)
		{
			return 0;
		}
		if (languageFree && courtOwn && stage == LanguageStage.Shared)
		{
			return 0; // дальше не будет никогда
		}
		if (share >= StrongShare && !oppressed)
		{
			return 0;
		}
		if (stage == LanguageStage.Own)
		{
			int speed = oppressed || share < SmallMinorityShare ? 2 : 1;
			return Math.Max(1, YearsToShared / speed);
		}
		int divisor = share < SmallMinorityShare && !oppressed ? 2 : 1;
		return Math.Max(1, (oppressed ? YearsToCommonOppressed : YearsToCommon) / divisor);
	}

	/// <summary>Слово для окна: «свой», «свой и общий», «только общий».</summary>
	public static string StageKey(LanguageStage stage)
	{
		switch (stage)
		{
			case LanguageStage.Own: return "RP52_LANG_OWN";
			case LanguageStage.Shared: return "RP52_LANG_SHARED";
			default: return "RP52_LANG_COMMON";
		}
	}

	/// <summary>
	/// 0.52: правитель из чужого народа.
	///
	/// Владелец 30.10.2026: «в России к власти может прийти эскимос. Но тоже технически
	/// можно запретить конституцией». Значит:
	///   • закон разрешает — правит спокойно, никакого штрафа;
	///   • закон запрещает — он всё равно правит (игра так решила), но держава
	///     теряет часть законности, пока он на троне.
	/// </summary>
	public static float ForeignRulerLegitimacyHit(bool foreignRuler, bool allowedByLaw, float share)
	{
		if (!foreignRuler || allowedByLaw)
		{
			return 0f;
		}
		// Чем больше в державе этого народа, тем меньше скандала: своих много.
		float own = Math.Max(0f, Math.Min(1f, share));
		return 8f * (1f - own);
	}

	/// <summary>Может ли народ просить свой устав: он тут больше половины и живёт не первый год.</summary>
	public static bool WantsOwnConstitution(float share, int yearsHere)
	{
		return share > LocalConstitutionRules.MajorityShare && yearsHere >= LocalConstitutionRules.RequestAfterYears;
	}
}
