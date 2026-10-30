using System;
using System.Collections.Generic;

namespace RealPolitic.Core;

public sealed class PolEventBus
{
	private readonly List<Action<PolEvent>> _handlers = new List<Action<PolEvent>>();

	private readonly List<PolEvent> _log = new List<PolEvent>();

	public int LogCapacity { get; set; } = 400;

	public Action<Exception>? OnHandlerError { get; set; }

	public IReadOnlyList<PolEvent> Log => _log;

	public void Subscribe(Action<PolEvent> handler)
	{
		if (handler != null)
		{
			_handlers.Add(handler);
		}
	}

	public void Unsubscribe(Action<PolEvent> handler)
	{
		_handlers.Remove(handler);
	}

	public void Publish(PolEvent evt)
	{
		if (evt == null)
		{
			return;
		}
		_log.Add(evt);
		while (_log.Count > LogCapacity && LogCapacity > 0)
		{
			_log.RemoveAt(0);
		}
		for (int i = 0; i < _handlers.Count; i++)
		{
			try
			{
				_handlers[i](evt);
			}
			catch (Exception obj)
			{
				OnHandlerError?.Invoke(obj);
			}
		}
	}

	public List<PolEvent> OfType(PolEventType type)
	{
		List<PolEvent> list = new List<PolEvent>();
		for (int i = 0; i < _log.Count; i++)
		{
			if (_log[i].Type == type)
			{
				list.Add(_log[i]);
			}
		}
		return list;
	}

	public void Clear()
	{
		_log.Clear();
	}
}
