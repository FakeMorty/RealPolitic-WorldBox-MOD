using System;
using System.Collections.Generic;

namespace RealPolitic.Core;

/// <summary>Adjacent, reversible amendments within the user's approved law menu; no new demographic restrictions.</summary>
internal static class ElectoralAmendmentMenu
{
    public static List<ConstitutionAmendments.Proposal> Build(ElectoralChapter law,bool tighten)
    {
        law.Validate();var result=new List<ConstitutionAmendments.Proposal>();
        void Step<T>(string article,T current,T[] ordered,Func<T,string> text,Action<Constitution,T> apply)
        {
            int at=Array.IndexOf(ordered,current),next=at+(tighten?1:-1);if(next<0 || next>=ordered.Length)return;
            T value=ordered[next];result.Add(new ConstitutionAmendments.Proposal{Article=article,From=text(current),To=text(value),Tightens=tighten,
                Apply=c=>{apply(c,value);c.Electoral.Validate();}});
        }
        string Number(int n)=>n.ToString(System.Globalization.CultureInfo.InvariantCulture);
        string FranchiseText(Franchise f)=>f==Franchise.Universal?"RP_CONST_FR_UNIVERSAL":f==Franchise.Notables?"RP_CONST_FR_NOTABLES":"RP_CONST_FR_CLAN";
        var franchises=new[]{Franchise.Universal,Franchise.Notables,Franchise.Clan};
        Step(ConstitutionAmendments.ArticleFranchise,law.Electors,franchises,FranchiseText,(c,v)=>{c.Electoral.Electors=v;c.Franchise=v;});
        Step("RP26_ART_CANDIDATES",law.Candidates,franchises,FranchiseText,(c,v)=>c.Electoral.Candidates=v);
        Step("RP26_ART_VOTING_AGE",law.VotingAge,new[]{16,18,21},Number,(c,v)=>c.Electoral.VotingAge=v);
        Step("RP26_ART_CANDIDACY_AGE",law.CandidacyAge,new[]{18,25,35},Number,(c,v)=>c.Electoral.CandidacyAge=v);
        Step("RP26_ART_TERM",law.TermYears,new[]{5,10,15,20},Number,(c,v)=>c.Electoral.TermYears=v);
        Step("RP26_ART_LIMIT",law.MaximumTerms,new[]{1,2,3,0},v=>v==0?"RP26_LIMIT_NONE":Number(v),(c,v)=>c.Electoral.MaximumTerms=v);
        if(law.MaximumTerms!=0)Step("RP26_ART_LIMIT_BASIS",law.TermLimitBasis,new[]{OfficeTermLimitBasis.Lifetime,OfficeTermLimitBasis.Consecutive},
            v=>v==OfficeTermLimitBasis.Lifetime?"RP26_LIFETIME":"RP26_CONSECUTIVE",(c,v)=>c.Electoral.TermLimitBasis=v);
        Step("RP26_ART_QUORUM",law.ReferendumQuorumPercent,new[]{0,25,50},v=>Number(v)+"%",(c,v)=>c.Electoral.ReferendumQuorumPercent=v);
        Step("RP26_ART_THRESHOLD",law.ReferendumThreshold,new[]{BallotThreshold.MoreThanHalf,BallotThreshold.AtLeastTwoThirds},
            v=>v==BallotThreshold.MoreThanHalf?"RP26_SIMPLE":"RP26_TWO_THIRDS",(c,v)=>c.Electoral.ReferendumThreshold=v);
        Step("RP26_ART_DURATION",law.BallotYears,new[]{1,3},Number,(c,v)=>c.Electoral.BallotYears=v);
        Step("RP26_ART_FOREIGN",law.ForeignDecisions,new[]{ForeignDecisionAuthority.Electorate,ForeignDecisionAuthority.Ruler},
            v=>v==ForeignDecisionAuthority.Ruler?"RP26_BY_RULER":"RP26_BY_ELECTORATE",(c,v)=>c.Electoral.ForeignDecisions=v);
        Step("RP26_ART_EXIT",law.RegionalExit,new[]{RegionalExitLaw.RegionalRight,RegionalExitLaw.WithCentralPermission,RegionalExitLaw.Forbidden},
            v=>v==RegionalExitLaw.Forbidden?"RP26_EXIT_FORBIDDEN":v==RegionalExitLaw.WithCentralPermission?"RP26_EXIT_PERMISSION":"RP26_EXIT_RIGHT",(c,v)=>c.Electoral.RegionalExit=v);
        return result;
    }
}
