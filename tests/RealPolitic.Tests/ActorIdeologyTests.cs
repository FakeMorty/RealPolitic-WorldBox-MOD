using System;
using System.Collections.Generic;
using RealPolitic.Core;
using Xunit;

namespace RealPolitic.Tests;

/// <summary>
/// Идеология как черта каждого жителя: раздача 70/30 и реальное влияние на послушание.
/// </summary>
public class ActorIdeologyTests
{
	[Fact]
	public void Roll_Gives_State_Ideology_To_About_Seventy_Percent()
	{
		Random rng = new Random(1234);
		int state = 0;
		const int n = 20000;
		for (int i = 0; i < n; i++)
		{
			if (ActorIdeologyRoll.Roll(IdeologyType.Monarchy, rng) == IdeologyType.Monarchy)
			{
				state++;
			}
		}
		double share = (double)state / n;
		Assert.InRange(share, 0.68, 0.72);
	}

	[Fact]
	public void Dissenting_Roll_Never_Returns_State_Ideology()
	{
		Random rng = new Random(7);
		for (int i = 0; i < 2000; i++)
		{
			Assert.NotEqual(IdeologyType.Theocracy, ActorIdeologyRoll.RollDissenting(IdeologyType.Theocracy, rng));
		}
	}

	[Fact]
	public void Dissenting_Roll_Covers_All_Other_Ideologies()
	{
		Random rng = new Random(99);
		HashSet<IdeologyType> seen = new HashSet<IdeologyType>();
		for (int i = 0; i < 5000; i++)
		{
			seen.Add(ActorIdeologyRoll.RollDissenting(IdeologyType.Monarchy, rng));
		}
		// 0.49: всех строёв стало 16, значит несогласных вариантов — 15.
		Assert.Equal(15, seen.Count);
	}

	[Fact]
	public void Allied_Ideology_Is_More_Likely_Than_Rival()
	{
		Random rng = new Random(4242);
		int allied = 0;
		int rival = 0;
		for (int i = 0; i < 20000; i++)
		{
			IdeologyType t = ActorIdeologyRoll.RollDissenting(IdeologyType.Monarchy, rng);
			if (t == IdeologyType.Theocracy || t == IdeologyType.Aristocracy)
			{
				allied++;
			}
			if (t == IdeologyType.Egalitarianism)
			{
				rival++;
			}
		}
		// Два союзника суммарно должны заметно превосходить одного соперника.
		Assert.True(allied > rival * 2, $"союзники={allied}, соперник={rival}");
	}

	[Fact]
	public void Same_Ideology_Citizen_Is_More_Obedient_Than_Dissenter()
	{
		float loyal = ActorIdeologyRoll.Obedience(IdeologyType.Monarchy, IdeologyType.Monarchy, 0.8f, 0.1f);
		float dissenter = ActorIdeologyRoll.Obedience(IdeologyType.Egalitarianism, IdeologyType.Monarchy, 0.8f, 0.1f);
		Assert.True(loyal > dissenter);
		Assert.InRange(loyal, 0.7f, 1f);
		Assert.InRange(dissenter, 0f, 0.6f);
	}

	[Fact]
	public void Rival_Dissenter_Is_Less_Obedient_Than_Allied_Dissenter()
	{
		float allied = ActorIdeologyRoll.Obedience(IdeologyType.Theocracy, IdeologyType.Monarchy, 0.5f, 0.2f);
		float rival = ActorIdeologyRoll.Obedience(IdeologyType.Egalitarianism, IdeologyType.Monarchy, 0.5f, 0.2f);
		Assert.True(allied > rival, $"союзник={allied}, соперник={rival}");
	}

	[Fact]
	public void Conviction_And_Dissent_Reduce_Obedience_Of_Dissenter()
	{
		float calm = ActorIdeologyRoll.Obedience(IdeologyType.Technocracy, IdeologyType.Monarchy, 0.2f, 0f);
		float fierce = ActorIdeologyRoll.Obedience(IdeologyType.Technocracy, IdeologyType.Monarchy, 1f, 1f);
		Assert.True(calm > fierce);
	}

	[Fact]
	public void Loyalty_Delta_Is_Positive_For_Supporters_And_Negative_For_Opponents()
	{
		Assert.True(ActorIdeologyRoll.LoyaltyDelta(IdeologyType.Monarchy, IdeologyType.Monarchy, 0.9f, 0f) > 0);
		Assert.True(ActorIdeologyRoll.LoyaltyDelta(IdeologyType.Egalitarianism, IdeologyType.Monarchy, 0.9f, 0.8f) < 0);
	}

	[Fact]
	public void Happiness_Delta_Punishes_Only_Unhappy_Dissenters()
	{
		Assert.True(ActorIdeologyRoll.HappinessDelta(IdeologyType.Monarchy, IdeologyType.Monarchy, 0.9f, 0f) >= 0);
		Assert.True(ActorIdeologyRoll.HappinessDelta(IdeologyType.Egalitarianism, IdeologyType.Monarchy, 1f, 1f) < 0);
	}

	[Fact]
	public void Tolerant_State_Keeps_Dissenters_More_Obedient()
	{
		// Племя терпимо к инакомыслию (DissentTolerance 0.8), теократия — нет (0.3).
		float inTribe = ActorIdeologyRoll.Obedience(IdeologyType.Technocracy, IdeologyType.Tribalism, 0.6f, 0.3f);
		float inTheocracy = ActorIdeologyRoll.Obedience(IdeologyType.Technocracy, IdeologyType.Theocracy, 0.6f, 0.3f);
		Assert.True(inTribe > inTheocracy, $"племя={inTribe}, теократия={inTheocracy}");
	}

	[Fact]
	public void Fanatic_Rival_Is_Rebellious_But_Loyalist_Is_Not()
	{
		Assert.True(ActorIdeologyRoll.IsRebellious(IdeologyType.Egalitarianism, IdeologyType.Monarchy, 1f, 1f));
		Assert.False(ActorIdeologyRoll.IsRebellious(IdeologyType.Monarchy, IdeologyType.Monarchy, 1f, 1f));
	}

	[Fact]
	public void Obedience_Always_Stays_In_Unit_Range()
	{
		Array all = Enum.GetValues(typeof(IdeologyType));
		foreach (IdeologyType a in all)
		{
			foreach (IdeologyType b in all)
			{
				for (float c = 0f; c <= 1f; c += 0.5f)
				{
					for (float d = 0f; d <= 1f; d += 0.5f)
					{
						float o = ActorIdeologyRoll.Obedience(a, b, c, d);
						Assert.InRange(o, 0f, 1f);
					}
				}
			}
		}
	}

	[Fact]
	public void Conviction_Roll_Stays_In_Unit_Range()
	{
		Random rng = new Random(5);
		for (int i = 0; i < 1000; i++)
		{
			float v = ActorIdeologyRoll.RollConviction(IdeologyType.Militarism, IdeologyType.Monarchy, rng);
			Assert.InRange(v, 0f, 1f);
		}
	}
}
