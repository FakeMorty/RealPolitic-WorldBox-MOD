using System;
using System.Collections.Generic;
using System.Linq;
using RealPolitic.Core;
using Xunit;

namespace RealPolitic.Tests;

/// <summary>
/// Гражданские войны (0.23 — «настоящая война»): мятежные города образуют государство
/// повстанцев, которое воюет с метрополией армиями WorldBox; ядро читает исход из мира
/// (DecideResolution), применяет его (ApplyCivilWarOutcome) и отдаёт игровому слою операции
/// через TakePendingSecessions / TakePendingWorldOps.
/// </summary>
public class CivilWarTests
{
	// ------------------------------------------------------------------ запуск мятежа

	[Fact]
	public void CivilWar_RequiresAtLeastTwoCities_AndNoRunningRevolution()
	{
		var state = WeakState(1);
		var p = new SimParams();
		var oneCity = Input(1, "Одиночество", 1);

		Assert.False(CivilWars.CanStart(state, oneCity, p, 50));
		Assert.True(CivilWars.CanStart(state, Input(1, "Два города", 2), p, 50));

		// Идущая революция блокирует второй внутренний конфликт:
		state.Revolution = new RevolutionState { Target = IdeologyType.Communism, StartedYear = 49, YearsLeft = 2 };
		Assert.False(CivilWars.CanStart(state, Input(1, "Два города", 2), p, 50));
	}

	[Fact]
	public void CivilWar_DisabledBySecessions_AndByCooldown()
	{
		var p = new SimParams();
		var pNoSecession = new SimParams { AllowSecession = false };
		var state = WeakState(1);

		// Отделения выключены — мятежу не из чего родиться: механика отключается целиком.
		Assert.False(CivilWars.CanStart(state, Input(1, "K", 4), pNoSecession, 50));

		// Передышка после предыдущей гражданской войны.
		state.LastCivilWarYear = 48;
		Assert.False(CivilWars.CanStart(state, Input(1, "K", 4), p, 50));
		Assert.True(CivilWars.CanStart(state, Input(1, "K", 4), p, 48 + p.CivilWarCooldownYears));
	}

	[Fact]
	public void CivilWar_ChanceGrowsWithDissentAndFallsWithLegitimacy()
	{
		var p = new SimParams();
		var healthy = new KingdomPolitics { KingdomId = 1, Ideology = IdeologyType.Monarchy, Legitimacy = 85f, AvgDissent = 0.02f };
		var sick = WeakState(1);

		float healthyChance = CivilWars.StartChance(healthy, Input(1, "A", 3), p);
		float sickChance = CivilWars.StartChance(sick, Input(1, "B", 3), p);

		Assert.True(healthyChance < 0.05f, $"у здоровой державы шанс должен быть мал, получено {healthyChance}");
		Assert.True(sickChance > healthyChance * 3f, $"у больной державы шанс должен быть заметно выше: {sickChance} vs {healthyChance}");
		Assert.True(sickChance <= p.CivilWarChanceCap);
	}

	[Fact]
	public void CivilWar_Starts_PublishesEvent_AndTakesNonCapitalCities()
	{
		var state = WeakState(1);
		var input = Input(1, "Галемар", 4);
		var bus = new PolEventBus();
		float legitimacyBefore = state.Legitimacy;

		bool started = CivilWars.TryStart(state, input, new SimParams(), bus, 50, new FixedRandom(0.0));

		Assert.True(started);
		Assert.NotNull(state.CivilWar);
		var cw = state.CivilWar!;
		Assert.Equal(50, cw.StartedYear);
		Assert.NotEmpty(cw.RebelCityIds);
		// Столица (первый город) в мятеже не участвует:
		Assert.DoesNotContain(input.Cities![0].CityId, cw.RebelCityIds);
		Assert.True(cw.RebelCityIds.Count < input.Cities.Count);
		// В 0.23 государство повстанцев создаёт следующий шаг мира (StartRebelKingdom):
		Assert.Equal(0, cw.RebelKingdomId);
		Assert.True(state.Legitimacy < legitimacyBefore);
		Assert.True(cw.RebelCapitalCityId != 0);

		var evt = Assert.Single(bus.OfType(PolEventType.CivilWarStarted));
		Assert.Equal(Severity.Critical, evt.Severity);
		Assert.Equal("Галемар", evt.Get("kingdom"));
		Assert.Equal(cw.RebelCityIds.Count, evt.GetId("cities"));
		Assert.StartsWith("RP_CW_CAUSE_", evt.Get("cause"));
	}

	[Fact]
	public void CivilWar_UsesRealLeaderName_WhenGameProvidesOne()
	{
		var state = WeakState(1);
		var ctx = new CivilWarContext
		{
			RebelLeaderName = (k, c) => "Борис Мятежник",
			RebelLeaderIsFemale = (k, c) => true,
			RebelLeaderActorId = (k, c) => 777
		};

		Assert.True(CivilWars.TryStart(state, Input(1, "K", 3), new SimParams(), new PolEventBus(), 50, new FixedRandom(0.0), ctx));
		Assert.Equal("Борис Мятежник", state.CivilWar!.RebelLeaderName);
		Assert.True(state.CivilWar.RebelLeaderIsFemale);
		Assert.Equal(777, state.CivilWar.RebelLeaderActorId);
	}

	[Fact]
	public void CivilWar_CauseWeights_ReactToRegencyEthnicityAndFamine()
	{
		var p = new SimParams();

		var regency = WeakState(1);
		regency.Regency = new RegencyState();
		regency.Regency.Start(49, "Регентский совет", 0);
		var noRegency = WeakState(1);
		var plainWeights = CivilWars.CauseWeights(noRegency, Input(1, "K", 3), p, 50);
		var regencyWeights = CivilWars.CauseWeights(regency, Input(1, "K", 3), p, 50);
		Assert.True(regencyWeights.ContainsKey(CivilWarCause.RegencyCrisis));
		Assert.False(plainWeights.ContainsKey(CivilWarCause.RegencyCrisis));
		Assert.Equal(3f, regencyWeights[CivilWarCause.RegencyCrisis]);

		var ethno = new KingdomPolitics { KingdomId = 2, Ideology = IdeologyType.Ethnonationalism, TitularRaceId = "humans", Legitimacy = 80f };
		var ethnoInput = Input(2, "Empire", 3);
		ethnoInput.Cities![2] = new CityInput { CityId = 12, KingdomId = 2, Population = 40, RaceId = "orcs", IsForeign = true, Loyalty = 20 };
		var ethnoWeights = CivilWars.CauseWeights(ethno, ethnoInput, p, 50);
		Assert.True(ethnoWeights.ContainsKey(CivilWarCause.EthnicOppression));

		var famine = WeakState(3);
		famine.Legitimacy = 70f;
		famine.AvgDissent = 0.1f;
		famine.Starving = true;
		Assert.True(CivilWars.CauseWeights(famine, Input(3, "K", 3), p, 50).ContainsKey(CivilWarCause.Famine));
	}

	[Fact]
	public void CivilWar_CauseWeights_AreDeterministic()
	{
		var state = WeakState(1);
		var input = Input(1, "K", 4);
		var p = new SimParams();
		var a = CivilWars.CauseWeights(state, input, p, 50);
		var b = CivilWars.CauseWeights(state, input, p, 50);
		Assert.Equal(a.Count, b.Count);
		foreach (var kv in a)
		{
			Assert.Equal(kv.Value, b[kv.Key]);
		}
	}

	[Fact]
	public void CivilWar_Causes_ExposeSecessionAndRegimeChangeTraits()
	{
		// Этнический мятеж склонен к отделению, династический спор — к смене власти.
		Assert.True(CivilWarCauses.SecessionBias(CivilWarCause.EthnicOppression) < CivilWarCauses.SecessionBias(CivilWarCause.SuccessionCrisis));
		Assert.True(CivilWarCauses.AimsAtRegimeChange(CivilWarCause.SuccessionCrisis));
		Assert.False(CivilWarCauses.AimsAtRegimeChange(CivilWarCause.EthnicOppression));
		Assert.NotEqual(CivilWarCauses.LocKey(CivilWarCause.Famine), CivilWarCauses.LocKey(CivilWarCause.Tyranny));
	}

	// ------------------------------------------------------------------ исход по миру (чистая функция)

	[Fact]
	public void CivilWar_Resolution_LoyalistVictory_WhenRebelKingdomHasNoCities()
	{
		var state = WeakState(1);
		var input = Input(1, "K", 5);
		var cw = ActiveWar(state, input, started: 51);
		var ctx = new CivilWarContext { KingdomCityCount = id => id == cw.RebelKingdomId ? 0 : 5 };

		Assert.Equal(CivilWarResolution.LoyalistVictory,
			CivilWars.DecideResolution(state, input, cw, new SimParams(), 52, ctx));
	}

	[Fact]
	public void CivilWar_Resolution_RebelTakeover_WhenRebelsOwnCapital()
	{
		var state = WeakState(1);
		var input = Input(1, "K", 5);
		var cw = ActiveWar(state, input, started: 51);
		var ctx = new CivilWarContext
		{
			KingdomCityCount = id => id == cw.RebelKingdomId ? 4 : 1,
			CityOwner = cityId => cityId == cw.ParentCapitalCityId ? cw.RebelKingdomId : state.KingdomId
		};

		Assert.Equal(CivilWarResolution.RebelTakeover,
			CivilWars.DecideResolution(state, input, cw, new SimParams(), 52, ctx));
	}

	[Fact]
	public void CivilWar_Resolution_Secession_WhenWarEndedInPeace()
	{
		var state = WeakState(1);
		var input = Input(1, "K", 5);
		var cw = ActiveWar(state, input, started: 51);
		// Война кончилась миром, повстанцы живы и держат города — отделение.
		input.Relations!.Add(new RelationInfo { OtherId = cw.RebelKingdomId, Relation = RelationType.Peace, Adjacent = true });
		var ctx = new CivilWarContext
		{
			KingdomCityCount = id => id == cw.RebelKingdomId ? 2 : 3,
			CityOwner = cityId => state.KingdomId
		};

		// Война была настоящей (подтверждена игрой), потом кончилась миром — белый мир = отделение.
		cw.WarEstablished = true;
		Assert.Equal(CivilWarResolution.Secession,
			CivilWars.DecideResolution(state, input, cw, new SimParams(), 53, ctx));
	}

	[Fact]
	public void CivilWar_NoWarInGame_IsNotPeace_NoSecession()
	{
		// Регрессия: игра не подтвердила войну — отсутствие войны не должно становиться отделением.
		var state = WeakState(1);
		var input = Input(1, "K", 5);
		var cw = ActiveWar(state, input, started: 51);
		input.Relations!.Add(new RelationInfo { OtherId = cw.RebelKingdomId, Relation = RelationType.Peace, Adjacent = true });
		var ctx = new CivilWarContext
		{
			KingdomCityCount = id => id == cw.RebelKingdomId ? 2 : 3,
			CityOwner = cityId => state.KingdomId
		};
		Assert.False(cw.WarEstablished);
		Assert.Equal(CivilWarResolution.None,
			CivilWars.DecideResolution(state, input, cw, new SimParams(), 53, ctx));
		Assert.Equal(CivilWarResolution.None,
			CivilWars.DecideResolution(state, input, cw, new SimParams(), 60, ctx));
	}

	[Fact]
	public void CivilWar_WarSeenInGame_IsRecorded()
	{
		var state = WeakState(1);
		var input = Input(1, "K", 5);
		var cw = ActiveWar(state, input, started: 51);
		input.Relations!.Add(new RelationInfo { OtherId = cw.RebelKingdomId, Relation = RelationType.War, Adjacent = true });
		var ctx = new CivilWarContext
		{
			KingdomCityCount = id => id == cw.RebelKingdomId ? 2 : 3,
			CityOwner = cityId => state.KingdomId
		};
		Assert.Equal(CivilWarResolution.None,
			CivilWars.DecideResolution(state, input, cw, new SimParams(), 52, ctx));
		Assert.True(cw.WarEstablished);
	}

	[Fact]
	public void CivilWar_Resolution_FirstYearPeace_DoesNotCountAsSecession()
	{
		var state = WeakState(1);
		var input = Input(1, "K", 5);
		var cw = ActiveWar(state, input, started: 51);
		// Игра ещё могла не успеть объявить войну — мир в ПЕРВЫЙ год войны не считается исходом.
		input.Relations!.Add(new RelationInfo { OtherId = cw.RebelKingdomId, Relation = RelationType.Peace, Adjacent = true });
		var ctx = new CivilWarContext { KingdomCityCount = id => id == cw.RebelKingdomId ? 2 : 3 };

		Assert.Equal(CivilWarResolution.None,
			CivilWars.DecideResolution(state, input, cw, new SimParams(), 51, ctx));
	}

	[Fact]
	public void CivilWar_Resolution_ForcedByTimeLimit()
	{
		var state = WeakState(1);
		var input = Input(1, "K", 5);
		var cw = ActiveWar(state, input, started: 51);
		input.Relations!.Add(new RelationInfo { OtherId = cw.RebelKingdomId, Relation = RelationType.War, Adjacent = true });
		var p = new SimParams { CivilWarDurationYears = 5, CivilWarFrozenExtraYears = 3 };
		var ctx = new CivilWarContext
		{
			KingdomCityCount = id => id == cw.RebelKingdomId ? 2 : 3,
			CityOwner = cityId => state.KingdomId
		};

		// Пока война идёт и лимит не вышел — ничего не решено.
		Assert.Equal(CivilWarResolution.None, CivilWars.DecideResolution(state, input, cw, p, 54, ctx));
		// Вышел предельный срок (9 лет войны > 5+3) — мир навязывается ядром.
		Assert.Equal(CivilWarResolution.Secession, CivilWars.DecideResolution(state, input, cw, p, 59, ctx));
	}

	[Fact]
	public void CivilWar_Resolution_None_WhileWarRages()
	{
		var state = WeakState(1);
		var input = Input(1, "K", 5);
		var cw = ActiveWar(state, input, started: 51);
		input.Relations!.Add(new RelationInfo { OtherId = cw.RebelKingdomId, Relation = RelationType.War, Adjacent = true });
		var ctx = new CivilWarContext
		{
			KingdomCityCount = id => id == cw.RebelKingdomId ? 2 : 3,
			CityOwner = cityId => state.KingdomId
		};

		Assert.Equal(CivilWarResolution.None, CivilWars.DecideResolution(state, input, cw, new SimParams(), 54, ctx));
	}

	// ------------------------------------------------------------------ полный цикл через мир (YearTick)

	[Fact]
	public void World_RebelStarts_QueuesPendingSecession()
	{
		var world = new PoliticalWorld { Rng = new Random(7) };
		world.SetYear(50);
		var parent = world.AddKingdom(1, IdeologyType.Monarchy, 0);
		parent.Legitimacy = 15f;
		parent.AvgDissent = 0.6f;
		parent.Ruler = new RulerRef { ActorId = 500, Name = "Король Артур", KingdomId = 1, SinceYear = 10, Age = 45 };
		for (int cityId = 10; cityId < 15; cityId++)
		{
			world.AddCity(cityId, 1);
		}

		Assert.True(CivilWars.TryStart(parent, Input(1, "Галемар", 5), new SimParams(), world.Bus, 50, new FixedRandom(0.0)));
		// Ровно как делает WorldFeeder: годовой тик с живым вводом.
		world.YearTick(new List<KingdomInput> { Input(1, "Галемар", 5, parent.Ruler) });

		// Повстанцы существуют в ядре как отдельное государство:
		var pending = Assert.Single(world.TakePendingSecessions());
		Assert.True(pending.ProvisionalKingdomId != 0 && pending.ProvisionalKingdomId != 1);
		Assert.Equal(1, pending.ParentKingdomId);
		Assert.Equal(parent.CivilWar!.RebelKingdomId, pending.ProvisionalKingdomId);
		Assert.NotEmpty(pending.CityIds);
		Assert.True(pending.DeclareWar);
		Assert.Empty(world.PendingSecessions);

		// Города повстанцев переехали в новое государство:
		int newId = pending.ProvisionalKingdomId;
		var breakaway = world.GetKingdom(newId);
		Assert.NotNull(breakaway);
		Assert.Equal(parent.CivilWar.RebelIdeology, breakaway!.Ideology);
		Assert.All(pending.CityIds, cityId => Assert.Equal(newId, world.GetCity(cityId)!.KingdomId));
		Assert.Equal(5 - pending.CityIds.Count, world.Cities.Count(c => c.KingdomId == 1));

		// Игра создала настоящее королевство — ядро переезжает на его id:
		int gameId = 4242;
		Assert.True(world.RemapKingdomId(newId, gameId));
		Assert.Null(world.GetKingdom(newId));
		Assert.NotNull(world.GetKingdom(gameId));
		Assert.False(world.RemapKingdomId(gameId, 1), "нельзя переехать на занятый идентификатор");
	}

	[Fact]
	public void World_SecessionOnPeace_PublishesSecessionAndMakePeace()
	{
		var world = new PoliticalWorld { Rng = new Random(7) };
		world.SetYear(50);
		var parent = world.AddKingdom(1, IdeologyType.Monarchy, 10);
		parent.Legitimacy = 20f;
		parent.AvgDissent = 0.6f;
		parent.Ruler = new RulerRef { ActorId = 500, Name = "Король Артур", KingdomId = 1, SinceYear = 10, Age = 45 };
		for (int cityId = 10; cityId < 15; cityId++)
		{
			world.AddCity(cityId, 1);
		}
		// Мятеж вспыхнул в 48-м, государство повстанцев (2) уже создано и держит города 12–13.
		parent.CivilWar = new CivilWarState
		{
			Cause = CivilWarCause.IdeologicalSplit,
			StartedYear = 48,
			RebelIdeology = IdeologyType.Communism,
			RebelLeaderName = "Вождь мятежа",
			RebelCapitalCityId = 12,
			ParentCapitalCityId = 10,
			RebelKingdomId = 2
		};
		parent.CivilWar.AddRebelCity(12);
		parent.CivilWar.AddRebelCity(13);
		// Война в игре была подтверждена ранее (до мира); без этого мир не считается отделением.
		parent.CivilWar.WarEstablished = true;
		world.AddKingdom(2, IdeologyType.Communism, 48);
		world.GetCity(12)!.KingdomId = 2;
		world.GetCity(13)!.KingdomId = 2;

		// Год 50 — третий год войны; война в мире завершилась миром.
		List<KingdomInput> inputs = new List<KingdomInput>
		{
			InputFor(1, "Галемар", new[] { 10, 11, 14 }, parent.Ruler, rel: new RelationInfo { OtherId = 2, Relation = RelationType.Peace, Adjacent = true }),
			InputFor(2, "Повстанцы", new[] { 12, 13 }, null, rel: new RelationInfo { OtherId = 1, Relation = RelationType.Peace, Adjacent = true })
		};
		world.YearTick(inputs);

		Assert.Null(parent.CivilWar);
		Assert.Equal(50, parent.LastCivilWarYear);
		Assert.Equal(1, parent.CivilWarsTotal);
		Assert.Contains(world.Bus.OfType(PolEventType.CivilWarEnded), e => e.Get("resolution") == "RP_CW_RESULT_SECESSION");
		Assert.Contains(world.Bus.OfType(PolEventType.KingdomSeceded), e => e.GetId("new_id") == 2);
		// Мир заключён: игровой слой получает операцию MakePeace.
		Assert.Contains(world.TakePendingWorldOps(), op => op.Type == WorldOpType.MakePeace && op.FromKingdomId == 1 && op.ToKingdomId == 2);
	}

	[Fact]
	public void World_RebelTakeover_DeposesRuler_AndQueuesCityTransfer()
	{
		var world = new PoliticalWorld { Rng = new Random(11) };
		world.SetYear(70);
		var state = world.AddKingdom(3, IdeologyType.Monarchy, 10);
		state.Legitimacy = 12f;
		state.DynastyReignsCount = 5;
		state.DynastyStartYear = 20;
		state.Ruler = new RulerRef { ActorId = 800, Name = "Король Освальд", KingdomId = 3, SinceYear = 30, Age = 52 };
		for (int cityId = 30; cityId < 33; cityId++)
		{
			world.AddCity(cityId, 3);
		}
		state.CivilWar = new CivilWarState
		{
			Cause = CivilWarCause.SuccessionCrisis,
			StartedYear = 66,
			RebelIdeology = IdeologyType.Democracy,
			RebelLeaderName = "Вождь",
			RebelCapitalCityId = 31,
			ParentCapitalCityId = 30,
			RebelKingdomId = 4
		};
		state.CivilWar.AddRebelCity(31);
		state.CivilWar.AddRebelCity(32);
		world.AddKingdom(4, IdeologyType.Democracy, 66);
		// Повстанцы заняли столицу метрополии (решительный переворот).
		world.GetCity(30)!.KingdomId = 4;
		world.GetCity(31)!.KingdomId = 4;
		world.GetCity(32)!.KingdomId = 4;

		List<KingdomInput> inputs = new List<KingdomInput>
		{
			InputFor(3, "Корона", new[] { 30 }, state.Ruler, rel: new RelationInfo { OtherId = 4, Relation = RelationType.War, Adjacent = true }),
			InputFor(4, "Повстанцы", new[] { 31, 32 }, null, rel: new RelationInfo { OtherId = 3, Relation = RelationType.War, Adjacent = true })
		};
		world.YearTick(inputs);

		Assert.Null(state.CivilWar);
		Assert.Null(state.Ruler);
		Assert.Contains(world.Bus.OfType(PolEventType.CivilWarEnded), e => e.Get("resolution") == "RP_CW_RESULT_REBELTAKEOVER");
		// Свергнутый правитель записан в реестр смертей как казнённый:
		Assert.Equal(DeathCause.Executed, world.Deaths.Get(800)!.Cause);
		// Все оставшиеся города переходят повстанцам:
		Assert.Contains(world.TakePendingWorldOps(), op => op.Type == WorldOpType.TransferCities && op.FromKingdomId == 3 && op.ToKingdomId == 4);
	}

	[Fact]
	public void World_SecessionDisabled_NeverStarts_AndFizzlesExisting()
	{
		var world = new PoliticalWorld { Rng = new Random(9) };
		world.SetYear(50);
		var parent = world.AddKingdom(1, IdeologyType.Monarchy, 10);
		parent.Legitimacy = 15f;
		parent.AvgDissent = 0.6f;
		for (int cityId = 10; cityId < 15; cityId++)
		{
			world.AddCity(cityId, 1);
		}

		// CanStart запрещает мятеж с выключенными отделениями.
		world.Params.AllowSecession = false;
		world.YearTick(new List<KingdomInput> { Input(1, "K", 5) });
		Assert.Null(world.GetKingdom(1)!.CivilWar);
		Assert.Empty(world.PendingSecessions);

		// Уже идущий мятеж (из старого сейва) гасится в зародыше на первом же тике.
		parent.CivilWar = new CivilWarState { Cause = CivilWarCause.Tyranny, StartedYear = 49, RebelCapitalCityId = 11 };
		parent.CivilWar.AddRebelCity(11);
		world.YearTick(new List<KingdomInput> { Input(1, "K", 5) });
		Assert.Null(world.GetKingdom(1)!.CivilWar);
		Assert.Empty(world.PendingSecessions);
	}

	[Fact]
	public void World_CivilWarIsDeterministic_ForSameSeed()
	{
		string Run(int seed)
		{
			var world = new PoliticalWorld { Rng = new Random(seed) };
			world.SetYear(1);
			var state = world.AddKingdom(1, IdeologyType.Monarchy, 0);
			state.Legitimacy = 18f;
			state.AvgDissent = 0.7f;
			for (int cityId = 10; cityId < 14; cityId++)
			{
				world.AddCity(cityId, 1);
			}
			var ruler = new RulerRef { ActorId = 1000, Name = "Король", KingdomId = 1, SinceYear = 0, Age = 44 };
			var log = new List<string>();
			for (int year = 1; year <= 40; year++)
			{
				world.SetYear(year);
				world.YearTick(new List<KingdomInput> { Input(1, "Держава", 4, ruler) });
				var cw = world.GetKingdom(1)?.CivilWar;
				log.Add($"{year}:{(cw == null ? "-" : $"{(int)cw.Cause}/{cw.RebelCityIds.Count}")}");
			}
			return string.Join("|", log);
		}

		Assert.Equal(Run(1234), Run(1234));
	}

	// ------------------------------------------------------------------ сохранение

	[Fact]
	public void SaveData_RoundTrip_PreservesCivilWar()
	{
		var source = new PoliticalWorld();
		source.SetYear(90);
		var k = source.AddKingdom(9, IdeologyType.Theocracy, 10);
		k.CivilWarsTotal = 2;
		k.LastCivilWarYear = 70;
		k.CivilWar = new CivilWarState
		{
			Cause = CivilWarCause.EthnicOppression,
			StartedYear = 88,
			RebelIdeology = IdeologyType.Egalitarianism,
			RebelLeaderName = "Вождь восстания",
			RebelLeaderIsFemale = true,
			RebelLeaderActorId = 555,
			RebelCapitalCityId = 92,
			ParentCapitalCityId = 90,
			RebelKingdomId = 12
		};
		k.CivilWar.AddRebelCity(92);
		k.CivilWar.AddRebelCity(93);

		var loaded = new PoliticalWorld();
		SaveData.Apply(loaded, SaveData.Write(source));

		var kLoaded = loaded.GetKingdom(9);
		Assert.NotNull(kLoaded);
		Assert.Equal(2, kLoaded!.CivilWarsTotal);
		Assert.Equal(70, kLoaded.LastCivilWarYear);
		var cw = kLoaded.CivilWar;
		Assert.NotNull(cw);
		Assert.Equal(CivilWarCause.EthnicOppression, cw!.Cause);
		Assert.Equal(88, cw.StartedYear);
		Assert.Equal(IdeologyType.Egalitarianism, cw.RebelIdeology);
		Assert.Equal("Вождь восстания", cw.RebelLeaderName);
		Assert.True(cw.RebelLeaderIsFemale);
		Assert.Equal(555, cw.RebelLeaderActorId);
		Assert.Equal(92, cw.RebelCapitalCityId);
		Assert.Equal(90, cw.ParentCapitalCityId);
		Assert.Equal(12, cw.RebelKingdomId);
		Assert.Equal(new List<int> { 92, 93 }, cw.RebelCityIds);
	}

	[Fact]
	public void SaveData_BackwardCompatible_WithPreCivilWarSaves()
	{
		var source = new PoliticalWorld();
		source.SetYear(40);
		source.AddKingdom(5, IdeologyType.Aristocracy, 5);
		string json = SaveData.Write(source).Replace("\"cwTotal\"", "\"__nope__\"");
		Assert.DoesNotContain("\"cw\"", json);

		var loaded = new PoliticalWorld();
		SaveData.Apply(loaded, json);
		var k = loaded.GetKingdom(5);
		Assert.NotNull(k);
		Assert.Null(k!.CivilWar);
		Assert.Equal(0, k.CivilWarsTotal);
		Assert.Equal(IdeologyType.Aristocracy, k.Ideology);
	}

	// ------------------------------------------------------------------ стратегический ИИ

	[Fact]
	public void Strategy_SuppressRebellion_OutranksRoutineActions()
	{
		var snapshot = new KingdomStrategySnapshot
		{
			KingdomId = 1,
			Year = 100,
			Ideology = IdeologyType.Monarchy,
			Population = 200,
			Warriors = 60,
			MaxWarriors = 100,
			Food = 900f,
			Gold = 300f,
			Stone = 200f,
			Legitimacy = 40f,
			CivilWarActive = true,
			RebelShare = 0.55f,
			LastActionYear = int.MinValue
		};
		var plan = KingdomStrategicPlanner.Choose(snapshot);
		Assert.Equal(KingdomActionType.SuppressRebellion, plan.Action);

		// Без мятежа то же государство занимается обычными делами:
		var calm = KingdomStrategicPlanner.Choose(new KingdomStrategySnapshot
		{
			KingdomId = 1,
			Year = 100,
			Ideology = IdeologyType.Monarchy,
			Population = 200,
			Warriors = 60,
			MaxWarriors = 100,
			Food = 900f,
			Gold = 300f,
			Stone = 200f,
			Legitimacy = 40f,
			LastActionYear = int.MinValue
		});
		Assert.NotEqual(KingdomActionType.SuppressRebellion, calm.Action);

		// Голод важнее подавления: армия без еды мятеж не подавит.
		var starving = KingdomStrategicPlanner.Choose(new KingdomStrategySnapshot
		{
			KingdomId = 1,
			Year = 100,
			Ideology = IdeologyType.Monarchy,
			Population = 200,
			Warriors = 60,
			MaxWarriors = 100,
			Food = 10f,
			Starving = true,
			Legitimacy = 40f,
			CivilWarActive = true,
			RebelShare = 0.55f,
			LastActionYear = int.MinValue
		});
		Assert.Equal(KingdomActionType.EmergencyFood, starving.Action);
	}

	// ------------------------------------------------------------------ помощники

	private static CivilWarState ActiveWar(KingdomPolitics state, KingdomInput input, int started)
	{
		var cw = new CivilWarState
		{
			Cause = CivilWarCause.Tyranny,
			StartedYear = started,
			RebelIdeology = IdeologyType.Monarchy,
			RebelCapitalCityId = input.Cities![1].CityId,
			ParentCapitalCityId = input.Cities![0].CityId,
			RebelKingdomId = 900
		};
		cw.AddRebelCity(input.Cities![1].CityId);
		state.CivilWar = cw;
		return cw;
	}

	private static KingdomPolitics WeakState(int id)
	{
		return new KingdomPolitics
		{
			KingdomId = id,
			Ideology = IdeologyType.Monarchy,
			Legitimacy = 15f,
			AvgDissent = 0.65f,
			Support = 20f,
			FoundedYear = 0,
			Ruler = new RulerRef { ActorId = 100 + id, Name = "Правитель", KingdomId = id, SinceYear = 1, Age = 45 }
		};
	}

	private static KingdomInput Input(int kingdomId, string name, int cityCount, RulerRef? ruler = null)
	{
		var cities = new List<CityInput>();
		for (int i = 0; i < cityCount; i++)
		{
			cities.Add(City(kingdomId * 10 + i, kingdomId, 80 - i * 20));
		}
		return new KingdomInput
		{
			KingdomId = kingdomId,
			Name = name,
			Population = 50 * cityCount,
			Warriors = 20,
			Ruler = ruler,
			Relations = new List<RelationInfo>(),
			Cities = cities
		};
	}

	private static KingdomInput InputFor(int kingdomId, string name, int[] cityIds, RulerRef? ruler, RelationInfo? rel = null)
	{
		var cities = new List<CityInput>();
		foreach (int cityId in cityIds)
		{
			cities.Add(City(cityId, kingdomId, 50));
		}
		var relations = new List<RelationInfo>();
		if (rel != null)
		{
			relations.Add(rel.Value);
		}
		return new KingdomInput
		{
			KingdomId = kingdomId,
			Name = name,
			Population = 50 * cityIds.Length,
			Warriors = 20,
			Ruler = ruler,
			Relations = relations,
			Cities = cities
		};
	}

	private static CityInput City(int cityId, int kingdomId, int loyalty)
	{
		return new CityInput { CityId = cityId, KingdomId = kingdomId, Population = 50, FoodOk = true, Loyalty = loyalty };
	}

	private sealed class FixedRandom : Random
	{
		private readonly double _val;

		public FixedRandom(double val)
		{
			_val = val;
		}

		public override double NextDouble() => _val;

		public override int Next(int maxValue) => maxValue / 2;
	}
}
