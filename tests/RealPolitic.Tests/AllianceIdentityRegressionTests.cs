using System;
using System.Collections.Generic;
using System.Linq;
using RealPolitic.Core;
using Xunit;

namespace RealPolitic.Tests;

public sealed class AllianceIdentityRegressionTests
{
    private static AllianceRecord A(int id,string name,params int[] members)
    {var a=new AllianceRecord{Id=id,Name=name,LeaderId=members.Length>0?members[0]:0};a.Members.AddRange(members);return a;}
    private static PoliticalWorld W(params AllianceRecord[] records)
    {var w=new PoliticalWorld();for(int i=1;i<=6;i++)w.AddKingdom(i,IdeologyType.Monarchy,0);w.SetAllianceSnapshot(records.ToList());w.ClearEvents();return w;}
    [Fact]public void RenamingTheSameNativeAllianceDoesNotInventFormationOrDissolution()
    {
        var w=W(A(81,"Старое имя",1,2));w.SetAllianceSnapshot(new List<AllianceRecord>{A(81,"Новое имя",1,2)});
        Assert.Empty(w.Bus.OfType(PolEventType.AllianceFormed));Assert.Empty(w.Bus.OfType(PolEventType.AllianceBroken));
        Assert.Equal("Новое имя",w.AlliancesBook.All.Single().Name);
    }
    [Fact]public void RenamingDoesNotHideAnActualMembershipChange()
    {
        var w=W(A(81,"Старое имя",1,2));w.SetAllianceSnapshot(new List<AllianceRecord>{A(81,"Новое имя",1,2,3)});
        Assert.Equal(3,Assert.Single(w.Bus.OfType(PolEventType.AllianceMemberJoined)).KingdomId);
        Assert.Empty(w.Bus.OfType(PolEventType.AllianceFormed));Assert.Empty(w.Bus.OfType(PolEventType.AllianceBroken));
    }
    [Fact]public void ReusedNameWithADifferentNativeIdIsNotTheOldAlliance()
    {
        var w=W(A(81,"Союз",1,2));w.SetAllianceSnapshot(new List<AllianceRecord>{A(99,"Союз",1,2)});
        Assert.Single(w.Bus.OfType(PolEventType.AllianceFormed));Assert.Single(w.Bus.OfType(PolEventType.AllianceBroken));
        Assert.Empty(w.Bus.OfType(PolEventType.AllianceMemberJoined));
    }
    [Fact]public void TwoAlliancesWithTheSameNameDoNotShareMemberHistory()
    {
        var w=W(A(81,"Союз",1,2),A(99,"Союз",3,4));
        w.SetAllianceSnapshot(new List<AllianceRecord>{A(99,"Союз",3,4),A(81,"Союз",1,2,5)});
        Assert.Equal(5,Assert.Single(w.Bus.OfType(PolEventType.AllianceMemberJoined)).KingdomId);
        Assert.Empty(w.Bus.OfType(PolEventType.AllianceMemberLeft));
    }
    [Fact]public void NativeIdsSurviveSaveReloadAndRename()
    {
        var w=W(A(81,"Союз",1,2));var copy=new PoliticalWorld();SaveData.Apply(copy,SaveData.Write(w));
        Assert.Equal(81,copy.AlliancesBook.All.Single().Id);copy.SetAllianceSnapshot(new List<AllianceRecord>{A(81,"Другое имя",1,2)});
        Assert.Empty(copy.Bus.OfType(PolEventType.AllianceFormed));Assert.Empty(copy.Bus.OfType(PolEventType.AllianceBroken));
    }
    [Fact]public void LegacyBookWithoutIdsIsMigratedSilentlyRatherThanAssignedOrdinalIds()
    {
        var w=W(A(81,"Старое имя",1,2));string legacy=SaveData.Write(w).Replace("\"id\":81,","");
        var copy=new PoliticalWorld();SaveData.Apply(copy,legacy);Assert.Equal(0,copy.AlliancesBook.All.Single().Id);
        copy.SetAllianceSnapshot(new List<AllianceRecord>{A(81,"Новое имя",1,2)});
        Assert.Empty(copy.Bus.OfType(PolEventType.AllianceFormed));Assert.Empty(copy.Bus.OfType(PolEventType.AllianceBroken));
        Assert.Equal(81,copy.AlliancesBook.All.Single().Id);
        var again=new PoliticalWorld();SaveData.Apply(again,SaveData.Write(copy));Assert.Equal(81,again.AlliancesBook.All.Single().Id);
    }
    [Fact]public void AmbiguousLegacyNamesAreNotGuessedDuringMigration()
    {
        var w=W(A(81,"Союз",1,2),A(99,"Союз",3,4));
        string legacy=SaveData.Write(w).Replace("\"id\":81,","").Replace("\"id\":99,","");
        var copy=new PoliticalWorld();SaveData.Apply(copy,legacy);
        copy.SetAllianceSnapshot(new List<AllianceRecord>{A(99,"Западный союз",3,4),A(81,"Восточный союз",1,2)});
        Assert.Empty(copy.Bus.OfType(PolEventType.AllianceFormed));Assert.Empty(copy.Bus.OfType(PolEventType.AllianceBroken));
        Assert.Equal(new[]{99,81},copy.AlliancesBook.All.Select(a=>a.Id));
    }
    [Theory][InlineData(0)][InlineData(81)]
    public void AnUnidentifiedOrDuplicateNativeIdDefersTheWholeSnapshot(int secondId)
    {
        var w=W(A(81,"Союз",1,2));w.SetAllianceSnapshot(new List<AllianceRecord>{A(81,"Союз",1,2),A(secondId,"Другой союз",3,4)});
        Assert.Single(w.AlliancesBook.All);Assert.Empty(w.Bus.OfType(PolEventType.AllianceFormed));
    }
    [Theory][InlineData("0")][InlineData("-1")][InlineData("1.5")][InlineData("2147483648")]
    public void InvalidPersistedNativeIdIsRejectedBeforeErasingTheCurrentWorld(string id)
    {
        var w=W(A(81,"Союз",1,2));string invalid=SaveData.Write(w).Replace("\"id\":81,","\"id\":"+id+",");
        Assert.Throws<FormatException>(()=>SaveData.Apply(w,invalid));Assert.Equal(81,w.AlliancesBook.All.Single().Id);Assert.NotNull(w.GetKingdom(1));
    }
    [Fact]public void DuplicatePersistedNativeIdIsRejectedBeforeErasingTheCurrentWorld()
    {
        var w=W(A(81,"Союз",1,2),A(99,"Другой союз",3,4));string invalid=SaveData.Write(w).Replace("\"id\":99,","\"id\":81,");
        Assert.Throws<FormatException>(()=>SaveData.Apply(w,invalid));Assert.Equal(2,w.AlliancesBook.Count);
    }
}
