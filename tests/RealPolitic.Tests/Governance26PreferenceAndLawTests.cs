using System;
using System.Collections.Generic;
using System.Linq;
using RealPolitic.Core;
using Xunit;

namespace RealPolitic.Tests;
public sealed class Governance26PreferenceAndLawTests
{
    private sealed class Port:ICivicHeadSource
    {
        public List<CivicResident> People;
        public bool TryReadCitizens(int id,out IList<CivicResident> people){people=People;return true;}
        public bool TryInstallHead(int country,int person)=>false;
    }
    [Theory][InlineData(1)][InlineData(2)][InlineData(7)][InlineData(19)][InlineData(37)][InlineData(73)][InlineData(911)][InlineData(2026)]
    public void RepresentativeOptimizationHasTheExactSamePersonalVotesAsTheFullCandidateRoll(int seed)
    {
        var random=new Random(seed);var w=new PoliticalWorld();w.BeginNewWorldGovernance("equivalence");var country=w.AddKingdom(1,IdeologyType.Democracy,0);
        country.Constitution.StateForm=HeadOfState.Elected;var law=country.Constitution.Electoral;
        law.Electors=law.Candidates=Franchise.Universal;law.MaximumTerms=0;law.VotingAge=18;law.CandidacyAge=18;
        var people=Enumerable.Range(1,160).Select(id=>new CivicResident{Person=new CivicPerson{ActorId=id,Age=random.Next(18,131),IsRulingClan=random.Next(2)==0,IsCityLeader=random.Next(4)==0},
            Belief=new CivicBelief{Ideology=(IdeologyType)random.Next(Enum.GetValues(typeof(IdeologyType)).Length),Conviction=(float)random.NextDouble(),Dissent=(float)random.NextDouble()},Name="Person "+id}).ToList();
        var office=w.Governance.OfficeFor(1);office.Commit(1,0,5,"previous");country.Ruler=new RulerRef{ActorId=1};
        CivicHeadElections.Tick(w,country,10,new Port{People=people});var ballot=Assert.Single(w.Governance.Procedures.Values).Ballot;
        // 0.48.5: если первый тур дал ровно поровну, мод проводит ВТОРОЙ тур, и в деле
        // остаётся его бюллетень (с двумя-тремя кандидатами, что поделили первое место).
        // Тогда сверяем личные голоса с выбором из оставшихся кандидатов — правило то же.
        var candidates=ballot.Capture().Options.Select(id=>people.Single(p=>p.Person.ActorId==id)).ToList();
        if(!ballot.Id.EndsWith(":runoff",StringComparison.Ordinal))Assert.Equal(160,candidates.Count);
        Assert.True(candidates.Count>=2);
        foreach(var voter in people)Assert.Equal(CivicHeadElections.Choose(voter,candidates,1),ballot.Votes[voter.Person.ActorId]);
    }
    [Fact]public void EqualCandidatesNeverAcquireAWinnerFromIterationOrder()
    {
        var a=new CivicResident{Person=new CivicPerson{ActorId=1,Age=50},Belief=new CivicBelief{Ideology=IdeologyType.Monarchy}};
        var b=new CivicResident{Person=new CivicPerson{ActorId=2,Age=50},Belief=new CivicBelief{Ideology=IdeologyType.Monarchy}};
        var voter=new CivicResident{Person=new CivicPerson{ActorId=3},Belief=new CivicBelief{Ideology=IdeologyType.Monarchy}};
        Assert.Equal(0,CivicHeadElections.Choose(voter,new[]{a,b},0));Assert.Equal(0,CivicHeadElections.Choose(voter,new[]{b,a},0));
    }
    [Theory][InlineData(false)][InlineData(true)]
    public void AllApprovedLawValuesAreReachableThroughReversibleAmendments(bool tighten)
    {
        var state=new KingdomPolitics{KingdomId=1,Ideology=IdeologyType.Monarchy};state.Constitution=new Constitution{Electoral=new ElectoralChapter()};var law=state.Constitution.Electoral;
        var seen=new Dictionary<string,HashSet<string>>();
        for(int sweep=0;sweep<6;sweep++)
        {
            foreach(bool direction in new[]{tighten,!tighten})for(int step=0;step<5;step++)
                foreach(var proposal in ConstitutionAmendments.Proposals(state,direction,false).Where(p=>p.Article.StartsWith("RP26_") || p.Article==ConstitutionAmendments.ArticleFranchise))
                {if(!seen.TryGetValue(proposal.Article,out var values))seen.Add(proposal.Article,values=new HashSet<string>());values.Add(proposal.From);values.Add(proposal.To);proposal.Apply(state.Constitution);law.Validate();}
        }
        Assert.Equal(3,seen["RP26_ART_VOTING_AGE"].Count);Assert.Equal(3,seen["RP26_ART_CANDIDACY_AGE"].Count);Assert.Equal(4,seen["RP26_ART_TERM"].Count);
        Assert.Equal(4,seen["RP26_ART_LIMIT"].Count);Assert.Equal(3,seen["RP26_ART_QUORUM"].Count);Assert.Equal(3,seen["RP26_ART_EXIT"].Count);
        Assert.Equal(2,seen["RP26_ART_LIMIT_BASIS"].Count);Assert.Equal(2,seen["RP26_ART_THRESHOLD"].Count);Assert.Equal(2,seen["RP26_ART_DURATION"].Count);
        Assert.Equal(2,seen["RP26_ART_FOREIGN"].Count);Assert.Equal(3,seen["RP26_ART_CANDIDATES"].Count);Assert.Equal(3,seen[ConstitutionAmendments.ArticleFranchise].Count);
    }
    [Fact]public void PassiveFranchiseAmendmentDoesNotChangeActiveRights()
    {
        var state=new KingdomPolitics{KingdomId=1,Ideology=IdeologyType.Monarchy,Constitution=new Constitution{Electoral=new ElectoralChapter()}};
        var p=ConstitutionAmendments.Proposals(state,true,false).Single(x=>x.Article=="RP26_ART_CANDIDATES");p.Apply(state.Constitution);
        Assert.Equal(Franchise.Universal,state.Constitution.Electoral.Electors);Assert.Equal(Franchise.Notables,state.Constitution.Electoral.Candidates);
    }
    [Fact]public void GovernmentStructuresHaveDistinctValidInitialCombinations()
    {
        var signatures=new HashSet<string>();foreach(HeadOfState form in Enum.GetValues(typeof(HeadOfState)))
        {var law=ElectoralDefaults.FromConstitution(new Constitution{StateForm=form});law.Validate();signatures.Add($"{law.VotingAge}/{law.CandidacyAge}/{law.TermYears}/{law.MaximumTerms}/{law.BallotYears}/{law.RegionalExit}");}
        Assert.Equal(5,signatures.Count);
    }
    [Fact]public void OfficeCannotCommitAnUnapprovedMandateLength()
    {var office=new CivicOfficeState{CountryId=1};Assert.Throws<ArgumentException>(()=>office.Commit(10,51,6,"wrong"));Assert.Equal(0,office.HolderId);Assert.Empty(office.Mandates);}
    [Fact]public void LegacyConstitutionsDoNotReceiveElectoralAmendments()
    {var state=new KingdomPolitics{KingdomId=1,Ideology=IdeologyType.Monarchy,Constitution=new Constitution()};Assert.DoesNotContain(ConstitutionAmendments.Proposals(state,true,false),p=>p.Article.StartsWith("RP26_"));Assert.Null(state.Constitution.Electoral);}
    [Fact]public void AbolishingAnElectiveOfficeTerminatesItsPendingExecutionWithoutPickingAReplacement()
    {
        var w=new PoliticalWorld();w.BeginNewWorldGovernance("law-change");var country=w.AddKingdom(1,IdeologyType.Democracy,0);country.Constitution.StateForm=HeadOfState.Elected;
        country.Constitution.Electoral=new ElectoralChapter();
        var resident=new CivicResident{Person=new CivicPerson{ActorId=101,Age=50},Belief=new CivicBelief{Ideology=IdeologyType.Democracy}};
        var port=new Port{People=new List<CivicResident>{resident}};CivicHeadElections.Tick(w,country,51,port);
        var pending=Assert.Single(w.Governance.Procedures.Values);Assert.Equal(CivicProcedureStage.ExecutionPending,pending.Stage);
        country.Constitution.StateForm=HeadOfState.Hereditary;Assert.False(CivicHeadElections.Tick(w,country,52,port));Assert.Equal(CivicProcedureStage.ExecutionFailed,pending.Stage);Assert.Empty(w.Governance.Offices[1].Mandates);
    }

}
