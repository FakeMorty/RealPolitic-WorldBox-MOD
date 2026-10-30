using System;
using System.Collections.Generic;

namespace RealPolitic.Core;

public sealed class DeathRegistry
{
	private readonly List<DeathRecord> _records = new List<DeathRecord>();

	private readonly Dictionary<int, DeathRecord> _byActor = new Dictionary<int, DeathRecord>();

	public int Capacity { get; set; } = 5000;

	public IReadOnlyList<DeathRecord> All => _records;

	public event Action<DeathRecord>? OnDeath;

	public DeathRecord Record(int actorId, string actorName, DeathCause cause, int year, int? killerId = null, string killerName = "", string killerTitle = "", int? tileId = null, int? kingdomId = null, string cityName = "", bool wasRuler = false, bool wasChosen = false, int age = 0, string note = "", bool wasFemale = false, int? reignYears = null, Action<DeathRecord>? enrich = null, bool notify = true)
	{
		DeathRecord deathRecord = new DeathRecord
		{
			ActorId = actorId,
			ActorName = (actorName ?? string.Empty),
			Cause = cause,
			Year = year,
			KillerId = killerId,
			KillerName = (killerName ?? string.Empty),
			KillerTitle = (killerTitle ?? string.Empty),
			TileId = tileId,
			KingdomId = kingdomId,
			CityName = (cityName ?? string.Empty),
			WasRuler = wasRuler,
			WasChosen = wasChosen,
			Age = age,
			Note = (note ?? string.Empty),
			WasFemale = wasFemale,
			ReignYears = (wasRuler && reignYears.HasValue) ? reignYears : null
		};
		// 0.23.4: подробности об убийце (вид, пол, возраст, держава) заполняются ДО рассылки OnDeath.
		// Раньше летопись получала запись до их заполнения — каждый убийца выглядел как «зверь: Имя (самец)».
		enrich?.Invoke(deathRecord);
		_records.Add(deathRecord);
		_byActor[actorId] = deathRecord;
		while (_records.Count > Capacity && Capacity > 0)
		{
			DeathRecord deathRecord2 = _records[0];
			_records.RemoveAt(0);
			if (_byActor.TryGetValue(deathRecord2.ActorId, out var value) && value == deathRecord2)
			{
				_byActor.Remove(deathRecord2.ActorId);
			}
		}
		if (notify) this.OnDeath?.Invoke(deathRecord);
		return deathRecord;
	}

	public DeathRecord? Get(int actorId)
	{
		if (!_byActor.TryGetValue(actorId, out var value))
		{
			return null;
		}
		return value;
	}

	public List<DeathRecord> Rulers()
	{
		List<DeathRecord> list = new List<DeathRecord>();
		for (int num = _records.Count - 1; num >= 0; num--)
		{
			if (_records[num].WasRuler)
			{
				list.Add(_records[num]);
			}
		}
		return list;
	}

	public List<DeathRecord> Chosen()
	{
		List<DeathRecord> list = new List<DeathRecord>();
		for (int num = _records.Count - 1; num >= 0; num--)
		{
			if (_records[num].WasChosen)
			{
				list.Add(_records[num]);
			}
		}
		return list;
	}

	public List<DeathRecord> InKingdom(int kingdomId, int limit = 25)
	{
		List<DeathRecord> list = new List<DeathRecord>();
		int num = _records.Count - 1;
		while (num >= 0 && list.Count < limit)
		{
			if (_records[num].KingdomId == kingdomId)
			{
				list.Add(_records[num]);
			}
			num--;
		}
		return list;
	}

	public int CountOf(DeathCause cause)
	{
		int num = 0;
		for (int i = 0; i < _records.Count; i++)
		{
			if (_records[i].Cause == cause)
			{
				num++;
			}
		}
		return num;
	}

	public void Clear()
	{
		_records.Clear();
		_byActor.Clear();
	}
}
