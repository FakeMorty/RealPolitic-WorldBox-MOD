using System;
using System.Collections.Generic;

namespace RealPolitic.Core;

/// <summary>
/// 0.49.1: расовый национализм — вторая, отдельная половина заказа.
///
/// Владелец 29.10.2026: «отдельно — расовый национализм, который берут иные расы».
/// И там же: «если в державе одна раса — расовые националисты не появляются».
///
/// Чем это отличается от гражданского национализма (Nationalism.cs):
///   • гражданский спор о ГОСУДАРСТВЕ: вернуть страну, которая была до присоединения;
///   • расовый спор о КРОВИ: в державе живёт чужая раса, и она хочет, чтобы
///     хозяйкой стала ОНА, а не титульная раса.
/// Поэтому у расового движения нет «родины» и нет «утраченных земель» — ему нужна
/// вторая раса внутри державы. Нет второй расы — нет движения, и точка.
///
/// Что делает, придя к власти (по ответам владельца):
///   • никакого вывоза и никаких убийств: чужим отзывают гражданство;
///   • держава закрывает им въезд на 50 / 100 / 200 лет — по силе движения;
///   • через три года движение возвращает прежний строй и уходит в тень.
/// </summary>
public sealed class RaceNationalismState
{
	/// <summary>Раса, которая подняла движение.</summary>
	public string RaceId { get; set; } = string.Empty;

	public string RaceName { get; set; } = string.Empty;

	/// <summary>Какую долю населения державы эта раса занимает (0..1).</summary>
	public float Share { get; set; }

	/// <summary>Сила движения 0..100.</summary>
	public float Pressure { get; set; }

	/// <summary>Строй, который стоял прямо перед националистами.</summary>
	public int PrevIdeology { get; set; } = -1;

	/// <summary>Титульная раса, которая была до них.</summary>
	public string PrevTitularRace { get; set; } = string.Empty;

	/// <summary>Год прихода к власти (0 — не у власти).</summary>
	public int SeizedYear { get; set; }

	/// <summary>До какого года чужим закрыт въезд.</summary>
	public int EntryBanUntilYear { get; set; }

	/// <summary>До какого года движение сидит тихо.</summary>
	public int ShadowUntilYear { get; set; }

	/// <summary>Сколько раз движение уже побеждало.</summary>
	public int Victories { get; set; }

	/// <summary>Есть ли вообще движение: есть раса и есть за что бороться.</summary>
	public bool Active => !string.IsNullOrEmpty(RaceId) && Share > 0f;

	/// <summary>У власти ли расовые националисты.</summary>
	public bool InPower(int year) => SeizedYear > 0 && year < SeizedYear + RaceNationalismEngine.ReturnAfterYears;
}

/// <summary>Кому держава отозвала гражданство и до какого года закрыт въезд.</summary>
public sealed class CitizenshipBan
{
	public int KingdomId { get; set; }

	public string RaceId { get; set; } = string.Empty;

	public string RaceName { get; set; } = string.Empty;

	public int FromYear { get; set; }

	public int UntilYear { get; set; }

	/// <summary>True — гражданство отозвано; false — только закрыт въезд новым.</summary>
	public bool CitizenshipRevoked { get; set; }
}

/// <summary>Чистые расчёты расового национализма — без обращения к игре.</summary>
public static class RaceNationalismEngine
{
	/// <summary>Сколько лет расовые националисты держат власть.</summary>
	public const int ReturnAfterYears = 3;

	/// <summary>С какого давления они начинают требовать власть.</summary>
	public const float PowerThreshold = 45f;

	/// <summary>Минимальная доля расы, чтобы она вообще считалась силой (иначе — мелочь).</summary>
	public const float MinRaceShare = 0.15f;

	/// <summary>Сколько лет сидят тихо после победы.</summary>
	public const int ShadowAfterVictoryYears = 40;

	/// <summary>Сколько лет сидят тихо после разгрома.</summary>
	public const int ShadowAfterDefeatYears = 20;

	/// <summary>
	/// Может ли в державе вообще появиться расовое движение.
	///
	/// Владелец: «если в державе одна раса — расовые националисты не появляются».
	/// Здесь это жёсткое правило, а не вероятность: одна раса — движения нет никогда.
	/// </summary>
	public static bool CanAppear(int distinctRaces, float minorityShare)
	{
		return distinctRaces >= 2 && minorityShare >= MinRaceShare;
	}

	/// <summary>
	/// Один год жизни расового движения. Возвращает новое давление 0..100.
	/// growth — насколько быстро копится злоба: при притеснении быстрее.
	/// </summary>
	public static float NextPressure(float pressure, float share, bool oppressed)
	{
		if (share < MinRaceShare)
		{
			// Раса слишком мала, чтобы что-то требовать — обида тихо остывает.
			return Math.Max(0f, pressure - 0.6f);
		}
		// Потолок — доля расы в державе, но вдвое выше: чужая раса, которая уже
		// составляет треть державы, способна дойти до самой высокой злобы.
		float ceiling = Clamp(share * 200f);
		float growth = oppressed ? 2.5f : 1.2f;
		growth *= 1f + share;
		if (pressure < ceiling)
		{
			return Clamp(Math.Min(ceiling, pressure + growth));
		}
		return Clamp(Math.Max(ceiling, pressure - 0.6f));
	}

	/// <summary>Готово ли движение взять власть.</summary>
	public static bool CanRise(RaceNationalismState state, int year)
	{
		if (state == null || !state.Active)
		{
			return false;
		}
		if (year < state.ShadowUntilYear || state.SeizedYear > 0)
		{
			return false;
		}
		return state.Pressure >= PowerThreshold;
	}

	/// <summary>Пришло ли время вернуть прежний строй.</summary>
	public static bool TimeToReturn(RaceNationalismState state, int year)
	{
		return state != null && state.SeizedYear > 0 && year >= state.SeizedYear + ReturnAfterYears;
	}

	/// <summary>
	/// Сколько лет закрыт въезд чужим. Ровно как у гражданских националистов:
	/// по силе движения — 50, 100 или 200 лет.
	/// </summary>
	public static int EntryBanYears(float pressure)
	{
		if (pressure >= 85f)
		{
			return 200;
		}
		if (pressure >= 65f)
		{
			return 100;
		}
		return 50;
	}

	/// <summary>
	/// Кого в первую очередь лишают гражданства. По характеру вождя:
	/// жестокий и завоеватель забирают гражданство у всех чужих,
	/// мягкий — только закрывает въезд новым.
	/// </summary>
	public static NationalismVerdict Verdict(RulerTrait trait, float pressure)
	{
		switch (trait)
		{
			case RulerTrait.Cruel:
			case RulerTrait.Conqueror:
				return NationalismVerdict.RevokeCitizenship;
			case RulerTrait.Just:
			case RulerTrait.Diplomat:
				return pressure >= 75f ? NationalismVerdict.RevokeCitizenship : NationalismVerdict.CloseBorders;
			case RulerTrait.Pious:
			case RulerTrait.Scholar:
				return pressure >= 65f ? NationalismVerdict.RevokeCitizenship : NationalismVerdict.CloseBorders;
			default:
				return NationalismVerdict.CloseBorders;
		}
	}

	/// <summary>Строй, который националисты вернут: тот, что стоял прямо перед ними.</summary>
	public static IdeologyType ParentIdeology(RaceNationalismState state)
	{
		if (state == null || state.PrevIdeology < 0)
		{
			return IdeologyType.Ethnonationalism;
		}
		IdeologyType previous = (IdeologyType)state.PrevIdeology;
		// Себя же вернуть нельзя — иначе движение стоит вечно.
		return previous == IdeologyType.Ethnonationalism ? IdeologyType.Monarchy : previous;
	}

	private static float Clamp(float value)
	{
		if (value < 0f)
		{
			return 0f;
		}
		if (value > 100f)
		{
			return 100f;
		}
		return value;
	}
}
