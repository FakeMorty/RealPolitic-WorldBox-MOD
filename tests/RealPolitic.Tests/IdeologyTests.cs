using System;
using System.Collections.Generic;
using RealPolitic.Core;
using Xunit;

namespace RealPolitic.Tests;

public class IdeologyTests
{
	[Fact]
	public void DescriptionAndIconKeys_AreDerivedFromName()
	{
		Assert.Equal("RP_IDEOLOGY_MONARCHY_DESC", Ideologies.DescriptionKey(IdeologyType.Monarchy));
		Assert.Equal("rp_ideology_monarchy", Ideologies.IconKey(IdeologyType.Monarchy));
		Assert.Equal("rp_ideology_merchantrepublic", Ideologies.IconKey(IdeologyType.MerchantRepublic));
	}

	[Fact]
	public void SuccessorIdeology_KeepsCurrentWhenRulerIsUnclear()
	{
		// В 0.23 выбор преемника детерминирован: без данных об убеждениях нового
		// правителя (rulerIdeology == null) строй державы сохраняется.
		Assert.Equal(IdeologyType.Monarchy, ActorSimulation.SuccessorIdeology(IdeologyType.Monarchy, null, IdeologyEpoch.Modern));
		Assert.Equal(IdeologyType.Tribalism, ActorSimulation.SuccessorIdeology(IdeologyType.Tribalism, null, IdeologyEpoch.DarkAges));
	}

	[Fact]
	public void SuccessorIdeology_AdoptsAllowedRulerIdeology()
	{
		Assert.Equal(IdeologyType.Communism, ActorSimulation.SuccessorIdeology(IdeologyType.Monarchy, IdeologyType.Communism, IdeologyEpoch.Modern));
		Assert.Equal(IdeologyType.Democracy, ActorSimulation.SuccessorIdeology(IdeologyType.Monarchy, IdeologyType.Democracy, IdeologyEpoch.EarlyModern));
		// Анахронизм отсеивается: король-коммунист в тёмных веках не сменит строй монархии.
		Assert.Equal(IdeologyType.Monarchy, ActorSimulation.SuccessorIdeology(IdeologyType.Monarchy, IdeologyType.Communism, IdeologyEpoch.DarkAges));
	}

	[Fact]
	public void Succession_SetsStateIdeologyToNewKingsIdeology()
	{
		var world = new PoliticalWorld();
		world.SetYear(300); // эпоха Модерн: допустим любой строй
		world.AddKingdom(1, IdeologyType.Monarchy, 0);
		world.AddCity(10, 1);

		world.YearTick(new List<KingdomInput> { Snapshot(100, "King1") });
		Assert.Equal(IdeologyType.Monarchy, world.GetKingdom(1)!.Ideology);

		// Новый король — зарегистрированный житель-коммунист: его убеждения и принимает держава.
		world.RegisterActor(101, 1, 10, new ActorPolitics(IdeologyType.Communism, ReformStance.Radical));
		world.SetYear(301);
		world.YearTick(new List<KingdomInput> { Snapshot(101, "King2") });

		Assert.Equal(IdeologyType.Communism, world.GetKingdom(1)!.Ideology);
		Assert.Single(world.Bus.OfType(PolEventType.KingdomAdoptedIdeology));
	}

	private static KingdomInput Snapshot(int rulerActorId, string rulerName)
	{
		return new KingdomInput
		{
			KingdomId = 1,
			Name = "A",
			Population = 100,
			Warriors = 5,
			Ruler = new RulerRef { ActorId = rulerActorId, Name = rulerName, KingdomId = 1, SinceYear = 1 },
			Cities = new List<CityInput>
			{
				new CityInput { CityId = 10, KingdomId = 1, Population = 100, FoodOk = true, CultureId = 7, CultureName = "C", ReligionId = 3, ReligionName = "R", LanguageId = 2, LanguageName = "L" }
			}
		};
	}
}
