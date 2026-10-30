using System;
using RealPolitic.Core;
using Xunit;

namespace RealPolitic.Tests;

/// <summary>0.31.12: при старте мира допустимы только племена и каганаты.</summary>
public class StartingPoolTests
{
	[Fact]
	public void RollStarting_OnlyTribesAndKhaganates()
	{
		var rng = new Random(11);
		for (int i = 0; i < 2000; i++)
		{
			IdeologyType rolled = Ideologies.RollStarting(rng);
			Assert.True(rolled == IdeologyType.Tribalism || rolled == IdeologyType.Khaganate, rolled.ToString());
		}
	}

	[Fact]
	public void IsStartingIdeology_RejectsMilitarismMonarchyAndOthers()
	{
		Assert.True(Ideologies.IsStartingIdeology(IdeologyType.Tribalism));
		Assert.True(Ideologies.IsStartingIdeology(IdeologyType.Khaganate));
		Assert.False(Ideologies.IsStartingIdeology(IdeologyType.Militarism));
		Assert.False(Ideologies.IsStartingIdeology(IdeologyType.Monarchy));
		Assert.False(Ideologies.IsStartingIdeology(IdeologyType.Theocracy));
	}
}

/// <summary>0.31.12: коренное имя без ярлыка строя (лодки, войны).</summary>
public class RootNameTests
{
	[Theory]
	[InlineData("Военный Орден Ракгар", "Ракгар")]
	[InlineData("Ракгар", "Ракгар")]
	[InlineData("Вольная Община Бульун", "Бульун")]
	public void RootName_DropsIdeologyLabel(string full, string root)
	{
		Assert.Equal(root, IdeologyStateNames.ExtractBaseToponym(full, null));
	}
}

/// <summary>0.31.12: названия из игры без ярлыка строя.</summary>
public class GameRootNameTests
{
	[Theory]
	[InlineData("Непобедимый Каганат Пикуня", "Пикуня")]
	[InlineData("Военная Держава Громар", "Громар")]
	[InlineData("Военный Орден Ракгар", "Ракгар")]
	[InlineData("Вечный Каганат Белинск", "Белинск")]
	public void GameName_IsRootWithoutLabel(string full, string root)
	{
		Assert.Equal(root, IdeologyStateNames.ExtractBaseToponym(full, null));
	}

	[Fact]
	public void GenerateStateName_NonTribal_KeepsRootInsideLabel()
	{
		// 0.31.17: имя на карте — коренное имя вместе с ярлыком строя,
		// коренное имя остаётся основой и попадает в названия войн и союзов.
		string militarismName = IdeologyStateNames.GenerateStateName(IdeologyType.Militarism, "Ракгар", "Ракгар", 3, 2);
		string khaganateName = IdeologyStateNames.GenerateStateName(IdeologyType.Khaganate, "Пикуня", "Пикуня", 5, 1);
		Assert.NotEqual("Ракгар", militarismName);
		Assert.NotEqual("Пикуня", khaganateName);
		// Титул настоящий («Марка Ракгар», «Ханство Пикуня»), а имя государства в войнах
		// и союзах берётся без титула — «Ракгар», «Пикуня».
		Assert.Contains("Ракгар", militarismName);
		Assert.Contains("Пикуня", khaganateName);
		Assert.NotEmpty(StateNaming.LabelOf(IdeologyType.Militarism, militarismName));
		Assert.NotEmpty(StateNaming.LabelOf(IdeologyType.Khaganate, khaganateName));
	}
}
