using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace RealPolitic.Core;

// 0.26 domain foundation. Game integration is deliberately not enabled in 0.25.1.
public enum BallotElectorKind { Person, Country }
public enum BallotThreshold { MoreThanHalf, AtLeastTwoThirds }

public sealed class CivicBallotSnapshot
{
    public string Id = "";
    public BallotElectorKind ElectorKind;
    public int OpenedYear;
    public int DurationYears;
    public int QuorumPercent;
    public BallotThreshold Threshold;
    public bool Closed;
    public bool AllowSingleCandidate;
    public List<int> Electors = new List<int>();
    public List<int> Options = new List<int>();
    public Dictionary<int,int> Votes = new Dictionary<int,int>();
}

public sealed class CivicTally
{
    public int Eligible { get; internal set; }
    public int Participated { get; internal set; }
    public int Abstained { get; internal set; }
    public int ValidVotes => Participated - Abstained;
    public bool QuorumReached { get; internal set; }
    public bool BinaryAccepted { get; internal set; }
    // Zero means no unique winner, never a fictional actor or arbitrary tie-break.
    public int UniquePluralityWinner { get; internal set; }
    public IReadOnlyDictionary<int,int> Counts { get; internal set; }
}

/// <summary>A frozen electoral roll and one stored, unchangeable vote per real elector ID.</summary>
public sealed class CivicBallot
{
    public const int Abstain = 0;
    public const int Yes = 1;
    public const int No = 2;
    private readonly HashSet<int> _electors;
    private readonly HashSet<int> _options;
    private readonly Dictionary<int,int> _votes = new Dictionary<int,int>();
    public string Id { get; }
    public BallotElectorKind ElectorKind { get; }
    public int OpenedYear { get; }
    public int DurationYears { get; }
    public int DeadlineYear => checked(OpenedYear + DurationYears);
    public int QuorumPercent { get; }
    public BallotThreshold Threshold { get; }
    public bool Closed { get; private set; }
    public bool AllowSingleCandidate { get; }
    public IReadOnlyDictionary<int,int> Votes => new ReadOnlyDictionary<int,int>(_votes);
    public int EligibleCount => _electors.Count;

    public CivicBallot(string id, BallotElectorKind kind, int openedYear, int durationYears,
        IEnumerable<int> electors, IEnumerable<int> options, int quorumPercent=0,
        BallotThreshold threshold=BallotThreshold.MoreThanHalf)
        :this(id,kind,openedYear,durationYears,electors,options,quorumPercent,threshold,false){}
    public static CivicBallot ForCandidates(string id,int year,IEnumerable<int> electors,IEnumerable<int> candidates)
        =>new CivicBallot(id,BallotElectorKind.Person,year,0,electors,candidates,0,BallotThreshold.MoreThanHalf,true);
    private CivicBallot(string id,BallotElectorKind kind,int openedYear,int durationYears,IEnumerable<int> electors,
        IEnumerable<int> options,int quorumPercent,BallotThreshold threshold,bool allowSingleCandidate)
    {
        if(string.IsNullOrWhiteSpace(id)) throw new ArgumentException("A ballot needs an identity.",nameof(id));
        if(openedYear<0 || durationYears<0 || (long)openedYear+durationYears>int.MaxValue) throw new ArgumentOutOfRangeException(nameof(durationYears));
        if(quorumPercent<0 || quorumPercent>100) throw new ArgumentOutOfRangeException(nameof(quorumPercent));
        if(!Enum.IsDefined(typeof(BallotElectorKind),kind) || !Enum.IsDefined(typeof(BallotThreshold),threshold))throw new ArgumentOutOfRangeException(nameof(kind));
        _electors = PositiveUnique(electors,nameof(electors));
        _options = PositiveUnique(options,nameof(options));
        if(_options.Count<(allowSingleCandidate?1:2) || allowSingleCandidate && kind!=BallotElectorKind.Person)throw new ArgumentException("At least two alternatives are needed.",nameof(options));
        AllowSingleCandidate=allowSingleCandidate;Id=id;ElectorKind=kind;OpenedYear=openedYear;DurationYears=durationYears;QuorumPercent=quorumPercent;Threshold=threshold;
    }
    private static HashSet<int> PositiveUnique(IEnumerable<int> source,string name)
    {
        if(source==null)throw new ArgumentNullException(name);
        var result=new HashSet<int>();
        foreach(int id in source)if(id<=0 || !result.Add(id))throw new ArgumentException("IDs must be positive and unique.",name);
        return result;
    }
    public bool TryCast(int electorId,int optionId,int year)
    {
        bool within = DurationYears==0 ? year==OpenedYear : year>=OpenedYear && year<DeadlineYear;
        if(Closed || !within || !_electors.Contains(electorId) || _votes.ContainsKey(electorId))return false;
        if(optionId!=Abstain && !_options.Contains(optionId))return false;
        _votes.Add(electorId,optionId);return true;
    }
    /// <summary>Calls an individual decision function once per enrolled, not-yet-voted elector.</summary>
    public int Collect(int year,Func<int,int> decide)
    {
        if(decide==null)throw new ArgumentNullException(nameof(decide));
        if(Closed || year<OpenedYear || (DurationYears>0 && year>=DeadlineYear) || (DurationYears==0 && year!=OpenedYear))return 0;
        var ids=new List<int>(_electors);ids.Sort();int count=0;
        foreach(int id in ids)if(!_votes.ContainsKey(id) && TryCast(id,decide(id),year))count++;
        return count;
    }
    internal bool TryCloseFullyVoted(int year)
    {
        if(Closed || year<OpenedYear || _votes.Count!=_electors.Count || _electors.Count==0)return false;
        Closed=true;return true;
    }
    public bool TryClose(int year,out CivicTally tally)
    {
        tally=null;if(Closed || year<DeadlineYear)return false;
        Closed=true;tally=Tally();return true;
    }
    public CivicTally Tally()
    {
        var counts=new Dictionary<int,int>();foreach(int id in _options)counts.Add(id,0);
        int abstained=0;
        foreach(int choice in _votes.Values)if(choice==Abstain)abstained++;else counts[choice]++;
        bool quorum=_electors.Count>0 && (long)_votes.Count*100 >= (long)_electors.Count*QuorumPercent;
        int valid=_votes.Count-abstained;bool accepted=false;
        if(Closed && quorum && valid>0 && _options.Count==2 && _options.Contains(Yes) && _options.Contains(No))
            {
                int denominator=ElectorKind==BallotElectorKind.Country ? _electors.Count : valid;
                accepted=Threshold==BallotThreshold.MoreThanHalf ? (long)counts[Yes]*2>denominator : (long)counts[Yes]*3>=(long)denominator*2;
            }
        int largest=0,winner=0;bool tied=false;
        foreach(var pair in counts)
        {
            if(pair.Value>largest){largest=pair.Value;winner=pair.Key;tied=false;}
            else if(pair.Value==largest)tied=true;
        }
        return new CivicTally{Eligible=_electors.Count,Participated=_votes.Count,Abstained=abstained,QuorumReached=quorum,
            BinaryAccepted=accepted,UniquePluralityWinner=Closed && quorum && largest>0 && !tied ? winner : 0,
            Counts=new ReadOnlyDictionary<int,int>(counts)};
    }
    public CivicBallotSnapshot Capture()
    {
        var electors=new List<int>(_electors);electors.Sort();var options=new List<int>(_options);options.Sort();
        return new CivicBallotSnapshot{Id=Id,ElectorKind=ElectorKind,OpenedYear=OpenedYear,DurationYears=DurationYears,
            QuorumPercent=QuorumPercent,Threshold=Threshold,Closed=Closed,AllowSingleCandidate=AllowSingleCandidate,Electors=electors,Options=options,Votes=new Dictionary<int,int>(_votes)};
    }
    public static CivicBallot Restore(CivicBallotSnapshot snapshot)
    {
        if(snapshot==null || snapshot.Votes==null)throw new ArgumentException("Missing ballot state.",nameof(snapshot));
        var ballot=new CivicBallot(snapshot.Id,snapshot.ElectorKind,snapshot.OpenedYear,snapshot.DurationYears,
            snapshot.Electors,snapshot.Options,snapshot.QuorumPercent,snapshot.Threshold,snapshot.AllowSingleCandidate);
        foreach(var pair in snapshot.Votes)if(!ballot.TryCast(pair.Key,pair.Value,snapshot.OpenedYear))throw new ArgumentException("Invalid saved vote.",nameof(snapshot));
        ballot.Closed=snapshot.Closed;return ballot;
    }
}
