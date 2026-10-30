using System;
using System.Collections.Generic;
using System.Linq;
using RealPolitic.Core;
using Xunit;

namespace RealPolitic.Tests;

public sealed class Governance26DispatchTests
{
    private sealed class Source:INationalPositionSource
    {
        public int Calls;
        public Func<NationalPositionRequest,int> Choice=r=>CivicBallot.Yes;
        public Func<NationalPositionRequest,bool> Available=r=>true;
        public bool EmptyRoll;
        public bool WrongTarget;
        public bool TryOpenRecognitionPosition(NationalPositionRequest r,out CivicProcedure procedure)
        {
            Calls++;procedure=null;if(!Available(r))return false;
            bool ruler=r.Rules.ForeignDecisions==ForeignDecisionAuthority.Ruler;
            var ballot=new CivicBallot(r.ProcedureId,BallotElectorKind.Person,r.Year,ruler?0:r.Rules.BallotYears,
                EmptyRoll?Array.Empty<int>():new[]{r.CountryId*100+1},new[]{1,2},ruler?0:r.Rules.ReferendumQuorumPercent,
                ruler?BallotThreshold.MoreThanHalf:r.Rules.ReferendumThreshold);
            ballot.Collect(r.Year,_=>Choice(r));
            procedure=new CivicProcedure(ballot,CivicProcedureKind.RecognitionPosition,r.CountryId,
                targetCountryId:WrongTarget?999:r.TargetCountryId);return true;
        }
    }
    private static PoliticalWorld World()=>newWorld();
    private static PoliticalWorld newWorld(){var w=new PoliticalWorld();w.BeginNewWorldGovernance("dispatch-test");w.SetYear(51);return w;}
    private static List<GovernanceNation> Frame(int duration=0)
    {
        var list=new List<GovernanceNation>();
        for(int id=1;id<=5;id++)list.Add(new GovernanceNation{CountryId=id,BirthYear=id==5?51:10,Name="Страна "+id,
            Population=100,Warriors=(5-id)*10,AnnualIncome=id*100,
            Rules=new ElectoralChapter{ForeignDecisions=duration==0?ForeignDecisionAuthority.Ruler:ForeignDecisionAuthority.Electorate,BallotYears=duration==0?1:duration}});
        return list;
    }
    private static bool Tick(PoliticalWorld w,int year,List<GovernanceNation> frame,Source source)
    {w.SetYear(year);return GovernanceDispatcher.Tick(w,year,frame,source);}
    [Fact]public void AllRulerPositionsResolveInTheOpeningYear()
    {
        var w=World();var s=new Source{Choice=r=>r.CountryId==1?2:1};Tick(w,51,Frame(),s);
        Assert.Equal(3,s.Calls);Assert.True(w.Governance.Recognition.IsRecognized(5));Assert.Empty(w.Governance.Recognition.Capture().Pending);
        Assert.All(w.Governance.Procedures.Values,p=>Assert.True(p.Ballot.Closed));
        Assert.Equal(2,w.Governance.Procedures.Values.Count(p=>p.Stage==CivicProcedureStage.Executed));
        Assert.Single(w.Bus.OfType(PolEventType.InternationalRecognitionDecided));
    }
    [Theory][InlineData(1)][InlineData(3)]public void PopulationVotesFinishBeforeTheInternationalDeadline(int duration)
    {
        var w=World();var frame=Frame(duration);var s=new Source();Tick(w,51,frame,s);
        Assert.False(w.Governance.Recognition.IsRecognized(5));Assert.Equal(3,s.Calls);
        for(int year=52;year<51+duration;year++)Tick(w,year,frame,s);
        Tick(w,51+duration,frame,s);Assert.True(w.Governance.Recognition.IsRecognized(5));Assert.Equal(3,s.Calls);
        Assert.Equal(3,w.Bus.OfType(PolEventType.NationalRecognitionPosition).Count);
    }
    [Fact]public void MixedConstitutionsDoNotDelayRulers()
    {
        var w=World();var f=Frame();f[2].Rules.ForeignDecisions=ForeignDecisionAuthority.Electorate;f[2].Rules.BallotYears=3;
        var s=new Source();Tick(w,51,f,s);
        Assert.Equal(2,w.Governance.Recognition.PendingVote(5).Votes.Count);Assert.False(w.Governance.Recognition.IsRecognized(5));
        Tick(w,54,f,s);Assert.True(w.Governance.Recognition.IsRecognized(5));
    }
    [Fact]public void ReloadPreservesVotesAndDoesNotRecallTheSource()
    {
        var w=World();var f=Frame(3);var s=new Source();Tick(w,51,f,s);w.SetYear(52);
        var copy=new PoliticalWorld();SaveData.Apply(copy,SaveData.Write(w));
        var opposite=new Source{Choice=r=>2};Tick(copy,52,f,opposite);Tick(copy,54,f,opposite);
        Assert.Equal(0,opposite.Calls);Assert.True(copy.Governance.Recognition.IsRecognized(5));
        Assert.All(copy.Governance.Procedures.Values,p=>Assert.Equal(1,p.Ballot.Votes.Values.Single()));
    }
    [Fact]public void RepeatedSameYearAndReloadedSameYearDoNothing()
    {
        var w=World();var f=Frame(3);var s=new Source();Assert.True(Tick(w,51,f,s));
        Assert.False(Tick(w,51,f,s));var copy=new PoliticalWorld();SaveData.Apply(copy,SaveData.Write(w));
        Assert.Equal(51,copy.Governance.LastDispatchYear);Assert.False(Tick(copy,51,f,s));Assert.Equal(3,s.Calls);
    }
    [Fact]public void UnchangedCompositionNeverReopensARefusal()
    {
        var w=World();var f=Frame();var no=new Source{Choice=r=>2};Tick(w,51,f,no);
        var yes=new Source();foreach(int year in new[]{52,55,56,100,150})Tick(w,year,f,yes);
        Assert.False(w.Governance.Recognition.IsRecognized(5));Assert.Equal(0,yes.Calls);
        Assert.Single(w.Bus.OfType(PolEventType.GreatPowerCompositionChanged));
        Assert.Single(w.Bus.OfType(PolEventType.InternationalRecognitionDecided));
    }
    [Fact]public void ChangedCompositionReopensARefusedCountry()
    {
        var w=World();var f=Frame();Tick(w,51,f,new Source{Choice=r=>2});f[3].Warriors=9999;
        Tick(w,56,f,new Source());Assert.True(w.Governance.Recognition.IsRecognized(5));
        Assert.Equal(2,w.Governance.Recognition.CompositionGeneration);
        Assert.Equal(2,w.Bus.OfType(PolEventType.InternationalRecognitionDecided).Count);
    }
    [Fact]public void DeadCouncilMemberIsReplacedWithoutWaitingFiveYears()
    {
        var w=World();var f=Frame(3);var s=new Source();Tick(w,51,f,s);
        var old=w.Governance.Procedures.Values.ToArray();f.RemoveAt(0);Tick(w,52,f,s);
        Assert.DoesNotContain(1,w.Governance.Recognition.GreatPowers);Assert.Contains(4,w.Governance.Recognition.GreatPowers);
        Assert.All(old,p=>Assert.Equal(CivicProcedureStage.Cancelled,p.Stage));
        Assert.Equal(3,w.Governance.Recognition.PendingVote(5).EligibleCount);
        Assert.Empty(w.Governance.Recognition.PendingVote(5).Votes);
        Tick(w,55,f,s);Assert.True(w.Governance.Recognition.IsRecognized(5));
    }
    [Fact]public void DeadApplicantCancelsItsNationalReferenda()
    {
        var w=World();var f=Frame(3);Tick(w,51,f,new Source());f.RemoveAt(4);Tick(w,52,f,new Source());
        Assert.Empty(w.Governance.Recognition.Capture().Pending);
        Assert.All(w.Governance.Procedures.Values,p=>Assert.Equal(CivicProcedureStage.Cancelled,p.Stage));
    }
    [Fact]public void HeadOrConstitutionChangesDoNotRecastAnExistingVote()
    {
        var w=World();var f=Frame(3);var s=new Source();Tick(w,51,f,s);
        f[0].Rules.ForeignDecisions=ForeignDecisionAuthority.Ruler;f[1].Rules.BallotYears=1;
        var opposite=new Source{Choice=r=>2};Tick(w,54,f,opposite);
        Assert.Equal(0,opposite.Calls);Assert.True(w.Governance.Recognition.IsRecognized(5));
    }
    [Fact]public void AnUnavailableHeadDoesNotBecomeAnInventedVote()
    {
        var w=World();var f=Frame();var s=new Source{Available=r=>r.CountryId!=3};Tick(w,51,f,s);
        Assert.Equal(2,w.Governance.Recognition.PendingVote(5).Votes.Count);
        Tick(w,52,f,s);Assert.True(w.Governance.Recognition.IsRecognized(5));
        Assert.Equal("1",w.Bus.OfType(PolEventType.InternationalRecognitionDecided).Single().Get("missing"));
    }
    [Fact]public void OneYesAndTwoUnavailableCountriesCannotRecognize()
    {
        var w=World();var f=Frame();var s=new Source{Available=r=>r.CountryId==1};Tick(w,51,f,s);Tick(w,52,f,s);
        Assert.False(w.Governance.Recognition.IsRecognized(5));Assert.False(w.Governance.Recognition.NeedsRecognitionVote(5));
    }
    [Fact]public void ThreeIndividualAbstentionsAreNotThreeExplicitNoVotes()
    {
        var w=World();Tick(w,51,Frame(),new Source{Choice=r=>0});var result=w.Bus.OfType(PolEventType.InternationalRecognitionDecided).Single();
        Assert.Equal("0",result.Get("yes"));Assert.Equal("0",result.Get("no"));Assert.Equal("3",result.Get("missing"));
    }
    [Fact]public void EmptyElectoratesHaveNoMandate()
    {
        var w=World();var f=Frame(1);var s=new Source{EmptyRoll=true};Tick(w,51,f,s);Tick(w,52,f,s);
        Assert.False(w.Governance.Recognition.IsRecognized(5));
        Assert.Equal("3",w.Bus.OfType(PolEventType.InternationalRecognitionDecided).Single().Get("missing"));
    }
    [Fact]public void LatePopulationReferendumIsNotStartedAfterItsCompletionWindow()
    {
        var w=World();var f=Frame(3);var unavailable=new Source{Available=r=>false};Tick(w,51,f,unavailable);
        var s=new Source();Tick(w,52,f,s);Assert.Equal(0,s.Calls);Tick(w,55,f,s);
        Assert.False(w.Governance.Recognition.IsRecognized(5));Assert.Empty(w.Governance.Procedures);
    }
    [Fact]public void MissingGreatPowerIncomeIsNotReplacedWithTreasury()
    {
        var w=World();var f=Frame();f[0].AnnualIncome=null;Tick(w,51,f,new Source());
        Assert.DoesNotContain(1,w.Governance.CaptureIncome().Keys);Assert.Contains(2,w.Governance.CaptureIncome().Keys);
    }
    [Fact]public void IncomeRetainsOnlyFiveDistinctAnnualSamples()
    {
        var w=World();var f=Frame();var s=new Source();for(int year=51;year<=57;year++){f[0].AnnualIncome=year;Tick(w,year,f,s);}
        Assert.Equal(new[]{53,54,55,56,57},w.Governance.CaptureIncome()[1].Keys);
    }
    [Fact]public void APartialInvalidFrameCannotKillCountriesOrRecordIncome()
    {
        var w=World();var f=Frame();Tick(w,51,f,new Source());int generation=w.Governance.Recognition.CompositionGeneration;
        f[1].Rules=null;f.RemoveAt(0);Assert.Throws<ArgumentException>(()=>Tick(w,52,f,new Source()));
        Assert.True(w.Governance.Recognition.IsRecognized(1));Assert.Equal(generation,w.Governance.Recognition.CompositionGeneration);
        Assert.Equal(51,w.Governance.LastDispatchYear);Assert.Single(w.Governance.CaptureIncome()[2]);
    }
    [Fact]public void DuplicateCountryIdsAreRejectedBeforeMutation()
    {
        var w=World();var f=Frame();f.Add(f[0]);Assert.Throws<ArgumentException>(()=>Tick(w,51,f,new Source()));
        Assert.Empty(w.Governance.Recognition.Capture().Countries);Assert.Empty(w.Governance.CaptureIncome());
    }
    [Fact]public void ExtinctIdentityCannotBeReused()
    {
        var w=World();var f=Frame();Tick(w,51,f,new Source());var original=f[0];f.RemoveAt(0);Tick(w,52,f,new Source());
        f.Add(original);Assert.Throws<ArgumentException>(()=>Tick(w,53,f,new Source()));
    }
    [Fact]public void LegacyWorldDoesNotReadThePortOrEnableItself()
    {
        var w=new PoliticalWorld();Assert.False(GovernanceDispatcher.Tick(w,507,null,null));Assert.False(w.Governance.Enabled);
    }
    [Fact]public void RecognitionRemainsPermanentAfterAPowerShift()
    {
        var w=World();var f=Frame();Tick(w,51,f,new Source());f[3].Warriors=9999;Tick(w,56,f,new Source{Choice=r=>2});
        Assert.True(w.Governance.Recognition.IsRecognized(5));Assert.Single(w.Bus.OfType(PolEventType.InternationalRecognitionDecided));
    }
    [Fact]public void AnEmptyValidLiveFrameMeansEveryCountryCeasedToExist()
    {
        var w=World();Tick(w,51,Frame(),new Source());Tick(w,52,new List<GovernanceNation>(),new Source());
        Assert.Empty(w.Governance.Recognition.GreatPowers);Assert.All(w.Governance.Recognition.Capture().Countries,c=>Assert.False(c.Alive));
    }
    [Fact]public void MismatchedNationalProcedureCannotBeSubmitted()
    {
        var w=World();Assert.Throws<InvalidOperationException>(()=>Tick(w,51,Frame(),new Source{WrongTarget=true}));
        Assert.Empty(w.Governance.Procedures);Assert.Empty(w.Governance.Recognition.PendingVote(5).Votes);
    }
    [Theory][InlineData(-2)][InlineData(1.5)]public void MalformedDispatchYearIsRejectedWithoutClearingTheWorld(double year)
    {
        var w=World();Tick(w,51,Frame(),new Source());string save=SaveData.Write(w);
        save=save.Replace("\"dispatchYear\":51","\"dispatchYear\":"+year.ToString(System.Globalization.CultureInfo.InvariantCulture));
        Assert.ThrowsAny<Exception>(()=>SaveData.Apply(w,save));Assert.True(w.Governance.Recognition.IsRecognized(5));
    }

    [Fact]public void AnnualRankingUsesIncomeAndExcludesAnUnrecognizedEconomicGiant()
    {
        var w=World();var f=Frame();foreach(var n in f){n.Warriors=10;n.Population=100;}
        f[4].AnnualIncome=999999;Tick(w,51,f,new Source{Available=r=>false});
        Assert.Equal(new[]{4,3,2},w.Governance.Recognition.GreatPowers);Assert.DoesNotContain(5,w.Governance.Recognition.GreatPowers);
    }
    [Fact]public void AClosedSavedBallotInTheVotingStageCanResumeWithoutRecasting()
    {
        var w=World();var f=Frame(3);Tick(w,51,f,new Source());
        foreach(var p in w.Governance.Procedures.Values)p.Ballot.TryClose(54,out _);
        w.SetYear(54);var restored=new PoliticalWorld();SaveData.Apply(restored,SaveData.Write(w));
        var s=new Source{Choice=r=>2};Tick(restored,54,f,s);
        Assert.Equal(0,s.Calls);Assert.True(restored.Governance.Recognition.IsRecognized(5));
    }
    [Theory][InlineData(1)][InlineData(3)]
    public void DelayedRecognitionCountKeepsAlreadyCollectedIndividualVotes(int duration)
    {
        var w=World();var f=Frame(duration);var source=new Source();Tick(w,51,f,source);Tick(w,60,f,source);
        Assert.True(w.Governance.Recognition.IsRecognized(5));Assert.Equal(3,source.Calls);
        Assert.Equal("3",w.Bus.OfType(PolEventType.InternationalRecognitionDecided).Single().Get("yes"));
    }
    [Fact]public void DelayedRecognitionAfterReloadDoesNotAskForNewVotes()
    {
        var w=World();var f=Frame(3);Tick(w,51,f,new Source());var copy=new PoliticalWorld();SaveData.Apply(copy,SaveData.Write(w));
        var opposite=new Source{Choice=_=>2};Tick(copy,60,f,opposite);
        Assert.True(copy.Governance.Recognition.IsRecognized(5));Assert.Equal(0,opposite.Calls);
    }
    [Fact]public void DelayedRecognitionDoesNotCreateAMissingPositionAfterTheDeadline()
    {
        var w=World();var f=Frame(3);var source=new Source{Available=r=>r.CountryId==1};Tick(w,51,f,source);
        source.Available=_=>true;Tick(w,60,f,source);
        Assert.False(w.Governance.Recognition.IsRecognized(5));Assert.Equal(3,source.Calls);
        Assert.Equal("1",w.Bus.OfType(PolEventType.InternationalRecognitionDecided).Single().Get("yes"));
    }
    [Fact]public void AClosedRecognitionReferendumCannotSubmitBeforeItsScheduledDate()
    {
        var w=World();var f=Frame(3);var source=new Source();Tick(w,51,f,source);
        Assert.True(w.Governance.Procedures.Values.First().Ballot.TryClose(54,out _));Tick(w,52,f,source);
        Assert.Empty(w.Governance.Recognition.PendingVote(5).Votes);Assert.False(w.Governance.Recognition.IsRecognized(5));
        Tick(w,54,f,source);Assert.True(w.Governance.Recognition.IsRecognized(5));
    }

}
