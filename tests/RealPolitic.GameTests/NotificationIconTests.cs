using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using RealPolitic.Core;
using Xunit;

namespace RealPolitic.GameTests;

public sealed class NotificationIconTests
{
	public NotificationIconTests()
	{
		ResetWorldLogStubs();
	}

	[Theory]
	[InlineData(PolEventType.WarStarted, "ui/Icons/rp_notify_war")]
	[InlineData(PolEventType.CivilWarStarted, "ui/Icons/rp_notify_war")]
	[InlineData(PolEventType.WarEnded, "ui/Icons/rp_notify_peace")]
	[InlineData(PolEventType.WarReferendum, "ui/Icons/rp_notify_vote")]
	[InlineData(PolEventType.DemocraticElection, "ui/Icons/rp_notify_vote")]
	[InlineData(PolEventType.ConstitutionEdited, "ui/Icons/rp_notify_law")]
	[InlineData(PolEventType.PeaceSettlement, "ui/Icons/rp_notify_peace")]
	[InlineData(PolEventType.Succession, "ui/Icons/rp_notify_dynasty")]
	[InlineData(PolEventType.RulerDeath, "ui/Icons/rp_notify_dynasty")]
	[InlineData(PolEventType.UnionMemberJoined, "ui/Icons/rp_notify_join")]
	[InlineData(PolEventType.UnionMemberLeft, "ui/Icons/rp_notify_leave")]
	[InlineData(PolEventType.BorderOpened, "ui/Icons/rp_notify_join")]
	[InlineData(PolEventType.BorderClosed, "ui/Icons/rp_notify_leave")]
	[InlineData(PolEventType.TradeRouteOpened, "ui/Icons/rp_notify_trade")]
	[InlineData(PolEventType.EconomicCrisisStarted, "ui/Icons/rp_notify_trade")]
	[InlineData(PolEventType.EspionageSuccess, "ui/Icons/rp_notify_spy")]
	[InlineData(PolEventType.EthnonationalistTension, "ui/Icons/rp_notify_war")]
	[InlineData(PolEventType.EraChanged, "ui/Icons/rp_notify_map")]
	public void GeneratedIconFollowsTheMeaningOfTheEvent(PolEventType type, string expectedPath)
	{
		var evt = new PolEvent(type, "TEST");

		Assert.Equal(expectedPath, NotificationIcons.PathFor(evt));
	}

	[Fact]
	public void GeneratedIdeologyIconIsUsedForItsRegime()
	{
		var evt = new PolEvent(PolEventType.KingdomAdoptedIdeology, "TEST")
			.With("new", "RP_IDEOLOGY_DEMOCRACY");

		Assert.Equal("ui/Icons/rp_notify_ideology_democracy", NotificationIcons.PathFor(evt));
	}

	[Fact]
	public void UnknownIdeologyWithoutItsOwnGeneratedIconFallsBackToTheMap()
	{
		var evt = new PolEvent(PolEventType.KingdomAdoptedIdeology, "TEST")
			.With("new", "RP_IDEOLOGY_UNKNOWN_STATE");

		Assert.Equal("ui/Icons/rp_notify_map", NotificationIcons.PathFor(evt));
	}

	[Fact]
	public void IdeologyEventsUseOnlyTheirGeneratedNotificationIcon()
	{
		var evt = new PolEvent(PolEventType.KingdomAdoptedIdeology, "TEST")
			.With("new", "RP_IDEOLOGY_EMPIRE");

		string path = NotificationIcons.PathFor(evt, IdeologyType.Empire);

		Assert.Equal("ui/Icons/rp_notify_ideology_empire", path);
		Assert.DoesNotContain("rp_ideology_", path);
	}

	[Theory]
	[InlineData("RP_GOV26_ADMISSION", "ui/Icons/rp_notify_join")]
	[InlineData("RP_GOV26_EXPULSION", "ui/Icons/rp_notify_leave")]
	public void AllianceMotionDistinguishesAdmissionFromExpulsion(string action, string expectedPath)
	{
		var evt = new PolEvent(PolEventType.CivicAllianceMotion, "TEST").With("action", action);

		Assert.Equal(expectedPath, NotificationIcons.PathFor(evt));
	}

	[Fact]
	public void EveryEventTypeUsesOnlyTheGeneratedNotificationPack()
	{
		var known = new HashSet<string>(StringComparer.Ordinal);
		foreach (string key in ModIcons.NotificationKeys)
		{
			known.Add(ModIcons.PathFor(key));
		}
		Assert.Equal(32, known.Count);

		foreach (PolEventType type in Enum.GetValues(typeof(PolEventType)))
		{
			string path = NotificationIcons.PathFor(new PolEvent(type, "TEST"));
			Assert.Contains(path, known);
			Assert.DoesNotContain("rp_ideology_", path);
			Assert.DoesNotContain("rp_tab_", path);
			Assert.DoesNotContain("rp_button_", path);
		}
	}

	[Theory]
	[InlineData("ui/Icons/rp_notify_war", "notify_war")]
	[InlineData("ui/Icons/rp_notify_dynasty", "notify_dynasty")]
	[InlineData("ui/Icons/iconPlay", "")]
	public void FileKeyIsRecoveredFromTheSpriteCachePath(string path, string expectedKey)
	{
		Assert.Equal(expectedKey, NotificationIcons.FileKeyForPath(path));
	}

	[Fact]
	public void GameMessagesCreatesOneCustomWorldLogAssetPerGeneratedIconAndKeepsAutoTesterTextBehaviour()
	{
		const string iconPath = "ui/Icons/rp_notify_war";

		GameMessages.Major("Началась война", iconPath);
		GameMessages.Major("Вторая война с той же иконкой", iconPath);

		Assert.Equal(2, WorldLogMessageExtensions.added.Count);
		Assert.All(WorldLogMessageExtensions.added, message => Assert.Equal("rp_notify_war", message.asset_id));

		WorldLogAsset asset = AssetManager.world_log_library.get("rp_notify_war");
		Assert.NotNull(asset);
		Assert.Equal(iconPath, asset.path_icon);
		Assert.Same(WorldLogLibrary.auto_tester.text_replacer, asset.text_replacer);
		Assert.Equal("Началась война", asset.text_replacer(WorldLogMessageExtensions.added[0], "шаблон игры"));
		Assert.Equal("ui/Icons/iconPlay", WorldLogLibrary.auto_tester.path_icon);
		Assert.Equal(2, AssetManager.world_log_library.list.Count); // auto_tester + один rp_notify_war
	}

	[Fact]
	public void DifferentGeneratedIconsGetDifferentWorldLogAssets()
	{
		GameMessages.Major("Война", "ui/Icons/rp_notify_war");
		GameMessages.Major("Наследование", "ui/Icons/rp_notify_dynasty");

		Assert.Equal("rp_notify_war", WorldLogMessageExtensions.added[0].asset_id);
		Assert.Equal("rp_notify_dynasty", WorldLogMessageExtensions.added[1].asset_id);
		Assert.Equal("ui/Icons/rp_notify_dynasty", AssetManager.world_log_library.get("rp_notify_dynasty").path_icon);
	}

	[Fact]
	public void EmptyIconPathFallsBackToOriginalAutoTesterAsset()
	{
		GameMessages.Minor("Обычное сообщение", null);

		WorldLogMessage message = Assert.Single(WorldLogMessageExtensions.added);
		Assert.Equal("auto_tester", message.asset_id);
	}

	[Fact]
	public void GameNotifierPassesTheGeneratedPoliticalMapToTheNativeMessage()
	{
		var bus = new PolEventBus();
		var notifier = new GameNotifier(bus, new NotificationCenter(bus));
		var evt = new PolEvent(PolEventType.KingdomAdoptedIdeology, "TEST")
		{
			Severity = Severity.Major
		}.With("kingdom", "Добрвецкая Империя")
		 .With("old", "RP_IDEOLOGY_TRIBALISM")
		 .With("new", "RP_IDEOLOGY_EMPIRE");

		MethodInfo show = typeof(GameNotifier).GetMethod("Show", BindingFlags.Instance | BindingFlags.NonPublic)!;
		show.Invoke(notifier, new object[] { evt });

		WorldLogMessage message = Assert.Single(WorldLogMessageExtensions.added);
		Assert.Equal("rp_notify_ideology_empire", message.asset_id);
		Assert.Equal("ui/Icons/rp_notify_ideology_empire", AssetManager.world_log_library.get(message.asset_id).path_icon);
	}

	private static void ResetWorldLogStubs()
	{
		AssetManager.world_log_library = new WorldLogLibrary();
		WorldLogMessageExtensions.added.Clear();

		FieldInfo cache = typeof(GameMessages).GetField("IconAssets", BindingFlags.Static | BindingFlags.NonPublic)!;
		((IDictionary)cache.GetValue(null)!).Clear();
	}
}
