using System;
using System.Collections.Generic;
using RealPolitic.Core;
using Xunit;

namespace RealPolitic.Tests;

/// <summary>
/// Данные для подробной летописи: длительность правления в записях о смерти
/// и детали предыдущего/нового правителя в событии преемственности.
/// </summary>
public class ChronicleDataTests
{
	[Fact]
	public void Succession_EventCarriesPreviousRulerDetails()
	{
		var world = new PoliticalWorld();
		world.Rng = new Random(42);
		world.SetBaseline(true);
		world.SetYear(5);
		world.AddKingdom(1, IdeologyType.Monarchy, 0);
		world.AddCity(10, 1);

		// Год 5: первый правитель (Старый, на троне с 1-го года).
		world.YearTick(new List<KingdomInput> { Snapshot(100, "Старый", false, 60) });

		// Год 6: смена на нового правителя (Молодой, возраст 12).
		world.SetYear(6);
		world.YearTick(new List<KingdomInput> { Snapshot(101, "Молодой", false, 12) });

		var evts = world.Bus.OfType(PolEventType.Succession);
		Assert.Single(evts);
		PolEvent evt = evts[0];
		Assert.Equal("Молодой", evt.Get("ruler"));
		Assert.Equal("12", evt.Get("age"));
		Assert.Equal("Старый", evt.Get("old_ruler"));
		// 6 - 1 + 1 = 6 лет правления старого правителя.
		Assert.Equal("6", evt.Get("old_reign"));
		Assert.Equal(Ideologies.RulerTitleKey(IdeologyType.Monarchy, false), evt.Get("old_title"));
	}

	[Fact]
	public void Succession_FirstRulerHasNoOldRuler()
	{
		var world = new PoliticalWorld();
		world.Rng = new Random(1);
		world.SetBaseline(true);
		world.SetYear(1);
		world.AddKingdom(1, IdeologyType.Monarchy, 0);

		// Первый тик — правитель появляется впервые: события преемственности нет.
		world.YearTick(new List<KingdomInput> { Snapshot(100, "Первый", false, 20) });
		Assert.Empty(world.Bus.OfType(PolEventType.Succession));

		// Второй тик без смены — тоже нет.
		world.SetYear(2);
		world.YearTick(new List<KingdomInput> { Snapshot(100, "Первый", false, 21) });
		Assert.Empty(world.Bus.OfType(PolEventType.Succession));
	}

	[Fact]
	public void RecordRulerDeath_SetsReignYearsFromSinceYear()
	{
		var bus = new PolEventBus();
		var deaths = new DeathRegistry();
		var state = new KingdomPolitics { KingdomId = 1, Ideology = IdeologyType.Monarchy };
		var ruler = new RulerRef { ActorId = 77, Name = "Queen", KingdomId = 1, IsFemale = true, SinceYear = 7 };

		DeathRecord rec = KingdomSimulation.RecordRulerDeath(deaths, bus, state, ruler, 10, DeathCause.Executed, "");
		Assert.NotNull(rec);
		Assert.Equal(10 - 7 + 1, rec.ReignYears);
	}

	[Fact]
	public void RecordRulerDeath_NoSinceYearMeansUnknownReign()
	{
		var bus = new PolEventBus();
		var deaths = new DeathRegistry();
		var state = new KingdomPolitics { KingdomId = 1, Ideology = IdeologyType.Monarchy };
		var ruler = new RulerRef { ActorId = 78, Name = "King", KingdomId = 1, SinceYear = 0 };

		DeathRecord rec = KingdomSimulation.RecordRulerDeath(deaths, bus, state, ruler, 10, DeathCause.Disease, "");
		Assert.NotNull(rec);
		Assert.Null(rec.ReignYears);
	}

	[Fact]
	public void Record_ReignYearsOnlyForRulers()
	{
		var deaths = new DeathRegistry();
		// Неправитель: длительность правления игнорируется.
		DeathRecord commoner = deaths.Record(1, "Bob", DeathCause.OldAge, 5, wasRuler: false, age: 80, reignYears: 99);
		Assert.Null(commoner.ReignYears);
		// Правитель: сохраняется.
		DeathRecord ruler = deaths.Record(2, "Alice", DeathCause.OldAge, 5, wasRuler: true, age: 70, wasFemale: true, reignYears: 30);
		Assert.Equal(30, ruler.ReignYears);
	}

	private static KingdomInput Snapshot(int rulerActorId, string rulerName, bool female, int age)
	{
		return new KingdomInput
		{
			KingdomId = 1,
			Name = "A",
			Population = 100,
			Warriors = 5,
			Ruler = new RulerRef { ActorId = rulerActorId, Name = rulerName, KingdomId = 1, SinceYear = 1, IsFemale = female, Age = age },
			Cities = new List<CityInput>
			{
				new CityInput { CityId = 10, KingdomId = 1, Population = 100, FoodOk = true, CultureId = 7, CultureName = "C", ReligionId = 3, ReligionName = "R", LanguageId = 2, LanguageName = "L" }
			}
		};
	}
}
