using System;
using System.Collections.Generic;

namespace RealPolitic.Core;

/// <summary>
/// Избирательная система и система регулярной ротации власти:
/// 15-летний цикл, максимум 3 срока за жизнь,
/// непоследовательные переизбрания (экс-лидеры возвращаются в пул кандидатов).
///
/// Охватывает все строи с коллегиальной сменой главы: демократия (всеобщие выборы), эгалитаризм (вече),
/// торговая республика (совет гильдий), технократия (совет академии), либерализм (парламент),
/// теократия (конклав) и аристократия (сенат). Коммунизм цикла не имеет.
/// </summary>
public static class ElectionSystem
{
	public const int TermYears = 15;
	public const int MaxLifetimeTerms = 3;

    /// <summary>0.26 controller owns actual ballots. Never substitute the legacy random election.</summary>
    public static Func<KingdomPolitics,int,PolEventBus,bool> CivicElectionHandler { get; set; }

	/// <summary>
	/// Электротехнический старт при реформе/смене строя: история сроков — ПО ЖИЗНИ человека,
	/// поэтому записи хозяйства перекочёвывают в новое состояние. Раньше каждая реформа
	/// начинала счётчик с нуля — и один и тот же лидер мог быть переизбран сколько угодно.
	/// </summary>
	public static ElectionState ReformedElectoralState(ElectionState? old, int year)
	{
		ElectionState fresh = new ElectionState { NextElectionYear = year + TermYears, TermsServed = 1 };
		if (old != null)
		{
			fresh.PastPresidents = old.PastPresidents;
		}
		return fresh;
	}

	/// <summary>
	/// Кандидат на выборный пост — всегда реальный житель (игровой слой подставляет юнита).
	/// </summary>
	public struct ElectionCandidate
	{
		public int ActorId;
		public string Name;
		public bool IsFemale;
		public int Age;
	}

	/// <summary>
	/// Поставщик кандидатов: (kingdomId, excludeActorId) → взрослый житель державы.
	/// В ядре без игры — null: тогда победитель получает отрицательный синтетический id
	/// (никогда не пересекается с id юнитов WorldBox) и пустое имя, которое игровой слой
	/// заменит именем реального юнита. Никаких предзаданных имён в ядре нет.
	/// </summary>
	public static Func<int, int, ElectionCandidate?>? CandidateResolver { get; set; }

	public static Func<int, int, bool>? CandidateValidator { get; set; }

    private static bool IsEligible(KingdomPolitics state, int actorId)
    {
        if (CandidateValidator == null) return true;
        try { return CandidateValidator(state.KingdomId, actorId); }
        catch { return false; }
    }

    private static int _syntheticCounter;

	/// <summary>Синтетический id для мира без игрового слоя: строго отрицательный.</summary>
	public static int NextSyntheticActorId()
	{
		_syntheticCounter++;
		return -(1000 + _syntheticCounter);
	}

	public static ElectionCandidate ResolveCandidate(KingdomPolitics state, int excludeActorId, Random rng)
	{
		ElectionCandidate? real = null;
		if (CandidateResolver != null)
		{
			try
			{
				real = CandidateResolver(state.KingdomId, excludeActorId);
			}
			catch
			{
				real = null;
			}
		}
		if (real.HasValue && real.Value.ActorId != 0)
		{
			PastPresidentRecord past = state.Election?.FindPastPresident(real.Value.ActorId);
			bool unlimited = state.Ideology == IdeologyType.FreeTerritory;
            if (!string.IsNullOrWhiteSpace(real.Value.Name) && real.Value.ActorId != excludeActorId
                && (unlimited || past == null || past.TermsCount < MaxLifetimeTerms)
                && IsEligible(state, real.Value.ActorId)) return real.Value;
		}
        // In a running game, no candidate is preferable to a fictional winner.
        if (CandidateResolver != null) return default;
		return new ElectionCandidate
		{
			ActorId = NextSyntheticActorId(),
			Name = string.Empty,
			IsFemale = false,
			Age = 35 + (rng != null ? rng.Next(25) : 10)
		};
	}

	public static string GetReelectedLocKey(IdeologyType ideology)
	{
		switch (ideology)
		{
			case IdeologyType.FreeTerritory:
				return "RP_EV_ELECTION_FREETERRITORY_REELECTED";
			case IdeologyType.Egalitarianism:
				return "RP_EV_ELECTION_EGALITARIANISM_REELECTED";
			case IdeologyType.MerchantRepublic:
				return "RP_EV_ELECTION_MERCHANTREPUBLIC_REELECTED";
			case IdeologyType.Technocracy:
				return "RP_EV_ELECTION_TECHNOCRACY_REELECTED";
			case IdeologyType.Liberalism:
				return "RP_EV_ELECTION_LIBERALISM_REELECTED";
			case IdeologyType.Theocracy:
				return "RP_EV_ELECTION_THEOCRACY_REELECTED";
			case IdeologyType.Aristocracy:
				return "RP_EV_ELECTION_ARISTOCRACY_REELECTED";
			case IdeologyType.Democracy:
			default:
				return "RP_EV_ELECTION_DEMOCRACY_REELECTED";
		}
	}

	public static string GetReturnLocKey(IdeologyType ideology)
	{
		switch (ideology)
		{
			case IdeologyType.FreeTerritory:
				return "RP_EV_ELECTION_FREETERRITORY_RETURN";
			case IdeologyType.Egalitarianism:
				return "RP_EV_ELECTION_EGALITARIANISM_RETURN";
			case IdeologyType.MerchantRepublic:
				return "RP_EV_ELECTION_MERCHANTREPUBLIC_RETURN";
			case IdeologyType.Technocracy:
				return "RP_EV_ELECTION_TECHNOCRACY_RETURN";
			case IdeologyType.Liberalism:
				return "RP_EV_ELECTION_LIBERALISM_RETURN";
			case IdeologyType.Theocracy:
				return "RP_EV_ELECTION_THEOCRACY_RETURN";
			case IdeologyType.Aristocracy:
				return "RP_EV_ELECTION_ARISTOCRACY_RETURN";
			case IdeologyType.Democracy:
			default:
				return "RP_EV_ELECTION_DEMOCRACY_RETURN";
		}
	}

	public static string GetNewLocKey(IdeologyType ideology)
	{
		switch (ideology)
		{
			case IdeologyType.FreeTerritory:
				return "RP_EV_ELECTION_FREETERRITORY_NEW";
			case IdeologyType.Egalitarianism:
				return "RP_EV_ELECTION_EGALITARIANISM_NEW";
			case IdeologyType.MerchantRepublic:
				return "RP_EV_ELECTION_MERCHANTREPUBLIC_NEW";
			case IdeologyType.Technocracy:
				return "RP_EV_ELECTION_TECHNOCRACY_NEW";
			case IdeologyType.Liberalism:
				return "RP_EV_ELECTION_LIBERALISM_NEW";
			case IdeologyType.Theocracy:
				return "RP_EV_ELECTION_THEOCRACY_NEW";
			case IdeologyType.Aristocracy:
				return "RP_EV_ELECTION_ARISTOCRACY_NEW";
			case IdeologyType.Democracy:
			default:
				return "RP_EV_ELECTION_DEMOCRACY_NEW";
		}
	}

	/// <summary>
	/// 0.31: Избрание атамана в вольной территории (FreeTerritory):
	/// - без ограничения числа сроков (атаман может избираться бесконечное число раз);
	/// - атаман правит бессрочно до смерти или пока сами жители вольной территории его не снимут.
	/// </summary>
	public static bool ElectAtaman(KingdomPolitics state, int year, PolEventBus? bus, Random? rng, int excludeActorId = 0, string? previousRulerName = null)
	{
		if (state == null || state.Ideology != IdeologyType.FreeTerritory)
		{
			return false;
		}
		if (state.Election == null)
		{
			state.Election = new ElectionState { NextElectionYear = int.MaxValue, TermsServed = 0 };
		}

		ElectionState election = state.Election;

		// В вольной территории нет лимита сроков: любой прежний атаман (кроме только что снятого) может быть избран снова сколько угодно раз!
		List<PastPresidentRecord> eligibleEx = new List<PastPresidentRecord>();
		for (int i = 0; i < election.PastPresidents.Count; i++)
		{
			PastPresidentRecord p = election.PastPresidents[i];
			if (p.ActorId != excludeActorId && !string.IsNullOrWhiteSpace(p.Name) && IsEligible(state, p.ActorId))
			{
				eligibleEx.Add(p);
			}
		}

		if (eligibleEx.Count > 0 && rng != null && rng.NextDouble() < 0.45)
		{
			PastPresidentRecord chosenEx = eligibleEx[rng.Next(eligibleEx.Count)];
			election.TermsServed = chosenEx.TermsCount + 1;
			chosenEx.TermsCount = election.TermsServed;
			election.LastElectionYear = year;
			election.NextElectionYear = int.MaxValue;
			state.Ruler = new RulerRef
			{
				ActorId = chosenEx.ActorId,
				Name = chosenEx.Name,
				KingdomId = state.KingdomId,
				SinceYear = year,
				Age = 40
			};
			state.Legitimacy = Math.Min(100f, Math.Max(60f, state.Legitimacy + 12f));
			state.Support = Math.Min(100f, Math.Max(58f, state.Support + 14f));
			state.AvgDissent = Math.Max(0f, state.AvgDissent - 0.15f);

			bus?.Publish(new PolEvent(PolEventType.DemocraticElection, GetReturnLocKey(IdeologyType.FreeTerritory))
			{
				Year = year,
				Severity = Severity.Major,
				KingdomId = state.KingdomId,
				ActorId = chosenEx.ActorId
			}.With("ruler", chosenEx.Name)
			 .With("terms", election.TermsServed)
			 .With("election_kind", "free_circle")
			 .With("previous_ruler", previousRulerName ?? string.Empty));
			return true;
		}

		ElectionCandidate cand = ResolveCandidate(state, excludeActorId, rng ?? new Random(1));
		int newId = cand.ActorId;
		string newName = cand.Name ?? string.Empty;
		if (newId == 0)
		{
			return false;
		}
		if (string.IsNullOrWhiteSpace(newName))
		{
			newName = "Вольный атаман #" + Math.Abs(newId);
		}
		PastPresidentRecord? previous = election.FindPastPresident(newId);
		election.TermsServed = (previous?.TermsCount ?? 0) + 1;
		election.RecordPresidentTerm(newId, newName, election.TermsServed);
		election.LastElectionYear = year;
		election.NextElectionYear = int.MaxValue;
		state.Ruler = new RulerRef
		{
			ActorId = newId,
			Name = newName,
			KingdomId = state.KingdomId,
			SinceYear = year,
			IsFemale = cand.IsFemale,
			Age = cand.Age > 0 ? cand.Age : 35
		};
		state.Legitimacy = Math.Min(100f, Math.Max(62f, state.Legitimacy + 14f));
		state.Support = Math.Min(100f, Math.Max(60f, state.Support + 15f));
		state.AvgDissent = Math.Max(0f, state.AvgDissent - 0.16f);

		bus?.Publish(new PolEvent(PolEventType.DemocraticElection, GetNewLocKey(IdeologyType.FreeTerritory))
		{
			Year = year,
			Severity = Severity.Major,
			KingdomId = state.KingdomId,
			ActorId = newId
		}.With("ruler", newName)
		 .With("terms", election.TermsServed)
		 .With("election_kind", "free_circle")
		 .With("previous_ruler", previousRulerName ?? string.Empty));
		return true;
	}

	public static bool HoldElection(KingdomPolitics state, int year, PolEventBus bus, Random rng)
	{
		if (state == null)
		{
			return false;
		}
        if (state.Constitution?.Electoral != null)
            return CivicElectionHandler != null && CivicElectionHandler(state,year,bus);
		if (state.Election == null)
		{
			state.Election = new ElectionState { NextElectionYear = year + TermYears, TermsServed = 1 };
			if (state.Ruler != null)
			{
				state.Election.RecordPresidentTerm(state.Ruler.ActorId, state.Ruler.Name, 1);
			}
			return false;
		}

		ElectionState election = state.Election;
		RulerRef current = state.Ruler;
		IdeologyType ideology = state.Ideology;

		// Проверяем возможность переизбрания действующего лидера:
		bool canIncumbentRun = current != null && election.TermsServed < MaxLifetimeTerms && IsEligible(state, current.ActorId);
		if (canIncumbentRun && current != null)
		{
			// Подстраховка: даже если общий счётчик потерян (де синхронизация),
			// личная запись с 3+ сроками ставит точку навсегда.
			PastPresidentRecord self = election.FindPastPresident(current.ActorId);
			if (self != null && self.TermsCount >= MaxLifetimeTerms)
			{
				canIncumbentRun = false;
				election.TermsServed = self.TermsCount;
			}
		}
		bool incumbentWins = false;

		if (canIncumbentRun)
		{
			// Шанс переизбрания зависит от довольства граждан и легитимности:
			float winChance = 0.5f + (state.Support - 50f) * 0.008f + (state.Legitimacy - 50f) * 0.005f - (state.AvgDissent * 0.2f);
			// 0.39: жители помнят нарушенные обещания — на выборах это минус действующей власти.
			winChance -= PromiseMemory.ElectionPenalty(state);
			if (winChance < 0.15f) winChance = 0.15f;
			if (winChance > 0.85f) winChance = 0.85f;
			if (rng != null && rng.NextDouble() < winChance)
			{
				incumbentWins = true;
			}
		}

		if (incumbentWins && current != null)
		{
			// Действующий лидер переизбран:
			election.TermsServed++;
			election.RecordPresidentTerm(current.ActorId, current.Name, election.TermsServed);
			election.LastElectionYear = year;
			election.NextElectionYear = year + TermYears;
			state.Legitimacy = Math.Min(100f, state.Legitimacy + 8f);
			state.Support = Math.Min(100f, state.Support + 5f);
			state.AvgDissent = Math.Max(0f, state.AvgDissent - 0.05f);

			bus?.Publish(new PolEvent(PolEventType.DemocraticElection, GetReelectedLocKey(ideology))
			{
				Year = year,
				Severity = Severity.Major,
				KingdomId = state.KingdomId,
				ActorId = current.ActorId
			}.With("ruler", current.Name).With("terms", election.TermsServed).With("election_kind", "renewed").With("next_election", election.NextElectionYear));
			return true;
		}

		// Действующий лидер не может или не смог переизбраться:
		if (current != null)
		{
			election.RecordPresidentTerm(current.ActorId, current.Name, election.TermsServed);
		}

		// Ищем доступных экс-лидеров (у кого меньше 3 сроков):
		List<PastPresidentRecord> eligibleEx = new List<PastPresidentRecord>();
		for (int i = 0; i < election.PastPresidents.Count; i++)
		{
			PastPresidentRecord p = election.PastPresidents[i];
			if ((current == null || p.ActorId != current.ActorId) && !string.IsNullOrWhiteSpace(p.Name) && p.TermsCount < MaxLifetimeTerms && IsEligible(state, p.ActorId))
			{
				eligibleEx.Add(p);
			}
		}

		// 45% шанс возвращения экс-лидера, если они есть:
		if (eligibleEx.Count > 0 && rng != null && rng.NextDouble() < 0.45)
		{
			PastPresidentRecord chosenEx = eligibleEx[rng.Next(eligibleEx.Count)];
			election.TermsServed = chosenEx.TermsCount + 1;
			chosenEx.TermsCount = election.TermsServed;
			election.LastElectionYear = year;
			election.NextElectionYear = year + TermYears;
			state.Ruler = new RulerRef
			{
				ActorId = chosenEx.ActorId,
				Name = chosenEx.Name,
				KingdomId = state.KingdomId,
				SinceYear = year,
				Age = (current != null && current.Age > 0) ? Math.Min(75, current.Age + 5) : 45
			};
			state.Legitimacy = Math.Min(100f, state.Legitimacy + 6f);
			state.AvgDissent = Math.Max(0f, state.AvgDissent - 0.04f);

			bus?.Publish(new PolEvent(PolEventType.DemocraticElection, GetReturnLocKey(ideology))
			{
				Year = year,
				Severity = Severity.Major,
				KingdomId = state.KingdomId,
				ActorId = chosenEx.ActorId
			}.With("ruler", chosenEx.Name).With("terms", election.TermsServed).With("election_kind", "return").With("next_election", election.NextElectionYear).With("previous_ruler", current?.Name ?? string.Empty));
			return true;
		}

		// Избрание нового лидера:
		ElectionCandidate cand = ResolveCandidate(state, current != null ? current.ActorId : 0, rng);
		int newId = cand.ActorId;
		string newName = cand.Name ?? string.Empty;
		if (newId == 0)
        {
            election.NextElectionYear = year + 1;
            bus?.Publish(new PolEvent(PolEventType.ElectionDeferred, "RP_EV_ELECTION_DEFERRED")
            { Year = year, KingdomId = state.KingdomId, Severity = Severity.Trivial }
            .With("next_election", election.NextElectionYear));
            return false;
        }
        PastPresidentRecord previous = election.FindPastPresident(newId);
        election.TermsServed = (previous?.TermsCount ?? 0) + 1;
        election.RecordPresidentTerm(newId, newName, election.TermsServed);
		election.LastElectionYear = year;
			election.NextElectionYear = year + TermYears;
		state.Ruler = new RulerRef
		{
			ActorId = newId,
			Name = newName,
			KingdomId = state.KingdomId,
			SinceYear = year,
			IsFemale = cand.IsFemale,
			Age = cand.Age
		};
		state.Legitimacy = Math.Min(100f, state.Legitimacy + 10f);
		state.AvgDissent = Math.Max(0f, state.AvgDissent - 0.08f);

		bus?.Publish(new PolEvent(PolEventType.DemocraticElection, GetNewLocKey(ideology))
		{
			Year = year,
			Severity = Severity.Major,
			KingdomId = state.KingdomId,
			ActorId = newId
		}.With("ruler", newName).With("terms", election.TermsServed).With("election_kind", "new").With("next_election", election.NextElectionYear).With("previous_ruler", current?.Name ?? string.Empty));
		return true;
	}
}
