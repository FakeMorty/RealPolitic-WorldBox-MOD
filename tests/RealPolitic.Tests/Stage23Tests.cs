using System;
using System.Collections.Generic;
using RealPolitic.Core;
using Xunit;

namespace RealPolitic.Tests;

/// <summary>Этап 23: честная хроника (убийца, распады), регентство по-настоящему,
/// фамилии династий, границы, тайные авиаудары, SaveData v12.</summary>
public class Stage23Tests
{
	// ================================================================== Убийца короля

	[Fact]
	public void KillerDetails_SavedAndLoaded()
	{
		var world = new PoliticalWorld { Rng = new Random(1) };
		world.SetBaseline(true);
		world.SetYear(5);
		DeathRecord rec = world.Deaths.Record(77, "Король Джейме", DeathCause.KilledByUnit, 5,
			99, "Эддард", null, null, 1, "Столичный", wasRuler: true, wasChosen: false,
			age: 41, note: "", wasFemale: false, reignYears: 16);
		rec.KillerIsCiv = true;
		rec.KillerKingdom = "Винтерфелл";
		rec.KillerCity = "Бор";
		rec.KillerJob = "воин";
		rec.KillerRace = "человек";
		rec.KillerFemale = true;
		rec.KillerAge = 34;

		string json = SaveData.Write(world);
		var restored = new PoliticalWorld();
		SaveData.Apply(restored, json);

		DeathRecord back = Assert.Single(restored.Deaths.All);
		Assert.Equal("Эддард", back.KillerName);
		Assert.Equal("Винтерфелл", back.KillerKingdom);
		Assert.Equal("человек", back.KillerRace);
		Assert.True(back.KillerFemale);
		Assert.Equal(34, back.KillerAge);
	}

	// ================================================================== фамилия династии

	[Theory]
	[InlineData("Эйгон Таргариен", "Старки", "Старки")]
	[InlineData("Визерис Таргариен III", "", "III")]
	[InlineData("Джотам", "", "Джотам")]
	[InlineData("", "Баратеоны", "Баратеоны")]
	public void DynastySurname_Rules(string founder, string clan, string expected)
	{
		// 0.23.2: фамилия династии = имя клана игры, а не личное имя основателя.
		Assert.Equal(expected, DynastyNaming.DynastySurname(founder, clan));
	}

	[Fact]
	public void WithSurname_AppendsOnce()
	{
		Assert.Equal("Эйгон Таргариен", DynastyNaming.WithSurname("Эйгон", "Таргариен"));
		Assert.Equal("Эйгон Таргариен", DynastyNaming.WithSurname("Эйгон Таргариен", "Таргариен"));
		Assert.Equal("Эйгон", DynastyNaming.WithSurname("Эйгон", ""));
	}

	[Fact]
	public void EnsureDynastySurname_SurnameIsClanName_NoFabricatedRenameEvent()
	{
		// 0.23.2: фамилия династии = имя клана игры, клан физически НЕ переименовывается.
		// Поэтому церемонии «переименование клана в честь основателя» больше нет —
		// событие DynastyRenamed из согласованного состояния не публикуется.
		var world = new PoliticalWorld { Rng = new Random(5) };
		world.SetBaseline(true);
		world.SetYear(7);
		KingdomPolitics k = world.AddKingdom(1, IdeologyType.Monarchy, 0);
		k.RoyalClanName = "Старки";
		k.DynastyFounder = "Эйгон Таргариен";

		world.EnsureDynastySurname(k);

		Assert.Equal("Старки", k.DynastySurname);
		Assert.Empty(world.Bus.OfType(PolEventType.DynastyRenamed));

		// Без клана (старый сейв) — фамилия по имени основателя; события всё равно нет.
		k.DynastySurname = string.Empty;
		k.RoyalClanName = string.Empty;
		world.EnsureDynastySurname(k);
		Assert.Equal("Таргариен", k.DynastySurname);
		Assert.Empty(world.Bus.OfType(PolEventType.DynastyRenamed));
	}

	[Fact]
	public void EnsureDynastySurname_DisabledOrNoFounder_Skips()
	{
		var world = new PoliticalWorld { Rng = new Random(5) };
		world.SetBaseline(true);
		world.SetYear(7);
		KingdomPolitics k = world.AddKingdom(1, IdeologyType.Monarchy, 0);
		k.RoyalClanName = "Джотамичи";
		world.EnsureDynastySurname(k);
		Assert.Empty(k.DynastySurname);

		k.DynastyFounder = "Беор";
		world.Params.EnableDynastySurnames = false;
		world.EnsureDynastySurname(k);
		Assert.Empty(k.DynastySurname);
	}

	// ================================================================== регентство

	[Theory]
	[InlineData(IdeologyType.Monarchy, SuccessionLaw.Salic, true)]
	[InlineData(IdeologyType.Monarchy, SuccessionLaw.LeaderCult, true)]
	[InlineData(IdeologyType.Tribalism, SuccessionLaw.Tanistry, true)]
	[InlineData(IdeologyType.Democracy, SuccessionLaw.DemocraticElection, false)]
	[InlineData(IdeologyType.MerchantRepublic, SuccessionLaw.DemocraticElection, false)]
	// Военная держава: ребёнок-наследник правителя тоже не должен править — за него регент.
	[InlineData(IdeologyType.Militarism, SuccessionLaw.Stratocracy, true)]
	public void RegencyGame_Qualifies(IdeologyType ideology, SuccessionLaw law, bool expected)
	{
		Assert.Equal(expected, RegencyGame.Qualifies(ideology, law));
	}

	[Fact]
	public void RegencyEvents_NamesBothSides()
	{
		var world = TwoKingdomWorld(out KingdomPolitics k1, out _);
		RegencyGame.PublishCrowned(world, k1, new RulerRef { Name = "Эйгон", Age = 6 }, "Деймон");
		var crowned = Assert.Single(world.Bus.OfType(PolEventType.RegencyCrowned));
		Assert.Equal("Эйгон", crowned.Get("heir"));
		Assert.Equal("Деймон", crowned.Get("regent"));
		Assert.Equal("6", crowned.Get("age"));

		RegencyGame.PublishPassed(world, k1, "Эйгон", "Деймон");
		var passed = Assert.Single(world.Bus.OfType(PolEventType.RegencyPassed));
		Assert.Equal("18", passed.Get("age"));
		Assert.False(k1.Regency!.IsActive);
	}

	// ================================================================== границы

	[Fact]
	public void Borders_ClosedByDefault_ExplicitClosePublishesOnce()
	{
		var world = TwoKingdomWorld(out _, out _);

		// 0.23.2: пара изначально закрыта по умолчанию — без события и без записи.
		Assert.True(world.BordersBook.ClosedBetween(1, 2));
		Assert.Empty(world.Bus.OfType(PolEventType.BorderClosed));

		// Правитель открыл, затем явно закрыл: событие — по одному разу на смену.
		Assert.True(world.ApplyBorderPolicy(1, 2, BorderStatus.Open));
		Assert.Single(world.Bus.OfType(PolEventType.BorderOpened));
		Assert.True(world.ApplyBorderPolicy(1, 2, BorderStatus.Closed));
		Assert.Single(world.Bus.OfType(PolEventType.BorderClosed));
		// Повторный приказ закрыть — не событие.
		Assert.False(world.ApplyBorderPolicy(1, 2, BorderStatus.Closed));
		Assert.Single(world.Bus.OfType(PolEventType.BorderClosed));
	}

	[Fact]
	public void Borders_OpenRequiresNegotiation_ThenEventOnce()
	{
		var world = TwoKingdomWorld(out _, out _);
		// 0.23.2: границы закрыты по умолчанию; открыть их можно только долгими
		// переговорами при наличии торгового пути (или союза/унии).
		world.Trade.Open(1, 2, 1);
		var p = world.Params;
		for (int year = 0; year < p.BorderOpeningTalksYears - 1; year++)
		{
			Assert.False(world.TryNegotiateBorderOpening(1, 2, alliedOrUnion: false, atWar: false, openTradeRoute: true),
				$"год {year}: переговоры ещё не набрали нужный стаж");
		}
		Assert.True(world.TryNegotiateBorderOpening(1, 2, alliedOrUnion: false, atWar: false, openTradeRoute: true));
		Assert.Equal(BorderStatus.Open, world.BordersBook.StatusBetween(1, 2));
		Assert.Single(world.Bus.OfType(PolEventType.BorderOpened));
	}

	[Fact]
	public void Borders_IntrusionOnExplicitClose_LocksReopening()
	{
		var world = TwoKingdomWorld(out _, out _);
		// Явно закрытая (после открытой) граница — даёт 50-летний запрет после облавы.
		world.ApplyBorderPolicy(1, 2, BorderStatus.Open);
		world.ApplyBorderPolicy(1, 2, BorderStatus.Closed);
		world.ReportBorderIntrusion(1, 2, "Отор Трубой");

		Assert.Single(world.Bus.OfType(PolEventType.BorderExpulsion));
		Assert.True(world.BordersBook.LockedUntilBetween(1, 2) >= world.Year + world.Params.BorderLockYears);
		// Облава: открыть границу раньше срока нельзя.
		Assert.False(world.ApplyBorderPolicy(1, 2, BorderStatus.Open));
		Assert.True(world.BordersBook.ClosedBetween(1, 2));
	}

	[Fact]
	public void Borders_Disabled_NoPolicy()
	{
		var world = TwoKingdomWorld(out _, out _);
		world.Params.EnableBorders = false;
		Assert.False(world.ApplyBorderPolicy(1, 2, BorderStatus.Closed));
		Assert.False(world.ApplyBorderPolicy(1, 2, BorderStatus.Open));
	}

	[Fact]
	public void Borders_RemovedWithDeadKingdom()
	{
		var world = TwoKingdomWorld(out _, out _);
		world.ApplyBorderPolicy(1, 2, BorderStatus.Open);
		Assert.False(world.BordersBook.ClosedBetween(1, 2));
		world.RemoveKingdom(2);
		// С уходом державы запись о границе вычищается из книги.
		Assert.Empty(world.BordersBook.All);
		Assert.False(world.ApplyBorderPolicy(1, 2, BorderStatus.Open));
	}

	// ================================================================== распад деревни

	[Fact]
	public void Collapse_TriggersAtThreeFragments()
	{
		var world = new PoliticalWorld { Rng = new Random(9) };
		world.SetBaseline(true);
		world.SetYear(10);
		world.AddKingdom(1, IdeologyType.Monarchy, 0);
		for (int id = 11; id <= 13; id++)
		{
			world.AddKingdom(id, IdeologyType.Monarchy, 0);
			world.AddCity(id * 10, id);
		}

		world.RecordSecessionFragment(1, 11);
		world.RecordSecessionFragment(1, 12);
		Assert.Empty(world.Bus.OfType(PolEventType.KingdomCollapsed));

		world.RecordSecessionFragment(1, 13);
		var evt = Assert.Single(world.Bus.OfType(PolEventType.KingdomCollapsed));
		Assert.Equal("3", evt.Get("count"));
		Assert.Contains("гор.", evt.Get("fragments") ?? "");
	}

	[Fact]
	public void Collapse_NotRepublishedWithinWindow()
	{
		var world = new PoliticalWorld { Rng = new Random(9) };
		world.SetBaseline(true);
		world.SetYear(10);
		world.AddKingdom(1, IdeologyType.Monarchy, 0);
		for (int id = 11; id <= 14; id++)
		{
			world.AddKingdom(id, IdeologyType.Monarchy, 0);
			world.AddCity(id * 10, id);
		}
		world.RecordSecessionFragment(1, 11);
		world.RecordSecessionFragment(1, 12);
		world.RecordSecessionFragment(1, 13);
		world.SetYear(11);
		world.RecordSecessionFragment(1, 14); // в той же 3-летке — без новой записи
		Assert.Single(world.Bus.OfType(PolEventType.KingdomCollapsed));
	}

	// ================================================================== авиаудары

	[Fact]
	public void PowerRaid_GatesByCapabilityAndCooldown()
	{
		var world = TwoKingdomWorld(out KingdomPolitics k1, out KingdomPolitics k2);
		world.Params.EnablePowerRaids = true;
		// Слабая держава — не летает.
		k1.Legitimacy = 10f;
		k2.Legitimacy = 10f;
		Assert.False(PowerRaids.CanRaid(k1, world.Params, world.Year, 80.0));
		// Разорённого врага не бомбят.
		k1.Legitimacy = 100f;
		if (k1.Economy != null) k1.Economy.Prosperity = 90f;
		Assert.False(PowerRaids.CanRaid(k1, world.Params, world.Year, 20.0));
		Assert.True(PowerRaids.CanRaid(k1, world.Params, world.Year, 80.0));
		// Cooldown: свежий налёт — пауза шесть лет.
		k1.LastPowerRaidYear = world.Year;
		Assert.False(PowerRaids.CanRaid(k1, world.Params, world.Year + 3, 80.0));
		Assert.True(PowerRaids.CanRaid(k1, world.Params, world.Year + PowerRaids.CooldownYears, 80.0));
	}

	[Fact]
	public void PowerRaid_OutcomeHurtsAndPublishes()
	{
		var world = TwoKingdomWorld(out KingdomPolitics k1, out KingdomPolitics k2);
		if (k2.Economy != null) k2.Economy.Prosperity = 80f;
		k2.Legitimacy = 60f;
		world.ApplyPowerRaidOutcome(1, 2, "Каменнобор");
		Assert.True(k2.Economy!.Prosperity < 80f);
		Assert.True(k2.Legitimacy < 60f);
		var evt = Assert.Single(world.Bus.OfType(PolEventType.PowerRaid));
		Assert.Equal("Каменнобор", evt.Get("town"));
		Assert.Equal(world.KingdomNameOf(2), evt.Get("target"));
		Assert.Contains("K2", evt.Get("target"));
	}

	[Fact]
	public void PowerRaid_Disabled_Never()
	{
		var world = TwoKingdomWorld(out KingdomPolitics k1, out _);
		k1.Legitimacy = 100f;
		if (k1.Economy != null) k1.Economy.Prosperity = 90f;
		world.Params.EnablePowerRaids = false;
		for (int y = 0; y < 60; y++)
		{
			world.SetYear(y);
			Assert.False(world.TryPowerRaid(1, 2));
		}
	}

	// ================================================================== сохранение v12

	[Fact]
	public void SaveLoad_KeepsBordersSurnameAndHeir()
	{
		var world = TwoKingdomWorld(out KingdomPolitics k1, out _);
		k1.DynastyFounder = "Эйгон Таргариен";
		world.EnsureDynastySurname(k1);
		k1.ClanRenamedToSurname = true;
		k1.LastPowerRaidYear = 4;
		world.ApplyBorderPolicy(1, 2, BorderStatus.Closed);
		k1.Regency = new RegencyState();
		k1.Regency.Start(world.Year, "Деймон", 55);
		k1.Regency.HeirActorId = 77;
		k1.Regency.HeirName = "Эйгон";
		k1.Regency.CrownSitsWithRegent = true;

		string json = SaveData.Write(world);
		var restored = new PoliticalWorld();
		SaveData.Apply(restored, json);

		Assert.Equal(18, SaveData.FormatVersion);
		KingdomPolitics back = restored.GetKingdom(1)!;
		// 0.23.2: фамилия династии = имя клана (клан «R1ичи»).
		Assert.Equal("R1ичи", back.DynastySurname);
		Assert.True(back.ClanRenamedToSurname);
		Assert.Equal(4, back.LastPowerRaidYear);
		Assert.True(restored.BordersBook.ClosedBetween(1, 2));
		Assert.Equal(77, back.Regency!.HeirActorId);
		Assert.Equal("Эйгон", back.Regency.HeirName);
		Assert.True(back.Regency.CrownSitsWithRegent);
	}

	[Fact]
	public void SaveLoad_OldSave_DefaultBordersClosedAndNoSurname()
	{
		var world = TwoKingdomWorld(out _, out _);
		string json = SaveData.Write(world);
		// Портят узлы, которых не было в старых сохранениях.
		json = json.Replace("\"borders\":[", "\"oldborders\":[");
		json = json.Replace("\"dsn\":\"", "\"olddsn\":\"");
		var restored = new PoliticalWorld();
		SaveData.Apply(restored, json);

		// 0.23.2: без записи в книге пара закрыта ПО УМОЛЧАНИЮ (а не «открыта»).
		Assert.True(restored.BordersBook.ClosedBetween(1, 2));
		Assert.Empty(restored.GetKingdom(1)!.DynastySurname);
		Assert.Equal(-100, restored.GetKingdom(1)!.LastPowerRaidYear);
	}

	// ================================================================== хелперы

	private static PoliticalWorld TwoKingdomWorld(out KingdomPolitics k1, out KingdomPolitics k2)
	{
		var world = new PoliticalWorld { Rng = new Random(42) };
		world.SetBaseline(true);
		world.SetYear(5);
		world.AddKingdom(1, IdeologyType.Monarchy, 0);
		world.AddKingdom(2, IdeologyType.Monarchy, 0);
		world.AddCity(10, 1);
		world.AddCity(20, 2);
		world.YearTick(new List<KingdomInput>
		{
			Snapshot(1), Snapshot(2)
		});
		k1 = world.GetKingdom(1)!;
		k2 = world.GetKingdom(2)!;
		return world;
	}

	private static KingdomInput Snapshot(int id) => new KingdomInput
	{
		KingdomId = id,
		Name = "K" + id,
		Population = 100,
		Warriors = 5,
		Ruler = new RulerRef
		{
			ActorId = id * 100 + 1,
			Name = "R" + id,
			KingdomId = id,
			SinceYear = 1,
			Age = 40
		},
		Relations = new List<RelationInfo>
		{
			new RelationInfo { OtherId = id == 1 ? 2 : 1, Relation = RelationType.Peace, Adjacent = true }
		},
		Cities = new List<CityInput>
		{
			new CityInput
			{
				CityId = id * 10,
				KingdomId = id,
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
