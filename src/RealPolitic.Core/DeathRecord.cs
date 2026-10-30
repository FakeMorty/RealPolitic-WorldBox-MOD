using System;
using System.Text;

namespace RealPolitic.Core;

public sealed class DeathRecord
{
	public int ActorId { get; set; }
    public int PopulationBeforeDeath { get; set; } = -1;
    public string KingdomNameAtDeath { get; set; } = string.Empty;
    public bool WasLastResident { get; set; }
    public bool LastResidentEvidenceActive { get; set; }

	public string ActorName { get; set; } = string.Empty;

	public DeathCause Cause { get; set; }

	public int Year { get; set; }

	public int? KillerId { get; set; }

	public string KillerName { get; set; } = string.Empty;

	public string KillerTitle { get; set; } = string.Empty;

	/// <summary>Убийца — разумное существо (а не зверь).</summary>
	public bool KillerIsCiv { get; set; }

	/// <summary>Государство убийцы (только для разумных).</summary>
	public string KillerKingdom { get; set; } = string.Empty;

	/// <summary>Город убийцы (только для разумных).</summary>
	public string KillerCity { get; set; } = string.Empty;

	/// <summary>Занятие убийцы: воин, правитель, житель (только для разумных).</summary>
	public string KillerJob { get; set; } = string.Empty;

	/// <summary>Вид зверя-убийцы: «волк», «медведь» (только для зверей).</summary>
	public string KillerSpecies { get; set; } = string.Empty;

	/// <summary>Разновидность убийцы для разумных: «человек», «эльф», «орк», «гном» (сущность).</summary>
	public string KillerRace { get; set; } = string.Empty;

	/// <summary>Пол убийцы (для форм «мужской/женский», самец/самка у зверей).</summary>
	public bool KillerFemale { get; set; }

	/// <summary>Возраст убийцы на момент убийства (0 — неизвестно).</summary>
	public int KillerAge { get; set; }

	public int? TileId { get; set; }

	public int? KingdomId { get; set; }

	public string CityName { get; set; } = string.Empty;

	public bool WasRuler { get; set; }

	public bool WasChosen { get; set; }

	/// <summary>Умерший — женщина (для титулов «королева» и т. п.).</summary>
	public bool WasFemale { get; set; }

	public int Age { get; set; }

	/// <summary>Сколько лет умерший правил (null — неизвестно, не был правителем или правитель не отслеживался).</summary>
	public int? ReignYears { get; set; }

	public string Note { get; set; } = string.Empty;

	public string LocKey => "RP_DEATH_" + Cause.ToString().ToUpperInvariant();

	/// <summary>
	/// Убийца — корабль игры (boat_trading_*, boat_transport_*, …), а не живое существо.
	/// Такие ассеты игра заводит как юнитов, но это суда: правителя расстреливают их пушки.
	/// Проверяем и по виду, и по имени — сырое «boat_…» приходит и в название юнита.
	/// </summary>
	public bool KillerIsBoat
	{
		get
		{
			if (Cause == DeathCause.KilledByBoat)
			{
				return true;
			}
			if (!string.IsNullOrEmpty(KillerSpecies) && KillerSpecies.IndexOf("boat", System.StringComparison.OrdinalIgnoreCase) >= 0)
			{
				return true;
			}
			if (!string.IsNullOrEmpty(KillerName) && KillerName.IndexOf("boat", System.StringComparison.OrdinalIgnoreCase) >= 0)
			{
				return true;
			}
			return false;
		}
	}

	/// <summary>Человекочитаемое «имя» корабля-убийцы: «Кузь-торговец» вместо сырого boat_trading_human.</summary>
	public string KillerBoatName
	{
		get
		{
			if (string.IsNullOrEmpty(KillerName))
			{
				return string.Empty;
			}
			// Имя юнита-лодки часто остаётся сырым id ассета (boat_trading_human).
			// Тогда показываем локализованную роль судна без сырого слова.
			if (KillerName.IndexOf("boat", System.StringComparison.OrdinalIgnoreCase) >= 0)
			{
				return string.Empty;
			}
			return KillerName;
		}
	}

	/// <summary>Роль судна-убийцы по id вида: «торговец», «перевозчик»… Пусто — вид неизвестен.</summary>
	public static string BoatKindLabel(string speciesId)
	{
		if (string.IsNullOrWhiteSpace(speciesId))
		{
			return string.Empty;
		}
		string s = speciesId.ToLowerInvariant();
		if (s.Contains("trading") || s.Contains("trade"))
		{
			return "торговец";
		}
		if (s.Contains("transport"))
		{
			return "перевозчик";
		}
		if (s.Contains("fishing"))
		{
			return "рыболов";
		}
		// Просто корабль/лодка без различия роли.
		return string.Empty;
	}

	/// <summary>
	/// Имя корабля в русских кавычках «…». Нужно, чтобы судно «Пичина» не путалось
	/// с королевой Пичиной: корабли всегда названы как «…», люди — без кавычек.
	/// 0.23.13: срезаем УЖЕ имеющиеся кавычки (обоих видов) с концов, затем оборачиваем
	/// ровно один раз — «„два“» и «„два“»-подобные опечатки не размножатся в лесенку «„„два““».
	/// </summary>
	public static string QuoteName(string name)
	{
		string core = Unquote(name);
		if (core.Length == 0)
		{
			return string.Empty;
		}
		return "\u00ab" + core + "\u00bb";
	}

	/// <summary>Срезает кавычки « » „ “ и пробелы с концов строки (сравнение имён без учёта оформления).</summary>
	public static string Unquote(string name)
	{
		if (string.IsNullOrWhiteSpace(name))
		{
			return string.Empty;
		}
		string trimmed = name.Trim();
		int start = 0;
		while (start < trimmed.Length && IsQuote(trimmed[start]))
		{
			start++;
		}
		int end = trimmed.Length;
		while (end > start && IsQuote(trimmed[end - 1]))
		{
			end--;
		}
		return trimmed.Substring(start, Math.Max(0, end - start)).Trim();
	}

	private static bool IsQuote(char c)
	{
		return c == '\u00ab' || c == '\u00bb' || c == '\u201e' || c == '\u201c' || c == '\u201d' || c == '"';
	}

	/// <summary>
	/// Ключ причины смерти с учётом пола умершего. Женские формы (RP_DEATH_*_F)
	/// согласуются с женскими титулами: «Королева … казнена», а не «казнён».
	/// </summary>
	public string LocKeyFor(bool female) => female ? LocKey + "_F" : LocKey;

	public override string ToString()
	{
		StringBuilder stringBuilder = new StringBuilder();
		stringBuilder.Append(ActorName);
		if (WasRuler)
		{
			stringBuilder.Append(" (правитель)");
		}
		stringBuilder.Append(" — ").Append(Cause);
		if (!string.IsNullOrEmpty(KillerName))
		{
			stringBuilder.Append(", от руки: ").Append(KillerName);
		}
		if (!string.IsNullOrEmpty(Note))
		{
			stringBuilder.Append(" [").Append(Note).Append(']');
		}
		stringBuilder.Append(", год ").Append(Year);
		return stringBuilder.ToString();
	}
}
