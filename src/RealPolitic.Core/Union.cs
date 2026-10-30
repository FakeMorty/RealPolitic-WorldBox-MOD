using System;
using System.Collections.Generic;

namespace RealPolitic.Core;

/// <summary>
/// 0.24: запись о поглощённом в унию государстве. Границы снимаются в момент поглощения —
/// это снапшот для возможного будущего отделения: с 0.31.2 поглощённые земли не отпускают
/// мирно, поэтому отделение по этому снимку возможно только через восстание и войну.
/// </summary>
[Serializable]
public sealed class AbsorbedChild
{
	public int KingdomId { get; set; }

	/// <summary>Столица поглощённой державы (для будущего восстановления).</summary>
	public int CapitalCityId { get; set; }

	/// <summary>Все города поглощённой державы на момент поглощения.</summary>
	public List<int> CityIds { get; set; } = new List<int>();

	public int AbsorbedYear { get; set; }
}

/// <summary>Уния держав. Останки старой модели (этап 21) сведены к двум формам —
/// вассалитет (дань, без поглощения) и соединение (поглощение: вступивший тотчас сливается).</summary>
public sealed class Union
{
	public readonly List<UnionMember> Members = new List<UnionMember>();

	private IdeologyType _leaderIdeology;

	public int Id { get; set; }

	public UnionType Type { get; set; }

	public string Name { get; set; } = string.Empty;

	/// <summary>Ключ локализации имени. Заполняется, когда имя придумало ядро,
	/// у которого нет доступа к словарям игры: игровой слой подставит перевод сам.
	/// У союзов, названных в игре («Рим–Карфаген»), ключ пуст и берётся <see cref="Name"/>.</summary>
	public string NameKey { get; set; } = string.Empty;

	/// <summary>Что показывать в событии: ключ для локализации или готовое имя.</summary>
	public string DisplayNameOrKey => NameKey.Length > 0 ? NameKey : Name;

	public int LeaderId { get; set; }

	public int FormedYear { get; set; }

	public float Cohesion { get; set; } = 0.7f;

	public int LeaderWarriors { get; set; }

	/// <summary>0.24: державы, поглощённые в соединение (снятые границы — основа права на выход).</summary>
	public readonly List<AbsorbedChild> AbsorbedChildren = new List<AbsorbedChild>();

	public IdeologyDef LeaderIdeology => Ideologies.Get(_leaderIdeology);

	/// <summary>
	/// Является ли данное объединение конфедерацией вольных территорий (только вольные территории).
	/// </summary>
	public bool IsConfederation => Type == UnionType.Merger && Unions.IsConfederationIdeology(_leaderIdeology);

	/// <summary>
	/// Является ли данное объединение федерацией (все идеологии, кроме уний и конфедераций вольных территорий).
	/// </summary>
	public bool IsFederation => Type == UnionType.Merger && !Unions.IsUnionIdeology(_leaderIdeology) && !Unions.IsConfederationIdeology(_leaderIdeology);

	public int MemberCount => Members.Count;

	/// <summary>Живых участников и поглощённых держав — общий размер союза для потолка.</summary>
	public int TotalSize => Members.Count + AbsorbedChildren.Count;

	public void SetLeaderIdeology(IdeologyType type)
	{
		_leaderIdeology = type;
	}

	public UnionMember? GetMember(int kingdomId)
	{
		for (int i = 0; i < Members.Count; i++)
		{
			if (Members[i].KingdomId == kingdomId)
			{
				return Members[i];
			}
		}
		return null;
	}

	public bool Contains(int kingdomId)
	{
		return GetMember(kingdomId) != null;
	}

	/// <summary>Была ли держава поглощена этой унией (для права на выход в 0.25).</summary>
	public AbsorbedChild? GetAbsorbed(int kingdomId)
	{
		for (int i = 0; i < AbsorbedChildren.Count; i++)
		{
			if (AbsorbedChildren[i].KingdomId == kingdomId)
			{
				return AbsorbedChildren[i];
			}
		}
		return null;
	}

	/// <summary>Добавляет запись о поглощении; повторное поглощение не дублирует список.</summary>
	public void RecordAbsorption(AbsorbedChild child)
	{
		if (child == null || child.KingdomId <= 0)
		{
			return;
		}
		if (GetAbsorbed(child.KingdomId) == null)
		{
			AbsorbedChildren.Add(child);
		}
	}
}
