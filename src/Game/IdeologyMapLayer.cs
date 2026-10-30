using System;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using NeoModLoader.services;
using RealPolitic.Core;
using UnityEngine;

namespace RealPolitic;

/// <summary>
/// Слой карты по идеологиям: государства с одинаковой идеологией сливаются
/// в одну зону (как штатный «Слой культур»).
///
/// Как это работает (важно для будущих правок):
/// у игры нет свободного значения MetaType под идеологию, а свой MetaTypeAsset
/// требует типизированных делегатов, которых нет в заглушках. Поэтому:
///   1. при включении слоя включается штатная опция «слой королевств»
///      (map_kingdom_layer) — игра начинает рисовать мета-зоны;
///   2. Harmony-префикс на ZoneCalculator.drawZoneMeta(зона, ассет, делегат)
///      полностью заменяет отрисовку: зона красится ФИКСИРОВАННЫМ цветом своей
///      идеологии (Ideologies.LayerColor), а граница рисуется только там, где у
///      соседней зоны другая идеология. Поэтому все государства одной идеологии
///      сливаются в общую одноцветную зону;
///   3. подписи зон заменяются на название идеологии (NameplatePrefix).
///
/// Все параметры патча — object: типизированные (TileZone/MetaTypeAsset) ломают
/// Harmony в игре с «IL Compile Error (unknown location)».
/// </summary>
public static class IdeologyMapLayer
{
	/// <summary>Опция игры, которую слой включает «под собой», чтобы зоны вообще рисовались.</summary>
	private const string HostOption = "map_kingdom_layer";

	private static bool _active;

	private static bool _patched;

	private static bool _hostWasEnabled;

	private static MethodInfo _applyColors;

	private static Type _color32Type;

	/// <summary>Текст, которым нужно заменить подпись текущей таблички.</summary>
	private static string _labelOverride;

	/// <summary>Табличка → подпись идеологии, назначенная ей в этом кадре.</summary>
	private static readonly Dictionary<object, string> _labelByPlate = new Dictionary<object, string>();

	/// <summary>Идеология → id государства, несущего подпись зоны.</summary>
	private static readonly Dictionary<IdeologyType, int> _labelOwners = new Dictionary<IdeologyType, int>();

	private static float _nextOwnerRefresh;

	public static bool Active => _active;

	// ---------------------------------------------------------------- включение

	/// <summary>Переключить слой. Вызывается кнопкой в панели сил и горячей клавишей.</summary>
	public static void Toggle()
	{
		SetActive(!_active);
	}

	public static void SetActive(bool value)
	{
		if (_active == value)
		{
			return;
		}
		_active = value;
		try
		{
			if (_active)
			{
				_hostWasEnabled = OptionEnabled(HostOption);
				SetOption(HostOption, true);
				LogService.LogInfo("[Real Politic] слой идеологий ВКЛЮЧЁН");
			}
			else
			{
				SetOption(HostOption, _hostWasEnabled);
				LogService.LogInfo("[Real Politic] слой идеологий выключен");
			}
			_labelByPlate.Clear();
			ResetZoneRenderCache();
			_paintedZones = 0;
			_emptyZones = 0;
			_statsLogged = false;
			RedrawZones();
		}
		catch (Exception ex)
		{
			LogService.LogError("[Real Politic] ошибка переключения слоя идеологий: " + ex.Message);
		}
	}

	/// <summary>
	/// Сбрасывает кэш отрисовки у всех зон. Без этого checkShouldReRender считает,
	/// что зона уже нарисована нужным цветом, и слой остаётся незакрашенным.
	/// </summary>
	private static void ResetZoneRenderCache()
	{
		try
		{
			MapBox map = World.world;
			object calculator = GameRefs.ReadMember(map, "zone_calculator") ?? GameRefs.ReadMember(map, "zone_manager");
			object zones = (calculator == null) ? null : GameRefs.ReadMember(calculator, "zones");
			System.Collections.IEnumerable list = zones as System.Collections.IEnumerable;
			if (list == null)
			{
				return;
			}
			int n = 0;
			foreach (object zone in list)
			{
				GameRefs.Invoke(zone, "resetRenderHelpers");
				n++;
			}
			LogService.LogInfo("[Real Politic] сброшен кэш отрисовки зон: " + n);
		}
		catch (Exception ex)
		{
			Patches.LogOncePublic("не удалось сбросить кэш зон: " + ex.Message);
		}
	}

	// ---------------------------------------------------------------- патч отрисовки

	public static int Install(Harmony harmony)
	{
		if (_patched)
		{
			return 0;
		}
		try
		{
			Type zoneCalculator = Patches.FindGameType("ZoneCalculator");
			if (zoneCalculator == null)
			{
				LogService.LogError("[Real Politic] ZoneCalculator не найден — слой идеологий недоступен");
				return 0;
			}
			MethodInfo target = null;
			MethodInfo[] methods = zoneCalculator.GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly);
			for (int i = 0; i < methods.Length; i++)
			{
				if (methods[i].Name != "drawZoneMeta")
				{
					continue;
				}
				ParameterInfo[] ps = methods[i].GetParameters();
				if (ps.Length == 3)
				{
					target = methods[i];
				}
			}
			if (target == null)
			{
				LogService.LogError("[Real Politic] ZoneCalculator.drawZoneMeta не найден — слой идеологий недоступен");
				return 0;
			}
			harmony.Patch(target, new HarmonyMethod(typeof(IdeologyMapLayer), "DrawZoneMetaPrefix", null), null, null, null, null);

			// Слой королевств может рисоваться «текучим» путём (drawGenericFluid),
			// тогда drawZoneMeta вообще не вызывается и зоны остаются незакрашенными.
			MethodInfo fluid = null;
			for (int i = 0; i < methods.Length; i++)
			{
				if (methods[i].Name == "drawGenericFluid")
				{
					fluid = methods[i];
				}
			}
			if (fluid != null)
			{
				harmony.Patch(fluid, new HarmonyMethod(typeof(IdeologyMapLayer), "DrawGenericFluidPrefix", null), null, null, null, null);
				LogService.LogInfo("[Real Politic] патч слоя идеологий (fluid) установлен");
			}
			else
			{
				LogService.LogInfo("[Real Politic] drawGenericFluid не найден — работает только обычный путь отрисовки");
			}
			_patched = true;
			LogService.LogInfo("[Real Politic] патч слоя идеологий установлен");
			int count = 1;
			count += PatchNameplates(harmony);
			return count;
		}
		catch (Exception ex)
		{
			LogService.LogError("[Real Politic] не удалось установить патч слоя идеологий: " + ex.Message);
			return 0;
		}
	}

	/// <summary>Патчи подписей: заменяем название королевства на название идеологии.</summary>
	private static int PatchNameplates(Harmony harmony)
	{
		try
		{
			Type plate = Patches.FindGameType("NameplateText");
			if (plate == null)
			{
				LogService.LogError("[Real Politic] NameplateText не найден — подписи зон останутся названиями королевств");
				return 0;
			}
			MethodInfo show = plate.GetMethod("showTextKingdom", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
			MethodInfo setText = null;
			MethodInfo[] methods = plate.GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly);
			for (int i = 0; i < methods.Length; i++)
			{
				if (methods[i].Name == "setText")
				{
					setText = methods[i];
					break;
				}
			}
			if (show == null || setText == null)
			{
				LogService.LogError("[Real Politic] NameplateText.showTextKingdom/setText не найдены — подписи зон не меняются");
				return 0;
			}
			harmony.Patch(show, new HarmonyMethod(typeof(IdeologyMapLayer), "ShowTextKingdomPrefix", null), null, null, null, null);
			harmony.Patch(setText, null, new HarmonyMethod(typeof(IdeologyMapLayer), "SetTextPostfix", null), null, null, null);
			LogService.LogInfo("[Real Politic] патч подписей зон по идеологиям установлен");
			return 1;
		}
		catch (Exception ex)
		{
			LogService.LogError("[Real Politic] не удалось установить патч подписей зон: " + ex.Message);
			return 0;
		}
	}

	/// <summary>
	/// Префикс отрисовки зоны. Когда слой активен — рисуем зону мета-объектом
	/// «представитель идеологии» и запрещаем оригинал (return false).
	/// </summary>
	/// <summary>Сколько зон реально покрашено — для диагностики в логе.</summary>
	private static int _paintedZones;

	/// <summary>Сколько зон оказалось «ничьими» (нет города/королевства/состояния).</summary>
	private static int _emptyZones;

	private static bool _statsLogged;

	private static bool DrawZoneMetaPrefix(object __instance, object pZone)
	{
		if (!_active)
		{
			return true;
		}
		try
		{
			IdeologyType? ideology = IdeologyOf(pZone);
			if (ideology == null)
			{
				_emptyZones++;
			}
			if (!_statsLogged && _paintedZones + _emptyZones >= 200)
			{
				_statsLogged = true;
				LogService.LogInfo("[Real Politic] слой идеологий: покрашено зон " + _paintedZones + ", ничьих " + _emptyZones);
			}
			if (ideology == null)
			{
				// Ничья земля / нет данных — очищаем зону, чтобы не осталась старая заливка.
				GameRefs.Invoke(__instance, "drawZoneClear", pZone);
				return false;
			}
			IdeologyType value = ideology.Value;
			bool up = IsBorder(GameRefs.ReadMember(pZone, "zone_up"), value);
			bool down = IsBorder(GameRefs.ReadMember(pZone, "zone_down"), value);
			bool left = IsBorder(GameRefs.ReadMember(pZone, "zone_left"), value);
			bool right = IsBorder(GameRefs.ReadMember(pZone, "zone_right"), value);
			// Свой фиксированный цвет идеологии: заливка полупрозрачная, граница — насыщенная.
			Ideologies.LayerColor(value, out byte r, out byte g, out byte b);
			object inside = MakeColor32(r, g, b, 140);
			object outside = MakeColor32((byte)Math.Min(255, r + 45), (byte)Math.Min(255, g + 45), (byte)Math.Min(255, b + 45), 255);
			// Перерисовываем только при изменениях (иначе лишняя работа каждый кадр).
			int id = ((int)value + 1) * 31 + (up ? 1 : 0) + (down ? 2 : 0) + (left ? 4 : 0) + (right ? 8 : 0);
			object should = GameRefs.Invoke(pZone, "checkShouldReRender", (int)value + 1, id, 0, false);
			if (should is false)
			{
				return false;
			}
			ApplyColors(__instance, pZone, inside, outside, up, down, left, right);
			return false;
		}
		catch (Exception ex)
		{
			Patches.LogOncePublic("ошибка отрисовки слоя идеологий: " + ex.Message);
			return true;
		}
	}

	/// <summary>
	/// «Текучий» путь отрисовки зон. Данные приходят структурой ZoneMetaData
	/// (в ней зона и мета-объект), красим её тем же цветом идеологии.
	/// </summary>
	private static bool DrawGenericFluidPrefix(object __instance, object pData)
	{
		if (!_active)
		{
			return true;
		}
		try
		{
			object zone = GameRefs.ReadMember(pData, "zone");
			if (zone == null)
			{
				return true;
			}
			IdeologyType? ideology = IdeologyOf(zone);
			if (ideology == null)
			{
				_emptyZones++;
				GameRefs.Invoke(__instance, "drawZoneClear", zone);
				return false;
			}
			IdeologyType value = ideology.Value;
			Ideologies.LayerColor(value, out byte r, out byte g, out byte b);
			object inside = MakeColor32(r, g, b, 150);
			ApplyColorsFull(__instance, zone, inside);
			return false;
		}
		catch (Exception ex)
		{
			Patches.LogOncePublic("ошибка отрисовки слоя идеологий (fluid): " + ex.Message);
			return true;
		}
	}

	/// <summary>Сплошная заливка зоны — applyMetaColorsToZoneFull(zone, ref Color32).</summary>
	private static void ApplyColorsFull(object calculator, object zone, object inside)
	{
		if (_applyColorsFull == null)
		{
			for (Type t = calculator.GetType(); t != null && _applyColorsFull == null; t = t.BaseType)
			{
				MethodInfo[] ms = t.GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly);
				for (int i = 0; i < ms.Length; i++)
				{
					if (ms[i].Name == "applyMetaColorsToZoneFull" && ms[i].GetParameters().Length == 2)
					{
						_applyColorsFull = ms[i];
						break;
					}
				}
			}
		}
		if (_applyColorsFull == null)
		{
			return;
		}
		try
		{
			_applyColorsFull.Invoke(calculator, new object[2] { zone, inside });
			_paintedZones++;
		}
		catch (Exception ex)
		{
			Patches.LogOncePublic("applyMetaColorsToZoneFull бросил исключение: " + (ex.InnerException?.Message ?? ex.Message));
		}
	}

	private static MethodInfo _applyColorsFull;

	/// <summary>Вызов applyMetaColorsToZone: у него параметры Color32 по ref, поэтому через массив аргументов.</summary>
	private static void ApplyColors(object calculator, object zone, object inside, object outside, bool up, bool down, bool left, bool right)
	{
		if (_applyColors == null)
		{
			// Ищем по всей иерархии типов и среди непубличных — сигнатура с ref-параметрами
			// не всегда находится обычным GetMethod.
			for (Type t = calculator.GetType(); t != null && _applyColors == null; t = t.BaseType)
			{
				MethodInfo[] ms = t.GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly);
				for (int i = 0; i < ms.Length; i++)
				{
					if (ms[i].Name == "applyMetaColorsToZone" && ms[i].GetParameters().Length == 7)
					{
						_applyColors = ms[i];
						break;
					}
				}
			}
			if (_applyColors == null)
			{
				LogService.LogError("[Real Politic] applyMetaColorsToZone не найден — слой идеологий не сможет красить зоны (" + calculator.GetType().Name + ")");
			}
			else
			{
				LogService.LogInfo("[Real Politic] applyMetaColorsToZone найден на " + _applyColors.DeclaringType.Name);
			}
		}
		if (_applyColors == null)
		{
			return;
		}
		// Сигнатура: (TileZone, ref Color32, ref Color32, bool, bool, bool, bool)
		try
		{
			_applyColors.Invoke(calculator, new object[7] { zone, inside, outside, up, down, left, right });
			_paintedZones++;
		}
		catch (Exception ex)
		{
			Patches.LogOncePublic("applyMetaColorsToZone бросил исключение: " + (ex.InnerException?.Message ?? ex.Message));
		}
	}

	private static object MakeColor32(byte r, byte g, byte b, byte a)
	{
		if (_color32Type == null)
		{
			_color32Type = Type.GetType("UnityEngine.Color32, UnityEngine.CoreModule");
		}
		return Activator.CreateInstance(_color32Type, new object[4] { r, g, b, a });
	}

	/// <summary>Идеология владельца зоны (null — зона ничья).</summary>
	private static IdeologyType? IdeologyOf(object zone)
	{
		object city = GameRefs.ReadMember(zone, "city");
		if (city == null)
		{
			return null;
		}
		Kingdom kingdom = GameRefs.ReadMember(city, "kingdom") as Kingdom;
		if (kingdom == null)
		{
			return null;
		}
		KingdomPolitics state = RealPoliticMod.World?.GetKingdom((int)((NanoObject)kingdom).id);
		if (state == null)
		{
			return null;
		}
		return state.Ideology;
	}

	/// <summary>Граница рисуется там, где у соседней зоны ДРУГАЯ идеология.</summary>
	private static bool IsBorder(object neighbourZone, IdeologyType ideology)
	{
		if (neighbourZone == null)
		{
			return true;
		}
		IdeologyType? other = IdeologyOf(neighbourZone);
		return other == null || other.Value != ideology;
	}

	// ---------------------------------------------------------------- подписи зон

	/// <summary>
	/// Подписи на карте. Игра рисует по табличке на каждое королевство
	/// (NameplateText.showTextKingdom). При активном слое подпись заменяется на
	/// НАЗВАНИЕ ИДЕОЛОГИИ («Монархия»), и показывается только одна табличка на
	/// идеологию — у крупнейшего её государства, иначе надписи дублируются.
	/// </summary>
	private static bool ShowTextKingdomPrefix(object __instance, object pMetaObject)
	{
		if (!_active)
		{
			return true;
		}
		try
		{
			Kingdom kingdom = pMetaObject as Kingdom;
			if (kingdom == null)
			{
				return true;
			}
			KingdomPolitics state = RealPoliticMod.World?.GetKingdom((int)((NanoObject)kingdom).id);
			if (state == null)
			{
				return true;
			}
			// Показываем подпись только у «главного» государства идеологии.
			if (!IsLabelOwner(kingdom, state.Ideology))
			{
				// Не владелец подписи — прячем табличку целиком, чтобы на карте
				// не оставалось названий отдельных королевств.
				HidePlate(__instance);
				return false;
			}
			string label = GameText.Localized(Ideologies.NameKey(state.Ideology), state.Ideology.ToString());
			// Привязываем подпись к КОНКРЕТНОЙ табличке: игра переиспользует таблички
			// из пула и зовёт setText для всех подряд, поэтому общая переменная
			// «прилипала» к чужой табличке и надписи съезжали при движении камеры.
			_labelByPlate[__instance] = label;
			_labelOverride = label;
			return true;
		}
		catch (Exception ex)
		{
			Patches.LogOncePublic("ошибка подписи слоя идеологий: " + ex.Message);
			return true;
		}
	}

	/// <summary>
	/// Гасит табличку целиком. Очистки текста мало: сама плашка (фон, баннеры,
	/// иконки) остаётся на карте чёрным прямоугольником, поэтому выключаем
	/// игровой объект штатным механизмом setShowing(false) + checkActive().
	/// </summary>
	private static void HidePlate(object plate)
	{
		try
		{
			_labelByPlate.Remove(plate);
			GameRefs.Invoke(plate, "setShowing", false);
			GameRefs.Invoke(plate, "checkActive");
		}
		catch (Exception)
		{
		}
	}

	/// <summary>Подменяем текст таблички сразу после того, как игра его выставила.</summary>
	private static void SetTextPostfix(object __instance)
	{
		if (!_active)
		{
			return;
		}
		// Только та табличка, которую мы сами пометили в префиксе.
		if (!_labelByPlate.TryGetValue(__instance, out string label) || label == null)
		{
			return;
		}
		_labelOverride = label;
		try
		{
			object textName = GameRefs.ReadMember(__instance, "_text_name");
			if (textName != null)
			{
				PropertyInfo textProp = textName.GetType().GetProperty("text", BindingFlags.Instance | BindingFlags.Public);
				textProp?.SetValue(textName, _labelOverride, null);
			}
		}
		catch (Exception)
		{
		}
		finally
		{
			_labelOverride = null;
			_labelByPlate.Remove(__instance);
		}
	}

	/// <summary>Крупнейшее государство идеологии — только оно несёт подпись. Пересчёт раз в 2 с.</summary>
	private static bool IsLabelOwner(Kingdom kingdom, IdeologyType ideology)
	{
		float now = Time.unscaledTime;
		if (now >= _nextOwnerRefresh)
		{
			_nextOwnerRefresh = now + 2f;
			_labelOwners.Clear();
			try
			{
				List<Kingdom> list = ((CoreSystemManager<Kingdom, KingdomData>)(object)World.world.kingdoms).list;
				Dictionary<IdeologyType, int> best = new Dictionary<IdeologyType, int>();
				for (int i = 0; list != null && i < list.Count; i++)
				{
					Kingdom k = list[i];
					if (k == null)
					{
						continue;
					}
					KingdomPolitics st = RealPoliticMod.World?.GetKingdom((int)((NanoObject)k).id);
					if (st == null)
					{
						continue;
					}
					// Государства без городов не должны претендовать на подпись,
					// иначе на карте появляются лишние дубли названия идеологии.
					List<City> kcities = GameRefs.KingdomCities(k);
					if (kcities == null || kcities.Count == 0)
					{
						continue;
					}
					// Сравниваем по населению, а при равенстве — по id, чтобы выбор был
					// однозначным и не «прыгал» между двумя одинаковыми государствами.
					int pop = GameRefs.KingdomPopulation(k) * 1000 + 999 - Math.Min(999, (int)((NanoObject)k).id);
					if (!best.TryGetValue(st.Ideology, out int cur) || pop > cur)
					{
						best[st.Ideology] = pop;
						_labelOwners[st.Ideology] = (int)((NanoObject)k).id;
					}
				}
			}
			catch (Exception)
			{
			}
		}
		return _labelOwners.TryGetValue(ideology, out int ownerId) && ownerId == (int)((NanoObject)kingdom).id;
	}

	// ---------------------------------------------------------------- мелкие помощники

	private static object CurrentMapAsset()
	{
		Type zones = Patches.FindGameType("Zones");
		return zones?.GetMethod("getMapMetaAsset", BindingFlags.Static | BindingFlags.Public)?.Invoke(null, null);
	}

	private static bool OptionEnabled(string id)
	{
		Type config = Patches.FindGameType("PlayerConfig");
		object result = config?.GetMethod("optionBoolEnabled", BindingFlags.Static | BindingFlags.Public)?.Invoke(null, new object[1] { id });
		return result is true;
	}

	private static void SetOption(string id, bool value)
	{
		Type config = Patches.FindGameType("PlayerConfig");
		config?.GetMethod("setOptionBool", BindingFlags.Static | BindingFlags.Public)?.Invoke(null, new object[2] { id, value });
	}

	/// <summary>Сбрасывает кэш отрисованных зон, чтобы карта перерисовалась сразу после переключения.</summary>
	private static void RedrawZones()
	{
		try
		{
			object calculator = GameRefs.ReadMember(World.world, "zone_calculator");
			if (calculator == null)
			{
				return;
			}
			GameRefs.Invoke(calculator, "setDrawnZonesDirty");
			GameRefs.Invoke(calculator, "dirtyAndClear");
		}
		catch (Exception)
		{
		}
	}
}
