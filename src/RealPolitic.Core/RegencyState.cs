namespace RealPolitic.Core;

/// <summary>
/// Состояние регентства для малолетнего правителя (возраст до 18 лет).
/// При активном регентстве действует штраф легитимности -25%, разрешены только оборонительные войны,
/// нарастает аристократическая угроза/нестабильность. Завершается при достижении 18 лет.
/// </summary>
public sealed class RegencyState
{
	/// <summary>Ключ локализации безымянного регентского совета (когда игра не назвала регента).</summary>
	public const string CouncilKey = "RP_REGENCY_COUNCIL";

	public bool IsActive { get; set; }

	public string RegentName { get; set; } = string.Empty;

	public int RegentActorId { get; set; }

	public int StartYear { get; set; }

	public int RulerTargetAge { get; set; } = 18;

	/// <summary>Идентификатор малолетнего наследника, отнятого от трона (действительное регентство, этап 23).</summary>
	public long HeirActorId { get; set; }

	/// <summary>Имя наследника — корона ему вернётся в 18 лет. Пусто — действительного свопа с регентом нет.</summary>
	public string HeirName { get; set; } = string.Empty;

	/// <summary>Флаг: хан коронации считается установленным, регент в игре физически на троне.</summary>
	public bool CrownSitsWithRegent { get; set; }

	public void Start(int year, string regentName, int regentActorId = 0)
	{
		IsActive = true;
		StartYear = year;
		RegentName = string.IsNullOrEmpty(regentName) ? CouncilKey : regentName;
		RegentActorId = regentActorId;
		RulerTargetAge = 18;
	}

	public void End()
	{
		IsActive = false;
		RegentName = string.Empty;
		RegentActorId = 0;
		// 0.23.11: полный сброс — иначе флаги «корона у регента» прилипли бы в сейве
		// после возвращения короны или смерти наследника.
		HeirActorId = 0;
		HeirName = string.Empty;
		CrownSitsWithRegent = false;
	}
}
