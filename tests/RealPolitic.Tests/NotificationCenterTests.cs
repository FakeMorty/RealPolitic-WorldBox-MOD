using RealPolitic.Core;
using Xunit;

namespace RealPolitic.Tests;

public class NotificationCenterTests
{
	[Fact]
	public void Summary_IsEmittedEveryFiveCollapsedEvents()
	{
		var bus = new PolEventBus();
		var center = new NotificationCenter(bus);
		var seen = new System.Collections.Generic.List<PolEvent>();
		center.OnNotification += seen.Add;

		for (int i = 0; i < 5; i++)
		{
			bus.Publish(new PolEvent(PolEventType.NewCulture, "RP_EV_NEW_CULTURE")
			{
				Year = 1,
				Severity = Severity.Major,
				KingdomId = 1
			}.With("culture", "Северяне").With("culture_id", 7));
		}

		// Первое событие показывается сразу, пятое превращается в сводку.
		bool hasSummary = false;
		foreach (PolEvent evt in seen)
		{
			if (evt.LocKey.EndsWith("_SUMMARY"))
			{
				hasSummary = true;
				Assert.Equal("5", evt.Get("count"));
			}
		}
		Assert.True(hasSummary);
	}

	[Fact]
	public void MutedType_IsNotQueued()
	{
		var bus = new PolEventBus();
		var center = new NotificationCenter(bus);
		center.Mute(PolEventType.NewCulture);

		bus.Publish(new PolEvent(PolEventType.NewCulture, "RP_EV_NEW_CULTURE"));

		Assert.Equal(0, center.PendingCount);
	}

	[Fact]
	public void BelowMinSeverity_IsFiltered()
	{
		var bus = new PolEventBus();
		var center = new NotificationCenter(bus) { MinSeverity = Severity.Critical };

		bus.Publish(new PolEvent(PolEventType.NewCulture, "RP_EV_NEW_CULTURE") { Severity = Severity.Major });

		Assert.Equal(0, center.PendingCount);
	}

	[Fact]
	public void NotifyLevel2_FiltersOutCountryCrisesAndRoutinePolitics_AllowsGeopolitics()
	{
		var bus = new PolEventBus();
		var center = new NotificationCenter(bus) { MinSeverity = Severity.Major };

		// Внутренние кризисы стран, референдумы и договоры имеют верхний потолок Minor (уровень 1)
		// и не должны спамить на уровне 2 (Major):
		bus.Publish(new PolEvent(PolEventType.EconomicCrisisStarted, "RP_EV_ECO_CRISIS_STARTED") { Severity = Severity.Major });
		bus.Publish(new PolEvent(PolEventType.IdeologyThreatBroke, "RP_EV_THREAT_BROKE") { Severity = Severity.Major });
		bus.Publish(new PolEvent(PolEventType.WarReferendum, "RP_EV_WAR_REFERENDUM") { Severity = Severity.Major });
		bus.Publish(new PolEvent(PolEventType.TreatySigned, "RP_EV_TREATY_SIGNED") { Severity = Severity.Major });
		Assert.Equal(0, center.PendingCount);

		// Крупные геополитические события (уровень 2) и критические (уровень 3) проходят:
		bus.Publish(new PolEvent(PolEventType.AllianceFormed, "RP_EV_ALLIANCE_FORMED") { Severity = Severity.Major });
		bus.Publish(new PolEvent(PolEventType.CivilWarStarted, "RP_EV_CW_START") { Severity = Severity.Critical });
		Assert.Equal(2, center.PendingCount);
	}
}
