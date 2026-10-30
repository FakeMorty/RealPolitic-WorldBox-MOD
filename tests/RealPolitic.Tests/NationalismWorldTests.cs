using System;
using System.Collections.Generic;
using System.Linq;
using RealPolitic.Core;
using Xunit;

namespace RealPolitic.Tests;

/// <summary>
/// 0.49: национализм в живом мире — от обид бывших земель до возврата строя.
/// </summary>
public sealed class NationalismWorldTests
{
	private static KingdomInput Input(int kingdomId, string name)
	{
		return new KingdomInput
		{
			KingdomId = kingdomId,
			Name = name,
			Population = 1200,
			Warriors = 90,
			Ruler = new RulerRef { ActorId = kingdomId * 100, Name = "Вождь " + kingdomId, KingdomId = kingdomId, SinceYear = 1, Age = 40 },
			Cities = new List<CityInput>()
		};
	}

	/// <summary>Города державы #1, чтобы мир не счёл их пропавшими.</summary>
	private static KingdomInput InputWithCities(int kingdomId, params int[] cityIds)
	{
		KingdomInput input = Input(kingdomId, "Держава");
		for (int i = 0; i < cityIds.Length; i++)
		{
			input.Cities.Add(new CityInput { CityId = cityIds[i], KingdomId = kingdomId, Population = 100, FoodOk = true });
		}
		return input;
	}

	private static readonly int[] AllWinnerCities = { 200, 201, 202, 203, 204, 205, 300, 301 };

	private static PoliticalWorld WorldWithConqueredHomeland(out KingdomPolitics winner)
	{
		var world = new PoliticalWorld();
		world.Rng = new Random(11);
		world.SetBaseline(true);
		world.SetYear(100);
		winner = world.AddKingdom(1, IdeologyType.Monarchy, 20);
		world.AddKingdom(2, IdeologyType.Democracy, 80);
		for (int i = 0; i < 6; i++)
		{
			world.AddCity(200 + i, 1);
			world.CityHomelands.Restore(new CityHomelandRecord { CityId = 200 + i, OriginalKingdomId = 2, OriginalKingdomName = "Родина", OriginalIdeology = IdeologyType.Democracy, AnnexedYear = 148 });
		}
		world.AddCity(300, 1);
		world.AddCity(301, 1);
		// Города 300/301 когда-то были отдельной державой «Родина» (запись перебиваем:
		// при AddCity книга уже записала нынешнего хозяина).
		world.CityHomelands.Restore(new CityHomelandRecord { CityId = 300, OriginalKingdomId = 2, OriginalKingdomName = "Родина", OriginalIdeology = IdeologyType.Democracy, WasHistoricalCapital = true, AnnexedYear = 148 });
		world.CityHomelands.Restore(new CityHomelandRecord { CityId = 301, OriginalKingdomId = 2, OriginalKingdomName = "Родина", OriginalIdeology = IdeologyType.Democracy, AnnexedYear = 148 });
		world.RemoveKingdom(2);
		return world;
	}

	[Fact]
	public void MovementFindsTheLostHomelandAndGrows()
	{
		PoliticalWorld world = WorldWithConqueredHomeland(out KingdomPolitics winner);
		world.NoteHomesteadBond(1, HomesteadBond.Federation);

		for (int year = 100; year < 140; year++)
		{
			world.SetYear(year);
			world.YearTick(new List<KingdomInput> { InputWithCities(1, AllWinnerCities) });
		}

		NationalismState movement = world.NationalismOf(1);
		Assert.Equal(2, movement.OriginKingdomId);
		Assert.Equal("Родина", movement.OriginName);
		Assert.True(movement.Pressure > 0f);
	}

	[Fact]
	public void NationalistsTakePowerAndThenGiveItBack()
	{
		PoliticalWorld world = WorldWithConqueredHomeland(out KingdomPolitics winner);
		world.NoteHomesteadBond(1, HomesteadBond.Union);
		NationalismState movement = world.NationalismOf(1);
		// Обида уже созрела — ждём только ближайшего года.
		movement.Pressure = NationalismEngine.PowerThreshold + 5f;

		world.SetYear(150);
		Assert.Equal(2, world.CityHomelands.All[300].OriginalKingdomId);
		Assert.Equal(8, world.CityIdsOf(1).Count);
		world.YearTick(new List<KingdomInput> { InputWithCities(1, AllWinnerCities) });
		Assert.Equal(2, world.CityHomelands.All[300].OriginalKingdomId);

		Assert.Equal(IdeologyType.Nationalism, winner.Ideology);
		Assert.Equal(150, movement.SeizedYear);
		// Прежний строй — тот, что стоял прямо перед националистами (монархия за год
		// могла уже перерасти в империю — это и есть «строй прямо перед ними»).
		IdeologyType parent = NationalismEngine.ParentIdeology(movement);
		Assert.NotEqual(IdeologyType.Nationalism, parent);
		Assert.Contains(world.Bus.OfType(PolEventType.NationalismSeizedPower), e => e.KingdomId == 1);
		// Въезд закрыт — как минимум на 50 лет, и срок назван.
		Assert.True(movement.EntryBanUntilYear >= 200);

		// Власть держат три года, потом возвращают монархию и уходят в тень.
		for (int year = 151; year <= 154; year++)
		{
			world.SetYear(year);
			world.YearTick(new List<KingdomInput> { InputWithCities(1, AllWinnerCities) });
		}
		Assert.Equal(parent, winner.Ideology);
		Assert.Equal(0, movement.SeizedYear);
		Assert.True(movement.ShadowUntilYear >= 154);
		Assert.Contains(world.Bus.OfType(PolEventType.NationalismPowerReturned), e => e.KingdomId == 1);
	}

	[Fact]
	public void KingdomWithoutLostLandsNeverBecomesNationalist()
	{
		var world = new PoliticalWorld();
		world.Rng = new Random(5);
		world.SetBaseline(true);
		world.SetYear(100);
		KingdomPolitics kingdom = world.AddKingdom(1, IdeologyType.Monarchy, 20);
		world.AddCity(200, 1);
		world.AddCity(201, 1);
		world.CityHomelands.EnsureRecorded(200, 1, "Держава", IdeologyType.Monarchy);
		world.CityHomelands.EnsureRecorded(201, 1, "Держава", IdeologyType.Monarchy);

		NationalismState movement = world.NationalismOf(1);
		movement.Pressure = 100f;
		for (int year = 100; year < 130; year++)
		{
			world.SetYear(year);
			world.YearTick(new List<KingdomInput> { InputWithCities(1, AllWinnerCities) });
		}
		Assert.NotEqual(IdeologyType.Nationalism, kingdom.Ideology);
	}

	[Fact]
	public void SaveAndLoadKeepTheMovement()
	{
		PoliticalWorld world = WorldWithConqueredHomeland(out KingdomPolitics winner);
		world.NoteHomesteadBond(1, HomesteadBond.Vassalage);
		NationalismState movement = world.NationalismOf(1);
		movement.OriginKingdomId = 2;
		movement.OriginName = "Родина";
		movement.OriginYear = 88;
		movement.Pressure = 61.5f;
		movement.EntryBanUntilYear = 300;
		movement.Victories = 2;

		string json = SaveData.Write(world);
		Assert.Contains("\"nat\"", json);
		var restored = new PoliticalWorld();
		SaveData.Apply(restored, json);
		NationalismState back = restored.NationalismOf(1);

		Assert.Equal(2, back.OriginKingdomId);
		Assert.Equal("Родина", back.OriginName);
		Assert.Equal(88, back.OriginYear);
		Assert.Equal(61.5f, back.Pressure, 2);
		Assert.Equal(HomesteadBond.Vassalage, back.Bond);
		Assert.Equal(300, back.EntryBanUntilYear);
		Assert.Equal(2, back.Victories);
	}
}
