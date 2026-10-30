using System;
using System.Collections.Generic;
using System.Linq;
using RealPolitic.Core;
using Xunit;

namespace RealPolitic.Tests;

public class TribalCivilizationTests
{
    private static readonly string[] ForbiddenHistoricalTribes =
    {
        "Древляне", "Поляне", "Инуиты", "Кривичи", "Вятичи", "Радимичи",
        "Дреговичи", "Тиверцы", "Уличи", "Берендеи", "Бужане", "Туареги",
        "Сарматы", "Бодричи", "Лютичи", "Ильменцы"
    };

    [Fact]
    public void TribalNames_AreDerivedStrictlyFromBiomeCultureOrCapital_WithoutHistoricalTribes()
    {
        Assert.Equal("Чащобники", TribalCivilization.GenerateTribalName("Древний Бор", "biome_forest", 1));
        Assert.Equal("Луговики", TribalCivilization.GenerateTribalName("Полянск", "biome_grass", 1));
        Assert.Equal("Мерзлотники", TribalCivilization.GenerateTribalName("Ледяная Тундра", "biome_permafrost", 1));
        Assert.Equal("Трясинники", TribalCivilization.GenerateTribalName("Мшарск", "biome_swamp", 1));
        Assert.Equal("Скальники", TribalCivilization.GenerateTribalName("Каменный Пик", "biome_hill", 1));
        Assert.Equal("Барханники", TribalCivilization.GenerateTribalName("Песчаный Оазис", "biome_desert", 1));
        Assert.Equal("Прибрежники", TribalCivilization.GenerateTribalName("Морской Берег", "biome_ocean", 1));

        // Производный этноним от произвольной столицы и культуры
        string derivedFromCapital = TribalCivilization.DeriveEthnonymFromCapital("Ольховск");
        Assert.Equal("Ольховяне", derivedFromCapital);
        Assert.Equal("Ольховия", TribalCivilization.GenerateCivilizedStateName(derivedFromCapital, "Ольховск"));

        string derivedFromCulture = TribalCivilization.GenerateTribalName("", "", 1, null, "Светлоречье");
        Assert.Equal("Светлоречане", derivedFromCulture);
        Assert.Equal("Альтхартмарчане", TribalCivilization.DeriveEthnonymFromCapital("Альтхартмарчане"));

        // Проверяем, что ни один из реальных исторических этнонимов никогда не генерируется
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        string[] biomes = { "biome_forest", "biome_grass", "biome_permafrost", "biome_swamp", "biome_hill", "biome_desert", "biome_ocean", "" };
        foreach (string b in biomes)
        {
            for (int k = 1; k <= 20; k++)
            {
                string name = TribalCivilization.GenerateTribalName("", b, k, seen);
                seen.Add(name);
                Assert.DoesNotContain(name, ForbiddenHistoricalTribes, StringComparer.OrdinalIgnoreCase);
            }
        }
    }

    [Fact]
    public void EveryIdeology_HasAtLeast20StateNamingPatterns()
    {
        foreach (IdeologyType ideology in Enum.GetValues(typeof(IdeologyType)))
        {
            IReadOnlyList<string> templates = IdeologyStateNames.GetTemplates(ideology);
            Assert.True(templates.Count >= 20, $"Идеология {ideology} имеет только {templates.Count} шаблонов (< 20)");

            List<string> variants = IdeologyStateNames.GetAllVariants(ideology, "Вельгард");
            Assert.True(variants.Count >= 20, $"Идеология {ideology} даёт только {variants.Count} уникальных названий для «Вельгард» (< 20)");
        }

        // Проверяем конкретные примеры названий для новых и существующих идеологий:
        Assert.Contains("Вельгардская Империя", IdeologyStateNames.GetAllVariants(IdeologyType.Empire, "Вельгард"));
        Assert.Contains("Вольноградский Каганат", IdeologyStateNames.GetAllVariants(IdeologyType.Khaganate, "Вольноград"));
        Assert.Contains("Вольная Территория Вольноград", IdeologyStateNames.GetAllVariants(IdeologyType.FreeTerritory, "Вольноград"));
        Assert.Contains("Королевство Серебряный Яр", IdeologyStateNames.GetAllVariants(IdeologyType.Monarchy, "Серебряный Яр"));
        Assert.Contains("Национальное Государство Ольховия", IdeologyStateNames.GetAllVariants(IdeologyType.Ethnonationalism, "Ольховия"));
    }

    [Fact]
    public void EtsaneEthnonym_GeneratesCivilizedToponymAndEmpireName()
    {
        Assert.True(TribalCivilization.IsKnownTribalEthnonym("Добрвецане"));
        Assert.Equal("Добрвеция", TribalCivilization.GenerateBaseToponym("Добрвецане"));
        Assert.Equal("Добрвеция", IdeologyStateNames.ExtractBaseToponym("Добрвецане"));
        Assert.Contains("Добрвецкая Империя", IdeologyStateNames.GetAllVariants(IdeologyType.Empire, "Добрвецане"));
    }

    [Fact]
    public void SuccessfulRevolutionToEmpire_CreatesPendingOfficialRenameForGameLayer()
    {
        var world = new PoliticalWorld { Rng = new FixedRandom(0.0) };
        world.SetYear(220);

        KingdomPolitics state = world.AddKingdom(7, IdeologyType.Tribalism, 1);
        state.HasTribalEthnonym = true;
        state.Census[IdeologyType.Empire] = 900;
        state.Census[IdeologyType.Tribalism] = 100;
        state.Legitimacy = 5f;
        state.AvgDissent = 0.90f;
        state.Revolution = new RevolutionState
        {
            Target = IdeologyType.Empire,
            StartedYear = 219,
            YearsLeft = 1
        };
        world.RememberKingdomName(7, "Добрвецане");
        world.AddCity(701, 7);
        world.AddCity(702, 7);
        world.AddCity(703, 7);

        var input = new KingdomInput
        {
            KingdomId = 7,
            Name = "Добрвецане",
            Population = 1000,
            Warriors = 120,
            Gold = 0f,
            Cities = new List<CityInput>
            {
                new CityInput { CityId = 701, KingdomId = 7, Name = "Добрвец", Population = 500, FoodOk = true, Loyalty = 30 },
                new CityInput { CityId = 702, KingdomId = 7, Name = "Северный Добрвец", Population = 300, FoodOk = true, Loyalty = 35 },
                new CityInput { CityId = 703, KingdomId = 7, Name = "Нижний Добрвец", Population = 200, FoodOk = true, Loyalty = 40 }
            },
            Relations = new List<RelationInfo>()
        };

        world.YearTick(new List<KingdomInput> { input });

        Assert.Equal(IdeologyType.Empire, state.Ideology);
        Assert.True(world.TryGetPendingRenameOverride(7, out string pendingName));
        Assert.Equal(world.KingdomNameOf(7), pendingName);
        Assert.NotEqual("Добрвецане", pendingName);
        // 0.31.18: на карте имя государства — коренное имя вместе с настоящим титулом
        // («Империя Добрвеция», «Держава Добрвеция»), коренное имя при этом сохраняется.
        Assert.Contains("Добрвеция", pendingName);
        Assert.NotEmpty(StateNaming.LabelOf(IdeologyType.Empire, pendingName));
        Assert.Equal("Добрвеция", StateNaming.Root(pendingName));
        Assert.Contains("Добрвец", pendingName);

        world.CompletePendingRenameOverride(7, "Добрвецане");
        Assert.True(world.TryGetPendingRenameOverride(7, out _));
        world.CompletePendingRenameOverride(7, pendingName);
        Assert.False(world.TryGetPendingRenameOverride(7, out _));
    }

    [Fact]
    public void FreeTerritory_AtamanRulesWithoutTermLimit_UntilDismissedByResidents()
    {
        var world = new PoliticalWorld();
        world.SetYear(50);

        KingdomPolitics ft = world.AddKingdom(1, IdeologyType.FreeTerritory, 1);
        ft.Ruler = new RulerRef { ActorId = 501, Name = "Нестор Вольный", KingdomId = 1, SinceYear = 10, Age = 35 };
        ft.Support = 75f;
        ft.Legitimacy = 75f;
        ft.AvgDissent = 0.08f;

        // 1. В вольной территории нет автоматического 15-летнего избирательного цикла:
        Assert.False(SuccessionLaws.HasPeriodicCycle(IdeologyType.FreeTerritory));

        var input = new KingdomInput
        {
            KingdomId = 1,
            Name = "Вольная Территория Вольноград",
            Population = 150,
            Warriors = 30,
            Ruler = ft.Ruler,
            Cities = new List<CityInput> { new CityInput { CityId = 10, KingdomId = 1, Name = "Вольноград", Population = 150, FoodOk = true } },
            Relations = new List<RelationInfo>()
        };

        // За 40 лет (больше двух 15-летних сроков) довольные жители НЕ меняют атамана по таймеру:
        for (int y = 50; y <= 90; y++)
        {
            world.SetYear(y);
            KingdomSimulation.TickFreeTerritoryAtaman(ft, input, world.Bus, y, new Random(y));
        }
        Assert.NotNull(ft.Ruler);
        Assert.Equal(501, ft.Ruler!.ActorId);

        // 2. Атаман может избираться бесконечное число раз (более 3 сроков подряд или непоследовательно):
        ft.Election = new ElectionState();
        ft.Election.RecordPresidentTerm(777, "Григорий Кремневой", 5); // уже 5 сроков!
        ElectionSystem.CandidateResolver = (kId, exclId) => new ElectionSystem.ElectionCandidate
        {
            ActorId = 777,
            Name = "Григорий Кремневой",
            Age = 44,
            IsFemale = false
        };
        try
        {
            ft.Ruler = null;
            bool elected6thTime = ElectionSystem.ElectAtaman(ft, 92, world.Bus, new Random(1));
            Assert.True(elected6thTime);
            Assert.NotNull(ft.Ruler);
            Assert.Equal(777, ft.Ruler!.ActorId);
            Assert.True(ft.Election.TermsServed >= 6);
        }
        finally
        {
            ElectionSystem.CandidateResolver = null;
        }

        // 3. Когда поддержка жителей падает, сами жители вольной территории снимают атамана на вольном круге:
        ft.Support = 25f;
        ft.AvgDissent = 0.65f;
        bool dismissed = KingdomSimulation.TickFreeTerritoryAtaman(ft, input, world.Bus, 100, new Random(2));
        Assert.True(dismissed);
        Assert.Contains(world.Bus.OfType(PolEventType.Impeached), e => e.LocKey == "RP_EV_ATAMAN_DISMISSED");
        Assert.NotNull(ft.Ruler);
        Assert.NotEqual(777, ft.Ruler!.ActorId);
    }

    [Fact]
    public void BeforeYear50_PeacefulCivilization_RequiresRecognitionOfEveryState()
    {
        var world = new PoliticalWorld();
        world.SetYear(35);

        KingdomPolitics tribe = world.AddKingdom(1, IdeologyType.Tribalism, 1);
        tribe.Economy = new EconomyState { Prosperity = 70f };
        string assigned = world.AssignTribalName(tribe, "Древний Бор", "biome_forest");
        Assert.Equal("Чащобники", assigned);
        Assert.True(tribe.HasTribalEthnonym);

        KingdomPolitics state2 = world.AddKingdom(2, IdeologyType.Monarchy, 1);
        state2.Census[IdeologyType.Monarchy] = 100;
        KingdomPolitics state3 = world.AddKingdom(3, IdeologyType.Theocracy, 1);
        state3.Census[IdeologyType.Theocracy] = 100;
        KingdomPolitics state4 = world.AddKingdom(4, IdeologyType.Aristocracy, 1);
        state4.Census[IdeologyType.Aristocracy] = 100;

        var input = new KingdomInput
        {
            KingdomId = 1,
            Name = "Чащобники",
            Population = 120,
            Warriors = 25,
            Gold = 300f,
            Cities = new List<CityInput>
            {
                new CityInput { CityId = 10, KingdomId = 1, Name = "Древний Бор", BiomeId = "biome_forest", Population = 120, FoodOk = true }
            },
            Relations = new List<RelationInfo>()
        };

        // Если хотя бы ОДНА держава (№4) враждебна до 50-го года — признания КАЖДОЙ державы нет!
        world.SetRulerRelation(2, 1, 20);
        world.SetRulerRelation(3, 1, 15);
        world.SetRulerRelation(4, 1, -35);

        bool civilizedWithOneHostile = TribalCivilization.TryCivilize(world, tribe, input, 35, out _);
        Assert.False(civilizedWithOneHostile);
        Assert.Equal(IdeologyType.Tribalism, tribe.Ideology);
        Assert.Equal("Чащобники", world.KingdomNameOf(1));

        // Когда ВСЕ державы мира признают племя (до 50-го года) — цивилизация проходит успешно!
        world.SetRulerRelation(4, 1, 10);
        bool civilizedUnanimous = TribalCivilization.TryCivilize(world, tribe, input, 35, out IdeologyType chosenIdeology);
        Assert.True(civilizedUnanimous);
        Assert.NotEqual(IdeologyType.Tribalism, chosenIdeology);
        Assert.Equal(chosenIdeology, tribe.Ideology);
        Assert.NotEqual("Чащобники", world.KingdomNameOf(1));
        Assert.False(tribe.HasTribalEthnonym);
        Assert.True(tribe.WasCivilized);
    }

    [Fact]
    public void AfterYear50_PeacefulCivilization_RequiresGreatPowersRecognition()
    {
        var world = new PoliticalWorld();
        world.SetYear(80);

        KingdomPolitics tribe = world.AddKingdom(1, IdeologyType.Tribalism, 10);
        tribe.Economy = new EconomyState { Prosperity = 72f };
        tribe.PreferredCivilizedIdeology = IdeologyType.MerchantRepublic;
        world.AssignTribalName(tribe, "Полянск", "biome_grass");
        Assert.Equal("Луговики", world.KingdomNameOf(1));

        for (int id = 2; id <= 5; id++)
        {
            KingdomPolitics k = world.AddKingdom(id, IdeologyType.Monarchy, 1);
            int pop = id <= 4 ? 300 : 40;
            k.Census[IdeologyType.Monarchy] = pop;
        }

        var input = new KingdomInput
        {
            KingdomId = 1,
            Name = "Луговики",
            Population = 140,
            Warriors = 20,
            Gold = 280f,
            Cities = new List<CityInput>
            {
                new CityInput { CityId = 11, KingdomId = 1, Name = "Полянск", BiomeId = "biome_grass", Population = 140, FoodOk = true }
            },
            Relations = new List<RelationInfo>()
        };

        world.SetRulerRelation(2, 1, -40);
        world.SetRulerRelation(3, 1, -40);
        world.SetRulerRelation(4, 1, 25);
        world.SetRulerRelation(5, 1, -60);

        Assert.False(TribalCivilization.TryCivilize(world, tribe, input, 80, out _));

        world.SetRulerRelation(2, 1, 20);
        Assert.True(TribalCivilization.TryCivilize(world, tribe, input, 80, out IdeologyType newIdeology));
        Assert.Equal(IdeologyType.MerchantRepublic, newIdeology);
        Assert.Equal(IdeologyType.MerchantRepublic, tribe.Ideology);
        Assert.NotEqual("Луговики", world.KingdomNameOf(1));
    }

    [Fact]
    public void UntilYear200_TribesLivePeacefully_AfterYear200_PeopleDemandCivilizationUpToCivilWar()
    {
        var world = new PoliticalWorld();
        KingdomPolitics tribe = world.AddKingdom(1, IdeologyType.Tribalism, 1);
        world.AssignTribalName(tribe, "Ледяная Бухта", "biome_permafrost");
        world.AddCity(101, 1);
        world.AddCity(102, 1);

        var input = new KingdomInput
        {
            KingdomId = 1,
            Name = world.KingdomNameOf(1),
            Population = 180,
            Warriors = 30,
            Gold = 20f,
            Cities = new List<CityInput>
            {
                new CityInput { CityId = 101, KingdomId = 1, Name = "Ледяная Бухта", BiomeId = "biome_permafrost", Population = 100, FoodOk = true, Loyalty = 80 },
                new CityInput { CityId = 102, KingdomId = 1, Name = "Морозный Яр", BiomeId = "biome_permafrost", Population = 80, FoodOk = true, Loyalty = 45 }
            },
            Relations = new List<RelationInfo>()
        };

        for (int y = 150; y < 200; y++)
        {
            world.SetYear(y);
            TribalCivilization.TickTribe(world, tribe, input, y);
        }
        Assert.False(tribe.TribalAwakeningAnnounced);
        Assert.Equal(0f, tribe.AvgDissent);
        Assert.Equal(IdeologyType.Tribalism, tribe.Ideology);

        bool civilWarStarted = false;
        var rng = new Random(5);
        for (int y = 200; y <= 220; y++)
        {
            world.SetYear(y);
            TribalCivilization.TickTribe(world, tribe, input, y);
            if (CivilWars.TryStart(tribe, input, world.Params, world.Bus, y, rng))
            {
                civilWarStarted = true;
                break;
            }
        }

        Assert.True(tribe.TribalAwakeningAnnounced);
        Assert.True(tribe.AvgDissent >= 0.30f);
        Assert.True(civilWarStarted, "После 200-го года отказ племени от цивилизации должен приводить к гражданской войне за цивилизацию");
        Assert.NotNull(tribe.CivilWar);
        Assert.NotEqual(IdeologyType.Tribalism, tribe.CivilWar!.RebelIdeology);
    }

    [Fact]
    public void Khaganate_CanPeacefullyCivilize_WithResourcesAndRecognition_PreservingToponym()
    {
        var world = new PoliticalWorld();
        world.SetYear(35);

        KingdomPolitics khaganate = world.AddKingdom(1, IdeologyType.Khaganate, 1);
        khaganate.Economy = new EconomyState { Prosperity = 74f };
        khaganate.PreferredCivilizedIdeology = IdeologyType.Monarchy;
        khaganate.Census[IdeologyType.Khaganate] = 220;
        world.RememberKingdomName(1, "Державный Улус Вестбренария");

        KingdomPolitics state2 = world.AddKingdom(2, IdeologyType.Monarchy, 1);
        state2.Census[IdeologyType.Monarchy] = 150;
        KingdomPolitics state3 = world.AddKingdom(3, IdeologyType.Theocracy, 1);
        state3.Census[IdeologyType.Theocracy] = 140;
        KingdomPolitics state4 = world.AddKingdom(4, IdeologyType.Aristocracy, 1);
        state4.Census[IdeologyType.Aristocracy] = 130;

        var input = new KingdomInput
        {
            KingdomId = 1,
            Name = "Державный Улус Вестбренария",
            Population = 220,
            Warriors = 55,
            Gold = 320f,
            Cities = new List<CityInput>
            {
                new CityInput { CityId = 10, KingdomId = 1, Name = "Альцхатис", Population = 220, FoodOk = true }
            },
            Relations = new List<RelationInfo>()
        };

        world.SetRulerRelation(2, 1, 20);
        world.SetRulerRelation(3, 1, 15);
        world.SetRulerRelation(4, 1, -35);
        Assert.False(TribalCivilization.TryCivilize(world, khaganate, input, 35, out _));
        Assert.Equal(IdeologyType.Khaganate, khaganate.Ideology);

        world.SetRulerRelation(4, 1, 10);
        Assert.True(TribalCivilization.TryCivilize(world, khaganate, input, 35, out IdeologyType chosen));
        Assert.Equal(IdeologyType.Monarchy, chosen);
        Assert.Equal(IdeologyType.Monarchy, khaganate.Ideology);
        Assert.True(khaganate.WasCivilized);

        string newName = world.KingdomNameOf(1);
        // 0.31.12: в игре государство называется коренным именем без ярлыка строя.
        Assert.DoesNotContain("Улус", newName);
        Assert.DoesNotContain("Каганат", newName);
        Assert.DoesNotContain("Альцхатис", newName);
        Assert.Contains("Вестбренар", newName);
        Assert.True(world.TryGetPendingRenameOverride(1, out string pendingName));
        Assert.Equal(newName, pendingName);
        Assert.Contains(world.Bus.OfType(PolEventType.KingdomAdoptedIdeology),
            e => e.LocKey == "RP_EV_KHAGANATE_CIVILIZED");
    }

    [Fact]
    public void UntilYear200_KhaganatesLivePeacefully_AfterYear200_PeopleDemandCivilizationUpToCivilWar()
    {
        var world = new PoliticalWorld();
        KingdomPolitics khaganate = world.AddKingdom(1, IdeologyType.Khaganate, 1);
        world.RememberKingdomName(1, "Державный Улус Вестбренария");
        world.AddCity(101, 1);
        world.AddCity(102, 1);

        var input = new KingdomInput
        {
            KingdomId = 1,
            Name = world.KingdomNameOf(1),
            Population = 260,
            Warriors = 70,
            Gold = 20f,
            Cities = new List<CityInput>
            {
                new CityInput { CityId = 101, KingdomId = 1, Name = "Альцхатис", Population = 150, FoodOk = true, Loyalty = 80 },
                new CityInput { CityId = 102, KingdomId = 1, Name = "Вестбренар", Population = 110, FoodOk = true, Loyalty = 45 }
            },
            Relations = new List<RelationInfo>()
        };

        for (int y = 150; y < 200; y++)
        {
            world.SetYear(y);
            TribalCivilization.TickTribe(world, khaganate, input, y);
        }
        Assert.False(khaganate.TribalAwakeningAnnounced);
        Assert.Equal(0f, khaganate.AvgDissent);
        Assert.Equal(IdeologyType.Khaganate, khaganate.Ideology);

        bool civilWarStarted = false;
        var rng = new Random(5);
        for (int y = 200; y <= 220; y++)
        {
            world.SetYear(y);
            TribalCivilization.TickTribe(world, khaganate, input, y);
            if (CivilWars.TryStart(khaganate, input, world.Params, world.Bus, y, rng))
            {
                civilWarStarted = true;
                break;
            }
        }

        Assert.True(khaganate.TribalAwakeningAnnounced);
        Assert.True(khaganate.AvgDissent >= 0.30f);
        Assert.True(khaganate.Census.ContainsKey(IdeologyType.Khaganate));
        Assert.Contains(world.Bus.OfType(PolEventType.CityIdeologyShift),
            e => e.LocKey == "RP_EV_KHAGANATE_AWAKENING");
        Assert.True(civilWarStarted, "После 200-го года отказ каганата от цивилизации должен приводить к гражданской войне за цивилизацию");
        Assert.NotNull(khaganate.CivilWar);
        Assert.False(TribalCivilization.NeedsCivilization(khaganate.CivilWar!.RebelIdeology));
    }

    [Fact]
    public void Khaganate_IsNeverACivilizationTarget_AndCivilizedStateCannotRevertToItAfterYear200()
    {
        var world = new PoliticalWorld();
        world.SetYear(250);

        KingdomPolitics tribe = world.AddKingdom(1, IdeologyType.Tribalism, 1);
        tribe.PreferredCivilizedIdeology = IdeologyType.Khaganate;
        tribe.Census[IdeologyType.Khaganate] = 900;
        tribe.Census[IdeologyType.Tribalism] = 100;
        var tribeInput = new KingdomInput
        {
            KingdomId = 1,
            Name = "Степняки",
            Population = 1000,
            Warriors = 500,
            Cities = new List<CityInput> { new CityInput { CityId = 10, KingdomId = 1, Population = 1000, FoodOk = true } },
            Relations = new List<RelationInfo>()
        };
        IdeologyType civilizationTarget = TribalCivilization.ChooseCivilizedIdeology(tribe, tribeInput, world, 250);
        Assert.False(TribalCivilization.NeedsCivilization(civilizationTarget));

        IdeologyType afterSuccession = ActorSimulation.SuccessorIdeology(
            IdeologyType.Monarchy,
            IdeologyType.Khaganate,
            EraRules.OfYear(250, world.Params),
            year: 250);
        Assert.Equal(IdeologyType.Monarchy, afterSuccession);

        KingdomPolitics egal = world.AddKingdom(2, IdeologyType.Egalitarianism, 50);
        egal.Census[IdeologyType.Khaganate] = 500;
        egal.Census[IdeologyType.Egalitarianism] = 50;
        egal.Legitimacy = 80f;
        var egalInput = new KingdomInput
        {
            KingdomId = 2,
            Name = "Вольная Земля",
            Population = 550,
            Cities = new List<CityInput> { new CityInput { CityId = 20, KingdomId = 2, Population = 550, FoodOk = true } },
            Relations = new List<RelationInfo>()
        };
        Assert.False(IdeologyPowers.Situational(egal, egalInput, 250));
        IdeologyPowers.Tick(egal, egalInput, 250, world.Bus, new Random(1));
        Assert.NotEqual(IdeologyType.Khaganate, egal.Ideology);

        KingdomPolitics mon = world.AddKingdom(3, IdeologyType.Monarchy, 50);
        mon.Census[IdeologyType.Khaganate] = 800;
        mon.Census[IdeologyType.Monarchy] = 50;
        mon.Legitimacy = 10f;
        mon.AvgDissent = 0.90f;
        KingdomSimulation.TickRevolution(mon, world.Params, world.Bus, 250, new Random(1), null);
        Assert.Null(mon.Revolution);
        Assert.Equal(IdeologyType.Monarchy, mon.Ideology);
    }

    [Fact]
    public void AfterYear200_CivilizedState_CannotRevertToTribalism()
    {
        var world = new PoliticalWorld();
        world.SetYear(250);

        IdeologyType afterSuccession = ActorSimulation.SuccessorIdeology(
            IdeologyType.Monarchy,
            IdeologyType.Tribalism,
            EraRules.OfYear(250, world.Params),
            year: 250);
        Assert.Equal(IdeologyType.Monarchy, afterSuccession);

        KingdomPolitics egal = world.AddKingdom(1, IdeologyType.Egalitarianism, 50);
        egal.Census[IdeologyType.Tribalism] = 500;
        egal.Census[IdeologyType.Egalitarianism] = 50;
        egal.Legitimacy = 80f;
        var egalInput = new KingdomInput
        {
            KingdomId = 1,
            Name = "Вольная Земля",
            Population = 550,
            Cities = new List<CityInput> { new CityInput { CityId = 1, KingdomId = 1, Population = 550, FoodOk = true } },
            Relations = new List<RelationInfo>()
        };
        Assert.False(IdeologyPowers.Situational(egal, egalInput, 250));
        IdeologyPowers.Tick(egal, egalInput, 250, world.Bus, new Random(1));
        Assert.NotEqual(IdeologyType.Tribalism, egal.Ideology);

        KingdomPolitics mon = world.AddKingdom(2, IdeologyType.Monarchy, 50);
        mon.Census[IdeologyType.Tribalism] = 800;
        mon.Census[IdeologyType.Monarchy] = 50;
        mon.Legitimacy = 10f;
        mon.AvgDissent = 0.90f;
        KingdomSimulation.TickRevolution(mon, world.Params, world.Bus, 250, new Random(1), null);
        Assert.Null(mon.Revolution);
        Assert.Equal(IdeologyType.Monarchy, mon.Ideology);
    }

    private sealed class FixedRandom : Random
    {
        private readonly double _value;

        public FixedRandom(double value)
        {
            _value = value;
        }

        public override double NextDouble() => _value;

        public override int Next(int maxValue) => 0;
    }
}
