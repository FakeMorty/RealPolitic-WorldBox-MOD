using RealPolitic.Core;
using Xunit;

namespace RealPolitic.GameTests;

public sealed class AllianceBalanceTests
{
	[Theory]
	// 0.47.1: две последние державы в один союз не входят — иначе войн больше не будет.
	[InlineData(1, 0)]
	[InlineData(2, 0)]
	[InlineData(3, 2)]
	[InlineData(5, 4)]
	[InlineData(6, 4)]
	[InlineData(10, 4)]
	[InlineData(20, 5)]
	public void NativeAllianceCapNeverLetsOneBlocContainEveryCountry(int total, int expected)
	{
		Assert.Equal(expected, AllianceBalance.MaxMembers(total));
		Assert.True(AllianceBalance.MaxMembers(total) < total);
	}

	[Fact]
	public void OversizedAllianceChoosesTheMostAlienAndResentedNonLeaderForExpulsionVote()
	{
		var world = new PoliticalWorld();
		for (int id = 1; id <= 4; id++)
		{
			KingdomPolitics state = world.AddKingdom(id, IdeologyType.Monarchy, 0);
			state.Census[IdeologyType.Monarchy] = 100;
		}
		KingdomPolitics outsider = world.AddKingdom(5, IdeologyType.Egalitarianism, 0);
		outsider.Census[IdeologyType.Egalitarianism] = 25;
		world.AddGrievance(2, 5, GrievanceReason.War, 90f);

		var record = new AllianceRecord { Id = 77, Name = "СДС", LeaderId = 1 };
		record.Members.AddRange(new[] { 1, 2, 3, 4, 5 });

		Assert.True(AllianceBalance.TryChooseExpulsion(world, record, out int proposer, out int subject));
		Assert.Equal(5, subject);
		Assert.Equal(2, proposer);
	}
}
