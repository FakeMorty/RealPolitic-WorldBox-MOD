using System;
using System.Reflection;
using NeoModLoader.services;

namespace RealPolitic;

/// <summary>
/// 0.23.5: создание окон так, как это делает сам NeoModLoader
/// (`NeoModLoader.General.WindowCreator.CreateEmptyWindow(id, titleKey, icon)`):
/// клонируется префаб `windows/empty`, окно регистрируется в `ScrollWindow._all_windows`
/// и `AssetManager.window_library`, после чего его можно открывать `ScrollWindow.showWindow(id)`.
///
/// Раньше окна конституции и родословной искали несуществующий `ScrollWindow.createWindow(id, title)`
/// и молча отключались («ScrollWindow.createWindow не найден» в Player.log) — игрок видел кнопку,
/// но окно не открывалось. Всё через рефлексию, чтобы сборка не зависела от версии NML.
/// </summary>
public static class ModWindow
{
	/// <summary>Создаёт (или возвращает уже созданное) окно. Возвращает ScrollWindow как object или null.</summary>
	public static object Create(string windowId, string titleKey, string titleText, out object contentTransform)
	{
		return Create(windowId, titleKey, titleText, null, out contentTransform);
	}

	/// <summary>То же окно, но со своей иконкой заголовка (файловый ключ из ModIcons).</summary>
	public static object Create(string windowId, string titleKey, string titleText, string iconKey, out object contentTransform)
	{
		contentTransform = null;
		try
		{
			string windowIcon = null;
			if (!string.IsNullOrEmpty(iconKey))
			{
				IdeologySprites.EnsureRegistered(iconKey, ModIcons.PathFor(iconKey));
				windowIcon = ModIcons.WindowIconFor(iconKey);
			}
			// Заголовок окна NML локализует по ключу — добавляем ключ в словарь игры, если его там нет.
			GameText.EnsureLocalized(titleKey, titleText);
			Type creator = Patches.FindGameType("NeoModLoader.General.WindowCreator");
			MethodInfo create = null;
			if (creator != null)
			{
				MethodInfo[] methods = creator.GetMethods(BindingFlags.Static | BindingFlags.Public);
				for (int i = 0; i < methods.Length; i++)
				{
					if (methods[i].Name == "CreateEmptyWindow")
					{
						create = methods[i];
						break;
					}
				}
			}
			object window = null;
			if (create != null)
			{
				ParameterInfo[] ps = create.GetParameters();
				object[] args = new object[ps.Length];
				for (int i = 0; i < ps.Length; i++)
				{
					if (i == 0)
					{
						args[i] = windowId;
					}
					else if (i == 1)
					{
						args[i] = titleKey;
					}
					else if (i == 2 && ps[i].ParameterType == typeof(string) && windowIcon != null)
					{
						// Третий параметр NML — короткий путь иконки после ui/Icons/.
						args[i] = windowIcon;
					}
					else
					{
						args[i] = ps[i].HasDefaultValue ? ps[i].DefaultValue : null;
					}
				}
				window = create.Invoke(null, args);
			}
			else
			{
				window = CreateManually(windowId, titleKey, windowIcon);
			}
			if (window == null)
			{
				return null;
			}
			contentTransform = GameRefs.ReadMember(window, "transform_content");
			if (contentTransform == null)
			{
				object t = GameRefs.ReadMember(window, "transform");
				contentTransform = GameRefs.Invoke(t, "Find", "Background/Scroll View/Viewport/Content") ?? t;
			}
			return window;
		}
		catch (Exception ex)
		{
			LogService.LogError("[Real Politic] окно «" + windowId + "» не создано: " + ex.Message);
			return null;
		}
	}

	/// <summary>Запасной путь без NML: повторяем WindowCreator.CreateEmptyWindow по шагам.</summary>
	private static object CreateManually(string windowId, string titleKey, string windowIcon)
	{
		Type scrollType = Patches.FindGameType("ScrollWindow");
		if (scrollType == null)
		{
			return null;
		}
		object all = GameRefs.ReadStatic(scrollType, "_all_windows");
		if (all != null && GameRefs.Invoke(all, "ContainsKey", windowId) is bool has && has)
		{
			return GameRefs.Invoke(all, "get_Item", windowId);
		}
		Type resources = Type.GetType("UnityEngine.Resources, UnityEngine.CoreModule");
		MethodInfo load = resources?.GetMethod("Load", new Type[] { typeof(string), typeof(Type) });
		object prefab = load?.Invoke(null, new object[] { "windows/empty", scrollType });
		if (prefab == null)
		{
			return null;
		}
		Type canvasMain = Patches.FindGameType("CanvasMain");
		object canvas = canvasMain == null ? null : GameRefs.ReadStatic(canvasMain, "instance");
		object parent = canvas == null ? null : GameRefs.ReadMember(canvas, "transformWindows");
		Type unityObject = Type.GetType("UnityEngine.Object, UnityEngine.CoreModule");
		Type transformType = Type.GetType("UnityEngine.Transform, UnityEngine.CoreModule");
		MethodInfo instantiate = unityObject?.GetMethod("Instantiate", new Type[] { unityObject, transformType });
		object window = instantiate?.Invoke(null, new object[] { prefab, parent });
		if (window == null)
		{
			return null;
		}
		GameRefs.WriteMember(window, "screen_id", windowId);
		GameRefs.WriteMember(window, "name", windowId);
		object titleText = GameRefs.ReadMember(window, "titleText");
		object localized = titleText == null ? null : GameRefs.Invoke(titleText, "GetComponent", Patches.FindGameType("LocalizedText"));
		if (localized != null)
		{
			GameRefs.WriteMember(localized, "key", titleKey);
		}
		if (all != null)
		{
			GameRefs.Invoke(all, "set_Item", windowId, window);
		}
		GameRefs.Invoke(window, "create", true);
		RegisterWindowAsset(windowId, windowIcon);
		object bg = GameRefs.Invoke(GameRefs.ReadMember(window, "transform"), "Find", "Background/Scroll View");
		object bgGo = bg == null ? null : GameRefs.ReadMember(bg, "gameObject");
		if (bgGo != null)
		{
			GameRefs.Invoke(bgGo, "SetActive", true);
		}
		return window;
	}

	/// <summary>Для запасного создания окна повторяет регистрацию его иконки в библиотеке окон игры.</summary>
	private static void RegisterWindowAsset(string windowId, string windowIcon)
	{
		if (string.IsNullOrEmpty(windowIcon))
		{
			return;
		}
		try
		{
			Type assetManager = Patches.FindGameType("AssetManager");
			Type windowAsset = Patches.FindGameType("WindowAsset");
			object library = assetManager == null ? null : GameRefs.ReadStatic(assetManager, "window_library");
			if (library == null || windowAsset == null)
			{
				return;
			}
			object asset = Activator.CreateInstance(windowAsset);
			GameRefs.WriteMember(asset, "id", windowId);
			GameRefs.WriteMember(asset, "icon_path", windowIcon);
			GameRefs.Invoke(library, "add", asset);
		}
		catch (Exception)
		{
			// Иконка заголовка не должна ломать само окно.
		}
	}

	/// <summary>Открывает окно штатным способом игры (`ScrollWindow.showWindow(id)`).</summary>
	public static void Show(string windowId, object window)
	{
		try
		{
			Type scrollType = Patches.FindGameType("ScrollWindow");
			if (scrollType != null)
			{
				MethodInfo show = scrollType.GetMethod("showWindow", BindingFlags.Static | BindingFlags.Public, null, new Type[] { typeof(string) }, null);
				if (show != null)
				{
					show.Invoke(null, new object[] { windowId });
				}
				else
				{
					MethodInfo[] methods = scrollType.GetMethods(BindingFlags.Static | BindingFlags.Public);
					for (int i = 0; i < methods.Length; i++)
					{
						if (methods[i].Name != "showWindow")
						{
							continue;
						}
						ParameterInfo[] ps = methods[i].GetParameters();
						if (ps.Length >= 1 && ps[0].ParameterType == typeof(string))
						{
							object[] args = new object[ps.Length];
							args[0] = windowId;
							for (int p = 1; p < ps.Length; p++)
							{
								args[p] = ps[p].HasDefaultValue ? ps[p].DefaultValue : (ps[p].ParameterType.IsValueType ? Activator.CreateInstance(ps[p].ParameterType) : null);
							}
							methods[i].Invoke(null, args);
							break;
						}
					}
				}
			}
			object go = GameRefs.ReadMember(window, "gameObject");
			if (go != null && GameRefs.ReadMember(go, "activeSelf") is bool active && !active)
			{
				GameRefs.Invoke(go, "SetActive", true);
				GameRefs.Invoke(window, "clickShow");
			}
			Type canvasMain = Patches.FindGameType("CanvasMain");
			object canvas = canvasMain == null ? null : GameRefs.ReadStatic(canvasMain, "instance");
			if (canvas != null)
			{
				GameRefs.Invoke(canvas, "setWindowsBGFade", true);
			}
		}
		catch (Exception ex)
		{
			LogService.LogError("[Real Politic] окно «" + windowId + "» не открыто: " + ex.Message);
		}
	}

	/// <summary>Задаёт высоту содержимого прокрутки, чтобы длинный документ листался
	/// (у пустого окна NML Content без layout-группы сам не растёт).</summary>
	public static void SetContentSize(object contentTransform, float width, float height, bool topAnchored = true)
	{
		try
		{
			object rect = GameRefs.Invoke(GameRefs.ReadMember(contentTransform, "gameObject"), "GetComponent", Type.GetType("UnityEngine.RectTransform, UnityEngine.CoreModule"));
			if (rect == null)
			{
				return;
			}
			Type v2 = Type.GetType("UnityEngine.Vector2, UnityEngine.CoreModule");
			if (topAnchored)
			{
				// Документ рисуется от верхнего края вниз (y ≤ 0): якорь и pivot — верх-центр.
				GameRefs.WriteMember(rect, "anchorMin", Activator.CreateInstance(v2, new object[] { 0.5f, 1f }));
				GameRefs.WriteMember(rect, "anchorMax", Activator.CreateInstance(v2, new object[] { 0.5f, 1f }));
				GameRefs.WriteMember(rect, "pivot", Activator.CreateInstance(v2, new object[] { 0.5f, 1f }));
				GameRefs.WriteMember(rect, "anchoredPosition", Activator.CreateInstance(v2, new object[] { 0f, 0f }));
			}
			GameRefs.WriteMember(rect, "sizeDelta", Activator.CreateInstance(v2, new object[] { width, height }));
		}
		catch (Exception)
		{
		}
	}

	/// <summary>Меняет текст заголовка уже созданного окна (например, «Конституция — Франция»).</summary>
	public static void SetTitle(object window, string text)
	{
		try
		{
			object titleText = GameRefs.ReadMember(window, "titleText");
			if (titleText != null)
			{
				GameRefs.WriteMember(titleText, "text", text);
			}
		}
		catch (Exception)
		{
		}
	}
}
