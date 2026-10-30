using System;
using System.Collections.Generic;
using System.Linq;
using RealPolitic.Core;
using Xunit;

namespace RealPolitic.Tests;

/// <summary>
/// Этап 18: экономика и торговля. Налоги, торговые пути, благосостояние, дефицит,
/// кризисы, эмбарго, режим экономии и связь экономики с гражданской войной.
/// </summary>
public class EconomyTests
{
	// ------------------------------------------------------------------ налоги и доходы

	[Fact]
	public void Economy_Disabled_DoesNothing()
	{
		var p = new SimParams { EnableEconomy = false };
		var state = State(1, IdeologyType.MerchantRepublic);
		var network = new TradeNetwork();
		network.Open(1, 2, 1);

		var report = Economy.Tick(state, Input(1, "Тир", cities: 4), p, network, 10, Rng(1), null);

		Assert.Equal(0f, report.Income);
		Assert.Equal(0f, report.Expenses);
		Assert.Equal(0f, report.GoldDelta);
		Assert.Null(state.Economy);
	}

	[Fact]
	public void Economy_TaxesScaleWithCitiesProsperityAndTaxRate()
	{
		var p = new SimParams();

		float small = Economy.Taxes(2, new EconomyState { Prosperity = 50f, TaxRate = 1f }, p, IdeologyType.Monarchy);
		float big = Economy.Taxes(6, new EconomyState { Prosperity = 50f, TaxRate = 1f }, p, IdeologyType.Monarchy);
		float rich = Economy.Taxes(2, new EconomyState { Prosperity = 95f, TaxRate = 1f }, p, IdeologyType.Monarchy);
		float poor = Economy.Taxes(2, new EconomyState { Prosperity = 5f, TaxRate = 1f }, p, IdeologyType.Monarchy);
		float taxed = Economy.Taxes(2, new EconomyState { Prosperity = 50f, TaxRate = 1.5f }, p, IdeologyType.Monarchy);

		Assert.True(big > small * 2.5f, $"6 городов должны давать заметно больше двух: {big} vs {small}");
		Assert.True(rich > small, "богатая держава собирает больше");
		Assert.True(poor < small, "разорённая держава собирает меньше");
		Assert.True(taxed > small, "высокая ставка даёт больше сбора");
		Assert.Equal(0f, Economy.Taxes(0, new EconomyState(), p, IdeologyType.Monarchy));
	}

	[Fact]
	public void Economy_TradeBiasMakesMerchantRepublicCollectMoreThanTribe()
	{
		var p = new SimParams();
		var eco = new EconomyState { Prosperity = 60f, TaxRate = 1f };

		float merchant = Economy.Taxes(4, eco, p, IdeologyType.MerchantRepublic);
		float tribe = Economy.Taxes(4, eco, p, IdeologyType.Tribalism);

		Assert.True(merchant > tribe * 1.8f, $"торговая республика собирает лучше племени: {merchant} vs {tribe}");
	}

	[Fact]
	public void Economy_ExpensesCoverArmyAndAdmin_AndAusterityCutsThem()
	{
		var p = new SimParams();
		var input = Input(1, "Тир", cities: 4, warriors: 60);
		var eco = new EconomyState();

		float peace = Economy.Expenses(input, eco, p, atWar: false);
		float war = Economy.Expenses(input, eco, p, atWar: true);
		float austere = Economy.Expenses(input, new EconomyState { Austerity = true }, p, atWar: false);

		Assert.True(peace > 0f);
		Assert.True(war > peace, "война дороже мира");
		Assert.True(austere < peace * 0.8f, $"экономия должна резать расходы: {austere} vs {peace}");
	}

	// ------------------------------------------------------------------ торговля

	[Fact]
	public void Economy_TradeIncomeRequiresOpenRoute()
	{
		var p = new SimParams();
		var state = State(1, IdeologyType.MerchantRepublic);
		var network = new TradeNetwork();
		var route = network.Open(1, 2, 1);
		Assert.NotNull(route);
		route!.Volume = 20f;

		var open = Economy.Tick(state, Input(1, "Тир", cities: 3, partner: Partner(2, adjacent: true, cities: 3)),
			p, network, 10, Rng(1), null);
		Assert.True(open.TradeIncome > 0f, "открытый путь приносит прибыль");

		route.Blocked = true;
		route.Volume = 20f;
		var blocked = Economy.Tick(state, Input(1, "Тир", cities: 3, partner: Partner(2, adjacent: true, cities: 3, atWar: true)),
			p, network, 11, Rng(1), null);
		Assert.Equal(0f, blocked.TradeIncome);
	}

	[Fact]
	public void Economy_TradeIncomeGrowsWithVolumeAndLevel()
	{
		var p = new SimParams();
		var partners = new Dictionary<int, EconomyPartner> { { 2, Partner(2, adjacent: true, cities: 3) } };
		var input = Input(1, "Тир", cities: 3, partner: Partner(2, adjacent: true, cities: 3));
		var eco = new EconomyState();

		var network = new TradeNetwork();
		var route = network.Open(1, 2, 1)!;
		route.Volume = 10f;
		float small = Economy.TradeIncome(State(1, IdeologyType.Monarchy), eco, input, partners, network, p);

		route.Volume = 40f;
		float big = Economy.TradeIncome(State(1, IdeologyType.Monarchy), eco, input, partners, network, p);

		route.Level = 3;
		float upgraded = Economy.TradeIncome(State(1, IdeologyType.Monarchy), eco, input, partners, network, p);

		Assert.True(big > small * 3f, $"объём важнее всего: {big} vs {small}");
		Assert.True(upgraded > big, "повышение уровня увеличивает прибыль");
	}

	[Fact]
	public void Economy_TradeIncomeIsCappedByTaxes()
	{
		var p = new SimParams { TradeIncomeCapOfTaxes = 1.5f, TradeVolumeCap = 1000f };
		var state = State(1, IdeologyType.Monarchy);
		var network = new TradeNetwork();
		var route = network.Open(1, 2, 1)!;
		route.Volume = 500f;
		route.Level = 3;

		var report = Economy.Tick(state, Input(1, "Тир", cities: 2, partner: Partner(2, adjacent: true, cities: 3)),
			p, network, 10, Rng(1), null);

		Assert.True(report.Taxes > 0f);
		Assert.True(report.TradeIncome <= report.Taxes * 1.5f + 0.001f,
			$"торговля не должна превышать налоги больше дозволенного: {report.TradeIncome} vs {report.Taxes}");
	}

	[Fact]
	public void Economy_VolumeCapDoesNotScaleWithLevel()
	{
		var p = new SimParams { TradeVolumeCap = 30f, TradeLevelUpYears = 1000 };
		var state = State(1, IdeologyType.MerchantRepublic);
		var network = new TradeNetwork();
		var route = network.Open(1, 2, 1)!;
		route.Volume = 29f;
		route.Level = 3;

		Economy.Tick(state, Input(1, "Тир", cities: 3, partner: Partner(2, adjacent: true, cities: 3)),
			p, network, 10, Rng(1), null);

		Assert.True(route.Volume <= 30f, $"потолок объёма один на все уровни: {route.Volume}");
	}

	[Fact]
	public void Economy_TariffRaisesOwnProfitAndChokesVolume()
	{
		var p = new SimParams();
		var partners = new Dictionary<int, EconomyPartner> { { 2, Partner(2, adjacent: true, cities: 3) } };
		var input = Input(1, "Тир", cities: 3, partner: Partner(2, adjacent: true, cities: 3));

		var network = new TradeNetwork();
		var route = network.Open(1, 2, 1)!;
		route.Volume = 20f;

		float free = Economy.TradeIncome(State(1, IdeologyType.Monarchy), new EconomyState { Tariff = 0f }, input, partners, network, p);
		float taxed = Economy.TradeIncome(State(1, IdeologyType.Monarchy), new EconomyState { Tariff = 0.4f }, input, partners, network, p);

		Assert.True(taxed > free, "своя пошлина увеличивает сбор");

		// Пошлина партнёра бьёт по нашей прибыли:
		var taxedPartner = new Dictionary<int, EconomyPartner> { { 2, Partner(2, adjacent: true, cities: 3, tariff: 0.4f) } };
		float choked = Economy.TradeIncome(State(1, IdeologyType.Monarchy), new EconomyState(), input, taxedPartner, network, p);
		Assert.True(choked < free, "чужая пошлина режет прибыль");
	}

	[Fact]
	public void Economy_WarStopsTradeEntirely()
	{
		var p = new SimParams();
		var state = State(1, IdeologyType.MerchantRepublic);
		var network = new TradeNetwork();
		network.Open(1, 2, 1)!.Volume = 30f;

		var report = Economy.Tick(state, Input(1, "Тир", cities: 3, atWar: true, partner: Partner(2, adjacent: true, cities: 3, atWar: true)),
			p, network, 10, Rng(1), null);

		Assert.Equal(0f, report.TradeIncome);
		Assert.True(network.Routes[0].Blocked, "война перекрывает путь");
	}

	[Fact]
	public void Economy_PeaceReopensBlockedRoute()
	{
		var p = new SimParams();
		var state = State(1, IdeologyType.Monarchy);
		var network = new TradeNetwork();
		var route = network.Open(1, 2, 1)!;
		route.Blocked = true;
		route.Volume = 20f;

		Economy.Tick(state, Input(1, "Тир", cities: 3, partner: Partner(2, adjacent: true, cities: 3)), p, network, 10, Rng(1), null);

		Assert.False(route.Blocked);
	}

	[Fact]
	public void Economy_EmbargoBlocksRouteWithTargetOnly()
	{
		var p = new SimParams();
		var state = State(1, IdeologyType.Militarism);
		state.Economy = new EconomyState { EmbargoTargetId = 2, EmbargoUntilYear = 30 };
		var network = new TradeNetwork();
		network.Open(1, 2, 1)!.Volume = 20f;
		network.Open(1, 3, 1)!.Volume = 20f;

		var report = Economy.Tick(state, Input(1, "Тир", cities: 3,
			partner: Partner(2, adjacent: true, cities: 3), partner2: Partner(3, adjacent: true, cities: 3)), p, network, 10, Rng(1), null);

		Assert.True(network.Find(1, 2)!.Blocked, "путь с целью эмбарго перекрыт");
		Assert.False(network.Find(1, 3)!.Blocked, "остальные пути работают");
		Assert.True(report.TradeIncome > 0f);
	}

	[Fact]
	public void Economy_RouteOpensOnlyForAdjacentPeacefulNeighbor()
	{
		var p = new SimParams { TradeRouteChance = 1f };

		var far = new TradeNetwork();
		Economy.Tick(State(1, IdeologyType.MerchantRepublic), Input(1, "Тир", cities: 3, partner: Partner(2, adjacent: false, cities: 3)),
			p, far, 10, Rng(1), null);
		Assert.Empty(far.Routes);

		var enemy = new TradeNetwork();
		Economy.Tick(State(1, IdeologyType.MerchantRepublic), Input(1, "Тир", cities: 3, partner: Partner(2, adjacent: true, cities: 3, atWar: true)),
			p, enemy, 10, Rng(1), null);
		Assert.Empty(enemy.Routes);

		var tiny = new TradeNetwork();
		Economy.Tick(State(1, IdeologyType.MerchantRepublic), Input(1, "Тир", cities: 3, partner: Partner(2, adjacent: true, cities: 1)),
			p, tiny, 10, Rng(1), null);
		Assert.Empty(tiny.Routes);

		var ok = new TradeNetwork();
		var bus = new PolEventBus();
		Economy.Tick(State(1, IdeologyType.MerchantRepublic), Input(1, "Тир", cities: 3, partner: Partner(2, adjacent: true, cities: 3)),
			p, ok, 10, Rng(1), bus);
		Assert.Single(ok.Routes);
		var evt = bus.Log.First(e => e.Type == PolEventType.TradeRouteOpened);
		Assert.Equal("Тир", evt.Params["kingdom"]);
		Assert.Equal("Партнёр 2", evt.Params["partner"]);
	}

	[Fact]
	public void Economy_RouteLevelUpAfterYearsAndVolume()
	{
		var p = new SimParams { TradeLevelUpYears = 5, TradeLevelUpVolume = 5f, TradeVolumeCap = 100f };
		var state = State(1, IdeologyType.MerchantRepublic);
		var network = new TradeNetwork();
		var route = network.Open(1, 2, 1)!;
		route.Volume = 40f;
		var bus = new PolEventBus();

		var report = Economy.Tick(state, Input(1, "Тир", cities: 3, partner: Partner(2, adjacent: true, cities: 3)),
			p, network, 10, Rng(1), bus);

		Assert.Equal(2, route.Level);
		Assert.Equal(1, report.LevelUps);
		Assert.Contains(bus.Log, e => e.Type == PolEventType.TradeRouteUpgraded);
	}

	[Fact]
	public void TradeNetwork_DropAndRemapKingdom()
	{
		var network = new TradeNetwork();
		network.Open(1, 2, 1);
		network.Open(1, 3, 1);
		network.Open(2, 3, 1);

		// Держава 3 участвует в двух путях (1-3 и 2-3) — оба снимаются:
		Assert.Equal(2, network.DropKingdom(3));
		Assert.Equal(1, network.Count);
		Assert.Null(network.Find(1, 3));
		Assert.NotNull(network.Find(1, 2));

		network.RemapKingdom(2, 42);
		Assert.NotNull(network.Find(1, 42));
		Assert.Null(network.Find(1, 2));
	}

	// ------------------------------------------------------------------ благосостояние и настроение

	[Fact]
	public void Economy_ProsperityRisesWithProfitAndFallsInWar()
	{
		var p = new SimParams { RecessionChance = 0f };

		var peaceful = State(1, IdeologyType.Monarchy);
		peaceful.Economy = new EconomyState { Prosperity = 50f };
		Economy.Tick(peaceful, Input(1, "Тир", cities: 4, warriors: 10), p, new TradeNetwork(), 10, Rng(1), null);

		var warring = State(2, IdeologyType.Monarchy);
		warring.Economy = new EconomyState { Prosperity = 50f };
		Economy.Tick(warring, Input(2, "Война", cities: 4, warriors: 10, atWar: true), p, new TradeNetwork(), 10, Rng(1), null);

		Assert.True(peaceful.Economy!.Prosperity > 50f, $"мир и прибыль растут: {peaceful.Economy.Prosperity}");
		Assert.True(warring.Economy!.Prosperity < 50f, $"война разрушает: {warring.Economy.Prosperity}");
	}

	[Fact]
	public void Economy_ProsperityConvergesAndDoesNotSaturate()
	{
		var p = new SimParams { RecessionChance = 0f };
		var state = State(1, IdeologyType.Monarchy);
		state.Economy = new EconomyState { Prosperity = 50f };
		var network = new TradeNetwork();

		// 200 мирных лет без торговли: благосостояние выходит на базовый уровень и стоит.
		for (int year = 1; year <= 200; year++)
		{
			Economy.Tick(state, Input(1, "Тир", cities: 4), p, network, year, Rng(year), null);
		}

		Assert.InRange(state.Economy!.Prosperity, p.ProsperityBaseline - 5f, p.ProsperityBaseline + 5f);
		Assert.True(state.Economy.Prosperity < 90f, $"благосостояние не должно упираться в потолок: {state.Economy.Prosperity}");
	}

	[Fact]
	public void Economy_BoomRequiresTradeRoutes()
	{
		var p = new SimParams { RecessionChance = 0f, BoomProsperity = 70f, BoomMinRoutes = 2 };
		var state = State(1, IdeologyType.Monarchy);
		state.Economy = new EconomyState { Prosperity = 90f };

		// Благосостояние высокое, но путей нет — бума не будет:
		var report = Economy.Tick(state, Input(1, "Тир", cities: 4), p, new TradeNetwork(), 10, Rng(1), null);
		Assert.False(report.Boom);
	}

	[Fact]
	public void Economy_FamineHurtsProsperityHarderThanWar()
	{
		var p = new SimParams { RecessionChance = 0f };

		var starving = State(1, IdeologyType.Monarchy);
		starving.Economy = new EconomyState { Prosperity = 50f };
		Economy.Tick(starving, Input(1, "Голод", cities: 3, starving: true), p, new TradeNetwork(), 10, Rng(1), null);

		var warring = State(2, IdeologyType.Monarchy);
		warring.Economy = new EconomyState { Prosperity = 50f };
		Economy.Tick(warring, Input(2, "Война", cities: 3, atWar: true), p, new TradeNetwork(), 10, Rng(1), null);

		Assert.True(starving.Economy!.Prosperity < warring.Economy!.Prosperity,
			$"голод страшнее войны: {starving.Economy.Prosperity} vs {warring.Economy.Prosperity}");
	}

	[Fact]
	public void Economy_HighProsperityRaisesSupportAndLowersDissent()
	{
		var p = new SimParams { RecessionChance = 0f };
		var state = State(1, IdeologyType.Monarchy);
		state.Support = 50f;
		state.AvgDissent = 0.2f;
		state.Economy = new EconomyState { Prosperity = 90f };

		var report = Economy.Tick(state, Input(1, "Тир", cities: 4), p, new TradeNetwork(), 10, Rng(1), null);

		Assert.True(state.Support > 50f, $"сытая держава поддерживает власть: {state.Support}");
		Assert.True(state.AvgDissent < 0.2f, $"недовольство падает: {state.AvgDissent}");
		Assert.True(report.HappinessDelta > 0f);
	}

	// ------------------------------------------------------------------ кризисы

	[Fact]
	public void Economy_DeficitForYearsEndsInBankruptcy()
	{
		var p = new SimParams { BankruptcyDeficitYears = 3, RecessionChance = 0f };
		var state = State(1, IdeologyType.Monarchy);
		state.Economy = new EconomyState { DeficitYears = 3, Prosperity = 40f };
		var bus = new PolEventBus();

		// Много воинов и мало городов — расход гарантированно превышает доход:
		var report = Economy.Tick(state, Input(1, "Тир", cities: 1, warriors: 200), p, new TradeNetwork(), 10, Rng(1), bus);

		Assert.Equal(EconomicCrisisType.Bankruptcy, state.Economy.Crisis);
		Assert.True(report.CrisisStarted);
		Assert.True(report.GoldDelta < 0f, "банкротство опустошает казну");
		Assert.True(state.Legitimacy < 60f, "дефолт бьёт по авторитету власти");
		Assert.Contains(bus.Log, e => e.Type == PolEventType.EconomicCrisisStarted);
	}

	[Fact]
	public void Economy_RecessionNotification_IsTrivialAndUsesTemplateKey()
	{
		// Рутинный спад не должен спамить всплывашками: Severity.Trivial при стандартном
		// уровне уведомлений уходит только в летопись. А ключ события — ключ шаблона
		// уведомления, а не ключ имени кризиса (иначе уведомление выродится в голое
		// «экономический спад» с маленькой буквы).
		var p = new SimParams { RecessionChance = 1f };
		var state = State(1, IdeologyType.MerchantRepublic);
		state.Economy = new EconomyState { Prosperity = 20f };
		var bus = new PolEventBus();

		Economy.Tick(state, Input(1, "Тир", cities: 3), p, new TradeNetwork(), 10, Rng(1), bus);

		Assert.Equal(EconomicCrisisType.Recession, state.Economy.Crisis);
		var evt = Assert.Single(bus.Log, e => e.Type == PolEventType.EconomicCrisisStarted);
		Assert.Equal("RP_EV_ECO_CRISIS_STARTED", evt.LocKey);
		Assert.Equal(Severity.Trivial, evt.Severity);
		// Имя кризиса — отдельным параметром (переводится слоем уведомлений/летописи):
		Assert.Equal("RP_ECO_CRISIS_RECESSION", evt.Get("crisis"));
	}

	[Fact]
	public void Economy_SevereCrisisNotification_IsMajor()
	{
		// Банкротство казны — серьёзный кризис: уведомление в игре обязательно.
		var p = new SimParams { BankruptcyDeficitYears = 3, RecessionChance = 0f };
		var state = State(1, IdeologyType.Monarchy);
		state.Economy = new EconomyState { DeficitYears = 3, Prosperity = 40f };
		var bus = new PolEventBus();

		Economy.Tick(state, Input(1, "Тир", cities: 1, warriors: 200), p, new TradeNetwork(), 10, Rng(1), bus);

		var evt = Assert.Single(bus.Log, e => e.Type == PolEventType.EconomicCrisisStarted);
		Assert.Equal(Severity.Major, evt.Severity);
		Assert.Equal("RP_EV_ECO_CRISIS_STARTED", evt.LocKey);
	}

	[Fact]
	public void Economy_CrisisHurtsSupportAndProsperity()
	{
		var p = new SimParams();
		var state = State(1, IdeologyType.MerchantRepublic);
		state.Support = 60f;
		state.Economy = new EconomyState { Prosperity = 60f, Crisis = EconomicCrisisType.Depression, CrisisUntilYear = 30 };

		var report = Economy.Tick(state, Input(1, "Тир", cities: 3), p, new TradeNetwork(), 10, Rng(1), null);

		Assert.True(state.Support < 60f, $"депрессия бьёт по поддержке: {state.Support}");
		Assert.True(state.Economy.Prosperity < 60f, $"депрессия разрушает благосостояние: {state.Economy.Prosperity}");
		Assert.True(report.HappinessDelta < 0f);
	}

	[Fact]
	public void Economy_CrisisEndsAndPublishesEvent()
	{
		var p = new SimParams();
		var state = State(1, IdeologyType.Monarchy);
		state.Economy = new EconomyState { Crisis = EconomicCrisisType.Recession, CrisisUntilYear = 10 };
		var bus = new PolEventBus();

		Economy.Tick(state, Input(1, "Тир", cities: 3), p, new TradeNetwork(), 10, Rng(1), bus);

		Assert.Equal(EconomicCrisisType.None, state.Economy.Crisis);
		var evt = Assert.Single(bus.Log, e => e.Type == PolEventType.EconomicCrisisEnded);
		// Окончание кризиса — только в летопись при стандартном уровне уведомлений:
		Assert.Equal(Severity.Trivial, evt.Severity);
	}

	[Fact]
	public void Economy_CrisisCooldownPreventsBackToBackCrises()
	{
		var p = new SimParams { RecessionChance = 1f, CrisisCooldownYears = 8 };
		var state = State(1, IdeologyType.MerchantRepublic);
		state.Economy = new EconomyState { LastCrisisYear = 7, Prosperity = 20f };

		var report = Economy.Tick(state, Input(1, "Тир", cities: 3), p, new TradeNetwork(), 10, Rng(1), null);

		Assert.False(report.CrisisStarted);
		Assert.Equal(EconomicCrisisType.None, state.Economy.Crisis);
	}

	[Fact]
	public void Economy_TradeCollapseWhenAllRoutesBlockedAndTradeMatters()
	{
		var p = new SimParams { TradeCollapseShare = 0.3f, RecessionChance = 0f };
		var state = State(1, IdeologyType.MerchantRepublic);
		state.Economy = new EconomyState { Prosperity = 50f, TradeShare = 0.6f };
		var network = new TradeNetwork();
		var route = network.Open(1, 2, 1)!;
		route.Volume = 20f;
		var bus = new PolEventBus();

		// Партнёр воюет с нами — путь перекрывается в этот же тик:
		Economy.Tick(state, Input(1, "Тир", cities: 3, partner: Partner(2, adjacent: true, cities: 3, atWar: true)),
			p, network, 10, Rng(1), bus);

		Assert.True(route.Blocked);
		Assert.Equal(EconomicCrisisType.TradeCollapse, state.Economy.Crisis);
	}

	[Fact]
	public void Economy_BoomNeedsProsperityAndGivesBonus()
	{
		var p = new SimParams { RecessionChance = 0f, BoomProsperity = 70f, BoomCooldownYears = 15, BoomMinRoutes = 2 };
		var state = State(1, IdeologyType.Monarchy);
		state.Economy = new EconomyState { Prosperity = 85f, LastBoomYear = 0 };
		var bus = new PolEventBus();
		// Бум — плод торговли: нужны работающие пути.
		var network = new TradeNetwork();
		network.Open(1, 2, 1)!.Volume = 15f;
		network.Open(1, 3, 1)!.Volume = 15f;

		var report = Economy.Tick(state, Input(1, "Тир", cities: 4,
			partner: Partner(2, adjacent: true, cities: 3), partner2: Partner(3, adjacent: true, cities: 3)),
			p, network, 10, Rng(1), bus);

		Assert.True(report.Boom);
		Assert.Equal(1, state.Economy.BoomsTotal);
		Assert.True(report.GoldDelta >= p.BoomGoldBonus, $"бум пополняет казну: {report.GoldDelta}");
		Assert.Contains(bus.Log, e => e.Type == PolEventType.EconomicBoom);
	}

	[Fact]
	public void Economy_BoomNotTwiceInCooldown()
	{
		var p = new SimParams { RecessionChance = 0f, BoomProsperity = 70f, BoomCooldownYears = 15 };
		var state = State(1, IdeologyType.Monarchy);
		state.Economy = new EconomyState { Prosperity = 90f, LastBoomYear = 8 };

		var report = Economy.Tick(state, Input(1, "Тир", cities: 4), p, new TradeNetwork(), 10, Rng(1), null);

		Assert.False(report.Boom);
	}

	[Fact]
	public void Economy_AusterityAndEmbargoExpireByYear()
	{
		var p = new SimParams { RecessionChance = 0f };
		var state = State(1, IdeologyType.Monarchy);
		state.Economy = new EconomyState { Austerity = true, AusterityUntilYear = 5, EmbargoTargetId = 2, EmbargoUntilYear = 5 };

		Economy.Tick(state, Input(1, "Тир", cities: 3, partner: Partner(2, adjacent: true, cities: 3)), p, new TradeNetwork(), 10, Rng(1), null);

		Assert.False(state.Economy.Austerity);
		Assert.Equal(0, state.Economy.EmbargoTargetId);
	}

	// ------------------------------------------------------------------ мир и планировщик

	[Fact]
	public void World_YearTickStoresEconomyReportAndAppliesEffect()
	{
		var world = new PoliticalWorld { Rng = Rng(7) };
		world.Params.EnableTrade = false;
		world.AddKingdom(1, IdeologyType.MerchantRepublic, 1);
		world.AddKingdom(2, IdeologyType.Monarchy, 1);

		EconomyReport? captured = null;
		world.EconomyEffectApplier = (state, report) =>
		{
			captured = report;
			return true;
		};

		world.YearTick(new List<KingdomInput>
		{
			WorldInput(1, "Тирград", cities: 3, neighbors: 2),
			WorldInput(2, "Королевство", cities: 3, neighbors: 1)
		});

		var report = world.LastEconomyReport(1);
		Assert.NotNull(report);
		Assert.True(report!.Taxes > 0f, "налоги начислены");
		Assert.NotNull(world.GetKingdom(1)!.Economy);
		Assert.NotNull(captured);
	}

	[Fact]
	public void World_EconomyEffectApplierFailureDoesNotBreakTick()
	{
		var world = new PoliticalWorld { Rng = Rng(3) };
		world.AddKingdom(1, IdeologyType.Monarchy, 1);
		world.EconomyEffectApplier = (state, report) => throw new InvalidOperationException("игровое API недоступно");

		int events = world.YearTick(new List<KingdomInput> { WorldInput(1, "Тир", cities: 2) });

		Assert.True(events >= 0);
		Assert.NotNull(world.LastEconomyReport(1));
	}

	[Fact]
	public void World_RemoveKingdomDropsItsRoutes()
	{
		var world = new PoliticalWorld { Rng = Rng(5) };
		world.AddKingdom(1, IdeologyType.Monarchy, 1);
		world.AddKingdom(2, IdeologyType.Monarchy, 1);
		world.Trade.Open(1, 2, 1);

		world.RemoveKingdom(2);

		Assert.Empty(world.Trade.Routes);
		Assert.Null(world.LastEconomyReport(2));
	}

	[Fact]
	public void Planner_PrefersAusterityWhenTreasuryBleeds()
	{
		var plan = KingdomStrategicPlanner.Choose(new KingdomStrategySnapshot
		{
			KingdomId = 1,
			EconomyEnabled = true,
			DeficitYears = 3,
			Gold = 200f,
			Food = 500f,
			Warriors = 20,
			MaxWarriors = 40,
			Cities = 3,
			Legitimacy = 70f,
			Population = 100
		});

		Assert.Equal(KingdomActionType.Austerity, plan.Action);
	}

	[Fact]
	public void Planner_MerchantRepublicWantsTradeRoute()
	{
		var plan = KingdomStrategicPlanner.Choose(new KingdomStrategySnapshot
		{
			KingdomId = 1,
			Ideology = IdeologyType.MerchantRepublic,
			EconomyEnabled = true,
			Gold = 300f,
			Food = 500f,
			Warriors = 20,
			MaxWarriors = 40,
			Cities = 4,
			Legitimacy = 75f,
			Population = 120,
			TradeRoutes = 0,
			MaxTradeRoutes = 6,
			DeficitYears = 0
		});

		Assert.Equal(KingdomActionType.DevelopTrade, plan.Action);
	}

	[Fact]
	public void Planner_AntiTradeEthnonationalism_DoesNotSeekTrade()
	{
		// В 0.23 изоляционизм упразднён (его ниша — либерализм, который, наоборот,
		// склонен к торговле). Антиторговый строй теперь — этнонационализм:
		// при тех же возможностях он предпочитает рост торговым путям.
		var plan = KingdomStrategicPlanner.Choose(new KingdomStrategySnapshot
		{
			KingdomId = 1,
			Ideology = IdeologyType.Ethnonationalism,
			EconomyEnabled = true,
			Gold = 300f,
			Food = 500f,
			Warriors = 20,
			MaxWarriors = 40,
			Cities = 4,
			Legitimacy = 75f,
			Population = 120,
			TradeRoutes = 0,
			MaxTradeRoutes = 6
		});

		Assert.NotEqual(KingdomActionType.DevelopTrade, plan.Action);
	}

	[Fact]
	public void Planner_LiberalismFavorsTrade_UnlikeEthnonationalism()
	{
		KingdomStrategySnapshot Base(IdeologyType ideo) => new KingdomStrategySnapshot
		{
			KingdomId = 1,
			Ideology = ideo,
			EconomyEnabled = true,
			Gold = 300f,
			Food = 500f,
			Warriors = 20,
			MaxWarriors = 40,
			Cities = 4,
			Legitimacy = 75f,
			Population = 120,
			TradeRoutes = 0,
			MaxTradeRoutes = 6
		};

		var liberal = KingdomStrategicPlanner.Choose(Base(IdeologyType.Liberalism));
		var ethno = KingdomStrategicPlanner.Choose(Base(IdeologyType.Ethnonationalism));

		Assert.Equal(KingdomActionType.DevelopTrade, liberal.Action);
		Assert.NotEqual(KingdomActionType.DevelopTrade, ethno.Action);
	}

	[Fact]
	public void Planner_MilitarismConsidersEmbargo()
	{
		var plan = KingdomStrategicPlanner.Choose(new KingdomStrategySnapshot
		{
			KingdomId = 1,
			Ideology = IdeologyType.Militarism,
			EconomyEnabled = true,
			EmbargoCandidate = true,
			Gold = 300f,
			Food = 500f,
			Warriors = 40,
			MaxWarriors = 40,
			Cities = 4,
			Legitimacy = 75f,
			Population = 120,
			TradeRoutes = 6,
			MaxTradeRoutes = 6
		});

		Assert.Equal(KingdomActionType.TradeEmbargo, plan.Action);
	}

	// ------------------------------------------------------------------ связь с мятежами

	[Fact]
	public void Economy_DepressionAddsRebellionCauseAndRaisesChance()
	{
		var p = new SimParams();
		var calm = WeakState(1);
		var starving = WeakState(1);
		starving.Economy = new EconomyState { Crisis = EconomicCrisisType.Depression, Prosperity = 15f };

		var input = WorldInput(1, "Тир", cities: 4);
		var calmWeights = CivilWars.CauseWeights(calm, input, p, 50);
		var sickWeights = CivilWars.CauseWeights(starving, input, p, 50);

		Assert.False(calmWeights.ContainsKey(CivilWarCause.EconomicCrisis));
		Assert.True(sickWeights.ContainsKey(CivilWarCause.EconomicCrisis));
		Assert.True(CivilWars.StartChance(starving, input, p) > CivilWars.StartChance(calm, input, p),
			"депрессия повышает риск мятежа");
	}

	[Fact]
	public void Economy_RecessionIsNotEnoughForRebellionCause()
	{
		var p = new SimParams();
		var state = WeakState(1);
		state.Economy = new EconomyState { Crisis = EconomicCrisisType.Recession, Prosperity = 40f };

		Assert.False(CivilWars.CauseWeights(state, WorldInput(1, "Тир", cities: 4), p, 50).ContainsKey(CivilWarCause.EconomicCrisis));
	}

	// ------------------------------------------------------------------ сохранение

	[Fact]
	public void SaveData_RoundTripsEconomyAndTradeRoutes()
	{
		var source = new PoliticalWorld();
		KingdomPolitics kingdom = source.AddKingdom(7, IdeologyType.MerchantRepublic, 3);
		kingdom.Economy = new EconomyState
		{
			Prosperity = 73.5f,
			TaxRate = 1.25f,
			Tariff = 0.2f,
			DeficitYears = 2,
			Crisis = EconomicCrisisType.Recession,
			CrisisUntilYear = 40,
			LastCrisisYear = 38,
			CrisesTotal = 3,
			BoomsTotal = 2,
			LastBoomYear = 20,
			Austerity = true,
			AusterityUntilYear = 45,
			EmbargoTargetId = 9,
			EmbargoUntilYear = 50,
			TradeShare = 0.42f,
			LastIncome = 88f,
			LastExpenses = 51f
		};
		source.AddKingdom(9, IdeologyType.Militarism, 3);
		source.Trade.Open(7, 9, 12)!.Volume = 33f;

		string json = SaveData.Write(source);
		var restored = new PoliticalWorld();
		SaveData.Apply(restored, json);

		var eco = restored.GetKingdom(7)!.Economy;
		Assert.NotNull(eco);
		Assert.Equal(73.5f, eco!.Prosperity, 3);
		Assert.Equal(1.25f, eco.TaxRate, 3);
		Assert.Equal(0.2f, eco.Tariff, 3);
		Assert.Equal(2, eco.DeficitYears);
		Assert.Equal(EconomicCrisisType.Recession, eco.Crisis);
		Assert.Equal(40, eco.CrisisUntilYear);
		Assert.Equal(3, eco.CrisesTotal);
		Assert.Equal(2, eco.BoomsTotal);
		Assert.True(eco.Austerity);
		Assert.Equal(9, eco.EmbargoTargetId);
		Assert.Equal(0.42f, eco.TradeShare, 3);

		Assert.Single(restored.Trade.Routes);
		var route = restored.Trade.Find(7, 9);
		Assert.NotNull(route);
		Assert.Equal(33f, route!.Volume, 3);
		Assert.Equal(12, route.EstablishedYear);
	}

	[Fact]
	public void SaveData_OldFormatWithoutEconomyKeepsDefaults()
	{
		var source = new PoliticalWorld();
		source.AddKingdom(4, IdeologyType.Monarchy, 1);
		string json = SaveData.Write(source)
			.Replace("\"eco\"", "\"eco_unused\"")
			.Replace("\"routes\"", "\"routes_unused\"");

		var restored = new PoliticalWorld();
		SaveData.Apply(restored, json);

		Assert.Null(restored.GetKingdom(4)!.Economy);
		Assert.Empty(restored.Trade.Routes);
	}

	[Fact]
	public void SaveData_FormatVersionIsSeventeen()
	{
		Assert.Equal(18, SaveData.FormatVersion);
	}

	// ------------------------------------------------------------------ хелперы

	private static Random Rng(int seed) => new Random(seed);

	private static KingdomPolitics State(int id, IdeologyType ideology, float legitimacy = 60f)
	{
		return new KingdomPolitics
		{
			KingdomId = id,
			Ideology = ideology,
			Legitimacy = legitimacy,
			Support = 50f,
			AvgDissent = 0.1f,
			FoundedYear = 1
		};
	}

	/// <summary>Слабое государство для проверки причин мятежа (как в CivilWarTests).</summary>
	private static KingdomPolitics WeakState(int id)
	{
		return new KingdomPolitics
		{
			KingdomId = id,
			Ideology = IdeologyType.Monarchy,
			Legitimacy = 15f,
			Support = 20f,
			AvgDissent = 0.65f,
			FoundedYear = 1,
			Starving = true
		};
	}

	/// <summary>Снимок державы для <see cref="PoliticalWorld.YearTick"/> и весов причин мятежа.</summary>
	private static KingdomInput WorldInput(int id, string name, int cities, params int[] neighbors)
	{
		var cityList = new List<CityInput>();
		for (int i = 0; i < cities; i++)
		{
			cityList.Add(new CityInput { CityId = id * 100 + i, KingdomId = id, Population = 40 });
		}
		var relations = new List<RelationInfo>();
		for (int i = 0; i < neighbors.Length; i++)
		{
			relations.Add(new RelationInfo { OtherId = neighbors[i], Relation = RelationType.Peace, Adjacent = true });
		}
		return new KingdomInput
		{
			KingdomId = id,
			Name = name,
			Population = cities * 40,
			Warriors = 10,
			Cities = cityList,
			Relations = relations
		};
	}

	private static EconomyPartner Partner(int id, bool adjacent, int cities, bool atWar = false, bool allied = false, float tariff = 0f)
	{
		return new EconomyPartner
		{
			KingdomId = id,
			Name = "Партнёр " + id,
			Adjacent = adjacent,
			Cities = cities,
			AtWar = atWar,
			Allied = allied,
			Tariff = tariff
		};
	}

	private static EconomyInput Input(int id, string name, int cities, int warriors = 10, bool atWar = false,
		bool starving = false, EconomyPartner partner = default, EconomyPartner partner2 = default)
	{
		var partners = new List<EconomyPartner>();
		if (partner.KingdomId != 0)
		{
			partners.Add(partner);
		}
		if (partner2.KingdomId != 0)
		{
			partners.Add(partner2);
		}
		return new EconomyInput
		{
			KingdomId = id,
			Name = name,
			Cities = cities,
			Population = cities * 40,
			Warriors = warriors,
			AtWar = atWar,
			Starving = starving,
			Ideology = IdeologyType.Monarchy,
			Legitimacy = 60f,
			AvgDissent = 0.1f,
			Support = 50f,
			Partners = partners
		};
	}
}
