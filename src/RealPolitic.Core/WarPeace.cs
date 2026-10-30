using System;
using System.Collections.Generic;

namespace RealPolitic.Core;

/// <summary>Итог мирного договора этапа 22: насколько жёсткие условия диктует победитель.</summary>
public enum SettlementLevel
{
	/// <summary>Стороны разошлись без победителя — условий нет, только перемирие.</summary>
	WhitePeace = 0,

	/// <summary>Проигравший публично унижен: легитимность и поддержка падают, обида копится.</summary>
	Humiliation = 1,

	/// <summary>Проигравший платит ежегодные репарации победителю.</summary>
	Reparations = 2,

	/// <summary>Проигравший уступает города победителю (плюс репарации и унижение).</summary>
	LandDemand = 3,

	/// <summary>Держава стёрта с карты. Мирный договор её не восстанавливает: уцелевшие
	/// земли остаются у победителя как поглощённый вассалитет либо обычная аннексия.</summary>
	TotalDefeat = 4
}

/// <summary>Режим приказа на восстановление. Значение Puppet сохранено только для
/// совместимости со старыми сохранениями: такие мирные приказы теперь превращаются
/// в поглощённые вассальные земли и отдельную державу не создают.</summary>
public enum RestorationMode
{
	/// <summary>Legacy: мирная марионетка из старых сохранений; при исполнении — вассальные земли сюзерена.</summary>
	Puppet = 0,

	/// <summary>Свободная держава после вооружённого освобождения/войны вассала за независимость.</summary>
	Free = 1
}

/// <summary>Приказ ядра игровому слою на отделение земель. Исполнить как новую державу
/// можно только вооружённое национальное освобождение; мирные/legacy-приказы
/// превращаются в поглощённые вассальные земли без создания государства.</summary>
public sealed class RestorationOrder
{
	/// <summary>Id уничтоженной державы (в игре её уже нет).</summary>
	public int DefeatedId { get; set; }

	public int VictorId { get; set; }

	public RestorationMode Mode { get; set; }

	/// <summary>Довоенные города проигравшего (в порядке: столица первой).</summary>
	public List<int> PrewarCityIds { get; set; } = new List<int>();

	/// <summary>Сколько первых городов в списке остаётся у метрополии при вооружённом отделении.</summary>
	public int CitiesKeptByVictor { get; set; }

	/// <summary>Имя погибшей державы — для летописи и имени новой.</summary>
	public string DefeatedName { get; set; } = string.Empty;

	public int Year { get; set; }

	/// <summary>0.30/0.31: приказ создан движком национального освобождения (а не послевоенной мирной конференцией).</summary>
	public bool IsNationalLiberation { get; set; }

	/// <summary>Legacy-флаг мирного освобождения из старых сохранений. С 0.31.2 такие
	/// приказы не создают державу, а превращаются в поглощённые вассальные земли.</summary>
	public bool LiberationPeaceful { get; set; }
}

/// <summary>Запись о заключённом мирном договоре: кто, кого, на каких условиях.</summary>
public sealed class PeaceSettlement
{
	public int VictorId { get; set; }

	public int DefeatedId { get; set; }

	public int Year { get; set; }

	public SettlementLevel Level { get; set; }

	/// <summary>Сила победы (0..100+), определившая уровень условий.</summary>
	public float Score { get; set; }

	/// <summary>Сколько городов проигравший обязан передать (0 — земли не трогаем).</summary>
	public int CitiesDemanded { get; set; }

	/// <summary>Ежегодные репарации золотом (0 — не платит).</summary>
	public float ReparationsPerYear { get; set; }

	/// <summary>Сколько лет платить репарации.</summary>
	public int ReparationsYears { get; set; }

	/// <summary>До этого года действует перемирие между сторонами.</summary>
	public int TruceUntilYear { get; set; }

	/// <summary>Ключ локализации описания условий ({cities},{gold},{years} подставляются в тексте).</summary>
	public string TermsLocKey
	{
		get
		{
			switch (Level)
			{
				case SettlementLevel.TotalDefeat: return "RP_WARTERMS_TOTAL";
				case SettlementLevel.LandDemand: return "RP_WARTERMS_FULL";
				case SettlementLevel.Reparations: return "RP_WARTERMS_PARTIAL";
				case SettlementLevel.Humiliation: return "RP_WARTERMS_HUMILIATION";
				default: return "RP_WARTERMS_HUMILIATION";
			}
		}
	}
}

/// <summary>Приказ на передачу городов по мирному договору (ядро → игровой слой).</summary>
public sealed class CityDemandOrder
{
	public int FromId { get; set; }

	public int ToId { get; set; }

	/// <summary>Сколько городов передать (столица и последний город неприкосновенны).</summary>
	public int Count { get; set; }
}

/// <summary>Ежегодные репарации проигравшего победителю (этап 22).</summary>
public sealed class ReparationsOrder
{
	public int PayerId { get; set; }

	public int PayeeId { get; set; }

	public float AmountPerYear { get; set; }

	public int YearsLeft { get; set; }
}

/// <summary>
/// Сводка потерь сторон в коалиционной войне (число и список уничтоженных государств с каждой стороны).
/// Не позволяет записывать исход как «белый мир», если у одной из сторон за войну были уничтожены государства.
/// </summary>
public sealed class WarCoalitionOutcome
{
	public int TotalAttackers { get; set; } = 1;

	public int DestroyedAttackers { get; set; }

	public List<string> DestroyedAttackerNames { get; set; } = new List<string>();

	public int TotalDefenders { get; set; } = 1;

	public int DestroyedDefenders { get; set; }

	public List<string> DestroyedDefenderNames { get; set; } = new List<string>();

	public List<int> AttackerSideIds { get; set; } = new List<int>();

	public List<int> DefenderSideIds { get; set; } = new List<int>();

	public bool HasDestroyedCountries => DestroyedAttackers > 0 || DestroyedDefenders > 0;

	public bool IsCoalitionWar => TotalAttackers > 1 || TotalDefenders > 1;

	/// <summary>
	/// Определяет победившую сторону:
	/// +1 = победили нападающие, -1 = победили защитники, 0 = ничья / белый мир.
	/// </summary>
	public int ResolveWinnerSide(int rawWinnerSign)
	{
		int totA = Math.Max(1, TotalAttackers);
		int totD = Math.Max(1, TotalDefenders);
		int deadA = Math.Max(0, DestroyedAttackers);
		int deadD = Math.Max(0, DestroyedDefenders);

		if (deadD > deadA)
		{
			return 1;
		}
		if (deadA > deadD)
		{
			return -1;
		}
		if (deadA > 0 && deadA == deadD)
		{
			long shareD = (long)deadD * totA;
			long shareA = (long)deadA * totD;
			if (shareD > shareA)
			{
				return 1;
			}
			if (shareA > shareD)
			{
				return -1;
			}
		}
		return rawWinnerSign;
	}

	/// <summary>Строка потерь сторон для летописи («уничтожено стран: у нападающих 3 из 4 [A, B, C], у защитников 0 из 2»).</summary>
	public string FormatCasualtiesSummary()
	{
		int totA = Math.Max(1, TotalAttackers);
		int totD = Math.Max(1, TotalDefenders);
		int deadA = Math.Max(0, DestroyedAttackers);
		int deadD = Math.Max(0, DestroyedDefenders);
		if (deadA == 0 && deadD == 0 && totA <= 1 && totD <= 1)
		{
			return string.Empty;
		}
		string aList = deadA > 0 && DestroyedAttackerNames != null && DestroyedAttackerNames.Count > 0
			? " [" + string.Join(", ", DestroyedAttackerNames) + "]"
			: string.Empty;
		string dList = deadD > 0 && DestroyedDefenderNames != null && DestroyedDefenderNames.Count > 0
			? " [" + string.Join(", ", DestroyedDefenderNames) + "]"
			: string.Empty;
		return "уничтожено стран: у нападающих " + deadA + " из " + totA + aList
			+ ", у защитников " + deadD + " из " + totD + dList;
	}
}

/// <summary>
/// Книга перемирий (этап 22): после любой развязки войны пара держав связана
/// перемирием до указанного года. Нарушение — политическое самоубийство.
/// </summary>
public sealed class TruceBook
{
	private readonly Dictionary<long, int> _until = new Dictionary<long, int>();

	private static long PairKey(int a, int b)
	{
		int min = Math.Min(a, b);
		int max = Math.Max(a, b);
		return ((long)min << 32) | (uint)max;
	}

	public int Count => _until.Count;

	public void Set(int a, int b, int untilYear)
	{
		if (a == b)
		{
			return;
		}
		_until[PairKey(a, b)] = untilYear;
	}

	/// <summary>До какого года действует перемирие пары (0 — перемирия нет).</summary>
	public int Until(int a, int b)
	{
		return _until.TryGetValue(PairKey(a, b), out int value) ? value : 0;
	}

	public bool Active(int a, int b, int year)
	{
		int until = Until(a, b);
		return until > 0 && year < until;
	}

	public void Remove(int a, int b)
	{
		_until.Remove(PairKey(a, b));
	}

	/// <summary>Снимает истёкшие перемирия. Возвращает пары для летописи.</summary>
	public List<KeyValuePair<int, int>> Expire(int year)
	{
		List<long> dead = new List<long>();
		foreach (KeyValuePair<long, int> pair in _until)
		{
			if (pair.Value <= year)
			{
				dead.Add(pair.Key);
			}
		}
		List<KeyValuePair<int, int>> expired = new List<KeyValuePair<int, int>>(dead.Count);
		for (int i = 0; i < dead.Count; i++)
		{
			_until.Remove(dead[i]);
			expired.Add(new KeyValuePair<int, int>((int)(dead[i] >> 32), (int)(dead[i] & 0xFFFFFFFF)));
		}
		return expired;
	}

	/// <summary>Убирает все перемирия с участием исчезнувшей державы.</summary>
	public void RemoveKingdom(int kingdomId)
	{
		List<long> dead = new List<long>();
		foreach (KeyValuePair<long, int> pair in _until)
		{
			if ((int)(pair.Key >> 32) == kingdomId || (int)(pair.Key & 0xFFFFFFFF) == kingdomId)
			{
				dead.Add(pair.Key);
			}
		}
		for (int i = 0; i < dead.Count; i++)
		{
			_until.Remove(dead[i]);
		}
	}

	/// <summary>Все действующие перемирия — для сохранения (ключ пары → до какого года).</summary>
	public IReadOnlyDictionary<long, int> Raw => _until;

	public void Clear()
	{
		_until.Clear();
	}
}

/// <summary>Параметры усталости от войны: как строй переносит затяжные кампании.</summary>
public static class WarFatigueDefs
{
	/// <summary>Множитель роста усталости по идеологии. Милитаризм живёт войной,
	/// торговля и равенство страдают первыми.</summary>
	public static float IdeologyFactor(IdeologyType type)
	{
		switch (type)
		{
			case IdeologyType.Militarism: return 0.6f;
			case IdeologyType.Ethnonationalism: return 0.75f;
			case IdeologyType.Communism: return 0.75f;
			case IdeologyType.Theocracy: return 0.85f;
			case IdeologyType.Tribalism: return 0.85f;
			case IdeologyType.Technocracy: return 1.1f;
			case IdeologyType.MerchantRepublic: return 1.4f;
			case IdeologyType.Democracy: return 1.4f;
			case IdeologyType.Egalitarianism: return 1.5f;
			case IdeologyType.Khaganate: return 0.70f;
			case IdeologyType.Empire: return 0.85f;
			case IdeologyType.FreeTerritory: return 0.90f;
			default: return 1f;
		}
	}

	/// <summary>Множитель риска мятежа от изнурённости войной (к 100 усталости — полуторный риск).</summary>
	public static float RebellionFactor(float fatigue)
	{
		if (fatigue < 50f)
		{
			return 1f;
		}
		return 1f + (fatigue - 50f) / 100f;
	}
}
