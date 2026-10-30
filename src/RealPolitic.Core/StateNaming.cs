using System;
using System.Collections.Generic;

namespace RealPolitic.Core;

/// <summary>
/// Правила имён государств (подтверждены владельцем 28.10.2026).
///
/// Коренное имя — «Гогиш» — это внутреннее имя страны. Оно никогда не меняется при смене строя
/// и используется там, где имя страны склеивается с другим словом:
///   • название войны («Гогишско-Вагская война»),
///   • название союза («Гогиш-Вагский союз»),
///   • любые составные конструкции.
///
/// Полное имя — «Королевство Гогиш», «Гогишский Рейх» — это коренное имя плюс ярлык строя.
/// Оно показывается на карте, в окнах государства и в нашей летописи.
///
/// Ярлык не один на идеологию: у каждой идеологии их около двадцати («Королевство», «Княжество»,
/// «Царство», «Герцогство», «Корона», «Престол»…), с правильным склонением коренного имени
/// («Гогишский Рейх», «Гогишская Империя», «Королевство Гогиш»). Конкретная форма выбирается
/// один раз по номеру государства и дальше не меняется — иначе имя дёргалось бы каждый год.
///
/// Племена — особый случай: у них нет ярлыка, их имя и есть название народа,
/// построенное по биому, культуре или городу («Луговики», «Чащобники», «Беляне»).
/// </summary>
public static class StateNaming
{
	/// <summary>Государство-племя: имя государства — этноним, ярлык не добавляется.</summary>
	public static bool HasLabel(IdeologyType ideology)
	{
		return ideology != IdeologyType.Tribalism;
	}

	/// <summary>Коренное имя: «Королевство Гогиш» → «Гогиш», «Гогишский Рейх» → «Гогиш».</summary>
	/// <summary>
	/// Убирает из шаблона приставку «({state})», если в самом имени державы уже есть титул.
	/// В 0.31.18 имя на карте — «Каганат Гогиш», и шаблон «Государство {kingdom} ({state})»
	/// давал «Государство Каганат Гогиш (каганат)» — строй назывался дважды.
	/// У племён и у старых сохранений без титула скобки остаются.
	/// </summary>
	public static string DropFormIfTitled(string template, string kingdomName)
	{
		if (string.IsNullOrEmpty(template))
		{
			return string.Empty;
		}
		if (string.IsNullOrWhiteSpace(kingdomName) || !IdeologyStateNames.HasIdeologyLabel(kingdomName))
		{
			return template;
		}
		return template.Replace("{kingdom} ({state})", "{kingdom}");
	}

	public static string Root(string name)
	{
		if (string.IsNullOrWhiteSpace(name))
		{
			return string.Empty;
		}
		string trimmed = name.Trim();
		string core = IdeologyStateNames.StripAllTitles(trimmed);
		// Ярлыка нет — имя не трогаем: «Гогиш» остаётся «Гогишем»,
		// а не превращается в «Гогишию» на ровном месте.
		if (string.IsNullOrWhiteSpace(core) || string.Equals(core, trimmed, StringComparison.Ordinal))
		{
			return trimmed;
		}
		return UndoAdjective(core.Trim());
	}

	/// <summary>
	/// Полное имя для карты, окон и летописи: «Королевство Гогиш», «Гогишский Рейх», «Каганат Гогиш».
	/// Форма постоянна для государства: одинаковый kingdomId всегда даёт одинаковое имя.
	/// Для племени возвращается этноним без ярлыка.
	/// </summary>
	public static string MapName(IdeologyType ideology, string name, int kingdomId, string capitalName = null)
	{
		if (string.IsNullOrWhiteSpace(name) || !HasLabel(ideology))
		{
			return name ?? string.Empty;
		}
		string root = Root(name);
		if (string.IsNullOrWhiteSpace(root) || root.StartsWith("#", StringComparison.Ordinal))
		{
			return name;
		}
		List<string> variants = IdeologyStateNames.GetAllVariants(ideology, root, capitalName);
		if (variants == null || variants.Count == 0)
		{
			return root;
		}
		// Предпочитаем формы, где коренное имя читается целиком: «Королевство Гогиш»,
		// «Гогишский Рейх». Формы, где от имени остался только хвост («Пикунская Орда»),
		// берём в последнюю очередь — из них потом не собрать «Пикунско-Вагскую войну».
		List<string> readable = new List<string>(variants.Count);
		foreach (string variant in variants)
		{
			// Только настоящие государственные титулы и только те формы,
			// где коренное имя читается целиком.
			if (IsPlausibleStateName(variant) && variant.IndexOf(root, StringComparison.OrdinalIgnoreCase) >= 0)
			{
				readable.Add(variant);
			}
		}
		if (readable.Count > 0)
		{
			variants = readable;
		}
		// Устойчивый выбор: одно и то же государство всегда получает одну и ту же форму.
		int index = (int)((uint)(kingdomId * 2654435761u) % (uint)variants.Count);
		string candidate = variants[index];
		if (string.IsNullOrWhiteSpace(candidate))
		{
			return root;
		}
		// 0.48.4: страховка от удвоения титула. Если в готовом имени два раза подряд
		// повторяется одна и та же пара слов («Независимое Королевство Независимое
		// Королевство Край»), лишнее убираем — повтор и есть лишнее.
		return CollapseRepeatedPhrase(candidate);
	}

	/// <summary>
	/// Убирает повтор одной и той же фразы подряд: «А Б А Б В» → «А Б В».
	/// Нужно на случай, когда имя державы уже содержит титул, а мод добавляет свой.
	/// </summary>
	public static string CollapseRepeatedPhrase(string name)
	{
		if (string.IsNullOrWhiteSpace(name))
		{
			return name ?? string.Empty;
		}
		string[] words = name.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
		if (words.Length < 2)
		{
			return name;
		}
		List<string> result = new List<string>(words.Length);
		int i = 0;
		while (i < words.Length)
		{
			// Ищем повтор блока из 1–3 слов: слова[i..i+k) == слова[i+k..i+2k).
			int k = 0;
			for (int size = 1; size <= 3 && i + 2 * size <= words.Length; size++)
			{
				bool same = true;
				for (int j = 0; j < size; j++)
				{
					if (!string.Equals(words[i + j], words[i + size + j], StringComparison.OrdinalIgnoreCase))
					{
						same = false;
						break;
					}
				}
				if (same)
				{
					k = size;
					break;
				}
			}
			if (k > 0 && result.Count + k + (words.Length - i - 2 * k) >= 1)
			{
				// Оставляем один экземпляр повтора и идём дальше.
				for (int j = 0; j < k; j++)
				{
					result.Add(words[i + j]);
				}
				i += 2 * k;
				continue;
			}
			result.Add(words[i]);
			i++;
		}
		return string.Join(" ", result);
	}

	/// <summary>
	/// Слова, которых в названии государства быть не может: это описание строя,
	/// а не государственный титул. «Гогишская Монархия», «Гогишская Палата Лордов»
	/// в реальном мире не существовали — и у нас не появятся.
	/// </summary>
	private static readonly string[] Forbidden =
	{
		"Монархия", "Самодержавие", "Двоевластие", "Палата", "Сход", "Поле", "Пакт", "Трон",
		"Корона", "Престол", "Империум", "Вольница", "Биржевой", "Ярмарочн", "Клан", "Магистров",
		"Лордов", "Чистой Крови", "Гильдия", "Вольное Общество"
	};

	/// <summary>Похоже ли имя на настоящее название государства (а не на описание строя).</summary>
	public static bool IsPlausibleStateName(string name)
	{
		if (string.IsNullOrWhiteSpace(name))
		{
			return false;
		}
		foreach (string word in Forbidden)
		{
			if (name.IndexOf(word, StringComparison.OrdinalIgnoreCase) >= 0)
			{
				return false;
			}
		}
		return true;
	}

	/// <summary>Ярлык строя в этом имени (для проверок и подсказок): «Королевство», «Рейх», …</summary>
	public static string LabelOf(IdeologyType ideology, string name, string capitalName = null)
	{
		if (!HasLabel(ideology) || string.IsNullOrWhiteSpace(name) || !IsPlausibleStateName(name))
		{
			return string.Empty;
		}
		string full = name.Trim();
		string root = Root(full);
		if (string.IsNullOrWhiteSpace(root))
		{
			return string.Empty;
		}
		// Форма «Титул Имя» или «Имя Титул»: титул — то, что осталось после удаления имени.
		string withoutWhole = RemoveWord(full, root).Trim();
		if (!string.IsNullOrWhiteSpace(withoutWhole) && withoutWhole != full && withoutWhole.IndexOf(root, StringComparison.Ordinal) < 0)
		{
			return withoutWhole;
		}
		// Форма со склонением: «Гогишский Рейх» — титул это последнее слово.
		string[] words = full.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
		if (words.Length >= 2)
		{
			return words[words.Length - 1];
		}
		return string.Empty;
	}

	/// <summary>
	/// «Гогишский» → «Гогиш», «Пикунская» → «Пикун»: возвращает основу имени без падежного окончания.
	/// Составные имена («Серебряный Яр») не трогаем.
	/// </summary>
	private static string UndoAdjective(string word)
	{
		if (string.IsNullOrEmpty(word) || word.IndexOf(' ') >= 0 || word.IndexOf('-') >= 0)
		{
			return word;
		}
		string[] endings = { "ский", "ская", "ское", "ские", "ый", "ий", "ой", "ая", "яя", "ое", "ее", "ие", "ые" };
		foreach (string ending in endings)
		{
			if (word.Length > ending.Length + 2 && word.EndsWith(ending, StringComparison.OrdinalIgnoreCase))
			{
				return word.Substring(0, word.Length - ending.Length);
			}
		}
		return word;
	}

	private static string RemoveWord(string whole, string part)
	{
		int at = whole.IndexOf(part, StringComparison.Ordinal);
		return at < 0 ? whole : whole.Remove(at, part.Length);
	}
}
