using System;
using System.Collections.Generic;

namespace RealPolitic.Core;

/// <summary>
/// Этап 26.2: поправки к конституции (§3 дизайна).
///
/// Две руки правят грамоту:
/// • правитель — раз в <see cref="SimParams.RulerAmendmentYears"/> лет, при легитимности выше
///   <see cref="SimParams.RulerAmendmentLegitimacy"/>, ОДНУ статью, направление — из характера
///   (жестокий/завоеватель закручивают гайки, справедливый/дипломат ослабляют). Ужесточение
///   при обязательной (Binding) конституции стоит −4 легитимности: народ встречает холодом;
/// • народ — при Binding и AvgDissent > 0.35 «конституционный кризис»: собрание/вече само
///   смягчает одну статью (без цены для правителя, но с записью в летопись).
///
/// Статья «О верховной власти» (форма правления) поправками не меняется — это переворот,
/// а не поправка; её меняют революция, мятеж и смена строя.
/// </summary>
public static class ConstitutionAmendments
{
	public const string ArticleFranchise = "RP_CONST_ART_FRANCHISE";
	public const string ArticleBorders = "RP_CONST_ART_BORDERS";
	public const string ArticleSuccession = "RP_CONST_ART_SUCCESSION";
	public const string ArticleSupremacy = "RP_CONST_ART_SUPREMACY";
	// 0.24: статьи VII–XV.
	public const string ArticleTax = "RP_CONST_ART_TAX";
	public const string ArticleTrade = "RP_CONST_ART_TRADE";
	public const string ArticleFaith = "RP_CONST_ART_FAITH";
	public const string ArticleLevy = "RP_CONST_ART_LEVY";
	public const string ArticleLand = "RP_CONST_ART_LAND";
	public const string ArticleWatch = "RP_CONST_ART_WATCH";
	public const string ArticleBlood = "RP_CONST_ART_BLOOD";
	public const string ArticleImpeachment = "RP_CONST_ART_IMPEACH";
	public const string ArticleUnion = "RP_CONST_ART_UNION";
	// 0.53: четыре статьи о палатах. Владелец 30.10.2026: сколько палат, от чего верхняя,
	// есть ли вето правителя и можно ли палаты распустить — всё это решает конституция.
	public const string ArticleChambers = "RP_CONST_ART_CHAMBERS";
	public const string ArticleUpperBasis = "RP_CONST_ART_UPPERBASIS";
	public const string ArticleVeto = "RP_CONST_ART_VETO";
	public const string ArticleDissolve = "RP_CONST_ART_DISSOLVE";

	public const string EditorPeople = "RP_CONST_EDITOR_PEOPLE";

	/// <summary>Одна возможная поправка: что и на что.</summary>
	public struct Proposal
	{
		public string Article;
		public string From;
		public string To;
		public bool Tightens;
		public Action<Constitution> Apply;
	}

	/// <summary>Годовой тик поправок. Возвращает true, если статья изменилась.</summary>
	public static bool Tick(KingdomPolitics state, string kingdomName, int year, SimParams p, Random rng, PolEventBus bus)
	{
		return Tick(state, kingdomName, year, p, rng, bus, chambersInCharge: false);
	}

	/// <summary>
	/// 0.53: если в державе есть палаты, конституцию правит парламент, а не правитель сам.
	/// Владелец 30.10.2026: «парламент реально принимает законы, у правителя вето».
	/// chambersInCharge — у державы есть города, значит законы идут через палаты (TickChambers).
	/// </summary>
	public static bool Tick(KingdomPolitics state, string kingdomName, int year, SimParams p, Random rng, PolEventBus bus, bool chambersInCharge)
	{
		if (state?.Constitution == null || p == null || !p.EnableConstitution)
		{
			return false;
		}
		if (chambersInCharge)
		{
			return false; // закон вносит палата — см. PoliticalWorld.TickChambers
		}
		if (TryPeoplesAmendment(state, kingdomName, year, p, rng, bus))
		{
			return true;
		}
		return TryRulersAmendment(state, kingdomName, year, p, rng, bus);
	}

	// ---------------------------------------------------------------- правитель

	public static bool RulerMayAmend(KingdomPolitics state, int year, SimParams p)
	{
		// 0.23.13: регентство завершается End() (IsActive=false), но объект RegencyState остаётся.
		// Блокировать поправки надо только ПОКА регентство активно — иначе правители, пережившие
		// хоть одно регентство, навсегда теряли право править конституцию.
		if (state?.Constitution == null || state.Ruler == null || (state.Regency != null && state.Regency.IsActive))
		{
			return false;
		}
		if (state.Legitimacy < p.RulerAmendmentLegitimacy)
		{
			return false;
		}
		// Правитель должен посидеть на троне: первые годы — не до законов.
		if (year - state.Ruler.SinceYear < 3)
		{
			return false;
		}
		return year - state.LastAmendmentYear >= p.RulerAmendmentYears;
	}

	/// <summary>Куда тянет правителя: +1 — закрутить гайки, −1 — ослабить, 0 — не трогать.</summary>
	public static int RulerDirection(KingdomPolitics state)
	{
		RulerTrait trait = state.Personality?.Trait ?? RulerTrait.None;
		float ambition = state.Personality?.Ambition ?? 50f;
		int dir = 0;
		switch (trait)
		{
			case RulerTrait.Cruel:
			case RulerTrait.Conqueror:
				dir = 1;
				break;
			case RulerTrait.Just:
			case RulerTrait.Diplomat:
				dir = -1;
				break;
			case RulerTrait.Pious:
				dir = 1;
				break;
			case RulerTrait.Scholar:
				dir = -1;
				break;
		}
		if (dir == 0)
		{
			if (ambition >= 70f) dir = 1;
			else if (ambition <= 30f) dir = -1;
		}
		// Слабая власть закручивает гайки, чтобы удержаться; уверенная — может себе позволить мягкость.
		if (dir == 0 && state.Legitimacy < 60f && state.AvgDissent > 0.3f)
		{
			dir = 1;
		}
		return dir;
	}

	private static bool TryRulersAmendment(KingdomPolitics state, string kingdomName, int year, SimParams p, Random rng, PolEventBus bus)
	{
		if (!RulerMayAmend(state, year, p))
		{
			return false;
		}
		int dir = RulerDirection(state);
		if (dir == 0)
		{
			return false;
		}
		List<Proposal> options = Proposals(state, dir > 0, forPeople: false);
		if (options.Count == 0)
		{
			return false;
		}
		// Раз в срок — не обязательно каждый раз: ~60% что правитель вообще берётся за перо.
		if (rng != null && rng.NextDouble() > 0.6)
		{
			state.LastAmendmentYear = year; // подумал — и отложил до следующего срока
			return false;
		}
		Proposal chosen = options[rng == null ? 0 : rng.Next(options.Count)];
		bool binding = state.Constitution.Supremacy == SupremacyRule.Binding;
		Enact(state, chosen, year, state.Ruler.ActorId, state.Ruler.Name ?? string.Empty);
		float legitimacyDelta = 0f;
		if (chosen.Tightens && binding)
		{
			legitimacyDelta = -4f;
		}
		else if (!chosen.Tightens)
		{
			legitimacyDelta = 2f;
		}
		if (legitimacyDelta != 0f)
		{
			state.Legitimacy = KingdomSimulation.Clamp(state.Legitimacy + legitimacyDelta, 0f, 100f);
		}
		Publish(bus, state, kingdomName, year, chosen, state.Ruler.Name ?? string.Empty, "RP_EV_CONST_AMENDED_RULER", legitimacyDelta);
		return true;
	}

	// ---------------------------------------------------------------- народ

	private static bool TryPeoplesAmendment(KingdomPolitics state, string kingdomName, int year, SimParams p, Random rng, PolEventBus bus)
	{
		if (state.IsYoungState(year, p?.YoungStateGraceYears ?? 50))
		{
			return false; // первые 50 лет существования государства народ не давит на власть
		}
		Constitution c = state.Constitution;
		if (c.Supremacy != SupremacyRule.Binding || c.WarPower == WarAuthority.ByRuler)
		{
			return false; // народ правит грамоту только там, где у него есть собрание и грамота обязательна
		}
		if (state.AvgDissent <= 0.35f || year - state.LastAmendmentYear < p.RulerAmendmentYears / 2)
		{
			return false;
		}
		List<Proposal> options = Proposals(state, tighten: false, forPeople: true);
		if (options.Count == 0)
		{
			return false;
		}
		// Кризис не каждый год: чем сильнее недовольство, тем вероятнее собрание соберётся.
		float chance = Math.Min(0.6f, (state.AvgDissent - 0.35f) * 2f + 0.15f);
		if (rng != null && rng.NextDouble() > chance)
		{
			return false;
		}
		Proposal chosen = options[rng == null ? 0 : rng.Next(options.Count)];
		Enact(state, chosen, year, 0, EditorPeople);
		// Уступка снимает часть напряжения.
		state.AvgDissent = KingdomSimulation.Clamp(state.AvgDissent - 0.08f, 0f, 1f);
		Publish(bus, state, kingdomName, year, chosen, EditorPeople, "RP_EV_CONST_AMENDED_PEOPLE", 0f);
		return true;
	}

	// ---------------------------------------------------------------- варианты

	/// <summary>Список статей, которые можно повернуть в заданную сторону.</summary>
	public static List<Proposal> Proposals(KingdomPolitics state, bool tighten, bool forPeople)
	{
		Constitution c = state.Constitution;
		var list = new List<Proposal>();
		if (tighten)
		{
			if (c.Franchise == Franchise.Universal)
				list.Add(Make(ArticleFranchise, FranchiseKey(Franchise.Universal), FranchiseKey(Franchise.Notables), true, x => x.Franchise = Franchise.Notables));
			else if (c.Franchise == Franchise.Notables)
				list.Add(Make(ArticleFranchise, FranchiseKey(Franchise.Notables), FranchiseKey(Franchise.Clan), true, x => x.Franchise = Franchise.Clan));
			if (c.Borders == BorderLaw.Open)
				list.Add(Make(ArticleBorders, BorderKey(BorderLaw.Open), BorderKey(BorderLaw.Controlled), true, x => x.Borders = BorderLaw.Controlled));
			else if (c.Borders == BorderLaw.Controlled)
				list.Add(Make(ArticleBorders, BorderKey(BorderLaw.Controlled), BorderKey(BorderLaw.Closed), true, x => x.Borders = BorderLaw.Closed));
			if (c.Succession == SuccessionRule.AnyAdult)
				list.Add(Make(ArticleSuccession, "RP_CONST_SUCC_ANYADULT", "RP_CONST_SUCC_STRICT", true, x => x.Succession = SuccessionRule.Strict));
			// 0.24, статьи VII–XV: закручивание гаек — шаг к более суровому значению.
			if (c.Taxes == TaxLaw.Light)
				list.Add(Make(ArticleTax, TaxKey(TaxLaw.Light), TaxKey(TaxLaw.Moderate), true, x => x.Taxes = TaxLaw.Moderate));
			else if (c.Taxes == TaxLaw.Moderate)
				list.Add(Make(ArticleTax, TaxKey(TaxLaw.Moderate), TaxKey(TaxLaw.Crushing), true, x => x.Taxes = TaxLaw.Crushing));
			if (c.Trade == TradeLaw.FreeTrade)
				list.Add(Make(ArticleTrade, TradeKey(TradeLaw.FreeTrade), TradeKey(TradeLaw.Moderate), true, x => x.Trade = TradeLaw.Moderate));
			else if (c.Trade == TradeLaw.Moderate)
				list.Add(Make(ArticleTrade, TradeKey(TradeLaw.Moderate), TradeKey(TradeLaw.Protectionism), true, x => x.Trade = TradeLaw.Protectionism));
			if (c.Faith == FaithLaw.Tolerance)
				list.Add(Make(ArticleFaith, FaithKey(FaithLaw.Tolerance), FaithKey(FaithLaw.StateFaith), true, x => x.Faith = FaithLaw.StateFaith));
			else if (c.Faith == FaithLaw.StateFaith)
				list.Add(Make(ArticleFaith, FaithKey(FaithLaw.StateFaith), FaithKey(FaithLaw.Purity), true, x => x.Faith = FaithLaw.Purity));
			if (c.Levy == LevyLaw.Militia)
				list.Add(Make(ArticleLevy, LevyKey(LevyLaw.Militia), LevyKey(LevyLaw.Conscription), true, x => x.Levy = LevyLaw.Conscription));
			else if (c.Levy == LevyLaw.Conscription)
				list.Add(Make(ArticleLevy, LevyKey(LevyLaw.Conscription), LevyKey(LevyLaw.Guard), true, x => x.Levy = LevyLaw.Guard));
			if (c.Land == LandLaw.Restitution)
				list.Add(Make(ArticleLand, LandKey(LandLaw.Restitution), LandKey(LandLaw.Expansion), true, x => x.Land = LandLaw.Expansion));
			else if (c.Land == LandLaw.Expansion)
				list.Add(Make(ArticleLand, LandKey(LandLaw.Expansion), LandKey(LandLaw.FreeConquest), true, x => x.Land = LandLaw.FreeConquest));
			if (c.Watch == WatchLaw.Liberty)
				list.Add(Make(ArticleWatch, WatchKey(WatchLaw.Liberty), WatchKey(WatchLaw.Patrol), true, x => x.Watch = WatchLaw.Patrol));
			else if (c.Watch == WatchLaw.Patrol)
				list.Add(Make(ArticleWatch, WatchKey(WatchLaw.Patrol), WatchKey(WatchLaw.SecretPolice), true, x => x.Watch = WatchLaw.SecretPolice));
			if (c.Blood == BloodLaw.AnyRoyalBlood)
				list.Add(Make(ArticleBlood, BloodKey(BloodLaw.AnyRoyalBlood), BloodKey(BloodLaw.DirectBlood), true, x => x.Blood = BloodLaw.DirectBlood));
			else if (c.Blood == BloodLaw.DirectBlood)
				list.Add(Make(ArticleBlood, BloodKey(BloodLaw.DirectBlood), BloodKey(BloodLaw.Current), true, x => x.Blood = BloodLaw.Current));
			if (c.Impeachment == ImpeachmentLaw.PeopleVote)
				list.Add(Make(ArticleImpeachment, ImpeachmentKey(ImpeachmentLaw.PeopleVote), ImpeachmentKey(ImpeachmentLaw.CityLeaders), true, x => x.Impeachment = ImpeachmentLaw.CityLeaders));
			else if (c.Impeachment == ImpeachmentLaw.CityLeaders)
				list.Add(Make(ArticleImpeachment, ImpeachmentKey(ImpeachmentLaw.CityLeaders), ImpeachmentKey(ImpeachmentLaw.Forbidden), true, x => x.Impeachment = ImpeachmentLaw.Forbidden));
			if (c.Union == UnionLaw.All)
				list.Add(Make(ArticleUnion, UnionKey(UnionLaw.All), UnionKey(UnionLaw.Neighbors), true, x => x.Union = UnionLaw.Neighbors));
			else if (c.Union == UnionLaw.Neighbors)
				list.Add(Make(ArticleUnion, UnionKey(UnionLaw.Neighbors), UnionKey(UnionLaw.SameLanguage), true, x => x.Union = UnionLaw.SameLanguage));
			// 0.53: закручивание — власть забирают у собрания.
			if (c.Chambers == ChamberCount.Two)
				list.Add(Make(ArticleChambers, "RP53_CHAMBERS_TWO", "RP53_CHAMBERS_ONE", true, x => x.Chambers = ChamberCount.One));
			if (c.UpperBasis == UpperChamberBasis.Peoples)
				list.Add(Make(ArticleUpperBasis, "RP53_BASIS_PEOPLES", "RP53_BASIS_CITIES", true, x => x.UpperBasis = UpperChamberBasis.Cities));
			else if (c.UpperBasis == UpperChamberBasis.Cities)
				list.Add(Make(ArticleUpperBasis, "RP53_BASIS_CITIES", "RP53_BASIS_CLANS", true, x => x.UpperBasis = UpperChamberBasis.Clans));
			if (!c.RulerVeto)
				list.Add(Make(ArticleVeto, "RP53_OFF", "RP53_ON", true, x => x.RulerVeto = true));
			if (!c.RulerMayDissolve)
				list.Add(Make(ArticleDissolve, "RP53_OFF", "RP53_ON", true, x => x.RulerMayDissolve = true));
			// Правитель, стеснённый обязательной грамотой, может низвести её до обычая.
			if (c.Supremacy == SupremacyRule.Binding && !forPeople)
				list.Add(Make(ArticleSupremacy, "RP_CONST_SUP_BINDING", "RP_CONST_SUP_ADVISORY", true, x => x.Supremacy = SupremacyRule.Advisory));
		}
		else
		{
			if (c.Franchise == Franchise.Clan)
				list.Add(Make(ArticleFranchise, FranchiseKey(Franchise.Clan), FranchiseKey(Franchise.Notables), false, x => x.Franchise = Franchise.Notables));
			else if (c.Franchise == Franchise.Notables)
				list.Add(Make(ArticleFranchise, FranchiseKey(Franchise.Notables), FranchiseKey(Franchise.Universal), false, x => x.Franchise = Franchise.Universal));
			// 0.53: ослабление — власть отдают собранию.
			if (c.Chambers == ChamberCount.One)
				list.Add(Make(ArticleChambers, "RP53_CHAMBERS_ONE", "RP53_CHAMBERS_TWO", false, x => x.Chambers = ChamberCount.Two));
			if (c.UpperBasis == UpperChamberBasis.Clans)
				list.Add(Make(ArticleUpperBasis, "RP53_BASIS_CLANS", "RP53_BASIS_CITIES", false, x => x.UpperBasis = UpperChamberBasis.Cities));
			else if (c.UpperBasis == UpperChamberBasis.Cities)
				list.Add(Make(ArticleUpperBasis, "RP53_BASIS_CITIES", "RP53_BASIS_PEOPLES", false, x => x.UpperBasis = UpperChamberBasis.Peoples));
			if (c.RulerVeto)
				list.Add(Make(ArticleVeto, "RP53_ON", "RP53_OFF", false, x => x.RulerVeto = false));
			if (c.RulerMayDissolve)
				list.Add(Make(ArticleDissolve, "RP53_ON", "RP53_OFF", false, x => x.RulerMayDissolve = false));
			if (c.Borders == BorderLaw.Closed)
				list.Add(Make(ArticleBorders, BorderKey(BorderLaw.Closed), BorderKey(BorderLaw.Controlled), false, x => x.Borders = BorderLaw.Controlled));
			else if (c.Borders == BorderLaw.Controlled)
				list.Add(Make(ArticleBorders, BorderKey(BorderLaw.Controlled), BorderKey(BorderLaw.Open), false, x => x.Borders = BorderLaw.Open));
			// Обычай → обязательная грамота: только по воле правителя-реформатора (народ и так при Binding).
			if (c.Supremacy == SupremacyRule.Advisory && !forPeople)
				list.Add(Make(ArticleSupremacy, "RP_CONST_SUP_ADVISORY", "RP_CONST_SUP_BINDING", false, x => x.Supremacy = SupremacyRule.Binding));
			// 0.24, статьи VII–XV: смягчение — шаг к более мягкому значению.
			if (c.Taxes == TaxLaw.Crushing)
				list.Add(Make(ArticleTax, TaxKey(TaxLaw.Crushing), TaxKey(TaxLaw.Moderate), false, x => x.Taxes = TaxLaw.Moderate));
			else if (c.Taxes == TaxLaw.Moderate)
				list.Add(Make(ArticleTax, TaxKey(TaxLaw.Moderate), TaxKey(TaxLaw.Light), false, x => x.Taxes = TaxLaw.Light));
			if (c.Trade == TradeLaw.Protectionism)
				list.Add(Make(ArticleTrade, TradeKey(TradeLaw.Protectionism), TradeKey(TradeLaw.Moderate), false, x => x.Trade = TradeLaw.Moderate));
			else if (c.Trade == TradeLaw.Moderate)
				list.Add(Make(ArticleTrade, TradeKey(TradeLaw.Moderate), TradeKey(TradeLaw.FreeTrade), false, x => x.Trade = TradeLaw.FreeTrade));
			if (c.Faith == FaithLaw.Purity)
				list.Add(Make(ArticleFaith, FaithKey(FaithLaw.Purity), FaithKey(FaithLaw.StateFaith), false, x => x.Faith = FaithLaw.StateFaith));
			else if (c.Faith == FaithLaw.StateFaith)
				list.Add(Make(ArticleFaith, FaithKey(FaithLaw.StateFaith), FaithKey(FaithLaw.Tolerance), false, x => x.Faith = FaithLaw.Tolerance));
			if (c.Levy == LevyLaw.Guard)
				list.Add(Make(ArticleLevy, LevyKey(LevyLaw.Guard), LevyKey(LevyLaw.Conscription), false, x => x.Levy = LevyLaw.Conscription));
			else if (c.Levy == LevyLaw.Conscription)
				list.Add(Make(ArticleLevy, LevyKey(LevyLaw.Conscription), LevyKey(LevyLaw.Militia), false, x => x.Levy = LevyLaw.Militia));
			if (c.Land == LandLaw.FreeConquest)
				list.Add(Make(ArticleLand, LandKey(LandLaw.FreeConquest), LandKey(LandLaw.Expansion), false, x => x.Land = LandLaw.Expansion));
			else if (c.Land == LandLaw.Expansion)
				list.Add(Make(ArticleLand, LandKey(LandLaw.Expansion), LandKey(LandLaw.Restitution), false, x => x.Land = LandLaw.Restitution));
			if (c.Watch == WatchLaw.SecretPolice)
				list.Add(Make(ArticleWatch, WatchKey(WatchLaw.SecretPolice), WatchKey(WatchLaw.Patrol), false, x => x.Watch = WatchLaw.Patrol));
			else if (c.Watch == WatchLaw.Patrol)
				list.Add(Make(ArticleWatch, WatchKey(WatchLaw.Patrol), WatchKey(WatchLaw.Liberty), false, x => x.Watch = WatchLaw.Liberty));
			if (c.Blood == BloodLaw.Current)
				list.Add(Make(ArticleBlood, BloodKey(BloodLaw.Current), BloodKey(BloodLaw.DirectBlood), false, x => x.Blood = BloodLaw.DirectBlood));
			else if (c.Blood == BloodLaw.DirectBlood)
				list.Add(Make(ArticleBlood, BloodKey(BloodLaw.DirectBlood), BloodKey(BloodLaw.AnyRoyalBlood), false, x => x.Blood = BloodLaw.AnyRoyalBlood));
			if (c.Impeachment == ImpeachmentLaw.Forbidden)
				list.Add(Make(ArticleImpeachment, ImpeachmentKey(ImpeachmentLaw.Forbidden), ImpeachmentKey(ImpeachmentLaw.CityLeaders), false, x => x.Impeachment = ImpeachmentLaw.CityLeaders));
			else if (c.Impeachment == ImpeachmentLaw.CityLeaders)
				list.Add(Make(ArticleImpeachment, ImpeachmentKey(ImpeachmentLaw.CityLeaders), ImpeachmentKey(ImpeachmentLaw.PeopleVote), false, x => x.Impeachment = ImpeachmentLaw.PeopleVote));
			if (c.Union == UnionLaw.SameLanguage)
				list.Add(Make(ArticleUnion, UnionKey(UnionLaw.SameLanguage), UnionKey(UnionLaw.Neighbors), false, x => x.Union = UnionLaw.Neighbors));
			else if (c.Union == UnionLaw.Neighbors)
				list.Add(Make(ArticleUnion, UnionKey(UnionLaw.Neighbors), UnionKey(UnionLaw.All), false, x => x.Union = UnionLaw.All));
		}
        if(c.Electoral!=null)
        {
            list.RemoveAll(p=>p.Article==ArticleFranchise);
            list.AddRange(ElectoralAmendmentMenu.Build(c.Electoral,tighten));
        }
		return list;
	}

	private static Proposal Make(string article, string from, string to, bool tightens, Action<Constitution> apply)
	{
		return new Proposal { Article = article, From = from, To = to, Tightens = tightens, Apply = apply };
	}

	private static string FranchiseKey(Franchise f)
	{
		switch (f)
		{
			case Franchise.Universal: return "RP_CONST_FR_UNIVERSAL";
			case Franchise.Notables: return "RP_CONST_FR_NOTABLES";
			default: return "RP_CONST_FR_CLAN";
		}
	}

	private static string BorderKey(BorderLaw b)
	{
		switch (b)
		{
			case BorderLaw.Open: return "RP_CONST_BORDER_OPEN";
			case BorderLaw.Controlled: return "RP_CONST_BORDER_CONTROLLED";
			default: return "RP_CONST_BORDER_CLOSED";
		}
	}

	// ---------------------------------------------------------------- ключи значений VII–XV (0.24)

	private static string TaxKey(TaxLaw v)
	{
		switch (v)
		{
			case TaxLaw.Light: return "RP_CONST_TAX_LIGHT";
			case TaxLaw.Crushing: return "RP_CONST_TAX_CRUSHING";
			default: return "RP_CONST_TAX_MODERATE";
		}
	}

	private static string TradeKey(TradeLaw v)
	{
		switch (v)
		{
			case TradeLaw.FreeTrade: return "RP_CONST_TRADE_FREE";
			case TradeLaw.Protectionism: return "RP_CONST_TRADE_PROTECTION";
			default: return "RP_CONST_TRADE_MODERATE";
		}
	}

	private static string FaithKey(FaithLaw v)
	{
		switch (v)
		{
			case FaithLaw.Tolerance: return "RP_CONST_FAITH_TOLERANCE";
			case FaithLaw.Purity: return "RP_CONST_FAITH_PURITY";
			default: return "RP_CONST_FAITH_STATE";
		}
	}

	private static string LevyKey(LevyLaw v)
	{
		switch (v)
		{
			case LevyLaw.Militia: return "RP_CONST_LEVY_MILITIA";
			case LevyLaw.Guard: return "RP_CONST_LEVY_GUARD";
			default: return "RP_CONST_LEVY_CONSCRIPT";
		}
	}

	private static string LandKey(LandLaw v)
	{
		switch (v)
		{
			case LandLaw.Restitution: return "RP_CONST_LAND_RESTITUTION";
			case LandLaw.Expansion: return "RP_CONST_LAND_EXPANSION";
			default: return "RP_CONST_LAND_FREE";
		}
	}

	private static string WatchKey(WatchLaw v)
	{
		switch (v)
		{
			case WatchLaw.Liberty: return "RP_CONST_WATCH_LIBERTY";
			case WatchLaw.Patrol: return "RP_CONST_WATCH_PATROL";
			default: return "RP_CONST_WATCH_SECRET";
		}
	}

	private static string BloodKey(BloodLaw v)
	{
		switch (v)
		{
			case BloodLaw.Current: return "RP_CONST_BLOOD_CURRENT";
			case BloodLaw.DirectBlood: return "RP_CONST_BLOOD_DIRECT";
			default: return "RP_CONST_BLOOD_ANY";
		}
	}

	private static string ImpeachmentKey(ImpeachmentLaw v)
	{
		switch (v)
		{
			case ImpeachmentLaw.CityLeaders: return "RP_CONST_IMPEACH_CITY";
			case ImpeachmentLaw.PeopleVote: return "RP_CONST_IMPEACH_PEOPLE";
			default: return "RP_CONST_IMPEACH_FORBIDDEN";
		}
	}

	private static string UnionKey(UnionLaw v)
	{
		switch (v)
		{
			case UnionLaw.All: return "RP_CONST_UNION_ALL";
			case UnionLaw.Neighbors: return "RP_CONST_UNION_NEIGHBORS";
			default: return "RP_CONST_UNION_LANG";
		}
	}

	/// <summary>
	/// 0.53: применить статью, которую приняли палаты. Публичная обёртка над Enact:
	/// палаты живут в отдельном файле и не имеют доступа к приватному методу.
	/// </summary>
	public static void EnactByChambers(KingdomPolitics state, Proposal proposal, int year, string editorName)
	{
		if (state?.Constitution == null)
		{
			return;
		}
		Enact(state, proposal, year, 0, editorName ?? string.Empty);
	}

	/// <summary>Найти предложение по статье и направлению (для применения принятого закона).</summary>
	public static bool TryFindProposal(KingdomPolitics state, string article, bool tightens, bool fromPeople, out Proposal found)
	{
		found = default;
		if (state?.Constitution == null || string.IsNullOrEmpty(article))
		{
			return false;
		}
		List<Proposal> list = Proposals(state, tightens, fromPeople);
		for (int i = 0; i < list.Count; i++)
		{
			if (list[i].Article == article)
			{
				found = list[i];
				return true;
			}
		}
		return false;
	}

	private static void Enact(KingdomPolitics state, Proposal proposal, int year, int editorId, string editorName)
	{
		proposal.Apply(state.Constitution);
		state.ConstitutionHistory.Add(new ConstitutionEdit
		{
			Year = year,
			Article = proposal.Article,
			From = proposal.From,
			To = proposal.To,
			EditorActorId = editorId,
			EditorName = editorName
		});
		state.LastAmendmentYear = year;
	}

	private static void Publish(PolEventBus bus, KingdomPolitics state, string kingdomName, int year, Proposal proposal, string editor, string locKey, float legitimacyDelta)
	{
		bus?.Publish(new PolEvent(PolEventType.ConstitutionEdited, locKey)
		{
			Year = year,
			Severity = Severity.Major,
			KingdomId = state.KingdomId
		}.With("kingdom", kingdomName ?? string.Empty)
			.With("article", proposal.Article)
			.With("from", proposal.From)
			.With("to", proposal.To)
			.With("editor", editor)
			.With("legitimacy", ((int)legitimacyDelta).ToString()));
	}
}
