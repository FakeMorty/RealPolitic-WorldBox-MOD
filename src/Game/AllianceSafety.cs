using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;

namespace RealPolitic;

/// <summary>Read-only membership checks. Never repairs saved game collections by guessing.</summary>
public static class AllianceSafety
{
    private const BindingFlags Flags=BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic;
    public static IEnumerable Members(object alliance)
    {
        IEnumerable fallback=null;
        foreach(var field in new[]{"kingdoms_hashset","kingdoms_list","kingdoms","p_kingdoms","members"})
            if(GameRefs.ReadMember(alliance,field) is IEnumerable list && !(list is string))
            {
                fallback ??= list;
                if(HasAnyElement(list))return list;
            }
        foreach(var method in new[]{"getMembers","getKingdoms"})
            if(GameRefs.Invoke(alliance,method) is IEnumerable list && !(list is string))
            {
                fallback ??= list;
                if(HasAnyElement(list))return list;
            }
        return fallback;
    }
    private static bool HasAnyElement(IEnumerable source)
    {
        if(source==null)return false;
        if(source is ICollection col)return col.Count>0;
        var enumerator=source.GetEnumerator();
        try{return enumerator.MoveNext();}
        finally{(enumerator as IDisposable)?.Dispose();}
    }
    private static List<object> ReadAlliances(object manager)
    {
        if(manager==null)return null;
        var list=GameRefs.ReadMember(manager,"list") as IEnumerable ?? manager as IEnumerable;
        var dict=GameRefs.ReadMember(manager,"dict") as IDictionary;
        if(list==null && dict==null)return null;
        var result=new List<object>();
        if(list!=null)
            foreach(var item in list)
                if(item!=null && IsAllianceAlive(item) && !ContainsRef(result,item))result.Add(item);
        if(dict!=null)
            foreach(var item in dict.Values)
                if(item!=null && IsAllianceAlive(item) && !ContainsRef(result,item))result.Add(item);
        return result;
    }
    private static bool IsAllianceAlive(object alliance)
    {
        if(GameRefs.ReadMember(alliance,"_alive") is bool alive && !alive)return false;
        if(GameRefs.Invoke(alliance,"isAlive") is bool isAlive && !isAlive)return false;
        return true;
    }
    private static bool ContainsRef(List<object> list,object target)
    {
        for(int i=0;i<list.Count;i++)if(ReferenceEquals(list[i],target))return true;
        return false;
    }
    public static bool TryMembership(Kingdom kingdom,out object alliance)
    {
        alliance=null;if(kingdom==null)return false;
        object manager=GameRefs.ReadMember(World.world,"alliances");
        var all=ReadAlliances(manager);
        // Inspect the full registry when available: a direct pointer alone hides duplicate membership.
        if(all!=null)
        {
            foreach(var candidate in all)
            {
                if(candidate==null)continue;
                var members=Members(candidate);if(members==null)return false;
                foreach(var member in members)
                    if(ReferenceEquals(member,kingdom))
                    {
                        if(alliance!=null && !ReferenceEquals(alliance,candidate))return false;
                        alliance=candidate;break;
                    }
            }
            // A stale direct pointer or dangling KingdomData.allianceID is not evidence of being safely unallied.
            foreach(var name in new[]{"alliance","current_alliance"})
                for(var t=kingdom.GetType();t!=null;t=t.BaseType)
                    if(t.GetField(name,Flags|BindingFlags.DeclaredOnly)!=null || t.GetProperty(name,Flags|BindingFlags.DeclaredOnly)!=null)
                        if(!ReferenceEquals(GameRefs.ReadMember(kingdom,name),alliance))return false;
            var directMethod=kingdom.GetType().GetMethod("getAlliance",Flags,null,Type.EmptyTypes,null);
            if(directMethod!=null)
            {
                try{if(!ReferenceEquals(directMethod.Invoke(kingdom,null),alliance))return false;}
                catch{return false;}
            }
            if(!ConsistentDataAllianceId(kingdom,alliance))return false;
            return true;
        }
        // Supported direct API if no enumerable registry exists.
        foreach(var name in new[]{"alliance","current_alliance"})
            for(var t=kingdom.GetType();t!=null;t=t.BaseType)
            {
                if(t.GetField(name,Flags|BindingFlags.DeclaredOnly)!=null || t.GetProperty(name,Flags|BindingFlags.DeclaredOnly)!=null)
                {alliance=GameRefs.ReadMember(kingdom,name);return ConsistentDataAllianceId(kingdom,alliance);}
            }
        var method=kingdom.GetType().GetMethod("getAlliance",Flags,null,Type.EmptyTypes,null);
        if(method!=null){try{alliance=method.Invoke(kingdom,null);return ConsistentDataAllianceId(kingdom,alliance);}catch{return false;}}
        return false;
    }
    private static bool ConsistentDataAllianceId(Kingdom kingdom,object alliance)
    {
        object data=GameRefs.ReadMember(kingdom,"data");if(data==null)return true;
        object raw=GameRefs.ReadMember(data,"allianceID");if(raw==null)return true;
        try
        {
            long id=Convert.ToInt64(raw);
            if(id<=0)return true;
            if(alliance==null)return false;
            int stable=StableId(alliance);
            return stable<=0 || id==stable;
        }
        catch{return false;}
    }
    /// <summary>0.47.1: сколько держав в альянсе (0 — не удалось прочитать).</summary>
    public static int MemberCount(object alliance)
    {
        var members=Members(alliance);
        if(members==null)return 0;
        int count=0;
        foreach(var item in members)
            if(item!=null)count++;
        return count;
    }

    public static Kingdom FirstMember(object alliance,Kingdom exclude=null)
    {
        var members=Members(alliance);if(members==null)return null;
        foreach(var item in members)
            if(item is Kingdom country && !ReferenceEquals(country,exclude))return country;
        return null;
    }
    public static bool Same(Kingdom a,Kingdom b)
    {
        return TryMembership(a,out var left) && left!=null && TryMembership(b,out var right) && ReferenceEquals(left,right);
    }
    public static bool CanJoin(Kingdom a,Kingdom b)
    {
        if(a==null || b==null || ReferenceEquals(a,b))return false;
        if(!TryMembership(a,out var left) || !TryMembership(b,out var right))return false;
        // No forced merger of distinct native alliances, and no repeated no-op success.
        return left==null || right==null;
    }
    public static int StableId(object alliance)
    {
        object value=GameRefs.ReadMember(alliance,"id") ?? GameRefs.ReadMember(GameRefs.ReadMember(alliance,"data"),"id");
        try {long id=Convert.ToInt64(value);return id>0 && id<=int.MaxValue ? (int)id : -1;}catch{return -1;}
    }
}
