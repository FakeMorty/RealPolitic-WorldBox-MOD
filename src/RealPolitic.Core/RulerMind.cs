using System;
using System.Collections.Generic;

namespace RealPolitic.Core;

/// <summary>
/// Оценка сил перед войной (0.39, просьба владельца «сделать правителей умнее»).
/// Правитель больше не бросается воевать «на глазок»: он складывает свои силы,
/// силы защитника и силы его союзников и решает, есть ли шанс.
/// Только арифметика — проверяется тестами без игры.
/// </summary>
public static class WarChances
{
	/// <summary>Ниже этого отношения сил нападать бессмысленно (своих меньше в 1,6 раза).</summary>
	public const float HopelessRatio = 0.62f;

	/// <summary>Минимальный перевес для обычного завоевания.</summary>
	public const float ConquestRatio = 1.15f;

	/// <summary>Минимальный перевес для войны «по праву» (месть, возврат земель, освобождение).</summary>
	public const float JustifiedRatio = 0.95f;

	/// <summary>Какую долю сил союзника учитываем: союзник может и не прийти.</summary>
	public const float AllyWeight = 0.6f;

	/// <summary>Сила державы: воины плюс небольшой вклад городов и достатка, минус усталость от войны.</summary>
	public static float Strength(int warriors, int cities, float prosperity, float warFatigue)
	{
		float baseStrength = Math.Max(1f, warriors);
		// Каждый город — это резерв и снабжение: 6% силы за город, но не больше 40%.
		float cityBonus = Math.Min(0.4f, Math.Max(0, cities) * 0.06f);
		// Достаток выше 50% помогает снабжению, ниже 50% — мешает.
		float wealthBonus = Math.Max(-0.25f, Math.Min(0.25f, (prosperity - 50f) / 200f));
		// Усталость от войны бьёт по боеспособности: до -40%.
		float fatiguePenalty = Math.Min(0.4f, Math.Max(0f, warFatigue) / 250f);
		return baseStrength * (1f + cityBonus + wealthBonus - fatiguePenalty);
	}

	/// <summary>
	/// Решение о войне. Возвращает оценку с причиной словами — её видно в журнале
	/// и в объяснении ИИ, поэтому игроку понятно, почему правитель не напал.
	/// </summary>
	/// <param name="attackerStrength">Сила нападающего.</param>
	/// <param name="defenderStrength">Сила защитника.</param>
	/// <param name="allyStrength">Суммарная сила союзников защитника (не учитываем целиком).</param>
	/// <param name="defensive">Война оборонительная (оборона союзника, независимость) — её не отменяем.</param>
	public static WarEstimate Estimate(float attackerStrength, float defenderStrength, float allyStrength, bool defensive)
	{
		float defence = Math.Max(1f, defenderStrength) + Math.Max(0f, allyStrength) * AllyWeight;
		float ratio = Math.Max(0f, attackerStrength) / defence;
		WarEstimate estimate = new WarEstimate
		{
			AttackerStrength = (int)Math.Round(Math.Max(0f, attackerStrength)),
			DefenderStrength = (int)Math.Round(defence),
			Ratio = ratio
		};
		if (defensive)
		{
			// От обороны не отговариваем: защищаться приходится и без перевеса.
			estimate.ShouldAttack = true;
			estimate.Reason = "война оборонительная";
			return estimate;
		}
		if (ratio < HopelessRatio)
		{
			estimate.ShouldAttack = false;
			estimate.Reason = "сил слишком мало";
			return estimate;
		}
		if (ratio < JustifiedRatio)
		{
			estimate.ShouldAttack = false;
			estimate.Reason = "перевеса нет";
			return estimate;
		}
		if (ratio < ConquestRatio)
		{
			// Перевеса хватает только на «правую» войну: завоевание отменяется.
			estimate.ShouldAttack = true;
			estimate.RequiresJustCause = true;
			estimate.Reason = "перевес небольшой — только по праву";
			return estimate;
		}
		estimate.ShouldAttack = true;
		estimate.Reason = "перевес есть";
		return estimate;
	}

	/// <summary>
	/// Итог с поправкой на повод: обычное завоевание при малом перевесе не начинается,
	/// а месть или возврат земель — начинается.
	/// </summary>
	public static WarEstimate Estimate(int attackerWarriors, int attackerCities, float attackerProsperity,
		float attackerFatigue, int defenderWarriors, int defenderCities, float defenderProsperity,
		float defenderFatigue, float allyWarriors, CasusBelliType casusBelli)
	{
		float mine = Strength(attackerWarriors, attackerCities, attackerProsperity, attackerFatigue);
		float his = Strength(defenderWarriors, defenderCities, defenderProsperity, defenderFatigue);
		bool defensive = casusBelli == CasusBelliType.Defense || casusBelli == CasusBelliType.Independence;
		WarEstimate estimate = Estimate(mine, his, allyWarriors, defensive);
		if (estimate.ShouldAttack && estimate.RequiresJustCause && casusBelli == CasusBelliType.Conquest)
		{
			estimate.ShouldAttack = false;
			estimate.Reason = "на завоевание перевеса мало";
		}
		return estimate;
	}
}

/// <summary>Что решил правитель, посчитав силы.</summary>
public sealed class WarEstimate
{
	public bool ShouldAttack { get; set; }

	/// <summary>Своя сила (округлённо, в воинах).</summary>
	public int AttackerStrength { get; set; }

	/// <summary>Сила защитника вместе с той долей союзников, что мы учитываем.</summary>
	public int DefenderStrength { get; set; }

	/// <summary>Отношение сил: больше 1 — перевес на нашей стороне.</summary>
	public float Ratio { get; set; }

	/// <summary>Перевеса хватает только на войну с настоящим поводом.</summary>
	public bool RequiresJustCause { get; set; }

	/// <summary>Причина решения словами — для журнала и объяснения.</summary>
	public string Reason { get; set; } = string.Empty;

	/// <summary>Строка для журнала: «силы 120 против 90 (1,3) — перевес есть».</summary>
	public string Describe()
	{
		return "силы " + AttackerStrength + " против " + DefenderStrength
			+ " (" + Math.Round(Ratio, 2).ToString("0.00", System.Globalization.CultureInfo.InvariantCulture) + ") — " + Reason;
	}
}

/// <summary>
/// Правитель учится на поражениях (0.39). После проигранной войны держава
/// 10 лет не начинает новых: восстанавливает хозяйство и войско.
/// Победа снимает этот запрет — победа пьянит, и это тоже по-человечески.
/// </summary>
public static class KingdomLearning
{
	/// <summary>Сколько лет держава не нападает после поражения.</summary>
	public const int AversionYears = 10;

	/// <summary>Записывает поражение: с этого года держава осторожна.</summary>
	public static void RecordDefeat(KingdomPolitics state, int year)
	{
		if (state == null)
		{
			return;
		}
		state.LastDefeatYear = year;
		state.Defeats = state.Defeats + 1;
		int until = year + AversionYears;
		if (until > state.WarAversionUntilYear)
		{
			state.WarAversionUntilYear = until;
		}
	}

	/// <summary>Записывает победу: держава снова готова воевать.</summary>
	public static void RecordVictory(KingdomPolitics state, int year)
	{
		if (state == null)
		{
			return;
		}
		state.LastVictoryYear = year;
		state.WarAversionUntilYear = 0;
	}

	/// <summary>Держится ли ещё осторожность после поражения.</summary>
	public static bool IsWarAverse(KingdomPolitics state, int year)
	{
		return state != null && year < state.WarAversionUntilYear;
	}

	/// <summary>Учит ли держава ещё хоть чему-то: 0 — не боится, 1 — присматривается, 2 — зареклась воевать.</summary>
	public static int CautionLevel(KingdomPolitics state, int year)
	{
		if (state == null)
		{
			return 0;
		}
		if (year < state.WarAversionUntilYear)
		{
			return 2;
		}
		if (state.Defeats > 0 && year - state.LastDefeatYear <= AversionYears * 2)
		{
			return 1;
		}
		return 0;
	}
}

/// <summary>
/// Память жителей об обещаниях (0.39). Нарушенные договоры и обход конституции
/// не забываются на следующий год: они копятся и потом бьют по выборам,
/// по поддержке и по росту петиций. Затухание — примерно за 20 лет.
/// </summary>
public static class PromiseMemory
{
	/// <summary>Сколько «очков вины» даёт каждое нарушение.</summary>
	public const float BrokenTruce = 30f;
	public const float BrokenTreaty = 20f;
	public const float UnconstitutionalWar = 22f;
	public const float IgnoredUltimatum = 25f;
	public const float ExecutedRuler = 15f;

	/// <summary>Сколько вины прощается за год (без учёта новых нарушений).</summary>
	public const float ForgetPerYear = 3f;

	/// <summary>Выше этой вины жители уже не верят правителю.</summary>
	public const float MaxScore = 100f;

	/// <summary>Записывает нарушение и возвращает новое значение вины.</summary>
	public static float Record(KingdomPolitics state, float points, int year)
	{
		if (state == null || points <= 0f)
		{
			return state == null ? 0f : state.PromiseScore;
		}
		state.PromiseScore = Math.Min(MaxScore, state.PromiseScore + points);
		state.LastPromiseBrokenYear = year;
		return state.PromiseScore;
	}

	/// <summary>
	/// Годовое затухание. Вызывается раз в год из игрового слоя — так результат
	/// не зависит от того, сколько раз за год державу опрашивали.
	/// </summary>
	public static void YearlyDecay(KingdomPolitics state)
	{
		if (state == null || state.PromiseScore <= 0f)
		{
			return;
		}
		state.PromiseScore = Math.Max(0f, state.PromiseScore - ForgetPerYear);
	}

	/// <summary>Насколько вина бьёт по легитимности за год (0..4).</summary>
	public static float YearlyLegitimacyHit(KingdomPolitics state)
	{
		float score = state?.PromiseScore ?? 0f;
		return Math.Min(4f, score / 25f);
	}

	/// <summary>Насколько вина бьёт по поддержке за год (0..6).</summary>
	public static float YearlySupportHit(KingdomPolitics state)
	{
		float score = state?.PromiseScore ?? 0f;
		return Math.Min(6f, score / 18f);
	}

	/// <summary>Addition к шансу выборов против действующей власти (0..0,25).</summary>
	public static float ElectionPenalty(KingdomPolitics state)
	{
		float score = state?.PromiseScore ?? 0f;
		return Math.Min(0.25f, score / 400f);
	}

	/// <summary>Насколько быстрее растёт общественное движение (петиция → ультиматум).</summary>
	public static float MovementGrowthBonus(KingdomPolitics state)
	{
		float score = state?.PromiseScore ?? 0f;
		return Math.Min(0.06f, score / 1500f);
	}

	/// <summary>Словами: насколько испорчена репутация — для окна и журнала.</summary>
	public static string Describe(KingdomPolitics state)
	{
		float score = state?.PromiseScore ?? 0f;
		if (score < 5f)
		{
			return "обещания держит";
		}
		if (score < 30f)
		{
			return "одно нарушение помнят";
		}
		if (score < 60f)
		{
			return "нарушения помнят";
		}
		return "правителю больше не верят";
	}
}
