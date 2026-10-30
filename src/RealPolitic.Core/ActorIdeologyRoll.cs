using System;

namespace RealPolitic.Core;

/// <summary>
/// Идеология отдельного жителя: раздача при «рождении» в мире и расчёт послушания.
/// Чистое ядро (без WorldBox), поэтому полностью покрывается тестами.
///
/// Правило пользователя: идеология — черта КАЖДОГО жителя, и с вероятностью 70 %
/// житель королевства разделяет идеологию короля. Остальные 30 % распределяются
/// между прочими идеологиями с учётом близости к государственной
/// (союзная идеология вероятнее вражеской, но вражеская тоже возможна).
/// </summary>
public static class ActorIdeologyRoll
{
	/// <summary>Доля жителей, наследующих идеологию короля.</summary>
	public const float StateShare = 0.7f;

	private static readonly IdeologyType[] _all = (IdeologyType[])Enum.GetValues(typeof(IdeologyType));

	/// <summary>Идеология жителя государства с идеологией <paramref name="state"/>.</summary>
	public static IdeologyType Roll(IdeologyType state, Random rng)
	{
		if (rng == null)
		{
			return state;
		}
		if (rng.NextDouble() < StateShare)
		{
			return state;
		}
		return RollDissenting(state, rng);
	}

	/// <summary>Раскатка с уважением к эпохе мира: анахронистичных взглядов не бывает.</summary>
	public static IdeologyType Roll(IdeologyType state, Random rng, IdeologyEpoch epoch)
	{
		if (!EraRules.Allowed(state, epoch))
		{
			state = EraRules.NearestAllowed(state, epoch);
		}
		if (rng == null)
		{
			return state;
		}
		if (rng.NextDouble() < StateShare)
		{
			return state;
		}
		return RollDissentingEpoch(state, rng, epoch);
	}

	/// <summary>Инакомыслящая идеология в пределах эпохи.</summary>
	public static IdeologyType RollDissentingEpoch(IdeologyType state, Random rng, IdeologyEpoch epoch)
	{
		if (rng == null)
		{
			return EraRules.Allowed(state, epoch) ? state : EraRules.NearestAllowed(state, epoch);
		}
		IdeologyType[] pool = EraRules.PoolOf(epoch);
		float total = 0f;
		float[] weights = new float[pool.Length];
		for (int i = 0; i < pool.Length; i++)
		{
			if (pool[i] == state)
			{
				weights[i] = 0f;
				continue;
			}
			float w = 2f + Ideologies.Affinity(state, pool[i]);
			if (w < 0.35f)
			{
				w = 0.35f;
			}
			// 0.45: идеи своей эпохи увлекают сильнее родства: иначе недовольные
			// монархии все как один становились аристократами (см. RecencyWeight).
			w *= EraRules.RecencyWeight(pool[i], epoch);
			weights[i] = w;
			total += w;
		}
		if (total <= 0f)
		{
			return state;
		}
		double pick = rng.NextDouble() * total;
		for (int i = 0; i < pool.Length; i++)
		{
			pick -= weights[i];
			if (pick <= 0)
			{
				return pool[i];
			}
		}
		return pool[pool.Length - 1];
	}

	/// <summary>Инакомыслящая идеология (гарантированно не государственная), взвешенная по близости.</summary>
	public static IdeologyType RollDissenting(IdeologyType state, Random rng)
	{
		float total = 0f;
		float[] weights = new float[_all.Length];
		for (int i = 0; i < _all.Length; i++)
		{
			if (_all[i] == state)
			{
				weights[i] = 0f;
				continue;
			}
			// Affinity ∈ [-1; 1]: у союзной идеологии вес ~3, у нейтральной ~2, у вражеской ~1.
			float w = 2f + Ideologies.Affinity(state, _all[i]);
			if (w < 0.35f)
			{
				w = 0.35f;
			}
			weights[i] = w;
			total += w;
		}
		if (total <= 0f)
		{
			return state;
		}
		double pick = rng.NextDouble() * total;
		for (int i = 0; i < _all.Length; i++)
		{
			pick -= weights[i];
			if (pick <= 0.0 && weights[i] > 0f)
			{
				return _all[i];
			}
		}
		for (int i = _all.Length - 1; i >= 0; i--)
		{
			if (weights[i] > 0f)
			{
				return _all[i];
			}
		}
		return state;
	}

	/// <summary>Убеждённость нового жителя: у согласных с государством она в среднем выше.</summary>
	public static float RollConviction(IdeologyType actor, IdeologyType state, Random rng)
	{
		double roll = (rng == null) ? 0.5 : rng.NextDouble();
		float v = (actor == state) ? (0.45f + (float)roll * 0.45f) : (0.55f + (float)roll * 0.45f);
		return Clamp(v, 0f, 1f);
	}

	/// <summary>
	/// Послушание жителя государству: 1 — полностью лоялен, 0 — не подчиняется.
	/// Житель государственной идеологии послушнее; чем убеждённее инакомыслящий
	/// и чем враждебнее его идеология государственной, тем он непослушнее.
	/// </summary>
	public static float Obedience(IdeologyType actor, IdeologyType state, float conviction, float dissent)
	{
		conviction = Clamp(conviction, 0f, 1f);
		dissent = Clamp(dissent, 0f, 1f);
		if (actor == state)
		{
			// Согласный: убеждённость делает его ещё послушнее.
			return Clamp(0.85f + 0.15f * conviction - 0.25f * dissent, 0f, 1f);
		}
		float affinity = Ideologies.Affinity(state, actor); // [-1; 1]
		float baseline = 0.55f + 0.2f * affinity;           // союзник ~0.67, враг ~0.35
		float stubbornness = 0.25f * conviction + 0.3f * dissent;
		// Терпимость государства к инакомыслию смягчает разрыв.
		float tolerance = Ideologies.Get(state).DissentTolerance;
		float softened = baseline - stubbornness + 0.15f * tolerance;
		return Clamp(softened, 0f, 1f);
	}

	/// <summary>Вклад жителя в лояльность города (игровой стат loyalty_traits/loyalty_mood).</summary>
	public static int LoyaltyDelta(IdeologyType actor, IdeologyType state, float conviction, float dissent)
	{
		float obedience = Obedience(actor, state, conviction, dissent);
		// 1.0 → +6, 0.5 → 0, 0.0 → −8 (несогласные бьют сильнее, чем помогают согласные).
		float scaled = (obedience - 0.5f) * ((obedience >= 0.5f) ? 12f : 16f);
		return (int)Math.Round(scaled);
	}

	/// <summary>Изменение настроения жителя за такт: инакомыслящие при нетерпимой власти недовольны.</summary>
	public static int HappinessDelta(IdeologyType actor, IdeologyType state, float conviction, float dissent)
	{
		if (actor == state)
		{
			return (conviction > 0.6f) ? 1 : 0;
		}
		float obedience = Obedience(actor, state, conviction, dissent);
		if (obedience >= 0.6f)
		{
			return 0;
		}
		return -(int)Math.Round((0.6f - obedience) * 10f);
	}

	/// <summary>Доля жителей, готовых поддержать мятеж (для летописи и revolt-логики).</summary>
	public static bool IsRebellious(IdeologyType actor, IdeologyType state, float conviction, float dissent)
	{
		if (actor == state)
		{
			return false;
		}
		return Obedience(actor, state, conviction, dissent) < 0.4f;
	}

	private static float Clamp(float v, float min, float max)
	{
		if (v < min)
		{
			return min;
		}
		return (v > max) ? max : v;
	}
}
