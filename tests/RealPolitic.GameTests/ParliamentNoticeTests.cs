using System;
using System.Reflection;
using RealPolitic;
using RealPolitic.Core;
using Xunit;

namespace RealPolitic.GameTests;

/// <summary>
/// 0.48.4: катка 29.10.2026 — у «Степняков» правящая партия прыгала
/// Совет старейшин → Партия граждан → Торгово-купеческая → Партия граждан,
/// и каждая смена объявлялась: четыре уведомления за шесть лет.
///
/// Тест стережёт три правила:
///   1) мелкое дрожание мест — не новость;
///   2) смена партии — новость, но не чаще раза в 10 лет;
///   3) возврат к партии, о которой уже писали недавно, — не новость (пинг-понг).
/// </summary>
public sealed class ParliamentNoticeTests
{
	private static bool Worth(int reportedIdeology, int reportedYear, int rulingIdeology, int year,
		System.Collections.Generic.Dictionary<int, int> memory, int seats = 50, int reportedSeats = 50)
	{
		Type runtime = typeof(ParliamentRuntime);
		Type noticeType = runtime.GetNestedType("Notice", BindingFlags.NonPublic);
		Assert.NotNull(noticeType);
		object notice = Activator.CreateInstance(noticeType!)!;
		noticeType!.GetField("RulingIdeology")!.SetValue(notice, reportedIdeology);
		noticeType.GetField("Year")!.SetValue(notice, reportedYear);
		noticeType.GetField("RulingSeats")!.SetValue(notice, reportedSeats);
		noticeType.GetField("LastSeats")!.SetValue(notice, 50);
		object memoryField = noticeType.GetField("ReportedIdeologies")!.GetValue(notice)!;
		foreach (var kv in memory)
		{
			memoryField.GetType().GetProperty("Item")!.SetValue(memoryField, kv.Value, new object[] { kv.Key });
		}
		PoliticalParty ruling = new PoliticalParty
		{
			Ideology = (IdeologyType)rulingIdeology,
			Name = "Партия",
			SeatsPercent = seats,
			IsRuling = true
		};
		MethodInfo worth = runtime.GetMethod("WorthNoticing", BindingFlags.Static | BindingFlags.NonPublic)!;
		return (bool)worth.Invoke(null, new object[] { notice, ruling, year })!;
	}

	[Fact]
	public void PartyChangeIsNewsButNotEveryOtherYear()
	{
		var memory = new System.Collections.Generic.Dictionary<int, int>();
		// Только что объявили о партии 1 в 200-м году; в 202-м пришла партия 2.
		Assert.False(Worth(1, 200, 2, 202, memory, seats: 60));
		// Прошло 10 лет — уже новость.
		Assert.True(Worth(1, 200, 2, 210, memory, seats: 60));
	}

	[Fact]
	public void SamePartyIsSilentWithinCooldown()
	{
		var memory = new System.Collections.Generic.Dictionary<int, int>();
		Assert.False(Worth(3, 300, 3, 305, memory, seats: 70));
		// Прошло больше 20 лет и места выросли заметно — вот это новость.
		Assert.True(Worth(3, 300, 3, 322, memory, seats: 70));
	}

	[Fact]
	public void ReturnToRecentPartyIsNotNews()
	{
		// О партии 2 писали в 200-м, вернулись в 215-м — это пинг-понг, не новость.
		var memory = new System.Collections.Generic.Dictionary<int, int> { [2] = 200 };
		Assert.False(Worth(1, 201, 2, 215, memory, seats: 60));
	}
}
