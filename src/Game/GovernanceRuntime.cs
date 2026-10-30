using System;
using System.Collections.Generic;
using RealPolitic.Core;

namespace RealPolitic;

/// <summary>Runs only for an explicitly enabled world. Does not infer newness from world age.</summary>
public sealed class GovernanceRuntime : INationalPositionSource
{
    private readonly PoliticalWorld _world;
    private readonly Func<int,Kingdom> _find;
    public GovernanceRuntime(PoliticalWorld world,Func<int,Kingdom> find){_world=world;_find=find;}
    public static void AnnualTick(PoliticalWorld world,int year,IList<KingdomInput> inputs,Func<int,Kingdom> find)
    {
        if(world?.Governance.Enabled!=true)return;
        var frame=new List<GovernanceNation>();
        foreach(var input in inputs)
        {
            var native=find(input.KingdomId);
            if(native==null || !(GameRefs.Invoke(native,"isCiv") is bool civic))
                throw new InvalidOperationException("Country classification unavailable; governance frame deferred.");
            if(!civic)continue;
            if(world.Governance.Recognition.ContainsCountry(input.KingdomId) && !world.Governance.Recognition.IsAlive(input.KingdomId))
            {
                int cityCount = GameRefs.Invoke(native, "countCities") is int cc ? cc : (native.cities?.Count ?? 0);
                if(cityCount==0 && input.Population<=0)continue;
                world.Governance.Recognition.RestoreAlive(input.KingdomId);
            }
            var state=world.GetKingdom(input.KingdomId);
            if(state?.Constitution?.Electoral==null)throw new InvalidOperationException("Missing enabled country's electoral chapter.");
            var union=world.UnionOf(input.KingdomId);
            var report=world.LastEconomyReport(input.KingdomId);
            frame.Add(new GovernanceNation{CountryId=input.KingdomId,BirthYear=Math.Min(year,Math.Max(0,state.FoundedYear)),Name=input.Name,
                Population=input.Population,Warriors=input.Warriors,Rules=state.Constitution.Electoral,
                Sovereign=union==null || union.Type!=UnionType.Vassal || union.LeaderId==input.KingdomId,
                AnnualIncome=report==null?(double?)null:report.Income});
        }
        GovernanceDispatcher.Tick(world,year,frame,new GovernanceRuntime(world,find));
    }
    /// <summary>Registry pulse, not an annual-income sample. Replaces vanished powers in the same year.</summary>
    public static void ObserveLifecycle(PoliticalWorld world,MapBox map,int year)
    {
        if(world?.Governance.Enabled!=true || year!=world.Governance.LastDispatchYear)return;
        var native=GameRefs.KingdomList(map);if(native==null)return;
        var frame=new List<GovernanceNation>();var live=new Dictionary<int,Kingdom>();var sovereignty=new Dictionary<int,bool>();
        foreach(var country in native)
        {
            if(country==null)continue;
            if(!(GameRefs.Invoke(country,"isCiv") is bool civic))return;
            if(!civic)continue;
            long raw=((NanoObject)country).id;if(raw<=0 || raw>int.MaxValue)return;int id=(int)raw;
            int pop=GameRefs.KingdomPopulation(country);
            if(world.Governance.Recognition.ContainsCountry(id) && !world.Governance.Recognition.IsAlive(id))
            {
                int cityCount = GameRefs.Invoke(country, "countCities") is int cc ? cc : (country.cities?.Count ?? 0);
                if(cityCount==0 && pop<=0)continue;
                world.Governance.Recognition.RestoreAlive(id);
            }
            var state=world.GetKingdom(id);if(state?.Constitution?.Electoral==null)return;
            var union=world.UnionOf(id);bool sovereign=union==null || union.Type!=UnionType.Vassal || union.LeaderId==id;
            if(live.ContainsKey(id))return;live.Add(id,country);sovereignty.Add(id,sovereign);
            frame.Add(new GovernanceNation{CountryId=id,BirthYear=Math.Min(year,Math.Max(0,state.FoundedYear)),Name=GameRefs.ReadProperty(country,"name")?.ToString()??"",
                Population=pop,Warriors=GameRefs.KingdomWarriors(country),Sovereign=sovereign,Rules=state.Constitution.Electoral});
        }
        var previous=world.Governance.Recognition.Capture();bool changed=false;var seen=new HashSet<int>();
        foreach(var country in previous.Countries)
        {
            if(!country.Alive)continue;seen.Add(country.CountryId);
            if(!live.ContainsKey(country.CountryId) || sovereignty[country.CountryId]!=country.Sovereign)changed=true;
            if(world.Governance.Recognition.NeedsRecognitionVote(country.CountryId))changed=true;
        }
        foreach(int id in live.Keys)if(!seen.Contains(id))changed=true;
        if(!changed)return;
        GovernanceDispatcher.Tick(world,year,frame,new GovernanceRuntime(world,id=>live.TryGetValue(id,out var country)?country:null),lifecycleChange:true);
    }
    public static void ObserveBirth(PoliticalWorld world,Kingdom country,int year,int parentId=0,bool lawful=false)
    {
        if(world?.Governance.Enabled!=true || country==null || !(GameRefs.Invoke(country,"isCiv") is bool civic) || !civic)return;
        long id=((NanoObject)country).id;
        if(id<=0 || id>int.MaxValue)throw new InvalidOperationException("Unsupported country identity.");
        if(!world.Governance.Recognition.ContainsCountry((int)id))world.Governance.Recognition.RegisterCountry((int)id,year);
        if(parentId>0)world.Governance.RecordCountryBirth((int)id,parentId,year,lawful);
    }
    public bool TryOpenRecognitionPosition(NationalPositionRequest request,out CivicProcedure procedure)
    {
        procedure=null;
        if(_world==null || !ReferenceEquals(_world,RealPoliticMod.World) || !_world.Governance.Enabled || _find==null)return false;
        var country=_find(request.CountryId);var state=_world.GetKingdom(request.CountryId);
        var target=_world.GetKingdom(request.TargetCountryId);
        if(country==null || ((NanoObject)country).id!=request.CountryId || state==null || target==null || !ReferenceEquals(state.Constitution?.Electoral,request.Rules))return false;
        var issue=CivicForeignInterests.Question(_world,state,target,true);
        if(request.Rules.ForeignDecisions==ForeignDecisionAuthority.Electorate)
            return CivicElectorateReader.TryOpenIssue(_world,country,null,request.Rules,issue,request.ProcedureId,
                CivicProcedureKind.RecognitionPosition,request.TargetCountryId,request.Year,out procedure,out _);
        var ruler=GameRefs.ReadRuler(country);
        if(ruler==null || !(GameRefs.Invoke(ruler,"isAlive") is bool alive) || !alive)return false;
        long id=GameText.ActorLongId(ruler);if(id<=0 || id>int.MaxValue)return false;
        var belief=ActorIdeologies.Get(ruler);if(belief==null)return false;
        var ballot=new CivicBallot(request.ProcedureId,BallotElectorKind.Person,request.Year,0,
            new[]{(int)id},new[]{CivicBallot.Yes,CivicBallot.No});
        issue.IsRulingFamily=true;ballot.TryCast((int)id,CivicPreferences.Decide(belief,issue),request.Year);
        procedure=new CivicProcedure(ballot,CivicProcedureKind.RecognitionPosition,request.CountryId,targetCountryId:request.TargetCountryId);
        return true;
    }
}
