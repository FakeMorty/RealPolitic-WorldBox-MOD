using System;
using System.Collections.Generic;

namespace RealPolitic.Core;

/// <summary>
/// Состояние пакта между двумя бывшими врагами после вердикта КМУД (0.34).
/// «Только оборона»: нападать друг на друга нельзя, союзный договор держится N лет.
/// </summary>
public sealed class KmudPact
{
	/// <summary>Первая сторона.</summary>
	public int A { get; set; }

	/// <summary>Вторая сторона.</summary>
	public int B { get; set; }

	/// <summary>Год, до которого (не включая) пакт действует.</summary>
	public int UntilYear { get; set; }

	/// <summary>Соглашение уже превращено в альянс в игре.</summary>
	public bool AllianceFormed { get; set; }

	/// <summary>Действует ли пакт в указанном году.</summary>
	public bool Active(int year) => year < UntilYear;
}

/// <summary>
/// Санкции против провинившейся державы (0.34): держава теряет благосостояние,
/// пока санкции действуют.
/// </summary>
public sealed class KmudSanction
{
	/// <summary>Кого наказывают.</summary>
	public int KingdomId { get; set; }

	/// <summary>До какого года (не включая) действуют санкции.</summary>
	public int UntilYear { get; set; }

	/// <summary>Сколько держав поддержало санкции (для летописи).</summary>
	public int Supporters { get; set; }

	public bool Active(int year) => year < UntilYear;
}

/// <summary>
/// Ассоциация: 40% еды и денег державы-донора идут нуждающейся державе.
/// Держится 20 лет (0.34).
/// </summary>
public sealed class KmudAssociation
{
	public int DonorId { get; set; }

	public int RecipientId { get; set; }

	public int UntilYear { get; set; }

	/// <summary>Сколько золота в год передаётся (40% дохода донора на момент подписания).</summary>
	public float GoldPerYear { get; set; }

	/// <summary>Сколько еды в год передаётся.</summary>
	public int FoodPerYear { get; set; }

	public bool Active(int year) => year < UntilYear;
}

/// <summary>Всё, что конференция решила, в одном месте — чтобы сохранить в сейв и применить.</summary>
public sealed class KmudDecisions
{
	/// <summary>Исход самого долгого конфликта.</summary>
	public int Outcome { get; set; }

	/// <summary>Сколько лет мира выбрали жители.</summary>
	public int PeaceYears { get; set; }

	/// <summary>Наказание безнравственной державы.</summary>
	public int Punishment { get; set; }

	/// <summary>Помощь отстающей державе.</summary>
	public int Aid { get; set; }

	/// <summary>Кого наказывают (0 — никого).</summary>
	public int PunishedId { get; set; }

	/// <summary>Кому помогают (0 — никому).</summary>
	public int AidedId { get; set; }

	/// <summary>Кто платит по вердикту (репарации) и кто получает.</summary>
	public int PayerId { get; set; }

	public int PayeeId { get; set; }

	/// <summary>0.47: решение по пятому вопросу — безопасность возвращения народа.</summary>
	public int Repatriation { get; set; }

	/// <summary>0.47: кого возвращаем (род/вид).</summary>
	public string RepatriationRaceId { get; set; } = string.Empty;

	/// <summary>0.47: куда возвращаем.</summary>
	public int RepatriationToId { get; set; }

	/// <summary>0.47: сколько человек удалось вернуть (для летописи).</summary>
	public int RepatriatedCount { get; set; }

	/// <summary>Решения уже применены в игре.</summary>
	public bool Applied { get; set; }
}

/// <summary>
/// Правила вердикта: кто платит, с кем заключается пакт «только оборона»
/// и что именно делает наказание. Только арифметика — проверяется тестами.
/// </summary>
public static class KmudVerdicts
{
	/// <summary>Сколько лет держится пакт «только оборона» после вердикта.</summary>
	public const int DefaultPactYears = 50;

	/// <summary>Сколько лет длятся санкции.</summary>
	public const int SanctionYears = 20;

	/// <summary>Сколько лет держится ассоциация (40% еды и денег).</summary>
	public const int AssociationYears = 20;

	/// <summary>Доля дохода и еды, которая уходит нуждающейся державе при ассоциации.</summary>
	public const float AssociationShare = 0.4f;

	/// <summary>Сколько золота в год платится по репарациям за проигранный конфликт.</summary>
	public const float ReparationsPerYear = 25f;

	/// <summary>Сколько лет платятся репарации.</summary>
	public const int ReparationsYears = 10;

	/// <summary>
	/// Кто кому платит по итогу конфликта.
	/// Победа агрессора — платит защитник; победа защитника — платит агрессор;
	/// белый мир — никто не платит.
	/// </summary>
	public static void ResolvePayer(KmudAgenda agenda, int outcome, KmudDecisions decisions)
	{
		if (agenda == null || decisions == null)
		{
			return;
		}
		decisions.Outcome = outcome;
		decisions.PayerId = 0;
		decisions.PayeeId = 0;
		if (!agenda.HasConflict)
		{
			return;
		}
		if (outcome == KmudOptions.OutcomeAggressor)
		{
			decisions.PayerId = agenda.DefenderId;
			decisions.PayeeId = agenda.AggressorId;
		}
		else if (outcome == KmudOptions.OutcomeDefender)
		{
			decisions.PayerId = agenda.AggressorId;
			decisions.PayeeId = agenda.DefenderId;
		}
	}

	/// <summary>
	/// Пакт «только оборона»: две державы самого долгого конфликта на 50 лет
	/// не могут нападать друг на друга, но обязаны защищать одна другую.
	/// Белый мир без такого пакта означал бы новую войну сразу после конференции.
	/// </summary>
	public static KmudPact? CreatePact(KmudAgenda agenda, int year, int pactYears = DefaultPactYears)
	{
		if (agenda == null || !agenda.HasConflict || agenda.AggressorId == agenda.DefenderId)
		{
			return null;
		}
		return new KmudPact
		{
			A = Math.Min(agenda.AggressorId, agenda.DefenderId),
			B = Math.Max(agenda.AggressorId, agenda.DefenderId),
			UntilYear = year + Math.Max(1, pactYears)
		};
	}

	/// <summary>Нужно ли менять строй по решению о наказании.</summary>
	public static bool ChangesIdeology(int punishment)
	{
		return punishment == KmudOptions.PunishKillAndReform
			|| punishment == KmudOptions.PunishReformOnly;
	}

	/// <summary>Нужно ли казнить правителя.</summary>
	public static bool ExecutesRuler(int punishment)
	{
		return punishment == KmudOptions.PunishKillAndReform;
	}

	/// <summary>
	/// Снимаются ли обвинения с наказанной державы (решение владельца от 29.10.2026:
	/// «после КМУД если агрессивное государство наказали, то обвинения снимаются»).
	/// Если жители выбрали «ничего не делать» — обвинения остаются: наказания не было.
	/// </summary>
	public static bool PardonsAccusations(int punishment)
	{
		return punishment == KmudOptions.PunishKillAndReform
			|| punishment == KmudOptions.PunishReformOnly
			|| punishment == KmudOptions.PunishSanctions;
	}

	/// <summary>0.47: возвращение народа берут под охрану КМУД (никто не нападает на принимающую державу).</summary>
	public static bool GuardsRepatriation(int decision)
	{
		return decision == KmudOptions.RepatGuard;
	}

	/// <summary>0.47: на дорогу выделяют деньги и еду.</summary>
	public static bool FundsRepatriation(int decision)
	{
		return decision == KmudOptions.RepatFund;
	}

	/// <summary>0.47: народ вообще возвращают (любой вариант, кроме «запретить»).</summary>
	public static bool AllowsRepatriation(int decision)
	{
		return decision == KmudOptions.RepatGuard
			|| decision == KmudOptions.RepatFund
			|| decision == KmudOptions.RepatAllow;
	}

	/// <summary>0.47: сколько лет держится охрана принимающей державы.</summary>
	public const int RepatriationGuardYears = 20;

	/// <summary>0.47: сколько человек одного рода возвращаем за один съезд.</summary>
	public const int RepatriationPeopleToMove = 25;

	/// <summary>0.47: сколько золота в год идёт на дорогу возвращающегося народа.</summary>
	public const float RepatriationFundPerYear = 15f;

	/// <summary>0.47: сколько лет идут эти выплаты.</summary>
	public const int RepatriationFundYears = 5;

	/// <summary>Нужны ли санкции.</summary>
	public static bool AppliesSanctions(int punishment)
	{
		return punishment == KmudOptions.PunishSanctions;
	}

	/// <summary>
	/// Новый строй для наказанной державы: строй, которого держится большинство
	/// держав мира. Так «смена строя» — это воля большинства, а не выдумка мода.
	/// Если большинство не определено, держава становится республикой.
	/// </summary>
	public static IdeologyType MajorityIdeology(IReadOnlyList<IdeologyType> ideologies)
	{
		if (ideologies == null || ideologies.Count == 0)
		{
			return IdeologyType.Democracy;
		}
		Dictionary<IdeologyType, int> counts = new Dictionary<IdeologyType, int>();
		for (int i = 0; i < ideologies.Count; i++)
		{
			counts.TryGetValue(ideologies[i], out int count);
			counts[ideologies[i]] = count + 1;
		}
		int best = 0;
		IdeologyType winner = IdeologyType.Democracy;
		foreach (KeyValuePair<IdeologyType, int> pair in counts)
		{
			if (pair.Value > best)
			{
				best = pair.Value;
				winner = pair.Key;
			}
		}
		return winner;
	}
}
