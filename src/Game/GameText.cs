using System;
using System.Collections.Generic;
using System.Reflection;
using RealPolitic.Core;

namespace RealPolitic;

public static class GameText
{
	public static string ActorName(Actor actor)
	{
		if (actor == null)
		{
			return string.Empty;
		}
		try
		{
			// Actor.name переопределён и возвращает отображаемое имя юнита.
			if (GameRefs.ReadProperty(actor, "name") is string { Length: >0 } text)
			{
				return text;
			}
			if (GameRefs.ReadProperty(actor.data, "name") is string { Length: >0 } text2)
			{
				return text2;
			}
			return "Безымянный";
		}
		catch (Exception)
		{
			return "Безымянный";
		}
	}

	public static string ActorTitle(Actor actor)
	{
		if (actor == null)
		{
			return string.Empty;
		}
		try
		{
			object obj = GameRefs.ReadProperty(actor, "title");
			if (obj != null)
			{
				return obj.ToString();
			}
			if (GameRefs.IsWarrior(actor))
			{
				return "воин";
			}
			return string.Empty;
		}
		catch (Exception)
		{
			return string.Empty;
		}
	}

	/// <summary>Полный (long) id юнита — для реестра родословной, где ключи long.</summary>
	public static long ActorLongId(Actor actor)
	{
		if (actor == null)
		{
			return 0L;
		}
		// 0.53.1: прямой доступ вместо отражения — этот вызов идёт каждый кадр.
		try
		{
			long fast = ((NanoObject)actor).id;
			if (fast > 0L)
			{
				return fast;
			}
		}
		catch (Exception)
		{
		}
		try
		{
			object obj = GameRefs.ReadProperty(actor.data, "id");
			if (obj is long num)
			{
				return num;
			}
			if (obj is int result)
			{
				return result;
			}
		}
		catch (Exception)
		{
		}
		return 0L;
	}

	public static int ActorId(Actor actor)
	{
		if (actor == null)
		{
			return -1;
		}
		// 0.53.1: прямой доступ вместо отражения — этот вызов идёт каждый кадр.
		try
		{
			long fast = ((NanoObject)actor).id;
			if (fast > 0L && fast <= int.MaxValue)
			{
				return (int)fast;
			}
		}
		catch (Exception)
		{
		}
		try
		{
			// В игре id — long (свойство), поэтому читаем и int, и long.
			object obj = GameRefs.ReadProperty(actor.data, "id");
			if (obj is long num)
			{
				return (int)num;
			}
			if (obj is int result)
			{
				return result;
			}
			return -1;
		}
		catch (Exception)
		{
			return -1;
		}
	}

	public static int ActorAge(Actor actor)
	{
		if (actor == null)
		{
			return 0;
		}
		try
		{
			object obj = GameRefs.Invoke(actor, "getAge");
			if (obj is float num)
			{
				return (int)num;
			}
			if (obj is int result)
			{
				return result;
			}
			object obj2 = GameRefs.ReadProperty(actor, "age");
			if (obj2 is float num2)
			{
				return (int)num2;
			}
			if (obj2 is int result2)
			{
				return result2;
			}
			return 0;
		}
		catch (Exception)
		{
			return 0;
		}
	}

	/// <summary>Юнит — женского пола (для титулов «королева», «жрица» и т. п.).</summary>
	public static bool ActorIsFemale(Actor actor)
	{
		if (actor == null)
		{
			return false;
		}
		try
		{
			if (GameRefs.Invoke(actor, "isSexFemale") is bool result)
			{
				return result;
			}
			if (actor.data != null && GameRefs.ReadProperty(actor.data, "sex")?.ToString() == "Female")
			{
				return true;
			}
			return false;
		}
		catch (Exception)
		{
			return false;
		}
	}

	/// <summary>Первая буква — заглавная («королева» → «Королева»).</summary>
	public static string Capitalize(string text)
	{
		if (string.IsNullOrEmpty(text))
		{
			return text;
		}
		if (char.IsUpper(text[0]))
		{
			return text;
		}
		return char.ToUpper(text[0]) + text.Substring(1);
	}

	/// <summary>Локализация по ключу; если перевода нет, возвращается сам ключ.</summary>
	public static string Localized(string key)
	{
		return Localized(key, key);
	}

	/// <summary>0.23.5: кладёт строку в словарь игры (как NML `LM.AddToCurrentLocale`), если ключа ещё нет.
	/// Нужно для заголовков окон: NML локализует их по ключу через LocalizedText.</summary>
	public static void EnsureLocalized(string key, string text)
	{
		try
		{
			if (string.IsNullOrEmpty(key) || Localized(key, string.Empty).Length > 0)
			{
				return;
			}
			Type type = Patches.FindGameType("LocalizedTextManager");
			object instance = type == null ? null : GameRefs.ReadStatic(type, "instance");
			object dict = instance == null ? null : GameRefs.ReadMember(instance, "_localized_text");
			if (dict != null)
			{
				GameRefs.Invoke(dict, "set_Item", key, text ?? key);
			}
		}
		catch (Exception)
		{
		}
	}

	public static string Localized(string key, string fallback)
	{
		try
		{
			if (string.IsNullOrEmpty(key) || key.IndexOf(' ') >= 0)
			{
				return fallback;
			}
			// LocalizedTextManager.getText — статический метод (в 0.51.2), не instance.text.
			Type type = Patches.FindGameType("LocalizedTextManager");
			if (type == null)
			{
				return fallback;
			}
			MethodInfo method = type.GetMethod("getText", BindingFlags.Static | BindingFlags.Public);
			if (method == null)
			{
				return fallback;
			}
			ParameterInfo[] parameters = method.GetParameters();
			object[] array = new object[parameters.Length];
			array[0] = key;
			for (int i = 1; i < parameters.Length; i++)
			{
				array[i] = (parameters[i].ParameterType.IsValueType ? Activator.CreateInstance(parameters[i].ParameterType) : null);
			}
			// WorldBox returns the key itself for a missing translation.
            // That is NOT a translation; honour the caller's readable fallback.
            return (method.Invoke(null, array) is string text
                && !string.IsNullOrWhiteSpace(text) && !string.Equals(text, key, StringComparison.Ordinal))
                ? text : fallback;
		}
		catch (Exception)
		{
			return fallback;
		}
	}

	public static string Format(string template, PolEvent evt)
	{
		// Подстановка параметров перенесена в ядро (PolEvent.Render), чтобы её можно было тестировать без WorldBox.
		return evt.Render(template);
	}
}
