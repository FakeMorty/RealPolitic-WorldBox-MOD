namespace RealPolitic.Core;

public sealed class SimParams
{
    public float RegionalObedienceThreshold=0.45f;
    public int RegionalLowYears=3;
    public int RegionalRetryYears=10;
	public float SpreadFromState = 0.1f;

	public float SpreadFromNeighbor = 0.045f;

	public float ConversionBase = 0.55f;

	public float ActorConvertThreshold = 1f;

	public float CityConvertThreshold = 2.2f;


	public float WarLegitimacyDrain = 4f;

	public float StarveLegitimacyDrain = 10f;

	public float LegitimacyRecovery = 4f;

	public float DissentLegitimacyDrain = 8f;



	public float DissentRate = 0.2f;

	public float DissentDecay = 0.25f;

	/// <summary>Первые N лет жизни молодого государства народ не поднимает протестные движения, петиции, ультиматумы, революции и мятежи против власти.</summary>
	public int YoungStateGraceYears = 50;

	/// <summary>Легитимность, ниже которой возможна революция (смена строя снизу).</summary>
	public float RevolutionLegitimacyLimit = 35f;

	/// <summary>Среднее недовольство подданных, с которого возможна революция.</summary>
	public float RevolutionDissentMin = 0.35f;

	/// <summary>Базовый годовой шанс вспышки революции при выполненных условиях.</summary>
	public float RevolutionSparkChance = 0.12f;

	/// <summary>Базовый шанс успеха революции в решающий год.</summary>
	public float RevolutionBaseChance = 0.35f;

	/// <summary>Длительность активной фазы революции в годах.</summary>
	public int RevolutionDuration = 3;

	/// <summary>Множитель роста недовольства в годы революции.</summary>
	public float RevolutionDissentMultiplier = 3f;

	// ---- Гражданские войны и расколы (этап 17) -------------------------------

	/// <summary>Базовый годовой шанс мятежа при прочих равных (умножается на недовольство и легитимность).</summary>
	public float CivilWarBaseChance = 0.05f;

	/// <summary>Потолок годового шанса (чтобы обречённое государство не взрывалось мгновенно).</summary>
	public float CivilWarChanceCap = 0.55f;

	/// <summary>Порог легитимности, ниже которого риск мятежа резко растёт.</summary>
	public float CivilWarLegitimacyLimit = 32f;

	/// <summary>Доля городов (кроме столицы), которая поднимается одновременно.</summary>
	public float CivilWarRebelCityShare = 0.4f;

	/// <summary>Срок войны в годах, после которого (вместе с CivilWarFrozenExtraYears) истощённые стороны мирятся — отделение состоялось.</summary>
	public int CivilWarDurationYears = 5;

	/// <summary>Добавочные годы к сроку войны до принудительного мира.</summary>
	public int CivilWarFrozenExtraYears = 3;

	/// <summary>Разовый удар по легитимности в год начала мятежа.
	/// Держим умеренным: легитимность восстанавливается всего на LegitimacyRecovery/10 за год,
	/// и большой разовый штраф запускает спираль «тирания → мятеж → тирания».</summary>
	public float CivilWarLegitimacyPenalty = 10f;

	/// <summary>Ежегодное падение легитимности, пока идёт война.</summary>
	public float CivilWarLegitimacyDrainPerYear = 1.2f;

	/// <summary>Передышка между гражданскими войнами одного государства.</summary>
	public int CivilWarCooldownYears = 6;

	/// <summary>Число городов, с которого державу начинает рвать от перенапряжения.</summary>
	public int CivilWarOverexpansionCities = 6;

	/// <summary>Разрешено ли создавать государства повстанцев (без этого гражданские войны невозможны:
	/// с 0.23 мятеж — это всегда настоящее государство и настоящая война).</summary>
	public bool AllowSecession = true;

	// ---------------------------------------------------------------------
	// Экономика и торговля
	// ---------------------------------------------------------------------

	/// <summary>Включена ли экономическая модель (налоги, благосостояние, кризисы).</summary>
	public bool EnableEconomy = true;

	/// <summary>Включена ли торговля между державами (торговые пути и прибыль с них).</summary>
	public bool EnableTrade = true;

	/// <summary>Базовый налоговый сбор с одного города в год.</summary>
	public float EconomyTaxBase = 2.2f;

	/// <summary>Границы налоговой ставки (множитель сбора).</summary>
	public float TaxRateMin = 0.6f;

	public float TaxRateMax = 1.6f;

	/// <summary>Максимальная пошлина на чужих купцов.</summary>
	public float TariffMax = 0.5f;

	/// <summary>Прибыль с единицы объёма торговли за уровень пути.</summary>
	public float TradeBaseProfit = 0.35f;

	/// <summary>Прирост объёма пути за год, пока он открыт.</summary>
	public float TradeVolumeGrowth = 1.4f;

	/// <summary>Потолок объёма пути. Уровень пути повышает прибыль, но не объём —
	/// иначе доход растёт квадратично и торговля перестаёт соотноситься с налогами.</summary>
	public float TradeVolumeCap = 22f;

	/// <summary>Во сколько раз торговый доход может превышать налоговый.
	/// Без потолка один развитый путь приносил бы больше всей казны.</summary>
	public float TradeIncomeCapOfTaxes = 1.5f;

	/// <summary>Как быстро тает объём пути в блокаде.</summary>
	public float TradeBlockadeDecay = 3f;

	/// <summary>Максимальный уровень пути (караванная тропа → дорога → торговый договор).</summary>
	public int TradeMaxLevel = 3;

	/// <summary>Сколько лет путь должен прожить и какой объём набрать для повышения уровня.</summary>
	public int TradeLevelUpYears = 12;

	public float TradeLevelUpVolume = 20f;

	/// <summary>Годовой шанс открыть путь с подходящим соседом (умножается на склонность строя).</summary>
	public float TradeRouteChance = 0.09f;

	/// <summary>Минимум городов у соседа, чтобы с ним имело смысл торговать.</summary>
	public int TradeMinCities = 2;

	/// <summary>Предел числа путей у одной державы.</summary>
	public int MaxTradeRoutes = 6;

	/// <summary>Бонус к прибыли и к шансу пути с союзником/участником унии.</summary>
	public float AlliedTradeBonus = 0.25f;

	/// <summary>Можно ли торговать во время войны.</summary>
	public bool TradeDuringWar = false;

	/// <summary>Доля торговой прибыли, превращаемая в продовольствие в голодный год.</summary>
	public float TradeFoodShare = 0.6f;

	/// <summary>Содержание одного воина в год и управление одним городом.</summary>
	public float ArmyUpkeepPerWarrior = 0.16f;

	public float AdminCostPerCity = 0.7f;

	/// <summary>Насколько дороже армия во время войны.</summary>
	public float WarUpkeepExtra = 0.35f;

	/// <summary>Экономия расходов в режиме жёсткой экономии (0..1).</summary>
	public float AusteritySavings = 0.3f;

	/// <summary>Целевое благосостояние державы, которая сводит концы с концами в мирный год.
	/// Благосостояние стремится к цели (как легитимность), а не растёт бесконечно:
	/// иначе любая стабильная держава упирается в 100% и boom становится routine.</summary>
	public float ProsperityBaseline = 65f;

	/// <summary>Доля разрыва с целью, закрываемая за год.</summary>
	public float ProsperityConvergence = 0.12f;

	/// <summary>Сдвиги цели благосостояния: дефицит, война, голод, кризис, экономия, налоги.</summary>
	public float ProsperityDeficitLoss = 10f;

	public float ProsperityWarLoss = 25f;

	public float ProsperityFamineLoss = 30f;

	public float ProsperityCrisisLoss = 15f;

	public float ProsperityAusterityLoss = 5f;

	public float ProsperityHighTaxLoss = 5f;

	public float ProsperityLowTaxGain = 5f;

	/// <summary>Вклад торговли в цель благосостояния: за путь и потолок.</summary>
	public float ProsperityTradePerRoute = 2.5f;

	public float ProsperityTradeMax = 15f;

	/// <summary>Границы настроения: сытое благосостояние и голодное.</summary>
	public float ProsperityHappyLine = 65f;

	public float ProsperityAngryLine = 35f;

	/// <summary>Влияние экономики на поддержку и настроение жителей.</summary>
	public float MoodProsperityGain = 0.8f;

	public float MoodProsperityLoss = 1.2f;

	public float MoodCrisisLoss = 1.6f;

	public float MoodHighTaxLoss = 0.7f;

	public float MoodAusterityLoss = 1f;

	/// <summary>Как сильно поддержка тянет за собой недовольство (доли 0..1).</summary>
	public float DissentFromMoodRate = 0.006f;

	/// <summary>Перевод поддержки в настроение жителей (очки счастья).</summary>
	public float HappinessFromMoodRate = 1.5f;

	/// <summary>Годовой шанс спада при прочих равных.</summary>
	public float RecessionChance = 0.035f;

	/// <summary>Включены династии: личности правителей, отношения и династические браки (этап 19).</summary>
	public bool EnableDynasties = true;

	/// <summary>Базовый шанс заключить династический брак при предложении.</summary>
	public float MarriageBaseChance = 0.6f;

	/// <summary>Минимальный возраст правителя для династического брака.</summary>
	public int MarriageMinAge = 16;

	/// <summary>Включены договоры (этап 20): пакты о ненападении, оборонительные и торговые.</summary>
	public bool EnableTreaties = true;

	/// <summary>Срок действия договора в годах.</summary>
	public int TreatyYears = 20;

	/// <summary>Торговый бонус действующего торгового соглашения.</summary>
	public float TradeAgreementBonus = 0.15f;

	/// <summary>Включены казус белли и обиды (этап 20): поводы к войне и их цена.</summary>
	public bool EnableCasusBelli = true;

	/// <summary>Включён шпионаж (этап 20): разведсети и тайные операции.</summary>
	public bool EnableEspionage = true;

	/// <summary>Стоимость вложения в разведсеть в золоте.</summary>
	public float SpyInvestCost = 25f;

	/// <summary>Лет перезарядки между тайными операциями одной державы.</summary>
	public int SpyCooldownYears = 5;

	/// <summary>Включены коалиции против агрессоров (этап 20).</summary>
	public bool EnableCoalitions = true;

	/// <summary>Очки агрессии, при которых соседи собирают коалицию.</summary>
	public float CoalitionAggressionThreshold = 40f;

	/// <summary>Лет жизни коалиции после последней войны с целью.</summary>
	public int CoalitionPeaceYears = 8;

	/// <summary>Доля дохода вассала, уходящая сюзерену как дань (этап 20).</summary>
	public float VassalTributeRate = 0.10f;

	/// <summary>Одобрение вассала, ниже которого он поднимает войну за независимость.</summary>
	public float VassalRebellionApproval = 0.30f;

	// Этап 21: альянсы игры, вассалитет, соединяющее поглощение.

	/// <summary>Следить за ванильными альянсами игры и писать их в летопись.</summary>
	public bool TrackAlliances = true;

	// Этап 22: война и мирные договоры — усталость, перемирия, репарации, уступка земель.

	/// <summary>Включены мирные договоры: усталость от войны, перемирия, условия победителя.</summary>
	public bool EnableWarPeace = true;

	/// <summary>Прирост усталости от войны за год в состоянии войны (до множителя строя).</summary>
	public float WarFatigueRate = 7f;

	/// <summary>Лечение усталости за мирный год.</summary>
	public float WarFatigueDecay = 9f;

	/// <summary>Длительность перемирия после любой развязки войны.</summary>
	public int TruceYears = 10;

	/// <summary>Максимум городов, забираемых победителем по договору
	/// (столица и последний город проигравшего неприкосновенны).</summary>
	public int MaxCededCities = 3;

	/// <summary>Сколько лет проигравший платит репарации.</summary>
	public int ReparationsYears = 8;

	/// <summary>Доля годового дохода проигравшего, уходящая на репарации
	/// (если экономика недоступна — фиксированные 12 золота в год).</summary>
	public float ReparationsIncomeShare = 0.35f;

	/// <summary>Брать стёртую с карты державу после тотального поражения в поглощённый
	/// вассалитет: все уцелевшие города остаются у победителя, старые границы запоминаются.</summary>
	public bool EnableRestoration = true;

	/// <summary>Минимум довоенных городов, чтобы земли уничтоженной державы оформлять вассальными.</summary>
	public int RestorationMinCities = 3;

	/// <summary>Legacy-параметр старых мирных восстановлений; с 0.31.2 не используется.</summary>
	public int RestorationVictorKeeps = 3;

	/// <summary>Legacy-параметр старых мирных восстановлений; с 0.31.2 не используется.</summary>
	public int RestorationReparationsYears = 12;

	// --- Этап 23: честная хроника, границы, рейды, династии ---

	/// <summary>Настоящее регентство: малолетний монарх отдаёт титул регенту до 18 лет.</summary>
	public bool EnableRealRegency = true;

	/// <summary>Пограничная политика государств: открытие/закрытие границ соседями.</summary>
	public bool EnableBorders = true;

	/// <summary>Сколько лет пара держав не открывает границу после облавы на чужаках.</summary>
	public int BorderLockYears = 50;

	/// <summary>0.23.2: границы по умолчанию закрыты (true) — открытие надо заслужить дипломатией.</summary>
	public bool BordersClosedByDefault = true;

	/// <summary>Сколько лет подряд пара должна выполнять условия открытия (мир, торговля, доверие) — «переговоры».</summary>
	public int BorderOpeningTalksYears = 8;

	/// <summary>Максимальная взаимная обида, при которой открытие границы ещё возможно.</summary>
	public float BorderOpeningMaxGrievance = 5f;

	/// <summary>Минимальная легитимность обеих сторон для открытия границы (слабая власть боится чужаков).</summary>
	public float BorderOpeningMinLegitimacy = 45f;

	/// <summary>Урон чужаку за пересечение закрытой границы (0 — бют, но без ущерба).</summary>
	public float BorderIntruderDamage = 5f;

	/// <summary>Конституции: действия мода подчиняются писаным законам держав</summary>
	public bool EnableConstitution = true;

	/// <summary>Между провалом народного/сенатного голосования и следующим разрешена пауза, лет.</summary>
	public int ReferendumCooldownYears = 10;

	/// <summary>Поправки: как часто правитель может править одну статью конституции, лет.</summary>
	public int RulerAmendmentYears = 15;

	/// <summary>Поправки: минимальная легитимность, при которой правитель берётся за перо.</summary>
	public float RulerAmendmentLegitimacy = 50f;

	/// <summary>Эпохальные ограничения строя: модерные идеологии появляются только с временем.</summary>
	public bool EnableEraRestrictions = true;

	/// <summary>Год начала средневековья: аристократы, торговцы, затворники.</summary>
	public int EraMedievalYear = 60;

	/// <summary>Год начала нового времени: эгалитаризм и демократия.</summary>
	public int EraEarlyModernYear = 150;

	/// <summary>Год наступления современности: технократия, коммунизм, этнонационализм.</summary>
	public int EraModernYear = 250;

	/// <summary>Тайные авиаудары высокоразвитых держав во время войны.</summary>
	public bool EnablePowerRaids = true;

	/// <summary>Урон благосостоянию жертвы от одного авиаудара.</summary>
	public float PowerRaidProsperityDamage = 4f;

	/// <summary>Новая эра династии: королевская семья носит фамилию, клан — фамилию основателя.</summary>
	public bool EnableDynastySurnames = true;

	/// <summary>Длительность спада, депрессии и банкротства (к значению добавляется разброс).</summary>
	public int RecessionYears = 2;

	public int DepressionYears = 3;

	public int BankruptcyYears = 2;

	/// <summary>Шанс, что спад на фоне дефицита перерастёт в депрессию.</summary>
	public float DepressionEscalationChance = 0.3f;

	/// <summary>Сколько лет дефицита подряд ведут к банкротству казны.</summary>
	public int BankruptcyDeficitYears = 4;

	/// <summary>Урон банкротства: золото и легитимность.</summary>
	public float BankruptcyGoldLoss = 60f;

	/// <summary>Банкротство объявляется только если в казне меньше этого золота.</summary>
	public float BankruptcyGoldThreshold = 50f;

	public float BankruptcyLegitimacyLoss = 12f;

	/// <summary>Доля торговли в доходе, при которой потеря всех путей считается крахом торговли.</summary>
	public float TradeCollapseShare = 0.3f;

	/// <summary>Передышка между кризисами одной державы.</summary>
	public int CrisisCooldownYears = 8;

	/// <summary>Условия экономического бума: благосостояние, число путей и периодичность.</summary>
	public float BoomProsperity = 74f;

	/// <summary>Бум — плод торговли: без развитых путей его не бывает.
	/// При цели 65 + максимум 15 за торговлю бум доступен только крупным торговым узлам.</summary>
	public int BoomMinRoutes = 2;

	public int BoomCooldownYears = 25;

	public float BoomGoldBonus = 40f;

	public float BoomHappiness = 6f;

	/// <summary>На сколько лет вводится режим экономии и торговое эмбарго.</summary>
	public int AusterityYears = 5;

	public int EmbargoYears = 8;

	/// <summary>Стоимость открытия торгового пути золотом (для стратегического действия).</summary>
	public float TradeInvestCost = 30f;

	/// <summary>Порог дефицита, с которого ИИ включает режим экономии.</summary>
	public int AusterityDeficitYears = 2;

	/// <summary>Насколько экономический кризис поднимает риск мятежа (множитель шанса).</summary>
	public float EconomicCrisisRebellionFactor = 1.8f;

	/// <summary>0.31: автоматическое включение международного признания и Совета великих держав на всех картах (включая старые сохранения).</summary>
	public bool EnableUniversalRecognition = true;

	/// <summary>0.31: год, до которого племена живут спокойно, а после которого народ внутри племени требует цивилизации.</summary>
	public int TribalDeclineYear = 200;

	/// <summary>0.31: стоимость мирной цивилизации племени в золоте казны.</summary>
	public float TribalCivilizationGoldCost = 220f;

	/// <summary>0.31: минимальное благосостояние племени для мирного перехода к цивилизации.</summary>
	public float TribalCivilizationProsperityMin = 62f;

	/// <summary>0.31: затраты благосостояния на строительство государственных институтов при цивилизации племени.</summary>
	public float TribalCivilizationProsperityCost = 18f;

	// ------------------------------------------------------------------
	// 0.32: КМУД — Конференция Мирного Урегулирования Дел
	// ------------------------------------------------------------------

	/// <summary>0.32: созывать ли КМУД (всемирную мирную конференцию) на этом мире.</summary>
	public bool EnableKmud = true;

	/// <summary>0.32: год первого созыва КМУД. По умолчанию 200-й год мира.</summary>
	public int KmudYear = 200;

	/// <summary>
	/// 0.34: через сколько лет после съезда собирается следующий. По умолчанию 50 лет
	/// (решение владельца: 200-й год, потом каждые 50 лет).
	/// </summary>
	public int KmudIntervalYears = 100;

	/// <summary>0.34: сколько лет держится пакт «только оборона» между бывшими врагами.</summary>
	public int KmudPactYears = 50;

	/// <summary>0.32: сколько лет идёт конференция (в это время жители голосуют по вопросам).</summary>
	public int KmudYears = 10;

	/// <summary>
	/// 0.32: сколько лет держится базовый запрет войн после созыва. Не меньше длительности
	/// конференции; сколько ещё продлить — решают жители голосованием (0/10/20/30 лет).
	/// </summary>
	public int KmudPauseYears = 10;

	/// <summary>0.46: расселение — державы сами основывают города на свободной земле.</summary>
	public bool EnableSettlementExpansion = true;

	/// <summary>0.46: переселение — жители подают заявления на переезд в другую державу.</summary>
	public bool EnableMigration = true;

	/// <summary>0.46: сколько заявлений на переезд рассматривается за год на всю карту.</summary>
	public int MigrationPerYear = 6;

	/// <summary>0.46: малые народы борются за сохранение (давление, побег, защита по конституции).</summary>
	public bool EnableRaceSurvival = true;

	public SimParams Clone()
	{
		return (SimParams)MemberwiseClone();
	}
}
