p = 'src/RealPolitic.Core/RulerRef.cs'
s = open(p, encoding='utf-8').read()
old = '''	/// <summary>ID клана в WorldBox.</summary>
	public long ClanId { get; set; }'''
assert s.count(old) == 1
s = s.replace(old, old + '''

	/// <summary>
	/// 0.52: народ правителя. Владелец 30.10.2026: «в России к власти может прийти
	/// эскимос», но конституция может это запретить.
	/// </summary>
	public string RaceId { get; set; } = string.Empty;''', 1)
open(p, 'w', encoding='utf-8').write(s)

p = 'src/Game/GameRefs.cs'
s = open(p, encoding='utf-8').read()
old2 = '''					IsFemale = GameText.ActorIsFemale(val2),
					Age = GameText.ActorAge(val2),
					ClanName = clanName,'''
assert s.count(old2) == 1
s = s.replace(old2, '''					IsFemale = GameText.ActorIsFemale(val2),
					Age = GameText.ActorAge(val2),
					// 0.52: запоминаем, из какого народа правитель.
					RaceId = ActorSpeciesId(val2),
					ClanName = clanName,''', 1)
open(p, 'w', encoding='utf-8').write(s)

# --- правило в ядре: чужой правитель и запрет конституцией
p = 'src/RealPolitic.Core/Peoples.cs'
s = open(p, encoding='utf-8').read()
old3 = '''	/// <summary>Может ли народ просить свой устав: он тут больше половины и живёт не первый год.</summary>'''
assert s.count(old3) == 1
s = s.replace(old3, '''	/// <summary>
	/// 0.52: правитель из чужого народа.
	///
	/// Владелец 30.10.2026: «в России к власти может прийти эскимос. Но тоже технически
	/// можно запретить конституцией». Значит:
	///   • закон разрешает — правит спокойно, никакого штрафа;
	///   • закон запрещает — он всё равно правит (игра так решила), но держава
	///     теряет часть законности, пока он на троне.
	/// </summary>
	public static float ForeignRulerLegitimacyHit(bool foreignRuler, bool allowedByLaw, float share)
	{
		if (!foreignRuler || allowedByLaw)
		{
			return 0f;
		}
		// Чем больше в державе этого народа, тем меньше скандала: своих много.
		float own = Math.Max(0f, Math.Min(1f, share));
		return 8f * (1f - own);
	}

	/// <summary>Может ли народ просить свой устав: он тут больше половины и живёт не первый год.</summary>''', 1)
open(p, 'w', encoding='utf-8').write(s)
print('ok')
