using System;
using System.Collections.Generic;
using RealPolitic.Core;
using Xunit;

namespace RealPolitic.Tests;

public class SuccessionAndDeathTests
{
	[Fact]
	public void Succession_EventCarriesGenderedArticleAndTitle()
	{
		// Женский правитель: «новая королева», мужской: «новый король».
		var world = new PoliticalWorld();
		world.Rng = new Random(42);
		world.SetBaseline(true);
		world.SetYear(5);
		world.AddKingdom(1, IdeologyType.Monarchy, 0);
		world.AddCity(10, 1);

		world.YearTick(new List<KingdomInput> { Snapshot(100, "King1", false) });

		world.SetYear(6);
		world.YearTick(new List<KingdomInput> { Snapshot(101, "Queen1", true) });

		var female = world.Bus.OfType(PolEventType.Succession);
		Assert.Single(female);
		Assert.Equal("RP_ART_F", female[0].Get("new"));
		Assert.Equal(Ideologies.RulerTitleKey(IdeologyType.Monarchy, true), female[0].Get("title"));
		Assert.Equal(Ideologies.StateInKey(IdeologyType.Monarchy), female[0].Get("state"));

		// Затем мужчина — «новый».
		world.SetYear(7);
		world.YearTick(new List<KingdomInput> { Snapshot(102, "King2", false) });
		var male = world.Bus.OfType(PolEventType.Succession);
		Assert.Equal(2, male.Count);
		Assert.Equal("RP_ART_M", male[1].Get("new"));
		Assert.Equal(Ideologies.RulerTitleKey(IdeologyType.Monarchy, false), male[1].Get("title"));
	}

	[Fact]
	public void Novelty_EventsCarryStateInKeyByKingdomIdeology()
	{
		var world = new PoliticalWorld();
		world.Rng = new Random(7);
		world.SetBaseline(true);
		world.SetYear(5);
		// Племенное государство: «В племени … основана религия …»
		world.AddKingdom(1, IdeologyType.Tribalism, 0);
		world.AddCity(10, 1);

		world.YearTick(new List<KingdomInput>
		{
			new KingdomInput
			{
				KingdomId = 1,
				Name = "Tribe",
				Population = 50,
				Warriors = 2,
				Ruler = new RulerRef { ActorId = 1, Name = "Chief", KingdomId = 1, SinceYear = 1 },
				Cities = new List<CityInput>
				{
					new CityInput { CityId = 10, KingdomId = 1, Population = 50, FoodOk = true, CultureId = 7, CultureName = "C", ReligionId = 100, ReligionName = "NewRel", LanguageId = 2, LanguageName = "L" }
				}
			}
		});

		var evts = world.Bus.OfType(PolEventType.NewReligion);
		Assert.Single(evts);
		Assert.Equal(Ideologies.StateInKey(IdeologyType.Tribalism), evts[0].Get("state"));
	}

	[Fact]
	public void DeathRecord_LocKeyFor_FemaleAddsSuffix()
	{
		var male = new DeathRecord { Cause = DeathCause.Executed };
		var female = new DeathRecord { Cause = DeathCause.Executed, WasFemale = true };
		Assert.Equal("RP_DEATH_EXECUTED", male.LocKey);
		Assert.Equal("RP_DEATH_EXECUTED", male.LocKeyFor(female: false));
		Assert.Equal("RP_DEATH_EXECUTED_F", female.LocKeyFor(female: true));
		Assert.Equal("RP_DEATH_FELL_F", new DeathRecord { Cause = DeathCause.Fell, WasFemale = true }.LocKeyFor(true));
	}

	[Fact]
	public void RecordRulerDeath_DoesNotDoublePublish_WhenAlreadyRecorded()
	{
		var bus = new PolEventBus();
		var deaths = new DeathRegistry();
		var state = new KingdomPolitics { KingdomId = 1, Ideology = IdeologyType.Monarchy };
		var ruler = new RulerRef { ActorId = 55, Name = "King", KingdomId = 1, IsFemale = true };

		// Смерть уже записана игровым хуком (GameDeaths) — ядро не публикует дубль.
		deaths.Record(55, "King", DeathCause.Explosion, 10, wasRuler: true, wasFemale: true);
		Assert.Empty(bus.OfType(PolEventType.RulerDeath));

		var recorded = KingdomSimulation.RecordRulerDeath(deaths, bus, state, ruler, 10, DeathCause.Explosion, "");
		Assert.NotNull(recorded);
		// Дубля нет: событие о смерти уже было опубликовано хуком, ядро молчит.
		Assert.Empty(bus.OfType(PolEventType.RulerDeath));

		// Повторный вызов: та же запись, событий по-прежнему нет.
		var again = KingdomSimulation.RecordRulerDeath(deaths, bus, state, ruler, 10, DeathCause.Explosion, "");
		Assert.Same(recorded, again);
		Assert.Empty(bus.OfType(PolEventType.RulerDeath));
	}

	[Fact]
	public void RecordRulerDeath_PublishesGenderedCauseForFemaleRuler()
	{
		var bus = new PolEventBus();
		var deaths = new DeathRegistry();
		var state = new KingdomPolitics { KingdomId = 1, Ideology = IdeologyType.Monarchy };
		var ruler = new RulerRef { ActorId = 56, Name = "Queen", KingdomId = 1, IsFemale = true };

		KingdomSimulation.RecordRulerDeath(deaths, bus, state, ruler, 10, DeathCause.Executed, "");
		var evts = bus.OfType(PolEventType.RulerDeath);
		Assert.Single(evts);
		Assert.Equal("RP_DEATH_EXECUTED_F", evts[0].Get("cause"));
		Assert.Equal(Ideologies.RulerTitleKey(IdeologyType.Monarchy, true), evts[0].Get("title"));
	}

	[Fact]
	public void Succession_PreservesPreviousRulerDetails_AfterInterregnum()
	{
		var world = new PoliticalWorld();
		world.Rng = new Random(42);
		world.SetBaseline(true);
		world.SetYear(5);
		world.AddKingdom(1, IdeologyType.Monarchy, 0);
		world.AddCity(10, 1);

		// Год 5: на троне старый король с 1-го года
		world.YearTick(new List<KingdomInput> { Snapshot(100, "OldKing", false) });

		// Год 6: междуцарствие (король умер, трон пуст — Ruler == null)
		world.SetYear(6);
		var interregnumInput = new KingdomInput
		{
			KingdomId = 1,
			Name = "A",
			Population = 100,
			Warriors = 5,
			Ruler = null,
			Cities = new List<CityInput>
			{
				new CityInput { CityId = 10, KingdomId = 1, Population = 100, FoodOk = true, CultureId = 7, CultureName = "C", ReligionId = 3, ReligionName = "R", LanguageId = 2, LanguageName = "L" }
			}
		};
		world.YearTick(new List<KingdomInput> { interregnumInput });
		// В год междуцарствия преемственность не объявляется (нового короля ещё нет)
		Assert.Empty(world.Bus.OfType(PolEventType.Succession));

		// Год 7: восходит новый король
		world.SetYear(7);
		world.YearTick(new List<KingdomInput> { Snapshot(101, "NewKing", false) });

		var evts = world.Bus.OfType(PolEventType.Succession);
		Assert.Single(evts);
		PolEvent evt = evts[0];
		Assert.Equal("NewKing", evt.Get("ruler"));
		// Старый король должен сохраниться в деталях преемственности, несмотря на год междуцарствия!
		Assert.Equal("OldKing", evt.Get("old_ruler"));
		Assert.Equal(Ideologies.RulerTitleKey(IdeologyType.Monarchy, false), evt.Get("old_title"));
		Assert.Equal("7", evt.Get("old_reign"));
	}

	private static KingdomInput Snapshot(int rulerActorId, string rulerName, bool female)
	{
		return new KingdomInput
		{
			KingdomId = 1,
			Name = "A",
			Population = 100,
			Warriors = 5,
			Ruler = new RulerRef { ActorId = rulerActorId, Name = rulerName, KingdomId = 1, SinceYear = 1, IsFemale = female },
			Cities = new List<CityInput>
			{
				new CityInput { CityId = 10, KingdomId = 1, Population = 100, FoodOk = true, CultureId = 7, CultureName = "C", ReligionId = 3, ReligionName = "R", LanguageId = 2, LanguageName = "L" }
			}
		};
	}
}
