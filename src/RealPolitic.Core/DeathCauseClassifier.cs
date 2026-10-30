using System;

namespace RealPolitic.Core;

public static class DeathCauseClassifier
{
	public static DeathCause Classify(string? damageType, bool hasKiller, bool killerIsCivUnit, bool killerIsAssassin, bool inRebellion, bool isSacrifice, bool killerIsBoat = false)
	{
		if (isSacrifice)
		{
			return DeathCause.Sacrifice;
		}
		if (inRebellion)
		{
			return DeathCause.DiedInRebellion;
		}
		if (killerIsAssassin)
		{
			return DeathCause.Assassination;
		}
		string text = (damageType ?? string.Empty).ToLowerInvariant();
		if (text.Contains("age") || text.Contains("natural"))
		{
			return DeathCause.OldAge;
		}
		if (text.Contains("starv") || text.Contains("hunger"))
		{
			return DeathCause.Starvation;
		}
		if (text.Contains("poison") || text.Contains("disease") || text.Contains("plague") || text.Contains("infection") || text.Contains("tumor") || text.Contains("ashfever"))
		{
			return DeathCause.Disease;
		}
		if (text.Contains("fire") || text.Contains("burn") || text.Contains("acid"))
		{
			return DeathCause.Fire;
		}
		if (text.Contains("freeze") || text.Contains("frost") || text.Contains("cold"))
		{
			return DeathCause.Frozen;
		}
		if (text.Contains("drown") || text.Contains("water"))
		{
			return DeathCause.Drowning;
		}
		if (text.Contains("lightning") || text.Contains("divine") || text.Contains("god"))
		{
			return DeathCause.DivineWrath;
		}
		if (text.Contains("curse") || text.Contains("void"))
		{
			return DeathCause.Curse;
		}
		if (text.Contains("tired") || text.Contains("overwork") || text.Contains("exhaust"))
		{
			return DeathCause.Overwork;
		}
		// Метеориты, бомбы и прочие взрывы в игре проходят как AttackType.Explosion.
		if (text.Contains("explosion") || text.Contains("explode") || text.Contains("bomb"))
		{
			return DeathCause.Explosion;
		}
		// Gravity — падение (лодки, сброшенные с обрыва юниты и т. п.).
		if (text.Contains("gravity") || text.Contains("fell"))
		{
			return DeathCause.Fell;
		}
		if (text.Contains("quake") || text.Contains("tornado") || text.Contains("meteor") || text.Contains("volcano") || text.Contains("earth"))
		{
			return DeathCause.Disaster;
		}
		if (text.Contains("eaten"))
		{
			return DeathCause.AnimalAttack;
		}
		if (text.Contains("weapon") && !hasKiller)
		{
			return DeathCause.KilledInBattle;
		}
		if (hasKiller)
		{
			// 0.23.12: корабль игры (boat_trading/transport) — это судно, а не зверь.
			// Правитель гибнет от его пушек, поэтому отдельная причина «корабельной пушкой».
			if (killerIsBoat)
			{
				return DeathCause.KilledByBoat;
			}
			if (!killerIsCivUnit)
			{
				return DeathCause.AnimalAttack;
			}
			return DeathCause.KilledByUnit;
		}
		return DeathCause.Unknown;
	}

	public static string Describe(DeathRecord rec)
	{
		return rec.Cause switch
		{
			DeathCause.OldAge => rec.ActorName + " умер от старости в " + rec.Age + " лет",
			DeathCause.KilledInBattle => rec.ActorName + " пал в бою" + ByWhom(rec) + Place(rec),
			DeathCause.KilledByUnit => rec.ActorName + " убит" + ByWhom(rec),
			DeathCause.Assassination => rec.ActorName + " убит заговорщиком" + ByWhom(rec),
			DeathCause.Executed => rec.ActorName + " казнён" + ByWhom(rec),
			DeathCause.DiedInRebellion => rec.ActorName + " погиб в восстании",
			DeathCause.Starvation => rec.ActorName + " умер от голода",
			DeathCause.Disease => rec.ActorName + " умер от болезни",
			DeathCause.Fire => rec.ActorName + " сгорел",
			DeathCause.Disaster => rec.ActorName + " погиб от стихии",
		DeathCause.AnimalAttack => rec.ActorName + " растерзан" + ByBeast(rec),
		DeathCause.KilledByBoat => rec.ActorName + " расстрелян" + ByBoat(rec),
		DeathCause.DivineWrath => rec.ActorName + " поражён божественной карой",
			DeathCause.Sacrifice => rec.ActorName + " принесён в жертву",
			DeathCause.Curse => rec.ActorName + " умер от проклятия",
			DeathCause.Drowning => rec.ActorName + " утонул",
			DeathCause.Frozen => rec.ActorName + " замёрз",
			DeathCause.Overwork => rec.ActorName + " умер от истощения",
			DeathCause.Explosion => rec.ActorName + " погиб во взрыве",
			DeathCause.Fell => rec.ActorName + " разбился при падении",
			_ => rec.ActorName + " умер при невыясненных обстоятельствах",
		};
	}

	public static string FormatKillerCountry(DeathRecord rec, bool isBoat = false, bool isBeast = false)
	{
		if (rec == null)
		{
			return string.Empty;
		}
		if (!string.IsNullOrEmpty(rec.KillerKingdom))
		{
			if (!string.IsNullOrEmpty(rec.KingdomNameAtDeath))
			{
				bool same = string.Equals(rec.KillerKingdom, rec.KingdomNameAtDeath, StringComparison.OrdinalIgnoreCase);
				if (isBoat)
				{
					return "держава: " + rec.KillerKingdom + (same ? " [свой флот]" : " [флот чужой державы]");
				}
				return "держава: " + rec.KillerKingdom + (same ? " [своя держава]" : " [чужая держава]");
			}
			return "держава: " + rec.KillerKingdom;
		}
		if (isBoat)
		{
			return "держава судна: не установлена";
		}
		if (isBeast)
		{
			return "вне государств";
		}
		return "держава: без государства";
	}

	/// <summary>
	/// Убийца-человек: имя, занятие, государство и город.
	/// «: Кузь (воин, держава: Мысерод, город: Реин)» — сразу видно, кто и из какой страны.
	/// </summary>
	private static string ByWhom(DeathRecord rec)
	{
		if (string.IsNullOrEmpty(rec.KillerName))
		{
			return string.Empty;
		}
		System.Collections.Generic.List<string> parts = new System.Collections.Generic.List<string>();
		if (!string.IsNullOrEmpty(rec.KillerJob))
		{
			parts.Add(rec.KillerJob);
		}
		else if (!string.IsNullOrEmpty(rec.KillerTitle))
		{
			parts.Add(rec.KillerTitle);
		}
		parts.Add(FormatKillerCountry(rec));
		if (!string.IsNullOrEmpty(rec.KillerCity))
		{
			parts.Add("город: " + rec.KillerCity);
		}
		string detail = (parts.Count == 0) ? string.Empty : (" (" + string.Join(", ", parts) + ")");
		return ": " + rec.KillerName + detail;
	}

	/// <summary>Корабль-убийца: роль судна, его имя-кличка и держава приписки.</summary>
	private static string ByBoat(DeathRecord rec)
	{
		string kind = DeathRecord.BoatKindLabel(rec.KillerSpecies);
		// «из пушки корабля» / «из пушки корабля-торговца» — родительный падеж.
		string ship = string.IsNullOrEmpty(kind) ? "корабля" : ("корабля-" + kind);
		string name = rec.KillerBoatName;
		string shipLabel = string.IsNullOrEmpty(name)
			? (" из пушки " + ship)
			: (" из пушки " + ship + " " + DeathRecord.QuoteName(name));
		string country = FormatKillerCountry(rec, isBoat: true);
		string port = string.IsNullOrEmpty(rec.KillerCity) ? string.Empty : (", порт: " + rec.KillerCity);
		return shipLabel + " (" + country + port + ")";
	}

	/// <summary>Зверь-убийца: вид, кличка и принадлежность.</summary>
	private static string ByBeast(DeathRecord rec)
	{
		bool hasSpecies = !string.IsNullOrEmpty(rec.KillerSpecies);
		bool hasName = !string.IsNullOrEmpty(rec.KillerName);
		if (!hasSpecies && !hasName)
		{
			return " зверем";
		}
		string country = FormatKillerCountry(rec, isBeast: true);
		if (!hasSpecies)
		{
			return " зверем: " + rec.KillerName + " (" + country + ")";
		}
		if (!hasName)
		{
			return ": " + rec.KillerSpecies + " (" + country + ")";
		}
		return ": " + rec.KillerSpecies + " (" + rec.KillerName + ", " + country + ")";
	}

	private static string Place(DeathRecord rec)
	{
		if (!string.IsNullOrEmpty(rec.CityName))
		{
			return " у " + rec.CityName;
		}
		return string.Empty;
	}
}
