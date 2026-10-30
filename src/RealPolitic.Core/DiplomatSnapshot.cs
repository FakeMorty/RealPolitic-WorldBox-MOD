namespace RealPolitic.Core;

public struct DiplomatSnapshot
{
	public int KingdomId;

	public IdeologyType Ideology;

	public ReformStance Stance;

	public float Legitimacy;

	public int Population;

	public int Warriors;

	public bool Starving;

	public bool AtWar;

	public int ReligionId;

	public bool UnderRegency;

	public string RaceId;

	/// <summary>0.24: статья X «О ратной повинности» — правит оценку военной мощи.
	/// null в ручных снапшотах тестов: множитель не применяется (нейтрально).</summary>
	public LevyLaw? Levy;

	/// <summary>0.24: обязательность грамоты для множителей статей.</summary>
	public bool BindingConstitution;

	/// <summary>0.24: язык державы (первоклассная мета-сущность; 0 = неизвестен) — для статьи XV.</summary>
	public int LanguageId { get; set; }

	/// <summary>0.24: общая ли граница с лидером (для статьи XV, значения Neighbors).
	/// null = данных нет, ядро не блокирует — решает верхний слой.</summary>
	public bool? AdjacentToLeader { get; set; }
}
