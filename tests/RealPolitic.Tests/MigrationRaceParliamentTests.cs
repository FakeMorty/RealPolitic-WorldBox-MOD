using System;
using System.Collections.Generic;
using RealPolitic.Core;
using Xunit;

namespace RealPolitic.Tests;

/// <summary>
/// 0.46: проверки новых механик — переселение, борьба малых народов,
/// парламент (100 мест полукругом), расселение.
///
/// Всё это чистые правила ядра: их можно проверить без запуска WorldBox,
/// и именно поэтому они вынесены из игрового слоя.
/// </summary>
public class MigrationRaceParliamentTests
{
	// ---------------------------------------------------------------- переселение

	[Fact]
	public void BothSidesAgree_IsPlainMove()
	{
		Assert.Equal(MigrationOutcome.Agreed,
			MigrationRules.Resolve(MigrationVerdict.Allow, MigrationVerdict.Allow, escapeSucceeded: false));
	}

	[Fact]
	public void HomeRefusedHostAgreed_IsEscape()
	{
		Assert.Equal(MigrationOutcome.Escaped,
			MigrationRules.Resolve(MigrationVerdict.Deny, MigrationVerdict.Allow, escapeSucceeded: true));
	}

	[Fact]
	public void HomeRefusedHostAgreed_ButCaughtOnBorder()
	{
		Assert.Equal(MigrationOutcome.Caught,
			MigrationRules.Resolve(MigrationVerdict.Deny, MigrationVerdict.Allow, escapeSucceeded: false));
	}

	[Fact]
	public void HomeAgreedHostRefused_StaysHome()
	{
		Assert.Equal(MigrationOutcome.StayedHome,
			MigrationRules.Resolve(MigrationVerdict.Allow, MigrationVerdict.Deny, escapeSucceeded: false));
	}

	[Fact]
	public void ClosedBorders_NeverLetPeopleGo()
	{
		// Даже довольного и не последнего в роду держава с закрытой границей не отпускает.
		Assert.Equal(MigrationVerdict.Deny,
			MigrationRules.DecideHome(BorderLaw.Closed, loyaltyOfCitizen: 1f, isLastOfRace: false, population: 100));
	}

	[Fact]
	public void LastOfRace_IsNeverReleased()
	{
		Assert.Equal(MigrationVerdict.Deny,
			MigrationRules.DecideHome(BorderLaw.Open, loyaltyOfCitizen: 1f, isLastOfRace: true, population: 100));
	}

	[Fact]
	public void EscapeIsHarderWithSecretPoliceAndClosedBorders()
	{
		float easy = MigrationRules.EscapeChance(0.8f, BorderLaw.Open, WatchLaw.Liberty, neighbouring: true);
		float hard = MigrationRules.EscapeChance(0.8f, BorderLaw.Closed, WatchLaw.SecretPolice, neighbouring: false);
		Assert.True(easy > hard, "на открытой границе бежать должно быть проще: " + easy + " против " + hard);
		Assert.True(hard < 0.5f, "при тайной полиции и закрытой границе побег должен быть редким: " + hard);
	}

	[Fact]
	public void OppressiveCountry_IsNotAValidDestinationForForeignRace()
	{
		// Этнонационализм принимает только «свой» род — чужой туда не поедет.
		float tolerated = RaceSurvival.Tolerance(RacePolicy.Oppress, sameTitularRace: true, legitimacy: 60f);
		float rejected = RaceSurvival.Tolerance(RacePolicy.Oppress, sameTitularRace: false, legitimacy: 60f);
		Assert.True(tolerated > 0.8f);
		Assert.True(rejected < 0.1f);

		List<MigrationCandidate> candidates = new List<MigrationCandidate>
		{
			new MigrationCandidate { KingdomId = 1, Tolerance = rejected, Prosperity = 90f, Neighbouring = true },
			new MigrationCandidate { KingdomId = 2, Tolerance = 0.9f, Prosperity = 40f, Neighbouring = false }
		};
		Assert.Equal(2, MigrationRules.ChooseDestination(candidates));
	}

	[Fact]
	public void WarDestinationIsAvoided()
	{
		List<MigrationCandidate> candidates = new List<MigrationCandidate>
		{
			new MigrationCandidate { KingdomId = 1, Tolerance = 0.9f, Prosperity = 80f, Neighbouring = true, AtWar = true }
		};
		Assert.Equal(-1, MigrationRules.ChooseDestination(candidates));
	}

	[Fact]
	public void PersecutionRaisesPressureAboveThreshold()
	{
		float calm = MigrationRules.Pressure(dissent: 0.1f, racePressure: 0f, prosperity: 70f, atWar: false);
		float persecuted = MigrationRules.Pressure(dissent: 0.5f, racePressure: 0.8f, prosperity: 30f, atWar: true);
		Assert.True(calm < MigrationRules.WanderlustThreshold);
		Assert.True(persecuted > MigrationRules.WanderlustThreshold);
	}

	// ---------------------------------------------------------------- малые народы

	[Fact]
	public void IdeologyDecidesRacePolicy()
	{
		Assert.Equal(RacePolicy.Oppress, RaceSurvival.PolicyOf(new KingdomPolitics { Ideology = IdeologyType.Ethnonationalism }));
		Assert.Equal(RacePolicy.Protect, RaceSurvival.PolicyOf(new KingdomPolitics { Ideology = IdeologyType.Democracy }));
		Assert.Equal(RacePolicy.Ignore, RaceSurvival.PolicyOf(new KingdomPolitics { Ideology = IdeologyType.Tribalism }));
	}

	[Fact]
	public void SmallNationIsEndangered()
	{
		Assert.True(RaceSurvival.IsEndangered(8, 400));
		Assert.False(RaceSurvival.IsEndangered(200, 400));
		Assert.False(RaceSurvival.IsEndangered(0, 400));
	}

	[Fact]
	public void OppressedSmallNationFeelsMorePressureThanProtected()
	{
		float oppressed = RaceSurvival.Pressure(RacePolicy.Oppress, endangered: true, population: 6, totalPopulation: 400);
		float protectedNation = RaceSurvival.Pressure(RacePolicy.Protect, endangered: true, population: 6, totalPopulation: 400);
		Assert.True(oppressed > protectedNation, "при притеснении давление должно быть выше");
		Assert.True(oppressed > 0.7f);
	}

	[Fact]
	public void SmallestOfRaceIsFound()
	{
		List<RaceShare> races = new List<RaceShare>
		{
			new RaceShare { RaceId = "fox", Population = 300 },
			new RaceShare { RaceId = "chicken", Population = 5 },
			new RaceShare { RaceId = "human", Population = 0 }
		};
		Assert.Equal("chicken", RaceSurvival.Smallest(races).RaceId);
		Assert.Equal(5, RaceSurvival.Share(races, "chicken") * 305, 0);
	}

	// ---------------------------------------------------------------- парламент

	[Fact]
	public void ParliamentAlwaysHasExactlyOneHundredSeats()
	{
		int[] seats = Parliament.Allocate(new[] { 47, 28, 15, 10 });
		int sum = 0;
		for (int i = 0; i < seats.Length; i++)
		{
			sum += seats[i];
		}
		Assert.Equal(100, sum);
		Assert.Equal(47, seats[0]);
	}

	[Fact]
	public void ParliamentHandlesUnevenShares()
	{
		// Доли не обязаны давать ровно 100 — места всё равно распределятся точно.
		int[] seats = Parliament.Allocate(new[] { 1, 1, 1 });
		int sum = seats[0] + seats[1] + seats[2];
		Assert.Equal(100, sum);
		Assert.True(Math.Abs(seats[0] - seats[1]) <= 1);
		Assert.True(Math.Abs(seats[1] - seats[2]) <= 1);
	}

	[Fact]
	public void ParliamentHandlesEmptyInput()
	{
		Assert.Empty(Parliament.Allocate(Array.Empty<int>()));
		Assert.Equal(0, Parliament.Allocate(new[] { 0, 0 })[0]);
	}

	[Fact]
	public void ArcMakesASemicircleAboveTheFloor()
	{
		ParliamentPoint[] points = Parliament.Arc(100, 5, 52f, 13f, 180f);
		Assert.Equal(100, points.Length);
		float minX = float.MaxValue, maxX = float.MinValue, minY = float.MaxValue;
		float maxY = float.MinValue;
		for (int i = 0; i < points.Length; i++)
		{
			minX = Math.Min(minX, points[i].X);
			maxX = Math.Max(maxX, points[i].X);
			minY = Math.Min(minY, points[i].Y);
			maxY = Math.Max(maxY, points[i].Y);
		}
		// Полукруг: по краям — почти горизонтально, по центру выше.
		Assert.True(maxX > 90f && minX < -90f, "разброс по горизонтали: " + minX + ".." + maxX);
		Assert.True(minY >= -0.5f, "места не должны уходить ниже пола: " + minY);
		Assert.True(maxY > 45f, "верхний ряд должен быть выше: " + maxY);
	}

	[Fact]
	public void SeatsAreFilledLeftToRightByPartyBlocks()
	{
		List<IdeologyType> parties = new List<IdeologyType> { IdeologyType.Monarchy, IdeologyType.Communism };
		List<int> seats = new List<int> { 40, 60 };
		ParliamentPoint[] points = Parliament.Arc(100, 5, 52f, 13f, 180f);
		int[] order = Parliament.SeatingOrder(parties, seats, points);
		Assert.Equal(100, order.Length);
		// Коммунисты левее — их блок должен стоять первым по порядку мест.
		Assert.Equal(1, order[0]);
		Assert.Equal(0, order[99]);
		// Блоки не перемешиваются.
		int changes = 0;
		for (int i = 1; i < order.Length; i++)
		{
			if (order[i] != order[i - 1])
			{
				changes++;
			}
		}
		Assert.Equal(1, changes);
	}

	[Fact]
	public void PartiesHaveDistinctColors()
	{
		var democracy = Parliament.Color(IdeologyType.Democracy);
		var communism = Parliament.Color(IdeologyType.Communism);
		Assert.True(Math.Abs(democracy.R - communism.R) > 0.2f);
		Assert.True(democracy.B > democracy.R);
	}

	// ---------------------------------------------------------------- расселение

	private static KingdomInput SampleInput(int population, int cities)
	{
		List<CityInput> list = new List<CityInput>();
		for (int i = 0; i < cities; i++)
		{
			list.Add(new CityInput { CityId = i + 1, Population = population / Math.Max(1, cities) });
		}
		return new KingdomInput
		{
			KingdomId = 1,
			Name = "Тест",
			Population = population,
			Cities = list,
			AtWar = false,
			Starving = false
		};
	}

	[Fact]
	public void SettlementNeedsFreeLandAndCooldown()
	{
		KingdomPolitics state = new KingdomPolitics { Ideology = IdeologyType.Monarchy, Economy = new EconomyState { Prosperity = 60f } };
		KingdomInput input = SampleInput(200, 2);
		SimParams p = new SimParams();
		Assert.True(SettlementExpansion.ShouldFound(state, input, 200, 100, -100, p, out _));
		// Нет свободной земли — города не будет.
		Assert.False(SettlementExpansion.ShouldFound(state, input, 10, 100, -100, p, out string whyLand));
		Assert.Contains("свободной земли", whyLand);
		// Только что основали — ждём.
		Assert.False(SettlementExpansion.ShouldFound(state, input, 200, 100, 95, p, out string whyCooldown));
		Assert.Contains("недавно", whyCooldown);
	}

	[Fact]
	public void SettlementIsNotFoundedInWarOrFamine()
	{
		KingdomPolitics state = new KingdomPolitics { Ideology = IdeologyType.Monarchy, Economy = new EconomyState { Prosperity = 60f } };
		KingdomInput atWar = SampleInput(200, 2);
		atWar.AtWar = true;
		Assert.False(SettlementExpansion.ShouldFound(state, atWar, 200, 100, -100, new SimParams(), out _));
		KingdomInput starving = SampleInput(200, 2);
		starving.Starving = true;
		Assert.False(SettlementExpansion.ShouldFound(state, starving, 200, 100, -100, new SimParams(), out _));
	}

	[Fact]
	public void CityLimitGrowsWithPopulation()
	{
		Assert.True(SettlementExpansion.CityLimit(600) > SettlementExpansion.CityLimit(200));
		Assert.Equal(0, SettlementExpansion.CityLimit(0));
		KingdomPolitics state = new KingdomPolitics { Ideology = IdeologyType.Monarchy, Economy = new EconomyState { Prosperity = 60f } };
		KingdomInput crowded = SampleInput(200, SettlementExpansion.CityLimit(200) + 1);
		Assert.False(SettlementExpansion.ShouldFound(state, crowded, 500, 100, -100, new SimParams(), out string why));
		Assert.Contains("предел", why);
	}
}
