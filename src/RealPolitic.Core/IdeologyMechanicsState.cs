namespace RealPolitic.Core;

/// <summary>
/// Живое состояние модовых механик одного государства: заряд ульты и шкала угрозы.
/// Хранится в KingdomPolitics.Mechanics.
/// </summary>
public sealed class IdeologyMechanicsState
{
	// ---------------------------------------------------------------- ульта

	/// <summary>Год последнего применения ульты (int.MinValue — ни разу).</summary>
	public int LastUsedYear { get; set; } = int.MinValue;

	/// <summary>Год, когда закончится действие эффекта (0 — не действует).</summary>
	public int ActiveUntilYear { get; set; }

	/// <summary>Цель ульты, если она направлена на другое государство (брак, договор, поход).</summary>
	public int? TargetKingdomId { get; set; }

	/// <summary>Ульта сейчас действует.</summary>
	public bool IsActive(int year)
	{
		return year < ActiveUntilYear;
	}

	/// <summary>Сколько лет осталось до конца перезарядки (0 — готова).</summary>
	public int CooldownLeft(IdeologyPowerDef def, int year)
	{
		if (LastUsedYear == int.MinValue)
		{
			return 0;
		}
		int ready = LastUsedYear + def.Cooldown;
		return ready > year ? ready - year : 0;
	}

	// ---------------------------------------------------------------- угроза

	/// <summary>Шкала угрозы 0..100.</summary>
	public float ThreatLevel { get; set; }

	/// <summary>Сколько раз угроза уже срывалась (для летописи и роста ставок).</summary>
	public int ThreatBreaks { get; set; }

	/// <summary>Год последнего срыва.</summary>
	public int LastBreakYear { get; set; } = int.MinValue;

	/// <summary>0.23.3: год, за который уже объявлено «кризис миновал» (чтобы не повторять).</summary>
	public int RecoveryAnnouncedFor { get; set; } = int.MinValue;

	/// <summary>Государство приходит в себя после срыва — угроза не копится.</summary>
	public bool IsRecovering(IdeologyThreatDef def, int year)
	{
		if (LastBreakYear == int.MinValue)
		{
			return false;
		}
		return year < LastBreakYear + def.Recovery;
	}

	/// <summary>Сбрасывает всё — при смене идеологии государства механики начинаются заново.</summary>
	public void Reset()
	{
		LastUsedYear = int.MinValue;
		ActiveUntilYear = 0;
		TargetKingdomId = null;
		ThreatLevel = 0f;
		ThreatBreaks = 0;
		LastBreakYear = int.MinValue;
		RecoveryAnnouncedFor = int.MinValue;
	}
}
