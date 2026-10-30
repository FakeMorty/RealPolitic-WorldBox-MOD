using System;

namespace RealPolitic.Core;

/// <summary>Черта характера правителя (этап 19): задаёт стиль державы в стратегии, экономике и годовых дрейфах.</summary>
public enum RulerTrait
{
	None = 0,

	/// <summary>Дипломат: союзы и браки легче, войны реже.</summary>
	Diplomat = 1,

	/// <summary>Завоеватель: тянет державу в войны и мобилизации.</summary>
	Conqueror = 2,

	/// <summary>Скопец: расходы двора урезаны, но подданные ворчат.</summary>
	Miser = 3,

	/// <summary>Транжира: двор дороже стоит, зато города довольны.</summary>
	Spendthrift = 4,

	/// <summary>Книжник: легитимность растёт от просвещённого правления.</summary>
	Scholar = 5,

	/// <summary>Благочестивый: власть освящена, подданные послушнее.</summary>
	Pious = 6,

	/// <summary>Жестокий: недовольство давится страхом, но поддержка тает.</summary>
	Cruel = 7,

	/// <summary>Справедливый: поддержка и легитимность растут, войны непопулярны.</summary>
	Just = 8
}

/// <summary>Модификаторы черт: единая точка баланса, чтобы эффекты не расползались по коду.</summary>
public static class RulerTraits
{
	public static string LocKey(RulerTrait trait)
	{
		return trait switch
		{
			RulerTrait.Diplomat => "RP_TRAIT_DIPLOMAT",
			RulerTrait.Conqueror => "RP_TRAIT_CONQUEROR",
			RulerTrait.Miser => "RP_TRAIT_MISER",
			RulerTrait.Spendthrift => "RP_TRAIT_SPENDTHRIFT",
			RulerTrait.Scholar => "RP_TRAIT_SCHOLAR",
			RulerTrait.Pious => "RP_TRAIT_PIOUS",
			RulerTrait.Cruel => "RP_TRAIT_CRUEL",
			RulerTrait.Just => "RP_TRAIT_JUST",
			_ => "RP_TRAIT_NONE"
		};
	}

	/// <summary>Множитель государственных расходов (содержание армии и городов).</summary>
	public static float ExpenseMultiplier(RulerTrait trait)
	{
		return trait switch
		{
			RulerTrait.Miser => 0.9f,
			RulerTrait.Spendthrift => 1.12f,
			_ => 1f
		};
	}

	/// <summary>Прибавка кScore войны/мобилизации в стратегическом планировщике.</summary>
	public static float WarBias(RulerTrait trait)
	{
		return trait switch
		{
			RulerTrait.Conqueror => 18f,
			RulerTrait.Cruel => 8f,
			RulerTrait.Pious => 4f,
			RulerTrait.Diplomat => -6f,
			RulerTrait.Just => -12f,
			RulerTrait.Scholar => -6f,
			_ => 0f
		};
	}

	/// <summary>Прибавка к мирной дипломатии (союзы) в планировщике.</summary>
	public static float DiplomacyBias(RulerTrait trait)
	{
		return trait switch
		{
			RulerTrait.Diplomat => 16f,
			RulerTrait.Just => 6f,
			RulerTrait.Miser => 4f,
			RulerTrait.Conqueror => -6f,
			RulerTrait.Cruel => -8f,
			_ => 0f
		};
	}

	/// <summary>Прибавка к шансу династического брака (в сотых долях шанса).</summary>
	public static float MarriageBias(RulerTrait trait)
	{
		return trait switch
		{
			RulerTrait.Diplomat => 15f,
			RulerTrait.Conqueror => 6f,
			RulerTrait.Miser => 4f,
			RulerTrait.Just => 4f,
			RulerTrait.Cruel => -8f,
			_ => 0f
		};
	}

	/// <summary>Годовой дрейф легитимности от характера.</summary>
	public static float YearlyLegitimacy(RulerTrait trait)
	{
		return trait switch
		{
			RulerTrait.Just => 1.5f,
			RulerTrait.Pious => 1f,
			RulerTrait.Scholar => 1f,
			RulerTrait.Cruel => -1f,
			RulerTrait.Spendthrift => -0.5f,
			_ => 0f
		};
	}

	/// <summary>Годовой дрейф поддержки подданных от характера.</summary>
	public static float YearlySupport(RulerTrait trait)
	{
		return trait switch
		{
			RulerTrait.Just => 1f,
			RulerTrait.Spendthrift => 1f,
			RulerTrait.Cruel => -1.5f,
			RulerTrait.Miser => -0.5f,
			_ => 0f
		};
	}

	/// <summary>Годовой дрейф недовольства (страх давит, справедливость гасит).</summary>
	public static float YearlyDissent(RulerTrait trait)
	{
		return trait switch
		{
			RulerTrait.Cruel => -0.02f,
			RulerTrait.Just => -0.01f,
			RulerTrait.Pious => -0.005f,
			_ => 0f
		};
	}
}

/// <summary>Личность правителя: черта характера и амбиции (этап 19). Назначается при воцарении и переживает сохранение.</summary>
public sealed class RulerPersonality
{
	public RulerTrait Trait { get; set; } = RulerTrait.None;

	/// <summary>Амбиции 0..100: разгоняют войны и великие начинания, гасят осторожную дипломатию.</summary>
	public float Ambition { get; set; } = 50f;

	/// <summary>Случайная личность для нового правителя (детерминирована данным rng).</summary>
	public static RulerPersonality Roll(Random rng)
	{
		// 0.23.13: раньше «1 + rng.Next(0,8)» давал 1..8 — черта Just (8) никогда не выпадала.
		RulerTrait trait = _rollable[rng.Next(_rollable.Length)];
		float ambition = (float)Math.Round(rng.NextDouble() * 100f);
		return new RulerPersonality { Trait = trait, Ambition = ambition };
	}

	private static readonly RulerTrait[] _rollable =
	{
		RulerTrait.Diplomat, RulerTrait.Conqueror, RulerTrait.Miser, RulerTrait.Spendthrift,
		RulerTrait.Scholar, RulerTrait.Pious, RulerTrait.Cruel, RulerTrait.Just
	};

	/// <summary>Вклад амбиций в воинственность планировщика (−10..+10).</summary>
	public float AmbitionWarBias => (Ambition - 50f) * 0.2f;

	/// <summary>Вклад амбиций в осторожную дипломатию: честолюбцы менее сговорчивы (−5..+5).</summary>
	public float AmbitionDiplomacyBias => (50f - Ambition) * 0.1f;
}

/// <summary>Узы династического брака между двумя державами (этап 19).</summary>
public sealed class MarriageBond
{
	/// <summary>Государство-супруг.</summary>
	public int PartnerId { get; set; }

	/// <summary>Год заключения брака.</summary>
	public int Year { get; set; }
}
