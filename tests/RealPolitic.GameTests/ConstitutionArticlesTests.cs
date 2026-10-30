using System;
using System.Reflection;
using RealPolitic;
using Xunit;

namespace RealPolitic.Tests;

/// <summary>
/// 0.48.4: «конституция не открывается».
///
/// В Player.log от 29.10.2026 прямым текстом:
///   конституция: ошибка окна конституции: Index was outside the bounds of the array.
/// Причина: в 0.46 в конституцию добавили две статьи (о переселении и о народах),
/// а список римских чисел остался на 15 штук — шестнадцатая статья падала.
/// </summary>
public class ConstitutionArticlesTests
{
	private static string Roman(int number)
	{
		MethodInfo method = typeof(ConstitutionWindow).GetMethod("Roman",
			BindingFlags.Static | BindingFlags.NonPublic);
		Assert.NotNull(method);
		return (string)method!.Invoke(null, new object[] { number })!;
	}

	[Theory]
	[InlineData(1, "I")]
	[InlineData(4, "IV")]
	[InlineData(9, "IX")]
	[InlineData(15, "XV")]
	[InlineData(16, "XVI")]
	[InlineData(17, "XVII")]
	[InlineData(20, "XX")]
	public void RomanNumeralsCoverEveryArticle(int number, string expected)
	{
		Assert.Equal(expected, Roman(number));
	}

	[Fact]
	public void NoArticleCanFallOffTheEnd()
	{
		// Сколько бы статей ни добавили дальше — число для каждой должно найтись.
		for (int i = 1; i <= 40; i++)
		{
			Assert.False(string.IsNullOrEmpty(Roman(i)));
		}
	}
}
