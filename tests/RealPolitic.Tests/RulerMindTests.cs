using System.Collections.Generic;
using RealPolitic.Core;
using Xunit;

namespace RealPolitic.Tests;

/// <summary>
/// 0.39: «правители и жители умнее». Проверяем правила ядра без запуска игры:
///  • оценка сил перед войной (свои, защитника и его союзников);
///  • осторожность после поражения на 10 лет;
///  • память жителей о нарушенных обещаниях.
/// </summary>
public class RulerMindTests
{
    // ---------------- А. Оценка сил перед войной ----------------

    [Fact]
    public void StrongAttacker_Attacks()
    {
        WarEstimate e = WarChances.Estimate(120f, 60f, 0f, false);
        Assert.True(e.ShouldAttack);
        Assert.True(e.Ratio > WarChances.ConquestRatio);
        Assert.Equal("перевес есть", e.Reason);
    }

    [Fact]
    public void WeakAttacker_DoesNotAttack()
    {
        WarEstimate e = WarChances.Estimate(40f, 90f, 0f, false);
        Assert.False(e.ShouldAttack);
        Assert.Equal("сил слишком мало", e.Reason);
    }

    [Fact]
    public void EnemiesAllies_AreCounted()
    {
        // Своих 100, у защитника 60, но за ним союзник на 100:
        // 60 + 100*0.6 = 120 против наших 100 — перевеса нет.
        WarEstimate e = WarChances.Estimate(100f, 60f, 100f, false);
        Assert.False(e.ShouldAttack);
        Assert.Equal(120, e.DefenderStrength);
    }

    [Fact]
    public void SmallEdge_AllowsOnlyJustifiedWar()
    {
        WarEstimate conquest = WarChances.Estimate(70, 1, 50f, 0f, 70, 1, 50f, 0f, 0f, CasusBelliType.Conquest);
        Assert.False(conquest.ShouldAttack);
        WarEstimate revenge = WarChances.Estimate(70, 1, 50f, 0f, 70, 1, 50f, 0f, 0f, CasusBelliType.Revenge);
        Assert.True(revenge.ShouldAttack);
    }

    [Fact]
    public void DefensiveWar_IsNeverCancelled()
    {
        WarEstimate e = WarChances.Estimate(20f, 300f, 0f, true);
        Assert.True(e.ShouldAttack);
        Assert.Equal("война оборонительная", e.Reason);
    }

    [Fact]
    public void WarFatigue_WeakensStrength()
    {
        float fresh = WarChances.Strength(100, 3, 50f, 0f);
        float tired = WarChances.Strength(100, 3, 50f, 90f);
        Assert.True(tired < fresh);
    }

    [Fact]
    public void PoorEconomy_WeakensStrength()
    {
        float rich = WarChances.Strength(100, 3, 80f, 0f);
        float poor = WarChances.Strength(100, 3, 20f, 0f);
        Assert.True(poor < rich);
    }

    // ---------------- В. Правитель учится на поражениях ----------------

    [Fact]
    public void Defeat_MakesRealmCautiousForTenYears()
    {
        KingdomPolitics state = new KingdomPolitics { KingdomId = 1 };
        KingdomLearning.RecordDefeat(state, 200);
        Assert.True(KingdomLearning.IsWarAverse(state, 200));
        Assert.True(KingdomLearning.IsWarAverse(state, 209));
        Assert.False(KingdomLearning.IsWarAverse(state, 210));
        Assert.Equal(1, state.Defeats);
        Assert.Equal(200, state.LastDefeatYear);
    }

    [Fact]
    public void Victory_ClearsCaution()
    {
        KingdomPolitics state = new KingdomPolitics { KingdomId = 1 };
        KingdomLearning.RecordDefeat(state, 200);
        KingdomLearning.RecordVictory(state, 203);
        Assert.False(KingdomLearning.IsWarAverse(state, 204));
        Assert.Equal(0, state.WarAversionUntilYear);
    }

    [Fact]
    public void Planner_RefusesWarAfterDefeat()
    {
        KingdomStrategySnapshot snapshot = new KingdomStrategySnapshot
        {
            KingdomId = 1,
            Year = 205,
            Ideology = IdeologyType.Monarchy,
            CasusBelliEnabled = true,
            WarTargetId = 2,
            WarScore = 90f,
            WarAversionUntilYear = 210,
            Legitimacy = 70f,
            Population = 400,
            Warriors = 100,
            MaxWarriors = 200
        };
        KingdomActionPlan plan = KingdomStrategicPlanner.Choose(snapshot);
        Assert.NotEqual(KingdomActionType.DeclareWar, plan.Action);
    }

    [Fact]
    public void Planner_AllowsWar_WhenNotAverse()
    {
        KingdomStrategySnapshot snapshot = new KingdomStrategySnapshot
        {
            KingdomId = 1,
            Year = 205,
            Ideology = IdeologyType.Monarchy,
            CasusBelliEnabled = true,
            WarTargetId = 2,
            WarScore = 90f,
            WarAversionUntilYear = 0,
            Legitimacy = 70f,
            Population = 400,
            Warriors = 100,
            MaxWarriors = 200
        };
        KingdomActionPlan plan = KingdomStrategicPlanner.Choose(snapshot);
        Assert.Equal(KingdomActionType.DeclareWar, plan.Action);
    }

    // ---------------- Б. Жители помнят обещания ----------------

    [Fact]
    public void PromiseScore_GrowsAndFades()
    {
        KingdomPolitics state = new KingdomPolitics { KingdomId = 1 };
        PromiseMemory.Record(state, PromiseMemory.BrokenTruce, 200);
        float after = state.PromiseScore;
        Assert.Equal(PromiseMemory.BrokenTruce, after);
        for (int i = 0; i < 10; i++)
        {
            PromiseMemory.YearlyDecay(state);
        }
        Assert.True(state.PromiseScore < after);
    }

    [Fact]
    public void PromiseScore_IsCapped()
    {
        KingdomPolitics state = new KingdomPolitics { KingdomId = 1 };
        for (int i = 0; i < 20; i++)
        {
            PromiseMemory.Record(state, PromiseMemory.BrokenTruce, 200);
        }
        Assert.Equal(PromiseMemory.MaxScore, state.PromiseScore);
    }

    [Fact]
    public void BrokenPromises_HitLegitimacySupportAndElections()
    {
        KingdomPolitics clean = new KingdomPolitics { KingdomId = 1 };
        KingdomPolitics guilty = new KingdomPolitics { KingdomId = 2 };
        PromiseMemory.Record(guilty, PromiseMemory.UnconstitutionalWar, 200);
        PromiseMemory.Record(guilty, PromiseMemory.BrokenTruce, 201);
        PromiseMemory.Record(guilty, PromiseMemory.IgnoredUltimatum, 202);
        Assert.True(PromiseMemory.YearlyLegitimacyHit(guilty) > PromiseMemory.YearlyLegitimacyHit(clean));
        Assert.True(PromiseMemory.YearlySupportHit(guilty) > PromiseMemory.YearlySupportHit(clean));
        Assert.True(PromiseMemory.ElectionPenalty(guilty) > 0f);
        Assert.True(PromiseMemory.MovementGrowthBonus(guilty) > 0f);
        Assert.Equal("правителю больше не верят", PromiseMemory.Describe(guilty));
    }

    [Fact]
    public void CleanRealm_KeepsItsWord()
    {
        KingdomPolitics state = new KingdomPolitics { KingdomId = 1 };
        Assert.Equal("обещания держит", PromiseMemory.Describe(state));
        Assert.Equal(0f, PromiseMemory.YearlyLegitimacyHit(state));
        Assert.Equal(0f, PromiseMemory.ElectionPenalty(state));
    }

    [Fact]
    public void NewFields_SurviveSaving()
    {
        PoliticalWorld world = new PoliticalWorld();
        KingdomPolitics state = world.AddKingdom(7, IdeologyType.Monarchy, 10, ReformStance.Moderate);
        KingdomLearning.RecordDefeat(state, 200);
        PromiseMemory.Record(state, PromiseMemory.BrokenTruce, 201);
        PoliticalWorld copy = new PoliticalWorld();
        SaveData.Apply(copy, SaveData.Write(world));
        KingdomPolitics restored = copy.GetKingdom(7);
        Assert.NotNull(restored);
        Assert.Equal(1, restored!.Defeats);
        Assert.Equal(200, restored.LastDefeatYear);
        Assert.Equal(210, restored.WarAversionUntilYear);
        Assert.Equal(PromiseMemory.BrokenTruce, restored.PromiseScore);
    }
}
