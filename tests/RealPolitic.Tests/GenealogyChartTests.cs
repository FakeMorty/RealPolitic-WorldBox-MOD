using System.Collections.Generic;
using System.Linq;
using RealPolitic.Core;
using Xunit;

namespace RealPolitic.Tests;

/// <summary>Этап 25c: раскладка интерактивного древа (верх — предки, низ — дети).</summary>
public class GenealogyChartTests
{
	private static FamilyRegistry Dynasty()
	{
		var reg = new FamilyRegistry();
		reg.Observe(1, "Основатель", false, 70, 70, 0, 0, "Корман", 1, 1);
		reg.Observe(2, "Основательша", true, 65, 70, 0, 0, "Вель", 2, 1);
		reg.Observe(3, "Дед", false, 45, 90, 1, 2, "Корман", 1, 1);
		reg.Observe(4, "Бабка", true, 40, 90, 0, 0, "Таль", 3, 1);
		reg.Observe(5, "Отец", false, 25, 110, 3, 4, "Корман", 1, 1);
		reg.Observe(6, "Мать", true, 23, 110, 0, 0, "Узум", 4, 1);
		reg.Observe(7, "Герой", false, 5, 130, 5, 6, "Корман", 1, 1);
		reg.Observe(8, "Брат", false, 3, 130, 5, 6, "Корман", 1, 1);
		reg.Observe(9, "Дядя", false, 23, 110, 3, 4, "Корман", 1, 1);
		reg.MarkDead(1, 75);
		reg.MarkDead(2, 80);
		return reg;
	}

	[Fact]
	public void Chart_PlacesParentsAndGrandparents_AboveFocus()
	{
		FamilyRegistry reg = Dynasty();
		(List<ChartNode> nodes, List<ChartEdge> edges) = GenealogyChart.Build(reg, 7);

		ChartNode hero = nodes.Single(n => n.IsFocus);
		Assert.Equal(7, hero.Id);
		Assert.Equal(0, hero.Gen);

		ChartNode father = nodes.Single(n => n.Id == 5);
		Assert.Equal(-1, father.Gen);
		Assert.True(father.X < hero.X); // отец слева

		ChartNode mother = nodes.Single(n => n.Id == 6);
		Assert.Equal(-1, mother.Gen);
		Assert.True(mother.X > hero.X); // мать справа

		// предки до прадедов включительно (depth до MaxAncestorGen-1 = 3)
		Assert.Contains(nodes, n => n.Id == 1 && n.Gen == -3 && n.Dead);
		Assert.Contains(nodes, n => n.Id == 3 && n.Gen == -2);
	}

	[Fact]
	public void Chart_EdgesConnect_ParentToChild()
	{
		FamilyRegistry reg = Dynasty();
		(List<ChartNode> nodes, List<ChartEdge> edges) = GenealogyChart.Build(reg, 7);

		Assert.True(nodes.Count > 4);
		foreach (ChartEdge e in edges)
		{
			Assert.True(e.FromNode >= 0 && e.FromNode < nodes.Count);
			Assert.True(e.ToNode >= 0 && e.ToNode < nodes.Count);
			Assert.Equal(nodes[e.FromNode].Gen, nodes[e.ToNode].Gen - 1);
		}
	}

	[Fact]
	public void Chart_ChildrenBelow_SiblingsBeside()
	{
		var reg = new FamilyRegistry();
		reg.Observe(1, "Отец", false, 40, 50, 0, 0, "X", 1, 1);
		reg.Observe(2, "Мать", true, 38, 50, 0, 0, "Y", 2, 1);
		reg.Observe(3, "Герой", false, 12, 50, 1, 2, "X", 1, 1);
		reg.Observe(4, "Братишка", false, 10, 50, 1, 2, "X", 1, 1);
		reg.Observe(5, "Внук у героя", false, 2, 50, 3, 0, "X", 1, 1);

		(List<ChartNode> nodes, _) = GenealogyChart.Build(reg, 3);

		Assert.Contains(nodes, n => n.Id == 4 && n.Gen == 0);
		Assert.Contains(nodes, n => n.Id == 5 && n.Gen == 1);
	}

	[Fact]
	public void Chart_MoreNode_RefocusesPastMaxDepth()
	{
		// Глубокая династия: 7 поколений — у самого старшего видимого предка должен быть переход «… выше».
		var reg = new FamilyRegistry();
		for (int i = 1; i <= 7; i++)
		{
			reg.Observe(i, "Колено" + i, false, 10 + (7 - i) * 20, 100, i - 1, 0, "X", 1, 1);
		}
		(List<ChartNode> nodes, _) = GenealogyChart.Build(reg, 7);

		ChartNode more = Assert.Single(nodes, n => n.More);
		Assert.Equal(3, more.Id);                        // переход к ближайшему старшему предку за глубиной показа
		Assert.Equal(-GenealogyChart.MaxAncestorGen, more.Gen);
	}

	[Fact]
	public void Chart_UnknownFocus_EmptyResult()
	{
		var reg = new FamilyRegistry();
		(List<ChartNode> nodes, List<ChartEdge> edges) = GenealogyChart.Build(reg, 999);
		Assert.Empty(nodes);
		Assert.Empty(edges);
	}
}
