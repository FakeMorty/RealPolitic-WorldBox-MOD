using System;
using System.Collections.Generic;

namespace RealPolitic.Core;

/// <summary>Торговый путь между двумя державами. Путь принадлежит обеим сторонам:
/// прибыль делится, а война или эмбарго любой из сторон перекрывает его целиком.</summary>
public sealed class TradeRoute
{
	public int Id { get; set; }

	public int FromKingdomId { get; set; }

	public int ToKingdomId { get; set; }

	public int EstablishedYear { get; set; }

	/// <summary>Уровень пути 1..3: караванная тропа, дорога, торговый договор.</summary>
	public int Level { get; set; } = 1;

	/// <summary>Накопленный объём торговли: растёт, пока путь открыт, падает в блокаде.</summary>
	public float Volume { get; set; }

	/// <summary>Путь перекрыт (война или эмбарго). Прибыль не начисляется.</summary>
	public bool Blocked { get; set; }

	public float LastProfitFrom { get; set; }

	public float LastProfitTo { get; set; }

	public bool Involves(int kingdomId)
	{
		return FromKingdomId == kingdomId || ToKingdomId == kingdomId;
	}

	public int OtherSide(int kingdomId)
	{
		return FromKingdomId == kingdomId ? ToKingdomId : FromKingdomId;
	}

	public int YearsActive(int year)
	{
		return year - EstablishedYear;
	}
}

/// <summary>Сеть торговых путей мира. Живёт в <see cref="PoliticalWorld"/> и переживает тики.</summary>
public sealed class TradeNetwork
{
    public Func<int,int,bool> ContactGate {get;set;}
    private bool Allowed(int a,int b)=>ContactGate==null || ContactGate(a,b);
	private readonly List<TradeRoute> _routes = new List<TradeRoute>();

	private int _nextId = 1;

	public IReadOnlyList<TradeRoute> Routes => _routes;

	public int Count => _routes.Count;

	public TradeRoute? Find(int a, int b)
	{
		for (int i = 0; i < _routes.Count; i++)
		{
			TradeRoute tradeRoute = _routes[i];
			if ((tradeRoute.FromKingdomId == a && tradeRoute.ToKingdomId == b) || (tradeRoute.FromKingdomId == b && tradeRoute.ToKingdomId == a))
			{
				return tradeRoute;
			}
		}
		return null;
	}

	/// <summary>Все пути державы (открытые и перекрытые).</summary>
	public List<TradeRoute> Of(int kingdomId)
	{
		List<TradeRoute> list = new List<TradeRoute>();
		for (int i = 0; i < _routes.Count; i++)
		{
			if (_routes[i].Involves(kingdomId) && Allowed(_routes[i].FromKingdomId,_routes[i].ToKingdomId))
			{
				list.Add(_routes[i]);
			}
		}
		return list;
	}

	public int OpenRoutesOf(int kingdomId)
	{
		int num = 0;
		for (int i = 0; i < _routes.Count; i++)
		{
			if (_routes[i].Involves(kingdomId) && !_routes[i].Blocked && Allowed(_routes[i].FromKingdomId,_routes[i].ToKingdomId))
			{
				num++;
			}
		}
		return num;
	}

	/// <summary>Открывает путь, если его ещё нет. Возвращает созданный путь или null.</summary>
	public TradeRoute? Open(int a, int b, int year)
	{
		if (a == b || a == 0 || b == 0 || Find(a, b) != null || !Allowed(a,b))
		{
			return null;
		}
		TradeRoute tradeRoute = new TradeRoute
		{
			Id = _nextId++,
			FromKingdomId = a,
			ToKingdomId = b,
			EstablishedYear = year,
			Level = 1,
			Volume = 10f
		};
		_routes.Add(tradeRoute);
		return tradeRoute;
	}

	/// <summary>Восстанавливает путь из сохранения (без генерации нового id).</summary>
	public TradeRoute Restore(TradeRoute route)
	{
		if (route == null)
		{
			return null!;
		}
		_routes.Add(route);
		if (route.Id >= _nextId)
		{
			_nextId = route.Id + 1;
		}
		return route;
	}

	/// <summary>Перекрывает все пути между двумя державами. Возвращает число перекрытых.</summary>
	public int BlockBetween(int a, int b)
	{
		int num = 0;
		for (int i = 0; i < _routes.Count; i++)
		{
			TradeRoute tradeRoute = _routes[i];
			if (((tradeRoute.FromKingdomId == a && tradeRoute.ToKingdomId == b) || (tradeRoute.FromKingdomId == b && tradeRoute.ToKingdomId == a)) && !tradeRoute.Blocked)
			{
				tradeRoute.Blocked = true;
				num++;
			}
		}
		return num;
	}

	/// <summary>Снимает блокаду путей державы со всеми, кроме перечисленных врагов.</summary>
	/// <summary>Снимает блокировку путей пары (граница открылась).</summary>
	public int UnblockBetween(int a, int b)
	{
		int num = 0;
		for (int i = 0; i < _routes.Count; i++)
		{
			TradeRoute tradeRoute = _routes[i];
			if (tradeRoute.Blocked && tradeRoute.Involves(a) && tradeRoute.Involves(b))
			{
				tradeRoute.Blocked = false;
				num++;
			}
		}
		return num;
	}

	public int UnblockFor(int kingdomId, HashSet<int>? stillAtWar)
	{
		int num = 0;
		for (int i = 0; i < _routes.Count; i++)
		{
			TradeRoute tradeRoute = _routes[i];
			if (tradeRoute.Blocked && tradeRoute.Involves(kingdomId))
			{
				int num2 = tradeRoute.OtherSide(kingdomId);
				if (stillAtWar == null || !stillAtWar.Contains(num2))
				{
					tradeRoute.Blocked = false;
					num++;
				}
			}
		}
		return num;
	}

	/// <summary>Удаляет все пути державы (держава исчезла или раскололась).</summary>
	public int DropKingdom(int kingdomId)
	{
		return _routes.RemoveAll((TradeRoute r) => r.Involves(kingdomId));
	}

	/// <summary>Переносит пути старого id на новый (раскол державы: временный id → реальный).</summary>
	public int RemapKingdom(int oldId, int newId)
	{
		if (oldId == newId)
		{
			return 0;
		}
		int num = 0;
		for (int i = 0; i < _routes.Count; i++)
		{
			TradeRoute tradeRoute = _routes[i];
			if (tradeRoute.FromKingdomId == oldId)
			{
				tradeRoute.FromKingdomId = newId;
				num++;
			}
			if (tradeRoute.ToKingdomId == oldId)
			{
				tradeRoute.ToKingdomId = newId;
				num++;
			}
		}
		return num;
	}

	public void Clear()
	{
		_routes.Clear();
		_nextId = 1;
	}
}
