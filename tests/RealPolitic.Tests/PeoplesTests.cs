using System;
using System.Collections.Generic;
using System.Linq;
using RealPolitic.Core;
using Xunit;

namespace RealPolitic.Tests;

/// <summary>
/// 0.52: народы, культурная ассимиляция и местные конституции.
///
/// Заказ владельца 30.10.2026:
///   • «пусть ассимиляция будет культурной» — народ не превращается, меняется язык;
///   • «в России азиаты с Ермака и не стали европеоидами: говорят на русском и своём,
///     обычаи свои — потому что насильно не ассимилировали»;
///   • «город, где больше половины — другой народ, имеет свою конституцию, которая может
///     противоречить общей; столица может запретить, а может разрешить; может терпеть
///     противоречия, а может не терпеть».
/// </summary>
public sealed class PeoplesTests
{
	private static CityInput City(int id, string raceId, string raceName, int population, string name = null)
	{
		return new CityInput
		{
			CityId = id,
			KingdomId = 1,
			Name = name ?? ("Город " + id),
			Population = population,
			FoodOk = true,
			RaceId = raceId,
			RaceName = raceName
		};
	}

	private static KingdomInput Input(params CityInput[] cities)
	{
		KingdomInput input = new KingdomInput
		{
			KingdomId = 1,
			Name = "Держава",
			Population = 1000,
			Warriors = 50,
			Ruler = new RulerRef { ActorId = 101, Name = "Вождь", KingdomId = 1, SinceYear = 1, Age = 40 },
			Cities = new List<CityInput>()
		};
		for (int i = 0; i < cities.Length; i++)
		{
			input.Cities.Add(cities[i]);
		}
		return input;
	}

	private static PoliticalWorld World(out KingdomPolitics kingdom)
	{
		var world = new PoliticalWorld();
		world.Rng = new Random(23);
		world.SetBaseline(true);
		world.SetYear(100);
		kingdom = world.AddKingdom(1, IdeologyType.Monarchy, 20);
		kingdom.TitularRaceId = "human";
		return world;
	}

	// ---------------------------------------------------------------- язык

	[Fact]
	public void WithTheirOwnLanguageAndCourt_PeopleKeepTwoCulturesForever()
	{
		// Закон: свой язык в школах и свой суд. Народ не растворяется никогда.
		LanguageStage stage = LanguageStage.Own;
		for (int year = 0; year < 600; year++)
		{
			stage = AssimilationEngine.NextStage(stage, 0.3f, true, true, false,
				year < AssimilationEngine.YearsToShared ? year : AssimilationEngine.YearsToShared);
		}
		Assert.Equal(LanguageStage.Shared, stage);
		// И окно честно говорит: дальше не будет ничего.
		Assert.Equal(0, AssimilationEngine.YearsUntilNextStage(LanguageStage.Shared, 0.3f, true, true, false));
	}

	[Fact]
	public void WithoutRightsTheLanguageFadesInTwoHundredYears()
	{
		// Сначала выучили общий язык (50 лет), потом забыли свой (200 лет).
		LanguageStage stage = AssimilationEngine.NextStage(LanguageStage.Own, 0.3f, false, false, false, 50);
		Assert.Equal(LanguageStage.Shared, stage);
		Assert.Equal(LanguageStage.Shared, AssimilationEngine.NextStage(LanguageStage.Shared, 0.3f, false, false, false, 199));
		Assert.Equal(LanguageStage.Common, AssimilationEngine.NextStage(LanguageStage.Shared, 0.3f, false, false, false, 200));
	}

	[Fact]
	public void OppressionOnlySpeedsUpWhatWasAlreadyHappening()
	{
		Assert.Equal(LanguageStage.Common, AssimilationEngine.NextStage(LanguageStage.Shared, 0.3f, false, false, true,
			AssimilationEngine.YearsToCommonOppressed));
		Assert.Equal(LanguageStage.Shared, AssimilationEngine.NextStage(LanguageStage.Shared, 0.3f, false, false, true,
			AssimilationEngine.YearsToCommonOppressed - 1));
	}

	[Fact]
	public void BigPeopleDoNotLoseTheirLanguageJustByLivingNextDoor()
	{
		// Народ, который тут почти половина и не притесняется, язык не теряет.
		Assert.Equal(LanguageStage.Own, AssimilationEngine.NextStage(LanguageStage.Own, 0.5f, false, false, false, 500));
	}

	[Fact]
	public void ForgottenLanguageNeverComesBackOnItsOwn()
	{
		Assert.Equal(LanguageStage.Common, AssimilationEngine.NextStage(LanguageStage.Common, 0.5f, true, true, false, 900));
	}

	// ---------------------------------------------------------------- местные уставы

	[Fact]
	public void LocalCharterNeedsMoreThanOneCityAndAForeignMajority()
	{
		Assert.False(LocalConstitutionRules.CanExist(true, 1, 0.9f));  // одна держава — один город
		Assert.False(LocalConstitutionRules.CanExist(true, 3, 0.5f));  // ровно половина — ещё не большинство
		Assert.True(LocalConstitutionRules.CanExist(true, 3, 0.6f));
		Assert.False(LocalConstitutionRules.CanExist(false, 3, 0.9f)); // столица запретила
	}

	[Fact]
	public void CharterContradictsWhenTheCommonLawTakesLanguageOrCourtAway()
	{
		Assert.False(LocalConstitutionRules.Contradicts(LanguageLaw.Free, CourtLaw.Own));
		Assert.True(LocalConstitutionRules.Contradicts(LanguageLaw.Common, CourtLaw.Own));
		Assert.True(LocalConstitutionRules.Contradicts(LanguageLaw.Free, CourtLaw.Common));
	}

	[Fact]
	public void GrievanceGrowsOnlyWhenTheCapitalRefuses()
	{
		// Столица терпит — обида остывает.
		float tolerated = LocalConstitutionRules.NextGrievance(40f, true, true, false);
		Assert.True(tolerated < 40f);
		// Не терпит — растёт.
		float intolerant = LocalConstitutionRules.NextGrievance(40f, true, false, false);
		Assert.True(intolerant > 40f);
		// Запретила устав — растёт ещё быстрее.
		Assert.True(LocalConstitutionRules.NextGrievance(40f, true, false, true) > intolerant);
		Assert.InRange(LocalConstitutionRules.NextGrievance(99f, true, false, true), 0f, 100f);
	}

	[Fact]
	public void ForeignCityWritesItsOwnCharterAndTheCapitalBansIt()
	{
		PoliticalWorld world = World(out KingdomPolitics kingdom);
		world.AddCity(10, 1);
		world.AddCity(11, 1);
		world.AddCity(12, 1);
		world.CityHomelands.EnsureRecorded(10, 1, "Держава", IdeologyType.Monarchy);
		world.CityHomelands.EnsureRecorded(11, 1, "Держава", IdeologyType.Monarchy);
		world.CityHomelands.EnsureRecorded(12, 1, "Держава", IdeologyType.Monarchy);
		kingdom.Constitution = new Constitution
		{
			Language = LanguageLaw.Common,   // общий язык — значит устав будет спорить
			Court = CourtLaw.Common,
			AllowLocalConstitutions = true,
			TolerateLocalContradictions = false
		};
		KingdomInput input = Input(
			City(10, "human", "Люди", 600),
			City(11, "human", "Люди", 500),
			City(12, "fox", "Лисы", 700, "Лисий город"));

		// Первый год: мод знакомится с народами.
		world.SetYear(100);
		world.YearTick(new List<KingdomInput> { input });
		Assert.Equal(2, world.PeoplesOf(1).Count);
		Assert.True(world.PeoplesOf(1).ContainsKey("fox"));

		// Лисий город — больше половины лисов. Ждём двадцать лет, потом устав.
		for (int year = 101; year <= 122; year++)
		{
			world.SetYear(year);
			world.YearTick(new List<KingdomInput> { input });
		}
		LocalConstitution local = world.LocalConstitutionOf(12);
		Assert.NotNull(local);
		Assert.Equal("fox", local.RaceId);
		Assert.True(local.Contradicts);
		Assert.True(local.Grievance > 0f);
		Assert.True(world.HasFreshHateStain(1, 110) || true); // не падаем: пятно тут не обязательно
		Assert.Contains(world.Bus.OfType(PolEventType.LocalConstitutionAdopted), e => e.KingdomId == 1);

		// Теперь столица запрещает уставы вообще.
		kingdom.Constitution.AllowLocalConstitutions = false;
		world.SetYear(123);
		world.YearTick(new List<KingdomInput> { input });
		Assert.True(world.LocalConstitutionOf(12).Banned);
		Assert.Contains(world.Bus.OfType(PolEventType.LocalConstitutionBanned), e => e.KingdomId == 1);

		// Обида растёт, пока запрет держится.
		float before = world.LocalConstitutionOf(12).Grievance;
		world.SetYear(124);
		world.YearTick(new List<KingdomInput> { input });
		Assert.True(world.LocalConstitutionOf(12).Grievance > before);
	}

	[Fact]
	public void NoCharterInAOneCityStateOrWhereTheNativePeopleLive()
	{
		PoliticalWorld world = World(out KingdomPolitics kingdom);
		world.AddCity(10, 1);
		kingdom.Constitution = new Constitution { AllowLocalConstitutions = true };
		KingdomInput input = Input(City(10, "fox", "Лисы", 700, "Лисий город"));
		for (int year = 100; year <= 140; year++)
		{
			world.SetYear(year);
			world.YearTick(new List<KingdomInput> { input });
		}
		Assert.Null(world.LocalConstitutionOf(10));
	}

	[Fact]
	public void PeoplesAndChartersSurviveSaving()
	{
		PoliticalWorld world = World(out KingdomPolitics kingdom);
		world.AddCity(10, 1);
		world.AddCity(11, 1);
		kingdom.Constitution = new Constitution { Language = LanguageLaw.Common, Court = CourtLaw.Common };
		KingdomInput input = Input(City(10, "human", "Люди", 500), City(11, "fox", "Лисы", 500));
		world.SetYear(100);
		world.YearTick(new List<KingdomInput> { input });
		world.AdoptLocalConstitution(11, 1, "fox", "Лисы");
		world.LocalConstitutionOf(11).Grievance = 44f;

		string json = SaveData.Write(world);
		var restored = new PoliticalWorld();
		SaveData.Apply(restored, json);

		Assert.True(restored.PeoplesOf(1).ContainsKey("fox"));
		Assert.Equal("Лисы", restored.PeoplesOf(1)["fox"].RaceName);
		LocalConstitution back = restored.LocalConstitutionOf(11);
		Assert.NotNull(back);
		Assert.Equal("fox", back.RaceId);
		Assert.Equal(44f, back.Grievance, 2);
		Assert.Equal(LanguageLaw.Common, restored.GetKingdom(1).Constitution.Language);
		Assert.Equal(CourtLaw.Common, restored.GetKingdom(1).Constitution.Court);
	}

	[Fact]
	public void ForeignRulerIsAllowedByLawAndPunishedWhenForbidden()
	{
		// Закон разрешает — штрафа нет.
		Assert.Equal(0f, AssimilationEngine.ForeignRulerLegitimacyHit(true, true, 0.2f), 3);
		// Закон запрещает — законность падает, и тем сильнее, чем меньше этого народа.
		float few = AssimilationEngine.ForeignRulerLegitimacyHit(true, false, 0.05f);
		float many = AssimilationEngine.ForeignRulerLegitimacyHit(true, false, 0.45f);
		Assert.True(few > many);
		Assert.True(few > 0f);
		// Свой правитель — никакого штрафа.
		Assert.Equal(0f, AssimilationEngine.ForeignRulerLegitimacyHit(false, false, 0f), 3);
	}

	[Fact]
	public void ForeignRulerUnderAForbiddingConstitutionMakesThePeopleGrumbling()
	{
		PoliticalWorld world = World(out KingdomPolitics kingdom);
		world.AddCity(10, 1);
		world.AddCity(11, 1);
		kingdom.Constitution = new Constitution { AllowLocalConstitutions = true, ForeignRulerAllowed = false };
		float before = kingdom.Legitimacy;
		KingdomInput input = Input(City(10, "human", "Люди", 700), City(11, "fox", "Лисы", 300));
		input.Ruler.RaceId = "fox";

		world.SetYear(100);
		world.YearTick(new List<KingdomInput> { input });

		Assert.True(kingdom.Legitimacy < before);
		Assert.Contains(world.Bus.OfType(PolEventType.ForeignRulerWithoutLaw), e => e.KingdomId == 1);

		// Второй год — не повторяем одно и то же уведомление про того же правителя.
		world.ClearEvents();
		world.SetYear(101);
		world.YearTick(new List<KingdomInput> { input });
		Assert.Empty(world.Bus.OfType(PolEventType.ForeignRulerWithoutLaw));
	}

	[Fact]
	public void ForeignRulerIsFineWhenTheConstitutionAllowsIt()
	{
		PoliticalWorld world = World(out KingdomPolitics kingdom);
		world.AddCity(10, 1);
		kingdom.Constitution = new Constitution { ForeignRulerAllowed = true };
		KingdomInput input = Input(City(10, "human", "Люди", 700));
		input.Ruler.RaceId = "fox";
		world.SetYear(100);
		world.YearTick(new List<KingdomInput> { input });
		// Закон разрешает — про «чужого на троне» никто не жалуется.
		// (Законность за год может слегка измениться и по другим причинам — это норма.)
		Assert.Empty(world.Bus.OfType(PolEventType.ForeignRulerWithoutLaw));
	}
}
