using System;
using System.Collections;
using System.Collections.Generic;
using NeoModLoader.services;
using RealPolitic.Core;

namespace RealPolitic;

/// <summary>
/// 0.23.2: наблюдатель за событиями самого мира, которые игра не объясняет, а мод — объясняет.
///  • смена эпохи (World.world_era / era_manager.getCurrentAge()) — с кратким описанием, что она несёт;
///  • вымирание подвида (World.world.subspecies: живых юнитов стало 0);
///  • династический раскол — родственник правителя основал собственное государство
///    (сверяем королей новых государств с реестром родословных).
/// Вызывается раз в игровой год из WorldFeeder.Tick; все обращения к игре — через рефлексию,
/// любая ошибка глушится и логируется один раз в год.
/// </summary>
public sealed class WorldEventsWatcher
{
	private readonly PoliticalWorld _world;

	private string _lastEraId;

	/// <summary>Подвиды, виденные живыми: id → (имя, вид).</summary>
	private readonly Dictionary<object, KeyValuePair<string, string>> _aliveSubspecies = new Dictionary<object, KeyValuePair<string, string>>();

	/// <summary>Имя подвида → год последнего объявления о вымирании (игра пересоздаёт подвиды с тем же именем).</summary>
	private readonly Dictionary<string, int> _extinctAnnounced = new Dictionary<string, int>(StringComparer.Ordinal);

	/// <summary>Государства, для которых уже проверяли основателя.</summary>
	private readonly HashSet<int> _knownKingdoms = new HashSet<int>();

	private bool _baseline;

	private int _lastErrorYear = int.MinValue;

	public WorldEventsWatcher(PoliticalWorld world)
	{
		_world = world;
	}

	public void Reset()
	{
		_lastEraId = null;
		_aliveSubspecies.Clear();
		_extinctAnnounced.Clear();
		_knownKingdoms.Clear();
		_baseline = false;
	}

	public void Tick(MapBox map, int year)
	{
		try
		{
			TickEra(map, year);
			TickSubspecies(map, year);
			TickRoyalSecessions(map, year);
			// Базовая линия — только когда мир реально заселён: на первом кадре загрузки готовой
			// карты список держав пуст, и без этой проверки все 36 стран Европы объявлялись
			// «новыми», а их правители — «отделившимися родственниками» друг друга.
			List<Kingdom> kingdoms = GameRefs.KingdomList(map);
			if (kingdoms != null && kingdoms.Count > 0)
			{
				_baseline = true;
			}
		}
		catch (Exception ex)
		{
			if (_lastErrorYear != year)
			{
				_lastErrorYear = year;
				LogService.LogError("[Real Politic] наблюдатель мира: " + ex.Message);
			}
		}
	}

	// ---------------------------------------------------------------- эпохи

	private void TickEra(MapBox map, int year)
	{
		object era = GameRefs.ReadMember(map, "era_manager");
		object age = era != null ? GameRefs.Invoke(era, "getCurrentAge") : null;
		if (age == null)
		{
			Type worldType = Patches.FindGameType("World");
			age = worldType?.GetProperty("world_era", System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.Public)?.GetValue(null, null);
		}
		string id = (GameRefs.ReadMember(age, "id") as string) ?? string.Empty;
		if (string.IsNullOrEmpty(id))
		{
			return;
		}
		if (_lastEraId == null)
		{
			_lastEraId = id;
			return;
		}
		if (id == _lastEraId)
		{
			return;
		}
		_lastEraId = id;
		string key = EraKey(id);
		string name = GameText.Localized("RP_ERA_" + key, id);
		string effect = GameText.Localized("RP_ERA_" + key + "_EFFECT", string.Empty);
		_world.Bus.Publish(new PolEvent(PolEventType.EraChanged, "RP_EV_ERA_CHANGED")
		{
			Year = year,
			Severity = Severity.Major
		}.With("era", name).With("era_effect", effect));
	}

	/// <summary>age_hope → HOPE; age_dark_age → DARK и т. п.</summary>
	public static string EraKey(string id)
	{
		string s = (id ?? string.Empty).ToLowerInvariant();
		if (s.StartsWith("age_", StringComparison.Ordinal))
		{
			s = s.Substring(4);
		}
		if (s.Contains("hope")) return "HOPE";
		if (s.Contains("sun")) return "SUN";
		if (s.Contains("dark")) return "DARK";
		if (s.Contains("tear")) return "TEARS";
		if (s.Contains("moon")) return "MOON";
		if (s.Contains("chaos")) return "CHAOS";
		if (s.Contains("wonder")) return "WONDERS";
		if (s.Contains("ice")) return "ICE";
		if (s.Contains("ash")) return "ASH";
		if (s.Contains("despair")) return "DESPAIR";
		return s.ToUpperInvariant();
	}

	// ---------------------------------------------------------------- подвиды

	private void TickSubspecies(MapBox map, int year)
	{
		IEnumerable manager = GameRefs.ReadMember(map, "subspecies") as IEnumerable;
		if (manager == null)
		{
			return;
		}
		HashSet<object> seenAlive = new HashSet<object>();
		foreach (object sub in manager)
		{
			if (sub == null)
			{
				continue;
			}
			int alive = CountAlive(sub);
			if (alive > 0)
			{
				seenAlive.Add(sub);
				if (!_aliveSubspecies.ContainsKey(sub))
				{
					_aliveSubspecies[sub] = new KeyValuePair<string, string>(SubspeciesName(sub), SpeciesName(sub));
				}
			}
		}
		if (!_baseline)
		{
			return;
		}
		List<object> gone = new List<object>();
		foreach (KeyValuePair<object, KeyValuePair<string, string>> pair in _aliveSubspecies)
		{
			if (!seenAlive.Contains(pair.Key))
			{
				gone.Add(pair.Key);
			}
		}
		for (int i = 0; i < gone.Count; i++)
		{
			KeyValuePair<string, string> info = _aliveSubspecies[gone[i]];
			_aliveSubspecies.Remove(gone[i]);
			if (string.IsNullOrEmpty(info.Key))
			{
				continue;
			}
			// Одно и то же имя («Пингвин Императорский») вымирало по 16 раз: игра плодит
			// одноимённые подвиды при каждом спавне. Повтор объявляем не чаще раза в 50 лет.
			if (_extinctAnnounced.TryGetValue(info.Key, out int lastYear) && year - lastYear < 50)
			{
				continue;
			}
			_extinctAnnounced[info.Key] = year;
			_world.Bus.Publish(new PolEvent(PolEventType.SubspeciesExtinct, "RP_EV_SUBSPECIES_EXTINCT")
			{
				Year = year,
				Severity = Severity.Major
			}.With("species", info.Key).With("race", info.Value));
		}
	}

	private static int CountAlive(object sub)
	{
		object count = GameRefs.Invoke(sub, "countAlive") ?? GameRefs.Invoke(sub, "countUnits");
		if (count is int n)
		{
			return n;
		}
		IEnumerable units = GameRefs.ReadMember(sub, "units") as IEnumerable;
		if (units == null)
		{
			return -1;
		}
		int total = 0;
		foreach (object u in units)
		{
			if (u != null && !(GameRefs.Invoke(u, "isAlive") is bool alive && !alive))
			{
				total++;
			}
		}
		return total;
	}

	private static string SubspeciesName(object sub)
	{
		return (GameRefs.ReadMember(sub, "name") ?? GameRefs.ReadMember(GameRefs.ReadMember(sub, "data"), "name"))?.ToString() ?? string.Empty;
	}

	private static string SpeciesName(object sub)
	{
		// build 719: Subspecies.getActorAsset() (наследуется от MetaObjectWithTraits).
		object asset = GameRefs.Invoke(sub, "getActorAsset") ?? GameRefs.Invoke(sub, "getCoreAsset") ?? GameRefs.ReadMember(sub, "base_asset");
		object data = GameRefs.ReadMember(sub, "data");
		string id = (GameRefs.ReadMember(asset, "id")
			?? GameRefs.ReadMember(data, "base_asset_id")
			?? GameRefs.ReadMember(sub, "base_asset_id")
			?? GameRefs.ReadMember(sub, "species_id"))?.ToString() ?? string.Empty;
		if (string.IsNullOrEmpty(id))
		{
			// Последний шанс — имя самого подвида без эпитета («Пингвин Императорский» → «Пингвин»).
			string own = SubspeciesName(sub);
			int space = own.IndexOf(' ');
			return space > 0 ? own.Substring(0, space) : own;
		}
		// Локализованное имя существа: сначала ключ ассета (getLocaleID), затем сам id.
		string key = GameRefs.Invoke(asset, "getLocaleID")?.ToString() ?? id;
		string loc = GameText.Localized(key, string.Empty);
		if (string.IsNullOrEmpty(loc))
		{
			loc = GameText.Localized(id, string.Empty);
		}
		if (string.IsNullOrEmpty(loc))
		{
			loc = GameText.Localized("actor_" + id, string.Empty);
		}
		return string.IsNullOrEmpty(loc) ? id : loc;
	}

	// ---------------------------------------------------------------- династические расколы

	private void TickRoyalSecessions(MapBox map, int year)
	{
		List<Kingdom> list = GameRefs.KingdomList(map);
		if (list == null)
		{
			return;
		}
		for (int i = 0; i < list.Count; i++)
		{
			Kingdom k = list[i];
			if (k == null)
			{
				continue;
			}
			int id = (int)((NanoObject)k).id;
			if (!_knownKingdoms.Add(id) || !_baseline)
			{
				continue;
			}
			// Если государство уже зарегистрировано в ядре (создано модом через освобождение,
			// мирное восстановление, референдум о выходе или гражданскую войну), это не спонтанный
			// ванильный династический раскол.
			if (_world.GetKingdom(id) != null)
			{
				continue;
			}
			Actor king = GameRefs.ReadRuler(k);
			if (king?.data == null)
			{
				continue;
			}
			long kingId = ((BaseSystemData)king.data).id;
			if (!_world.Family.TryGet(kingId, out FamilyRecord rec))
			{
				continue;
			}
			// Ищем правящего родственника в других монархиях: родитель, брат/сестра, ребёнок.
			foreach (KingdomPolitics other in _world.Kingdoms)
			{
				if (other.KingdomId == id || other.Ruler == null
					|| (other.Ideology != IdeologyType.Monarchy && other.Ideology != IdeologyType.Empire && other.Ideology != IdeologyType.Khaganate))
				{
					continue;
				}
				string relation = Relation(rec, other.Ruler.ActorId);
				if (relation == null)
				{
					continue;
				}
				_world.Bus.Publish(new PolEvent(PolEventType.RoyalRelativeSeceded, "RP_EV_ROYAL_RELATIVE_SECEDED")
				{
					Year = year,
					Severity = Severity.Major,
					KingdomId = other.KingdomId
				}.With("relative", rec.Name)
					.With("relation", GameText.Localized("RP_REL_" + relation + (rec.Female ? "_F" : "_M"), relation))
					.With("kingdom", _world.KingdomNameOf(other.KingdomId))
					.With("ruler", other.Ruler.Name ?? string.Empty)
					.With("new_kingdom", ((NanoObject)k).name ?? string.Empty));
				break;
			}
		}
	}

	/// <summary>SIBLING / CHILD / PARENT, если житель rec — родственник жителя otherId; иначе null.</summary>
	private string Relation(FamilyRecord rec, long otherId)
	{
		if (otherId == 0 || rec == null)
		{
			return null;
		}
		if (rec.FatherId == otherId || rec.MotherId == otherId)
		{
			return "CHILD";
		}
		if (_world.Family.TryGet(otherId, out FamilyRecord other))
		{
			if (other.FatherId == rec.Id || other.MotherId == rec.Id)
			{
				return "PARENT";
			}
			bool sameFather = rec.FatherId != 0 && rec.FatherId == other.FatherId;
			bool sameMother = rec.MotherId != 0 && rec.MotherId == other.MotherId;
			if (sameFather || sameMother)
			{
				return "SIBLING";
			}
		}
		return null;
	}
}
