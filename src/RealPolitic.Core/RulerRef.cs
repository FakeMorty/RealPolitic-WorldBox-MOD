namespace RealPolitic.Core;

public sealed class RulerRef
{
	public int ActorId { get; set; }

	public string Name { get; set; } = string.Empty;

	public int KingdomId { get; set; }

	public int SinceYear { get; set; }

	/// <summary>Правитель — женщина (для титулов «королева», «жрица» и т. п.).</summary>
	public bool IsFemale { get; set; }

	/// <summary>Возраст правителя (обновляется при каждой синхронизации с игрой).</summary>
	public int Age { get; set; }

	/// <summary>Клан / семья правителя (из системы кланов WorldBox).</summary>
	public string ClanName { get; set; } = string.Empty;

	/// <summary>ID клана в WorldBox.</summary>
	public long ClanId { get; set; }

	/// <summary>
	/// 0.52: народ правителя. Владелец 30.10.2026: «в России к власти может прийти
	/// эскимос», но конституция может это запретить.
	/// </summary>
	public string RaceId { get; set; } = string.Empty;

	/// <summary>
	/// Этот «правитель» — регент, сидящий на троне за малолетнего наследника.
	/// Ядро обязано не считать его ни преемником, ни новой династией: настоящая
	/// корона числится за наследником в RegencyState (трагедия дома Вевович, 0.23.11).
	/// </summary>
	public bool IsRegent { get; set; }
}
