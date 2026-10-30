using System.Collections.Generic;

namespace RealPolitic.Core;

/// <summary>Военная коалиция против агрессора (этап 20).</summary>
public sealed class Coalition
{
	public int Id { get; set; }

	/// <summary>Держава, против которой создан блок.</summary>
	public int TargetId { get; set; }

	public readonly List<int> Members = new List<int>();

	public int FormedYear { get; set; }

	/// <summary>Год последней войны цели с кем-либо из членов: коалиция живёт, пока война свежа.</summary>
	public int LastWarYear { get; set; }

	/// <summary>Держава в составе коалиции.</summary>
	public bool Contains(int kingdomId)
	{
		return Members.Contains(kingdomId);
	}

	/// <summary>Прочие члены коалиции кроме указанной державы.</summary>
	public List<int> OtherMembers(int kingdomId)
	{
		List<int> result = new List<int>();
		for (int i = 0; i < Members.Count; i++)
		{
			if (Members[i] != kingdomId)
			{
				result.Add(Members[i]);
			}
		}
		return result;
	}
}

/// <summary>Запись об объявленной войне — топливо для «очков агрессии» (этап 20).</summary>
public sealed class WarDeclarationRecord
{
	public int AttackerId { get; set; }

	public int DefenderId { get; set; }

	public int Year { get; set; }

	public CasusBelliType CasusBelli { get; set; }
}

/// <summary>Приказ о дани: сколько вассал платит сюзерену в этом году (этап 20).</summary>
public sealed class TributeOrder
{
	public int VassalId { get; set; }

	public int SuzerainId { get; set; }

	public float Amount { get; set; }
}

/// <summary>Хранилище коалиций политического мира.</summary>
public sealed class CoalitionBook
{
	private readonly List<Coalition> _coalitions = new List<Coalition>();
	private int _nextId = 1;

	public IReadOnlyList<Coalition> All => _coalitions;

	/// <summary>Активная коалиция против данной державы.</summary>
	public Coalition Against(int targetId)
	{
		for (int i = 0; i < _coalitions.Count; i++)
		{
			if (_coalitions[i].TargetId == targetId)
			{
				return _coalitions[i];
			}
		}
		return null;
	}

	/// <summary>Коалиция, в которой состоит держава.</summary>
	public Coalition MemberOf(int kingdomId)
	{
		for (int i = 0; i < _coalitions.Count; i++)
		{
			if (_coalitions[i].Contains(kingdomId))
			{
				return _coalitions[i];
			}
		}
		return null;
	}

	public Coalition Create(int targetId, int year)
	{
		Coalition coalition = new Coalition
		{
			Id = _nextId++,
			TargetId = targetId,
			FormedYear = year,
			LastWarYear = year
		};
		_coalitions.Add(coalition);
		return coalition;
	}

	public bool Remove(Coalition coalition)
	{
		return coalition != null && _coalitions.Remove(coalition);
	}

	/// <summary>Восстанавливает коалицию из сохранения с её исходным идентификатором.</summary>
	public void Restore(Coalition coalition)
	{
		if (coalition == null)
		{
			return;
		}
		_coalitions.Add(coalition);
		if (coalition.Id >= _nextId)
		{
			_nextId = coalition.Id + 1;
		}
	}

	public void Clear()
	{
		_coalitions.Clear();
		_nextId = 1;
	}
}
