using System;
using System.Collections.Generic;

namespace RealPolitic.Core;

/// <summary>A complete annual frame; a failed/partial native read must never be passed as an empty frame.</summary>
public sealed class GovernanceNation
{
    public int CountryId;
    public int BirthYear;
    public string Name="";
    public int Population;
    public int Warriors;
    public bool Sovereign=true;
    public double? AnnualIncome;
    public ElectoralChapter Rules;
}

public sealed class NationalPositionRequest
{
    public string ProcedureId;
    public int CountryId;
    public int TargetCountryId;
    public int Year;
    public int LatestCompletionYear;
    public ElectoralChapter Rules;
}

/// <summary>The port reads actual people. False means unavailable, never a fabricated national vote.</summary>
public interface INationalPositionSource
{
    bool TryOpenRecognitionPosition(NationalPositionRequest request,out CivicProcedure procedure);
}

/// <summary>Annual recognition orchestration. No native action, RNG, or implicit new-world activation.</summary>
public static class GovernanceDispatcher
{
    public static string PositionId(string roundId,int countryId)=>roundId+":position:"+countryId;

    public static bool Tick(PoliticalWorld world,int year,IList<GovernanceNation> nations,INationalPositionSource source,bool lifecycleChange=false)
    {
        if(world==null)throw new ArgumentNullException(nameof(world));
        var state=world.Governance;
        if(!state.Enabled)return false;
        if(year<0)throw new ArgumentOutOfRangeException(nameof(year));
        if(year<state.LastDispatchYear || year==state.LastDispatchYear && !lifecycleChange)return false;
        if(nations==null || source==null)throw new ArgumentNullException();
        // Validate the entire frame before treating missing countries as extinct or recording any income.
        var live=new SortedDictionary<int,GovernanceNation>();
        foreach(var nation in nations)
        {
            if(nation==null || nation.CountryId<=0 || nation.BirthYear<0 || nation.BirthYear>year
                || nation.Population<0 || nation.Warriors<0 || nation.Rules==null || live.ContainsKey(nation.CountryId)
                || nation.AnnualIncome.HasValue && (double.IsNaN(nation.AnnualIncome.Value)||double.IsInfinity(nation.AnnualIncome.Value)))
                throw new ArgumentException("Incomplete or invalid governance frame.");
            nation.Rules.Validate();live.Add(nation.CountryId,nation);
        }
        var registry=state.Recognition;
        var old=registry.Capture();var known=new Dictionary<int,CountryRecognitionState>();
        foreach(var country in old.Countries)
        {
            known.Add(country.CountryId,country);
            if(!country.Alive && live.ContainsKey(country.CountryId))throw new ArgumentException("Extinct country ID was reused.");
        }
        foreach(var country in old.Countries)if(country.Alive && !live.ContainsKey(country.CountryId))registry.DestroyCountry(country.CountryId);
        var powers=new List<NationalPowerSnapshot>();
        foreach(var nation in live.Values)
        {
            if(!known.ContainsKey(nation.CountryId))registry.RegisterCountry(nation.CountryId,nation.BirthYear,nation.Sovereign);
            else registry.SetSovereignty(nation.CountryId,nation.Sovereign);
            if(nation.AnnualIncome.HasValue)state.RecordIncome(nation.CountryId,year,nation.AnnualIncome.Value);
            var power=new NationalPowerSnapshot{CountryId=nation.CountryId,Population=nation.Population,Warriors=nation.Warriors};
            state.FillIncome(power);powers.Add(power);
        }
        if(registry.ReviewGreatPowers(year,powers))
        {
            var names=new List<string>();foreach(int id in registry.GreatPowers)names.Add(Name(live,id));
            world.Bus.Publish(new PolEvent(PolEventType.GreatPowerCompositionChanged,"RP_GOV26_EV_COUNCIL")
                {Year=year,Severity=Severity.Major}.With("members",names.Count==0?"—":string.Join(", ",names.ToArray())));
        }
        var active=registry.Capture();
        var activeIds=new HashSet<string>(StringComparer.Ordinal);
        foreach(var pending in active.Pending.Values)
            foreach(int voter in pending.Electors)activeIds.Add(PositionId(pending.Id,voter));
        foreach(var p in state.Procedures.Values)
            if(p.Kind==CivicProcedureKind.RecognitionPosition && !activeIds.Contains(p.Ballot.Id)
                && (p.Stage==CivicProcedureStage.Voting || p.Stage==CivicProcedureStage.Approved))
                p.Transition(CivicProcedureStage.Cancelled,"recognition-round-invalidated");
        foreach(var nation in live.Values)
        {
            if(!registry.NeedsRecognitionVote(nation.CountryId))continue;
            int duration=0;
            foreach(int voter in registry.GreatPowers)
            {
                var rules=live[voter].Rules;
                if(rules.ForeignDecisions==ForeignDecisionAuthority.Electorate)duration=Math.Max(duration,rules.BallotYears);
            }
            // One extra year keeps the country ballot open at the last national referendum's deadline.
            registry.OpenRecognitionVote(nation.CountryId,year,checked(duration+1));
        }
        active=registry.Capture();var targets=new List<int>(active.Pending.Keys);targets.Sort();
        foreach(int target in targets)
        {
            var round=registry.PendingVote(target);if(round==null)continue;
            foreach(int voter in round.Capture().Electors)
            {
                if(round.Votes.ContainsKey(voter))continue;
                string id=PositionId(round.Id,voter);
                if(!state.Procedures.TryGetValue(id,out var procedure))
                {
                    var rules=live[voter].Rules;
                    int duration=rules.ForeignDecisions==ForeignDecisionAuthority.Ruler?0:rules.BallotYears;
                    // Never begin a late national referendum that cannot finish before the international deadline.
                    if((long)year+duration>=round.DeadlineYear)continue;
                    var request=new NationalPositionRequest{ProcedureId=id,CountryId=voter,TargetCountryId=target,
                        Year=year,LatestCompletionYear=round.DeadlineYear-1,Rules=rules};
                    if(!source.TryOpenRecognitionPosition(request,out procedure))continue;
                    ValidatePosition(request,procedure);
                    if(!state.Procedures.ContainsKey(id))state.AddProcedure(procedure);
                    else if(!ReferenceEquals(state.Procedures[id],procedure))throw new InvalidOperationException("The port replaced a saved procedure.");
                }
                if(year<procedure.Ballot.DeadlineYear || procedure.Stage==CivicProcedureStage.Cancelled || procedure.Stage==CivicProcedureStage.ExecutionFailed)continue;
                if(procedure.Stage==CivicProcedureStage.Voting && (procedure.Ballot.Closed || procedure.Ballot.TryClose(year,out _)))
                {
                    bool yes=procedure.Ballot.Tally().BinaryAccepted;
                    procedure.Transition(yes?CivicProcedureStage.Approved:CivicProcedureStage.Rejected);
                    var tally=procedure.Ballot.Tally();
                    world.Bus.Publish(new PolEvent(PolEventType.NationalRecognitionPosition,"RP_GOV26_EV_NATIONAL_POSITION")
                        {Year=year,KingdomId=voter,Severity=Severity.Minor}.With("kingdom",Name(live,voter))
                        .With("target",Name(live,target)).With("yes",tally.Counts[CivicBallot.Yes])
                        .With("no",tally.Counts[CivicBallot.No]).With("abstain",tally.Abstained)
                        .With("eligible",tally.Eligible).With("result",NationalChoice(procedure)==CivicBallot.Abstain?"RP_GOV26_ABSTAIN":yes?"RP_GOV26_YES":"RP_GOV26_NO"));
                }
                // No quorum or no valid individual choice means no national mandate, never a fabricated Yes.
                if(procedure.Stage==CivicProcedureStage.Approved || procedure.Stage==CivicProcedureStage.ExecutionPending
                    || procedure.Stage==CivicProcedureStage.Executed || procedure.Stage==CivicProcedureStage.Rejected)
                {
                    int choice=NationalChoice(procedure);
                    // Aggregate timely individual votes at their scheduled completion, even if
                    // this frame is late. Missing procedures above still cannot open after the deadline.
                    if(round.TryCast(voter,choice,procedure.Ballot.DeadlineYear) && procedure.Stage==CivicProcedureStage.Approved)
                    {
                        procedure.Transition(CivicProcedureStage.ExecutionPending);
                        procedure.Transition(CivicProcedureStage.Executed,"national-position-submitted");
                    }
                }
            }
            // Decisions arrive before close; all three explicit positions permit an immediate result.
            if(registry.CompleteRecognitionVote(target,year,out bool recognized))
            {
                var tally=round.Tally();
                world.Bus.Publish(new PolEvent(PolEventType.InternationalRecognitionDecided,"RP_GOV26_EV_RECOGNITION")
                    {Year=year,KingdomId=target,Severity=Severity.Major}.With("kingdom",Name(live,target))
                    .With("yes",tally.Counts[CivicBallot.Yes]).With("no",tally.Counts[CivicBallot.No])
                    .With("missing",tally.Eligible-tally.Participated+tally.Abstained)
                    .With("result",recognized?"RP_GOV26_RECOGNIZED":"RP_GOV26_REFUSED"));
            }
        }
        state.RestoreDispatchYear(year);return true;
    }
    private static int NationalChoice(CivicProcedure p)
    {var tally=p.Ballot.Tally();return !tally.QuorumReached || tally.ValidVotes==0?CivicBallot.Abstain:tally.BinaryAccepted?CivicBallot.Yes:CivicBallot.No;}
    private static void ValidatePosition(NationalPositionRequest r,CivicProcedure p)
    {
        int duration=r.Rules.ForeignDecisions==ForeignDecisionAuthority.Ruler?0:r.Rules.BallotYears;
        int quorum=r.Rules.ForeignDecisions==ForeignDecisionAuthority.Ruler?0:r.Rules.ReferendumQuorumPercent;
        var threshold=r.Rules.ForeignDecisions==ForeignDecisionAuthority.Ruler?BallotThreshold.MoreThanHalf:r.Rules.ReferendumThreshold;
        if(p==null || p.Ballot.DurationYears!=duration || p.Ballot.QuorumPercent!=quorum || p.Ballot.Threshold!=threshold
            || duration==0 && p.Ballot.EligibleCount!=1 || p.Kind!=CivicProcedureKind.RecognitionPosition || p.CountryId!=r.CountryId || p.TargetCountryId!=r.TargetCountryId
            || p.CityId!=0 || p.Ballot.Id!=r.ProcedureId || p.Ballot.ElectorKind!=BallotElectorKind.Person
            || p.Ballot.OpenedYear!=r.Year || p.Ballot.DeadlineYear>r.LatestCompletionYear
            || p.Stage!=CivicProcedureStage.Voting || p.Ballot.Closed)
            throw new InvalidOperationException("The national vote source returned a mismatched procedure.");
    }
    private static string Name(SortedDictionary<int,GovernanceNation> live,int id)
        =>live.TryGetValue(id,out var nation) && !string.IsNullOrWhiteSpace(nation.Name)?nation.Name:"#"+id;
}
