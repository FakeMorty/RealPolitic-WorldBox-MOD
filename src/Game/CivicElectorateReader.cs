using System;
using System.Collections;
using System.Collections.Generic;
using RealPolitic.Core;

namespace RealPolitic;

/// <summary>Reads actual living citizens; unknown API state defers a ballot instead of shrinking its roll.</summary>
public static class CivicElectorateReader
{
    public sealed class Elector
    {
        public Actor Actor;
        public CivicPerson Person;
        public CivicBelief Belief;
    }
    public static bool TryRead(PoliticalWorld world,Kingdom country,City region,ElectoralChapter rules,out List<Elector> electors,out string reason)
    {
        bool success=TryReadCore(world,country,region,rules,out electors,out reason,true);
        if(!success)electors.Clear();return success;
    }
    public static bool TryReadCitizens(PoliticalWorld world,Kingdom country,ElectoralChapter rules,out List<Elector> electors,out string reason)
    {
        bool success=TryReadCore(world,country,null,rules,out electors,out reason,false);if(!success)electors.Clear();return success;
    }
    public static bool TryReadRegionCitizens(PoliticalWorld world,Kingdom country,City region,ElectoralChapter rules,out List<Elector> electors,out string reason)
    {
        bool success=TryReadCore(world,country,region,rules,out electors,out reason,false);if(!success)electors.Clear();return success;
    }
    private static bool TryReadCore(PoliticalWorld world,Kingdom country,City region,ElectoralChapter rules,out List<Elector> electors,out string reason,bool activeFranchise)
    {
        electors=new List<Elector>();reason="";
        if(world==null || !ReferenceEquals(world,RealPoliticMod.World) || !world.Governance.Enabled || country==null || rules==null)
        {reason="governance-disabled-or-wrong-world";return false;}
        try
        {
            rules.Validate();long countryId=((NanoObject)country).id;
            if(countryId<=0 || countryId>int.MaxValue){reason="unsupported-country-id";return false;}
            if(region!=null && !ReferenceEquals(region.kingdom,country)){reason="region-not-owned";return false;}
            var units=GameRefs.ReadMember(region!=null ? (object)region : country,"units") as IEnumerable;
            if(units==null){reason="resident-list-unavailable";return false;}
            var state=world.GetKingdom((int)countryId);if(state==null){reason="unknown-political-state";return false;}
            if(!ReferenceEquals(state.Constitution?.Electoral,rules)){reason="non-authoritative-electoral-rules";return false;}
            long royalClan=state.RoyalClanId;
            var ruler=GameRefs.ReadRuler(country);
            if(royalClan<=0 && ruler!=null)royalClan=GameRefs.ActorClanId(ruler);
            var leaders=new HashSet<long>();var cities=GameRefs.KingdomCities(country);
            if(cities==null){reason="city-leaders-unavailable";return false;}
            foreach(var city in cities)if(city?.leader!=null)leaders.Add(GameText.ActorLongId(city.leader));
            var seen=new Dictionary<int,Actor>();
            foreach(object unit in units)
            {
                if(!(unit is Actor actor)){if(unit==null)continue;reason="invalid-resident-entry";return false;}
                if(actor.data==null)continue; // disposed actors have no valid citizenship or identity
                if(GameRefs.IsBoatUnit(actor))continue; // boats in Kingdom.units / City.units are vessels, not voters
                if(!ReferenceEquals(actor.kingdom,country))continue; // a foreign visitor does not vote
                if(region!=null && actor.city!=null && !ReferenceEquals(actor.city,region))continue;
                long id=GameText.ActorLongId(actor);
                if(id<=0 || id>int.MaxValue){reason="unsupported-actor-id";return false;}
                if(seen.TryGetValue((int)id,out var previous))
                {if(!ReferenceEquals(previous,actor)){reason="conflicting-actor-identity";return false;}continue;}
                seen.Add((int)id,actor);
                object life=GameRefs.Invoke(actor,"isAlive");if(!(life is bool alive)){reason="life-state-unavailable";return false;}
                if(!alive)continue;
                object age=GameRefs.Invoke(actor,"getAge")??GameRefs.ReadMember(actor,"age");
                double years=age is int ai ? ai : age is float af ? af : age is double ad ? ad : -1;
                if(double.IsNaN(years)||double.IsInfinity(years)||years<0||years>int.MaxValue){reason="age-unavailable";return false;}
                bool family=royalClan>0 && GameRefs.ActorClanId(actor)==royalClan;
                var person=new CivicPerson{ActorId=(int)id,Age=(int)years,Alive=true,Citizen=true,
                    IsRulingClan=family,IsNotable=family,IsCityLeader=leaders.Contains(id)};
                if(activeFranchise && !rules.MayVote(person))continue;
                var belief=ActorIdeologies.Get(actor);if(belief==null){reason="personal-position-unavailable";return false;}
                electors.Add(new Elector{Actor=actor,Person=person,Belief=belief});
            }
            electors.Sort((a,b)=>a.Person.ActorId.CompareTo(b.Person.ActorId));return true;
        }
        catch(Exception ex){reason="electorate-read-failed: "+ex.Message;electors.Clear();return false;}
    }
    /// <summary>Creates and fills a saved issue ballot. This does not execute its political outcome.</summary>
    public static bool TryOpenIssue(PoliticalWorld world,Kingdom country,City region,ElectoralChapter rules,CivicIssueContext issue,
        string id,CivicProcedureKind kind,int targetCountryId,int year,out CivicProcedure procedure,out string reason,
        bool centralPermission=false,int affectedCityId=0,RegionalExitLaw? openingExitLaw=null,string authorizedByProcedureId="")
    {
        procedure=null;
        if(issue==null || kind==CivicProcedureKind.HeadElection){reason="invalid-issue";return false;}
        if(kind==CivicProcedureKind.Independence)
        {
            var effectiveLaw=openingExitLaw??rules?.RegionalExit??RegionalExitLaw.Forbidden;
            bool legalExit=effectiveLaw==RegionalExitLaw.RegionalRight
                || effectiveLaw==RegionalExitLaw.WithCentralPermission && centralPermission;
            if(country==null || region==null || rules==null || !legalExit) {reason="no-lawful-exit";return false;}
            var capital=GameRefs.KingdomCapital(country);var cities=GameRefs.KingdomCities(country);
            if(capital==null || ReferenceEquals(capital,region) || cities==null || cities.Count<2) {reason="invalid-breakaway-region";return false;}
        }
        if(!TryRead(world,country,region,rules,out var people,out reason))return false;
        try
        {
            if(world.Governance.Procedures.ContainsKey(id)){reason="procedure-already-exists";return false;}
            var ids=new List<int>();foreach(var person in people)ids.Add(person.Person.ActorId);
            var ballot=new CivicBallot(id,BallotElectorKind.Person,year,rules.BallotYears,ids,new[]{CivicBallot.Yes,CivicBallot.No},rules.ReferendumQuorumPercent,rules.ReferendumThreshold);
            var choices=new Dictionary<int,int>();
            foreach(var person in people)
            {
                var context=new CivicIssueContext{CurrentGovernment=issue.CurrentGovernment,ProposedGovernment=issue.ProposedGovernment,
                    StrategicInterest=issue.StrategicInterest,Independence=issue.Independence,ReverseChoice=issue.ReverseChoice,RecognitionOfOurBreakaway=issue.RecognitionOfOurBreakaway,
                    IsRulingFamily=person.Person.IsRulingClan,IsCityLeader=person.Person.IsCityLeader};
                choices.Add(person.Person.ActorId,CivicPreferences.Decide(person.Belief,context));
            }
            // Collect all decisions only after the entire roll and context have validated successfully.
            ballot.Collect(year,actorId=>choices[actorId]);
            procedure=new CivicProcedure(ballot,kind,(int)((NanoObject)country).id,region==null?affectedCityId:(int)((NanoObject)region).id,
                targetCountryId,openingExitLaw:openingExitLaw,authorizedByProcedureId:authorizedByProcedureId);
            world.Governance.AddProcedure(procedure);return true;
        }
        catch(Exception ex){reason="ballot-open-failed: "+ex.Message;procedure=null;return false;}
    }
}
