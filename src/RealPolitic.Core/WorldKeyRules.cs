using System;

namespace RealPolitic.Core;

/// <summary>
/// 0.48: правила «ключ мира» — по нему мод раскладывает летопись по папкам.
///
/// Что случилось у владельца (29.10.2026). Он прислал два файла ключа из разных папок
/// одного и того же мира одной и той же игровой сессии:
///   • <c>universal-1</c> — временный ключ, который мод ставит, когда подхватывает
///     уже существующий мир и ещё не знает его настоящего имени;
///   • <c>native-44d25702…0b4f46</c> — настоящий ключ, который игра выдаёт миру.
/// Летопись при смене ключа начинала новую папку, и один мир оказывался разорван
/// на два файла: в одном «Держава покинула альянс», в другом вся остальная история.
///
/// Правило теперь такое: временный ключ можно заменить настоящим (это тот же мир),
/// а вот обратная замена и любые другие смены — это уже другой мир.
/// </summary>
public static class WorldKeyRules
{
	/// <summary>Ключ-заготовка, поставленный моду, пока игра не дала настоящее имя мира.</summary>
	public static bool IsProvisional(string key)
	{
		return !string.IsNullOrEmpty(key) && key.StartsWith("universal-", StringComparison.Ordinal);
	}

	/// <summary>Настоящий ключ, выданный игрой.</summary>
	public static bool IsNative(string key)
	{
		return !string.IsNullOrEmpty(key) && key.StartsWith("native-", StringComparison.Ordinal);
	}

	/// <summary>
	/// Это тот же самый мир, только теперь у него настоящее имя?
	/// Тогда папку летописи менять нельзя — иначе история разорвётся на две.
	/// </summary>
	public static bool IsSameWorldUpgrade(string boundKey, string incomingKey)
	{
		if (string.IsNullOrEmpty(boundKey) || string.IsNullOrEmpty(incomingKey))
		{
			return false;
		}
		if (string.Equals(boundKey, incomingKey, StringComparison.Ordinal))
		{
			return false; // менять нечего
		}
		return IsProvisional(boundKey) && IsNative(incomingKey);
	}
}
