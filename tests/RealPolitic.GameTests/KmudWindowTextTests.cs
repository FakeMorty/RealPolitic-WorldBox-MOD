using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using RealPolitic.Core;
using Xunit;

namespace RealPolitic.GameTests;

/// <summary>
/// 0.36: проверки «бумажной» части окна КМУД — все надписи, которые оно показывает,
/// должны существовать на обоих языках, а у каждого варианта ответа должна быть строка.
/// Окно рисуется рефлексией, тестами его не «прокликать», но тексты проверить можно.
/// </summary>
public sealed class KmudWindowTextTests
{
	private static readonly string[] WindowKeys =
	{
		"RP36_KMUD_WIN_TITLE",
		"RP36_KMUD_ROUND",
		"RP36_KMUD_OPEN_BUTTON",
		"RP36_KMUD_NAME",
		"RP36_KMUD_STAGE_LIVE",
		"RP36_KMUD_STAGE_DONE",
		"RP36_KMUD_STAGE_NONE",
		"RP36_KMUD_NOT_HELD",
		"RP36_KMUD_NOT_HELD_HINT",
		"RP36_KMUD_AGENDA_TITLE",
		"RP36_KMUD_CARD_CONFLICT",
		"RP36_KMUD_CARD_WORST",
		"RP36_KMUD_CARD_POOREST",
		"RP36_KMUD_NO_CONFLICT",
		"RP36_KMUD_CONFLICT_NOTE",
		"RP36_KMUD_NO_ACCUSED",
		"RP36_KMUD_NO_ACCUSED_NOTE",
		"RP36_KMUD_NO_POOREST",
		"RP36_KMUD_POOREST_NOTE",
		"RP36_KMUD_TURNOUT",
		"RP36_KMUD_TURNOUT_NOTE",
		"RP36_KMUD_VOTES",
		"RP36_KMUD_DECIDED",
		"RP36_KMUD_TIE",
		"RP36_KMUD_SEAL_TITLE",
		"RP36_KMUD_FOOTER",
		"RP36_KMUD_PACTS",
		"RP36_KMUD_SANCTIONS",
		"RP36_KMUD_MEMBERS"
	};

	[Fact]
	public void EveryWindowLabel_ExistsInBothLanguages()
	{
		Dictionary<string, string> ru = ReadLocale("ru.json");
		Dictionary<string, string> en = ReadLocale("en.json");
		foreach (string key in WindowKeys)
		{
			Assert.True(ru.ContainsKey(key), "нет русского текста: " + key);
			Assert.True(en.ContainsKey(key), "нет английского текста: " + key);
			Assert.False(string.IsNullOrWhiteSpace(ru[key]), "пустой русский текст: " + key);
			Assert.False(string.IsNullOrWhiteSpace(en[key]), "пустой английский текст: " + key);
		}
	}

	[Fact]
	public void EveryAnswerOption_HasTextOnBothLanguages()
	{
		Dictionary<string, string> ru = ReadLocale("ru.json");
		Dictionary<string, string> en = ReadLocale("en.json");
		foreach (KmudQuestion question in Enum.GetValues(typeof(KmudQuestion)))
		{
			int[] options = KmudOptions.For(question);
			Assert.NotEmpty(options);
			foreach (int option in options)
			{
				string key = KmudOptions.LocKey(question, option);
				Assert.NotEqual("RP32_OPT_UNKNOWN", key);
				Assert.True(ru.ContainsKey(key), "нет русского текста варианта: " + key);
				Assert.True(en.ContainsKey(key), "нет английского текста варианта: " + key);
			}
		}
	}

	[Fact]
	public void QuestionTitles_AreTranslated()
	{
		Dictionary<string, string> ru = ReadLocale("ru.json");
		Dictionary<string, string> en = ReadLocale("en.json");
		string[] keys = { "RP32_Q_CONFLICT", "RP32_Q_PEACE", "RP32_Q_PUNISH", "RP32_Q_AID" };
		foreach (string key in keys)
		{
			Assert.True(ru.ContainsKey(key), "нет русского вопроса: " + key);
			Assert.True(en.ContainsKey(key), "нет английского вопроса: " + key);
		}
	}

	[Fact]
	public void RussianAndEnglishLocales_HaveTheSameKeys()
	{
		Dictionary<string, string> ru = ReadLocale("ru.json");
		Dictionary<string, string> en = ReadLocale("en.json");
		var missingInEn = new List<string>();
		foreach (string key in ru.Keys)
		{
			if (!en.ContainsKey(key)) missingInEn.Add(key);
		}
		var missingInRu = new List<string>();
		foreach (string key in en.Keys)
		{
			if (!ru.ContainsKey(key)) missingInRu.Add(key);
		}
		Assert.Empty(missingInEn);
		Assert.Empty(missingInRu);
	}

	private static Dictionary<string, string> ReadLocale(string file)
	{
		string path = Path.Combine(FindModRoot(), "Locales", file);
		Assert.True(File.Exists(path), "нет файла локали: " + path);
		using JsonDocument doc = JsonDocument.Parse(File.ReadAllText(path));
		var result = new Dictionary<string, string>();
		foreach (JsonProperty property in doc.RootElement.EnumerateObject())
		{
			result[property.Name] = property.Value.ValueKind == JsonValueKind.String
				? property.Value.GetString() ?? string.Empty
				: string.Empty;
		}
		return result;
	}

	private static string FindModRoot()
	{
		DirectoryInfo dir = new DirectoryInfo(AppContext.BaseDirectory);
		while (dir != null && !File.Exists(Path.Combine(dir.FullName, "mod.json"))) dir = dir.Parent;
		Assert.NotNull(dir);
		return dir.FullName;
	}
}
