using System.Collections.Generic;
using System.Linq;
using RealPolitic.Core;
using Xunit;

namespace RealPolitic.Tests;

/// <summary>0.31.2: мирный договор не восстанавливает уничтоженную державу.
/// Уцелевшие города остаются у победителя как поглощённые вассальные земли;
/// независимость позднее возможна только восстанием и войной.</summary>
public sealed class PeaceTreatyVassalageTests
{
    [Theory]
    [InlineData(IdeologyType.Democracy)]
    [InlineData(IdeologyType.Monarchy)]
    [InlineData(IdeologyType.Militarism)]
    public void TotalDefeatPeaceNeverRestoresSeparateStateForAnyVictor(IdeologyType victorIdeology)
    {
        PoliticalWorld world = TotalDefeatWorld(victorIdeology, prewarCities: 3);

        PeaceSettlement settlement = world.ResolveWarEnd(
            attackerId: 1,
            defenderId: 2,
            winnerId: 1,
            warYears: 6,
            defeatedCitiesLeft: 0)!;

        Assert.NotNull(settlement);
        Assert.Equal(SettlementLevel.TotalDefeat, settlement.Level);
        Assert.Equal(3, settlement.CitiesDemanded);
        Assert.Equal(0f, settlement.ReparationsPerYear);
        Assert.Equal(0, settlement.ReparationsYears);
        Assert.Empty(world.PendingRestorations);
        Assert.Empty(world.Reparations);
        Assert.Empty(world.Bus.OfType(PolEventType.KingdomRestored));
        Assert.Contains(world.Bus.OfType(PolEventType.PeaceSettlement),
            e => e.Get("terms") == "RP_WARTERMS_TOTAL_VASSAL" && e.Get("cities") == "3");

        // Серебряный Яр не появился отдельной страной: все города остались у Ольховии,
        // а его столицы, границы, имя и строй запомнены как вассальные земли.
        Assert.All(new[] { 201, 202, 203 }, cityId => Assert.Equal(1, world.GetCity(cityId)!.KingdomId));
        Union union = Assert.IsType<Union>(world.UnionOf(2));
        Assert.Equal(UnionType.Vassal, union.Type);
        Assert.Equal(1, union.LeaderId);
        UnionMember member = Assert.IsType<UnionMember>(union.GetMember(2));
        Assert.Equal(201, member.CapitalCityId);
        Assert.Equal(new[] { 201, 202, 203 }, member.CityIds);
        Assert.Equal("Серебряный Яр", member.OriginalName);
        Assert.Equal(IdeologyType.Monarchy, member.OriginalIdeology);
        Assert.True(world.IsAbsorbedHomelandCity(1, 202));
    }

    [Fact]
    public void AbsorbedTreatyLandsCanLaterDeclareIndependenceWar()
    {
        PoliticalWorld world = TotalDefeatWorld(IdeologyType.Monarchy, prewarCities: 3);
        world.ResolveWarEnd(1, 2, winnerId: 1, warYears: 6, defeatedCitiesLeft: 0);

        // Игровой слой снимает исчезнувшее нативное государство; вассальный снимок остаётся.
        world.RemoveKingdom(2);
        Assert.Null(world.GetKingdom(2));
        Assert.NotNull(world.UnionOf(2));

        Assert.True(world.LeaveUnionForIndependence(2, out string result), result);
        RestorationOrder order = Assert.Single(world.PendingRestorations);
        Assert.True(order.IsNationalLiberation);
        Assert.False(order.LiberationPeaceful);
        Assert.Equal("Серебряный Яр", order.DefeatedName);
        Assert.Contains(201, order.PrewarCityIds);
        Assert.Contains(202, order.PrewarCityIds);
        Assert.Contains(203, order.PrewarCityIds);
    }

    [Fact]
    public void TotalDefeatWithTooFewPrewarCitiesAnnexesWithoutVassalSnapshot()
    {
        PoliticalWorld world = TotalDefeatWorld(IdeologyType.Monarchy, prewarCities: 2);

        PeaceSettlement settlement = world.ResolveWarEnd(
            attackerId: 1,
            defenderId: 2,
            winnerId: 1,
            warYears: 3,
            defeatedCitiesLeft: 0)!;

        Assert.Equal(SettlementLevel.TotalDefeat, settlement.Level);
        Assert.Equal(2, settlement.CitiesDemanded);
        Assert.Empty(world.PendingRestorations);
        Assert.Null(world.UnionOf(2));
        Assert.Contains(world.Bus.OfType(PolEventType.PeaceSettlement),
            e => e.Get("terms") == "RP_WARTERMS_ANNEXED");
    }

    [Fact]
    public void LegacyPeaceRestorationOrderConvertsToAbsorbedVassalAndCannotCompleteAsState()
    {
        var world = new PoliticalWorld();
        world.SetYear(120);
        world.AddKingdom(1, IdeologyType.Monarchy, 20);
        world.RememberKingdomName(1, "Ольховия");
        world.AddCity(100, 1);
        world.AddCity(501, 1);
        world.AddCity(502, 1);
        world.CityHomelands.Restore(new CityHomelandRecord
        {
            CityId = 501,
            OriginalKingdomId = 5,
            OriginalKingdomName = "Добрыня",
            OriginalIdeology = IdeologyType.MerchantRepublic,
            WasHistoricalCapital = true,
            AnnexedYear = 100
        });
        world.CityHomelands.Restore(new CityHomelandRecord
        {
            CityId = 502,
            OriginalKingdomId = 5,
            OriginalKingdomName = "Добрыня",
            OriginalIdeology = IdeologyType.MerchantRepublic,
            AnnexedYear = 100
        });

        var legacyOrder = new RestorationOrder
        {
            DefeatedId = 5,
            VictorId = 1,
            Mode = RestorationMode.Puppet,
            CitiesKeptByVictor = 1,
            DefeatedName = "Добрыня",
            Year = 119,
            PrewarCityIds = new List<int> { 501, 502 },
            IsNationalLiberation = false,
            LiberationPeaceful = false
        };

        Assert.True(world.ConvertRestorationToAbsorbedVassal(legacyOrder));
        Assert.Empty(world.PendingRestorations);
        Assert.Equal(1, world.GetCity(501)!.KingdomId);
        Assert.Equal(1, world.GetCity(502)!.KingdomId);
        UnionMember member = Assert.IsType<UnionMember>(world.UnionOf(5)?.GetMember(5));
        Assert.Equal(new[] { 501, 502 }, member.CityIds);
        Assert.Equal(IdeologyType.MerchantRepublic, member.OriginalIdeology);

        Assert.Null(world.CompleteRestoration(legacyOrder, 999, "Добрыня"));
        Assert.Null(world.GetKingdom(999));
    }

    [Fact]
    public void SaveDataRoundtripPreservesArmedAndLegacyPeacefulRestorationFlags()
    {
        var world = new PoliticalWorld();
        world.SetYear(140);
        world.AddKingdom(1, IdeologyType.Monarchy, 20);
        world.AddCity(100, 1);
        world.AddPendingRestoration(new RestorationOrder
        {
            DefeatedId = 7,
            VictorId = 1,
            Mode = RestorationMode.Free,
            CitiesKeptByVictor = 1,
            DefeatedName = "Серебряный Яр",
            Year = 140,
            PrewarCityIds = new List<int> { 100, 701 },
            IsNationalLiberation = true,
            LiberationPeaceful = false
        });
        world.AddPendingRestoration(new RestorationOrder
        {
            DefeatedId = 8,
            VictorId = 1,
            Mode = RestorationMode.Free,
            CitiesKeptByVictor = 1,
            DefeatedName = "Добрыня",
            Year = 140,
            PrewarCityIds = new List<int> { 100, 801 },
            IsNationalLiberation = true,
            LiberationPeaceful = true
        });

        string json = SaveData.Write(world);
        var restored = new PoliticalWorld();
        SaveData.Apply(restored, json);

        RestorationOrder armed = restored.PendingRestorations.Single(o => o.DefeatedId == 7);
        Assert.True(armed.IsNationalLiberation);
        Assert.False(armed.LiberationPeaceful);
        RestorationOrder legacyPeaceful = restored.PendingRestorations.Single(o => o.DefeatedId == 8);
        Assert.True(legacyPeaceful.IsNationalLiberation);
        Assert.True(legacyPeaceful.LiberationPeaceful);
    }

    private static PoliticalWorld TotalDefeatWorld(IdeologyType victorIdeology, int prewarCities)
    {
        var world = new PoliticalWorld();
        world.SetYear(180);
        KingdomPolitics victor = world.AddKingdom(1, victorIdeology, 100);
        world.AddKingdom(2, IdeologyType.Monarchy, 100);
        world.RememberKingdomName(1, "Ольховия");
        world.RememberKingdomName(2, "Серебряный Яр");
        victor.Legitimacy = 80f;

        world.AddCity(100, 1);
        var prewar = new List<int>();
        for (int i = 0; i < prewarCities; i++)
        {
            int cityId = 201 + i;
            prewar.Add(cityId);
            world.AddCity(cityId, 2);
            // К моменту мирного договора Ольховия уже захватила города Серебряного Яра.
            world.GetCity(cityId)!.KingdomId = 1;
        }
        world.RememberPrewarCities(2, prewar, prewar[0]);
        return world;
    }
}
