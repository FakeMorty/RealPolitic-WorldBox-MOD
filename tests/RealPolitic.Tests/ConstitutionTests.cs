using System;
using System.Collections.Generic;
using RealPolitic.Core;
using Xunit;

namespace RealPolitic.Tests;

/// <summary>Этап 26.1 «Фундамент конституции»: шесть статей, формы правления как подвид
/// идеологии, война — следствие формы, голосования живых жителей, наследование отпочковками.</summary>
public class ConstitutionTests
{
	private static PoliticalWorld NewWorld()
	{
		return new PoliticalWorld();
	}

	private static List<(long ActorId, IdeologyType Ideology)> Sample(IdeologyType ideology, int count)
	{
		var list = new List<(long, IdeologyType)>();
		for (int i = 0; i < count; i++)
		{
			list.Add((1000 + i, ideology));
		}
		return list;
	}

	[Fact]
	public void WarAuthority_FollowsStateForm()
	{
		Assert.Equal(WarAuthority.ByRuler, ConstitutionGates.WarAuthorityOf(HeadOfState.Hereditary));
		Assert.Equal(WarAuthority.ByRuler, ConstitutionGates.WarAuthorityOf(HeadOfState.Elected));
		Assert.Equal(WarAuthority.ByAssembly, ConstitutionGates.WarAuthorityOf(HeadOfState.ParliamentaryMonarchy));
		Assert.Equal(WarAuthority.ByAssembly, ConstitutionGates.WarAuthorityOf(HeadOfState.Parliamentary));
		Assert.Equal(WarAuthority.ByPeople, ConstitutionGates.WarAuthorityOf(HeadOfState.Assembly));
	}

	[Fact]
	public void Defaults_MonarchyDependsOnEra()
	{
		Constitution darkAges = ConstitutionDefaults.For(IdeologyType.Monarchy, IdeologyEpoch.DarkAges);
		Assert.Equal(HeadOfState.Hereditary, darkAges.StateForm); // абсолютная
		Assert.Equal(SupremacyRule.Advisory, darkAges.Supremacy); // обычай, не писаный закон
		Assert.Equal(WarAuthority.ByRuler, darkAges.WarPower);

		Constitution modern = ConstitutionDefaults.For(IdeologyType.Monarchy, IdeologyEpoch.Modern);
		Assert.Equal(HeadOfState.ParliamentaryMonarchy, modern.StateForm);
		Assert.Equal(SupremacyRule.Binding, modern.Supremacy);
		Assert.Equal(WarAuthority.ByAssembly, modern.WarPower);
	}

	[Fact]
	public void Defaults_DemocracyIsPresidential_ByRulerWar()
	{
		Constitution c = ConstitutionDefaults.For(IdeologyType.Democracy, IdeologyEpoch.Modern);
		Assert.Equal(HeadOfState.Elected, c.StateForm);
		Assert.True(c.HoldsElectionsForHead);
		Assert.Equal(WarAuthority.ByRuler, c.WarPower); // президентская: войну решает исполнительная ветвь
		Assert.Equal(Franchise.Universal, c.Franchise);
	}

	[Fact]
	public void Defaults_CommunismPartyDecidesWar_EgalitarianismVeche()
	{
		Constitution communism = ConstitutionDefaults.For(IdeologyType.Communism, IdeologyEpoch.Modern);
		Assert.Equal(SupremacyRule.Binding, communism.Supremacy);
		Assert.Equal(WarAuthority.ByRuler, communism.WarPower); // по утверждённой §2.3 таблице коммунизм — ByRuler

		Constitution egalitarian = ConstitutionDefaults.For(IdeologyType.Egalitarianism, IdeologyEpoch.EarlyModern);
		Assert.Equal(HeadOfState.Assembly, egalitarian.StateForm); // вече
		Assert.Equal(WarAuthority.ByPeople, egalitarian.WarPower);
		Assert.Equal(Franchise.Universal, egalitarian.Franchise);
	}

	[Fact]
	public void HoldsElections_OnlyElectiveForms()
	{
		Assert.True(ConstitutionGates.HoldsElections(HeadOfState.Elected));
		Assert.True(ConstitutionGates.HoldsElections(HeadOfState.Parliamentary));
		Assert.False(ConstitutionGates.HoldsElections(HeadOfState.Hereditary));
		Assert.False(ConstitutionGates.HoldsElections(HeadOfState.ParliamentaryMonarchy));
		Assert.False(ConstitutionGates.HoldsElections(HeadOfState.Assembly));
	}

	[Fact]
	public void WarGate_AdvisoryAlwaysPasses_AndAssemblyBlocksWithoutMandate()
	{
		var state = new KingdomPolitics { KingdomId = 1, Ideology = IdeologyType.Democracy };
		// обычай — просто шумит
		state.Constitution = new Constitution { StateForm = HeadOfState.Assembly, Supremacy = SupremacyRule.Advisory };
		Assert.True(ConstitutionGates.WarIsConstitutionallyAllowed(state, 2, 100));

		// Binding + ByAssembly — голосование обязательно
		state.Constitution = new Constitution { StateForm = HeadOfState.Parliamentary, Supremacy = SupremacyRule.Binding };
		Assert.False(ConstitutionGates.WarIsConstitutionallyAllowed(state, 2, 100));

		// правитель (ByRuler) — не спрашивает никого
		state.Constitution = new Constitution { StateForm = HeadOfState.Hereditary, Supremacy = SupremacyRule.Binding };
		Assert.True(ConstitutionGates.WarIsConstitutionallyAllowed(state, 2, 100));
	}

	[Fact]
	public void WarAuthorization_ExpliresAfterTerm()
	{
		var state = new KingdomPolitics { KingdomId = 1 };
		state.WarAuthorizations.Add(new WarAuthorization { TargetKingdomId = 7, Year = 100, ValidUntilYear = 103 });
		Assert.True(state.HasWarAuthorization(7, 100));
		Assert.True(state.HasWarAuthorization(7, 103));
		Assert.False(state.HasWarAuthorization(7, 104)); // срок вышел — колоть нельзя
		Assert.False(state.HasWarAuthorization(9, 101)); // на другую державу мандат не расползается
	}

	[Fact]
	public void Referendum_NoQuorum_IsVoid()
	{
		stateSetup(out KingdomPolitics state, IdeologyType.Militarism);
		state.LastVoterSample = Sample(IdeologyType.Militarism, 3); // меньше кворума в 5
		bool passed = WarReferendum.Hold(state, "Военные", 2, "Цель", 100, new Random(1), null, new SimParams());
		Assert.False(passed);
		Assert.Empty(state.WarAuthorizations);
		Assert.Empty(state.ReferendumCooldowns); // несостоявшееся — без издержек
	}

	[Fact]
	public void Referendum_MilitaristVoters_GrantThreeYearMandate()
	{
		stateSetup(out KingdomPolitics state, IdeologyType.Militarism);
		state.Legitimacy = 60f;
		state.LastVoterSample = Sample(IdeologyType.Militarism, 60); // аппетит 0.85 — отказ маловероятен
		bool passed = WarReferendum.Hold(state, "Военные", 2, "Цель", 100, new Random(7), null, new SimParams());
		Assert.True(passed);
		WarAuthorization auth = Assert.Single(state.WarAuthorizations);
		Assert.Equal(2, auth.TargetKingdomId);
		Assert.Equal(103, auth.ValidUntilYear); // мандат на 3 года (AuthorizationYears)
		Assert.True(auth.VotesFor + auth.VotesAgainst == 60);
		Assert.True(state.HasWarAuthorization(2, 101));
	}

	[Fact]
	public void Referendum_PacifistVotersReject_WithPriceAndCooldown()
	{
		// Эгалитаризм (аппетит 0.30) — самый миролюбивый строй после упразднения изоляционизма:
		// собрание почти наверняка отвечает «нет» войне.
		stateSetup(out KingdomPolitics state, IdeologyType.Egalitarianism);
		state.Legitimacy = 50f;
		state.LastVoterSample = Sample(IdeologyType.Egalitarianism, 60);
		bool passed = WarReferendum.Hold(state, "Затворники", 2, "Цель", 100, new Random(3), new PolEventBus(), new SimParams());
		Assert.False(passed);
		Assert.Equal(46f, state.Legitimacy); // -4 за то, что правитель дёргал собрание зря
		Assert.True(state.ReferendumCooldowns[2] >= 110); // кулодаун по умолчанию 10 лет
	}

	[Fact]
	public void Referendum_CooldownBlocksRepeat()
	{
		stateSetup(out KingdomPolitics state, IdeologyType.Egalitarianism);
		state.LastVoterSample = Sample(IdeologyType.Egalitarianism, 60);
		Assert.False(WarReferendum.Hold(state, "Затворники", 2, "Цель", 100, new Random(3), null, new SimParams()));
		// попытка тем же годом тратит ничего: ни голоса, ни новых издержек
		float legitimacy = state.Legitimacy;
		Assert.False(WarReferendum.Hold(state, "Затворники", 2, "Цель", 100, new Random(5), null, new SimParams()));
		Assert.Equal(legitimacy, state.Legitimacy);
	}

	[Fact]
	public void UnconstitutionalWar_PenalizesOnlyWithoutMandate()
	{
		PoliticalWorld world = NewWorld();
		KingdomPolitics attacker = world.AddKingdom(1, IdeologyType.Aristocracy, 0); // сенат = ByAssembly Binding
		world.AddKingdom(2, IdeologyType.Monarchy, 0);
		Assert.NotNull(attacker.Constitution);
		Assert.Equal(WarAuthority.ByAssembly, attacker.Constitution.WarPower);
		float before = attacker.Legitimacy;

		world.ApplyConstitutionalViolation(1, 2);
		Assert.Equal(before - 12f, attacker.Legitimacy); // война без мандата сената

		// с действующим мандатом — всё честно, ничего не штрафуется
		attacker.Legitimacy = before;
		attacker.WarAuthorizations.Add(new WarAuthorization { TargetKingdomId = 2, Year = 100, ValidUntilYear = 103 });
		world.ApplyConstitutionalViolation(1, 2);
		Assert.Equal(before, attacker.Legitimacy);
	}

	[Fact]
	public void BorderGates_RespectBindingSupremacy()
	{
		PoliticalWorld world = NewWorld();
		KingdomPolitics a = world.AddKingdom(1, IdeologyType.Monarchy, 0);
		world.AddKingdom(2, IdeologyType.Monarchy, 0);

		a.Constitution = new Constitution { Borders = BorderLaw.Open, Supremacy = SupremacyRule.Binding };
		Assert.False(world.ApplyBorderPolicy(1, 2, BorderStatus.Closed)); // закон Open: не закрыть
		Assert.True(world.ApplyBorderPolicy(1, 2, BorderStatus.Open));

		a.Constitution = new Constitution { Borders = BorderLaw.Open, Supremacy = SupremacyRule.Advisory };
		Assert.True(world.ApplyBorderPolicy(1, 2, BorderStatus.Closed)); // обычай не вяжет

		a.Constitution = new Constitution { Borders = BorderLaw.Closed, Supremacy = SupremacyRule.Binding };
		Assert.False(world.ApplyBorderPolicy(1, 2, BorderStatus.Open)); // закон Closed: не открыть
	}

	[Fact]
	public void Breakaway_GetsFreshConstitutionOfItsOwnIdeology()
	{
		// 0.23: повстанцы воюют ЗА СВОЙ строй — отколовшееся государство получает
		// новую конституцию своей идеологии (а не копию родительской, против которой восстали).
		PoliticalWorld world = NewWorld();
		world.SetYear(100);
		KingdomPolitics parent = world.AddKingdom(1, IdeologyType.Democracy, 0);
		parent.Constitution = new Constitution
		{
			StateForm = HeadOfState.Parliamentary,
			Franchise = Franchise.Notables,
			Borders = BorderLaw.Controlled,
			Succession = SuccessionRule.AnyAdult,
			Supremacy = SupremacyRule.Binding
		};
		KingdomPolitics child = world.CreateBreakawayKingdom(2, parent, null, IdeologyType.Militarism, 100);
		Assert.Equal(HeadOfState.Hereditary, child.Constitution.StateForm); // дефолт милитаризма (стратократия), не «наследие» демократии
		Assert.Equal(Franchise.Notables, child.Constitution.Franchise);
		Assert.Single(child.ConstitutionHistory);
		// Первая правка — «основана повстанцами» (RP_CONST_FOUNDED_BY_REBELS), а не «унаследована».
		Assert.Equal("RP_CONST_FOUNDED_BY_REBELS", child.ConstitutionHistory[0].To);
	}

	[Fact]
	public void SaveData_RoundTrip_PreservesConstitution()
	{
		PoliticalWorld world = NewWorld();
		KingdomPolitics state = world.AddKingdom(1, IdeologyType.Aristocracy, 0);
		state.Constitution = new Constitution
		{
			StateForm = HeadOfState.Parliamentary,
			Franchise = Franchise.Notables,
			Borders = BorderLaw.Closed,
			Succession = SuccessionRule.Strict,
			Supremacy = SupremacyRule.Binding
		};
		state.ConstitutionHistory.Add(new ConstitutionEdit { Year = 90, Article = "границы", From = "Open", To = "Closed", EditorName = "Сенат" });
		state.WarAuthorizations.Add(new WarAuthorization { TargetKingdomId = 5, Year = 95, ValidUntilYear = 98, VotesFor = 30, VotesAgainst = 12 });
		state.ReferendumCooldowns[5] = 105;
		state.LastConstitutionalUsurpYear = 77;

		string json = SaveData.Write(world);
		Assert.Contains("\"format\":18", json, StringComparison.Ordinal);

		var restored = new PoliticalWorld();
		SaveData.Apply(restored, json);
		KingdomPolitics back = restored.GetKingdom(1);
		Assert.NotNull(back.Constitution);
		Assert.Equal(HeadOfState.Parliamentary, back.Constitution.StateForm);
		Assert.Equal(Franchise.Notables, back.Constitution.Franchise);
		Assert.Equal(BorderLaw.Closed, back.Constitution.Borders);
		Assert.Equal(SuccessionRule.Strict, back.Constitution.Succession);
		Assert.Equal(SupremacyRule.Binding, back.Constitution.Supremacy);
		ConstitutionEdit edit = Assert.Single(back.ConstitutionHistory);
		Assert.Equal(90, edit.Year);
		Assert.Equal("Сенат", edit.EditorName);
		WarAuthorization auth = Assert.Single(back.WarAuthorizations);
		Assert.Equal(5, auth.TargetKingdomId);
		Assert.Equal(98, auth.ValidUntilYear);
		Assert.Equal(105, back.ReferendumCooldowns[5]);
		Assert.Equal(77, back.LastConstitutionalUsurpYear);
	}

	private static void stateSetup(out KingdomPolitics state, IdeologyType ideology)
	{
		state = new KingdomPolitics { KingdomId = 1, Ideology = ideology };
		state.Constitution = new Constitution { StateForm = HeadOfState.Parliamentary, Supremacy = SupremacyRule.Binding };
	}

	[Fact]
	public void EffectiveWarAuthority_ActiveRegency_StripsRegentOfSoloWarRight()
	{
		var state = new KingdomPolitics { KingdomId = 1, Ideology = IdeologyType.Monarchy };
		state.Constitution = new Constitution { StateForm = HeadOfState.Hereditary, Supremacy = SupremacyRule.Binding };
		Assert.Equal(WarAuthority.ByRuler, ConstitutionGates.EffectiveWarAuthority(state));

		// Активное регентство с регентом на троне: правитель больше не решает единолично.
		state.Regency = new RegencyState();
		state.Regency.Start(100, "Опекун", 2);
		state.Ruler = new RulerRef { ActorId = 2, Name = "Опекун", IsRegent = true, SinceYear = 100, Age = 40 };

		Assert.Equal(WarAuthority.ByAssembly, ConstitutionGates.EffectiveWarAuthority(state));
		// Войну без мандата не объявить: ByAssembly + активное регентство.
		Assert.False(ConstitutionGates.WarIsConstitutionallyAllowed(state, 9, 100));

		// Регентство завершилось — полномочия вернулись правителю.
		state.Regency.End();
		Assert.Equal(WarAuthority.ByRuler, ConstitutionGates.EffectiveWarAuthority(state));
		Assert.True(ConstitutionGates.WarIsConstitutionallyAllowed(state, 9, 100));
	}

	[Fact]
	public void RulerMayAmend_AfterRegencyEnded_Allowed()
	{
		// 0.23.13: регентство оставляло объект RegencyState с IsActive=false —
		// поправки навсегда запрещались правителю, пережившему регентство.
		var state = new KingdomPolitics { KingdomId = 1, Ideology = IdeologyType.Monarchy };
		state.Constitution = new Constitution { StateForm = HeadOfState.Hereditary, Supremacy = SupremacyRule.Binding };
		state.Ruler = new RulerRef { ActorId = 3, Name = "Король", SinceYear = 90, Age = 24 };
		var p = new SimParams { EnableConstitution = true, RulerAmendmentYears = 15, RulerAmendmentLegitimacy = 50f };
		state.Legitimacy = 80f;

		// Идёт регентство — править грамоту нельзя.
		state.Regency = new RegencyState();
		state.Regency.Start(100, "Опекун", 2);
		Assert.False(ConstitutionAmendments.RulerMayAmend(state, 110, p));

		// Регентство закончилось — право вернулось.
		state.Regency.End();
		Assert.True(ConstitutionAmendments.RulerMayAmend(state, 115, p));
	}
}
