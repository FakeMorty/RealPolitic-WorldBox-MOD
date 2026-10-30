namespace RealPolitic.Core;

public sealed class CityPolitics
{
	public int CityId { get; set; }

	public int KingdomId { get; set; }

	public IdeologyType Dominant { get; set; }

	public float Share { get; set; }

	public float AvgDissent { get; set; }
}
