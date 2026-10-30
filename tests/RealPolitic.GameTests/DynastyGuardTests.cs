using System.Collections.Generic;
using RealPolitic.Core;
using Xunit;

namespace RealPolitic.Tests;

/// <summary>
/// Регрессия 0.17.3 («семья и клан сопряжены?»): если игра после смерти монарха
/// ставит короля со стороны, мод возвращает власть родственнику старого короля
/// (ребёнку, иначе совершеннолетнему соклановцу) через Kingdom.setKing.
/// </summary>
public class DynastyGuardTests
{
	private static Actor MakeActor(long id, int age, long clanId = 0, long parent1 = 0)
	{
		return new Actor
		{
			id = id,
			age = age,
			data = new ActorData
			{
				id = id,
				clan = clanId != 0 ? new Clan { id = clanId, name = "Королевский род" } : null,
				parent_id_1 = parent1
			}
		};
	}

	[Fact]
	public void Guard_InstallsChild_WhenGamePicksOutsider()
	{
		Kingdom kingdom = new Kingdom { id = 1 };
		Actor child = MakeActor(51, 30, clanId: 7, parent1: 50);
		Actor outsider = MakeActor(52, 40, clanId: 9);
		kingdom.units = new List<Actor> { child, outsider };
		var state = new KingdomPolitics { KingdomId = 1, Ideology = IdeologyType.Monarchy, RoyalClanId = 7 };

		Actor heir = GameRefs.GuardDynasty(kingdom, state, outsider, 50);

		Assert.NotNull(heir);
		Assert.Same(child, heir);
		Assert.Same(child, kingdom.king); // setKing реально вызван
	}

	[Fact]
	public void Guard_PrefersEldestChild_OverClansman()
	{
		Kingdom kingdom = new Kingdom { id = 1 };
		Actor youngChild = MakeActor(51, 18, clanId: 7, parent1: 50);
		Actor elderChild = MakeActor(53, 34, clanId: 7, parent1: 50);
		Actor clansman = MakeActor(54, 50, clanId: 7);
		Actor outsider = MakeActor(52, 40, clanId: 9);
		kingdom.units = new List<Actor> { youngChild, elderChild, clansman, outsider };
		var state = new KingdomPolitics { KingdomId = 1, Ideology = IdeologyType.Monarchy, RoyalClanId = 7 };

		Actor heir = GameRefs.GuardDynasty(kingdom, state, outsider, 50);

		Assert.Same(elderChild, heir);
	}

	[Fact]
	public void Guard_LeavesKing_WhenNewKingIsClansman()
	{
		Kingdom kingdom = new Kingdom { id = 1 };
		Actor clansmanKing = MakeActor(52, 40, clanId: 7);
		kingdom.units = new List<Actor> { clansmanKing };
		var state = new KingdomPolitics { KingdomId = 1, Ideology = IdeologyType.Monarchy, RoyalClanId = 7 };

		Actor heir = GameRefs.GuardDynasty(kingdom, state, clansmanKing, 50);

		Assert.Null(heir);
		Assert.Null(kingdom.king); // власть не тронута
	}

	[Fact]
	public void Guard_NoAdultRelatives_OutsideStays()
	{
		Kingdom kingdom = new Kingdom { id = 1 };
		Actor minorChild = MakeActor(51, 6, clanId: 7, parent1: 50); // малолетний — не кандидат
		Actor outsider = MakeActor(52, 40, clanId: 9);
		kingdom.units = new List<Actor> { minorChild, outsider };
		var state = new KingdomPolitics { KingdomId = 1, Ideology = IdeologyType.Monarchy, RoyalClanId = 7 };

		Actor heir = GameRefs.GuardDynasty(kingdom, state, outsider, 50);

		Assert.Null(heir);
		Assert.Null(kingdom.king);
	}

	[Fact]
	public void Guard_NotMonarchy_NoIntervention()
	{
		Kingdom kingdom = new Kingdom { id = 1 };
		Actor child = MakeActor(51, 30, clanId: 7, parent1: 50);
		Actor outsider = MakeActor(52, 40, clanId: 9);
		kingdom.units = new List<Actor> { child, outsider };
		var state = new KingdomPolitics { KingdomId = 1, Ideology = IdeologyType.Democracy, RoyalClanId = 7 };

		Assert.Null(GameRefs.GuardDynasty(kingdom, state, outsider, 50));
		Assert.Null(kingdom.king);
	}
}
