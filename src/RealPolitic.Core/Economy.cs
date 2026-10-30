using System;
using System.Collections.Generic;

namespace RealPolitic.Core;

/// <summary>Сосед с точки зрения экономики: с ним либо торгуют, либо воюют.</summary>
public struct EconomyPartner
{
	public int KingdomId;

	public string Name;

	/// <summary>С державой идёт война — торговля невозможна.</summary>
	public bool AtWar;

	/// <summary>Союз или уния: торговля идёт лучше.</summary>
	public bool Allied;

	/// <summary>Множитель прибыли за отношения с партнёром (союз, тип унии).
	/// Ноль означает «не задан» — тогда берётся <see cref="Allied"/> и обычный бонус союза:
	/// структура не умеет иметь значение по умолчанию, и старые сохранённые снимки остаются рабочими.</summary>
	public float TradeMultiplier;

	public bool Adjacent;

	public int Cities;

	/// <summary>Пошлина партнёра на наших купцов.</summary>
	public float Tariff;
}

/// <summary>Всё, что нужно экономике державы за один год.</summary>
public struct EconomyInput
{
	public int KingdomId;

	public string Name;

	public int Cities;

	public int Population;

	public int Warriors;

	public bool AtWar;

	public bool Starving;

	/// <summary>Реальная казна (золото игры); -1 = неизвестно (тогда судим только по дефициту).</summary>
	public float Gold;

	public IdeologyType Ideology;

	public float Legitimacy;

	public float AvgDissent;

	public float Support;

	public bool CivilWarActive;

	public List<EconomyPartner> Partners;

	public static readonly EconomyInput Empty = default(EconomyInput);
}

/// <summary>Что экономика сделала за год: это читает игровой слой, чтобы применить эффект в мире.</summary>
public sealed class EconomyReport
{
	public float Taxes { get; set; }

	public float TradeIncome { get; set; }

	public float Income { get; set; }

	public float Expenses { get; set; }

	public float Net { get; set; }

	/// <summary>Сколько золота добавить (или отнять, если отрицательно) в мире.</summary>
	public float GoldDelta { get; set; }

	/// <summary>Продовольствие из торговли: заполняется только в голодный год.</summary>
	public float FoodDelta { get; set; }

	/// <summary>Влияние на настроение жителей (применяется к городам).</summary>
	public float HappinessDelta { get; set; }

	public int OpenRoutes { get; set; }

	public int BlockedRoutes { get; set; }

	public bool CrisisStarted { get; set; }

	public EconomicCrisisType Crisis { get; set; } = EconomicCrisisType.None;

	public bool Boom { get; set; }

	public int RoutesOpened { get; set; }

	public int LevelUps { get; set; }
}

/// <summary>Экономика державы: налоги, торговля, благосостояние, дефицит, кризисы и бума.</summary>
public static class Economy
{
	/// <summary>Годовой ход экономики. Чистая функция состояния: игру меняет только отчёт.</summary>
	public static EconomyReport Tick(KingdomPolitics state, EconomyInput input, SimParams p, TradeNetwork network, int year, Random rng, PolEventBus? bus)
	{
		if (state == null)
		{
			throw new ArgumentNullException(nameof(state));
		}
		if (p == null)
		{
			throw new ArgumentNullException(nameof(p));
		}
		rng ??= new Random(1);
		EconomyReport report = new EconomyReport();
		if (!p.EnableEconomy)
		{
			return report;
		}
		EconomyState eco = EnsureState(state);
		// Доля торговли прошлого года: в этом тике она ещё не посчитана,
		// а для «краха торговли» важно, насколько держава зависела от путей.
		float previousTradeShare = eco.TradeShare;
		int cities = Math.Max(0, input.Cities);
		int taxUnits = cities > 0 ? Math.Max(cities, input.Population / 40) : 0;
		Dictionary<int, EconomyPartner> partners = IndexPartners(input);

		SyncRouteBlocks(state, eco, input, network, year, bus);
		if (p.EnableTrade)
		{
			TickTradeVolume(state, eco, input, network, p, year, bus, report);
			MaybeOpenRoute(state, eco, input, partners, network, p, year, rng, bus, report);
		}

		report.Taxes = Taxes(taxUnits, eco, p, input.Ideology) * ConstitutionEffects.TaxMultiplier(state.Constitution?.Taxes ?? TaxLaw.Moderate, ConstitutionEffects.IsBinding(state));
		report.TradeIncome = p.EnableTrade ? TradeIncome(state, eco, input, partners, network, p) : 0f;
		// 0.24: статья VIII «О купечестве и пошлинах» правит торговую прибыль.
		report.TradeIncome *= ConstitutionEffects.TradeIncomeMultiplier(state.Constitution?.Trade ?? TradeLaw.Moderate, ConstitutionEffects.IsBinding(state));
		// Торговля дополняет казну, а не заменяет её: потолок — доля от налогов.
		float tradeCap = report.Taxes * p.TradeIncomeCapOfTaxes;
		if (report.TradeIncome > tradeCap)
		{
			report.TradeIncome = tradeCap;
		}
		report.Income = report.Taxes + report.TradeIncome;
		report.Expenses = Expenses(input, eco, p, input.AtWar || input.CivilWarActive,
			RulerTraits.ExpenseMultiplier(state.Personality?.Trait ?? RulerTrait.None)
			* ConstitutionEffects.LevyUpkeepMultiplier(state.Constitution?.Levy ?? LevyLaw.Conscription, ConstitutionEffects.IsBinding(state)));
		// 0.24: статья XII «О тайной страже» — сыскная служба стоит казне ежегодных денег.
		report.Expenses += ConstitutionEffects.WatchGoldCost(state.Constitution?.Watch ?? WatchLaw.Liberty, ConstitutionEffects.IsBinding(state));
		report.Net = report.Income - report.Expenses;
		report.GoldDelta = Clamp(report.Net, -120f, 160f);
		if (input.Starving && report.TradeIncome > 0f)
		{
			// В голодный год торговля кормит: часть торговой прибыли идёт на закупку хлеба.
			report.FoodDelta = report.TradeIncome * p.TradeFoodShare;
		}
		report.OpenRoutes = network?.OpenRoutesOf(input.KingdomId) ?? 0;
		report.BlockedRoutes = (network?.Of(input.KingdomId).Count ?? 0) - report.OpenRoutes;
		eco.TradeShare = report.Income > 0f ? report.TradeIncome / report.Income : 0f;
		eco.LastIncome = report.Income;
		eco.LastExpenses = report.Expenses;
		eco.DeficitYears = (report.Net < 0f) ? (eco.DeficitYears + 1) : 0;

		TickCrisis(state, eco, input, p, year, rng, bus, report, network, previousTradeShare);
		UpdateProsperity(eco, input, report, p);
		ApplyMood(state, eco, input, report, p);
		TickBoom(state, eco, input, p, year, bus, report);
		ExpireTimers(eco, year);
		return report;
	}

	/// <summary>Состояние экономики создаётся лениво: старые сохранения грузятся без него.</summary>
	public static EconomyState EnsureState(KingdomPolitics state)
	{
		if (state.Economy == null)
		{
			state.Economy = new EconomyState();
		}
		return state.Economy;
	}

	/// <summary>Налоговый сбор: города × база × ставка × благосостояние × отношение строя к налогам.</summary>
	public static float Taxes(int cities, EconomyState eco, SimParams p, IdeologyType ideology)
	{
		if (cities <= 0)
		{
			return 0f;
		}
		float prosperity = Clamp(eco.Prosperity, 0f, 100f);
		return cities * p.EconomyTaxBase * Clamp(eco.TaxRate, p.TaxRateMin, p.TaxRateMax)
			* (0.6f + prosperity / 100f * 0.8f) * TaxBias(ideology);
	}

	/// <summary>Прибыль со всех открытых путей: объём × уровень × пошлины обеих сторон.</summary>
	public static float TradeIncome(KingdomPolitics state, EconomyState eco, EconomyInput input, Dictionary<int, EconomyPartner> partners, TradeNetwork network, SimParams p)
	{
		if (network == null)
		{
			return 0f;
		}
		if (input.AtWar && !p.TradeDuringWar)
		{
			// Воюющая держава не торгует вовсе: купцы не ходят через линию фронта.
			return 0f;
		}
		float total = 0f;
		List<TradeRoute> list = network?.Of(input.KingdomId);
		if (list == null)
		{
			return 0f;
		}
		for (int i = 0; i < list.Count; i++)
		{
			TradeRoute route = list[i];
			if (route.Blocked || route.Volume <= 0f)
			{
				continue;
			}
			EconomyPartner partner;
			partners.TryGetValue(route.OtherSide(input.KingdomId), out partner);
			float partnerTariff = Clamp(partner.Tariff, 0f, p.TariffMax);
			float profit = route.Volume * p.TradeBaseProfit * route.Level
				* (1f + Clamp(eco.Tariff, 0f, p.TariffMax))
				* (1f - partnerTariff * 0.6f);
			// Союз даёт плоский бонус, уния — свой по типу (федерация и полная уния торгуют лучше вассалитета).
			float multiplier = partner.TradeMultiplier > 0f
				? partner.TradeMultiplier
				: (partner.Allied ? 1f + p.AlliedTradeBonus : 1f);
			profit *= multiplier;
			if (route.FromKingdomId == input.KingdomId)
			{
				route.LastProfitFrom = profit;
			}
			else
			{
				route.LastProfitTo = profit;
			}
			total += profit;
		}
		return total;
	}

	/// <summary>Расходы: содержание армии и управление, война дороже, экономия урезает.</summary>
	public static float Expenses(EconomyInput input, EconomyState eco, SimParams p, bool atWar, float personalityMultiplier = 1f)
	{
		float num = Math.Max(0, input.Warriors) * p.ArmyUpkeepPerWarrior * (atWar ? 1f + p.WarUpkeepExtra : 1f);
		num += Math.Max(0, input.Cities) * p.AdminCostPerCity;
		if (eco.Austerity)
		{
			num *= 1f - p.AusteritySavings;
		}
		// Этап 19: характер правителя удешевляет или удорожает двор и армию.
		num *= personalityMultiplier;
		return num;
	}

	/// <summary>Отношение строя к сбору налогов: республики и технократия собирают легче племени.</summary>
	public static float TaxBias(IdeologyType ideology)
	{
		return ideology switch
		{
			IdeologyType.MerchantRepublic => 1.3f,
			IdeologyType.Technocracy => 1.2f,
			IdeologyType.Democracy => 1.15f,
			IdeologyType.Communism => 1f,
			IdeologyType.Monarchy => 1f,
			IdeologyType.Theocracy => 0.95f,
			IdeologyType.Aristocracy => 0.9f,
			IdeologyType.Militarism => 0.85f,
			IdeologyType.Ethnonationalism => 0.8f,
			IdeologyType.Tribalism => 0.6f,
			IdeologyType.Liberalism => 1.15f,
			IdeologyType.Empire => 1.15f,
			IdeologyType.Khaganate => 0.85f,
			IdeologyType.FreeTerritory => 0.70f,
			_ => 1f
		};
	}

	/// <summary>Склонность строя к кризисам: рынок нестабилен, племя и изоляционизм — устойчивы.</summary>
	public static float CrisisBias(IdeologyType ideology)
	{
		return ideology switch
		{
			IdeologyType.MerchantRepublic => 1.35f,
			IdeologyType.Democracy => 1.1f,
			IdeologyType.Aristocracy => 1.15f,
			IdeologyType.Technocracy => 1.05f,
			IdeologyType.Egalitarianism => 1.1f,
			IdeologyType.Tribalism => 0.7f,
			IdeologyType.Liberalism => 1.25f,
			IdeologyType.Communism => 0.9f,
			IdeologyType.Empire => 1.05f,
			IdeologyType.Khaganate => 0.85f,
			IdeologyType.FreeTerritory => 0.80f,
			_ => 1f
		};
	}

	/// <summary>Желание строить торговые пути: торговая республика рвётся в путь, изоляционизм — нет.</summary>
	public static float TradeBias(IdeologyType ideology)
	{
		return ideology switch
		{
			IdeologyType.MerchantRepublic => 3f,
			IdeologyType.Democracy => 1.8f,
			IdeologyType.Technocracy => 1.5f,
			IdeologyType.Aristocracy => 1.3f,
			IdeologyType.Egalitarianism => 1.2f,
			IdeologyType.Monarchy => 1f,
			IdeologyType.Theocracy => 0.9f,
			IdeologyType.Militarism => 0.7f,
			IdeologyType.Communism => 0.6f,
			IdeologyType.Ethnonationalism => 0.4f,
			IdeologyType.Liberalism => 2.5f,
			IdeologyType.Tribalism => 0.5f,
			IdeologyType.Empire => 1.3f,
			IdeologyType.Khaganate => 1.1f,
			IdeologyType.FreeTerritory => 0.9f,
			_ => 1f
		};
	}

	public static float Clamp(float value, float min, float max)
	{
		if (value < min)
		{
			return min;
		}
		if (value > max)
		{
			return max;
		}
		return value;
	}

	private static Dictionary<int, EconomyPartner> IndexPartners(EconomyInput input)
	{
		Dictionary<int, EconomyPartner> dictionary = new Dictionary<int, EconomyPartner>();
		if (input.Partners != null)
		{
			for (int i = 0; i < input.Partners.Count; i++)
			{
				EconomyPartner economyPartner = input.Partners[i];
				dictionary[economyPartner.KingdomId] = economyPartner;
			}
		}
		return dictionary;
	}

	/// <summary>Война перекрывает путь, мир — открывает. Держим сеть согласованной без внешних хуков.</summary>
	private static void SyncRouteBlocks(KingdomPolitics state, EconomyState eco, EconomyInput input, TradeNetwork network, int year, PolEventBus? bus)
	{
		if (network == null)
		{
			return;
		}
		List<TradeRoute> list = network.Of(input.KingdomId);
		for (int i = 0; i < list.Count; i++)
		{
			TradeRoute route = list[i];
			int num = route.OtherSide(input.KingdomId);
			bool blocked = (eco.EmbargoActive(year) && eco.EmbargoTargetId == num) || PartnerAtWar(input, num);
			if (blocked && !route.Blocked)
			{
				route.Blocked = true;
				bus?.Publish(new PolEvent(PolEventType.TradeRouteBroken, "RP_EV_TRADE_BROKEN")
				{
					Year = year,
					Severity = Severity.Minor,
					KingdomId = state.KingdomId
				}.With("kingdom", input.Name ?? string.Empty)
					.With("partner", PartnerName(input, num)));
			}
			else if (!blocked && route.Blocked)
			{
				route.Blocked = false;
			}
		}
	}

	private static string PartnerName(EconomyInput input, int partnerId)
	{
		if (input.Partners != null)
		{
			for (int i = 0; i < input.Partners.Count; i++)
			{
				if (input.Partners[i].KingdomId == partnerId && !string.IsNullOrEmpty(input.Partners[i].Name))
				{
					return input.Partners[i].Name;
				}
			}
		}
		return partnerId.ToString();
	}

	private static bool PartnerAtWar(EconomyInput input, int partnerId)
	{
		if (input.Partners == null)
		{
			return false;
		}
		for (int i = 0; i < input.Partners.Count; i++)
		{
			if (input.Partners[i].KingdomId == partnerId)
			{
				return input.Partners[i].AtWar;
			}
		}
		return false;
	}

	/// <summary>Объём растёт, пока путь открыт, и тает в блокаде; зрелый путь повышает уровень.</summary>
	private static void TickTradeVolume(KingdomPolitics state, EconomyState eco, EconomyInput input, TradeNetwork network, SimParams p, int year, PolEventBus? bus, EconomyReport report)
	{
		if (network == null)
		{
			return;
		}
		List<TradeRoute> list = network.Of(state.KingdomId);
		for (int i = 0; i < list.Count; i++)
		{
			TradeRoute route = list[i];
			if (route.Blocked)
			{
				route.Volume = Math.Max(0f, route.Volume - p.TradeBlockadeDecay);
				continue;
			}
			route.Volume = Math.Min(p.TradeVolumeCap, route.Volume + p.TradeVolumeGrowth * (1f + eco.Prosperity / 200f));
			if (route.Level < p.TradeMaxLevel && route.YearsActive(year) >= p.TradeLevelUpYears * route.Level
				&& route.Volume >= p.TradeLevelUpVolume * route.Level)
			{
				route.Level++;
				report.LevelUps++;
				bus?.Publish(new PolEvent(PolEventType.TradeRouteUpgraded, "RP_EV_TRADE_UPGRADED")
				{
					Year = year,
					Severity = Severity.Minor,
					KingdomId = state.KingdomId
				}.With("kingdom", input.Name ?? string.Empty)
					.With("partner", PartnerName(input, route.OtherSide(state.KingdomId)))
					.With("level", route.Level));
			}
		}
	}

	/// <summary>Новый путь: соседство, мир, хотя бы два города и желание строя торговать.</summary>
	private static void MaybeOpenRoute(KingdomPolitics state, EconomyState eco, EconomyInput input, Dictionary<int, EconomyPartner> partners, TradeNetwork network, SimParams p, int year, Random rng, PolEventBus? bus, EconomyReport report)
	{
		if (network == null || input.Partners == null || input.AtWar)
		{
			return;
		}
		if (network.OpenRoutesOf(input.KingdomId) >= p.MaxTradeRoutes)
		{
			return;
		}
		for (int i = 0; i < input.Partners.Count; i++)
		{
			EconomyPartner partner = input.Partners[i];
			if (partner.AtWar || !partner.Adjacent || partner.Cities < p.TradeMinCities)
			{
				continue;
			}
			if (eco.EmbargoActive(year) && eco.EmbargoTargetId == partner.KingdomId)
			{
				continue;
			}
			if (network.Find(input.KingdomId, partner.KingdomId) != null)
			{
				continue;
			}
			float num = p.TradeRouteChance * TradeBias(input.Ideology) * (partner.Allied ? 1f + p.AlliedTradeBonus : 1f);
			if (rng.NextDouble() < num)
			{
				TradeRoute route = network.Open(input.KingdomId, partner.KingdomId, year);
				if (route != null)
				{
					report.RoutesOpened++;
					bus?.Publish(new PolEvent(PolEventType.TradeRouteOpened, "RP_EV_TRADE_OPENED")
					{
						Year = year,
						Severity = Severity.Minor,
						KingdomId = state.KingdomId
					}.With("kingdom", input.Name ?? string.Empty)
						.With("partner", string.IsNullOrEmpty(partner.Name) ? partner.KingdomId.ToString() : partner.Name));
				}
			}
		}
	}

	private static void TickCrisis(KingdomPolitics state, EconomyState eco, EconomyInput input, SimParams p, int year, Random rng, PolEventBus? bus, EconomyReport report, TradeNetwork network, float previousTradeShare)
	{
		if (eco.InCrisis)
		{
			if (year >= eco.CrisisUntilYear)
			{
				// Спад может перерасти в депрессию, если держава так и не выровняла казну.
				if (eco.Crisis == EconomicCrisisType.Recession && eco.DeficitYears > 0 && rng.NextDouble() < p.DepressionEscalationChance)
				{
					StartCrisis(state, eco, input, EconomicCrisisType.Depression, p, year, rng, bus, report);
				}
				else
				{
					EndCrisis(state, eco, input, p, year, bus);
				}
			}
			return;
		}
		if (eco.LastCrisisYear > 0 && year - eco.LastCrisisYear < p.CrisisCooldownYears)
		{
			return;
		}
		// Банкротство — только когда казна реально пуста: годы дефицита И золота меньше порога.
		// Если казна неизвестна (input.Gold < 0f из-за недоступного API), не объявляем дефолт каждые 4 года автоматически.
		bool treasuryConfirmedEmpty = input.Gold >= 0f && input.Gold < p.BankruptcyGoldThreshold;
		bool unknownTreasurySevereCollapse = input.Gold < 0f && eco.DeficitYears >= p.BankruptcyDeficitYears * 2 && eco.Prosperity < 35f;
		if (eco.DeficitYears >= p.BankruptcyDeficitYears && (treasuryConfirmedEmpty || unknownTreasurySevereCollapse))
		{
			StartCrisis(state, eco, input, EconomicCrisisType.Bankruptcy, p, year, rng, bus, report);
			return;
		}
		int num = network?.Of(input.KingdomId).Count ?? 0;
		int num2 = network?.OpenRoutesOf(input.KingdomId) ?? 0;
		if (num > 0 && num2 == 0 && previousTradeShare >= p.TradeCollapseShare)
		{
			StartCrisis(state, eco, input, EconomicCrisisType.TradeCollapse, p, year, rng, bus, report);
			return;
		}
		float num3 = p.RecessionChance * (1f + eco.DeficitYears * 0.35f) * (1.6f - Clamp(eco.Prosperity, 0f, 100f) / 100f)
			* CrisisBias(input.Ideology) * (input.AtWar ? 1.4f : 1f) * (input.CivilWarActive ? 1.5f : 1f);
		if (rng.NextDouble() < num3)
		{
			StartCrisis(state, eco, input, EconomicCrisisType.Recession, p, year, rng, bus, report);
		}
	}

	private static void StartCrisis(KingdomPolitics state, EconomyState eco, EconomyInput input, EconomicCrisisType type, SimParams p, int year, Random rng, PolEventBus? bus, EconomyReport report)
	{
		int num = type switch
		{
			EconomicCrisisType.Recession => p.RecessionYears + rng.Next(0, 2),
			EconomicCrisisType.Depression => p.DepressionYears + rng.Next(0, 3),
			EconomicCrisisType.Bankruptcy => p.BankruptcyYears + rng.Next(0, 2),
			_ => p.RecessionYears
		};
		int deficitBefore = eco.DeficitYears;
        eco.Crisis = type;
		eco.CrisisUntilYear = year + num;
		eco.LastCrisisYear = year;
		eco.CrisesTotal++;
		eco.DeficitYears = 0;
		if (type == EconomicCrisisType.Bankruptcy)
		{
			// Дефолт: казна пуста, доверие подданных подорвано.
			report.GoldDelta -= p.BankruptcyGoldLoss;
			state.Legitimacy = Clamp(state.Legitimacy - p.BankruptcyLegitimacyLoss, 0f, 100f);
		}
		report.CrisisStarted = true;
		report.Crisis = type;
		// LocKey — ключ ШАБЛОНА уведомления (RP_EV_ECO_CRISIS_STARTED), а не ключ имени типа
		// кризиса: имя кризиса уходит параметром {crisis} и переводится отдельно.
		// Рутинный спад — Severity.Trivial: при стандартном уровне уведомлений он попадает
		// только в летопись и не спамит всплывашками; депрессия, банкротство и крах торговли
		// достаточно серьёзны (могут поднять мятеж), чтобы заслужить уведомление в игре.
		bus?.Publish(new PolEvent(PolEventType.EconomicCrisisStarted, "RP_EV_ECO_CRISIS_STARTED")
		{
			Year = year,
			Severity = type == EconomicCrisisType.Recession ? Severity.Trivial : Severity.Major,
			KingdomId = state.KingdomId
		}.With("kingdom", input.Name ?? string.Empty)
			.With("crisis", EconomicCrises.LocKey(type))
			.With("years", num).With("deficit_before", deficitBefore).With("gold_before", input.Gold).With("crisis_kind", (int)type)
			.With("prosperity", eco.Prosperity));
	}

	private static void EndCrisis(KingdomPolitics state, EconomyState eco, EconomyInput input, SimParams p, int year, PolEventBus? bus)
	{
		EconomicCrisisType crisis = eco.Crisis;
		eco.Crisis = EconomicCrisisType.None;
		eco.CrisisUntilYear = 0;
		// Окончание кризиса — рутина для летописи: не дублируем поток всплывашек
		// (при уровне уведомлений «показывать всё» событие всё равно видно).
		bus?.Publish(new PolEvent(PolEventType.EconomicCrisisEnded, "RP_EV_ECO_CRISIS_ENDED")
		{
			Year = year,
			Severity = Severity.Trivial,
			KingdomId = state.KingdomId
		}.With("kingdom", input.Name ?? string.Empty)
			.With("crisis", EconomicCrises.LocKey(crisis))
			.With("prosperity", eco.Prosperity));
	}

	private static void UpdateProsperity(EconomyState eco, EconomyInput input, EconomyReport report, SimParams p)
	{
		float target = p.ProsperityBaseline;
		target += Math.Min(p.ProsperityTradeMax, report.OpenRoutes * p.ProsperityTradePerRoute);
		if (report.Net < 0f)
		{
			target -= p.ProsperityDeficitLoss;
		}
		if (input.AtWar)
		{
			target -= p.ProsperityWarLoss;
		}
		if (input.Starving)
		{
			target -= p.ProsperityFamineLoss;
		}
		if (eco.InCrisis)
		{
			target -= p.ProsperityCrisisLoss * EconomicCrises.Severity(eco.Crisis);
		}
		if (eco.Austerity)
		{
			target -= p.ProsperityAusterityLoss;
		}
		if (eco.TaxRate > 1.2f)
		{
			target -= p.ProsperityHighTaxLoss;
		}
		else if (eco.TaxRate < 0.9f)
		{
			target += p.ProsperityLowTaxGain;
		}
		target = Clamp(target, 0f, 100f);
		eco.Prosperity = Clamp(eco.Prosperity + (target - eco.Prosperity) * p.ProsperityConvergence, 0f, 100f);
	}

	private static void ApplyMood(KingdomPolitics state, EconomyState eco, EconomyInput input, EconomyReport report, SimParams p)
	{
		float num = 0f;
		if (eco.Prosperity >= p.ProsperityHappyLine)
		{
			num += p.MoodProsperityGain;
		}
		else if (eco.Prosperity <= p.ProsperityAngryLine)
		{
			num -= p.MoodProsperityLoss;
		}
		if (eco.InCrisis)
		{
			num -= p.MoodCrisisLoss * EconomicCrises.Severity(eco.Crisis);
		}
		if (eco.TaxRate > 1.2f)
		{
			num -= p.MoodHighTaxLoss;
		}
		if (eco.Austerity)
		{
			num -= p.MoodAusterityLoss;
		}
		// 0.24: статья VII «О казне и податях» — настроение подданных по тяжести бремени.
		if (state.Constitution != null)
		{
			TaxLaw taxLaw = state.Constitution.Taxes;
			if (taxLaw != TaxLaw.Moderate)
			{
				num += ConstitutionEffects.TaxMood(taxLaw, state.Constitution.Supremacy == SupremacyRule.Binding);
			}
		}
		// 0.24: статья IX «О вере и веротерпимости» — недовольство по отношению к иноверцам.
		if (state.Constitution != null)
		{
			FaithLaw faithLaw = state.Constitution.Faith;
			if (faithLaw != FaithLaw.StateFaith)
			{
				float faithDelta = ConstitutionEffects.FaithDissent(faithLaw, state.Constitution.Supremacy == SupremacyRule.Binding);
				// Чистота веры раскалывает только там, где есть иноверцы: размер эффекта
				// тут скромный — основное действие в летописи/событиях и дипломатии.
				state.AvgDissent = Clamp(state.AvgDissent + faithDelta, 0f, 1f);
			}
		}
		state.Support = Clamp(state.Support + num, 0f, 100f);
		// Недовольство реагирует медленнее поддержки: кризис накапливается годами.
		state.AvgDissent = Clamp(state.AvgDissent - num * p.DissentFromMoodRate, 0f, 1f);
		report.HappinessDelta = num * p.HappinessFromMoodRate;
	}

	private static void TickBoom(KingdomPolitics state, EconomyState eco, EconomyInput input, SimParams p, int year, PolEventBus? bus, EconomyReport report)
	{
		if (eco.InCrisis || eco.DeficitYears > 0 || input.AtWar || input.Starving)
		{
			return;
		}
		if (eco.Prosperity < p.BoomProsperity || report.OpenRoutes < p.BoomMinRoutes)
		{
			return;
		}
		if (eco.LastBoomYear > 0 && year - eco.LastBoomYear < p.BoomCooldownYears)
		{
			return;
		}
		eco.LastBoomYear = year;
		eco.BoomsTotal++;
		report.GoldDelta += p.BoomGoldBonus;
		report.HappinessDelta += p.BoomHappiness;
		report.Boom = true;
		// Бум — рутина, как и спад: при стандартном уровне уведомлений уходит
		// только в летопись и не спамит всплывашками (жалоба 0.17.2: «уведомления
		// об экономических спадах и так далее» при уровне 1).
		bus?.Publish(new PolEvent(PolEventType.EconomicBoom, "RP_EV_ECO_BOOM")
		{
			Year = year,
			Severity = Severity.Trivial,
			KingdomId = state.KingdomId
		}.With("kingdom", input.Name ?? string.Empty)
			.With("prosperity", eco.Prosperity)
			.With("gold", p.BoomGoldBonus));
	}

	private static void ExpireTimers(EconomyState eco, int year)
	{
		if (eco.Austerity && year > eco.AusterityUntilYear)
		{
			eco.Austerity = false;
		}
		if (eco.EmbargoTargetId != 0 && year > eco.EmbargoUntilYear)
		{
			eco.EmbargoTargetId = 0;
		}
	}
}
