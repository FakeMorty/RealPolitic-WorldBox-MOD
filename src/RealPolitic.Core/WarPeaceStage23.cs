using System;
using System.Collections.Generic;

namespace RealPolitic.Core;

/// <summary>
/// Этап 23: честная хроника распадов, действительное регентство, эпохальные авиаудары
/// (тайные рейды), границы между государствами и новая эра династии.
/// </summary>

// ---------------------------------------------------------------- распады держав

/// <summary>Один фрагмент распавшейся державы (для летописи: имя и число городов).</summary>
public sealed class CollapseFragment
{
	public int KingdomId;

	public string Name = string.Empty;

	public int Towns;
}

/// <summary>
/// Ключ пары держав (все парные состояния этапа 23: границы, войн-рейды).
/// Ключ симметричен: min(id) старшим словом, max(id) младшим.
/// </summary>
public static class PairKey
{
	public static long Of(int a, int b)
	{
		int lo = Math.Min(a, b);
		int hi = Math.Max(a, b);
		return ((long)hi << 32) | (uint)lo;
	}

	public static int A(long key) => (int)(key & 0xFFFFFFFFL);

	public static int B(long key) => (int)(key >> 32);
}

// ---------------------------------------------------------------- границы

/// <summary>Политика пограничного режима между двумя державами.</summary>
public enum BorderStatus
{
	/// <summary>Границы открыты: граждане свободно переходят из государства в государство.</summary>
	Open = 0,

	/// <summary>
	/// Границы закрыты: чужаков изгоняют домой (погоней), граждане чужой державы
	/// в своих землях терпят нападки местных; торговые пути разорваны.
	/// </summary>
	Closed = 1
}

/// <summary>
/// Пограничный режим между двумя соседями. Действует от `SinceYear`
/// (события «открылись/закрылись» пишутся один раз при смене).
/// </summary>
public sealed class BorderRecord
{
	public BorderStatus Status = BorderStatus.Closed;

	/// <summary>Сколько лет подряд пара выполняет условия открытия (дипломатические переговоры).</summary>
	public int OpeningTalksYears;

	public int SinceYear;

	/// <summary>Граница была закрыта осознанно (после открытой) — облава на ней даёт запрет на открытие.</summary>
	public bool ExplicitlyClosed;

	/// <summary>Год, ДО которого раскрытие запрещено (обида за переправу чужаков домой).</summary>
	public int LockedUntilYear;
}

/// <summary>Книга границ: политика пары + нюанс сериализации.</summary>
public sealed class BordersBook
{
	private readonly Dictionary<long, BorderRecord> _borders = new Dictionary<long, BorderRecord>();

	public IEnumerable<KeyValuePair<long, BorderRecord>> All => _borders;

	/// <summary>0.23.2: по умолчанию границы ЗАКРЫТЫ — открыть их надо дипломатически заслужить.</summary>
	public static BorderStatus DefaultStatus = BorderStatus.Closed;

	public BorderStatus StatusBetween(int a, int b)
	{ return _borders.TryGetValue(PairKey.Of(a, b), out BorderRecord r) ? r.Status : DefaultStatus; }

	/// <summary>Счётчик лет переговоров об открытии; сбрасывается, когда условия нарушены.</summary>
	public int OpeningTalks(int a, int b)
	{ return _borders.TryGetValue(PairKey.Of(a, b), out BorderRecord r) ? r.OpeningTalksYears : 0; }

	public int AdvanceOpeningTalks(int a, int b)
	{
		long key = PairKey.Of(a, b);
		if (!_borders.TryGetValue(key, out BorderRecord rec))
		{
			rec = new BorderRecord { Status = DefaultStatus };
			_borders[key] = rec;
		}
		return ++rec.OpeningTalksYears;
	}

	public void ResetOpeningTalks(int a, int b)
	{
		if (_borders.TryGetValue(PairKey.Of(a, b), out BorderRecord rec))
		{
			rec.OpeningTalksYears = 0;
		}
	}

	public bool ClosedBetween(int a, int b) => StatusBetween(a, b) == BorderStatus.Closed;

	/// <summary>Закрыта решением правителя (а не «по умолчанию»).</summary>
	public bool ExplicitlyClosedBetween(int a, int b)
	{ return _borders.TryGetValue(PairKey.Of(a, b), out BorderRecord r) && r.ExplicitlyClosed; }

	/// <summary>Год, до которого пара заперта (после эпизода пограничной облавы на чужаках).</summary>
	public int LockedUntilBetween(int a, int b)
	{ return _borders.TryGetValue(PairKey.Of(a, b), out BorderRecord r) ? r.LockedUntilYear : 0; }

	/// <summary>
	/// Прямая установка режима (решение правителя/особого курса). Возвращает true,
	/// если режим реально сменился (событие публикует вызывающий).
	/// </summary>
	public bool SetStatus(int a, int b, BorderStatus status, int year)
	{
		long key = PairKey.Of(a, b);
		bool existed = _borders.TryGetValue(key, out BorderRecord rec);
		if ((existed && rec.Status == status) || (!existed && status == DefaultStatus))
		{
			if (!existed)
			{
				// Фиксируем запись, чтобы SinceYear был известен, но событие не публикуем.
				_borders[key] = new BorderRecord { Status = status, SinceYear = year };
			}
			return false;
		}
		rec = rec ?? new BorderRecord();
		rec.OpeningTalksYears = 0;
		rec.ExplicitlyClosed = status == BorderStatus.Closed;
		rec.Status = status;
		rec.SinceYear = year;
		_borders[key] = rec;
		return true;
	}

	/// <summary>Запирает пару после эпизода высылки чужаков (ускорение полсотни лет).</summary>
	public void Lock(int a, int b, int year, int years)
	{
		long key = PairKey.Of(a, b);
		if (!_borders.TryGetValue(key, out BorderRecord rec))
		{
			rec = new BorderRecord();
			_borders[key] = rec;
		}
		rec.LockedUntilYear = Math.Max(rec.LockedUntilYear, year + Math.Max(0, years));
	}

	public void RemoveKingdom(int id)
	{
		List<long> purge = new List<long>();
		foreach (KeyValuePair<long, BorderRecord> pair in _borders)
		{
			if (PairKey.A(pair.Key) == id || PairKey.B(pair.Key) == id)
			{
				purge.Add(pair.Key);
			}
		}
		for (int i = 0; i < purge.Count; i++)
		{
			_borders.Remove(purge[i]);
		}
	}

	public void Clear() => _borders.Clear();
}

// ---------------------------------------------------------------- регентство

/// <summary>
/// Действительное регентство этапа 23. Наследник известен ядру: на трон звался регент,
/// носящий королевский титул «за него». В момент совершеннолетия наследник
/// получает корону обратно (в игре делает фиддер по опросу).
/// </summary>
public static class RegencyGame
{
	/// <summary>Порог совершеннолетия по законам престолонаследия.</summary>
	public const int MajorityAge = 18;

	/// <summary>Действительное регентство — институт наследственной власти: корону
	/// носит малолетний наследник рода, пока взрослая родня правит за него. Это верно
	/// и для монархий, и для военных диктатур: ребёнок-наследник не должен править
	/// сам. Исключение — выборы и пленумы: там наследников всегда выбирают взрослых.</summary>
	public static bool Qualifies(IdeologyType ideology, SuccessionLaw law)
	{
		if (SuccessionLaws.HasPeriodicCycle(law) || SuccessionLaws.HasPeriodicCycle(ideology))
		{
			return false;
		}
		return true;
	}

	/// <summary>
	/// Ход положения: строится ли действительное регентство и под каким возрастом кандидат.
	/// Возвращает 0, если наследник уже не нуждается в регенте.
	/// </summary>
	public static int YearsRemaind(int heirAge, int year, int crownYear)
	{
		if (heirAge >= MajorityAge)
		{
			return 0;
		}
		return Math.Max(0, MajorityAge - heirAge);
	}

	/// <summary>
	/// Публикует событие «регент на троне» (титул ушёл от отроческого наследника).
	/// </summary>
	public static void PublishCrowned(PoliticalWorld world, KingdomPolitics state, RulerRef heir, string regentName)
	{
		state.Regency ??= new RegencyState();
		if (!state.Regency.IsActive)
		{
			state.Regency.Start(world.Year, regentName, state.Ruler?.ActorId ?? 0);
		}
		state.Regency.RegentName = regentName;
		world.Bus.Publish(new PolEvent(PolEventType.RegencyCrowned, "RP_EV_REGENCY_CROWNED")
		{
			Year = world.Year,
			Severity = Severity.Major,
			KingdomId = state.KingdomId
		}.With("kingdom", world.KingdomNameOf(state.KingdomId))
			.With("regent", regentName)
			.With("heir", heir.Name)
			.With("age", Math.Max(0, heir.Age)));
	}

	/// <summary>Событие «наследник совершеннолетний, корона возвращается».</summary>
	public static void PublishPassed(PoliticalWorld world, KingdomPolitics state, string heirName, string regentName)
	{
		state.Regency?.End();
		world.Bus.Publish(new PolEvent(PolEventType.RegencyPassed, "RP_EV_REGENCY_PASSED")
		{
			Year = world.Year,
			Severity = Severity.Major,
			KingdomId = state.KingdomId
		}.With("kingdom", world.KingdomNameOf(state.KingdomId))
			.With("heir", heirName)
			.With("regent", regentName)
			.With("age", MajorityAge));
	}
}

// ---------------------------------------------------------------- регена у власти: эпохальные рейды

/// <summary>
/// Тайный авиаудар: высокоразвитая держава (теховая зрелость) изредка бросает
/// звено бомбардировщиков по вражескому городу. Честно — не Белград: ущерб ограничен
/// (бомба, а не ядро), события редки и документируются летописью.
/// </summary>
public static class PowerRaids
{
	/// <summary>Уровень развития (тех/городские зрелость + казна), с которого держава способна на рейд.</summary>
	public const float Threshold = 60f;

	/// <summary>Минимальный разрыв между рейдами одной державы, лет.</summary>
	public const int CooldownYears = 6;

	/// <summary>
	/// Балл развития державы: благосостояние экономики (уровень городов/техов отражается
	/// в доходах) + стабильная легитимность. Порог открывает рейды.
	/// </summary>
	public static double Capability(KingdomPolitics state)
	{
		double wealth = Math.Max(0.0, Math.Min(100.0, state.Economy?.Prosperity ?? 0.0)) * 0.6;
		double order = Math.Max(0.0, Math.Min(100.0, state.Legitimacy)) * 0.4;
		return wealth + order;
	}

	/// <summary>Держава способна на рейд против wojna-врага (и туман войны его прикрывает).</summary>
	public static bool CanRaid(KingdomPolitics state, SimParams p, int year, double enemyWelfare)
	{
		if (!p.EnablePowerRaids || state == null)
		{
			return false;
		}
		if (Capability(state) < Threshold)
		{
			return false;
		}
		if (enemyWelfare <= 30.0)
		{
			return false; // бить по разорённому — бессмысленно и нечестно.
		}
		return state.LastPowerRaidYear <= 0 || year - state.LastPowerRaidYear >= CooldownYears;
	}

	/// <summary>Событие тайного рейда: жертвой выбран самый обеспеченный чужой город.</summary>
	public static void PublishRaid(PoliticalWorld world, KingdomPolitics bomber, int victimKingdomId, string townName)
	{
		world.Bus.Publish(new PolEvent(PolEventType.PowerRaid, "RP_EV_POWER_RAID")
		{
			Year = world.Year,
			Severity = Severity.Major,
			KingdomId = bomber.KingdomId
		}.With("kingdom", world.KingdomNameOf(bomber.KingdomId))
			.With("target", world.KingdomNameOf(victimKingdomId))
			.With("town", townName));
	}
}

// ---------------------------------------------------------------- распады

/// <summary>
/// Реестр распадающихся держав: если держава лишается в ханаке (за 3 года) трёх и более
/// обломков с суммарно тремя и более городами — хронка получает единую итоговую строку
/// «государство С распалось на N государств: ...», без заставы читателя домысливать.
/// На четвёртый год окна запись не публикуется повторно.
/// </summary>
public sealed class CollapseTracker
{
	private readonly Dictionary<int, List<KeyValuePair<int, string>>> _fragments = new Dictionary<int, List<KeyValuePair<int, string>>>();
	private readonly Dictionary<int, int> _firstYear = new Dictionary<int, int>();
	private readonly Dictionary<int, int> _lastPublished = new Dictionary<int, int>();

	public const int WindowYears = 3;
	public const int MinFragments = 3;

	/// <summary>Фиксирует очередной откол; вернёт сводку, если основание для распада сложилось.</summary>
	public List<CollapseFragment>? RecordFragment(int fromKingdomId, int year,
		int fragmentId, string fragmentName, Func<int, int> townsOfKingdom)
	{
		if (!_fragments.TryGetValue(fromKingdomId, out List<KeyValuePair<int, string>> list))
		{
			list = new List<KeyValuePair<int, string>>();
			_fragments[fromKingdomId] = list;
			_firstYear[fromKingdomId] = year;
		}
		else if (year - _firstYear[fromKingdomId] > WindowYears)
		{
			// Новое окно.
			_fragments[fromKingdomId] = list = new List<KeyValuePair<int, string>>();
			_firstYear[fromKingdomId] = year;
		}
		for (int i = 0; i < list.Count; i++)
		{
			if (list[i].Key == fragmentId)
			{
				return null;
			}
		}
		list.Add(new KeyValuePair<int, string>(fragmentId, fragmentName));
		if (list.Count < MinFragments)
		{
			return null;
		}
		if (_lastPublished.TryGetValue(fromKingdomId, out int last) && year - last < WindowYears)
		{
			return null;
		}
		_lastPublished[fromKingdomId] = year;
		List<CollapseFragment> result = new List<CollapseFragment>(list.Count);
		for (int i = 0; i < list.Count; i++)
		{
			result.Add(new CollapseFragment
			{
				KingdomId = list[i].Key,
				Name = list[i].Value,
				Towns = Math.Max(1, townsOfKingdom != null ? townsOfKingdom(list[i].Key) : 1)
			});
		}
		return result;
	}

	/// <summary>Держава исчезла/возродилась — таблица чистится.</summary>
	public void RemoveKingdom(int id)
	{
		_fragments.Remove(id);
		_firstYear.Remove(id);
		_lastPublished.Remove(id);
	}

	public void Clear()
	{
		_fragments.Clear();
		_firstYear.Clear();
		_lastPublished.Clear();
	}
}

// ---------------------------------------------------------------- новая эра династии

/// <summary>
/// Пункт пользователя: королевская семья носит фамилию, клан называется по фамилии
/// короля-основателя. Ядро определяет правила формирования и хранит «новую эру».
/// </summary>
public static class DynastyNaming
{
	/// <summary>
	/// Фамилия династии из имени короля-основателя: берётся второе слово полного имени
	/// («Таргариен» из «Эйгон Таргариен»), иначе имя притяжательным окончанием
	/// (как если бы звался дом). Королевский клан зовётся СЛОВОМ фамилии.
	/// </summary>
	public static string DynastySurname(string founderName, string founderClanName)
	{
		// 0.23.2: фамилия династии = ИМЯ КЛАНА игры («Дел Енте»), а не личное имя основателя.
		// Так фамилия правителя всегда совпадает с его кланом, есть у первого короля с первого дня,
		// а родственники в других государствах носят ту же фамилию по праву крови, а не «по бренду».
		if (!string.IsNullOrWhiteSpace(founderClanName))
		{
			return founderClanName.Trim();
		}
		if (!string.IsNullOrWhiteSpace(founderName))
		{
			string cleaned = founderName.Trim();
			int space = cleaned.LastIndexOf(' ');
			if (space > 0 && space < cleaned.Length - 1)
			{
				string family = cleaned.Substring(space + 1).Trim();
				if (family.Length >= 2)
				{
					return family;
				}
			}
			// Однословное имя основателя: «Гардаян» → династия «Гардаян».
			if (cleaned.Length >= 3)
			{
				return cleaned;
			}
		}
		return string.IsNullOrWhiteSpace(founderClanName) ? string.Empty : founderClanName.Trim();
	}

	/// <summary>
	/// Отображаемое имя члена королевской фамилии: «Имя Фамилия». Если фамилия в имени
	/// уже есть (её придумала сама игра) — не дублируем.
	/// </summary>
	public static string WithSurname(string actorName, string dynasty)
	{
		if (string.IsNullOrWhiteSpace(actorName) || string.IsNullOrWhiteSpace(dynasty))
		{
			return actorName ?? string.Empty;
		}
		return actorName.IndexOf(dynasty, StringComparison.Ordinal) >= 0 ? actorName : actorName + " " + dynasty;
	}
}
