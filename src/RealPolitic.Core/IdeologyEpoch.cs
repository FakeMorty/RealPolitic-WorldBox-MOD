using System;
using System.Collections.Generic;

namespace RealPolitic.Core;

/// <summary>Эпохи развития мира: ограничивают, какие строи вообще возможны сейчас.
/// Защищает от «коммунизма в пятом году»: модерные идеологии требуют времени.</summary>
public enum IdeologyEpoch
{
	/// <summary>Тёмные века: племена, монархии, жрецы и древние деспотии.</summary>
	DarkAges = 0,

	/// <summary>Средние века: добавляются аристократы, торговцы и затворники.</summary>
	Medieval = 1,

	/// <summary>Новое время: гражданские идеи — эгалитаризм и демократия.</summary>
	EarlyModern = 2,

	/// <summary>Современность: технократия, коммунизм, этнонационализм.</summary>
	Modern = 3
}

/// <summary>Правила эпох: какая идеология допустима в каком году мира.</summary>
public static class EraRules
{
	// Эпоха определяет не уровень мира, а его возраст: год мира меряется одинаково у всех.
	private static readonly IdeologyType[] _darkAges =
	{
		IdeologyType.Tribalism, IdeologyType.Monarchy, IdeologyType.Theocracy, IdeologyType.Militarism,
		IdeologyType.Khaganate, IdeologyType.FreeTerritory
	};

	private static readonly IdeologyType[] _medieval =
	{
		IdeologyType.Aristocracy, IdeologyType.MerchantRepublic
	};

	private static readonly IdeologyType[] _earlyModern =
	{
		IdeologyType.Egalitarianism, IdeologyType.Democracy, IdeologyType.Liberalism, IdeologyType.Empire
	};

	private static readonly IdeologyType[] _modern =
	{
		IdeologyType.Technocracy, IdeologyType.Communism, IdeologyType.Ethnonationalism
	};

	/// <summary>Эпоха мира по его году и настройкам (из панели настроек мода).</summary>
	public static IdeologyEpoch OfYear(int year, SimParams p)
	{
		if (p != null && !p.EnableEraRestrictions)
		{
			return IdeologyEpoch.Modern;
		}
		if (p != null && year >= p.EraModernYear)
		{
			return IdeologyEpoch.Modern;
		}
		if (p != null && year >= p.EraEarlyModernYear)
		{
			return IdeologyEpoch.EarlyModern;
		}
		int medieval = p != null ? p.EraMedievalYear : 60;
		return year >= medieval ? IdeologyEpoch.Medieval : IdeologyEpoch.DarkAges;
	}

	/// <summary>Анахронизмов нет: строй допустим только в свою эпоху и позже.</summary>
	public static bool Allowed(IdeologyType t, IdeologyEpoch epoch)
	{
		if (epoch >= IdeologyEpoch.Modern)
		{
			return true;
		}
		for (int i = 0; i < PoolOf(epoch).Length; i++)
		{
			if (PoolOf(epoch)[i] == t)
			{
				return true;
			}
		}
		return false;
	}

	/// <summary>Все строи, доступные в эпохе (накопительно, от тёмных веков вверх).</summary>
	public static IdeologyType[] PoolOf(IdeologyEpoch epoch)
	{
		List<IdeologyType> pool = new List<IdeologyType>(_darkAges);
		if (epoch >= IdeologyEpoch.Medieval)
		{
			pool.AddRange(_medieval);
		}
		if (epoch >= IdeologyEpoch.EarlyModern)
		{
			pool.AddRange(_earlyModern);
		}
		if (epoch >= IdeologyEpoch.Modern)
		{
			pool.AddRange(_modern);
		}
		return pool.ToArray();
	}

	/// <summary>
	/// 0.45: насколько «своим» считается строй для этой эпохи.
	/// 2.5 — строй появился именно в эту эпоху (новые идеи увлекают умы),
	/// 1.0 — достался от прошлых веков.
	///
	/// Зачем. Владелец 29.10.2026: «почему после племён и каганатов все
	/// революционировали в Аристократические республики?» Причина была в том,
	/// что инакомыслие жителей взвешивалось ТОЛЬКО по родству идеологий:
	/// у монархии ближайший родственник — аристократия, поэтому недовольные
	/// почти всегда становились аристократами, и каждая революция поднимала
	/// знамя аристократической республики. Теперь новым идеям эпохи даётся
	/// фору: с 60-го года это аристократия и торговая республика, с 150-го —
	/// эгалитаризм, демократия, либерализм и империя, с 250-го — технократия,
	/// коммунизм и этнонационализм.
	/// </summary>
	public static float RecencyWeight(IdeologyType t, IdeologyEpoch epoch)
	{
		if (epoch <= IdeologyEpoch.DarkAges)
		{
			return 1f;
		}
		IdeologyType[] newest = epoch switch
		{
			IdeologyEpoch.Medieval => _medieval,
			IdeologyEpoch.EarlyModern => _earlyModern,
			_ => _modern
		};
		for (int i = 0; i < newest.Length; i++)
		{
			if (newest[i] == t)
			{
				return 2.5f;
			}
		}
		return 1f;
	}

	/// <summary>Ближайший допустимый в эпохе строй к заданному (по сродству идеологий),
	/// для коррекции древних сейвов. Совпадение по смыслу, никогда не случайно.</summary>
	public static IdeologyType NearestAllowed(IdeologyType from, IdeologyEpoch epoch)
	{
		IdeologyType[] pool = PoolOf(epoch);
		IdeologyType best = pool[0];
		float bestAffinity = float.MinValue;
		for (int i = 0; i < pool.Length; i++)
		{
			float affinity = Ideologies.Affinity(from, pool[i]);
			// Среди равных по сродству предпочитаем более «архаичный» — шаг назад не должен быть скачком.
			if (affinity > bestAffinity || (Math.Abs(affinity - bestAffinity) < 0.0001f && i < Array.IndexOf(pool, best)))
			{
				bestAffinity = affinity;
				best = pool[i];
			}
		}
		return best;
	}
}
