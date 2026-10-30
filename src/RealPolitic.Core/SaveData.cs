using System;
using System.Collections.Generic;

namespace RealPolitic.Core;

public static class SaveData
{
	/// <summary>v18: persisted governance, individual ballots and electoral chapters; v16/v17 remain legacy.</summary>
	public const int FormatVersion = 18;

	private static List<int> IntList(TinyJson.Node? node)
	{
		List<int> list = new List<int>();
		if (node?.Arr != null)
		{
			for (int i = 0; i < node.Arr.Count; i++)
			{
				list.Add((int)node.Arr[i].Num);
			}
		}
		return list;
	}

    // Stage native alliance identities before world.Clear(). Missing IDs belong to the legacy
    // name-only book and remain unknown (0), never invented sequential IDs.
    private static List<AllianceRecord>? ReadAllianceSnapshot(TinyJson.Node? section)
    {
        if(section==null)return null;
        if(section.Kind!=TinyJson.TokenKind.Array)throw new FormatException("Invalid alliance collection.");
        var records=new List<AllianceRecord>();var ids=new HashSet<int>();
        foreach(var entry in section.Arr)
        {
            if(entry.Kind!=TinyJson.TokenKind.Object)throw new FormatException("Invalid alliance record.");
            int id=0;var saved=entry.Get("id");
            if(saved!=null)
            {
                if(saved.Kind!=TinyJson.TokenKind.Number || double.IsNaN(saved.Num) || double.IsInfinity(saved.Num)
                    || saved.Num<1 || saved.Num>int.MaxValue || Math.Truncate(saved.Num)!=saved.Num)
                    throw new FormatException("Invalid native alliance identity.");
                id=(int)saved.Num;if(!ids.Add(id))throw new FormatException("Repeated native alliance identity.");
            }
            var record=new AllianceRecord{Id=id,Name=entry.GetString("n"),LeaderId=entry.GetInt("l")};
            var members=entry.Get("m");
            if(members!=null)
            {
                if(members.Kind!=TinyJson.TokenKind.Array)throw new FormatException("Invalid alliance members.");
                foreach(var member in members.Arr)record.Members.Add((int)member.Num);
            }
            records.Add(record);
        }
        return records;
    }

	public static string Write(PoliticalWorld world)
	{
		TinyJson.Writer writer = new TinyJson.Writer();
		writer.BeginObject();
			writer.Key("format").Value(FormatVersion);
		writer.Key("year").Value(world.Year);
		writer.Key("baseline").Value(world.BaselineRecorded);
        writer.Key("governance26"); GovernanceCodec.Write(writer, world.Governance);
		writer.Key("kingdoms").BeginArray();
		foreach (KingdomPolitics kingdom in world.Kingdoms)
		{
			writer.BeginObject();
			writer.Key("id").Value(kingdom.KingdomId);
			writer.Key("i").Value((int)kingdom.Ideology);
			writer.Key("s").Value((int)kingdom.Stance);
			writer.Key("l").Value(kingdom.Legitimacy);
				writer.Key("f").Value(kingdom.FoundedYear);
				writer.Key("support").Value(kingdom.Support);
				writer.Key("dissent").Value(kingdom.AvgDissent);
			writer.Key("war").Value(kingdom.InWar);
			writer.Key("starving").Value(kingdom.Starving);
			// Этап 22 (v11): усталость от войны; ноль не пишем — миры без войн байт-совместимы с v10.
			if (kingdom.WarFatigue > 0f)
			{
				writer.Key("wf").Value(kingdom.WarFatigue);
			}
			// 0.39: память о поражениях и о нарушенных обещаниях. Нули не пишем.
			if (kingdom.Defeats > 0 || kingdom.WarAversionUntilYear != 0)
			{
				writer.Key("dfc").Value(kingdom.Defeats);
				writer.Key("ldy").Value(kingdom.LastDefeatYear);
				writer.Key("wav").Value(kingdom.WarAversionUntilYear);
				writer.Key("lvy").Value(kingdom.LastVictoryYear);
			}
			if (kingdom.PromiseScore > 0f)
			{
				writer.Key("prm").Value(kingdom.PromiseScore);
				writer.Key("prmy").Value(kingdom.LastPromiseBrokenYear);
			}
				writer.Key("mech").BeginObject();
				writer.Key("last").Value(kingdom.Mechanics.LastUsedYear);
				writer.Key("active").Value(kingdom.Mechanics.ActiveUntilYear);
				writer.Key("target").Value(kingdom.Mechanics.TargetKingdomId ?? (-1));
				writer.Key("threat").Value(kingdom.Mechanics.ThreatLevel);
				writer.Key("breaks").Value(kingdom.Mechanics.ThreatBreaks);
				writer.Key("breakYear").Value(kingdom.Mechanics.LastBreakYear);
				writer.EndObject();
			writer.Key("ruler").Value((kingdom.Ruler == null) ? (-1) : kingdom.Ruler.ActorId);
			writer.Key("rulerName").Value((kingdom.Ruler == null) ? string.Empty : kingdom.Ruler.Name);
			writer.Key("rulerSince").Value((kingdom.Ruler != null) ? kingdom.Ruler.SinceYear : 0);
			writer.Key("rulerFemale").Value((kingdom.Ruler != null) && kingdom.Ruler.IsFemale);
			writer.Key("rulerAge").Value((kingdom.Ruler != null) ? kingdom.Ruler.Age : 0);
			writer.Key("rulerClan").Value((kingdom.Ruler != null) ? kingdom.Ruler.ClanName : string.Empty);
			writer.Key("rulerClanId").Value((kingdom.Ruler != null) ? kingdom.Ruler.ClanId : 0L);
			writer.Key("clan").Value(kingdom.RoyalClanName ?? string.Empty);
			writer.Key("clanId").Value(kingdom.RoyalClanId);
			writer.Key("prevClanId").Value(kingdom.PreviousRoyalClanId);
			if (kingdom.RoyalClanHistory.Count > 0)
			{
				writer.Key("royalClans").BeginArray();
				for (int h = 0; h < kingdom.RoyalClanHistory.Count; h++)
				{
					writer.Value(kingdom.RoyalClanHistory[h]);
				}
				writer.EndArray();
			}
			writer.Key("dynFounder").Value(kingdom.DynastyFounder ?? string.Empty);
			writer.Key("dynReigns").Value(kingdom.DynastyReignsCount);
			writer.Key("dynStart").Value(kingdom.DynastyStartYear);
			// 0.24, статья XIV: перерыв между вотумами импичмента переживает сохранение.
			writer.Key("impeachYear").Value(kingdom.LastImpeachmentYear);
		writer.Key("law").Value((int)kingdom.SuccessionLaw);
		// Этап 19 (v8): личность правителя и узы династического брака.
		if (kingdom.Personality != null)
		{
			writer.Key("pers").BeginObject();
			writer.Key("t").Value((int)kingdom.Personality.Trait);
			writer.Key("a").Value(kingdom.Personality.Ambition);
			writer.EndObject();
		}
		if (kingdom.Marriage != null)
		{
			writer.Key("mrg").BeginObject();
			writer.Key("p").Value(kingdom.Marriage.PartnerId);
			writer.Key("y").Value(kingdom.Marriage.Year);
			writer.EndObject();
		}
		// Этап 20 (v9): разведсеть державы.
		if (kingdom.Spy != null && kingdom.Spy.Level > 0)
		{
			writer.Key("spy").BeginObject();
			writer.Key("l").Value(kingdom.Spy.Level);
			writer.Key("o").Value(kingdom.Spy.LastOpYear);
			writer.Key("i").Value(kingdom.Spy.LastInvestYear);
			writer.EndObject();
		}
			if (kingdom.Regency != null && kingdom.Regency.IsActive)
				{
					writer.Key("regency").BeginObject();
					writer.Key("active").Value(kingdom.Regency.IsActive);
					writer.Key("regent").Value(kingdom.Regency.RegentName ?? string.Empty);
					writer.Key("regentId").Value(kingdom.Regency.RegentActorId);
					writer.Key("start").Value(kingdom.Regency.StartYear);
					// Этап 23 (v12): кто именно наследник, кому вернётся корона.
					if (kingdom.Regency.HeirActorId != 0)
					{
						writer.Key("heir").Value(kingdom.Regency.HeirActorId);
						writer.Key("heirName").Value(kingdom.Regency.HeirName ?? string.Empty);
						writer.Key("heirCrowned").Value(kingdom.Regency.CrownSitsWithRegent);
					}
					writer.EndObject();
				}
			// Этап 23 (v12): фамилия династии и последний рейд — если есть.
			if (!string.IsNullOrEmpty(kingdom.DynastySurname))
			{
				writer.Key("dsn").Value(kingdom.DynastySurname);
				writer.Key("cln").Value(kingdom.ClanRenamedToSurname);
			}
			if (kingdom.LastPowerRaidYear >= 0)
			{
				writer.Key("lspr").Value(kingdom.LastPowerRaidYear);
			}
			if (kingdom.Election != null)
			{
				writer.Key("election").BeginObject();
				writer.Key("terms").Value(kingdom.Election.TermsServed);
				writer.Key("next").Value(kingdom.Election.NextElectionYear);
				writer.Key("last").Value(kingdom.Election.LastElectionYear);
				writer.Key("past").BeginArray();
				foreach (var p in kingdom.Election.PastPresidents)
				{
					writer.BeginObject();
					writer.Key("id").Value(p.ActorId);
					writer.Key("name").Value(p.Name ?? string.Empty);
					writer.Key("terms").Value(p.TermsCount);
					writer.EndObject();
				}
				writer.EndArray();
				writer.EndObject();
			}
			// Этап 26 (v14): писаная конституция державы.
			if (kingdom.Constitution != null)
			{
				writer.Key("cnst").BeginObject();
                if (kingdom.Constitution.Electoral != null) { writer.Key("electoral26"); GovernanceCodec.WriteChapter(writer, kingdom.Constitution.Electoral); }
				writer.Key("sf").Value((int)kingdom.Constitution.StateForm);
				writer.Key("fr").Value((int)kingdom.Constitution.Franchise);
				writer.Key("bl").Value((int)kingdom.Constitution.Borders);
				writer.Key("sc").Value((int)kingdom.Constitution.Succession);
				writer.Key("sp").Value((int)kingdom.Constitution.Supremacy);
				// 0.24: расширенные статьи VII–XV.
				writer.Key("tx").Value((int)kingdom.Constitution.Taxes);
				writer.Key("tr").Value((int)kingdom.Constitution.Trade);
				writer.Key("fh").Value((int)kingdom.Constitution.Faith);
				writer.Key("lv").Value((int)kingdom.Constitution.Levy);
				writer.Key("ld").Value((int)kingdom.Constitution.Land);
				writer.Key("wt").Value((int)kingdom.Constitution.Watch);
				writer.Key("bd").Value((int)kingdom.Constitution.Blood);
				writer.Key("im").Value((int)kingdom.Constitution.Impeachment);
				writer.Key("un").Value((int)kingdom.Constitution.Union);
				// 0.52: статьи о народах.
				writer.Key("rc").Value((int)kingdom.Constitution.Race);
				writer.Key("lg").Value((int)kingdom.Constitution.Language);
				writer.Key("ct").Value((int)kingdom.Constitution.Court);
				writer.Key("alc").Value(kingdom.Constitution.AllowLocalConstitutions);
				writer.Key("tlc").Value(kingdom.Constitution.TolerateLocalContradictions);
				writer.Key("fra").Value(kingdom.Constitution.ForeignRulerAllowed);
				// 0.53: палаты.
				writer.Key("chm").Value((int)kingdom.Constitution.Chambers);
				writer.Key("ub").Value((int)kingdom.Constitution.UpperBasis);
				writer.Key("rvt").Value(kingdom.Constitution.RulerVeto);
				writer.Key("rds").Value(kingdom.Constitution.RulerMayDissolve);
				if (kingdom.ConstitutionHistory.Count > 0)
				{
					writer.Key("hist").BeginArray();
					for (int h = 0; h < kingdom.ConstitutionHistory.Count; h++)
					{
						ConstitutionEdit edit = kingdom.ConstitutionHistory[h];
						writer.BeginObject();
						writer.Key("y").Value(edit.Year);
						writer.Key("a").Value(edit.Article ?? string.Empty);
						writer.Key("from").Value(edit.From ?? string.Empty);
						writer.Key("to").Value(edit.To ?? string.Empty);
						writer.Key("who").Value(edit.EditorName ?? string.Empty);
						writer.EndObject();
					}
					writer.EndArray();
				}
				if (kingdom.WarAuthorizations.Count > 0)
				{
					writer.Key("wauth").BeginArray();
					for (int w = 0; w < kingdom.WarAuthorizations.Count; w++)
					{
						WarAuthorization auth = kingdom.WarAuthorizations[w];
						writer.BeginObject();
						writer.Key("t").Value(auth.TargetKingdomId);
						writer.Key("y").Value(auth.Year);
						writer.Key("until").Value(auth.ValidUntilYear);
						writer.Key("vf").Value(auth.VotesFor);
						writer.Key("va").Value(auth.VotesAgainst);
						writer.EndObject();
					}
					writer.EndArray();
				}
				if (kingdom.ReferendumCooldowns.Count > 0)
				{
					writer.Key("rcd").BeginArray();
					foreach (KeyValuePair<int, int> cd in kingdom.ReferendumCooldowns)
					{
						writer.BeginObject();
						writer.Key("t").Value(cd.Key);
						writer.Key("until").Value(cd.Value);
						writer.EndObject();
					}
					writer.EndArray();
				}
				writer.EndObject();
			}
			if (kingdom.LastAmendmentYear > -100)
			{
				writer.Key("lam").Value(kingdom.LastAmendmentYear);
			}
			if (kingdom.LastConstitutionalUsurpYear > -100)
			{
				writer.Key("lcu").Value(kingdom.LastConstitutionalUsurpYear);
			}
			if (!string.IsNullOrEmpty(kingdom.TitularRaceId))
			{
				writer.Key("race").Value(kingdom.TitularRaceId);
			}
			writer.Key("cwTotal").Value(kingdom.CivilWarsTotal);
			writer.Key("cwLast").Value(kingdom.LastCivilWarYear);
			if (kingdom.CivilWar != null)
			{
				CivilWarState cw = kingdom.CivilWar;
				writer.Key("cw").BeginObject();
				writer.Key("cause").Value((int)cw.Cause);
				writer.Key("start").Value(cw.StartedYear);
				writer.Key("ideology").Value((int)cw.RebelIdeology);
				writer.Key("leader").Value(cw.RebelLeaderName ?? string.Empty);
				writer.Key("leaderFem").Value(cw.RebelLeaderIsFemale);
				writer.Key("leaderId").Value(cw.RebelLeaderActorId);
				writer.Key("capital").Value(cw.RebelCapitalCityId);
				writer.Key("parentCapital").Value(cw.ParentCapitalCityId);
				writer.Key("rebelKingdom").Value(cw.RebelKingdomId);
				writer.Key("cities").BeginArray();
				for (int c = 0; c < cw.RebelCityIds.Count; c++)
				{
					writer.Value(cw.RebelCityIds[c]);
				}
				writer.EndArray();
				writer.EndObject();
			}
			EconomyState? eco = kingdom.Economy;
			if (eco != null)
			{
				writer.Key("eco").BeginObject();
				writer.Key("prosperity").Value(eco.Prosperity);
				writer.Key("tax").Value(eco.TaxRate);
				writer.Key("tariff").Value(eco.Tariff);
				writer.Key("deficit").Value(eco.DeficitYears);
				writer.Key("crisis").Value((int)eco.Crisis);
				writer.Key("crisisUntil").Value(eco.CrisisUntilYear);
				writer.Key("lastCrisis").Value(eco.LastCrisisYear);
				writer.Key("crises").Value(eco.CrisesTotal);
				writer.Key("booms").Value(eco.BoomsTotal);
				writer.Key("lastBoom").Value(eco.LastBoomYear);
				writer.Key("austerity").Value(eco.Austerity);
				writer.Key("austerityUntil").Value(eco.AusterityUntilYear);
				writer.Key("embargo").Value(eco.EmbargoTargetId);
				writer.Key("embargoUntil").Value(eco.EmbargoUntilYear);
				writer.Key("tradeShare").Value(eco.TradeShare);
				writer.Key("income").Value(eco.LastIncome);
				writer.Key("expenses").Value(eco.LastExpenses);
				writer.EndObject();
			}
			if (kingdom.Revolution != null)
			{
				writer.Key("rev").BeginObject();
				writer.Key("t").Value((int)kingdom.Revolution.Target);
				writer.Key("s").Value((int)kingdom.Revolution.TargetStance);
				writer.Key("y").Value(kingdom.Revolution.StartedYear);
				writer.Key("left").Value(kingdom.Revolution.YearsLeft);
				writer.EndObject();
			}
			if (kingdom.ActiveCampaign != null)
			{
				StrategicCampaignPlan camp = kingdom.ActiveCampaign;
				writer.Key("camp").BeginObject();
				writer.Key("g").Value((int)camp.Goal);
				writer.Key("tk").Value(camp.TargetKingdomId);
				writer.Key("tn").Value(camp.TargetKingdomName ?? string.Empty);
				writer.Key("sub").Value(camp.SubjectName ?? string.Empty);
				writer.Key("sy").Value(camp.StartedYear);
				writer.Key("dy").Value(camp.DeadlineYear);
				writer.Key("idx").Value(camp.CurrentStepIndex);
				writer.Key("done").Value(camp.CompletedSteps);
				writer.Key("rk").Value(camp.ReasonKey ?? string.Empty);
				writer.Key("steps").BeginArray();
				for (int st = 0; st < camp.Steps.Count; st++)
				{
					writer.Value((int)camp.Steps[st]);
				}
				writer.EndArray();
				writer.EndObject();
			}
			if (kingdom.LastCivicMovementYear > -100)
			{
				writer.Key("lcm").Value(kingdom.LastCivicMovementYear);
			}
			if (kingdom.ActiveMovement != null)
			{
				CivicMovement mov = kingdom.ActiveMovement;
				writer.Key("mov").BeginObject();
				writer.Key("d").Value((int)mov.Demand);
				writer.Key("st").Value((int)mov.Stage);
				writer.Key("sy").Value(mov.StartedYear);
				writer.Key("sh").Value(mov.SupportShare);
				writer.Key("ln").Value(mov.LeaderName ?? string.Empty);
				writer.EndObject();
			}
			if (kingdom.Parties.Count > 0)
			{
				writer.Key("parties").BeginArray();
				for (int pIdx = 0; pIdx < kingdom.Parties.Count; pIdx++)
				{
					PoliticalParty party = kingdom.Parties[pIdx];
					writer.BeginObject();
					writer.Key("i").Value((int)party.Ideology);
					writer.Key("nk").Value(party.NameKey ?? string.Empty);
					// 0.48.3: собственное имя партии («Народная партия «Ракрог»»).
					writer.Key("nm").Value(party.Name ?? string.Empty);
					writer.Key("sp").Value(party.SeatsPercent);
					writer.Key("r").Value(party.IsRuling);
					writer.EndObject();
				}
				writer.EndArray();
			}
			// 0.49: национализм — что держава хочет вернуть и как сильно.
			{
				NationalismState movement = world.NationalismOf(kingdom.KingdomId);
				if (movement != null && (movement.Active || movement.Pressure > 0f))
				{
					writer.Key("nat").BeginObject();
					writer.Key("ok").Value(movement.OriginKingdomId);
					writer.Key("on").Value(movement.OriginName ?? string.Empty);
					writer.Key("oy").Value(movement.OriginYear);
					writer.Key("bd").Value((int)movement.Bond);
					writer.Key("pr").Value(movement.Pressure);
					writer.Key("pi").Value(movement.PrevIdeology);
					writer.Key("sy").Value(movement.SeizedYear);
					writer.Key("eb").Value(movement.EntryBanUntilYear);
					writer.Key("sh").Value(movement.ShadowUntilYear);
					writer.Key("sup").Value(movement.SuppressedYear);
					writer.Key("vic").Value(movement.Victories);
					writer.EndObject();
				}
			}
			// 0.53: законопроект, который сейчас обсуждают палаты.
			{
				Bill bill = world.BillOf(kingdom.KingdomId);
				if (bill != null && bill.Pending)
				{
					writer.Key("bill").BeginObject();
					writer.Key("a").Value(bill.Article ?? string.Empty);
					writer.Key("f").Value(bill.From ?? string.Empty);
					writer.Key("t").Value(bill.To ?? string.Empty);
					writer.Key("ti").Value(bill.Tightens);
					writer.Key("pp").Value(bill.FromPeople);
					writer.Key("py").Value(bill.ProposedYear);
					writer.Key("st").Value((int)bill.Stage);
					writer.Key("lf").Value(bill.LowerFor);
					writer.Key("la").Value(bill.LowerAgainst);
					writer.Key("uf").Value(bill.UpperFor);
					writer.Key("ua").Value(bill.UpperAgainst);
					writer.Key("cy").Value(bill.ConferenceYear);
					writer.Key("ed").Value(bill.EditorName ?? string.Empty);
					writer.Key("pi").Value(bill.ProposerIdeology.HasValue ? (int)bill.ProposerIdeology.Value : -1);
					writer.EndObject();
				}
			}
			// 0.52: народы державы — когда пришли и на каком языке говорят.
			{
				Dictionary<string, PeopleRecord> book = world.PeoplesOf(kingdom.KingdomId);
				if (book != null && book.Count > 0)
				{
					writer.Key("peo").BeginArray();
					foreach (PeopleRecord record in book.Values)
					{
						writer.BeginObject();
						writer.Key("r").Value(record.RaceId ?? string.Empty);
						writer.Key("n").Value(record.RaceName ?? string.Empty);
						writer.Key("fy").Value(record.FirstSeenYear);
						writer.Key("st").Value((int)record.Stage);
						writer.Key("sy").Value(record.StageSinceYear);
						writer.Key("ry").Value(record.RequestedYear);
						writer.EndObject();
					}
					writer.EndArray();
				}
			}
			// 0.49.1: расовое движение — чужая раса, которая рвётся к власти.
			{
				RaceNationalismState race = world.RaceMovementOf(kingdom.KingdomId);
				if (race != null && (race.Active || race.Pressure > 0f || race.EntryBanUntilYear > 0))
				{
					writer.Key("rnat").BeginObject();
					writer.Key("rid").Value(race.RaceId ?? string.Empty);
					writer.Key("rn").Value(race.RaceName ?? string.Empty);
					writer.Key("sh").Value(race.Share);
					writer.Key("pr").Value(race.Pressure);
					writer.Key("pi").Value(race.PrevIdeology);
					writer.Key("ptr").Value(race.PrevTitularRace ?? string.Empty);
					writer.Key("sy").Value(race.SeizedYear);
					writer.Key("eb").Value(race.EntryBanUntilYear);
					writer.Key("shy").Value(race.ShadowUntilYear);
					writer.Key("vic").Value(race.Victories);
					writer.EndObject();
				}
			}
			if (kingdom.HasTribalEthnonym)
			{
				writer.Key("trEth").Value(true);
			}
			if (kingdom.TribalAwakeningAnnounced)
			{
				writer.Key("trAwk").Value(true);
			}
			if (kingdom.WasCivilized)
			{
				writer.Key("trCiv").Value(true);
			}
			if (kingdom.LastCivilizedYear > 0)
			{
				writer.Key("trCivY").Value(kingdom.LastCivilizedYear);
			}
			writer.EndObject();
		}
		writer.EndArray();
		// 0.52: местные конституции городов.
		if (world.LocalConstitutions.Count > 0)
		{
			writer.Key("locals").BeginArray();
			foreach (LocalConstitution local in world.LocalConstitutions.Values)
			{
				writer.BeginObject();
				writer.Key("c").Value(local.CityId);
				writer.Key("k").Value(local.KingdomId);
				writer.Key("r").Value(local.RaceId ?? string.Empty);
				writer.Key("n").Value(local.RaceName ?? string.Empty);
				writer.Key("fy").Value(local.FoundedYear);
				writer.Key("b").Value(local.Banned);
				writer.Key("by").Value(local.BannedYear);
				writer.Key("wl").Value(local.WantsLanguage);
				writer.Key("wc").Value(local.WantsCourt);
				writer.Key("g").Value(local.Grievance);
				writer.EndObject();
			}
			writer.EndArray();
		}
		// 0.50: пятна за отнятое гражданство (мир помнит пятьдесят лет).
		if (world.HateStains.Count > 0)
		{
			writer.Key("stains").BeginArray();
			foreach (KeyValuePair<int, int> pair in world.HateStains)
			{
				writer.BeginObject();
				writer.Key("k").Value(pair.Key);
				writer.Key("y").Value(pair.Value);
				writer.EndObject();
			}
			writer.EndArray();
		}
		// 0.49.1: кому отозвали гражданство и до какого года закрыт въезд.
		if (world.CitizenshipBans.Count > 0)
		{
			writer.Key("cbans").BeginArray();
			for (int b = 0; b < world.CitizenshipBans.Count; b++)
			{
				CitizenshipBan ban = world.CitizenshipBans[b];
				writer.BeginObject();
				writer.Key("k").Value(ban.KingdomId);
				writer.Key("rid").Value(ban.RaceId ?? string.Empty);
				writer.Key("rn").Value(ban.RaceName ?? string.Empty);
				writer.Key("fy").Value(ban.FromYear);
				writer.Key("uy").Value(ban.UntilYear);
				writer.Key("rv").Value(ban.CitizenshipRevoked);
				writer.EndObject();
			}
			writer.EndArray();
		}
		writer.Key("cities").BeginArray();
		foreach (CityPolitics city in world.Cities)
		{
			writer.BeginObject();
			writer.Key("id").Value(city.CityId);
			writer.Key("k").Value(city.KingdomId);
			writer.Key("i").Value((int)city.Dominant);
			writer.EndObject();
		}
		writer.EndArray();
		writer.Key("actors").BeginArray();
		foreach (KeyValuePair<int, ActorEntry> actorEntry in world.ActorEntries)
		{
			ActorEntry value = actorEntry.Value;
			writer.BeginObject();
			writer.Key("id").Value(value.Id);
			writer.Key("k").Value(value.KingdomId);
			writer.Key("c").Value(value.CityId);
			writer.Key("i").Value((int)value.Politics.Ideology);
			writer.Key("s").Value((int)value.Politics.Stance);
			writer.Key("v").Value(value.Politics.Conviction);
			writer.Key("d").Value(value.Politics.Dissent);
			writer.Key("p").Value(value.Politics.ConversionProgress);
			writer.EndObject();
		}
		writer.EndArray();
		writer.Key("deaths").BeginArray();
		foreach (DeathRecord item in world.Deaths.All)
		{
			writer.BeginObject();
			writer.Key("id").Value(item.ActorId);
			writer.Key("n").Value(item.ActorName);
			writer.Key("c").Value((int)item.Cause);
			writer.Key("y").Value(item.Year);
			writer.Key("killer").Value(item.KillerId ?? (-1));
			writer.Key("kn").Value(item.KillerName);
			writer.Key("kt").Value(item.KillerTitle);
			writer.Key("king").Value(item.KingdomId ?? (-1));
			writer.Key("city").Value(item.CityName);
			writer.Key("r").Value(item.WasRuler);
			writer.Key("ch").Value(item.WasChosen);
			writer.Key("a").Value(item.Age);
			writer.Key("note").Value(item.Note);
            writer.Key("pop_before").Value(item.PopulationBeforeDeath);
            writer.Key("king_name").Value(item.KingdomNameAtDeath);
            writer.Key("last_resident").Value(item.WasLastResident);
            writer.Key("last_active").Value(item.LastResidentEvidenceActive);
			writer.Key("fem").Value(item.WasFemale);
			if (item.ReignYears.HasValue)
			{
				writer.Key("reign").Value(item.ReignYears.Value);
			}
			if (item.KillerIsCiv)
			{
				writer.Key("kc").Value(true);
				writer.Key("kking").Value(item.KillerKingdom);
				writer.Key("kcity").Value(item.KillerCity);
				writer.Key("kjob").Value(item.KillerJob);
				if (!string.IsNullOrEmpty(item.KillerRace))
				{
					writer.Key("krace").Value(item.KillerRace);
				}
			}
			else if (!string.IsNullOrEmpty(item.KillerSpecies))
			{
				writer.Key("kspec").Value(item.KillerSpecies);
			}
			if (item.KillerFemale)
			{
				writer.Key("kf").Value(true);
			}
			if (item.KillerAge > 0)
			{
				writer.Key("ka").Value(item.KillerAge);
			}
			writer.EndObject();
		}
		writer.EndArray();
		// Этап 25 (v13): реестр родословной — все жители, замеченные миром.
		writer.Key("family").BeginArray();
		foreach (FamilyRecord fr in world.Family.ToList())
		{
			writer.BeginObject();
			writer.Key("id").Value(fr.Id.ToString());
			writer.Key("n").Value(fr.Name ?? string.Empty);
			writer.Key("f").Value(fr.Female);
			writer.Key("by").Value(fr.BirthYear);
			if (fr.DeathYear != null)
			{
				writer.Key("dy").Value(fr.DeathYear.Value);
			}
			if (fr.FatherId != 0)
			{
				writer.Key("fa").Value(fr.FatherId.ToString());
			}
			if (fr.MotherId != 0)
			{
				writer.Key("mo").Value(fr.MotherId.ToString());
			}
			if (!string.IsNullOrEmpty(fr.ClanName))
			{
				writer.Key("cl").Value(fr.ClanName);
			}
			if (fr.ClanId != 0)
			{
				writer.Key("cid").Value(fr.ClanId.ToString());
			}
			writer.Key("k").Value(fr.KingdomId);
			writer.Key("seen").Value(fr.LastSeenYear);
			writer.EndObject();
		}
		writer.EndArray();
		writer.Key("unions").BeginArray();
		foreach (Union union in world.Unions)
		{
			writer.BeginObject();
			writer.Key("id").Value(union.Id);
			writer.Key("t").Value((int)union.Type);
			writer.Key("n").Value(union.Name);
			writer.Key("nk").Value(union.NameKey);
			writer.Key("leader").Value(union.LeaderId);
			writer.Key("y").Value(union.FormedYear);
			writer.Key("c").Value(union.Cohesion);
			writer.Key("w").Value(union.LeaderWarriors);
			writer.Key("m").BeginArray();
			for (int i = 0; i < union.Members.Count; i++)
			{
				// С v7 участник — объект: одобрение и год вступления раньше терялись,
				// и загруженный вассал на грани выхода «прощал» сюзерена.
				writer.BeginObject();
				writer.Key("id").Value(union.Members[i].KingdomId);
				writer.Key("a").Value(union.Members[i].Approval);
				writer.Key("j").Value(union.Members[i].JoinedYear);
				writer.EndObject();
			}
			writer.EndArray();
			// 0.24: поглощённые в соединение державы — снятые границы для «права на выход» (0.25).
			if (union.AbsorbedChildren.Count > 0)
			{
				writer.Key("abs").BeginArray();
				for (int i = 0; i < union.AbsorbedChildren.Count; i++)
				{
					AbsorbedChild child = union.AbsorbedChildren[i];
					writer.BeginObject();
					writer.Key("id").Value(child.KingdomId);
					writer.Key("cap").Value(child.CapitalCityId);
					writer.Key("y").Value(child.AbsorbedYear);
					writer.Key("cities").BeginArray();
					for (int j = 0; j < child.CityIds.Count; j++)
					{
						writer.Value(child.CityIds[j]);
					}
					writer.EndArray();
					writer.EndObject();
				}
				writer.EndArray();
			}
			bool hasVassalBorders = false;
			for (int i = 0; i < union.Members.Count; i++)
			{
				if (union.Members[i].CityIds != null && union.Members[i].CityIds.Count > 0)
				{
					hasVassalBorders = true;
					break;
				}
			}
			if (hasVassalBorders)
			{
				writer.Key("vb").BeginArray();
				for (int i = 0; i < union.Members.Count; i++)
				{
					UnionMember mem = union.Members[i];
					if (mem.CityIds == null || mem.CityIds.Count == 0)
					{
						continue;
					}
					writer.BeginObject();
					writer.Key("id").Value(mem.KingdomId);
					writer.Key("cap").Value(mem.CapitalCityId);
					writer.Key("n").Value(mem.OriginalName ?? string.Empty);
					writer.Key("i").Value((int)mem.OriginalIdeology);
					writer.Key("cities").BeginArray();
					for (int j = 0; j < mem.CityIds.Count; j++)
					{
						writer.Value(mem.CityIds[j]);
					}
					writer.EndArray();
					writer.EndObject();
				}
				writer.EndArray();
			}
			writer.EndObject();
		}
		writer.EndArray();
		// Этап 19 (v8): личные отношения правителей (пары государств). Пустой список не пишем —
		// так сохранения миров без динамики остаются байт-в-байт совместимыми с форматом до этапа 19.
		if (world.RulerRelations.Count > 0)
		{
			writer.Key("rels").BeginArray();
			foreach (KeyValuePair<long, int> relation in world.RulerRelations)
			{
				writer.BeginObject();
				writer.Key("a").Value((int)(relation.Key >> 32));
				writer.Key("b").Value((int)(relation.Key & 0xFFFFFFFF));
				writer.Key("v").Value(relation.Value);
				writer.EndObject();
			}
			writer.EndArray();
		}
		// Этап 20 (v9): договоры, обиды, коалиции и история объявленных войн.
		// Пустые секции не пишем — миры до этапа 20 остаются байт-в-байт совместимыми.
		if (world.TreatiesBook.Count > 0)
		{
			writer.Key("treaties").BeginArray();
			foreach (Treaty treaty in world.TreatiesBook.All)
			{
				writer.BeginObject();
				writer.Key("t").Value((int)treaty.Type);
				writer.Key("a").Value(treaty.PartyA);
				writer.Key("b").Value(treaty.PartyB);
				writer.Key("s").Value(treaty.SignedYear);
				writer.Key("e").Value(treaty.ExpiresYear);
				writer.EndObject();
			}
			writer.EndArray();
		}
		if (world.Grievances.Raw.Count > 0)
		{
			writer.Key("griev").BeginArray();
			foreach (KeyValuePair<long, float> griev in world.Grievances.Raw)
			{
				writer.BeginObject();
				writer.Key("f").Value((int)(griev.Key >> 32));
				writer.Key("o").Value((int)(griev.Key & 0xFFFFFFFF));
				writer.Key("v").Value(griev.Value);
				writer.EndObject();
			}
			writer.EndArray();
		}
		if (world.Coalitions.All.Count > 0)
		{
			writer.Key("coals").BeginArray();
			foreach (Coalition coalition in world.Coalitions.All)
			{
				writer.BeginObject();
				writer.Key("i").Value(coalition.Id);
				writer.Key("t").Value(coalition.TargetId);
				writer.Key("y").Value(coalition.FormedYear);
				writer.Key("w").Value(coalition.LastWarYear);
				writer.Key("m").BeginArray();
				for (int m = 0; m < coalition.Members.Count; m++)
				{
					writer.Value(coalition.Members[m]);
				}
				writer.EndArray();
				writer.EndObject();
			}
			writer.EndArray();
		}
		if (world.WarDeclarations.Count > 0)
		{
			writer.Key("wars").BeginArray();
			foreach (WarDeclarationRecord record in world.WarDeclarations)
			{
				writer.BeginObject();
				writer.Key("a").Value(record.AttackerId);
				writer.Key("d").Value(record.DefenderId);
				writer.Key("y").Value(record.Year);
				writer.Key("c").Value((int)record.CasusBelli);
				writer.EndObject();
			}
			writer.EndArray();
		}
		// Этап 22 (v11): перемирия и действующие репарации. Пустые секции не пишем.
		if (world.TrucesBook.Count > 0)
		{
			writer.Key("truces").BeginArray();
			foreach (KeyValuePair<long, int> truce in world.TrucesBook.Raw)
			{
				writer.BeginObject();
				writer.Key("a").Value((int)(truce.Key >> 32));
				writer.Key("b").Value((int)(truce.Key & 0xFFFFFFFF));
				writer.Key("u").Value(truce.Value);
				writer.EndObject();
			}
			writer.EndArray();
		}
		if (world.Reparations.Count > 0)
		{
			writer.Key("reps").BeginArray();
			foreach (ReparationsOrder order in world.Reparations)
			{
				writer.BeginObject();
				writer.Key("p").Value(order.PayerId);
				writer.Key("t").Value(order.PayeeId);
				writer.Key("a").Value(order.AmountPerYear);
				writer.Key("y").Value(order.YearsLeft);
				writer.EndObject();
			}
			writer.EndArray();
		}
		// 0.32: состояние конференции КМУД. Пишем только если она созывалась.
		if (world.Kmud != null && world.Kmud.WasHeld)
		{
			writer.Key("kmud").BeginObject();
			writer.Key("ph").Value((int)world.Kmud.Phase);
			writer.Key("oy").Value(world.Kmud.OpenedYear);
			writer.Key("cy").Value(world.Kmud.ClosesYear);
			writer.Key("tu").Value(world.Kmud.TruceUntilYear);
			writer.Key("mem").Value(world.Kmud.Members);
			writer.Key("wp2").Value(world.Kmud.WarsPrevented);
			writer.Key("mi").BeginArray();
			foreach (int memberId in world.Kmud.MemberIds)
			{
				writer.BeginObject();
				writer.Key("k").Value(memberId);
				writer.EndObject();
			}
			writer.EndArray();
			writer.Key("we").Value(world.Kmud.WarsEnded);
			writer.Key("wp").Value(world.Kmud.WarsStoppedDuringPause);
			writer.Key("al").Value(world.Kmud.AllianceId);
			writer.Key("af").Value(world.Kmud.AllianceFormed ? 1 : 0);
			writer.Key("mr").Value(world.Kmud.MembersReturned);
			writer.Key("rnd").Value(world.Kmud.Round);
			writer.Key("res").Value(world.Kmud.ResultsAnnounced ? 1 : 0);
			writer.Key("py").Value(world.Kmud.LastPeaceYears);
			// Повестка конференции.
			writer.Key("ag").BeginObject();
			writer.Key("a").Value(world.Kmud.Agenda.AggressorId);
			writer.Key("d").Value(world.Kmud.Agenda.DefenderId);
			writer.Key("cy").Value(world.Kmud.Agenda.ConflictYears);
			writer.Key("w").Value(world.Kmud.Agenda.WorstId);
			writer.Key("p").Value(world.Kmud.Agenda.PoorestId);
			// 0.47: пятый вопрос — о каком народе и куда.
			writer.Key("rr").Value(world.Kmud.Agenda.RepatriationRaceId ?? string.Empty);
			writer.Key("rn").Value(world.Kmud.Agenda.RepatriationRaceName ?? string.Empty);
			writer.Key("rt").Value(world.Kmud.Agenda.RepatriationToId);
			writer.Key("rf").Value(world.Kmud.Agenda.RepatriationFromId);
			writer.EndObject();
			// Бюллетени: по каждому вопросу — счётчики вариантов.
			writer.Key("bal").BeginArray();
			foreach (KeyValuePair<KmudQuestion, KmudBallotResult> ballot in world.Kmud.Voting.Ballots)
			{
				writer.BeginObject();
				writer.Key("q").Value((int)ballot.Key);
				writer.Key("v").Value(ballot.Value.Voters);
				writer.Key("w").Value(ballot.Value.Winner);
				writer.Key("c").BeginArray();
				foreach (KeyValuePair<int, int> count in ballot.Value.Counts)
				{
					writer.BeginObject();
					writer.Key("o").Value(count.Key);
					writer.Key("n").Value(count.Value);
					writer.EndObject();
				}
				writer.EndArray();
				writer.EndObject();
			}
			writer.EndArray();
			// Решения съезда.
			writer.Key("dec").BeginObject();
			writer.Key("o").Value(world.Kmud.Decisions.Outcome);
			writer.Key("py").Value(world.Kmud.Decisions.PeaceYears);
			writer.Key("pu").Value(world.Kmud.Decisions.Punishment);
			writer.Key("ai").Value(world.Kmud.Decisions.Aid);
			writer.Key("pid").Value(world.Kmud.Decisions.PunishedId);
			writer.Key("aid").Value(world.Kmud.Decisions.AidedId);
			writer.Key("pay").Value(world.Kmud.Decisions.PayerId);
			writer.Key("rec").Value(world.Kmud.Decisions.PayeeId);
			writer.Key("ap").Value(world.Kmud.Decisions.Applied ? 1 : 0);
			// 0.47: решение по пятому вопросу.
			writer.Key("rp").Value(world.Kmud.Decisions.Repatriation);
			writer.Key("rr").Value(world.Kmud.Decisions.RepatriationRaceId ?? string.Empty);
			writer.Key("rt").Value(world.Kmud.Decisions.RepatriationToId);
			writer.Key("rc").Value(world.Kmud.Decisions.RepatriatedCount);
			writer.EndObject();
			// Пакты «только оборона».
			writer.Key("pacts").BeginArray();
			foreach (KmudPact pact in world.Kmud.Pacts)
			{
				writer.BeginObject();
				writer.Key("a").Value(pact.A);
				writer.Key("b").Value(pact.B);
				writer.Key("u").Value(pact.UntilYear);
				writer.Key("al").Value(pact.AllianceFormed ? 1 : 0);
				writer.EndObject();
			}
			writer.EndArray();
			// Санкции.
			writer.Key("sanct").BeginArray();
			foreach (KmudSanction sanction in world.Kmud.Sanctions)
			{
				writer.BeginObject();
				writer.Key("k").Value(sanction.KingdomId);
				writer.Key("u").Value(sanction.UntilYear);
				writer.Key("s").Value(sanction.Supporters);
				writer.EndObject();
			}
			writer.EndArray();
			// Ассоциации.
			writer.Key("assoc").BeginArray();
			foreach (KmudAssociation association in world.Kmud.Associations)
			{
				writer.BeginObject();
				writer.Key("d").Value(association.DonorId);
				writer.Key("r").Value(association.RecipientId);
				writer.Key("u").Value(association.UntilYear);
				writer.Key("g").Value(association.GoldPerYear);
				writer.Key("f").Value(association.FoodPerYear);
				writer.EndObject();
			}
			writer.EndArray();
			writer.EndObject();
		}
		// 0.23.2: довоенные города воюющих держав и отложенные восстановления.
		bool wrotePrewar = false;
		foreach (KingdomPolitics pk in world.Kingdoms)
		{
			IReadOnlyList<int> pre = world.PrewarCitiesOf(pk.KingdomId);
			if (pre.Count == 0)
			{
				continue;
			}
			if (!wrotePrewar)
			{
				writer.Key("prewar").BeginArray();
				wrotePrewar = true;
			}
			writer.BeginObject();
			writer.Key("k").Value(pk.KingdomId);
			writer.Key("c").BeginArray();
			for (int i = 0; i < pre.Count; i++)
			{
				writer.Value(pre[i]);
			}
			writer.EndArray();
			writer.EndObject();
		}
		if (wrotePrewar)
		{
			writer.EndArray();
		}
		if (world.PendingRestorations.Count > 0)
		{
			writer.Key("restore").BeginArray();
			foreach (RestorationOrder ro in world.PendingRestorations)
			{
				writer.BeginObject();
				writer.Key("d").Value(ro.DefeatedId);
				writer.Key("v").Value(ro.VictorId);
				writer.Key("m").Value((int)ro.Mode);
				writer.Key("keep").Value(ro.CitiesKeptByVictor);
				writer.Key("n").Value(ro.DefeatedName);
				writer.Key("y").Value(ro.Year);
				if (ro.IsNationalLiberation)
				{
					writer.Key("lib").Value(true);
				}
				if (ro.LiberationPeaceful)
				{
					writer.Key("lp").Value(true);
				}
				writer.Key("c").BeginArray();
				for (int i = 0; i < ro.PrewarCityIds.Count; i++)
				{
					writer.Value(ro.PrewarCityIds[i]);
				}
				writer.EndArray();
				writer.EndObject();
			}
			writer.EndArray();
		}
		// Этап 23 (v12): пограничная политика пар держав; непустую книгу пишем.
		bool wroteBorders = false;
		foreach (KeyValuePair<long, BorderRecord> pair in world.BordersBook.All)
		{
			if (!wroteBorders)
			{
				writer.Key("borders").BeginArray();
				wroteBorders = true;
			}
			writer.BeginObject();
			writer.Key("a").Value((int)(pair.Key >> 32));
			writer.Key("b").Value((int)(pair.Key & 0xFFFFFFFF));
			writer.Key("s").Value((int)pair.Value.Status);
			writer.Key("u").Value(pair.Value.SinceYear);
			writer.Key("l").Value(pair.Value.LockedUntilYear);
			writer.EndObject();
		}
		if (wroteBorders)
		{
			writer.EndArray();
		}
		// Этап 21 (v10): снимок ванильных альянсов игры. Пустой список не пишем:
		// если секции нет, первый игровой снимок после загрузки запишется молча как базовый.
		if (world.AlliancesBook.Count > 0)
		{
			writer.Key("alls").BeginArray();
			foreach (AllianceRecord alliance in world.AlliancesBook.All)
			{
				writer.BeginObject();
                if(alliance.Id>0)writer.Key("id").Value(alliance.Id);
				writer.Key("n").Value(alliance.Name);
				writer.Key("l").Value(alliance.LeaderId);
				writer.Key("m").BeginArray();
				for (int m = 0; m < alliance.Members.Count; m++)
				{
					writer.Value(alliance.Members[m]);
				}
				writer.EndArray();
				writer.EndObject();
			}
			writer.EndArray();
		}
		writer.Key("routes").BeginArray();
		foreach (TradeRoute route in world.Trade.Routes)
		{
			writer.BeginObject();
			writer.Key("id").Value(route.Id);
			writer.Key("from").Value(route.FromKingdomId);
			writer.Key("to").Value(route.ToKingdomId);
			writer.Key("year").Value(route.EstablishedYear);
			writer.Key("level").Value(route.Level);
			writer.Key("vol").Value(route.Volume);
			writer.Key("blocked").Value(route.Blocked);
			writer.EndObject();
		}
		writer.EndArray();
		if (world.DiplomaticMemories.Count > 0)
		{
			writer.Key("dipmem").BeginArray();
			foreach (DiplomaticMemoryEntry dm in world.DiplomaticMemories.All)
			{
				writer.BeginObject();
				writer.Key("f").Value(dm.FromKingdomId);
				writer.Key("t").Value(dm.TowardsKingdomId);
				writer.Key("k").Value((int)dm.Kind);
				writer.Key("y").Value(dm.Year);
				writer.Key("e").Value(dm.ExpiresYear);
				writer.Key("d").Value(dm.TrustDelta);
				if (!string.IsNullOrEmpty(dm.SubjectName))
				{
					writer.Key("s").Value(dm.SubjectName);
				}
				writer.EndObject();
			}
			writer.EndArray();
		}
		if (world.CityHomelands.Count > 0)
		{
			writer.Key("homelands").BeginArray();
			foreach (CityHomelandRecord hl in world.CityHomelands.All.Values)
			{
				writer.BeginObject();
				writer.Key("c").Value(hl.CityId);
				writer.Key("ok").Value(hl.OriginalKingdomId);
				writer.Key("on").Value(hl.OriginalKingdomName ?? string.Empty);
				writer.Key("oi").Value((int)hl.OriginalIdeology);
				if (hl.WasHistoricalCapital)
				{
					writer.Key("cap").Value(true);
				}
				if (hl.AnnexedYear > 0)
				{
					writer.Key("ay").Value(hl.AnnexedYear);
				}
				writer.EndObject();
			}
			writer.EndArray();
		}
		writer.EndObject();
		return writer.ToString();
	}

	public static List<Union> Apply(PoliticalWorld world, string json)
	{
		List<Union> list = new List<Union>();
		if (string.IsNullOrEmpty(json))
		{
			return list;
		}
		TinyJson.Node node;
		try
		{
			node = TinyJson.ParseStrict(json);
		}
		catch (Exception)
		{
			return list;
		}
		if (node == null || node.IsNull || node.Kind != TinyJson.TokenKind.Object)
		{
			return list;
		}
        // Validate all new state before clearing or changing the currently running world.
        // A damaged ballot must not silently become legacy mode or destroy existing votes.
        if(node.GetInt("format")>FormatVersion)throw new FormatException("This state was written by a newer save format.");
        if(node.Get("kingdoms")!=null && node.Get("kingdoms").Kind!=TinyJson.TokenKind.Array)throw new FormatException("Invalid kingdom collection.");
        GovernanceState governance = GovernanceCodec.Read(node.Get("governance26"));
        var electoralChapters = new Dictionary<int, ElectoralChapter>();
        var stagedKingdoms = node.Get("kingdoms");
        if (stagedKingdoms != null)
            foreach (var staged in stagedKingdoms.Arr)
            {
                var chapter = staged.Get("cnst")?.Get("electoral26");
                if (chapter != null)
                {
                    int id = staged.GetInt("id", -1);
                    if (id <= 0 || electoralChapters.ContainsKey(id)) throw new FormatException("Invalid electoral chapter owner.");
                    electoralChapters.Add(id, GovernanceCodec.ReadChapter(chapter));
                }
            }
        var allianceSnapshot=ReadAllianceSnapshot(node.Get("alls"));
        world.Clear();
        world.RestoreGovernance(governance);
		TinyJson.Node node2 = node.Get("kingdoms");
		if (node2 != null)
		{
			for (int i = 0; i < node2.Arr.Count; i++)
			{
				TinyJson.Node node3 = node2.Arr[i];
				int num = node3.GetInt("id", -1);
				if (num >= 0)
				{
					KingdomPolitics kingdomPolitics = world.AddKingdom(num, (IdeologyType)node3.GetInt("i"), node3.GetInt("f"), (ReformStance)node3.GetInt("s", 2));
						kingdomPolitics.Legitimacy = node3.GetFloat("l", kingdomPolitics.Legitimacy);
						kingdomPolitics.Support = node3.GetFloat("support", kingdomPolitics.Support);
						kingdomPolitics.AvgDissent = node3.GetFloat("dissent", kingdomPolitics.AvgDissent);
					kingdomPolitics.InWar = node3.GetBool("war", kingdomPolitics.InWar);
					kingdomPolitics.Starving = node3.GetBool("starving", kingdomPolitics.Starving);
					// Этап 22 (v11): усталость от войны; в мирах v1–v10 отсутствует — стартует с нуля.
					kingdomPolitics.WarFatigue = node3.GetFloat("wf", 0f);
					// 0.39: поражения и обещания; в старых мирах отсутствуют — чистый лист.
					kingdomPolitics.Defeats = node3.GetInt("dfc");
					kingdomPolitics.LastDefeatYear = node3.GetInt("ldy", -100);
					kingdomPolitics.WarAversionUntilYear = node3.GetInt("wav");
					kingdomPolitics.LastVictoryYear = node3.GetInt("lvy", -100);
					kingdomPolitics.PromiseScore = node3.GetFloat("prm", 0f);
					kingdomPolitics.LastPromiseBrokenYear = node3.GetInt("prmy", -100);
						TinyJson.Node nodeMechanics = node3.Get("mech");
						if (nodeMechanics != null && !nodeMechanics.IsNull)
						{
							kingdomPolitics.Mechanics.LastUsedYear = nodeMechanics.GetInt("last", int.MinValue);
							kingdomPolitics.Mechanics.ActiveUntilYear = nodeMechanics.GetInt("active");
							int target = nodeMechanics.GetInt("target", -1);
							kingdomPolitics.Mechanics.TargetKingdomId = target < 0 ? (int?)null : target;
							kingdomPolitics.Mechanics.ThreatLevel = nodeMechanics.GetFloat("threat", kingdomPolitics.Mechanics.ThreatLevel);
							kingdomPolitics.Mechanics.ThreatBreaks = nodeMechanics.GetInt("breaks");
							kingdomPolitics.Mechanics.LastBreakYear = nodeMechanics.GetInt("breakYear", int.MinValue);
						}
					int num2 = node3.GetInt("ruler", -1);
					if (num2 >= 0)
					{
						kingdomPolitics.Ruler = new RulerRef
						{
							ActorId = num2,
							Name = node3.GetString("rulerName"),
							KingdomId = num,
							SinceYear = node3.GetInt("rulerSince"),
							IsFemale = node3.GetBool("rulerFemale", false),
							Age = node3.GetInt("rulerAge", 0),
							ClanName = node3.GetString("rulerClan", string.Empty),
							ClanId = (long)node3.GetInt("rulerClanId", 0)
						};
						world.SetLastRuler(num, kingdomPolitics.Ruler);
					}
					kingdomPolitics.RoyalClanName = node3.GetString("clan", string.Empty);
					kingdomPolitics.RoyalClanId = (long)node3.GetInt("clanId", 0);
					kingdomPolitics.PreviousRoyalClanId = (long)node3.GetInt("prevClanId", 0);
					// 0.24, статья XIII: венценосная история домов (право крови переживает сохранение).
					TinyJson.Node royalClans = node3.Get("royalClans");
					if (royalClans != null)
					{
						for (int h = 0; h < royalClans.Arr.Count; h++)
						{
							kingdomPolitics.RecordRoyalClan((long)royalClans.Arr[h].Num);
						}
					}
					kingdomPolitics.DynastyFounder = node3.GetString("dynFounder", string.Empty);
					kingdomPolitics.DynastyReignsCount = node3.GetInt("dynReigns", 1);
					kingdomPolitics.DynastyStartYear = node3.GetInt("dynStart", kingdomPolitics.FoundedYear);
					kingdomPolitics.LastImpeachmentYear = node3.GetInt("impeachYear", -100);
					// Этап 19 (v8): личность и брак; в мирах v1–v7 отсутствуют — останутся null до воцарения.
					TinyJson.Node nodePers = node3.Get("pers");
					if (nodePers != null)
					{
						kingdomPolitics.Personality = new RulerPersonality
						{
							Trait = (RulerTrait)nodePers.GetInt("t", 0),
							Ambition = nodePers.GetFloat("a", 50f)
						};
					}
				TinyJson.Node nodeMrg = node3.Get("mrg");
				if (nodeMrg != null)
				{
					kingdomPolitics.Marriage = new MarriageBond
					{
						PartnerId = nodeMrg.GetInt("p"),
						Year = nodeMrg.GetInt("y")
					};
				}
				// Этап 20 (v9): разведсеть; в мирах v1–v8 отсутствует.
				TinyJson.Node nodeSpy = node3.Get("spy");
				if (nodeSpy != null)
				{
					kingdomPolitics.Spy = new SpyNetwork
					{
						Level = nodeSpy.GetInt("l", 0),
						LastOpYear = nodeSpy.GetInt("o", int.MinValue),
						LastInvestYear = nodeSpy.GetInt("i", int.MinValue)
					};
				}
				if (node3.Has("law"))
					{
						kingdomPolitics.SuccessionLaw = (SuccessionLaw)node3.GetInt("law", (int)SuccessionLaws.GetDefaultLaw(kingdomPolitics.Ideology));
					}
					else
					{
						kingdomPolitics.SuccessionLaw = SuccessionLaws.GetDefaultLaw(kingdomPolitics.Ideology);
					}
					TinyJson.Node nodeRegency = node3.Get("regency");
					if (nodeRegency != null && !nodeRegency.IsNull)
					{
						kingdomPolitics.Regency = new RegencyState
						{
							IsActive = nodeRegency.GetBool("active", false),
							RegentName = nodeRegency.GetString("regent", string.Empty),
							RegentActorId = nodeRegency.GetInt("regentId", 0),
							StartYear = nodeRegency.GetInt("start", 0),
							// Этап 23 (v12): старые сейвы наследника не знали — 0/пусто.
							HeirActorId = nodeRegency.GetInt("heir", 0),
							HeirName = nodeRegency.GetString("heirName", string.Empty),
							CrownSitsWithRegent = nodeRegency.GetBool("heirCrowned", false)
						};
					}
					// Этап 23 (v12): фамилия, клан, рейды.
					kingdomPolitics.DynastySurname = node3.GetString("dsn", string.Empty);
					kingdomPolitics.ClanRenamedToSurname = node3.GetBool("cln", false);
					kingdomPolitics.LastPowerRaidYear = node3.GetInt("lspr", -100);
					TinyJson.Node nodeElection = node3.Get("election");
					if (nodeElection != null && !nodeElection.IsNull)
					{
						kingdomPolitics.Election = new ElectionState
						{
							TermsServed = nodeElection.GetInt("terms", 1),
							NextElectionYear = nodeElection.GetInt("next", world.Year + ElectionSystem.TermYears),
							LastElectionYear = nodeElection.GetInt("last", 0)
						};
						TinyJson.Node nodePast = nodeElection.Get("past");
						if (nodePast != null && !nodePast.IsNull)
						{
							for (int pIdx = 0; pIdx < nodePast.Arr.Count; pIdx++)
							{
								TinyJson.Node pNode = nodePast.Arr[pIdx];
								kingdomPolitics.Election.PastPresidents.Add(new PastPresidentRecord
								{
									ActorId = pNode.GetInt("id", 0),
									Name = pNode.GetString("name", string.Empty),
									TermsCount = pNode.GetInt("terms", 1)
								});
							}
						}
					}
					// Этап 26 (v14): конституции; в мирах до v14 их не было — дефолт рождается
					// кнопкой при первом тике (EnsureConstitution в PoliticalWorld).
					TinyJson.Node nodeCnst = node3.Get("cnst");
					if (nodeCnst != null && !nodeCnst.IsNull)
					{
						kingdomPolitics.Constitution = new Constitution
						{
							StateForm = (HeadOfState)nodeCnst.GetInt("sf", (int)HeadOfState.Hereditary),
							Franchise = (Franchise)nodeCnst.GetInt("fr", (int)Franchise.Clan),
							Borders = (BorderLaw)nodeCnst.GetInt("bl", (int)BorderLaw.Open),
							Succession = (SuccessionRule)nodeCnst.GetInt("sc", (int)SuccessionRule.AnyAdult),
							Supremacy = (SupremacyRule)nodeCnst.GetInt("sp", (int)SupremacyRule.Advisory),
							Taxes = (TaxLaw)nodeCnst.GetInt("tx", (int)TaxLaw.Moderate),
							Trade = (TradeLaw)nodeCnst.GetInt("tr", (int)TradeLaw.Moderate),
							Faith = (FaithLaw)nodeCnst.GetInt("fh", (int)FaithLaw.Tolerance),
							Levy = (LevyLaw)nodeCnst.GetInt("lv", (int)LevyLaw.Conscription),
							Land = (LandLaw)nodeCnst.GetInt("ld", (int)LandLaw.Expansion),
							Watch = (WatchLaw)nodeCnst.GetInt("wt", (int)WatchLaw.Liberty),
							Blood = (BloodLaw)nodeCnst.GetInt("bd", (int)BloodLaw.Current),
							Impeachment = (ImpeachmentLaw)nodeCnst.GetInt("im", (int)ImpeachmentLaw.Forbidden),
							Union = (UnionLaw)nodeCnst.GetInt("un", (int)UnionLaw.All),
							// 0.52: статьи о народах (в старых сейвах их нет — берём терпимые).
							Race = (RaceLaw)nodeCnst.GetInt("rc", (int)RaceLaw.Equal),
							Language = (LanguageLaw)nodeCnst.GetInt("lg", (int)LanguageLaw.Free),
							Court = (CourtLaw)nodeCnst.GetInt("ct", (int)CourtLaw.Own),
							AllowLocalConstitutions = nodeCnst.GetBool("alc", true),
							TolerateLocalContradictions = nodeCnst.GetBool("tlc", true),
							ForeignRulerAllowed = nodeCnst.GetBool("fra", true),
							// 0.53: палаты. В старых сейвах — как велит строй.
							Chambers = (ChamberCount)nodeCnst.GetInt("chm", (int)ChamberCount.One),
							UpperBasis = (UpperChamberBasis)nodeCnst.GetInt("ub", (int)UpperChamberBasis.Cities),
							RulerVeto = nodeCnst.GetBool("rvt", true),
							RulerMayDissolve = nodeCnst.GetBool("rds", false)
						};
						if (electoralChapters.TryGetValue(num, out var chapter26)) kingdomPolitics.Constitution.Electoral = chapter26;
                        TinyJson.Node nodeHist = nodeCnst.Get("hist");
						if (nodeHist != null && !nodeHist.IsNull)
						{
							for (int h = 0; h < nodeHist.Arr.Count; h++)
							{
								TinyJson.Node hNode = nodeHist.Arr[h];
								kingdomPolitics.ConstitutionHistory.Add(new ConstitutionEdit
								{
									Year = hNode.GetInt("y", 0),
									Article = hNode.GetString("a", string.Empty),
									From = hNode.GetString("from", string.Empty),
									To = hNode.GetString("to", string.Empty),
									EditorName = hNode.GetString("who", string.Empty)
								});
							}
						}
						TinyJson.Node nodeWauth = nodeCnst.Get("wauth");
						if (nodeWauth != null && !nodeWauth.IsNull)
						{
							for (int w = 0; w < nodeWauth.Arr.Count; w++)
							{
								TinyJson.Node wNode = nodeWauth.Arr[w];
								kingdomPolitics.WarAuthorizations.Add(new WarAuthorization
								{
									TargetKingdomId = wNode.GetInt("t", -1),
									Year = wNode.GetInt("y", 0),
									ValidUntilYear = wNode.GetInt("until", 0),
									VotesFor = wNode.GetInt("vf", 0),
									VotesAgainst = wNode.GetInt("va", 0)
								});
							}
						}
						TinyJson.Node nodeRcd = nodeCnst.Get("rcd");
						if (nodeRcd != null && !nodeRcd.IsNull)
						{
							for (int r = 0; r < nodeRcd.Arr.Count; r++)
							{
								TinyJson.Node rNode = nodeRcd.Arr[r];
								kingdomPolitics.ReferendumCooldowns[rNode.GetInt("t", -1)] = rNode.GetInt("until", 0);
							}
						}
					}
					kingdomPolitics.LastConstitutionalUsurpYear = node3.GetInt("lcu", -100);
					kingdomPolitics.LastAmendmentYear = node3.GetInt("lam", -100);
					kingdomPolitics.TitularRaceId = node3.GetString("race", string.Empty);
					// Гражданские войны (формат v5). В сохранениях v1..v4 узла нет —
					// значения по умолчанию оставляют государство «не воевавшим».
					kingdomPolitics.CivilWarsTotal = node3.GetInt("cwTotal", 0);
					kingdomPolitics.LastCivilWarYear = node3.GetInt("cwLast", 0);
					TinyJson.Node nodeCw = node3.Get("cw");
					if (nodeCw != null && !nodeCw.IsNull)
					{
						CivilWarState cw = new CivilWarState
						{
							Cause = (CivilWarCause)nodeCw.GetInt("cause", (int)CivilWarCause.Tyranny),
							StartedYear = nodeCw.GetInt("start", 0),
							RebelIdeology = (IdeologyType)nodeCw.GetInt("ideology", (int)kingdomPolitics.Ideology),
							RebelLeaderName = nodeCw.GetString("leader", string.Empty),
							RebelLeaderIsFemale = nodeCw.GetBool("leaderFem", false),
							RebelLeaderActorId = nodeCw.GetInt("leaderId", 0),
							RebelCapitalCityId = nodeCw.GetInt("capital", 0),
							ParentCapitalCityId = nodeCw.GetInt("parentCapital", 0),
							RebelKingdomId = nodeCw.GetInt("rebelKingdom", 0)
						};
						TinyJson.Node cwCities = nodeCw.Get("cities");
						if (cwCities != null && !cwCities.IsNull)
						{
							for (int cwIdx = 0; cwIdx < cwCities.Arr.Count; cwIdx++)
							{
								cw.RebelCityIds.Add((int)cwCities.Arr[cwIdx].Num);
							}
						}
						kingdomPolitics.CivilWar = cw;
					}
					// Экономика (формат v6). В v1..v5 узла нет — держим значения по умолчанию.
					TinyJson.Node nodeEco = node3.Get("eco");
					if (nodeEco != null && !nodeEco.IsNull)
					{
						kingdomPolitics.Economy = new EconomyState
						{
							Prosperity = nodeEco.GetFloat("prosperity", 50f),
							TaxRate = nodeEco.GetFloat("tax", 1f),
							Tariff = nodeEco.GetFloat("tariff", 0f),
							DeficitYears = nodeEco.GetInt("deficit", 0),
							Crisis = (EconomicCrisisType)nodeEco.GetInt("crisis", 0),
							CrisisUntilYear = nodeEco.GetInt("crisisUntil", 0),
							LastCrisisYear = nodeEco.GetInt("lastCrisis", 0),
							CrisesTotal = nodeEco.GetInt("crises", 0),
							BoomsTotal = nodeEco.GetInt("booms", 0),
							LastBoomYear = nodeEco.GetInt("lastBoom", 0),
							Austerity = nodeEco.GetBool("austerity", false),
							AusterityUntilYear = nodeEco.GetInt("austerityUntil", 0),
							EmbargoTargetId = nodeEco.GetInt("embargo", 0),
							EmbargoUntilYear = nodeEco.GetInt("embargoUntil", 0),
							TradeShare = nodeEco.GetFloat("tradeShare", 0f),
							LastIncome = nodeEco.GetFloat("income", 0f),
							LastExpenses = nodeEco.GetFloat("expenses", 0f)
						};
					}
					TinyJson.Node node4 = node3.Get("rev");
					if (node4 != null && !node4.IsNull)
					{
						kingdomPolitics.Revolution = new RevolutionState
						{
							Target = (IdeologyType)node4.GetInt("t"),
							TargetStance = (ReformStance)node4.GetInt("s"),
							StartedYear = node4.GetInt("y"),
							YearsLeft = node4.GetInt("left", 1)
						};
					}
					TinyJson.Node nodeCamp = node3.Get("camp");
					if (nodeCamp != null && !nodeCamp.IsNull)
					{
						StrategicCampaignPlan camp = new StrategicCampaignPlan
						{
							Goal = (StrategicGoalType)nodeCamp.GetInt("g", 0),
							TargetKingdomId = nodeCamp.GetInt("tk", 0),
							TargetKingdomName = nodeCamp.GetString("tn", string.Empty),
							SubjectName = nodeCamp.GetString("sub", string.Empty),
							StartedYear = nodeCamp.GetInt("sy", 0),
							DeadlineYear = nodeCamp.GetInt("dy", 0),
							CurrentStepIndex = nodeCamp.GetInt("idx", 0),
							CompletedSteps = nodeCamp.GetInt("done", 0),
							ReasonKey = nodeCamp.GetString("rk", string.Empty)
						};
						TinyJson.Node campSteps = nodeCamp.Get("steps");
						if (campSteps != null && !campSteps.IsNull)
						{
							for (int st = 0; st < campSteps.Arr.Count; st++)
							{
								camp.Steps.Add((KingdomActionType)(int)campSteps.Arr[st].Num);
							}
						}
						kingdomPolitics.ActiveCampaign = camp;
					}
					kingdomPolitics.LastCivicMovementYear = node3.GetInt("lcm", -100);
					TinyJson.Node nodeMov = node3.Get("mov");
					if (nodeMov != null && !nodeMov.IsNull)
					{
						kingdomPolitics.ActiveMovement = new CivicMovement
						{
							Demand = (CivicDemandType)nodeMov.GetInt("d", 0),
							Stage = (CivicMovementStage)nodeMov.GetInt("st", 0),
							StartedYear = nodeMov.GetInt("sy", 0),
							SupportShare = nodeMov.GetFloat("sh", 0f),
							LeaderName = nodeMov.GetString("ln", string.Empty)
						};
					}
					TinyJson.Node nodeParties = node3.Get("parties");
					if (nodeParties != null && !nodeParties.IsNull)
					{
						for (int pIdx = 0; pIdx < nodeParties.Arr.Count; pIdx++)
						{
							TinyJson.Node pn = nodeParties.Arr[pIdx];
							kingdomPolitics.Parties.Add(new PoliticalParty
							{
								Ideology = (IdeologyType)pn.GetInt("i", 0),
								NameKey = pn.GetString("nk", string.Empty),
								Name = pn.GetString("nm", string.Empty),
								SeatsPercent = pn.GetInt("sp", 0),
								IsRuling = pn.GetBool("r", false)
							});
						}
					}
					// 0.49: восстановление национализма.
					TinyJson.Node nodeNat = node3.Get("nat");
					if (nodeNat != null && !nodeNat.IsNull)
					{
						NationalismState movement = world.NationalismOf(kingdomPolitics.KingdomId);
						if (movement != null)
						{
							movement.OriginKingdomId = nodeNat.GetInt("ok", 0);
							movement.OriginName = nodeNat.GetString("on", string.Empty);
							movement.OriginYear = nodeNat.GetInt("oy", 0);
							movement.Bond = (HomesteadBond)nodeNat.GetInt("bd", 0);
							movement.Pressure = nodeNat.GetFloat("pr", 0f);
							movement.PrevIdeology = nodeNat.GetInt("pi", -1);
							movement.SeizedYear = nodeNat.GetInt("sy", 0);
							movement.EntryBanUntilYear = nodeNat.GetInt("eb", 0);
							movement.ShadowUntilYear = nodeNat.GetInt("sh", 0);
							movement.SuppressedYear = nodeNat.GetInt("sup", 0);
							movement.Victories = nodeNat.GetInt("vic", 0);
						}
					}
					// 0.53: восстановление законопроекта.
					TinyJson.Node nodeBill = node3.Get("bill");
					if (nodeBill != null && !nodeBill.IsNull)
					{
						Bill bill = new Bill
						{
							KingdomId = kingdomPolitics.KingdomId,
							Article = nodeBill.GetString("a", string.Empty),
							From = nodeBill.GetString("f", string.Empty),
							To = nodeBill.GetString("t", string.Empty),
							Tightens = nodeBill.GetBool("ti", false),
							FromPeople = nodeBill.GetBool("pp", false),
							ProposedYear = nodeBill.GetInt("py", 0),
							Stage = (BillStage)nodeBill.GetInt("st", 0),
							LowerFor = nodeBill.GetInt("lf", 0),
							LowerAgainst = nodeBill.GetInt("la", 0),
							UpperFor = nodeBill.GetInt("uf", 0),
							UpperAgainst = nodeBill.GetInt("ua", 0),
							ConferenceYear = nodeBill.GetInt("cy", 0),
							EditorName = nodeBill.GetString("ed", string.Empty),
							ProposerIdeology = nodeBill.GetInt("pi", -1) < 0 ? null : (IdeologyType?)nodeBill.GetInt("pi", -1)
						};
						world.SetBill(bill);
					}
					// 0.52: восстановление народов державы.
					TinyJson.Node nodePeoples = node3.Get("peo");
					if (nodePeoples != null && !nodePeoples.IsNull)
					{
						Dictionary<string, PeopleRecord> book = world.PeoplesOf(kingdomPolitics.KingdomId);
						if (book != null)
						{
							for (int pIdx = 0; pIdx < nodePeoples.Arr.Count; pIdx++)
							{
								TinyJson.Node pn = nodePeoples.Arr[pIdx];
								string raceId = pn.GetString("r", string.Empty);
								if (string.IsNullOrEmpty(raceId))
								{
									continue;
								}
								book[raceId] = new PeopleRecord
								{
									RaceId = raceId,
									RaceName = pn.GetString("n", string.Empty),
									FirstSeenYear = pn.GetInt("fy", 0),
									Stage = (LanguageStage)pn.GetInt("st", 0),
									StageSinceYear = pn.GetInt("sy", 0),
									RequestedYear = pn.GetInt("ry", 0)
								};
							}
						}
					}
					// 0.49.1: восстановление расового движения.
					TinyJson.Node nodeRace = node3.Get("rnat");
					if (nodeRace != null && !nodeRace.IsNull)
					{
						RaceNationalismState race = world.RaceMovementOf(kingdomPolitics.KingdomId);
						if (race != null)
						{
							race.RaceId = nodeRace.GetString("rid", string.Empty);
							race.RaceName = nodeRace.GetString("rn", string.Empty);
							race.Share = nodeRace.GetFloat("sh", 0f);
							race.Pressure = nodeRace.GetFloat("pr", 0f);
							race.PrevIdeology = nodeRace.GetInt("pi", -1);
							race.PrevTitularRace = nodeRace.GetString("ptr", string.Empty);
							race.SeizedYear = nodeRace.GetInt("sy", 0);
							race.EntryBanUntilYear = nodeRace.GetInt("eb", 0);
							race.ShadowUntilYear = nodeRace.GetInt("shy", 0);
							race.Victories = nodeRace.GetInt("vic", 0);
						}
					}
					kingdomPolitics.HasTribalEthnonym = node3.GetBool("trEth", false);
					kingdomPolitics.TribalAwakeningAnnounced = node3.GetBool("trAwk", false);
					kingdomPolitics.WasCivilized = node3.GetBool("trCiv", false);
					kingdomPolitics.LastCivilizedYear = node3.GetInt("trCivY", 0);
				}
			}
		}
		// 0.52: восстановление местных конституций.
		TinyJson.Node nodeLocals = node.Get("locals");
		if (nodeLocals != null && !nodeLocals.IsNull)
		{
			for (int lIdx = 0; lIdx < nodeLocals.Arr.Count; lIdx++)
			{
				TinyJson.Node ln = nodeLocals.Arr[lIdx];
				int cityId = ln.GetInt("c", 0);
				if (cityId <= 0)
				{
					continue;
				}
				LocalConstitution local = world.AdoptLocalConstitution(
					cityId, ln.GetInt("k", 0), ln.GetString("r", string.Empty), ln.GetString("n", string.Empty));
				if (local == null)
				{
					continue;
				}
				local.FoundedYear = ln.GetInt("fy", 0);
				local.Banned = ln.GetBool("b", false);
				local.BannedYear = ln.GetInt("by", 0);
				local.WantsLanguage = ln.GetBool("wl", true);
				local.WantsCourt = ln.GetBool("wc", true);
				local.Grievance = ln.GetFloat("g", 0f);
			}
		}
		// 0.50: восстановление пятен.
		TinyJson.Node nodeStains = node.Get("stains");
		if (nodeStains != null && !nodeStains.IsNull)
		{
			for (int sIdx = 0; sIdx < nodeStains.Arr.Count; sIdx++)
			{
				TinyJson.Node sn = nodeStains.Arr[sIdx];
				world.NoteHateStain(sn.GetInt("k", 0), sn.GetInt("y", 0));
			}
		}
		// 0.49.1: восстановление запретов на гражданство и въезд.
		TinyJson.Node nodeBans = node.Get("cbans");
		if (nodeBans != null && !nodeBans.IsNull)
		{
			for (int b = 0; b < nodeBans.Arr.Count; b++)
			{
				TinyJson.Node bn = nodeBans.Arr[b];
				world.AddCitizenshipBan(
					bn.GetInt("k", 0),
					bn.GetString("rid", string.Empty),
					bn.GetString("rn", string.Empty),
					bn.GetInt("fy", 0),
					bn.GetInt("uy", 0),
					bn.GetBool("rv", false));
			}
		}
		TinyJson.Node node5 = node.Get("cities");
		if (node5 != null)
		{
			for (int j = 0; j < node5.Arr.Count; j++)
			{
				TinyJson.Node node6 = node5.Arr[j];
				world.AddCity(node6.GetInt("id"), node6.GetInt("k")).Dominant = (IdeologyType)node6.GetInt("i");
			}
		}
		TinyJson.Node node7 = node.Get("actors");
		if (node7 != null)
		{
			for (int k = 0; k < node7.Arr.Count; k++)
			{
				TinyJson.Node node8 = node7.Arr[k];
				ActorPolitics politics = new ActorPolitics((IdeologyType)node8.GetInt("i"), (ReformStance)node8.GetInt("s", 2))
				{
					Conviction = node8.GetFloat("v", 0.5f),
					Dissent = node8.GetFloat("d"),
					ConversionProgress = node8.GetFloat("p")
				};
				world.RegisterActor(node8.GetInt("id"), node8.GetInt("k"), node8.GetInt("c"), politics);
			}
		}
		TinyJson.Node node9 = node.Get("deaths");
		if (node9 != null)
		{
			for (int l = 0; l < node9.Arr.Count; l++)
			{
				TinyJson.Node node10 = node9.Arr[l];
				int num3 = node10.GetInt("killer", -1);
				int num4 = node10.GetInt("king", -1);
				DeathRegistry deaths = world.Deaths;
				int actorId = node10.GetInt("id");
				string actorName = node10.GetString("n");
				int cause = node10.GetInt("c");
				int year = node10.GetInt("y");
				int? killerId = ((num3 < 0) ? ((int?)null) : new int?(num3));
				string killerName = node10.GetString("kn");
				string killerTitle = node10.GetString("kt");
				int? kingdomId = ((num4 < 0) ? ((int?)null) : new int?(num4));
				string cityName = node10.GetString("city");
				bool wasRuler = node10.GetBool("r");
				bool wasChosen = node10.GetBool("ch");
				int age = node10.GetInt("a");
				string note = node10.GetString("note");
				bool wasFemale = node10.GetBool("fem", false);
				int? reignYears = (node10.Get("reign") != null && !node10.Get("reign").IsNull) ? new int?(node10.GetInt("reign")) : null;
				DeathRecord record = deaths.Record(actorId, actorName, (DeathCause)cause, year, killerId, killerName, killerTitle, null, kingdomId, cityName, wasRuler, wasChosen, age, note, wasFemale, reignYears, notify: false);
				if (record != null)
				{
					record.PopulationBeforeDeath = node10.GetInt("pop_before", -1);
                    record.KingdomNameAtDeath = node10.GetString("king_name");
                    record.WasLastResident = node10.GetBool("last_resident", false);
                    record.LastResidentEvidenceActive = node10.GetBool("last_active", false);
                    world.ChronicleCauses.Restore(record);
                    record.KillerIsCiv = node10.GetBool("kc", false);
					record.KillerKingdom = node10.GetString("kking");
					record.KillerCity = node10.GetString("kcity");
					record.KillerJob = node10.GetString("kjob");
					record.KillerSpecies = node10.GetString("kspec");
					record.KillerRace = node10.GetString("krace");
					record.KillerFemale = node10.GetBool("kf", false);
					record.KillerAge = node10.GetInt("ka");
					// 0.23.12: исцеляем старые записи, где корабль записан зверем
					// («растерзан зверем», убийца boat_*): причина поднимается до корабельной.
					if (record.KillerIsBoat && record.Cause == DeathCause.AnimalAttack)
					{
						record.Cause = DeathCause.KilledByBoat;
					}
				}
			}
		}
		// Этап 25 (v13): реестр родословной. Старые сохранения (v12 и ниже) блока не имеют — реестр
		// заполнится снова от живых глаз мира на первом же годовом снимке, увы без прародителей.
		TinyJson.Node nodeFam = node.Get("family");
		if (nodeFam != null)
		{
			List<FamilyRecord> family = new List<FamilyRecord>();
			for (int nf = 0; nf < nodeFam.Arr.Count; nf++)
			{
				TinyJson.Node fn = nodeFam.Arr[nf];
				FamilyRecord rec = new FamilyRecord
				{
					Id = fn.GetLong("id"),
					Name = fn.GetString("n"),
					Female = fn.GetBool("f", false),
					BirthYear = fn.GetInt("by"),
					FatherId = fn.GetLong("fa"),
					MotherId = fn.GetLong("mo"),
					ClanName = fn.GetString("cl"),
					ClanId = fn.GetLong("cid"),
					KingdomId = fn.GetInt("k"),
					LastSeenYear = fn.GetInt("seen")
				};
				if (fn.Get("dy") != null && !fn.Get("dy").IsNull)
				{
					rec.DeathYear = fn.GetInt("dy");
				}
				family.Add(rec);
			}
			world.Family.Load(family);
		}
		TinyJson.Node node11 = node.Get("unions");
		if (node11 != null)
		{
			for (int m = 0; m < node11.Arr.Count; m++)
			{
				TinyJson.Node node12 = node11.Arr[m];
				int num5 = node12.GetInt("leader");
				Union union = new Union
				{
					Id = node12.GetInt("id"),
					Type = (UnionType)node12.GetInt("t"),
					Name = node12.GetString("n"),
					NameKey = node12.GetString("nk"),
					LeaderId = num5,
					FormedYear = node12.GetInt("y"),
					Cohesion = node12.GetFloat("c", 0.7f),
					LeaderWarriors = node12.GetInt("w")
				};
				KingdomPolitics kingdom = world.GetKingdom(num5);
				if (kingdom != null)
				{
					union.SetLeaderIdeology(kingdom.Ideology);
				}
				TinyJson.Node node13 = node12.Get("m");
				if (node13 != null)
				{
					for (int n = 0; n < node13.Arr.Count; n++)
					{
						TinyJson.Node node14 = node13.Arr[n];
						bool detailed = node14.Obj.Count > 0;
						int num6 = detailed ? node14.GetInt("id") : (int)node14.Num;
						union.Members.Add(new UnionMember
						{
							KingdomId = num6,
							IsLeader = (num6 == num5),
							Approval = detailed ? node14.GetFloat("a", 0.6f) : 0.6f,
							JoinedYear = detailed ? node14.GetInt("j", union.FormedYear) : union.FormedYear
						});
					}
				}
				// 0.24: поглощённые державы и снятые границы для «права на выход».
				TinyJson.Node nodeAbs = node12.Get("abs");
				if (nodeAbs != null)
				{
					for (int n = 0; n < nodeAbs.Arr.Count; n++)
					{
						TinyJson.Node nodeChild = nodeAbs.Arr[n];
						AbsorbedChild child = new AbsorbedChild
						{
							KingdomId = nodeChild.GetInt("id"),
							CapitalCityId = nodeChild.GetInt("cap"),
							AbsorbedYear = nodeChild.GetInt("y")
						};
						TinyJson.Node nodeCities = nodeChild.Get("cities");
						if (nodeCities != null)
						{
							for (int c = 0; c < nodeCities.Arr.Count; c++)
							{
								child.CityIds.Add((int)nodeCities.Arr[c].Num);
							}
						}
						union.RecordAbsorption(child);
					}
				}
				TinyJson.Node nodeVb = node12.Get("vb");
				if (nodeVb != null)
				{
					for (int n = 0; n < nodeVb.Arr.Count; n++)
					{
						TinyJson.Node nodeMem = nodeVb.Arr[n];
						int memId = nodeMem.GetInt("id");
						UnionMember? mem = union.GetMember(memId);
						if (mem != null)
						{
							mem.CapitalCityId = nodeMem.GetInt("cap");
							mem.OriginalName = nodeMem.GetString("n", string.Empty);
							mem.OriginalIdeology = (IdeologyType)nodeMem.GetInt("i", (int)IdeologyType.Monarchy);
							TinyJson.Node nodeCities = nodeMem.Get("cities");
							if (nodeCities != null)
							{
								for (int c = 0; c < nodeCities.Arr.Count; c++)
								{
									mem.CityIds.Add((int)nodeCities.Arr[c].Num);
								}
							}
						}
					}
				}
				if (union.MemberCount > 0)
				{
					world.RestoreUnion(union);
					list.Add(union);
				}
			}
		}
		// Торговые пути (формат v6): восстанавливаем сеть целиком, включая перекрытые.
		// Этап 19 (v8): отношения правителей; в старых мирах отсутствуют — все пары начинают с нуля.
		TinyJson.Node nodeRels = node.Get("rels");
		if (nodeRels != null)
		{
			for (int m = 0; m < nodeRels.Arr.Count; m++)
			{
				TinyJson.Node rel = nodeRels.Arr[m];
				world.SetRulerRelation(rel.GetInt("a"), rel.GetInt("b"), rel.GetInt("v"));
			}
		}
		// Этап 20 (v9): договоры, обиды, коалиции и история войн; в старых мирах отсутствуют.
		TinyJson.Node nodeTreaties = node.Get("treaties");
		if (nodeTreaties != null)
		{
			for (int m = 0; m < nodeTreaties.Arr.Count; m++)
			{
				TinyJson.Node tr = nodeTreaties.Arr[m];
				world.TreatiesBook.Add(new Treaty
				{
					Type = (TreatyType)tr.GetInt("t", 1),
					PartyA = tr.GetInt("a"),
					PartyB = tr.GetInt("b"),
					SignedYear = tr.GetInt("s"),
					ExpiresYear = tr.GetInt("e")
				});
			}
		}
		TinyJson.Node nodeGriev = node.Get("griev");
		if (nodeGriev != null)
		{
			for (int m = 0; m < nodeGriev.Arr.Count; m++)
			{
				TinyJson.Node gr = nodeGriev.Arr[m];
				world.Grievances.Set(gr.GetInt("f"), gr.GetInt("o"), gr.GetFloat("v", 0f));
			}
		}
		TinyJson.Node nodeCoals = node.Get("coals");
		if (nodeCoals != null)
		{
			for (int m = 0; m < nodeCoals.Arr.Count; m++)
			{
				TinyJson.Node co = nodeCoals.Arr[m];
				Coalition coalition = new Coalition
				{
					Id = co.GetInt("i", 1),
					TargetId = co.GetInt("t"),
					FormedYear = co.GetInt("y"),
					LastWarYear = co.GetInt("w", co.GetInt("y"))
				};
				TinyJson.Node members = co.Get("m");
				if (members != null)
				{
					for (int k = 0; k < members.Arr.Count; k++)
					{
						coalition.Members.Add((int)members.Arr[k].Num);
					}
				}
				world.Coalitions.Restore(coalition);
			}
		}
		TinyJson.Node nodeWars = node.Get("wars");
		if (nodeWars != null)
		{
			for (int m = 0; m < nodeWars.Arr.Count; m++)
			{
				TinyJson.Node wd = nodeWars.Arr[m];
				world.WarDeclarationsAdd(new WarDeclarationRecord
				{
					AttackerId = wd.GetInt("a"),
					DefenderId = wd.GetInt("d"),
					Year = wd.GetInt("y"),
					CasusBelli = (CasusBelliType)wd.GetInt("c", 0)
				});
			}
		}
		// Этап 22 (v11): перемирия и репарации; в мирах v1–v10 отсутствуют.
		TinyJson.Node nodeTruces = node.Get("truces");
		if (nodeTruces != null)
		{
			for (int m = 0; m < nodeTruces.Arr.Count; m++)
			{
				TinyJson.Node tc = nodeTruces.Arr[m];
				world.TrucesBook.Set(tc.GetInt("a"), tc.GetInt("b"), tc.GetInt("u"));
			}
		}
		TinyJson.Node nodeKmud = node.Get("kmud");
		if (nodeKmud != null && world.Kmud != null)
		{
			world.Kmud.Phase = (KmudPhase)nodeKmud.GetInt("ph");
			world.Kmud.OpenedYear = nodeKmud.GetInt("oy");
			world.Kmud.ClosesYear = nodeKmud.GetInt("cy");
			world.Kmud.TruceUntilYear = nodeKmud.GetInt("tu");
			world.Kmud.Members = nodeKmud.GetInt("mem");
			world.Kmud.WarsPrevented = nodeKmud.GetInt("wp2");
			TinyJson.Node membersNode = nodeKmud.Get("mi");
			if (membersNode != null && membersNode.Arr != null)
			{
				for (int m = 0; m < membersNode.Arr.Count; m++)
				{
					world.Kmud.MemberIds.Add(membersNode.Arr[m].GetInt("k"));
				}
			}
			world.Kmud.WarsEnded = nodeKmud.GetInt("we");
			world.Kmud.WarsStoppedDuringPause = nodeKmud.GetInt("wp");
			world.Kmud.AllianceId = nodeKmud.GetInt("al");
			world.Kmud.AllianceFormed = nodeKmud.GetInt("af") != 0;
			world.Kmud.MembersReturned = nodeKmud.GetInt("mr");
			world.Kmud.ResultsAnnounced = nodeKmud.GetInt("res") != 0;
			world.Kmud.LastPeaceYears = nodeKmud.GetInt("py");
			TinyJson.Node agendaNode = nodeKmud.Get("ag");
			if (agendaNode != null && world.Kmud.Agenda != null)
			{
				world.Kmud.Agenda.AggressorId = agendaNode.GetInt("a");
				world.Kmud.Agenda.DefenderId = agendaNode.GetInt("d");
				world.Kmud.Agenda.ConflictYears = agendaNode.GetInt("cy");
				world.Kmud.Agenda.WorstId = agendaNode.GetInt("w");
				world.Kmud.Agenda.PoorestId = agendaNode.GetInt("p");
				world.Kmud.Agenda.RepatriationRaceId = agendaNode.GetString("rr", string.Empty);
				world.Kmud.Agenda.RepatriationRaceName = agendaNode.GetString("rn", string.Empty);
				world.Kmud.Agenda.RepatriationToId = agendaNode.GetInt("rt");
				world.Kmud.Agenda.RepatriationFromId = agendaNode.GetInt("rf");
			}
			world.Kmud.Round = nodeKmud.GetInt("rnd");
			TinyJson.Node decNode = nodeKmud.Get("dec");
			if (decNode != null && world.Kmud.Decisions != null)
			{
				world.Kmud.Decisions.Outcome = decNode.GetInt("o");
				world.Kmud.Decisions.PeaceYears = decNode.GetInt("py");
				world.Kmud.Decisions.Punishment = decNode.GetInt("pu");
				world.Kmud.Decisions.Aid = decNode.GetInt("ai");
				world.Kmud.Decisions.PunishedId = decNode.GetInt("pid");
				world.Kmud.Decisions.AidedId = decNode.GetInt("aid");
				world.Kmud.Decisions.PayerId = decNode.GetInt("pay");
				world.Kmud.Decisions.PayeeId = decNode.GetInt("rec");
				world.Kmud.Decisions.Applied = decNode.GetInt("ap") != 0;
				// 0.47: решение по пятому вопросу.
				world.Kmud.Decisions.Repatriation = decNode.GetInt("rp");
				world.Kmud.Decisions.RepatriationRaceId = decNode.GetString("rr", string.Empty);
				world.Kmud.Decisions.RepatriationToId = decNode.GetInt("rt");
				world.Kmud.Decisions.RepatriatedCount = decNode.GetInt("rc");
			}
			TinyJson.Node pactsNode = nodeKmud.Get("pacts");
			if (pactsNode != null && pactsNode.Arr != null)
			{
				for (int p = 0; p < pactsNode.Arr.Count; p++)
				{
					TinyJson.Node one = pactsNode.Arr[p];
					world.Kmud.Pacts.Add(new KmudPact
					{
						A = one.GetInt("a"),
						B = one.GetInt("b"),
						UntilYear = one.GetInt("u"),
						AllianceFormed = one.GetInt("al") != 0
					});
				}
			}
			TinyJson.Node sanctNode = nodeKmud.Get("sanct");
			if (sanctNode != null && sanctNode.Arr != null)
			{
				for (int p = 0; p < sanctNode.Arr.Count; p++)
				{
					TinyJson.Node one = sanctNode.Arr[p];
					world.Kmud.Sanctions.Add(new KmudSanction
					{
						KingdomId = one.GetInt("k"),
						UntilYear = one.GetInt("u"),
						Supporters = one.GetInt("s")
					});
				}
			}
			TinyJson.Node assocNode = nodeKmud.Get("assoc");
			if (assocNode != null && assocNode.Arr != null)
			{
				for (int p = 0; p < assocNode.Arr.Count; p++)
				{
					TinyJson.Node one = assocNode.Arr[p];
					world.Kmud.Associations.Add(new KmudAssociation
					{
						DonorId = one.GetInt("d"),
						RecipientId = one.GetInt("r"),
						UntilYear = one.GetInt("u"),
						GoldPerYear = (float)one.GetInt("g"),
						FoodPerYear = one.GetInt("f")
					});
				}
			}
			TinyJson.Node balNode = nodeKmud.Get("bal");
			if (balNode != null && balNode.Arr != null)
			{
				for (int b = 0; b < balNode.Arr.Count; b++)
				{
					TinyJson.Node one = balNode.Arr[b];
					KmudQuestion question = (KmudQuestion)one.GetInt("q");
					if (!world.Kmud.Voting.Ballots.TryGetValue(question, out KmudBallotResult ballot) || ballot == null)
					{
						continue;
					}
					ballot.Voters = one.GetInt("v");
					ballot.Winner = one.GetInt("w");
					TinyJson.Node countsNode = one.Get("c");
					if (countsNode != null && countsNode.Arr != null)
					{
						for (int c = 0; c < countsNode.Arr.Count; c++)
						{
							TinyJson.Node pair = countsNode.Arr[c];
							ballot.Counts[pair.GetInt("o")] = pair.GetInt("n");
						}
					}
				}
			}
		}
		TinyJson.Node nodeReps = node.Get("reps");
		if (nodeReps != null)
		{
			for (int m = 0; m < nodeReps.Arr.Count; m++)
			{
				TinyJson.Node rp = nodeReps.Arr[m];
				world.AddReparations(new ReparationsOrder
				{
					PayerId = rp.GetInt("p"),
					PayeeId = rp.GetInt("t"),
					AmountPerYear = rp.GetFloat("a", 12f),
					YearsLeft = rp.GetInt("y", 1)
				});
			}
		}
		TinyJson.Node nodePrewar = node.Get("prewar");
		if (nodePrewar != null)
		{
			for (int m = 0; m < nodePrewar.Arr.Count; m++)
			{
				TinyJson.Node pw = nodePrewar.Arr[m];
				world.RememberPrewarCities(pw.GetInt("k"), IntList(pw.Get("c")));
			}
		}
		TinyJson.Node nodeRestore = node.Get("restore");
		if (nodeRestore != null)
		{
			for (int m = 0; m < nodeRestore.Arr.Count; m++)
			{
				TinyJson.Node ro = nodeRestore.Arr[m];
				world.AddPendingRestoration(new RestorationOrder
				{
					DefeatedId = ro.GetInt("d"),
					VictorId = ro.GetInt("v"),
					Mode = (RestorationMode)ro.GetInt("m", 0),
					CitiesKeptByVictor = ro.GetInt("keep", 0),
					DefeatedName = ro.GetString("n"),
					Year = ro.GetInt("y"),
					IsNationalLiberation = ro.GetBool("lib", false),
					LiberationPeaceful = ro.GetBool("lp", false),
					PrewarCityIds = IntList(ro.Get("c"))
				});
			}
		}
		// Этап 23 (v12): границы; в мирах v1–v11 отсутствуют — по умолчанию все границы открыты.
		TinyJson.Node nodeBorders = node.Get("borders");
		if (nodeBorders != null)
		{
			for (int m = 0; m < nodeBorders.Arr.Count; m++)
			{
				TinyJson.Node br = nodeBorders.Arr[m];
				int ba = br.GetInt("a");
				int bb = br.GetInt("b");
				world.BordersBook.SetStatus(ba, bb, (BorderStatus)br.GetInt("s"), br.GetInt("u"));
				int lockUntil = br.GetInt("l");
				if (lockUntil > 0)
				{
					world.BordersBook.Lock(ba, bb, 0, lockUntil);
				}
			}
		}
		// Этап 21 (v10): ванильные альянсы игры; в старых мирах отсутствуют.
        if(allianceSnapshot!=null)
        {
            world.AlliancesBook.Replace(allianceSnapshot);
            world.MarkAllianceBaseline();
        }
		TinyJson.Node nodeRoutes = node.Get("routes");
		if (nodeRoutes != null)
		{
			for (int r = 0; r < nodeRoutes.Arr.Count; r++)
			{
				TinyJson.Node nodeRoute = nodeRoutes.Arr[r];
				TradeRoute route = new TradeRoute
				{
					Id = nodeRoute.GetInt("id", 0),
					FromKingdomId = nodeRoute.GetInt("from", 0),
					ToKingdomId = nodeRoute.GetInt("to", 0),
					EstablishedYear = nodeRoute.GetInt("year", 0),
					Level = nodeRoute.GetInt("level", 1),
					Volume = nodeRoute.GetFloat("vol", 10f),
					Blocked = nodeRoute.GetBool("blocked", false)
				};
				if (route.FromKingdomId != route.ToKingdomId && world.GetKingdom(route.FromKingdomId) != null
					&& world.GetKingdom(route.ToKingdomId) != null)
				{
					world.Trade.Restore(route);
				}
			}
		}
		foreach (KingdomPolitics k in world.Kingdoms)
		{
			k.Union = world.UnionOf(k.KingdomId);
		}
		TinyJson.Node nodeDipMem = node.Get("dipmem");
		if (nodeDipMem != null)
		{
			for (int dmIdx = 0; dmIdx < nodeDipMem.Arr.Count; dmIdx++)
			{
				TinyJson.Node dm = nodeDipMem.Arr[dmIdx];
				world.DiplomaticMemories.Restore(new DiplomaticMemoryEntry
				{
					FromKingdomId = dm.GetInt("f", 0),
					TowardsKingdomId = dm.GetInt("t", 0),
					Kind = (DiplomaticMemoryKind)dm.GetInt("k", 0),
					Year = dm.GetInt("y", 0),
					ExpiresYear = dm.GetInt("e", 0),
					TrustDelta = dm.GetInt("d", 0),
					SubjectName = dm.GetString("s", string.Empty)
				});
			}
		}
		TinyJson.Node nodeHomelands = node.Get("homelands");
		if (nodeHomelands != null)
		{
			for (int hlIdx = 0; hlIdx < nodeHomelands.Arr.Count; hlIdx++)
			{
				TinyJson.Node hl = nodeHomelands.Arr[hlIdx];
				world.CityHomelands.Restore(new CityHomelandRecord
				{
					CityId = hl.GetInt("c", 0),
					OriginalKingdomId = hl.GetInt("ok", 0),
					OriginalKingdomName = hl.GetString("on", string.Empty),
					OriginalIdeology = (IdeologyType)hl.GetInt("oi", (int)IdeologyType.Monarchy),
					WasHistoricalCapital = hl.GetBool("cap", false),
					AnnexedYear = hl.GetInt("ay", 0)
				});
			}
		}
		world.ClearEvents();
		world.SetBaseline(node.GetBool("baseline", fallback: true));
		world.SetYear(node.GetInt("year"));
		return list;
	}
}
