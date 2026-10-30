using System;
using System.Collections.Generic;
using RealPolitic.Core;
using Xunit;

namespace RealPolitic.Tests;

/// <summary>
/// Тесты модовых механик идеологий: ульта (кулдаун, условия) и угроза (накопление, срыв).
/// </summary>
public class IdeologyMechanicsTests
{
	private static KingdomPolitics State(IdeologyType ideology, int founded = 0)
	{
		return new KingdomPolitics
		{
			KingdomId = 1,
			Ideology = ideology,
			Legitimacy = 70f,
			Support = 60f,
			FoundedYear = founded
		};
	}

	private static KingdomInput Input(bool atWar = false, int cities = 1)
	{
		List<CityInput> list = new List<CityInput>();
		for (int i = 0; i < cities; i++)
		{
			list.Add(new CityInput { CityId = i + 1 });
		}
		return new KingdomInput
		{
			KingdomId = 1,
			Name = "Тест",
			AtWar = atWar,
			Cities = list,
			Relations = new List<RelationInfo> { RelationInfo.Peaceful(2, adjacent: true) }
		};
	}

	// ---------------------------------------------------------------- определения

	[Fact]
	public void EveryIdeologyHasPowerAndThreat()
	{
		foreach (IdeologyType type in Enum.GetValues(typeof(IdeologyType)))
		{
			IdeologyPowerDef power = IdeologyMechanics.Power(type);
			IdeologyThreatDef threat = IdeologyMechanics.Threat(type);
			Assert.False(string.IsNullOrEmpty(power.NameKey));
			Assert.False(string.IsNullOrEmpty(threat.NameKey));
			Assert.True(power.Cooldown > 0);
			Assert.True(threat.Threshold > 0f);
		}
	}

	// ---------------------------------------------------------------- ульта

	[Fact]
	public void PowerGoesOnCooldownAfterUse()
	{
		KingdomPolitics state = State(IdeologyType.Militarism);
		IdeologyPowerDef def = IdeologyMechanics.Power(IdeologyType.Militarism);
		IdeologyPowers.Use(state, Input(atWar: true), def, 100, null);

		Assert.Equal(100, state.Mechanics.LastUsedYear);
		Assert.True(state.Mechanics.IsActive(101));
		Assert.True(state.Mechanics.CooldownLeft(def, 101) > 0);
		Assert.Equal(0, state.Mechanics.CooldownLeft(def, 100 + def.Cooldown));
	}

	[Fact]
	public void PowerBlockedWhileOnCooldown()
	{
		KingdomPolitics state = State(IdeologyType.Militarism);
		IdeologyPowerDef def = IdeologyMechanics.Power(IdeologyType.Militarism);
		IdeologyPowers.Use(state, Input(atWar: true), def, 100, null);

		Assert.False(IdeologyPowers.CanUse(state, Input(atWar: true), def, 105));
		Assert.True(IdeologyPowers.CanUse(state, Input(atWar: true), def, 100 + def.Cooldown + def.Duration));
	}

	[Fact]
	public void PowerBlockedByYoungAgeAndLowLegitimacy()
	{
		IdeologyPowerDef def = IdeologyMechanics.Power(IdeologyType.Monarchy);

		KingdomPolitics young = State(IdeologyType.Monarchy, founded: 100);
		Assert.False(IdeologyPowers.CanUse(young, Input(), def, 101));

		KingdomPolitics weak = State(IdeologyType.Monarchy, founded: 1);
		weak.Legitimacy = 5f;
		Assert.False(IdeologyPowers.CanUse(weak, Input(), def, 100));
	}

	[Fact]
	public void MilitarismUsesPowerOnlyInWar()
	{
		KingdomPolitics state = State(IdeologyType.Militarism);
		Assert.True(IdeologyPowers.Situational(state, Input(atWar: true), 100));
		Assert.False(IdeologyPowers.Situational(state, Input(atWar: false), 100));
	}

	[Fact]
	public void EgalitarianAssemblyAdoptsMajorityIdeology()
	{
		KingdomPolitics state = State(IdeologyType.Egalitarianism, founded: 1);
		state.Census[IdeologyType.Technocracy] = 80;
		state.Census[IdeologyType.Egalitarianism] = 20;

		Assert.Equal(IdeologyType.Technocracy, IdeologyPowers.MajorityIdeology(state));

		IdeologyPowerDef def = IdeologyMechanics.Power(IdeologyType.Egalitarianism);
		IdeologyPowers.Use(state, Input(), def, 100, null);

		Assert.Equal(IdeologyType.Technocracy, state.Ideology);
	}

	[Fact]
	public void PowerPublishesEvent()
	{
		KingdomPolitics state = State(IdeologyType.Theocracy, founded: 1);
		PolEventBus bus = new PolEventBus();
		IdeologyPowerDef def = IdeologyMechanics.Power(IdeologyType.Theocracy);
		IdeologyPowers.Use(state, Input(), def, 100, bus);

		Assert.Single(bus.OfType(PolEventType.IdeologyPowerUsed));
	}

	// ---------------------------------------------------------------- угроза

	[Fact]
	public void MilitarismThreatGrowsInWarAndFallsInPeace()
	{
		KingdomPolitics state = State(IdeologyType.Militarism);
		Assert.True(IdeologyThreats.Growth(state, Input(atWar: true), 100) > 0f);
		Assert.True(IdeologyThreats.Growth(state, Input(atWar: false), 100) < 0f);
	}

	[Fact]
	public void MerchantThreatGrowsInWar()
	{
		KingdomPolitics state = State(IdeologyType.MerchantRepublic);
		Assert.True(IdeologyThreats.Growth(state, Input(atWar: true), 100) > 0f);
		Assert.True(IdeologyThreats.Growth(state, Input(atWar: false), 100) < 0f);
	}

	[Fact]
	public void TribalThreatGrowsWithCityCount()
	{
		KingdomPolitics state = State(IdeologyType.Tribalism);
		float small = IdeologyThreats.Growth(state, Input(cities: 1), 100);
		float big = IdeologyThreats.Growth(state, Input(cities: 8), 100);
		Assert.True(small < 0f);
		Assert.True(big > small);
	}

	[Fact]
	public void AristocracyThreatGrowsInPeace()
	{
		// Заговор знати — внутренняя угроза аристократии, растущая именно в долгом мире.
		KingdomPolitics state = State(IdeologyType.Aristocracy);
		Assert.True(IdeologyThreats.Growth(state, Input(atWar: false), 100) > 0f);
	}

	[Fact]
	public void ThreatBreaksAndResets()
	{
		KingdomPolitics state = State(IdeologyType.Militarism);
		PolEventBus bus = new PolEventBus();
		state.Mechanics.ThreatLevel = 99f;

		bool broke = IdeologyThreats.Tick(state, Input(atWar: true), 100, bus);

		Assert.True(broke);
		Assert.Equal(0f, state.Mechanics.ThreatLevel);
		Assert.Equal(1, state.Mechanics.ThreatBreaks);
		Assert.Single(bus.OfType(PolEventType.IdeologyThreatBroke));
	}

	[Fact]
	public void ThreatDoesNotGrowWhileRecovering()
	{
		KingdomPolitics state = State(IdeologyType.Militarism);
		IdeologyThreatDef def = IdeologyMechanics.Threat(IdeologyType.Militarism);
		state.Mechanics.ThreatLevel = 99f;
		IdeologyThreats.Tick(state, Input(atWar: true), 100, null);

		state.Mechanics.ThreatLevel = 50f;
		IdeologyThreats.Tick(state, Input(atWar: true), 101, null);

		Assert.True(state.Mechanics.IsRecovering(def, 101));
		Assert.True(state.Mechanics.ThreatLevel < 50f);
	}

	[Fact]
	public void ThreatLevelStaysInRange()
	{
		KingdomPolitics state = State(IdeologyType.Militarism);
		for (int year = 0; year < 200; year++)
		{
			IdeologyThreats.Tick(state, Input(), year, null);
			Assert.InRange(state.Mechanics.ThreatLevel, 0f, 100f);
		}
	}

	[Fact]
	public void FragmentationIsZeroForUniformPopulation()
	{
		KingdomPolitics state = State(IdeologyType.Egalitarianism);
		state.Census[IdeologyType.Egalitarianism] = 100;
		Assert.Equal(0f, IdeologyThreats.Fragmentation(state), 3);

		state.Census[IdeologyType.Monarchy] = 100;
		Assert.True(IdeologyThreats.Fragmentation(state) > 0.4f);
	}

	[Fact]
	public void DissentShareCountsForeignIdeologies()
	{
		KingdomPolitics state = State(IdeologyType.Theocracy);
		state.Census[IdeologyType.Theocracy] = 75;
		state.Census[IdeologyType.Technocracy] = 25;
		Assert.Equal(0.25f, IdeologyThreats.DissentShare(state), 3);
	}

	[Fact]
	public void MechanicsResetClearsEverything()
	{
		KingdomPolitics state = State(IdeologyType.Monarchy);
		state.Mechanics.ThreatLevel = 50f;
		state.Mechanics.LastUsedYear = 10;
		state.Mechanics.Reset();

		Assert.Equal(0f, state.Mechanics.ThreatLevel);
		Assert.Equal(int.MinValue, state.Mechanics.LastUsedYear);
	}

	[Fact]
	public void StrongMonarchAtAge65DoesNotSpiralIntoSuccessionCrisisWhileWeakElderlyMonarchDoes()
	{
		KingdomPolitics strong = State(IdeologyType.Monarchy);
		strong.Legitimacy = 80f;
		strong.Ruler = new RulerRef { ActorId = 1, Name = "Король", Age = 65, SinceYear = 20 };
		Assert.True(IdeologyThreats.Growth(strong, Input(), 100) < 0f);

		KingdomPolitics weak = State(IdeologyType.Monarchy);
		weak.Legitimacy = 35f;
		weak.Ruler = new RulerRef { ActorId = 2, Name = "Слабый король", Age = 75, SinceYear = 40 };
		Assert.True(IdeologyThreats.Growth(weak, Input(), 100) > 5f);
	}
}
