using System;
using System.Collections.Generic;
using System.Diagnostics;
using NeoModLoader.services;

namespace RealPolitic;

/// <summary>
/// 0.41: встроенный замер скорости мода.
///
/// Зачем. Владелец 29.10.2026: «игра лагает сильно». По журналу и летописи видно,
/// что мод замедляется по мере роста мира (год 2–10 — около 5 секунд на год,
/// годы 10–90 — уже около 19 секунд), но точную причину по логам не видно.
/// Поэтому мод сам считает, сколько миллисекунд занимает каждый его шаг,
/// и раз в 25 игровых лет пишет одну строку в Player.log:
///
///   производительность: год 75 — за годов: 25, на год 84 мс:
///   сборка держав 41 мс, годовой шаг ядра 22 мс, синхронизация 12 мс,
///   летопись 3 мс, жителей опрошено 6 400
///
/// По этой строке сразу видно, что тяжело: если «сборка держав» — виноват опрос
/// жителей, если «синхронизация» — обход городов, если всё мало́, а игра всё равно
/// тормозит — дело не в моде.
/// </summary>
public static class PerfProbe
{
	/// <summary>Счётчики шагов, которые мы измеряем.</summary>
	public static class Step
	{
		public const int Input = 0;
		public const int Core = 1;
		public const int Sync = 2;
		public const int Chronicle = 3;
		public const int Family = 4;
		public const int Annual = 5;
		public const int Count = 6;
	}

	private static readonly string[] Names = { "сборка держав", "годовой шаг ядра", "синхронизация", "летопись", "родословная", "годовые эффекты" };

	private static readonly double[] TotalMs = new double[Step.Count];

	private static readonly Stopwatch Clock = new Stopwatch();

	private static int _openStep = -1;

	private static int _windowStartYear = -1;

	private static int _years;

	private static int _citizens;

	private static double _censusMs;

	private static readonly Stopwatch CensusClock = new Stopwatch();

	private static readonly HashSet<string> _logged = new HashSet<string>();

	/// <summary>Сколько лет между строками замера.</summary>
	public const int ReportEveryYears = 25;

	/// <summary>Начать измерение шага. Парные вызовы Begin/End намеренно без вложенности.</summary>
	public static void Begin(int step)
	{
		if (!RealPoliticMod.PerfReport)
		{
			return;
		}
		_openStep = step;
		Clock.Restart();
	}

	/// <summary>Закончить измерение шага.</summary>
	public static void End()
	{
		if (_openStep < 0)
		{
			return;
		}
		Clock.Stop();
		if (_openStep < Step.Count)
		{
			TotalMs[_openStep] += Clock.Elapsed.TotalMilliseconds;
		}
		_openStep = -1;
	}

	/// <summary>Сколько жителей опрошено за окно (для отчёта).</summary>
	public static void CountCitizens(int count)
	{
		if (count > 0)
		{
			_citizens += count;
		}
	}

	/// <summary>0.43: начало замера переписи жителей (внутри «сборки держав»).</summary>
	public static void BeginCensus()
	{
		if (!RealPoliticMod.PerfReport)
		{
			return;
		}
		CensusClock.Restart();
	}

	/// <summary>0.43: конец замера переписи жителей.</summary>
	public static void EndCensus()
	{
		if (!CensusClock.IsRunning)
		{
			return;
		}
		CensusClock.Stop();
		_censusMs += CensusClock.Elapsed.TotalMilliseconds;
	}

	// ---------- 0.53.1: замер «горячих» мест, которые работают не раз в год, а каждый кадр ----------
	private static readonly Stopwatch HotClock = new Stopwatch();
	private static int _hotOpen;
	private static double _loyaltyMs;
	private static long _reflectionAtStart;

	/// <summary>Начало замера горячего места (патч лояльности города).</summary>
	public static void HotBegin()
	{
		if (!RealPoliticMod.PerfReport || _hotOpen > 0)
		{
			return;
		}
		_hotOpen++;
		HotClock.Restart();
	}

	/// <summary>Конец замера горячего места.</summary>
	public static void HotEnd()
	{
		if (_hotOpen == 0)
		{
			return;
		}
		_hotOpen--;
		HotClock.Stop();
		_loyaltyMs += HotClock.Elapsed.TotalMilliseconds;
	}

	/// <summary>Отметка «прошёл ещё один игровой год». Раз в 25 лет пишет строку.</summary>
	public static void YearPassed(int year)
	{
		if (!RealPoliticMod.PerfReport)
		{
			return;
		}
		_years++;
		if (_windowStartYear < 0)
		{
			_windowStartYear = year;
			_reflectionAtStart = GameRefs.ReflectionCalls;
			return;
		}
		if (year - _windowStartYear < ReportEveryYears)
		{
			return;
		}
		double perYear = TotalMs[Step.Core] + TotalMs[Step.Input] + TotalMs[Step.Sync] + TotalMs[Step.Chronicle] + TotalMs[Step.Family] + TotalMs[Step.Annual];
		perYear = _years > 0 ? perYear / _years : perYear;
		string line = "[Real Politic] производительность: год " + year
			+ " — лет в отчёте: " + _years
			+ ", на год " + perYear.ToString("F0") + " мс: "
			+ Names[Step.Input] + " " + Average(Step.Input).ToString("F0") + " мс, "
			+ Names[Step.Core] + " " + Average(Step.Core).ToString("F0") + " мс, "
			+ Names[Step.Sync] + " " + Average(Step.Sync).ToString("F0") + " мс, "
			+ Names[Step.Chronicle] + " " + Average(Step.Chronicle).ToString("F0") + " мс, "
            + Names[Step.Family] + " " + Average(Step.Family).ToString("F0") + " мс, "
            + Names[Step.Annual] + " " + Average(Step.Annual).ToString("F0") + " мс"
			+ ", жителей опрошено " + _citizens
			+ ", из них перепись " + (_years > 0 ? _censusMs / _years : _censusMs).ToString("F0") + " мс"
			// 0.53.1: то, что работает каждый кадр, а не раз в год. Если эти числа
			// большие при маленьком «на год» — тормозит не годовой шаг, а патчи.
			+ "; каждый кадр: лояльность городов " + (_years > 0 ? _loyaltyMs / _years : _loyaltyMs).ToString("F0")
			+ " мс на год, чтений через отражение " + (GameRefs.ReflectionCalls - _reflectionAtStart)
			+ " (запомнено членов: " + GameRefs.ReflectionCacheSize + ")";
		string key = "year:" + year;
		if (_logged.Add(key))
		{
			LogService.LogInfo(line);
		}
		Reset();
		_windowStartYear = year;
	}

	private static double Average(int step)
	{
		return _years > 0 ? TotalMs[step] / _years : TotalMs[step];
	}

	/// <summary>
	/// Сколько миллисекунд мод тратит на один игровой год (по последнему окну).
	/// По этому числу видно, виноват ли мод: WorldBox на скорости x20 отдаёт годы
	/// очень часто, и если мод тратит 80+ мс на год, игра будет «спотыкаться».
	/// </summary>
	public static double MsPerYear
	{
		get
		{
			double total = TotalMs[Step.Core] + TotalMs[Step.Input] + TotalMs[Step.Sync] + TotalMs[Step.Chronicle] + TotalMs[Step.Family] + TotalMs[Step.Annual];
			return _years > 0 ? total / _years : total;
		}
	}

	/// <summary>
	/// Мод под нагрузкой: на год уходит больше 70 мс. Тогда тяжёлые части
	/// (перепись взглядов, обход городов) работают через год, а не каждый год.
	/// Так на скорости x20 игра не «спотыкается» на моде.
	/// </summary>
	public static bool UnderPressure => MsPerYear > 70.0;

	/// <summary>Сброс при смене мира.</summary>
	public static void Reset()
	{
		for (int i = 0; i < Step.Count; i++)
		{
			TotalMs[i] = 0;
		}
		_openStep = -1;
		_windowStartYear = -1;
		_years = 0;
		_citizens = 0;
		_censusMs = 0;
		_loyaltyMs = 0;
		_hotOpen = 0;
		_reflectionAtStart = GameRefs.ReflectionCalls;
	}
}
