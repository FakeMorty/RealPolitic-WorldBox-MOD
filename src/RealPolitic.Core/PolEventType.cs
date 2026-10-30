namespace RealPolitic.Core;

public enum PolEventType
{
	None = 0,
	ActorConverted = 10,
	CityIdeologyShift = 11,
	KingdomAdoptedIdeology = 12,
	KingdomReformed = 13,
	RevolutionStarted = 14,
	RevolutionSucceeded = 15,
	RevolutionFailed = 16,
	RulerDeath = 20,
	Succession = 21,
	LegitimacyCrisis = 22,
	DiplomaticAction = 30,
	UnionFormed = 40,
	UnionMemberJoined = 41,
	UnionDissolved = 42,
	UnionMemberLeft = 43,
	NewReligion = 50,
	NewLanguage = 51,
	NewCulture = 52,
	WarStarted = 60,
	WarEnded = 61,
	IdeologyPowerUsed = 70,
	IdeologyThreatBroke = 71,
	IdeologyPowerEnded = 72,
	RegencyStarted = 80,
	RegencyEnded = 81,
	DemocraticElection = 82,
	EthnonationalistTension = 83,
	DynasticUnionFormed = 84,
	CitySeceded = 85,
	DynasticShift = 86,
	DynasticExtinction = 87,
	CivilWarStarted = 90,
	CivilWarEnded = 95,
	KingdomSeceded = 96,
	TradeRouteOpened = 97,
	TradeRouteUpgraded = 98,
	TradeRouteBroken = 99,
	EconomicCrisisStarted = 100,
	EconomicCrisisEnded = 101,
	EconomicBoom = 102,
	TradeEmbargo = 103,
	DynasticMarriage = 104,
	TreatySigned = 105,
	TreatyBroken = 106,
	TreatyExpired = 107,
	CasusBelliDeclared = 108,
	EspionageExposed = 109,
	EspionageSuccess = 110,
	CoalitionFormed = 111,
	CoalitionWar = 112,
	CoalitionDissolved = 113,
	VassalRebellion = 114,

	// Этап 21: ванильные альянсы, наследование короны в унии, институты федерации,
	// интеграция участников и реформа в полную унию.
	AllianceFormed = 115,
	AllianceBroken = 116,
	AllianceMemberJoined = 117,
	AllianceMemberLeft = 118,
	UnionCrownInherited = 119,
	UnionSuccessionCrisis = 120,
	UnionInstitutionGained = 121,
	IntegrationStarted = 122,
	IntegrationCompleted = 123,
	UnionReformed = 124,
	/// <summary>0.24: держава вступила в соединяющую унию и тотчас поглощена лидером.</summary>
	UnionMerged = 149,
	/// <summary>0.24: собрание/народ отозвал выборного главу импичментом (статья XIV).</summary>
	Impeached = 150,
    KingdomDisappeared = 151,
    LastResidentDied = 152,
    ElectionDeferred = 153,

	// Этап 22: война и мирные договоры — перемирия, репарации, уступка земель, белый мир.
	PeaceSettlement = 125,
	WhitePeace = 126,
	TruceBroken = 127,
	TruceExpired = 128,

	// Этап 23: честная хроника, династия и границы.
	/// <summary>Королевство распалось (вышло минимум крупных фрагментов — суммарная сводка).</summary>
	KingdomCollapsed = 130,
	/// <summary>Действительное регентство: титул монарха носит регент до совершеннолетия наследника.</summary>
	RegencyCrowned = 131,
	/// <summary>Наследник стал совершеннолетним: корона возвращается к нему от регента.</summary>
	RegencyPassed = 132,
	/// <summary>Тайный авиаудар высокоразвитой державы (рейд бомбардировщиков по врагу).</summary>
	PowerRaid = 133,
	/// <summary>Границы между государствами официально открыты.</summary>
	BorderOpened = 134,
	/// <summary>Границы между государствами официально закрыты.</summary>
	BorderClosed = 135,
	/// <summary>Чужака выдворили через закрытую границу (погоней либо облавой).</summary>
	BorderExpulsion = 136,
	/// <summary>Закрытые границы сорвали торговые маршруты двух государств.</summary>
	TradeRoutesSevered = 137,
	/// <summary>Новая эра: королевский клан переименован в фамилию короля-основателя.</summary>
	DynastyRenamed = 138,

	// Этап 26: конституция.
	/// <summary>Голосование народа/сената по вопросу войны (задат маршала было, результат решил всё).</summary>
	WarReferendum = 140,
	/// <summary>На трон вступил наследник вне конституционного закона (акт узурпации).</summary>
	ConstitutionalUsurpation = 141,
	/// <summary>Правитель издаёт правку конституции (стать, что было, что стало).</summary>
	ConstitutionEdited = 142,

	/// <summary>Действие в обход конституции: война без мандата собрания/народа, узурпация.</summary>
	UnconstitutionalAct = 143,

	// 0.23.2: события мира — понятные новичку.
	/// <summary>Сменилась эпоха мира (Эра надежды, солнца, тьмы…) — с пояснением, что она несёт.</summary>
	EraChanged = 144,
	/// <summary>Подвид (раса) вымер — не осталось ни одного живого представителя.</summary>
	SubspeciesExtinct = 145,
	/// <summary>Родственник правящей династии основал собственное государство.</summary>
	RoyalRelativeSeceded = 146,
	/// <summary>Legacy-событие прежнего мирного восстановления державы победителем; с 0.31.2 не публикуется.</summary>
	KingdomRestored = 147,
	/// <summary>0.23.3: внутренний кризис строя миновал — держава пришла в себя после срыва угрозы.</summary>
	IdeologyThreatRecovered = 148,
    GreatPowerCompositionChanged = 260,
    NationalRecognitionPosition = 261,
    InternationalRecognitionDecided = 262,
    CivicElectionDeferred = 263,
    RegionalCivicProcedure = 264,
    CivicAllianceMotion = 265,
    NationalAlliancePosition = 266,

    // 0.27: многошаговые стратегические кампании и дипломатическая память
    StrategicCampaignStarted = 270,
    StrategicCampaignCompleted = 271,
    DiplomaticMemoryRecorded = 272,

    // 0.28: живое общество — петиции, ультиматумы и парламентские партии
    CivicPetitionSubmitted = 280,
    CivicUltimatumResolved = 281,
    ParliamentaryShift = 282,

    // 0.29: восстания поглощённых народов и историческая память городов
    NationalLiberationUprising = 290,

    // 0.30: мирные конференции и коалиционный раздел трофеев
    PeaceConferenceConcluded = 300,
    CoalitionSpoilsDispute = 301,

    // 0.31: международное признание на всех картах и дипломатическая борьба за признание
    RecognitionLobbyMission = 310,
    MetropolisRecognitionProtest = 311,

    // 0.32: КМУД — Конференция Мирного Урегулирования Дел
    KmudConvened = 320,
    KmudClosed = 321,
    KmudPauseViolated = 322,
    KmudAllianceFormed = 323,
    KmudAgenda = 324,
    KmudResult = 325,
    KmudResultsAnnounced = 326,
    KmudPact = 327,
    KmudReparations = 328,
    KmudExecution = 329,
    KmudIdeologyChange = 330,
    KmudSanctions = 331,
    KmudAid = 332,

    // 0.39: запрет объявлять войны вместо гашения; снятие обвинений после наказания
    KmudWarPrevented = 333,
    KmudPardoned = 334,

    // 0.44: «скоро съезд» — чтобы о КМУД было слышно ДО её начала, а не только в год созыва
    KmudUpcoming = 335,

    // 0.46: переселение, расселение, малые народы и парламент
    MigrationResettled = 340,
    CityFounded = 341,
    RaceEndangered = 342,
    RaceProtected = 343,
    ParliamentElected = 344,

    // 0.47: пятый вопрос КМУД — безопасность возвращения народа на родину
    KmudRepatriation = 345,

    // 0.49: гражданский национализм — «вернуть своё государство»
    NationalismSeizedPower = 350,
    NationalismPowerReturned = 351,
    NationalismSuppressed = 352,

    // 0.49.1: расовый национализм — чужая раса берёт державу себе
    RaceNationalismSeizedPower = 353,
    RaceNationalismPowerReturned = 354,

    // 0.50: мир возмущается и наказывает
    WorldOutrageSanctions = 355,
    WorldOutrageStatusLost = 356,

    // 0.52: народы, язык и местные конституции
    PeopleLanguageShift = 360,
    LocalConstitutionAdopted = 361,
    LocalConstitutionBanned = 362,
    LocalConstitutionConflict = 363,
    LocalConstitutionRevolt = 364,
    ForeignRulerWithoutLaw = 365,

    // 0.53: две палаты, законы, вето и ступор
    BillPassed = 370,
    BillRejected = 371,
    ChambersDisagree = 372,
    RulerVetoed = 373,
    VetoOverridden = 374
}
