using System;
using System.Collections.Generic;
using System.Linq;
using RealPolitic.Core;
using Xunit;
namespace RealPolitic.Tests;

public class ChronicleEdition25Tests
{
    private static DeathRecord Last(int year = 111) => new DeathRecord { ActorId = 9, ActorName = "Стеттаович", KingdomId = 7, KingdomNameAtDeath = "Кашу", PopulationBeforeDeath = 1, Year = year, Cause = DeathCause.OldAge };
    [Fact] public void LastResident_RequiresConfirmedDeath() { var c = new ChronicleCausality(); Assert.False(c.Confirm(Last(), false, 0)); Assert.Null(c.Take(7,112)); }
    [Fact] public void LastResident_RequiresZeroSurvivors() { var c = new ChronicleCausality(); Assert.False(c.Confirm(Last(), true, 1)); }
    [Fact] public void UnknownPopulation_IsNotExtinction() { var c = new ChronicleCausality(); Assert.False(c.Confirm(Last(), true, -1)); }
    [Fact] public void MoreThanOneBefore_IsNotLastResident() { var c = new ChronicleCausality(); var d = Last(); d.PopulationBeforeDeath=2; Assert.False(c.Confirm(d,true,0)); }
    [Fact] public void ConfirmedChain_CanBeConsumedOnlyOnce() { var c=new ChronicleCausality(); var d=Last(); Assert.True(c.Confirm(d,true,0)); Assert.Same(d,c.Take(7,112)); Assert.Null(c.Take(7,112)); }
    [Fact] public void DuplicateConfirmation_IsIgnored() { var c=new ChronicleCausality(); var d=Last(); Assert.True(c.Confirm(d,true,0)); Assert.False(c.Confirm(d,true,0)); Assert.Equal(1,c.Count); }
    [Fact] public void NewResidents_InvalidateOldDeath() { var c=new ChronicleCausality();var d=Last();c.Confirm(d,true,0);c.ObservePopulation(7,2);Assert.Null(c.Take(7,112));Assert.False(d.LastResidentEvidenceActive); }
    [Fact] public void UnknownPopulation_DoesNotEraseEvidence() { var c=new ChronicleCausality();var d=Last();c.Confirm(d,true,0);c.ObservePopulation(7,-1);Assert.Same(d,c.Take(7,112)); }
    [Fact] public void AncientDeath_IsNotAttachedToLaterCollapse() { var c=new ChronicleCausality();c.Confirm(Last(),true,0);Assert.Null(c.Take(7,125)); }
    [Fact] public void FutureDeath_IsNotAttachedToPastEvent() { var c=new ChronicleCausality();c.Confirm(Last(),true,0);Assert.Null(c.Take(7,110)); }
    [Fact] public void Evidence_IsKingdomSpecific() { var c=new ChronicleCausality();c.Confirm(Last(),true,0);Assert.Null(c.Take(8,112));Assert.NotNull(c.Take(7,112)); }
    [Fact] public void WorldReset_DropsEvidence() { var world=new PoliticalWorld();world.ChronicleCauses.Confirm(Last(),true,0);world.Clear();Assert.Equal(0,world.ChronicleCauses.Count); }
    [Fact] public void SaveRoundTrip_PreservesEvidence_WithoutReplayingDeaths()
    {
        var world=new PoliticalWorld();world.AddKingdom(7,IdeologyType.Theocracy,0);world.SetYear(111);
        var d=world.Deaths.Record(9,"Стеттаович",DeathCause.OldAge,111,kingdomId:7,wasRuler:true);
        d.PopulationBeforeDeath=1;d.KingdomNameAtDeath="Кашу";world.ChronicleCauses.Confirm(d,true,0);
        string json=SaveData.Write(world);
        var restored=new PoliticalWorld();int replayed=0;restored.Deaths.OnDeath += _ => replayed++;
        SaveData.Apply(restored,json);
        Assert.Equal(0,replayed);Assert.Equal("Кашу",restored.Deaths.Get(9).KingdomNameAtDeath);
        restored.SetYear(112);restored.RemoveKingdom(7);
        Assert.NotNull(Assert.Single(restored.Bus.OfType(PolEventType.KingdomDisappeared)).Death);
    }
    [Fact] public void InvalidatedEvidence_DoesNotReturnAfterLoad()
    {
        var world=new PoliticalWorld();world.AddKingdom(7,IdeologyType.Theocracy,0);
        var d=world.Deaths.Record(9,"Старый",DeathCause.OldAge,111,kingdomId:7);d.PopulationBeforeDeath=1;
        world.ChronicleCauses.Confirm(d,true,0);world.ChronicleCauses.ObservePopulation(7,1);
        var restored=new PoliticalWorld();SaveData.Apply(restored,SaveData.Write(world));Assert.Equal(0,restored.ChronicleCauses.Count);
    }
    [Fact] public void OldFormat16_LoadsWithoutInventingLastResidentEvidence()
    {
        const string old = "{\"format\":16,\"year\":112,\"kingdoms\":[],\"deaths\":[{\"id\":9,\"n\":\"Стеттаович\",\"c\":0,\"y\":111,\"king\":7,\"r\":true,\"fem\":false}]}";
        var world = new PoliticalWorld();SaveData.Apply(world, old);
        Assert.NotNull(world.Deaths.Get(9));Assert.False(world.Deaths.Get(9).WasLastResident);
        Assert.Equal(-1,world.Deaths.Get(9).PopulationBeforeDeath);Assert.Equal(0,world.ChronicleCauses.Count);
    }
    [Fact] public void ConfirmedDeathDoesNotEndStillExistingState()
    {
        var world=new PoliticalWorld();world.AddKingdom(7,IdeologyType.Theocracy,0);
        Assert.True(world.ChronicleCauses.Confirm(Last(),true,0));
        Assert.NotNull(world.GetKingdom(7));Assert.Empty(world.Bus.OfType(PolEventType.KingdomDisappeared));
    }
    private static ChronicleSnapshot Snap(int year=1,int pop=10,int ruler=1) => new ChronicleSnapshot { KingdomId=1,Year=year,Population=pop,RulerId=ruler,RulerName="Имя",Legitimacy=50,Cities=1,Ideology=IdeologyType.Monarchy };
    [Fact] public void FirstReview_HasNoImaginaryPriorHistory() { Assert.Empty(new ChronicleNotebook().Compare(Snap())); }
    [Fact] public void Delta_ShowsPopulationChange() { var n=new ChronicleNotebook();n.Compare(Snap());Assert.Contains("10 → 7 (-3)",n.Compare(Snap(2,7))); }
    [Fact] public void RenameOfSameActor_IsNotNewRuler() { var n=new ChronicleNotebook();n.Compare(Snap());var s=Snap(2);s.RulerName="Другое";Assert.Empty(n.Compare(s)); }
    [Fact] public void NamesakeWithDifferentId_IsDifferentRuler() { var n=new ChronicleNotebook();n.Compare(Snap());Assert.Contains("№2",n.Compare(Snap(2,10,2))); }
    [Fact] public void ClockRollback_DoesNotCompareFutureToPast() { var n=new ChronicleNotebook();n.Compare(Snap(100));Assert.Empty(n.Compare(Snap(1,1))); }
    [Fact] public void SameYearRepeatedReview_DoesNotDuplicateDelta() { var n=new ChronicleNotebook();n.Compare(Snap());Assert.Empty(n.Compare(Snap(1,7))); }
    [Fact] public void Farewell_LabelsOldSnapshotAsOld() { var n=new ChronicleNotebook();n.Compare(Snap(110,1));Assert.Contains("прошлого обзора",n.Farewell(1));Assert.Empty(n.Farewell(1));Assert.Equal(0,n.Count); }
    [Fact] public void Notebook_PrunesMissingKingdoms() { var n=new ChronicleNotebook();n.Compare(Snap());n.Retain(new HashSet<int>());Assert.Equal(0,n.Count); }
    [Fact] public void Explanations_AreNotRepeatedEveryYear() { var n=new ChronicleNotebook();var s=new KingdomPolitics { Economy=new EconomyState() };var p=new SimParams();Assert.NotEmpty(n.Conditions(Snap(),s,p));Assert.Empty(n.Conditions(Snap(2),s,p));Assert.NotEmpty(n.Conditions(Snap(11),s,p)); }
    [Fact] public void ChangedDangerLevel_IsExplainedImmediately() { var n=new ChronicleNotebook();var p=new SimParams();var s=new KingdomPolitics();n.Conditions(Snap(),s,p);var now=Snap(2);now.Legitimacy=1;Assert.Contains("критически",n.Conditions(now,s,p)); }
    [Fact] public void SmallCommunity_IsExplained_NotDeleted() { var n=new ChronicleNotebook();var s=Snap(1,1);Assert.Contains("формальные",n.Conditions(s,new KingdomPolitics(),new SimParams()));Assert.Equal(1,s.Population); }
    [Fact] public void UnknownName_IsExplicit_NotBlank() { Assert.Contains("имя не установлено",ChronicleNotebook.Person(" ",3)); }
    [Fact] public void Vacancy_IsNotADeadPerson() { Assert.Equal("главы нет",ChronicleNotebook.Person("",0)); }
    [Fact] public void Digest_GroupsSameYearAndRace() { var d=new ChronicleExtinctionDigest();d.Add(1,"Краб","Краб I");d.Add(1,"Краб","Краб II");var line=Assert.Single(d.Drain());Assert.Contains("Краб I",line);Assert.Contains("Краб II",line);Assert.Contains("2",line); }
    [Fact] public void Digest_DoesNotMixYears() { var d=new ChronicleExtinctionDigest();d.Add(1,"Краб","I");Assert.False(d.Add(2,"Краб","II"));d.Drain();Assert.True(d.Add(2,"Краб","II")); }
    [Fact] public void Digest_KeepsSpeciesSeparate() { var d=new ChronicleExtinctionDigest();d.Add(1,"Краб","I");d.Add(1,"Бабочка","II");Assert.Equal(2,d.Drain().Count); }
    [Fact] public void Digest_MemoryIsBounded_WithOverflowFallback() { var d=new ChronicleExtinctionDigest();for(int i=0;i<128;i++)Assert.True(d.Add(1,"вид"+i,"ветвь"));Assert.False(d.Add(1,"новый","ветвь"));Assert.Equal(128,d.Drain().Count); }
    [Fact] public void Digest_OnlyFourExamplesPlusCount() { var d=new ChronicleExtinctionDigest();for(int i=0;i<100;i++)d.Add(1,"Краб","Подвид"+i);var line=Assert.Single(d.Drain());Assert.Contains("ещё 96",line);Assert.DoesNotContain("Подвид99",line); }
    [Fact] public void Digest_DoesNotInventCause() { var d=new ChronicleExtinctionDigest();d.Add(1,"Краб","I");Assert.Contains("не установленная причина",Assert.Single(d.Drain())); }
}
