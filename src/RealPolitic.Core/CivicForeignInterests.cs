using System;

namespace RealPolitic.Core;

public sealed class CivicCountryBirth
{
    public int CountryId;
    public int ParentId;
    public int Year;
    public bool Lawful;
}

/// <summary>Observable interests, not a target result or probability. Every elector still decides individually.</summary>
public static class CivicForeignInterests
{
    public static CivicIssueContext Question(PoliticalWorld world,KingdomPolitics voter,KingdomPolitics target,bool recognition)
    {
        if(world==null || voter==null || target==null)throw new ArgumentNullException();
        var own=new NationalPowerSnapshot{CountryId=voter.KingdomId};var other=new NationalPowerSnapshot{CountryId=target.KingdomId};
        world.Governance.FillIncome(own);world.Governance.FillIncome(other);
        double total=own.AverageIncome+other.AverageIncome;
        double trade=total>0 && !double.IsInfinity(total)?other.AverageIncome/total:0;
        float relations=Math.Max(-1,Math.Min(1,world.GetRulerRelation(voter.KingdomId,target.KingdomId)/100f));
        float grievances=Math.Max(0,Math.Min(1,world.GetGrievance(voter.KingdomId,target.KingdomId)/100f));
        return new CivicIssueContext{CurrentGovernment=voter.Ideology,ProposedGovernment=target.Ideology,
            StrategicInterest=Math.Max(-1,Math.Min(1,relations*0.30f-grievances*0.40f+(float)trade*0.15f)),
            RecognitionOfOurBreakaway=recognition && world.Governance.CountryBirths.TryGetValue(target.KingdomId,out var birth) && birth.ParentId==voter.KingdomId};
    }
}
