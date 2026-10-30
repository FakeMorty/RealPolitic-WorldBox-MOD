using System;using System.Collections.Generic;using System.Linq;using System.Reflection;using RealPolitic.Core;using Xunit;
namespace RealPolitic.Tests;
public class Audit500RegressionTests : IDisposable
{
    public class NativeAlliance {public long id;public List<Kingdom> kingdoms_list=new List<Kingdom>();}
    public class NativeManager {public List<NativeAlliance> list=new List<NativeAlliance>();public bool Result;public bool NoMutation;public int Calls;public bool forceAlliance(Kingdom a,Kingdom b){Calls++;if(Result&&!NoMutation){var n=list.FirstOrDefault(x=>x.kingdoms_list.Contains(a)||x.kingdoms_list.Contains(b));if(n==null){n=new NativeAlliance{id=77};list.Add(n);}if(!n.kingdoms_list.Contains(a))n.kingdoms_list.Add(a);if(!n.kingdoms_list.Contains(b))n.kingdoms_list.Add(b);}return Result;}}
    public class NamedNativeAlliance:NativeAlliance {public string name;public Kingdom leader;}
    [Fact]public void FeederNativeIdReachesTheSavedBookAndRenameDoesNotInventHistory()
    {
        var native=new NamedNativeAlliance{id=81,name="Северный союз",leader=a,kingdoms_list=new List<Kingdom>{a,b}};manager.list.Add(native);
        var world=new PoliticalWorld();world.SetAllianceSnapshot((List<AllianceRecord>)Snapshot());native.name="Северный договор";
        world.SetAllianceSnapshot((List<AllianceRecord>)Snapshot());Assert.Empty(world.Bus.OfType(PolEventType.AllianceFormed));Assert.Empty(world.Bus.OfType(PolEventType.AllianceBroken));
        var copy=new PoliticalWorld();SaveData.Apply(copy,SaveData.Write(world));Assert.Equal(81,copy.AlliancesBook.All.Single().Id);
        copy.SetAllianceSnapshot((List<AllianceRecord>)Snapshot());Assert.Empty(copy.Bus.OfType(PolEventType.AllianceFormed));
    }
    public class Laws {public bool Enabled;public bool isEnabled(string key)=>Enabled;}
    private readonly NativeManager manager=new NativeManager();
    private readonly Kingdom a=new Kingdom{id=1,name="A"},b=new Kingdom{id=2,name="B"};
    public Audit500RegressionTests(){MapBox.instance=new MapBox{alliances=manager};}
    public void Dispose(){MapBox.instance=null;}
    private object Snapshot()=>typeof(WorldFeeder).GetMethod("ReadAllianceRecords",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(new WorldFeeder(new PoliticalWorld()),null);
    [Fact]public void UnavailableSnapshotIsNotAnEmptyRegistry(){MapBox.instance.alliances=null;Assert.Null(Snapshot());}
    [Fact]public void KnownEmptySnapshotIsValid()=>Assert.Empty((List<AllianceRecord>)Snapshot());
    [Fact]public void UnknownNativeIdDoesNotPublishPartialSnapshot(){manager.list.Add(new NativeAlliance{id=0});Assert.Null(Snapshot());}
    public class MissingMembers {public long id=55;}
    public class UnreadableManager {public List<object> list=new List<object>{new MissingMembers()};}
    [Fact]public void UnknownMembersDoNotPublishFalseDepartures(){MapBox.instance.alliances=new UnreadableManager();Assert.Null(Snapshot());}
    private bool Offer()=>DiplomacyExecutor.Execute(new DiplomaticMove{Action=DiplomaticAction.ProposeAlliance},a,b);
    [Fact]public void BoxedFalseIsNotSuccess(){manager.Result=false;Assert.False(Offer());Assert.Equal(1,manager.Calls);}
    [Fact]public void TrueWithoutMembershipIsNotSuccess(){manager.Result=true;manager.NoMutation=true;Assert.False(Offer());}
    [Fact]public void TrueIsSuccess(){manager.Result=true;Assert.True(Offer());}
    [Fact]public void ExistingSharedAllianceIsNotForcedAgain(){manager.Result=true;manager.list.Add(new NativeAlliance{kingdoms_list=new List<Kingdom>{a,b}});Assert.False(Offer());Assert.Equal(0,manager.Calls);}
    [Fact]public void DifferentAlliancesAreNotForcedTogether(){manager.Result=true;manager.list.Add(new NativeAlliance{kingdoms_list=new List<Kingdom>{a}});manager.list.Add(new NativeAlliance{kingdoms_list=new List<Kingdom>{b}});Assert.False(Offer());Assert.Equal(0,manager.Calls);}
    [Fact]public void UnalliedMemberCanJoinKnownAlliance(){manager.Result=true;manager.list.Add(new NativeAlliance{kingdoms_list=new List<Kingdom>{a}});Assert.True(Offer());}
    [Fact]public void DuplicateMembershipBlocksFurtherForce(){manager.Result=true;manager.list.Add(new NativeAlliance{kingdoms_list=new List<Kingdom>{a}});manager.list.Add(new NativeAlliance{kingdoms_list=new List<Kingdom>{a}});Assert.False(Offer());Assert.Equal(0,manager.Calls);}
    [Fact]public void UnknownMembershipIsNotGuessed(){MapBox.instance.alliances=new object();Assert.False(AllianceSafety.CanJoin(a,b));}
    [Fact]public void SameAllianceIsVisibleWithoutLegacyRelationApi(){var n=new NativeAlliance{kingdoms_list=new List<Kingdom>{a,b}};manager.list.Add(n);Assert.True(AllianceSafety.Same(a,b));}
    [Fact]public void NativeIdsAreNotListPositions(){var x=new NativeAlliance{id=48};var y=new NativeAlliance{id=96};manager.list.Add(x);manager.list.Add(y);manager.list.Remove(x);Assert.Equal(96,AllianceSafety.StableId(manager.list[0]));}
    [Fact]public void MissingIdIsNotInvented()=>Assert.Equal(-1,AllianceSafety.StableId(new object()));
    [Fact]public void WorldLawOffIsRespected(){MapBox.instance.world_laws=new Laws{Enabled=false};Assert.False(GameRefs.IsDiplomacyWorldLawEnabled());}
    [Fact]public void WorldLawOnIsRespected(){MapBox.instance.world_laws=new Laws{Enabled=true};Assert.True(GameRefs.IsDiplomacyWorldLawEnabled());}
    private class Resident:Actor {public bool Alive=true;public bool isAlive()=>Alive;}
    [Fact]public void AnimalSingletonIsNotLastCitizen()
    {
        var world=new PoliticalWorld();var resident=new Resident{id=12,data=new ActorData{id=12,name="Краб"}};var animals=new Kingdom{id=999,name="crab",units=new List<Actor>{resident}};resident.kingdom=animals;
        var tracker=new GameDeaths(world);tracker.OnActorDeath(resident,"old_age",null);resident.Alive=false;tracker.ConfirmActorDeath(resident,12,animals);
        Assert.Empty(world.Bus.OfType(PolEventType.LastResidentDied));Assert.Null(world.Deaths.Get(12));
    }
    [Fact]public void TrackedCivilSingletonStillWorks()
    {
        var world=new PoliticalWorld();world.AddKingdom(1,IdeologyType.Theocracy,0);var resident=new Resident{id=12,data=new ActorData{id=12,name="Последний"}};a.units=new List<Actor>{resident};resident.kingdom=a;
        var tracker=new GameDeaths(world);tracker.OnActorDeath(resident,"old_age",null);resident.Alive=false;tracker.ConfirmActorDeath(resident,12,a);Assert.Single(world.Bus.OfType(PolEventType.LastResidentDied));
    }
    [Fact]public void LastResidentDeathIsConfirmedEvenWhenBoatsRemainInKingdomUnits()
    {
        var world=new PoliticalWorld();world.AddKingdom(1,IdeologyType.Monarchy,0);
        var resident=new Resident{id=12,data=new ActorData{id=12,name="Последний житель"}};
        var boat1=new Resident{id=91,data=new ActorData{id=91,name="boat_trading_human"}};
        var boat2=new Resident{id=92,data=new ActorData{id=92,name="boat_transport_human"}};
        a.units=new List<Actor>{boat1,resident,boat2};resident.kingdom=a;boat1.kingdom=a;boat2.kingdom=a;
        var tracker=new GameDeaths(world);tracker.OnActorDeath(resident,"old_age",null);resident.Alive=false;tracker.ConfirmActorDeath(resident,12,a);
        Assert.Single(world.Bus.OfType(PolEventType.LastResidentDied));
    }
}
public class Audit500ChronicleTests
{
    private static PolEvent Border(int year=3,string guest="B")=>new PolEvent(PolEventType.BorderExpulsion,""){Year=year,KingdomId=1}.With("kingdom","A").With("partner",guest).With("intruder","Гость");
    [Fact]public void DigestCountsReportsNotUniquePeople(){var d=new ChronicleBorderDigest();d.Add(Border());d.Add(Border());string line=Assert.Single(d.Drain());Assert.Contains("— 2",line);Assert.Contains("не уникальных людей",line);Assert.Equal(-1,d.Year);}
    [Fact]public void DifferentGuestsRemainSeparate(){var d=new ChronicleBorderDigest();d.Add(Border(3,"B"));d.Add(Border(3,"C"));Assert.Equal(2,d.Drain().Count);}
    [Fact]public void CannotMixYears(){var d=new ChronicleBorderDigest();Assert.True(d.Add(Border(3)));Assert.False(d.Add(Border(4)));Assert.Single(d.Drain());Assert.True(d.Add(Border(4)));}
    [Fact]public void FinishAndReloadDrainExactlyOnce()
    {
        string folder=System.IO.Path.Combine(System.IO.Path.GetTempPath(),"audit500-"+Guid.NewGuid());
        try{var w=new PoliticalWorld();var c=new Chronicle(w,null,folder);Chronicle.WriteBorderEpisodes=true;w.Bus.Publish(Border());c.BeginObservation();c.Finish();c.Finish();string path=(string)typeof(Chronicle).GetField("_path",BindingFlags.Instance|BindingFlags.NonPublic).GetValue(c);Assert.Single(System.IO.File.ReadAllLines(path),l=>l.Contains("[ГРАНИЦЫ: ИТОГИ]"));}finally{Chronicle.WriteBorderEpisodes=false;if(System.IO.Directory.Exists(folder))System.IO.Directory.Delete(folder,true);}
    }
    [Fact]public void SecessionDoesNotCountTheSameCrisisTwice()
    {
        string folder=System.IO.Path.Combine(System.IO.Path.GetTempPath(),"audit500-"+Guid.NewGuid());
        try{var w=new PoliticalWorld();var c=new Chronicle(w,null,folder);w.Bus.Publish(new PolEvent(PolEventType.IdeologyThreatBroke,"t"){Year=3}.With("kingdom","A"));w.Bus.Publish(new PolEvent(PolEventType.CitySeceded,"t"){Year=3}.With("kingdom","A").With("city","X"));Assert.Equal(1,(int)typeof(Chronicle).GetField("_threatsBroke",BindingFlags.Instance|BindingFlags.NonPublic).GetValue(c));}finally{if(System.IO.Directory.Exists(folder))System.IO.Directory.Delete(folder,true);}
    }
}
