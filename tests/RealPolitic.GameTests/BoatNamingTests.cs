using System;
using System.Collections.Generic;
using RealPolitic.Core;
using Xunit;

namespace RealPolitic.Tests;

/// <summary>
/// 0.23.12: корабли получают национальные имена в «кавычках» — по городам, державе
/// и известным людям. Имя «Пичина» (корабль) не должно совпадать с королевой Пичиной
/// без кавычек.
/// </summary>
public class BoatNamingTests
{
	[Fact]
	public void IsBoatUnit_DetectsRawAssetName()
	{
		Actor boat = new Actor { id = 1, name = "boat_transport_human", age = 1, data = new ActorData { id = 1 } };
		Actor human = new Actor { id = 2, name = "Пичина", age = 30, data = new ActorData { id = 2 } };

		Assert.True(GameRefs.IsBoatUnit(boat));
		Assert.False(GameRefs.IsBoatUnit(human));
	}

	[Fact]
	public void QuoteBoatName_WrapsInGuillemets_Once()
	{
		Assert.Equal("«Ныски»", GameRefs.QuoteBoatName("Ныски"));
		Assert.Equal("«Ныски»", GameRefs.QuoteBoatName("«Ныски»"));
		Assert.Equal(string.Empty, GameRefs.QuoteBoatName(""));
	}

	[Fact]
	public void PickBoatNameFromPool_Quotes_AndAvoidsCurrentName()
	{
		// Пул — национальные имена: города, держава, правитель.
		List<string> pool = new List<string> { "Кевике", "Свово", "Пичина" };
		Random rng = new Random(7);

		// Корабль «Пичина» уже назван — из пула исключается имя «Пичина».
		for (int i = 0; i < 40; i++)
		{
			string name = GameRefs.PickBoatNameFromPool(pool, "Пичина", rng);
			Assert.NotEqual("Пичина", name);
			Assert.NotEqual("«Пичина»", name);
			Assert.StartsWith("«", name);
			Assert.EndsWith("»", name);
		}
	}

	[Fact]
	public void PickBoatNameFromPool_EmptyPool_ReturnsEmpty()
	{
		Assert.Equal(string.Empty, GameRefs.PickBoatNameFromPool(new List<string>(), "Мур", new Random(1)));
		Assert.Equal(string.Empty, GameRefs.PickBoatNameFromPool(null, "Мур", new Random(1)));
	}
}
