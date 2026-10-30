using System;
using System.Collections.Generic;
using System.Text;

namespace RealPolitic.Core;

/// <summary>
/// Этап 25: родословная без амнезии. Игровые родственные связи теряют умерших,
/// со временем ни прабабки, ни братьев покойного короля не вспомнить.
/// Этот реестр фиксирует КАЖДОГО жителя мира один раз: имя, пол, год рождения,
/// родителей, клан и год смерти (когда его больше не видно в мире).
/// Время жизни реестра — время жизни мира: родословная строится на любой глубине,
/// а покойные помечены «†» и датами, как в хорошем генеалогическом кабинете.
/// </summary>
[Serializable]
public sealed class FamilyRecord
{
	public long Id { get; set; }

	public string Name { get; set; } = string.Empty;

	public bool Female { get; set; }

	/// <summary>Год рождения (вычисляется из возраста первой встречи).</summary>
	public int BirthYear { get; set; }

	/// <summary>Год смерти (null, пока жив; -1 = пропал из мира без фиксации смерти).</summary>
	public int? DeathYear { get; set; }

	public long FatherId { get; set; }

	public long MotherId { get; set; }

	public string ClanName { get; set; } = string.Empty;

	public long ClanId { get; set; }

	public int KingdomId { get; set; }

	/// <summary>Последний год, когда житель был замечен живым.</summary>
	public int LastSeenYear { get; set; }
}

[Serializable]
public sealed class FamilyRegistry
{
	/// <summary>Житель считается умершим, если его не видели столько лет подряд.</summary>
	public const int MissingYearsBeforeDead = 3;

	/// <summary>Потолок хранилища (архив без «катастрофы памяти»): при упорядоченном
	/// превышении вымываются древнейшие ДАВНО умершие записи — свежие поколения вечны.</summary>
	public const int Capacity = 20000;

	private readonly Dictionary<long, FamilyRecord> _records = new Dictionary<long, FamilyRecord>();

	public int Count => _records.Count;

	/// <summary>Зарегистрировать/обновить наблюдение живого жителя.</summary>
	/// <param name="id">Игровой id актёра.</param>
	/// <param name="age">Текущий возраст (для года рождения).</param>
	/// <param name="year">Текущий год мира.</param>
	public void Observe(long id, string name, bool female, int age, int year,
		long fatherId, long motherId, string clanName, long clanId, int kingdomId)
	{
		if (id == 0)
		{
			return;
		}
		if (!_records.TryGetValue(id, out FamilyRecord rec))
		{
			rec = new FamilyRecord { Id = id };
			_records.Add(id, rec);
			TrimIfOvercapacity();
		}
		rec.Name = name ?? string.Empty;
		rec.Female = female;
		rec.ClanName = clanName ?? string.Empty;
		rec.ClanId = clanId;
		rec.KingdomId = kingdomId;
		rec.LastSeenYear = year;
		if (age > 0 && year > 0)
		{
			int birthYear = year - age;
			if (rec.BirthYear <= 0 || Math.Abs(rec.BirthYear - birthYear) > 1)
			{
				rec.BirthYear = birthYear;
			}
		}
		if (fatherId != 0)
		{
			rec.FatherId = fatherId;
		}
		if (motherId != 0)
		{
			rec.MotherId = motherId;
		}
	}

	/// <summary>
	/// Cheap annual presence refresh for a record already in the registry. Most of the
	/// world's actors have fixed ancestry/name data, so re-reading all of it via game
	/// reflection every year is wasteful; the game integration still performs the full
	/// Observe() once for each newly encountered actor.
	/// </summary>
	public bool ObservePresence(long id, int year, int kingdomId)
	{
		if (id == 0 || !_records.TryGetValue(id, out FamilyRecord rec))
		{
			return false;
		}
		if (rec.DeathYear == null)
		{
			rec.LastSeenYear = year;
			rec.KingdomId = kingdomId;
		}
		return true;
	}

	/// <summary>Точная фиксация смерти (хук смерти игры знает момент).</summary>
	public void MarkDead(long id, int year)
	{
		if (id == 0 || !_records.TryGetValue(id, out FamilyRecord rec) || rec.DeathYear != null)
		{
			return;
		}
		rec.DeathYear = year;
	}

	/// <summary>Кто в этом году не наблюдался ≥ MissingYearsBeforeDead — помечается умершим.</summary>
	public int MarkMissingDead(int year)
	{
		int marked = 0;
		foreach (KeyValuePair<long, FamilyRecord> kv in _records)
		{
			FamilyRecord rec = kv.Value;
			if (rec.DeathYear == null && rec.LastSeenYear >= 0 && year - rec.LastSeenYear >= MissingYearsBeforeDead)
			{
				rec.DeathYear = rec.LastSeenYear + 1;
				marked++;
			}
		}
		return marked;
	}

	/// <summary>Очистка при переполнении: вымываем древнейших давно умерших «листовых»
	/// записей (без известных детей), пока размер не вернётся под потолок.</summary>
	private void TrimIfOvercapacity()
	{
		if (_records.Count <= Capacity)
		{
			return;
		}
		List<long> removable = new List<long>();
		HashSet<long> parents = new HashSet<long>();
		foreach (FamilyRecord r in _records.Values)
		{
			if (r.FatherId != 0) parents.Add(r.FatherId);
			if (r.MotherId != 0) parents.Add(r.MotherId);
		}
		foreach (FamilyRecord r in _records.Values)
		{
			if (r.DeathYear == null || parents.Contains(r.Id))
			{
				continue;
			}
			removable.Add(r.Id);
		}
		removable.Sort();
		for (int i = 0; i < removable.Count && _records.Count > Capacity; i++)
		{
			_records.Remove(removable[i]);
		}
	}

	public bool TryGet(long id, out FamilyRecord rec)
	{
		return _records.TryGetValue(id, out rec);
	}

	/// <summary>Запись или null — удобство тестов и прямых запросов.</summary>
	public FamilyRecord GetById(long id)
	{
		return _records.TryGetValue(id, out FamilyRecord r) ? r : null;
	}

	/// <summary>Восстановление из сериализованных данных.</summary>
	public void Load(List<FamilyRecord> records)
	{
		_records.Clear();
		if (records == null)
		{
			return;
		}
		for (int i = 0; i < records.Count; i++)
		{
			FamilyRecord r = records[i];
			if (r != null && r.Id != 0)
			{
				_records[r.Id] = r;
			}
		}
	}

	/// <summary>Сериализация: полный список записей.</summary>
	public List<FamilyRecord> ToList()
	{
		return new List<FamilyRecord>(_records.Values);
	}

	/// <summary>Дети жителя (по записям реестра: родители детей указывают на него).</summary>
	public List<FamilyRecord> Children(long id)
	{
		List<FamilyRecord> result = new List<FamilyRecord>();
		if (id == 0)
		{
			return result;
		}
		foreach (FamilyRecord r in _records.Values)
		{
			if (r.FatherId == id || r.MotherId == id)
			{
				result.Add(r);
			}
		}
		result.Sort((a, b) => a.BirthYear.CompareTo(b.BirthYear));
		return result;
	}

	/// <summary>Братья и сёстры (общий родитель, без самого жителя).</summary>
	public List<FamilyRecord> Siblings(FamilyRecord rec)
	{
		List<FamilyRecord> result = new List<FamilyRecord>();
		if (rec == null || (rec.FatherId == 0 && rec.MotherId == 0))
		{
			return result;
		}
		foreach (FamilyRecord r in _records.Values)
		{
			if (r.Id == rec.Id)
			{
				continue;
			}
			bool sameFather = rec.FatherId != 0 && r.FatherId == rec.FatherId;
			bool sameMother = rec.MotherId != 0 && r.MotherId == rec.MotherId;
			if (sameFather && sameMother)
			{
				result.Add(r);
			}
		}
		result.Sort((a, b) => a.BirthYear.CompareTo(b.BirthYear));
		return result;
	}
}

/// <summary>Строители читаемого текста родословной (ядро отдачи UI строк).</summary>
public static class GenealogyTree
{
	/// <summary>
	/// Рендер родословной жителя: сам герой, родители, деды/бабки,
	/// дети с партнёрами, братья/сёстры. Покойные с пометкой «†» и датами.
	/// Максимум входа — amount строк (обрезка по важности, не по алфавиту).
	/// </summary>
	public static List<string> Build(FamilyRegistry reg, long id, int maxLines = 22)
	{
		List<string> lines = new List<string>();
		if (reg == null || !reg.TryGet(id, out FamilyRecord self))
		{
			return lines;
		}
		FamilyRecord father = GetOrNull(reg, self.FatherId);
		FamilyRecord mother = GetOrNull(reg, self.MotherId);
		List<FamilyRecord> children = reg.Children(id);
		List<FamilyRecord> siblings = reg.Siblings(self);

		lines.Add("■ " + Describe(self, selfName: true));
		AddRelative(lines, CoreText.L("RP_FAMILY_FATHER", "отец"), father);
		AddRelative(lines, CoreText.L("RP_FAMILY_MOTHER", "мать"), mother);
		FamilyRecord ff = GetOrNull(reg, father?.FatherId ?? 0);
		FamilyRecord fm = GetOrNull(reg, father?.MotherId ?? 0);
		FamilyRecord mf = GetOrNull(reg, mother?.FatherId ?? 0);
		FamilyRecord mm = GetOrNull(reg, mother?.MotherId ?? 0);
		AddRelative(lines, CoreText.L("RP_FAMILY_GRANDFATHER_P", "дед (по отцу)"), ff);
		AddRelative(lines, CoreText.L("RP_FAMILY_GRANDMOTHER_P", "бабка (по отцу)"), fm);
		AddRelative(lines, CoreText.L("RP_FAMILY_GRANDFATHER_M", "дед (по матери)"), mf);
		AddRelative(lines, CoreText.L("RP_FAMILY_GRANDMOTHER_M", "бабка (по матери)"), mm);
		// Глубина династии: отцовская линия до самого старшего известного предка.
		// За 200 лет жизни мира видно весь род сразу — не кликая по профилям умерших.
		List<FamilyRecord> chain = AncestorChain(reg, self);
		if (chain.Count > 2)
		{
			lines.Add(CoreText.L("RP_FAMILY_LINE", "линия рода ({count} покол.):").Replace("{count}", chain.Count.ToString()));
			StringBuilder sb = new StringBuilder();
			int chunk = 0;
			for (int i = chain.Count - 1; i >= 0; i--)
			{
				if (sb.Length > 0)
				{
					sb.Append(" → ");
				}
				sb.Append(ShortName(chain[i]));
				chunk++;
				if (chunk == 3 && i > 0)
				{
					lines.Add(sb.ToString());
					sb = new StringBuilder();
					chunk = 0;
				}
			}
			if (sb.Length > 0)
			{
				lines.Add(sb.ToString());
			}
		}
		if (children.Count > 0)
		{
			int shown = 0;
			for (int i = 0; i < children.Count && shown < 8; i++, shown++)
			{
				string partner = OtherParentName(reg, children[i], self);
				lines.Add((children[i].Female ? CoreText.L("RP_FAMILY_DAUGHTER", "дочь: ") : CoreText.L("RP_FAMILY_SON", "сын: "))
					+ Describe(children[i])
					+ (partner != null ? " — с " + partner : string.Empty));
			}
			if (children.Count > 8)
			{
				lines.Add("… и ещё " + (children.Count - 8) + " детей");
			}
		}
		if (siblings.Count > 0)
		{
			int shown = 0;
			for (int i = 0; i < siblings.Count && shown < 6; i++, shown++)
			{
				lines.Add((siblings[i].Female ? CoreText.L("RP_FAMILY_SISTER", "сестра: ") : CoreText.L("RP_FAMILY_BROTHER", "брат: ")) + Describe(siblings[i]));
			}
			if (siblings.Count > 6)
			{
				lines.Add("… и ещё " + (siblings.Count - 6) + " в семье");
			}
		}
		if (lines.Count > maxLines)
		{
			lines.RemoveRange(maxLines, lines.Count - maxLines);
		}
		return lines;
	}

	/// <summary>Отцовская линия вверх: self → отец → дед → … до крайнего известного предка (через мать, если отцовской ветки нет).</summary>
	public static List<FamilyRecord> AncestorChain(FamilyRegistry reg, FamilyRecord self)
	{
		List<FamilyRecord> chain = new List<FamilyRecord>();
		if (reg == null || self == null)
		{
			return chain;
		}
		FamilyRecord cur = self;
		HashSet<long> seen = new HashSet<long>();
		for (int depth = 0; depth < 32 && cur != null; depth++)
		{
			chain.Add(cur);
			seen.Add(cur.Id);
			FamilyRecord next = GetOrNull(reg, cur.FatherId);
			if (next == null)
			{
				next = GetOrNull(reg, cur.MotherId);
			}
			if (next == null || seen.Contains(next.Id))
			{
				break;
			}
			cur = next;
		}
		return chain;
	}

	/// <summary>Компактный вид для цепочки рода: имя и даты без скобок-отступов.</summary>
	private static string ShortName(FamilyRecord r)
	{
		if (r == null) return "?";
		if (r.BirthYear > 0 && r.DeathYear != null && r.DeathYear.Value > 0)
		{
			return "†" + r.Name + " (" + r.BirthYear + "–" + r.DeathYear.Value + ")";
		}
		if (r.BirthYear > 0)
		{
			return r.Name + " (" + r.BirthYear + "+)";
		}
		return r.Name;
	}

	private static FamilyRecord GetOrNull(FamilyRegistry reg, long id)
	{
		if (id == 0 || !reg.TryGet(id, out FamilyRecord r))
		{
			return null;
		}
		return r;
	}

	private static void AddRelative(List<string> lines, string title, FamilyRecord r)
	{
		if (r != null)
		{
			lines.Add(title + ": " + Describe(r));
		}
	}

	/// <summary>Имя второго родителя ребёнка (партнёр героя записями реестра).</summary>
	private static string OtherParentName(FamilyRegistry reg, FamilyRecord child, FamilyRecord self)
	{
		long otherId = (child.FatherId == self.Id) ? child.MotherId : child.FatherId;
		if (otherId != 0 && reg.TryGet(otherId, out FamilyRecord other))
		{
			return other.Name;
		}
		return null;
	}

	/// <summary>«† Имя (30–98)» для покойных, «Имя (род. 30)» для живых.</summary>
	public static string Describe(FamilyRecord r, bool selfName = false)
	{
		StringBuilder sb = new StringBuilder();
		if (r.DeathYear != null)
		{
			sb.Append("† ");
		}
		sb.Append(r.Name);
		if (r.BirthYear > 0)
		{
			sb.Append(" (");
			if (r.DeathYear != null && r.DeathYear.Value > 0)
			{
				sb.Append(r.BirthYear);
				sb.Append("–");
				sb.Append(r.DeathYear.Value);
			}
			else
			{
				sb.Append(CoreText.L("RP_FAMILY_BORN", "род. "));
				sb.Append(r.BirthYear);
			}
			sb.Append(")");
		}
		else if (r.DeathYear != null && r.DeathYear.Value > 0)
		{
			sb.Append(" (ум. ");
			sb.Append(r.DeathYear.Value);
			sb.Append(")");
		}
		return sb.ToString();
	}
}
