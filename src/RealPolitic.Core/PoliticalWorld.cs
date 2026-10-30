using System;
using System.Collections.Generic;

namespace RealPolitic.Core;

public sealed class PoliticalWorld
{
    public PoliticalWorld(){Trade.ContactGate=(a,b)=>Governance.Recognition.CanContact(a,b,InternationalContact.Trade);}
    public ChronicleCausality ChronicleCauses { get; } = new ChronicleCausality();

	private readonly Dictionary<int, KingdomPolitics> _kingdoms = new Dictionary<int, KingdomPolitics>();

	private readonly Dictionary<int, CityPolitics> _cities = new Dictionary<int, CityPolitics>();

	private readonly Dictionary<int, ActorEntry> _actors = new Dictionary<int, ActorEntry>();

	private readonly Dictionary<int, List<int>> _actorsByCity = new Dictionary<int, List<int>>();

	private readonly Dictionary<int, int> _religionOrigin = new Dictionary<int, int>();

	private readonly Dictionary<int, int> _languageOrigin = new Dictionary<int, int>();

	private readonly Dictionary<int, int> _cultureOrigin = new Dictionary<int, int>();

	private readonly Dictionary<int, int> _lastRuler = new Dictionary<int, int>();

	/// <summary>Последние ссылки на правителей по государству (для деталей о предыдущем правителе в событии преемственности).</summary>
	private readonly Dictionary<int, RulerRef> _lastRulerRef = new Dictionary<int, RulerRef>();

	private readonly List<int> _neighborScratch = new List<int>();

	private readonly List<Union> _unions = new List<Union>();

	private readonly Dictionary<int, EconomyReport> _economyReports = new Dictionary<int, EconomyReport>();

	private readonly Dictionary<int, string> _kingdomNames = new Dictionary<int, string>();

	private readonly Dictionary<int, string> _pendingRenameOverrides = new Dictionary<int, string>();

	private readonly Dictionary<int, string> _annexedBy = new Dictionary<int, string>();

	/// <summary>0.49: национализм по державам — кто чего хочет вернуть и насколько сильно.</summary>
	private readonly Dictionary<int, NationalismState> _nationalism = new Dictionary<int, NationalismState>();

	/// <summary>0.49: состояние национализма державы (создаётся по требованию).</summary>
	public NationalismState NationalismOf(int kingdomId)
	{
		if (kingdomId <= 0)
		{
			return null;
		}
		if (!_nationalism.TryGetValue(kingdomId, out NationalismState state))
		{
			state = new NationalismState();
			_nationalism[kingdomId] = state;
		}
		return state;
	}

	/// <summary>0.49: все записи о национализме (для сохранения).</summary>
	public IReadOnlyDictionary<int, NationalismState> Nationalism => _nationalism;

	/// <summary>0.49.1: расовый национализм по державам — чужая раса, которая рвётся к власти.</summary>
	private readonly Dictionary<int, RaceNationalismState> _raceNationalism = new Dictionary<int, RaceNationalismState>();

	/// <summary>0.49.1: кому держава отозвала гражданство и до какого года закрыт въезд.</summary>
	private readonly List<CitizenshipBan> _citizenshipBans = new List<CitizenshipBan>();

	/// <summary>0.49.1: состояние расового движения державы (создаётся по требованию).</summary>
	public RaceNationalismState RaceMovementOf(int kingdomId)
	{
		if (kingdomId <= 0)
		{
			return null;
		}
		if (!_raceNationalism.TryGetValue(kingdomId, out RaceNationalismState state))
		{
			state = new RaceNationalismState();
			_raceNationalism[kingdomId] = state;
		}
		return state;
	}

	/// <summary>0.49.1: все записи расового национализма (для сохранения).</summary>
	public IReadOnlyDictionary<int, RaceNationalismState> RaceNationalism => _raceNationalism;

	/// <summary>0.49.1: все запреты на гражданство и въезд.</summary>
	public IReadOnlyList<CitizenshipBan> CitizenshipBans => _citizenshipBans;

	/// <summary>0.52: народы держав — кто пришёл, когда и на каком языке говорит.</summary>
	private readonly Dictionary<int, Dictionary<string, PeopleRecord>> _peoples
		= new Dictionary<int, Dictionary<string, PeopleRecord>>();

	/// <summary>0.52: местные конституции по городам.</summary>
	private readonly Dictionary<int, LocalConstitution> _localConstitutions = new Dictionary<int, LocalConstitution>();

	/// <summary>0.52: народы державы (создаётся по требованию).</summary>
	public Dictionary<string, PeopleRecord> PeoplesOf(int kingdomId)
	{
		if (kingdomId <= 0)
		{
			return null;
		}
		if (!_peoples.TryGetValue(kingdomId, out Dictionary<string, PeopleRecord> book))
		{
			book = new Dictionary<string, PeopleRecord>();
			_peoples[kingdomId] = book;
		}
		return book;
	}

	/// <summary>0.52: все народы всех держав (для сохранения).</summary>
	public IReadOnlyDictionary<int, Dictionary<string, PeopleRecord>> Peoples => _peoples;

	/// <summary>0.53: законопроекты по державам.</summary>
	private readonly Dictionary<int, Bill> _bills = new Dictionary<int, Bill>();

	/// <summary>0.53: год, когда палаты последний раз закончили дело — новое берут не сразу.</summary>
	private readonly Dictionary<int, int> _lastBillYear = new Dictionary<int, int>();

	/// <summary>0.53: сколько лет подряд палаты не могут ничего принять.</summary>
	private readonly Dictionary<int, int> _deadlockSince = new Dictionary<int, int>();

	/// <summary>0.53: законопроект державы (null — ничего не обсуждается).</summary>
	public Bill BillOf(int kingdomId)
	{
		return _bills.TryGetValue(kingdomId, out Bill bill) ? bill : null;
	}

	/// <summary>0.53: положить законопроект (загрузка сохранения).</summary>
	public void SetBill(Bill bill)
	{
		if (bill != null && bill.KingdomId > 0)
		{
			_bills[bill.KingdomId] = bill;
		}
	}

	/// <summary>0.53: все законопроекты (для сохранения и окна).</summary>
	public IReadOnlyDictionary<int, Bill> Bills => _bills;

	/// <summary>0.53: сколько лет палаты в ступоре (0 — не в ступоре).</summary>
	public int DeadlockYearsOf(int kingdomId)
	{
		return _deadlockSince.TryGetValue(kingdomId, out int since) && since > 0 ? Math.Max(0, Year - since) : 0;
	}

	/// <summary>0.52: о каком чужом правителе уже сказали (чтобы не повторять каждый год).</summary>
	private readonly Dictionary<int, int> _foreignRulerNoted = new Dictionary<int, int>();

	/// <summary>0.52: последние известные имена городов (для уведомлений и окна).</summary>
	private readonly Dictionary<int, string> _cityNames = new Dictionary<int, string>();

	/// <summary>0.52: имя города; если игра его ещё не назвала — «город #12».</summary>
	public string CityNameOf(int cityId)
	{
		if (_cityNames.TryGetValue(cityId, out string name) && !string.IsNullOrWhiteSpace(name))
		{
			return name;
		}
		return "город #" + cityId;
	}

	/// <summary>0.52: все местные конституции (по номеру города).</summary>
	public IReadOnlyDictionary<int, LocalConstitution> LocalConstitutions => _localConstitutions;

	public LocalConstitution LocalConstitutionOf(int cityId)
	{
		return _localConstitutions.TryGetValue(cityId, out LocalConstitution local) ? local : null;
	}

	/// <summary>0.52: устав города написан.</summary>
	public LocalConstitution AdoptLocalConstitution(int cityId, int kingdomId, string raceId, string raceName)
	{
		if (cityId <= 0 || kingdomId <= 0 || string.IsNullOrEmpty(raceId))
		{
			return null;
		}
		LocalConstitution local = new LocalConstitution
		{
			CityId = cityId,
			KingdomId = kingdomId,
			RaceId = raceId,
			RaceName = raceName ?? string.Empty,
			FoundedYear = Year,
			WantsLanguage = true,
			WantsCourt = true
		};
		_localConstitutions[cityId] = local;
		return local;
	}

	/// <summary>0.52: устав города снят вместе с городом.</summary>
	public void DropLocalConstitution(int cityId)
	{
		_localConstitutions.Remove(cityId);
	}

	/// <summary>0.50: год, когда держава отняла гражданство у чужих (0 — не отнимала).</summary>
	private readonly Dictionary<int, int> _hateStains = new Dictionary<int, int>();

	/// <summary>0.50: все пятна (для сохранения).</summary>
	public IReadOnlyDictionary<int, int> HateStains => _hateStains;

	/// <summary>0.50: запомнить, что держава отняла гражданство у чужих.</summary>
	public void NoteHateStain(int kingdomId, int year)
	{
		if (kingdomId > 0)
		{
			_hateStains[kingdomId] = year;
		}
	}

	/// <summary>
	/// 0.50: свежее ли пятно. Пятьдесят лет — это примерно два поколения:
	/// мир помнит, но не вечно.
	/// </summary>
	public bool HasFreshHateStain(int kingdomId, int year)
	{
		return _hateStains.TryGetValue(kingdomId, out int when)
			&& when > 0 && year - when <= OutrageEngine.StainYears;
	}

	/// <summary>
	/// 0.50: мир возмущается и наказывает.
	///
	/// Владелец 29.10.2026, ответ по национализму: «мир возмущается и наказывает
	/// (санкции, КМУД, потеря статуса великой державы)».
	///
	/// Что делает этот метод:
	///   1) объявляет санкции — благосостояние державы падает каждый год, пока они идут;
	///   2) портит отношения её правителя со всеми остальными;
	///   3) если проступок тяжкий (отняли гражданство) — снимает звание великой державы;
	///   4) оставляет пятно: на ближайшем съезде КМУД державу назовут обвиняемой;
	///   5) пишет об этом в уведомления и летопись.
	/// Возвращает то, что решил мир.
	/// </summary>
	public OutrageResponse PunishOutrage(int kingdomId, float pressure, bool citizenshipRevoked, string reasonKey)
	{
		if (kingdomId <= 0)
		{
			return OutrageResponse.None;
		}
		float level = OutrageEngine.Level(pressure, citizenshipRevoked);
		OutrageResponse response = OutrageEngine.Response(level, citizenshipRevoked, Kmud.WasHeld);
		if (response == OutrageResponse.None)
		{
			return response;
		}
		// 1. Санкции. Если они уже идут — продлеваем, а не дублируем.
		int years = OutrageEngine.SanctionFor(response, citizenshipRevoked);
		int until = Year + years;
		int supporters = 0;
		foreach (KingdomPolitics other in Kingdoms)
		{
			if (other != null && other.KingdomId != kingdomId)
			{
				supporters++;
			}
		}
		KmudSanction existing = null;
		for (int i = 0; i < Kmud.Sanctions.Count; i++)
		{
			if (Kmud.Sanctions[i].KingdomId == kingdomId)
			{
				existing = Kmud.Sanctions[i];
				break;
			}
		}
		if (existing != null)
		{
			existing.UntilYear = Math.Max(existing.UntilYear, until);
			existing.Supporters = Math.Max(existing.Supporters, supporters);
		}
		else
		{
			Kmud.Sanctions.Add(new KmudSanction
			{
				KingdomId = kingdomId,
				UntilYear = until,
				Supporters = supporters
			});
		}
		// 2. Отношения правителей со всеми портятся.
		int hit = OutrageEngine.RelationHitFor(response, citizenshipRevoked);
		if (hit > 0)
		{
			foreach (KingdomPolitics other in Kingdoms)
			{
				if (other != null && other.KingdomId != kingdomId)
				{
					ChangeRulerRelation(kingdomId, other.KingdomId, -hit);
				}
			}
		}
		// 4. Пятно: на съезде КМУД державу назовут обвиняемой.
		if (citizenshipRevoked)
		{
			NoteHateStain(kingdomId, Year);
		}
		PolEvent sanctions = new PolEvent(PolEventType.WorldOutrageSanctions, "RP50_EV_OUTRAGE_SANCTIONS")
		{ Year = Year, KingdomId = kingdomId, Severity = Severity.Major };
		sanctions.With("kingdom", KingdomNameOf(kingdomId))
			.With("years_word", years + (years == 1 ? " год" : (years < 5 ? " года" : " лет")))
			.With("until", until)
			.With("supporters", supporters)
			.With("reason", reasonKey);
		Bus.Publish(sanctions);
		// 3. Потеря звания великой державы.
		if (OutrageEngine.LosesGreatPower(response) && Governance.Recognition.StripGreatPower(kingdomId))
		{
			PolEvent lost = new PolEvent(PolEventType.WorldOutrageStatusLost, "RP50_EV_STATUS_LOST")
			{ Year = Year, KingdomId = kingdomId, Severity = Severity.Major };
			lost.With("kingdom", KingdomNameOf(kingdomId));
			Bus.Publish(lost);
		}
		return response;
	}

	/// <summary>
	/// 0.49.1: закрыт ли этой расе въезд в державу в этом году.
	/// Именно это и значит «депортация» по заказу: никто никого не вывозит —
	/// человеку просто не дают стать гражданином.
	/// </summary>
	public bool IsEntryBanned(int kingdomId, string raceId, int year)
	{
		if (kingdomId <= 0 || string.IsNullOrEmpty(raceId))
		{
			return false;
		}
		for (int i = 0; i < _citizenshipBans.Count; i++)
		{
			CitizenshipBan ban = _citizenshipBans[i];
			if (ban.KingdomId == kingdomId && ban.RaceId == raceId && ban.UntilYear > year)
			{
				return true;
			}
		}
		return false;
	}

	/// <summary>0.49.1: записать запрет (отзыв гражданства или закрытый въезд).</summary>
	public void AddCitizenshipBan(int kingdomId, string raceId, string raceName, int fromYear, int untilYear, bool revoke)
	{
		if (kingdomId <= 0 || string.IsNullOrEmpty(raceId))
		{
			return;
		}
		_citizenshipBans.Add(new CitizenshipBan
		{
			KingdomId = kingdomId,
			RaceId = raceId,
			RaceName = raceName ?? string.Empty,
			FromYear = fromYear,
			UntilYear = untilYear,
			CitizenshipRevoked = revoke
		});
	}

	/// <summary>
	/// 0.49: отметить, КАК держава потеряла свою родину. Федерация и уния дают
	/// самых горячих националистов, вассалитет — средних, захват — самых тихих.
	/// Вызывается, когда мод знает способ присоединения (уния, вассалитет).
	/// </summary>
	public void NoteHomesteadBond(int kingdomId, HomesteadBond bond)
	{
		NationalismState state = NationalismOf(kingdomId);
		if (state != null && state.Bond < bond)
		{
			state.Bond = bond;
		}
	}

	private readonly HashSet<int> _mergedIntoSuzerain = new HashSet<int>();

	/// <summary>Помечает державу как влитую в сюзерена/лидера унии, чтобы при снятии старого Kingdom не публиковалось ложное «держава исчезла».</summary>
	public void RecordMergedIntoSuzerain(int memberId)
	{
		if (memberId > 0)
		{
			_mergedIntoSuzerain.Add(memberId);
		}
	}

	/// <summary>Регистрирует известное название державы в ядре (например, имя мятежного города при отделении).</summary>
	public void RememberKingdomName(int kingdomId, string name)
	{
		if (kingdomId > 0 && !string.IsNullOrWhiteSpace(name))
		{
			_kingdomNames[kingdomId] = name;
		}
	}

		/// <summary>Задаёт принудительное переименование державы для синхронизации с игровым слоем.</summary>
		public void SetPendingRenameOverride(int kingdomId, string name)
		{
			if (kingdomId > 0 && !string.IsNullOrWhiteSpace(name))
			{
				_kingdomNames[kingdomId] = name;
				_pendingRenameOverrides[kingdomId] = name;
			}
		}

		/// <summary>
		/// Официальные имена, которые ядро уже присвоило державам, но игровой слой ещё должен
		/// записать в живые королевства WorldBox. Само назначение имени в ядре не меняет native
		/// объект игры: без этого списка революция могла сменить строй и титул правителя, а надпись
		/// на карте оставалась старой (например, империя продолжала называться «Добрвецане»).
		/// </summary>
		public IReadOnlyDictionary<int, string> PendingRenameOverrides => _pendingRenameOverrides;

		public bool TryGetPendingRenameOverride(int kingdomId, out string name)
		{
			return _pendingRenameOverrides.TryGetValue(kingdomId, out name);
		}

		/// <summary>Снимает ожидание только если игровому слою удалось записать именно это имя.</summary>
		public void CompletePendingRenameOverride(int kingdomId, string appliedName)
		{
			if (_pendingRenameOverrides.TryGetValue(kingdomId, out string pending)
				&& string.Equals(pending, appliedName, StringComparison.Ordinal))
			{
				_pendingRenameOverrides.Remove(kingdomId);
			}
		}

	/// <summary>Присваивает племени этнонимическое название («Чащобники», «Луговики», «Мерзлотники», «Ольховяне» и т.д.) по его столице, культуре или биому.</summary>
	public string AssignTribalName(KingdomPolitics state, string? capitalName, string? biomeId = null, string? cultureName = null)
	{
		if (state == null)
		{
			return string.Empty;
		}
		HashSet<string> used = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
		foreach (KeyValuePair<int, string> kv in _kingdomNames)
		{
			if (kv.Key != state.KingdomId && !string.IsNullOrWhiteSpace(kv.Value))
			{
				used.Add(kv.Value);
			}
		}
		string tribalName = TribalCivilization.GenerateTribalName(capitalName, biomeId, state.KingdomId, used, cultureName);
		state.HasTribalEthnonym = true;
		RememberKingdomName(state.KingdomId, tribalName);
		SetPendingRenameOverride(state.KingdomId, tribalName);
		return tribalName;
	}

	/// <summary>Присваивает державе идеологическое государственное название (из 20+ вариантов для её идеологии).</summary>
	public string AssignIdeologyStateName(
		KingdomPolitics state,
		string? rawName,
		string? capitalName = null,
		string? biomeId = null,
		string? cultureName = null)
	{
		if (state == null)
		{
			return string.Empty;
		}
		if (state.Ideology == IdeologyType.Tribalism)
		{
			return AssignTribalName(state, capitalName ?? rawName, biomeId, cultureName);
		}
		HashSet<string> used = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
		foreach (KeyValuePair<int, string> kv in _kingdomNames)
		{
			if (kv.Key != state.KingdomId && !string.IsNullOrWhiteSpace(kv.Value))
			{
				used.Add(kv.Value);
			}
		}
		string newName = IdeologyStateNames.GenerateStateName(
			state.Ideology,
			rawName,
			capitalName,
			state.KingdomId,
			Year,
			used,
			biomeId,
			cultureName);
		state.HasTribalEthnonym = false;
		RememberKingdomName(state.KingdomId, newName);
		SetPendingRenameOverride(state.KingdomId, newName);
		return newName;
	}

	/// <summary>
	/// Приводит уже известное имя державы к форме её текущего строя, если оно ей не соответствует.
	/// Топоним сохраняется; меняется только идеологическая форма («Королевство X» после перехода
	/// к вольной территории становится, например, «Вольная Территория X»).
	/// </summary>
	public string EnsureIdeologyStateName(
		KingdomPolitics state,
		string? rawName,
		string? capitalName = null,
		string? biomeId = null,
		string? cultureName = null)
	{
		if (state == null)
		{
			return string.Empty;
		}
		string current = KingdomNameOf(state.KingdomId);
		if (string.IsNullOrWhiteSpace(current) || current.StartsWith("#"))
		{
			current = !string.IsNullOrWhiteSpace(rawName) ? rawName.Trim() : capitalName ?? string.Empty;
		}
		if (string.IsNullOrWhiteSpace(current))
		{
			return string.Empty;
		}
		// 0.31.17: на карте, в окнах и в летописи — полное имя с ярлыком строя
		// («Королевство Гогиш», «Гогишский Рейх»). Коренное имя («Гогиш») остаётся
		// для названий войн и союзов — его выделяет StateNaming.Root / WarTitle.
		if (state.Ideology != IdeologyType.Tribalism)
		{
			string labelled = StateNaming.MapName(state.Ideology, current, state.KingdomId, capitalName);
			if (!string.IsNullOrWhiteSpace(labelled) && labelled != current)
			{
				_kingdomNames[state.KingdomId] = labelled;
			}
			return string.IsNullOrWhiteSpace(labelled) ? current : labelled;
		}
		if (state.Ideology == IdeologyType.Tribalism)
		{
			if (TribalCivilization.IsKnownTribalEthnonym(current))
			{
				state.HasTribalEthnonym = true;
				return current;
			}
			return AssignIdeologyStateName(state, current, capitalName, biomeId, cultureName);
		}
		List<string> variants = IdeologyStateNames.GetAllVariants(state.Ideology, current, capitalName);
		for (int i = 0; i < variants.Count; i++)
		{
			if (string.Equals(variants[i], current, StringComparison.OrdinalIgnoreCase))
			{
				return current;
			}
		}
		return AssignIdeologyStateName(state, current, capitalName, biomeId, cultureName);
	}

	// Этап 19: личные отношения правителей (ключ — пара государств, значение −100..100).
	private readonly Dictionary<long, int> _rulerRelations = new Dictionary<long, int>();

	// Этап 20: глубокая дипломатия — договоры, обиды, коалиции, история войн.
	private readonly TreatyBook _treaties = new TreatyBook();

	private readonly GrievanceBook _grievances = new GrievanceBook();

	private readonly CoalitionBook _coalitions = new CoalitionBook();

	private readonly List<WarDeclarationRecord> _warDeclarations = new List<WarDeclarationRecord>();

	/// <summary>Пары держав, воюющих по последнему снимку игры (ключ = меньший id · 2^32 + больший).</summary>
	private readonly HashSet<long> _warsSnapshot = new HashSet<long>();

	private static long WarPairKey(int a, int b) => a < b ? ((long)a << 32) | (uint)b : ((long)b << 32) | (uint)a;

	/// <summary>Воюют ли две державы по последнему снимку игры.</summary>
	public bool AreAtWar(int a, int b) => _warsSnapshot.Contains(WarPairKey(a, b));

	/// <summary>Ведёт ли держава хотя бы одну войну по последнему снимку игры.</summary>
	public bool IsAtWar(int kingdomId)
	{
		foreach (KingdomPolitics other in Kingdoms)
		{
			if (other.KingdomId != kingdomId && AreAtWar(kingdomId, other.KingdomId))
			{
				return true;
			}
		}
		return false;
	}

	/// <summary>Есть ли у двух держав общий противник по последнему снимку игры.</summary>
	public bool HasCommonEnemy(int a, int b)
	{
		if (a == b)
		{
			return false;
		}
		foreach (KingdomPolitics other in Kingdoms)
		{
			int id = other.KingdomId;
			if (id != a && id != b && AreAtWar(a, id) && AreAtWar(b, id))
			{
				return true;
			}
		}
		return false;
	}

	// Этап 22: перемирия, репарации и требования городов по мирным договорам.
	private readonly TruceBook _truces = new TruceBook();
	// Этап 23: пограничная политика и распады держав.
	private readonly BordersBook _borders = new BordersBook();
	private readonly CollapseTracker _collapses = new CollapseTracker();

	private readonly List<ReparationsOrder> _reparations = new List<ReparationsOrder>();
	// Приказы на вооружённое отделение/освобождение земель (исполняет игровой слой);
	// мирные legacy-приказы превращаются там в поглощённые вассальные земли.
	private readonly List<RestorationOrder> _pendingRestorations = new List<RestorationOrder>();
	// Довоенный состав городов держав: id державы → города на момент объявления войны.
	private readonly Dictionary<int, List<int>> _prewarCities = new Dictionary<int, List<int>>();

	private readonly List<CityDemandOrder> _pendingCityDemands = new List<CityDemandOrder>();

	/// <summary>Призывы вступить в войну (член коалиции/пакта → агрессор): исполняются игровым слоем.</summary>
	private readonly List<KeyValuePair<int, int>> _pendingDefenseCalls = new List<KeyValuePair<int, int>>();

	/// <summary>Живые вассалы, уже вышедшие из унии по итогам годового одобрения:
	/// пары (вассал, бывший сюзерен), для которых игровой слой обязан начать
	/// настоящую войну за независимость. Список не сохраняется отдельно: он
	/// исполняется в том же годовом цикле, а при отказе API ставится заново.</summary>
	private readonly List<KeyValuePair<int, int>> _pendingIndependenceWars = new List<KeyValuePair<int, int>>();

	/// <summary>Переводы золота по тайным операциям: исполняются игровым слоем.</summary>
	private readonly List<KeyValuePair<int, int>> _pendingGoldTransfers = new List<KeyValuePair<int, int>>();

	// Этап 21: ванильные альянсы игры и унии держав (вассалитет и соединяющее поглощение).

	/// <summary>Последний сверенный с игрой снимок ванильных альянсов.</summary>
	private readonly AllianceBook _alliances = new AllianceBook();

	/// <summary>Базовый снимок альянсов записан (первый снимок событий не даёт).</summary>
	private bool _allianceBaseline;

	/// <summary>Переводы городов при интеграции: пары (из державы, в державу). Исполняет игровой слой.</summary>
	private readonly List<KeyValuePair<int, int>> _pendingCityTransfers = new List<KeyValuePair<int, int>>();

	/// <summary>0.24: поглощения (соединения), которые игровой слой ещё не исполнил в мире.</summary>
	private readonly List<PendingMerger> _pendingMergers = new List<PendingMerger>();

	private int _nextUnionId = 1;

	/// <summary>Следующий свободный идентификатор для государства, созданного ядром (отделение).</summary>
	private int _nextBreakawayId = 1;

	public PolEventBus Bus { get; } = new PolEventBus();

	public DeathRegistry Deaths { get; } = new DeathRegistry();

	/// <summary>Этап 25: реестр родословной всех жителей мира (умершие родичи не амнезия).</summary>
	public FamilyRegistry Family { get; } = new FamilyRegistry();

	public UnionRules UnionRules { get; set; } = new UnionRules();

	public SimParams Params { get; set; } = new SimParams();

	public Random Rng { get; set; } = new Random(12345);

	/// <summary>Этап 19: отдельный случайный поток для личностей правителей и браков,
	/// чтобы не смещать последовательность политических бросков (детерминизм старых тестов).</summary>
	public Random DynastyRng { get; set; } = new Random(150019);

	public GovernanceState Governance { get; private set; } = new GovernanceState();
	public DiplomaticMemoryBook DiplomaticMemories { get; } = new DiplomaticMemoryBook();
	public CityHomelandBook CityHomelands { get; } = new CityHomelandBook();
    public Func<int,IList<int>,bool> CivicSecessionRequest {get;set;}

    /// <summary>Only an explicit, verified creation event may call this. Never infer from Year.</summary>
    public void BeginNewWorldGovernance(string worldKey)
    {
        if (Governance.Enabled) throw new InvalidOperationException("New-world governance is already initialized.");
        Governance = GovernanceState.ForNewWorld(worldKey);
        foreach(var state in _kingdoms.Values) if(state.Constitution!=null && state.Constitution.Electoral==null)
            state.Constitution.Electoral=ElectoralDefaults.FromConstitution(state.Constitution);
    }
    /// <summary>
    /// 0.48.3: конституция «по требованию». Владелец 29.10.2026: «Ещё и конституция
    /// не открывается». Окно конституции молча выходило, если у державы её ещё не было
    /// (так бывает у старых сейвов, у держав, которые только что появились, и после
    /// смены строя): кнопка есть, а нажатие ничего не делает и ничего не объясняет.
    /// Теперь грамота выписывается в момент открытия — по строю и эпохе.
    /// </summary>
    public Constitution EnsureConstitution(int kingdomId)
    {
        if (!Params.EnableConstitution)
        {
            return null;
        }
        KingdomPolitics state = GetKingdom(kingdomId);
        if (state == null)
        {
            return null;
        }
        if (state.Constitution == null)
        {
            state.Constitution = CreateConstitution(state.Ideology, EraRules.OfYear(Year, Params));
        }
        return state.Constitution;
    }

    private Constitution CreateConstitution(IdeologyType ideology,IdeologyEpoch epoch)
    {
        var c=ConstitutionDefaults.For(ideology,epoch);
        if(Governance.Enabled)c.Electoral=ElectoralDefaults.FromConstitution(c);return c;
    }
    internal void RestoreGovernance(GovernanceState state) { Governance = state ?? throw new ArgumentNullException(nameof(state)); }

    public int Year { get; private set; }

	public bool BaselineRecorded { get; private set; }

	public IReadOnlyCollection<KingdomPolitics> Kingdoms => _kingdoms.Values;

	public IReadOnlyCollection<CityPolitics> Cities => _cities.Values;

	public IReadOnlyList<Union> Unions => _unions;

	public int ActorCount => _actors.Count;

	public Dictionary<int, ActorEntry> ActorEntries => _actors;

	public int LeaderReligionId { get; set; }

	/// <summary>
	/// Поставщик данных о предводителе мятежа: (kingdomId, cityId) → (имя, женщина, actorId).
	/// Игровой слой подставляет реального лидера города; в ядре и тестах остаётся null,
	/// и тогда имя вождя генерируется по знамени мятежа.
	/// </summary>
	public Func<int, int, (string Name, bool IsFemale, int ActorId)>? RebelLeaderResolver { get; set; }

	/// <summary>Сеть торговых путей мира.</summary>
	public TradeNetwork Trade { get; } = new TradeNetwork();

	/// <summary>Игровой слой применяет экономический эффект (золото, еда, настроение) в мире.
	/// Возврат false означает, что игровое API недоступно — ядро просто продолжит без эффекта.</summary>
	public Func<KingdomPolitics, EconomyReport, bool>? EconomyEffectApplier { get; set; }

	/// <summary>Название державы из последнего игрового снимка (пусто, если ещё не приходило).</summary>
	public string KingdomNameOf(int kingdomId)
	{
		return _kingdomNames.TryGetValue(kingdomId, out var value) ? value : string.Empty;
	}

	// ------------------------------------------------------------------ отношения правителей (этап 19)

	/// <summary>Ключ пары государств для словаря отношений (не зависит от порядка аргументов).</summary>
	public static long PairKey(int a, int b)
	{
		return a < b ? ((long)a << 32) | (uint)b : ((long)b << 32) | (uint)a;
	}

	/// <summary>Личные отношения правителей двух держав (−100..100, 0 — не знакомы).</summary>
	public int GetRulerRelation(int a, int b)
	{
		return _rulerRelations.TryGetValue(PairKey(a, b), out var value) ? value : 0;
	}

	/// <summary>Сдвиг отношений правителей с зажатием в −100..100.</summary>
	public void ChangeRulerRelation(int a, int b, int delta)
	{
		long key = PairKey(a, b);
		int current = _rulerRelations.TryGetValue(key, out var value) ? value : 0;
		_rulerRelations[key] = Math.Max(-100, Math.Min(100, current + delta));
	}

	/// <summary>Все пары отношений правителей (для сохранения).</summary>
	public IReadOnlyDictionary<long, int> RulerRelations => _rulerRelations;

	/// <summary>Устанавливает отношение напрямую (загрузка сохранения).</summary>
	public void SetRulerRelation(int a, int b, int value)
	{
		_rulerRelations[PairKey(a, b)] = Math.Max(-100, Math.Min(100, value));
	}

	/// <summary>Отчёт экономики державы за последний тик (null — экономика выключена).</summary>
	public EconomyReport? LastEconomyReport(int kingdomId)
	{
		return _economyReports.TryGetValue(kingdomId, out var value) ? value : null;
	}

	public KingdomPolitics? GetKingdom(int id)
	{
		if (!_kingdoms.TryGetValue(id, out var value))
		{
			return null;
		}
		return value;
	}

	public CityPolitics? GetCity(int id)
	{
		if (!_cities.TryGetValue(id, out var value))
		{
			return null;
		}
		return value;
	}

	public ActorEntry? GetActor(int id)
	{
		if (!_actors.TryGetValue(id, out var value))
		{
			return null;
		}
		return value;
	}

	public IdeologyType NeighborIdeology(int kingdomId)
	{
		return GetKingdom(kingdomId)?.Ideology ?? IdeologyType.Monarchy;
	}

	public float NeighborMissionary(int kingdomId)
	{
		return GetKingdom(kingdomId)?.Def.Missionary ?? 0f;
	}

	/// <summary>
	/// Стартовый уровень угрозы (0..35) — свой у каждого государства, чтобы срывы
	/// расходились во времени. Считается от id и идеологии, без обращения к Rng.
	/// </summary>
	public static float StartingThreat(int kingdomId, IdeologyType ideology)
	{
		int hash = kingdomId * 2654435761u.GetHashCode() ^ ((int)ideology + 1) * 40503;
		int spread = Math.Abs(hash % 36);
		return spread;
	}

	public static SuccessionLaw InitialMonarchyLaw(int kingdomId)
	{
		int hash = Math.Abs((kingdomId * 2654435761u.GetHashCode()) % 4);
		return hash switch
		{
			0 => SuccessionLaw.Salic,
			1 => SuccessionLaw.Cognatic,
			2 => SuccessionLaw.StrictAgnatic,
			3 => SuccessionLaw.DynasticUnion,
			_ => SuccessionLaw.Salic
		};
	}

	public KingdomPolitics AddKingdom(int kingdomId, IdeologyType ideology, int foundedYear, ReformStance stance = ReformStance.Moderate)
	{
		KingdomPolitics kingdomPolitics = new KingdomPolitics
		{
			KingdomId = kingdomId,
			Ideology = ideology,
			Stance = ActorSimulation.ClampStanceFor(ideology, stance),
			Legitimacy = Ideologies.Get(ideology).Legitimacy,
			FoundedYear = foundedYear,
			SuccessionLaw = (ideology == IdeologyType.Monarchy) ? InitialMonarchyLaw(kingdomId) : SuccessionLaws.GetDefaultLaw(ideology)
		};
		if (SuccessionLaws.HasPeriodicCycle(ideology))
		{
			kingdomPolitics.Election = new ElectionState { NextElectionYear = foundedYear + ElectionSystem.TermYears, TermsServed = 1 };
		}
		// Разброс стартовой угрозы. Без него все государства, созданные в один год,
		// копят угрозу с одной скоростью и срываются синхронно: в тестовой партии
		// в год 55 «наследника нет» объявили разом все семь держав.
		// Бросок берём ОТ ИДЕНТИФИКАТОРА, а не из общего Rng: так разброс
		// детерминирован и не сдвигает последовательность случайных чисел мира.
		kingdomPolitics.Mechanics.ThreatLevel = StartingThreat(kingdomId, ideology);
		if (Params.EnableConstitution)
		{
			kingdomPolitics.Constitution = CreateConstitution(ideology, EraRules.OfYear(Year, Params));
		}
		_kingdoms[kingdomId] = kingdomPolitics;
		return kingdomPolitics;
	}

	/// <summary>Конституции живут не от голубизны: при смене идеологии додефолт под новую
	/// (если держава ещё не правила конституции руками/собраниями — история пуста).</summary>
	public void RefreshConstitutionAfterIdeologyChange(KingdomPolitics state)
	{
		if (!Params.EnableConstitution || state == null)
		{
			return;
		}
		if (state.Constitution == null || state.ConstitutionHistory.Count == 0)
		{
			var existingChapter = state.Constitution?.Electoral;
            state.Constitution = CreateConstitution(state.Ideology, EraRules.OfYear(Year, Params));
            if (existingChapter != null) state.Constitution.Electoral = existingChapter;
		}
	}

	/// <summary>Предгейтовая проверка действий войны (используется перед удачным сбросом агрессии:
	/// не разрешена — идём на голосование; голосование тоже провалилось — война не состоится).</summary>
	public bool TryConstitutionalWar(int kingdomId, int targetId, string defenderName, out string why)
	{
		why = null;
		if (!Params.EnableConstitution)
		{
			return true;
		}
		KingdomPolitics state = GetKingdom(kingdomId);
		if (state == null)
		{
			return true;
		}
		if (state.Constitution == null)
		{
			state.Constitution = CreateConstitution(state.Ideology, EraRules.OfYear(Year, Params));
		}
		if (ConstitutionGates.WarIsConstitutionallyAllowed(state, targetId, Year))
		{
			return true;
		}
		// Война с этой державой уже идёт — продолжение не требует нового голосования.
		if (AreAtWar(kingdomId, targetId))
		{
			return true;
		}
		// Binding + не правитель решает = собрание/народ: голосование с подсчётом живых жителей.
		bool authorized = WarReferendum.Hold(state, KingdomNameOf(kingdomId),
			targetId, defenderName ?? KingdomNameOf(targetId), Year, Rng, Bus, Params);
		if (!authorized)
		{
			why = CoreText.L("RP_RES_REFERENDUM_FAILED", "конституция запретила: голосование народа сорвалось");
			return false;
		}
		return true;
	}

	/// <summary>Ванильная война, объявленная игрой в обход конституции: акт вне закона
	/// (по выбору игрока — mod_only: игра не ломается, нарушителя карает его же народ).</summary>
	public void ApplyConstitutionalViolation(int attackerId, int defenderId)
	{
		if (!Params.EnableConstitution)
		{
			return;
		}
		KingdomPolitics state = GetKingdom(attackerId);
		if (state?.Constitution == null)
		{
			return;
		}
		if (state.Constitution.WarPower == WarAuthority.ByRuler || state.Constitution.Supremacy == SupremacyRule.Advisory)
		{
			return;
		}
		if (state.HasWarAuthorization(defenderId, Year))
		{
			return;
		}
		// Уже воюющие державы (например, повторная запись войны игрой) — не новый обход конституции.
		if (AreAtWar(attackerId, defenderId))
		{
			return;
		}
		// Война, объявленная самим модом в этом году (оборона союзника, независимость,
		// уже прошедший референдум), — не «ванильный обход» конституции.
		for (int i = _warDeclarations.Count - 1; i >= 0; i--)
		{
			WarDeclarationRecord rec = _warDeclarations[i];
			if (rec.Year < Year - 1)
			{
				break;
			}
			if (rec.AttackerId == attackerId && rec.DefenderId == defenderId)
			{
				return;
			}
		}
		int legBefore = (int)Math.Round(state.Legitimacy);
		state.Legitimacy = Math.Max(0f, state.Legitimacy - 12f);
		// 0.39: жители запоминают, что правитель пошёл против собственного закона.
		PromiseMemory.Record(state, PromiseMemory.UnconstitutionalWar, Year);
		Bus.Publish(new PolEvent(PolEventType.UnconstitutionalAct, "RP_EV_UNCONSTITUTIONAL_WAR")
		{
			Year = Year,
			Severity = Severity.Major,
			KingdomId = attackerId
		}.With("kingdom", KingdomNameOf(attackerId))
			.With("target", KingdomNameOf(defenderId))
			.With("before", legBefore.ToString())
			.With("after", ((int)Math.Round(state.Legitimacy)).ToString()));
	}

	/// <summary>Этап 26: смена правителя в обход конституции — узурпация.
	/// Карает только «жёсткую» (Binding) конституцию: (а) глава избираем, а выборов в этом году
	/// не было; (б) дом сменился на чужой клан без права претензии. Множитель ×0.6 к легитимности.</summary>
	private void CheckConstitutionalSuccession(KingdomPolitics state, KingdomInput input, bool dynastyShifted, bool inheritedByClaim)
	{
		if (!Params.EnableConstitution || input.Ruler == null || state?.Constitution == null)
		{
			return;
		}
		Constitution constitution = state.Constitution;
		if (constitution.Supremacy != SupremacyRule.Binding)
		{
			return; // советская роль — «совет», узурпации как таковой нет
		}
		bool usurped = false;
		if (constitution.HoldsElectionsForHead)
		{
			// Узурпацию можно вменить только там, где выборный цикл реально идёт
			// (демократия, вече, купцы, технократы…). Собрание теократии или сената
			// само утверждает преемника — периодических выборов у него нет, и каждая
			// смена главы не должна клеймиться узурпацией.
			bool runsElectoralCycle = SuccessionLaws.HasPeriodicCycle(state.SuccessionLaw)
				|| SuccessionLaws.HasPeriodicCycle(state.Ideology);
			if (runsElectoralCycle && (state.Election == null || Year - state.Election.LastElectionYear > 1))
			{
				// Выборы не проходили, а правитель сменился. Допуск в один год: победитель
				// модовых выборов садится на игровой трон только на следующем синке,
				// и без допуска каждые законные выборы клеймились бы узурпацией.
				usurped = true;
			}
		}
		else if (dynastyShifted && !inheritedByClaim && state.Ideology == IdeologyType.Monarchy)
		{
			// 0.24, статья XIII «О кровных связях»: если претендент — кровный родич прежнего
			// правящего дома (или дома любого венценосца державы), смена дома НЕ клеймится узурпацией.
			usurped = !ConstitutionGates.BloodClaim(state, dynastyShifted, inheritedByClaim: false, input.Ruler.ClanId, RoyalClansOf(state));
		}
		if (!usurped || state.LastConstitutionalUsurpYear == Year)
		{
			return;
		}
		state.LastConstitutionalUsurpYear = Year;
		int legBefore = (int)Math.Round(state.Legitimacy);
		state.Legitimacy = Math.Max(5f, state.Legitimacy * 0.6f);
		state.AvgDissent = Math.Min(1f, state.AvgDissent + 0.10f);
		Bus.Publish(new PolEvent(PolEventType.ConstitutionalUsurpation, "RP_EV_CONST_USURPATION")
		{
			Year = Year,
			Severity = Severity.Major,
			KingdomId = state.KingdomId,
			ActorId = input.Ruler.ActorId
		}.With("kingdom", KingdomNameOf(state.KingdomId))
			.With("ruler", input.Ruler.Name)
			.With("before", legBefore.ToString())
			.With("after", ((int)Math.Round(state.Legitimacy)).ToString()));
	}

	/// <summary>0.24, статья XIII: венценосные роды, УШЕДШИЕ с престола державы
	/// (прежний дом + история домов). Текущий дом сюда не входит: претендент по крови —
	/// возвращающийся наследник ушедшего рода, а не сам нынешний венценосец.</summary>
	private static IEnumerable<long> RoyalClansOf(KingdomPolitics state)
	{
		List<long> clans = new List<long>();
		if (state.PreviousRoyalClanId > 0)
		{
			clans.Add(state.PreviousRoyalClanId);
		}
		for (int i = 0; i < state.RoyalClanHistory.Count; i++)
		{
			if (state.RoyalClanHistory[i] > 0)
			{
				clans.Add(state.RoyalClanHistory[i]);
			}
		}
		return clans;
	}

	/// <summary>Города державы по данным ядра.</summary>
	public List<int> CityIdsOf(int kingdomId)
	{
		List<int> list = new List<int>();
		foreach (CityPolitics city in _cities.Values)
		{
			if (city.KingdomId == kingdomId)
			{
				list.Add(city.CityId);
			}
		}
		return list;
	}

	/// <summary>Столица державы: первый город, пришедший в ядро (0 — городов нет).</summary>
	public int CapitalCityIdOf(int kingdomId)
	{
		foreach (CityPolitics city in _cities.Values)
		{
			if (city.KingdomId == kingdomId)
			{
				return city.CityId;
			}
		}
		return 0;
	}

	public void RemoveKingdom(int kingdomId)
	{
        bool existed = _kingdoms.ContainsKey(kingdomId);
        DeathRecord lastResident = ChronicleCauses.Take(kingdomId, Year);
        string removedName = KingdomNameOf(kingdomId);
        if (string.IsNullOrWhiteSpace(removedName)) removedName = "#" + kingdomId;
		_kingdoms.Remove(kingdomId);
		_lastRuler.Remove(kingdomId);
		_lastRulerRef.Remove(kingdomId);
		_economyReports.Remove(kingdomId);
		_treaties.RemoveKingdom(kingdomId);
		Trade.DropKingdom(kingdomId);
		List<int> list = new List<int>();
		foreach (CityPolitics value in _cities.Values)
		{
			if (value.KingdomId == kingdomId)
			{
				list.Add(value.CityId);
			}
		}
		for (int i = 0; i < list.Count; i++)
		{
			RemoveCity(list[i]);
		}
		List<int> list2 = new List<int>();
		foreach (ActorEntry value2 in _actors.Values)
		{
			if (value2.KingdomId == kingdomId)
			{
				list2.Add(value2.Id);
			}
		}
		for (int j = 0; j < list2.Count; j++)
		{
			UnregisterActor(list2[j]);
		}
		// Этап 22: перемирия и репарации с исчезнувшей державой отменяются.
		_truces.RemoveKingdom(kingdomId);
		_reparations.RemoveAll(o => o.PayerId == kingdomId || o.PayeeId == kingdomId);
		_pendingCityDemands.RemoveAll(o => o.FromId == kingdomId || o.ToId == kingdomId);
		_prewarCities.Remove(kingdomId);
		// Восстановления не трогаем: приказ переживает гибель державы намеренно.
		// Этап 23.
		_borders.RemoveKingdom(kingdomId);
		_collapses.RemoveKingdom(kingdomId);
        _annexedBy.TryGetValue(kingdomId, out string annexerName);
        _annexedBy.Remove(kingdomId);
        bool mergedSilently = _mergedIntoSuzerain.Remove(kingdomId);
        if (existed && !mergedSilently)
        {
            PolEvent ended = new PolEvent(PolEventType.KingdomDisappeared, "RP_EV_KINGDOM_DISAPPEARED")
            { Year = Year, KingdomId = kingdomId, Severity = Severity.Major, Death = lastResident };
            ended.With("kingdom", removedName);
            if (!string.IsNullOrEmpty(annexerName))
                ended.With("annexed_by", annexerName);
            if (lastResident != null)
                ended.With("last_resident", lastResident.ActorName).With("death_year", lastResident.Year)
                    .With("death_cause", lastResident.LocKeyFor(lastResident.WasFemale));
            Bus.Publish(ended);
        }
	}

	public CityPolitics AddCity(int cityId, int kingdomId)
	{
		CityPolitics cityPolitics = new CityPolitics
		{
			CityId = cityId,
			KingdomId = kingdomId,
			Dominant = IdeologyType.Monarchy,
			Share = 1f
		};
		_cities[cityId] = cityPolitics;
		if (kingdomId > 0)
		{
			KingdomPolitics? owner = GetKingdom(kingdomId);
			CityHomelands.EnsureRecorded(cityId, kingdomId, KingdomNameOf(kingdomId), owner?.Ideology ?? IdeologyType.Monarchy);
		}
		return cityPolitics;
	}

	public void RemoveCity(int cityId)
	{
		_cities.Remove(cityId);
		if (_actorsByCity.TryGetValue(cityId, out var value))
		{
			int[] array = value.ToArray();
			for (int i = 0; i < array.Length; i++)
			{
				UnregisterActor(array[i]);
			}
			_actorsByCity.Remove(cityId);
		}
	}

	public ActorEntry RegisterActor(int actorId, int kingdomId, int cityId, ActorPolitics politics)
	{
		ActorEntry actorEntry = new ActorEntry(actorId, politics, kingdomId, cityId);
		_actors[actorId] = actorEntry;
		if (!_actorsByCity.TryGetValue(cityId, out var value))
		{
			value = new List<int>();
			_actorsByCity[cityId] = value;
		}
		value.Add(actorId);
		return actorEntry;
	}

	public ActorEntry RegisterBirth(int actorId, int kingdomId, int cityId, ActorPolitics? mother, ActorPolitics? father)
	{
		KingdomPolitics kingdomPolitics = GetKingdom(kingdomId);
		if (kingdomPolitics == null)
		{
			kingdomPolitics = AddKingdom(kingdomId, Ideologies.RollStarting(Rng), Year);
		}
		ActorPolitics politics = ActorSimulation.Inherit(mother, father, GetCity(cityId), kingdomPolitics, Rng, EraRules.OfYear(Year, Params));
		return RegisterActor(actorId, kingdomId, cityId, politics);
	}

	public void UnregisterActor(int actorId)
	{
		if (_actors.TryGetValue(actorId, out var value))
		{
			_actors.Remove(actorId);
			if (_actorsByCity.TryGetValue(value.CityId, out var value2))
			{
				value2.Remove(actorId);
			}
		}
	}

	public void MoveActor(int actorId, int cityId, int kingdomId)
	{
		if (_actors.TryGetValue(actorId, out var value))
		{
			if (_actorsByCity.TryGetValue(value.CityId, out var value2))
			{
				value2.Remove(actorId);
			}
			value.CityId = cityId;
			value.KingdomId = kingdomId;
			if (!_actorsByCity.TryGetValue(cityId, out var value3))
			{
				value3 = new List<int>();
				_actorsByCity[cityId] = value3;
			}
			value3.Add(actorId);
		}
	}

	public int YearTick(List<KingdomInput> inputs)
	{
		int count = Bus.Log.Count;
		if (inputs != null)
		{
			ReconcileSnapshot(inputs);
			SnapshotWars(inputs);
			for (int i = 0; i < inputs.Count; i++)
			{
				KingdomInput input = inputs[i];
				if (!_kingdoms.ContainsKey(input.KingdomId))
				{
					AddKingdom(input.KingdomId, Ideologies.RollStarting(Rng), Year);
				}
				ProcessKingdom(input);
			}
		}
		TickDiplomacyDeep(inputs);
		ProcessUnions();
		TickNationalism(inputs);
		TickRaceNationalism(inputs);
		TickPeoples(inputs);
		TickChambers(inputs);
		BaselineRecorded = true;
		Year++;
		return Bus.Log.Count - count;
	}

	/// <summary>
	/// 0.53: год палат — настоящая внутренняя политика.
	///
	/// Владелец 30.10.2026 выбрал: палаты решают по-настоящему, у правителя есть вето,
	/// а сколько палат и от чего верхняя — решает конституция.
	///
	/// Как идёт закон:
	///   1. Нижняя палата голосует. Простое большинство — дальше.
	///   2. Если статья про войну, границы, союз или повинность — голосует верхняя.
	///   3. Не сошлись — согласительная комиссия на год, потом ещё одно голосование.
	///   4. Правитель может наложить вето; палаты могут преодолеть его двумя третями.
	///   5. Двадцать лет без единого принятого закона — это ступор: законность падает.
	/// </summary>
	private void TickChambers(List<KingdomInput> inputs)
	{
		if (inputs == null || inputs.Count == 0)
		{
			return;
		}
		for (int i = 0; i < inputs.Count; i++)
		{
			int kingdomId = inputs[i].KingdomId;
			if (!_kingdoms.TryGetValue(kingdomId, out KingdomPolitics state) || state == null)
			{
				continue;
			}
			if (state.Constitution == null || state.CivilWar != null || state.Revolution != null)
			{
				continue;
			}
			int cityCount = input_city_count(inputs[i]);
			if (!ChamberRules.HasChambers(state, cityCount))
			{
				continue;
			}
			Bill bill = BillOf(kingdomId);
			// 1. Новый закон вносится только если ничего не висит и палаты успели отдышаться.
			if (bill == null || !bill.Pending)
			{
				if (bill != null)
				{
					_bills.Remove(kingdomId);
					_lastBillYear[kingdomId] = Year;
				}
				if (_lastBillYear.TryGetValue(kingdomId, out int lastYear) && Year - lastYear < 2)
				{
					continue;
				}
				bill = ProposeBill(state, kingdomId, cityCount);
				if (bill == null)
				{
					if (state.Constitution.Chambers == ChamberCount.Two)
					{
						TickChamberDeadlock(state, kingdomId);
					}
					continue;
				}
				_bills[kingdomId] = bill;
			}
			AdvanceBill(state, kingdomId, bill, cityCount);
		}
	}

	private static int input_city_count(KingdomInput input)
	{
		return input.Cities != null ? input.Cities.Count : 0;
	}

	/// <summary>Какая партия вносит закон: чем больше мест, тем вероятнее (как на выборах).</summary>
	private PoliticalParty PickParty(KingdomPolitics state)
	{
		if (state?.Parties == null || state.Parties.Count == 0)
		{
			return null;
		}
		int total = 0;
		for (int i = 0; i < state.Parties.Count; i++)
		{
			PoliticalParty party = state.Parties[i];
			if (party != null)
			{
				total += Math.Max(1, party.SeatsPercent);
			}
		}
		if (total <= 0)
		{
			return state.Parties[0];
		}
		int roll = Rng.Next(total);
		for (int i = 0; i < state.Parties.Count; i++)
		{
			PoliticalParty party = state.Parties[i];
			if (party == null)
			{
				continue;
			}
			roll -= Math.Max(1, party.SeatsPercent);
			if (roll < 0)
			{
				return party;
			}
		}
		return state.Parties[0];
	}

	/// <summary>Внести новый законопроект: правитель по своему характеру, либо народ по требованию.</summary>
	private Bill ProposeBill(KingdomPolitics state, int kingdomId, int cityCount)
	{
		if (state.Parties != null && state.Parties.Count > 0 && Rng.NextDouble() < 0.35)
		{
			// Парламентская держава: закон вносит партия, а не правитель.
			CivicDemandType demand = state.ActiveMovement != null ? state.ActiveMovement.Demand : CivicDemandType.None;
			bool forPeople = demand == CivicDemandType.IntroduceElections
				|| demand == CivicDemandType.LowerTaxesAndAusterity
				|| demand == CivicDemandType.EndExhaustingWar
				|| demand == CivicDemandType.AbolishSecretPolice;
			// 0.53: не только закручивание. Половина законов идёт от народа и ослабляет власть,
			// иначе в республике, где у власти либералы, палаты навсегда упирались в отказ.
			bool tighten = forPeople ? false : Rng.NextDouble() < 0.5;
			PoliticalParty author = PickParty(state);
			if (author == null)
			{
				return null;
			}
			List<ConstitutionAmendments.Proposal> options = ConstitutionAmendments.Proposals(state, tighten, forPeople);
			if (options.Count > 0)
			{
				ConstitutionAmendments.Proposal chosen = options[Rng.Next(options.Count)];
				return new Bill
				{
					KingdomId = kingdomId,
					Article = chosen.Article,
					From = chosen.From,
					To = chosen.To,
					Tightens = chosen.Tightens,
					FromPeople = forPeople,
					ProposedYear = Year,
					Stage = BillStage.Lower,
					ProposerIdeology = author.Ideology,
					EditorName = author.Name ?? string.Empty
				};
			}
		}
		// Правитель вносит поправку, как и раньше, но теперь её надо провести через палаты.
		if (!ConstitutionAmendments.RulerMayAmend(state, Year, Params))
		{
			return null;
		}
		int dir = ConstitutionAmendments.RulerDirection(state);
		if (dir == 0)
		{
			return null;
		}
		List<ConstitutionAmendments.Proposal> list = ConstitutionAmendments.Proposals(state, dir > 0, forPeople: false);
		if (list.Count == 0)
		{
			return null;
		}
		ConstitutionAmendments.Proposal pick = list[Rng.Next(list.Count)];
		return new Bill
		{
			KingdomId = kingdomId,
			Article = pick.Article,
			From = pick.From,
			To = pick.To,
			Tightens = pick.Tightens,
			FromPeople = false,
			ProposedYear = Year,
			Stage = BillStage.Lower,
			EditorName = state.Ruler?.Name ?? string.Empty
		};
	}

	/// <summary>Провести законопроект на один шаг вперёд.</summary>
	private void AdvanceBill(KingdomPolitics state, int kingdomId, Bill bill, int cityCount)
	{
		Constitution constitution = state.Constitution;
		ChamberCount chambers = constitution.Chambers;
		bool twoChambers = chambers == ChamberCount.Two;

		if (bill.Stage == BillStage.Lower)
		{
			ChamberRules.LowerVote(state, bill, out int forSeats, out int againstSeats);
			bill.LowerFor = forSeats;
			bill.LowerAgainst = againstSeats;
			if (!ChamberRules.Majority(forSeats, againstSeats))
			{
				FinishBill(state, kingdomId, bill, false, "RP53_EV_BILL_FAILED_LOWER");
				return;
			}
			if (twoChambers && ChamberRules.NeedsUpperChamber(bill.Article))
			{
				bill.Stage = BillStage.Upper;
				return;
			}
			// Одна палата или вопрос не земельный — сразу к правителю.
			AfterChambers(state, kingdomId, bill);
			return;
		}

		if (bill.Stage == BillStage.Upper)
		{
			int seats = ChamberRules.UpperSeats(constitution.UpperBasis, cityCount,
				Math.Max(1, PeoplesOf(kingdomId)?.Count ?? 1), Math.Max(1, cityCount * 2));
			ChamberRules.UpperVote(constitution.UpperBasis, seats, bill, state.Legitimacy, state.Support,
				out int upFor, out int upAgainst);
			bill.UpperFor = upFor;
			bill.UpperAgainst = upAgainst;
			if (ChamberRules.Majority(upFor, upAgainst))
			{
				AfterChambers(state, kingdomId, bill);
				return;
			}
			// Палаты не сошлись — согласительная комиссия на год.
			bill.Stage = BillStage.Conference;
			bill.ConferenceYear = Year + 1;
			PolEvent conference = new PolEvent(PolEventType.ChambersDisagree, "RP53_EV_CONFERENCE")
			{ Year = Year, KingdomId = kingdomId, Severity = Severity.Minor };
			conference.With("kingdom", KingdomNameOf(kingdomId))
				.With("article", bill.Article)
				.With("lower", bill.LowerFor)
				.With("upper", bill.UpperFor);
			Bus.Publish(conference);
			return;
		}

		if (bill.Stage == BillStage.Conference)
		{
			if (Year < bill.ConferenceYear)
			{
				return;
			}
			// Повторное голосование верхней палаты: за год договорились или нет.
			int seats = ChamberRules.UpperSeats(constitution.UpperBasis, cityCount,
				Math.Max(1, PeoplesOf(kingdomId)?.Count ?? 1), Math.Max(1, cityCount * 2));
			ChamberRules.UpperVote(constitution.UpperBasis, seats, bill, state.Legitimacy + 10f, state.Support,
				out int again, out int againstAgain);
			bill.UpperFor = again;
			bill.UpperAgainst = againstAgain;
			if (ChamberRules.Majority(again, againstAgain))
			{
				AfterChambers(state, kingdomId, bill);
			}
			else
			{
				FinishBill(state, kingdomId, bill, false, "RP53_EV_BILL_FAILED_UPPER");
			}
			return;
		}

		if (bill.Stage == BillStage.Vetoed)
		{
			// Палаты пробуют преодолеть вето двумя третями.
			if (ChamberRules.TwoThirds(bill.LowerFor, bill.LowerAgainst)
				&& (!twoChambers || !ChamberRules.NeedsUpperChamber(bill.Article)
					|| ChamberRules.TwoThirds(bill.UpperFor, bill.UpperAgainst)))
			{
				PolEvent overrode = new PolEvent(PolEventType.VetoOverridden, "RP53_EV_VETO_OVERRIDDEN")
				{ Year = Year, KingdomId = kingdomId, Severity = Severity.Major };
				overrode.With("kingdom", KingdomNameOf(kingdomId))
					.With("article", bill.Article)
					.With("lower", bill.LowerFor);
				Bus.Publish(overrode);
				ApplyBill(state, kingdomId, bill);
			}
			else
			{
				FinishBill(state, kingdomId, bill, false, "RP53_EV_VETO_HELD");
			}
		}
	}

	/// <summary>Палаты приняли: теперь слово за правителем — вето или подпись.</summary>
	private void AfterChambers(KingdomPolitics state, int kingdomId, Bill bill)
	{
		Constitution constitution = state.Constitution;
		RulerTrait trait = state.Personality?.Trait ?? RulerTrait.None;
		bool veto = constitution.RulerVeto && ChamberRules.RulerVetoes(trait, bill.Tightens, bill.FromPeople);
		if (veto)
		{
			bill.Stage = BillStage.Vetoed;
			PolEvent vetoed = new PolEvent(PolEventType.RulerVetoed, "RP53_EV_VETO")
			{ Year = Year, KingdomId = kingdomId, Severity = Severity.Major };
			vetoed.With("kingdom", KingdomNameOf(kingdomId))
				.With("article", bill.Article)
				.With("ruler", state.Ruler?.Name ?? string.Empty);
			Bus.Publish(vetoed);
			return;
		}
		ApplyBill(state, kingdomId, bill);
	}

	/// <summary>Закон принят — меняем статью и пишем в летопись.</summary>
	private void ApplyBill(KingdomPolitics state, int kingdomId, Bill bill)
	{
		if (ConstitutionAmendments.TryFindProposal(state, bill.Article, bill.Tightens, bill.FromPeople, out ConstitutionAmendments.Proposal proposal))
		{
			ConstitutionAmendments.EnactByChambers(state, proposal, Year, bill.EditorName);
		}
		bill.Stage = BillStage.Passed;
		_bills.Remove(kingdomId);
		_deadlockSince.Remove(kingdomId);
		PolEvent passed = new PolEvent(PolEventType.BillPassed, "RP53_EV_BILL_PASSED")
		{ Year = Year, KingdomId = kingdomId, Severity = Severity.Major };
		passed.With("kingdom", KingdomNameOf(kingdomId))
			.With("article", bill.Article)
			.With("lower", bill.LowerFor)
			.With("upper", bill.UpperFor)
			.With("two_chambers", state.Constitution.Chambers == ChamberCount.Two ? "RP53_YES" : "RP53_NO");
		Bus.Publish(passed);
	}

	private void FinishBill(KingdomPolitics state, int kingdomId, Bill bill, bool passed, string key)
	{
		bill.Stage = passed ? BillStage.Passed : BillStage.Rejected;
		_bills.Remove(kingdomId);
		PolEvent rejected = new PolEvent(PolEventType.BillRejected, key)
		{ Year = Year, KingdomId = kingdomId, Severity = Severity.Minor };
		rejected.With("kingdom", KingdomNameOf(kingdomId))
			.With("article", bill.Article)
			.With("lower", bill.LowerFor)
			.With("upper", bill.UpperFor);
		Bus.Publish(rejected);
	}

	/// <summary>Ступор: палаты двадцать лет ничего не принимают — законность падает.</summary>
	private void TickChamberDeadlock(KingdomPolitics state, int kingdomId)
	{
		if (!_deadlockSince.TryGetValue(kingdomId, out int since) || since <= 0)
		{
			_deadlockSince[kingdomId] = Year;
			return;
		}
		if (Year - since < ChamberRules.DeadlockYears)
		{
			return;
		}
		state.Legitimacy = Math.Max(0f, state.Legitimacy - ChamberRules.DeadlockLegitimacyHit);
	}

	/// <summary>
	/// 0.52: год народов — культурная ассимиляция и местные конституции.
	///
	/// Владелец 30.10.2026:
	///   • «пусть ассимиляция будет культурной»: народ не меняется, меняется язык;
	///   • «в России азиаты живут с Ермака и не стали европеоидами, говорят на русском
	///     и своём — потому что их насильно не ассимилировали»;
	///   • «если в государстве больше одного города и есть город, где больше половины
	///     жителей — другой народ, там есть своя конституция, которая может
	///     противоречить общей. Столица может запретить народностям иметь конституцию,
	///     а может разрешить. И может терпеть противоречия, а может не терпеть».
	///
	/// Ничего в юнитах игры не меняется: ни внешность, ни подвид. Только язык и устав.
	/// </summary>
	private void TickPeoples(List<KingdomInput> inputs)
	{
		if (inputs == null || inputs.Count == 0)
		{
			return;
		}
		for (int i = 0; i < inputs.Count; i++)
		{
			KingdomInput input = inputs[i];
			int kingdomId = input.KingdomId;
			if (!_kingdoms.TryGetValue(kingdomId, out KingdomPolitics state) || state == null)
			{
				continue;
			}
			List<CityInput> cities = input.Cities;
			if (cities == null || cities.Count == 0)
			{
				continue;
			}
			// 1. Перепись по городам.
			for (int c = 0; c < cities.Count; c++)
			{
				if (cities[c].CityId > 0 && !string.IsNullOrWhiteSpace(cities[c].Name))
				{
					_cityNames[cities[c].CityId] = cities[c].Name;
				}
			}
			Dictionary<string, int> byRace = new Dictionary<string, int>();
			Dictionary<string, string> names = new Dictionary<string, string>();
			Dictionary<int, string> cityRace = new Dictionary<int, string>();
			int total = 0;
			for (int c = 0; c < cities.Count; c++)
			{
				string raceId = !string.IsNullOrEmpty(cities[c].RaceId) ? cities[c].RaceId : cities[c].CultureName;
				if (string.IsNullOrEmpty(raceId))
				{
					continue;
				}
				int population = cities[c].Population > 0 ? cities[c].Population : 1;
				total += population;
				byRace.TryGetValue(raceId, out int was);
				byRace[raceId] = was + population;
				if (!names.ContainsKey(raceId))
				{
					names[raceId] = !string.IsNullOrEmpty(cities[c].RaceName) ? cities[c].RaceName : raceId;
				}
				if (cities[c].CityId > 0)
				{
					cityRace[cities[c].CityId] = raceId;
				}
			}
			if (total <= 0 || byRace.Count == 0)
			{
				continue;
			}
			// 2. Записи народов: завести, обновить численность и долю.
			Dictionary<string, PeopleRecord> book = PeoplesOf(kingdomId);
			foreach (KeyValuePair<string, int> pair in byRace)
			{
				if (!book.TryGetValue(pair.Key, out PeopleRecord record))
				{
					record = new PeopleRecord
					{
						RaceId = pair.Key,
						RaceName = names[pair.Key],
						FirstSeenYear = Year,
						StageSinceYear = Year,
						Stage = pair.Key == state.TitularRaceId ? LanguageStage.Shared : LanguageStage.Own
					};
					book[pair.Key] = record;
				}
				record.Population = pair.Value;
				record.Share = (float)pair.Value / total;
				record.Titular = pair.Key == state.TitularRaceId;
				if (string.IsNullOrEmpty(record.RaceName))
				{
					record.RaceName = names[pair.Key];
				}
			}
			// Народ, который ушёл, больше не держим в книге: он не «растворяется», он уехал.
			List<string> gone = new List<string>();
			foreach (string raceId in book.Keys)
			{
				if (!byRace.ContainsKey(raceId))
				{
					gone.Add(raceId);
				}
			}
			for (int g = 0; g < gone.Count; g++)
			{
				book.Remove(gone[g]);
			}
			// 3. Язык: ступень меняется по закону державы.
			Constitution constitution = state.Constitution;
			LanguageLaw languageLaw = constitution?.Language ?? LanguageLaw.Free;
			CourtLaw courtLaw = constitution?.Court ?? CourtLaw.Own;
			RaceLaw raceLaw = constitution?.Race ?? RaceLaw.Equal;
			bool languageFree = languageLaw == LanguageLaw.Free;
			bool courtOwn = courtLaw == CourtLaw.Own;
			bool oppressed = raceLaw == RaceLaw.Segregated;
			foreach (PeopleRecord record in book.Values)
			{
				int yearsInStage = Math.Max(0, Year - record.StageSinceYear);
				LanguageStage next = AssimilationEngine.NextStage(
					record.Stage, record.Share, languageFree, courtOwn, oppressed, yearsInStage);
				if (next != record.Stage && !record.Titular)
				{
					record.Stage = next;
					record.StageSinceYear = Year;
					PolEvent language = new PolEvent(PolEventType.PeopleLanguageShift, "RP52_EV_LANGUAGE")
					{ Year = Year, KingdomId = kingdomId, Severity = Severity.Minor };
					language.With("kingdom", KingdomNameOf(kingdomId))
						.With("race", record.RaceName)
						.With("stage", AssimilationEngine.StageKey(next));
					Bus.Publish(language);
				}
			}
			// 3.5. Правитель из чужого народа.
			// Владелец 30.10.2026: «в России к власти может прийти эскимос, но тоже
			// технически можно запретить конституцией».
			RulerRef ruler = input.Ruler;
			if (ruler != null && !string.IsNullOrEmpty(ruler.RaceId)
				&& !string.IsNullOrEmpty(state.TitularRaceId)
				&& !string.Equals(ruler.RaceId, state.TitularRaceId, StringComparison.OrdinalIgnoreCase)
				&& constitution != null && !constitution.ForeignRulerAllowed)
			{
				_foreignRulerNoted.TryGetValue(kingdomId, out int noted);
				if (noted != ruler.ActorId)
				{
					_foreignRulerNoted[kingdomId] = ruler.ActorId;
					book.TryGetValue(ruler.RaceId, out PeopleRecord rulerPeople);
					float hit = AssimilationEngine.ForeignRulerLegitimacyHit(
						true, false, rulerPeople != null ? rulerPeople.Share : 0f);
					state.Legitimacy = Math.Max(0f, state.Legitimacy - hit);
					PolEvent stranger = new PolEvent(PolEventType.ForeignRulerWithoutLaw, "RP52_EV_FOREIGN_RULER")
					{ Year = Year, KingdomId = kingdomId, Severity = Severity.Major };
					stranger.With("kingdom", KingdomNameOf(kingdomId))
						.With("ruler", ruler.Name ?? string.Empty)
						.With("race", rulerPeople != null ? rulerPeople.RaceName : ruler.RaceId)
						.With("titular", names.TryGetValue(state.TitularRaceId ?? string.Empty, out string tn) ? tn : state.TitularRaceId);
					Bus.Publish(stranger);
				}
			}
			// 4. Местные конституции.
			bool allowLocal = constitution?.AllowLocalConstitutions ?? true;
			bool tolerate = constitution?.TolerateLocalContradictions ?? true;
			foreach (KeyValuePair<int, string> pair in cityRace)
			{
				int cityId = pair.Key;
				string raceId = pair.Value;
				CityPolitics city = GetCity(cityId);
				if (city == null || city.KingdomId != kingdomId)
				{
					continue;
				}
				// Доля народа в этом городе. Игра даёт по городу один главный народ,
				// поэтому по умолчанию считаем город однородным; если слой игры когда-нибудь
				// сообщит точную долю (CityInput.RaceShare) — возьмём её.
				float cityShare = 1f;
				for (int c = 0; c < cities.Count; c++)
				{
					if (cities[c].CityId == cityId && cities[c].RaceShare > 0f)
					{
						cityShare = cities[c].RaceShare;
						break;
					}
				}
				LocalConstitution local = LocalConstitutionOf(cityId);
				bool foreignMajority = raceId != state.TitularRaceId && cityShare > LocalConstitutionRules.MajorityShare;
				bool canExist = LocalConstitutionRules.CanExist(allowLocal, cities.Count, cityShare) && foreignMajority;
				string raceName = names.TryGetValue(raceId, out string nm) ? nm : raceId;
				// Устав должен существовать, а его нет — пишем.
				if (canExist && local == null)
				{
					book.TryGetValue(raceId, out PeopleRecord record);
					int yearsHere = record != null ? Math.Max(0, Year - record.FirstSeenYear) : 0;
					if (record != null && !AssimilationEngine.WantsOwnConstitution(cityShare, yearsHere))
					{
						continue;
					}
					local = AdoptLocalConstitution(cityId, kingdomId, raceId, raceName);
					if (record != null)
					{
						record.RequestedYear = Year;
					}
					PolEvent adopted = new PolEvent(PolEventType.LocalConstitutionAdopted, "RP52_EV_LOCAL_ADOPTED")
					{ Year = Year, KingdomId = kingdomId, Severity = Severity.Minor };
					adopted.With("kingdom", KingdomNameOf(kingdomId))
						.With("city", CityNameOf(cityId))
						.With("race", raceName);
					Bus.Publish(adopted);
				}
				if (local == null)
				{
					continue;
				}
				// Столица запретила уставы — этот снимается и начинает спорить.
				if (!allowLocal && !local.Banned)
				{
					local.Banned = true;
					local.BannedYear = Year;
					PolEvent banned = new PolEvent(PolEventType.LocalConstitutionBanned, "RP52_EV_LOCAL_BANNED")
					{ Year = Year, KingdomId = kingdomId, Severity = Severity.Major };
					banned.With("kingdom", KingdomNameOf(kingdomId))
						.With("city", CityNameOf(cityId))
						.With("race", local.RaceName);
					Bus.Publish(banned);
				}
				else if (allowLocal && local.Banned)
				{
					// Разрешили снова — устав возвращается.
					local.Banned = false;
					local.BannedYear = 0;
					PolEvent back = new PolEvent(PolEventType.LocalConstitutionAdopted, "RP52_EV_LOCAL_RESTORED")
					{ Year = Year, KingdomId = kingdomId, Severity = Severity.Minor };
					back.With("kingdom", KingdomNameOf(kingdomId))
						.With("city", CityNameOf(cityId))
						.With("race", local.RaceName);
					Bus.Publish(back);
				}
				// Устав живой — обновляем, чего народ просит.
				if (!local.Banned)
				{
					local.WantsLanguage = languageLaw != LanguageLaw.Free;
					local.WantsCourt = courtLaw != CourtLaw.Own;
				}
				bool contradicts = local.WantsLanguage || local.WantsCourt;
				float before = local.Grievance;
				local.Grievance = LocalConstitutionRules.NextGrievance(local.Grievance, contradicts, tolerate, local.Banned);
				// Громкая жалоба и угроза мятежа — по одному разу на порог.
				if (before < LocalConstitutionRules.ComplaintThreshold && local.Grievance >= LocalConstitutionRules.ComplaintThreshold)
				{
					PolEvent hate = new PolEvent(PolEventType.LocalConstitutionConflict, "RP52_EV_LOCAL_CONFLICT")
					{ Year = Year, KingdomId = kingdomId, Severity = Severity.Major };
					hate.With("kingdom", KingdomNameOf(kingdomId))
						.With("city", CityNameOf(cityId))
						.With("race", local.RaceName)
						.With("grievance", (int)Math.Round(local.Grievance));
					Bus.Publish(hate);
				}
				else if (before < LocalConstitutionRules.RevoltThreshold && local.Grievance >= LocalConstitutionRules.RevoltThreshold)
				{
					PolEvent revolt = new PolEvent(PolEventType.LocalConstitutionRevolt, "RP52_EV_LOCAL_REVOLT")
					{ Year = Year, KingdomId = kingdomId, Severity = Severity.Critical };
					revolt.With("kingdom", KingdomNameOf(kingdomId))
						.With("city", CityNameOf(cityId))
						.With("race", local.RaceName)
						.With("grievance", (int)Math.Round(local.Grievance));
					Bus.Publish(revolt);
				}
			}
			// 5. Города больше не принадлежат державе — их уставы убираем.
			List<int> dead = new List<int>();
			foreach (LocalConstitution local in _localConstitutions.Values)
			{
				if (local.KingdomId == kingdomId && GetCity(local.CityId) == null)
				{
					dead.Add(local.CityId);
				}
			}
			for (int d = 0; d < dead.Count; d++)
			{
				DropLocalConstitution(dead[d]);
			}
		}
	}

	/// <summary>
	/// 0.49.1: год расового национализма.
	///
	/// Считаем по городам: у каждого города в данных игры есть раса (CityInput.RaceId),
	/// у державы — титульная раса (KingdomPolitics.TitularRaceId). Все, кто не титульный,
	/// и есть «чужие». Самая крупная чужая раса, если её доля не меньше 15 %, и есть
	/// расовое движение.
	///
	/// Жёсткое правило владельца: в державе с ОДНОЙ расой таких движений не бывает
	/// никогда — здесь это проверка, а не вероятность.
	/// </summary>
	private void TickRaceNationalism(List<KingdomInput> inputs)
	{
		if (inputs == null || inputs.Count == 0)
		{
			return;
		}
		for (int i = 0; i < inputs.Count; i++)
		{
			KingdomInput input = inputs[i];
			int kingdomId = input.KingdomId;
			if (!_kingdoms.TryGetValue(kingdomId, out KingdomPolitics state) || state == null)
			{
				continue;
			}
			RaceNationalismState movement = RaceMovementOf(kingdomId);
			if (movement == null)
			{
				continue;
			}
			// 1. Время вернуть прежний строй.
			if (RaceNationalismEngine.TimeToReturn(movement, Year))
			{
				state.Ideology = RaceNationalismEngine.ParentIdeology(movement);
				if (!string.IsNullOrEmpty(movement.PrevTitularRace))
				{
					state.TitularRaceId = movement.PrevTitularRace;
				}
				movement.SeizedYear = 0;
				movement.ShadowUntilYear = Year + RaceNationalismEngine.ShadowAfterVictoryYears;
				movement.Pressure = 20f;
				movement.Victories++;
				RefreshConstitutionAfterIdeologyChange(state);
				PolEvent restored = new PolEvent(PolEventType.RaceNationalismPowerReturned, "RP49_EV_RACE_RETURNED")
				{ Year = Year, KingdomId = kingdomId, Severity = Severity.Major };
				restored.With("kingdom", KingdomNameOf(kingdomId))
					.With("race", movement.RaceName ?? string.Empty)
					.With("ideology", Ideologies.NameKey(state.Ideology))
					.With("ban_until", movement.EntryBanUntilYear);
				Bus.Publish(restored);
				continue;
			}
			// 2. У власти — не пересчитываем.
			if (movement.SeizedYear > 0)
			{
				continue;
			}
			// 3. Считаем расы по городам.
			List<CityInput> cities = input.Cities;
			if (cities == null || cities.Count == 0)
			{
				continue;
			}
			string titular = state.TitularRaceId;
			int total = 0;
			Dictionary<string, int> byRace = new Dictionary<string, int>();
			Dictionary<string, string> names = new Dictionary<string, string>();
			for (int c = 0; c < cities.Count; c++)
			{
				string raceId = !string.IsNullOrEmpty(cities[c].RaceId) ? cities[c].RaceId : cities[c].CultureName;
				if (string.IsNullOrEmpty(raceId))
				{
					continue;
				}
				int population = cities[c].Population > 0 ? cities[c].Population : 1;
				total += population;
				byRace.TryGetValue(raceId, out int was);
				byRace[raceId] = was + population;
				if (!names.ContainsKey(raceId))
				{
					names[raceId] = !string.IsNullOrEmpty(cities[c].RaceName) ? cities[c].RaceName : raceId;
				}
			}
			if (total <= 0 || byRace.Count == 0)
			{
				continue;
			}
			if (string.IsNullOrEmpty(titular))
			{
				// Титульная раса ещё не определена — берём самую многочисленную.
				int best = -1;
				foreach (KeyValuePair<string, int> pair in byRace)
				{
					if (pair.Value > best)
					{
						best = pair.Value;
						titular = pair.Key;
					}
				}
				state.TitularRaceId = titular;
			}
			// 4. Ищем самую крупную чужую расу.
			string rivalId = string.Empty;
			int rivalPopulation = 0;
			foreach (KeyValuePair<string, int> pair in byRace)
			{
				if (pair.Key == titular || pair.Value <= rivalPopulation)
				{
					continue;
				}
				rivalId = pair.Key;
				rivalPopulation = pair.Value;
			}
			float rivalShare = (float)rivalPopulation / total;
			// Правило владельца: одна раса — расовых националистов нет.
			if (string.IsNullOrEmpty(rivalId) || !RaceNationalismEngine.CanAppear(byRace.Count, rivalShare))
			{
				movement.RaceId = string.Empty;
				movement.RaceName = string.Empty;
				movement.Share = 0f;
				movement.Pressure = Math.Max(0f, movement.Pressure - 0.6f);
				continue;
			}
			movement.RaceId = rivalId;
			movement.RaceName = names.TryGetValue(rivalId, out string rivalName) ? rivalName : rivalId;
			movement.Share = rivalShare;
			bool oppressed = RaceSurvival.PolicyOf(state) == RacePolicy.Oppress;
			movement.Pressure = RaceNationalismEngine.NextPressure(movement.Pressure, rivalShare, oppressed);
			// 5. Берут власть?
			if (movement.Pressure < RaceNationalismEngine.PowerThreshold || Year < movement.ShadowUntilYear)
			{
				continue;
			}
			RulerTrait trait = state.Personality?.Trait ?? RulerTrait.None;
			movement.PrevIdeology = (int)state.Ideology;
			movement.PrevTitularRace = state.TitularRaceId;
			movement.SeizedYear = Year;
			movement.EntryBanUntilYear = Year + RaceNationalismEngine.EntryBanYears(movement.Pressure);
			NationalismVerdict verdict = RaceNationalismEngine.Verdict(trait, movement.Pressure);
			// Хозяйкой становится их раса, строй — этнонационализм.
			state.Ideology = IdeologyType.Ethnonationalism;
			state.TitularRaceId = movement.RaceId;
			if (verdict == NationalismVerdict.RevokeCitizenship)
			{
				// Отзыв гражданства у всех, кто не из их расы. Никого не вывозим.
				foreach (KeyValuePair<string, int> pair in byRace)
				{
					if (pair.Key == movement.RaceId)
					{
						continue;
					}
					string otherName = names.TryGetValue(pair.Key, out string n) ? n : pair.Key;
					AddCitizenshipBan(kingdomId, pair.Key, otherName, Year, movement.EntryBanUntilYear, true);
				}
			}
			else
			{
				string prevRace = movement.PrevTitularRace ?? string.Empty;
				string prevName = names.TryGetValue(prevRace, out string t) ? t : prevRace;
				AddCitizenshipBan(kingdomId, prevRace, prevName, Year, movement.EntryBanUntilYear, false);
			}
			RefreshConstitutionAfterIdeologyChange(state);
			PolEvent seized = new PolEvent(PolEventType.RaceNationalismSeizedPower, "RP49_EV_RACE_SEIZED")
			{ Year = Year, KingdomId = kingdomId, Severity = Severity.Critical };
			seized.With("kingdom", KingdomNameOf(kingdomId))
				.With("race", movement.RaceName)
				.With("share", (int)Math.Round(movement.Share * 100f))
				.With("pressure", (int)Math.Round(movement.Pressure))
				.With("ban_until", movement.EntryBanUntilYear)
				.With("verdict", verdict == NationalismVerdict.RevokeCitizenship ? "RP49_VERDICT_REVOKE" : "RP49_VERDICT_BORDERS");
			Bus.Publish(seized);
			// 0.50: за отнятое гражданство мир наказывает жёстко — санкции, потеря
			// звания великой державы и вопрос на ближайшем съезде КМУД.
			PunishOutrage(kingdomId, movement.Pressure, verdict == NationalismVerdict.RevokeCitizenship, "RP50_REASON_RACE");
		}
	}

	/// <summary>
	/// 0.49: год национализма в каждой державе.
	///
	/// Считаем по городам: у мода уже есть книга родных земель (CityHomelandBook) —
	/// она помнит, какой державе город принадлежал изначально. Города, чья родина
	/// не совпадает с нынешним хозяином, и есть «бывшие земли». Чем их больше и чем
	/// мягче было присоединение, тем сильнее движение.
	///
	/// Три исхода года:
	///   • националисты дошли до власти — держава временно становится националистической,
	///     запоминается прежний строй (его они и вернут);
	///   • пришло время — они возвращают прежний строй и уходят в тень;
	///   • ничего не происходит — движение растёт или остывает.
	/// </summary>
	private void TickNationalism(List<KingdomInput> inputs)
	{
		if (inputs == null || inputs.Count == 0)
		{
			return;
		}
		for (int i = 0; i < inputs.Count; i++)
		{
			int kingdomId = inputs[i].KingdomId;
			if (!_kingdoms.TryGetValue(kingdomId, out KingdomPolitics state) || state == null)
			{
				continue;
			}
			NationalismState movement = NationalismOf(kingdomId);
			if (movement == null)
			{
				continue;
			}
			// 1. Время вернуть прежний строй.
			if (NationalismEngine.TimeToReturn(movement, Year))
			{
				IdeologyType parent = NationalismEngine.ParentIdeology(movement);
				state.Ideology = parent;
				movement.SeizedYear = 0;
				movement.ShadowUntilYear = Year + NationalismEngine.ShadowAfterVictoryYears;
				movement.Pressure = 20f;
				movement.Victories++;
				RefreshConstitutionAfterIdeologyChange(state);
				PolEvent restored = new PolEvent(PolEventType.NationalismPowerReturned, "RP49_EV_POWER_RETURNED")
				{ Year = Year, KingdomId = kingdomId, Severity = Severity.Major };
				restored.With("kingdom", KingdomNameOf(kingdomId))
					.With("ideology", Ideologies.NameKey(parent))
					.With("origin", movement.OriginName ?? string.Empty)
					.With("ban_until", movement.EntryBanUntilYear);
				Bus.Publish(restored);
				continue;
			}
			// 2. Пока националисты у власти, движение не пересчитываем.
			if (movement.SeizedYear > 0)
			{
				continue;
			}
			// 3. Считаем бывшие земли.
			int homelandCities = 0;
			int dominantOrigin = 0;
			string originName = string.Empty;
			int annexedYear = 0;
			List<int> cities = new List<int>();
			List<CityPolitics> possessed = new List<CityPolitics>();
			foreach (CityPolitics each in Cities)
			{
				if (each.KingdomId == kingdomId)
				{
					possessed.Add(each);
				}
			}
			for (int c = 0; c < possessed.Count; c++)
			{
				cities.Add(possessed[c].CityId);
				if (!CityHomelands.All.TryGetValue(possessed[c].CityId, out CityHomelandRecord record) || record == null)
				{
					continue;
				}
				if (record.OriginalKingdomId <= 0 || record.OriginalKingdomId == kingdomId)
				{
					continue;
				}
				homelandCities++;
				if (record.OriginalKingdomId == dominantOrigin)
				{
					continue;
				}
				// Берём ту родину, которой больше всего городов.
				if (string.IsNullOrEmpty(originName) || record.AnnexedYear >= annexedYear)
				{
					dominantOrigin = record.OriginalKingdomId;
					originName = record.OriginalKingdomName;
					annexedYear = record.AnnexedYear;
				}
			}
			if (homelandCities == 0 || string.IsNullOrEmpty(originName))
			{
				// Своей родины нет — национализма тут не будет, злоба тихо остывает.
				movement.Pressure = Math.Max(0f, movement.Pressure - 0.6f);
				movement.OriginKingdomId = 0;
				movement.OriginName = string.Empty;
				continue;
			}
			movement.OriginKingdomId = dominantOrigin;
			movement.OriginName = originName;
			if (movement.OriginYear == 0)
			{
				movement.OriginYear = annexedYear > 0 ? annexedYear : Year;
			}
			// Доля бывших земель, подогретая способом присоединения:
			// уния и федерация раздувают движение, захват — нет.
			float share = (float)homelandCities / Math.Max(1, cities.Count) * NationalismEngine.BondFactor(movement.Bond);
			movement.Pressure = NationalismEngine.NextPressure(
				movement.Pressure, share, state.Ideology != IdeologyType.Nationalism,
				Year - movement.OriginYear);
			// 4. Дошли до власти?
			if (Year >= movement.ShadowUntilYear && movement.Pressure >= NationalismEngine.PowerThreshold
				&& state.Ideology != IdeologyType.Nationalism)
			{
				RulerTrait trait = state.Personality?.Trait ?? RulerTrait.None;
				IdeologyType previous = state.Ideology;
				NationalismEngine.Seize(movement, Year, previous, trait);
				state.Ideology = IdeologyType.Nationalism;
				movement.EntryBanUntilYear = movement.SeizedYear + Math.Max(movement.EntryBanUntilYear - movement.SeizedYear, 0);
				RefreshConstitutionAfterIdeologyChange(state);
				PolEvent seized = new PolEvent(PolEventType.NationalismSeizedPower, "RP49_EV_SEIZED_POWER")
				{ Year = Year, KingdomId = kingdomId, Severity = Severity.Critical };
				seized.With("kingdom", KingdomNameOf(kingdomId))
					.With("origin", originName)
					.With("pressure", (int)Math.Round(movement.Pressure))
					.With("bound", NationalismEngine.BondReasonKey(movement.Bond))
					.With("ban_until", movement.EntryBanUntilYear)
					.With("verdict", movement.EntryBanUntilYear - Year >= 100 ? "RP49_VERDICT_HARD" : "RP49_VERDICT_SOFT");
				Bus.Publish(seized);
				// 0.50: мир возмущается и наказывает (санкции, потеря статуса, КМУД).
				PunishOutrage(kingdomId, movement.Pressure, false, "RP50_REASON_NATIONALISM");
			}
		}
	}

	/// <summary>
	/// Запоминает, кто с кем воюет, и продлевает мандаты на уже идущие войны:
	/// собрание/народ голосуют за НАЧАЛО войны, а не за каждый её год — пока война с той же
	/// державой продолжается, повторный референдум не нужен.
	/// </summary>
	private void SnapshotWars(List<KingdomInput> inputs)
	{
		_warsSnapshot.Clear();
		for (int i = 0; i < inputs.Count; i++)
		{
			KingdomInput input = inputs[i];
			if (input.Relations == null)
			{
				continue;
			}
			for (int r = 0; r < input.Relations.Count; r++)
			{
				RelationInfo rel = input.Relations[r];
				if (rel.Relation == RelationType.War)
				{
					_warsSnapshot.Add(WarPairKey(input.KingdomId, rel.OtherId));
				}
			}
			KingdomPolitics state = GetKingdom(input.KingdomId);
			if (state == null)
			{
				continue;
			}
			for (int a = 0; a < state.WarAuthorizations.Count; a++)
			{
				WarAuthorization auth = state.WarAuthorizations[a];
				if (auth.ValidUntilYear >= Year && AreAtWar(input.KingdomId, auth.TargetKingdomId))
				{
					auth.ValidUntilYear = Math.Max(auth.ValidUntilYear, Year + 1);
				}
			}
		}
	}

	private void ReconcileSnapshot(List<KingdomInput> inputs)
	{
		HashSet<int> hashSet = new HashSet<int>();
		HashSet<int> hashSet2 = new HashSet<int>();
		bool flag = false;
		for (int i = 0; i < inputs.Count; i++)
		{
			KingdomInput kingdomInput = inputs[i];
            ChronicleCauses.ObservePopulation(kingdomInput.KingdomId, kingdomInput.Population);
			hashSet.Add(kingdomInput.KingdomId);
			if (kingdomInput.Cities != null)
			{
				if (kingdomInput.Cities.Count > 0)
				{
					flag = true;
				}
				for (int j = 0; j < kingdomInput.Cities.Count; j++)
				{
					hashSet2.Add(kingdomInput.Cities[j].CityId);
				}
			}
		}
		if (flag)
		{
			List<int> list = new List<int>();
			foreach (CityPolitics value in _cities.Values)
			{
				if (!hashSet2.Contains(value.CityId))
				{
					list.Add(value.CityId);
				}
			}
			for (int k = 0; k < list.Count; k++)
			{
				RemoveCity(list[k]);
			}
		}
		List<int> list2 = new List<int>();
		foreach (KingdomPolitics value2 in _kingdoms.Values)
		{
			if (!hashSet.Contains(value2.KingdomId))
			{
				list2.Add(value2.KingdomId);
			}
		}
		for (int l = 0; l < list2.Count; l++)
		{
			int goneId = list2[l];
			if (_kingdoms.TryGetValue(goneId, out KingdomPolitics goneState) && goneState.CivilWar != null)
			{
				CivilWarState cw = goneState.CivilWar;
				if (cw.RebelKingdomId != 0 && hashSet.Contains(cw.RebelKingdomId))
				{
					ApplyCivilWarOutcome(goneState, new KingdomInput { KingdomId = goneId, Name = KingdomNameOf(goneId) }, new CivilWarOutcome
					{
						Resolution = CivilWarResolution.RebelTakeover,
						RebelIdeology = cw.RebelIdeology,
						Cause = cw.Cause,
						RebelKingdomId = cw.RebelKingdomId,
						RulerDeposed = true,
						YearsActive = cw.YearsActive(Year)
					}, Year);
					_mergedIntoSuzerain.Add(goneId);
				}
			}
			RemoveKingdom(goneId);
		}
	}

	public void RestoreUnion(Union union)
	{
		if (union != null)
		{
			_unions.Add(union);
			if (union.Id >= _nextUnionId)
			{
				_nextUnionId = union.Id + 1;
			}
		}
	}

	public Union? GetUnion(int id)
	{
		for (int i = 0; i < _unions.Count; i++)
		{
			if (_unions[i].Id == id)
			{
				return _unions[i];
			}
		}
		return null;
	}

	public Union? UnionOf(int kingdomId)
	{
		for (int i = 0; i < _unions.Count; i++)
		{
			if (_unions[i].Contains(kingdomId))
			{
				return _unions[i];
			}
		}
		return null;
	}

	public Union? TryCreateUnion(UnionType type, int leaderId, int memberId, string name, DiplomatSnapshot memberSnap, out Unions.JoinCheck check, int leaderReligionId = 0, int leaderWarriors = 0, string nameKey = "")
	{
        if(!Governance.Recognition.CanContact(leaderId,memberId,InternationalContact.PoliticalUnion))
        {check=RealPolitic.Core.Unions.JoinCheck.No("RP_GOV26_UNRECOGNIZED","государство не признано");return null;}
		KingdomPolitics kingdom = GetKingdom(leaderId);
		KingdomPolitics kingdom2 = GetKingdom(memberId);
		if (kingdom == null || kingdom2 == null)
		{
			check = RealPolitic.Core.Unions.JoinCheck.No("RP_UNION_UNKNOWN", "нет такого государства");
			return null;
		}
		if (kingdom.Legitimacy < UnionRules.MinLeaderLegitimacy)
		{
			check = RealPolitic.Core.Unions.JoinCheck.No("RP_UNION_LEADER_WEAK", "легитимность лидера " + (int)kingdom.Legitimacy);
			return null;
		}
		// 0.24: статья XV «О федеративном устройстве» — закон поглощающей державы решает,
		// кого она вправе принять в себя (соединение), а кого — нет. Язык и соседство
		// приходят в снимке претендента; нет данных (null/0) — ядро не блокирует молча,
		// окончательное слово остаётся игровому слою, который знает карту и языки.
		if (type == UnionType.Merger && kingdom.Constitution != null)
		{
			UnionLaw unionLaw = kingdom.Constitution.Union;
			int memberLanguage = memberSnap.LanguageId;
			int leaderLanguage = LeaderLanguageId;
			bool? adjacent = memberSnap.AdjacentToLeader;
			string gate = ConstitutionGates.UnionAdmission(unionLaw, leaderLanguage, memberLanguage, adjacent ?? true);
			if (gate != null)
			{
				check = RealPolitic.Core.Unions.JoinCheck.No(gate, "статья XV о федеративном устройстве");
				return null;
			}
		}
		Union union = RealPolitic.Core.Unions.Create(_nextUnionId, type, name, kingdom, Year, leaderWarriors);
		union.NameKey = nameKey ?? string.Empty;
		union.SetLeaderIdeology(kingdom.Ideology);
		check = RealPolitic.Core.Unions.CanJoin(union, kingdom2, memberSnap, UnionRules);
		if (!check.Ok)
		{
			return null;
		}
		if (type == UnionType.Merger)
		{
			// 0.24: соединение — вступивший тотчас поглощается лидером: ядро фиксирует факт
			// и снимает границы для права на выход (0.25), а перевод городов, флагов,
			// имён и смерть старого государства исполняет игровой слой.
			int capId = CapitalCityIdOf(memberId);
			List<int> memberCities = CityIdsOf(memberId);
			string memberName = KingdomNameOf(memberId);
			union.RecordAbsorption(new AbsorbedChild
			{
				KingdomId = memberId,
				CapitalCityId = capId,
				CityIds = memberCities,
				AbsorbedYear = Year
			});
			for (int i = 0; i < memberCities.Count; i++)
			{
				CityHomelands.EnsureRecorded(memberCities[i], memberId, memberName, kingdom2.Ideology, memberCities[i] == capId);
			}
			// Живым участником поглощённый не числится: уния = держава лидера из одного члена.
			_unions.Add(union);
			_nextUnionId++;
			RecordMergedIntoSuzerain(memberId);
			_pendingMergers.Add(new PendingMerger { MemberId = memberId, LeaderId = leaderId, MemberName = memberName });
			string mergeLocKey = union.IsConfederation
				? "RP_EV_CONFEDERATION_MERGED"
				: (union.IsFederation ? "RP_EV_FEDERATION_MERGED" : "RP_EV_UNION_MERGED");
			string mergeTypeKey = union.IsConfederation
				? "RP_UNIONTYPE_CONFEDERATION"
				: (union.IsFederation ? "RP_UNIONTYPE_FEDERATION" : "RP_UNIONTYPE_MERGER");
			string mergeKind = union.IsConfederation ? "confederation" : (union.IsFederation ? "federation" : "union");
			Bus.Publish(new PolEvent(PolEventType.UnionMerged, mergeLocKey)
			{
				Year = Year,
				Severity = Severity.Critical,
				KingdomId = memberId
			}.With("name", union.DisplayNameOrKey)
				.With("type", mergeTypeKey)
				.With("union_kind", mergeKind)
				.With("leader", leaderId)
				.With("member", memberId));
			return union;
		}
		RealPolitic.Core.Unions.AddMember(union, kingdom2.KingdomId, Year);
		if (type == UnionType.Vassal)
		{
			RecordVassalBorders(union.GetMember(kingdom2.KingdomId), kingdom2);
		}
		_unions.Add(union);
		_nextUnionId++;
		Bus.Publish(new PolEvent(PolEventType.UnionFormed, "RP_EV_UNION_FORMED")
		{
			Year = Year,
			Severity = Severity.Critical,
			KingdomId = leaderId
		}.With("name", union.DisplayNameOrKey).With("type", "RP_UNIONTYPE_" + type.ToString().ToUpperInvariant()).With("leader", leaderId)
			.With("member", memberId));
		return union;
	}

	private void RecordVassalBorders(UnionMember member, KingdomPolitics vassalState)
	{
		if (member == null || vassalState == null)
		{
			return;
		}
		int memberId = vassalState.KingdomId;
		int capId = CapitalCityIdOf(memberId);
		List<int> cities = CityIdsOf(memberId);
		string name = KingdomNameOf(memberId);
		member.CapitalCityId = capId;
		member.CityIds = cities;
		member.OriginalName = name;
		member.OriginalIdeology = vassalState.Ideology;
		for (int i = 0; i < cities.Count; i++)
		{
			CityHomelands.EnsureRecorded(cities[i], memberId, name, vassalState.Ideology, cities[i] == capId);
		}
	}

	/// <summary>Язык, которым говорит держава-лидер (для статьи XV). Игровой слой кладёт реальный
	/// CityLanguageId лидера; 0 — неизвестен, сверка по языку не блокирует.</summary>
	public int LeaderLanguageId { get; set; }

	/// <summary>Приглашает державу в уже существующую унию: так союз растёт дальше пары участников.
	/// Возвращает ту же унию, если вступление состоялось, и <c>null</c> вместе с причиной отказа — если нет.</summary>
	public Union? TryJoinUnion(Union union, int candidateId, DiplomatSnapshot candidateSnap, out Unions.JoinCheck check, int candidateReligionId = 0)
	{
		if (union == null)
		{
			check = RealPolitic.Core.Unions.JoinCheck.No("RP_UNION_UNKNOWN", "нет такого союза");
			return null;
		}
        if(!Governance.Recognition.CanContact(union.LeaderId,candidateId,InternationalContact.PoliticalUnion))
        {check=RealPolitic.Core.Unions.JoinCheck.No("RP_GOV26_UNRECOGNIZED","государство не признано");return null;}
		KingdomPolitics leader = GetKingdom(union.LeaderId);
		KingdomPolitics candidate = GetKingdom(candidateId);
		if (leader == null || candidate == null)
		{
			check = RealPolitic.Core.Unions.JoinCheck.No("RP_UNION_UNKNOWN", "нет такого государства");
			return null;
		}
		if (UnionOf(candidateId) != null)
		{
			check = RealPolitic.Core.Unions.JoinCheck.No("RP_UNION_ALREADY_MEMBER", "уже состоит в другом союзе");
			return null;
		}
		if (leader.Legitimacy < UnionRules.MinLeaderLegitimacy)
		{
			check = RealPolitic.Core.Unions.JoinCheck.No("RP_UNION_LEADER_WEAK", "легитимность лидера " + (int)leader.Legitimacy);
			return null;
		}
		// 0.24: соединение — статья XV поглощающей державы решает, кого принять.
		if (union.Type == UnionType.Merger && leader.Constitution != null)
		{
			string gate = ConstitutionGates.UnionAdmission(leader.Constitution.Union, LeaderLanguageId, candidateSnap.LanguageId, candidateSnap.AdjacentToLeader ?? true);
			if (gate != null)
			{
				check = RealPolitic.Core.Unions.JoinCheck.No(gate, "статья XV о федеративном устройстве");
				return null;
			}
		}
		check = RealPolitic.Core.Unions.CanJoin(union, candidate, candidateSnap, UnionRules);
		if (union.Type == UnionType.Merger && check.Ok)
		{
			// 0.24: соединяющая уния — вступивший тотчас поглощается. Живого члена не добавляем,
			// записываем поглощение (границы для права на выход) и просим игровой слой перевести города.
			int capId = CapitalCityIdOf(candidateId);
			List<int> memberCities = CityIdsOf(candidateId);
			string memberName = KingdomNameOf(candidateId);
			union.RecordAbsorption(new AbsorbedChild
			{
				KingdomId = candidateId,
				CapitalCityId = capId,
				CityIds = memberCities,
				AbsorbedYear = Year
			});
			for (int i = 0; i < memberCities.Count; i++)
			{
				CityHomelands.EnsureRecorded(memberCities[i], candidateId, memberName, candidate.Ideology, memberCities[i] == capId);
			}
			RecordMergedIntoSuzerain(candidateId);
			_pendingMergers.Add(new PendingMerger { MemberId = candidateId, LeaderId = union.LeaderId, MemberName = memberName });
			string mergeLocKey = union.IsConfederation
				? "RP_EV_CONFEDERATION_MERGED"
				: (union.IsFederation ? "RP_EV_FEDERATION_MERGED" : "RP_EV_UNION_MERGED");
			string mergeTypeKey = union.IsConfederation
				? "RP_UNIONTYPE_CONFEDERATION"
				: (union.IsFederation ? "RP_UNIONTYPE_FEDERATION" : "RP_UNIONTYPE_MERGER");
			string mergeKind = union.IsConfederation ? "confederation" : (union.IsFederation ? "federation" : "union");
			Bus.Publish(new PolEvent(PolEventType.UnionMerged, mergeLocKey)
			{
				Year = Year,
				Severity = Severity.Critical,
				KingdomId = candidateId
			}.With("name", union.DisplayNameOrKey)
				.With("type", mergeTypeKey)
				.With("union_kind", mergeKind)
				.With("leader", union.LeaderId)
				.With("member", candidateId));
			return union;
		}
		if (!check.Ok)
		{
			return null;
		}
		RealPolitic.Core.Unions.AddMember(union, candidateId, Year);
		if (union.Type == UnionType.Vassal)
		{
			RecordVassalBorders(union.GetMember(candidateId), candidate);
		}
		Bus.Publish(new PolEvent(PolEventType.UnionMemberJoined, "RP_EV_UNION_JOINED")
		{
			Year = Year,
			Severity = Severity.Major,
			KingdomId = candidateId
		}.With("union", union.DisplayNameOrKey).With("member", candidateId).With("leader", union.LeaderId)
			.With("count", union.MemberCount));
		return union;
	}

	/// <summary>Насколько отношения с партнёром улучшают торговлю: союз даёт свой бонус,
	/// уния — свой по типу, берётся лучшее из двух.</summary>
	private float PartnerTradeMultiplier(RelationType relation, Union? union, int selfId, int partnerId)
	{
		float multiplier = 1f;
		if (union != null)
		{
			multiplier = Math.Max(multiplier, RealPolitic.Core.Unions.TradeModifier(union.Type));
		}
		if (relation == RelationType.Alliance)
		{
			multiplier = Math.Max(multiplier, 1f + Params.AlliedTradeBonus);
		}
		// Этап 20: действующее торговое соглашение добавляет бонус к товарообороту.
		if (Params.EnableTreaties && _treaties.Between(selfId, partnerId, TreatyType.TradeAgreement) != null)
		{
			multiplier = Math.Max(multiplier, 1f + Params.TradeAgreementBonus);
		}
		return multiplier;
	}

	/// <summary>
	/// 0.31.2: регистрирует земли уничтоженной/исторической державы как поглощённые
	/// вассальные земли сюзерена. Отдельное государство на карте НЕ создаётся: все
	/// перечисленные города, которые сейчас принадлежат сюзерену, остаются у него,
	/// а старые границы, столица, имя и строй запоминаются в снимке вассала.
	/// Вернуть независимость такие земли смогут только восстанием и войной.
	/// </summary>
	public bool TryRegisterAbsorbedVassalTerritory(
		int suzerainId,
		int vassalId,
		string vassalName,
		IdeologyType originalIdeology,
		IEnumerable<int> cityIds,
		int capitalCityId = 0,
		float approval = 0.45f)
	{
		KingdomPolitics? suzerain = GetKingdom(suzerainId);
		if (suzerain == null || vassalId <= 0 || vassalId == suzerainId || cityIds == null)
		{
			return false;
		}

		List<int> absorbedCities = new List<int>();
		foreach (int cityId in cityIds)
		{
			if (cityId <= 0 || absorbedCities.Contains(cityId))
			{
				continue;
			}
			CityPolitics? city = GetCity(cityId);
			if (city != null && city.KingdomId == suzerainId)
			{
				absorbedCities.Add(cityId);
			}
		}
		if (absorbedCities.Count == 0)
		{
			return false;
		}
		if (capitalCityId <= 0 || !absorbedCities.Contains(capitalCityId))
		{
			capitalCityId = absorbedCities[0];
		}
		else if (absorbedCities[0] != capitalCityId)
		{
			absorbedCities.Remove(capitalCityId);
			absorbedCities.Insert(0, capitalCityId);
		}

		string rememberedName = !string.IsNullOrWhiteSpace(vassalName)
			? vassalName
			: KingdomNameOf(vassalId);
		Union? union = UnionOf(vassalId);
		UnionMember? member = null;
		if (union != null)
		{
			if (union.Type != UnionType.Vassal || union.LeaderId != suzerainId)
			{
				return false;
			}
			member = union.GetMember(vassalId);
			if (member == null)
			{
				return false;
			}
		}
		else
		{
			union = UnionOf(suzerainId);
			if (union != null && (union.Type != UnionType.Vassal || union.LeaderId != suzerainId))
			{
				return false;
			}
			if (union == null)
			{
				if (UnionRules.MaxMembers < 2)
				{
					return false;
				}
				string suzerainName = KingdomNameOf(suzerainId);
				string unionName = !string.IsNullOrWhiteSpace(suzerainName)
					&& !suzerainName.StartsWith("#", StringComparison.Ordinal)
					&& !string.IsNullOrWhiteSpace(rememberedName)
					&& !rememberedName.StartsWith("#", StringComparison.Ordinal)
						? suzerainName + "–" + rememberedName
						: string.Empty;
				union = RealPolitic.Core.Unions.Create(_nextUnionId, UnionType.Vassal, unionName, suzerain, Year);
				union.NameKey = unionName.Length == 0 ? "RP_UNION_NAME_PUPPET" : string.Empty;
				union.Cohesion = 0.55f;
				_unions.Add(union);
				_nextUnionId++;
			}
			if (union.TotalSize >= UnionRules.MaxMembers)
			{
				return false;
			}
			float clampedApproval = approval < 0f ? 0f : approval > 1f ? 1f : approval;
			RealPolitic.Core.Unions.AddMember(union, vassalId, Year, clampedApproval);
			member = union.GetMember(vassalId);
			if (member == null)
			{
				return false;
			}
		}

		if (member.CityIds == null)
		{
			member.CityIds = new List<int>();
		}
		for (int i = 0; i < absorbedCities.Count; i++)
		{
			int cityId = absorbedCities[i];
			if (!member.CityIds.Contains(cityId))
			{
				member.CityIds.Add(cityId);
			}
			CityHomelands.EnsureRecorded(cityId, vassalId, rememberedName, originalIdeology, cityId == capitalCityId);
			CityHomelands.MarkAnnexedIfChanged(cityId, suzerainId, Year);
		}
		if (member.CapitalCityId <= 0 || !member.CityIds.Contains(member.CapitalCityId))
		{
			member.CapitalCityId = capitalCityId;
		}
		if (string.IsNullOrWhiteSpace(member.OriginalName) && !string.IsNullOrWhiteSpace(rememberedName))
		{
			member.OriginalName = rememberedName;
		}
		member.OriginalIdeology = originalIdeology;
		if (!string.IsNullOrWhiteSpace(rememberedName))
		{
			RememberKingdomName(vassalId, rememberedName);
		}
		_annexedBy[vassalId] = KingdomNameOf(suzerainId);
		suzerain.Union = union;
		KingdomPolitics? vassal = GetKingdom(vassalId);
		if (vassal != null)
		{
			vassal.Union = union;
		}
		return true;
	}

	/// <summary>
	/// 0.31.2: старые мирные приказы на восстановление (из прежних сохранений) не должны
	/// воскрешать отдельную державу. Они превращаются в поглощённые вассальные земли.
	/// </summary>
	public bool ConvertRestorationToAbsorbedVassal(RestorationOrder order)
	{
		if (order == null || order.DefeatedId <= 0 || order.VictorId <= 0)
		{
			return false;
		}
		KingdomPolitics? defeated = GetKingdom(order.DefeatedId);
		IdeologyType originalIdeology;
		if (defeated != null)
		{
			originalIdeology = defeated.Ideology;
		}
		else
		{
			originalIdeology = GetKingdom(order.VictorId)?.Ideology ?? IdeologyType.Monarchy;
			for (int i = 0; i < order.PrewarCityIds.Count; i++)
			{
				CityHomelandRecord? rec = CityHomelands.Get(order.PrewarCityIds[i]);
				if (rec != null && rec.OriginalKingdomId == order.DefeatedId)
				{
					originalIdeology = rec.OriginalIdeology;
					break;
				}
			}
		}
		int capitalCityId = order.PrewarCityIds.Count > 0 ? order.PrewarCityIds[0] : 0;
		return TryRegisterAbsorbedVassalTerritory(
			order.VictorId,
			order.DefeatedId,
			order.DefeatedName,
			originalIdeology,
			order.PrewarCityIds,
			capitalCityId,
			0.45f);
	}

	/// <summary>
	/// Город принадлежит поглощённым вассальным землям либо исторической родине уже
	/// исчезнувшей державы. Такие земли нельзя мирно отпустить референдумом: путь к
	/// независимости для них — только восстание и война.
	/// </summary>
	public bool IsAbsorbedHomelandCity(int currentKingdomId, int cityId)
	{
		if (currentKingdomId <= 0 || cityId <= 0)
		{
			return false;
		}
		for (int i = 0; i < _unions.Count; i++)
		{
			Union union = _unions[i];
			if (union.LeaderId != currentKingdomId)
			{
				continue;
			}
			if (union.Type == UnionType.Vassal)
			{
				for (int m = 0; m < union.Members.Count; m++)
				{
					UnionMember member = union.Members[m];
					if (!member.IsLeader && member.CityIds != null && member.CityIds.Contains(cityId))
					{
						return true;
					}
				}
			}
			for (int c = 0; c < union.AbsorbedChildren.Count; c++)
			{
				if (union.AbsorbedChildren[c].CityIds != null && union.AbsorbedChildren[c].CityIds.Contains(cityId))
				{
					return true;
				}
			}
		}
		CityHomelandRecord? rec = CityHomelands.Get(cityId);
		return rec != null
			&& rec.OriginalKingdomId > 0
			&& rec.OriginalKingdomId != currentKingdomId
			&& GetKingdom(rec.OriginalKingdomId) == null;
	}

	private KingdomPolitics? LookupUnionMemberPolitics(Union union, int memberId)
	{
		KingdomPolitics? live = GetKingdom(memberId);
		if (live != null)
		{
			return live;
		}
		if (union == null || union.Type != UnionType.Vassal || memberId == union.LeaderId)
		{
			return null;
		}
		UnionMember? member = union.GetMember(memberId);
		if (member == null || member.CityIds == null || member.CityIds.Count == 0)
		{
			return null;
		}
		KingdomPolitics? suzerain = GetKingdom(union.LeaderId);
		if (suzerain == null)
		{
			return null;
		}
		int ownedBySuzerain = 0;
		float dissentSum = 0f;
		for (int i = 0; i < member.CityIds.Count; i++)
		{
			CityPolitics? city = GetCity(member.CityIds[i]);
			if (city != null && city.KingdomId == union.LeaderId)
			{
				ownedBySuzerain++;
				dissentSum += city.AvgDissent;
			}
		}
		if (ownedBySuzerain == 0)
		{
			return null;
		}
		float avgDissent = dissentSum / ownedBySuzerain;
		return new KingdomPolitics
		{
			KingdomId = memberId,
			Ideology = member.OriginalIdeology,
			Legitimacy = suzerain.Legitimacy,
			AvgDissent = Math.Max(avgDissent, suzerain.AvgDissent),
			Starving = suzerain.Starving
		};
	}

	private bool TryQueueVassalIndependenceWar(Union union, UnionMember? member)
	{
		if (union == null || union.Type != UnionType.Vassal || member == null || member.CityIds == null || member.CityIds.Count == 0)
		{
			return false;
		}
		int suzerainId = union.LeaderId;
		List<int> suzerainCities = CityIdsOf(suzerainId);
		if (suzerainCities.Count < 2)
		{
			return false;
		}
		int suzerainCap = CapitalCityIdOf(suzerainId);
		List<int> vassalCities = new List<int>();
		for (int i = 0; i < member.CityIds.Count; i++)
		{
			int cid = member.CityIds[i];
			if (cid != suzerainCap && suzerainCities.Contains(cid))
			{
				vassalCities.Add(cid);
			}
		}
		if (vassalCities.Count == 0 || suzerainCities.Count - vassalCities.Count < 1)
		{
			return false;
		}
		if (member.CapitalCityId > 0 && vassalCities.Contains(member.CapitalCityId) && vassalCities[0] != member.CapitalCityId)
		{
			vassalCities.Remove(member.CapitalCityId);
			vassalCities.Insert(0, member.CapitalCityId);
		}
		List<int> orderCities = new List<int>(1 + vassalCities.Count) { suzerainCap };
		orderCities.AddRange(vassalCities);
		string vassalName = !string.IsNullOrWhiteSpace(member.OriginalName) ? member.OriginalName : KingdomNameOf(member.KingdomId);
		AddPendingRestoration(new RestorationOrder
		{
			DefeatedId = member.KingdomId,
			VictorId = suzerainId,
			Mode = RestorationMode.Free,
			CitiesKeptByVictor = 1,
			DefeatedName = vassalName,
			Year = Year,
			PrewarCityIds = orderCities,
			IsNationalLiberation = true,
			LiberationPeaceful = false
		});
		Bus.Publish(new PolEvent(PolEventType.VassalRebellion, "RP_EV_VASSAL_REBELLION")
		{
			Year = Year,
			Severity = Severity.Critical,
			KingdomId = member.KingdomId
		}.With("kingdom", string.IsNullOrWhiteSpace(vassalName) ? ("#" + member.KingdomId) : vassalName)
			.With("suzerain", KingdomNameOf(suzerainId))
			.With("confirmed", "0"));
		return true;
	}

	private void ProcessUnions()
	{
		for (int num = _unions.Count - 1; num >= 0; num--)
		{
			Union union = _unions[num];
			Dictionary<int, UnionMember> snapshotMembers = new Dictionary<int, UnionMember>();
			for (int m = 0; m < union.Members.Count; m++)
			{
				snapshotMembers[union.Members[m].KingdomId] = union.Members[m];
			}
			List<int> list = RealPolitic.Core.Unions.Tick(union, id => LookupUnionMemberPolitics(union, id), UnionRules, Rng);
			for (int i = 0; i < list.Count; i++)
			{
				int leftId = list[i];
				if (union.Type == UnionType.Vassal && snapshotMembers.TryGetValue(leftId, out UnionMember? leftMember))
				{
					bool queuedAbsorbedRestoration = TryQueueVassalIndependenceWar(union, leftMember);
					if (!queuedAbsorbedRestoration && leftId != union.LeaderId && GetKingdom(union.LeaderId) != null)
					{
						// Живой отдельный вассал не может просто исчезнуть из унии
						// по итогам одобрения: его выход — война за независимость.
						// Поглощённый вассал выше уже получил приказ вооружённого
						// восстановления по запомненным городам и сюда не попадает.
						KingdomPolitics liveVassal = GetKingdom(leftId);
							if (liveVassal != null && CityIdsOf(leftId).Count > 0)
							{
								QueueIndependenceWar(leftId, union.LeaderId);
								Bus.Publish(new PolEvent(PolEventType.VassalRebellion, "RP_EV_VASSAL_REBELLION")
								{
									Year = Year,
									Severity = Severity.Critical,
									KingdomId = leftId
								}.With("kingdom", KingdomNameOf(leftId))
									.With("suzerain", KingdomNameOf(union.LeaderId))
									.With("confirmed", "0"));
							}
					}
				}
				Bus.Publish(new PolEvent(PolEventType.UnionMemberLeft, "RP_EV_UNION_MEMBER_LEFT")
				{
					Year = Year,
					Severity = Severity.Major,
					KingdomId = leftId
				}.With("union", union.DisplayNameOrKey).With("member", leftId));
			}
			bool leaderAlive = GetKingdom(union.LeaderId) != null;
			bool shouldDissolve = !leaderAlive || (union.Type == UnionType.Merger ? union.TotalSize <= 1 : union.MemberCount <= 1);
			if (shouldDissolve)
			{
				_unions.RemoveAt(num);
				Bus.Publish(new PolEvent(PolEventType.UnionDissolved, "RP_EV_UNION_DISSOLVED")
				{
					Year = Year,
					Severity = Severity.Critical,
					KingdomId = union.LeaderId
				}.With("name", union.DisplayNameOrKey).With("cohesion", union.Cohesion));
			}
		}
	}

	public void ClearEvents()
	{
		Bus.Clear();
	}

	public void SetBaseline(bool value)
	{
		BaselineRecorded = value;
	}

	public void SetYear(int year)
	{
		Year = year;
		DiplomaticMemories.PruneExpired(year);
	}

	public void SetLastRuler(int kingdomId, RulerRef? ruler)
	{
		if (ruler != null)
		{
			_lastRuler[kingdomId] = ruler.ActorId;
			_lastRulerRef[kingdomId] = ruler;
		}
		else
		{
			_lastRuler.Remove(kingdomId);
			_lastRulerRef.Remove(kingdomId);
		}
	}

	private void ProcessKingdom(KingdomInput input)
	{
		KingdomPolitics kingdomPolitics = _kingdoms[input.KingdomId];
		IdeologyType ideologyAtProcessStart = kingdomPolitics.Ideology;
		kingdomPolitics.Union = UnionOf(input.KingdomId);
		// Страж эпохи: анахронистичный строй (из старых сейвов) отодвигается к ближайшему
		// допустимому для данной эпохи — мир никогда не деградирует ниже допустимых форм.
		if (kingdomPolitics.Ideology != 0) // пропускаем IdeologyType.None-подобные
		{
			IdeologyEpoch epochNow = EraRules.OfYear(Year, Params);
			if (!EraRules.Allowed(kingdomPolitics.Ideology, epochNow))
			{
				IdeologyType corrected = EraRules.NearestAllowed(kingdomPolitics.Ideology, epochNow);
				if (corrected != kingdomPolitics.Ideology)
				{
					// Смена строя — целиком: закон преемственности и избирательный цикл
					// тоже принадлежат строю (иначе «племя» продолжало бы проводить выборы).
					kingdomPolitics.Ideology = corrected;
					kingdomPolitics.SuccessionLaw = SuccessionLaws.GetDefaultLaw(corrected);
					if (!SuccessionLaws.HasPeriodicCycle(corrected))
					{
						kingdomPolitics.Election = null;
					}
					RefreshConstitutionAfterIdeologyChange(kingdomPolitics);
					string eraCapName = input.Cities != null && input.Cities.Count > 0 ? input.Cities[0].Name : string.Empty;
					string eraCapBiome = input.Cities != null && input.Cities.Count > 0 ? input.Cities[0].BiomeId : string.Empty;
					string eraCapCulture = input.Cities != null && input.Cities.Count > 0 ? input.Cities[0].CultureName : string.Empty;
					EnsureIdeologyStateName(kingdomPolitics, input.Name, eraCapName, eraCapBiome, eraCapCulture);
				}
			}
		}
		// Этап 26: конституция — от идеологии+эпохи; у держав старых сейвов (v13 ниже)
		// её ещё нет — выписываем дефолт при первом тике.
		if (Params.EnableConstitution && kingdomPolitics.Constitution == null)
		{
			kingdomPolitics.Constitution = CreateConstitution(kingdomPolitics.Ideology, EraRules.OfYear(Year, Params));
		}
		if (_pendingRenameOverrides.TryGetValue(input.KingdomId, out string forcedName))
		{
			_kingdomNames[input.KingdomId] = forcedName;
			if (string.Equals(input.Name, forcedName, StringComparison.Ordinal))
			{
				_pendingRenameOverrides.Remove(input.KingdomId);
			}
		}
		else if (!string.IsNullOrEmpty(input.Name))
		{
			_kingdomNames[input.KingdomId] = input.Name;
		}
		if (input.Relations == null)
		{
			input.Relations = new List<RelationInfo>();
		}
		float num = 0f;
		int num2 = 0;
		foreach (CityPolitics value5 in _cities.Values)
		{
			if (value5.KingdomId == input.KingdomId && _actorsByCity.TryGetValue(value5.CityId, out var value))
			{
				RecomputeCity(value5, value, kingdomPolitics, out var dissentSum);
				num += dissentSum;
				num2 += value.Count;
			}
		}
		int num3 = 0;
		foreach (ActorEntry value6 in _actors.Values)
		{
			if (value6.KingdomId == input.KingdomId)
			{
				num3++;
			}
		}
		if (num3 > 0)
		{
			kingdomPolitics.Census.Clear();
			foreach (ActorEntry value6 in _actors.Values)
			{
				if (value6.KingdomId == input.KingdomId)
				{
					kingdomPolitics.Census.TryGetValue(value6.Politics.Ideology, out var value2);
					kingdomPolitics.Census[value6.Politics.Ideology] = value2 + 1;
				}
			}
			KingdomSimulation.UpdateCensusDerived(kingdomPolitics, num, num2);
		}
		else if (kingdomPolitics.Population == 0)
		{
			KingdomSimulation.UpdateCensusDerived(kingdomPolitics, 0f, 0);
		}
		TribalCivilization.TickTribe(this, kingdomPolitics, input, Year);
		CivicSocietyEngine.TickMovements(kingdomPolitics, this, Year);
		NationalLiberationEngine.TickKingdom(kingdomPolitics, this, Year);
		_neighborScratch.Clear();
		for (int i = 0; i < input.Relations.Count; i++)
		{
			int otherId = input.Relations[i].OtherId;
			if (!_neighborScratch.Contains(otherId))
			{
				_neighborScratch.Add(otherId);
			}
		}
		List<int> neighborIds = new List<int>(_neighborScratch);
		Dictionary<IdeologyType, float> dictionary = new Dictionary<IdeologyType, float>();
		foreach (ActorEntry value7 in _actors.Values)
		{
			if (value7.KingdomId == input.KingdomId)
			{
				CityPolitics city = GetCity(value7.CityId);
				float num4 = ActorSimulation.TickActor(value7, kingdomPolitics, city, Params, NeighborIdeology, NeighborMissionary, neighborIds, Rng, Bus, Year);
				if (num4 > 0f)
				{
					IdeologyType conversionTarget = value7.Politics.ConversionTarget;
					dictionary.TryGetValue(conversionTarget, out var value3);
					dictionary[conversionTarget] = value3 + num4;
				}
			}
		}
		DetectNovelties(input);
		HandleSuccession(kingdomPolitics, input);
		TickDynasties(kingdomPolitics, input);
		IdeologyType ideologyBeforeTick = kingdomPolitics.Ideology;
		CivilWarOutcome? civilWarOutcome = KingdomSimulation.TickState(kingdomPolitics, input, Params, Bus, Year, NeighborMissionary, NeighborIdeology, Rng, Deaths, BuildCivilWarContext(input));
		if (civilWarOutcome != null)
		{
			ApplyCivilWarOutcome(kingdomPolitics, input, civilWarOutcome, Year);
		}
		else if (kingdomPolitics.CivilWar != null && kingdomPolitics.CivilWar.RebelKingdomId == 0)
		{
			// Мятеж только что вспыхнул: повстанцы сразу образуют своё государство и воюют по-настоящему.
			StartRebelKingdom(kingdomPolitics, input, Year);
		}
		// Революция, мирное принятие строя, воля вече или победа мятежа сменили идеологию —
		// конституция должна соответствовать новому строю (иначе «монархия» с парламентской
		// формой правления и выборами, или «демократия» с наследственным троном).
			if (Year >= (Params?.TribalDeclineYear ?? 200)
				&& kingdomPolitics.Ideology != ideologyBeforeTick
				&& TribalCivilization.NeedsCivilization(kingdomPolitics.Ideology))
			{
				// После 200-го года нельзя заново принять ранний строй: ни цивилизованная держава
				// не скатывается в племя/каганат, ни племя не «цивилизуется» в каганат.
				kingdomPolitics.Ideology = ideologyBeforeTick;
			}
		if (kingdomPolitics.Ideology != ideologyBeforeTick)
		{
			string capName = input.Cities != null && input.Cities.Count > 0 ? input.Cities[0].Name : string.Empty;
			string capBiome = input.Cities != null && input.Cities.Count > 0 ? input.Cities[0].BiomeId : string.Empty;
			string capCulture = input.Cities != null && input.Cities.Count > 0 ? input.Cities[0].CultureName : string.Empty;
				bool civilizedFromEarlyOrder = TribalCivilization.NeedsCivilization(ideologyBeforeTick)
					&& !TribalCivilization.NeedsCivilization(kingdomPolitics.Ideology);
				bool handledCivilizedName = false;
				if (civilizedFromEarlyOrder)
				{
					kingdomPolitics.WasCivilized = true;
					kingdomPolitics.LastCivilizedYear = Year;
					if (ideologyBeforeTick == IdeologyType.Tribalism && kingdomPolitics.HasTribalEthnonym)
					{
						string civName = TribalCivilization.GenerateCivilizedStateName(
							KingdomNameOf(kingdomPolitics.KingdomId),
							capName,
							kingdomPolitics.Ideology,
							kingdomPolitics.KingdomId,
							Year);
						kingdomPolitics.HasTribalEthnonym = false;
						RememberKingdomName(kingdomPolitics.KingdomId, civName);
						SetPendingRenameOverride(kingdomPolitics.KingdomId, civName);
						handledCivilizedName = true;
					}
				}
				if (!handledCivilizedName && _kingdomNames.TryGetValue(kingdomPolitics.KingdomId, out string? existingName) && !string.IsNullOrWhiteSpace(existingName))
			{
				AssignIdeologyStateName(kingdomPolitics, existingName, capName, capBiome, capCulture);
			}
			RefreshConstitutionAfterIdeologyChange(kingdomPolitics);
		}
		else if (kingdomPolitics.CivilWar == null && kingdomPolitics.Revolution == null)
		{
			TryProclaimEmpire(kingdomPolitics, input);
			// 0.24, статья XIV «Об импичменте»: выборная держава может досрочно отозвать главу
			// (уровень поддержки < 50 — у статьи есть за что сработать).
			TickImpeachment(kingdomPolitics);
			// Этап 26.2: поправки — правитель (по характеру) или народ (при кризисе) правят одну статью.
			bool chambersInCharge = CityIdsOf(kingdomPolitics.KingdomId).Count > 0;
			ConstitutionAmendments.Tick(kingdomPolitics, KingdomNameOf(kingdomPolitics.KingdomId), Year, Params, Rng, Bus, chambersInCharge);
		}
		{
			// Строй мог смениться и вне ProcessKingdom (восстановление, гражданские процедуры,
			// игровой слой) ещё до этого тика. Ensure ничего не делает, если имя уже является
			// одной из 20+ форм текущего строя, поэтому проверка безопасна каждый год.
			string finalCapName = input.Cities != null && input.Cities.Count > 0 ? input.Cities[0].Name : string.Empty;
			string finalCapBiome = input.Cities != null && input.Cities.Count > 0 ? input.Cities[0].BiomeId : string.Empty;
			string finalCapCulture = input.Cities != null && input.Cities.Count > 0 ? input.Cities[0].CultureName : string.Empty;
			EnsureIdeologyStateName(kingdomPolitics, input.Name, finalCapName, finalCapBiome, finalCapCulture);
		}
		TickWarFatigue(kingdomPolitics, input);
		TickEconomy(kingdomPolitics, input);
		if (kingdomPolitics.Ruler != null)
		{
			_lastRuler[kingdomPolitics.KingdomId] = kingdomPolitics.Ruler.ActorId;
			_lastRulerRef[kingdomPolitics.KingdomId] = kingdomPolitics.Ruler;
		}
	}

	/// <summary>
	/// 0.31: Крупная и легитимная монархия начиная с Нового времени (150+ год) может провозгласить себя Империей.
	/// </summary>
	private void TryProclaimEmpire(KingdomPolitics state, KingdomInput input)
	{
		if (state == null || state.Ideology != IdeologyType.Monarchy)
		{
			return;
		}
		IdeologyEpoch era = EraRules.OfYear(Year, Params);
		if (!EraRules.Allowed(IdeologyType.Empire, era))
		{
			return;
		}
		int cityCount = input.Cities != null ? input.Cities.Count : CityIdsOf(state.KingdomId).Count;
		if (cityCount < 3 || state.Legitimacy < 74f || state.Starving || state.AvgDissent > 0.28f)
		{
			return;
		}
		if (Rng != null && Rng.NextDouble() >= 0.08)
		{
			return;
		}
		IdeologyType oldIdeology = state.Ideology;
		state.Ideology = IdeologyType.Empire;
		state.SuccessionLaw = SuccessionLaws.GetDefaultLaw(IdeologyType.Empire);
		RefreshConstitutionAfterIdeologyChange(state);

		string capName = input.Cities != null && input.Cities.Count > 0 ? input.Cities[0].Name : string.Empty;
		string capBiome = input.Cities != null && input.Cities.Count > 0 ? input.Cities[0].BiomeId : string.Empty;
		string capCulture = input.Cities != null && input.Cities.Count > 0 ? input.Cities[0].CultureName : string.Empty;
		string oldName = KingdomNameOf(state.KingdomId);
		if (_kingdomNames.TryGetValue(state.KingdomId, out string? existingName) && !string.IsNullOrWhiteSpace(existingName))
		{
			AssignIdeologyStateName(state, existingName, capName, capBiome, capCulture);
		}
		string newName = KingdomNameOf(state.KingdomId);

		Bus.Publish(new PolEvent(PolEventType.KingdomAdoptedIdeology, "RP_EV_ADOPTED_IDEOLOGY")
		{
			Year = Year,
			Severity = Severity.Critical,
			KingdomId = state.KingdomId
		}.With("old_name", oldName)
		 .With("kingdom", newName)
		 .With("old", Ideologies.NameKey(oldIdeology))
		 .With("new", Ideologies.NameKey(IdeologyType.Empire)));
	}

	/// <summary>0.24, статья XIV «Об импичменте»: в выборной державе при поддержке ниже половины
	/// законодательное собрание может досрочно отозвать главу. Народное голосование требует половины
	/// народа, «серебряные короны» — половины лидеров городов; отказ избирать сурово бьёт по
	/// легитимности и согласию. Советующая конституция лишь нашептывает — без силы к смещению.</summary>
	private void TickImpeachment(KingdomPolitics state)
	{
		if (state?.Constitution == null
			|| state.Ruler == null
			|| !SuccessionLaws.HasPeriodicCycle(state.Ideology))
		{
			return; // статья живёт только в выборных державах
		}
		ImpeachmentLaw law = state.Constitution.Impeachment;
		if (law == ImpeachmentLaw.Forbidden)
		{
			return; // импичмент запрещён статьёй
		}
		// Основание: поддержка ниже половины — значит, против главы уже половина народа
		// (и, стало быть, половина градоначальников) — вотум проходим по обоим правилам.
		if (!ConstitutionGates.ImpeachmentDue(state, state.Support, holdsElections: true))
		{
			return;
		}
		// «Серебряные короны»: без второго градоначальника собрать половину голосов некому.
		if (law == ImpeachmentLaw.CityLeaders && CityIdsOf(state.KingdomId).Count < 2)
		{
			return;
		}
		// Перерыв между вотумами: один раз в два года — иначе смысл потерян.
		if (Year - state.LastImpeachmentYear < 2)
		{
			return;
		}
		double drive = (50.0 - state.Support) / 50.0;              // 0 при 50, 1 при 0
		double gate = 0.04 + drive * 0.06;                         // 4% → 10% в год
		if (SuccessionLaws.HasPeriodicCycle(state.SuccessionLaw))
		{
			gate *= 1.6; // там, где выборы — не формальность, отзыв жёстче
		}
		if (Rng.NextDouble() >= gate)
		{
			return; // вотума в этом году не будет
		}
		state.LastImpeachmentYear = Year;
		string rulerName = state.Ruler.Name ?? string.Empty;
		state.Legitimacy = Math.Max(5f, state.Legitimacy * 0.55f);
		state.AvgDissent = Math.Min(1f, state.AvgDissent + 0.08f);
		// Глава отозван волей закона; коллегия изберёт преемника в свой срок.
		state.Ruler = null;
		Bus.Publish(new PolEvent(PolEventType.Impeached, "RP_EV_IMPEACHED")
		{
			Year = Year,
			Severity = Severity.Major,
			KingdomId = state.KingdomId
		}.With("kingdom", KingdomNameOf(state.KingdomId))
			.With("ruler", rulerName));
	}

	/// <summary>Этап 22: годовой тик усталости от войны. Растёт в войне (строй решает, как болезненно),
	/// тает в мире; высокая усталость давит легитимность, поддержку и настроение подданных.</summary>
	private void TickWarFatigue(KingdomPolitics state, KingdomInput input)
	{
		if (!Params.EnableWarPeace)
		{
			state.WarFatigue = 0f;
			return;
		}
		if (input.AtWar)
		{
			state.WarFatigue = Math.Min(100f,
				state.WarFatigue + Params.WarFatigueRate * WarFatigueDefs.IdeologyFactor(state.Ideology));
		}
		else
		{
			state.WarFatigue = Math.Max(0f, state.WarFatigue - Params.WarFatigueDecay);
		}
		if (state.WarFatigue >= 60f)
		{
			bool breaking = state.WarFatigue >= 85f;
			state.Legitimacy = KingdomSimulation.Clamp(state.Legitimacy - (breaking ? 2f : 1f), 0f, 100f);
			state.AvgDissent = KingdomSimulation.Clamp(state.AvgDissent + (breaking ? 0.02f : 0.01f), 0f, 1f);
			if (breaking)
			{
				state.Support = KingdomSimulation.Clamp(state.Support - 1f, 0f, 100f);
			}
		}
	}

	/// <summary>Годовой ход экономики: налоги, торговля, благосостояние, кризисы.</summary>
	private void TickEconomy(KingdomPolitics state, KingdomInput input)
	{
		EconomyInput economyInput = BuildEconomyInput(state, input);
		EconomyReport report = Economy.Tick(state, economyInput, Params, Trade, Year, Rng, Bus);
		if (Params.EnableEconomy)
		{
			_economyReports[state.KingdomId] = report;
			if (EconomyEffectApplier != null && (report.GoldDelta != 0f || report.FoodDelta != 0f || report.HappinessDelta != 0f))
			{
				try
				{
					EconomyEffectApplier(state, report);
				}
				catch (Exception)
				{
					// Игровое API может быть недоступно: экономика продолжается в ядре, мир не трогаем.
				}
			}
		}
	}

	/// <summary>Собирает экономический снимок: свои показатели и партнёры по торговле.</summary>
	private EconomyInput BuildEconomyInput(KingdomPolitics state, KingdomInput input)
	{
		EconomyInput economyInput = new EconomyInput
		{
			KingdomId = input.KingdomId,
			Name = input.Name ?? string.Empty,
			Cities = input.Cities?.Count ?? 0,
			Population = input.Population,
			Warriors = input.Warriors,
			AtWar = input.AtWar,
			Starving = input.Starving,
			Gold = input.Gold,
			Ideology = state.Ideology,
			Legitimacy = state.Legitimacy,
			AvgDissent = state.AvgDissent,
			Support = state.Support,
			CivilWarActive = state.CivilWar != null,
			Partners = new List<EconomyPartner>()
		};
		if (input.Relations != null)
		{
			for (int i = 0; i < input.Relations.Count; i++)
			{
				RelationInfo relation = input.Relations[i];
				if (!_kingdoms.TryGetValue(relation.OtherId, out var partner))
				{
					continue;
				}
				int cities = 0;
				foreach (CityPolitics value in _cities.Values)
				{
					if (value.KingdomId == relation.OtherId)
					{
						cities++;
					}
				}
				Union? partnerUnion = UnionOf(relation.OtherId);
				economyInput.Partners.Add(new EconomyPartner
				{
					KingdomId = relation.OtherId,
					Name = KingdomNameOf(relation.OtherId),
					AtWar = relation.Relation == RelationType.War,
					Allied = relation.Relation == RelationType.Alliance || partnerUnion != null,
					TradeMultiplier = PartnerTradeMultiplier(relation.Relation, partnerUnion, input.KingdomId, relation.OtherId),
					Adjacent = relation.Adjacent,
					Cities = cities,
					Tariff = partner.Economy?.Tariff ?? 0f
				});
			}
		}
		return economyInput;
	}

	private void RecomputeCity(CityPolitics city, List<int> members, KingdomPolitics state, out float dissentSum)
	{
		dissentSum = 0f;
		if (members.Count == 0)
		{
			city.Share = 0f;
			return;
		}
		Dictionary<IdeologyType, int> dictionary = new Dictionary<IdeologyType, int>();
		int num = 0;
		IdeologyType ideologyType = city.Dominant;
		for (int i = 0; i < members.Count; i++)
		{
			ActorEntry actorEntry = _actors[members[i]];
			dissentSum += actorEntry.Politics.Dissent;
			IdeologyType ideology = actorEntry.Politics.Ideology;
			dictionary.TryGetValue(ideology, out var value);
			dictionary[ideology] = value + 1;
			if (dictionary[ideology] > num)
			{
				num = dictionary[ideology];
				ideologyType = ideology;
			}
		}
		city.Share = (float)num / (float)members.Count;
		city.AvgDissent = dissentSum / (float)members.Count;
		if (ideologyType != city.Dominant)
		{
			float share = city.Share;
			IdeologyType dominant = city.Dominant;
			if (city.Share >= 0.5f)
			{
				city.Dominant = ideologyType;
				Bus.Publish(new PolEvent(PolEventType.CityIdeologyShift, "RP_EV_CITY_SHIFT")
				{
					Year = Year,
					Severity = Severity.Minor,
					KingdomId = state.KingdomId,
					CityId = city.CityId
				}.With("old", Ideologies.NameKey(dominant)).With("new", Ideologies.NameKey(city.Dominant)).With("share", (int)(share * 100f)));
			}
		}
	}

	private void DetectNovelties(KingdomInput input)
	{
		if (input.Cities == null)
		{
			return;
		}
		for (int i = 0; i < input.Cities.Count; i++)
		{
			CityInput cityInput = input.Cities[i];
			TrackNovelty(_religionOrigin, cityInput.ReligionId, cityInput.CityId, cityInput.KingdomId, cityInput.ReligionName, "religion", PolEventType.NewReligion, "RP_EV_NEW_RELIGION");
			TrackNovelty(_languageOrigin, cityInput.LanguageId, cityInput.CityId, cityInput.KingdomId, cityInput.LanguageName, "language", PolEventType.NewLanguage, "RP_EV_NEW_LANGUAGE");
			TrackNovelty(_cultureOrigin, cityInput.CultureId, cityInput.CityId, cityInput.KingdomId, cityInput.CultureName, "culture", PolEventType.NewCulture, "RP_EV_NEW_CULTURE");
		}
	}

	private void TrackNovelty(Dictionary<int, int> origins, int id, int cityId, int kingdomId, string displayName, string paramName, PolEventType type, string locKey)
	{
		if (id != 0 && !origins.ContainsKey(id))
		{
			origins[id] = cityId;
			if (BaselineRecorded)
			{
				PolEvent polEvent = new PolEvent(type, locKey)
				{
					Year = Year,
					Severity = Severity.Major,
					CityId = cityId,
					KingdomId = kingdomId
				};
				// Идентификатор — для дедупликации, имя — для текста игроку.
			polEvent.With(paramName + "_id", id);
			if (!string.IsNullOrEmpty(displayName))
			{
				polEvent.With(paramName, displayName);
			}
			// «В королевстве/племени/теократии…» — форма зависит от идеологии государства.
			var kp = GetKingdom(kingdomId);
			polEvent.With("state", Ideologies.StateInKey(kp?.Ideology ?? IdeologyType.Monarchy));
			Bus.Publish(polEvent);
			}
		}
	}

	private void HandleSuccession(KingdomPolitics state, KingdomInput input)
	{
		int num = ((input.Ruler == null) ? (-1) : input.Ruler.ActorId);
		int value;
		bool num2 = _lastRuler.TryGetValue(state.KingdomId, out value);
		_lastRuler[state.KingdomId] = num;
		if (!num2 && input.Ruler != null)
		{
			// 0.23.11: трон занял регент за малолетнего наследника? Ядро не присваивает ему
			// статус законного правителя — личность и династия остаются за наследником
			// (известным через RegencyState). Пока корона у регента, state.Ruler не ведём.
			if (input.Ruler.IsRegent)
			{
				// Всё прочее (династия основания и фамилия) уже записано за предыдущим домом
				// либо запишется при возвращении короны наследнику.
				if (state.Regency == null)
				{
					state.Regency = new RegencyState();
				}
				if (!state.Regency.IsActive)
				{
					state.Regency.Start(Year, input.Ruler.Name, input.Ruler.ActorId);
				}
				state.Regency.RegentActorId = input.Ruler.ActorId;
				state.Regency.RegentName = input.Ruler.Name;
			}
			else
			{
				state.Ruler = input.Ruler;
				if (state.Ideology == IdeologyType.Monarchy && string.IsNullOrEmpty(state.RoyalClanName))
				{
					string rClan = input.Ruler.ClanName;
					state.RoyalClanName = !string.IsNullOrEmpty(rClan) ? rClan : (input.Ruler.Name + CoreText.L("RP_CLAN_SUFFIX", "ичи"));
					state.RoyalClanId = input.Ruler.ClanId;
					state.DynastyFounder = input.Ruler.Name;
					state.DynastyReignsCount = 1;
					state.DynastyStartYear = Year;
					// 0.23.2: фамилия — с первого правителя, а не после его смерти.
					EnsureDynastySurname(state);
				}
			}
		}
		bool dynastyShifted = false;
		bool inheritedByClaim = false;
		if (num2 && value != num)
		{
			// 0.23.11: трон занял регент (регентство за малолетнего наследника). Это НЕ
			// смена власти и НЕ новая династия: личность и дом остаются за наследником,
			// который вернётся на трон в 18 лет. Иначе регент-узурпатор объявлялся бы
			// «новым домом» и переписывал бы правящую династию (трагедия дома Вевович).
			// Маркер IsRegent — сигнал игрового слоя: корона физически у регента.
			if (input.Ruler != null && input.Ruler.IsRegent)
			{
				if (state.Regency == null)
				{
					state.Regency = new RegencyState();
				}
				if (!state.Regency.IsActive)
				{
					state.Regency.Start(Year, input.Ruler.Name, input.Ruler.ActorId);
				}
				state.Regency.RegentActorId = input.Ruler.ActorId;
				state.Regency.RegentName = input.Ruler.Name;
				// Легитимность малолетства уже режет собственный блок механики — не режем
				// здесь снова, чтобы не складывать штраф «смены власти» поверх регентства.
				return;
			}
			// Этап 19: новый правитель приносит новую личность (черту и амбиции).
			if (Params.EnableDynasties && input.Ruler != null)
			{
				state.Personality = RulerPersonality.Roll(DynastyRng);
			}
			// Этап 23: фамилия династии фиксируется при первом же преемнике монархии,
			// даже если имя клана игры было известно до появления механики фамилий.
			if (state.Ideology == IdeologyType.Monarchy && input.Ruler != null)
			{
				EnsureDynastySurname(state);
			}
			if (value >= 0)
			{
				state.Legitimacy = KingdomSimulation.Clamp(state.Legitimacy - 12f, 0f, 100f);
			}
			if (input.Ruler != null)
			{
				// Идеология государства = идеология короля: новый правитель приносит свои собственные
				// убеждения (как житель, за которым ядро следило), но только те, что возможны в эпохе мира.
				ActorEntry rulerEntry = GetActor(input.Ruler.ActorId);
				IdeologyType? rulerIdeology = rulerEntry?.Politics.Ideology;
				IdeologyType newRulerIdeology = ActorSimulation.SuccessorIdeology(state.Ideology, rulerIdeology, EraRules.OfYear(Year, Params), Year);
					if (TribalCivilization.NeedsCivilization(state.Ideology)
						&& !TribalCivilization.NeedsCivilization(newRulerIdeology))
					{
						// Племя или каганат не становится цивилизованным просто от смены правителя
						// без накопления ресурсов и признания держав: запоминаем выбранный
						// правителем цивилизованный ориентир для мирной цивилизации.
						state.PreferredCivilizedIdeology = newRulerIdeology;
						newRulerIdeology = state.Ideology;
					}
					else if (newRulerIdeology != state.Ideology
						&& TribalCivilization.NeedsCivilization(newRulerIdeology)
						&& Year >= (Params?.TribalDeclineYear ?? 200))
					{
						newRulerIdeology = state.Ideology;
					}
				bool female = input.Ruler.IsFemale;
				PolEvent polEvent = new PolEvent(PolEventType.Succession, "RP_EV_SUCCESSION")
				{
					Year = Year,
					Severity = Severity.Major,
					KingdomId = state.KingdomId,
					ActorId = input.Ruler.ActorId
				}.With("ruler", input.Ruler.Name)
					.With("title", Ideologies.RulerTitleKey(newRulerIdeology, female))
					.With("state", Ideologies.StateInKey(newRulerIdeology))
					// «новый/новая» — отдельный параметр (локальный артикль зависит от пола).
					.With("new", female ? "RP_ART_F" : "RP_ART_M")
					.With("ideology", Ideologies.NameKey(newRulerIdeology))
					.With("legitimacy", state.Legitimacy)
					.With("age", input.Ruler.Age);

				// Детали о предыдущем правителе (для подробной летописи): имя, титул, длительность правления.
				// Поддерживает междуцарствие (когда между правителями был промежуток без короля).
				RulerRef oldRuler = null;
				_lastRulerRef.TryGetValue(state.KingdomId, out oldRuler);
				if (oldRuler != null && oldRuler.ActorId != num && !string.IsNullOrEmpty(oldRuler.Name))
				{
					polEvent.With("old_ruler", oldRuler.Name).With("old_actor", oldRuler.ActorId)
						.With("old_title", Ideologies.RulerTitleKey(state.Ideology, oldRuler.IsFemale))
						.With("old_reign", Math.Max(0, Year - oldRuler.SinceYear + 1));
				}

				// Применение законов престолонаследия и династии монархии:
				if (state.Ideology == IdeologyType.Monarchy)
				{
					string rulerClan = input.Ruler.ClanName;
					if (string.IsNullOrEmpty(state.RoyalClanName))
					{
						state.RoyalClanName = !string.IsNullOrEmpty(rulerClan) ? rulerClan : (input.Ruler.Name + CoreText.L("RP_CLAN_SUFFIX", "ичи"));
						state.RoyalClanId = input.Ruler.ClanId;
						state.DynastyFounder = input.Ruler.Name;
						state.DynastyReignsCount = 1;
						state.DynastyStartYear = Year;
						EnsureDynastySurname(state);
						polEvent.With("dynasty", state.RoyalClanName).With("dynasty_reigns", 1);
					}
					else if (!string.IsNullOrEmpty(rulerClan) && string.Equals(rulerClan, state.RoyalClanName, StringComparison.OrdinalIgnoreCase))
					{
						// Преемственность внутри одной правящей династии:
						state.DynastyReignsCount++;
						polEvent.With("dynasty", state.RoyalClanName).With("dynasty_reigns", state.DynastyReignsCount);
					}
						else if (!string.IsNullOrEmpty(rulerClan) && !string.Equals(rulerClan, state.RoyalClanName, StringComparison.OrdinalIgnoreCase))
					{
						// Смена династии: прежний дом пресёкся или свергнут, трон занял новый клан!
						string oldDynasty = state.RoyalClanName;
						string newDynasty = rulerClan;
						long newClanId = input.Ruler.ClanId;
						// 0.24, статья XIII: прежний род запоминается для кровного права —
						// его наследник вправе вернуть венец без клейма узурпатора.
						long prevClanId = state.RoyalClanId;

						// Этап 19: династический брак даёт дому супруга претензию на корону.
						// Если претензия сработала, трон занимает дом супруга (а не случайный клан);
						// переход мягче, чем захват.
						bool inherited = false;
						KingdomPolitics spouse = Params.EnableDynasties && state.Marriage != null
							? GetKingdom(state.Marriage.PartnerId)
							: null;
						if (spouse != null && !string.IsNullOrEmpty(spouse.RoyalClanName))
						{
							float claim = 0.5f + GetRulerRelation(state.KingdomId, spouse.KingdomId) / 400f;
							if (Rng.NextDouble() < claim)
							{
								inherited = true;
								inheritedByClaim = true;
								newDynasty = spouse.RoyalClanName;
								newClanId = spouse.RoyalClanId;
								state.Legitimacy = Math.Max(10f, state.Legitimacy - 10f);
								state.AvgDissent = Math.Min(1f, state.AvgDissent + 0.08f);
								state.Mechanics.ThreatLevel = Math.Min(100f, state.Mechanics.ThreatLevel + 10f);
							}
						}
						if (!inherited)
						{
							// Падение легитимности и рост недовольства при смене правящего дома:
							state.Legitimacy = Math.Max(10f, state.Legitimacy - 25f);
							state.AvgDissent = Math.Min(1f, state.AvgDissent + 0.20f);
							state.Mechanics.ThreatLevel = Math.Min(100f, state.Mechanics.ThreatLevel + 30f);
						}
						// Брак пережил своё: корона либо ушла супругу, либо дом сменился посторонним кланом.
						state.Marriage = null;

						state.PreviousRoyalClanId = prevClanId;
						state.RecordRoyalClan(prevClanId);
						state.RoyalClanName = newDynasty;
						state.RoyalClanId = newClanId;
						state.DynastyFounder = input.Ruler.Name;
						state.DynastyReignsCount = 1;
						state.DynastyStartYear = Year;
						dynastyShifted = true;
						// 0.23.2: новый дом — новая фамилия (по его клану); старая не «прилипает» к трону.
						state.DynastySurname = string.Empty;
						state.ClanRenamedToSurname = false;
						EnsureDynastySurname(state);

						polEvent.With("dynasty_shift", "1")
							.With("old_dynasty", oldDynasty)
							.With("new_dynasty", newDynasty);

						Bus.Publish(new PolEvent(PolEventType.DynasticShift, "RP_EV_DYNASTY_SHIFT")
						{
							Year = Year,
							Severity = Severity.Critical,
							KingdomId = state.KingdomId,
							ActorId = input.Ruler.ActorId
						}.With("kingdom", input.Name ?? string.Empty)
							.With("ruler", input.Ruler.Name)
							.With("old_dynasty", oldDynasty)
							.With("new_dynasty", newDynasty));
					}

					switch (state.SuccessionLaw)
					{
					case SuccessionLaw.Salic:
						if (female)
						{
							state.Legitimacy = Math.Max(0f, state.Legitimacy - 20f);
							state.AvgDissent = Math.Min(1f, state.AvgDissent + 0.15f);
							polEvent.With("law_note", "RP_LAW_SALIC_VIOLATION");
						}
						else
						{
							state.Legitimacy = Math.Min(100f, state.Legitimacy + 5f);
						}
						break;
					case SuccessionLaw.Cognatic:
						if (female)
						{
							state.Legitimacy = Math.Min(100f, state.Legitimacy + 10f);
						}
						else
						{
							state.Legitimacy = Math.Min(100f, state.Legitimacy + 5f);
						}
						break;
					case SuccessionLaw.DynasticUnion:
						// 0.24: личная уния упразднена — закон оставляет только династический ярлык
						// наследования (брачная претензия описывается статьёй XIII «О кровных связях»).
						break;
					case SuccessionLaw.StrictAgnatic:
						if (female)
						{
							state.Legitimacy = Math.Max(0f, state.Legitimacy - 30f);
							state.Mechanics.ThreatLevel = Math.Min(100f, state.Mechanics.ThreatLevel + 35f);
						}
						else if (input.Ruler.Age < 18 && input.Ruler.Age > 0)
						{
							state.Legitimacy = Math.Max(0f, state.Legitimacy - 25f);
							state.Mechanics.ThreatLevel = Math.Min(100f, state.Mechanics.ThreatLevel + 25f);
						}
						else
						{
							state.Legitimacy = Math.Min(100f, state.Legitimacy + 15f);
						}
						break;
					}

				}

				// Периодические выборы при смене власти:
				if (state.Constitution?.Electoral==null && (SuccessionLaws.HasPeriodicCycle(state.SuccessionLaw) || SuccessionLaws.HasPeriodicCycle(state.Ideology)))
				{
					if (state.Election == null)
					{
						state.Election = new ElectionState();
					}
					// Смена главы в выборном строе оформляется как выборы этого года —
					// иначе конституционная проверка считала бы её узурпацией.
					state.Election.LastElectionYear = Year;
					PastPresidentRecord past = state.Election.FindPastPresident(input.Ruler.ActorId);
					if (past != null)
					{
						state.Election.TermsServed = Math.Min(ElectionSystem.MaxLifetimeTerms, past.TermsCount + 1);
						past.TermsCount = state.Election.TermsServed;
						state.Election.NextElectionYear = Year + ElectionSystem.TermYears;
						Bus.Publish(new PolEvent(PolEventType.DemocraticElection, ElectionSystem.GetReturnLocKey(state.Ideology))
						{
							Year = Year,
							Severity = Severity.Major,
							KingdomId = state.KingdomId,
							ActorId = input.Ruler.ActorId
						}.With("ruler", input.Ruler.Name).With("terms", state.Election.TermsServed).With("election_kind", "observed").With("next_election", state.Election.NextElectionYear));
					}
					else
					{
						state.Election.TermsServed = 1;
						state.Election.RecordPresidentTerm(input.Ruler.ActorId, input.Ruler.Name, 1);
						state.Election.NextElectionYear = Year + ElectionSystem.TermYears;
						Bus.Publish(new PolEvent(PolEventType.DemocraticElection, ElectionSystem.GetNewLocKey(state.Ideology))
						{
							Year = Year,
							Severity = Severity.Major,
							KingdomId = state.KingdomId,
							ActorId = input.Ruler.ActorId
						}.With("ruler", input.Ruler.Name).With("terms", 1).With("election_kind", "observed").With("next_election", state.Election.NextElectionYear));
					}
				}

				// Регентство при малолетнем правителе:
				if (input.Ruler.Age < 18 && input.Ruler.Age > 0)
				{
					if (state.Regency == null)
					{
						state.Regency = new RegencyState();
					}
					if (!state.Regency.IsActive)
					{
						state.Regency.Start(Year, RegencyState.CouncilKey, 0);
						state.Legitimacy = Math.Max(5f, state.Legitimacy * 0.75f);
						Bus.Publish(new PolEvent(PolEventType.RegencyStarted, "RP_EV_REGENCY_START")
						{
							Year = Year,
							Severity = Severity.Major,
							KingdomId = state.KingdomId,
							ActorId = input.Ruler.ActorId
						}.With("ruler", input.Ruler.Name).With("age", input.Ruler.Age));
					}
				}
				else if (state.Regency != null && state.Regency.IsActive)
				{
					state.Regency.End();
					state.Legitimacy = Math.Min(100f, state.Legitimacy * 1.33f);
					Bus.Publish(new PolEvent(PolEventType.RegencyEnded, "RP_EV_REGENCY_END")
					{
						Year = Year,
						Severity = Severity.Major,
						KingdomId = state.KingdomId,
						ActorId = input.Ruler.ActorId
					}.With("ruler", input.Ruler.Name));
				}

				Bus.Publish(polEvent);
				// Этап 26: смена правителя фактически = узурпация, если конституция обязывает иной порядок.
				CheckConstitutionalSuccession(state, input, dynastyShifted, inheritedByClaim);
				if (newRulerIdeology != state.Ideology)
				{
					IdeologyType oldIdeology = state.Ideology;
					string oldName = KingdomNameOf(state.KingdomId);
					state.Ideology = newRulerIdeology;
					RefreshConstitutionAfterIdeologyChange(state);
					state.SuccessionLaw = SuccessionLaws.GetDefaultLaw(state.Ideology);
					if (SuccessionLaws.HasPeriodicCycle(state.Ideology))
					{
						state.Election = ElectionSystem.ReformedElectoralState(state.Election, Year);
						state.Election.RecordPresidentTerm(input.Ruler.ActorId, input.Ruler.Name, 1);
					}
					string successionCapName = input.Cities != null && input.Cities.Count > 0 ? input.Cities[0].Name : string.Empty;
					string successionCapBiome = input.Cities != null && input.Cities.Count > 0 ? input.Cities[0].BiomeId : string.Empty;
					string successionCapCulture = input.Cities != null && input.Cities.Count > 0 ? input.Cities[0].CultureName : string.Empty;
					string renamed = EnsureIdeologyStateName(state, oldName, successionCapName, successionCapBiome, successionCapCulture);
					Bus.Publish(new PolEvent(PolEventType.KingdomAdoptedIdeology, "RP_EV_ADOPTED_IDEOLOGY")
					{
						Year = Year,
						Severity = Severity.Major,
						KingdomId = state.KingdomId
					}.With("old_name", oldName)
					 .With("kingdom", renamed)
					 .With("old", Ideologies.NameKey(oldIdeology))
					 .With("new", Ideologies.NameKey(state.Ideology)));
				}
			}
		}
		if (input.Ruler != null)
		{
			// Сохраняем ссылку на текущего правителя: при следующей смене он станет «предыдущим».
			_lastRulerRef[state.KingdomId] = input.Ruler;
		}
	}

	// ------------------------------------------------------------------ гражданские войны

	/// <summary>Собирает контекст механики из данных, которые знает ядро (города, их взгляды и недовольство).</summary>
	private CivilWarContext BuildCivilWarContext(KingdomInput input)
	{
		CivilWarContext ctx = new CivilWarContext
		{
			CityIdeology = cityId => GetCity(cityId)?.Dominant ?? IdeologyType.Monarchy,
			CityUnrest = cityId => GetCity(cityId)?.AvgDissent ?? 0f,
			CityOwner = cityId => GetCity(cityId)?.KingdomId ?? 0,
			KingdomCityCount = kingdomId =>
			{
				if (!_kingdoms.ContainsKey(kingdomId))
				{
					return 0;
				}
				int n = 0;
				foreach (CityPolitics c in _cities.Values)
				{
					if (c.KingdomId == kingdomId)
					{
						n++;
					}
				}
				return n;
			}
		};
		if (RebelLeaderResolver != null)
		{
			ctx.RebelLeaderName = (kingdomId, cityId) => RebelLeaderResolver(kingdomId, cityId).Name ?? string.Empty;
			ctx.RebelLeaderIsFemale = (kingdomId, cityId) => RebelLeaderResolver(kingdomId, cityId).IsFemale;
			ctx.RebelLeaderActorId = (kingdomId, cityId) => RebelLeaderResolver(kingdomId, cityId).ActorId;
		}
		return ctx;
	}

	/// <summary>
	/// Начало гражданской войны: мятежные города образуют государство повстанцев в ядре
	/// (временный id), а игровой слой создаёт настоящее королевство и объявляет войну.
	/// Если создать государство нельзя (отделения выключены), мятеж гаснет в зародыше.
	/// </summary>
	private void StartRebelKingdom(KingdomPolitics state, KingdomInput input, int year)
	{
		CivilWarState cw = state.CivilWar;
		if (cw == null)
		{
			return;
		}
		if (!Params.AllowSecession)
		{
			state.CivilWar = null;
			return;
		}
		int newId = AllocateKingdomId();
		KingdomPolitics rebels = CreateBreakawayKingdom(newId, state, cw.RebelCityIds, cw.RebelIdeology, year);
		cw.RebelKingdomId = rebels.KingdomId;
		if (cw.RebelLeaderActorId != 0)
		{
			rebels.Ruler = new RulerRef
			{
				ActorId = cw.RebelLeaderActorId,
				Name = cw.RebelLeaderName ?? string.Empty,
				IsFemale = cw.RebelLeaderIsFemale,
				KingdomId = rebels.KingdomId,
				SinceYear = year,
				Age = 30
			};
		}
		PendingSecessions.Add(new PendingSecession
		{
			ParentKingdomId = state.KingdomId,
			ProvisionalKingdomId = rebels.KingdomId,
			CityIds = new List<int>(cw.RebelCityIds),
			Ideology = cw.RebelIdeology,
			Cause = cw.Cause,
			LeaderName = cw.RebelLeaderName,
			Year = year,
			DeclareWar = true
		});
	}

	/// <summary>
	/// Применяет исход гражданской войны, решённый настоящей войной в мире:
	/// подавление (государство повстанцев исчезло), переворот (повстанцы взяли столицу —
	/// вся держава переходит к ним) или отделение (мир — повстанцы остаются независимыми).
	/// </summary>
	private void ApplyCivilWarOutcome(KingdomPolitics state, KingdomInput input, CivilWarOutcome outcome, int year)
	{
		CivilWarState cw = state.CivilWar;
		if (cw == null)
		{
			return;
		}
		string kingdomName = !string.IsNullOrEmpty(input.Name) ? input.Name : KingdomNameOf(state.KingdomId);
		int rebelCities = cw.RebelCityIds.Count;
		int capitalCity = cw.RebelCapitalCityId;
		string causeKey = CivilWarCauses.LocKey(cw.Cause);
		state.CivilWar = null;
		state.LastCivilWarYear = year;
		state.CivilWarsTotal++;
		KingdomPolitics? rebels = GetKingdom(cw.RebelKingdomId);

		switch (outcome.Resolution)
		{
			case CivilWarResolution.LoyalistVictory:
				state.Legitimacy = Math.Min(100f, state.Legitimacy + 18f);
				state.AvgDissent = Math.Max(0f, state.AvgDissent - 0.12f);
				state.Mechanics.ThreatLevel = Math.Max(0f, state.Mechanics.ThreatLevel * 0.5f);
				foreach (CityPolitics c in _cities.Values)
				{
					if (c.KingdomId == state.KingdomId || (cw.RebelKingdomId != 0 && c.KingdomId == cw.RebelKingdomId) || cw.RebelCityIds.Contains(c.CityId))
					{
						c.KingdomId = state.KingdomId;
						c.Dominant = state.Ideology;
						c.AvgDissent = Math.Max(0f, c.AvgDissent * 0.3f);
					}
				}
				foreach (ActorEntry actor in _actors.Values)
				{
					if (actor.KingdomId == state.KingdomId || (cw.RebelKingdomId != 0 && actor.KingdomId == cw.RebelKingdomId))
					{
						actor.KingdomId = state.KingdomId;
						actor.Politics.Ideology = state.Ideology;
						actor.Politics.Dissent = Math.Max(0f, actor.Politics.Dissent * 0.3f);
					}
				}
				if (rebels != null)
				{
					RecordMergedIntoSuzerain(rebels.KingdomId);
					RemoveKingdom(rebels.KingdomId);
				}
				break;
			case CivilWarResolution.RebelTakeover:
			{
				IdeologyType oldIdeology = state.Ideology;
				string preTakeoverName = kingdomName;
					if (TribalCivilization.NeedsCivilization(oldIdeology)
						&& !TribalCivilization.NeedsCivilization(outcome.RebelIdeology)
						&& (oldIdeology == IdeologyType.Khaganate || state.HasTribalEthnonym))
				{
						string capName = input.Cities != null && input.Cities.Count > 0 ? input.Cities[0].Name : string.Empty;
						kingdomName = oldIdeology == IdeologyType.Khaganate
							? IdeologyStateNames.GenerateStateName(outcome.RebelIdeology, kingdomName, capName, state.KingdomId, year)
							: TribalCivilization.GenerateCivilizedStateName(kingdomName, capName, outcome.RebelIdeology, state.KingdomId, year);
					state.HasTribalEthnonym = false;
					state.WasCivilized = true;
					state.LastCivilizedYear = year;
					RememberKingdomName(state.KingdomId, kingdomName);
				}
				RulerRef? ruler = state.Ruler;
				if (ruler != null)
				{
					outcome.DeposedRulerDeath = KingdomSimulation.RecordRulerDeath(Deaths, Bus, state, ruler, year, DeathCause.Executed, "RP_DEATH_NOTE_DEPOSED");
					state.Ruler = null;
					SetLastRuler(state.KingdomId, null);
				}
				// Вся держава переходит к повстанцам: города метрополии — в их государство,
				// прежние границы и название страны возвращаются, а во главе встаёт предводитель бунтовщиков.
				List<int> remaining = new List<int>();
				foreach (CityPolitics c in _cities.Values)
				{
					if (c.KingdomId == state.KingdomId)
					{
						remaining.Add(c.CityId);
					}
				}
				if (rebels != null)
				{
					PendingWorldOps.Add(new PendingWorldOp
					{
						Type = WorldOpType.TransferCities,
						FromKingdomId = state.KingdomId,
						ToKingdomId = rebels.KingdomId,
						CityIds = remaining,
						Year = year,
						OriginalKingdomName = kingdomName,
						LeaderActorId = cw.RebelLeaderActorId
					});
						rebels.Ideology = outcome.RebelIdeology;
						if (TribalCivilization.NeedsCivilization(oldIdeology)
							&& !TribalCivilization.NeedsCivilization(outcome.RebelIdeology))
						{
							rebels.WasCivilized = true;
							rebels.LastCivilizedYear = year;
							rebels.HasTribalEthnonym = false;
						}
						RefreshConstitutionAfterIdeologyChange(rebels);
					rebels.SuccessionLaw = SuccessionLaws.GetDefaultLaw(rebels.Ideology);
					rebels.Legitimacy = Math.Max(rebels.Legitimacy, rebels.Def.Legitimacy * 0.7f);
					rebels.AvgDissent = Math.Max(0f, rebels.AvgDissent - 0.1f);
					if (!string.IsNullOrWhiteSpace(kingdomName))
					{
						_kingdomNames[rebels.KingdomId] = kingdomName;
						_pendingRenameOverrides[rebels.KingdomId] = kingdomName;
					}
					if (cw.RebelLeaderActorId != 0)
					{
						rebels.Ruler = new RulerRef
						{
							ActorId = cw.RebelLeaderActorId,
							Name = cw.RebelLeaderName ?? string.Empty,
							IsFemale = cw.RebelLeaderIsFemale,
							KingdomId = rebels.KingdomId,
							SinceYear = year,
							Age = 30
						};
					}
					foreach (CityPolitics c in _cities.Values)
					{
						if (c.KingdomId == state.KingdomId || c.KingdomId == rebels.KingdomId)
						{
							c.Dominant = outcome.RebelIdeology;
							c.AvgDissent = Math.Max(0f, c.AvgDissent * 0.4f);
						}
					}
					foreach (ActorEntry actor in _actors.Values)
					{
						if (actor.KingdomId == state.KingdomId || actor.KingdomId == rebels.KingdomId)
						{
							actor.Politics.Ideology = outcome.RebelIdeology;
							actor.Politics.Dissent = Math.Max(0f, actor.Politics.Dissent * 0.4f);
						}
					}
					RecordMergedIntoSuzerain(state.KingdomId);
				}
				else
				{
					// Государства повстанцев в ядре уже нет — режим меняется на месте.
					state.Ideology = outcome.RebelIdeology;
					state.Stance = KingdomSimulation.ClampStance(state.Stance);
					state.SuccessionLaw = SuccessionLaws.GetDefaultLaw(state.Ideology);
					state.Election = SuccessionLaws.HasPeriodicCycle(state.Ideology)
						? ElectionSystem.ReformedElectoralState(state.Election, year)
						: null;
					state.Legitimacy = Math.Max(5f, state.Def.Legitimacy * 0.55f);
					state.DynastyReignsCount = 1;
					state.DynastyStartYear = year;
					string takeoverCapName = input.Cities != null && input.Cities.Count > 0 ? input.Cities[0].Name : string.Empty;
					string takeoverCapBiome = input.Cities != null && input.Cities.Count > 0 ? input.Cities[0].BiomeId : string.Empty;
					string takeoverCapCulture = input.Cities != null && input.Cities.Count > 0 ? input.Cities[0].CultureName : string.Empty;
					kingdomName = EnsureIdeologyStateName(state, kingdomName, takeoverCapName, takeoverCapBiome, takeoverCapCulture);
				}
				Bus.Publish(new PolEvent(PolEventType.KingdomAdoptedIdeology, "RP_EV_ADOPTED_IDEOLOGY")
				{
					Year = year,
					Severity = Severity.Critical,
					KingdomId = state.KingdomId
				}.With("old_name", preTakeoverName)
				 .With("kingdom", kingdomName)
				 .With("old", Ideologies.NameKey(oldIdeology))
				 .With("new", Ideologies.NameKey(outcome.RebelIdeology)));
				break;
			}
			case CivilWarResolution.Secession:
			{
				Bus.Publish(new PolEvent(PolEventType.KingdomSeceded, "RP_EV_KINGDOM_SECEDED")
				{
					Year = year,
					Severity = Severity.Critical,
					KingdomId = state.KingdomId,
					CityId = capitalCity
				}.With("kingdom", kingdomName)
					.With("new_id", cw.RebelKingdomId)
					.With("city", capitalCity)
					.With("cities", rebelCities)
					.With("ideology", Ideologies.NameKey(outcome.RebelIdeology))
					.With("cause", causeKey)
					.With("leader", cw.RebelLeaderName));
				if (rebels != null)
				{
					rebels.Legitimacy = Math.Min(100f, rebels.Legitimacy + 15f);
					string rebelStateName = KingdomNameOf(rebels.KingdomId);
					for (int r = 0; r < cw.RebelCityIds.Count; r++)
					{
						int rcid = cw.RebelCityIds[r];
						CityHomelands.RebindHomeland(rcid, rebels.KingdomId, rebelStateName);
						CityHomelandRecord? hrec = CityHomelands.Get(rcid);
						if (hrec != null)
						{
							hrec.OriginalIdeology = rebels.Ideology;
						}
					}
					PendingWorldOps.Add(new PendingWorldOp
					{
						Type = WorldOpType.MakePeace,
						FromKingdomId = state.KingdomId,
						ToKingdomId = rebels.KingdomId,
						Year = year
					});
				}
				break;
			}
		}

		Bus.Publish(new PolEvent(PolEventType.CivilWarEnded, "RP_EV_CIVILWAR_ENDED_" + outcome.Resolution.ToString().ToUpperInvariant())
		{
			Year = year,
			Severity = Severity.Critical,
			KingdomId = state.KingdomId
		}.With("kingdom", kingdomName)
			.With("cause", causeKey)
			.With("resolution", "RP_CW_RESULT_" + outcome.Resolution.ToString().ToUpperInvariant())
			.With("years", Math.Max(1, outcome.YearsActive - 1))
			.With("cities", rebelCities)
			.With("leader", cw.RebelLeaderName)
			.With("leaderActorId", cw.RebelLeaderActorId));
	}

	/// <summary>Операции над миром (передача городов, мир), которые игровой слой ещё не исполнил.</summary>
	public List<PendingWorldOp> PendingWorldOps { get; } = new List<PendingWorldOp>();

	/// <summary>Забирает и очищает список операций, ожидающих исполнения в игре.</summary>
	public List<PendingWorldOp> TakePendingWorldOps()
	{
		List<PendingWorldOp> list = new List<PendingWorldOp>(PendingWorldOps);
		PendingWorldOps.Clear();
		return list;
	}

	/// <summary>Свободный идентификатор для государства, созданного ядром (позже заменяется на игровой).</summary>
	public int AllocateKingdomId()
	{
		int max = 0;
		foreach (int id in _kingdoms.Keys)
		{
			if (id > max)
			{
				max = id;
			}
		}
		if (_nextBreakawayId <= max)
		{
			_nextBreakawayId = max + 1;
		}
		return _nextBreakawayId++;
	}

	/// <summary>
	/// Создаёт новое государство из мятежных городов: переносит города и их жителей,
	/// задаёт знамя мятежа как государственный строй.
	/// </summary>
	public KingdomPolitics CreateBreakawayKingdom(int newKingdomId, KingdomPolitics parent, List<int> cityIds, IdeologyType ideology, int year)
	{
		KingdomPolitics breakaway = AddKingdom(newKingdomId, ideology, year);
		// Повстанцы воюют за СВОЙ строй — новая держава получает свежую конституцию
		// своей идеологии (а не копию родительской, против которой и восстали).
		if (Params.EnableConstitution)
		{
			breakaway.Constitution = CreateConstitution(ideology, EraRules.OfYear(year, Params));
			breakaway.ConstitutionHistory.Add(new ConstitutionEdit
			{
				Year = year,
				Article = "RP_CONST_ART_FOUNDATION",
				From = string.Empty,
				To = "RP_CONST_FOUNDED_BY_REBELS",
				EditorName = "RP_CONST_EDITOR_ASSEMBLY"
			});
		}
		breakaway.Legitimacy = Math.Max(10f, breakaway.Def.Legitimacy * 0.7f);
		breakaway.TitularRaceId = ideology == IdeologyType.Ethnonationalism ? parent.TitularRaceId : string.Empty;
		breakaway.DynastyFounder = string.Empty;
		breakaway.RoyalClanName = string.Empty;
		if (cityIds != null)
		{
			for (int i = 0; i < cityIds.Count; i++)
			{
				int cityId = cityIds[i];
				CityPolitics? city = GetCity(cityId);
				if (city == null)
				{
					continue;
				}
				city.KingdomId = newKingdomId;
				if (_actorsByCity.TryGetValue(cityId, out var members))
				{
					for (int j = 0; j < members.Count; j++)
					{
						MoveActor(members[j], cityId, newKingdomId);
					}
				}
			}
		}
		return breakaway;
	}

	/// <summary>
	/// Заменяет временный идентификатор государства на настоящий id королевства WorldBox.
	/// Вызывается игровым слоем сразу после того, как новое королевство создано в игре.
	/// </summary>
	public bool RemapKingdomId(int from, int to)
	{
		if (from == to)
		{
			return true;
		}
		if (!_kingdoms.TryGetValue(from, out var state) || _kingdoms.ContainsKey(to))
		{
			return false;
		}
		_kingdoms.Remove(from);
		state.KingdomId = to;
		_kingdoms[to] = state;
		if (_economyReports.TryGetValue(from, out var report))
		{
			_economyReports.Remove(from);
			_economyReports[to] = report;
		}
		Trade.RemapKingdom(from, to);
		if (_kingdomNames.TryGetValue(from, out var movedName))
		{
			_kingdomNames.Remove(from);
			_kingdomNames[to] = movedName;
		}
		foreach (CityPolitics city in _cities.Values)
		{
			if (city.KingdomId == from)
			{
				city.KingdomId = to;
			}
		}
		foreach (ActorEntry actor in _actors.Values)
		{
			if (actor.KingdomId == from)
			{
				actor.KingdomId = to;
			}
		}
		if (_lastRulerRef.TryGetValue(from, out var movedRuler))
		{
			movedRuler.KingdomId = to;
			_lastRulerRef.Remove(from);
			_lastRulerRef[to] = movedRuler;
			if (_lastRuler.TryGetValue(from, out var lastId))
			{
				_lastRuler.Remove(from);
				_lastRuler[to] = lastId;
			}
		}
		else
		{
			_lastRuler.Remove(from);
		}
		for (int i = 0; i < _unions.Count; i++)
		{
			UnionMember? member = _unions[i].GetMember(from);
			if (member != null)
			{
				member.KingdomId = to;
			}
			if (_unions[i].LeaderId == from)
			{
				_unions[i].LeaderId = to;
			}
		}
		for (int j = 0; j < PendingSecessions.Count; j++)
		{
			if (PendingSecessions[j].ProvisionalKingdomId == from)
			{
				PendingSecessions[j].ProvisionalKingdomId = to;
			}
		}
		for (int j = 0; j < PendingWorldOps.Count; j++)
		{
			if (PendingWorldOps[j].FromKingdomId == from) PendingWorldOps[j].FromKingdomId = to;
			if (PendingWorldOps[j].ToKingdomId == from) PendingWorldOps[j].ToKingdomId = to;
		}
		// Гражданская война метрополии ссылается на государство повстанцев по id.
		foreach (KingdomPolitics k in _kingdoms.Values)
		{
			if (k.CivilWar != null && k.CivilWar.RebelKingdomId == from)
			{
				k.CivilWar.RebelKingdomId = to;
			}
		}
		return true;
	}

	/// <summary>
	/// Немедленный откат несостоявшегося отделения: игровой API отказал, значит
	// никакого нового государства в WorldBox нет — убираем «призрак» из ядра сразу,
	/// города возвращаются родителю. (Без этого расхождение жило до следующего снимка.)
	/// </summary>
	public bool AbortSecession(int provisionalId, int parentId, List<int> cityIds)
	{
		bool removed = _kingdoms.Remove(provisionalId);
		if (_kingdoms.TryGetValue(parentId, out var parentState) && parentState.CivilWar != null && parentState.CivilWar.RebelKingdomId == provisionalId)
		{
			// Игра не смогла создать государство повстанцев — мятеж не состоялся.
			parentState.CivilWar = null;
		}
		if (cityIds != null)
		{
			for (int i = 0; i < cityIds.Count; i++)
			{
				if (_cities.TryGetValue(cityIds[i], out var city))
				{
					city.KingdomId = parentId;
				}
				// Жители «призрачной» державы возвращаются к родителю вместе с городами.
				if (_actorsByCity.TryGetValue(cityIds[i], out var members))
				{
					for (int j = 0; j < members.Count; j++)
					{
						if (_actors.TryGetValue(members[j], out var actor) && actor.KingdomId == provisionalId)
						{
							actor.KingdomId = parentId;
						}
					}
				}
			}
		}
		_lastRulerRef.Remove(provisionalId);
		_lastRuler.Remove(provisionalId);
		_economyReports.Remove(provisionalId);
		_kingdomNames.Remove(provisionalId);
		Trade.DropKingdom(provisionalId);
		return removed;
	}

	/// <summary>Отделения, которые игровой слой ещё не исполнил в WorldBox.</summary>
	public List<PendingSecession> PendingSecessions { get; } = new List<PendingSecession>();

	/// <summary>Забирает и очищает список отделений, ожидающих исполнения в игре.</summary>
	public List<PendingSecession> TakePendingSecessions()
	{
		List<PendingSecession> list = new List<PendingSecession>(PendingSecessions);
		PendingSecessions.Clear();
		return list;
	}

	/// <summary>Этап 19: годовое обслуживание династий — личность, дрейфы характера, отношения правителей, узы брака.</summary>
	// ------------------------------------------------------------------
	// Этап 20: глубокая дипломатия — договоры, обиды, казус белли, шпионаж,
	// коалиции, дань вассалов и войны за независимость.
	// ------------------------------------------------------------------

	/// <summary>Книга договоров мира.</summary>
	public TreatyBook TreatiesBook => _treaties;

	/// <summary>Накопленные обиды держав друг на друга.</summary>
	public GrievanceBook Grievances => _grievances;

	/// <summary>Коалиции против агрессоров.</summary>
	public CoalitionBook Coalitions => _coalitions;

	/// <summary>История объявленных войн (топливо «очков агрессии»).</summary>
	public IReadOnlyList<WarDeclarationRecord> WarDeclarations => _warDeclarations;

	/// <summary>
	/// Снимает с державы обвинения в развязывании войн: её записи об объявленных войнах
	/// стираются. Нужно после наказания на съезде КМУД (0.39), иначе одна и та же держава
	/// обвиняется на каждом съезде за старые войны — в симуляции на 1000 лет так вышло
	/// 7 раз из 16 у одного и того же племени.
	/// Возвращает, сколько обвинений снято.
	/// </summary>
	public int PardonWarDeclarations(int kingdomId)
	{
		if (kingdomId <= 0)
		{
			return 0;
		}
		int removed = 0;
		for (int i = _warDeclarations.Count - 1; i >= 0; i--)
		{
			if (_warDeclarations[i] != null && _warDeclarations[i].AttackerId == kingdomId)
			{
				_warDeclarations.RemoveAt(i);
				removed++;
			}
		}
		return removed;
	}

	/// <summary>Добавляет запись об объявленной войне (используется загрузкой сохранений).</summary>
	public void WarDeclarationsAdd(WarDeclarationRecord record)
	{
		if (record != null)
		{
			_warDeclarations.Add(record);
		}
	}

	/// <summary>Призывы вступить в войну: пары (вассал/союзник, агрессор). Исполняются игровым слоем.</summary>
	public IReadOnlyList<KeyValuePair<int, int>> PendingDefenseCalls => _pendingDefenseCalls;

	/// <summary>Войны живых вассалов за независимость: пары (вассал, сюзерен).</summary>
	public IReadOnlyList<KeyValuePair<int, int>> PendingIndependenceWars => _pendingIndependenceWars;

	public void QueueIndependenceWar(int vassalId, int suzerainId)
	{
		if (vassalId <= 0 || suzerainId <= 0 || vassalId == suzerainId)
		{
			return;
		}
		var pair = new KeyValuePair<int, int>(vassalId, suzerainId);
		if (!_pendingIndependenceWars.Contains(pair))
		{
			_pendingIndependenceWars.Add(pair);
		}
	}

	public List<KeyValuePair<int, int>> TakePendingIndependenceWars()
	{
		var taken = new List<KeyValuePair<int, int>>(_pendingIndependenceWars);
		_pendingIndependenceWars.Clear();
		return taken;
	}

	/// <summary>Переводы золота по тайным операциям: пары (от кого, кому). Размер — <see cref="SpyStealAmount"/>.</summary>
	public IReadOnlyList<KeyValuePair<int, int>> PendingGoldTransfers => _pendingGoldTransfers;

	/// <summary>Сумма кражи казны за одну успешную операцию.</summary>
	public const float SpyStealAmount = 25f;

	public void ClearPendingCalls()
	{
		_pendingDefenseCalls.Clear();
		_pendingGoldTransfers.Clear();
		_pendingIndependenceWars.Clear();
	}

	// ------------------------------------------------------------------
	// Этап 21: ванильные альянсы, вассалитет и соединяющее поглощение.
	// ------------------------------------------------------------------

	/// <summary>Книга ванильных альянсов игры (последний сверенный снимок).</summary>
	public AllianceBook AlliancesBook => _alliances;
	/// <summary>Переводы городов по завершённой интеграции: пары (из державы, в державу).</summary>
	public IReadOnlyList<KeyValuePair<int, int>> PendingCityTransfers => _pendingCityTransfers;

	public void ClearPendingCityTransfers()
	{
		_pendingCityTransfers.Clear();
	}

	/// <summary>0.24: поглощения (соединения), ждущие исполнения в мире.</summary>
	public IReadOnlyList<PendingMerger> PendingMergers => _pendingMergers;

	/// <summary>Забирает и очищает список поглощений; игровой слой исполнит перевод городов и смерть держав.</summary>
	public List<PendingMerger> TakePendingMergers()
	{
		List<PendingMerger> list = new List<PendingMerger>(_pendingMergers);
		_pendingMergers.Clear();
		return list;
	}

	public void ClearPendingMergers()
	{
		_pendingMergers.Clear();
	}

	/// <summary>Загрузка: базовый снимок альянсов уже известен (из сохранения) — события не нужны.</summary>
	public void MarkAllianceBaseline()
	{
		_allianceBaseline = true;
	}

	/// <summary>Имена держав-участниц альянса через запятую (для летописи: «кем создан»).</summary>
	private string MemberNames(AllianceRecord rec)
	{
		List<string> names = new List<string>();
		for (int i = 0; i < rec.Members.Count; i++)
		{
			string n = KingdomNameOf(rec.Members[i]);
			if (!string.IsNullOrEmpty(n))
			{
				names.Add(n);
			}
		}
		return string.Join(", ", names);
	}

	/// <summary>
	/// 0.48: имя державы для летописи, но без пустоты.
	/// В мире владельца на 1251-м году в летопись попало «Держава  покинула альянс
	/// «Лига государств»» — дважды, с пустым именем: держава к тому моменту уже
	/// перестала существовать, и её имени в памяти не осталось. Пустая строка
	/// в летописи выглядит как ошибка мода, поэтому подставляем понятное пояснение.
	/// </summary>
	private string MemberNameOrFallback(int kingdomId)
	{
		string name = KingdomNameOf(kingdomId);
		if (!string.IsNullOrWhiteSpace(name))
		{
			return name;
		}
		return "безымянная держава №" + kingdomId;
	}

	/// <summary>Этап 21: игровой слой передаёт снимок ванильных альянсов до годового тика.
	/// Ядро сверяет его с прежним снимком и публикует события создания/распада/вступления/выхода.</summary>
	public void SetAllianceSnapshot(List<AllianceRecord> snapshot)
	{
		if (!Params.TrackAlliances || snapshot == null)
		{
			return;
		}
        // A partial or ambiguous read is not evidence of a dissolution. Never substitute order for identity.
        var freshById=new Dictionary<int,AllianceRecord>();
        foreach(var record in snapshot)
        {
            if(record==null || record.Id<=0 || freshById.ContainsKey(record.Id))return;
            freshById.Add(record.Id,record);
        }
        bool legacyBook=false;foreach(var record in _alliances.All)if(record.Id<=0)legacyBook=true;
		if (legacyBook || !_allianceBaseline && !BaselineRecorded)
		{
			// Initial world or legacy book without trustworthy IDs: silently establish a native baseline.
            // Names cannot prove continuity; old historical events are not rewritten.
			_alliances.Replace(snapshot);
			_allianceBaseline = true;
			return;
		}
        var oldById=new Dictionary<int,AllianceRecord>();
        foreach(var record in _alliances.All)
        {
            if(oldById.ContainsKey(record.Id))return;
            oldById.Add(record.Id,record);
        }
		for (int i = 0; i < snapshot.Count; i++)
		{
			AllianceRecord fresh = snapshot[i];
			if (string.IsNullOrEmpty(fresh.Name))
			{
				continue;
			}
			oldById.TryGetValue(fresh.Id,out var old);
			if (old == null)
			{
				Bus.Publish(new PolEvent(PolEventType.AllianceFormed, "RP_EV_ALLIANCE_FORMED")
				{
					Year = Year,
					Severity = Severity.Major,
					KingdomId = fresh.LeaderId
				}.With("name", fresh.Name).With("leader", KingdomNameOf(fresh.LeaderId)).With("count", fresh.Members.Count)
					.With("members", MemberNames(fresh)));
				continue;
			}
			for (int m = 0; m < fresh.Members.Count; m++)
			{
				if (!old.Members.Contains(fresh.Members[m]))
				{
					Bus.Publish(new PolEvent(PolEventType.AllianceMemberJoined, "RP_EV_ALLIANCE_JOINED")
					{
						Year = Year,
						Severity = Severity.Minor,
						KingdomId = fresh.Members[m]
					}.With("name", fresh.Name).With("member", KingdomNameOf(fresh.Members[m])).With("count", fresh.Members.Count));
				}
			}
			for (int m = 0; m < old.Members.Count; m++)
			{
				if (!fresh.Members.Contains(old.Members[m]))
				{
					Bus.Publish(new PolEvent(PolEventType.AllianceMemberLeft, "RP_EV_ALLIANCE_LEFT")
					{
						Year = Year,
						Severity = Severity.Minor,
						KingdomId = old.Members[m]
					}.With("name", fresh.Name).With("member", MemberNameOrFallback(old.Members[m])));
				}
			}
		}
		for (int i = 0; i < _alliances.Count; i++)
		{
			AllianceRecord old = _alliances.All[i];
			bool alive = freshById.ContainsKey(old.Id);
			if (!alive)
			{
				Bus.Publish(new PolEvent(PolEventType.AllianceBroken, "RP_EV_ALLIANCE_BROKEN")
				{
					Year = Year,
					Severity = Severity.Major,
					KingdomId = old.LeaderId
				}.With("name", old.Name).With("count", old.Members.Count));
			}
		}
		_alliances.Replace(snapshot);
		_allianceBaseline = true;
	}

	// Этап 22: война и мирные договоры — усталость, перемирия, условия победителя.
	// ------------------------------------------------------------------

	/// <summary>Книга перемирий между парами держав.</summary>
	public TruceBook TrucesBook => _truces;

	// ------------------------------------------------------------------
	// 0.32: КМУД — всемирная мирная конференция
	// ------------------------------------------------------------------

	/// <summary>Состояние конференции КМУД: созыв, сроки, запрет войн.</summary>
	public KmudState Kmud { get; } = new KmudState();

	/// <summary>Идёт ли сейчас конференция.</summary>
	public bool KmudActive => Kmud != null && Kmud.IsConvened;

	/// <summary>Действует ли сейчас всемирный запрет войн (конференция или выбранные годы мира).</summary>
	public bool KmudPauseActive => Kmud != null && KmudRules.PauseActive(Kmud, Year);

	// ------------------------------------------------------------------
	// Этап 23: границы, регентство по-настоящему, распады, рейды, фамилии.
	// ------------------------------------------------------------------

	/// <summary>
	/// Фамилия династии (этап 23): при первой возможности имя фам фамилии короля-основателя
	/// становится именем королевского дома; само событие «новая эра» фиксируется в летописи
	/// один раз. Клан игры физически переименовывает фиддер (см. GameRefs.RenameRoyalClan).
	/// </summary>
	public void EnsureDynastySurname(KingdomPolitics state)
	{
		if (!Params.EnableDynastySurnames || state == null)
		{
			return;
		}
		if (!string.IsNullOrEmpty(state.DynastySurname))
		{
			return;
		}
		if (string.IsNullOrEmpty(state.DynastyFounder))
		{
			return; // закладываем только в королевском доме с известным основателем
		}
		string clanName = !string.IsNullOrEmpty(state.RoyalClanName) ? state.RoyalClanName : (state.Ruler?.ClanName ?? string.Empty);
		string surname = DynastyNaming.DynastySurname(state.DynastyFounder, clanName);
		if (string.IsNullOrEmpty(surname))
		{
			return;
		}
		// Зеркало игрового клана (RoyalClanName) НЕ трогаем: оно синхронизируется
		// фидером с реальным кланом игры, иначе сломается счёт преемственности
		// (сравнение с clan нового короля до переименования клана в игре).
		// Клан игры физически переименовывает фиддер; после этого зеркало = фамилия.
		string oldName = state.RoyalClanName;
		state.DynastySurname = surname;
		if (string.IsNullOrEmpty(oldName) || oldName == surname)
		{
			return; // первое знакомство мира с домом — литописует само вступление династии
		}
		Bus.Publish(new PolEvent(PolEventType.DynastyRenamed, "RP_EV_DYNASTY_RENAMED")
		{
			Year = Year,
			Severity = Severity.Major,
			KingdomId = state.KingdomId
		}.With("kingdom", KingdomNameOf(state.KingdomId))
			.With("old", oldName)
			.With("dynasty", surname)
			.With("founder", state.DynastyFounder));
	}

	/// <summary>Пограничная политика пар держав (открыта/закрыта, срок запрета).</summary>
	public BordersBook BordersBook => _borders;

	/// <summary>Причина, по которой открытие границы невозможно/ещё не созрело (для лога и UI).</summary>
	public string LastBorderOpeningVerdict = string.Empty;

	/// <summary>
	/// 0.23.2: дипломатическое открытие закрытой границы. Условия должны выполняться
	/// `BorderOpeningTalksYears` лет ПОДРЯД (переговоры), иначе счётчик сбрасывается:
	///  • не война и нет активного перемирия (свежий мир — ещё не доверие);
	///  • союз/уния — быстрый путь (переговоры вдвое короче), иначе нужен открытый торговый путь;
	///  • взаимная обида ≤ BorderOpeningMaxGrievance;
	///  • легитимность обеих сторон ≥ BorderOpeningMinLegitimacy;
	///  • нет замка после облавы; конституции не запрещают.
	/// Возвращает true, если граница открылась в этом году.
	/// </summary>
	public bool TryNegotiateBorderOpening(int a, int b, bool alliedOrUnion, bool atWar, bool openTradeRoute)
	{
        LastBorderOpeningVerdict = string.Empty;
        if(!Governance.Recognition.CanContact(a,b,InternationalContact.Trade))
        {LastBorderOpeningVerdict=CoreText.L("RP_GOV26_CONTACT_REFUSED","международное признание требуется для мирных связей");return false;}
		if (!Params.EnableBorders || a == b || !_borders.ClosedBetween(a, b))
		{
			return false;
		}
		KingdomPolitics ka = GetKingdom(a);
		KingdomPolitics kb = GetKingdom(b);
		if (ka == null || kb == null)
		{
			return false;
		}
		string fail = null;
		if (atWar) fail = "война";
		else if (TruceActiveBetween(a, b)) fail = "перемирие ещё свежо";
		else if (_borders.LockedUntilBetween(a, b) > Year) fail = "запрет после облавы до " + _borders.LockedUntilBetween(a, b);
		else if (!alliedOrUnion && !openTradeRoute) fail = "нет ни союза, ни торгового пути";
		else if (GetGrievance(a, b) > Params.BorderOpeningMaxGrievance || GetGrievance(b, a) > Params.BorderOpeningMaxGrievance) fail = "взаимные обиды";
		else if (ka.Legitimacy < Params.BorderOpeningMinLegitimacy || kb.Legitimacy < Params.BorderOpeningMinLegitimacy) fail = "слабая легитимность одной из сторон";
		else if (ka.CivilWar != null || kb.CivilWar != null) fail = "гражданская война";
		else if (Params.EnableConstitution && (ConstitutionGates.BorderMustBeClosed(ka.Constitution) || ConstitutionGates.BorderMustBeClosed(kb.Constitution))) fail = "конституция требует закрытых границ";
		if (fail != null)
		{
			_borders.ResetOpeningTalks(a, b);
			LastBorderOpeningVerdict = fail;
			return false;
		}
		int need = Math.Max(1, alliedOrUnion ? (Params.BorderOpeningTalksYears + 1) / 2 : Params.BorderOpeningTalksYears);
		int talks = _borders.AdvanceOpeningTalks(a, b);
		if (talks < need)
		{
			LastBorderOpeningVerdict = "переговоры " + talks + "/" + need;
			return false;
		}
		return ApplyBorderPolicy(a, b, BorderStatus.Open);
	}

	/// <summary>
	/// Решение правителя (ИИ-стратегия или игрок) об открытии/закрытии границ с соседом.
	/// Публикует событие только при реальной смене режима; на перемирии закрываться
	/// поздно — перемирие свято; при закрытии ломаются торговые пути пары.
	/// </summary>
	public bool ApplyBorderPolicy(int kingdomId, int otherId, BorderStatus status)
	{
		if (!Params.EnableBorders || kingdomId == otherId)
		{
			return false;
		}
		KingdomPolitics a = GetKingdom(kingdomId);
		KingdomPolitics b = GetKingdom(otherId);
		if (a == null || b == null)
		{
			return false;
		}
		if (status == BorderStatus.Open && _borders.LockedUntilBetween(kingdomId, otherId) > Year)
		{
			return false; // после облавы на чужаках границу не открывают сразу
		}
		// Этап 26: писаный закон о границах выше приказа правителя — конституция Open
		// запрещает закрытие, конституция Closed запрещает открытие (советская роль не вяжет).
		if (Params.EnableConstitution)
		{
			Constitution aLaw = GetKingdom(kingdomId)?.Constitution;
			if (status == BorderStatus.Closed && !ConstitutionGates.BorderEnforcementAllowed(aLaw))
			{
				return false;
			}
			if (status == BorderStatus.Open && ConstitutionGates.BorderMustBeClosed(aLaw))
			{
				return false;
			}
		}
		if (!_borders.SetStatus(kingdomId, otherId, status, Year))
		{
			return false;
		}
		if (status == BorderStatus.Closed)
		{
			Bus.Publish(new PolEvent(PolEventType.BorderClosed, "RP_EV_BORDER_CLOSED")
			{
				Year = Year,
				Severity = Severity.Major,
				KingdomId = kingdomId
			}.With("kingdom", KingdomNameOf(kingdomId)).With("partner", KingdomNameOf(otherId)));
			return true;
		}
		Bus.Publish(new PolEvent(PolEventType.BorderOpened, "RP_EV_BORDER_OPENED")
		{
			Year = Year,
			Severity = Severity.Minor,
			KingdomId = kingdomId
		}.With("kingdom", KingdomNameOf(kingdomId)).With("partner", KingdomNameOf(otherId)));
		return true;
	}

	/// <summary>
	/// Игровой слой докладывает чужакa за закрытой границей: ядро взимает цену облавы
	/// (легитимность/обида нарушителя-соседа не даётся — нарушителя гоняют, а не судят),
	/// запирает пару на BorderLockYears и публикует событие выдворения.
	/// </summary>
	public void ReportBorderIntrusion(int intoKingdomId, int intruderKingdomId, string intruderName)
	{
		if (!Params.EnableBorders)
		{
			return;
		}
		KingdomPolitics into = GetKingdom(intoKingdomId);
		if (into == null)
		{
			return;
		}
		// 0.23.2: запрет на открытие — только если граница закрыта решением правителя (инцидент),
		// иначе при границах «закрыто по умолчанию» открыть их было бы невозможно никогда.
		if (_borders.ExplicitlyClosedBetween(intoKingdomId, intruderKingdomId))
		{
			_borders.Lock(intoKingdomId, intruderKingdomId, Year, Params.BorderLockYears);
		}
		else
		{
			// Чужак на закрытой по умолчанию границе — мелкая дипломатическая заноза: переговоры откатываются.
			_borders.ResetOpeningTalks(intoKingdomId, intruderKingdomId);
		}
		Bus.Publish(new PolEvent(PolEventType.BorderExpulsion, "RP_EV_BORDER_EXPULSION")
		{
			Year = Year,
			Severity = Severity.Trivial,
			KingdomId = intoKingdomId
		}.With("kingdom", KingdomNameOf(intoKingdomId))
			.With("partner", KingdomNameOf(intruderKingdomId))
			.With("intruder", intruderName));
	}

	/// <summary>
	/// Регистрация откола населённого пункта/сецессии: когда за CollapseTracker.WindowYears
	/// лет от державы изгоняется MinFragments+ фрагментов — ядро публикует единую строку
	/// «С распалось на N государств» с перечнем, чтобы летопись не заставляла домысливать.
	/// </summary>
	public void RecordSecessionFragment(int fromKingdomId, int fragmentId)
	{
		if (!BaselineRecorded)
		{
			return;
		}
		System.Func<int, int> townsOf = id =>
		{
			int n = 0;
			foreach (CityPolitics c in _cities.Values)
			{
				if (c.KingdomId == id)
				{
					n++;
				}
			}
			return n;
		};
		List<CollapseFragment> summary = _collapses.RecordFragment(fromKingdomId, Year,
			fragmentId, KingdomNameOf(fragmentId), townsOf);
		if (summary == null)
		{
			return;
		}
		string joined = string.Join(", ", summary.ConvertAll(f => f.Name + " (" + f.Towns + " гор.)"));
		Bus.Publish(new PolEvent(PolEventType.KingdomCollapsed, "RP_EV_KINGDOM_COLLAPSED")
		{
			Year = Year,
			Severity = Severity.Critical,
			KingdomId = fromKingdomId
		}.With("kingdom", KingdomNameOf(fromKingdomId))
			.With("count", summary.Count)
			.With("fragments", joined));
	}

	/// <summary>
	/// Тайный авиаудар: фиддер ежегодно зовёт по державам в состоянии войны.
	/// Балл развития высокий, цель — благополучный противник; cooldown честный.
	/// Возвращает true, если державе дозволено бросить звено (игра производит взрыв).
	/// </summary>
	public bool TryPowerRaid(int kingdomId, int enemyKingdomId)
	{
		if (!Params.EnablePowerRaids)
		{
			return false;
		}
		KingdomPolitics bomber = GetKingdom(kingdomId);
		KingdomPolitics enemy = GetKingdom(enemyKingdomId);
		if (bomber == null || enemy == null)
		{
			return false;
		}
		// Этап 26: налёт = применение силы против чужого государства; если конституция
		//.Binding требует мандата собрания/народа — налётчик идёт через голосование.
		if (Params.EnableConstitution && !TryConstitutionalWar(kingdomId, enemyKingdomId, KingdomNameOf(enemyKingdomId), out _))
		{
			return false;
		}
		double enemyWelfare = enemy.Economy?.Prosperity ?? 50f;
		if (!PowerRaids.CanRaid(bomber, Params, Year, enemyWelfare))
		{
			return false;
		}
		// Чем выше балл развития сверху порога — тем чаще налёты, но не ежегодно.
		double cap = PowerRaids.Capability(bomber);
		double chance = 0.05 + (cap - PowerRaids.Threshold) / 400.0;
		if (Rng.NextDouble() >= chance)
		{
			return false;
		}
		bomber.LastPowerRaidYear = Year;
		return true;
	}

	/// <summary>Развязка рейда: событие + урон благосостоянию и легитимности жертвы.</summary>
	public void ApplyPowerRaidOutcome(int bomberKingdomId, int victimKingdomId, string townName)
	{
		KingdomPolitics bomber = GetKingdom(bomberKingdomId);
		KingdomPolitics victim = GetKingdom(victimKingdomId);
		if (bomber == null || victim == null)
		{
			return;
		}
		if (victim.Economy != null)
		{
			victim.Economy.Prosperity = KingdomSimulation.Clamp(victim.Economy.Prosperity - Params.PowerRaidProsperityDamage, 0f, 100f);
		}
		victim.Legitimacy = KingdomSimulation.Clamp(victim.Legitimacy - 1.5f, 0f, 100f);
		PowerRaids.PublishRaid(this, bomber, victimKingdomId, townName);
	}

	/// <summary>Действующие приказы на репарации.</summary>
	public IReadOnlyList<ReparationsOrder> Reparations => _reparations;

	/// <summary>Действует ли перемирие между парой держав.</summary>
	public bool TruceActiveBetween(int a, int b)
	{
		return _truces.Active(a, b, Year);
	}

	/// <summary>До какого года действует перемирие пары (0 — перемирия нет).</summary>
	public int TruceUntilBetween(int a, int b)
	{
		return _truces.Until(a, b);
	}

	/// <summary>Требования передачи городов по мирным договорам. Исполняются игровым слоем.</summary>
	public IReadOnlyList<CityDemandOrder> PendingCityDemands => _pendingCityDemands;

	/// <summary>Приказы на отделение земель. Новую державу по ним может создать только
	/// вооружённое освобождение; мирные legacy-приказы конвертируются в вассальные земли.</summary>
	public IReadOnlyList<RestorationOrder> PendingRestorations => _pendingRestorations;

	public void AddPendingRestoration(RestorationOrder order)
	{
		if (order != null)
		{
			_pendingRestorations.Add(order);
		}
	}

	public List<RestorationOrder> TakePendingRestorations()
	{
		List<RestorationOrder> list = new List<RestorationOrder>(_pendingRestorations);
		_pendingRestorations.Clear();
		return list;
	}

	/// <summary>0.23.2: запомнить довоенный состав городов державы (зовётся при объявлении войны).
	/// Если запись уже есть (держава воюет на несколько фронтов) — не перезаписываем.</summary>
	public void RememberPrewarCities(int kingdomId, List<int> cityIds, int capitalId = 0)
	{
		if (kingdomId <= 0 || cityIds == null || cityIds.Count == 0)
		{
			return;
		}
		if (!_prewarCities.TryGetValue(kingdomId, out var list))
		{
			list = new List<int>(cityIds);
			_prewarCities[kingdomId] = list;
		}
		// Столица — первой: победитель оставляет её себе.
		if (capitalId > 0 && list.Contains(capitalId) && list[0] != capitalId)
		{
			list.Remove(capitalId);
			list.Insert(0, capitalId);
		}
	}

	/// <summary>Довоенные города державы (пусто, если война не была зафиксирована).</summary>
	public IReadOnlyList<int> PrewarCitiesOf(int kingdomId)
	{
		return _prewarCities.TryGetValue(kingdomId, out var list) ? list : (IReadOnlyList<int>)Array.Empty<int>();
	}

	/// <summary>Забыть довоенный состав (война кончилась, держава жива).</summary>
	public void ForgetPrewarCities(int kingdomId)
	{
		_prewarCities.Remove(kingdomId);
	}

	public void ClearPendingCityDemands()
	{
		_pendingCityDemands.Clear();
	}

	/// <summary>Загрузка из сохранения: восстановить приказ на репарации как есть.</summary>
	public void AddReparations(ReparationsOrder order)
	{
		if (order != null && order.PayerId != order.PayeeId && order.YearsLeft > 0)
		{
			_reparations.Add(order);
		}
	}

	/// <summary>Последний зафиксированный казус белли атакующего против защитника
	/// (для определения «прожорливости» победителя при мирном договоре).</summary>
	public CasusBelliType LastCasusBelli(int attackerId, int defenderId)
	{
		for (int i = _warDeclarations.Count - 1; i >= 0; i--)
		{
			WarDeclarationRecord record = _warDeclarations[i];
			if (record.AttackerId == attackerId && record.DefenderId == defenderId)
			{
				return record.CasusBelli;
			}
		}
		return CasusBelliType.Conquest;
	}

	/// <summary>
	/// Этап 22: развязка войны любого происхождения (ванильной или объявленной модом).
	/// Всегда: облегчение усталости обеих сторон и перемирие. При явном победителе —
	/// мирный договор с условиями по силе победы: унижение, репарации, уступка земель.
	/// Возвращает договор (или null, если развязка не оформлена: стороны погибли, настройка выключена).
	/// </summary>
	/// <param name="winnerId">id державы-победителя; 0 — стороны разошлись без победителя.</param>
	/// <param name="defeatedCitiesLeft">Сколько городов осталось у проигравшего в игре (−1 — неизвестно).
	/// 0 означает тотальное поражение: державы больше нет, и обычные условия (репарации с мёртвого) бессмысленны.</param>
	public PeaceSettlement ResolveWarEnd(int attackerId, int defenderId, int winnerId, int warYears, int defeatedCitiesLeft = -1, WarCoalitionOutcome coalition = null)
	{
		if (!Params.EnableWarPeace || attackerId == defenderId || attackerId <= 0 || defenderId <= 0)
		{
			return null;
		}
		KingdomPolitics attacker = GetKingdom(attackerId);
		KingdomPolitics defender = GetKingdom(defenderId);
		if (attacker == null || defender == null)
		{
			return null;
		}
		if (coalition != null)
		{
			int rawSign = winnerId == attackerId ? 1 : winnerId == defenderId ? -1 : 0;
			int resolvedSign = coalition.ResolveWinnerSide(rawSign);
			winnerId = resolvedSign > 0 ? attackerId : resolvedSign < 0 ? defenderId : 0;
		}
		string casualtiesSummary = coalition?.FormatCasualtiesSummary() ?? string.Empty;
		if (defeatedCitiesLeft == 0 && (winnerId == attackerId || winnerId == defenderId))
		{
			PeaceSettlement total = ResolveTotalDefeat(attackerId, defenderId, winnerId, warYears, casualtiesSummary);
			if (total != null)
			{
				return total;
			}
		}
		ForgetPrewarCities(attackerId);
		ForgetPrewarCities(defenderId);
		if (warYears < 1)
		{
			warYears = 1;
		}
		// Облегчение после развязки: усталость сразу тает вдвое, дальше лечится миром.
		attacker.WarFatigue *= 0.5f;
		defender.WarFatigue *= 0.5f;
		int truceUntil = Year + Params.TruceYears;
		_truces.Set(attackerId, defenderId, truceUntil);
		bool attackerWon = winnerId == attackerId;
		bool defenderWon = winnerId == defenderId;
		if (!attackerWon && !defenderWon)
		{
			Bus.Publish(new PolEvent(PolEventType.WhitePeace, "RP_EV_WHITE_PEACE")
			{
				Year = Year,
				Severity = Severity.Minor,
				KingdomId = attackerId
			}.With("kingdom", KingdomNameOf(attackerId))
				.With("partner", KingdomNameOf(defenderId))
				.With("years", warYears.ToString())
				.With("truce", Params.TruceYears.ToString())
				.With("casualties", casualtiesSummary));
			return new PeaceSettlement
			{
				Year = Year,
				Level = SettlementLevel.WhitePeace,
				TruceUntilYear = truceUntil
			};
		}
		int victorId = attackerWon ? attackerId : defenderId;
		int defeatedId = attackerWon ? defenderId : attackerId;
		KingdomPolitics victor = attackerWon ? attacker : defender;
		KingdomPolitics defeated = attackerWon ? defender : attacker;
		// 0.39: победитель смелеет, проигравший зарекается воевать на 10 лет.
		KingdomLearning.RecordVictory(victor, Year);
		KingdomLearning.RecordDefeat(defeated, Year);
		// Сила победы: длительная кампания за землю или уничтожение стран коалиции противника позволяет диктовать жёсткие условия.
		CasusBelliType cb = LastCasusBelli(attackerId, defenderId);
		bool landHungry = attackerWon && (cb == CasusBelliType.Conquest || cb == CasusBelliType.Reconquest);
		int enemyDestroyed = coalition == null ? 0 : (attackerWon ? coalition.DestroyedDefenders : coalition.DestroyedAttackers);
		float score = 20f + warYears * 10f + (victor.Legitimacy - defeated.Legitimacy) / 5f + (landHungry ? 15f : 0f) + enemyDestroyed * 35f;
		SettlementLevel level;
		if (score >= 70f && landHungry)
		{
			level = SettlementLevel.LandDemand;
		}
		else if (score >= 45f)
		{
			level = SettlementLevel.Reparations;
		}
		else if (score >= 25f)
		{
			level = SettlementLevel.Humiliation;
		}
		else
		{
			// Победа слишком слабая, чтобы диктовать условия: стороны расходятся белым миром.
			Bus.Publish(new PolEvent(PolEventType.WhitePeace, "RP_EV_WHITE_PEACE")
			{
				Year = Year,
				Severity = Severity.Minor,
				KingdomId = attackerId
			}.With("kingdom", KingdomNameOf(attackerId))
				.With("partner", KingdomNameOf(defenderId))
				.With("years", warYears.ToString())
				.With("truce", Params.TruceYears.ToString())
				.With("casualties", casualtiesSummary));
			return new PeaceSettlement
			{
				Year = Year,
				Level = SettlementLevel.WhitePeace,
				TruceUntilYear = truceUntil,
				Score = score,
				VictorId = victorId,
				DefeatedId = defeatedId
			};
		}
		PeaceSettlement settlement = new PeaceSettlement
		{
			VictorId = victorId,
			DefeatedId = defeatedId,
			Year = Year,
			Level = level,
			Score = score,
			TruceUntilYear = truceUntil
		};
		// Унижение есть при любом ненулевом уровне условий.
		float humiliation = level == SettlementLevel.LandDemand ? 15f : level == SettlementLevel.Reparations ? 12f : 8f;
		defeated.Legitimacy = KingdomSimulation.Clamp(defeated.Legitimacy - humiliation, 0f, 100f);
		defeated.Support = KingdomSimulation.Clamp(defeated.Support - humiliation / 2f, 0f, 100f);
		victor.Legitimacy = KingdomSimulation.Clamp(victor.Legitimacy + 5f, 0f, 100f);
		ChangeRulerRelation(victorId, defeatedId, -30);
		_grievances.Add(defeatedId, victorId,
			level == SettlementLevel.LandDemand ? GrievanceReason.Conquest : GrievanceReason.War,
			level == SettlementLevel.LandDemand ? 40f : 25f);
		if (level >= SettlementLevel.Reparations)
		{
			float perYear = defeated.Economy != null && defeated.Economy.LastIncome > 0f
				? defeated.Economy.LastIncome * Params.ReparationsIncomeShare
				: 12f;
			settlement.ReparationsPerYear = perYear;
			settlement.ReparationsYears = Params.ReparationsYears;
		}
		if (level == SettlementLevel.LandDemand)
		{
			int cities = Math.Max(1, Math.Min(Params.MaxCededCities, warYears / 2));
			settlement.CitiesDemanded = cities;
			DiplomaticMemories.Record(defeatedId, victorId, DiplomaticMemoryKind.AnnexedOurCity, Year, 80, -45, KingdomNameOf(defeatedId));
		}
		PeaceConferenceResult? conference = null;
		if (coalition != null)
		{
			// 0.27: союзники по победоносной коалиции запоминают боевое братство.
			IReadOnlyList<int> winners = winnerId == attackerId ? coalition.AttackerSideIds : coalition.DefenderSideIds;
			for (int i = 0; i < winners.Count; i++)
			{
				for (int j = i + 1; j < winners.Count; j++)
				{
					DiplomaticMemories.Record(winners[i], winners[j], DiplomaticMemoryKind.AlliedInWar, Year, 50, 25);
					DiplomaticMemories.Record(winners[j], winners[i], DiplomaticMemoryKind.AlliedInWar, Year, 50, 25);
				}
			}
			// 0.30: многосторонняя мирная конференция и раздел трофеев между союзниками по вкладу
			conference = PeaceConferenceEngine.ConductCoalitionSettlement(
				this,
				victorId,
				defeatedId,
				winners,
				settlement.CitiesDemanded,
				settlement.ReparationsPerYear,
				settlement.ReparationsYears,
				truceUntil,
				_reparations,
				_pendingCityDemands);
		}
		if (conference == null)
		{
			if (settlement.ReparationsPerYear > 0f && settlement.ReparationsYears > 0)
			{
				_reparations.Add(new ReparationsOrder
				{
					PayerId = defeatedId,
					PayeeId = victorId,
					AmountPerYear = settlement.ReparationsPerYear,
					YearsLeft = settlement.ReparationsYears
				});
			}
			if (settlement.CitiesDemanded > 0)
			{
				_pendingCityDemands.Add(new CityDemandOrder { FromId = defeatedId, ToId = victorId, Count = settlement.CitiesDemanded });
			}
		}
		Bus.Publish(new PolEvent(PolEventType.PeaceSettlement, "RP_EV_PEACE_SETTLEMENT")
		{
			Year = Year,
			Severity = Severity.Major,
			KingdomId = victorId
		}.With("victor", KingdomNameOf(victorId))
			.With("defeated", KingdomNameOf(defeatedId))
			.With("terms", settlement.TermsLocKey)
			.With("cities", settlement.CitiesDemanded.ToString())
			.With("gold", settlement.ReparationsPerYear.ToString("0.#"))
			.With("years", settlement.ReparationsYears.ToString())
			.With("war_years", warYears.ToString())
			.With("truce", Params.TruceYears.ToString())
			.With("casualties", casualtiesSummary));
		return settlement;
	}

	/// <summary>
	/// Тотальное поражение — у проигравшего не осталось городов. С 0.31.2 мирный договор
	/// никогда не восстанавливает уничтоженную державу: все уцелевшие довоенные города
	/// остаются у победителя как поглощённые вассальные земли со снимком старых границ.
	/// Если вассальные земли оформить нельзя (настройка выключена, городов мало или
	/// победитель структурно не может стать сюзереном), происходит обычная аннексия.
	/// Независимость позднее возможна только через восстание и войну за независимость.
	/// </summary>
	private PeaceSettlement ResolveTotalDefeat(int attackerId, int defenderId, int winnerId, int warYears, string casualtiesSummary = "")
	{
		int defeatedId = winnerId == attackerId ? defenderId : attackerId;
		int victorId = winnerId;
		KingdomPolitics victor = GetKingdom(victorId);
		KingdomPolitics defeated = GetKingdom(defeatedId);
		if (victor == null || defeated == null)
		{
			return null;
		}
		IReadOnlyList<int> prewar = PrewarCitiesOf(defeatedId);
		int truceUntil = Year + Params.TruceYears;
		victor.WarFatigue *= 0.5f;
		victor.Legitimacy = KingdomSimulation.Clamp(victor.Legitimacy + 8f, 0f, 100f);
		bool vassalRegistered = false;
		int absorbedCityCount = 0;
		if (Params.EnableRestoration && prewar.Count >= Math.Max(2, Params.RestorationMinCities))
		{
			vassalRegistered = TryRegisterAbsorbedVassalTerritory(
				victorId,
				defeatedId,
				KingdomNameOf(defeatedId),
				defeated.Ideology,
				prewar,
				prewar[0],
				0.45f);
			if (vassalRegistered)
			{
				UnionMember? absorbedMember = UnionOf(defeatedId)?.GetMember(defeatedId);
				absorbedCityCount = absorbedMember?.CityIds?.Count ?? 0;
			}
		}
		if (!vassalRegistered)
		{
			if (prewar.Count == 0 && _annexedBy.ContainsKey(defeatedId))
			{
				return null;
			}
			// Деревню не возрождают: полная аннексия, без «репараций с покойника».
			_annexedBy[defeatedId] = KingdomNameOf(victorId);
			Bus.Publish(new PolEvent(PolEventType.PeaceSettlement, "RP_EV_PEACE_SETTLEMENT")
			{
				Year = Year,
				Severity = Severity.Major,
				KingdomId = victorId
			}.With("victor", KingdomNameOf(victorId))
				.With("defeated", KingdomNameOf(defeatedId))
				.With("terms", "RP_WARTERMS_ANNEXED")
				.With("cities", prewar.Count.ToString())
				.With("gold", "0")
				.With("years", "0")
				.With("war_years", warYears.ToString())
				.With("truce", Params.TruceYears.ToString())
				.With("casualties", casualtiesSummary ?? string.Empty));
			ForgetPrewarCities(defeatedId);
			return new PeaceSettlement
			{
				VictorId = victorId,
				DefeatedId = defeatedId,
				Year = Year,
				Level = SettlementLevel.TotalDefeat,
				Score = 100f,
				CitiesDemanded = prewar.Count,
				TruceUntilYear = truceUntil
			};
		}
		// Никакого отдельного «восстановленного» государства и репараций с исчезнувшей
		// державы: города уже остались у сюзерена, а вассальные земли платят обычную
		// внутреннюю дань через механизм поглощённого вассалитета.
		_truces.Set(victorId, defeatedId, truceUntil);
		_grievances.Add(defeatedId, victorId, GrievanceReason.Conquest, 45f);
		DiplomaticMemories.Record(
			defeatedId,
			victorId,
			DiplomaticMemoryKind.AnnexedOurCity,
			Year,
			80,
			-40,
			KingdomNameOf(defeatedId));
		ForgetPrewarCities(defeatedId);
		PeaceSettlement settlement = new PeaceSettlement
		{
			VictorId = victorId,
			DefeatedId = defeatedId,
			Year = Year,
			Level = SettlementLevel.TotalDefeat,
			Score = 100f,
			CitiesDemanded = absorbedCityCount,
			TruceUntilYear = truceUntil
		};
		Bus.Publish(new PolEvent(PolEventType.PeaceSettlement, "RP_EV_PEACE_SETTLEMENT")
		{
			Year = Year,
			Severity = Severity.Major,
			KingdomId = victorId
		}.With("victor", KingdomNameOf(victorId))
			.With("defeated", KingdomNameOf(defeatedId))
			.With("terms", "RP_WARTERMS_TOTAL_VASSAL")
			.With("cities", absorbedCityCount.ToString())
			.With("gold", "0")
			.With("years", "0")
			.With("war_years", warYears.ToString())
			.With("truce", Params.TruceYears.ToString())
			.With("casualties", casualtiesSummary ?? string.Empty));
		return settlement;
	}

	/// <summary>
	/// Игровой слой создал державу после вооружённого национально-освободительного
	/// восстания или войны вассала за независимость — ядро оформляет её статус.
	/// Мирные и старые договорные приказы сюда не допускаются: они превращаются в
	/// поглощённые вассальные земли и отдельную державу не создают.
	/// </summary>
	public KingdomPolitics CompleteRestoration(RestorationOrder order, int newKingdomId, string newName)
	{
		if (order == null)
		{
			return null;
		}
		if (!order.IsNationalLiberation || order.LiberationPeaceful)
		{
			ConvertRestorationToAbsorbedVassal(order);
			return null;
		}
		if (newKingdomId <= 0)
		{
			return null;
		}
		KingdomPolitics state = GetKingdom(newKingdomId);
		IdeologyType ideology = IdeologyType.Monarchy;
		if (order.PrewarCityIds.Count > order.CitiesKeptByVictor)
		{
			CityHomelandRecord? homeland = CityHomelands.Get(order.PrewarCityIds[order.CitiesKeptByVictor]);
			if (homeland != null)
			{
				ideology = homeland.OriginalIdeology;
			}
		}
		if (state == null)
		{
			state = AddKingdom(newKingdomId, ideology, Year);
		}
		else
		{
			state.Ideology = ideology;
		}
		string effectiveName = string.IsNullOrEmpty(newName) ? order.DefeatedName : newName;
		if (!string.IsNullOrEmpty(effectiveName))
		{
			_kingdomNames[newKingdomId] = effectiveName;
			effectiveName = EnsureIdeologyStateName(state, effectiveName);
		}
		for (int i = 0; i < order.PrewarCityIds.Count; i++)
		{
			CityPolitics city = GetCity(order.PrewarCityIds[i]);
			if (city != null && i >= order.CitiesKeptByVictor)
			{
				city.KingdomId = newKingdomId;
				CityHomelands.RebindHomeland(city.CityId, newKingdomId, effectiveName);
			}
		}
		Union? leaderUnion = UnionOf(order.VictorId);
		if (leaderUnion != null && leaderUnion.Type == UnionType.Merger)
		{
			HashSet<int> releasedCities = new HashSet<int>();
			for (int rc = order.CitiesKeptByVictor; rc < order.PrewarCityIds.Count; rc++)
			{
				releasedCities.Add(order.PrewarCityIds[rc]);
			}
			leaderUnion.AbsorbedChildren.RemoveAll(c =>
				c.KingdomId == order.DefeatedId || c.CityIds.Exists(cid => releasedCities.Contains(cid)));
		}
		Union? vassalUnion = UnionOf(order.DefeatedId);
		if (vassalUnion != null && vassalUnion.Type == UnionType.Vassal)
		{
			vassalUnion.Members.RemoveAll(m => m.KingdomId == order.DefeatedId);
			if (vassalUnion.MemberCount <= 1)
			{
				_unions.Remove(vassalUnion);
			}
		}
		state.Legitimacy = 55f;
		state.Support = 65f;
		CityHomelands.RecordLiberationAttempt(newKingdomId, Year);
		CityHomelands.RecordLiberationAttempt(order.VictorId, Year);
		CityHomelands.RecordLiberationAttempt(order.DefeatedId, Year);
		_grievances.Add(order.VictorId, newKingdomId, GrievanceReason.Conquest, 35f);
		_grievances.Add(newKingdomId, order.VictorId, GrievanceReason.Conquest, 35f);
		return state;
	}

	/// <summary>Этап 22: годовой сбор репараций (в формате дани — исполняется той же
	/// передачей золота в игровом слое). Исполненные годы вычитаются из срока.</summary>
	public List<TributeOrder> CollectReparations()
	{
		List<TributeOrder> orders = new List<TributeOrder>();
		if (!Params.EnableWarPeace)
		{
			return orders;
		}
		for (int i = _reparations.Count - 1; i >= 0; i--)
		{
			ReparationsOrder order = _reparations[i];
			if (GetKingdom(order.PayerId) == null || GetKingdom(order.PayeeId) == null)
			{
				_reparations.RemoveAt(i);
				continue;
			}
			if (order.YearsLeft <= 0)
			{
				_reparations.RemoveAt(i);
				continue;
			}
			orders.Add(new TributeOrder { VassalId = order.PayerId, SuzerainId = order.PayeeId, Amount = order.AmountPerYear });
			order.YearsLeft--;
			if (order.YearsLeft <= 0)
			{
				_reparations.RemoveAt(i);
			}
		}
		return orders;
	}

	/// <summary>Этап 20: заключить договор между двумя державами (шанс зависит от отношений и сродства).</summary>
	public bool TrySignTreaty(int a, int b, TreatyType type, bool atWar, out string result)
	{
        result = string.Empty;
        if(!Governance.Recognition.CanContact(a,b,InternationalContact.Trade))
        {result=CoreText.L("RP_GOV26_CONTACT_REFUSED","международное признание требуется для мирных связей");return false;}
		if (!Params.EnableTreaties)
		{
			result = CoreText.L("RP_RES_TREATIES_DISABLED", "договоры отключены настройкой");
			return false;
		}
		KingdomPolitics ka = GetKingdom(a);
		KingdomPolitics kb = GetKingdom(b);
		if (ka == null || kb == null || a == b)
		{
			result = CoreText.L("RP_RES_TREATY_PARTIES_UNAVAILABLE", "державы недоступны для договора");
			return false;
		}
		if (atWar)
		{
			result = CoreText.L("RP_RES_TREATY_AT_WAR", "державы в войне — договор невозможен");
			return false;
		}
		if (_treaties.Between(a, b, type) != null)
		{
			result = CoreText.L("RP_RES_TREATY_EXISTS", "такой договор уже действует");
			return false;
		}
		int relation = GetRulerRelation(a, b);
		if (type == TreatyType.DefensePact && relation < 10)
		{
			result = CoreText.L("RP_RES_PACT_NO_TRUST", "недостаточно доверия для оборонительного пакта");
			return false;
		}
		float chance = 0.6f + relation / 200f
			+ (type == TreatyType.NonAggression ? 0.1f : 0f)
			+ (Ideologies.Affinity(ka.Ideology, kb.Ideology) > 0f ? 0.05f : -0.05f);
		if (Rng.NextDouble() >= chance)
		{
			result = CoreText.L("RP_RES_TREATY_REJECTED", "одна из сторон отвергла договор");
			return false;
		}
		_treaties.Add(new Treaty
		{
			Type = type,
			PartyA = a,
			PartyB = b,
			SignedYear = Year,
			ExpiresYear = Year + Params.TreatyYears
		});
		ChangeRulerRelation(a, b, type == TreatyType.DefensePact ? 25 : 15);
		Bus.Publish(new PolEvent(PolEventType.TreatySigned, "RP_EV_TREATY_SIGNED")
		{
			Year = Year,
			Severity = Severity.Minor,
			KingdomId = a
		}.With("kingdom", KingdomNameOf(a))
			.With("partner", KingdomNameOf(b))
			.With("treaty", Treaty.TypeLocKey(type)));
		result = CoreText.L("RP_RES_TREATY_SIGNED", "договор заключён");
		return true;
	}

	/// <summary>Этап 20: нарушение договора. Нарушитель платит репутацией, обиженный копит претензии.</summary>
	public void BreakTreaty(int violatorId, int victimId, Treaty treaty, GrievanceReason reason)
	{
		if (treaty == null || !_treaties.Remove(treaty))
		{
			return;
		}
		KingdomPolitics violator = GetKingdom(violatorId);
		if (violator != null)
		{
			violator.Legitimacy = Math.Max(0f, violator.Legitimacy - 10f);
			violator.Mechanics.ThreatLevel = Math.Min(100f, violator.Mechanics.ThreatLevel + 10f);
		}
		ChangeRulerRelation(violatorId, victimId, -30);
		_grievances.Add(victimId, violatorId, reason, 30f);
		DiplomaticMemories.Record(victimId, violatorId, DiplomaticMemoryKind.BrokeTreaty, Year, 60, -35, Treaty.TypeLocKey(treaty.Type));
		Bus.Publish(new PolEvent(PolEventType.TreatyBroken, "RP_EV_TREATY_BROKEN")
		{
			Year = Year,
			Severity = Severity.Major,
			KingdomId = violatorId
		}.With("kingdom", KingdomNameOf(violatorId))
			.With("partner", KingdomNameOf(victimId))
			.With("treaty", Treaty.TypeLocKey(treaty.Type)));
	}

	public void AddGrievance(int from, int to, GrievanceReason reason, float amount)
	{
		_grievances.Add(from, to, reason, amount);
	}

	public float GetGrievance(int from, int to)
	{
		return _grievances.Get(from, to);
	}

	/// <summary>Этап 20: лучший доступный повод к войне против державы.</summary>
	public CasusBelliType BestCasusBelli(int attackerId, int defenderId, int claims)
	{
		if (!Params.EnableCasusBelli)
		{
			return CasusBelliType.Conquest;
		}
		KingdomPolitics attacker = GetKingdom(attackerId);
		KingdomPolitics defender = GetKingdom(defenderId);
		if (attacker == null || defender == null)
		{
			return CasusBelliType.Conquest;
		}
		if (DiplomaticMemories.TryFindIrredentistTarget(attackerId, Year, out int holderId, out _) && holderId == defenderId)
		{
			claims = Math.Max(claims, 1);
		}
		float affinity = Ideologies.Affinity(attacker.Ideology, defender.Ideology);
		bool treatyBroken = _grievances.LastReason(attackerId, defenderId) == GrievanceReason.TreatyBroken
			&& _grievances.Get(attackerId, defenderId) >= 15f;
		return CasusBelliDefs.Best(_grievances.Get(attackerId, defenderId), claims, affinity, treatyBroken);
	}

	/// <summary>Этап 20: оформить объявленную войну — цена казус белли, обиды, призыв союзников.</summary>
	public void ApplyDeclareWar(int attackerId, int defenderId, CasusBelliType cb)
	{
		KingdomPolitics attacker = GetKingdom(attackerId);
		KingdomPolitics defender = GetKingdom(defenderId);
		CasusBelliDef def = CasusBelliDefs.Get(cb);
		if (attacker != null)
		{
			// 0.24: статья XI «О чужой земле» множит цену захватнических войн.
			float landMul = 1f;
			if (attacker.Constitution != null
				&& (cb == CasusBelliType.Conquest || cb == CasusBelliType.Reconquest || cb == CasusBelliType.Independence))
			{
				landMul = ConstitutionEffects.LandCasusBelliMultiplier(attacker.Constitution.Land, attacker.Constitution.Supremacy == SupremacyRule.Binding);
			}
			attacker.Legitimacy = KingdomSimulation.Clamp(attacker.Legitimacy - def.LegitimacyCost * landMul, 0f, 100f);
			attacker.Support = KingdomSimulation.Clamp(attacker.Support - def.SupportCost * landMul, 0f, 100f);
			attacker.AvgDissent = KingdomSimulation.Clamp(attacker.AvgDissent + def.DissentCost * landMul, 0f, 1f);
			attacker.Mechanics.ThreatLevel = Math.Min(100f, attacker.Mechanics.ThreatLevel + def.ThreatGain);
		}
		// Война по определению сжигает договоры между сторонами.
		Treaty burned = _treaties.AnyBetween(attackerId, defenderId);
		if (burned != null)
		{
			BreakTreaty(attackerId, defenderId, burned, GrievanceReason.TreatyBroken);
		}
		// Этап 22: нападение в перемирие — вероломство. Дорогая легитимность,
		// вспышка угрозы и тяжёлая обида жертвы, перемирие сгорает.
		if (Params.EnableWarPeace && _truces.Active(attackerId, defenderId, Year))
		{
			_truces.Remove(attackerId, defenderId);
			if (attacker != null)
			{
				attacker.Legitimacy = KingdomSimulation.Clamp(attacker.Legitimacy - 15f, 0f, 100f);
				attacker.Support = KingdomSimulation.Clamp(attacker.Support - 10f, 0f, 100f);
				attacker.Mechanics.ThreatLevel = Math.Min(100f, attacker.Mechanics.ThreatLevel + 12f);
			}
			ChangeRulerRelation(attackerId, defenderId, -40);
			// 0.39: сорванное перемирие помнят и жители самой державы — не только соседи.
			PromiseMemory.Record(attacker, PromiseMemory.BrokenTruce, Year);
			_grievances.Add(defenderId, attackerId, GrievanceReason.TreatyBroken, 40f);
			DiplomaticMemories.Record(defenderId, attackerId, DiplomaticMemoryKind.BrokeTreaty, Year, 75, -45, "Truce");
			Bus.Publish(new PolEvent(PolEventType.TruceBroken, "RP_EV_TRUCE_BROKEN")
			{
				Year = Year,
				Severity = Severity.Major,
				KingdomId = attackerId
			}.With("kingdom", KingdomNameOf(attackerId))
				.With("partner", KingdomNameOf(defenderId)));
		}
		_grievances.Add(defenderId, attackerId,
			cb == CasusBelliType.Conquest ? GrievanceReason.Conquest : GrievanceReason.War,
			def.VictimGrievance);
		_warDeclarations.Add(new WarDeclarationRecord
		{
			AttackerId = attackerId,
			DefenderId = defenderId,
			Year = Year,
			CasusBelli = cb
		});
		RememberPrewarCities(attackerId, CityIdsOf(attackerId));
		RememberPrewarCities(defenderId, CityIdsOf(defenderId));
		// Журнал объявлений войны иначе рос без предела (и целиком уходил в сохранение):
		// для агрессии нужны 10 лет, для казус белли мирного договора — живые войны; 60 лет с запасом.
		while (_warDeclarations.Count > 0 && Year - _warDeclarations[0].Year > 60)
		{
			_warDeclarations.RemoveAt(0);
		}
		bool defensive = cb == CasusBelliType.Defense || cb == CasusBelliType.Independence;
		// Вступление в войну на стороне союзника — не новая агрессия: пакты и коалиции
		// самого агрессора не должны подниматься «на защиту» от защитников (цепная реакция).
		if (cb != CasusBelliType.Defense)
		{
			QueueDefenseCalls(defenderId, attackerId);
		}
		Bus.Publish(new PolEvent(PolEventType.CasusBelliDeclared, "RP_EV_CASUS_BELLI")
		{
			Year = Year,
			Severity = defensive ? Severity.Major : Severity.Critical,
			KingdomId = attackerId
		}.With("kingdom", KingdomNameOf(attackerId))
			.With("target", KingdomNameOf(defenderId))
			.With("cb", def.LocKey));
	}

	/// <summary>Очки агрессии державы за последние 10 лет: коалиции собираются против самых буйных.</summary>
	public float AggressionScoreOf(int kingdomId)
	{
		float score = 0f;
		for (int i = 0; i < _warDeclarations.Count; i++)
		{
			WarDeclarationRecord record = _warDeclarations[i];
			if (record.AttackerId != kingdomId || Year - record.Year >= 10)
			{
				continue;
			}
			switch (record.CasusBelli)
			{
				case CasusBelliType.Conquest: score += 30f; break;
				case CasusBelliType.BrokenTreaty: score += 15f; break;
				case CasusBelliType.IdeologicalLiberation: score += 12f; break;
				case CasusBelliType.Revenge: score += 10f; break;
				case CasusBelliType.Reconquest: score += 8f; break;
			}
		}
		return score;
	}

	/// <summary>Этап 20: расширить разведсеть на один уровень (стоимость проверяет вызывающий).</summary>
	public bool InvestEspionage(int kingdomId, float goldAvailable, out string result)
	{
		result = string.Empty;
		if (!Params.EnableEspionage)
		{
			result = CoreText.L("RP_RES_SPY_DISABLED", "шпионаж отключён настройкой");
			return false;
		}
		KingdomPolitics kingdom = GetKingdom(kingdomId);
		if (kingdom == null)
		{
			result = CoreText.L("RP_RES_STATE_UNAVAILABLE", "государство недоступно");
			return false;
		}
		if (kingdom.Spy == null)
		{
			kingdom.Spy = new SpyNetwork();
		}
		if (kingdom.Spy.Level >= SpyOps.MaxNetworkLevel)
		{
			result = CoreText.L("RP_RES_SPY_MAX", "разведсеть уже максимального уровня");
			return false;
		}
		if (kingdom.Spy.LastInvestYear != int.MinValue && Year - kingdom.Spy.LastInvestYear < 2)
		{
			result = CoreText.L("RP_RES_SPY_COOLDOWN", "расширение сети требует времени");
			return false;
		}
		if (goldAvailable < Params.SpyInvestCost)
		{
			result = CoreText.L("RP_RES_NO_GOLD", "казне не хватает золота");
			return false;
		}
		kingdom.Spy.Level++;
		kingdom.Spy.LastInvestYear = Year;
		result = CoreText.L("RP_RES_SPY_EXPANDED", "разведсеть расширена до уровня ") + kingdom.Spy.Level;
		return true;
	}

	/// <summary>Этап 20: тайная операция против другой державы. Возврат: успех операции.</summary>
	public bool TryCovertOp(int actorId, int targetId, SpyOpType op, out string result)
	{
		result = string.Empty;
		if (!Params.EnableEspionage)
		{
			result = CoreText.L("RP_RES_SPY_DISABLED", "шпионаж отключён настройкой");
			return false;
		}
		KingdomPolitics actor = GetKingdom(actorId);
		KingdomPolitics target = GetKingdom(targetId);
		if (actor == null || target == null || actorId == targetId)
		{
			result = CoreText.L("RP_RES_COVERT_TARGET_UNAVAILABLE", "цель недоступна для операции");
			return false;
		}
		if (actor.Spy == null || actor.Spy.Level < 1)
		{
			result = CoreText.L("RP_RES_NO_SPY_NETWORK", "нет разведсети — некому действовать");
			return false;
		}
		if (actor.Spy.LastOpYear != int.MinValue && Year - actor.Spy.LastOpYear < Params.SpyCooldownYears)
		{
			result = CoreText.L("RP_RES_AGENTS_NOT_READY", "агенты ещё не готовы к новой операции");
			return false;
		}
		actor.Spy.LastOpYear = Year;
		SpyOpDef opDef = SpyOps.Get(op);
		bool success = Rng.NextDouble() < SpyOps.SuccessChance(op, actor.Spy.Level);
		if (success)
		{
			switch (op)
			{
				case SpyOpType.Sabotage:
					if (target.Economy != null)
					{
						target.Economy.Prosperity = KingdomSimulation.Clamp(target.Economy.Prosperity - 12f, 0f, 100f);
					}
					target.Mechanics.ThreatLevel = Math.Min(100f, target.Mechanics.ThreatLevel + 10f);
					break;
				case SpyOpType.InciteDissent:
					target.AvgDissent = KingdomSimulation.Clamp(target.AvgDissent + 0.12f, 0f, 1f);
					target.Support = KingdomSimulation.Clamp(target.Support - 6f, 0f, 100f);
					target.Mechanics.ThreatLevel = Math.Min(100f, target.Mechanics.ThreatLevel + 15f);
					break;
				case SpyOpType.StealGold:
					// Золото живёт в игре: перевод исполняет игровой слой.
					_pendingGoldTransfers.Add(new KeyValuePair<int, int>(targetId, actorId));
					break;
			}
			Bus.Publish(new PolEvent(PolEventType.EspionageSuccess, "RP_EV_ESPIONAGE_SUCCESS")
			{
				Year = Year,
				Severity = Severity.Trivial,
				KingdomId = actorId
			}.With("kingdom", KingdomNameOf(actorId))
				.With("target", KingdomNameOf(targetId))
				.With("op", opDef.LocKey));
			result = CoreText.L("RP_RES_COVERT_SUCCESS", "тайная операция удалась");
			return true;
		}
		bool exposed = Rng.NextDouble() < opDef.ExposureOnFail;
		if (exposed)
		{
			actor.Spy.Level = Math.Max(0, actor.Spy.Level - 1);
			actor.Legitimacy = Math.Max(0f, actor.Legitimacy - 8f);
			ChangeRulerRelation(actorId, targetId, -40);
			_grievances.Add(targetId, actorId, GrievanceReason.Espionage, 35f);
			Bus.Publish(new PolEvent(PolEventType.EspionageExposed, "RP_EV_ESPIONAGE_EXPOSED")
			{
				Year = Year,
				Severity = Severity.Major,
				KingdomId = targetId
			}.With("kingdom", KingdomNameOf(targetId))
				.With("spy", KingdomNameOf(actorId))
				.With("op", opDef.LocKey));
			result = CoreText.L("RP_RES_COVERT_EXPOSED", "операция провалилась, агенты разоблачены");
		}
		else
		{
			result = CoreText.L("RP_RES_COVERT_FAILED_CLEAN", "операция сорвалась, но агенты ушли чисто");
		}
		return false;
	}

	/// <summary>Этап 20: приказы о дани вассалов сюзеренам; перевод золота исполняет игровой слой.</summary>
	public List<TributeOrder> CollectTributes()
	{
		List<TributeOrder> orders = new List<TributeOrder>();
		if (Params.VassalTributeRate <= 0f)
		{
			return orders;
		}
		for (int i = 0; i < _unions.Count; i++)
		{
			Union union = _unions[i];
			if (union.Type != UnionType.Vassal)
			{
				continue;
			}
			for (int m = 0; m < union.Members.Count; m++)
			{
				UnionMember member = union.Members[m];
				if (member.IsLeader)
				{
					continue;
				}
				KingdomPolitics vassal = GetKingdom(member.KingdomId);
				if (vassal == null)
				{
					// Вассал влит в сюзерена: дань взимается внутри страны, разъедая лояльность вассальных земель.
					KingdomPolitics? suzerain = GetKingdom(union.LeaderId);
					if (suzerain != null && member.CityIds != null && member.CityIds.Count > 0)
					{
						float extraDrain = suzerain.Legitimacy < 45f ? 0.02f : 0f;
						member.Approval = Math.Max(0f, member.Approval - (0.01f + extraDrain));
					}
					continue;
				}
				if (vassal.Economy == null || vassal.Economy.LastIncome <= 0f)
				{
					continue;
				}
				float amount = vassal.Economy.LastIncome * Params.VassalTributeRate;
				orders.Add(new TributeOrder
				{
					VassalId = member.KingdomId,
					SuzerainId = union.LeaderId,
					Amount = amount
				});
				// Дань разъедает лояльность вассала — путь к войне за независимость.
				member.Approval = Math.Max(0f, member.Approval - 0.01f);
			}
		}
		return orders;
	}

	/// <summary>Этап 20: вассал объявляет войну за независимость — выходит из унии и жжёт мосты.</summary>
	public bool LeaveUnionForIndependence(int vassalId, out string result)
	{
		result = string.Empty;
		Union union = UnionOf(vassalId);
		if (union == null || union.Type != UnionType.Vassal || union.LeaderId == vassalId)
		{
			result = CoreText.L("RP_RES_NOT_VASSAL", "держава не состоит в вассальной унии");
			return false;
		}
		int suzerainId = union.LeaderId;
		UnionMember? member = union.GetMember(vassalId);
		string vassalName = member != null && !string.IsNullOrWhiteSpace(member.OriginalName) ? member.OriginalName : KingdomNameOf(vassalId);
		union.Members.RemoveAll(x => x.KingdomId == vassalId);
		KingdomPolitics vassal = GetKingdom(vassalId);
		if (vassal != null && vassal.Union == union)
		{
			vassal.Union = null;
		}
		if (union.MemberCount <= 1)
		{
			_unions.Remove(union);
		}
		ChangeRulerRelation(vassalId, suzerainId, -30);
		_grievances.Add(suzerainId, vassalId, GrievanceReason.BrokenUnion, 25f);
		bool queuedRestoration = TryQueueVassalIndependenceWar(union, member);
		if (!queuedRestoration)
		{
				if (vassal != null && CityIdsOf(vassalId).Count > 0)
				{
					QueueIndependenceWar(vassalId, suzerainId);
				}
				Bus.Publish(new PolEvent(PolEventType.VassalRebellion, "RP_EV_VASSAL_REBELLION")
				{
					Year = Year,
					Severity = Severity.Critical,
					KingdomId = vassalId
				}.With("kingdom", string.IsNullOrWhiteSpace(vassalName) ? KingdomNameOf(vassalId) : vassalName)
					.With("suzerain", KingdomNameOf(suzerainId))
					.With("confirmed", "0"));
			}
		result = CoreText.L("RP_RES_VASSAL_REVOLT", "вассал восстал против сюзерена");
		return true;
	}

	/// <summary>Годовой тик глубокой дипломатии: сроки договоров, затухание обид, жизнь коалиций.</summary>
	private void TickDiplomacyDeep(List<KingdomInput> inputs)
	{
		if (Params.EnableTreaties)
		{
			// Война сжигает договоры — даже если её начала сама игра, а не наш ИИ.
			if (inputs != null)
			{
				for (int w = 0; w < inputs.Count; w++)
				{
					KingdomInput wInput = inputs[w];
					if (wInput.Relations == null)
					{
						continue;
					}
					for (int r = 0; r < wInput.Relations.Count; r++)
					{
						RelationInfo rel = wInput.Relations[r];
						if (rel.Relation != RelationType.War || wInput.KingdomId >= rel.OtherId)
						{
							continue;
						}
						Treaty burned = _treaties.AnyBetween(wInput.KingdomId, rel.OtherId);
						if (burned != null)
						{
							BreakTreaty(wInput.KingdomId, rel.OtherId, burned, GrievanceReason.TreatyBroken);
						}
					}
				}
			}
			List<Treaty> expired = _treaties.Expire(Year);
			for (int i = 0; i < expired.Count; i++)
			{
				Treaty treaty = expired[i];
				Bus.Publish(new PolEvent(PolEventType.TreatyExpired, "RP_EV_TREATY_EXPIRED")
				{
					Year = Year,
					Severity = Severity.Trivial,
					KingdomId = treaty.PartyA
				}.With("kingdom", KingdomNameOf(treaty.PartyA))
					.With("partner", KingdomNameOf(treaty.PartyB))
					.With("treaty", Treaty.TypeLocKey(treaty.Type)));
			}
		}
		if (Params.EnableCasusBelli)
		{
			_grievances.Decay(4f);
		}
		// Этап 22: истёкшие перемирия снимаются — о них пишет только летопись.
		if (Params.EnableWarPeace)
		{
			List<KeyValuePair<int, int>> expiredTruces = _truces.Expire(Year);
			for (int i = 0; i < expiredTruces.Count; i++)
			{
				Bus.Publish(new PolEvent(PolEventType.TruceExpired, "RP_EV_TRUCE_EXPIRED")
				{
					Year = Year,
					Severity = Severity.Trivial,
					KingdomId = expiredTruces[i].Key
				}.With("kingdom", KingdomNameOf(expiredTruces[i].Key))
					.With("partner", KingdomNameOf(expiredTruces[i].Value)));
			}
		}
		if (Params.EnableCoalitions)
		{
			TickCoalitions();
		}
	}

	private void TickCoalitions()
	{
		// Сбор коалиций против агрессоров, накопивших достаточно очков.
		List<int> kingdomIds = new List<int>(_kingdoms.Keys);
		for (int i = 0; i < kingdomIds.Count; i++)
		{
			int targetId = kingdomIds[i];
			if (_coalitions.Against(targetId) != null || AggressionScoreOf(targetId) < Params.CoalitionAggressionThreshold)
			{
				continue;
			}
			Union targetUnion = UnionOf(targetId);
			List<int> founders = new List<int>();
			for (int j = 0; j < kingdomIds.Count; j++)
			{
				int candidateId = kingdomIds[j];
                if(Governance.Enabled && !Governance.Recognition.IsRecognized(candidateId))continue;
				if (candidateId == targetId || _coalitions.MemberOf(candidateId) != null)
				{
					continue;
				}
				if (targetUnion != null && targetUnion.Contains(candidateId))
				{
					continue;
				}
				float grievance = _grievances.Get(candidateId, targetId);
				int relation = GetRulerRelation(candidateId, targetId);
				if (grievance >= 20f || relation <= -20)
				{
					founders.Add(candidateId);
				}
			}
			if (founders.Count >= 2)
			{
				Coalition coalition = _coalitions.Create(targetId, Year);
				coalition.Members.AddRange(founders);
				Bus.Publish(new PolEvent(PolEventType.CoalitionFormed, "RP_EV_COALITION_FORMED")
				{
					Year = Year,
					Severity = Severity.Critical,
					KingdomId = founders[0]
				}.With("target", KingdomNameOf(targetId))
					.With("count", founders.Count));
			}
		}
		// Роспуск: цель исчезла или мир продержался достаточно долго.
		List<Coalition> snapshot = new List<Coalition>(_coalitions.All);
		for (int i = 0; i < snapshot.Count; i++)
		{
			Coalition coalition = snapshot[i];
			bool targetGone = GetKingdom(coalition.TargetId) == null;
			if (targetGone || Year - coalition.LastWarYear >= Params.CoalitionPeaceYears)
			{
				_coalitions.Remove(coalition);
				Bus.Publish(new PolEvent(PolEventType.CoalitionDissolved, "RP_EV_COALITION_DISSOLVED")
				{
					Year = Year,
					Severity = Severity.Minor,
					KingdomId = coalition.TargetId
				}.With("target", KingdomNameOf(coalition.TargetId)));
			}
		}
	}

	/// <summary>Союзнические обязательства: кто поднимается на защиту атакованной державы.</summary>
	private void QueueDefenseCalls(int defenderId, int attackerId)
	{
		// Оборонительные пакты.
		if (Params.EnableTreaties)
		{
			List<Treaty> pacts = _treaties.Of(defenderId);
			for (int i = 0; i < pacts.Count; i++)
			{
				if (pacts[i].Type != TreatyType.DefensePact)
				{
					continue;
				}
				int partner = pacts[i].OtherParty(defenderId);
				if (partner != attackerId && GetKingdom(partner) != null
					&& !_pendingDefenseCalls.Contains(new KeyValuePair<int, int>(partner, attackerId)))
				{
					_pendingDefenseCalls.Add(new KeyValuePair<int, int>(partner, attackerId));
					// Пакт — обязательство на срок договора, а не одноразовый талон:
					// он остаётся в силе и сработает снова при следующем нападении.
					Bus.Publish(new PolEvent(PolEventType.CoalitionWar, "RP_EV_COALITION_WAR")
					{
						Year = Year,
						Severity = Severity.Major,
						KingdomId = partner
					}.With("kingdom", KingdomNameOf(partner))
						.With("target", KingdomNameOf(attackerId))
						.With("ally", KingdomNameOf(defenderId)));
				}
			}
		}
		// Коалиция против агрессора: нападение на участника поднимает весь блок.
		Coalition coalition = _coalitions.MemberOf(defenderId);
		if (coalition != null && coalition.TargetId == attackerId)
		{
			coalition.LastWarYear = Year;
			List<int> others = coalition.OtherMembers(defenderId);
			for (int i = 0; i < others.Count; i++)
			{
				int member = others[i];
				if (GetKingdom(member) == null
					|| _pendingDefenseCalls.Contains(new KeyValuePair<int, int>(member, attackerId)))
				{
					continue;
				}
				_pendingDefenseCalls.Add(new KeyValuePair<int, int>(member, attackerId));
				Bus.Publish(new PolEvent(PolEventType.CoalitionWar, "RP_EV_COALITION_WAR")
				{
					Year = Year,
					Severity = Severity.Major,
					KingdomId = member
				}.With("kingdom", KingdomNameOf(member))
					.With("target", KingdomNameOf(attackerId))
					.With("ally", KingdomNameOf(defenderId)));
			}
		}
		// 0.24: соединившиеся державы — уже одно государство; отдельной «федеративной обороны» нет.
	}

	private void TickDynasties(KingdomPolitics state, KingdomInput input)
	{
		if (!Params.EnableDynasties)
		{
			return;
		}
		// Личность назначается при воцарении; государствам из старых сохранений выдаём при первом тике.
		// 0.23.11: личность — только у законного правителя; регент её не получает
		// (личность остаётся за наследником).
		if (state.Personality == null && input.Ruler != null && !input.Ruler.IsRegent)
		{
			state.Personality = RulerPersonality.Roll(DynastyRng);
		}
		RulerTrait trait = state.Personality?.Trait ?? RulerTrait.None;
		if (state.Personality != null)
		{
			state.Legitimacy = KingdomSimulation.Clamp(state.Legitimacy + RulerTraits.YearlyLegitimacy(trait), 0f, 100f);
			state.Support = KingdomSimulation.Clamp(state.Support + RulerTraits.YearlySupport(trait), 0f, 100f);
			state.AvgDissent = KingdomSimulation.Clamp(state.AvgDissent + RulerTraits.YearlyDissent(trait), 0f, 1f);
		}
		// Личные отношения правителей дрейфуют вслед за реальными отношениями держав.
		if (input.Relations != null)
		{
			for (int i = 0; i < input.Relations.Count; i++)
			{
				int otherId = input.Relations[i].OtherId;
				if (otherId == state.KingdomId)
				{
					continue;
				}
				if (input.Relations[i].Relation == RelationType.War)
				{
					ChangeRulerRelation(state.KingdomId, otherId, -3);
				}
				else if (input.Relations[i].Relation == RelationType.Alliance)
				{
					ChangeRulerRelation(state.KingdomId, otherId, 1);
				}
			}
			// Узы брака рвутся, если супруги пошли друг на друга войной.
			// Штраф −40 применяется один раз на пару: второй супруг лишь фиксирует уже разорванные узы.
			if (state.Marriage != null)
			{
				int partnerId = state.Marriage.PartnerId;
				for (int i = 0; i < input.Relations.Count; i++)
				{
					if (input.Relations[i].OtherId == partnerId && input.Relations[i].Relation == RelationType.War)
					{
						KingdomPolitics partnerState = GetKingdom(partnerId);
						bool alreadyBroken = partnerState != null && partnerState.Marriage == null;
						state.Marriage = null;
						if (!alreadyBroken)
						{
							ChangeRulerRelation(state.KingdomId, partnerId, -40);
						}
						break;
					}
				}
			}
		}
		// Узы рвутся, если супруга больше нет (или его узы указывают не на нас — асимметрия после загрузки).
		if (state.Marriage != null)
		{
			KingdomPolitics partner = GetKingdom(state.Marriage.PartnerId);
			if (partner == null || (partner.Marriage != null && partner.Marriage.PartnerId != state.KingdomId))
			{
				state.Marriage = null;
			}
		}
	}

	/// <summary>
	/// Этап 19: попытка династического брака между двумя наследственными державами.
	/// Успех связывает дома узами (претензия на корону при пресечении), улучшает отношения
	/// и публикует событие; отказ объясняется строкой результата для диагностики и UI.
	/// </summary>
	public bool TryMarry(int aId, int bId, bool atWar, out string result)
	{
		result = string.Empty;
		if (!Params.EnableDynasties)
		{
			result = CoreText.L("RP_RES_DYNASTIES_DISABLED", "династии отключены настройкой");
			return false;
		}
		KingdomPolitics a = GetKingdom(aId);
		KingdomPolitics b = GetKingdom(bId);
		if (a == null || b == null || aId == bId)
		{
			result = CoreText.L("RP_RES_MARRIAGE_UNAVAILABLE", "державы недоступны для брака");
			return false;
		}
		if (!Ideologies.CanDynasticMarriage(a.Ideology) || !Ideologies.CanDynasticMarriage(b.Ideology))
		{
			result = CoreText.L("RP_RES_MARRIAGE_MONARCHIES_ONLY", "брак возможен только между династическими державами (монархия, империя или каганат)");
			return false;
		}
		if (a.Ruler == null || b.Ruler == null)
		{
			result = CoreText.L("RP_RES_NO_RULER", "у одной из держав нет правителя");
			return false;
		}
		if (a.Ruler.Age < Params.MarriageMinAge || b.Ruler.Age < Params.MarriageMinAge)
		{
			result = CoreText.L("RP_RES_RULER_TOO_YOUNG", "правитель слишком юн для брака");
			return false;
		}
		if (atWar)
		{
			result = CoreText.L("RP_RES_MARRIAGE_AT_WAR", "державы в войне — брак невозможен");
			return false;
		}
		if (a.Marriage != null || b.Marriage != null)
		{
			result = CoreText.L("RP_RES_HOUSE_ALREADY_MARRIED", "один из домов уже связан браком");
			return false;
		}
		if (a.Union != null && b.Union != null && a.Union.Id == b.Union.Id)
		{
			result = CoreText.L("RP_RES_HOUSES_SAME_UNION", "дома уже в одном союзе");
			return false;
		}
		float chance = Params.MarriageBaseChance
			+ GetRulerRelation(aId, bId) / 200f
			+ (RulerTraits.MarriageBias(a.Personality?.Trait ?? RulerTrait.None)
				+ RulerTraits.MarriageBias(b.Personality?.Trait ?? RulerTrait.None)) / 200f
			+ (Ideologies.Affinity(a.Ideology, b.Ideology) > 0f ? 0.1f : -0.1f);
		if (Rng.NextDouble() >= chance)
		{
			result = CoreText.L("RP_RES_MARRIAGE_DECLINED", "дома отклонили предложение брака");
			return false;
		}
		a.Marriage = new MarriageBond { PartnerId = bId, Year = Year };
		b.Marriage = new MarriageBond { PartnerId = aId, Year = Year };
		ChangeRulerRelation(aId, bId, 40);
		Bus.Publish(new PolEvent(PolEventType.DynasticMarriage, "RP_EV_DYNASTIC_MARRIAGE")
		{
			Year = Year,
			Severity = Severity.Major,
			KingdomId = aId
		}.With("kingdom", KingdomNameOf(aId))
			.With("partner", KingdomNameOf(bId))
			.With("ruler", a.Ruler.Name)
			.With("spouse", b.Ruler.Name));
		result = CoreText.L("RP_RES_MARRIAGE_CONCLUDED", "династический брак заключён");
		return true;
	}

	public int ReligionAppeared(int id)
	{
		if (!_religionOrigin.TryGetValue(id, out var value))
		{
			return 0;
		}
		return value;
	}

	public List<IdeologyType> IdeologySpread()
	{
		Dictionary<IdeologyType, int> counts = new Dictionary<IdeologyType, int>();
		foreach (ActorEntry value2 in _actors.Values)
		{
			counts.TryGetValue(value2.Politics.Ideology, out var value);
			counts[value2.Politics.Ideology] = value + 1;
		}
		List<IdeologyType> list = new List<IdeologyType>(counts.Keys);
		list.Sort((IdeologyType x, IdeologyType y) => counts[y].CompareTo(counts[x]));
		return list;
	}

	public void Clear()
	{
        Governance = new GovernanceState();
		DiplomaticMemories.Clear();
		CityHomelands.Clear();
		RecognitionDiplomacyEngine.ClearCache();
		_kingdoms.Clear();
		_cities.Clear();
		_actors.Clear();
		_actorsByCity.Clear();
		_religionOrigin.Clear();
		_languageOrigin.Clear();
		_cultureOrigin.Clear();
		_borders.Clear();
		_collapses.Clear();
		_lastRuler.Clear();
		_lastRulerRef.Clear();
		_unions.Clear();
		_economyReports.Clear();
		_kingdomNames.Clear();
		_rulerRelations.Clear();
		_treaties.Clear();
		_grievances.Clear();
		_coalitions.Clear();
		_warDeclarations.Clear();
		_pendingDefenseCalls.Clear();
		_pendingGoldTransfers.Clear();
		_pendingIndependenceWars.Clear();
		_truces.Clear();
		_reparations.Clear();
		_pendingCityDemands.Clear();
		_pendingRestorations.Clear();
		_prewarCities.Clear();
		Trade.Clear();
		Bus.Clear();
		Deaths.Clear();
        ChronicleCauses.Clear();
		Family.Load(null);
		_alliances.Clear();
		_allianceBaseline = false;
		_pendingCityTransfers.Clear();
		_pendingMergers.Clear();
		PendingSecessions.Clear();
		PendingWorldOps.Clear();
		_warsSnapshot.Clear();
		_nextUnionId = 1;
		_nextBreakawayId = 1;
		Year = 0;
		BaselineRecorded = false;
	}
}
