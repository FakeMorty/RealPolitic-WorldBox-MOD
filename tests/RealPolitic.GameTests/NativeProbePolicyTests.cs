using RealPolitic.NativeDiagnostics;
using Xunit;

namespace RealPolitic.Tests;
public sealed class NativeProbePolicyTests
{
    [Theory]
    [InlineData("Kingdom","clearBuildingList",false)]
    [InlineData("Kingdom","clearListUnits",false)]
    [InlineData("Kingdom","clearListCities",false)]
    [InlineData("Kingdom","Dispose",true)]
    [InlineData("Kingdom","allianceLeave",true)]
    [InlineData("Alliance","join",true)]
    [InlineData("Alliance","leave",true)]
    [InlineData("AllianceManager","newAlliance",true)]
    [InlineData("MapBox","generateNewMap",true)]
    [InlineData("SaveManager","loadWorld",true)]
    [InlineData("SaveManager","loadData",true)]
    [InlineData("SaveManager","startLoadSlot",true)]
    [InlineData("SaveManager","saveWorldToDirectory",true)]
    [InlineData("SaveManager","clickSaveSlot",true)]
    public void OnlyRelevantLifecycleAndMembershipCallsAreObserved(string type,string method,bool expected)
    {Assert.Equal(expected,NativeProbePolicy.Observe(type,method));}
    [Theory]
    [InlineData("C:/Users/user/AppData/LocalLow/mkarpenko/WorldBox/saves/save21/","save21")]
    [InlineData("C:\\Users\\user\\saves\\save21\\","save21")]
    [InlineData("/home/user/saves/save21","save21")]
    [InlineData("","")]
    public void SlotLabelExtractsFolderNameEvenWithTrailingSeparators(string path,string expected)
    {Assert.Equal(expected,NativeProbePolicy.SlotLabel(path));}
    [Fact]public void LifecycleHasADedicatedBudgetNotSharedWithKingdomHousekeeping()
    {Assert.True(NativeProbePolicy.Lifecycle("SaveManager"));Assert.True(NativeProbePolicy.Lifecycle("MapBox"));Assert.False(NativeProbePolicy.Lifecycle("Kingdom"));}
}
