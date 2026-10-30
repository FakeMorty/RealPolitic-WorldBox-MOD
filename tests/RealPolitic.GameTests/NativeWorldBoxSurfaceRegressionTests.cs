using System.Collections.Generic;
using RealPolitic;
using RealPolitic.Core;
using Xunit;

namespace RealPolitic.GameTests;

public sealed class NativeWorldBoxSurfaceRegressionTests
{
    private sealed class FakeTile
    {
        public int x;
        public int y;
    }

    private struct FakePoint
    {
        public float x { get; set; }
        public float y { get; set; }
    }

    private sealed class Native051Kingdom : Kingdom
    {
        public bool EnemiesFlag = false;
        public readonly HashSet<Kingdom> WarTargets = new();
        public int PeoplePopulation;
        public int MaxWarriorLimit;
        public string? NativeSpecies;

        public Native051Kingdom()
        {
            cities = new List<City>();
            units = new List<Actor>();
            data = new KingdomData();
        }

        public bool hasEnemies() => EnemiesFlag || WarTargets.Count > 0;
        public bool isInWarWith(Kingdom other) => other != null && WarTargets.Contains(other);
        public int getPopulationPeople() => PeoplePopulation;
        public int countWarriorsMax() => MaxWarriorLimit;
        public string? getSpecies() => NativeSpecies;
    }

    private sealed class Native051City : City
    {
        public readonly Dictionary<string, int> Stock = new();
        public readonly HashSet<Kingdom> neighbours_kingdoms = new();
        public FakeTile? _city_tile;
        public FakePoint city_center;
        public string? CurrentSpecies;
        public int JoinCalls;
        public int SetKingdomCalls;
        public int UpdateTotalFoodCalls;

        public Native051City()
        {
            units = new List<Actor>();
            data = new CityData();
        }

        public int getResourcesAmount(string id) => Stock.TryGetValue(id, out int val) ? val : 0;

        public int takeResource(string id, int amount)
        {
            int have = getResourcesAmount(id);
            int taken = amount > have ? have : amount;
            Stock[id] = have - taken;
            return taken;
        }

        public int addResourcesToRandomStockpile(string id, int amount = 1)
        {
            Stock[id] = getResourcesAmount(id) + amount;
            return Stock[id];
        }

        public void updateTotalFood() => UpdateTotalFoodCalls++;

        public string? getCurrentSpecies() => CurrentSpecies;

        public FakeTile? getTile(bool pForceCenter = false) => _city_tile;

        public new void joinAnotherKingdom(Kingdom target, bool pCaptured = false, bool pRebellion = false)
        {
            JoinCalls++;
            kingdom = target;
        }

        public new void setKingdom(Kingdom target, bool pFromLoad = false)
        {
            SetKingdomCalls++;
            kingdom = target;
        }
    }

    private sealed class Native051BoatAsset
    {
        public string id = "boat_trading_human";
        public bool is_boat = true;
        public bool civ = false;
    }

    private sealed class Native051HumanAsset
    {
        public string id = "human";
        public bool is_boat = false;
        public bool civ = true;
    }

    private sealed class Native051Actor : Actor
    {
        public object? asset;
        public bool BoatFlag;
        public int AgeYears = 25;

        public bool isAlive() => true;
        public bool isBoat() => BoatFlag;
        public int getAge() => AgeYears;
    }

    private sealed class Native051HappinessData : ActorData
    {
        public int happiness = 50;
    }

    [Fact]
    public void WarChecks_Support_Native051_HasEnemies_And_IsInWarWith()
    {
        var k1 = new Native051Kingdom { id = 1, name = "A" };
        var k2 = new Native051Kingdom { id = 2, name = "B" };
        var k3 = new Native051Kingdom { id = 3, name = "C" };
        k1.WarTargets.Add(k2);

        Assert.True(GameRefs.IsAtWar(k1));
        Assert.False(GameRefs.IsAtWar(k3));
        Assert.True(GameRefs.AreKingdomsAtWar(k1, k2));
        Assert.False(GameRefs.AreKingdomsAtWar(k1, k3));
    }

    [Fact]
    public void NegativeGoldDelta_ClampsAgainstStockpile_And_NeverProducesNegativeResource()
    {
        var kingdom = new Native051Kingdom { id = 10, name = "GoldTest" };
        var capital = new Native051City
        {
            id = 100,
            name = "Capital",
            kingdom = kingdom,
            data = new CityData { id = 100, name = "Capital" }
        };
        capital.Stock["gold"] = 12;
        kingdom.capital = capital;
        kingdom.cities.Add(capital);

        var report = new EconomyReport
        {
            GoldDelta = -50f,
            FoodDelta = 10f
        };

        Assert.True(EconomyExecutor.ApplyReport(kingdom, report));
        Assert.Equal(0, capital.getResourcesAmount("gold"));
        Assert.True(GameRefs.TryKingdomResource(kingdom, "gold", out float remainingGold));
        Assert.Equal(0f, remainingGold);
        Assert.Equal(10, capital.getResourcesAmount("bread"));
        Assert.True(capital.UpdateTotalFoodCalls > 0);
    }

    [Fact]
    public void IntResourceReturn_And_WarCheck_WorkIn_DevelopTrade_In_Native051()
    {
        var map = new MapBox { kingdoms = new KingdomManager { list = new List<Kingdom>() } };
        var k1 = new Native051Kingdom { id = 20, name = "Trader" };
        var k2 = new Native051Kingdom { id = 21, name = "Enemy" };
        var k3 = new Native051Kingdom { id = 22, name = "PeacePartner" };
        k1.WarTargets.Add(k2);

        var cap1 = new Native051City { id = 200, name = "Port1", kingdom = k1, data = new CityData { id = 200, name = "Port1" } };
        var cap2 = new Native051City { id = 201, name = "Port2", kingdom = k2, data = new CityData { id = 201, name = "Port2" } };
        var cap3 = new Native051City { id = 202, name = "Port3", kingdom = k3, data = new CityData { id = 202, name = "Port3" } };
        cap1.Stock["gold"] = 80;
        k1.capital = cap1;
        k1.cities.Add(cap1);
        k2.capital = cap2;
        k2.cities.Add(cap2);
        k3.capital = cap3;
        k3.cities.Add(cap3);

        map.kingdoms.list.Add(k1);
        map.kingdoms.list.Add(k2);
        map.kingdoms.list.Add(k3);

        var world = new PoliticalWorld();
        var state1 = world.AddKingdom(20, IdeologyType.Democracy, 1);
        world.AddKingdom(21, IdeologyType.Monarchy, 1);
        world.AddKingdom(22, IdeologyType.Democracy, 1);

        var simParams = new SimParams();
        bool ok = EconomyExecutor.DevelopTrade(map, k1, state1, world, 1, simParams, out _);

        Assert.True(ok);
        Assert.True(cap1.getResourcesAmount("gold") < 80);
        var routes = world.Trade.Of(20);
        Assert.Single(routes);
        Assert.True(routes[0].Involves(22));
        Assert.False(routes[0].Involves(21));
    }

    [Fact]
    public void SpeciesAndMaxWarriors_Read_Native051_Methods()
    {
        var kingdom = new Native051Kingdom
        {
            id = 30,
            name = "EthnoState",
            NativeSpecies = "human",
            MaxWarriorLimit = 42
        };
        var sameSpeciesCity = new Native051City { id = 301, kingdom = kingdom, CurrentSpecies = "human" };
        var foreignSpeciesCity = new Native051City { id = 302, kingdom = kingdom, CurrentSpecies = "orc" };

        Assert.Equal("human", GameRefs.KingdomSpeciesId(kingdom));
        Assert.Equal("human", GameRefs.CitySpeciesId(sameSpeciesCity));
        Assert.Equal("orc", GameRefs.CitySpeciesId(foreignSpeciesCity));
        Assert.Equal(42, GameRefs.KingdomMaxWarriors(kingdom));
    }

    [Fact]
    public void Boats_AreExcluded_From_Population_Electorate_And_AdultCandidates()
    {
        var kingdom = new Native051Kingdom { id = 40, name = "MaritimeRepublic", PeoplePopulation = 0 };
        var city = new Native051City
        {
            id = 400,
            name = "Port",
            kingdom = kingdom,
            data = new CityData { id = 400, name = "Port" }
        };
        kingdom.capital = city;
        kingdom.cities.Add(city);

        var boat = new Native051Actor
        {
            id = 4001,
            kingdom = kingdom,
            city = city,
            BoatFlag = true,
            AgeYears = 120,
            asset = new Native051BoatAsset(),
            data = new ActorData { id = 4001, name = "«Святой Пётр»" }
        };
        var citizen = new Native051Actor
        {
            id = 4002,
            kingdom = kingdom,
            city = city,
            BoatFlag = false,
            AgeYears = 30,
            asset = new Native051HumanAsset(),
            data = new ActorData { id = 4002, name = "Иван" }
        };

        city.units.Add(boat);
        city.units.Add(citizen);
        kingdom.units.Add(boat);
        kingdom.units.Add(citizen);

        Assert.True(GameRefs.IsBoatUnit(boat));
        Assert.False(GameRefs.IsBoatUnit(citizen));
        Assert.Equal(1, GameRefs.KingdomPopulation(kingdom));
        Assert.Same(citizen, GameRefs.FindAdultResident(city));
        Assert.Same(citizen, GameRefs.TryPickAdultUnit(kingdom, excludeId: 0));

        PoliticalWorld previousWorld = RealPoliticMod.World;
        try
        {
            var world = new PoliticalWorld();
            RealPoliticMod.World = world;
            world.BeginNewWorldGovernance("native051-boat-test");
            var state = world.AddKingdom(40, IdeologyType.Democracy, 1);
            var chapter = state.Constitution.Electoral;
            Assert.True(CivicElectorateReader.TryReadCitizens(world, kingdom, chapter, out List<CivicElectorateReader.Elector> electors, out string reason), reason);
            Assert.Single(electors);
            Assert.Same(citizen, electors[0].Actor);
        }
        finally
        {
            RealPoliticMod.World = previousWorld;
        }
    }

    [Fact]
    public void MoveCityToKingdom_StopsAfterSuccessfulJoinAnotherKingdom()
    {
        var oldKingdom = new Native051Kingdom { id = 50, name = "Old" };
        var newKingdom = new Native051Kingdom { id = 51, name = "New" };
        var city = new Native051City { id = 500, name = "BorderTown", kingdom = oldKingdom };

        CivilWarExecutor.MoveCityToKingdom(city, newKingdom);

        Assert.Same(newKingdom, city.kingdom);
        Assert.Equal(1, city.JoinCalls);
        Assert.Equal(0, city.SetKingdomCalls);
    }

    [Fact]
    public void CityTile_And_NeighboursKingdoms_Support_Native051_Fields()
    {
        var k1 = new Native051Kingdom { id = 60, name = "K1" };
        var k2 = new Native051Kingdom { id = 61, name = "K2" };
        var tile = new FakeTile { x = 14, y = 28 };
        var city1 = new Native051City { id = 600, kingdom = k1, _city_tile = tile, city_center = new FakePoint { x = 14f, y = 28f } };
        var city2 = new Native051City { id = 601, kingdom = k2, city_center = new FakePoint { x = 20f, y = 30f } };
        city1.neighbours_kingdoms.Add(k2);
        k1.cities.Add(city1);
        k2.cities.Add(city2);

        var map = new MapBox { kingdoms = new KingdomManager { list = new List<Kingdom>() } };
        map.kingdoms.list.Add(k1);
        map.kingdoms.list.Add(k2);

        Assert.Same(tile, GameRefs.CityTile(city1));

        var relations = new List<RelationInfo>();
        GameRefs.CollectRelations(map, k1, relations);
        Assert.Single(relations);
        Assert.Equal(61, relations[0].OtherId);
        Assert.True(relations[0].Adjacent);
    }

    [Fact]
    public void ApplyHappiness_Updates_Native051_ActorDataHappiness_WithClamping()
    {
        var data = new Native051HappinessData { id = 7001, happiness = 95 };
        var actor = new Native051Actor { id = 7001, data = data };

        IdeologyEffects.ApplyHappiness(actor, 15);
        Assert.Equal(100, data.happiness);

        IdeologyEffects.ApplyHappiness(actor, -140);
        Assert.Equal(0, data.happiness);
    }

    [Fact]
    public void GameNotifier_Localizes_RP28_DemandTaxes_And_Outcome_Keys()
    {
        var bus = new PolEventBus();
        var center = new NotificationCenter(bus);
        var notifier = new GameNotifier(bus, center);

        var evt = new PolEvent(PolEventType.CivicUltimatumResolved, "RP28_EV_ULTIMATUM")
        {
            Year = 75,
            KingdomId = 1,
            Severity = Severity.Major
        }.With("kingdom", "Новгород")
         .With("demand", "RP28_DEMAND_TAXES")
         .With("outcome", "RP28_MOVEMENT_CONCEDED")
         .With("support", "44");

        string rendered = notifier.RenderEventText(evt);
        Assert.DoesNotContain("RP28_", rendered);
        Assert.Contains("Снижение налогов и отмена режима экономии", rendered);
        Assert.Contains("власть пошла на уступки и провела реформу", rendered);
    }

    private sealed class Native051AllianceWithHashset
    {
        public long id = 91;
        public List<Kingdom> kingdoms_list = new List<Kingdom>(); // Пустой до следующего кадра checkLists()
        public HashSet<Kingdom> kingdoms_hashset = new HashSet<Kingdom>();
    }

    [Fact]
    public void AllianceSafety_Members_PrefersPopulatedHashset_When_KingdomsList_IsStaleEmpty()
    {
        var k1 = new Native051Kingdom { id = 81, name = "A" };
        var k2 = new Native051Kingdom { id = 82, name = "B" };
        var alliance = new Native051AllianceWithHashset();
        alliance.kingdoms_hashset.Add(k1);
        alliance.kingdoms_hashset.Add(k2);

        var members = AllianceSafety.Members(alliance);
        Assert.NotNull(members);
        var list = new List<Kingdom>();
        foreach (object item in members)
        {
            if (item is Kingdom k) list.Add(k);
        }
        Assert.Equal(2, list.Count);
        Assert.Contains(k1, list);
        Assert.Contains(k2, list);
    }

    [Fact]
    public void ChronicleEdition_DoesNotAppendLegacy3TermLimit_To_IndividualBallotElections()
    {
        var world = new PoliticalWorld();
        var evt = new PolEvent(PolEventType.DemocraticElection, "RP_GOV26_EV_HEAD_ELECTED")
        {
            Year = 99,
            KingdomId = 1,
            ActorId = 2641
        }.With("kingdom", "Яроградия")
         .With("ruler", "Алда")
         .With("election_kind", "individual-ballots")
         .With("votes", 10)
         .With("eligible", 17)
         .With("terms", 4)
         .With("next_election", 111);

        string baseLine = "Яроградия: Избран глава государства Алда — 10 из 17 голосов (сроков: 4, следующие выборы: 111).";
        string explained = ChronicleEdition.Explain(evt, baseLine, world);
        Assert.DoesNotContain("лимит 3 срока за жизнь", explained);
    }
}
