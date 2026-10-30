using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using NeoModLoader.services;
using RealPolitic.Core;

namespace RealPolitic;

public static class GameRefs
{
	private static readonly HashSet<string> _warned = new HashSet<string>();

	private static void Warn(string what)
	{
		if (_warned.Add(what))
		{
			LogService.LogError("[Real Politic] не найден игровой API: " + what);
		}
	}

	/// <summary>Безопасно получает живой список государств текущего мира.</summary>
	public static List<Kingdom> KingdomList(MapBox map)
	{
		try
		{
			if (map == null || map.kingdoms == null) return null;
			var list = ((CoreSystemManager<Kingdom, KingdomData>)(object)map.kingdoms).list;
			if (ReadMember(map.kingdoms, "dict") is IDictionary dict && dict.Count > 0)
			{
				List<Kingdom> combined = null;
				foreach (object val in dict.Values)
				{
					if (!(val is Kingdom k)) continue;
					if (ReadMember(k, "_alive") is bool alive && !alive) continue;
					if (list != null && list.Contains(k)) continue;
					if (combined == null)
					{
						combined = list != null ? new List<Kingdom>(list) : new List<Kingdom>();
					}
					if (!combined.Contains(k)) combined.Add(k);
				}
				if (combined != null) return combined;
			}
			return list;
		}
		catch (Exception ex)
		{
			Warn("Map.kingdoms.list: " + ex.Message);
			return null;
		}
	}

	/// <summary>Безопасно получает живой список городов текущего мира.</summary>
	public static List<City> CityList(MapBox map)
	{
		try
		{
			if (map == null || map.cities == null) return null;
			return ((CoreSystemManager<City, CityData>)(object)map.cities).list;
		}
		catch (Exception ex)
		{
			Warn("Map.cities.list: " + ex.Message);
			return null;
		}
	}

	public static int KingdomPopulation(Kingdom kingdom)
	{
		try
		{
			// WorldBox 0.50+: Kingdom.getPopulationPeople() исключает корабли (_boats) из числа жителей.
			if (Invoke(kingdom, "getPopulationPeople") is int people && people > 0)
			{
				return people;
			}
			List<Actor> units = ((MetaObject<KingdomData>)(object)kingdom)?.units;
			if (units == null)
			{
				return 0;
			}
			int count = 0;
			for (int i = 0; i < units.Count; i++)
			{
				Actor u = units[i];
				if (u != null && !IsBoatUnit(u))
				{
					count++;
				}
			}
			return count;
		}
		catch (Exception ex)
		{
			Warn("Kingdom.units: " + ex.Message);
			return 0;
		}
	}

	/// <summary>
	/// 0.43: население быстрым путём самой игры (getPopulationPeople), без обхода юнитов.
	/// Возвращает false, если такого метода нет — тогда вызывающий считает сам.
	/// </summary>
	public static bool TryKingdomPopulation(Kingdom kingdom, out int population)
	{
		population = 0;
		try
		{
			if (Invoke(kingdom, "getPopulationPeople") is int people && people > 0)
			{
				population = people;
				return true;
			}
		}
		catch (Exception)
		{
		}
		return false;
	}

	/// <summary>0.43: число воинов быстрым путём игры, без обхода юнитов.</summary>
	public static bool TryKingdomWarriors(Kingdom kingdom, out int warriors)
	{
		warriors = 0;
		try
		{
			if (Invoke(kingdom, "countTotalWarriors") is int counted && counted >= 0)
			{
				warriors = counted;
				return true;
			}
			if (ReadProperty(kingdom, "warriors_count") is int result)
			{
				warriors = result;
				return true;
			}
		}
		catch (Exception)
		{
		}
		return false;
	}

	/// <summary>
	/// 0.43: население и воины за ОДИН проход по юнитам державы.
	/// Раньше это были два отдельных прохода (KingdomPopulation и KingdomWarriors),
	/// и в каждом на юнита вызывалась рефлексия — на больших мирах это главная
	/// статья расходов при сборке держав.
	/// </summary>
	public static void KingdomCountsByUnits(Kingdom kingdom, out int population, out int warriors)
	{
		population = 0;
		warriors = 0;
		try
		{
			List<Actor> units = ((MetaObject<KingdomData>)(object)kingdom)?.units;
			if (units == null)
			{
				return;
			}
			for (int i = 0; i < units.Count; i++)
			{
				Actor u = units[i];
				if (u == null || IsBoatUnit(u))
				{
					continue;
				}
				population++;
				if (IsWarrior(u))
				{
					warriors++;
				}
			}
		}
		catch (Exception ex)
		{
			Warn("Kingdom.units: " + ex.Message);
		}
	}

	public static int KingdomWarriors(Kingdom kingdom)
	{
		try
		{
			// WorldBox 0.50+: Kingdom.countTotalWarriors(); старые сборки — warriors_count.
			if (Invoke(kingdom, "countTotalWarriors") is int counted)
			{
				return counted;
			}
			if (ReadProperty(kingdom, "warriors_count") is int result)
			{
				return result;
			}
			List<Actor> units = ((MetaObject<KingdomData>)(object)kingdom).units;
			if (units == null)
			{
				return 0;
			}
			int num = 0;
			for (int i = 0; i < units.Count; i++)
			{
				if (units[i] != null && IsWarrior(units[i]))
				{
					num++;
				}
			}
			return num;
		}
		catch (Exception ex)
		{
			Warn("Kingdom warriors: " + ex.Message);
			return 0;
		}
	}

	public static bool IsWarrior(Actor actor)
	{
		try
		{
			// WorldBox 0.50+: Actor.isWarrior(); затем старые варианты.
			if (Invoke(actor, "isWarrior") is bool method)
			{
				return method;
			}
			if (ReadProperty(actor, "is_profession_warrior") is bool prop)
			{
				return prop;
			}
			if (ReadProperty(actor, "is_warrior") is bool result)
			{
				return result;
			}
			return ReadProperty(actor, "profession")?.ToString().ToLowerInvariant().Contains("warrior") ?? false;
		}
		catch (Exception)
		{
			return false;
		}
	}

	public static List<City> KingdomCities(Kingdom kingdom)
	{
		try
		{
			return kingdom.cities;
		}
		catch (Exception ex)
		{
			Warn("Kingdom.cities: " + ex.Message);
			return null;
		}
	}

	/// <summary>
	/// Настоящая столица королевства (поле Kingdom.capital в игре).
	/// Порядок Kingdom.cities столицу НЕ гарантирует — по хронологии завоеваний
	/// первым в списке может оказаться любой город (в 0.17.0 это приводило к тому,
	/// что «отделялась» столица). При недоступности поля — запасной cities[0].
	/// </summary>
	public static City KingdomCapital(Kingdom kingdom)
	{
		try
		{
			if (kingdom != null && ReadProperty(kingdom, "capital") is City capital && capital != null)
			{
				return capital;
			}
		}
		catch (Exception ex)
		{
			Warn("Kingdom.capital: " + ex.Message);
		}
		List<City> cities = KingdomCities(kingdom);
		return cities != null && cities.Count > 0 ? cities[0] : null;
	}

	public static string CityBiomeId(City city)
	{
		try
		{
			if (city == null)
			{
				return string.Empty;
			}
			object tile = Invoke(city, "getTile", false) ?? Invoke(city, "getTile") ?? ReadMember(city, "_city_tile");
			if (tile == null)
			{
				return string.Empty;
			}
			object tileType = ReadMember(tile, "Type") ?? ReadMember(tile, "main_type");
			if (tileType != null)
			{
				string biomeId = ReadMember(tileType, "biome_id") as string
					?? ReadMember(ReadMember(tileType, "biome_asset"), "id") as string
					?? ReadMember(tileType, "id") as string;
				if (!string.IsNullOrWhiteSpace(biomeId))
				{
					return biomeId;
				}
			}
			return string.Empty;
		}
		catch (Exception)
		{
			return string.Empty;
		}
	}

	public static int CityPopulation(City city)
	{
		try
		{
			// 0.51.2: у City нет поля units_total; население через getPopulationPeople() или status.population.
			object obj = Invoke(city, "getPopulationPeople");
			if (obj is int result)
			{
				return result;
			}
			if (obj is long num)
			{
				return (int)num;
			}
			object status = ReadProperty(city, "status");
			if (status != null && ReadProperty(status, "population") is int population)
			{
				return population;
			}
			// Запасной вариант: число юнитов города.
			object units = ReadProperty(city, "units");
			if (units is System.Collections.ICollection collection)
			{
				return collection.Count;
			}
			return 0;
		}
		catch (Exception ex)
		{
			Warn("City population: " + ex.Message);
			return 0;
		}
	}

	public static bool CityStarving(City city)
	{
		try
		{
			if (city == null)
			{
				return false;
			}
			int pop = CityPopulation(city);
			int? totalFood = null;
			if ((TryInvoke(city, "getTotalFood", out object tfObj) || TryInvoke(city, "countFoodTotal", out tfObj) || TryInvoke(city, "countFood", out tfObj))
				&& tfObj is int tf && tf >= 0)
			{
				totalFood = tf;
			}
			// 0.51.2: MetaObject<CityData>.countStarving() считает реально голодающих (isStarving),
			// в отличие от city.status.hungry (жители, проголодавшиеся перед обычным приёмом пищи).
			if (TryInvoke(city, "countStarving", out object stObj) && stObj is int starving && starving >= 0)
			{
				if (starving > 0)
				{
					if (totalFood.HasValue && pop > 0 && totalFood.Value >= pop && starving * 5 < Math.Max(5, pop))
					{
						return false;
					}
					return true;
				}
				if (totalFood.HasValue && totalFood.Value > Math.Max(4, pop / 3))
				{
					return false;
				}
			}
			object status = ReadProperty(city, "status");
			if (status != null && ReadProperty(status, "hungry") is int hungry && hungry > 0)
			{
				// Если в амбарах полно еды (>= 0.75 еды на жителя, мин. 10), а проголодалась
				// лишь малая доля (< 1/3 населения) — это обычный обеденный цикл, а не голод.
				if (totalFood.HasValue && pop > 0 && totalFood.Value >= Math.Max(10, (pop * 3) / 4) && hungry * 3 < pop)
				{
					return false;
				}
				return true;
			}
			return false;
		}
		catch (Exception)
		{
			return false;
		}
	}

	public static bool KingdomStarving(Kingdom kingdom)
	{
		List<City> list = KingdomCities(kingdom);
		if (list == null || list.Count == 0)
		{
			return false;
		}
		int num = 0;
		for (int i = 0; i < list.Count; i++)
		{
			if (list[i] != null && CityStarving(list[i]))
			{
				num++;
			}
		}
		return num * 2 > list.Count;
	}

	public static int CityReligionId(City city)
	{
		return CultureId(city, "religion");
	}

	public static int CityLanguageId(City city)
	{
		return CultureId(city, "language");
	}

	public static int CityCultureId(City city)
	{
		return CultureId(city, "culture");
	}

	public static string CityReligionName(City city)
	{
		return CultureName(city, "religion");
	}

	public static string CityLanguageName(City city)
	{
		return CultureName(city, "language");
	}

	public static string CityCultureName(City city)
	{
		return CultureName(city, "culture");
	}

	/// <summary>
	/// Возвращает отображаемое имя религии/языка/культуры города.
	/// Прослойка безопасна: при любом несовпадении API возвращает пустую строку, не прерывая игру.
	/// </summary>
	private static string CultureName(City city, string kind)
	{
		try
		{
			object obj = ReadProperty(city, kind);
			if (obj == null)
			{
				return string.Empty;
			}
			// name — виртуальное свойство (CoreSystemObject<T>.name → data.name).
			object name = ReadProperty(obj, "name");
			if (name != null && !string.IsNullOrEmpty(name.ToString()))
			{
				return name.ToString();
			}
			// Запасной вариант: data.name.
			object data = ReadProperty(obj, "data");
			if (data != null)
			{
				object dataName = ReadProperty(data, "name");
				if (dataName != null && !string.IsNullOrEmpty(dataName.ToString()))
				{
					return dataName.ToString();
				}
			}
			// Последний вариант: идентификатор, чтобы игрок хотя бы видел, что объект появился.
			object id = ReadProperty(obj, "id");
			if (id != null)
			{
				return kind + " №" + id;
			}
			return string.Empty;
		}
		catch (Exception ex)
		{
			Warn("City." + kind + " name: " + ex.Message);
			return string.Empty;
		}
	}

	private static int CultureId(City city, string kind)
	{
		try
		{
			object obj = ReadProperty(city, kind);
			if (obj == null)
			{
				return 0;
			}
			// В игре id — long (свойство), а не int.
			object id = ReadProperty(obj, "id");
			if (id is long num)
			{
				return (int)num;
			}
			if (id is int result)
			{
				return result;
			}
			return 0;
		}
		catch (Exception ex)
		{
			Warn("City." + kind + ": " + ex.Message);
			return 0;
		}
	}

	public static bool IsAtWar(Kingdom kingdom)
	{
		try
		{
			if (kingdom == null)
			{
				return false;
			}
			// WorldBox 0.50+: Kingdom.hasEnemies() и Kingdom.getWars(bool pRandom).
			if (Invoke(kingdom, "hasEnemies") is bool hasEnemies)
			{
				return hasEnemies;
			}
			if (Invoke(kingdom, "getWars", false) is IEnumerable warsEnum)
			{
				foreach (object w in warsEnum)
				{
					if (w != null && !(Invoke(w, "hasEnded") is bool ended && ended))
					{
						return true;
					}
				}
			}
			if (ReadProperty(kingdom, "wars") is ICollection collection)
			{
				return collection.Count > 0;
			}
			object obj = ReadProperty(kingdom, "at_war");
			bool flag = default(bool);
			int num;
			if (obj is bool)
			{
				flag = (bool)obj;
				num = 1;
			}
			else
			{
				num = 0;
			}
			return (byte)((uint)num & (flag ? 1u : 0u)) != 0;
		}
		catch (Exception ex)
		{
			Warn("Kingdom wars: " + ex.Message);
			return false;
		}
	}

	/// <summary>Воюют ли две державы между собой (WorldBox 0.50+: isInWarWith, старые стабы: isAtWarWith, реестр войн).</summary>
	public static bool AreKingdomsAtWar(Kingdom a, Kingdom b)
	{
		if (a == null || b == null || ReferenceEquals(a, b))
		{
			return false;
		}
		try
		{
			if (Invoke(a, "isInWarWith", b) is bool inWar && inWar)
			{
				return true;
			}
			if (Invoke(a, "isAtWarWith", b) is bool atWar && atWar)
			{
				return true;
			}
			return DiplomacyExecutor.AreAtWar(a, b);
		}
		catch (Exception)
		{
			return false;
		}
	}

	/// <summary>Вид/раса государства в WorldBox 0.50+ (species_id / getSpecies / original_actor_asset / race).</summary>
	public static string KingdomSpeciesId(Kingdom kingdom)
	{
		if (kingdom == null)
		{
			return string.Empty;
		}
		try
		{
			object raw = ReadProperty(kingdom, "species_id")
				?? Invoke(kingdom, "getSpecies")
				?? ReadProperty(ReadMember(kingdom, "data"), "original_actor_asset")
				?? ReadProperty(kingdom, "race");
			return raw?.ToString() ?? string.Empty;
		}
		catch (Exception)
		{
			return string.Empty;
		}
	}

	/// <summary>Вид/раса города в WorldBox 0.50+ (getCurrentSpecies / getSpecies / getMainSubspecies / race).</summary>
	public static string CitySpeciesId(City city)
	{
		if (city == null)
		{
			return string.Empty;
		}
		try
		{
			object raw = Invoke(city, "getCurrentSpecies")
				?? Invoke(city, "getSpecies")
				?? ReadProperty(Invoke(city, "getMainSubspecies"), "species_id")
				?? ReadProperty(city, "race");
			return raw?.ToString() ?? string.Empty;
		}
		catch (Exception)
		{
			return string.Empty;
		}
	}

	/// <summary>Центральный тайл города в WorldBox 0.50+ (getTile(false) / _city_tile / tile).</summary>
	public static object CityTile(City city)
	{
		if (city == null)
		{
			return null;
		}
		try
		{
			return Invoke(city, "getTile", false)
				?? ReadMember(city, "_city_tile")
				?? ReadProperty(city, "tile");
		}
		catch (Exception)
		{
			return null;
		}
	}

		/// <summary>Безопасно читает сумму ресурса по городам государства.</summary>
		public static float KingdomResource(Kingdom kingdom, string resource)
		{
			return TryKingdomResource(kingdom, resource, out float total) ? total : 0f;
		}

		/// <summary>
		/// Читает ресурс с признаком доступности API. Нулевой запас — валидное состояние
		/// и не должен подменяться безопасным fallback-значением стратегического ИИ.
		/// </summary>
		public static bool TryKingdomResource(Kingdom kingdom, string resource, out float total)
        {
            total=0f;
            try
            {
                var cities=KingdomCities(kingdom);if(cities==null)return false;
                bool isFood=string.Equals(resource,"food",StringComparison.OrdinalIgnoreCase);
                if(isFood)
                {
                    // Verified native aggregate: zero is valid, a missing or negative value is not.
                    if(Invoke(kingdom,"countTotalFood") is int kingdomFood && kingdomFood>=0)
                    {total=kingdomFood;return true;}
                    double food=0;bool complete=true;
                    foreach(var city in cities)
                    {
                        if(city==null)return false;
                        if(!(Invoke(city,"getTotalFood") is int amount) || amount<0){complete=false;break;}
                        food+=amount;
                    }
                    if(complete && food<=float.MaxValue){total=(float)food;return true;}
                }
                bool isGold=string.Equals(resource,"gold",StringComparison.OrdinalIgnoreCase);
                if(isGold)
                {
                    double goldSum=0;bool goldComplete=true;
                    foreach(var city in cities)
                    {
                        if(city==null)return false;
                        object rawGold=Invoke(city,"get_amount_gold") ?? ReadProperty(city,"amount_gold") ?? Invoke(city,"getResourcesAmount","gold");
                        if(!TryResourceValue(rawGold,out double gAmount)){goldComplete=false;break;}
                        goldSum+=gAmount;
                    }
                    if(goldComplete && goldSum<=float.MaxValue){total=(float)goldSum;return true;}
                }
                // Legacy per-resource route must cover EVERY requested city/key. A partial
                // sum is not an observed country total, and missing APIs are not zero stock.
                string[] keys=isFood?new[]{"bread","meat","fish","berries","wheat"}:new[]{resource};
                double sum=0;
                foreach(var city in cities)
                {
                    if(city==null)return false;
                    foreach(string key in keys)
                    {
                        if(!TryResourceValue(Invoke(city,"getResourcesAmount",key),out double amount))return false;
                        sum+=amount;if(sum>float.MaxValue)return false;
                    }
                }
                total=(float)sum;return true;
            }
            catch(Exception ex)
            {Warn("Kingdom resource "+resource+": "+ex.Message);total=0f;return false;}
        }
        public static bool TryResourceValue(object value,out double amount)
        {
            amount=value is int i?i:value is long l?l:value is float f?f:value is double d?d:double.NaN;
            return !double.IsNaN(amount) && !double.IsInfinity(amount) && amount>=0 && amount<=float.MaxValue;
        }

		public static int KingdomMaxWarriors(Kingdom kingdom)
		{
			try
			{
				// WorldBox 0.50+: Kingdom.countWarriorsMax(); старые стабы — getMaxWarriors().
				object value = Invoke(kingdom, "countWarriorsMax") ?? Invoke(kingdom, "getMaxWarriors");
				if (value is int integer) return integer;
				if (value is long longValue) return (int)longValue;
				return Math.Max(1, KingdomPopulation(kingdom) / 3);
			}
			catch (Exception)
			{
				return Math.Max(1, KingdomPopulation(kingdom) / 3);
			}
		}

		public static void CollectRelations(MapBox map, Kingdom kingdom, List<RelationInfo> target)
	{
		try
		{
			object obj = Invoke(kingdom, "getDiplomacy");
            // Missing legacy getDiplomacy must not erase neighbouring states
            // from trade and strategic snapshots.
			List<Kingdom> list = KingdomList(map);
			if (list == null) return;
			for (int i = 0; i < list.Count; i++)
			{
				Kingdom val = list[i];
				if (val != null && ((NanoObject)val).id != ((NanoObject)kingdom).id)
				{
					RelationInfo item = new RelationInfo
					{
						OtherId = (int)((NanoObject)val).id,
						Relation = ReadPairRelation(kingdom, val, obj),
						Adjacent = AreAdjacent(map, kingdom, val),
						SharedReligionId = SharedCulture(kingdom, val, "religion"),
						SharedLanguageId = SharedCulture(kingdom, val, "language"),
						SharedCultureId = SharedCulture(kingdom, val, "culture")
					};
					target.Add(item);
				}
			}
		}
		catch (Exception ex)
		{
			Warn("Kingdom diplomacy: " + ex.Message);
		}
	}

    private static RelationType ReadPairRelation(Kingdom kingdom, Kingdom other, object legacyDiplomacy)
    {
        if (AreKingdomsAtWar(kingdom, other))
            return RelationType.War;
        if (AllianceSafety.Same(kingdom, other)) return RelationType.Alliance;
        // Keep the legacy route when available, but collect topology even
        // when no relation provider exists. Unknown stays None, not Peace.
        return ReadRelation(legacyDiplomacy, other);
    }

	private static RelationType ReadRelation(object diplomacy, Kingdom other)
	{
		try
		{
			object obj = Invoke(diplomacy, "getRelation", other);
			if (obj == null)
			{
				return RelationType.None;
			}
			string text = obj.ToString().ToUpperInvariant();
			if (text.Contains("WAR"))
			{
				return RelationType.War;
			}
			if (text.Contains("ALLIANCE"))
			{
				return RelationType.Alliance;
			}
			if (text.Contains("PEACE"))
			{
				return RelationType.Peace;
			}
			return RelationType.None;
		}
		catch (Exception)
		{
			return RelationType.None;
		}
	}

	private static bool AreAdjacent(MapBox map, Kingdom a, Kingdom b)
	{
		try
		{
			// 0.23.2: сначала — родное соседство игры (границы зон соприкасаются). На больших
			// картах (Европа) центры городов дальше 30 клеток, и старая проверка «по расстоянию»
			// не находила ни одного соседа: ни торговли, ни браков, ни границ за 140 лет.
			int native = NativeNeighbours(a, b);
			if (native >= 0)
			{
				return native > 0;
			}
			List<City> list = KingdomCities(a);
			List<City> list2 = KingdomCities(b);
			if (list == null || list2 == null)
			{
				return false;
			}
			for (int i = 0; i < list.Count; i++)
			{
				for (int j = 0; j < list2.Count; j++)
				{
					if (CitiesTouch(list[i], list2[j]))
					{
						return true;
					}
				}
			}
			return false;
		}
		catch (Exception)
		{
			return false;
		}
	}

	/// <summary>1 — соседи по данным игры, 0 — не соседи, −1 — API соседства недоступен.</summary>
	private static int NativeNeighbours(Kingdom a, Kingdom b)
	{
		try
		{
			object set = ReadMember(a, "neighbours_kingdoms") ?? Invoke(a, "getNeighbours");
			if (set is System.Collections.IEnumerable kingdoms)
			{
				foreach (object k in kingdoms)
				{
					if (ReferenceEquals(k, b))
					{
						return 1;
					}
				}
				return 0;
			}
			// Уровень городов (WorldBox 0.50+): City.neighbours_kingdoms и City.neighbours_cities.
			List<City> list = KingdomCities(a);
			if (list == null || list.Count == 0)
			{
				return -1;
			}
			bool anyApi = false;
			for (int i = 0; i < list.Count; i++)
			{
				if (list[i] == null) continue;
				object kset = ReadMember(list[i], "neighbours_kingdoms");
				if (kset is System.Collections.IEnumerable nKingdoms)
				{
					anyApi = true;
					foreach (object k in nKingdoms)
					{
						if (ReferenceEquals(k, b))
						{
							return 1;
						}
					}
				}
				object cset = ReadMember(list[i], "neighbours_cities");
				if (!(cset is System.Collections.IEnumerable cities))
				{
					continue;
				}
				anyApi = true;
				foreach (object c in cities)
				{
					if (c is City city && city.kingdom != null && ReferenceEquals(city.kingdom, b))
					{
						return 1;
					}
				}
			}
			return anyApi ? 0 : -1;
		}
		catch (Exception)
		{
			return -1;
		}
	}

	private static bool CitiesTouch(City a, City b)
	{
		if (a == null || b == null)
		{
			return false;
		}
		try
		{
			object obj = ReadProperty(a, "position") ?? ReadMember(a, "city_center") ?? CityTile(a);
			object obj2 = ReadProperty(b, "position") ?? ReadMember(b, "city_center") ?? CityTile(b);
			if (obj == null || obj2 == null)
			{
				return false;
			}
			float num = ToFloat(ReadProperty(obj, "x"));
			float num2 = ToFloat(ReadProperty(obj, "y"));
			float num3 = ToFloat(ReadProperty(obj2, "x"));
			float num4 = ToFloat(ReadProperty(obj2, "y"));
			float num5 = num - num3;
			float num6 = num2 - num4;
			// Запасной путь без API соседства: 60 клеток между центрами (раньше 30 — мало для больших карт).
			return num5 * num5 + num6 * num6 <= 3600f;
		}
		catch (Exception)
		{
			return false;
		}
	}

	private static int SharedCulture(Kingdom a, Kingdom b, string kind)
	{
		List<City> list = KingdomCities(a);
		List<City> list2 = KingdomCities(b);
		if (list == null || list2 == null)
		{
			return 0;
		}
		for (int i = 0; i < list.Count; i++)
		{
			int num = CultureId(list[i], kind);
			if (num == 0)
			{
				continue;
			}
			for (int j = 0; j < list2.Count; j++)
			{
				if (CultureId(list2[j], kind) == num)
				{
					return num;
				}
			}
		}
		return 0;
	}

	public static void RefreshRulers(MapBox map, PoliticalWorld world)
	{
		List<Kingdom> list = KingdomList(map);
		if (list == null) return;
		for (int i = 0; i < list.Count; i++)
		{
			Kingdom val = list[i];
			if (val == null)
			{
				continue;
			}
			KingdomPolitics kingdom = world.GetKingdom((int)((NanoObject)val).id);
			if (kingdom == null)
			{
				continue;
			}
			Actor val2 = ReadRuler(val);
			if (val2 == null)
			{
				kingdom.Ruler = null;
				continue;
			}
			string clanName = ActorClanName(val2) ?? string.Empty;
			long clanId = 0L;
			try
			{
				object clanObj = ReadProperty(val2, "clan") ?? Invoke(val2, "getClan");
				if (clanObj != null)
				{
					object cId = ReadProperty(clanObj, "id") ?? ReadMember(ReadMember(clanObj, "data"), "id");
					if (cId is long lId) clanId = lId;
					else if (cId is int iId) clanId = iId;
				}
			}
			catch { }

			RulerRef ruler = kingdom.Ruler;
			if (ruler == null || ruler.ActorId != ((BaseSystemData)val2.data).id)
			{
				// Страж династии: игра могла поставить короля со стороны — если у старого
				// короля есть родня, власть возвращается ей, и RulerRef обновится на
				// следующем синке уже настоящим (родным) королём.
				// Исключение: если возвращённый наследник сам ребёнок — не выходим:
				// такой случай накрывается действительным регентством ниже.
				Actor guardedHeir = ruler != null ? GuardDynasty(val, kingdom, val2, ruler.ActorId) : null;
				if (guardedHeir != null)
				{
					// Взрослый родной наследник — династия спасена, уходим в страж;
					// несовершеннолетний — трон внутри рода возвращён, но власть
					// на время отдаём регента: синхронизируем точку опоры.
					if (GameText.ActorAge(guardedHeir) >= RealPolitic.Core.RegencyGame.MajorityAge)
					{
						continue;
					}
					val2 = guardedHeir;
				}
				// Этап 23: действительное регентство. Игрой избран несовершеннолетний
				// монарх — пока есть взрослая родня, корона уходит регента: setKing
				// регента, а наследник записан в RegencyState до 18 лет.
				if (world.Params.EnableRealRegency
					&& GameText.ActorAge(val2) < RealPolitic.Core.RegencyGame.MajorityAge
					&& RealPolitic.Core.RegencyGame.Qualifies(kingdom.Ideology, kingdom.SuccessionLaw))
				{
					// 0.23.11: регент — только взрослая родня наследника (его же династии).
					// Никакой «канцлер из столицы» и никакой случайный взрослый: иначе корона
					// уходит постороннему клану, и дом наследника свергает регент-узурпатор.
					// (Трагедия дома Вевович: Радрек получил регента Аледы из чужого рода.)
					// Если совершеннолетней родни нет — наследник правит сам до 18 лет,
					// с обычным регентством-советом (без узурпации).
					Actor regent = FindRoyalHeir(val, kingdom, ruler?.ActorId ?? 0, val2, RealPolitic.Core.RegencyGame.MajorityAge);
					if (regent != null && !ReferenceEquals(regent, val2))
					{
						try
						{
							Invoke(val, "setKing", regent, false);
						}
						catch (Exception) { }
						if (ReferenceEquals(ReadRuler(val), regent))
						{
							kingdom.Regency = kingdom.Regency ?? new RealPolitic.Core.RegencyState();
							if (!kingdom.Regency.IsActive)
							{
								kingdom.Regency.Start(world.Year, GameText.ActorName(regent),
									(int)((BaseSystemData)regent.data).id);
							}
							kingdom.Regency.HeirActorId = (int)((BaseSystemData)val2.data).id;
							kingdom.Regency.HeirName = GameText.ActorName(val2);
							kingdom.Regency.CrownSitsWithRegent = true;
							RealPolitic.Core.RegencyGame.PublishCrowned(world, kingdom,
								new RulerRef { Name = kingdom.Regency.HeirName, Age = GameText.ActorAge(val2) },
								kingdom.Regency.RegentName);
							val2 = regent;
							clanName = ActorClanName(val2);
							clanId = ActorClanId(val2);
						}
					}
				}
				else if (GameText.ActorAge(val2) > 0
					&& GameText.ActorAge(val2) < RealPolitic.Core.RegencyGame.MajorityAge
					&& !RealPolitic.Core.RegencyGame.Qualifies(kingdom.Ideology, kingdom.SuccessionLaw))
				{
					Actor elder = FindAdultElder(val, val2, RealPolitic.Core.RegencyGame.MajorityAge);
					if (elder != null && !ReferenceEquals(elder, val2))
					{
						if (TrySetKingOnKingdom(val, elder, quiet: true) && ReferenceEquals(ReadRuler(val), elder))
						{
							val2 = elder;
							clanName = ActorClanName(val2);
							clanId = ActorClanId(val2);
						}
					}
				}
				kingdom.Ruler = new RulerRef
				{
					ActorId = (int)((BaseSystemData)val2.data).id,
					Name = GameText.ActorName(val2),
					KingdomId = (int)((NanoObject)val).id,
					SinceYear = world.Year,
					IsFemale = GameText.ActorIsFemale(val2),
					Age = GameText.ActorAge(val2),
					// 0.52: запоминаем, из какого народа правитель.
					RaceId = ActorSpeciesId(val2),
					ClanName = clanName,
					ClanId = clanId,
					// 0.23.11: ядро видит, что на троне регент, а не преемник — не объявляет
					// смену власти/династии и не списывает регентство по возрасту регента.
					IsRegent = kingdom.Regency != null && kingdom.Regency.IsActive
						&& kingdom.Regency.CrownSitsWithRegent
				};
			}
			else
			{
				// Тот же правитель — обновляем возраст и клан:
				ruler.Age = GameText.ActorAge(val2);
				if (!string.IsNullOrEmpty(clanName))
				{
					ruler.ClanName = clanName;
					ruler.ClanId = clanId;
				}
				// 0.23.11: маркер «на троне регент» держится, пока идёт регентство;
				// снятие регентства/возврат короны корректурой обновят его сами.
				ruler.IsRegent = kingdom.Regency != null && kingdom.Regency.IsActive
					&& kingdom.Regency.CrownSitsWithRegent;
			}

			// Этап 23: регент сидит на троне за малолетнего наследника?
			// Как только наследник в королевстве достиг 18 — корона возвращается.
			if (kingdom.Regency != null && kingdom.Regency.IsActive
				&& kingdom.Regency.CrownSitsWithRegent && kingdom.Regency.HeirActorId != 0)
			{
				Actor heir = null;
				List<Actor> units = ((MetaObject<KingdomData>)(object)val).units;
				if (units != null)
				{
					for (int u = 0; u < units.Count; u++)
					{
						if (units[u] != null && ((BaseSystemData)units[u].data).id == kingdom.Regency.HeirActorId)
						{
							heir = units[u];
							break;
						}
					}
				}
				if (heir != null && GameText.ActorAge(heir) >= RealPolitic.Core.RegencyGame.MajorityAge)
				{
					string regentName = kingdom.Regency.RegentName;
					string heirName = kingdom.Regency.HeirName;
					if (string.IsNullOrEmpty(heirName))
					{
						heirName = GameText.ActorName(heir);
					}
					try
					{
						Invoke(val, "setKing", heir, false);
					}
					catch (Exception) { }
					if (ReferenceEquals(ReadRuler(val), heir))
					{
						RealPolitic.Core.RegencyGame.PublishPassed(world, kingdom, heirName, regentName);
						// 0.23.11: корона вернулась — снимаем флаг свопа с регентом и сразу
						// переписываем точку опоры на наследника, чтобы ядро в ЭТОМ году
						// увидело законного преемника (а не «смену власти регентом»).
						kingdom.Regency.CrownSitsWithRegent = false;
						kingdom.Regency.HeirActorId = 0;
						kingdom.Regency.HeirName = string.Empty;
						kingdom.Ruler = new RulerRef
						{
							ActorId = (int)((BaseSystemData)heir.data).id,
							Name = heirName,
							KingdomId = (int)((NanoObject)val).id,
							SinceYear = world.Year,
							IsFemale = GameText.ActorIsFemale(heir),
							Age = GameText.ActorAge(heir),
							ClanName = ActorClanName(heir) ?? string.Empty,
							ClanId = ActorClanId(heir),
							IsRegent = false
						};
						// RulerRef сам обновится на следующем проходе цикла синхронизации.
					}
				}
				else if (heir == null)
				{
					// Наследник мёртв/ушёл — регентский статус снимается естественно,
					// регент остаётся законным правителем (следующая смена власти — как обычно).
					kingdom.Regency.End();
				}
			}
		}
	}

	/// <summary>0.46: род (вид) самого жителя — для борьбы малых народов и переселения.</summary>
	public static string ActorSpeciesId(Actor actor)
	{
		if (actor == null)
		{
			return string.Empty;
		}
		try
		{
			object raw = ReadMember(actor, "species_id")
				?? Invoke(actor, "getSpecies")
				?? ReadProperty(ReadMember(actor, "asset"), "species_id")
				?? ReadProperty(actor, "race");
			return raw?.ToString() ?? string.Empty;
		}
		catch (Exception)
		{
			return string.Empty;
		}
	}

	/// <summary>0.46: имя жителя для журнала и летописи (без обращения к игре, если пусто).</summary>
	public static string ActorDisplayName(Actor actor)
	{
		try
		{
			string name = GameText.ActorName(actor);
			return string.IsNullOrEmpty(name) ? "житель" : name;
		}
		catch (Exception)
		{
			return "житель";
		}
	}

	/// <summary>0.46: правитель державы? Правитель не переселяется — он у власти.</summary>
	public static bool IsRulerActor(Actor actor, Kingdom kingdom)
	{
		try
		{
			if (actor == null || kingdom == null)
			{
				return false;
			}
			return ReferenceEquals(ReadRuler(kingdom), actor);
		}
		catch (Exception)
		{
			return false;
		}
	}

	/// <summary>0.46: глава города? Его тоже не переселяем — город останется без головы.</summary>
	public static bool IsCityLeaderActor(Actor actor, City city)
	{
		try
		{
			if (actor == null || city == null)
			{
				return false;
			}
			object leader = ReadMember(city, "leader") ?? Invoke(city, "getLeader");
			return leader != null && ReferenceEquals(leader, actor);
		}
		catch (Exception)
		{
			return false;
		}
	}

	public static Actor ReadRuler(Kingdom kingdom)
	{
		try
		{
			// 0.51.2: поле правителя называется king, а не ruler.
			object obj = ReadProperty(kingdom, "king");
			if (obj is Actor actor)
			{
				return actor;
			}
			// Совместимость со старыми версиями.
			object obj2 = ReadProperty(kingdom, "ruler");
			return (Actor)((obj2 is Actor) ? obj2 : null);
		}
		catch (Exception ex)
		{
			Warn("Kingdom.king: " + ex.Message);
			return null;
		}
	}

	public static bool IsClan(Kingdom kingdom)
	{
		try
		{
			if (ReadProperty(kingdom, "is_clan") is bool result)
			{
				return result;
			}
			return ReadProperty(kingdom, "clan") != null;
		}
		catch (Exception)
		{
			return false;
		}
	}

	public static string KingdomTitle(Kingdom kingdom)
	{
		try
		{
			object obj = ReadProperty(kingdom, "title");
			return (obj == null) ? string.Empty : obj.ToString();
		}
		catch (Exception)
		{
			return string.Empty;
		}
	}

 public static object ReadProperty(object target, string name)
 {
   return ReadMember(target, name);
	}

	/// <summary>
	/// Чтение поля или свойства с обходом всей иерархии типов.
	/// Важно для унаследованных членов (например, gameObject у MonoBehaviour
	/// объявлен в Component — точный GetType() его не найдёт).
	/// </summary>
	// ================= 0.43: кэш рефлексии =================
	// Было: КАЖДЫЙ вызов ReadMember/Invoke заново перебирал иерархию типов через
	// Type.GetField/GetProperty, а Invoke ещё и вызывал type.GetMethods(flags) —
	// это выделение массива ВСЕХ методов типа на каждый вызов. В замере 29.10.2026
	// «сборка держав» стоила 782 мс на игровой год при 599 опрошенных жителях:
	// почти всё время уходило на поиск членов через рефлексию, а не на работу мода.
	// Теперь найденные члены и методы запоминаются по паре (тип, имя).
	private sealed class MethodSlot
	{
		public MethodInfo Method;
		public ParameterInfo[] Parameters;
	}

	private static readonly Dictionary<string, MemberInfo> MemberCache = new Dictionary<string, MemberInfo>();
	private static readonly HashSet<string> MissingMembers = new HashSet<string>();
	private static readonly Dictionary<string, MethodSlot[]> MethodCache = new Dictionary<string, MethodSlot[]>();
	private static readonly HashSet<string> MissingMethods = new HashSet<string>();

	private static string CacheKey(Type type, string name)
	{
		return type.FullName + "|" + name;
	}

	/// <summary>Сброс кэша рефлексии (горячая перезагрузка сборок, смена мира).</summary>
	public static void ClearReflectionCache()
	{
		MemberCache.Clear();
		MissingMembers.Clear();
		MethodCache.Clear();
		MissingMethods.Clear();
	}

	private static MemberInfo FindMember(Type type, string name)
	{
		string key = CacheKey(type, name);
		if (MemberCache.TryGetValue(key, out MemberInfo cached))
		{
			return cached;
		}
		if (MissingMembers.Contains(key))
		{
			return null;
		}
		FieldInfo field = type.GetField(name, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
		if (field != null)
		{
			MemberCache[key] = field;
			return field;
		}
		PropertyInfo property = type.GetProperty(name, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
		if (property != null && property.CanRead)
		{
			MemberCache[key] = property;
			return property;
		}
		MissingMembers.Add(key);
		return null;
	}

	private static MethodSlot[] DeclaredMethods(Type type, string name)
	{
		string key = CacheKey(type, name);
		if (MethodCache.TryGetValue(key, out MethodSlot[] cached))
		{
			return cached;
		}
		if (MissingMethods.Contains(key))
		{
			return null;
		}
		const BindingFlags flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly;
		List<MethodSlot> found = null;
		foreach (MethodInfo method in type.GetMethods(flags))
		{
			if (method.Name != name || method.ContainsGenericParameters)
			{
				continue;
			}
			if (found == null)
			{
				found = new List<MethodSlot>(2);
			}
			found.Add(new MethodSlot { Method = method, Parameters = method.GetParameters() });
		}
		if (found == null)
		{
			MissingMethods.Add(key);
			return null;
		}
		MethodSlot[] slots = found.ToArray();
		MethodCache[key] = slots;
		return slots;
	}

	/// <summary>Чтение поля или свойства с обходом всей иерархии типов (с кэшем).</summary>
	public static object ReadMember(object target, string name)
	{
		if (target == null)
		{
			return null;
		}
		// 0.53.1: РАНЬШЕ ЗДЕСЬ НЕ БЫЛО КЭША. Кэш членов (FindMember) существовал с 0.43,
		// но им пользовался только вызов методов — а ReadMember каждый раз обходил всю
		// иерархию типов через GetField/GetProperty. Эти вызовы идут каждый кадр
		// (лояльность города, титул правителя, карта идеологий), да ещё и аллоцируют —
		// отсюда «замирание на секунды» и мусор для сборщика мусора.
		// Владелец 30.10.2026: «каждый конец года пролагивает на секунды».
		ReflectionCalls++;
		for (Type type = target.GetType(); type != null; type = type.BaseType)
		{
			MemberInfo member = FindMember(type, name);
			if (member is FieldInfo field)
			{
				try
				{
					return field.GetValue(target);
				}
				catch (Exception)
				{
					return null;
				}
			}
			if (member is PropertyInfo property && property.CanRead)
			{
				try
				{
					return property.GetValue(target, null);
				}
				catch (Exception)
				{
					return null;
				}
			}
		}
		return null;
	}

	/// <summary>0.53.1: сколько раз мод читал поля через отражение (для отчёта о скорости).</summary>
	public static long ReflectionCalls;

	/// <summary>0.53.1: сколько членов уже найдено и запомнено.</summary>
	public static int ReflectionCacheSize => MemberCache.Count + MethodCache.Count;

	/// <summary>0.53.1: сброс счётчиков отражения для нового окна замера.</summary>
	public static void ResetReflectionCounters()
	{
		ReflectionCalls = 0;
	}

	/// <summary>
	/// Запись поля или свойства с обходом иерархии типов (зеркало <see cref="ReadMember"/>).
	/// Возвращает false, если член не найден или недоступен для записи — вызывающий
	/// может попробовать другой способ, не роняя игру.
	/// </summary>
	/// <summary>Статическое поле/свойство типа (с обходом иерархии).</summary>
	public static object ReadStatic(Type type, string name)
	{
		for (Type t = type; t != null; t = t.BaseType)
		{
			const BindingFlags flags = BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic;
			FieldInfo field = t.GetField(name, flags);
			if (field != null)
			{
				try { return field.GetValue(null); } catch (Exception) { return null; }
			}
			PropertyInfo property = t.GetProperty(name, flags);
			if (property != null && property.CanRead)
			{
				try { return property.GetValue(null, null); } catch (Exception) { return null; }
			}
		}
		return null;
	}

	public static bool WriteMember(object target, string name, object value)
	{
		if (target == null)
		{
			return false;
		}
		ReflectionCalls++;
		for (Type type = target.GetType(); type != null; type = type.BaseType)
		{
			MemberInfo cached = FindMember(type, name);
			FieldInfo field = cached as FieldInfo;
			if (field != null)
			{
				try
				{
					field.SetValue(target, Convert.ChangeType(value, field.FieldType));
					return true;
				}
				catch (Exception)
				{
					return false;
				}
			}
			PropertyInfo property = cached as PropertyInfo;
			if (property != null && property.CanWrite)
			{
				try
				{
					property.SetValue(target, Convert.ChangeType(value, property.PropertyType), null);
					return true;
				}
				catch (Exception)
				{
					return false;
				}
			}
		}
		return false;
	}

  /// <summary>Последняя ошибка внутри игрового метода, вызванного через Invoke (для диагностики).</summary>
  public static string LastInvokeError;

  public static object Invoke(object target, string name, params object[] args)
  {
    TryInvoke(target, name, out object result, args);
    return result;
  }

  // A void return value is null too. Callers performing mutations must use
  // the success flag, not the result, to decide whether to run a fallback.
	  public static bool TryInvoke(object target, string name, out object result, params object[] args)
	  {
	    result = null;
	    LastInvokeError = null;
	    if (target == null)
	    {
	      LastInvokeError = name + ": target is null";
	      return false;
	    }
	    args = args ?? Array.Empty<object>();
	    bool found = false;
    for (Type type = target.GetType(); type != null; type = type.BaseType)
    {
	      MethodSlot[] slots = DeclaredMethods(type, name);
	      if (slots == null) continue;
	      for (int slotIndex = 0; slotIndex < slots.Length; slotIndex++)
	      {
	        MethodSlot slot = slots[slotIndex];
	        if (!ParametersMatch(slot.Parameters, args)) continue;
	        found = true;
        try
        {
          result = slot.Method.Invoke(target, PrepareArgs(slot.Parameters, args));
          return true;
        }
        catch (TargetInvocationException ex)
        {
          LastInvokeError = name + ": " + (ex.InnerException?.GetType().Name ?? "?") + " " + (ex.InnerException?.Message ?? ex.Message);
          return false;
        }
	        catch (ArgumentException ex) { LastInvokeError = name + ": " + ex.Message; }
	      }
	    }
	    if (!found && LastInvokeError == null)
	    {
	      LastInvokeError = name + ": compatible method not found";
	    }
	    return false;
	  }

  public static bool HasMember(Type type, string name)
  {
    if (type == null) return false;
    const BindingFlags flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
    for (Type current = type; current != null; current = current.BaseType)
    {
     if (current.GetField(name, flags) != null || current.GetProperty(name, flags) != null)
      return true;
     foreach (MethodInfo method in current.GetMethods(flags))
      if (method.Name == name) return true;
    }
    return false;
  }

  private static bool ParametersMatch(ParameterInfo[] parameters, object[] args)
  {
   if (parameters.Length != args.Length) return false;
   for (int i = 0; i < parameters.Length; i++)
   {
    if (args[i] == null) continue;
    Type parameterType = parameters[i].ParameterType;
    if (parameterType.IsByRef) parameterType = parameterType.GetElementType();
    if (parameterType.IsInstanceOfType(args[i])) continue;
    if (parameterType.IsEnum && args[i] is int) continue;
    if (IsNumericType(parameterType) && IsNumericType(args[i].GetType())) continue;
    return false;
   }
   return true;
  }

  private static bool IsNumericType(Type type)
  {
   if (type == null) return false;
   TypeCode code = Type.GetTypeCode(type);
   return code >= TypeCode.SByte && code <= TypeCode.Double;
  }

  private static object[] PrepareArgs(ParameterInfo[] parameters, object[] args)
  {
   if (args == null || args.Length == 0) return Array.Empty<object>();
   object[] result = new object[args.Length];
   for (int i = 0; i < args.Length; i++)
   {
    object arg = args[i];
    if (arg == null)
    {
     result[i] = null;
     continue;
    }
    Type pType = parameters[i].ParameterType;
    if (pType.IsByRef) pType = pType.GetElementType();
    if (pType.IsInstanceOfType(arg))
    {
     result[i] = arg;
    }
    else if (pType.IsEnum && arg is int iVal)
    {
     result[i] = Enum.ToObject(pType, iVal);
    }
    else if (IsNumericType(pType) && IsNumericType(arg.GetType()))
    {
     try
     {
      result[i] = Convert.ChangeType(arg, pType);
     }
     catch
     {
      result[i] = arg;
     }
    }
    else
    {
     result[i] = arg;
    }
   }
   return result;
  }

	private static float ToFloat(object value)
	{
		if (value is float)
		{
			return (float)value;
		}
		if (value is int num)
		{
			return num;
		}
		if (value is double num2)
		{
			return (float)num2;
		}
		return 0f;
	}

	/// <summary>
	/// Проверяет, разрешена ли дипломатия в Законах мира WorldBox (World Laws).
	/// Если игрок выключил закон «Дипломатия» или включил «Вечный мир», мод уважает
	/// выбор игрока и не начинает автоматические войны.
	/// </summary>
	public static bool IsDiplomacyWorldLawEnabled()
	{
		try
		{
            // PlayerConfig.optionBoolEnabled is not an authoritative world-law source:
            // unknown aliases can return false and veto every war. Read the actual world law.
			MapBox map = World.world;
			if (map != null)
			{
				// build 719: MapBox.world_laws (WorldLaws) с isEnabled("world_law_diplomacy").
				object worldLaws = ReadMember(map, "world_laws") ?? ReadMember(map, "worldLaws");
				if (worldLaws != null)
				{
					object isOpt = Invoke(worldLaws, "isEnabled", "world_law_diplomacy") ?? Invoke(worldLaws, "isOption", "diplomacy");
					if (isOpt is bool b && !b)
					{
						return false;
					}
				}
			}
			return true;
		}
		catch
		{
			return true;
		}
	}

	/// <summary>Старейший живой совершеннолетний житель города (кандидат в лидеры).</summary>
	public static Actor FindAdultResident(City city)
	{
		try
		{
			List<Actor> units = ((MetaObject<CityData>)(object)city).units;
			if (units == null)
			{
				return null;
			}
			Actor best = null;
			int bestAge = 15;
			for (int i = 0; i < units.Count; i++)
			{
				Actor u = units[i];
				if (u == null || IsBoatUnit(u)) continue;
				if (Invoke(u, "isAlive") is bool alive && !alive) continue;
				int age = GameText.ActorAge(u);
				if (age > bestAge)
				{
					bestAge = age;
					best = u;
				}
			}
			return best;
		}
		catch (Exception ex)
		{
			Warn("FindAdultResident: " + ex.Message);
			return null;
		}
	}

	/// <summary>Идентификатор клана жителя (0 — нет клана).</summary>
	public static long ActorClanId(Actor actor)
	{
		try
		{
			object clanObj = ReadProperty(actor, "clan") ?? Invoke(actor, "getClan");
			if (clanObj != null)
			{
				object cId = ReadProperty(clanObj, "id") ?? ReadMember(ReadMember(clanObj, "data"), "id");
				if (cId is long lId) return lId;
				if (cId is int iId) return iId;
			}
		}
		catch
		{
		}
		return 0L;
	}

	/// <summary>Родители жителя (id), если игра их хранит.</summary>
	public static long[] ActorParentIds(Actor actor)
	{
		long[] result = new long[2];
		try
		{
			object data = ReadProperty(actor, "data");
			object p1 = ReadMember(data, "parent_id_1");
			object p2 = ReadMember(data, "parent_id_2");
			if (p1 is long l1) result[0] = l1; else if (p1 is int i1) result[0] = i1;
			if (p2 is long l2) result[1] = l2; else if (p2 is int i2) result[1] = i2;
		}
		catch
		{
		}
		return result;
	}

	/// <summary>
	/// Этап 23: переименовывает королевский клан игры в фамилию династии (если имя
	/// сменилось ядром). Безопасно: пустое имя/нет клана — ничего не происходит.
	/// </summary>
	public static bool RenameRoyalClan(Kingdom gameKingdom, string newName)
	{
		try
		{
			if (gameKingdom == null || string.IsNullOrWhiteSpace(newName))
			{
				return false;
			}
			Actor king = ReadRuler(gameKingdom);
			if (king == null)
			{
				return false;
			}
			object clan = ReadProperty(king, "clan") ?? Invoke(king, "getClan");
			if (clan == null)
			{
				return false;
			}
			string current = ReadProperty(clan, "name")?.ToString() ?? ReadProperty(ReadMember(clan, "data"), "name")?.ToString();
			if (string.Equals(current, newName, StringComparison.Ordinal))
			{
				return true;
			}
			// Clan является NanoObject → имя есть и в объекте, и в data (метаобъект).
			bool set = TrySetProperty(clan, "name", newName) | TrySetProperty(ReadMember(clan, "data"), "name", newName);
			if (!set)
			{
				Invoke(clan, "setName", newName);
			}
			return true;
		}
		catch (Exception)
		{
			return false;
		}
	}

	/// <summary>Переименовывает всех членов королевского клана: «Имя Фамилия».
	/// Идемпотентно (без двойных фамилий), вызывать можно ежегодно —
	/// новорождённые принцы подхватят фамилию на следующем тике.</summary>
	public static int ApplySurnameToRoyalFamily(Kingdom gameKingdom, string surname)
	{
		int renamed = 0;
		try
		{
			if (gameKingdom == null || string.IsNullOrWhiteSpace(surname))
			{
				return 0;
			}
			Actor king = ReadRuler(gameKingdom);
			if (king == null)
			{
				return 0;
			}
			object royalClan = ReadProperty(king, "clan") ?? Invoke(king, "getClan");
			if (royalClan == null)
			{
				return 0;
			}
			object clanId = ReadProperty(royalClan, "id");
			long kingId = ((BaseSystemData)king.data).id;
			List<City> cities = WorldCities(gameKingdom);
			if (cities == null)
			{
				return 0;
			}
			for (int c = 0; c < cities.Count; c++)
			{
				System.Collections.IEnumerable units = ReadMember(cities[c], "units") as System.Collections.IEnumerable;
				if (units == null)
				{
					continue;
				}
				foreach (object u in units)
				{
					Actor actor = u as Actor;
					if (actor == null)
					{
						continue;
					}
					bool family = false;
					object hisClan = ReadProperty(actor, "clan") ?? Invoke(actor, "getClan");
					if (hisClan != null)
					{
						if (ReferenceEquals(hisClan, royalClan))
						{
							family = true;
						}
						else
						{
							object hisId = ReadProperty(hisClan, "id");
							family = clanId != null && EqualsIds(clanId, hisId);
						}
					}
				// Дети правителя: кровь тут понятнее кланов — даже вне королевского клана
				// королевские дети носят династическую фамилию отца/матери. Этап 25: мы ещё
				// и перетаскиваем таких детей В королевский клан (у правительницы дети по
				// умолчанию уходят в род отца — теперь династия через неё не умирает).
				if (!family)
				{
					long[] parents = ActorParentIds(actor);
					family = (parents[0] == kingId && kingId != 0) || (parents[1] == kingId && kingId != 0);
					if (family)
					{
						TryJoinClan(actor, royalClan);
					}
				}
					if (!family)
					{
						continue;
					}
					if (RenameWithSurname(actor, surname))
					{
						renamed++;
					}
				}
			}
		}
		catch (Exception)
		{
			return renamed;
		}
		return renamed;
	}

	/// <summary>Дописывает фамилию к имени актёра, если её там ещё нет.</summary>
	private static bool RenameWithSurname(Actor actor, string surname)
	{
		try
		{
			string name = ReadProperty(actor, "name")?.ToString() ?? ReadProperty(ReadMember(actor, "data"), "name")?.ToString();
			if (string.IsNullOrEmpty(name))
			{
				return false;
			}
			string withSurname = DynastyNaming.WithSurname(name, surname);
			if (withSurname == name)
			{
				return false;
			}
			bool set = TrySetProperty(actor, "name", withSurname) | TrySetProperty(ReadMember(actor, "data"), "name", withSurname);
			if (!set)
			{
				Invoke(actor, "setName", withSurname);
			}
			return true;
		}
		catch (Exception)
		{
			return false;
		}
	}

	/// <summary>Города державы списком (тот же канал, что у KingdomCities, но публично для семейных обходов).</summary>
	private static List<City> WorldCities(Kingdom gameKingdom)
	{
		return KingdomCities(gameKingdom);
	}

	private static bool EqualsIds(object a, object b)
	{
		if (a == null || b == null) return false;
		if (a is int ai && b is int bi) return ai == bi;
		if (a is long al && b is long bl) return al == bl;
		if (a.ToString() == b.ToString()) return true;
		return ReferenceEquals(a, b);
	}

	private static bool TrySetProperty(object target, string name, object value)
	{
		try
		{
			if (target == null) return false;
			System.Reflection.PropertyInfo prop = target.GetType().GetProperty(name,
				System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);
			if (prop != null && prop.CanWrite)
			{
				prop.SetValue(target, value, null);
				return true;
			}
			System.Reflection.FieldInfo field = target.GetType().GetField(name,
				System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);
			if (field != null)
			{
				field.SetValue(target, value);
				return true;
			}
		}
		catch (Exception)
		{
		}
		return false;
	}

	/// <summary>
	/// Этап 23: тайный авиаудар — вызывает бог-силу «бомба» по координатам плитки.
	/// Цепочка кандидатов (новые версии игры скрывают API по-разному):
	/// World.world.powers.drop("bomb", x, y) → call → BombActions.spawnBomb…
	/// Возвращает true, если взрыв был задан; любые отказы игры логируются вызывающим.
	/// </summary>
	public static bool TryCallGodPowerBomb(object powersObj, int x, int y, object tileObj)
	{
		if (powersObj == null) return false;
		try
		{
			object bomb = Invoke(powersObj, "getPower", "bomb");
			if (bomb != null)
			{
				// GodPower.click(WorldTile, PowerButton)/click(x, y) — по наличию.
				if (tileObj != null && TryVoid(bomb, "click", tileObj, null)) return true;
				if (TryVoid(bomb, "click", tileObj)) return true;
				if (TryVoid(bomb, "clickAt", x, y)) return true;
			}
			// Возможные обходные вызовы самого менеджера.
			if (TryVoid(powersObj, "drop", "bomb", x, y)) return true;
			if (TryVoid(powersObj, "callPower", "bomb", x, y)) return true;
		}
		catch (Exception)
		{
		}
		return false;
	}

	private static bool TryVoid(object target, string name, params object[] args)
	{
		try
		{
			return Invoke(target, name, args) != null || MethodExists(target, name, args?.Length ?? 0);
		}
		catch (Exception)
		{
			return false;
		}
	}

	private static bool MethodExists(object target, string name, int argCount)
	{
		try
		{
			System.Reflection.MethodInfo[] methods = target.GetType().GetMethods(
				System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);
			for (int i = 0; i < methods.Length; i++)
			{
				if (methods[i].Name == name && methods[i].GetParameters().Length == argCount)
				{
					methods[i].Invoke(target, BuildArgs(methods[i], argCount));
					return true;
				}
			}
		}
		catch (Exception)
		{
		}
		return false;
	}

	private static object[] BuildArgs(System.Reflection.MethodInfo method, int count)
	{
		object[] args = new object[count];
		System.Reflection.ParameterInfo[] ps = method.GetParameters();
		for (int i = 0; i < count && i < ps.Length; i++)
		{
			args[i] = ps[i].ParameterType.IsValueType ? Activator.CreateInstance(ps[i].ParameterType) : null;
		}
		return args;
	}

	/// <summary>
	/// Этап 23: перемещение юнита на новую плитку (выдворение чужака за границу).
	/// Цепочка: Actor.moveTo/teleport/spawnAt по методу-совпадению; провали — деградация.
	/// </summary>
	public static bool TryExpelActor(Actor actor, object homeTileObj)
	{
		return TryExpelActor(actor, homeTileObj, 15f);
	}

	/// <summary>Выдворить чужака: лёгкий профилактический удар + приказ бежать домой
	/// («ножками», как и положено пограничному погрому). Телепортация — последний
	/// резерв, когда у актёра нет физических путей движения.</summary>
	public static bool TryExpelActor(Actor actor, object homeTileObj, float damage)
	{
		try
		{
			if (actor == null) return false;
			if (damage > 0f)
			{
				TryPunishActor(actor, damage);
			}
			if (homeTileObj != null)
			{
				// Движение своими ногами — как погромный беглец из бедной страны.
				if (InvokeWithFirstArg(actor, "goTo", homeTileObj)) return true;
				if (InvokeWithFirstArg(actor, "moveTo", homeTileObj)) return true;
				if (InvokeWithFirstArg(actor, "goToTile", homeTileObj)) return true;
				// Телепортация — только если движетеля в игре нет вообще.
				if (InvokeWithFirstArg(actor, "teleport", homeTileObj)) return true;
				if (InvokeWithFirstArg(actor, "spawnAt", homeTileObj)) return true;
			}
		}
		catch (Exception)
		{
		}
		return false;
	}

	/// <summary>Вызов метода с первым реальным аргументом (например goTo(tile[, false, false])) —
	/// остальные позиции заполняются дефолтами параметров.</summary>
	private static bool InvokeWithFirstArg(object target, string name, object firstArg)
	{
		try
		{
			if (target == null) return false;
			System.Reflection.MethodInfo[] methods = target.GetType().GetMethods(
				System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);
			for (int i = 0; i < methods.Length; i++)
			{
				System.Reflection.ParameterInfo[] ps = methods[i].GetParameters();
				if (methods[i].Name != name || ps.Length < 1)
				{
					continue;
				}
				if (firstArg != null && !ps[0].ParameterType.IsInstanceOfType(firstArg))
				{
					continue; // тип не совместим с первым параметром — пробуем другую перегрузку
				}
				object[] args = new object[ps.Length];
				args[0] = firstArg;
				for (int a = 1; a < ps.Length; a++)
				{
					args[a] = ps[a].ParameterType.IsValueType ? Activator.CreateInstance(ps[a].ParameterType) : null;
				}
                object result = methods[i].Invoke(target, args);
                if (result is bool accepted && !accepted) continue;
                return true;
			}
		}
		catch (Exception)
		{
		}
		return false;
	}

	/// <summary>Небольшой удар по чужаку: штраф здоровья без казни (движение врага прервать).</summary>
	public static bool TryPunishActor(Actor actor, float damage)
	{
		try
		{
			if (actor == null || damage <= 0f) return false;
			if (TryVoid(actor, "hit", damage)) return true;
			if (TryVoid(actor, "damage", damage)) return true;
			object health = ReadProperty(ReadMember(actor, "data"), "health");
			if (health != null)
			{
				float now = health is int hi ? hi : (health is float hf ? hf : 0f);
				if (now > 20f)
				{
					TrySetProperty(ReadMember(actor, "data"), "health", Math.Max(20f, now - damage));
					return true;
				}
			}
		}
		catch (Exception)
		{
		}
		return false;
	}

	/// <summary>
	/// Страж династии (0.17.3): если игра после смерти монарха поставила короля
	/// НЕ из королевского клана, а у старого короля остались живые совершеннолетние
	/// родственники (дети или члены того же клана) — возвращаем власть родне через
	/// Kingdom.setKing(Actor, bool pFromLoad). Иначе семья и клан разошлись бы:
	/// «в монархии власть берёт кто-то не из королевской семьи».
	/// Возвращает установленного наследника или null (власть остаётся как есть).
	/// </summary>
	public static Actor GuardDynasty(Kingdom gameKingdom, RealPolitic.Core.KingdomPolitics state, Actor newKing, long oldKingId)
	{
		try
		{
			if (gameKingdom == null || state == null || newKing == null
				|| (state.Ideology != RealPolitic.Core.IdeologyType.Monarchy
					&& state.Ideology != RealPolitic.Core.IdeologyType.Empire
					&& state.Ideology != RealPolitic.Core.IdeologyType.Khaganate)
				|| state.RoyalClanId == 0)
			{
				return null;
			}
			long newClan = ActorClanId(newKing);
			if (newClan != 0 && newClan == state.RoyalClanId)
			{
				return null; // новый король из королевского клана — всё в порядке
			}
			Actor heir = FindRoyalHeir(gameKingdom, state, oldKingId, newKing);
			if (heir == null)
			{
				return null;
			}
			// setKing — void: успех проверяем чтением короля после вызова.
			Invoke(gameKingdom, "setKing", heir, false);
			if (ReferenceEquals(ReadRuler(gameKingdom), heir))
			{
				return heir;
			}
		}
		catch
		{
		}
		return null;
	}

	/// <summary>Лучший живой совершеннолетний родственник старого короля: сначала дети, затем соклановцы.
	/// minAge: 16 достаточно для воцарения, регент своего королевства должен быть взрослым (18).</summary>
	public static Actor FindRoyalHeir(Kingdom gameKingdom, RealPolitic.Core.KingdomPolitics state, long oldKingId, Actor exclude, int minAge = 16)
	{
		try
		{
			List<Actor> units = ((MetaObject<KingdomData>)(object)gameKingdom).units;
			if (units == null) return null;
			Actor child = null;
			int childAge = -1;
			Actor clansman = null;
			int clansmanAge = -1;
			for (int i = 0; i < units.Count; i++)
			{
				Actor u = units[i];
				if (u == null || ReferenceEquals(u, exclude) || IsBoatUnit(u)) continue;
				if (Invoke(u, "isAlive") is bool alive && !alive) continue;
				int age = GameText.ActorAge(u);
				if (age < minAge) continue;
				long[] parents = ActorParentIds(u);
				bool isChild = oldKingId != 0 && (parents[0] == oldKingId || parents[1] == oldKingId);
				if (isChild && age > childAge)
				{
					childAge = age;
					child = u;
				}
				long clan = ActorClanId(u);
				if (!isChild && clan != 0 && clan == state.RoyalClanId && age > clansmanAge)
				{
					clansmanAge = age;
					clansman = u;
				}
			}
			return child ?? clansman;
		}
		catch
		{
			return null;
		}
	}

	/// <summary>Старейший совершеннолетний житель державы для ненаследственных режимов (племя, теократия и др.),
	/// чтобы игра не оставляла грудного младенца вождём или верховным жрецом при наличии взрослых.</summary>
	public static Actor FindAdultElder(Kingdom gameKingdom, Actor exclude, int minAge = 18)
	{
		try
		{
			List<Actor> units = ((MetaObject<KingdomData>)(object)gameKingdom).units;
			if (units == null) return null;
			Actor best = null;
			int bestAge = -1;
			for (int i = 0; i < units.Count; i++)
			{
				Actor u = units[i];
				if (u == null || ReferenceEquals(u, exclude) || IsBoatUnit(u)) continue;
				if (Invoke(u, "isAlive") is bool alive && !alive) continue;
				int age = GameText.ActorAge(u);
				if (age >= minAge && age > bestAge)
				{
					bestAge = age;
					best = u;
				}
			}
			return best;
		}
		catch
		{
			return null;
		}
	}

	/// <summary>
	/// Имя клана (династии) правителя или жителя из официальной системы кланов WorldBox.
	/// </summary>
	public static string ActorClanName(Actor actor)
	{
		try
		{
			if (actor == null)
			{
				return null;
			}
			object clan = ReadProperty(actor, "clan") ?? Invoke(actor, "getClan");
			if (clan != null)
			{
				object name = ReadProperty(clan, "name") ?? ReadMember(ReadMember(clan, "data"), "name");
				if (name is string s && !string.IsNullOrEmpty(s))
				{
					return s;
				}
			}
		}
		catch
		{
		}
		return null;
	}

	// ============ Этап 25: «реальность без оговорок» — выбранные правители на троне,
	// прижизненные посты экс-правителей, казнь главаря разгромленного мятежа,
	// клановая привязка детей правительницы. ============

	/// <summary>Первый взрослый юнит королевства (не король): кандидат на выборный трон.</summary>
	public static Actor TryPickAdultUnit(object gameKingdom, long excludeId)
	{
		List<City> cities = WorldCities(gameKingdom as Kingdom);
		if (cities == null) return null;
		for (int c = 0; c < cities.Count; c++)
		{
			System.Collections.IEnumerable units = ReadMember(cities[c], "units") as System.Collections.IEnumerable;
			if (units == null) continue;
			foreach (object u in units)
			{
				Actor a = u as Actor;
				if (a?.data == null || IsBoatUnit(a)) continue;
				if (Invoke(a, "isAlive") is bool alive && !alive) continue;
				long id = ((BaseSystemData)a.data).id;
				if (id == excludeId) continue;
				if (GameText.ActorAge(a) >= 18) return a;
			}
		}
		return null;
	}

	/// <summary>Юнит по глобальному id по всем городам мира (поиск разлитых членов династии).</summary>
	public static Actor FindActorById(long actorId)
	{
		if (actorId == 0) return null;
		try
		{
			MapBox w = World.world;
			if (w == null) return null;
			// 0.23.8: сначала общий список юнитов мира (бродяги без города тоже нужны древу).
			if ((object)w.units?.getSimpleList() is System.Collections.IEnumerable everyone)
			{
				foreach (object u in everyone)
				{
					Actor a = u as Actor;
					if (a?.data != null && ((BaseSystemData)a.data).id == actorId) return a;
				}
			}
			if (w.cities == null) return null;
			System.Collections.IEnumerable all = CityList(w);
			if (all == null) return null;
			foreach (object c in all)
			{
				City city = c as City;
				if (city == null) continue;
				System.Collections.IEnumerable units = ReadMember(city, "units") as System.Collections.IEnumerable;
				if (units == null) continue;
				foreach (object u in units)
				{
					Actor a = u as Actor;
					if (a?.data != null && ((BaseSystemData)a.data).id == actorId) return a;
				}
			}
		}
		catch (Exception) { }
		return null;
	}

	/// <summary>Переименовать живого юнита (выборник удостаивается имени лидера народа).</summary>
	public static bool TryRenameActor(Actor actor, string name)
	{
		if (actor == null || string.IsNullOrEmpty(name)) return false;
		try
		{
			if (actor.data != null)
			{
				Invoke(actor, "setName", name);
				WriteMember(actor.data, "name", name);
			}
			Invoke(actor, "setName", name);
			Invoke(actor, "rename", name);
			return true;
		}
		catch (Exception) { return false; }
	}

	// ============ 0.23.12: корабли — национальные имена в «кавычках» ============

	/// <summary>Юнит — корабль игры (boat_trading_*, boat_transport_* …), а не живое существо.</summary>
	public static bool IsBoatUnit(Actor actor)
	{
		if (actor == null)
		{
			return false;
		}
		try
		{
			if (Invoke(actor, "isBoat") is bool isBoat && isBoat)
			{
				return true;
			}
			object asset = ReadMember(actor, "asset");
			if (asset != null && ReadMember(asset, "is_boat") is bool assetBoat && assetBoat)
			{
				return true;
			}
			string speciesId = BoatSpeciesId(actor);
			if (!string.IsNullOrEmpty(speciesId) && speciesId.IndexOf("boat", StringComparison.OrdinalIgnoreCase) >= 0)
			{
				return true;
			}
			string name = GameText.ActorName(actor);
			if (!string.IsNullOrEmpty(name) && name.IndexOf("boat", StringComparison.OrdinalIgnoreCase) >= 0)
			{
				return true;
			}
		}
		catch (Exception)
		{
		}
		return false;
	}

	/// <summary>id подвида/ассета юнита: «boat_trading_human». Пусто — определить не удалось.</summary>
	public static string BoatSpeciesId(Actor actor)
	{
		if (actor == null)
		{
			return string.Empty;
		}
		try
		{
			object subspecies = ReadMember(actor, "subspecies");
			if (subspecies != null && ReadMember(subspecies, "species_id") is string sid && !string.IsNullOrEmpty(sid))
			{
				return sid;
			}
			object asset = ReadMember(actor, "asset");
			object id = (asset == null) ? null : ReadMember(asset, "id");
			return id?.ToString() ?? string.Empty;
		}
		catch (Exception)
		{
			return string.Empty;
		}
	}

	/// <summary>Образец корабельного имени: уже в «кавычках» — как есть, иначе оборачиваем.</summary>
	public static string QuoteBoatName(string name)
	{
		if (string.IsNullOrWhiteSpace(name))
		{
			return string.Empty;
		}
		string trimmed = name.Trim();
		if (trimmed.StartsWith("\u00ab", StringComparison.Ordinal) && trimmed.EndsWith("\u00bb", StringComparison.Ordinal))
		{
			return trimmed;
		}
		return "\u00ab" + trimmed + "\u00bb";
	}

	/// <summary>Выбирает имя корабля из национального пула (города, держава, известные люди).
	/// Имя нынешней клички корабля исключается, чтобы не получилось «Мур» → «Мур».
	/// Возвращает имя в «кавычках», пусто — пул пуст или совпадение.</summary>
	public static string PickBoatNameFromPool(List<string> pool, string currentName, Random rng)
	{
		if (pool == null || pool.Count == 0 || rng == null)
		{
			return string.Empty;
		}
		List<string> candidates = new List<string>();
		for (int i = 0; i < pool.Count; i++)
		{
			string s = (pool[i] ?? string.Empty).Trim();
			if (s.Length < 2)
			{
				continue;
			}
			if (!string.IsNullOrEmpty(currentName) && string.Equals(s, currentName.Trim(), StringComparison.OrdinalIgnoreCase))
			{
				continue;
			}
			string quoted = QuoteBoatName(s);
			if (!candidates.Contains(quoted))
			{
				candidates.Add(quoted);
			}
		}
		if (candidates.Count == 0)
		{
			return string.Empty;
		}
		return candidates[rng.Next(candidates.Count)];
	}

	/// <summary>
	/// Раз в год: переименовывает корабли мира в национальные имена их державы —
	/// по названиям её городов, самого государства и известных людей (нынешний правитель,
	/// основатель династии, павшие правители летописи). Имя всегда в «кавычках», чтобы
	/// «Пичина» (корабль) не путалась с королевой Пичиной. Идемпотентно: уже названное
	/// судно не трогаем. Возвращает число переименованных судов за этот год.
	/// </summary>
	public static int ApplyBoatNames(MapBox map, PoliticalWorld world, Random rng)
	{
		if (map == null || world == null || rng == null)
		{
			return 0;
		}
		int renamed = 0;
		try
		{
			List<Actor> units = map.units?.getSimpleList();
			if (units == null || units.Count == 0)
			{
				return 0;
			}
			// Имена павших правителей собираются один раз на проход (а не на каждый корабль).
			Dictionary<int, List<string>> notable = new Dictionary<int, List<string>>();
			for (int r = 0; r < world.Deaths.All.Count; r++)
			{
				DeathRecord d = world.Deaths.All[r];
				if (d == null || !d.WasRuler || d.KingdomId == null)
				{
					continue;
				}
				if (!notable.TryGetValue(d.KingdomId.Value, out List<string> names))
				{
					names = new List<string>();
					notable[d.KingdomId.Value] = names;
				}
				if (names.Count < 8)
				{
					names.Add(d.ActorName);
				}
			}
			for (int i = 0; i < units.Count; i++)
			{
				Actor boat = units[i];
				if (boat == null || boat.data == null || !IsBoatUnit(boat))
				{
					continue;
				}
				string currentName = GameText.ActorName(boat);
				if (!string.IsNullOrEmpty(currentName) && currentName.Trim().StartsWith("\u00ab", StringComparison.Ordinal))
				{
					continue; // нами уже названо
				}
				string nationName = PickBoatNameFor(boat, world, notable, currentName, rng);
				if (string.IsNullOrEmpty(nationName))
				{
					continue;
				}
				if (TryRenameActor(boat, nationName))
				{
					renamed++;
				}
			}
		}
		catch (Exception ex)
		{
			LogService.LogError("[Real Politic] ошибка переименования кораблей: " + ex.Message);
		}
		return renamed;
	}

	/// <summary>Национальный пул имён для корабля конкретной державы и выбор одного имени.</summary>
	private static string PickBoatNameFor(Actor boat, PoliticalWorld world, Dictionary<int, List<string>> notable, string currentName, Random rng)
	{
		try
		{
			Kingdom kingdom = ((BaseSimObject)boat).kingdom;
			if (kingdom == null && boat.city != null)
			{
				kingdom = boat.city.kingdom;
			}
			if (kingdom == null)
			{
				return string.Empty;
			}
			List<string> pool = new List<string>();
			List<City> cities = KingdomCities(kingdom);
			if (cities != null)
			{
				for (int c = 0; c < cities.Count; c++)
				{
					if (cities[c] != null)
					{
						AddPool(pool, ((NanoObject)cities[c]).name);
					}
				}
			}
			// 0.31.12: лодка носит коренное имя державы («Ракгар»), без ярлыка строя («Военный Орден»).
			string kingdomRaw = ((NanoObject)kingdom).name;
			if (!string.IsNullOrWhiteSpace(kingdomRaw) && !kingdomRaw.Trim().StartsWith("#", StringComparison.Ordinal))
			{
				AddPool(pool, RealPolitic.Core.IdeologyStateNames.ExtractBaseToponym(kingdomRaw, null));
			}
			Actor ruler = ReadRuler(kingdom);
			if (ruler != null)
			{
				AddPool(pool, GameText.ActorName(ruler));
			}
			int kingdomId = (int)((NanoObject)kingdom).id;
			KingdomPolitics state = world.GetKingdom(kingdomId);
			if (state != null)
			{
				AddPool(pool, state.DynastyFounder);
			}
			if (notable.TryGetValue(kingdomId, out List<string> deadRulers))
			{
				for (int d = 0; d < deadRulers.Count; d++)
				{
					AddPool(pool, deadRulers[d]);
				}
			}
			return PickBoatNameFromPool(pool, currentName, rng);
		}
		catch (Exception)
		{
			return string.Empty;
		}
	}

	private static void AddPool(List<string> pool, string name)
	{
		if (string.IsNullOrWhiteSpace(name))
		{
			return;
		}
		string trimmed = name.Trim();
		if (trimmed.Length < 2)
		{
			return;
		}
		string quoted = QuoteBoatName(trimmed);
		if (!pool.Contains(quoted))
		{
			pool.Add(quoted);
		}
	}

	/// <summary>Возвести юнита на престол королевства: setKing с вариациями аргументов.</summary>
	/// <summary>0.23.2: переименовать державу в игре (восстановленная держава получает старое имя).</summary>
	public static bool TryRenameKingdom(Kingdom kingdom, string name)
	{
		if (kingdom == null || string.IsNullOrEmpty(name))
		{
			return false;
		}
		try
		{
			object data = ReadMember(kingdom, "data");
			bool ok = data != null && WriteMember(data, "name", name);
			if (!ok)
			{
				ok = Invoke(kingdom, "setName", name) != null || TrySetProperty(kingdom, "name", name);
			}
			return ok;
		}
		catch (Exception)
		{
			return false;
		}
	}

	public static bool TrySetKingOnKingdom(object gameKingdom, Actor actor, bool quiet)
	{
		if (gameKingdom == null || actor == null) return false;
		try
		{
            // The boolean is pFromLoad, NOT "quiet". Runtime elections must
            // execute the game's normal ownership/profession bookkeeping.
            if (TryInvoke(gameKingdom, "setKing", out _, actor, false)
                || TryInvoke(gameKingdom, "setKing", out _, actor))
                return ReferenceEquals(ReadRuler(gameKingdom as Kingdom), actor);
            // Never write the king field directly: that bypasses game invariants.
            return false;
		}
		catch (Exception) { return false; }
	}

	/// <summary>Приспособить свергнутого правителя: городская должность или просто возвращение к народу.</summary>
	public static bool TryAppointToCity(object gameKingdom, Actor actor)
	{
		if (gameKingdom == null || actor == null) return false;
		List<City> cities = WorldCities(gameKingdom as Kingdom);
		if (cities == null) return false;
        // One actor can lead only one city. Prefer their home city and stop
        // after the first successful assignment; never assign the reigning king.
        if (ReferenceEquals(ReadRuler(gameKingdom as Kingdom), actor)) return false;
        City home = actor.city;
        if (home != null && cities.Contains(home)
            && TryInvoke(home, "setLeader", out _, actor, true)) return true;
        foreach (City city in new List<City>(cities))
        {
            if (city == null || ReferenceEquals(city, home)) continue;
            if (TryInvoke(city, "setLeader", out _, actor, true)
                || TryInvoke(city, "setLeader", out _, actor)) return true;
        }
        return false;
	}

	/// <summary>Вызвать метод без аргументов, подставив значения по умолчанию всех его параметров
	/// (для методов вроде Actor.die(bool = false, AttackType = Other, …)).</summary>
	public static bool InvokeWithDefaults(object target, string name)
	{
		if (target == null) return false;
		try
		{
			for (Type t = target.GetType(); t != null; t = t.BaseType)
			{
				foreach (MethodInfo m in t.GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly))
				{
					if (m.Name != name) continue;
					ParameterInfo[] ps = m.GetParameters();
					bool allDefault = true;
					object[] args = new object[ps.Length];
					for (int i = 0; i < ps.Length; i++)
					{
						if (!ps[i].HasDefaultValue) { allDefault = false; break; }
						args[i] = ps[i].DefaultValue;
						// у enum-параметров DefaultValue приходит как int
						if (ps[i].ParameterType.IsEnum && args[i] != null && !(args[i] is Enum))
						{
							args[i] = Enum.ToObject(ps[i].ParameterType, args[i]);
						}
					}
					if (!allDefault) continue;
					m.Invoke(target, args);
					return true;
				}
			}
		}
		catch (Exception ex) { LastInvokeError = ex.InnerException?.Message ?? ex.Message; }
		return false;
	}

	/// <summary>Кара судьи: главаря разгромленного мятежа предают смерти.</summary>
	public static bool TryExecuteActor(Actor actor)
	{
		if (actor == null) return false;
		try
		{
			// build 719: Actor.die(bool pDestroy=false, AttackType, bool, bool) — все параметры со значениями по умолчанию.
			if (InvokeWithDefaults(actor, "die")) return true;
			if (TryInvoke(actor, "killHimself", out _)) return true;
			if (TryInvoke(actor, "kill", out _)) return true;
			// Крайний случай: здоровье в ноль — смерть доберётся сама.
			if (actor.data != null)
			{
				return WriteMember(actor.data, "health", 0);
			}
		}
		catch (Exception) { }
		return false;
	}

	/// <summary>Присоединить юнита к клану (королева вводит детей в свой род).</summary>
	public static bool TryJoinClan(Actor actor, object clan)
	{
		if (actor == null || clan == null) return false;
		try
		{
			// build 719: единственный правильный путь — Actor.setClan(Clan): он сам обновляет реестр кланов.
			Invoke(actor, "setClan", clan);
			object current = ReadProperty(actor, "clan");
			if (!ReferenceEquals(current, clan))
			{
				// запасной путь для других сборок
				Invoke(clan, "addUnit", actor);
				WriteMember(actor, "clan", clan);
				current = ReadProperty(actor, "clan");
			}
			return ReferenceEquals(current, clan);
		}
		catch (Exception) { return false; }
	}

	/// <summary>Перевести юнита в другое королевство (мятежный вождь становится гражданином восставшей державы).</summary>
	public static bool TryReassignKingdom(Actor actor, object kingdom)
	{
		if (actor == null || kingdom == null) return false;
		try
		{
			Invoke(actor, "setKingdom", kingdom);
			object current = ReadProperty(actor, "kingdom") ?? Invoke(actor, "getKingdom");
			if (ReferenceEquals(current, kingdom)) return true;
			Invoke(actor, "forceKingdom", kingdom);
			Invoke(actor, "joinKingdom", kingdom);
			WriteMember(actor, "kingdom", kingdom);
			current = ReadProperty(actor, "kingdom") ?? Invoke(actor, "getKingdom");
			return ReferenceEquals(current, kingdom);
		}
		catch (Exception) { return false; }
	}
}
