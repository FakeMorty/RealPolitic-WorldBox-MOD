using RealPolitic.Core;
using Xunit;

namespace RealPolitic.Tests;

public sealed class NationalLiberationTests
{
    [Fact]
    public void CitiesRememberTheirOriginalMotherlandAfterConquestAndRestoreItDuringCrisis()
    {
        var world = new PoliticalWorld();
        var empire = world.AddKingdom(1, IdeologyType.Militarism, 10);
        var homeland = world.AddKingdom(2, IdeologyType.Democracy, 10);

        // В начале истории города 101, 102 принадлежат державе #2
        world.AddCity(100, 1);
        world.AddCity(101, 2);
        world.AddCity(102, 2);
        world.CityHomelands.EnsureRecorded(101, 2, "Ирландия", IdeologyType.Democracy, isCapital: true);
        world.CityHomelands.EnsureRecorded(102, 2, "Ирландия", IdeologyType.Democracy);

        // Держава #1 захватывает города 101 и 102, а держава #2 исчезает с карты
        world.GetCity(101)!.KingdomId = 1;
        world.GetCity(102)!.KingdomId = 1;
        world.RemoveKingdom(2);

        // При высокой военной усталости в империи начинается национально-освободительное восстание
        empire.WarFatigue = 65f;
        NationalLiberationEngine.TickKingdom(empire, world, 80);

        Assert.Single(world.PendingRestorations);
        var order = world.PendingRestorations[0];
        Assert.Equal("Ирландия", order.DefeatedName);
        Assert.Equal(2, order.DefeatedId);
        Assert.Equal(1, order.CitiesKeptByVictor);
        Assert.Contains(101, order.PrewarCityIds);
        Assert.Contains(102, order.PrewarCityIds);
    }

    [Fact]
    public void DemocraticMetropolisRecognizesAbsorbedVassalLandsWithoutRestoringIndependence()
    {
        var world = new PoliticalWorld();
        var republic = world.AddKingdom(1, IdeologyType.Democracy, 10);
        world.AddCity(100, 1);
        world.AddCity(101, 1);
        world.CityHomelands.Restore(new CityHomelandRecord
        {
            CityId = 101,
            OriginalKingdomId = 5,
            OriginalKingdomName = "Шотландия",
            OriginalIdeology = IdeologyType.Democracy,
            WasHistoricalCapital = true,
            AnnexedYear = 30
        });

        republic.AvgDissent = 0.35f;
        NationalLiberationEngine.TickKingdom(republic, world, 90);

        // Мирный путь больше не создаёт отдельную страну: приказ на отделение отсутствует,
        // город остаётся у метрополии, а старая Шотландия запомнена как вассальные земли.
        Assert.Empty(world.PendingRestorations);
        Assert.Equal(1, world.GetCity(101)!.KingdomId);
        Union union = Assert.IsType<Union>(world.UnionOf(5));
        Assert.Equal(UnionType.Vassal, union.Type);
        Assert.Equal(1, union.LeaderId);
        UnionMember member = Assert.IsType<UnionMember>(union.GetMember(5));
        Assert.Equal(101, member.CapitalCityId);
        Assert.Contains(101, member.CityIds);
        Assert.Equal("Шотландия", member.OriginalName);
        Assert.Equal(IdeologyType.Democracy, member.OriginalIdeology);
        Assert.True(world.IsAbsorbedHomelandCity(1, 101));
        Assert.False(world.IsAbsorbedHomelandCity(1, 100));
        Assert.Equal(10, world.DiplomaticMemories.GetTrustScore(5, 1, 90));
        Assert.Contains(world.Bus.OfType(PolEventType.NationalLiberationUprising),
            e => e.Get("mode") == "RP29_LIBERATION_VASSAL");
    }

    [Fact]
    public void SevereCrisisInDemocraticMetropolisStillStartsArmedLiberation()
    {
        var world = new PoliticalWorld();
        var republic = world.AddKingdom(1, IdeologyType.Democracy, 10);
        world.AddCity(100, 1);
        world.AddCity(101, 1);
        world.CityHomelands.Restore(new CityHomelandRecord
        {
            CityId = 101,
            OriginalKingdomId = 5,
            OriginalKingdomName = "Шотландия",
            OriginalIdeology = IdeologyType.Democracy,
            WasHistoricalCapital = true,
            AnnexedYear = 30
        });

        // Тяжёлый кризис — это уже восстание, а не мирное признание вассальных земель.
        republic.AvgDissent = 0.60f;
        NationalLiberationEngine.TickKingdom(republic, world, 90);

        RestorationOrder order = Assert.Single(world.PendingRestorations);
        Assert.True(order.IsNationalLiberation);
        Assert.False(order.LiberationPeaceful);
        Assert.Contains(world.Bus.OfType(PolEventType.NationalLiberationUprising),
            e => e.Get("mode") == "RP29_LIBERATION_WAR");
    }

    [Fact]
    public void SaveDataRoundtripPreservesCityHomelands()
    {
        var world = new PoliticalWorld();
        world.AddKingdom(1, IdeologyType.Monarchy, 10);
        world.AddCity(100, 1);
        world.CityHomelands.Restore(new CityHomelandRecord
        {
            CityId = 100,
            OriginalKingdomId = 7,
            OriginalKingdomName = "Новгород",
            OriginalIdeology = IdeologyType.MerchantRepublic,
            WasHistoricalCapital = true,
            AnnexedYear = 44
        });

        string json = SaveData.Write(world);
        var restored = new PoliticalWorld();
        SaveData.Apply(restored, json);

        var rec = restored.CityHomelands.Get(100);
        Assert.NotNull(rec);
        Assert.Equal(7, rec!.OriginalKingdomId);
        Assert.Equal("Новгород", rec.OriginalKingdomName);
        Assert.Equal(IdeologyType.MerchantRepublic, rec.OriginalIdeology);
        Assert.True(rec.WasHistoricalCapital);
        Assert.Equal(44, rec.AnnexedYear);
    }

    [Fact]
    public void NationalLiberationNeverSpawnsDuplicateKingdomNameOrSelfLiberation()
    {
        var world = new PoliticalWorld();
        var occupier = world.AddKingdom(10, IdeologyType.Monarchy, 10);
        var livingNamesake = world.AddKingdom(20, IdeologyType.Monarchy, 10);
        var names = (System.Collections.Generic.Dictionary<int, string>)typeof(PoliticalWorld)
            .GetField("_kingdomNames", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!
            .GetValue(world)!;
        names[10] = "Новосулимания";
        names[20] = "Белолугия";

        world.AddCity(100, 10);
        world.AddCity(101, 10);
        world.AddCity(102, 10);

        // Город 101 когда-то числился за погибшей тёзкой самой метрополии (#3, «Новосулимания»)
        world.CityHomelands.Restore(new CityHomelandRecord
        {
            CityId = 101,
            OriginalKingdomId = 3,
            OriginalKingdomName = "Новосулимания",
            OriginalIdeology = IdeologyType.Monarchy
        });
        // Город 102 числился за погибшим id #4, но держава с именем «Белолугия» (#20) уже жива на карте
        world.CityHomelands.Restore(new CityHomelandRecord
        {
            CityId = 102,
            OriginalKingdomId = 4,
            OriginalKingdomName = "Белолугия",
            OriginalIdeology = IdeologyType.Monarchy
        });

        occupier.AvgDissent = 0.60f;
        occupier.Legitimacy = 25f;
        NationalLiberationEngine.TickKingdom(occupier, world, 90);

        Assert.Empty(world.PendingRestorations);
    }

    [Fact]
    public void LiberationCompleteRestorationRebindsHomelandWithoutPostwarPeaceEventOrReparations()
    {
        var world = new PoliticalWorld();
        world.SetYear(90);
        var occupier = world.AddKingdom(1, IdeologyType.Monarchy, 10);
        world.AddCity(100, 1);
        world.AddCity(101, 1);
        world.CityHomelands.Restore(new CityHomelandRecord
        {
            CityId = 101,
            OriginalKingdomId = 5,
            OriginalKingdomName = "Добромирия",
            OriginalIdeology = IdeologyType.Monarchy,
            WasHistoricalCapital = true,
            AnnexedYear = 30
        });

        occupier.AvgDissent = 0.50f;
        NationalLiberationEngine.TickKingdom(occupier, world, 90);
        var order = Assert.Single(world.PendingRestorations);
        Assert.True(order.IsNationalLiberation);

        world.CompleteRestoration(order, 35, "Добромирия");

        // Освобождение не должно публиковать ложное послевоенное событие «Как после великой войны»
        Assert.Empty(world.Bus.OfType(PolEventType.KingdomRestored));
        Assert.Empty(world.Reparations);
        // Историческая родина города 101 перепривязана к новому id #35
        Assert.Equal(35, world.CityHomelands.Get(101)!.OriginalKingdomId);
    }

    [Fact]
    public void ArmedLiberationAfterVassalRecognitionRemovesAbsorbedVassalSnapshot()
    {
        var world = new PoliticalWorld();
        world.SetYear(90);
        var republic = world.AddKingdom(1, IdeologyType.Democracy, 10);
        world.AddCity(100, 1);
        world.AddCity(101, 1);
        world.CityHomelands.Restore(new CityHomelandRecord
        {
            CityId = 101,
            OriginalKingdomId = 5,
            OriginalKingdomName = "Шотландия",
            OriginalIdeology = IdeologyType.Democracy,
            WasHistoricalCapital = true,
            AnnexedYear = 30
        });

        republic.AvgDissent = 0.35f;
        NationalLiberationEngine.TickKingdom(republic, world, 90);
        Assert.NotNull(world.UnionOf(5));

        // Через 13 лет (после кулдауна) земли поднимают вооружённое восстание.
        world.SetYear(103);
        republic.AvgDissent = 0.60f;
        NationalLiberationEngine.TickKingdom(republic, world, 103);
        RestorationOrder order = Assert.Single(world.PendingRestorations);
        Assert.False(order.LiberationPeaceful);

        world.CompleteRestoration(order, 35, "Шотландия");

        Assert.Null(world.UnionOf(5));
        Assert.Null(world.UnionOf(1));
        Assert.NotNull(world.GetKingdom(35));
    }
}
