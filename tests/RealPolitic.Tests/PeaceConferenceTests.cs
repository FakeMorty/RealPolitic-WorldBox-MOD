using System.Linq;
using RealPolitic.Core;
using Xunit;

namespace RealPolitic.Tests;

public sealed class PeaceConferenceTests
{
    [Fact]
    public void CoalitionVictoryPartitionsReparationsAndCitiesAmongAllies()
    {
        var world = new PoliticalWorld();
        var k1 = world.AddKingdom(1, IdeologyType.Militarism, 10);
        var k2 = world.AddKingdom(2, IdeologyType.Monarchy, 10);
        var defeated = world.AddKingdom(3, IdeologyType.Monarchy, 10);
        k1.Census[IdeologyType.Militarism] = 100;
        k2.Census[IdeologyType.Monarchy] = 90;
        defeated.Economy = new EconomyState { LastIncome = 100f };
        world.SetYear(80);

        // Союзник #2 имеет историческую претензию к побеждённому #3 — это повышает его вклад на конференции
        world.DiplomaticMemories.Record(2, 3, DiplomaticMemoryKind.AnnexedOurCity, 60, 80, -45, "Силезия");

        var coalition = new WarCoalitionOutcome
        {
            TotalAttackers = 2,
            TotalDefenders = 1,
            AttackerSideIds = { 1, 2 },
            DefenderSideIds = { 3 }
        };

        // 4 года войны -> 2 города требуют победители + репарации
        var settlement = world.ResolveWarEnd(attackerId: 1, defenderId: 3, winnerId: 1, warYears: 4, defeatedCitiesLeft: 4, coalition: coalition);
        Assert.Equal(SettlementLevel.LandDemand, settlement.Level);
        Assert.Equal(2, settlement.CitiesDemanded);

        // Оба союзника получают свою долю репараций и по 1 городу, а также перемирие с побеждённым
        Assert.Equal(2, world.Reparations.Count);
        Assert.Contains(world.Reparations, r => r.PayeeId == 1 && r.AmountPerYear > 0f);
        Assert.Contains(world.Reparations, r => r.PayeeId == 2 && r.AmountPerYear > 0f);
        Assert.True(world.TrucesBook.Active(1, 3, 81));
        Assert.True(world.TrucesBook.Active(2, 3, 81));

        var demands = world.PendingCityDemands;
        Assert.Equal(2, demands.Sum(d => d.Count));
        Assert.Contains(demands, d => d.ToId == 1 && d.Count == 1);
        Assert.Contains(demands, d => d.ToId == 2 && d.Count == 1);
    }

    [Fact]
    public void ShortchangedMajorAllyRecordsSpoilsDisputeAndGrievance()
    {
        var world = new PoliticalWorld();
        var k1 = world.AddKingdom(1, IdeologyType.Militarism, 10);
        var k2 = world.AddKingdom(2, IdeologyType.Militarism, 10);
        var defeated = world.AddKingdom(3, IdeologyType.Monarchy, 10);
        k1.Census[IdeologyType.Militarism] = 100;
        k2.Census[IdeologyType.Militarism] = 80;
        world.SetYear(90);

        var coalition = new WarCoalitionOutcome
        {
            TotalAttackers = 2,
            TotalDefenders = 2,
            DestroyedDefenders = 1,
            AttackerSideIds = { 1, 2 },
            DefenderSideIds = { 3, 4 }
        };

        // 2 года войны с разгромом одного защитника -> всего 1 уступленный город, который достаётся главному победителю #1, а крупный союзник #2 остаётся без земли
        var settlement = world.ResolveWarEnd(attackerId: 1, defenderId: 3, winnerId: 1, warYears: 2, defeatedCitiesLeft: 3, coalition: coalition);
        Assert.Equal(1, settlement.CitiesDemanded);

        // Союзник #2 затаил обиду (SpoilsShortchanged = -30, +25 боевое братство => итого -5 и претензия BrokenUnion)
        Assert.True(world.GetGrievance(2, 1) >= 25f);
        Assert.Contains(world.DiplomaticMemories.All, m => m.FromKingdomId == 2 && m.TowardsKingdomId == 1 && m.Kind == DiplomaticMemoryKind.SpoilsShortchanged);
    }
}
