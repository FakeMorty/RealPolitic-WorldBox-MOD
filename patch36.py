import json, collections

# ---------- 1. Какими статьями конституция задаёт палаты — и как их можно поменять.
p = 'src/RealPolitic.Core/ConstitutionAmendments.cs'
s = open(p, encoding='utf-8').read()

old = '''	public const string ArticleUnion = "RP_CONST_ART_UNION";'''
assert s.count(old) == 1
s = s.replace(old, '''	public const string ArticleUnion = "RP_CONST_ART_UNION";
	// 0.53: четыре статьи о палатах. Владелец 30.10.2026: сколько палат, от чего верхняя,
	// есть ли вето правителя и можно ли палаты распустить — всё это решает конституция.
	public const string ArticleChambers = "RP_CONST_ART_CHAMBERS";
	public const string ArticleUpperBasis = "RP_CONST_ART_UPPERBASIS";
	public const string ArticleVeto = "RP_CONST_ART_VETO";
	public const string ArticleDissolve = "RP_CONST_ART_DISSOLVE";''', 1)

old = '''			// Правитель, стеснённый обязательной грамотой, может низвести её до обычая.'''
assert s.count(old) == 1
s = s.replace(old, '''			// 0.53: закручивание — власть забирают у собрания.
			if (c.Chambers == ChamberCount.Two)
				list.Add(Make(ArticleChambers, "RP53_CHAMBERS_TWO", "RP53_CHAMBERS_ONE", true, x => x.Chambers = ChamberCount.One));
			if (c.UpperBasis == UpperChamberBasis.Peoples)
				list.Add(Make(ArticleUpperBasis, "RP53_BASIS_PEOPLES", "RP53_BASIS_CITIES", true, x => x.UpperBasis = UpperChamberBasis.Cities));
			else if (c.UpperBasis == UpperChamberBasis.Cities)
				list.Add(Make(ArticleUpperBasis, "RP53_BASIS_CITIES", "RP53_BASIS_CLANS", true, x => x.UpperBasis = UpperChamberBasis.Clans));
			if (!c.RulerVeto)
				list.Add(Make(ArticleVeto, "RP53_OFF", "RP53_ON", true, x => x.RulerVeto = true));
			if (!c.RulerMayDissolve)
				list.Add(Make(ArticleDissolve, "RP53_OFF", "RP53_ON", true, x => x.RulerMayDissolve = true));
			// Правитель, стеснённый обязательной грамотой, может низвести её до обычая.''', 1)

old = '''			if (c.Borders == BorderLaw.Closed)'''
assert s.count(old) == 1
s = s.replace(old, '''			// 0.53: ослабление — власть отдают собранию.
			if (c.Chambers == ChamberCount.One)
				list.Add(Make(ArticleChambers, "RP53_CHAMBERS_ONE", "RP53_CHAMBERS_TWO", false, x => x.Chambers = ChamberCount.Two));
			if (c.UpperBasis == UpperChamberBasis.Clans)
				list.Add(Make(ArticleUpperBasis, "RP53_BASIS_CLANS", "RP53_BASIS_CITIES", false, x => x.UpperBasis = UpperChamberBasis.Cities));
			else if (c.UpperBasis == UpperChamberBasis.Cities)
				list.Add(Make(ArticleUpperBasis, "RP53_BASIS_CITIES", "RP53_BASIS_PEOPLES", false, x => x.UpperBasis = UpperChamberBasis.Peoples));
			if (c.RulerVeto)
				list.Add(Make(ArticleVeto, "RP53_ON", "RP53_OFF", false, x => x.RulerVeto = false));
			if (c.RulerMayDissolve)
				list.Add(Make(ArticleDissolve, "RP53_ON", "RP53_OFF", false, x => x.RulerMayDissolve = false));
			if (c.Borders == BorderLaw.Closed)''', 1)
open(p, 'w', encoding='utf-8').write(s)

# ---------- 2. Окно конституции: четыре новые статьи.
p = 'src/Game/ConstitutionWindow.cs'
s = open(p, encoding='utf-8').read()
old = '''			GameText.Localized("RP_CONST_ART_LOCAL", "О местных уставах")
		};'''
assert s.count(old) == 1
s = s.replace(old, '''			GameText.Localized("RP_CONST_ART_LOCAL", "О местных уставах"),
			// 0.53: палаты.
			GameText.Localized("RP_CONST_ART_CHAMBERS", "О палатах"),
			GameText.Localized("RP_CONST_ART_UPPERBASIS", "О совете земель"),
			GameText.Localized("RP_CONST_ART_VETO", "О вето правителя"),
			GameText.Localized("RP_CONST_ART_DISSOLVE", "О роспуске палат")
		};''', 1)
old = '''			LocalCharterLawText(c)
		};'''
assert s.count(old) == 1
s = s.replace(old, '''			LocalCharterLawText(c),
			ChambersLawText(c),
			UpperBasisLawText(c),
			(c.RulerVeto
				? GameText.Localized("RP53_CONST_VETO_YES", "Правитель может наложить вето на принятый палатами закон; преодолеть его можно двумя третями голосов.")
				: GameText.Localized("RP53_CONST_VETO_NO", "Вето у правителя нет: что приняли палаты, то и закон.")),
			(c.RulerMayDissolve
				? GameText.Localized("RP53_CONST_DISSOLVE_YES", "Правитель может распустить палаты.")
				: GameText.Localized("RP53_CONST_DISSOLVE_NO", "Распустить палаты правитель не может."))
		};''', 1)

old = '''		private static string LocalCharterLawText(Constitution c)'''
assert s.count(old) == 1
s = s.replace(old, '''		/// <summary>0.53: сколько палат и как они называются.</summary>
		private static string ChambersLawText(Constitution c)
		{
			return c.Chambers == ChamberCount.Two
				? GameText.Localized("RP53_CONST_CHAMBERS_TWO",
					"Две палаты: народное собрание (сто мест) и совет земель. Закон проходит обе.")
				: GameText.Localized("RP53_CONST_CHAMBERS_ONE",
					"Одна палата: народное собрание (сто мест). Совет земель не собирается.");
		}

		/// <summary>0.53: от чего считается совет земель.</summary>
		private static string UpperBasisLawText(Constitution c)
		{
			switch (c.UpperBasis)
			{
				case UpperChamberBasis.Peoples:
					return GameText.Localized("RP53_CONST_UB_PEOPLES",
						"В совете земель голос у каждого народа державы — малому народу защита.");
				case UpperChamberBasis.Clans:
					return GameText.Localized("RP53_CONST_UB_CLANS",
						"В совете земель голос у знатных родов: чем древнее род, тем больше весу.");
				default:
					return GameText.Localized("RP53_CONST_UB_CITIES",
						"В совете земель голос у каждого города — один город, один голос.");
			}
		}

		private static string LocalCharterLawText(Constitution c)''', 1)
open(p, 'w', encoding='utf-8').write(s)

def patch(path, pairs):
    d = json.loads(open(path, encoding='utf-8').read(), object_pairs_hook=collections.OrderedDict)
    for k, v in pairs.items():
        d[k] = v
    open(path, 'w', encoding='utf-8').write(json.dumps(d, ensure_ascii=False, indent=2) + "\n")

patch('Locales/ru.json', {
    "RP_CONST_ART_CHAMBERS": "О палатах",
    "RP_CONST_ART_UPPERBASIS": "О совете земель",
    "RP_CONST_ART_VETO": "О вето правителя",
    "RP_CONST_ART_DISSOLVE": "О роспуске палат",
    "RP53_CONST_CHAMBERS_TWO": "Две палаты: народное собрание (сто мест) и совет земель. Закон проходит обе.",
    "RP53_CONST_CHAMBERS_ONE": "Одна палата: народное собрание (сто мест). Совет земель не собирается.",
    "RP53_CONST_UB_CITIES": "В совете земель голос у каждого города — один город, один голос.",
    "RP53_CONST_UB_PEOPLES": "В совете земель голос у каждого народа державы — малому народу защита.",
    "RP53_CONST_UB_CLANS": "В совете земель голос у знатных родов: чем древнее род, тем больше весу.",
    "RP53_CONST_VETO_YES": "Правитель может наложить вето на принятый палатами закон; преодолеть его можно двумя третями голосов.",
    "RP53_CONST_VETO_NO": "Вето у правителя нет: что приняли палаты, то и закон.",
    "RP53_CONST_DISSOLVE_YES": "Правитель может распустить палаты.",
    "RP53_CONST_DISSOLVE_NO": "Распустить палаты правитель не может.",
})
patch('Locales/en.json', {
    "RP_CONST_ART_CHAMBERS": "On the chambers",
    "RP_CONST_ART_UPPERBASIS": "On the council of lands",
    "RP_CONST_ART_VETO": "On the ruler's veto",
    "RP_CONST_ART_DISSOLVE": "On dissolving the chambers",
    "RP53_CONST_CHAMBERS_TWO": "Two chambers: the people's assembly (one hundred seats) and the council of lands. A bill passes both.",
    "RP53_CONST_CHAMBERS_ONE": "One chamber: the people's assembly (one hundred seats). The council of lands does not convene.",
    "RP53_CONST_UB_CITIES": "In the council of lands every city has a vote — one city, one vote.",
    "RP53_CONST_UB_PEOPLES": "In the council of lands every people of the realm has a vote — a shield for small peoples.",
    "RP53_CONST_UB_CLANS": "In the council of lands noble clans vote: the older the clan, the greater its weight.",
    "RP53_CONST_VETO_YES": "The ruler may veto a bill passed by the chambers; two thirds can override it.",
    "RP53_CONST_VETO_NO": "The ruler has no veto: whatever the chambers pass becomes law.",
    "RP53_CONST_DISSOLVE_YES": "The ruler may dissolve the chambers.",
    "RP53_CONST_DISSOLVE_NO": "The ruler may not dissolve the chambers.",
})
print('ok')
