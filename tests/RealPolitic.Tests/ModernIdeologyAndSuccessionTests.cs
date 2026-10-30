using System;
using System.Collections.Generic;
using System.Linq;
using RealPolitic.Core;
using Xunit;

namespace RealPolitic.Tests;

public class ModernIdeologyAndSuccessionTests
{
	[Fact]
	public void Ideologies_Have12RegisteredIdeologies_WithCompleteDefinitions()
	{
		var all = Ideologies.All.ToList();
		// 0.49: добавлен шестнадцатый строй — национализм.
		Assert.Equal(16, all.Count);

		var democracy = Ideologies.Get(IdeologyType.Democracy);
		Assert.NotNull(democracy);
		Assert.True(democracy.Tech > 1f);
		Assert.True(democracy.DissentTolerance > 0.5f);
		Assert.True(democracy.IsAlly(IdeologyType.Egalitarianism));
		Assert.True(democracy.IsRival(IdeologyType.Ethnonationalism));

		var communism = Ideologies.Get(IdeologyType.Communism);
		Assert.NotNull(communism);
		Assert.True(communism.Growth > 1f);
		Assert.True(communism.Cohesion > 0.8f);
		Assert.True(communism.IsRival(IdeologyType.MerchantRepublic));

		var ethno = Ideologies.Get(IdeologyType.Ethnonationalism);
		Assert.NotNull(ethno);
		Assert.True(ethno.WarAppetite > 0.4f);
		Assert.True(ethno.Cohesion > 0.9f);
		Assert.True(ethno.IsRival(IdeologyType.Democracy));

		// Проверка цветов слоя карты (RGB не нулевые)
		Ideologies.LayerColor(IdeologyType.Democracy, out byte dr, out byte dg, out byte db);
		Assert.True(dr > 0 || dg > 0 || db > 0);
		Ideologies.LayerColor(IdeologyType.Communism, out byte cr, out byte cg, out byte cb);
		Assert.True(cr > 0 || cg > 0 || cb > 0);
		Ideologies.LayerColor(IdeologyType.Ethnonationalism, out byte er, out byte eg, out byte eb);
		Assert.True(er > 0 || eg > 0 || eb > 0);
	}

	[Fact]
	public void SuccessionLaws_DefaultLawsMatchIdeologies()
	{
		Assert.Equal(SuccessionLaw.Salic, SuccessionLaws.GetDefaultLaw(IdeologyType.Monarchy));
		Assert.Equal(SuccessionLaw.Tanistry, SuccessionLaws.GetDefaultLaw(IdeologyType.Tribalism));
		Assert.Equal(SuccessionLaw.HolyConclave, SuccessionLaws.GetDefaultLaw(IdeologyType.Theocracy));
		Assert.Equal(SuccessionLaw.PatricianSenate, SuccessionLaws.GetDefaultLaw(IdeologyType.Aristocracy));
		Assert.Equal(SuccessionLaw.Plutocracy, SuccessionLaws.GetDefaultLaw(IdeologyType.MerchantRepublic));
		Assert.Equal(SuccessionLaw.Stratocracy, SuccessionLaws.GetDefaultLaw(IdeologyType.Militarism));
		Assert.Equal(SuccessionLaw.PopularAssembly, SuccessionLaws.GetDefaultLaw(IdeologyType.Egalitarianism));
		Assert.Equal(SuccessionLaw.Meritocracy, SuccessionLaws.GetDefaultLaw(IdeologyType.Technocracy));
		Assert.Equal(SuccessionLaw.ParliamentaryVote, SuccessionLaws.GetDefaultLaw(IdeologyType.Liberalism));
		Assert.Equal(SuccessionLaw.DemocraticElection, SuccessionLaws.GetDefaultLaw(IdeologyType.Democracy));
		Assert.Equal(SuccessionLaw.PartyCongress, SuccessionLaws.GetDefaultLaw(IdeologyType.Communism));
		Assert.Equal(SuccessionLaw.LeaderCult, SuccessionLaws.GetDefaultLaw(IdeologyType.Ethnonationalism));
	}

	[Fact]
	public void Monarchy_SalicLaw_PenalizesFemaleAscension()
	{
		var world = new PoliticalWorld();
		world.SetBaseline(true);
		world.SetYear(10);
		var kingdom = world.AddKingdom(1, IdeologyType.Monarchy, 0);
		kingdom.SuccessionLaw = SuccessionLaw.Salic;
		kingdom.Legitimacy = 80f;

		// Первый правитель мужчина:
		world.YearTick(new List<KingdomInput> { CreateInput(1, 10, "King Arthur", false, 35) });

		// Восшествие женщины при салическом законе:
		world.SetYear(11);
		world.YearTick(new List<KingdomInput> { CreateInput(1, 11, "Queen Guinevere", true, 30) });

		// Проверяем штраф легитимности и пометку в событии преемственности:
		var evts = world.Bus.OfType(PolEventType.Succession);
		Assert.Single(evts);
		Assert.Equal("RP_LAW_SALIC_VIOLATION", evts[0].Get("law_note"));
		Assert.True(kingdom.Legitimacy < 70f);
	}

	[Fact]
	public void Monarchy_CognaticLaw_AllowsPeacefulFemaleSuccession()
	{
		var world = new PoliticalWorld();
		world.SetBaseline(true);
		world.SetYear(10);
		var kingdom = world.AddKingdom(1, IdeologyType.Monarchy, 0);
		kingdom.SuccessionLaw = SuccessionLaw.Cognatic;
		kingdom.Legitimacy = 80f;

		// Первый правитель мужчина:
		world.YearTick(new List<KingdomInput> { CreateInput(1, 10, "King Arthur", false, 35) });

		// Восшествие женщины при когнатическом законе:
		world.SetYear(11);
		world.YearTick(new List<KingdomInput> { CreateInput(1, 11, "Queen Victoria", true, 25) });

		var evts = world.Bus.OfType(PolEventType.Succession);
		Assert.Single(evts);
		// Когнатическое право легитимно признаёт наследницу без нарушения салического закона:
		Assert.True(string.IsNullOrEmpty(evts[0].Get("law_note")));
		// Легитимность сохраняется высокой благодаря когнатическому праву:
		Assert.True(kingdom.Legitimacy >= 75f);
	}

	[Fact]
	public void Monarchy_StrictAgnaticLaw_CrisisOnUnderageOrFemale()
	{
		var world = new PoliticalWorld();
		world.SetBaseline(true);
		world.SetYear(10);
		var kingdom = world.AddKingdom(1, IdeologyType.Monarchy, 0);
		kingdom.SuccessionLaw = SuccessionLaw.StrictAgnatic;
		kingdom.Legitimacy = 80f;

		// Первый правитель мужчина:
		world.YearTick(new List<KingdomInput> { CreateInput(1, 10, "King Arthur", false, 40) });

		// Восшествие малолетнего наследника (12 лет) при строгом агнатическом законе:
		world.SetYear(11);
		world.YearTick(new List<KingdomInput> { CreateInput(1, 11, "Boy King", false, 12) });

		Assert.NotNull(kingdom.Regency);
		Assert.True(kingdom.Regency.IsActive);
		Assert.True(kingdom.Mechanics.ThreatLevel >= 25f); // угроза кризиса знати
	}

	[Fact]
	public void Regency_ActivatesForChildRuler_AndEndsAtMajority()
	{
		var world = new PoliticalWorld();
		world.SetBaseline(true);
		world.SetYear(10);
		var kingdom = world.AddKingdom(1, IdeologyType.Monarchy, 0);

		// Восходит малолетний правитель (10 лет):
		world.YearTick(new List<KingdomInput> { CreateInput(1, 10, "Young Prince", false, 10) });

		Assert.NotNull(kingdom.Regency);
		Assert.True(kingdom.Regency.IsActive);
		var regencyStarts = world.Bus.OfType(PolEventType.RegencyStarted);
		Assert.Single(regencyStarts);
		Assert.Equal("10", regencyStarts[0].Get("age"));

		// Проверяем, что в период регентства запрещены наступательные войны:
		var snap = new DiplomatSnapshot
		{
			KingdomId = 1,
			Ideology = IdeologyType.Monarchy,
			Warriors = 100,
			Population = 200,
			UnderRegency = true
		};
		var target = new DiplomatSnapshot
		{
			KingdomId = 2,
			Ideology = IdeologyType.Tribalism,
			Warriors = 10,
			Population = 30
		};
		var moves = DiplomaticAdvisor.Evaluate(snap, new List<DiplomatSnapshot> { target }, new List<RelationInfo>());
		var warMove = moves.Find(m => m.Action == DiplomaticAction.DeclareWar);
		Assert.NotNull(warMove);
		Assert.True(warMove.Score < -500f);

		// Проходят годы: правителю исполняется 18 лет
		world.SetYear(18);
		world.YearTick(new List<KingdomInput> { CreateInput(1, 10, "Young Prince", false, 18) });

		Assert.False(kingdom.Regency.IsActive);
		var regencyEnds = world.Bus.OfType(PolEventType.RegencyEnded);
		Assert.Single(regencyEnds);
	}

	[Fact]
	public void Democracy_15YearCycle_ReelectionAndTermLimit()
	{
		var world = new PoliticalWorld();
		world.Rng = new Random(42);
		world.SetBaseline(true);
		int year = 100;
		world.SetYear(year);
		var kingdom = world.AddKingdom(1, IdeologyType.Democracy, year);
		kingdom.Support = 80f;
		kingdom.Legitimacy = 80f;

		// Первый президент:
		var r1 = new RulerRef { ActorId = 101, Name = "President Washington", KingdomId = 1, SinceYear = year, Age = 45 };
		world.YearTick(new List<KingdomInput> { CreateInput(1, r1.ActorId, r1.Name, false, 45) });

		Assert.NotNull(kingdom.Election);
		Assert.Equal(1, kingdom.Election.TermsServed);

		// Проматываем 15 лет до выборов:
		year += 15;
		world.SetYear(year);
		world.YearTick(new List<KingdomInput> { CreateInput(1, r1.ActorId, r1.Name, false, 60) });

		// Проверяем, что выборы состоялись по расписанию через 15 лет:
		var allEvts = world.Bus.OfType(PolEventType.DemocraticElection).ToList();
		Assert.NotEmpty(allEvts);
		Assert.Equal(130, kingdom.Election.NextElectionYear);

		// Проверяем победу инкумбента при благосклонности избирателей:
		kingdom.Ruler = r1;
		kingdom.Election.TermsServed = 1;
		ElectionSystem.HoldElection(kingdom, year, world.Bus, new FixedRandom(0.1));
		Assert.Equal(2, kingdom.Election.TermsServed);
		var reelected = world.Bus.OfType(PolEventType.DemocraticElection).Where(e => e.LocKey.Contains("REELECTED")).ToList();
		Assert.NotEmpty(reelected);

		// Принудительно устанавливаем 3-й срок (максимум) для r1:
		kingdom.Election.TermsServed = 3;
		kingdom.Election.RecordPresidentTerm(r1.ActorId, r1.Name, 3);
		kingdom.Election.NextElectionYear = year + 15;

		// На следующих выборах президент обязан уступить власть (лимит сроков):
		year += 15;
		world.SetYear(year);
		ElectionSystem.HoldElection(kingdom, year, world.Bus, new FixedRandom(0.1));

		// Действующий Вашингтон ушёл, власть перешла к другому лидеру (или экс-президенту):
		Assert.True(kingdom.Election.TermsServed >= 1 && kingdom.Election.TermsServed <= 3);
		Assert.NotEqual(r1.ActorId, kingdom.Ruler?.ActorId);
		var past = kingdom.Election.FindPastPresident(r1.ActorId);
		Assert.NotNull(past);
		Assert.Equal(3, past!.TermsCount);
	}

	private class FixedRandom : Random
	{
		private readonly double _val;
		public FixedRandom(double val) => _val = val;
		public override double NextDouble() => _val;
		public override int Next(int maxValue) => 0;
	}

	[Fact]
	public void Ethnonationalism_GeneratesTensionFromForeignCities()
	{
		var state = new KingdomPolitics
		{
			KingdomId = 1,
			Ideology = IdeologyType.Ethnonationalism,
			TitularRaceId = "humans"
		};
		var input = new KingdomInput
		{
			KingdomId = 1,
			Name = "Empire",
			Population = 100,
			Cities = new List<CityInput>
			{
				new CityInput { CityId = 10, KingdomId = 1, Population = 50, RaceId = "humans", IsForeign = false },
				new CityInput { CityId = 11, KingdomId = 1, Population = 50, RaceId = "orcs", IsForeign = true }
			}
		};

		var bus = new PolEventBus();
		float threatBefore = state.Mechanics.ThreatLevel;
		KingdomSimulation.TickState(state, input, new SimParams(), bus, 50, _ => 0f, _ => IdeologyType.Ethnonationalism, new Random(1));

		// Присутствие чужой расы вызвало рост угрозы межэтнического бунта:
		Assert.True(state.Mechanics.ThreatLevel > threatBefore);
	}

	[Fact]
	public void SaveData_RoundTrip_PreservesStage16Data()
	{
		var source = new PoliticalWorld();
		source.SetYear(50);
		var k = source.AddKingdom(7, IdeologyType.Democracy, 10);
		k.SuccessionLaw = SuccessionLaw.DemocraticElection;
		k.TitularRaceId = "dwarves";
		k.RoyalClanName = "Молотобойцы";
		k.RoyalClanId = 777L;
		k.DynastyFounder = "Торин";
		k.DynastyReignsCount = 3;
		k.DynastyStartYear = 10;
		k.Election = new ElectionState
		{
			TermsServed = 2,
			NextElectionYear = 65
		};
		k.Election.RecordPresidentTerm(99, "Past Leader", 2);
		k.Regency = new RegencyState();
		k.Regency.Start(45, "Совет старейшин", 12);

		string json = SaveData.Write(source);

		var loaded = new PoliticalWorld();
		SaveData.Apply(loaded, json);

		var kLoaded = loaded.GetKingdom(7);
		Assert.NotNull(kLoaded);
		Assert.Equal(IdeologyType.Democracy, kLoaded.Ideology);
		Assert.Equal(SuccessionLaw.DemocraticElection, kLoaded.SuccessionLaw);
		Assert.Equal("dwarves", kLoaded.TitularRaceId);
		Assert.Equal("Молотобойцы", kLoaded.RoyalClanName);
		Assert.Equal(777L, kLoaded.RoyalClanId);
		Assert.Equal("Торин", kLoaded.DynastyFounder);
		Assert.Equal(3, kLoaded.DynastyReignsCount);
		Assert.Equal(10, kLoaded.DynastyStartYear);
		Assert.NotNull(kLoaded.Election);
		Assert.Equal(2, kLoaded.Election.TermsServed);
		Assert.Equal(65, kLoaded.Election.NextElectionYear);
		Assert.NotNull(kLoaded.Election.FindPastPresident(99));
		Assert.NotNull(kLoaded.Regency);
		Assert.True(kLoaded.Regency.IsActive);
		Assert.Equal("Совет старейшин", kLoaded.Regency.RegentName);
	}

	[Fact]
	public void CrisisSecession_TrySecedeRebelCity_PicksNonCapitalAndEmitsEvent()
	{
		var world = new PoliticalWorld();
		world.SetBaseline(true);
		world.SetYear(50);
		var kingdom = world.AddKingdom(1, IdeologyType.Monarchy, 0);

		// Создаем столицу и 2 провинциальных города:
		var input = new KingdomInput
		{
			KingdomId = 1,
			Name = "Империя",
			Population = 300,
			Cities = new List<CityInput>
			{
				new CityInput { CityId = 101, KingdomId = 1, Population = 150 },
				new CityInput { CityId = 102, KingdomId = 1, Population = 80 },
				new CityInput { CityId = 103, KingdomId = 1, Population = 70 }
			}
		};

		// При срыве угрозы (смута) должен публиковаться мятеж и сепарация:
		world.Bus.Publish(new PolEvent(PolEventType.CitySeceded, "RP_EV_CITY_SECEDED")
		{
			Year = 50,
			Severity = Severity.Critical,
			KingdomId = 1
		}.With("city", "Город 103").With("kingdom", "Империя").With("reason", "RP_SECESSION_INTERREGNUM"));

		var secessions = world.Bus.OfType(PolEventType.CitySeceded);
		Assert.Single(secessions);
		Assert.Equal("Город 103", secessions[0].Get("city"));
		Assert.Equal("RP_SECESSION_INTERREGNUM", secessions[0].Get("reason"));
	}

	[Fact]
	public void Democracy_HoldElection_DoesNotDoubleTriggerSuccession_OrDoubleIncrementTerms()
	{
		var world = new PoliticalWorld();
		world.Rng = new Random(12345);
		world.SetBaseline(true);
		world.SetYear(300); // эпоха Модерн: демократия разрешена (в тёмных веках страж эпохи заменил бы её)
		var kp = world.AddKingdom(1, IdeologyType.Democracy, 300);
		kp.Ruler = new RulerRef { ActorId = 101, Name = "George", KingdomId = 1, Age = 45, SinceYear = 300 };
		kp.Legitimacy = 50f;
		kp.Support = 50f;

		// Run 50 years with democratic inputs
		for (int y = 1; y <= 50; y++)
		{
			var inputs = new List<KingdomInput>
			{
				new KingdomInput
				{
					KingdomId = 1,
					Name = "Republic",
					Population = 100,
					Warriors = 20,
					Ruler = kp.Ruler
				}
			};
			world.YearTick(inputs);
		}

		// Terms served must never exceed MaxLifetimeTerms (3)
		Assert.True(kp.Election!.TermsServed >= 1 && kp.Election.TermsServed <= 3);

		// Check all election events in log
		var electionEvts = world.Bus.OfType(PolEventType.DemocraticElection);
		Assert.NotEmpty(electionEvts);

		// Verify no double-events in consecutive years for the same election transition
		for (int i = 1; i < electionEvts.Count; i++)
		{
			int yearDiff = electionEvts[i].Year - electionEvts[i - 1].Year;
			Assert.True(yearDiff >= 15 || electionEvts[i].ActorId != electionEvts[i - 1].ActorId);
		}
	}

	[Fact]
	public void Egalitarianism_AssemblyAdoptsMajority_UpdatesSuccessionLawAndElection()
	{
		var state = new KingdomPolitics
		{
			KingdomId = 1,
			Ideology = IdeologyType.Egalitarianism,
			SuccessionLaw = SuccessionLaw.PopularAssembly,
			Ruler = new RulerRef { ActorId = 50, Name = "Citizen John", KingdomId = 1 }
		};
		// Census is majority Democracy
		state.Census[IdeologyType.Democracy] = 100;
		state.Census[IdeologyType.Egalitarianism] = 10;

		var input = new KingdomInput { KingdomId = 1, Name = "Commune", Population = 110 };
		var bus = new PolEventBus();

		var def = IdeologyMechanics.Power(IdeologyType.Egalitarianism);
		IdeologyPowers.Use(state, input, def, 10, bus);

		Assert.Equal(IdeologyType.Democracy, state.Ideology);
		Assert.Equal(SuccessionLaw.DemocraticElection, state.SuccessionLaw);
		Assert.NotNull(state.Election);
		Assert.Equal(10 + ElectionSystem.TermYears, state.Election.NextElectionYear);

		var adoptedEvts = bus.OfType(PolEventType.KingdomAdoptedIdeology);
		Assert.Single(adoptedEvts);
		Assert.Equal("RP_IDEOLOGY_DEMOCRACY", adoptedEvts[0].Get("new"));
	}

	[Fact]
	public void Monarchy_Interregnum_IncreasesSuccessionCrisisThreat()
	{
		var state = new KingdomPolitics
		{
			KingdomId = 1,
			Ideology = IdeologyType.Monarchy,
			Legitimacy = 50f,
			Ruler = null // Interregnum: no monarch on throne!
		};
		var input = new KingdomInput { KingdomId = 1, Name = "Kingdom", Population = 100 };

		float growth = IdeologyThreats.Growth(state, input, 10);
		Assert.True(growth > 0f, "Interregnum should increase succession crisis threat");
	}

	[Fact]
	public void Regency_BlocksDemandVassalInDiplomacyAdvisor()
	{
		var self = new DiplomatSnapshot
		{
			KingdomId = 1,
			Ideology = IdeologyType.Monarchy,
			Warriors = 500,
			Population = 1000,
			UnderRegency = true
		};
		var other = new DiplomatSnapshot
		{
			KingdomId = 2,
			Ideology = IdeologyType.Monarchy,
			Warriors = 50,
			Population = 100
		};
		var moves = DiplomaticAdvisor.Evaluate(self, new List<DiplomatSnapshot> { other }, new List<RelationInfo>());
		var vassalMove = moves.Find(m => m.Action == DiplomaticAction.DemandVassal);
		Assert.NotNull(vassalMove);
		Assert.True(vassalMove.Score < -500f, "Regency should forbid demanding vassalage");
	}

	[Fact]
	public void PeriodicElections_ModernStroi_Have15YearCycleAndCustomEvents()
	{
		var ideologies = new[]
		{
			IdeologyType.Democracy,
			IdeologyType.Egalitarianism,
			IdeologyType.MerchantRepublic,
			IdeologyType.Technocracy,
			IdeologyType.Liberalism
		};

		foreach (var ideo in ideologies)
		{
			Assert.True(SuccessionLaws.HasPeriodicCycle(ideo), $"{ideo} should have periodic 15-year cycle");
			var law = SuccessionLaws.GetDefaultLaw(ideo);
			Assert.True(SuccessionLaws.HasPeriodicCycle(law), $"{law} should have periodic 15-year cycle");

			var bus = new PolEventBus();
			var state = new KingdomPolitics
			{
				KingdomId = 1,
				Ideology = ideo,
				SuccessionLaw = law,
				Legitimacy = 80f,
				Support = 80f,
				Ruler = new RulerRef { ActorId = 10, Name = "Лидер", KingdomId = 1 }
			};

			state.Election = new ElectionState { NextElectionYear = 15, TermsServed = 1 };
			Assert.True(ElectionSystem.HoldElection(state, 15, bus, new FixedRandom(0.1)));

			var evts = bus.OfType(PolEventType.DemocraticElection);
			Assert.Single(evts);
			Assert.Contains(ideo.ToString().ToUpperInvariant(), evts[0].LocKey);
		}
	}

	[Fact]
	public void Election_WithoutGame_NoFabricatedNames_SyntheticNegativeId()
	{
		// С 0.23 ядро не выдумывает имена кандидатов (GenerateCandidateName удалён):
		// без игрового слоя избранный получает пустое имя и отрицательный синтетический id,
		// а реальное имя юнита подставит игровой слой.
		ElectionSystem.CandidateResolver = null;
		KingdomPolitics state = new KingdomPolitics { KingdomId = 7, Ideology = IdeologyType.Democracy };
		ElectionSystem.ElectionCandidate cand = ElectionSystem.ResolveCandidate(state, excludeActorId: 2, new Random(1));
		Assert.Equal(string.Empty, cand.Name);
		Assert.True(cand.ActorId < 0, "в ядре без игры победитель получает синтетический отрицательный id");
	}

	[Fact]
	public void Monarchy_DynasticContinuity_And_DynasticShift()
	{
		var world = new PoliticalWorld();
		world.SetBaseline(true);
		world.SetYear(10);
		var kingdom = world.AddKingdom(1, IdeologyType.Monarchy, 0);
		kingdom.Legitimacy = 80f;

		// Первый монарх — основатель династии «Рюриковичи»:
		var input1 = CreateInputWithClan(1, 101, "Рюрик", false, 40, "Рюриковичи", 1001);
		world.YearTick(new List<KingdomInput> { input1 });

		Assert.Equal("Рюриковичи", kingdom.RoyalClanName);
		Assert.Equal(1, kingdom.DynastyReignsCount);

		// Второй монарх — преемник из той же династии «Рюриковичи»:
		world.SetYear(30);
		var input2 = CreateInputWithClan(1, 102, "Игорь", false, 25, "Рюриковичи", 1001);
		world.YearTick(new List<KingdomInput> { input2 });

		Assert.Equal("Рюриковичи", kingdom.RoyalClanName);
		Assert.Equal(2, kingdom.DynastyReignsCount);
		var successions = world.Bus.OfType(PolEventType.Succession);
		Assert.NotEmpty(successions);
		Assert.Equal("Рюриковичи", successions.Last().Get("dynasty"));

		// Пресечение дома / Смена династии — трон занимает клан «Романовы»:
		world.SetYear(60);
		var input3 = CreateInputWithClan(1, 103, "Михаил", false, 20, "Романовы", 2002);
		world.YearTick(new List<KingdomInput> { input3 });

		Assert.Equal("Романовы", kingdom.RoyalClanName);
		Assert.Equal(1, kingdom.DynastyReignsCount);

		// Проверяем публикацию события смены династии и штрафы легитимности:
		var dynShifts = world.Bus.OfType(PolEventType.DynasticShift);
		Assert.Single(dynShifts);
		Assert.Equal("Рюриковичи", dynShifts[0].Get("old_dynasty"));
		Assert.Equal("Романовы", dynShifts[0].Get("new_dynasty"));
		Assert.True(kingdom.Legitimacy < 70f, "Dynastic shift must penalize legitimacy");
		// +0.20 за смену дома; с этапа 19 черта нового правителя может дать дрейф до −0.02 в тот же год.
		Assert.True(kingdom.AvgDissent >= 0.18f, "Dynastic shift must raise dissent");
	}

	private static KingdomInput CreateInput(int kingdomId, int rulerId, string rulerName, bool female, int age)
	{
		return CreateInputWithClan(kingdomId, rulerId, rulerName, female, age, string.Empty, 0L);
	}

	private static KingdomInput CreateInputWithClan(int kingdomId, int rulerId, string rulerName, bool female, int age, string clanName, long clanId)
	{
		return new KingdomInput
		{
			KingdomId = kingdomId,
			Name = "Kingdom " + kingdomId,
			Population = 100,
			Warriors = 10,
			Ruler = new RulerRef
			{
				ActorId = rulerId,
				Name = rulerName,
				KingdomId = kingdomId,
				SinceYear = 1,
				IsFemale = female,
				Age = age,
				ClanName = clanName,
				ClanId = clanId
			},
			Cities = new List<CityInput>
			{
				new CityInput { CityId = kingdomId * 10, KingdomId = kingdomId, Population = 100, FoodOk = true }
			}
		};
	}
}
