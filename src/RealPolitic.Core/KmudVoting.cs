using System;
using System.Collections.Generic;

namespace RealPolitic.Core;

/// <summary>Пять вопросов конференции КМУД (0.33; пятый добавлен в 0.47).</summary>
public enum KmudQuestion
{
	/// <summary>Как завершить долгий конфликт.</summary>
	ConflictOutcome = 0,

	/// <summary>Сколько лет полного мира установить.</summary>
	PeaceYears = 1,

	/// <summary>Что сделать с самой безнравственной державой.</summary>
	Punish = 2,

	/// <summary>Чем помочь самой отстающей державе.</summary>
	Aid = 3,

	/// <summary>
	/// 0.47: обеспечить безопасность возвращения малого народа туда, куда он сам
	/// захотел переехать (решение владельца от 29.10.2026).
	/// </summary>
	Repatriation = 4
}

/// <summary>Варианты ответов. Числа — это и есть «id варианта» в бюллетене.</summary>
public static class KmudOptions
{
	// Вопрос 1: итог долгого конфликта.
	public const int OutcomeAggressor = 1;
	public const int OutcomeDefender = 2;
	public const int OutcomeWhitePeace = 3;

	// Вопрос 2: годы мира.
	public const int Peace0 = 0;
	public const int Peace10 = 10;
	public const int Peace20 = 20;
	public const int Peace30 = 30;

	// Вопрос 3: наказание.
	public const int PunishKillAndReform = 1;
	public const int PunishReformOnly = 2;
	public const int PunishSanctions = 3;
	public const int PunishNothing = 4;

	// Вопрос 4: помощь.
	public const int AidMoney = 1;
	public const int AidFood = 2;
	public const int AidAssociation = 3;
	public const int AidNothing = 4;

	// Вопрос 5 (0.47): безопасность возвращения народа.
	public const int RepatGuard = 1;
	public const int RepatFund = 2;
	public const int RepatAllow = 3;
	public const int RepatDeny = 4;

	/// <summary>Все варианты вопроса — в том порядке, как они стоят в окне.</summary>
	public static int[] For(KmudQuestion question)
	{
		switch (question)
		{
			case KmudQuestion.ConflictOutcome:
				return new[] { OutcomeAggressor, OutcomeDefender, OutcomeWhitePeace };
			case KmudQuestion.PeaceYears:
				return new[] { Peace0, Peace10, Peace20, Peace30 };
			case KmudQuestion.Punish:
				return new[] { PunishKillAndReform, PunishReformOnly, PunishSanctions, PunishNothing };
			case KmudQuestion.Aid:
				return new[] { AidMoney, AidFood, AidAssociation, AidNothing };
			case KmudQuestion.Repatriation:
				return new[] { RepatGuard, RepatFund, RepatAllow, RepatDeny };
			default:
				return new int[0];
		}
	}

	/// <summary>Ключ локализации названия варианта.</summary>
	public static string LocKey(KmudQuestion question, int option)
	{
		switch (question)
		{
			case KmudQuestion.ConflictOutcome:
				switch (option)
				{
					case OutcomeAggressor: return "RP32_OPT_OUTCOME_AGGRESSOR";
					case OutcomeDefender: return "RP32_OPT_OUTCOME_DEFENDER";
					case OutcomeWhitePeace: return "RP32_OPT_OUTCOME_WHITE";
				}
				break;
			case KmudQuestion.PeaceYears:
				return "RP32_OPT_PEACE_" + option;
			case KmudQuestion.Punish:
				switch (option)
				{
					case PunishKillAndReform: return "RP32_OPT_PUNISH_KILL";
					case PunishReformOnly: return "RP32_OPT_PUNISH_REFORM";
					case PunishSanctions: return "RP32_OPT_PUNISH_SANCTIONS";
					case PunishNothing: return "RP32_OPT_PUNISH_NONE";
				}
				break;
			case KmudQuestion.Aid:
				switch (option)
				{
					case AidMoney: return "RP32_OPT_AID_MONEY";
					case AidFood: return "RP32_OPT_AID_FOOD";
					case AidAssociation: return "RP32_OPT_AID_ASSOCIATION";
					case AidNothing: return "RP32_OPT_AID_NONE";
				}
				break;
			case KmudQuestion.Repatriation:
				switch (option)
				{
					case RepatGuard: return "RP47_OPT_REPAT_GUARD";
					case RepatFund: return "RP47_OPT_REPAT_FUND";
					case RepatAllow: return "RP47_OPT_REPAT_ALLOW";
					case RepatDeny: return "RP47_OPT_REPAT_DENY";
				}
				break;
		}
		return "RP32_OPT_UNKNOWN";
	}
}

/// <summary>Повестка конференции: о ком и о чём именно голосуют.</summary>
public sealed class KmudAgenda
{
	/// <summary>Держава-агрессор в самом долгом конфликте (0 — конфликта нет).</summary>
	public int AggressorId { get; set; }

	/// <summary>Держава-защитник в самом долгом конфликте (0 — конфликта нет).</summary>
	public int DefenderId { get; set; }

	/// <summary>Сколько лет длится этот конфликт.</summary>
	public int ConflictYears { get; set; }

	/// <summary>Самая безнравственная держава по подсчёту мода (0 — не выбрана).</summary>
	public int WorstId { get; set; }

	/// <summary>За что именно её признали худшей (готовые причины).</summary>
	public readonly List<string> WorstReasons = new List<string>();

	/// <summary>Самая отстающая держава (0 — не выбрана).</summary>
	public int PoorestId { get; set; }

	/// <summary>Признак, по которому её считают отстающей.</summary>
	public string PoorestReason { get; set; } = string.Empty;

	public bool HasConflict => AggressorId > 0 && DefenderId > 0;

	// ---- 0.47: пятый вопрос — возвращение малого народа.

	/// <summary>Род (вид), о котором идёт речь в пятом вопросе.</summary>
	public string RepatriationRaceId { get; set; } = string.Empty;

	/// <summary>Название рода так, как его зовут в игре («лисы», «куры», «люди»).</summary>
	public string RepatriationRaceName { get; set; } = string.Empty;

	/// <summary>Держава, куда народ хочет вернуться (0 — вопроса нет).</summary>
	public int RepatriationToId { get; set; }

	/// <summary>Держава, откуда народ уходит (0 — не выбрана).</summary>
	public int RepatriationFromId { get; set; }

	/// <summary>Есть ли о чём спрашивать: выбран народ и куда возвращаться.</summary>
	public bool HasRepatriation => RepatriationToId > 0 && !string.IsNullOrEmpty(RepatriationRaceId);
}

/// <summary>Один бюллетень: сколько жителей выбрали каждый вариант.</summary>
public sealed class KmudBallotResult
{
	public KmudQuestion Question { get; }

	/// <summary>Голоса по вариантам: «id варианта → сколько жителей».</summary>
	public readonly Dictionary<int, int> Counts = new Dictionary<int, int>();

	/// <summary>Сколько жителей всего проголосовало по этому вопросу.</summary>
	public int Voters { get; set; }

	/// <summary>Победивший вариант (0 — голосов нет).</summary>
	public int Winner { get; set; }

	/// <summary>Совпал ли первый результат со вторым (ничья).</summary>
	public bool Tied { get; set; }

	/// <summary>Второй по популярности вариант (для ничьей в летописи).</summary>
	public int RunnerUp { get; set; }

	public KmudBallotResult(KmudQuestion question)
	{
		Question = question;
		int[] options = KmudOptions.For(question);
		for (int i = 0; i < options.Length; i++)
		{
			Counts[options[i]] = 0;
		}
	}

	public int CountOf(int option)
	{
		return Counts.TryGetValue(option, out int value) ? value : 0;
	}

	public void Add(int option, int amount = 1)
	{
		if (!Counts.ContainsKey(option) || amount <= 0)
		{
			return;
		}
		Counts[option] += amount;
		Voters += amount;
	}

	/// <summary>
	/// Подсчёт победителя. Ничья не разрешается выдумкой: побеждает самый мирный
	/// из равных вариантов (меньшее зло), а факт ничьей остаётся в летописи.
	/// </summary>
	public void Tally()
	{
		int best = 0;
		int bestCount = 0;
		int runnerOption = 0;
		int runnerCount = 0;
		foreach (KeyValuePair<int, int> pair in Counts)
		{
			if (pair.Value > bestCount || (pair.Value == bestCount && pair.Value > 0 && PreferOnTie(Question, pair.Key, best)))
			{
				if (best != pair.Key)
				{
					runnerOption = best;
					runnerCount = bestCount;
				}
				best = pair.Key;
				bestCount = pair.Value;
			}
			else if (pair.Value > runnerCount && pair.Key != best)
			{
				runnerOption = pair.Key;
				runnerCount = pair.Value;
			}
		}
		Winner = bestCount > 0 ? best : 0;
		RunnerUp = runnerOption;
		Tied = bestCount > 0 && runnerCount == bestCount;
	}

	/// <summary>При равенстве голосов выбираем более мирный и мягкий вариант.</summary>
	private static bool PreferOnTie(KmudQuestion question, int candidate, int current)
	{
		switch (question)
		{
			case KmudQuestion.ConflictOutcome:
				// Белый мир лучше победы одной из сторон.
				return Peacefulness(candidate) > Peacefulness(current);
			case KmudQuestion.PeaceYears:
				return candidate > current;
			case KmudQuestion.Punish:
				return SeverityRank(candidate) < SeverityRank(current);
			case KmudQuestion.Aid:
				// При равенстве помогаем делом, а не «ничего не делать».
				return candidate != KmudOptions.AidNothing && current == KmudOptions.AidNothing;
			default:
				return false;
		}
	}

	private static int Peacefulness(int option)
	{
		switch (option)
		{
			case KmudOptions.OutcomeWhitePeace: return 3;
			case KmudOptions.OutcomeDefender: return 2;
			case KmudOptions.OutcomeAggressor: return 1;
			default: return 0;
		}
	}

	private static int SeverityRank(int option)
	{
		switch (option)
		{
			case KmudOptions.PunishKillAndReform: return 3;
			case KmudOptions.PunishReformOnly: return 2;
			case KmudOptions.PunishSanctions: return 1;
			default: return 0;
		}
	}
}

/// <summary>Все четыре бюллетеня конференции и список уже проголосовавших жителей.</summary>
public sealed class KmudVotingState
{
	/// <summary>Бюллетени по вопросам.</summary>
	public readonly Dictionary<KmudQuestion, KmudBallotResult> Ballots = new Dictionary<KmudQuestion, KmudBallotResult>();

	/// <summary>Кто уже проголосовал (id жителей) — один житель, один голос на каждый вопрос.</summary>
	public readonly HashSet<int> Voted = new HashSet<int>();

	/// <summary>Итоги уже подсчитаны и оглашены.</summary>
	public bool Finalized { get; set; }

	public KmudVotingState()
	{
		foreach (KmudQuestion question in Enum.GetValues(typeof(KmudQuestion)))
		{
			Ballots[question] = new KmudBallotResult(question);
		}
	}

	public KmudBallotResult Of(KmudQuestion question)
	{
		return Ballots[question];
	}

	/// <summary>Самый популярный вариант ответа (0 — ещё никто не голосовал).</summary>
	public int Leading(KmudQuestion question)
	{
		KmudBallotResult ballot = Of(question);
		int best = 0;
		int bestCount = 0;
		foreach (KeyValuePair<int, int> pair in ballot.Counts)
		{
			if (pair.Value > bestCount)
			{
				bestCount = pair.Value;
				best = pair.Key;
			}
		}
		return best;
	}

	/// <summary>Все итоги сразу — для летописи и последствий.</summary>
	public Dictionary<KmudQuestion, int> TallyAll()
	{
		Dictionary<KmudQuestion, int> winners = new Dictionary<KmudQuestion, int>();
		foreach (KeyValuePair<KmudQuestion, KmudBallotResult> pair in Ballots)
		{
			pair.Value.Tally();
			winners[pair.Key] = pair.Value.Winner;
		}
		Finalized = true;
		return winners;
	}
}
