using System;
using System.Collections.Generic;
using RealPolitic.Core;
using Xunit;

namespace RealPolitic.Tests;

/// <summary>Этап 20: договоры, обиды и казус белли, шпионаж, коалиции, дань и войны за независимость.</summary>
public class DiplomacyDeepTests
{
	// ------------------------------------------------------------------ договоры

	[Fact]
	public void Treaty_Signed_RaisesRelationsAndPublishesEvent()
	{
		var world = TwoKingdomWorld(out _, out _);
		Assert.True(SignUntilSuccess(world, 1, 2, TreatyType.NonAggression));
		Assert.Equal(15, world.GetRulerRelation(1, 2));
		var evt = Assert.Single(world.Bus.OfType(PolEventType.TreatySigned));
		Assert.Equal("RP_EV_TREATY_SIGNED", evt.LocKey);
		Assert.Equal("RP_TREATY_NONAGGRESSION", evt.Get("treaty"));
		Assert.NotNull(world.TreatiesBook.Between(1, 2, TreatyType.NonAggression));
	}

	[Fact]
	public void Treaty_Guards_RejectInvalidSignings()
	{
		var world = TwoKingdomWorld(out _, out _);
		string result;

		Assert.True(SignUntilSuccess(world, 1, 2, TreatyType.NonAggression));
		// Повторно тот же договор не заключается.
		Assert.False(world.TrySignTreaty(1, 2, TreatyType.NonAggression, false, out result));
		// В войне договоры не подписывают.
		Assert.False(world.TrySignTreaty(1, 2, TreatyType.TradeAgreement, true, out result));
		// Оборонительный пакт требует доверия (отношения ниже 10).
		world.SetRulerRelation(1, 2, -50);
		Assert.False(world.TrySignTreaty(1, 2, TreatyType.DefensePact, false, out result));
		// Отключённые договоры не работают.
		world.SetRulerRelation(1, 2, 0);
		world.Params.EnableTreaties = false;
		Assert.False(world.TrySignTreaty(1, 2, TreatyType.TradeAgreement, false, out result));
	}

	[Fact]
	public void Treaty_Expires_AfterTerm_AndLogsToChronicle()
	{
		var world = TwoKingdomWorld(out _, out _);
		world.Params.TreatyYears = 3;
		Assert.True(SignUntilSuccess(world, 1, 2, TreatyType.TradeAgreement));

		// Год подписания + 3 года: договор доживает до срока и тихо истекает.
		// Проверка истечения идёт до инкремента года внутри YearTick, поэтому нужен тик года 9.
		for (int year = 6; year <= 9; year++)
		{
			world.SetYear(year);
			world.YearTick(BothKingdoms(RelationType.Peace));
		}
		Assert.Null(world.TreatiesBook.Between(1, 2, TreatyType.TradeAgreement));
		Assert.Single(world.Bus.OfType(PolEventType.TreatyExpired));
	}

	[Fact]
	public void War_BreaksTreaty_WithReputationPenalties()
	{
		var world = TwoKingdomWorld(out var k1, out _);
		Assert.True(SignUntilSuccess(world, 1, 2, TreatyType.NonAggression));
		float legitimacyBefore = k1.Legitimacy;

		world.SetYear(6);
		world.YearTick(new List<KingdomInput>
		{
			KingdomSnapshot(1, 101, "KingA", RelationType.War),
			KingdomSnapshot(2, 201, "QueenB", RelationType.War)
		});

		Assert.Null(world.TreatiesBook.Between(1, 2, TreatyType.NonAggression));
		Assert.Single(world.Bus.OfType(PolEventType.TreatyBroken));
		// +30 за нарушение договора минус годовой распад обид (4).
		Assert.Equal(26f, world.GetGrievance(2, 1), 1);
		Assert.True(k1.Legitimacy < legitimacyBefore);
	}

	[Fact]
	public void NonAggressionActive_IsVisibleToPlannerGate()
	{
		var world = TwoKingdomWorld(out _, out _);
		Assert.False(world.TreatiesBook.NonAggressionActive(1, 2));
		Assert.True(SignUntilSuccess(world, 1, 2, TreatyType.NonAggression));
		Assert.True(world.TreatiesBook.NonAggressionActive(1, 2));
		// Оборонительный пакт тоже считается обязательством о ненападении.
		world.SetRulerRelation(1, 2, 40);
		Assert.True(SignUntilSuccess(world, 1, 2, TreatyType.DefensePact));
		Assert.True(world.TreatiesBook.NonAggressionActive(2, 1));
	}

	// ------------------------------------------------------------------ казус белли и обиды

	[Fact]
	public void CasusBelli_Best_PicksStrongestAvailableGround()
	{
		var world = TwoKingdomWorld(out _, out _);
		// Ничего нет — остаётся дорогое завоевание.
		Assert.Equal(CasusBelliType.Conquest, world.BestCasusBelli(1, 2, 0));
		// Претензии дают возврат земель.
		Assert.Equal(CasusBelliType.Reconquest, world.BestCasusBelli(1, 2, 3));
		// Накопленная обида — месть.
		world.AddGrievance(1, 2, GrievanceReason.War, 30f);
		Assert.Equal(CasusBelliType.Revenge, world.BestCasusBelli(1, 2, 0));
		// Нарушенный договор перевешивает всё.
		world.AddGrievance(1, 2, GrievanceReason.TreatyBroken, 20f);
		Assert.Equal(CasusBelliType.BrokenTreaty, world.BestCasusBelli(1, 2, 0));
		// Отключённые казус белли всегда возвращают завоевание.
		world.Params.EnableCasusBelli = false;
		Assert.Equal(CasusBelliType.Conquest, world.BestCasusBelli(1, 2, 3));
	}

	[Fact]
	public void DeclareWar_Conquest_IsExpensive_DefenseIsCheap()
	{
		var world = TwoKingdomWorld(out var k1, out var k2);
		k1.Legitimacy = 80f;
		k1.Support = 70f;
		k1.AvgDissent = 0.1f;

		world.ApplyDeclareWar(1, 2, CasusBelliType.Conquest);

		Assert.Equal(68f, k1.Legitimacy, 1);
		Assert.Equal(60f, k1.Support, 1);
		Assert.Equal(0.20f, k1.AvgDissent, 2);
		Assert.Equal(50f, world.GetGrievance(2, 1), 1);
		var record = Assert.Single(world.WarDeclarations);
		Assert.Equal(CasusBelliType.Conquest, record.CasusBelli);
		Assert.Single(world.Bus.OfType(PolEventType.CasusBelliDeclared));

		// Оборона союзника почти ничего не стоит и даже поднимает поддержку.
		var world2 = TwoKingdomWorld(out var d1, out _);
		d1.Legitimacy = 50f;
		d1.Support = 50f;
		world2.ApplyDeclareWar(1, 2, CasusBelliType.Defense);
		Assert.Equal(50f, d1.Legitimacy, 1);
		Assert.Equal(55f, d1.Support, 1);
	}

	[Fact]
	public void DeclareWar_BurnsExistingTreaty()
	{
		var world = TwoKingdomWorld(out _, out _);
		Assert.True(SignUntilSuccess(world, 1, 2, TreatyType.NonAggression));
		world.ApplyDeclareWar(1, 2, CasusBelliType.Conquest);
		Assert.Null(world.TreatiesBook.Between(1, 2, TreatyType.NonAggression));
		Assert.Single(world.Bus.OfType(PolEventType.TreatyBroken));
	}

	[Fact]
	public void Grievances_Decay_EachYear()
	{
		var world = TwoKingdomWorld(out _, out _);
		world.AddGrievance(1, 2, GrievanceReason.War, 40f);
		Assert.Equal(40f, world.GetGrievance(1, 2), 1);
		world.Grievances.Decay(4f);
		Assert.Equal(36f, world.GetGrievance(1, 2), 1);
	}

	// ------------------------------------------------------------------ шпионаж

	[Fact]
	public void Espionage_Invest_RaisesLevel_WithGoldAndCooldownGuards()
	{
		var world = TwoKingdomWorld(out var k1, out _);
		string result;
		Assert.True(world.InvestEspionage(1, 100f, out result), result);
		Assert.Equal(1, k1.Spy.Level);
		// Сразу расширять снова нельзя.
		Assert.False(world.InvestEspionage(1, 100f, out result));
		// Без золота сеть не растёт.
		world.GetKingdom(1).Spy.LastInvestYear -= 5;
		Assert.False(world.InvestEspionage(1, 5f, out result));
		// Потолок уровня.
		k1.Spy.Level = SpyOps.MaxNetworkLevel;
		Assert.False(world.InvestEspionage(1, 100f, out result));
	}

	[Fact]
	public void CovertOp_Sabotage_Success_LowersProsperity()
	{
		var world = TwoKingdomWorld(out _, out var k2);
		string result;
		Assert.True(world.InvestEspionage(1, 100f, out result));
		k2.Economy = new EconomyState { Prosperity = 70f };
		// Ищем зерно, при котором бросок успеха проходит (шанс саботажа сети 1 = 0.6).
		int successSeed = FindCovertSeed(world, SpyOpType.Sabotage, wantSuccess: true);
		Assert.True(successSeed >= 0, "не нашлось зерна с успешным саботажем");
		// Чистый прогон: события предыдущих попыток не должны влиять на проверки.
		k2.Economy.Prosperity = 70f;
		world.ClearEvents();
		world.GetKingdom(1).Spy.LastOpYear = int.MinValue;
		world.Rng = new Random(successSeed);
		Assert.True(world.TryCovertOp(1, 2, SpyOpType.Sabotage, out result));
		Assert.Equal(58f, k2.Economy.Prosperity, 1);
		Assert.Single(world.Bus.OfType(PolEventType.EspionageSuccess));
		Assert.Empty(world.Bus.OfType(PolEventType.EspionageExposed));
	}

	[Fact]
	public void CovertOp_Fail_WithExposure_PunishesSpyState()
	{
		var world = TwoKingdomWorld(out var k1, out _);
		string result;
		Assert.True(world.InvestEspionage(1, 100f, out result));
		// Ищем зерно: провал (бросок >= 0.55 для подстрекательства) и немедленное разоблачение (бросок < 0.5).
		int exposedSeed = FindExposedSeed(world, SpyOpType.InciteDissent);
		Assert.True(exposedSeed >= 0, "не нашлось зерна с разоблачением");
		// Чистый мир для проверки последствий: штрафы не должны двоиться от перебора зёрен.
		var fresh = TwoKingdomWorld(out var f1, out _);
		Assert.True(fresh.InvestEspionage(1, 100f, out result));
		f1.Legitimacy = 60f;
		fresh.Rng = new Random(exposedSeed);
		Assert.False(fresh.TryCovertOp(1, 2, SpyOpType.InciteDissent, out result));
		Assert.Single(fresh.Bus.OfType(PolEventType.EspionageExposed));
		Assert.Equal(52f, f1.Legitimacy, 1);
		Assert.Equal(0, f1.Spy.Level);
		Assert.Equal(-40, fresh.GetRulerRelation(1, 2));
		Assert.Equal(35f, fresh.GetGrievance(2, 1), 1);
	}

	[Fact]
	public void CovertOp_Guards_NoNetworkOrCooldown()
	{
		var world = TwoKingdomWorld(out var k1, out _);
		string result;
		// Сети нет.
		Assert.False(world.TryCovertOp(1, 2, SpyOpType.Sabotage, out result));
		world.InvestEspionage(1, 100f, out result);
		// Операция прошла — вторая в тот же год невозможна.
		world.Rng = new Random(0);
		world.TryCovertOp(1, 2, SpyOpType.Sabotage, out result);
		Assert.False(world.TryCovertOp(1, 2, SpyOpType.Sabotage, out result));
	}

	[Fact]
	public void CovertOp_StealGold_QueuesTransferOnSuccess()
	{
		var world = TwoKingdomWorld(out _, out _);
		string result;
		Assert.True(world.InvestEspionage(1, 100f, out result));
		int successSeed = FindCovertSeed(world, SpyOpType.StealGold, wantSuccess: true);
		Assert.True(successSeed >= 0);
		world.ClearPendingCalls();
		world.GetKingdom(1).Spy.LastOpYear = int.MinValue;
		world.Rng = new Random(successSeed);
		Assert.True(world.TryCovertOp(1, 2, SpyOpType.StealGold, out result));
		var transfer = Assert.Single(world.PendingGoldTransfers);
		Assert.Equal(2, transfer.Key);
		Assert.Equal(1, transfer.Value);
	}

	// ------------------------------------------------------------------ коалиции

	[Fact]
	public void Coalition_FormsAgainstAggressor_WithEnoughAnger()
	{
		var world = ThreeKingdomWorld();
		// Держава 1 развязывает две войны завоевания: 60 очков агрессии.
		world.ApplyDeclareWar(1, 2, CasusBelliType.Conquest);
		world.ApplyDeclareWar(1, 3, CasusBelliType.Conquest);
		// Пострадавшие уже обижены (по 50); третьему добавим отношения.
		world.SetRulerRelation(3, 1, -30);

		world.SetYear(6);
		world.YearTick(ThreeKingdoms(RelationType.Peace));

		var coalition = world.Coalitions.Against(1);
		Assert.NotNull(coalition);
		Assert.Contains(2, coalition.Members);
		Assert.Contains(3, coalition.Members);
		Assert.Single(world.Bus.OfType(PolEventType.CoalitionFormed));
	}

	[Fact]
	public void Coalition_AttackOnMember_RaisesDefenseCalls()
	{
		var world = ThreeKingdomWorld();
		world.ApplyDeclareWar(1, 2, CasusBelliType.Conquest);
		world.ApplyDeclareWar(1, 3, CasusBelliType.Conquest);
		world.SetRulerRelation(3, 1, -30);
		world.SetYear(6);
		world.YearTick(ThreeKingdoms(RelationType.Peace));
		var coalition = world.Coalitions.Against(1);
		Assert.NotNull(coalition);
		world.ClearPendingCalls();

		// Агрессор нападает на участника коалиции — второй участник получает призыв.
		int yearNow = world.Year;
		world.ApplyDeclareWar(1, 2, CasusBelliType.Revenge);
		var call = Assert.Single(world.PendingDefenseCalls);
		Assert.Equal(3, call.Key);
		Assert.Equal(1, call.Value);
		Assert.Equal(yearNow, coalition.LastWarYear);
		Assert.Single(world.Bus.OfType(PolEventType.CoalitionWar));
	}

	[Fact]
	public void Coalition_Dissolves_AfterLongPeace()
	{
		var world = ThreeKingdomWorld();
		world.ApplyDeclareWar(1, 2, CasusBelliType.Conquest);
		world.ApplyDeclareWar(1, 3, CasusBelliType.Conquest);
		world.SetYear(6);
		world.YearTick(ThreeKingdoms(RelationType.Peace));
		var coalition = world.Coalitions.Against(1);
		Assert.NotNull(coalition);

		world.Coalitions.Against(1).LastWarYear -= world.Params.CoalitionPeaceYears + 1;
		world.SetYear(7);
		world.YearTick(ThreeKingdoms(RelationType.Peace));
		Assert.Null(world.Coalitions.Against(1));
		Assert.Single(world.Bus.OfType(PolEventType.CoalitionDissolved));
	}

	[Fact]
	public void DefensePact_AttackOnPartner_QueuesDefenseCall_PactSurvives()
	{
		var world = ThreeKingdomWorld();
		world.SetRulerRelation(1, 2, 40);
		Assert.True(SignUntilSuccess(world, 1, 2, TreatyType.DefensePact));
		world.ClearPendingCalls();

		// Третья держава нападает на участника пакта.
		world.ApplyDeclareWar(3, 1, CasusBelliType.Conquest);
		var call = Assert.Single(world.PendingDefenseCalls);
		Assert.Equal(2, call.Key);
		Assert.Equal(3, call.Value);
		// 0.23: пакт — обязательство на срок договора, а не одноразовый талон: он остаётся в силе.
		Assert.NotNull(world.TreatiesBook.Between(1, 2, TreatyType.DefensePact));
	}

	// ------------------------------------------------------------------ вассалитет

	[Fact]
	public void Tribute_CollectedFromVassals_DrainsApproval()
	{
		var world = VassalWorld(out Union union, out KingdomPolitics vassal);
		vassal.Economy = new EconomyState { LastIncome = 100f };
		float approvalBefore = union.GetMember(2).Approval;

		var orders = world.CollectTributes();
		var order = Assert.Single(orders);
		Assert.Equal(2, order.VassalId);
		Assert.Equal(1, order.SuzerainId);
		Assert.Equal(10f, order.Amount, 2);
		Assert.Equal(approvalBefore - 0.01f, union.GetMember(2).Approval, 3);

		// Без дохода дани нет.
		vassal.Economy.LastIncome = 0f;
		Assert.Empty(world.CollectTributes());
	}

	[Fact]
	public void Independence_LeavesUnion_BurnsBridges()
	{
		var world = VassalWorld(out Union union, out _);
		string result;
		Assert.True(world.LeaveUnionForIndependence(2, out result), result);
		Assert.False(union.Contains(2));
		Assert.Single(world.Bus.OfType(PolEventType.VassalRebellion));
		Assert.Equal(-30, world.GetRulerRelation(2, 1));
		Assert.Equal(25f, world.GetGrievance(1, 2), 1);
		// Повторно восстать нельзя — держава уже свободна.
		Assert.False(world.LeaveUnionForIndependence(2, out result));
	}

	[Fact]
	public void ApprovalCollapseOfLiveVassal_QueuesAnIndependenceWarInsteadOfPeacefulExit()
	{
		var world = VassalWorld(out Union union, out KingdomPolitics vassal);
		union.GetMember(2).Approval = 0f;
		vassal.AvgDissent = 1f;
		world.ClearEvents();

		world.SetYear(6);
		world.YearTick(BothKingdoms(RelationType.Peace));

		Assert.False(union.Contains(2));
		var pendingWar = Assert.Single(world.PendingIndependenceWars);
		Assert.Equal(2, pendingWar.Key);
		Assert.Equal(1, pendingWar.Value);
		Assert.Single(world.Bus.OfType(PolEventType.VassalRebellion));
	}

	// ------------------------------------------------------------------ планировщик

	[Fact]
	public void Planner_DeclaresWar_WithCasusBelli()
	{
		var plan = KingdomStrategicPlanner.Choose(PeacefulSnapshot(declareWar: true));
		Assert.Equal(KingdomActionType.DeclareWar, plan.Action);
	}

	[Fact]
	public void Planner_DeclaresIndependence_WhenApprovalCollapses()
	{
		var plan = KingdomStrategicPlanner.Choose(new KingdomStrategySnapshot
		{
			KingdomId = 2,
			Year = 20,
			Population = 100,
			Warriors = 20,
			MaxWarriors = 20,
			Food = 1000f,
			Gold = 500f,
			Stone = 500f,
			Legitimacy = 60f,
			Support = 60f,
			LastActionYear = int.MinValue,
			VassalOfId = 1,
			VassalApproval = 0.1f,
			VassalRebellionApproval = 0.3f
		});
		Assert.Equal(KingdomActionType.DeclareIndependence, plan.Action);
		Assert.True(plan.Score >= 70f);
	}

	[Fact]
	public void Planner_MobilizesVassal_WhenSuzerainAtWar()
	{
		var plan = KingdomStrategicPlanner.Choose(new KingdomStrategySnapshot
		{
			KingdomId = 2,
			Year = 20,
			Population = 100,
			Warriors = 30,
			MaxWarriors = 100,
			Food = 1000f,
			Gold = 500f,
			Stone = 500f,
			Legitimacy = 60f,
			Support = 60f,
			LastActionYear = int.MinValue,
			VassalOfId = 1,
			VassalApproval = 0.8f,
			SuzerainAtWar = true
		});
		Assert.Equal(KingdomActionType.Mobilize, plan.Action);
	}

	[Fact]
	public void Planner_SignsTreaty_AndInvestsEspionage()
	{
		var treaty = KingdomStrategicPlanner.Choose(new KingdomStrategySnapshot
		{
			KingdomId = 1,
			Year = 20,
			Population = 100,
			Warriors = 10,
			MaxWarriors = 20,
			Food = 1000f,
			Gold = 60f,
			Stone = 500f,
			Legitimacy = 60f,
			Support = 60f,
			LastActionYear = int.MinValue,
			TreatiesEnabled = true,
			TreatyCandidateId = 2,
			TreatyType = TreatyType.TradeAgreement,
			TreatyScore = 55f
		});
		Assert.Equal(KingdomActionType.SignTreaty, treaty.Action);

		var spy = KingdomStrategicPlanner.Choose(new KingdomStrategySnapshot
		{
			KingdomId = 1,
			Year = 20,
			Population = 100,
			Warriors = 10,
			MaxWarriors = 20,
			Food = 1000f,
			Gold = 200f,
			Stone = 500f,
			Legitimacy = 40f,
			Support = 60f,
			LastActionYear = int.MinValue,
			EspionageEnabled = true,
			SpyLevel = 1
		});
		Assert.Equal(KingdomActionType.InvestEspionage, spy.Action);
	}

	// ------------------------------------------------------------------ сохранение

	[Fact]
	public void SaveLoad_DeepDiplomacy_Roundtrip()
	{
		var world = ThreeKingdomWorld();
		string result;
		world.SetRulerRelation(1, 2, 40);
		Assert.True(SignUntilSuccess(world, 1, 2, TreatyType.DefensePact));
		world.AddGrievance(3, 1, GrievanceReason.War, 42f);
		world.ApplyDeclareWar(3, 2, CasusBelliType.Revenge);
		// Пакт 1–2 списан призывом на оборону — подписываем новый договор, чтобы секция не опустела.
		Assert.True(SignUntilSuccess(world, 1, 3, TreatyType.NonAggression));
		world.InvestEspionage(1, 100f, out result);

		string json = SaveData.Write(world);
		Assert.Contains("\"treaties\"", json);
		Assert.Contains("\"griev\"", json);
		Assert.Contains("\"wars\"", json);
		Assert.Contains("\"spy\"", json);

		var restored = new PoliticalWorld();
		SaveData.Apply(restored, json);
		Assert.NotNull(restored.TreatiesBook.Between(1, 3, TreatyType.NonAggression));
		Assert.Equal(42f, restored.GetGrievance(3, 1), 1);
		var record = Assert.Single(restored.WarDeclarations);
		Assert.Equal(CasusBelliType.Revenge, record.CasusBelli);
		Assert.Equal(1, restored.GetKingdom(1).Spy.Level);
	}

	[Fact]
	public void OldWorlds_LoadWithoutDeepDiplomacy_Sections()
	{
		var world = TwoKingdomWorld(out _, out _);
		string json = SaveData.Write(world);
		Assert.DoesNotContain("\"treaties\"", json);
		Assert.DoesNotContain("\"griev\"", json);
		Assert.DoesNotContain("\"coals\"", json);
		Assert.DoesNotContain("\"wars\"", json);

		var restored = new PoliticalWorld();
		SaveData.Apply(restored, json);
		Assert.Equal(0, restored.TreatiesBook.Count);
		Assert.Empty(restored.Coalitions.All);
		Assert.Empty(restored.WarDeclarations);
		Assert.Null(restored.GetKingdom(1).Spy);
	}

	// ------------------------------------------------------------------ хелперы

	/// <summary>Две мирные монархии-соседки после базового тика.</summary>
	private static PoliticalWorld TwoKingdomWorld(out KingdomPolitics k1, out KingdomPolitics k2, int seed = 42)
	{
		var world = new PoliticalWorld();
		world.Rng = new Random(seed);
		world.SetBaseline(true);
		world.SetYear(5);
		world.AddKingdom(1, IdeologyType.Monarchy, 0);
		world.AddKingdom(2, IdeologyType.Monarchy, 0);
		world.AddCity(10, 1);
		world.AddCity(20, 2);
		world.YearTick(new List<KingdomInput>
		{
			KingdomSnapshot(1, 101, "KingA", RelationType.Peace, name: "Alpha"),
			KingdomSnapshot(2, 201, "QueenB", RelationType.Peace, name: "Beta")
		});
		k1 = world.GetKingdom(1);
		k2 = world.GetKingdom(2);
		return world;
	}

	private static PoliticalWorld ThreeKingdomWorld(int seed = 42)
	{
		var world = new PoliticalWorld();
		world.Rng = new Random(seed);
		world.SetBaseline(true);
		world.SetYear(5);
		world.AddKingdom(1, IdeologyType.Monarchy, 0);
		world.AddKingdom(2, IdeologyType.Monarchy, 0);
		world.AddKingdom(3, IdeologyType.Monarchy, 0);
		world.AddCity(10, 1);
		world.AddCity(20, 2);
		world.AddCity(30, 3);
		world.YearTick(new List<KingdomInput>
		{
			KingdomSnapshot(1, 101, "KingA", RelationType.Peace, name: "Alpha"),
			KingdomSnapshot(2, 201, "QueenB", RelationType.Peace, name: "Beta"),
			KingdomSnapshot(3, 301, "KingC", RelationType.Peace, name: "Gamma")
		});
		return world;
	}

	private static PoliticalWorld VassalWorld(out Union union, out KingdomPolitics vassal)
	{
		var world = TwoKingdomWorld(out _, out vassal);
		var leader = world.GetKingdom(1);
		leader.Legitimacy = 80f;
		vassal.Legitimacy = 60f;
		DiplomatSnapshot memberSnap = new DiplomatSnapshot
		{
			KingdomId = 2,
			Ideology = IdeologyType.Monarchy,
			Legitimacy = 60f,
			Warriors = 10,
			Population = 100
		};
		Unions.JoinCheck check;
		union = world.TryCreateUnion(UnionType.Vassal, 1, 2, "Вассалитет", memberSnap, out check, 0, 0);
		Assert.NotNull(union);
		vassal.Union = union;
		return world;
	}

	private static List<KingdomInput> BothKingdoms(RelationType relation)
	{
		return new List<KingdomInput>
		{
			KingdomSnapshot(1, 101, "KingA", relation),
			KingdomSnapshot(2, 201, "QueenB", relation)
		};
	}

	private static List<KingdomInput> ThreeKingdoms(RelationType relation)
	{
		return new List<KingdomInput>
		{
			KingdomSnapshot(1, 101, "KingA", relation),
			KingdomSnapshot(2, 201, "QueenB", relation),
			KingdomSnapshot(3, 301, "KingC", relation)
		};
	}

	private static KingdomStrategySnapshot PeacefulSnapshot(bool declareWar)
	{
		return new KingdomStrategySnapshot
		{
			KingdomId = 1,
			Year = 20,
			Ideology = IdeologyType.Monarchy,
			Population = 100,
			Warriors = 10,
			MaxWarriors = 20,
			Food = 1000f,
			Gold = 500f,
			Stone = 500f,
			Legitimacy = 60f,
			Support = 60f,
			LastActionYear = int.MinValue,
			CasusBelliEnabled = declareWar,
			WarTargetId = declareWar ? 2 : 0,
			WarCasusBelli = CasusBelliType.Revenge,
			WarScore = declareWar ? 60f : 0f
		};
	}

	private static KingdomInput KingdomSnapshot(int kingdomId, int rulerActorId, string rulerName,
		RelationType relation, string name = null)
	{
		var relations = new List<RelationInfo>();
		int partner = kingdomId == 1 ? 2 : 1;
		relations.Add(new RelationInfo { OtherId = partner, Relation = relation, Adjacent = true });
		if (kingdomId == 1)
		{
			relations.Add(new RelationInfo { OtherId = 3, Relation = relation, Adjacent = true });
		}
		else if (kingdomId == 3)
		{
			relations.Add(new RelationInfo { OtherId = 1, Relation = relation, Adjacent = true });
		}
		return new KingdomInput
		{
			KingdomId = kingdomId,
			Name = name ?? ("K" + kingdomId),
			Population = 100,
			Warriors = 5,
			Ruler = new RulerRef
			{
				ActorId = rulerActorId,
				Name = rulerName,
				KingdomId = kingdomId,
				SinceYear = 1,
				Age = 30,
				ClanName = "Дом" + kingdomId,
				ClanId = rulerActorId
			},
			Relations = relations,
			Cities = new List<CityInput>
			{
				new CityInput
				{
					CityId = kingdomId * 10,
					KingdomId = kingdomId,
					Population = 100,
					FoodOk = true,
					CultureId = 7,
					CultureName = "C",
					ReligionId = 3,
					ReligionName = "R",
					LanguageId = 2,
					LanguageName = "L"
				}
			}
		};
	}

	/// <summary>Подбирает зерно, при котором подписание договора проходит.</summary>
	private static bool SignUntilSuccess(PoliticalWorld world, int a, int b, TreatyType type)
	{
		for (int seed = 0; seed < 500; seed++)
		{
			world.Rng = new Random(seed);
			string r;
			if (world.TrySignTreaty(a, b, type, false, out r))
				return true;
		}
		return false;
	}

	/// <summary>Подбирает зерно с нужным исходом тайной операции (успех/провал).</summary>
	private static int FindCovertSeed(PoliticalWorld world, SpyOpType op, bool wantSuccess)
	{
		for (int seed = 0; seed < 4000; seed++)
		{
			world.GetKingdom(1).Spy.LastOpYear = int.MinValue;
			world.Rng = new Random(seed);
			string r;
			if (world.TryCovertOp(1, 2, op, out r) == wantSuccess)
				return seed;
		}
		return -1;
	}

	/// <summary>Подбирает зерно, при котором операция проваливается именно с разоблачением.</summary>
	private static int FindExposedSeed(PoliticalWorld world, SpyOpType op)
	{
		for (int seed = 0; seed < 4000; seed++)
		{
			world.GetKingdom(1).Spy.LastOpYear = int.MinValue;
			world.Rng = new Random(seed);
			int exposedBefore = world.Bus.OfType(PolEventType.EspionageExposed).Count;
			string r;
			if (!world.TryCovertOp(1, 2, op, out r) && world.Bus.OfType(PolEventType.EspionageExposed).Count > exposedBefore)
				return seed;
		}
		return -1;
	}
}
