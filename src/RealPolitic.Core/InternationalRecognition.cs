using System;
using System.Collections.Generic;

namespace RealPolitic.Core;

public sealed class NationalPowerSnapshot
{
    public int CountryId;
    public int Warriors;
    public int Population;
    public readonly List<double> RecentAnnualIncome=new List<double>();
    public double AverageIncome
    {
        get
        {
            double sum=0;int count=0;
            for(int i=Math.Max(0,RecentAnnualIncome.Count-5);i<RecentAnnualIncome.Count;i++)
            {double x=RecentAnnualIncome[i];if(double.IsNaN(x)||double.IsInfinity(x))continue;sum+=x;count++;}
            return count==0 ? 0 : double.IsPositiveInfinity(sum) ? double.MaxValue : Math.Max(0,sum/count);
        }
    }
}

public static class GreatPowerRanking
{
    /// <summary>60% warriors, 25% population, 15% five-year average income; no treasury.</summary>
    public static List<int> Rank(IList<NationalPowerSnapshot> eligible)
    {
        if(eligible==null)throw new ArgumentNullException(nameof(eligible));
        int armyMax=0,popMax=0;double incomeMax=0;var ids=new HashSet<int>();
        foreach(var p in eligible)
        {
            if(p==null || p.CountryId<=0 || !ids.Add(p.CountryId))throw new ArgumentException("Invalid/duplicate country.");
            armyMax=Math.Max(armyMax,Math.Max(0,p.Warriors));popMax=Math.Max(popMax,Math.Max(0,p.Population));incomeMax=Math.Max(incomeMax,p.AverageIncome);
        }
        var scored=new List<KeyValuePair<int,double>>();
        foreach(var p in eligible)
        {
            double score=(armyMax>0 ? 0.60*Math.Max(0,p.Warriors)/armyMax : 0)
                +(popMax>0 ? 0.25*Math.Max(0,p.Population)/popMax : 0)
                +(incomeMax>0 ? 0.15*p.AverageIncome/incomeMax : 0);
            scored.Add(new KeyValuePair<int,double>(p.CountryId,score));
        }
        scored.Sort((a,b)=>{int c=b.Value.CompareTo(a.Value);return c!=0 ? c : a.Key.CompareTo(b.Key);});
        var result=new List<int>();foreach(var p in scored)result.Add(p.Key);return result;
    }
}

public enum RecognitionOrigin { None, FirstFiftyYears, InsufficientGreatPowers, Council }
public enum InternationalContact { Trade, Alliance, PoliticalUnion, War, Peace }
public sealed class CountryRecognitionState
{
    public int CountryId;
    public int BirthYear;
    public bool Alive=true;
    public bool Sovereign=true;
    public bool Recognized;
    public int RecognizedYear=-1;
    public RecognitionOrigin Origin;
    public int LastDecisionGeneration=-1;
    public CountryRecognitionState Copy()=>(CountryRecognitionState)MemberwiseClone();
}
public sealed class RecognitionSnapshot
{
    public bool EnabledForNewWorld;
    public int LastReviewYear=-1;
    public int CompositionGeneration;
    public List<int> GreatPowers=new List<int>();
    public List<CountryRecognitionState> Countries=new List<CountryRecognitionState>();
    public Dictionary<int,CivicBallotSnapshot> Pending=new Dictionary<int,CivicBallotSnapshot>();
}

/// <summary>0.26 domain registry. The annual dispatcher runs only for explicitly enabled new-world state.</summary>
public sealed class InternationalRecognition
{
    private readonly Dictionary<int,CountryRecognitionState> _countries=new Dictionary<int,CountryRecognitionState>();
    private readonly List<int> _greatPowers=new List<int>();
    private readonly Dictionary<int,CivicBallot> _pending=new Dictionary<int,CivicBallot>();
    public bool EnabledForNewWorld {get;}
    public int LastReviewYear {get;private set;}=-1;
    public int CompositionGeneration {get;private set;}
    public IReadOnlyList<int> GreatPowers=>_greatPowers.AsReadOnly();
    public event Action<IReadOnlyList<int>> CompositionChanged;
    public event Action<int> CountryRecognized;
    public InternationalRecognition(bool enabledForNewWorld){EnabledForNewWorld=enabledForNewWorld;}
    public bool ContainsCountry(int id)=>_countries.ContainsKey(id);
    public bool IsAlive(int id)=>_countries.TryGetValue(id,out var state) && state.Alive;
    public void RestoreAlive(int id)
    {
        if(_countries.TryGetValue(id,out var country))country.Alive=true;
    }
    public void RegisterCountry(int id,int birthYear,bool sovereign=true)
    {
        if(id<=0 || birthYear<0 || _countries.ContainsKey(id))throw new ArgumentException("Invalid or repeated country birth.");
        var state=new CountryRecognitionState{CountryId=id,BirthYear=birthYear,Sovereign=sovereign};_countries.Add(id,state);
        if(!EnabledForNewWorld)return;
        if(birthYear<50)Recognize(state,birthYear,RecognitionOrigin.FirstFiftyYears);
        else
        {
            int count=0;
            foreach(var p in _countries.Values)if(p.Alive && p.Sovereign && p.Recognized)count++;
            if(count<3)Recognize(state,birthYear,RecognitionOrigin.InsufficientGreatPowers);
        }
    }
    public void RegisterFoundingCountry(int id,int birthYear,bool sovereign=true)
    {
        if(id<=0 || birthYear<0 || _countries.ContainsKey(id))throw new ArgumentException("Invalid or repeated country birth.");
        var state=new CountryRecognitionState{CountryId=id,BirthYear=birthYear,Sovereign=sovereign};
        _countries.Add(id,state);
        if(EnabledForNewWorld)Recognize(state,birthYear,RecognitionOrigin.FirstFiftyYears);
    }
    private void Recognize(CountryRecognitionState country,int year,RecognitionOrigin origin)
    {
        if(country.Recognized)return;
        country.Recognized=true;country.RecognizedYear=year;country.Origin=origin;
        CountryRecognized?.Invoke(country.CountryId);
    }
    public void DestroyCountry(int id)
    {
        if(_countries.TryGetValue(id,out var country))country.Alive=false;
        if(_greatPowers.Contains(id))_pending.Clear();else _pending.Remove(id);
    }
    public void SetSovereignty(int id,bool sovereign)
    {
        if(!_countries.TryGetValue(id,out var country))throw new ArgumentException("Unknown country.");
        country.Sovereign=sovereign;
        if(!sovereign && _greatPowers.Contains(id))_pending.Clear();
    }
    /// <summary>
    /// 0.50: снять с державы звание великой державы — за националистическую расправу
    /// над своими жителями. Место в совете освобождается, голосования по составу
    /// совета сбрасываются: новый совет будет избран заново.
    /// </summary>
    public bool StripGreatPower(int id)
    {
        if(id<=0)return false;
        if(!_greatPowers.Remove(id))return false;
        _pending.Clear();
        CompositionGeneration=checked(CompositionGeneration+1);
        CompositionChanged?.Invoke(new List<int>(_greatPowers).AsReadOnly());
        return true;
    }

    public bool IsRecognized(int id)=>!EnabledForNewWorld || _countries.TryGetValue(id,out var state) && state.Alive && state.Recognized;
    public bool CanContact(int a,int b,InternationalContact kind)
    {
        if(!Enum.IsDefined(typeof(InternationalContact),kind))return false;
        if(!EnabledForNewWorld)return true;
        if(a==b || !_countries.TryGetValue(a,out var left) || !left.Alive || !_countries.TryGetValue(b,out var right) || !right.Alive)return false;
        return kind==InternationalContact.War || kind==InternationalContact.Peace || left.Recognized && right.Recognized;
    }
    /// <summary>Supply a complete, authoritative live power snapshot, not a partial failed read.</summary>
    public bool ReviewGreatPowers(int year,IList<NationalPowerSnapshot> live)
    {
        if(year<0)throw new ArgumentOutOfRangeException(nameof(year));
        if(!EnabledForNewWorld || year<50)return false;
        if(live==null)throw new ArgumentNullException(nameof(live));
        var eligible=new List<NationalPowerSnapshot>();var available=new HashSet<int>();
        foreach(var p in live)
            if(p!=null && _countries.TryGetValue(p.CountryId,out var state) && state.Alive && state.Sovereign && state.Recognized)
            {eligible.Add(p);available.Add(p.CountryId);}
        bool missing=false;foreach(int id in _greatPowers)if(!available.Contains(id))missing=true;
        bool first=_greatPowers.Count==0 && eligible.Count>=3;
        bool due=LastReviewYear<0 || (long)year-LastReviewYear>=5;
        if(!missing && !first && !due)return false;
        var ranked=GreatPowerRanking.Rank(eligible);var next=new List<int>();
        if(ranked.Count>=3)next.AddRange(ranked.GetRange(0,3));
        var previous=new HashSet<int>(_greatPowers);bool changed=previous.Count!=next.Count;
        foreach(int id in next)if(!previous.Contains(id))changed=true;
        LastReviewYear=year;
        if(!changed)return false;
        _greatPowers.Clear();_greatPowers.AddRange(next);
        CompositionGeneration=checked(CompositionGeneration+1);
        // Old council decisions may not be carried over as votes of a new council.
        _pending.Clear();
        CompositionChanged?.Invoke(new List<int>(_greatPowers).AsReadOnly());return true;
    }
    private bool ActiveCouncil()
    {
        if(_greatPowers.Count!=3)return false;
        foreach(int id in _greatPowers)if(!_countries.TryGetValue(id,out var state)||!state.Alive||!state.Sovereign||!state.Recognized)return false;
        return true;
    }
    public bool NeedsRecognitionVote(int id)
        =>EnabledForNewWorld && ActiveCouncil() && _countries.TryGetValue(id,out var country)
            && country.Alive && !country.Recognized && country.LastDecisionGeneration!=CompositionGeneration && !_pending.ContainsKey(id);
    public CivicBallot OpenRecognitionVote(int id,int year,int durationYears)
    {
        if(!NeedsRecognitionVote(id))return null;
        var ballot=new CivicBallot("recognition:"+id+":"+CompositionGeneration,BallotElectorKind.Country,year,durationYears,_greatPowers,new[]{CivicBallot.Yes,CivicBallot.No});
        _pending.Add(id,ballot);return ballot;
    }
    public CivicBallot PendingVote(int id)=>_pending.TryGetValue(id,out var ballot)?ballot:null;
    public bool CompleteRecognitionVote(int id,int year,out bool recognized)
    {
        recognized=false;
        if(!ActiveCouncil())return false;
        if(!_pending.TryGetValue(id,out var ballot) || !_countries.TryGetValue(id,out var country) || !country.Alive)return false;
        if(!ballot.Closed && !ballot.TryCloseFullyVoted(year) && !ballot.TryClose(year,out _))return false;
        var tally=ballot.Tally();
        country.LastDecisionGeneration=CompositionGeneration;_pending.Remove(id);
        recognized=tally.Counts[CivicBallot.Yes]>=2;
        if(recognized)Recognize(country,year,RecognitionOrigin.Council);
        return true;
    }
    public RecognitionSnapshot Capture()
    {
        var snapshot=new RecognitionSnapshot{EnabledForNewWorld=EnabledForNewWorld,LastReviewYear=LastReviewYear,
            CompositionGeneration=CompositionGeneration,GreatPowers=new List<int>(_greatPowers)};
        var ids=new List<int>(_countries.Keys);ids.Sort();foreach(int id in ids)snapshot.Countries.Add(_countries[id].Copy());
        foreach(var pair in _pending)snapshot.Pending.Add(pair.Key,pair.Value.Capture());
        return snapshot;
    }
    public static InternationalRecognition Restore(RecognitionSnapshot snapshot)
    {
        if(snapshot==null || snapshot.Countries==null || snapshot.GreatPowers==null || snapshot.Pending==null
            || snapshot.CompositionGeneration<0 || snapshot.LastReviewYear< -1)throw new ArgumentException("Invalid recognition snapshot.");
        if(!snapshot.EnabledForNewWorld && (snapshot.GreatPowers.Count>0 || snapshot.Pending.Count>0))throw new ArgumentException("Disabled world has an active council.");
        var registry=new InternationalRecognition(snapshot.EnabledForNewWorld){LastReviewYear=snapshot.LastReviewYear,CompositionGeneration=snapshot.CompositionGeneration};
        foreach(var country in snapshot.Countries)
        {
            if(country==null || country.CountryId<=0 || country.BirthYear<0 || registry._countries.ContainsKey(country.CountryId)
                || country.LastDecisionGeneration>snapshot.CompositionGeneration || country.LastDecisionGeneration< -1
                || !Enum.IsDefined(typeof(RecognitionOrigin),country.Origin)
                || country.Recognized && (country.RecognizedYear<country.BirthYear || country.Origin==RecognitionOrigin.None)
                || !country.Recognized && country.Origin!=RecognitionOrigin.None)throw new ArgumentException("Invalid country record.");
            registry._countries.Add(country.CountryId,country.Copy());
        }
        if(snapshot.GreatPowers.Count!=0 && snapshot.GreatPowers.Count!=3)throw new ArgumentException("Council must be empty or have three members.");
        var ids=new HashSet<int>();foreach(int id in snapshot.GreatPowers)
        {
            if(!ids.Add(id) || !registry._countries.TryGetValue(id,out var state) || !state.Recognized)throw new ArgumentException("Invalid great power.");
            registry._greatPowers.Add(id);
        }
        foreach(var pair in snapshot.Pending)
        {
            if(!registry.NeedsRecognitionVote(pair.Key))throw new ArgumentException("Invalid pending recognition.");
            var ballot=CivicBallot.Restore(pair.Value);var saved=ballot.Capture();
            if(ballot.ElectorKind!=BallotElectorKind.Country || saved.Electors.Count!=3 || !ids.SetEquals(saved.Electors)
                || saved.Options.Count!=2 || !saved.Options.Contains(CivicBallot.Yes) || !saved.Options.Contains(CivicBallot.No)
                || ballot.Id!="recognition:"+pair.Key+":"+snapshot.CompositionGeneration)throw new ArgumentException("Council ballot does not match the council.");
            registry._pending.Add(pair.Key,ballot);
        }
        return registry;
    }
}

public static class AllianceBallots
{
    /// <summary>The applicant/expellee does not vote; more than half of all remaining voters is needed.</summary>
    public static CivicBallot Open(string id,IEnumerable<int> members,int subjectId,int year,int durationYears)
    {
        if(members==null || subjectId<=0)throw new ArgumentException("Invalid alliance proposal.");
        var voters=new List<int>();foreach(int member in members)if(member!=subjectId)voters.Add(member);
        return new CivicBallot(id,BallotElectorKind.Country,year,durationYears,voters,new[]{CivicBallot.Yes,CivicBallot.No});
    }
    public static bool MayAdmit(CivicBallot ballot,bool applicantConsent)
        => applicantConsent && ballot!=null && ballot.ElectorKind==BallotElectorKind.Country && ballot.Closed && ballot.Tally().BinaryAccepted;
}
