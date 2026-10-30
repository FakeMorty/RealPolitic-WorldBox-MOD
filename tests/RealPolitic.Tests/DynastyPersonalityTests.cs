using System;
using System.Collections.Generic;
using RealPolitic.Core;
using Xunit;

namespace RealPolitic.Tests;

/// <summary>Этап 19: личности правителей, отношения монархов и династические браки.</summary>
public class DynastyPersonalityTests
{
	[Fact]
	public void Roll_IsDeterministicAndWithinRange()
	{
		var a = RulerPersonality.Roll(new Random(150019));
		var b = RulerPersonality.Roll(new Random(150019));
		Assert.Equal(a.Trait, b.Trait);
		Assert.Equal(a.Ambition, b.Ambition);

		for (int seed = 0; seed < 200; seed++)
		{
			var p = RulerPersonality.Roll(new Random(seed));
			Assert.InRange((int)p.Trait, 1, 8);
			Assert.InRange(p.Ambition, 0f, 100f);
		}
	}

	[Fact]
	public void Roll_CanProduceJustTrait()
	{
		// 0.23.13: «1 + rng.Next(0,8)» давал 1..8, и Just (8) не выпадал никогда.
		bool seenJust = false;
		var seen = new HashSet<RulerTrait>();
		for (int seed = 0; seed < 3000; seed++)
		{
			var p = RulerPersonality.Roll(new Random(seed));
			seen.Add(p.Trait);
			if (p.Trait == RulerTrait.Just)
			{
				seenJust = true;
			}
		}
		Assert.True(seenJust, "черта «Справедливый» должна выпадать");
		Assert.Contains(RulerTrait.Diplomat, seen);
		Assert.Contains(RulerTrait.Pious, seen);
		Assert.Contains(RulerTrait.Cruel, seen);
	}

	[Fact]
	public void Traits_ExpenseMultipliers_MatchBalance()
	{
		Assert.Equal(0.9f, RulerTraits.ExpenseMultiplier(RulerTrait.Miser), 3);
		Assert.Equal(1.12f, RulerTraits.ExpenseMultiplier(RulerTrait.Spendthrift), 3);
		Assert.Equal(1f, RulerTraits.ExpenseMultiplier(RulerTrait.Diplomat), 3);
		Assert.Equal(1f, RulerTraits.ExpenseMultiplier(RulerTrait.None), 3);
	}

	[Fact]
	public void Traits_Biases_SpotChecks()
	{
		Assert.Equal(18f, RulerTraits.WarBias(RulerTrait.Conqueror), 3);
		Assert.Equal(-12f, RulerTraits.WarBias(RulerTrait.Just), 3);
		Assert.Equal(16f, RulerTraits.DiplomacyBias(RulerTrait.Diplomat), 3);
		Assert.Equal(-8f, RulerTraits.DiplomacyBias(RulerTrait.Cruel), 3);
		Assert.Equal(15f, RulerTraits.MarriageBias(RulerTrait.Diplomat), 3);
		Assert.Equal(-8f, RulerTraits.MarriageBias(RulerTrait.Cruel), 3);
		// Локализуемый ключ есть у каждой черты.
		Assert.Equal("RP_TRAIT_CONQUEROR", RulerTraits.LocKey(RulerTrait.Conqueror));
		Assert.Equal("RP_TRAIT_NONE", RulerTraits.LocKey(RulerTrait.None));
	}

	[Fact]
	public void TryMarry_Success_CreatesBondsRaisesRelationAndPublishesEvent()
	{
		var world = MarriedWorld(out _, out _);

		var k1 = world.GetKingdom(1);
		var k2 = world.GetKingdom(2);
		Assert.NotNull(k1.Marriage);
		Assert.NotNull(k2.Marriage);
		Assert.Equal(2, k1.Marriage.PartnerId);
		Assert.Equal(1, k2.Marriage.PartnerId);
		Assert.Equal(world.Year, k1.Marriage.Year);
		Assert.Equal(40, world.GetRulerRelation(1, 2));

		var evts = world.Bus.OfType(PolEventType.DynasticMarriage);
		var marriage = Assert.Single(evts);
		Assert.Equal("RP_EV_DYNASTIC_MARRIAGE", marriage.LocKey);
		Assert.Equal(Severity.Major, marriage.Severity);
		Assert.Contains("Alpha", marriage.Get("kingdom"));
		Assert.Contains("Beta", marriage.Get("partner"));
		Assert.Equal("KingA", marriage.Get("ruler"));
		Assert.Equal("QueenB", marriage.Get("spouse"));
	}

	[Fact]
	public void TryMarry_EmpireAndKhaganateUseTheSameDynasticRuleAsTheCandidateSearch()
	{
		var world = new PoliticalWorld { Rng = new Random(1), DynastyRng = new Random(1) };
		world.SetYear(50);
		var empire = world.AddKingdom(1, IdeologyType.Empire, 0);
		var khaganate = world.AddKingdom(2, IdeologyType.Khaganate, 0);
		empire.Ruler = new RulerRef { ActorId = 101, Name = "Emperor", KingdomId = 1, SinceYear = 40, Age = 35 };
		khaganate.Ruler = new RulerRef { ActorId = 201, Name = "Khatun", KingdomId = 2, SinceYear = 40, Age = 33, IsFemale = true };
		world.Params.MarriageBaseChance = 1f;
		world.SetRulerRelation(1, 2, 100);

		Assert.True(world.TryMarry(1, 2, false, out string result), result);
		Assert.Equal(2, empire.Marriage.PartnerId);
		Assert.Equal(1, khaganate.Marriage.PartnerId);
	}

	[Fact]
	public void TryMarry_Guards_RejectInvalidPairs()
	{
		var world = MarriedWorld(out _, out _);
		string result;

		// Оба уже связаны браком.
		Assert.False(world.TryMarry(1, 2, false, out result));
		Assert.False(string.IsNullOrEmpty(result));

		// Брак с самим собой невозможен.
		Assert.False(world.TryMarry(1, 1, false, out result));

		// Война перечёркивает предложение.
		var third = world.AddKingdom(3, IdeologyType.Monarchy, 0);
		third.Ruler = new RulerRef { ActorId = 301, Name = "KingC", KingdomId = 3, SinceYear = 1, Age = 30 };
		var k1 = world.GetKingdom(1);
		k1.Marriage = null;
		world.GetKingdom(2).Marriage = null;
		Assert.False(world.TryMarry(1, 3, true, out result));
		Assert.Contains("войн", result, StringComparison.OrdinalIgnoreCase);

		// Слишком юный правитель.
		third.Ruler.Age = 10;
		Assert.False(world.TryMarry(1, 3, false, out result));
		Assert.Contains("юн", result, StringComparison.OrdinalIgnoreCase);

		// Республика не заключает династических браков.
		third.Ruler.Age = 30;
		third.Ideology = IdeologyType.Democracy;
		Assert.False(world.TryMarry(1, 3, false, out result));
		Assert.Contains("монархи", result, StringComparison.OrdinalIgnoreCase);
	}

	[Fact]
	public void Planner_ProposesMarriage_ForPeacefulMonarchyWithCandidate()
	{
		var plan = KingdomStrategicPlanner.Choose(new KingdomStrategySnapshot
		{
			KingdomId = 1,
			Year = 20,
			Ideology = IdeologyType.Monarchy,
			Population = 100,
			Warriors = 20,
			MaxWarriors = 20,
			Food = 1000f,
			Gold = 500f,
			Stone = 500f,
			Legitimacy = 60f,
			Support = 60f,
			PowerReady = false,
			LastActionYear = int.MinValue,
			Trait = RulerTrait.None,
			Ambition = 50f,
			DynastiesEnabled = true,
			MarriageCandidateId = 2,
			MarriageScore = 80f
		});
		Assert.Equal(KingdomActionType.ProposeMarriage, plan.Action);
		Assert.True(plan.Score > 70f);
	}

	[Fact]
	public void Planner_PersonalityAndAmbitionShiftWarScores()
	{
		var conqueror = KingdomStrategicPlanner.Choose(WarSnapshot(RulerTrait.Conqueror, 90f));
		var just = KingdomStrategicPlanner.Choose(WarSnapshot(RulerTrait.Just, 10f));
		Assert.Equal(KingdomActionType.Mobilize, conqueror.Action);
		Assert.Equal(KingdomActionType.Mobilize, just.Action);
		// Завоеватель с высокими амбициями мобилизуется заметно охотнее справедливого скромника.
		Assert.True(conqueror.Score > just.Score + 30f);
	}

	[Fact]
	public void War_BreaksMarriageBondAndSoursRelations()
	{
		var world = MarriedWorld(out _, out _);
		Assert.Equal(40, world.GetRulerRelation(1, 2));

		world.SetYear(6);
		world.YearTick(new List<KingdomInput>
		{
			KingdomSnapshot(1, 101, "KingA", "ДомА", RelationType.War),
			KingdomSnapshot(2, 201, "QueenB", "ДомБ", RelationType.War)
		});

		Assert.Null(world.GetKingdom(1).Marriage);
		Assert.Null(world.GetKingdom(2).Marriage);
		// Штраф за предательство −40 применяется к паре один раз, плюс годовые военные дрейфы −3×2:
		// +40 − 40 − 3 − 3 = −6.
		Assert.Equal(-6, world.GetRulerRelation(1, 2));
	}

	[Fact]
	public void Succession_MarriageInheritance_SpouseClanTakesThroneWithoutUnion()
	{
		// Претензия 0.55 срабатывает не на каждом зерне — находим подходящее перебором.
		int chosenSeed = -1;
		PoliticalWorld hit = null;
		for (int seed = 0; seed < 3000 && hit == null; seed++)
		{
			var world = MarriedWorld(out _, out _, seed);
			world.SetYear(6);
			world.YearTick(new List<KingdomInput>
			{
				KingdomSnapshot(1, 102, "King2", "Новые", RelationType.Peace),
				KingdomSnapshot(2, 201, "QueenB", "ДомБ", RelationType.Peace)
			});
			if (world.GetKingdom(1).RoyalClanName == "ДомБ")
			{
				chosenSeed = seed;
				hit = world;
			}
		}
		Assert.True(chosenSeed >= 0, "не нашлось зерна, при котором сработала претензия супруга");

		var k1 = hit.GetKingdom(1);
		var k2 = hit.GetKingdom(2);
		Assert.Equal("ДомБ", k1.RoyalClanName);
		Assert.Equal(k2.RoyalClanId, k1.RoyalClanId);
		Assert.Equal(1, k1.DynastyReignsCount);
		Assert.Null(k1.Marriage);
		// Мягкий переход: брак гасит удар по легитимности (−10 вместо −25).
		Assert.True(k1.Legitimacy >= 35f);
		// 0.24: личная уния упразднена — дом супруга просто занимает трон, союза не образуется.
		Assert.Null(k1.Union);
		Assert.Null(k2.Union);
		Assert.Empty(hit.Bus.OfType(PolEventType.DynasticUnionFormed));
	}

	[Fact]
	public void SaveLoad_PersonalityMarriageRelations_Roundtrip()
	{
		var world = MarriedWorld(out _, out _);
		world.SetRulerRelation(1, 2, 77);
		world.GetKingdom(1).Personality = new RulerPersonality { Trait = RulerTrait.Scholar, Ambition = 33f };

		string json = SaveData.Write(world);
		Assert.Contains("\"pers\"", json);
		Assert.Contains("\"mrg\"", json);
		Assert.Contains("\"rels\"", json);

		var restored = new PoliticalWorld();
		SaveData.Apply(restored, json);

		var k1 = restored.GetKingdom(1);
		var k2 = restored.GetKingdom(2);
		Assert.Equal(RulerTrait.Scholar, k1.Personality.Trait);
		Assert.Equal(33f, k1.Personality.Ambition, 3);
		Assert.NotNull(k1.Marriage);
		Assert.Equal(2, k1.Marriage.PartnerId);
		Assert.NotNull(k2.Marriage);
		Assert.Equal(1, k2.Marriage.PartnerId);
		Assert.Equal(77, restored.GetRulerRelation(1, 2));
	}

	[Fact]
	public void OldWorlds_GetPersonalityOnFirstTick_AndV7SavesLoadWithNulls()
	{
		// Мир без личности (старое сохранение): первый тик выдаёт правителю характер.
		var world = new PoliticalWorld();
		world.SetBaseline(true);
		world.SetYear(5);
		world.AddKingdom(1, IdeologyType.Monarchy, 0);
		world.AddCity(10, 1);
		world.YearTick(new List<KingdomInput> { KingdomSnapshot(1, 101, "KingA", "ДомА", RelationType.None) });
		Assert.NotNull(world.GetKingdom(1).Personality);

		// У сохранений до этапа 19 нет секций личности/браков/отношений — загрузка не падает.
		var vintage = new PoliticalWorld();
		vintage.AddKingdom(1, IdeologyType.Monarchy, 0);
		vintage.GetKingdom(1).Personality = null;
		string json = SaveData.Write(vintage);
		Assert.DoesNotContain("\"pers\"", json);
		Assert.DoesNotContain("\"rels\"", json);

		var restored = new PoliticalWorld();
		SaveData.Apply(restored, json);
		Assert.Null(restored.GetKingdom(1).Personality);
		Assert.Equal(0, restored.GetRulerRelation(1, 2));

		// И первый же тик добирает личность.
		restored.SetBaseline(true);
		restored.SetYear(6);
		restored.YearTick(new List<KingdomInput> { KingdomSnapshot(1, 101, "KingA", "ДомА", RelationType.None) });
		Assert.NotNull(restored.GetKingdom(1).Personality);
	}

	// ------------------------------------------------------------------ хелперы

	/// <summary>Две воюющих… нет, две мирные монархии; брак уже заключён (шанс 1.0).</summary>
	private static PoliticalWorld MarriedWorld(out KingdomPolitics k1, out KingdomPolitics k2, int rngSeed = 42)
	{
		var world = new PoliticalWorld();
		world.Rng = new Random(rngSeed);
		world.DynastyRng = new Random(7);
		world.SetBaseline(true);
		world.SetYear(5);
		world.AddKingdom(1, IdeologyType.Monarchy, 0);
		world.AddKingdom(2, IdeologyType.Monarchy, 0);
		world.AddCity(10, 1);
		world.AddCity(20, 2);
		world.YearTick(new List<KingdomInput>
		{
			KingdomSnapshot(1, 101, "KingA", "ДомА", RelationType.Peace, name: "Alpha"),
			KingdomSnapshot(2, 201, "QueenB", "ДомБ", RelationType.Peace, name: "Beta")
		});
		world.Params.MarriageBaseChance = 1f;
		string result;
		Assert.True(world.TryMarry(1, 2, false, out result), result);
		k1 = world.GetKingdom(1);
		k2 = world.GetKingdom(2);
		return world;
	}

	private static KingdomInput KingdomSnapshot(int kingdomId, int rulerActorId, string rulerName, string clan,
		RelationType relation, string name = null)
	{
		var relations = new List<RelationInfo>();
		if (kingdomId == 1)
		{
			relations.Add(new RelationInfo { OtherId = 2, Relation = relation, Adjacent = true });
		}
		else
		{
			relations.Add(new RelationInfo { OtherId = 1, Relation = relation, Adjacent = true });
		}
		return new KingdomInput
		{
			KingdomId = kingdomId,
			Name = name ?? (kingdomId == 1 ? "Alpha" : "Beta"),
			Population = 100,
			Warriors = 5,
			Ruler = new RulerRef
			{
				ActorId = rulerActorId,
				Name = rulerName,
				KingdomId = kingdomId,
				SinceYear = 1,
				Age = 30,
				ClanName = clan,
				ClanId = rulerActorId
			},
			Relations = relations,
			Cities = new List<CityInput>
			{
				new CityInput
				{
					CityId = kingdomId * 10,
					KingdomId = kingdomId,
					Population = 100,
					FoodOk = true,
					CultureId = 7,
					CultureName = "C",
					ReligionId = 3,
					ReligionName = "R",
					LanguageId = 2,
					LanguageName = "L"
				}
			}
		};
	}

	private static KingdomStrategySnapshot WarSnapshot(RulerTrait trait, float ambition)
	{
		return new KingdomStrategySnapshot
		{
			KingdomId = 1,
			Year = 20,
			Ideology = IdeologyType.Monarchy,
			Population = 100,
			Warriors = 60,
			MaxWarriors = 100,
			Food = 1000f,
			Gold = 500f,
			Stone = 500f,
			Legitimacy = 60f,
			Support = 60f,
			AtWar = true,
			PowerReady = false,
			LastActionYear = int.MinValue,
			Trait = trait,
			Ambition = ambition
		};
	}
}
