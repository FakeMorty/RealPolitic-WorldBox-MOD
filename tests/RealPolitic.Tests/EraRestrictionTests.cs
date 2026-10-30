using System;
using System.Collections.Generic;
using RealPolitic.Core;
using Xunit;

namespace RealPolitic.Tests;

/// <summary>Этап 24 (исправление отзывов): анахронизмов нет — строй не может
/// обогнать эпоху мира. «Коммунизм в пятом году» больше невозможен.</summary>
public class EraRestrictionTests
{
	private static readonly IdeologyType[] Moderns =
	{
		IdeologyType.Technocracy, IdeologyType.Communism, IdeologyType.Ethnonationalism
	};

	private static readonly IdeologyType[] EarlyModerns =
	{
		IdeologyType.Egalitarianism, IdeologyType.Democracy
	};

	[Fact]
	public void EraBoundaries_FollowYears()
	{
		var p = new SimParams();
		Assert.Equal(IdeologyEpoch.DarkAges, EraRules.OfYear(0, p));
		Assert.Equal(IdeologyEpoch.DarkAges, EraRules.OfYear(59, p));
		Assert.Equal(IdeologyEpoch.Medieval, EraRules.OfYear(60, p));
		Assert.Equal(IdeologyEpoch.Medieval, EraRules.OfYear(149, p));
		Assert.Equal(IdeologyEpoch.EarlyModern, EraRules.OfYear(150, p));
		Assert.Equal(IdeologyEpoch.EarlyModern, EraRules.OfYear(249, p));
		Assert.Equal(IdeologyEpoch.Modern, EraRules.OfYear(250, p));

		// Отключённые ограничения — весь мир живёт в современности.
		p.EnableEraRestrictions = false;
		Assert.Equal(IdeologyEpoch.Modern, EraRules.OfYear(1, p));
	}

	[Fact]
	public void DarkAges_BlockAllModerns()
	{
		foreach (IdeologyType m in Moderns)
		{
			Assert.False(EraRules.Allowed(m, IdeologyEpoch.DarkAges), $"{m} не должен быть доступен в тёмных веках");
			Assert.False(EraRules.Allowed(m, IdeologyEpoch.Medieval), $"{m} не должен быть доступен в средние века");
			Assert.False(EraRules.Allowed(m, IdeologyEpoch.EarlyModern), $"{m} не должен быть доступен в новое время");
		}
		foreach (IdeologyType m in EarlyModerns)
		{
			Assert.False(EraRules.Allowed(m, IdeologyEpoch.Medieval));
			Assert.True(EraRules.Allowed(m, IdeologyEpoch.EarlyModern));
		}
		Assert.True(EraRules.Allowed(IdeologyType.Monarchy, IdeologyEpoch.DarkAges));
		Assert.True(EraRules.Allowed(IdeologyType.Tribalism, IdeologyEpoch.DarkAges));
	}

	[Fact]
	public void SuccessorIdeology_NeverOutrunsEra()
	{
		// Что бы ни исповедовал новый правитель (перебираем все строи желаний),
		// держава не примет строй, невозможный в текущей эпохе.
		foreach (IdeologyType current in (IdeologyType[])Enum.GetValues(typeof(IdeologyType)))
		foreach (IdeologyType wish in (IdeologyType[])Enum.GetValues(typeof(IdeologyType)))
		foreach (IdeologyEpoch era in (IdeologyEpoch[])Enum.GetValues(typeof(IdeologyEpoch)))
		{
			IdeologyType next = ActorSimulation.SuccessorIdeology(current, wish, era);
			Assert.True(EraRules.Allowed(next, era),
				$"преемник {current}/{wish}→{next} выходит за эпоху {era}");
		}
	}

	[Fact]
	public void SuccessorIdeology_RespectsContinuity_WhenRulerUnclear()
	{
		// Без данных об убеждениях нового правителя строй сохраняется,
		// даже если нынешний уже анахроничен для совсем ранней эпохи.
		foreach (IdeologyType current in (IdeologyType[])Enum.GetValues(typeof(IdeologyType)))
		{
			IdeologyType next = ActorSimulation.SuccessorIdeology(current, null, IdeologyEpoch.DarkAges);
			Assert.True(EraRules.Allowed(next, IdeologyEpoch.DarkAges));
		}
	}

	[Fact]
	public void CitizenRoll_StaysWithinEra()
	{
		var rng = new Random(11);
		for (int i = 0; i < 1000; i++)
		{
			IdeologyType citizen = ActorIdeologyRoll.Roll(IdeologyType.Monarchy, rng, IdeologyEpoch.DarkAges);
			Assert.True(EraRules.Allowed(citizen, IdeologyEpoch.DarkAges), $"житель-анахронизм: {citizen}");
		}
	}

	[Fact]
	public void OldSaveWithCommunism_IsCorrectedToAncient()
	{
		// Мир старого формата (0.19 эра не уважала): год 5, идеология Коммунизм — хирургия.
		var world = new PoliticalWorld { Rng = new Random(3) };
		world.SetBaseline(true);
		world.SetYear(5);
		world.AddKingdom(1, IdeologyType.Communism, 0);
		world.AddCity(10, 1);
		world.YearTick(new List<KingdomInput> { Snapshot(1) });

		KingdomPolitics state = world.GetKingdom(1)!;
		Assert.True(EraRules.Allowed(state.Ideology, IdeologyEpoch.DarkAges),
			$"коммунизм должен был исцелиться к {state.Ideology}, а не застыть");
		Assert.NotEqual(IdeologyType.Communism, state.Ideology);
	}

	[Fact]
	public void OldSaveWithModerns_HealedOnlyWhenEraYoung()
	{
		// Год 260 (современность) — коммунизм легален и не трогается.
		var world = new PoliticalWorld { Rng = new Random(3) };
		world.SetBaseline(true);
		world.SetYear(260);
		world.AddKingdom(1, IdeologyType.Communism, 0);
		world.AddCity(10, 1);
		world.YearTick(new List<KingdomInput> { Snapshot(1) });
		Assert.Equal(IdeologyType.Communism, world.GetKingdom(1)!.Ideology);
	}

	[Fact]
	public void EraDreams_Disabled_AllowAnything()
	{
		var world = new PoliticalWorld { Rng = new Random(3) };
		world.SetBaseline(true);
		world.SetYear(5);
		world.Params.EnableEraRestrictions = false;
		world.AddKingdom(1, IdeologyType.Communism, 0);
		world.AddCity(10, 1);
		world.YearTick(new List<KingdomInput> { Snapshot(1) });
		Assert.Equal(IdeologyType.Communism, world.GetKingdom(1)!.Ideology);
	}

	[Fact]
	public void CitizenRoll_NullRng_StaysSafe()
	{
		Assert.Equal(IdeologyType.Monarchy, ActorIdeologyRoll.Roll(IdeologyType.Monarchy, null, IdeologyEpoch.DarkAges));
		Assert.True(EraRules.Allowed(ActorIdeologyRoll.Roll(IdeologyType.Communism, null, IdeologyEpoch.DarkAges), IdeologyEpoch.DarkAges));
		Assert.Equal(IdeologyType.Monarchy, ActorIdeologyRoll.RollDissentingEpoch(IdeologyType.Monarchy, null, IdeologyEpoch.DarkAges));
	}

	[Fact]
	public void SuccessorIdeology_NullRng_RespectsEra()
	{
		IdeologyType ok = ActorSimulation.SuccessorIdeology(IdeologyType.Communism, null, IdeologyEpoch.DarkAges);
		Assert.True(EraRules.Allowed(ok, IdeologyEpoch.DarkAges));
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
		Relations = new List<RelationInfo>(),
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
