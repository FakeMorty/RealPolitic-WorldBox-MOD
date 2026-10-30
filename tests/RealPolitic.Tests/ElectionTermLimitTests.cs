using System;
using System.Collections.Generic;
using RealPolitic.Core;
using Xunit;

namespace RealPolitic.Tests;

/// <summary>Фикс «лидеры переизбираются больше 3-х раз»: счётчик сроков — личный
/// и пожизненный, не обнуляется ни реформами, ни сменой строя, ни десинхронизацией.</summary>
public class ElectionTermLimitTests
{
	private static KingdomPolitics DemocraticState()
	{
		var state = new KingdomPolitics
		{
			KingdomId = 1,
			Ideology = IdeologyType.Democracy,
			Legitimacy = 60f,
			Support = 60f,
			Ruler = new RulerRef
			{
				ActorId = 1001,
				Name = "Лидер А",
				KingdomId = 1,
				SinceYear = 0,
				Age = 45
			}
		};
		state.Election = new ElectionState { NextElectionYear = 15, TermsServed = 1 };
		state.Election.RecordPresidentTerm(1001, "Лидер А", 1);
		return state;
	}

	[Fact]
	public void LongRun_NobodyExceedsThreeTerms()
	{
		var rng = new Random(777);
		for (int sim = 0; sim < 200; sim++)
		{
			KingdomPolitics state = DemocraticState();
			for (int year = 15; year <= 300; year += 15)
			{
				ElectionSystem.HoldElection(state, year, null, rng);
				Assert.True(state.Election!.TermsServed <= ElectionSystem.MaxLifetimeTerms,
					$"общий счётчик выше порога: {state.Election.TermsServed} (sim {sim}, год {year})");
				foreach (PastPresidentRecord past in state.Election.PastPresidents)
				{
					Assert.True(past.TermsCount <= ElectionSystem.MaxLifetimeTerms,
						$"у {past.Name} зафиксировано {past.TermsCount} сроков");
				}
			}
		}
	}

	[Fact]
	public void ReformPreservesLifetimeRecords()
	{
		KingdomPolitics state = DemocraticState();
		// Лидер А отслужил все три срока — навсегда.
		state.Election!.PastPresidents[0].TermsCount = 3;
		state.Election.TermsServed = 3;

		// «Реформа»: Демократия → Коммунизм на один правительский цикл → Демократия.
		// По старому коду возврат к демократии обнулял прошлое — лидер А переизбирался без конца.
		ElectionState fresh = ElectionSystem.ReformedElectoralState(state.Election, 60);

		PastPresidentRecord? record = fresh.FindPastPresident(1001);
		Assert.NotNull(record);
		Assert.Equal(3, record!.TermsCount);
		Assert.Equal(fresh.PastPresidents, state.Election.PastPresidents); // тот же список по ссылке
	}

	[Fact]
	public void DesyncedCounter_StillCannotExceedLimit()
	{
		// Подстраховка: даже если «общий» счётчик потерян (синхронизация слетела),
		// личная запись с тремя сроками — жёсткий стоп.
		KingdomPolitics state = DemocraticState();
		state.Election!.PastPresidents[0].TermsCount = 3;
		state.Election.TermsServed = 1; // десинхронизация!

		var alwaysWinning = new FakeRandom(0.01); // бросок даёт победу действующего при любом шансе
		for (int year = 15; year <= 60; year += 15)
		{
			ElectionSystem.HoldElection(state, year, null, alwaysWinning);
			Assert.True(state.Election.TermsServed <= 3);
			Assert.True(state.Election.PastPresidents[0].TermsCount <= 3);
		}
	}

	[Fact]
	public void AfterLimitReached_AnotherLeaderIsChosen()
	{
		KingdomPolitics state = DemocraticState();
		state.Election!.PastPresidents[0].TermsCount = 3;
		state.Election.TermsServed = 3;

		ElectionSystem.HoldElection(state, 15, null, new Random(1));

		Assert.NotEqual(1001, state.Ruler!.ActorId);
		Assert.Equal(1, state.Election.TermsServed);
	}

	private sealed class FakeRandom : Random
	{
		private readonly double _v;

		public FakeRandom(double v) { _v = v; }

		public override double NextDouble() => _v;

		public override int Next(int maxValue) => 0;
	}
}
