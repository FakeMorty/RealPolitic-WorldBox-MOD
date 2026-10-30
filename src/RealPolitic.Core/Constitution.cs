using System;
using System.Collections.Generic;
using System.Text;

namespace RealPolitic.Core;

/// <summary>
/// Этап 26: Конституция. Писаный свод законов государства, который
/// действия мода реально соблюдают, а правители и народ правят по своим процедурам.
/// Форма правления — подвид внутри идеологии (монархия абсолютная/выборная/парламентская),
/// война — наследие формы, электорат — настоящие юниты игры.
/// </summary>
public enum HeadOfState
{
	/// <summary>Наследный трон (вождь старшин, деспот, стратократ — все по роду/силе).</summary>
	Hereditary = 0,

	/// <summary>Выборная голова: президент сроком, танистр, кандидатуры выборщиков.</summary>
	Elected = 1,

	/// <summary>Парламентская монархия: престол наследный, но война и законы — за палатой.</summary>
	ParliamentaryMonarchy = 2,

	/// <summary>Парламентская демократия: президент выборный, война — за сенатомчи.</summary>
	Parliamentary = 3,

	/// <summary>Собрание/вече (община): голова — слуга собрания.</summary>
	Assembly = 4
}

public enum Franchise
{
	/// <summary>Голосуют все взрослые граждане державы.</summary>
	Universal = 0,

	/// <summary>Голосуют заметные: лидеры городов и родня правящей династии.</summary>
	Notables = 1,

	/// <summary>Голосует только правящий клан (племянной и родовой строй).</summary>
	Clan = 2
}

public enum WarAuthority
{
	/// <summary>Правитель решает единолично (сильная центральная власть).</summary>
	ByRuler = 0,

	/// <summary>Палата / сенат / совет выносит решение голосованием.</summary>
	ByAssembly = 1,

	/// <summary>Народ на вечи голосует лично.</summary>
	ByPeople = 2
}

public enum BorderLaw
{
	/// <summary>Границы принципиально открыты (обитатели проходят без лишних вопросов).</summary>
	Open = 0,

	/// <summary>Регулируемые границы: ИИ-политика решает вопрос облава (как сейчас).</summary>
	Controlled = 1,

	/// <summary>Границы всегда закрыты, облава — почётное право любой грядки страны.</summary>
	Closed = 2
}

public enum SuccessionRule
{
	/// <summary>Строгое наследование: права на трон — только по линии закона (узурп защитунает).</summary>
	Strict = 0,

	/// <summary>Любой взрослый из города может стать правителем (кого ветром нанесло).</summary>
	AnyAdult = 1
}

public enum SupremacyRule
{
	/// <summary>Конституция выше правителя: законы действуют и точка.</summary>
	Binding = 0,

	/// <summary>Конституция — обычай: хрупкое соглашение без принуждения.</summary>
	Advisory = 1
}

// ============ 0.24: расширенные статьи конституции (VII–XV) ============

/// <summary>Статья VII «О казне и податях»: тяжесть налогового бремени.</summary>
public enum TaxLaw
{
	/// <summary>Подати лёгкие: казна тощает, подданные довольны.</summary>
	Light = 0,

	/// <summary>Подати умеренные.</summary>
	Moderate = 1,

	/// <summary>Подати кабальные: казна полна, подданные стонут.</summary>
	Crushing = 2
}

/// <summary>Статья VIII «О купечестве и пошлинах»: свобода торговли.</summary>
public enum TradeLaw
{
	/// <summary>Свободный торг: объёмы растут, тарифы символичны.</summary>
	FreeTrade = 0,

	/// <summary>Умеренные пошлины.</summary>
	Moderate = 1,

	/// <summary>Протекционизм: высокие пошлины, торговля сворачивается.</summary>
	Protectionism = 2
}

/// <summary>Статья IX «О вере и веротерпимости»: отношение к иноверцам.</summary>
public enum FaithLaw
{
	/// <summary>Веротерпимость: всякая вера подданных чтима.</summary>
	Tolerance = 0,

	/// <summary>Государственная вера: своя вера первенствует, иные терпимы.</summary>
	StateFaith = 1,

	/// <summary>Чистота веры: иноверцы гонимы, ересь — как зараза.</summary>
	Purity = 2
}

/// <summary>Статья X «О ратной повинности»: устройство войска.</summary>
public enum LevyLaw
{
	/// <summary>Ополчение: войско дёшево, но некрепко.</summary>
	Militia = 0,

	/// <summary>Призыв: обычное народное войско.</summary>
	Conscription = 1,

	/// <summary>Профессиональная гвардия: дорогое, но сильнейшее войско.</summary>
	Guard = 2
}

/// <summary>Статья XI «О чужой земле»: отношение к приобретению земель.</summary>
public enum LandLaw
{
	/// <summary>Только возврат своего: чужие захваты державе заказаны.</summary>
	Restitution = 0,

	/// <summary>Приращение дозволено: захват чужого — законное дело на войне.</summary>
	Expansion = 1,

	/// <summary>Расширение вольно: держава вправе расти без оглядки.</summary>
	FreeConquest = 2
}

/// <summary>Статья XII «О тайной страже»: правопорядок и сыск.</summary>
public enum WatchLaw
{
	/// <summary>Вольница: стражи нет, каждый сам за себя.</summary>
	Liberty = 0,

	/// <summary>Дозор: караул ловит смуту.</summary>
	Patrol = 1,

	/// <summary>Сыск: тайная стража пресекает заговоры в зародыше.</summary>
	SecretPolice = 2
}

/// <summary>Статья XIII «О кровных связях»: кто вправе унаследовать венец.</summary>
public enum BloodLaw
{
	/// <summary>Как сейчас: трон наследует род и порядок закона.</summary>
	Current = 0,

	/// <summary>Прямая кровь: всякий рождённый от короля или королевы вправе на трон.</summary>
	DirectBlood = 1,

	/// <summary>Кровь любого венценосца: всякий, кто кровно связан с каким-либо королём, вправе на любой избранный им трон.</summary>
	AnyRoyalBlood = 2
}

/// <summary>Статья XIV «Об импичменте»: досрочный отзыв выборного главы (только в выборных державах).</summary>
public enum ImpeachmentLaw
{
	/// <summary>Импичмент запрещён.</summary>
	Forbidden = 0,

	/// <summary>Импичменту довольно 50% лидеров городов (серебряные короны).</summary>
	CityLeaders = 1,

	/// <summary>Импичменту довольно 50% голосов всего населения.</summary>
	PeopleVote = 2
}

/// <summary>Статья XV «О федеративном устройстве»: кого держава готова принять (слить) в себя.</summary>
public enum UnionLaw
{
	/// <summary>Держава открыта и готова принять в себя каждого.</summary>
	All = 0,

	/// <summary>Готова принять каждого, с кем есть общие границы.</summary>
	Neighbors = 1,

	/// <summary>Готова принять только тех, с кем общий язык.</summary>
	SameLanguage = 2
}

/// <summary>Конституция державы: пятнадцать записанных статей + детали выборного курса.</summary>
[Serializable]
public sealed class Constitution
{
    /// <summary>0.26: absent in legacy worlds. Rules are not activated merely by loading a young world.</summary>
    public ElectoralChapter Electoral;
	public HeadOfState StateForm = HeadOfState.Hereditary;

	private Franchise _legacyFranchise = Franchise.Clan;
    public Franchise Franchise
    {
        get => Electoral == null ? _legacyFranchise : Electoral.Electors;
        set { _legacyFranchise=value; if(Electoral!=null) Electoral.Electors=value; }
    }

	public BorderLaw Borders = BorderLaw.Open;

	public SuccessionRule Succession = SuccessionRule.AnyAdult;

	public SupremacyRule Supremacy = SupremacyRule.Advisory;

	// ---- 0.24: статьи VII–XV ----

	/// <summary>VII О казне и податях.</summary>
	public TaxLaw Taxes = TaxLaw.Moderate;

	/// <summary>VIII О купечестве и пошлинах.</summary>
	public TradeLaw Trade = TradeLaw.Moderate;

	/// <summary>IX О вере и веротерпимости.</summary>
	public FaithLaw Faith = FaithLaw.Tolerance;

	/// <summary>X О ратной повинности.</summary>
	public LevyLaw Levy = LevyLaw.Conscription;

	/// <summary>XI О чужой земле.</summary>
	public LandLaw Land = LandLaw.Expansion;

	/// <summary>XII О тайной страже.</summary>
	public WatchLaw Watch = WatchLaw.Liberty;

	/// <summary>XIII О кровных связях.</summary>
	public BloodLaw Blood = BloodLaw.Current;

	/// <summary>XIV Об импичменте.</summary>
	public ImpeachmentLaw Impeachment = ImpeachmentLaw.Forbidden;

	/// <summary>XV О федеративном устройстве.</summary>
	public UnionLaw Union = UnionLaw.All;

	// ---- 0.52: статьи XVI–XIX «О народах» ----

	/// <summary>XVI О народах: равные права, коренным больше или пришлым только жить.</summary>
	public RaceLaw Race = RaceLaw.Equal;

	/// <summary>XVII О языке: свой язык в школах или только общий.</summary>
	public LanguageLaw Language = LanguageLaw.Free;

	/// <summary>XVIII О суде народов: свой суд у общин или общий для всех.</summary>
	public CourtLaw Court = CourtLaw.Own;

	/// <summary>XIX О местных уставах: разрешает ли столица городам писать свою конституцию.</summary>
	public bool AllowLocalConstitutions = true;

	/// <summary>XX О противоречиях: терпит ли столица расхождения местных уставов с общим законом.</summary>
	public bool TolerateLocalContradictions = true;

	/// <summary>XXI О чужих на троне: может ли править тот, кто не из коренного народа.</summary>
	public bool ForeignRulerAllowed = true;

	// ---- 0.53: палаты ----

	/// <summary>XXII О палатах: одна или две. По умолчанию — как велит строй.</summary>
	public ChamberCount Chambers = ChamberCount.One;

	/// <summary>XXIII О совете земель: от чего в нём голоса — от городов, народов или родов.</summary>
	public UpperChamberBasis UpperBasis = UpperChamberBasis.Cities;

	/// <summary>XXIV О вето: может ли правитель не подписать закон, принятый палатами.</summary>
	public bool RulerVeto = true;

	/// <summary>XXV О роспуске: может ли правитель разогнать палаты.</summary>
	public bool RulerMayDissolve;

	/// <summary>Власть войны — следствие формы (§2.3 дизайна); отдельно не правится.</summary>
	public WarAuthority WarPower => ConstitutionGates.WarAuthorityOf(StateForm);

	/// <summary>Проводятся ли общенародные выборы головы (президент/танистр).</summary>
	public bool HoldsElectionsForHead => (StateForm == HeadOfState.Elected || StateForm == HeadOfState.Parliamentary);

	/// <summary>Короткая строка для UI: «Абсолютная монархия · война: правитель · границы: открытые».</summary>
	public string Summary(IdeologyType ideology)
	{
		StringBuilder sb = new StringBuilder();
		sb.Append(ConstitutionTexts.FormName(StateForm, ideology));
		sb.Append(" · война: ");
		sb.Append(ConstitutionTexts.WarAuthorityText(WarPower));
		sb.Append(" · границы: ");
		sb.Append(ConstitutionTexts.BorderText(Borders));
		return sb.ToString();
	}
}

/// <summary>Дефолтные конституции: подвид формы правления по идеологии (§2.1 дизайна),
/// ограничение по эпохе (парламентская монархия — с Нового времени).</summary>
public static class ConstitutionDefaults
{
	public static Constitution For(IdeologyType ideology, IdeologyEpoch epoch)
	{
		var c = new Constitution();
		HeadOfState form;
		switch (ideology)
		{
			case IdeologyType.Tribalism:
				form = HeadOfState.Hereditary; // вождь старшин
				c.Franchise = Franchise.Clan;
				c.Borders = BorderLaw.Open;
				c.Succession = SuccessionRule.AnyAdult;
				c.Supremacy = SupremacyRule.Advisory;
				break;
			case IdeologyType.Monarchy:
				// с Нового времени — парламентская; раньше — абсолютная
				form = (epoch >= IdeologyEpoch.EarlyModern) ? HeadOfState.ParliamentaryMonarchy : HeadOfState.Hereditary;
				c.Franchise = Franchise.Notables;
				c.Borders = BorderLaw.Controlled;
				c.Succession = SuccessionRule.Strict;
				c.Supremacy = (form == HeadOfState.ParliamentaryMonarchy) ? SupremacyRule.Binding : SupremacyRule.Advisory;
				break;
			case IdeologyType.Ethnonationalism:
				form = HeadOfState.Hereditary; // вождь нации
				c.Franchise = Franchise.Clan;
				c.Borders = BorderLaw.Closed;
				c.Succession = SuccessionRule.Strict;
				c.Supremacy = SupremacyRule.Advisory;
				break;
			case IdeologyType.Liberalism:
				form = HeadOfState.Parliamentary; // парламент решает, кабинет исполняет
				c.Franchise = Franchise.Universal;
				c.Borders = BorderLaw.Open;
				c.Succession = SuccessionRule.AnyAdult;
				c.Supremacy = SupremacyRule.Binding;
				break;
			case IdeologyType.Militarism:
				form = HeadOfState.Hereditary; // стратократ отвечает перед военным советом, а не перед сенатом
				c.Franchise = Franchise.Notables;
				c.Borders = BorderLaw.Closed;
				c.Succession = SuccessionRule.Strict;
				c.Supremacy = SupremacyRule.Advisory;
				break;
			case IdeologyType.Theocracy:
				form = HeadOfState.Parliamentary; // клир — его собрание
				c.Franchise = Franchise.Notables;
				c.Borders = BorderLaw.Controlled;
				c.Succession = SuccessionRule.Strict;
				c.Supremacy = SupremacyRule.Binding;
				break;
			case IdeologyType.Technocracy:
				form = HeadOfState.Parliamentary; // технократические директора решают собранием
				c.Franchise = Franchise.Notables;
				c.Borders = BorderLaw.Controlled;
				c.Succession = SuccessionRule.AnyAdult;
				c.Supremacy = SupremacyRule.Binding;
				break;
			case IdeologyType.Aristocracy:
				form = HeadOfState.Parliamentary; // сенат
				c.Franchise = Franchise.Notables;
				c.Borders = BorderLaw.Controlled;
				c.Succession = SuccessionRule.Strict;
				c.Supremacy = SupremacyRule.Binding;
				break;
			case IdeologyType.MerchantRepublic:
				form = HeadOfState.Parliamentary; // дружина купцов решает окол неё
				c.Franchise = Franchise.Notables;
				c.Borders = BorderLaw.Controlled;
				c.Succession = SuccessionRule.AnyAdult;
				c.Supremacy = SupremacyRule.Binding;
				break;
			case IdeologyType.Democracy:
				// президентская демократия по умолчанию: война — исполнительная ветвь (ByRuler);
				// парламентский подвид рождается правками 26.2, не дефолтом.
				form = HeadOfState.Elected;
				c.Franchise = Franchise.Universal;
				c.Borders = BorderLaw.Open;
				c.Succession = SuccessionRule.AnyAdult;
				c.Supremacy = SupremacyRule.Binding;
				break;
			case IdeologyType.Egalitarianism:
				form = HeadOfState.Assembly; // вече
				c.Franchise = Franchise.Universal;
				c.Borders = BorderLaw.Open;
				c.Succession = SuccessionRule.AnyAdult;
				c.Supremacy = SupremacyRule.Binding;
				break;
			case IdeologyType.Communism:
				// утверждённая таблица войны §2.3: коммунизм — ByRuler (партия решает,
				// не выходя к собранию) — потому Hereditary-ветвь, а не собрание.
				form = HeadOfState.Hereditary;
				c.Franchise = Franchise.Clan;
				c.Borders = BorderLaw.Closed;
				c.Succession = SuccessionRule.Strict;
				c.Supremacy = SupremacyRule.Binding;
				break;
			case IdeologyType.Khaganate:
				form = HeadOfState.Hereditary; // каган / хатун правит по праву рода и курултая
				c.Franchise = Franchise.Clan;
				c.Borders = BorderLaw.Open;
				c.Succession = SuccessionRule.AnyAdult;
				c.Supremacy = SupremacyRule.Advisory;
				break;
			case IdeologyType.Empire:
				form = HeadOfState.Hereditary; // император / императрица
				c.Franchise = Franchise.Notables;
				c.Borders = BorderLaw.Controlled;
				c.Succession = SuccessionRule.Strict;
				c.Supremacy = SupremacyRule.Advisory;
				break;
			case IdeologyType.FreeTerritory:
				form = HeadOfState.Assembly; // вольный круг: атаман — слуга вольницы, избирается бессрочно до снятия жителями
				c.Franchise = Franchise.Universal;
				c.Borders = BorderLaw.Open;
				c.Succession = SuccessionRule.AnyAdult;
				c.Supremacy = SupremacyRule.Binding;
				break;
			default:
				form = HeadOfState.Hereditary;
				c.Franchise = Franchise.Clan;
				c.Borders = BorderLaw.Open;
				c.Succession = SuccessionRule.AnyAdult;
				c.Supremacy = SupremacyRule.Advisory;
				break;
		}
		c.StateForm = form;
		ApplySocietalDefaults(c, ideology);
		return c;
	}

	/// <summary>0.24: статьи VII–XV по умолчанию — по природе строя и идеологии.
	/// Реальная таблица дефолтов (согласована в дизайне 0.24).</summary>
	private static void ApplySocietalDefaults(Constitution c, IdeologyType ideology)
	{
		switch (ideology)
		{
			case IdeologyType.Tribalism:
				c.Taxes = TaxLaw.Light; c.Trade = TradeLaw.FreeTrade; c.Faith = FaithLaw.Tolerance;
				c.Levy = LevyLaw.Militia; c.Land = LandLaw.Expansion; c.Watch = WatchLaw.Liberty;
				c.Blood = BloodLaw.Current; c.Impeachment = ImpeachmentLaw.Forbidden; c.Union = UnionLaw.All;
				break;
			case IdeologyType.Monarchy:
				c.Taxes = TaxLaw.Moderate; c.Trade = TradeLaw.Moderate; c.Faith = FaithLaw.StateFaith;
				c.Levy = LevyLaw.Conscription; c.Land = LandLaw.Expansion; c.Watch = WatchLaw.Patrol;
				c.Blood = BloodLaw.DirectBlood; c.Impeachment = ImpeachmentLaw.Forbidden; c.Union = UnionLaw.Neighbors;
				break;
			case IdeologyType.Ethnonationalism:
				c.Taxes = TaxLaw.Crushing; c.Trade = TradeLaw.Protectionism; c.Faith = FaithLaw.Purity;
				c.Levy = LevyLaw.Guard; c.Land = LandLaw.FreeConquest; c.Watch = WatchLaw.SecretPolice;
				c.Blood = BloodLaw.DirectBlood; c.Impeachment = ImpeachmentLaw.Forbidden; c.Union = UnionLaw.SameLanguage;
				break;
			case IdeologyType.Liberalism:
				c.Taxes = TaxLaw.Light; c.Trade = TradeLaw.FreeTrade; c.Faith = FaithLaw.Tolerance;
				c.Levy = LevyLaw.Militia; c.Land = LandLaw.Restitution; c.Watch = WatchLaw.Patrol;
				c.Blood = BloodLaw.Current; c.Impeachment = ImpeachmentLaw.PeopleVote; c.Union = UnionLaw.All;
				break;
			case IdeologyType.Militarism:
				c.Taxes = TaxLaw.Crushing; c.Trade = TradeLaw.Moderate; c.Faith = FaithLaw.StateFaith;
				c.Levy = LevyLaw.Guard; c.Land = LandLaw.FreeConquest; c.Watch = WatchLaw.Patrol;
				c.Blood = BloodLaw.DirectBlood; c.Impeachment = ImpeachmentLaw.Forbidden; c.Union = UnionLaw.Neighbors;
				break;
			case IdeologyType.Theocracy:
				c.Taxes = TaxLaw.Moderate; c.Trade = TradeLaw.Moderate; c.Faith = FaithLaw.Purity;
				c.Levy = LevyLaw.Conscription; c.Land = LandLaw.Expansion; c.Watch = WatchLaw.SecretPolice;
				c.Blood = BloodLaw.Current; c.Impeachment = ImpeachmentLaw.Forbidden; c.Union = UnionLaw.SameLanguage;
				break;
			case IdeologyType.Technocracy:
				c.Taxes = TaxLaw.Moderate; c.Trade = TradeLaw.Moderate; c.Faith = FaithLaw.Tolerance;
				c.Levy = LevyLaw.Guard; c.Land = LandLaw.Restitution; c.Watch = WatchLaw.Patrol;
				c.Blood = BloodLaw.Current; c.Impeachment = ImpeachmentLaw.CityLeaders; c.Union = UnionLaw.Neighbors;
				break;
			case IdeologyType.Aristocracy:
				c.Taxes = TaxLaw.Light; c.Trade = TradeLaw.Moderate; c.Faith = FaithLaw.StateFaith;
				c.Levy = LevyLaw.Conscription; c.Land = LandLaw.Expansion; c.Watch = WatchLaw.Patrol;
				c.Blood = BloodLaw.DirectBlood; c.Impeachment = ImpeachmentLaw.CityLeaders; c.Union = UnionLaw.Neighbors;
				break;
			case IdeologyType.MerchantRepublic:
				c.Taxes = TaxLaw.Light; c.Trade = TradeLaw.FreeTrade; c.Faith = FaithLaw.Tolerance;
				c.Levy = LevyLaw.Guard; c.Land = LandLaw.Restitution; c.Watch = WatchLaw.Patrol;
				c.Blood = BloodLaw.Current; c.Impeachment = ImpeachmentLaw.CityLeaders; c.Union = UnionLaw.All;
				break;
			case IdeologyType.Democracy:
				c.Taxes = TaxLaw.Moderate; c.Trade = TradeLaw.FreeTrade; c.Faith = FaithLaw.Tolerance;
				c.Levy = LevyLaw.Conscription; c.Land = LandLaw.Restitution; c.Watch = WatchLaw.Patrol;
				c.Blood = BloodLaw.Current; c.Impeachment = ImpeachmentLaw.PeopleVote; c.Union = UnionLaw.All;
				break;
			case IdeologyType.Egalitarianism:
				c.Taxes = TaxLaw.Moderate; c.Trade = TradeLaw.Moderate; c.Faith = FaithLaw.Tolerance;
				c.Levy = LevyLaw.Militia; c.Land = LandLaw.Restitution; c.Watch = WatchLaw.Patrol;
				c.Blood = BloodLaw.Current; c.Impeachment = ImpeachmentLaw.PeopleVote; c.Union = UnionLaw.All;
				break;
			case IdeologyType.Communism:
				c.Taxes = TaxLaw.Crushing; c.Trade = TradeLaw.Protectionism; c.Faith = FaithLaw.StateFaith;
				c.Levy = LevyLaw.Guard; c.Land = LandLaw.FreeConquest; c.Watch = WatchLaw.SecretPolice;
				c.Blood = BloodLaw.Current; c.Impeachment = ImpeachmentLaw.Forbidden; c.Union = UnionLaw.Neighbors;
				break;
			case IdeologyType.Khaganate:
				c.Taxes = TaxLaw.Light; c.Trade = TradeLaw.FreeTrade; c.Faith = FaithLaw.Tolerance;
				c.Levy = LevyLaw.Conscription; c.Land = LandLaw.FreeConquest; c.Watch = WatchLaw.Patrol;
				c.Blood = BloodLaw.DirectBlood; c.Impeachment = ImpeachmentLaw.Forbidden; c.Union = UnionLaw.All;
				break;
			case IdeologyType.Empire:
				c.Taxes = TaxLaw.Moderate; c.Trade = TradeLaw.Moderate; c.Faith = FaithLaw.StateFaith;
				c.Levy = LevyLaw.Guard; c.Land = LandLaw.Expansion; c.Watch = WatchLaw.SecretPolice;
				c.Blood = BloodLaw.DirectBlood; c.Impeachment = ImpeachmentLaw.Forbidden; c.Union = UnionLaw.All;
				break;
			case IdeologyType.FreeTerritory:
				c.Taxes = TaxLaw.Light; c.Trade = TradeLaw.FreeTrade; c.Faith = FaithLaw.Tolerance;
				c.Levy = LevyLaw.Militia; c.Land = LandLaw.Restitution; c.Watch = WatchLaw.Liberty;
				c.Blood = BloodLaw.Current; c.Impeachment = ImpeachmentLaw.PeopleVote; c.Union = UnionLaw.All;
				break;
			default:
				c.Taxes = TaxLaw.Moderate; c.Trade = TradeLaw.Moderate; c.Faith = FaithLaw.Tolerance;
				c.Levy = LevyLaw.Conscription; c.Land = LandLaw.Expansion; c.Watch = WatchLaw.Liberty;
				c.Blood = BloodLaw.Current; c.Impeachment = ImpeachmentLaw.Forbidden; c.Union = UnionLaw.All;
				break;
		}
	}
}

/// <summary>История правок конституции (учредительные акты и ревизии) — для летописи и отладки.</summary>
[Serializable]
public sealed class ConstitutionEdit
{
	public int Year { get; set; }

	public string Article { get; set; } = string.Empty;

	public string From { get; set; } = string.Empty;

	public string To { get; set; } = string.Empty;

	public int EditorActorId { get; set; }

	public string EditorName { get; set; } = string.Empty;
}

/// <summary>Голосование по вопросу войны: зафиксированное разрешение и его срок.</summary>
[Serializable]
public sealed class WarAuthorization
{
	public int TargetKingdomId { get; set; }

	public int Year { get; set; }

	public int ValidUntilYear { get; set; }

	public int VotesFor { get; set; }

	public int VotesAgainst { get; set; }
}
