using System;
using System.Collections.Generic;

namespace RealPolitic.Core;

/// <summary>Причина обиды одной державы на другую (этап 20).</summary>
public enum GrievanceReason
{
	War = 1,
	CitySeized = 2,
	TreatyBroken = 3,
	Espionage = 4,
	Conquest = 5,
	BrokenUnion = 6
}

/// <summary>Тип казуса белли — формального повода к войне (этап 20).</summary>
public enum CasusBelliType
{
	/// <summary>Завоевание без повода: дорого для легитимности и поддержки.</summary>
	Conquest = 0,

	/// <summary>Месть за обиды: накопленные претензии, прошлые войны.</summary>
	Revenge = 1,

	/// <summary>Ответ на нарушенный договор.</summary>
	BrokenTreaty = 2,

	/// <summary>Возврат собственных земель (территориальные претензии).</summary>
	Reconquest = 3,

	/// <summary>Освобождение от враждебного строя.</summary>
	IdeologicalLiberation = 4,

	/// <summary>Оборона союзника по пакту или коалиции: народная война, почти без издержек.</summary>
	Defense = 5,

	/// <summary>Война вассала за независимость.</summary>
	Independence = 6
}

/// <summary>Политическая цена объявления войны с данным казус белли.</summary>
public sealed class CasusBelliDef
{
	public CasusBelliType Type { get; set; }

	/// <summary>Ключ локализации названия повода.</summary>
	public string LocKey { get; set; }

	/// <summary>Штраф легитимности при объявлении.</summary>
	public float LegitimacyCost { get; set; }

	/// <summary>Штраф поддержки подданных.</summary>
	public float SupportCost { get; set; }

	/// <summary>Прирост недовольства.</summary>
	public float DissentCost { get; set; }

	/// <summary>Прирост внутренней угрозы.</summary>
	public float ThreatGain { get; set; }

	/// <summary>Обида, которую война оставляет у жертвы (топливо будущих претензий).</summary>
	public float VictimGrievance { get; set; }
}

/// <summary>Таблица казус белли: единственная точка баланса поводов к войне.</summary>
public static class CasusBelliDefs
{
	public static CasusBelliDef Get(CasusBelliType type)
	{
		switch (type)
		{
			case CasusBelliType.Revenge:
				return new CasusBelliDef { Type = type, LocKey = "RP_CB_REVENGE", LegitimacyCost = 6f, SupportCost = 4f, DissentCost = 0.04f, ThreatGain = 8f, VictimGrievance = 35f };
			case CasusBelliType.BrokenTreaty:
				return new CasusBelliDef { Type = type, LocKey = "RP_CB_BROKEN_TREATY", LegitimacyCost = 3f, SupportCost = 2f, DissentCost = 0.02f, ThreatGain = 5f, VictimGrievance = 25f };
			case CasusBelliType.Reconquest:
				return new CasusBelliDef { Type = type, LocKey = "RP_CB_RECONQUEST", LegitimacyCost = 2f, SupportCost = 2f, DissentCost = 0.02f, ThreatGain = 4f, VictimGrievance = 25f };
			case CasusBelliType.IdeologicalLiberation:
				return new CasusBelliDef { Type = type, LocKey = "RP_CB_IDEOLOGY", LegitimacyCost = 4f, SupportCost = 3f, DissentCost = 0.03f, ThreatGain = 6f, VictimGrievance = 30f };
			case CasusBelliType.Defense:
				return new CasusBelliDef { Type = type, LocKey = "RP_CB_DEFENSE", LegitimacyCost = 0f, SupportCost = -5f, DissentCost = -0.02f, ThreatGain = 0f, VictimGrievance = 20f };
			case CasusBelliType.Independence:
				return new CasusBelliDef { Type = type, LocKey = "RP_CB_INDEPENDENCE", LegitimacyCost = 0f, SupportCost = -8f, DissentCost = -0.03f, ThreatGain = 0f, VictimGrievance = 30f };
			default:
				return new CasusBelliDef { Type = CasusBelliType.Conquest, LocKey = "RP_CB_CONQUEST", LegitimacyCost = 12f, SupportCost = 10f, DissentCost = 0.10f, ThreatGain = 15f, VictimGrievance = 50f };
		}
	}

	/// <summary>Лучший доступный повод атакующего против защитника. Возвращает Conquest, если особых поводов нет.</summary>
	/// <param name="grievance">Обида атакующего на защитника (0..100).</param>
	/// <param name="claims">Территориальные претензии атакующего (из игровой дипломатии).</param>
	/// <param name="affinity">Идеологическое сродство (−1..1).</param>
	/// <param name="treatyBroken">Державой-защитником недавно был нарушен договор с атакующим.</param>
	public static CasusBelliType Best(float grievance, int claims, float affinity, bool treatyBroken)
	{
		if (treatyBroken)
		{
			return CasusBelliType.BrokenTreaty;
		}
		if (claims > 0)
		{
			return CasusBelliType.Reconquest;
		}
		if (grievance >= 25f)
		{
			return CasusBelliType.Revenge;
		}
		if (affinity <= -0.3f)
		{
			return CasusBelliType.IdeologicalLiberation;
		}
		return CasusBelliType.Conquest;
	}
}

/// <summary>Накопленные обиды: направленная пара «кто на кого обижён».</summary>
public sealed class GrievanceBook
{
	private readonly Dictionary<long, float> _values = new Dictionary<long, float>();
	private readonly Dictionary<long, GrievanceReason> _lastReasons = new Dictionary<long, GrievanceReason>();

	private static long PairKey(int from, int to)
	{
		return ((long)from << 32) | (uint)to;
	}

	/// <summary>Обида державы <c>from</c> на державу <c>to</c> (0..100).</summary>
	public float Get(int from, int to)
	{
		return _values.TryGetValue(PairKey(from, to), out float v) ? v : 0f;
	}

	/// <summary>Последняя причина обиды (для выбора казус белли).</summary>
	public GrievanceReason LastReason(int from, int to)
	{
		return _lastReasons.TryGetValue(PairKey(from, to), out var r) ? r : GrievanceReason.War;
	}

	public void Add(int from, int to, GrievanceReason reason, float amount)
	{
		if (from == to || amount <= 0f)
		{
			return;
		}
		long key = PairKey(from, to);
		_values[key] = Math.Min(100f, Get(from, to) + amount);
		_lastReasons[key] = reason;
	}

	public void Set(int from, int to, float value)
	{
		long key = PairKey(from, to);
		if (value <= 0f)
		{
			_values.Remove(key);
			_lastReasons.Remove(key);
			return;
		}
		_values[key] = Math.Min(100f, value);
	}

	/// <summary>Годовое затухание обид: время лечит, но медленно.</summary>
	public void Decay(float amountPerYear)
	{
		// Mono/.NET 4.8 бросает «Collection was modified» даже при перезаписи
		// существующего ключа во время foreach (в .NET 8 это разрешено — поэтому
		// тесты не ловили). Сначала собираем изменения, потом применяем.
		List<long> dead = new List<long>();
		List<KeyValuePair<long, float>> snapshot = new List<KeyValuePair<long, float>>(_values);
		for (int i = 0; i < snapshot.Count; i++)
		{
			float next = snapshot[i].Value - amountPerYear;
			if (next <= 0f)
			{
				dead.Add(snapshot[i].Key);
			}
			else
			{
				_values[snapshot[i].Key] = next;
			}
		}
		for (int i = 0; i < dead.Count; i++)
		{
			_values.Remove(dead[i]);
			_lastReasons.Remove(dead[i]);
		}
	}

	/// <summary>Все непустые обиды — для сохранения.</summary>
	public IReadOnlyDictionary<long, float> Raw => _values;

	public void Clear()
	{
		_values.Clear();
		_lastReasons.Clear();
	}
}
