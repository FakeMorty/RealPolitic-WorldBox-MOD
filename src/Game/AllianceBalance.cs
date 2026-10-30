using System;
using System.Collections.Generic;
using NeoModLoader.services;
using RealPolitic.Core;

namespace RealPolitic;

/// <summary>
/// Предел и ежегодная проверка переросших нативных альянсов.
/// В 700-летнем прогоне 0.31.1 один блок постепенно собрал 6–8 держав при
/// всего 5–7 живых странах: воевать стало почти не с кем. Теперь приём
/// останавливается раньше, а уже переросший блок периодически ставит на
/// обычное голосование вопрос об исключении самого чужеродного участника.
/// </summary>
public static class AllianceBalance
{
	public const int MinimumStateAgeYears = 10;
	public const int ExpulsionReviewCooldownYears = 10;

	/// <summary>Максимум участников одного нативного альянса при данном числе живых держав.</summary>
	public static int MaxMembers(int totalCountries)
	{
		// 0.47.1: правило «всегда оставляем место для войны» переехало в ядро
		// (AllianceRoom), чтобы его проверяли тесты без игры. Две последние державы
		// в один союз не входят: иначе воевать будет не с кем.
		return RealPolitic.Core.AllianceRoom.MaxMembers(totalCountries);
	}

	/// <summary>
	/// Выбирает предмет и инициатора голосования об исключении из переросшего
	/// альянса: самый идеологически чужой и обидный для остальных участник,
	/// не являющийся основателем/лидером снимка.
	/// </summary>
	public static bool TryChooseExpulsion(PoliticalWorld world, AllianceRecord record, out int proposerId, out int subjectId)
	{
		proposerId = 0;
		subjectId = 0;
		if (world == null || record == null || record.Members == null || record.Members.Count <= 2)
		{
			return false;
		}

		var states = new Dictionary<int, KingdomPolitics>();
		foreach (int memberId in record.Members)
		{
			KingdomPolitics state = world.GetKingdom(memberId);
			if (state != null)
			{
				states[memberId] = state;
			}
		}
		if (states.Count <= 2)
		{
			return false;
		}

		float bestSubjectScore = float.MaxValue;
		foreach (KeyValuePair<int, KingdomPolitics> candidate in states)
		{
			if (candidate.Key == record.LeaderId)
			{
				continue;
			}
			float affinitySum = 0f;
			float grievanceSum = 0f;
			int comparisons = 0;
			foreach (KeyValuePair<int, KingdomPolitics> other in states)
			{
				if (other.Key == candidate.Key)
				{
					continue;
				}
				affinitySum += Ideologies.Affinity(candidate.Value.Ideology, other.Value.Ideology);
				grievanceSum += world.GetGrievance(candidate.Key, other.Key);
				grievanceSum += world.GetGrievance(other.Key, candidate.Key);
				comparisons++;
			}
			if (comparisons == 0)
			{
				continue;
			}
			float score = affinitySum / comparisons
				- grievanceSum / comparisons / 80f
				+ Math.Min(0.15f, candidate.Value.Population / 2000f);
			if (score < bestSubjectScore || (Math.Abs(score - bestSubjectScore) < 0.0001f && candidate.Key < subjectId))
			{
				bestSubjectScore = score;
				subjectId = candidate.Key;
			}
		}
		if (subjectId == 0)
		{
			return false;
		}

		KingdomPolitics subject = states[subjectId];
		float bestProposerScore = float.MaxValue;
		foreach (KeyValuePair<int, KingdomPolitics> candidate in states)
		{
			if (candidate.Key == subjectId)
			{
				continue;
			}
			float score = Ideologies.Affinity(candidate.Value.Ideology, subject.Ideology)
				- world.GetGrievance(candidate.Key, subjectId) / 60f
				- world.GetGrievance(subjectId, candidate.Key) / 120f
				+ (candidate.Key == record.LeaderId ? -0.05f : 0f);
			if (score < bestProposerScore || (Math.Abs(score - bestProposerScore) < 0.0001f && candidate.Key < proposerId))
			{
				bestProposerScore = score;
				proposerId = candidate.Key;
			}
		}
		return proposerId > 0 && proposerId != subjectId;
	}

	/// <summary>
	/// Раз в десять лет ставит переросший мирный альянс перед обычным
	/// голосованием об исключении. Никого напрямую из списка не вычёркивает.
	/// </summary>
	public static void ReviewOversized(PoliticalWorld world, List<AllianceRecord> records, int totalCountries, int year)
	{
		if (world?.Governance?.Enabled != true || records == null || totalCountries <= 2)
		{
			return;
		}
		int cap = MaxMembers(totalCountries);
		foreach (AllianceRecord record in records)
		{
			if (record == null || record.Id <= 0 || record.Members == null || record.Members.Count <= cap)
			{
				continue;
			}
			bool wartime = false;
			foreach (int memberId in record.Members)
			{
				if (world.IsAtWar(memberId))
				{
					wartime = true;
					break;
				}
			}
			if (wartime || HasRecentExpulsionMotion(world, record.Id, year))
			{
				continue;
			}
			if (!TryChooseExpulsion(world, record, out int proposerId, out int memberToExpel))
			{
				continue;
			}
			if (CivicAllianceRuntime.RequestExpulsion(world, proposerId, memberToExpel, year))
			{
				LogService.LogInfo("[Real Politic] переросший альянс «" + record.Name + "» (" + record.Members.Count
					+ " из " + totalCountries + " держав): поставлен вопрос об исключении #" + memberToExpel);
			}
		}
	}

	private static bool HasRecentExpulsionMotion(PoliticalWorld world, int allianceId, int year)
	{
		foreach (CivicAllianceMotion motion in world.Governance.AllianceMotions.Values)
		{
			if (motion.AllianceId == allianceId && motion.Action == CivicAllianceAction.Expulsion
				&& motion.Ballot.OpenedYear > year - ExpulsionReviewCooldownYears)
			{
				return true;
			}
		}
		return false;
	}
}
