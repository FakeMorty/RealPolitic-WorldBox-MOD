using System.Collections.Generic;
using RealPolitic.Core;
using Xunit;

namespace RealPolitic.Tests;

/// <summary>
/// Регрессия «Трагедия дома Вевович» (0.23.11) на игровом слое: выбор регента.
///
/// Раньше, когда у малолетнего наследника не находилось взрослой родни, регентом
/// назначался ЛЮБОЙ взрослый житель столицы (канцлер/наместник) — корона физически
/// уходила постороннему клану, и регент-узурпатор свергал дом наследника
/// (Аледы из чужого рода сменил дом Вевович на Мазекзтка). Теперь регент — только
/// взрослая родня наследника; нет родни — наследник правит сам под регентским советом.
/// </summary>
public class RegentPickerRegressionTests : System.IDisposable
{
	private static MapBox SetupWorld(Kingdom kingdom)
	{
		var box = new MapBox { map_stats = new MapStats { history_current_year = 88 } };
		box.kingdoms = new KingdomManager { list = new List<Kingdom> { kingdom } };
		box.cities = new CityManager { list = new List<City>() };
		MapBox.instance = box;
		return box;
	}

	private static Actor MakeActor(long id, int age, string name, long clanId)
	{
		return new Actor
		{
			id = id,
			name = name,
			age = age,
			data = new ActorData
			{
				id = id,
				clan = clanId != 0 ? new Clan { id = clanId, name = "Род" } : null
			}
		};
	}

	public void Dispose()
	{
		MapBox.instance = null;
	}

	[Fact]
	public void Regent_IsChosenFromRoyalClan_WhenAdultRelativeExists()
	{
		// Королевский дом Вевович (клан 7). Наследник Радрек — 15 лет.
		Actor heir = MakeActor(11, 15, "Радрек", clanId: 7);
		Actor royalAdult = MakeActor(12, 34, "Влеры", clanId: 7);   // взрослый соклановец
		Actor outsider = MakeActor(13, 40, "Аледы", clanId: 9);      // чужой род
		var kingdom = new Kingdom
		{
			id = 1,
			name = "Ныски",
			king = heir,
			cities = new List<City>(),
			units = new List<Actor> { heir, royalAdult, outsider }
		};
		SetupWorld(kingdom);

		var world = new PoliticalWorld();
		world.SetYear(88);
		KingdomPolitics state = world.AddKingdom(1, IdeologyType.Monarchy, 0);
		state.RoyalClanId = 7;
		state.RoyalClanName = "Вевович";

		GameRefs.RefreshRulers(MapBox.instance, world);

		// Регент — взрослый член королевского дома, а не посторонний.
		Assert.Same(royalAdult, kingdom.king);
		Assert.NotSame(outsider, kingdom.king);
		// Маркер регентa доходит до ядра через RulerRef.
		Assert.True(state.Ruler!.IsRegent);
		// Наследник записан и ждёт 18 лет.
		Assert.True(state.Regency!.CrownSitsWithRegent);
		Assert.Equal(11, state.Regency.HeirActorId);
	}

	[Fact]
	public void Regent_IsNeverOutsider_WhenNoAdultRelativeExists()
	{
		// У наследника нет совершеннолетней родни в королевстве — только чужой взрослый.
		// Регент-узурпатор не должен получить корону: наследник правит сам до 18 лет.
		Actor heir = MakeActor(11, 15, "Радрек", clanId: 7);
		Actor outsider = MakeActor(13, 40, "Аледы", clanId: 9);
		var kingdom = new Kingdom
		{
			id = 1,
			name = "Ныски",
			king = heir,
			cities = new List<City>(),
			units = new List<Actor> { heir, outsider }
		};
		SetupWorld(kingdom);

		var world = new PoliticalWorld();
		world.SetYear(88);
		KingdomPolitics state = world.AddKingdom(1, IdeologyType.Monarchy, 0);
		state.RoyalClanId = 7;
		state.RoyalClanName = "Вевович";

		GameRefs.RefreshRulers(MapBox.instance, world);

		// Корона не ушла постороннему: на троне остался наследник.
		Assert.Same(heir, kingdom.king);
		Assert.NotSame(outsider, kingdom.king);
		// И это не «корона у регента» — регент-узурпатор не появился.
		Assert.False(state.Ruler!.IsRegent);
		Assert.True(state.Regency == null);
	}

	[Fact]
	public void Crown_ReturnsToHeir_WhenHeReachesMajority()
	{
		// Полный цикл: корона у регента, наследнику исполняется 18 — корона возвращается.
		Actor heir = MakeActor(11, 18, "Радрек", clanId: 7);
		Actor regent = MakeActor(12, 34, "Влеры", clanId: 7);
		var kingdom = new Kingdom
		{
			id = 1,
			name = "Ныски",
			king = regent,
			cities = new List<City>(),
			units = new List<Actor> { heir, regent }
		};
		SetupWorld(kingdom);

		var world = new PoliticalWorld();
		world.SetYear(106);
		KingdomPolitics state = world.AddKingdom(1, IdeologyType.Monarchy, 0);
		state.RoyalClanId = 7;
		state.RoyalClanName = "Вевович";
		state.Regency = new RegencyState();
		state.Regency.Start(88, "Влеры", 12);
		state.Regency.HeirActorId = 11;
		state.Regency.HeirName = "Радрек";
		state.Regency.CrownSitsWithRegent = true;
		state.Ruler = new RulerRef { ActorId = 12, Name = "Влеры", KingdomId = 1, Age = 34, IsRegent = true };

		GameRefs.RefreshRulers(MapBox.instance, world);

		// Корона вернулась наследнику…
		Assert.Same(heir, kingdom.king);
		// …и ядро сразу видит законного правителя (а не «смену власти»).
		Assert.False(state.Ruler!.IsRegent);
		Assert.Equal(11, state.Ruler!.ActorId);
		Assert.False(state.Regency!.CrownSitsWithRegent);
		Assert.False(state.Regency!.IsActive);
	}
}
