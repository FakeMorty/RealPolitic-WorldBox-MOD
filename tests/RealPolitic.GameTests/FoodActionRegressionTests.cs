using System.Collections.Generic;
using RealPolitic.Core;
using Xunit;
namespace RealPolitic.Tests;
public class FoodActionRegressionTests
{
    private sealed class StockpileCity : City
    {
        public int Bread;
        public void addResourcesToRandomStockpile(string resource, int amount) { if (resource == "bread") Bread += amount; }
    }
    [Fact]
    public void MissingResourceApi_IsNotReportedAsSuccess()
    {
        var k = new Kingdom { cities = new List<City> { new City() } };
        Assert.False(IdeologyMechanicsEffects.TryApplyStrategicAction(k, new KingdomPolitics(), KingdomActionType.EmergencyFood, 100, out _));
    }
    [Fact]
    public void WorkingVoidResourceApi_IsSuccess_AndRunsOnce()
    {
        var c = new StockpileCity();
        var k = new Kingdom { cities = new List<City> { c } };
        Assert.True(IdeologyMechanicsEffects.TryApplyStrategicAction(k, new KingdomPolitics(), KingdomActionType.EmergencyFood, 100, out _));
        Assert.Equal(8, c.Bread);
    }
    private sealed class HungerStatus {public int hungry=5;}
    private sealed class MeasuredCity:City
    {
        public int Bread=3,Calls;
        public HungerStatus status=new HungerStatus();
        public int getResourcesAmount(string resource)=>Bread;
        public int getTotalFood()=>Bread;
        // The meaning of an actual native int return is not assumed by diagnostics.
        public int addResourcesToRandomStockpile(string resource,int amount){Calls++;Bread+=amount;return 0;}
    }
    private sealed class ThrowingReaderCity:City
    {
        public int Bread,Calls,Reads;
        public int getResourcesAmount(string resource){Reads++;throw new System.InvalidOperationException("read unavailable");}
        public int getTotalFood(){Reads++;throw new System.InvalidOperationException("read unavailable");}
        public void addResourcesToRandomStockpile(string resource,int amount){Calls++;Bread+=amount;}
    }
    [Fact]public void ObservationShowsStockChangeWithoutClaimingThatCitizensHaveEaten()
    {
        var c=new MeasuredCity{id=4};var k=new Kingdom{id=1,cities=new List<City>{c}};var observation=EmergencyFoodObservation.Capture(k);
        c.addResourcesToRandomStockpile("bread",8);string text=observation.DescribeAfter(100,true);
        Assert.Contains("bread=3->11",text);Assert.Contains("hungry_status=5->5",text);Assert.Contains("consumption=not-measured",text);
        Assert.Contains("bread_delta_observed=8",text);Assert.Equal(1,c.Calls);
    }
    [Fact]public void MissingReadersAreUnknownNotAZeroMeasuredStockChange()
    {
        var k=new Kingdom{cities=new List<City>{new City()}};string text=EmergencyFoodObservation.Capture(k).DescribeAfter(100,false);
        Assert.Contains("bread=unknown->unknown",text);Assert.Contains("bread_delta_observed=unknown",text);Assert.Contains("paired_cities=0/1",text);
    }
    [Fact]public void DiagnosticCityDetailsAreBoundedButAllPairedCitiesContribute()
    {
        var cities=new List<City>();for(int i=1;i<=10;i++)cities.Add(new MeasuredCity{id=i});var k=new Kingdom{cities=cities};
        var observation=EmergencyFoodObservation.Capture(k);foreach(MeasuredCity c in cities)c.addResourcesToRandomStockpile("bread",8);
        string text=observation.DescribeAfter(100,true);
        Assert.Contains("omitted_details=2",text);Assert.Contains("bread_delta_observed=80",text);Assert.Contains("paired_cities=10/10",text);
        Assert.DoesNotContain(" | city=9 ",text);Assert.DoesNotContain(" | city=10 ",text);
    }
    [Fact]public void RepeatedObservationNeverAddsOrRemovesResources()
    {
        var c=new MeasuredCity();var k=new Kingdom{cities=new List<City>{c}};var observation=EmergencyFoodObservation.Capture(k);
        observation.DescribeAfter(100,false);observation.DescribeAfter(100,false);Assert.Equal(3,c.Bread);Assert.Equal(0,c.Calls);
    }
    [Fact]public void ThrowingDiagnosticReadersCannotPreventOrRepeatTheFoodRequest()
    {
        var c=new ThrowingReaderCity();var k=new Kingdom{cities=new List<City>{c}};
        Assert.True(IdeologyMechanicsEffects.TryApplyStrategicAction(k,new KingdomPolitics(),KingdomActionType.EmergencyFood,100,out _));
        Assert.Equal(8,c.Bread);Assert.Equal(1,c.Calls);Assert.True(c.Reads>=4);
    }
    [Fact]public void FoodResultDescribesARequestNotVerifiedHungerRecovery()
    {
        var c=new StockpileCity();var k=new Kingdom{cities=new List<City>{c}};
        Assert.True(IdeologyMechanicsEffects.TryApplyStrategicAction(k,new KingdomPolitics(),KingdomActionType.EmergencyFood,100,out var result));
        Assert.Contains("запрос",result.ToLowerInvariant());Assert.DoesNotContain("пополнены",result.ToLowerInvariant());Assert.Equal(8,c.Bread);
    }
    private sealed class StarvingCounterCity:City
    {
        public int Food=397,Pop=142,Starving=0;
        public HungerStatus status=new HungerStatus{hungry=6};
        public int getTotalFood()=>Food;
        public int getPopulationPeople()=>Pop;
        public int countStarving()=>Starving;
    }
    [Fact]public void AbundantFoodWithRoutineMealtimeHungerIsNotReportedAsCityStarvation()
    {
        var city=new StarvingCounterCity{Food=397,Pop=142,Starving=0,status=new HungerStatus{hungry=6}};
        Assert.False(GameRefs.CityStarving(city));
    }
    [Fact]public void GenuineStarvingUnitsOrDepletedGranariesReportCityStarvation()
    {
        var depleted=new StarvingCounterCity{Food=12,Pop=142,Starving=4,status=new HungerStatus{hungry=55}};
        Assert.True(GameRefs.CityStarving(depleted));
        var massStarving=new StarvingCounterCity{Food=200,Pop=100,Starving=25,status=new HungerStatus{hungry=40}};
        Assert.True(GameRefs.CityStarving(massStarving));
    }

}
