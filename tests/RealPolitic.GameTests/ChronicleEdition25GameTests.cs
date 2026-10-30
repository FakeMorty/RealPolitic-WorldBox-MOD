using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.Json;
using System.Text.RegularExpressions;
using RealPolitic.Core;
using Xunit;
namespace RealPolitic.Tests;

public sealed class ChronicleEdition25GameTests : IDisposable
{
    private sealed class Resident : Actor { public bool Alive=true; public bool isAlive() => Alive; }
    private readonly PoliticalWorld world = new PoliticalWorld();
    private readonly Kingdom kingdom;
    private readonly Resident resident;
    private readonly GameDeaths tracker;
    private readonly List<string> folders=new List<string>();
    public ChronicleEdition25GameTests()
    {
        resident=new Resident { id=9,name="Стеттаович",age=127,data=new ActorData { id=9,name="Стеттаович" } };
        kingdom=new Kingdom { id=7,name="Кашу",king=resident,units=new List<Actor>{resident},cities=new List<City>() };
        resident.kingdom=kingdom;
        MapBox.instance=new MapBox { kingdoms=new KingdomManager { list=new List<Kingdom>{kingdom} },cities=new CityManager { list=new List<City>() } };
        world.AddKingdom(7,IdeologyType.Theocracy,0);world.SetYear(1);tracker=new GameDeaths(world);
        Chronicle.ReadableEdition=true;Chronicle.GroupExtinctions=true;
    }
    private Chronicle NewChronicle()
    {
        string folder=Path.Combine(Path.GetTempPath(),"chronicle25-"+Guid.NewGuid());folders.Add(folder);
        return new Chronicle(world,null,folder);
    }
    private static List<string> Buffer(Chronicle c) => (List<string>)typeof(Chronicle).GetField("_buffer",BindingFlags.Instance|BindingFlags.NonPublic).GetValue(c);
    private void Die() { tracker.OnActorDeath(resident,"old_age",null);resident.Alive=false;tracker.ConfirmActorDeath(resident,9,kingdom); }
    [Fact] public void LastResident_IsConfirmedAfterDeath_NotBefore()
    {
        tracker.OnActorDeath(resident,"old_age",null);Assert.Empty(world.Bus.OfType(PolEventType.LastResidentDied));
        resident.Alive=false;tracker.ConfirmActorDeath(resident,9,kingdom);Assert.Single(world.Bus.OfType(PolEventType.LastResidentDied));
    }
    [Fact] public void CancelledDeath_DoesNotCreateExtinctionEvidence() { tracker.OnActorDeath(resident,"old_age",null);tracker.ConfirmActorDeath(resident,9,kingdom);Assert.Equal(0,world.ChronicleCauses.Count); }
    [Fact] public void AnotherLivingResident_PreventsLastResidentClaim() { kingdom.units.Add(new Resident { data=new ActorData { id=10 } });Die();Assert.Empty(world.Bus.OfType(PolEventType.LastResidentDied)); }
    [Fact] public void ArrivingResidentBetweenPrefixAndPostfix_PreventsClaim() { tracker.OnActorDeath(resident,"old_age",null);resident.Alive=false;kingdom.units.Add(new Resident());tracker.ConfirmActorDeath(resident,9,kingdom);Assert.Empty(world.Bus.OfType(PolEventType.LastResidentDied)); }
    [Fact] public void ConfirmedCommoner_IsNotLostWhenCommonerTrackingIsOff() { kingdom.king=null;Die();Assert.NotNull(world.Deaths.Get(9));Assert.Single(world.Bus.OfType(PolEventType.LastResidentDied)); }
    [Fact] public void DuplicatePostfix_DoesNotDuplicateLastResidentEvent() { Die();tracker.ConfirmActorDeath(resident,9,kingdom);Assert.Single(world.Bus.OfType(PolEventType.LastResidentDied)); }
    [Fact] public void DeathMetadata_SurvivesDisposedActorData() { tracker.OnActorDeath(resident,"old_age",null);resident.data=null;resident.Alive=false;tracker.ConfirmActorDeath(resident,9,kingdom);Assert.Equal("Стеттаович",Assert.Single(world.Bus.OfType(PolEventType.LastResidentDied)).Get("resident")); }
    [Fact] public void Chronicle_ExplainsLastDeathAndStateEnd()
    {
        var c=NewChronicle();Die();world.RemoveKingdom(7);
        Assert.Contains(Buffer(c),line=>line.Contains("ПОСЛЕДНИЙ ЖИТЕЛЬ")&&line.Contains("Стеттаович"));
        Assert.Contains(Buffer(c),line=>line.Contains("КОНЕЦ ДЕРЖАВЫ")&&line.Contains("после смерти последнего жителя"));
    }
    [Fact] public void UnexplainedDisappearance_RemainsUnexplained() { var c=NewChronicle();world.RemoveKingdom(7);Assert.Contains(Buffer(c),line=>line.Contains("Подтверждённой причины")||line.Contains("Подтверждённой причины".ToLowerInvariant()));Assert.DoesNotContain(Buffer(c),line=>line.Contains("после смерти последнего жителя")); }
    [Fact] public void ReelectionText_DistinguishesSamePerson()
    {
        var evt=new PolEvent(PolEventType.DemocraticElection,"test"){ActorId=9}.With("ruler","Марао").With("election_kind","renewed").With("next_election",125);
        var line=ChronicleEdition.Explain(evt,"Выборы.",world);Assert.Contains("остаётся тем же",line);Assert.Contains("125",line);Assert.Contains("№9",line);
    }
    [Fact] public void ObservedSuccession_DoesNotPretendThereWasBallot()
    { var evt=new PolEvent(PolEventType.DemocraticElection,"test").With("election_kind","observed");Assert.Contains("не доказательство",ChronicleEdition.Explain(evt,"Смена.",world)); }
    [Fact] public void Succession_UsesRecordedPredecessorDeath()
    {
        world.Deaths.Record(1,"Старый",DeathCause.OldAge,1);
        var evt=new PolEvent(PolEventType.Succession,"test"){Year=2}.With("old_actor",1);
        Assert.Contains("скончался",ChronicleEdition.Explain(evt,"Смена.",world));
    }
    [Fact] public void SuccessionWithoutDeath_DoesNotInventOne()
    { var evt=new PolEvent(PolEventType.Succession,"test"){Year=2}.With("old_actor",1);Assert.Contains("не установлена",ChronicleEdition.Explain(evt,"Смена.",world)); }
    [Fact] public void ExtinctionGrouping_ProducesOneLineAtReview()
    {
        var c=NewChronicle();world.Bus.Publish(new PolEvent(PolEventType.SubspeciesExtinct,"test"){Year=1}.With("race","Краб").With("species","I"));
        world.Bus.Publish(new PolEvent(PolEventType.SubspeciesExtinct,"test"){Year=1}.With("race","Краб").With("species","II"));
        c.WriteYearSummary(1);Assert.Single(Buffer(c),line=>line.Contains("ПРИРОДА: ИТОГИ"));
    }
    [Fact] public void UngroupedOption_KeepsIndividualExtinctionLines()
    {
        Chronicle.GroupExtinctions=false;var c=NewChronicle();
        world.Bus.Publish(new PolEvent(PolEventType.SubspeciesExtinct,"test"){Year=1}.With("race","Краб").With("species","I"));
        Assert.Contains(Buffer(c),line=>line.Contains("ВЫМИРАНИЕ"));
    }
    [Fact] public void DeadActorStillInKingField_IsNotPrintedAsLivingRuler()
    { resident.Alive=false;Assert.Null(ChronicleEdition.LivingRuler(kingdom)); }
    [Fact] public void UnknownLifeApi_DoesNotInventDeath()
    {
        var actor=new Actor { id=21,data=new ActorData { id=21 } };kingdom.king=actor;kingdom.units=new List<Actor>{actor};actor.kingdom=kingdom;
        tracker.OnActorDeath(actor,"old_age",null);tracker.ConfirmActorDeath(actor,21,kingdom);
        Assert.Empty(world.Bus.OfType(PolEventType.LastResidentDied));
    }
    [Fact] public void NormalExit_WritesPendingSameYearExtinctions()
    {
        var c=NewChronicle();world.Bus.Publish(new PolEvent(PolEventType.SubspeciesExtinct,"test"){Year=1}.With("race","Краб").With("species","I"));
        c.Finish();string path=(string)typeof(Chronicle).GetField("_path",BindingFlags.Instance|BindingFlags.NonPublic).GetValue(c);
        Assert.Contains("ПРИРОДА: ИТОГИ",File.ReadAllText(path));Assert.Empty(Buffer(c));
    }
    [Fact] public void ReloadSession_DoesNotCompareAgainstOldSession()
    {
        var c=NewChronicle();c.WriteYearSummary(1);c.BeginObservation();c.WriteYearSummary(2);
        Assert.DoesNotContain(Buffer(c),line=>line.Contains("С прошлого обзора"));
    }
    [Fact] public void RealDeathHookState_CarriesIdentityIntoPostfix()
    {
        var previous=RealPoliticMod.Deaths;RealPoliticMod.Deaths=tracker;
        try
        {
            var prefix=typeof(Patches).GetMethod("DeathPrefix",BindingFlags.Static|BindingFlags.NonPublic);
            var postfix=typeof(Patches).GetMethod("DeathPostfix",BindingFlags.Static|BindingFlags.NonPublic);
            object[] arguments={resident,new object[]{false,"old_age"},null};prefix.Invoke(null,arguments);
            Assert.NotNull(arguments[2]);resident.Alive=false;resident.data=null;postfix.Invoke(null,new object[]{arguments[2]});
            Assert.Single(world.Bus.OfType(PolEventType.LastResidentDied));
        }
        finally { RealPoliticMod.Deaths=previous; }
    }
    [Fact] public void MissingCandidate_IsExplainedWithoutIssuingMandate()
    {
        ElectionSystem.CandidateResolver = (_,__) => null;
        ElectionSystem.CandidateValidator = null;
        try
        {
            var state=world.GetKingdom(7);state.Ruler=new RulerRef {ActorId=9,Name="Стеттаович"};
            state.Election.TermsServed=3;state.Election.RecordPresidentTerm(9,"Стеттаович",3);
            var c=NewChronicle();Assert.False(ElectionSystem.HoldElection(state,1,world.Bus,new Random(1)));
            Assert.Single(world.Bus.OfType(PolEventType.ElectionDeferred));
            Assert.Contains(Buffer(c),line=>line.Contains("Новый мандат не выдан"));
            Assert.Equal(3,state.Election.TermsServed);
        }
        finally { ElectionSystem.CandidateResolver=null;ElectionSystem.CandidateValidator=null; }
    }
    [Theory][InlineData("ru")][InlineData("en")]
    public void AllEditionKeysAndPlaceholders_AreTranslated(string language)
    {
        DirectoryInfo root=new DirectoryInfo(AppContext.BaseDirectory);while(root!=null&&!File.Exists(Path.Combine(root.FullName,"mod.json")))root=root.Parent;
        Assert.NotNull(root);var locale=JsonDocument.Parse(File.ReadAllText(Path.Combine(root.FullName,"Locales",language+".json")));
        foreach(string path in Directory.EnumerateFiles(Path.Combine(root.FullName,"src"),"*.cs",SearchOption.AllDirectories))
            foreach(Match match in Regex.Matches(File.ReadAllText(path),"\"(RP_CHRON25_[A-Z_]+)\"\\s*,\\s*\"([^\"]*)\""))
            {
                string key=match.Groups[1].Value;Assert.True(locale.RootElement.TryGetProperty(key,out var value),key);
                var expected=Regex.Matches(match.Groups[2].Value,"\\{[^}]+\\}").Select(m=>m.Value).OrderBy(x=>x).ToArray();
                var actual=Regex.Matches(value.GetString(),"\\{[^}]+\\}").Select(m=>m.Value).OrderBy(x=>x).ToArray();Assert.Equal(expected,actual);
            }
    }
    public void Dispose()
    {
        MapBox.instance=null;Chronicle.ReadableEdition=true;Chronicle.GroupExtinctions=true;
        foreach(string folder in folders)if(Directory.Exists(folder))Directory.Delete(folder,true);
    }

    private sealed class FakeBoatAsset { public string id = "boat_transport_human"; public bool civ = false; public bool is_boat = true; }
    private sealed class BoatResident : Actor { public object asset = new FakeBoatAsset(); }

    [Fact]
    public void BoatKiller_RecordsForeignKingdomAndPortInChronicleAndSuccession()
    {
        var germany = new Kingdom { id = 40, name = "Германия", units = new List<Actor>(), cities = new List<City>() };
        var sweden = new Kingdom { id = 41, name = "Швеция", units = new List<Actor>(), cities = new List<City>() };
        var normCity = new City { id = 400, name = "Норкорвенфорд", kingdom = germany };
        var winsted = new City { id = 410, name = "Винстед", kingdom = sweden };
        var consul = new Resident { id = 5834, name = "Сильвия", age = 68, kingdom = germany, city = normCity, data = new ActorData { id = 5834, name = "Сильвия" } };
        germany.king = consul;
        germany.units.Add(consul);

        var boat = new BoatResident
        {
            id = 9001,
            name = "«Винстед»",
            kingdom = sweden,
            city = winsted,
            data = new ActorData { id = 9001, name = "«Винстед»" }
        };
        sweden.units.Add(boat);

        world.AddKingdom(40, IdeologyType.Aristocracy, 400);
        world.AddKingdom(41, IdeologyType.Tribalism, 410);
        world.SetYear(420);

        var c = NewChronicle();
        tracker.OnActorDeath(consul, "explosion", boat);

        DeathRecord rec = world.Deaths.Get(5834);
        Assert.NotNull(rec);
        Assert.Equal("Швеция", rec.KillerKingdom);
        Assert.Equal("Винстед", rec.KillerCity);
        Assert.Contains(Buffer(c), line => line.Contains("[СМЕРТЬ ПРАВИТЕЛЯ]")
            && line.Contains("держава: Швеция [флот чужой державы]")
            && line.Contains("порт: Винстед"));

        var succEvt = new PolEvent(PolEventType.Succession, "test") { Year = rec.Year + 1, ActorId = 6337 }
            .With("old_actor", 5834).With("ruler", "Кастин");
        string explained = ChronicleEdition.Explain(succEvt, "Смена.", world);
        Assert.Contains("держава: Швеция [флот чужой державы]", explained);
    }

    [Fact]
    public void GameWars_CoalitionWarWithDestroyedDefender_IsRecordedAsAttackerVictoryNotWhitePeace()
    {
        GameWars.Reset();
        var czechia = new Kingdom { id = 10, name = "Чехия", cities = new List<City> { new City { id = 101, name = "Марента" } } };
        var poland = new Kingdom { id = 20, name = "Польша", cities = new List<City> { new City { id = 201, name = "Миренск" } } };
        var portugal = new Kingdom { id = 21, name = "Португалия", cities = new List<City> { new City { id = 211, name = "Соколмир" } } };
        var italy = new Kingdom { id = 22, name = "Италия", cities = new List<City> { new City { id = 221, name = "Элдтарвинтон" } } };

        MapBox.instance = new MapBox
        {
            kingdoms = new KingdomManager { list = new List<Kingdom> { czechia, poland, portugal, italy } },
            cities = new CityManager { list = new List<City>() }
        };
        world.AddKingdom(10, IdeologyType.Militarism, 101);
        world.AddKingdom(20, IdeologyType.Aristocracy, 201);
        world.AddKingdom(21, IdeologyType.Monarchy, 211);
        world.AddKingdom(22, IdeologyType.Monarchy, 221);
        world.SetAllianceSnapshot(new List<AllianceRecord>
        {
            new AllianceRecord { Id = 1, Name = "Португало-Итальянский союз", Members = { 20, 21, 22 } }
        });
        world.SetYear(412);

        RealPoliticMod.World = world;
        var war = new War { id = 777, data = new WarData { id = 777 } };
        var c = NewChronicle();

        GameWars.OnWarStarted(czechia, poland, new WarTypeAsset { name_template = "war_conquest" }, war);

        // В ходе войны Польша уничтожена (0 городов и удалена из списка живых держав),
        // а движок завершает войну с уцелевшими союзниками номинальным WarWinner.Nobody.
        poland.cities.Clear();
        MapBox.instance.kingdoms.list.Remove(poland);
        world.RemoveKingdom(20);
        world.SetYear(416);

        GameWars.OnWarEnded(war, WarWinner.Nobody);

        var warStartEvt = Assert.Single(world.Bus.OfType(PolEventType.WarStarted));
        Assert.Equal("Чехия", warStartEvt.Get("attacker_side"));
        Assert.Equal("Польша, Португалия, Италия", warStartEvt.Get("defender_side"));
        Assert.Contains(Buffer(c), line => line.Contains("Началась война: Чехия → Польша, Португалия, Италия"));

        var warEndEvt = Assert.Single(world.Bus.OfType(PolEventType.WarEnded));
        Assert.Equal("Чехия", warEndEvt.Get("attacker"));
        Assert.Equal("Польша", warEndEvt.Get("defender"));
        Assert.Equal("Польша, Португалия, Италия", warEndEvt.Get("defender_side"));
        Assert.Equal("RP_WARWINNER_ATTACKERS", warEndEvt.Get("winner"));
        Assert.Contains("у защитников 1 из 3 [Польша]", warEndEvt.Get("casualties"));
        Assert.Contains(Buffer(c), line => line.Contains("Война: Чехия против Польша, Португалия, Италия — победили атакующие")
            && line.Contains("уничтожено стран: у нападающих 0 из 1, у защитников 1 из 3 [Польша]"));
    }

    [Fact]
    public void GameNotifier_DropsStaleLaggingNotificationsFrom20YearsAgo()
    {
        var bus = new PolEventBus();
        var center = new NotificationCenter(bus) { MinSeverity = Severity.Minor };
        var notifier = new GameNotifier(bus, center) { Interval = 0f, MaxLagYears = 2 };

        // Накопилось старое событие 20-летней давности и свежее событие текущего года
        bus.Publish(new PolEvent(PolEventType.WarStarted, "RP_EV_WAR_STARTED")
        {
            Year = 400,
            Severity = Severity.Major
        }.With("attacker", "СтараяВойна"));
        bus.Publish(new PolEvent(PolEventType.AllianceFormed, "RP_EV_ALLIANCE_FORMED")
        {
            Year = 420,
            Severity = Severity.Major
        }.With("name", "СвежийСоюз"));

        MethodInfo dequeueFresh = typeof(GameNotifier).GetMethod("DequeueFresh", BindingFlags.Instance | BindingFlags.NonPublic);
        PolEvent fresh = (PolEvent)dequeueFresh.Invoke(notifier, new object[] { 420 });
        Assert.NotNull(fresh);
        Assert.Equal(PolEventType.AllianceFormed, fresh.Type);
        Assert.Equal(420, fresh.Year);
        Assert.Equal(0, center.PendingCount);
    }
}
