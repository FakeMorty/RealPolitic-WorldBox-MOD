using System;
using System.Collections.Generic;

namespace RealPolitic.Core;

public enum KingdomActionType
{
    None, EmergencyFood, Defend, Mobilize, Demobilize, RestoreLegitimacy,
    UseIdeologyPower, SeekPeace, SeekAlliance, InvestGrowth, AccumulateGold, AccumulateStone,
    SuppressRebellion, DevelopTrade, Austerity, TradeEmbargo,
    ProposeMarriage,
    SignTreaty, DeclareWar, InvestEspionage, CovertOperation, DeclareIndependence,
    IntegrateMember
}

/// <summary>Единый read-only снимок состояния государства для стратегического ИИ.</summary>
public sealed class KingdomStrategySnapshot
{
    public int KingdomId { get; init; }
    public int Year { get; init; }
    public IdeologyType Ideology { get; init; } = IdeologyType.Monarchy;
    public SuccessionLaw SuccessionLaw { get; init; } = SuccessionLaw.Salic;
    public bool UnderRegency { get; init; }
    public int Population { get; init; }
    public int Warriors { get; init; }
    public int MaxWarriors { get; init; }
    public int Cities { get; init; }
    public float Food { get; init; }
    public float Gold { get; init; }
    public float Stone { get; init; }
    public float AvgLoyalty { get; init; }
    public float Legitimacy { get; init; }
    public float ThreatLevel { get; init; }
    public float Support { get; init; }
    public float AvgDissent { get; init; }
    public bool AtWar { get; init; }
    public bool Starving { get; init; }
    public bool PowerReady { get; init; }
    public bool PowerActive { get; init; }
    public int LastActionYear { get; init; } = int.MinValue;
    public int ActionCooldownYears { get; init; } = 3;
    public int PeacefulNeighbors { get; init; }
    public int HostileNeighbors { get; init; }
    public bool HasAllianceOffer { get; init; }
    /// <summary>В государстве идёт гражданская война.</summary>
    // --- экономика ---
    /// <summary>Включена ли экономическая модель (иначе экономические действия не планируются).</summary>
    public bool EconomyEnabled { get; init; }

    /// <summary>Благосостояние 0..100.</summary>
    public float Prosperity { get; init; }

    /// <summary>Прибыль с торговли за прошлый год.</summary>
    public float TradeIncome { get; init; }

    /// <summary>Открытых торговых путей.</summary>
    public int TradeRoutes { get; init; }

    /// <summary>Предел путей для державы.</summary>
    public int MaxTradeRoutes { get; init; } = 6;

    /// <summary>Лет подряд в дефиците.</summary>
    public int DeficitYears { get; init; }

    /// <summary>Идёт ли экономический кризис.</summary>
    public bool InEconomicCrisis { get; init; }

    /// <summary>Режим экономии уже введён.</summary>
    public bool AusterityActive { get; init; }

    /// <summary>Есть ли сосед, против которого имеет смысл ввести эмбарго.</summary>
    public bool EmbargoCandidate { get; init; }

    /// <summary>Эмбарго уже действует.</summary>
    public bool EmbargoActive { get; init; }

    public bool CivilWarActive { get; init; }
    /// <summary>Доля силы повстанцев (0..1), если война идёт.</summary>
    public float RebelShare { get; init; }

    // --- этап 19: личности и браки ---
    /// <summary>Черта характера правителя.</summary>
    public RulerTrait Trait { get; init; } = RulerTrait.None;

    /// <summary>Амбиции правителя 0..100.</summary>
    public float Ambition { get; init; } = 50f;

    /// <summary>Включены ли династии (иначе браки не планируются).</summary>
    public bool DynastiesEnabled { get; init; }

    /// <summary>Id лучшего кандидата в династические супруги (0 — кандидата нет).</summary>
    public int MarriageCandidateId { get; init; }

    /// <summary>С кандидатом сейчас война (брак невозможен; защита от устаревшего плана).</summary>
    public bool MarriageCandidateWar { get; init; }

    /// <summary>Привлекательность брака с кандидатом (для score действия).</summary>
    public float MarriageScore { get; init; }

    // --- этап 20: глубокая дипломатия ---
    /// <summary>Включены ли договоры.</summary>
    public bool TreatiesEnabled { get; init; }

    /// <summary>Id лучшего кандидата на договор (0 — нет).</summary>
    public int TreatyCandidateId { get; init; }

    /// <summary>Тип предлагаемого договора.</summary>
    public TreatyType TreatyType { get; init; } = TreatyType.NonAggression;

    /// <summary>С кандидатом на договор сейчас война.</summary>
    public bool TreatyCandidateWar { get; init; }

    /// <summary>Привлекательность договора (для score).</summary>
    public float TreatyScore { get; init; }

    /// <summary>Включены ли казус белли.</summary>
    public bool CasusBelliEnabled { get; init; }

    /// <summary>Id цели для объявления войны (0 — нет).</summary>
    public int WarTargetId { get; init; }

    /// <summary>Лучший повод к войне против цели.</summary>
    public CasusBelliType WarCasusBelli { get; init; } = CasusBelliType.Conquest;

    /// <summary>Привлекательность войны с целью (для score).</summary>
    public float WarScore { get; init; }

    /// <summary>Включён ли шпионаж.</summary>
    public bool EspionageEnabled { get; init; }

    /// <summary>Текущий уровень разведсети (0 — нет сети).</summary>
    public int SpyLevel { get; init; }

    /// <summary>Id цели для тайной операции (0 — нет).</summary>
    public int CovertTargetId { get; init; }

    /// <summary>Тип тайной операции.</summary>
    public SpyOpType CovertOp { get; init; } = SpyOpType.Sabotage;

    /// <summary>Привлекательность тайной операции (для score).</summary>
    public float CovertScore { get; init; }

    /// <summary>Стоимость вложения в разведсеть.</summary>
    public float SpyInvestCost { get; init; } = 25f;

    /// <summary>Id сюзерена, если держава — вассал (0 — свободна).</summary>
    public int VassalOfId { get; init; }

    /// <summary>Одобрение вассалитета (0..1); низкое ведёт к войне за независимость.</summary>
    public float VassalApproval { get; init; } = 1f;

    /// <summary>Сюзерен сейчас воюет — вассалу пора мобилизоваться.</summary>
    public bool SuzerainAtWar { get; init; }

    /// <summary>Порог одобрения, ниже которого вассал восстаёт.</summary>
    public float VassalRebellionApproval { get; init; } = 0.3f;

    /// <summary>Этап 21: держава возглавляет унию.</summary>
    public bool IsUnionLeader { get; init; }

    /// <summary>Этап 21: тип унии державы (если не в унии — значение не используется).</summary>
    public UnionType UnionType { get; init; }

    /// <summary>Этап 21: держава состоит в унии.</summary>
    public bool InUnion { get; init; }

    /// <summary>Этап 21: число участников унии державы.</summary>
    public int UnionMemberCount { get; init; }

    /// <summary>Этап 22: усталость от войны (0..100). Высокая усталость — повод искать мира.</summary>
    public float WarFatigue { get; init; }

    /// <summary>0.39: до какого года держава не начинает войн (после поражения — 10 лет).</summary>
    public int WarAversionUntilYear { get; init; }

    /// <summary>0.39: вина перед жителями за нарушенные обещания (0..100).</summary>
    public float PromiseScore { get; init; }

    public float FoodPerPerson => Population <= 0 ? Food : Food / Population;
    public float ArmyRatio => MaxWarriors <= 0 ? 0f : (float)Warriors / MaxWarriors;
    // int.MinValue означает, что государство ещё ни разу не принимало решение.
    // Разность считается в long, чтобы первый тик не ломался из-за переполнения int.
    public bool CooldownReady => LastActionYear == int.MinValue
        || ActionCooldownYears <= 0
        || (long)Year - LastActionYear >= ActionCooldownYears;
}

public sealed class KingdomActionPlan
{
    public KingdomActionType Action { get; }
    public float Score { get; }
    public string Reason { get; }
    public bool IsFallback => Action == KingdomActionType.None;

    public KingdomActionPlan(KingdomActionType action, float score, string reason)
    {
        Action = action; Score = score; Reason = reason ?? string.Empty;
    }
}

/// <summary>Детерминированный планировщик: выбирает одно объяснимое действие, но не меняет мир.</summary>
public static class KingdomStrategicPlanner
{
    private sealed class Candidate
    {
        public KingdomActionType Action;
        public float Score;
        public string Reason = string.Empty;
    }

    public static KingdomActionPlan Choose(KingdomStrategySnapshot s)
    {
        if (s == null) throw new ArgumentNullException(nameof(s));
        if (!s.CooldownReady)
            return new KingdomActionPlan(KingdomActionType.None, 0f, CoreText.L("RP_WHY_COOLDOWN", "Ожидание стратегической перезарядки"));

        var c = new List<Candidate>();
        // 0.23.2: в игре запасы еды в амбарах обычно невелики относительно населения (жители едят по мере голода),
        // поэтому порог «1,5 еды на человека» выполнялся почти всегда и ИИ каждый год «пополнял еду».
        // Теперь: реальный голод (город голодает) — 100; совсем пустые амбары (<0,4 на человека) — 60.
        Add(c, KingdomActionType.EmergencyFood, s.Starving ? 100f : (s.FoodPerPerson < 0.4f ? 60f : 0f), CoreText.L("RP_WHY_FOOD", "Голод требует пополнения запасов еды"));
        // Гражданская война — приоритет выше рутины, но ниже голода: голодная армия мятеж не подавит.
        Add(c, KingdomActionType.SuppressRebellion, s.CivilWarActive ? 92f + s.RebelShare * 6f : 0f, CoreText.L("RP_WHY_REBELLION", "Мятеж требует немедленного подавления"));
        Add(c, KingdomActionType.Defend, s.AtWar && s.ArmyRatio < 0.55f ? 88f : 0f, CoreText.L("RP_WHY_DEFEND", "Война и недостаток армии требуют обороны"));
        float restoreLegitimacyScore = s.Legitimacy < 30f
            ? 84f + 30f - s.Legitimacy
            : (s.Legitimacy < 55f && s.ThreatLevel >= 65f ? 62f + (s.ThreatLevel - 65f) * 0.6f + (55f - s.Legitimacy) * 0.3f : 0f);
        Add(c, KingdomActionType.RestoreLegitimacy, restoreLegitimacyScore, CoreText.L("RP_WHY_LEGITIMACY", "Низкая легитимность угрожает устойчивости"));
        Add(c, KingdomActionType.UseIdeologyPower, s.PowerReady && !s.PowerActive ? PowerScore(s) : 0f, CoreText.L("RP_WHY_POWER", "Готово уникальное действие идеологии"));
        Add(c, KingdomActionType.Demobilize, !s.AtWar && s.ArmyRatio > ArmyCeiling(s.Ideology) ? 68f : 0f, CoreText.L("RP_WHY_DEMOBILIZE", "В мирное время армия превышает безопасный предел"));
        // Этап 22: народ изнурён войной — мир становится самым сильным желанием власти.
        float seekPeaceScore = 0f;
        if (s.AtWar && (s.Legitimacy < 45f || s.WarFatigue >= 55f))
        {
            seekPeaceScore = 60f + s.WarFatigue * 0.35f + (s.Legitimacy < 45f ? (45f - s.Legitimacy) * 0.4f : 0f);
        }
        Add(c, KingdomActionType.SeekPeace, seekPeaceScore, s.WarFatigue >= 55f
            ? CoreText.L("RP_WHY_PEACE_FATIGUE", "Народ изнурён войной — нужен мир хотя бы на время")
            : CoreText.L("RP_WHY_PEACE", "Мир снизит давление на легитимность и ресурсы"));
        // Этап 19: характер и амбиции правителя подмешиваются в военные и дипломатические оценки.
        float personalityWar = RulerTraits.WarBias(s.Trait) + (s.Ambition - 50f) * 0.2f;
        float personalityDip = RulerTraits.DiplomacyBias(s.Trait) + (50f - s.Ambition) * 0.1f;
        Add(c, KingdomActionType.SeekAlliance, !s.AtWar && s.HasAllianceOffer ? 56f + AllianceBias(s.Ideology) + personalityDip : 0f, CoreText.L("RP_WHY_ALLIANCE", "Доступно мирное дипломатическое усиление"));
        // Династический брак: редкое, но сильное дипломатическое вложение монархии.
        Add(c, KingdomActionType.ProposeMarriage, s.DynastiesEnabled && s.MarriageCandidateId != 0 && !s.AtWar
            ? s.MarriageScore + RulerTraits.MarriageBias(s.Trait) : 0f, CoreText.L("RP_WHY_MARRIAGE", "Династический брак свяжет наш дом с соседним"));
        // Этап 20: договоры — закрепление мира и торговли с надёжным соседом.
        Add(c, KingdomActionType.SignTreaty, s.TreatiesEnabled && s.TreatyCandidateId != 0 && !s.AtWar
            ? s.TreatyScore : 0f, CoreText.L("RP_WHY_TREATY", "Договор закрепит мир или расширит торговлю"));
        // Этап 20: война по обоснованному поводу — политический инструмент, а не рутина.
        float declareWarScore = s.CasusBelliEnabled && s.WarTargetId != 0 && !s.AtWar && !s.Starving && !s.UnderRegency
            ? s.WarScore + RulerTraits.WarBias(s.Trait) + (s.Ambition - 50f) * 0.2f : 0f;
        // 0.39: после проигранной войны правитель учится — новых войн не начинает.
        bool warAverse = s.Year < s.WarAversionUntilYear;
        if (warAverse)
        {
            declareWarScore = 0f;
        }
        Add(c, KingdomActionType.DeclareWar, declareWarScore, warAverse
            ? CoreText.L("RP_WHY_WAR_AVERSE", "Недавний разгром: держава восстанавливает силы")
            : CoreText.L("RP_WHY_WAR", "Обоснованный повод требует наказать обидчика"));
        // Этап 20: разведка и тайные операции.
        Add(c, KingdomActionType.InvestEspionage, s.EspionageEnabled && s.SpyLevel < SpyOps.MaxNetworkLevel && !s.AtWar
            && s.Gold >= s.SpyInvestCost + 30f ? 33f + s.SpyLevel * 2f : 0f, CoreText.L("RP_WHY_SPY", "Разведсеть позволит действовать тайно"));
        Add(c, KingdomActionType.CovertOperation, s.EspionageEnabled && s.SpyLevel >= 1 && s.CovertTargetId != 0
            ? s.CovertScore : 0f, CoreText.L("RP_WHY_COVERT", "Агенты готовы провести операцию против цели"));
        // Этап 20: вассал сбрасывает ярмо, когда одобрение упало ниже порога.
        Add(c, KingdomActionType.DeclareIndependence, s.VassalOfId != 0 && s.VassalApproval < s.VassalRebellionApproval && !s.AtWar
            ? 70f + (s.VassalRebellionApproval - s.VassalApproval) * 100f : 0f, CoreText.L("RP_WHY_INDEPENDENCE", "Вассал сбрасывает ярмо сюзерена"));
        // 0.24: интеграция упразднена — вступление в соединяющую унию само и есть поглощение.
        // При регентстве разрешена только оборонительная мобилизация во время войны:
        float mobilizeScore = (s.AtWar && s.ArmyRatio < 0.8f) ? (52f + WarBias(s.Ideology) + personalityWar) : 0f;
        // Этап 20: воинская обязанность вассала — сюзерен в войне, надо поднимать армию.
        if (mobilizeScore <= 0f && s.SuzerainAtWar && !s.AtWar && s.ArmyRatio < 0.8f)
        {
            mobilizeScore = 48f + s.ArmyRatio * 10f;
        }
        if (s.UnderRegency && !s.AtWar) mobilizeScore = 0f;
        Add(c, KingdomActionType.Mobilize, mobilizeScore, s.UnderRegency && !s.AtWar ? CoreText.L("RP_WHY_REGENCY_NO_MOBILIZE", "Регентство запрещает наступательную мобилизацию") : CoreText.L("RP_WHY_MOBILIZE", "Нужно восполнить армию для войны"));
        Add(c, KingdomActionType.InvestGrowth, !s.AtWar && !s.Starving && s.Legitimacy >= 45f ? 35f + GrowthBias(s.Ideology) : 0f, CoreText.L("RP_WHY_GROWTH", "Стабильное государство может инвестировать в рост"));
        // Экономика: дефицит лечится экономией, рост — торговыми путями, враг — эмбарго.
        Add(c, KingdomActionType.Austerity, s.EconomyEnabled && !s.AusterityActive && s.DeficitYears >= 2 ? 70f + s.DeficitYears * 3f + (s.InEconomicCrisis ? 8f : 0f) : 0f, CoreText.L("RP_WHY_AUSTERITY", "Казна в дефиците — нужен режим экономии"));
        Add(c, KingdomActionType.DevelopTrade, s.EconomyEnabled && !s.AtWar && s.TradeRoutes < s.MaxTradeRoutes && s.Gold >= 30f ? 42f + TradeBias(s.Ideology) : 0f, CoreText.L("RP_WHY_TRADE", "Торговый путь принесёт доход и благосостояние"));
        Add(c, KingdomActionType.TradeEmbargo, s.EconomyEnabled && !s.EmbargoActive && s.EmbargoCandidate && !s.AtWar ? 44f + EmbargoBias(s.Ideology) : 0f, CoreText.L("RP_WHY_EMBARGO", "Эмбарго подорвёт экономику соперника"));
        Add(c, KingdomActionType.AccumulateGold, !s.AtWar && s.Gold < 100f ? 30f + GoldBias(s.Ideology) : 0f, CoreText.L("RP_WHY_GOLD", "Казна недостаточна для будущих решений"));
        Add(c, KingdomActionType.AccumulateStone, !s.AtWar && s.Stone < 80f ? 20f + StoneBias(s.Ideology) : 0f, CoreText.L("RP_WHY_STONE", "Запас камня недостаточен для развития"));

        Candidate best = null;
        foreach (var item in c)
            if (item.Score > 0f && (best == null || item.Score > best.Score || (Math.Abs(item.Score - best.Score) < 0.001f && item.Action < best.Action))) best = item;
        return best == null ? new KingdomActionPlan(KingdomActionType.None, 0f, CoreText.L("RP_WHY_NONE", "Нет приоритетной угрозы или возможности")) : new KingdomActionPlan(best.Action, best.Score, best.Reason);
    }

    private static void Add(List<Candidate> list, KingdomActionType action, float score, string reason) => list.Add(new Candidate { Action = action, Score = score, Reason = reason });
    private static float ArmyCeiling(IdeologyType i)
    {
        switch (i)
        {
        case IdeologyType.Militarism: return 1.35f;
        case IdeologyType.Ethnonationalism: return 1.25f;
        case IdeologyType.Khaganate: return 1.25f;
        case IdeologyType.Empire: return 1.20f;
        case IdeologyType.Communism: return 1.15f;
        case IdeologyType.FreeTerritory: return 1.05f;
        case IdeologyType.Liberalism: return 0.85f;
        case IdeologyType.Democracy: return 0.85f;
        default: return 0.9f;
        }
    }
    private static float WarBias(IdeologyType i)
    {
        switch (i)
        {
        case IdeologyType.Ethnonationalism: return 22f;
        case IdeologyType.Militarism: return 22f;
        case IdeologyType.Khaganate: return 18f;
        case IdeologyType.Empire: return 14f;
        case IdeologyType.Theocracy: return 10f;
        case IdeologyType.Communism: return 8f;
        case IdeologyType.FreeTerritory: return -4f;
        case IdeologyType.MerchantRepublic: return -8f;
        case IdeologyType.Democracy: return -14f;
        default: return 0f;
        }
    }
    private static float AllianceBias(IdeologyType i)
    {
        switch (i)
        {
        case IdeologyType.MerchantRepublic: return 18f;
        case IdeologyType.Democracy: return 16f;
        case IdeologyType.Empire: return 6f;
        case IdeologyType.Communism: return -6f;
        case IdeologyType.Liberalism: return 17f;
        case IdeologyType.Ethnonationalism: return -24f;
        default: return 0f;
        }
    }
    private static float GrowthBias(IdeologyType i)
    {
        switch (i)
        {
        case IdeologyType.Communism: return 20f;
        case IdeologyType.Tribalism: return 18f;
        case IdeologyType.Technocracy: return 15f;
        case IdeologyType.Empire: return 14f;
        case IdeologyType.Democracy: return 12f;
        case IdeologyType.FreeTerritory: return 10f;
        case IdeologyType.Ethnonationalism: return 4f;
        case IdeologyType.Liberalism: return 10f;
        default: return 0f;
        }
    }
    private static float GoldBias(IdeologyType i)
    {
        switch (i)
        {
        case IdeologyType.MerchantRepublic: return 22f;
        case IdeologyType.Empire: return 15f;
        case IdeologyType.Democracy: return 14f;
        case IdeologyType.Technocracy: return 12f;
        case IdeologyType.Ethnonationalism: return -8f;
        case IdeologyType.Tribalism: return -10f;
        case IdeologyType.Communism: return -12f;
        default: return 0f;
        }
    }
    private static float StoneBias(IdeologyType i)
    {
        switch (i)
        {
        case IdeologyType.Communism: return 22f;
        case IdeologyType.Technocracy: return 20f;
        default: return 0f;
        }
    }
    private static float TradeBias(IdeologyType i)
    {
        switch (i)
        {
        case IdeologyType.MerchantRepublic: return 26f;
        case IdeologyType.Democracy: return 16f;
        case IdeologyType.Technocracy: return 13f;
        case IdeologyType.Aristocracy: return 8f;
        case IdeologyType.Egalitarianism: return 6f;
        case IdeologyType.Militarism: return -8f;
        case IdeologyType.Ethnonationalism: return -18f;
        case IdeologyType.Liberalism: return 8f;
        default: return 0f;
        }
    }
    private static float EmbargoBias(IdeologyType i)
    {
        switch (i)
        {
        case IdeologyType.Ethnonationalism: return 20f;
        case IdeologyType.Militarism: return 16f;
        case IdeologyType.Communism: return 10f;
        case IdeologyType.MerchantRepublic: return -14f;
        case IdeologyType.Democracy: return -8f;
        case IdeologyType.Liberalism: return -12f;
        default: return 0f;
        }
    }
    private static float PowerScore(KingdomStrategySnapshot s)
    {
        if (s.Ideology == IdeologyType.Militarism && s.AtWar) return 78f;
        if (s.Ideology == IdeologyType.Communism && (s.Food < 200f || s.Stone < 100f)) return 76f;
        if (s.Ideology == IdeologyType.Ethnonationalism && (s.AtWar || s.HostileNeighbors > 0)) return 75f;
        if (s.Ideology == IdeologyType.Democracy && (s.AvgDissent > 0.25f || s.Support < 45f)) return 74f;
        if (s.Ideology == IdeologyType.Tribalism && s.Starving) return 72f;
        return 48f;
    }
}

/// <summary>Игровой слой реализует этот интерфейс через безопасные reflection-вызовы.</summary>
public interface IKingdomActionExecutor
{
    bool TryExecute(KingdomActionPlan plan, KingdomStrategySnapshot snapshot, out string result);
}

public static class KingdomActionExecutor
{
    public static bool TryExecute(IKingdomActionExecutor executor, KingdomActionPlan plan, KingdomStrategySnapshot snapshot, out string result)
    {
        if (executor == null) { result = "API исполнения недоступен"; return false; }
        try { return executor.TryExecute(plan, snapshot, out result); }
        catch (Exception ex) { result = "Ошибка безопасного вызова: " + ex.GetType().Name; return false; }
    }
}
