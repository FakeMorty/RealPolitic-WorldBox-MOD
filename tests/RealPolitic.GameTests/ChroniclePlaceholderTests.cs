using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Text.RegularExpressions;
using Xunit;

namespace RealPolitic.GameTests;

/// <summary>
/// 0.38: в летописи вместо «Союз оформлен в игре: {allied}» стояла голая скобка — шаблон
/// в локали содержал параметр, который рендер не подставлял. Этот тест ловит такие случаи:
/// каждый {параметр} из строк летописи обязан подставляться в Chronicle.cs.
/// </summary>
public sealed class ChroniclePlaceholderTests
{
	[Fact]
	public void EveryPlaceholderInChronicleStrings_IsSubstituted()
	{
		string chronicle = File.ReadAllText(Path.Combine(FindModRoot(), "src", "Game", "Chronicle.cs"));
		var broken = new List<string>();
		foreach (string locale in new[] { "ru.json", "en.json" })
		{
			using JsonDocument doc = JsonDocument.Parse(
				File.ReadAllText(Path.Combine(FindModRoot(), "Locales", locale)));
			foreach (JsonProperty property in doc.RootElement.EnumerateObject())
			{
				string key = property.Name;
				if (!key.StartsWith("RP", StringComparison.Ordinal) || !key.Contains("CHRON")) continue;
				if (property.Value.ValueKind != JsonValueKind.String) continue;
				string text = property.Value.GetString() ?? string.Empty;
				int index = chronicle.IndexOf("\"" + key + "\"", StringComparison.Ordinal);
				if (index < 0) continue;
				int from = Math.Max(0, index - 800);
				int to = Math.Min(chronicle.Length, index + 3000);
				string window = chronicle.Substring(from, to - from);
				foreach (Match match in Regex.Matches(text, @"\{(\w+)\}"))
				{
					string placeholder = match.Groups[1].Value;
					if (!window.Contains("\"{" + placeholder + "}\"") && !window.Contains("{" + placeholder + "}"))
					{
						broken.Add(locale + " " + key + " → {" + placeholder + "}");
					}
				}
			}
		}
		Assert.Empty(broken);
	}

	private static string FindModRoot()
	{
		DirectoryInfo dir = new DirectoryInfo(AppContext.BaseDirectory);
		while (dir != null && !File.Exists(Path.Combine(dir.FullName, "mod.json"))) dir = dir.Parent;
		Assert.NotNull(dir);
		return dir.FullName;
	}
}
