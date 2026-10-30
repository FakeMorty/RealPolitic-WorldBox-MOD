namespace RealPolitic.Core;

public sealed class ActorPolitics
{
	public IdeologyType Ideology { get; set; }

	public ReformStance Stance { get; set; }

	public float Conviction { get; set; }

	public float Dissent { get; set; }

	public float ConversionProgress { get; set; }

	public IdeologyType ConversionTarget { get; set; }

	public ActorPolitics(IdeologyType ideology, ReformStance stance)
	{
		Ideology = ideology;
		Stance = stance;
		Conviction = 0.5f;
		Dissent = 0f;
		ConversionTarget = ideology;
	}

	public float LoyaltyModifier()
	{
		float num = Ideologies.Get(Ideology).Loyalty * (0.5f + 0.5f * Conviction);
		float num2 = (0f - Dissent) * 0.6f;
		return num + num2;
	}

	public bool Accepts(IdeologyType other)
	{
		return Ideologies.Get(Ideology).IsAlly(other);
	}
}
