using System.Collections.Generic;
using RealPolitic.Core;
using Xunit;

namespace RealPolitic.Tests;

/// <summary>
/// 0.33, этап 3: голосование на КМУД — вопросы, мнения жителей, подсчёт.
/// </summary>
public class KmudVotingTests
{
    // ---------------- бюллетень и подсчёт ----------------

    [Fact]
    public void Ballot_StartsEmpty_WithEveryOptionAtZero()
    {
        KmudBallotResult ballot = new KmudBallotResult(KmudQuestion.PeaceYears);
        Assert.Equal(0, ballot.Voters);
        Assert.Equal(4, ballot.Counts.Count);
        Assert.Equal(0, ballot.CountOf(KmudOptions.Peace30));
    }

    [Fact]
    public void Ballot_CountsVotes_AndFindsWinner()
    {
        KmudBallotResult ballot = new KmudBallotResult(KmudQuestion.PeaceYears);
        for (int i = 0; i < 7; i++) ballot.Add(KmudOptions.Peace20);
        for (int i = 0; i < 3; i++) ballot.Add(KmudOptions.Peace0);
        ballot.Tally();
        Assert.Equal(10, ballot.Voters);
        Assert.Equal(KmudOptions.Peace20, ballot.Winner);
        Assert.False(ballot.Tied);
    }

    [Fact]
    public void Ballot_OnTie_PrefersLongerPeace()
    {
        KmudBallotResult ballot = new KmudBallotResult(KmudQuestion.PeaceYears);
        ballot.Add(KmudOptions.Peace10);
        ballot.Add(KmudOptions.Peace30);
        ballot.Tally();
        Assert.True(ballot.Tied);
        Assert.Equal(KmudOptions.Peace30, ballot.Winner);
    }

    [Fact]
    public void Ballot_OnTie_PrefersWhitePeace()
    {
        KmudBallotResult ballot = new KmudBallotResult(KmudQuestion.ConflictOutcome);
        ballot.Add(KmudOptions.OutcomeAggressor);
        ballot.Add(KmudOptions.OutcomeWhitePeace);
        ballot.Tally();
        Assert.True(ballot.Tied);
        Assert.Equal(KmudOptions.OutcomeWhitePeace, ballot.Winner);
    }

    [Fact]
    public void Ballot_OnTie_PrefersMilderPunishment()
    {
        KmudBallotResult ballot = new KmudBallotResult(KmudQuestion.Punish);
        ballot.Add(KmudOptions.PunishKillAndReform);
        ballot.Add(KmudOptions.PunishSanctions);
        ballot.Tally();
        Assert.Equal(KmudOptions.PunishSanctions, ballot.Winner);
    }

    [Fact]
    public void Ballot_WithoutVotes_HasNoWinner()
    {
        KmudBallotResult ballot = new KmudBallotResult(KmudQuestion.Aid);
        ballot.Tally();
        Assert.Equal(0, ballot.Winner);
    }

    [Fact]
    public void Ballot_IgnoresUnknownOptions()
    {
        KmudBallotResult ballot = new KmudBallotResult(KmudQuestion.Aid);
        ballot.Add(999, 5);
        Assert.Equal(0, ballot.Voters);
    }

    [Fact]
    public void VotingState_TallyAll_GivesAnswerForEveryQuestion()
    {
        KmudVotingState voting = new KmudVotingState();
        voting.Of(KmudQuestion.ConflictOutcome).Add(KmudOptions.OutcomeWhitePeace, 10);
        voting.Of(KmudQuestion.PeaceYears).Add(KmudOptions.Peace20, 10);
        voting.Of(KmudQuestion.Punish).Add(KmudOptions.PunishReformOnly, 10);
        voting.Of(KmudQuestion.Aid).Add(KmudOptions.AidFood, 10);
        voting.Of(KmudQuestion.Repatriation).Add(KmudOptions.RepatGuard, 10);
        Dictionary<KmudQuestion, int> winners = voting.TallyAll();
        Assert.Equal(5, winners.Count);
        Assert.Equal(KmudOptions.OutcomeWhitePeace, winners[KmudQuestion.ConflictOutcome]);
        Assert.Equal(KmudOptions.Peace20, winners[KmudQuestion.PeaceYears]);
        Assert.Equal(KmudOptions.PunishReformOnly, winners[KmudQuestion.Punish]);
        Assert.Equal(KmudOptions.AidFood, winners[KmudQuestion.Aid]);
        Assert.Equal(KmudOptions.RepatGuard, winners[KmudQuestion.Repatriation]);
        Assert.True(voting.Finalized);
    }

    [Fact]
    public void VotingState_OneCitizen_VotesOnceInTheList()
    {
        KmudVotingState voting = new KmudVotingState();
        Assert.True(voting.Voted.Add(501));
        Assert.False(voting.Voted.Add(501));
        Assert.Single(voting.Voted);
    }

    // ---------------- мнения жителей ----------------

    [Fact]
    public void Militarist_WantsAggressorWin_AndNoPeaceYears()
    {
        KmudPreferences.Situation s = new KmudPreferences.Situation { Own = IdeologyType.Militarism, Conviction = 0.9f };
        Assert.Equal(KmudOptions.OutcomeAggressor, KmudPreferences.Decide(KmudQuestion.ConflictOutcome, s));
        Assert.Equal(KmudOptions.Peace0, KmudPreferences.Decide(KmudQuestion.PeaceYears, s));
    }

    [Fact]
    public void PeacefulIdeology_WantsWhitePeace_AndLongPeace()
    {
        KmudPreferences.Situation s = new KmudPreferences.Situation { Own = IdeologyType.Egalitarianism, Conviction = 0.8f };
        Assert.Equal(KmudOptions.OutcomeWhitePeace, KmudPreferences.Decide(KmudQuestion.ConflictOutcome, s));
        Assert.Equal(KmudOptions.Peace30, KmudPreferences.Decide(KmudQuestion.PeaceYears, s));
    }

    [Fact]
    public void Defender_SidesWithDefender()
    {
        KmudPreferences.Situation s = new KmudPreferences.Situation
        {
            Own = IdeologyType.Monarchy,
            OwnIsDefender = true,
            WarWeary = false
        };
        Assert.Equal(KmudOptions.OutcomeDefender, KmudPreferences.Decide(KmudQuestion.ConflictOutcome, s));
    }

    [Fact]
    public void WarWearyAggressor_WantsWhitePeace()
    {
        KmudPreferences.Situation s = new KmudPreferences.Situation
        {
            Own = IdeologyType.Khaganate,
            OwnIsAggressor = true,
            WarWeary = true
        };
        Assert.Equal(KmudOptions.OutcomeWhitePeace, KmudPreferences.Decide(KmudQuestion.ConflictOutcome, s));
    }

    [Fact]
    public void AccusedState_VotesAgainstItsOwnPunishment()
    {
        KmudPreferences.Situation s = new KmudPreferences.Situation
        {
            Own = IdeologyType.Communism,
            OwnIsAccused = true
        };
        Assert.Equal(KmudOptions.PunishNothing, KmudPreferences.Decide(KmudQuestion.Punish, s));
    }

    [Fact]
    public void HarshIdeology_DemandsLeaderExecution()
    {
        KmudPreferences.Situation s = new KmudPreferences.Situation { Own = IdeologyType.Egalitarianism, Conviction = 0.9f };
        Assert.Equal(KmudOptions.PunishKillAndReform, KmudPreferences.Decide(KmudQuestion.Punish, s));
    }

    [Fact]
    public void LiberalPrefersSanctions_OverExecution()
    {
        KmudPreferences.Situation s = new KmudPreferences.Situation { Own = IdeologyType.Liberalism };
        int answer = KmudPreferences.Decide(KmudQuestion.Punish, s);
        Assert.NotEqual(KmudOptions.PunishKillAndReform, answer);
    }

    [Fact]
    public void PoorestState_VotesForMoney()
    {
        KmudPreferences.Situation s = new KmudPreferences.Situation
        {
            Own = IdeologyType.Monarchy,
            OwnIsPoorest = true
        };
        Assert.Equal(KmudOptions.AidMoney, KmudPreferences.Decide(KmudQuestion.Aid, s));
    }

    [Fact]
    public void PoorState_NeverVotesForAssociation()
    {
        KmudPreferences.Situation s = new KmudPreferences.Situation
        {
            Own = IdeologyType.Communism,
            OwnIsPoor = true
        };
        Assert.NotEqual(KmudOptions.AidAssociation, KmudPreferences.Decide(KmudQuestion.Aid, s));
    }

    [Fact]
    public void EveryIdeology_AnswersEveryQuestion()
    {
        foreach (IdeologyType ideology in System.Enum.GetValues(typeof(IdeologyType)))
        {
            KmudPreferences.Situation s = new KmudPreferences.Situation { Own = ideology };
            foreach (KmudQuestion question in System.Enum.GetValues(typeof(KmudQuestion)))
            {
                int answer = KmudPreferences.Decide(question, s);
                Assert.Contains(answer, KmudOptions.For(question));
            }
        }
    }

    [Fact]
    public void SameSituation_GivesSameAnswer()
    {
        KmudPreferences.Situation s = new KmudPreferences.Situation { Own = IdeologyType.Democracy, Conviction = 0.4f };
        for (int i = 0; i < 5; i++)
        {
            Assert.Equal(
                KmudPreferences.Decide(KmudQuestion.PeaceYears, s),
                KmudPreferences.Decide(KmudQuestion.PeaceYears, s));
        }
    }

    // ---------------- сохранение ----------------

    [Fact]
    public void Ballots_SurviveSaveAndLoad()
    {
        PoliticalWorld world = new PoliticalWorld();
        KmudRules.Convene(world.Kmud, new SimParams(), 200, 5, 1);
        world.Kmud.Voting.Of(KmudQuestion.PeaceYears).Add(KmudOptions.Peace20, 12);
        world.Kmud.Voting.Of(KmudQuestion.Aid).Add(KmudOptions.AidFood, 4);
        world.Kmud.Agenda.AggressorId = 3;
        world.Kmud.Agenda.DefenderId = 5;
        world.Kmud.Agenda.WorstId = 3;
        world.Kmud.LastPeaceYears = 20;
        PoliticalWorld copy = new PoliticalWorld();
        SaveData.Apply(copy, SaveData.Write(world));
        Assert.Equal(12, copy.Kmud.Voting.Of(KmudQuestion.PeaceYears).CountOf(KmudOptions.Peace20));
        Assert.Equal(4, copy.Kmud.Voting.Of(KmudQuestion.Aid).CountOf(KmudOptions.AidFood));
        Assert.Equal(3, copy.Kmud.Agenda.AggressorId);
        Assert.Equal(5, copy.Kmud.Agenda.DefenderId);
        Assert.Equal(3, copy.Kmud.Agenda.WorstId);
        Assert.Equal(20, copy.Kmud.LastPeaceYears);
    }

    // ---------------- моральный счёт (голод) ----------------

    [Fact]
    public void Morality_FamineAlone_MakesStateAccused()
    {
        // Решение владельца: «добавь голод» — морить свой народ тяжело даже без войн.
        int worst = KmudMorality.Worst(new List<KmudMisdeed>
        {
            KmudMorality.Record(1, 0, false),
            KmudMorality.Record(2, 0, true)
        });
        Assert.Equal(2, worst);
    }

    [Fact]
    public void Morality_SingleWarWithoutFamine_IsNotEnough()
    {
        Assert.Equal(0, KmudMorality.Worst(new List<KmudMisdeed> { KmudMorality.Record(1, 1, false) }));
    }

    [Fact]
    public void Morality_TwoWarsWithoutFamine_IsEnough()
    {
        Assert.Equal(1, KmudMorality.Worst(new List<KmudMisdeed> { KmudMorality.Record(1, 2, false) }));
    }

    [Fact]
    public void Morality_MoreWarsBeatFamine()
    {
        int worst = KmudMorality.Worst(new List<KmudMisdeed>
        {
            KmudMorality.Record(1, 0, true),   // 2 очка: голод
            KmudMorality.Record(2, 3, false)   // 3 очка: три войны
        });
        Assert.Equal(2, worst);
    }

    [Fact]
    public void Morality_TieGoesToMoreWars_ThenToLowerId()
    {
        // 1 война + голод = 3 очка и 3 войны = 3 очка: обвиняем тот, кто развязал больше войн.
        int worst = KmudMorality.Worst(new List<KmudMisdeed>
        {
            KmudMorality.Record(5, 1, true),
            KmudMorality.Record(4, 3, false)
        });
        Assert.Equal(4, worst);
        // Полное равенство — меньший id, чтобы порядок был постоянным.
        Assert.Equal(3, KmudMorality.Worst(new List<KmudMisdeed>
        {
            KmudMorality.Record(7, 2, false),
            KmudMorality.Record(3, 2, false)
        }));
    }

    [Fact]
    public void Morality_ReasonsMentionFamineAndWars()
    {
        KmudMisdeed record = KmudMorality.Record(1, 2, true);
        List<string> reasons = record.Reasons();
        Assert.Contains(reasons, r => r.Contains("войн"));
        Assert.Contains(reasons, r => r.Contains("голод"));
    }

    [Fact]
    public void Morality_EmptyWorld_HasNoAccused()
    {
        Assert.Equal(0, KmudMorality.Worst(new List<KmudMisdeed>()));
        Assert.Equal(0, KmudMorality.Worst(null));
    }
}
