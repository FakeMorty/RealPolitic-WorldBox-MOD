using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using NeoModLoader.services;
using RealPolitic.Core;
using UnityEngine;

namespace RealPolitic;

public static class Patches
{
	public const string HarmonyId = "com.realpolitic.worldbox";

	public static bool DiplomacyEnabled = true;

	public static bool UnionsEnabled = true;

	private static Harmony _harmony;

	public static int Install()
	{
		//IL_0007: Unknown result type (might be due to invalid IL or missing references)
		//IL_0011: Expected O, but got Unknown
		int num = 0;
		try
		{
			_harmony = new Harmony("com.realpolitic.worldbox");
		}
		catch (Exception ex)
		{
			LogService.LogError("[Real Politic] Harmony недоступен: " + ex.Message);
			return 0;
		}
		// 0.39: запрет войн на съезде КМУД работает до объявления войны.
		// Если игра не примет перехват, мод продолжает работать со старым гашением.
		num += KmudWarBan.Install(_harmony);
		return num + PatchDeath() + PatchIdeologyDisplay() + PatchKingTitle() + PatchSelectedKingTitle() + PatchWars() + PatchLoyalty() + IdeologyMapLayer.Install(_harmony) + WorldPersistence.Install(_harmony);
	}

	/// <summary>
	/// Войны в летописи: postfix на WarManager.newWar (единая точка создания всех войн,
	/// включая тотальные и «из мести») и на WarManager.endWar (единая точка завершения).
	/// </summary>
	private static int PatchWars()
	{
		int num = 0;
		try
		{
			Type type = FindGameType("WarManager");
			if (type == null)
			{
				LogService.LogError("[Real Politic] WarManager не найден — войны не попадут в летопись");
				return 0;
			}
			// Harmony-патчи на newWar/endWar в этой версии игры стабильно валятся
			// с «IL Compile Error (unknown location)» — даже когда все параметры object.
			// Поэтому войны отслеживаются опросом состояния в GameWars.Poll().
			LogService.LogInfo("[Real Politic] войны отслеживаются опросом состояния (WarManager найден: " + (type != null) + ")");
		}
		catch (Exception ex)
		{
			LogService.LogError("[Real Politic] не удалось установить патчи войн: " + ex.Message);
		}
		return num;
	}

	// Параметры принимаем как object: типизированные (Kingdom/WarTypeAsset/War) приводили
	// к «IL Compile Error» при компиляции патча Harmony в игре (v0.6.0, Player.log).
	private static void WarStartedPostfix(object __instance, object pAttacker, object pDefender, object pAsset, object __result)
	{
		try
		{
			GameWars.OnWarStarted(pAttacker as Kingdom, pDefender as Kingdom, pAsset as WarTypeAsset, __result as War);
		}
		catch (Exception ex)
		{
			LogService.LogError("[Real Politic] ошибка в патче начала войны: " + ex.Message);
		}
	}

	private static void WarEndedPostfix(object __instance, object pWar, object pWinner)
	{
		try
		{
			GameWars.OnWarEnded(pWar as War, pWinner);
		}
		catch (Exception ex)
		{
			LogService.LogError("[Real Politic] ошибка в патче конца войны: " + ex.Message);
		}
	}

	/// <summary>
	/// Влияние идеологий на лояльность городов: постфикс на City.getLoyalty.
	/// Город, где значительная часть жителей не разделяет государственную идеологию,
	/// теряет лояльность (вплоть до ванильного восстания); идеологически однородный — получает.
	/// </summary>
	private static int PatchLoyalty()
	{
		try
		{
			Type type = FindGameType("City");
			if (type == null)
			{
				LogService.LogError("[Real Politic] City не найден — идеология не влияет на лояльность");
				return 0;
			}
			MethodInfo method = type.GetMethod("getLoyalty", BindingFlags.Instance | BindingFlags.Public);
			if (method == null)
			{
				LogService.LogError("[Real Politic] City.getLoyalty не найден — идеология не влияет на лояльность");
				return 0;
			}
			_harmony.Patch(method, null, new HarmonyMethod(typeof(Patches), "LoyaltyPostfix", null), null, null, null);
			LogService.LogInfo("[Real Politic] патч влияния идеологии на лояльность городов установлен");
			return 1;
		}
		catch (Exception ex)
		{
			LogService.LogError("[Real Politic] не удалось установить патч лояльности: " + ex.Message);
			return 0;
		}
	}

	private static void LoyaltyPostfix(object __instance, ref int __result)
	{
		// 0.53.1: этот постфикс вызывается у игры постоянно (окно, ИИ, каждый город).
		// Замер покажет в отчёте, сколько он реально стоит.
		PerfProbe.HotBegin();
		try
		{
			if (!IdeologyEffects.AffectLoyalty)
			{
				return;
			}
			City city = __instance as City;
			if (city == null)
			{
				return;
			}
			__result += IdeologyEffects.CityLoyaltyBonus(city);
		}
		catch (Exception ex)
		{
			LogOnce("ошибка поправки лояльности по идеологии: " + ex.Message);
		}
		finally
		{
			PerfProbe.HotEnd();
		}
	}

	/// <summary>Поиск типа игры: сначала Assembly-CSharp, затем все загруженные сборки.</summary>
	public static Type FindGameType(string name)
	{
		Type type = Type.GetType(name + ", Assembly-CSharp");
		if (type != null)
		{
			return type;
		}
		try
		{
			foreach (Assembly assembly in AppDomain.CurrentDomain.GetAssemblies())
			{
				try
				{
					type = assembly.GetType(name, false);
					if (type != null)
					{
						return type;
					}
				}
				catch (Exception)
				{
				}
			}
		}
		catch (Exception)
		{
		}
		return null;
	}

	private static int PatchKingTitle()
	{
		try
		{
			Type type = FindGameType("KingdomKingElement");
			if (type == null)
			{
				LogService.LogError("[Real Politic] KingdomKingElement не найден — титул правителя в окне лидера не меняется");
				return 0;
			}
			MethodInfo method = type.GetMethod("showContent", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
			if (method == null)
			{
				LogService.LogError("[Real Politic] KingdomKingElement.showContent не найден — титул правителя в окне лидера не меняется");
				return 0;
			}
			_harmony.Patch(method, new HarmonyMethod(typeof(Patches), "KingTitlePrefix", (Type[])null), null, null, null, null);
			LogService.LogInfo("[Real Politic] патч титула правителя (король/королева) в окне лидера установлен");
			return 1;
		}
		catch (Exception ex)
		{
			LogService.LogError("[Real Politic] не удалось установить патч титула правителя: " + ex.Message);
			return 0;
		}
	}

	/// <summary>Титул правителя по идеологии и полу (запасные значения — русские, как в Locales/ru.json).</summary>
	private static string FallbackTitle(IdeologyType ideology, bool female)
	{
		return ideology switch
		{
			IdeologyType.Tribalism => "вождь",
			IdeologyType.Theocracy => (female ? "верховная жрица" : "верховный жрец"),
			IdeologyType.Aristocracy => "консул",
			IdeologyType.MerchantRepublic => "дож",
			IdeologyType.Militarism => (female ? "военачальница" : "военачальник"),
			IdeologyType.Egalitarianism => "старейшина",
			IdeologyType.Technocracy => (female ? "председательница" : "председатель"),
			IdeologyType.Liberalism => (female ? "премьер-министр" : "премьер-министр"),
			IdeologyType.Democracy => "президент",
			IdeologyType.Communism => "генеральный секретарь",
			IdeologyType.Ethnonationalism => (female ? "верховная вождица" : "верховный вождь"),
			IdeologyType.Khaganate => (female ? "хатун" : "каган"),
			IdeologyType.Empire => (female ? "императрица" : "император"),
			IdeologyType.FreeTerritory => (female ? "атаманша" : "атаман"),
			_ => (female ? "королева" : "король"),
		};
	}

	/// <summary>Локализованный титул правителя (идеология + пол) с заглавной буквой.</summary>
	private static string RulerTitle(IdeologyType ideology, bool female)
	{
		return GameText.Capitalize(GameText.Localized(Ideologies.RulerTitleKey(ideology, female), FallbackTitle(ideology, female)));
	}

	private static void KingTitlePrefix(object __instance)
	{
		try
		{
			Kingdom kingdom = GameRefs.ReadProperty(__instance, "kingdom") as Kingdom;
			if (kingdom == null)
			{
				return;
			}
			Actor king = GameRefs.ReadRuler(kingdom);
			if (king == null)
			{
				return;
			}
			bool female = GameText.ActorIsFemale(king);
			IdeologyType ideology = RealPoliticMod.World?.GetKingdom((int)((NanoObject)kingdom).id)?.Ideology ?? IdeologyType.Monarchy;
			// Для монархии в локали есть ключи заголовка окна (RP_UI_KING/QUEEN — «Король»/«Королева»);
			// для остальных идеологий — RP_TITLE_*_M/F.
			string key = (ideology == IdeologyType.Monarchy) ? ((female ? "RP_UI_QUEEN" : "RP_UI_KING")) : Ideologies.RulerTitleKey(ideology, female);
			string title = RulerTitle(ideology, female);
			object titleElement = GameRefs.ReadProperty(__instance, "_title_element");
			ApplyKingTitle(titleElement, key, title);
		}
		catch (Exception ex)
		{
			LogService.LogError("[Real Politic] ошибка титула правителя: " + ex.Message);
		}
	}

	/// <summary>
	/// Подстановка заголовка «Король/Королева/…» в окне лидера. Двойное действие:
	/// 1) подменяем ключ LocalizedText — будущие перерисовки менеджером локализованных
	/// текстов игры возьмут наш ключ (в т. ч. при первом старте LocalizedText);
	/// 2) сразу прописываем текст в Text — строка корректна даже если перерисовка
	/// по ключу не произошла (ключ не найден, updateText отклонён и т. п.).
	/// </summary>
	private static void ApplyKingTitle(object titleElement, string key, string title)
	{
		try
		{
			if (titleElement == null)
			{
				return;
			}
			object lt = FindComponentInChildren(titleElement, "LocalizedText");
			if (lt != null)
			{
				Type ltType = lt.GetType();
				MethodInfo setKeyAndUpdate = ltType.GetMethod("setKeyAndUpdate", BindingFlags.Instance | BindingFlags.Public);
				if (setKeyAndUpdate != null)
				{
					setKeyAndUpdate.Invoke(lt, new object[1] { key });
				}
				else
				{
					FieldInfo keyField = ltType.GetField("key", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
					keyField?.SetValue(lt, key);
					MethodInfo updateText = ltType.GetMethod("updateText", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
					updateText?.Invoke(lt, new object[1] { true });
				}
				// Прямой текст на Text-компоненте LocalizedText (страховка).
				SetTextValue(GameRefs.ReadProperty(lt, "text"), title);
				LogOnce("титул в окне лидера: key=" + key + ", text='" + title + "' (LocalizedText)");
				return;
			}
			// Запасной вариант: обычный Text без LocalizedText.
			SetTextValue(FindComponentInChildren(titleElement, "Text"), title);
			LogOnce("титул в окне лидера: text='" + title + "' (Text)");
		}
		catch (Exception ex)
		{
			LogService.LogError("[Real Politic] не удалось подставить титул в окно лидера: " + ex.Message);
		}
	}

	private static void SetTextValue(object textObject, string value)
	{
		if (textObject == null)
		{
			return;
		}
		try
		{
			PropertyInfo prop = textObject.GetType().GetProperty("text", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
			prop?.SetValue(textObject, value);
		}
		catch (Exception)
		{
		}
	}

	/// <summary>GetComponentInChildren&lt;T&gt;(includeInactive: true) через reflection (GameObject или Component).</summary>
	private static object FindComponentInChildren(object target, string typeName)
	{
		try
		{
			Type componentType = FindGameType(typeName) ?? Type.GetType("UnityEngine.UI." + typeName + ", UnityEngine.UI");
			if (componentType == null)
			{
				return null;
			}
			Type t = target.GetType();
			MethodInfo gcc = t.GetMethod("GetComponentInChildren", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic, null, new Type[2] { typeof(Type), typeof(bool) }, null);
			if (gcc == null)
			{
				return null;
			}
			return gcc.Invoke(target, new object[2] { componentType, true });
		}
		catch (Exception)
		{
			return null;
		}
	}

	/// <summary>
	/// Нижний бар выбранного объекта («Король Анаэла», шаблон "titled_king" с $unit$):
	/// подменяем на титул с учётом пола и идеологии («Королева Анаэла», «Вождь …»).
	/// </summary>
	private static int PatchSelectedKingTitle()
	{
		try
		{
			Type type = FindGameType("SelectedMetaUnitElement");
			if (type == null)
			{
				LogService.LogError("[Real Politic] SelectedMetaUnitElement не найден — титул в нижнем баре не меняется");
				return 0;
			}
			MethodInfo method = type.GetMethod("show", BindingFlags.Instance | BindingFlags.Public, null, new Type[2] { typeof(Actor), typeof(string) }, null);
			if (method == null)
			{
				LogService.LogError("[Real Politic] SelectedMetaUnitElement.show(Actor,string) не найден — титул в нижнем баре не меняется");
				return 0;
			}
			_harmony.Patch(method, null, new HarmonyMethod(typeof(Patches), "SelectedKingTitlePostfix", null), null, null, null);
			LogService.LogInfo("[Real Politic] патч титула правителя в нижнем баре установлен");
			return 1;
		}
		catch (Exception ex)
		{
			LogService.LogError("[Real Politic] не удалось установить патч нижнего бара: " + ex.Message);
			return 0;
		}
	}

	private static void SelectedKingTitlePostfix(object __instance, Actor pActor, string pLocaleKey)
	{
		// Шаблон титула правителя в нижнем баре — ключ "titled_king" (таб королевства).
		if (pActor == null || pLocaleKey != "titled_king")
		{
			return;
		}
		try
		{
			if (!GameDeaths.IsRuler(pActor))
			{
				return;
			}
			Kingdom kingdom = ((BaseSimObject)pActor).kingdom;
			IdeologyType ideology = (kingdom != null && RealPoliticMod.World != null)
				? (RealPoliticMod.World.GetKingdom((int)((NanoObject)kingdom).id)?.Ideology ?? IdeologyType.Monarchy)
				: IdeologyType.Monarchy;
			bool female = GameText.ActorIsFemale(pActor);
			string title = RulerTitle(ideology, female);
			string name = GameText.ActorName(pActor);
			SetTextValue(GameRefs.ReadProperty(__instance, "_title"), title + " " + name);
			LogOnce("титул в нижнем баре: '" + title + " " + name + "' (идеология " + ideology.ToString() + ", female=" + female + ")");
		}
		catch (Exception ex)
		{
			LogService.LogError("[Real Politic] ошибка титула в нижнем баре: " + ex.Message);
		}
	}

	private static int PatchIdeologyDisplay()
	{
		int num = 0;
		num += PatchWindowPostfix("UnitWindow", "ShowMetaRowsPostfix");
		num += PatchWindowPostfix("KingdomWindow", "KingdomShowMetaRowsPostfix");
		return num;
	}

	private static int PatchWindowPostfix(string windowTypeName, string postfixName)
	{
		try
		{
			Type type = FindGameType(windowTypeName);
			if (type == null)
			{
				LogService.LogError("[Real Politic] " + windowTypeName + " не найден — идеология не показывается");
				return 0;
			}
			MethodInfo method = type.GetMethod("showMetaRows", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
			if (method == null)
			{
				LogService.LogError("[Real Politic] " + windowTypeName + ".showMetaRows не найден — идеология не показывается");
				return 0;
			}
			_harmony.Patch(method, null, new HarmonyMethod(typeof(Patches), postfixName, null), null, null, null);
			LogService.LogInfo("[Real Politic] патч показа идеологии в " + windowTypeName + " установлен");
			return 1;
		}
		catch (Exception ex)
		{
			LogService.LogError("[Real Politic] не удалось установить патч " + windowTypeName + ": " + ex.Message);
			return 0;
		}
	}

	private static object _lastUnitWindow;

	private static object _lastKingdomWindow;

	private static float _nextEnsure;

	private static readonly Dictionary<string, int> _unitDiagCounts = new Dictionary<string, int>();

	private static readonly Dictionary<string, int> _ensureFailures = new Dictionary<string, int>();

	private static readonly HashSet<string> _ensureGaveUp = new HashSet<string>();

	/// <summary>Диагностика окна юнита: до 5 записей на причину (по ней видно, где именно цепочка рвётся).</summary>
	private static void LogUnitDiag(string reason)
	{
		int count = _unitDiagCounts.TryGetValue(reason, out int c) ? c : 0;
		count++;
		_unitDiagCounts[reason] = count;
		if (count <= 5)
		{
			LogService.LogInfo("[Real Politic] окно юнита (вызов showMetaRows #" + count + "): " + reason);
		}
	}

	/// <summary>Id жителя в открытом сейчас окне юнита (0, если окна нет). Этап 25.</summary>
	public static long CurrentUnitWindowActorId()
	{
		try
		{
			if (_lastUnitWindow == null)
			{
				return 0;
			}
			Actor actor = GameRefs.ReadMember(_lastUnitWindow, "actor") as Actor;
			if (actor?.data == null)
			{
				return 0;
			}
			return ((BaseSystemData)actor.data).id;
		}
		catch (Exception)
		{
			return 0;
		}
	}

	/// <summary>Контейнер строк статистики окна — место среди строк профиля,
	/// где живут наши блоки (идеология, родословная, кнопка «Дерево»).</summary>
	public static object TryGetStatsContainer(object window)
	{
		try
		{
			return FindBestStatsContainer(window);
		}
		catch (Exception)
		{
			return null;
		}
	}

	private static void ShowMetaRowsPostfix(object __instance)
	{
		// Запоминаем окно: «гарантированный механизм» EnsureIdeologyRows() повторит
		// проверку строки идеологии, даже если прямой вызов по какой-то причине не сработал.
		_lastUnitWindow = __instance;
		try
		{
			Actor actor = GameRefs.ReadMember(__instance, "actor") as Actor;
			if (actor == null)
			{
				LogUnitDiag("actor == null — юнит не выбран");
				return;
			}
			Kingdom kingdom = ((BaseSimObject)actor).kingdom;
			if (kingdom == null)
			{
				LogUnitDiag("kingdom == null — юнит без государства");
					return;
				}
				int kingdomId = (int)((NanoObject)kingdom).id;
				if (kingdomId <= 0)
				{
					return; // дикие/служебные королевства игры не являются политическими державами
				}
				KingdomPolitics kingdomPolitics = RealPoliticMod.World?.GetKingdom(kingdomId);
			if (kingdomPolitics == null)
			{
				LogUnitDiag("идеология государства #" + kingdomId + " не найдена в политическом мире (королевство ещё не синхронизировано?)");
				return;
			}
			object container = FindBestStatsContainer(__instance);
			if (container == null)
			{
				LogUnitDiag("контейнер строк не найден (meta_rows_container == null и в иерархии окна нет StatsRowsContainer)");
				return;
			}
			// Личная идеология ЖИТЕЛЯ (черта конкретного юнита) — в том же блоке, что язык и культура.
			ActorIdeologies.Faith faith = ActorIdeologies.Get(actor);
			IdeologyType personal = (faith == null) ? kingdomPolitics.Ideology : faith.Ideology;
			LogUnitDiag("добавляю строку идеологии (житель: " + personal.ToString() + ", государство #" + kingdomId + ": " + kingdomPolitics.Ideology.ToString() + ", контейнер " + container.GetType().Name + ")");
			ShowIdeologyRow(container, personal, faith, kingdomPolitics.Ideology);
			// 0.23.2: родословная больше не засоряет окно жителя — она в отдельном окне
			// (кнопка-вкладка на боковой панели; если панели нет — кнопка среди строк).
			FamilyTreeWindow.AttachOpenButton(__instance);
			if (!FamilyTreeWindow.HasSideTab)
			{
				ShowFamilyBlock(container, ((BaseSystemData)actor.data).id);
			}
		}
		catch (Exception ex)
		{
			LogService.LogError("[Real Politic] ошибка показа идеологии юнита: " + ex);
		}
	}

	private static readonly System.Collections.Generic.HashSet<string> _loggedOnce = new System.Collections.Generic.HashSet<string>();

	public static void LogOncePublic(string message)
	{
		LogOnce(message);
	}

	private static void LogOnce(string message)
	{
		if (_loggedOnce.Add(message))
		{
			LogService.LogInfo("[Real Politic] " + message);
		}
	}

	/// <summary>
	/// Строки модовых механик в окне государства: ульта (готова / действует / перезарядка)
	/// и шкала уникальной угрозы в процентах. Пользователь видит и то, и другое сразу.
	/// </summary>
	private static void ShowMechanicsRows(object container, KingdomPolitics state)
	{
		try
		{
			if (state?.Mechanics == null)
			{
				return;
			}
			int year = RealPoliticMod.CurrentYear();
			IdeologyPowerDef power = IdeologyMechanics.Power(state.Ideology);
			IdeologyThreatDef threat = IdeologyMechanics.Threat(state.Ideology);

			// Плюс и минус строя — двумя короткими строками, чтобы не налезать на заголовок.
			string plus = GameText.Localized("RP_PLUS_" + Key(state.Ideology), string.Empty);
			string minus = GameText.Localized("RP_MINUS_" + Key(state.Ideology), string.Empty);
			if (!string.IsNullOrEmpty(plus))
			{
				ShowSimpleRow(container, "RP_PLUS_TITLE", plus, EnsureUiRowIcon(ModIcons.AdmitKey), localizeTitle: true);
			}
			if (!string.IsNullOrEmpty(minus))
			{
				ShowSimpleRow(container, "RP_MINUS_TITLE", minus, EnsureUiRowIcon(ModIcons.ExpelKey), localizeTitle: true);
			}

			// Ульта: слева её название, справа — короткое состояние.
			string powerName = GameText.Localized(power.NameKey, power.NameKey);
			string powerState;
			if (state.Mechanics.IsActive(year))
			{
				int left = state.Mechanics.ActiveUntilYear - year;
				powerState = GameText.Localized("RP_POWER_ACTIVE", "действует") + " " + left + GameText.Localized("RP_YEARS_SHORT", "л.");
			}
			else
			{
				int cd = state.Mechanics.CooldownLeft(power, year);
				powerState = (cd <= 0)
					? GameText.Localized("RP_POWER_READY", "готово")
					: cd + GameText.Localized("RP_YEARS_SHORT", "л.");
			}
			ShowSimpleRow(container, powerName, powerState, EnsureIcon(state.Ideology), localizeTitle: false);

			// Угроза: слева её название, справа — шкала из 5 делений и проценты.
				int percent = (int)Math.Round(state.Mechanics.ThreatLevel);
				string threatName = GameText.Localized(threat.NameKey, threat.NameKey);
				ShowSimpleRow(container, threatName, ThreatBar(percent) + " " + percent + "%", EnsureIcon(IdeologyType.Militarism), localizeTitle: false);

				// Преемственность: закон смены власти и текущий статус (регентство / срок выборов).
				string successionLawName = GameText.Localized(SuccessionLaws.NameKey(state.SuccessionLaw), state.SuccessionLaw.ToString());
				string successionStatus = successionLawName;
				if (state.Regency != null && state.Regency.IsActive)
				{
					int yearsLeft = Math.Max(1, 18 - (state.Ruler?.Age ?? 0));
					string regencyStr = GameText.Localized("RP_REGENCY_STATUS", "Регентство") + " (" + yearsLeft + GameText.Localized("RP_YEARS_SHORT", "л.") + ")";
					successionStatus = successionLawName + " • " + regencyStr;
				}
				else if (SuccessionLaws.HasPeriodicCycle(state.SuccessionLaw) || SuccessionLaws.HasPeriodicCycle(state.Ideology))
				{
					int terms = state.Election?.TermsServed ?? 1;
					int nextIn = Math.Max(0, (state.Election?.NextElectionYear ?? (year + 15)) - year);
					string termStr = GameText.Localized("RP_TERM_STATUS", "Срок") + " " + terms + "/3 (" + nextIn + GameText.Localized("RP_YEARS_SHORT", "л.") + ")";
					successionStatus = successionLawName + " • " + termStr;
				}
				else if (state.Ideology == IdeologyType.FreeTerritory)
				{
					int terms = state.Election?.TermsServed ?? 1;
					string termStr = GameText.Localized("RP_ATAMAN_TERM_STATUS", "Срок") + " " + terms + " (бессрочно)";
					successionStatus = successionLawName + " • " + termStr;
				}
				else if ((state.Ideology == IdeologyType.Monarchy || state.Ideology == IdeologyType.Empire || state.Ideology == IdeologyType.Khaganate)
					&& !string.IsNullOrEmpty(state.RoyalClanName))
				{
					successionStatus = successionLawName + " • «" + state.RoyalClanName + "»";
				}
				string successionIcon = (SuccessionLaws.HasPeriodicCycle(state.SuccessionLaw) || SuccessionLaws.HasPeriodicCycle(state.Ideology))
					? EnsureUiRowIcon(ModIcons.VotesKey)
					: EnsureUiRowIcon(ModIcons.FamilyKey);
				ShowSimpleRow(container, "RP_SUCCESSION_TITLE", CompactUiText(successionStatus, 26), successionIcon, localizeTitle: true);
				// Этап 26: писаные законы державы — форма правления, власть войны, границы.
				if (state.Constitution != null && RealPoliticMod.World?.Params.EnableConstitution == true)
				{
					// 0.23.2: в строке — только форма правления одним словом; полный текст в окне «Конституция».
					ShowSimpleRow(container, "RP_CONST_TITLE", CompactUiText(ConstitutionL10n.Summary(state.Constitution, state.Ideology), 22), EnsureUiRowIcon(ModIcons.ConstitutionKey), localizeTitle: true);
				}
				// Этап 19: личность правителя — характер, амбиции и династический брак.
				if (RealPoliticMod.ShowDynastyUI && RealPoliticMod.Feeder != null && RealPoliticMod.Feeder.TryGetPersonalitySummary(state.KingdomId, out string personality))
				{
					ShowSimpleRow(container, "RP_PERSONALITY_TITLE", CompactUiText(personality, 26), EnsureUiRowIcon(ModIcons.FamilyKey), localizeTitle: true);
				}

				if (state.Parties.Count > 0)
				{
					PoliticalParty rulingParty = state.Parties[0];
					// 0.48.3: показываем собственное имя партии — «Народная партия «Ракрог»»,
					// а у старых сейвов без имени — прежнюю общую строку.
					string partyLabel = string.IsNullOrEmpty(rulingParty.Name)
						? GameText.Localized(rulingParty.NameKey, CivicText.Fallback(rulingParty.NameKey))
						: rulingParty.Name;
					string partyTitle = partyLabel + " (" + rulingParty.SeatsPercent + "%)";
					ShowSimpleRow(container, "RP28_PARTY_TITLE", CompactUiText(partyTitle, 26), EnsureUiRowIcon(ModIcons.VotesKey), localizeTitle: true);
				}
				if (state.ActiveMovement != null)
				{
					string demandKey = CivicSocietyEngine.DemandLocKey(state.ActiveMovement.Demand);
					string demandTitle = GameText.Localized(demandKey, CivicText.Fallback(demandKey));
					string movText = demandTitle + " (" + (int)Math.Round(state.ActiveMovement.SupportShare * 100f) + "%)";
					ShowSimpleRow(container, "RP28_MOVEMENT_TITLE", CompactUiText(movText, 26), EnsureIcon(IdeologyType.Egalitarianism), localizeTitle: true);
				}
				if (RealPoliticMod.World?.CityHomelands != null)
				{
					int foreignCities = 0;
					string foreignHomeland = string.Empty;
					foreach (CityPolitics c in RealPoliticMod.World.Cities)
					{
						if (c.KingdomId == state.KingdomId)
						{
							CityHomelandRecord? rec = RealPoliticMod.World.CityHomelands.Get(c.CityId);
							if (rec != null && rec.OriginalKingdomId != state.KingdomId)
							{
								foreignCities++;
								if (string.IsNullOrEmpty(foreignHomeland) && !string.IsNullOrEmpty(rec.OriginalKingdomName))
								{
									foreignHomeland = rec.OriginalKingdomName;
								}
							}
						}
					}
					if (foreignCities > 0)
					{
						string hlText = (string.IsNullOrEmpty(foreignHomeland) ? "?" : foreignHomeland) + " (" + foreignCities + ")";
						ShowSimpleRow(container, "RP29_HOMELAND_TITLE", CompactUiText(hlText, 26), EnsureIcon(IdeologyType.Tribalism), localizeTitle: true);
					}
				}
				// Объяснимое решение стратегического ИИ — последнее принятое/запланированное действие.
				if (RealPoliticMod.ShowStrategyUI && state.ActiveCampaign != null)
				{
					string goalTitle = GameText.Localized(GrandAi.GoalLocKey(state.ActiveCampaign.Goal), state.ActiveCampaign.Goal.ToString());
					string campaignText = goalTitle + " (" + state.ActiveCampaign.CompletedSteps + "/" + state.ActiveCampaign.Steps.Count + ")";
					ShowSimpleRow(container, "RP27_CAMPAIGN_TITLE", CompactUiText(campaignText, 26), EnsureUiRowIcon(ModIcons.IdeologyLayerKey), localizeTitle: true);
				}
				if (RealPoliticMod.ShowStrategyUI && RealPoliticMod.Feeder != null && RealPoliticMod.Feeder.TryGetStrategicPlan(state.KingdomId, out KingdomActionPlan plan))
				{
					string action = GameText.Localized("RP_STRATEGY_" + plan.Action.ToString().ToUpperInvariant(), plan.Action.ToString());
					string planText = action + " (" + (int)plan.Score + ")";
					ShowSimpleRow(container, "RP_STRATEGY_TITLE", CompactUiText(planText, 24), EnsureUiRowIcon(ModIcons.IdeologyLayerKey), localizeTitle: true);
					if (!plan.IsFallback && RealPoliticMod.Feeder.TryGetStrategicResult(state.KingdomId, out string result)
						&& !string.IsNullOrWhiteSpace(result) && !string.Equals(result, plan.Reason, StringComparison.Ordinal))
					{
						ShowSimpleRow(container, "RP_STRATEGY_RESULT", CompactUiText(result, 24), EnsureUiRowIcon(ModIcons.IdeologyLayerKey), localizeTitle: true);
					}
				}
				// Экономика: благосостояние, saldo казны, число торговых путей и активный кризис.
				if (RealPoliticMod.ShowEconomyUI && RealPoliticMod.Feeder != null && RealPoliticMod.Feeder.TryGetEconomySummary(state.KingdomId, out string economy))
				{
					ShowSimpleRow(container, "RP_ECONOMY_TITLE", CompactUiText(economy, 26), EnsureIcon(IdeologyType.MerchantRepublic), localizeTitle: true);
				}
				// Гражданская война: причина, перевес повстанцев, длительность и число мятежных городов.
				if (RealPoliticMod.ShowCivilWarUI && RealPoliticMod.Feeder != null && RealPoliticMod.Feeder.TryGetCivilWarSummary(state.KingdomId, out string civilWar))
				{
					ShowSimpleRow(container, "RP_CIVILWAR_TITLE", CompactUiText(civilWar, 26), EnsureIcon(IdeologyType.Militarism), localizeTitle: true);
				}
				if (RealPoliticMod.ShowDiplomacyUI && RealPoliticMod.Feeder != null && RealPoliticMod.Feeder.TryGetDiplomacySummary(state.KingdomId, out string diplomacy))
				{
					ShowSimpleRow(container, "RP_DIPLOMACY_TITLE", CompactUiText(diplomacy, 26), EnsureIcon(IdeologyType.Aristocracy), localizeTitle: true);
				}
				if (RealPoliticMod.ShowUnionUI && RealPoliticMod.Feeder != null && RealPoliticMod.Feeder.TryGetUnionSummary(state.KingdomId, out string unionInfo))
				{
					ShowSimpleRow(container, "RP_UNION_INFO_TITLE", CompactUiText(unionInfo, 26), EnsureUiRowIcon(ModIcons.AdmitKey), localizeTitle: true);
				}
				if (RealPoliticMod.ShowKingdomChronicleUI && RealPoliticMod.Feeder != null && RealPoliticMod.Feeder.TryGetLatestKingdomEvent(state.KingdomId, out string latestEvent))
				{
					ShowSimpleRow(container, "RP_CHRONICLE_LATEST", CompactUiText(latestEvent, 26), EnsureUiRowIcon(ModIcons.ConstitutionKey), localizeTitle: true);
				}
			}
		catch (Exception ex)
		{
			LogOnce("не удалось показать строки механик: " + ex.Message);
		}
	}

	/// <summary>Текстовая шкала угрозы из 5 делений — компактна и видна прямо в строке окна.</summary>
	private static string ThreatBar(int percent)
	{
		int filled = Math.Max(0, Math.Min(5, (percent + 10) / 20));
		return new string('\u25ae', filled) + new string('\u25af', 5 - filled);
	}

	/// <summary>Ограничивает динамический текст, чтобы строки не переполняли prefab окна.</summary>
	private static string CompactUiText(string value, int maxLength)
	{
		if (string.IsNullOrWhiteSpace(value) || value.Length <= maxLength)
		{
			return value ?? string.Empty;
		}
		return value.Substring(0, Math.Max(1, maxLength - 1)).TrimEnd() + "…";
	}

	/// <summary>Суффикс ключей локализации по идеологии (TRIBALISM, MONARCHY, …).</summary>
	private static string Key(IdeologyType type)
	{
		return type.ToString().ToUpperInvariant();
	}

	/// <summary>Короткий ключ кнопочной/вкладочной иконки для строки статистики (без ui/Icons/).</summary>
	private static string EnsureUiRowIcon(string key)
	{
		IdeologySprites.EnsureRegistered(key, ModIcons.PathFor(key));
		return ModIcons.WindowIconFor(key);
	}

	/// <summary>Простая строка «заголовок: значение» в контейнере статистики.</summary>
	private static void ShowSimpleRow(object container, string titleKey, string value, string iconKey, bool localizeTitle = true)
	{
		MethodInfo showStatRow = FindShowStatRow(container.GetType());
		if (showStatRow == null)
		{
			return;
		}
		Type metaType = FindGameType("MetaType");
		object metaNone = (metaType == null) ? 0 : Enum.ToObject(metaType, 0);
		// У мета-строки видимая часть — иконка. Пустой ключ оставлял бы плитку без картинки,
		// поэтому даже для будущей строки без собственной темы есть штатная иконка королевства.
		string icon = string.IsNullOrEmpty(iconKey) ? "iconKingdom" : iconKey;
		try
		{
			object row = showStatRow.Invoke(container, new object[10]
			{
				titleKey, value, string.Empty, metaNone, -1L, false, icon, null, null, localizeTitle
			});
			if (row != null)
			{
				ActivateRow(row);
			}
		}
		catch (Exception)
		{
		}
	}

	private static void KingdomShowMetaRowsPostfix(object __instance)
	{
		_lastKingdomWindow = __instance;
		try
		{
			Kingdom kingdom = GameRefs.ReadProperty(__instance, "meta_object") as Kingdom;
			if (kingdom == null)
			{
				return;
			}
			int kingdomId = (int)((NanoObject)kingdom).id;
			KingdomPolitics kingdomPolitics = RealPoliticMod.World?.GetKingdom(kingdomId);
			if (kingdomPolitics == null)
			{
				return;
			}
			object container = FindBestStatsContainer(__instance);
			if (container == null)
			{
				return;
			}
			ShowIdeologyRow(container, kingdomPolitics.Ideology, null, kingdomPolitics.Ideology);
			ShowMechanicsRows(container, kingdomPolitics);
			// 0.23.6: заголовок «Королевство» над девизом заменяем на форму государства
			// («Племя», «Теократия», …) — иначе над «Вождём» висело слово «Королевство».
			ApplyStateFormHeader(__instance, kingdomPolitics.Ideology);
			// 0.45: вместо трёх отдельных вкладок (конституция, КМУД, голосования) —
			// одна вкладка «Real Politic» с оглавлением. Три вкладки не помещались
			// в боковую панель, и нижние кнопки уезжали за край экрана
			// (жалоба владельца 29.10.2026: «слева кнопки уже исчезают»).
			ModHubWindow.AttachOpenButton(__instance, kingdomId, ((NanoObject)kingdom).name);
			ModHubWindow.HideLegacyTabs(__instance);
			// 0.45: пока мод сам ведёт союзы, кнопка-«щит» альянса не мозолит глаза
			// и ничего не создаёт мимо политики мода.
			NativeAllianceButtonHider.HideInKingdomWindow(__instance);
		}
		catch (Exception ex)
		{
			LogService.LogError("[Real Politic] ошибка показа идеологии королевства: " + ex);
		}
	}

	/// <summary>Ключи LocalizedText, под которыми в окне державы лежит заголовок «Королевство».</summary>
	private static readonly HashSet<string> _kingdomHeaderKeys = new HashSet<string> { "kingdom", "kingdom_name", "kingdom_title", "kingdoms", "titled_kingdom" };

	/// <summary>
	/// Заголовок блока имени/девиза в окне державы: в игре это всегда «Королевство»,
	/// у нас же держава может быть племенем, республикой, теократией. Подставляем
	/// название формы государства из локали мода (RP_STATE_*, с заглавной буквы).
	/// Ищем LocalizedText по известным ключам; список всех найденных ключей один раз
	/// пишем в лог — если в новой версии игры ключ переименуют, будет видно, на что менять.
	/// </summary>
	private static void ApplyStateFormHeader(object window, IdeologyType ideology)
	{
		try
		{
			object windowGo = GameRefs.ReadMember(window, "gameObject");
			Type ltType = FindGameType("LocalizedText");
			if (windowGo == null || ltType == null)
			{
				return;
			}
			MethodInfo getAll = windowGo.GetType().GetMethod("GetComponentsInChildren", BindingFlags.Instance | BindingFlags.Public, null, new Type[2] { typeof(Type), typeof(bool) }, null);
			if (!(getAll?.Invoke(windowGo, new object[2] { ltType, true }) is Array all))
			{
				return;
			}
			string title = GameText.Capitalize(GameText.Localized(Ideologies.StateNameKey(ideology), GameText.Localized(Ideologies.NameKey(ideology), ideology.ToString())));
			List<string> keys = new List<string>();
			int changed = 0;
			for (int i = 0; i < all.Length; i++)
			{
				object lt = all.GetValue(i);
				string key = GameRefs.ReadMember(lt, "key")?.ToString() ?? string.Empty;
				keys.Add(key);
				if (!_kingdomHeaderKeys.Contains(key))
				{
					continue;
				}
				// Только заголовки-«плашки», не строки статистики (у тех имя объекта начинается с "[KV]").
				object go = GameRefs.ReadMember(lt, "gameObject");
				string goName = GameRefs.ReadMember(go, "name")?.ToString() ?? string.Empty;
				object parent = GameRefs.ReadMember(GameRefs.ReadMember(go, "transform"), "parent");
				string parentName = GameRefs.ReadMember(GameRefs.ReadMember(parent, "gameObject"), "name")?.ToString() ?? string.Empty;
				if (goName.StartsWith("[KV]") || parentName.StartsWith("[KV]"))
				{
					continue;
				}
				SetTextValue(GameRefs.ReadMember(lt, "text"), title);
				changed++;
			}
			LogOnce("заголовок формы государства в окне державы: '" + title + "' (заменено " + changed + "; ключи LocalizedText окна: " + string.Join(", ", keys) + ")");
		}
		catch (Exception ex)
		{
			LogOnce("не удалось подставить форму государства в заголовок окна державы: " + ex.Message);
		}
	}

	/// <summary>
	/// Гарантированный механизм показа строки идеологии (вызывается из Update раз в 0.5 с).
	/// Для каждого открытого окна (юнита/государства): строка «[KV] RP_IDEOLOGY_TITLE»
	/// должна существовать в контейнере и быть активной; если её нет — добавляем,
	/// если идеология сменилась, пока окно открыто — обновляем текст.
	/// Работает независимо от того, когда и сколько раз игра вызывала showMetaRows().
	/// </summary>
	public static void EnsureIdeologyRows()
	{
		float unscaledTime = Time.unscaledTime;
		if (unscaledTime < _nextEnsure)
		{
			return;
		}
		_nextEnsure = unscaledTime + 0.5f;
		try
		{
			EnsureWindowRow(_lastUnitWindow, UnitWindowRow, "unit");
			EnsureWindowRow(_lastKingdomWindow, KingdomWindowRow, "kingdom");
		}
		catch (Exception ex)
		{
			LogService.LogError("[Real Politic] ошибка гарантированного механизма строки идеологии: " + ex.Message);
		}
	}

	/// <summary>Что показывать в строке идеологии: сама идеология, вера жителя (если это юнит) и идеология государства.</summary>
	private struct RowInfo
	{
		public IdeologyType Ideology;

		public ActorIdeologies.Faith Faith;

		public IdeologyType State;
	}

	private static RowInfo? UnitWindowRow(object window)
	{
		try
		{
			Actor actor = GameRefs.ReadMember(window, "actor") as Actor;
			if (actor == null)
			{
				return null;
			}
			Kingdom kingdom = ((BaseSimObject)actor).kingdom;
			if (kingdom == null)
			{
				return null;
			}
			KingdomPolitics state = RealPoliticMod.World?.GetKingdom((int)((NanoObject)kingdom).id);
			if (state == null)
			{
				return null;
			}
			ActorIdeologies.Faith faith = ActorIdeologies.Get(actor);
			if (faith == null)
			{
				return null;
			}
			return new RowInfo { Ideology = faith.Ideology, Faith = faith, State = state.Ideology };
		}
		catch (Exception)
		{
			return null;
		}
	}

	private static RowInfo? KingdomWindowRow(object window)
	{
		try
		{
			Kingdom kingdom = GameRefs.ReadMember(window, "meta_object") as Kingdom;
			if (kingdom == null)
			{
				return null;
			}
			int kingdomId = (int)((NanoObject)kingdom).id;
			if (kingdomId <= 0)
			{
				return null;
			}
			KingdomPolitics state = RealPoliticMod.World?.GetKingdom(kingdomId);
			if (state == null)
			{
				return null;
			}
			return new RowInfo { Ideology = state.Ideology, Faith = null, State = state.Ideology };
		}
		catch (Exception)
		{
			return null;
		}
	}

	private static void EnsureWindowRow(object window, Func<object, RowInfo?> rowGetter, string tag)
	{
		if (window == null)
		{
			return;
		}
		object gameObject = GameRefs.ReadMember(window, "gameObject");
		if (gameObject == null || !(GameRefs.ReadMember(gameObject, "activeInHierarchy") is true))
		{
			return;
		}
		if (_ensureGaveUp.Contains(tag))
		{
			return;
		}
		RowInfo? rowValue = rowGetter(window);
		if (rowValue == null)
		{
			return;
		}
		RowInfo info = rowValue.Value;
		IdeologyType ideology = info.Ideology;
		object container = FindBestStatsContainer(window);
		if (container == null)
		{
			return;
		}
		string expected = RowValueText(ideology, info.Faith, info.State);
		object rows = GameRefs.ReadMember(container, "stats_rows");
		if (rows is IList list)
		{
			for (int i = 0; i < list.Count; i++)
			{
				object row = list[i];
				object rowGo = GameRefs.ReadMember(row, "gameObject");
				if (rowGo == null || GameRefs.ReadMember(rowGo, "name")?.ToString() != "[KV] RP_IDEOLOGY_TITLE")
				{
					continue;
				}
				// Строка есть: гарантируем, что она активна и текст актуален.
				if (!(GameRefs.ReadMember(rowGo, "activeSelf") is true))
				{
					GameRefs.Invoke(rowGo, "SetActive", true);
				}
				SetTextValue(GameRefs.ReadMember(row, "value"), expected);
				return;
			}
		}
		// Строки нет — добавляем. Если после нескольких попыток она так и не появится,
		// перестаём пытаться (иначе бесконечно заспамим контейнер невидимыми строками).
		int failures = _ensureFailures.TryGetValue(tag, out int f) ? f : 0;
		failures++;
		_ensureFailures[tag] = failures;
		LogOnce("гарантированный механизм: добавляю строку идеологии (" + tag + ", попытка " + failures + ", контейнер " + container.GetType().Name + ")");
		ShowIdeologyRow(container, ideology, info.Faith, info.State);
		if (failures >= 4)
		{
			_ensureGaveUp.Add(tag);
			LogService.LogError("[Real Politic] гарантированный механизм не смог показать строку идеологии (" + tag + ") за 4 попытки — см. записи «окно юнита» в логе выше");
		}
	}

	/// <summary>
	/// Контейнер строк, куда добавлять строку идеологии. В приоритете поле окна
	/// meta_rows_container (туда игра пишет строки мета-раздела); если в нём нет строк,
	/// ищем по всей иерархии окна любой StatsRowsContainer с содержимым
	/// (кроме общего статистического «content_stats»).
	/// </summary>
	private static object FindBestStatsContainer(object window)
	{
		// Требование пользователя: строка идеологии должна быть в РЯДУ МЕТА-ИКОНОК
		// (субспециес/культура/язык/религия/герб), а не в общем списке статистики.
		// Этот ряд — компонент StatsMetaRowsContainer. Поле meta_rows_container у окна
		// иногда не читается через иерархию типов, поэтому дополнительно ищем компонент
		// нужного типа прямо в иерархии объектов окна.
		object metaRow = FindMetaRowsContainer(window);
		if (metaRow != null)
		{
			return metaRow;
		}
		object preferred = GetFieldInHierarchy(window, "meta_rows_container");
		object best = preferred;
		int bestScore = ScoreContainer(preferred) + 1000;
		object[] all = GetAllStatsContainers(window);
		for (int i = 0; i < all.Length; i++)
		{
			object c = all[i];
			if (ReferenceEquals(c, preferred) || c == null || IsContentStats(c))
			{
				continue;
			}
			int score = ScoreContainer(c);
			if (score > bestScore)
			{
				bestScore = score;
				best = c;
			}
		}
		return best;
	}

	/// <summary>
	/// Ряд мета-иконок окна (StatsMetaRowsContainer) — именно туда игра кладёт
	/// культуру/язык/религию/субспециес, и именно туда должна попадать идеология.
	/// </summary>
	private static object FindMetaRowsContainer(object window)
	{
		try
		{
			object direct = GetFieldInHierarchy(window, "meta_rows_container");
			if (direct != null && IsMetaRowsContainer(direct))
			{
				return direct;
			}
			object[] all = GetAllStatsContainers(window);
			for (int i = 0; i < all.Length; i++)
			{
				if (all[i] != null && IsMetaRowsContainer(all[i]))
				{
					return all[i];
				}
			}
		}
		catch (Exception)
		{
		}
		return null;
	}

	private static bool IsMetaRowsContainer(object container)
	{
		for (Type t = container.GetType(); t != null; t = t.BaseType)
		{
			if (t.Name == "StatsMetaRowsContainer")
			{
				return true;
			}
		}
		return false;
	}

	private static int ScoreContainer(object container)
	{
		if (container == null)
		{
			return 0;
		}
		int score = 0;
		object go = GameRefs.ReadMember(container, "gameObject");
		if (go != null && GameRefs.ReadMember(go, "activeInHierarchy") is true)
		{
			score += 100;
		}
		if (GameRefs.ReadMember(container, "stats_rows") is IList list)
		{
			score += Math.Min(50, list.Count * 5);
		}
		return score;
	}

	private static bool IsContentStats(object container)
	{
		try
		{
			object transform = GameRefs.ReadMember(container, "transform");
			object name = GameRefs.ReadMember(transform, "name");
			return name != null && name.ToString() == "content_stats";
		}
		catch (Exception)
		{
			return false;
		}
	}

	private static object[] GetAllStatsContainers(object window)
	{
		try
		{
			object go = GameRefs.ReadMember(window, "gameObject");
			Type type = FindGameType("StatsRowsContainer");
			if (go == null || type == null)
			{
				return Array.Empty<object>();
			}
			MethodInfo getComponents = go.GetType().GetMethod("GetComponentsInChildren", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic, null, new Type[2] { typeof(Type), typeof(bool) }, null);
			if (getComponents == null)
			{
				return Array.Empty<object>();
			}
			object result = getComponents.Invoke(go, new object[2] { type, true });
			return result as object[] ?? Array.Empty<object>();
		}
		catch (Exception)
		{
			return Array.Empty<object>();
		}
	}

	/// <summary>
	/// Поиск метода showStatRow с обходом ВСЕЙ иерархии типов.
	/// GetMethod(name, Instance|Public) на StatsMetaRowsContainer в игре возвращал null
	/// (метод объявлен в базовом StatsRowsContainer, плюс возможна неоднозначность с
	/// перегрузками showStatRowMeta), поэтому идём по BaseType вручную и берём
	/// перегрузку с наибольшим числом параметров.
	/// </summary>
	private static MethodInfo FindShowStatRow(Type type)
	{
		// Ищем в порядке предпочтения: showStatRow (базовый StatsRowsContainer),
		// затем showStatRowMeta (есть только у StatsMetaRowsContainer — ряда мета-иконок).
		// В игре встречался случай, когда DeclaredOnly-обход не находил метод,
		// поэтому сначала пробуем обычный поиск по всей иерархии сразу.
		string[] names = new string[2] { "showStatRow", "showStatRowMeta" };
		for (int n = 0; n < names.Length; n++)
		{
			MethodInfo direct = PickOverload(type.GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.FlattenHierarchy), names[n]);
			if (direct != null)
			{
				return direct;
			}
			for (Type t = type; t != null; t = t.BaseType)
			{
				MethodInfo[] methods;
				try
				{
					methods = t.GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly);
				}
				catch (Exception)
				{
					continue;
				}
				MethodInfo found = PickOverload(methods, names[n]);
				if (found != null)
				{
					return found;
				}
			}
		}
		return null;
	}

	/// <summary>Перегрузка с наибольшим числом параметров (нам нужна та, что с иконкой и тултипом).</summary>
	private static MethodInfo PickOverload(MethodInfo[] methods, string name)
	{
		MethodInfo best = null;
		for (int i = 0; i < methods.Length; i++)
		{
			if (methods[i].Name != name || methods[i].IsGenericMethod)
			{
				continue;
			}
			int count = methods[i].GetParameters().Length;
			if (count < 6)
			{
				continue;
			}
			if (best == null || count > best.GetParameters().Length)
			{
				best = methods[i];
			}
		}
		return best;
	}

	/// <summary>Список методов типа — для диагностики, если showStatRow так и не нашёлся.</summary>
	private static string DumpMethods(Type type)
	{
		try
		{
			List<string> names = new List<string>();
			for (Type t = type; t != null && names.Count < 30; t = t.BaseType)
			{
				MethodInfo[] methods = t.GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly);
				for (int i = 0; i < methods.Length && names.Count < 30; i++)
				{
					if (methods[i].Name.StartsWith("show"))
					{
						names.Add(t.Name + "." + methods[i].Name + "/" + methods[i].GetParameters().Length);
					}
				}
			}
			return string.Join(", ", names.ToArray());
		}
		catch (Exception)
		{
			return "н/д";
		}
	}

	/// <summary>
	/// Строка «Идеология: …» в окне юнита/государства. Ключевое для видимости:
	/// у строки нет заметного фона — если спрайт иконки null, строка невидима.
	/// Поэтому перед показом гарантируем, что иконка реально в спрайт-кэше игры
	/// (по тому же пути, что ищет showStatRow), а при неудаче — используем иконку игры.
	/// </summary>
	/// <summary>Этап 25: родословная жителя прямо в его окне.
	/// Игра помнит только живых — наш реестр помнит всех, и покойных тоже («†»).</summary>
	private const int MaxFamilyRows = 15;

	private static void ShowFamilyBlock(object container, long actorId)
	{
		try
		{
			if (RealPoliticMod.World == null)
			{
				return;
			}
			System.Collections.Generic.List<string> lines = GenealogyTree.Build(
				RealPoliticMod.World.Family, actorId, MaxFamilyRows);
			if (lines == null || lines.Count == 0)
			{
				return;
			}
			ShowStatLine(container, "RP_FAMILY_TITLE", string.Empty, EnsureUiRowIcon(ModIcons.FamilyKey));
			for (int i = 0; i < lines.Count; i++)
			{
				ShowStatLine(container, "RP_FAMILY_" + i.ToString(), lines[i], EnsureUiRowIcon(ModIcons.FamilyKey));
			}
		}
		catch (Exception ex)
		{
			LogOnce("ошибка блока родословной: " + ex.Message);
		}
	}

	/// <summary>Строка статистики с обязательной иконкой (родословная и прочие текстовые блоки).</summary>
	private static void ShowStatLine(object container, string key, string value, string icon)
	{
		if (string.IsNullOrEmpty(icon))
		{
			icon = "iconKingdom";
		}
		MethodInfo showStatRow = FindShowStatRow(container.GetType());
		if (showStatRow == null)
		{
			LogOnce("showStatRow не найден для строки «" + key + "»");
			return;
		}
		Type metaType = FindGameType("MetaType");
		object metaNone = (metaType == null) ? (object)0 : Enum.ToObject(metaType, 0);
		object result;
		try
		{
			result = showStatRow.Invoke(container, new object[10]
			{
				key, value, string.Empty, metaNone, -1L, false, icon, null, null, true
			});
		}
		catch (Exception ex)
		{
			LogOnce("showStatRow бросил исключение для «" + key + "»: " + ex.Message);
			return;
		}
		if (result != null)
		{
			ActivateRow(result);
		}
	}

	private static void ShowIdeologyRow(object container, IdeologyType ideology, ActorIdeologies.Faith faith, IdeologyType stateIdeology)
	{
		MethodInfo showStatRow = FindShowStatRow(container.GetType());
		if (showStatRow == null)
		{
			LogOnce("showStatRow не найден на " + container.GetType().Name + " (" + DumpMethods(container.GetType()) + ")");
			return;
		}
		Type metaType = FindGameType("MetaType");
		object metaNone = (metaType == null) ? 0 : Enum.ToObject(metaType, 0);
		string name = RowValueText(ideology, faith, stateIdeology);
		string icon = EnsureIcon(ideology);
		object result;
		try
		{
			result = showStatRow.Invoke(container, new object[10]
			{
				"RP_IDEOLOGY_TITLE", name, string.Empty, metaNone, -1L, false, icon, null, null, true
			});
		}
		catch (Exception ex)
		{
			LogOnce("showStatRow бросил исключение: " + ex.Message);
			return;
		}
		if (result == null)
		{
			LogOnce("showStatRow вернул null для идеологии " + ideology.ToString());
			return;
		}
		// getStatRow оставляет строку неактивной, а корутина showRows() активирует строки
		// покадрово и может не успеть/не дойти до нашей строки. Активируем явно.
		ActivateRow(result);
		LogRowDiagnostics(result, name, icon);
	}

	/// <summary>
	/// Текст значения строки идеологии. Для жителя-инакомыслящего добавляется послушание:
	/// «Технократия (несогласный, 34%)» — сразу видно, что идеология реально влияет.
	/// </summary>
	private static string RowValueText(IdeologyType ideology, ActorIdeologies.Faith faith, IdeologyType stateIdeology)
	{
		string name = GameText.Localized(Ideologies.NameKey(ideology), ideology.ToString());
		if (faith == null)
		{
			return name;
		}
		int obedience = (int)(ActorIdeologyRoll.Obedience(faith.Ideology, stateIdeology, faith.Conviction, faith.Dissent) * 100f);
		if (ideology != stateIdeology)
		{
			return name + " (" + GameText.Localized("RP_IDEOLOGY_DISSENTER", "несогласный") + ", " + obedience + "%)";
		}
		return name + " (" + GameText.Localized("RP_IDEOLOGY_LOYAL", "лоялен") + ", " + obedience + "%)";
	}

	/// <summary>
	/// Гарантирует иконку идеологии в кэше спрайтов игры и возвращает её ключ
	/// (без префикса "ui/Icons/"). Если не получилось — ключ готовой иконки игры,
	/// чтобы строка была видна хотя бы с универсальной иконкой.
	/// </summary>
	private static string EnsureIcon(IdeologyType ideology)
	{
		string key = Ideologies.IconKey(ideology);
		if (SpriteInGameCache("ui/Icons/" + key))
		{
			return key;
		}
		// По требованию: зарегистрировать из встроенного ресурса / папки мода.
		IdeologySprites.EnsureRegistered(ideology);
		if (SpriteInGameCache("ui/Icons/" + key))
		{
			LogOnce("иконка " + key + " зарегистрирована по требованию");
			return key;
		}
		// Последний рубеж: иконка ДОЛЖНА быть непустой, иначе у KeyValueField нет фона
		// и строка визуально исчезает. Перебираем заведомо существующие иконки игры.
		string[] fallbacks = new string[4] { "iconKingdom", "iconCulture", "iconReligion", "iconClan" };
		for (int i = 0; i < fallbacks.Length; i++)
		{
			if (SpriteInGameCache("ui/Icons/" + fallbacks[i]))
			{
				LogOnce("иконка " + key + " недоступна — использую иконку игры " + fallbacks[i] + " (строка останется видимой)");
				return fallbacks[i];
			}
		}
		LogOnce("иконка " + key + " недоступна и ни одна запасная иконка игры не найдена");
		return "iconKingdom";
	}

	private static bool SpriteInGameCache(string pathId)
	{
		try
		{
			Type spriteLoader = FindGameType("SpriteTextureLoader");
			if (spriteLoader == null)
			{
				return false;
			}
			MethodInfo getSprite = spriteLoader.GetMethod("getSprite", BindingFlags.Static | BindingFlags.Public);
			if (getSprite == null)
			{
				return false;
			}
			return getSprite.Invoke(null, new object[1] { pathId }) != null;
		}
		catch (Exception)
		{
			return false;
		}
	}

	private static void ActivateRow(object row)
	{
		try
		{
			// gameObject объявлен в Component — нужен обход иерархии (ReadProperty ищет только точный тип).
			object gameObject = GameRefs.ReadMember(row, "gameObject");
			GameRefs.Invoke(gameObject, "SetActive", true);
		}
		catch (Exception)
		{
		}
	}

	private static void LogRowDiagnostics(object row, string expectedName, string iconKey)
	{
		try
		{
			object go = GameRefs.ReadMember(row, "gameObject");
			bool activeSelf = GameRefs.ReadMember(go, "activeSelf") is bool b && b;
			string nameStr = ReadTextProperty(row, "name_text");
			string valueStr = ReadTextProperty(row, "value");
			object icon = GameRefs.ReadMember(row, "icon");
			bool iconSprite = (icon != null && GameRefs.ReadMember(icon, "sprite") != null);
			LogOnce("строка идеологии: active=" + activeSelf + ", name='" + nameStr + "', value='" + valueStr
				+ "', иконка=" + iconSprite + " (" + iconKey + "), ожидалось='" + expectedName + "'");
		}
		catch (Exception ex)
		{
			LogOnce("диагностика строки идеологии не удалась: " + ex.Message);
		}
	}

	private static string ReadTextProperty(object row, string field)
	{
		object textObject = GameRefs.ReadProperty(row, field);
		if (textObject == null)
		{
			return "?";
		}
		object text = GameRefs.ReadProperty(textObject, "text");
		return (text == null) ? "?" : text.ToString();
	}

	private static object GetFieldInHierarchy(object target, string name)
	{
		for (Type type = target.GetType(); type != null; type = type.BaseType)
		{
			FieldInfo field = type.GetField(name, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
			if (field != null)
			{
				return field.GetValue(target);
			}
		}
		return null;
	}

	private static int PatchDeath()
	{
		//IL_0069: Unknown result type (might be due to invalid IL or missing references)
		//IL_0076: Expected O, but got Unknown
		Type typeFromHandle = typeof(Actor);
		string[] array = new string[5] { "die", "killHimself", "getKilled", "applyDeath", "dead" };
		for (int i = 0; i < array.Length; i++)
		{
			MethodInfo methodInfo = FindMethod(typeFromHandle, array[i]);
			if (!(methodInfo == null))
			{
				try
				{
					// Префикс, а не постфикс: внутри die() игра сбрасывает kingdom.king,
					// профессию и (позже, в Dispose) data/attackedBy — постфикс уже не видит
					// ни статус «был королём», ни убийцу, ни тип урона.
					_harmony.Patch((MethodBase)methodInfo, new HarmonyMethod(typeof(Patches), "DeathPrefix", (Type[])null), new HarmonyMethod(typeof(Patches), "DeathPostfix", (Type[])null), (HarmonyMethod)null, (HarmonyMethod)null, (HarmonyMethod)null);
					LogService.LogInfo("[Real Politic] патч смерти установлен на Actor." + array[i]);
					return 1;
				}
				catch (Exception ex)
				{
					LogService.LogError("[Real Politic] не удалось установить патч на Actor." + array[i] + ": " + ex.Message);
				}
			}
		}
		LogService.LogError("[Real Politic] метод смерти юнита не найден — хроника смертей отключена");
		return 0;
	}

	private static MethodInfo FindMethod(Type type, string name)
	{
		MethodInfo[] methods = type.GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
		for (int i = 0; i < methods.Length; i++)
		{
			if (methods[i].Name == name)
			{
				return methods[i];
			}
		}
		return null;
	}

	private sealed class DeathHookState
    {
        public Actor Actor;
        public Kingdom Kingdom;
        public int ActorId;
    }

    private static void DeathPostfix(DeathHookState __state)
    {
        if (__state == null) return;
        try { RealPoliticMod.Deaths?.ConfirmActorDeath(__state.Actor, __state.ActorId, __state.Kingdom); }
        catch (Exception ex) { LogOnce("подтверждение смерти: " + ex.Message); }
    }

    private static void DeathPrefix(object __instance, object[] __args, out DeathHookState __state)
	{
        __state = null;
		try
		{
			Actor val = (Actor)((__instance is Actor) ? __instance : null);
			if (val == null)
			{
				return;
			}
			// Записываем только фактический переход «жив → мёртв»; die() может вызываться
			// повторно на уже мёртвом юните (например, при уничтожении объекта).
			if (!ActorAlive(val))
			{
				return;
			}
			string text = null;
			// Второй параметр die(bool, AttackType, bool, bool) — тип атаки, ставший причиной смерти.
			if (__args != null && __args.Length > 1 && __args[1] != null)
			{
				text = __args[1].ToString();
			}
			if (string.IsNullOrEmpty(text))
			{
				// Запасной вариант: поле _last_attack_type юнита.
				object last = GameRefs.ReadProperty(val, "_last_attack_type");
				if (last != null)
				{
					text = last.ToString();
				}
			}
			__state = new DeathHookState { Actor = val, Kingdom = val.kingdom, ActorId = GameText.ActorId(val) };
            Actor killer = ReadAttackedBy(val);
			RealPoliticMod.Deaths?.OnActorDeath(val, text, killer);
		}
		catch (Exception ex)
		{
			LogService.LogError("[Real Politic] ошибка в патче смерти: " + ex.Message);
		}
	}

	private static bool ActorAlive(Actor actor)
	{
		try
		{
			if (GameRefs.Invoke(actor, "isAlive") is bool result)
			{
				return result;
			}
			return true;
		}
		catch (Exception)
		{
			return true;
		}
	}

	private static Actor ReadAttackedBy(Actor actor)
	{
		try
		{
			object atk = GameRefs.ReadProperty(actor, "attackedBy");
			if (atk == null)
			{
				return null;
			}
			// Текущая игра: Actor.attackedBy — BaseSimObject (сам убийца). Старые сборки: обёртка с полем «a».
			Actor a = atk as Actor ?? GameRefs.ReadProperty(atk, "a") as Actor;
			if (a == null || ReferenceEquals(a, actor))
			{
				return null;
			}
			return a;
		}
		catch (Exception)
		{
			return null;
		}
	}

	public static void Uninstall()
	{
		try
		{
			if (_harmony != null)
			{
				Harmony.UnpatchID("com.realpolitic.worldbox");
			}
		}
		catch (Exception ex)
		{
			LogService.LogError("[Real Politic] не удалось снять патчи: " + ex.Message);
		}
	}
}
