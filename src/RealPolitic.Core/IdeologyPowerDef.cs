namespace RealPolitic.Core;

/// <summary>
/// Определение «ульты» идеологии — активного действия, доступного только ей.
/// Правитель-ИИ применяет её сам, когда выполнены условия (см. IdeologyPowers.CanUse).
/// </summary>
public sealed class IdeologyPowerDef
{
	/// <summary>Ключ локализации названия: RP_POWER_*.</summary>
	public string NameKey = string.Empty;

	/// <summary>Ключ локализации описания эффекта: RP_POWER_*_DESC.</summary>
	public string DescKey = string.Empty;

	/// <summary>Сколько лет действует эффект после применения.</summary>
	public int Duration;

	/// <summary>Перезарядка в годах (отсчитывается от МОМЕНТА ПРИМЕНЕНИЯ).</summary>
	public int Cooldown;

	/// <summary>Минимальный возраст государства в годах, до которого ульта недоступна.</summary>
	public int MinAge;

	/// <summary>Минимальная легитимность, нужная чтобы правитель решился.</summary>
	public float MinLegitimacy;
}

/// <summary>
/// Определение уникальной угрозы идеологии — процесса, который копится и однажды срывается.
/// </summary>
public sealed class IdeologyThreatDef
{
	/// <summary>Ключ локализации названия: RP_THREAT_*.</summary>
	public string NameKey = string.Empty;

	/// <summary>Ключ локализации описания: RP_THREAT_*_DESC.</summary>
	public string DescKey = string.Empty;

	/// <summary>Значение шкалы, при котором угроза срывается (шкала всегда 0..100).</summary>
	public float Threshold = 100f;

	/// <summary>Сколько лет государство «приходит в себя» после срыва (угроза не копится).</summary>
	public int Recovery = 5;
}
