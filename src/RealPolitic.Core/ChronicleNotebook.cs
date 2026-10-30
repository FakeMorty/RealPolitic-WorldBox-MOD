using System;
using System.Collections.Generic;
using System.Globalization;

namespace RealPolitic.Core;

public sealed class ChronicleSnapshot
{
    public int KingdomId, Year, Population, Cities, RulerId, Legitimacy, Routes;
    public string RulerName = string.Empty;
    public IdeologyType Ideology;
    public bool Starving, AtWar;
}

/// <summary>Bounded, per-session reading aids. Does not alter political simulation.</summary>
public sealed class ChronicleNotebook
{
    private readonly Dictionary<int, ChronicleSnapshot> _previous = new Dictionary<int, ChronicleSnapshot>();
    private readonly Dictionary<int, string> _condition = new Dictionary<int, string>();
    private readonly Dictionary<int, int> _conditionYear = new Dictionary<int, int>();
    public int Count => _previous.Count;
    public static string T(string key, string fallback, params object[] args)
        => string.Format(CultureInfo.InvariantCulture, CoreText.L(key, fallback), args);

    public string Compare(ChronicleSnapshot now)
    {
        _previous.TryGetValue(now.KingdomId, out ChronicleSnapshot old);
        _previous[now.KingdomId] = now;
        if (old == null || now.Year <= old.Year) return string.Empty;
        var changes = new List<string>();
        if (old.Population != now.Population)
            changes.Add(T("RP_CHRON25_DELTA_POP", "жители {0} → {1} ({2})", old.Population, now.Population, Signed(now.Population - old.Population)));
        if (old.Cities != now.Cities)
            changes.Add(T("RP_CHRON25_DELTA_CITIES", "города {0} → {1}", old.Cities, now.Cities));
        if (old.RulerId != now.RulerId)
            changes.Add(T("RP_CHRON25_DELTA_RULER", "глава: {0} → {1}", Person(old.RulerName, old.RulerId), Person(now.RulerName, now.RulerId)));
        if (old.Ideology != now.Ideology)
            changes.Add(T("RP_CHRON25_DELTA_IDEOLOGY", "строй: {0} → {1}", CoreText.L(Ideologies.NameKey(old.Ideology), old.Ideology.ToString()), CoreText.L(Ideologies.NameKey(now.Ideology), now.Ideology.ToString())));
        if (Math.Abs(old.Legitimacy - now.Legitimacy) >= 5)
            changes.Add(T("RP_CHRON25_DELTA_LEGIT", "легитимность {0} → {1}", old.Legitimacy, now.Legitimacy));
        if (old.Routes != now.Routes)
            changes.Add(T("RP_CHRON25_DELTA_ROUTES", "торговые пути {0} → {1}", old.Routes, now.Routes));
        if (old.Starving != now.Starving)
            changes.Add(now.Starving ? CoreText.L("RP_CHRON25_HUNGER_ON", "зафиксирован голод") : CoreText.L("RP_CHRON25_HUNGER_OFF", "голод больше не отмечен в снимке"));
        if (old.AtWar != now.AtWar)
            changes.Add(now.AtWar ? CoreText.L("RP_CHRON25_WAR_ON", "держава теперь воюет") : CoreText.L("RP_CHRON25_WAR_OFF", "активная война больше не отмечена"));
        return changes.Count == 0 ? string.Empty : T("RP_CHRON25_SINCE", "С прошлого обзора (год {0}): {1}.", old.Year, string.Join("; ", changes));
    }

    public string Conditions(ChronicleSnapshot now, KingdomPolitics state, SimParams settings)
    {
        var parts = new List<string>();
        if (now.Population <= 5)
            parts.Add(T("RP_CHRON25_SMALL", "Малая община ({0} жит., формальные институты).", now.Population));
        if (now.Legitimacy < 25)
            parts.Add(CoreText.L("RP_CHRON25_LOW_LEGIT", "Легитимность критически низкая: доверие к власти ослаблено."));
        if (state?.Economy != null && settings.EnableEconomy)
        {
            EconomyState eco = state.Economy;
            parts.Add(T("RP_CHRON25_ECONOMY", "Экономика: благосостояние {0}%, баланс {1}/{2}, дефицит (лет): {3}.", (int)eco.Prosperity, (int)eco.LastIncome, (int)eco.LastExpenses, eco.DeficitYears));
            if (eco.InCrisis)
                parts.Add(T("RP_CHRON25_CRISIS", "Действует {0} (до {1} года).", CoreText.L(EconomicCrises.LocKey(eco.Crisis), eco.Crisis.ToString()), eco.CrisisUntilYear));
            if (settings.EnableTrade && now.Routes == 0)
                parts.Add(T("RP_CHRON25_NO_TRADE", "Торговых путей нет (нужен мир с соседом от {0} гор.).", settings.TradeMinCities));
            if (now.Starving)
                parts.Add(CoreText.L("RP_CHRON25_HUNGER_NOTE", "В стране отмечен голод среди жителей."));
        }
        if (state?.Election != null && (state.Constitution == null || ConstitutionGates.HoldsElections(state.Constitution.StateForm)) && (SuccessionLaws.HasPeriodicCycle(state.Ideology) || SuccessionLaws.HasPeriodicCycle(state.SuccessionLaw)))
            parts.Add(T("RP_CHRON25_ELECTION_DUE", "Избирательный цикл: сроков {0}/{1}, следующие выборы — год {2}.", state.Election.TermsServed, ElectionSystem.MaxLifetimeTerms, state.Election.NextElectionYear));
        // Detailed conditions are shown initially, at meaningful condition changes, or once per decade.
        string signature = (now.Population <= 5) + ":" + (now.Legitimacy < 25) + ":" + now.Starving + ":" + (now.Routes == 0) + ":" + (state?.Economy?.Crisis.ToString() ?? "") + ":" + (state?.Election?.NextElectionYear ?? 0);
        if (_condition.TryGetValue(now.KingdomId, out string previous) && previous == signature
            && _conditionYear.TryGetValue(now.KingdomId, out int year) && now.Year >= year && now.Year - year < 10) return string.Empty;
        _condition[now.KingdomId] = signature;
        _conditionYear[now.KingdomId] = now.Year;
        return string.Join(" ", parts);
    }

    public static string Person(string name, int id)
    {
        if (id == 0) return CoreText.L("RP_CHRON25_VACANT", "главы нет");
        if (string.IsNullOrWhiteSpace(name)) name = CoreText.L("RP_CHRON25_UNNAMED", "имя не установлено");
        return T("RP_CHRON25_PERSON", "{0} [персонаж №{1}]", name, id);
    }
    private static string Signed(int value) => value > 0 ? "+" + value : value.ToString(CultureInfo.InvariantCulture);
    public string Farewell(int kingdomId)
    {
        if (!_previous.TryGetValue(kingdomId, out ChronicleSnapshot previous)) return string.Empty;
        _previous.Remove(kingdomId); _condition.Remove(kingdomId); _conditionYear.Remove(kingdomId);
        return T("RP_CHRON25_FAREWELL", "По данным прошлого обзора (год {0}): жителей {1}, городов {2}, глава: {3}.", previous.Year, previous.Population, previous.Cities, Person(previous.RulerName, previous.RulerId));
    }
    public void Retain(ISet<int> live)
    {
        var remove = new List<int>();
        foreach (int id in _previous.Keys) if (!live.Contains(id)) remove.Add(id);
        foreach (int id in remove) { _previous.Remove(id); _condition.Remove(id); _conditionYear.Remove(id); }
    }
    public void Clear() { _previous.Clear(); _condition.Clear(); _conditionYear.Clear(); }
}

/// <summary>Compress only routine extinction notices. Urgent political events are never delayed.</summary>
public sealed class ChronicleExtinctionDigest
{
    private readonly Dictionary<string, List<string>> _names = new Dictionary<string, List<string>>();
    private readonly Dictionary<string, int> _counts = new Dictionary<string, int>();
    public int Year { get; private set; } = -1;
    public bool CanAdd(int year) => Year < 0 || year == Year;
    public bool Add(int year, string race, string species)
    {
        race = string.IsNullOrWhiteSpace(race) ? CoreText.L("RP_CHRON25_UNKNOWN_SPECIES", "вид не установлен") : race;
        if (!CanAdd(year) || _names.Count >= 128 && !_names.ContainsKey(race)) return false;
        Year = year;
        if (!_names.TryGetValue(race, out List<string> names)) { names = new List<string>(); _names[race] = names; _counts[race] = 0; }
        _counts[race]++;
        if (!string.IsNullOrWhiteSpace(species) && names.Count < 4 && !names.Contains(species)) names.Add(species);
        return true;
    }
    public List<string> Drain()
    {
        var lines = new List<string>();
        foreach (var pair in _names)
        {
            int count = _counts[pair.Key];
            string examples = pair.Value.Count == 0 ? "" : string.Join(", ", pair.Value);
            if (count > pair.Value.Count) examples += ChronicleNotebook.T("RP_CHRON25_AND_MORE", "; ещё {0}", count - pair.Value.Count);
            lines.Add(ChronicleNotebook.T("RP_CHRON25_EXTINCT", "Вымирания за год: {0}; подвидов — {1}. {2}. Причина вымирания: не установленная причина гибели.", pair.Key, count, examples));
        }
        _names.Clear(); _counts.Clear(); Year = -1;
        return lines;
    }
    public void Clear() { _names.Clear(); _counts.Clear(); Year = -1; }
}

/// <summary>Groups recorded reports, not unique people or all crossings in the game.</summary>
public sealed class ChronicleBorderDigest
{
    private sealed class Group {public string Host="",Guest="";public int Reports;public readonly List<string> Examples=new List<string>();}
    private readonly Dictionary<string,Group> _groups=new Dictionary<string,Group>();
    public int Year {get;private set;}=-1;
    public bool CanAdd(int year)=>Year<0||Year==year;
    public bool Add(PolEvent evt)
    {
        if(!CanAdd(evt.Year))return false;
        string key=(evt.KingdomId?.ToString()??evt.Get("kingdom"))+"\u001f"+evt.Get("partner_id")+"\u001f"+evt.Get("partner");
        if(!_groups.TryGetValue(key,out var group))
        {
            if(_groups.Count>=128)return false;
            _groups[key]=group=new Group{Host=evt.Get("kingdom"),Guest=evt.Get("partner")};
        }
        Year=evt.Year;group.Reports++;
        string name=evt.Get("intruder");if(group.Examples.Count<3&&!string.IsNullOrEmpty(name)&&!group.Examples.Contains(name))group.Examples.Add(name);
        return true;
    }
    public List<string> Drain()
    {
        var result=new List<string>();
        foreach(var g in _groups.Values)
            result.Add(ChronicleNotebook.T("RP_CHRON251_BORDER_DIGEST","{0}: записанные пограничные эпизоды с жителями державы {1} — {2}. Примеры: {3}. Это число сообщений, не уникальных людей; прибытие домой не подтверждается.",g.Host,g.Guest,g.Reports,string.Join(", ",g.Examples)));
        _groups.Clear();Year=-1;return result;
    }
}
