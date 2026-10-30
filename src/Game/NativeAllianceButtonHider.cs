using System;
using System.Collections.Generic;
using System.Reflection;
using NeoModLoader.services;

namespace RealPolitic;

/// <summary>
/// 0.45: убираем из окна державы кнопку-«щит» (создать альянс / вступить в альянс).
///
/// Владелец 29.10.2026: «заблокируй возможность создавать или входить в альянсы,
/// если мод этого по тем или иным причинам пока не позволяет. Задолбал отвлекаться
/// на этот щит».
///
/// Как. Когда открыто окно державы, мод обходит кнопки окна и прячет те, что отвечают
/// за альянс: по имени объекта, по имени кнопки-подсказки (TooltipButton) и по ключу
/// локализации подписи — если там есть «alliance»/«альянс». Всё в try/catch: если игра
/// переименует кнопку, мод ничего не сломает, а в журнал попадёт строка со списком
/// найденных кнопок — по ней видно, что искать в следующей версии.
///
/// Функционально союз всё равно не заключить: это делает NativeAllianceGuards.
/// Здесь — только чтобы кнопка не мозолила глаза.
/// </summary>
public static class NativeAllianceButtonHider
{
	private static bool _logged;

	/// <summary>Спрятать кнопки альянса в окне державы. Вызывается при каждом открытии окна.</summary>
	public static void HideInKingdomWindow(object kingdomWindow)
	{
		if (!RealPoliticMod.BlockNativeAlliances || kingdomWindow == null)
		{
			return;
		}
		try
		{
			object windowGo = GameRefs.ReadMember(kingdomWindow, "gameObject");
			Type buttonType = Patches.FindGameType("UnityEngine.UI.Button") ?? Patches.FindGameType("Button");
			if (windowGo == null || buttonType == null)
			{
				LogOnce("кнопка альянса не спрятана: не найдено окно или тип Button");
				return;
			}
			object[] all = GameRefs.Invoke(windowGo, "GetComponentsInChildren", new object[] { buttonType, true }) as object[];
			if (all == null)
			{
				Array arr = GameRefs.Invoke(windowGo, "GetComponentsInChildren", new object[] { buttonType, true }) as Array;
				if (arr == null)
				{
					LogOnce("кнопка альянса не спрятана: GetComponentsInChildren не отдал список");
					return;
				}
				all = new object[arr.Length];
				for (int i = 0; i < arr.Length; i++)
				{
					all[i] = arr.GetValue(i);
				}
			}
			List<string> seen = new List<string>();
			int hidden = 0;
			for (int i = 0; i < all.Length; i++)
			{
				object button = all[i];
				if (button == null)
				{
					continue;
				}
				object go = GameRefs.ReadMember(button, "gameObject");
				string name = GameRefs.ReadMember(go, "name")?.ToString() ?? string.Empty;
				string markers = name + " " + ReadTooltip(go) + " " + ReadLabels(go);
				if (_logged && seen.Count < 60)
				{
					seen.Add(name);
				}
				if (!IsAllianceButton(markers))
				{
					continue;
				}
				SideTabButton.SetActivePublic(go, false);
				hidden++;
			}
			if (!_logged)
			{
				_logged = true;
				LogService.LogInfo("[Real Politic] кнопки окна державы (для поиска кнопки альянса): "
					+ string.Join(" | ", seen));
			}
			if (hidden > 0)
			{
				LogService.LogInfo("[Real Politic] кнопка альянса скрыта в окне державы (найдено: " + hidden + ")");
			}
		}
		catch (Exception ex)
		{
			LogOnce("кнопка альянса не спрятана: " + ex.Message);
		}
	}

	private static bool IsAllianceButton(string markers)
	{
		if (string.IsNullOrEmpty(markers))
		{
			return false;
		}
		string lower = markers.ToLowerInvariant();
		return lower.Contains("alliance") || lower.Contains("альянс") || lower.Contains("союз_")
			|| lower.Contains("alliance_button") || lower.Contains("ally");
	}

	/// <summary>Подсказка кнопки: у TooltipButton это поле с ключом локализации.</summary>
	private static string ReadTooltip(object go)
	{
		try
		{
			object tip = GameRefs.Invoke(GameRefs.Invoke(go, "GetComponent", new object[] { "TooltipButton" }), "GetType") != null
				? GameRefs.Invoke(go, "GetComponent", new object[] { "TooltipButton" })
				: null;
			if (tip == null)
			{
				return string.Empty;
			}
			return (GameRefs.ReadMember(tip, "tip_name")?.ToString() ?? string.Empty) + " "
				+ (GameRefs.ReadMember(tip, "tip_description")?.ToString() ?? string.Empty) + " "
				+ (GameRefs.ReadMember(tip, "localized_key")?.ToString() ?? string.Empty);
		}
		catch (Exception)
		{
			return string.Empty;
		}
	}

	/// <summary>Подписи внутри кнопки (LocalizedText с ключом и текстом).</summary>
	private static string ReadLabels(object go)
	{
		try
		{
			Type ltType = Patches.FindGameType("LocalizedText");
			if (ltType == null || go == null)
			{
				return string.Empty;
			}
			Array arr = GameRefs.Invoke(go, "GetComponentsInChildren", new object[] { ltType, true }) as Array;
			if (arr == null)
			{
				return string.Empty;
			}
			System.Text.StringBuilder sb = new System.Text.StringBuilder();
			for (int i = 0; i < arr.Length && i < 8; i++)
			{
				object lt = arr.GetValue(i);
				sb.Append(GameRefs.ReadMember(lt, "key")?.ToString() ?? string.Empty).Append(' ');
				sb.Append(GameRefs.ReadMember(lt, "text")?.ToString() ?? string.Empty).Append(' ');
			}
			return sb.ToString();
		}
		catch (Exception)
		{
			return string.Empty;
		}
	}

	private static readonly HashSet<string> _loggedMessages = new HashSet<string>();

	private static void LogOnce(string message)
	{
		if (_loggedMessages.Add(message))
		{
			LogService.LogInfo("[Real Politic] " + message);
		}
	}
}
