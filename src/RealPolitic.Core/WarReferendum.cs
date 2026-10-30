using System;
using System.Collections.Generic;

namespace RealPolitic.Core;

/// <summary>
/// Этап 26: голосования по вопросу войны. Без фальшивых кубиков: голоса считаются
/// по НАСТОЯЩИМ живым юнитам (выборка из игры — см. KingdomPolitics.LastVoterSample),
/// вес голоса определяется идеологией голосующего и общественной моралью державы.
/// </summary>
public static class WarReferendum
{
	/// <summary>Минимум голосующих, чтобы вече считалось состоявшимся.</summary>
	public const int Quorum = 5;

	/// <summary>Срок действия разрешения на войну после открытого голосования, лет.</summary>
	public const int AuthorizationYears = 3;

	/// <summary>Сколько лет та же держава не повторяет попытку голосования по той же цели.</summary>
	public static int CooldownYears(SimParams p) => p != null && p.ReferendumCooldownYears > 0 ? p.ReferendumCooldownYears : 10;

	/// <summary>Шанс голоса «за» по идеологии юнита (база личного мнения гражданина).</summary>
	public static float WarAppetite(IdeologyType ideology)
	{
		switch (ideology)
		{
			case IdeologyType.Militarism: return 0.85f;
			case IdeologyType.Ethnonationalism: return 0.78f;
			case IdeologyType.Communism: return 0.62f;
			case IdeologyType.Tribalism: return 0.60f;
			case IdeologyType.Aristocracy: return 0.55f;
			case IdeologyType.Monarchy: return 0.50f;
			case IdeologyType.Theocracy: return 0.48f;
			case IdeologyType.MerchantRepublic: return 0.42f;
			case IdeologyType.Technocracy: return 0.45f;
			case IdeologyType.Democracy: return 0.38f;
			case IdeologyType.Liberalism: return 0.35f;
			case IdeologyType.Egalitarianism: return 0.30f;
			case IdeologyType.Khaganate: return 0.75f;
			case IdeologyType.Empire: return 0.65f;
			case IdeologyType.FreeTerritory: return 0.45f;
			default: return 0.45f;
		}
	}

	/// <summary>Личный голос юнита: аппетит идеологии ± политический климат державы.</summary>
	public static bool VoteYes(IdeologyType ideology, KingdomPolitics kingdom, Random rng)
	{
		float appetite = WarAppetite(ideology);
		// изнурение войной и недовольство охлаждают, легитимность воодушевляет
		appetite += (kingdom.Legitimacy - 50f) / 500f;
		appetite -= kingdom.WarFatigue / 400f;
		appetite -= kingdom.AvgDissent / 10f;
		appetite = KingdomSimulation.Clamp(appetite, 0.05f, 0.95f);
		return ((rng ?? new Random(1)).NextDouble() < appetite);
	}

	/// <summary>
	/// Провести референдум/голосование палаты по войне. Возвращает исход
	/// (прошёл проход или нет), сообщает событие в шину со счётом и даёт/отказывает
	/// в действующем разрешении. Квота/кворум честные: если мало голосов —
	/// попытка признаёт несостоявшейся и не расходует срок разрешения.
	/// </summary>
	public static bool Hold(
		KingdomPolitics kingdom, string kingdomName,
		int targetId, string targetName,
		int year, Random rng, PolEventBus bus, SimParams p)
	{
		if (kingdom == null)
		{
			return false;
		}
		if (kingdom.ReferendumCooldowns.TryGetValue(targetId, out int cooldownUntil) && year < cooldownUntil)
		{
			return false; // недавно уже не вышло — пока пролежит запал
		}
		List<(long ActorId, IdeologyType Ideology)> voters = kingdom.LastVoterSample;
		if (voters == null || voters.Count < Quorum)
		{
			// Недостают данных об электорате: голосование несостоявшееся (не минус, не плюс) —
			// просто ничья. о том, что так оно и есть, летопись не узнает (ни события).
			return false;
		}
		int votesFor = 0;
		for (int i = 0; i < voters.Count; i++)
		{
			if (VoteYes(voters[i].Ideology, kingdom, rng))
			{
				votesFor++;
			}
		}
		int against = voters.Count - votesFor;
		bool authorized = votesFor > voters.Count / 2f;
		bus?.Publish(new PolEvent(PolEventType.WarReferendum, authorized ? "RP_EV_WAR_REFERENDUM_YES" : "RP_EV_WAR_REFERENDUM_NO")
		{
			Year = year,
			Severity = Severity.Major,
			KingdomId = kingdom.KingdomId
		}.With("kingdom", kingdomName ?? string.Empty)
			.With("target", targetName ?? string.Empty)
			.With("votesFor", votesFor)
			.With("votesAgainst", against)
			.With("body", ConstitutionTexts.WarBodyName(kingdom.Constitution?.WarPower ?? WarAuthority.ByRuler)));
		if (authorized)
		{
			kingdom.WarAuthorizations.Add(new WarAuthorization
			{
				TargetKingdomId = targetId,
				Year = year,
				ValidUntilYear = year + AuthorizationYears,
				VotesFor = votesFor,
				VotesAgainst = against
			});
		}
		else
		{
			// люди ответили «нет» — правитель получает издержку за то, что задавал вопрос столько всего
			kingdom.Legitimacy = KingdomSimulation.Clamp(kingdom.Legitimacy - 4f, 0f, 100f);
			kingdom.ReferendumCooldowns[targetId] = year + CooldownYears(p);
		}
		return authorized;
	}
}
