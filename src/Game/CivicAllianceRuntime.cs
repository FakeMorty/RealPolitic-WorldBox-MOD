using System;
using System.Collections.Generic;
using RealPolitic.Core;

namespace RealPolitic;

/// <summary>Read-only native membership checks and sanctioned API calls. Never edits alliance collections.</summary>
public sealed class CivicAllianceRuntime:ICivicAllianceSource
{
    private readonly PoliticalWorld _world;
    [ThreadStatic] private static CivicAllianceMotion _executing;
    public static bool ExecutingApprovedMotion=>_executing!=null;
    public static bool ApprovedPair(Kingdom a,Kingdom b)
        =>_executing!=null && _executing.Action==CivicAllianceAction.Admission && a!=null && b!=null
            && (((NanoObject)a).id==_executing.ProposerId && ((NanoObject)b).id==_executing.SubjectId
                || ((NanoObject)b).id==_executing.ProposerId && ((NanoObject)a).id==_executing.SubjectId);
    public static bool ApprovedJoin(object alliance,Kingdom candidate)
    {
        if(_executing==null || _executing.Action!=CivicAllianceAction.Admission || candidate==null)return false;
        long id=((NanoObject)candidate).id;
        if(id!=_executing.ProposerId && id!=_executing.SubjectId)return false;
        if(_executing.AllianceId<=0 || alliance==null)return true;
        int stable=AllianceSafety.StableId(alliance);
        return stable<=0 || stable==_executing.AllianceId;
    }
    public static bool RequestAdmissionFromAlliance(PoliticalWorld world,object alliance,Kingdom candidate,int year)
    {
        if(world?.Governance.Enabled!=true || alliance==null || candidate==null)return false;
        var member=AllianceSafety.FirstMember(alliance,candidate);
        return member!=null && RequestAdmission(world,member,candidate,year);
    }
    public CivicAllianceRuntime(PoliticalWorld world){_world=world;}
    private Kingdom Country(int id)=>CivilWarExecutor.FindKingdom(World.world,id);
    private static bool Members(object alliance,out List<int> ids)
    {
        ids=new List<int>();var source=AllianceSafety.Members(alliance);if(source==null)return false;var unique=new HashSet<int>();
        foreach(var item in source)
        {
            if(!(item is Kingdom country))return false;long raw=((NanoObject)country).id;
            if(raw<=0 || raw>int.MaxValue || !unique.Add((int)raw) || !AllianceSafety.TryMembership(country,out var membership)
                || !ReferenceEquals(membership,alliance))return false;
            ids.Add((int)raw);
        }
        ids.Sort();return true;
    }
    public static bool RequestAdmission(PoliticalWorld world,Kingdom proposer,Kingdom candidate,int year)
    {
        if(world?.Governance.Enabled!=true || proposer==null || candidate==null || ReferenceEquals(proposer,candidate))return false;
        if(!AllianceSafety.TryMembership(proposer,out var alliance) || !AllianceSafety.TryMembership(candidate,out var other))return false;
        if(alliance==null && other!=null){var swap=proposer;proposer=candidate;candidate=swap;alliance=other;other=null;}
        if(other!=null)return false;
        int proposerId=(int)((NanoObject)proposer).id,subject=(int)((NanoObject)candidate).id;
        if(!world.Governance.Recognition.CanContact(proposerId,subject,InternationalContact.Alliance))return false;
        List<int> members;int id=alliance==null?0:AllianceSafety.StableId(alliance);
        if(alliance==null)members=new List<int>{proposerId};else if(id<=0 || !Members(alliance,out members))return false;
        var source=new CivicAllianceRuntime(world);
        if(!source.AdmissionAllowed(proposerId,subject,members,year))return false;
        return CivicAllianceMotions.Propose(world.Governance,CivicAllianceAction.Admission,id,proposerId,subject,members,year,source)!=null;
    }
    public static bool RequestExpulsion(PoliticalWorld world,int proposer,int subject,int year)
    {
        var source=new CivicAllianceRuntime(world);var country=source.Country(proposer);
        if(world?.Governance.Enabled!=true || country==null || !AllianceSafety.TryMembership(country,out var alliance) || alliance==null
            || AllianceSafety.StableId(alliance)<=0 || !Members(alliance,out var members)
            || !members.Contains(proposer) || !members.Contains(subject))return false;
        return CivicAllianceMotions.Propose(world.Governance,CivicAllianceAction.Expulsion,AllianceSafety.StableId(alliance),proposer,subject,members,year,source)!=null;
    }
    private bool AdmissionAllowed(int proposerId,int subjectId,List<int> members,int year)
    {
        var proposer=_world.GetKingdom(proposerId);var subject=_world.GetKingdom(subjectId);
        if(proposer==null || subject==null || members==null)return false;
        if(year-Math.Max(0,proposer.FoundedYear)<AllianceBalance.MinimumStateAgeYears
            || year-Math.Max(0,subject.FoundedYear)<AllianceBalance.MinimumStateAgeYears)return false;
        if(_world.AreAtWar(proposerId,subjectId))return false;
        return members.Count+1<=AllianceBalance.MaxMembers(ViableCountryCount());
    }
    private int ViableCountryCount()
    {
        int count=0;
        foreach(var state in _world.Kingdoms)
        {
            var country=Country(state.KingdomId);
            if(country!=null && (country.cities!=null && country.cities.Count>0 || state.Population>0))count++;
        }
        return count;
    }
    public bool TryReadRules(int countryId,out ElectoralChapter rules)
    {rules=_world.GetKingdom(countryId)?.Constitution?.Electoral;return rules!=null && Country(countryId)!=null && _world.Governance.Recognition.IsRecognized(countryId);}
    public bool StillValid(CivicAllianceMotion motion)
    {
        var proposer=Country(motion.ProposerId);var subject=Country(motion.SubjectId);
        if(proposer==null || subject==null || !AllianceSafety.TryMembership(proposer,out var alliance)
            || !AllianceSafety.TryMembership(subject,out var subjectAlliance))return false;
        if(motion.AllianceId==0)
            return motion.Action==CivicAllianceAction.Admission && alliance==null && subjectAlliance==null
                && motion.Members.Count==1 && motion.Members[0]==motion.ProposerId
                && AdmissionAllowed(motion.ProposerId,motion.SubjectId,motion.Members,RealPoliticMod.CurrentYear())
                && _world.Governance.Recognition.CanContact(motion.ProposerId,motion.SubjectId,InternationalContact.Alliance);
        if(alliance==null || AllianceSafety.StableId(alliance)!=motion.AllianceId || !Members(alliance,out var members)
            || !new HashSet<int>(members).SetEquals(motion.Members))return false;
        if(motion.Action==CivicAllianceAction.Admission && subjectAlliance!=null || motion.Action==CivicAllianceAction.Expulsion && !ReferenceEquals(subjectAlliance,alliance))return false;
        if(motion.Action==CivicAllianceAction.Admission
            && !AdmissionAllowed(motion.ProposerId,motion.SubjectId,members,RealPoliticMod.CurrentYear()))return false;
        foreach(int id in members)if(id!=motion.SubjectId && !TryReadRules(id,out _))return false;
        return motion.Action==CivicAllianceAction.Expulsion || _world.Governance.Recognition.IsRecognized(motion.SubjectId);
    }
    public bool TryOpenPosition(AlliancePositionRequest request,out CivicProcedure procedure)
    {
        procedure=null;var country=Country(request.CountryId);var state=_world.GetKingdom(request.CountryId);
        var target=_world.GetKingdom(request.PoliticalTargetId);
        if(country==null || state==null || target==null || !ReferenceEquals(state.Constitution?.Electoral,request.Rules)
            || !ReferenceEquals(_world,RealPoliticMod.World) || !_world.Governance.Enabled)return false;
        var issue=CivicForeignInterests.Question(_world,state,target,false);issue.ReverseChoice=request.Expulsion;
        issue.AllianceQuestion=true;issue.AllianceConsent=request.Consent;
        issue.AllianceShareAfter=request.TotalCountries>0 ? Math.Max(0f,Math.Min(1f,(float)request.AllianceMembersAfter/request.TotalCountries)) : 0f;
        issue.VoterAtWar=request.VoterAtWar;issue.SubjectAtWar=request.SubjectAtWar;issue.AllianceAtWar=request.AllianceAtWar;
        issue.DirectWar=request.DirectWar;issue.CommonEnemy=request.CommonEnemy;
        if(request.Rules.ForeignDecisions==ForeignDecisionAuthority.Electorate)
            return CivicElectorateReader.TryOpenIssue(_world,country,null,request.Rules,issue,request.Id,CivicProcedureKind.AlliancePosition,
                request.SubjectId,request.Year,out procedure,out _);
        var ruler=GameRefs.ReadRuler(country);
        if(ruler==null || !(GameRefs.Invoke(ruler,"isAlive") is bool alive) || !alive)return false;
        long actor=GameText.ActorLongId(ruler);if(actor<=0 || actor>int.MaxValue)return false;
        var belief=ActorIdeologies.Get(ruler);if(belief==null)return false;
        var ballot=new CivicBallot(request.Id,BallotElectorKind.Person,request.Year,0,new[]{(int)actor},new[]{1,2});
        ballot.TryCast((int)actor,CivicPreferences.Decide(belief,issue),request.Year);
        procedure=new CivicProcedure(ballot,CivicProcedureKind.AlliancePosition,request.CountryId,targetCountryId:request.SubjectId);return true;
    }
    public bool TryExecute(CivicAllianceMotion motion)
    {
        if(!ReferenceEquals(_world,RealPoliticMod.World) || !_world.Governance.Enabled || motion.Stage!=CivicMotionStage.ExecutionPending
            || !_world.Governance.AllianceMotions.TryGetValue(motion.Id,out var saved) || !ReferenceEquals(saved,motion))return false;
        if(AlreadyApplied(motion))return true;
        if(!StillValid(motion))return false;
        var proposer=Country(motion.ProposerId);var subject=Country(motion.SubjectId);
        var previous=_executing;_executing=motion;
        try
        {
            if(motion.Action==CivicAllianceAction.Admission)
            {
                if(!DiplomacyExecutor.ForceAlliance(proposer,subject))return false;
            }
            else
            {
                if(!AllianceSafety.TryMembership(proposer,out var alliance) || alliance==null)return false;
                // Unknown signatures fail visibly. No fallback to List.Remove, field writes or guessed collection repair.
                if(!GameRefs.TryInvoke(alliance,"leave",out var result,subject,true) || result is bool accepted && !accepted)return false;
            }
            return AlreadyApplied(motion);
        }
        finally{_executing=previous;}
    }
    private bool AlreadyApplied(CivicAllianceMotion motion)
    {
        var proposer=Country(motion.ProposerId);var subject=Country(motion.SubjectId);
        if(proposer==null || subject==null || !AllianceSafety.TryMembership(proposer,out var alliance)
            || !AllianceSafety.TryMembership(subject,out var subjectAlliance))return false;
        // A two-country alliance may dissolve when the target leaves. Confirm the resulting
        // absence for both living countries, not a guessed list repair or invented surviving alliance.
        if(motion.Action==CivicAllianceAction.Expulsion && motion.Members.Count==2 && alliance==null && subjectAlliance==null)return true;
        if(alliance==null || !Members(alliance,out var actual))return false;
        var expected=new HashSet<int>(motion.Members);
        if(motion.Action==CivicAllianceAction.Admission)
        {expected.Add(motion.SubjectId);return ReferenceEquals(subjectAlliance,alliance) && (motion.AllianceId==0 || AllianceSafety.StableId(alliance)==motion.AllianceId) && expected.SetEquals(actual);}
        expected.Remove(motion.SubjectId);
        return subjectAlliance==null && AllianceSafety.StableId(alliance)==motion.AllianceId && expected.SetEquals(actual);
    }
}
