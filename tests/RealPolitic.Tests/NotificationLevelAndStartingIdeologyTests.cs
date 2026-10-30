using System;
using System.Collections.Generic;
using RealPolitic.Core;
using Xunit;

namespace RealPolitic.Tests;

/// <summary>
/// Регрессии 0.17.3: (1) уровень уведомлений 1 не пропускает рутинную экономику
/// (спад/бум — Trivial); (2) новые державы стартуют со случайного архаичного строя,
/// а не поголовно с монархии.
/// </summary>
public class NotificationLevelAndStartingIdeologyTests
{
	[Fact]
	public void NotifyLevel1_RoutineEconomy_NotQueued()
	{
		var bus = new PolEventBus();
		var center = new NotificationCenter(bus) { MinSeverity = Severity.Minor };

		bus.Publish(new PolEvent(PolEventType.EconomicCrisisStarted, "RP_EV_ECO_CRISIS_STARTED")
		{
			Severity = Severity.Trivial
		});
		bus.Publish(new PolEvent(PolEventType.EconomicBoom, "RP_EV_ECO_BOOM")
		{
			Severity = Severity.Trivial
		});

		Assert.Null(center.Dequeue());
	}

	[Fact]
	public void NotifyLevel0_RoutineEconomy_Queued()
	{
		var bus = new PolEventBus();
		var center = new NotificationCenter(bus) { MinSeverity = Severity.Trivial };

		bus.Publish(new PolEvent(PolEventType.EconomicCrisisStarted, "RP_EV_ECO_CRISIS_STARTED")
		{
			Severity = Severity.Trivial
		});

		Assert.NotNull(center.Dequeue());
	}

	[Fact]
	public void MuteUnmute_IsSymmetric()
	{
		var bus = new PolEventBus();
		var center = new NotificationCenter(bus) { MinSeverity = Severity.Trivial };

		center.Mute(PolEventType.WarStarted);
		bus.Publish(new PolEvent(PolEventType.WarStarted, "RP_EV_WAR_STARTED") { Severity = Severity.Major });
		Assert.Null(center.Dequeue());

		center.Unmute(PolEventType.WarStarted);
		bus.Publish(new PolEvent(PolEventType.WarStarted, "RP_EV_WAR_STARTED") { Severity = Severity.Major });
		Assert.NotNull(center.Dequeue());
	}

	private static readonly HashSet<IdeologyType> Archaic = new HashSet<IdeologyType>
	{
		IdeologyType.Tribalism,
		IdeologyType.Monarchy,
		IdeologyType.Khaganate,
		IdeologyType.FreeTerritory,
		IdeologyType.Theocracy,
		IdeologyType.Aristocracy,
		IdeologyType.Militarism,
		IdeologyType.MerchantRepublic
	};

	[Fact]
	public void RollArchaic_NeverReturnsModernIdeologies()
	{
		var rng = new Random(42);
		var seen = new HashSet<IdeologyType>();
		for (int i = 0; i < 3000; i++)
		{
			IdeologyType rolled = Ideologies.RollArchaic(rng);
			Assert.Contains(rolled, Archaic);
			seen.Add(rolled);
		}
		// Разброс: мир не стартует одним строем.
		Assert.True(seen.Count >= 5, $"слишком бедный разброс: {seen.Count}");
		Assert.Contains(IdeologyType.Tribalism, seen);
		Assert.Contains(IdeologyType.Monarchy, seen);
	}

	[Fact]
	public void RollStarting_UsesArchaicPool()
	{
		var rng = new Random(7);
		for (int i = 0; i < 3000; i++)
		{
			Assert.Contains(Ideologies.RollStarting(rng), Archaic);
		}
	}
}
