using System.Collections.Generic;

namespace RealPolitic.Core;

/// <summary>Тип международного договора (этап 20).</summary>
public enum TreatyType
{
	/// <summary>Пакт о ненападении: пока действует, стороны не объявляют друг другу войну.</summary>
	NonAggression = 1,

	/// <summary>Оборонительный союз: нападение на одну сторону поднимает в войну другую.</summary>
	DefensePact = 2,

	/// <summary>Торговое соглашение: бонус к торговле между сторонами.</summary>
	TradeAgreement = 3
}

/// <summary>Договор между двумя державами: тип, стороны, срок действия.</summary>
public sealed class Treaty
{
	public TreatyType Type { get; set; }

	public int PartyA { get; set; }

	public int PartyB { get; set; }

	public int SignedYear { get; set; }

	public int ExpiresYear { get; set; }

	/// <summary>Ключ локализации имени типа договора.</summary>
	public static string TypeLocKey(TreatyType type)
	{
		switch (type)
		{
			case TreatyType.NonAggression: return "RP_TREATY_NONAGGRESSION";
			case TreatyType.DefensePact: return "RP_TREATY_DEFENSE_PACT";
			case TreatyType.TradeAgreement: return "RP_TREATY_TRADE";
			default: return "RP_TREATY_NONAGGRESSION";
		}
	}

	/// <summary>Договор касается державы с данным id.</summary>
	public bool Involves(int kingdomId)
	{
		return PartyA == kingdomId || PartyB == kingdomId;
	}

	/// <summary>Вторая сторона договора относительно данной державы.</summary>
	public int OtherParty(int kingdomId)
	{
		return PartyA == kingdomId ? PartyB : PartyA;
	}
}

/// <summary>Книга договоров политического мира: хранение, поиск, истечение.</summary>
public sealed class TreatyBook
{
	private readonly List<Treaty> _treaties = new List<Treaty>();

	public IReadOnlyList<Treaty> All => _treaties;

	public int Count => _treaties.Count;

	/// <summary>Договор данного типа между парой держав (порядок сторон не важен).</summary>
	public Treaty Between(int a, int b, TreatyType type)
	{
		for (int i = 0; i < _treaties.Count; i++)
		{
			Treaty t = _treaties[i];
			if (t.Type == type && ((t.PartyA == a && t.PartyB == b) || (t.PartyA == b && t.PartyB == a)))
			{
				return t;
			}
		}
		return null;
	}

	/// <summary>Любой действующий договор между парой.</summary>
	public Treaty AnyBetween(int a, int b)
	{
		for (int i = 0; i < _treaties.Count; i++)
		{
			Treaty t = _treaties[i];
			if ((t.PartyA == a && t.PartyB == b) || (t.PartyA == b && t.PartyB == a))
			{
				return t;
			}
		}
		return null;
	}

	/// <summary>Все договоры державы.</summary>
	public List<Treaty> Of(int kingdomId)
	{
		List<Treaty> result = new List<Treaty>();
		for (int i = 0; i < _treaties.Count; i++)
		{
			if (_treaties[i].Involves(kingdomId))
			{
				result.Add(_treaties[i]);
			}
		}
		return result;
	}

	/// <summary>Действует ли пакт о ненападении между парой.</summary>
	public bool NonAggressionActive(int a, int b)
	{
		return Between(a, b, TreatyType.NonAggression) != null
			|| Between(a, b, TreatyType.DefensePact) != null;
	}

	public void Add(Treaty treaty)
	{
		_treaties.Add(treaty);
	}

	public bool Remove(Treaty treaty)
	{
		return treaty != null && _treaties.Remove(treaty);
	}

	/// <summary>Снимает все договоры уничтоженной державы.</summary>
	public void RemoveKingdom(int kingdomId)
	{
		for (int i = _treaties.Count - 1; i >= 0; i--)
		{
			if (_treaties[i].Involves(kingdomId))
			{
				_treaties.RemoveAt(i);
			}
		}
	}

	/// <summary>Снимает договоры, чей срок истёк. Возвращает их для летописи.</summary>
	public List<Treaty> Expire(int year)
	{
		List<Treaty> expired = new List<Treaty>();
		for (int i = _treaties.Count - 1; i >= 0; i--)
		{
			if (_treaties[i].ExpiresYear <= year)
			{
				expired.Add(_treaties[i]);
				_treaties.RemoveAt(i);
			}
		}
		expired.Reverse();
		return expired;
	}

	public void Clear()
	{
		_treaties.Clear();
	}
}
