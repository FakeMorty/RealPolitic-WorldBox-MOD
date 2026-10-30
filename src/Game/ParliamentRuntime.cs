using System;
using System.Collections.Generic;
using RealPolitic.Core;

namespace RealPolitic;

/// <summary>
/// 0.46: уведомления о выборах в парламент.
///
/// Владелец 29.10.2026: «в уведомления выносить выборы в парламент стран».
///
/// Как. Раз в год мод смотрит состав партий каждой державы. Если состав изменился
/// (партии получили другие места или сменилась правящая), в уведомления уходит строка
/// с итогом: кто получил большинство, сколько мест у остальных и кто теперь правит.
/// Сравнение по «подписи» состава, поэтому одна и та же картина не повторяется каждый год.
/// </summary>
public static class ParliamentRuntime
{
	/// <summary>
	/// 0.48: что уже сообщили по каждой державе. Владелец прогнал 1000 лет и получил
	/// 94 строки «выборы в парламент» — по одной в год, при том что партия всё время
	/// одна и та же («Совет знати»), а места гуляют 81–87. Это шум, а не событие.
	/// Теперь помним: кто правит, с каким числом мест и когда об этом говорили.
	/// </summary>
	private sealed class Notice
	{
		public int RulingIdeology = -1;
		public int RulingSeats;
		public int Year;
		public int LastSeats;

		/// <summary>
		/// 0.48.4: о каких партиях уже сообщали и когда. Нужно против «пинг-понга»:
		/// на катке 29.10.2026 у «Степняков» правящая партия прыгала
		/// Совет старейшин → Партия граждан → Торгово-купеческая → Партия граждан,
		/// и каждая смена считалась новостью — четыре уведомления за шесть лет.
		/// </summary>
		public readonly Dictionary<int, int> ReportedIdeologies = new Dictionary<int, int>();
	}

	private static readonly Dictionary<int, Notice> _lastNotice = new Dictionary<int, Notice>();

	/// <summary>Не чаще одного сообщения о выборах в столько лет на державу.</summary>
	public const int NoticeCooldownYears = 20;

	/// <summary>
	/// 0.48.4: даже смена правящей партии — не чаще раза в столько лет.
	/// Иначе при «пинг-понге» между двумя партиями летопись тонет (катка 29.10.2026).
	/// </summary>
	public const int PartyChangeCooldownYears = 10;

	/// <summary>Партия, о которой уже сообщали в эти годы, не считается новой (пинг-понг).</summary>
	public const int SamePartyMemoryYears = 40;

	/// <summary>Насколько должны измениться места, чтобы это считалось новостью.</summary>
	public const int SeatsChangeToNotify = 10;

	/// <summary>Какой год мод уже обработал (после загрузки старого сохранения — сбрасываем память).</summary>
	private static int _lastYear = int.MinValue;

	/// <summary>Забыть прошлые выборы (новый мир, загрузка сохранения).</summary>
	public static void Reset()
	{
		_lastNotice.Clear();
		_lastYear = int.MinValue;
	}

	public static void AnnualTick(PoliticalWorld world, int year)
	{
		try
		{
			if (world == null || world.Bus == null)
			{
				return;
			}
			// Мир откатили назад (загрузили сохранение) — прежние подписи больше не годятся.
			if (year < _lastYear)
			{
				Reset();
			}
			_lastYear = year;
			foreach (KingdomPolitics state in world.Kingdoms)
			{
				if (state == null || !CivicSocietyEngine.HasParliamentaryParties(state) || state.Parties == null
					|| state.Parties.Count == 0)
				{
					continue;
				}
				PoliticalParty ruling = Ruling(state);
				if (ruling == null)
				{
					continue;
				}
				if (!_lastNotice.TryGetValue(state.KingdomId, out Notice notice))
				{
					// Первый раз просто запоминаем картину: иначе при загрузке мира
					// прилетит пачка «выборов», которых не было.
					Notice fresh = new Notice
					{
						RulingIdeology = (int)ruling.Ideology,
						RulingSeats = ruling.SeatsPercent,
						Year = year,
						LastSeats = ruling.SeatsPercent
					};
					fresh.ReportedIdeologies[(int)ruling.Ideology] = year;
					_lastNotice[state.KingdomId] = fresh;
					continue;
				}
				if (!WorthNoticing(notice, ruling, year))
				{
					notice.LastSeats = ruling.SeatsPercent;
					continue;
				}
				notice.RulingIdeology = (int)ruling.Ideology;
				notice.RulingSeats = ruling.SeatsPercent;
				notice.LastSeats = ruling.SeatsPercent;
				notice.Year = year;
				notice.ReportedIdeologies[(int)ruling.Ideology] = year;
				Publish(world, state, year);
			}
		}
		catch (Exception)
		{
		}
	}

	/// <summary>Правящая партия державы.</summary>
	private static PoliticalParty Ruling(KingdomPolitics state)
	{
		PoliticalParty ruling = null;
		for (int i = 0; i < state.Parties.Count; i++)
		{
			PoliticalParty party = state.Parties[i];
			if (party == null)
			{
				continue;
			}
			if (party.IsRuling)
			{
				return party;
			}
			if (ruling == null || party.SeatsPercent > ruling.SeatsPercent)
			{
				ruling = party;
			}
		}
		return ruling;
	}

	/// <summary>
	/// Стоит ли вообще писать об этих выборах.
	/// Новость — это смена правящей партии или большая перемена в местах.
	/// Мелкое дрожание 85→84→87 новостью не считается, иначе летопись тонет.
	/// </summary>
	private static bool WorthNoticing(Notice notice, PoliticalParty ruling, int year)
	{
		if (notice == null || ruling == null)
		{
			return false;
		}
		// Смена правящей партии — новость, но с двумя оговорками (0.48.4):
		//   • не чаще раза в PartyChangeCooldownYears лет;
		//   • если эта партия уже была у власти на памяти мода (летопись уже писала
		//     о ней) — это «пинг-понг» двух партий, а не новое событие.
		if (notice.RulingIdeology != (int)ruling.Ideology)
		{
			if (year - notice.Year < PartyChangeCooldownYears)
			{
				return false;
			}
			if (notice.ReportedIdeologies.TryGetValue((int)ruling.Ideology, out int reportedYear)
				&& year - reportedYear < SamePartyMemoryYears)
			{
				return false;
			}
			return true;
		}
		if (year - notice.Year < NoticeCooldownYears)
		{
			return false;
		}
		return Math.Abs(ruling.SeatsPercent - notice.RulingSeats) >= SeatsChangeToNotify;
	}

	private static void Publish(PoliticalWorld world, KingdomPolitics state, int year)
	{
		try
		{
			PoliticalParty ruling = null;
			int rulingSeats = 0;
			int oppositionSeats = 0;
			List<string> others = new List<string>();
			for (int i = 0; i < state.Parties.Count; i++)
			{
				PoliticalParty party = state.Parties[i];
				if (party == null)
				{
					continue;
				}
				if (party.IsRuling)
				{
					ruling = party;
					rulingSeats = party.SeatsPercent;
				}
				else
				{
					oppositionSeats += party.SeatsPercent;
					others.Add(GameText.Localized(party.NameKey, party.Ideology.ToString()) + " " + party.SeatsPercent);
				}
			}
			if (ruling == null)
			{
				return;
			}
			PolEvent evt = new PolEvent(PolEventType.ParliamentElected, "RP_EV_PARLIAMENT_ELECTED") { Year = year };
			evt.Severity = Severity.Minor;
			evt.KingdomId = state.KingdomId;
			world.Bus.Publish(evt
				.With("kingdom", world.KingdomNameOf(state.KingdomId))
				.With("party", string.IsNullOrEmpty(ruling.Name)
					? GameText.Localized(ruling.NameKey, ruling.Ideology.ToString())
					: ruling.Name)
				.With("ideology", GameText.Localized(Ideologies.NameKey(ruling.Ideology), ruling.Ideology.ToString()))
				.With("seats", rulingSeats)
				.With("opposition", oppositionSeats)
				.With("others", string.Join(", ", others)));
		}
		catch (Exception)
		{
		}
	}
}
