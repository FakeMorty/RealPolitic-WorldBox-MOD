using System;

namespace RealPolitic.Core;

public enum OfficeTermLimitBasis { Consecutive, Lifetime }
public enum ForeignDecisionAuthority { Ruler, Electorate }
public enum RegionalExitLaw { Forbidden, WithCentralPermission, RegionalRight }

/// <summary>Approved 0.26 law menu attached only to explicitly enabled constitutions.</summary>
public sealed class ElectoralChapter
{
    public Franchise Electors = Franchise.Universal;
    public Franchise Candidates = Franchise.Universal;
    public int VotingAge = 18;
    public int CandidacyAge = 25;
    public int TermYears = 15;
    public int MaximumTerms = 3; // zero is unlimited
    public OfficeTermLimitBasis TermLimitBasis = OfficeTermLimitBasis.Lifetime;
    public int ReferendumQuorumPercent = 50;
    public BallotThreshold ReferendumThreshold = BallotThreshold.MoreThanHalf;
    public int BallotYears = 1;
    public ForeignDecisionAuthority ForeignDecisions = ForeignDecisionAuthority.Ruler;
    public RegionalExitLaw RegionalExit = RegionalExitLaw.Forbidden;

    public void Validate()
    {
        if(!Enum.IsDefined(typeof(Franchise),Electors)||!Enum.IsDefined(typeof(Franchise),Candidates)
            ||!Enum.IsDefined(typeof(OfficeTermLimitBasis),TermLimitBasis)
            ||!Enum.IsDefined(typeof(BallotThreshold),ReferendumThreshold)
            ||!Enum.IsDefined(typeof(ForeignDecisionAuthority),ForeignDecisions)
            ||!Enum.IsDefined(typeof(RegionalExitLaw),RegionalExit))throw new ArgumentException("Unknown electoral law.");
        if(!OneOf(VotingAge,16,18,21)||!OneOf(CandidacyAge,18,25,35)||!OneOf(TermYears,5,10,15,20)
            ||!OneOf(MaximumTerms,0,1,2,3)||!OneOf(ReferendumQuorumPercent,0,25,50)||!OneOf(BallotYears,1,3))
            throw new ArgumentException("Value is outside the approved electoral menu.");
    }
    private static bool OneOf(int value,params int[] options)=>Array.IndexOf(options,value)>=0;
    private static bool FranchiseAllows(Franchise law,CivicPerson person)
    {
        switch(law)
        {
            case Franchise.Universal:return true;
            case Franchise.Notables:return person.IsNotable || person.IsCityLeader;
            case Franchise.Clan:return person.IsRulingClan;
            default:return false;
        }
    }
    public bool MayVote(CivicPerson person)
        => person!=null && person.ActorId>0 && person.Alive && person.Citizen && person.Age>=VotingAge && FranchiseAllows(Electors,person);
    public bool MayStand(CivicPerson person,int lifetimeTerms,int consecutiveTerms)
    {
        if(person==null || person.ActorId<=0 || !person.Alive || !person.Citizen || person.Age<CandidacyAge || !FranchiseAllows(Candidates,person))return false;
        int used=TermLimitBasis==OfficeTermLimitBasis.Lifetime ? lifetimeTerms : consecutiveTerms;
        return lifetimeTerms>=0 && consecutiveTerms>=0 && (MaximumTerms==0 || used<MaximumTerms);
    }
    public bool LegalExitPossible(bool centralPermission)=>RegionalExit==RegionalExitLaw.RegionalRight
        || RegionalExit==RegionalExitLaw.WithCentralPermission && centralPermission;
}

public sealed class CivicPerson
{
    public int ActorId;
    public int Age;
    public bool Alive=true;
    public bool Citizen=true;
    public bool IsNotable;
    public bool IsCityLeader;
    public bool IsRulingClan;
}

/// <summary>Pure yearly observation; never turns a failed referendum into a secession.</summary>
public sealed class RegionalInitiative
{
    public int CityId;
    public int LastObservedYear=-1;
    public int LowLoyaltyYears;
    public int NextPermittedYear;
    public float LoyaltyThreshold=0.45f;
    public int RequiredYears=3;
    public int RetryYears=10;
    public bool Observe(int year,float loyalty,int metropolisCityCount,bool isCapital)
    {
        if(year<0 || year<=LastObservedYear)return false;
        if(float.IsNaN(loyalty)||float.IsInfinity(loyalty)||loyalty<0 || loyalty>1)throw new ArgumentOutOfRangeException(nameof(loyalty));
        bool low=loyalty<LoyaltyThreshold && metropolisCityCount>=2 && !isCapital;
        LowLoyaltyYears=low ? (LastObservedYear==year-1 ? LowLoyaltyYears+1 : 1) : 0;
        LastObservedYear=year;
        return low && year>=NextPermittedYear && LowLoyaltyYears>=RequiredYears;
    }
    public void RecordFailedReferendum(int year)
    {
        if(year<0 || RetryYears<0 || (long)year+RetryYears>int.MaxValue)throw new ArgumentOutOfRangeException(nameof(year));
        NextPermittedYear=Math.Max(NextPermittedYear,year+RetryYears);
        LowLoyaltyYears=0;
    }
}


public static class ElectoralDefaults
{
    // Defaults depend on the constitutional structure; subsequent amendments preserve independent clauses.
    public static ElectoralChapter FromConstitution(Constitution constitution)
    {
        if(constitution==null)throw new ArgumentNullException(nameof(constitution));
        var law=new ElectoralChapter{Electors=constitution.Franchise,Candidates=constitution.Franchise,
            ForeignDecisions=constitution.WarPower==WarAuthority.ByRuler ? ForeignDecisionAuthority.Ruler : ForeignDecisionAuthority.Electorate,
            RegionalExit=constitution.StateForm==HeadOfState.Hereditary ? RegionalExitLaw.Forbidden
                : constitution.StateForm==HeadOfState.ParliamentaryMonarchy ? RegionalExitLaw.WithCentralPermission : RegionalExitLaw.RegionalRight};
        switch(constitution.StateForm)
        {
            case HeadOfState.Hereditary:law.VotingAge=21;law.CandidacyAge=35;law.TermYears=20;law.MaximumTerms=0;law.BallotYears=3;break;
            case HeadOfState.Parliamentary:law.TermYears=10;law.MaximumTerms=2;law.TermLimitBasis=OfficeTermLimitBasis.Consecutive;law.ReferendumQuorumPercent=25;break;
            case HeadOfState.Assembly:law.VotingAge=16;law.CandidacyAge=18;law.TermYears=5;law.MaximumTerms=1;law.TermLimitBasis=OfficeTermLimitBasis.Consecutive;law.ReferendumQuorumPercent=0;break;
            case HeadOfState.ParliamentaryMonarchy:law.BallotYears=3;break;
        }
        law.Validate();return law;
    }
}
