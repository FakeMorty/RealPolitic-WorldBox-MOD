using System;

namespace RealPolitic.Core;

/// <summary>A person's own political position; persisted in new-world mode, not re-rolled on load.</summary>
public class CivicBelief
{
    public IdeologyType Ideology;
    public float Conviction;
    public float Dissent;
    public int KingdomId=-1;
    public IdeologyType LastStateIdeology;
    public int LastDissentYear;
    public void Validate()
    {
        if(!Enum.IsDefined(typeof(IdeologyType),Ideology)||!Enum.IsDefined(typeof(IdeologyType),LastStateIdeology)
            ||float.IsNaN(Conviction)||float.IsInfinity(Conviction)||Conviction<0||Conviction>1
            ||float.IsNaN(Dissent)||float.IsInfinity(Dissent)||Dissent<0||Dissent>1||KingdomId< -1||LastDissentYear<0)
            throw new ArgumentException("Invalid personal belief.");
    }
    public CivicBelief Copy()=>(CivicBelief)MemberwiseClone();
}

public sealed class CivicIssueContext
{
    public IdeologyType CurrentGovernment;
    public IdeologyType ProposedGovernment;
    // National/geopolitical evaluation supplied by the political layer, not invented by the tally.
    public float StrategicInterest; // [-1,1]
    public bool Independence;
    public bool ReverseChoice;
    public bool RecognitionOfOurBreakaway;
    public bool IsRulingFamily;
    public bool IsCityLeader;
    // Alliance questions need observable bloc/war context; a bare ideology affinity
    // made nearly every ruler vote to enlarge one alliance forever.
    public bool AllianceQuestion;
    public bool AllianceConsent;
    public float AllianceShareAfter; // [0,1]: share of viable countries after admission
    public bool VoterAtWar;
    public bool SubjectAtWar;
    public bool AllianceAtWar;
    public bool DirectWar;
    public bool CommonEnemy;
}

public static class CivicPreferences
{
    /// <summary>No target percentage or RNG: each decision follows this elector's saved attributes.</summary>
    public static int Decide(CivicBelief person,CivicIssueContext issue)
    {
        if(person==null||issue==null)throw new ArgumentNullException();person.Validate();
        if(!Enum.IsDefined(typeof(IdeologyType),issue.CurrentGovernment)||!Enum.IsDefined(typeof(IdeologyType),issue.ProposedGovernment)
            ||float.IsNaN(issue.StrategicInterest)||float.IsInfinity(issue.StrategicInterest)||issue.StrategicInterest< -1||issue.StrategicInterest>1
            ||float.IsNaN(issue.AllianceShareAfter)||float.IsInfinity(issue.AllianceShareAfter)||issue.AllianceShareAfter<0||issue.AllianceShareAfter>1)
            throw new ArgumentException("Invalid political question.");
        float affinity=Ideologies.Affinity(person.Ideology,issue.ProposedGovernment);
        float score=affinity*(0.35f+0.35f*person.Conviction)+0.35f*issue.StrategicInterest;
        if(issue.AllianceQuestion)
        {
            // The larger a bloc becomes, the less another admission looks like protection
            // and the more it looks like surrendering room to manoeuvre.
            if(issue.AllianceShareAfter>0.34f)score-=(issue.AllianceShareAfter-0.34f)*0.90f;
            if(issue.DirectWar)score-=0.80f;
            if(issue.CommonEnemy)score+=0.22f;
            if(issue.AllianceConsent)
            {
                if(issue.AllianceAtWar && !issue.VoterAtWar)score-=0.18f;
                else if(issue.VoterAtWar)score+=0.10f;
            }
            else
            {
                // Existing members do not want to import somebody else's unrelated war.
                if(issue.SubjectAtWar && !issue.CommonEnemy)score-=0.18f;
                if(!issue.VoterAtWar && !issue.SubjectAtWar && !issue.CommonEnemy)score-=0.05f;
            }
        }
        if(issue.RecognitionOfOurBreakaway)
        {
            bool selfDetermination=person.Ideology==IdeologyType.Democracy || person.Ideology==IdeologyType.Liberalism || person.Ideology==IdeologyType.Egalitarianism || person.Ideology==IdeologyType.FreeTerritory;
            score+=(selfDetermination?0.10f:-0.45f)+person.Dissent*0.25f;
            if(issue.IsRulingFamily)score-=0.40f;
            if(issue.IsCityLeader)score-=0.10f;
        }
        if(issue.Independence)
        {
            float obedience=ActorIdeologyRoll.Obedience(person.Ideology,issue.CurrentGovernment,person.Conviction,person.Dissent);
            score=(0.5f-obedience)*1.4f+person.Dissent*0.35f
                +(affinity-Ideologies.Affinity(person.Ideology,issue.CurrentGovernment))*0.25f;
            if(issue.IsRulingFamily)score-=0.30f;
            if(issue.IsCityLeader)score+=0.10f;
        }
        // Ambivalent electors abstain. A missing actual ballot remains non-participation, not abstention.
        int choice=score>0.08f ? CivicBallot.Yes : score< -0.08f ? CivicBallot.No : CivicBallot.Abstain;
        return issue.ReverseChoice && choice!=CivicBallot.Abstain ? choice==CivicBallot.Yes?CivicBallot.No:CivicBallot.Yes : choice;
    }
}
