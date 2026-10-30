using System;
using System.Collections.Generic;
using RealPolitic.Core;
using Xunit;

namespace RealPolitic.Tests;

/// <summary>
/// 0.45: «почему после племён и каганатов все революционировали в Аристократические республики?»
///
/// Причина была не в эпохах, а в том, что инакомыслие жителей взвешивалось ТОЛЬКО
/// по родству идеологий: у монархии ближайший родственник — аристократия, поэтому
/// недовольные почти всегда становились аристократами, а революция поднимала знамя
/// аристократической республики. Теперь идеи своей эпохи получают фору (×2.5),
/// и в новые века державы приходят с разными знамёнами.
/// </summary>
public class IdeologyEraDiversityTests
{
	private static Dictionary<IdeologyType, int> RollMany(IdeologyType state, IdeologyEpoch epoch, int count)
	{
		Random rng = new Random(12345);
		Dictionary<IdeologyType, int> tally = new Dictionary<IdeologyType, int>();
		for (int i = 0; i < count; i++)
		{
			IdeologyType rolled = ActorIdeologyRoll.Roll(state, rng, epoch);
			tally.TryGetValue(rolled, out int c);
			tally[rolled] = c + 1;
		}
		return tally;
	}

	[Fact]
	public void MedievalDissentIsNotOnlyAristocracy()
	{
		Dictionary<IdeologyType, int> tally = RollMany(IdeologyType.Monarchy, IdeologyEpoch.Medieval, 4000);
		int dissent = 0;
		int aristocracy = 0;
		foreach (KeyValuePair<IdeologyType, int> pair in tally)
		{
			if (pair.Key == IdeologyType.Monarchy)
			{
				continue;
			}
			dissent += pair.Value;
			if (pair.Key == IdeologyType.Aristocracy)
			{
				aristocracy = pair.Value;
			}
		}
		Assert.True(dissent > 0, "недовольных не оказалось вовсе");
		// Аристократия — самая частая оппозиция в средние века, но не единственная:
		// её доля среди недовольных не должна переваливать за 3/4.
		Assert.True(aristocracy < dissent * 0.75,
			"аристократия среди недовольных: " + aristocracy + " из " + dissent);
	}

	[Fact]
	public void EarlyModernDissentBringsNewIdeas()
	{
		Dictionary<IdeologyType, int> tally = RollMany(IdeologyType.Monarchy, IdeologyEpoch.EarlyModern, 4000);
		int modern = 0;
		int aristocracy = 0;
		foreach (KeyValuePair<IdeologyType, int> pair in tally)
		{
			if (pair.Key == IdeologyType.Democracy || pair.Key == IdeologyType.Egalitarianism
				|| pair.Key == IdeologyType.Liberalism || pair.Key == IdeologyType.Empire)
			{
				modern += pair.Value;
			}
			if (pair.Key == IdeologyType.Aristocracy)
			{
				aristocracy = pair.Value;
			}
		}
		Assert.True(modern > aristocracy,
			"новых идей (" + modern + ") должно быть больше, чем аристократии (" + aristocracy + ")");
	}

	[Fact]
	public void ModernDissentBringsTechnocracyCommunismAndEthnonationalism()
	{
		Dictionary<IdeologyType, int> tally = RollMany(IdeologyType.Monarchy, IdeologyEpoch.Modern, 4000);
		int seen = 0;
		foreach (IdeologyType type in new[] { IdeologyType.Technocracy, IdeologyType.Communism, IdeologyType.Ethnonationalism })
		{
			if (tally.ContainsKey(type))
			{
				seen++;
			}
		}
		Assert.Equal(3, seen);
	}

	[Fact]
	public void RecencyWeight_IsHigherForTheNewEraOnly()
	{
		Assert.Equal(2.5f, EraRules.RecencyWeight(IdeologyType.Aristocracy, IdeologyEpoch.Medieval));
		Assert.Equal(1f, EraRules.RecencyWeight(IdeologyType.Aristocracy, IdeologyEpoch.EarlyModern));
		Assert.Equal(2.5f, EraRules.RecencyWeight(IdeologyType.Democracy, IdeologyEpoch.EarlyModern));
		Assert.Equal(2.5f, EraRules.RecencyWeight(IdeologyType.Communism, IdeologyEpoch.Modern));
		Assert.Equal(1f, EraRules.RecencyWeight(IdeologyType.Democracy, IdeologyEpoch.Modern));
		// В тёмных веках форы нет ни у кого: новых идей там просто не существует.
		Assert.Equal(1f, EraRules.RecencyWeight(IdeologyType.Monarchy, IdeologyEpoch.DarkAges));
	}

	[Fact]
	public void EraPoolsStayCumulative()
	{
		// Ответ на вопрос владельца: да, после 60-го года открывается Средневековье,
		// и пул копится — старые строи не исчезают.
		Assert.Contains(IdeologyType.Monarchy, EraRules.PoolOf(IdeologyEpoch.Medieval));
		Assert.Contains(IdeologyType.Aristocracy, EraRules.PoolOf(IdeologyEpoch.Medieval));
		Assert.Contains(IdeologyType.Aristocracy, EraRules.PoolOf(IdeologyEpoch.Modern));
		Assert.DoesNotContain(IdeologyType.Aristocracy, EraRules.PoolOf(IdeologyEpoch.DarkAges));
	}
}
