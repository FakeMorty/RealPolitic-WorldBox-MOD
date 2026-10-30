import json, collections

# ---------- сохранение: статьи конституции
p = 'src/RealPolitic.Core/SaveData.cs'
s = open(p, encoding='utf-8').read()
old = '''				writer.Key("un").Value((int)kingdom.Constitution.Union);'''
assert s.count(old) == 1
s = s.replace(old, '''				writer.Key("un").Value((int)kingdom.Constitution.Union);
				// 0.52: статьи о народах.
				writer.Key("rc").Value((int)kingdom.Constitution.Race);
				writer.Key("lg").Value((int)kingdom.Constitution.Language);
				writer.Key("ct").Value((int)kingdom.Constitution.Court);
				writer.Key("alc").Value(kingdom.Constitution.AllowLocalConstitutions);
				writer.Key("tlc").Value(kingdom.Constitution.TolerateLocalContradictions);
				writer.Key("fra").Value(kingdom.Constitution.ForeignRulerAllowed);''', 1)

old2 = '''				writer.Key("vic").Value(movement.Victories);
					writer.EndObject();
				}
			}'''
# запись народов и уставов — в конец блока державы (после национализма)
old3 = '''			// 0.49.1: расовое движение — чужая раса, которая рвётся к власти.'''
assert s.count(old3) == 1
s = s.replace(old3, '''			// 0.52: народы державы — когда пришли и на каком языке говорят.
			{
				Dictionary<string, PeopleRecord> book = world.PeoplesOf(kingdom.KingdomId);
				if (book != null && book.Count > 0)
				{
					writer.Key("peo").BeginArray();
					foreach (PeopleRecord record in book.Values)
					{
						writer.BeginObject();
						writer.Key("r").Value(record.RaceId ?? string.Empty);
						writer.Key("n").Value(record.RaceName ?? string.Empty);
						writer.Key("fy").Value(record.FirstSeenYear);
						writer.Key("st").Value((int)record.Stage);
						writer.Key("sy").Value(record.StageSinceYear);
						writer.Key("ry").Value(record.RequestedYear);
						writer.EndObject();
					}
					writer.EndArray();
				}
			}
			// 0.49.1: расовое движение — чужая раса, которая рвётся к власти.''', 1)

# чтение статей
old4 = '''						kingdomPolitics.Constitution.Union = (UnionLaw)nodeConstitution.GetInt("un");'''
assert s.count(old4) == 1
s = s.replace(old4, old4 + '''
						// 0.52: статьи о народах (в старых сейвах их нет — берём терпимые).
						kingdomPolitics.Constitution.Race = (RaceLaw)nodeConstitution.GetInt("rc", (int)RaceLaw.Equal);
						kingdomPolitics.Constitution.Language = (LanguageLaw)nodeConstitution.GetInt("lg", (int)LanguageLaw.Free);
						kingdomPolitics.Constitution.Court = (CourtLaw)nodeConstitution.GetInt("ct", (int)CourtLaw.Own);
						kingdomPolitics.Constitution.AllowLocalConstitutions = nodeConstitution.GetBool("alc", true);
						kingdomPolitics.Constitution.TolerateLocalContradictions = nodeConstitution.GetBool("tlc", true);
						kingdomPolitics.Constitution.ForeignRulerAllowed = nodeConstitution.GetBool("fra", true);''', 1)

# чтение народов
old5 = '''					// 0.49.1: восстановление расового движения.'''
assert s.count(old5) == 1
s = s.replace(old5, '''					// 0.52: восстановление народов державы.
					TinyJson.Node nodePeoples = node3.Get("peo");
					if (nodePeoples != null && !nodePeoples.IsNull)
					{
						Dictionary<string, PeopleRecord> book = world.PeoplesOf(kingdomPolitics.KingdomId);
						if (book != null)
						{
							for (int pIdx = 0; pIdx < nodePeoples.Arr.Count; pIdx++)
							{
								TinyJson.Node pn = nodePeoples.Arr[pIdx];
								string raceId = pn.GetString("r", string.Empty);
								if (string.IsNullOrEmpty(raceId))
								{
									continue;
								}
								book[raceId] = new PeopleRecord
								{
									RaceId = raceId,
									RaceName = pn.GetString("n", string.Empty),
									FirstSeenYear = pn.GetInt("fy", 0),
									Stage = (LanguageStage)pn.GetInt("st", 0),
									StageSinceYear = pn.GetInt("sy", 0),
									RequestedYear = pn.GetInt("ry", 0)
								};
							}
						}
					}
					// 0.49.1: восстановление расового движения.''', 1)

# запись местных уставов — рядом с запретами
old6 = '''		// 0.50: пятна за отнятое гражданство (мир помнит пятьдесят лет).'''
assert s.count(old6) == 1
s = s.replace(old6, '''		// 0.52: местные конституции городов.
		if (world.LocalConstitutions.Count > 0)
		{
			writer.Key("locals").BeginArray();
			foreach (LocalConstitution local in world.LocalConstitutions.Values)
			{
				writer.BeginObject();
				writer.Key("c").Value(local.CityId);
				writer.Key("k").Value(local.KingdomId);
				writer.Key("r").Value(local.RaceId ?? string.Empty);
				writer.Key("n").Value(local.RaceName ?? string.Empty);
				writer.Key("fy").Value(local.FoundedYear);
				writer.Key("b").Value(local.Banned);
				writer.Key("by").Value(local.BannedYear);
				writer.Key("wl").Value(local.WantsLanguage);
				writer.Key("wc").Value(local.WantsCourt);
				writer.Key("g").Value(local.Grievance);
				writer.EndObject();
			}
			writer.EndArray();
		}
		// 0.50: пятна за отнятое гражданство (мир помнит пятьдесят лет).''', 1)

# чтение уставов
old7 = '''		// 0.50: восстановление пятен.'''
assert s.count(old7) == 1
s = s.replace(old7, '''		// 0.52: восстановление местных конституций.
		TinyJson.Node nodeLocals = node.Get("locals");
		if (nodeLocals != null && !nodeLocals.IsNull)
		{
			for (int lIdx = 0; lIdx < nodeLocals.Arr.Count; lIdx++)
			{
				TinyJson.Node ln = nodeLocals.Arr[lIdx];
				int cityId = ln.GetInt("c", 0);
				if (cityId <= 0)
				{
					continue;
				}
				LocalConstitution local = world.AdoptLocalConstitution(
					cityId, ln.GetInt("k", 0), ln.GetString("r", string.Empty), ln.GetString("n", string.Empty));
				if (local == null)
				{
					continue;
				}
				local.FoundedYear = ln.GetInt("fy", 0);
				local.Banned = ln.GetBool("b", false);
				local.BannedYear = ln.GetInt("by", 0);
				local.WantsLanguage = ln.GetBool("wl", true);
				local.WantsCourt = ln.GetBool("wc", true);
				local.Grievance = ln.GetFloat("g", 0f);
			}
		}
		// 0.50: восстановление пятен.''', 1)
open(p, 'w', encoding='utf-8').write(s)
print('save ok')
