using System.Collections.Generic;
using RealPolitic.Core;
using Xunit;

namespace RealPolitic.Tests;

/// <summary>Рост уний и торговый бонус по типу унии (0.24: вассалитет и соединение).</summary>
public class UnionGrowthTests
{
	[Fact]
	public void ThirdKingdom_CanJoinExistingUnion()
	{
		PoliticalWorld world = NewWorld(1, 2, 3);
		Union union = world.TryCreateUnion(UnionType.Vassal, 1, 2, "Тройственный союз", Snap(2), out _, 3, 100)!;
		Assert.Equal(2, union.MemberCount);

		Union joined = world.TryJoinUnion(union, 3, Snap(3), out Unions.JoinCheck check, 3);

		Assert.NotNull(joined);
		Assert.True(check.Ok, check.ReasonDetail);
		Assert.Equal(3, union.MemberCount);
		Assert.Same(union, world.UnionOf(3));
		Assert.Contains(world.Bus.OfType(PolEventType.UnionMemberJoined), e => e.Get("count") == "3");
	}

	[Fact]
	public void JoinUnion_RejectsMemberBeyondLimit()
	{
		PoliticalWorld world = NewWorld(1, 2, 3);
		world.UnionRules.MaxMembers = 2;
		Union union = world.TryCreateUnion(UnionType.Vassal, 1, 2, "V", Snap(2), out _, 3, 100)!;

		Union joined = world.TryJoinUnion(union, 3, Snap(3), out Unions.JoinCheck check, 3);

		Assert.Null(joined);
		Assert.Equal("RP_UNION_TOO_LARGE", check.ReasonKey);
		Assert.Equal(2, union.MemberCount);
	}

	[Fact]
	public void JoinUnion_RejectsKingdomAlreadyInAnotherUnion()
	{
		PoliticalWorld world = NewWorld(1, 2, 3, 4);
		Union first = world.TryCreateUnion(UnionType.Vassal, 1, 2, "Первая", Snap(2), out _, 3, 100)!;
		Union second = world.TryCreateUnion(UnionType.Vassal, 3, 4, "Вторая", Snap(4), out _, 3, 100)!;

		Union joined = world.TryJoinUnion(first, 4, Snap(4), out Unions.JoinCheck check, 3);

		Assert.Null(joined);
		Assert.Equal("RP_UNION_ALREADY_MEMBER", check.ReasonKey);
		Assert.Same(second, world.UnionOf(4));
	}

	[Fact]
	public void JoinUnion_RequiresLegitimateLeader()
	{
		PoliticalWorld world = NewWorld(1, 2, 3);
		Union union = world.TryCreateUnion(UnionType.Vassal, 1, 2, "V", Snap(2), out _, 3, 100)!;
		world.GetKingdom(1).Legitimacy = 20f;

		Union joined = world.TryJoinUnion(union, 3, Snap(3), out Unions.JoinCheck check, 3);

		Assert.Null(joined);
		Assert.Equal("RP_UNION_LEADER_WEAK", check.ReasonKey);
	}

	[Fact]
	public void UnionTradeBonus_ReachesTradeIncome()
	{
		var p = new SimParams();
		var network = new TradeNetwork();
		TradeRoute route = network.Open(1, 2, 1)!;
		route.Volume = 20f;
		var eco = new EconomyState();
		EconomyInput input = Input(Partner(2, 1f));

		float vassal = Economy.TradeIncome(State(1), eco, input, Partners(Partner(2, Unions.TradeModifier(UnionType.Vassal))), network, p);
		float merger = Economy.TradeIncome(State(1), eco, input, Partners(Partner(2, Unions.TradeModifier(UnionType.Merger))), network, p);

		Assert.Equal(1.1f, Unions.TradeModifier(UnionType.Vassal), 3);
		Assert.Equal(1.25f, Unions.TradeModifier(UnionType.Merger), 3);
		Assert.True(merger > vassal, "соединение торгует лучше вассалитета");
	}

	[Fact]
	public void TradeMultiplier_FallsBackToAllianceBonusWhenUnset()
	{
		var p = new SimParams();
		var network = new TradeNetwork();
		TradeRoute route = network.Open(1, 2, 1)!;
		route.Volume = 20f;
		EconomyInput input = Input(Partner(2, 1f));

		// Множитель не задан (ноль) — работает прежний бонус союза: старые снимки остаются рабочими.
		float legacyAllied = Economy.TradeIncome(State(1), new EconomyState(), input, Partners(Partner(2, 0f, allied: true)), network, p);
		float explicitAllied = Economy.TradeIncome(State(1), new EconomyState(), input, Partners(Partner(2, 1f + p.AlliedTradeBonus)), network, p);
		float plain = Economy.TradeIncome(State(1), new EconomyState(), input, Partners(Partner(2, 0f)), network, p);

		Assert.Equal(explicitAllied, legacyAllied, 3);
		Assert.True(legacyAllied > plain, "союзник торгует лучше постороннего");
	}

	[Fact]
	public void VassalPartner_BringsMoreTradeThanStranger()
	{
		// Сквозная проверка связки: мир сам подставляет множитель по типу унии в снимок экономики.
		// Вассалитет оставляет партнёра живым участником — только у живой доли есть торговый бонус
		// (поглощённый в соединение уже не отдельная держава).
		EconomyReport? withUnion = TradeIncomeAfterTick(union: true);
		EconomyReport? withoutUnion = TradeIncomeAfterTick(union: false);

		Assert.NotNull(withUnion);
		Assert.NotNull(withoutUnion);
		Assert.True(withUnion.TradeIncome > withoutUnion.TradeIncome,
			"вассал должен приносить больше торгового дохода: " + withUnion.TradeIncome + " против " + withoutUnion.TradeIncome);
	}

	private static EconomyReport? TradeIncomeAfterTick(bool union)
	{
		PoliticalWorld world = new PoliticalWorld();
		world.SetBaseline(true);
		world.SetYear(10);
		world.Rng = new System.Random(7);
		world.AddKingdom(1, IdeologyType.Monarchy, 0);
		world.AddKingdom(2, IdeologyType.Monarchy, 0);
		world.AddCity(11, 1);
		world.AddCity(22, 2);
		world.GetKingdom(1).Legitimacy = 80f;
		world.GetKingdom(2).Legitimacy = 80f;

		if (union)
		{
			world.TryCreateUnion(UnionType.Vassal, 1, 2, "Вассалитет", Snap(2), out _, 3, 100);
		}

		TradeRoute route = world.Trade.Open(1, 2, 10)!;
		route.Volume = 20f;

		world.SetYear(11);
		world.YearTick(new List<KingdomInput> { Neighbor(1), Neighbor(2) });
		return world.LastEconomyReport(1);
	}

	// ------------------------------------------------------------------ хелперы

	private static PoliticalWorld NewWorld(params int[] ids)
	{
		var world = new PoliticalWorld();
		world.SetBaseline(true);
		world.SetYear(10);
		foreach (int id in ids)
		{
			KingdomPolitics k = world.AddKingdom(id, IdeologyType.Monarchy, 0);
			k.Legitimacy = 70f;
			world.AddCity(id * 10, id);
		}
		return world;
	}

	private static DiplomatSnapshot Snap(int id, int warriors = 5)
	{
		return new DiplomatSnapshot
		{
			KingdomId = id,
			Ideology = IdeologyType.Monarchy,
			Warriors = warriors,
			ReligionId = 3,
			Population = 50,
			Legitimacy = 60,
			AdjacentToLeader = true,
			LanguageId = 2
		};
	}

	/// <summary>Держава с единственным соседом: так снимок экономики получает партнёра.</summary>
	private static KingdomInput Neighbor(int id)
	{
		int other = id == 1 ? 2 : 1;
		return new KingdomInput
		{
			KingdomId = id,
			Name = "Держава " + id,
			Population = 400,
			Warriors = 10,
			Cities = FourCities(id),
			Relations = new List<RelationInfo>
			{
				new RelationInfo { OtherId = other, Relation = RelationType.Peace, Adjacent = true }
			}
		};
	}

	private static List<CityInput> FourCities(int id)
	{
		var cities = new List<CityInput>();
		for (int i = 0; i < 4; i++)
		{
			cities.Add(new CityInput { CityId = id * 10 + i, KingdomId = id, Population = 100, FoodOk = true });
		}
		return cities;
	}

	private static KingdomPolitics State(int id)
	{
		var k = new KingdomPolitics { KingdomId = id, Ideology = IdeologyType.Monarchy };
		return k;
	}

	private static EconomyPartner Partner(int id, float multiplier, bool allied = false)
	{
		return new EconomyPartner
		{
			KingdomId = id,
			Name = "Партнёр " + id,
			Adjacent = true,
			Cities = 3,
			Allied = allied,
			TradeMultiplier = multiplier
		};
	}

	private static Dictionary<int, EconomyPartner> Partners(EconomyPartner partner)
	{
		return new Dictionary<int, EconomyPartner> { { partner.KingdomId, partner } };
	}

	private static EconomyInput Input(EconomyPartner partner)
	{
		return new EconomyInput
		{
			KingdomId = 1,
			Name = "Тир",
			Cities = 3,
			Population = 300,
			Warriors = 10,
			Legitimacy = 60f,
			Ideology = IdeologyType.Monarchy,
			Partners = new List<EconomyPartner> { partner }
		};
	}
}
