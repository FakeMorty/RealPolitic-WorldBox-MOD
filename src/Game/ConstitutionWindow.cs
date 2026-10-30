using System;
using System.Collections.Generic;
using System.Reflection;
using System.Text;
using NeoModLoader.services;
using RealPolitic.Core;

namespace RealPolitic;

/// <summary>
/// Этап 26: окно «Конституция» — грамота державы, как настоящая свитка.
/// Заглавие с вензелями, статьи римскими числами, разделители, раздел поправок,
/// действующие военные мандаты, клеймо узурпации и подпись правящего дома.
/// Собирается строго рефлексией; при сбое — LogOnce, мод не падает, строка
/// «Конституция» в профиле державы остаётся.
/// </summary>
public static class ConstitutionWindow
{
	private const string WindowId = "realpolitic_constitution";

	private static object _window;

	private static object _content;

	private static int _kingdomId;

	private static string _kingdomName = string.Empty;

	private static bool _disabled;

	private static object _openButton;

	// ---------------------------------------------------------------- публичный API

	/// <summary>Заранее создаёт пустое окно конституции на старте (пока ни одно окно не открыто),
	/// чтобы первый клик не вызывал WindowCreator.CreateEmptyWindow → hide("instant") поверх открытого окна державы.</summary>
	public static bool Prewarm()
	{
		if (_disabled)
		{
			return false;
		}
		if (_window != null && IsAlive(_window))
		{
			return true;
		}
		_window = ModWindow.Create(WindowId, "RP_CONST_WIN_TITLE", GameText.Localized("RP_CONST_WIN_TITLE", "Конституция"), ModIcons.ConstitutionKey, out object content);
		if (_window == null)
		{
			return false;
		}
		_content = content ?? _window;
		ModUi.SetupAutoLayout(_content, spacing: 1f, padTop: 6f);
		ModWindow.SetTitle(_window, TitleForWindow());
		return true;
	}

	/// <summary>Открыть/перестроить грамоту данной державы.</summary>
	public static void Toggle(int kingdomId, string kingdomName)
	{
		try
		{
			if (kingdomId <= 0 || _disabled)
			{
				return;
			}
			if (RealPoliticMod.World == null)
			{
				return;
			}
			KingdomPolitics state = RealPoliticMod.World.GetKingdom(kingdomId);
			if (state == null)
			{
				LogOnce("конституция: держава №" + kingdomId + " в моде не найдена — открывать нечего");
				return;
			}
			// 0.48.3: «конституция не открывается». Если грамоты ещё нет (держава новая,
			// старый сейв, только что сменился строй) — выписываем её сейчас, а не молчим.
			if (state.Constitution == null)
			{
				state.Constitution = RealPoliticMod.World.EnsureConstitution(kingdomId);
			}
			if (state.Constitution == null)
			{
				LogOnce("конституция: у державы №" + kingdomId + " её нет и включить нечего"
					+ " (EnableConstitution=" + (RealPoliticMod.World.Params?.EnableConstitution == true) + ")");
			}
			_kingdomId = kingdomId;
			_kingdomName = kingdomName ?? string.Empty;
			if (!EnsureWindow())
			{
				return;
			}
			// 0.48.3: даже если сборка грамоты упала на конкретной державе, окно всё равно
			// открывается — с короткой честной запиской. Молчание кнопки — худший вариант.
			try
			{
				Rebuild(state);
			}
			catch (Exception buildEx)
			{
				ShowBuildFailure(buildEx);
			}
			ShowWindow();
			LogService.LogInfo("[Real Politic] конституция открыта: держава №" + _kingdomId);
		}
		catch (Exception ex)
		{
			LogOnce("ошибка окна конституции: " + ex.Message);
		}
	}

	/// <summary>Клик из окна державы: открыть конституцию текущего показанного государства.</summary>
	/// <summary>0.45: запомнить державу без создания отдельной вкладки (вкладка мода теперь одна).</summary>
	public static void AttachOpenButtonSafe(int kingdomId, string kingdomName)
	{
		_kingdomId = kingdomId;
		_kingdomName = kingdomName ?? string.Empty;
	}

	/// <summary>0.45: открыть окно конституции для запомненной державы (из оглавления мода).</summary>
	public static void Open()
	{
		// 0.48.3: в журнал — что именно нажали и для какой державы.
		// Владелец 29.10.2026: «конституция не открывается» — по логу сразу видно, где застряло.
		LogService.LogInfo("[Real Politic] нажата кнопка «Конституция», держава №" + _kingdomId + " «" + _kingdomName + "»");
		ToggleForCurrent();
	}

	public static void ToggleForCurrent()
	{
		if (_kingdomId > 0)
		{
			Toggle(_kingdomId, _kingdomName);
		}
	}

	// ---------------------------------------------------------------- кнопка в профиле державы

	/// <summary>Прицепить кнопку «📜 Конституция» строкой сразу под строкой-конституцией
	/// в окне королевства (вызывается из Patches при построении строк).</summary>
	public static void AttachOpenButton(object kingdomWindow, int kingdomId, string kingdomName)
	{
		try
		{
			if (_disabled || RealPoliticMod.World?.Params.EnableConstitution != true)
			{
				return;
			}
			_kingdomId = kingdomId;
			_kingdomName = kingdomName ?? string.Empty;
			// 0.23.2: предпочитаем кнопку-вкладку на боковой панели окна (как штатные вкладки игры).
			object tab = SideTabButton.Ensure(kingdomWindow, "rp_constitution_tab", ModIcons.ConstitutionKey, ModIcons.PathFor(ModIcons.ConstitutionKey),
				GameText.Localized("RP_CONST_OPEN_BUTTON", "📜 Конституция"),
				typeof(ConstitutionWindow).GetMethod("OnOpenClick", BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public));
			if (tab != null)
			{
				_openButton = tab;
				return;
			}
			if (_openButton != null && IsAlive(_openButton))
			{
				return;
			}
			object container = Patches.TryGetStatsContainer(kingdomWindow);
			if (container == null)
			{
				return;
			}
			object windowGo = GameRefs.ReadMember(kingdomWindow, "gameObject");
			object source = FirstButton(windowGo);
			if (source == null)
			{
				return;
			}
			object clone = CloneUnder(source, container);
			if (clone == null)
			{
				return;
			}
			SetName(clone, "rp_constitution_button");
			DestroyComponent(clone, "PowerButton");
			DestroyComponent(clone, "TipButton");
			DestroyComponent(clone, "TooltipButton");
			DestroyComponent(clone, "HoverTip");
			SetRect(clone, 0f, 0f, 210f, 26f);
			SetButtonText(clone, GameText.Localized("RP_CONST_OPEN_BUTTON", "📜 Конституция"));
			ModUi.AddIcon(clone, ModIcons.ConstitutionKey, -91f, 0f, 20f);
			HookOpenClick(clone);
			PlaceAfterRow(clone, container, "[KV] RP_CONST_TITLE");
			_openButton = clone;
		}
		catch (Exception ex)
		{
			LogOnce("не удалось прицепить кнопку конституции: " + ex.Message);
		}
	}

	// ---------------------------------------------------------------- окно

	private static bool EnsureWindow()
	{
		try
		{
			if (_window != null && IsAlive(_window))
			{
				ModWindow.SetTitle(_window, TitleForWindow());
				return true;
			}
			// 0.23.5: окно создаётся штатным для NML способом (WindowCreator.CreateEmptyWindow),
			// а не через несуществующий ScrollWindow.createWindow.
			_window = ModWindow.Create(WindowId, "RP_CONST_WIN_TITLE", GameText.Localized("RP_CONST_WIN_TITLE", "Конституция"), ModIcons.ConstitutionKey, out object content);
			if (_window == null)
			{
				Disable("не удалось создать окно (см. лог выше)");
				return false;
			}
			_content = content ?? _window;
			// 0.23.6: автораскладка столбиком (как в окнах NML) — текст больше не уезжает за край.
			ModUi.SetupAutoLayout(_content, spacing: 1f, padTop: 6f);
			ModWindow.SetTitle(_window, TitleForWindow());
			return true;
		}
		catch (Exception ex)
		{
			Disable("не удалось создать окно конституции: " + ex.Message);
			return false;
		}
	}

	private static string TitleForWindow()
	{
		string key = GameText.Localized("RP_CONST_WIN_TITLE", "Конституция");
		return string.IsNullOrEmpty(_kingdomName) ? key : key + " — " + _kingdomName;
	}

	private static void ShowWindow()
	{
		ModWindow.Show(WindowId, _window);
	}

	// ---------------------------------------------------------------- грамота

	private sealed class DocLine
	{
		public string Text;

		public float Size;

		public float R;

		public float G;

		public float B;

		public bool Bold;

		public float Gap; // отступ ПЕРЕД строкой

		public DocLine(string text, float size, float r, float g, float b, bool bold, float gap)
		{
			Text = text;
			Size = size;
			R = r;
			G = g;
			B = b;
			Bold = bold;
			Gap = gap;
		}
	}

	/// <summary>Сборка документа: преамбула, 6 статей, поправки, мандаты, печать.
	/// Строки просто добавляются по порядку — VerticalLayoutGroup на Content ставит их столбиком.</summary>
	/// <summary>Грамота не собралась — показываем это человеку, а не только в лог.</summary>
	private static void ShowBuildFailure(Exception buildEx)
	{
		try
		{
			LogService.LogError("[Real Politic] конституция: грамота не собралась — " + buildEx);
			object contentTransform = GameRefs.ReadMember(_content, "transform") ?? _content;
			ModUi.ClearChildren(contentTransform);
			ModUi.Text(contentTransform, GameText.Localized("RP_CONST_WIN_TITLE", "Конституция"), 13f, 0.95f, 0.90f, 0.70f, bold: true);
			ModUi.Spacer(contentTransform, 4f);
			ModUi.Text(contentTransform, "Грамоту этой державы собрать не удалось — подробности записаны в файл журнала (Player.log).", 10f, 0.85f, 0.75f, 0.62f);
			ModUi.Spacer(contentTransform, 3f);
			ModUi.Text(contentTransform, "Держава: " + _kingdomName + " (№" + _kingdomId + ")", 9f, 0.75f, 0.72f, 0.64f);
			ModUi.Text(contentTransform, "Причина: " + buildEx.Message, 9f, 0.70f, 0.66f, 0.60f);
		}
		catch (Exception)
		{
		}
	}

	/// <summary>Римское число для номера статьи (1 → I, 17 → XVII). Считается, а не перечисляется.</summary>
	private static string Roman(int number)
	{
		if (number <= 0)
		{
			return "0";
		}
		int[] values = { 1000, 900, 500, 400, 100, 90, 50, 40, 10, 9, 5, 4, 1 };
		string[] symbols = { "M", "CM", "D", "CD", "C", "XC", "L", "XL", "X", "IX", "V", "IV", "I" };
		System.Text.StringBuilder text = new System.Text.StringBuilder();
		int rest = number;
		for (int i = 0; i < values.Length && rest > 0; i++)
		{
			while (rest >= values[i])
			{
				text.Append(symbols[i]);
				rest -= values[i];
			}
		}
		return text.ToString();
	}

	private static void Rebuild(KingdomPolitics state)
	{
		object contentTransform = GameRefs.ReadMember(_content, "transform") ?? _content;
		ModUi.ClearChildren(contentTransform);
		Constitution c = state.Constitution;
		List<DocLine> lines = Compose(state, c);
		for (int i = 0; i < lines.Count; i++)
		{
			DocLine line = lines[i];
			if (line.Gap > 1f)
			{
				ModUi.Spacer(contentTransform, line.Gap);
			}
			ModUi.Text(contentTransform, line.Text, line.Size, line.R, line.G, line.B, line.Bold);
		}
	}

	private static List<DocLine> Compose(KingdomPolitics state, Constitution c)
	{
		var lines = new List<DocLine>();
		IdeologyType ideology = state.Ideology;
		int year = RealPoliticMod.CurrentYear();

		// ---- преамбула: свиток с вензелями
		Title(lines, GameText.Localized("RP_CONST_DOC_TITLE", "✦ К О Н С Т И Т У Ц И Я ✦"));
		// 0.46: сначала — самое главное простыми словами, потом полный текст.
		// Владелец 29.10.2026: «сделай конституцию подробнее и язык не доисторический,
		// а общий и понятный».
		Note(lines, GameText.Localized("RP_CONST_SUMMARY_TITLE", "Коротко: как устроена держава"));
		Body(lines, GameText.Localized("RP_CONST_SUMMARY_RULE",
			"• Правит: {form}.\n• Голос имеют: {franchise}.\n• Войну объявляет: {war}.\n• Границы: {borders}.")
			.Replace("{form}", ConstitutionL10n.FormName(c.StateForm, ideology))
			.Replace("{franchise}", ConstitutionL10n.FranchiseName(c.Electoral?.Electors ?? c.Franchise))
			.Replace("{war}", ConstitutionL10n.WarAuthorityText(c.WarPower))
			.Replace("{borders}", ConstitutionL10n.BorderText(c.Borders)));
		Divider(lines);
		Note(lines, GameText.Localized("RP_CONST_DOC_SUBTITLE", "Основной закон державы")
			+ " «" + _kingdomName + "»");
		Note(lines, GameText.Localized("RP_CONST_DOC_GRANTED", "Высочайше утверждена в {year} году")
			.Replace("{year}", state.FoundedYear.ToString()));
		Divider(lines);

		// ---- статьи римскими числами
		// 0.48.4: ЭТО И БЫЛА ПРИЧИНА «конституция не открывается».
		// В 0.46 добавили две статьи (о переселении и о народах), а список римских чисел
		// остался на 15 — и на 16-й статье окно падало с
		// «Index was outside the bounds of the array» (видно в Player.log от 29.10.2026).
		// Теперь числа не перечислены руками, а считаются — список статей можно расти.
		string[] articleTitles =
		{
			GameText.Localized("RP_CONST_ART_FORM", "О верховной власти"),
			GameText.Localized("RP_CONST_ART_FRANCHISE", "О правах подданных"),
			GameText.Localized("RP_CONST_ART_WAR", "О войне и мире"),
			GameText.Localized("RP_CONST_ART_BORDERS", "О границах державы"),
			GameText.Localized("RP_CONST_ART_SUCCESSION", "О престолонаследии"),
			GameText.Localized("RP_CONST_ART_SUPREMACY", "О силе сей грамоты"),
			GameText.Localized("RP_CONST_ART_TAX", "О податях"),
			GameText.Localized("RP_CONST_ART_TRADE", "О торге"),
			GameText.Localized("RP_CONST_ART_FAITH", "О вере"),
			GameText.Localized("RP_CONST_ART_LEVY", "О ратной повинности"),
			GameText.Localized("RP_CONST_ART_LAND", "О землях"),
			GameText.Localized("RP_CONST_ART_WATCH", "О дозоре"),
			GameText.Localized("RP_CONST_ART_BLOOD", "О кровных связях"),
			GameText.Localized("RP_CONST_ART_IMPEACH", "Об импичменте"),
			GameText.Localized("RP_CONST_ART_UNION", "Союзы с другими державами"),
			// 0.46: две новые статьи — про переселение и про народы державы.
			GameText.Localized("RP_CONST_ART_MIGRATION", "Переселение жителей"),
			GameText.Localized("RP_CONST_ART_RACES", "Народы державы"),
			// 0.52: четыре новые статьи — права народов, язык, суд и местные уставы.
			GameText.Localized("RP_CONST_ART_RACE_LAW", "О правах народов"),
			GameText.Localized("RP_CONST_ART_LANGUAGE", "О языке"),
			GameText.Localized("RP_CONST_ART_COURT", "О суде народов"),
			GameText.Localized("RP_CONST_ART_LOCAL", "О местных уставах"),
			// 0.53: палаты.
			GameText.Localized("RP_CONST_ART_CHAMBERS", "О палатах"),
			GameText.Localized("RP_CONST_ART_UPPERBASIS", "О совете земель"),
			GameText.Localized("RP_CONST_ART_VETO", "О вето правителя"),
			GameText.Localized("RP_CONST_ART_DISSOLVE", "О роспуске палат")
		};
		string[] articleBodies =
		{
			ConstitutionL10n.FormName(c.StateForm, ideology) + ". " + ConstitutionL10n.SupremacyText(c.Supremacy) + ".",
			ConstitutionL10n.FranchiseName(c.Electoral?.Electors??c.Franchise) + ".",
			ConstitutionL10n.WarAuthorityText(c.WarPower) + ". " + ActiveMandateLine(state, year),
			ConstitutionL10n.BorderText(c.Borders) + ".",
			ConstitutionL10n.SuccessionText(c.Succession) + ".",
			ConstitutionL10n.SupremacyText(c.Supremacy) + ".",
			ConstitutionL10n.TaxText(c.Taxes) + ".",
			ConstitutionL10n.TradeLawText(c.Trade) + ".",
			ConstitutionL10n.FaithText(c.Faith) + ".",
			ConstitutionL10n.LevyText(c.Levy) + ".",
			ConstitutionL10n.LandText(c.Land) + ".",
			ConstitutionL10n.WatchText(c.Watch) + ".",
			ConstitutionL10n.BloodText(c.Blood) + ".",
			ConstitutionL10n.ImpeachmentText(c.Impeachment) + ".",
			ConstitutionL10n.UnionLawText(c.Union) + ".",
			MigrationLawText(c),
			RacesLawText(state),
			RaceRightsLawText(c),
			LanguageLawText(c),
			CourtLawText(c),
			LocalCharterLawText(c),
			ChambersLawText(c),
			UpperBasisLawText(c),
			(c.RulerVeto
				? GameText.Localized("RP53_CONST_VETO_YES", "Правитель может наложить вето на принятый палатами закон; преодолеть его можно двумя третями голосов.")
				: GameText.Localized("RP53_CONST_VETO_NO", "Вето у правителя нет: что приняли палаты, то и закон.")),
			(c.RulerMayDissolve
				? GameText.Localized("RP53_CONST_DISSOLVE_YES", "Правитель может распустить палаты.")
				: GameText.Localized("RP53_CONST_DISSOLVE_NO", "Распустить палаты правитель не может."))
		};
		for (int a = 0; a < articleBodies.Length; a++)
		{
			string title = a < articleTitles.Length ? articleTitles[a] : string.Empty;
			Head(lines, GameText.Localized("RP_CONST_ART_PREFIX", "Статья {num}. {name}")
				.Replace("{num}", Roman(a + 1)).Replace("{name}", title));
			Body(lines, articleBodies[a]);
			if (a == 2 || a == 4 || a == 7 || a == 10 || a == 13)
			{
				Divider(lines);
			}
		}

        if (c.Electoral != null)
        {
            Divider(lines);
            Head(lines, GameText.Localized("RP26_CHAPTER", "Избирательные права и референдумы"));
            foreach (string line in ElectoralChapterL10n.Lines(c.Electoral)) Body(lines,line);
        }

		// ---- поправки
		Head(lines, GameText.Localized("RP_CONST_AMENDMENTS", "Поправки"));
		if (state.ConstitutionHistory.Count == 0)
		{
			Note(lines, GameText.Localized("RP_CONST_NO_AMENDMENTS", "От утверждения не менялась."));
		}
		else
		{
			int start = Math.Max(0, state.ConstitutionHistory.Count - 6);
			for (int i = start; i < state.ConstitutionHistory.Count; i++)
			{
				ConstitutionEdit edit = state.ConstitutionHistory[i];
				Body(lines, GameText.Localized("RP_CONST_AMEND_LINE", "{year}: {article} — «{from}» → «{to}» ({who})")
					.Replace("{year}", edit.Year.ToString())
					.Replace("{article}", Loc(edit.Article))
					.Replace("{from}", Loc(edit.From))
					.Replace("{to}", Loc(edit.To))
					.Replace("{who}", Loc(edit.EditorName)));
			}
		}

		// ---- клеймо узурпации
		if (state.LastConstitutionalUsurpYear > -100)
		{
			Warning(lines, GameText.Localized("RP_CONST_USURP_MARK", "⚠ В {year} году престол занят в обход конституции — в летописи записано клеймо узурпатора.")
				.Replace("{year}", state.LastConstitutionalUsurpYear.ToString()));
		}
		Divider(lines);

		// ---- подпись и печать
		string rulerName = state.Ruler?.Name ?? string.Empty;
		if (!string.IsNullOrEmpty(rulerName))
		{
			string titleKey = Ideologies.RulerTitleKey(ideology, state.Ruler.IsFemale);
			string title = GameText.Localized(titleKey, titleKey);
			Note(lines, GameText.Localized("RP_CONST_SIGN_RULER", "Подписано: {title} {name}")
				.Replace("{title}", title).Replace("{name}", rulerName));
		}
		if (!string.IsNullOrEmpty(state.RoyalClanName))
		{
			Note(lines, GameText.Localized("RP_CONST_SIGN_HOUSE", "Печать дома {clan}")
				.Replace("{clan}", state.RoyalClanName));
		}
		else
		{
			Note(lines, GameText.Localized("RP_CONST_SIGN_BODY", "Заверено: {body}")
				.Replace("{body}", ConstitutionL10n.WarBodyName(c.WarPower)));
		}
		return lines;
	}

	private static string ActiveMandateLine(KingdomPolitics state, int year)
	{
		for (int i = state.WarAuthorizations.Count - 1; i >= 0; i--)
		{
			WarAuthorization auth = state.WarAuthorizations[i];
			if (auth.ValidUntilYear >= year)
			{
				string target = RealPoliticMod.World?.KingdomNameOf(auth.TargetKingdomId) ?? ("#" + auth.TargetKingdomId);
				return GameText.Localized("RP_CONST_MANDATE_LINE", "Собрание дозволило войну против {target} до {until} года ({vf} за / {va} против).")
					.Replace("{target}", target)
					.Replace("{until}", auth.ValidUntilYear.ToString())
					.Replace("{vf}", auth.VotesFor.ToString())
					.Replace("{va}", auth.VotesAgainst.ToString());
			}
		}
		return string.Empty;
	}

	/// <summary>0.46: статья XVI — что говорит конституция о переезде жителей.</summary>
	private static string MigrationLawText(Constitution c)
	{
		switch (c.Borders)
		{
			case BorderLaw.Open:
				return GameText.Localized("RP_CONST_MIGRATION_OPEN",
					"Уехать может каждый: заявление рассматривают и родная держава, и та, куда человек хочет переехать. "
					+ "Если обе согласны — человек просто переезжает. Если родная держава против, а новая согласна — человек "
					+ "может сбежать; перейдя границу, он становится гражданином новой державы.");
			case BorderLaw.Closed:
				return GameText.Localized("RP_CONST_MIGRATION_CLOSED",
					"Уехать нельзя: граница закрыта. Попытка побега карается, но отчаявшиеся всё равно пробуют.");
			default:
				return GameText.Localized("RP_CONST_MIGRATION_CONTROLLED",
					"Уехать можно с разрешения родной державы. Довольных отпускают, недовольных — нет. "
					+ "Если родная держава отказала, а принимающая согласна, человек может сбежать через границу.");
		}
	}

	/// <summary>0.46: статья XVII — как держава относится к своим народам.</summary>
	/// <summary>0.52: статья о правах народов — простыми словами, без архаики.</summary>
	private static string RaceRightsLawText(Constitution c)
	{
		switch (c.Race)
		{
			case RaceLaw.Titular:
				return GameText.Localized("RP52_CONST_RACE_TITULAR",
					"Коренной народ державы имеет больше прав, чем пришлые: пришлые живут, работают и платят подати, но на высшие должности их не зовут");
			case RaceLaw.Segregated:
				return GameText.Localized("RP52_CONST_RACE_SEG",
					"Пришлым разрешено только жить и работать. Свой язык и свой суд у них отняты, и потому их дети постепенно забывают язык отцов");
			default:
				return GameText.Localized("RP52_CONST_RACE_EQUAL",
					"Все народы державы равны перед законом: и коренные, и пришлые судятся одним судом и платят одинаковые подати");
		}
	}

	/// <summary>0.52: статья о языке.</summary>
	private static string LanguageLawText(Constitution c)
	{
		return c.Language == LanguageLaw.Free
			? GameText.Localized("RP52_CONST_LANG_FREE",
				"Каждый народ учит детей своему языку. В делах державы все говорят на общем, а дома и в своей общине — на своём. Так народ живёт двумя культурами и не исчезает")
			: GameText.Localized("RP52_CONST_LANG_COMMON",
				"В державе один общий язык: в школах, в суде и в делах — только он. Свой язык семей не запрещён, но и не поддерживается, и через два-три поколения его забывают");
	}

	/// <summary>0.52: статья о суде народов.</summary>
	private static string CourtLawText(Constitution c)
	{
		return c.Court == CourtLaw.Own
			? GameText.Localized("RP52_CONST_COURT_OWN",
				"Спор внутри общины разбирают свои, по своим обычаям; державный суд берётся только за тяжкие дела")
			: GameText.Localized("RP52_CONST_COURT_COMMON",
				"Один суд для всех: и коренной, и пришлый судятся по общему закону, обычаи общин суд не признаёт");
	}

	/// <summary>0.52: статья о местных уставах — то, что владелец просил 30.10.2026.</summary>
	/// <summary>0.53: сколько палат и как они называются.</summary>
	private static string ChambersLawText(Constitution c)
	{
		return c.Chambers == ChamberCount.Two
			? GameText.Localized("RP53_CONST_CHAMBERS_TWO",
				"Две палаты: народное собрание (сто мест) и совет земель. Закон проходит обе.")
			: GameText.Localized("RP53_CONST_CHAMBERS_ONE",
				"Одна палата: народное собрание (сто мест). Совет земель не собирается.");
	}

	/// <summary>0.53: от чего считается совет земель.</summary>
	private static string UpperBasisLawText(Constitution c)
	{
		switch (c.UpperBasis)
		{
			case UpperChamberBasis.Peoples:
				return GameText.Localized("RP53_CONST_UB_PEOPLES",
					"В совете земель голос у каждого народа державы — малому народу защита.");
			case UpperChamberBasis.Clans:
				return GameText.Localized("RP53_CONST_UB_CLANS",
					"В совете земель голос у знатных родов: чем древнее род, тем больше весу.");
			default:
				return GameText.Localized("RP53_CONST_UB_CITIES",
					"В совете земель голос у каждого города — один город, один голос.");
		}
	}

	private static string LocalCharterLawText(Constitution c)
	{
		if (!c.AllowLocalConstitutions)
		{
			return GameText.Localized("RP52_CONST_LOCAL_BANNED",
				"Городам запрещено писать свои уставы: закон державы один для всех. Если город с чужим народом всё равно считает иначе, это его дело — но устава у него не будет");
		}
		if (!c.TolerateLocalContradictions)
		{
			return GameText.Localized("RP52_CONST_LOCAL_STRICT",
				"Город, где больше половины жителей — пришлый народ, может написать свой устав. Но если он расходится с этим законом, столица его не терпит и требует привести к общему");
		}
		return GameText.Localized("RP52_CONST_LOCAL_FREE",
			"Город, где больше половины жителей — пришлый народ, может написать свой устав, и столица терпит расхождения с общим законом: живут же рядом разные обычаи");
	}

	private static string RacesLawText(KingdomPolitics state)
	{
		switch (RaceSurvival.PolicyOf(state))
		{
			case RacePolicy.Protect:
				return GameText.Localized("RP_CONST_RACES_PROTECT",
					"Все народы державы равны. Малые народы под защитой закона: их не вытесняют, "
					+ "им помогают сохранять свой язык и обычаи, а желающим уехать — не мешают.");
			case RacePolicy.Oppress:
				return GameText.Localized("RP_CONST_RACES_OPRESS",
					"Один народ объявлен хозяином державы. Остальные должны раствориться в нём: "
					+ "их язык и обычаи не защищаются, их переселяют и притесняют. Поэтому они бегут "
					+ "в державы, где к ним относятся иначе.");
			default:
				return GameText.Localized("RP_CONST_RACES_IGNORE",
					"Закон не различает народы: как сложится, так и сложится. Сильный народ "
					+ "вытесняет слабого, и никто этому не мешает.");
		}
	}

	// ---- стили документа

	private static void Title(List<DocLine> l, string text) { l.Add(new DocLine(text, 15f, 1f, 0.85f, 0.45f, true, 2f)); }
	private static void Head(List<DocLine> l, string text) { l.Add(new DocLine(text, 12f, 1f, 0.9f, 0.6f, true, 9f)); }
	private static void Body(List<DocLine> l, string text)
	{
		// Перенос по словам делает сам Unity Text (ширина страницы задана в ModUi).
		l.Add(new DocLine(text, 11f, 0.93f, 0.93f, 0.93f, false, 1f));
	}
	private static void Note(List<DocLine> l, string text) { l.Add(new DocLine(text, 11f, 0.75f, 0.72f, 0.66f, false, 1f)); }
	private static void Warning(List<DocLine> l, string text)
	{
		l.Add(new DocLine(text, 11f, 1f, 0.5f, 0.45f, false, 1f));
	}
	private static void Divider(List<DocLine> l) { l.Add(new DocLine("· ❖ ·", 11f, 0.7f, 0.65f, 0.5f, false, 6f)); }

	// ---------------------------------------------------------------- Unity-примитивы (рефлексия, как в FamilyTreeWindow)

	private static void SetRect(object go, float x, float y, float width, float height)
	{
		object rect = GameRefs.ReadMember(go, "rectTransform") ?? GetComponent(go, "UnityEngine.RectTransform, UnityEngine.CoreModule");
		if (rect == null)
		{
			return;
		}
		SetProperty(rect, "anchorMin", NewVector2(0.5f, 0.5f));
		SetProperty(rect, "anchorMax", NewVector2(0.5f, 0.5f));
		SetProperty(rect, "pivot", NewVector2(0.5f, 0.5f));
		SetProperty(rect, "anchoredPosition", NewVector2(x, y));
		SetProperty(rect, "sizeDelta", NewVector2(width, height));
		SetProperty(go, "activeSelf", true);
	}

	private static object NewVector2(float x, float y)
	{
		Type t = Type.GetType("UnityEngine.Vector2, UnityEngine.CoreModule");
		return t == null ? null : Activator.CreateInstance(t, new object[2] { x, y });
	}

	private static void SetProperty(object target, string name, object value)
	{
		if (target == null || value == null)
		{
			return;
		}
		PropertyInfo prop = target.GetType().GetProperty(name, BindingFlags.Instance | BindingFlags.Public);
		if (prop == null || !prop.CanWrite)
		{
			return;
		}
		try
		{
			if (value.GetType() != prop.PropertyType && !prop.PropertyType.IsInstanceOfType(value))
			{
				value = Convert.ChangeType(value, prop.PropertyType);
			}
			prop.SetValue(target, value, null);
		}
		catch (Exception)
		{
		}
	}

	private static object EnumValue(string typeName, string name)
	{
		Type t = Type.GetType(typeName);
		return t == null ? null : Enum.Parse(t, name);
	}

	private static void SetColor(object textComp, float r, float g, float b, float a)
	{
		Type t = Type.GetType("UnityEngine.Color, UnityEngine.CoreModule");
		if (t == null)
		{
			return;
		}
		object color = Activator.CreateInstance(t, new object[4] { r, g, b, a });
		SetProperty(textComp, "color", color);
	}

	private static object GetComponent(object go, string typeName)
	{
		try
		{
			Type t = Type.GetType(typeName);
			if (t == null || go == null)
			{
				return null;
			}
			return GameRefs.Invoke(go, "GetComponent", new object[1] { t });
		}
		catch (Exception)
		{
			return null;
		}
	}

	private static void DestroyComponent(object go, string componentName)
	{
		try
		{
			object comp = GetComponent(go, componentName + ", Assembly-CSharp")
				?? GetComponent(go, componentName + ", UnityEngine.UI")
				?? GetComponent(go, componentName + ", UnityEngine.CoreModule");
			if (comp == null)
			{
				return;
			}
			Type unityObject = Type.GetType("UnityEngine.Object, UnityEngine.CoreModule");
			MethodInfo destroy = unityObject?.GetMethod("Destroy", BindingFlags.Static | BindingFlags.Public, null, new Type[] { unityObject }, null);
			destroy?.Invoke(null, new object[] { comp });
		}
		catch (Exception)
		{
		}
	}

	private static object FirstButton(object windowGo)
	{
		try
		{
			Type buttonType = Type.GetType("UnityEngine.UI.Button, UnityEngine.UI");
			if (buttonType == null || windowGo == null)
			{
				return null;
			}
			object arr = GameRefs.Invoke(windowGo, "GetComponentsInChildren", new object[1] { buttonType });
			if (arr is Array components && components.Length > 0)
			{
				return components.GetValue(0);
			}
		}
		catch (Exception)
		{
		}
		return null;
	}

	private static object CloneUnder(object original, object parent)
	{
		try
		{
			Type unityObject = Type.GetType("UnityEngine.Object, UnityEngine.CoreModule");
			if (unityObject == null)
			{
				return null;
			}
			MethodInfo[] methods = unityObject.GetMethods(BindingFlags.Static | BindingFlags.Public);
			for (int i = 0; i < methods.Length; i++)
			{
				if (methods[i].Name != "Instantiate" || methods[i].IsGenericMethod)
				{
					continue;
				}
				ParameterInfo[] ps = methods[i].GetParameters();
				if (ps.Length == 2 && ps[1].ParameterType.Name == "Transform")
				{
					object parentTransform = GameRefs.ReadMember(parent, "transform") ?? parent;
					return methods[i].Invoke(null, new object[2] { original, parentTransform });
				}
			}
		}
		catch (Exception ex)
		{
			LogOnce("Instantiate конституционной кнопки провалился: " + ex.Message);
		}
		return null;
	}

	private static void SetName(object go, string name)
	{
		try
		{
			SetProperty(go, "name", name);
		}
		catch (Exception)
		{
		}
	}

	private static void SetButtonText(object buttonGo, string text)
	{
		try
		{
			Type textType = Type.GetType("UnityEngine.UI.Text, UnityEngine.UI");
			if (textType == null)
			{
				return;
			}
			if (GameRefs.Invoke(buttonGo, "GetComponentInChildren", new object[1] { textType }) is object comp && comp != null)
			{
				SetProperty(comp, "text", text);
			}
		}
		catch (Exception)
		{
		}
	}

	private static void HookOpenClick(object buttonGo)
	{
		try
		{
			Type buttonType = Type.GetType("UnityEngine.UI.Button, UnityEngine.UI");
			object button = GetComponent(buttonGo, "UnityEngine.UI.Button, UnityEngine.UI");
			if (button == null || buttonType == null)
			{
				return;
			}
			object onClick = GameRefs.ReadMember(button, "onClick");
			if (onClick == null)
			{
				return;
			}
			Type actionType = typeof(Action);
			Delegate click = Delegate.CreateDelegate(actionType, typeof(ConstitutionWindow).GetMethod("OnOpenClick", BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public));
			Type unityEventType = onClick.GetType();
			MethodInfo addListener = unityEventType.GetMethod("AddListener", BindingFlags.Instance | BindingFlags.Public, null, new Type[] { typeof(object) }, null);
			Type unityAction = Type.GetType("UnityEngine.Events.UnityAction, UnityEngine.CoreModule");
			if (unityAction != null)
			{
				Delegate unityClick = Delegate.CreateDelegate(unityAction, typeof(ConstitutionWindow).GetMethod("OnOpenClick", BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public));
				MethodInfo addUnityListener = unityEventType.GetMethod("AddListener", BindingFlags.Instance | BindingFlags.Public, null, new Type[] { unityAction }, null);
				addUnityListener?.Invoke(onClick, new object[] { unityClick });
				return;
			}
			addListener?.Invoke(onClick, new object[] { click });
		}
		catch (Exception ex)
		{
			LogOnce("клик по кнопке конституции не подвязан: " + ex.Message);
		}
	}

	private static void OnOpenClick()
	{
		LogService.LogInfo("[Real Politic] клик: конституция (королевство #" + _kingdomId + ")");
		ToggleForCurrent();
	}

	/// <summary>Кнопка становится строкой сразу под строкой с названием name заданной строки.</summary>
	private static void PlaceAfterRow(object clone, object container, string rowName)
	{
		try
		{
			object rowsObj = GameRefs.ReadMember(container, "stats_rows");
			if (rowsObj is not System.Collections.IList rows)
			{
				return;
			}
			for (int i = 0; i < rows.Count; i++)
			{
				object rowGo = GameRefs.ReadMember(rows[i], "gameObject");
				if (rowGo == null || !string.Equals(GameRefs.ReadMember(rowGo, "name")?.ToString(), rowName, StringComparison.Ordinal))
				{
					continue;
				}
				object cloneTransform = GameRefs.ReadMember(clone, "transform");
				object rowTransform = GameRefs.ReadMember(rowGo, "transform");
				if (GameRefs.Invoke(rowTransform, "GetSiblingIndex") is int sib)
				{
					GameRefs.Invoke(cloneTransform, "SetSiblingIndex", new object[] { sib + 1 });
				}
				return;
			}
		}
		catch (Exception)
		{
		}
	}

	private static bool IsAlive(object unityObj)
	{
		try
		{
			if (unityObj == null)
			{
				return false;
			}
			// Unity-объекты «умирают» не по null: сравниваем с null их оператором.
			Type unityObject = Type.GetType("UnityEngine.Object, UnityEngine.CoreModule");
			MethodInfo op = unityObject?.GetMethod("op_Inequality", BindingFlags.Static | BindingFlags.Public);
			if (op != null)
			{
				return (bool)op.Invoke(null, new object[] { unityObj, null });
			}
			return true;
		}
		catch (Exception)
		{
			return false;
		}
	}

	private static MethodInfo FindStatic(Type type, string name, int paramCount)
	{
		if (type == null)
		{
			return null;
		}
		MethodInfo[] methods = type.GetMethods(BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
		for (int i = 0; i < methods.Length; i++)
		{
			if (methods[i].Name == name && methods[i].GetParameters().Length == paramCount)
			{
				return methods[i];
			}
		}
		return null;
	}

	private static readonly HashSet<string> _logged = new HashSet<string>();

	private static void LogOnce(string message)
	{
		if (_logged.Add(message))
		{
			LogService.LogInfo("[Real Politic] конституция: " + message);
		}
	}

	private static void Disable(string reason)
	{
		if (_disabled)
		{
			return;
		}
		_disabled = true;
		LogService.LogInfo("[Real Politic] окно конституции отключено: " + reason);
	}

	/// <summary>Значения поправок ядро хранит ключами локализации (RP_…, RP26_…)
	/// либо живым текстом. RP26_* не начинается с RP_, поэтому отдельно проверяем
	/// встроенные гражданские ключи и их русский запасной текст.</summary>
	private static string Loc(string value)
	{
		if (string.IsNullOrEmpty(value))
		{
			return string.Empty;
		}
		bool isLocalizationKey = value.StartsWith("RP_", StringComparison.Ordinal)
			|| CivicText.IsLocalizationKey(value);
		return isLocalizationKey ? GameText.Localized(value, CivicText.Fallback(value)) : value;
	}
}
