using System;

namespace RealPolitic.Core;

/// <summary>
/// Мост локализации для ядра: ядро не знает игры, поэтому все человекочитаемые строки
/// проходят через <see cref="L"/> — ключ + русский запасной текст. Игровой слой подставляет
/// <see cref="Localizer"/> (LocalizedTextManager), тесты и симуляции получают запасной текст.
/// </summary>
public static class CoreText
{
	/// <summary>(ключ, запасной текст) → локализованная строка. null — без локализации.</summary>
	public static Func<string, string, string>? Localizer { get; set; }

	public static string L(string key, string fallback)
	{
		Func<string, string, string>? localizer = Localizer;
		if (localizer == null || string.IsNullOrEmpty(key))
		{
			return fallback;
		}
		try
		{
			string text = localizer(key, fallback);
			return string.IsNullOrEmpty(text) ? fallback : text;
		}
		catch
		{
			return fallback;
		}
	}
}
