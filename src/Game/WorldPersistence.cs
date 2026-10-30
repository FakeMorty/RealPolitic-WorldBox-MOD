using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using HarmonyLib;
using NeoModLoader.services;
using RealPolitic.Core;

namespace RealPolitic;

/// <summary>
/// Игровой слой сохранения и загрузки политического состояния мира (Этап 10).
///
/// Сохраняет политическое состояние (идеологии, унии, причины смерти, механики строев)
/// в слот сохранения игры (если путь известен) и в резервный файл автосохранения.
/// Поддерживает безопасный Harmony-перехват SaveManager/MapBox с graceful degradation.
/// </summary>
public static class WorldPersistence
{
	private const string StateFileName = "realpolitic_state.json";

	private static string _lastSavedPath = string.Empty;

	private static string _lastLoadedPath = string.Empty;
    private static MapBox _loadedMap;
    private static string _lastSerializedPath;
    private static string _lastSerializedState;
    internal static bool ConsumeLoadedStateFor(MapBox map)
    {
        MapBox loaded=_loadedMap;_loadedMap=null;
        return map!=null && ReferenceEquals(map,loaded);
    }

	public static string LastSavedPath => _lastSavedPath;

	public static string LastLoadedPath => _lastLoadedPath;

	/// <summary>Установка Harmony-хуков сохранения и загрузки.</summary>
	public static int Install(Harmony harmony)
	{
		if (harmony == null)
		{
			return 0;
		}
        return NativeWorldLifecycle.Install(harmony);
	}

	private static int PatchSaveMethods(Harmony harmony)
	{
		try
		{
			Type type = Patches.FindGameType("SaveManager");
			if (type == null)
			{
				LogService.LogInfo("[Real Politic] SaveManager не найден — сохранение работает через автосохранение");
				return 0;
			}
			MethodInfo[] methods = type.GetMethods(BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
			for (int i = 0; i < methods.Length; i++)
			{
				MethodInfo m = methods[i];
				if (m.Name.IndexOf("save", StringComparison.OrdinalIgnoreCase) >= 0)
				{
					try
					{
						harmony.Patch(m, null, new HarmonyMethod(typeof(WorldPersistence), nameof(SavePostfix), (Type[])null), null, null, null);
						LogService.LogInfo("[Real Politic] установлен хук сохранения на SaveManager." + m.Name);
						return 1;
					}
					catch (Exception ex)
					{
						LogService.LogInfo("[Real Politic] не удалось пропатчить SaveManager." + m.Name + ": " + ex.Message);
					}
				}
			}
		}
		catch (Exception ex)
		{
			LogService.LogError("[Real Politic] ошибка поиска методов сохранения: " + ex.Message);
		}
		return 0;
	}

	private static int PatchLoadMethods(Harmony harmony)
	{
		try
		{
			Type type = Patches.FindGameType("SaveManager");
			if (type == null)
			{
				return 0;
			}
			MethodInfo[] methods = type.GetMethods(BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
			for (int i = 0; i < methods.Length; i++)
			{
				MethodInfo m = methods[i];
				if (m.Name.IndexOf("load", StringComparison.OrdinalIgnoreCase) >= 0)
				{
					try
					{
						harmony.Patch(m, null, new HarmonyMethod(typeof(WorldPersistence), nameof(LoadPostfix), (Type[])null), null, null, null);
						LogService.LogInfo("[Real Politic] установлен хук загрузки на SaveManager." + m.Name);
						return 1;
					}
					catch (Exception ex)
					{
						LogService.LogInfo("[Real Politic] не удалось пропатчить SaveManager." + m.Name + ": " + ex.Message);
					}
				}
			}
		}
		catch (Exception ex)
		{
			LogService.LogError("[Real Politic] ошибка поиска методов загрузки: " + ex.Message);
		}
		return 0;
	}

	private static int _lastSavedYear = -1;

	/// <summary>Новый мир: год нового мира может совпасть с последним сохранённым годом старого.</summary>
	public static void ResetSaveThrottle()
	{
		_lastSavedYear = -1;
        _lastSerializedPath=null; _lastSerializedState=null;
	}

	private static void SavePostfix(object[] __args)
	{
		try
		{
			// Этап 25 (тишина логов): игра дергает автосохранение очень часто,
			// в том числе на паузе; дубли одного года лишены смысла и забивают лог.
			if (RealPoliticMod.World?.Governance.Enabled != true && (IsWorldPaused() || WorldYear() == _lastSavedYear))
			{
				return;
			}
			string folder = ExtractFolder(__args);
			if (Save(folder))
			{
				_lastSavedYear = WorldYear();
			}
		}
		catch (Exception ex)
		{
			LogService.LogError("[Real Politic] ошибка в постфиксе сохранения: " + ex.Message);
		}
	}

	private static int WorldYear()
	{
		// Те же часы, что и у годового цикла мода: иначе (map_stats.year в игре нет)
		// год всегда был -1, и дроссель «раз в год» никогда не срабатывал.
		int year = RealPoliticMod.CurrentYear();
		if (year >= 0)
		{
			return year;
		}
		try
		{
			MapBox w = World.world;
			if (w != null)
			{
				object stats = GameRefs.ReadMember(w, "map_stats");
				object y = GameRefs.ReadMember(stats, "history_current_year") ?? GameRefs.ReadMember(stats, "year");
				if (y != null)
				{
					return Convert.ToInt32(y);
				}
			}
		}
		catch (Exception) { }
		return -1;
	}

	private static bool IsWorldPaused()
	{
		try
		{
			object w = World.world;
			object result = GameRefs.Invoke(w, "isPaused");
			if (result is bool b) return b;
			object speed = GameRefs.ReadMember(w, "speed");
			if (speed != null)
			{
				int val = Convert.ToInt32(speed);
				return val == 0;
			}
		}
		catch (Exception) { }
		return false;
	}

	private static void LoadPostfix(object[] __args)
	{
		try
		{
			string folder = ExtractFolder(__args);
			Load(folder);
		}
		catch (Exception ex)
		{
			LogService.LogError("[Real Politic] ошибка в постфиксе загрузки: " + ex.Message);
		}
	}

	private static string ExtractFolder(object[] args)
	{
		if (args == null || args.Length == 0)
		{
			return null;
		}
		for (int i = 0; i < args.Length; i++)
		{
			if (args[i] is string str && !string.IsNullOrEmpty(str))
			{
				if (str.Contains("/") || str.Contains("\\") || str.StartsWith("save", StringComparison.OrdinalIgnoreCase))
				{
					return str;
				}
			}
		}
		return null;
	}

	/// <summary>Сохранение состояния текущего мира в файл.</summary>
	public static bool Save(string targetFolder = null)
	{
		try
		{
			PoliticalWorld world = RealPoliticMod.World;
			if (world == null)
			{
				return false;
			}
			string path = ResolveSavePath(targetFolder);
			string dir = Path.GetDirectoryName(path);
			if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
			{
				Directory.CreateDirectory(dir);
			}
			string json = SaveData.Write(world);
            if(_lastSerializedPath==path && _lastSerializedState==json && File.Exists(path))return true;
			string tempPath = path + ".tmp";
			File.WriteAllText(tempPath, json);
            if (File.Exists(path)) File.Replace(tempPath,path,null);
            else File.Move(tempPath,path);
            _lastSerializedPath=path; _lastSerializedState=json;
			_lastSavedPath = path;
			LogService.LogInfo("[Real Politic] политическое состояние сохранено: " + path + " (" + json.Length + " байт)");
			return true;
		}
		catch (Exception ex)
		{
			LogService.LogError("[Real Politic] ошибка сохранения состояния мира: " + ex.Message);
			return false;
		}
	}

	/// <summary>Загрузка политического состояния мира из файла.</summary>
	public static bool Load(string targetFolder = null)
	{
		try
		{
			PoliticalWorld world = RealPoliticMod.World;
			if (world == null)
			{
				return false;
			}
			string path = ResolveLoadPath(targetFolder);
			if (string.IsNullOrEmpty(path) || !File.Exists(path))
			{
				return false;
			}
			string json = File.ReadAllText(path);
			if (string.IsNullOrWhiteSpace(json))
			{
				return false;
			}
			var root = TinyJson.ParseStrict(json);
            if(root.Kind!=TinyJson.TokenKind.Object) return false;
            GovernanceState incoming = GovernanceCodec.Read(root.Get("governance26"));
            if(incoming.Enabled && string.IsNullOrEmpty(targetFolder))
            {
                LogService.LogError("[Real Politic] состояние нового мира не загружено из общего автосохранения: принадлежность текущему миру не подтверждена");
                return false;
            }
            List<Union> unions = SaveData.Apply(world, json);
            ActorIdeologies.Clear();
            _loadedMap=World.world;
            RealPoliticMod.Chronicle?.BeginObservation();
			_lastLoadedPath = path;
			LogService.LogInfo("[Real Politic] политическое состояние загружено: " + path + " (год " + world.Year + ", держав: " + world.Kingdoms.Count + ", уний: " + unions.Count + ")");
			return true;
		}
		catch (Exception ex)
		{
			LogService.LogError("[Real Politic] ошибка загрузки состояния мира: " + ex.Message);
			return false;
		}
	}

	public static string ResolveSavePath(string targetFolder)
	{
		if (!string.IsNullOrEmpty(targetFolder))
		{
			string resolved = NativeCheckpointBinding.ResolveSaveDirectory(targetFolder);
			if (!string.IsNullOrEmpty(resolved))return Path.Combine(resolved, StateFileName);
			if (targetFolder.Contains("/") || targetFolder.Contains("\\"))return Path.Combine(targetFolder, StateFileName);
		}
		return GetAutosavePath();
	}

	public static string ResolveLoadPath(string targetFolder)
	{
		if (!string.IsNullOrEmpty(targetFolder))
		{
			string resolved = NativeCheckpointBinding.ResolveSaveDirectory(targetFolder);
			string candidate = Path.Combine(resolved??targetFolder, StateFileName);
            return File.Exists(candidate) ? candidate : null;
        }
        string autosave = GetAutosavePath();
		if (File.Exists(autosave))
		{
			return autosave;
		}
		return null;
	}

	public static string GetAutosavePath()
	{
		try
		{
			if (!string.IsNullOrEmpty(RealPoliticMod.ModFolder))
			{
				return Path.Combine(RealPoliticMod.ModFolder, "saves", StateFileName);
			}
		}
		catch (Exception)
		{
		}
		try
		{
			string docs = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
			if (!string.IsNullOrEmpty(docs))
			{
				return Path.Combine(docs, "RealPolitic", "saves", StateFileName);
			}
		}
		catch (Exception)
		{
		}
		return Path.Combine(Directory.GetCurrentDirectory(), "saves", StateFileName);
	}
}
