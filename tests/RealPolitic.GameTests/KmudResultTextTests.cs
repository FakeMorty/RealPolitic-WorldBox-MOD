using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using RealPolitic.Core;
using Xunit;

namespace RealPolitic.GameTests;

/// <summary>
/// 0.48.2 — заказ владельца: «И где результат?»
///
/// В окне «История» было четыре строки «КМУД: результат по вопросу» и ни слова
/// о том, что решили. Причина простая: в тексте уведомления стояла голая фраза
/// без подстановок — сами решения (вопрос и выбранный вариант) в неё не попадали.
/// Они лежали в полях события, но окно «История» показывает только текст.
///
/// Теперь в тексте есть вопрос, решение и число голосов. Тест стережёт, чтобы
/// подстановки не потерялись снова.
/// </summary>
public sealed class KmudResultTextTests
{
	[Fact]
	public void NotificationText_HasQuestionAnswerAndVotes()
	{
		Dictionary<string, string> ru = ReadLocale("ru.json");
		string text = ru["RP32_EV_KMUD_RESULT"];
		Assert.Contains("{question}", text);
		Assert.Contains("{answer}", text);
		Assert.Contains("{voters}", text);
		Assert.Contains("{number}", text);
	}

	[Fact]
	public void BothLanguages_AgreeOnPlaceholders()
	{
		Dictionary<string, string> ru = ReadLocale("ru.json");
		Dictionary<string, string> en = ReadLocale("en.json");
		string[] marks = { "{question}", "{answer}", "{voters}", "{number}" };
		foreach (string mark in marks)
		{
			Assert.Contains(mark, ru["RP32_EV_KMUD_RESULT"]);
			Assert.Contains(mark, en["RP32_EV_KMUD_RESULT"]);
		}
	}

	[Fact]
	public void EveryQuestionAndOption_HasReadableText()
	{
		Dictionary<string, string> ru = ReadLocale("ru.json");
		foreach (KmudQuestion question in Enum.GetValues(typeof(KmudQuestion)))
		{
			foreach (int option in KmudOptions.For(question))
			{
				string key = KmudOptions.LocKey(question, option);
				Assert.True(ru.TryGetValue(key, out string? value), "нет русского текста варианта: " + key);
				Assert.False(string.IsNullOrWhiteSpace(value), "пустой текст варианта: " + key);
			}
		}
	}

	[Fact]
	public void ResultLine_ReadsLikeASentence()
	{
		Dictionary<string, string> ru = ReadLocale("ru.json");
		// Так событие собирается в KmudRuntime: вопрос, вариант и голоса.
		PolEvent evt = new PolEvent(PolEventType.KmudResult, "RP32_EV_KMUD_RESULT")
			.With("question", "Время полного мира")
			.With("answer", "10 лет мира")
			.With("voters", 807)
			.With("number", 2);
		string line = evt.Render(ru["RP32_EV_KMUD_RESULT"]);
		Assert.Contains("вопрос 2", line);
		Assert.Contains("Время полного мира", line);
		Assert.Contains("10 лет мира", line);
		Assert.Contains("807", line);
		Assert.DoesNotContain("{", line);
	}

	[Fact]
	public void SummaryLine_ShowsVotersAndPeaceYears()
	{
		Dictionary<string, string> ru = ReadLocale("ru.json");
		Assert.Contains("{total_voters}", ru["RP32_EV_KMUD_RESULTS"]);
		Assert.Contains("{peace_years}", ru["RP32_EV_KMUD_RESULTS"]);
		// Отдельная строка на случай «мир не продлевали» — иначе вышло бы «продлён на 0 лет».
		Assert.True(ru.ContainsKey("RP32_EV_KMUD_RESULTS_NOWAR"));
		Assert.Contains("{total_voters}", ru["RP32_EV_KMUD_RESULTS_NOWAR"]);
		Assert.DoesNotContain("{peace_years}", ru["RP32_EV_KMUD_RESULTS_NOWAR"]);
	}

	private static Dictionary<string, string> ReadLocale(string file)
	{
		string path = Path.Combine(FindModRoot(), "Locales", file);
		Assert.True(File.Exists(path), "нет файла локали: " + path);
		using JsonDocument doc = JsonDocument.Parse(File.ReadAllText(path));
		Dictionary<string, string> result = new Dictionary<string, string>();
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
		DirectoryInfo? dir = new DirectoryInfo(AppContext.BaseDirectory);
		while (dir != null && !File.Exists(Path.Combine(dir.FullName, "mod.json")))
		{
			dir = dir.Parent;
		}
		Assert.NotNull(dir);
		return dir!.FullName;
	}
}
