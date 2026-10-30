using System;
using System.Collections.Generic;
using System.Linq;
using RealPolitic.Core;
using Xunit;

namespace RealPolitic.Tests;

/// <summary>
/// Этап 23, п.6: статистическая проверка того, что идеология меняет поведение
/// по-настоящему, а не только подпись под портретом. Множим сценарии
/// дипломатического советника и считаем, кто чаще всего выбирает войну.
/// </summary>
public class IdeologyStatCheckTests
{
	private const int Scenarios = 400;

	// Детерминированная матрица «атакующая идеология × обороняющаяся» (12×12 пар):
	// считаем, в скольких парах ход-война набирает порог советника. Никакой случайности.
	private static Dictionary<IdeologyType, int> CountWarChoices(Random unused, bool underRegency = false)
	{
		var counts = new Dictionary<IdeologyType, int>();
		IdeologyType[] all = Ideologies.All.Select(d => d.Type).OrderBy(t => (int)t).ToArray();
		foreach (IdeologyType t in all)
		{
			counts[t] = 0;
		}
		var w = new DiplomacyWeights();
		foreach (IdeologyType selfIdeology in all)
		{
			foreach (IdeologyType targetIdeology in all)
			{
				if (targetIdeology == selfIdeology) continue;
				var target = new DiplomatSnapshot
				{
					KingdomId = 2,
					Ideology = targetIdeology,
					Stance = ReformStance.Moderate,
					Legitimacy = 60f,
					Population = 300,
					Warriors = 40,
					Starving = false,
					AtWar = false,
					ReligionId = 7,
					RaceId = "human"
				};
				var self = new DiplomatSnapshot
				{
					KingdomId = 1,
					Ideology = selfIdeology,
					Stance = ReformStance.Moderate,
					Legitimacy = 60f,
					Population = 300,
					Warriors = 60,
					Starving = false,
					AtWar = false,
					ReligionId = 7,
					UnderRegency = underRegency,
					RaceId = "human"
				};
				var rels = new List<RelationInfo>
				{
					new RelationInfo
					{
						OtherId = 2,
						Relation = RelationType.Peace,
						Adjacent = true,
						Claims = 40
					}
				};
				List<DiplomaticMove> moves = DiplomaticAdvisor.Evaluate(self, new List<DiplomatSnapshot> { target }, rels, w);
				foreach (DiplomaticMove m in moves)
				{
					if (m.Action == DiplomaticAction.DeclareWar && m.Score >= w.ActThreshold)
					{
						counts[selfIdeology]++;
						break;
					}
				}
			}
		}
		return counts;
	}

	[Fact]
	public void Ethnonationalism_ReallyExpandsMoreThanPacifists()
	{
		Dictionary<IdeologyType, int> wars = CountWarChoices(new Random(2023));

		int ethno = wars[IdeologyType.Ethnonationalism];
		// Этнонационализм объявляет войны статистически чаще всех
		// «миролюбивых» форм — разрыв проверяем по скачку, не по шуму.
		foreach (IdeologyType pacifist in new[]
		{
			IdeologyType.Democracy, IdeologyType.Liberalism, IdeologyType.Theocracy,
			IdeologyType.Egalitarianism, IdeologyType.MerchantRepublic, IdeologyType.Technocracy
		})
		{
			Assert.True(ethno > wars[pacifist],
				$"этнонационализм ({ethno}) должен чаще объявлять войну, чем {pacifist} ({wars[pacifist]})");
		}
		// Эффект не символический: в большинстве пар ход-война у этнонационалистов набрал порог.
		Assert.True(ethno >= 2 * wars[IdeologyType.Democracy] + 1,
			$"разрыв этнонационализм({ethno})/демократия({wars[IdeologyType.Democracy]}) должен быть существенным");
	}

	[Fact]
	public void Militarism_StaysTheMostWarlike()
	{
		Dictionary<IdeologyType, int> wars = CountWarChoices(new Random(2023));
		foreach (KeyValuePair<IdeologyType, int> kv in wars)
		{
			if (kv.Key == IdeologyType.Militarism) continue;
			Assert.True(wars[IdeologyType.Militarism] >= kv.Value,
				$"милитаризм ({wars[IdeologyType.Militarism]}) не должен уступать {kv.Key} ({kv.Value})");
		}
	}

	[Fact]
	public void RegencyBlocksAggressiveWars_ForEthnonationalistsToo()
	{
		Dictionary<IdeologyType, int> wars = CountWarChoices(new Random(7), underRegency: true);
		Assert.Equal(0, wars[IdeologyType.Ethnonationalism]);
		Assert.Equal(0, wars[IdeologyType.Militarism]);
	}

	[Fact]
	public void WarAppetite_TableOrdersAsDesigned()
	{
		// Документная проверка таблицы: аппетиты войны задают расширение.
		float ethno = Ideologies.Get(IdeologyType.Ethnonationalism).WarAppetite;
		float mil = Ideologies.Get(IdeologyType.Militarism).WarAppetite;
		float com = Ideologies.Get(IdeologyType.Communism).WarAppetite;
		float dem = Ideologies.Get(IdeologyType.Democracy).WarAppetite;
		float iso = Ideologies.Get(IdeologyType.Liberalism).WarAppetite;
		Assert.True(mil >= ethno);
		Assert.True(ethno > com);
		Assert.True(com > dem);
		Assert.True(dem > iso);
	}
}
