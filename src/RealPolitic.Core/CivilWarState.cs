using System.Collections.Generic;

namespace RealPolitic.Core;

/// <summary>Чем закончилась гражданская война.</summary>
public enum CivilWarResolution
{
	/// <summary>Война идёт.</summary>
	None = 0,

	/// <summary>Победа законной власти: государство повстанцев уничтожено, города возвращены.</summary>
	LoyalistVictory = 1,

	/// <summary>Повстанцы взяли столицу (или держава пала): вся страна переходит под их власть — смена строя.</summary>
	RebelTakeover = 2,

	/// <summary>Отделение: война закончилась миром, государство повстанцев отстояло независимость.</summary>
	Secession = 3
}

/// <summary>
/// Состояние гражданской войны в государстве. Живёт в <see cref="KingdomPolitics.CivilWar"/>.
///
/// С версии 0.23 гражданская война — настоящая война в игре: мятежные города сразу
/// образуют отдельное государство повстанцев (<see cref="RebelKingdomId"/>), которое
/// объявляет войну метрополии. Исход решают реальные сражения WorldBox:
/// исчезло государство повстанцев — мятеж подавлен; повстанцы взяли столицу — переворот;
/// стороны заключили мир — отделение состоялось.
/// </summary>
public sealed class CivilWarState
{
	/// <summary>Причина мятежа.</summary>
	public CivilWarCause Cause { get; set; } = CivilWarCause.None;

	/// <summary>Год начала войны.</summary>
	public int StartedYear { get; set; }

	/// <summary>
	/// Настоящая война между метрополией и повстанцами уже была подтверждена игрой.
	/// Пока это false, отсутствие войны НЕ считается миром: игра обязана объявить войну
	/// (исполнитель повторяет попытку каждый год). Мир (отделение по белому миру) возможен
	/// только после того, как война была и закончилась.
	/// </summary>
	public bool WarEstablished { get; set; }

	/// <summary>Идеология, за которую воюют повстанцы.</summary>
	public IdeologyType RebelIdeology { get; set; } = IdeologyType.Monarchy;

	/// <summary>Имя предводителя повстанцев (настоящее имя юнита; пусто, если игра его не дала).</summary>
	public string RebelLeaderName { get; set; } = string.Empty;

	/// <summary>Предводитель повстанцев — женщина (для титулов).</summary>
	public bool RebelLeaderIsFemale { get; set; }

	/// <summary>ID юнита-предводителя (0 — неизвестен).</summary>
	public int RebelLeaderActorId { get; set; }

	/// <summary>ID города-столицы мятежа.</summary>
	public int RebelCapitalCityId { get; set; }

	/// <summary>Столица метрополии на момент начала войны: её захват повстанцами = переворот.</summary>
	public int ParentCapitalCityId { get; set; }

	/// <summary>Государство повстанцев (id ядра; после создания в игре — id королевства WorldBox). 0 — ещё не создано.</summary>
	public int RebelKingdomId { get; set; }

	/// <summary>Города, поднявшие мятеж в начале войны (для летописи и отката).</summary>
	public List<int> RebelCityIds { get; } = new List<int>();

	/// <summary>Сколько лет длится война (включая текущий).</summary>
	public int YearsActive(int year)
	{
		return year - StartedYear + 1;
	}

	/// <summary>Город входил в первоначальный мятеж?</summary>
	public bool HoldsCity(int cityId)
	{
		return RebelCityIds.Contains(cityId);
	}

	/// <summary>Добавить мятежный город (без дублей).</summary>
	public bool AddRebelCity(int cityId)
	{
		if (RebelCityIds.Contains(cityId))
		{
			return false;
		}
		RebelCityIds.Add(cityId);
		return true;
	}
}

/// <summary>Результат завершения гражданской войны (что именно должен применить мир).</summary>
public sealed class CivilWarOutcome
{
	public CivilWarResolution Resolution { get; set; } = CivilWarResolution.None;

	/// <summary>Идеология победивших повстанцев / нового государства.</summary>
	public IdeologyType RebelIdeology { get; set; }

	/// <summary>Причина мятежа (для текстов).</summary>
	public CivilWarCause Cause { get; set; }

	/// <summary>Государство повстанцев.</summary>
	public int RebelKingdomId { get; set; }

	/// <summary>Правитель свергнут повстанцами.</summary>
	public bool RulerDeposed { get; set; }

	/// <summary>Запись о смерти свергнутого правителя (если она была заведена).</summary>
	public DeathRecord? DeposedRulerDeath { get; set; }

	/// <summary>Сколько лет шла война.</summary>
	public int YearsActive { get; set; }
}
