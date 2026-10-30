using System.Collections.Generic;

namespace RealPolitic.Core;

public sealed class UnionMember
{
	public int KingdomId { get; set; }

	public bool IsLeader { get; set; }

	public float Approval { get; set; } = 0.6f;

	public int JoinedYear { get; set; }

	/// <summary>Столица вассала на момент вхождения в вассалитет (для восстановления границ при войне за независимость).</summary>
	public int CapitalCityId { get; set; }

	/// <summary>Запомненные города (границы) вассала на момент вливания в сюзерена.</summary>
	public List<int> CityIds { get; set; } = new List<int>();

	/// <summary>Запомненное название вассальной державы.</summary>
	public string OriginalName { get; set; } = string.Empty;

	/// <summary>Запомненная идеология вассальной державы.</summary>
	public IdeologyType OriginalIdeology { get; set; } = IdeologyType.Monarchy;
}
