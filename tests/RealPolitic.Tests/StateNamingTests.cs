using System.Collections.Generic;
using RealPolitic.Core;
using Xunit;

namespace RealPolitic.Tests;

/// <summary>
/// Правила имён, подтверждённые владельцем 28.10.2026.
///
/// Коренное имя («Гогиш») — для названий войн и союзов.
/// Полное имя («Королевство Гогиш», «Гогишский Рейх») — для карты, окон и летописи.
/// У племени ярлыка нет: его имя и есть название народа («Луговики», «Чащобники»).
/// </summary>
public class StateNamingTests
{
	[Fact]
	public void RootName_IsExtractedFromAnyLabelledForm()
	{
		Assert.Equal("Гогиш", StateNaming.Root("Королевство Гогиш"));
		Assert.Equal("Гогиш", StateNaming.Root("Гогишский Рейх"));
		Assert.Equal("Гогиш", StateNaming.Root("Княжество Гогиш"));
		Assert.Equal("Гогиш", StateNaming.Root("Гогиш"));
	}

	[Fact]
	public void MapName_AddsLabel_AndKeepsRootName()
	{
		string full = StateNaming.MapName(IdeologyType.Monarchy, "Гогиш", 7);

		Assert.NotEqual("Гогиш", full);
		Assert.Contains("Гогиш", full);
	}

	[Fact]
	public void MapName_IsStable_ForSameKingdom()
	{
		string first = StateNaming.MapName(IdeologyType.Monarchy, "Гогиш", 42);
		string second = StateNaming.MapName(IdeologyType.Monarchy, "Гогиш", 42);
		string again = StateNaming.MapName(IdeologyType.Monarchy, first, 42);

		Assert.Equal(first, second);
		// Имя не «размножается» при повторной обработке: «Королевство Гогиш» остаётся собой.
		Assert.Equal(first, again);
	}

	[Fact]
	public void DifferentIdeologies_GiveDifferentLabels()
	{
		var seen = new HashSet<string>();
		foreach (IdeologyType ideology in new[]
		{
			IdeologyType.Monarchy, IdeologyType.Theocracy, IdeologyType.Khaganate,
			IdeologyType.Empire, IdeologyType.Communism, IdeologyType.FreeTerritory
		})
		{
			seen.Add(StateNaming.MapName(ideology, "Гогиш", 3));
		}

		Assert.True(seen.Count >= 5, "формы разных строёв не должны совпадать: " + string.Join(" | ", seen));
	}

	[Fact]
	public void Tribe_HasNoLabel_AndKeepsItsOwnName()
	{
		Assert.Equal("Луговики", StateNaming.MapName(IdeologyType.Tribalism, "Луговики", 5));
		Assert.Equal("Чащобники", StateNaming.MapName(IdeologyType.Tribalism, "Чащобники", 9));
		Assert.False(StateNaming.HasLabel(IdeologyType.Tribalism));
		Assert.True(StateNaming.HasLabel(IdeologyType.Monarchy));
	}

	[Fact]
	public void WarTitle_UsesRootName_WithoutLabel()
	{
		string? title = IdeologyStateNames.WarTitle("Королевство Гогиш", "Каганат Вага");

		Assert.NotNull(title);
		Assert.StartsWith("Гогишско-", title);
		Assert.DoesNotContain("Королевство", title);
	}

	[Fact]
	public void LabelledName_KeepsDeclension_VariantsAvailable()
	{
		List<string> variants = IdeologyStateNames.GetAllVariants(IdeologyType.Monarchy, "Гогиш");

		Assert.True(variants.Count >= 20, "у строя должно быть не меньше 20 форм, было " + variants.Count);
		Assert.Contains("Королевство Гогиш", variants);
	}

	[Theory]
	[InlineData(IdeologyType.Monarchy)]
	[InlineData(IdeologyType.Theocracy)]
	[InlineData(IdeologyType.Aristocracy)]
	[InlineData(IdeologyType.MerchantRepublic)]
	[InlineData(IdeologyType.Militarism)]
	[InlineData(IdeologyType.Egalitarianism)]
	[InlineData(IdeologyType.Technocracy)]
	[InlineData(IdeologyType.Liberalism)]
	[InlineData(IdeologyType.Democracy)]
	[InlineData(IdeologyType.Communism)]
	[InlineData(IdeologyType.Ethnonationalism)]
	[InlineData(IdeologyType.Khaganate)]
	[InlineData(IdeologyType.Empire)]
	[InlineData(IdeologyType.FreeTerritory)]
	public void NoIdeologyCallsItselfByAbstractPolity(IdeologyType ideology)
	{
		// «Гогишская Монархия», «Гогишская Палата Лордов» — так страны не назывались.
		foreach (string variant in IdeologyStateNames.GetAllVariants(ideology, "Гогиш"))
		{
			Assert.True(StateNaming.IsPlausibleStateName(variant), "недопустимое имя: " + variant);
		}
	}

	[Theory]
	[InlineData(IdeologyType.Monarchy)]
	[InlineData(IdeologyType.Theocracy)]
	[InlineData(IdeologyType.Khaganate)]
	[InlineData(IdeologyType.Empire)]
	[InlineData(IdeologyType.Communism)]
	[InlineData(IdeologyType.FreeTerritory)]
	[InlineData(IdeologyType.Militarism)]
	[InlineData(IdeologyType.MerchantRepublic)]
	[InlineData(IdeologyType.Democracy)]
	public void EachIdeology_HasAtLeastTwentyTitles(IdeologyType ideology)
	{
		Assert.True(IdeologyStateNames.GetAllVariants(ideology, "Гогиш").Count >= 20,
			ideology + ": " + IdeologyStateNames.GetAllVariants(ideology, "Гогиш").Count);
	}

	[Fact]
	public void Monarchy_UsesRealRoyalTitles_NotTheWordMonarchy()
	{
		List<string> variants = IdeologyStateNames.GetAllVariants(IdeologyType.Monarchy, "Гогиш");

		Assert.Contains("Королевство Гогиш", variants);
		Assert.Contains("Княжество Гогиш", variants);
		Assert.Contains("Великое Княжество Гогиш", variants);
		Assert.Contains("Царство Гогиш", variants);
		Assert.Contains("Герцогство Гогиш", variants);
		Assert.DoesNotContain("Гогишская Монархия", variants);
		Assert.DoesNotContain("Самодержавие Гогиш", variants);
	}

    [Theory]
    [InlineData("Каганат Гогиш")]
    [InlineData("Королевство Гогиш")]
    [InlineData("Великое Княжество Гогиш")]
    [InlineData("Гогишское Великое Княжество")]
    [InlineData("Священная Гогишская Империя")]
    public void Chronicle_DropsForm_WhenNameAlreadyCarriesTitle(string titled)
    {
        // Шаблон летописи: «Государство {kingdom} ({state}): …». Если в имени уже есть титул,
        // скобки со строем убираются — иначе строй называется дважды.
        string template = "Государство {kingdom} ({state}): основана религия «Гогокизм»";
        string result = StateNaming.DropFormIfTitled(template, titled);
        Assert.Equal("Государство {kingdom}: основана религия «Гогокизм»", result);
    }

    [Theory]
    [InlineData("Гогиш")]
    [InlineData("Чащобники")]
    [InlineData("Луговики")]
    [InlineData("")]
    public void Chronicle_KeepsForm_WhenNameHasNoTitle(string plain)
    {
        string template = "Государство {kingdom} ({state}): основана религия «Гогокизм»";
        Assert.Equal(template, StateNaming.DropFormIfTitled(template, plain));
    }

    [Fact]
    public void Chronicle_DropForm_HandlesEmptyTemplate()
    {
        Assert.Equal(string.Empty, StateNaming.DropFormIfTitled("", "Каганат Гогиш"));
    }
}
