using System.Collections.Generic;
using RealPolitic.Core;
using Xunit;

namespace RealPolitic.Tests;

/// <summary>
/// Регрессия бага «И где сепарация?» (журнал 0.17.1): событие «город отделился»
/// публиковалось, но новое государство не создавалось — рефлексия вызывала
/// makeOwnKingdom/setKingdom с неправильным числом аргументов (в игре у них
/// параметры по умолчанию), и все кандидаты тихо не совпадали.
///
/// Теперь: (1) сепарация реально создаёт королевство и объявляет войну;
/// (2) при недоступном игровом API событие НЕ публикуется и призрак
/// государства в ядре не живёт.
/// </summary>
public class SecessionRegressionTests : System.IDisposable
{
	private static MapBox SetupWorld(bool withKingdomManager)
	{
		var box = new MapBox { map_stats = new MapStats { history_current_year = 120 } };
		if (withKingdomManager)
		{
			box.kingdoms = new KingdomManager { list = new List<Kingdom>() };
		}
		box.cities = new CityManager { list = new List<City>() };
		box.diplomacy = new DiplomacyManager();
		MapBox.instance = box;
		return box;
	}

	private static Kingdom MakeKingdom(MapBox box, long id, string name, params City[] cities)
	{
		var kingdom = new Kingdom { id = id, name = name, cities = new List<City>(cities), capital = cities.Length > 0 ? cities[0] : null };
		foreach (City c in cities)
		{
			c.kingdom = kingdom;
			box.cities.list.Add(c);
		}
		box.kingdoms?.list.Add(kingdom);
		return kingdom;
	}

	public void Dispose()
	{
		MapBox.instance = null;
	}

	// ------------------------------------------------------------------ успех

	[Fact]
	public void Secession_CreatesRealKingdom_MovesCity_AndDeclaresRebelWar()
	{
		MapBox box = SetupWorld(true);
		var capital = new City { id = 101, name = "Столица" };
		var rebel = new City { id = 102, name = "Бунтующий", leader = new Actor { id = 7, name = "Вождь" } };
		Kingdom parent = MakeKingdom(box, 1, "Империя", capital, rebel);

		bool ok = DiplomacyExecutor.TrySecedeCity(parent, rebel, out Kingdom newKingdom);

		Assert.True(ok);
		Assert.NotNull(newKingdom);
		Assert.NotSame(parent, newKingdom);
		Assert.Same(newKingdom, rebel.kingdom);           // город реально перешёл
		Assert.Contains(newKingdom, box.kingdoms.list);   // зарегистрирован — виден в окне «Королевства»
		Assert.DoesNotContain(rebel, parent.cities);
		Assert.Single(box.diplomacy.startedWars);         // объявлена война за независимость
	}

	[Fact]
	public void CivilWarSecession_TransfersAllRebelCities_AndIsRegistered()
	{
		MapBox box = SetupWorld(true);
		var capital = new City { id = 101, name = "Столица" };
		var r1 = new City { id = 102, name = "Город 2", leader = new Actor { id = 7 } };
		var r2 = new City { id = 103, name = "Город 3" };
		Kingdom parent = MakeKingdom(box, 1, "Империя", capital, r1, r2);

		var pending = new PendingSecession
		{
			ParentKingdomId = 1,
			ProvisionalKingdomId = -100,
			LeaderName = "Вождь",
			Year = 120,
			CityIds = new List<int> { 102, 103 }
		};

		bool created = CivilWarExecutor.ExecuteSecession(pending, box, out int gameId);

		Assert.True(created);
		Assert.NotEqual(0, gameId);
		Assert.Same(r1.kingdom, r2.kingdom);
		Assert.NotSame(parent, r1.kingdom);
		Assert.Contains(r1.kingdom, box.kingdoms.list);
		Assert.Single(parent.cities); // у родителя осталась только столица
	}

	[Fact]
	public void Success_Path_PublishesCitySecededEvent()
	{
		MapBox box = SetupWorld(true);
		var capital = new City { id = 101, name = "Столица" };
		var rebel = new City { id = 102, name = "Бунтующий", leader = new Actor { id = 7 } };
		Kingdom parent = MakeKingdom(box, 1, "Империя", capital, rebel);

		var savedWorld = RealPoliticMod.World;
		try
		{
			RealPoliticMod.World = new PoliticalWorld();
			bool seceded = IdeologyMechanicsEffects.TrySecedeRebelCity(parent, null, "RP_SECESSION_INTERREGNUM");

			Assert.True(seceded);
			var evts = RealPoliticMod.World.Bus.OfType(PolEventType.CitySeceded);
			var evt = Assert.Single(evts);
			Assert.Equal("Бунтующий", evt.Get("city"));
			Assert.Equal("Империя", evt.Get("kingdom"));
		}
		finally
		{
			RealPoliticMod.World = savedWorld;
		}
	}

	// ------------------------------------------------------------------ отказ

	[Fact]
	public void Failure_Path_DoesNotPublishEvent_AndReturnsFalse()
	{
		// Без менеджера королевств игровой «мир» не может создать новое государство:
		// раньше текст «город отделился» показывался всё равно.
		SetupWorld(false);
		var capital = new City { id = 101, name = "Столица" };
		var rebel = new City { id = 102, name = "Бунтующий" };
		Kingdom parent = new Kingdom { id = 1, name = "Империя", cities = new List<City> { capital, rebel } };
		capital.kingdom = parent;
		rebel.kingdom = parent;

		var savedWorld = RealPoliticMod.World;
		try
		{
			RealPoliticMod.World = new PoliticalWorld();
			bool seceded = IdeologyMechanicsEffects.TrySecedeRebelCity(parent, null, "RP_SECESSION_INTERREGNUM");

			Assert.False(seceded);
			Assert.Empty(RealPoliticMod.World.Bus.OfType(PolEventType.CitySeceded));
			Assert.Same(parent, rebel.kingdom); // город остался у родителя
		}
		finally
		{
			RealPoliticMod.World = savedWorld;
		}
	}

	[Fact]
	public void Failure_Path_CoreAbort_RemovesPhantomKingdom_Immediately()
	{
		var world = new PoliticalWorld();
		world.SetYear(120);
		var parentState = world.AddKingdom(1, IdeologyType.Monarchy, 0);
		world.CreateBreakawayKingdom(-100, parentState, new List<int> { 102 }, IdeologyType.Monarchy, 120);

		Assert.NotNull(world.GetKingdom(-100));

		bool removed = world.AbortSecession(-100, 1, new List<int> { 102 });

		Assert.True(removed);
		Assert.Null(world.GetKingdom(-100));
	}

	// ----------------------------------------------------- регрессия 0.17.2

	[Fact]
	public void Secession_LeaderlessCity_AppointsAdultResident_AndSucceeds()
	{
		// Регрессия мира на 300 лет (0.17.2): не-столицы в игре живут без лидера,
		// makeNewCivKingdom(null) падал с NullReference внутри игры, и все 5
		// попыток сепарации честно, но бесполезно отклонялись.
		// Теперь старший взрослый житель города ставится лидером до вызова.
		MapBox box = SetupWorld(true);
		var capital = new City { id = 101, name = "Столица" };
		var kid = new Actor { id = 11, name = "Ребёнок", age = 5 };
		var elder = new Actor { id = 12, name = "Старейшина", age = 41 };
		var rebel = new City { id = 102, name = "Безлидерный", units = new List<Actor> { kid, elder } };
		Kingdom parent = MakeKingdom(box, 1, "Империя", capital, rebel);

		bool ok = DiplomacyExecutor.TrySecedeCity(parent, rebel, out Kingdom newKingdom);

		Assert.True(ok);
		Assert.NotNull(newKingdom);
		Assert.Same(elder, rebel.leader);             // лидер назначен из жителей
		Assert.Contains(newKingdom, box.kingdoms.list);
		Assert.DoesNotContain(rebel, parent.cities);
	}

	[Fact]
	public void Secession_EmptyLeaderlessCity_ReturnsFalse_NoPhantomEvent()
	{
		// В городе без взрослых жителей сепарация невозможна: отказ без события.
		MapBox box = SetupWorld(true);
		var capital = new City { id = 101, name = "Столица" };
		var rebel = new City { id = 102, name = "Пустырь", units = new List<Actor>() };
		Kingdom parent = MakeKingdom(box, 1, "Империя", capital, rebel);

		var savedWorld = RealPoliticMod.World;
		try
		{
			RealPoliticMod.World = new PoliticalWorld();
			bool ok = DiplomacyExecutor.TrySecedeCity(parent, rebel, out Kingdom newKingdom);

			Assert.False(ok);
			Assert.Null(newKingdom);
			Assert.Same(parent, rebel.kingdom);
		}
		finally
		{
			RealPoliticMod.World = savedWorld;
		}
	}

	[Fact]
	public void Secession_NeverPicksRealCapital_EvenIfNotFirstInList()
	{
		// Регрессия 0.17.0: «Касин отделился от Сливанска» — столицей оказался
		// не cities[0], а настоящее поле Kingdom.capital. Порядок списка —
		// столица в конце, кандидатом должен стать не-столичный город.
		MapBox box = SetupWorld(true);
		var province = new City { id = 101, name = "Провинция", units = new List<Actor> { new Actor { id = 21, name = "Старейшина", age = 33 } } };
		var capital = new City { id = 102, name = "Столица" };
		Kingdom parent = MakeKingdom(box, 1, "Империя", province, capital);
		parent.capital = capital; // столица — последняя в списке

		var savedWorld = RealPoliticMod.World;
		try
		{
			RealPoliticMod.World = new PoliticalWorld();
			bool seceded = IdeologyMechanicsEffects.TrySecedeRebelCity(parent, null, "RP_SECESSION_INTERREGNUM");

			Assert.True(seceded);
			Assert.NotSame(parent, province.kingdom); // отделяется провинция, не столица
			Assert.Same(parent, capital.kingdom);     // столица осталась у родителя
			var evt = Assert.Single(RealPoliticMod.World.Bus.OfType(PolEventType.CitySeceded));
			Assert.Equal("Провинция", evt.Get("city"));
		}
		finally
		{
			RealPoliticMod.World = savedWorld;
		}
	}

}
