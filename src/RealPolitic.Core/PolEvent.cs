using System;
using System.Collections.Generic;

namespace RealPolitic.Core;

public sealed class PolEvent
{
	public readonly Dictionary<string, string> Params = new Dictionary<string, string>();

	public PolEventType Type { get; set; }

	public int Year { get; set; }

	public Severity Severity { get; set; } = Severity.Minor;

	public string LocKey { get; set; } = string.Empty;

	public int? KingdomId { get; set; }

	public int? CityId { get; set; }

	public int? ActorId { get; set; }

	public DeathRecord? Death { get; set; }

	public PolEvent(PolEventType type, string locKey)
	{
		Type = type;
		LocKey = locKey;
	}

	public PolEvent With(string key, string value)
	{
		Params[key] = value;
		return this;
	}

	public PolEvent With(string key, int value)
	{
		Params[key] = value.ToString();
		return this;
	}

	public PolEvent With(string key, float value)
	{
		Params[key] = ((int)Math.Round(value)).ToString();
		return this;
	}

	public override string ToString()
	{
		string text = string.Empty;
		foreach (KeyValuePair<string, string> item in Params)
		{
			text = text + " " + item.Key + "=" + item.Value;
		}
		return "[y" + Year + "] " + Type.ToString() + " " + LocKey + text;
	}

	/// <summary>
	/// Подставляет параметры события в шаблон вида «В королевстве {kingdom} основана культура {culture}».
	/// Чистая функция: не зависит от WorldBox, используется как ядром, так и игровым слоем.
	/// </summary>
	public string Render(string template)
	{
		if (string.IsNullOrEmpty(template))
		{
			return string.Empty;
		}
		string text = template;
		foreach (KeyValuePair<string, string> item in Params)
		{
			text = text.Replace("{" + item.Key + "}", item.Value ?? string.Empty);
		}
		return text;
	}

	public string Get(string key)
	{
		if (Params.TryGetValue(key, out var value))
		{
			return value;
		}
		return string.Empty;
	}

	public int GetId(string key)
	{
		if (Params.TryGetValue(key, out var value) && int.TryParse(value, out var num))
		{
			return num;
		}
		return 0;
	}
}
