using System;
using System.Collections.Generic;
using NeoModLoader.services;
using RealPolitic.Core;

namespace RealPolitic;

/// <summary>
/// Реальное влияние идеологий на игру («идеологии — не пук в лужу»).
///
/// Три канала воздействия, все через безопасные вызовы:
///  1. <b>Лояльность города</b> — постфикс на City.getLoyalty: город, где много жителей
///     с чужой идеологией, теряет лояльность (вплоть до восстаний, это ванильная механика),
///     а однородный идеологически город её получает.
///  2. <b>Настроение жителей</b> — периодический тик: несогласный житель постепенно
///     теряет счастье, согласный (убеждённый) — прибавляет. Счастье в WorldBox влияет
///     на восстания, работу и рождаемость.
///  3. <b>Недовольство</b> — годовое накопление Dissent у несогласных, с учётом
///     терпимости государственной идеологии (DissentTolerance).
/// </summary>
public static class IdeologyEffects
{
	/// <summary>Влияние идеологий на лояльность городов включено (настройка IdeologyAffectsLoyalty).</summary>
	public static bool AffectLoyalty = true;

	/// <summary>Влияние идеологий на настроение жителей включено (настройка IdeologyAffectsMood).</summary>
	public static bool AffectMood = true;

	/// <summary>Максимальный модификатор лояльности города от идеологий (±).</summary>
	public static int LoyaltyCap = 120;

	private static float _nextMoodTick;

	private static int _moodCursor;

	private static readonly Dictionary<int, int> _lastCityBonus = new Dictionary<int, int>();

	private static readonly Dictionary<int, float> _cityBonusTimestamp = new Dictionary<int, float>();

	/// <summary>Очистка кэша поправок лояльности при смене или сбросе мира.</summary>
	public static void ClearCache()
	{
		_lastCityBonus.Clear();
		_cityBonusTimestamp.Clear();
		_moodCursor = 0;
	}

	/// <summary>Итоговая поправка к лояльности города от идеологического состава жителей (с секундным кэшированием).</summary>
	/// <summary>0.53.1: как долго держим посчитанную поправку лояльности (было 1 с).</summary>
	private const float LoyaltyCacheSeconds = 3f;

	public static int CityLoyaltyBonus(City city)
	{
		if (!AffectLoyalty || city == null)
		{
			return 0;
		}
		try
		{
			int cityId = ReadId(city);
			if (cityId < 0)
			{
				return 0;
			}
			float now = UnityEngine.Time.unscaledTime;
			if (_cityBonusTimestamp.TryGetValue(cityId, out float lastTime) && (now - lastTime < LoyaltyCacheSeconds) && now >= lastTime)
			{
				if (_lastCityBonus.TryGetValue(cityId, out int cached))
				{
					return cached;
				}
			}
			Kingdom kingdom = city.kingdom;
			if (kingdom == null)
			{
				return 0;
			}
			int kingdomId = ReadId(kingdom);
			IdeologyType state = ActorIdeologies.StateIdeologyOf(kingdomId);
			List<Actor> units = city.units;
			if (units == null || units.Count == 0)
			{
				return 0;
			}
			float sum = 0f;
			int counted = 0;
			for (int i = 0; i < units.Count; i++)
			{
				Actor actor = units[i];
				if (actor == null || !IsCountable(actor))
				{
					continue;
				}
				ActorIdeologies.Faith faith = ActorIdeologies.Get(actor);
				if (faith == null)
				{
					continue;
				}
				counted++;
				sum += ActorIdeologyRoll.LoyaltyDelta(faith.Ideology, state, faith.Conviction, faith.Dissent);
			}
			if (counted == 0)
			{
				return 0;
			}
				int bonus = (int)Math.Round(sum / counted);
				KingdomPolitics kState = RealPoliticMod.World?.GetKingdom(kingdomId);
				if (kState != null)
				{
					int graceYears = RealPoliticMod.World?.Params?.YoungStateGraceYears ?? 50;
					if (kState.IsYoungState(RealPoliticMod.CurrentYear(), graceYears))
					{
						// Первые 50 лет существования государства народ не бузит на молодую власть:
						// идеологические штрафы лояльности провинций не раскачивают новую страну.
						if (bonus < 0)
						{
							bonus = 0;
						}
					}
					else
					{
						// Низкая легитимность и внутренние смуты сокрушают лояльность провинций
						if (kState.Legitimacy < 50f)
						{
							bonus -= (int)Math.Round((50f - kState.Legitimacy) * 1.5f);
						}
						else if (kState.Legitimacy > 75f)
						{
							bonus += (int)Math.Round((kState.Legitimacy - 75f) * 0.3f);
						}

						// Высокий уровень недовольства (Dissent) дополнительно расшатывает города
						if (kState.AvgDissent > 0.35f)
						{
							bonus -= (int)Math.Round((kState.AvgDissent - 0.35f) * 100f);
						}

						// Накопленная угроза кризиса (смута, застой, раскол) нагнетает бунт
						if (kState.Mechanics.ThreatLevel >= 75f)
						{
							bonus -= 30;
						}
					}
				}
				if (state == IdeologyType.Ethnonationalism)
				{
					string kRace = GameRefs.KingdomSpeciesId(kingdom);
					string cRace = GameRefs.CitySpeciesId(city);
					if (!string.IsNullOrEmpty(kRace) && !string.IsNullOrEmpty(cRace))
					{
						if (!string.Equals(kRace, cRace, StringComparison.OrdinalIgnoreCase))
						{
							bonus -= 45;
						}
						else
						{
							bonus += 15;
						}
					}
				}
			if (bonus > LoyaltyCap)
			{
				bonus = LoyaltyCap;
			}
			if (bonus < -LoyaltyCap)
			{
				bonus = -LoyaltyCap;
			}
			_lastCityBonus[cityId] = bonus;
			_cityBonusTimestamp[cityId] = now;
			return bonus;
		}
		catch (Exception)
		{
			return 0;
		}
	}

	/// <summary>Последняя посчитанная поправка (для показа в окне города/летописи без пересчёта).</summary>
	public static int LastCityBonus(int cityId)
	{
		return _lastCityBonus.TryGetValue(cityId, out int v) ? v : 0;
	}

	/// <summary>
	/// Периодический тик настроения: порциями (чтобы не нагружать кадр) проходим по живым
	/// юнитам и двигаем счастье в зависимости от согласия с государственной идеологией.
	/// </summary>
	public static void Tick(float unscaledTime, int currentYear, SimParams p)
	{
		if (!AffectMood && !AffectLoyalty)
		{
			return;
		}
		if (unscaledTime < _nextMoodTick)
		{
			return;
		}
		_nextMoodTick = unscaledTime + 1.0f;
		try
		{
			MapBox world = World.world;
			List<Actor> units = world?.units?.getSimpleList();
			if (units == null || units.Count == 0)
			{
				return;
			}
			// 200 юнитов за тик — полный обход большого мира занимает несколько секунд.
			int batch = Math.Min(200, units.Count);
			for (int n = 0; n < batch; n++)
			{
				if (_moodCursor >= units.Count)
				{
					_moodCursor = 0;
				}
				Actor actor = units[_moodCursor++];
				if (actor == null || !IsCountable(actor))
				{
					continue;
				}
				ActorIdeologies.Faith faith = ActorIdeologies.Get(actor);
				if (faith == null)
				{
					continue;
				}
				if (faith.LastDissentYear < currentYear)
				{
					faith.LastDissentYear = currentYear;
					ActorIdeologies.AdvanceYear(actor, p?.DissentRate ?? 0.2f, p?.DissentDecay ?? 0.25f);
				}
				if (AffectMood)
				{
					IdeologyType state = ActorIdeologies.StateIdeologyOf(ActorIdeologies.KingdomIdOf(actor));
					int delta = ActorIdeologyRoll.HappinessDelta(faith.Ideology, state, faith.Conviction, faith.Dissent);
					if (delta != 0)
					{
						ApplyHappiness(actor, delta);
					}
				}
			}
		}
		catch (Exception ex)
		{
			LogOnce("ошибка тика настроения по идеологии: " + ex.Message);
		}
	}

	/// <summary>Счастье жителя: setHappiness(getHappiness() + delta) / data.happiness через безопасную прослойку.</summary>
	public static void ApplyHappiness(Actor actor, int delta)
	{
		if (actor == null || delta == 0)
		{
			return;
		}
		try
		{
			object data = GameRefs.ReadMember(actor, "data");
			object current = GameRefs.Invoke(actor, "getHappiness");
			if (data != null && GameRefs.ReadMember(data, "happiness") is int dataHappiness
				&& (!(current is int curInt) || (curInt == 0 && dataHappiness != 0)))
			{
				current = dataHappiness;
			}
			if (!(current is int value))
			{
				return;
			}
			int clamped = Math.Max(0, Math.Min(100, value + delta));
			if (!GameRefs.TryInvoke(actor, "setHappiness", out _, clamped, true))
			{
				GameRefs.TryInvoke(actor, "changeHappiness", out _, null, delta);
			}
			if (data != null)
			{
				GameRefs.WriteMember(data, "happiness", clamped);
			}
		}
		catch (Exception)
		{
		}
	}

	/// <summary>Учитываем только разумных живых civ-юнитов государства (не животных, не корабли и не мобов).</summary>
	private static bool IsCountable(Actor actor)
	{
		try
		{
			if (actor == null || GameRefs.IsBoatUnit(actor))
			{
				return false;
			}
			if (!(GameRefs.Invoke(actor, "isAlive") is bool alive) || !alive)
			{
				return false;
			}
			if (GameRefs.Invoke(actor, "isSapient") is bool sapient && !sapient)
			{
				return false;
			}
			if (GameRefs.Invoke(actor, "isKingdomCiv") is bool civ && !civ)
			{
				return false;
			}
			return true;
		}
		catch (Exception)
		{
			return false;
		}
	}

	private static int ReadId(object nanoObject)
	{
		// 0.53.1: прямой доступ вместо отражения — вызывается из патча лояльности.
		try
		{
			if (nanoObject is NanoObject nano)
			{
				long fast = nano.id;
				if (fast > 0L && fast <= int.MaxValue)
				{
					return (int)fast;
				}
			}
		}
		catch (Exception)
		{
		}
		object id = GameRefs.ReadMember(nanoObject, "id");
		if (id is long l)
		{
			return (int)l;
		}
		return (id is int i) ? i : -1;
	}

	private static readonly HashSet<string> _logged = new HashSet<string>();

	private static void LogOnce(string message)
	{
		if (_logged.Add(message))
		{
			LogService.LogError("[Real Politic] " + message);
		}
	}
}
