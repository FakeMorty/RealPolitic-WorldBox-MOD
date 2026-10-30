using System;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using NeoModLoader.services;
using RealPolitic.Core;

namespace RealPolitic;

/// <summary>
/// 0.39: запрет войн на съезде КМУД работает ДО объявления, а не после.
///
/// Зачем переделывать. В партии от 28.10.2026 (летопись «Мир 2026-10-28 12-36»)
/// запрет войн действовал, но войска всё равно объявляли войны, а мод их гасил.
/// В летопись попало 23 строки «объявленная война отменена — действует запрет войн»,
/// и получалось, что запрет не уменьшает число войн, а только прячет результат.
/// Решение владельца от 29.10.2026: «Блокировка».
///
/// Как устроено:
///  1. Свои войны мод объявляет через <see cref="DiplomacyExecutor"/> — там запрет
///     проверяется до вызова игры (это работает всегда).
///  2. Войны, которые начинает сам WorldBox (его ИИ), перехватываются префиксом
///     Harmony на DiplomacyManager.startWar и WarManager.newWar. Патч ставится
///     в try/catch: если игра его не примет, мод продолжит работать как раньше —
///     со старым гашением войн (ошибка попадёт в журнал одной строкой).
///  3. Каждая отбитая война считается: в журнал пишется одна строка за год,
///     а не строка на каждую попытку.
/// </summary>
public static class KmudWarBan
{
	/// <summary>Сколько объявлений войны отбито за этот год (для одной строки в журнале).</summary>
	private static int _preventedThisYear;

	private static int _loggedYear = -1;

	private static int _totalPrevented;

	private static readonly HashSet<string> _logged = new HashSet<string>();

	/// <summary>Всего отбито объявлений войны с начала партии (для окна КМУД и журнала).</summary>
	public static int TotalPrevented => _totalPrevented;

	/// <summary>Сброс при смене мира.</summary>
	public static void Reset()
	{
		_preventedThisYear = 0;
		_loggedYear = -1;
		_totalPrevented = 0;
	}

	/// <summary>
	/// Можно ли начинать войну между этими двумя державами. true — можно,
	/// false — запрещено (действует пауза съезда или пакт «только оборона»).
	/// Отбитая попытка считается и попадает в журнал, но не в летопись:
	/// жители не должны читать по строке на каждую попытку.
	/// </summary>
	public static bool AllowStart(Kingdom attacker, Kingdom defender)
	{
		try
		{
			if (attacker == null || defender == null || ReferenceEquals(attacker, defender))
			{
				return true;
			}
			PoliticalWorld world = RealPoliticMod.World;
			KmudState state = world?.Kmud;
			if (state == null || !state.WasHeld)
			{
				return true;
			}
			int attackerId = (int)((NanoObject)attacker).id;
			int defenderId = (int)((NanoObject)defender).id;
			if (!KmudRules.WarForbidden(state, attackerId, defenderId, world.Year, out string reason))
			{
				return true;
			}
			_preventedThisYear++;
			_totalPrevented++;
			state.WarsPrevented = state.WarsPrevented + 1;
			LogOnce(world.Year, reason, world.KingdomNameOf(attackerId), world.KingdomNameOf(defenderId));
			return false;
		}
		catch (Exception ex)
		{
			LogOnce("ошибка проверки запрета войн: " + ex.Message);
			// Ошибка проверки не должна ломать игру: пропускаем войну как раньше,
			// её погасит старый сторож KmudRuntime.Tick.
			return true;
		}
	}

	/// <summary>Строка в журнал: одна на год, чтобы не забивать Player.log.</summary>
	private static void LogOnce(int year, string reason, string attacker, string defender)
	{
		if (_loggedYear == year)
		{
			return;
		}
		_loggedYear = year;
		LogService.LogInfo("[Real Politic] КМУД: война не объявлена — " + reason
			+ " (" + attacker + " против " + defender + "). Отбито за год: " + _preventedThisYear
			+ ", всего: " + _totalPrevented);
		// 0.44: об отбитых войнах теперь слышно и в уведомлениях, а не только в журнале мода.
		// Владелец 29.10.2026: «почему в уведомлениях ни словом ни духом о КМУДе?» —
		// событие KmudWarPrevented было объявлено в коде, но НИ РАЗУ не публиковалось.
		PublishPrevented(year);
		_preventedThisYear = 0;
	}

	/// <summary>0.44: одно уведомление за год — сколько войн не дали объявить и до какого года запрет.</summary>
	private static void PublishPrevented(int year)
	{
		try
		{
			PoliticalWorld world = RealPoliticMod.World;
			if (world?.Bus == null)
			{
				return;
			}
			PolEvent prevented = new PolEvent(PolEventType.KmudWarPrevented, "RP_EV_KMUD_WAR_PREVENTED") { Year = year };
			prevented.Severity = Severity.Major;
			world.Bus.Publish(prevented
				.With("count", _totalPrevented)
				.With("year_count", _preventedThisYear)
				.With("until", world.Kmud?.TruceUntilYear ?? 0));
		}
		catch (Exception ex)
		{
			LogOnce("уведомление об отбитой войне: " + ex.Message);
		}
	}

	private static void LogOnce(string message)
	{
		if (_logged.Add(message))
		{
			LogService.LogInfo("[Real Politic] КМУД/запрет войн: " + message);
		}
	}

	/// <summary>
	/// Ставит перехват на объявление войны в самой игре. Возвращает число
	/// установленных патчей (0 — игра не дала, работает старое гашение).
	/// </summary>
	public static int Install(Harmony harmony)
	{
		if (harmony == null)
		{
			return 0;
		}
		int installed = 0;
		installed += PatchMethod(harmony, "DiplomacyManager", "startWar");
		installed += PatchMethod(harmony, "WarManager", "newWar");
		if (installed > 0)
		{
			LogService.LogInfo("[Real Politic] КМУД: запрет войн работает до объявления (перехватов: " + installed + ")");
		}
		else
		{
			LogService.LogError("[Real Politic] КМУД: перехватить объявление войны не удалось —"
				+ " работает прежнее гашение войн (строки «война отменена» вернутся)");
		}
		return installed;
	}

	private static int PatchMethod(Harmony harmony, string typeName, string methodName)
	{
		try
		{
			Type type = Patches.FindGameType(typeName);
			if (type == null)
			{
				LogOnce(typeName + " не найден");
				return 0;
			}
			MethodInfo method = null;
			MethodInfo[] all = type.GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
			for (int i = 0; i < all.Length; i++)
			{
				if (all[i].Name == methodName && all[i].GetParameters().Length >= 2)
				{
					method = all[i];
					break;
				}
			}
			if (method == null)
			{
				LogOnce(typeName + "." + methodName + " не найден");
				return 0;
			}
			harmony.Patch(method, new HarmonyMethod(typeof(KmudWarBan), "WarStartPrefix", null), null, null, null, null);
			return 1;
		}
		catch (Exception ex)
		{
			LogOnce(typeName + "." + methodName + ": " + ex.Message);
			return 0;
		}
	}

	/// <summary>
	/// Префикс объявления войны. Параметры — object[]: типизированные параметры
	/// в этом моде уже давали «IL Compile Error» при компиляции патча в игре.
	/// false — оригинальный метод не выполняется (война не начинается).
	/// </summary>
	private static bool WarStartPrefix(object[] __args)
	{
		try
		{
			Kingdom attacker = null;
			Kingdom defender = null;
			if (__args != null)
			{
				for (int i = 0; i < __args.Length; i++)
				{
					if (!(__args[i] is Kingdom kingdom))
					{
						continue;
					}
					if (attacker == null)
					{
						attacker = kingdom;
					}
					else if (!ReferenceEquals(kingdom, attacker))
					{
						defender = kingdom;
						break;
					}
				}
			}
			return AllowStart(attacker, defender);
		}
		catch (Exception ex)
		{
			LogOnce("ошибка перехвата объявления войны: " + ex.Message);
			return true;
		}
	}
}
