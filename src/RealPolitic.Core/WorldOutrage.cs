using System;
using System.Collections.Generic;

namespace RealPolitic.Core;

/// <summary>Насколько сильно мир возмущён и что именно делает.</summary>
public enum OutrageResponse
{
	/// <summary>Молчат: обычная смена строя, никого не тронули.</summary>
	None = 0,

	/// <summary>Санкции и охлаждение отношений.</summary>
	Sanctions = 1,

	/// <summary>Санкции, охлаждение и потеря статуса великой державы.</summary>
	SanctionsAndStatus = 2,

	/// <summary>Всё выше плюс вопрос на ближайшем съезде КМУД.</summary>
	SanctionsStatusAndKmud = 3
}

/// <summary>
/// 0.50: мир возмущается и наказывает.
///
/// Владелец 29.10.2026, ответ №4 по национализму: «мир возмущается и наказывает
/// (санкции, КМУД, потеря статуса великой державы)».
///
/// Правило простое и понятное: если держава отняла у людей гражданство или
/// устроила националистический переворот — соседи этого не одобряют.
///   • санкции — благосостояние державы падает каждый год, пока они действуют;
///   • отношения правителей со всеми портятся;
///   • великая держава, которая устроила такое, теряет место в совете;
///   • а если съезд КМУД уже собирался — вопрос о ней попадёт в повестку.
///
/// Всё это — арифметика без обращения к игре, поэтому проверяется тестами.
/// </summary>
public static class OutrageEngine
{
	/// <summary>Сколько лет держатся санкции за обычный националистический переворот.</summary>
	public const int SanctionYears = 20;

	/// <summary>Сколько лет держатся санкции, если у людей ещё и отобрали гражданство.</summary>
	public const int HarshSanctionYears = 40;

	/// <summary>Сколько лет мир помнит про отнятое гражданство (пятно для съезда).</summary>
	public const int StainYears = 50;

	/// <summary>Насколько падают отношения правителей со всеми (за один раз).</summary>
	public const int RelationHit = 25;

	/// <summary>Насколько падают отношения, если гражданство отобрали.</summary>
	public const int HarshRelationHit = 45;

	/// <summary>
	/// Насколько мир возмущён, 0..100. Сильное движение и отзыв гражданства
	/// возмущают больше, чем тихая смена строя в маленькой державе.
	/// </summary>
	public static float Level(float pressure, bool citizenshipRevoked)
	{
		float level = Clamp(pressure);
		if (citizenshipRevoked)
		{
			level += 25f;
		}
		return Clamp(level);
	}

	/// <summary>Что именно сделает мир при таком возмущении.</summary>
	public static OutrageResponse Response(float level, bool citizenshipRevoked, bool kmudAlreadyHeld)
	{
		float value = Clamp(level);
		if (value < 30f && !citizenshipRevoked)
		{
			return OutrageResponse.None;
		}
		bool status = citizenshipRevoked || value >= 60f;
		if (!status)
		{
			return OutrageResponse.Sanctions;
		}
		return kmudAlreadyHeld ? OutrageResponse.SanctionsStatusAndKmud : OutrageResponse.SanctionsAndStatus;
	}

	/// <summary>Сколько лет длятся санкции.</summary>
	public static int SanctionFor(OutrageResponse response, bool citizenshipRevoked)
	{
		if (response == OutrageResponse.None)
		{
			return 0;
		}
		return citizenshipRevoked ? HarshSanctionYears : SanctionYears;
	}

	/// <summary>Насколько портятся отношения со всеми державами.</summary>
	public static int RelationHitFor(OutrageResponse response, bool citizenshipRevoked)
	{
		if (response == OutrageResponse.None)
		{
			return 0;
		}
		return citizenshipRevoked ? HarshRelationHit : RelationHit;
	}

	/// <summary>Теряет ли держава место великой державы.</summary>
	public static bool LosesGreatPower(OutrageResponse response)
	{
		return response == OutrageResponse.SanctionsAndStatus
			|| response == OutrageResponse.SanctionsStatusAndKmud;
	}

	/// <summary>Попадёт ли вопрос в повестку съезда.</summary>
	public static bool GoesToKmud(OutrageResponse response)
	{
		return response == OutrageResponse.SanctionsStatusAndKmud;
	}

	private static float Clamp(float value)
	{
		if (value < 0f)
		{
			return 0f;
		}
		return value > 100f ? 100f : value;
	}
}
