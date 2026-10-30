using System;
using RealPolitic.Core;

namespace RealPolitic;

public class GameDeaths
{
	private readonly PoliticalWorld _world;

	public bool TrackCommoners;

	public GameDeaths(PoliticalWorld world)
	{
		_world = world;
		_world.Deaths.OnDeath += OnDeathRecorded;
	}

	public void OnActorDeath(Actor actor, string damageType, Actor killer)
	{
        if(NativeWorldLifecycle.Transitioning)return;
		if (actor == null)
		{
			return;
		}
		int observedYear = RealPoliticMod.CurrentYear();
        if (observedYear < 0) observedYear = _world.Year;
        int num = GameText.ActorId(actor);
		if (num < 0)
		{
			return;
		}
		// Житель умер: убираем его идеологию из сводок. Без этого «несогласные»
		// в летописи считали бы мёртвых — в длинных мирах счётчики раздувались
		// до нескольких населений державы (мир 300 лет: 135+1262 при населении 478).
		ActorIdeologies.Forget(num);
		// Родословная: точная дата смерти вместо «пропал из виду N лет назад».
		try
		{
			_world.Family.MarkDead(GameText.ActorLongId(actor), observedYear);
		}
		catch (Exception)
		{
		}
		// Защита от повторного срабатывания (die() может вызываться несколько раз на одном юните).
		if (_world.Deaths.Get(num) != null)
		{
			return;
		}
		bool flag = IsRuler(actor);
		bool flag2 = IsChosen(actor);
        Kingdom origin = actor.kingdom;
        // Only small lists can supply last-resident evidence. Avoid scanning
        // a large kingdom for every ordinary death, while allowing boats/corpses in units.
        int populationBefore = origin?.units != null && origin.units.Count <= 32 && _world.GetKingdom((int)origin.id) != null
            && !(GameRefs.ReadMember(origin, "wild") is bool wild && wild)
            && !(GameRefs.Invoke(origin, "isCiv") is bool civic && !civic) ? CountLivingResidents(origin) : -1;
        bool lastCandidate = populationBefore == 1;
		if (!flag && !flag2 && !lastCandidate && !TrackCommoners)
		{
			_world.UnregisterActor(num);
			return;
		}
		int? num2 = null;
		string text = string.Empty;
		string text2 = string.Empty;
		string killerKingdom = string.Empty;
		string killerCity = string.Empty;
		string killerJob = string.Empty;
		string killerSpecies = string.Empty;
		string killerRace = string.Empty;
		bool killerFemale = false;
		int killerAge = 0;
		bool killerIsCiv = false;
		bool killerIsBoat = false;
		if (killer != null)
		{
			num2 = GameText.ActorId(killer);
			text = GameText.ActorName(killer);
			text2 = GameText.ActorTitle(killer);
			// 0.23.12: корабль (boat_trading/transport…) — судно, а не зверь и не житель. Его пушки
			// расстреливают правителя, поэтому проверяем судно раньше цивилизованности.
			killerIsBoat = IsBoatUnit(killer);
			killerIsCiv = !killerIsBoat && IsCivUnit(killer);
			killerFemale = GameText.ActorIsFemale(killer);
			killerAge = Math.Max(0, GameText.ActorAge(killer));
			killerKingdom = ResolveKillerKingdomName(killer);
			killerCity = (killer.city == null) ? string.Empty : (((NanoObject)killer.city).name ?? string.Empty);
			if (killerIsCiv)
			{
				// Разумный убийца: где живёт и чем занимается, какого он вида.
				killerJob = ProfessionName(killer);
				killerRace = SpeciesName(killer);
			}
			else if (killerIsBoat)
			{
				// Судно: вид сохраняем как есть (boat_*), в летописи он превратится
				// в «корабль-торговец/перевозчик», а не в сырой id, плюс держава и порт приписки.
				killerSpecies = SpeciesName(killer);
			}
			else
			{
				// Зверь: какого он вида.
				killerSpecies = SpeciesName(killer);
			}
		}
		DeathCause cause = DeathCauseClassifier.Classify(damageType, killer != null, killerIsCiv, killer != null && IsAssassin(killer), InRebellion(actor), damageType?.ToLowerInvariant().Contains("sacrifice") ?? false, killerIsBoat);
		DeathRegistry deaths = _world.Deaths;
		string actorName = GameText.ActorName(actor);
		int year = observedYear;
		int? killerId = num2;
		string killerName = text;
		string killerTitle = text2;
		int? kingdomId = ((((BaseSimObject)actor).kingdom == null) ? ((int?)null) : new int?((int)((NanoObject)((BaseSimObject)actor).kingdom).id));
		string cityName = ((actor.city == null) ? string.Empty : ((NanoObject)actor.city).name);
		bool wasRuler = flag;
		bool wasChosen = flag2;
		int age = GameText.ActorAge(actor);
		bool wasFemale = GameText.ActorIsFemale(actor);
		int? reign = null;
		if (wasRuler)
		{
			// Патч смерти — префикс на die(): игра ещё не сбросила kingdom.king,
			// и политический мир сохранил ссылку на умершего правителя.
			RulerRef ruler = _world.GetKingdom(kingdomId ?? -1)?.Ruler;
			if (ruler != null && ruler.ActorId == num && ruler.SinceYear > 0)
			{
				reign = Math.Max(0, year - ruler.SinceYear + 1);
			}
		}
		deaths.Record(num, actorName, cause, year, killerId, killerName, killerTitle, null, kingdomId, cityName, wasRuler, wasChosen, age, "", wasFemale, reign, record =>
		{
			record.PopulationBeforeDeath = populationBefore;
            record.KingdomNameAtDeath = origin == null ? string.Empty : origin.name;
            record.KillerIsCiv = killerIsCiv;
			record.KillerKingdom = killerKingdom;
			record.KillerCity = killerCity;
			record.KillerJob = killerJob;
			record.KillerSpecies = killerSpecies;
			record.KillerRace = killerRace;
			record.KillerFemale = killerFemale;
			record.KillerAge = killerAge;
		});
		_world.UnregisterActor(num);
	}

    public static string ResolveKillerKingdomName(Actor killer)
    {
        if (killer == null) return string.Empty;
        try
        {
            Kingdom kk = ((BaseSimObject)killer).kingdom ?? killer.city?.kingdom;
            if (IsCivilizedKingdom(kk) && !string.IsNullOrEmpty(((NanoObject)kk).name))
            {
                return ((NanoObject)kk).name;
            }
            var list = World.world?.kingdoms?.list;
            if (list != null)
            {
                for (int i = 0; i < list.Count; i++)
                {
                    Kingdom candidate = list[i];
                    if (IsCivilizedKingdom(candidate) && candidate.units != null && candidate.units.Contains(killer)
                        && !string.IsNullOrEmpty(((NanoObject)candidate).name))
                    {
                        return ((NanoObject)candidate).name;
                    }
                }
            }
        }
        catch (Exception)
        {
        }
        return string.Empty;
    }

    private static bool IsCivilizedKingdom(Kingdom kk)
    {
        if (kk == null) return false;
        if (GameRefs.ReadMember(kk, "wild") is bool wild && wild) return false;
        if (GameRefs.Invoke(kk, "isCiv") is bool isCiv && !isCiv) return false;
        return true;
    }

    public static int CountLivingResidents(Kingdom kingdom)
    {
        if (kingdom?.units == null) return -1;
        int count = 0;
        foreach (Actor resident in kingdom.units)
        {
            if (resident == null) continue;
            if (GameRefs.IsBoatUnit(resident)) continue;
            if (GameRefs.Invoke(resident, "isAlive") is bool alive && !alive) continue;
            // Unknown life state is treated as living: do not invent extinction.
            count++;
        }
        return count;
    }

    public void ConfirmActorDeath(Actor actor, int actorId, Kingdom origin)
    {
        if (!(GameRefs.Invoke(actor, "isAlive") is bool alive) || alive) return;
        DeathRecord record = _world.Deaths.Get(actorId);
        if (record == null || record.PopulationBeforeDeath != 1 || origin == null || _world.GetKingdom((int)origin.id) == null) return;
        if (!_world.ChronicleCauses.Confirm(record, true, CountLivingResidents(origin))) return;
        _world.Bus.Publish(new PolEvent(PolEventType.LastResidentDied, "RP_EV_LAST_RESIDENT_DIED")
        { Year = record.Year, KingdomId = record.KingdomId, ActorId = actorId, Death = record, Severity = Severity.Major }
        .With("kingdom", record.KingdomNameAtDeath).With("resident", record.ActorName)
        .With("cause", record.LocKeyFor(record.WasFemale)));
    }

	private void OnDeathRecorded(DeathRecord rec)
	{
		if (rec != null && rec.WasRuler)
		{
			IdeologyType ideology = _world.GetKingdom(rec.KingdomId ?? -1)?.Ideology ?? IdeologyType.Monarchy;
			_world.Bus.Publish(new PolEvent(PolEventType.RulerDeath, "RP_EV_RULER_DEATH")
			{
				Year = rec.Year,
				Severity = Severity.Critical,
				KingdomId = rec.KingdomId,
				ActorId = rec.ActorId,
				Death = rec
		}.With("ruler", rec.ActorName)
			.With("title", Ideologies.RulerTitleKey(ideology, rec.WasFemale))
			.With("cause", rec.LocKeyFor(rec.WasFemale)));
		}
	}

	public static bool IsRuler(Actor actor)
	{
		if (actor == null)
		{
			return false;
		}
		try
		{
			Kingdom kingdom = ((BaseSimObject)actor).kingdom;
			if (kingdom == null)
			{
				return false;
			}
			Actor val = GameRefs.ReadRuler(kingdom);
			return val != null && val == actor;
		}
		catch (Exception)
		{
			return false;
		}
	}

	public static bool IsChosen(Actor actor)
	{
		if (actor == null)
		{
			return false;
		}
		try
		{
			if (GameRefs.ReadProperty(actor, "is_chosen") is bool result)
			{
				return result;
			}
			if (GameRefs.Invoke(actor, "hasTrait", "chosen") is bool result2)
			{
				return result2;
			}
			object obj = GameRefs.Invoke(actor, "hasTrait", "legendary");
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
		catch (Exception)
		{
			return false;
		}
	}

	/// <summary>
	/// Убийца — разумное существо (человек, эльф, орк, гном), а не зверь.
	/// Метода isCivUnit у Actor нет, поэтому решаем по трём признакам:
	/// разумный вид (isSapient), флаг civ в ассете и наличие королевства.
	/// Раньше проверка всегда возвращала false — и любой убийца, даже король,
	/// записывался как «растерзан зверем».
	/// </summary>
	private static bool IsCivUnit(Actor actor)
	{
		if (actor == null || IsBoatUnit(actor))
		{
			return false;
		}
		try
		{
			// WorldBox 0.51: разумность — у подвида (actor.subspecies.isSapient()),
			// цивилизованность королевства — Kingdom.isCiv(). Раньше проверялся
			// несуществующий Actor.isSapient() → любой убийца записывался зверем.
			object subspecies = GameRefs.ReadMember(actor, "subspecies");
			if (subspecies != null && GameRefs.Invoke(subspecies, "isSapient") is bool sapient)
			{
				return sapient;
			}
			if (GameRefs.Invoke(actor, "isSapient") is bool sapient2)
			{
				return sapient2;
			}
			object asset = GameRefs.ReadMember(actor, "asset");
			if (asset != null)
			{
				if (GameRefs.ReadMember(asset, "is_boat") is bool isBoat && isBoat) return false;
				if (GameRefs.ReadMember(asset, "is_sapient") is bool isSapient) return isSapient;
				if (GameRefs.ReadMember(asset, "civ") is bool civ) return civ;
				if (GameRefs.ReadMember(asset, "unit") is bool unit && unit) return true;
			}
			Kingdom kingdom = ((BaseSimObject)actor).kingdom;
			if (kingdom != null && GameRefs.Invoke(kingdom, "isCiv") is bool kingdomCiv)
			{
				return kingdomCiv;
			}
			// Запасной признак: у зверей нет королевства, у горожан и воинов — есть.
			return kingdom != null && !IsWildKingdom(kingdom);
		}
		catch (Exception)
		{
			return false;
		}
	}

	/// <summary>Королевство диких зверей (у животных в WorldBox оно тоже бывает).</summary>
	private static bool IsWildKingdom(Kingdom kingdom)
	{
		try
		{
			object id = GameRefs.ReadMember(kingdom, "asset_id") ?? GameRefs.ReadMember(kingdom, "asset");
			string text = (id == null) ? string.Empty : id.ToString().ToLowerInvariant();
			return text.Contains("wild") || text.Contains("animal") || text.Contains("neutral");
		}
		catch (Exception)
		{
			return false;
		}
	}

	/// <summary>
	/// Убийца — корабль игры, а не живое существо. WorldBox заводит корабли как
	/// юнитов («boat_trading_human», «boat_transport_human»…), но это суда: их пушки
	/// стреляют по врагам. Ищем слово «boat» в id подвида/ассета и в имени юнита.
	/// </summary>
	private static bool IsBoatUnit(Actor actor)
	{
		return GameRefs.IsBoatUnit(actor);
	}

	/// <summary>Вид зверя для летописи: «волк», «медведь» и т. п.</summary>
	public static string SpeciesName(Actor actor)
	{
		if (actor == null)
		{
			return string.Empty;
		}
		try
		{
			// 0.51: у юнита есть подвид; его базовый вид (human/elf/orc/dwarf…) локализуется игрой.
			object subspecies = GameRefs.ReadMember(actor, "subspecies");
			if (subspecies != null)
			{
				object speciesId = GameRefs.ReadMember(subspecies, "species_id");
				if (speciesId is string sid && !string.IsNullOrEmpty(sid))
				{
					string loc = GameText.Localized(sid, string.Empty);
					if (!string.IsNullOrEmpty(loc)) return loc;
					return GameText.Localized("race_" + sid, sid);
				}
			}
			object asset = GameRefs.ReadMember(actor, "asset");
			object id = (asset == null) ? null : GameRefs.ReadMember(asset, "id");
			if (id == null)
			{
				return string.Empty;
			}
			string key = id.ToString();
			// У видов есть штатная локализация, она же даёт русское название.
			return GameText.Localized(key, key);
		}
		catch (Exception)
		{
			return string.Empty;
		}
	}

	/// <summary>Занятие убийцы: воин, король, вождь, житель.</summary>
	public static string ProfessionName(Actor actor)
	{
		if (actor == null)
		{
			return string.Empty;
		}
		try
		{
			if (GameRefs.ReadProperty(actor, "is_profession_king") is bool king && king)
			{
				return GameText.Localized("RP_JOB_KING", "правитель");
			}
			if (GameRefs.ReadProperty(actor, "is_profession_leader") is bool leader && leader)
			{
				return GameText.Localized("RP_JOB_LEADER", "вождь");
			}
			if (GameRefs.ReadProperty(actor, "is_profession_warrior") is bool warrior && warrior)
			{
				return GameText.Localized("RP_JOB_WARRIOR", "воин");
			}
			return GameText.Localized("RP_JOB_CITIZEN", "житель");
		}
		catch (Exception)
		{
			return string.Empty;
		}
	}

	private static bool IsAssassin(Actor killer)
	{
		try
		{
			if (GameRefs.ReadProperty(killer, "is_assassin") is bool result)
			{
				return result;
			}
			return GameRefs.ReadProperty(killer, "plot")?.ToString().ToLowerInvariant().Contains("assassin") ?? false;
		}
		catch (Exception)
		{
			return false;
		}
	}

	private static bool InRebellion(Actor actor)
	{
		try
		{
			Kingdom kingdom = ((BaseSimObject)actor).kingdom;
			if (kingdom == null)
			{
				return false;
			}
			return GameRefs.ReadProperty(kingdom, "rebellion") != null;
		}
		catch (Exception)
		{
			return false;
		}
	}
}
