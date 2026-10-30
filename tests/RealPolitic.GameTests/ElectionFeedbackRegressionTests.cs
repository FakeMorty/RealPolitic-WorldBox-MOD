using System;
using System.Collections.Generic;
using System.Reflection;
using RealPolitic.Core;
using Xunit;
namespace RealPolitic.Tests;

public sealed class ElectionFeedbackRegressionTests : IDisposable
{
    private readonly PoliticalWorld world = new PoliticalWorld();
    private readonly Kingdom kingdom;
    private readonly Actor a, b;
    private readonly KingdomPolitics state;
    private readonly WorldFeeder feeder;
    public ElectionFeedbackRegressionTests()
    {
        a = new Actor { id = 100, age = 40, data = new ActorData { id = 100, name = "Марао" } };
        b = new Actor { id = 200, age = 35, data = new ActorData { id = 200, name = "Поми" } };
        var city = new City { units = new List<Actor> { a, b } };
        kingdom = new Kingdom { id = 1, king = a, cities = new List<City> { city }, units = city.units };
        city.kingdom = kingdom; a.kingdom = kingdom; b.kingdom = kingdom;
        MapBox.instance = new MapBox { cities = new CityManager { list = kingdom.cities }, kingdoms = new KingdomManager { list = new List<Kingdom> { kingdom } } };
        world.SetYear(95);
        state = world.AddKingdom(1, IdeologyType.MerchantRepublic, 0);
        state.Ruler = Ruler(a);
        state.Election = new ElectionState { NextElectionYear = 110, TermsServed = 1 };
        state.Election.RecordPresidentTerm(100, "Марао", 1);
        feeder = new WorldFeeder(world);
    }
    private RulerRef Ruler(Actor actor) => new RulerRef { ActorId = (int)actor.data.id, KingdomId = 1, Age = actor.age, Name = actor.data.name };
    private void Apply(int id)
    {
        var evt = new PolEvent(PolEventType.DemocraticElection, "test") { KingdomId = 1, ActorId = id, Year = 110 };
        typeof(WorldFeeder).GetMethod("TryEntruoneElect", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(feeder, new object[] { kingdom, evt });
    }
    [Fact]
    public void ReelectingIncumbent_DoesNotReplaceHim()
    { Apply(100); Assert.Same(a, kingdom.king); Assert.Null(kingdom.cities[0].leader); }
    [Fact]
    public void ObservedAppointment_IsIdempotent_Over40Years()
    { for (int i = 0; i < 40; i++) { Apply(100); Assert.Same(a, kingdom.king); } }
    [Fact]
    public void WinnerLookup_UsesManagerList_NotManagerEnumerable()
    { Assert.Same(b, GameRefs.FindActorById(200)); }
    [Fact]
    public void MissingWinner_DoesNotInstallRandomPerson()
    { Apply(999); Assert.Same(a, kingdom.king); }
    [Fact]
    public void ElectionInstallsExactWinner_AndAcknowledgesSync()
    {
        state.Ruler = Ruler(b);
        Apply(200);
        Assert.Same(b, kingdom.king);
        var last = (Dictionary<int,int>)typeof(PoliticalWorld).GetField("_lastRuler", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(world);
        Assert.Equal(200, last[1]);
        Apply(200);
        Assert.Same(b, kingdom.king);
        Assert.Same(a, kingdom.cities[0].leader);
    }
    [Fact]
    public void TermHistory_CannotBeResetByReform()
    { state.Election.RecordPresidentTerm(100, "Марао", 3); state.Election.RecordPresidentTerm(100, "Марао", 1); Assert.Equal(3, state.Election.FindPastPresident(100).TermsCount); }
    [Fact]
    public void ExhaustedCandidate_IsNotRecycledAsNew()
    {
        state.Election.RecordPresidentTerm(200, "Поми", 3);
        ElectionSystem.CandidateResolver = (_,__) => new ElectionSystem.ElectionCandidate { ActorId = 200, Age = 35 };
        Assert.Equal(0, ElectionSystem.ResolveCandidate(state, 100, new Random(1)).ActorId);
        Assert.Equal(3, state.Election.FindPastPresident(200).TermsCount);
    }
    [Fact]
    public void LiveGameWithoutCandidate_DoesNotInventWinner()
    {
        ElectionSystem.CandidateResolver = (_,__) => null;
        state.Election.TermsServed = 3;
        state.Election.RecordPresidentTerm(100, "Марао", 3);
        Assert.False(ElectionSystem.HoldElection(state, 110, world.Bus, new Random(1)));
        Assert.Equal(100, state.Ruler.ActorId);
        Assert.Equal(111, state.Election.NextElectionYear);
    }
    private sealed class WinRandom : Random { public override double NextDouble() => 0; }
    [Fact]
    public void Reelection_DoesNotCreateSecondElectionAtNextSync()
    {
        // Mirrors game bridge: a successful event must update the succession baseline.
        world.SetLastRuler(1, state.Ruler);
        world.Bus.Subscribe(evt => { if (evt.Type == PolEventType.DemocraticElection) Apply(evt.ActorId.Value); });
        Assert.True(ElectionSystem.HoldElection(state, 110, world.Bus, new WinRandom()));
        Assert.Same(a, kingdom.king);
        Assert.Equal(125, state.Election.NextElectionYear);
        Assert.Equal(2, state.Election.TermsServed);
    }
    public void Dispose()
    {
        MapBox.instance = null;
        ElectionSystem.CandidateResolver = null;
        ElectionSystem.CandidateValidator = null;
    }
}
