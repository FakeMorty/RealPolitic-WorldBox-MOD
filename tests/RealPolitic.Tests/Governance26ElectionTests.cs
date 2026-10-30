using System;
using System.Collections.Generic;
using System.Linq;
using RealPolitic.Core;
using Xunit;

namespace RealPolitic.Tests;
public sealed class Governance26ElectionTests
{
    private sealed class Port:ICivicHeadSource
    {
        public List<CivicResident> People=new List<CivicResident>{Resident(101,40),Resident(102,80,true),Resident(103,20)};
        public bool Readable=true,Installable=true;
        public int Calls,Installed;
        public bool TryReadCitizens(int country,out IList<CivicResident> residents){residents=People;return Readable;}
        public bool TryInstallHead(int country,int actor){Calls++;if(Installable)Installed=actor;return Installable;}
    }
    private static CivicResident Resident(int id,int age,bool leader=false)=>new CivicResident{Person=new CivicPerson{ActorId=id,Age=age,IsCityLeader=leader},
        Belief=new CivicBelief{Ideology=IdeologyType.Democracy,LastStateIdeology=IdeologyType.Democracy,Conviction=1},Name="Житель "+id};
    private static PoliticalWorld World()
    {
        var w=new PoliticalWorld();w.BeginNewWorldGovernance("election-world");w.SetYear(51);var c=w.AddKingdom(1,IdeologyType.Democracy,10);
        c.Constitution.StateForm=HeadOfState.Elected;c.Constitution.Electoral.Electors=Franchise.Universal;c.Constitution.Electoral.Candidates=Franchise.Universal;
        c.Constitution.Electoral.TermYears=5;c.Ruler=new RulerRef{ActorId=101,Name="Прежний",KingdomId=1};return w;
    }
    private static bool Tick(PoliticalWorld w,Port p,int year=51){w.SetYear(year);return CivicHeadElections.Tick(w,w.GetKingdom(1),year,p);}
    [Fact]public void ActualCandidateWinsByStoredPersonalVotes()
    {
        var w=World();var p=new Port();Assert.True(Tick(w,p));Assert.Equal(102,p.Installed);Assert.Equal(102,w.GetKingdom(1).Ruler.ActorId);
        var procedure=w.Governance.Procedures.Values.Single();Assert.Equal(CivicProcedureStage.Executed,procedure.Stage);
        Assert.Equal(new[]{101,102,103},procedure.Ballot.Votes.Keys.OrderBy(x=>x));Assert.All(procedure.Ballot.Votes.Values,v=>Assert.Equal(102,v));
        Assert.Equal(56,w.Governance.Offices[1].NextElectionYear);Assert.Equal(1,w.Governance.Offices[1].Mandates[102].Lifetime);
    }
    [Fact]public void DoesNotRunAgainInsideAnUnexpiredMandate()
    {var w=World();var p=new Port();Tick(w,p);Assert.False(Tick(w,p));Assert.False(Tick(w,p,55));Assert.Equal(1,p.Calls);Assert.Single(w.Governance.Procedures);}
    [Theory][InlineData(5)][InlineData(10)][InlineData(15)][InlineData(20)]public void ConstitutionalTermReplacesTheLegacyFifteenYears(int term)
    {var w=World();w.GetKingdom(1).Constitution.Electoral.TermYears=term;Tick(w,new Port());Assert.Equal(51+term,w.Governance.Offices[1].NextElectionYear);Assert.Equal(51+term,w.GetKingdom(1).Election.NextElectionYear);}
    [Theory][InlineData(1)][InlineData(2)][InlineData(3)]public void LifetimeLimitExcludesTheIncumbentAfterItsLastMandate(int limit)
    {
        var w=World();w.GetKingdom(1).Constitution.Electoral.MaximumTerms=limit;var p=new Port();
        for(int i=0;i<limit;i++)Assert.True(Tick(w,p,51+i*5));
        Assert.Equal(limit,w.Governance.Offices[1].Mandates[102].Lifetime);Assert.True(Tick(w,p,51+limit*5));
        Assert.Equal(101,p.Installed);Assert.Equal(0,w.Governance.Offices[1].Mandates[102].Consecutive);
    }
    [Fact]public void UnlimitedTermsHaveNoHiddenThreeTermCap()
    {var w=World();w.GetKingdom(1).Constitution.Electoral.MaximumTerms=0;var p=new Port();for(int i=0;i<5;i++)Assert.True(Tick(w,p,51+i*5));Assert.Equal(5,w.Governance.Offices[1].Mandates[102].Lifetime);}
    [Fact]public void ConsecutiveLimitAllowsAReturnAfterAnotherHead()
    {
        var w=World();var law=w.GetKingdom(1).Constitution.Electoral;law.MaximumTerms=1;law.TermLimitBasis=OfficeTermLimitBasis.Consecutive;
        var p=new Port();Tick(w,p);Tick(w,p,56);Assert.Equal(101,p.Installed);Tick(w,p,61);Assert.Equal(102,p.Installed);
        Assert.Equal(2,w.Governance.Offices[1].Mandates[102].Lifetime);Assert.Equal(1,w.Governance.Offices[1].Mandates[102].Consecutive);
    }
    [Fact]public void PassiveRightsAreSeparateFromTheActiveVotingAge()
    {
        var w=World();var law=w.GetKingdom(1).Constitution.Electoral;law.VotingAge=21;law.CandidacyAge=18;
        var p=new Port();p.People[2].Person.IsCityLeader=true;p.People[2].Person.Age=20;
        Assert.True(Tick(w,p));var ballot=w.Governance.Procedures.Values.Single().Ballot.Capture();
        Assert.Contains(103,ballot.Options);Assert.DoesNotContain(103,ballot.Electors);
    }
    [Theory][InlineData(18)][InlineData(25)][InlineData(35)]public void CandidateAgeIsAppliedToTheActualCandidateRoll(int age)
    {
        var w=World();w.GetKingdom(1).Constitution.Electoral.CandidacyAge=age;var p=new Port();p.People[0].Person.Age=age-1;
        Tick(w,p);Assert.DoesNotContain(101,w.Governance.Procedures.Values.Single().Ballot.Capture().Options);
    }
    [Fact]public void CandidateAndVoterCensusesAreAppliedIndependently()
    {
        var w=World();var law=w.GetKingdom(1).Constitution.Electoral;law.Electors=Franchise.Clan;law.Candidates=Franchise.Notables;
        var p=new Port();p.People[0].Person.IsRulingClan=true;Assert.True(Tick(w,p));
        var ballot=w.Governance.Procedures.Values.Single().Ballot.Capture();Assert.Equal(new[]{101},ballot.Electors);Assert.Equal(new[]{102},ballot.Options);
    }
    [Fact]public void ReferendumQuorumDoesNotInventAnUnapprovedHeadElectionQuorum()
    {var w=World();w.GetKingdom(1).Constitution.Electoral.ReferendumQuorumPercent=50;Tick(w,new Port());Assert.Equal(0,w.Governance.Procedures.Values.Single().Ballot.QuorumPercent);}
    [Fact]public void NativeApiRefusalDoesNotRecordAnyTermOrReplaceTheWinner()
    {
        var w=World();var p=new Port{Installable=false};Assert.False(Tick(w,p));var procedure=w.Governance.Procedures.Values.Single();
        Assert.Equal(CivicProcedureStage.ExecutionPending,procedure.Stage);Assert.Empty(w.Governance.Offices[1].Mandates);Assert.Equal(101,w.GetKingdom(1).Ruler.ActorId);
        Assert.Empty(w.Bus.OfType(PolEventType.DemocraticElection));
    }
    [Fact]public void ReloadRetriesOnlyTheSavedWinnerAndCommitsExactlyOnce()
    {
        var w=World();var p=new Port{Installable=false};Tick(w,p);var restored=new PoliticalWorld();SaveData.Apply(restored,SaveData.Write(w));
        p.Installable=true;p.People[0].Person.IsCityLeader=true;p.People[0].Person.Age=100;p.People[1].Person.IsCityLeader=false;
        Assert.True(Tick(restored,p,52));Assert.Equal(102,p.Installed);Assert.Equal(1,restored.Governance.Offices[1].Mandates[102].Lifetime);
        Assert.False(Tick(restored,p,52));Assert.Single(restored.Bus.OfType(PolEventType.DemocraticElection));
    }
    [Fact]public void ADeadOrEmigratedWinnerIsNeverSubstitutedByAnotherPerson()
    {
        var w=World();var p=new Port{Installable=false};Tick(w,p);p.People.RemoveAt(1);p.Installable=true;
        Assert.False(Tick(w,p,52));Assert.Equal(CivicProcedureStage.ExecutionFailed,w.Governance.Procedures.Values.Single().Stage);
        Assert.Empty(w.Governance.Offices[1].Mandates);Assert.Equal(1,p.Calls);
    }
    [Fact]public void EqualCandidatesCauseADeadlockNotAnIdTieBreak()
    {
        var w=World();var p=new Port();p.People[0].Person.Age=80;p.People[0].Person.IsCityLeader=true;
        Assert.False(Tick(w,p));Assert.Equal(0,p.Calls);Assert.Equal(CivicProcedureStage.Rejected,w.Governance.Procedures.Values.Single().Stage);
        Assert.False(Tick(w,p));Assert.Single(w.Governance.Procedures);
    }
    [Fact]public void NoCandidatesMeansNoSyntheticLeader()
    {var w=World();var p=new Port();foreach(var r in p.People)r.Person.Age=18;Assert.False(Tick(w,p));Assert.Empty(w.Governance.Procedures);Assert.Equal(0,p.Calls);}
    [Fact]public void MissingAuthoritativeRosterDoesNotShrinkTheElectorate()
    {var w=World();Assert.False(Tick(w,new Port{Readable=false}));Assert.Empty(w.Governance.Procedures);Assert.Empty(w.Governance.Offices[1].Mandates);}
    [Fact]public void DuplicateCitizenIdentityFailsBeforeAnyVotesOrNativeAction()
    {var w=World();var p=new Port();p.People.Add(p.People[0]);Assert.Throws<ArgumentException>(()=>Tick(w,p));Assert.Empty(w.Governance.Procedures);Assert.Equal(0,p.Calls);}
    [Theory][InlineData(HeadOfState.Hereditary)][InlineData(HeadOfState.ParliamentaryMonarchy)]public void HereditaryHeadsAreNotTurnedIntoPresidents(HeadOfState form)
    {var w=World();w.GetKingdom(1).Constitution.StateForm=form;Assert.False(Tick(w,new Port()));Assert.Empty(w.Governance.Offices);}
    [Fact]public void AChangedTermLawDoesNotRewriteAnAlreadyGrantedMandate()
    {var w=World();var p=new Port();Tick(w,p);w.GetKingdom(1).Constitution.Electoral.TermYears=20;Assert.False(Tick(w,p,55));Tick(w,p,56);Assert.Equal(76,w.Governance.Offices[1].NextElectionYear);}
    [Fact]public void ObservingAnUnelectedNativeReplacementTriggersAnElectionNotAFreeTerm()
    {var w=World();var p=new Port();Tick(w,p);w.GetKingdom(1).Ruler=new RulerRef{ActorId=103};Assert.True(Tick(w,p,52));Assert.Equal(102,w.GetKingdom(1).Ruler.ActorId);Assert.Equal(2,w.Governance.Offices[1].Mandates[102].Lifetime);}
    [Fact]public void OfficeHistoryRoundTripsAndReloadDoesNotAddATerm()
    {
        var w=World();var p=new Port();Tick(w,p);Tick(w,p,56);var copy=new PoliticalWorld();SaveData.Apply(copy,SaveData.Write(w));
        Assert.Equal(2,copy.Governance.Offices[1].Mandates[102].Lifetime);Assert.Equal(2,copy.Governance.Offices[1].Mandates[102].Consecutive);
        Assert.False(Tick(copy,p,56));Assert.Equal(61,copy.Governance.Offices[1].NextElectionYear);
    }
    [Fact]public void CommitIsIdempotentForTheSameProcedure()
    {var o=new CivicOfficeState{CountryId=1};o.Commit(10,1,5,"a");o.Commit(10,1,5,"a");Assert.Equal(1,o.Mandates[10].Lifetime);o.Validate();}
    [Fact]public void InvalidMandateHistoryIsRejectedBeforeTheCurrentWorldIsCleared()
    {
        var w=World();Tick(w,new Port());string save=SaveData.Write(w).Replace("\"consecutive\":1","\"consecutive\":9");
        Assert.ThrowsAny<Exception>(()=>SaveData.Apply(w,save));Assert.Equal(102,w.GetKingdom(1).Ruler.ActorId);Assert.Equal(1,w.Governance.Offices[1].Mandates[102].Lifetime);
    }
    [Fact]public void LegacyWorldDoesNotAcquireNewOfficeHistory()
    {var w=new PoliticalWorld();var c=w.AddKingdom(1,IdeologyType.Democracy,0);Assert.False(CivicHeadElections.Tick(w,c,100,new Port()));Assert.Empty(w.Governance.Offices);}
}
