using System;
using System.Collections.Generic;

namespace RealPolitic.Core;

public static class Ideologies
{
	private static readonly Dictionary<IdeologyType, IdeologyDef> _defs;

	private static readonly IdeologyType[] _empty;

	public static IEnumerable<IdeologyDef> All => _defs.Values;

	static Ideologies()
	{
		_defs = new Dictionary<IdeologyType, IdeologyDef>();
		_empty = new IdeologyType[0];
		Add(new IdeologyDef(IdeologyType.Tribalism, -0.1f, 0.95f, 0.1f, 0.85f, 30, 0.5f, 0.2f, 0.8f, 1.4f, 0, 1, new IdeologyType[1] { IdeologyType.Egalitarianism }, new IdeologyType[2]
		{
			IdeologyType.Monarchy,
			IdeologyType.Technocracy
		}));
		Add(new IdeologyDef(IdeologyType.Monarchy, 0.15f, 1f, 0.1f, 0.95f, 80, 0.8f, 0.3f, 0.45f, 0.8f, 0, 3, new IdeologyType[2]
		{
			IdeologyType.Theocracy,
			IdeologyType.Aristocracy
		}, new IdeologyType[1] { IdeologyType.Egalitarianism }));
		Add(new IdeologyDef(IdeologyType.Theocracy, 0.25f, 0.9f, 0.05f, 0.85f, 70, 0.95f, 0.8f, 0.3f, 0.6f, 0, 2, new IdeologyType[1] { IdeologyType.Monarchy }, new IdeologyType[2]
		{
			IdeologyType.Technocracy,
			IdeologyType.Egalitarianism
		}));
		Add(new IdeologyDef(IdeologyType.Aristocracy, -0.05f, 1f, 0.15f, 0.95f, 55, 0.7f, 0.25f, 0.4f, 1f, 0, 2, new IdeologyType[2]
		{
			IdeologyType.Monarchy,
			IdeologyType.MerchantRepublic
		}, new IdeologyType[2]
		{
			IdeologyType.Egalitarianism,
			IdeologyType.Tribalism
		}));
		Add(new IdeologyDef(IdeologyType.MerchantRepublic, 0f, 1.1f, -0.1f, 1.05f, 55, 0.6f, 0.6f, 0.55f, 1.1f, 2, 4, new IdeologyType[2]
		{
			IdeologyType.Technocracy,
			IdeologyType.Aristocracy
		}, new IdeologyType[1] { IdeologyType.Militarism }));
		Add(new IdeologyDef(IdeologyType.Militarism, 0.1f, 0.9f, 0.6f, 0.9f, 60, 0.9f, 0.5f, 0.35f, 0.9f, 0, 2, new IdeologyType[1] { IdeologyType.Monarchy }, new IdeologyType[3]
		{
			IdeologyType.MerchantRepublic,
			IdeologyType.Egalitarianism,
			IdeologyType.Liberalism
		}));
		Add(new IdeologyDef(IdeologyType.Egalitarianism, 0.2f, 1.05f, -0.05f, 0.95f, 65, 0.85f, 0.7f, 0.7f, 1.3f, 3, 4, new IdeologyType[1] { IdeologyType.Technocracy }, new IdeologyType[3]
		{
			IdeologyType.Monarchy,
			IdeologyType.Aristocracy,
			IdeologyType.Theocracy
		}));
		Add(new IdeologyDef(IdeologyType.Technocracy, -0.15f, 1f, 0f, 1.25f, 50, 0.75f, 0.45f, 0.5f, 1.2f, 2, 4, new IdeologyType[2]
		{
			IdeologyType.MerchantRepublic,
			IdeologyType.Egalitarianism
		}, new IdeologyType[2]
		{
			IdeologyType.Theocracy,
			IdeologyType.Tribalism
		}));
		// Либерализм: свободная торговля и открытые границы, парламент решает о войне; терпим к инакомыслию, слаб в сплочённости.
		Add(new IdeologyDef(IdeologyType.Liberalism, -0.05f, 1.15f, -0.3f, 1.2f, 55, 0.55f, 0.55f, 0.8f, 1.25f, 2, 4, new IdeologyType[3]
		{
			IdeologyType.Democracy,
			IdeologyType.MerchantRepublic,
			IdeologyType.Technocracy
		}, new IdeologyType[3]
		{
			IdeologyType.Communism,
			IdeologyType.Ethnonationalism,
			IdeologyType.Militarism
		}));
		// 0.49: национализм. Низкая лояльность к хозяину (он для них чужой), высокая
		// сплочённость вокруг «своих», воинственность выше средней — но не фанатичная.
		Add(new IdeologyDef(IdeologyType.Nationalism, 0.05f, 1.0f, 0.3f, 0.95f, 55, 0.9f, 0.1f, 0.25f, 0.9f, 0, 2, new IdeologyType[2]
		{
			IdeologyType.Ethnonationalism,
			IdeologyType.Democracy
		}, new IdeologyType[3]
		{
			IdeologyType.Empire,
			IdeologyType.Khaganate,
			IdeologyType.Monarchy
		}));
		Add(new IdeologyDef(IdeologyType.Democracy, 0.05f, 1.15f, -0.2f, 1.25f, 60, 0.65f, 0.5f, 0.75f, 1.2f, 1, 4, new IdeologyType[3]
		{
			IdeologyType.Egalitarianism,
			IdeologyType.MerchantRepublic,
			IdeologyType.Technocracy
		}, new IdeologyType[3]
		{
			IdeologyType.Ethnonationalism,
			IdeologyType.Militarism,
			IdeologyType.Theocracy
		}));
		Add(new IdeologyDef(IdeologyType.Communism, 0.15f, 1.2f, 0.2f, 1.15f, 65, 0.85f, 0.8f, 0.25f, 1.4f, 3, 4, new IdeologyType[1]
		{
			IdeologyType.Egalitarianism
		}, new IdeologyType[4]
		{
			IdeologyType.MerchantRepublic,
			IdeologyType.Aristocracy,
			IdeologyType.Ethnonationalism,
			IdeologyType.Monarchy
		}));
		Add(new IdeologyDef(IdeologyType.Ethnonationalism, 0.25f, 1.0f, 0.45f, 0.9f, 75, 0.95f, -0.2f, 0.15f, 0.6f, 0, 2, new IdeologyType[2]
		{
			IdeologyType.Militarism,
			IdeologyType.Monarchy
		}, new IdeologyType[4]
		{
			IdeologyType.Democracy,
			IdeologyType.Communism,
			IdeologyType.Egalitarianism,
			IdeologyType.MerchantRepublic
		}));
		Add(new IdeologyDef(IdeologyType.Khaganate, 0.30f, 0.90f, 0.35f, 0.85f, 74, 0.85f, 0.10f, 0.15f, 0.50f, 0, 2, new IdeologyType[4]
		{
			IdeologyType.Monarchy,
			IdeologyType.Militarism,
			IdeologyType.Tribalism,
			IdeologyType.Empire
		}, new IdeologyType[4]
		{
			IdeologyType.Democracy,
			IdeologyType.Liberalism,
			IdeologyType.Communism,
			IdeologyType.MerchantRepublic
		}));
		Add(new IdeologyDef(IdeologyType.Empire, 0.40f, 0.85f, 0.65f, 0.85f, 82, 0.75f, 0.10f, 0.20f, 0.55f, 0, 2, new IdeologyType[4]
		{
			IdeologyType.Monarchy,
			IdeologyType.Aristocracy,
			IdeologyType.Militarism,
			IdeologyType.Khaganate
		}, new IdeologyType[4]
		{
			IdeologyType.FreeTerritory,
			IdeologyType.Egalitarianism,
			IdeologyType.Communism,
			IdeologyType.Tribalism
		}));
		Add(new IdeologyDef(IdeologyType.FreeTerritory, 0.90f, 0.55f, 0.45f, 0.70f, 66, 0.40f, 0.25f, 0.30f, 0.35f, 2, 4, new IdeologyType[3]
		{
			IdeologyType.Egalitarianism,
			IdeologyType.Liberalism,
			IdeologyType.Tribalism
		}, new IdeologyType[5]
		{
			IdeologyType.Empire,
			IdeologyType.Monarchy,
			IdeologyType.Ethnonationalism,
			IdeologyType.Theocracy,
			IdeologyType.Aristocracy
		}));
	}

	private static void Add(IdeologyDef def)
	{
		_defs[def.Type] = def;
	}

	public static IdeologyDef Get(IdeologyType type)
	{
		if (!_defs.TryGetValue(type, out var value))
		{
			return _defs[IdeologyType.Monarchy];
		}
		return value;
	}

	public static float Affinity(IdeologyType a, IdeologyType b)
	{
		if (a == b)
		{
			return 1f;
		}
		IdeologyDef ideologyDef = Get(a);
		if (ideologyDef.IsRival(b))
		{
			return -1f;
		}
		if (ideologyDef.IsAlly(b))
		{
			return 0.6f;
		}
		IdeologyDef ideologyDef2 = Get(b);
		float num = Math.Abs(ideologyDef.WarAppetite - ideologyDef2.WarAppetite);
		float num2 = Math.Abs(ideologyDef.Tech - ideologyDef2.Tech);
		float num3 = Math.Abs(ideologyDef.Cohesion - ideologyDef2.Cohesion);
		return 0.2f - 0.15f * (num + num2 + num3);
	}

	/// <summary>
	/// Стартовая идеология нового государства. Раньше все новые державы жёстко
	/// получали монархию — мир выходил почти сплошь монархическим. Теперь бросок
	/// со взвешиванием: «архаичные» строи заметно вероятнее, поздние (технократия,
	/// торговая республика, модерн) — редки, как и положено на заре мира.
	/// </summary>
	public static IdeologyType RollStarting(Random rng)
	{
		// 0.31.12: стартовый пул — только племена и каганаты. Монархии, военные и прочие строи
		// появляются позже: через цивилизацию, реформы и смены строя.
		if (rng == null)
		{
			return IdeologyType.Tribalism;
		}
		return rng.Next(3) == 0 ? IdeologyType.Khaganate : IdeologyType.Tribalism;
	}

	/// <summary>Допустим ли строй при старте мира (только племя и каганат).</summary>
	public static bool IsStartingIdeology(IdeologyType ideology)
		=> ideology == IdeologyType.Tribalism || ideology == IdeologyType.Khaganate;

	/// <summary>
	/// Бросок стартового строя из архаичного пула (племена и монархии преобладают,
	/// теократии/аристократии и прочие древние строи встречаются регулярно).
	/// Модерные идеологии (технократия, демократия, коммунизм, этнонационализм)
	/// в стартовый пул не входят — до них державы дорастают реформами.
	/// </summary>
	public static IdeologyType RollArchaic(Random rng)
	{
		if (rng == null)
		{
			return IdeologyType.Tribalism;
		}
		IdeologyType[] pool = new IdeologyType[8]
		{
			IdeologyType.Tribalism,
			IdeologyType.Monarchy,
			IdeologyType.Khaganate,
			IdeologyType.FreeTerritory,
			IdeologyType.Theocracy,
			IdeologyType.Aristocracy,
			IdeologyType.Militarism,
			IdeologyType.MerchantRepublic
		};
		int[] weights = new int[8] { 24, 24, 12, 10, 12, 10, 8, 6 };
		int total = 0;
		for (int i = 0; i < weights.Length; i++)
		{
			total += weights[i];
		}
		int roll = rng.Next(total);
		for (int i = 0; i < pool.Length; i++)
		{
			roll -= weights[i];
			if (roll < 0)
			{
				return pool[i];
			}
		}
		return IdeologyType.Tribalism;
	}

	/// <summary>
	/// Династический брак возможен только между наследственными монархическими
	/// формами: монархией, империей и каганатом. Планировщик, поиск кандидата и
	/// само заключение брака обязаны пользоваться одним этим правилом.
	/// </summary>
	public static bool CanDynasticMarriage(IdeologyType type)
	{
		return type == IdeologyType.Monarchy
			|| type == IdeologyType.Empire
			|| type == IdeologyType.Khaganate;
	}

	public static string NameKey(IdeologyType type)
	{
		return "RP_IDEOLOGY_" + type.ToString().ToUpperInvariant();
	}

	/// <summary>Ключ локализации понятного описания идеологии (RP_IDEOLOGY_*_DESC).</summary>
	public static string DescriptionKey(IdeologyType type)
	{
		return NameKey(type) + "_DESC";
	}

	/// <summary>Ключ локализации названия государства в предложном падеже (для «В {state} {name}…»): RP_STATEIN_*.</summary>
	public static string StateInKey(IdeologyType type)
	{
		return "RP_STATEIN_" + type.ToString().ToUpperInvariant();
	}

	/// <summary>Ключ локализации названия государства в именительном падеже (для летописи): RP_STATE_*.</summary>
	public static string StateNameKey(IdeologyType type)
	{
		return "RP_STATE_" + type.ToString().ToUpperInvariant();
	}

	/// <summary>Ключ локализации титула правителя с учётом пола: RP_TITLE_*_M / RP_TITLE_*_F.</summary>
	public static string RulerTitleKey(IdeologyType type, bool female)
	{
		return "RP_TITLE_" + type.ToString().ToUpperInvariant() + (female ? "_F" : "_M");
	}

	/// <summary>Ключ локализации иконки идеологии (rp_ideology_*).</summary>
	public static string IconKey(IdeologyType type)
	{
		return "rp_ideology_" + type.ToString().ToLowerInvariant();
	}

	/// <summary>
	/// Фиксированный цвет идеологии для слоя карты (RGB 0..255).
	/// Один цвет на идеологию — при включённом слое ВСЕ государства с этой
	/// идеологией красятся одинаково, независимо от собственных цветов.
	/// Подобраны контрастные, различимые между собой оттенки.
	/// </summary>
	public static void LayerColor(IdeologyType type, out byte r, out byte g, out byte b)
	{
		switch (type)
		{
		case IdeologyType.Tribalism:       r = 141; g =  94; b =  48; break; // охра / земля
		case IdeologyType.Monarchy:        r = 168; g =  46; b =  56; break; // багряный
		case IdeologyType.Theocracy:       r = 226; g = 190; b =  62; break; // золотой
		case IdeologyType.Aristocracy:     r = 118; g =  70; b = 160; break; // пурпурный
		case IdeologyType.MerchantRepublic:r =  40; g = 150; b = 136; break; // бирюзовый
		case IdeologyType.Militarism:      r =  90; g =  96; b = 104; break; // сталь
		case IdeologyType.Egalitarianism:  r = 214; g = 106; b =  42; break; // оранжевый
		case IdeologyType.Technocracy:     r =  48; g = 122; b = 200; break; // синий
		case IdeologyType.Liberalism:      r = 240; g = 176; b =  48; break; // золотисто-жёлтый
		case IdeologyType.Democracy:       r =  52; g = 152; b = 219; break; // лазурный / синий
		case IdeologyType.Communism:       r = 214; g =  40; b =  40; break; // кумачовый / красный
		case IdeologyType.Ethnonationalism:r =  92; g =  64; b =  51; break; // умбра / бронза
		case IdeologyType.Khaganate:       r = 196; g = 132; b =  45; break; // степное золото / янтарь
		case IdeologyType.Empire:          r = 142; g =  36; b = 106; break; // имперский пурпур
		case IdeologyType.FreeTerritory:   r =  46; g =  58; b =  52; break; // вольница / тёмно-изумрудный графит
		case IdeologyType.Nationalism:     r = 176; g =  98; b =  34; break; // тёмная медь / знамя восставших
		default:                           r = 150; g = 150; b = 150; break;
		}
	}

	public static string StanceKey(ReformStance stance)
	{
		return "RP_STANCE_" + stance.ToString().ToUpperInvariant();
	}

	public static string UnionTypeKey(UnionType type)
	{
		return "RP_UNIONTYPE_" + type.ToString().ToUpperInvariant();
	}
}
