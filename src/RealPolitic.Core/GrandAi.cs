using System;
using System.Collections.Generic;
using System.Linq;

namespace RealPolitic.Core;

/// <summary>
/// Стратегические векторы Большого ИИ (Grand AI) державы.
/// Каждый вектор оценивается по шкале 0..100 (100 — максимальная срочность/потребность внимания).
/// </summary>
public sealed class GrandAiVectors
{
    public float Survival { get; init; }
    public float Stability { get; init; }
    public float Economy { get; init; }
    public float Diplomacy { get; init; }
    public float Ideology { get; init; }

    public string DominantVector
    {
        get
        {
            float max = Survival;
            string name = "Survival";
            if (Stability > max) { max = Stability; name = "Stability"; }
            if (Economy > max) { max = Economy; name = "Economy"; }
            if (Diplomacy > max) { max = Diplomacy; name = "Diplomacy"; }
            if (Ideology > max) { name = "Ideology"; }
            return name;
        }
    }
}

/// <summary>
/// Кандидат стратегического решения Большого ИИ с оценкой, вектором и статусом конституционного фильтра.
/// </summary>
public sealed class GrandAiCandidate
{
    public KingdomActionType Action { get; init; }
    public float RawScore { get; set; }
    public float FinalScore => BlockedByGate ? 0f : RawScore;
    public string Vector { get; init; } = string.Empty;
    public string Reason { get; init; } = string.Empty;
    public bool BlockedByGate { get; init; }
    public string GateReason { get; init; } = string.Empty;
}

/// <summary>
/// Полный объяснимый отчёт Большого ИИ (Grand AI) за стратегический такт державы.
/// </summary>
public sealed class GrandAiReport
{
    public int KingdomId { get; init; }
    public int Year { get; init; }
    public GrandAiVectors Vectors { get; init; } = new GrandAiVectors();
    public KingdomActionPlan SelectedPlan { get; init; } = new KingdomActionPlan(KingdomActionType.None, 0f, string.Empty);
    public IReadOnlyList<GrandAiCandidate> RankedCandidates { get; init; } = Array.Empty<GrandAiCandidate>();
    public IReadOnlyList<GrandAiCandidate> BlockedCandidates { get; init; } = Array.Empty<GrandAiCandidate>();
}

/// <summary>
/// Детерминированный многовекторный планировщик «Большой ИИ» (Grand AI).
/// Оценивает положение государства по 5 стратегическим векторам (Выживание, Устойчивость,
/// Экономика, Дипломатия, Идеология), проверяет конституционные статьи (ст. XII–XV и главу о выборах)
/// и международное признание (0.26) до выбора действия и формирует объяснимый отчёт.
/// </summary>
public static class GrandAi
{
    public static GrandAiVectors EvaluateVectors(KingdomStrategySnapshot s)
    {
        if (s == null) throw new ArgumentNullException(nameof(s));

        float survival = 0f;
        if (s.Starving) survival = Math.Max(survival, 100f);
        else if (s.FoodPerPerson < 0.4f) survival = Math.Max(survival, 65f);
        if (s.CivilWarActive) survival = Math.Max(survival, 92f + Clamp(s.RebelShare, 0f, 1f) * 8f);
        if (s.AtWar)
        {
            float warPressure = s.ArmyRatio < 0.55f ? 88f : 60f + Clamp(1f - s.ArmyRatio, 0f, 1f) * 25f;
            survival = Math.Max(survival, warPressure);
        }

        float stability = 0f;
        if (s.Legitimacy < 60f) stability += (60f - Clamp(s.Legitimacy, 0f, 60f)) * 1.35f;
        if (s.ThreatLevel > 25f) stability = Math.Max(stability, s.ThreatLevel * 0.9f);
        if (s.Support < 50f) stability = Math.Max(stability, (50f - Clamp(s.Support, 0f, 50f)) * 1.1f);
        if (s.WarFatigue >= 40f) stability = Math.Max(stability, s.WarFatigue * 0.95f);
        stability = Clamp(stability, 0f, 100f);

        float economy = 0f;
        if (s.EconomyEnabled)
        {
            if (s.InEconomicCrisis) economy = Math.Max(economy, 85f);
            if (s.DeficitYears > 0) economy = Math.Max(economy, Math.Min(90f, 55f + s.DeficitYears * 10f));
            if (s.TradeRoutes < s.MaxTradeRoutes && !s.AtWar) economy = Math.Max(economy, 42f);
        }
        if (s.Gold < 100f) economy = Math.Max(economy, 30f + (100f - Clamp(s.Gold, 0f, 100f)) * 0.25f);
        if (s.Stone < 80f) economy = Math.Max(economy, 20f + (80f - Clamp(s.Stone, 0f, 80f)) * 0.2f);
        economy = Clamp(economy, 0f, 100f);

        float diplomacy = 0f;
        if (s.AtWar && (s.WarFatigue >= 55f || s.Legitimacy < 45f))
            diplomacy = Math.Max(diplomacy, 65f + s.WarFatigue * 0.3f);
        if (!s.AtWar && s.HasAllianceOffer) diplomacy = Math.Max(diplomacy, 58f);
        if (!s.AtWar && s.TreatiesEnabled && s.TreatyCandidateId != 0) diplomacy = Math.Max(diplomacy, Clamp(s.TreatyScore, 0f, 95f));
        if (!s.AtWar && s.DynastiesEnabled && s.MarriageCandidateId != 0) diplomacy = Math.Max(diplomacy, Clamp(s.MarriageScore, 0f, 95f));
        if (!s.AtWar && s.CasusBelliEnabled && s.WarTargetId != 0) diplomacy = Math.Max(diplomacy, Clamp(s.WarScore, 0f, 90f));
        if (s.VassalOfId != 0 && s.VassalApproval < s.VassalRebellionApproval) diplomacy = Math.Max(diplomacy, 75f);
        diplomacy = Clamp(diplomacy, 0f, 100f);

        float ideology = 0f;
        if (s.PowerReady && !s.PowerActive) ideology = Math.Max(ideology, 55f);
        if (s.AvgDissent > 0.2f) ideology = Math.Max(ideology, Clamp(s.AvgDissent * 100f, 0f, 90f));
        ideology = Clamp(ideology, 0f, 100f);

        return new GrandAiVectors
        {
            Survival = Clamp(survival, 0f, 100f),
            Stability = stability,
            Economy = economy,
            Diplomacy = diplomacy,
            Ideology = ideology
        };
    }

    public static GrandAiReport Evaluate(
        KingdomStrategySnapshot s,
        Constitution? constitution = null,
        InternationalRecognition? recognition = null,
        StrategicCampaignPlan? activeCampaign = null)
    {
        if (s == null) throw new ArgumentNullException(nameof(s));

        GrandAiVectors vectors = EvaluateVectors(s);
        if (!s.CooldownReady)
        {
            return new GrandAiReport
            {
                KingdomId = s.KingdomId,
                Year = s.Year,
                Vectors = vectors,
                SelectedPlan = new KingdomActionPlan(KingdomActionType.None, 0f, CoreText.L("RP_WHY_COOLDOWN", "Ожидание стратегической перезарядки"))
            };
        }

        var all = new List<GrandAiCandidate>();
        float personalityWar = RulerTraits.WarBias(s.Trait) + (s.Ambition - 50f) * 0.2f;
        float personalityDip = RulerTraits.DiplomacyBias(s.Trait) + (50f - s.Ambition) * 0.1f;

        AddCandidate(all, s, constitution, recognition, KingdomActionType.EmergencyFood,
            s.Starving ? 100f : (s.FoodPerPerson < 0.4f ? 60f : 0f),
            "Survival", CoreText.L("RP_WHY_FOOD", "Голод требует пополнения запасов еды"));

        AddCandidate(all, s, constitution, recognition, KingdomActionType.SuppressRebellion,
            s.CivilWarActive ? 92f + s.RebelShare * 6f : 0f,
            "Survival", CoreText.L("RP_WHY_REBELLION", "Мятеж требует немедленного подавления"));

        AddCandidate(all, s, constitution, recognition, KingdomActionType.Defend,
            s.AtWar && s.ArmyRatio < 0.55f ? 88f : 0f,
            "Survival", CoreText.L("RP_WHY_DEFEND", "Война и недостаток армии требуют обороны"));

        float restoreLegitimacyScore = s.Legitimacy < 30f
            ? 84f + 30f - s.Legitimacy
            : (s.Legitimacy < 55f && s.ThreatLevel >= 65f ? 62f + (s.ThreatLevel - 65f) * 0.6f + (55f - s.Legitimacy) * 0.3f : 0f);
        AddCandidate(all, s, constitution, recognition, KingdomActionType.RestoreLegitimacy,
            restoreLegitimacyScore,
            "Stability", CoreText.L("RP_WHY_LEGITIMACY", "Низкая легитимность угрожает устойчивости"));

        AddCandidate(all, s, constitution, recognition, KingdomActionType.UseIdeologyPower,
            s.PowerReady && !s.PowerActive ? PowerScore(s) : 0f,
            "Ideology", CoreText.L("RP_WHY_POWER", "Готово уникальное действие идеологии"));

        AddCandidate(all, s, constitution, recognition, KingdomActionType.Demobilize,
            !s.AtWar && s.ArmyRatio > ArmyCeiling(s.Ideology) ? 68f : 0f,
            "Stability", CoreText.L("RP_WHY_DEMOBILIZE", "В мирное время армия превышает безопасный предел"));

        float seekPeaceScore = 0f;
        if (s.AtWar && (s.Legitimacy < 45f || s.WarFatigue >= 55f))
            seekPeaceScore = 60f + s.WarFatigue * 0.35f + (s.Legitimacy < 45f ? (45f - s.Legitimacy) * 0.4f : 0f);
        AddCandidate(all, s, constitution, recognition, KingdomActionType.SeekPeace,
            seekPeaceScore,
            "Diplomacy", s.WarFatigue >= 55f
                ? CoreText.L("RP_WHY_PEACE_FATIGUE", "Народ изнурён войной — нужен мир хотя бы на время")
                : CoreText.L("RP_WHY_PEACE", "Мир снизит давление на легитимность и ресурсы"));

        AddCandidate(all, s, constitution, recognition, KingdomActionType.SeekAlliance,
            !s.AtWar && s.HasAllianceOffer ? 56f + AllianceBias(s.Ideology) + personalityDip : 0f,
            "Diplomacy", CoreText.L("RP_WHY_ALLIANCE", "Доступно мирное дипломатическое усиление"));

        AddCandidate(all, s, constitution, recognition, KingdomActionType.ProposeMarriage,
            s.DynastiesEnabled && s.MarriageCandidateId != 0 && !s.AtWar && !s.MarriageCandidateWar
                ? s.MarriageScore + RulerTraits.MarriageBias(s.Trait) : 0f,
            "Diplomacy", CoreText.L("RP_WHY_MARRIAGE", "Династический брак свяжет наш дом с соседним"),
            targetId: s.MarriageCandidateId);

        AddCandidate(all, s, constitution, recognition, KingdomActionType.SignTreaty,
            s.TreatiesEnabled && s.TreatyCandidateId != 0 && !s.AtWar && !s.TreatyCandidateWar
                ? s.TreatyScore : 0f,
            "Diplomacy", CoreText.L("RP_WHY_TREATY", "Договор закрепит мир или расширит торговлю"),
            targetId: s.TreatyCandidateId);

        // 0.39: после поражения держава 10 лет не начинает войн (та же логика, что в KingdomStrategy).
        bool warAverse = s.Year < s.WarAversionUntilYear;
        float declareWarScore = !warAverse && s.CasusBelliEnabled && s.WarTargetId != 0 && !s.AtWar && !s.Starving && !s.UnderRegency
            ? s.WarScore + personalityWar : 0f;
        AddCandidate(all, s, constitution, recognition, KingdomActionType.DeclareWar,
            declareWarScore,
            "Diplomacy", warAverse
                ? CoreText.L("RP_WHY_WAR_AVERSE", "Недавний разгром: держава восстанавливает силы")
                : CoreText.L("RP_WHY_WAR", "Обоснованный повод требует наказать обидчика"),
            targetId: s.WarTargetId);

        AddCandidate(all, s, constitution, recognition, KingdomActionType.InvestEspionage,
            s.EspionageEnabled && s.SpyLevel < SpyOps.MaxNetworkLevel && !s.AtWar && s.Gold >= s.SpyInvestCost + 30f
                ? 33f + s.SpyLevel * 2f : 0f,
            "Diplomacy", CoreText.L("RP_WHY_SPY", "Разведсеть позволит действовать тайно"));

        AddCandidate(all, s, constitution, recognition, KingdomActionType.CovertOperation,
            s.EspionageEnabled && s.SpyLevel >= 1 && s.CovertTargetId != 0 ? s.CovertScore : 0f,
            "Diplomacy", CoreText.L("RP_WHY_COVERT", "Агенты готовы провести операцию против цели"),
            targetId: s.CovertTargetId);

        AddCandidate(all, s, constitution, recognition, KingdomActionType.DeclareIndependence,
            s.VassalOfId != 0 && s.VassalApproval < s.VassalRebellionApproval && !s.AtWar
                ? 70f + (s.VassalRebellionApproval - s.VassalApproval) * 100f : 0f,
            "Survival", CoreText.L("RP_WHY_INDEPENDENCE", "Вассал сбрасывает ярмо сюзерена"),
            targetId: s.VassalOfId);

        float mobilizeScore = (s.AtWar && s.ArmyRatio < 0.8f) ? (52f + WarBias(s.Ideology) + personalityWar) : 0f;
        if (mobilizeScore <= 0f && s.SuzerainAtWar && !s.AtWar && s.ArmyRatio < 0.8f)
            mobilizeScore = 48f + s.ArmyRatio * 10f;
        if (s.UnderRegency && !s.AtWar) mobilizeScore = 0f;
        AddCandidate(all, s, constitution, recognition, KingdomActionType.Mobilize,
            mobilizeScore,
            "Survival", s.UnderRegency && !s.AtWar
                ? CoreText.L("RP_WHY_REGENCY_NO_MOBILIZE", "Регентство запрещает наступательную мобилизацию")
                : CoreText.L("RP_WHY_MOBILIZE", "Нужно восполнить армию для войны"));

        AddCandidate(all, s, constitution, recognition, KingdomActionType.InvestGrowth,
            !s.AtWar && !s.Starving && s.Legitimacy >= 45f ? 35f + GrowthBias(s.Ideology) : 0f,
            "Economy", CoreText.L("RP_WHY_GROWTH", "Стабильное государство может инвестировать в рост"));

        AddCandidate(all, s, constitution, recognition, KingdomActionType.Austerity,
            s.EconomyEnabled && !s.AusterityActive && s.DeficitYears >= 2 ? 70f + s.DeficitYears * 3f + (s.InEconomicCrisis ? 8f : 0f) : 0f,
            "Economy", CoreText.L("RP_WHY_AUSTERITY", "Казна в дефиците — нужен режим экономии"));

        AddCandidate(all, s, constitution, recognition, KingdomActionType.DevelopTrade,
            s.EconomyEnabled && !s.AtWar && s.TradeRoutes < s.MaxTradeRoutes && s.Gold >= 30f ? 42f + TradeBias(s.Ideology) : 0f,
            "Economy", CoreText.L("RP_WHY_TRADE", "Торговый путь принесёт доход и благосостояние"));

        AddCandidate(all, s, constitution, recognition, KingdomActionType.TradeEmbargo,
            s.EconomyEnabled && !s.EmbargoActive && s.EmbargoCandidate && !s.AtWar ? 44f + EmbargoBias(s.Ideology) : 0f,
            "Economy", CoreText.L("RP_WHY_EMBARGO", "Эмбарго подорвёт экономику соперника"));

        AddCandidate(all, s, constitution, recognition, KingdomActionType.AccumulateGold,
            !s.AtWar && s.Gold < 100f ? 30f + GoldBias(s.Ideology) : 0f,
            "Economy", CoreText.L("RP_WHY_GOLD", "Казна недостаточна для будущих решений"));

        AddCandidate(all, s, constitution, recognition, KingdomActionType.AccumulateStone,
            !s.AtWar && s.Stone < 80f ? 20f + StoneBias(s.Ideology) : 0f,
            "Economy", CoreText.L("RP_WHY_STONE", "Запас камня недостаточен для развития"));

        if (activeCampaign != null && !activeCampaign.IsCompleted)
        {
            KingdomActionType campaignAction = activeCampaign.CurrentStepAction;
            if (campaignAction != KingdomActionType.None)
            {
                GrandAiCandidate? existing = all.FirstOrDefault(c => c.Action == campaignAction);
                if (existing != null)
                {
                    existing.RawScore += 28f;
                }
                else
                {
                    AddCandidate(
                        all,
                        s,
                        constitution,
                        recognition,
                        campaignAction,
                        58f,
                        CampaignVector(activeCampaign.Goal),
                        CoreText.L(activeCampaign.ReasonKey, "Шаг долгосрочной государственной кампании"),
                        targetId: activeCampaign.TargetKingdomId);
                }
            }
        }

        var ranked = all
            .Where(c => !c.BlockedByGate && c.FinalScore > 0f)
            .OrderByDescending(c => c.FinalScore)
            .ThenBy(c => c.Action)
            .ToList();

        var blocked = all
            .Where(c => c.BlockedByGate)
            .OrderByDescending(c => c.RawScore)
            .ThenBy(c => c.Action)
            .ToList();

        GrandAiCandidate? best = ranked.FirstOrDefault();
        KingdomActionPlan plan = best == null
            ? new KingdomActionPlan(KingdomActionType.None, 0f, CoreText.L("RP_WHY_NONE", "Нет приоритетной угрозы или возможности"))
            : new KingdomActionPlan(best.Action, best.FinalScore, best.Reason);

        return new GrandAiReport
        {
            KingdomId = s.KingdomId,
            Year = s.Year,
            Vectors = vectors,
            SelectedPlan = plan,
            RankedCandidates = ranked,
            BlockedCandidates = blocked
        };
    }

    public static KingdomActionPlan Choose(
        KingdomStrategySnapshot s,
        Constitution? constitution = null,
        InternationalRecognition? recognition = null,
        StrategicCampaignPlan? activeCampaign = null)
        => Evaluate(s, constitution, recognition, activeCampaign).SelectedPlan;

    private static string CampaignVector(StrategicGoalType goal) => goal switch
    {
        StrategicGoalType.SurviveFamineAndCrisis => "Survival",
        StrategicGoalType.StabilizeRegime => "Stability",
        StrategicGoalType.ReclaimLostHomeland => "Diplomacy",
        StrategicGoalType.PeacefulUnification => "Diplomacy",
        StrategicGoalType.DefensivePreparation => "Survival",
        StrategicGoalType.EconomicExpansion => "Economy",
        _ => "Stability"
    };

    private static void AddCandidate(
        List<GrandAiCandidate> list,
        KingdomStrategySnapshot s,
        Constitution? constitution,
        InternationalRecognition? recognition,
        KingdomActionType action,
        float rawScore,
        string vector,
        string reason,
        int targetId = 0)
    {
        if (rawScore <= 0f) return;
        bool blocked = !PassesGates(s, constitution, recognition, action, targetId, out string gateReason);
        list.Add(new GrandAiCandidate
        {
            Action = action,
            RawScore = rawScore,
            Vector = vector,
            Reason = reason,
            BlockedByGate = blocked,
            GateReason = gateReason
        });
    }

    private static bool PassesGates(
        KingdomStrategySnapshot s,
        Constitution? constitution,
        InternationalRecognition? recognition,
        KingdomActionType action,
        int targetId,
        out string gateReason)
    {
        gateReason = string.Empty;

        // 1. Конституционные ограничения (статья XI «О чужой земле» и регентство).
        if (action == KingdomActionType.DeclareWar)
        {
            if (s.UnderRegency)
            {
                gateReason = "Регентство запрещает объявление наступательной войны";
                return false;
            }
            if (constitution != null && constitution.Supremacy == SupremacyRule.Binding && constitution.Land == LandLaw.Restitution)
            {
                if (s.WarCasusBelli != CasusBelliType.Reconquest
                    && s.WarCasusBelli != CasusBelliType.BrokenTreaty
                    && s.WarCasusBelli != CasusBelliType.Independence)
                {
                    gateReason = "Статья XI конституции (только возврат своего) запрещает завоевательные войны без территориальной реституции";
                    return false;
                }
            }
        }

        // 2. Фильтр международного признания (0.26).
        if (recognition != null && s.KingdomId > 0)
        {
            if (action == KingdomActionType.SeekAlliance)
            {
                if (!recognition.IsRecognized(s.KingdomId))
                {
                    gateReason = "Непризнанное государство не может инициировать вступление в альянс";
                    return false;
                }
            }
            else if (targetId > 0 && action == KingdomActionType.SignTreaty)
            {
                if (!recognition.CanContact(s.KingdomId, targetId, InternationalContact.Trade))
                {
                    gateReason = "Дипломатический договор невозможен без международного признания";
                    return false;
                }
            }
        }

        return true;
    }

    private static float ArmyCeiling(IdeologyType i) => i switch
    {
        IdeologyType.Militarism => 1.35f,
        IdeologyType.Ethnonationalism => 1.25f,
        IdeologyType.Khaganate => 1.25f,
        IdeologyType.Empire => 1.20f,
        IdeologyType.Communism => 1.15f,
        IdeologyType.FreeTerritory => 1.05f,
        IdeologyType.Liberalism => 0.85f,
        IdeologyType.Democracy => 0.85f,
        _ => 0.9f
    };

    private static float WarBias(IdeologyType i) => i switch
    {
        IdeologyType.Ethnonationalism => 22f,
        IdeologyType.Militarism => 22f,
        IdeologyType.Khaganate => 18f,
        IdeologyType.Empire => 14f,
        IdeologyType.Theocracy => 10f,
        IdeologyType.Communism => 8f,
        IdeologyType.FreeTerritory => -4f,
        IdeologyType.MerchantRepublic => -8f,
        IdeologyType.Democracy => -14f,
        _ => 0f
    };

    private static float AllianceBias(IdeologyType i) => i switch
    {
        IdeologyType.MerchantRepublic => 18f,
        IdeologyType.Democracy => 16f,
        IdeologyType.Empire => 6f,
        IdeologyType.Communism => -6f,
        IdeologyType.Liberalism => 17f,
        IdeologyType.Ethnonationalism => -24f,
        _ => 0f
    };

    private static float GrowthBias(IdeologyType i) => i switch
    {
        IdeologyType.Communism => 20f,
        IdeologyType.Tribalism => 18f,
        IdeologyType.Technocracy => 15f,
        IdeologyType.Empire => 14f,
        IdeologyType.Democracy => 12f,
        IdeologyType.FreeTerritory => 10f,
        IdeologyType.Ethnonationalism => 4f,
        IdeologyType.Liberalism => 10f,
        _ => 0f
    };

    private static float GoldBias(IdeologyType i) => i switch
    {
        IdeologyType.MerchantRepublic => 22f,
        IdeologyType.Empire => 15f,
        IdeologyType.Democracy => 14f,
        IdeologyType.Technocracy => 12f,
        IdeologyType.Ethnonationalism => -8f,
        IdeologyType.Tribalism => -10f,
        IdeologyType.Communism => -12f,
        _ => 0f
    };

    private static float StoneBias(IdeologyType i) => i switch
    {
        IdeologyType.Communism => 22f,
        IdeologyType.Technocracy => 20f,
        _ => 0f
    };

    private static float TradeBias(IdeologyType i) => i switch
    {
        IdeologyType.MerchantRepublic => 26f,
        IdeologyType.Democracy => 16f,
        IdeologyType.Technocracy => 13f,
        IdeologyType.Aristocracy => 8f,
        IdeologyType.Egalitarianism => 6f,
        IdeologyType.Militarism => -8f,
        IdeologyType.Ethnonationalism => -18f,
        IdeologyType.Liberalism => 8f,
        _ => 0f
    };

    private static float EmbargoBias(IdeologyType i) => i switch
    {
        IdeologyType.Ethnonationalism => 20f,
        IdeologyType.Militarism => 16f,
        IdeologyType.Communism => 10f,
        IdeologyType.MerchantRepublic => -14f,
        IdeologyType.Democracy => -8f,
        IdeologyType.Liberalism => -12f,
        _ => 0f
    };

    private static float PowerScore(KingdomStrategySnapshot s)
    {
        if (s.Ideology == IdeologyType.Militarism && s.AtWar) return 78f;
        if (s.Ideology == IdeologyType.Communism && (s.Food < 200f || s.Stone < 100f)) return 76f;
        if (s.Ideology == IdeologyType.Ethnonationalism && (s.AtWar || s.HostileNeighbors > 0)) return 75f;
        if (s.Ideology == IdeologyType.Democracy && (s.AvgDissent > 0.25f || s.Support < 45f)) return 74f;
        if (s.Ideology == IdeologyType.Tribalism && s.Starving) return 72f;
        return 48f;
    }

    private static float Clamp(float v, float min, float max) => v < min ? min : (v > max ? max : v);

    // ============================== 0.27: многошаговые кампании и дипломатическая память ==============================

    public static StrategicCampaignPlan? ManageCampaign(
        KingdomPolitics state,
        KingdomStrategySnapshot s,
        PoliticalWorld world,
        int year)
    {
        if (state == null || s == null || world == null) return null;

        StrategicCampaignPlan? current = state.ActiveCampaign;
        if (current != null)
        {
            // Проверяем: завершены ли все шаги, достигнута ли цель, или истёк срок плана
            bool goalSatisfied = IsGoalSatisfied(current, state, s);
            if (current.IsCompleted || goalSatisfied || year > current.DeadlineYear)
            {
                bool success = current.IsCompleted || goalSatisfied;
                world.Bus.Publish(new PolEvent(PolEventType.StrategicCampaignCompleted, "RP27_EV_CAMPAIGN_COMPLETED")
                {
                    Year = year,
                    KingdomId = state.KingdomId,
                    Severity = Severity.Minor
                }.With("kingdom", world.KingdomNameOf(state.KingdomId))
                    .With("goal", GoalLocKey(current.Goal))
                    .With("target", current.TargetKingdomName ?? string.Empty)
                    .With("result", success ? "RP27_CAMPAIGN_RES_SUCCESS" : "RP27_CAMPAIGN_RES_EXPIRED")
                    .With("steps", current.CompletedSteps + "/" + current.Steps.Count));
                state.ActiveCampaign = null;
                current = null;
            }
            else if (s.Starving && current.Goal != StrategicGoalType.SurviveFamineAndCrisis)
            {
                // Чрезвычайная ситуация (голод) перехватывает обычный план
                state.ActiveCampaign = null;
                current = null;
            }
        }

        if (current == null)
        {
            StrategicCampaignPlan? created = BuildNewCampaign(state, s, world, year);
            if (created != null)
            {
                state.ActiveCampaign = created;
                world.Bus.Publish(new PolEvent(PolEventType.StrategicCampaignStarted, "RP27_EV_CAMPAIGN_STARTED")
                {
                    Year = year,
                    KingdomId = state.KingdomId,
                    Severity = Severity.Minor
                }.With("kingdom", world.KingdomNameOf(state.KingdomId))
                    .With("goal", GoalLocKey(created.Goal))
                    .With("target", string.IsNullOrEmpty(created.TargetKingdomName) ? "—" : created.TargetKingdomName)
                    .With("years", Math.Max(1, created.DeadlineYear - year))
                    .With("steps", created.Steps.Count));
                current = created;
            }
        }

        return current;
    }

    public static void NotifyStepExecuted(KingdomPolitics state, KingdomActionType executedAction, PoliticalWorld world, int year)
    {
        StrategicCampaignPlan? campaign = state?.ActiveCampaign;
        if (campaign == null || executedAction == KingdomActionType.None) return;
        if (campaign.CurrentStepAction == executedAction)
        {
            campaign.CurrentStepIndex++;
            campaign.CompletedSteps++;
            if (campaign.IsCompleted && world != null)
            {
                world.Bus.Publish(new PolEvent(PolEventType.StrategicCampaignCompleted, "RP27_EV_CAMPAIGN_COMPLETED")
                {
                    Year = year,
                    KingdomId = state.KingdomId,
                    Severity = Severity.Minor
                }.With("kingdom", world.KingdomNameOf(state.KingdomId))
                    .With("goal", GoalLocKey(campaign.Goal))
                    .With("target", campaign.TargetKingdomName ?? string.Empty)
                    .With("result", "RP27_CAMPAIGN_RES_SUCCESS")
                    .With("steps", campaign.CompletedSteps + "/" + campaign.Steps.Count));
                state.ActiveCampaign = null;
            }
        }
    }

    private static bool IsGoalSatisfied(StrategicCampaignPlan plan, KingdomPolitics state, KingdomStrategySnapshot s)
    {
        return plan.Goal switch
        {
            StrategicGoalType.SurviveFamineAndCrisis => !s.Starving && !s.InEconomicCrisis && s.FoodPerPerson >= 0.8f,
            StrategicGoalType.StabilizeRegime => s.Legitimacy >= 70f && s.ThreatLevel < 35f && !s.CivilWarActive,
            StrategicGoalType.ReclaimLostHomeland => s.AtWar && plan.CompletedSteps >= 2,
            StrategicGoalType.PeacefulUnification => s.PeacefulNeighbors > 0 && plan.CompletedSteps >= 2,
            StrategicGoalType.DefensivePreparation => s.ArmyRatio >= 0.75f && plan.CompletedSteps >= 2,
            StrategicGoalType.EconomicExpansion => s.Prosperity >= 72f && s.TradeRoutes >= 2 && plan.CompletedSteps >= 2,
            _ => false
        };
    }

    private static StrategicCampaignPlan? BuildNewCampaign(
        KingdomPolitics state,
        KingdomStrategySnapshot s,
        PoliticalWorld world,
        int year)
    {
        if (s.Starving || s.InEconomicCrisis)
        {
            return new StrategicCampaignPlan
            {
                Goal = StrategicGoalType.SurviveFamineAndCrisis,
                StartedYear = year,
                DeadlineYear = year + 6,
                Steps = new List<KingdomActionType>
                {
                    s.Starving ? KingdomActionType.EmergencyFood : KingdomActionType.Austerity,
                    KingdomActionType.AccumulateGold,
                    KingdomActionType.InvestGrowth
                },
                ReasonKey = "RP27_GOAL_SURVIVE"
            };
        }

        if (s.Legitimacy < 50f || s.ThreatLevel >= 60f || s.CivilWarActive)
        {
            return new StrategicCampaignPlan
            {
                Goal = StrategicGoalType.StabilizeRegime,
                StartedYear = year,
                DeadlineYear = year + 8,
                Steps = new List<KingdomActionType>
                {
                    s.CivilWarActive ? KingdomActionType.SuppressRebellion : KingdomActionType.RestoreLegitimacy,
                    KingdomActionType.RestoreLegitimacy,
                    KingdomActionType.Demobilize
                },
                ReasonKey = "RP27_GOAL_STABILIZE"
            };
        }

        // Проверяем долгосрочную память об отнятых землях (ирредентизм / реванш)
        if (!s.AtWar && world.DiplomaticMemories.TryFindIrredentistTarget(state.KingdomId, year, out int rivalId, out string cityName))
        {
            if (world.GetKingdom(rivalId) != null)
            {
                return new StrategicCampaignPlan
                {
                    Goal = StrategicGoalType.ReclaimLostHomeland,
                    TargetKingdomId = rivalId,
                    TargetKingdomName = world.KingdomNameOf(rivalId),
                    SubjectName = cityName,
                    StartedYear = year,
                    DeadlineYear = year + 12,
                    Steps = new List<KingdomActionType>
                    {
                        KingdomActionType.AccumulateGold,
                        KingdomActionType.Mobilize,
                        KingdomActionType.DeclareWar
                    },
                    ReasonKey = "RP27_GOAL_RECLAIM"
                };
            }
        }

        if (!s.AtWar && s.HostileNeighbors > 0 && s.ArmyRatio < 0.65f)
        {
            return new StrategicCampaignPlan
            {
                Goal = StrategicGoalType.DefensivePreparation,
                StartedYear = year,
                DeadlineYear = year + 8,
                Steps = new List<KingdomActionType>
                {
                    KingdomActionType.Mobilize,
                    KingdomActionType.SeekAlliance,
                    KingdomActionType.SignTreaty
                },
                ReasonKey = "RP27_GOAL_DEFENSE"
            };
        }

        if (!s.AtWar && s.TreatyCandidateId > 0 && world.DiplomaticMemories.GetTrustScore(state.KingdomId, s.TreatyCandidateId, year) >= 10)
        {
            return new StrategicCampaignPlan
            {
                Goal = StrategicGoalType.PeacefulUnification,
                TargetKingdomId = s.TreatyCandidateId,
                TargetKingdomName = world.KingdomNameOf(s.TreatyCandidateId),
                StartedYear = year,
                DeadlineYear = year + 10,
                Steps = new List<KingdomActionType>
                {
                    KingdomActionType.SignTreaty,
                    KingdomActionType.SeekAlliance,
                    KingdomActionType.DevelopTrade
                },
                ReasonKey = "RP27_GOAL_UNIFY"
            };
        }

        if (!s.AtWar && s.EconomyEnabled && s.Prosperity < 68f)
        {
            return new StrategicCampaignPlan
            {
                Goal = StrategicGoalType.EconomicExpansion,
                StartedYear = year,
                DeadlineYear = year + 10,
                Steps = new List<KingdomActionType>
                {
                    KingdomActionType.DevelopTrade,
                    KingdomActionType.AccumulateGold,
                    KingdomActionType.InvestGrowth
                },
                ReasonKey = "RP27_GOAL_ECONOMY"
            };
        }

        return null;
    }

    public static string GoalLocKey(StrategicGoalType goal) => goal switch
    {
        StrategicGoalType.SurviveFamineAndCrisis => "RP27_GOAL_SURVIVE",
        StrategicGoalType.StabilizeRegime => "RP27_GOAL_STABILIZE",
        StrategicGoalType.ReclaimLostHomeland => "RP27_GOAL_RECLAIM",
        StrategicGoalType.PeacefulUnification => "RP27_GOAL_UNIFY",
        StrategicGoalType.DefensivePreparation => "RP27_GOAL_DEFENSE",
        StrategicGoalType.EconomicExpansion => "RP27_GOAL_ECONOMY",
        _ => "RP27_GOAL_NONE"
    };
}

public enum StrategicGoalType
{
    None = 0,
    SurviveFamineAndCrisis = 1,
    StabilizeRegime = 2,
    ReclaimLostHomeland = 3,
    PeacefulUnification = 4,
    DefensivePreparation = 5,
    EconomicExpansion = 6
}

public sealed class StrategicCampaignPlan
{
    public StrategicGoalType Goal { get; set; }
    public int TargetKingdomId { get; set; }
    public string TargetKingdomName { get; set; } = string.Empty;
    public string SubjectName { get; set; } = string.Empty;
    public int StartedYear { get; set; }
    public int DeadlineYear { get; set; }
    public int CurrentStepIndex { get; set; }
    public int CompletedSteps { get; set; }
    public List<KingdomActionType> Steps { get; set; } = new List<KingdomActionType>();
    public string ReasonKey { get; set; } = string.Empty;

    public KingdomActionType CurrentStepAction =>
        Steps != null && CurrentStepIndex >= 0 && CurrentStepIndex < Steps.Count
            ? Steps[CurrentStepIndex]
            : KingdomActionType.None;

    public bool IsCompleted => Steps != null && Steps.Count > 0 && CurrentStepIndex >= Steps.Count;
}

public enum DiplomaticMemoryKind
{
    None = 0,
    AlliedInWar = 1,
    AbandonedInWar = 2,
    BrokeTreaty = 3,
    AnnexedOurCity = 4,
    RestoredOurState = 5,
    HonoredPeace = 6,
    SpoilsShortchanged = 7
}

public sealed class DiplomaticMemoryEntry
{
    public int FromKingdomId { get; set; }
    public int TowardsKingdomId { get; set; }
    public DiplomaticMemoryKind Kind { get; set; }
    public int Year { get; set; }
    public int ExpiresYear { get; set; }
    public int TrustDelta { get; set; }
    public string SubjectName { get; set; } = string.Empty;
}

public sealed class DiplomaticMemoryBook
{
    private readonly List<DiplomaticMemoryEntry> _entries = new List<DiplomaticMemoryEntry>();

    public IReadOnlyList<DiplomaticMemoryEntry> All => _entries;

    public int Count => _entries.Count;

    public void Clear() => _entries.Clear();

    public void Record(
        int fromId,
        int towardsId,
        DiplomaticMemoryKind kind,
        int year,
        int durationYears,
        int trustDelta,
        string subjectName = "")
    {
        if (fromId <= 0 || towardsId <= 0 || fromId == towardsId || kind == DiplomaticMemoryKind.None) return;
        for (int i = _entries.Count - 1; i >= 0; i--)
        {
            var e = _entries[i];
            if (e.FromKingdomId == fromId && e.TowardsKingdomId == towardsId && e.Kind == kind
                && string.Equals(e.SubjectName, subjectName ?? string.Empty, StringComparison.Ordinal))
            {
                _entries.RemoveAt(i);
            }
        }
        _entries.Add(new DiplomaticMemoryEntry
        {
            FromKingdomId = fromId,
            TowardsKingdomId = towardsId,
            Kind = kind,
            Year = year,
            ExpiresYear = year + Math.Max(1, durationYears),
            TrustDelta = trustDelta,
            SubjectName = subjectName ?? string.Empty
        });
    }

    public void Restore(DiplomaticMemoryEntry entry)
    {
        if (entry != null && entry.FromKingdomId > 0 && entry.TowardsKingdomId > 0)
        {
            _entries.Add(entry);
        }
    }

    public int GetTrustScore(int fromId, int towardsId, int currentYear)
    {
        int sum = 0;
        for (int i = 0; i < _entries.Count; i++)
        {
            var e = _entries[i];
            if (e.FromKingdomId == fromId && e.TowardsKingdomId == towardsId && currentYear <= e.ExpiresYear)
            {
                sum += e.TrustDelta;
            }
        }
        return sum < -100 ? -100 : (sum > 100 ? 100 : sum);
    }

    public bool TryFindIrredentistTarget(int fromId, int currentYear, out int holderId, out string cityName)
    {
        holderId = 0;
        cityName = string.Empty;
        for (int i = _entries.Count - 1; i >= 0; i--)
        {
            var e = _entries[i];
            if (e.FromKingdomId == fromId && e.Kind == DiplomaticMemoryKind.AnnexedOurCity && currentYear <= e.ExpiresYear)
            {
                holderId = e.TowardsKingdomId;
                cityName = e.SubjectName;
                return true;
            }
        }
        return false;
    }

    public void PruneExpired(int currentYear)
    {
        for (int i = _entries.Count - 1; i >= 0; i--)
        {
            if (currentYear > _entries[i].ExpiresYear)
            {
                _entries.RemoveAt(i);
            }
        }
    }
}
