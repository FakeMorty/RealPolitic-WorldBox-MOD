namespace RealPolitic.Core;

public struct RelationInfo
{
	public int OtherId;

	public RelationType Relation;

	public bool Adjacent;

	public int SharedReligionId;

	public int SharedLanguageId;

	public int SharedCultureId;

	public int Claims;

	public static RelationInfo Peaceful(int otherId, bool adjacent)
	{
		return new RelationInfo
		{
			OtherId = otherId,
			Relation = RelationType.Peace,
			Adjacent = adjacent
		};
	}
}
