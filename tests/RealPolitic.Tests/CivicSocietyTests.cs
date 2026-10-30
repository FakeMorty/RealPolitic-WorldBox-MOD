using RealPolitic.Core;
using Xunit;

namespace RealPolitic.Tests;

public sealed class CivicSocietyTests
{
    [Fact]
    public void ParliamentaryRepublicUpdatesRulingPartyFromCensus()
    {
        var world = new PoliticalWorld();
        var state = world.AddKingdom(1, IdeologyType.Democracy, 10);
        state.Census[IdeologyType.Liberalism] = 65;
        state.Census[IdeologyType.Democracy] = 35;

        CivicSocietyEngine.UpdateParties(state, world, 100);
        Assert.Equal(2, state.Parties.Count);
        Assert.Equal(IdeologyType.Liberalism, state.Parties[0].Ideology);
        Assert.Equal(65, state.Parties[0].SeatsPercent);
        Assert.True(state.Parties[0].IsRuling);
    }

    [Fact]
    public void ReformerRulerConcedesToAbolishSecretPoliceUltimatum()
    {
        var world = new PoliticalWorld();
        var state = world.AddKingdom(1, IdeologyType.Monarchy, 10);
        state.Constitution = ConstitutionDefaults.For(IdeologyType.Monarchy, IdeologyEpoch.Modern);
        state.Constitution.Watch = WatchLaw.SecretPolice;
        state.AvgDissent = 0.45f;
        state.Support = 42f;
        state.Personality = new RulerPersonality { Trait = RulerTrait.Just, Ambition = 40f };

        // Тик 1 (через 50 лет после основания государства): движение возникает и подаёт петицию
        CivicSocietyEngine.TickMovements(state, world, 60);
        Assert.NotNull(state.ActiveMovement);
        Assert.Equal(CivicDemandType.AbolishSecretPolice, state.ActiveMovement!.Demand);

        // Тик 2: поддержка переваливает за порог ультиматума, реформатор уступает
        CivicSocietyEngine.TickMovements(state, world, 61);
        Assert.Null(state.ActiveMovement);
        Assert.Equal(WatchLaw.Patrol, state.Constitution.Watch);
        Assert.True(state.Support > 42f);
    }

    [Fact]
    public void YoungStateUnder50YearsDoesNotAgitateAgainstGovernment()
    {
        var world = new PoliticalWorld();
        var state = world.AddKingdom(1, IdeologyType.Monarchy, 10);
        state.Constitution = ConstitutionDefaults.For(IdeologyType.Monarchy, IdeologyEpoch.Modern);
        state.Constitution.Watch = WatchLaw.SecretPolice;
        state.AvgDissent = 0.55f;
        state.Support = 30f;
        state.Legitimacy = 15f;
        state.Census[IdeologyType.Democracy] = 80;
        state.Census[IdeologyType.Monarchy] = 20;

        var input = new KingdomInput
        {
            KingdomId = 1,
            Name = "Молодая Держава",
            Population = 100,
            Warriors = 20,
            Cities = new System.Collections.Generic.List<CityInput>
            {
                new CityInput { CityId = 1, KingdomId = 1, Population = 50, Loyalty = 40 },
                new CityInput { CityId = 2, KingdomId = 1, Population = 50, Loyalty = 30 }
            }
        };

        // В первые 50 лет после основания (годы 10..59, возраст 0..49 лет) народ не бузит на власть:
        for (int year = 10; year < 60; year++)
        {
            CivicSocietyEngine.TickMovements(state, world, year);
            Assert.Null(state.ActiveMovement);
            Assert.False(CivilWars.CanStart(state, input, world.Params, year));
            KingdomSimulation.TickRevolution(state, world.Params, world.Bus, year, new System.Random(1), null);
            Assert.Null(state.Revolution);
        }

        // На 50-й год устойчивого существования (год 60, возраст 50 лет) ограничения снимаются:
        Assert.True(CivilWars.CanStart(state, input, world.Params, 60));
        CivicSocietyEngine.TickMovements(state, world, 60);
        Assert.NotNull(state.ActiveMovement);
    }

    [Fact]
    public void WarmongerAutocratSuppressesUltimatumByForce()
    {
        var world = new PoliticalWorld();
        var state = world.AddKingdom(1, IdeologyType.Militarism, 10);
        state.Constitution = ConstitutionDefaults.For(IdeologyType.Militarism, IdeologyEpoch.Modern);
        state.Constitution.Watch = WatchLaw.SecretPolice;
        state.AvgDissent = 0.30f;
        state.Support = 48f;
        state.Legitimacy = 80f;
        state.Personality = new RulerPersonality { Trait = RulerTrait.Conqueror, Ambition = 80f };

        CivicSocietyEngine.TickMovements(state, world, 60);
        Assert.NotNull(state.ActiveMovement);
        state.ActiveMovement!.SupportShare = 0.41f; // Порог ультиматума

        float beforeThreat = state.Mechanics.ThreatLevel;
        CivicSocietyEngine.TickMovements(state, world, 61);
        Assert.Null(state.ActiveMovement);
        Assert.Equal(WatchLaw.SecretPolice, state.Constitution.Watch);
        Assert.True(state.Mechanics.ThreatLevel > beforeThreat);
    }

    [Fact]
    public void SaveDataRoundtripPreservesActiveMovementAndParties()
    {
        var world = new PoliticalWorld();
        var state = world.AddKingdom(1, IdeologyType.Democracy, 10);
        state.ActiveMovement = new CivicMovement
        {
            Demand = CivicDemandType.LowerTaxesAndAusterity,
            Stage = CivicMovementStage.PetitionSubmitted,
            StartedYear = 80,
            SupportShare = 0.34f,
            LeaderName = "Артём"
        };
        state.Parties.Add(new PoliticalParty
        {
            Ideology = IdeologyType.Democracy,
            NameKey = "RP28_PARTY_DEMOCRATIC",
            SeatsPercent = 58,
            IsRuling = true
        });

        string json = SaveData.Write(world);
        var restored = new PoliticalWorld();
        SaveData.Apply(restored, json);

        var rk = restored.GetKingdom(1);
        Assert.NotNull(rk?.ActiveMovement);
        Assert.Equal(CivicDemandType.LowerTaxesAndAusterity, rk!.ActiveMovement!.Demand);
        Assert.Single(rk.Parties);
        Assert.Equal(58, rk.Parties[0].SeatsPercent);
    }

    [Fact]
    public void YearTickPreservesLiveCensusAndVariesUltimatumSupportInsteadOfConstant42Percent()
    {
        var world = new PoliticalWorld();
        world.SetBaseline(true);
        world.SetYear(65);

        // В живой игре WorldBox массив PoliticalWorld._actors пуст, а перепись (Census),
        // поддержка (Support) и недовольство (AvgDissent) собираются из живых юнитов.
        var k1 = world.AddKingdom(1, IdeologyType.Monarchy, 1);
        k1.Census[IdeologyType.Monarchy] = 78;
        k1.Census[IdeologyType.Democracy] = 22;
        k1.Support = 78f;
        k1.AvgDissent = 0.05f;

        var k2 = world.AddKingdom(2, IdeologyType.Monarchy, 1);
        k2.Constitution = ConstitutionDefaults.For(IdeologyType.Monarchy, IdeologyEpoch.Modern);
        k2.Constitution.Watch = WatchLaw.SecretPolice;
        k2.Census[IdeologyType.Monarchy] = 35;
        k2.Census[IdeologyType.Democracy] = 65;
        k2.Support = 35f;
        k2.AvgDissent = 0.58f;

        world.YearTick(new System.Collections.Generic.List<KingdomInput>
        {
            new KingdomInput { KingdomId = 1, Name = "Благополучная", Population = 100 },
            new KingdomInput { KingdomId = 2, Name = "Кризисная", Population = 100 }
        });

        // Перепись и реальная поддержка не сбрасываются в 0 при пустом _actors:
        Assert.Equal(100, k1.Population);
        Assert.True(k1.Support > 60f);
        Assert.Null(k1.ActiveMovement); // При поддержке 78% и низком недовольстве петиций нет

        // В кризисной державе движение возникает с процентом, зависящим от реального недовольства:
        Assert.NotNull(k2.ActiveMovement);
        int initialPct = (int)System.Math.Round(k2.ActiveMovement!.SupportShare * 100f);
        Assert.NotEqual(36, initialPct);
    }
}
