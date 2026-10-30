using System;
using System.Collections.Generic;
using RealPolitic.Core;

namespace RealPolitic;

/// <summary>
/// 0.33, этап 3: голосование жителей на конференции КМУД.
///
/// Каждый житель каждой державы отвечает на четыре вопроса. Голоса собираются не разом
/// (в мире бывает больше тысячи жителей), а порциями каждый год конференции: бюджет
/// голосов на год, дальше — со следующего. К концу 10 лет успевают проголосовать все.
///
/// Один житель — один голос на каждый вопрос: повторно он не считается (список проголосовавших).
/// </summary>
public static class KmudVoting
{
	/// <summary>Сколько жителей опрашиваем за один игровой год (бережём кадр).</summary>
	public const int VotesPerYear = 400;

	/// <summary>Годовой шаг: собрать порцию голосов или огласить итоги.</summary>
	public static void AnnualTick(PoliticalWorld world, MapBox map, int year)
	{
		if (world?.Kmud == null || map == null)
		{
			return;
		}
		KmudState state = world.Kmud;
		if (!state.IsConvened)
		{
			return;
		}
		try
		{
			// Последний год конференции — оглашение итогов, голоса уже не принимаются.
			if (year >= state.ClosesYear)
			{
				return;
			}
			int collected = Collect(world, map, VotesPerYear);
			if (collected > 0)
			{
				NLog("[Real Politic] КМУД: голосов собрано за год " + year + ": " + collected
					+ " (всего жителей проголосовало: " + state.Voting.Voted.Count + ")");
			}
		}
		catch (Exception ex)
		{
			NLog("[Real Politic] КМУД: ошибка сбора голосов: " + ex.Message);
		}
	}

	/// <summary>Собирает не больше <paramref name="budget"/> голосов. Возвращает число голосов.</summary>
	public static int Collect(PoliticalWorld world, MapBox map, int budget)
	{
		int collected = 0;
		List<Kingdom> kingdoms = GameRefs.KingdomList(map);
		if (kingdoms == null)
		{
			return 0;
		}
		for (int i = 0; i < kingdoms.Count && collected < budget; i++)
		{
			Kingdom kingdom = kingdoms[i];
			if (kingdom == null || kingdom.units == null)
			{
				continue;
			}
			int kingdomId = (int)((NanoObject)kingdom).id;
			KingdomPolitics politics = world.GetKingdom(kingdomId);
			if (politics == null)
			{
				continue;
			}
			foreach (Actor actor in kingdom.units)
			{
				if (collected >= budget)
				{
					break;
				}
				if (actor == null || GameRefs.IsBoatUnit(actor))
				{
					continue;
				}
				int actorId = (int)((NanoObject)actor).id;
				if (actorId <= 0 || world.Kmud.Voting.Voted.Contains(actorId))
				{
					continue;
				}
				if (GameRefs.Invoke(actor, "isAlive") is bool alive && !alive)
				{
					continue;
				}
				CastVotes(world, politics, kingdomId, actorId, actor);
				collected++;
			}
		}
		return collected;
	}

	/// <summary>Один житель отвечает на все пять вопросов.</summary>
	private static void CastVotes(PoliticalWorld world, KingdomPolitics politics, int kingdomId, int actorId, Actor actor)
	{
		KmudVotingState voting = world.Kmud.Voting;
		KmudAgenda agenda = world.Kmud.Agenda;
		KmudPreferences.Situation situation = BuildSituation(world, politics, kingdomId, actorId, actor);
		foreach (KmudQuestion question in Enum.GetValues(typeof(KmudQuestion)))
		{
			int answer = KmudPreferences.Decide(question, situation);
			if (answer == 0)
			{
				continue;
			}
			// По вопросам «о ком речь» держава-адресат не может отвечать за себя дважды:
			// это уже учтено в предпочтениях (OwnIsAccused / OwnIsPoorest).
			voting.Of(question).Add(answer);
		}
		voting.Voted.Add(actorId);
	}

	private static KmudPreferences.Situation BuildSituation(PoliticalWorld world, KingdomPolitics politics, int kingdomId, int actorId, Actor actor)
	{
		KmudAgenda agenda = world.Kmud.Agenda;
		bool warWeary = world.IsAtWar(kingdomId);
		KmudPreferences.Situation situation = new KmudPreferences.Situation
		{
			Own = politics.Ideology,
			Conviction = 0.5f,
			Dissent = 0f,
			OwnIsAggressor = agenda.AggressorId == kingdomId,
			OwnIsDefender = agenda.DefenderId == kingdomId,
			WarWeary = warWeary,
			OwnIsAccused = agenda.WorstId == kingdomId,
			OwnIsPoorest = agenda.PoorestId == kingdomId,
			OwnIsPoor = IsPoor(world, kingdomId),
			// 0.47: пятый вопрос — возвращение народа.
			OwnIsRepatriationTarget = agenda.RepatriationToId == kingdomId,
			OwnIsRepatriationSource = agenda.RepatriationFromId == kingdomId,
			OwnIsRepatriatedRace = !string.IsNullOrEmpty(agenda.RepatriationRaceId)
				&& GameRefs.ActorSpeciesId(actor) == agenda.RepatriationRaceId
		};
		// Если у жителя уже есть собственное убеждение (личная идеология), голосует он по нему,
		// а не по строю державы. Нет записи — значит, живёт по общему строю.
		ActorEntry entry = world.GetActor(actorId);
		if (entry != null)
		{
			situation.Own = entry.Politics.Ideology;
			situation.Conviction = entry.Politics.Conviction;
			situation.Dissent = entry.Politics.Dissent;
		}
		return situation;
	}

	/// <summary>Держава еле сводит концы: казна или благосостояние ниже половины нормы.</summary>
	private static bool IsPoor(PoliticalWorld world, int kingdomId)
	{
		KingdomPolitics politics = world.GetKingdom(kingdomId);
		if (politics == null)
		{
			return false;
		}
		float prosperity = politics.Economy?.Prosperity ?? 50f;
		return prosperity < 40f;
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
