using System;
using System.Reflection;
using NeoModLoader.services;
using RealPolitic.Core;
using UnityEngine;

namespace RealPolitic;

/// <summary>
/// Кнопка «Слой идеологий» в игровой панели слоёв — рядом со «Слоем культур».
///
/// Своя кнопка создаётся клонированием штатной кнопки culture_layer:
/// у клона снимается компонент PowerButton (иначе он попытается найти
/// несуществующую силу culture_layer по имени объекта и подпишется на чужой toggle),
/// а клик вешается напрямую на UnityEngine.UI.Button → IdeologyMapLayer.Toggle().
/// У кнопки собственная иконка политической карты (rp_tab_ideology_layer), подсветка «включено/выключено»
/// делается цветом изображения — так не нужен доступ к приватным ToggleIcon игры.
///
/// Кнопка пересоздаётся, если панель сил была пересобрана (проверка раз в секунду).
/// </summary>
public static class IdeologyLayerButton
{
	private const string ObjectName = "rp_ideology_layer";

	private static object _button;

	private static object _iconImage;

	private static float _nextCheck;

	private static int _failures;

	/// <summary>Вызывается каждый кадр из RealPoliticMod.Update.</summary>
	public static void Tick()
	{
		float now = Time.unscaledTime;
		if (now < _nextCheck)
		{
			return;
		}
		_nextCheck = now + 1f;
		try
		{
			if (_button != null && IsAlive(_button))
			{
				UpdateHighlight();
				return;
			}
			if (_failures >= 5)
			{
				return;
			}
			if (!TryCreate())
			{
				_failures++;
				if (_failures == 5)
				{
					LogService.LogError("[Real Politic] кнопку слоя идеологий создать не удалось — используйте горячую клавишу (см. настройки мода)");
				}
			}
			else
			{
				_failures = 0;
				LogService.LogInfo("[Real Politic] кнопка слоя идеологий добавлена в панель слоёв");
			}
		}
		catch (Exception ex)
		{
			Patches.LogOncePublic("ошибка кнопки слоя идеологий: " + ex.Message);
		}
	}

	// ---------------------------------------------------------------- создание

	private static bool TryCreate()
	{
		object source = FindPowerButton("culture_layer");
		if (source == null)
		{
			return false;
		}
		object sourceGo = GameRefs.ReadMember(source, "gameObject");
		object parent = GameRefs.ReadMember(GameRefs.ReadMember(sourceGo, "transform"), "parent");
		if (sourceGo == null || parent == null)
		{
			return false;
		}
		// Проверяем, не существует ли уже кнопка в контейнере
		object existingTransform = GameRefs.Invoke(parent, "Find", ObjectName);
		if (existingTransform != null)
		{
			object existingGo = GameRefs.ReadMember(existingTransform, "gameObject") ?? existingTransform;
			_button = existingGo;
			_iconImage = FindIconImage(existingGo);
			ApplyIcon();
			UpdateHighlight();
			return true;
		}
		object clone = Instantiate(sourceGo, parent);
		if (clone == null)
		{
			return false;
		}
		SetName(clone, ObjectName);
			// Снимаем игровую логику силы: иначе клон будет управлять слоем культур.
			DestroyComponent(clone, "PowerButton");
			DestroyComponent(clone, "TipButton");
			// В разных сборках WorldBox подсказка может иметь одно из этих имён.
			// Клон не должен показывать подсказку штатного слоя поверх нижней панели.
			DestroyComponent(clone, "TooltipButton");
			DestroyComponent(clone, "HoverTip");
			HookClick(clone);
		_iconImage = FindIconImage(clone);
		ApplyIcon();
		// Ставим кнопку сразу после кнопки слоя культур.
		object srcTransform = GameRefs.ReadMember(sourceGo, "transform");
		object cloneTransform = GameRefs.ReadMember(clone, "transform");
		object index = GameRefs.Invoke(srcTransform, "GetSiblingIndex");
		if (index is int i)
		{
			GameRefs.Invoke(cloneTransform, "SetSiblingIndex", i + 1);
		}
		_button = clone;
		UpdateHighlight();
		return true;
	}

	private static object FindPowerButton(string id)
	{
		Type type = Patches.FindGameType("PowerButton");
		MethodInfo get = type?.GetMethod("get", BindingFlags.Static | BindingFlags.Public);
		return get?.Invoke(null, new object[1] { id });
	}

	private static object Instantiate(object original, object parent)
	{
		Type unityObject = Type.GetType("UnityEngine.Object, UnityEngine.CoreModule") ?? Patches.FindGameType("Object");
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
				return methods[i].Invoke(null, new object[2] { original, parent });
			}
		}
		return null;
	}

	private static void SetName(object go, string name)
	{
		PropertyInfo prop = go.GetType().GetProperty("name", BindingFlags.Instance | BindingFlags.Public);
		prop?.SetValue(go, name, null);
	}

	private static void DestroyComponent(object go, string componentName)
	{
		try
		{
			Type type = Patches.FindGameType(componentName);
			if (type == null)
			{
				return;
			}
			MethodInfo getComponent = go.GetType().GetMethod("GetComponent", BindingFlags.Instance | BindingFlags.Public, null, new Type[1] { typeof(Type) }, null);
			object component = getComponent?.Invoke(go, new object[1] { type });
			if (component == null)
			{
				return;
			}
			Type unityObject = Type.GetType("UnityEngine.Object, UnityEngine.CoreModule");
			MethodInfo destroy = unityObject?.GetMethod("DestroyImmediate", BindingFlags.Static | BindingFlags.Public, null, new Type[1] { unityObject }, null);
			destroy?.Invoke(null, new object[1] { component });
		}
		catch (Exception)
		{
		}
	}

	/// <summary>Клик по клону: снимаем старые слушатели и вешаем переключение слоя.</summary>
	private static void HookClick(object go)
	{
		try
		{
			Type buttonType = Type.GetType("UnityEngine.UI.Button, UnityEngine.UI");
			if (buttonType == null)
			{
				return;
			}
			MethodInfo getComponent = go.GetType().GetMethod("GetComponent", BindingFlags.Instance | BindingFlags.Public, null, new Type[1] { typeof(Type) }, null);
			object button = getComponent?.Invoke(go, new object[1] { buttonType });
			if (button == null)
			{
				return;
			}
			object onClick = GameRefs.ReadMember(button, "onClick");
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
			Delegate handler = Delegate.CreateDelegate(actionType, typeof(IdeologyLayerButton).GetMethod("OnClick", BindingFlags.Static | BindingFlags.NonPublic));
			MethodInfo addListener = onClick.GetType().GetMethod("AddListener", BindingFlags.Instance | BindingFlags.Public);
			addListener?.Invoke(onClick, new object[1] { handler });
		}
		catch (Exception ex)
		{
			Patches.LogOncePublic("не удалось повесить клик на кнопку слоя идеологий: " + ex.Message);
		}
	}

	private static void OnClick()
	{
		IdeologyMapLayer.Toggle();
		UpdateHighlight();
	}

	// ---------------------------------------------------------------- вид кнопки

	/// <summary>Иконка внутри кнопки — дочерний Image, который не является фоном самой кнопки.</summary>
	private static object FindIconImage(object go)
	{
		try
		{
			Type imageType = Type.GetType("UnityEngine.UI.Image, UnityEngine.UI");
			if (imageType == null)
			{
				return null;
			}
			MethodInfo getComponents = go.GetType().GetMethod("GetComponentsInChildren", BindingFlags.Instance | BindingFlags.Public, null, new Type[2] { typeof(Type), typeof(bool) }, null);
			object result = getComponents?.Invoke(go, new object[2] { imageType, true });
			if (!(result is object[] images))
			{
				return null;
			}
			for (int i = 0; i < images.Length; i++)
			{
				object image = images[i];
				object imageGo = GameRefs.ReadMember(image, "gameObject");
				if (!ReferenceEquals(imageGo, go))
				{
					return image;
				}
			}
			return images.Length > 0 ? images[0] : null;
		}
		catch (Exception)
		{
			return null;
		}
	}

	private static void ApplyIcon()
	{
		try
		{
			if (_iconImage == null)
			{
				return;
			}
			// Собственная политическая карта с цветными территориями: корона монархии
			// раньше вводила в заблуждение, будто кнопка относится только к монархиям.
			object sprite = ModIcons.GetSprite(ModIcons.IdeologyLayerKey);
			if (sprite == null)
			{
				return;
			}
			PropertyInfo spriteProp = _iconImage.GetType().GetProperty("sprite", BindingFlags.Instance | BindingFlags.Public);
			spriteProp?.SetValue(_iconImage, sprite, null);
		}
		catch (Exception)
		{
		}
	}

	/// <summary>Подсветка состояния: активный слой — яркая иконка, выключенный — приглушённая.</summary>
	private static void UpdateHighlight()
	{
		try
		{
			if (_iconImage == null)
			{
				return;
			}
			Type colorType = Type.GetType("UnityEngine.Color, UnityEngine.CoreModule");
			if (colorType == null)
			{
				return;
			}
			float v = IdeologyMapLayer.Active ? 1f : 0.45f;
			object color = Activator.CreateInstance(colorType, new object[4] { v, v, v, 1f });
			PropertyInfo colorProp = _iconImage.GetType().GetProperty("color", BindingFlags.Instance | BindingFlags.Public);
			colorProp?.SetValue(_iconImage, color, null);
		}
		catch (Exception)
		{
		}
	}

		private static bool IsAlive(object go)
		{
			if (go == null)
			{
				return false;
			}
			try
			{
				if (go is UnityEngine.Object uo && uo == null)
				{
					return false;
				}
				object transform = GameRefs.ReadMember(go, "transform");
				return transform != null;
			}
			catch (Exception)
			{
				return false;
			}
		}
}
