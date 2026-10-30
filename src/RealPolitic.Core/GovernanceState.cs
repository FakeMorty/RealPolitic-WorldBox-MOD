using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace RealPolitic.Core;

public enum CivicProcedureKind { Independence, RecognitionPosition, AlliancePosition, HeadElection, CentralPermission }
public enum CivicProcedureStage { Voting, Approved, Rejected, ExecutionPending, Executed, ExecutionFailed, Cancelled }

public sealed class CivicProcedure
{
    public CivicBallot Ballot {get;}
    public CivicProcedureKind Kind {get;}
    public int CountryId {get;}
    public int CityId {get;}
    public int TargetCountryId {get;}
    public RegionalExitLaw? OpeningExitLaw {get;}
    public string AuthorizedByProcedureId {get;}="";
    public CivicProcedureStage Stage {get;private set;}
    public string ExecutionNote {get;private set;}="";
    public CivicProcedure(CivicBallot ballot,CivicProcedureKind kind,int countryId,int cityId=0,int targetCountryId=0,
        CivicProcedureStage stage=CivicProcedureStage.Voting,string executionNote="",
        RegionalExitLaw? openingExitLaw=null,string authorizedByProcedureId="")
    {
        if(ballot==null || countryId<=0 || cityId<0 || targetCountryId<0 || !Enum.IsDefined(typeof(CivicProcedureKind),kind)
            || !Enum.IsDefined(typeof(CivicProcedureStage),stage))throw new ArgumentException("Invalid civic procedure.");
        if(ballot.ElectorKind!=BallotElectorKind.Person)throw new ArgumentException("A national constitutional procedure needs individual persons, not country votes.");
        if(ballot.AllowSingleCandidate && kind!=CivicProcedureKind.HeadElection)throw new ArgumentException("Candidate-only ballot on an issue procedure.");
        if((kind==CivicProcedureKind.Independence || kind==CivicProcedureKind.CentralPermission) && cityId<=0 && kind==CivicProcedureKind.Independence)
            throw new ArgumentException("Independence needs a city.");
        string authId=authorizedByProcedureId??"";
        if(openingExitLaw.HasValue)
        {
            if(!Enum.IsDefined(typeof(RegionalExitLaw),openingExitLaw.Value) || openingExitLaw.Value==RegionalExitLaw.Forbidden)
                throw new ArgumentException("Invalid opening regional exit law.");
            if(kind!=CivicProcedureKind.CentralPermission && kind!=CivicProcedureKind.Independence)
                throw new ArgumentException("Opening exit law is only valid for regional exit procedures.");
            if(cityId<=0)throw new ArgumentException("Regional exit procedure with an opening basis requires a city.");
            if(kind==CivicProcedureKind.CentralPermission
                && (openingExitLaw.Value!=RegionalExitLaw.WithCentralPermission || authId.Length>0))
                throw new ArgumentException("Invalid central permission opening basis.");
            if(kind==CivicProcedureKind.Independence && openingExitLaw.Value==RegionalExitLaw.RegionalRight && authId.Length>0)
                throw new ArgumentException("Regional right procedure cannot reference a central permission procedure.");
            if(kind==CivicProcedureKind.Independence && openingExitLaw.Value==RegionalExitLaw.WithCentralPermission
                && string.IsNullOrWhiteSpace(authId))
                throw new ArgumentException("Central permission exit basis requires an authorizing procedure ID.");
        }
        else if(authId.Length>0)
            throw new ArgumentException("Authorizing procedure ID requires an explicit opening exit law.");
        if(stage!=CivicProcedureStage.Voting && stage!=CivicProcedureStage.Cancelled && !ballot.Closed)throw new ArgumentException("An unfinished ballot has no executable result.");
        Ballot=ballot;Kind=kind;CountryId=countryId;CityId=cityId;TargetCountryId=targetCountryId;
        OpeningExitLaw=openingExitLaw;AuthorizedByProcedureId=authId;Stage=stage;ExecutionNote=executionNote??"";
        if(stage==CivicProcedureStage.Rejected && SuccessfulResult || stage!=CivicProcedureStage.Voting && stage!=CivicProcedureStage.Cancelled && stage!=CivicProcedureStage.Rejected && !SuccessfulResult)
            throw new ArgumentException("Saved execution stage contradicts the recorded votes.");
    }
    public bool HasVerifiedAuthorization(IReadOnlyDictionary<string,CivicProcedure> procedures)
    {
        if(string.IsNullOrEmpty(AuthorizedByProcedureId) || procedures==null)return false;
        return procedures.TryGetValue(AuthorizedByProcedureId,out var permission)
            && permission!=null
            && permission.Kind==CivicProcedureKind.CentralPermission
            && permission.CountryId==CountryId && permission.CityId==CityId
            && permission.Stage==CivicProcedureStage.Executed && permission.Ballot.Closed
            && permission.Ballot.Tally().BinaryAccepted
            && permission.Ballot.DeadlineYear<=Ballot.OpenedYear;
    }
    private bool SuccessfulResult => Ballot.Closed && (Kind==CivicProcedureKind.HeadElection ? Ballot.Tally().UniquePluralityWinner>0 : Ballot.Tally().BinaryAccepted);
    public bool Transition(CivicProcedureStage next,string note="")
    {
        bool allowed=Stage==CivicProcedureStage.Voting && Ballot.Closed && (next==CivicProcedureStage.Approved && SuccessfulResult || next==CivicProcedureStage.Rejected && !SuccessfulResult)
            || Stage==CivicProcedureStage.Approved && next==CivicProcedureStage.ExecutionPending
            || Stage==CivicProcedureStage.ExecutionPending && (next==CivicProcedureStage.Executed || next==CivicProcedureStage.ExecutionFailed)
            || (Stage==CivicProcedureStage.Voting || Stage==CivicProcedureStage.Approved) && next==CivicProcedureStage.Cancelled;
        if(!allowed)return false;Stage=next;ExecutionNote=note??"";return true;
    }
}

/// <summary>Persisted aggregate. A missing block means legacy, never a new world inferred from its age.</summary>
public sealed class GovernanceState
{
    public InternationalRecognition Recognition {get;}
    public string WorldKey {get;}
    public bool Enabled=>Recognition.EnabledForNewWorld;
    public int LastDispatchYear {get;private set;}=-1;
    internal void RestoreDispatchYear(int year)
    {if(year< -1 || !Enabled && year!= -1)throw new ArgumentException("Invalid governance dispatch year.");LastDispatchYear=year;}
    private readonly Dictionary<string,CivicProcedure> _procedures=new Dictionary<string,CivicProcedure>(StringComparer.Ordinal);
    private readonly Dictionary<string,CivicAllianceMotion> _motions=new Dictionary<string,CivicAllianceMotion>(StringComparer.Ordinal);
    public IReadOnlyDictionary<string,CivicAllianceMotion> AllianceMotions=>new ReadOnlyDictionary<string,CivicAllianceMotion>(_motions);
    public void AddAllianceMotion(CivicAllianceMotion motion)
    {
        if(!Enabled || motion==null || _motions.ContainsKey(motion.Id))throw new ArgumentException("Invalid or repeated alliance motion.");
        motion.Validate();_motions.Add(motion.Id,motion);
    }
    private readonly Dictionary<int,CivicCountryBirth> _births=new Dictionary<int,CivicCountryBirth>();
    public IReadOnlyDictionary<int,CivicCountryBirth> CountryBirths=>new ReadOnlyDictionary<int,CivicCountryBirth>(_births);
    public void RecordCountryBirth(int country,int parent,int year,bool lawful)
    {
        if(!Enabled || country<=0 || parent<=0 || country==parent || year<0 || !Recognition.ContainsCountry(country) || !Recognition.ContainsCountry(parent))
            throw new ArgumentException("Invalid country birth context.");
        foreach(var entry in Recognition.Capture().Countries)
            if(entry.CountryId==country && entry.BirthYear!=year || entry.CountryId==parent && entry.BirthYear>year)
                throw new ArgumentException("Inconsistent country birth year.");
        int ancestor=parent;var visited=new HashSet<int>{country};
        while(true){if(!visited.Add(ancestor))throw new ArgumentException("Cyclic country ancestry.");if(!_births.TryGetValue(ancestor,out var origin))break;ancestor=origin.ParentId;}
        if(_births.TryGetValue(country,out var previous))
        {if(previous.ParentId==parent && previous.Year==year && previous.Lawful==lawful)return;throw new ArgumentException("Conflicting country birth context.");}
        _births.Add(country,new CivicCountryBirth{CountryId=country,ParentId=parent,Year=year,Lawful=lawful});
    }
    private readonly Dictionary<int,CivicOfficeState> _offices=new Dictionary<int,CivicOfficeState>();
    public IReadOnlyDictionary<int,CivicOfficeState> Offices=>new ReadOnlyDictionary<int,CivicOfficeState>(_offices);
    public CivicOfficeState OfficeFor(int countryId)
    {
        if(!Enabled || countryId<=0)throw new ArgumentException("Invalid elected office.");
        if(!_offices.TryGetValue(countryId,out var office))_offices.Add(countryId,office=new CivicOfficeState{CountryId=countryId});return office;
    }
    internal void RestoreOffice(CivicOfficeState office)
    {
        if(!Enabled || office==null || _offices.ContainsKey(office.CountryId))throw new ArgumentException("Invalid or repeated office.");
        office.Validate();_offices.Add(office.CountryId,office);
    }
    private readonly Dictionary<int,RegionalInitiative> _initiatives=new Dictionary<int,RegionalInitiative>();
    private readonly Dictionary<int,CivicBelief> _beliefs=new Dictionary<int,CivicBelief>();
    public IReadOnlyDictionary<int,CivicBelief> Beliefs=>new ReadOnlyDictionary<int,CivicBelief>(_beliefs);
    private readonly Dictionary<int,SortedDictionary<int,double>> _income=new Dictionary<int,SortedDictionary<int,double>>();
    public IReadOnlyDictionary<string,CivicProcedure> Procedures=>new ReadOnlyDictionary<string,CivicProcedure>(_procedures);
    public IReadOnlyDictionary<int,RegionalInitiative> Initiatives=>new ReadOnlyDictionary<int,RegionalInitiative>(_initiatives);
    public GovernanceState():this(new InternationalRecognition(false),""){}
    internal GovernanceState(InternationalRecognition recognition,string worldKey)
    {
        if(recognition==null || recognition.EnabledForNewWorld && string.IsNullOrWhiteSpace(worldKey))throw new ArgumentException("A new-world regime needs its own persistent world key.");
        Recognition=recognition;WorldKey=worldKey??"";
    }
    public static GovernanceState ForNewWorld(string worldKey)=>new GovernanceState(new InternationalRecognition(true),worldKey);
    public void RememberBelief(int actorId,CivicBelief belief)
    {
        if(!Enabled || actorId<=0 || belief==null)throw new ArgumentException("Invalid belief owner.");
        belief.Validate();_beliefs[actorId]=belief;
    }
    public void AddProcedure(CivicProcedure procedure)
    {
        if(!Enabled || procedure==null || _procedures.ContainsKey(procedure.Ballot.Id))throw new ArgumentException("Procedure is disabled or repeated.");
        _procedures.Add(procedure.Ballot.Id,procedure);
    }
    public RegionalInitiative InitiativeFor(int cityId)
    {
        if(!Enabled || cityId<=0)throw new ArgumentException("Invalid initiative.");
        if(!_initiatives.TryGetValue(cityId,out var state))_initiatives.Add(cityId,state=new RegionalInitiative{CityId=cityId});return state;
    }
    internal void RestoreInitiative(RegionalInitiative state)
    {
        if(!Enabled || state==null || state.CityId<=0 || _initiatives.ContainsKey(state.CityId) || state.LowLoyaltyYears<0
            || state.LastObservedYear< -1 || state.NextPermittedYear<0 || state.RequiredYears<1 || state.RetryYears<0
            || float.IsNaN(state.LoyaltyThreshold)||float.IsInfinity(state.LoyaltyThreshold)||state.LoyaltyThreshold<=0 || state.LoyaltyThreshold>1)
            throw new ArgumentException("Invalid saved regional initiative.");
        _initiatives.Add(state.CityId,state);
    }
    public void RecordIncome(int countryId,int year,double amount)
    {
        if(!Enabled || countryId<=0 || year<0 || double.IsNaN(amount)||double.IsInfinity(amount))throw new ArgumentException("Invalid annual income.");
        if(!_income.TryGetValue(countryId,out var years))_income.Add(countryId,years=new SortedDictionary<int,double>());
        // One observation per year; loading or updating twice must not rewrite the historical input.
        if(years.ContainsKey(year))return;
        years.Add(year,amount);
        while(years.Count>5){var e=years.GetEnumerator();e.MoveNext();years.Remove(e.Current.Key);}
    }
    public IReadOnlyDictionary<int,SortedDictionary<int,double>> CaptureIncome()
    {
        var copy=new Dictionary<int,SortedDictionary<int,double>>();foreach(var p in _income)copy.Add(p.Key,new SortedDictionary<int,double>(p.Value));
        return new ReadOnlyDictionary<int,SortedDictionary<int,double>>(copy);
    }
    public void FillIncome(NationalPowerSnapshot power)
    {
        if(power==null)throw new ArgumentNullException(nameof(power));power.RecentAnnualIncome.Clear();
        if(_income.TryGetValue(power.CountryId,out var years))foreach(double amount in years.Values)power.RecentAnnualIncome.Add(amount);
    }
}
