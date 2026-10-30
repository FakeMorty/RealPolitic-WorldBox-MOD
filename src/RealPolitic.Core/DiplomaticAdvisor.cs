using System;
using System.Collections.Generic;

namespace RealPolitic.Core;

public static class DiplomaticAdvisor
{
	public static List<DiplomaticMove> Evaluate(DiplomatSnapshot self, List<DiplomatSnapshot> others, List<RelationInfo> relations, DiplomacyWeights? w = null)
	{
		if (others == null)
		{
			others = new List<DiplomatSnapshot>();
		}
		if (relations == null)
		{
			relations = new List<RelationInfo>();
		}
		w = w ?? new DiplomacyWeights();
		IdeologyDef ideologyDef = Ideologies.Get(self.Ideology);
		List<DiplomaticMove> list = new List<DiplomaticMove>();
		for (int i = 0; i < others.Count; i++)
		{
			DiplomatSnapshot s = others[i];
			if (s.KingdomId == self.KingdomId)
			{
				continue;
			}
			RelationInfo relationInfo = FindRelation(relations, s.KingdomId);
			float num = Ideologies.Affinity(self.Ideology, s.Ideology);
			float num2 = Power(self);
			float num3 = Power(s);
			float num4 = ((num3 <= 0.01f) ? 5f : (num2 / num3));
			bool flag = self.ReligionId != 0 && relationInfo.SharedReligionId == self.ReligionId;
		if (relationInfo.Relation != RelationType.War && relationInfo.Relation != RelationType.Alliance)
		{
			// Союзнику войну не предлагаем: сначала нужен разрыв или исключение из альянса.
			// 0.23.9: война без общей границы невозможна — как у стратегического ИИ
			// (WorldFeeder.FindWarTarget), так и у советника дипломатии. Далёкая держава
			// не порождает кандидатуру «объявить войну»: армию до неё не довести.
			// Отступление: Relation.None значит «дипломатия неизвестна» (API игры не ответил) —
			// тогда поведение прежнее, кандидатуру строим (как в регентских проверках).
			DiplomaticMove diplomaticMove = new DiplomaticMove
				{
					Action = DiplomaticAction.DeclareWar,
					TargetId = s.KingdomId
				};
				float num5 = 10f;
				num5 += (0f - num) * w.Ideology;
				diplomaticMove.Reasons.Add("сродство идеологий " + num.ToString("F2") + " → " + ((0f - num) * w.Ideology).ToString("F1"));
				num5 += Math.Min(1f, num4 - 1f) * w.Power;
				if (num4 > 1f)
				{
					diplomaticMove.Reasons.Add("мы сильнее в " + num4.ToString("F2") + " раза");
				}
				num5 += (float)relationInfo.Claims / 100f * w.Claims;
				if (relationInfo.Claims > 0)
				{
					diplomaticMove.Reasons.Add("территориальные претензии " + relationInfo.Claims);
				}
				num5 += w.Neighbor; // 0.23.9: соседство теперь обязательно, бонус постоянен
				if (relationInfo.Relation == RelationType.Alliance)
				{
					num5 -= w.AlliancePenalty;
				}
				if (self.AtWar)
				{
					num5 -= w.WarPenalty;
				}
				if (self.Starving)
				{
					num5 -= w.Starving;
				}
				if (self.Legitimacy < 40f)
				{
					num5 -= w.WeakLegitimacy;
				}
				if (flag)
				{
					num5 -= w.SharedReligion;
				}
				if (relationInfo.SharedLanguageId != 0)
				{
					num5 -= w.SharedLanguage;
				}
				if (relationInfo.SharedCultureId != 0)
				{
					num5 -= w.SharedCulture;
				}
				num5 += ideologyDef.WarAppetite * 25f;
				if (self.Ideology == IdeologyType.Liberalism)
				{
					num5 -= 15f;
				}
				if (self.Ideology == IdeologyType.Militarism && num4 > 1.2f)
				{
					num5 += 12f;
				}
				if (self.Ideology == IdeologyType.MerchantRepublic)
				{
					num5 -= 10f;
				}
				if (self.Ideology == IdeologyType.Ethnonationalism)
				{
					num5 += 18f;
				}
				if (self.Ideology == IdeologyType.Democracy)
				{
					num5 -= 16f;
				}
			if (self.UnderRegency)
			{
				num5 = -999f;
				diplomaticMove.Reasons.Add("регентство разрешает только оборонительные войны");
			}
			// Войну ДАЛЁКОЙ державе не предлагаем вовсе (армию до неё не довести).
			// Исключение — Relation.None: дипломатия неизвестна, проверки не трогаем
			// (нет данных о соседстве — решать будет верхний слой).
			if (relationInfo.Relation == RelationType.None || relationInfo.Adjacent)
			{
				diplomaticMove.Score = num5;
				list.Add(diplomaticMove);
			}
		}
			if (relationInfo.Relation != RelationType.War && relationInfo.Relation != RelationType.Alliance)
			{
				DiplomaticMove diplomaticMove2 = new DiplomaticMove
				{
					Action = DiplomaticAction.ProposeAlliance,
					TargetId = s.KingdomId
				};
				float num6 = 12f;
				num6 += num * w.Ideology;
				diplomaticMove2.Reasons.Add("сродство идеологий " + num.ToString("F2"));
				float val = 1f - Math.Abs(num4 - 1f);
				num6 += Math.Max(0f, val) * w.Power * 0.5f;
				if (relationInfo.Adjacent)
				{
					num6 += 4f;
				}
				if (flag)
				{
					num6 += w.SharedReligion;
				}
				if (relationInfo.SharedLanguageId != 0)
				{
					num6 += w.SharedLanguage;
				}
				if (relationInfo.SharedCultureId != 0)
				{
					num6 += w.SharedCulture;
				}
				if (self.AtWar)
				{
					num6 += 8f;
				}
				if (self.Legitimacy < 40f)
				{
					num6 += 5f;
				}
				if (self.Ideology == IdeologyType.Militarism)
				{
					num6 -= 12f;
				}
				if (self.Ideology == IdeologyType.Liberalism)
				{
					num6 += 10f;
				}
				if (self.Ideology == IdeologyType.MerchantRepublic)
				{
					num6 += 10f;
				}
				if (self.Ideology == IdeologyType.Monarchy)
				{
					num6 += 5f;
				}
				if (self.Ideology == IdeologyType.Ethnonationalism)
				{
					num6 -= 24f;
				}
				if (self.Ideology == IdeologyType.Democracy)
				{
					num6 += 15f;
				}
				diplomaticMove2.Score = num6;
				list.Add(diplomaticMove2);
			}
			else if (relationInfo.Relation == RelationType.Alliance && !self.AtWar && !s.AtWar && !self.UnderRegency
				&& Unions.CanMergeTogether(self.Ideology, s.Ideology)
				&& num >= 0.75f && num4 >= 0.95f && num4 <= 2.2f && self.Legitimacy >= 55f && s.Legitimacy >= 45f)
			{
				DiplomaticMove federationMove = new DiplomaticMove
				{
					Action = DiplomaticAction.ProposeAlliance,
					TargetId = s.KingdomId
				};
				float fedScore = 18f + num * w.Ideology;
				string kindLabel = Unions.IsConfederationIdeology(self.Ideology)
					? "добровольная конфедерация вольных территорий"
					: (Unions.IsUnionIdeology(self.Ideology) ? "добровольная уния союзников" : "добровольная федерация союзников");
				federationMove.Reasons.Add(kindLabel + ", сродство " + num.ToString("F2"));
				if (relationInfo.Adjacent)
				{
					fedScore += 8f;
				}
				if (flag)
				{
					fedScore += w.SharedReligion;
				}
				if (relationInfo.SharedLanguageId != 0)
				{
					fedScore += w.SharedLanguage;
				}
				if (relationInfo.SharedCultureId != 0)
				{
					fedScore += w.SharedCulture;
				}
				if (self.Ideology == IdeologyType.Democracy || self.Ideology == IdeologyType.Liberalism || self.Ideology == IdeologyType.MerchantRepublic || self.Ideology == IdeologyType.Egalitarianism)
				{
					fedScore += 14f;
				}
				if (self.Ideology == IdeologyType.Ethnonationalism || self.Ideology == IdeologyType.Militarism)
				{
					fedScore -= 20f;
				}
				federationMove.Score = fedScore;
				list.Add(federationMove);
			}
			if (num4 > 1.6f && relationInfo.Relation != RelationType.War)
			{
				DiplomaticMove diplomaticMove3 = new DiplomaticMove
				{
					Action = DiplomaticAction.DemandVassal,
					TargetId = s.KingdomId
				};
				float num7 = 5f + Math.Min(1.5f, num4 - 1.6f) * 25f;
				num7 += num * 10f;
				num7 = ((!relationInfo.Adjacent) ? (num7 - 10f) : (num7 + 6f));
				if (relationInfo.Relation == RelationType.Alliance)
				{
					num7 += 6f;
				}
				if (self.Ideology == IdeologyType.Liberalism)
				{
					num7 += 8f;
				}
				if (self.Ideology == IdeologyType.Monarchy)
				{
					num7 += 10f;
				}
				if (self.Ideology == IdeologyType.Militarism)
				{
					num7 += 8f;
				}
				if (self.Ideology == IdeologyType.Egalitarianism)
				{
					num7 -= 15f;
				}
				if (self.UnderRegency)
				{
					num7 = -999f;
					diplomaticMove3.Reasons.Add("регентство разрешает только оборонительные действия");
				}
				diplomaticMove3.Reasons.Add("превосходство в силе " + num4.ToString("F2"));
				diplomaticMove3.Score = num7;
				list.Add(diplomaticMove3);
			}
			if (relationInfo.Relation == RelationType.War)
			{
				DiplomaticMove diplomaticMove4 = new DiplomaticMove
				{
					Action = DiplomaticAction.MakePeace,
					TargetId = s.KingdomId
				};
				float num8 = 15f;
				if (num4 < 1f)
				{
					num8 += 15f;
				}
				if (self.Starving)
				{
					num8 += 12f;
				}
				if (self.Legitimacy < 40f)
				{
					num8 += 10f;
				}
				num8 += num * 15f;
				num8 += ideologyDef.WarAppetite * -20f;
				if (self.Ideology == IdeologyType.Liberalism)
				{
					num8 += 12f;
				}
				if (self.Ideology == IdeologyType.Democracy)
				{
					num8 += 14f;
				}
				if (self.Ideology == IdeologyType.Ethnonationalism)
				{
					num8 -= 12f;
				}
				diplomaticMove4.Reasons.Add("усталость от войны");
				diplomaticMove4.Score = num8;
				list.Add(diplomaticMove4);
			}
		}
		list.Add(new DiplomaticMove
		{
			Action = DiplomaticAction.Hold,
			TargetId = 0,
			Score = w.HoldBias
		});
		list.Sort((DiplomaticMove a, DiplomaticMove b) => b.Score.CompareTo(a.Score));
		return list;
	}

	public static DiplomaticMove? BestMove(DiplomatSnapshot self, List<DiplomatSnapshot> others, List<RelationInfo> relations, DiplomacyWeights? w = null)
	{
		w = w ?? new DiplomacyWeights();
		List<DiplomaticMove> list = Evaluate(self, others, relations, w);
		if (list.Count == 0)
		{
			return null;
		}
		DiplomaticMove diplomaticMove = list[0];
		if (diplomaticMove.Action == DiplomaticAction.Hold)
		{
			return null;
		}
		if (diplomaticMove.Score < w.ActThreshold)
		{
			return null;
		}
		return diplomaticMove;
	}

	public static float Power(DiplomatSnapshot s)
	{
		float basePower = (float)s.Warriors * 1f + (float)s.Population * 0.25f;
		// 0.24: статья X «О ратной повинности» — гвардия воюет злее, ополчение слабее.
		if (s.Levy.HasValue)
		{
			basePower *= ConstitutionEffects.LevyPowerMultiplier(s.Levy.Value, s.BindingConstitution);
		}
		return basePower;
	}

	private static RelationInfo FindRelation(List<RelationInfo> relations, int otherId)
	{
		for (int i = 0; i < relations.Count; i++)
		{
			if (relations[i].OtherId == otherId)
			{
				return relations[i];
			}
		}
		return new RelationInfo
		{
			OtherId = otherId,
			Relation = RelationType.None
		};
	}
}
