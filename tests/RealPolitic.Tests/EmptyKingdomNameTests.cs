using System.Collections.Generic;
using RealPolitic.Core;
using Xunit;

namespace RealPolitic.Tests;

/// <summary>
/// 0.48: в летописи не должно быть пустых имён держав.
///
/// В мире владельца (летопись 29.10.2026, год 1251) дважды появилось
/// «Держава  покинула альянс «Лига государств»» — с пустым именем, потому что
/// держава к тому моменту уже перестала существовать. Выглядит как ошибка мода.
/// </summary>
public class EmptyKingdomNameTests
{
	[Fact]
	public void LeavingAllianceDoesNotPrintAnEmptyRealmName()
	{
		PoliticalWorld world = new PoliticalWorld();
		world.AddKingdom(1, IdeologyType.Monarchy, 0);
		world.AddKingdom(2, IdeologyType.Monarchy, 0);
		// Держава 2 известна политике, но имени у неё нет — как у вымершей.
		List<AllianceRecord> first = new List<AllianceRecord>
		{
			new AllianceRecord { Id = 10, Name = "Лига государств", LeaderId = 1, Members = { 1, 2 } }
		};
		world.SetAllianceSnapshot(first);
		List<AllianceRecord> second = new List<AllianceRecord>
		{
			new AllianceRecord { Id = 10, Name = "Лига государств", LeaderId = 1, Members = { 1 } }
		};
		List<PolEvent> published = new List<PolEvent>();
		world.Bus.Subscribe(published.Add);
		world.SetAllianceSnapshot(second);

		PolEvent left = published.Find(e => e.Type == PolEventType.AllianceMemberLeft);
		Assert.NotNull(left);
		Assert.False(string.IsNullOrWhiteSpace(left.Get("member")));
	}
}
