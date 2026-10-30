using System;
using System.Collections.Generic;
using System.IO;
using RealPolitic;
using RealPolitic.Core;
using Xunit;

namespace RealPolitic.GameTests;

public sealed class IconAssetsTests
{
	[Fact]
	public void EveryIdeologyAndUiButton_HasTheSame64PixelPngInBothIconFolders()
	{
		string root = FindModRoot();
		string[] keys = ModIcons.AllFileKeys();

		Assert.Equal(55, keys.Length);
		Assert.Equal(keys.Length, new HashSet<string>(keys).Count);
		Assert.Equal(16, Enum.GetValues(typeof(IdeologyType)).Length);
		Assert.Equal(7, ModIcons.UiKeys.Length);
		Assert.Equal(32, ModIcons.NotificationKeys.Length);

		foreach (string key in keys)
		{
			string runtimePath = Path.Combine(root, "icons", key + ".png");
			string embeddedPath = Path.Combine(root, "icons", "64", key + ".png");
			Assert.True(File.Exists(runtimePath), "Нет иконки для запасной загрузки: " + runtimePath);
			Assert.True(File.Exists(embeddedPath), "Нет иконки для встраивания в DLL: " + embeddedPath);

			byte[] runtime = File.ReadAllBytes(runtimePath);
			byte[] embedded = File.ReadAllBytes(embeddedPath);
			Assert.Equal(runtime, embedded);
			AssertPng64(runtime, key);
		}
	}

	[Fact]
	public void IconKeysAndSpritePaths_AreDistinct()
	{
		var ideologyPaths = new HashSet<string>();
		foreach (IdeologyType type in Enum.GetValues(typeof(IdeologyType)))
		{
			Assert.True(ideologyPaths.Add(Ideologies.IconKey(type)), "Повторяется путь иконки идеологии " + type);
		}

		var uiPaths = new HashSet<string>();
		foreach (string key in ModIcons.UiKeys)
		{
			Assert.True(uiPaths.Add(ModIcons.PathFor(key)), "Повторяется путь иконки кнопки " + key);
		}
		Assert.Equal("ui/Icons/rp_tab_civic_votes", ModIcons.PathFor(ModIcons.VotesKey));
		Assert.Equal("rp_tab_ideology_layer", ModIcons.WindowIconFor(ModIcons.IdeologyLayerKey));

		var notificationPaths = new HashSet<string>();
		foreach (string key in ModIcons.NotificationKeys)
		{
			Assert.True(notificationPaths.Add(ModIcons.PathFor(key)), "Повторяется путь иконки уведомления " + key);
			Assert.StartsWith("notify_", key);
		}
		Assert.Equal("ui/Icons/rp_notify_war", ModIcons.PathFor(ModIcons.NotifyWarKey));
	}

	[Fact]
	public void IconLoadingAndButtons_DoNotFallBackToTheOldMissingOrBorrowedIcons()
	{
		string root = FindModRoot();
		string sprites = Read(root, "src", "Game", "IdeologySprites.cs");
		string votes = Read(root, "src", "Game", "CivicVotesWindow.cs");
		string layer = Read(root, "src", "Game", "IdeologyLayerButton.cs");
		string patches = Read(root, "src", "Game", "Patches.cs");
		string gameProject = Read(root, "src", "Game", "RealPolitic.csproj");
		string releaseProject = Read(root, "tools", "release-build", "RealPolitic.Release.csproj");

		Assert.Contains("Enum.GetValues(typeof(IdeologyType))", sprites);
		Assert.Contains("ModIcons.RegisterUiIcons()", sprites);
		Assert.Contains("ModIcons.RegisterNotificationIcons()", sprites);
		Assert.Contains("Path.Combine(folder, \"icons\", \"64\", key + \".png\")", sprites);

		Assert.Contains("ModIcons.VotesKey,ModIcons.PathFor(ModIcons.VotesKey)", votes);
		Assert.Contains("ModIcons.ExpelKey", votes);
		Assert.Contains("ModIcons.AdmitKey", votes);
		Assert.DoesNotContain("\"tab_civic_votes\",\"ui/Icons/rp_tab_constitution\"", votes);

		Assert.Contains("ModIcons.GetSprite(ModIcons.IdeologyLayerKey)", layer);
		Assert.DoesNotContain("Ideologies.IconKey(IdeologyType.Monarchy)", layer);

		Assert.Contains("EnsureUiRowIcon(ModIcons.ConstitutionKey)", patches);
		Assert.Contains("EnsureUiRowIcon(ModIcons.FamilyKey)", patches);
		Assert.Contains("EnsureIcon(IdeologyType.Militarism)", patches);
		Assert.DoesNotContain(", null, localizeTitle: true);", patches);
		Assert.DoesNotContain(", null, localizeTitle: false);", patches);
		Assert.DoesNotContain("lines[i], string.Empty", patches);

		Assert.Contains("../../icons/64/*.png", gameProject);
		Assert.Contains("../../icons/64/*.png", releaseProject);
	}

	private static string Read(string root, params string[] parts)
	{
		string path = root;
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

	private static void AssertPng64(byte[] bytes, string key)
	{
		Assert.True(bytes.Length > 200, "Подозрительно пустая иконка " + key);
		byte[] signature = { 137, 80, 78, 71, 13, 10, 26, 10 };
		for (int i = 0; i < signature.Length; i++) Assert.Equal(signature[i], bytes[i]);
		Assert.Equal(64, ReadBigEndianInt32(bytes, 16));
		Assert.Equal(64, ReadBigEndianInt32(bytes, 20));
	}

	private static int ReadBigEndianInt32(byte[] bytes, int offset)
	{
		return (bytes[offset] << 24) | (bytes[offset + 1] << 16) | (bytes[offset + 2] << 8) | bytes[offset + 3];
	}
}
