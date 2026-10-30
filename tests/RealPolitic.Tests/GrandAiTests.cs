using RealPolitic.Core;
using Xunit;

namespace RealPolitic.Tests;

public sealed class GrandAiTests
{
    private static KingdomStrategySnapshot Base(IdeologyType ideology = IdeologyType.Monarchy) => new KingdomStrategySnapshot
    {
        KingdomId = 1,
        Year = 100,
        Ideology = ideology,
        Population = 120,
        Warriors = 30,
        MaxWarriors = 60,
        Cities = 3,
        Food = 400,
        Gold = 150,
        Stone = 100,
        AvgLoyalty = 70,
        Legitimacy = 75,
        ThreatLevel = 10,
        Support = 75,
        AvgDissent = 0.05f,
        LastActionYear = 90
    };

    [Fact]
    public void WithoutConstitutionalBlocksGrandAiMatchesCoreStrategicPlanner()
    {
        foreach (IdeologyType ideology in System.Enum.GetValues(typeof(IdeologyType)))
        {
            var s = Base(ideology);
            var expected = KingdomStrategicPlanner.Choose(s);
            var actual = GrandAi.Choose(s);
            Assert.Equal(expected.Action, actual.Action);
            Assert.Equal(expected.Score, actual.Score, 3);
        }
    }

    [Fact]
    public void BindingRestitutionConstitutionBlocksConquestWarAndSelectsNextValidCandidate()
    {
        var s = new KingdomStrategySnapshot
        {
            KingdomId = 1,
            Year = 100,
            Ideology = IdeologyType.Militarism,
            Population = 120,
            Warriors = 35,
            MaxWarriors = 60,
            Food = 400,
            Gold = 150,
            Stone = 100,
            Legitimacy = 75,
            CasusBelliEnabled = true,
            WarTargetId = 2,
            WarCasusBelli = CasusBelliType.Conquest,
            WarScore = 80f,
            LastActionYear = 90
        };

        var constitution = ConstitutionDefaults.For(IdeologyType.Militarism, IdeologyEpoch.Modern);
        constitution.Supremacy = SupremacyRule.Binding;
        constitution.Land = LandLaw.Restitution;

        var report = GrandAi.Evaluate(s, constitution);
        Assert.NotEqual(KingdomActionType.DeclareWar, report.SelectedPlan.Action);
        var blockedWar = Assert.Single(report.BlockedCandidates, c => c.Action == KingdomActionType.DeclareWar);
        Assert.Contains("Статья XI", blockedWar.GateReason);
    }

    [Fact]
    public void BindingRestitutionAllowsReconquestWar()
    {
        var reconquestSnapshot = new KingdomStrategySnapshot
        {
            KingdomId = 1,
            Year = 100,
            Ideology = IdeologyType.Monarchy,
            Population = 120,
            Warriors = 35,
            MaxWarriors = 60,
            Food = 400,
            Gold = 150,
            Stone = 100,
            Legitimacy = 75,
            CasusBelliEnabled = true,
            WarTargetId = 2,
            WarCasusBelli = CasusBelliType.Reconquest,
            WarScore = 80f,
            LastActionYear = 90
        };

        var constitution = ConstitutionDefaults.For(IdeologyType.Monarchy, IdeologyEpoch.Modern);
        constitution.Supremacy = SupremacyRule.Binding;
        constitution.Land = LandLaw.Restitution;

        var allowedReport = GrandAi.Evaluate(reconquestSnapshot, constitution);
        Assert.Equal(KingdomActionType.DeclareWar, allowedReport.SelectedPlan.Action);
    }

    [Fact]
    public void UnrecognizedStateBlocksSeekAllianceInGrandAi()
    {
        var s = new KingdomStrategySnapshot
        {
            KingdomId = 4,
            Year = 100,
            Ideology = IdeologyType.MerchantRepublic,
            Population = 120,
            Warriors = 25,
            MaxWarriors = 50,
            Food = 400,
            Gold = 150,
            Stone = 100,
            Legitimacy = 75,
            HasAllianceOffer = true,
            LastActionYear = 90
        };

        var recognition = new InternationalRecognition(enabledForNewWorld: true);
        recognition.RegisterCountry(1, 1);
        recognition.RegisterCountry(2, 1);
        recognition.RegisterCountry(3, 1);
        recognition.RegisterCountry(4, 95); // Born after year 50 when >= 3 recognized states exist -> unrecognized

        var byRecognition = GrandAi.Evaluate(s, null, recognition);
        Assert.NotEqual(KingdomActionType.SeekAlliance, byRecognition.SelectedPlan.Action);
        Assert.Contains(byRecognition.BlockedCandidates, c => c.Action == KingdomActionType.SeekAlliance && c.GateReason.Contains("Непризнанное"));
    }

    [Fact]
    public void EvaluateVectorsIdentifiesDominantStrategicPressure()
    {
        var starving = new KingdomStrategySnapshot
        {
            KingdomId = 1,
            Year = 100,
            Population = 100,
            Food = 10,
            Starving = true,
            Legitimacy = 75,
            LastActionYear = 90
        };
        var report = GrandAi.Evaluate(starving);
        Assert.Equal("Survival", report.Vectors.DominantVector);
        Assert.Equal(100f, report.Vectors.Survival);
        Assert.Equal(KingdomActionType.EmergencyFood, report.SelectedPlan.Action);
    }

    [Fact]
    public void MultiStepReclaimLostHomelandCampaignAdvancesThroughStepsAndCompletes()
    {
        var world = new PoliticalWorld();
        var victim = world.AddKingdom(1, IdeologyType.Monarchy, 10);
        var aggressor = world.AddKingdom(2, IdeologyType.Militarism, 10);
        world.SetYear(50);

        // В 50 году Пруссия отнимает город у Польши по мирному договору
        world.DiplomaticMemories.Record(1, 2, DiplomaticMemoryKind.AnnexedOurCity, 50, 80, -45, "Гданьск");
        Assert.Equal(-45, world.DiplomaticMemories.GetTrustScore(1, 2, 55));
        Assert.Equal(CasusBelliType.Reconquest, world.BestCasusBelli(1, 2, claims: 0));

        var snapshot = Base(IdeologyType.Monarchy);
        var campaign = GrandAi.ManageCampaign(victim, snapshot, world, 55);
        Assert.NotNull(campaign);
        Assert.Equal(StrategicGoalType.ReclaimLostHomeland, campaign!.Goal);
        Assert.Equal(2, campaign.TargetKingdomId);
        Assert.Equal(new[] { KingdomActionType.AccumulateGold, KingdomActionType.Mobilize, KingdomActionType.DeclareWar }, campaign.Steps);

        // Шаг 1: накопить золото
        var eval1 = GrandAi.Evaluate(snapshot, victim.Constitution, null, campaign);
        Assert.Equal(KingdomActionType.AccumulateGold, eval1.SelectedPlan.Action);
        GrandAi.NotifyStepExecuted(victim, KingdomActionType.AccumulateGold, world, 55);
        Assert.Equal(1, campaign.CurrentStepIndex);

        // Шаг 2: мобилизовать армию
        var eval2 = GrandAi.Evaluate(snapshot, victim.Constitution, null, campaign);
        Assert.Equal(KingdomActionType.Mobilize, eval2.SelectedPlan.Action);
        GrandAi.NotifyStepExecuted(victim, KingdomActionType.Mobilize, world, 58);
        Assert.Equal(2, campaign.CurrentStepIndex);

        // Шаг 3: объявить войну возвращения
        GrandAi.NotifyStepExecuted(victim, KingdomActionType.DeclareWar, world, 61);
        Assert.True(campaign.IsCompleted);
        Assert.Null(victim.ActiveCampaign);
    }

    [Fact]
    public void SaveDataRoundtripPreservesActiveCampaignAndDiplomaticMemories()
    {
        var world = new PoliticalWorld();
        var k1 = world.AddKingdom(1, IdeologyType.Democracy, 10);
        var k2 = world.AddKingdom(2, IdeologyType.Monarchy, 10);
        world.SetYear(70);
        world.DiplomaticMemories.Record(1, 2, DiplomaticMemoryKind.AlliedInWar, 68, 50, 25, "Коалиция");
        k1.ActiveCampaign = new StrategicCampaignPlan
        {
            Goal = StrategicGoalType.PeacefulUnification,
            TargetKingdomId = 2,
            TargetKingdomName = "Австрия",
            StartedYear = 69,
            DeadlineYear = 79,
            CurrentStepIndex = 1,
            CompletedSteps = 1,
            Steps = { KingdomActionType.SignTreaty, KingdomActionType.SeekAlliance, KingdomActionType.DevelopTrade },
            ReasonKey = "RP27_GOAL_UNIFY"
        };

        string json = SaveData.Write(world);
        var restored = new PoliticalWorld();
        SaveData.Apply(restored, json);

        Assert.Equal(25, restored.DiplomaticMemories.GetTrustScore(1, 2, 70));
        var restoredK1 = restored.GetKingdom(1);
        Assert.NotNull(restoredK1?.ActiveCampaign);
        Assert.Equal(StrategicGoalType.PeacefulUnification, restoredK1!.ActiveCampaign!.Goal);
        Assert.Equal(1, restoredK1.ActiveCampaign.CurrentStepIndex);
        Assert.Equal(KingdomActionType.SeekAlliance, restoredK1.ActiveCampaign.CurrentStepAction);
    }
}
