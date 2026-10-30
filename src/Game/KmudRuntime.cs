using System;
using System.Collections.Generic;
using RealPolitic.Core;

namespace RealPolitic;

/// <summary>
/// 0.32, этап 1: запуск и обслуживание КМУД (Конференция Мирного Урегулирования Дел).
///
/// Что делает эта версия:
///   • на 200-м году (настройка KmudYear) созывает конференцию;
///   • все идущие войны прекращаются белым миром — и внешние, и гражданские;
///   • каждой паре держав ставится запрет войн на время конференции;
///   • если игра всё-таки начнёт войну в эти годы — мод гасит её и пишет в летопись.
///
/// Голосование, решения и последствия — следующие этапы.
/// </summary>
public static class KmudRuntime
{
	private static float _nextEnforce;

	/// <summary>0.44: какие напоминания о съезде уже показаны (ключ «год съезда:осталось лет»).</summary>
	private static readonly HashSet<string> _announcedUpcoming = new HashSet<string>();
	private static bool _loggedMissingApi;

	/// <summary>Годовой шаг: созыв конференции.</summary>
	public static void AnnualTick(PoliticalWorld world, MapBox map, int year)
	{
		if (world == null || map == null)
		{
			return;
		}
		try
		{
			KmudState state = world.Kmud;
			if (state == null)
			{
				return;
			}
			List<Kingdom> kingdoms = GameRefs.KingdomList(map);
			List<int> withLand = new List<int>();
			if (kingdoms != null)
			{
				for (int i = 0; i < kingdoms.Count; i++)
				{
					Kingdom kingdom = kingdoms[i];
					if (kingdom == null)
					{
						continue;
					}
					List<City> cities = GameRefs.KingdomCities(kingdom);
					if (cities != null && cities.Count > 0)
					{
						withLand.Add((int)((NanoObject)kingdom).id);
					}
				}
			}
			// Этап 4: действующие санкции и ассоциации работают каждый год.
			KmudEffects.AnnualEffects(world, map, year);
			// Этап 2: пока конференция идёт — держим состав альянса.
			if (state.IsConvened)
			{
				KmudAlliance.MaintainAlliance(world, map);
				// Этап 3: жители голосуют по четырём вопросам — порциями, по годам.
				KmudVoting.AnnualTick(world, map, year);
			}
			// Этап 4 (заготовка): когда срок вышел — закрываем конференцию и распускаем альянс.
			// Подсчёт голосов появится на следующем этапе; пока мира сверх конференции не добавляем.
			if (KmudRules.ShouldClose(state, year))
			{
				// Итоги голосования: подсчёт, оглашение, последствия.
				int peaceYears = AnnounceResults(world, year);
				// Этап 4: решения исполняются сразу после оглашения.
				int applied = KmudEffects.Apply(world, map, year);
				NLog("[Real Politic] КМУД: исполнено решений: " + applied);
				KmudAlliance.Dissolve(world);
				PolEvent closed = KmudRules.Close(state, year, peaceYears);
				world.Bus.Publish(closed);
				NLog("[Real Politic] КМУД: конференция закрыта в году " + year
					+ ", альянс распущен, продление мира: " + peaceYears + " лет");
				return;
			}
			// 0.44: предупреждаем о съезде ЗАРАНЕЕ. Владелец 29.10.2026: «почему в уведомлениях
			// ни словом ни духом о КМУДе?» — первый съезд по правилам только в 200-м году,
			// и до него про КМУД действительно нигде не говорилось. Теперь мод сам напоминает
			// за 25, 10, 5 и 1 год до каждой конференции.
			AnnounceUpcoming(world, year, withLand.Count);
			if (!KmudRules.ShouldConvene(state, world.Params, year, withLand.Count))
			{
				return;
			}
			// ПОРЯДОК ВАЖЕН, и дважды ошибкой:
			//  1) созыв первым — он чистит повестку съезда (0.37: раньше повестка собиралась
			//     до созыва, обнулялась, и съезд закрывался с «исполнено решений: 0»);
			//  2) повестка — ДО гашения войн (0.38: гасили войны первыми, поэтому мод сам
			//     только что погасил всё, а потом искал «идущий конфликт» и не находил его —
			//     в логе было «объявленных войн в памяти: 7, из них идут сейчас: 0»).
			PolEvent evt = KmudRules.Convene(state, world.Params, year, withLand.Count, 0);
			// 0.39: запоминаем, КТО сидит за столом — запрет войн действует между участниками.
			state.MemberIds.Clear();
			for (int i = 0; i < withLand.Count; i++)
			{
				state.MemberIds.Add(withLand[i]);
			}
			BuildAgenda(world, year);
			// 3. Теперь гасим все идущие войны белым миром и записываем их число в событие.
			int ended = DiplomacyExecutor.EndAllWars();
			state.WarsEnded = ended;
			evt.With("wars_ended", ended);
			// 4. Ставим запрет войн каждой паре участников — на время конференции.
			int pauseYears = Math.Max(1, world.Params?.KmudPauseYears ?? KmudRules.DefaultDurationYears);
			int until = year + pauseYears;
			List<int[]> pairs = KmudRules.AllPairs(withLand);
			for (int i = 0; i < pairs.Count; i++)
			{
				world.TrucesBook.Set(pairs[i][0], pairs[i][1], until);
			}
			// Если настройка требует паузу дольше, чем длительность конференции, — берём её.
			if (state.TruceUntilYear < until)
			{
				state.TruceUntilYear = until;
			}
			world.Bus.Publish(evt);
			NLog("[Real Politic] КМУД: созвана в году " + year + ", держав: " + withLand.Count
				+ ", прекращено войн: " + ended + ", запрет войн до " + state.TruceUntilYear);
			// Этап 2: все державы входят в общий альянс КМУД.
			int inAlliance = KmudAlliance.FormAll(world, map);
			if (inAlliance > 0)
			{
				world.Bus.Publish(new PolEvent(PolEventType.KmudAllianceFormed, "RP32_EV_KMUD_ALLIANCE") { Year = year }
					.With("members", inAlliance)
					.With("name", KmudAlliance.AllianceName));
			}
		}
		catch (Exception ex)
		{
			NLog("[Real Politic] КМУД: ошибка созыва: " + ex.Message);
		}
	}

	/// <summary>
	/// Частый шаг (раз в пару секунд): сторож паузы. Если в годы конференции кто-то всё-таки
	/// начал войну — гасим её, чтобы «приостановка войн» не осталась на бумаге.
	/// </summary>
	public static void Tick()
	{
		if (UnityEngine.Time.unscaledTime < _nextEnforce)
		{
			return;
		}
		_nextEnforce = UnityEngine.Time.unscaledTime + 2f;
		try
		{
			PoliticalWorld world = RealPoliticMod.World;
			if (world == null || world.Kmud == null)
			{
				return;
			}
			if (!world.KmudPauseActive)
			{
				return;
			}
			int stopped = DiplomacyExecutor.EndAllWars();
			if (stopped > 0)
			{
				world.Kmud.WarsStoppedDuringPause += stopped;
				world.Bus.Publish(new PolEvent(PolEventType.KmudPauseViolated, "RP32_EV_KMUD_PAUSE_VIOLATED") { Year = world.Year }
					.With("count", stopped)
					.With("until", world.Kmud.TruceUntilYear));
				NLog("[Real Politic] КМУД: войн прекращено во время паузы: " + stopped);
			}
		}
		catch (Exception ex)
		{
			if (!_loggedMissingApi)
			{
				_loggedMissingApi = true;
				NLog("[Real Politic] КМУД: сторож паузы недоступен: " + ex.Message);
			}
		}
	}

	/// <summary>
	/// Повестка: самый долгий идущий конфликт, самая безнравственная держава
	/// (по числу развязанных войн) и самая отстающая (по благосостоянию).
	/// </summary>
	private static void BuildAgenda(PoliticalWorld world, int year)
	{
		KmudAgenda agenda = world.Kmud.Agenda;
		agenda.AggressorId = 0;
		agenda.DefenderId = 0;
		agenda.ConflictYears = 0;
		agenda.WorstId = 0;
		agenda.WorstReasons.Clear();
		agenda.PoorestId = 0;
		agenda.PoorestReason = string.Empty;
		agenda.RepatriationRaceId = string.Empty;
		agenda.RepatriationRaceName = string.Empty;
		agenda.RepatriationToId = 0;
		agenda.RepatriationFromId = 0;

		// Самый долгий конфликт: из объявленных войн берём ту, что идёт до сих пор.
		int bestYears = 0;
		int ongoing = 0;
		IReadOnlyList<WarDeclarationRecord> declarations = world.WarDeclarations;
		for (int i = 0; i < declarations.Count; i++)
		{
			WarDeclarationRecord record = declarations[i];
			if (!world.AreAtWar(record.AttackerId, record.DefenderId))
			{
				continue;
			}
			ongoing++;
			int years = Math.Max(1, year - record.Year);
			if (years > bestYears)
			{
				bestYears = years;
				agenda.AggressorId = record.AttackerId;
				agenda.DefenderId = record.DefenderId;
			}
		}
		agenda.ConflictYears = bestYears;
		// 0.37: если повестка вышла пустой, в логе сразу видно, сколько войн мы видели.
		// Раньше на 250-м году писалось «— против —» и понять причину было нельзя.
		NLog("[Real Politic] КМУД: повестка — объявленных войн в памяти: " + declarations.Count
			+ ", из них идут сейчас: " + ongoing
			+ (agenda.HasConflict ? ", самый долгий: " + world.KingdomNameOf(agenda.AggressorId)
				+ " против " + world.KingdomNameOf(agenda.DefenderId) : ", идущих войн не найдено"));

		// Самая безнравственная: развязанные войны плюс голод собственного народа
		// (решение владельца от 28.10.2026: «добавь голод»).
		Dictionary<int, int> aggressions = new Dictionary<int, int>();
		for (int i = 0; i < declarations.Count; i++)
		{
			int attacker = declarations[i].AttackerId;
			aggressions.TryGetValue(attacker, out int count);
			aggressions[attacker] = count + 1;
		}
		List<KmudMisdeed> misdeeds = new List<KmudMisdeed>();
		foreach (int kingdomId in aggressions.Keys)
		{
			KingdomPolitics politics = world.GetKingdom(kingdomId);
			if (politics == null)
			{
				continue;
			}
			// 0.50: отнятое гражданство — тоже проступок (и тяжёлый).
			misdeeds.Add(KmudMorality.Record(kingdomId, aggressions[kingdomId], politics.Starving,
				world.HasFreshHateStain(kingdomId, year)));
		}
		// Голодающая держава без единой войны тоже попадает в список обвиняемых.
		foreach (KingdomPolitics politics in world.Kingdoms)
		{
			if (politics == null || aggressions.ContainsKey(politics.KingdomId))
			{
				continue;
			}
			bool stain = world.HasFreshHateStain(politics.KingdomId, year);
			if (politics.Starving || stain)
			{
				misdeeds.Add(KmudMorality.Record(politics.KingdomId, 0, politics.Starving, stain));
			}
		}
		agenda.WorstId = KmudMorality.Worst(misdeeds);
		if (agenda.WorstId > 0)
		{
			for (int i = 0; i < misdeeds.Count; i++)
			{
				if (misdeeds[i].KingdomId == agenda.WorstId)
				{
					agenda.WorstReasons.AddRange(misdeeds[i].Reasons());
					break;
				}
			}
		}

		// Самая отстающая: ниже всех благосостояние среди живых держав.
		float worstProsperity = float.MaxValue;
		foreach (KingdomPolitics politics in world.Kingdoms)
		{
			if (politics == null)
			{
				continue;
			}
			float prosperity = politics.Economy?.Prosperity ?? 50f;
			if (prosperity < worstProsperity)
			{
				worstProsperity = prosperity;
				agenda.PoorestId = politics.KingdomId;
			}
		}
		if (agenda.PoorestId > 0)
		{
			agenda.PoorestReason = "благосостояние " + (int)Math.Round(worstProsperity) + "%";
		}
		BuildRepatriationAgenda(world, agenda);
		world.Bus.Publish(new PolEvent(PolEventType.KmudAgenda, "RP32_EV_KMUD_AGENDA") { Year = year }
			.With("aggressor", agenda.HasConflict ? world.KingdomNameOf(agenda.AggressorId) : "—")
			.With("defender", agenda.HasConflict ? world.KingdomNameOf(agenda.DefenderId) : "—")
			.With("years", agenda.ConflictYears)
			.With("worst", agenda.WorstId > 0
				? world.KingdomNameOf(agenda.WorstId) + " (" + string.Join(", ", agenda.WorstReasons) + ")"
				: "не выбрана")
			.With("poorest", agenda.PoorestId > 0 ? world.KingdomNameOf(agenda.PoorestId) : "не выбрана")
			.With("race", agenda.HasRepatriation ? agenda.RepatriationRaceName : "нет")
			.With("to", agenda.HasRepatriation ? world.KingdomNameOf(agenda.RepatriationToId) : "—")
			.With("from", agenda.RepatriationFromId > 0 ? world.KingdomNameOf(agenda.RepatriationFromId) : "—"));
	}

	/// <summary>
	/// 0.47: пятый вопрос — возвращение малого народа.
	/// Ищем самый гонимый народ этого мира: тот, чья доля где-то упала ниже порога
	/// (RaceSurvival.IsEndangered), и державу, которая его как раз терпит
	/// (политика «защищают» или это её коренной народ). Туда народ и «голосует» вернуться.
	/// </summary>
	private static void BuildRepatriationAgenda(PoliticalWorld world, KmudAgenda agenda)
	{
		try
		{
			MapBox map = World.world;
			if (map == null || world.Params == null || !world.Params.EnableRaceSurvival)
			{
				return;
			}
			List<Kingdom> kingdoms = GameRefs.KingdomList(map);
			if (kingdoms == null || kingdoms.Count < 2)
			{
				return;
			}
			// 1. Собираем по каждой державе: кто живёт и как держава к родам относится.
			List<KingdomRaces> census = new List<KingdomRaces>();
			for (int i = 0; i < kingdoms.Count; i++)
			{
				Kingdom kingdom = kingdoms[i];
				if (kingdom == null)
				{
					continue;
				}
				int kingdomId = (int)((NanoObject)kingdom).id;
				KingdomPolitics politics = world.GetKingdom(kingdomId);
				if (politics == null)
				{
					continue;
				}
				List<RaceShare> races = MigrationRuntime.CountRaces(kingdom, out int total);
				if (races.Count == 0 || total <= 0)
				{
					continue;
				}
				census.Add(new KingdomRaces
				{
					KingdomId = kingdomId,
					Races = races,
					Total = total,
					Policy = RaceSurvival.PolicyOf(politics),
					TitularRaceId = politics.TitularRaceId ?? string.Empty
				});
			}
			// 2. Ищем самый гонимый малый народ: у кого давление самое высокое.
			string bestRace = string.Empty;
			string bestRaceName = string.Empty;
			int bestFrom = 0;
			float bestPressure = 0f;
			for (int i = 0; i < census.Count; i++)
			{
				KingdomRaces row = census[i];
				if (row.Policy != RacePolicy.Oppress && row.Policy != RacePolicy.Ignore)
				{
					continue;
				}
				for (int r = 0; r < row.Races.Count; r++)
				{
					RaceShare race = row.Races[r];
					if (race == null || string.IsNullOrEmpty(race.RaceId) || row.Races.Count < 2)
					{
						continue;
					}
					if (!RaceSurvival.IsEndangered(race.Population, row.Total))
					{
						continue;
					}
					float pressure = RaceSurvival.Pressure(row.Policy, true, race.Population, row.Total);
					if (pressure > bestPressure)
					{
						bestPressure = pressure;
						bestRace = race.RaceId;
						bestRaceName = race.RaceName;
						bestFrom = row.KingdomId;
					}
				}
			}
			if (string.IsNullOrEmpty(bestRace))
			{
				return;
			}
			// 3. Куда: держава, которая этот народ защищает (или где он коренной) и где его ещё мало.
			int bestTo = 0;
			int bestScore = -1;
			for (int i = 0; i < census.Count; i++)
			{
				KingdomRaces row = census[i];
				if (row.KingdomId == bestFrom)
				{
					continue;
				}
				bool titular = row.TitularRaceId == bestRace;
				bool protects = row.Policy == RacePolicy.Protect;
				if (!titular && !protects)
				{
					continue;
				}
				int there = 0;
				for (int r = 0; r < row.Races.Count; r++)
				{
					if (row.Races[r].RaceId == bestRace)
					{
						there = row.Races[r].Population;
					}
				}
				// Оценка: коренной народ важнее, «защищают» — приятный бонус, чем больше места, тем лучше.
				int score = (titular ? 1000 : 500) - there;
				if (score > bestScore)
				{
					bestScore = score;
					bestTo = row.KingdomId;
				}
			}
			if (bestTo <= 0)
			{
				return;
			}
			agenda.RepatriationRaceId = bestRace;
			agenda.RepatriationRaceName = string.IsNullOrEmpty(bestRaceName) ? bestRace : bestRaceName;
			agenda.RepatriationFromId = bestFrom;
			agenda.RepatriationToId = bestTo;
			NLog("[Real Politic] КМУД: пятый вопрос — народ " + agenda.RepatriationRaceName
				+ " из державы " + world.KingdomNameOf(bestFrom)
				+ " хочет вернуться в " + world.KingdomNameOf(bestTo) + " (давление " + bestPressure.ToString("0.##") + ")");
		}
		catch (Exception ex)
		{
			NLog("[Real Politic] КМУД: пятый вопрос не составлен: " + ex.Message);
		}
	}

	/// <summary>Сводка по родам одной державы для повестки пятого вопроса.</summary>
	private sealed class KingdomRaces
	{
		public int KingdomId;
		public List<RaceShare> Races;
		public int Total;
		public RacePolicy Policy;
		public string TitularRaceId;
	}

	/// <summary>Ключ локализации названия вопроса.</summary>
	private static string QuestionKey(KmudQuestion question)
	{
		switch (question)
		{
			case KmudQuestion.ConflictOutcome: return "RP32_Q_CONFLICT";
			case KmudQuestion.PeaceYears: return "RP32_Q_PEACE";
			case KmudQuestion.Punish: return "RP32_Q_PUNISH";
			case KmudQuestion.Aid: return "RP32_Q_AID";
			case KmudQuestion.Repatriation: return "RP47_Q_REPAT";
			default: return "RP32_Q_UNKNOWN";
		}
	}

	/// <summary>
	/// Подсчёт и оглашение итогов голосования. Возвращает выбранное число лет мира
	/// (0 — продления нет). Здесь же применяется главное последствие — сколько лет без войн.
	/// </summary>
	private static int AnnounceResults(PoliticalWorld world, int year)
	{
		KmudState state = world.Kmud;
		if (state.ResultsAnnounced)
		{
			return state.LastPeaceYears;
		}
		Dictionary<KmudQuestion, int> winners = state.Voting.TallyAll();
		state.ResultsAnnounced = true;
		int peaceYears = winners.TryGetValue(KmudQuestion.PeaceYears, out int years) ? years : 0;
		// Записываем решения: кто платит по конфликту, кого казнят/перестраивают, кому помогают.
		KmudDecisions decisions = state.Decisions;
		decisions.Punishment = winners.TryGetValue(KmudQuestion.Punish, out int punishment) ? punishment : 0;
		decisions.Aid = winners.TryGetValue(KmudQuestion.Aid, out int aid) ? aid : 0;
		decisions.PunishedId = KmudVerdicts.AppliesSanctions(decisions.Punishment)
			|| KmudVerdicts.ChangesIdeology(decisions.Punishment) ? state.Agenda.WorstId : 0;
		// «Ничего не делать» — тоже решение: тогда и адресата нет.
		if (decisions.Punishment == KmudOptions.PunishNothing)
		{
			decisions.PunishedId = 0;
		}
		decisions.AidedId = decisions.Aid == KmudOptions.AidNothing ? 0 : state.Agenda.PoorestId;
		// 0.47: пятый вопрос — возвращение народа. Даже «запретить» записываем как решение.
		decisions.Repatriation = winners.TryGetValue(KmudQuestion.Repatriation, out int repat) ? repat : 0;
		if (state.Agenda.HasRepatriation && KmudVerdicts.AllowsRepatriation(decisions.Repatriation))
		{
			decisions.RepatriationRaceId = state.Agenda.RepatriationRaceId;
			decisions.RepatriationToId = state.Agenda.RepatriationToId;
		}
		else
		{
			decisions.RepatriationRaceId = string.Empty;
			decisions.RepatriationToId = 0;
		}
		KmudVerdicts.ResolvePayer(state.Agenda, winners.TryGetValue(KmudQuestion.ConflictOutcome, out int outcome) ? outcome : 0, decisions);
		decisions.Applied = false;
		state.LastPeaceYears = peaceYears;
		if (peaceYears > 0)
		{
			int until = KmudRules.PeaceUntil(year, peaceYears);
			if (until > state.TruceUntilYear)
			{
				state.TruceUntilYear = until;
			}
		}
		// Оглашение: каждый вопрос — отдельной строкой летописи.
		// 0.47 (заказ владельца): в конце заседания решения приходят в уведомления
		// ПО ОДНОМУ на каждый из пяти вопросов — отдельным всплывающим сообщением,
		// чтобы каждое было видно, а не сливалось в одну строку.
		int number = 0;
		foreach (KmudQuestion question in Enum.GetValues(typeof(KmudQuestion)))
		{
			number++;
			KmudBallotResult ballot = state.Voting.Of(question);
			// По пятому вопросу, если о народе говорить было нечего, решения нет.
			if (question == KmudQuestion.Repatriation && !state.Agenda.HasRepatriation)
			{
				continue;
			}
			// По пятому вопросу название вопроса собираем сразу с народом и державой:
			// «Вернуть народ «лисы» в державу Лунная под защиту?» — понятно без пояснений.
			string questionText = QuestionKey(question);
			if (question == KmudQuestion.Repatriation && state.Agenda.HasRepatriation)
			{
				questionText = GameText.Localized("RP47_Q_REPAT_TEXT",
					"Вернуть народ «{race}» в державу {to} под защиту?")
					.Replace("{race}", state.Agenda.RepatriationRaceName)
					.Replace("{to}", world.KingdomNameOf(state.Agenda.RepatriationToId));
			}
			PolEvent result = new PolEvent(PolEventType.KmudResult, "RP32_EV_KMUD_RESULT") { Year = year };
			result.Severity = Severity.Major;
			world.Bus.Publish(result
				.With("question", questionText)
				.With("answer", KmudOptions.LocKey(question, ballot.Winner))
				.With("voters", ballot.Voters)
				.With("number", number)
				.With("race", state.Agenda.HasRepatriation ? state.Agenda.RepatriationRaceName : string.Empty)
				.With("to", state.Agenda.HasRepatriation ? world.KingdomNameOf(state.Agenda.RepatriationToId) : string.Empty)
				.With("tied", ballot.Tied ? 1 : 0));
		}
		// 0.48.2: и в общей строке итогов пишем числа, а не голый заголовок.
		// Если мира не выбрали, так и говорим — «продлён на 0 лет» читается как ошибка.
		string resultsKey = peaceYears > 0 ? "RP32_EV_KMUD_RESULTS" : "RP32_EV_KMUD_RESULTS_NOWAR";
		world.Bus.Publish(new PolEvent(PolEventType.KmudResultsAnnounced, resultsKey) { Year = year }
			.With("total_voters", state.Voting.Voted.Count)
			.With("peace_years", peaceYears));
		NLog("[Real Politic] КМУД: итоги оглашены, жителей проголосовало: " + state.Voting.Voted.Count
			+ ", мир продлён на " + peaceYears + " лет");
		return peaceYears;
	}

	/// <summary>«1 год», «2 года», «5 лет» — чтобы не выходило «через 1 лет».</summary>
	private static string YearsWord(int years)
	{
		int tail100 = years % 100;
		int tail10 = years % 10;
		if (tail100 >= 11 && tail100 <= 14)
		{
			return years + " лет";
		}
		if (tail10 == 1)
		{
			return years + " год";
		}
		if (tail10 >= 2 && tail10 <= 4)
		{
			return years + " года";
		}
		return years + " лет";
	}

	/// <summary>Год следующего съезда: первый — из настроек, дальше — через интервал.</summary>
	private static int NextCongressYear(PoliticalWorld world)
	{
		KmudState state = world.Kmud;
		SimParams p = world.Params;
		if (p == null)
		{
			return 0;
		}
		int interval = Math.Max(1, p.KmudIntervalYears);
		if (state == null || state.Phase == KmudPhase.NotHeld)
		{
			return Math.Max(1, p.KmudYear);
		}
		return state.OpenedYear + interval;
	}

	/// <summary>
	/// 0.44: напоминание перед съездом — за 25, 10, 5 и 1 год.
	/// Одно уведомление на каждую контрольную точку: журнал и всплывашки не засоряются.
	/// </summary>
	private static void AnnounceUpcoming(PoliticalWorld world, int year, int kingdomsWithLand)
	{
		try
		{
			KmudState state = world.Kmud;
			if (state == null || state.IsConvened || !world.Params.EnableKmud)
			{
				return;
			}
			// 0.48: если держав с землёй меньше двух, съезду нечего решать — молчим,
			// а не пишем «за столом будут все державы (0 с землёй)» (так было в логе
			// владельца после вымирания мира).
			if (kingdomsWithLand < 2)
			{
				return;
			}
			int next = NextCongressYear(world);
			int left = next - year;
			if (left != 25 && left != 10 && left != 5 && left != 1)
			{
				return;
			}
			string key = "kmud_upcoming:" + next + ":" + left;
			if (!_announcedUpcoming.Add(key))
			{
				return;
			}
			PolEvent upcoming = new PolEvent(PolEventType.KmudUpcoming, "RP_EV_KMUD_UPCOMING") { Year = year };
			upcoming.Severity = Severity.Major;
			world.Bus.Publish(upcoming
				.With("year", next)
				.With("years", left)
				.With("years_word", YearsWord(left))
				.With("kingdoms", kingdomsWithLand));
			NLog("[Real Politic] КМУД: напоминание — съезд в " + next + " году, осталось лет: " + left
				+ ", держав с землёй: " + kingdomsWithLand);
		}
		catch (Exception ex)
		{
			NLog("[Real Politic] КМУД: ошибка напоминания о съезде: " + ex.Message);
		}
	}

	private static void NLog(string message)
	{
		try
		{
			NeoModLoader.services.LogService.LogInfo(message);
		}
		catch (Exception)
		{
		}
	}
}
