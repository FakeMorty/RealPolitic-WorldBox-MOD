using System;
using RealPolitic.Core;
using Xunit;

namespace RealPolitic.Tests;

public class SaveDataTests
{
    [Fact]
    public void RoundTrip_PreservesIdeologyMechanicsState()
    {
        var source = new PoliticalWorld();
        source.SetYear(42);
        source.SetBaseline(true);
        KingdomPolitics kingdom = source.AddKingdom(7, IdeologyType.Militarism, 3);
        kingdom.Support = 31.5f;
        kingdom.AvgDissent = 0.27f;
        kingdom.InWar = true;
        kingdom.Starving = true;
        kingdom.Mechanics.LastUsedYear = 35;
        kingdom.Mechanics.ActiveUntilYear = 45;
        kingdom.Mechanics.TargetKingdomId = 9;
        kingdom.Mechanics.ThreatLevel = 68.25f;
        kingdom.Mechanics.ThreatBreaks = 2;
        kingdom.Mechanics.LastBreakYear = 28;

        string json = SaveData.Write(source);
        var restored = new PoliticalWorld();
        SaveData.Apply(restored, json);
        KingdomPolitics actual = restored.GetKingdom(7);

        Assert.NotNull(actual);
        Assert.Equal(42, restored.Year);
        Assert.Equal(31.5f, actual.Support, 3);
        Assert.Equal(0.27f, actual.AvgDissent, 3);
        Assert.True(actual.InWar);
        Assert.True(actual.Starving);
        Assert.Equal(35, actual.Mechanics.LastUsedYear);
        Assert.Equal(45, actual.Mechanics.ActiveUntilYear);
        Assert.Equal(9, actual.Mechanics.TargetKingdomId);
        Assert.Equal(68.25f, actual.Mechanics.ThreatLevel, 3);
        Assert.Equal(2, actual.Mechanics.ThreatBreaks);
        Assert.Equal(28, actual.Mechanics.LastBreakYear);
    }

    [Fact]
    public void RoundTrip_PreservesUnionsAndMembership()
    {
        var source = new PoliticalWorld();
        source.SetYear(50);
        KingdomPolitics k1 = source.AddKingdom(1, IdeologyType.Monarchy, 1);
        KingdomPolitics k2 = source.AddKingdom(2, IdeologyType.Monarchy, 1);

        var union = new Union
        {
            Id = 100,
            Type = UnionType.Vassal,
            Name = "Grand Alliance",
            LeaderId = 1,
            FormedYear = 40,
            Cohesion = 0.85f,
            LeaderWarriors = 120
        };
        union.Members.Add(new UnionMember { KingdomId = 1, IsLeader = true, Approval = 0.9f, JoinedYear = 40 });
        union.Members.Add(new UnionMember { KingdomId = 2, IsLeader = false, Approval = 0.75f, JoinedYear = 40 });
        source.RestoreUnion(union);
        k1.Union = union;
        k2.Union = union;

        string json = SaveData.Write(source);
        var restored = new PoliticalWorld();
        SaveData.Apply(restored, json);

        Assert.Single(restored.Unions);
        Union restoredUnion = restored.Unions[0];
        Assert.Equal(100, restoredUnion.Id);
        Assert.Equal("Grand Alliance", restoredUnion.Name);
        Assert.Equal(UnionType.Vassal, restoredUnion.Type);
        Assert.Equal(1, restoredUnion.LeaderId);
        Assert.Equal(2, restoredUnion.MemberCount);
        Assert.NotNull(restored.UnionOf(1));
        Assert.NotNull(restored.UnionOf(2));
        Assert.Equal(restoredUnion, restored.GetKingdom(1)!.Union);
        Assert.Equal(restoredUnion, restored.GetKingdom(2)!.Union);

        // С формата v7 участник хранится объектом: одобрение и год вступления больше не теряются.
        Assert.Equal(0.9f, restoredUnion.GetMember(1)!.Approval, 3);
        Assert.Equal(0.75f, restoredUnion.GetMember(2)!.Approval, 3);
        Assert.Equal(40, restoredUnion.GetMember(2)!.JoinedYear);
    }

    [Fact]
    public void RoundTrip_PreservesUnionNameKey()
    {
        var source = new PoliticalWorld();
        source.SetYear(50);
        source.AddKingdom(1, IdeologyType.Monarchy, 1);
        source.AddKingdom(2, IdeologyType.Monarchy, 1);

        var union = new Union
        {
            Id = 7,
            Type = UnionType.Vassal,
            Name = string.Empty,
            NameKey = "RP_UNION_NAME_DYNASTIC",
            LeaderId = 1,
            FormedYear = 49
        };
        union.Members.Add(new UnionMember { KingdomId = 1, IsLeader = true, JoinedYear = 49 });
        union.Members.Add(new UnionMember { KingdomId = 2, JoinedYear = 49 });
        source.RestoreUnion(union);

        string json = SaveData.Write(source);
        var restored = new PoliticalWorld();
        SaveData.Apply(restored, json);

        Assert.Equal("RP_UNION_NAME_DYNASTIC", restored.Unions[0].NameKey);
        Assert.Equal("RP_UNION_NAME_DYNASTIC", restored.Unions[0].DisplayNameOrKey);
    }

    [Fact]
    public void Apply_ReadsLegacyUnionMembersSavedAsPlainIds()
    {
        // Мир формата v6: участники — голые числа. Загрузка обязана пройти и подставить значения по умолчанию.
        var source = new PoliticalWorld();
        source.SetYear(50);
        source.AddKingdom(1, IdeologyType.Monarchy, 1);
        source.AddKingdom(2, IdeologyType.Monarchy, 1);
        var union = new Union { Id = 3, Type = UnionType.Vassal, Name = "Legacy", LeaderId = 1, FormedYear = 44 };
        union.Members.Add(new UnionMember { KingdomId = 1, IsLeader = true, JoinedYear = 44 });
        union.Members.Add(new UnionMember { KingdomId = 2, JoinedYear = 44, Approval = 0.31f });
        source.RestoreUnion(union);

        string json = SaveData.Write(source);

        // Вырезаем массив участников и возвращаем его в виде формата v6 — голых чисел.
        int start = json.IndexOf("\"m\":[", StringComparison.Ordinal);
        Assert.True(start > 0, "в сохранении должен быть массив участников");
        int end = json.IndexOf(']', start);
        string legacy = json.Substring(0, start) + "\"m\":[1,2]" + json.Substring(end + 1);
        Assert.Contains("\"m\":[1,2]", legacy);

        var restored = new PoliticalWorld();
        SaveData.Apply(restored, legacy);

        Assert.Single(restored.Unions);
        Assert.Equal(2, restored.Unions[0].MemberCount);
        Assert.Equal(0.6f, restored.Unions[0].GetMember(2)!.Approval, 3);
        Assert.Equal(44, restored.Unions[0].GetMember(2)!.JoinedYear);
    }

    [Fact]
    public void RoundTrip_PreservesFemaleRulerAndAge()
    {
        var source = new PoliticalWorld();
        KingdomPolitics kingdom = source.AddKingdom(5, IdeologyType.Monarchy, 2);
        kingdom.Ruler = new RulerRef
        {
            ActorId = 123,
            Name = "Queen Elisabeth",
            KingdomId = 5,
            SinceYear = 10,
            IsFemale = true,
            Age = 45
        };

        string json = SaveData.Write(source);
        var restored = new PoliticalWorld();
        SaveData.Apply(restored, json);

        KingdomPolitics? restoredK = restored.GetKingdom(5);
        Assert.NotNull(restoredK);
        Assert.NotNull(restoredK.Ruler);
        Assert.Equal("Queen Elisabeth", restoredK.Ruler.Name);
        Assert.True(restoredK.Ruler.IsFemale);
        Assert.Equal(45, restoredK.Ruler.Age);
        Assert.Equal(10, restoredK.Ruler.SinceYear);
    }

    [Fact]
    public void RoundTrip_PreservesDeathRecordEnrichedFields()
    {
        var source = new PoliticalWorld();
        source.SetYear(25);
        source.Deaths.Record(
            actorId: 99,
            actorName: "Queen Victoria",
            cause: DeathCause.OldAge,
            year: 25,
            killerId: null,
            killerName: "",
            killerTitle: "",
            tileId: null,
            kingdomId: 1,
            cityName: "London",
            wasRuler: true,
            wasChosen: true,
            age: 81,
            note: "Beloved",
            wasFemale: true,
            reignYears: 63
        );

        DeathRecord? death = source.Deaths.Get(99);
        Assert.NotNull(death);
        death.KillerIsCiv = true;
        death.KillerKingdom = "Rebels";
        death.KillerCity = "York";
        death.KillerJob = "assassin";

        string json = SaveData.Write(source);
        var restored = new PoliticalWorld();
        SaveData.Apply(restored, json);

        DeathRecord? restoredDeath = restored.Deaths.Get(99);
        Assert.NotNull(restoredDeath);
        Assert.Equal("Queen Victoria", restoredDeath.ActorName);
        Assert.True(restoredDeath.WasFemale);
        Assert.True(restoredDeath.WasRuler);
        Assert.True(restoredDeath.WasChosen);
        Assert.Equal(81, restoredDeath.Age);
        Assert.Equal(63, restoredDeath.ReignYears);
        Assert.True(restoredDeath.KillerIsCiv);
        Assert.Equal("Rebels", restoredDeath.KillerKingdom);
        Assert.Equal("York", restoredDeath.KillerCity);
        Assert.Equal("assassin", restoredDeath.KillerJob);
    }

    [Fact]
    public void Apply_Format2WithoutNewFieldsKeepsSafeDefaults()
    {
        const string format2Json = "{\"format\":2,\"year\":15,\"baseline\":true,\"kingdoms\":[{\"id\":4,\"i\":2,\"s\":1,\"l\":50,\"f\":0,\"mech\":{\"last\":-2147483648,\"active\":0,\"target\":-1,\"threat\":10.0,\"breaks\":0,\"breakYear\":0},\"ruler\":77,\"rulerName\":\"King Carl\",\"rulerSince\":5}],\"cities\":[],\"actors\":[],\"deaths\":[{\"id\":10,\"n\":\"Old Man\",\"c\":1,\"y\":12,\"killer\":-1,\"kn\":\"\",\"kt\":\"\",\"king\":4,\"city\":\"Cap\",\"r\":false,\"ch\":false,\"a\":70,\"note\":\"\"}],\"unions\":[]}";
        var world = new PoliticalWorld();

        SaveData.Apply(world, format2Json);
        KingdomPolitics kingdom = world.GetKingdom(4);

        Assert.NotNull(kingdom);
        Assert.NotNull(kingdom.Ruler);
        Assert.Equal("King Carl", kingdom.Ruler.Name);
        Assert.False(kingdom.Ruler.IsFemale);
        Assert.Equal(0, kingdom.Ruler.Age);

        DeathRecord? death = world.Deaths.Get(10);
        Assert.NotNull(death);
        Assert.Equal("Old Man", death.ActorName);
        Assert.False(death.WasFemale);
        Assert.Null(death.ReignYears);
        Assert.False(death.KillerIsCiv);
    }

    [Fact]
    public void Apply_OldFormatWithoutMechanicsKeepsSafeDefaults()
    {
        const string oldJson = "{\"format\":1,\"year\":8,\"baseline\":true,\"kingdoms\":[{\"id\":3,\"i\":1,\"s\":2,\"l\":60,\"f\":1}],\"cities\":[],\"actors\":[],\"deaths\":[],\"unions\":[]}";
        var world = new PoliticalWorld();

        SaveData.Apply(world, oldJson);
        KingdomPolitics kingdom = world.GetKingdom(3);

        Assert.NotNull(kingdom);
        Assert.Equal(8, world.Year);
        Assert.Equal(int.MinValue, kingdom.Mechanics.LastUsedYear);
        Assert.Equal(0, kingdom.Mechanics.ActiveUntilYear);
        Assert.Null(kingdom.Mechanics.TargetKingdomId);
        Assert.InRange(kingdom.Mechanics.ThreatLevel, 0f, 35f);
    }
}
