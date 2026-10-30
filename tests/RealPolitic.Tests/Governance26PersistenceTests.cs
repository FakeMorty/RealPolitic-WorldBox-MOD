using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using RealPolitic.Core;
using Xunit;

namespace RealPolitic.Tests;

public class Governance26PersistenceTests
{
    private static PoliticalWorld NewWorld()
    {
        var w=new PoliticalWorld();w.BeginNewWorldGovernance("native-world-test");w.SetYear(51);
        for(int id=1;id<=4;id++){w.AddKingdom(id,IdeologyType.Democracy,id==4?51:10);w.Governance.Recognition.RegisterCountry(id,id==4?51:10);}
        w.Governance.Recognition.ReviewGreatPowers(50,new[]{Power(1,30),Power(2,20),Power(3,10)});
        return w;
    }
    private static NationalPowerSnapshot Power(int id,int warriors)=>new NationalPowerSnapshot{CountryId=id,Warriors=warriors,Population=100};
    private static CivicProcedure AddProcedure(PoliticalWorld w,bool close=false)
    {
        var b=new CivicBallot("independence:1:9",BallotElectorKind.Person,51,3,new[]{101,102,103},new[]{1,2},50);
        b.TryCast(101,1,51);b.TryCast(102,1,51);if(close)b.TryClose(54,out _);
        var p=new CivicProcedure(b,CivicProcedureKind.Independence,1,9);w.Governance.AddProcedure(p);return p;
    }
    [Fact]public void FullSavePreservesFrozenRollAndVotes()
    {
        var w=NewWorld();AddProcedure(w);w.Governance.Recognition.OpenRecognitionVote(4,51,3).TryCast(1,2,51);
        var loaded=new PoliticalWorld();SaveData.Apply(loaded,SaveData.Write(w));
        Assert.True(loaded.Governance.Enabled);Assert.Equal("native-world-test",loaded.Governance.WorldKey);
        var ballot=loaded.Governance.Procedures["independence:1:9"].Ballot;Assert.Equal(3,ballot.EligibleCount);Assert.Equal(2,ballot.Votes.Count);
        Assert.False(ballot.TryCast(101,2,52));Assert.Equal(2,loaded.Governance.Recognition.Capture().Pending[4].Votes[1]);
    }
    [Theory][InlineData(0)][InlineData(1)][InlineData(49)][InlineData(507)]public void MissingMarkerNeverEnablesAnOldWorld(int year)
    {var w=new PoliticalWorld();w.BeginNewWorldGovernance("previous");SaveData.Apply(w,"{\"format\":17,\"year\":"+year+",\"kingdoms\":[]}");Assert.False(w.Governance.Enabled);Assert.Empty(w.Governance.Procedures);}
    [Fact]public void LegacySixteenStillLoads(){var w=new PoliticalWorld();SaveData.Apply(w,"{\"format\":16,\"year\":507,\"kingdoms\":[{\"id\":7,\"i\":1}]}");Assert.Equal(507,w.Year);Assert.NotNull(w.GetKingdom(7));Assert.False(w.Governance.Enabled);Assert.Null(w.GetKingdom(7).Constitution?.Electoral);}
    [Fact]public void ClearRemovesAllNewWorldState(){var w=NewWorld();AddProcedure(w);w.Clear();Assert.False(w.Governance.Enabled);Assert.Empty(w.Governance.Procedures);Assert.Empty(w.Governance.Recognition.GreatPowers);}
    [Fact]public void ElectoralChapterRoundTripsAllNorms()
    {
        var w=NewWorld();var c=w.GetKingdom(1).Constitution.Electoral;c.Electors=Franchise.Clan;c.Candidates=Franchise.Notables;c.VotingAge=16;c.CandidacyAge=35;
        c.TermYears=5;c.MaximumTerms=0;c.TermLimitBasis=OfficeTermLimitBasis.Consecutive;c.ReferendumQuorumPercent=25;c.ReferendumThreshold=BallotThreshold.AtLeastTwoThirds;
        c.BallotYears=3;c.ForeignDecisions=ForeignDecisionAuthority.Electorate;c.RegionalExit=RegionalExitLaw.WithCentralPermission;
        var copy=new PoliticalWorld();SaveData.Apply(copy,SaveData.Write(w));var x=copy.GetKingdom(1).Constitution.Electoral;
        Assert.Equal(Franchise.Clan,x.Electors);Assert.Equal(Franchise.Notables,x.Candidates);Assert.Equal(16,x.VotingAge);Assert.Equal(35,x.CandidacyAge);
        Assert.Equal(5,x.TermYears);Assert.Equal(0,x.MaximumTerms);Assert.Equal(c.TermLimitBasis,x.TermLimitBasis);Assert.Equal(25,x.ReferendumQuorumPercent);
        Assert.Equal(c.ReferendumThreshold,x.ReferendumThreshold);Assert.Equal(3,x.BallotYears);Assert.Equal(c.ForeignDecisions,x.ForeignDecisions);Assert.Equal(c.RegionalExit,x.RegionalExit);
    }
    [Fact]public void AmendmentAndNewChapterHaveOneElectorate(){var w=NewWorld();var c=w.GetKingdom(1).Constitution;c.Franchise=Franchise.Clan;Assert.Equal(Franchise.Clan,c.Electoral.Electors);c.Electoral.Electors=Franchise.Notables;Assert.Equal(Franchise.Notables,c.Franchise);}
    [Fact]public void ReformDoesNotResetTheNewElectoralChapter(){var w=NewWorld();var s=w.GetKingdom(1);s.Constitution.Electoral.TermYears=5;s.Constitution.Electoral.MaximumTerms=1;s.Ideology=IdeologyType.Aristocracy;w.RefreshConstitutionAfterIdeologyChange(s);Assert.Equal(5,s.Constitution.Electoral.TermYears);Assert.Equal(1,s.Constitution.Electoral.MaximumTerms);}
    [Fact]public void InitiativeAndCooldownSurviveSave(){var w=NewWorld();var i=w.Governance.InitiativeFor(9);i.Observe(50,.3f,2,false);i.RecordFailedReferendum(51);var copy=new PoliticalWorld();SaveData.Apply(copy,SaveData.Write(w));var j=copy.Governance.Initiatives[9];Assert.Equal(50,j.LastObservedYear);Assert.Equal(61,j.NextPermittedYear);Assert.False(j.Observe(52,.2f,2,false));}
    [Fact]public void IncomeKeepsFiveYearsAndDoesNotDuplicateOnLoad()
    {
        var w=NewWorld();for(int y=45;y<=51;y++)w.Governance.RecordIncome(1,y,y+.5);w.Governance.RecordIncome(1,51,99999);
        var copy=new PoliticalWorld();SaveData.Apply(copy,SaveData.Write(w));var p=Power(1,1);copy.Governance.FillIncome(p);
        Assert.Equal(new[]{47.5,48.5,49.5,50.5,51.5},p.RecentAnnualIncome);Assert.Equal(49.5,p.AverageIncome);
    }
    [Fact]public void BeliefsAreSavedNotRerolled(){var w=NewWorld();w.Governance.RememberBelief(101,new CivicBelief{Ideology=IdeologyType.Monarchy,Conviction=.91f,Dissent=.72f,KingdomId=1,LastStateIdeology=IdeologyType.Democracy,LastDissentYear=51});var copy=new PoliticalWorld();SaveData.Apply(copy,SaveData.Write(w));var b=copy.Governance.Beliefs[101];Assert.Equal(IdeologyType.Monarchy,b.Ideology);Assert.Equal(.91f,b.Conviction);Assert.Equal(.72f,b.Dissent);Assert.Equal(51,b.LastDissentYear);}
    [Fact]public void ExecutedActionIsNotVotingAgain(){var w=NewWorld();var p=AddProcedure(w,true);Assert.True(p.Transition(CivicProcedureStage.Approved));Assert.True(p.Transition(CivicProcedureStage.ExecutionPending));Assert.True(p.Transition(CivicProcedureStage.Executed,"native-confirmed"));var loaded=new PoliticalWorld();SaveData.Apply(loaded,SaveData.Write(w));var q=loaded.Governance.Procedures[p.Ballot.Id];Assert.Equal(CivicProcedureStage.Executed,q.Stage);Assert.Equal("native-confirmed",q.ExecutionNote);Assert.False(q.Ballot.TryClose(54,out _));Assert.False(q.Transition(CivicProcedureStage.ExecutionPending));}
    [Fact]public void RejectedBallotCannotBecomeAnApprovedAction(){var b=new CivicBallot("no",BallotElectorKind.Person,1,0,new[]{1},new[]{1,2});b.TryCast(1,2,1);b.TryClose(1,out _);var p=new CivicProcedure(b,CivicProcedureKind.CentralPermission,1);Assert.False(p.Transition(CivicProcedureStage.Approved));Assert.True(p.Transition(CivicProcedureStage.Rejected));}
    [Fact]public void CorruptGovernanceDoesNotClearTheRunningWorld(){var current=NewWorld();AddProcedure(current);string json=SaveData.Write(current).Replace("\"schema\":"+GovernanceCodec.SchemaVersion,"\"schema\":999");Assert.Throws<FormatException>(()=>SaveData.Apply(current,json));Assert.Equal(4,current.Kingdoms.Count);Assert.Single(current.Governance.Procedures);Assert.Equal("native-world-test",current.Governance.WorldKey);}
    [Fact]public void InvalidLawDoesNotClearTheRunningWorld(){var current=NewWorld();string json=SaveData.Write(current).Replace("\"votingAge\":18","\"votingAge\":3");Assert.Throws<FormatException>(()=>SaveData.Apply(current,json));Assert.Equal(4,current.Kingdoms.Count);Assert.Equal(18,current.GetKingdom(1).Constitution.Electoral.VotingAge);}
    [Fact]public void FutureFormatCannotSilentlyDropUnrecognizedFields(){var w=NewWorld();string json=SaveData.Write(w).Replace("\"format\":18","\"format\":999");Assert.Throws<FormatException>(()=>SaveData.Apply(w,json));Assert.Equal(4,w.Kingdoms.Count);}
    [Fact]public void MalformedKingdomCollectionDoesNotEraseCurrentWorld(){var w=NewWorld();Assert.Throws<FormatException>(()=>SaveData.Apply(w,"{\"format\":18,\"kingdoms\":false}"));Assert.Equal(4,w.Kingdoms.Count);}
    [Fact]public void NewElectoralLawNeverFallsBackToAnInventedRandomResult()
    {
        var previous=ElectionSystem.CivicElectionHandler;
        try{ElectionSystem.CivicElectionHandler=null;var w=NewWorld();var s=w.GetKingdom(1);s.Ruler=new RulerRef{ActorId=101,Name="Реальный"};Assert.False(ElectionSystem.HoldElection(s,100,w.Bus,new Random(1)));Assert.Equal(101,s.Ruler.ActorId);Assert.Empty(w.Bus.OfType(PolEventType.DemocraticElection));}
        finally{ElectionSystem.CivicElectionHandler=previous;}
    }
    [Fact]public void NewElectionDelegatesToTheActualBallotController()
    {
        var previous=ElectionSystem.CivicElectionHandler;
        try{var w=NewWorld();var state=w.GetKingdom(1);int calls=0;ElectionSystem.CivicElectionHandler=(s,y,b)=>{Assert.Same(state,s);Assert.Equal(100,y);calls++;return false;};Assert.False(ElectionSystem.HoldElection(state,100,w.Bus,new Random(1)));Assert.Equal(1,calls);}
        finally{ElectionSystem.CivicElectionHandler=previous;}
    }
    [Fact]public void StringIsNotABoolean(){var w=NewWorld();string json=SaveData.Write(w).Replace("\"enabled\":true","\"enabled\":\"true\"");Assert.Throws<FormatException>(()=>SaveData.Apply(w,json));Assert.True(w.Governance.Enabled);}
    [Fact]public void FractionalIdIsNotTruncated(){var w=NewWorld();string json=SaveData.Write(w).Replace("\"id\":1,\"birth\"","\"id\":1.5,\"birth\"");Assert.Throws<FormatException>(()=>SaveData.Apply(w,json));Assert.True(w.Governance.Enabled);}
    [Fact]public void SerializedNumbersDoNotDependOnLocalDecimalSeparator()
    {
        var old=CultureInfo.CurrentCulture;try{CultureInfo.CurrentCulture=CultureInfo.GetCultureInfo("ru-RU");var w=NewWorld();w.Governance.RecordIncome(1,50,123.125);string json=SaveData.Write(w);Assert.Contains("123.125",json);var x=new PoliticalWorld();SaveData.Apply(x,json);Assert.Equal(123.125,x.Governance.CaptureIncome()[1][50]);}finally{CultureInfo.CurrentCulture=old;}
    }
    [Fact]public void NoRecognitionEventsAreReplayedByLoad(){var w=NewWorld();var x=new PoliticalWorld();SaveData.Apply(x,SaveData.Write(w));Assert.Empty(x.Bus.Log);}
    [Theory][InlineData("{\"a\":1")][InlineData("[1,]")][InlineData("{\"a\":true,}")][InlineData("{\"a\":1,\"a\":2}")][InlineData("{\"a\":1,\"\\u0061\":2}")][InlineData("true junk")][InlineData("01")][InlineData("+1")][InlineData("1e9999")][InlineData("\"bad\\q\"")][InlineData("\"bad\nline\"")][InlineData("")][InlineData("{\"a\" 1}")]
    public void StrictParserRejectsDamagedOrAmbiguousJson(string text)=>Assert.Throws<FormatException>(()=>TinyJson.ParseStrict(text));
    [Theory][InlineData("{}")][InlineData("[]")][InlineData("null")][InlineData("[true,false,-1,0,1.25,1e-3,\"Привет\\u0021\"]")]
    public void StrictParserAcceptsValidJson(string text)=>Assert.NotNull(TinyJson.ParseStrict(text));
    [Fact]public void DeepMalformedSaveDoesNotOverflowParser(){string text=new string('[',70)+"0"+new string(']',70);Assert.Throws<FormatException>(()=>TinyJson.ParseStrict(text));}
    [Fact]public void PoliticalPreferencesFollowThePersonNotATargetPercentage()
    {
        var loyal=new CivicBelief{Ideology=IdeologyType.Monarchy,Conviction=1,Dissent=0,LastStateIdeology=IdeologyType.Monarchy};
        var rebel=new CivicBelief{Ideology=IdeologyType.Democracy,Conviction=1,Dissent=1,LastStateIdeology=IdeologyType.Monarchy};
        var question=new CivicIssueContext{CurrentGovernment=IdeologyType.Monarchy,ProposedGovernment=IdeologyType.Democracy,Independence=true};
        Assert.Equal(CivicBallot.No,CivicPreferences.Decide(loyal,question));Assert.Equal(CivicBallot.Yes,CivicPreferences.Decide(rebel,question));
    }
    [Fact]public void ProcedureOpeningExitBasisAndAuthorizationRoundTripAndValidateCrossLinks()
    {
        var w=NewWorld();
        var permBallot=new CivicBallot("regional:1:9:50",BallotElectorKind.Person,50,0,new[]{101},new[]{1,2});
        permBallot.TryCast(101,1,50);permBallot.TryClose(50,out _);
        var perm=new CivicProcedure(permBallot,CivicProcedureKind.CentralPermission,1,9,
            stage:CivicProcedureStage.Executed,executionNote:"central-permission-used",
            openingExitLaw:RegionalExitLaw.WithCentralPermission);
        w.Governance.AddProcedure(perm);
        var refBallot=new CivicBallot("regional:1:9:50:referendum",BallotElectorKind.Person,51,1,new[]{101,102},new[]{1,2});
        refBallot.TryCast(101,1,51);refBallot.TryCast(102,1,51);
        var regional=new CivicProcedure(refBallot,CivicProcedureKind.Independence,1,9,
            openingExitLaw:RegionalExitLaw.WithCentralPermission,authorizedByProcedureId:perm.Ballot.Id);
        w.Governance.AddProcedure(regional);
        string saved=SaveData.Write(w);
        var loaded=new PoliticalWorld();SaveData.Apply(loaded,saved);
        var restored=loaded.Governance.Procedures[regional.Ballot.Id];
        Assert.Equal(RegionalExitLaw.WithCentralPermission,restored.OpeningExitLaw);
        Assert.Equal(perm.Ballot.Id,restored.AuthorizedByProcedureId);
        Assert.True(restored.HasVerifiedAuthorization(loaded.Governance.Procedures));
        string forged=saved.Replace("\"authorizedBy\":\""+perm.Ballot.Id+"\"","\"authorizedBy\":\"missing-permission\"");
        Assert.Throws<FormatException>(()=>SaveData.Apply(new PoliticalWorld(),forged));
    }
}
