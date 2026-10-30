using System;
using System.Collections.Generic;

namespace RealPolitic.Core;

public sealed class NotificationCenter
{
	private sealed class CollapsedGroup
	{
		public int Year;

		public int Count;

		public PolEvent? Sample;
	}

	private readonly PolEventBus _bus;

	private readonly Queue<PolEvent> _pending = new Queue<PolEvent>();

	private readonly HashSet<PolEventType> _muted = new HashSet<PolEventType>();

	private readonly Dictionary<PolEventType, CollapsedGroup> _groups = new Dictionary<PolEventType, CollapsedGroup>();

	public Severity MinSeverity { get; set; } = Severity.Minor;

	public int MaxPending { get; set; } = 60;

	public bool CollapseSameType { get; set; } = true;

	public int PendingCount => _pending.Count;

	public event Action<PolEvent>? OnNotification;

	public NotificationCenter(PolEventBus bus)
	{
		_bus = bus ?? throw new ArgumentNullException("bus");
		_bus.Subscribe(Handle);
	}

	public void Mute(PolEventType type)
	{
		_muted.Add(type);
	}

	public void Unmute(PolEventType type)
	{
		_muted.Remove(type);
	}

	public bool IsMuted(PolEventType type)
	{
		return _muted.Contains(type);
	}

	public void ClearPending()
	{
		_pending.Clear();
	}

	public PolEvent? Dequeue()
	{
		if (_pending.Count != 0)
		{
			return _pending.Dequeue();
		}
		return null;
	}

	/// <summary>Впереди критическое событие (смерть правителя) — показывать без ожидания интервала.</summary>
	public bool PeekIsCritical()
	{
		return _pending.Count != 0 && _pending.Peek().Severity == Severity.Critical;
	}

	private void Handle(PolEvent evt)
	{
		if (evt == null || _muted.Contains(evt.Type) || EffectiveSeverity(evt) < MinSeverity)
		{
			return;
		}
		if (CollapseSameType && IsGroupable(evt.Type))
		{
			if (!_groups.TryGetValue(evt.Type, out var value))
			{
				value = new CollapsedGroup();
				_groups[evt.Type] = value;
			}
			if (value.Year != evt.Year)
			{
				value.Year = evt.Year;
				value.Count = 0;
			}
			value.Count++;
			value.Sample = evt;
			if (value.Count == 1)
			{
				Enqueue(evt);
			}
			else if (value.Count % 5 == 0)
			{
				PolEvent polEvent = new PolEvent(evt.Type, evt.LocKey + "_SUMMARY")
				{
					Year = evt.Year,
					Severity = Severity.Minor,
					KingdomId = evt.KingdomId
				};
				polEvent.Params["count"] = value.Count.ToString();
				Enqueue(polEvent);
			}
		}
		else
		{
			Enqueue(evt);
		}
	}

	/// <summary>
	/// Эффективная важность события для фильтрации уведомлений (NotifyLevel 0..3):
	/// 0 (Trivial) — всё подряд (включая спады экономики, торговлю, пограничные выдворения, промежуточные шаги процедур);
	/// 1 (Minor) — внутренние кризисы отдельных стран, текущая дипломатия, выборы, референдумы, поправки, рождение культур/языков;
	/// 2 (Major) — только крупные геополитические события мира (войны, революции, расколы, образование/распад союзов и уний, падение держав);
	/// 3 (Critical) — только чрезвычайные переломы (смерть правителя, гражданская война, победа революции, крах империи).
	/// </summary>
	public static Severity EffectiveSeverity(PolEvent evt)
	{
		if (evt == null)
		{
			return Severity.Trivial;
		}
		Severity cap = NotificationTierCap(evt.Type);
		return evt.Severity < cap ? evt.Severity : cap;
	}

	private static Severity NotificationTierCap(PolEventType type)
	{
		switch (type)
		{
			// Уровень 0 (Trivial): рутина, торговые пути, пограничные стычки, отложенные выборы, промежуточные позиции
			case PolEventType.ActorConverted:
			case PolEventType.CityIdeologyShift:
			case PolEventType.EconomicCrisisEnded:
			case PolEventType.EconomicBoom:
			case PolEventType.TradeRouteOpened:
			case PolEventType.TradeRouteUpgraded:
			case PolEventType.TradeRouteBroken:
			case PolEventType.TradeRoutesSevered:
			case PolEventType.BorderExpulsion:
			case PolEventType.BorderOpened:
			case PolEventType.ElectionDeferred:
			case PolEventType.CivicElectionDeferred:
			case PolEventType.NationalRecognitionPosition:
			case PolEventType.NationalAlliancePosition:
			case PolEventType.TruceExpired:
			case PolEventType.TreatyExpired:
				return Severity.Trivial;

			// Уровень 1 (Minor): внутренние кризисы отдельных стран, текущая дипломатия, выборы, престолонаследие, конституционные правки
			case PolEventType.EconomicCrisisStarted:
			case PolEventType.IdeologyThreatBroke:
			case PolEventType.IdeologyThreatRecovered:
			case PolEventType.LegitimacyCrisis:
			case PolEventType.IdeologyPowerUsed:
			case PolEventType.IdeologyPowerEnded:
			case PolEventType.WarReferendum:
			case PolEventType.ConstitutionEdited:
			case PolEventType.UnconstitutionalAct:
			case PolEventType.TreatySigned:
			case PolEventType.TreatyBroken:
			case PolEventType.TradeEmbargo:
			case PolEventType.CasusBelliDeclared:
			case PolEventType.EspionageExposed:
			case PolEventType.EspionageSuccess:
			case PolEventType.BorderClosed:
			case PolEventType.PowerRaid:
			case PolEventType.Succession:
			case PolEventType.DemocraticElection:
			case PolEventType.RegencyStarted:
			case PolEventType.RegencyEnded:
			case PolEventType.RegencyCrowned:
			case PolEventType.RegencyPassed:
			case PolEventType.DynasticMarriage:
			case PolEventType.DynastyRenamed:
			case PolEventType.EthnonationalistTension:
			case PolEventType.AllianceMemberJoined:
			case PolEventType.AllianceMemberLeft:
			case PolEventType.UnionMemberJoined:
			case PolEventType.UnionMemberLeft:
			case PolEventType.UnionInstitutionGained:
			case PolEventType.IntegrationStarted:
			case PolEventType.RegionalCivicProcedure:
			case PolEventType.CivicAllianceMotion:
			case PolEventType.NewReligion:
			case PolEventType.NewLanguage:
			case PolEventType.NewCulture:
			case PolEventType.DiplomaticAction:
			case PolEventType.StrategicCampaignStarted:
			case PolEventType.StrategicCampaignCompleted:
			case PolEventType.DiplomaticMemoryRecorded:
			case PolEventType.CivicPetitionSubmitted:
			case PolEventType.ParliamentaryShift:
			case PolEventType.CoalitionSpoilsDispute:
			case PolEventType.RecognitionLobbyMission:
			case PolEventType.MetropolisRecognitionProtest:
				return Severity.Minor;

			// Уровень 3 (Critical): чрезвычайные переломные события
			case PolEventType.RulerDeath:
			case PolEventType.RevolutionSucceeded:
			case PolEventType.CivilWarStarted:
			case PolEventType.CivilWarEnded:
			case PolEventType.KingdomCollapsed:
			case PolEventType.Impeached:
			case PolEventType.ConstitutionalUsurpation:
				return Severity.Critical;

			// Уровень 2 (Major): крупные геополитические события мира (войны, расколы, союзы, унии, мирные договоры, падение держав)
			default:
				return Severity.Major;
		}
	}

	private static bool IsGroupable(PolEventType type)
	{
		switch (type)
		{
			case PolEventType.ActorConverted:
			case PolEventType.DiplomaticAction:
			case PolEventType.NewReligion:
			case PolEventType.NewLanguage:
			case PolEventType.NewCulture:
			case PolEventType.EconomicCrisisStarted:
			case PolEventType.IdeologyThreatBroke:
			case PolEventType.IdeologyThreatRecovered:
			case PolEventType.IdeologyPowerUsed:
			case PolEventType.TreatySigned:
			case PolEventType.TreatyBroken:
			case PolEventType.WarReferendum:
			case PolEventType.ConstitutionEdited:
			case PolEventType.EspionageExposed:
			case PolEventType.EspionageSuccess:
			case PolEventType.CivicAllianceMotion:
			case PolEventType.RegionalCivicProcedure:
			case PolEventType.SubspeciesExtinct:
			case PolEventType.BorderExpulsion:
				return true;
			default:
				return false;
		}
	}

	private void Enqueue(PolEvent evt)
	{
		_pending.Enqueue(evt);
		while (_pending.Count > MaxPending && MaxPending > 0)
		{
			_pending.Dequeue();
		}
		this.OnNotification?.Invoke(evt);
	}
}
