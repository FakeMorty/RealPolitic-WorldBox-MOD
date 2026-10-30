using System;
using System.Collections.Generic;
using Xunit;
namespace RealPolitic.Tests;

public class CrashSafetyRegressionTests
{
    private sealed class Calls
    {
        public int Count;
        public void Change() { Count++; }
        public object NullResult() { Count++; return null; }
        public void Fail() { Count++; throw new InvalidOperationException("test failure"); }
        public void Overloaded() { }
        public void Overloaded(int value) { }
    }
    private sealed class TrackingKingdom : Kingdom
    {
        public bool FromLoad;
        public new void setKing(Actor actor, bool fromLoad)
        { FromLoad = fromLoad; king = actor; }
    }
    [Fact]
    public void VoidMutation_IsSuccess_AndRunsOnce()
    {
        var calls = new Calls();
        Assert.True(GameRefs.TryInvoke(calls, "Change", out var result));
        Assert.Null(result);
        Assert.Equal(1, calls.Count);
    }
    [Fact]
    public void NullReturn_IsNotMissingMethod()
    {
        var calls = new Calls();
        Assert.True(GameRefs.TryInvoke(calls, "NullResult", out _));
        Assert.False(GameRefs.TryInvoke(calls, "Missing", out _));
        Assert.Equal(1, calls.Count);
    }
    [Fact]
    public void ThrowingMutation_IsNotRetried()
    {
        var calls = new Calls();
        Assert.False(GameRefs.TryInvoke(calls, "Fail", out _));
        Assert.Contains("test failure", GameRefs.LastInvokeError);
        Assert.Equal(1, calls.Count);
    }
    [Fact]
    public void HasMember_HandlesOverloads()
    { Assert.True(GameRefs.HasMember(typeof(Calls), "Overloaded")); }
    [Fact]
    public void FormerRuler_LeadsOnlyOneCity()
    {
        var a = new Actor(); var c1 = new City(); var c2 = new City();
        var k = new Kingdom { cities = new List<City> { c1, c2 } };
        Assert.True(GameRefs.TryAppointToCity(k, a));
        Assert.Same(a, c1.leader);
        Assert.Null(c2.leader);
    }
    [Fact]
    public void ReigningKing_IsNotMadeCityLeader()
    {
        var a = new Actor(); var c = new City();
        var k = new Kingdom { king = a, cities = new List<City> { c } };
        Assert.False(GameRefs.TryAppointToCity(k, a));
        Assert.Null(c.leader);
    }
    [Fact]
    public void RuntimeElection_DoesNotUseSaveLoadingMode()
    {
        var k = new TrackingKingdom(); var a = new Actor();
        Assert.True(GameRefs.TrySetKingOnKingdom(k, a, quiet: true));
        Assert.False(k.FromLoad);
    }
    [Fact]
    public void NoCities_IsNotSuccessfulAppointment()
    { Assert.False(GameRefs.TryAppointToCity(new Kingdom { cities = new List<City>() }, new Actor())); }
}
