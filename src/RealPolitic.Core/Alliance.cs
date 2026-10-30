using System.Collections.Generic;

namespace RealPolitic.Core;

/// <summary>Этап 21: ванильный альянс игры (WorldBox создаёт их сюжетами «создать/вступить/разрушить»).
/// Ядро получает снимок альянсов от игрового слоя и следит за изменениями состава.</summary>
public sealed class AllianceRecord
{
    /// <summary>Native stable ID; zero only for a legacy saved book awaiting a real snapshot.</summary>
	public int Id;

	public string Name = string.Empty;

	public int LeaderId;

	public readonly List<int> Members = new List<int>();
}

/// <summary>Книга ванильных альянсов: последний снимок, сверенный с игрой.</summary>
public sealed class AllianceBook
{
	private readonly List<AllianceRecord> _all = new List<AllianceRecord>();

	public IReadOnlyList<AllianceRecord> All => _all;

	public int Count => _all.Count;

	/// <summary>Альянс, в котором состоит держава (или null).</summary>
	public AllianceRecord? Of(int kingdomId)
	{
		for (int i = 0; i < _all.Count; i++)
		{
			if (_all[i].Members.Contains(kingdomId))
			{
				return _all[i];
			}
		}
		return null;
	}

	public AllianceRecord? ByName(string name)
	{
		if (string.IsNullOrEmpty(name))
		{
			return null;
		}
		for (int i = 0; i < _all.Count; i++)
		{
			if (string.Equals(_all[i].Name, name, System.StringComparison.Ordinal))
			{
				return _all[i];
			}
		}
		return null;
	}

	public void Replace(List<AllianceRecord> records)
	{
		_all.Clear();
		if (records != null)
		{
			_all.AddRange(records);
		}
	}

	public void Clear()
	{
		_all.Clear();
	}
}
