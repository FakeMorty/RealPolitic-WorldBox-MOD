using System;

namespace RealPolitic.Core;

/// <summary>Тип тайной операции разведсети (этап 20).</summary>
public enum SpyOpType
{
	/// <summary>Саботаж: бьёт по благосостоянию и стабильности цели.</summary>
	Sabotage = 1,

	/// <summary>Подстрекательство: разжигает недовольство и роняет поддержку власти.</summary>
	InciteDissent = 2,

	/// <summary>Кража казны: золото цели перетекает заказчику.</summary>
	StealGold = 3
}

/// <summary>Параметры тайной операции: единственная точка баланса шпионажа.</summary>
public sealed class SpyOpDef
{
	public SpyOpType Type { get; set; }

	public string LocKey { get; set; }

	/// <summary>Базовый шанс успеха при сети 1-го уровня (+0.10 за каждый следующий).</summary>
	public float BaseSuccess { get; set; }

	/// <summary>Шанс разоблачения при провале.</summary>
	public float ExposureOnFail { get; set; }
}

/// <summary>Таблица тайных операций.</summary>
public static class SpyOps
{
	/// <summary>Максимальный уровень разведсети.</summary>
	public const int MaxNetworkLevel = 3;

	public static SpyOpDef Get(SpyOpType type)
	{
		switch (type)
		{
			case SpyOpType.InciteDissent:
				return new SpyOpDef { Type = type, LocKey = "RP_SPY_OP_INCITE", BaseSuccess = 0.55f, ExposureOnFail = 0.5f };
			case SpyOpType.StealGold:
				return new SpyOpDef { Type = type, LocKey = "RP_SPY_OP_STEAL", BaseSuccess = 0.5f, ExposureOnFail = 0.7f };
			default:
				return new SpyOpDef { Type = SpyOpType.Sabotage, LocKey = "RP_SPY_OP_SABOTAGE", BaseSuccess = 0.6f, ExposureOnFail = 0.5f };
		}
	}

	/// <summary>Шанс успеха операции данным уровнем сети.</summary>
	public static float SuccessChance(SpyOpType op, int networkLevel)
	{
		if (networkLevel < 1)
		{
			return 0f;
		}
		SpyOpDef def = Get(op);
		return Math.Min(0.95f, def.BaseSuccess + (networkLevel - 1) * 0.1f);
	}
}

/// <summary>Разведывательная сеть державы (этап 20).</summary>
public sealed class SpyNetwork
{
	/// <summary>Уровень сети 0..3; ноль — сети нет.</summary>
	public int Level { get; set; }

	/// <summary>Год последней тайной операции (для перезарядки).</summary>
	public int LastOpYear { get; set; } = int.MinValue;

	/// <summary>Год последнего вложения в сеть.</summary>
	public int LastInvestYear { get; set; } = int.MinValue;
}
