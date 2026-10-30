using RealPolitic.Core;
using Xunit;

namespace RealPolitic.Tests;

public sealed class RecognitionDiplomacyTests
{
    [Fact]
    public void LegacySlotIdentityIsStableAcrossDifferentSaveYearsAndRoundTrips()
    {
        var world = new PoliticalWorld();
        const string slotIdentity = "slot-0123456789abcdef0123456789abcdef";
        Assert.True(RecognitionDiplomacyEngine.EnsureUniversalRecognitionBaseline(
            world, 15, stableWorldKey: slotIdentity));
        Assert.Equal(slotIdentity, world.Governance.WorldKey);

        string saved = SaveData.Write(world);
        var loaded = new PoliticalWorld();
        SaveData.Apply(loaded, saved);
        Assert.False(RecognitionDiplomacyEngine.EnsureUniversalRecognitionBaseline(
            loaded, 28, stableWorldKey: slotIdentity));
        Assert.Equal(slotIdentity, loaded.Governance.WorldKey);
    }

    [Fact]
    public void LegacySaveInitializesFoundingRecognizedStatesAndGreatPowerCouncilWhileNewBreakawayMustSeekRecognition()
    {
        // Имитируем старое сохранение (например, save21 на 511 году), где Governance.Enabled == false,
        // а три существующие державы основаны в 200, 310 и 420 годах.
        var world = new PoliticalWorld();
        var k1 = world.AddKingdom(1, IdeologyType.Monarchy, 200);
        var k2 = world.AddKingdom(2, IdeologyType.Democracy, 310);
        var k3 = world.AddKingdom(3, IdeologyType.MerchantRepublic, 420);
        k1.Census[IdeologyType.Monarchy] = 180;
        k2.Census[IdeologyType.Democracy] = 150;
        k3.Census[IdeologyType.MerchantRepublic] = 120;
        world.SetYear(511);

        Assert.False(world.Governance.Enabled);

        // При первом годовом тике 0.31 автоматически активируется Совет великих держав и признание
        bool initialized = RecognitionDiplomacyEngine.EnsureUniversalRecognitionBaseline(world, 511, "save21");
        Assert.True(initialized);
        Assert.True(world.Governance.Enabled);
        Assert.True(world.Governance.Recognition.IsRecognized(1));
        Assert.True(world.Governance.Recognition.IsRecognized(2));
        Assert.True(world.Governance.Recognition.IsRecognized(3));
        Assert.Equal(3, world.Governance.Recognition.GreatPowers.Count);

        // В 512 году от державы #1 отделяется мятежный край #4 — он рождается НЕПРИЗНАННЫМ
        var k4 = world.AddKingdom(4, IdeologyType.Democracy, 512);
        k4.Census[IdeologyType.Democracy] = 45;
        world.Governance.Recognition.RegisterCountry(4, 512);
        world.Governance.RecordCountryBirth(4, parent: 1, year: 512, lawful: false);

        Assert.False(world.Governance.Recognition.IsRecognized(4));
        Assert.True(world.Governance.Recognition.NeedsRecognitionVote(4));

        // Открываем раунд голосования Совета великих держав и запускаем дипломатическую борьбу за признание
        var ballot = world.Governance.Recognition.OpenRecognitionVote(4, 512, durationYears: 2);
        Assert.NotNull(ballot);

        // Великая держава #3 дружит с метрополией #1, а великая держава #2 близка по строю (демократия) к #4
        world.SetRulerRelation(3, 1, 40);
        RecognitionDiplomacyEngine.Tick(world, 512);

        // Посольство непризнанной республики #4 улучшило отношения с демократической великой державой #2
        Assert.True(world.GetRulerRelation(4, 2) > 0);
        // Протест метрополии #1 ухудшил отношение союзной ей великой державы #3 к мятежному краю #4
        Assert.True(world.GetRulerRelation(3, 4) < 0);

        // Великая держава #2 голосует ЗА признание #4 — метрополия #1 копит обиду на #2, а #4 запоминает помощь
        Assert.True(ballot!.TryCast(2, CivicBallot.Yes, 512));
        RecognitionDiplomacyEngine.Tick(world, 513);

        Assert.Equal(25, world.DiplomaticMemories.GetTrustScore(4, 2, 513));
        Assert.True(world.GetGrievance(1, 2) >= 20f);
    }
}
