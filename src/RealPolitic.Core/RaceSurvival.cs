using System;
using System.Collections.Generic;

namespace RealPolitic.Core;

/// <summary>Что держава делает со своими малыми народами.</summary>
public enum RacePolicy
{
	/// <summary>Не замечает: как пойдёт, так пойдёт.</summary>
	Ignore = 0,

	/// <summary>Защищает: даёт льготы, не даёт вытеснить, разрешает жить по-своему.</summary>
	Protect = 1,

	/// <summary>Притесняет: один народ — хозяин, остальные должны растворяться.</summary>
	Oppress = 2
}

/// <summary>Сколько жителей какого рода живёт в державе.</summary>
public sealed class RaceShare
{
	public string RaceId { get; set; } = string.Empty;

	public string RaceName { get; set; } = string.Empty;

	public int Population { get; set; }
}

/// <summary>
/// 0.46: борьба малых народов за сохранение.
///
/// Владелец 29.10.2026: начал мир тремя расами по двое — люди, лисы, куры.
/// «В итоге все вымерли кроме лисов… Пусть сами расы будут пытаться бороться
/// за своё сохранение. И государство им может в этом помогать/мешать/ничего не делать».
///
/// Как это работает здесь. Ядро не умеет менять саму игру (кто с кем плодится — движок),
/// но оно умеет три вещи, и все три реально видны в мире:
///   1) замечает, что род стал малым (меньше доли от населения державы) — и включает
///      для него «борьбу за сохранение»: недовольство, требования, желание уехать;
///   2) выбирает политику державы по строю и конституции: защищать, притеснять
///      или не вмешиваться (этнонационализм притесняет, эгалитаризм и демократия
///      защищают, племя и каганат не замечают);
///   3) считает, куда этому роду стоит бежать: держава, которая его терпит.
/// Дальше вступает переселение (MigrationRules) — и малый народ физически
/// переезжает туда, где его не вытесняют. Так раса не исчезает, а расходится.
/// </summary>
public static class RaceSurvival
{
	/// <summary>Доля населения, ниже которой род считается малым и начинает бороться.</summary>
	public const float EndangeredShare = 0.20f;

	/// <summary>Меньше этого числа жителей — род на грани исчезновения.</summary>
	public const int EndangeredPopulation = 12;

	/// <summary>Политика державы по отношению к малому народу.</summary>
	public static RacePolicy PolicyOf(KingdomPolitics state)
	{
		if (state == null)
		{
			return RacePolicy.Ignore;
		}
		switch (state.Ideology)
		{
			case IdeologyType.Ethnonationalism:
				return RacePolicy.Oppress;
			case IdeologyType.Egalitarianism:
			case IdeologyType.Democracy:
			case IdeologyType.Liberalism:
			case IdeologyType.MerchantRepublic:
			case IdeologyType.Technocracy:
				return RacePolicy.Protect;
			case IdeologyType.Nationalism:
				// 0.49: гражданский национализм спорит о государстве, а не о крови.
				return RacePolicy.Ignore;
			case IdeologyType.Tribalism:
			case IdeologyType.Khaganate:
			case IdeologyType.Militarism:
			case IdeologyType.FreeTerritory:
				return RacePolicy.Ignore;
		}
		// Монархия, аристократия, теократия, империя, коммунизм: смотрим на конституцию.
		Constitution c = state.Constitution;
		if (c == null)
		{
			return RacePolicy.Ignore;
		}
		// Веротерпимость и всеобщее избирательное право — признак того, что чужих не гнобят.
		if (c.Faith == FaithLaw.Tolerance && c.Franchise == Franchise.Universal)
		{
			return RacePolicy.Protect;
		}
		if (c.Faith == FaithLaw.Purity && c.Franchise == Franchise.Clan)
		{
			return RacePolicy.Oppress;
		}
		return RacePolicy.Ignore;
	}

	/// <summary>
	/// Насколько держава готова принять чужой род (0..1) — то же число,
	/// что решает судьбу заявления на переезд.
	/// </summary>
	public static float Tolerance(RacePolicy policy, bool sameTitularRace, float legitimacy)
	{
		switch (policy)
		{
			case RacePolicy.Protect:
				return 0.9f;
			case RacePolicy.Oppress:
				return sameTitularRace ? 0.85f : 0.05f;
			default:
				// Никакой политики: чужих принимают, но без восторга.
				return sameTitularRace ? 0.8f : 0.45f + Math.Max(0f, Math.Min(0.2f, (legitimacy - 50f) / 250f));
		}
	}

	/// <summary>Считается ли род малым (борется за сохранение) в этой державе.</summary>
	public static bool IsEndangered(int population, int totalPopulation)
	{
		if (population <= 0)
		{
			return false; // род уже ушёл — это не «борьба», это факт
		}
		if (population <= EndangeredPopulation)
		{
			return true;
		}
		return totalPopulation > 0 && (float)population / totalPopulation < EndangeredShare;
	}

	/// <summary>
	/// Давление на жителя малого рода (0..1): насколько ему хочется уехать
	/// и насколько он готов отстаивать свои права.
	/// При защите давление меньше, при притеснении — максимальное.
	/// </summary>
	public static float Pressure(RacePolicy policy, bool endangered, int population, int totalPopulation)
	{
		float share = totalPopulation > 0 ? (float)population / totalPopulation : 0f;
		// Чем меньше доля рода, тем сильнее страх исчезнуть.
		float fear = endangered ? 0.35f + (1f - Math.Min(1f, share / EndangeredShare)) * 0.35f : 0.1f;
		switch (policy)
		{
			case RacePolicy.Oppress:
				return Math.Min(1f, fear + 0.45f);
			case RacePolicy.Protect:
				return Math.Max(0f, fear - 0.25f);
			default:
				return fear;
		}
	}

	/// <summary>
	/// Доля рода среди населения державы (0..1). Нужна и для политики, и для летописи.
	/// </summary>
	public static float Share(IReadOnlyList<RaceShare> races, string raceId)
	{
		if (races == null || races.Count == 0 || string.IsNullOrEmpty(raceId))
		{
			return 0f;
		}
		int total = 0;
		int mine = 0;
		for (int i = 0; i < races.Count; i++)
		{
			total += races[i].Population;
			if (races[i].RaceId == raceId)
			{
				mine += races[i].Population;
			}
		}
		return total > 0 ? (float)mine / total : 0f;
	}

	/// <summary>Самый малый (но ещё живой) род державы — тот, кому нужна защита.</summary>
	public static RaceShare Smallest(IReadOnlyList<RaceShare> races)
	{
		if (races == null)
		{
			return null;
		}
		RaceShare smallest = null;
		for (int i = 0; i < races.Count; i++)
		{
			RaceShare r = races[i];
			if (r == null || r.Population <= 0)
			{
				continue;
			}
			if (smallest == null || r.Population < smallest.Population)
			{
				smallest = r;
			}
		}
		return smallest;
	}

}
