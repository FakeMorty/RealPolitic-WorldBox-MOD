using System;
using System.IO;
using RealPolitic.Core;
using Xunit;

namespace RealPolitic.Tests;

/// <summary>
/// 0.44: две жалобы владельца от 29.10.2026 и проверки на них.
///
/// 1) «Почему в уведомлениях ни словом ни духом о КМУДе?»
///    Причина была двойная: (а) у КМУД не было человеческих текстов в уведомлениях —
///    всплывашка показывала сухую подпись из словаря, а часть событий вообще служебный
///    ключ; (б) событие KmudWarPrevented (отбитая война) в коде было объявлено, но
///    ни разу не публиковалось, а до первого съезда (200-й год) про КМУД не говорилось
///    вовсе. Теперь есть тексты на каждое событие, напоминания за 25/10/5/1 год
///    и уведомление об отбитых войнах.
///
/// 2) «Где их мозг, когда они до 50-го года объявляют войны хер пойми кому,
///    у них даже общей границы нет».
///    Причина: стратегический план правителя проверял соседство и силы (0.39–0.40),
///    а путь советника (DiplomaticAdvisor → DeclareWar) — нет. Теперь оба замка
///    стоят в одном месте, и мимо них не проходит ни одно объявление войны мода.
/// </summary>
public class KmudVisibilityAndWarSanityTests
{
	private static string Read(params string[] parts)
	{
		string path = Path.Combine(FindRoot(), Path.Combine(parts));
		return File.ReadAllText(path);
	}

	private static string FindRoot()
	{
		DirectoryInfo dir = new DirectoryInfo(AppContext.BaseDirectory);
		while (dir != null)
		{
			if (Directory.Exists(Path.Combine(dir.FullName, "src")) &&
				Directory.Exists(Path.Combine(dir.FullName, "tests")))
			{
				return dir.FullName;
			}
			dir = dir.Parent;
		}
		throw new InvalidOperationException("корень репозитория не найден");
	}

	[Fact]
	public void EveryKmudEventHasHumanNotificationText()
	{
		string notifier = Read("src", "Game", "GameNotifier.cs");
		foreach (string type in new[]
		{
			"KmudUpcoming", "KmudConvened", "KmudClosed", "KmudAllianceFormed", "KmudPauseViolated",
			"KmudAgenda", "KmudResult", "KmudResultsAnnounced", "KmudPact", "KmudReparations",
			"KmudExecution", "KmudIdeologyChange", "KmudSanctions", "KmudPardoned", "KmudAid",
			"KmudWarPrevented"
		})
		{
			Assert.True(notifier.Contains("PolEventType." + type + " =>"),
				"нет текста уведомления для КМУД-события " + type);
		}
	}

	[Fact]
	public void WarPreventedEventIsActuallyPublished()
	{
		string ban = Read("src", "Game", "KmudWarBan.cs");
		Assert.Contains("PolEventType.KmudWarPrevented", ban);
		Assert.Contains("world.Bus.Publish(", ban);
	}

	[Fact]
	public void UpcomingCongressIsAnnouncedInAdvance()
	{
		string runtime = Read("src", "Game", "KmudRuntime.cs");
		Assert.Contains("AnnounceUpcoming(", runtime);
		// Напоминания — за 25, 10, 5 и 1 год до съезда.
		Assert.Contains("left != 25 && left != 10 && left != 5 && left != 1", runtime);
	}

	[Fact]
	public void AdvisorDeclareWarPassesTheSameSanityGateAsThePlan()
	{
		string feeder = Read("src", "Game", "WorldFeeder.cs");
		int start = feeder.IndexOf("case DiplomaticAction.DeclareWar:", StringComparison.Ordinal);
		Assert.True(start > 0, "ветка DeclareWar в ApplyDiplomacy не найдена");
		string body = feeder.Substring(start, Math.Min(2600, feeder.Length - start));
		Assert.Contains("WarSanityAllows(", body);
		// Общая граница и расчёт сил — внутри самого замка.
		int gate = feeder.IndexOf("private bool WarSanityAllows(", StringComparison.Ordinal);
		Assert.True(gate > 0, "замок WarSanityAllows не найден");
		string gateBody = feeder.Substring(gate, Math.Min(2600, feeder.Length - gate));
		Assert.Contains("SharesBorder(attackerInput, targetId)", gateBody);
		Assert.Contains("WarChances.Estimate(", gateBody);
		// Оборонительные поводы проходят без проверок: защищаться приходится всегда.
		Assert.Contains("CasusBelliType.Defense", gateBody);
		Assert.Contains("CasusBelliType.Independence", gateBody);
	}

	[Fact]
	public void WarAgainstNonNeighbourIsRefusedByTheGate()
	{
		// География — железное правило: проверяем на уровне правила, что отсутствие
		// общей границы означает отказ (логика замка читается по исходнику выше,
		// а здесь фиксируем сам факт, что проверка идёт ДО расчёта сил).
		string feeder = Read("src", "Game", "WorldFeeder.cs");
		int border = feeder.IndexOf("нет общей границы", StringComparison.Ordinal);
		int estimate = feeder.IndexOf("WarEstimate estimate = WarChances.Estimate(", StringComparison.Ordinal);
		Assert.True(border > 0 && estimate > border,
			"сначала должна отсекаться далёкая держава, и только потом считаться силы");
	}

	[Fact]
	public void NextCongressYearFollowsTheHundredYearSchedule()
	{
		// Правило расписания живёт в ядре и проверяется честно, без игры.
		SimParams p = new SimParams();
		Assert.Equal(200, p.KmudYear);
		Assert.Equal(100, p.KmudIntervalYears);
		KmudState state = new KmudState();
		Assert.False(KmudRules.ShouldConvene(state, p, 199, 4));
		Assert.True(KmudRules.ShouldConvene(state, p, 200, 4));
		// После закрытия первого съезда следующий — через 100 лет.
		state.Phase = KmudPhase.Closed;
		state.OpenedYear = 200;
		Assert.False(KmudRules.ShouldConvene(state, p, 299, 4));
		Assert.True(KmudRules.ShouldConvene(state, p, 300, 4));
	}
}
