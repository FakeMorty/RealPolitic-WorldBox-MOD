using System.Collections.Generic;

namespace RealPolitic.Core;

public struct KingdomInput
{
	public int KingdomId;

	public string Name;

	public int Population;

	public int Warriors;

	public bool AtWar;

	public bool Starving;

	/// <summary>Реальная казна королевства (золото игры); -1 = неизвестно.</summary>
	public float Gold;

	public RulerRef? Ruler;

	public List<RelationInfo> Relations;

	public List<CityInput> Cities;

	public string RaceId;

	public string RaceName;
}
