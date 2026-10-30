using RealPolitic.Core;
using Xunit;

namespace RealPolitic.Tests;

/// <summary>
/// 0.48.4: катка 29.10.2026 (год 57) — держава называлась
/// «Независимое Королевство Независимое Королевство Край»: мод применил титул к имени,
/// в котором титул уже был. Причина — держава называется словом «Край», а это слово
/// входит в наши титульные наборы («Вольный Край»), поэтому очистка имени возвращала
/// его целиком.
/// </summary>
public class StateNameCollapseTests
{
	[Fact]
	public void CountryNamedLikeATitleKeepsItsName()
	{
		Assert.Equal("Край", IdeologyStateNames.StripAllTitles("Независимое Королевство Край"));
		Assert.Equal("Край", StateNaming.Root("Независимое Королевство Край"));
	}

	[Fact]
	public void TitleIsNotAppliedTwice()
	{
		string name = StateNaming.MapName(IdeologyType.Monarchy, "Независимое Королевство Край", 42);
		Assert.DoesNotContain("Независимое Королевство Независимое", name);
		Assert.Contains("Край", name);
	}

	[Fact]
	public void RepeatedPhraseIsCollapsed()
	{
		Assert.Equal("Независимое Королевство Край",
			StateNaming.CollapseRepeatedPhrase("Независимое Королевство Независимое Королевство Край"));
		Assert.Equal("Гогиш", StateNaming.CollapseRepeatedPhrase("Гогиш Гогиш"));
	}

	[Fact]
	public void NormalNamesStayUntouched()
	{
		Assert.Equal("Королевство Гогиш", StateNaming.CollapseRepeatedPhrase("Королевство Гогиш"));
		Assert.Equal("Соединённое Королевство Норара",
			StateNaming.CollapseRepeatedPhrase("Соединённое Королевство Норара"));
		Assert.Equal("Гогиш", StateNaming.Root("Королевство Гогиш"));
	}
}
