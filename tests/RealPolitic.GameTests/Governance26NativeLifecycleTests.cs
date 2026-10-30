using System;
using System.IO;
using System.Reflection;
using RealPolitic.Core;
using Xunit;

namespace RealPolitic.Tests;
public sealed class Governance26NativeLifecycleTests:IDisposable
{
    private readonly string previous=RealPoliticMod.ModFolder;
    private readonly string folder=Path.Combine(Path.GetTempPath(),"rp26-native-"+Guid.NewGuid());
    private readonly PoliticalWorld world=new PoliticalWorld();
    private readonly MapBox map=new MapBox();
    private bool loading;
    private readonly byte[] native={1,3,5,7,9};
    public Governance26NativeLifecycleTests()
    {
        Directory.CreateDirectory(folder);RealPoliticMod.ModFolder=folder;NativeWorldLifecycle.ResetForTests();
        NativeWorldLifecycle.LoadingReader=()=>loading;world.BeginNewWorldGovernance("old-world");world.SetYear(507);world.AddKingdom(7,IdeologyType.Monarchy,1);
    }
    public void Dispose(){NativeWorldLifecycle.ResetForTests();RealPoliticMod.ModFolder=previous;Directory.Delete(folder,true);}
    private string Slot(string name,string json,byte[] bytes=null)
    {
        var path=Path.Combine(folder,name);Directory.CreateDirectory(path);File.WriteAllBytes(Path.Combine(path,"map.wbox"),bytes??native);
        File.WriteAllText(Path.Combine(path,"realpolitic_state.json"),json);NativeCheckpointBinding.Record(path,json);return path;
    }
    private void Loaded(){loading=true;Assert.False(NativeWorldLifecycle.TryComplete(map,world));loading=false;}
    [Fact]public void AgeAndMapReferenceAloneNeverCreateANewRegime()
    {world.Clear();world.SetYear(1);Assert.True(NativeWorldLifecycle.TryComplete(map,world));Assert.False(world.Governance.Enabled);Assert.Equal(0,NativeWorldLifecycle.Revision);}
    [Fact]public void TrustedCreationWaitsForActualLoadingAndUsesANewIdentity()
    {
        NativeWorldLifecycle.BeginCreation(true);Assert.False(NativeWorldLifecycle.TryComplete(map,world));Assert.Equal("old-world",world.Governance.WorldKey);
        Loaded();Assert.True(NativeWorldLifecycle.TryComplete(map,world));Assert.True(world.Governance.Enabled);
        Assert.NotEqual("old-world",world.Governance.WorldKey);Assert.Empty(world.Kingdoms);Assert.Equal(1,NativeWorldLifecycle.Revision);
    }
    [Fact]public void SameMapObjectCanRepresentTwoSeparateNewWorlds()
    {
        NativeWorldLifecycle.BeginCreation(true);Loaded();NativeWorldLifecycle.TryComplete(map,world);string first=world.Governance.WorldKey;
        NativeWorldLifecycle.BeginCreation(true);Loaded();NativeWorldLifecycle.TryComplete(map,world);
        Assert.NotEqual(first,world.Governance.WorldKey);Assert.Equal(2,NativeWorldLifecycle.Revision);
    }
    [Fact]public void AFailedCapabilityNeverEnablesTheNewMode()
    {world.Clear();NativeWorldLifecycle.BeginCreation(false);Loaded();Assert.False(NativeWorldLifecycle.TryComplete(map,world));Assert.False(world.Governance.Enabled);Assert.Contains("capability",NativeWorldLifecycle.Status);}
    [Fact]public void UnknownLoadingStateDoesNotEraseTheCurrentWorld()
    {NativeWorldLifecycle.BeginCreation(true);NativeWorldLifecycle.LoadingReader=()=>null;Assert.False(NativeWorldLifecycle.TryComplete(map,world));Assert.NotNull(world.GetKingdom(7));}
    [Fact]public void ExactSelectedCheckpointRestoresRatherThanReinitializesTheWorld()
    {
        string json=SaveData.Write(world),slot=Slot("save1",json);world.Clear();NativeWorldLifecycle.BeginLoad(native,slot);
        Loaded();Assert.True(NativeWorldLifecycle.TryComplete(map,world));Assert.Equal("old-world",world.Governance.WorldKey);Assert.NotNull(world.GetKingdom(7));Assert.Equal(507,world.Year);
    }
    [Fact]public void ASelectedLegacySlotDoesNotImportTheGlobalOrIndexedNewWorld()
    {
        Slot("new",SaveData.Write(world));string legacy=Path.Combine(folder,"legacy");Directory.CreateDirectory(legacy);
        NativeWorldLifecycle.BeginLoad(native,legacy);Loaded();Assert.True(NativeWorldLifecycle.TryComplete(map,world));Assert.False(world.Governance.Enabled);
    }
    [Fact]public void ALoadCancelsAPendingCreationTicket()
    {
        NativeWorldLifecycle.BeginCreation(true);NativeWorldLifecycle.BeginLoad(new byte[]{99},null);Loaded();
        Assert.True(NativeWorldLifecycle.TryComplete(map,world));Assert.False(world.Governance.Enabled);
    }
    [Fact]public void GenerationHelperInsideALoadCannotEnableAnOldSave()
    {
        NativeWorldLifecycle.BeginLoad(new byte[]{99},null);
        typeof(NativeWorldLifecycle).GetMethod("GeneratePrefix",BindingFlags.NonPublic|BindingFlags.Static).Invoke(null,null);
        Loaded();Assert.True(NativeWorldLifecycle.TryComplete(map,world));Assert.False(world.Governance.Enabled);
    }
    [Fact]public void NoPathByteLoadUsesOnlyAnExactUniqueNativeCheckpoint()
    {
        Slot("save1",SaveData.Write(world));world.Clear();NativeWorldLifecycle.BeginLoad(native,null);Loaded();
        Assert.True(NativeWorldLifecycle.TryComplete(map,world));Assert.Equal("old-world",world.Governance.WorldKey);
    }
    [Fact]public void NativeBytesFromAnotherCheckpointNeverImportTheLatestPoliticalSnapshot()
    {Slot("save1",SaveData.Write(world));Assert.Null(NativeCheckpointBinding.Read(new byte[]{9,9},null,out var error));Assert.Null(error);}
    [Fact]public void IdenticalNativeBytesWithDifferentPoliticsAreAmbiguousNotLatestWins()
    {
        string first=SaveData.Write(world);string old=Slot("old",first);world.SetYear(508);string newer=Slot("new",SaveData.Write(world));
        Assert.Null(NativeCheckpointBinding.Read(native,null,out var error));Assert.Contains("ambiguous",error);
        Assert.Equal(first,NativeCheckpointBinding.Read(native,old,out error));Assert.Null(error);
        Assert.Contains("\"year\":508",NativeCheckpointBinding.Read(native,newer,out error));Assert.Null(error);
    }
    [Fact]public void AnAmbiguousNoPathLoadHoldsSimulationInsteadOfImportingTheWrongSave()
    {
        Slot("one",SaveData.Write(world));world.SetYear(508);Slot("two",SaveData.Write(world));NativeWorldLifecycle.BeginLoad(native,null);Loaded();
        Assert.False(NativeWorldLifecycle.TryComplete(map,world));Assert.Equal(508,world.Year);Assert.True(NativeWorldLifecycle.Transitioning);
    }
    [Fact]public void AChangedSidecarCannotMatchTheNativeBinding()
    {
        var slot=Slot("one",SaveData.Write(world));File.AppendAllText(Path.Combine(slot,"realpolitic_state.json")," ");
        Assert.Null(NativeCheckpointBinding.Read(native,slot,out var error));Assert.Contains("political-checkpoint-mismatch",error);
    }
    [Fact]public void AChangedNativeFileCannotBeMistakenForTheSameWorldCheckpoint()
    {var slot=Slot("one",SaveData.Write(world));Assert.Null(NativeCheckpointBinding.Read(new byte[]{4},slot,out var error));Assert.Contains("native-checkpoint-mismatch",error);}
    [Fact]public void SameCheckpointMayBeRecordedAgainWithoutBecomingAmbiguous()
    {string json=SaveData.Write(world),slot=Slot("one",json);NativeCheckpointBinding.Record(slot,json);Assert.Equal(json,NativeCheckpointBinding.Read(native,null,out var error));Assert.Null(error);}
    [Fact]public void ACorruptBoundSnapshotIsRejectedBeforeCurrentStateIsErased()
    {
        string slot=Slot("bad","{\"format\":999,\"kingdoms\":[]}");NativeWorldLifecycle.BeginLoad(native,slot);Loaded();
        Assert.False(NativeWorldLifecycle.TryComplete(map,world));Assert.NotNull(world.GetKingdom(7));Assert.Contains("validation-failed",NativeWorldLifecycle.Status);
    }
    [Fact]public void RepeatedCompletionDoesNotCreateAnotherWorldKey()
    {
        NativeWorldLifecycle.BeginCreation(true);Loaded();NativeWorldLifecycle.TryComplete(map,world);string key=world.Governance.WorldKey;
        Assert.True(NativeWorldLifecycle.TryComplete(map,world));Assert.Equal(key,world.Governance.WorldKey);Assert.Equal(1,NativeWorldLifecycle.Revision);
    }
    [Fact]public void NativeCallExceptionRevokesTheCreationProof()
    {
        NativeWorldLifecycle.BeginCreation(true);var failure=new InvalidOperationException("native generation failed");
        var result=typeof(NativeWorldLifecycle).GetMethod("TransitionFinalizer",BindingFlags.NonPublic|BindingFlags.Static).Invoke(null,new object[]{failure});
        Assert.Same(failure,result);Loaded();Assert.False(NativeWorldLifecycle.TryComplete(map,world));Assert.Equal("old-world",world.Governance.WorldKey);
    }
    [Fact]public void ExplicitOldSlotCanRestoreItsOwnLegacySidecarWithoutANewModeManifest()
    {
        var legacy=new PoliticalWorld();legacy.SetYear(507);legacy.AddKingdom(123,IdeologyType.Democracy,7);
        string path=Path.Combine(folder,"legacy-own");Directory.CreateDirectory(path);File.WriteAllBytes(Path.Combine(path,"map.wbox"),native);
        File.WriteAllText(Path.Combine(path,"realpolitic_state.json"),"{\"format\":17,\"year\":507,\"kingdoms\":[{\"id\":123,\"i\":0}],\"cities\":[]}");
        NativeWorldLifecycle.BeginLoad(native,path);Loaded();Assert.True(NativeWorldLifecycle.TryComplete(map,world));
        Assert.False(world.Governance.Enabled);Assert.Equal(507,world.Year);Assert.NotNull(world.GetKingdom(123));Assert.Null(world.GetKingdom(7));
    }
    [Fact]public void UnboundEnabledSidecarCannotMasqueradeAsALegacySelectedSlot()
    {
        string path=Path.Combine(folder,"unbound-new");Directory.CreateDirectory(path);File.WriteAllBytes(Path.Combine(path,"map.wbox"),native);
        File.WriteAllText(Path.Combine(path,"realpolitic_state.json"),SaveData.Write(world));
        NativeWorldLifecycle.BeginLoad(native,path);Loaded();Assert.False(NativeWorldLifecycle.TryComplete(map,world));Assert.Equal(507,world.Year);Assert.NotNull(world.GetKingdom(7));
    }

    [Fact]public void OverwrittenSlotDoesNotKeepAnUnboundedSeriesOfFullWorldCopies()
    {
        string json=SaveData.Write(world);Slot("rolling",json);for(int i=0;i<20;i++)Slot("rolling",json,new byte[]{2,(byte)i});
        Assert.Single(Directory.GetFiles(Path.Combine(folder,"WorldCheckpoints"),"*.json"));
        Assert.Null(NativeCheckpointBinding.Read(native,null,out var error));Assert.Contains("selected-slot",error);
    }
    [Fact]public void ReplacingOneSlotDoesNotPruneACheckpointStillUsedByAnotherSlot()
    {
        string json=SaveData.Write(world);Slot("shared-a",json);Slot("shared-b",json);Slot("shared-a",json,new byte[]{4,2,0});
        Assert.Equal(json,NativeCheckpointBinding.Read(native,null,out var error));Assert.Null(error);
    }

    [Fact]public void IndexedSnapshotAlsoHasItsOwnIntegrityCheck()
    {
        Slot("signed",SaveData.Write(world));string file=Assert.Single(Directory.GetFiles(Path.Combine(folder,"WorldCheckpoints"),"*.json"));
        File.WriteAllText(file,File.ReadAllText(file).Replace("\"year\":507","\"year\":508"));
        NativeWorldLifecycle.BeginLoad(native,null);Loaded();Assert.False(NativeWorldLifecycle.TryComplete(map,world));Assert.Equal(507,world.Year);
    }

    private string SelectedEntry(params object[] arguments)
    {
        object[] call={arguments,null};typeof(NativeWorldLifecycle).GetMethod("SelectedLoadPrefix",BindingFlags.NonPublic|BindingFlags.Static).Invoke(null,call);return (string)call[1];
    }
    [Fact]public void SlotLabelResolvesThroughWorldBoxSaveManager()
    {
        string slot=Path.Combine(folder,"actual-save-folder");Directory.CreateDirectory(slot);
        SaveManager.TestResolvedFolder=slot;
        try
        {
            Assert.Equal(Path.GetFullPath(slot),NativeCheckpointBinding.ResolveSaveDirectory("save37"));
            Assert.Equal(Path.GetFullPath(slot),Path.GetFullPath(Path.GetDirectoryName(WorldPersistence.ResolveSavePath("save37"))));
            string sidecar=Path.Combine(slot,"realpolitic_state.json");File.WriteAllText(sidecar,"{}");
            Assert.Equal(sidecar,WorldPersistence.ResolveLoadPath("save37"));
        }
        finally{SaveManager.TestResolvedFolder=null;}
    }

    [Fact]public void LegacySelectedSlotHasAStableIdentityAcrossReloads()
    {
        string slot=Path.Combine(folder,"legacy-stable-slot");Directory.CreateDirectory(slot);
        File.WriteAllBytes(Path.Combine(slot,"map.wbox"),native);
        NativeWorldLifecycle.BeginLoad(native,slot);Loaded();Assert.True(NativeWorldLifecycle.TryComplete(map,world));
        string first=NativeWorldLifecycle.LoadedSlotWorldKey;
        Assert.StartsWith("slot-",first);

        NativeWorldLifecycle.BeginLoad(native,slot);Loaded();Assert.True(NativeWorldLifecycle.TryComplete(map,world));
        Assert.Equal(first,NativeWorldLifecycle.LoadedSlotWorldKey);

        string other=Path.Combine(folder,"another-slot");Directory.CreateDirectory(other);
        Assert.NotEqual(first,NativeWorldLifecycle.LegacyWorldKeyForSlot(other));
    }

    [Fact]public void NativeInstanceSlotLoaderDoesNotRequireAByteLoaderCall()
    {
        string slot=Slot("instance-slot",SaveData.Write(world));world.Clear();SelectedEntry(slot,false);
        Loaded();Assert.True(NativeWorldLifecycle.TryComplete(map,world));Assert.Equal("old-world",world.Governance.WorldKey);Assert.Equal(507,world.Year);
    }
    [Fact]public void InstanceLoadDataCanSelectItsPathAfterItsSavedMapArgument()
    {
        string slot=Slot("instance-data",SaveData.Write(world));world.Clear();SelectedEntry(new object(),slot);
        Loaded();Assert.True(NativeWorldLifecycle.TryComplete(map,world));Assert.NotNull(world.GetKingdom(7));Assert.True(world.Governance.Enabled);
    }
    [Fact]public void NativeInstanceLoaderExceptionRevokesItsTicketAndPreservesTheException()
    {
        string slot=Slot("failed-instance",SaveData.Write(world));var state=SelectedEntry(slot,false);var error=new InvalidOperationException("native-instance-failed");
        var returned=typeof(NativeWorldLifecycle).GetMethod("SelectedLoadFinalizer",BindingFlags.NonPublic|BindingFlags.Static).Invoke(null,new object[]{error,state});
        Assert.Same(error,returned);Loaded();Assert.False(NativeWorldLifecycle.TryComplete(map,world));Assert.Contains("native-instance-failed",NativeWorldLifecycle.Status);
    }
    [Fact]public void LoadingSignalAlreadyTrueAtInstanceEntryIsNotMissed()
    {
        string slot=Slot("already-loading",SaveData.Write(world));loading=true;SelectedEntry(slot,false);loading=false;
        Assert.True(NativeWorldLifecycle.TryComplete(map,world));Assert.Equal("old-world",world.Governance.WorldKey);
    }

    [Fact]public void CreationSamplesAnAlreadyActiveLoadingSignalAtEntry()
    {
        loading=true;NativeWorldLifecycle.BeginCreation(true);loading=false;
        Assert.True(NativeWorldLifecycle.TryComplete(map,world));Assert.NotEqual("old-world",world.Governance.WorldKey);
    }
    [Fact]public void SelectedBoundSlotWithAMissingSidecarCannotBecomeALegacyWorld()
    {
        string slot=Slot("missing-political",SaveData.Write(world));File.Delete(Path.Combine(slot,"realpolitic_state.json"));
        NativeWorldLifecycle.BeginLoad(native,slot);Loaded();Assert.False(NativeWorldLifecycle.TryComplete(map,world));
        Assert.Equal("old-world",world.Governance.WorldKey);Assert.NotNull(world.GetKingdom(7));
        Assert.Contains("political-checkpoint-missing",NativeWorldLifecycle.Status);
    }
    [Fact]public void ADeletedIndexedSnapshotDoesNotSilentlyDowngradeToLegacy()
    {
        Slot("missing-index",SaveData.Write(world));File.Delete(Assert.Single(Directory.GetFiles(Path.Combine(folder,"WorldCheckpoints"),"*.json")));
        NativeWorldLifecycle.BeginLoad(native,null);Loaded();Assert.False(NativeWorldLifecycle.TryComplete(map,world));
        Assert.Equal(507,world.Year);Assert.Contains("indexed-political-checkpoint-missing",NativeWorldLifecycle.Status);
    }
    [Fact]public void SelectedBoundSlotWithAMissingNativeMapDoesNotBecomeAnImport()
    {
        string slot=Slot("missing-map",SaveData.Write(world));File.Delete(Path.Combine(slot,"map.wbox"));SelectedEntry(slot,false);
        Loaded();Assert.False(NativeWorldLifecycle.TryComplete(map,world));Assert.NotNull(world.GetKingdom(7));
        Assert.Contains("selected-native-checkpoint-missing",NativeWorldLifecycle.Status);
    }
    [Fact]public void ExplicitSelectedFileDoesNotReadItsDifferentSiblingInstead()
    {
        string slot=Slot("two-map-files",SaveData.Write(world));string selected=Path.Combine(slot,"map.json");File.WriteAllBytes(selected,new byte[]{9,8,7});
        SelectedEntry(selected,false);Loaded();Assert.False(NativeWorldLifecycle.TryComplete(map,world));
        Assert.Equal("old-world",world.Governance.WorldKey);Assert.Contains("native-checkpoint-mismatch",NativeWorldLifecycle.Status);
    }

    private object LifecycleCall(string name,params object[] args)
        =>typeof(NativeWorldLifecycle).GetMethod(name,BindingFlags.NonPublic|BindingFlags.Static).Invoke(null,args);
    private string ActiveLoadFolder()
        =>(string)typeof(NativeWorldLifecycle).GetField("_loadFolder",BindingFlags.NonPublic|BindingFlags.Static).GetValue(null);
    [Fact]public void NestedLoadDataWithoutAPathPreservesTheOuterPhysicalSlotProof()
    {
        string slot=Slot("nested-data",SaveData.Write(world));string outer=SelectedEntry(slot,false);
        Assert.Equal(slot,ActiveLoadFolder());string inner=SelectedEntry(new object(),null);
        LifecycleCall("SelectedLoadPostfix",inner);Assert.Equal(slot,ActiveLoadFolder());
        LifecycleCall("SelectedLoadPostfix",outer);Assert.Null(ActiveLoadFolder());
        Loaded();Assert.True(NativeWorldLifecycle.TryComplete(map,world));Assert.Equal("old-world",world.Governance.WorldKey);
    }
    [Fact]public void NestedExceptionRetainsTheFailureAndRestoresBothPathScopes()
    {
        string slot=Slot("nested-error",SaveData.Write(world));string outer=SelectedEntry(slot,false),inner=SelectedEntry(new object(),null);
        var error=new InvalidOperationException("nested-native-failure");Assert.Same(error,LifecycleCall("SelectedLoadFinalizer",error,inner));
        Assert.Equal(slot,ActiveLoadFolder());LifecycleCall("SelectedLoadPostfix",outer);Assert.Null(ActiveLoadFolder());
        Loaded();Assert.False(NativeWorldLifecycle.TryComplete(map,world));Assert.Contains("nested-native-failure",NativeWorldLifecycle.Status);
    }
    [Fact]public void AFailedPathWrapperRevokesTheTicketMadeByItsNestedByteLoader()
    {
        Slot("wrapper-error",SaveData.Write(world));string previousFolder=SelectedEntry("Maps/resource-world");
        NativeWorldLifecycle.BeginLoad(native,null);var error=new InvalidOperationException("path-wrapper-failed");
        Assert.Same(error,LifecycleCall("SelectedLoadFinalizer",error,previousFolder));Assert.Null(ActiveLoadFolder());
        Loaded();Assert.False(NativeWorldLifecycle.TryComplete(map,world));Assert.Contains("path-wrapper-failed",NativeWorldLifecycle.Status);
    }
    [Fact]public void StartLoadSlotHoldsTicksDuringPreLoadAnimationUntilLoadWorldTakesOver()
    {
        string slot=Slot("save21",SaveData.Write(world));
        LifecycleCall("StartLoadSlotPrefix");
        Assert.True(NativeWorldLifecycle.LoadSlotPending);
        Assert.True(NativeWorldLifecycle.Transitioning);
        Assert.False(NativeWorldLifecycle.TryComplete(map,world));
        Assert.Equal("awaiting-native-load-slot-entry",NativeWorldLifecycle.Status);
        string state=SelectedEntry(slot,false);
        Assert.False(NativeWorldLifecycle.LoadSlotPending);
        Assert.True(NativeWorldLifecycle.Transitioning);
        LifecycleCall("SelectedLoadPostfix",state);
        Loaded();
        Assert.True(NativeWorldLifecycle.TryComplete(map,world));
        Assert.False(NativeWorldLifecycle.Transitioning);
        Assert.Equal("old-world",world.Governance.WorldKey);
    }
    [Fact]public void UncompressedAutosaveWbaxBindsAndReloadsLikeCompressedWbox()
    {
        string autosave=Path.Combine(folder,"autosave-1793003135");Directory.CreateDirectory(autosave);
        File.WriteAllBytes(Path.Combine(autosave,"map.wbax"),native);
        string json=SaveData.Write(world);
        File.WriteAllText(Path.Combine(autosave,"realpolitic_state.json"),json);
        NativeCheckpointBinding.Record(autosave,json);
        Assert.True(File.Exists(Path.Combine(autosave,"realpolitic_checkpoint.sha256")));
        world.Clear();
        string state=SelectedEntry(autosave,false);
        LifecycleCall("SelectedLoadPostfix",state);
        Loaded();
        Assert.True(NativeWorldLifecycle.TryComplete(map,world));
        Assert.Equal("old-world",world.Governance.WorldKey);
        Assert.NotNull(world.GetKingdom(7));
    }

}
