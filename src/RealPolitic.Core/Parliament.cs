using System;
using System.Collections.Generic;

namespace RealPolitic.Core;

/// <summary>
/// 0.46: парламент — 100 мест полукругом.
///
/// Владелец 29.10.2026: «добавить кнопку где будет отрисованы места в парламенте.
/// Пусть это будет 100 мест. Они должны быть расположены красивым полукругом…
/// можно посмотреть на цвет, спуститься вниз и посмотреть, что под этим цветом
/// сидит партия такая-то с такой-то идеологией. Ну и в уведомления выносить
/// выборы в парламент».
///
/// Места считает ядро (дележ по крупнейшим остаткам), а рисует игровой слой.
/// Геометрия тоже здесь: ряд за рядом по дуге полукруга — так окно и строится,
/// и это можно проверить тестами без Unity.
/// </summary>
public static class Parliament
{
	/// <summary>Сколько мест в парламенте.</summary>
	public const int TotalSeats = 100;

	/// <summary>Сколько мест показывать в полукруге (все).</summary>
	public const int Rows = 5;

	/// <summary>
	/// Распределение мест по долям партий (в процентах, любые числа).
	/// Метод крупнейших остатков: сумма долей не обязана быть 100,
	/// а сумма мест всегда ровно <see cref="TotalSeats"/>.
	/// </summary>
	public static int[] Allocate(IReadOnlyList<int> shares, int totalSeats = TotalSeats)
	{
		int count = shares?.Count ?? 0;
		int[] seats = new int[count];
		if (count == 0 || totalSeats <= 0)
		{
			return seats;
		}
		long sum = 0;
		for (int i = 0; i < count; i++)
		{
			sum += Math.Max(0, shares[i]);
		}
		if (sum <= 0)
		{
			return seats;
		}
		long[] remainders = new long[count];
		int assigned = 0;
		for (int i = 0; i < count; i++)
		{
			long value = Math.Max(0, shares[i]);
			long exact = value * totalSeats;
			seats[i] = (int)(exact / sum);
			remainders[i] = exact % sum;
			assigned += seats[i];
		}
		// Остаток раздаём по одному тем, у кого остаток больше; при равенстве — левее.
		while (assigned < totalSeats)
		{
			int best = -1;
			for (int i = 0; i < count; i++)
			{
				if (shares[i] <= 0)
				{
					continue;
				}
				if (best < 0 || remainders[i] > remainders[best])
				{
					best = i;
				}
			}
			if (best < 0)
			{
				break;
			}
			seats[best]++;
			remainders[best] = -1;
			assigned++;
		}
		return seats;
	}

	/// <summary>
	/// Раскладка мест полукругом: ряд за рядом, от левого края к правому.
	/// Координаты в условных единицах окна (0 — центр нижнего края).
	/// Возвращает массив точек; индекс = место.
	/// </summary>
	public static ParliamentPoint[] Arc(int totalSeats, int rows, float radiusInner, float radiusStep, float arcDegrees)
	{
		if (totalSeats <= 0)
		{
			return Array.Empty<ParliamentPoint>();
		}
		rows = Math.Max(1, rows);
		ParliamentPoint[] points = new ParliamentPoint[totalSeats];
		// Сколько мест в каждом ряду: ближние ряды короче (дуга уже), дальние длиннее.
		int[] perRow = new int[rows];
		float weightSum = 0f;
		for (int r = 0; r < rows; r++)
		{
			weightSum += (radiusInner + radiusStep * r);
		}
		int assigned = 0;
		for (int r = 0; r < rows; r++)
		{
			float w = (radiusInner + radiusStep * r) / weightSum;
			perRow[r] = r == rows - 1 ? totalSeats - assigned : (int)Math.Round(totalSeats * w);
			assigned += perRow[r];
		}
		int index = 0;
		for (int r = 0; r < rows; r++)
		{
			int inRow = perRow[r];
			if (inRow <= 0)
			{
				continue;
			}
			float radius = radiusInner + radiusStep * r;
			for (int i = 0; i < inRow && index < totalSeats; i++, index++)
			{
				// Полукруг: угол от 180° (лево) до 0° (право).
				float t = inRow == 1 ? 0.5f : (float)i / (inRow - 1);
				double angle = Math.PI * (1.0 - t * (arcDegrees / 180.0));
				points[index] = new ParliamentPoint
				{
					Seat = index,
					Row = r,
					X = (float)(Math.Cos(angle) * radius),
					Y = (float)(Math.Sin(angle) * radius)
				};
			}
		}
		return points;
	}

	/// <summary>
	/// Кому принадлежит каждое место: партии идут блоками слева направо,
	/// как в настоящем зале — от левых к правым. Возвращает индекс партии на каждое место.
	/// </summary>
	public static int[] SeatingOrder(IReadOnlyList<IdeologyType> partyIdeologies, IReadOnlyList<int> seats, IReadOnlyList<ParliamentPoint> points)
	{
		int count = seats?.Count ?? 0;
		int[] order = new int[points?.Count ?? 0];
		if (count == 0 || order.Length == 0)
		{
			return order;
		}
		// Порядок партий в зале: по «левизне» строя — от коммунизма к этнонационализму.
		int[] partyOrder = new int[count];
		for (int i = 0; i < count; i++)
		{
			partyOrder[i] = i;
		}
		Array.Sort(partyOrder, (a, b) =>
		{
			int cmp = PoliticalAxis(partyIdeologies[a]).CompareTo(PoliticalAxis(partyIdeologies[b]));
			return cmp != 0 ? cmp : a.CompareTo(b);
		});
		int seat = 0;
		for (int p = 0; p < count; p++)
		{
			int party = partyOrder[p];
			for (int s = 0; s < seats[party] && seat < order.Length; s++, seat++)
			{
				order[seat] = party;
			}
		}
		// Если мест больше, чем мандатов (округление), остаток отдаём последней партии.
		for (; seat < order.Length; seat++)
		{
			order[seat] = partyOrder[count - 1];
		}
		return order;
	}

	/// <summary>Условная «ось» политики: 0 — крайне левый строй, 100 — крайне правый.
	/// Нужна только для рассадки в зале слева направо.</summary>
	public static int PoliticalAxis(IdeologyType ideology)
	{
		switch (ideology)
		{
			case IdeologyType.Communism: return 5;
			case IdeologyType.Egalitarianism: return 15;
			case IdeologyType.Democracy: return 25;
			case IdeologyType.Liberalism: return 35;
			case IdeologyType.Technocracy: return 45;
			case IdeologyType.MerchantRepublic: return 50;
			case IdeologyType.FreeTerritory: return 55;
			case IdeologyType.Aristocracy: return 65;
			case IdeologyType.Monarchy: return 70;
			case IdeologyType.Theocracy: return 75;
			case IdeologyType.Empire: return 80;
			case IdeologyType.Khaganate: return 85;
			case IdeologyType.Militarism: return 90;
			case IdeologyType.Ethnonationalism: return 95;
			case IdeologyType.Tribalism: return 100;
			default: return 50;
		}
	}

	/// <summary>Цвет партии для зала: тот же, что у строя на карте идеологий.</summary>
	public static (float R, float G, float B) Color(IdeologyType ideology)
	{
		// Копия палитры Ideologies.ColorOf: ядро не тянет игровые типы,
		// а окно красится даже без обращения к игре.
		Ideologies.LayerColor(ideology, out byte r, out byte g, out byte b);
		return (r / 255f, g / 255f, b / 255f);
	}
}

/// <summary>Точка одного места в зале (условные единицы окна).</summary>
public struct ParliamentPoint
{
	public int Seat;

	public int Row;

	public float X;

	public float Y;
}
