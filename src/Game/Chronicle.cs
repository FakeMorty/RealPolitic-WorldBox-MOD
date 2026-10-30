using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using NeoModLoader.services;
using RealPolitic.Core;
using UnityEngine;

namespace RealPolitic;

/// <summary>
/// Летопись мира: пишет в текстовый файл максимально подробную хронику политики.
/// 1) Каждое событие — отдельной строкой со всеми известными деталями
///    (имена, пол, возраст, титулы по идеологии, государство, причина смерти,
///    убийца, длительность правления и т. д.).
/// 2) Раз в SummaryEveryYears лет — сводка по всем государствам (правитель,
///    легитимность, курс, война/революция, города, религия/язык/культура).
/// 3) Накапливается статистика за мир (смерти по причинам, смены престола,
///    революции, новшества) — печатается в сводке и при смене мира.
/// Пишется порциями (буфер), чтобы не открывать файл на каждое событие.
/// </summary>
public class Chronicle
{
	/// <summary>0.31.12: склонение числа лет по-русски: «1 год», «2 года», «5 лет».</summary>
	internal static string RuYearsPhrase(string raw)
	{
		if (!int.TryParse(raw, out int n))
		{
			return raw;
		}
		int mod100 = Math.Abs(n) % 100;
		int mod10 = Math.Abs(n) % 10;
		string word = (mod100 >= 11 && mod100 <= 14) ? "лет"
			: mod10 == 1 ? "год"
			: (mod10 >= 2 && mod10 <= 4) ? "года"
			: "лет";
		return n + " " + word;
	}

    public static bool ReadableEdition = true;
    public static bool GroupExtinctions = true;
    public static bool GroupBorders = true;
    /// <summary>0.31.9: писать ли в хронику пограничные эпизоды. По умолчанию нет.</summary>
    public static bool WriteBorderEpisodes = false;
    /// <summary>0.31.10: писать ли годовые сводки по государствам и годовую статистику. По умолчанию нет.</summary>
    public static bool WriteYearSummaries = false;
    private readonly ChronicleBorderDigest _borderDigest = new ChronicleBorderDigest();
    private readonly ChronicleNotebook _notebook = new ChronicleNotebook();
    private readonly ChronicleExtinctionDigest _extinctions = new ChronicleExtinctionDigest();
    private MapBox _editionMap;

	private readonly PoliticalWorld _world;

	private readonly GameNotifier _notifier;

	private readonly List<string> _buffer = new List<string>();

	private string _path;
	private string _baseFolder;
	private string _boundWorldKey;

	private readonly UTF8Encoding _encoding = new UTF8Encoding(encoderShouldEmitUTF8Identifier: true);

	private int _lastYear = -1;

	private int _lastSummaryYear = -1;

	/// <summary>0.23.3: первый год летописи в этом мире (для строки «наиграно с загрузки»).</summary>
	private int _firstYear = -1;

	/// <summary>0.23.3: с какого года идёт текущая сводка (если год не пропущен — равен году сводки).</summary>
	private int _summaryFromYear = -1;

	private float _nextFlush;

	private bool _failed;

	private bool _prologueWritten;

	// «Фотография» накопленной статистики на момент прошлой сводки — для строчки «За этот год».
	private int _yDeathsRulers;

	private int _yDeathsChosen;

	private int _ySuccessions;

	private int _yRevolutions;

	private int _yAdoptions;

	private int _yDiplomacy;

	private int _yUnions;

	private int _yNoveltyReligion;

	private int _yNoveltyLanguage;

	private int _yNoveltyCulture;

	private int _yWarsStarted;

	private int _yWarsEnded;

	private int _yPowers;

	private int _yThreats;

	private int _yCivilWars;

	private int _ySecessions;

	private int _yEconomiesCrises;

	private int _yEconomiesBooms;

	private int _yTradeRoutes;

	// Накапливаемая статистика мира (сбрасывается при новом мире).
	private int _deathsRulers;

	private int _deathsChosen;

	private readonly Dictionary<string, int> _deathsByCause = new Dictionary<string, int>();

	private readonly Dictionary<int, string> _lastDeferredSignatureByKingdom = new Dictionary<int, string>();

	private int _successions;

	private int _civilWars;

	private int _secessions;

	private int _economiesCrises;

	private int _economiesBooms;

	private int _marriages;

	private int _yMarriages;

	private int _treaties;

	private int _yTreaties;

	private int _treatiesBroken;

	private int _yTreatiesBroken;

	private int _espionage;

	private int _yEspionage;

	private int _coalitions;

	private int _yCoalitions;

	private int _cbWars;

	private int _yCbWars;

	/// <summary>Сколько конституционных актов (голосования, узурпации, нарушения) — счётчик года.</summary>
	private int _constitutionActs;
	private int _yConstitutionActs;

	private int _tradeRoutesOpened;

	// Этап 21: альянсы игры, наследование короны, институты, интеграция, полные унии.

	private int _alliancesFormed;

	private int _yAlliancesFormed;

	private int _alliancesBroken;

	private int _yAlliancesBroken;

	private int _unionCrowns;

	private int _yUnionCrowns;

	private int _institutions;

	private int _integrations;

	private int _yIntegrations;

	private int _unionReforms;

	private int _revolutions;

	private int _adoptions;

	private int _diplomacy;

	private int _unions;

	private int _noveltyReligion;

	private int _noveltyLanguage;

	private int _noveltyCulture;

	private int _warsStarted;

	private int _warsEnded;

	// Этап 22: мирные договоры и перемирия.

	private int _peaceSettlements;

	private int _yPeaceSettlements;

	private int _whitePeaces;

	private int _yWhitePeaces;

	private int _trucesBroken;

	private int _yTrucesBroken;

	private int _citiesCeded;

	private int _yCitiesCeded;

	// Этап 23: распады, рейды, границы.

	private int _kingdomCollapses;

	private int _yKingdomCollapses;

	private int _collapseFragments;

	private int _yCollapseFragments;

	private int _powerRaids;

	private int _yPowerRaids;

	private int _borderClosures;

	private int _yBorderClosures;

	/// <summary>Каждое сколько лет писать сводку по государствам (1 — каждый год).</summary>
	public int SummaryEveryYears { get; set; } = 1;

	public Chronicle(PoliticalWorld world, GameNotifier notifier, string folder)
	{
		_world = world;
		_notifier = notifier;
		_baseFolder = folder;
		_path = ResolvePath(folder);
		if (!string.IsNullOrEmpty(_path))
		{
			try
			{
				string directoryName = System.IO.Path.GetDirectoryName(_path);
				if (!string.IsNullOrEmpty(directoryName))
				{
					Directory.CreateDirectory(directoryName);
				}
				WriteFileHeader();
                _buffer.Add(L("RP_CHRON25_GUIDE", "ЛЕТОПИСЬ МИРА: события отражают политическую и дипломатическую историю государств; ↳ — перемены с прошлого обзора, ℹ — внутреннее состояние державы."));
			}
			catch (Exception ex)
			{
				_failed = true;
				LogService.LogError("[Real Politic] не удалось создать файл летописи: " + ex.Message);
			}
		}
		_world.Bus.Subscribe(OnEvent);
		_world.Deaths.OnDeath += OnDeath;
		if (!_failed && !string.IsNullOrEmpty(_path))
		{
			LogService.LogInfo("[Real Politic] летопись пишется в: " + _path);
		}
	}

	public string Path => _path;

	private static string ResolvePath(string folder)
	{
		try
		{
			if (!string.IsNullOrEmpty(folder))
			{
				return System.IO.Path.Combine(folder, "chronicle.txt");
			}
		}
		catch (Exception)
		{
		}
		try
		{
			// Запасной путь: документы пользователя, если папка мода недоступна.
			string docs = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
			if (!string.IsNullOrEmpty(docs))
			{
				return System.IO.Path.Combine(docs, "RealPolitic", "chronicle.txt");
			}
		}
		catch (Exception)
		{
		}
		return System.IO.Path.Combine(Directory.GetCurrentDirectory(), "realpolitic_chronicle.txt");
	}

	// ---------- События ----------

	private void OnEvent(PolEvent evt)
	{
		if (evt == null || _path == null || _failed)
		{
			return;
		}
		// Смерти правителей записываем через OnDeath (там полная причина и длительность правления).
		if (evt.Type == PolEventType.RulerDeath)
		{
			return;
		}
		if (_extinctions.Year >= 0 && !_extinctions.CanAdd(evt.Year)) DrainExtinctions();
        if (_borderDigest.Year >= 0 && !_borderDigest.CanAdd(evt.Year)) DrainBorders();
        // 0.31.9: пограничные эпизоды (выдворения) по умолчанию не пишутся в хронику:
        // это шум, который забивает файл. Они учитываются в годовой статистике.
        if (evt.Type == PolEventType.BorderExpulsion && !WriteBorderEpisodes)
        {
            CountEvent(evt);
            return;
        }
        if (ReadableEdition && GroupBorders && evt.Type == PolEventType.BorderExpulsion)
        {
            if (_borderDigest.Add(evt)) { CountEvent(evt); return; }
        }
        if (ReadableEdition && GroupExtinctions && evt.Type == PolEventType.SubspeciesExtinct)
        {
            if (_extinctions.Add(evt.Year, evt.Get("race"), evt.Get("species"))) { CountEvent(evt); return; }
        }
        if (evt.Type == PolEventType.CivicElectionDeferred && evt.KingdomId.HasValue)
        {
            string sig = (evt.Get("reason") ?? string.Empty) + "|" + (evt.Get("eligible") ?? string.Empty);
            if (_lastDeferredSignatureByKingdom.TryGetValue(evt.KingdomId.Value, out string prevSig) && prevSig == sig)
            {
                CountEvent(evt);
                return;
            }
            _lastDeferredSignatureByKingdom[evt.KingdomId.Value] = sig;
        }
        else if (evt.Type == PolEventType.DemocraticElection && evt.KingdomId.HasValue)
        {
            _lastDeferredSignatureByKingdom.Remove(evt.KingdomId.Value);
        }
        string text = DetailLine(evt);
        if (ReadableEdition)
        {
            text = ChronicleEdition.Explain(evt, text, _world);
            if (evt.Type == PolEventType.KingdomDisappeared)
            {
                string finalReview = _notebook.Farewell(evt.KingdomId ?? -1);
                if (!string.IsNullOrEmpty(finalReview)) text += " " + finalReview;
            }
        }
		if (!string.IsNullOrEmpty(text))
		{
			_buffer.Add(YearPrefix(evt.Year) + " [" + CategoryOf(evt) + "] " + text);
		}
		CountEvent(evt);
	}

	private void OnDeath(DeathRecord rec)
	{
		if (rec == null || _path == null || _failed)
		{
			return;
		}
		if (!rec.WasRuler && !rec.WasChosen)
		{
			return;
		}
		string category = (rec.WasRuler ? L("RP_CHRON_DEATH_RULER", "СМЕРТЬ ПРАВИТЕЛЯ") : L("RP_CHRON_DEATH_CHOSEN", "СМЕРТЬ ИЗБРАННОГО"));
		_buffer.Add(YearPrefix(rec.Year) + " [" + category + "] " + DeathLine(rec));
		if (rec.WasRuler)
		{
			_deathsRulers++;
			string label = L(rec.LocKeyFor(rec.WasFemale), rec.Cause.ToString());
			_deathsByCause[label] = _deathsByCause.TryGetValue(label, out int c) ? c + 1 : 1;
		}
		else
		{
			_deathsChosen++;
		}
	}

	/// <summary>Сколько раз за год правители применяли особое действие строя.</summary>
	private int _powersUsed;

	/// <summary>Сколько раз за год срывались внутренние угрозы строя.</summary>
	private int _threatsBroke;

	private void CountEvent(PolEvent evt)
	{
		switch (evt.Type)
		{
			case PolEventType.Succession:
				_successions++;
				break;
			case PolEventType.RevolutionStarted:
			case PolEventType.RevolutionSucceeded:
			case PolEventType.RevolutionFailed:
				_revolutions++;
				break;
			case PolEventType.KingdomAdoptedIdeology:
				_adoptions++;
				break;
			case PolEventType.DiplomaticAction:
				_diplomacy++;
				break;
			case PolEventType.UnionFormed:
			case PolEventType.UnionMemberJoined:
			case PolEventType.UnionMemberLeft:
			case PolEventType.UnionDissolved:
			case PolEventType.DynasticUnionFormed:
			case PolEventType.UnionMerged:
				_unions++;
				break;
		case PolEventType.WarStarted:
			_warsStarted++;
			break;
		case PolEventType.WarEnded:
			_warsEnded++;
			break;
		case PolEventType.PeaceSettlement:
			_peaceSettlements++;
			if (int.TryParse(evt.Get("cities"), out int ceded) && ceded > 0)
			{
				_citiesCeded += ceded;
			}
			break;
		case PolEventType.WhitePeace:
			_whitePeaces++;
			break;
			case PolEventType.TruceBroken:
				_trucesBroken++;
				break;
			case PolEventType.KingdomCollapsed:
				_kingdomCollapses++;
				if (int.TryParse(evt.Get("count"), out int frags) && frags > 0)
				{
					_collapseFragments += frags;
				}
				break;
			case PolEventType.PowerRaid:
				_powerRaids++;
				break;
			case PolEventType.BorderClosed:
				_borderClosures++;
				break;
			case PolEventType.NewReligion:
				_noveltyReligion++;
				break;
			case PolEventType.NewLanguage:
				_noveltyLanguage++;
				break;
			case PolEventType.NewCulture:
				_noveltyCulture++;
				break;
			case PolEventType.IdeologyPowerUsed:
				_powersUsed++;
				break;
			case PolEventType.IdeologyThreatBroke:
				_threatsBroke++;
				break;
			case PolEventType.DynasticShift:
				_successions++;
				break;
			case PolEventType.CivilWarStarted:
				_civilWars++;
				break;
			case PolEventType.KingdomSeceded:
				_secessions++;
				break;
			case PolEventType.EconomicCrisisStarted:
				_economiesCrises++;
				break;
			case PolEventType.DynasticMarriage:
				_marriages++;
				break;
			case PolEventType.TreatySigned:
				_treaties++;
				break;
			case PolEventType.TreatyBroken:
				_treatiesBroken++;
				break;
			case PolEventType.EspionageExposed:
			case PolEventType.EspionageSuccess:
				_espionage++;
				break;
			case PolEventType.CoalitionFormed:
				_coalitions++;
				break;
			case PolEventType.CasusBelliDeclared:
				_cbWars++;
				break;
			case PolEventType.WarReferendum:
			case PolEventType.ConstitutionalUsurpation:
			case PolEventType.ConstitutionEdited:
			case PolEventType.UnconstitutionalAct:
			case PolEventType.Impeached:
				_constitutionActs++;
				break;
			case PolEventType.EconomicBoom:
				_economiesBooms++;
				break;
			case PolEventType.TradeRouteOpened:
				_tradeRoutesOpened++;
				break;
			// Этап 21: альянсы игры и глубина уний.
			case PolEventType.AllianceFormed:
				_alliancesFormed++;
				break;
			case PolEventType.AllianceBroken:
				_alliancesBroken++;
				break;
			case PolEventType.UnionCrownInherited:
			case PolEventType.UnionSuccessionCrisis:
				_unionCrowns++;
				break;
			case PolEventType.UnionInstitutionGained:
				_institutions++;
				break;
			case PolEventType.IntegrationCompleted:
				_integrations++;
				break;
			case PolEventType.UnionReformed:
				_unionReforms++;
				break;
		}
	}

	// ---------- Время, шапка, пролог ----------

	/// <summary>Префикс строки события: «Год N (чч:мм:сс). » — игровые годы + реальное время.</summary>
	private static string YearPrefix(int year)
	{
		return L("RP_YEAR_LABEL", "Год") + " " + year + " (" + NowTime() + "). ";
	}

	/// <summary>Реальное время в формате часы:минуты:секунды.</summary>
	private static string NowTime()
	{
		return DateTime.Now.ToString("HH:mm:ss");
	}

	/// <summary>Реальная дата в формате дд.мм.гггг.</summary>
	private static string NowDate()
	{
		return DateTime.Now.ToString("dd.MM.yyyy");
	}

	/// <summary>
	/// Шапка летописи. В шапке — дата (и время) создания/запуска файла,
	/// чтобы было понятно, в какой момент реальной жизни начата эта запись.
	/// </summary>
	private void WriteFileHeader()
	{
		_buffer.Add("");
		_buffer.Add(L("RP_CHRON_HEADER", "Летопись мира (создано {date} {time})").Replace("{date}", NowDate()).Replace("{time}", NowTime()));
		_buffer.Add("====================================================");
	}

	/// <summary>
	/// Разделитель «НОВЫЙ МИР» с датой и временем смены мира.
	/// </summary>
	private void WriteNewWorldDivider()
	{
		_buffer.Add("");
		_buffer.Add("====================================================");
		_buffer.Add("=== " + L("RP_CHRON_NEW_WORLD", "НОВЫЙ МИР") + " (" + NowDate() + " " + NowTime() + ") ===");
		_buffer.Add("====================================================");
	}

	/// <summary>
	/// Пролог: полное описание мира на старте. Перечисляет все государства
	/// с правителем, строением, населением, воинами, легитимностью, курсом,
	/// городами/столицей/религией/языком/культурой и идеологии мира с их
	/// описаниями. Даёт «завязку» для чтения летописи (в т. ч. ИИ).
	/// </summary>
	private void WritePrologue(int year)
	{
		if (_path == null || _failed)
		{
			return;
		}
		MapBox map = World.world;
		List<Kingdom> list = (map == null) ? null : GameRefs.KingdomList(map);
		if (list == null || list.Count == 0)
		{
			return;
		}
		try
		{
			_buffer.Add("");
			_buffer.Add("=== " + Fmt2("RP_CHRON_PROLOGUE", "ПРОЛОГ: мир в {year} году", "year", year.ToString()) + " (" + NowDate() + " " + NowTime() + ") ===");
			// Какие строи есть в этом мире — с описанием каждого.
			Dictionary<IdeologyType, int> ideologyCount = new Dictionary<IdeologyType, int>();
			for (int i = 0; i < list.Count; i++)
			{
				if (list[i] == null)
				{
					continue;
				}
				IdeologyType t = IdeologyOf((int)((NanoObject)list[i]).id);
				if (!ideologyCount.ContainsKey(t))
				{
					ideologyCount.Add(t, 0);
				}
				ideologyCount[t]++;
			}
			if (ideologyCount.Count > 0)
			{
				_buffer.Add(L("RP_CHRON_PROLOGUE_IDEOLOGIES", "Строи в этом мире:"));
				foreach (KeyValuePair<IdeologyType, int> item in ideologyCount)
				{
					string name = L(Ideologies.NameKey(item.Key), item.Key.ToString());
					string desc = L(Ideologies.DescriptionKey(item.Key), string.Empty);
					_buffer.Add("  • " + name + " (государств: " + item.Value + "): " + desc);
				}
			}
			// Описание каждого государства.
			int count = 0;
			int popTotal = 0;
            var liveIds = new System.Collections.Generic.HashSet<int>();
			for (int i = 0; i < list.Count; i++)
			{
				Kingdom kingdom = list[i];
				if (kingdom == null)
				{
					continue;
				}
				int id = (int)((NanoObject)kingdom).id;
				int population;
				int warriors;
				string line = KingdomLine(id, kingdom, out population, out warriors);
				if (!string.IsNullOrEmpty(line))
				{
					count++;
					popTotal += population;
					_buffer.Add(line);
                    liveIds.Add(id);
                    if (ReadableEdition)
                    {
                        KingdomPolitics state = _world.GetKingdom(id);
                        Actor ruler = ChronicleEdition.LivingRuler(kingdom);
                        var snapshot = new ChronicleSnapshot { KingdomId = id, Year = year,
                            Population = population, Cities = kingdom.cities?.Count ?? 0,
                            RulerId = ruler == null ? 0 : GameText.ActorId(ruler), RulerName = GameText.ActorName(ruler),
                            Legitimacy = (int)(state?.Legitimacy ?? 0), Ideology = state?.Ideology ?? IdeologyType.Tribalism,
                            Routes = _world.Trade.OpenRoutesOf(id), Starving = state?.Starving ?? false, AtWar = state?.InWar ?? false };
                        string delta = _notebook.Compare(snapshot);
                        if (!string.IsNullOrEmpty(delta)) _buffer.Add("  ↳ " + delta);
                        string context = _notebook.Conditions(snapshot, state, _world.Params);
                        if (!string.IsNullOrEmpty(context)) _buffer.Add("  ℹ " + context);
                    }
				}
			}
			_notebook.Retain(liveIds);
            _buffer.Add(Fmt2("RP_CHRON_TOTALS", "Государств: {kingdoms}. Общее население: {population}.", "kingdoms", count.ToString(), "population", popTotal.ToString()));
		}
		catch (Exception ex)
		{
			LogService.LogError("[Real Politic] летопись: ошибка пролога: " + ex.Message);
		}
	}

	// ---------- Детальная запись события ----------

	private static string CategoryOf(PolEvent evt)
	{
		return evt.Type switch
		{
			PolEventType.Succession => "СМЕНА ПРАВИТЕЛЯ",
			PolEventType.KingdomAdoptedIdeology => "СМЕНА СТРОЯ",
			PolEventType.KingdomReformed => "КУРС РЕФОРМ",
			PolEventType.RevolutionStarted => "РЕВОЛЮЦИЯ",
			PolEventType.RevolutionSucceeded => "РЕВОЛЮЦИЯ",
			PolEventType.RevolutionFailed => "РЕВОЛЮЦИЯ",
			PolEventType.NewReligion => "НОВАЯ РЕЛИГИЯ",
			PolEventType.NewLanguage => "НОВЫЙ ЯЗЫК",
			PolEventType.NewCulture => "НОВАЯ КУЛЬТУРА",
			PolEventType.CityIdeologyShift => "НАСТРОЕНИЯ",
			PolEventType.DiplomaticAction => "ДИПЛОМАТИЯ",
			PolEventType.UnionFormed => "УНИИ",
			PolEventType.UnionMemberJoined => "УНИИ",
			PolEventType.UnionMemberLeft => "УНИИ",
			PolEventType.UnionDissolved => "УНИИ",
			PolEventType.UnionMerged => (string.Equals(evt.Get("union_kind"), "confederation", StringComparison.Ordinal) || string.Equals(evt.LocKey, "RP_EV_CONFEDERATION_MERGED", StringComparison.Ordinal))
				? "КОНФЕДЕРАЦИИ"
				: ((string.Equals(evt.Get("union_kind"), "federation", StringComparison.Ordinal) || string.Equals(evt.LocKey, "RP_EV_FEDERATION_MERGED", StringComparison.Ordinal)) ? "ФЕДЕРАЦИИ" : "УНИИ"),
			PolEventType.Impeached => string.Equals(evt.LocKey, "RP_EV_ATAMAN_DISMISSED", StringComparison.Ordinal) ? "ВОЛЬНЫЙ КРУГ" : "КОНСТИТУЦИЯ",
            PolEventType.ConstitutionEdited => "КОНСТИТУЦИЯ",
			PolEventType.ConstitutionalUsurpation => "КОНСТИТУЦИЯ",
			PolEventType.UnconstitutionalAct => "КОНСТИТУЦИЯ",
			PolEventType.WarReferendum => "РЕФЕРЕНДУМ О ВОЙНЕ",
			PolEventType.LegitimacyCrisis => "КРИЗИС ВЛАСТИ",
			PolEventType.DynasticExtinction => "ДИНАСТИИ",
			PolEventType.WarStarted => "ВОЙНА",
			PolEventType.WarEnded => "ВОЙНА",
			PolEventType.ActorConverted => "ВЗГЛЯДЫ",
			PolEventType.IdeologyPowerUsed => "ОСОБОЕ ДЕЙСТВИЕ",
			PolEventType.IdeologyThreatBroke => "ВНУТРЕННИЙ КРИЗИС",
			PolEventType.IdeologyThreatRecovered => "ВНУТРЕННИЙ КРИЗИС",
			PolEventType.IdeologyPowerEnded => "ОСОБОЕ ДЕЙСТВИЕ",
			PolEventType.RegencyStarted => "РЕГЕНТСТВО",
			PolEventType.RegencyEnded => "РЕГЕНТСТВО",
			PolEventType.DemocraticElection => "ВЫБОРЫ",
			PolEventType.EthnonationalistTension => "ЭТНОПОЛИТИКА",
			PolEventType.DynasticMarriage => "ДИНАСТИИ",
			PolEventType.TreatySigned => "ДОГОВОРЫ",
			PolEventType.TreatyBroken => "ДОГОВОРЫ",
			PolEventType.TreatyExpired => "ДОГОВОРЫ",
			PolEventType.CasusBelliDeclared => "ВОЙНА",
			PolEventType.EspionageExposed => "ШПИОНАЖ",
			PolEventType.EspionageSuccess => "ШПИОНАЖ",
			PolEventType.CoalitionFormed => "КОАЛИЦИИ",
			PolEventType.CoalitionWar => "КОАЛИЦИИ",
			PolEventType.CoalitionDissolved => "КОАЛИЦИИ",
			PolEventType.VassalRebellion => "МЯТЕЖ И СЕПАРАЦИЯ",
			// Этап 21: альянсы игры и глубина уний.
			PolEventType.AllianceFormed => "АЛЬЯНСЫ",
			PolEventType.AllianceBroken => "АЛЬЯНСЫ",
			PolEventType.AllianceMemberJoined => "АЛЬЯНСЫ",
			PolEventType.AllianceMemberLeft => "АЛЬЯНСЫ",
			PolEventType.UnionCrownInherited => "УНИИ",
			PolEventType.UnionSuccessionCrisis => "УНИИ",
			PolEventType.UnionInstitutionGained => "УНИИ",
			PolEventType.IntegrationStarted => "ИНТЕГРАЦИЯ",
			PolEventType.IntegrationCompleted => "ИНТЕГРАЦИЯ",
			PolEventType.UnionReformed => "УНИИ",
			// Этап 22: война и мирные договоры.
			PolEventType.PeaceSettlement => "МИРНЫЕ ДОГОВОРЫ",
			PolEventType.WhitePeace => "МИРНЫЕ ДОГОВОРЫ",
			PolEventType.TruceBroken => "МИРНЫЕ ДОГОВОРЫ",
			PolEventType.TruceExpired => "МИРНЫЕ ДОГОВОРЫ",
			// Этап 23.
			PolEventType.KingdomCollapsed => "РАСПАД ДЕРЖАВЫ",
            PolEventType.KingdomDisappeared => "КОНЕЦ ДЕРЖАВЫ",
            PolEventType.LastResidentDied => "ПОСЛЕДНИЙ ЖИТЕЛЬ",
            PolEventType.ElectionDeferred => "ВЫБОРЫ: ОТСРОЧКА",
			PolEventType.RegencyCrowned => "КОРОНА",
			PolEventType.RegencyPassed => "КОРОНА",
			PolEventType.DynastyRenamed => "ДИНАСТИИ",
			PolEventType.PowerRaid => "ТАЙНАЯ ВОЙНА",
			PolEventType.BorderOpened => "ГРАНИЦЫ",
			PolEventType.BorderClosed => "ГРАНИЦЫ",
			PolEventType.BorderExpulsion => "ГРАНИЦЫ",
			PolEventType.DynasticUnionFormed => "ДИНАСТИЧЕСКАЯ УНИЯ",
			PolEventType.CitySeceded => "МЯТЕЖ И СЕПАРАЦИЯ",
			PolEventType.CivilWarStarted => "ГРАЖДАНСКАЯ ВОЙНА",
			PolEventType.CivilWarEnded => "ГРАЖДАНСКАЯ ВОЙНА",
			PolEventType.KingdomSeceded => "РАСКОЛ ГОСУДАРСТВА",
			PolEventType.TradeRouteOpened => "ТОРГОВЛЯ",
			PolEventType.TradeRouteUpgraded => "ТОРГОВЛЯ",
			PolEventType.TradeRouteBroken => "ТОРГОВЛЯ",
			PolEventType.TradeRoutesSevered => "ГРАНИЦЫ",
			PolEventType.TradeEmbargo => "ТОРГОВЛЯ",
			PolEventType.EconomicCrisisStarted => "ЭКОНОМИКА",
			PolEventType.EconomicCrisisEnded => "ЭКОНОМИКА",
			PolEventType.EconomicBoom => "ЭКОНОМИКА",
			PolEventType.DynasticShift => "СМЕНА ДИНАСТИИ",
			PolEventType.EraChanged => "СМЕНА ЭПОХИ",
			PolEventType.SubspeciesExtinct => "ВЫМИРАНИЕ",
			PolEventType.RoyalRelativeSeceded => "ДИНАСТИИ",
			PolEventType.KingdomRestored => "МИРНЫЕ ДОГОВОРЫ",
            PolEventType.GreatPowerCompositionChanged => "ВЕЛИКИЕ ДЕРЖАВЫ",
            PolEventType.NationalRecognitionPosition => "ГОЛОСОВАНИЕ",
            PolEventType.CivicElectionDeferred => "ВЫБОРЫ",
            PolEventType.RegionalCivicProcedure => "РЕФЕРЕНДУМ / ВЫХОД",
            PolEventType.CivicAllianceMotion => "ГОЛОСОВАНИЕ АЛЬЯНСА",
            PolEventType.NationalAlliancePosition => "РЕШЕНИЕ УЧАСТНИКА АЛЬЯНСА",
            PolEventType.InternationalRecognitionDecided => "ПРИЗНАНИЕ",
            PolEventType.StrategicCampaignStarted => "СТРАТЕГИЧЕСКИЙ ПЛАН",
            PolEventType.StrategicCampaignCompleted => "СТРАТЕГИЧЕСКИЙ ПЛАН",
            PolEventType.DiplomaticMemoryRecorded => "ДИПЛОМАТИЧЕСКАЯ ПАМЯТЬ",
            PolEventType.CivicPetitionSubmitted => "ОБЩЕСТВЕННАЯ ПЕТИЦИЯ",
            PolEventType.CivicUltimatumResolved => "ОБЩЕСТВЕННЫЙ УЛЬТИМАТУМ",
            PolEventType.ParliamentaryShift => "ПАРЛАМЕНТ И ПАРТИИ",
            PolEventType.BillPassed => "ПАРЛАМЕНТ И ПАРТИИ",
            PolEventType.BillRejected => "ПАРЛАМЕНТ И ПАРТИИ",
            PolEventType.ChambersDisagree => "ПАРЛАМЕНТ И ПАРТИИ",
            PolEventType.RulerVetoed => "ПАРЛАМЕНТ И ПАРТИИ",
            PolEventType.VetoOverridden => "ПАРЛАМЕНТ И ПАРТИИ",
            PolEventType.NationalLiberationUprising => "ОСВОБОЖДЕНИЕ НАРОДА",
            PolEventType.PeaceConferenceConcluded => "МИРНАЯ КОНФЕРЕНЦИЯ",
            PolEventType.CoalitionSpoilsDispute => "МИРНАЯ КОНФЕРЕНЦИЯ",
            PolEventType.RecognitionLobbyMission => "ПРИЗНАНИЕ",
            PolEventType.MetropolisRecognitionProtest => "ПРИЗНАНИЕ",
			_ => evt.Type.ToString().ToUpperInvariant()
		};
	}

	/// <summary>Подробный текст события (без префикса «Год N»). Сам локализирует параметры-ключи.</summary>
	private string DetailLine(PolEvent evt)
	{
		try
		{
			MapBox map = World.world;
			// Имена государств/городов по идентификаторам (если событие их не несёт).
			if (evt.KingdomId.HasValue && string.IsNullOrEmpty(evt.Get("kingdom")))
			{
				evt.With("kingdom", GameNotifier.KingdomName(map, evt.KingdomId.Value));
			}
			if (evt.CityId.HasValue && string.IsNullOrEmpty(evt.Get("city")))
			{
				evt.With("city", GameNotifier.CityName(map, evt.CityId.Value));
			}
			// «Лидер/участник» союзов в событиях — идентификаторы государств; раскрываем в имена.
			ResolveKingdomIdParam(evt, map, "leader");
			ResolveKingdomIdParam(evt, map, "member");
			// Параметры-ключи локализации (титул, идеологии, курс, тип, действие).
			LocalizeKeyParams(evt);

            if(evt.Type==PolEventType.NationalAlliancePosition)
                return evt.Render(L("RP_GOV26_EV_ALLIANCE_POSITION","{kingdom}: {question} — {subject}, решение: {result}. Личных голосов за: {yes}, против: {no}, воздержались: {abstain}; избирателей: {eligible}."));
            if(evt.Type==PolEventType.CivicAllianceMotion)
            {
                string stage=evt.Get("stage");string[] stages={"идёт голосование","одобрено","отклонено","ожидает исполнения","исполнено","игра не подтвердила исполнение","отменено: изменился состав альянса или допустимость вопроса"};
                int.TryParse(stage,out int number);string status=L("RP_GOV26_MOTION_STAGE_"+stage,number>=0 && number<stages.Length?stages[number]:stage);
                if(evt.Get("note")=="applicant-did-not-consent")status+=" (кандидат отказался от вступления)";
                bool admission=evt.Get("action")=="RP_GOV26_ADMISSION" || evt.Get("action")==L("RP_GOV26_ADMISSION",CivicText.Fallback("RP_GOV26_ADMISSION"));
                string action=L(admission?"RP_GOV26_ADMISSION":"RP_GOV26_EXPULSION",admission?"Приём":"Исключение");
                return evt.Render(L("RP_GOV26_EV_ALLIANCE_MOTION","{action}: {subject}. Инициатор — {proposer}. {status}; за — {yes} из {eligible} имеющих право голоса стран. Для приёма отдельно требуется конституционное согласие кандидата.")
                    .Replace("{action}",action).Replace("{status}",status));
            }
            if(evt.Type==PolEventType.RegionalCivicProcedure)
            {
                string outcome=evt.Get("outcome");string fallback=outcome switch
                {
                    "RP_GOV26_REGION_PERMISSION"=>"запрошено конституционное разрешение центра на региональный референдум",
                    "RP_GOV26_REGION_OPENED"=>"открыт референдум о законном выходе; отделения ещё нет",
                    "RP_GOV26_REGION_REJECTED"=>"референдум не одобрил выход; повторная попытка не ранее чем через установленный перерыв",
                    "RP_GOV26_REGION_PERMISSION_REFUSED"=>"центр не разрешил референдум; действует перерыв перед новой попыткой",
                    "RP_GOV26_REGION_INVALID"=>"процедура отменена: изменились собственник города, столица или закон выхода",
                    "RP_GOV26_REGION_NATIVE_FAILED"=>"голосование одобрило выход, но игровой API не создал новое государство",
                    "RP_GOV26_REGION_EXECUTED"=>"законный выход исполнен без объявления войны; международное признание определяется отдельно",
                    "RP_GOV26_REGION_WAR_FAILED"=>"город отделился, но игра неожиданно начала войну и не подтвердила мир; законное исполнение не подтверждено",
                    "RP_GOV26_REGION_REBELLION_REFUSED"=>"попытка незаконного отделения не исполнена: игровой API не подтвердил появление отдельной страны",
                    "RP_GOV26_REGION_REBELLION"=>"незаконный мятеж создал отдельную страну; война за независимость подтверждена",
                    "RP_GOV26_REGION_REBELLION_NO_WAR"=>"мятеж создал отдельную страну, но война за независимость не подтверждена игровым API",
                    _=>outcome
                };
                return evt.Render(L("RP_GOV26_EV_REGION","{city} / {kingdom}: {outcome}.").Replace("{outcome}",L(outcome,fallback)));
            }
            if(evt.Type==PolEventType.NationalismSeizedPower)
            {
                // 0.49: националисты пришли к власти — и сразу видно, что они требуют.
                string bound=evt.Get("bound");
                string verdict=evt.Get("verdict");
                return evt.Render(L("RP49_CHRON_SEIZED",
                    "НАЦИОНАЛИСТЫ У ВЛАСТИ. Держава {kingdom} требует вернуть государство «{origin}» ({bound}); настроение {pressure}%. Чужим закрыт въезд до {ban_until} года ({verdict}).")
                    .Replace("{bound}",L(bound,"родину потеряли"))
                    .Replace("{verdict}",L(verdict,"запрет въезда")));
            }
            if(evt.Type==PolEventType.NationalismPowerReturned)
            {
                return evt.Render(L("RP49_CHRON_RETURNED",
                    "{kingdom}: националисты вернули прежний строй и ушли в тень. Национализма больше нет, чужим закрыт въезд до {ban_until} года.")
                    .Replace("{ban_until}",evt.Get("ban_until")));
            }
            if(evt.Type==PolEventType.RaceNationalismSeizedPower)
            {
                // 0.49.1: чужая раса взяла державу себе. Никого не вывозим — только гражданство.
                string verdict=evt.Get("verdict");
                return evt.Render(L("RP49_CHRON_RACE_SEIZED",
                    "ЧУЖАЯ РАСА У ВЛАСТИ. В державе {kingdom} народ «{race}» ({share}% населения) взял власть: строй стал этнонационализмом, чужим отозвали гражданство и закрыли въезд до {ban_until} года ({verdict}). Никого не вывозили и не убивали — только лишили права быть гражданином.")
                    .Replace("{verdict}",L(verdict,"отзыв гражданства")));
            }
            if(evt.Type==PolEventType.RaceNationalismPowerReturned)
            {
                return evt.Render(L("RP49_CHRON_RACE_RETURNED",
                    "{kingdom}: расовые националисты народа «{race}» вернули прежний строй и ушли в тень. Въезд чужим закрыт до {ban_until} года."));
            }
            if(evt.Type==PolEventType.ForeignRulerWithoutLaw)
            {
                return evt.Render(L("RP52_CHRON_FOREIGN_RULER",
                    "{kingdom}: на трон сел {ruler} из народа «{race}», хотя конституция велит править только «{titular}». Он правит — но законность державы упала, и народ ропщет."));
            }
            if(evt.Type==PolEventType.PeopleLanguageShift)
            {
                return evt.Render(L("RP52_CHRON_LANGUAGE",
                    "{kingdom}: народ {race} перешёл на {stage}. Люди остались те же — сменился язык и обычай.")
                    .Replace("{stage}",L(evt.Get("stage"),"общий язык")));
            }
            if(evt.Type==PolEventType.LocalConstitutionAdopted)
            {
                return evt.Render(L("RP52_CHRON_LOCAL_ADOPTED",
                    "{city} ({kingdom}): народ {race} написал СВОЙ УСТАВ — свой язык в школах и свой суд. Общая конституция державы может с этим спорить, но устав уже написан."));
            }
            if(evt.Type==PolEventType.LocalConstitutionBanned)
            {
                return evt.Render(L("RP52_CHRON_LOCAL_BANNED",
                    "{kingdom}: столица ЗАПРЕТИЛА местный устав в городе {city}. Народ {race} этого не забудет: обида будет расти, пока запрет не снимут."));
            }
            if(evt.Type==PolEventType.LocalConstitutionConflict)
            {
                return evt.Render(L("RP52_CHRON_LOCAL_CONFLICT",
                    "{city} ({kingdom}): народ {race} открыто жалуется на столицу (обида {grievance} из 100). Местный устав расходится с общим законом: просят свой язык и свой суд."));
            }
            if(evt.Type==PolEventType.LocalConstitutionRevolt)
            {
                return evt.Render(L("RP52_CHRON_LOCAL_REVOLT",
                    "{city} ({kingdom}): народ {race} доведён до предела (обида {grievance} из 100). Если столица не уступит — город поднимет мятеж."));
            }
            if(evt.Type==PolEventType.WorldOutrageSanctions)
            {
                // 0.50: мир возмущается и наказывает — видно, за что и на сколько.
                string reason=evt.Get("reason");
                return evt.Render(L("RP50_CHRON_SANCTIONS",
                    "МИР ВОЗМУЩЁН. Против {kingdom} объявлены санкции на {years_word} (до {until} года), поддержали {supporters} держав. Причина: {reason}. Пока санкции идут, благосостояние державы падает каждый год, а отношения с соседями испорчены.")
                    .Replace("{reason}",L(reason,"националистический переворот")));
            }
            if(evt.Type==PolEventType.WorldOutrageStatusLost)
            {
                return evt.Render(L("RP50_CHRON_STATUS_LOST",
                    "{kingdom} ЛИШЕНА ЗВАНИЯ ВЕЛИКОЙ ДЕРЖАВЫ. Совет переизберут без неё — за то, что она отняла гражданство у своих жителей. На ближайшем съезде КМУД этот поступок назовут вслух."));
            }
            if(evt.Type==PolEventType.CivicElectionDeferred)
            {
                string reason=evt.Get("reason");string fallback=reason switch
                {
                    "RP_GOV26_ELECTION_NO_CANDIDATES"=>"нет допущенных кандидатов или избирателей",
                    "RP_GOV26_ELECTION_TIE"=>"нет единственного победителя",
                    // 0.48.5: второй тур тоже не дал победителя — так и пишем, без «нет единственного победителя» дважды.
                    "RP_GOV26_ELECTION_TIE_AFTER_RUNOFF"=>"и второй тур не выявил единственного победителя",
                    "RP_GOV26_ELECTION_INVALID_WINNER"=>"победитель больше не отвечает закону",
                    "RP_GOV26_ELECTION_API_REFUSAL"=>"игра не подтвердила вступление избранного главы в должность",
                    _=>reason
                };
                string rendered=evt.Render(L("RP_GOV26_EV_HEAD_DEFERRED","Выборы главы не завершены: {reason}. Случайный победитель не назначается.")
                    .Replace("{reason}",L(reason,fallback)));
                string kName=evt.Get("kingdom");
                return !string.IsNullOrWhiteSpace(kName) && !rendered.Contains(kName)?kName+": "+rendered:rendered;
            }
            if(evt.Type==PolEventType.DemocraticElection && evt.LocKey=="RP_EV_ATAMAN_ELECTED")
            {
                string rendered=evt.Render(L("RP_EV_ATAMAN_ELECTED","В вольной территории {kingdom} на вольном круге избран атаман {ruler} (без ограничения сроков — до кончины или смещения жителями)!"));
                string kName=evt.Get("kingdom");
                return !string.IsNullOrWhiteSpace(kName) && !rendered.Contains(kName)?kName+": "+rendered:rendered;
            }
            if(evt.Type==PolEventType.DemocraticElection && evt.LocKey=="RP_GOV26_EV_HEAD_RUNOFF")
            {
                // 0.48.5: второй тур — отдельной строкой, чтобы было видно, что выборы были не одни.
                string rendered=evt.Render(L("RP_GOV26_EV_HEAD_RUNOFF",
                    "второй тур определил победителя: {ruler} — {runoff_votes} голосов из {eligible}; ближайший соперник {other} — {other_votes}."));
                string kName=evt.Get("kingdom");
                return !string.IsNullOrWhiteSpace(kName) && !rendered.Contains(kName)?kName+": "+rendered:rendered;
            }
            if(evt.Type==PolEventType.DemocraticElection && evt.LocKey=="RP_GOV26_EV_HEAD_ELECTED")
            {
                string rendered=evt.Render(L(evt.LocKey,"{ruler}: избран личными голосами граждан — {votes} из {eligible} избирателей. Сроков за жизнь в этой стране: {terms}; следующие выборы — {next_election}."));
                string kName=evt.Get("kingdom");
                return !string.IsNullOrWhiteSpace(kName) && !rendered.Contains(kName)?kName+": "+rendered:rendered;
            }
            if(evt.Type==PolEventType.GreatPowerCompositionChanged)
                return evt.Render(L("RP_GOV26_EV_COUNCIL","Состав великих держав: {members}."));
            if(evt.Type==PolEventType.NationalRecognitionPosition || evt.Type==PolEventType.InternationalRecognitionDecided)
            {
                string key=evt.Type==PolEventType.NationalRecognitionPosition?"RP_GOV26_EV_NATIONAL_POSITION":"RP_GOV26_EV_RECOGNITION";
                string fallback=evt.Type==PolEventType.NationalRecognitionPosition
                    ?"{kingdom}: признание {target} — {result}. За: {yes}, против: {no}, воздержались: {abstain}; избирателей: {eligible}."
                    :"{kingdom}: {result}. Великие державы — за: {yes}, против: {no}, без решения: {missing}.";
                string result=evt.Get("result");
                string resultFallback=result switch
                {
                    "RP_GOV26_YES"=>"за", "RP_GOV26_NO"=>"против", "RP_GOV26_ABSTAIN"=>"нет действительного решения",
                    "RP_GOV26_RECOGNIZED"=>"государство признано",
                    "RP_GOV26_REFUSED"=>"в признании отказано; новое рассмотрение — только после изменения состава великих держав",
                    _=>result
                };
                return evt.Render(L(key,fallback).Replace("{result}",L(result,resultFallback)));
            }
            if(evt.Type==PolEventType.StrategicCampaignStarted)
            {
                string goalKey=evt.Get("goal");
                string goalFallback=goalKey switch
                {
                    "RP27_GOAL_SURVIVE"=>"Выживание и выход из кризиса",
                    "RP27_GOAL_STABILIZE"=>"Укрепление власти и порядка",
                    "RP27_GOAL_RECLAIM"=>"Возвращение утраченных земель",
                    "RP27_GOAL_UNIFY"=>"Мирное сближение и союз",
                    "RP27_GOAL_DEFENSE"=>"Оборонительная подготовка",
                    "RP27_GOAL_ECONOMY"=>"Экономический подъём и торговля",
                    _=>goalKey
                };
                return evt.Render(L("RP27_EV_CAMPAIGN_STARTED","{kingdom} начинает долгосрочную кампанию «{goal}» (срок: {years} лет, шагов: {steps}).")
                    .Replace("{goal}",L(goalKey,goalFallback)));
            }
            if(evt.Type==PolEventType.StrategicCampaignCompleted)
            {
                string goalKey=evt.Get("goal");
                string goalFallback=goalKey switch
                {
                    "RP27_GOAL_SURVIVE"=>"Выживание и выход из кризиса",
                    "RP27_GOAL_STABILIZE"=>"Укрепление власти и порядка",
                    "RP27_GOAL_RECLAIM"=>"Возвращение утраченных земель",
                    "RP27_GOAL_UNIFY"=>"Мирное сближение и союз",
                    "RP27_GOAL_DEFENSE"=>"Оборонительная подготовка",
                    "RP27_GOAL_ECONOMY"=>"Экономический подъём и торговля",
                    _=>goalKey
                };
                string resKey=evt.Get("result");
                string resFallback=resKey=="RP27_CAMPAIGN_RES_SUCCESS"?"цель достигнута":"срок истёк";
                return evt.Render(L("RP27_EV_CAMPAIGN_COMPLETED","{kingdom}: кампания «{goal}» завершена ({result}, выполнено шагов: {steps}).")
                    .Replace("{goal}",L(goalKey,goalFallback))
                    .Replace("{result}",L(resKey,resFallback)));
            }
            if(evt.Type==PolEventType.CivicPetitionSubmitted)
            {
                string demandKey=evt.Get("demand");
                string demandFallback=demandKey switch
                {
                    "RP28_DEMAND_PEACE"=>"Завершение изнурительной войны",
                    "RP28_DEMAND_TAXES"=>"Снижение налогов и отмена режима экономии",
                    "RP28_DEMAND_LIBERTY"=>"Упразднение тайного сыска",
                    "RP28_DEMAND_ELECTIONS"=>"Введение выборного правления",
                    "RP28_DEMAND_AUTONOMY"=>"Право регионов на референдум",
                    _=>demandKey
                };
                return evt.Render(L("RP28_EV_PETITION","{kingdom}: граждане подали петицию «{demand}» (поддержка: {support}%).")
                    .Replace("{demand}",L(demandKey,demandFallback)));
            }
            if(evt.Type==PolEventType.CivicUltimatumResolved)
            {
                string demandKey=evt.Get("demand");
                string demandFallback=demandKey switch
                {
                    "RP28_DEMAND_PEACE"=>"Завершение изнурительной войны",
                    "RP28_DEMAND_TAXES"=>"Снижение налогов и отмена режима экономии",
                    "RP28_DEMAND_LIBERTY"=>"Упразднение тайного сыска",
                    "RP28_DEMAND_ELECTIONS"=>"Введение выборного правления",
                    "RP28_DEMAND_AUTONOMY"=>"Право регионов на референдум",
                    _=>demandKey
                };
                string outKey=evt.Get("outcome");
                string outFallback=outKey=="RP28_MOVEMENT_CONCEDED"
                    ?"власть пошла на уступки и провела реформу"
                    :"движение подавлено силой — недовольство растёт";
                return evt.Render(L("RP28_EV_ULTIMATUM","{kingdom}: ультиматум движения «{demand}» ({support}% граждан) — {outcome}.")
                    .Replace("{demand}",L(demandKey,demandFallback))
                    .Replace("{outcome}",L(outKey,outFallback)));
            }
            if(evt.Type==PolEventType.ParliamentaryShift)
            {
                string partyKey=evt.Get("party");
                return evt.Render(L("RP28_EV_PARLIAMENT_SHIFT","{kingdom}: большинство в собрании получила партия «{party}» ({seats}% мест).")
                    .Replace("{party}",L(partyKey,partyKey)));
            }
            if(evt.Type==PolEventType.NationalLiberationUprising)
            {
                string modeKey=evt.Get("mode");
                bool confirmed = evt.Get("confirmed") != "0";
                string modeFallback=!confirmed && modeKey=="RP29_LIBERATION_WAR"
                    ?"восставшие пытаются отделиться; война за независимость пока не подтверждена игрой"
                    :modeKey=="RP29_LIBERATION_VASSAL"
                    ?"метрополия мирно признала их вассальными землями, но независимости не дала; города остались у метрополии"
                    :modeKey=="RP29_LIBERATION_PEACEFUL"
                    ?"метрополия мирно признала восстановление независимости"
                    :"восставшие провозгласили независимость с оружием в руках";
                return evt.Render(L("RP29_EV_LIBERATION","{kingdom}: судьба исторических земель «{homeland}» ({cities} гор.) — {mode}.")
                    .Replace("{mode}",L(modeKey,modeFallback)));
            }
            if(evt.Type==PolEventType.PeaceConferenceConcluded)
                return evt.Render(L("RP30_EV_PEACE_CONFERENCE","Мирная конференция ({allies} союзн.) после победы над {defeated}: раздел трофеев и вкладов — {division}."));
            if(evt.Type==PolEventType.CoalitionSpoilsDispute)
                return evt.Render(L("RP30_EV_SPOILS_DISPUTE","Дипломатический раскол: держава {ally} (вклад {share}%) возмущена тем, что {leader} обделил её при разделе земель {defeated}."));
            if(evt.Type==PolEventType.RecognitionLobbyMission)
                return evt.Render(L("RP31_EV_RECOGNITION_LOBBY","Непризнанное государство {kingdom} направило посольство к великой державе {power}, добиваясь международного признания."));
            if(evt.Type==PolEventType.MetropolisRecognitionProtest)
                return evt.Render(L("RP31_EV_METROPOLIS_PROTEST","Метрополия {metropolis} направила дипломатический протест великим державам против признания мятежного края {breakaway}."));
			switch (evt.Type)
			{
				case PolEventType.Succession:
				{
					// Ручная подстановка: титул — с заглавной, «state» — именительный падеж
					// (в событии он в предложном), параметры события не мутируем.
					string line = StateNaming.DropFormIfTitled(L("RP_CHRON_EV_SUCCESSION", "Государство {kingdom} ({state}): новый правитель — {title} {ruler}"), evt.Get("kingdom"));
					line = line.Replace("{kingdom}", evt.Get("kingdom"))
						.Replace("{state}", StateNameOf(evt.KingdomId ?? -1))
						.Replace("{title}", CapitalizedTitle(evt))
						.Replace("{ruler}", evt.Get("ruler"));
					string age = evt.Get("age");
					if (!string.IsNullOrEmpty(age) && age != "0")
					{
						line += " (" + Fmt2("RP_CHRON_AGE", "возраст {age}", "age", age) + ")";
					}
					string oldRuler = evt.Get("old_ruler");
					if (!string.IsNullOrEmpty(oldRuler))
					{
						string old = L("RP_CHRON_EV_SUCCESSION_OLD", "предыдущий правитель: {old_title} {old_ruler}");
						old = old.Replace("{old_title}", GameText.Capitalize(LocalizedKeyParam(evt, "old_title")))
							.Replace("{old_ruler}", oldRuler);
						string oldReign = evt.Get("old_reign");
						if (!string.IsNullOrEmpty(oldReign))
						{
							old += " (" + Fmt2("RP_CHRON_REIGN", "лет правления: {years}", "years", oldReign) + ")";
						}
						line += "; " + old;
					}
					return line;
				}
				case PolEventType.NewReligion:
					return StateLine("RP_CHRON_EV_RELIGION", "Государство {kingdom} ({state}): основана религия «{religion}»", evt, "religion");
				case PolEventType.NewLanguage:
					return StateLine("RP_CHRON_EV_LANGUAGE", "Государство {kingdom} ({state}): зародился язык «{language}»", evt, "language");
				case PolEventType.NewCulture:
					return StateLine("RP_CHRON_EV_CULTURE", "Государство {kingdom} ({state}): возникла культура «{culture}»", evt, "culture");
				case PolEventType.KingdomAdoptedIdeology:
					if (string.Equals(evt.LocKey, "RP_EV_TRIBE_CIVILIZED", StringComparison.Ordinal))
					{
						return Fmt("RP_CHRON_EV_TRIBE_CIVILIZED", "Племя {old_name} накопило ресурсы на цивилизацию, получило признание ({recognition}) и избрало себе идеологию «{new}», став государством {kingdom}", evt);
					}
					if (string.Equals(evt.LocKey, "RP_EV_KHAGANATE_CIVILIZED", StringComparison.Ordinal))
					{
						return Fmt("RP_CHRON_EV_KHAGANATE_CIVILIZED", "Каганат {old_name} накопил ресурсы на цивилизацию, получил признание ({recognition}) и избрал себе идеологию «{new}», став государством {kingdom}", evt);
					}
					if (!string.IsNullOrWhiteSpace(evt.Get("old_name")) && !string.Equals(evt.Get("old_name"), evt.Get("kingdom"), StringComparison.Ordinal))
					{
						return Fmt("RP_CHRON_EV_ADOPTED_RENAMED", "Государство {old_name}: строй сменился — с {old} на {new}; новое официальное наименование державы — «{kingdom}»", evt);
					}
					return Fmt("RP_CHRON_EV_ADOPTED", "Государство {kingdom}: строй сменился — с {old} на {new}", evt);
				case PolEventType.KingdomReformed:
					return Fmt("RP_CHRON_EV_REFORM", "Государство {kingdom}: новый политический курс — {new}", evt);
				case PolEventType.RevolutionStarted:
					return Fmt("RP_CHRON_EV_REV_START", "Государство {kingdom}: началось восстание сторонников {ideology}", evt);
				case PolEventType.RevolutionSucceeded:
					return Fmt("RP_CHRON_EV_REV_WIN", "Государство {kingdom}: революция победила, новый строй — {new}", evt);
				case PolEventType.RevolutionFailed:
					return Fmt("RP_CHRON_EV_REV_FAIL", "Государство {kingdom}: восстание сторонников {ideology} подавлено", evt);
				case PolEventType.CityIdeologyShift:
					if (string.Equals(evt.LocKey, "RP_EV_TRIBAL_AWAKENING", StringComparison.Ordinal))
					{
						return Fmt("RP_CHRON_EV_TRIBAL_AWAKENING", "В племени {kingdom} народ более не желает жить родовым укладом и требует перехода к цивилизации («{new}»)", evt);
					}
					if (string.Equals(evt.LocKey, "RP_EV_KHAGANATE_AWAKENING", StringComparison.Ordinal))
					{
						return Fmt("RP_CHRON_EV_KHAGANATE_AWAKENING", "В каганате {kingdom} народ более не желает жить укладом улусов и требует перехода к цивилизации («{new}»)", evt);
					}
					return Fmt("RP_CHRON_EV_CITY", "Город {city}: доминирующие настроения — {old} → {new} (доля {share}%)", evt);
				case PolEventType.UnionFormed:
					return Fmt("RP_CHRON_EV_UNION_FORMED", "Образован союз «{name}» ({type}). Лидер: {leader}. Участник: {member}", evt);
				case PolEventType.UnionMerged:
					if (string.Equals(evt.Get("union_kind"), "confederation", StringComparison.Ordinal)
						|| string.Equals(evt.LocKey, "RP_EV_CONFEDERATION_MERGED", StringComparison.Ordinal))
					{
						return Fmt("RP_CHRON_EV_CONFEDERATION_MERGED", "Вольная территория {member} добровольно объединяется с вольной территорией {leader} в конфедерацию вольных территорий «{name}»", evt);
					}
					if (string.Equals(evt.Get("union_kind"), "federation", StringComparison.Ordinal)
						|| string.Equals(evt.LocKey, "RP_EV_FEDERATION_MERGED", StringComparison.Ordinal))
					{
						return Fmt("RP_CHRON_EV_FEDERATION_MERGED", "Держава {member} добровольно объединяется с державой {leader} в федерацию «{name}»", evt);
					}
					return Fmt("RP_CHRON_EV_UNION_MERGED", "Держава {member} добровольно объединяется с державой {leader} в унию «{name}»", evt);
				case PolEventType.Impeached:
					if (string.Equals(evt.LocKey, "RP_EV_ATAMAN_DISMISSED", StringComparison.Ordinal))
					{
						return Fmt("RP_CHRON_EV_ATAMAN_DISMISSED", "Жители вольной территории {kingdom} на вольном круге сняли с должности атамана {ruler} за потерю народного доверия", evt);
					}
					return Fmt("RP_CHRON_EV_IMPEACHED", "Собрание {kingdom} объявило импичмент {ruler}", evt);
				case PolEventType.UnionMemberJoined:
					return Fmt("RP_CHRON_EV_UNION_JOINED", "В союз «{union}» вошло государство {member} (всего {count} держав)", evt);
				case PolEventType.UnionMemberLeft:
					return Fmt("RP_CHRON_EV_UNION_LEFT", "Из союза «{union}» вышло государство {member}", evt);
				case PolEventType.UnionDissolved:
					return Fmt("RP_CHRON_EV_UNION_DIS", "Союз «{name}» распался", evt);
				case PolEventType.DiplomaticAction:
					return Fmt("RP_CHRON_EV_DIPLO", "{kingdom}: {action}", evt);
				case PolEventType.WarStarted:
				{
					string attackerSide = !string.IsNullOrEmpty(evt.Get("attacker_side")) ? evt.Get("attacker_side") : evt.Get("attacker");
					string defenderSide = !string.IsNullOrEmpty(evt.Get("defender_side")) ? evt.Get("defender_side") : evt.Get("defender");
					string template = ("1".Equals(evt.Get("total")) ? L("RP_CHRON_EV_WAR_START_TOTAL", "Тотальная война: {attacker} против всех") : L("RP_CHRON_EV_WAR_START", "Началась война: {attacker} → {defender}"));
					string line = template.Replace("{attacker}", attackerSide).Replace("{defender}", defenderSide);
					string warName = evt.Get("war_name");
					if (!string.IsNullOrEmpty(warName))
					{
						line += " «" + warName + "»";
					}
					string type = LocalizedKeyParam(evt, "type");
					if (!string.IsNullOrEmpty(type))
					{
						line += " (" + type + ")";
					}
					// Этап 21: ванильный альянс вступается за защищающегося.
					string allies = evt.Get("allies");
					string attackerAllies = evt.Get("attacker_allies");
					if (!string.IsNullOrEmpty(allies) && !string.IsNullOrEmpty(attackerAllies))
					{
						line += " — " + Fmt2("RP_CHRON_WAR_ALLIANCES",
							"ВОЙНА АЛЬЯНСОВ: «{attacker_allies}» ({attacker}) против «{allies}» ({defender})",
							"attacker_allies", attackerAllies, "allies", allies, "attacker", attackerSide)
							.Replace("{defender}", defenderSide);
					}
					else if (!string.IsNullOrEmpty(allies))
					{
						line += " — " + Fmt2("RP_CHRON_WAR_ALLIES",
							"за державу {defender} вступается альянс «{allies}»",
							"defender", evt.Get("defender"), "allies", allies);
					}
					else if (!string.IsNullOrEmpty(attackerAllies))
					{
						line += " — " + Fmt2("RP_CHRON_WAR_ATTACKER_ALLIES",
							"за нападающую державу {attacker} стоит альянс «{attacker_allies}»",
							"attacker", evt.Get("attacker"), "attacker_allies", attackerAllies);
					}
					return line;
				}
				case PolEventType.WarEnded:
				{
					string attackerSide = !string.IsNullOrEmpty(evt.Get("attacker_side")) ? evt.Get("attacker_side") : evt.Get("attacker");
					string defenderSide = !string.IsNullOrEmpty(evt.Get("defender_side")) ? evt.Get("defender_side") : evt.Get("defender");
					string line = L("RP_CHRON_EV_WAR_END", "Война: {attacker} против {defender} — {winner}")
						.Replace("{attacker}", attackerSide)
						.Replace("{defender}", defenderSide)
						.Replace("{winner}", LocalizedKeyParam(evt, "winner"));
					string duration = evt.Get("duration");
					string casualties = evt.Get("casualties");
					List<string> meta = new List<string>();
					string warName = evt.Get("war_name");
					if (!string.IsNullOrEmpty(warName))
					{
						meta.Add("«" + warName + "»");
					}
					if (!string.IsNullOrEmpty(duration))
					{
						meta.Add(Fmt2("RP_CHRON_WAR_DURATION", "длительность: {years}", "years", RuYearsPhrase(duration)));
					}
					if (!string.IsNullOrEmpty(casualties))
					{
						meta.Add(casualties);
					}
					if (meta.Count > 0)
					{
						line += " (" + string.Join("; ", meta) + ")";
					}
					return line;
				}
			case PolEventType.PeaceSettlement:
			{
				string line = L("RP_CHRON_EV_PEACE_SETTLEMENT", "Мирный договор: держава {victor} диктует условия державе {defeated} — {terms}. Перемирие до {truce} года.")
					.Replace("{victor}", evt.Get("victor"))
					.Replace("{defeated}", evt.Get("defeated"))
					.Replace("{terms}", LocalizedKeyParam(evt, "terms"))
					.Replace("{cities}", evt.Get("cities"))
					.Replace("{gold}", evt.Get("gold"))
					.Replace("{years}", evt.Get("years"))
					.Replace("{war_years}", evt.Get("war_years"))
					.Replace("{truce}", evt.Get("truce"));
				string casualties = evt.Get("casualties");
				if (!string.IsNullOrEmpty(casualties))
				{
					line += " (" + casualties + ").";
				}
				return line;
			}
			case PolEventType.KingdomRestored:
				return L("RP_CHRON_EV_KINGDOM_RESTORED", "Как после великой войны: держава {victor} не стала стирать {old} из истории. На довоенных землях восстановлено государство {kingdom} — {mode}. Возвращено городов: {cities}; столица и ещё {kept} гор. остались за победителем.")
					.Replace("{kingdom}", evt.Get("kingdom"))
					.Replace("{old}", evt.Get("old"))
					.Replace("{victor}", evt.Get("victor"))
					.Replace("{mode}", LocalizedKeyParam(evt, "mode"))
					.Replace("{cities}", evt.Get("cities"))
					.Replace("{kept}", evt.Get("kept"));
			case PolEventType.WhitePeace:
			{
				string line = L("RP_CHRON_EV_WHITE_PEACE", "Белый мир: {kingdom} и {partner} прекратили бессмысленную бойню ({years} лет войны). Перемирие до {truce} года.")
					.Replace("{kingdom}", evt.Get("kingdom"))
					.Replace("{partner}", evt.Get("partner"))
					.Replace("{years}", evt.Get("years"))
					.Replace("{truce}", evt.Get("truce"));
				string casualties = evt.Get("casualties");
				if (!string.IsNullOrEmpty(casualties))
				{
					line += " (" + casualties + ").";
				}
				return line;
			}
			case PolEventType.TruceBroken:
				return L("RP_CHRON_EV_TRUCE_BROKEN", "{kingdom} вероломно разорвало перемирие с державой {partner}! Доверие к правителям пошатнулось.")
					.Replace("{kingdom}", evt.Get("kingdom"))
					.Replace("{partner}", evt.Get("partner"));
		case PolEventType.TruceExpired:
			return L("RP_CHRON_EV_TRUCE_EXPIRED", "Перемирие между {kingdom} и {partner} истекло — путь к войне вновь открыт.")
				.Replace("{kingdom}", evt.Get("kingdom"))
				.Replace("{partner}", evt.Get("partner"));
		// Этап 23.
		case PolEventType.ElectionDeferred:
            return L("RP_CHRON25_ELECTION_DEFERRED", "Государство {kingdom}: проверка выборов не дала подтверждённого кандидата — нет подходящего жителя либо недоступны данные. Новый мандат не выдан. Следующая проверка — год {year}.")
                .Replace("{kingdom}", evt.Get("kingdom")).Replace("{year}", evt.Get("next_election"));
        case PolEventType.SubspeciesExtinct:
            return L("RP_CHRON25_EXTINCTION_SINGLE", "Подвид {species} вида {race} больше не обнаружен в мире. Причина вымирания этим наблюдением не установлена.")
                .Replace("{species}", evt.Get("species")).Replace("{race}", evt.Get("race"));
        case PolEventType.LastResidentDied:
            return L("RP_CHRON25_LAST_RESIDENT", "В {kingdom} погиб последний житель — {resident} ({cause}).")
                .Replace("{kingdom}", evt.Get("kingdom")).Replace("{resident}", ReadableEdition && evt.ActorId.HasValue ? ChronicleNotebook.Person(evt.Get("resident"), evt.ActorId.Value) : evt.Get("resident"))
                .Replace("{cause}", LocalizedKeyParam(evt, "cause"));
        case PolEventType.KingdomDisappeared:
            if (evt.Death != null && evt.Death.WasLastResident)
                return L("RP_CHRON25_END_LAST", "Государство {kingdom} прекратило существование после смерти последнего жителя — {resident} (год {year}, {cause}).")
                    .Replace("{kingdom}", evt.Get("kingdom")).Replace("{resident}", ReadableEdition ? ChronicleNotebook.Person(evt.Death.ActorName, evt.Death.ActorId) : evt.Death.ActorName)
                    .Replace("{year}", evt.Death.Year.ToString()).Replace("{cause}", L(evt.Death.LocKeyFor(evt.Death.WasFemale), evt.Death.Cause.ToString()));
            return Fmt("RP_CHRON_EV_KINGDOM_DISAPPEARED", "Государство {kingdom} больше не существует как отдельная держава.", evt);
        case PolEventType.KingdomCollapsed:
			return L("RP_CHRON_EV_KINGDOM_COLLAPSED", "Государство {kingdom} распалось на {count} обломков: {fragments}. Прежняя империя перестала существовать как единое целое.")
				.Replace("{kingdom}", evt.Get("kingdom"))
				.Replace("{count}", evt.Get("count"))
				.Replace("{fragments}", evt.Get("fragments"));
		case PolEventType.RegencyCrowned:
			return L("RP_CHRON_EV_REGENCY_CROWNED", "Государство {kingdom}: наследник {heir} слишком юн ({age} лет) — корону носит регент {regent} до совершеннолетия престолонаследника.")
				.Replace("{kingdom}", evt.Get("kingdom"))
				.Replace("{heir}", evt.Get("heir"))
				.Replace("{age}", evt.Get("age"))
				.Replace("{regent}", LocalizedKeyParam(evt, "regent"));
		case PolEventType.RegencyPassed:
			return L("RP_CHRON_EV_REGENCY_PASSED", "Государство {kingdom}: наследнику {heir} исполнилось 18 — корона возвращается законному монарху; регентство {regent} завершено.")
				.Replace("{kingdom}", evt.Get("kingdom"))
				.Replace("{heir}", evt.Get("heir"))
				.Replace("{regent}", LocalizedKeyParam(evt, "regent"));
		case PolEventType.DynastyRenamed:
			return L("RP_CHRON_EV_DYNASTY_RENAMED", "Правящий дом государства {kingdom} вступил в новую эру: клан «{old}» переименован по фамилии короля-основателя {founder} — теперь династия {dynasty}.")
				.Replace("{kingdom}", evt.Get("kingdom"))
				.Replace("{old}", evt.Get("old"))
				.Replace("{dynasty}", evt.Get("dynasty"))
				.Replace("{founder}", evt.Get("founder"));
		case PolEventType.PowerRaid:
			return L("RP_CHRON_EV_POWER_RAID", "Звено бомбардировщиков державы {kingdom} совершило тайный налёт на город {town} державы {target}.")
				.Replace("{kingdom}", evt.Get("kingdom"))
				.Replace("{target}", evt.Get("target"))
				.Replace("{town}", evt.Get("town"));
		case PolEventType.BorderOpened:
			return L("RP_CHRON_EV_BORDER_OPENED", "{kingdom} и {partner} открыли друг другу границы: граждане свободно переходят из одной державы в другую.")
				.Replace("{kingdom}", evt.Get("kingdom"))
				.Replace("{partner}", evt.Get("partner"));
		case PolEventType.BorderClosed:
			return L("RP_CHRON_EV_BORDER_CLOSED", "{kingdom} закрыло границу с {partner}: чужаков выдворяют домой, торговые пути замораживаются.")
				.Replace("{kingdom}", evt.Get("kingdom"))
				.Replace("{partner}", evt.Get("partner"));
		case PolEventType.BorderExpulsion:
			return L("RP_CHRON_EV_BORDER_EXPULSION", "{kingdom}: чужак {intruder} из державы {partner} пойман за закрытой границей и выдворен домой.")
				.Replace("{kingdom}", evt.Get("kingdom"))
				.Replace("{partner}", evt.Get("partner"))
				.Replace("{intruder}", evt.Get("intruder"));
			case PolEventType.ActorConverted:
				return Fmt("RP_CHRON_EV_CONVERTED", "Житель сменил взгляды: {old} → {new}", evt);
				case PolEventType.CitySeceded:
				{
					string template = L("RP_EV_CITY_SECEDED", "Город {city} отделился от государства {kingdom} в результате кризиса ({reason})!");
					string detail = template.Replace("{city}", evt.Get("city"))
						.Replace("{kingdom}", evt.Get("kingdom"))
						.Replace("{reason}", LocalizedKeyParam(evt, "reason"));
                    if (!string.IsNullOrEmpty(evt.Get("new_kingdom"))) detail += " " + L("RP_CHRON251_SECESSION_CHILD", "Новое государство: {state}. Война с прежней державой: {status}.").Replace("{state}", evt.Get("new_kingdom")).Replace("{status}", L(evt.Get("war_status"), "не подтверждена"));
                    return detail;
				}
				case PolEventType.CivilWarStarted:
				{
					string template = L("RP_CHRON_EV_CIVILWAR_START",
						"В государстве {kingdom} вспыхнула гражданская война ({cause}). Повстанцы под знаменем {ideology} "
						+ "во главе с {leader} заняли городов: {cities}. Перевес сил повстанцев: {share}%.");
					return template.Replace("{kingdom}", evt.Get("kingdom"))
						.Replace("{cause}", LocalizedKeyParam(evt, "cause"))
						.Replace("{ideology}", LocalizedKeyParam(evt, "ideology"))
						.Replace("{leader}", FallbackLeader(evt))
						.Replace("{cities}", evt.Get("cities"))
						.Replace("{share}", evt.Get("share"));
				}
			case PolEventType.TradeRouteOpened:
				return Fmt("RP_CHRON_EV_TRADE_OPENED",
					"Государство {kingdom} проложило новый торговый путь — купцы пошли по новым дорогам.", evt);
			case PolEventType.TradeRouteUpgraded:
				return Fmt("RP_CHRON_EV_TRADE_UPGRADED",
					"Торговля {kingdom} расширена: путь с державой {kingdom_partner} повышен до уровня {level}.", evt);
			case PolEventType.TradeRouteBroken:
				return Fmt("RP_CHRON_EV_TRADE_BROKEN",
					"Торговля между {kingdom} и {partner} прекращена — путь перекрыт.", evt);
			case PolEventType.TradeRoutesSevered:
				return Fmt("RP_CHRON_EV_TRADE_SEVERED",
					"Государство {kingdom} закрыло границу с державой {partner}: торговые пути разорваны — линий потеряно: {count}.", evt);
			case PolEventType.TradeEmbargo:
				return Fmt("RP_CHRON_EV_TRADE_EMBARGO",
					"Государство {kingdom} ввело торговое эмбарго против {target} на {years} лет; перекрыто путей: {routes}.", evt);
			case PolEventType.EconomicCrisisStarted:
				return Fmt("RP_CHRON_EV_ECO_CRISIS",
					"В государстве {kingdom} — {crisis}: ожидается, что продлится около {years} лет.", evt);
			case PolEventType.DynasticMarriage:
				return Fmt("RP_CHRON_EV_DYNASTIC_MARRIAGE",
					"Династический брак: правящие дома держав {kingdom} ({ruler}) и {partner} ({spouse}) скрепили династический союз.", evt);
			case PolEventType.TreatySigned:
				return L("RP_CHRON_EV_TREATY_SIGNED", "Державы {kingdom} и {partner} скрепили подписями {treaty}.")
					.Replace("{kingdom}", evt.Get("kingdom")).Replace("{partner}", evt.Get("partner"))
					.Replace("{treaty}", LocalizedKeyParam(evt, "treaty"));
			case PolEventType.TreatyBroken:
				return L("RP_CHRON_EV_TREATY_BROKEN", "Государство {kingdom} вероломно нарушило {treaty} с державой {partner}.")
					.Replace("{kingdom}", evt.Get("kingdom")).Replace("{partner}", evt.Get("partner"))
					.Replace("{treaty}", LocalizedKeyParam(evt, "treaty"));
			case PolEventType.TreatyExpired:
				return L("RP_CHRON_EV_TREATY_EXPIRED", "Истёк срок действия договора ({treaty}) между {kingdom} и {partner}.")
					.Replace("{kingdom}", evt.Get("kingdom")).Replace("{partner}", evt.Get("partner"))
					.Replace("{treaty}", LocalizedKeyParam(evt, "treaty"));
			case PolEventType.CasusBelliDeclared:
				return L("RP_CHRON_EV_CASUS_BELLI", "Государство {kingdom} объявило войну державе {target}. Повод: {cb}.")
					.Replace("{kingdom}", evt.Get("kingdom")).Replace("{target}", evt.Get("target"))
					.Replace("{cb}", LocalizedKeyParam(evt, "cb"));
			case PolEventType.EspionageExposed:
				return L("RP_CHRON_EV_ESPIONAGE_EXPOSED", "В государстве {kingdom} разоблачены агенты державы {spy} ({op}).")
					.Replace("{kingdom}", evt.Get("kingdom")).Replace("{spy}", evt.Get("spy"))
					.Replace("{op}", LocalizedKeyParam(evt, "op"));
			case PolEventType.EspionageSuccess:
				return L("RP_CHRON_EV_ESPIONAGE_SUCCESS", "Тайная операция ({op}) державы {kingdom} против {target} увенчалась успехом.")
					.Replace("{kingdom}", evt.Get("kingdom")).Replace("{target}", evt.Get("target"))
					.Replace("{op}", LocalizedKeyParam(evt, "op"));
			case PolEventType.CoalitionFormed:
				return Fmt("RP_CHRON_EV_COALITION_FORMED",
					"Против державы {target} собрана коалиция из {count} государств.", evt);
			case PolEventType.CoalitionWar:
				return Fmt("RP_CHRON_EV_COALITION_WAR",
					"Государство {kingdom} вступило в войну против {target}, защищая союзника {ally}.", evt);
			case PolEventType.CoalitionDissolved:
				return Fmt("RP_CHRON_EV_COALITION_DISSOLVED",
					"Коалиция против державы {target} распущена за ненадобностью.", evt);
                case PolEventType.VassalRebellion:
                    return Fmt("RP_CHRON_EV_VASSAL_REBELLION",
                        evt.Get("confirmed") == "0"
                            ? "Вассал {kingdom} пытается выйти из-под власти сюзерена {suzerain}; война за независимость пока не подтверждена игрой."
                            : "Вассал {kingdom} восстал против сюзерена {suzerain} и объявил войну за независимость.", evt);
			// Этап 21: альянсы игры, наследование короны, институты, интеграция, полная уния.
			// Имена держав в параметрах «leader»/«member» и ключи RP_* раскрываются до этого свитча.
			case PolEventType.AllianceFormed:
				return Fmt("RP_CHRON_EV_ALLIANCE_FORMED",
					"Создан альянс «{name}» по почину державы {leader} — участники: {members} ({count} держав).", evt);
			case PolEventType.AllianceBroken:
				return Fmt("RP_CHRON_EV_ALLIANCE_BROKEN",
					"Альянс «{name}» распался (на момент роспуска: {count} держав).", evt);
			case PolEventType.AllianceMemberJoined:
				return Fmt("RP_CHRON_EV_ALLIANCE_JOINED",
					"Держава {member} вступила в альянс «{name}» — теперь {count} держав.", evt);
			case PolEventType.AllianceMemberLeft:
				return Fmt("RP_CHRON_EV_ALLIANCE_LEFT",
					"Держава {member} покинула альянс «{name}».", evt);
			case PolEventType.UnionCrownInherited:
				return Fmt("RP_CHRON_EV_UNION_CROWN_INHERITED",
					"Смена правителя в державе {kingdom}: корона осталась в союзе «{union}».", evt);
			case PolEventType.UnionSuccessionCrisis:
				return Fmt("RP_CHRON_EV_UNION_SUCCESSION_CRISIS",
					"Кризис престолонаследия в державе {kingdom}: союз «{union}» разорван.", evt);
			case PolEventType.UnionInstitutionGained:
				return Fmt("RP_CHRON_EV_UNION_INSTITUTION",
					"Союз «{union}» учредил институт: {institution} (уровень {level}).", evt);
			case PolEventType.IntegrationStarted:
				return Fmt("RP_CHRON_EV_INTEGRATION_STARTED",
					"Держава {kingdom} начала интеграцию державы {member} (союз «{union}»).", evt);
			case PolEventType.IntegrationCompleted:
				return Fmt("RP_CHRON_EV_INTEGRATION_COMPLETED",
					"Интеграция завершена: держава {member} поглощена державой {kingdom}.", evt);
			case PolEventType.UnionReformed:
				return Fmt("RP_CHRON_EV_UNION_REFORMED",
					"Союз «{union}» реформирован в полную унию во главе с державой {leader}.", evt);
			case PolEventType.EconomicCrisisEnded:
				return Fmt("RP_CHRON_EV_ECO_CRISIS_END",
					"Экономика {kingdom} оправилась: {crisis} позади, благосостояние {prosperity}%.", evt);
			case PolEventType.EconomicBoom:
				return Fmt("RP_CHRON_EV_ECO_BOOM",
					"В государстве {kingdom} экономический бум: благосостояние {prosperity}%, казна пополнена на {gold}.", evt);
			case PolEventType.CivilWarEnded:
				{
					string template = L("RP_CHRON_EV_CIVILWAR_END",
						"Гражданская война в {kingdom} ({cause}) окончена: {resolution}. Длилась {years}, мятежных городов: {cities}.");
					return template.Replace("{kingdom}", evt.Get("kingdom"))
						.Replace("{cause}", LocalizedKeyParam(evt, "cause"))
						.Replace("{resolution}", LocalizedKeyParam(evt, "resolution"))
						.Replace("{years}", RuYearsPhrase(evt.Get("years")))
						.Replace("{cities}", evt.Get("cities"));
				}
			case PolEventType.KingdomSeceded:
				{
					string template = L("RP_CHRON_EV_SECESSION",
						"РАСКОЛ: от государства {kingdom} отделилось новое государство строя {ideology} "
						+ "({cause}); городов: {cities}, предводитель — {leader}.");
					return template.Replace("{kingdom}", evt.Get("kingdom"))
						.Replace("{ideology}", LocalizedKeyParam(evt, "ideology"))
						.Replace("{cause}", LocalizedKeyParam(evt, "cause"))
						.Replace("{cities}", evt.Get("cities"))
						.Replace("{leader}", FallbackLeader(evt));
				}
			case PolEventType.DynasticShift:
				{
					string template = L("RP_EV_DYNASTY_SHIFT", "Династия «{old_dynasty}» пресеклась. На трон {kingdom} взошёл {ruler}, положив начало династии «{new_dynasty}»!");
					return template.Replace("{kingdom}", evt.Get("kingdom"))
						.Replace("{ruler}", evt.Get("ruler"))
						.Replace("{old_dynasty}", evt.Get("old_dynasty"))
						.Replace("{new_dynasty}", evt.Get("new_dynasty"));
				}
			case PolEventType.KmudConvened:
				{
					// 0.32: созыв всемирной мирной конференции.
					string template = L("RP32_CHRON_KMUD_CONVENED",
						"СОЗВАНА КМУД — Конференция Мирного Урегулирования Дел. Держав-участников: {members}. "
						+ "Все войны прекращены белым миром (войн погашено: {wars_ended}). "
						+ "Запрет войн — до {until} года, конференция закроется в {closes} году.");
					return template.Replace("{members}", evt.Get("members"))
						.Replace("{wars_ended}", evt.Get("wars_ended"))
						.Replace("{until}", evt.Get("until"))
						.Replace("{closes}", evt.Get("closes"));
				}
			case PolEventType.KmudPauseViolated:
				{
					string template = L("RP32_CHRON_KMUD_PAUSE_VIOLATED",
						"КМУД: объявленная война отменена — действует запрет войн. Войн погашено за раз: {count}.");
					return template.Replace("{count}", evt.Get("count"));
				}
			case PolEventType.KmudAllianceFormed:
				{
					string template = L("RP32_CHRON_KMUD_ALLIANCE",
						"Все державы вступили в общий альянс: «{name}». Участников: {members}. "
						+ "До конца конференции войны между ними запрещены.");
					return template.Replace("{name}", evt.Get("name"))
						.Replace("{members}", evt.Get("members"));
				}
			case PolEventType.KmudClosed:
				{
					string peaceYears = evt.Get("peace_years");
					bool extended = !string.IsNullOrEmpty(peaceYears) && peaceYears != "0";
					string template = extended
						? L("RP32_CHRON_KMUD_CLOSED",
							"КМУД ЗАВЕРШЕНА. Альянс распущен, держав-участников: {members}. "
							+ "Жители выбрали мир ещё на {peace_years} лет — войны запрещены до {until} года.")
						: L("RP32_CHRON_KMUD_CLOSED_NOWAR",
							"КМУД ЗАВЕРШЕНА. Альянс распущен, держав-участников: {members}. "
							+ "Продления мира не выбрано — прежние враги вольны объявлять войну снова.");
					return template.Replace("{members}", evt.Get("members"))
						.Replace("{peace_years}", peaceYears)
						.Replace("{until}", evt.Get("until"));
				}
			case PolEventType.KmudAgenda:
				{
					// Повестка: о каком конфликте и о каких державах будут голосовать жители.
					// 0.48.4: «Долгий конфликт — — против —, лет: 0» читалось как поломка.
					// Если войн нет, так и пишем.
					bool hasConflict = !string.IsNullOrWhiteSpace(evt.Get("aggressor"))
						&& evt.Get("aggressor") != "—";
					string template = hasConflict
						? L("RP32_CHRON_KMUD_AGENDA",
							"КМУД: повестка дня. Долгий конфликт — {aggressor} против {defender}, лет: {years}. "
							+ "Обвиняемая держава: {worst}. Самая отстающая: {poorest}.")
						: L("RP32_CHRON_KMUD_AGENDA_NOWAR",
							"КМУД: повестка дня. Войн нет — мирить некого. "
							+ "Обвиняемая держава: {worst}. Самая отстающая: {poorest}.");
					return template.Replace("{aggressor}", evt.Get("aggressor"))
						.Replace("{defender}", evt.Get("defender"))
						.Replace("{years}", evt.Get("years"))
						.Replace("{worst}", evt.Get("worst"))
						.Replace("{poorest}", evt.Get("poorest"));
				}
			case PolEventType.KmudResult:
				{
					// Один вопрос — одна строка: что решили жители и сколько их голосовало.
					string template = L("RP32_CHRON_KMUD_RESULT",
						"КМУД, вопрос «{question}»: решено — {answer} (голосов: {voters}).");
					string line = template.Replace("{question}", LocalizedKeyParam(evt, "question"))
						.Replace("{answer}", LocalizedKeyParam(evt, "answer"))
						.Replace("{voters}", evt.Get("voters"));
					if (evt.Get("tied") == "1")
					{
						line += " " + L("RP32_CHRON_KMUD_TIE",
							"Голоса разделились поровну — принят более мягкий вариант.");
					}
					return line;
				}
			case PolEventType.KmudPact:
				{
					string template = L("RP32_CHRON_KMUD_PACT",
						"КМУД: {a} и {b} связаны пактом «только оборона» до {until} года ({years} лет): "
						+ "нападать друг на друга нельзя, защищать — обязательно. Союз оформлен в игре: {allied}.");
					// 0.38: {allied} раньше оставался как есть — в летописи стояло «Союз оформлен
					// в игре: {allied}». Теперь пишем словом.
					string allied = evt.Get("allied") == "1"
						? L("RP36_YES", "да")
						: L("RP36_NO", "нет");
					return template.Replace("{a}", evt.Get("a"))
						.Replace("{b}", evt.Get("b"))
						.Replace("{until}", evt.Get("until"))
						.Replace("{years}", evt.Get("years"))
						.Replace("{allied}", allied);
				}
			case PolEventType.KmudReparations:
				{
					string template = L("RP32_CHRON_KMUD_REPARATIONS",
						"КМУД: {payer} платит {payee} по {amount} золота в год — {years} лет.");
					return template.Replace("{payer}", evt.Get("payer"))
						.Replace("{payee}", evt.Get("payee"))
						.Replace("{amount}", evt.Get("amount"))
						.Replace("{years}", evt.Get("years"));
				}
			case PolEventType.KmudExecution:
				{
					string template = L("RP32_CHRON_KMUD_EXECUTION",
						"КМУД: правитель державы {kingdom} — {ruler} — казнён по приговору конференции.");
					return template.Replace("{kingdom}", evt.Get("kingdom"))
						.Replace("{ruler}", evt.Get("ruler"));
				}
			case PolEventType.KmudIdeologyChange:
				{
					string template = L("RP32_CHRON_KMUD_REFORM",
						"КМУД: в державе {kingdom} строй сменён с «{old}» на «{new}» — так решило большинство держав мира.");
					return template.Replace("{kingdom}", evt.Get("kingdom"))
						.Replace("{old}", LocalizedKeyParam(evt, "old"))
						.Replace("{new}", LocalizedKeyParam(evt, "new"));
				}
			case PolEventType.KmudSanctions:
				{
					if (evt.LocKey != null && evt.LocKey.EndsWith("_ENDED", StringComparison.Ordinal))
					{
						return L("RP32_CHRON_KMUD_SANCTIONS_ENDED", "КМУД: санкции против державы {kingdom} истекли.")
							.Replace("{kingdom}", evt.Get("kingdom"));
					}
					string template = L("RP32_CHRON_KMUD_SANCTIONS",
						"КМУД: против державы {kingdom} введены санкции — поддержали {supporters} держав, "
						+ "срок до {until} года. Хозяйство хиреет.");
					return template.Replace("{kingdom}", evt.Get("kingdom"))
						.Replace("{supporters}", evt.Get("supporters"))
						.Replace("{until}", evt.Get("until"));
				}
			case PolEventType.KmudPardoned:
				{
					// 0.39: наказанная держава выходит из списка обвиняемых.
					string count = evt.Get("count");
					string template = L("RP32_CHRON_KMUD_PARDONED",
						"КМУД: держава {kingdom} понесла наказание — обвинения с неё сняты (снято: {count}).");
					return template.Replace("{kingdom}", evt.Get("kingdom"))
						.Replace("{count}", string.IsNullOrEmpty(count) ? "0" : count);
				}
			case PolEventType.KmudAid:
				{
					string key = evt.LocKey ?? string.Empty;
					if (key.EndsWith("ASSOCIATION_ENDED", StringComparison.Ordinal))
					{
						return L("RP32_CHRON_KMUD_ASSOCIATION_ENDED", "КМУД: ассоциация {donor} → {kingdom} завершилась по сроку.")
							.Replace("{donor}", evt.Get("donor")).Replace("{kingdom}", evt.Get("kingdom"));
					}
					if (key.EndsWith("AID_FAILED", StringComparison.Ordinal))
					{
						return L("RP32_CHRON_KMUD_AID_FAILED", "КМУД: помочь державе {kingdom} не удалось — донора не нашлось.")
							.Replace("{kingdom}", evt.Get("kingdom"));
					}
					if (key.EndsWith("AID_ASSOCIATION", StringComparison.Ordinal))
					{
						return L("RP32_CHRON_KMUD_AID_ASSOCIATION",
							"КМУД: держава {donor} ассоциирована с {kingdom} до {until} года: 40% её денег и еды идут нуждающейся стороне.")
							.Replace("{donor}", evt.Get("donor")).Replace("{kingdom}", evt.Get("kingdom")).Replace("{until}", evt.Get("until"));
					}
					if (key.EndsWith("AID_FOOD", StringComparison.Ordinal))
					{
						return L("RP32_CHRON_KMUD_AID_FOOD", "КМУД: державе {kingdom} направлена еда — 60 хлеба в её города.")
							.Replace("{kingdom}", evt.Get("kingdom"));
					}
					return L("RP32_CHRON_KMUD_AID_MONEY", "КМУД: державе {kingdom} направлены деньги — 120 золота в её города.")
						.Replace("{kingdom}", evt.Get("kingdom"));
				}
				default:
					// Сводные и прочие события — шаблон уведомления как есть.
					string text = _notifier?.RenderEventText(evt) ?? string.Empty;
					return text;
			}
		}
		catch (Exception ex)
		{
			LogService.LogError("[Real Politic] летопись: ошибка записи события " + evt.Type + ": " + ex.Message);
			return string.Empty;
		}
	}

	/// <summary>Подробная строка о смерти (без префикса «Год N» и категории).</summary>
	private string DeathLine(DeathRecord rec)
	{
		try
		{
			MapBox map = World.world;
			string text = string.Empty;
			if (rec.WasRuler)
			{
				IdeologyType ideology = _world.GetKingdom(rec.KingdomId ?? -1)?.Ideology ?? IdeologyType.Monarchy;
				text += GameText.Capitalize(L(Ideologies.RulerTitleKey(ideology, rec.WasFemale), "правитель"));
				text += " " + (ReadableEdition ? ChronicleNotebook.Person(rec.ActorName, rec.ActorId) : rec.ActorName);
			}
			else
			{
				text += ReadableEdition ? ChronicleNotebook.Person(rec.ActorName, rec.ActorId) : rec.ActorName;
			}
			if (rec.Age > 0)
			{
				text += " (" + Fmt2("RP_CHRON_AGE", "возраст {age}", "age", rec.Age.ToString()) + ")";
			}
			string kingdom = (rec.KingdomId == null || map == null) ? string.Empty : GameNotifier.KingdomName(map, rec.KingdomId.Value);
			if (!string.IsNullOrEmpty(kingdom))
			{
				string state = StateNameOf(rec.KingdomId.Value);
				text += " — " + kingdom;
				if (!string.IsNullOrEmpty(state))
				{
					text += " (" + state + ")";
				}
			}
			text += ": " + L(rec.LocKeyFor(rec.WasFemale), rec.Cause.ToString());
			if (rec.ReignYears.HasValue)
			{
				text += " " + Fmt2("RP_CHRON_REIGN", "лет правления: {years}", "years", rec.ReignYears.Value.ToString());
			}
			if (!string.IsNullOrEmpty(rec.KillerName) || !string.IsNullOrEmpty(rec.KillerSpecies))
			{
				if (rec.KillerIsBoat)
				{
					// 0.23.12: корабль — судно, а не зверь. «корабль-торговец «Мур»».
					text += " " + Fmt2("RP_CHRON_BOAT", "судно: {boat}", "boat", BoatDetails(rec));
				}
				else if (rec.KillerIsCiv)
				{
					// Разумный убийца: имя, вид сущности, держава, город, пол, возраст —
					// «Кузь (человек, Мысерод, Реин, мужской, 34 года)».
					text += " " + Fmt2("RP_CHRON_KILLER", "убийца: {killer}", "killer", KillerDetails(rec));
				}
				else
				{
					// Зверь: вид, кличка, принадлежность, пол, возраст — «волк (Хрю, вне государств, самец, 4 года)».
					string beast = rec.KillerSpecies;
					if (string.IsNullOrEmpty(beast))
					{
						beast = rec.KillerName;
					}
					List<string> details = new List<string>();
					if (!string.IsNullOrEmpty(rec.KillerName) && !string.IsNullOrEmpty(rec.KillerSpecies))
					{
						details.Add(rec.KillerName);
					}
					details.Add(DeathCauseClassifier.FormatKillerCountry(rec, isBeast: true));
					details.Add(L(rec.KillerFemale ? "RP_SEX_BEAST_F" : "RP_SEX_BEAST_M", rec.KillerFemale ? "самка" : "самец"));
					if (rec.KillerAge > 0)
					{
						details.Add(Fmt2("RP_CHRON_KILLER_AGE", "{age} лет", "age", rec.KillerAge.ToString()));
					}
					if (details.Count > 0)
					{
						beast = beast + " (" + string.Join(", ", details) + ")";
					}
					text += " " + Fmt2("RP_CHRON_BEAST", "зверь: {beast}", "beast", beast);
				}
			}
			if (!string.IsNullOrEmpty(rec.CityName))
			{
				text += " " + Fmt2("RP_CHRON_CITY", "город: {city}", "city", rec.CityName);
			}
			if (!string.IsNullOrEmpty(rec.Note))
			{
				string note = rec.Note.StartsWith("RP_", StringComparison.Ordinal) ? GameText.Localized(rec.Note, rec.Note) : rec.Note;
				text += " [" + note + "]";
			}
			return text;
		}
		catch (Exception ex)
		{
			LogService.LogError("[Real Politic] летопись: ошибка записи смерти: " + ex.Message);
			return rec.ToString();
		}
	}

	/// <summary>Компактный текст убийцы для всплывающих уведомлений и сводок преемственности (с привязкой к стране).</summary>
	public static string KillerShort(DeathRecord rec)
	{
		if (rec == null)
		{
			return string.Empty;
		}
		if (rec.KillerIsBoat)
		{
			// 0.23.12: корабль — судно, а не зверь: «корабль-торговец «Мур» (держава: ...)».
			return BoatDetails(rec);
		}
		List<string> parts = new List<string>();
		if (rec.KillerIsCiv)
		{
			if (!string.IsNullOrEmpty(rec.KillerRace))
			{
				parts.Add(rec.KillerRace);
			}
			parts.Add(DeathCauseClassifier.FormatKillerCountry(rec));
			if (!string.IsNullOrEmpty(rec.KillerCity))
			{
				parts.Add("город: " + rec.KillerCity);
			}
		}
		else if (!string.IsNullOrEmpty(rec.KillerSpecies))
		{
			parts.Add(rec.KillerSpecies);
			parts.Add(DeathCauseClassifier.FormatKillerCountry(rec, isBeast: true));
		}
		if (parts.Count == 0)
		{
			return rec.KillerName;
		}
		return string.IsNullOrEmpty(rec.KillerName) ? string.Join(", ", parts) : (rec.KillerName + " (" + string.Join(", ", parts) + ")");
	}

	/// <summary>Описание корабля-убийцы: роль судна, имя в «кавычках», держава и порт приписки.</summary>
	public static string BoatDetails(DeathRecord rec)
	{
		if (rec == null)
		{
			return string.Empty;
		}
		string kind = DeathRecord.BoatKindLabel(rec.KillerSpecies);
		string display = string.IsNullOrEmpty(kind) ? "корабль" : ("корабль-" + kind);
		string shipLabel = string.IsNullOrEmpty(rec.KillerBoatName)
			? display
			: (display + " " + DeathRecord.QuoteName(rec.KillerBoatName));
		List<string> meta = new List<string> { DeathCauseClassifier.FormatKillerCountry(rec, isBoat: true) };
		if (!string.IsNullOrEmpty(rec.KillerCity))
		{
			meta.Add("порт: " + rec.KillerCity);
		}
		return shipLabel + " (" + string.Join(", ", meta) + ")";
	}

	/// <summary>Полное описание разумного убийцы: вид сущности, государство, город, пол, возраст.</summary>
	public string KillerDetails(DeathRecord rec)
	{
		string killer = rec.KillerName;
		List<string> parts = new List<string>();
		// Раса всегда первой: «Кузь (человек, воин, держава: Мысерод, город: Реин, мужской, 34 года)».
		parts.Add(string.IsNullOrEmpty(rec.KillerRace) ? L("RP_KILLER_CIV_UNKNOWN", "разумное существо") : rec.KillerRace);
		if (!string.IsNullOrEmpty(rec.KillerJob))
		{
			parts.Add(rec.KillerJob);
		}
		parts.Add(DeathCauseClassifier.FormatKillerCountry(rec));
		if (!string.IsNullOrEmpty(rec.KillerCity))
		{
			parts.Add("город: " + rec.KillerCity);
		}
		parts.Add(L(rec.KillerFemale ? "RP_SEX_F" : "RP_SEX_M", rec.KillerFemale ? "женский" : "мужской"));
		if (rec.KillerAge > 0)
		{
			parts.Add(Fmt2("RP_CHRON_KILLER_AGE", "{age} года", "age", rec.KillerAge.ToString()));
		}
		if (parts.Count > 0)
		{
			killer = killer + " (" + string.Join(", ", parts) + ")";
		}
		return killer;
	}

	// ---------- Сводка по годам ----------

	public void WriteYearSummary(int year)
	{
        DrainExtinctions(); DrainBorders();
		// 0.31.10: годовые сводки по государствам и годовая статистика по умолчанию не пишутся.
		// Остаются события: войны, мятежи, смены и смерти правителей и т.д.
		if (!WriteYearSummaries)
		{
			return;
		}
		if (_path == null || _failed)
		{
			return;
		}
		MapBox map = World.world;
		if (map == null)
		{
			return;
		}
		try
		{
			_buffer.Add("");
			string title = (_summaryFromYear > 0 && _summaryFromYear < year)
				? Fmt2("RP_CHRON_SUMMARY_RANGE", "СВОДКА ЗА ГОДЫ {from}–{year}", "from", _summaryFromYear.ToString(), "year", year.ToString())
				: Fmt2("RP_CHRON_SUMMARY", "СВОДКА ЗА ГОД {year}", "year", year.ToString());
			_summaryFromYear = -1;
			string played = _firstYear >= 0 && year >= _firstYear
				? ", " + Fmt2("RP_CHRON_PLAYED", "наиграно с загрузки: {years} лет", "years", (year - _firstYear).ToString())
				: string.Empty;
			_buffer.Add("=== " + title + " (" + NowTime() + played + ") ===");
			List<Kingdom> list = GameRefs.KingdomList(map);
			if (list == null) return;
			int count = 0;
			int popTotal = 0;
            var liveIds = new System.Collections.Generic.HashSet<int>();
			for (int i = 0; i < list.Count; i++)
			{
				Kingdom kingdom = list[i];
				if (kingdom == null)
				{
					continue;
				}
				int id = (int)((NanoObject)kingdom).id;
				int population;
				int warriors;
				string line = KingdomLine(id, kingdom, out population, out warriors);
				if (!string.IsNullOrEmpty(line))
				{
					count++;
					popTotal += population;
					_buffer.Add(line);
                    liveIds.Add(id);
                    if (ReadableEdition)
                    {
                        KingdomPolitics state = _world.GetKingdom(id);
                        Actor ruler = ChronicleEdition.LivingRuler(kingdom);
                        var snapshot = new ChronicleSnapshot { KingdomId = id, Year = year,
                            Population = population, Cities = kingdom.cities?.Count ?? 0,
                            RulerId = ruler == null ? 0 : GameText.ActorId(ruler), RulerName = GameText.ActorName(ruler),
                            Legitimacy = (int)(state?.Legitimacy ?? 0), Ideology = state?.Ideology ?? IdeologyType.Tribalism,
                            Routes = _world.Trade.OpenRoutesOf(id), Starving = state?.Starving ?? false, AtWar = state?.InWar ?? false };
                        string delta = _notebook.Compare(snapshot);
                        if (!string.IsNullOrEmpty(delta)) _buffer.Add("  ↳ " + delta);
                        string context = _notebook.Conditions(snapshot, state, _world.Params);
                        if (!string.IsNullOrEmpty(context)) _buffer.Add("  ℹ " + context);
                    }
				}
			}
			_notebook.Retain(liveIds);
            _buffer.Add(Fmt2("RP_CHRON_TOTALS", "Государств: {kingdoms}. Общее население: {population}.", "kingdoms", count.ToString(), "population", popTotal.ToString()));
			string recap = RecapLine();
			if (!string.IsNullOrEmpty(recap))
			{
				_buffer.Add(recap);
			}
			_buffer.Add(StatsLine());
			UpdateYearBaseline();
		}
		catch (Exception ex)
		{
			LogService.LogError("[Real Politic] летопись: ошибка сводки за год " + year + ": " + ex.Message);
		}
	}

	/// <summary>
	/// Подробная строка-описание одного государства (общая для пролога и годовой сводки):
	/// название, строй с идеологией, население/воины, правитель с титулом и годом восшествия,
	/// легитимность, курс, война/революция, города со столицей и религия/язык/культура,
	/// идеологический состав подданных.
	/// </summary>
	private string KingdomLine(int id, Kingdom kingdom, out int population, out int warriors)
	{
		population = 0;
		warriors = 0;
		try
		{
			try
			{
				population = GameRefs.KingdomPopulation(kingdom);
				warriors = GameRefs.KingdomWarriors(kingdom);
			}
			catch (Exception)
			{
			}
			string line = "• " + ((NanoObject)kingdom).name + " — " + StateNameOf(id) + " (" + L(Ideologies.NameKey(IdeologyOf(id)), IdeologyOf(id).ToString()) + "). ";
			line += Fmt2("RP_CHRON_POP", "Население: {pop}, воинов: {war}.", "pop", population.ToString(), "war", warriors.ToString()) + " ";
			line += RulerLine(id, kingdom) + " ";
			KingdomPolitics kp = _world.GetKingdom(id);
			if (kp != null)
			{
				line += Fmt2("RP_CHRON_LEGIT", "Легитимность: {legit}/100.", "legit", ((int)kp.Legitimacy).ToString()) + " " + L(Ideologies.StanceKey(kp.Stance), kp.Stance.ToString()) + ". ";
				if (kp.InWar)
				{
					line += L("RP_CHRON_AT_WAR", "ВЕДЁТ ВОЙНУ.") + " ";
				}
				if (kp.Revolution != null)
				{
					line += L("RP_CHRON_REVOLUTION", "РЕВОЛЮЦИЯ.") + " ";
				}
			}
			line += CitiesLine(kingdom);
			// Идеологический состав подданных: сколько согласных с королём, сколько нет.
			ActorIdeologies.Summary(id, out int loyalCount, out int dissenters, out float avgObedience);
			if (loyalCount + dissenters > 0)
			{
				line += " " + Fmt2("RP_CHRON_IDEOLOGY_SPLIT",
					"Взгляды подданных: с правителем {loyal}, несогласных {dissent}, среднее послушание {obey}%.",
					"loyal", loyalCount.ToString(),
					"dissent", dissenters.ToString(),
					"obey", ((int)(avgObedience * 100f)).ToString());
			}
			return line;
		}
		catch (Exception ex)
		{
			LogService.LogError("[Real Politic] летопись: ошибка описания государства: " + ex.Message);
			return string.Empty;
		}
	}

	/// <summary>
	/// Строка «итоги года»: разность накопленной статистики мира и «фотографии»,
	/// снятой на момент прошлой сводки. Показывает, что именно произошло за этот год.
	/// </summary>
	private string RecapLine()
	{
		List<string> parts = new List<string>();
		int deaths = _deathsRulers - _yDeathsRulers;
		if (deaths > 0)
		{
			parts.Add(Fmt2("RP_CHRON_STATS_DEATHS", "смертей правителей: {count}", "count", deaths.ToString()));
		}
		int chosen = _deathsChosen - _yDeathsChosen;
		if (chosen > 0)
		{
			parts.Add(L("RP_CHRON_DEATH_CHOSEN", "СМЕРТЬ ИЗБРАННОГО").ToLowerInvariant() + ": " + chosen.ToString());
		}
		int successions = _successions - _ySuccessions;
		if (successions > 0)
		{
			parts.Add(Fmt2("RP_CHRON_STATS_SUCCESSIONS", "смен правителей: {count}", "count", successions.ToString()));
		}
		int adoptions = _adoptions - _yAdoptions;
		if (adoptions > 0)
		{
			parts.Add(Fmt2("RP_CHRON_STATS_ADOPTIONS", "смен строя: {count}", "count", adoptions.ToString()));
		}
		int revolutions = _revolutions - _yRevolutions;
		if (revolutions > 0)
		{
			parts.Add(Fmt2("RP_CHRON_STATS_REVOLUTIONS", "революций: {count}", "count", revolutions.ToString()));
		}
		int diplomacy = _diplomacy - _yDiplomacy;
		if (diplomacy > 0)
		{
			parts.Add(Fmt2("RP_CHRON_STATS_DIPLO", "дипломатических действий: {count}", "count", diplomacy.ToString()));
		}
		int unions = _unions - _yUnions;
		if (unions > 0)
		{
			parts.Add(Fmt2("RP_CHRON_STATS_UNIONS", "событий в униях: {count}", "count", unions.ToString()));
		}
		int warsStarted = _warsStarted - _yWarsStarted;
		int warsEnded = _warsEnded - _yWarsEnded;
		if (warsStarted > 0 || warsEnded > 0)
		{
			parts.Add(Fmt2("RP_CHRON_STATS_WARS", "войн начато: {count}, завершено: {ended}", "count", warsStarted.ToString(), "ended", warsEnded.ToString()));
		}
		int noveltyReligion = _noveltyReligion - _yNoveltyReligion;
		int noveltyLanguage = _noveltyLanguage - _yNoveltyLanguage;
		int noveltyCulture = _noveltyCulture - _yNoveltyCulture;
		if (noveltyReligion + noveltyLanguage + noveltyCulture > 0)
		{
			parts.Add(Fmt2("RP_CHRON_STATS_NOVELTIES", "новых религий: {religion}, языков: {language}, культур: {culture}",
				"religion", noveltyReligion.ToString(), "language", noveltyLanguage.ToString(), "culture", noveltyCulture.ToString()));
		}
		int civilWars = _civilWars - _yCivilWars;
		int secessions = _secessions - _ySecessions;
		if (civilWars > 0 || secessions > 0)
		{
			parts.Add(Fmt2("RP_CHRON_STATS_CIVILWARS", "гражданских войн: {count}, расколов государств: {secessions}",
				"count", civilWars.ToString(), "secessions", secessions.ToString()));
		}
		int crises = _economiesCrises - _yEconomiesCrises;
		int booms = _economiesBooms - _yEconomiesBooms;
		int routes = _tradeRoutesOpened - _yTradeRoutes;
		if (crises > 0 || booms > 0 || routes > 0)
		{
			parts.Add(Fmt2("RP_CHRON_STATS_ECONOMY", "экономических кризисов: {crises}, периодов бума: {booms}, новых торговых путей: {routes}",
				"crises", crises.ToString(), "booms", booms.ToString(), "routes", routes.ToString()));
		}
		int marriages = _marriages - _yMarriages;
		if (marriages > 0)
		{
			parts.Add(Fmt2("RP_CHRON_STATS_MARRIAGES", "династических браков: {count}", "count", marriages.ToString()));
		}
		int cbWars = _cbWars - _yCbWars;
		int treaties = _treaties - _yTreaties;
		int espionage = _espionage - _yEspionage;
		int coalitions = _coalitions - _yCoalitions;
		if (cbWars > 0 || treaties > 0)
		{
			parts.Add(Fmt2("RP_CHRON_STATS_DEEPDIPLO_WAR",
				"войн по казус белли: {cbwars}, договоров: {treaties}",
				"cbwars", cbWars.ToString(), "treaties", treaties.ToString()));
		}
		if (espionage > 0 || coalitions > 0)
		{
			parts.Add(Fmt2("RP_CHRON_STATS_DEEPDIPLO_SPY",
				"тайных операций: {espionage}, коалиций: {coals}",
				"espionage", espionage.ToString(), "coals", coalitions.ToString()));
		}
		int constitutionActs = _constitutionActs - _yConstitutionActs;
		if (constitutionActs > 0)
		{
			parts.Add(Fmt2("RP_CHRON_STATS_CONSTITUTION", "конституционных актов: {acts}", "acts", constitutionActs.ToString()));
		}
		int alliancesFormed = _alliancesFormed - _yAlliancesFormed;
		int alliancesBroken = _alliancesBroken - _yAlliancesBroken;
		int unionCrowns = _unionCrowns - _yUnionCrowns;
		int integrations = _integrations - _yIntegrations;
		if (alliancesFormed > 0 || alliancesBroken > 0)
		{
			parts.Add(Fmt2("RP_CHRON_STATS_ALLIANCES",
				"альянсов создано: {formed}, распалось: {broken}",
				"formed", alliancesFormed.ToString(), "broken", alliancesBroken.ToString()));
		}
		if (unionCrowns > 0 || integrations > 0)
		{
			parts.Add(Fmt2("RP_CHRON_STATS_UNION_DEPTH_YEAR",
				"наследований короны в униях: {crowns}, интеграций завершено: {integr}",
				"crowns", unionCrowns.ToString(), "integr", integrations.ToString()));
		}
		int powers = _powersUsed - _yPowers;
		int threats = _threatsBroke - _yThreats;
		if (powers > 0 || threats > 0)
		{
			parts.Add(Fmt2("RP_CHRON_STATS_MECHANICS", "особых действий: {powers}, внутренних кризисов: {threats}", "powers", powers.ToString(), "threats", threats.ToString()));
		}
		int settlements = _peaceSettlements - _yPeaceSettlements;
		int whitePeaces = _whitePeaces - _yWhitePeaces;
		int trucesBroken = _trucesBroken - _yTrucesBroken;
		int citiesCeded = _citiesCeded - _yCitiesCeded;
		if (settlements > 0 || whitePeaces > 0 || citiesCeded > 0)
		{
			parts.Add(Fmt2("RP_CHRON_STATS_WARPEACE",
				"мирных договоров: {deals}, белых миров: {white}, городов уступлено: {cities}",
				"deals", settlements.ToString(), "white", whitePeaces.ToString(), "cities", citiesCeded.ToString()));
		}
		if (trucesBroken > 0)
		{
			parts.Add(Fmt2("RP_CHRON_STATS_TRUCES", "перемирий нарушено: {broken}", "broken", trucesBroken.ToString()));
		}
		int collapses = _kingdomCollapses - _yKingdomCollapses;
		int collapseFrags = _collapseFragments - _yCollapseFragments;
		if (collapses > 0)
		{
			parts.Add(Fmt2("RP_CHRON_STATS_COLLAPSES", "распадов держав: {count} (всего обломков: {fragments})",
				"count", collapses.ToString(), "fragments", collapseFrags.ToString()));
		}
		int raids = _powerRaids - _yPowerRaids;
		int borderClosures = _borderClosures - _yBorderClosures;
		if (raids > 0 || borderClosures > 0)
		{
			parts.Add(Fmt2("RP_CHRON_STATS_RAIDBORDERS",
				"тайных авиаударов: {raids}, закрытий границ: {borders}",
				"raids", raids.ToString(), "borders", borderClosures.ToString()));
		}
		if (parts.Count == 0)
		{
			return string.Empty;
		}
		return Fmt2("RP_CHRON_RECAP", "За этот год: {parts}", "parts", string.Join("; ", parts));
	}

	/// <summary>Снимает текущие накопленные счётчики в «базу года» (после сводки).</summary>
	private void UpdateYearBaseline()
	{
		_yDeathsRulers = _deathsRulers;
		_yDeathsChosen = _deathsChosen;
		_ySuccessions = _successions;
		_yRevolutions = _revolutions;
		_yAdoptions = _adoptions;
		_yDiplomacy = _diplomacy;
		_yUnions = _unions;
		_yNoveltyReligion = _noveltyReligion;
		_yNoveltyLanguage = _noveltyLanguage;
		_yNoveltyCulture = _noveltyCulture;
		_yWarsStarted = _warsStarted;
		_yWarsEnded = _warsEnded;
		_yPowers = _powersUsed;
		_yThreats = _threatsBroke;
		_yCivilWars = _civilWars;
		_ySecessions = _secessions;
		_yEconomiesCrises = _economiesCrises;
		_yEconomiesBooms = _economiesBooms;
		_yMarriages = _marriages;
		_yTreaties = _treaties;
		_yTreatiesBroken = _treatiesBroken;
		_yEspionage = _espionage;
		_yCoalitions = _coalitions;
		_yCbWars = _cbWars;
		_yConstitutionActs = _constitutionActs;
		_yTradeRoutes = _tradeRoutesOpened;
		_yAlliancesFormed = _alliancesFormed;
		_yAlliancesBroken = _alliancesBroken;
		_yUnionCrowns = _unionCrowns;
		_yIntegrations = _integrations;
		_yPeaceSettlements = _peaceSettlements;
		_yWhitePeaces = _whitePeaces;
		_yTrucesBroken = _trucesBroken;
		_yCitiesCeded = _citiesCeded;
		_yKingdomCollapses = _kingdomCollapses;
		_yCollapseFragments = _collapseFragments;
		_yPowerRaids = _powerRaids;
		_yBorderClosures = _borderClosures;
	}

	/// <summary>Имя предводителя мятежа для текста летописи (если имени нет — обобщённое «повстанцы»).</summary>
	private string FallbackLeader(PolEvent evt)
	{
		string leader = evt.Get("leader");
		return string.IsNullOrEmpty(leader) ? L("RP_CW_LEADER_UNKNOWN", "повстанцы") : leader;
	}

	private IdeologyType IdeologyOf(int kingdomId)
	{
		return _world.GetKingdom(kingdomId)?.Ideology ?? IdeologyType.Monarchy;
	}

	private string StateNameOf(int kingdomId)
	{
		return L(Ideologies.StateNameKey(IdeologyOf(kingdomId)), L(Ideologies.NameKey(IdeologyOf(kingdomId)), IdeologyOf(kingdomId).ToString()));
	}

	private string RulerLine(int kingdomId, Kingdom kingdom)
	{
		IdeologyType ideology = IdeologyOf(kingdomId);
		Actor ruler = null;
		try
		{
			ruler = ChronicleEdition.LivingRuler(kingdom);
		}
		catch (Exception)
		{
		}
		if (ruler == null)
		{
			return L("RP_CHRON_NO_RULER", "Правителя нет.");
		}
		try
		{
			bool female = GameText.ActorIsFemale(ruler);
			string title = GameText.Capitalize(L(Ideologies.RulerTitleKey(ideology, female), "правитель"));
			string name = GameText.ActorName(ruler);
            if (ReadableEdition) name = ChronicleNotebook.Person(name, GameText.ActorId(ruler));
			string line = Fmt2("RP_CHRON_RULER", "Правитель: {ruler}.", "ruler", title + " " + name);
			int age = GameText.ActorAge(ruler);
			if (age > 0)
			{
				line += " (" + Fmt2("RP_CHRON_AGE", "возраст {age}", "age", age.ToString()) + ")";
			}
			string clan = GameRefs.ActorClanName(ruler);
			if (!string.IsNullOrEmpty(clan))
			{
				line += ", " + Fmt2("RP_CHRON_CLAN", "клан «{clan}»", "clan", clan);
			}
			KingdomPolitics kp = _world.GetKingdom(kingdomId);
			RulerRef ref2 = kp?.Ruler;
			if (ref2 != null && ref2.ActorId == GameText.ActorId(ruler) && ref2.SinceYear > 0)
			{
				line += ", " + Fmt2("RP_CHRON_THRONE", "на троне с {year} года", "year", ref2.SinceYear.ToString());
			}
			if (kp?.Regency != null && kp.Regency.IsActive)
			{
				line += " (при регентском совете)";
			}
			if (!line.EndsWith("."))
			{
				line += ".";
			}
			return line;
		}
		catch (Exception)
		{
			return string.Empty;
		}
	}

	private string CitiesLine(Kingdom kingdom)
	{
		try
		{
			List<City> cities = GameRefs.KingdomCities(kingdom);
			if (cities == null || cities.Count == 0)
			{
				return Fmt2("RP_CHRON_CITIES_ONE", "Городов: {count}.", "count", "0");
			}
			City capital = null;
			int best = -1;
			for (int i = 0; i < cities.Count; i++)
			{
				if (cities[i] == null)
				{
					continue;
				}
				int population = 0;
				try
				{
					population = GameRefs.CityPopulation(cities[i]);
				}
				catch (Exception)
				{
				}
				if (population > best)
				{
					best = population;
					capital = cities[i];
				}
			}
			if (capital == null)
			{
				return Fmt2("RP_CHRON_CITIES", "Городов: {count} (", "count", cities.Count.ToString()) + Fmt2("RP_CHRON_CAPITAL", "столица: {city})", "city", "?");
			}
			string line = Fmt2("RP_CHRON_CITIES", "Городов: {count} (", "count", cities.Count.ToString());
			line += Fmt2("RP_CHRON_CAPITAL", "столица: {city})", "city", ((NanoObject)capital).name);
			string religion = GameRefs.CityReligionName(capital);
			string language = GameRefs.CityLanguageName(capital);
			string culture = GameRefs.CityCultureName(capital);
			if (!string.IsNullOrEmpty(religion))
			{
				line += " " + Fmt2("RP_CHRON_RELIGION", "Религия: {name}.", "name", religion);
			}
			if (!string.IsNullOrEmpty(language))
			{
				line += " " + Fmt2("RP_CHRON_LANGUAGE", "Язык: {name}.", "name", language);
			}
			if (!string.IsNullOrEmpty(culture))
			{
				line += " " + Fmt2("RP_CHRON_CULTURE", "Культура: {name}.", "name", culture);
			}
			return line;
		}
		catch (Exception)
		{
			return string.Empty;
		}
	}

	/// <summary>Строка накопленной статистики мира.</summary>
	private string StatsLine()
	{
		List<string> parts = new List<string>();
		string causes = string.Empty;
		foreach (KeyValuePair<string, int> item in _deathsByCause)
		{
			causes += (causes.Length > 0 ? ", " : string.Empty) + item.Key + " — " + item.Value;
		}
		parts.Add(Fmt2("RP_CHRON_STATS_DEATHS", "смертей правителей: {count}", "count", _deathsRulers.ToString()));
		if (causes.Length > 0)
		{
			parts.Add(Fmt2("RP_CHRON_STATS_CAUSES", "причины: {causes}", "causes", causes));
		}
		if (_deathsChosen > 0)
		{
			parts.Add(L("RP_CHRON_DEATH_CHOSEN", "СМЕРТЬ ИЗБРАННОГО").ToLowerInvariant() + ": " + _deathsChosen);
		}
		if (_successions > 0)
		{
			parts.Add(Fmt2("RP_CHRON_STATS_SUCCESSIONS", "смен правителей: {count}", "count", _successions.ToString()));
		}
		if (_adoptions > 0)
		{
			parts.Add(Fmt2("RP_CHRON_STATS_ADOPTIONS", "смен строя: {count}", "count", _adoptions.ToString()));
		}
		if (_revolutions > 0)
		{
			parts.Add(Fmt2("RP_CHRON_STATS_REVOLUTIONS", "революций: {count}", "count", _revolutions.ToString()));
		}
		parts.Add(Fmt2("RP_CHRON_STATS_NOVELTIES", "новых религий: {religion}, языков: {language}, культур: {culture}",
			"religion", _noveltyReligion.ToString(), "language", _noveltyLanguage.ToString(), "culture", _noveltyCulture.ToString()));
		if (_diplomacy > 0)
		{
			parts.Add(Fmt2("RP_CHRON_STATS_DIPLO", "дипломатических действий: {count}", "count", _diplomacy.ToString()));
		}
		if (_unions > 0)
		{
			parts.Add(Fmt2("RP_CHRON_STATS_UNIONS", "событий в униях: {count}", "count", _unions.ToString()));
		}
		parts.Add(Fmt2("RP_CHRON_STATS_WARS", "войн начато: {count}, завершено: {ended}", "count", _warsStarted.ToString(), "ended", _warsEnded.ToString()));
		if (_civilWars > 0 || _secessions > 0)
		{
			parts.Add(Fmt2("RP_CHRON_STATS_CIVILWARS", "гражданских войн: {count}, расколов государств: {secessions}",
				"count", _civilWars.ToString(), "secessions", _secessions.ToString()));
		}
		if (_economiesCrises > 0 || _economiesBooms > 0 || _tradeRoutesOpened > 0)
		{
			parts.Add(Fmt2("RP_CHRON_STATS_ECONOMY", "экономических кризисов: {crises}, периодов бума: {booms}, новых торговых путей: {routes}",
				"crises", _economiesCrises.ToString(), "booms", _economiesBooms.ToString(), "routes", _tradeRoutesOpened.ToString()));
		}
		if (_marriages > 0)
		{
			parts.Add(Fmt2("RP_CHRON_STATS_MARRIAGES", "династических браков: {count}", "count", _marriages.ToString()));
		}
		if (_cbWars > 0 || _treaties > 0)
		{
			parts.Add(Fmt2("RP_CHRON_STATS_DEEPDIPLO_WAR",
				"войн по казус белли: {cbwars}, договоров: {treaties}",
				"cbwars", _cbWars.ToString(), "treaties", _treaties.ToString()));
		}
		if (_espionage > 0 || _coalitions > 0)
		{
			parts.Add(Fmt2("RP_CHRON_STATS_DEEPDIPLO_SPY",
				"тайных операций: {espionage}, коалиций: {coals}",
				"espionage", _espionage.ToString(), "coals", _coalitions.ToString()));
		}
		if (_alliancesFormed > 0 || _alliancesBroken > 0)
		{
			parts.Add(Fmt2("RP_CHRON_STATS_ALLIANCES",
				"альянсов создано: {formed}, распалось: {broken}",
				"formed", _alliancesFormed.ToString(), "broken", _alliancesBroken.ToString()));
		}
		if (_unionCrowns > 0 || _integrations > 0 || _unionReforms > 0)
		{
			parts.Add(Fmt2("RP_CHRON_STATS_UNION_DEPTH",
				"наследований короны в униях: {crowns}, интеграций завершено: {integr}, полных уний: {reforms}",
				"crowns", _unionCrowns.ToString(), "integr", _integrations.ToString(), "reforms", _unionReforms.ToString()));
		}
		if (_powersUsed > 0 || _threatsBroke > 0)
		{
			parts.Add(Fmt2("RP_CHRON_STATS_MECHANICS", "особых действий: {powers}, внутренних кризисов: {threats}", "powers", _powersUsed.ToString(), "threats", _threatsBroke.ToString()));
		}
		return L("RP_CHRON_STATS", "СТАТИСТИКА") + ": " + string.Join("; ", parts) + ".";
	}

	private void ResetStats()
	{
		_deathsRulers = 0;
		_deathsChosen = 0;
		_deathsByCause.Clear();
		_successions = 0;
		_revolutions = 0;
		_adoptions = 0;
		_diplomacy = 0;
		_unions = 0;
		_noveltyReligion = 0;
		_noveltyLanguage = 0;
		_noveltyCulture = 0;
		_warsStarted = 0;
		_powersUsed = 0;
		_threatsBroke = 0;
		_warsEnded = 0;
		_yDeathsRulers = 0;
		_yDeathsChosen = 0;
		_ySuccessions = 0;
		_yRevolutions = 0;
		_yAdoptions = 0;
		_yDiplomacy = 0;
		_yUnions = 0;
		_yNoveltyReligion = 0;
		_yNoveltyLanguage = 0;
		_yNoveltyCulture = 0;
		_yWarsStarted = 0;
		_yWarsEnded = 0;
		_yPowers = 0;
		_yThreats = 0;
		_civilWars = 0;
		_secessions = 0;
		_yCivilWars = 0;
		_ySecessions = 0;
		_economiesCrises = 0;
		_economiesBooms = 0;
		_marriages = 0;
		_yMarriages = 0;
		_treaties = 0;
		_yTreaties = 0;
		_treatiesBroken = 0;
		_yTreatiesBroken = 0;
		_espionage = 0;
		_yEspionage = 0;
		_coalitions = 0;
		_yCoalitions = 0;
		_cbWars = 0;
		_yCbWars = 0;
		_tradeRoutesOpened = 0;
		_yEconomiesCrises = 0;
		_yEconomiesBooms = 0;
		_yTradeRoutes = 0;
		_alliancesFormed = 0;
		_yAlliancesFormed = 0;
		_alliancesBroken = 0;
		_yAlliancesBroken = 0;
		_peaceSettlements = 0;
		_yPeaceSettlements = 0;
		_whitePeaces = 0;
		_yWhitePeaces = 0;
		_trucesBroken = 0;
		_yTrucesBroken = 0;
		_citiesCeded = 0;
		_yCitiesCeded = 0;
		_kingdomCollapses = 0;
		_yKingdomCollapses = 0;
		_collapseFragments = 0;
		_yCollapseFragments = 0;
		_powerRaids = 0;
		_yPowerRaids = 0;
		_borderClosures = 0;
		_yBorderClosures = 0;
		_unionCrowns = 0;
		_yUnionCrowns = 0;
		_institutions = 0;
		_integrations = 0;
		_yIntegrations = 0;
		_unionReforms = 0;
	}

	// ---------- Запись на диск ----------

	public void Flush()
	{
		EnsureWorldBinding();
		if (_path == null || _failed)
		{
			return;
		}
		// Core.Year is incremented at the end of YearTick. Snapshot headers
        // must use the game's current year, not the next simulation year.
        MapBox activeMap = World.world;
        if (!ReferenceEquals(activeMap, _editionMap))
        {
            if (_editionMap != null) BeginObservation();
            else { DrainExtinctions(); DrainBorders(); _notebook.Clear(); }
            _editionMap = activeMap;
        }
        int reportYear = RealPoliticMod.CurrentYear();
        if (reportYear < 0) reportYear = _world.Year;
        float unscaledTime = Time.unscaledTime;
		// 0.23.3: смену игрового года проверяем каждый кадр, а не раз в 2 секунды —
		// на высокой скорости за 2 секунды проходит несколько лет, и сводки «проглатывались».
		bool yearChanged = _lastYear >= 0 && reportYear != _lastYear;
		if (unscaledTime < _nextFlush && !yearChanged)
		{
			return;
		}
		_nextFlush = unscaledTime + 2f;
		try
		{
			if (_lastYear >= 0 && reportYear < _lastYear)
			{
				// Год откатился — начался новый мир: итоговая сводка, разделитель с датой и новый пролог.
				WriteYearSummary(_lastYear);
				WriteNewWorldDivider();
				ResetStats();
                _notebook.Clear();
                _extinctions.Clear();
				_lastSummaryYear = -1;
				_firstYear = -1;
				_prologueWritten = false;
			}
			_lastYear = reportYear;
			if (!_prologueWritten)
			{
				WritePrologue(reportYear);
				_prologueWritten = true;
			}
			if (_lastSummaryYear < 0)
			{
				_lastSummaryYear = reportYear;
				if (_firstYear < 0)
				{
					_firstYear = reportYear;
				}
			}
			else if (reportYear - _lastSummaryYear >= SummaryEveryYears)
			{
				// Сводка описывает состояние мира на текущий год; если пропущено несколько лет
				// (очень высокая скорость), в заголовке будет диапазон «за годы A–B».
				_summaryFromYear = _lastSummaryYear + 1;
				WriteYearSummary(reportYear);
				_lastSummaryYear = reportYear;
			}
			if (_extinctions.Year >= 0 && (reportYear > _extinctions.Year || !ReadableEdition || !GroupExtinctions)) DrainExtinctions();
            if (_borderDigest.Year >= 0 && (reportYear > _borderDigest.Year || !ReadableEdition || !GroupBorders)) DrainBorders();
            if (_buffer.Count > 0)
			{
				AppendBuffer();
				_buffer.Clear();
			}
		}
		catch (Exception ex)
		{
			_failed = true;
			LogService.LogError("[Real Politic] не удалось записать летопись: " + ex.Message);
		}
	}

    /// <summary>
    /// 0.31.8/0.31.11: каждый мир пишет в свою папку worlds/&lt;имя&gt;/&lt;имя&gt;.txt.
    /// Мир различается по постоянному ключу (WorldKey), он записан внутри папки в world-key.txt.
    /// Папка и файл создаются только тогда, когда в мире действительно появилось событие,
    /// поэтому пустой стартовый мир (его WorldBox создаёт сам) папки не оставляет.
    /// </summary>
    private void EnsureWorldBinding()
    {
        string key = _world?.Governance?.WorldKey;
        if (string.IsNullOrEmpty(key) || key == _boundWorldKey || _baseFolder == null)
        {
            return;
        }
        // 0.48: временный ключ «universal-…» заменился настоящим «native-…» — это тот же
        // мир, и папку менять нельзя. Иначе летопись одного мира разрывается на две папки
        // (именно это владелец и прислал 29.10.2026).
        if (RealPolitic.Core.WorldKeyRules.IsSameWorldUpgrade(_boundWorldKey, key))
        {
            try
            {
                AppendBuffer();
                _boundWorldKey = key;
                _worldKeyValue = key;
                // Перезаписываем ключ в папке, чтобы в следующем сеансе мир нашли по настоящему ключу.
                if (_worldKeyFile != null && _path != null)
                {
                    string folder = System.IO.Path.GetDirectoryName(_path);
                    if (folder != null && Directory.Exists(folder))
                    {
                        File.WriteAllText(_worldKeyFile, key, _encoding);
                    }
                }
                LogService.LogInfo("[Real Politic] ключ мира уточнён (папка летописи та же): " + key);
            }
            catch (Exception ex)
            {
                LogService.LogError("[Real Politic] не удалось уточнить ключ мира: " + ex.Message);
            }
            return;
        }
        try
        {
            // Накопленное для предыдущего мира дописываем в его файл (если там что-то было).
            AppendBuffer();
            string name = WorldFolderName(key);
            string folder = System.IO.Path.Combine(_baseFolder, "worlds", name);
            _path = System.IO.Path.Combine(folder, name + ".txt");
            _worldKeyFile = System.IO.Path.Combine(folder, "world-key.txt");
            _worldKeyValue = key;
            _boundWorldKey = key;
            _headerPending = true;
            _failed = false;
            _lastYear = -1;
            _lastSummaryYear = -1;
            _firstYear = -1;
            LogService.LogInfo("[Real Politic] летопись мира: " + _path);
        }
        catch (Exception ex)
        {
            _failed = true;
            LogService.LogError("[Real Politic] не удалось открыть летопись мира: " + ex.Message);
        }
    }

    private string _worldKeyFile;
    private string _worldKeyValue;
    private bool _headerPending;

    /// <summary>Пишет накопленные строки в файл мира; при первой записи создаёт папку и заголовок.</summary>
    private void AppendBuffer()
    {
        if (_path == null || _failed || _buffer.Count == 0)
        {
            return;
        }
        if (_headerPending)
        {
            string directory = System.IO.Path.GetDirectoryName(_path);
            Directory.CreateDirectory(directory);
            File.WriteAllText(_worldKeyFile, _worldKeyValue, _encoding);
            bool continuing = File.Exists(_path);
            var tail = new List<string>(_buffer);
            _buffer.Clear();
            WriteFileHeader();
            if (continuing)
            {
                _buffer.Add("=== " + L("RP_CHRON_NEW_SESSION", "ПРОДОЛЖЕНИЕ ЗАПИСИ") + " (" + NowDate() + " " + NowTime() + ") ===");
            }
            else
            {
                _buffer.Add(L("RP_CHRON25_GUIDE", "ЛЕТОПИСЬ МИРА: события отражают политическую и дипломатическую историю государств; ↳ — перемены с прошлого обзора, ℹ — внутреннее состояние державы."));
            }
            _buffer.AddRange(tail);
            _headerPending = false;
        }
        File.AppendAllLines(_path, _buffer, _encoding);
        _buffer.Clear();
    }

    /// <summary>
    /// Имя папки мира: «Мир ГГГГ-ММ-ДД ЧЧ-ММ <хвост ключа>». Если в папке уже лежит world-key.txt
    /// с этим ключом (мир продолжили в новом сеансе), возвращаем её имя, чтобы дата не менялась.
    /// </summary>
    private string WorldFolderName(string key)
    {
        string worldsRoot = System.IO.Path.Combine(_baseFolder, "worlds");
        string tail = KeyTail(key);
        try
        {
            if (Directory.Exists(worldsRoot))
            {
                string[] dirs = Directory.GetDirectories(worldsRoot);
                // Первый проход: точное совпадение ключа в world-key.txt.
                foreach (string dir in dirs)
                {
                    string keyFile = System.IO.Path.Combine(dir, "world-key.txt");
                    if (File.Exists(keyFile) && ReadKeyFile(keyFile) == key)
                    {
                        return System.IO.Path.GetFileName(dir);
                    }
                }
                // 0.48.1: второй проход — по «хвосту» ключа в имени папки.
                // Владелец 29.10.2026: один и тот же мир (universal-1) лёг в ЧЕТЫРЕ
                // папки — 17:07, 17:59, 20:39 — потому что точное сравнение почему-то
                // не срабатывало, и каждая сессия начинала новый файл. История мира
                // рассыпалась по папкам, и в самой свежей был только конец.
                // Теперь, если папка с таким же «хвостом» уже есть, продолжаем в неё:
                // лучше дописать историю в соседнюю папку, чем начать новую с нуля.
                string best = null;
                DateTime bestTime = DateTime.MinValue;
                foreach (string dir in dirs)
                {
                    string name = System.IO.Path.GetFileName(dir);
                    if (!name.EndsWith(" " + tail, StringComparison.Ordinal))
                    {
                        continue;
                    }
                    string keyFile = System.IO.Path.Combine(dir, "world-key.txt");
                    // Папку с ДРУГИМ известным ключом не трогаем: это точно другой мир.
                    if (File.Exists(keyFile))
                    {
                        string stored = ReadKeyFile(keyFile);
                        if (!string.IsNullOrEmpty(stored) && stored != key
                            && !RealPolitic.Core.WorldKeyRules.IsSameWorldUpgrade(stored, key)
                            && !RealPolitic.Core.WorldKeyRules.IsSameWorldUpgrade(key, stored))
                        {
                            continue;
                        }
                    }
                    DateTime written = Directory.GetLastWriteTime(dir);
                    if (written > bestTime)
                    {
                        bestTime = written;
                        best = name;
                    }
                }
                if (best != null)
                {
                    LogService.LogInfo("[Real Politic] летопись: продолжаем папку мира по хвосту ключа «"
                        + tail + "» — " + best);
                    return best;
                }
            }
        }
        catch (Exception ex)
        {
            // Не получилось прочитать папки — создадим новое имя ниже.
            LogService.LogInfo("[Real Politic] летопись: поиск папки мира не удался: " + ex.Message);
        }
        string stamp = DateTime.Now.ToString("yyyy-MM-dd HH-mm", System.Globalization.CultureInfo.InvariantCulture);
        return "Мир " + stamp + " " + tail;
    }

    /// <summary>Хвост ключа мира для имени папки: последние шесть букв и цифр ключа.</summary>
    private static string KeyTail(string key)
    {
        var tailChars = new System.Text.StringBuilder();
        if (string.IsNullOrEmpty(key))
        {
            return string.Empty;
        }
        for (int i = key.Length - 1; i >= 0 && tailChars.Length < 6; i--)
        {
            if (char.IsLetterOrDigit(key[i]))
            {
                tailChars.Insert(0, key[i]);
            }
        }
        return tailChars.ToString();
    }

    /// <summary>Читает world-key.txt без BOM и лишних пробелов (в старых папках файл писался иначе).</summary>
    private string ReadKeyFile(string path)
    {
        try
        {
            string raw = File.ReadAllText(path, _encoding);
            return raw.Trim().TrimStart('\uFEFF').Trim();
        }
        catch (Exception)
        {
            return string.Empty;
        }
    }

    /// <summary>Finish the current batch on normal game exit, even if its year has not ended.</summary>
    public void Finish()
    {
        if (_path == null || _failed) return;
        try
        {
            DrainExtinctions(); DrainBorders();
            if (_buffer.Count > 0) { AppendBuffer(); _buffer.Clear(); }
        }
        catch (Exception ex) { LogService.LogError("[Real Politic] завершение летописи: " + ex.Message); }
    }

    public void BeginObservation()
    {
        DrainExtinctions(); DrainBorders();
        _notebook.Clear();
        ResetStats();
        _lastYear = -1; _lastSummaryYear = -1; _firstYear = -1;
        _prologueWritten = false;
        _buffer.Add(L("RP_CHRON25_NEW_SESSION", "--- Новый сеанс наблюдений: сравнения начинаются заново; прошлые события не воспроизводятся. ---"));
    }

    private void DrainBorders()
    {
        int year = _borderDigest.Year;
        foreach (string line in _borderDigest.Drain()) _buffer.Add(YearPrefix(year) + " [" + L("RP_CHRON251_BORDERS", "ГРАНИЦЫ: ИТОГИ") + "] " + line);
    }

    private void DrainExtinctions()
    {
        int year = _extinctions.Year;
        foreach (string line in _extinctions.Drain()) _buffer.Add(YearPrefix(year) + " [" + L("RP_CHRON25_NATURE", "ПРИРОДА: ИТОГИ") + "] " + line);
    }

	// ---------- Вспомогательное ----------

	private static string L(string key, string fallback)
	{
		return GameText.Localized(key, fallback);
	}

	/// <summary>Шаблон (локализованный) + подстановка параметров события (без мутации «state»/«title» — они подставляются вручную).</summary>
	private static string Fmt(string key, string fallbackTemplate, PolEvent evt)
	{
		return evt.Render(L(key, fallbackTemplate));
	}

	/// <summary>Шаблон вида «Государство {kingdom} ({state}): … {kind}» с именительным падежом государства.</summary>
	private string StateLine(string key, string fallbackTemplate, PolEvent evt, string kindParam)
	{
		string template = StateNaming.DropFormIfTitled(L(key, fallbackTemplate), evt.Get("kingdom"));
		return template.Replace("{kingdom}", evt.Get("kingdom"))
			.Replace("{state}", StateNameOf(evt.KingdomId ?? -1))
			.Replace("{" + kindParam + "}", evt.Get(kindParam));
	}

	/// <summary>Параметр-ключ RP_* → локализованное значение (если это ещё ключ).</summary>
	private static string LocalizedKeyParam(PolEvent evt, string param)
	{
		string value = evt.Get(param);
		if (string.IsNullOrEmpty(value))
		{
			return string.Empty;
		}
		string fallback = value switch
		{
			"RP_WARWINNER_ATTACKERS" => "победили атакующие",
			"RP_WARWINNER_DEFENDERS" => "победили защищающиеся",
			"RP_WARWINNER_PEACE" => "мир",
			"RP_WARWINNER_MERGED" => "объединение",
			"RP_WARWINNER_NOBODY" => "без победителя",
			_ => CivicText.Fallback(value)
		};
		return (value.StartsWith("RP_", StringComparison.Ordinal) || value.StartsWith("RP26_",StringComparison.Ordinal)) ? L(value, fallback) : value;
	}

	/// <summary>Шаблон (локализованный) + подстановка отдельных значений (до 3 пар).</summary>
	private static string Fmt2(string key, string fallbackTemplate, string k1, string v1, string k2 = null, string v2 = null, string k3 = null, string v3 = null)
	{
		string template = L(key, fallbackTemplate);
		template = template.Replace("{" + k1 + "}", v1 ?? string.Empty);
		if (k2 != null)
		{
			template = template.Replace("{" + k2 + "}", v2 ?? string.Empty);
		}
		if (k3 != null)
		{
			template = template.Replace("{" + k3 + "}", v3 ?? string.Empty);
		}
		return template;
	}

	/// <summary>Титул из параметра события (ключ RP_TITLE_* → локализованный, с заглавной).</summary>
	private static string CapitalizedTitle(PolEvent evt)
	{
		string title = evt.Get("title");
		if (string.IsNullOrEmpty(title))
		{
			return string.Empty;
		}
		if (title.StartsWith("RP_", StringComparison.Ordinal))
		{
			title = L(title, title);
		}
		return GameText.Capitalize(title);
	}

	/// <summary>Локализуем параметры-ключи (если ещё не локализованы) — как в GameNotifier.</summary>
	private static void LocalizeKeyParams(PolEvent evt)
	{
		List<string> keys = new List<string>(evt.Params.Keys);
		for (int i = 0; i < keys.Count; i++)
		{
			string key = keys[i];
			string value = evt.Params[key];
			if (value != null && (value.StartsWith("RP_", StringComparison.Ordinal) || CivicText.IsLocalizationKey(value)))
			{
				evt.Params[key] = L(value, CivicText.Fallback(value));
			}
		}
	}

	/// <summary>Параметр-идентификатор государства (leader/member) → имя государства.</summary>
	private static void ResolveKingdomIdParam(PolEvent evt, MapBox map, string param)
	{
		if (!evt.Params.TryGetValue(param, out var value) || string.IsNullOrEmpty(value) || map == null)
		{
			return;
		}
		if (!int.TryParse(value, out int id) || id <= 0)
		{
			return;
		}
		evt.Params[param] = GameNotifier.KingdomName(map, id);
	}
}
