using System;
using System.Collections.Generic;

namespace RealPolitic.Core;

public static class Unions
{
	/// <summary>
	/// В унии могут объединяться только монархии, племена, каганаты и империи.
	/// </summary>
	public static bool IsUnionIdeology(IdeologyType ideology)
	{
		return ideology == IdeologyType.Monarchy
			|| ideology == IdeologyType.Tribalism
			|| ideology == IdeologyType.Khaganate
			|| ideology == IdeologyType.Empire;
	}

	/// <summary>
	/// Вольные территории объединяются только с другими вольными территориями в конфедерацию вольных территорий.
	/// </summary>
	public static bool IsConfederationIdeology(IdeologyType ideology)
	{
		return ideology == IdeologyType.FreeTerritory;
	}

	/// <summary>
	/// Совместимы ли две идеологии по форме добровольного объединения (уния / федерация / конфедерация вольных территорий).
	/// </summary>
	public static bool CanMergeTogether(IdeologyType leaderIdeology, IdeologyType candidateIdeology)
	{
		if (IsConfederationIdeology(leaderIdeology) || IsConfederationIdeology(candidateIdeology))
		{
			return leaderIdeology == IdeologyType.FreeTerritory && candidateIdeology == IdeologyType.FreeTerritory;
		}
		return IsUnionIdeology(leaderIdeology) == IsUnionIdeology(candidateIdeology);
	}

	public struct JoinCheck
	{
		public bool Ok;

		public string ReasonKey;

		public string ReasonDetail;

		public static JoinCheck Yes()
		{
			return new JoinCheck
			{
				Ok = true,
				ReasonKey = "RP_UNION_OK"
			};
		}

		public static JoinCheck No(string key, string detail)
		{
			return new JoinCheck
			{
				Ok = false,
				ReasonKey = key,
				ReasonDetail = detail
			};
		}
	}

	public static JoinCheck CanJoin(Union union, KingdomPolitics candidate, DiplomatSnapshot candidateSnap, UnionRules rules)
	{
		if (union == null)
		{
			throw new ArgumentNullException("union");
		}
		if (candidate == null)
		{
			throw new ArgumentNullException("candidate");
		}
		if (union.Contains(candidate.KingdomId))
		{
			return JoinCheck.No("RP_UNION_ALREADY_MEMBER", "уже состоит в этом союзе");
		}
		if (union.TotalSize >= rules.MaxMembers)
		{
			return JoinCheck.No("RP_UNION_TOO_LARGE", "в союзе уже " + union.TotalSize + " из " + rules.MaxMembers + " держав");
		}
		if (union.Type == UnionType.Vassal && union.LeaderWarriors > 0 && (float)candidateSnap.Warriors * rules.VassalPowerRatio > (float)union.LeaderWarriors)
		{
			return JoinCheck.No("RP_UNION_TOO_STRONG", "слишком силён для вассалитета");
		}
		float num = Ideologies.Affinity(union.LeaderIdeology.Type, candidate.Ideology);
		if (num < rules.MinAffinity)
		{
			return JoinCheck.No("RP_UNION_IDEOLOGY_GAP", "сродство идеологий " + num.ToString("F2") + " < " + rules.MinAffinity.ToString("F2"));
		}
		if (union.Type == UnionType.Merger && !CanMergeTogether(union.LeaderIdeology.Type, candidate.Ideology))
		{
			if (IsConfederationIdeology(union.LeaderIdeology.Type) || IsConfederationIdeology(candidate.Ideology))
			{
				return JoinCheck.No("RP_UNION_FORM_MISMATCH", "вольные территории объединяются только с другими вольными территориями в конфедерацию");
			}
			return JoinCheck.No("RP_UNION_FORM_MISMATCH", "в унии объединяются только монархии, империи, каганаты и племена, остальные — в федерации");
		}
		if (candidate.Legitimacy < rules.MinMemberLegitimacy)
		{
			return JoinCheck.No("RP_UNION_LOW_LEGITIMACY", "легитимность " + (int)candidate.Legitimacy + " < " + (int)rules.MinMemberLegitimacy);
		}
		if (candidate.AvgDissent > rules.MaxMemberDissent)
		{
			return JoinCheck.No("RP_UNION_UNREST", "недовольство " + candidate.AvgDissent.ToString("F2") + " > " + rules.MaxMemberDissent.ToString("F2"));
		}
		if (candidate.InWar)
		{
			return JoinCheck.No("RP_UNION_AT_WAR", "ведёт войну");
		}
		if (candidate.Revolution != null)
		{
			return JoinCheck.No("RP_UNION_REVOLUTION", "в государстве революция");
		}
		return JoinCheck.Yes();
	}

	/// <summary>Годовой тик живой части унии: вассалы и (редко) незавершённые переходные соединения.
	/// Поглощённые державы сюда не попадают — их жизнь кончилась в момент поглощения.</summary>
	public static List<int> Tick(Union union, Func<int, KingdomPolitics?> kingdomLookup, UnionRules rules, Random rng)
	{
		List<int> list = new List<int>();
		if (union == null || union.Type == UnionType.Merger)
		{
			return list;
		}
		float num = 0f;
		int num2 = 0;
		for (int num3 = union.Members.Count - 1; num3 >= 0; num3--)
		{
			UnionMember unionMember = union.Members[num3];
			if (!unionMember.IsLeader)
			{
				KingdomPolitics kingdomPolitics = kingdomLookup(unionMember.KingdomId);
				if (kingdomPolitics == null)
				{
					union.Members.RemoveAt(num3);
					list.Add(unionMember.KingdomId);
				}
				else
				{
					float num4 = Ideologies.Affinity(union.LeaderIdeology.Type, kingdomPolitics.Ideology);
					num += num4;
					num2++;
					float num5 = (num4 - 0.45f) * 0.1f;
					if (kingdomPolitics.InWar)
					{
						num5 -= 0.03f;
					}
					if (kingdomPolitics.Starving)
					{
						num5 -= 0.05f;
					}
					if (union.Type == UnionType.Vassal && kingdomPolitics.Ideology == IdeologyType.FreeTerritory)
					{
						// Вольная территория ненавидит быть чьим-либо вассалом и стремится сбросить ярмо.
						num5 -= 0.16f;
					}
					num5 -= kingdomPolitics.AvgDissent * 0.08f;
					num5 += (float)(rng.NextDouble() - 0.5) * 0.02f;
					unionMember.Approval = Clamp(unionMember.Approval + num5, 0f, 1f);
					if (unionMember.Approval < rules.BreakupApproval)
					{
						union.Members.RemoveAt(num3);
						list.Add(unionMember.KingdomId);
					}
				}
			}
		}
		float num6 = -0.01f;
		if (num2 > 0)
		{
			float num7 = num / (float)num2;
			num6 += (num7 - 0.4f) * 0.12f;
		}
		else
		{
			num6 -= 0.2f;
		}
		union.Cohesion = Clamp(union.Cohesion + num6, 0f, 1f);
		if (union.Cohesion < rules.BreakupCohesion || union.Members.Count <= 1)
		{
			for (int i = 0; i < union.Members.Count; i++)
			{
				if (!list.Contains(union.Members[i].KingdomId))
				{
					list.Add(union.Members[i].KingdomId);
				}
			}
			union.Members.Clear();
		}
		return list;
	}

	public static Union Create(int id, UnionType type, string name, KingdomPolitics leader, int year, int leaderWarriors = 0)
	{
		Union union = new Union();
		union.Id = id;
		union.Type = type;
		union.Name = name;
		union.LeaderId = leader.KingdomId;
		union.FormedYear = year;
		union.Cohesion = 0.7f;
		union.LeaderWarriors = leaderWarriors;
		union.SetLeaderIdeology(leader.Ideology);
		union.Members.Add(new UnionMember
		{
			KingdomId = leader.KingdomId,
			IsLeader = true,
			Approval = 1f,
			JoinedYear = year
		});
		return union;
	}

	public static void AddMember(Union union, int kingdomId, int year, float approval = 0.6f)
	{
		union.Members.Add(new UnionMember
		{
			KingdomId = kingdomId,
			IsLeader = false,
			Approval = approval,
			JoinedYear = year
		});
	}

	/// <summary>Влияние унии на лояльность: вассал тяготится данью, поглощённого уже нет.</summary>
	public static float LoyaltyModifier(UnionType type, float approval)
	{
		return type switch
		{
			UnionType.Vassal => -0.15f,
			_ => 0.05f
		} + (approval - 0.5f) * 0.2f;
	}

	public static float TradeModifier(UnionType type)
	{
		return type switch
		{
			UnionType.Vassal => 1.1f,
			_ => 1.25f
		};
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
