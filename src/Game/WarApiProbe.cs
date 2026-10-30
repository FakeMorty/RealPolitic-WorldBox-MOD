using System;
using System.Collections.Generic;
using System.Reflection;
using NeoModLoader.services;

namespace RealPolitic;

/// <summary>
/// Единственное место, где мод начинает войну в игре, и единственное место,
/// где об этом пишется внятная строка в Player.log.
///
/// Зачем понадобилось. В 0.31.12 и 0.31.14 мод вызывал
/// <c>diplomacy.startWar(...)</c> «слепым» отражением: искался первый публичный
/// метод с именем startWar и нужным числом аргументов, типы не сверялись,
/// результат никуда не писался. В обеих партиях в журнале осталась ровно одна
/// строка («дипломатический API недоступен…»), и по ней нельзя понять, что
/// именно случилось: метода с такой арностью нет, метод вернул null, игра
/// отказала или война создалась, но не видна проверке.
///
/// Что делает этот модуль:
///  • один раз за запуск выгружает в журнал фактические сигнатуры
///    (WarTypeLibrary — поля; startWar, newWar, getWar — все перегрузки с типами
///    параметров и типом возврата), чтобы больше не гадать;
///  • при каждом вызове перебирает ПЕРЕГРУЗКИ, подставляя аргументы по их типам
///    (Kingdoms → нападающий/защитник, WarTypeAsset → тип войны, bool → true,
///    остальные — значения по умолчанию), и пишет, что вернула каждая;
///  • страхуется запасным путём WarManager.newWar (по декомпиляции игры
///    startWar делегирует именно в него);
///  • война считается начатой только если она есть в реестре игры
///    (Kingdom.isInWarWith / WarManager.getWar / рост wars.list), а не потому,
///    что метод «не упал».
/// </summary>
public static class WarApiProbe
{
	private static readonly HashSet<string> _logged = new HashSet<string>();

	private static bool _described;

	/// <summary>Сообщение журнала с защитой от повторов (одинаковый текст пишется один раз).</summary>
	private static void Say(string message)
	{
		if (_logged.Add(message))
		{
			LogService.LogInfo("[Real Politic] война/API: " + message);
		}
	}

	/// <summary>Выгружает в журнал фактические сигнатуры военного API (один раз за запуск).</summary>
	public static void DescribeOnce()
	{
		if (_described)
		{
			return;
		}
		_described = true;
		try
		{
			Say("WarTypeLibrary: " + DescribeLibrary());
			Say("diplomacy.startWar: " + DescribeMethods(ReadField(World.world, "diplomacy"), "startWar"));
			Say("WarManager.newWar: " + DescribeMethods(ReadField(World.world, "wars"), "newWar"));
			Say("WarManager.getWar: " + DescribeMethods(ReadField(World.world, "wars"), "getWar"));
			Say("реестр войн: " + WarCount() + " шт.");
		}
		catch (Exception ex)
		{
			Say("описание API не удалось: " + ex.Message);
		}
	}

	/// <summary>
	/// Начинает войну и подтверждает её по реестру игры.
	/// Возвращает true только если война действительно есть в игре.
	/// </summary>
	public static bool EnsureWar(Kingdom attacker, Kingdom defender, string assetName, out string verdict)
	{
		DescribeOnce();
		object asset = StaticField("WarTypeLibrary", assetName);
		if (asset == null)
		{
			verdict = "в WarTypeLibrary нет поля «" + assetName + "» (" + DescribeLibrary() + ")";
			Say("тип войны не найден: " + verdict);
			return false;
		}
		object diplomacy = ReadField(World.world, "diplomacy");
		object wars = ReadField(World.world, "wars");
		return EnsureWar(diplomacy, wars, attacker, defender, asset as WarTypeAsset, out verdict);
	}

	/// <summary>
	/// Основная логика, отделённая от статики игры: её можно проверить на заглушках.
	/// <paramref name="asset"/> может быть null — тогда тип войны подставит сама игра.
	/// </summary>
	public static bool EnsureWar(object diplomacy, object wars, Kingdom attacker, Kingdom defender, WarTypeAsset asset, out string verdict)
	{
		verdict = "не выполнено";
		if (attacker == null || defender == null || ReferenceEquals(attacker, defender))
		{
			verdict = "некорректная пара государств";
			return false;
		}
		int before = CountWars(wars);
		List<string> attempts = new List<string>();

		// 1. Штатный путь: DiplomacyManager.startWar(...).
		bool returnedObject;
		InvokeAll(diplomacy, "startWar", attacker, defender, asset, attempts, out returnedObject);

		// 2. Запасной путь: WarManager.newWar(Kingdom, Kingdom, WarTypeAsset).
		//    По декомпиляции startWar делегирует именно сюда. Пробуем, только если
		//    первый путь не дал ни объекта, ни новой записи в реестре.
		if (!returnedObject && CountWars(wars) <= before)
		{
			InvokeAll(wars, "newWar", attacker, defender, asset, attempts, out returnedObject);
		}

		if (wars != null)
		{
			TryInvoke(wars, "checkLists", out _);
		}

		bool atWar = DiplomacyExecutor.AreAtWar(attacker, defender);
		int after = CountWars(wars);
		// Реестр игры читается (в журнале видны войны) — верим только ему.
		// Реестр недоступен (заглушки, другая версия игры) — единственный признак это
		// возвращённый объект войны, иначе проверка станет недоказуемой.
		bool registryReadable = before >= 0 && after >= 0;
		bool grown = registryReadable && after > before;
		bool ok = atWar || grown || (!registryReadable && returnedObject);
		verdict = (attempts.Count == 0 ? "вызовов не было" : string.Join(" | ", attempts))
			+ "; войн в реестре: " + before + " → " + after
			+ "; isInWarWith: " + (atWar ? "да" : "нет")
			+ "; итог: " + (ok ? "война есть" : "войны нет");
		Say(verdict);
		return ok;
	}

	/// <summary>Сколько войн сейчас в реестре игры.</summary>
	public static int WarCount()
	{
		return CountWars(ReadField(World.world, "wars"));
	}

	private static int CountWars(object wars)
	{
		object list = ReadField(wars, "list");
		if (list is System.Collections.ICollection collection)
		{
			return collection.Count;
		}
		return (list is System.Collections.IList items) ? items.Count : -1;
	}

	/// <summary>
	/// Перебирает ВСЕ перегрузки метода с данным именем и подставляет аргументы по типам.
	/// Возвращает true, если хотя бы один вызов дошёл до игры без исключения.
	/// </summary>
	private static void InvokeAll(object target, string name, Kingdom attacker, Kingdom defender, WarTypeAsset asset, List<string> attempts, out bool returnedObject)
	{
		returnedObject = false;
		if (target == null)
		{
			attempts.Add(name + ": объект отсутствует");
			return;
		}
		foreach (MethodInfo method in AllMethods(target.GetType(), name))
		{
			ParameterInfo[] parameters = method.GetParameters();
			object[] args = new object[parameters.Length];
			bool usable = true;
			int kingdomSlot = 0;
			for (int i = 0; i < parameters.Length; i++)
			{
				Type pt = parameters[i].ParameterType;
				if (typeof(Kingdom).IsAssignableFrom(pt))
				{
					args[i] = (kingdomSlot++ == 0) ? attacker : defender;
					if (args[i] == null)
					{
						usable = false;
					}
				}
				else if (asset != null && pt.IsInstanceOfType(asset))
				{
					args[i] = asset;
				}
				else if (pt == typeof(bool))
				{
					args[i] = true;
				}
				else if (parameters[i].HasDefaultValue)
				{
					args[i] = parameters[i].DefaultValue;
				}
				else if (pt.IsValueType)
				{
					// Неизвестный значимый параметр (enum, int): рискуем нулём только если он необязательный.
					args[i] = parameters[i].HasDefaultValue ? parameters[i].DefaultValue : Activator.CreateInstance(pt);
				}
				else
				{
					args[i] = null;
				}
			}
			if (kingdomSlot < 2)
			{
				attempts.Add(Signature(method) + " — пропущено: не двухсторонняя война");
				continue;
			}
			if (!usable)
			{
				attempts.Add(Signature(method) + " — пропущено: нет государства");
				continue;
			}
			try
			{
				object result = method.Invoke(target, args);
				if (result != null)
				{
					returnedObject = true;
				}
				attempts.Add(Signature(method) + " → " + (result == null ? "null" : "объект " + result.GetType().Name));
			}
			catch (TargetInvocationException ex)
			{
				attempts.Add(Signature(method) + " → исключение: " + (ex.InnerException?.Message ?? ex.Message));
			}
			catch (Exception ex)
			{
				attempts.Add(Signature(method) + " → ошибка: " + ex.Message);
			}
		}
		if (attempts.Count == 0)
		{
			attempts.Add(name + ": перегрузок не найдено");
		}
	}

	private static IEnumerable<MethodInfo> AllMethods(Type type, string name)
	{
		const BindingFlags flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly;
		for (Type current = type; current != null; current = current.BaseType)
		{
			foreach (MethodInfo method in current.GetMethods(flags))
			{
				if (method.Name == name && !method.ContainsGenericParameters)
				{
					yield return method;
				}
			}
		}
	}

	private static string Signature(MethodInfo method)
	{
		ParameterInfo[] parameters = method.GetParameters();
		string text = method.Name + "(";
		for (int i = 0; i < parameters.Length; i++)
		{
			text += (i == 0 ? "" : ", ") + Short(parameters[i].ParameterType);
		}
		return text + ") : " + Short(method.ReturnType);
	}

	private static string DescribeMethods(object target, string name)
	{
		if (target == null)
		{
			return "объект отсутствует";
		}
		List<string> found = new List<string>();
		foreach (MethodInfo method in AllMethods(target.GetType(), name))
		{
			found.Add(Signature(method));
		}
		return found.Count == 0 ? "перегрузок нет" : string.Join(" | ", found);
	}

	/// <summary>Поля WarTypeLibrary — чтобы сразу видеть, какие типы войн реально есть в игре.</summary>
	private static string DescribeLibrary()
	{
		Type type = Patches.FindGameType("WarTypeLibrary");
		if (type == null)
		{
			return "тип не найден";
		}
		List<string> fields = new List<string>();
		foreach (FieldInfo field in type.GetFields(BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic))
		{
			fields.Add(field.Name + ":" + Short(field.FieldType));
		}
		return fields.Count == 0 ? "полей нет" : string.Join(", ", fields);
	}

	private static string Short(Type type)
	{
		return type == null ? "?" : type.Name;
	}

	private static object ReadField(object target, string name)
	{
		if (target == null)
		{
			return null;
		}
		const BindingFlags flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly;
		for (Type current = target.GetType(); current != null; current = current.BaseType)
		{
			FieldInfo field = current.GetField(name, flags);
			if (field != null)
			{
				return field.GetValue(target);
			}
		}
		return null;
	}

	private static object StaticField(string typeName, string name)
	{
		Type type = Patches.FindGameType(typeName);
		if (type == null)
		{
			return null;
		}
		return type.GetField(name, BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic)?.GetValue(null);
	}

	private static bool TryInvoke(object target, string name, out object result)
	{
		result = null;
		if (target == null)
		{
			return false;
		}
		foreach (MethodInfo method in AllMethods(target.GetType(), name))
		{
			try
			{
				result = method.Invoke(target, new object[method.GetParameters().Length]);
				return true;
			}
			catch (Exception)
			{
				return false;
			}
		}
		return false;
	}
}
