using System;
using System.Collections.Generic;
using NeoModLoader.services;
using RealPolitic.Core;
using UnityEngine;

namespace RealPolitic;

/// <summary>
/// Игровые последствия модовых механик идеологий.
///
/// Ядро (RealPolitic.Core) решает КОГДА сработала ульта и КОГДА сорвалась угроза;
/// этот класс переводит решения ядра в события самой игры: золото в городах,
/// счастье жителей, воины, лояльность.
///
/// Всё делается через безопасные вызовы (try/catch + рефлексия там, где сигнатура
/// не гарантирована), чтобы падение одного эффекта не роняло мод целиком.
/// </summary>
public static class IdeologyMechanicsEffects
{
	/// <summary>Модовые механики идеологий включены (настройка IdeologyMechanics).</summary>
	public static bool Enabled = true;

	private static float _nextTick;

	/// <summary>Последний игровой год, в котором каждому государству выдали эффект ульты.</summary>
	private static readonly Dictionary<int, int> _lastAppliedYear = new Dictionary<int, int>();

	/// <summary>Сброс при смене мира (id держав и годы нового мира начинаются заново).</summary>
	public static void Reset()
	{
		_lastAppliedYear.Clear();
		_nextTick = 0f;
	}

	/// <summary>Периодический тик: применяем эффекты активных ульт. Вызывается из Update.</summary>
	public static void Tick(PoliticalWorld world, int year)
	{
		if (!Enabled || world == null)
		{
			return;
		}
		float now = Time.unscaledTime;
		if (now < _nextTick)
		{
			return;
		}
		_nextTick = now + 2f;
		try
		{
			MapBox map = World.world;
			if (map == null || map.kingdoms == null)
			{
				return;
			}
			List<Kingdom> kingdoms = ((CoreSystemManager<Kingdom, KingdomData>)(object)map.kingdoms).list;
			for (int i = 0; kingdoms != null && i < kingdoms.Count; i++)
			{
				Kingdom kingdom = kingdoms[i];
				if (kingdom == null)
				{
					continue;
				}
					KingdomPolitics state = world.GetKingdom((int)((NanoObject)kingdom).id);
					if (state == null)
					{
						continue;
					}
					if (state.Mechanics.IsActive(year))
					{
						int kingdomId = (int)((NanoObject)kingdom).id;
						if (_lastAppliedYear.TryGetValue(kingdomId, out int appliedYear) && appliedYear == year)
						{
							continue;
						}
						ApplyActivePower(kingdom, state, year);
						_lastAppliedYear[kingdomId] = year;
					}
			}
		}
		catch (Exception ex)
		{
			Patches.LogOncePublic("ошибка эффектов механик идеологий: " + ex.Message);
		}
	}

	// ---------------------------------------------------------------- эффекты ульт

	/// <summary>
	/// Исполнитель локальных решений стратегического ИИ. Все изменения ограничены
	/// существующими безопасными операциями этого класса и состоянием ядра.
	/// </summary>
	public static bool TryApplyStrategicAction(Kingdom kingdom, KingdomPolitics state, KingdomActionType action, int year, out string result)
	{
		result = string.Empty;
		if (kingdom == null || state == null)
		{
			result = GameText.Localized("RP_RES_STATE_MISSING", "игровое или политическое состояние недоступно");
			return false;
		}
		try
		{
			switch (action)
			{
					case KingdomActionType.EmergencyFood:
                        var foodObservation=EmergencyFoodObservation.Capture(kingdom);
                        bool foodRequested=GiveGold(kingdom, capitalOnly: false, amount: 8, resource: "bread");
                        foodObservation.LogAfter(year,foodRequested);
						if (!foodRequested)
                        {
                            result = GameText.Localized("RP_RES_FOOD_FAILED", "пополнение еды не выполнено: нет доступного склада или игрового API");
                            return false;
                        }
						result = GameText.Localized("RP_RES_FOOD_RESTOCKED", "запрос экстренного пополнения еды передан игровому API; питание жителей не проверено");
						return true;
				case KingdomActionType.Defend:
				case KingdomActionType.Mobilize:
					Mobilize(kingdom);
					result = GameText.Localized("RP_RES_MOBILIZED", "оборона усилена мобилизацией жителей");
					return true;
				case KingdomActionType.Demobilize:
					Demobilize(kingdom);
					result = GameText.Localized("RP_RES_DEMOBILIZED", "часть войска демобилизована");
					return true;
				case KingdomActionType.SuppressRebellion:
					return CivilWarExecutor.SuppressRebellion(kingdom, state, out result);
				case KingdomActionType.RestoreLegitimacy:
					state.Legitimacy = Math.Min(80f, state.Legitimacy + 16f);
					state.Support = Math.Min(100f, state.Support + 4f);
					state.Mechanics.ThreatLevel = Math.Max(0f, state.Mechanics.ThreatLevel - 15f);
					result = GameText.Localized("RP_RES_LEGITIMACY_RESTORED", "легитимность восстановлена политическими мерами");
					return true;
				case KingdomActionType.UseIdeologyPower:
					IdeologyPowerDef power = IdeologyMechanics.Power(state.Ideology);
					if (state.Mechanics.IsActive(year) || state.Mechanics.CooldownLeft(power, year) > 0)
					{
						result = GameText.Localized("RP_RES_POWER_NOT_READY", "ульта ещё не готова");
						return false;
					}
					state.Mechanics.LastUsedYear = year;
					state.Mechanics.ActiveUntilYear = year + power.Duration;
					ApplyActivePower(kingdom, state, year);
					result = GameText.Localized("RP_RES_POWER_USED", "уникальная способность идеологии активирована");
					return true;
				case KingdomActionType.AccumulateGold:
					GiveGold(kingdom, capitalOnly: true, amount: 12, resource: "gold");
					result = GameText.Localized("RP_RES_GOLD_ADDED", "казна пополнена");
					return true;
				case KingdomActionType.AccumulateStone:
					GiveGold(kingdom, capitalOnly: true, amount: 8, resource: "stone");
					result = GameText.Localized("RP_RES_STONE_ADDED", "запас камня пополнен");
					return true;
				case KingdomActionType.InvestGrowth:
					GiveGold(kingdom, capitalOnly: true, amount: 4, resource: "gold");
					GiveGold(kingdom, capitalOnly: true, amount: 4, resource: "stone");
					result = GameText.Localized("RP_RES_GROWTH_INVESTED", "инвестиция в развитие направлена в столицу");
					return true;
				default:
					result = GameText.Localized("RP_RES_NEED_DIPLOMACY_EXECUTOR", "для этого действия нужен дипломатический исполнитель");
					return false;
			}
		}
		catch (Exception ex)
		{
			result = "эффект не применён: " + ex.GetType().Name;
			return false;
		}
	}

	/// <summary>Эффект действующей ульты — применяется, пока она активна.</summary>
	private static void ApplyActivePower(Kingdom kingdom, KingdomPolitics state, int year)
	{
		switch (state.Ideology)
		{
			case IdeologyType.Tribalism:
				// Великий сход: племя пирует — люди довольны и здоровы.
				NudgeHappiness(kingdom, 3);
				Heal(kingdom, 0.05f);
				GiveGold(kingdom, capitalOnly: false, amount: 1, resource: "meat");
				break;
			case IdeologyType.Monarchy:
				// Династический брак: порядок в державе.
				NudgeHappiness(kingdom, 2);
				break;
			case IdeologyType.Theocracy:
				// Священный поход: рвение исцеляет раны верующих.
				NudgeHappiness(kingdom, 2);
				Heal(kingdom, 0.08f);
				break;
			case IdeologyType.Aristocracy:
				// Пожалование титула: столица богатеет.
				GiveGold(kingdom, capitalOnly: true, amount: 4);
				break;
			case IdeologyType.MerchantRepublic:
				// Торговый договор: золото течёт во все города.
				GiveGold(kingdom, capitalOnly: false, amount: 3);
				break;
			case IdeologyType.Militarism:
				// Мобилизация: горожане реально становятся воинами, пока действует ульта.
				Mobilize(kingdom);
				NudgeHappiness(kingdom, -2);
				break;
			case IdeologyType.Technocracy:
				// Великий проект: столица получает камень и металл на стройку.
				GiveGold(kingdom, capitalOnly: true, amount: 2);
				GiveGold(kingdom, capitalOnly: true, amount: 2, resource: "stone");
				break;
			case IdeologyType.Liberalism:
				// Свободная торговля: золото и довольство во всех городах.
				NudgeHappiness(kingdom, 2);
				GiveGold(kingdom, capitalOnly: false, amount: 2);
				break;
			case IdeologyType.Democracy:
				// Всенародный референдум: подъем доверия и торговля.
				NudgeHappiness(kingdom, 3);
				GiveGold(kingdom, capitalOnly: false, amount: 2);
				break;
			case IdeologyType.Communism:
				// Пятилетний план: мощный приток ресурсов во все города.
				GiveGold(kingdom, capitalOnly: false, amount: 2, resource: "bread");
				GiveGold(kingdom, capitalOnly: false, amount: 2, resource: "stone");
				GiveGold(kingdom, capitalOnly: false, amount: 2, resource: "gold");
				break;
		case IdeologyType.Ethnonationalism:
			// Национальный триумф: мобилизация и подъем боевого духа.
			Mobilize(kingdom);
			NudgeHappiness(kingdom, 2);
			break;
		case IdeologyType.Khaganate:
			// Великий курултай: конница и улусы собираются под знамя кагана.
			Mobilize(kingdom);
			NudgeHappiness(kingdom, 2);
			GiveGold(kingdom, capitalOnly: false, amount: 1, resource: "meat");
			break;
		case IdeologyType.Empire:
			// Имперский манифест: расцвет столицы и порядок в провинциях.
			NudgeHappiness(kingdom, 3);
			GiveGold(kingdom, capitalOnly: true, amount: 4);
			break;
		case IdeologyType.FreeTerritory:
			// Вольный клич: вольница встаёт на защиту свободной земли.
			Mobilize(kingdom);
			NudgeHappiness(kingdom, 3);
			break;
		}
	}

	/// <summary>
	/// Всеобщая мобилизация: часть горожан переводится в воины прямо в игре
	/// (City.makeWarrior выдаёт профессию и оружие). Не больше свободных слотов
	/// города и по чуть-чуть за тик, чтобы город не опустел мгновенно.
	/// </summary>
	private static void Mobilize(Kingdom kingdom)
	{
		List<City> cities = GameRefs.KingdomCities(kingdom);
		if (cities == null)
		{
			return;
		}
		for (int i = 0; i < cities.Count; i++)
		{
			City city = cities[i];
			if (city == null)
			{
				continue;
			}
			try
			{
				int current = (GameRefs.Invoke(city, "countWarriors") is int c) ? c : 0;
				int max = (GameRefs.Invoke(city, "getMaxWarriors") is int m) ? m : 0;
				// В мобилизацию город может выставить в полтора раза больше обычного.
				int target = (int)(max * 1.5f);
				int free = target - current;
				if (free <= 0)
				{
					continue;
				}
				List<Actor> units = city.units;
				if (units == null)
				{
					continue;
				}
				int made = 0;
				for (int u = 0; u < units.Count && made < 2 && made < free; u++)
				{
					Actor actor = units[u];
					if (actor == null)
					{
						continue;
					}
					// Берём только простых горожан: правителей и детей не трогаем.
					if (!(GameRefs.ReadProperty(actor, "is_profession_citizen") is bool citizen) || !citizen)
					{
						continue;
					}
					if (GameText.ActorAge(actor) < 16)
					{
						continue;
					}
					GameRefs.Invoke(city, "makeWarrior", actor);
					made++;
				}
			}
			catch (Exception)
			{
			}
		}
	}

	// ---------------------------------------------------------------- последствия срывов

	/// <summary>
	/// Срыв уникальной угрозы — разовое тяжёлое последствие.
	/// Вызывается из GameNotifier, когда ядро публикует IdeologyThreatBroke.
	/// </summary>
	public static void ApplyThreatBreak(Kingdom kingdom, IdeologyType ideology)
	{
		if (!Enabled || kingdom == null)
		{
			return;
		}
		try
		{
			int kingdomId = (int)((NanoObject)kingdom).id;
			KingdomPolitics state = RealPoliticMod.World?.GetKingdom(kingdomId);

			switch (ideology)
			{
			case IdeologyType.Tribalism:
				// Раскол племени: восстание и раскол на независимые общины.
				NudgeHappiness(kingdom, -35);
				TrySecedeRebelCity(kingdom, state, "RP_SECESSION_TRIBAL_SPLIT");
				break;
			case IdeologyType.Monarchy:
				// Кризис престолонаследия / Великая смута: распад и война за независимость провинций.
				NudgeHappiness(kingdom, -30);
				TrySecedeRebelCity(kingdom, state, "RP_SECESSION_INTERREGNUM");
				break;
			case IdeologyType.Theocracy:
				// Ересь: религиозный раскол и отделение города еретиков.
				NudgeHappiness(kingdom, -30);
				TrySecedeRebelCity(kingdom, state, "RP_SECESSION_HERESY");
				break;
			case IdeologyType.Aristocracy:
				// Заговор знати: феодальный мятеж барона и сепарация.
				NudgeHappiness(kingdom, -20);
				TrySecedeRebelCity(kingdom, state, "RP_SECESSION_CORRUPTION");
				break;
			case IdeologyType.MerchantRepublic:
				// Долговой кризис: опустошение казны и бунт портового города.
				DrainGold(kingdom);
				NudgeHappiness(kingdom, -25);
				TrySecedeRebelCity(kingdom, state, "RP_SECESSION_DEBT_COLLAPSE");
				break;
			case IdeologyType.Militarism:
				// Военная усталость и мятеж: армия бунтует, гарнизон отделяется.
				Demobilize(kingdom);
				NudgeHappiness(kingdom, -35);
				TrySecedeRebelCity(kingdom, state, "RP_SECESSION_MILITARY_MUTINY");
				break;
			case IdeologyType.Egalitarianism:
				// Паралич власти: раскол схода общины и выход недовольных.
				NudgeHappiness(kingdom, -25);
				TrySecedeRebelCity(kingdom, state, "RP_SECESSION_PARALYSIS");
				break;
			case IdeologyType.Technocracy:
				// Техногенный кризис / Утечка умов: бунт рабочих и аварии.
				NudgeHappiness(kingdom, -25);
				TrySecedeRebelCity(kingdom, state, "RP_SECESSION_ACADEMY_UNREST");
				break;
			case IdeologyType.Liberalism:
				// Расслоение: бунт обнищавших и отделение города.
				NudgeHappiness(kingdom, -25);
				DrainGold(kingdom);
				TrySecedeRebelCity(kingdom, state, "RP_SECESSION_INEQUALITY");
				break;
			case IdeologyType.Democracy:
				// Политическая поляризация: гражданский конфликт, потеря казны и сепарация.
				NudgeHappiness(kingdom, -25);
				DrainGold(kingdom);
				TrySecedeRebelCity(kingdom, state, "RP_SECESSION_POLARIZATION");
				break;
			case IdeologyType.Communism:
				// Бюрократический застой и дефицит: бунт фабрик и отделение.
				NudgeHappiness(kingdom, -30);
				DrainGold(kingdom);
				TrySecedeRebelCity(kingdom, state, "RP_SECESSION_COMMUNIST_UNREST");
				break;
			case IdeologyType.Ethnonationalism:
				// Межэтнический бунт: восстание инородцев и немедленное отделение окраины.
				NudgeHappiness(kingdom, -35);
				Demobilize(kingdom);
				TrySecedeRebelCity(kingdom, state, "RP_SECESSION_ETHNIC_REVOLT");
				break;
			case IdeologyType.Khaganate:
				// Междоусобица улусов: распря родов и отделение кочевого улуса.
				NudgeHappiness(kingdom, -30);
				TrySecedeRebelCity(kingdom, state, "RP_SECESSION_ULUS_FEUD");
				break;
			case IdeologyType.Empire:
				// Дворцовый переворот: смута в столице и мятеж наместника провинции.
				NudgeHappiness(kingdom, -30);
				DrainGold(kingdom);
				TrySecedeRebelCity(kingdom, state, "RP_SECESSION_PALACE_COUP");
				break;
			case IdeologyType.FreeTerritory:
				// Атаманщина: раскол между батьками и выход своевольной общины.
				NudgeHappiness(kingdom, -25);
				TrySecedeRebelCity(kingdom, state, "RP_SECESSION_ATAMANSHCHINA");
				break;
			}
		}
		catch (Exception ex)
		{
			Patches.LogOncePublic("ошибка последствий срыва угрозы: " + ex.Message);
		}
	}

	/// <summary>
	/// Попытка отделить окраинный/недовольный город при кризисе державы.
	/// </summary>
	public static bool TrySecedeRebelCity(Kingdom kingdom, KingdomPolitics state, string reasonKey)
	{
		if (kingdom == null) return false;
		List<City> cities = GameRefs.KingdomCities(kingdom);
		if (cities == null || cities.Count <= 1)
		{
			return false;
		}
		// Столица не может отделяться (в игре мятежат только не-столицы).
		// Берём настоящее поле Kingdom.capital: порядок cities столицы не гарантирует —
		// в 0.17.0 «отделялась» столица (см. летопись: «Касин отделился от Сливанска»).
		City capital = GameRefs.KingdomCapital(kingdom);
		City candidate = null;
		int lowestLoyalty = int.MaxValue;
		// Ищем среди неосновных городов город с наименьшей лояльностью:
		for (int i = 0; i < cities.Count; i++)
		{
			City c = cities[i];
			if (c == null || ReferenceEquals(c, capital)) continue;
			// В 0.51 у getLoyalty есть параметр по умолчанию — зовём с явным аргументом,
			// иначе строгий матчинг числа параметров вернёт ноль у всех городов.
			int loyalty = (GameRefs.Invoke(c, "getLoyalty", false) ?? GameRefs.Invoke(c, "getLoyalty")) is int l ? l : 0;
			if (loyalty < lowestLoyalty)
			{
				lowestLoyalty = loyalty;
				candidate = c;
			}
		}
		if (candidate == null)
		{
			// Запасной выбор: любой город, кроме столицы.
			for (int i = cities.Count - 1; i >= 0; i--)
			{
				if (cities[i] != null && !ReferenceEquals(cities[i], capital))
				{
					candidate = cities[i];
					break;
				}
			}
		}
		if (candidate == null) return false;

        var political=RealPoliticMod.World;
        if(political?.Governance.Enabled==true && state?.Constitution?.Electoral?.RegionalExit!=RegionalExitLaw.Forbidden)
            return CivicRegionalRuntime.Request(political,kingdom,candidate,RealPoliticMod.CurrentYear());
		string cityName = GameRefs.ReadProperty(candidate, "name")?.ToString() ?? "Город";
		string kingdomName = GameRefs.ReadProperty(kingdom, "name")?.ToString() ?? "Государство";

		Kingdom rebelKingdom;
		bool seceded = DiplomacyExecutor.TrySecedeCity(kingdom, candidate, out rebelKingdom);

		// Событие публикуем ТОЛЬКО если сепарация реально произошла в игре.
		// Раньше текст «город отделился» показывался даже при отказе игрового API —
		// пользователь видел фантомное отделение без нового государства.
		if (seceded)
		{
			PoliticalWorld world = RealPoliticMod.World;
			if (world != null)
			{
				world.Bus.Publish(new PolEvent(PolEventType.CitySeceded, "RP_EV_CITY_SECEDED")
				{
					Year = world.Year,
					Severity = Severity.Critical,
					KingdomId = (int)((NanoObject)kingdom).id
				}.With("city", cityName)
					.With("kingdom", kingdomName)
					.With("reason", reasonKey)
            .With("new_kingdom", GameRefs.ReadProperty(rebelKingdom, "name")?.ToString() ?? "?")
            .With("war_status", DiplomacyExecutor.AreAtWar(kingdom,rebelKingdom) ? "RP_CHRON251_WAR_CONFIRMED" : "RP_CHRON251_WAR_UNCONFIRMED"));
				// Этап 23: серия отколов схлопывается в единую строку «государство распалось».
				if (rebelKingdom != null)
				{
					world.RecordSecessionFragment((int)((NanoObject)kingdom).id, (int)((NanoObject)rebelKingdom).id);
				}
			}
		}
		else
		{
			Patches.LogOncePublic("сепарация " + cityName + " не выполнена: игра не создала государство повстанцев (" + (GameRefs.LastInvokeError ?? "подробности выше в логе") + ")");
		}
		return seceded;
	}

	/// <summary>Роспуск части войска: воины возвращаются к мирным занятиям.</summary>
	private static void Demobilize(Kingdom kingdom)
	{
		List<City> cities = GameRefs.KingdomCities(kingdom);
		if (cities == null)
		{
			return;
		}
		for (int i = 0; i < cities.Count; i++)
		{
			List<Actor> units = cities[i]?.units;
			if (units == null)
			{
				continue;
			}
			int sent = 0;
			for (int u = 0; u < units.Count && sent < 3; u++)
			{
				Actor actor = units[u];
				if (actor == null)
				{
					continue;
				}
				if (!(GameRefs.ReadProperty(actor, "is_profession_warrior") is bool warrior) || !warrior)
				{
					continue;
				}
				try
				{
					// UnitProfession: Nothing=0, Baby=1, Unit=2 — обычный горожанин.
					Type t = Patches.FindGameType("UnitProfession");
					if (t != null)
					{
						object unitValue = Enum.ToObject(t, 2);
						GameRefs.Invoke(actor, "setProfession", unitValue, true);
						sent++;
					}
				}
				catch (Exception)
				{
				}
			}
		}
	}

	// ---------------------------------------------------------------- помощники

	/// <summary>Сдвигает счастье всех жителей государства (в WorldBox влияет на восстания и работу).</summary>
	private static void NudgeHappiness(Kingdom kingdom, int delta)
	{
		List<City> cities = GameRefs.KingdomCities(kingdom);
		if (cities == null)
		{
			return;
		}
		for (int i = 0; i < cities.Count; i++)
		{
			List<Actor> units = cities[i]?.units;
			if (units == null)
			{
				continue;
			}
			for (int u = 0; u < units.Count; u++)
			{
				Actor actor = units[u];
				if (actor == null)
				{
					continue;
				}
				try
				{
					IdeologyEffects.ApplyHappiness(actor, delta);
				}
				catch (Exception)
				{
				}
			}
		}
	}

	/// <summary>Добавляет золото в города государства.</summary>
	/// <summary>Лечит жителей государства (доля от максимума здоровья).</summary>
	private static void Heal(Kingdom kingdom, float percent)
	{
		List<City> cities = GameRefs.KingdomCities(kingdom);
		if (cities == null)
		{
			return;
		}
		for (int i = 0; i < cities.Count; i++)
		{
			List<Actor> units = cities[i]?.units;
			if (units == null)
			{
				continue;
			}
			for (int u = 0; u < units.Count; u++)
			{
				if (units[u] == null)
				{
					continue;
				}
				try
				{
					GameRefs.Invoke(units[u], "restoreHealthPercent", percent);
				}
				catch (Exception)
				{
				}
			}
		}
	}

	/// <summary>0.34: выдать ресурс державе извне (помощь КМУД). Открытая обёртка над GiveGold.</summary>
	public static bool GrantResource(Kingdom kingdom, string resource, int amount)
	{
		return GiveGold(kingdom, capitalOnly: false, amount: amount, resource: resource);
	}

	private static bool GiveGold(Kingdom kingdom, bool capitalOnly, int amount, string resource = "gold")
	{
		List<City> cities = GameRefs.KingdomCities(kingdom);
		if (cities == null || cities.Count == 0)
		{
			return false;
		}
		if (capitalOnly)
		{
			City capital = GameRefs.KingdomCapital(kingdom) ?? cities[0];
			return AddResource(capital, resource, amount);
		}
		bool applied = false;
		for (int i = 0; i < cities.Count; i++)
		{
			applied |= AddResource(cities[i], resource, amount);
		}
		return applied;
	}

	/// <summary>Обнуляет запасы золота — последствие долгового кризиса.</summary>
	private static void DrainGold(Kingdom kingdom)
	{
		List<City> cities = GameRefs.KingdomCities(kingdom);
		if (cities == null)
		{
			return;
		}
		for (int i = 0; i < cities.Count; i++)
		{
			City city = cities[i];
			if (city == null)
			{
				continue;
			}
			object have = GameRefs.Invoke(city, "getResourcesAmount", "gold")
				?? GameRefs.Invoke(city, "get_amount_gold")
				?? GameRefs.ReadProperty(city, "amount_gold");
			if (GameRefs.TryResourceValue(have, out double goldVal) && goldVal > 0)
			{
				int gold = (int)Math.Floor(goldVal);
				if (!GameRefs.TryInvoke(city, "takeResource", out _, "gold", gold))
				{
					AddResource(city, "gold", -gold);
				}
			}
		}
	}

	/// <summary>Меняет запас ресурса города.</summary>
	private static bool AddResource(City city, string resource, int amount)
	{
		if (city == null || amount == 0)
		{
			return false;
		}
		try
		{
			if (string.Equals(resource, "food", StringComparison.OrdinalIgnoreCase))
			{
				resource = "bread";
			}
			if (amount < 0)
			{
				return GameRefs.TryInvoke(city, "takeResource", out _, resource, -amount)
					|| GameRefs.TryInvoke(city, "addResourcesToRandomStockpile", out _, resource, amount);
			}
			bool ok = GameRefs.TryInvoke(city, "addResourcesToRandomStockpile", out _, resource, amount);
			if (ok && (string.Equals(resource, "bread", StringComparison.OrdinalIgnoreCase)
				|| string.Equals(resource, "meat", StringComparison.OrdinalIgnoreCase)))
			{
				GameRefs.Invoke(city, "updateTotalFood");
			}
			return ok;
		}
		catch (Exception)
		{
            return false;
		}
	}
}
