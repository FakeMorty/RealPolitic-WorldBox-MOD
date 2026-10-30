using System;
using System.Collections.Generic;
using RealPolitic.Core;
using Xunit;

namespace RealPolitic.Tests;

/// <summary>Этап 21 (0.24): ванильные альянсы, вассалитет (дань без поглощения) и
/// соединяющее поглощение (Merger): вступивший тотчас сливается с лидером, границы
/// снимаются в снапшот AbsorbedChild для «права на выход» (0.25).</summary>
public class Stage21Tests
{
	// ------------------------------------------------------------------ альянсы игры

	[Fact]
	public void Alliances_FirstSnapshot_IsSilentBaseline()
	{
		var world = new PoliticalWorld();
		world.Rng = new Random(42);
		world.AddKingdom(1, IdeologyType.Monarchy, 0);
		world.AddKingdom(2, IdeologyType.Monarchy, 0);

		world.SetAllianceSnapshot(new List<AllianceRecord>
		{
			Alliance(1, "Северный союз", 1, 1, 2)
		});

		Assert.Equal(1, world.AlliancesBook.Count);
		Assert.Empty(world.Bus.OfType(PolEventType.AllianceFormed));
	}

	[Fact]
	public void Alliances_NewAlliance_PublishesFormedEvent()
	{
		var world = TwoKingdomWorld(out _, out _);
		world.SetAllianceSnapshot(new List<AllianceRecord>
		{
			Alliance(1, "Северный союз", 1, 1, 2)
		});
		// Первый снимок после базового тика уже публикует событие — альянс появился в мире.
		var evt = Assert.Single(world.Bus.OfType(PolEventType.AllianceFormed));
		Assert.Equal("Северный союз", evt.Get("name"));
		Assert.Equal("2", evt.Get("count"));
	}

	[Fact]
	public void Alliances_VanishedAlliance_PublishesBrokenEvent()
	{
		var world = TwoKingdomWorld(out _, out _);
		world.SetAllianceSnapshot(new List<AllianceRecord>
		{
			Alliance(1, "Северный союз", 1, 1, 2)
		});
		world.ClearEvents();
		world.SetAllianceSnapshot(new List<AllianceRecord>());

		var broken = Assert.Single(world.Bus.OfType(PolEventType.AllianceBroken));
		Assert.Equal("Северный союз", broken.Get("name"));
		Assert.Equal(0, world.AlliancesBook.Count);
	}

	[Fact]
	public void Alliances_MemberChanges_PublishJoinAndLeave()
	{
		var world = ThreeKingdomWorld(out _, out _, out _);
		world.SetAllianceSnapshot(new List<AllianceRecord>
		{
			Alliance(1, "Северный союз", 1, 1, 2)
		});
		world.ClearEvents();

		// Третья держава вступает.
		world.SetAllianceSnapshot(new List<AllianceRecord>
		{
			Alliance(1, "Северный союз", 1, 1, 2, 3)
		});
		var joined = Assert.Single(world.Bus.OfType(PolEventType.AllianceMemberJoined));
		// Параметр «member» публикуется именем державы, а не идентификатором.
		Assert.Contains("Gamma", joined.Get("member"));

		// Двое выходят — остаётся один лидер.
		world.SetAllianceSnapshot(new List<AllianceRecord>
		{
			Alliance(1, "Северный союз", 1, 1)
		});
		Assert.Equal(2, world.Bus.OfType(PolEventType.AllianceMemberLeft).Count);
	}

	[Fact]
	public void Alliances_TrackingDisabled_IgnoresSnapshot()
	{
		var world = TwoKingdomWorld(out _, out _);
		world.Params.TrackAlliances = false;
		world.SetAllianceSnapshot(new List<AllianceRecord>
		{
			Alliance(1, "Северный союз", 1, 1, 2)
		});
		Assert.Equal(0, world.AlliancesBook.Count);
		Assert.Empty(world.Bus.OfType(PolEventType.AllianceFormed));
	}

	// ------------------------------------------------------------------ соединение (Merger)

	[Fact]
	public void Merger_AbsorbsMemberImmediately()
	{
		var world = TwoKingdomWorld(out var k1, out var k2);
		Unions.JoinCheck check;
		Union union = world.TryCreateUnion(UnionType.Merger, 1, 2, "Соединение", Member(2), out check);

		Assert.NotNull(union);
		Assert.True(check.Ok, check.ReasonDetail);
		// Поглощённый не живёт в союзе: в участниках только лидер.
		Assert.Equal(1, union.MemberCount);
		Assert.True(union.Contains(1));
		Assert.False(union.Contains(2));
		// Но границы сняты в снапшот для «права на выход».
		AbsorbedChild child = union.GetAbsorbed(2);
		Assert.NotNull(child);
		Assert.Equal(20, child.CapitalCityId);
		Assert.Contains(20, child.CityIds);
		// Игровой слой должен перевести города члена лидеру.
		PendingMerger pending = Assert.Single(world.PendingMergers);
		Assert.Equal(2, pending.MemberId);
		Assert.Equal(1, pending.LeaderId);
		// Событие поглощения, а не старого «образования».
		Assert.Single(world.Bus.OfType(PolEventType.UnionMerged));
		Assert.Empty(world.Bus.OfType(PolEventType.UnionFormed));
		Assert.Equal(world.KingdomNameOf(2), pending.MemberName);
		Assert.Contains("Beta", pending.MemberName);
	}

	[Fact]
	public void Merger_GatedByArticleXV_NeighborsOnly()
	{
		var world = TwoKingdomWorld(out var k1, out _);
		k1.Constitution.Union = UnionLaw.Neighbors;

		// Не сосед — статья XV не пускает.
		Unions.JoinCheck check;
		Union union = world.TryCreateUnion(UnionType.Merger, 1, 2, "Соединение", Member(2, adjacent: false), out check);
		Assert.Null(union);
		Assert.False(check.Ok);
		Assert.Equal("RP_UNION_LAW_NEIGHBORS", check.ReasonKey);

		// Сосед — допускается, соседство приходит в снимке.
		union = world.TryCreateUnion(UnionType.Merger, 1, 2, "Соединение", Member(2, adjacent: true), out check);
		Assert.NotNull(union);
		Assert.True(check.Ok);
	}

	[Fact]
	public void Merger_GatedByArticleXV_SameLanguage()
	{
		var world = TwoKingdomWorld(out var k1, out _);
		k1.Constitution.Union = UnionLaw.SameLanguage;
		world.LeaderLanguageId = 5;

		// Язык разный — статья XV не пускает.
		Unions.JoinCheck check;
		Union union = world.TryCreateUnion(UnionType.Merger, 1, 2, "Соединение", Member(2, languageId: 3), out check);
		Assert.Null(union);
		Assert.False(check.Ok);
		Assert.Equal("RP_UNION_LAW_LANGUAGE", check.ReasonKey);

		// Язык один — допускается.
		union = world.TryCreateUnion(UnionType.Merger, 1, 2, "Соединение", Member(2, languageId: 5), out check);
		Assert.NotNull(union);
		Assert.True(check.Ok);

		// Язык члена неизвестен (0) — ядро не блокирует молча, слово за игровым слоем.
		world.LeaderLanguageId = 7;
		union = world.TryCreateUnion(UnionType.Merger, 1, 2, "Соединение", Member(2, languageId: 0), out check);
		Assert.NotNull(union);
	}

	[Fact]
	public void Merger_NotApplicableToVassalType()
	{
		var world = TwoKingdomWorld(out var k1, out _);
		k1.Constitution.Union = UnionLaw.Neighbors;
		world.LeaderLanguageId = 9;

		// Вассалитет статьёй XV не судится: соседство/язык на него не влияют.
		Unions.JoinCheck check;
		Union union = world.TryCreateUnion(UnionType.Vassal, 1, 2, "Вассалитет", Member(2, adjacent: false, languageId: 3), out check);
		Assert.NotNull(union);
		Assert.True(check.Ok);
		Assert.Equal(2, union.MemberCount);
	}

	// ------------------------------------------------------------------ вассалитет (дань, без поглощения)

	[Fact]
	public void VassalUnion_MembersStayLiving()
	{
		var world = TwoKingdomWorld(out _, out var k2);
		Unions.JoinCheck check;
		Union union = world.TryCreateUnion(UnionType.Vassal, 1, 2, "Вассалитет", Member(2), out check);

		Assert.NotNull(union);
		Assert.Equal(2, union.MemberCount);
		Assert.True(union.Contains(2));
		Assert.Null(union.GetAbsorbed(2));
		Assert.Empty(world.PendingMergers);
		Assert.Single(world.Bus.OfType(PolEventType.UnionFormed));
		// Наследие прежнего API: GetMember возвращает живого участника.
		Assert.NotNull(union.GetMember(2));
	}

	// ------------------------------------------------------------------ вступление в существующую унию

	[Fact]
	public void JoinUnion_Merger_AbsorbsCandidate()
	{
		var world = ThreeKingdomWorld(out _, out _, out _);
		Unions.JoinCheck check;
		Union union = world.TryCreateUnion(UnionType.Merger, 1, 2, "Соединение", Member(2), out check);
		Assert.NotNull(union);

		Union joined = world.TryJoinUnion(union, 3, Member(3), out check);

		Assert.NotNull(joined);
		Assert.True(check.Ok, check.ReasonDetail);
		Assert.NotNull(union.GetAbsorbed(3));
		Assert.False(union.Contains(3));
		Assert.Equal(1, union.MemberCount);
		Assert.Equal(2, world.PendingMergers.Count);
	}

	[Fact]
	public void JoinUnion_Vassal_AddsLivingMember()
	{
		var world = ThreeKingdomWorld(out _, out _, out _);
		Unions.JoinCheck check;
		Union union = world.TryCreateUnion(UnionType.Vassal, 1, 2, "Вассалитет", Member(2), out check);
		Assert.NotNull(union);

		Union joined = world.TryJoinUnion(union, 3, Member(3), out check);

		Assert.NotNull(joined);
		Assert.True(check.Ok, check.ReasonDetail);
		Assert.Equal(3, union.MemberCount);
		Assert.Same(union, world.UnionOf(3));
		Assert.Contains(world.Bus.OfType(PolEventType.UnionMemberJoined), e => e.Get("count") == "3");
	}

	[Fact]
	public void JoinUnion_RejectsMemberBeyondLimit()
	{
		var world = ThreeKingdomWorld(out _, out _, out _);
		world.UnionRules.MaxMembers = 2;
		Unions.JoinCheck check;
		Union union = world.TryCreateUnion(UnionType.Vassal, 1, 2, "Вассалитет", Member(2), out check);

		Union joined = world.TryJoinUnion(union, 3, Member(3), out check);

		Assert.Null(joined);
		Assert.Equal("RP_UNION_TOO_LARGE", check.ReasonKey);
		Assert.Equal(2, union.MemberCount);
	}

	[Fact]
	public void JoinUnion_Merger_RespectsSizeCapViaAbsorbed()
	{
		// Соединение тоже считается по потолку: поглощённые занимают место рядом с живыми.
		var world = FourKingdomWorld(out _, out _, out _, out _);
		world.UnionRules.MaxMembers = 2;
		Unions.JoinCheck check;
		Union union = world.TryCreateUnion(UnionType.Merger, 1, 2, "Соединение", Member(2), out check);
		Assert.NotNull(union);

		// Уже 1 живой + 1 поглощённый = размер 2 — третьему места нет.
		Union joined = world.TryJoinUnion(union, 3, Member(3), out check);
		Assert.Null(joined);
		Assert.Equal("RP_UNION_TOO_LARGE", check.ReasonKey);
	}

	[Fact]
	public void JoinUnion_RejectsKingdomAlreadyInAnotherUnion()
	{
		var world = FourKingdomWorld(out _, out _, out _, out _);
		Unions.JoinCheck check;
		Union first = world.TryCreateUnion(UnionType.Vassal, 1, 2, "Первая", Member(2), out check);
		Union second = world.TryCreateUnion(UnionType.Vassal, 3, 4, "Вторая", Member(4), out check);

		Union joined = world.TryJoinUnion(first, 4, Member(4), out check);

		Assert.Null(joined);
		Assert.Equal("RP_UNION_ALREADY_MEMBER", check.ReasonKey);
		Assert.Same(second, world.UnionOf(4));
	}

	[Fact]
	public void JoinUnion_RequiresLegitimateLeader()
	{
		var world = ThreeKingdomWorld(out var k1, out _, out _);
		Unions.JoinCheck check;
		Union union = world.TryCreateUnion(UnionType.Vassal, 1, 2, "Вассалитет", Member(2), out check);
		Assert.NotNull(union);
		k1.Legitimacy = 20f;

		Union joined = world.TryJoinUnion(union, 3, Member(3), out check);

		Assert.Null(joined);
		Assert.Equal("RP_UNION_LEADER_WEAK", check.ReasonKey);
	}

	// ------------------------------------------------------------------ сохранение

	[Fact]
	public void SaveLoad_MergerAbsorptionAndAlliances_Roundtrip()
	{
		var world = TwoKingdomWorld(out _, out _);
		Unions.JoinCheck check;
		Union union = world.TryCreateUnion(UnionType.Merger, 1, 2, "Соединение", Member(2), out check);
		Assert.NotNull(union);
		world.SetAllianceSnapshot(new List<AllianceRecord>
		{
			Alliance(1, "Северный союз", 1, 1, 2)
		});

		string json = SaveData.Write(world);
		Assert.Contains("\"abs\"", json);
		Assert.Contains("\"alls\"", json);
		Assert.DoesNotContain("\"inst\"", json);

		var restored = new PoliticalWorld();
		SaveData.Apply(restored, json);
		Union restoredUnion = Assert.Single(restored.Unions);
		Assert.Equal(UnionType.Merger, restoredUnion.Type);
		AbsorbedChild child = restoredUnion.GetAbsorbed(2);
		Assert.NotNull(child);
		Assert.Equal(20, child.CapitalCityId);
		Assert.Contains(20, child.CityIds);
		Assert.Equal(world.Year, child.AbsorbedYear);

		// Базовый снимок альянсов восстановлен: повторный снимок с теми же именами
		// не «создаёт» альянсы заново.
		restored.SetAllianceSnapshot(new List<AllianceRecord>
		{
			Alliance(1, "Северный союз", 1, 1, 2)
		});
		Assert.Empty(restored.Bus.OfType(PolEventType.AllianceFormed));
		Assert.Equal(1, restored.AlliancesBook.Count);
	}

	// ------------------------------------------------------------------ хелперы

	private static AllianceRecord Alliance(int id, string name, int leader, params int[] members)
	{
		var record = new AllianceRecord { Id = id, Name = name, LeaderId = leader };
		record.Members.AddRange(members);
		return record;
	}

	private static DiplomatSnapshot Member(int id, bool adjacent = true, int languageId = 2)
	{
		return new DiplomatSnapshot
		{
			KingdomId = id,
			Ideology = IdeologyType.Monarchy,
			Legitimacy = 60f,
			Warriors = 5,
			Population = 100,
			AdjacentToLeader = adjacent,
			LanguageId = languageId
		};
	}

	/// <summary>Две мирные монархии-соседки после базового тика.</summary>
	private static PoliticalWorld TwoKingdomWorld(out KingdomPolitics k1, out KingdomPolitics k2, int seed = 42)
	{
		var world = new PoliticalWorld();
		world.Rng = new Random(seed);
		world.SetBaseline(true);
		world.SetYear(5);
		world.AddKingdom(1, IdeologyType.Monarchy, 0);
		world.AddKingdom(2, IdeologyType.Monarchy, 0);
		world.AddCity(10, 1);
		world.AddCity(20, 2);
		world.YearTick(new List<KingdomInput>
		{
			KingdomSnapshot(1, 101, "KingA", RelationType.Peace, name: "Alpha"),
			KingdomSnapshot(2, 201, "QueenB", RelationType.Peace, name: "Beta")
		});
		k1 = world.GetKingdom(1);
		k2 = world.GetKingdom(2);
		return world;
	}

	private static PoliticalWorld ThreeKingdomWorld(out KingdomPolitics k1, out KingdomPolitics k2, out KingdomPolitics k3)
	{
		var world = new PoliticalWorld();
		world.Rng = new Random(42);
		world.SetBaseline(true);
		world.SetYear(5);
		world.AddKingdom(1, IdeologyType.Monarchy, 0);
		world.AddKingdom(2, IdeologyType.Monarchy, 0);
		world.AddKingdom(3, IdeologyType.Monarchy, 0);
		world.AddCity(10, 1);
		world.AddCity(20, 2);
		world.AddCity(30, 3);
		world.YearTick(new List<KingdomInput>
		{
			KingdomSnapshot(1, 101, "KingA", RelationType.Peace, name: "Alpha"),
			KingdomSnapshot(2, 201, "QueenB", RelationType.Peace, name: "Beta"),
			KingdomSnapshot(3, 301, "KingC", RelationType.Peace, name: "Gamma")
		});
		k1 = world.GetKingdom(1);
		k2 = world.GetKingdom(2);
		k3 = world.GetKingdom(3);
		return world;
	}

	private static PoliticalWorld FourKingdomWorld(out KingdomPolitics k1, out KingdomPolitics k2, out KingdomPolitics k3, out KingdomPolitics k4)
	{
		var world = new PoliticalWorld();
		world.Rng = new Random(42);
		world.SetBaseline(true);
		world.SetYear(5);
		world.AddKingdom(1, IdeologyType.Monarchy, 0);
		world.AddKingdom(2, IdeologyType.Monarchy, 0);
		world.AddKingdom(3, IdeologyType.Monarchy, 0);
		world.AddKingdom(4, IdeologyType.Monarchy, 0);
		world.AddCity(10, 1);
		world.AddCity(20, 2);
		world.AddCity(30, 3);
		world.AddCity(40, 4);
		world.YearTick(new List<KingdomInput>
		{
			KingdomSnapshot(1, 101, "KingA", RelationType.Peace, name: "Alpha"),
			KingdomSnapshot(2, 201, "QueenB", RelationType.Peace, name: "Beta"),
			KingdomSnapshot(3, 301, "KingC", RelationType.Peace, name: "Gamma"),
			KingdomSnapshot(4, 401, "KingD", RelationType.Peace, name: "Delta")
		});
		k1 = world.GetKingdom(1);
		k2 = world.GetKingdom(2);
		k3 = world.GetKingdom(3);
		k4 = world.GetKingdom(4);
		return world;
	}

	private static KingdomInput KingdomSnapshot(int kingdomId, int rulerActorId, string rulerName,
		RelationType relation, string name = null)
	{
		var relations = new List<RelationInfo>();
		if (kingdomId == 1)
		{
			relations.Add(new RelationInfo { OtherId = 2, Relation = relation, Adjacent = true });
			relations.Add(new RelationInfo { OtherId = 3, Relation = relation, Adjacent = true });
			relations.Add(new RelationInfo { OtherId = 4, Relation = relation, Adjacent = true });
		}
		else
		{
			relations.Add(new RelationInfo { OtherId = 1, Relation = relation, Adjacent = true });
		}
		return new KingdomInput
		{
			KingdomId = kingdomId,
			Name = name ?? ("K" + kingdomId),
			Population = 100,
			Warriors = 5,
			Ruler = new RulerRef
			{
				ActorId = rulerActorId,
				Name = rulerName,
				KingdomId = kingdomId,
				SinceYear = 1,
				Age = 30,
				ClanName = "Дом" + kingdomId,
				ClanId = rulerActorId
			},
			Relations = relations,
			Cities = new List<CityInput>
			{
				new CityInput
				{
					CityId = kingdomId * 10,
					KingdomId = kingdomId,
					Population = 100,
					FoodOk = true,
					CultureId = 7,
					CultureName = "C",
					ReligionId = 3,
					ReligionName = "R",
					LanguageId = 2,
					LanguageName = "L"
				}
			}
		};
	}
}
