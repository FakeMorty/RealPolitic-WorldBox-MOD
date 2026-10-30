using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.Json;
using System.Text.RegularExpressions;
using RealPolitic.Core;
using Xunit;

// Test-only stand-in for the game's missing-key behaviour. Not included in release DLL.
public static class LocalizedTextManager
{
    public static string getText(string key) => key == "test_translation" ? "Перевод" : key;
}
namespace RealPolitic.Tests
{
public sealed class ChronicleReviewRegressionTests : IDisposable
{
    private sealed class NeighbourKingdom : Kingdom
    {
        public List<Kingdom> neighbours_kingdoms;
        public bool isAtWarWith(Kingdom other) => true;
    }
    [Fact]
    public void MissingTranslation_UsesReadableFallback()
    { Assert.Equal("Человекочитаемый текст", GameText.Localized("RP_MISSING", "Человекочитаемый текст")); }
    [Fact]
    public void ExistingTranslation_IsPreserved()
    { Assert.Equal("Перевод", GameText.Localized("test_translation", "fallback")); }
    [Fact]
    public void MissingTranslation_CanStillReturnKeyWhenExplicitlyRequested()
    { Assert.Equal("RP_MISSING", GameText.Localized("RP_MISSING")); }
    [Fact]
    public void RemovedKingdom_IsLoggedOnce_WithItsSavedName()
    {
        var world = new PoliticalWorld(); world.AddKingdom(77, IdeologyType.Theocracy, 1); world.SetYear(112);
        var names = (Dictionary<int,string>)typeof(PoliticalWorld).GetField("_kingdomNames", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(world);
        names[77] = "Кашу";
        world.RemoveKingdom(77); world.RemoveKingdom(77);
        var evt = Assert.Single(world.Bus.OfType(PolEventType.KingdomDisappeared));
        Assert.Equal("Кашу", evt.Get("kingdom")); Assert.Equal(112, evt.Year);
        Assert.Null(world.GetKingdom(77));
    }
    [Fact]
    public void WorldReset_IsNotReportedAsDeathOfEveryState()
    {
        var world = new PoliticalWorld(); world.AddKingdom(1, IdeologyType.Monarchy, 1); world.Clear();
        Assert.Empty(world.Bus.OfType(PolEventType.KingdomDisappeared));
    }
    [Fact]
    public void MissingLegacyDiplomacy_DoesNotHideNeighboursOrWar()
    {
        var other = new Kingdom { id = 2, cities = new List<City>() };
        var source = new NeighbourKingdom { id = 1, neighbours_kingdoms = new List<Kingdom> { other }, cities = new List<City>() };
        var map = new MapBox { kingdoms = new KingdomManager { list = new List<Kingdom> { source, other } } };
        var relations = new List<RelationInfo>();
        GameRefs.CollectRelations(map, source, relations);
        var relation = Assert.Single(relations);
        Assert.True(relation.Adjacent); Assert.Equal(RelationType.War, relation.Relation);
    }
    [Fact]
    public void NamelessLiveCandidate_IsNotPublishedAsWinner()
    {
        var state = new KingdomPolitics { KingdomId = 1, Election = new ElectionState() };
        ElectionSystem.CandidateResolver = (_,__) => new ElectionSystem.ElectionCandidate { ActorId = 99, Age = 35, Name = " " };
        Assert.Equal(0, ElectionSystem.ResolveCandidate(state, 1, new Random(1)).ActorId);
    }
    [Theory]
    [InlineData(false, "RP_DEATH_OLDAGE")]
    [InlineData(true, "RP_DEATH_OLDAGE_F")]
    public void DeathGender_UsesRecordedSex_NotName(bool female, string expected)
    { var rec = new DeathRecord { ActorName = "Франеттон", Cause = DeathCause.OldAge, WasFemale = female }; Assert.Equal(expected, rec.LocKeyFor(rec.WasFemale)); }
    [Theory]
    [InlineData("ru")]
    [InlineData("en")]
    public void AllLiteralChronicleKeys_HaveTranslations(string language)
    {
        DirectoryInfo dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir != null && !File.Exists(Path.Combine(dir.FullName, "mod.json"))) dir = dir.Parent;
        Assert.NotNull(dir);
        var json = JsonDocument.Parse(File.ReadAllText(Path.Combine(dir.FullName, "Locales", language + ".json")));
        string source = File.ReadAllText(Path.Combine(dir.FullName, "src", "Game", "Chronicle.cs"));
        foreach (Match m in Regex.Matches(source, "\"(RP_CHRON_[A-Z_]+)\""))
            Assert.True(json.RootElement.TryGetProperty(m.Groups[1].Value, out _), language + ": missing " + m.Groups[1].Value);
    }
    [Fact]
    public void SummaryYear_UsesGameClock_NotAdvancedCoreClock()
    {
        string folder = Path.Combine(Path.GetTempPath(), "rp-review-" + Guid.NewGuid());
        try
        {
            MapBox.instance = new MapBox { kingdoms = new KingdomManager { list = new List<Kingdom>() }, cities = new CityManager { list = new List<City>() } };
            var world = new PoliticalWorld(); world.SetYear(2); // stubs' game clock is year 1
            var chronicle = new Chronicle(world, null, folder);
            chronicle.Flush();
            int year = (int)typeof(Chronicle).GetField("_lastYear", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(chronicle);
            Assert.Equal(1, year);
        }
        finally { MapBox.instance = null; if (Directory.Exists(folder)) Directory.Delete(folder, true); }
    }
    public void Dispose() { ElectionSystem.CandidateResolver = null; ElectionSystem.CandidateValidator = null; }
}
}
