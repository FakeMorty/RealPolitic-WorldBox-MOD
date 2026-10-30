p = 'src/Game/PerfProbe.cs'
s = open(p, encoding='utf-8').read()

old = '''	/// <summary>Отметка «прошёл ещё один игровой год». Раз в 25 лет пишет строку.</summary>'''
assert s.count(old) == 1
s = s.replace(old, '''	// ---------- 0.53.1: замер «горячих» мест, которые работают не раз в год, а каждый кадр ----------
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

	/// <summary>Отметка «прошёл ещё один игровой год». Раз в 25 лет пишет строку.</summary>''', 1)

old = '''		_years++;
		if (_windowStartYear < 0)
		{
			_windowStartYear = year;
			return;
		}'''
assert s.count(old) == 1
s = s.replace(old, '''		_years++;
		if (_windowStartYear < 0)
		{
			_windowStartYear = year;
			_reflectionAtStart = GameRefs.ReflectionCalls;
			return;
		}''', 1)

old = '''			+ ", жителей опрошено " + _citizens
			+ ", из них перепись " + (_years > 0 ? _censusMs / _years : _censusMs).ToString("F0") + " мс";'''
assert s.count(old) == 1
s = s.replace(old, '''			+ ", жителей опрошено " + _citizens
			+ ", из них перепись " + (_years > 0 ? _censusMs / _years : _censusMs).ToString("F0") + " мс"
			// 0.53.1: то, что работает каждый кадр, а не раз в год. Если эти числа
			// большие при маленьком «на год» — тормозит не годовой шаг, а патчи.
			+ "; каждый кадр: лояльность городов " + (_years > 0 ? _loyaltyMs / _years : _loyaltyMs).ToString("F0")
			+ " мс на год, чтений через отражение " + (GameRefs.ReflectionCalls - _reflectionAtStart)
			+ " (запомнено членов: " + GameRefs.ReflectionCacheSize + ")";''', 1)

old = '''		_citizens = 0;
		_censusMs = 0;
	}'''
assert s.count(old) == 1
s = s.replace(old, '''		_citizens = 0;
		_censusMs = 0;
		_loyaltyMs = 0;
		_hotOpen = 0;
		_reflectionAtStart = GameRefs.ReflectionCalls;
	}''', 1)
open(p, 'w', encoding='utf-8').write(s)

# --- патч лояльности под замером
p = 'src/Game/Patches.cs'
s = open(p, encoding='utf-8').read()
old = '''	private static void LoyaltyPostfix(object __instance, ref int __result)
	{
		try
		{'''
assert s.count(old) == 1
s = s.replace(old, '''	private static void LoyaltyPostfix(object __instance, ref int __result)
	{
		// 0.53.1: этот постфикс вызывается у игры постоянно (окно, ИИ, каждый город).
		// Замер покажет в отчёте, сколько он реально стоит.
		PerfProbe.HotBegin();
		try
		{''', 1)
old = '''			LogOnce("ошибка поправки лояльности по идеологии: " + ex.Message);
		}
	}'''
assert s.count(old) == 1
s = s.replace(old, '''			LogOnce("ошибка поправки лояльности по идеологии: " + ex.Message);
		}
		finally
		{
			PerfProbe.HotEnd();
		}
	}''', 1)
open(p, 'w', encoding='utf-8').write(s)

# --- предупреждение о тяжёлом годе
p = 'src/Game/WorldFeeder.cs'
s = open(p, encoding='utf-8').read()
old = '''			_lastYear = currentYear;
			if (!_baselineInitialized)'''
assert s.count(old) == 1
s = s.replace(old, '''			_lastYear = currentYear;
			// 0.53.1: считаем, сколько длится весь годовой шаг мода — от начала до конца.
			System.Diagnostics.Stopwatch yearWatch = System.Diagnostics.Stopwatch.StartNew();
			if (!_baselineInitialized)''', 1)

old = '''				PlanDiplomacy();
				PlanStrategicActions(currentYear);
				_world.SetAllianceSnapshot(ReadAllianceRecords());
			}'''
assert s.count(old) == 1
s = s.replace(old, '''				PlanDiplomacy();
				PlanStrategicActions(currentYear);
				_world.SetAllianceSnapshot(ReadAllianceRecords());
				// 0.53.1: если год занял больше полусекунды — пишем строку с разбивкой.
				// Так видно, наш это тормоз или игра сама: наш вклад уже посчитан в PerfProbe.
				long yearMs = yearWatch.ElapsedMilliseconds;
				if (yearMs > 500)
				{
					LogService.LogInfo("[Real Politic] тяжёлый год " + currentYear + ": весь годовой шаг мода "
						+ yearMs + " мс, из них по прибору " + PerfProbe.MsPerYear.ToString("F0") + " мс на год. "
						+ (yearMs > 1000 ? "Если игра всё равно замирает на секунды — виноват не мод: смотрите, сколько в этот день считает сама игра." : ""));
				}
			}''', 1)
open(p, 'w', encoding='utf-8').write(s)
print('ok')
