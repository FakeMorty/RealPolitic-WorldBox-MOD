using System.Collections.Generic;
using System.Linq;
using RealPolitic.Core;
using Xunit;

namespace RealPolitic.Tests;

/// <summary>
/// Этап 25: реестр родословной (умершие родичи не теряются) + круговорот через SaveData v13.
/// </summary>
public class FamilyRegistryTests
{
	[Fact]
	public void ObserveCreatesAndUpdates_Record()
	{
		var reg = new FamilyRegistry();
		reg.Observe(100, "Джу", false, 20, 100, fatherId: 10, motherId: 11, "Корман", 5, 7);

		Assert.True(reg.TryGet(100, out FamilyRecord rec));
		Assert.Equal("Джу", rec.Name);
		Assert.Equal(80, rec.BirthYear);       // год рождения из возраста: 100 - 20
		Assert.Equal(10, rec.FatherId);
		Assert.Equal(11, rec.MotherId);
		Assert.Equal(100, rec.LastSeenYear);
		Assert.Equal(7, rec.KingdomId);

		reg.Observe(100, "Джу", false, 21, 101, 0, 0, "Корман", 5, 7);
		Assert.Equal(101, rec.LastSeenYear);
		Assert.Equal(10, rec.FatherId); // записанные родители не затираются нулём
	}

	[Fact]
	public void PresenceRefresh_UpdatesOnlyLivenessAndKingdom()
	{
		var reg = new FamilyRegistry();
		reg.Observe(42, "Руна", true, 17, 100, 10, 11, "Великий род", 9, 3);

		Assert.True(reg.ObservePresence(42, 101, 4));
		FamilyRecord record = reg.GetById(42);
		Assert.Equal(101, record.LastSeenYear);
		Assert.Equal(4, record.KingdomId);
		Assert.Equal("Руна", record.Name);
		Assert.Equal("Великий род", record.ClanName);
		Assert.Equal(10, record.FatherId);
		Assert.Equal(11, record.MotherId);
		Assert.False(reg.ObservePresence(99, 101, 4));

		reg.MarkDead(42, 102);
		Assert.True(reg.ObservePresence(42, 103, 5));
		Assert.Equal(101, record.LastSeenYear);
		Assert.Equal(4, record.KingdomId);
		Assert.Equal(102, record.DeathYear);
	}

	[Fact]
	public void MarkDeadDirectly_AndByMissing()
	{
		var reg = new FamilyRegistry();
		reg.Observe(1, "Отец", false, 40, 50, 0, 0, "Сток", 1, 1);
		reg.MarkDead(1, 52);
		Assert.Equal(52, reg.GetById(1).DeathYear);

		reg.Observe(2, "Дитя", true, 5, 50, 1, 0, "Сток", 1, 1);
		// не наблюдаем: через 3 года отсутствия «умершим» признаётся по нехватке данных
		int marked = reg.MarkMissingDead(50 + FamilyRegistry.MissingYearsBeforeDead);
		Assert.Equal(1, marked);
		Assert.Equal(51, reg.GetById(2).DeathYear);

		// Повторный прогон не трогает уже похороненных:
		Assert.Equal(0, reg.MarkMissingDead(99));
	}

	[Fact]
	public void ChildrenAndSiblings_Resolve()
	{
		var reg = new FamilyRegistry();
		reg.Observe(1, "Папа", false, 40, 50, 0, 0, "Сток", 1, 1);
		reg.Observe(2, "Мама", true, 38, 50, 0, 0, "Вель", 2, 1);
		reg.Observe(3, "Сын", false, 10, 50, 1, 2, "Сток", 1, 1);
		reg.Observe(4, "Дочь", true, 8, 50, 1, 2, "Сток", 1, 1);
		reg.Observe(5, "Другой", false, 12, 50, 9, 9, "Хтон", 3, 2);

		List<FamilyRecord> children = reg.Children(1);
		Assert.Equal(2, children.Count);
		Assert.Contains(children, c => c.Id == 3);
		Assert.Contains(children, c => c.Id == 4);
		Assert.Empty(reg.Children(5));

		FamilyRecord son = reg.GetById(3);
		List<FamilyRecord> sibs = reg.Siblings(son);
		Assert.Single(sibs);
		Assert.Equal(4, sibs[0].Id);
	}

	[Fact]
	public void Tree_Renders_Parents_Children_AndDeadMarks()
	{
		var reg = new FamilyRegistry();
		reg.Observe(1, "Папа", false, 40, 50, 0, 0, "Сток", 1, 1);
		reg.Observe(2, "Мама", true, 38, 50, 0, 0, "Вель", 2, 1);
		reg.Observe(3, "Сын", false, 10, 50, 1, 2, "Сток", 1, 1);
		reg.MarkDead(1, 55);

		List<string> lines = GenealogyTree.Build(reg, 3, maxLines: 20);

		Assert.True(lines.Count >= 3);
		Assert.Equal("■ Сын (род. 40)", lines[0]);
		Assert.Contains(lines, l => l.StartsWith("отец: † Папа (10–55)"));
		Assert.Contains(lines, l => l.StartsWith("мать: Мама (род. 12)"));
	}

	[Fact]
	public void Tree_ChildrenListed_WithPartner_AndCap()
	{
		var reg = new FamilyRegistry();
		reg.Observe(1, "Папа", false, 60, 60, 0, 0, "Сток", 1, 1);
		reg.Observe(2, "Жена", true, 55, 60, 0, 0, "Вель", 2, 1);
		for (int i = 0; i < 10; i++)
		{
			reg.Observe(100 + i, "Сын" + i, false, 5 + i, 60, 1, 2, "Сток", 1, 1);
		}

		List<string> lines = GenealogyTree.Build(reg, 1, maxLines: 30);

		Assert.Contains(lines, l => l == "сын: Сын9 (род. 46) — с Жена");
		Assert.Contains(lines, l => l == "… и ещё 2 детей");
		// Потолок строк соблюдён независимо:
		Assert.True(GenealogyTree.Build(reg, 1, maxLines: 5).Count <= 5);
	}

	[Fact]
	public void Tree_AncestorChain_ShowsDynastyDepth()
	{
		var reg = new FamilyRegistry();
		// 5 поколений отцовской линии: 200 лет мира помещаются в один блок
		reg.Observe(1, "Прадед", false, 40, 50, 0, 0, "Корман", 1, 1);
		reg.Observe(2, "Дед", false, 50, 70, 1, 0, "Корман", 1, 1);
		reg.Observe(3, "Отец", false, 30, 90, 2, 0, "Корман", 1, 1);
		reg.Observe(4, "Сын", false, 10, 110, 3, 0, "Корман", 1, 1);
		reg.MarkDead(1, 60); reg.MarkDead(2, 101);

		List<FamilyRecord> chain = GenealogyTree.AncestorChain(reg, reg.GetById(4));
		Assert.Equal(4, chain.Count);
		Assert.Equal(1, chain[3].Id);

		List<string> lines = GenealogyTree.Build(reg, 4, maxLines: 20);
		Assert.Contains(lines, l => l == "линия рода (4 покол.):");
		Assert.Contains(lines, l => l.Contains("†Прадед (") && l.Contains("→"));
	}

	[Fact]
	public void SaveData_RoundTrip_PreservesFamily()
	{
		var world = new PoliticalWorld();
		world.Family.Observe(5000000001234L, "Бабка", true, 60, 77, 0, 0, "Корман", 42, 3);
		world.Family.Observe(5000000005678L, "Внук", false, 10, 77, 0, 5000000001234L, "Корман", 42, 3);
		world.Family.MarkDead(5000000001234L, 78);

		string json = SaveData.Write(world);
		var restored = new PoliticalWorld();
		SaveData.Apply(restored, json);

		Assert.True(restored.Family.TryGet(5000000001234L, out FamilyRecord gran));
		Assert.Equal("Бабка", gran.Name);
		Assert.True(gran.Female);
		Assert.Equal(78, gran.DeathYear);
		Assert.True(restored.Family.TryGet(5000000005678L, out FamilyRecord grandchild));
		Assert.Equal(5000000001234L, grandchild.MotherId);
		Assert.Equal(42, grandchild.ClanId);
		List<FamilyRecord> kids = restored.Family.Children(5000000001234L);
		Assert.Single(kids);
	}

	[Fact]
	public void Trim_OnOvercapacity_RemovesOldDeadLeaves()
	{
		var reg = new FamilyRegistry();
		for (int i = 1; i <= FamilyRegistry.Capacity + 10; i++)
		{
			reg.Observe(i, "Житель" + i, false, 3, 10, 0, 0, "", 0, 1);
			reg.MarkDead(i, 20);
		}
		reg.Observe(FamilyRegistry.Capacity + 11, "Свежий", false, 1, 30, 0, 0, "", 0, 1);
		Assert.True(reg.Count <= FamilyRegistry.Capacity + 20);
	}
}
