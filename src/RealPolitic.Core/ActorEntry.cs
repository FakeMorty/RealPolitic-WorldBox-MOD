namespace RealPolitic.Core;

public sealed class ActorEntry
{
	public int Id { get; }

	public int KingdomId { get; set; }

	public int CityId { get; set; }

	public ActorPolitics Politics { get; }

	public ActorEntry(int id, ActorPolitics politics, int kingdomId, int cityId)
	{
		Id = id;
		Politics = politics;
		KingdomId = kingdomId;
		CityId = cityId;
	}
}
