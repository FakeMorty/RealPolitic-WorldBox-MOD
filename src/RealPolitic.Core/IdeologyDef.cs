using System.Collections.Generic;

namespace RealPolitic.Core;

public sealed class IdeologyDef
{
	public IdeologyType Type { get; }

	public string Id { get; }

	public float Loyalty { get; }

	public float Growth { get; }

	public float WarAppetite { get; }

	public float Tech { get; }

	public int Legitimacy { get; }

	public float Cohesion { get; }

	public float Missionary { get; }

	public float DissentTolerance { get; }

	public float ReformPressure { get; }

	public int MinReform { get; }

	public int MaxReform { get; }

	public IReadOnlyList<IdeologyType> Allies { get; }

	public IReadOnlyList<IdeologyType> Rivals { get; }

	internal IdeologyDef(IdeologyType type, float loyalty, float growth, float warAppetite, float tech, int legitimacy, float cohesion, float missionary, float dissentTolerance, float reformPressure, int minReform, int maxReform, IdeologyType[] allies, IdeologyType[] rivals)
	{
		Type = type;
		Id = type.ToString();
		Loyalty = loyalty;
		Growth = growth;
		WarAppetite = warAppetite;
		Tech = tech;
		Legitimacy = legitimacy;
		Cohesion = cohesion;
		Missionary = missionary;
		DissentTolerance = dissentTolerance;
		ReformPressure = reformPressure;
		MinReform = minReform;
		MaxReform = maxReform;
		Allies = allies;
		Rivals = rivals;
	}

	public bool WithinReformRange(int stance)
	{
		if (stance >= MinReform)
		{
			return stance <= MaxReform;
		}
		return false;
	}

	public bool IsAlly(IdeologyType other)
	{
		if (other == Type)
		{
			return true;
		}
		for (int i = 0; i < Allies.Count; i++)
		{
			if (Allies[i] == other)
			{
				return true;
			}
		}
		return false;
	}

	public bool IsRival(IdeologyType other)
	{
		for (int i = 0; i < Rivals.Count; i++)
		{
			if (Rivals[i] == other)
			{
				return true;
			}
		}
		return false;
	}
}
