using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using System.Text;
using NeoModLoader.services;

namespace RealPolitic;

/// <summary>
/// Канал показа сообщений. В WorldBox 0.51.2 уведомления слева сверху — это
/// «история мира» (HistoryHud), куда сообщения попадают через WorldLogMessage + add().
/// Того же канала использует игра для «основано новое королевство» (WorldLog.logNewKingdom).
///
/// Раньше все сообщения мода использовали служебный ассет auto_tester, поэтому
/// игра показывала его iconPlay — синюю стрелку вправо. Теперь для каждого пути
/// иконки один раз создаётся отдельный тип записи истории: он копирует поведение
/// auto_tester (включая вывод готового текста из special1), но в поле path_icon
/// получает собственный спрайт Real Politic. Если нативный API изменится, показ
/// честно откатывается к исходному auto_tester, а не теряет само уведомление.
/// </summary>
public static class GameMessages
{
	private static readonly Dictionary<string, object> IconAssets = new Dictionary<string, object>(StringComparer.Ordinal);
	private static readonly HashSet<string> IconFailurePaths = new HashSet<string>(StringComparer.Ordinal);
	private static bool _customIconLogShown;

	public static void Critical(string text)
	{
		Send(text, NotificationIcons.DefaultPath);
	}

	public static void Critical(string text, string iconPath)
	{
		Send(text, iconPath);
	}

	public static void Major(string text)
	{
		Send(text, NotificationIcons.DefaultPath);
	}

	public static void Major(string text, string iconPath)
	{
		Send(text, iconPath);
	}

	public static void Minor(string text)
	{
		Send(text, NotificationIcons.DefaultPath);
	}

	public static void Minor(string text, string iconPath)
	{
		Send(text, iconPath);
	}

	private static void Send(string text, string iconPath)
	{
		if (string.IsNullOrEmpty(text))
		{
			return;
		}
		try
		{
			if (!TryWorldLogMessage(text, iconPath))
			{
				// Запасной канал: журнал NML, если игровой API не совпал с ожидаемым.
				LogService.LogInfo("[Real Politic] " + text);
			}
		}
		catch (Exception ex)
		{
			LogService.LogError("[Real Politic] не удалось показать сообщение: " + ex.Message);
			LogService.LogInfo("[Real Politic] " + text);
		}
	}

	private static bool TryWorldLogMessage(string text, string iconPath)
	{
		try
		{
			Type msgType = Patches.FindGameType("WorldLogMessage");
			Type extType = Patches.FindGameType("WorldLogMessageExtensions");
			if (msgType == null || extType == null)
			{
				return false;
			}

			object asset = GetNotificationAsset(iconPath);
			if (asset == null)
			{
				return false;
			}

			// new WorldLogMessage(asset, text) — конструктор выставляет timestamp сам.
			object msg = CreateWorldLogMessage(msgType, asset, text);
			if (msg == null)
			{
				return false;
			}

			// WorldLogMessageExtensions.add(msg) — показ в HistoryHud + запись в БД истории.
			MethodInfo add = extType.GetMethod("add", BindingFlags.Static | BindingFlags.Public);
			if (add == null)
			{
				return false;
			}
			add.Invoke(null, new object[1] { msg });
			return true;
		}
		catch (Exception)
		{
			return false;
		}
	}

	private static object CreateWorldLogMessage(Type msgType, object asset, string text)
	{
		// Ищем конструктор (WorldLogAsset, string, string, string) и вызываем с null для optional-параметров.
		ConstructorInfo[] constructors = msgType.GetConstructors();
		foreach (ConstructorInfo constructorInfo in constructors)
		{
			ParameterInfo[] parameters = constructorInfo.GetParameters();
			if (parameters.Length >= 2 && parameters[0].ParameterType.Name == "WorldLogAsset")
			{
				object[] array = new object[parameters.Length];
				array[0] = asset;
				array[1] = text;
				for (int i = 2; i < parameters.Length; i++)
				{
					array[i] = (parameters[i].ParameterType.IsValueType ? Activator.CreateInstance(parameters[i].ParameterType) : null);
				}
				return constructorInfo.Invoke(array);
			}
		}
		return null;
	}

	private static object GetNotificationAsset(string iconPath)
	{
		object template = GetAutoTesterAsset();
		if (template == null || string.IsNullOrWhiteSpace(iconPath))
		{
			return template;
		}

		object cached;
		if (IconAssets.TryGetValue(iconPath, out cached) && cached != null)
		{
			return cached;
		}

		try
		{
			NotificationIcons.EnsureSpriteRegistered(iconPath);

			object library = GetWorldLogLibrary();
			if (library == null)
			{
				return template;
			}

			string assetId = AssetIdForPath(iconPath);
			object asset = LibraryGet(library, assetId);
			if (asset != null)
			{
				if (!SetMemberValue(asset, "path_icon", iconPath))
				{
					return template;
				}
				IconAssets[iconPath] = asset;
				return asset;
			}

			asset = Activator.CreateInstance(template.GetType(), true);
			CopyInstanceFields(template, asset);
			if (!SetMemberValue(asset, "id", assetId) || !SetMemberValue(asset, "path_icon", iconPath))
			{
				return template;
			}

			object added = LibraryAdd(library, asset);
			if (added != null && template.GetType().IsInstanceOfType(added))
			{
				asset = added;
			}

			// WorldLogMessage хранит только asset_id, а картинку позднее ищет по
			// библиотеке. Поэтому недостаточно держать клон в собственном кэше:
			// проверяем, что игра правда может найти его по новому идентификатору.
			object registered = LibraryGet(library, assetId);
			if (registered != null)
			{
				asset = registered;
			}
			if (!string.Equals(GetMemberString(asset, "id"), assetId, StringComparison.Ordinal)
				|| !SetMemberValue(asset, "path_icon", iconPath))
			{
				return template;
			}

			IconAssets[iconPath] = asset;
			if (!_customIconLogShown)
			{
				_customIconLogShown = true;
				LogService.LogInfo("[Real Politic] уведомления используют собственные иконки вместо служебной стрелки auto_tester");
			}
			return asset;
		}
		catch (Exception ex)
		{
			if (IconFailurePaths.Add(iconPath))
			{
				LogService.LogError("[Real Politic] не удалось подготовить иконку уведомления " + iconPath + ": " + ExceptionMessage(ex) + ". Используется запасная служебная иконка игры.");
			}
			return template;
		}
	}

	internal static string AssetIdForPath(string iconPath)
	{
		string source = NotificationIcons.FileKeyForPath(iconPath);
		if (string.IsNullOrEmpty(source))
		{
			source = iconPath ?? string.Empty;
		}
		if (source.StartsWith("notify_", StringComparison.OrdinalIgnoreCase))
		{
			source = source.Substring("notify_".Length);
		}

		StringBuilder result = new StringBuilder("rp_notify_");
		bool lastWasUnderscore = true;
		foreach (char c in source.ToLowerInvariant())
		{
			if ((c >= 'a' && c <= 'z') || (c >= '0' && c <= '9'))
			{
				result.Append(c);
				lastWasUnderscore = false;
			}
			else if (!lastWasUnderscore)
			{
				result.Append('_');
				lastWasUnderscore = true;
			}
		}
		while (result.Length > 0 && result[result.Length - 1] == '_')
		{
			result.Length--;
		}
		return result.ToString() == "rp_notify" ? "rp_notify_default" : result.ToString();
	}

	private static object GetAutoTesterAsset()
	{
		try
		{
			Type type = Patches.FindGameType("WorldLogLibrary");
			if (type != null)
			{
				object value = GetMemberValue(type, null, "auto_tester");
				if (value != null)
				{
					return value;
				}
			}

			object library = GetWorldLogLibrary();
			return library == null ? null : LibraryGet(library, "auto_tester");
		}
		catch (Exception)
		{
			return null;
		}
	}

	private static object GetWorldLogLibrary()
	{
		try
		{
			Type managerType = Patches.FindGameType("AssetManager");
			if (managerType == null)
			{
				return null;
			}

			object library = GetMemberValue(managerType, null, "world_log_library");
			if (library != null)
			{
				return library;
			}

			object instance = GetMemberValue(managerType, null, "_instance") ?? GetMemberValue(managerType, null, "instance");
			if (instance != null)
			{
				library = GetMemberValue(instance.GetType(), instance, "world_log_library");
				if (library != null)
				{
					return library;
				}
			}

			object list = instance == null
				? GetMemberValue(managerType, null, "_list")
				: GetMemberValue(instance.GetType(), instance, "_list");
			return FindWorldLogLibraryInList(list);
		}
		catch (Exception)
		{
			return null;
		}
	}

	private static object FindWorldLogLibraryInList(object list)
	{
		IEnumerable enumerable = list as IEnumerable;
		if (enumerable == null)
		{
			return null;
		}
		foreach (object candidate in enumerable)
		{
			if (candidate != null && candidate.GetType().Name == "WorldLogLibrary")
			{
				return candidate;
			}
		}
		return null;
	}

	private static object LibraryGet(object library, string assetId)
	{
		if (library == null || string.IsNullOrEmpty(assetId))
		{
			return null;
		}

		// Настоящий AssetLibrary.get при промахе возвращает пустой ассет и пишет id
		// в список ненайденных, поэтому сначала спрашиваем has(), если он доступен.
		object has = InvokeCompatibleMethod(library, "has", assetId);
		if (has is bool && !(bool)has)
		{
			return null;
		}

		object asset = InvokeCompatibleMethod(library, "get", assetId);
		return asset != null && string.Equals(GetMemberString(asset, "id"), assetId, StringComparison.Ordinal) ? asset : null;
	}

	private static object LibraryAdd(object library, object asset)
	{
		return InvokeCompatibleMethod(library, "add", asset);
	}

	private static object InvokeCompatibleMethod(object target, string name, object argument)
	{
		if (target == null || argument == null)
		{
			return null;
		}

		MethodInfo[] methods = target.GetType().GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
		for (int pass = 0; pass < 2; pass++)
		{
			foreach (MethodInfo method in methods)
			{
				if (method.Name != name || method.IsGenericMethodDefinition)
				{
					continue;
				}
				ParameterInfo[] parameters = method.GetParameters();
				if (parameters.Length != 1)
				{
					continue;
				}
				Type parameterType = parameters[0].ParameterType;
				bool compatible = pass == 0
					? parameterType == argument.GetType()
					: parameterType.IsAssignableFrom(argument.GetType());
				if (compatible)
				{
					return method.Invoke(target, new object[1] { argument });
				}
			}
		}
		return null;
	}

	private static object GetMemberValue(Type type, object target, string name)
	{
		if (type == null)
		{
			return null;
		}

		const BindingFlags flags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static;
		FieldInfo field = type.GetField(name, flags);
		if (field != null)
		{
			return field.IsStatic ? field.GetValue(null) : (target == null ? null : field.GetValue(target));
		}

		PropertyInfo property = type.GetProperty(name, flags);
		MethodInfo getter = property == null ? null : property.GetGetMethod(true);
		if (getter == null)
		{
			return null;
		}
		return getter.IsStatic ? getter.Invoke(null, null) : (target == null ? null : getter.Invoke(target, null));
	}

	private static string GetMemberString(object target, string name)
	{
		object value = GetInstanceMemberValue(target, name);
		return value == null ? null : value.ToString();
	}

	private static object GetInstanceMemberValue(object target, string name)
	{
		if (target == null)
		{
			return null;
		}

		const BindingFlags flags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.DeclaredOnly;
		for (Type type = target.GetType(); type != null; type = type.BaseType)
		{
			FieldInfo field = type.GetField(name, flags);
			if (field != null)
			{
				return field.GetValue(target);
			}
			PropertyInfo property = type.GetProperty(name, flags);
			MethodInfo getter = property == null ? null : property.GetGetMethod(true);
			if (getter != null)
			{
				return getter.Invoke(target, null);
			}
		}
		return null;
	}

	private static bool SetMemberValue(object target, string name, object value)
	{
		if (target == null || value == null)
		{
			return false;
		}

		const BindingFlags flags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.DeclaredOnly;
		for (Type type = target.GetType(); type != null; type = type.BaseType)
		{
			FieldInfo field = type.GetField(name, flags);
			if (field != null)
			{
				if (field.IsInitOnly || !field.FieldType.IsAssignableFrom(value.GetType()))
				{
					return false;
				}
				field.SetValue(target, value);
				return true;
			}

			PropertyInfo property = type.GetProperty(name, flags);
			MethodInfo setter = property == null ? null : property.GetSetMethod(true);
			if (setter != null)
			{
				if (!property.PropertyType.IsAssignableFrom(value.GetType()))
				{
					return false;
				}
				setter.Invoke(target, new object[1] { value });
				return true;
			}
		}
		return false;
	}

	private static void CopyInstanceFields(object source, object destination)
	{
		const BindingFlags flags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.DeclaredOnly;
		for (Type type = source.GetType(); type != null && type != typeof(object); type = type.BaseType)
		{
			foreach (FieldInfo field in type.GetFields(flags))
			{
				if (field.IsInitOnly)
				{
					continue;
				}
				try
				{
					field.SetValue(destination, field.GetValue(source));
				}
				catch (Exception)
				{
					// Одно служебное поле новой версии игры не должно ломать весь показ:
					// обязательные id/path_icon отдельно проверяются после копирования.
				}
			}
		}
	}

	private static string ExceptionMessage(Exception ex)
	{
		Exception current = ex;
		while (current.InnerException != null)
		{
			current = current.InnerException;
		}
		return current.Message;
	}
}
