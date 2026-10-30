using System;
using System.Collections.Generic;

namespace RealPolitic.Core;

public static class ActorSimulation
{
	public static float TickActor(ActorEntry actor, KingdomPolitics state, CityPolitics? city, SimParams p, Func<int, IdeologyType> neighborIdeology, Func<int, float> neighborMissionary, IReadOnlyList<int> neighborIds, Random rng, PolEventBus bus, int year)
	{
		ActorPolitics politics = actor.Politics;
		float result = 0f;
		if (politics.Ideology != state.Ideology)
		{
			float dissentTolerance = state.Def.DissentTolerance;
			float num = Clamp((1f - dissentTolerance) * 2f, 0.2f, 1.4f);
			politics.Dissent = Clamp(politics.Dissent + p.DissentRate * num * (1.5f - politics.Conviction), 0f, 1f);
		}
		else
		{
			politics.Dissent = Clamp(politics.Dissent - p.DissentDecay * (1f + state.Legitimacy / 200f), 0f, 1f);
		}
		if (state.Starving)
		{
			politics.Dissent = Clamp(politics.Dissent + 0.06f, 0f, 1f);
		}
		if (state.InWar)
		{
			politics.Dissent = Clamp(politics.Dissent + 0.03f, 0f, 1f);
		}
		IdeologyType best = politics.Ideology;
		float bestPressure = 0f;
		float amount = p.SpreadFromState * (0.4f + state.Legitimacy / 100f) * (0.5f + state.Support / 100f);
		if (politics.Ideology != state.Ideology)
		{
			Accumulate(ref best, ref bestPressure, state.Ideology, amount);
		}
		if (city != null && city.Dominant != politics.Ideology && city.Share > 0.25f)
		{
			Accumulate(ref best, ref bestPressure, city.Dominant, p.SpreadFromState * city.Share);
		}
		if (neighborIds != null)
		{
			for (int i = 0; i < neighborIds.Count; i++)
			{
				int arg = neighborIds[i];
				IdeologyType ideologyType = neighborIdeology(arg);
				if (ideologyType != politics.Ideology)
				{
					float num2 = neighborMissionary(arg) * p.SpreadFromNeighbor;
					if (!(num2 <= 0f))
					{
						Accumulate(ref best, ref bestPressure, ideologyType, num2);
					}
				}
			}
		}
		if (best == politics.Ideology || bestPressure <= 0f)
		{
			return result;
		}
		if (year >= (p?.TribalDeclineYear ?? 200)
			&& TribalCivilization.NeedsCivilization(best)
			&& best != state.Ideology)
		{
			return result;
		}
		result = bestPressure * (politics.Dissent + 0.2f);
		float num3 = Ideologies.Affinity(politics.Ideology, best);
		float num4 = 1.6f - politics.Conviction;
		float num5 = 0.25f + (num3 + 1f) * 0.25f;
		politics.ConversionProgress += p.ConversionBase * bestPressure * num4 * num5;
		if (politics.ConversionTarget != best)
		{
			politics.ConversionProgress *= 0.5f;
			politics.ConversionTarget = best;
		}
		if (politics.ConversionProgress >= p.ActorConvertThreshold)
		{
			IdeologyType ideology = politics.Ideology;
			politics.Ideology = best;
			politics.ConversionProgress = 0f;
			politics.ConversionTarget = best;
			politics.Conviction = Clamp(0.35f + (float)rng.NextDouble() * 0.5f, 0f, 1f);
			politics.Dissent = Clamp(politics.Dissent * 0.5f, 0f, 1f);
			politics.Stance = ClampStanceFor(best, politics.Stance);
			bus.Publish(new PolEvent(PolEventType.ActorConverted, "RP_EV_ACTOR_CONVERTED")
			{
				Year = year,
				Severity = Severity.Trivial,
				KingdomId = state.KingdomId,
				CityId = city?.CityId,
				ActorId = actor.Id
			}.With("old", Ideologies.NameKey(ideology)).With("new", Ideologies.NameKey(politics.Ideology)));
		}
		return result;
	}

	public static ActorPolitics Inherit(ActorPolitics? mother, ActorPolitics? father, CityPolitics? city, KingdomPolitics state, Random rng, IdeologyEpoch epoch = IdeologyEpoch.Modern)
	{
		IdeologyType ideology;
		float v;
		if (mother != null && father != null)
		{
			if (mother.Ideology == father.Ideology)
			{
				ideology = mother.Ideology;
				v = 0.55f + (float)rng.NextDouble() * 0.35f;
			}
			else
			{
				ideology = ((mother.Conviction >= father.Conviction || rng.NextDouble() < 0.5) ? mother.Ideology : father.Ideology);
				v = 0.3f + (float)rng.NextDouble() * 0.4f;
			}
		}
		else if (mother != null)
		{
			ideology = mother.Ideology;
			v = 0.5f + (float)rng.NextDouble() * 0.3f;
		}
		else if (city != null)
		{
			ideology = city.Dominant;
			v = 0.3f + (float)rng.NextDouble() * 0.4f;
		}
		else
		{
			ideology = state.Ideology;
			v = 0.3f + (float)rng.NextDouble() * 0.4f;
		}
		if (rng.NextDouble() < 0.019999999552965164)
		{
			ideology = RandomIdeology(rng, epoch);
			v = 0.6f + (float)rng.NextDouble() * 0.4f;
		}
		ReformStance stance = ClampStanceFor(ideology, state.Stance);
		return new ActorPolitics(ideology, stance)
		{
			Conviction = Clamp(v, 0f, 1f)
		};
	}

	internal static IdeologyType RandomIdeology(Random rng)
	{
		Array values = Enum.GetValues(typeof(IdeologyType));
		return (IdeologyType)values.GetValue(rng.Next(values.Length));
	}

	/// <summary>Сверхъестественная «смена убеждений» тоже уважает эпоху мира.</summary>
	internal static IdeologyType RandomIdeology(Random rng, IdeologyEpoch epoch)
	{
		IdeologyType[] pool = EraRules.PoolOf(epoch);
		return pool[rng.Next(pool.Length)];
	}

	/// <summary>
	/// Идеология нового правителя при смене власти. Государство следует за королём:
	/// новый правитель приносит СВОИ убеждения (те, что ядро отслеживает у него как у жителя),
	/// а не случайный строй. Если убеждения правителя неизвестны — строй сохраняется.
	/// В любом случае результат не выходит за рамки эпохи мира (табу на анахронизмы).
	/// </summary>
	public static IdeologyType SuccessorIdeology(IdeologyType current, IdeologyType? rulerIdeology, IdeologyEpoch epoch, int year = 0)
	{
		IdeologyType candidate = rulerIdeology ?? current;
		if (candidate != current && TribalCivilization.NeedsCivilization(candidate) && year >= 200)
		{
			candidate = current;
		}
		if (EraRules.Allowed(candidate, epoch))
		{
			return candidate;
		}
		return EraRules.Allowed(current, epoch) ? current : EraRules.NearestAllowed(current, epoch);
	}

	internal static ReformStance ClampStanceFor(IdeologyType ideology, ReformStance stance)
	{
		IdeologyDef ideologyDef = Ideologies.Get(ideology);
		int num = (int)stance;
		if (num < ideologyDef.MinReform)
		{
			num = ideologyDef.MinReform;
		}
		if (num > ideologyDef.MaxReform)
		{
			num = ideologyDef.MaxReform;
		}
		return (ReformStance)num;
	}

	private static void Accumulate(ref IdeologyType best, ref float bestPressure, IdeologyType candidate, float amount)
	{
		if (amount > bestPressure)
		{
			bestPressure = amount;
			best = candidate;
		}
	}

	private static float Clamp(float v, float min, float max)
	{
		if (!(v < min))
		{
			if (!(v > max))
			{
				return v;
			}
			return max;
		}
		return min;
	}
}
