using System;

namespace RealPolitic.Core;

/// <summary>
/// 0.24: механическое действие статей VII–XII конституции. Все величины — чистые
/// множители/сдвиги от закона и обязательности грамоты (статья VI): при «обычае»
/// действие мягче (сообный «бормоток»), при обязательной грамоте — в полную силу.
/// </summary>
public static class ConstitutionEffects
{
	// ============ VII О казне и податях ============

	/// <summary>Множитель налогового сбора: лёгкие 0.75, умеренные 1, кабальные 1.35.</summary>
	public static float TaxMultiplier(TaxLaw law, bool binding)
	{
		float base_;
		switch (law)
		{
			case TaxLaw.Light: base_ = 0.75f; break;
			case TaxLaw.Crushing: base_ = 1.35f; break;
			default: base_ = 1f; break;
		}
		return binding ? base_ : 1f + (base_ - 1f) * 0.5f;
	}

	/// <summary>Годовой сдвиг поддержки от налогов: лёгкие +2, кабальные −3.</summary>
	public static float TaxMood(TaxLaw law, bool binding)
	{
		float raw = law switch
		{
			TaxLaw.Light => 2f,
			TaxLaw.Crushing => -3f,
			_ => 0f
		};
		return binding ? raw : raw * 0.5f;
	}

	// ============ VIII О купечестве и пошлинах ============

	/// <summary>Множитель торговой прибыли: свободный торг 1.15, умеренные 1, протекционизм 0.7.</summary>
	public static float TradeIncomeMultiplier(TradeLaw law, bool binding)
	{
		float base_;
		switch (law)
		{
			case TradeLaw.FreeTrade: base_ = 1.15f; break;
			case TradeLaw.Protectionism: base_ = 0.7f; break;
			default: base_ = 1f; break;
		}
		return binding ? base_ : 1f + (base_ - 1f) * 0.5f;
	}

	// ============ IX О вере и веротерпимости ============

	/// <summary>Годовой сдвиг недовольства от веры: терпимость −0.01, гос. вера 0, чистота +0.03.</summary>
	public static float FaithDissent(FaithLaw law, bool binding)
	{
		float raw = law switch
		{
			FaithLaw.Tolerance => -0.01f,
			FaithLaw.Purity => 0.03f,
			_ => 0f
		};
		return binding ? raw : raw * 0.5f;
	}

	// ============ X О ратной повинности ============

	/// <summary>Множитель содержания войска: ополчение 0.7, призыв 1, гвардия 1.4.</summary>
	public static float LevyUpkeepMultiplier(LevyLaw law, bool binding)
	{
		float base_;
		switch (law)
		{
			case LevyLaw.Militia: base_ = 0.7f; break;
			case LevyLaw.Guard: base_ = 1.4f; break;
			default: base_ = 1f; break;
		}
		return binding ? base_ : 1f + (base_ - 1f) * 0.5f;
	}

	/// <summary>Множитель военной мощи державы в оценке сил: ополчение 0.85, гвардия 1.25.</summary>
	public static float LevyPowerMultiplier(LevyLaw law, bool binding)
	{
		float base_;
		switch (law)
		{
			case LevyLaw.Militia: base_ = 0.85f; break;
			case LevyLaw.Guard: base_ = 1.25f; break;
			default: base_ = 1f; break;
		}
		return binding ? base_ : 1f + (base_ - 1f) * 0.5f;
	}

	// ============ XI О чужой земле ============

	/// <summary>Множитель цены завоевания (казус белли Conquest/Reconquest): возврат 1.5, приращение 1, вольное 0.7.</summary>
	public static float LandCasusBelliMultiplier(LandLaw law, bool binding)
	{
		float base_;
		switch (law)
		{
			case LandLaw.Restitution: base_ = 1.5f; break;
			case LandLaw.FreeConquest: base_ = 0.7f; break;
			default: base_ = 1f; break;
		}
		return binding ? base_ : 1f + (base_ - 1f) * 0.5f;
	}

	// ============ XII О тайной страже ============

	/// <summary>Множитель шанса мятежа: вольница 1.15, дозор 1, сыск 0.85.</summary>
	public static float WatchRebellionMultiplier(WatchLaw law, bool binding)
	{
		float base_;
		switch (law)
		{
			case WatchLaw.Liberty: base_ = 1.15f; break;
			case WatchLaw.SecretPolice: base_ = 0.85f; break;
			default: base_ = 1f; break;
		}
		return binding ? base_ : 1f + (base_ - 1f) * 0.5f;
	}

	/// <summary>Цена стражи в золоте за год: вольница 0, дозор 2, сыск 6.</summary>
	public static float WatchGoldCost(WatchLaw law, bool binding)
	{
		float raw = law switch
		{
			WatchLaw.Patrol => 2f,
			WatchLaw.SecretPolice => 6f,
			_ => 0f
		};
		return binding ? raw : raw * 0.5f;
	}

	/// <summary>Мягкий сдвиг недовольства от сыска: вольница +0.01 (распущенность), сыск −0.02.</summary>
	public static float WatchDissent(WatchLaw law, bool binding)
	{
		float raw = law switch
		{
			WatchLaw.Liberty => 0.01f,
			WatchLaw.SecretPolice => -0.02f,
			_ => 0f
		};
		return binding ? raw : raw * 0.5f;
	}

	/// <summary>Обязательность грамоты: статья VI (Binding — полная сила).</summary>
	public static bool IsBinding(KingdomPolitics state)
	{
		return state?.Constitution != null && state.Constitution.Supremacy == SupremacyRule.Binding;
	}
}
