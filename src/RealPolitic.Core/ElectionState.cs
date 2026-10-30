using System.Collections.Generic;

namespace RealPolitic.Core;

/// <summary>
/// Запись об экс-президенте: число отслуженных сроков (максимум 3 за всю жизнь).
/// </summary>
public sealed class PastPresidentRecord
{
	public int ActorId { get; set; }

	public string Name { get; set; } = string.Empty;

	public int TermsCount { get; set; }
}

/// <summary>
/// Состояние избирательной системы государства.
/// Отслеживает текущий срок (до 3 за жизнь), год следующих выборов (15-летний цикл)
/// и пул экс-президентов для непоследовательного переизбрания.
/// </summary>
public sealed class ElectionState
{
	public int TermsServed { get; set; } = 1;

	public int NextElectionYear { get; set; }

	/// <summary>Год, когда выборы проводились в последний раз (0 — ещё не проводились).</summary>
	public int LastElectionYear { get; set; }

	public List<PastPresidentRecord> PastPresidents { get; set; } = new List<PastPresidentRecord>();

	public PastPresidentRecord? FindPastPresident(int actorId)
	{
		for (int i = 0; i < PastPresidents.Count; i++)
		{
			if (PastPresidents[i].ActorId == actorId)
			{
				return PastPresidents[i];
			}
		}
		return null;
	}

	public void RecordPresidentTerm(int actorId, string name, int termsServed)
	{
		PastPresidentRecord past = FindPastPresident(actorId);
		if (past != null)
		{
			past.TermsCount = System.Math.Max(past.TermsCount, termsServed);
			past.Name = name;
		}
		else
		{
			PastPresidents.Add(new PastPresidentRecord
			{
				ActorId = actorId,
				Name = name,
				TermsCount = termsServed
			});
		}
	}
}
