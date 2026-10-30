using System;
using System.Collections.Generic;
using System.Text;
using NeoModLoader.services;
using RealPolitic.Core;

namespace RealPolitic;

/// <summary>
/// 0.50.1: окно «Национализм» — чтобы всё, что делает национализм, было видно
/// не только в уведомлениях, но и там, где игрок сам его ищет.
///
/// Владелец 29.10.2026 (правило КМУД, но оно общее): «механика не должна быть
/// в секрете — всё видно». Здесь на одной странице:
///   • гражданское движение: какую родину хотят вернуть и насколько сильна обида;
///   • расовое движение: какой народ рвётся к власти и сколько его;
///   • кто у власти прямо сейчас и когда вернётся прежний строй;
///   • кому закрыт въезд и до какого года;
///   • чем наказал мир (санкции, потеря звания великой державы).
///
/// Окно только читает данные ядра, ничего не меняет.
/// </summary>
public static class NationalismWindow
{
	private const string WindowId = "realpolitic_nationalism";

	/// <summary>Ключ значка: он же файл icons/nationalism.png.</summary>
	public const string IconKeyPublic = "nationalism";

	private const string IconKey = IconKeyPublic;

	private static object _window;

	private static object _content;

	private static int _kingdomId;

	private static string _kingdomName = string.Empty;

	private static bool _disabled;

	// ---------------------------------------------------------------- публичный API

	public static void AttachCountry(int kingdomId, string kingdomName)
	{
		_kingdomId = kingdomId;
		_kingdomName = kingdomName ?? string.Empty;
	}

	public static void Open()
	{
		try
		{
			if (_disabled || RealPoliticMod.World == null)
			{
				return;
			}
			if (_kingdomId <= 0)
			{
				return;
			}
			if (_window == null && !Prewarm())
			{
				// Окно не создалось (например, игра ещё не готова) — молчим, но
				// повторно не долбим: следующий клик попробует снова после сброса.
				LogOnce("страница национализма: окно не создалось, попробую позже");
				_disabled = false;
				return;
			}
			if (_window == null || _content == null)
			{
				return;
			}
			try
			{
				Render();
			}
			catch (Exception buildEx)
			{
				ShowFailure(buildEx);
			}
			ModWindow.Show(WindowId, _window);
		}
		catch (Exception ex)
		{
			LogOnce("окно национализма не открылось: " + ex.Message);
		}
	}

	/// <summary>Заранее создаёт пустое окно, чтобы первый клик не «мигал» окном державы.</summary>
	public static bool Prewarm()
	{
		if (_disabled)
		{
			return false;
		}
		if (_window != null && ModUi.IsAlive(_window))
		{
			return true;
		}
		_window = ModWindow.Create(WindowId, "RP50_WIN_NATIONALISM",
			GameText.Localized("RP50_WIN_NATIONALISM", "Национализм"), IconKey, out object content);
		if (_window == null)
		{
			_disabled = true;
			LogOnce("страница национализма: окно игры не создано — раздел останется кнопкой без окна");
			return false;
		}
		_content = content ?? _window;
		ModUi.SetupAutoLayout(_content, spacing: 1f, padTop: 6f);
		return true;
	}

	// ---------------------------------------------------------------- отрисовка

	private static void Render()
	{
		PoliticalWorld world = RealPoliticMod.World;
		KingdomPolitics state = world.GetKingdom(_kingdomId);
		ModUi.ClearChildren(_content);
		if (state == null)
		{
			ModUi.Text(_content, GameText.Localized("RP50_WIN_NO_KINGDOM",
				"Держава не найдена: возможно, она уже исчезла с карты."), 11f, 0.85f, 0.55f, 0.45f);
			return;
		}
		ModWindow.SetTitle(_window, _kingdomName + " — " + GameText.Localized("RP50_WIN_NATIONALISM", "Национализм"));

		NationalismState civic = world.NationalismOf(_kingdomId);
		RaceNationalismState race = world.RaceMovementOf(_kingdomId);
		bool anyCivic = civic != null && (civic.Active || civic.Pressure > 0f || civic.SeizedYear > 0);
		bool anyRace = race != null && (race.Active || race.Pressure > 0f || race.SeizedYear > 0);

		if (!anyCivic && !anyRace && !HasBans(world) && !HasSanctions(world))
		{
			ModUi.Text(_content, GameText.Localized("RP50_WIN_CALM",
				"Пока тихо: бывших земель у державы нет, чужих народов тоже — националистам не за что бороться."),
				11f, 0.86f, 0.84f, 0.72f);
			ModUi.Spacer(_content, 6f);
			Explain();
			return;
		}

		if (anyCivic)
		{
			DrawCivic(world, civic);
		}
		if (anyRace)
		{
			DrawRace(world, race);
		}
		DrawBans(world);
		DrawPunishment(world);
		ModUi.Spacer(_content, 6f);
		Explain();
	}

	/// <summary>Гражданский национализм: вернуть своё государство.</summary>
	private static void DrawCivic(PoliticalWorld world, NationalismState civic)
	{
		Header(GameText.Localized("RP50_WIN_CIVIC", "Гражданский национализм — вернуть своё государство"));
		if (!string.IsNullOrEmpty(civic.OriginName))
		{
			Row(GameText.Localized("RP50_WIN_ORIGIN", "Хотят вернуть государство"),
				"«" + civic.OriginName + "»");
		}
		if (civic.OriginYear > 0)
		{
			Row(GameText.Localized("RP50_WIN_LOST_YEAR", "Земли потеряны в"), civic.OriginYear + " г.");
		}
		Row(GameText.Localized("RP50_WIN_BOND", "Как присоединили"),
			GameText.Localized(BondKey(civic.Bond), BondFallback(civic.Bond)));
		Row(GameText.Localized("RP50_WIN_PRESSURE", "Сила движения"), Bar(civic.Pressure));
		Row(GameText.Localized("RP50_WIN_STATE", "Что сейчас"),
			GameText.Localized(StateKey(civic, world.Year), StateFallback(civic, world.Year)));
		if (civic.SeizedYear > 0)
		{
			Row(GameText.Localized("RP50_WIN_RETURN", "Прежний строй вернётся"),
				(civic.SeizedYear + NationalismEngine.ReturnAfterYears) + " г.");
		}
		else if (civic.ShadowUntilYear > world.Year)
		{
			Row(GameText.Localized("RP50_WIN_SHADOW", "Движение затихло до"), civic.ShadowUntilYear + " г.");
		}
		if (civic.Victories > 0)
		{
			Row(GameText.Localized("RP50_WIN_VICTORIES", "Побед у движения"), civic.Victories.ToString());
		}
		ModUi.Spacer(_content, 5f);
	}

	/// <summary>Расовый национализм: чужая раса рвётся к власти.</summary>
	private static void DrawRace(PoliticalWorld world, RaceNationalismState race)
	{
		Header(GameText.Localized("RP50_WIN_RACE", "Расовый национализм — чужая раса хочет власти"));
		if (!string.IsNullOrEmpty(race.RaceName))
		{
			Row(GameText.Localized("RP50_WIN_RACE_WHO", "Народ"), "«" + race.RaceName + "»");
		}
		Row(GameText.Localized("RP50_WIN_RACE_SHARE", "Доля в державе"), Percent(race.Share));
		Row(GameText.Localized("RP50_WIN_PRESSURE", "Сила движения"), Bar(race.Pressure));
		Row(GameText.Localized("RP50_WIN_RACE_STATE", "Что сейчас"),
			GameText.Localized(RaceStateKey(race, world.Year), RaceStateFallback(race, world.Year)));
		if (race.SeizedYear > 0)
		{
			Row(GameText.Localized("RP50_WIN_RETURN", "Прежний строй вернётся"),
				(race.SeizedYear + RaceNationalismEngine.ReturnAfterYears) + " г.");
		}
		else if (race.ShadowUntilYear > world.Year)
		{
			Row(GameText.Localized("RP50_WIN_SHADOW", "Движение затихло до"), race.ShadowUntilYear + " г.");
		}
		ModUi.Spacer(_content, 5f);
	}

	/// <summary>Кому закрыт въезд и до какого года.</summary>
	private static void DrawBans(PoliticalWorld world)
	{
		if (!HasBans(world))
		{
			return;
		}
		Header(GameText.Localized("RP50_WIN_BANS", "Кому закрыт въезд"));
		int shown = 0;
		IReadOnlyList<CitizenshipBan> bans = world.CitizenshipBans;
		for (int i = 0; i < bans.Count; i++)
		{
			CitizenshipBan ban = bans[i];
			if (ban.KingdomId != _kingdomId || ban.UntilYear <= world.Year)
			{
				continue;
			}
			string who = string.IsNullOrEmpty(ban.RaceName) ? ban.RaceId : ban.RaceName;
			string what = ban.CitizenshipRevoked
				? GameText.Localized("RP50_WIN_BAN_REVOKED", "гражданство отозвано, въезд закрыт")
				: GameText.Localized("RP50_WIN_BAN_CLOSED", "въезд закрыт");
			Row("«" + who + "»", what + " — " + GameText.Localized("RP50_WIN_UNTIL", "до") + " " + ban.UntilYear + " г.");
			shown++;
			if (shown >= 6)
			{
				break;
			}
		}
		if (shown == 0)
		{
			Row(GameText.Localized("RP50_WIN_BANS_NONE", "Сейчас"), GameText.Localized("RP50_WIN_BANS_EXPIRED", "все прошлые запреты уже истекли"));
		}
		ModUi.Spacer(_content, 5f);
	}

	/// <summary>Чем наказал мир.</summary>
	private static void DrawPunishment(PoliticalWorld world)
	{
		bool sanctioned = false;
		IReadOnlyList<KmudSanction> sanctions = world.Kmud?.Sanctions;
		if (sanctions != null)
		{
			for (int i = 0; i < sanctions.Count; i++)
			{
				if (sanctions[i].KingdomId == _kingdomId && sanctions[i].UntilYear > world.Year)
				{
					Row(GameText.Localized("RP50_WIN_SANCTIONS", "Санкции мира"),
						GameText.Localized("RP50_WIN_UNTIL", "до") + " " + sanctions[i].UntilYear + " г. ("
						+ GameText.Localized("RP50_WIN_SUPPORTERS", "поддержали держав") + ": " + sanctions[i].Supporters + ")");
					sanctioned = true;
					break;
				}
			}
		}
		if (world.HasFreshHateStain(_kingdomId, world.Year))
		{
			Row(GameText.Localized("RP50_WIN_STAIN", "Память мира"),
				GameText.Localized("RP50_WIN_STAIN_TEXT",
					"державу назовут обвиняемой на ближайшем съезде КМУД — за отнятое гражданство"));
		}
		if (sanctioned)
		{
			ModUi.Spacer(_content, 5f);
		}
	}

	private static void Explain()
	{
		string help = GameText.Localized("RP50_WIN_HELP", "Как это устроено: обида растёт от бывших земель и от чужих народов, через два поколения память тускнеет. Перевалив порог, националисты берут власть, через три года возвращают прежний строй и уходят в тень. Отнятое гражданство не высылает людей — оно просто закрывает им дверь.");
		ModUi.Text(_content, help, 10f, 0.72f, 0.70f, 0.62f);
	}

	// ---------------------------------------------------------------- мелкие помощники

	private static bool HasBans(PoliticalWorld world)
	{
		IReadOnlyList<CitizenshipBan> bans = world.CitizenshipBans;
		for (int i = 0; i < bans.Count; i++)
		{
			if (bans[i].KingdomId == _kingdomId && bans[i].UntilYear > world.Year)
			{
				return true;
			}
		}
		return false;
	}

	private static bool HasSanctions(PoliticalWorld world)
	{
		IReadOnlyList<KmudSanction> sanctions = world.Kmud?.Sanctions;
		if (sanctions == null)
		{
			return false;
		}
		for (int i = 0; i < sanctions.Count; i++)
		{
			if (sanctions[i].KingdomId == _kingdomId && sanctions[i].UntilYear > world.Year)
			{
				return true;
			}
		}
		return false;
	}

	private static void Header(string text)
	{
		ModUi.Text(_content, text, 12f, 0.95f, 0.88f, 0.66f, bold: true);
	}

	private static void Row(string caption, string value)
	{
		ModUi.Text(_content, caption + ": " + value, 11f, 0.88f, 0.86f, 0.78f);
	}

	private static string Percent(float share)
	{
		int value = (int)Math.Round(Math.Max(0f, Math.Min(1f, share)) * 100f);
		return value + " %";
	}

	/// <summary>Полоска вместо числа: «████░░░░ 47 из 100» — сразу видно, далеко ли до порога.</summary>
	private static string Bar(float pressure)
	{
		float value = Math.Max(0f, Math.Min(100f, pressure));
		int filled = (int)Math.Round(value / 10f);
		StringBuilder sb = new StringBuilder();
		for (int i = 0; i < 10; i++)
		{
			sb.Append(i < filled ? '█' : '░');
		}
		sb.Append(' ').Append((int)Math.Round(value)).Append(" / 100");
		if (value >= NationalismEngine.PowerThreshold)
		{
			sb.Append(' ').Append(GameText.Localized("RP50_WIN_ABOVE", "(выше порога — могут взять власть)"));
		}
		return sb.ToString();
	}

	private static string BondKey(HomesteadBond bond)
	{
		return "RP49_BOND_" + bond.ToString().ToUpperInvariant();
	}

	private static string BondFallback(HomesteadBond bond)
	{
		switch (bond)
		{
			case HomesteadBond.Union: return "родина вошла в унию";
			case HomesteadBond.Federation: return "родина вошла в федерацию";
			case HomesteadBond.Vassalage: return "родина стала вассалом";
			default: return "родину завоевали";
		}
	}

	private static string StateKey(NationalismState civic, int year)
	{
		if (civic.SeizedYear > 0)
		{
			return "RP50_WIN_ST_IN_POWER";
		}
		if (civic.ShadowUntilYear > year)
		{
			return "RP50_WIN_ST_SHADOW";
		}
		if (civic.Pressure >= NationalismEngine.PowerThreshold)
		{
			return "RP50_WIN_ST_READY";
		}
		return civic.Pressure > 0f ? "RP50_WIN_ST_GROWING" : "RP50_WIN_ST_QUIET";
	}

	private static string StateFallback(NationalismState civic, int year)
	{
		if (civic.SeizedYear > 0) return "националисты у власти";
		if (civic.ShadowUntilYear > year) return "движение затихло";
		if (civic.Pressure >= NationalismEngine.PowerThreshold) return "готовы взять власть";
		return civic.Pressure > 0f ? "копят силы" : "тихо";
	}

	private static string RaceStateKey(RaceNationalismState race, int year)
	{
		if (race.SeizedYear > 0)
		{
			return "RP50_WIN_ST_IN_POWER";
		}
		if (race.ShadowUntilYear > year)
		{
			return "RP50_WIN_ST_SHADOW";
		}
		if (race.Pressure >= RaceNationalismEngine.PowerThreshold)
		{
			return "RP50_WIN_ST_READY";
		}
		return race.Pressure > 0f ? "RP50_WIN_ST_GROWING" : "RP50_WIN_ST_QUIET";
	}

	private static string RaceStateFallback(RaceNationalismState race, int year)
	{
		if (race.SeizedYear > 0) return "этот народ у власти";
		if (race.ShadowUntilYear > year) return "движение затихло";
		if (race.Pressure >= RaceNationalismEngine.PowerThreshold) return "готовы взять власть";
		return race.Pressure > 0f ? "копят силы" : "тихо";
	}

	private static void ShowFailure(Exception ex)
	{
		try
		{
			ModUi.ClearChildren(_content);
			ModUi.Text(_content, GameText.Localized("RP50_WIN_FAIL",
				"Не удалось собрать страницу национализма. Мод работает, просто эта страница пустая."),
				11f, 0.9f, 0.6f, 0.5f);
			ModUi.Text(_content, ex.Message, 9f, 0.7f, 0.6f, 0.55f);
		}
		catch
		{
			// пусто: даже сообщение об ошибке не должно ронять мод
		}
		LogOnce("страница национализма не собрана: " + ex.Message);
	}

	private static void LogOnce(string message)
	{
		LogService.LogInfo("[Real Politic] " + message);
	}
}
