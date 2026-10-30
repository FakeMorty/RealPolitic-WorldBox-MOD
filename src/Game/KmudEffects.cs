using System;
using System.Collections.Generic;
using RealPolitic.Core;

namespace RealPolitic;

/// <summary>
/// 0.34, этап 4: исполнение решений конференции КМУД.
///   - итог самого долгого конфликта: репарации и пакт «только оборона» на 50 лет;
///   - наказание: казнь правителя, смена строя на строй большинства, санкции;
///   - помощь: деньги, еда, ассоциация 40% еды и денег на 20 лет.
/// </summary>
public static class KmudEffects
{

	/// <summary>Применяет все решения съезда. Возвращает число выполненных пунктов.</summary>
	public static int Apply(PoliticalWorld world, MapBox map, int year)
	{
		if (world?.Kmud == null || map == null || world.Kmud.Decisions == null)
		{
			return 0;
		}
		KmudDecisions decisions = world.Kmud.Decisions;
		if (decisions.Applied)
		{
			return 0;
		}
		// 0.37: подробности в лог — иначе «исполнено решений: 0» ничего не объясняет.
		NLog("[Real Politic] КМУД: исполняем решения — конфликт: " + (world.Kmud.Agenda.HasConflict ? "да" : "нет")
			+ ", наказание: " + decisions.Punishment + " (держава " + decisions.PunishedId + ")"
			+ ", помощь: " + decisions.Aid + " (держава " + decisions.AidedId + ")");
		int done = 0;
		done += ApplyConflictOutcome(world, map, year);
		done += ApplyPunishment(world, map, year);
		done += ApplyAid(world, map, year);
		done += ApplyRepatriation(world, map, year);
		decisions.Applied = true;
		return done;
	}

	/// <summary>
	/// 0.47: пятый вопрос — безопасность возвращения малого народа.
	///   «взять под охрану» — народ возвращается, а принимающую державу на 20 лет
	///     прикрывают пакты: никто из участников съезда на неё не нападёт;
	///   «выделить деньги и еду» — народ возвращается за счёт казны (перевод денег и еды);
	///   «разрешить, но без охраны» — народ просто возвращается;
	///   «запретить» — ничего не делаем.
	/// Возвращает число выполненных пунктов.
	/// </summary>
	private static int ApplyRepatriation(PoliticalWorld world, MapBox map, int year)
	{
		KmudState state = world.Kmud;
		KmudDecisions decisions = state.Decisions;
		if (!KmudVerdicts.AllowsRepatriation(decisions.Repatriation)
			|| string.IsNullOrEmpty(decisions.RepatriationRaceId)
			|| decisions.RepatriationToId <= 0)
		{
			return 0;
		}
		int to = decisions.RepatriationToId;
		int moved = MigrationRuntime.ResettleRace(world, map, decisions.RepatriationRaceId, to,
			KmudVerdicts.RepatriationPeopleToMove);
		decisions.RepatriatedCount = moved;
		int guards = 0;
		if (KmudVerdicts.GuardsRepatriation(decisions.Repatriation))
		{
			guards = GuardRealm(world, to, year);
		}
		if (KmudVerdicts.FundsRepatriation(decisions.Repatriation))
		{
			FundRealm(world, to, year);
		}
		PolEvent evt = new PolEvent(PolEventType.KmudRepatriation, "RP47_EV_KMUD_REPATRIATION") { Year = year };
		evt.Severity = Severity.Major;
		world.Bus.Publish(evt
			.With("race", GameText.Localized(decisions.RepatriationRaceId,
				GameText.Localized("race_" + decisions.RepatriationRaceId, decisions.RepatriationRaceId)))
			.With("to", world.KingdomNameOf(to))
			.With("moved", moved)
			.With("guards", guards)
			.With("decision", decisions.Repatriation));
		NLog("[Real Politic] КМУД: возвращение народа — решено " + decisions.Repatriation
			+ ", переехало человек: " + moved + ", охрана пактов: " + guards);
		return moved > 0 || guards > 0 ? 1 : 0;
	}

	/// <summary>Охрана принимающей державы: пакты «не нападать» со всеми участниками съезда.</summary>
	private static int GuardRealm(PoliticalWorld world, int kingdomId, int year)
	{
		KmudState state = world.Kmud;
		int until = year + KmudVerdicts.RepatriationGuardYears;
		int added = 0;
		if (state.MemberIds == null)
		{
			return 0;
		}
		for (int i = 0; i < state.MemberIds.Count; i++)
		{
			int other = state.MemberIds[i];
			if (other == kingdomId || other <= 0)
			{
				continue;
			}
			state.Pacts.Add(new KmudPact
			{
				A = Math.Min(kingdomId, other),
				B = Math.Max(kingdomId, other),
				UntilYear = until,
				AllianceFormed = false
			});
			world.TrucesBook.Set(kingdomId, other, until);
			added++;
		}
		return added;
	}

	/// <summary>Деньги и еда на дорогу: самая богатая держава платит принимающей.</summary>
	private static void FundRealm(PoliticalWorld world, int kingdomId, int year)
	{
		try
		{
			float best = -1f;
			int donor = 0;
			foreach (KingdomPolitics politics in world.Kingdoms)
			{
				if (politics == null || politics.KingdomId == kingdomId)
				{
					continue;
				}
				float prosperity = politics.Economy?.Prosperity ?? 50f;
				if (prosperity > best)
				{
					best = prosperity;
					donor = politics.KingdomId;
				}
			}
			if (donor <= 0)
			{
				return;
			}
			world.AddReparations(new ReparationsOrder
			{
				PayerId = donor,
				PayeeId = kingdomId,
				AmountPerYear = KmudVerdicts.RepatriationFundPerYear,
				YearsLeft = KmudVerdicts.RepatriationFundYears
			});
			world.Bus.Publish(new PolEvent(PolEventType.KmudAid, "RP32_EV_KMUD_AID") { Year = year }
				.With("donor", world.KingdomNameOf(donor))
				.With("recipient", world.KingdomNameOf(kingdomId))
				.With("kind", "repatriation"));
		}
		catch (Exception ex)
		{
			NLog("[Real Politic] КМУД: деньги на возвращение не отправлены: " + ex.Message);
		}
	}

	private static int ApplyConflictOutcome(PoliticalWorld world, MapBox map, int year)
	{
		KmudState state = world.Kmud;
		KmudAgenda agenda = state.Agenda;
		KmudDecisions decisions = state.Decisions;
		int done = 0;
		if (!agenda.HasConflict)
		{
			return 0;
		}
		if (decisions.PayerId > 0 && decisions.PayeeId > 0 && decisions.PayerId != decisions.PayeeId)
		{
			world.AddReparations(new ReparationsOrder
			{
				PayerId = decisions.PayerId,
				PayeeId = decisions.PayeeId,
				AmountPerYear = KmudVerdicts.ReparationsPerYear,
				YearsLeft = KmudVerdicts.ReparationsYears
			});
			world.Bus.Publish(new PolEvent(PolEventType.KmudReparations, "RP32_EV_KMUD_REPARATIONS") { Year = year }
				.With("payer", world.KingdomNameOf(decisions.PayerId))
				.With("payee", world.KingdomNameOf(decisions.PayeeId))
				.With("amount", KmudVerdicts.ReparationsPerYear)
				.With("years", KmudVerdicts.ReparationsYears));
			done++;
		}
		int pactYears = Math.Max(1, world.Params == null ? KmudVerdicts.DefaultPactYears : world.Params.KmudPactYears);
		KmudPact pact = KmudVerdicts.CreatePact(agenda, year, pactYears);
		if (pact != null)
		{
			state.Pacts.Add(pact);
			world.TrucesBook.Set(pact.A, pact.B, pact.UntilYear);
			bool allied = FormPactAlliance(map, pact);
			pact.AllianceFormed = allied;
			world.Bus.Publish(new PolEvent(PolEventType.KmudPact, "RP32_EV_KMUD_PACT") { Year = year }
				.With("a", world.KingdomNameOf(pact.A))
				.With("b", world.KingdomNameOf(pact.B))
				.With("until", pact.UntilYear)
				.With("allied", allied ? 1 : 0)
				.With("years", pactYears));
			done++;
		}
		return done;
	}

	private static bool FormPactAlliance(MapBox map, KmudPact pact)
	{
		try
		{
			Kingdom a = FindKingdom(map, pact.A);
			Kingdom b = FindKingdom(map, pact.B);
			if (a == null || b == null)
			{
				return false;
			}
			// 0.47.1: две последние державы не сводятся в союз — только перемирие.
			if (NativeAllianceGuards.CountRealmsWithLand() <= 2)
			{
				NLog("[Real Politic] КМУД: пакт «только оборона» без альянса — в мире осталось две державы");
				return false;
			}
			return DiplomacyExecutor.ForceAllianceDirect(a, b);
		}
		catch (Exception ex)
		{
			NLog("[Real Politic] КМУД: пакт не удалось оформить альянсом: " + ex.Message);
			return false;
		}
	}

	private static int ApplyPunishment(PoliticalWorld world, MapBox map, int year)
	{
		KmudDecisions decisions = world.Kmud.Decisions;
		int punished = decisions.PunishedId;
		if (punished <= 0)
		{
			return 0;
		}
		KingdomPolitics politics = world.GetKingdom(punished);
		if (politics == null)
		{
			return 0;
		}
		int punishment = decisions.Punishment;
		int done = 0;
		if (KmudVerdicts.ExecutesRuler(punishment))
		{
			Kingdom kingdom = FindKingdom(map, punished);
			Actor ruler = kingdom == null ? null : (ChronicleEdition.LivingRuler(kingdom) ?? GameRefs.ReadRuler(kingdom));
			string rulerName = ruler == null ? string.Empty : ((NanoObject)ruler).name;
			bool killed = ruler != null && GameRefs.TryExecuteActor(ruler);
			world.Bus.Publish(new PolEvent(PolEventType.KmudExecution, "RP32_EV_KMUD_EXECUTION") { Year = year }
				.With("kingdom", world.KingdomNameOf(punished))
				.With("ruler", string.IsNullOrEmpty(rulerName) ? "правитель" : rulerName)
				.With("ok", killed ? 1 : 0));
			done++;
		}
		if (KmudVerdicts.ChangesIdeology(punishment))
		{
			List<IdeologyType> ideologies = new List<IdeologyType>();
			foreach (KingdomPolitics other in world.Kingdoms)
			{
				if (other != null && other.KingdomId != punished)
				{
					ideologies.Add(other.Ideology);
				}
			}
			IdeologyType target = KmudVerdicts.MajorityIdeology(ideologies);
			IdeologyType before = politics.Ideology;
			if (target != before)
			{
				politics.Ideology = target;
				world.Bus.Publish(new PolEvent(PolEventType.KmudIdeologyChange, "RP32_EV_KMUD_REFORM") { Year = year }
					.With("kingdom", world.KingdomNameOf(punished))
					.With("old", Ideologies.NameKey(before))
					.With("new", Ideologies.NameKey(target)));
				done++;
			}
		}
		if (KmudVerdicts.AppliesSanctions(punishment))
		{
			int supporters = 0;
			foreach (KingdomPolitics other in world.Kingdoms)
			{
				if (other != null && other.KingdomId != punished)
				{
					supporters++;
				}
			}
			world.Kmud.Sanctions.Add(new KmudSanction
			{
				KingdomId = punished,
				UntilYear = year + KmudVerdicts.SanctionYears,
				Supporters = supporters
			});
			world.Bus.Publish(new PolEvent(PolEventType.KmudSanctions, "RP32_EV_KMUD_SANCTIONS") { Year = year }
				.With("kingdom", world.KingdomNameOf(punished))
				.With("supporters", supporters)
				.With("until", year + KmudVerdicts.SanctionYears));
			done++;
		}
		// 0.39 (решение владельца от 29.10.2026): если державу наказали — обвинения
		// снимаются. Иначе одна и та же держава обвиняется на каждом съезде за старые
		// войны и никогда не выходит из обвиняемых.
		if (KmudVerdicts.PardonsAccusations(punishment))
		{
			int pardoned = world.PardonWarDeclarations(punished);
			NLog("[Real Politic] КМУД: с державы «" + world.KingdomNameOf(punished)
				+ "» снято обвинений: " + pardoned);
			world.Bus.Publish(new PolEvent(PolEventType.KmudPardoned, "RP32_EV_KMUD_PARDONED") { Year = year }
				.With("kingdom", world.KingdomNameOf(punished))
				.With("count", pardoned));
		}
		return done;
	}

	private static int ApplyAid(PoliticalWorld world, MapBox map, int year)
	{
		KmudDecisions decisions = world.Kmud.Decisions;
		int poor = decisions.AidedId;
		if (poor <= 0)
		{
			return 0;
		}
		Kingdom kingdom = FindKingdom(map, poor);
		if (kingdom == null)
		{
			return 0;
		}
		switch (decisions.Aid)
		{
			case KmudOptions.AidMoney:
			{
				bool ok = IdeologyMechanicsEffects.GrantResource(kingdom, "gold", 120);
				world.Bus.Publish(new PolEvent(PolEventType.KmudAid, "RP32_EV_KMUD_AID_MONEY") { Year = year }
					.With("kingdom", world.KingdomNameOf(poor))
					.With("ok", ok ? 1 : 0));
				return 1;
			}
			case KmudOptions.AidFood:
			{
				bool ok = IdeologyMechanicsEffects.GrantResource(kingdom, "bread", 60);
				world.Bus.Publish(new PolEvent(PolEventType.KmudAid, "RP32_EV_KMUD_AID_FOOD") { Year = year }
					.With("kingdom", world.KingdomNameOf(poor))
					.With("ok", ok ? 1 : 0));
				return 1;
			}
			case KmudOptions.AidAssociation:
			{
				int donor = FindRichest(world, poor);
				if (donor <= 0)
				{
					world.Bus.Publish(new PolEvent(PolEventType.KmudAid, "RP32_EV_KMUD_AID_FAILED") { Year = year }
						.With("kingdom", world.KingdomNameOf(poor)));
					return 0;
				}
				KingdomPolitics donorState = world.GetKingdom(donor);
				float income = donorState == null || donorState.Economy == null ? 0f : donorState.Economy.LastIncome;
				float goldPerYear = Math.Max(1f, income * KmudVerdicts.AssociationShare);
				world.Kmud.Associations.Add(new KmudAssociation
				{
					DonorId = donor,
					RecipientId = poor,
					UntilYear = year + KmudVerdicts.AssociationYears,
					GoldPerYear = goldPerYear,
					FoodPerYear = 20
				});
				world.Bus.Publish(new PolEvent(PolEventType.KmudAid, "RP32_EV_KMUD_AID_ASSOCIATION") { Year = year }
					.With("donor", world.KingdomNameOf(donor))
					.With("kingdom", world.KingdomNameOf(poor))
					.With("until", year + KmudVerdicts.AssociationYears)
					.With("gold", goldPerYear));
				return 1;
			}
			default:
				return 0;
		}
	}

	private static int FindRichest(PoliticalWorld world, int exceptId)
	{
		int best = 0;
		float bestIncome = float.MinValue;
		foreach (KingdomPolitics politics in world.Kingdoms)
		{
			if (politics == null || politics.KingdomId == exceptId)
			{
				continue;
			}
			float income = politics.Economy == null ? 0f : politics.Economy.LastIncome;
			if (income > bestIncome)
			{
				bestIncome = income;
				best = politics.KingdomId;
			}
		}
		return best;
	}

	/// <summary>Каждый год: санкции давят благосостояние, ассоциация перекачивает средства.</summary>
	public static void AnnualEffects(PoliticalWorld world, MapBox map, int year)
	{
		if (world?.Kmud == null || map == null)
		{
			return;
		}
		try
		{
			for (int i = world.Kmud.Sanctions.Count - 1; i >= 0; i--)
			{
				KmudSanction sanction = world.Kmud.Sanctions[i];
				if (!sanction.Active(year))
				{
					world.Kmud.Sanctions.RemoveAt(i);
					world.Bus.Publish(new PolEvent(PolEventType.KmudSanctions, "RP32_EV_KMUD_SANCTIONS_ENDED") { Year = year }
						.With("kingdom", world.KingdomNameOf(sanction.KingdomId)));
					continue;
				}
				KingdomPolitics politics = world.GetKingdom(sanction.KingdomId);
				if (politics != null && politics.Economy != null)
				{
					politics.Economy.Prosperity = Math.Max(0f, politics.Economy.Prosperity - 3f);
				}
			}
			for (int i = world.Kmud.Associations.Count - 1; i >= 0; i--)
			{
				KmudAssociation association = world.Kmud.Associations[i];
				if (!association.Active(year))
				{
					world.Kmud.Associations.RemoveAt(i);
					world.Bus.Publish(new PolEvent(PolEventType.KmudAid, "RP32_EV_KMUD_ASSOCIATION_ENDED") { Year = year }
						.With("donor", world.KingdomNameOf(association.DonorId))
						.With("kingdom", world.KingdomNameOf(association.RecipientId)));
					continue;
				}
				Kingdom recipient = FindKingdom(map, association.RecipientId);
				if (recipient == null)
				{
					continue;
				}
				IdeologyMechanicsEffects.GrantResource(recipient, "gold", (int)Math.Round(association.GoldPerYear));
				IdeologyMechanicsEffects.GrantResource(recipient, "bread", association.FoodPerYear);
			}
		}
		catch (Exception ex)
		{
			NLog("[Real Politic] КМУД: ошибка годовых последствий: " + ex.Message);
		}
	}

	private static Kingdom FindKingdom(MapBox map, int id)
	{
		List<Kingdom> list = GameRefs.KingdomList(map);
		if (list == null)
		{
			return null;
		}
		for (int i = 0; i < list.Count; i++)
		{
			Kingdom kingdom = list[i];
			if (kingdom != null && (int)((NanoObject)kingdom).id == id)
			{
				return kingdom;
			}
		}
		return null;
	}

	private static void NLog(string message)
	{
		try
		{
			NeoModLoader.services.LogService.LogInfo(message);
		}
		catch (Exception)
		{
		}
	}
}
