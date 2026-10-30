using System;
using System.Reflection;
using HarmonyLib;
using NeoModLoader.services;

namespace RealPolitic;

/// <summary>Guard the verified native alliance entry points (forceAlliance, newAlliance, Alliance.join) confirmed by NativeApiSurface/NativeLifecycleTrace.</summary>
public static class NativeAllianceGuards
{
    public static int Install(Harmony harmony)
    {
        int installed=0;
        var type=Patches.FindGameType("AllianceManager")??Patches.FindGameType("AlliancesManager");
        if(type==null)type=Patches.FindGameType("MapBox")?.GetField("alliances",BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic)?.FieldType;
        var method=type?.GetMethod("forceAlliance",BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Instance,null,new[]{typeof(Kingdom),typeof(Kingdom)},null);
        if(method!=null && method.ReturnType==typeof(bool))
        {
            try{harmony.Patch(method,new HarmonyMethod(typeof(NativeAllianceGuards),nameof(Prefix),null),null,null,null,null);installed++;}
            catch(Exception e){LogService.LogError("[Real Politic] native alliance guard (forceAlliance): "+e.Message);}
        }
        var newAlliance=type?.GetMethod("newAlliance",BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Instance,null,new[]{typeof(Kingdom),typeof(Kingdom)},null);
        if(newAlliance!=null && newAlliance.ReturnType!=typeof(void))
        {
            try{harmony.Patch(newAlliance,new HarmonyMethod(typeof(NativeAllianceGuards),nameof(NewAlliancePrefix),null),null,null,null,null);installed++;}
            catch(Exception e){LogService.LogError("[Real Politic] native alliance guard (newAlliance): "+e.Message);}
        }
        var allianceType=Patches.FindGameType("Alliance");
        var join=allianceType?.GetMethod("join",BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Instance,null,new[]{typeof(Kingdom),typeof(bool),typeof(bool)},null);
        if(join!=null && join.ReturnType==typeof(bool))
        {
            try{harmony.Patch(join,new HarmonyMethod(typeof(NativeAllianceGuards),nameof(JoinPrefix),null),null,null,null,null);installed++;}
            catch(Exception e){LogService.LogError("[Real Politic] native alliance guard (Alliance.join): "+e.Message);}
        }
        return installed;
    }
    /// <summary>
    /// 0.47.1: мод сам зовёт forceAlliance (альянс КМУД, пакты после вердикта).
    /// Такие вызовы не должны попадать под запрет — иначе мод запретит сам себе.
    /// </summary>
    [ThreadStatic] private static bool _modCall;

    internal static void BeginModAllianceCall(){_modCall=true;}
    internal static void EndModAllianceCall(){_modCall=false;}

    /// <summary>0.45: ванильные альянсы закрыты, пока их не разрешил мод.</summary>
    private static bool Blocked(Kingdom a,Kingdom b)
    {
        // Мод создаёт союз сам — не мешаем.
        if(_modCall)return false;
        if(!RealPoliticMod.BlockNativeAlliances)return false;
        var world=RealPoliticMod.World;
        if(world==null || NativeWorldLifecycle.Transitioning)return false;
        // 0.47.1 (жалоба владельца 29.10.2026: «он уже в альянсе — зачем он долбится туда снова»).
        // Если хоть одна сторона уже в союзе, игра всё равно пытается «свести» их снова
        // каждый год и каждый раз дёргает игрока. Такой запрос гасим молча — и НИКАКОГО
        // предложения не создаём: предлагать вступить тому, кто уже в союзе, нечего.
        if(AllianceSafety.TryMembership(a,out var memberA) && memberA!=null)return true;
        if(AllianceSafety.TryMembership(b,out var memberB) && memberB!=null)return true;
        // 0.47.1: в мире всегда должно остаться место для войны — последние две державы
        // в один союз не входят (правило живёт в ядре, AllianceRoom).
        if(!HasRoomForAnotherAlly(a,b)){NoteNoRoom(a,b);return true;}
        // Если управление державами включено, мод сам разбирает такой клик и превращает
        // его в официальное предложение (см. ниже) — это не «обход», рубить нечего.
        if(world.Governance.Enabled)return false;
        if(CivicAllianceRuntime.ApprovedPair(a,b))return false;
        // Альянс самого КМУД мод создаёт сам — это не «обход».
        string name=GameRefs.ReadProperty(a,"name")?.ToString()??string.Empty;
        if(name==KmudAlliance.AllianceName)return false;
        NoteBlocked(a,b);
        return true;
    }

    /// <summary>
    /// 0.47.1: останется ли в мире место для войны, если эти две державы сведут в союз.
    /// Считаем живые державы с землёй и участников уже существующего союза.
    /// </summary>
    private static bool HasRoomForAnotherAlly(Kingdom a,Kingdom b)
    {
        try
        {
            int countries=CountRealmsWithLand();
            if(countries<=0)return true; // не смогли посчитать — не мешаем игре
            int members=0;
            if(AllianceSafety.TryMembership(a,out var alliance) && alliance!=null)
            {
                members=Math.Max(members,AllianceSafety.MemberCount(alliance));
            }
            // Обе стороны пока свободны: после союза участников будет двое.
            int after=members>0?members+1:2;
            if(!RealPolitic.Core.AllianceRoom.LeavesRoomForWar(countries,after))return false;
            return after<=RealPolitic.Core.AllianceRoom.MaxMembers(countries);
        }
        catch(Exception){return true;}
    }

    /// <summary>Сколько держав реально живёт на карте (с городами или с жителями).</summary>
    internal static int CountRealmsWithLand()
    {
        try
        {
            var map=World.world;
            var list=GameRefs.KingdomList(map);
            if(list==null)return 0;
            int count=0;
            for(int i=0;i<list.Count;i++)
            {
                var kingdom=list[i];
                if(kingdom==null)continue;
                if(kingdom.cities!=null && kingdom.cities.Count>0){count++;continue;}
                if(kingdom.units!=null && kingdom.units.Count>0)count++;
            }
            return count;
        }
        catch(Exception){return 0;}
    }

    /// <summary>Одна строка в журнал за год — чтобы было видно, что запрет работает.</summary>
    private static int _blockedYear=-1;
    private static void NoteBlocked(Kingdom a,Kingdom b)
    {
        try
        {
            int year=RealPoliticMod.CurrentYear();
            if(_blockedYear==year)return;
            _blockedYear=year;
            LogService.LogInfo("[Real Politic] альянс не создан: "+(GameRefs.ReadProperty(a,"name")??"?")
                +" и "+(GameRefs.ReadProperty(b,"name")??"?")+" — союзы заключаются через политику мода (КМУД, конституция)");
        }
        catch(Exception){}
    }

    internal static bool Prefix(Kingdom __0,Kingdom __1,ref bool __result)
    {
        var world=RealPoliticMod.World;
        if(Blocked(__0,__1)){__result=false;return false;}
        if(world?.Governance.Enabled!=true || NativeWorldLifecycle.Transitioning || CivicAllianceRuntime.ApprovedPair(__0,__1))return true;
        CivicAllianceRuntime.RequestAdmission(world,__0,__1,RealPoliticMod.CurrentYear());
        __result=false;return false; // A proposal is not a completed membership mutation.
    }
    internal static bool NewAlliancePrefix(Kingdom __0,Kingdom __1,ref object __result)
    {
        var world=RealPoliticMod.World;
        if(Blocked(__0,__1)){__result=null;return false;}
        if(world?.Governance.Enabled!=true || NativeWorldLifecycle.Transitioning || CivicAllianceRuntime.ApprovedPair(__0,__1))return true;
        CivicAllianceRuntime.RequestAdmission(world,__0,__1,RealPoliticMod.CurrentYear());
        __result=null;return false;
    }
    /// <summary>Одна строка в журнал за год: почему союз не разрешён по правилу «место для войны».</summary>
    private static int _noRoomYear=-1;
    private static void NoteNoRoom(Kingdom a,Kingdom b)
    {
        try
        {
            int year=RealPoliticMod.CurrentYear();
            if(_noRoomYear==year)return;
            _noRoomYear=year;
            LogService.LogInfo("[Real Politic] союз не разрешён: "
                +RealPolitic.Core.AllianceRoom.RefusalReason(CountRealmsWithLand()));
        }
        catch(Exception){}
    }

    internal static bool JoinPrefix(object __instance,Kingdom __0,ref bool __result)
    {
        var world=RealPoliticMod.World;
        // 0.47.1: уже в союзе — вступать некуда. Молча, без предложений и уведомлений.
        if(!_modCall && AllianceSafety.TryMembership(__0,out var already) && already!=null){__result=false;return false;}
        // Вход в чужой альянс закрыт так же, как создание своего.
        if(RealPoliticMod.BlockNativeAlliances && world!=null && !NativeWorldLifecycle.Transitioning
            && world.Governance.Enabled!=true
            && !CivicAllianceRuntime.ApprovedJoin(__instance,__0) && !IsKmudAlliance(__instance))
        {
            __result=false;return false;
        }
        if(world?.Governance.Enabled!=true || NativeWorldLifecycle.Transitioning || CivicAllianceRuntime.ApprovedJoin(__instance,__0))return true;
        CivicAllianceRuntime.RequestAdmissionFromAlliance(world,__instance,__0,RealPoliticMod.CurrentYear());
        __result=false;return false;
    }

    /// <summary>Это альянс КМУД? Его ведёт мод, а не игра — его не блокируем.</summary>
    private static bool IsKmudAlliance(object alliance)
    {
        try
        {
            string name=GameRefs.ReadProperty(alliance,"name")?.ToString()??string.Empty;
            return string.Equals(name,KmudAlliance.AllianceName,StringComparison.Ordinal);
        }
        catch(Exception){return false;}
    }
}
