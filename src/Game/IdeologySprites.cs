using System;
using System.IO;
using System.Reflection;
using NeoModLoader.services;
using RealPolitic.Core;

namespace RealPolitic;

/// <summary>
/// Загружает пиксельные иконки идеологий в спрайт-кэш игры
/// через SpriteTextureLoader.addSprite("ui/Icons/rp_ideology_*", bytes), чтобы их можно
/// было показывать в строке информации юнита рядом с религией и культурой.
///
/// Источник иконки: сначала встроенный ресурс DLL (RealPolitic.icons.<ключ>.png),
/// при неудаче — {папка мода}/icons/<ключ>.png, затем {папка мода}/icons/64/<ключ>.png.
/// </summary>
public static class IdeologySprites
{
	private static bool _registered;

	public static void Register()
	{
		if (_registered)
		{
			return;
		}
		_registered = true;
		Assembly asm = typeof(IdeologySprites).Assembly;
		Array ideologyTypes = Enum.GetValues(typeof(IdeologyType));
		int loaded = 0;
		int verified = 0;
		try
		{
			// Перебираем сам enum, а не отдельный ручной список: так новая идеология
			// не сможет снова остаться без предварительной загрузки своей иконки.
			for (int i = 0; i < ideologyTypes.Length; i++)
			{
				IdeologyType type = (IdeologyType)ideologyTypes.GetValue(i);
				string key = type.ToString().ToLowerInvariant();
				if (EnsureRegistered(asm, type, out bool added, out bool inCache))
				{
					if (added)
					{
						loaded++;
					}
					if (inCache)
					{
						verified++;
					}
					else
					{
						LogService.LogError("[Real Politic] спрайт не создан из байтов для: " + key);
					}
				}
				else
				{
					LogService.LogError("[Real Politic] иконка идеологии не найдена: " + key);
				}
			}
			LogService.LogInfo("[Real Politic] иконки идеологий: передано " + loaded + "/" + ideologyTypes.Length + ", спрайтов в кэше " + verified + "/" + ideologyTypes.Length + " (папка мода: " + (string.IsNullOrEmpty(RealPoliticMod.ModFolder) ? "н/д" : RealPoliticMod.ModFolder) + ")");
			ModIcons.RegisterUiIcons();
			ModIcons.RegisterNotificationIcons();
		}
		catch (Exception ex)
		{
			LogService.LogError("[Real Politic] не удалось загрузить иконки идеологий: " + ex);
		}
	}

	/// <summary>
	/// Гарантирует, что иконка идеологии есть в спрайт-кэше игры под путём
	/// "ui/Icons/rp_ideology_*" — тем самым путём, по которому showStatRow её ищет.
	/// Вызывается и при старте для всех идеологий enum, и по требованию перед показом строки.
	/// </summary>
	public static void EnsureRegistered(IdeologyType type)
	{
		EnsureRegistered(typeof(IdeologySprites).Assembly, type, out _, out _);
	}

	private static bool EnsureRegistered(Assembly asm, IdeologyType type, out bool added, out bool inCache)
	{
		return EnsureRegistered(asm, type.ToString().ToLowerInvariant(), "ui/Icons/" + Ideologies.IconKey(type), out added, out inCache);
	}

	/// <summary>0.23.2: произвольная иконка мода (файл icons/&lt;key&gt;.png) под путём спрайт-кэша игры.
	/// Используется кнопками боковой панели (конституция, родословная).</summary>
	public static bool EnsureRegistered(string key, string pathId)
	{
		return EnsureRegistered(typeof(IdeologySprites).Assembly, key, pathId, out _, out _);
	}

	private static bool EnsureRegistered(Assembly asm, string key, string pathId, out bool added, out bool inCache)
	{
		added = false;
		inCache = false;
		try
		{
			Type spriteLoader = Patches.FindGameType("SpriteTextureLoader");
			if (spriteLoader == null)
			{
				LogService.LogError("[Real Politic] SpriteTextureLoader не найден — иконки идеологий не загружены");
				return false;
			}
			MethodInfo getSprite = spriteLoader.GetMethod("getSprite", BindingFlags.Static | BindingFlags.Public);
			object existing = getSprite?.Invoke(null, new object[1] { pathId });
			if (existing != null)
			{
				inCache = true;
				return true;
			}
			// ВАЖНО (причина «передано 0/9» в v0.6.0): getSprite при промахе делает
			// Resources.Load(...) и кладёт в _cached_sprites[pPath] = null. После этого
			// addSprite() вызывает Dictionary.Add с уже существующим ключом → ArgumentException,
			// спрайт молча теряется. Поэтому сначала убираем null-заглушку из кэша.
			RemoveNullCacheEntry(spriteLoader, pathId);
			MethodInfo addSprite = spriteLoader.GetMethod("addSprite", BindingFlags.Static | BindingFlags.Public);
			if (addSprite == null)
			{
				LogService.LogError("[Real Politic] SpriteTextureLoader.addSprite не найден — иконки идеологий не загружены");
				return false;
			}
			byte[] bytes = ReadResource(asm, "RealPolitic.icons." + key + ".png");
			if (bytes == null || bytes.Length == 0)
			{
				// Запасной путь: файл в папке мода.
				bytes = ReadFromFolder(key);
			}
			if (bytes == null || bytes.Length == 0)
			{
				return false;
			}
			try
			{
				addSprite.Invoke(null, new object[2] { pathId, bytes });
			}
			catch (Exception ex)
			{
				// Ключ мог появиться между проверкой и вставкой — пробуем ещё раз, очистив кэш.
				LogService.LogError("[Real Politic] addSprite(" + pathId + ") не прошёл: " + (ex.InnerException?.Message ?? ex.Message));
				RemoveNullCacheEntry(spriteLoader, pathId);
				try
				{
					addSprite.Invoke(null, new object[2] { pathId, bytes });
				}
				catch (Exception)
				{
				}
			}
			added = true;
			object sprite = getSprite?.Invoke(null, new object[1] { pathId });
			inCache = sprite != null;
			return inCache;
		}
		catch (Exception ex)
		{
			LogService.LogError("[Real Politic] не удалось зарегистрировать иконку " + key + ": " + ex.Message);
			return false;
		}
	}

	/// <summary>
	/// Убирает из SpriteTextureLoader._cached_sprites запись с null-спрайтом по данному пути.
	/// Без этого повторный addSprite() падает на Dictionary.Add (ключ уже занят null-ом),
	/// и иконка никогда не попадает в кэш.
	/// </summary>
	private static void RemoveNullCacheEntry(Type spriteLoader, string pathId)
	{
		try
		{
			FieldInfo cacheField = spriteLoader.GetField("_cached_sprites", BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
			object cache = cacheField?.GetValue(null);
			if (cache == null)
			{
				return;
			}
			MethodInfo remove = cache.GetType().GetMethod("Remove", new Type[1] { typeof(string) });
			remove?.Invoke(cache, new object[1] { pathId });
		}
		catch (Exception)
		{
		}
	}

	private static byte[] ReadResource(Assembly asm, string name)
	{
		try
		{
			using (Stream stream = asm.GetManifestResourceStream(name))
			{
				if (stream == null)
				{
					return null;
				}
				using (MemoryStream ms = new MemoryStream())
				{
					stream.CopyTo(ms);
					return ms.ToArray();
				}
			}
		}
		catch (Exception)
		{
			return null;
		}
	}

	private static byte[] ReadFromFolder(string key)
	{
		try
		{
			string folder = RealPoliticMod.ModFolder;
			if (string.IsNullOrEmpty(folder))
			{
				return null;
			}
			string path = Path.Combine(folder, "icons", key + ".png");
			if (!File.Exists(path))
			{
				// Встроенный в DLL набор исторически собирается из icons/64;
				// проверяем и эту папку, чтобы один отсутствующий дубль не лишал кнопку картинки.
				path = Path.Combine(folder, "icons", "64", key + ".png");
			}
			if (!File.Exists(path))
			{
				return null;
			}
			return File.ReadAllBytes(path);
		}
		catch (Exception)
		{
			return null;
		}
	}
}
