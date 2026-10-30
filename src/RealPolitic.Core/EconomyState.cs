namespace RealPolitic.Core;

/// <summary>Тип экономического кризиса.</summary>
public enum EconomicCrisisType
{
	None = 0,

	/// <summary>Спад: падает благосостояние, растёт недовольство.</summary>
	Recession = 1,

	/// <summary>Депрессия: затяжной спад с обвалом благосостояния.</summary>
	Depression = 2,

	/// <summary>Банкротство казны: многолетний дефицит закончился дефолтом.</summary>
	Bankruptcy = 3,

	/// <summary>Крах торговли: все пути перекрыты войной или эмбарго.</summary>
	TradeCollapse = 4
}

/// <summary>Ключ локализации типа кризиса (для уведомлений и летописи).</summary>
public static class EconomicCrises
{
	public static string LocKey(EconomicCrisisType type)
	{
		return type switch
		{
			EconomicCrisisType.Recession => "RP_ECO_CRISIS_RECESSION",
			EconomicCrisisType.Depression => "RP_ECO_CRISIS_DEPRESSION",
			EconomicCrisisType.Bankruptcy => "RP_ECO_CRISIS_BANKRUPTCY",
			EconomicCrisisType.TradeCollapse => "RP_ECO_CRISIS_TRADECOLLAPSE",
			_ => "RP_ECO_CRISIS_NONE"
		};
	}

	/// <summary>Насколько кризис опасен для устойчивости власти (множитель эффектов).</summary>
	public static float Severity(EconomicCrisisType type)
	{
		return type switch
		{
			EconomicCrisisType.Recession => 1f,
			EconomicCrisisType.Depression => 1.8f,
			EconomicCrisisType.Bankruptcy => 2.2f,
			EconomicCrisisType.TradeCollapse => 1.3f,
			_ => 0f
		};
	}

	/// <summary>Кризис достаточно тяжёл, чтобы стать причиной мятежа.</summary>
	public static bool CanSparkRebellion(EconomicCrisisType type)
	{
		return type == EconomicCrisisType.Depression || type == EconomicCrisisType.Bankruptcy
			|| type == EconomicCrisisType.TradeCollapse;
	}
}

/// <summary>Экономическое состояние державы: благосостояние, налоги, пошлины, кризисы.</summary>
public sealed class EconomyState
{
	/// <summary>Благосостояние 0..100: растёт от прибыли и торговли, падает от войн, голода и кризисов.</summary>
	public float Prosperity { get; set; } = 50f;

	/// <summary>Налоговый множитель (0.6..1.6). Высокие налоги — больше дохода, меньше поддержки.</summary>
	public float TaxRate { get; set; } = 1f;

	/// <summary>Пошлина на чужих купцов (0..0.5). Даёт доход, но душит объём торговли.</summary>
	public float Tariff { get; set; }

	/// <summary>Доход за прошлый год (налоги + торговля).</summary>
	public float LastIncome { get; set; }

	/// <summary>Расходы за прошлый год (армия, управление, война).</summary>
	public float LastExpenses { get; set; }

	/// <summary>Доля дохода от торговли за прошлый год (0..1) — мера зависимости от путей.</summary>
	public float TradeShare { get; set; }

	/// <summary>Сколько лет подряд казна в дефиците.</summary>
	public int DeficitYears { get; set; }

	/// <summary>Активный кризис.</summary>
	public EconomicCrisisType Crisis { get; set; } = EconomicCrisisType.None;

	/// <summary>Год, до которого длится кризис.</summary>
	public int CrisisUntilYear { get; set; }

	/// <summary>Год последнего кризиса (0 — не было): защита от кризиса каждый год.</summary>
	public int LastCrisisYear { get; set; }

	/// <summary>Сколько раз держава переживала кризисы и сколько было периодов процветания.</summary>
	public int CrisesTotal { get; set; }

	public int BoomsTotal { get; set; }

	/// <summary>Год последнего бума (для защиты от повторного уведомления).</summary>
	public int LastBoomYear { get; set; }

	/// <summary>Режим экономии: урезает расходы, но бьёт по поддержке.</summary>
	public bool Austerity { get; set; }

	public int AusterityUntilYear { get; set; }

	/// <summary>Цель торгового эмбарго (0 — эмбарго нет).</summary>
	public int EmbargoTargetId { get; set; }

	public int EmbargoUntilYear { get; set; }

	/// <summary>Год последнего изменения налогового режима (для кулдауна решений ИИ).</summary>
	public int LastTaxChangeYear { get; set; } = int.MinValue;

	public bool InCrisis => Crisis != EconomicCrisisType.None;

	public bool EmbargoActive(int year)
	{
		return EmbargoTargetId != 0 && year <= EmbargoUntilYear;
	}

	public bool AusterityActive(int year)
	{
		return Austerity && year <= AusterityUntilYear;
	}

	/// <summary>Сброс экономики при смене власти силой (захват власти повстанцами).</summary>
	public void OnRegimeChange()
	{
		Crisis = EconomicCrisisType.None;
		CrisisUntilYear = 0;
		DeficitYears = 0;
		Austerity = false;
		AusterityUntilYear = 0;
		EmbargoTargetId = 0;
		EmbargoUntilYear = 0;
		TaxRate = 1f;
	}
}
