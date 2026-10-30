using System;
using System.Reflection;
using NeoModLoader.services;
using RealPolitic.Core;

namespace RealPolitic;

/// <summary>
/// 0.45: одна вкладка мода вместо трёх.
///
/// Зачем. Владелец 29.10.2026: «а ещё слева кнопки уже исчезают». Так и было:
/// мод добавлял в окно державы ТРИ своих вкладки (конституция, КМУД, голосования).
/// Вместе со штатными вкладками игры они не помещались в боковую панель, и нижние
/// кнопки уезжали за край экрана.
///
/// Теперь в окне державы одна вкладка «Real Politic», а внутри — оглавление:
/// КМУД, Конституция, Голосования. Старые отдельные вкладки больше не создаются,
/// поэтому панель всегда умещается.
/// </summary>
public static class ModHubWindow
{
	private const string WindowId = "realpolitic_hub";
	private const string TabObjectName = "rp_mod_tab";
	private const string TabTitleKey = "RP_HUB_TAB";

	private static object _window;
	private static object _content;
	private static int _kingdomId;
	private static string _kingdomName = string.Empty;
	private static bool _disabled;

	public static bool Prewarm()
	{
		if (_window != null)
		{
			return true;
		}
		try
		{
			_window = ModWindow.Create(WindowId, TabTitleKey,
				GameText.Localized(TabTitleKey, "Real Politic"), ModIcons.KmudKey, out object content);
			if (_window == null)
			{
				return false;
			}
			_content = content ?? _window;
			ModUi.SetupAutoLayout(_content, 1f, 6f);
			return true;
		}
		catch (Exception ex)
		{
			Disable("подготовка окна Real Politic: " + ex.Message);
			return false;
		}
	}

	/// <summary>Единственная вкладка мода в окне державы.</summary>
	public static void AttachOpenButton(object kingdomWindow, int kingdomId, string kingdomName)
	{
		try
		{
			if (_disabled)
			{
				return;
			}
			_kingdomId = kingdomId;
			_kingdomName = kingdomName ?? string.Empty;
			SideTabButton.Ensure(kingdomWindow, TabObjectName, ModIcons.KmudKey, ModIcons.PathFor(ModIcons.KmudKey),
				GameText.Localized(TabTitleKey, "Real Politic"),
				typeof(ModHubWindow).GetMethod(nameof(Open), BindingFlags.Static | BindingFlags.Public));
		}
		catch (Exception ex)
		{
			LogOnce("вкладка Real Politic не прицеплена: " + ex.Message);
		}
	}

	/// <summary>Скрыть старые отдельные вкладки, если они остались от прошлых версий мода.</summary>
	public static void HideLegacyTabs(object kingdomWindow)
	{
		try
		{
			if (kingdomWindow == null)
			{
				return;
			}
			object tabs = GameRefs.ReadMember(kingdomWindow, "tabs");
			object tabsGo = tabs != null ? GameRefs.ReadMember(tabs, "gameObject") : null;
			object transform = tabsGo != null ? GameRefs.ReadMember(tabsGo, "transform") : null;
			if (transform == null)
			{
				return;
			}
			string[] legacy = { "rp_kmud_tab", "rp_constitution_tab", "rp_votes_tab" };
			for (int i = 0; i < legacy.Length; i++)
			{
				object found = GameRefs.Invoke(transform, "Find", legacy[i]);
				object go = found != null ? GameRefs.ReadMember(found, "gameObject") : null;
				SideTabButton.SetActivePublic(go, false);
			}
		}
		catch (Exception ex)
		{
			LogOnce("старые вкладки мода не скрыты: " + ex.Message);
		}
	}

	public static void Open()
	{
		try
		{
			if (_disabled || RealPoliticMod.World == null)
			{
				return;
			}
			if (_window == null && !Prewarm())
			{
				return;
			}
			if (_window == null || _content == null)
			{
				return;
			}
			Render();
			ModWindow.Show(WindowId, _window);
		}
		catch (Exception ex)
		{
			Disable("окно Real Politic не открылось: " + ex.Message);
		}
	}

	/// <summary>
	/// Оглавление мода: четыре раздела крупными кнопками во всю ширину окна.
	/// Владелец 29.10.2026: «кнопки должны быть во всю ширину окна и типо там должно
	/// быть написано „Конституция“ и я читаю конституцию страны при нажатии. Или „КМУД“
	/// и я смотрю последний отчёт КМУД».
	/// </summary>
	private static void Render()
	{
		try
		{
			PoliticalWorld world = RealPoliticMod.World;
			ModUi.ClearChildren(_content);
			ModUi.Text(_content, _kingdomName, 15f, 0.92f, 0.89f, 0.78f, bold: true);
			ModUi.Text(_content, GameText.Localized("RP_HUB_HINT",
				"Разделы мода для этой державы."), 11f, 0.80f, 0.78f, 0.70f);
			ModUi.Spacer(_content, 6f);

			// КМУД — состояние конференции и ход голосования.
			Section(GameText.Localized("RP_HUB_KMUD", "КМУД"), GameText.Localized("RP_HUB_KMUD_HINT",
				"Последний отчёт конференции и ход голосования."), ModIcons.KmudKey, () =>
			{
				KmudWindow.AttachOpenButtonSafe(_kingdomId, _kingdomName);
				KmudWindow.Open();
			});

			// Конституция — только если она включена в настройках.
			if (world?.Params?.EnableConstitution == true)
			{
				Section(GameText.Localized("RP_HUB_CONSTITUTION", "Конституция"), GameText.Localized("RP_HUB_CONSTITUTION_HINT",
					"Главный закон державы простыми словами: кто правит, кто объявляет войну, что с границами."),
					ModIcons.ConstitutionKey, () =>
				{
					ConstitutionWindow.AttachOpenButtonSafe(_kingdomId, _kingdomName);
					ConstitutionWindow.Open();
				});
			}

			// Голосования — только при включённом управлении (Governance).
			if (world?.Governance?.Enabled == true)
			{
				Section(GameText.Localized("RP_HUB_VOTES", "Голосования"), GameText.Localized("RP_HUB_VOTES_HINT",
					"Что сейчас решают в державе и кто за что голосует."), ModIcons.VotesKey, () =>
				{
					CivicVotesWindow.AttachCountry(_kingdomId);
					CivicVotesWindow.Open();
				});
			}

			// 0.52: народы — кто живёт, на каком языке и у кого свой устав.
			Section(GameText.Localized("RP_HUB_PEOPLES", "Народы"), GameText.Localized("RP_HUB_PEOPLES_HINT",
				"Кто в державе коренной, кто пришлый, на каком языке говорит и у каких городов свой устав."),
				PeoplesWindow.IconKeyPublic, () =>
			{
				PeoplesWindow.AttachCountry(_kingdomId, _kingdomName);
				PeoplesWindow.Open();
			});

			// 0.50.1: национализм — кто чего хочет и чем наказал мир.
			Section(GameText.Localized("RP_HUB_NATIONALISM", "Национализм"), GameText.Localized("RP_HUB_NATIONALISM_HINT",
				"Сила движения, кого хотят вернуть, кому закрыт въезд и какие санкции сейчас действуют."),
				NationalismWindow.IconKeyPublic, () =>
			{
				NationalismWindow.AttachCountry(_kingdomId, _kingdomName);
				NationalismWindow.Open();
			});

			// 0.46: парламент — 100 мест полукругом.
			Section(GameText.Localized("RP_HUB_PARLIAMENT", "Парламент"), GameText.Localized("RP_HUB_PARLIAMENT_HINT",
				"Сто мест полукругом: цвет — партия, нажмите на место и прочитайте, кто на нём сидит."),
				ModIcons.VotesKey, () =>
			{
				ParliamentWindow.AttachCountry(_kingdomId);
				ParliamentWindow.Open();
			});

			ModUi.Spacer(_content, 6f);
			ModUi.Text(_content, GameText.Localized("RP_HUB_FOOT",
				"Одна вкладка вместо трёх: кнопки в окне державы больше не уезжают за экран."),
				10f, 0.72f, 0.70f, 0.62f);
		}
		catch (Exception ex)
		{
			LogOnce("окно Real Politic не отрисовано: " + ex.Message);
		}
	}

	/// <summary>Кнопка во всю ширину окна + подпись под ней, объясняющая, что там внутри.</summary>
	private static void Section(string title, string hint, string iconKey, Action onClick)
	{
		ModUi.Button(_content, title, ModUi.PageWidth, 32f, 13f, 0.95f, 0.90f, 0.70f, iconKey, onClick);
		if (!string.IsNullOrEmpty(hint))
		{
			ModUi.Text(_content, hint, 10f, 0.74f, 0.72f, 0.63f);
		}
		ModUi.Spacer(_content, 6f);
	}

	private static void Disable(string reason)
	{
		_disabled = true;
		LogService.LogError("[Real Politic] окно Real Politic отключено: " + reason);
	}

	private static void LogOnce(string message)
	{
		LogService.LogInfo("[Real Politic] " + message);
	}
}
