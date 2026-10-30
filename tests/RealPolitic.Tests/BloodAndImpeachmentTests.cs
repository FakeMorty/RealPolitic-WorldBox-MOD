using System;
using System.Collections.Generic;
using RealPolitic.Core;
using Xunit;

namespace RealPolitic.Tests;

/// <summary>Статьи XIII «О кровных связях» и XIV «Об импичменте» (0.24):
/// право крови снимает клеймо узурпатора; импичмент отзывает главу в выборной державе.</summary>
public class BloodAndImpeachmentTests
{
	// ------------------------------------------------------------------ статья XIII

	[Fact]
	public void BloodClaim_CurrentLaw_NeverClaims()
	{
		var state = Monarchy();
		state.Constitution.Blood = BloodLaw.Current;
		state.DynastyReignsCount = 2;
		state.PreviousRoyalClanId = 2002;

		// Смена дома на чужой род — право крови молчит: узурпация как обычно.
		Assert.False(ConstitutionGates.BloodClaim(state, dynastyShifted: true, inheritedByClaim: false, clanId: 2002, RoyalClans(state)));
	}

	[Fact]
	public void BloodClaim_DirectBlood_RecognizesPreviousHouse()
	{
		var state = Monarchy();
		state.Constitution.Blood = BloodLaw.DirectBlood;
		state.PreviousRoyalClanId = 2002;

		// Претендент из прежнего правящего дома — не узурпатор.
		Assert.True(ConstitutionGates.BloodClaim(state, dynastyShifted: true, inheritedByClaim: false, clanId: 2002, RoyalClans(state)));
		// Посторонний род — узурпация.
		Assert.False(ConstitutionGates.BloodClaim(state, dynastyShifted: true, inheritedByClaim: false, clanId: 9999, RoyalClans(state)));
	}

	[Fact]
	public void BloodClaim_AnyRoyalBlood_UsesRoyalAncestorIds()
	{
		var state = Monarchy();
		state.Constitution.Blood = BloodLaw.AnyRoyalBlood;
		state.PreviousRoyalClanId = 2002;

		// Любой венценосец даёт право: род претендента в списке венценосных.
		Assert.True(ConstitutionGates.BloodClaim(state, dynastyShifted: true, inheritedByClaim: false, clanId: 3003, new long[] { 1001, 3003 }));
		// Рода нет в списке — узурпация.
		Assert.False(ConstitutionGates.BloodClaim(state, dynastyShifted: true, inheritedByClaim: false, clanId: 4004, new long[] { 1001 }));
	}

	[Fact]
	public void BloodClaim_AdvisoryOrNonMonarchy_NoEffect()
	{
		// Обычай: право крови не связывает.
		var advisory = Monarchy();
		advisory.Constitution.Supremacy = SupremacyRule.Advisory;
		advisory.Constitution.Blood = BloodLaw.DirectBlood;
		advisory.PreviousRoyalClanId = 2002;
		Assert.False(ConstitutionGates.BloodClaim(advisory, true, false, 2002, RoyalClans(advisory)));

		// Не монархия: право крови — дело венценосных домов.
		var democracy = new KingdomPolitics { KingdomId = 1, Ideology = IdeologyType.Democracy };
		democracy.Constitution = new Constitution { Supremacy = SupremacyRule.Binding, Blood = BloodLaw.DirectBlood };
		democracy.PreviousRoyalClanId = 2002;
		Assert.False(ConstitutionGates.BloodClaim(democracy, true, false, 2002, RoyalClans(democracy)));
	}

	[Fact]
	public void Succession_BloodHeir_NotTaggedUsurper()
	{
		var world = new PoliticalWorld();
		world.Rng = new Random(3);
		world.SetBaseline(true);
		world.SetYear(10);
		var k = world.AddKingdom(1, IdeologyType.Monarchy, 0);
		// «Кровь любого венценосца»: законный претендент из прежнего королевского дома
		// вправе вернуть венец без клейма узурпатора.
		k.Constitution.Blood = BloodLaw.AnyRoyalBlood;
		k.Constitution.Supremacy = SupremacyRule.Binding;

		// Дом А правит.
		world.YearTick(new List<KingdomInput> { Ruler(1, 101, "Alfred", "ДомА", 1001) });
		// Дом Б сменил (узурпация), и род А запомнен в венценосной истории.
		world.SetYear(11);
		world.YearTick(new List<KingdomInput> { Ruler(1, 102, "Bruno", "ДомБ", 2002) });
		Assert.Equal("ДомБ", k.RoyalClanName);
		Assert.Equal((long)1001, k.PreviousRoyalClanId);
		Assert.Single(world.Bus.OfType(PolEventType.ConstitutionalUsurpation));

		// Возвращается наследник дома А — по праву крови это не узурпация.
		world.ClearEvents();
		world.SetYear(12);
		world.YearTick(new List<KingdomInput> { Ruler(1, 103, "Adel", "ДомА", 1001) });
		Assert.Equal("ДомА", k.RoyalClanName);
		Assert.Empty(world.Bus.OfType(PolEventType.ConstitutionalUsurpation));
	}

	[Fact]
	public void Succession_BloodHeir_UnderCurrentLaw_IsUsurper()
	{
		var world = new PoliticalWorld();
		world.Rng = new Random(3);
		world.SetBaseline(true);
		world.SetYear(10);
		var k = world.AddKingdom(1, IdeologyType.Monarchy, 0);
		k.Constitution.Blood = BloodLaw.Current;
		k.Constitution.Supremacy = SupremacyRule.Binding;

		world.YearTick(new List<KingdomInput> { Ruler(1, 101, "Alfred", "ДомА", 1001) });
		world.SetYear(11);
		world.YearTick(new List<KingdomInput> { Ruler(1, 102, "Bruno", "ДомБ", 2002) });
		world.ClearEvents();
		world.SetYear(12);
		world.YearTick(new List<KingdomInput> { Ruler(1, 103, "Adel", "ДомА", 1001) });

		// Право крови не записано (Current): возвращение дома А — снова узурпация.
		Assert.Single(world.Bus.OfType(PolEventType.ConstitutionalUsurpation));
	}

	// ------------------------------------------------------------------ статья XIV

	[Fact]
	public void Impeachment_Forbidden_EvenInElectiveState()
	{
		var state = Democracy();
		state.Constitution.Impeachment = ImpeachmentLaw.Forbidden;
		Assert.False(ConstitutionGates.ImpeachmentDue(state, support: 20f, holdsElections: true));
	}

	[Fact]
	public void Impeachment_RequiresElections()
	{
		var monarchy = Monarchy();
		monarchy.Constitution.Impeachment = ImpeachmentLaw.PeopleVote;
		Assert.False(ConstitutionGates.ImpeachmentDue(monarchy, support: 10f, holdsElections: false));
	}

	[Fact]
	public void Impeachment_RequiresLowSupport()
	{
		var state = Democracy();
		state.Constitution.Impeachment = ImpeachmentLaw.PeopleVote;

		// Поддержка выше половины — отзывать не за что.
		Assert.False(ConstitutionGates.ImpeachmentDue(state, support: 60f, holdsElections: true));
		// Поддержка ниже половины — основание есть.
		Assert.True(ConstitutionGates.ImpeachmentDue(state, support: 40f, holdsElections: true));
	}

	[Fact]
	public void Impeachment_Success_RecallsRulerAndPublishesEvent()
	{
		// Зерно с гарантированным вотумом: перебором находим прошедший случай.
		for (int seed = 0; seed < 400; seed++)
		{
			var world = ElectionWorld(seed);
			var k = world.GetKingdom(1);
			world.SetYear(30);
			world.YearTick(new List<KingdomInput> { Snap(1) });

			if (world.Bus.OfType(PolEventType.Impeached).Count == 1)
			{
				Assert.Null(k.Ruler);
				Assert.Equal(30, k.LastImpeachmentYear);
				PolEvent evt = world.Bus.OfType(PolEventType.Impeached)[0];
				Assert.Equal("Президент", evt.Get("ruler"));
				return;
			}
		}
		Assert.Fail("не нашлось зерна со сработавшим вотумом импичмента");
	}

	[Fact]
	public void Impeachment_SkipsNonElectiveState()
	{
		var world = new PoliticalWorld();
		world.Rng = new Random(0);
		world.SetBaseline(true);
		world.SetYear(20);
		var k = world.AddKingdom(1, IdeologyType.Monarchy, 0);
		k.Constitution = new Constitution { Supremacy = SupremacyRule.Binding, Impeachment = ImpeachmentLaw.PeopleVote };
		k.Support = 10f;
		world.AddCity(10, 1);

		for (int year = 21; year <= 40; year++)
		{
			world.SetYear(year);
			world.YearTick(new List<KingdomInput> { Snap(1) });
		}

		// В монархии выборов нет — вотум ни разу не собирался.
		Assert.Empty(world.Bus.OfType(PolEventType.Impeached));
		Assert.NotNull(k.Ruler);
	}

	// ------------------------------------------------------------------ хелперы

	private static KingdomPolitics Monarchy()
	{
		var state = new KingdomPolitics { KingdomId = 1, Ideology = IdeologyType.Monarchy };
		state.Constitution = new Constitution { Supremacy = SupremacyRule.Binding, Blood = BloodLaw.Current };
		return state;
	}

	private static KingdomPolitics Democracy()
	{
		var state = new KingdomPolitics { KingdomId = 1, Ideology = IdeologyType.Democracy };
		state.Constitution = new Constitution { Supremacy = SupremacyRule.Binding, Impeachment = ImpeachmentLaw.PeopleVote };
		return state;
	}

	private static IEnumerable<long> RoyalClans(KingdomPolitics state)
	{
		var list = new List<long>();
		if (state.RoyalClanId > 0) list.Add(state.RoyalClanId);
		if (state.PreviousRoyalClanId > 0) list.Add(state.PreviousRoyalClanId);
		return list;
	}

	private static PoliticalWorld ElectionWorld(int seed)
	{
		var world = new PoliticalWorld();
		world.Rng = new Random(seed);
		world.Params.EnableEraRestrictions = false; // демократия доступна сразу
		world.SetBaseline(true);
		world.SetYear(20);
		var k = world.AddKingdom(1, IdeologyType.Democracy, 0);
		k.Constitution = new Constitution { Supremacy = SupremacyRule.Binding, Impeachment = ImpeachmentLaw.PeopleVote };
		k.Support = 20f;   // ниже половины — есть основание
		k.Legitimacy = 80f;
		world.AddCity(10, 1);
		world.AddCity(11, 1);
		return world;
	}

	private static KingdomInput Snap(int id)
	{
		return new KingdomInput
		{
			KingdomId = id,
			Name = "Держава",
			Population = 400,
			Warriors = 5,
			Ruler = new RulerRef { ActorId = 101, Name = "Президент", KingdomId = id, SinceYear = 20, Age = 40 },
			Relations = new List<RelationInfo>(),
			Cities = new List<CityInput>
			{
				new CityInput { CityId = 10, KingdomId = id, Population = 200, FoodOk = true, LanguageId = 2 },
				new CityInput { CityId = 11, KingdomId = id, Population = 200, FoodOk = true, LanguageId = 2 }
			}
		};
	}

	private static KingdomInput Ruler(int kingdomId, int rulerId, string name, string clan, long clanId)
	{
		return new KingdomInput
		{
			KingdomId = kingdomId,
			Name = "Держава",
			Population = 100,
			Warriors = 5,
			Ruler = new RulerRef
			{
				ActorId = rulerId,
				Name = name,
				KingdomId = kingdomId,
				SinceYear = 1,
				Age = 30,
				ClanName = clan,
				ClanId = clanId
			},
			Relations = new List<RelationInfo>(),
			Cities = new List<CityInput>
			{
				new CityInput { CityId = 10, KingdomId = kingdomId, Population = 100, FoodOk = true, LanguageId = 2, LanguageName = "L" }
			}
		};
	}
}
