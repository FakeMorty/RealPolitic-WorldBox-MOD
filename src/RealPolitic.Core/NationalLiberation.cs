using System;
using System.Collections.Generic;
using System.Linq;

namespace RealPolitic.Core;

public sealed class CityHomelandRecord
{
    public int CityId { get; set; }
    public int OriginalKingdomId { get; set; }
    public string OriginalKingdomName { get; set; } = string.Empty;
    public IdeologyType OriginalIdeology { get; set; } = IdeologyType.Monarchy;
    public bool WasHistoricalCapital { get; set; }
    public int AnnexedYear { get; set; }
}

public sealed class CityHomelandBook
{
    private readonly Dictionary<int, CityHomelandRecord> _byCity = new Dictionary<int, CityHomelandRecord>();
    private readonly Dictionary<int, int> _lastLiberationAttemptByHomeland = new Dictionary<int, int>();

    public IReadOnlyDictionary<int, CityHomelandRecord> All => _byCity;

    public int Count => _byCity.Count;

    public void Clear()
    {
        _byCity.Clear();
        _lastLiberationAttemptByHomeland.Clear();
    }

    public void EnsureRecorded(int cityId, int kingdomId, string kingdomName, IdeologyType ideology, bool isCapital = false)
    {
        if (cityId <= 0 || kingdomId <= 0) return;
        if (!_byCity.TryGetValue(cityId, out CityHomelandRecord? rec))
        {
            _byCity[cityId] = new CityHomelandRecord
            {
                CityId = cityId,
                OriginalKingdomId = kingdomId,
                OriginalKingdomName = kingdomName ?? string.Empty,
                OriginalIdeology = ideology,
                WasHistoricalCapital = isCapital
            };
        }
        else if (rec.OriginalKingdomId == kingdomId && string.IsNullOrEmpty(rec.OriginalKingdomName) && !string.IsNullOrEmpty(kingdomName))
        {
            rec.OriginalKingdomName = kingdomName;
        }
    }

    public void MarkAnnexedIfChanged(int cityId, int currentKingdomId, int year)
    {
        if (_byCity.TryGetValue(cityId, out CityHomelandRecord? rec))
        {
            if (rec.OriginalKingdomId != currentKingdomId && rec.AnnexedYear == 0)
            {
                rec.AnnexedYear = year;
            }
            else if (rec.OriginalKingdomId == currentKingdomId)
            {
                rec.AnnexedYear = 0;
            }
        }
    }

    public void Restore(CityHomelandRecord record)
    {
        if (record != null && record.CityId > 0 && record.OriginalKingdomId > 0)
        {
            _byCity[record.CityId] = record;
        }
    }

    public CityHomelandRecord? Get(int cityId)
    {
        return _byCity.TryGetValue(cityId, out CityHomelandRecord? rec) ? rec : null;
    }

    public bool CanAttemptLiberation(int originalKingdomId, int year, int cooldownYears = 12)
    {
        if (!_lastLiberationAttemptByHomeland.TryGetValue(originalKingdomId, out int lastYear))
            return true;
        return year - lastYear >= cooldownYears;
    }

    public void RecordLiberationAttempt(int originalKingdomId, int year)
    {
        _lastLiberationAttemptByHomeland[originalKingdomId] = year;
    }

    public void RebindHomeland(int cityId, int newKingdomId, string newKingdomName)
    {
        if (cityId <= 0 || newKingdomId <= 0) return;
        if (_byCity.TryGetValue(cityId, out CityHomelandRecord? existing) && existing != null)
        {
            existing.OriginalKingdomId = newKingdomId;
            if (!string.IsNullOrWhiteSpace(newKingdomName))
            {
                existing.OriginalKingdomName = newKingdomName;
            }
            existing.AnnexedYear = 0;
        }
    }
}

public static class NationalLiberationEngine
{
    public static void TickKingdom(KingdomPolitics occupier, PoliticalWorld world, int year)
    {
        if (occupier == null || world == null) return;

        // Синхронизируем историческую родину всех городов державы
        var occupierCities = new List<CityPolitics>();
        foreach (CityPolitics c in world.Cities)
        {
            if (c.KingdomId == occupier.KingdomId)
            {
                occupierCities.Add(c);
                world.CityHomelands.EnsureRecorded(
                    c.CityId,
                    occupier.KingdomId,
                    world.KingdomNameOf(occupier.KingdomId),
                    occupier.Ideology);
                world.CityHomelands.MarkAnnexedIfChanged(c.CityId, occupier.KingdomId, year);
            }
        }

        // Если у державы меньше 2 городов, отделяться нечему (единственный город — столица)
        if (occupierCities.Count < 2) return;
        int graceYears = world.Params != null ? world.Params.YoungStateGraceYears : 50;
        if (occupier.FoundedYear > 0 && year - occupier.FoundedYear < graceYears) return;

        // Проверяем условия для пробуждения национально-освободительного движения
        bool pressureHigh = occupier.AvgDissent >= 0.30f
            || occupier.Legitimacy < 45f
            || occupier.WarFatigue >= 50f
            || (occupier.Constitution?.Electoral != null && occupier.Constitution.Electoral.RegionalExit == RegionalExitLaw.RegionalRight && occupier.AvgDissent >= 0.20f);

        if (!pressureHigh) return;

        string occupierName = world.KingdomNameOf(occupier.KingdomId);
        int occupierCapitalId = world.CapitalCityIdOf(occupier.KingdomId);

        // Группируем инородные города по их исторической родине (которой сейчас нет на карте)
        var groups = new Dictionary<int, List<CityHomelandRecord>>();
        foreach (CityPolitics c in occupierCities)
        {
            // Никогда не отдаём единственную столицу метрополии, если городов всего два
            if (occupierCapitalId > 0 && c.CityId == occupierCapitalId && occupierCities.Count == 2) continue;

            CityHomelandRecord? rec = world.CityHomelands.Get(c.CityId);
            if (rec == null || rec.OriginalKingdomId == occupier.KingdomId) continue;
            Union? occupierUnion = world.UnionOf(occupier.KingdomId);
            if (occupierUnion != null && occupierUnion.Type == UnionType.Merger && occupierUnion.GetAbsorbed(rec.OriginalKingdomId) != null
                && !(occupier.AvgDissent >= 0.55f && occupier.Legitimacy < 30f))
            {
                continue;
            }
            if (world.GetKingdom(rec.OriginalKingdomId) != null) continue; // Историческая держава ещё жива
            if (!world.CityHomelands.CanAttemptLiberation(rec.OriginalKingdomId, year)) continue;

            // Нельзя «освобождать» державу саму от себя или создавать дубликат уже живого имени на карте
            if (!string.IsNullOrWhiteSpace(rec.OriginalKingdomName))
            {
                if (string.Equals(rec.OriginalKingdomName, occupierName, StringComparison.OrdinalIgnoreCase))
                    continue;
                bool sameNameAlive = false;
                foreach (KingdomPolitics living in world.Kingdoms)
                {
                    if (string.Equals(world.KingdomNameOf(living.KingdomId), rec.OriginalKingdomName, StringComparison.OrdinalIgnoreCase))
                    {
                        sameNameAlive = true;
                        break;
                    }
                }
                if (sameNameAlive) continue;
            }

            if (!groups.TryGetValue(rec.OriginalKingdomId, out List<CityHomelandRecord>? list))
            {
                list = new List<CityHomelandRecord>();
                groups[rec.OriginalKingdomId] = list;
            }
            list.Add(rec);
        }

        if (groups.Count == 0) return;

        // Выбираем самое крупное поглощённое государство
        var bestGroup = groups.Values
            .OrderByDescending(g => g.Count)
            .ThenBy(g => g[0].OriginalKingdomId)
            .First();

        // Нельзя забрать все города у метрополии — минимум 1 город (столица) остаётся у метрополии
        int maxLiberated = Math.Min(bestGroup.Count, occupierCities.Count - 1);
        if (maxLiberated <= 0) return;

        CityHomelandRecord sample = bestGroup[0];
        world.CityHomelands.RecordLiberationAttempt(sample.OriginalKingdomId, year);

        var liberatedCityIds = bestGroup
            .OrderByDescending(r => r.WasHistoricalCapital)
            .ThenBy(r => r.CityId)
            .Take(maxLiberated)
            .Select(r => r.CityId)
            .ToList();

        bool severeCrisis = occupier.AvgDissent >= 0.55f
            || occupier.WarFatigue >= 55f
            || occupier.Legitimacy <= 25f;
        bool recognizeVassalLands = ShouldRecognizeVassalLands(occupier) && !severeCrisis;
        string homelandName = string.IsNullOrEmpty(sample.OriginalKingdomName)
            ? "#" + sample.OriginalKingdomId
            : sample.OriginalKingdomName;

        if (recognizeVassalLands)
        {
            // 0.31.2: метрополия может мирно признать только вассальные земли.
            // Отдельной страны на карте не появляется, все города остаются у метрополии;
            // независимость позднее возможна только восстанием и войной.
            Union? existingUnion = world.UnionOf(sample.OriginalKingdomId);
            UnionMember? existingMember = existingUnion?.GetMember(sample.OriginalKingdomId);
            bool alreadyRegistered = existingUnion != null
                && existingUnion.Type == UnionType.Vassal
                && existingUnion.LeaderId == occupier.KingdomId
                && existingMember?.CityIds != null
                && liberatedCityIds.All(cityId => existingMember.CityIds.Contains(cityId));
            if (alreadyRegistered)
            {
                return;
            }
            bool registered = world.TryRegisterAbsorbedVassalTerritory(
                occupier.KingdomId,
                sample.OriginalKingdomId,
                homelandName,
                sample.OriginalIdeology,
                liberatedCityIds,
                liberatedCityIds[0],
                0.50f);
            if (!registered)
            {
                return;
            }
            occupier.Legitimacy = KingdomSimulation.Clamp(occupier.Legitimacy + 2f, 0f, 100f);
            occupier.AvgDissent = KingdomSimulation.Clamp(occupier.AvgDissent - 0.06f, 0f, 1f);
            world.DiplomaticMemories.Record(sample.OriginalKingdomId, occupier.KingdomId, DiplomaticMemoryKind.HonoredPeace, year, 40, 10, homelandName);
            world.Bus.Publish(new PolEvent(PolEventType.NationalLiberationUprising, "RP29_EV_LIBERATION")
            {
                Year = year,
                KingdomId = occupier.KingdomId,
                Severity = Severity.Major
            }.With("kingdom", world.KingdomNameOf(occupier.KingdomId))
                .With("homeland", homelandName)
                .With("cities", liberatedCityIds.Count.ToString())
                .With("mode", "RP29_LIBERATION_VASSAL"));
            return;
        }

        // Ниже — только вооружённое восстание: оформляем заказ на возрождение
        // исторического государства (первый город в PrewarCityIds остаётся за метрополией,
        // остальные уходят возрождённой державе, игровой слой объявляет войну).
        int keptCityId = occupierCities.First(c => !liberatedCityIds.Contains(c.CityId)).CityId;
        var orderCities = new List<int> { keptCityId };
        orderCities.AddRange(liberatedCityIds);

        world.AddPendingRestoration(new RestorationOrder
        {
            DefeatedId = sample.OriginalKingdomId,
            VictorId = occupier.KingdomId,
            Mode = RestorationMode.Free,
            CitiesKeptByVictor = 1,
            DefeatedName = homelandName,
            Year = year,
            PrewarCityIds = orderCities,
            IsNationalLiberation = true,
            LiberationPeaceful = false
        });

        occupier.Legitimacy = KingdomSimulation.Clamp(occupier.Legitimacy - 10f, 0f, 100f);
        occupier.Mechanics.ThreatLevel = KingdomSimulation.Clamp(occupier.Mechanics.ThreatLevel + 12f, 0f, 100f);
		world.AddGrievance(occupier.KingdomId, sample.OriginalKingdomId, GrievanceReason.BrokenUnion, 40f);
		world.DiplomaticMemories.Record(sample.OriginalKingdomId, occupier.KingdomId, DiplomaticMemoryKind.AnnexedOurCity, year, 80, -40, homelandName);
		world.Bus.Publish(new PolEvent(PolEventType.NationalLiberationUprising, "RP29_EV_LIBERATION")
		{
			Year = year,
			KingdomId = occupier.KingdomId,
			Severity = Severity.Major
		}.With("kingdom", world.KingdomNameOf(occupier.KingdomId))
			.With("homeland", homelandName)
			.With("cities", liberatedCityIds.Count.ToString())
			.With("mode", "RP29_LIBERATION_WAR")
			.With("confirmed", "0"));

	}

    /// <summary>Готова ли метрополия мирно признать исторические земли вассальными.
    /// Независимость этим путём не даётся никогда.</summary>
    public static bool ShouldRecognizeVassalLands(KingdomPolitics occupier)
    {
        if (occupier == null) return false;
        if (occupier.Constitution?.Electoral != null && occupier.Constitution.Electoral.RegionalExit == RegionalExitLaw.RegionalRight)
            return true;
        if (occupier.Ideology == IdeologyType.Democracy
            || occupier.Ideology == IdeologyType.Liberalism
            || occupier.Ideology == IdeologyType.Egalitarianism)
        {
            return occupier.Personality?.Trait != RulerTrait.Conqueror && occupier.Personality?.Trait != RulerTrait.Cruel;
        }
        if (occupier.Personality != null && (occupier.Personality.Trait == RulerTrait.Just || occupier.Personality.Trait == RulerTrait.Diplomat))
            return true;
        return false;
    }
}
