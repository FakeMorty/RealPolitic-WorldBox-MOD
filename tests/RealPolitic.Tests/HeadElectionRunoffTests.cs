using System;
using System.Collections.Generic;
using System.Linq;
using RealPolitic.Core;
using Xunit;

namespace RealPolitic.Tests;

/// <summary>
/// 0.48.5: второй тур выборов главы.
/// Владелец 29.10.2026: «Второй тур нужен. Это сделает выборы интереснее».
///
/// Раньше при равном числе голосов выборы просто объявлялись несостоявшимися
/// («нет единственного победителя»), и в катке на 606 лет таких строк было 15.
/// Теперь жители голосуют ещё раз — только за тех, кто разделил первое место.
/// </summary>
public sealed class HeadElectionRunoffTests
{
	private sealed class Port : ICivicHeadSource
	{
		public List<CivicResident> People = new List<CivicResident>();
		public int Installed;
		public bool TryReadCitizens(int country, out IList<CivicResident> residents)
		{
			residents = People;
			return true;
		}
		public bool TryInstallHead(int country, int actor)
		{
			Installed = actor;
			return true;
		}
	}

	private static CivicResident Resident(int id, int age, IdeologyType belief)
		=> new CivicResident
		{
			Person = new CivicPerson { ActorId = id, Age = age },
			Belief = new CivicBelief { Ideology = belief, LastStateIdeology = belief, Conviction = 1 },
			Name = "Житель " + id
		};

	private static PoliticalWorld World()
	{
		PoliticalWorld world = new PoliticalWorld();
		world.BeginNewWorldGovernance("runoff-world");
		world.SetYear(51);
		KingdomPolitics country = world.AddKingdom(1, IdeologyType.Democracy, 10);
		country.Constitution.StateForm = HeadOfState.Elected;
		country.Constitution.Electoral.Electors = Franchise.Universal;
		country.Constitution.Electoral.Candidates = Franchise.Universal;
		country.Constitution.Electoral.TermYears = 5;
		country.Ruler = new RulerRef { ActorId = 101, Name = "Прежний", KingdomId = 1 };
		return world;
	}

	/// <summary>
	/// Расклад для второго тура (семь жителей, картина 3:3:1).
	///
	///  101 — демократ, 60 лет   → за него: он сам, 104 и 105 (3 голоса)
	///  102 — милитарист, 60 лет  → за него: он сам, 106 и 107 (3 голоса)
	///  103 — теократ, 40 лет     → за него только он сам (1 голос)
	///
	/// Первый тур: 3:3 — 101 и 102 делят первое место, и выборы, по старому коду,
	/// объявлялись несостоявшимися. Во втором туре остаются только 101 и 102,
	/// и голос теократа 103 переходит к милитаристу 102 (теократия теснее с милитаризмом,
	/// чем с демократией): 4:3 в пользу 102.
	/// </summary>
	private static Port SplitElectorate()
	{
		return new Port
		{
			People = new List<CivicResident>
			{
				Resident(101, 60, IdeologyType.Democracy),
				Resident(102, 60, IdeologyType.Militarism),
				Resident(103, 40, IdeologyType.Theocracy),
				Resident(104, 30, IdeologyType.Democracy),
				Resident(105, 30, IdeologyType.Democracy),
				Resident(106, 30, IdeologyType.Militarism),
				Resident(107, 30, IdeologyType.Militarism)
			}
		};
	}

	[Fact]
	public void TieInTheFirstRoundGivesASecondRoundNotAFailure()
	{
		PoliticalWorld world = World();
		Port port = SplitElectorate();
		// Два сторонника демократии и два сторонника монархии — 2:2.
		CivicHeadElections.Tick(world, world.GetKingdom(1), 51, port);
		CivicProcedure procedure = world.Governance.Procedures.Values.Single();
		Assert.Contains("runoff", procedure.Ballot.Id);
		Assert.Equal(CivicProcedureStage.Executed, procedure.Stage);
		Assert.Equal(102, port.Installed);
	}

	[Fact]
	public void SecondRoundIsWrittenIntoTheChronicle()
	{
		PoliticalWorld world = World();
		CivicHeadElections.Tick(world, world.GetKingdom(1), 51, SplitElectorate());
		Assert.Single(world.Bus.OfType(PolEventType.DemocraticElection)
			.Where(e => e.LocKey == "RP_GOV26_EV_HEAD_RUNOFF"));
	}

	[Fact]
	public void SecondRoundOnlyOffersTheCandidatesWhoTied()
	{
		PoliticalWorld world = World();
		CivicHeadElections.Tick(world, world.GetKingdom(1), 51, SplitElectorate());
		CivicProcedure procedure = world.Governance.Procedures.Values.Single();
		Assert.Contains("runoff", procedure.Ballot.Id);
		// Теократ не делил первое место — во второй тур он не попадает,
		// а его избиратели голосуют за одного из двоих.
		Assert.DoesNotContain(103, procedure.Ballot.Capture().Options);
		Assert.Equal(2, procedure.Ballot.Capture().Options.Count);
	}

	[Fact]
	public void NoRunoffWhenThereIsAClearWinner()
	{
		PoliticalWorld world = World();
		Port port = new Port
		{
			People = new List<CivicResident>
			{
				Resident(101, 80, IdeologyType.Monarchy),
				Resident(102, 40, IdeologyType.Monarchy),
				Resident(103, 30, IdeologyType.Democracy)
			}
		};
		Assert.True(CivicHeadElections.Tick(world, world.GetKingdom(1), 51, port));
		CivicProcedure procedure = world.Governance.Procedures.Values.Single();
		Assert.DoesNotContain("runoff", procedure.Ballot.Id);
		Assert.Equal(101, port.Installed);
	}
}
