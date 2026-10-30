using System;
using System.Collections;
using System.Reflection;
using System.Text;
using NeoModLoader.services;

namespace RealPolitic;

/// <summary>
/// 0.23.2: кнопка-вкладка на боковой панели окна королевства/жителя — там же, где
/// штатные вкладки игры (поселения, армия, культура…). Делается клонированием одной из
/// штатных вкладок (ScrollWindow.tabs → дочерние объекты с компонентом WindowMetaTab):
/// с клона снимаются WindowMetaTab/TipButton (иначе он переключал бы чужую вкладку),
/// иконка меняется на иконку мода, на UnityEngine.UI.Button вешается наш обработчик.
///
/// Способ подсмотрен у EmpireCraft (KingdomWindowPatch: __instance.tabs, WindowMetaTab, Icon)
/// и NML (PowerButtonCreator: клон + замена спрайта), но без прямых ссылок на типы игры —
/// вся работа через рефлексию, чтобы сборка не зависела от версии Assembly-CSharp.
/// </summary>
public static class SideTabButton
{
	/// <summary>Создаёт (или находит уже созданную) кнопку с именем <paramref name="objectName"/> на боковой панели окна.
	/// Возвращает GameObject кнопки или null, если у окна нет панели вкладок.</summary>
	public static object Ensure(object window, string objectName, string iconKey, string iconPath, string tooltip, MethodInfo onClick)
	{
		try
		{
			if (window == null || onClick == null)
			{
				return null;
			}
			object tabs = GameRefs.ReadMember(window, "tabs");
			object tabsGo = tabs != null ? GameRefs.ReadMember(tabs, "gameObject") : null;
			object tabsTransform = tabsGo != null ? GameRefs.ReadMember(tabsGo, "transform") : null;
			if (tabsTransform == null)
			{
				return null;
			}
			// Уже есть?
			object existing = GameRefs.Invoke(tabsTransform, "Find", objectName);
			if (existing != null && IsAlive(existing))
			{
				object existingGo = GameRefs.ReadMember(existing, "gameObject");
				// Если при первом создании спрайт ещё не был готов, повторяем установку иконки,
				// а не оставляем навсегда картинку штатной вкладки, из которой клонировали кнопку.
				if (IdeologySprites.EnsureRegistered(iconKey, iconPath))
				{
					ApplyIcon(existingGo, iconPath);
				}
				SetActive(existingGo, true);
				return existingGo;
			}
			object source = FindSourceTab(tabsTransform);
			if (source == null)
			{
				return null;
			}
			bool sourceWasActive = IsActive(source);
			object clone = null;
			try
			{
				if (sourceWasActive)
				{
					SetActive(source, false);
				}
				clone = Instantiate(source, tabsTransform);
			}
			finally
			{
				if (sourceWasActive)
				{
					SetActive(source, true);
				}
			}
			if (clone == null)
			{
				return null;
			}
			SetProperty(clone, "name", objectName);
			// 0.23.2b: вычищаем ВСЕ игровые скрипты и старую кнопку в обратном порядке
			// (сначала ButtonSfx/WindowMetaTab/TipButton, затем Button, чтобы Unity не ругался на зависимости),
			// затем ставим свежий UnityEngine.UI.Button и сбрасываем цвета.
			string report = StripComponents(clone);
			ResetVisuals(clone);
			// Иконка.
			if (IdeologySprites.EnsureRegistered(iconKey, iconPath))
			{
				ApplyIcon(clone, iconPath);
			}
			LogService.LogInfo("[Real Politic] боковая кнопка " + objectName + ": " + report);
			HookClick(clone, onClick);
			// Простая подсказка: имя объекта видно в отладке, текст — через наш всплывающий ярлык нельзя без TipButton,
			// поэтому кладём подсказку в имя дочернего Text, если он есть (не у всех вкладок).
			SetChildText(clone, tooltip);
			SetActive(clone, true);
			// В самый низ панели (после штатных вкладок).
			GameRefs.Invoke(GameRefs.ReadMember(clone, "transform"), "SetAsLastSibling");
			LogService.LogInfo("[Real Politic] боковая кнопка «" + tooltip + "» добавлена в окно " + window.GetType().Name);
			return clone;
		}
		catch (Exception ex)
		{
			Patches.LogOncePublic("не удалось создать боковую кнопку " + objectName + ": " + ex.Message);
			return null;
		}
	}

	// ---------------------------------------------------------------- поиск/клон

	private static object FindSourceTab(object tabsTransform)
	{
		try
		{
			if (!(GameRefs.ReadMember(tabsTransform, "childCount") is int count) || count == 0)
			{
				return null;
			}
			object fallback = null;
			for (int i = 0; i < count; i++)
			{
				object child = GameRefs.Invoke(tabsTransform, "GetChild", i);
				object go = child != null ? GameRefs.ReadMember(child, "gameObject") : null;
				if (go == null)
				{
					continue;
				}
				string name = GameRefs.ReadMember(go, "name")?.ToString() ?? string.Empty;
				if (name.StartsWith("rp_", StringComparison.Ordinal) || name.StartsWith("space", StringComparison.Ordinal))
				{
					continue;
				}
				object tab = GetComponent(go, "WindowMetaTab, Assembly-CSharp");
				object button = GetComponent(go, "UnityEngine.UI.Button, UnityEngine.UI");
				if (button == null)
				{
					continue;
				}
				bool interactable = !(GameRefs.ReadMember(button, "interactable") is bool ia) || ia;
				// Предпочитаем НЕактивную вкладку: у активной игра выключает кнопку и подсвечивает фон.
				if (tab != null && interactable)
				{
					return go;
				}
				if (fallback == null || (tab != null && GetComponent(fallback, "WindowMetaTab, Assembly-CSharp") == null))
				{
					fallback = go;
				}
			}
			return fallback;
		}
		catch (Exception)
		{
			return null;
		}
	}

	private static object Instantiate(object original, object parentTransform)
	{
		Type unityObject = Type.GetType("UnityEngine.Object, UnityEngine.CoreModule");
		if (unityObject == null)
		{
			return null;
		}
		MethodInfo[] methods = unityObject.GetMethods(BindingFlags.Static | BindingFlags.Public);
		for (int i = 0; i < methods.Length; i++)
		{
			if (methods[i].Name != "Instantiate" || methods[i].IsGenericMethod)
			{
				continue;
			}
			ParameterInfo[] ps = methods[i].GetParameters();
			if (ps.Length == 2 && ps[1].ParameterType.Name == "Transform")
			{
				return methods[i].Invoke(null, new object[2] { original, parentTransform });
			}
		}
		return null;
	}

	// ---------------------------------------------------------------- компоненты

	private static object GetComponent(object go, string typeName)
	{
		try
		{
			Type t = Type.GetType(typeName) ?? Patches.FindGameType(typeName.Split(',')[0].Trim());
			if (t == null || go == null)
			{
				return null;
			}
			return GameRefs.Invoke(go, "GetComponent", new object[1] { t });
		}
		catch (Exception)
		{
			return null;
		}
	}

	private static void DestroyComponent(object go, string componentName)
	{
		try
		{
			object comp = GetComponent(go, componentName + ", Assembly-CSharp");
			if (comp == null)
			{
				return;
			}
			Type unityObject = Type.GetType("UnityEngine.Object, UnityEngine.CoreModule");
			MethodInfo destroy = unityObject?.GetMethod("DestroyImmediate", BindingFlags.Static | BindingFlags.Public, null, new Type[1] { unityObject }, null);
			destroy?.Invoke(null, new object[1] { comp });
		}
		catch (Exception)
		{
		}
	}

	private static void ApplyIcon(object go, string spritePath)
	{
		try
		{
			Type loader = Patches.FindGameType("SpriteTextureLoader");
			MethodInfo getSprite = loader?.GetMethod("getSprite", BindingFlags.Static | BindingFlags.Public);
			object sprite = getSprite?.Invoke(null, new object[1] { spritePath });
			if (sprite == null)
			{
				return;
			}
			Type imageType = Type.GetType("UnityEngine.UI.Image, UnityEngine.UI");
			if (imageType == null)
			{
				return;
			}
			// Сначала дочерний объект «Icon» (так у штатных вкладок), иначе — любой дочерний Image, не фон.
			object transform = GameRefs.ReadMember(go, "transform");
			object iconTr = GameRefs.Invoke(transform, "Find", "Icon");
			object image = iconTr != null ? GameRefs.Invoke(iconTr, "GetComponent", new object[1] { imageType }) : null;
			if (image == null)
			{
				object arr = GameRefs.Invoke(go, "GetComponentsInChildren", new object[2] { imageType, true });
				if (arr is Array images)
				{
					for (int i = 0; i < images.Length; i++)
					{
						object img = images.GetValue(i);
						if (!ReferenceEquals(GameRefs.ReadMember(img, "gameObject"), go))
						{
							image = img;
							break;
						}
					}
					if (image == null && images.Length > 0)
					{
						image = images.GetValue(0);
					}
				}
			}
			if (image != null)
			{
				SetProperty(image, "sprite", sprite);
				Type colorType = Type.GetType("UnityEngine.Color, UnityEngine.CoreModule");
				if (colorType != null)
				{
					SetProperty(image, "color", Activator.CreateInstance(colorType, new object[4] { 1f, 1f, 1f, 1f }));
				}
			}
		}
		catch (Exception)
		{
		}
	}

	private static void HookClick(object go, MethodInfo handler)
	{
		try
		{
			object button = GetComponent(go, "UnityEngine.UI.Button, UnityEngine.UI");
			object onClick = button != null ? GameRefs.ReadMember(button, "onClick") : null;
			if (onClick == null)
			{
				return;
			}
			GameRefs.Invoke(onClick, "RemoveAllListeners");
			Type actionType = Type.GetType("UnityEngine.Events.UnityAction, UnityEngine.CoreModule");
			if (actionType == null)
			{
				return;
			}
			Delegate del = Delegate.CreateDelegate(actionType, handler);
			MethodInfo addListener = onClick.GetType().GetMethod("AddListener", BindingFlags.Instance | BindingFlags.Public);
			addListener?.Invoke(onClick, new object[1] { del });
			SetProperty(button, "interactable", true);
		}
		catch (Exception ex)
		{
			Patches.LogOncePublic("клик боковой кнопки: " + ex.Message);
		}
	}

	/// <summary>Снимает с клона все компоненты, кроме RectTransform/Image/Layout*, и добавляет свежий Button.
	/// Возвращает строку-отчёт для лога (что было на вкладке).</summary>
	private static string StripComponents(object go)
	{
		StringBuilder report = new StringBuilder();
		try
		{
			Type componentType = Type.GetType("UnityEngine.Component, UnityEngine.CoreModule");
			Type unityObject = Type.GetType("UnityEngine.Object, UnityEngine.CoreModule");
			MethodInfo destroy = unityObject?.GetMethod("DestroyImmediate", BindingFlags.Static | BindingFlags.Public, null, new Type[1] { unityObject }, null);
			if (componentType == null || destroy == null)
			{
				return "нет доступа к UnityEngine.Component";
			}
			// Сначала дети (у вкладок бывают дочерние подсветки со своими скриптами), потом корень.
			object arr = GameRefs.Invoke(go, "GetComponentsInChildren", new object[2] { componentType, true });
			if (!(arr is Array comps))
			{
				return "GetComponentsInChildren вернул null";
			}
			// Два прохода в обратном порядке: сначала зависимые скрипты игры (ButtonSfx, TipButton, WindowMetaTab),
			// затем сама старая Button и всё прочее не из белого списка.
			for (int pass = 0; pass < 2; pass++)
			{
				for (int i = comps.Length - 1; i >= 0; i--)
				{
					object c = comps.GetValue(i);
					if (c == null || !IsAlive(c))
					{
						continue;
					}
					Type t = c.GetType();
					string n = t.Name;
					if (pass == 0)
					{
						report.Append(n).Append(' ');
					}
					bool keep = n == "RectTransform" || n == "Transform" || n == "Image" || n == "CanvasRenderer"
						|| n == "Text" || n.StartsWith("Layout", StringComparison.Ordinal) || n.EndsWith("LayoutGroup", StringComparison.Ordinal)
						|| n == "ContentSizeFitter" || n == "AspectRatioFitter" || n == "Mask" || n == "RectMask2D";
					if (keep)
					{
						continue;
					}
					bool isGameScript = t.Assembly.GetName().Name == "Assembly-CSharp" || (t.Namespace ?? string.Empty).Length == 0;
					bool isSelectable = n == "Button" || n == "Toggle" || n == "Selectable" || n == "EventTrigger";
					if (pass == 0 && !isGameScript)
					{
						continue;
					}
					try
					{
						destroy.Invoke(null, new object[1] { c });
					}
					catch (Exception ex)
					{
						report.Append("[не снят ").Append(n).Append(": ").Append(ex.InnerException?.Message ?? ex.Message).Append("] ");
					}
				}
			}
			// Свежая кнопка.
			Type buttonType = Type.GetType("UnityEngine.UI.Button, UnityEngine.UI");
			MethodInfo addComponent = go.GetType().GetMethod("AddComponent", BindingFlags.Instance | BindingFlags.Public, null, new Type[1] { typeof(Type) }, null);
			object button = buttonType != null ? addComponent?.Invoke(go, new object[1] { buttonType }) : null;
			if (button != null)
			{
				SetProperty(button, "interactable", true);
				Type imageType = Type.GetType("UnityEngine.UI.Image, UnityEngine.UI");
				object rootImage = imageType != null ? GameRefs.Invoke(go, "GetComponent", new object[1] { imageType }) : null;
				if (rootImage != null)
				{
					SetProperty(button, "targetGraphic", rootImage);
					SetProperty(rootImage, "raycastTarget", true);
				}
				report.Append("→ Button добавлен");
			}
			else
			{
				report.Append("→ Button НЕ добавлен");
			}
		}
		catch (Exception ex)
		{
			report.Append(" ошибка: ").Append(ex.Message);
		}
		return report.ToString();
	}

	/// <summary>Сбрасывает цвета всех Image в белый, выключает дочерние подсветки «выбрано/нажато».</summary>
	private static void ResetVisuals(object go)
	{
		try
		{
			Type imageType = Type.GetType("UnityEngine.UI.Image, UnityEngine.UI");
			Type colorType = Type.GetType("UnityEngine.Color, UnityEngine.CoreModule");
			if (imageType == null || colorType == null)
			{
				return;
			}
			object white = Activator.CreateInstance(colorType, new object[4] { 1f, 1f, 1f, 1f });
			if (GameRefs.Invoke(go, "GetComponentsInChildren", new object[2] { imageType, true }) is Array images)
			{
				for (int i = 0; i < images.Length; i++)
				{
					object img = images.GetValue(i);
					object imgGo = GameRefs.ReadMember(img, "gameObject");
					string n = (GameRefs.ReadMember(imgGo, "name")?.ToString() ?? string.Empty).ToLowerInvariant();
					if (!ReferenceEquals(imgGo, go) && n != "icon"
						&& (n.Contains("select") || n.Contains("active") || n.Contains("press") || n.Contains("highlight") || n.Contains("glow") || n.Contains("frame") || n.Contains("red")))
					{
						SetActive(imgGo, false);
						continue;
					}
					SetProperty(img, "color", white);
				}
			}
			// CanvasGroup клона: полностью видим и кликабелен.
			object canvasGroup = GetComponent(go, "UnityEngine.CanvasGroup, UnityEngine.UI") ?? GetComponent(go, "UnityEngine.CanvasGroup, UnityEngine.CoreModule");
			if (canvasGroup != null)
			{
				SetProperty(canvasGroup, "alpha", 1f);
				SetProperty(canvasGroup, "interactable", true);
				SetProperty(canvasGroup, "blocksRaycasts", true);
			}
		}
		catch (Exception)
		{
		}
	}

	private static void SetChildText(object go, string text)
	{
		try
		{
			Type textType = Type.GetType("UnityEngine.UI.Text, UnityEngine.UI");
			if (textType == null)
			{
				return;
			}
			object comp = GameRefs.Invoke(go, "GetComponentInChildren", new object[2] { textType, true });
			if (comp != null)
			{
				SetProperty(comp, "text", text);
			}
		}
		catch (Exception)
		{
		}
	}

	private static bool IsActive(object go)
	{
		try
		{
			return GameRefs.ReadMember(go, "activeSelf") is bool active && active;
		}
		catch (Exception)
		{
			return true;
		}
	}

	/// <summary>0.45: показать/скрыть кнопку снаружи (объединение вкладок мода в одну).</summary>
	public static void SetActivePublic(object go, bool active)
	{
		SetActive(go, active);
	}

	private static void SetActive(object go, bool active)
	{
		try
		{
			GameRefs.Invoke(go, "SetActive", active);
		}
		catch (Exception)
		{
		}
	}

	private static void SetProperty(object target, string name, object value)
	{
		PropertyInfo prop = target?.GetType().GetProperty(name, BindingFlags.Instance | BindingFlags.Public);
		prop?.SetValue(target, value, null);
	}

	public static bool IsAlive(object unityObj)
	{
		try
		{
			if (unityObj == null)
			{
				return false;
			}
			Type unityObject = Type.GetType("UnityEngine.Object, UnityEngine.CoreModule");
			MethodInfo op = unityObject?.GetMethod("op_Inequality", BindingFlags.Static | BindingFlags.Public);
			if (op != null)
			{
				return (bool)op.Invoke(null, new object[] { unityObj, null });
			}
			return true;
		}
		catch (Exception)
		{
			return false;
		}
	}
}
