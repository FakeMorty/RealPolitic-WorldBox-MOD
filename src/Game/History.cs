using System.Collections.Generic;
using RealPolitic.Core;

namespace RealPolitic;

public static class History
{
	private static readonly List<PolEvent> _events = new List<PolEvent>();

	public static IReadOnlyList<PolEvent> Events => _events;

	public static void Add(PolEvent evt)
	{
		if (evt != null)
		{
			_events.Add(evt);
			while (_events.Count > 300)
			{
				_events.RemoveAt(0);
			}
		}
	}

	public static void Clear()
	{
		_events.Clear();
	}
}
