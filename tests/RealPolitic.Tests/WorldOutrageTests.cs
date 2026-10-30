using System;
using System.Collections.Generic;
using System.Linq;
using RealPolitic.Core;
using Xunit;

namespace RealPolitic.Tests;

/// <summary>
/// 0.50: мир возмущается и наказывает.
/// Заказ владельца: «санкции, КМУД, потеря статуса великой державы».
/// </summary>
public sealed class WorldOutrageTests
{
	private static KingdomInput Input(int kingdomId, params int[] cityIds)
	{
		KingdomInput input = new KingdomInput
		{
			KingdomId = kingdomId,
			Name = "Держава" + kingdomId,
			Population = 800,
			Warriors = 60,
			Ruler = new RulerRef { ActorId = kingdomId * 100, Name = "Вождь", KingdomId = kingdomId, SinceYear = 1, Age = 40 },
			Cities = new List<CityInput>()
		};
		for (int i = 0; i < cityIds.Length; i++)
		{
			input.Cities.Add(new CityInput { CityId = cityIds[i], KingdomId = kingdomId, Population = 100, FoodOk = true });
		}
		return input;
	}

	[Fact]
	public void QuietCoupIsForgivenButSprawlingOneIsNot()
	{
		Assert.Equal(OutrageResponse.None, OutrageEngine.Response(20f, false, false));
		Assert.Equal(OutrageResponse.Sanctions, OutrageEngine.Response(40f, false, false));
		Assert.Equal(OutrageResponse.SanctionsAndStatus, OutrageEngine.Response(70f, false, false));
		Assert.Equal(OutrageResponse.SanctionsStatusAndKmud, OutrageEngine.Response(70f, false, true));
	}

	[Fact]
	public void TakingCitizenshipIsAlwaysPunishedHarsherAndLonger()
	{
		Assert.Equal(OutrageResponse.SanctionsAndStatus, OutrageEngine.Response(20f, true, false));
		Assert.Equal(OutrageEngine.HarshSanctionYears, OutrageEngine.SanctionFor(OutrageResponse.SanctionsAndStatus, true));
		Assert.Equal(OutrageEngine.SanctionYears, OutrageEngine.SanctionFor(OutrageResponse.SanctionsAndStatus, false));
		Assert.True(OutrageEngine.RelationHitFor(OutrageResponse.Sanctions, true) > OutrageEngine.RelationHitFor(OutrageResponse.Sanctions, false));
	}

	[Fact]
	public void OnlyHeavyDeedsCostGreatPowerStatus()
	{
		Assert.False(OutrageEngine.LosesGreatPower(OutrageResponse.Sanctions));
		Assert.True(OutrageEngine.LosesGreatPower(OutrageResponse.SanctionsAndStatus));
		Assert.False(OutrageEngine.GoesToKmud(OutrageResponse.SanctionsAndStatus));
		Assert.True(OutrageEngine.GoesToKmud(OutrageResponse.SanctionsStatusAndKmud));
	}

	[Fact]
	public void WorldAnswersWithSanctionsAndRuinedRelations()
	{
		var world = new PoliticalWorld();
		world.SetYear(120);
		world.AddKingdom(1, IdeologyType.Monarchy, 10);
		world.AddKingdom(2, IdeologyType.Democracy, 10);
		world.AddKingdom(3, IdeologyType.Monarchy, 10);

		OutrageResponse response = world.PunishOutrage(1, 50f, false, "RP50_REASON_NATIONALISM");

		Assert.Equal(OutrageResponse.Sanctions, response);
		Assert.Single(world.Kmud.Sanctions);
		Assert.Equal(1, world.Kmud.Sanctions[0].KingdomId);
		Assert.Equal(140, world.Kmud.Sanctions[0].UntilYear);
		// Отношения испорчены со всеми соседями.
		Assert.True(world.GetRulerRelation(1, 2) < 0);
		Assert.True(world.GetRulerRelation(1, 3) < 0);
		Assert.Contains(world.Bus.OfType(PolEventType.WorldOutrageSanctions), e => e.KingdomId == 1);
	}

	[Fact]
	public void SecondOutrageExtendsTheSameSanctionsInsteadOfPilingUp()
	{
		var world = new PoliticalWorld();
		world.SetYear(120);
		world.AddKingdom(1, IdeologyType.Monarchy, 10);
		world.AddKingdom(2, IdeologyType.Democracy, 10);

		world.PunishOutrage(1, 50f, false, "RP50_REASON_NATIONALISM");
		world.SetYear(130);
		world.PunishOutrage(1, 50f, false, "RP50_REASON_NATIONALISM");

		Assert.Single(world.Kmud.Sanctions);
		Assert.Equal(150, world.Kmud.Sanctions[0].UntilYear);
	}

	[Fact]
	public void RevokedCitizenshipLeavesAStainForTheNextCongress()
	{
		var world = new PoliticalWorld();
		world.SetYear(120);
		world.AddKingdom(1, IdeologyType.Monarchy, 10);
		world.AddKingdom(2, IdeologyType.Democracy, 10);

		world.PunishOutrage(1, 50f, true, "RP50_REASON_RACE");

		Assert.True(world.HasFreshHateStain(1, 120));
		// Помнят пятьдесят лет, но не вечно.
		Assert.True(world.HasFreshHateStain(1, 165));
		Assert.False(world.HasFreshHateStain(1, 175));
		Assert.False(world.HasFreshHateStain(2, 120));
	}

	[Fact]
	public void OutrageWithoutAnyKingdomIsIgnored()
	{
		var world = new PoliticalWorld();
		world.SetYear(120);
		Assert.Equal(OutrageResponse.None, world.PunishOutrage(0, 100f, true, "RP50_REASON_RACE"));
		Assert.Empty(world.Kmud.Sanctions);
	}

	[Fact]
	public void CitizenshipHateWeighsInTheMoralTrial()
	{
		KmudMisdeed quiet = KmudMorality.Record(1, 0, false, false);
		KmudMisdeed hate = KmudMorality.Record(2, 0, false, true);
		Assert.True(hate.Score > quiet.Score);
		Assert.Equal(2, KmudMorality.Worst(new List<KmudMisdeed> { quiet, hate }));
		Assert.Contains(hate.Reasons(), r => r.Contains("гражданство"));
	}

	[Fact]
	public void NationalistCoupInTheWorldTurnsTheWorldAgainstIt()
	{
		var world = new PoliticalWorld();
		world.Rng = new Random(7);
		world.SetBaseline(true);
		world.SetYear(100);
		KingdomPolitics kingdom = world.AddKingdom(1, IdeologyType.Monarchy, 20);
		world.AddKingdom(2, IdeologyType.Democracy, 80);
		world.AddCity(200, 1);
		world.AddCity(300, 1);
		world.AddCity(400, 2);
		world.CityHomelands.Restore(new CityHomelandRecord { CityId = 200, OriginalKingdomId = 9, OriginalKingdomName = "Родина", OriginalIdeology = IdeologyType.Democracy, AnnexedYear = 98 });
		world.CityHomelands.Restore(new CityHomelandRecord { CityId = 300, OriginalKingdomId = 9, OriginalKingdomName = "Родина", OriginalIdeology = IdeologyType.Democracy, AnnexedYear = 98 });
		world.NationalismOf(1).Pressure = 80f;

		world.YearTick(new List<KingdomInput> { Input(1, 200, 300), Input(2, 400) });

		Assert.Equal(IdeologyType.Nationalism, kingdom.Ideology);
		Assert.Contains(world.Kmud.Sanctions, s => s.KingdomId == 1);
		Assert.True(world.GetRulerRelation(1, 2) < 0);
	}
}
