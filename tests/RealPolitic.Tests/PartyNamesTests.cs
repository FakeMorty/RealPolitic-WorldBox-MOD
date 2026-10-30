using System;
using System.Collections.Generic;
using RealPolitic.Core;
using Xunit;

namespace RealPolitic.Tests;

/// <summary>
/// 0.48.3: имена партий.
/// Заказ владельца: «около 50-ти вариаций для названий партий по идеологиям
/// плюс синхронизация с названием страны».
/// </summary>
public class PartyNamesTests
{
	public static IEnumerable<object[]> Ideologies()
	{
		foreach (IdeologyType ideology in Enum.GetValues(typeof(IdeologyType)))
		{
			yield return new object[] { ideology };
		}
	}

	[Theory]
	[MemberData(nameof(Ideologies))]
	public void EveryIdeologyHasFiftyDistinctNames(IdeologyType ideology)
	{
		IReadOnlyList<string> variants = PartyNames.Variants(ideology);
		Assert.Equal(50, variants.Count);
		HashSet<string> unique = new HashSet<string>(StringComparer.Ordinal);
		foreach (string name in variants)
		{
			Assert.False(string.IsNullOrWhiteSpace(name));
			Assert.True(unique.Add(name), "повтор названия: " + name);
			Assert.False(name.Contains("  ", StringComparison.Ordinal), "двойной пробел: " + name);
			Assert.True(char.IsUpper(name[0]), "название с маленькой буквы: " + name);
		}
	}

	[Fact]
	public void NameIsBoundToTheCountry()
	{
		string name = PartyNames.For(IdeologyType.Democracy, "Ракрог", 7);
		Assert.Contains("«Ракрог»", name);
	}

	[Fact]
	public void SameCountryAlwaysGetsTheSameParty()
	{
		// Иначе название партии дёргалось бы каждый год.
		string first = PartyNames.For(IdeologyType.Aristocracy, "Пушок", 12);
		for (int i = 0; i < 20; i++)
		{
			Assert.Equal(first, PartyNames.For(IdeologyType.Aristocracy, "Пушок", 12));
		}
	}

	[Fact]
	public void DifferentCountriesGetDifferentNames()
	{
		HashSet<string> seen = new HashSet<string>(StringComparer.Ordinal);
		for (int id = 1; id <= 12; id++)
		{
			seen.Add(PartyNames.For(IdeologyType.Monarchy, "Держава" + id, id));
		}
		Assert.True(seen.Count >= 6, "слишком мало разных названий на 12 держав: " + seen.Count);
	}

	[Fact]
	public void DifferentIdeologiesSoundDifferent()
	{
		string monarchy = PartyNames.For(IdeologyType.Monarchy, "Ракрог", 3);
		string communism = PartyNames.For(IdeologyType.Communism, "Ракрог", 3);
		Assert.NotEqual(monarchy, communism);
		string monarchyWord = monarchy.Replace(" «Ракрог»", string.Empty).Split(' ')[0];
		string communismWord = communism.Replace(" «Ракрог»", string.Empty).Split(' ')[0];
		Assert.NotEqual(monarchyWord, communismWord);
	}

	[Fact]
	public void CountryRootWithoutTitleIsUsed()
	{
		// В имени на карте стоит титул: «Королевство Ракрог». В названии партии он не нужен.
		string root = StateNaming.Root("Королевство Ракрог");
		string name = PartyNames.For(IdeologyType.Ethnonationalism, root, 5);
		Assert.DoesNotContain("Королевство", name);
		Assert.Contains("Ракрог", name);
	}

	[Fact]
	public void EmptyCountryStillGivesReadableName()
	{
		string name = PartyNames.For(IdeologyType.Liberalism, string.Empty, 1);
		Assert.False(string.IsNullOrWhiteSpace(name));
		Assert.DoesNotContain("«", name);
	}
}
