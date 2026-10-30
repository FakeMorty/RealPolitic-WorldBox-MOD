using System;
using System.Collections.Generic;
using System.Linq;

namespace RealPolitic.Core;

public sealed class AllySpoilsShare
{
    public int KingdomId { get; set; }
    public string KingdomName { get; set; } = string.Empty;
    public float ContributionWeight { get; set; }
    public int CitiesAwarded { get; set; }
    public float ReparationsPerYear { get; set; }
}

public sealed class PeaceConferenceResult
{
    public int Year { get; set; }
    public int LeadVictorId { get; set; }
    public int DefeatedId { get; set; }
    public int TotalCitiesDemanded { get; set; }
    public float TotalReparationsPerYear { get; set; }
    public int ReparationsYears { get; set; }
    public List<AllySpoilsShare> Shares { get; set; } = new List<AllySpoilsShare>();
    public List<int> ShortchangedAllyIds { get; set; } = new List<int>();
}

public static class PeaceConferenceEngine
{
    public static PeaceConferenceResult? ConductCoalitionSettlement(
        PoliticalWorld world,
        int leadVictorId,
        int defeatedId,
        IReadOnlyList<int> victorSideIds,
        int totalCitiesDemanded,
        float totalReparationsPerYear,
        int reparationsYears,
        int truceUntilYear,
        List<ReparationsOrder> reparationsOut,
        List<CityDemandOrder> cityDemandsOut)
    {
        if (world == null || victorSideIds == null) return null;

        // Собираем всех живых участников победившей коалиции (включая главного победителя)
        var allies = new List<int>();
        if (leadVictorId > 0 && world.GetKingdom(leadVictorId) != null)
        {
            allies.Add(leadVictorId);
        }
        for (int i = 0; i < victorSideIds.Count; i++)
        {
            int id = victorSideIds[i];
            if (id > 0 && id != defeatedId && !allies.Contains(id) && world.GetKingdom(id) != null)
            {
                allies.Add(id);
            }
        }

        if (allies.Count <= 1)
        {
            return null;
        }

        // Вычисляем вклад каждого союзника в победу
        var rawScores = new Dictionary<int, float>();
        float sumScores = 0f;
        foreach (int allyId in allies)
        {
            KingdomPolitics? k = world.GetKingdom(allyId);
            float score = 20f;
            if (allyId == leadVictorId) score += 35f;
            if (k != null)
            {
                score += Math.Min(60f, k.Population * 0.25f);
                score += k.WarFatigue * 0.5f;
            }
            if (world.DiplomaticMemories.TryFindIrredentistTarget(allyId, world.Year, out int holderId, out _) && holderId == defeatedId)
            {
                score += 40f;
            }
            rawScores[allyId] = score;
            sumScores += score;
        }

        if (sumScores <= 0f) sumScores = 1f;

        var result = new PeaceConferenceResult
        {
            Year = world.Year,
            LeadVictorId = leadVictorId,
            DefeatedId = defeatedId,
            TotalCitiesDemanded = totalCitiesDemanded,
            TotalReparationsPerYear = totalReparationsPerYear,
            ReparationsYears = reparationsYears
        };

        foreach (int allyId in allies)
        {
            float weight = rawScores[allyId] / sumScores;
            result.Shares.Add(new AllySpoilsShare
            {
                KingdomId = allyId,
                KingdomName = world.KingdomNameOf(allyId),
                ContributionWeight = weight
            });
            // Перемирие со всеми участниками победившей коалиции
            world.TrucesBook.Set(allyId, defeatedId, truceUntilYear);
        }

        // Распределение городов по вкладу (метод наибольших остатков д'Ондта / Хэра)
        if (totalCitiesDemanded > 0)
        {
            int remainingCities = totalCitiesDemanded;
            var exactQuotas = result.Shares
                .Select(sh => new { Share = sh, Quota = sh.ContributionWeight * totalCitiesDemanded })
                .ToList();

            foreach (var item in exactQuotas)
            {
                int whole = Math.Min(remainingCities, (int)Math.Floor(item.Quota));
                item.Share.CitiesAwarded = whole;
                remainingCities -= whole;
            }

            var byRemainder = exactQuotas
                .OrderByDescending(x => x.Quota - Math.Floor(x.Quota))
                .ThenByDescending(x => x.Share.ContributionWeight)
                .ToList();

            for (int i = 0; i < byRemainder.Count && remainingCities > 0; i++)
            {
                byRemainder[i].Share.CitiesAwarded++;
                remainingCities--;
            }

            foreach (AllySpoilsShare sh in result.Shares)
            {
                if (sh.CitiesAwarded > 0)
                {
                    cityDemandsOut.Add(new CityDemandOrder
                    {
                        FromId = defeatedId,
                        ToId = sh.KingdomId,
                        Count = sh.CitiesAwarded
                    });
                }
            }
        }

        // Распределение репараций пропорционально вкладу (обделённые землёй получают повышенную долю репараций, но всё равно могут затаить обиду)
        if (totalReparationsPerYear > 0f && reparationsYears > 0)
        {
            foreach (AllySpoilsShare sh in result.Shares)
            {
                float rep = (float)Math.Round(totalReparationsPerYear * sh.ContributionWeight, 1);
                if (rep > 0f)
                {
                    sh.ReparationsPerYear = rep;
                    reparationsOut.Add(new ReparationsOrder
                    {
                        PayerId = defeatedId,
                        PayeeId = sh.KingdomId,
                        AmountPerYear = rep,
                        YearsLeft = reparationsYears
                    });
                }
            }
        }

        // Проверяем: не оказался ли крупный союзник (вклад >= 25%) обделён при разделе земель
        if (totalCitiesDemanded > 0)
        {
            foreach (AllySpoilsShare sh in result.Shares)
            {
                if (sh.KingdomId != leadVictorId && sh.ContributionWeight >= 0.25f && sh.CitiesAwarded == 0)
                {
                    result.ShortchangedAllyIds.Add(sh.KingdomId);
                    world.DiplomaticMemories.Record(
                        sh.KingdomId,
                        leadVictorId,
                        DiplomaticMemoryKind.SpoilsShortchanged,
                        world.Year,
                        45,
                        -30,
                        world.KingdomNameOf(defeatedId));
                    world.ChangeRulerRelation(sh.KingdomId, leadVictorId, -25);
                    world.AddGrievance(sh.KingdomId, leadVictorId, GrievanceReason.BrokenUnion, 25f);

                    world.Bus.Publish(new PolEvent(PolEventType.CoalitionSpoilsDispute, "RP30_EV_SPOILS_DISPUTE")
                    {
                        Year = world.Year,
                        KingdomId = sh.KingdomId,
                        Severity = Severity.Minor
                    }.With("ally", sh.KingdomName)
                        .With("leader", world.KingdomNameOf(leadVictorId))
                        .With("defeated", world.KingdomNameOf(defeatedId))
                        .With("share", ((int)Math.Round(sh.ContributionWeight * 100f)).ToString()));
                }
            }
        }

        string divisionSummary = string.Join(", ", result.Shares.Select(sh =>
            sh.KingdomName + " (" + (int)Math.Round(sh.ContributionWeight * 100f) + "%"
            + (sh.CitiesAwarded > 0 ? ", гор.: " + sh.CitiesAwarded : string.Empty)
            + (sh.ReparationsPerYear > 0f ? ", реп.: " + sh.ReparationsPerYear.ToString("0.#") : string.Empty)
            + ")"));

        world.Bus.Publish(new PolEvent(PolEventType.PeaceConferenceConcluded, "RP30_EV_PEACE_CONFERENCE")
        {
            Year = world.Year,
            KingdomId = leadVictorId,
            Severity = Severity.Major
        }.With("victor", world.KingdomNameOf(leadVictorId))
            .With("defeated", world.KingdomNameOf(defeatedId))
            .With("allies", result.Shares.Count.ToString())
            .With("division", divisionSummary));

        return result;
    }
}
