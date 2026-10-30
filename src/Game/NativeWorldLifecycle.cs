using System;
using System.IO;
using System.Reflection;
using System.Security.Cryptography;
using HarmonyLib;
using NeoModLoader.services;
using RealPolitic.Core;

namespace RealPolitic;

/// <summary>Explicit native creation/load tickets. Never infers world creation from map reference or age.
/// Signatures are capability-checked; generation/loading are asynchronous and must pass through a loading phase.</summary>
public static class NativeWorldLifecycle
{
    private sealed class Transition
    {
        public bool Creating;
        public bool SawLoading;
        public string Json;
        public string SlotFolder;
        public string Error;
    }
    private static Transition _pending;
    private static DateTime _loadSlotHoldUntilUtc;
    private static string _loadFolder;
    private static string _activeSlotFolder;
    private static bool _capable;
    private static bool _installed;
    private static string _lastHold;
    public static long Revision {get;private set;}
    public static bool LoadSlotPending=>_pending==null && DateTime.UtcNow<_loadSlotHoldUntilUtc;
    public static bool Transitioning=>_pending!=null || DateTime.UtcNow<_loadSlotHoldUntilUtc;
    /// <summary>Stable identity for a legacy save slot whose political sidecar has no world key.</summary>
    public static string LoadedSlotWorldKey=>LegacyWorldKeyForSlot(_activeSlotFolder);
    internal static string LegacyWorldKeyForSlot(string folder)
    {
        if(string.IsNullOrWhiteSpace(folder))return null;
        try
        {
            string path=Path.GetFullPath(folder).TrimEnd(Path.DirectorySeparatorChar,Path.AltDirectorySeparatorChar).ToLowerInvariant();
            using(var sha=SHA256.Create())
            {
                byte[] digest=sha.ComputeHash(System.Text.Encoding.UTF8.GetBytes(path));
                return "slot-"+BitConverter.ToString(digest,0,16).Replace("-","").ToLowerInvariant();
            }
        }
        catch{return null;}
    }
    public static string Status=>_pending?.Error??(_pending!=null?"awaiting-native-loading-completion":LoadSlotPending?"awaiting-native-load-slot-entry":"ready");
    internal static Func<bool?> LoadingReader=ReadLoading;
    internal static void ResetForTests(){_pending=null;_loadSlotHoldUntilUtc=DateTime.MinValue;_loadFolder=null;_activeSlotFolder=null;_capable=false;_installed=false;_lastHold=null;Revision=0;LoadingReader=ReadLoading;}
    internal static void BeginLoadSlotHold()=>_loadSlotHoldUntilUtc=DateTime.UtcNow.AddSeconds(5);
    public static int Install(Harmony harmony)
    {
        _installed=true;int count=0;bool creation=false,load=false,save=false;
        var map=Patches.FindGameType("MapBox");var manager=Patches.FindGameType("SaveManager");
        if(map!=null)creation=Patch(harmony,map.GetMethod("generateNewMap",BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic,null,Type.EmptyTypes,null),nameof(GeneratePrefix),nameof(ScheduledPostfix));
        if(manager!=null)
        {
            var flags=BindingFlags.Static|BindingFlags.Public|BindingFlags.NonPublic;
            load=Patch(harmony,manager.GetMethod("loadMapFromBytes",flags,null,new[]{typeof(byte[])},null),nameof(LoadBytesPrefix),nameof(ScheduledPostfix));
            save=Patch(harmony,manager.GetMethod("saveWorldToDirectory",flags,null,new[]{typeof(string),typeof(bool),typeof(bool)},null),null,nameof(SaveDirectoryPostfix));
            // Confirmed by the user's native API dump: UI slot loading is an INSTANCE method,
            // and need not pass through the static byte-array loader.
            var instance=BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic;
            var startSlot=manager.GetMethod("startLoadSlot",instance,null,Type.EmptyTypes,null);
            if(startSlot!=null && Patch(harmony,startSlot,nameof(StartLoadSlotPrefix),null))count++;
            var selected=manager.GetMethod("loadWorld",instance,null,new[]{typeof(string),typeof(bool)},null);
            if(selected!=null && Patch(harmony,selected,nameof(SelectedLoadPrefix),nameof(SelectedLoadPostfix))){count++;load=true;}
            var saved=Patches.FindGameType("SavedMap");
            var data=saved==null?null:manager.GetMethod("loadData",instance,null,new[]{saved,typeof(string)},null);
            if(data!=null && Patch(harmony,data,nameof(SelectedLoadPrefix),nameof(SelectedLoadPostfix)))count++;
            // Only the verified world-resource wrapper supplies context. loadStatsFrom(string)
            // also exists in the real API, but reading statistics is NOT a world transition.
            var resource=manager.GetMethod("loadMapFromResources",flags,null,new[]{typeof(string)},null);
            if(resource!=null && Patch(harmony,resource,nameof(SelectedLoadPrefix),nameof(SelectedLoadPostfix)))count++;
        }
        int allianceGuard=NativeAllianceGuards.Install(harmony);count+=allianceGuard;
        _capable=creation && load && save && allianceGuard>0;
        if(creation)count++;if(load)count++;if(save)count++;
        LogService.LogInfo("[Real Politic] native lifecycle: creation="+creation+", bytes-load="+load+", directory-save="+save+", alliance-guard="+allianceGuard+"; automatic new-world capability="+_capable);
        return count;
    }
    private static bool Patch(Harmony harmony,MethodInfo method,string prefix,string postfix)
    {
        if(harmony==null || method==null || method.IsAbstract || method.ContainsGenericParameters)return false;
        try
        {
            harmony.Patch(method,prefix==null?null:new HarmonyMethod(typeof(NativeWorldLifecycle),prefix,null),
                postfix==null?null:new HarmonyMethod(typeof(NativeWorldLifecycle),postfix,null),null,
                prefix==nameof(SelectedLoadPrefix)?new HarmonyMethod(typeof(NativeWorldLifecycle),nameof(SelectedLoadFinalizer),null):
                prefix==nameof(GeneratePrefix) || prefix==nameof(LoadBytesPrefix)?new HarmonyMethod(typeof(NativeWorldLifecycle),nameof(TransitionFinalizer),null):null,null);return true;
        }
        catch(Exception e){LogService.LogError("[Real Politic] lifecycle hook "+method.Name+": "+e.Message);return false;}
    }
    private static Exception TransitionFinalizer(Exception __exception)
    {
        if(__exception!=null && _pending!=null)_pending.Error="native-lifecycle-call-failed: "+__exception.Message;
        return __exception;
    }
    private static bool? ReadLoading()
    {
        var type=Patches.FindGameType("Config");if(type==null)return null;
        var property=type.GetProperty("worldLoading",BindingFlags.Static|BindingFlags.Public|BindingFlags.NonPublic);
        try{return property?.GetValue(null,null) is bool loading?(bool?)loading:null;}catch{return null;}
    }
    private static void StartLoadSlotPrefix()=>BeginLoadSlotHold();
    private static void GeneratePrefix()
    {
        // A loader may use generation helpers. An explicit load ticket always takes precedence.
        if(_pending!=null && !_pending.Creating)return;
        BeginCreation(_capable);
    }
    internal static void BeginCreation(bool capable)
    { _loadSlotHoldUntilUtc=DateTime.MinValue;_activeSlotFolder=null;_pending=new Transition{Creating=true,Error=capable?null:"native-lifecycle-capability-incomplete"};ObserveLoading(); }
    private static void LoadBytesPrefix(object[] __args)
    {
        if(__args==null || __args.Length!=1 || !(__args[0] is byte[] bytes))return;
        BeginLoad(bytes,_loadFolder);
    }
    internal static void BeginLoad(byte[] bytes,string folder)
    {
        _loadSlotHoldUntilUtc=DateTime.MinValue;
        var transition=new Transition{SlotFolder=string.IsNullOrWhiteSpace(folder)?null:folder};
        try{transition.Json=NativeCheckpointBinding.Read(bytes,folder,out var error);transition.Error=error;}
        catch(Exception e){transition.Error="checkpoint-read-failed: "+e.Message;}
        _pending=transition;ObserveLoading();
    }
    private static void ScheduledPostfix(){ObserveLoading();}
    private static void ObserveLoading(){if(_pending!=null && LoadingReader()==true)_pending.SawLoading=true;}
    /// <summary>Called before reading registries. False holds all mod simulation until a verified completion.</summary>
    public static bool TryComplete(MapBox map,PoliticalWorld world)
    {
        if(LoadSlotPending)return false;
        if(_pending==null)return true;
        ObserveLoading();bool? loading=LoadingReader();
        if(_pending.Error!=null || loading==null)
        {
            string reason=_pending.Error??"native-loading-state-unavailable";
            if(_lastHold!=reason){_lastHold=reason;LogService.LogError("[Real Politic] политическая симуляция приостановлена: "+reason);}
            return false;
        }
        if(map==null || world==null || loading!=false || !_pending.SawLoading)return false;
        var pending=_pending;
        try
        {
            // Pre-validate without touching the live world; malformed checkpoints never turn into a new world.
            if(pending.Json!=null)
            {
                var validation=new PoliticalWorld();SaveData.Apply(validation,pending.Json);
                if(validation.Governance.Enabled && _installed && !_capable)throw new InvalidOperationException("Native governance contracts are incomplete.");
                SaveData.Apply(world,pending.Json);
            }
            else
            {
                world.Clear();
                if(pending.Creating)world.BeginNewWorldGovernance("native-"+Guid.NewGuid().ToString("N"));
            }
            _activeSlotFolder=pending.Creating?null:pending.SlotFolder;
            _pending=null;_lastHold=null;Revision=checked(Revision+1);WorldPersistence.ResetSaveThrottle();return true;
        }
        catch(Exception e){pending.Error="checkpoint-validation-failed: "+e.Message;return false;}
    }
    private static void SelectedLoadPrefix(object[] __args,out string __state)
    {
        LoadPathPrefix(__args,out __state);
        if(__args!=null)foreach(var arg in __args)if(arg is string path)
        {
            try
            {
                string directFile=File.Exists(path)?Path.GetFullPath(path):null;
                string directory=NativeCheckpointBinding.ResolveSaveDirectory(path);
                if(directory==null && directFile!=null)directory=Path.GetDirectoryName(directFile);
                if(directory==null)continue;
                // A file path is an explicit selection, not permission to substitute a sibling map.
                string native=directFile??NativeCheckpointBinding.ResolveNativeMapPath(directory);
                if(native==null || !File.Exists(native))
                {
                    // Evidence of a political slot cannot be silently reclassified as a resource import.
                    if(File.Exists(Path.Combine(directory,"realpolitic_state.json")) || File.Exists(Path.Combine(directory,"realpolitic_checkpoint.sha256")))
                    {_pending=new Transition{Error="selected-native-checkpoint-missing"};return;}
                    continue;
                }
                _loadFolder=directory;BeginLoad(File.ReadAllBytes(native),directory);return;
            }
            catch(Exception ex){_pending=new Transition{Error="selected-checkpoint-read-failed: "+ex.Message};return;}
        }
        // Resource/imported maps without a physical slot are legacy, never a creation ticket.
        // Do not discard a byte-loader's already established proof for nested loadData calls.
        if(_pending==null || _pending.Creating)BeginLoad(Array.Empty<byte>(),null);
    }
    private static void SelectedLoadPostfix(string __state){ObserveLoading();LoadPathPostfix(__state);}
    private static Exception SelectedLoadFinalizer(Exception __exception,string __state)
    {TransitionFinalizer(__exception);_loadFolder=__state;return __exception;}
    private static void LoadPathPrefix(object[] __args,out string __state)
    {
        __state=_loadFolder;if(__args==null)return;
        foreach(var arg in __args)if(arg is string path)
        {
            try
            {
                string directFile=File.Exists(path)?Path.GetFullPath(path):null;
                string directory=NativeCheckpointBinding.ResolveSaveDirectory(path);
                if(directory==null && directFile!=null)directory=Path.GetDirectoryName(directFile);
                if(directory!=null && (directFile!=null || NativeCheckpointBinding.ResolveNativeMapPath(directory)!=null)){_loadFolder=directory;return;}
            }
            catch{}
        }
    }
    private static void LoadPathPostfix(string __state){_loadFolder=__state;}
    private static void SaveDirectoryPostfix(object[] __args,object __result)
    {
        if(Transitioning || __result==null || __args==null || __args.Length<1 || !(__args[0] is string rawFolder))return;
        string folder=NativeCheckpointBinding.ResolveSaveDirectory(rawFolder)??rawFolder;
        var world=RealPoliticMod.World;if(world==null)return;
        if(WorldPersistence.Save(folder))
        {
            try{NativeCheckpointBinding.Record(folder,SaveData.Write(world));}
            catch(Exception e){LogService.LogError("[Real Politic] native checkpoint binding failed: "+e.Message);}
        }
    }
}

/// <summary>Binds political state to exact native checkpoint bytes, not merely the world's identity or age.
/// Ambiguous identical native bytes with different political snapshots are never silently resolved to the latest.</summary>
public static class NativeCheckpointBinding
{
    private const string StateFile="realpolitic_state.json";
    private const string ManifestFile="realpolitic_checkpoint.sha256";
    private static string Index=>Path.Combine(RealPoliticMod.ModFolder??"","WorldCheckpoints");
    private static string Hash(byte[] bytes)
    {using(var sha=SHA256.Create())return BitConverter.ToString(sha.ComputeHash(bytes)).Replace("-","").ToLowerInvariant();}
    private static string TextHash(string text)=>Hash(System.Text.Encoding.UTF8.GetBytes(text));

    /// <summary>
    /// WorldBox's load hooks pass slot labels such as "save37", not necessarily a filesystem
    /// path. Resolve those labels through the game's own SaveManager API before looking for the
    /// map or its political sidecar; otherwise a valid load is misclassified as an unbound map.
    /// </summary>
    internal static string ResolveSaveDirectory(string path)
    {
        if(string.IsNullOrWhiteSpace(path))return null;
        try
        {
            if(Directory.Exists(path))return Path.GetFullPath(path);
            if(File.Exists(path))return Path.GetDirectoryName(Path.GetFullPath(path));
        }
        catch{}
        var manager=Patches.FindGameType("SaveManager");
        if(manager==null)return null;
        const BindingFlags flags=BindingFlags.Static|BindingFlags.Public|BindingFlags.NonPublic;
        foreach(string methodName in new[]{"folderPath","getSavePath"})
        {
            try
            {
                var method=manager.GetMethod(methodName,flags,null,new[]{typeof(string)},null);
                if(method==null || method.ReturnType!=typeof(string))continue;
                string resolved=method.Invoke(null,new object[]{path}) as string;
                if(string.IsNullOrWhiteSpace(resolved))continue;
                if(Directory.Exists(resolved))return Path.GetFullPath(resolved);
                if(File.Exists(resolved))return Path.GetDirectoryName(Path.GetFullPath(resolved));
                if(ResolveNativeMapPath(resolved)!=null)return Path.GetFullPath(resolved);
            }
            catch{}
        }
        return null;
    }

    internal static string ResolveNativeMapPath(string folder)
    {
        if(string.IsNullOrWhiteSpace(folder))return null;
        var candidates=new System.Collections.Generic.List<string>{"map.wbox","map.wbax","map.json"};
        try
        {
            var sm=Patches.FindGameType("SaveManager");
            if(sm!=null)
            {
                var flags=BindingFlags.Static|BindingFlags.Public|BindingFlags.NonPublic;
                foreach(string fieldName in new[]{"name_main_data","name_main_data_non_zip","name_main_data_old"})
                {
                    if(sm.GetField(fieldName,flags)?.GetValue(null) is string val && !string.IsNullOrWhiteSpace(val) && !candidates.Contains(val))
                        candidates.Add(val);
                }
            }
        }
        catch{}
        foreach(string name in candidates)
        {
            string candidate=Path.Combine(folder,name);
            if(File.Exists(candidate))return candidate;
        }
        return null;
    }
    public static void Record(string folder,string json)
    {
        if(string.IsNullOrWhiteSpace(folder) || string.IsNullOrWhiteSpace(RealPoliticMod.ModFolder))throw new IOException("Missing checkpoint folder.");
        string native=ResolveNativeMapPath(folder);
        if(native==null || !File.Exists(native))throw new IOException("Completed native map file was not found.");
        string nativeHash=Hash(File.ReadAllBytes(native));string politicalHash=TextHash(json);
        // Publish a complete hash pair last; a half-written sidecar can never match this manifest.
        if(!File.Exists(Path.Combine(folder,StateFile)) || TextHash(File.ReadAllText(Path.Combine(folder,StateFile)))!=politicalHash)
            throw new IOException("Sidecar changed before checkpoint binding.");
        Directory.CreateDirectory(Index);
        Atomic(Path.Combine(Index,nativeHash+".pending"),"checkpoint-publication-in-progress");
        Atomic(Path.Combine(folder,ManifestFile),nativeHash+"\n"+politicalHash+"\n");
        string path=Path.Combine(Index,nativeHash+".json");
        if(File.Exists(path) && File.ReadAllText(path)!=json)Atomic(Path.Combine(Index,nativeHash+".ambiguous"),"multiple-political-snapshots");
        else if(!File.Exists(path))Atomic(path,json);
        Atomic(Path.Combine(Index,nativeHash+".sha256"),TextHash(File.ReadAllText(path)));
        string slots=Path.Combine(Index,"SlotReferences");Directory.CreateDirectory(slots);
        string canonical=Path.GetFullPath(folder).TrimEnd(Path.DirectorySeparatorChar,Path.AltDirectorySeparatorChar);
        string slotKey=TextHash(Path.DirectorySeparatorChar=='\\'?canonical.ToUpperInvariant():canonical);
        string reference=Path.Combine(slots,slotKey+".slot");string[] previous=File.Exists(reference)?File.ReadAllLines(reference):Array.Empty<string>();
        Atomic(reference,nativeHash+"\n"+canonical+"\n");
        if(previous.Length==2 && previous[0]!=nativeHash && ValidHash(previous[0]))
        {
            bool retained=false;
            foreach(string file in Directory.GetFiles(slots,"*.slot"))
            {
                string[] record=File.ReadAllLines(file);if(record.Length!=2 || record[0]!=previous[0])continue;
                string manifest=Path.Combine(record[1],ManifestFile);
                if(File.Exists(manifest) && File.ReadAllLines(manifest).Length==2 && File.ReadAllLines(manifest)[0]==previous[0]){retained=true;break;}
            }
            if(!retained)
            {
                // Retain a tiny tombstone, not every historical full-world JSON. Unknown old bytes must not become a legacy world.
                Atomic(Path.Combine(Index,previous[0]+".pruned"),"no-current-slot-reference;selected-slot-required");
                string obsolete=Path.Combine(Index,previous[0]+".json");if(File.Exists(obsolete))File.Delete(obsolete);
                string digest=Path.Combine(Index,previous[0]+".sha256");if(File.Exists(digest))File.Delete(digest);
            }
        }
        File.Delete(Path.Combine(Index,nativeHash+".pending"));
    }
    private static bool ValidHash(string value)=>value!=null && value.Length==64 && System.Linq.Enumerable.All(value,c=>c>='0' && c<='9' || c>='a' && c<='f');
    public static string Read(byte[] bytes,string explicitFolder,out string error)
    {
        error=null;if(bytes==null)throw new ArgumentNullException(nameof(bytes));string hash=Hash(bytes);
        if(!string.IsNullOrWhiteSpace(explicitFolder))
        {
            string sidecar=Path.Combine(explicitFolder,StateFile),manifest=Path.Combine(explicitFolder,ManifestFile);
            if(!File.Exists(sidecar))
            {
                // A remaining binding proves this is an incomplete checkpoint, not an untouched legacy slot.
                if(File.Exists(manifest))error="selected-slot-political-checkpoint-missing";
                return null; // Selected legacy slot never imports a global backup.
            }
            if(!File.Exists(manifest))
            {
                string legacy=File.ReadAllText(sidecar);var root=TinyJson.ParseStrict(legacy);
                if(!GovernanceCodec.Read(root.Get("governance26")).Enabled)return legacy;
                error="selected-new-regime-checkpoint-is-not-bound";return null;
            }
            var hashes=File.ReadAllLines(manifest);
            if(hashes.Length!=2 || hashes[0]!=hash){error="selected-slot-native-checkpoint-mismatch";return null;}
            string json=File.ReadAllText(sidecar);
            if(TextHash(json)!=hashes[1]){error="selected-slot-political-checkpoint-mismatch";return null;}return json;
        }
        if(string.IsNullOrWhiteSpace(RealPoliticMod.ModFolder))return null;
        if(File.Exists(Path.Combine(Index,hash+".pending")) || File.Exists(Path.Combine(Index,hash+".pruned")))
        {error="native-checkpoint-requires-an-explicit-selected-slot";return null;}
        if(File.Exists(Path.Combine(Index,hash+".ambiguous"))){error="ambiguous-native-checkpoint-needs-selected-slot";return null;}
        string indexed=Path.Combine(Index,hash+".json"),indexDigest=Path.Combine(Index,hash+".sha256");
        if(!File.Exists(indexed))
        {
            if(File.Exists(indexDigest))error="indexed-political-checkpoint-missing";
            return null;
        }
        string snapshot=File.ReadAllText(indexed);
        if(!File.Exists(indexDigest) || File.ReadAllText(indexDigest)!=TextHash(snapshot)){error="indexed-political-checkpoint-mismatch";return null;}
        return snapshot;
    }
    private static void Atomic(string path,string text)
    {
        string temp=path+".tmp-"+Guid.NewGuid().ToString("N");
        try{File.WriteAllText(temp,text);if(File.Exists(path))File.Replace(temp,path,null);else File.Move(temp,path);}
        finally{if(File.Exists(temp))File.Delete(temp);}
    }
}
