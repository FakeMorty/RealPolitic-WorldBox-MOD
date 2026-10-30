using System.Collections.Generic;
using RealPolitic.Core;
using Xunit;

namespace RealPolitic.Tests;

/// <summary>
/// 0.32, этап 1: созыв КМУД. Проверяем только правила ядра — без запуска игры.
/// </summary>
public class KmudTests
{
    private static SimParams Params(int year = 200, bool enabled = true)
    {
        SimParams p = new SimParams();
        p.EnableKmud = enabled;
        p.KmudYear = year;
        p.KmudYears = 10;
        p.KmudPauseYears = 10;
        return p;
    }

    [Fact]
    public void Conference_DoesNotConvene_BeforeItsYear()
    {
        KmudState state = new KmudState();
        Assert.False(KmudRules.ShouldConvene(state, Params(), 199, 5));
        Assert.Equal(KmudPhase.NotHeld, state.Phase);
    }

    [Fact]
    public void Conference_Convenes_InItsYear()
    {
        KmudState state = new KmudState();
        Assert.True(KmudRules.ShouldConvene(state, Params(), 200, 5));
    }

    [Fact]
    public void Conference_Convenes_EvenIfWorldLoadedPastYear()
    {
        // Мир мог быть загружен на 350-м году — конференция не должна потеряться.
        KmudState state = new KmudState();
        Assert.True(KmudRules.ShouldConvene(state, Params(), 350, 4));
    }

    [Fact]
    public void Conference_RepeatsEveryHundredYears()
    {
        // Решение владельца (29.10.2026): первый съезд на 200-м году, далее каждые 100 лет.
        KmudState state = new KmudState();
        KmudRules.Convene(state, Params(), 200, 5, 2);
        // Съезд закрылся через 10 лет — только после этого считается срок до следующего.
        KmudRules.Close(state, 210, 0);
        Assert.False(KmudRules.ShouldConvene(state, Params(), 210, 5));
        Assert.False(KmudRules.ShouldConvene(state, Params(), 299, 5));
        Assert.True(KmudRules.ShouldConvene(state, Params(), 300, 5));
        state.Phase = KmudPhase.Closed;
        state.OpenedYear = 300;
        Assert.False(KmudRules.ShouldConvene(state, Params(), 399, 5));
        Assert.True(KmudRules.ShouldConvene(state, Params(), 400, 5));
    }

    [Fact]
    public void Conference_IsDisabled_BySetting()
    {
        KmudState state = new KmudState();
        Assert.False(KmudRules.ShouldConvene(state, Params(enabled: false), 300, 6));
    }

    [Fact]
    public void Conference_NeedsAtLeastTwoKingdoms()
    {
        KmudState state = new KmudState();
        Assert.False(KmudRules.ShouldConvene(state, Params(), 200, 1));
        Assert.False(KmudRules.ShouldConvene(state, Params(), 200, 0));
        Assert.True(KmudRules.ShouldConvene(state, Params(), 200, 2));
    }

    [Fact]
    public void Conference_SetsTenYearPause_AndClosesInTenYears()
    {
        KmudState state = new KmudState();
        PolEvent evt = KmudRules.Convene(state, Params(), 200, 6, 3);
        Assert.Equal(KmudPhase.Convened, state.Phase);
        Assert.Equal(200, state.OpenedYear);
        Assert.Equal(210, state.ClosesYear);
        Assert.Equal(210, state.TruceUntilYear);
        Assert.Equal(6, state.Members);
        Assert.Equal(3, state.WarsEnded);
        Assert.Equal(PolEventType.KmudConvened, evt.Type);
        Assert.Equal("6", evt.Get("members"));
        Assert.Equal("3", evt.Get("wars_ended"));
        Assert.Equal("210", evt.Get("until"));
    }

    [Fact]
    public void Pause_IsActiveDuringConference_AndExpiresAfter()
    {
        KmudState state = new KmudState();
        KmudRules.Convene(state, Params(), 200, 6, 0);
        Assert.False(KmudRules.PauseActive(state, 199));
        Assert.True(KmudRules.PauseActive(state, 200));
        Assert.True(KmudRules.PauseActive(state, 209));
        Assert.False(KmudRules.PauseActive(state, 210));
    }

    [Fact]
    public void Pause_NeverShorterThanConference()
    {
        SimParams p = Params();
        p.KmudYears = 10;
        p.KmudPauseYears = 3; // игрок выставил бессмысленно короткую паузу
        KmudState state = new KmudState();
        KmudRules.Convene(state, p, 200, 4, 0);
        Assert.Equal(210, state.TruceUntilYear);
        Assert.True(KmudRules.PauseActive(state, 209));
    }

    [Fact]
    public void Conference_ClosesOnlyWhenTermIsOver()
    {
        KmudState state = new KmudState();
        KmudRules.Convene(state, Params(), 200, 4, 0);
        Assert.False(KmudRules.ShouldClose(state, 209));
        Assert.True(KmudRules.ShouldClose(state, 210));
        state.Phase = KmudPhase.Closed;
        Assert.False(KmudRules.ShouldClose(state, 211));
    }

    [Fact]
    public void AllPairs_GivesEveryPairOnce_WithoutSelfPairs()
    {
        List<int[]> pairs = KmudRules.AllPairs(new List<int> { 1, 2, 3, 4 });
        Assert.Equal(6, pairs.Count);
        foreach (int[] pair in pairs)
        {
            Assert.NotEqual(pair[0], pair[1]);
        }
        Assert.Contains(pairs, p => p[0] == 1 && p[1] == 2);
        Assert.Contains(pairs, p => p[0] == 3 && p[1] == 4);
    }

    [Fact]
    public void AllPairs_WithSingleKingdom_IsEmpty()
    {
        Assert.Empty(KmudRules.AllPairs(new List<int> { 7 }));
        Assert.Empty(KmudRules.AllPairs(null));
    }

    [Fact]
    public void ConferenceState_SurvivesSaveAndLoad()
    {
        PoliticalWorld world = new PoliticalWorld();
        KmudRules.Convene(world.Kmud, Params(), 201, 7, 4);
        world.Kmud.WarsStoppedDuringPause = 2;
        PoliticalWorld copy = new PoliticalWorld();
        SaveData.Apply(copy, SaveData.Write(world));
        Assert.Equal(KmudPhase.Convened, copy.Kmud.Phase);
        Assert.Equal(201, copy.Kmud.OpenedYear);
        Assert.Equal(211, copy.Kmud.ClosesYear);
        Assert.Equal(211, copy.Kmud.TruceUntilYear);
        Assert.Equal(7, copy.Kmud.Members);
        Assert.Equal(4, copy.Kmud.WarsEnded);
        Assert.Equal(2, copy.Kmud.WarsStoppedDuringPause);
    }

    [Fact]
    public void NewWorld_StartsWithoutConference()
    {
        PoliticalWorld world = new PoliticalWorld();
        Assert.Equal(KmudPhase.NotHeld, world.Kmud.Phase);
        Assert.False(world.KmudPauseActive);
    }

    [Fact]
    public void Close_WithoutExtension_LiftsWarBanImmediately()
    {
        KmudState state = new KmudState();
        KmudRules.Convene(state, Params(), 200, 5, 0);
        PolEvent evt = KmudRules.Close(state, 210, 0);
        Assert.Equal(KmudPhase.Closed, state.Phase);
        Assert.Equal(210, state.TruceUntilYear);
        Assert.False(KmudRules.PauseActive(state, 210));
        Assert.Equal(PolEventType.KmudClosed, evt.Type);
        Assert.Equal("0", evt.Get("peace_years"));
    }

    [Fact]
    public void Close_WithExtension_KeepsPeaceForChosenYears()
    {
        KmudState state = new KmudState();
        KmudRules.Convene(state, Params(), 200, 5, 0);
        KmudRules.Close(state, 210, 20);
        Assert.Equal(230, state.TruceUntilYear);
        Assert.True(KmudRules.PauseActive(state, 229));
        Assert.False(KmudRules.PauseActive(state, 230));
    }

    [Fact]
    public void Close_WithShorterExtension_DoesNotShortenExistingBan()
    {
        KmudState state = new KmudState();
        SimParams p = Params();
        p.KmudPauseYears = 40;
        KmudRules.Convene(state, p, 200, 5, 0);
        Assert.Equal(240, state.TruceUntilYear);
        KmudRules.Close(state, 210, 10);
        Assert.Equal(240, state.TruceUntilYear);
    }

    [Fact]
    public void ConferenceState_KeepsAllianceIdAndReturns()
    {
        PoliticalWorld world = new PoliticalWorld();
        KmudRules.Convene(world.Kmud, Params(), 200, 4, 1);
        world.Kmud.AllianceId = 42;
        world.Kmud.AllianceFormed = true;
        world.Kmud.MembersReturned = 3;
        PoliticalWorld copy = new PoliticalWorld();
        SaveData.Apply(copy, SaveData.Write(world));
        Assert.Equal(42, copy.Kmud.AllianceId);
        Assert.True(copy.Kmud.AllianceFormed);
        Assert.Equal(3, copy.Kmud.MembersReturned);
    }

    [Fact]
    public void Conference_IntervalIsConfigurable()
    {
        KmudState state = new KmudState();
        KmudRules.Convene(state, Params(), 100, 5, 0);
        KmudRules.Close(state, 110, 0);
        SimParams p = Params(year: 100);
        p.KmudIntervalYears = 25;
        Assert.False(KmudRules.ShouldConvene(state, p, 120, 5));
        Assert.True(KmudRules.ShouldConvene(state, p, 125, 5));
    }

    [Fact]
    public void NewConference_StartsVotingFromScratch()
    {
        KmudState state = new KmudState();
        KmudRules.Convene(state, Params(), 200, 5, 0);
        state.Voting.Of(KmudQuestion.PeaceYears).Add(KmudOptions.Peace30, 9);
        state.Voting.Voted.Add(1);
        state.Agenda.WorstId = 4;
        KmudRules.Close(state, 210, 0);
        KmudRules.Convene(state, Params(), 250, 6, 1);
        Assert.Equal(2, state.Round);
        Assert.Equal(0, state.Voting.Of(KmudQuestion.PeaceYears).CountOf(KmudOptions.Peace30));
        Assert.Empty(state.Voting.Voted);
        Assert.Equal(0, state.Agenda.WorstId);
    }

    [Fact]
    public void Verdict_WhitePeace_HasNoPayer()
    {
        KmudAgenda agenda = new KmudAgenda { AggressorId = 2, DefenderId = 5, ConflictYears = 40 };
        KmudDecisions decisions = new KmudDecisions();
        KmudVerdicts.ResolvePayer(agenda, KmudOptions.OutcomeWhitePeace, decisions);
        Assert.Equal(0, decisions.PayerId);
        Assert.Equal(0, decisions.PayeeId);
    }

    [Fact]
    public void Verdict_AggressorWin_DefenderPays()
    {
        KmudAgenda agenda = new KmudAgenda { AggressorId = 2, DefenderId = 5, ConflictYears = 40 };
        KmudDecisions decisions = new KmudDecisions();
        KmudVerdicts.ResolvePayer(agenda, KmudOptions.OutcomeAggressor, decisions);
        Assert.Equal(5, decisions.PayerId);
        Assert.Equal(2, decisions.PayeeId);
    }

    [Fact]
    public void Verdict_DefenderWin_AggressorPays()
    {
        KmudAgenda agenda = new KmudAgenda { AggressorId = 2, DefenderId = 5, ConflictYears = 40 };
        KmudDecisions decisions = new KmudDecisions();
        KmudVerdicts.ResolvePayer(agenda, KmudOptions.OutcomeDefender, decisions);
        Assert.Equal(2, decisions.PayerId);
        Assert.Equal(5, decisions.PayeeId);
    }

    [Fact]
    public void Verdict_AlwaysCreatesFiftyYearPact_BetweenFormerEnemies()
    {
        // Чтобы белый мир не превратился в новую войну на следующий год.
        KmudAgenda agenda = new KmudAgenda { AggressorId = 7, DefenderId = 3, ConflictYears = 30 };
        KmudPact pact = KmudVerdicts.CreatePact(agenda, 210);
        Assert.NotNull(pact);
        Assert.Equal(3, pact.A);
        Assert.Equal(7, pact.B);
        Assert.Equal(260, pact.UntilYear);
        Assert.True(pact.Active(259));
        Assert.False(pact.Active(260));
    }

    [Fact]
    public void Verdict_WithoutConflict_HasNoPact()
    {
        Assert.Null(KmudVerdicts.CreatePact(new KmudAgenda(), 210));
        Assert.Null(KmudVerdicts.CreatePact(new KmudAgenda { AggressorId = 4, DefenderId = 4 }, 210));
    }

    [Fact]
    public void PunishmentFlags_FollowTheVotedOption()
    {
        Assert.True(KmudVerdicts.ExecutesRuler(KmudOptions.PunishKillAndReform));
        Assert.True(KmudVerdicts.ChangesIdeology(KmudOptions.PunishKillAndReform));
        Assert.False(KmudVerdicts.ExecutesRuler(KmudOptions.PunishReformOnly));
        Assert.True(KmudVerdicts.ChangesIdeology(KmudOptions.PunishReformOnly));
        Assert.False(KmudVerdicts.ChangesIdeology(KmudOptions.PunishSanctions));
        Assert.True(KmudVerdicts.AppliesSanctions(KmudOptions.PunishSanctions));
        Assert.False(KmudVerdicts.AppliesSanctions(KmudOptions.PunishNothing));
    }

    [Fact]
    public void MajorityIdeology_PicksTheMostCommon()
    {
        IdeologyType result = KmudVerdicts.MajorityIdeology(new List<IdeologyType>
        {
            IdeologyType.Monarchy, IdeologyType.Democracy, IdeologyType.Democracy, IdeologyType.Monarchy,
            IdeologyType.Democracy, IdeologyType.Khaganate
        });
        Assert.Equal(IdeologyType.Democracy, result);
        Assert.Equal(IdeologyType.Democracy, KmudVerdicts.MajorityIdeology(new List<IdeologyType>()));
        Assert.Equal(IdeologyType.Democracy, KmudVerdicts.MajorityIdeology(null));
    }

    [Fact]
    public void PactsSanctionsAssociations_SurviveSaveAndLoad()
    {
        PoliticalWorld world = new PoliticalWorld();
        KmudRules.Convene(world.Kmud, new SimParams(), 200, 5, 1);
        world.Kmud.Pacts.Add(new KmudPact { A = 1, B = 2, UntilYear = 260, AllianceFormed = true });
        world.Kmud.Sanctions.Add(new KmudSanction { KingdomId = 3, UntilYear = 220, Supporters = 4 });
        world.Kmud.Associations.Add(new KmudAssociation { DonorId = 1, RecipientId = 3, UntilYear = 230, GoldPerYear = 40f, FoodPerYear = 20 });
        world.Kmud.Decisions.PunishedId = 3;
        world.Kmud.Decisions.Punishment = KmudOptions.PunishKillAndReform;
        world.Kmud.Decisions.PayerId = 2;
        world.Kmud.Decisions.PayeeId = 1;
        PoliticalWorld copy = new PoliticalWorld();
        SaveData.Apply(copy, SaveData.Write(world));
        Assert.Single(copy.Kmud.Pacts);
        Assert.Equal(260, copy.Kmud.Pacts[0].UntilYear);
        Assert.Single(copy.Kmud.Sanctions);
        Assert.Equal(4, copy.Kmud.Sanctions[0].Supporters);
        Assert.Single(copy.Kmud.Associations);
        Assert.Equal(3, copy.Kmud.Decisions.PunishedId);
        Assert.Equal(2, copy.Kmud.Decisions.PayerId);
    }

    // ----- 0.39: запрет объявлять войны вместо гашения -----

    private static KmudState HeldState(int openedYear = 200, int truceUntil = 220)
    {
        KmudState state = new KmudState();
        KmudRules.Convene(state, Params(), openedYear, 3, 0);
        state.TruceUntilYear = truceUntil;
        state.MemberIds.Add(1);
        state.MemberIds.Add(2);
        state.MemberIds.Add(3);
        return state;
    }

    [Fact]
    public void War_IsForbidden_BetweenMembersWhilePeaceLasts()
    {
        KmudState state = HeldState();
        Assert.True(KmudRules.WarForbidden(state, 1, 2, 205, out string reason));
        Assert.Contains("запрет войн", reason);
    }

    [Fact]
    public void War_IsAllowed_AfterTruceExpires()
    {
        KmudState state = HeldState();
        Assert.False(KmudRules.WarForbidden(state, 1, 2, 220, out _));
    }

    [Fact]
    public void War_IsAllowed_AgainstNonMember()
    {
        KmudState state = HeldState();
        // 9 — держава, которой не было за столом (появилась позже).
        Assert.False(KmudRules.WarForbidden(state, 1, 9, 205, out _));
    }

    [Fact]
    public void Pact_ForbidsWar_EvenAfterPeaceEnds()
    {
        KmudState state = HeldState();
        state.Pacts.Add(new KmudPact { A = 1, B = 3, UntilYear = 300 });
        // полный мир уже истёк, но пакт «только оборона» ещё держит
        Assert.True(KmudRules.WarForbidden(state, 3, 1, 250, out string reason));
        Assert.Contains("пакт", reason);
        Assert.False(KmudRules.WarForbidden(state, 1, 3, 300, out _));
    }

    [Fact]
    public void War_IsAllowed_BeforeFirstConference()
    {
        KmudState state = new KmudState();
        Assert.False(KmudRules.WarForbidden(state, 1, 2, 50, out _));
    }

    [Fact]
    public void Accusations_ArePardoned_OnlyWhenPunishmentHappened()
    {
        Assert.True(KmudVerdicts.PardonsAccusations(KmudOptions.PunishKillAndReform));
        Assert.True(KmudVerdicts.PardonsAccusations(KmudOptions.PunishReformOnly));
        Assert.True(KmudVerdicts.PardonsAccusations(KmudOptions.PunishSanctions));
        // «ничего не делать» — наказания не было, обвинения остаются.
        Assert.False(KmudVerdicts.PardonsAccusations(KmudOptions.PunishNothing));
    }

    [Fact]
    public void Pardon_RemovesOnlyThatKingdomsAccusations()
    {
        PoliticalWorld world = new PoliticalWorld();
        world.WarDeclarationsAdd(new WarDeclarationRecord { AttackerId = 5, DefenderId = 6, Year = 190 });
        world.WarDeclarationsAdd(new WarDeclarationRecord { AttackerId = 5, DefenderId = 7, Year = 195 });
        world.WarDeclarationsAdd(new WarDeclarationRecord { AttackerId = 6, DefenderId = 5, Year = 196 });
        Assert.Equal(2, world.PardonWarDeclarations(5));
        Assert.Single(world.WarDeclarations);
        Assert.Equal(6, world.WarDeclarations[0].AttackerId);
    }

    [Fact]
    public void MemberIds_SurviveSaving()
    {
        PoliticalWorld world = new PoliticalWorld();
        KmudRules.Convene(world.Kmud, new SimParams(), 200, 3, 1);
        world.Kmud.MemberIds.Add(4);
        world.Kmud.MemberIds.Add(9);
        world.Kmud.WarsPrevented = 7;
        PoliticalWorld copy = new PoliticalWorld();
        SaveData.Apply(copy, SaveData.Write(world));
        Assert.Equal(new List<int> { 4, 9 }, copy.Kmud.MemberIds);
        Assert.Equal(7, copy.Kmud.WarsPrevented);
        // после загрузки запрет войн продолжает действовать для тех же держав
        copy.Kmud.TruceUntilYear = 220;
        Assert.True(KmudRules.WarForbidden(copy.Kmud, 4, 9, 210, out _));
    }
}
