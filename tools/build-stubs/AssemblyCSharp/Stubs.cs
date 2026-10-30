// Заглушки API WorldBox (Assembly-CSharp) — ТОЛЬКО для компиляции мода.
// ВАЖНО: вид члена (поле/свойство) и тип должны ТОЧНО совпадать с реальной игрой,
// иначе скомпилированный IL будет искать несуществующий член (MissingFieldException /
// MissingMethodException) и мод упадёт при загрузке. Источник — декомпиляция
// настоящей Assembly-CSharp (game_decomp/).

using System.Collections.Generic;

// ---- Данные (global namespace, как в игре) ----
// Реально: BaseSystemData : IDisposable { public long id { get; set; } public string name { get; set; } }
public class BaseSystemData
{
    public long id { get; set; }
    public string name { get; set; }
}

public class KingdomData : BaseSystemData { }
public class CityData : BaseSystemData { }

// Реально: Clan : Family (именованный род; id и name наследуются от NanoObject/data).
public class Clan
{
    public long id { get; set; }
    public string name { get; set; }
}

// Реально: ActorData { public long parent_id_1 { get; set; } public long parent_id_2 { get; set; }
//   public Clan clan { get; set; } public Family family { get; set; } ... }
public class ActorData : BaseSystemData
{
    public long parent_id_1 { get; set; }
    public long parent_id_2 { get; set; }
    public Clan clan { get; set; }
}

// ---- Объекты мира ----
// Реально: NanoObject { public long id => getID(); public virtual string name { get; set; } }
public class NanoObject
{
    public virtual long id { get; set; } // сеттер добавлен для тестов; в игре id => getID()
    public virtual string name { get; set; }
}

// Реально: BaseSimObject : NanoObject { public Kingdom kingdom; }  (поля city/data здесь НЕТ)
public class BaseSimObject : NanoObject
{
    public Kingdom kingdom;
}

// Реально: Actor : BaseSimObject { public ActorData data; public City city; public Clan clan => data.clan; ... }
public class Actor : BaseSimObject
{
    public ActorData data;
    public City city;

    public Clan clan => data?.clan;

    // Реально: возраст читается через getAge()/age.
    public int age { get; set; }

    // Реально: public int getHappiness(); public void setHappiness(int pValue, bool pClamp = true);
    public int getHappiness() => 0;
    public void setHappiness(int pValue, bool pClamp = true) { }
}

// Реально: CoreSystemObject<TData> : NanoObject { public TData data; }
public class CoreSystemObject<TData> : NanoObject
{
    public TData data;
}

// Реально: MetaObject<TData> : CoreSystemObject<TData> { public List<Actor> units { get; } }
// (сеттер добавлен только для тестов; IL мода ссылается на getter)
public class MetaObject<TData> : CoreSystemObject<TData>
{
    public List<Actor> units { get; set; }
}

// Реально: Kingdom : MetaObjectWithTraits<KingdomData, KingdomTrait> { public readonly List<City> cities; public City capital; ... }
public class Kingdom : MetaObject<KingdomData>
{
    public List<City> cities;
    public City capital;

    // Для тестов: кто сидит на троне после setKing.
    public Actor king;

    // Реально: public void setKing(Actor pActor, bool pFromLoad = false);
    public void setKing(Actor pActor, bool pFromLoad = false)
    {
        king = pActor;
    }
}

// Реально: City : MetaObject<CityData> { public Kingdom kingdom; public Actor leader;
//   public int getLoyalty(bool pForceRecalc = false);
//   public Kingdom makeOwnKingdom(Actor pActor, bool pRebellion = false, bool pFellApart = false);
//   public void setKingdom(Kingdom pKingdom, bool pFromLoad = false);
//   public void joinAnotherKingdom(Kingdom pNewSetKingdom, bool pCaptured = false, bool pRebellion = false); ... }
public class City : MetaObject<CityData>
{
    public Kingdom kingdom;

    public Actor leader;

    // Реально: public void setLeader(Actor pActor, bool pNew).
    public void setLeader(Actor pActor, bool pNew)
    {
        if (pActor != null)
        {
            leader = pActor;
        }
    }

    public int getLoyalty(bool pForceRecalc = false) => 0;

    // Поведение для тестов: отражает игру — город выходит из старого королевства
    // и образует новое через KingdomManager (если менеджера нет — сепарация не удалась).
    public Kingdom makeOwnKingdom(Actor pActor, bool pRebellion = false, bool pFellApart = false)
    {
        if (pActor == null)
        {
            // Как в игре: makeNewCivKingdom(null) -> NullReferenceException.
            throw new System.NullReferenceException("makeOwnKingdom: leader is null");
        }
        if (World.world == null || World.world.kingdoms == null || World.world.kingdoms.list == null)
        {
            return null;
        }
        // Реальный менеджер назначает новому королевству id при регистрации.
        Kingdom k = new Kingdom { id = 1000 + World.world.kingdoms.list.Count, cities = new List<City>() };
        World.world.kingdoms.list.Add(k);
        if (kingdom != null && kingdom.cities != null)
        {
            kingdom.cities.Remove(this);
        }
        kingdom = k;
        k.cities.Add(this);
        return k;
    }

    public void setKingdom(Kingdom pKingdom, bool pFromLoad = false)
    {
        if (kingdom != null && kingdom.cities != null)
        {
            kingdom.cities.Remove(this);
        }
        kingdom = pKingdom;
        if (pKingdom != null && pKingdom.cities != null)
        {
            pKingdom.cities.Add(this);
        }
    }

    public void joinAnotherKingdom(Kingdom pNewSetKingdom, bool pCaptured = false, bool pRebellion = false)
    {
        setKingdom(pNewSetKingdom, false);
    }
}

// ---- Войны ----
// Реально: public enum WarWinner { Nobody, Attackers, Defenders, Peace, Merged }
public enum WarWinner
{
    Nobody,
    Attackers,
    Defenders,
    Peace,
    Merged
}

// Реально: WarTypeAsset : Asset { public string name_template; public bool total_war; ... }
public class WarTypeAsset
{
    public string name_template = "war_conquest";
    public bool total_war;
}

// Реально: WarTypeLibrary — статические ассеты типов войн.
public static class WarTypeLibrary
{
    public static WarTypeAsset normal = new WarTypeAsset { name_template = "war_conquest" };
    public static WarTypeAsset rebellion = new WarTypeAsset { name_template = "war_rebellion" };
    public static WarTypeAsset inspire = new WarTypeAsset { name_template = "war_inspire" };
}

// Реально: DiplomacyManager { public War startWar(Kingdom pAttacker, Kingdom pDefender, WarTypeAsset pAsset, bool pLog = true); ... }
public class DiplomacyManager
{
    // Для тестов: журнал начатых войн.
    public readonly List<War> startedWars = new List<War>();

    public War startWar(Kingdom pAttacker, Kingdom pDefender, WarTypeAsset pAsset, bool pLog = true)
    {
        War w = new War();
        startedWars.Add(w);
        return w;
    }
}

// Реально: WarData (наследует BaseSystemData)
public class WarData : BaseSystemData { }

// Реально: War : MetaObject<WarData> { public Kingdom getMainAttacker(); public Kingdom getMainDefender(); ... }
public class War : MetaObject<WarData>
{
    public Kingdom getMainAttacker() => null;
    public Kingdom getMainDefender() => null;
}

// Реально: WarManager : SystemManager<War, WarData>
// { public War newWar(Kingdom, Kingdom, WarTypeAsset); public void endWar(War, WarWinner pWinner = WarWinner.Nobody); ... }
public class WarManager : CoreSystemManager<War, WarData>
{
    public War newWar(Kingdom pAttacker, Kingdom pDefender, WarTypeAsset pType) => null;
    public void endWar(War pWar, WarWinner pWinner = WarWinner.Nobody) { }
}

// ---- Менеджеры ----
// Реально: CoreSystemManager<TObject,TData> : SystemManager<...> { public readonly List<TObject> list; }
public class CoreSystemManager<TObject, TData>
{
    public List<TObject> list;
}

// Реально: KingdomManager : MetaSystemManager<Kingdom, KingdomData>
public class KingdomManager : CoreSystemManager<Kingdom, KingdomData> { }

// Реально: CityManager : MetaSystemManager<City, CityData>
public class CityManager : CoreSystemManager<City, CityData> { }

// ---- Карта и мир ----
// Реально: MapStats { public int history_current_year = -1; }
public class MapStats
{
    public int history_current_year;
}

// Реально: ActorManager : SimSystemManager<Actor, ActorData> { public List<Actor> getSimpleList(); }
public class ActorManager
{
    public List<Actor> getSimpleList() => null;
}

// Реально: MapBox : MonoBehaviour { public MapStats map_stats; public KingdomManager kingdoms; public CityManager cities; public ActorManager units; public static MapBox instance; }
public class MapBox
{
    public MapStats map_stats;
    public KingdomManager kingdoms;
    public CityManager cities;
    public ActorManager units;
    public DiplomacyManager diplomacy;
    public object alliances;
    public object world_laws;

    public static MapBox instance;

    // Реально: public double getCurWorldTime() — игровые часы мира (60 единиц = 1 год).
    // Test/demo clock only. The mod calls the real method, never this test field.
    public double TestWorldTime;
    public double getCurWorldTime() => TestWorldTime;
}

// Реально: static class World { public static MapBox world { get { return MapBox.instance; } } }
public static class World
{
    public static MapBox world => instance_holder();

    private static MapBox instance_holder() => MapBox.instance;
}

// Minimal path-resolution surface verified in the native API dump.
public static class SaveManager
{
    public static string TestResolvedFolder;
    public static string folderPath(string slot) => TestResolvedFolder;
    public static string getSavePath(string slot) => TestResolvedFolder;
}

// ---- История мира и уведомления (только рефлексия мода и тестов) ----
// В настоящей игре WorldLogAsset : Asset, а WorldLogLibrary — AssetLibrary<WorldLogAsset>.
// Мод не ссылается на эти типы напрямую: он ищет поля id/path_icon и методы has/get/add
// по именам. Заглушка повторяет именно эту наблюдаемую поверхность, чтобы тесты могли
// проверить создание отдельного типа записи с собственной иконкой без запуска WorldBox.
public class Asset
{
    public string id;
    public int _hashcode;
    public int _index;

    public virtual void create() { }
    public void setHash(int value) => _hashcode = value;
    public void setIndexID(int value) => _index = value;
}

public delegate string WorldLogTextFormatter(WorldLogMessage message, string text);

public class WorldLogAsset : Asset
{
    public string group;
    public string locale_id;
    public string path_icon;
    public int color;
    public int random_ids;
    public WorldLogTextFormatter text_replacer;
}

public class WorldLogLibrary
{
    private readonly Dictionary<string, WorldLogAsset> dict = new Dictionary<string, WorldLogAsset>();

    public readonly List<WorldLogAsset> list = new List<WorldLogAsset>();
    public WorldLogAsset t;
    public static WorldLogAsset auto_tester;

    public WorldLogLibrary()
    {
        auto_tester = new WorldLogAsset
        {
            id = "auto_tester",
            path_icon = "ui/Icons/iconPlay",
            color = 7,
            text_replacer = (message, text) => message.special1
        };
        dict[auto_tester.id] = auto_tester;
        list.Add(auto_tester);
        t = auto_tester;
    }

    public bool has(string assetId) => assetId != null && dict.ContainsKey(assetId);

    public WorldLogAsset get(string assetId)
    {
        WorldLogAsset asset;
        return assetId != null && dict.TryGetValue(assetId, out asset) ? asset : null;
    }

    public WorldLogAsset add(WorldLogAsset asset)
    {
        list.RemoveAll(existing => existing.id == asset.id);
        dict[asset.id] = asset;
        list.Add(asset);
        t = asset;
        return asset;
    }
}

public static class AssetManager
{
    public static WorldLogLibrary world_log_library = new WorldLogLibrary();
}

public class WorldLogMessage
{
    public string asset_id { get; set; }
    public string special1 { get; set; }
    public string special2 { get; set; }
    public string special3 { get; set; }

    public WorldLogMessage() { }

    public WorldLogMessage(WorldLogAsset asset, string pSpecial1, string pSpecial2, string pSpecial3)
    {
        asset_id = asset?.id;
        special1 = pSpecial1;
        special2 = pSpecial2;
        special3 = pSpecial3;
    }
}

public static class WorldLogMessageExtensions
{
    public static readonly List<WorldLogMessage> added = new List<WorldLogMessage>();

    public static void add(WorldLogMessage message) => added.Add(message);

    public static WorldLogAsset getAsset(WorldLogMessage message)
    {
        return message == null ? null : AssetManager.world_log_library.get(message.asset_id);
    }
}
