using System;
using System.Collections.Generic;
using NeoModLoader.services;
using RealPolitic.Core;

namespace RealPolitic;

/// <summary>
/// Игровой исполнитель экономики и торговли (этап 18).
///
/// Ядро (<see cref="Economy"/>) считает налоги, торговлю, благосостояние и кризисы;
/// этот класс переносит результат в WorldBox: раскладывает прибыль и убытки по городам,
/// меняет настроение жителей и исполняет стратегические действия
/// («развить торговлю», «режим экономии», «торговое эмбарго»).
///
/// Все обращения к игре идут через безопасную рефлексию (<see cref="GameRefs"/>):
/// если сигнатура изменилась — действие пропускается с одной записью в лог,
/// годовой цикл и игра продолжают работать.
/// </summary>
public static class EconomyExecutor
{
	/// <summary>Включена ли экономическая модель (настройка EnableEconomy).</summary>
	public static bool EconomyEnabled = true;

	/// <summary>Включена ли торговля между державами (настройка EnableTrade).</summary>
	public static bool TradeEnabled = true;

	private static readonly HashSet<string> _warned = new HashSet<string>();

	private static void Warn(string what)
	{
		if (_warned.Add(what))
		{
			LogService.LogError("[Real Politic] API экономики недоступно: " + what);
		}
	}

	/// <summary>
	/// Применяет годовой отчёт экономики в мире: золото и хлеб раскладываются по городам,
	/// настроение жителей сдвигается на величину из отчёта.
	/// </summary>
	public static bool ApplyReport(Kingdom kingdom, EconomyReport report)
	{
		if (!EconomyEnabled || kingdom == null || report == null)
		{
			return false;
		}
		try
		{
			List<City> cities = GameRefs.KingdomCities(kingdom);
			if (cities == null || cities.Count == 0)
			{
				return false;
			}
			bool applied = false;
			int goldPerCity = (int)Math.Round(report.GoldDelta / cities.Count);
			int foodPerCity = (int)Math.Round(report.FoodDelta / cities.Count);
			int happiness = (int)Math.Round(report.HappinessDelta);
			for (int i = 0; i < cities.Count; i++)
			{
				City city = cities[i];
				if (city == null)
				{
					continue;
				}
				if (goldPerCity != 0)
				{
					applied |= AddResource(city, "gold", goldPerCity);
				}
				if (foodPerCity != 0)
				{
					applied |= AddResource(city, "bread", foodPerCity);
				}
				if (happiness != 0)
				{
					NudgeCityHappiness(city, happiness);
					applied = true;
				}
			}
			return applied;
		}
		catch (Exception ex)
		{
			Warn("ApplyReport: " + ex.Message);
			return false;
		}
	}

	/// <summary>
	/// Стратегическое действие «развить торговлю»: держава вкладывает золото в пути —
	/// существующий путь растёт в объёме, а при отсутствии путей открывается новый.
	/// </summary>
	public static bool DevelopTrade(MapBox map, Kingdom kingdom, KingdomPolitics state, PoliticalWorld world, int year, SimParams p, out string result)
	{
		result = string.Empty;
		if (!EconomyEnabled || !TradeEnabled || kingdom == null || state == null || world == null || p == null)
		{
			return false;
		}
		try
		{
			// Инвестиция оплачивается из запасов столицы: нет золота — нет и вложений.
			if (!TakeResource(kingdom, "gold", (int)Math.Round(p.TradeInvestCost)))
			{
				result = GameText.Localized("RP_RES_NO_GOLD_INVEST", "нет золота для вложений");
				return false;
			}
			List<TradeRoute> routes = world.Trade.Of(state.KingdomId);
			TradeRoute best = null;
			for (int i = 0; i < routes.Count; i++)
			{
				if (!routes[i].Blocked && (best == null || routes[i].Volume < best.Volume))
				{
					best = routes[i];
				}
			}
			if (best != null)
			{
				best.Volume += p.TradeVolumeGrowth * 6f;
				result = GameText.Localized("RP_RES_TRADE_EXPANDED", "торговля расширена");
				return true;
			}
			// Своего пути нет — пробуем открыть его с мирным соседом немедленно.
			List<Kingdom> kingdoms = GameRefs.KingdomList(map);
			if (kingdoms == null)
			{
				result = GameText.Localized("RP_RES_NO_GOLD_INVEST", "нет золота для вложений");
				return false;
			}
			for (int j = 0; j < kingdoms.Count; j++)
			{
				Kingdom other = kingdoms[j];
				if (other == null || other == kingdom)
				{
					continue;
				}
				int otherId = KingdomId(other);
				if (otherId == state.KingdomId || world.GetKingdom(otherId) == null)
				{
					continue;
				}
				if (GameRefs.AreKingdomsAtWar(kingdom, other))
				{
					continue;
				}
				TradeRoute route = world.Trade.Open(state.KingdomId, otherId, year);
				if (route != null)
				{
					route.Volume += p.TradeVolumeGrowth * 4f;
					world.Bus.Publish(new PolEvent(PolEventType.TradeRouteOpened, "RP_EV_TRADE_OPENED")
					{
						Year = year,
						Severity = Severity.Minor,
						KingdomId = state.KingdomId
					}.With("kingdom", KingdomName(kingdom))
						.With("partner", KingdomName(other)));
					result = GameText.Localized("RP_RES_TRADE_ROUTE_OPENED", "открыт новый торговый путь");
					return true;
				}
			}
			result = GameText.Localized("RP_RES_NO_PARTNER", "нет подходящего партнёра");
			return false;
		}
		catch (Exception ex)
		{
			Warn("DevelopTrade: " + ex.Message);
			return false;
		}
	}

	/// <summary>Стратегическое действие «режим экономии»: урезает расходы на несколько лет.</summary>
	public static bool StartAusterity(Kingdom kingdom, KingdomPolitics state, int year, SimParams p, out string result)
	{
		result = string.Empty;
		if (!EconomyEnabled || kingdom == null || state == null || p == null)
		{
			return false;
		}
		EconomyState eco = Economy.EnsureState(state);
		eco.Austerity = true;
		eco.AusterityUntilYear = year + p.AusterityYears;
		result = GameText.Localized("RP_RES_AUSTERITY", "введён режим экономии");
		// Жёсткая экономия немедленно бьёт по настроению: люди замечают урезание.
		NudgeKingdomHappiness(kingdom, -4);
		return true;
	}

	/// <summary>
	/// Стратегическое действие «торговое эмбарго»: перекрывает пути с выбранным соперником.
	/// Цель ищет игровой слой — ядру нужен только идентификатор.
	/// </summary>
	public static bool StartEmbargo(MapBox map, Kingdom kingdom, KingdomPolitics state, PoliticalWorld world, int year, SimParams p, out string result)
	{
		result = string.Empty;
		if (!EconomyEnabled || !TradeEnabled || map == null || kingdom == null || state == null || world == null || p == null)
		{
			return false;
		}
		try
		{
			List<Kingdom> kingdoms = GameRefs.KingdomList(map);
			if (kingdoms == null)
			{
				return false;
			}
			for (int i = 0; i < kingdoms.Count; i++)
			{
				Kingdom other = kingdoms[i];
				if (other == null || other == kingdom)
				{
					continue;
				}
				int otherId = KingdomId(other);
				if (otherId == state.KingdomId || world.GetKingdom(otherId) == null)
				{
					continue;
				}
				if (!GameRefs.AreKingdomsAtWar(kingdom, other))
				{
					continue;
				}
				EconomyState eco = Economy.EnsureState(state);
				eco.EmbargoTargetId = otherId;
				eco.EmbargoUntilYear = year + p.EmbargoYears;
				int blocked = world.Trade.BlockBetween(state.KingdomId, otherId);
				world.Bus.Publish(new PolEvent(PolEventType.TradeEmbargo, "RP_EV_TRADE_EMBARGO")
				{
					Year = year,
					Severity = Severity.Major,
					KingdomId = state.KingdomId
				}.With("kingdom", KingdomName(kingdom))
					.With("target", KingdomName(other))
					.With("years", p.EmbargoYears)
					.With("routes", blocked));
				result = GameText.Localized("RP_RES_EMBARGO", "введено торговое эмбарго");
				return true;
			}
			return false;
		}
		catch (Exception ex)
		{
			Warn("StartEmbargo: " + ex.Message);
			return false;
		}
	}

	/// <summary>Строка экономики для окна державы: казна, торговля, благосостояние, кризис.</summary>
	public static bool TryGetSummary(KingdomPolitics state, PoliticalWorld world, SimParams p, int year, out string summary)
	{
		summary = string.Empty;
		EconomyState? eco = state?.Economy;
		if (!EconomyEnabled || eco == null || world == null)
		{
			return false;
		}
		int net = (int)Math.Round(eco.LastIncome - eco.LastExpenses);
		int prosperity = (int)Math.Round(eco.Prosperity);
		int routes = world.Trade.OpenRoutesOf(state.KingdomId);
		string text = GameText.Localized("RP_ECO_PROSPERITY_SHORT") + " " + prosperity + "%";
		text += " • " + (net >= 0 ? "+" : string.Empty) + net + " " + GameText.Localized("RP_ECO_INCOME_SHORT");
		if (TradeEnabled && routes > 0)
		{
			text += " • " + GameText.Localized("RP_ECO_ROUTES_SHORT") + " " + routes;
		}
		if (eco.InCrisis)
		{
			text += " • " + GameText.Localized(EconomicCrises.LocKey(eco.Crisis));
		}
		else if (eco.AusterityActive(year))
		{
			text += " • " + GameText.Localized("RP_ECO_AUSTERITY_ACTIVE");
		}
		else if (eco.EmbargoActive(year))
		{
			text += " • " + GameText.Localized("RP_ECO_EMBARGO_ACTIVE");
		}
		summary = text;
		return true;
	}

	// ------------------------------------------------------------------ безопасные обёртки

	private static bool AddResource(City city, string resource, int amount)
	{
		if (city == null || amount == 0)
		{
			return false;
		}
		try
		{
			if (amount < 0)
			{
				int toTake = -amount;
				object raw = GameRefs.Invoke(city, "getResourcesAmount", resource)
					?? (string.Equals(resource, "gold", StringComparison.OrdinalIgnoreCase)
						? (GameRefs.Invoke(city, "get_amount_gold") ?? GameRefs.ReadProperty(city, "amount_gold"))
						: null);
				if (GameRefs.TryResourceValue(raw, out double available))
				{
					toTake = Math.Min(toTake, Math.Max(0, (int)Math.Floor(available)));
					if (toTake <= 0)
					{
						return true; // Склад уже пуст — не загоняем нативный ресурс в минус.
					}
				}
				bool taken = GameRefs.TryInvoke(city, "takeResource", out _, resource, toTake)
					|| GameRefs.TryInvoke(city, "addResourcesToRandomStockpile", out _, resource, -toTake);
				if (taken && IsFoodResource(resource))
				{
					GameRefs.Invoke(city, "updateTotalFood");
				}
				return taken;
			}
			bool added = GameRefs.TryInvoke(city, "addResourcesToRandomStockpile", out _, resource, amount);
			if (added && IsFoodResource(resource))
			{
				GameRefs.Invoke(city, "updateTotalFood");
			}
			return added;
		}
		catch (Exception ex)
		{
			Warn("AddResource(" + resource + "): " + ex.Message);
			return false;
		}
	}

	private static bool IsFoodResource(string resource)
	{
		return string.Equals(resource, "bread", StringComparison.OrdinalIgnoreCase)
			|| string.Equals(resource, "meat", StringComparison.OrdinalIgnoreCase)
			|| string.Equals(resource, "fish", StringComparison.OrdinalIgnoreCase)
			|| string.Equals(resource, "berries", StringComparison.OrdinalIgnoreCase)
			|| string.Equals(resource, "wheat", StringComparison.OrdinalIgnoreCase)
			|| string.Equals(resource, "food", StringComparison.OrdinalIgnoreCase);
	}

	/// <summary>Забирает ресурс из столицы, если запас достаточен. Возврат false — платить нечем.</summary>
	private static bool TakeResource(Kingdom kingdom, string resource, int amount)
	{
		try
		{
			List<City> cities = GameRefs.KingdomCities(kingdom);
			if (cities == null || cities.Count == 0)
			{
				return false;
			}
			City capital = GameRefs.KingdomCapital(kingdom) ?? cities[0];
			if (capital == null)
			{
				return false;
			}
			object current = GameRefs.Invoke(capital, "getResourcesAmount", resource)
				?? (string.Equals(resource, "gold", StringComparison.OrdinalIgnoreCase)
					? (GameRefs.Invoke(capital, "get_amount_gold") ?? GameRefs.ReadProperty(capital, "amount_gold"))
					: null);
			// WorldBox 0.50+: getResourcesAmount возвращает int (System.Int32), старые стабы — float.
			if (GameRefs.TryResourceValue(current, out double have) && have >= amount)
			{
				if (!GameRefs.TryInvoke(capital, "takeResource", out _, resource, amount))
				{
					GameRefs.Invoke(capital, "addResourcesToRandomStockpile", resource, -amount);
				}
				return true;
			}
			return false;
		}
		catch (Exception ex)
		{
			Warn("TakeResource(" + resource + "): " + ex.Message);
			return false;
		}
	}

	private static void NudgeCityHappiness(City city, int delta)
	{
		try
		{
			List<Actor> units = city?.units;
			if (units == null)
			{
				return;
			}
			// Не трогаем всех жителей больших городов каждый год — достаточно выборки.
			int step = units.Count > 40 ? units.Count / 40 : 1;
			for (int i = 0; i < units.Count; i += step)
			{
				Actor actor = units[i];
				if (actor != null)
				{
					IdeologyEffects.ApplyHappiness(actor, delta);
				}
			}
		}
		catch (Exception ex)
		{
			Warn("NudgeCityHappiness: " + ex.Message);
		}
	}

	private static void NudgeKingdomHappiness(Kingdom kingdom, int delta)
	{
		try
		{
			List<City> cities = GameRefs.KingdomCities(kingdom);
			if (cities == null)
			{
				return;
			}
			for (int i = 0; i < cities.Count; i++)
			{
				NudgeCityHappiness(cities[i], delta);
			}
		}
		catch (Exception ex)
		{
			Warn("NudgeKingdomHappiness: " + ex.Message);
		}
	}

	/// <summary>Название державы для уведомлений: имя из игры, при неудаче — идентификатор.</summary>
	private static string KingdomName(Kingdom kingdom)
	{
		try
		{
			object name = GameRefs.ReadProperty(kingdom, "name");
			if (name != null && !string.IsNullOrEmpty(name.ToString()))
			{
				return name.ToString();
			}
			object data = GameRefs.ReadProperty(kingdom, "data");
			if (data != null)
			{
				object dataName = GameRefs.ReadProperty(data, "name");
				if (dataName != null && !string.IsNullOrEmpty(dataName.ToString()))
				{
					return dataName.ToString();
				}
			}
		}
		catch (Exception)
		{
			// Имя — косметика: ниже вернём идентификатор.
		}
		return "№" + KingdomId(kingdom);
	}

	private static int KingdomId(Kingdom kingdom)
	{
		try
		{
			return (int)((NanoObject)kingdom).id;
		}
		catch (Exception)
		{
			return 0;
		}
	}
}
