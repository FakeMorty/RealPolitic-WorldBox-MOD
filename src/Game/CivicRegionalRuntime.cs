using System;
using System.Collections.Generic;
using RealPolitic.Core;

namespace RealPolitic;

/// <summary>One-city lawful exit; ballots and cooldowns live in the persisted governance aggregate.</summary>
public static class CivicRegionalRuntime
{
    private static bool ValidRegion(Kingdom country,City city)
    {
        var cities=GameRefs.KingdomCities(country);var capital=GameRefs.KingdomCapital(country);
        return country!=null && city!=null && ReferenceEquals(city.kingdom,country) && cities!=null && cities.Count>=2
            && cities.Contains(city) && capital!=null && !ReferenceEquals(capital,city);
    }
    private static bool HistoricalBlocked(PoliticalWorld world,int countryId,City city)
        => city!=null && world.IsAbsorbedHomelandCity(countryId,(int)((NanoObject)city).id);
    private static bool HasActive(PoliticalWorld world,int city)
    {
        foreach(var p in world.Governance.Procedures.Values)
            if(p.CityId==city && (p.Kind==CivicProcedureKind.Independence || p.Kind==CivicProcedureKind.CentralPermission)
                && (p.Stage==CivicProcedureStage.Voting || p.Stage==CivicProcedureStage.Approved || p.Stage==CivicProcedureStage.ExecutionPending))return true;
        return false;
    }
    public static bool RequestCrisis(PoliticalWorld world,MapBox map,int countryId,IList<int> proposed,int year)
    {
        if(world?.Governance.Enabled!=true || !ReferenceEquals(world,RealPoliticMod.World) || !world.Params.AllowSecession)return false;
        var country=CivilWarExecutor.FindKingdom(map,countryId);var owned=GameRefs.KingdomCities(country);if(owned==null)return false;
        City candidate=null;
        foreach(int id in proposed)foreach(var city in new List<City>(owned))if(city!=null && ((NanoObject)city).id==id && ValidRegion(country,city)){candidate=city;break;}
        if(candidate==null)return false;
        var law=world.GetKingdom(countryId)?.Constitution?.Electoral;if(law==null)return false;
        // Земли исчезнувшей/поглощённой державы нельзя мирно вернуть референдумом:
        // кризис для них может перерасти только в вооружённое восстание.
        if(law.RegionalExit!=RegionalExitLaw.Forbidden && !HistoricalBlocked(world,countryId,candidate))return Request(world,country,candidate,year);
        if(!DiplomacyExecutor.TrySecedeCity(country,candidate,out var newborn)){Announce(world,country,candidate,year,"RP_GOV26_REGION_REBELLION_REFUSED");return false;}
        bool war=DiplomacyExecutor.AreAtWar(country,newborn);
        if(!war){DiplomacyExecutor.TryDeclareWar(newborn,country);war=DiplomacyExecutor.AreAtWar(country,newborn);}
        Announce(world,country,candidate,year,war?"RP_GOV26_REGION_REBELLION":"RP_GOV26_REGION_REBELLION_NO_WAR",(int)((NanoObject)newborn).id);
        return true;
    }
    public static bool Request(PoliticalWorld world,Kingdom country,City city,int year)
    {
        if(world?.Governance.Enabled!=true || !ReferenceEquals(world,RealPoliticMod.World) || !world.Params.AllowSecession || !ValidRegion(country,city))return false;
        int countryId=(int)((NanoObject)country).id,cityId=(int)((NanoObject)city).id;
        if(world.IsAbsorbedHomelandCity(countryId,cityId))return false;
        var law=world.GetKingdom(countryId)?.Constitution?.Electoral;
        if(law==null || law.RegionalExit==RegionalExitLaw.Forbidden)return false;
        if(HasActive(world,cityId))return true;
        if(year<world.Governance.InitiativeFor(cityId).NextPermittedYear)return true;
        string id="regional:"+countryId+":"+cityId+":"+year;
        if(law.RegionalExit==RegionalExitLaw.RegionalRight)
            return OpenRegional(world,country,city,law,id,year,false,RegionalExitLaw.RegionalRight,"");
        var state=world.GetKingdom(countryId);
        var issue=Issue(world,state,city);CivicProcedure permission;
        if(law.ForeignDecisions==ForeignDecisionAuthority.Electorate)
        {
            if(!CivicElectorateReader.TryOpenIssue(world,country,null,law,issue,id,CivicProcedureKind.CentralPermission,0,year,
                out permission,out _,affectedCityId:cityId,openingExitLaw:RegionalExitLaw.WithCentralPermission))return false;
        }
        else
        {
            var ruler=GameRefs.ReadRuler(country);
            if(ruler==null || !(GameRefs.Invoke(ruler,"isAlive") is bool alive) || !alive)return false;
            long actor=GameText.ActorLongId(ruler);if(actor<=0 || actor>int.MaxValue)return false;
            var belief=ActorIdeologies.Get(ruler);if(belief==null)return false;
            issue.IsRulingFamily=true;
            var ballot=new CivicBallot(id,BallotElectorKind.Person,year,0,new[]{(int)actor},new[]{1,2});
            ballot.TryCast((int)actor,CivicPreferences.Decide(belief,issue),year);
            permission=new CivicProcedure(ballot,CivicProcedureKind.CentralPermission,countryId,cityId,
                openingExitLaw:RegionalExitLaw.WithCentralPermission);
            world.Governance.AddProcedure(permission);
        }
        Announce(world,country,city,year,"RP_GOV26_REGION_PERMISSION");return true;
    }
    private static CivicIssueContext Issue(PoliticalWorld world,KingdomPolitics state,City city)
        =>new CivicIssueContext{CurrentGovernment=state.Ideology,ProposedGovernment=world.GetCity((int)((NanoObject)city).id)?.Dominant??state.Ideology,Independence=true};
    private static bool OpenRegional(PoliticalWorld world,Kingdom country,City city,ElectoralChapter law,string id,int year,bool permission,
        RegionalExitLaw? openingExitLaw=null,string authorizedByProcedureId="")
    {
        var state=world.GetKingdom((int)((NanoObject)country).id);
        if(!CivicElectorateReader.TryOpenIssue(world,country,city,law,Issue(world,state,city),id,CivicProcedureKind.Independence,0,year,
            out _,out _,centralPermission:permission,openingExitLaw:openingExitLaw,authorizedByProcedureId:authorizedByProcedureId))return false;
        Announce(world,country,city,year,"RP_GOV26_REGION_OPENED");return true;
    }
    private static bool HasLegacyCentralPermission(PoliticalWorld world,CivicProcedure regional)
    {
        const string suffix=":referendum";
        if(!regional.Ballot.Id.EndsWith(suffix,StringComparison.Ordinal))return false;
        string id=regional.Ballot.Id.Substring(0,regional.Ballot.Id.Length-suffix.Length);
        return world.Governance.Procedures.TryGetValue(id,out var permission)
            && permission.Kind==CivicProcedureKind.CentralPermission
            && permission.CountryId==regional.CountryId && permission.CityId==regional.CityId
            && permission.Stage==CivicProcedureStage.Executed && permission.Ballot.Closed
            && permission.Ballot.Tally().BinaryAccepted
            && permission.Ballot.DeadlineYear<=regional.Ballot.OpenedYear;
    }
    private static bool HasValidExitBasis(PoliticalWorld world,CivicProcedure p,ElectoralChapter law)
    {
        if(law==null)return false;
        if(p.Kind==CivicProcedureKind.CentralPermission)
            return p.OpeningExitLaw.HasValue
                ? p.OpeningExitLaw.Value==RegionalExitLaw.WithCentralPermission
                : law.RegionalExit==RegionalExitLaw.WithCentralPermission;
        if(p.Kind==CivicProcedureKind.Independence)
        {
            if(p.OpeningExitLaw.HasValue)
            {
                if(p.OpeningExitLaw.Value==RegionalExitLaw.RegionalRight)
                    return string.IsNullOrEmpty(p.AuthorizedByProcedureId);
                if(p.OpeningExitLaw.Value==RegionalExitLaw.WithCentralPermission)
                    return p.HasVerifiedAuthorization(world.Governance.Procedures);
                return false;
            }
            // Legacy v2 procedure without persisted opening basis: cannot invent permission if current law requires more.
            if(law.RegionalExit==RegionalExitLaw.RegionalRight)return true;
            if(law.RegionalExit==RegionalExitLaw.WithCentralPermission)return HasLegacyCentralPermission(world,p);
            return false;
        }
        return false;
    }
    public static void AnnualTick(PoliticalWorld world,MapBox map,int year)
    {
        if(world?.Governance.Enabled!=true || !ReferenceEquals(world,RealPoliticMod.World) || !world.Params.AllowSecession)return;
        var list=GameRefs.KingdomList(map);if(list==null)return;
        var countries=new Dictionary<int,Kingdom>();var cities=new Dictionary<int,City>();
        foreach(var country in list)
        {
            if(country==null)continue;countries[(int)((NanoObject)country).id]=country;
            var owned=GameRefs.KingdomCities(country);if(owned!=null)foreach(var city in owned)if(city!=null)cities[(int)((NanoObject)city).id]=city;
        }
        var procedures=new List<CivicProcedure>(world.Governance.Procedures.Values);procedures.Sort((a,b)=>string.CompareOrdinal(a.Ballot.Id,b.Ballot.Id));
        foreach(var p in procedures)
        {
            if(p.Kind!=CivicProcedureKind.Independence && p.Kind!=CivicProcedureKind.CentralPermission)continue;
            if(p.Stage!=CivicProcedureStage.Voting && p.Stage!=CivicProcedureStage.Approved && p.Stage!=CivicProcedureStage.ExecutionPending)continue;
            countries.TryGetValue(p.CountryId,out var country);cities.TryGetValue(p.CityId,out var city);
            var law=world.GetKingdom(p.CountryId)?.Constitution?.Electoral;
            // A changed owner/capital or missing opening legal basis invalidates the action, never retries a different city.
            // Lawfully opened procedures retain their opening legal basis even if RegionalExit is amended later.
            if(law==null || !ValidRegion(country,city) || world.IsAbsorbedHomelandCity(p.CountryId,p.CityId) || !HasValidExitBasis(world,p,law))
            {
                p.Transition(p.Stage==CivicProcedureStage.ExecutionPending?CivicProcedureStage.ExecutionFailed:CivicProcedureStage.Cancelled,"region-or-law-changed");
                world.Governance.InitiativeFor(p.CityId).RecordFailedReferendum(year);Announce(world,country,city,year,"RP_GOV26_REGION_INVALID");continue;
            }
            // Closed is not a waiver of the constitutional duration, including after restore.
            if(year<p.Ballot.DeadlineYear)continue;
            if(p.Stage==CivicProcedureStage.Voting)
            {
                if(!p.Ballot.Closed && !p.Ballot.TryClose(year,out _))continue;
                bool yes=p.Ballot.Tally().BinaryAccepted;p.Transition(yes?CivicProcedureStage.Approved:CivicProcedureStage.Rejected);
                if(!yes){world.Governance.InitiativeFor(p.CityId).RecordFailedReferendum(year);Announce(world,country,city,year,p.Kind==CivicProcedureKind.CentralPermission?"RP_GOV26_REGION_PERMISSION_REFUSED":"RP_GOV26_REGION_REJECTED");continue;}
            }
            if(p.Kind==CivicProcedureKind.CentralPermission)
            {
                if(OpenRegional(world,country,city,law,p.Ballot.Id+":referendum",year,true,RegionalExitLaw.WithCentralPermission,p.Ballot.Id))
                {p.Transition(CivicProcedureStage.ExecutionPending);p.Transition(CivicProcedureStage.Executed,"central-permission-used");}
                continue;
            }
            if(p.Stage==CivicProcedureStage.Approved)p.Transition(CivicProcedureStage.ExecutionPending,"lawful-exit-intent");
            // makeOwnKingdom receives pRebellion=false. Never add a declaration of war to a lawful result.
            if(!DiplomacyExecutor.TryCarveKingdom(country,city,out var newborn))
            {
                p.Transition(CivicProcedureStage.ExecutionFailed,"native-exit-refused");
                world.Governance.InitiativeFor(p.CityId).RecordFailedReferendum(year);Announce(world,country,city,year,"RP_GOV26_REGION_NATIVE_FAILED");continue;
            }
            int newbornId=(int)((NanoObject)newborn).id;
            if(world.GetKingdom(newbornId)==null)world.AddKingdom(newbornId,Issue(world,world.GetKingdom(p.CountryId),city).ProposedGovernment,year);
            bool peace=!DiplomacyExecutor.AreAtWar(country,newborn);
            if(!peace)peace=DiplomacyExecutor.TryMakePeace(country,newborn) && !DiplomacyExecutor.AreAtWar(country,newborn);
            p.Transition(peace?CivicProcedureStage.Executed:CivicProcedureStage.ExecutionFailed,"new-country:"+newbornId+(peace?":lawful":":unexpected-war"));
            Announce(world,country,city,year,peace?"RP_GOV26_REGION_EXECUTED":"RP_GOV26_REGION_WAR_FAILED",newbornId);
        }
        foreach(var country in list)
        {
            if(country==null || !(GameRefs.Invoke(country,"isCiv") is bool civic) || !civic)continue;
            var state=world.GetKingdom((int)((NanoObject)country).id);var law=state?.Constitution?.Electoral;
            var owned=GameRefs.KingdomCities(country);if(law==null || owned==null)continue;
            foreach(var city in new List<City>(owned))
            {
                if(!ValidRegion(country,city))continue;
                int id=(int)((NanoObject)city).id;if(world.IsAbsorbedHomelandCity((int)((NanoObject)country).id,id))continue;var initiative=world.Governance.InitiativeFor(id);
                if(initiative.LoyaltyThreshold!=world.Params.RegionalObedienceThreshold || initiative.RequiredYears!=world.Params.RegionalLowYears)
                    initiative.LowLoyaltyYears=0;
                initiative.LoyaltyThreshold=world.Params.RegionalObedienceThreshold;initiative.RequiredYears=world.Params.RegionalLowYears;initiative.RetryYears=world.Params.RegionalRetryYears;
                if(!CivicElectorateReader.TryReadRegionCitizens(world,country,city,law,out var people,out _) || people.Count==0)continue;
                double sum=0;foreach(var person in people)sum+=ActorIdeologyRoll.Obedience(person.Belief.Ideology,state.Ideology,person.Belief.Conviction,person.Belief.Dissent);
                if(initiative.Observe(year,(float)(sum/people.Count),owned.Count,false) && !HasActive(world,id))Request(world,country,city,year);
            }
        }
    }
    private static void Announce(PoliticalWorld world,Kingdom country,City city,int year,string outcome,int newborn=0)
        =>world.Bus.Publish(new PolEvent(PolEventType.RegionalCivicProcedure,"RP_GOV26_EV_REGION")
            {Year=year,KingdomId=country==null?(int?)null:(int)((NanoObject)country).id,CityId=city==null?(int?)null:(int)((NanoObject)city).id,Severity=Severity.Major}
            .With("kingdom",country==null?"?":GameRefs.ReadProperty(country,"name")?.ToString()??"?")
            .With("city",city==null?"?":GameRefs.ReadProperty(city,"name")?.ToString()??"?").With("outcome",outcome).With("new_country",newborn));
}
