using System;
using System.Collections.Generic;

namespace RealPolitic.Core;

/// <summary>Почему держава попала в обвиняемые: вид проступка и его вес.</summary>
public sealed class KmudMisdeed
{
	public int KingdomId { get; set; }

	/// <summary>Сколько войн развязала держава.</summary>
	public int WarsStarted { get; set; }

	/// <summary>Голодает ли её народ прямо сейчас.</summary>
	public bool Starving { get; set; }

	/// <summary>0.50: отнимала ли держава гражданство у чужих по националистическому закону.</summary>
	public bool TookCitizenship { get; set; }

	/// <summary>Итоговый вес проступков (больше — хуже).</summary>
	public int Score => WarsStarted + (Starving ? StarvingWeight : 0) + (TookCitizenship ? CitizenshipWeight : 0);

	/// <summary>Отзыв гражданства весит как три развязанные войны: это судьба живых людей.</summary>
	public const int CitizenshipWeight = 3;

	/// <summary>Голод весит как две развязанные войны: морить собственный народ — тяжело.</summary>
	public const int StarvingWeight = 2;

	/// <summary>Порог, ниже которого державу не обвиняют.</summary>
	public const int Threshold = 2;

	/// <summary>Причины словами — для летописи.</summary>
	public List<string> Reasons()
	{
		List<string> reasons = new List<string>();
		if (WarsStarted > 0)
		{
			reasons.Add("развязанных войн: " + WarsStarted);
		}
		if (Starving)
		{
			reasons.Add("голод в собственной державе");
		}
		if (TookCitizenship)
		{
			reasons.Add("отняли гражданство у чужих");
		}
		return reasons;
	}
}

/// <summary>
/// Моральный счёт держав для вопроса 3 конференции КМУД (0.35).
/// Считаем только то, что видно в игре и понятно игроку: развязанные войны и голод
/// собственного народа (решение владельца от 28.10.2026: «добавь голод»).
/// </summary>
public static class KmudMorality
{
	/// <summary>
	/// Выбирает самую безнравственную державу. Возвращает 0, если никто не перешёл порог.
	/// При равенстве веса обвиняемым становится тот, кто развязал больше войн; если и тут
	/// равенство — меньший id (порядок постоянный, без случайности).
	/// </summary>
	public static int Worst(IReadOnlyList<KmudMisdeed> all)
	{
		if (all == null)
		{
			return 0;
		}
		KmudMisdeed best = null;
		for (int i = 0; i < all.Count; i++)
		{
			KmudMisdeed current = all[i];
			if (current == null || current.Score < KmudMisdeed.Threshold)
			{
				continue;
			}
			if (best == null
				|| current.Score > best.Score
				|| (current.Score == best.Score && current.WarsStarted > best.WarsStarted)
				|| (current.Score == best.Score && current.WarsStarted == best.WarsStarted && current.KingdomId < best.KingdomId))
			{
				best = current;
			}
		}
		return best == null ? 0 : best.KingdomId;
	}

	/// <summary>Запись державы для подсчёта.</summary>
	public static KmudMisdeed Record(int kingdomId, int warsStarted, bool starving)
	{
		return Record(kingdomId, warsStarted, starving, false);
	}

	/// <summary>0.50: то же, но с националистическим проступком.</summary>
	public static KmudMisdeed Record(int kingdomId, int warsStarted, bool starving, bool tookCitizenship)
	{
		return new KmudMisdeed
		{
			KingdomId = kingdomId,
			WarsStarted = Math.Max(0, warsStarted),
			Starving = starving,
			TookCitizenship = tookCitizenship
		};
	}
}
