using System;
using System.Collections.Generic;
using RealPolitic.Core;
using Xunit;

namespace RealPolitic.Tests;

/// <summary>
/// Полный жизненный цикл ульты: применение → действие → окончание,
/// и разброс стартовой угрозы между государствами.
/// </summary>
public class PowerLifecycleTests
{
	private static KingdomInput Input(bool atWar = false, int cities = 1)
	{
		List<CityInput> list = new List<CityInput>();
		for (int i = 0; i < cities; i++)
		{
			list.Add(new CityInput { CityId = i + 1, KingdomId = 1, Population = 50 });
		}
		return new KingdomInput
		{
			KingdomId = 1,
			Name = "Тест",
			Population = 100,
			AtWar = atWar,
			Cities = list,
			Relations = new List<RelationInfo> { new RelationInfo { OtherId = 2, Relation = RelationType.Peace, Adjacent = true } }
		};
	}

	[Fact]
	public void Power_PublishesEndEvent_WhenDurationExpires()
	{
		KingdomPolitics state = new KingdomPolitics { KingdomId = 1, Ideology = IdeologyType.Monarchy, FoundedYear = 1, Legitimacy = 79f };
		KingdomInput input = Input();
		PolEventBus bus = new PolEventBus();
		Random rng = new Random(1);
		for (int year = 1; year <= 60; year++)
		{
			IdeologyPowers.Tick(state, input, year, bus, rng);
		}
		Assert.NotEmpty(bus.OfType(PolEventType.IdeologyPowerUsed));
		Assert.NotEmpty(bus.OfType(PolEventType.IdeologyPowerEnded));
	}

	[Fact]
	public void EndEvent_ComesAfterUseEvent()
	{
		KingdomPolitics state = new KingdomPolitics { KingdomId = 1, Ideology = IdeologyType.Monarchy, FoundedYear = 1, Legitimacy = 79f };
		KingdomInput input = Input();
		PolEventBus bus = new PolEventBus();
		Random rng = new Random(1);
		int usedYear = -1;
		int endedYear = -1;
		for (int year = 1; year <= 40; year++)
		{
			IdeologyPowers.Tick(state, input, year, bus, rng);
			foreach (PolEvent e in bus.Log)
			{
				if (e.Type == PolEventType.IdeologyPowerUsed && usedYear < 0)
				{
					usedYear = e.Year;
				}
				if (e.Type == PolEventType.IdeologyPowerEnded && endedYear < 0)
				{
					endedYear = e.Year;
				}
			}
		}
		Assert.True(usedYear > 0);
		Assert.True(endedYear > usedYear);
		// Длительность ульты монархии — 15 лет.
		Assert.Equal(IdeologyMechanics.Power(IdeologyType.Monarchy).Duration, endedYear - usedYear);
	}

	[Fact]
	public void EndEvent_IsMinorSeverity()
	{
		KingdomPolitics state = new KingdomPolitics { KingdomId = 1, Ideology = IdeologyType.Monarchy, FoundedYear = 1, Legitimacy = 79f };
		KingdomInput input = Input();
		PolEventBus bus = new PolEventBus();
		Random rng = new Random(1);
		for (int year = 1; year <= 40; year++)
		{
			IdeologyPowers.Tick(state, input, year, bus, rng);
		}
		foreach (PolEvent e in bus.OfType(PolEventType.IdeologyPowerEnded))
		{
			Assert.Equal(Severity.Minor, e.Severity);
		}
	}

	[Fact]
	public void StartingThreat_DiffersBetweenKingdoms()
	{
		HashSet<float> values = new HashSet<float>();
		for (int id = 1; id <= 7; id++)
		{
			values.Add(PoliticalWorld.StartingThreat(id, IdeologyType.Monarchy));
		}
		// Семь государств не должны стартовать с одинаковой угрозой,
		// иначе кризисы срываются у всех в один год.
		Assert.True(values.Count >= 5);
	}

	[Fact]
	public void StartingThreat_StaysInRange()
	{
		for (int id = 1; id <= 200; id++)
		{
			float v = PoliticalWorld.StartingThreat(id, IdeologyType.Theocracy);
			Assert.InRange(v, 0f, 35f);
		}
	}

	[Fact]
	public void StartingThreat_IsDeterministic()
	{
		float a = PoliticalWorld.StartingThreat(42, IdeologyType.Militarism);
		float b = PoliticalWorld.StartingThreat(42, IdeologyType.Militarism);
		Assert.Equal(a, b);
	}
}
