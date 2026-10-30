namespace RealPolitic.Core;

/// <summary>
/// Причина гражданской войны. Причина определяет, кто поднимает мятеж,
/// какую идеологию несут повстанцы и к чему они стремятся — захватить власть
/// или отделиться.
/// </summary>
public enum CivilWarCause
{
	None = 0,

	/// <summary>Идеологический раскол: крупная группа подданных иного строя берётся за оружие.</summary>
	IdeologicalSplit = 1,

	/// <summary>Кризис престолонаследия: часть знати оспаривает законность правителя.</summary>
	SuccessionCrisis = 2,

	/// <summary>Заговор знати в период регентства малолетнего правителя.</summary>
	RegencyCrisis = 3,

	/// <summary>Восстание угнетаемого меньшинства (этнонационализм, чистки и ассимиляция).</summary>
	EthnicOppression = 4,

	/// <summary>Тирания: низкая легитимность власти при высоком недовольстве подданных.</summary>
	Tyranny = 5,

	/// <summary>Оспаривание итогов выборов (демократия: проигравшая сторона не признаёт результат).</summary>
	ElectionDispute = 6,

	/// <summary>Перенапряжение: дальние города перестают слушаться столицы.</summary>
	Overexpansion = 7,

	/// <summary>Голодный бунт.</summary>
	Famine = 8,

	/// <summary>Экономический кризис: обнищание и дефолт казны выводят подданных на улицы.</summary>
	EconomicCrisis = 9
}

/// <summary>Справочник причин гражданской войны: локализация и поведенческие коэффициенты.</summary>
public static class CivilWarCauses
{
	/// <summary>Ключ локализации названия причины (для уведомлений и летописи).</summary>
	public static string LocKey(CivilWarCause cause)
	{
		return cause switch
		{
			CivilWarCause.IdeologicalSplit => "RP_CW_CAUSE_IDEOLOGICAL",
			CivilWarCause.SuccessionCrisis => "RP_CW_CAUSE_SUCCESSION",
			CivilWarCause.RegencyCrisis => "RP_CW_CAUSE_REGENCY",
			CivilWarCause.EthnicOppression => "RP_CW_CAUSE_ETHNIC",
			CivilWarCause.Tyranny => "RP_CW_CAUSE_TYRANNY",
			CivilWarCause.ElectionDispute => "RP_CW_CAUSE_ELECTION",
			CivilWarCause.Overexpansion => "RP_CW_CAUSE_OVEREXPANSION",
			CivilWarCause.Famine => "RP_CW_CAUSE_FAMINE",
			CivilWarCause.EconomicCrisis => "RP_CW_CAUSE_ECONOMIC",
			_ => "RP_CW_CAUSE_UNKNOWN"
		};
	}

	/// <summary>
	/// Склонность причины к отделению (а не к захвату власти).
	/// Этнические и окраинные мятежи хотят независимости; династические и голодные — смены власти.
	/// Значение добавляется к доле повстанцев, необходимой для исхода «отделение».
	/// </summary>
	public static float SecessionBias(CivilWarCause cause)
	{
		return cause switch
		{
			CivilWarCause.EthnicOppression => -0.12f,
			CivilWarCause.Overexpansion => -0.1f,
			CivilWarCause.IdeologicalSplit => -0.04f,
			CivilWarCause.SuccessionCrisis => 0.1f,
			CivilWarCause.RegencyCrisis => 0.12f,
			CivilWarCause.ElectionDispute => 0.08f,
			_ => 0f
		};
	}

	/// <summary>
	/// Военная организация мятежников (множитель силы повстанцев в сражениях).
	/// Заговор знати и спор о престолонаследии дают опытных воинов, голодный бунт — нет.
	/// </summary>
	public static float MilitarySkill(CivilWarCause cause)
	{
		return cause switch
		{
			CivilWarCause.RegencyCrisis => 0.3f,
			CivilWarCause.SuccessionCrisis => 0.25f,
			CivilWarCause.EthnicOppression => 0.15f,
			CivilWarCause.IdeologicalSplit => 0.1f,
			CivilWarCause.ElectionDispute => 0.05f,
			CivilWarCause.Overexpansion => 0f,
			CivilWarCause.Tyranny => -0.05f,
			CivilWarCause.Famine => -0.15f,
			CivilWarCause.EconomicCrisis => -0.1f,
			_ => 0f
		};
	}

	/// <summary>Цель повстанцев — смена строя (в отличие от чистого отделения).</summary>
	public static bool AimsAtRegimeChange(CivilWarCause cause)
	{
		return cause switch
		{
			CivilWarCause.SuccessionCrisis => true,
			CivilWarCause.RegencyCrisis => true,
			CivilWarCause.Tyranny => true,
			CivilWarCause.Famine => true,
			CivilWarCause.EconomicCrisis => true,
			CivilWarCause.ElectionDispute => true,
			_ => false
		};
	}
}
