using System;
using RealPolitic.Core;
using Xunit;

namespace RealPolitic.Tests;

/// <summary>
/// 0.49: гражданский национализм. Заказ владельца 29.10.2026.
///
/// Главное, что проверяем:
///   • федерация и уния дают самых горячих, захват — самых тихих;
///   • третье поколение родину уже не помнит (желание падает вдвое за 30 лет);
///   • придя к власти, национализм не правит сам, а возвращает прежний строй;
///   • въезд закрывается на 50 / 100 / 200 лет по силе движения;
///   • жестокий правитель — отзыв гражданства, мягкий — только запрет въезда.
/// </summary>
public class NationalismEngineTests
{
	[Fact]
	public void FederationAndUnionAreHotterThanConquest()
	{
		Assert.True(NationalismEngine.BaseShare(HomesteadBond.Union) > NationalismEngine.BaseShare(HomesteadBond.Conquest));
		Assert.True(NationalismEngine.BaseShare(HomesteadBond.Federation) > NationalismEngine.BaseShare(HomesteadBond.Vassalage));
		Assert.True(NationalismEngine.BaseShare(HomesteadBond.Vassalage) > NationalismEngine.BaseShare(HomesteadBond.Conquest));
	}

	[Fact]
	public void ThirdGenerationForgetsTheHomeland()
	{
		// Через 60 лет (два поколения) желание вдвое меньше, через 120 — вчетверо.
		float start = 80f;
		Assert.Equal(40f, NationalismEngine.GenerationsFade(start, 60), 1);
		Assert.Equal(20f, NationalismEngine.GenerationsFade(start, 120), 1);
		Assert.Equal(80f, NationalismEngine.GenerationsFade(start, 0), 1);
	}

	[Fact]
	public void MovementGrowsWhenOppressedAndNeverLeavesRange()
	{
		float calm = NationalismEngine.NextPressure(10f, 0.5f, false, 0);
		float pressed = NationalismEngine.NextPressure(10f, 0.5f, true, 0);
		Assert.True(pressed > calm);
		Assert.InRange(calm, 0f, 100f);
		Assert.InRange(NationalismEngine.NextPressure(99.9f, 1f, true, 0), 0f, 100f);
		// Без бывших земель расти некуда: потолок — ноль.
		Assert.Equal(0f, NationalismEngine.NextPressure(0f, 0f, true, 0), 3);
		// С большинством бывших земель движение доходит до высокого давления.
		float hot = 0f;
		for (int year = 0; year < 60; year++)
		{
			hot = NationalismEngine.NextPressure(hot, 1f, false, year);
		}
		Assert.True(hot > NationalismEngine.PowerThreshold);
	}

	[Fact]
	public void NationalistsReturnTheSystemThatWasBeforeThem()
	{
		NationalismState state = new NationalismState { OriginKingdomId = 3, OriginName = "Пушок", Pressure = 80f };
		NationalismEngine.Seize(state, 210, IdeologyType.Democracy, RulerTrait.Just);
		Assert.Equal(210, state.SeizedYear);
		// Власть держат три года, потом возвращают демократию.
		Assert.False(NationalismEngine.TimeToReturn(state, 212));
		Assert.True(NationalismEngine.TimeToReturn(state, 213));
		Assert.Equal(IdeologyType.Democracy, NationalismEngine.ParentIdeology(state));
	}

	[Fact]
	public void NationalismNeverReturnsItself()
	{
		NationalismState state = new NationalismState { OriginKingdomId = 3, Pressure = 80f, PrevIdeology = (int)IdeologyType.Nationalism };
		Assert.Equal(IdeologyType.Democracy, NationalismEngine.ParentIdeology(state));
	}

	[Fact]
	public void EntryBanDependsOnTheStrengthOfTheMovement()
	{
		Assert.Equal(50, NationalismEngine.EntryBanYears(50f));
		Assert.Equal(100, NationalismEngine.EntryBanYears(70f));
		Assert.Equal(200, NationalismEngine.EntryBanYears(90f));
	}

	[Fact]
	public void CruelRulerRevokesCitizenshipAndJustRulerOnlyClosesBorders()
	{
		Assert.Equal(NationalismVerdict.RevokeCitizenship, NationalismEngine.Verdict(RulerTrait.Cruel, 70f));
		Assert.Equal(NationalismVerdict.RevokeCitizenship, NationalismEngine.Verdict(RulerTrait.Conqueror, 70f));
		Assert.Equal(NationalismVerdict.CloseBorders, NationalismEngine.Verdict(RulerTrait.Just, 70f));
		Assert.Equal(NationalismVerdict.CloseBorders, NationalismEngine.Verdict(RulerTrait.Diplomat, 70f));
	}

	[Fact]
	public void WeakMovementDoesNotRiseAndShadowedOneStaysQuiet()
	{
		NationalismState state = new NationalismState { OriginKingdomId = 3, Pressure = 30f };
		Assert.False(NationalismEngine.CanRise(state, 100));
		state.Pressure = 50f;
		Assert.True(NationalismEngine.CanRise(state, 100));
		Assert.False(NationalismEngine.ReadyToUprise(state, 100));
		state.Pressure = 75f;
		Assert.True(NationalismEngine.ReadyToUprise(state, 100));
		// После победы движение сидит тихо.
		state.ShadowUntilYear = 140;
		Assert.False(NationalismEngine.CanRise(state, 120));
		Assert.True(NationalismEngine.CanRise(state, 141));
	}

	[Fact]
	public void WithoutAHomelandThereIsNoNationalism()
	{
		NationalismState state = new NationalismState
		{
			OriginKingdomId = 0, OriginName = "Пушок", Pressure = 100f
		};
		Assert.False(NationalismEngine.CanRise(state, 200));
	}
}
