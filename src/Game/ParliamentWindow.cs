using System;
using System.Collections.Generic;
using System.Reflection;
using System.Text;
using NeoModLoader.services;
using RealPolitic.Core;

namespace RealPolitic;

/// <summary>
/// 0.46: парламент державы — окно со 100 местами полукругом.
///
/// Владелец 29.10.2026: «добавить кнопку где будет отрисованы места в парламенте.
/// Пусть это будет 100 мест… красивым полукругом… посмотреть на цвет, спуститься
/// вниз и посмотреть, что под этим цветом сидит партия такая-то с такой-то идеологией».
///
/// Как устроено:
///   • места считает ядро (Parliament.Allocate — крупнейшие остатки, сумма всегда 100);
///   • координаты полукруга тоже считает ядро (Parliament.Arc), окно только рисует точки;
///   • под полукругом — список партий: цветной квадрат, название партии, строй, число мест;
///   • клик по месту (или по строке списка) показывает, кто на нём сидит.
/// Кнопка «Парламент» живёт в оглавлении мода (вкладка Real Politic).
/// </summary>
public static class ParliamentWindow
{
	private const string WindowId = "realpolitic_parliament";
	private const string TitleKey = "RP_PARL_WIN_TITLE";

	private static object _window;
	private static object _content;
	private static int _kingdomId;
	private static bool _disabled;

	/// <summary>Какое место выбрано (для строки «вы сидите на месте N»).</summary>
	private static int _selectedSeat = -1;

	public static bool Prewarm()
	{
		if (_window != null)
		{
			return true;
		}
		try
		{
			_window = ModWindow.Create(WindowId, TitleKey, GameText.Localized(TitleKey, "Парламент"),
				ModIcons.VotesKey, out object content);
			if (_window == null)
			{
				return false;
			}
			_content = content ?? _window;
			ModUi.SetupAutoLayout(_content, 2f, 6f);
			return true;
		}
		catch (Exception ex)
		{
			Disable("подготовка окна парламента: " + ex.Message);
			return false;
		}
	}

	public static void AttachCountry(int kingdomId)
	{
		_kingdomId = kingdomId;
	}

	public static void Open()
	{
		try
		{
			if (_disabled || RealPoliticMod.World == null)
			{
				return;
			}
			if (_window == null && !Prewarm())
			{
				return;
			}
			if (_window == null || _content == null)
			{
				return;
			}
			_selectedSeat = -1;
			Render();
			ModWindow.Show(WindowId, _window);
		}
		catch (Exception ex)
		{
			Disable("окно парламента не открылось: " + ex.Message);
		}
	}

	private static void Render()
	{
		try
		{
			PoliticalWorld world = RealPoliticMod.World;
			ModUi.ClearChildren(_content);
			KingdomPolitics state = world?.GetKingdom(_kingdomId);
			if (state == null)
			{
				ModUi.Text(_content, GameText.Localized("RP_PARL_NO_KINGDOM", "Держава не найдена."), 11f, 0.9f, 0.8f, 0.6f);
				return;
			}
			ModWindow.SetTitle(_window, GameText.Localized(TitleKey, "Парламент") + " — " + world.KingdomNameOf(_kingdomId));
			List<PoliticalParty> parties = PartiesOf(state);
			if (parties.Count == 0)
			{
				ModUi.Text(_content, GameText.Localized("RP_PARL_NONE",
					"В этой державе нет парламента: правитель решает сам."), 11f, 0.92f, 0.86f, 0.70f);
				return;
			}

			// 1. Места: 100 штук, полукругом.
			List<int> shares = new List<int>(parties.Count);
			List<IdeologyType> ideologies = new List<IdeologyType>(parties.Count);
			for (int i = 0; i < parties.Count; i++)
			{
				shares.Add(Math.Max(0, parties[i].SeatsPercent));
				ideologies.Add(parties[i].Ideology);
			}
			int[] seats = Parliament.Allocate(shares);
			ParliamentPoint[] points = Parliament.Arc(Parliament.TotalSeats, Parliament.Rows, 20f, 15f, 180f);
			int[] seating = Parliament.SeatingOrder(ideologies, seats, points);

			ModUi.Text(_content, GameText.Localized("RP_PARL_ARC_TITLE", "Зал заседаний — {n} мест").Replace("{n}", Parliament.TotalSeats.ToString()),
				13f, 0.95f, 0.90f, 0.70f, bold: true);
			object panel = ModUi.Panel(_content, ModUi.PageWidth, 92f);
			if (panel != null)
			{
				for (int i = 0; i < points.Length && i < seating.Length; i++)
				{
					int partyIndex = seating[i];
					IdeologyType ideology = ideologies[partyIndex];
					(float r, float g, float b) = Parliament.Color(ideology);
					int seatIndex = i;
					ModUi.Dot(panel, points[i].X, points[i].Y - 45f, 7f, r, g, b, () =>
					{
						_selectedSeat = seatIndex;
						Render();
					});
				}
			}

			// 1б. Вторая палата (0.53): совет земель. От чего считаются голоса — решает конституция.
			ChamberCount chambers = ChamberRules.ChambersOf(state);
			int cityCount = world.CityIdsOf(_kingdomId).Count;
			int peopleCount = Math.Max(1, world.PeoplesOf(_kingdomId)?.Count ?? 1);
			int upperSeats = ChamberRules.UpperSeats(state.Constitution.UpperBasis, cityCount, peopleCount,
				Math.Max(1, cityCount * 2));
			if (chambers == ChamberCount.Two)
			{
				ModUi.Text(_content, GameText.Localized("RP53_WIN_UPPER", "Совет земель — {n} мест")
					.Replace("{n}", upperSeats.ToString()), 13f, 0.95f, 0.86f, 0.45f, bold: true);
				ModUi.Text(_content, GameText.Localized("RP53_WIN_UPPER_BASIS", "Голоса считаются: {basis}.")
					.Replace("{basis}", BasisName(state.Constitution.UpperBasis)), 10f, 0.85f, 0.82f, 0.70f);
				object upperPanel = ModUi.Panel(_content, ModUi.PageWidth, 92f);
				if (upperPanel != null && upperSeats > 0)
				{
					ParliamentPoint[] upperPoints = Parliament.Arc(upperSeats,
						upperSeats > 20 ? Parliament.Rows : 2, 20f, 15f, 180f);
					Bill billNow = world.BillOf(_kingdomId);
					int votedFor = billNow != null ? billNow.UpperFor : 0;
					for (int i = 0; i < upperPoints.Length; i++)
					{
						float rr = 0.85f;
						float gg = 0.72f;
						float bb = 0.30f;
						if (i < votedFor)
						{
							rr = 0.55f;
							gg = 0.85f;
							bb = 0.45f;
						}
						ModUi.Dot(upperPanel, upperPoints[i].X, upperPoints[i].Y - 45f, 6f, rr, gg, bb, (Action)null);
					}
				}
			}
			else
			{
				ModUi.Text(_content, GameText.Localized("RP53_WIN_ONE",
					"Палата одна: совета земель в этой державе нет."), 10f, 0.82f, 0.80f, 0.72f);
			}

			// 1в. Что палаты рассматривают прямо сейчас.
			Bill billNow2 = world.BillOf(_kingdomId);
			ModUi.Text(_content, GameText.Localized("RP53_BILL_TITLE", "Закон на рассмотрении"),
				12f, 0.95f, 0.90f, 0.70f, bold: true);
			if (billNow2 == null || !billNow2.Pending)
			{
				ModUi.Text(_content, GameText.Localized("RP53_BILL_NONE",
					"Сейчас палаты ничего не рассматривают."), 10f, 0.78f, 0.76f, 0.70f);
			}
			else
			{
				ModUi.Text(_content, GameText.Localized("RP53_BILL_LINE", "«{article}» — {stage}")
					.Replace("{article}", GameText.Localized(billNow2.Article, billNow2.Article))
					.Replace("{stage}", StageName(billNow2)), 11f, 1f, 0.90f, 0.60f);
				ModUi.Text(_content, GameText.Localized("RP53_BILL_VOTES",
					"Нижняя палата: {lf} за, {la} против.")
					.Replace("{lf}", billNow2.LowerFor.ToString())
					.Replace("{la}", billNow2.LowerAgainst.ToString()), 10f, 0.80f, 0.80f, 0.72f);
				if (chambers == ChamberCount.Two)
				{
					ModUi.Text(_content, GameText.Localized("RP53_BILL_VOTES_UPPER",
						"Совет земель: {uf} за, {ua} против.")
						.Replace("{uf}", billNow2.UpperFor.ToString())
						.Replace("{ua}", billNow2.UpperAgainst.ToString()), 10f, 0.80f, 0.80f, 0.72f);
				}
				if (!string.IsNullOrEmpty(billNow2.EditorName))
				{
					ModUi.Text(_content, GameText.Localized("RP53_BILL_AUTHOR", "Внёс: {who}")
						.Replace("{who}", billNow2.EditorName), 10f, 0.75f, 0.73f, 0.65f);
				}
			}

			// 1г. Как устроено по конституции.
			if (state.Constitution != null)
			{
				ModUi.Text(_content, GameText.Localized("RP53_WIN_SETTINGS",
					"Палат: {count}. Вето правителя: {veto}. Роспуск палат по конституции: {dissolve}.")
					.Replace("{count}", chambers == ChamberCount.Two
						? GameText.Localized("RP53_CHAMBERS_TWO", "две")
						: GameText.Localized("RP53_CHAMBERS_ONE", "одна"))
					.Replace("{veto}", state.Constitution.RulerVeto
						? GameText.Localized("RP53_ON", "есть")
						: GameText.Localized("RP53_OFF", "нет"))
					.Replace("{dissolve}", state.Constitution.RulerMayDissolve
						? GameText.Localized("RP53_ON", "есть")
						: GameText.Localized("RP53_OFF", "нет")),
					10f, 0.75f, 0.73f, 0.65f);
			}

			// 2. Кто на выбранном месте.
			if (_selectedSeat >= 0 && _selectedSeat < seating.Length)
			{
				int partyIndex = seating[_selectedSeat];
				string partyName = string.IsNullOrEmpty(parties[partyIndex].Name)
					? GameText.Localized(parties[partyIndex].NameKey,
						GameText.Localized(Ideologies.NameKey(parties[partyIndex].Ideology), parties[partyIndex].Ideology.ToString()))
					: parties[partyIndex].Name;
				string ideologyName = GameText.Localized(Ideologies.NameKey(parties[partyIndex].Ideology), parties[partyIndex].Ideology.ToString());
				ModUi.Text(_content,
					GameText.Localized("RP_PARL_SEAT_INFO", "Место {seat}: партия «{party}», строй — {ideology}, мест всего {seats}.")
						.Replace("{seat}", (_selectedSeat + 1).ToString())
						.Replace("{party}", partyName)
						.Replace("{ideology}", ideologyName)
						.Replace("{seats}", seats[partyIndex].ToString()),
					11f, 1f, 0.86f, 0.45f, bold: true);
			}

			// 3. Список партий: цвет — партия — строй — места.
			ModUi.Text(_content, GameText.Localized("RP_PARL_LEGEND", "Партии в зале"), 12f, 0.95f, 0.90f, 0.70f, bold: true);
			for (int i = 0; i < parties.Count && i < ideologies.Count; i++)
			{
				PoliticalParty party = parties[i];
				string partyName = string.IsNullOrEmpty(party.Name)
					? GameText.Localized(party.NameKey,
						GameText.Localized(Ideologies.NameKey(party.Ideology), party.Ideology.ToString()))
					: party.Name;
				string ideologyName = GameText.Localized(Ideologies.NameKey(party.Ideology), party.Ideology.ToString());
				string line = "■ " + partyName + " — " + ideologyName + " · " + seats[i] + " "
					+ GameText.Localized("RP_PARL_SEATS", "мест") + (party.IsRuling ? " · "
					+ GameText.Localized("RP_PARL_RULING", "правящая") : string.Empty);
				(float r, float g, float b) = Parliament.Color(party.Ideology);
				ModUi.Text(_content, line, 11f, r, g, b);
			}

			// 4. Сколько всего голосов и когда выборы.
			ModUi.Text(_content, GameText.Localized("RP_PARL_SUMMARY",
				"Всего мест: {total}. Партий в зале: {parties}. Строй державы: {state}.")
					.Replace("{total}", Parliament.TotalSeats.ToString())
					.Replace("{parties}", parties.Count.ToString())
					.Replace("{state}", GameText.Localized(Ideologies.NameKey(state.Ideology), state.Ideology.ToString())),
				10f, 0.75f, 0.73f, 0.65f);
		}
		catch (Exception ex)
		{
			LogService.LogError("[Real Politic] парламент не отрисован: " + ex.Message);
		}
	}

	/// <summary>Партии державы. Если ядро ещё не считало — берём одну правящую.</summary>
	private static List<PoliticalParty> PartiesOf(KingdomPolitics state)
	{
		List<PoliticalParty> list = new List<PoliticalParty>();
		if (state.Parties != null && state.Parties.Count > 0)
		{
			for (int i = 0; i < state.Parties.Count; i++)
			{
				if (state.Parties[i] != null && state.Parties[i].SeatsPercent > 0)
				{
					list.Add(state.Parties[i]);
				}
			}
			return list;
		}
		if (CivicSocietyEngine.HasParliamentaryParties(state))
		{
			list.Add(new PoliticalParty
			{
				Ideology = state.Ideology,
				Name = PartyNames.For(state.Ideology,
					StateNaming.Root(RealPoliticMod.World != null ? RealPoliticMod.World.KingdomNameOf(state.KingdomId) : string.Empty),
					state.KingdomId),
				NameKey = CivicSocietyEngine.PartyNameKey(state.Ideology),
				SeatsPercent = 100,
				IsRuling = true
			});
		}
		return list;
	}

	/// <summary>От чего считается совет земель.</summary>
	private static string BasisName(UpperChamberBasis basis)
	{
		switch (basis)
		{
			case UpperChamberBasis.Peoples:
				return GameText.Localized("RP53_BASIS_PEOPLES", "по народам");
			case UpperChamberBasis.Clans:
				return GameText.Localized("RP53_BASIS_CLANS", "по знатным родам");
			default:
				return GameText.Localized("RP53_BASIS_CITIES", "по городам");
		}
	}

	/// <summary>Где сейчас законопроект — человеческими словами.</summary>
	private static string StageName(Bill bill)
	{
		switch (bill.Stage)
		{
			case BillStage.Upper:
				return GameText.Localized("RP53_STAGE_UPPER", "ждёт голосования совета земель");
			case BillStage.Conference:
				return GameText.Localized("RP53_STAGE_CONFERENCE", "согласительная комиссия до {year} года")
					.Replace("{year}", bill.ConferenceYear.ToString());
			case BillStage.Vetoed:
				return GameText.Localized("RP53_STAGE_VETOED", "правитель наложил вето, палаты пробуют преодолеть");
			case BillStage.Passed:
				return GameText.Localized("RP53_STAGE_PASSED", "принят");
			case BillStage.Rejected:
				return GameText.Localized("RP53_STAGE_REJECTED", "отклонён");
			default:
				return GameText.Localized("RP53_STAGE_LOWER", "ждёт голосования нижней палаты");
		}
	}

	private static void Disable(string reason)
	{
		_disabled = true;
		LogService.LogError("[Real Politic] окно парламента отключено: " + reason);
	}
}
