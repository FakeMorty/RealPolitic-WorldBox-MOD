using System;
using System.Collections.Generic;
using System.Linq;

namespace RealPolitic.Core;

public enum CivicDemandType
{
    None = 0,
    EndExhaustingWar = 1,
    LowerTaxesAndAusterity = 2,
    AbolishSecretPolice = 3,
    IntroduceElections = 4,
    GrantRegionalAutonomy = 5
}

public enum CivicMovementStage
{
    GatheringSupport = 0,
    PetitionSubmitted = 1,
    UltimatumIssued = 2
}

public sealed class CivicMovement
{
    public CivicDemandType Demand { get; set; }
    public CivicMovementStage Stage { get; set; }
    public int StartedYear { get; set; }
    public float SupportShare { get; set; }
    public string LeaderName { get; set; } = string.Empty;
}

public sealed class PoliticalParty
{
    public IdeologyType Ideology { get; set; }
    /// <summary>Название, написанное словами: «Народная партия «Ракрог»» (0.48.3).
    /// Пусто — значит показываем старую общую строку по NameKey.</summary>
    public string Name { get; set; } = string.Empty;
    public string NameKey { get; set; } = string.Empty;
    public int SeatsPercent { get; set; }
    public bool IsRuling { get; set; }
}

public static class CivicSocietyEngine
{
    public static bool HasParliamentaryParties(KingdomPolitics state)
    {
        if (state == null) return false;
        if (state.Ideology == IdeologyType.Democracy
            || state.Ideology == IdeologyType.Liberalism
            || state.Ideology == IdeologyType.MerchantRepublic
            || state.Ideology == IdeologyType.Egalitarianism
            || state.Ideology == IdeologyType.Technocracy)
        {
            return true;
        }
        return state.Constitution != null
            && (state.Constitution.HoldsElectionsForHead
                || state.Constitution.StateForm == HeadOfState.ParliamentaryMonarchy
                || state.Constitution.StateForm == HeadOfState.Assembly);
    }

    public static void UpdateParties(KingdomPolitics state, PoliticalWorld world, int year)
    {
        if (state == null) return;
        if (!HasParliamentaryParties(state))
        {
            state.Parties.Clear();
            return;
        }

        // 0.48.3: название партии собираем со страной: «Народная партия «Ракрог»».
        // Коренное имя берём без титула («Ракрог», а не «Барство Ракрог»).
        string stateRoot = world != null ? StateNaming.Root(world.KingdomNameOf(state.KingdomId)) : string.Empty;
        int totalPop = state.Population;
        if (totalPop <= 0)
        {
            if (state.Parties.Count == 0)
            {
                state.Parties.Add(new PoliticalParty
                {
                    Ideology = state.Ideology,
                    Name = PartyNames.For(state.Ideology, stateRoot, state.KingdomId),
                    NameKey = PartyNameKey(state.Ideology),
                    SeatsPercent = 100,
                    IsRuling = true
                });
            }
            return;
        }

        var top = state.Census
            .Where(kv => kv.Value > 0)
            .OrderByDescending(kv => kv.Value)
            .ThenBy(kv => kv.Key)
            .Take(4)
            .ToList();

        if (top.Count == 0) return;

        int sumTop = top.Sum(kv => kv.Value);
        if (sumTop <= 0) return;

        IdeologyType previousRuling = state.Parties.FirstOrDefault(p => p.IsRuling)?.Ideology ?? state.Ideology;
        state.Parties.Clear();

        int assigned = 0;
        for (int i = 0; i < top.Count; i++)
        {
            int seats = i == top.Count - 1
                ? Math.Max(0, 100 - assigned)
                : Math.Max(1, (int)Math.Round(top[i].Value * 100.0 / sumTop));
            assigned += seats;
            state.Parties.Add(new PoliticalParty
            {
                Ideology = top[i].Key,
                Name = PartyNames.For(top[i].Key, stateRoot, state.KingdomId),
                NameKey = PartyNameKey(top[i].Key),
                SeatsPercent = seats,
                IsRuling = i == 0
            });
        }

        PoliticalParty ruling = state.Parties[0];
        if (ruling.Ideology != previousRuling && ruling.SeatsPercent >= 45 && world != null && world.BaselineRecorded)
        {
            world.Bus.Publish(new PolEvent(PolEventType.ParliamentaryShift, "RP28_EV_PARLIAMENT_SHIFT")
            {
                Year = year,
                KingdomId = state.KingdomId,
                Severity = Severity.Minor
            }.With("kingdom", world.KingdomNameOf(state.KingdomId))
                .With("party", string.IsNullOrEmpty(ruling.Name) ? ruling.NameKey : ruling.Name)
                .With("seats", ruling.SeatsPercent.ToString()));
        }
    }

    public const int YoungStateGraceYears = 50;

    public static void TickMovements(KingdomPolitics state, PoliticalWorld world, int year)
    {
        if (state == null || world == null) return;
        UpdateParties(state, world, year);

        int graceYears = world.Params != null ? world.Params.YoungStateGraceYears : YoungStateGraceYears;
        if (state.IsYoungState(year, graceYears))
        {
            state.ActiveMovement = null;
            return;
        }

        if (year - state.LastCivicMovementYear < 5 && state.ActiveMovement == null)
        {
            return;
        }

        CivicMovement? mov = state.ActiveMovement;
        float effectiveSupport = (state.Population == 0 && state.Support <= 0f) ? 65f : state.Support;
        float variance = (world.BaselineRecorded && state.KingdomId > 0)
            ? (((state.KingdomId * 31 + year * 17) % 15) - 7) * 0.004f
            : 0f;
        if (mov == null)
        {
            CivicDemandType demand = DetectPressingDemand(state);
            if (demand == CivicDemandType.None) return;

            float initialShare = Math.Max(0.10f, Math.Min(0.45f, 0.16f + state.AvgDissent * 0.35f + (100f - effectiveSupport) * 0.002f + variance));
            mov = new CivicMovement
            {
                Demand = demand,
                Stage = CivicMovementStage.GatheringSupport,
                StartedYear = year,
                SupportShare = initialShare,
                LeaderName = state.Ruler?.Name ?? string.Empty
            };
            state.ActiveMovement = mov;
            if (initialShare >= 0.25f)
            {
                mov.Stage = CivicMovementStage.PetitionSubmitted;
                world.Bus.Publish(new PolEvent(PolEventType.CivicPetitionSubmitted, "RP28_EV_PETITION")
                {
                    Year = year,
                    KingdomId = state.KingdomId,
                    Severity = Severity.Minor
                }.With("kingdom", world.KingdomNameOf(state.KingdomId))
                    .With("demand", DemandLocKey(mov.Demand))
                    .With("support", ((int)Math.Round(mov.SupportShare * 100f)).ToString()));
            }
            return;
        }

        // Проверяем, не отпала ли причина движения сама собой
        if (!IsDemandStillValid(state, mov.Demand))
        {
            state.ActiveMovement = null;
            state.LastCivicMovementYear = year;
            return;
        }

        // Рост поддержки движения
        float growth = Math.Max(0.02f, 0.06f + state.AvgDissent * 0.12f + (state.WarFatigue > 50f ? 0.04f : 0f) + variance
            + PromiseMemory.MovementGrowthBonus(state));
        mov.SupportShare = Math.Min(0.95f, mov.SupportShare + growth);

        if (mov.Stage == CivicMovementStage.GatheringSupport && mov.SupportShare >= 0.25f)
        {
            mov.Stage = CivicMovementStage.PetitionSubmitted;
            world.Bus.Publish(new PolEvent(PolEventType.CivicPetitionSubmitted, "RP28_EV_PETITION")
            {
                Year = year,
                KingdomId = state.KingdomId,
                Severity = Severity.Minor
            }.With("kingdom", world.KingdomNameOf(state.KingdomId))
                .With("demand", DemandLocKey(mov.Demand))
                .With("support", ((int)Math.Round(mov.SupportShare * 100f)).ToString()));
            return;
        }

        if (mov.SupportShare >= 0.40f || year - mov.StartedYear >= 4)
        {
            mov.Stage = CivicMovementStage.UltimatumIssued;
            ResolveUltimatum(state, mov, world, year);
            state.ActiveMovement = null;
            state.LastCivicMovementYear = year;
        }
    }

    public static bool ShouldConcede(KingdomPolitics state, CivicMovement mov)
    {
        float concedeScore = mov.SupportShare * 100f;
        if (state.Ideology == IdeologyType.Democracy || state.Ideology == IdeologyType.Liberalism || state.Ideology == IdeologyType.Egalitarianism)
            concedeScore += 22f;
        if (state.Ideology == IdeologyType.Militarism || state.Ideology == IdeologyType.Ethnonationalism || state.Ideology == IdeologyType.Theocracy)
            concedeScore -= 18f;
        if (state.Personality != null)
        {
            if (state.Personality.Trait == RulerTrait.Just || state.Personality.Trait == RulerTrait.Scholar || state.Personality.Trait == RulerTrait.Diplomat)
                concedeScore += 18f;
            if (state.Personality.Trait == RulerTrait.Cruel || state.Personality.Trait == RulerTrait.Conqueror)
                concedeScore -= 20f;
        }
        if (state.Legitimacy < 45f)
            concedeScore += 15f;
        return concedeScore >= 50f;
    }

    private static void ResolveUltimatum(KingdomPolitics state, CivicMovement mov, PoliticalWorld world, int year)
    {
        bool concede = ShouldConcede(state, mov);
        if (concede)
        {
            ApplyConcession(state, mov.Demand);
            state.Support = KingdomSimulation.Clamp(state.Support + 14f, 0f, 100f);
            state.Legitimacy = KingdomSimulation.Clamp(state.Legitimacy + 6f, 0f, 100f);
            state.AvgDissent = KingdomSimulation.Clamp(state.AvgDissent - 0.18f, 0f, 1f);
            state.Mechanics.ThreatLevel = KingdomSimulation.Clamp(state.Mechanics.ThreatLevel - 12f, 0f, 100f);
        }
        else
        {
            state.Support = KingdomSimulation.Clamp(state.Support - 12f, 0f, 100f);
            state.AvgDissent = KingdomSimulation.Clamp(state.AvgDissent + 0.16f, 0f, 1f);
            state.Mechanics.ThreatLevel = KingdomSimulation.Clamp(state.Mechanics.ThreatLevel + 15f, 0f, 100f);
        }

        world.Bus.Publish(new PolEvent(PolEventType.CivicUltimatumResolved, "RP28_EV_ULTIMATUM")
        {
            Year = year,
            KingdomId = state.KingdomId,
            Severity = concede ? Severity.Minor : Severity.Major
        }.With("kingdom", world.KingdomNameOf(state.KingdomId))
            .With("demand", DemandLocKey(mov.Demand))
            .With("outcome", concede ? "RP28_MOVEMENT_CONCEDED" : "RP28_MOVEMENT_SUPPRESSED")
            .With("support", ((int)Math.Round(mov.SupportShare * 100f)).ToString()));
    }

    private static void ApplyConcession(KingdomPolitics state, CivicDemandType demand)
    {
        switch (demand)
        {
            case CivicDemandType.EndExhaustingWar:
                state.WarFatigue = Math.Max(0f, state.WarFatigue - 25f);
                if (state.Constitution != null && state.Constitution.StateForm == HeadOfState.Hereditary)
                {
                    state.Constitution.StateForm = HeadOfState.ParliamentaryMonarchy;
                }
                break;
            case CivicDemandType.LowerTaxesAndAusterity:
                if (state.Economy != null)
                {
                    state.Economy.Austerity = false;
                    state.Economy.TaxRate = Math.Min(1.0f, state.Economy.TaxRate);
                }
                if (state.Constitution != null)
                {
                    state.Constitution.Taxes = TaxLaw.Moderate;
                }
                break;
            case CivicDemandType.AbolishSecretPolice:
                if (state.Constitution != null)
                {
                    state.Constitution.Watch = WatchLaw.Patrol;
                }
                break;
            case CivicDemandType.IntroduceElections:
                if (state.Constitution != null)
                {
                    state.Constitution.StateForm = HeadOfState.Elected;
                    state.Constitution.Franchise = Franchise.Universal;
                }
                break;
            case CivicDemandType.GrantRegionalAutonomy:
                if (state.Constitution?.Electoral != null)
                {
                    state.Constitution.Electoral.RegionalExit = RegionalExitLaw.RegionalRight;
                }
                break;
        }
    }

    private static CivicDemandType DetectPressingDemand(KingdomPolitics state)
    {
        if (state.InWar && state.WarFatigue >= 55f)
            return CivicDemandType.EndExhaustingWar;
        if (state.Economy != null && (state.Economy.Austerity || state.Economy.TaxRate > 1.25f) && state.AvgDissent >= 0.22f)
            return CivicDemandType.LowerTaxesAndAusterity;
        if (state.Constitution != null && state.Constitution.Watch == WatchLaw.SecretPolice && state.AvgDissent >= 0.25f)
            return CivicDemandType.AbolishSecretPolice;
        if (state.Constitution != null && !state.Constitution.HoldsElectionsForHead && DemocraticShare(state) >= 0.30f)
            return CivicDemandType.IntroduceElections;
        if (state.Constitution?.Electoral != null && state.Constitution.Electoral.RegionalExit == RegionalExitLaw.Forbidden && state.AvgDissent >= 0.30f)
            return CivicDemandType.GrantRegionalAutonomy;
        return CivicDemandType.None;
    }

    private static bool IsDemandStillValid(KingdomPolitics state, CivicDemandType demand)
    {
        return demand switch
        {
            CivicDemandType.EndExhaustingWar => state.InWar && state.WarFatigue >= 35f,
            CivicDemandType.LowerTaxesAndAusterity => state.Economy != null && (state.Economy.Austerity || state.Economy.TaxRate > 1.15f),
            CivicDemandType.AbolishSecretPolice => state.Constitution != null && state.Constitution.Watch == WatchLaw.SecretPolice,
            CivicDemandType.IntroduceElections => state.Constitution != null && !state.Constitution.HoldsElectionsForHead,
            CivicDemandType.GrantRegionalAutonomy => state.Constitution?.Electoral != null && state.Constitution.Electoral.RegionalExit == RegionalExitLaw.Forbidden,
            _ => false
        };
    }

    private static float DemocraticShare(KingdomPolitics state)
    {
        int pop = state.Population;
        if (pop <= 0) return 0f;
        int dem = state.CensusOf(IdeologyType.Democracy)
            + state.CensusOf(IdeologyType.Liberalism)
            + state.CensusOf(IdeologyType.Egalitarianism);
        return (float)dem / pop;
    }

    public static string DemandLocKey(CivicDemandType demand) => demand switch
    {
        CivicDemandType.EndExhaustingWar => "RP28_DEMAND_PEACE",
        CivicDemandType.LowerTaxesAndAusterity => "RP28_DEMAND_TAXES",
        CivicDemandType.AbolishSecretPolice => "RP28_DEMAND_LIBERTY",
        CivicDemandType.IntroduceElections => "RP28_DEMAND_ELECTIONS",
        CivicDemandType.GrantRegionalAutonomy => "RP28_DEMAND_AUTONOMY",
        _ => "RP28_DEMAND_NONE"
    };

    public static string PartyNameKey(IdeologyType ideology) => ideology switch
    {
        IdeologyType.Democracy => "RP28_PARTY_DEMOCRATIC",
        IdeologyType.Liberalism => "RP28_PARTY_LIBERAL",
        IdeologyType.Egalitarianism => "RP28_PARTY_SOCIAL",
        IdeologyType.Communism => "RP28_PARTY_VANGUARD",
        IdeologyType.Monarchy => "RP28_PARTY_ROYALIST",
        IdeologyType.Aristocracy => "RP28_PARTY_NOBLE",
        IdeologyType.MerchantRepublic => "RP28_PARTY_GUILD",
        IdeologyType.Technocracy => "RP28_PARTY_TECHNOCRAT",
        IdeologyType.Militarism => "RP28_PARTY_MILITARY",
        IdeologyType.Theocracy => "RP28_PARTY_CLERICAL",
        IdeologyType.Ethnonationalism => "RP28_PARTY_NATIONAL",
        IdeologyType.Khaganate => "RP28_PARTY_STEPPE",
        IdeologyType.Empire => "RP28_PARTY_IMPERIAL",
        IdeologyType.FreeTerritory => "RP28_PARTY_FREETERRITORY",
        _ => "RP28_PARTY_TRADITIONAL"
    };
}
