using System;
using System.Collections.Generic;

namespace RealPolitic.Core;

public enum CivicAllianceAction { Admission, Expulsion }
public enum CivicMotionStage { Voting, Approved, Rejected, ExecutionPending, Executed, ExecutionFailed, Cancelled }
public sealed class CivicAllianceMotion
{
    public string Id="";
    public CivicAllianceAction Action;
    public int AllianceId;
    public int ProposerId;
    public int SubjectId;
    public List<int> Members=new List<int>();
    public CivicBallot Ballot;
    public CivicMotionStage Stage;
    public string Note="";
    public bool ConsentAnnounced;
    public string PositionId(int voter)=>Id+":position:"+voter;
    public string ConsentId=>Id+":applicant-consent";
    public void Validate()
    {
        if(string.IsNullOrWhiteSpace(Id) || AllianceId<0 || ProposerId<=0 || SubjectId<=0 || ProposerId==SubjectId
            || !Enum.IsDefined(typeof(CivicAllianceAction),Action) || !Enum.IsDefined(typeof(CivicMotionStage),Stage)
            || Members==null || Ballot==null || Note==null || Ballot.Id!=Id || Ballot.ElectorKind!=BallotElectorKind.Country)
            throw new ArgumentException("Invalid alliance motion.");
        var members=new HashSet<int>();foreach(int id in Members)if(id<=0 || !members.Add(id))throw new ArgumentException("Invalid member roll.");
        if(!members.Contains(ProposerId) || Action==CivicAllianceAction.Admission && members.Contains(SubjectId)
            || Action==CivicAllianceAction.Expulsion && (!members.Contains(SubjectId) || AllianceId==0))throw new ArgumentException("Invalid subject.");
        members.Remove(SubjectId);var saved=Ballot.Capture();
        if(!members.SetEquals(saved.Electors) || saved.Options.Count!=2 || !saved.Options.Contains(1) || !saved.Options.Contains(2))
            throw new ArgumentException("Alliance electorate differs from the frozen member roll.");
        if((Stage==CivicMotionStage.Approved || Stage==CivicMotionStage.ExecutionPending || Stage==CivicMotionStage.Executed)
            && (!Ballot.Closed || !Ballot.Tally().BinaryAccepted))throw new ArgumentException("Execution without a majority of all eligible members.");
    }
}
public sealed class AlliancePositionRequest
{
    public string Id;
    public int CountryId;
    public int SubjectId;
    public int PoliticalTargetId;
    public int Year;
    public int LatestCompletionYear;
    public bool Expulsion;
    public ElectoralChapter Rules;
    public int AllianceMembersAfter;
    public int TotalCountries;
    public bool Consent;
    public bool VoterAtWar;
    public bool SubjectAtWar;
    public bool AllianceAtWar;
    public bool DirectWar;
    public bool CommonEnemy;
}
public interface ICivicAllianceSource
{
    // Exact frozen membership, recognition and subject eligibility; false cancels a stale motion.
    bool StillValid(CivicAllianceMotion motion);
    bool TryReadRules(int countryId,out ElectoralChapter rules);
    bool TryOpenPosition(AlliancePositionRequest request,out CivicProcedure procedure);
    // Must check exact resulting native membership; no direct mutation of internal saved lists.
    bool TryExecute(CivicAllianceMotion motion);
}
public static class CivicAllianceMotions
{
    public static CivicAllianceMotion Propose(GovernanceState state,CivicAllianceAction action,int allianceId,int proposer,int subject,
        IEnumerable<int> currentMembers,int year,ICivicAllianceSource source)
    {
        if(state==null || !state.Enabled || source==null || year<0)return null;
        var members=new List<int>(currentMembers);members.Sort();
        var voters=new List<int>();int duration=0;
        foreach(int member in members)
        {
            if(member==subject)continue;voters.Add(member);
            if(!source.TryReadRules(member,out var rules))return null;rules.Validate();
            if(rules.ForeignDecisions==ForeignDecisionAuthority.Electorate)duration=Math.Max(duration,rules.BallotYears);
        }
        if(action==CivicAllianceAction.Admission)
        {
            if(!source.TryReadRules(subject,out var rules))return null;rules.Validate();
            if(rules.ForeignDecisions==ForeignDecisionAuthority.Electorate)duration=Math.Max(duration,rules.BallotYears);
        }
        string id="alliance:"+allianceId+":"+(int)action+":"+subject+":"+proposer+":"+year;
        foreach(var previous in state.AllianceMotions.Values)
        {
            if(previous.Id==id || previous.AllianceId==allianceId && previous.SubjectId==subject
                && (previous.Stage==CivicMotionStage.Voting || previous.Stage==CivicMotionStage.Approved || previous.Stage==CivicMotionStage.ExecutionPending))return null;

            // 0.31.5, по итогам 700-летнего прогона: альянс не должен в один и тот же
            // годовой цикл цепочкой принимать всех соседей, а отказавшийся кандидат —
            // получать новое предложение каждые несколько лет, пока случайно не скажет «да».
            if(action==CivicAllianceAction.Admission && previous.Action==CivicAllianceAction.Admission)
            {
                int opened=previous.Ballot.OpenedYear;
                bool finished=previous.Stage==CivicMotionStage.Executed || previous.Stage==CivicMotionStage.Rejected
                    || previous.Stage==CivicMotionStage.ExecutionFailed || previous.Stage==CivicMotionStage.Cancelled;
                if(finished && previous.SubjectId==subject && year>=opened && year<opened+15)return null;
                if(allianceId!=0 && previous.AllianceId==allianceId && previous.Stage==CivicMotionStage.Executed
                    && year>=opened && year<opened+5)return null;
            }
        }
        var motion=new CivicAllianceMotion{Id=id,Action=action,AllianceId=allianceId,ProposerId=proposer,SubjectId=subject,Members=members,
            Ballot=new CivicBallot(id,BallotElectorKind.Country,year,checked(duration+1),voters,new[]{1,2})};
        motion.Validate();if(!source.StillValid(motion))return null;state.AddAllianceMotion(motion);return motion;
    }
    public static void Tick(PoliticalWorld world,int year,ICivicAllianceSource source)
    {
        if(world?.Governance.Enabled!=true)return;
        var state=world.Governance;var motions=new List<CivicAllianceMotion>(state.AllianceMotions.Values);
        motions.Sort((a,b)=>string.CompareOrdinal(a.Id,b.Id));
        foreach(var motion in motions)
        {
            if(motion.Stage!=CivicMotionStage.Voting && motion.Stage!=CivicMotionStage.Approved && motion.Stage!=CivicMotionStage.ExecutionPending)continue;
            if(motion.Stage==CivicMotionStage.Voting && !source.StillValid(motion))
            {
                motion.Stage=CivicMotionStage.Cancelled;motion.Note="membership-or-eligibility-changed";
                foreach(var p in state.Procedures.Values)if(p.Kind==CivicProcedureKind.AlliancePosition && p.Ballot.Id.StartsWith(motion.Id+":",StringComparison.Ordinal))
                    p.Transition(CivicProcedureStage.Cancelled,"alliance-motion-cancelled");
                Publish(world,motion,year);continue;
            }
            if(motion.Stage==CivicMotionStage.Voting)
            {
                foreach(int voter in motion.Ballot.Capture().Electors)
                {
                    if(motion.Ballot.Votes.ContainsKey(voter))continue;
                    var position=Position(world,state,source,motion,voter,false,year);
                    // The individual ballots already contain the timely votes. A delayed simulation tick
                    // must submit their result at their scheduled completion, not treat it as a new late vote.
                    // Position() still refuses to open a missing procedure after the parent deadline.
                    if(position!=null && position.Ballot.Closed && motion.Ballot.TryCast(voter,NationalChoice(position),position.Ballot.DeadlineYear))
                    {
                        PublishNational(world,motion,position,year,false);
                        if(position.Stage==CivicProcedureStage.Approved)
                        {position.Transition(CivicProcedureStage.ExecutionPending);position.Transition(CivicProcedureStage.Executed,"national-alliance-position-submitted");}
                    }
                }
                bool consent=true,consentReady=true;
                if(motion.Action==CivicAllianceAction.Admission)
                {
                    var applicant=Position(world,state,source,motion,motion.SubjectId,true,year);
                    consentReady=applicant!=null && applicant.Ballot.Closed;
                    consent=consentReady && NationalChoice(applicant)==CivicBallot.Yes;
                    if(consentReady && !motion.ConsentAnnounced)
                    {
                        PublishNational(world,motion,applicant,year,true);motion.ConsentAnnounced=true;
                        if(applicant.Stage==CivicProcedureStage.Approved)
                        {applicant.Transition(CivicProcedureStage.ExecutionPending);applicant.Transition(CivicProcedureStage.Executed,"national-alliance-consent-submitted");}
                    }
                }
                bool all=motion.Ballot.Votes.Count==motion.Ballot.EligibleCount && consentReady;
                bool due=year>=motion.Ballot.DeadlineYear;
                if(!all && !due)continue;
                if(all)motion.Ballot.TryCloseFullyVoted(year);else motion.Ballot.TryClose(year,out _);
                motion.Stage=motion.Ballot.Tally().BinaryAccepted && consent?CivicMotionStage.Approved:CivicMotionStage.Rejected;
                motion.Note=consent?"member-majority-tallied":"applicant-did-not-consent";
                Publish(world,motion,year);
            }
            if(motion.Stage==CivicMotionStage.Approved)motion.Stage=CivicMotionStage.ExecutionPending;
            if(motion.Stage!=CivicMotionStage.ExecutionPending)continue;
            if(source.TryExecute(motion)){motion.Stage=CivicMotionStage.Executed;motion.Note="exact-native-membership-confirmed";}
            else {motion.Stage=CivicMotionStage.ExecutionFailed;motion.Note="native-execution-unconfirmed";}
            Publish(world,motion,year);
        }
    }
    private static CivicProcedure Position(PoliticalWorld world,GovernanceState state,ICivicAllianceSource source,CivicAllianceMotion motion,int voter,bool consent,int year)
    {
        string id=consent?motion.ConsentId:motion.PositionId(voter);
        if(!state.Procedures.TryGetValue(id,out var p))
        {
            if(!source.TryReadRules(voter,out var rules))return null;rules.Validate();
            int duration=rules.ForeignDecisions==ForeignDecisionAuthority.Ruler?0:rules.BallotYears;
            if((long)year+duration>=motion.Ballot.DeadlineYear)return null;
            bool allianceAtWar=false;
            foreach(int member in motion.Members)if(world.IsAtWar(member)){allianceAtWar=true;break;}
            int politicalTarget=consent?motion.ProposerId:motion.SubjectId;
            var r=new AlliancePositionRequest{Id=id,CountryId=voter,SubjectId=motion.SubjectId,PoliticalTargetId=politicalTarget,
                Year=year,LatestCompletionYear=motion.Ballot.DeadlineYear-1,Rules=rules,Expulsion=!consent && motion.Action==CivicAllianceAction.Expulsion,
                AllianceMembersAfter=motion.Action==CivicAllianceAction.Admission?motion.Members.Count+1:Math.Max(0,motion.Members.Count-1),
                TotalCountries=world.Kingdoms.Count,Consent=consent,
                VoterAtWar=world.IsAtWar(voter),SubjectAtWar=world.IsAtWar(motion.SubjectId),AllianceAtWar=allianceAtWar,
                DirectWar=world.AreAtWar(voter,politicalTarget),CommonEnemy=world.HasCommonEnemy(voter,motion.SubjectId)};
            if(!source.TryOpenPosition(r,out p))return null;
            if(p==null || p.Kind!=CivicProcedureKind.AlliancePosition || p.CountryId!=voter || p.TargetCountryId!=motion.SubjectId
                || p.CityId!=0 || p.Ballot.Id!=id || p.Ballot.ElectorKind!=BallotElectorKind.Person || p.Ballot.OpenedYear!=year
                || p.Ballot.DurationYears!=duration || p.Ballot.Closed || p.Stage!=CivicProcedureStage.Voting
                || p.Ballot.QuorumPercent!=(duration==0?0:rules.ReferendumQuorumPercent)
                || p.Ballot.Threshold!=(duration==0?BallotThreshold.MoreThanHalf:rules.ReferendumThreshold)
                || duration==0 && p.Ballot.EligibleCount!=1)throw new InvalidOperationException("Invalid national alliance position.");
            if(!state.Procedures.ContainsKey(id))state.AddProcedure(p);
            else if(!ReferenceEquals(state.Procedures[id],p))throw new InvalidOperationException("Saved position was replaced.");
        }
        // Even an already closed saved ballot cannot submit a future constitutional result.
        if(year<p.Ballot.DeadlineYear || p.Stage==CivicProcedureStage.Cancelled || p.Stage==CivicProcedureStage.ExecutionFailed)return null;
        if(p.Stage==CivicProcedureStage.Voting && (p.Ballot.Closed || p.Ballot.TryClose(year,out _)))
            p.Transition(p.Ballot.Tally().BinaryAccepted?CivicProcedureStage.Approved:CivicProcedureStage.Rejected);
        return p;
    }
    private static int NationalChoice(CivicProcedure p)
    {var tally=p.Ballot.Tally();return !tally.QuorumReached || tally.ValidVotes==0?CivicBallot.Abstain:tally.BinaryAccepted?CivicBallot.Yes:CivicBallot.No;}
    private static void PublishNational(PoliticalWorld world,CivicAllianceMotion motion,CivicProcedure p,int year,bool consent)
    {
        var tally=p.Ballot.Tally();int choice=NationalChoice(p);
        world.Bus.Publish(new PolEvent(PolEventType.NationalAlliancePosition,"RP_GOV26_EV_ALLIANCE_POSITION")
            {Year=year,KingdomId=p.CountryId,Severity=Severity.Major}.With("kingdom",world.KingdomNameOf(p.CountryId))
            .With("subject",world.KingdomNameOf(motion.SubjectId)).With("question",consent?"RP_GOV26_CONSENT":motion.Action==CivicAllianceAction.Admission?"RP_GOV26_ADMISSION":"RP_GOV26_EXPULSION")
            .With("result",choice==CivicBallot.Yes?"RP_GOV26_YES":choice==CivicBallot.No?"RP_GOV26_NO":"RP_GOV26_ABSTAIN")
            .With("yes",tally.Counts[1]).With("no",tally.Counts[2]).With("abstain",tally.Abstained).With("eligible",tally.Eligible));
    }
    private static void Publish(PoliticalWorld world,CivicAllianceMotion motion,int year)
        =>world.Bus.Publish(new PolEvent(PolEventType.CivicAllianceMotion,"RP_GOV26_EV_ALLIANCE_MOTION")
            {Year=year,KingdomId=motion.SubjectId,Severity=Severity.Major}.With("motion",motion.Id).With("stage",(int)motion.Stage).With("status","RP_GOV26_MOTION_STAGE_"+(int)motion.Stage)
            .With("action",motion.Action==CivicAllianceAction.Admission?"RP_GOV26_ADMISSION":"RP_GOV26_EXPULSION").With("proposer",world.KingdomNameOf(motion.ProposerId)).With("subject",world.KingdomNameOf(motion.SubjectId))
            .With("yes",motion.Ballot.Tally().Counts[1]).With("eligible",motion.Ballot.EligibleCount).With("note",motion.Note));
}
