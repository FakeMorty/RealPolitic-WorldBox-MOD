using System;
using System.Collections.Generic;

namespace RealPolitic.Core;

/// <summary>
/// 0.52: законы о народах. Владелец 30.10.2026:
/// «в России есть азиаты... они не стали европеоидами, говорят на русском и своём,
/// у них свои традиции. Наверное потому, что мы их насильно не ассимилировали».
///
/// Отсюда три статьи конституции, и все три — про язык и обычай, а не про внешность.
/// Никакого превращения курицы в лиса в моде нет и не будет.
/// </summary>
public enum RaceLaw
{
	/// <summary>Все равны: у коренных и пришлых одинаковые права.</summary>
	Equal = 0,

	/// <summary>Коренным больше: пришлым жить можно, но прав меньше.</summary>
	Titular = 1,

	/// <summary>Пришлым только жить и работать: язык и суд отняты, растворение быстрое.</summary>
	Segregated = 2
}

/// <summary>Что с языком народов.</summary>
public enum LanguageLaw
{
	/// <summary>Свой язык в школах и в делах общины — народ не растворяется никогда.</summary>
	Free = 0,

	/// <summary>Только общий язык: дети учат язык державы, язык бабушек забывается.</summary>
	Common = 1
}

/// <summary>Что с судом народов.</summary>
public enum CourtLaw
{
	/// <summary>Свой суд у народов: спор внутри общины решают свои.</summary>
	Own = 0,

	/// <summary>Общий суд: все судятся у державы.</summary>
	Common = 1
}

/// <summary>
/// 0.52: местная конституция города.
///
/// Владелец 30.10.2026: «если в государстве с более чем одним городом есть город,
/// который на больше чем 50 % населения другой расой, то там есть ещё и своя
/// конституция, которая может противоречить общей».
///
/// Что это значит по-простому: город, где больше половины жителей — пришлый народ,
/// пишет себе свой устав. Он не отделяется и не бунтует сразу — он просто живёт
/// по своим правилам и просит столицу их не отнимать.
///
/// Столица при этом решает две вещи (обе — статьи конституции державы):
///   • разрешить народностям иметь свой устав вообще или запретить;
///   • терпеть противоречия с общей конституцией или не терпеть.
/// </summary>
public sealed class LocalConstitution
{
	/// <summary>Город, у которого свой устав.</summary>
	public int CityId { get; set; }

	/// <summary>Держава, в которой он живёт (для быстрого поиска).</summary>
	public int KingdomId { get; set; }

	/// <summary>Народ большинства (больше половины жителей города).</summary>
	public string RaceId { get; set; } = string.Empty;

	public string RaceName { get; set; } = string.Empty;

	/// <summary>Год, когда устав написали.</summary>
	public int FoundedYear { get; set; }

	/// <summary>Столица запретила устав (тогда он в подвешенном состоянии и злоба растёт).</summary>
	public bool Banned { get; set; }

	/// <summary>Год запрета.</summary>
	public int BannedYear { get; set; }

	/// <summary>Народ просит свой язык (устав против общей статьи о языке).</summary>
	public bool WantsLanguage { get; set; }

	/// <summary>Народ просит свой суд.</summary>
	public bool WantsCourt { get; set; }

	/// <summary>Насколько город обижен на столицу, 0..100.</summary>
	public float Grievance { get; set; }

	/// <summary>В уставе есть хоть одно расхождение с общей конституцией.</summary>
	public bool Contradicts => WantsLanguage || WantsCourt;

	/// <summary>Живой ли устав: есть город и он не запрещён — или запрещён, но ещё спорит.</summary>
	public bool Active => !Banned;
}

/// <summary>
/// Правила местных конституций. Только арифметика — проверяется тестами.
/// </summary>
public static class LocalConstitutionRules
{
	/// <summary>Доля народа, с которой город считается «их» городом.</summary>
	public const float MajorityShare = 0.5f;

	/// <summary>С какого года жизни народ просит свой язык, если общий закон ему мешает.</summary>
	public const int RequestAfterYears = 20;

	/// <summary>Скорость роста обиды, пока устав запрещён или противоречие не терпят.</summary>
	public const float GrievanceGrowth = 2.5f;

	/// <summary>Скорость, с которой обида остывает, когда столица уступает.</summary>
	public const float GrievanceCooling = 1.5f;

	/// <summary>Порог, после которого город громко жалуется (уведомление и летопись).</summary>
	public const float ComplaintThreshold = 50f;

	/// <summary>Порог, после которого это уже угроза мятежа.</summary>
	public const float RevoltThreshold = 80f;

	/// <summary>
	/// Может ли у города быть свой устав.
	/// Владелец: нужна держава больше чем из одного города и город, где больше
	/// половины — другой народ. Плюс столица должна это разрешать.
	/// </summary>
	public static bool CanExist(bool capitalAllows, int kingdomCities, float raceShare)
	{
		return capitalAllows && kingdomCities > 1 && raceShare > MajorityShare;
	}

	/// <summary>
	/// Спорит ли устав с общей конституцией: народ просит язык или суд,
	/// а общий закон ему этого не даёт.
	/// </summary>
	public static bool Contradicts(LanguageLaw language, CourtLaw court)
	{
		return language != LanguageLaw.Free || court != CourtLaw.Own;
	}

	/// <summary>
	/// Один год обиды города на столицу.
	/// tolerated — столица терпит противоречия; banned — устав вовсе запрещён.
	/// </summary>
	public static float NextGrievance(float grievance, bool contradicts, bool tolerated, bool banned)
	{
		if (banned || (contradicts && !tolerated))
		{
			// Запретили или не терпят — обида растёт.
			float extra = banned ? 1.5f : 0f;
			return Clamp(grievance + GrievanceGrowth + extra);
		}
		// Всё улажено или столица терпит — обида остывает.
		return Clamp(grievance - GrievanceCooling);
	}

	/// <summary>Требует ли город внимания столицы прямо сейчас.</summary>
	public static bool Complains(float grievance)
	{
		return grievance >= ComplaintThreshold;
	}

	/// <summary>Пора ли считать это угрозой мятежа.</summary>
	public static bool ThreatensRevolt(float grievance)
	{
		return grievance >= RevoltThreshold;
	}

	/// <summary>Короткая подпись состояния устава для окна и летописи.</summary>
	public static string StatusKey(LocalConstitution local, bool tolerated)
	{
		if (local == null)
		{
			return "RP52_LOCAL_NONE";
		}
		if (local.Banned)
		{
			return "RP52_LOCAL_BANNED";
		}
		if (local.Contradicts && !tolerated)
		{
			return "RP52_LOCAL_DISPUTED";
		}
		if (local.Contradicts)
		{
			return "RP52_LOCAL_TOLERATED";
		}
		return "RP52_LOCAL_AGREED";
	}

	private static float Clamp(float value)
	{
		if (value < 0f)
		{
			return 0f;
		}
		return value > 100f ? 100f : value;
	}
}
