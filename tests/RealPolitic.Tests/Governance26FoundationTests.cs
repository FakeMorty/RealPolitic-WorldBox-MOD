using System;
using System.Collections.Generic;
using System.Linq;
using RealPolitic.Core;
using Xunit;

namespace RealPolitic.Tests;

public class Governance26FoundationTests
{
    private static CivicBallot People(int count=3,int duration=1,int quorum=0,BallotThreshold threshold=BallotThreshold.MoreThanHalf)
        =>new CivicBallot("test",BallotElectorKind.Person,10,duration,Enumerable.Range(1,count),new[]{CivicBallot.Yes,CivicBallot.No},quorum,threshold);
    [Fact]public void EveryPersonHasAnActualStoredVote()
    {
        var b=People(5);var seen=new List<int>();Assert.Equal(5,b.Collect(10,id=>{seen.Add(id);return id<=3?CivicBallot.Yes:CivicBallot.No;}));
        Assert.Equal(new[]{1,2,3,4,5},seen);Assert.Equal(5,b.Votes.Count);Assert.True(b.TryClose(11,out var tally));Assert.True(tally.BinaryAccepted);Assert.Equal(3,tally.Counts[CivicBallot.Yes]);
    }
    [Fact]public void DuplicateVotingIsImpossible(){var b=People();Assert.True(b.TryCast(1,CivicBallot.Yes,10));Assert.False(b.TryCast(1,CivicBallot.No,10));Assert.Equal(CivicBallot.Yes,b.Votes[1]);}
    [Theory][InlineData(0)][InlineData(-1)][InlineData(99)]public void ForeignAndFictionalVotersAreRejected(int id)=>Assert.False(People().TryCast(id,CivicBallot.Yes,10));
    [Fact]public void InvalidOptionRejected()=>Assert.False(People().TryCast(1,1234,10));
    [Fact]public void RollCannotContainDuplicateOrFictionalIds(){Assert.Throws<ArgumentException>(()=>new CivicBallot("x",BallotElectorKind.Person,1,1,new[]{1,1},new[]{1,2}));Assert.Throws<ArgumentException>(()=>new CivicBallot("x",BallotElectorKind.Person,1,1,new[]{-7},new[]{1,2}));}
    [Fact]public void NothingIsAcceptedBeforeClosing(){var b=People();b.Collect(10,_=>CivicBallot.Yes);Assert.False(b.Tally().BinaryAccepted);Assert.False(b.TryClose(10,out _));}
    [Fact]public void BoundaryAndDuplicateClosure(){var b=People();Assert.False(b.TryCast(1,1,9));Assert.False(b.TryCast(1,1,11));Assert.True(b.TryClose(11,out _));Assert.False(b.TryClose(12,out _));Assert.False(b.TryCast(2,1,10));}
    [Fact]public void AbstentionIsParticipationButNotAValidChoice(){var b=People(4,quorum:50);b.TryCast(1,1,10);b.TryCast(2,0,10);b.TryClose(11,out var t);Assert.Equal(2,t.Participated);Assert.Equal(1,t.ValidVotes);Assert.True(t.QuorumReached);Assert.True(t.BinaryAccepted);}
    [Fact]public void QuorumUsesTheFrozenRoll(){var b=People(5,quorum:50);b.TryCast(1,1,10);b.TryCast(2,1,10);b.TryClose(11,out var t);Assert.False(t.QuorumReached);Assert.False(t.BinaryAccepted);}
    [Fact]public void TieIsRejection(){var b=People(2);b.TryCast(1,1,10);b.TryCast(2,2,10);b.TryClose(11,out var t);Assert.False(t.BinaryAccepted);Assert.Equal(0,t.UniquePluralityWinner);}
    [Fact]public void ExactlyTwoThirdsIsEnough(){var b=People(3,threshold:BallotThreshold.AtLeastTwoThirds);b.Collect(10,id=>id<3?1:2);b.TryClose(11,out var t);Assert.True(t.BinaryAccepted);}
    [Fact]public void CountryMajorityUsesAllEligibleCountries(){var b=new CivicBallot("x",BallotElectorKind.Country,10,0,new[]{1,2,3},new[]{1,2});b.TryCast(1,1,10);b.TryCast(2,0,10);b.TryClose(10,out var t);Assert.False(t.BinaryAccepted);}
    [Fact]public void EmptyElectorateDoesNotApprove(){var b=People(0);b.TryClose(11,out var t);Assert.False(t.QuorumReached);Assert.False(t.BinaryAccepted);}
    [Fact]public void CaptureHasNoMutableAliases(){var b=People();b.TryCast(1,1,10);var s=b.Capture();s.Votes[1]=2;s.Electors.Clear();Assert.Equal(1,b.Votes[1]);Assert.Equal(3,b.EligibleCount);}
    [Fact]public void RestoreDoesNotRerollSubmittedVotes(){var b=People();b.TryCast(1,1,10);var restored=CivicBallot.Restore(b.Capture());var seen=new List<int>();restored.Collect(10,id=>{seen.Add(id);return 2;});Assert.Equal(new[]{2,3},seen);Assert.Equal(1,restored.Votes[1]);}
    [Fact]public void ClosedRestoreDoesNotRepeatClosure(){var b=People();b.Collect(10,_=>1);b.TryClose(11,out _);var c=CivicBallot.Restore(b.Capture());Assert.True(c.Tally().BinaryAccepted);Assert.False(c.TryClose(11,out _));}
    [Fact]public void SavedForeignVoteIsRejected(){var s=People().Capture();s.Votes[99]=1;Assert.Throws<ArgumentException>(()=>CivicBallot.Restore(s));}
    private static CivicPerson Person()=>new CivicPerson{ActorId=1,Age=25};
    [Fact]public void ActiveAndPassiveRightsAreIndependent(){var law=new ElectoralChapter{Electors=Franchise.Universal,Candidates=Franchise.Clan};law.Validate();Assert.True(law.MayVote(Person()));Assert.False(law.MayStand(Person(),0,0));}
    [Theory][InlineData(16,false)][InlineData(17,false)][InlineData(18,true)]public void VotingAgeIsEnforced(int age,bool eligible){var p=Person();p.Age=age;Assert.Equal(eligible,new ElectoralChapter().MayVote(p));}
    [Fact]public void DeadAndForeignPeopleDoNotVote(){var p=Person();p.Alive=false;Assert.False(new ElectoralChapter().MayVote(p));p.Alive=true;p.Citizen=false;Assert.False(new ElectoralChapter().MayVote(p));}
    [Fact]public void NotablesAndCityLeadersHaveExplicitFranchise(){var law=new ElectoralChapter{Electors=Franchise.Notables};var p=Person();Assert.False(law.MayVote(p));p.IsCityLeader=true;Assert.True(law.MayVote(p));}
    [Fact]public void LifetimeAndConsecutiveLimitsDiffer(){var law=new ElectoralChapter{MaximumTerms=2,TermLimitBasis=OfficeTermLimitBasis.Lifetime};Assert.False(law.MayStand(Person(),2,0));law.TermLimitBasis=OfficeTermLimitBasis.Consecutive;Assert.True(law.MayStand(Person(),2,0));Assert.False(law.MayStand(Person(),3,2));}
    [Fact]public void UnlimitedMeansNoTermCeiling(){var law=new ElectoralChapter{MaximumTerms=0};Assert.True(law.MayStand(Person(),100,100));}
    [Fact]public void UnapprovedNormsAreRejected()=>Assert.Throws<ArgumentException>(()=>new ElectoralChapter{VotingAge=4}.Validate());
    [Fact]public void ExitLawIsNotUniversalPermission(){var law=new ElectoralChapter();Assert.False(law.LegalExitPossible(true));law.RegionalExit=RegionalExitLaw.WithCentralPermission;Assert.False(law.LegalExitPossible(false));Assert.True(law.LegalExitPossible(true));law.RegionalExit=RegionalExitLaw.RegionalRight;Assert.True(law.LegalExitPossible(false));}
    [Fact]public void InitiativeNeedsThreeDifferentConsecutiveYears(){var r=new RegionalInitiative();Assert.False(r.Observe(1,.4f,2,false));Assert.False(r.Observe(1,.4f,2,false));Assert.False(r.Observe(2,.4f,2,false));Assert.True(r.Observe(3,.4f,2,false));}
    [Fact]public void ObservationGapResetsStreak(){var r=new RegionalInitiative();r.Observe(1,.4f,2,false);r.Observe(2,.4f,2,false);Assert.False(r.Observe(4,.4f,2,false));Assert.Equal(1,r.LowLoyaltyYears);}
    [Fact]public void CapitalAndLastCityNeverInitiate(){var r=new RegionalInitiative();for(int y=1;y<=5;y++)Assert.False(r.Observe(y,.1f,2,true));for(int y=6;y<=10;y++)Assert.False(r.Observe(y,.1f,1,false));}
    [Fact]public void FailedReferendumHasTenYearCooldown(){var r=new RegionalInitiative();r.RecordFailedReferendum(20);for(int y=21;y<30;y++)Assert.False(r.Observe(y,.2f,2,false));Assert.True(r.Observe(30,.2f,2,false));}
    [Fact]public void FortyFivePercentIsNotBelowFortyFive(){var r=new RegionalInitiative();for(int y=0;y<10;y++)Assert.False(r.Observe(y,.45f,2,false));}
    private static NationalPowerSnapshot Power(int id,int army=10,int pop=100,double income=10){var p=new NationalPowerSnapshot{CountryId=id,Warriors=army,Population=pop};p.RecentAnnualIncome.Add(income);return p;}
    private static List<NationalPowerSnapshot> Four()=>new List<NationalPowerSnapshot>{Power(1,40),Power(2,30),Power(3,20),Power(4,10)};
    private static InternationalRecognition Registry()
    {var r=new InternationalRecognition(true);for(int id=1;id<=4;id++)r.RegisterCountry(id,10);r.ReviewGreatPowers(50,Four());return r;}
    [Fact]public void EconomyUsesLastFiveAvailableYears(){var p=Power(1,income:9999);for(int i=0;i<5;i++)p.RecentAnnualIncome.Add(10);Assert.Equal(10,p.AverageIncome);}
    [Fact]public void ArmyCanOutweighPopulationAndIncome(){var a=Power(1,army:100,pop:1,income:1);var b=Power(2,army:0,pop:1000,income:1000);Assert.Equal(1,GreatPowerRanking.Rank(new[]{a,b})[0]);}
    [Fact]public void EqualPowerUsesStableIdNotInputOrder()=>Assert.Equal(new[]{1,2},GreatPowerRanking.Rank(new[]{Power(2),Power(1)}));
    [Fact]public void ZeroIncomeDoesNotDivideByZero()=>Assert.Equal(new[]{1,2},GreatPowerRanking.Rank(new[]{Power(2,0,0,0),Power(1,0,0,0)}));
    [Fact]public void FirstFiftyYearsAutomaticallyRecognized(){var r=new InternationalRecognition(true);for(int i=1;i<=8;i++)r.RegisterCountry(i,49);Assert.True(r.IsRecognized(8));Assert.False(r.ReviewGreatPowers(49,Four()));}
    [Fact]public void FifthNewStateAfterFiftyNeedsCouncil(){var r=Registry();r.RegisterCountry(5,51);Assert.False(r.IsRecognized(5));Assert.True(r.NeedsRecognitionVote(5));}
    [Fact]public void UnderThreeRecognizedCountriesAutoRecognize(){var r=new InternationalRecognition(true);r.RegisterCountry(1,100);r.RegisterCountry(2,100);r.RegisterCountry(3,100);r.RegisterCountry(4,100);Assert.True(r.IsRecognized(3));Assert.False(r.IsRecognized(4));}
    [Fact]public void OldWorldModeDoesNotRestrictExistingMechanics(){var r=new InternationalRecognition(false);Assert.True(r.CanContact(1,99,InternationalContact.Alliance));Assert.False(r.ReviewGreatPowers(500,Four()));}
    [Fact]public void UnrecognizedCannotRankThemselvesIntoCouncil(){var r=Registry();r.RegisterCountry(5,51);var powers=Four();powers.Add(Power(5,9999));r.ReviewGreatPowers(55,powers);Assert.DoesNotContain(5,r.GreatPowers);}
    [Fact]public void PowerReviewIsFiveYearly(){var r=Registry();var p=Four();p[3].Warriors=999;Assert.False(r.ReviewGreatPowers(54,p));Assert.True(r.ReviewGreatPowers(55,p));Assert.Contains(4,r.GreatPowers);}
    [Fact]public void DisappearedPowerIsReplacedImmediately(){var r=Registry();r.DestroyCountry(1);Assert.True(r.ReviewGreatPowers(51,Four().Where(p=>p.CountryId!=1).ToList()));Assert.Equal(new[]{2,3,4},r.GreatPowers);}
    [Fact]public void PublishedCompositionIsAHistoricalSnapshot(){var r=new InternationalRecognition(true);for(int i=1;i<=4;i++)r.RegisterCountry(i,10);IReadOnlyList<int> first=null;r.CompositionChanged+=ids=>{if(first==null)first=ids;};r.ReviewGreatPowers(50,Four());var next=Four();next[3].Warriors=999;r.ReviewGreatPowers(55,next);Assert.Equal(new[]{1,2,3},first);Assert.Contains(4,r.GreatPowers);}
    [Fact]public void UnchangedCouncilDoesNotRepublishOrAdvanceGeneration(){var r=Registry();int n=0;r.CompositionChanged+=_=>n++;int g=r.CompositionGeneration;Assert.False(r.ReviewGreatPowers(55,Four()));Assert.Equal(0,n);Assert.Equal(g,r.CompositionGeneration);}
    [Fact]public void TwoOfThreeRecognizeGlobally(){var r=Registry();r.RegisterCountry(5,51);var b=r.OpenRecognitionVote(5,51,0);b.Collect(51,id=>id==1?2:1);Assert.True(r.CompleteRecognitionVote(5,51,out bool yes));Assert.True(yes);Assert.True(r.IsRecognized(5));Assert.True(r.CanContact(4,5,InternationalContact.Trade));}
    [Fact]public void OneYesAndTwoAbstentionsDoNotRecognize(){var r=Registry();r.RegisterCountry(5,51);var b=r.OpenRecognitionVote(5,51,0);b.Collect(51,id=>id==1?1:0);r.CompleteRecognitionVote(5,51,out bool yes);Assert.False(yes);}
    [Fact]public void RejectionWaitsForCompositionNotFiveYearTimer(){var r=Registry();r.RegisterCountry(5,51);r.OpenRecognitionVote(5,51,0).Collect(51,_=>2);r.CompleteRecognitionVote(5,51,out _);r.ReviewGreatPowers(55,Four());Assert.False(r.NeedsRecognitionVote(5));var p=Four();p[3].Warriors=999;r.ReviewGreatPowers(60,p);Assert.True(r.NeedsRecognitionVote(5));}
    [Fact]public void RecognitionIsPermanentAcrossCouncilChanges(){var r=Registry();r.RegisterCountry(5,51);r.OpenRecognitionVote(5,51,0).Collect(51,_=>1);r.CompleteRecognitionVote(5,51,out _);var p=Four();p[3].Warriors=999;r.ReviewGreatPowers(55,p);Assert.True(r.IsRecognized(5));Assert.False(r.NeedsRecognitionVote(5));}
    [Theory][InlineData(InternationalContact.Trade,false)][InlineData(InternationalContact.Alliance,false)][InlineData(InternationalContact.PoliticalUnion,false)][InlineData(InternationalContact.War,true)][InlineData(InternationalContact.Peace,true)]
    public void IsolationDoesNotGrantWarImmunity(InternationalContact contact,bool allowed){var r=Registry();r.RegisterCountry(5,51);Assert.Equal(allowed,r.CanContact(1,5,contact));}
    [Fact]public void LoadKeepsVotesAndGenerationWithoutEvents(){var r=Registry();r.RegisterCountry(5,51);var b=r.OpenRecognitionVote(5,51,3);b.TryCast(1,1,51);var s=r.Capture();var copy=InternationalRecognition.Restore(s);Assert.Equal(r.CompositionGeneration,copy.CompositionGeneration);Assert.False(copy.NeedsRecognitionVote(5));var restored=copy.Capture().Pending[5];Assert.Equal(1,restored.Votes[1]);Assert.False(copy.CompleteRecognitionVote(5,52,out _));}
    [Fact]public void CaptureCannotRevokeRecognition(){var r=Registry();var snapshot=r.Capture();snapshot.Countries[0].Recognized=false;Assert.True(r.IsRecognized(1));}
    [Fact]public void ADeadGreatPowerCannotFinishARecognitionRound(){var r=Registry();r.RegisterCountry(5,51);r.OpenRecognitionVote(5,51,3).Collect(51,_=>1);r.DestroyCountry(1);Assert.False(r.CompleteRecognitionVote(5,54,out _));Assert.Empty(r.Capture().Pending);}
    [Fact]public void SavingBetweenDeathAndReplacementKeepsTheWorldLoadable(){var r=Registry();r.DestroyCountry(1);var restored=InternationalRecognition.Restore(r.Capture());Assert.True(restored.ReviewGreatPowers(51,Four().Where(p=>p.CountryId!=1).ToList()));Assert.Equal(new[]{2,3,4},restored.GreatPowers);}
    [Fact]public void ExclusionSubjectHasNoVote(){var b=AllianceBallots.Open("kick",new[]{1,2,3},3,10,0);Assert.Equal(2,b.EligibleCount);Assert.False(b.TryCast(3,2,10));b.Collect(10,_=>1);b.TryClose(10,out var t);Assert.True(t.BinaryAccepted);}
    [Fact]public void AdmissionNeedsApplicantConsent(){var b=AllianceBallots.Open("join",new[]{1,2,3},4,10,0);b.Collect(10,_=>1);b.TryClose(10,out _);Assert.False(AllianceBallots.MayAdmit(b,false));Assert.True(AllianceBallots.MayAdmit(b,true));}
    [Fact]public void LateCouncilCollapseAutoRecognizesOnlyNewBirthsNotPreviouslyRefusedCountries()
    {
        var r=Registry();r.RegisterCountry(5,51);r.OpenRecognitionVote(5,51,0).Collect(51,_=>2);r.CompleteRecognitionVote(5,51,out _);
        r.DestroyCountry(1);r.DestroyCountry(2);r.DestroyCountry(4);r.ReviewGreatPowers(100,new[]{Power(3),Power(5,999)});
        r=InternationalRecognition.Restore(r.Capture());Assert.Empty(r.GreatPowers);Assert.False(r.IsRecognized(5));
        r.RegisterCountry(6,100);r.RegisterCountry(7,100);r.RegisterCountry(8,100);
        Assert.True(r.IsRecognized(6));Assert.True(r.IsRecognized(7));Assert.False(r.IsRecognized(8));Assert.False(r.IsRecognized(5));
        r.ReviewGreatPowers(100,new[]{Power(3),Power(5,999),Power(6),Power(7),Power(8,999)});
        Assert.Equal(new[]{3,6,7},r.GreatPowers);Assert.True(r.NeedsRecognitionVote(5));Assert.False(r.IsRecognized(5));
    }

}
