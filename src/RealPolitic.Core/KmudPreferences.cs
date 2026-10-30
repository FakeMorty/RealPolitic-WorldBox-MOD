using System;

namespace RealPolitic.Core;

/// <summary>
/// Кто чего хочет на конференции: мнение одного жителя по одному вопросу.
/// Никакого случайного числа: ответ выводится из строя, убеждённости и положения
/// собственной державы — поэтому один и тот же житель отвечает одинаково, а тесты
/// проверяют поведение, а не везение.
/// </summary>
public static class KmudPreferences
{
	/// <summary>Положение жителя при голосовании.</summary>
	public sealed class Situation
	{
		/// <summary>Строй собственной державы.</summary>
		public IdeologyType Own = IdeologyType.Monarchy;

		/// <summary>Насколько житель убеждён в своём строе (0..1).</summary>
		public float Conviction = 0.5f;

		/// <summary>Недовольство своим строем (0..1) — сильнее тянет к переменам.</summary>
		public float Dissent;

		/// <summary>Его держава — агрессор в долгом конфликте.</summary>
		public bool OwnIsAggressor;

		/// <summary>Его держава — защитник в долгом конфликте.</summary>
		public bool OwnIsDefender;

		/// <summary>Война у его державы идёт давно и измотала её.</summary>
		public bool WarWeary;

		/// <summary>Его держава — та самая «безнравственная».</summary>
		public bool OwnIsAccused;

		/// <summary>Его держава беднее всех — это о ней голосуют в вопросе о помощи.</summary>
		public bool OwnIsPoorest;

		/// <summary>Его держава сама еле сводит концы с концами.</summary>
		public bool OwnIsPoor;

		/// <summary>0.47: его держава — та, куда возвращается народ.</summary>
		public bool OwnIsRepatriationTarget;

		/// <summary>0.47: его держава — та, откуда народ уходит.</summary>
		public bool OwnIsRepatriationSource;

		/// <summary>0.47: он сам того самого рода, о котором идёт пятый вопрос.</summary>
		public bool OwnIsRepatriatedRace;
	}

	/// <summary>Ответ жителя на вопрос конференции.</summary>
	public static int Decide(KmudQuestion question, Situation situation)
	{
		if (situation == null)
		{
			return 0;
		}
		switch (question)
		{
			case KmudQuestion.ConflictOutcome:
				return DecideOutcome(situation);
			case KmudQuestion.PeaceYears:
				return DecidePeaceYears(situation);
			case KmudQuestion.Punish:
				return DecidePunish(situation);
			case KmudQuestion.Aid:
				return DecideAid(situation);
			case KmudQuestion.Repatriation:
				return DecideRepatriation(situation);
			default:
				return 0;
		}
	}

	/// <summary>
	/// Вопрос 1. Кто больше выиграл от войны, тот хочет победы; кто устал, тот хочет мира;
	/// воинственные строи тянут к силе, мирные — к белому миру.
	/// </summary>
	private static int DecideOutcome(Situation s)
	{
		float aggressor = Aggression(s.Own) * (0.6f + 0.4f * s.Conviction);
		float defender = 0.30f;
		float white = Peacefulness(s.Own);

		if (s.OwnIsAggressor)
		{
			aggressor += 0.35f;
			defender -= 0.15f;
		}
		if (s.OwnIsDefender)
		{
			defender += 0.45f;
			aggressor -= 0.10f;
		}
		if (s.WarWeary)
		{
			// Измотанная войной держава теряет охоту побеждать: даже воинственный строй
			// склоняется к белому миру, иначе конференция никогда не заканчивала войны.
			white += 0.45f;
			aggressor *= 0.5f;
			defender -= 0.10f;
		}
		// Недовольный своим строем житель не хочет, чтобы его держава побеждала «за счёт других».
		if (s.Dissent > 0.6f)
		{
			white += 0.15f;
		}
		return Best(new[]
		{
			(KmudOptions.OutcomeAggressor, aggressor),
			(KmudOptions.OutcomeDefender, defender),
			(KmudOptions.OutcomeWhitePeace, white)
		});
	}

	/// <summary>Вопрос 2. Воинственные хотят нуля лет, миролюбивые — длинного мира.</summary>
	private static int DecidePeaceYears(Situation s)
	{
		// База: насколько строй склонен к миру (0..1) плюс терпимость к войне у уставших.
		float peace = Peacefulness(s.Own);
		if (s.WarWeary)
		{
			peace += 0.35f;
		}
		if (s.OwnIsDefender)
		{
			peace += 0.15f;
		}
		if (s.OwnIsAggressor)
		{
			peace -= 0.25f;
		}
		if (s.Dissent > 0.6f)
		{
			peace -= 0.1f;
		}
		int years = peace switch
		{
			>= 0.85f => KmudOptions.Peace30,
			>= 0.60f => KmudOptions.Peace20,
			>= 0.30f => KmudOptions.Peace10,
			_ => KmudOptions.Peace0
		};
		return years;
	}

	/// <summary>Вопрос 3. Чем ближе строй к свободе, тем мягче наказание.</summary>
	private static int DecidePunish(Situation s)
	{
		if (s.OwnIsAccused)
		{
			// Обвиняемая держава голосует за «ничего не делать» — это её право.
			return KmudOptions.PunishNothing;
		}
		float harsh = 0f;
		switch (s.Own)
		{
			case IdeologyType.Communism:
			case IdeologyType.Egalitarianism:
				harsh = 1.0f;
				break;
			case IdeologyType.Theocracy:
			case IdeologyType.Khaganate:
			case IdeologyType.Militarism:
			case IdeologyType.Ethnonationalism:
				harsh = 0.85f;
				break;
			case IdeologyType.Aristocracy:
			case IdeologyType.Monarchy:
			case IdeologyType.Empire:
				harsh = 0.55f;
				break;
			case IdeologyType.Democracy:
			case IdeologyType.Liberalism:
			case IdeologyType.Technocracy:
				harsh = 0.35f;
				break;
			case IdeologyType.MerchantRepublic:
			case IdeologyType.FreeTerritory:
			case IdeologyType.Tribalism:
				harsh = 0.25f;
				break;
		}
		// Недовольство своим строем: житель не хочет казней, он хочет смены строя.
		if (s.Dissent > 0.5f)
		{
			harsh -= 0.25f;
		}
		if (harsh >= 0.8f)
		{
			return KmudOptions.PunishKillAndReform;
		}
		if (harsh >= 0.5f)
		{
			return KmudOptions.PunishReformOnly;
		}
		if (harsh >= 0.3f)
		{
			return KmudOptions.PunishSanctions;
		}
		return KmudOptions.PunishNothing;
	}

	/// <summary>
	/// Вопрос 5 (0.47). Охранять ли возвращение малого народа.
	/// Сам народ и принимающая держава хотят охраны; держава, из которой уходят, —
	/// против (теряет людей); воинственные строи не хотят тратить силы на чужой народ.
	/// </summary>
	private static int DecideRepatriation(Situation s)
	{
		float guard = 0.25f + Peacefulness(s.Own) * 0.35f;
		float fund = 0.20f;
		float allow = 0.25f;
		float deny = 0.10f + Aggression(s.Own) * 0.35f;
		if (s.OwnIsRepatriatedRace)
		{
			// Это его народ: он хочет, чтобы возвращение было безопасным.
			guard += 0.55f;
			deny -= 0.15f;
		}
		if (s.OwnIsRepatriationTarget)
		{
			guard += 0.25f;
			fund += 0.15f;
		}
		if (s.OwnIsRepatriationSource)
		{
			// Терять жителей никто не хочет: родная держава голосует «не возвращать».
			deny += 0.45f;
			guard -= 0.20f;
		}
		if (s.WarWeary)
		{
			// Уставшие от войны не готовы ещё и конвои охранять, но и мешать не станут.
			guard -= 0.15f;
			allow += 0.20f;
		}
		if (s.OwnIsPoor)
		{
			fund -= 0.10f;
		}
		return Best(new[]
		{
			(KmudOptions.RepatGuard, guard),
			(KmudOptions.RepatFund, fund),
			(KmudOptions.RepatAllow, allow),
			(KmudOptions.RepatDeny, deny)
		});
	}

	/// <summary>Вопрос 4. Кому и чем помочь — по своему строю и достатку.</summary>
	private static int DecideAid(Situation s)
	{
		if (s.OwnIsPoorest)
		{
			// Сама нуждающаяся держава: пусть помогут делом, а не «ничего».
			return KmudOptions.AidMoney;
		}
		if (s.OwnIsPoor)
		{
			// Бедной державе не до ассоциаций — только деньги или еда.
			return Peacefulness(s.Own) >= 0.5f ? KmudOptions.AidMoney : KmudOptions.AidNothing;
		}
		switch (s.Own)
		{
			case IdeologyType.Communism:
			case IdeologyType.Egalitarianism:
				return KmudOptions.AidAssociation;
			case IdeologyType.Theocracy:
				return KmudOptions.AidFood;
			case IdeologyType.MerchantRepublic:
			case IdeologyType.Technocracy:
			case IdeologyType.Liberalism:
			case IdeologyType.Democracy:
				return KmudOptions.AidMoney;
			case IdeologyType.Militarism:
			case IdeologyType.Ethnonationalism:
			case IdeologyType.Khaganate:
			case IdeologyType.Empire:
				return KmudOptions.AidNothing;
			default:
				return KmudOptions.AidFood;
		}
	}

	// ------------------------------------------------------------------
	// характер строя
	// ------------------------------------------------------------------

	/// <summary>Склонность строя к нападению (0..1).</summary>
	private static float Aggression(IdeologyType ideology)
	{
		switch (ideology)
		{
			case IdeologyType.Militarism: return 1.0f;
			case IdeologyType.Khaganate: return 0.9f;
			case IdeologyType.Empire: return 0.85f;
			case IdeologyType.Ethnonationalism: return 0.8f;
			case IdeologyType.Theocracy: return 0.55f;
			case IdeologyType.Aristocracy: return 0.5f;
			case IdeologyType.Monarchy: return 0.45f;
			case IdeologyType.MerchantRepublic: return 0.4f;
			case IdeologyType.Technocracy: return 0.35f;
			case IdeologyType.Tribalism: return 0.35f;
			case IdeologyType.Democracy: return 0.3f;
			case IdeologyType.Liberalism: return 0.25f;
			case IdeologyType.Communism: return 0.2f;
			case IdeologyType.Egalitarianism: return 0.15f;
			case IdeologyType.FreeTerritory: return 0.1f;
			default: return 0.4f;
		}
	}

	/// <summary>Склонность строя к миру (0..1).</summary>
	private static float Peacefulness(IdeologyType ideology)
	{
		switch (ideology)
		{
			case IdeologyType.FreeTerritory: return 1.0f;
			case IdeologyType.Egalitarianism: return 0.9f;
			case IdeologyType.Communism: return 0.8f;
			case IdeologyType.Liberalism: return 0.8f;
			case IdeologyType.Democracy: return 0.7f;
			case IdeologyType.MerchantRepublic: return 0.65f;
			case IdeologyType.Technocracy: return 0.6f;
			case IdeologyType.Tribalism: return 0.55f;
			case IdeologyType.Monarchy: return 0.45f;
			case IdeologyType.Aristocracy: return 0.4f;
			case IdeologyType.Theocracy: return 0.35f;
			case IdeologyType.Ethnonationalism: return 0.3f;
			case IdeologyType.Empire: return 0.25f;
			case IdeologyType.Khaganate: return 0.2f;
			case IdeologyType.Militarism: return 0.05f;
			default: return 0.5f;
		}
	}

	/// <summary>Вариант с наибольшим счётом; при равенстве — первый по порядку объявления.</summary>
	private static int Best((int option, float score)[] candidates)
	{
		int best = 0;
		float bestScore = float.MinValue;
		for (int i = 0; i < candidates.Length; i++)
		{
			if (candidates[i].score > bestScore + 0.0001f)
			{
				bestScore = candidates[i].score;
				best = candidates[i].option;
			}
		}
		return best;
	}
}
