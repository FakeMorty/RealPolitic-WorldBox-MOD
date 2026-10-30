using System;
using System.Linq;
using RealPolitic.Core;
using Xunit;

namespace RealPolitic.GameTests;

/// <summary>
/// 0.50.1: страница «Национализм» в оглавлении мода.
///
/// Владелец 29.10.2026: механика не должна быть в секрете. Проверяем то, что можно
/// проверить без запуска игры: значок существует в наборе, а данные для страницы
/// ядро действительно отдаёт (обида, запрет въезда, санкции).
/// </summary>
public sealed class NationalismWindowTests
{
	[Fact]
	public void ThePageUsesARealGeneratedIcon()
	{
		Assert.Contains(NationalismWindow.IconKeyPublic, ModIcons.AllFileKeys());
	}

	[Fact]
	public void ThePageHasWhatToShowForAStateWithALostHomeland()
	{
		var world = new PoliticalWorld();
		world.SetYear(150);
		KingdomPolitics kingdom = world.AddKingdom(1, IdeologyType.Nationalism, 10);
		world.AddCity(200, 1);
		world.CityHomelands.Restore(new CityHomelandRecord
		{
			CityId = 200,
			OriginalKingdomId = 5,
			OriginalKingdomName = "Родина",
			OriginalIdeology = IdeologyType.Democracy,
			AnnexedYear = 120
		});
		NationalismState civic = world.NationalismOf(1);
		civic.Pressure = 62f;
		civic.OriginKingdomId = 5;
		civic.OriginName = "Родина";
		civic.Bond = HomesteadBond.Union;

		// Странице нужны ровно эти поля — проверяем, что они есть и не пустые.
		Assert.True(civic.Active);
		Assert.Equal("Родина", civic.OriginName);
		Assert.Equal(62f, civic.Pressure, 3);
		Assert.True(civic.Pressure > NationalismEngine.PowerThreshold);

		// И запрет въезда, если мир уже наказал.
		world.AddCitizenshipBan(1, "fox", "Лисы", 150, 300, true);
		Assert.True(world.IsEntryBanned(1, "fox", 150));
		Assert.Single(world.CitizenshipBans);
	}

	[Fact]
	public void ThePageShowsSanctionsOnlyWhileTheyLast()
	{
		var world = new PoliticalWorld();
		world.SetYear(200);
		world.AddKingdom(1, IdeologyType.Nationalism, 10);
		world.Kmud.Sanctions.Add(new KmudSanction { KingdomId = 1, UntilYear = 220, Supporters = 4 });

		Assert.True(world.Kmud.Sanctions[0].Active(200));
		Assert.False(world.Kmud.Sanctions[0].Active(230));
	}
}
