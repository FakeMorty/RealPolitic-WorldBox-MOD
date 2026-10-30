using System;
using RealPolitic.Core;
using Xunit;

namespace RealPolitic.Tests;
public sealed class Governance26ForeignInterestTests
{
    private static PoliticalWorld World()
    {var w=new PoliticalWorld();w.BeginNewWorldGovernance("interests");w.AddKingdom(1,IdeologyType.Monarchy,0);w.AddKingdom(2,IdeologyType.Monarchy,51);w.Governance.Recognition.RegisterCountry(1,0);w.Governance.Recognition.RegisterCountry(2,51);return w;}
    [Fact]public void RecordedMetropolisCanOpposeItsOwnBreakawaysRecognitionWithoutAVeto()
    {
        var w=World();w.Governance.RecordCountryBirth(2,1,51,false);var issue=CivicForeignInterests.Question(w,w.GetKingdom(1),w.GetKingdom(2),true);issue.IsRulingFamily=true;
        Assert.True(issue.RecognitionOfOurBreakaway);
        Assert.Equal(CivicBallot.No,CivicPreferences.Decide(new CivicBelief{Ideology=IdeologyType.Monarchy,Conviction=1},issue));
        Assert.Equal(1,CivicPreferences.Decide(new CivicBelief{Ideology=IdeologyType.Democracy,Conviction=1,Dissent=1},new CivicIssueContext{CurrentGovernment=IdeologyType.Monarchy,ProposedGovernment=IdeologyType.Democracy,RecognitionOfOurBreakaway=true}));
    }
    [Fact]public void OtherPowersDoNotInheritTheMetropolisTerritorialInterest()
    {var w=World();w.AddKingdom(3,IdeologyType.Monarchy,0);w.Governance.RecordCountryBirth(2,1,51,true);Assert.False(CivicForeignInterests.Question(w,w.GetKingdom(3),w.GetKingdom(2),true).RecognitionOfOurBreakaway);}
    [Fact]public void OriginSurvivesSavingAndDoesNotBecomeAutomaticRecognition()
    {var w=World();w.Governance.RecordCountryBirth(2,1,51,true);var restored=new PoliticalWorld();SaveData.Apply(restored,SaveData.Write(w));var birth=restored.Governance.CountryBirths[2];Assert.Equal(1,birth.ParentId);Assert.True(birth.Lawful);Assert.Equal(51,birth.Year);}
    [Fact]public void BirthMetadataCannotBeMovedToAnotherYearOrCreateAnAncestryCycle()
    {var w=World();Assert.Throws<ArgumentException>(()=>w.Governance.RecordCountryBirth(2,1,50,true));w.Governance.RecordCountryBirth(2,1,51,true);Assert.Throws<ArgumentException>(()=>w.Governance.RecordCountryBirth(1,2,0,true));Assert.Single(w.Governance.CountryBirths);}
    [Fact]public void RepeatedObservationOfTheSameCountryBirthIsIdempotent()
    {var w=World();w.Governance.RecordCountryBirth(2,1,51,true);w.Governance.RecordCountryBirth(2,1,51,true);Assert.Single(w.Governance.CountryBirths);Assert.Throws<ArgumentException>(()=>w.Governance.RecordCountryBirth(2,1,51,false));}
    [Fact]public void EconomicInterestUsesRecordedIncomeNotTreasury()
    {var w=World();w.Governance.RecordIncome(1,50,20);w.Governance.RecordIncome(2,50,80);var issue=CivicForeignInterests.Question(w,w.GetKingdom(1),w.GetKingdom(2),false);Assert.InRange(issue.StrategicInterest,0.119f,0.121f);}
    [Fact]public void UnrecognizedCountryCannotSignPeacefulTreatiesOrNegotiateOpenBorders()
    {
        var w=World();for(int id=3;id<=4;id++)w.Governance.Recognition.RegisterCountry(id,0);w.Governance.Recognition.RegisterCountry(5,51);w.AddKingdom(5,IdeologyType.Monarchy,51);
        foreach(TreatyType treaty in Enum.GetValues(typeof(TreatyType)))Assert.False(w.TrySignTreaty(1,5,treaty,false,out _));
        Assert.False(w.TryNegotiateBorderOpening(1,5,true,false,true));Assert.Equal(0,w.TreatiesBook.Count);
        Assert.True(w.Governance.Recognition.CanContact(1,5,InternationalContact.War));Assert.True(w.Governance.Recognition.CanContact(1,5,InternationalContact.Peace));
    }
    [Fact]public void ElectedOfficeWithoutItsIndividualElectionCannotBeRestored()
    {var w=World();w.Governance.OfficeFor(1).Commit(101,51,5,"nonexistent-election");string broken=SaveData.Write(w);Assert.Throws<FormatException>(()=>SaveData.Apply(w,broken));Assert.Equal(2,w.Kingdoms.Count);}
    [Fact]public void OversizedAllianceAndDirectWarCanTurnAnOtherwiseYesVoteIntoNo()
    {
        var person=new CivicBelief{Ideology=IdeologyType.Monarchy,Conviction=0};
        var ordinary=new CivicIssueContext{CurrentGovernment=IdeologyType.Monarchy,ProposedGovernment=IdeologyType.Monarchy,
            AllianceQuestion=true,AllianceShareAfter=0.20f};
        var oversized=new CivicIssueContext{CurrentGovernment=IdeologyType.Monarchy,ProposedGovernment=IdeologyType.Monarchy,
            AllianceQuestion=true,AllianceShareAfter=0.85f};
        var enemy=new CivicIssueContext{CurrentGovernment=IdeologyType.Monarchy,ProposedGovernment=IdeologyType.Monarchy,
            AllianceQuestion=true,AllianceShareAfter=0.20f,DirectWar=true};
        Assert.Equal(CivicBallot.Yes,CivicPreferences.Decide(person,ordinary));
        Assert.Equal(CivicBallot.No,CivicPreferences.Decide(person,oversized));
        Assert.Equal(CivicBallot.No,CivicPreferences.Decide(person,enemy));
    }
    [Fact]public void NationalPositionCannotDisguiseCountryVotesAsIndividualCitizens()
    {var ballot=new CivicBallot("fake-national",BallotElectorKind.Country,51,1,new[]{1,2,3},new[]{1,2});Assert.Throws<ArgumentException>(()=>new CivicProcedure(ballot,CivicProcedureKind.RecognitionPosition,1,targetCountryId:5));}

}
