using System;
using System.Collections.Generic;
using System.IO;
using RealPolitic.Core;
using Xunit;

namespace RealPolitic.GameTests;

/// <summary>
/// 0.48: проверки по итогам 1000-летнего мира владельца.
///
/// Что случилось в его игре 29.10.2026 (летопись 1252–1347): летопись забили
/// 94 одинаковые строки «выборы в парламент» — по одной в год, хотя правящая
/// партия не менялась. Ещё журнал игры сообщал «версия 0.45.0», хотя стояла 0.47.1:
/// номер версии был записан руками и отстал от mod.json.
///
/// Эти тесты ловят ровно такие вещи, чтобы они не вернулись.
/// </summary>
public class Run1000YearsRegressionTests
{
	[Fact]
	public void VersionInCodeMatchesManifest()
	{
		string root = FindModRoot();
		string manifest = File.ReadAllText(Path.Combine(root, "mod.json"));
		int at = manifest.IndexOf("\"version\"", StringComparison.Ordinal);
		Assert.True(at >= 0, "в mod.json нет поля version");
		int quote = manifest.IndexOf('"', manifest.IndexOf(':', at) + 1);
		int end = manifest.IndexOf('"', quote + 1);
		string version = manifest.Substring(quote + 1, end - quote - 1);

		// Номер версии в коде обязан совпадать с манифестом: иначе в журнале игры
		// игрок видит одно, а установлено другое (так и было с «0.45.0»).
		Assert.Equal(version, RealPoliticMod.Version);
	}

	[Fact]
	public void ParliamentNoticesAreRareAndNotAnnualNoise()
	{
		// 20 лет тишины на державу и порог в 10 мест — так в летописи не будет
		// «выборы» каждый год при неизменной партии.
		Assert.True(ParliamentRuntime.NoticeCooldownYears >= 10);
		Assert.True(ParliamentRuntime.SeatsChangeToNotify >= 5);

		PoliticalWorld world = new PoliticalWorld();
		KingdomPolitics state = world.AddKingdom(1, IdeologyType.Democracy, 0);
		state.Parties.Add(new PoliticalParty { Ideology = IdeologyType.Aristocracy, NameKey = "RP_PARTY_NOBLES", SeatsPercent = 85, IsRuling = true });
		state.Parties.Add(new PoliticalParty { Ideology = IdeologyType.Democracy, NameKey = "RP_PARTY_DEM", SeatsPercent = 15 });

		List<PolEvent> published = new List<PolEvent>();
		world.Bus.Subscribe(published.Add);

		// 30 лет подряд места дрожат как в жизни: 85, 84, 87, 86, 82...
		int[] seats = { 85, 84, 87, 86, 82, 83, 87, 86, 84, 81 };
		int year = 1250;
		for (int round = 0; round < 6; round++)
		{
			for (int i = 0; i < seats.Length; i++)
			{
				year++;
				state.Parties[0].SeatsPercent = seats[i];
				state.Parties[1].SeatsPercent = 100 - seats[i];
				ParliamentRuntime.AnnualTick(world, year);
			}
		}

		int notices = 0;
		for (int i = 0; i < published.Count; i++)
		{
			if (published[i].Type == PolEventType.ParliamentElected)
			{
				notices++;
			}
		}
		// Раньше здесь было бы 60 сообщений. Теперь — ни одного: партия та же,
		// места не ушли дальше десяти.
		Assert.Equal(0, notices);
	}

	[Fact]
	public void ChangeOfRulingPartyIsStillAnnounced()
	{
		PoliticalWorld world = new PoliticalWorld();
		KingdomPolitics state = world.AddKingdom(2, IdeologyType.Democracy, 0);
		state.Parties.Add(new PoliticalParty { Ideology = IdeologyType.Aristocracy, NameKey = "RP_PARTY_NOBLES", SeatsPercent = 60, IsRuling = true });
		state.Parties.Add(new PoliticalParty { Ideology = IdeologyType.Democracy, NameKey = "RP_PARTY_DEM", SeatsPercent = 40 });

		List<PolEvent> published = new List<PolEvent>();
		world.Bus.Subscribe(published.Add);

		ParliamentRuntime.AnnualTick(world, 1300); // запомнили картину
		state.Parties[0].SeatsPercent = 30;
		state.Parties[0].IsRuling = false;
		state.Parties[1].SeatsPercent = 70;
		state.Parties[1].IsRuling = true;
		ParliamentRuntime.AnnualTick(world, 1310);

		Assert.Contains(published, e => e.Type == PolEventType.ParliamentElected);
	}

	private static string FindModRoot()
	{
		string dir = AppContext.BaseDirectory;
		for (int i = 0; i < 8 && dir != null; i++)
		{
			if (File.Exists(Path.Combine(dir, "mod.json")))
			{
				return dir;
			}
			dir = Path.GetDirectoryName(dir);
		}
		throw new InvalidOperationException("не найден корень мода (mod.json)");
	}
}
