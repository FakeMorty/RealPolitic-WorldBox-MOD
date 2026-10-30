using System;
using System.Collections.Generic;

namespace RealPolitic.Core;

/// <summary>Сколько палат в державе.</summary>
public enum ChamberCount
{
	/// <summary>Одна палата — народное собрание.</summary>
	One = 0,

	/// <summary>Две палаты — народное собрание и совет земель.</summary>
	Two = 1
}

/// <summary>От чего считаются голоса в верхней палате.</summary>
public enum UpperChamberBasis
{
	/// <summary>Один город — один голос.</summary>
	Cities = 0,

	/// <summary>Один народ — один голос (малым народам защита).</summary>
	Peoples = 1,

	/// <summary>Знатные роды: чем древнее род, тем больше весу.</summary>
	Clans = 2
}

/// <summary>Где сейчас законопроект.</summary>
public enum BillStage
{
	/// <summary>Ждёт голосования нижней палаты.</summary>
	Lower = 0,

	/// <summary>Нижняя приняла, ждёт верхнюю.</summary>
	Upper = 1,

	/// <summary>Палаты не сошлись: согласительная комиссия на год.</summary>
	Conference = 2,

	/// <summary>Принят палатами, но правитель наложил вето.</summary>
	Vetoed = 3,

	/// <summary>Закон принят.</summary>
	Passed = 4,

	/// <summary>Закон отклонён.</summary>
	Rejected = 5
}

/// <summary>
/// 0.53: законопроект. Владелец 30.10.2026 выбрал: палаты решают по-настоящему,
/// у правителя есть вето, а сколько палат и от чего верхняя — решает конституция.
/// </summary>
public sealed class Bill
{
	public int KingdomId { get; set; }

	/// <summary>Какая статья меняется (ключ вроде RP_CONST_ART_TAX).</summary>
	public string Article { get; set; } = string.Empty;

	public string From { get; set; } = string.Empty;

	public string To { get; set; } = string.Empty;

	/// <summary>Закручивает ли гайки (для предсказания голосов).</summary>
	public bool Tightens { get; set; }

	/// <summary>Внесено народом (движением), а не правителем.</summary>
	public bool FromPeople { get; set; }

	/// <summary>
	/// Какая партия внесла закон. Партия не пишет закон, который сама не поддерживает, —
	/// поэтому её места всегда идут «за». Пусто — вносил правитель.
	/// </summary>
	public IdeologyType? ProposerIdeology { get; set; }

	public int ProposedYear { get; set; }

	public BillStage Stage { get; set; } = BillStage.Lower;

	public int LowerFor { get; set; }

	public int LowerAgainst { get; set; }

	public int UpperFor { get; set; }

	public int UpperAgainst { get; set; }

	/// <summary>Год повторного голосования в согласительной комиссии.</summary>
	public int ConferenceYear { get; set; }

	/// <summary>Кто внёс — для летописи.</summary>
	public string EditorName { get; set; } = string.Empty;

	/// <summary>Закон ещё обсуждается.</summary>
	public bool Pending => Stage == BillStage.Lower || Stage == BillStage.Upper
		|| Stage == BillStage.Conference || Stage == BillStage.Vetoed;
}

/// <summary>
/// 0.53: правила двух палат. Только арифметика — проверяется тестами без запуска игры.
/// </summary>
public static class ChamberRules
{
	/// <summary>Мест в нижней палате (как и было с 0.46).</summary>
	public const int LowerSeats = 100;

	/// <summary>Больше этого числа мест в верхней палате не бывает: совет не должен быть толпой.</summary>
	public const int UpperMaxSeats = 40;

	/// <summary>Сколько лет палаты могут не договориться, прежде чем это станет кризисом.</summary>
	public const int DeadlockYears = 20;

	/// <summary>Насколько падает законность за каждый год полного ступора.</summary>
	public const float DeadlockLegitimacyHit = 1f;

	/// <summary>Простое большинство: строго больше половины.</summary>
	public static bool Majority(int forVotes, int againstVotes)
	{
		int total = forVotes + againstVotes;
		if (total <= 0)
		{
			return false;
		}
		return forVotes * 2 > total;
	}

	/// <summary>Две трети — этим палаты преодолевают вето правителя.</summary>
	public static bool TwoThirds(int forVotes, int againstVotes)
	{
		int total = forVotes + againstVotes;
		if (total <= 0)
		{
			return false;
		}
		return forVotes * 3 >= total * 2;
	}

	/// <summary>
	/// Сколько мест в верхней палате: от городов, от народов или с учётом древности родов.
	/// </summary>
	public static int UpperSeats(UpperChamberBasis basis, int cities, int peoples, int clans)
	{
		int seats;
		switch (basis)
		{
			case UpperChamberBasis.Peoples:
				seats = peoples;
				break;
			case UpperChamberBasis.Clans:
				// Родов обычно вдвое больше, чем городов, но и они упираются в потолок.
				seats = clans > 0 ? clans : cities / 2;
				break;
			default:
				seats = cities;
				break;
		}
		if (seats < 1)
		{
			return 1;
		}
		return Math.Min(UpperMaxSeats, seats);
	}

	/// <summary>
	/// Нужна ли для этой статьи вторая палата.
	/// Деньги даёт народ, а войну, границы, союзы и землю решают земли.
	/// </summary>
	public static bool NeedsUpperChamber(string article)
	{
		if (string.IsNullOrEmpty(article))
		{
			return false;
		}
		return article == ConstitutionAmendments.ArticleBorders
			|| article == ConstitutionAmendments.ArticleUnion
			|| article == ConstitutionAmendments.ArticleLand
			|| article == ConstitutionAmendments.ArticleLevy;
	}

	/// <summary>Ветствует ли правитель закон, идущий против его взглядов.</summary>
	public static bool RulerVetoes(RulerTrait trait, bool tightens, bool fromPeople)
	{
		switch (trait)
		{
			case RulerTrait.Cruel:
			case RulerTrait.Conqueror:
				// Властные не любят, когда закон ослабляет их власть.
				return !tightens;
			case RulerTrait.Just:
			case RulerTrait.Diplomat:
				// Мягкие не любят, когда закон закручивает гайки, особенно если просят люди.
				return tightens && fromPeople;
			case RulerTrait.Miser:
				return !tightens; // скупой не отдаёт и не раздаёт
			default:
				return false;
		}
	}

	/// <summary>
	/// Как голосует нижняя палата. Партии-сторонники перемен голосуют «за», противники — «против».
	/// spendthrift — «щедрый» правитель, чьи союзники тянут к ослаблению.
	/// </summary>
	public static void LowerVote(KingdomPolitics state, Bill bill, out int forSeats, out int againstSeats)
	{
		forSeats = 0;
		againstSeats = 0;
		if (state?.Parties == null || state.Parties.Count == 0)
		{
			// Партий нет (племя, империя) — палата голосует по настроению державы.
			int support = (int)Math.Round(Math.Max(0f, Math.Min(100f, state?.Support ?? 50f)));
			int seats = LowerSeats * support / 100;
			forSeats = bill.Tightens ? LowerSeats - seats : seats;
			againstSeats = LowerSeats - forSeats;
			return;
		}
		for (int i = 0; i < state.Parties.Count; i++)
		{
			PoliticalParty party = state.Parties[i];
			if (party == null)
			{
				continue;
			}
			int seats = Math.Max(0, party.SeatsPercent);
			bool favoursChange = Favours(party.Ideology, bill.Tightens);
			// 0.53: партия голосует за свой собственный закон, а правящая — за закон своего правительства.
			if (party.IsRuling || (bill.ProposerIdeology.HasValue && bill.ProposerIdeology.Value == party.Ideology))
			{
				favoursChange = true;
			}
			if (bill.FromPeople)
			{
				// Народ просит — партия, близкая к народу по духу, поддерживает.
				favoursChange = favoursChange || PoliticalAxisIsPopular(party.Ideology);
			}
			if (favoursChange)
			{
				forSeats += seats;
			}
			else
			{
				againstSeats += seats;
			}
		}
	}

	/// <summary>Тянет ли строй к закручиванию или к ослаблению.</summary>
	public static bool Favours(IdeologyType ideology, bool tightens)
	{
		bool authoritarian = ideology == IdeologyType.Monarchy
			|| ideology == IdeologyType.Empire
			|| ideology == IdeologyType.Theocracy
			|| ideology == IdeologyType.Khaganate
			|| ideology == IdeologyType.Militarism
			|| ideology == IdeologyType.Aristocracy
			|| ideology == IdeologyType.Ethnonationalism
			|| ideology == IdeologyType.Tribalism;
		return tightens == authoritarian;
	}

	private static bool PoliticalAxisIsPopular(IdeologyType ideology)
	{
		return ideology == IdeologyType.Democracy
			|| ideology == IdeologyType.Liberalism
			|| ideology == IdeologyType.Egalitarianism
			|| ideology == IdeologyType.Communism;
	}

	/// <summary>
	/// Голоса верхней палаты. Города и народы голосуют «как держава в целом», знатные роды —
	/// по своей выгоде.
	/// </summary>
	public static void UpperVote(UpperChamberBasis basis, int seats, Bill bill, float legitimacy, float support,
		out int forSeats, out int againstSeats)
	{
		forSeats = 0;
		againstSeats = 0;
		if (seats <= 0)
		{
			return;
		}
		float mood = Math.Max(0f, Math.Min(1f, (legitimacy * 0.5f + support * 0.5f) / 100f));
		if (basis == UpperChamberBasis.Clans)
		{
			// Знать держится за власть: ослабление поддерживает, закручивание — нет.
			float share = bill.Tightens ? 1f - mood : mood;
			forSeats = (int)Math.Round(seats * share);
		}
		else
		{
			forSeats = (int)Math.Round(seats * mood);
		}
		forSeats = Math.Max(0, Math.Min(seats, forSeats));
		againstSeats = seats - forSeats;
	}

	/// <summary>Сколько палат в державе: по конституции, а если там не сказано — по строю.</summary>
	public static ChamberCount ChambersOf(KingdomPolitics state)
	{
		Constitution c = state?.Constitution;
		if (c != null)
		{
			return c.Chambers;
		}
		return DefaultChambersFor(state?.Ideology ?? IdeologyType.Tribalism);
	}

	/// <summary>Значение по умолчанию: республикам — две палаты, монархиям — одна.</summary>
	public static ChamberCount DefaultChambersFor(IdeologyType ideology)
	{
		switch (ideology)
		{
			case IdeologyType.Democracy:
			case IdeologyType.Liberalism:
			case IdeologyType.Technocracy:
			case IdeologyType.MerchantRepublic:
			case IdeologyType.Aristocracy:
			case IdeologyType.Egalitarianism:
				return ChamberCount.Two;
			default:
				return ChamberCount.One;
		}
	}

	/// <summary>Может ли держава вообще иметь палаты этого размера (нужны города).</summary>
	public static bool HasChambers(KingdomPolitics state, int cityCount)
	{
		return cityCount > 0 && state != null;
	}
}
