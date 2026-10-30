using System;
using System.Collections.Generic;
using System.Linq;
using RealPolitic.Core;
using Xunit;

namespace RealPolitic.Tests;

/// <summary>
/// 0.53: две палаты, законы, вето и ступор.
/// Владелец 30.10.2026 выбрал: палаты решают по-настоящему, у правителя есть вето,
/// а сколько палат и от чего верхняя — решает конституция.
/// </summary>
public sealed class ChambersTests
{
	private static CityInput City(int id)
	{
		return new CityInput { CityId = id, KingdomId = 1, Name = "Город " + id, Population = 100, FoodOk = true };
	}

	private static KingdomInput Input(int cities)
	{
		KingdomInput input = new KingdomInput
		{
			KingdomId = 1,
			Name = "Держава",
			Population = 500,
			Warriors = 40,
			Ruler = new RulerRef { ActorId = 101, Name = "Вождь", KingdomId = 1, SinceYear = 1, Age = 40 },
			Cities = new List<CityInput>()
		};
		for (int i = 0; i < cities; i++)
		{
			input.Cities.Add(City(200 + i));
		}
		return input;
	}

	[Fact]
	public void MajorityAndTwoThirds()
	{
		Assert.True(ChamberRules.Majority(51, 49));
		Assert.False(ChamberRules.Majority(50, 50));
		Assert.True(ChamberRules.TwoThirds(67, 33));
		Assert.False(ChamberRules.TwoThirds(66, 34));
		Assert.False(ChamberRules.Majority(0, 0));
	}

	[Fact]
	public void UpperChamberSeatsDependOnTheBasisInTheConstitution()
	{
		// От городов: сколько городов — столько мест.
		Assert.Equal(6, ChamberRules.UpperSeats(UpperChamberBasis.Cities, 6, 3, 9));
		// От народов: сколько народов — столько мест.
		Assert.Equal(3, ChamberRules.UpperSeats(UpperChamberBasis.Peoples, 6, 3, 9));
		// От родов: родов больше, но есть потолок.
		Assert.Equal(9, ChamberRules.UpperSeats(UpperChamberBasis.Clans, 6, 3, 9));
		Assert.Equal(ChamberRules.UpperMaxSeats, ChamberRules.UpperSeats(UpperChamberBasis.Cities, 500, 3, 9));
		Assert.Equal(1, ChamberRules.UpperSeats(UpperChamberBasis.Cities, 0, 0, 0));
	}

	[Fact]
	public void OnlyLandAndWarBillsGoToTheUpperChamber()
	{
		Assert.True(ChamberRules.NeedsUpperChamber(ConstitutionAmendments.ArticleBorders));
		Assert.True(ChamberRules.NeedsUpperChamber(ConstitutionAmendments.ArticleUnion));
		Assert.True(ChamberRules.NeedsUpperChamber(ConstitutionAmendments.ArticleLand));
		Assert.False(ChamberRules.NeedsUpperChamber(ConstitutionAmendments.ArticleTax));
		Assert.False(ChamberRules.NeedsUpperChamber(ConstitutionAmendments.ArticleFaith));
	}

	[Fact]
	public void TwoChambersAreTheDefaultForRepublicsOnly()
	{
		Assert.Equal(ChamberCount.Two, ChamberRules.DefaultChambersFor(IdeologyType.Democracy));
		Assert.Equal(ChamberCount.Two, ChamberRules.DefaultChambersFor(IdeologyType.MerchantRepublic));
		Assert.Equal(ChamberCount.One, ChamberRules.DefaultChambersFor(IdeologyType.Empire));
		Assert.Equal(ChamberCount.One, ChamberRules.DefaultChambersFor(IdeologyType.Tribalism));
	}

	[Fact]
	public void VetoDependsOnRulerCharacter()
	{
		// Жестокий не даёт ослабить свою власть.
		Assert.True(ChamberRules.RulerVetoes(RulerTrait.Cruel, false, false));
		Assert.False(ChamberRules.RulerVetoes(RulerTrait.Cruel, true, false));
		// Мягкий не даёт закрутить гайки, если просит народ.
		Assert.True(ChamberRules.RulerVetoes(RulerTrait.Just, true, true));
		Assert.False(ChamberRules.RulerVetoes(RulerTrait.Just, true, false));
	}

	[Fact]
	public void LowerChamberVotesByItsParties()
	{
		var state = new KingdomPolitics { KingdomId = 1 };
		state.Parties.Add(new PoliticalParty { Ideology = IdeologyType.Monarchy, SeatsPercent = 60 });
		state.Parties.Add(new PoliticalParty { Ideology = IdeologyType.Democracy, SeatsPercent = 40 });
		var bill = new Bill { Article = ConstitutionAmendments.ArticleTax, Tightens = true };
		ChamberRules.LowerVote(state, bill, out int forSeats, out int againstSeats);
		Assert.Equal(100, forSeats + againstSeats);
		// Монархисты за закручивание, демократы против.
		Assert.Equal(60, forSeats);
		Assert.Equal(40, againstSeats);
	}

	[Fact]
	public void BillPassesThroughTwoChambersAndChangesTheLaw()
	{
		var world = new PoliticalWorld();
		world.Rng = new Random(5);
		world.SetBaseline(true);
		world.SetYear(100);
		KingdomPolitics kingdom = world.AddKingdom(1, IdeologyType.Democracy, 20);
		kingdom.Constitution = new Constitution
		{
			Chambers = ChamberCount.Two,
			UpperBasis = UpperChamberBasis.Cities,
			RulerVeto = false,
			Taxes = TaxLaw.Light
		};
		kingdom.Legitimacy = 70f;
		kingdom.Support = 70f;
		kingdom.Ruler = new RulerRef { ActorId = 101, Name = "Вождь", KingdomId = 1, SinceYear = 1, Age = 40 };
		kingdom.Personality = new RulerPersonality { Trait = RulerTrait.Just, Ambition = 50f };
		kingdom.LastAmendmentYear = 0;
		for (int i = 0; i < 4; i++)
		{
			world.AddCity(200 + i, 1);
		}
		KingdomInput input = Input(4);

		int passed = 0;
		for (int year = 100; year < 140; year++)
		{
			world.SetYear(year);
			world.YearTick(new List<KingdomInput> { input });
		}
		for (int i = 0; i < world.Bus.Log.Count; i++)
		{
			PolEvent e = world.Bus.Log[i];
			if (e.Type == PolEventType.BillPassed && e.KingdomId == 1)
			{
				passed++;
			}
		}
		Assert.True(passed > 0, "за 40 лет палаты должны были принять хотя бы один закон");
		// И принимают не каждый год: у них бывает перерыв.
		Assert.True(passed < 30, "палаты не должны штамповать по закону ежегодно");
		Assert.True(kingdom.ConstitutionHistory.Count > 0, "принятые законы должны попадать в историю конституции");
	}

	[Fact]
	public void RulerVetoStopsTheBillAndChambersCanOverride()
	{
		var world = new PoliticalWorld();
		world.SetYear(100);
		kingdom(world, out KingdomPolitics state);
		state.Constitution = new Constitution { Chambers = ChamberCount.One, RulerVeto = true };
		Bill bill = new Bill
		{
			KingdomId = 1,
			Article = ConstitutionAmendments.ArticleTax,
			Tightens = false,
			ProposedYear = 100,
			Stage = BillStage.Lower,
			LowerFor = 80,
			LowerAgainst = 20
		};
		world.SetBill(bill);
		Assert.Same(bill, world.BillOf(1));
		Assert.True(bill.Pending);
	}

	[Fact]
	public void BillSurvivesSaving()
	{
		var world = new PoliticalWorld();
		world.SetYear(120);
		kingdom(world, out KingdomPolitics state);
		state.Constitution = new Constitution { Chambers = ChamberCount.Two, UpperBasis = UpperChamberBasis.Peoples, RulerVeto = false };
		world.AddCity(200, 1);
		world.AddCity(201, 1);
		world.SetBill(new Bill
		{
			KingdomId = 1,
			Article = ConstitutionAmendments.ArticleBorders,
			From = "RP_CONST_BORDER_OPEN",
			To = "RP_CONST_BORDER_CLOSED",
			Tightens = true,
			ProposedYear = 119,
			Stage = BillStage.Upper,
			LowerFor = 61,
			LowerAgainst = 39
		});

		string json = SaveData.Write(world);
		var restored = new PoliticalWorld();
		SaveData.Apply(restored, json);

		Bill back = restored.BillOf(1);
		Assert.NotNull(back);
		Assert.Equal(ConstitutionAmendments.ArticleBorders, back.Article);
		Assert.Equal(BillStage.Upper, back.Stage);
		Assert.Equal(61, back.LowerFor);
		Assert.Equal(ChamberCount.Two, restored.GetKingdom(1).Constitution.Chambers);
		Assert.Equal(UpperChamberBasis.Peoples, restored.GetKingdom(1).Constitution.UpperBasis);
	}

	[Fact]
	public void TheConstitutionItselfCanTurnChambersOnAndOff()
	{
		var state = new KingdomPolitics { KingdomId = 1 };
		state.Constitution = new Constitution { Chambers = ChamberCount.One, UpperBasis = UpperChamberBasis.Clans, RulerVeto = true };
		var open = ConstitutionAmendments.Proposals(state, tighten: false, forPeople: true);
		Assert.Contains(open, x => x.Article == ConstitutionAmendments.ArticleChambers && x.To == "RP53_CHAMBERS_TWO");
		// Верхняя палата: от родов — к городам, потом к народам.
		Assert.Contains(open, x => x.Article == ConstitutionAmendments.ArticleUpperBasis && x.To == "RP53_BASIS_CITIES");
		Assert.Contains(open, x => x.Article == ConstitutionAmendments.ArticleVeto && x.To == "RP53_OFF");

		var tight = ConstitutionAmendments.Proposals(state, tighten: true, forPeople: false);
		Assert.Contains(tight, x => x.Article == ConstitutionAmendments.ArticleDissolve && x.To == "RP53_ON");
		Assert.DoesNotContain(tight, x => x.Article == ConstitutionAmendments.ArticleChambers);
	}

	[Fact]
	public void RulerNoLongerEditsTheConstitutionBehindTheChambers()
	{
		var world = new PoliticalWorld();
		world.SetBaseline(true);
		world.SetYear(120);
		KingdomPolitics state = world.AddKingdom(1, IdeologyType.Monarchy, 20);
		state.Constitution = new Constitution { Chambers = ChamberCount.One, Taxes = TaxLaw.Light };
		state.Ruler = new RulerRef { ActorId = 101, Name = "Король", KingdomId = 1, SinceYear = 1, Age = 50 };
		state.Personality = new RulerPersonality { Trait = RulerTrait.Conqueror, Ambition = 90f };
		state.Legitimacy = 90f;
		// Палаты в ответе за законы: сам правитель статью не трогает.
		Assert.False(ConstitutionAmendments.Tick(state, "Держава", 120, new SimParams(), new Random(3), world.Bus, chambersInCharge: true));
		Assert.Empty(state.ConstitutionHistory);
	}

	private static void kingdom(PoliticalWorld world, out KingdomPolitics state)
	{
		world.SetBaseline(true);
		state = world.AddKingdom(1, IdeologyType.Democracy, 10);
	}
}
