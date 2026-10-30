using System.Collections.Generic;

namespace RealPolitic.Core;

/// <summary>
/// Отделение, которое ядро уже оформило, а игровой слой ещё не исполнил в WorldBox.
/// Ядро создаёт новое государство с временным идентификатором; игра создаёт настоящее
/// королевство и возвращает его id через <see cref="PoliticalWorld.RemapKingdomId"/>.
/// </summary>
public sealed class PendingSecession
{
	/// <summary>Государство, от которого отделились города.</summary>
	public int ParentKingdomId { get; set; }

	/// <summary>Временный id нового государства в ядре.</summary>
	public int ProvisionalKingdomId { get; set; }

	/// <summary>Города нового государства (первый — столица).</summary>
	public List<int> CityIds { get; set; } = new List<int>();

	/// <summary>Строй нового государства.</summary>
	public IdeologyType Ideology { get; set; }

	/// <summary>Причина раскола.</summary>
	public CivilWarCause Cause { get; set; }

	/// <summary>Имя вождя (настоящее имя юнита; может быть пустым).</summary>
	public string LeaderName { get; set; } = string.Empty;

	/// <summary>Год отделения.</summary>
	public int Year { get; set; }

	/// <summary>Это начало гражданской войны: метрополия и повстанцы должны воевать.</summary>
	public bool DeclareWar { get; set; } = true;
}

/// <summary>
/// 0.24: соединение, оформленное ядром и ждущее исполнения игровым слоем:
/// перевести города поглощённого в державу лидера, сменить флаг/имя, снять счёт государства.
/// </summary>
public sealed class PendingMerger
{
	public int LeaderId { get; set; }

	public int MemberId { get; set; }

	/// <summary>Имя поглощённой державы (для летописи, если соплеменники ещё могли видеть её имя).</summary>
	public string MemberName { get; set; } = string.Empty;
}

/// <summary>Тип операции, которую ядро просит исполнить игровой слой в мире WorldBox.</summary>
public enum WorldOpType
{
	/// <summary>Передать города из одного государства в другое (переворот: страна переходит к повстанцам).</summary>
	TransferCities = 1,

	/// <summary>Заключить мир между двумя государствами.</summary>
	MakePeace = 2
}

/// <summary>Операция над миром, отложенная до игрового слоя.</summary>
public sealed class PendingWorldOp
{
	public WorldOpType Type { get; set; }

	public int FromKingdomId { get; set; }

	public int ToKingdomId { get; set; }

	public List<int> CityIds { get; set; } = new List<int>();

	public int Year { get; set; }

	/// <summary>Исходное имя державы (возвращается при победе повстанцев в гражданской войне).</summary>
	public string OriginalKingdomName { get; set; } = string.Empty;

	/// <summary>ID предводителя повстанцев, становящегося главой объединённой державы.</summary>
	public int LeaderActorId { get; set; }
}
