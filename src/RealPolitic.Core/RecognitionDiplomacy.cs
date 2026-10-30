using System;
using System.Collections.Generic;
using System.Linq;

namespace RealPolitic.Core;

public static class RecognitionDiplomacyEngine
{
    private static readonly Dictionary<long, int> _lastLobbyYearByPair = new Dictionary<long, int>();
    private static readonly Dictionary<int, int> _lastProtestYearByTarget = new Dictionary<int, int>();
    private static readonly HashSet<string> _processedVotes = new HashSet<string>(StringComparer.Ordinal);

    private static long PairKey(int a, int b) => ((long)a << 32) | (uint)b;

    public static void ClearCache()
    {
        _lastLobbyYearByPair.Clear();
        _lastProtestYearByTarget.Clear();
        _processedVotes.Clear();
    }

    /// <summary>
    /// 0.31: включает международное признание и Совет великих держав на любой карте
    /// (включая старые сохранения вроде save21), регистрируя уже существующие на карте
    /// государства как признанные страны-основатели и создавая им выборные главы конституции.
    /// Все новые государства, возникающие после этого момента (мятежи, сецессии, освобождение),
    /// проходят полноценную процедуру международного признания.
    /// </summary>
    public static bool EnsureUniversalRecognitionBaseline(PoliticalWorld world, int year, string worldKeyPrefix = "universal", string stableWorldKey = null)
    {
        if (world == null) return false;
        if (!world.Params.EnableUniversalRecognition) return false;

        bool initializedNow = false;
        if (!world.Governance.Enabled)
        {
            // A legacy save may be loaded before it has a political checkpoint. Use its
            // stable save-slot identity when available; year-based keys split one
            // world into a new Chronicle folder each time an older save is resumed.
            string key = !string.IsNullOrWhiteSpace(stableWorldKey)
                ? stableWorldKey
                : (string.IsNullOrWhiteSpace(worldKeyPrefix) ? "universal" : worldKeyPrefix) + "-" + Math.Max(1, year);
            world.BeginNewWorldGovernance(key);
            initializedNow = true;
        }

        // Все державы, существующие в момент базовой инициализации старого мира, получают выборную главу и статус признанных стран-основателей
        foreach (KingdomPolitics k in world.Kingdoms)
        {
            if (k.Constitution == null)
            {
                k.Constitution = ConstitutionDefaults.For(k.Ideology, EraRules.OfYear(year, world.Params));
            }
            if (k.Constitution.Electoral == null)
            {
                k.Constitution.Electoral = ElectoralDefaults.FromConstitution(k.Constitution);
            }
            if (initializedNow && !world.Governance.Recognition.ContainsCountry(k.KingdomId))
            {
                Union? union = world.UnionOf(k.KingdomId);
                bool sovereign = union == null || union.Type != UnionType.Vassal || union.LeaderId == k.KingdomId;
                world.Governance.Recognition.RegisterFoundingCountry(k.KingdomId, Math.Max(0, k.FoundedYear), sovereign);
            }
        }

        if (initializedNow && year >= 50)
        {
            var snapshots = new List<NationalPowerSnapshot>();
            foreach (KingdomPolitics k in world.Kingdoms)
            {
                var snap = new NationalPowerSnapshot
                {
                    CountryId = k.KingdomId,
                    Population = Math.Max(1, k.Population),
                    Warriors = Math.Max(1, k.Population / 4)
                };
                if (k.Economy != null && k.Economy.LastIncome > 0f)
                {
                    snap.RecentAnnualIncome.Add(k.Economy.LastIncome);
                }
                snapshots.Add(snap);
            }
            if (world.Governance.Recognition.ReviewGreatPowers(year, snapshots))
            {
                var names = world.Governance.Recognition.GreatPowers
                    .Select(id => world.KingdomNameOf(id))
                    .ToList();
                world.Bus.Publish(new PolEvent(PolEventType.GreatPowerCompositionChanged, "RP_GOV26_EV_COUNCIL")
                {
                    Year = year,
                    Severity = Severity.Major
                }.With("members", names.Count == 0 ? "—" : string.Join(", ", names)));
            }
        }

        return initializedNow;
    }

    /// <summary>
    /// 0.31: ежегодный цикл дипломатической борьбы за международное признание:
    /// посольства непризнанных стран, протесты метрополий и реакция на голоса великих держав.
    /// </summary>
    public static void Tick(PoliticalWorld world, int year)
    {
        if (world == null || !world.Governance.Enabled) return;

        InternationalRecognition recognition = world.Governance.Recognition;
        IReadOnlyList<int> greatPowers = recognition.GreatPowers;
        if (greatPowers.Count < 3) return;

        RecognitionSnapshot snap = recognition.Capture();
        foreach (CountryRecognitionState cState in snap.Countries)
        {
            if (!cState.Alive || cState.Recognized) continue;
            if (recognition.PendingVote(cState.CountryId) == null && cState.LastDecisionGeneration == snap.CompositionGeneration)
                continue;

            int targetId = cState.CountryId;
            KingdomPolitics? target = world.GetKingdom(targetId);
            if (target == null) continue;

            int parentId = 0;
            bool unlawfulSecession = false;
            if (world.Governance.CountryBirths.TryGetValue(targetId, out CivicCountryBirth? birth) && birth != null)
            {
                parentId = birth.ParentId;
                unlawfulSecession = !birth.Lawful;
            }

            // 1. Посольство непризнанной державы к одной из великих держав
            foreach (int gpId in greatPowers)
            {
                if (gpId == targetId || gpId == parentId) continue;
                long pKey = PairKey(targetId, gpId);
                if (_lastLobbyYearByPair.TryGetValue(pKey, out int lastLobby) && year - lastLobby < 4)
                    continue;

                KingdomPolitics? gp = world.GetKingdom(gpId);
                if (gp == null) continue;

                _lastLobbyYearByPair[pKey] = year;
                int bonus = Ideologies.Affinity(target.Ideology, gp.Ideology) >= 0f ? 22 : 12;
                world.ChangeRulerRelation(targetId, gpId, bonus);
                world.Bus.Publish(new PolEvent(PolEventType.RecognitionLobbyMission, "RP31_EV_RECOGNITION_LOBBY")
                {
                    Year = year,
                    KingdomId = targetId,
                    Severity = Severity.Minor
                }.With("kingdom", world.KingdomNameOf(targetId))
                    .With("power", world.KingdomNameOf(gpId)));
                break; // Одно посольство в год от непризнанной державы
            }

            // 2. Дипломатический протест метрополии против признания мятежного края
            if (unlawfulSecession && parentId > 0 && world.GetKingdom(parentId) != null)
            {
                if (!_lastProtestYearByTarget.TryGetValue(targetId, out int lastProtest) || year - lastProtest >= 5)
                {
                    _lastProtestYearByTarget[targetId] = year;
                    foreach (int gpId in greatPowers)
                    {
                        if (gpId == parentId || gpId == targetId) continue;
                        if (world.GetRulerRelation(gpId, parentId) >= 10)
                        {
                            world.ChangeRulerRelation(gpId, targetId, -18);
                        }
                    }
                    world.Bus.Publish(new PolEvent(PolEventType.MetropolisRecognitionProtest, "RP31_EV_METROPOLIS_PROTEST")
                    {
                        Year = year,
                        KingdomId = parentId,
                        Severity = Severity.Minor
                    }.With("metropolis", world.KingdomNameOf(parentId))
                        .With("breakaway", world.KingdomNameOf(targetId)));
                }
            }

            // 3. Отслеживаем поданные голоса великих держав в текущем раунде признания
            CivicBallot? pending = recognition.PendingVote(targetId);
            if (pending != null)
            {
                foreach (KeyValuePair<int, int> vote in pending.Votes)
                {
                    int gpId = vote.Key;
                    int choice = vote.Value;
                    string voteKey = pending.Id + ":" + gpId;
                    if (!_processedVotes.Add(voteKey)) continue;

                    if (choice == CivicBallot.Yes)
                    {
                        world.DiplomaticMemories.Record(targetId, gpId, DiplomaticMemoryKind.RestoredOurState, year, 50, 25, "Recognition");
                        world.ChangeRulerRelation(targetId, gpId, 20);
                        if (unlawfulSecession && parentId > 0 && world.GetKingdom(parentId) != null && gpId != parentId)
                        {
                            world.ChangeRulerRelation(parentId, gpId, -20);
                            world.AddGrievance(parentId, gpId, GrievanceReason.BrokenUnion, 20f);
                        }
                    }
                    else if (choice == CivicBallot.No)
                    {
                        world.ChangeRulerRelation(targetId, gpId, -15);
                        if (parentId > 0 && world.GetKingdom(parentId) != null && gpId != parentId)
                        {
                            world.ChangeRulerRelation(parentId, gpId, 12);
                        }
                    }
                }
            }
        }
    }
}
