using System;
using System.Collections.Generic;
using System.Linq;

namespace RealPolitic.Core;

/// <summary>
/// 0.31–0.31.3: Жизненный цикл ранних строев — племён и каганатов, — племенные этнонимы
/// (строго от биома, культуры или названия столицы, без реальных исторических племён)
/// и переход к цивилизации:
/// 1) До 200-го года племена и каганаты спокойно живут своим ранним укладом.
/// 2) В любой год племя или каганат может цивилизоваться мирно, если накопил достаточно
///    ресурсов (золота/благосостояния) и получил международное признание:
///    - до 50-го года — признание КАЖДОЙ живой державы мира;
///    - с 50-го года — признание Совета великих держав.
///    При этом держава сама выбирает себе цивилизованную идеологию и получает её официальное название.
/// 3) После 200-го года народ внутри племени или каганата стремится к цивилизации: растёт
///    недовольство, и дело может дойти до гражданской войны за цивилизацию.
/// 4) После 200-го года ни одна цивилизованная держава не может вернуться ни к племени, ни к каганату.
/// </summary>
public static class TribalCivilization
{
    /// <summary>
    /// Ранние уклады, которым ещё предстоит цивилизоваться. Каганат — полноценная ранняя
    /// державная форма с каганом и курултаем, но не конечная «цивилизованная» цель: как и племя,
    /// он должен со временем перейти к одному из цивилизованных строев.
    /// </summary>
    public static bool NeedsCivilization(IdeologyType ideology)
        => ideology == IdeologyType.Tribalism || ideology == IdeologyType.Khaganate;

    private static readonly string[] ArcticTribes =
    {
        "Мерзлотники", "Тундровики", "Студенецкие", "Зимогоры", "Льдинники", "Морозники", "Снегоходцы", "Беломоры"
    };

    private static readonly string[] ForestTribes =
    {
        "Чащобники", "Лесовики", "Боровчане", "Дубравичи", "Ольховцы", "Кедровники", "Березняки", "Ельники"
    };

    private static readonly string[] PlainsTribes =
    {
        "Луговики", "Степняки", "Разнотравцы", "Вольнопольцы", "Ковыльники", "Равнинники", "Златопольцы", "Медотравцы"
    };

    private static readonly string[] SwampTribes =
    {
        "Трясинники", "Болотники", "Мшаряне", "Камышане", "Топяне", "Моховики", "Осочники", "Заводчане"
    };

    private static readonly string[] MountainTribes =
    {
        "Скальники", "Горцы", "Каменнобродцы", "Хребтовики", "Кремневики", "Утёсники", "Пиковчане", "Рудокопы"
    };

    private static readonly string[] DesertTribes =
    {
        "Барханники", "Песчаники", "Дюнники", "Суховейцы", "Оазисники", "Такырники", "Солнцепоклонники", "Пыльники"
    };

    private static readonly string[] RiverCoastTribes =
    {
        "Прибрежники", "Поречане", "Белобережцы", "Плёсяне", "Заливники", "Волнорезы", "Бродники", "Озёрники"
    };

    private static readonly string[] FallbackBiomeTribes =
    {
        "Чащобники", "Луговики", "Мерзлотники", "Скальники", "Прибрежники", "Трясинники",
        "Барханники", "Лесовики", "Степняки", "Тундровики", "Горцы", "Поречане",
        "Боровчане", "Вольнопольцы", "Зимогоры", "Хребтовики", "Белобережцы", "Мшаряне"
    };

    private static readonly Dictionary<string, string> TribalToToponymMap = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
    {
        ["Мерзлотники"] = "Мерзлотия",
        ["Тундровики"] = "Тундровия",
        ["Студенецкие"] = "Студенец",
        ["Зимогоры"] = "Зимогорье",
        ["Льдинники"] = "Льдиния",
        ["Морозники"] = "Морозия",
        ["Снегоходцы"] = "Снеговия",
        ["Беломоры"] = "Беломорье",
        ["Чащобники"] = "Чащобия",
        ["Лесовики"] = "Лесовия",
        ["Боровчане"] = "Боровия",
        ["Дубравичи"] = "Дубравия",
        ["Ольховцы"] = "Ольховия",
        ["Кедровники"] = "Кедровия",
        ["Березняки"] = "Березнякия",
        ["Ельники"] = "Ельничия",
        ["Луговики"] = "Луговия",
        ["Степняки"] = "Степной Край",
        ["Разнотравцы"] = "Разнотравье",
        ["Вольнопольцы"] = "Вольнополье",
        ["Ковыльники"] = "Ковылия",
        ["Равнинники"] = "Равниния",
        ["Златопольцы"] = "Златополье",
        ["Медотравцы"] = "Медотравье",
        ["Трясинники"] = "Трясиния",
        ["Болотники"] = "Болотный Край",
        ["Мшаряне"] = "Мшария",
        ["Камышане"] = "Камышия",
        ["Топяне"] = "Топия",
        ["Моховики"] = "Моховия",
        ["Осочники"] = "Осочия",
        ["Заводчане"] = "Заводье",
        ["Скальники"] = "Скалия",
        ["Горцы"] = "Нагорье",
        ["Каменнобродцы"] = "Каменнобродье",
        ["Хребтовики"] = "Хребтовия",
        ["Кремневики"] = "Кремневия",
        ["Утёсники"] = "Утёсия",
        ["Пиковчане"] = "Пикогорье",
        ["Рудокопы"] = "Рудногорье",
        ["Барханники"] = "Бархания",
        ["Песчаники"] = "Песчания",
        ["Дюнники"] = "Дюния",
        ["Суховейцы"] = "Суховейск",
        ["Оазисники"] = "Оазисия",
        ["Такырники"] = "Такырия",
        ["Солнцепоклонники"] = "Солнечный Предел",
        ["Пыльники"] = "Пыльноземье",
        ["Прибрежники"] = "Прибрежье",
        ["Поречане"] = "Поречье",
        ["Белобережцы"] = "Белобережье",
        ["Плёсяне"] = "Плёсия",
        ["Заливники"] = "Заливия",
        ["Волнорезы"] = "Волноморье",
        ["Бродники"] = "Бродовия",
        ["Озёрники"] = "Озёрный Край"
    };

    public static bool IsKnownTribalEthnonym(string? name)
    {
        if (string.IsNullOrWhiteSpace(name))
            return false;
        string trimmed = name!.Trim();
        if (TribalToToponymMap.ContainsKey(trimmed))
            return true;
        return trimmed.EndsWith("яне", StringComparison.OrdinalIgnoreCase)
            || trimmed.EndsWith("чане", StringComparison.OrdinalIgnoreCase)
            || trimmed.EndsWith("ецане", StringComparison.OrdinalIgnoreCase)
            || trimmed.EndsWith("цане", StringComparison.OrdinalIgnoreCase)
            || trimmed.EndsWith("шане", StringComparison.OrdinalIgnoreCase)
            || trimmed.EndsWith("жане", StringComparison.OrdinalIgnoreCase)
            || trimmed.EndsWith("ичи", StringComparison.OrdinalIgnoreCase)
            || trimmed.EndsWith("ники", StringComparison.OrdinalIgnoreCase)
            || trimmed.EndsWith("вики", StringComparison.OrdinalIgnoreCase)
            || trimmed.EndsWith("цы", StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Генерирует племенное название (этноним во множественном числе: «Чащобники», «Луговики», «Мерзлотники», «Ольховяне» и т.д.)
    /// строго на основе названия столицы, культуры или биома — без реальных исторических племён.
    /// </summary>
    public static string GenerateTribalName(
        string? capitalName,
        string? biomeId,
        int kingdomId = 1,
        ICollection<string>? existingNames = null,
        string? cultureName = null)
    {
        string cap = (capitalName ?? string.Empty).Trim();
        string biome = (biomeId ?? string.Empty).Trim().ToLowerInvariant();
        string capLower = cap.ToLowerInvariant();

        string[]? pool = null;

        // 1. Определение по ключевым природным корням столицы или биома
        if (ContainsAny(capLower, "древ", "лес", "бор", "рощ", "чащ", "дуб", "ольх", "кедр", "сосн", "берез", "ель")
            || ContainsAny(biome, "forest", "jungle", "birch", "maple", "enchanted"))
        {
            pool = ForestTribes;
        }
        else if (ContainsAny(capLower, "пол", "луг", "степ", "равнин", "злат", "трав", "ковыл")
            || ContainsAny(biome, "grass", "savanna", "clover", "flower"))
        {
            pool = PlainsTribes;
        }
        else if (ContainsAny(capLower, "север", "мороз", "лед", "лёд", "снеж", "студен", "тундр", "зимо", "мерзл")
            || ContainsAny(biome, "permafrost", "snow", "ice", "crystal"))
        {
            pool = ArcticTribes;
        }
        else if (ContainsAny(capLower, "болот", "топ", "мшар", "трясин", "ряс", "камыш", "мох")
            || ContainsAny(biome, "swamp", "mushroom"))
        {
            pool = SwampTribes;
        }
        else if (ContainsAny(capLower, "гор", "кам", "скал", "утёс", "хреб", "кремн", "желез", "пик", "руд")
            || ContainsAny(biome, "hill", "rock", "mountain", "infernal"))
        {
            pool = MountainTribes;
        }
        else if (ContainsAny(capLower, "пес", "дюн", "пуст", "бархан", "солн", "оазис", "сухо")
            || ContainsAny(biome, "desert", "sand", "lemon", "wasteland"))
        {
            pool = DesertTribes;
        }
        else if (ContainsAny(capLower, "мор", "вод", "реч", "плёс", "брод", "яр", "озер", "берег", "залив", "волн")
            || ContainsAny(biome, "ocean", "beach", "river"))
        {
            pool = RiverCoastTribes;
        }

        if (pool != null)
        {
            for (int i = 0; i < pool.Length; i++)
            {
                string candidate = pool[i % pool.Length];
                if (existingNames == null || !existingNames.Contains(candidate))
                {
                    return candidate;
                }
            }
        }

        // 2. Производный этноним от имени культуры (если задана)
        if (!string.IsNullOrWhiteSpace(cultureName))
        {
            string fromCulture = DeriveEthnonymFromToponym(cultureName!);
            if (!string.IsNullOrWhiteSpace(fromCulture) && (existingNames == null || !existingNames.Contains(fromCulture)))
            {
                return fromCulture;
            }
        }

        // 3. Производный этноним напрямую от имени столицы (например: «Ольховск» -> «Ольховяне», «Беловодье» -> «Беловодцы»)
        if (!string.IsNullOrWhiteSpace(cap))
        {
            string derived = DeriveEthnonymFromCapital(cap);
            if (!string.IsNullOrWhiteSpace(derived) && (existingNames == null || !existingNames.Contains(derived)))
            {
                return derived;
            }
        }

        // 4. Запасной природно-географический пул
        for (int i = 0; i < FallbackBiomeTribes.Length; i++)
        {
            string candidate = FallbackBiomeTribes[(Math.Max(0, kingdomId - 1) + i) % FallbackBiomeTribes.Length];
            if (existingNames == null || !existingNames.Contains(candidate))
            {
                return candidate;
            }
        }

        return FallbackBiomeTribes[Math.Max(0, kingdomId - 1) % FallbackBiomeTribes.Length];
    }

    /// <summary>
    /// Образует этноним племени от названия столицы или культуры («Ольховск» -> «Ольховяне», «Беловодье» -> «Беловодцы»,
    /// «Вольноград» -> «Вольноградцы»). Не использует реальные исторические племена.
    /// </summary>
    public static string DeriveEthnonymFromCapital(string capitalName)
    {
        return DeriveEthnonymFromToponym(capitalName);
    }

    public static string DeriveEthnonymFromToponym(string toponym)
    {
        if (string.IsNullOrWhiteSpace(toponym))
            return "Чащобники";

        string clean = toponym.Trim();
        if (IsKnownTribalEthnonym(clean))
            return clean;
        string[] parts = clean.Split(new[] { ' ', '-' }, StringSplitOptions.RemoveEmptyEntries);
        string word = parts[parts.Length - 1];
        string[] suffixes = { "город", "град", "посад", "бург", "полье", "водье", "земье", "озерье", "ск", "цк", "ка", "ово", "ево", "ино", "ье", "ья", "ия", "а", "я", "ы", "и", "о", "е" };
        string stem = word;
        foreach (string suf in suffixes)
        {
            if (stem.Length > suf.Length + 2 && stem.EndsWith(suf, StringComparison.OrdinalIgnoreCase))
            {
                if (suf == "полье") { return stem.Substring(0, stem.Length - 5) + "польцы"; }
                if (suf == "водье") { return stem.Substring(0, stem.Length - 5) + "водцы"; }
                if (suf == "земье") { return stem.Substring(0, stem.Length - 5) + "земцы"; }
                if (suf == "озерье") { return stem.Substring(0, stem.Length - 6) + "озерцы"; }
                if (suf == "град" || suf == "город") { return word + "цы"; }
                stem = stem.Substring(0, stem.Length - suf.Length);
                break;
            }
        }

        if (stem.Length < 2)
            return "Луговики";

        char last = char.ToLowerInvariant(stem[stem.Length - 1]);
        if (last == 'к')
            return stem.Substring(0, stem.Length - 1) + "чане";
        if (last == 'г')
            return stem.Substring(0, stem.Length - 1) + "жане";
        if (last == 'х')
            return stem.Substring(0, stem.Length - 1) + "шане";
        if (last == 'ц' || last == 'ч' || last == 'ш' || last == 'щ' || last == 'ж')
            return stem + "ане";
        if (last == 'д' || last == 'т' || last == 'р')
            return stem + "цы";
        return stem + "яне";
    }

    /// <summary>
    /// Преобразует племенной этноним в базовый топоним страны («Чащобники» -> «Чащобия», «Луговики» -> «Луговия», «Ольховяне» -> «Ольховия»).
    /// </summary>
    public static string GenerateBaseToponym(string? tribalName, string? capitalName = null)
    {
        string t = (tribalName ?? string.Empty).Trim();
        if (!string.IsNullOrEmpty(t) && TribalToToponymMap.TryGetValue(t, out string? mapped))
        {
            return mapped;
        }
        if (t.EndsWith("ники", StringComparison.OrdinalIgnoreCase) && t.Length > 5)
        {
            return t.Substring(0, t.Length - 4) + "ия";
        }
        if (t.EndsWith("вики", StringComparison.OrdinalIgnoreCase) && t.Length > 5)
        {
            return t.Substring(0, t.Length - 2) + "ия";
        }
        if (t.EndsWith("яне", StringComparison.OrdinalIgnoreCase) && t.Length > 4)
        {
            return t.Substring(0, t.Length - 3) + "ия";
        }
        if (t.EndsWith("чане", StringComparison.OrdinalIgnoreCase) && t.Length > 5)
        {
            return t.Substring(0, t.Length - 4) + "ия";
        }
        if (t.EndsWith("ане", StringComparison.OrdinalIgnoreCase) && t.Length > 4)
        {
            return t.Substring(0, t.Length - 3) + "ия";
        }
        if (t.EndsWith("ичи", StringComparison.OrdinalIgnoreCase) && t.Length > 4)
        {
            return t.Substring(0, t.Length - 3) + "ия";
        }
        if (t.EndsWith("цы", StringComparison.OrdinalIgnoreCase) && t.Length > 3)
        {
            return t.Substring(0, t.Length - 2) + "ия";
        }
        if (!string.IsNullOrWhiteSpace(capitalName))
        {
            return capitalName!.Trim();
        }
        return string.IsNullOrEmpty(t) ? "Вельгард" : t;
    }

    /// <summary>
    /// Преобразует племенной этноним в название цивилизованной державы.
    /// Если передана целевая идеология, генерирует полное идеологическое название («Королевство Чащобия», «Вельгардский Каганат» и т.д.).
    /// Если идеология не передана, возвращает базовый цивилизованный топоним («Чащобия», «Луговия», «Ольховия»).
    /// </summary>
    public static string GenerateCivilizedStateName(
        string? tribalName,
        string? capitalName = null,
        IdeologyType? targetIdeology = null,
        int kingdomId = 1,
        int variantSeed = 0,
        ICollection<string>? existingNames = null)
    {
        string baseToponym = GenerateBaseToponym(tribalName, capitalName);
        if (targetIdeology.HasValue && targetIdeology.Value != IdeologyType.Tribalism)
        {
            return IdeologyStateNames.GenerateStateName(
                targetIdeology.Value,
                baseToponym,
                capitalName,
                kingdomId,
                variantSeed,
                existingNames);
        }
        return baseToponym;
    }

    /// <summary>
    /// Проверяет, накопило ли племя достаточно ресурсов для мирного перехода к цивилизации.
    /// </summary>
    public static bool HasResourcesToCivilize(KingdomPolitics state, KingdomInput input, SimParams p)
    {
        if (state == null || !NeedsCivilization(state.Ideology))
            return false;
        if (state.CivilWar != null || state.Starving || input.Starving)
            return false;
        if (state.TribalCivilizationFunded)
            return true;
        if (state.Economy != null && state.Economy.Crisis != EconomicCrisisType.None)
            return false;

        float goldCost = p != null ? p.TribalCivilizationGoldCost : 220f;
        float minProsperity = p != null ? p.TribalCivilizationProsperityMin : 62f;

        bool enoughGold = input.Gold >= goldCost;
        bool enoughProsperity = state.Economy == null || state.Economy.Prosperity >= minProsperity;
        return enoughGold && enoughProsperity;
    }

    /// <summary>
    /// Проверяет международное признание для цивилизации племени или каганата:
    /// - до 50-го года (`year &lt; 50`) требуется признание КАЖДОЙ живой державы мира;
    /// - с 50-го года (`year &gt;= 50`) требуется признание великих держав.
    /// </summary>
    public static bool CheckRecognitionForCivilization(
        PoliticalWorld world,
        KingdomPolitics state,
        KingdomInput input,
        int year,
        out string recognitionSummary,
        out int yesCount,
        out int requiredCount)
    {
        recognitionSummary = string.Empty;
        yesCount = 0;
        requiredCount = 0;
        if (world == null || state == null)
            return false;

        var otherKingdoms = world.Kingdoms
            .Where(k => k.KingdomId != state.KingdomId && k.Population > 0)
            .OrderBy(k => k.KingdomId)
            .ToList();

        if (otherKingdoms.Count == 0)
        {
            otherKingdoms = world.Kingdoms
                .Where(k => k.KingdomId != state.KingdomId)
                .OrderBy(k => k.KingdomId)
                .ToList();
        }

        if (year < 50)
        {
            requiredCount = otherKingdoms.Count;
            if (requiredCount == 0)
            {
                recognitionSummary = "единогласное признание держав мира";
                return true;
            }

            foreach (KingdomPolitics other in otherKingdoms)
            {
                if (IsAtWarWith(input, other.KingdomId))
                    continue;
                int rel = world.GetRulerRelation(other.KingdomId, state.KingdomId);
                int trust = world.DiplomaticMemories.GetTrustScore(other.KingdomId, state.KingdomId, year);
                if (rel >= -10 && trust >= -10)
                {
                    yesCount++;
                }
            }

            bool allApproved = yesCount == requiredCount;
            if (allApproved)
            {
                recognitionSummary = $"единогласное признание всех {requiredCount} держав мира (до 50-го года)";
            }
            return allApproved;
        }
        else
        {
            List<int> greatPowerIds = new List<int>();
            if (world.Governance.Enabled && world.Governance.Recognition.GreatPowers.Count > 0)
            {
                foreach (int gpId in world.Governance.Recognition.GreatPowers)
                {
                    if (gpId != state.KingdomId && world.GetKingdom(gpId) != null)
                    {
                        greatPowerIds.Add(gpId);
                    }
                }
            }

            if (greatPowerIds.Count == 0)
            {
                greatPowerIds = otherKingdoms
                    .Where(k => !NeedsCivilization(k.Ideology))
                    .OrderByDescending(k => k.Population)
                    .ThenBy(k => k.KingdomId)
                    .Take(3)
                    .Select(k => k.KingdomId)
                    .ToList();

                if (greatPowerIds.Count == 0)
                {
                    greatPowerIds = otherKingdoms
                        .OrderByDescending(k => k.Population)
                        .ThenBy(k => k.KingdomId)
                        .Take(3)
                        .Select(k => k.KingdomId)
                        .ToList();
                }
            }

            if (greatPowerIds.Count == 0)
            {
                requiredCount = 0;
                recognitionSummary = "признание Совета великих держав";
                return true;
            }

            requiredCount = (greatPowerIds.Count / 2) + 1;
            foreach (int gpId in greatPowerIds)
            {
                if (IsAtWarWith(input, gpId))
                    continue;
                int rel = world.GetRulerRelation(gpId, state.KingdomId);
                int trust = world.DiplomaticMemories.GetTrustScore(gpId, state.KingdomId, year);
                if (rel >= -10 && trust >= -10)
                {
                    yesCount++;
                }
            }

            bool gpApproved = yesCount >= requiredCount;
            if (gpApproved)
            {
                recognitionSummary = $"признание великих держав ({yesCount} из {greatPowerIds.Count} «за»)";
            }
            return gpApproved;
        }
    }

    /// <summary>
    /// Выбирает цивилизованную идеологию, которую племя или каганат сам принимает при переходе
    /// к цивилизации. Никогда не возвращает ни Tribalism, ни Khaganate: оба строя сами нуждаются
    /// в цивилизации и не могут быть её результатом.
    /// </summary>
    public static IdeologyType ChooseCivilizedIdeology(KingdomPolitics state, KingdomInput input, PoliticalWorld world, int year)
    {
        IdeologyEpoch epoch = EraRules.OfYear(year, world?.Params);
        IdeologyType[] allowed = EraRules.PoolOf(epoch)
            .Where(t => !NeedsCivilization(t))
            .ToArray();

        if (allowed.Length == 0)
            return IdeologyType.Monarchy;

        // 1. Если у раннего строя явно задан желаемый цивилизованный строй и он допустим в эпоху
        if (state.PreferredCivilizedIdeology.HasValue
            && !NeedsCivilization(state.PreferredCivilizedIdeology.Value)
            && allowed.Contains(state.PreferredCivilizedIdeology.Value))
        {
            return state.PreferredCivilizedIdeology.Value;
        }

        // 2. Если среди жителей уже есть сторонники цивилизованных идеологий (перепись)
        IdeologyType bestCensus = IdeologyType.Monarchy;
        int bestCount = 0;
        foreach (KeyValuePair<IdeologyType, int> kv in state.Census)
        {
            if (!NeedsCivilization(kv.Key) && allowed.Contains(kv.Key) && kv.Value > bestCount)
            {
                bestCount = kv.Value;
                bestCensus = kv.Key;
            }
        }
        if (bestCount > 0)
        {
            return bestCensus;
        }

        // 3. По профилю развития самой ранней державы (торговля, армия, степь/воля, эпоха)
        if (epoch >= IdeologyEpoch.EarlyModern)
        {
            if (allowed.Contains(IdeologyType.Empire) && input.Cities != null && input.Cities.Count >= 3)
                return IdeologyType.Empire;
            if (allowed.Contains(IdeologyType.Democracy) && (state.KingdomId % 4 == 0))
                return IdeologyType.Democracy;
            if (allowed.Contains(IdeologyType.Egalitarianism) && (state.KingdomId % 4 == 1))
                return IdeologyType.Egalitarianism;
            if (allowed.Contains(IdeologyType.Liberalism) && (state.KingdomId % 4 == 2))
                return IdeologyType.Liberalism;
        }

        if (epoch >= IdeologyEpoch.Medieval)
        {
            if (allowed.Contains(IdeologyType.MerchantRepublic) && ((world != null && world.Trade.OpenRoutesOf(state.KingdomId) > 0) || state.KingdomId % 5 == 0))
                return IdeologyType.MerchantRepublic;
            if (allowed.Contains(IdeologyType.Aristocracy) && state.KingdomId % 5 == 1)
                return IdeologyType.Aristocracy;
        }

        if (input.Warriors > 0 && input.Population > 0 && input.Warriors * 4 >= input.Population)
        {
            if (allowed.Contains(IdeologyType.Militarism))
                return IdeologyType.Militarism;
        }

        // Воинственный степной каганат, не ставший милитаристским по армии выше,
        // при цивилизации чаще закрепляет власть знати улусов, а не остаётся каганатом.
        if (state.KingdomId % 5 == 2 && allowed.Contains(IdeologyType.Aristocracy))
        {
            return IdeologyType.Aristocracy;
        }

        if (state.KingdomId % 5 == 3 && allowed.Contains(IdeologyType.FreeTerritory))
        {
            return IdeologyType.FreeTerritory;
        }

        if (state.KingdomId % 5 == 4 && allowed.Contains(IdeologyType.Theocracy))
        {
            return IdeologyType.Theocracy;
        }

        return IdeologyType.Monarchy;
    }

    /// <summary>
    /// Пытается провести мирную цивилизацию племени или каганата, если держава накопила ресурсы
    /// и получила требуемое признание.
    /// </summary>
    public static bool TryCivilize(PoliticalWorld world, KingdomPolitics state, KingdomInput input, int year, out IdeologyType newIdeology)
    {
        newIdeology = state?.Ideology ?? IdeologyType.Tribalism;
        if (world == null || state == null || !NeedsCivilization(state.Ideology))
            return false;

        if (!HasResourcesToCivilize(state, input, world.Params))
            return false;

        if (!CheckRecognitionForCivilization(world, state, input, year, out string recognitionSummary, out _, out _))
            return false;

        newIdeology = ChooseCivilizedIdeology(state, input, world, year);
        ApplyCivilizationTransition(world, state, input, newIdeology, recognitionSummary, year);
        return true;
    }

    public static void ApplyCivilizationTransition(
        PoliticalWorld world,
        KingdomPolitics state,
        KingdomInput input,
        IdeologyType newIdeology,
        string recognitionSummary,
        int year)
    {
        IdeologyType oldIdeology = state.Ideology;
        if (NeedsCivilization(newIdeology))
            newIdeology = IdeologyType.Monarchy;

        string oldName = world.KingdomNameOf(state.KingdomId);
        if (string.IsNullOrWhiteSpace(oldName) || oldName.StartsWith("#"))
        {
            string fallbackKind = oldIdeology == IdeologyType.Khaganate ? "Каганат" : "Племя";
            oldName = !string.IsNullOrWhiteSpace(input.Name) ? input.Name : (fallbackKind + " #" + state.KingdomId);
        }

        string capName = input.Cities != null && input.Cities.Count > 0 ? input.Cities[0].Name : string.Empty;
        // У каганата старое имя — уже официальная государственная форма («Державный Улус
        // Вестбренария»), а не этноним. Её надо разобрать до топонима, не подменяя столицей.
        string newStateName = oldIdeology == IdeologyType.Khaganate
            ? IdeologyStateNames.GenerateStateName(newIdeology, oldName, capName, state.KingdomId, year)
            : GenerateCivilizedStateName(oldName, capName, newIdeology, state.KingdomId, year);
        state.HasTribalEthnonym = false;
        world.RememberKingdomName(state.KingdomId, newStateName);
        world.SetPendingRenameOverride(state.KingdomId, newStateName);

        // Оплата высокой стоимости цивилизации
        state.TribalCivilizationFunded = false;
        state.WasCivilized = true;
        state.LastCivilizedYear = year;
        if (state.Economy != null)
        {
            float cost = world.Params != null ? world.Params.TribalCivilizationProsperityCost : 18f;
            state.Economy.Prosperity = Math.Max(35f, state.Economy.Prosperity - cost);
        }

        state.Ideology = newIdeology;
        state.SuccessionLaw = SuccessionLaws.GetDefaultLaw(newIdeology);
        state.Constitution = ConstitutionDefaults.For(newIdeology, EraRules.OfYear(year, world.Params));
        state.Constitution.Electoral = ElectoralDefaults.FromConstitution(state.Constitution);
        if (SuccessionLaws.HasPeriodicCycle(newIdeology))
        {
            state.Election = ElectionSystem.ReformedElectoralState(state.Election, year);
            if (state.Ruler != null)
            {
                state.Election.RecordPresidentTerm(state.Ruler.ActorId, state.Ruler.Name, 1);
            }
        }
        else
        {
            state.Election = null;
        }

        state.Legitimacy = Math.Min(100f, Math.Max(68f, state.Legitimacy + 12f));
        state.AvgDissent = Math.Max(0f, state.AvgDissent - 0.20f);
        state.Mechanics.Reset();

        // Переводим города и перепись на новый цивилизованный строй
        foreach (CityPolitics cp in world.Cities)
        {
            if (cp.KingdomId == state.KingdomId)
            {
                cp.Dominant = newIdeology;
                cp.AvgDissent = Math.Max(0f, cp.AvgDissent - 0.15f);
            }
        }

        string civilizedKey = oldIdeology == IdeologyType.Khaganate
            ? "RP_EV_KHAGANATE_CIVILIZED"
            : "RP_EV_TRIBE_CIVILIZED";
        world.Bus.Publish(new PolEvent(PolEventType.KingdomAdoptedIdeology, civilizedKey)
        {
            Year = year,
            Severity = Severity.Critical,
            KingdomId = state.KingdomId
        }.With("old_name", oldName)
         .With("kingdom", newStateName)
         .With("old", Ideologies.NameKey(oldIdeology))
         .With("new", Ideologies.NameKey(newIdeology))
         .With("recognition", recognitionSummary));
    }

    /// <summary>
    /// Годовой тик раннего строя (метод исторически называется TickTribe):
    /// - В любой год проверяет, может ли племя или каганат мирно цивилизоваться (ресурсы + признание).
    /// - До 200-го года ранние строи спокойно живут без внутреннего давления.
    /// - После 200-го года народ внутри ещё не цивилизовавшейся державы требует цивилизации:
    ///   растёт недовольство, города переходят к цивилизованным идеям, вплоть до гражданской войны.
    /// </summary>
    public static void TickTribe(PoliticalWorld world, KingdomPolitics state, KingdomInput input, int year)
    {
        if (world == null || state == null)
            return;

        if (!NeedsCivilization(state.Ideology))
        {
            state.WasCivilized = true;
            return;
        }

        // 1. Попытка мирной цивилизации при наличии накопленных ресурсов и признания
        if (TryCivilize(world, state, input, year, out _))
            return;

        int declineYear = world.Params != null ? world.Params.TribalDeclineYear : 200;
        if (year < declineYear)
        {
            // До 200-го года племена и каганаты спокойно существуют
            return;
        }

        // 2. После 200-го года люди внутри раннего строя хотят цивилизоваться!
        IdeologyType desired = ChooseCivilizedIdeology(state, input, world, year);
        if (!state.TribalAwakeningAnnounced)
        {
            state.TribalAwakeningAnnounced = true;
            string awakeningKey = state.Ideology == IdeologyType.Khaganate
                ? "RP_EV_KHAGANATE_AWAKENING"
                : "RP_EV_TRIBAL_AWAKENING";
            world.Bus.Publish(new PolEvent(PolEventType.CityIdeologyShift, awakeningKey)
            {
                Year = year,
                Severity = Severity.Major,
                KingdomId = state.KingdomId
            }.With("kingdom", world.KingdomNameOf(state.KingdomId))
             .With("old", Ideologies.NameKey(state.Ideology))
             .With("new", Ideologies.NameKey(desired))
             .With("share", 55));
        }

        // Народ переходит к цивилизованным взглядам, недовольство ранним строем растёт
        int yearsPast = Math.Max(1, year - declineYear + 1);
        float dissentStep = Math.Min(0.08f, 0.035f + yearsPast * 0.002f);
        state.AvgDissent = KingdomSimulation.Clamp(state.AvgDissent + dissentStep, 0f, 1f);
        state.Legitimacy = KingdomSimulation.Clamp(state.Legitimacy - 2.2f, 5f, 100f);
        state.Mechanics.ThreatLevel = KingdomSimulation.Clamp(state.Mechanics.ThreatLevel + 4.0f, 0f, 95f);

        // Сдвигаем идеологию нестоличных городов (и переписи) к выбранному народом цивилизованному строю
        int capId = world.CapitalCityIdOf(state.KingdomId);
        foreach (CityPolitics cp in world.Cities)
        {
            if (cp.KingdomId == state.KingdomId && (cp.CityId != capId || world.CityIdsOf(state.KingdomId).Count == 1))
            {
                cp.Dominant = desired;
                cp.Share = Math.Max(cp.Share, 0.62f);
                cp.AvgDissent = Math.Max(cp.AvgDissent, state.AvgDissent);
            }
        }

        int totalPop = Math.Max(20, state.Population > 0 ? state.Population : input.Population);
        int desiredCount = (int)(totalPop * Math.Min(0.75f, 0.45f + yearsPast * 0.03f));
        int oldOrderCount = Math.Max(1, totalPop - desiredCount);
        state.Census[desired] = desiredCount;
        state.Census[state.Ideology] = oldOrderCount;
    }

    private static bool IsAtWarWith(KingdomInput input, int otherId)
    {
        if (input.Relations == null)
            return false;
        for (int i = 0; i < input.Relations.Count; i++)
        {
            if (input.Relations[i].OtherId == otherId && input.Relations[i].Relation == RelationType.War)
                return true;
        }
        return false;
    }

    private static bool ContainsAny(string text, params string[] needles)
    {
        if (string.IsNullOrEmpty(text))
            return false;
        for (int i = 0; i < needles.Length; i++)
        {
            if (text.IndexOf(needles[i], StringComparison.OrdinalIgnoreCase) >= 0)
                return true;
        }
        return false;
    }
}
