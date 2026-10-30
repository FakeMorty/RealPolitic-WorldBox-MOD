using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using Xunit;

namespace RealPolitic.GameTests;

/// <summary>
/// 0.37: файл настроек должен читаться загрузчиком NML.
/// В игре в лог падало «ModConfig file ... is empty or in invalid format!», и все
/// ползунки мода (включая годы КМУД) молча не применялись: у одной записи был
/// незнакомый загрузчику тип «BOOL» вместо «SWITCH».
/// </summary>
public sealed class ConfigFileTests
{
	private static readonly HashSet<string> KnownTypes = new HashSet<string>
	{
		"SWITCH", "INT_SLIDER", "SLIDER"
	};

	[Fact]
	public void DefaultConfig_HasOnlyKnownTypes()
	{
		foreach (KeyValuePair<string, JsonElement> item in Items())
		{
			Assert.True(item.Value.TryGetProperty("Type", out JsonElement type),
				"у записи настроек нет поля Type: " + item.Key);
			string name = type.GetString();
			Assert.True(KnownTypes.Contains(name),
				"загрузчик не знает тип «" + name + "» у записи " + item.Key
				+ " — файл настроек будет отвергнут целиком");
		}
	}

	[Fact]
	public void DefaultConfig_HasNoDuplicateIds()
	{
		var seen = new HashSet<string>();
		foreach (KeyValuePair<string, JsonElement> item in Items())
		{
			Assert.True(seen.Add(item.Key), "повторяющийся Id в настройках: " + item.Key);
		}
	}

	[Fact]
	public void KmudSettings_ArePresentAndSwitchIsBool()
	{
		Dictionary<string, JsonElement> all = All();
		foreach (string id in new[] { "KmudEnabled", "KmudYear", "KmudYears", "KmudIntervalYears", "KmudPactYears" })
		{
			Assert.True(all.ContainsKey(id), "в настройках нет записи " + id);
		}
		Assert.Equal("SWITCH", all["KmudEnabled"].GetProperty("Type").GetString());
		Assert.True(all["KmudEnabled"].GetProperty("BoolVal").GetBoolean());
		Assert.Equal(100, all["KmudIntervalYears"].GetProperty("IntVal").GetInt32());
	}

	private static Dictionary<string, JsonElement> All()
	{
		string path = Path.Combine(FindModRoot(), "default_config.json");
		Assert.True(File.Exists(path), "нет файла default_config.json");
		using JsonDocument doc = JsonDocument.Parse(File.ReadAllText(path));
		var result = new Dictionary<string, JsonElement>();
		foreach (JsonProperty tab in doc.RootElement.EnumerateObject())
		{
			foreach (JsonElement item in tab.Value.EnumerateArray())
			{
				result[item.GetProperty("Id").GetString()] = item.Clone();
			}
		}
		return result;
	}

	private static IEnumerable<KeyValuePair<string, JsonElement>> Items()
	{
		foreach (KeyValuePair<string, JsonElement> item in All())
		{
			yield return item;
		}
	}

	private static string FindModRoot()
	{
		DirectoryInfo dir = new DirectoryInfo(AppContext.BaseDirectory);
		while (dir != null && !File.Exists(Path.Combine(dir.FullName, "mod.json"))) dir = dir.Parent;
		Assert.NotNull(dir);
		return dir.FullName;
	}
}
