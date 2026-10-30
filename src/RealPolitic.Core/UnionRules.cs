namespace RealPolitic.Core;

public sealed class UnionRules
{
	public float MinAffinity = 0.4f;

	public float MinLeaderLegitimacy = 55f;

	public float MinMemberLegitimacy = 40f;

	public float MaxMemberDissent = 0.5f;

	public float BreakupApproval = 0.25f;

	public float BreakupCohesion = 0.2f;

	public float VassalPowerRatio = 1.6f;

	/// <summary>Сколько держав помещается в одну унию (вместе с поглощёнными). Без потолка
	/// сильная держава со временем проглотила бы весь мир.</summary>
	public int MaxMembers = 5;
}
