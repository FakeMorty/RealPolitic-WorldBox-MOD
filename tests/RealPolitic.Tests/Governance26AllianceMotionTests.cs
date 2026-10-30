using System;
using System.Collections.Generic;
using System.Linq;
using RealPolitic.Core;
using Xunit;

namespace RealPolitic.Tests;
public sealed class Governance26AllianceMotionTests
{
    private sealed class Port:ICivicAllianceSource
    {
        public bool Valid=true,Executed=true;
        public int Opens,Executions,Duration;
        public Func<int,int> Vote=id=>1;
        public Func<int,bool> Available=id=>true;
        public bool StillValid(CivicAllianceMotion motion)=>Valid;
        public bool TryReadRules(int country,out ElectoralChapter rules)
        {rules=new ElectoralChapter{ForeignDecisions=Duration==0?ForeignDecisionAuthority.Ruler:ForeignDecisionAuthority.Electorate,BallotYears=Duration==0?1:Duration};return true;}
        public bool TryOpenPosition(AlliancePositionRequest r,out CivicProcedure procedure)
        {
            procedure=null;if(!Available(r.CountryId))return false;Opens++;
            var b=new CivicBallot(r.Id,BallotElectorKind.Person,r.Year,Duration,new[]{100+r.CountryId},new[]{1,2},Duration==0?0:r.Rules.ReferendumQuorumPercent,r.Rules.ReferendumThreshold);
            b.Collect(r.Year,_=>Vote(r.CountryId));procedure=new CivicProcedure(b,CivicProcedureKind.AlliancePosition,r.CountryId,targetCountryId:r.SubjectId);return true;
        }
        public bool TryExecute(CivicAllianceMotion motion){Executions++;return Executed;}
    }
    private static PoliticalWorld World(){var w=new PoliticalWorld();w.BeginNewWorldGovernance("alliance-test");w.SetYear(51);return w;}
    private static CivicAllianceMotion Propose(PoliticalWorld w,Port p,CivicAllianceAction action=CivicAllianceAction.Admission,int proposer=1,int year=51)
        =>CivicAllianceMotions.Propose(w.Governance,action,8,proposer,5,action==CivicAllianceAction.Admission?new[]{1,2,3}:new[]{1,2,3,5},year,p);
    [Fact]public void AdmissionRequiresMajorityOfAllMembersAndTheApplicantsConstitutionalConsent()
    {var w=World();var p=new Port{Vote=id=>id==3?2:1};var m=Propose(w,p);CivicAllianceMotions.Tick(w,51,p);Assert.Equal(CivicMotionStage.Executed,m.Stage);Assert.Equal(4,p.Opens);Assert.Equal(1,p.Executions);}
    [Fact]public void ApplicantCannotVoteOnItsOwnAdmission()
    {var w=World();var p=new Port();var m=Propose(w,p);Assert.DoesNotContain(5,m.Ballot.Capture().Electors);CivicAllianceMotions.Tick(w,51,p);Assert.True(w.Governance.Procedures.ContainsKey(m.ConsentId));Assert.False(w.Governance.Procedures.ContainsKey(m.PositionId(5)));}
    [Fact]public void ExpulsionTargetDoesNotVoteAndDoesNotHaveAVeto()
    {var w=World();var p=new Port{Vote=id=>id==5?2:1};var m=Propose(w,p,CivicAllianceAction.Expulsion);CivicAllianceMotions.Tick(w,51,p);Assert.Equal(CivicMotionStage.Executed,m.Stage);Assert.Equal(3,p.Opens);Assert.False(w.Governance.Procedures.ContainsKey(m.ConsentId));}
    [Fact]public void TwoYesOfThreeIsAValidStrictMajority()
    {var w=World();var p=new Port{Vote=id=>id==3?0:1};var m=Propose(w,p);CivicAllianceMotions.Tick(w,51,p);Assert.Equal(CivicMotionStage.Executed,m.Stage);}
    [Fact]public void OneYesWithTwoMissingVotesIsNotAMajorityOfThosePresent()
    {var w=World();var p=new Port{Available=id=>id==1 || id==5};var m=Propose(w,p);CivicAllianceMotions.Tick(w,51,p);CivicAllianceMotions.Tick(w,52,p);Assert.Equal(CivicMotionStage.Rejected,m.Stage);Assert.Equal(0,p.Executions);}
    [Fact]public void ApplicantMayDeclineDespiteAUnanimousAlliance()
    {var w=World();var p=new Port{Vote=id=>id==5?2:1};var m=Propose(w,p);CivicAllianceMotions.Tick(w,51,p);Assert.Equal(CivicMotionStage.Rejected,m.Stage);Assert.Equal("applicant-did-not-consent",m.Note);Assert.Equal(0,p.Executions);}
    [Fact]public void MissingApplicantDecisionIsNotInventedConsent()
    {var w=World();var p=new Port{Available=id=>id!=5};var m=Propose(w,p);CivicAllianceMotions.Tick(w,51,p);CivicAllianceMotions.Tick(w,52,p);Assert.Equal(CivicMotionStage.Rejected,m.Stage);}
    [Fact]public void AnyExistingMemberMayProposeAnExpulsion()
    {var w=World();var p=new Port();var m=Propose(w,p,CivicAllianceAction.Expulsion,3);CivicAllianceMotions.Tick(w,51,p);Assert.Equal(3,m.ProposerId);Assert.Equal(CivicMotionStage.Executed,m.Stage);}
    [Fact]public void AnOutsiderCannotProposeOnBehalfOfTheAlliance()
    {var w=World();Assert.Throws<ArgumentException>(()=>Propose(w,new Port(),CivicAllianceAction.Expulsion,99));Assert.Empty(w.Governance.AllianceMotions);}
    [Theory][InlineData(1)][InlineData(3)]public void NationalReferendaFinishBeforeAllianceCounting(int duration)
    {var w=World();var p=new Port{Duration=duration};var m=Propose(w,p);CivicAllianceMotions.Tick(w,51,p);Assert.Equal(CivicMotionStage.Voting,m.Stage);Assert.Equal(0,p.Executions);CivicAllianceMotions.Tick(w,51+duration,p);Assert.Equal(CivicMotionStage.Executed,m.Stage);}
    [Fact]public void ReloadKeepsAllPersonalVotesEvenIfTheSourceChangesItsMind()
    {
        var w=World();var p=new Port{Duration=3};var m=Propose(w,p);CivicAllianceMotions.Tick(w,51,p);var loaded=new PoliticalWorld();SaveData.Apply(loaded,SaveData.Write(w));
        var opposite=new Port{Duration=3,Vote=id=>2};CivicAllianceMotions.Tick(loaded,54,opposite);
        Assert.Equal(0,opposite.Opens);Assert.Equal(CivicMotionStage.Executed,loaded.Governance.AllianceMotions[m.Id].Stage);
    }
    [Fact]public void RepeatedTickAndReloadDoNotExecuteAnAlreadyAppliedMotion()
    {
        var w=World();var p=new Port();var m=Propose(w,p);CivicAllianceMotions.Tick(w,51,p);CivicAllianceMotions.Tick(w,51,p);
        var loaded=new PoliticalWorld();SaveData.Apply(loaded,SaveData.Write(w));CivicAllianceMotions.Tick(loaded,52,p);Assert.Equal(1,p.Executions);
    }
    [Fact]public void MembershipChangeCancelsRatherThanChangesTheDenominator()
    {
        var w=World();var p=new Port{Duration=3};var m=Propose(w,p);CivicAllianceMotions.Tick(w,51,p);p.Valid=false;CivicAllianceMotions.Tick(w,52,p);
        Assert.Equal(CivicMotionStage.Cancelled,m.Stage);Assert.Equal(3,m.Ballot.EligibleCount);Assert.Equal(0,p.Executions);
        Assert.All(w.Governance.Procedures.Values,x=>Assert.Equal(CivicProcedureStage.Cancelled,x.Stage));
    }
    [Fact]public void NativeRefusalIsNotAnnouncedAsSuccessfulAdmission()
    {var w=World();var p=new Port{Executed=false};var m=Propose(w,p);CivicAllianceMotions.Tick(w,51,p);Assert.Equal(CivicMotionStage.ExecutionFailed,m.Stage);Assert.Equal("native-execution-unconfirmed",m.Note);}
    [Fact]public void DuplicateActiveSubjectMotionIsNotCreatedByAnotherProposer()
    {var w=World();var p=new Port{Duration=3};Assert.NotNull(Propose(w,p));Assert.Null(Propose(w,p,CivicAllianceAction.Admission,2));Assert.Single(w.Governance.AllianceMotions);}
    [Fact]public void ARefusedCandidateIsNotAskedAgainUntilFifteenYearsPass()
    {
        var w=World();var p=new Port{Vote=id=>id==5?2:1};var m=Propose(w,p);CivicAllianceMotions.Tick(w,51,p);
        Assert.Equal(CivicMotionStage.Rejected,m.Stage);
        Assert.Null(Propose(w,p,year:60));
        Assert.NotNull(Propose(w,p,year:66));
    }
    [Fact]public void AnExistingAllianceCannotChainAnotherAdmissionForFiveYears()
    {
        var w=World();var p=new Port();var m=Propose(w,p);CivicAllianceMotions.Tick(w,51,p);
        Assert.Equal(CivicMotionStage.Executed,m.Stage);
        Assert.Null(CivicAllianceMotions.Propose(w.Governance,CivicAllianceAction.Admission,8,1,4,new[]{1,2,3,5},53,p));
        Assert.NotNull(CivicAllianceMotions.Propose(w.Governance,CivicAllianceAction.Admission,8,1,4,new[]{1,2,3,5},56,p));
    }
    [Fact]public void NewlyFoundedAllianceRequiresBothFoundersConsent()
    {
        var w=World();var p=new Port();var m=CivicAllianceMotions.Propose(w.Governance,CivicAllianceAction.Admission,0,1,5,new[]{1},51,p);
        CivicAllianceMotions.Tick(w,51,p);Assert.Equal(CivicMotionStage.Executed,m.Stage);Assert.Equal(2,p.Opens);Assert.Equal(1,m.Ballot.EligibleCount);
    }
    [Fact]public void AllianceCannotExpelItsProposerFromItsOwnMotion()
    {var w=World();Assert.Throws<ArgumentException>(()=>CivicAllianceMotions.Propose(w.Governance,CivicAllianceAction.Expulsion,8,5,5,new[]{1,2,3,5},51,new Port()));}
    [Fact]public void AnAdmissionSubjectCannotAlreadyBeAFrozenMember()
    {var w=World();Assert.Throws<ArgumentException>(()=>CivicAllianceMotions.Propose(w.Governance,CivicAllianceAction.Admission,8,1,5,new[]{1,5},51,new Port()));}
    [Fact]public void AllAbstentionsNeverGiveAdmissionConsent()
    {var w=World();var p=new Port{Vote=id=>0};var m=Propose(w,p);CivicAllianceMotions.Tick(w,51,p);Assert.Equal(CivicMotionStage.Rejected,m.Stage);Assert.Equal(3,m.Ballot.Tally().Abstained);}
    [Fact]public void LegacyStateCannotAcquireAllianceMotions()
    {var w=new PoliticalWorld();Assert.Null(CivicAllianceMotions.Propose(w.Governance,CivicAllianceAction.Admission,8,1,5,new[]{1,2},51,new Port()));Assert.Empty(w.Governance.AllianceMotions);}
    [Fact]public void ACorruptFrozenMemberRollIsRejectedBeforeTheCurrentWorldIsCleared()
    {
        var w=World();var p=new Port();Propose(w,p);string save=SaveData.Write(w).Replace("\"members\":[1,2,3]","\"members\":[1,2]");
        Assert.ThrowsAny<Exception>(()=>SaveData.Apply(w,save));Assert.Single(w.Governance.AllianceMotions);
    }
    [Fact]public void RecognitionTradeGateBlocksOpeningAndProfitWithoutDeletingHistory()
    {
        var w=World();for(int id=1;id<=4;id++)w.Governance.Recognition.RegisterCountry(id,id<4?1:51);
        Assert.NotNull(w.Trade.Open(1,2,51));Assert.Null(w.Trade.Open(1,4,51));
        w.Trade.Restore(new TradeRoute{Id=50,FromKingdomId=1,ToKingdomId=4});Assert.Equal(2,w.Trade.Count);Assert.Single(w.Trade.Of(1));Assert.Equal(1,w.Trade.OpenRoutesOf(1));
    }
    [Fact]public void FailedMemberMajorityDoesNotCountTheApplicantsYesAsAnotherMemberVote()
    {var w=World();var p=new Port{Vote=id=>id==1 || id==5?1:2};var m=Propose(w,p);CivicAllianceMotions.Tick(w,51,p);Assert.Equal(CivicMotionStage.Rejected,m.Stage);Assert.Equal(1,m.Ballot.Tally().Counts[1]);}
    [Fact]public void RestoredAdmissionCannotExecuteWithoutItsSavedApplicantProcedure()
    {
        var w=World();var port=new Port();var m=Propose(w,port);CivicAllianceMotions.Tick(w,51,port);
        string damaged=SaveData.Write(w).Replace("\"id\":\""+m.ConsentId+"\"","\"id\":\"wrong-consent\"");
        Assert.Throws<FormatException>(()=>SaveData.Apply(w,damaged));Assert.Equal(CivicMotionStage.Executed,w.Governance.AllianceMotions[m.Id].Stage);
    }
    [Fact]public void RestoredCountryPositionRequiresItsOwnIndividualVoteProcedure()
    {
        var w=World();var port=new Port();var m=Propose(w,port);CivicAllianceMotions.Tick(w,51,port);
        string damaged=SaveData.Write(w).Replace("\"id\":\""+m.PositionId(1)+"\"","\"id\":\"wrong-position\"");
        Assert.Throws<FormatException>(()=>SaveData.Apply(w,damaged));Assert.Equal(4,w.Governance.Procedures.Count);
    }
    [Fact]public void IndividualCountryAndApplicantResultsAreAnnouncedOnlyOnceAcrossReload()
    {
        var w=World();var port=new Port();var m=Propose(w,port);CivicAllianceMotions.Tick(w,51,port);
        Assert.Equal(4,w.Bus.Log.Count(e=>e.Type==PolEventType.NationalAlliancePosition));Assert.True(m.ConsentAnnounced);
        Assert.All(w.Governance.Procedures.Values,p=>Assert.Equal(CivicProcedureStage.Executed,p.Stage));
        var restored=new PoliticalWorld();SaveData.Apply(restored,SaveData.Write(w));CivicAllianceMotions.Tick(restored,52,port);
        Assert.DoesNotContain(restored.Bus.Log,e=>e.Type==PolEventType.NationalAlliancePosition);
    }

    [Theory][InlineData(1)][InlineData(3)]
    public void LateAggregationKeepsNationalVotesAlreadyCastDuringTheLegalPeriod(int duration)
    {
        var w=World();var p=new Port{Duration=duration};var m=Propose(w,p);CivicAllianceMotions.Tick(w,51,p);
        Assert.Equal(4,p.Opens);Assert.Equal(CivicMotionStage.Voting,m.Stage);
        CivicAllianceMotions.Tick(w,60,p);
        Assert.Equal(CivicMotionStage.Executed,m.Stage);Assert.Equal(3,m.Ballot.Votes.Count);Assert.Equal(1,p.Executions);
    }
    [Fact]public void LateAggregationAfterReloadDoesNotReplaceTheOriginalPersonalVotes()
    {
        var w=World();var p=new Port{Duration=3};var m=Propose(w,p);CivicAllianceMotions.Tick(w,51,p);
        var restored=new PoliticalWorld();SaveData.Apply(restored,SaveData.Write(w));var opposite=new Port{Duration=3,Vote=_=>2};
        CivicAllianceMotions.Tick(restored,60,opposite);
        Assert.Equal(CivicMotionStage.Executed,restored.Governance.AllianceMotions[m.Id].Stage);Assert.Equal(0,opposite.Opens);Assert.Equal(1,opposite.Executions);
    }
    [Fact]public void LateAggregationDoesNotInventMissingNationalDecisions()
    {
        var w=World();var p=new Port{Duration=3,Available=id=>id==1 || id==5};var m=Propose(w,p);CivicAllianceMotions.Tick(w,51,p);
        p.Available=_=>true;CivicAllianceMotions.Tick(w,60,p);
        Assert.Equal(CivicMotionStage.Rejected,m.Stage);Assert.Equal(2,p.Opens);Assert.Single(m.Ballot.Votes);Assert.Equal(0,p.Executions);
    }

    [Fact]public void AClosedSavedReferendumCannotBeSubmittedBeforeItsScheduledCompletion()
    {
        var w=World();var port=new Port{Duration=3};var m=Propose(w,port);
        var b=new CivicBallot(m.PositionId(1),BallotElectorKind.Person,51,3,new[]{101},new[]{1,2});
        Assert.True(b.TryCast(101,1,51));Assert.True(b.TryClose(54,out _));
        w.Governance.AddProcedure(new CivicProcedure(b,CivicProcedureKind.AlliancePosition,1,targetCountryId:5,stage:CivicProcedureStage.Approved));
        CivicAllianceMotions.Tick(w,51,port);Assert.Empty(m.Ballot.Votes);Assert.Equal(CivicMotionStage.Voting,m.Stage);
        CivicAllianceMotions.Tick(w,54,port);Assert.Equal(CivicMotionStage.Executed,m.Stage);
    }

}
