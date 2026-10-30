using System;
using System.IO;
using System.Text.RegularExpressions;
using Xunit;

namespace RealPolitic.GameTests;

/// <summary>
/// 0.37: сторожа против двух ошибок, которые нашлись только в настоящей игре
/// (лог от 28.10.2026, конференция 200-го года).
///
/// 1) Повестка съезда собиралась ДО созыва, а созыв её обнуляет — в итоге конференция
///    закрывалась с пустой повесткой и «исполнено решений: 0»: ни пакта, ни помощи.
/// 2) События КМУД уходили в летопись без года — в файле мира стояло «Год 0».
/// </summary>
public sealed class KmudOrderRegressionTests
{
	[Fact]
	public void AgendaIsBuiltAfterTheConferenceOpens()
	{
		string source = Read("src", "Game", "KmudRuntime.cs");
		int convene = source.IndexOf("KmudRules.Convene(", StringComparison.Ordinal);
		int agenda = source.IndexOf("BuildAgenda(world, year);", StringComparison.Ordinal);
		Assert.True(convene > 0, "в KmudRuntime не найден созыв конференции");
		Assert.True(agenda > convene,
			"повестка должна собираться ПОСЛЕ KmudRules.Convene: созыв чистит повестку, " +
			"иначе съезд закрывается с пустой повесткой и решения не исполняются");
	}

	[Fact]
	public void CoreConveneClearsTheAgenda_SoOrderMatters()
	{
		var state = new RealPolitic.Core.KmudState();
		state.Agenda.AggressorId = 3;
		state.Agenda.DefenderId = 4;
		state.Agenda.WorstId = 3;
		state.Agenda.PoorestId = 4;
		RealPolitic.Core.KmudRules.Convene(state, new RealPolitic.Core.SimParams(), 200, 8, 1);
		Assert.False(state.Agenda.HasConflict);
		Assert.Equal(0, state.Agenda.WorstId);
		Assert.Equal(0, state.Agenda.PoorestId);
	}

	[Fact]
	public void WarsAreEndedAfterTheAgendaIsTaken()
	{
		string source = Read("src", "Game", "KmudRuntime.cs");
		int convene = source.IndexOf("KmudRules.Convene(", StringComparison.Ordinal);
		int agenda = source.IndexOf("BuildAgenda(world, year);", StringComparison.Ordinal);
		int endAll = source.IndexOf("DiplomacyExecutor.EndAllWars()", StringComparison.Ordinal);
		Assert.True(convene > 0 && agenda > convene, "повестка должна идти после созыва");
		Assert.True(endAll > agenda,
			"войны нужно гасить ПОСЛЕ сбора повестки: иначе мод сначала сам гасит все войны, " +
			"а потом ищет идущий конфликт и не находит его (в логе было «идут сейчас: 0» при 7 войнах)");
	}

	[Fact]
	public void ConferenceAlliance_BypassesTheCivicVote()
	{
		string executor = Read("src", "Game", "DiplomacyExecutor.cs");
		Assert.Contains("public static bool ForceAllianceDirect(", executor);
		string alliance = Read("src", "Game", "KmudAlliance.cs");
		Assert.Contains("DiplomacyExecutor.ForceAllianceDirect(", alliance);
		Assert.DoesNotContain("DiplomacyExecutor.ForceAlliance(", alliance);
		string effects = Read("src", "Game", "KmudEffects.cs");
		Assert.Contains("DiplomacyExecutor.ForceAllianceDirect(", effects);
	}

	[Fact]
	public void EveryKmudEventCarriesTheYear()
	{
		foreach (string file in new[] { "KmudRuntime.cs", "KmudEffects.cs", "KmudAlliance.cs" })
		{
			string source = Read("src", "Game", file);
			foreach (Match match in Regex.Matches(source, @"new PolEvent\(PolEventType\.Kmud\w+,\s*""[^""]+""\)\s*(\{ Year = [\w.]+ \})?"))
			{
				Assert.True(match.Groups[1].Success,
					"событие без года (в летописи будет «Год 0»): " + match.Value.Replace("\n", " "));
			}
		}
		string core = Read("src", "RealPolitic.Core", "Kmud.cs");
		Assert.Contains("PolEventType.KmudConvened, \"RP32_EV_KMUD_CONVENED\") { Year = year }", core);
		Assert.Contains("PolEventType.KmudClosed, \"RP32_EV_KMUD_CLOSED\") { Year = year }", core);
	}

	[Fact]
	public void DissolveClosesEveryAllianceNamedKmd()
	{
		string source = Read("src", "Game", "KmudAlliance.cs");
		Assert.Contains("AlliancesNamedKmd(", source);
		Assert.Contains("ReleaseForeignAlliances(", source);
		Assert.Contains("world.Kmud.AllianceFormed = false;", source);
	}

	private static string Read(params string[] parts)
	{
		string path = FindModRoot();
		foreach (string part in parts) path = Path.Combine(path, part);
		return File.ReadAllText(path);
	}

	private static string FindModRoot()
	{
		DirectoryInfo dir = new DirectoryInfo(AppContext.BaseDirectory);
		while (dir != null && !File.Exists(Path.Combine(dir.FullName, "mod.json"))) dir = dir.Parent;
		Assert.NotNull(dir);
		return dir.FullName;
	}
}
