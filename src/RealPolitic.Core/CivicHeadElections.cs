using System;
using System.Collections.Generic;

namespace RealPolitic.Core;

public sealed class CivicResident
{
    public CivicPerson Person;
    public CivicBelief Belief;
    public string Name="";
    public bool IsFemale;
    public long ClanId;
    public string ClanName="";
}
public sealed class CivicMandates
{
    public int ActorId;
    public int Lifetime;
    public int Consecutive;
}
public sealed class CivicOfficeState
{
    public int CountryId;
    public int HolderId;
    public int SinceYear;
    public int NextElectionYear;
    public int LastAttemptYear=-1;
    public string LastCommittedProcedure="";
    public readonly Dictionary<int,CivicMandates> Mandates=new Dictionary<int,CivicMandates>();
    public void Validate()
    {
        if(CountryId<=0 || HolderId<0 || SinceYear<0 || NextElectionYear<0 || LastAttemptYear< -1 || LastCommittedProcedure==null)
            throw new ArgumentException("Invalid elected office.");
        foreach(var p in Mandates)if(p.Key<=0 || p.Value==null || p.Value.ActorId!=p.Key || p.Value.Lifetime<0
            || p.Value.Consecutive<0 || p.Value.Consecutive>p.Value.Lifetime || p.Key!=HolderId && p.Value.Consecutive!=0)
            throw new ArgumentException("Invalid mandate history.");
        if(HolderId>0 && Array.IndexOf(new[]{5,10,15,20},NextElectionYear-SinceYear)<0)throw new ArgumentException("Invalid mandate duration.");
        if(HolderId>0 && (!Mandates.TryGetValue(HolderId,out var holder) || holder.Lifetime<1 || holder.Consecutive<1))
            throw new ArgumentException("Elected holder has no mandate.");
    }
    public void Commit(int actorId,int year,int termYears,string procedureId)
    {
        if(actorId<=0 || year<0 || Array.IndexOf(new[]{5,10,15,20},termYears)<0 || string.IsNullOrWhiteSpace(procedureId) || (long)year+termYears>int.MaxValue)
            throw new ArgumentException("Invalid mandate.");
        if(LastCommittedProcedure==procedureId)return;
        foreach(var p in Mandates.Values)if(p.ActorId!=actorId)p.Consecutive=0;
        if(!Mandates.TryGetValue(actorId,out var record))Mandates.Add(actorId,record=new CivicMandates{ActorId=actorId});
        record.Lifetime=checked(record.Lifetime+1);record.Consecutive=checked(record.Consecutive+1);
        HolderId=actorId;SinceYear=year;NextElectionYear=checked(year+termYears);LastCommittedProcedure=procedureId;
    }
}
public interface ICivicHeadSource
{
    bool TryReadCitizens(int countryId,out IList<CivicResident> citizens);
    // Must verify the exact winner on the actual throne. False never counts as an executed election.
    bool TryInstallHead(int countryId,int actorId);
}

public static class CivicHeadElections
{
    public static bool IsElective(Constitution constitution)=>constitution!=null &&
        (constitution.StateForm==HeadOfState.Elected || constitution.StateForm==HeadOfState.Parliamentary || constitution.StateForm==HeadOfState.Assembly);

    private static double CandidateMerit(CivicResident candidate)
        =>(candidate.Person.IsCityLeader?0.10:0)+Math.Min(candidate.Person.Age,100)*0.001;
    private static double Score(CivicResident voter,CivicResident candidate,int incumbent)
        =>Ideologies.Affinity(voter.Belief.Ideology,candidate.Belief.Ideology)*(0.5+0.5*voter.Belief.Conviction)
            +CandidateMerit(candidate)+(voter.Person.IsRulingClan && candidate.Person.IsRulingClan?0.03:0)
            +(candidate.Person.ActorId==voter.Person.ActorId?0.02:0)
            -(candidate.Person.ActorId==incumbent?voter.Belief.Dissent*0.30:0);
    public static int Choose(CivicResident voter,IList<CivicResident> candidates,int incumbent)
    {
        if(voter?.Person==null || voter.Belief==null || candidates==null)throw new ArgumentException("Invalid election profiles.");
        voter.Belief.Validate();foreach(var c in candidates){if(c?.Person==null || c.Belief==null)throw new ArgumentException("Invalid candidate.");c.Belief.Validate();}
        return ChooseFast(voter,candidates,incumbent,null);
    }
    private static int ChooseFast(CivicResident voter,IList<CivicResident> candidates,int incumbent,CivicResident extra)
    {
        double best=double.NegativeInfinity,second=double.NegativeInfinity;int winner=0;
        void Consider(CivicResident candidate)
        {
            double score=Score(voter,candidate,incumbent);
            if(score>best){second=best;best=score;winner=candidate.Person.ActorId;}
            else if(score>second)second=score;
        }
        foreach(var candidate in candidates)Consider(candidate);if(extra!=null)Consider(extra);
        return best-second<=0.000001 || best<=0?CivicBallot.Abstain:winner;
    }
    /// <summary>Exact model optimization, NOT a restricted candidate roll. Preserve two ties per preference class,
    /// the incumbent separately and each voter's own candidature. All legal candidate IDs remain in the ballot.</summary>
    private static List<CivicResident> PreferenceRepresentatives(IList<CivicResident> candidates,int incumbent)
    {
        var buckets=new Dictionary<int,List<CivicResident>>();var result=new List<CivicResident>();
        foreach(var candidate in candidates)
        {
            if(candidate.Person.ActorId==incumbent){result.Add(candidate);continue;}
            int key=(int)candidate.Belief.Ideology*2+(candidate.Person.IsRulingClan?1:0);
            if(!buckets.TryGetValue(key,out var bucket))buckets.Add(key,bucket=new List<CivicResident>());
            bucket.Add(candidate);bucket.Sort((a,b)=>CandidateMerit(b).CompareTo(CandidateMerit(a)));if(bucket.Count>2)bucket.RemoveAt(2);
        }
        foreach(var bucket in buckets.Values)result.AddRange(bucket);return result;
    }
    public static bool Tick(PoliticalWorld world,KingdomPolitics country,int year,ICivicHeadSource source)
    {
        if(world==null || country==null || source==null)throw new ArgumentNullException();
        if(!world.Governance.Enabled || country.Constitution?.Electoral==null)return false;
        if(!IsElective(country.Constitution))
        {
            foreach(var pending in world.Governance.Procedures.Values)
                if(pending.CountryId==country.KingdomId && pending.Kind==CivicProcedureKind.HeadElection
                    && (pending.Stage==CivicProcedureStage.Voting || pending.Stage==CivicProcedureStage.Approved || pending.Stage==CivicProcedureStage.ExecutionPending))
                    pending.Transition(pending.Stage==CivicProcedureStage.ExecutionPending?CivicProcedureStage.ExecutionFailed:CivicProcedureStage.Cancelled,"head-office-no-longer-elective");
            return false;
        }
        if(year<0 || (long)year+country.Constitution.Electoral.TermYears>int.MaxValue)throw new ArgumentOutOfRangeException(nameof(year));
        var law=country.Constitution.Electoral;law.Validate();
        var office=world.Governance.OfficeFor(country.KingdomId);
        CivicProcedure procedure=null;
        foreach(var p in world.Governance.Procedures.Values)
            if(p.Kind==CivicProcedureKind.HeadElection && p.CountryId==country.KingdomId
                && (p.Stage==CivicProcedureStage.Approved || p.Stage==CivicProcedureStage.ExecutionPending || p.Stage==CivicProcedureStage.Voting))
            {if(procedure!=null)throw new InvalidOperationException("Multiple active head elections.");procedure=p;}
        if(procedure==null && country.Ideology==IdeologyType.FreeTerritory && country.Ruler!=null && country.Ruler.ActorId>0)return false;
        if(procedure==null && office.HolderId>0 && country.Ruler?.ActorId==office.HolderId && year<office.NextElectionYear)return false;
        if(procedure==null && year<=office.LastAttemptYear)return false;
        if(!source.TryReadCitizens(country.KingdomId,out var citizens))return false;
        var profiles=new Dictionary<int,CivicResident>();
        foreach(var resident in citizens)
        {
            if(resident?.Person==null || resident.Belief==null || resident.Person.ActorId<=0 || resident.Person.Age<0
                || !resident.Person.Alive || !resident.Person.Citizen || profiles.ContainsKey(resident.Person.ActorId))
                throw new ArgumentException("Invalid complete citizen roll.");
            resident.Belief.Validate();profiles.Add(resident.Person.ActorId,resident);
        }
        bool unlimitedAtaman=country.Ideology==IdeologyType.FreeTerritory;
        if(procedure==null)
        {
            var candidates=new List<CivicResident>();var electors=new List<int>();
            foreach(var resident in profiles.Values)
            {
                if(law.MayVote(resident.Person))electors.Add(resident.Person.ActorId);
                office.Mandates.TryGetValue(resident.Person.ActorId,out var terms);
                if(law.MayStand(resident.Person,unlimitedAtaman?0:(terms?.Lifetime??0),unlimitedAtaman?0:(terms?.Consecutive??0)))candidates.Add(resident);
            }
            office.LastAttemptYear=year;
            if(candidates.Count==0 || electors.Count==0){PublishDeferred(world,country,year,"RP_GOV26_ELECTION_NO_CANDIDATES");return false;}
            candidates.Sort((a,b)=>a.Person.ActorId.CompareTo(b.Person.ActorId));
            var options=new List<int>();foreach(var candidate in candidates)options.Add(candidate.Person.ActorId);
            string id="head-election:"+country.KingdomId+":"+year;
            var ballot=CivicBallot.ForCandidates(id,year,electors,options);
            var representatives=PreferenceRepresentatives(candidates,office.HolderId);var representativeIds=new HashSet<int>();
            foreach(var c in representatives)representativeIds.Add(c.Person.ActorId);var eligibleCandidates=new HashSet<int>(options);
            ballot.Collect(year,actor=>ChooseFast(profiles[actor],representatives,office.HolderId,
                eligibleCandidates.Contains(actor) && !representativeIds.Contains(actor)?profiles[actor]:null));ballot.TryClose(year,out var tally);
            // 0.48.5: ВТОРОЙ ТУР. Заказ владельца 29.10.2026: «Второй тур нужен. Это сделает
            // выборы интереснее». Если в первом туре никто не собрал больше всех, жители
            // голосуют ещё раз — но только за тех, кто разделил первое место. Случайного
            // победителя по-прежнему никто не назначает.
            bool runoffHeld=false;
            if(tally.UniquePluralityWinner==0)
            {
                var second=Runoff(world,country,year,ballot,electors,profiles,office);
                if(second!=null){ballot=second;tally=ballot.Tally();runoffHeld=true;}
            }
            procedure=new CivicProcedure(ballot,CivicProcedureKind.HeadElection,country.KingdomId);world.Governance.AddProcedure(procedure);
            procedure.Transition(tally.UniquePluralityWinner>0?CivicProcedureStage.Approved:CivicProcedureStage.Rejected);
            if(procedure.Stage==CivicProcedureStage.Rejected){
                PublishDeferred(world,country,year,runoffHeld?"RP_GOV26_ELECTION_TIE_AFTER_RUNOFF":"RP_GOV26_ELECTION_TIE");
                return false;}
        }
        if(procedure.Stage==CivicProcedureStage.Voting)
        {
            if(!procedure.Ballot.Closed && !procedure.Ballot.TryClose(year,out _))return false;
            procedure.Transition(procedure.Ballot.Tally().UniquePluralityWinner>0?CivicProcedureStage.Approved:CivicProcedureStage.Rejected);
            if(procedure.Stage==CivicProcedureStage.Rejected)return false;
        }
        int winner=procedure.Ballot.Tally().UniquePluralityWinner;
        if(procedure.Stage==CivicProcedureStage.Approved)procedure.Transition(CivicProcedureStage.ExecutionPending);
        office.Mandates.TryGetValue(winner,out var history);
        bool alreadyCommitted=office.LastCommittedProcedure==procedure.Ballot.Id;
        if(!profiles.TryGetValue(winner,out var elected) || !alreadyCommitted && !law.MayStand(elected.Person,unlimitedAtaman?0:(history?.Lifetime??0),unlimitedAtaman?0:(history?.Consecutive??0)))
        {procedure.Transition(CivicProcedureStage.ExecutionFailed,"winner-no-longer-eligible");PublishDeferred(world,country,year,"RP_GOV26_ELECTION_INVALID_WINNER");return false;}
        // An API refusal leaves the same winner pending; no random replacement and no extra mandate.
        if(!source.TryInstallHead(country.KingdomId,winner)){PublishDeferred(world,country,year,"RP_GOV26_ELECTION_API_REFUSAL");return false;}
        office.Commit(winner,year,law.TermYears,procedure.Ballot.Id);
        country.Ruler=new RulerRef{ActorId=winner,Name=elected.Name,Age=elected.Person.Age,IsFemale=elected.IsFemale,KingdomId=country.KingdomId,SinceYear=office.SinceYear,ClanId=elected.ClanId,ClanName=elected.ClanName};
        if(country.RoyalClanId!=elected.ClanId)country.RecordRoyalClan(country.RoyalClanId);
        country.RoyalClanId=elected.ClanId;country.RoyalClanName=elected.ClanName??"";
        country.Regency?.End();
        world.SetLastRuler(country.KingdomId,country.Ruler);
        if(country.Election==null)country.Election=new ElectionState();
        country.Election.NextElectionYear=unlimitedAtaman?0:office.NextElectionYear;country.Election.LastElectionYear=year;
        country.Election.TermsServed=office.Mandates[winner].Lifetime;
        procedure.Transition(CivicProcedureStage.Executed,"exact-native-winner-confirmed");
        world.Bus.Publish(new PolEvent(PolEventType.DemocraticElection,unlimitedAtaman?"RP_EV_ATAMAN_ELECTED":"RP_GOV26_EV_HEAD_ELECTED")
            {Year=year,KingdomId=country.KingdomId,ActorId=winner,Severity=Severity.Major}
            .With("ruler",elected.Name).With("terms",office.Mandates[winner].Lifetime).With("next_election",unlimitedAtaman?0:office.NextElectionYear)
            .With("election_kind",unlimitedAtaman?"free_circle":"individual-ballots").With("votes",procedure.Ballot.Tally().Counts[winner]).With("eligible",procedure.Ballot.EligibleCount));
        return true;
    }
    /// <summary>
    /// 0.48.5: второй тур выборов главы.
    ///
    /// Кто участвует: только те, кто разделил первое место в первом туре (обычно двое).
    /// Голосуют те же избиратели: у каждого уже есть свои предпочтения, поэтому
    /// результат второго тура честно отличается от первого — сторонники выбывших
    /// кандидатов выбирают из оставшихся.
    /// Возвращает закрытый бюллетень второго тура или null, если второго тура быть не может
    /// (например, все воздержались).
    /// </summary>
    private static CivicBallot Runoff(PoliticalWorld world,KingdomPolitics country,int year,CivicBallot first,
        List<int> electors,Dictionary<int,CivicResident> profiles,CivicOfficeState office)
    {
        var counts=first.Tally().Counts;
        int max=0;
        foreach(var pair in counts)
            if(pair.Key!=CivicBallot.Abstain && pair.Value>max)max=pair.Value;
        if(max<=0)return null;
        var tied=new List<int>();
        foreach(var pair in counts)
            if(pair.Key!=CivicBallot.Abstain && pair.Value==max)tied.Add(pair.Key);
        tied.Sort();
        if(tied.Count<2)return null;
        var candidates=new List<CivicResident>();
        foreach(int id in tied)
            if(profiles.TryGetValue(id,out var resident))candidates.Add(resident);
        if(candidates.Count<2)return null;
        CivicBallot runoff=CivicBallot.ForCandidates(first.Id+":runoff",year,electors,tied);
        // Во втором туре — только честный выбор: если жителю соперники совершенно равны,
        // он вправе воздержаться. Никаких «победителей по номеру» не выдумываем:
        // если второй тур тоже дал поровну, выборы так и остаются несостоявшимися.
        runoff.Collect(year,actor=>ChooseFast(profiles[actor],candidates,office.HolderId,null));
        if(!runoff.TryClose(year,out var tally))return null;
        int winner=tally.UniquePluralityWinner;
        if(winner>0)
        {
            int other=0,otherVotes=0;
            foreach(int id in tied)
            {
                if(id==winner)continue;
                int votes;
                tally.Counts.TryGetValue(id,out votes);
                if(votes>=otherVotes){other=id;otherVotes=votes;}
            }
            string winnerName=profiles.TryGetValue(winner,out var w)?w.Name:string.Empty;
            string otherName=profiles.TryGetValue(other,out var o)?o.Name:string.Empty;
            world.Bus.Publish(new PolEvent(PolEventType.DemocraticElection,"RP_GOV26_EV_HEAD_RUNOFF")
                {Year=year,KingdomId=country.KingdomId,ActorId=winner,Severity=Severity.Minor}
                .With("ruler",winnerName).With("runoff_votes",tally.Counts[winner])
                .With("other",otherName).With("other_votes",otherVotes)
                .With("eligible",runoff.EligibleCount).With("candidates",tied.Count));
        }
        return runoff;
    }

    private static void PublishDeferred(PoliticalWorld world,KingdomPolitics country,int year,string reason)
        =>world.Bus.Publish(new PolEvent(PolEventType.CivicElectionDeferred,"RP_GOV26_EV_HEAD_DEFERRED")
            {Year=year,KingdomId=country.KingdomId,Severity=Severity.Minor}.With("reason",reason));
}
