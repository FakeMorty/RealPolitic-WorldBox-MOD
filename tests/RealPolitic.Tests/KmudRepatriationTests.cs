using System;
using System.Collections.Generic;
using RealPolitic.Core;
using Xunit;

namespace RealPolitic.Tests;

/// <summary>
/// 0.47: пятый вопрос КМУД — безопасность возвращения малого народа.
///
/// Владелец 29.10.2026: «на повестку дня ставится вопрос об обеспечении безопасности
/// репатриации какой-нибудь расы туда, куда они голосованием захотели».
/// Здесь проверяется, что вопрос действительно пятый, что варианты ответа
/// различаются по смыслу и что решения правильно понимаются исполнителем.
/// </summary>
public class KmudRepatriationTests
{
	[Fact]
	public void ConferenceHasExactlyFiveQuestions()
	{
		Assert.Equal(5, Enum.GetValues(typeof(KmudQuestion)).Length);
		Assert.Equal(KmudQuestion.Repatriation, (KmudQuestion)4);
	}

	[Fact]
	public void RepatriationQuestionHasFourAnswers()
	{
		int[] options = KmudOptions.For(KmudQuestion.Repatriation);
		Assert.Equal(4, options.Length);
		Assert.Equal(KmudOptions.RepatGuard, options[0]);
		Assert.Equal(KmudOptions.RepatDeny, options[3]);
	}

	[Fact]
	public void EveryAnswerHasItsOwnText()
	{
		int[] options = KmudOptions.For(KmudQuestion.Repatriation);
		HashSet<string> keys = new HashSet<string>(StringComparer.Ordinal);
		for (int i = 0; i < options.Length; i++)
		{
			string key = KmudOptions.LocKey(KmudQuestion.Repatriation, options[i]);
			Assert.StartsWith("RP47_OPT_REPAT_", key);
			Assert.True(keys.Add(key), "у каждого варианта свой текст: " + key);
		}
		Assert.Equal(4, keys.Count);
	}

	[Fact]
	public void AgendaKnowsWhetherThereIsSomethingToAsk()
	{
		KmudAgenda empty = new KmudAgenda();
		Assert.False(empty.HasRepatriation);

		KmudAgenda ready = new KmudAgenda
		{
			RepatriationRaceId = "fox",
			RepatriationRaceName = "лисы",
			RepatriationToId = 7,
			RepatriationFromId = 3
		};
		Assert.True(ready.HasRepatriation);
	}

	[Fact]
	public void GuardFundAndAllowAllMoveThePeople_ButDenyDoesNot()
	{
		Assert.True(KmudVerdicts.AllowsRepatriation(KmudOptions.RepatGuard));
		Assert.True(KmudVerdicts.AllowsRepatriation(KmudOptions.RepatFund));
		Assert.True(KmudVerdicts.AllowsRepatriation(KmudOptions.RepatAllow));
		Assert.False(KmudVerdicts.AllowsRepatriation(KmudOptions.RepatDeny));

		Assert.True(KmudVerdicts.GuardsRepatriation(KmudOptions.RepatGuard));
		Assert.False(KmudVerdicts.GuardsRepatriation(KmudOptions.RepatAllow));

		Assert.True(KmudVerdicts.FundsRepatriation(KmudOptions.RepatFund));
		Assert.False(KmudVerdicts.FundsRepatriation(KmudOptions.RepatGuard));
	}

	[Fact]
	public void ThePeopleItselfVotesForProtection()
	{
		// Житель того самого рода хочет, чтобы возвращение охраняли.
		int answer = KmudPreferences.Decide(KmudQuestion.Repatriation, new KmudPreferences.Situation
		{
			Own = IdeologyType.Monarchy,
			OwnIsRepatriatedRace = true,
			WarWeary = false
		});
		Assert.Equal(KmudOptions.RepatGuard, answer);
	}

	[Fact]
	public void TheRealmItLosesPeopleFromVotesAgainst()
	{
		// Держава, из которой народ уходит, теряет жителей — она против.
		int answer = KmudPreferences.Decide(KmudQuestion.Repatriation, new KmudPreferences.Situation
		{
			Own = IdeologyType.Militarism,
			OwnIsRepatriationSource = true
		});
		Assert.Equal(KmudOptions.RepatDeny, answer);
	}

	[Fact]
	public void TheHostRealmIsNotAgainstIt()
	{
		int answer = KmudPreferences.Decide(KmudQuestion.Repatriation, new KmudPreferences.Situation
		{
			Own = IdeologyType.Democracy,
			OwnIsRepatriationTarget = true
		});
		Assert.NotEqual(KmudOptions.RepatDeny, answer);
	}

	[Fact]
	public void EveryCitizenGivesSomeAnswer()
	{
		Array ideologies = Enum.GetValues(typeof(IdeologyType));
		for (int i = 0; i < ideologies.Length; i++)
		{
			int answer = KmudPreferences.Decide(KmudQuestion.Repatriation, new KmudPreferences.Situation
			{
				Own = (IdeologyType)ideologies.GetValue(i)
			});
			Assert.Contains(answer, KmudOptions.For(KmudQuestion.Repatriation));
		}
	}

	[Fact]
	public void DecisionsStartEmptyAndRememberTheRace()
	{
		KmudDecisions decisions = new KmudDecisions();
		Assert.Equal(0, decisions.Repatriation);
		Assert.Equal(string.Empty, decisions.RepatriationRaceId);
		Assert.Equal(0, decisions.RepatriatedCount);
	}

	[Fact]
	public void ReturningPeopleIsLimitedSoTheYearDoesNotStall()
	{
		Assert.InRange(KmudVerdicts.RepatriationPeopleToMove, 1, 100);
		Assert.InRange(KmudVerdicts.RepatriationGuardYears, 1, 100);
		Assert.True(KmudVerdicts.RepatriationFundPerYear > 0f);
		Assert.InRange(KmudVerdicts.RepatriationFundYears, 1, 50);
	}
}
