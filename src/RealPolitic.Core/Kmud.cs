using System;
using System.Collections.Generic;

namespace RealPolitic.Core;

/// <summary>Состояние конференции КМУД (Конференция Мирного Урегулирования Дел).</summary>
public enum KmudPhase
{
	/// <summary>Ещё не созывалась.</summary>
	NotHeld = 0,

	/// <summary>Идёт: альянс действует, войны запрещены, жители голосуют.</summary>
	Convened = 1,

	/// <summary>Завершена: решения приняты, альянс распущен.</summary>
	Closed = 2
}

/// <summary>
/// Память конференции: когда созвана, сколько длится, до какого года запрещены войны.
/// Хранится в сейве вместе с политическим миром (0.32, этап 1).
/// </summary>
public sealed class KmudState
{
	/// <summary>Текущая стадия конференции.</summary>
	public KmudPhase Phase { get; set; } = KmudPhase.NotHeld;

	/// <summary>Номер съезда: 1 — первый (200-й год), 2 — через 100 лет и так далее.</summary>
	public int Round { get; set; }

	/// <summary>Год созыва (0 — не созывалась).</summary>
	public int OpenedYear { get; set; }

	/// <summary>Год, в котором конференция закрывается (OpenedYear + длительность).</summary>
	public int ClosesYear { get; set; }

	/// <summary>До какого года (не включая) действует запрет войн между участниками.</summary>
	public int TruceUntilYear { get; set; }

	/// <summary>Сколько держав вступило в конференцию.</summary>
	public int Members { get; set; }

	/// <summary>Сколько войн было прекращено при созыве.</summary>
	public int WarsEnded { get; set; }

	/// <summary>Сколько войн пришлось погасить уже во время конференции (нарушения паузы).</summary>
	public int WarsStoppedDuringPause { get; set; }

	/// <summary>Номер общего альянса КМУД в игре (0 — альянс ещё не создан).</summary>
	public int AllianceId { get; set; }

	/// <summary>Создан ли общий альянс хотя бы раз.</summary>
	public bool AllianceFormed { get; set; }

	/// <summary>Сколько раз приходилось возвращать державу в альянс (вышла сама).</summary>
	public int MembersReturned { get; set; }

	/// <summary>Повестка конференции: о каких державах идёт речь в вопросах.</summary>
	public KmudAgenda Agenda { get; set; } = new KmudAgenda();

	/// <summary>Бюллетени и список проголосовавших жителей.</summary>
	public KmudVotingState Voting { get; set; } = new KmudVotingState();

	/// <summary>Сколько лет мира выбрали жители на последнем голосовании.</summary>
	public int LastPeaceYears { get; set; }

	/// <summary>Решения последнего съезда.</summary>
	public KmudDecisions Decisions { get; set; } = new KmudDecisions();

	/// <summary>
	/// Id держав-участников последнего созыва. Нужны, чтобы запрет войн действовал
	/// не «вообще», а только между участниками съезда (0.39).
	/// </summary>
	public List<int> MemberIds { get; set; } = new List<int>();

	/// <summary>Сколько объявлений войны отбито запретом (0.39: не гасим, а не даём объявить).</summary>
	public int WarsPrevented { get; set; }

	/// <summary>Пакты «только оборона» между бывшими врагами.</summary>
	public List<KmudPact> Pacts { get; set; } = new List<KmudPact>();

	/// <summary>Действующие санкции.</summary>
	public List<KmudSanction> Sanctions { get; set; } = new List<KmudSanction>();

	/// <summary>Действующие ассоциации «40% еды и денег».</summary>
	public List<KmudAssociation> Associations { get; set; } = new List<KmudAssociation>();

	/// <summary>Итоги уже оглашены.</summary>
	public bool ResultsAnnounced { get; set; }

	/// <summary>Идёт ли сейчас конференция.</summary>
	public bool IsConvened => Phase == KmudPhase.Convened;

	/// <summary>Действует ли запрет войн в этом году (конференция была и срок ещё не вышел).</summary>
	public bool PauseActiveAt(int year)
	{
		return WasHeld && year >= OpenedYear && year < TruceUntilYear;
	}

	/// <summary>Проводилась ли конференция хоть раз (созыв был).</summary>
	public bool WasHeld => Phase != KmudPhase.NotHeld;
}

/// <summary>
/// Правила созыва КМУД. Здесь только арифметика и никакой игры — поэтому проверяется тестами
/// без запуска WorldBox.
/// </summary>
public static class KmudRules
{
	/// <summary>Год созыва по умолчанию.</summary>
	public const int DefaultYear = 200;

	/// <summary>Сколько лет идёт конференция по умолчанию.</summary>
	public const int DefaultDurationYears = 10;

	/// <summary>
	/// Пора ли созывать конференцию. Созыв один раз за мир: если она уже была (или идёт) —
	/// второй раз не собираем.
	/// </summary>
	public static bool ShouldConvene(KmudState state, SimParams? p, int year, int kingdomsWithLand)
	{
		if (state == null || p == null)
		{
			return false;
		}
		if (!p.EnableKmud)
		{
			return false;
		}
		// Конференция собирается не один раз: первый съезд и далее каждые N лет
		// (по умолчанию 200-й год, потом 300-й, 400-й и так далее).
		int interval = Math.Max(1, p.KmudIntervalYears);
		if (state.Phase == KmudPhase.Convened)
		{
			return false;
		}
		if (state.Phase == KmudPhase.Closed && year < state.OpenedYear + interval)
		{
			return false;
		}
		// Конференция имеет смысл, только если есть кого мирить.
		if (kingdomsWithLand < 2)
		{
			return false;
		}
		return year >= Math.Max(1, p.KmudYear);
	}

	/// <summary>
	/// Открывает конференцию: запоминает годы, число участников и ставит запрет войн.
	/// Возвращает событие для летописи.
	/// </summary>
	public static PolEvent Convene(KmudState state, SimParams? p, int year, int members, int warsEnded)
	{
		if (state == null)
		{
			throw new ArgumentNullException(nameof(state));
		}
		int duration = Math.Max(1, p?.KmudYears ?? DefaultDurationYears);
		int pause = Math.Max(duration, p?.KmudPauseYears ?? DefaultDurationYears);
		state.Phase = KmudPhase.Convened;
		state.Round++;
		state.OpenedYear = year;
		state.ClosesYear = year + duration;
		// Запрет войн держится, пока идёт конференция, и ещё столько лет, сколько выбрали
		// жители (это уточнит голосование; пока — базовая пауза на время конференции).
		state.TruceUntilYear = year + pause;
		state.Members = members;
		state.WarsEnded = warsEnded;
		state.WarsStoppedDuringPause = 0;
		// Каждый съезд голосует заново: старые бюллетени и повестка не переносятся.
		state.Voting = new KmudVotingState();
		state.Agenda = new KmudAgenda();
		state.ResultsAnnounced = false;
		state.LastPeaceYears = 0;
		return new PolEvent(PolEventType.KmudConvened, "RP32_EV_KMUD_CONVENED") { Year = year }
			.With("members", members)
			.With("wars_ended", warsEnded)
			.With("until", state.TruceUntilYear)
			.With("closes", state.ClosesYear);
	}

	/// <summary>Пора ли закрывать конференцию (истёк срок, а голосование завершено).</summary>
	public static bool ShouldClose(KmudState state, int year)
	{
		return state != null && state.Phase == KmudPhase.Convened && year >= state.ClosesYear;
	}

	/// <summary>
	/// Закрывает конференцию: альянс распускается, решения вступают в силу.
	/// Возвращает событие для летописи.
	/// </summary>
	public static PolEvent Close(KmudState state, int year, int chosenPeaceYears)
	{
		if (state == null)
		{
			throw new ArgumentNullException(nameof(state));
		}
		state.Phase = KmudPhase.Closed;
		// Продление мира по решению жителей: 0 — войны снова можно начинать сразу.
		if (chosenPeaceYears > 0)
		{
			int until = year + chosenPeaceYears;
			if (until > state.TruceUntilYear)
			{
				state.TruceUntilYear = until;
			}
		}
		else
		{
			// Конференция окончена и продления нет — запрет войн снимается сразу.
			state.TruceUntilYear = year;
		}
		return new PolEvent(PolEventType.KmudClosed, "RP32_EV_KMUD_CLOSED") { Year = year }
			.With("peace_years", chosenPeaceYears)
			.With("until", state.TruceUntilYear)
			.With("members", state.Members);
	}

	/// <summary>Год, до которого (не включая) действует продлённый жителями мир (0 — не продлевали).</summary>
	public static int PeaceUntil(int year, int chosenPeaceYears)
	{
		return chosenPeaceYears <= 0 ? year : year + chosenPeaceYears;
	}

	/// <summary>Действует ли сейчас запрет войн между участниками.</summary>
	public static bool PauseActive(KmudState state, int year)
	{
		return state != null
			&& state.WasHeld
			&& year >= state.OpenedYear
			&& year < state.TruceUntilYear;
	}

	/// <summary>
	/// Можно ли объявить войну между двумя державами. 0.39: вместо «погасить уже
	/// объявленную войну» мод просто не даёт её объявить — иначе летопись забивается
	/// строками «война отменена — действует запрет войн» (в партии от 28.10.2026 таких
	/// строк было 23).
	/// Возвращает true, если война запрещена, и заполняет причину.
	/// </summary>
	public static bool WarForbidden(KmudState state, int attackerId, int defenderId, int year, out string reason)
	{
		reason = string.Empty;
		if (state == null || attackerId <= 0 || defenderId <= 0 || attackerId == defenderId)
		{
			return false;
		}
		if (!state.WasHeld)
		{
			return false;
		}
		// Пакт «только оборона»: нападать друг на друга нельзя до конца срока пакта.
		if (state.Pacts != null)
		{
			for (int i = 0; i < state.Pacts.Count; i++)
			{
				KmudPact pact = state.Pacts[i];
				if (pact == null || year >= pact.UntilYear)
				{
					continue;
				}
				int lo = Math.Min(attackerId, defenderId);
				int hi = Math.Max(attackerId, defenderId);
				if (pact.A == lo && pact.B == hi)
				{
					reason = "пакт «только оборона» до " + pact.UntilYear + " года";
					return true;
				}
			}
		}
		// Полный мир, выбранный жителями: войны запрещены участникам съезда.
		if (!state.PauseActiveAt(year))
		{
			return false;
		}
		if (!IsMember(state, attackerId) || !IsMember(state, defenderId))
		{
			return false;
		}
		reason = "запрет войн до " + state.TruceUntilYear + " года";
		return true;
	}

	/// <summary>Входит ли держава в последний созыв (если список участников не сохранён — считаем, что да).</summary>
	public static bool IsMember(KmudState state, int kingdomId)
	{
		if (state == null)
		{
			return false;
		}
		if (state.MemberIds == null || state.MemberIds.Count == 0)
		{
			// Старые сохранения (до 0.39) списка не знают — не ломаем им запрет.
			return true;
		}
		for (int i = 0; i < state.MemberIds.Count; i++)
		{
			if (state.MemberIds[i] == kingdomId)
			{
				return true;
			}
		}
		return false;
	}

	/// <summary>Все пары держав-участников — чтобы поставить запрет войн каждой паре.</summary>
	public static List<int[]> AllPairs(IReadOnlyList<int> kingdomIds)
	{
		List<int[]> pairs = new List<int[]>();
		if (kingdomIds == null)
		{
			return pairs;
		}
		for (int i = 0; i < kingdomIds.Count; i++)
		{
			for (int j = i + 1; j < kingdomIds.Count; j++)
			{
				if (kingdomIds[i] != kingdomIds[j])
				{
					pairs.Add(new[] { kingdomIds[i], kingdomIds[j] });
				}
			}
		}
		return pairs;
	}
}
