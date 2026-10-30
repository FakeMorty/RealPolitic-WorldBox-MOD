p = 'src/RealPolitic.Core/PoliticalWorld.cs'
s = open(p, encoding='utf-8').read()

# --- имена городов: запоминаем из входа, чтобы писать в уведомления
old = '''	/// <summary>0.52: все местные конституции (по номеру города).</summary>'''
assert s.count(old) == 1
s = s.replace(old, '''	/// <summary>0.52: последние известные имена городов (для уведомлений и окна).</summary>
	private readonly Dictionary<int, string> _cityNames = new Dictionary<int, string>();

	/// <summary>0.52: имя города; если игра его ещё не назвала — «город #12».</summary>
	public string CityNameOf(int cityId)
	{
		if (_cityNames.TryGetValue(cityId, out string name) && !string.IsNullOrWhiteSpace(name))
		{
			return name;
		}
		return "город #" + cityId;
	}

	/// <summary>0.52: все местные конституции (по номеру города).</summary>''', 1)

# --- доля народа в городе: берём из данных игры, иначе город считаем однородным
old2 = '''				// Считаем долю этого народа именно в этом городе.
				int cityTotal = 0;
				int ours = 0;
				for (int c = 0; c < cities.Count; c++)
				{
					if (cities[c].CityId != cityId)
					{
						continue;
					}
					cityTotal = cities[c].Population > 0 ? cities[c].Population : 1;
				}
				ours = cityTotal; // один город — один народ в данных игры
				float cityShare = cityTotal > 0 ? (float)ours / Math.Max(1, cityTotal) : 0f;
				if (cities.Count > 0 && cityTotal == 0)
				{
					continue;
				}'''
assert s.count(old2) == 1
s = s.replace(old2, '''				// Доля народа в этом городе. Игра даёт по городу один главный народ,
				// поэтому по умолчанию считаем город однородным; если слой игры когда-нибудь
				// сообщит точную долю (CityInput.RaceShare) — возьмём её.
				float cityShare = 1f;
				for (int c = 0; c < cities.Count; c++)
				{
					if (cities[c].CityId == cityId && cities[c].RaceShare > 0f)
					{
						cityShare = cities[c].RaceShare;
						break;
					}
				}''', 1)

# --- запоминаем имена городов в начале переписи
old3 = '''			for (int c = 0; c < cities.Count; c++)
			{
				string raceId = !string.IsNullOrEmpty(cities[c].RaceId) ? cities[c].RaceId : cities[c].CultureName;
				if (string.IsNullOrEmpty(raceId))
				{
					continue;
				}
				int population = cities[c].Population > 0 ? cities[c].Population : 1;
				total += population;
				byRace.TryGetValue(raceId, out int was);'''
assert s.count(old3) == 1
s = s.replace(old3, '''			for (int c = 0; c < cities.Count; c++)
			{
				if (cities[c].CityId > 0 && !string.IsNullOrWhiteSpace(cities[c].Name))
				{
					_cityNames[cities[c].CityId] = cities[c].Name;
				}
				string raceId = !string.IsNullOrEmpty(cities[c].RaceId) ? cities[c].RaceId : cities[c].CultureName;
				if (string.IsNullOrEmpty(raceId))
				{
					continue;
				}
				int population = cities[c].Population > 0 ? cities[c].Population : 1;
				total += population;
				byRace.TryGetValue(raceId, out int was);''', 1)
open(p, 'w', encoding='utf-8').write(s)

# --- поле доли в городе
p = 'src/RealPolitic.Core/CityInput.cs'
s = open(p, encoding='utf-8').read()
old4 = '''	public string RaceName;'''
assert s.count(old4) == 1
s = s.replace(old4, '''	public string RaceName;

	/// <summary>
	/// 0.52: доля главного народа города (0..1). 0 — «игра не сказала»,
	/// тогда город считается однородным.
	/// </summary>
	public float RaceShare;''', 1)
open(p, 'w', encoding='utf-8').write(s)
print('ok')
