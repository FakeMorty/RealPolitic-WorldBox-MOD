using System.Collections.Generic;
using RealPolitic.Core;
using Xunit;

namespace RealPolitic.Tests;

public class NoveltyNotificationTests
{
	private static PoliticalWorld NewWorld()
	{
		var world = new PoliticalWorld();
		world.SetBaseline(true);
		world.SetYear(5);
		world.AddKingdom(1, IdeologyType.Monarchy, 0);
		world.AddCity(10, 1);
		return world;
	}

	private static KingdomInput Snapshot(string cultureName = "Северяне", int cultureId = 7)
	{
		return new KingdomInput
		{
			KingdomId = 1,
			Name = "Асгард",
			Population = 100,
			Warriors = 5,
			Cities = new List<CityInput>
			{
				new CityInput
				{
					CityId = 10,
					KingdomId = 1,
					Population = 100,
					FoodOk = true,
					CultureId = cultureId,
					CultureName = cultureName,
					ReligionId = 3,
					ReligionName = "Культ Света",
					LanguageId = 2,
					LanguageName = "Северный"
				}
			}
		};
	}

	[Fact]
	public void NewCulture_EventCarriesIdAndName()
	{
		PoliticalWorld world = NewWorld();

		world.YearTick(new List<KingdomInput> { Snapshot() });

		List<PolEvent> cultures = world.Bus.OfType(PolEventType.NewCulture);
		Assert.Single(cultures);

		PolEvent evt = cultures[0];
		Assert.Equal("7", evt.Get("culture_id"));
		Assert.Equal("Северяне", evt.Get("culture"));
		Assert.Equal(1, evt.KingdomId);
		Assert.Equal(10, evt.CityId);
	}

	[Fact]
	public void NewReligion_And_NewLanguage_CarryIdAndName()
	{
		PoliticalWorld world = NewWorld();

		world.YearTick(new List<KingdomInput> { Snapshot() });

		PolEvent religion = Assert.Single(world.Bus.OfType(PolEventType.NewReligion));
		Assert.Equal("3", religion.Get("religion_id"));
		Assert.Equal("Культ Света", religion.Get("religion"));

		PolEvent language = Assert.Single(world.Bus.OfType(PolEventType.NewLanguage));
		Assert.Equal("2", language.Get("language_id"));
		Assert.Equal("Северный", language.Get("language"));
	}

	[Fact]
	public void SameCultureId_IsDeduplicated()
	{
		PoliticalWorld world = NewWorld();

		world.YearTick(new List<KingdomInput> { Snapshot() });
		int afterFirst = world.Bus.OfType(PolEventType.NewCulture).Count;

		// Тот же идентификатор культуры — повторного уведомления быть не должно.
		world.SetYear(6);
		world.YearTick(new List<KingdomInput> { Snapshot() });

		Assert.Equal(afterFirst, world.Bus.OfType(PolEventType.NewCulture).Count);
	}

	[Fact]
	public void DifferentCultureId_PublishesNewEvent()
	{
		PoliticalWorld world = NewWorld();

		world.YearTick(new List<KingdomInput> { Snapshot() });
		world.SetYear(6);
		world.YearTick(new List<KingdomInput> { Snapshot("Южане", 8) });

		Assert.Equal(2, world.Bus.OfType(PolEventType.NewCulture).Count);
	}

	[Fact]
	public void BaselineNotRecorded_SilentlyRecordsFirstNovelty_ThenFiresOnNextYear()
	{
		// Загруженный сохранённый мир: BaselineRecorded = false.
		// Первый снимок молча фиксирует существующие культуры/религии/языки.
		var world = new PoliticalWorld();
		world.SetBaseline(false);
		world.SetYear(50);
		world.AddKingdom(1, IdeologyType.Monarchy, 0);
		world.AddCity(10, 1);

		world.YearTick(new List<KingdomInput> { Snapshot() });
		Assert.Empty(world.Bus.OfType(PolEventType.NewCulture));

		// На следующий год появляется новая культура — событие публикуется.
		world.SetYear(51);
		world.YearTick(new List<KingdomInput> { Snapshot("Южане", 8) });

		PolEvent evt = Assert.Single(world.Bus.OfType(PolEventType.NewCulture));
		Assert.Equal("8", evt.Get("culture_id"));
		Assert.Equal("Южане", evt.Get("culture"));
	}

	[Fact]
	public void Render_ReplacesKingdomAndCulturePlaceholders()
	{
		var evt = new PolEvent(PolEventType.NewCulture, "RP_EV_NEW_CULTURE")
			.With("kingdom", "Асгард")
			.With("culture", "Северяне");

		string text = evt.Render("В королевстве {kingdom} основана культура {culture}");

		Assert.Equal("В королевстве Асгард основана культура Северяне", text);
	}

	[Fact]
	public void Render_LeavesUnknownPlaceholdersUntouched()
	{
		var evt = new PolEvent(PolEventType.NewCulture, "RP_EV_NEW_CULTURE")
			.With("culture", "Северяне");

		string text = evt.Render("В королевстве {kingdom} основана культура {culture}");

		Assert.Equal("В королевстве {kingdom} основана культура Северяне", text);
	}
}
