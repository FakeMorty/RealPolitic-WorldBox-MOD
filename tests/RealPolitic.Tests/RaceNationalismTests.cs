using System;
using System.Collections.Generic;
using System.Linq;
using RealPolitic.Core;
using Xunit;

namespace RealPolitic.Tests;

/// <summary>
/// 0.49.1: расовый национализм — вторая половина заказа владельца.
///
/// Главные правила, которые проверяем:
///   • одна раса в державе — расовых националистов не бывает НИКОГДА;
///   • маленькая чужая раса (меньше 15 %) ничего не требует;
///   • придя к власти, чужая раса делает хозяйкой СЕБЯ, а чужим отзывает гражданство;
///   • никого не вывозят и не убивают — только закрывают дверь;
///   • через три года возвращается прежний строй.
/// </summary>
public sealed class RaceNationalismTests
{
	private static CityInput City(int id, string raceId, string raceName, int population)
	{
		return new CityInput
		{
			CityId = id,
			KingdomId = 1,
			Name = "Город " + id,
			Population = population,
			FoodOk = true,
			RaceId = raceId,
			RaceName = raceName
		};
	}

	private static KingdomInput Input(string titularRace, params CityInput[] cities)
	{
		KingdomInput input = new KingdomInput
		{
			KingdomId = 1,
			Name = "Держава",
			Population = 1000,
			Warriors = 80,
			Ruler = new RulerRef { ActorId = 101, Name = "Вождь", KingdomId = 1, SinceYear = 1, Age = 40 },
			Cities = new List<CityInput>(),
			RaceId = titularRace,
			RaceName = titularRace
		};
		for (int i = 0; i < cities.Length; i++)
		{
			input.Cities.Add(cities[i]);
		}
		return input;
	}

	private static PoliticalWorld World(string titularRace, IdeologyType ideology)
	{
		var world = new PoliticalWorld();
		world.Rng = new Random(17);
		world.SetBaseline(true);
		world.SetYear(100);
		KingdomPolitics kingdom = world.AddKingdom(1, ideology, 20);
		kingdom.TitularRaceId = titularRace;
		return world;
	}

	[Fact]
	public void OneRaceNeverProducesRaceNationalists()
	{
		// Правило владельца: одна раса — движения нет. Проверяем и на уровне правила,
		// и на уровне целого столетия жизни.
		Assert.False(RaceNationalismEngine.CanAppear(1, 0f));
		Assert.False(RaceNationalismEngine.CanAppear(1, 1f));

		PoliticalWorld world = World("human", IdeologyType.Monarchy);
		KingdomInput input = Input("human",
			City(1, "human", "Люди", 500),
			City(2, "human", "Люди", 500));
		world.CityHomelands.EnsureRecorded(1, 1, "Держава", IdeologyType.Monarchy);
		world.CityHomelands.EnsureRecorded(2, 1, "Держава", IdeologyType.Monarchy);

		for (int year = 100; year < 200; year++)
		{
			world.SetYear(year);
			world.YearTick(new List<KingdomInput> { input });
		}

		RaceNationalismState movement = world.RaceMovementOf(1);
		Assert.False(movement.Active);
		Assert.Equal(0f, movement.Pressure, 3);
		Assert.Empty(world.CitizenshipBans);
	}

	[Fact]
	public void TinyMinorityDemandsNothingButBigOneGrows()
	{
		Assert.False(RaceNationalismEngine.CanAppear(2, 0.05f));
		Assert.True(RaceNationalismEngine.CanAppear(2, 0.4f));

		// 5 % — обида только остывает, 40 % — растёт.
		Assert.True(RaceNationalismEngine.NextPressure(20f, 0.05f, true) < 20f);
		float grown = 0f;
		for (int year = 0; year < 40; year++)
		{
			grown = RaceNationalismEngine.NextPressure(grown, 0.4f, false);
		}
		Assert.True(grown >= RaceNationalismEngine.PowerThreshold);
	}

	[Fact]
	public void CruelLeaderRevokesCitizenshipJustOneOnlyClosesBorders()
	{
		Assert.Equal(NationalismVerdict.RevokeCitizenship, RaceNationalismEngine.Verdict(RulerTrait.Cruel, 50f));
		Assert.Equal(NationalismVerdict.RevokeCitizenship, RaceNationalismEngine.Verdict(RulerTrait.Conqueror, 50f));
		Assert.Equal(NationalismVerdict.CloseBorders, RaceNationalismEngine.Verdict(RulerTrait.Just, 50f));
	}

	[Fact]
	public void EntryBanIsFiftyOneHundredOrTwoHundredYears()
	{
		Assert.Equal(50, RaceNationalismEngine.EntryBanYears(50f));
		Assert.Equal(100, RaceNationalismEngine.EntryBanYears(70f));
		Assert.Equal(200, RaceNationalismEngine.EntryBanYears(90f));
	}

	[Fact]
	public void ForeignRaceTakesPowerThenGivesItBack()
	{
		PoliticalWorld world = World("human", IdeologyType.Monarchy);
		KingdomInput input = Input("human",
			City(1, "human", "Люди", 400),
			City(2, "fox", "Лисы", 600));
		world.CityHomelands.EnsureRecorded(1, 1, "Держава", IdeologyType.Monarchy);
		world.CityHomelands.EnsureRecorded(2, 1, "Держава", IdeologyType.Monarchy);
		// Обида уже созрела.
		world.RaceMovementOf(1).Pressure = 90f;

		world.SetYear(100);
		world.YearTick(new List<KingdomInput> { input });

		RaceNationalismState movement = world.RaceMovementOf(1);
		KingdomPolitics kingdom = world.GetKingdom(1);
		Assert.Equal(IdeologyType.Ethnonationalism, kingdom.Ideology);
		Assert.Equal("fox", kingdom.TitularRaceId);
		Assert.Equal(100, movement.SeizedYear);
		// Въезд закрыт минимум на 50 лет, и решение записано.
		Assert.True(movement.EntryBanUntilYear >= 150);
		Assert.NotEmpty(world.CitizenshipBans);
		Assert.True(world.IsEntryBanned(1, "human", 120));
		Assert.False(world.IsEntryBanned(1, "fox", 120));
		Assert.Contains(world.Bus.OfType(PolEventType.RaceNationalismSeizedPower), e => e.KingdomId == 1);

		// Хозяином снова становится прежний народ и прежний строй.
		for (int year = 101; year <= 104; year++)
		{
			world.SetYear(year);
			world.YearTick(new List<KingdomInput> { input });
		}
		Assert.Equal(IdeologyType.Monarchy, kingdom.Ideology);
		Assert.Equal("human", kingdom.TitularRaceId);
		Assert.Equal(0, movement.SeizedYear);
		Assert.Contains(world.Bus.OfType(PolEventType.RaceNationalismPowerReturned), e => e.KingdomId == 1);
		// Но дверь для чужих остаётся закрытой до названного года.
		Assert.True(world.IsEntryBanned(1, "human", 120));
	}

	[Fact]
	public void RaceMovementSurvivesSaving()
	{
		PoliticalWorld world = World("human", IdeologyType.Monarchy);
		RaceNationalismState movement = world.RaceMovementOf(1);
		movement.RaceId = "fox";
		movement.RaceName = "Лисы";
		movement.Share = 0.55f;
		movement.Pressure = 71.5f;
		movement.EntryBanUntilYear = 260;
		world.AddCitizenshipBan(1, "human", "Люди", 120, 260, true);

		string json = SaveData.Write(world);
		var restored = new PoliticalWorld();
		SaveData.Apply(restored, json);

		RaceNationalismState back = restored.RaceMovementOf(1);
		Assert.Equal("fox", back.RaceId);
		Assert.Equal("Лисы", back.RaceName);
		Assert.Equal(0.55f, back.Share, 3);
		Assert.Equal(71.5f, back.Pressure, 2);
		Assert.Equal(260, back.EntryBanUntilYear);
		Assert.Single(restored.CitizenshipBans);
		Assert.True(restored.CitizenshipBans[0].CitizenshipRevoked);
		Assert.True(restored.IsEntryBanned(1, "human", 200));
	}
}
