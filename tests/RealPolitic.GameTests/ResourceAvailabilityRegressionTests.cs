using System.Collections.Generic;
using RealPolitic.Core;
using Xunit;

namespace RealPolitic.Tests;
public sealed class ResourceAvailabilityRegressionTests
{
    private sealed class GoldCity:City {public int Amount;public int getResourcesAmount(string key)=>Amount;}
    private sealed class FoodCity:City {public int Amount;public int getTotalFood()=>Amount;}
    private sealed class FloatCity:City {public float Amount;public float getResourcesAmount(string key)=>Amount;}
    private sealed class FoodKingdom:Kingdom {public int Amount;public int countTotalFood()=>Amount;}
    private static Kingdom K(params City[] cities)=>new Kingdom{cities=new List<City>(cities)};
    [Fact]public void MissingResourceApiIsUnknownNotAnObservedZero()
    {Assert.False(GameRefs.TryKingdomResource(K(new City()),"gold",out var value));Assert.Equal(0,value);}
    [Fact]public void ARealZeroStockRemainsAValidObservation()
    {Assert.True(GameRefs.TryKingdomResource(K(new GoldCity{Amount=0}),"gold",out var value));Assert.Equal(0,value);}
    [Fact]public void OneUnreadableCityDoesNotProduceAPartialKnownGoldTotal()
    {Assert.False(GameRefs.TryKingdomResource(K(new GoldCity{Amount=50},new City()),"gold",out var value));Assert.Equal(0,value);}
    [Fact]public void OneUnreadableCityDoesNotProduceAPartialKnownFoodTotal()
    {Assert.False(GameRefs.TryKingdomResource(K(new FoodCity{Amount=50},new City()),"food",out var value));Assert.Equal(0,value);}
    [Fact]public void CompleteCityFoodTotalsIncludeGenuineZeros()
    {Assert.True(GameRefs.TryKingdomResource(K(new FoodCity{Amount=0},new FoodCity{Amount=25}),"food",out var value));Assert.Equal(25,value);}
    [Fact]public void NativeKingdomFoodTotalCanReportAGenuineZero()
    {var k=new FoodKingdom{Amount=0,cities=new List<City>{new City()}};Assert.True(GameRefs.TryKingdomResource(k,"food",out var value));Assert.Equal(0,value);}
    [Theory][InlineData(float.NaN)][InlineData(float.PositiveInfinity)][InlineData(-1f)]
    public void InvalidResourceReadIsUnknownAndNeverLeaksIntoTheSnapshot(float amount)
    {Assert.False(GameRefs.TryKingdomResource(K(new FloatCity{Amount=amount}),"gold",out var value));Assert.Equal(0,value);}
    [Fact]public void NegativeNativeFoodTotalIsNotAValidObservation()
    {var k=new FoodKingdom{Amount=-1,cities=new List<City>{new City()}};Assert.False(GameRefs.TryKingdomResource(k,"food",out var value));Assert.Equal(0,value);}
    [Fact]public void NullCityEntryDoesNotProduceAPartialKnownTotal()
    {Assert.False(GameRefs.TryKingdomResource(K(new GoldCity{Amount=20},null),"gold",out var value));Assert.Equal(0,value);}
    [Fact]public void AConfirmedEmptyCityRegistryHasZeroResources()
    {Assert.True(GameRefs.TryKingdomResource(K(),"gold",out var value));Assert.Equal(0,value);}
    [Fact]public void FeederMarksFallbackAsUnknownAndDoesNotReplaceARealZero()
    {
        var method=typeof(WorldFeeder).GetMethod("ResourceOrSafeFloor",System.Reflection.BindingFlags.Static|System.Reflection.BindingFlags.NonPublic);
        object[] missing={K(new City()),"gold",100f,null};Assert.Equal(100f,(float)method.Invoke(null,missing));Assert.False((bool)missing[3]);
        object[] zero={K(new GoldCity{Amount=0}),"gold",100f,null};Assert.Equal(0f,(float)method.Invoke(null,zero));Assert.True((bool)zero[3]);
    }

}
