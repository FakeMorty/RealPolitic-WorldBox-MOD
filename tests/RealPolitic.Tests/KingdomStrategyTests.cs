using System;
using RealPolitic.Core;
using Xunit;

public sealed class KingdomStrategyTests
{
    private static KingdomStrategySnapshot Base(IdeologyType ideology = IdeologyType.Monarchy) => new KingdomStrategySnapshot
    {
        KingdomId = 1, Year = 100, Ideology = ideology, Population = 100, Warriors = 30, MaxWarriors = 50,
        Cities = 3, Food = 500, Gold = 150, Stone = 100, AvgLoyalty = 70, Legitimacy = 70,
        ThreatLevel = 10, Support = 70, AvgDissent = 5, LastActionYear = 90
    };

    [Fact]
    public void StarvationHasPriorityOverNormalGrowth()
    {
        var s = new KingdomStrategySnapshot { Population = 100, Food = 20, Legitimacy = 70, Year = 100, LastActionYear = 90 };
        Assert.Equal(KingdomActionType.EmergencyFood, KingdomStrategicPlanner.Choose(s).Action);
    }

    [Fact]
    public void WarAndWeakArmyChooseDefense()
    {
        var s = new KingdomStrategySnapshot { AtWar = true, Warriors = 10, MaxWarriors = 100, Food = 500, Legitimacy = 70, Year = 100, LastActionYear = 90 };
        Assert.Equal(KingdomActionType.Defend, KingdomStrategicPlanner.Choose(s).Action);
    }

    [Fact]
    public void LowLegitimacyIsExplained()
    {
        var s = new KingdomStrategySnapshot { Legitimacy = 10, Population = 100, Food = 500, Year = 100, LastActionYear = 90 };
        var plan = KingdomStrategicPlanner.Choose(s);
        Assert.Equal(KingdomActionType.RestoreLegitimacy, plan.Action);
        Assert.Contains("легитимность", plan.Reason);
    }

    [Fact]
    public void ElevatedThreatWithModerateLegitimacyTriggersRestoreLegitimacyBeforeBreak()
    {
        var s = new KingdomStrategySnapshot
        {
            Ideology = IdeologyType.Monarchy, Legitimacy = 42, ThreatLevel = 75,
            Population = 100, Food = 500, Gold = 150, Stone = 100, Year = 100, LastActionYear = 90
        };
        var plan = KingdomStrategicPlanner.Choose(s);
        Assert.Equal(KingdomActionType.RestoreLegitimacy, plan.Action);
    }

    [Fact]
    public void CooldownReturnsSafeNoop()
    {
        var s = new KingdomStrategySnapshot { Year = 100, LastActionYear = 99, ActionCooldownYears = 3 };
        var plan = KingdomStrategicPlanner.Choose(s);
        Assert.Equal(KingdomActionType.None, plan.Action);
        Assert.True(plan.IsFallback);
    }

    [Fact]
    public void FirstStrategicTickIsNotBlockedBySentinelYear()
    {
        var s = new KingdomStrategySnapshot
        {
            Year = 100, Population = 100, Food = 500, Gold = 150, Stone = 100,
            Legitimacy = 70, LastActionYear = int.MinValue
        };

        var plan = KingdomStrategicPlanner.Choose(s);

        Assert.NotEqual(KingdomActionType.None, plan.Action);
    }

    [Fact]
    public void MilitarismKeepsLargerPeaceArmyBeforeDemobilizing()
    {
        var ordinary = new KingdomStrategySnapshot { Ideology = IdeologyType.Monarchy, Warriors = 50, MaxWarriors = 50, Food = 500, Legitimacy = 70, Year = 100, LastActionYear = 90 };
        var militarist = new KingdomStrategySnapshot { Ideology = IdeologyType.Militarism, Warriors = 50, MaxWarriors = 50, Food = 500, Legitimacy = 70, Year = 100, LastActionYear = 90 };
        Assert.Equal(KingdomActionType.Demobilize, KingdomStrategicPlanner.Choose(ordinary).Action);
        Assert.NotEqual(KingdomActionType.Demobilize, KingdomStrategicPlanner.Choose(militarist).Action);
    }

    [Fact]
    public void MerchantRepublicPrefersAllianceToGrowth()
    {
        var s = new KingdomStrategySnapshot { Ideology = IdeologyType.MerchantRepublic, HasAllianceOffer = true, Food = 500, Gold = 150, Stone = 100, Legitimacy = 70, Year = 100, LastActionYear = 90 };
        Assert.Equal(KingdomActionType.SeekAlliance, KingdomStrategicPlanner.Choose(s).Action);
    }

    [Fact]
    public void EmptyTreasuryIsRecognizedAsShortage()
    {
        var s = new KingdomStrategySnapshot
        {
            Ideology = IdeologyType.MerchantRepublic, Population = 100, Food = 500,
            Gold = 0, Stone = 100, Legitimacy = 70, Year = 100, LastActionYear = 90
        };
        Assert.Equal(KingdomActionType.AccumulateGold, KingdomStrategicPlanner.Choose(s).Action);
    }

    [Fact]
    public void SameInputIsDeterministic()
    {
        var first = KingdomStrategicPlanner.Choose(Base(IdeologyType.Technocracy));
        var second = KingdomStrategicPlanner.Choose(Base(IdeologyType.Technocracy));
        Assert.Equal(first.Action, second.Action); Assert.Equal(first.Score, second.Score);
    }

    [Fact]
    public void ExecutorFailureIsContained()
    {
        var ok = KingdomActionExecutor.TryExecute(new ThrowingExecutor(), new KingdomActionPlan(KingdomActionType.Defend, 1, "test"), Base(), out var result);
        Assert.False(ok); Assert.Contains("Ошибка", result);
    }

    [Fact]
    public void NullExecutorIsReportedWithoutThrowing()
    {
        var ok = KingdomActionExecutor.TryExecute(null, new KingdomActionPlan(KingdomActionType.Defend, 1, "test"), Base(), out var result);
        Assert.False(ok); Assert.Contains("недоступен", result);
    }

    private sealed class ThrowingExecutor : IKingdomActionExecutor
    {
        public bool TryExecute(KingdomActionPlan plan, KingdomStrategySnapshot snapshot, out string result) => throw new InvalidOperationException();
    }
}
