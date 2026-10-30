using System;
using System.Collections.Generic;
using NeoModLoader.services;
using RealPolitic.Core;

namespace RealPolitic;

/// <summary>
/// 0.46: расселение — исполнение в игре.
///
/// Решение «пора ли закладывать город» принимает ядро (<see cref="SettlementExpansion"/>),
/// а здесь мы только находим подходящего жителя и просим игру основать город.
///
/// Как это делается родными средствами WorldBox:
///   • берём обычного жителя державы (не правителя, не воина, не главу города);
///   • проверяем, что игра сама разрешает ему основать город (Actor.canBuildNewCity);
///   • зовём Actor.buildCityAndStartCivilization — житель уходит и ставит новый город.
/// Если игра откажет (метода нет или он вернул false), мод просто не расселяется
/// и пишет одну строку в журнал — мир не ломается.
/// </summary>
public static class SettlementRuntime
{
	private static readonly Dictionary<int, int> _lastFoundedYear = new Dictionary<int, int>();
	private static int _loggedYear = -1;

	public static void Reset()
	{
		_lastFoundedYear.Clear();
		_loggedYear = -1;
	}

	public static void AnnualTick(PoliticalWorld world, MapBox map, int year, IReadOnlyDictionary<int, KingdomInput> inputs)
	{
		try
		{
			if (world == null || map == null || inputs == null || world.Params == null
				|| !world.Params.EnableSettlementExpansion)
			{
				return;
			}
			// Раз в год, а не на каждый кадр: города не появляются чаще, чем раз в 12 лет.
			List<Kingdom> kingdoms = GameRefs.KingdomList(map);
			if (kingdoms == null)
			{
				return;
			}
			for (int k = 0; k < kingdoms.Count; k++)
			{
				Kingdom kingdom = kingdoms[k];
				if (kingdom == null)
				{
					continue;
				}
				int id = (int)((NanoObject)kingdom).id;
				KingdomPolitics state = world.GetKingdom(id);
				if (state == null || !inputs.TryGetValue(id, out KingdomInput input))
				{
					continue;
				}
				_lastFoundedYear.TryGetValue(id, out int lastFounded);
				int freeLand = CountFreeLand(map, kingdom);
				if (!SettlementExpansion.ShouldFound(state, input, freeLand, year, lastFounded, world.Params, out string why))
				{
					if (year != _loggedYear && freeLand > 0 && freeLand < 40 && input.Population >= SettlementExpansion.MinPopulation)
					{
						LogService.LogInfo("[Real Politic] расселение: " + world.KingdomNameOf(id) + " — " + why);
					}
					continue;
				}
				if (TryFoundCity(kingdom, input, out string result))
				{
					_lastFoundedYear[id] = year;
					LogService.LogInfo("[Real Politic] расселение: " + world.KingdomNameOf(id) + " основала город — " + result);
					Publish(world, id, year);
				}
				else if (year != _loggedYear)
				{
					_loggedYear = year;
					LogService.LogInfo("[Real Politic] расселение: игра не дала основать город (" + result + ")");
				}
			}
		}
		catch (Exception ex)
		{
			LogService.LogError("[Real Politic] расселение: " + ex.Message);
		}
	}

	/// <summary>Сколько свободных пригодных клеток вокруг державы (грубая оценка по карте).</summary>
	private static int CountFreeLand(MapBox map, Kingdom kingdom)
	{
		try
		{
			List<City> cities = GameRefs.KingdomCities(kingdom);
			if (cities == null || cities.Count == 0)
			{
				return 0;
			}
			// Считаем клетки земли без города и без чужой державы в радиусе вокруг городов.
			object tiles = GameRefs.ReadMember(map, "tiles_list") ?? GameRefs.ReadMember(map, "tiles");
			if (!(tiles is System.Collections.IEnumerable list))
			{
				return 0;
			}
			int free = 0;
			int samples = 0;
			foreach (object tileObj in list)
			{
				if (tileObj == null)
				{
					continue;
				}
				samples++;
				object top = GameRefs.Invoke(tileObj, "getTopTileType");
				string type = top?.ToString() ?? string.Empty;
				if (type.Length == 0)
				{
					continue;
				}
				bool land = type.Contains("grass") || type.Contains("soil") || type.Contains("forest")
					|| type.Contains("sand") || type.Contains("snow") || type.Contains("mount");
				if (!land)
				{
					continue;
				}
				object city = GameRefs.ReadMember(tileObj, "city");
				object kingdomOnTile = GameRefs.ReadMember(tileObj, "kingdom");
				if (city == null && kingdomOnTile == null)
				{
					free++;
				}
				if (free >= 200)
				{
					break;
				}
			}
			return free;
		}
		catch (Exception)
		{
			return 0;
		}
	}

	/// <summary>Просим игру основать город. true — получилось.</summary>
	private static bool TryFoundCity(Kingdom kingdom, KingdomInput input, out string result)
	{
		result = string.Empty;
		try
		{
			List<City> cities = GameRefs.KingdomCities(kingdom);
			if (cities == null || cities.Count == 0)
			{
				result = "нет городов";
				return false;
			}
			for (int c = 0; c < cities.Count; c++)
			{
				City city = cities[c];
				if (city == null || !(GameRefs.ReadMember(city, "units") is List<Actor> units))
				{
					continue;
				}
				int stride = Math.Max(1, units.Count / 8);
				for (int i = 0; i < units.Count; i += stride)
				{
					Actor actor = units[i];
					if (actor == null || GameRefs.IsBoatUnit(actor) || GameRefs.IsWarrior(actor))
					{
						continue;
					}
					if (GameRefs.IsRulerActor(actor, kingdom) || GameRefs.IsCityLeaderActor(actor, city))
					{
						continue;
					}
					if (GameRefs.Invoke(actor, "canBuildNewCity") is bool can && !can)
					{
						continue;
					}
					object built = GameRefs.Invoke(actor, "buildCityAndStartCivilization");
					if (built is bool ok && ok)
					{
						result = GameRefs.ActorDisplayName(actor);
						return true;
					}
					// Метод есть, но вернул не bool: считаем, что город поставлен,
					// если у жителя появился новый город, отличный от прежнего.
					if (built == null && GameRefs.Invoke(actor, "getCity") is City now && !ReferenceEquals(now, city))
					{
						result = GameRefs.ActorDisplayName(actor);
						return true;
					}
				}
			}
			result = "не нашлось свободного поселенца";
			return false;
		}
		catch (Exception ex)
		{
			result = ex.Message;
			return false;
		}
	}

	private static void Publish(PoliticalWorld world, int kingdomId, int year)
	{
		try
		{
			if (world?.Bus == null)
			{
				return;
			}
			PolEvent evt = new PolEvent(PolEventType.CityFounded, "RP_EV_CITY_FOUNDED") { Year = year };
			evt.Severity = Severity.Minor;
			evt.KingdomId = kingdomId;
			world.Bus.Publish(evt.With("kingdom", world.KingdomNameOf(kingdomId)));
		}
		catch (Exception)
		{
		}
	}
}
