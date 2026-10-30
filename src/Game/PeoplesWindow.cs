using System;
using System.Collections.Generic;
using NeoModLoader.services;
using RealPolitic.Core;

namespace RealPolitic;

/// <summary>
/// 0.52: страница «Народы державы».
///
/// Владелец 30.10.2026: механика не должна быть в секрете, а глазами хочется видеть,
/// кто в державе хозяин, кто пришлый и что с языком. Здесь три блока:
///   1) народы: сколько душ, с какого года, на каком языке говорят;
///   2) местные уставы: у каких городов своя конституция и чем она недовольна;
///   3) заряды на будущее: кто растворяется, а кто держится.
///
/// Страница только читает данные ядра.
/// </summary>
public static class PeoplesWindow
{
	private const string WindowId = "realpolitic_peoples";

	public const string IconKeyPublic = "tab_civic_votes";

	private static object _window;

	private static object _content;

	private static int _kingdomId;

	private static string _kingdomName = string.Empty;

	private static bool _disabled;

	public static void AttachCountry(int kingdomId, string kingdomName)
	{
		_kingdomId = kingdomId;
		_kingdomName = kingdomName ?? string.Empty;
	}

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
		_window = ModWindow.Create(WindowId, "RP52_WIN_TITLE",
			GameText.Localized("RP52_WIN_TITLE", "Народы"), IconKeyPublic, out object content);
		if (_window == null)
		{
			_disabled = true;
			LogOnce("страница народов: окно игры не создано");
			return false;
		}
		_content = content ?? _window;
		ModUi.SetupAutoLayout(_content, spacing: 1f, padTop: 6f);
		return true;
	}

	public static void Open()
	{
		try
		{
			if (_disabled || RealPoliticMod.World == null || _kingdomId <= 0)
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
			try
			{
				Render();
			}
			catch (Exception ex)
			{
				ModUi.ClearChildren(_content);
				ModUi.Text(_content, GameText.Localized("RP52_WIN_FAIL",
					"Не удалось собрать страницу народов. Мод работает, страница пустая."), 11f, 0.9f, 0.6f, 0.5f);
				LogOnce("страница народов не собрана: " + ex.Message);
			}
			ModWindow.Show(WindowId, _window);
		}
		catch (Exception ex)
		{
			LogOnce("страница народов не открылась: " + ex.Message);
		}
	}

	private static void Render()
	{
		PoliticalWorld world = RealPoliticMod.World;
		KingdomPolitics state = world.GetKingdom(_kingdomId);
		if (state == null)
		{
			ModUi.Text(_content, GameText.Localized("RP52_WIN_GONE",
				"Держава не найдена: возможно, она уже исчезла с карты."), 11f, 0.85f, 0.55f, 0.45f);
			return;
		}
		ModWindow.SetTitle(_window, _kingdomName + " — " + GameText.Localized("RP52_WIN_TITLE", "Народы"));
		ModUi.ClearChildren(_content);

		Dictionary<string, PeopleRecord> book = world.PeoplesOf(_kingdomId);
		Constitution constitution = state.Constitution;

		// ---- 1. Народы
		ModUi.Text(_content, GameText.Localized("RP52_WIN_PEOPLES", "Кто живёт в державе"),
			12f, 0.95f, 0.88f, 0.66f, bold: true);
		if (book == null || book.Count == 0)
		{
			ModUi.Text(_content, GameText.Localized("RP52_WIN_NO_PEOPLES",
				"Пока нечего показать: мод ещё не видел городов этой державы. Подождите год-другой игрового времени."),
				11f, 0.86f, 0.84f, 0.72f);
		}
		else
		{
			List<PeopleRecord> list = new List<PeopleRecord>(book.Values);
			list.Sort((a, b) => b.Share.CompareTo(a.Share));
			for (int i = 0; i < list.Count && i < 8; i++)
			{
				PeopleRecord record = list[i];
				string title = record.RaceName + " — " + Percent(record.Share)
					+ (record.Titular ? GameText.Localized("RP52_WIN_TITULAR", " · коренной народ") : "");
				ModUi.Text(_content, title, 11.5f, 0.93f, 0.90f, 0.78f, bold: true);
				string arrived = record.Titular
					? GameText.Localized("RP52_WIN_ALWAYS", "живёт здесь всегда")
					: GameText.Localized("RP52_WIN_ARRIVED", "пришли в") + " " + record.FirstSeenYear + " "
						+ GameText.Localized("RP52_WIN_YEAR", "году");
				ModUi.Text(_content, "   " + arrived, 10f, 0.78f, 0.76f, 0.66f);
				ModUi.Text(_content, "   " + GameText.Localized("RP52_WIN_LANG", "язык") + ": "
					+ GameText.Localized(AssimilationEngine.StageKey(record.Stage), "свой и общий"),
					10f, 0.78f, 0.76f, 0.66f);
				if (record.Stage != LanguageStage.Common)
				{
					int left = AssimilationEngine.YearsUntilNextStage(record.Stage, record.Share,
						constitution?.Language == LanguageLaw.Free, constitution?.Court == CourtLaw.Own,
						constitution?.Race == RaceLaw.Segregated);
					if (left > 0)
					{
						ModUi.Text(_content, "   " + GameText.Localized("RP52_WIN_LANG_LEFT", "следующая ступень через")
							+ " " + left + " " + GameText.Localized("RP52_WIN_YEARS", "лет"),
							10f, 0.72f, 0.70f, 0.60f);
					}
					else
					{
						ModUi.Text(_content, "   " + GameText.Localized("RP52_WIN_LANG_KEEP",
							"свой язык никуда не денется — так и останется двумя культурами"),
							10f, 0.72f, 0.82f, 0.66f);
					}
				}
				ModUi.Spacer(_content, 3f);
			}
		}

		// ---- 2. Местные уставы
		ModUi.Spacer(_content, 4f);
		ModUi.Text(_content, GameText.Localized("RP52_WIN_LOCALS", "Города со своим уставом"),
			12f, 0.95f, 0.88f, 0.66f, bold: true);
		int shown = 0;
		bool allowLocal = constitution?.AllowLocalConstitutions ?? true;
		bool tolerate = constitution?.TolerateLocalContradictions ?? true;
		foreach (LocalConstitution local in world.LocalConstitutions.Values)
		{
			if (local.KingdomId != _kingdomId)
			{
				continue;
			}
			string status = GameText.Localized(LocalConstitutionRules.StatusKey(local, tolerate), "устав в силе");
			ModUi.Text(_content, world.CityNameOf(local.CityId) + " — " + local.RaceName, 11.5f, 0.93f, 0.90f, 0.78f, bold: true);
			ModUi.Text(_content, "   " + status, 10f, 0.80f, 0.78f, 0.68f);
			if (local.Contradicts)
			{
				string what = local.WantsLanguage && local.WantsCourt
					? GameText.Localized("RP52_WIN_WANTS_BOTH", "просят свой язык и свой суд")
					: (local.WantsLanguage
						? GameText.Localized("RP52_WIN_WANTS_LANG", "просят свой язык")
						: GameText.Localized("RP52_WIN_WANTS_COURT", "просят свой суд"));
				ModUi.Text(_content, "   " + what, 10f, 0.82f, 0.74f, 0.62f);
			}
			ModUi.Text(_content, "   " + GameText.Localized("RP52_WIN_GRIEVANCE", "обида на столицу") + ": "
				+ Gauge(local.Grievance), 10f, 0.78f, 0.76f, 0.66f);
			shown++;
			ModUi.Spacer(_content, 3f);
			if (shown >= 6)
			{
				break;
			}
		}
		if (shown == 0)
		{
			ModUi.Text(_content, allowLocal
				? GameText.Localized("RP52_WIN_NO_LOCALS",
					"Своих уставов нет: ни в одном городе пришлый народ не составляет больше половины.")
				: GameText.Localized("RP52_WIN_LOCALS_BANNED",
					"Столица запретила народам свои уставы — по конституции. Если такие города есть, они копят обиду."),
				10.5f, 0.80f, 0.76f, 0.64f);
		}

		// ---- 3. Чего ждать
		ModUi.Spacer(_content, 4f);
		string laws = GameText.Localized("RP52_WIN_LAWS", "Законы о народах")
			+ ": " + RaceLawText(constitution?.Race ?? RaceLaw.Equal)
			+ " · " + LanguageLawText(constitution?.Language ?? LanguageLaw.Free)
			+ " · " + CourtLawText(constitution?.Court ?? CourtLaw.Own);
		ModUi.Text(_content, laws, 10.5f, 0.86f, 0.84f, 0.72f);
		string ruler = (constitution?.ForeignRulerAllowed ?? true)
			? GameText.Localized("RP52_WIN_RULER_OK", "Править может любой народ — конституция не запрещает.")
			: GameText.Localized("RP52_WIN_RULER_NO", "Править может только коренной народ — так записано в конституции.");
		ModUi.Text(_content, ruler, 10.5f, 0.86f, 0.84f, 0.72f);
		ModUi.Spacer(_content, 4f);
		ModUi.Text(_content, GameText.Localized("RP52_WIN_HELP",
			"Как это устроено: народ не превращается в другой народ — меняется только язык. Со своим языком и своим судом народ живёт двумя культурами вечно. Ассимиляция силой быстрее, но она даёт бунт, а не мир."),
			10f, 0.72f, 0.70f, 0.62f);
	}

	private static string RaceLawText(RaceLaw law)
	{
		switch (law)
		{
			case RaceLaw.Titular: return GameText.Localized("RP52_RACE_TITULAR", "коренным больше прав");
			case RaceLaw.Segregated: return GameText.Localized("RP52_RACE_SEGREGATED", "пришлым только жить и работать");
			default: return GameText.Localized("RP52_RACE_EQUAL", "все равны");
		}
	}

	private static string LanguageLawText(LanguageLaw law)
	{
		return law == LanguageLaw.Free
			? GameText.Localized("RP52_LANG_LAW_FREE", "свой язык в школах")
			: GameText.Localized("RP52_LANG_LAW_COMMON", "только общий язык");
	}

	private static string CourtLawText(CourtLaw law)
	{
		return law == CourtLaw.Own
			? GameText.Localized("RP52_COURT_OWN", "свой суд у народов")
			: GameText.Localized("RP52_COURT_COMMON", "общий суд для всех");
	}

	private static string Percent(float share)
	{
		return (int)Math.Round(Math.Max(0f, Math.Min(1f, share)) * 100f) + " %";
	}

	private static string Gauge(float value)
	{
		float clamped = Math.Max(0f, Math.Min(100f, value));
		int filled = (int)Math.Round(clamped / 10f);
		string bar = string.Empty;
		for (int i = 0; i < 10; i++)
		{
			bar += i < filled ? "█" : "░";
		}
		return bar + " " + (int)Math.Round(clamped) + " / 100";
	}

	private static void LogOnce(string message)
	{
		LogService.LogInfo("[Real Politic] " + message);
	}
}
