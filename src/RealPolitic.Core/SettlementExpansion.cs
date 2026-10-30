using System;

namespace RealPolitic.Core;

/// <summary>
/// 0.46: расселение — державы сами основывают новые города на свободной земле.
///
/// Владелец 29.10.2026 (скриншот): огромные пространства стоят пустыми, «никто туда
/// свой город поставить не хочет». В самой игре так и есть: поселенцы идут только
/// туда, куда их тянет движок, а большие пустые равнины между державами остаются ничьими.
///
/// Правило простое и понятное игроку:
///   • держава закладывает город, только если ей есть чем: люди, еда, казна, мир;
///   • не чаще, чем раз в N лет (иначе карта превратится в ковёр из городов);
///   • у державы не больше предела городов (он растёт с населением);
///   • воевать и голодать нельзя — тогда не до новых городов.
/// Решение принимает ядро (тестируется), а игру двигает игровой слой.
/// </summary>
public static class SettlementExpansion
{
	/// <summary>Минимум жителей, чтобы отправить поселенцев.</summary>
	public const int MinPopulation = 45;

	/// <summary>Минимум еды (благосостояние тоже учитываем), чтобы не основывать город в голод.</summary>
	public const float MinProsperity = 35f;

	/// <summary>Сколько лет между новыми городами одной державы.</summary>
	public const int CooldownYears = 12;

	/// <summary>Предел городов державы: 3 + население / 120.</summary>
	public static int CityLimit(int population)
	{
		if (population <= 0)
		{
			return 0;
		}
		return 3 + population / 120;
	}

	/// <summary>Пора ли основывать новый город. <paramref name="freeLand"/> — сколько
	/// свободных пригодных клеток рядом с державой насчитал игровой слой.</summary>
	public static bool ShouldFound(KingdomPolitics state, KingdomInput input, int freeLand, int year,
		int lastFoundedYear, SimParams p, out string why)
	{
		why = null;
		if (state == null)
		{
			why = "державы нет в памяти мода";
			return false;
		}
		if (p != null && !p.EnableSettlementExpansion)
		{
			why = "расселение выключено в настройках";
			return false;
		}
		if (input.Cities == null || input.Cities.Count == 0)
		{
			why = "у державы нет городов";
			return false;
		}
		if (input.AtWar || state.InWar)
		{
			why = "держава воюет";
			return false;
		}
		if (state.Starving || input.Starving)
		{
			why = "держава голодает";
			return false;
		}
		if (input.Population < MinPopulation)
		{
			why = "мало людей (" + input.Population + " из " + MinPopulation + ")";
			return false;
		}
		float prosperity = state.Economy?.Prosperity ?? 50f;
		if (prosperity < MinProsperity)
		{
			why = "хозяйство слабое (благосостояние " + (int)prosperity + "%)";
			return false;
		}
		int limit = CityLimit(input.Population);
		if (input.Cities.Count >= limit)
		{
			why = "городов уже " + input.Cities.Count + " при пределе " + limit;
			return false;
		}
		if (year - lastFoundedYear < CooldownYears)
		{
			why = "недавно уже закладывали город (" + (year - lastFoundedYear) + " лет назад)";
			return false;
		}
		if (freeLand < 40)
		{
			why = "рядом нет свободной земли (" + freeLand + " клеток из 40)";
			return false;
		}
		return true;
	}
}
