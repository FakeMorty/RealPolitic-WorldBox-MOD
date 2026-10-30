using System;
using System.Collections.Generic;
using NeoModLoader.services;
using RealPolitic.Core;

namespace RealPolitic;

/// <summary>
/// Игровой исполнитель гражданских войн и расколов.
///
/// С 0.23 ядро (<see cref="CivilWars"/>) решает только, КТО и ПОЧЕМУ восстал; этот класс
/// создаёт в WorldBox настоящее государство повстанцев и настоящую войну, а исход
/// (подавление / переворот / отделение) ядро читает из состояния мира. Здесь же
/// исполняются операции по итогам: передача городов повстанцам при перевороте и мир при отделении.
///
/// Все обращения к игре идут через безопасную рефлексию (<see cref="GameRefs"/>):
/// если сигнатура изменилась — действие пропускается с одной записью в лог,
/// годовой цикл и игра продолжают работать.
/// </summary>
public static class CivilWarExecutor
{
	/// <summary>Разрешено ли вообще создавать новые государства (настройка EnableSecessions).</summary>
	public static bool SecessionsEnabled = true;

	/// <summary>Разрешены ли гражданские войны (настройка EnableCivilWars).</summary>
	public static bool CivilWarsEnabled = true;

	private static readonly HashSet<string> _warned = new HashSet<string>();

	private static void Warn(string what)
	{
		if (_warned.Add(what))
		{
			LogService.LogError("[Real Politic] API гражданской войны недоступен: " + what);
		}
	}

	/// <summary>
	/// Исполняет отделение в игре: столица мятежа образует новое королевство
	/// (нативный механизм WorldBox), остальные мятежные города переводятся к нему,
	/// начинается война за независимость.
	/// </summary>
	public static bool ExecuteSecession(PendingSecession pending, MapBox map, out int gameKingdomId)
	{
		gameKingdomId = 0;
		if (!SecessionsEnabled || pending == null || map == null || pending.CityIds.Count == 0)
		{
			return false;
		}
		try
		{
			Kingdom parent = FindKingdom(map, pending.ParentKingdomId);
			if (parent == null)
			{
				return false;
			}
			List<City> cities = GameRefs.KingdomCities(parent);
			if (cities == null)
			{
				return false;
			}
			// Город-столица мятежа: первый в списке ядра.
			City capital = FindCity(cities, pending.CityIds[0]);
			if (capital == null)
			{
				return false;
			}
			string capitalName = GameRefs.ReadProperty(capital, "name") as string
				?? ((NanoObject)(object)capital).name;
			if (!DiplomacyExecutor.TrySecedeCity(parent, capital, out Kingdom rebelKingdom) || rebelKingdom == null)
			{
				Warn("не удалось образовать государство повстанцев из города " + pending.CityIds[0]);
				return false;
			}
			// Повстанческое государство получает имя по отделившемуся городу и строю,
			// за который воюет: топоним — столица мятежа, форма — из 20+ вариантов её идеологии.
			if (!string.IsNullOrWhiteSpace(capitalName))
			{
				string rebelName = IdeologyStateNames.GenerateStateName(
					pending.Ideology,
					capitalName,
					capitalName,
					pending.ProvisionalKingdomId,
					pending.Year,
					null,
					GameRefs.CityBiomeId(capital),
					GameRefs.CityCultureName(capital));
				GameRefs.TryRenameKingdom(rebelKingdom, rebelName);
			}
			// Остальные мятежные города переводим в новое королевство.
			for (int i = 1; i < pending.CityIds.Count; i++)
			{
				City city = FindCity(cities, pending.CityIds[i]);
				if (city != null)
				{
					MoveCityToKingdom(city, rebelKingdom);
				}
			}
			// Этап 25: вождь мятежа — реальный правитель новой державы, а не фантом.
			Actor leader = GameRefs.ReadProperty(capital, "leader") as Actor
				?? GameRefs.Invoke(capital, "getLeader") as Actor;
			if (leader != null)
			{
				GameRefs.TrySetKingOnKingdom(rebelKingdom, leader, quiet: true);
				GameRefs.TryReassignKingdom(leader, rebelKingdom);
			}
			gameKingdomId = KingdomId(rebelKingdom);
			if (gameKingdomId != 0 && pending.DeclareWar && !DiplomacyExecutor.AreAtWar(parent, rebelKingdom))
			{
				// TrySecedeCity уже пытался начать войну за независимость; страхуемся — без войны
				// гражданской войны нет, и ядро посчитало бы это «миром» (отделением).
				DiplomacyExecutor.TryDeclareWar(rebelKingdom, parent);
			}
			return gameKingdomId != 0;
		}
		catch (Exception ex)
		{
			Warn("ExecuteSecession: " + ex.Message);
			return false;
		}
	}

	/// <summary>
	/// Исполняет операцию по итогам гражданской войны: переворот (все города метрополии
	/// переходят повстанцам, старое королевство исчезает) или мир (отделение состоялось).
	/// </summary>
	public static bool ExecuteWorldOp(PendingWorldOp op, MapBox map)
	{
		if (op == null || map == null)
		{
			return false;
		}
		try
		{
			Kingdom from = FindKingdom(map, op.FromKingdomId);
			Kingdom to = FindKingdom(map, op.ToKingdomId);
			if (to == null)
			{
				return false;
			}
			switch (op.Type)
			{
				case WorldOpType.TransferCities:
				{
					int moved = 0;
					if (from != null)
					{
						List<City> cities = GameRefs.KingdomCities(from);
						if (cities != null)
						{
							List<City> snapshot = new List<City>(cities);
							for (int i = 0; i < snapshot.Count; i++)
							{
								City city = snapshot[i];
								if (city == null)
								{
									continue;
								}
								if (op.CityIds.Count > 0 && !op.CityIds.Contains((int)((NanoObject)city).id))
								{
									continue;
								}
								MoveCityToKingdom(city, to);
								moved++;
							}
						}
						DiplomacyExecutor.TryMakePeace(from, to);
					}
					if (!string.IsNullOrWhiteSpace(op.OriginalKingdomName))
					{
						GameRefs.TryRenameKingdom(to, op.OriginalKingdomName);
					}
					if (op.LeaderActorId > 0)
					{
						Actor rebelLeader = GameRefs.FindActorById(op.LeaderActorId);
						if (rebelLeader != null)
						{
							GameRefs.TryReassignKingdom(rebelLeader, to);
							GameRefs.TrySetKingOnKingdom(to, rebelLeader, quiet: true);
						}
					}
					return moved > 0 || !string.IsNullOrWhiteSpace(op.OriginalKingdomName);
				}
				case WorldOpType.MakePeace:
					return from != null && DiplomacyExecutor.TryMakePeace(from, to);
				default:
					return false;
			}
		}
		catch (Exception ex)
		{
			Warn("ExecuteWorldOp: " + ex.Message);
			return false;
		}
	}

	/// <summary>
	/// Годовые эффекты идущей гражданской войны: мятежные города теряют лояльность,
	/// война съедает продовольствие и казну, жители обеих сторон несчастны.
	/// </summary>
	public static void ApplyWarEffects(Kingdom kingdom, KingdomPolitics state, int year)
	{
		if (!CivilWarsEnabled || kingdom == null || state?.CivilWar == null)
		{
			return;
		}
		try
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
				// Тыл устал от братоубийственной войны.
				NudgeCityLoyalty(city, -2);
				NudgeCityHappiness(city, -4);
			}
			// Война требует снабжения: часть запасов сгорает.
			TakeResource(kingdom, "bread", 6);
			TakeResource(kingdom, "gold", 4);
		}
		catch (Exception ex)
		{
			Warn("ApplyWarEffects: " + ex.Message);
		}
	}

	/// <summary>
	/// Действие стратегического ИИ «подавить мятеж»: верные города поднимают ополчение,
	/// позиция повстанцев в ядре ослабевает.
	/// </summary>
	public static bool SuppressRebellion(Kingdom kingdom, KingdomPolitics state, out string result)
	{
		result = string.Empty;
		if (!CivilWarsEnabled || kingdom == null || state?.CivilWar == null)
		{
			result = GameText.Localized("RP_ACT_NO_REBELLION", "мятежа нет");
			return false;
		}
		try
		{
			CivilWarState cw = state.CivilWar;
			List<City> cities = GameRefs.KingdomCities(kingdom);
			int rallied = 0;
			if (cities != null)
			{
				for (int i = 0; i < cities.Count; i++)
				{
					City city = cities[i];
					if (city == null || cw.HoldsCity((int)((NanoObject)city).id))
					{
						continue;
					}
					if (RallyCityDefence(city))
					{
						rallied++;
					}
					NudgeCityLoyalty(city, 4);
				}
			}
			// Карательный поход — это настоящая война с государством повстанцев.
			Kingdom rebels = FindKingdom(World.world, cw.RebelKingdomId);
			if (rebels != null && !DiplomacyExecutor.AreAtWar(kingdom, rebels))
			{
				DiplomacyExecutor.TryDeclareWar(kingdom, rebels);
			}
			result = rallied > 0
				? GameText.Localized("RP_ACT_SUPPRESS_RALLIED", "верные города подняли ополчение") + " (" + rallied + ")"
				: GameText.Localized("RP_ACT_SUPPRESS_MARCH", "войска двинуты на мятежников");
			return true;
		}
		catch (Exception ex)
		{
			Warn("SuppressRebellion: " + ex.Message);
			result = GameText.Localized("RP_ACT_SUPPRESS_FAILED", "подавление мятежа не удалось") + ": " + ex.Message;
			return false;
		}
	}

	/// <summary>
	/// Читает лидера города для ядра: имя, пол и id юнита.
	/// Если лидера нет (или API изменилось) — берём старшего жителя; если и его нет — пустые значения.
	/// </summary>
	public static (string Name, bool IsFemale, int ActorId) ReadCityLeader(MapBox map, int kingdomId, int cityId)
	{
		try
		{
			Kingdom kingdom = FindKingdom(map, kingdomId);
			if (kingdom == null)
			{
				return (string.Empty, false, 0);
			}
			List<City> cities = GameRefs.KingdomCities(kingdom);
			City city = FindCity(cities, cityId);
			if (city == null)
			{
				return (string.Empty, false, 0);
			}
			Actor leader = (GameRefs.ReadProperty(city, "leader") as Actor) ?? GameRefs.FindAdultResident(city);
			if (leader == null)
			{
				return (string.Empty, false, 0);
			}
			return (GameText.ActorName(leader) ?? string.Empty, GameText.ActorIsFemale(leader), GameText.ActorId(leader));
		}
		catch (Exception ex)
		{
			Warn("ReadCityLeader: " + ex.Message);
			return (string.Empty, false, 0);
		}
	}

	// ---------------------------------------------------------------- помощники

	public static Kingdom FindKingdom(MapBox map, int kingdomId)
	{
		List<Kingdom> list = GameRefs.KingdomList(map);
		if (list == null)
		{
			return null;
		}
		for (int i = 0; i < list.Count; i++)
		{
			if (list[i] != null && KingdomId(list[i]) == kingdomId)
			{
				return list[i];
			}
		}
		return null;
	}

	private static City FindCity(List<City> cities, int cityId)
	{
		if (cities == null)
		{
			return null;
		}
		for (int i = 0; i < cities.Count; i++)
		{
			if (cities[i] != null && (int)((NanoObject)cities[i]).id == cityId)
			{
				return cities[i];
			}
		}
		return null;
	}

	private static int KingdomId(Kingdom kingdom)
	{
		try
		{
			return kingdom == null ? 0 : (int)((NanoObject)kingdom).id;
		}
		catch (Exception)
		{
			return 0;
		}
	}

	/// <summary>Перевод города в другое королевство: перебираем известные варианты API.</summary>
	public static void MoveCityToKingdom(City city, Kingdom kingdom)
	{
		if (city == null || kingdom == null)
		{
			return;
		}
		try
		{
			// WorldBox 0.50+: joinAnotherKingdom и setKingdom возвращают void (null при Invoke),
			// поэтому проверяем фактическую смену city.kingdom после каждого шага, не вызывая их подряд.
			if (GameRefs.TryInvoke(city, "joinAnotherKingdom", out _, kingdom, false, true)
				&& ReferenceEquals(city.kingdom, kingdom))
			{
				return;
			}
			if (GameRefs.TryInvoke(city, "setKingdom", out _, kingdom, false)
				&& ReferenceEquals(city.kingdom, kingdom))
			{
				return;
			}
			if (GameRefs.TryInvoke(city, "setKingdom", out _, kingdom)
				&& ReferenceEquals(city.kingdom, kingdom))
			{
				return;
			}
			GameRefs.Invoke(kingdom, "addCity", city);
			if (city.kingdom != null && !ReferenceEquals(city.kingdom, kingdom))
			{
				Warn("не удалось перевести город " + (int)((NanoObject)city).id + " в государство повстанцев");
			}
		}
		catch (Exception ex)
		{
			Warn("MoveCityToKingdom: " + ex.Message);
		}
	}

	/// <summary>Сдвиг лояльности города: метод, затем поле данных (API лояльности менялся между версиями).</summary>
	private static void NudgeCityLoyalty(City city, int delta)
	{
		try
		{
			object current = GameRefs.Invoke(city, "getLoyalty", false) ?? GameRefs.Invoke(city, "getLoyalty");
			if (!(current is int loyalty))
			{
				return;
			}
			int target = Math.Max(0, Math.Min(100, loyalty + delta));
			if (GameRefs.Invoke(city, "setLoyalty", target) == null)
			{
				// В 0.51 нет setLoyalty: лояльность кэшируется в приватном _loyalty_cached
				// (getLoyalty(false) читает кэш). Пишем в кэш — до следующего пересчёта
				// игра видит наше значение.
				if (!GameRefs.WriteMember(city, "_loyalty_cached", target))
				{
					object data = GameRefs.ReadProperty(city, "data");
					if (data != null)
					{
						GameRefs.WriteMember(data, "loyalty", target);
					}
				}
			}
		}
		catch (Exception ex)
		{
			Warn("NudgeCityLoyalty: " + ex.Message);
		}
	}

	/// <summary>Сдвиг настроения жителей города (через безопасную обёртку эффектов идеологий).</summary>
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

	/// <summary>Город поднимает ополчение: часть жителей становится воинами.</summary>
	private static bool RallyCityDefence(City city)
	{
		try
		{
			int current = (GameRefs.Invoke(city, "countWarriors") is int c) ? c : 0;
			int max = (GameRefs.Invoke(city, "getMaxWarriors") is int m) ? m : 0;
			if (max <= 0 || current >= max)
			{
				return false;
			}
			List<Actor> units = city.units;
			if (units == null)
			{
				return false;
			}
			Type professionType = Patches.FindGameType("UnitProfession");
			object warrior = professionType != null ? Enum.ToObject(professionType, 3) : null;
			int made = 0;
			for (int i = 0; i < units.Count && made < 3; i++)
			{
				Actor actor = units[i];
				if (actor == null || GameRefs.IsBoatUnit(actor))
				{
					continue;
				}
				if (GameRefs.ReadProperty(actor, "is_profession_warrior") is bool isWarrior && isWarrior)
				{
					continue;
				}
				if (GameRefs.ReadProperty(actor, "is_profession_king") is bool isKing && isKing)
				{
					continue;
				}
				if (GameRefs.ReadProperty(actor, "is_profession_leader") is bool isLeader && isLeader)
				{
					continue;
				}
				if (GameText.ActorAge(actor) > 0 && GameText.ActorAge(actor) < 16)
				{
					continue;
				}
				if (GameRefs.TryInvoke(city, "makeWarrior", out _, actor)
					|| (GameRefs.TryInvoke(city, "tryToMakeWarrior", out object ok, actor) && ok is bool b && b))
				{
					made++;
					continue;
				}
				if (warrior != null && GameRefs.TryInvoke(actor, "setProfession", out _, warrior, true))
				{
					made++;
				}
			}
			return made > 0;
		}
		catch (Exception ex)
		{
			Warn("RallyCityDefence: " + ex.Message);
			return false;
		}
	}

	/// <summary>Изъятие части ресурса королевства (война требует снабжения).</summary>
	private static void TakeResource(Kingdom kingdom, string resource, int amount)
	{
		try
		{
			List<City> cities = GameRefs.KingdomCities(kingdom);
			if (cities == null || cities.Count == 0)
			{
				return;
			}
			City capital = GameRefs.KingdomCapital(kingdom) ?? cities[0];
			if (capital == null)
			{
				return;
			}
			object current = GameRefs.Invoke(capital, "getResourcesAmount", resource)
				?? (string.Equals(resource, "gold", StringComparison.OrdinalIgnoreCase)
					? (GameRefs.Invoke(capital, "get_amount_gold") ?? GameRefs.ReadProperty(capital, "amount_gold"))
					: null);
			if (GameRefs.TryResourceValue(current, out double have) && have > amount)
			{
				GameRefs.Invoke(capital, "takeResource", resource, amount);
			}
		}
		catch (Exception)
		{
			// Ресурсная часть — необязательная: если API другой, война просто не ест запасы.
		}
	}
}
