using System;
using System.Collections.Generic;
using RealPolitic.Core;
using Xunit;

namespace RealPolitic.Tests;

/// <summary>Этап 22: война и мирные договоры — усталость от войны, перемирии,
/// условия победителя (унижение, репарации, уступка земель), белый мир.</summary>
public class Stage22Tests
{
	// ================================================================== усталость от войны

	[Fact]
	public void Fatigue_GrowsDuringWar_AndDecaysInPeace()
	{
		var world = TwoKingdomWorld(out KingdomPolitics k1, out _);
		TickWar(world, 6, atWar: true);

		Assert.True(k1.WarFatigue > 0f);
		float warPeak = k1.WarFatigue;
		// Монархия копит без множителя: 6 лет × 7 = 42.
		Assert.Equal(42f, warPeak, 1);

		TickWar(world, 3, atWar: false);
		Assert.True(k1.WarFatigue < warPeak);
		// 3 мирных года × 9 = 27 спада.
		Assert.Equal(15f, k1.WarFatigue, 1);
	}

	[Fact]
	public void Fatigue_MilitarismEndures_EgalitarianismBreaksFast()
	{
		var world = new PoliticalWorld();
		world.Rng = new Random(42);
		world.SetBaseline(true);
		// Год 200: эгалитаризм уже легален (эпоха нового времени), иначе страж эпохи
		// отодвинет его к ближайшему допустимому строю.
		world.SetYear(200);
		world.AddKingdom(1, IdeologyType.Militarism, 0);
		world.AddKingdom(2, IdeologyType.Egalitarianism, 0);
		world.AddCity(10, 1);
		world.AddCity(20, 2);
		TickWar(world, 6, atWar: true);

		KingdomPolitics milit = world.GetKingdom(1)!;
		KingdomPolitics egal = world.GetKingdom(2)!;
		// 6 лет: милитаризм 6×7×0.6 = 25.2, эгалитаризм 6×7×1.5 = 63.
		Assert.Equal(25.2f, milit.WarFatigue, 1);
		Assert.Equal(63f, egal.WarFatigue, 1);
		Assert.True(milit.WarFatigue < egal.WarFatigue);
	}

	[Fact]
	public void Fatigue_High_DrainsLegitimacyDissentAndSupport()
	{
		var world = TwoKingdomWorld(out KingdomPolitics k1, out KingdomPolitics k2);
		// Разогреваем усталость напрямую: эффекты применяются на следующем тике.
		// Симметричную власть сравниваем со второй державой без усталости —
		// тик пересчитывает поддержку/недовольство по переписи, поэтому мерим дельту.
		k1.WarFatigue = 90f;
		TickWar(world, 1, atWar: true);

		Assert.True(k1.WarFatigue > 90f);
		Assert.True(k1.Legitimacy < k2.Legitimacy, $"легитимность изнурённой державы ниже: {k1.Legitimacy} vs {k2.Legitimacy}");
		Assert.True(k1.AvgDissent > k2.AvgDissent, $"недовольство изнурённой державы выше: {k1.AvgDissent} vs {k2.AvgDissent}");
		Assert.True(k1.Support <= k2.Support, $"поддержка изнурённой державы не выше: {k1.Support} vs {k2.Support}");
	}

	[Fact]
	public void Fatigue_Disabled_StaysZero()
	{
		var world = TwoKingdomWorld(out KingdomPolitics k1, out _);
		world.Params.EnableWarPeace = false;
		TickWar(world, 4, atWar: true);
		Assert.Equal(0f, k1.WarFatigue);
	}

	[Fact]
	public void Fatigue_Exhaustion_RaisesCivilWarChance()
	{
		var world = TwoKingdomWorld(out KingdomPolitics k1, out _);
		k1.AvgDissent = 0.4f;
		k1.Legitimacy = 25f;
		var input = Snapshot(1, RelationType.War);
		k1.WarFatigue = 0f;
		float calm = CivilWars.StartChance(k1, input, world.Params);
		k1.WarFatigue = 100f;
		float exhausted = CivilWars.StartChance(k1, input, world.Params);

		Assert.True(calm > 0f);
		Assert.True(exhausted > calm * 1.2f, $"усталость 100 должна заметно поднимать шанс мятежа: {calm} → {exhausted}");
	}

	// ================================================================== развязка войны

	[Fact]
	public void Settlement_ConquerorLongWar_DemandsLandAndReparations()
	{
		var world = TwoKingdomWorld(out KingdomPolitics k1, out KingdomPolitics k2);
		world.ApplyDeclareWar(1, 2, CasusBelliType.Conquest);
		k1.WarFatigue = 60f;
		k2.WarFatigue = 40f;
		k1.Legitimacy = 70f;
		k2.Legitimacy = 50f;
		int year = world.Year;
		world.ClearEvents();

		PeaceSettlement settlement = world.ResolveWarEnd(1, 2, winnerId: 1, warYears: 8)!;

		Assert.NotNull(settlement);
		Assert.Equal(SettlementLevel.LandDemand, settlement.Level);
		// 8 лет войны / 2 = 4 города, потолок MaxCededCities = 3.
		Assert.Equal(3, settlement.CitiesDemanded);
		var demand = Assert.Single(world.PendingCityDemands);
		Assert.Equal(2, demand.FromId);
		Assert.Equal(1, demand.ToId);
		Assert.Equal(3, demand.Count);
		Assert.True(settlement.ReparationsPerYear > 0f);
		Assert.Equal(world.Params.ReparationsYears, settlement.ReparationsYears);
		var rep = Assert.Single(world.Reparations);
		Assert.Equal(2, rep.PayerId);
		Assert.Equal(1, rep.PayeeId);
		Assert.Equal(year + world.Params.TruceYears, settlement.TruceUntilYear);
		Assert.True(world.TruceActiveBetween(1, 2));
		// Унижение: проигравший −15 легитимности, победитель +5.
		Assert.True(k2.Legitimacy <= 35.5f, $"легитимность проигравшего должна упасть: {k2.Legitimacy}");
		Assert.Equal(75f, k1.Legitimacy);
		// Облегчение: усталость обеих сторон делится надвое.
		Assert.Equal(30f, k1.WarFatigue);
		Assert.Equal(20f, k2.WarFatigue);
		// Событие с полными параметрами договора.
		var evt = Assert.Single(world.Bus.OfType(PolEventType.PeaceSettlement));
		Assert.Equal("RP_EV_PEACE_SETTLEMENT", evt.LocKey);
		Assert.Equal("RP_WARTERMS_FULL", evt.Get("terms"));
		Assert.Equal("3", evt.Get("cities"));
		Assert.Equal(Severity.Major, evt.Severity);
		Assert.Empty(world.Bus.OfType(PolEventType.WhitePeace));
	}

	[Fact]
	public void Settlement_DefenderVictory_NoLand_OnlyReparations()
	{
		var world = TwoKingdomWorld(out KingdomPolitics k1, out KingdomPolitics k2);
		world.ApplyDeclareWar(1, 2, CasusBelliType.Conquest);
		k1.Legitimacy = 50f;
		k2.Legitimacy = 70f;
		world.ClearEvents();

		// Агрессор разбит: победа защитника земли захватчику не даёт.
		PeaceSettlement settlement = world.ResolveWarEnd(1, 2, winnerId: 2, warYears: 8)!;

		Assert.Equal(SettlementLevel.Reparations, settlement.Level);
		Assert.Equal(0, settlement.CitiesDemanded);
		Assert.Empty(world.PendingCityDemands);
		var rep = Assert.Single(world.Reparations);
		Assert.Equal(1, rep.PayerId);
		Assert.Equal(2, rep.PayeeId);
		var evt = Assert.Single(world.Bus.OfType(PolEventType.PeaceSettlement));
		Assert.Equal("RP_WARTERMS_PARTIAL", evt.Get("terms"));
	}

	[Fact]
	public void Settlement_DefensiveCbVictor_NeverDemandsLand()
	{
		var world = TwoKingdomWorld(out KingdomPolitics k1, out _);
		// Даже длинную войну по оборонительному поводу победитель не конвертирует в земли.
		world.ApplyDeclareWar(1, 2, CasusBelliType.Defense);
		k1.Legitimacy = 90f;
		world.GetKingdom(2)!.Legitimacy = 10f;
		world.ClearEvents();

		PeaceSettlement settlement = world.ResolveWarEnd(1, 2, winnerId: 1, warYears: 12)!;

		Assert.Equal(SettlementLevel.Reparations, settlement.Level);
		Assert.Empty(world.PendingCityDemands);
	}

	[Fact]
	public void Settlement_ShortWar_HumiliationOnly()
	{
		var world = TwoKingdomWorld(out KingdomPolitics k1, out KingdomPolitics k2);
		world.ApplyDeclareWar(1, 2, CasusBelliType.Conquest);
		k1.Legitimacy = 50f;
		k2.Legitimacy = 50f;
		world.ClearEvents();

		// 2 года: счёт 40 — хватает только на унижение. Победил защитник (k2),
		// проигравший агрессор (k1) унижается на 8 легитимности.
		PeaceSettlement settlement = world.ResolveWarEnd(1, 2, winnerId: 2, warYears: 2)!;

		Assert.Equal(SettlementLevel.Humiliation, settlement.Level);
		Assert.Equal(0, settlement.CitiesDemanded);
		Assert.Empty(world.Reparations);
		Assert.Empty(world.PendingCityDemands);
		Assert.Equal(42f, k1.Legitimacy, 1);
		Assert.Equal(55f, k2.Legitimacy, 1);
		var evt = Assert.Single(world.Bus.OfType(PolEventType.PeaceSettlement));
		Assert.Equal("RP_WARTERMS_HUMILIATION", evt.Get("terms"));
	}

	[Fact]
	public void Settlement_MarginalVictory_IsWhitePeace()
	{
		var world = TwoKingdomWorld(out KingdomPolitics k1, out KingdomPolitics k2);
		// Слабый победитель при сильном проигравшем: счёт ниже порога условий.
		// Победа защитника (без завоевательного повода): 20 + 10 − 12 = 18.
		k1.Legitimacy = 80f;
		k2.Legitimacy = 20f;
		world.ClearEvents();

		PeaceSettlement settlement = world.ResolveWarEnd(1, 2, winnerId: 2, warYears: 1)!;

		Assert.Equal(SettlementLevel.WhitePeace, settlement.Level);
		Assert.Empty(world.Reparations);
		Assert.Empty(world.PendingCityDemands);
		var evt = Assert.Single(world.Bus.OfType(PolEventType.WhitePeace));
		Assert.Equal(Severity.Minor, evt.Severity);
		Assert.Empty(world.Bus.OfType(PolEventType.PeaceSettlement));
	}

	[Fact]
	public void Settlement_NoWinner_WhitePeaceAndTruce()
	{
		var world = TwoKingdomWorld(out _, out _);
		int year = world.Year;
		world.ClearEvents();

		PeaceSettlement settlement = world.ResolveWarEnd(1, 2, winnerId: 0, warYears: 5)!;

		Assert.NotNull(settlement);
		Assert.Equal(SettlementLevel.WhitePeace, settlement.Level);
		Assert.True(world.TruceActiveBetween(1, 2));
		Assert.Equal(year + world.Params.TruceYears, settlement.TruceUntilYear);
		var evt = Assert.Single(world.Bus.OfType(PolEventType.WhitePeace));
		Assert.Equal("5", evt.Get("years"));
	}

	[Theory]
	[InlineData(4, 3, 2, 0, -1)] // 3 из 4 нападающих уничтожено, у защитников 0 -> победа защитников
	[InlineData(4, 1, 2, 0, -1)] // 1 из 4 нападающих уничтожена, у защитников 0 -> победа защитников
	[InlineData(1, 0, 3, 1, 1)]  // 1 из 3 защитников уничтожена, у нападающих 0 -> победа нападающих
	[InlineData(2, 0, 2, 0, 0)]  // 0 уничтожено с обеих сторон при rawWinner=0 -> белый мир
	public void CoalitionWar_DestroyedCountriesOverrideWhitePeace(
		int totalAttackers, int destroyedAttackers, int totalDefenders, int destroyedDefenders, int expectedSign)
	{
		var world = TwoKingdomWorld(out _, out _);
		world.ClearEvents();

		var coalition = new WarCoalitionOutcome
		{
			TotalAttackers = totalAttackers,
			DestroyedAttackers = destroyedAttackers,
			DestroyedAttackerNames = destroyedAttackers > 0 ? new List<string> { "ПалАгрессор" } : new List<string>(),
			TotalDefenders = totalDefenders,
			DestroyedDefenders = destroyedDefenders,
			DestroyedDefenderNames = destroyedDefenders > 0 ? new List<string> { "Польша" } : new List<string>()
		};

		Assert.Equal(expectedSign, coalition.ResolveWinnerSide(0));

		PeaceSettlement settlement = world.ResolveWarEnd(1, 2, winnerId: 0, warYears: 1, defeatedCitiesLeft: -1, coalition: coalition)!;
		Assert.NotNull(settlement);

		if (expectedSign == 0)
		{
			Assert.Equal(SettlementLevel.WhitePeace, settlement.Level);
			Assert.Single(world.Bus.OfType(PolEventType.WhitePeace));
		}
		else
		{
			Assert.NotEqual(SettlementLevel.WhitePeace, settlement.Level);
			Assert.Empty(world.Bus.OfType(PolEventType.WhitePeace));
			int expectedVictor = expectedSign > 0 ? 1 : 2;
			Assert.Equal(expectedVictor, settlement.VictorId);
			var evt = Assert.Single(world.Bus.OfType(PolEventType.PeaceSettlement));
			Assert.Contains("уничтожено стран:", evt.Get("casualties"));
		}
	}

	[Fact]
	public void Settlement_DisabledOrDead_ReturnsNull()
	{
		var world = TwoKingdomWorld(out _, out _);
		world.Params.EnableWarPeace = false;
		Assert.Null(world.ResolveWarEnd(1, 2, 1, 8));
		Assert.False(world.TruceActiveBetween(1, 2));

		world.Params.EnableWarPeace = true;
		Assert.Null(world.ResolveWarEnd(1, 999, 1, 8));
		Assert.Null(world.ResolveWarEnd(1, 1, 1, 8));
	}

	// ================================================================== перемирие

	[Fact]
	public void Truce_ExpiresAfterConfiguredYears()
	{
		var world = TwoKingdomWorld(out _, out _);
		world.ResolveWarEnd(1, 2, winnerId: 1, warYears: 1);
		Assert.True(world.TruceActiveBetween(1, 2));

		// Отматываем TruceYears мирных лет: перемирие должно истечь.
		TickWar(world, world.Params.TruceYears + 1, atWar: false);

		Assert.False(world.TruceActiveBetween(1, 2));
		var evt = Assert.Single(world.Bus.OfType(PolEventType.TruceExpired));
		Assert.Equal(Severity.Trivial, evt.Severity);
	}

	[Fact]
	public void Truce_BrokenWarDeclaration_IsCostly()
	{
		var world = TwoKingdomWorld(out KingdomPolitics k1, out _);
		world.ResolveWarEnd(1, 2, winnerId: 1, warYears: 4);
		Assert.True(world.TruceActiveBetween(1, 2));
		k1.Legitimacy = 70f;
		k1.Support = 60f;
		float threatBefore = k1.Mechanics.ThreatLevel;
		world.ClearEvents();

		world.ApplyDeclareWar(1, 2, CasusBelliType.Conquest);

		Assert.False(world.TruceActiveBetween(1, 2), "перемирие сгорает при нарушении");
		var evt = Assert.Single(world.Bus.OfType(PolEventType.TruceBroken));
		Assert.Equal(Severity.Major, evt.Severity);
		Assert.True(k1.Legitimacy <= 70f - 15f - 12f + 0.5f, $"вероломство дорого стоит: {k1.Legitimacy}");
		Assert.True(k1.Support <= 40.5f);
		Assert.True(k1.Mechanics.ThreatLevel > threatBefore);
		Assert.True(world.GetGrievance(2, 1) >= 40f, "жертва вероломства копит обиду");
	}

	[Fact]
	public void Truce_AndReparations_PrunedWithDeadKingdom()
	{
		var world = TwoKingdomWorld(out _, out _);
		world.ResolveWarEnd(1, 2, winnerId: 1, warYears: 8);
		Assert.True(world.TruceActiveBetween(1, 2));
		Assert.NotEmpty(world.Reparations);

		world.RemoveKingdom(2);

		Assert.False(world.TruceActiveBetween(1, 2));
		Assert.Empty(world.Reparations);
		Assert.Empty(world.PendingCityDemands);
	}

	// ================================================================== репарации

	[Fact]
	public void Reparations_PaidEveryYear_ThenStop()
	{
		var world = TwoKingdomWorld(out _, out _);
		world.ResolveWarEnd(1, 2, winnerId: 1, warYears: 8);

		int years = world.Params.ReparationsYears;
		for (int i = 0; i < years; i++)
		{
			var orders = world.CollectReparations();
			var order = Assert.Single(orders);
			Assert.Equal(2, order.VassalId);
			Assert.Equal(1, order.SuzerainId);
			Assert.True(order.Amount > 0f);
		}
		Assert.Empty(world.CollectReparations());
		Assert.Empty(world.Reparations);
	}

	[Fact]
	public void Reparations_Disabled_NotCollected()
	{
		var world = TwoKingdomWorld(out _, out _);
		world.ResolveWarEnd(1, 2, winnerId: 1, warYears: 8);
		Assert.NotEmpty(world.Reparations);
		world.Params.EnableWarPeace = false;
		Assert.Empty(world.CollectReparations());
	}

	// ================================================================== сохранение (v11)

	[Fact]
	public void SaveLoad_KeepsFatigueTrucesAndReparations()
	{
		var world = TwoKingdomWorld(out KingdomPolitics k1, out KingdomPolitics k2);
		world.ResolveWarEnd(1, 2, winnerId: 1, warYears: 8);
		k1.WarFatigue = 33f;
		k2.WarFatigue = 55f;

		string json = SaveData.Write(world);
		var restored = new PoliticalWorld();
		SaveData.Apply(restored, json);

		Assert.Equal(SaveData.FormatVersion, 18);
		Assert.Equal(33f, restored.GetKingdom(1)!.WarFatigue, 2);
		Assert.Equal(55f, restored.GetKingdom(2)!.WarFatigue, 2);
		restored.SetYear(world.Year);
		Assert.True(restored.TruceActiveBetween(1, 2));
		Assert.Equal(world.TruceUntilBetween(1, 2), restored.TruceUntilBetween(1, 2));
		var rep = Assert.Single(restored.Reparations);
		Assert.Equal(2, rep.PayerId);
		Assert.Equal(1, rep.PayeeId);
		Assert.True(rep.AmountPerYear > 0f);
		Assert.True(rep.YearsLeft > 0);
		Assert.True(rep.YearsLeft < world.Params.ReparationsYears || rep.YearsLeft == world.Params.ReparationsYears);
	}

	[Fact]
	public void SaveLoad_OldWorldWithoutSections_StartsClean()
	{
		var world = TwoKingdomWorld(out _, out _);
		string json = SaveData.Write(world);
		// Старый формат: без секций перемирий, репараций и поля усталости.
		json = json.Replace("\"truces\":[", "\"oldtruces\":[");
		var restored = new PoliticalWorld();
		SaveData.Apply(restored, json);

		Assert.Equal(0f, restored.GetKingdom(1)!.WarFatigue);
		Assert.False(restored.TruceActiveBetween(1, 2));
		Assert.Empty(restored.Reparations);
	}

	// ================================================================== планировщик

	[Fact]
	public void Planner_ExhaustedByWar_ChoosesPeace()
	{
		var plan = KingdomStrategicPlanner.Choose(new KingdomStrategySnapshot
		{
			KingdomId = 1,
			Year = 20,
			Ideology = IdeologyType.Monarchy,
			AtWar = true,
			WarFatigue = 85f,
			Legitimacy = 55f,
			Food = 400f,
			Population = 100,
			Gold = 60f,
			Stone = 60f,
			Warriors = 60,
			MaxWarriors = 100,
			LastActionYear = int.MinValue
		});
		Assert.Equal(KingdomActionType.SeekPeace, plan.Action);
	}

	[Fact]
	public void Planner_FreshWar_KeepsFighting()
	{
		var plan = KingdomStrategicPlanner.Choose(new KingdomStrategySnapshot
		{
			KingdomId = 1,
			Year = 20,
			Ideology = IdeologyType.Militarism,
			AtWar = true,
			WarFatigue = 20f,
			Legitimacy = 60f,
			Food = 400f,
			Population = 100,
			Gold = 60f,
			Stone = 60f,
			Warriors = 60,
			MaxWarriors = 100,
			LastActionYear = int.MinValue
		});
		Assert.NotEqual(KingdomActionType.SeekPeace, plan.Action);
	}

	// ================================================================== хелперы

	private static PoliticalWorld TwoKingdomWorld(out KingdomPolitics k1, out KingdomPolitics k2)
	{
		var world = new PoliticalWorld();
		world.Rng = new Random(42);
		world.SetBaseline(true);
		world.SetYear(5);
		world.AddKingdom(1, IdeologyType.Monarchy, 0);
		world.AddKingdom(2, IdeologyType.Monarchy, 0);
		world.AddCity(10, 1);
		world.AddCity(20, 2);
		world.YearTick(new List<KingdomInput>
		{
			Snapshot(1, RelationType.Peace),
			Snapshot(2, RelationType.Peace)
		});
		k1 = world.GetKingdom(1)!;
		k2 = world.GetKingdom(2)!;
		return world;
	}

	private static void TickWar(PoliticalWorld world, int years, bool atWar)
	{
		for (int i = 0; i < years; i++)
		{
			RelationType relation = atWar ? RelationType.War : RelationType.Peace;
			var inputs = new List<KingdomInput>
			{
				Snapshot(1, relation),
				Snapshot(2, relation)
			};
			var a = inputs[0];
			a.AtWar = atWar;
			var b = inputs[1];
			b.AtWar = atWar;
			inputs[0] = a;
			inputs[1] = b;
			world.YearTick(inputs);
		}
	}

	private static KingdomInput Snapshot(int kingdomId, RelationType relation)
	{
		int partner = kingdomId == 1 ? 2 : 1;
		return new KingdomInput
		{
			KingdomId = kingdomId,
			Name = "K" + kingdomId,
			Population = 100,
			Warriors = 5,
			Ruler = new RulerRef
			{
				ActorId = kingdomId * 100 + 1,
				Name = "Ruler" + kingdomId,
				KingdomId = kingdomId,
				SinceYear = 1,
				Age = 40
			},
			Relations = new List<RelationInfo>
			{
				new RelationInfo { OtherId = partner, Relation = relation, Adjacent = true }
			},
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
}
