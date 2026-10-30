using System;
using System.Collections.Generic;
using RealPolitic.Core;
using Xunit;

namespace RealPolitic.Tests;

/// <summary>
/// Регрессия «Трагедия дома Вевович» (0.23.11): регент-узурпатор.
///
/// Сценарий из легенды: после смерти короля Даиана Вевовича на трон в 15 лет взошёл
/// его сын Радрек. Ему назначили регента, но регент (Аледы из чужого рода) узурпировал
/// власть, и династия сменилась на Мазекзтка. Это баг: регент — не преемник и не новая
/// династия. Ядро обязано держать законного правителя (наследника) и дом Вевовичей,
/// пока корона не вернётся наследнику в 18 лет.
/// </summary>
public class RegencyUsurpationTests
{
	// ------------------------------------------------------------------ сага

	[Fact]
	public void Regent_DoesNotBecomeRuler_NorShiftDynasty()
	{
		var world = new PoliticalWorld { Rng = new Random(42) };
		world.SetBaseline(true);
		world.SetYear(87);
		KingdomPolitics k = world.AddKingdom(1, IdeologyType.Monarchy, 0);
		world.AddCity(10, 1);

		// Год 87: король Даиан Вевович (зрелый, дом Вевович).
		world.YearTick(new List<KingdomInput> { Snapshot(100, "Даиан", 70, "Вевович", 7, regent: false) });
		Assert.Equal("Вевович", k.RoyalClanName);
		Assert.Equal(100, world.GetKingdom(1).Ruler!.ActorId);

		// Год 88: трон занял регент Аледы из ЧУЖОГО рода (клан 9 «Мазекзтка»).
		// Игровой слой помечает его IsRegent — корона у регента, а не у преемника.
		k.Regency = new RegencyState();
		k.Regency.Start(88, "Аледы", 101);
		k.Regency.HeirActorId = 11;
		k.Regency.HeirName = "Радрек";
		k.Regency.CrownSitsWithRegent = true;

		world.SetYear(88);
		world.YearTick(new List<KingdomInput> { Snapshot(101, "Аледы", 40, "Мазекзтка", 9, regent: true) });

		// Не преемник: события преемственности нет…
		Assert.Empty(world.Bus.OfType(PolEventType.Succession));
		// …династия не сменилась…
		Assert.Empty(world.Bus.OfType(PolEventType.DynasticShift));
		Assert.Equal("Вевович", k.RoyalClanName);
		Assert.Equal(7, k.RoyalClanId);
		// …и законный правитель — прежний король, а не регент.
		Assert.Equal(100, k.Ruler!.ActorId);
		Assert.False(k.Ruler!.IsRegent);

		// Год 106: Радрек достиг 18, корона вернулась наследнику.
		world.SetYear(106);
		world.YearTick(new List<KingdomInput> { Snapshot(11, "Радрек", 18, "Вевович", 7, regent: false) });

		Assert.Equal(11, k.Ruler!.ActorId);
		Assert.Equal("Вевович", k.RoyalClanName); // дом устоял
		Assert.Empty(world.Bus.OfType(PolEventType.DynasticShift));
		var succ = world.Bus.OfType(PolEventType.Succession);
		Assert.Single(succ);
		Assert.Equal("Даиан", succ[0].Get("old_ruler")); // преемник — после настоящего короля
		Assert.False(k.Regency!.IsActive);
	}

	[Fact]
	public void RegentFlag_StopsTickStateFromOverwritingRuler()
	{
		// Непосредственно: TickState не должен класть регента в «правителя» ядра.
		var state = new KingdomPolitics { KingdomId = 1, Ideology = IdeologyType.Monarchy };
		state.Ruler = new RulerRef { ActorId = 100, Name = "Даиан", KingdomId = 1, Age = 70 };
		var input = new KingdomInput
		{
			KingdomId = 1,
			Name = "Ныски",
			Population = 100,
			Warriors = 5,
			Ruler = new RulerRef { ActorId = 101, Name = "Аледы", KingdomId = 1, Age = 40, IsRegent = true },
			Relations = new List<RelationInfo>(),
			Cities = new List<CityInput>()
		};
		var bus = new PolEventBus();

		KingdomSimulation.TickState(state, input, new SimParams(), bus, 88, _ => 0f, _ => IdeologyType.Monarchy, new Random(1));

		Assert.Equal(100, state.Ruler!.ActorId); // Даиан остался «правителем» ядра
		Assert.Equal("Даиан", state.Ruler!.Name);
	}

	// ------------------------------------------------------------------ вспомогательное

	private static KingdomInput Snapshot(int actorId, string name, int age, string clanName, long clanId, bool regent)
	{
		return new KingdomInput
		{
			KingdomId = 1,
			Name = "Ныски",
			Population = 100,
			Warriors = 5,
			Ruler = new RulerRef
			{
				ActorId = actorId,
				Name = name,
				KingdomId = 1,
				SinceYear = 1,
				Age = age,
				ClanName = clanName,
				ClanId = clanId,
				IsRegent = regent
			},
			Cities = new List<CityInput>
			{
				new CityInput { CityId = 10, KingdomId = 1, Population = 100, FoodOk = true, CultureId = 7, CultureName = "C", ReligionId = 3, ReligionName = "R", LanguageId = 2, LanguageName = "L" }
			}
		};
	}
}
