using System.Collections.Generic;
using RealPolitic.Core;
using Xunit;

namespace RealPolitic.Tests;

public class UnionTests
{
	private static DiplomatSnapshot Member(int id, IdeologyType ideology, int warriors = 5, int religionId = 3)
	{
		return new DiplomatSnapshot
		{
			KingdomId = id,
			Ideology = ideology,
			Warriors = warriors,
			ReligionId = religionId,
			Population = 50,
			Legitimacy = 60,
			AdjacentToLeader = true,
			LanguageId = 2
		};
	}

	[Fact]
	public void VassalUnion_FormsWhenLeaderStrongerAndLegitimate()
	{
		var world = NewWorld(1, 2, IdeologyType.Monarchy, IdeologyType.Monarchy);
		world.GetKingdom(1).Legitimacy = 70f;

		Union union = world.TryCreateUnion(UnionType.Vassal, 1, 2, "Тест-уния", Member(2, IdeologyType.Monarchy), out var check, 3, 100);

		Assert.NotNull(union);
		Assert.Equal(2, union.MemberCount);
		Assert.Same(union, world.UnionOf(2));
		Assert.Contains(world.Bus.OfType(PolEventType.UnionFormed), e => e.Get("name") == "Тест-уния");
	}

	[Fact]
	public void TooStrongKingdom_CannotBeVassal()
	{
		var world = NewWorld(1, 2, IdeologyType.Monarchy, IdeologyType.Monarchy);
		world.GetKingdom(1).Legitimacy = 70f;

		Union union = world.TryCreateUnion(UnionType.Vassal, 1, 2, "X", Member(2, IdeologyType.Monarchy, warriors: 200), out var check, 3, 100);

		Assert.Null(union);
		Assert.False(check.Ok);
	}

	[Fact]
	public void Merger_BlockedByIdeologyGap()
	{
		var world = NewWorld(1, 2, IdeologyType.Monarchy, IdeologyType.Egalitarianism);
		world.GetKingdom(1).Legitimacy = 70f;

		Union union = world.TryCreateUnion(UnionType.Merger, 1, 2, "Соединение", Member(2, IdeologyType.Egalitarianism), out var check, 3, 100);

		Assert.Null(union);
		Assert.False(check.Ok);
		Assert.Equal("RP_UNION_IDEOLOGY_GAP", check.ReasonKey);
	}

	[Fact]
	public void UnionMembership_IsAttachedToKingdomDuringTick()
	{
		var world = NewWorld(1, 2, IdeologyType.Monarchy, IdeologyType.Monarchy);
		world.GetKingdom(1).Legitimacy = 70f;
		world.TryCreateUnion(UnionType.Vassal, 1, 2, "U", Member(2, IdeologyType.Monarchy), out _, 3, 100);

		world.SetYear(world.Year + 1);
		world.YearTick(new List<KingdomInput> { Snap(1, "A"), Snap(2, "B") });

		Assert.NotNull(world.GetKingdom(2).Union);
	}

	[Fact]
	public void UnionTypeKey_MapsAllEnumValues()
	{
		Assert.Equal("RP_UNIONTYPE_VASSAL", Ideologies.UnionTypeKey(UnionType.Vassal));
		Assert.Equal("RP_UNIONTYPE_MERGER", Ideologies.UnionTypeKey(UnionType.Merger));
	}

	[Fact]
	public void OnlyMonarchiesAndTribesFormUnions_OthersFormFederations()
	{
		// 1. Монархия + Племя (или Монархия + Монархия) могут объединиться в УНИЮ:
		var worldUnion = NewWorld(1, 2, IdeologyType.Monarchy, IdeologyType.Tribalism);
		worldUnion.UnionRules.MinAffinity = -1f;
		worldUnion.GetKingdom(1).Legitimacy = 75f;
		worldUnion.GetKingdom(2).Legitimacy = 70f;
		Union? monarchTribeUnion = worldUnion.TryCreateUnion(UnionType.Merger, 1, 2, "Коронная Уния", Member(2, IdeologyType.Tribalism), out var check1);
		Assert.NotNull(monarchTribeUnion);
		Assert.True(check1.Ok);
		Assert.False(monarchTribeUnion!.IsFederation);
		var evtUnion = Assert.Single(worldUnion.Bus.OfType(PolEventType.UnionMerged));
		Assert.Equal("RP_EV_UNION_MERGED", evtUnion.LocKey);
		Assert.Equal("union", evtUnion.Get("union_kind"));

		// 2. Монархия/Племя НЕ может объединиться с Республикой/Теократией/Демократией:
		var worldMismatch = NewWorld(1, 2, IdeologyType.Monarchy, IdeologyType.Theocracy);
		worldMismatch.UnionRules.MinAffinity = -1f;
		worldMismatch.GetKingdom(1).Legitimacy = 75f;
		worldMismatch.GetKingdom(2).Legitimacy = 70f;
		Union? mismatch = worldMismatch.TryCreateUnion(UnionType.Merger, 1, 2, "Невозможный союз", Member(2, IdeologyType.Theocracy), out var check2);
		Assert.Null(mismatch);
		Assert.False(check2.Ok);
		Assert.Equal("RP_UNION_FORM_MISMATCH", check2.ReasonKey);

		// 3. Все остальные идеологии (республики, демократии и т.д.) объединяются в ФЕДЕРАЦИИ:
		var worldFed = NewWorld(1, 2, IdeologyType.MerchantRepublic, IdeologyType.Democracy);
		worldFed.UnionRules.MinAffinity = -1f;
		worldFed.GetKingdom(1).Legitimacy = 75f;
		worldFed.GetKingdom(2).Legitimacy = 70f;
		Union? federation = worldFed.TryCreateUnion(UnionType.Merger, 1, 2, "Вольная Федерация", Member(2, IdeologyType.Democracy), out var check3);
		Assert.NotNull(federation);
		Assert.True(check3.Ok);
		Assert.True(federation!.IsFederation);
		var evtFed = Assert.Single(worldFed.Bus.OfType(PolEventType.UnionMerged));
		Assert.Equal("RP_EV_FEDERATION_MERGED", evtFed.LocKey);
		Assert.Equal("federation", evtFed.Get("union_kind"));

		// 4. Каганаты и Империи также объединяются в УНИИ:
		var worldEmpKhag = NewWorld(1, 2, IdeologyType.Empire, IdeologyType.Khaganate);
		worldEmpKhag.UnionRules.MinAffinity = -1f;
		worldEmpKhag.GetKingdom(1).Legitimacy = 80f;
		worldEmpKhag.GetKingdom(2).Legitimacy = 75f;
		Union? empKhagUnion = worldEmpKhag.TryCreateUnion(UnionType.Merger, 1, 2, "Имперский Союз", Member(2, IdeologyType.Khaganate), out var check4);
		Assert.NotNull(empKhagUnion);
		Assert.True(check4.Ok);
		Assert.False(empKhagUnion!.IsFederation);
		Assert.False(empKhagUnion.IsConfederation);

		// 5. Вольные территории объединяются ТОЛЬКО с другими вольными территориями в КОНФЕДЕРАЦИЮ ВОЛЬНЫХ ТЕРРИТОРИЙ:
		var worldConfedMismatch = NewWorld(1, 2, IdeologyType.FreeTerritory, IdeologyType.Egalitarianism);
		worldConfedMismatch.UnionRules.MinAffinity = -1f;
		worldConfedMismatch.GetKingdom(1).Legitimacy = 75f;
		worldConfedMismatch.GetKingdom(2).Legitimacy = 70f;
		Union? confedMismatch = worldConfedMismatch.TryCreateUnion(UnionType.Merger, 1, 2, "Невозможная конфедерация", Member(2, IdeologyType.Egalitarianism), out var check5);
		Assert.Null(confedMismatch);
		Assert.False(check5.Ok);
		Assert.Equal("RP_UNION_FORM_MISMATCH", check5.ReasonKey);

		var worldConfed = NewWorld(1, 2, IdeologyType.FreeTerritory, IdeologyType.FreeTerritory);
		worldConfed.UnionRules.MinAffinity = -1f;
		worldConfed.GetKingdom(1).Legitimacy = 75f;
		worldConfed.GetKingdom(2).Legitimacy = 70f;
		Union? confed = worldConfed.TryCreateUnion(UnionType.Merger, 1, 2, "Вольноград–Яснополье", Member(2, IdeologyType.FreeTerritory), out var check6);
		Assert.NotNull(confed);
		Assert.True(check6.Ok);
		Assert.True(confed!.IsConfederation);
		Assert.False(confed.IsFederation);
		var evtConfed = Assert.Single(worldConfed.Bus.OfType(PolEventType.UnionMerged));
		Assert.Equal("RP_EV_CONFEDERATION_MERGED", evtConfed.LocKey);
		Assert.Equal("confederation", evtConfed.Get("union_kind"));
	}

	[Fact]
	public void FreeTerritory_CanBeVassalized_ButHatesItAndRebelsQuickly()
	{
		var world = NewWorld(1, 2, IdeologyType.Empire, IdeologyType.FreeTerritory);
		world.UnionRules.MinAffinity = -1f;
		world.GetKingdom(1).Legitimacy = 80f;
		world.GetKingdom(2).Legitimacy = 65f;

		Union? vassalUnion = world.TryCreateUnion(UnionType.Vassal, 1, 2, "Вассалитет", Member(2, IdeologyType.FreeTerritory, warriors: 5), out var check, leaderWarriors: 50);
		Assert.NotNull(vassalUnion);
		Assert.True(check.Ok);

		UnionMember? ftMember = vassalUnion!.GetMember(2);
		Assert.NotNull(ftMember);
		float startApproval = ftMember!.Approval;

		// За 3 года вассалитета одобрение вольной территории стремительно падает и она выходит из вассалитета:
		var rng = new System.Random(7);
		List<int> rebelled = new List<int>();
		for (int y = 0; y < 4; y++)
		{
			List<int> left = Unions.Tick(vassalUnion, id => world.GetKingdom(id), world.UnionRules, rng);
			rebelled.AddRange(left);
			if (rebelled.Contains(2))
			{
				break;
			}
		}

		Assert.Contains(2, rebelled);
		Assert.True(ftMember.Approval < startApproval - 0.25f);
	}

	private static PoliticalWorld NewWorld(int id1, int id2, IdeologyType i1, IdeologyType i2)
	{
		var world = new PoliticalWorld();
		world.SetBaseline(true);
		world.SetYear(10);
		world.AddKingdom(id1, i1, 0);
		world.AddKingdom(id2, i2, 0);
		world.AddCity(id1 * 10, id1);
		world.AddCity(id2 * 10, id2);
		return world;
	}

	private static KingdomInput Snap(int id, string name)
	{
		return new KingdomInput
		{
			KingdomId = id,
			Name = name,
			Population = 100,
			Warriors = 5,
			Cities = new List<CityInput>
			{
				new CityInput { CityId = id * 10, KingdomId = id, Population = 100, FoodOk = true, CultureId = 7, CultureName = "C", ReligionId = 3, ReligionName = "R", LanguageId = 2, LanguageName = "L" }
			}
		};
	}
}
