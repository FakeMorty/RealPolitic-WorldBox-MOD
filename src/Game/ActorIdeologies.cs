using System;
using System.Collections.Generic;
using NeoModLoader.services;
using RealPolitic.Core;

namespace RealPolitic;

/// <summary>
/// Идеология КАЖДОГО жителя как личная черта.
///
/// Правила (требование пользователя):
///  • у любого жителя есть идеология;
///  • с вероятностью 70 % житель королевства исповедует идеологию своего короля,
///    остальные 30 % — другая идеология (взвешенно по близости к государственной);
///  • идеология реально влияет: несогласные хуже подчиняются (ниже лояльность города,
///    хуже настроение, меньше вклад в управление), согласные — послушнее и довольнее.
///
/// Запись идеологии привязана к id юнита и переигрывается, если житель сменил государство
/// (при переходе он не мгновенно меняет взгляды: сохраняет свои, но пересчитывается послушание).
/// </summary>
public static class ActorIdeologies
{
    public sealed class Faith : CivicBelief { }

	private static readonly Dictionary<int, Faith> _faiths = new Dictionary<int, Faith>();
	private static readonly HashSet<string> _invalidBeliefWarnings = new HashSet<string>();

	private static readonly Random _rng = new Random(20260913);

	/// <summary>Сколько жителей получило идеологию (для лога и летописи).</summary>
	public static int Assigned;

	public static int Count => _faiths.Count;

	public static void Clear()
	{
		_faiths.Clear();
		_invalidBeliefWarnings.Clear();
		Assigned = 0;
	}

	/// <summary>Идеология жителя; раскатывается при первом обращении. null — юнит непригоден (не разумный/без id).</summary>
	public static Faith Get(Actor actor)
	{
		if (actor == null)
		{
			return null;
		}
        if (RealPoliticMod.World?.Governance.Enabled == true)
        {
            long realId=GameText.ActorLongId(actor);if(realId<=0 || realId>int.MaxValue)return null;
        }
		int id = GameText.ActorId(actor);
		if (id < 0)
		{
			return null;
		}
		int kingdomId = KingdomIdOf(actor);
		if (_faiths.TryGetValue(id, out Faith existing))
		{
			// Житель сменил государство: взгляды остаются прежними, но фиксируем новое подданство.
			if (existing.KingdomId != kingdomId)
			{
				existing.KingdomId = kingdomId;
			}
			NormalizeForGovernance(existing, id);
			if (RealPoliticMod.World?.Governance.Enabled == true && id > 0) RealPoliticMod.World.Governance.RememberBelief(id,existing);
			return existing;
		}
        GovernanceState governance = RealPoliticMod.World?.Governance;
		if (governance != null && governance.Enabled && governance.Beliefs.TryGetValue(id, out var saved))
		{
			Faith restored = new Faith { Ideology=saved.Ideology, Conviction=saved.Conviction, Dissent=saved.Dissent,
				KingdomId=kingdomId, LastStateIdeology=saved.LastStateIdeology, LastDissentYear=saved.LastDissentYear };
			NormalizeForGovernance(restored, id);
			_faiths[id]=restored; governance.RememberBelief(id,restored); return restored;
        }
		IdeologyType state = StateIdeologyOf(kingdomId);
		Faith faith = new Faith
		{
			// Инакомыслие жителей тоже ограничено эпохой: юный мир не знает коммунизма.
			Ideology = ActorIdeologyRoll.Roll(state, _rng, EraRules.OfYear(Math.Max(0, RealPoliticMod.CurrentYear()), RealPoliticMod.World?.Params)),
			KingdomId = kingdomId,
			LastStateIdeology = state,
			LastDissentYear = Math.Max(0, RealPoliticMod.CurrentYear())
		};
		faith.Conviction = ActorIdeologyRoll.RollConviction(faith.Ideology, state, _rng);
		_faiths[id] = faith;
		Assigned++;
		NormalizeForGovernance(faith, id);
		if (governance != null && governance.Enabled && id > 0) governance.RememberBelief(id,faith);
		return faith;
	}

	/// <summary>Идеология жителя без раскатки (null, если ещё не назначена).</summary>
	public static Faith Peek(int actorId)
	{
		return _faiths.TryGetValue(actorId, out Faith f) ? f : null;
	}

	public static void Forget(int actorId)
	{
		_faiths.Remove(actorId);
	}

	/// <summary>Идеология государства (или Monarchy, если государство неизвестно ядру).</summary>
	public static IdeologyType StateIdeologyOf(int kingdomId)
	{
		KingdomPolitics state = (kingdomId < 0) ? null : RealPoliticMod.World?.GetKingdom(kingdomId);
		return (state == null) ? IdeologyType.Monarchy : state.Ideology;
	}

	public static int KingdomIdOf(Actor actor)
	{
		try
		{
			Kingdom kingdom = ((BaseSimObject)actor)?.kingdom;
			if (kingdom == null)
			{
				return -1;
			}
			// 0.53.1: прямой доступ вместо отражения.
			try
			{
				long fast = ((NanoObject)kingdom).id;
				if (fast > 0L && fast <= int.MaxValue)
				{
					return (int)fast;
				}
			}
			catch (Exception)
			{
			}
			object id = GameRefs.ReadMember(kingdom, "id");
				if (id is long l)
				{
					return l < 0 || l > int.MaxValue ? -1 : (int)l;
			}
			return (id is int i) ? i : -1;
		}
		catch (Exception)
		{
			return -1;
		}
	}

	/// <summary>Послушание жителя государству [0..1] (1 — образцовый подданный).</summary>
	public static float Obedience(Actor actor)
	{
		Faith faith = Get(actor);
		if (faith == null)
		{
			return 1f;
		}
		IdeologyType state = StateIdeologyOf(KingdomIdOf(actor));
		return ActorIdeologyRoll.Obedience(faith.Ideology, state, faith.Conviction, faith.Dissent);
	}

	/// <summary>Житель не разделяет государственную идеологию.</summary>
	public static bool IsDissenter(Actor actor)
	{
		Faith faith = Get(actor);
		if (faith == null)
		{
			return false;
		}
		return faith.Ideology != StateIdeologyOf(KingdomIdOf(actor));
	}

	/// <summary>
	/// Накопление недовольства/смягчение взглядов за игровой год.
	/// Вызывается годовым тиком из <see cref="ActorPolitics"/>-слоя игры (ActorEffects).
	/// </summary>
	public static void AdvanceYear(Actor actor, float dissentRate, float dissentDecay)
	{
		Faith faith = Get(actor);
		if (faith == null)
		{
			return;
		}
		int kingdomId = KingdomIdOf(actor);
		IdeologyType state = StateIdeologyOf(kingdomId);
		faith.LastStateIdeology = state;
		KingdomPolitics kState = RealPoliticMod.World?.GetKingdom(kingdomId);
		int graceYears = RealPoliticMod.World?.Params?.YoungStateGraceYears ?? 50;
		if (faith.Ideology == state || (kState != null && kState.IsYoungState(RealPoliticMod.CurrentYear(), graceYears)))
		{
			faith.Dissent = Clamp01(faith.Dissent - dissentDecay);
			return;
		}
		float tolerance = Ideologies.Get(state).DissentTolerance;
		float growth = dissentRate * (1.4f - tolerance) * (1.5f - faith.Conviction);
		faith.Dissent = Clamp01(faith.Dissent + Math.Max(0.01f, growth));
	}

	/// <summary>Житель сменил взгляды (давление государства/города победило).</summary>
	public static void Convert(int actorId, IdeologyType to)
	{
		if (_faiths.TryGetValue(actorId, out Faith faith))
		{
			faith.Ideology = to;
			faith.Dissent *= 0.4f;
			faith.Conviction = Clamp01(0.35f + (float)_rng.NextDouble() * 0.4f);
		}
	}

	/// <summary>Сводка по государству: сколько согласных/несогласных и средняя лояльность (для летописи и окна).</summary>
	public static void Summary(int kingdomId, out int loyal, out int dissenters, out float avgObedience)
	{
		loyal = 0;
		dissenters = 0;
		float sum = 0f;
		int total = 0;
		IdeologyType state = StateIdeologyOf(kingdomId);
		foreach (KeyValuePair<int, Faith> pair in _faiths)
		{
			Faith f = pair.Value;
			if (f.KingdomId != kingdomId)
			{
				continue;
			}
			total++;
			if (f.Ideology == state)
			{
				loyal++;
			}
			else
			{
				dissenters++;
			}
			sum += ActorIdeologyRoll.Obedience(f.Ideology, state, f.Conviction, f.Dissent);
		}
		avgObedience = (total == 0) ? 1f : (sum / total);
	}

	public static void LogSummaryOnce()
	{
		try
		{
			LogService.LogInfo("[Real Politic] идеологии жителей: назначено " + Assigned + ", в памяти " + _faiths.Count);
		}
		catch (Exception)
		{
		}
	}

	private static float Clamp01(float v)
	{
		if (float.IsNaN(v) || float.IsInfinity(v))
		{
			return 0.5f;
		}
		if (v < 0f)
		{
			return 0f;
		}
		return (v > 1f) ? 1f : v;
	}

	private static void NormalizeForGovernance(Faith faith, int actorId)
	{
		if (faith == null)
		{
			return;
		}
		string issue = null;
		if (!Enum.IsDefined(typeof(IdeologyType), faith.Ideology))
		{
			issue = "ideology";
			faith.Ideology = IdeologyType.Monarchy;
		}
		if (!Enum.IsDefined(typeof(IdeologyType), faith.LastStateIdeology))
		{
			issue = issue ?? "lastStateIdeology";
			faith.LastStateIdeology = faith.Ideology;
		}
		if (float.IsNaN(faith.Conviction) || float.IsInfinity(faith.Conviction) || faith.Conviction < 0f || faith.Conviction > 1f)
		{
			issue = issue ?? "conviction";
			faith.Conviction = Clamp01(faith.Conviction);
		}
		if (float.IsNaN(faith.Dissent) || float.IsInfinity(faith.Dissent) || faith.Dissent < 0f || faith.Dissent > 1f)
		{
			issue = issue ?? "dissent";
			faith.Dissent = Clamp01(faith.Dissent);
		}
		if (faith.KingdomId < -1)
		{
			issue = issue ?? "kingdomId";
			faith.KingdomId = -1;
		}
		if (faith.LastDissentYear < 0)
		{
			issue = issue ?? "lastDissentYear";
			faith.LastDissentYear = 0;
		}
		if (issue != null && _invalidBeliefWarnings.Add(issue))
		{
			LogService.LogError("[Real Politic] исправлено повреждённое убеждение жителя #" + actorId + ": поле " + issue);
		}
	}
}
