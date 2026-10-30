using System;
using System.Collections.Generic;
using System.Reflection;
using NeoModLoader.services;
using RealPolitic.Core;

namespace RealPolitic;

/// <summary>
/// 0.36: окно КМУД — Конференции Мирного Урегулирования Дел.
///
/// Как устроено: содержимое — вертикальная колонка (автораскладка окна), а каждый
/// смысловой блок (шапка, карточка повестки, вопрос с полосами голосов) — это «холст»
/// ModUi.Canvas фиксированной высоты, внутри которого всё расставлено вручную через Place.
/// Так карточки получают фон, цветной корешок слева и полосы голосования — обычным
/// столбиком текста этого не сделать.
///
/// Ничего не ломает: любая ошибка Unity-рефлексии глушится и пишется один раз в лог,
/// окно просто не откроется, мод продолжает работать.
/// </summary>
public static class KmudWindow
{
	private const string WindowId = "realpolitic_kmud";

	// Ширина страницы окна (та же, что у конституции и древа).
	private const float W = ModUi.PageWidth;

	// Палитра: тёмный пергамент, золото, бронза, зелень согласия, багрец приговора.
	private const float GoldR = 0.95f, GoldG = 0.80f, GoldB = 0.42f;
	private const float BronzeR = 0.66f, BronzeG = 0.56f, BronzeB = 0.40f;
	private const float PaperR = 0.91f, PaperG = 0.89f, PaperB = 0.82f;
	private const float BloodR = 0.66f, BloodG = 0.24f, BloodB = 0.20f;
	private const float AmberR = 0.62f, AmberG = 0.42f, AmberB = 0.16f;
	private const float MossR = 0.22f, MossG = 0.44f, MossB = 0.32f;

	private static object _window;

	private static object _content;

	private static bool _disabled;

	private static int _kingdomId;

	private static string _kingdomName = string.Empty;

	// ---------------------------------------------------------------- публичный API

	/// <summary>Заранее создаёт пустое окно, чтобы первый клик не мигал поверх окна державы.</summary>
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
		try
		{
			_window = ModWindow.Create(WindowId, "RP36_KMUD_WIN_TITLE",
				GameText.Localized("RP36_KMUD_WIN_TITLE", "КМУД"), ModIcons.KmudKey, out object content);
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
			Disable("подготовка окна КМУД: " + ex.Message);
			return false;
		}
	}

	/// <summary>Прицепить вкладку «КМУД» на боковую панель окна державы.</summary>
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
			SideTabButton.Ensure(kingdomWindow, "rp_kmud_tab", ModIcons.KmudKey, ModIcons.PathFor(ModIcons.KmudKey),
				GameText.Localized("RP36_KMUD_OPEN_BUTTON", "🏛 КМУД — Конференция Мирного Урегулирования Дел"),
				typeof(KmudWindow).GetMethod("OnOpenClick", BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public));
		}
		catch (Exception ex)
		{
			LogOnce("вкладка КМУД не прицеплена: " + ex.Message);
		}
	}

	/// <summary>0.45: запомнить державу, не создавая отдельную вкладку (вкладка теперь одна — Real Politic).</summary>
	public static void AttachOpenButtonSafe(int kingdomId, string kingdomName)
	{
		_kingdomId = kingdomId;
		_kingdomName = kingdomName ?? string.Empty;
	}

	/// <summary>Клик по вкладке: открыть окно конференции.</summary>
	public static void OnOpenClick()
	{
		Open();
	}

	/// <summary>Открыть окно конференции для последней выбранной державы.</summary>
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
			Render(RealPoliticMod.World);
			ModWindow.SetTitle(_window, TitleForWindow(RealPoliticMod.World));
			ModWindow.Show(WindowId, _window);
		}
		catch (Exception ex)
		{
			Disable("окно КМУД не открылось: " + ex.Message);
		}
	}

	// ---------------------------------------------------------------- заголовок окна

	private static string TitleForWindow(PoliticalWorld world)
	{
		string title = GameText.Localized("RP36_KMUD_WIN_TITLE", "КМУД");
		KmudState state = world?.Kmud;
		if (state != null && state.Round > 0)
		{
			title = title + " — " + GameText.Localized("RP36_KMUD_ROUND", "съезд") + " №" + state.Round;
		}
		return title;
	}

	// ---------------------------------------------------------------- отрисовка

	private static void Render(PoliticalWorld world)
	{
		KmudState state = world?.Kmud;
		if (state == null)
		{
			return;
		}
		ModUi.ClearChildren(GameRefs.ReadMember(_content, "gameObject") ?? _content);

		Header(state);
		ModUi.Spacer(_content, 3f);

		if (state.Phase == KmudPhase.NotHeld)
		{
			NotHeld(world);
			return;
		}

		Agenda(world, state);
		ModUi.Spacer(_content, 3f);

		if (state.Phase == KmudPhase.Convened)
		{
			Turnout(world, state);
			ModUi.Spacer(_content, 2f);
		}

		Questions(state);

		ModUi.Spacer(_content, 3f);
		Seal(world, state);
	}

	/// <summary>Шапка: золотая полоса с названием конференции и её стадией.</summary>
	private static void Header(KmudState state)
	{
		string name = GameText.Localized("RP36_KMUD_NAME",
			"КОНФЕРЕНЦИЯ МИРНОГО УРЕГУЛИРОВАНИЯ ДЕЛ");
		string stage;
		switch (state.Phase)
		{
			case KmudPhase.Convened:
				stage = GameText.Localized("RP36_KMUD_STAGE_LIVE", "съезд идёт · жители голосуют");
				break;
			case KmudPhase.Closed:
				stage = GameText.Localized("RP36_KMUD_STAGE_DONE", "съезд завершён · решения в силе");
				break;
			default:
				stage = GameText.Localized("RP36_KMUD_STAGE_NONE", "конференция не созывалась");
				break;
		}
		if (state.Round > 0)
		{
			stage = GameText.Localized("RP36_KMUD_ROUND", "съезд") + " №" + state.Round + " · " + stage;
		}
		float height = BlockHeight(9f, 8f, name, 10f, stage, 8.5f);
		object canvas = ModUi.Canvas(_content, height);
		if (canvas == null)
		{
			return;
		}
		Sheet sheet = new Sheet(canvas, height);
		ModUi.Tile(canvas, W, height, 0.13f, 0.11f, 0.06f, 0.95f, null);
		PlaceLast(canvas, 0f, 0f, W, height);
		// Верхняя и нижняя золотые нити — «рамка грамоты».
		ModUi.Line(canvas, 0f, height / 2f - 3f, W, 1f, GoldR, GoldG, GoldB, 0.85f);
		ModUi.Line(canvas, 0f, -height / 2f + 3f, W, 1f, GoldR, GoldG, GoldB, 0.85f);

		sheet.Row(name, 10f, GoldR, GoldG, GoldB, true);
		sheet.Row(stage, 8.5f, BronzeR, BronzeG, BronzeB, false);
	}

	private static void NotHeld(PoliticalWorld world)
	{
		int year = world.Params?.KmudYear ?? KmudRules.DefaultYear;
		int interval = world.Params?.KmudIntervalYears ?? 100;
		string head = GameText.Localized("RP36_KMUD_NOT_HELD", "Конференция ещё не созывалась.");
		string hint = GameText.Localized("RP36_KMUD_NOT_HELD_HINT",
			"Первый съезд собирается на {year}-м году мира, дальше — каждые {years} лет. В год съезда все войны заканчиваются белым миром, а жители решают четыре общих вопроса.")
			.Replace("{year}", year.ToString()).Replace("{years}", interval.ToString());
		float height = BlockHeight(8f, 8f, head, 10f, hint, 8.5f) + 3f;
		object canvas = ModUi.Canvas(_content, height);
		if (canvas == null)
		{
			return;
		}
		Sheet sheet = new Sheet(canvas, height);
		ModUi.Tile(canvas, W, height, 0.10f, 0.10f, 0.09f, 0.85f, null);
		PlaceLast(canvas, 0f, 0f, W, height);
		ModUi.Line(canvas, -W / 2f + 2f, 0f, 3f, height, MossR, MossG, MossB, 1f);
		sheet.Row(head, 10f, PaperR, PaperG, PaperB, true);
		sheet.Gap(2f);
		sheet.Row(hint, 8.5f, BronzeR, BronzeG, BronzeB, false);
	}

	/// <summary>Повестка дня: три карточки — конфликт, обвиняемая держава, отстающая.</summary>
	private static void Agenda(PoliticalWorld world, KmudState state)
	{
		KmudAgenda agenda = state.Agenda;
		if (agenda == null)
		{
			return;
		}
		SectionTitle(GameText.Localized("RP36_KMUD_AGENDA_TITLE", "ПОВЕСТКА ДНЯ"));

		string conflict = agenda.HasConflict
			? world.KingdomNameOf(agenda.AggressorId) + "  ⚔  " + world.KingdomNameOf(agenda.DefenderId)
			: GameText.Localized("RP36_KMUD_NO_CONFLICT", "войн нет — мирить некого");
		string conflictNote = agenda.HasConflict
			? GameText.Localized("RP36_KMUD_CONFLICT_NOTE", "самый долгий конфликт · длится лет: {years}")
				.Replace("{years}", agenda.ConflictYears.ToString())
			: string.Empty;
		Card(GameText.Localized("RP36_KMUD_CARD_CONFLICT", "Долгий конфликт"), conflict, conflictNote, BloodR, BloodG, BloodB);

		string worst = agenda.WorstId > 0
			? world.KingdomNameOf(agenda.WorstId)
			: GameText.Localized("RP36_KMUD_NO_ACCUSED", "никого не обвиняют");
		string worstNote = agenda.WorstReasons != null && agenda.WorstReasons.Count > 0
			? string.Join(" · ", agenda.WorstReasons.ToArray())
			: GameText.Localized("RP36_KMUD_NO_ACCUSED_NOTE", "в этом мире пока некому предъявить счёт");
		Card(GameText.Localized("RP36_KMUD_CARD_WORST", "Обвиняемая держава"), worst, worstNote, AmberR, AmberG, AmberB);

		string poorest = agenda.PoorestId > 0
			? world.KingdomNameOf(agenda.PoorestId)
			: GameText.Localized("RP36_KMUD_NO_POOREST", "все державы ровны");
		string poorestNote = string.IsNullOrEmpty(agenda.PoorestReason)
			? GameText.Localized("RP36_KMUD_POOREST_NOTE", "ей нужнее всех помощь")
			: agenda.PoorestReason;
		Card(GameText.Localized("RP36_KMUD_CARD_POOREST", "Самая отстающая"), poorest, poorestNote, MossR, MossG, MossB);
	}

	private static void SectionTitle(string text)
	{
		float height = BlockHeight(3f, 3f, text, 9.5f);
		object canvas = ModUi.Canvas(_content, height);
		if (canvas == null)
		{
			return;
		}
		Sheet sheet = new Sheet(canvas, height);
		sheet.Row(text, 9.5f, GoldR, GoldG, GoldB, true);
	}

	/// <summary>Карточка повестки: подложка, цветной корешок слева, название, значение, примечание.</summary>
	private static void Card(string title, string value, string note, float r, float g, float b)
	{
		float height = BlockHeight(6f, 7f, title, 8.5f, value, 11f, note, 8f);
		object canvas = ModUi.Canvas(_content, height);
		if (canvas == null)
		{
			return;
		}
		Sheet sheet = new Sheet(canvas, height);
		ModUi.Tile(canvas, W, height, 0.085f, 0.08f, 0.07f, 0.9f, null);
		PlaceLast(canvas, 0f, 0f, W, height);
		ModUi.Line(canvas, -W / 2f + 2f, 0f, 3f, height, r, g, b, 1f);
		// Тонкая золотая нить под верхним краем карточки.
		ModUi.Line(canvas, 1.5f, height / 2f - 4f, W - 5f, 1f, GoldR, GoldG, GoldB, 0.30f);

		sheet.Gap(3f);
		sheet.Row(title, 8.5f, r + 0.22f, g + 0.22f, b + 0.22f, true);
		sheet.Row(value, 11f, PaperR, PaperG, PaperB, true);
		sheet.Row(note, 8f, BronzeR, BronzeG, BronzeB, false);
	}

	/// <summary>Сколько жителей уже проголосовало — общая полоса участия.</summary>
	private static void Turnout(PoliticalWorld world, KmudState state)
	{
		int voted = state.Voting != null && state.Voting.Voted != null ? state.Voting.Voted.Count : 0;
		int population = 0;
		try
		{
			foreach (KingdomPolitics kingdom in world.Kingdoms)
			{
				population += kingdom.Population;
			}
		}
		catch (Exception)
		{
		}
		float share = population > 0 ? Math.Min(1f, (float)voted / population) : 0f;

		string line = GameText.Localized("RP36_KMUD_TURNOUT", "Голосуют жители: {voted} из {total}")
			.Replace("{voted}", voted.ToString())
			.Replace("{total}", population > 0 ? population.ToString() : "?");
		float height = BlockHeight(5f, 5f, line, 9.5f) + 12f;
		object canvas = ModUi.Canvas(_content, height);
		if (canvas == null)
		{
			return;
		}
		Sheet sheet = new Sheet(canvas, height);
		ModUi.Tile(canvas, W, height, 0.09f, 0.09f, 0.08f, 0.9f, null);
		PlaceLast(canvas, 0f, 0f, W, height);
		sheet.Gap(1f);
		sheet.Row(line, 9.5f, PaperR, PaperG, PaperB, true);
		Bar(canvas, sheet.Top(6f), share, 0.30f, 0.62f, 0.86f,
			GameText.Localized("RP36_KMUD_TURNOUT_NOTE", "каждый год голосуют новые жители"));
	}

	private static void Questions(KmudState state)
	{
		foreach (KmudQuestion question in Enum.GetValues(typeof(KmudQuestion)))
		{
			KmudBallotResult ballot = state.Voting != null ? state.Voting.Of(question) : null;
			if (ballot == null)
			{
				continue;
			}
			ModUi.Spacer(_content, 2f);
			QuestionBlock(state, question, ballot);
		}
	}

	/// <summary>Один вопрос: заголовок, строка состояния и по полосе на каждый вариант.</summary>
	private static void QuestionBlock(KmudState state, KmudQuestion question, KmudBallotResult ballot)
	{
		int[] options = KmudOptions.For(question);
		if (options == null || options.Length == 0)
		{
			return;
		}
		string title = GameText.Localized(QuestionKey(question), QuestionFallback(question)).ToUpperInvariant();
		string subtitle = QuestionSubtitle(state, question, ballot);
		// Высота: шапка вопроса (2 строки) + на каждый вариант его подпись (с переносом) и полоса.
		float optionsHeight = 0f;
		for (int i = 0; i < options.Length; i++)
		{
			string optionLabel = GameText.Localized(KmudOptions.LocKey(question, options[i]), options[i].ToString());
			optionsHeight += RowHeight(optionLabel + "  ·  100%  (000)", 9.5f) + 9f;
		}
		float height = BlockHeight(7f, 5f, title, 9.5f, subtitle, 8f) + optionsHeight;
		object canvas = ModUi.Canvas(_content, height);
		if (canvas == null)
		{
			return;
		}
		Sheet sheet = new Sheet(canvas, height);
		ModUi.Tile(canvas, W, height, 0.075f, 0.07f, 0.065f, 0.9f, null);
		PlaceLast(canvas, 0f, 0f, W, height);
		ModUi.Line(canvas, -W / 2f + 2f, 0f, 2f, height, GoldR, GoldG, GoldB, 0.8f);

		sheet.Gap(3f);
		sheet.Row(title, 9.5f, GoldR, GoldG, GoldB, true);
		sheet.Row(subtitle, 8f, BronzeR, BronzeG, BronzeB, false);
		sheet.Gap(2f);

		int max = 1;
		for (int i = 0; i < options.Length; i++)
		{
			max = Math.Max(max, ballot.CountOf(options[i]));
		}
		for (int i = 0; i < options.Length; i++)
		{
			OptionRow(canvas, sheet, ballot, question, options[i], max);
		}
	}

	private static void OptionRow(object canvas, Sheet sheet, KmudBallotResult ballot, KmudQuestion question, int option, int max)
	{
		int count = ballot.CountOf(option);
		float share = ballot.Voters > 0 ? (float)count / ballot.Voters : 0f;
		bool leading = (!ballot.Tied && ballot.Winner == option) || (ballot.Tied && count == max && count > 0);
		string label = GameText.Localized(KmudOptions.LocKey(question, option), option.ToString());
		string percent = (int)Math.Round(share * 100f) + "%";
		// Лидирующий вариант — золотом и крупнее, остальные — обычным пергаментом.
		sheet.Row(label + "  ·  " + percent + "  (" + count + ")", leading ? 9.5f : 8.5f,
			leading ? GoldR : PaperR, leading ? GoldG : PaperG, leading ? GoldB : PaperB, leading);
		Bar(canvas, sheet.Top(7f), share, leading ? 0.88f : 0.42f, leading ? 0.70f : 0.46f, leading ? 0.26f : 0.34f, null);
		sheet.Gap(2f);
	}

	private static string QuestionSubtitle(KmudState state, KmudQuestion question, KmudBallotResult ballot)
	{
		if (state.ResultsAnnounced)
		{
			string decided = GameText.Localized("RP36_KMUD_DECIDED", "решено: {answer}")
				.Replace("{answer}", GameText.Localized(KmudOptions.LocKey(question, ballot.Winner), ballot.Winner.ToString()));
			return ballot.Tied
				? decided + " · " + GameText.Localized("RP36_KMUD_TIE", "голоса разделились поровну — принят мягкий вариант")
				: decided;
		}
		return GameText.Localized("RP36_KMUD_VOTES", "уже проголосовали: {n}").Replace("{n}", ballot.Voters.ToString());
	}

	/// <summary>Полоса голосов: тёмный жёлоб и цветная заливка на долю голосов.</summary>
	private static void Bar(object canvas, float y, float share, float r, float g, float b, string caption)
	{
		share = Math.Max(0f, Math.Min(1f, share));
		const float full = W - 8f;
		ModUi.Line(canvas, 2f, y, full, 5f, 0.05f, 0.05f, 0.04f, 0.9f);
		float width = Math.Max(1.5f, full * share);
		ModUi.Line(canvas, 2f - full / 2f + width / 2f, y, width, 5f, r, g, b, 1f);
		if (caption != null)
		{
			ModUi.Line(canvas, 2f, y - 5f, full, 1f, GoldR, GoldG, GoldB, 0.25f);
			TextInCanvas(canvas, caption, -W / 2f + 6f, y - 10f, W - 12f, 8f, BronzeR, BronzeG, BronzeB, false, "MiddleLeft");
		}
	}

	/// <summary>Печать в подвале: сроки, пакты, санкции, итоговая явка.</summary>
	private static void Seal(PoliticalWorld world, KmudState state)
	{
		string sealTitle = GameText.Localized("RP36_KMUD_SEAL_TITLE", "ПЕЧАТЬ КОНФЕРЕНЦИИ");
		string footer = GameText.Localized("RP36_KMUD_FOOTER",
			"Открыта в {open} году, закрывается в {close}. Войны запрещены до {truce} года.")
			.Replace("{open}", state.OpenedYear.ToString())
			.Replace("{close}", state.ClosesYear.ToString())
			.Replace("{truce}", state.TruceUntilYear.ToString());
		string pacts = state.Pacts != null && state.Pacts.Count > 0
			? GameText.Localized("RP36_KMUD_PACTS", "Пактов «только оборона»: {n}")
				.Replace("{n}", state.Pacts.Count.ToString())
			: null;
		string sanctions = state.Sanctions != null && state.Sanctions.Count > 0
			? GameText.Localized("RP36_KMUD_SANCTIONS", "Действующих санкций: {n}")
				.Replace("{n}", state.Sanctions.Count.ToString())
			: null;
		string members = state.Members > 0
			? GameText.Localized("RP36_KMUD_MEMBERS", "Держав за столом: {n}")
				.Replace("{n}", state.Members.ToString())
			: null;
		// 0.39: видно, что запрет войн работает ДО объявления, а не гасит уже начатые.
		string prevented = state.WarsPrevented > 0
			? GameText.Localized("RP36_KMUD_PREVENTED", "Войн не дали объявить: {n}")
				.Replace("{n}", state.WarsPrevented.ToString())
			: null;

		float height = BlockHeight(9f, 9f, sealTitle, 9.5f, footer, 8f, pacts, 8f, sanctions, 8f, members, 8f, prevented, 8f);
		object canvas = ModUi.Canvas(_content, height);
		if (canvas == null)
		{
			return;
		}
		Sheet sheet = new Sheet(canvas, height);
		ModUi.Tile(canvas, W, height, 0.12f, 0.10f, 0.06f, 0.95f, null);
		PlaceLast(canvas, 0f, 0f, W, height);
		ModUi.Line(canvas, 0f, height / 2f - 3f, W, 1f, GoldR, GoldG, GoldB, 0.8f);
		ModUi.Line(canvas, 0f, -height / 2f + 3f, W, 1f, GoldR, GoldG, GoldB, 0.8f);

		sheet.Gap(3f);
		sheet.Row(sealTitle, 9.5f, GoldR, GoldG, GoldB, true);
		sheet.Row(footer, 8f, PaperR, PaperG, PaperB, false);
		sheet.Row(pacts, 8f, 0.56f, 0.80f, 0.62f, false);
		sheet.Row(sanctions, 8f, 0.86f, 0.48f, 0.38f, false);
		sheet.Row(members, 8f, BronzeR, BronzeG, BronzeB, false);
		sheet.Row(prevented, 8f, 0.56f, 0.72f, 0.86f, false);
	}

	// ---------------------------------------------------------------- мелкие помощники

	private static string QuestionKey(KmudQuestion question)
	{
		switch (question)
		{
			case KmudQuestion.ConflictOutcome: return "RP32_Q_CONFLICT";
			case KmudQuestion.PeaceYears: return "RP32_Q_PEACE";
			case KmudQuestion.Punish: return "RP32_Q_PUNISH";
			case KmudQuestion.Aid: return "RP32_Q_AID";
			case KmudQuestion.Repatriation: return "RP47_Q_REPAT";
			default: return "RP32_Q_UNKNOWN";
		}
	}

	private static string QuestionFallback(KmudQuestion question)
	{
		switch (question)
		{
			case KmudQuestion.ConflictOutcome: return "Итог долгого конфликта";
			case KmudQuestion.PeaceYears: return "Время полного мира";
			case KmudQuestion.Punish: return "Наказание безнравственной державы";
			case KmudQuestion.Aid: return "Помощь отстающей державе";
			case KmudQuestion.Repatriation: return "Безопасность возвращения народа";
			default: return "Вопрос";
		}
	}

	/// <summary>Последний созданный в холсте объект ставим по центру с нужным размером.</summary>
	private static void PlaceLast(object canvas, float x, float y, float width, float height)
	{
		try
		{
			object transform = GameRefs.ReadMember(canvas, "transform");
			int count = Convert.ToInt32(GameRefs.ReadMember(transform, "childCount") ?? 0);
			if (count <= 0)
			{
				return;
			}
			object child = GameRefs.Invoke(transform, "GetChild", count - 1);
			object go = GameRefs.ReadMember(child, "gameObject");
			if (go != null)
			{
				ModUi.Place(go, x, y, width, height);
			}
		}
		catch (Exception)
		{
		}
	}

	/// <summary>Текст внутри холста: левый край leftX, вертикальный центр — centerY.</summary>
	private static void TextInCanvas(object canvas, string text, float leftX, float centerY, float width, float size,
		float r, float g, float b, bool bold, string align)
	{
		object go = ModUi.Text(canvas, text, size, r, g, b, bold, align, width);
		if (go != null)
		{
			// 0.48.3: высота — по числу строк после переноса, а не «одна строка на всё».
			// Если текст занял две строки, центр блока смещаем вниз, чтобы первая строка
			// осталась там, куда её поставил вызывающий.
			float lineHeight = size + 2f;
			float height = ModUi.TextHeight(text, size, width);
			float center = centerY - (height - lineHeight) / 2f;
			ModUi.Place(go, leftX + width / 2f, center, width, height);
		}
	}

	private static void Disable(string reason)
	{
		_disabled = true;
		LogOnce(reason);
	}

	private static void LogOnce(string message)
	{
		try
		{
			LogService.LogInfo("[Real Politic] " + message);
		}
		catch (Exception)
		{
		}
	}

	/// <summary>
	/// Сколько высоты займёт строка: если текст длиннее строки, он переносится по словам,
	/// и под него нужно резервировать несколько строк, иначе подпись налезет на полосу голосов.
	/// </summary>
	private static float RowHeight(string text, float size)
	{
		if (string.IsNullOrEmpty(text))
		{
			return 0f;
		}
		// 0.48.3: честная мерка (ModUi.LineCount) вместо оценки «по числу букв».
		// Старая оценка занижала высоту — строки налезали друг на друга (снимки 540–543).
		return ModUi.TextHeight(text, size, ModUi.PageWidth - 12f);
	}

	/// <summary>Высота холста по его строкам: (текст, размер шрифта) парами.</summary>
	private static float BlockHeight(float topPad, float bottomPad, params object[] rows)
	{
		float total = topPad + bottomPad;
		for (int i = 0; i + 1 < rows.Length; i += 2)
		{
			total += RowHeight(rows[i] as string, Convert.ToSingle(rows[i + 1]));
		}
		return Math.Max(12f, total);
	}

	/// <summary>Стопка строк сверху вниз внутри холста; высота строки считается с учётом переноса.</summary>
	private sealed class Sheet
	{
		private readonly object _canvas;

		private readonly float _height;

		private float _top;

		public Sheet(object canvas, float height)
		{
			_canvas = canvas;
			_height = height;
			_top = height / 2f;
		}

		/// <summary>Отступить сверху.</summary>
		public void Gap(float height)
		{
			_top -= height;
		}

		/// <summary>Y-координата для элемента высотой height сразу под уже написанным.</summary>
		public float Top(float height)
		{
			return _top - height / 2f;
		}

		/// <summary>Написать строку слева и сдвинуть курсор вниз на её полную высоту.</summary>
		public void Row(string text, float size, float r, float g, float b, bool bold)
		{
			if (string.IsNullOrEmpty(text))
			{
				return;
			}
			float height = RowHeight(text, size);
			float width = ModUi.PageWidth - 12f;
			float left = -ModUi.PageWidth / 2f + 6f;
			// Первая строка текста стоит у самого верха отведённого места.
			float lineHeight = size + 2f;
			float centerY = _top - lineHeight / 2f;
			if (_top - height < -_height / 2f - 0.5f)
			{
				return;
			}
			TextInCanvas(_canvas, text, left, centerY, width, size, r, g, b, bold, "MiddleLeft");
			_top -= height;
		}
	}
}
