using System.Collections.Generic;

namespace RealPolitic.Core;

/// <summary>
/// Этап 25c: раскладка генеалогического древа для окна (кликабельные узлы).
/// Фокусный человек — в центре. Предки веером вверх (отец слева, мать справа,
/// вилка сужается с каждым коленом), дети рядами вниз. Старше 4 колен вверх —
/// «… ↑» переход: клик по предку показывает дерево от его лица, и выше.
/// </summary>
public sealed class ChartNode
{
	public long Id;               // 0 — декоративный узел без записи
	public float X;               // горизонтальная позиция (условные единицы)
	public int Gen;               // поколение: фокус = 0, предки отрицательные, дети положительные
	public string Label = string.Empty;
	public bool Dead;
	public bool IsFocus;
	public bool More;             // узел «…выше/дальше» — переход по клику (Id = куда)
}

public sealed class ChartEdge
{
	public int FromNode;          // индекс узла-родителя в списке
	public int ToNode;            // индекс узла-ребёнка
}

public static class GenealogyChart
{
	/// <summary>Максимум колен вверх от фокуса (дальше — через «… выше»).</summary>
	public const int MaxAncestorGen = 4;

	/// <summary>Вилки расходятся на ширину Week одного колена в родительском ярусе.</summary>
	public const float ForkStep = 1.6f;

	public static (List<ChartNode> Nodes, List<ChartEdge> Edges) Build(FamilyRegistry reg, long focusId)
	{
		List<ChartNode> nodes = new List<ChartNode>();
		List<ChartEdge> edges = new List<ChartEdge>();
		if (reg == null || !reg.TryGet(focusId, out FamilyRecord focus))
		{
			return (nodes, edges);
		}
		HashSet<long> placed = new HashSet<long>();
		int focusIndex = nodes.Count;
		nodes.Add(MakeNode(focus, 0f, 0, isFocus: true));
		placed.Add(focus.Id);

		// Предки веером вверх: depth 1 — родители, до MaxAncestorGen — вилка сужается.
		PlaceAncestors(reg, focus, 0, 1, 0f, FocusFork(1), nodes, edges, placed, focusIndex, 0);

		// Братья/сёстры рядом с фокусом (до 3 с каждой стороны, дальше «…»).
		List<FamilyRecord> siblings = reg.Siblings(focus);
		int left = 0, right = 0;
		for (int i = 0; i < siblings.Count; i++)
		{
			if (placed.Contains(siblings[i].Id))
			{
				continue;
			}
			float sx;
			if (i % 2 == 0)
			{
				right++;
				sx = FocusFork(1) * 2f + (right - 1) * 1.1f;
			}
			else
			{
				left++;
				sx = -FocusFork(1) * 2f - (left - 1) * 1.1f;
			}
			if (right + left > 6)
			{
				ChartNode more = new ChartNode { Id = -1, X = sx, Gen = 0, Label = "…", More = false };
				nodes.Add(more);
				break;
			}
			placed.Add(siblings[i].Id);
			nodes.Add(MakeNode(siblings[i], sx, 0, false));
		}

		// Дети — ряд внизу (до 8, дальше «…»), внуки упираются в клик: дети переходят в фокус.
		List<FamilyRecord> children = reg.Children(focus.Id);
		float spread = 1.2f;
		int shown = 0;
		for (int i = 0; i < children.Count; i++)
		{
			if (shown >= 8 || placed.Contains(children[i].Id))
			{
				break;
			}
			float offset = (shown - 3.5f) * spread;
			ChartNode childNode = MakeNode(children[i], offset, 1, false);
			nodes.Add(childNode);
			placed.Add(children[i].Id);
			edges.Add(new ChartEdge { FromNode = focusIndex, ToNode = nodes.Count - 1 });
			shown++;
		}
		if (children.Count > shown)
		{
			nodes.Add(new ChartNode { Id = -1, X = 4.8f * spread, Gen = 1, Label = "…и ещё " + (children.Count - shown) });
		}
		return (nodes, edges);
	}

	private static float FocusFork(int depth)
	{
		// Ширина вилки родителей: чем глубже, тем уже (веерная раскладка).
		float w = ForkStep;
		for (int i = 1; i < depth; i++)
		{
			w *= 0.62f;
		}
		return w;
	}

	private static void PlaceAncestors(FamilyRegistry reg, FamilyRecord person,
		int personIndex, int depth, float personX, float fork,
		List<ChartNode> nodes, List<ChartEdge> edges, HashSet<long> placed, int focusIndex, int dummy)
	{
		if (depth > MaxAncestorGen || person == null)
		{
			return;
		}
		// Глубже максимума: «… ↑» над самым старшим Известным предком ветки.
		FamilyRecord father = GetOrNull(reg, person.FatherId);
		FamilyRecord mother = GetOrNull(reg, person.MotherId);
		if (depth == MaxAncestorGen)
		{
			FamilyRecord deepest = father ?? mother;
			if (deepest != null)
			{
				ChartNode more = new ChartNode
				{
					Id = deepest.Id,
					X = personX,
					Gen = -depth,
					Label = "… выше → " + deepest.Name.Split(' ')[0],
					More = true,
					Dead = deepest.DeathYear != null
				};
				nodes.Add(more);
				edges.Add(new ChartEdge { FromNode = nodes.Count - 1, ToNode = personIndex });
			}
			return;
		}
		if (father != null && !placed.Contains(father.Id))
		{
			placed.Add(father.Id);
			ChartNode fatherNode = MakeNode(father, personX - fork, -depth, false);
			nodes.Add(fatherNode);
			int idx = nodes.Count - 1;
			edges.Add(new ChartEdge { FromNode = idx, ToNode = personIndex });
			PlaceAncestors(reg, father, idx, depth + 1, fatherNode.X, FocusFork(depth + 1), nodes, edges, placed, focusIndex, 0);
		}
		if (mother != null && !placed.Contains(mother.Id))
		{
			placed.Add(mother.Id);
			ChartNode motherNode = MakeNode(mother, personX + fork, -depth, false);
			nodes.Add(motherNode);
			int idx = nodes.Count - 1;
			edges.Add(new ChartEdge { FromNode = idx, ToNode = personIndex });
			PlaceAncestors(reg, mother, idx, depth + 1, motherNode.X, FocusFork(depth + 1), nodes, edges, placed, focusIndex, 0);
		}
	}

	private static FamilyRecord GetOrNull(FamilyRegistry reg, long id)
	{
		if (id == 0 || !reg.TryGet(id, out FamilyRecord r))
		{
			return null;
		}
		return r;
	}

	private static ChartNode MakeNode(FamilyRecord r, float x, int gen, bool isFocus)
	{
		string years;
		if (r.BirthYear > 0 && r.DeathYear != null && r.DeathYear.Value > 0)
		{
			years = r.BirthYear + "–" + r.DeathYear.Value;
		}
		else if (r.BirthYear > 0)
		{
			years = r.BirthYear + "+";
		}
		else
		{
			years = string.Empty;
		}
		return new ChartNode
		{
			Id = r.Id,
			X = x,
			Gen = gen,
			Label = r.Name + (years.Length > 0 ? "\n" + years : string.Empty),
			Dead = r.DeathYear != null,
			IsFocus = isFocus
		};
	}
}
