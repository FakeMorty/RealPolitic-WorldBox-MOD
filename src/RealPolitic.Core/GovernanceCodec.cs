using System;
using System.Collections.Generic;

namespace RealPolitic.Core;

/// <summary>Versioned, deterministic save codec. Missing legacy blocks disable the new system.</summary>
public static class GovernanceCodec
{
    public const int SchemaVersion=3;
    public const int MinSupportedSchemaVersion=1;
    public static void Write(TinyJson.Writer w,GovernanceState state)
    {
        w.BeginObject();w.Key("schema").Value(SchemaVersion);w.Key("world").Value(state.WorldKey);w.Key("dispatchYear").Value(state.LastDispatchYear);
        w.Key("recognition");WriteRecognition(w,state.Recognition.Capture());
        w.Key("procedures").BeginArray();var keys=new List<string>(state.Procedures.Keys);keys.Sort(StringComparer.Ordinal);
        foreach(string key in keys)
        {
            var p=state.Procedures[key];w.BeginObject();w.Key("kind").Value((int)p.Kind);w.Key("country").Value(p.CountryId);
            w.Key("city").Value(p.CityId);w.Key("target").Value(p.TargetCountryId);w.Key("stage").Value((int)p.Stage);
            if(p.OpeningExitLaw.HasValue)w.Key("openingExitLaw").Value((int)p.OpeningExitLaw.Value);
            if(!string.IsNullOrEmpty(p.AuthorizedByProcedureId))w.Key("authorizedBy").Value(p.AuthorizedByProcedureId);
            w.Key("note").Value(p.ExecutionNote);w.Key("ballot");WriteBallot(w,p.Ballot.Capture());w.EndObject();
        }
        w.EndArray();w.Key("initiatives").BeginArray();var cities=new List<int>(state.Initiatives.Keys);cities.Sort();
        foreach(int city in cities)
        {
            var p=state.Initiatives[city];w.BeginObject();w.Key("city").Value(city);w.Key("last").Value(p.LastObservedYear);
            w.Key("lowYears").Value(p.LowLoyaltyYears);w.Key("next").Value(p.NextPermittedYear);w.Key("loyalty").Value(p.LoyaltyThreshold);
            w.Key("required").Value(p.RequiredYears);w.Key("retry").Value(p.RetryYears);w.EndObject();
        }
        w.EndArray();w.Key("income").BeginArray();var income=state.CaptureIncome();var countries=new List<int>(income.Keys);countries.Sort();
        foreach(int country in countries)
        {
            w.BeginObject();w.Key("country").Value(country);w.Key("years").BeginArray();
            foreach(var year in income[country]){w.BeginObject();w.Key("year").Value(year.Key);w.Key("amount").Value(year.Value);w.EndObject();}
            w.EndArray();w.EndObject();
        }
        w.EndArray();w.Key("beliefs").BeginArray();var people=new List<int>(state.Beliefs.Keys);people.Sort();
        foreach(int id in people)
        {
            var b=state.Beliefs[id];b.Validate();w.BeginObject();w.Key("id").Value(id);w.Key("ideology").Value((int)b.Ideology);
            w.Key("conviction").Value(b.Conviction);w.Key("dissent").Value(b.Dissent);w.Key("country").Value(b.KingdomId);
            w.Key("stateIdeology").Value((int)b.LastStateIdeology);w.Key("lastYear").Value(b.LastDissentYear);w.EndObject();
        }
        w.EndArray();w.Key("offices").BeginArray();var officeIds=new List<int>(state.Offices.Keys);officeIds.Sort();
        foreach(int id in officeIds)
        {
            var office=state.Offices[id];office.Validate();w.BeginObject();w.Key("country").Value(id);w.Key("holder").Value(office.HolderId);
            w.Key("since").Value(office.SinceYear);w.Key("next").Value(office.NextElectionYear);w.Key("attempt").Value(office.LastAttemptYear);
            w.Key("committed").Value(office.LastCommittedProcedure);w.Key("mandates").BeginArray();var actors=new List<int>(office.Mandates.Keys);actors.Sort();
            foreach(int actor in actors){var term=office.Mandates[actor];w.BeginObject();w.Key("actor").Value(actor);w.Key("lifetime").Value(term.Lifetime);w.Key("consecutive").Value(term.Consecutive);w.EndObject();}
            w.EndArray();w.EndObject();
        }
        w.EndArray();w.Key("allianceMotions").BeginArray();var motionIds=new List<string>(state.AllianceMotions.Keys);motionIds.Sort(StringComparer.Ordinal);
        foreach(string id in motionIds)
        {
            var m=state.AllianceMotions[id];m.Validate();w.BeginObject();w.Key("id").Value(m.Id);w.Key("action").Value((int)m.Action);
            w.Key("alliance").Value(m.AllianceId);w.Key("proposer").Value(m.ProposerId);w.Key("subject").Value(m.SubjectId);
            w.Key("consentAnnounced").Value(m.ConsentAnnounced);w.Key("members");Ints(w,m.Members);w.Key("stage").Value((int)m.Stage);w.Key("note").Value(m.Note);w.Key("ballot");WriteBallot(w,m.Ballot.Capture());w.EndObject();
        }
        w.EndArray();w.Key("countryBirths").BeginArray();var births=new List<int>(state.CountryBirths.Keys);births.Sort();
        foreach(int id in births){var b=state.CountryBirths[id];w.BeginObject();w.Key("country").Value(b.CountryId);w.Key("parent").Value(b.ParentId);w.Key("year").Value(b.Year);w.Key("lawful").Value(b.Lawful);w.EndObject();}
        w.EndArray();w.EndObject();
    }
    public static GovernanceState Read(TinyJson.Node node)
    {
        if(node==null)return new GovernanceState();
        try
        {
            Object(node);int schema=Int(node,"schema");if(schema<1 || schema>SchemaVersion)throw new FormatException("Unsupported governance schema.");
            if(schema>=2){Need(node,"offices");Need(node,"allianceMotions");Need(node,"dispatchYear");Need(node,"countryBirths");}
            var state=new GovernanceState(InternationalRecognition.Restore(ReadRecognition(Need(node,"recognition"))),String(node,"world"));
            if(node.Get("dispatchYear")!=null)state.RestoreDispatchYear(Int(node,"dispatchYear"));
            foreach(var n in Array(node,"procedures"))
            {
                RegionalExitLaw? openingExitLaw=null;
                string authorizedBy="";
                if(schema>=3)
                {
                    if(n.Get("openingExitLaw")!=null)openingExitLaw=(RegionalExitLaw)Int(n,"openingExitLaw");
                    if(n.Get("authorizedBy")!=null)authorizedBy=String(n,"authorizedBy");
                }
                state.AddProcedure(new CivicProcedure(CivicBallot.Restore(ReadBallot(Need(n,"ballot"))),(CivicProcedureKind)Int(n,"kind"),
                    Int(n,"country"),Int(n,"city"),Int(n,"target"),(CivicProcedureStage)Int(n,"stage"),String(n,"note"),
                    openingExitLaw,authorizedBy));
            }
            foreach(var n in Array(node,"initiatives"))state.RestoreInitiative(new RegionalInitiative{CityId=Int(n,"city"),LastObservedYear=Int(n,"last"),
                LowLoyaltyYears=Int(n,"lowYears"),NextPermittedYear=Int(n,"next"),LoyaltyThreshold=(float)Number(n,"loyalty"),RequiredYears=Int(n,"required"),RetryYears=Int(n,"retry")});
            var countryIds=new HashSet<int>();
            foreach(var n in Array(node,"income"))
            {
                int id=Int(n,"country");if(!countryIds.Add(id))throw new FormatException("Repeated income country.");
                var years=new HashSet<int>();var records=Array(n,"years");if(records.Count>5)throw new FormatException("Too many historical years.");
                foreach(var year in records){int y=Int(year,"year");if(!years.Add(y))throw new FormatException("Repeated income year.");state.RecordIncome(id,y,Number(year,"amount"));}
            }
            foreach(var n in Array(node,"beliefs"))
            {
                int id=Int(n,"id");if(state.Beliefs.ContainsKey(id))throw new FormatException("Repeated personal belief.");
                state.RememberBelief(id,new CivicBelief{Ideology=(IdeologyType)Int(n,"ideology"),Conviction=(float)Number(n,"conviction"),Dissent=(float)Number(n,"dissent"),
                    KingdomId=Int(n,"country"),LastStateIdeology=(IdeologyType)Int(n,"stateIdeology"),LastDissentYear=Int(n,"lastYear")});
            }
            if(node.Get("offices")!=null)foreach(var n in Array(node,"offices"))
            {
                var office=new CivicOfficeState{CountryId=Int(n,"country"),HolderId=Int(n,"holder"),SinceYear=Int(n,"since"),
                    NextElectionYear=Int(n,"next"),LastAttemptYear=Int(n,"attempt"),LastCommittedProcedure=String(n,"committed")};
                foreach(var p in Array(n,"mandates"))
                {
                    int actor=Int(p,"actor");if(office.Mandates.ContainsKey(actor))throw new FormatException("Repeated personal mandate.");
                    office.Mandates.Add(actor,new CivicMandates{ActorId=actor,Lifetime=Int(p,"lifetime"),Consecutive=Int(p,"consecutive")});
                }
                state.RestoreOffice(office);
            }
            if(node.Get("allianceMotions")!=null)foreach(var n in Array(node,"allianceMotions"))
                state.AddAllianceMotion(new CivicAllianceMotion{Id=String(n,"id"),Action=(CivicAllianceAction)Int(n,"action"),AllianceId=Int(n,"alliance"),
                    ProposerId=Int(n,"proposer"),SubjectId=Int(n,"subject"),Members=IntArray(n,"members"),Stage=(CivicMotionStage)Int(n,"stage"),
                    Note=String(n,"note"),ConsentAnnounced=n.Get("consentAnnounced")!=null && Bool(n,"consentAnnounced"),Ballot=CivicBallot.Restore(ReadBallot(Need(n,"ballot")))});
            if(node.Get("countryBirths")!=null)foreach(var n in Array(node,"countryBirths"))
                state.RecordCountryBirth(Int(n,"country"),Int(n,"parent"),Int(n,"year"),Bool(n,"lawful"));
            ValidateCrossLinks(state);return state;
        }
        catch(ArgumentException ex){throw new FormatException("Invalid governance state.",ex);}
    }
    private static void ValidateCrossLinks(GovernanceState state)
    {
        foreach(var p in state.Procedures.Values)
        {
            if(p.Kind==CivicProcedureKind.Independence && p.OpeningExitLaw==RegionalExitLaw.WithCentralPermission
                && !p.HasVerifiedAuthorization(state.Procedures))
                throw new FormatException("Regional referendum has no matching approved central permission procedure.");
        }
        foreach(var office in state.Offices.Values)
        {
            if(office.HolderId==0)continue;
            if(!state.Procedures.TryGetValue(office.LastCommittedProcedure,out var p) || p.Kind!=CivicProcedureKind.HeadElection
                || p.CountryId!=office.CountryId || !p.Ballot.Closed || p.Ballot.Tally().UniquePluralityWinner!=office.HolderId)
                throw new FormatException("Office mandate has no matching individual election.");
        }
        foreach(var motion in state.AllianceMotions.Values)
        {
            foreach(var vote in motion.Ballot.Votes)
            {
                if(!state.Procedures.TryGetValue(motion.PositionId(vote.Key),out var p) || !Matches(p,vote.Key,motion.SubjectId) || NationalChoice(p)!=vote.Value)
                    throw new FormatException("Country alliance position has no matching individual vote.");
            }
            bool executable=motion.Stage==CivicMotionStage.Approved || motion.Stage==CivicMotionStage.ExecutionPending
                || motion.Stage==CivicMotionStage.Executed || motion.Stage==CivicMotionStage.ExecutionFailed;
            if(executable && motion.Action==CivicAllianceAction.Admission
                && (!state.Procedures.TryGetValue(motion.ConsentId,out var consent) || !Matches(consent,motion.SubjectId,motion.SubjectId) || NationalChoice(consent)!=CivicBallot.Yes))
                throw new FormatException("Admission execution without the applicant's saved constitutional consent.");
        }
    }
    private static bool Matches(CivicProcedure p,int voter,int subject)
        =>p.Kind==CivicProcedureKind.AlliancePosition && p.CountryId==voter && p.TargetCountryId==subject && p.CityId==0 && p.Ballot.Closed;
    private static int NationalChoice(CivicProcedure p)
    {var tally=p.Ballot.Tally();return !tally.QuorumReached || tally.ValidVotes==0?CivicBallot.Abstain:tally.BinaryAccepted?CivicBallot.Yes:CivicBallot.No;}
    private static void WriteRecognition(TinyJson.Writer w,RecognitionSnapshot s)
    {
        w.BeginObject();w.Key("enabled").Value(s.EnabledForNewWorld);w.Key("lastReview").Value(s.LastReviewYear);w.Key("generation").Value(s.CompositionGeneration);
        w.Key("powers");Ints(w,s.GreatPowers);w.Key("countries").BeginArray();
        foreach(var n in s.Countries)
        {
            w.BeginObject();w.Key("id").Value(n.CountryId);w.Key("birth").Value(n.BirthYear);w.Key("alive").Value(n.Alive);w.Key("sovereign").Value(n.Sovereign);
            w.Key("recognized").Value(n.Recognized);w.Key("year").Value(n.RecognizedYear);w.Key("origin").Value((int)n.Origin);w.Key("decision").Value(n.LastDecisionGeneration);w.EndObject();
        }
        w.EndArray();w.Key("pending").BeginArray();var ids=new List<int>(s.Pending.Keys);ids.Sort();
        foreach(int id in ids){w.BeginObject();w.Key("country").Value(id);w.Key("ballot");WriteBallot(w,s.Pending[id]);w.EndObject();}
        w.EndArray();w.EndObject();
    }
    private static RecognitionSnapshot ReadRecognition(TinyJson.Node n)
    {
        var s=new RecognitionSnapshot{EnabledForNewWorld=Bool(n,"enabled"),LastReviewYear=Int(n,"lastReview"),CompositionGeneration=Int(n,"generation"),GreatPowers=IntArray(n,"powers")};
        foreach(var c in Array(n,"countries"))s.Countries.Add(new CountryRecognitionState{CountryId=Int(c,"id"),BirthYear=Int(c,"birth"),Alive=Bool(c,"alive"),Sovereign=Bool(c,"sovereign"),
            Recognized=Bool(c,"recognized"),RecognizedYear=Int(c,"year"),Origin=(RecognitionOrigin)Int(c,"origin"),LastDecisionGeneration=Int(c,"decision")});
        foreach(var p in Array(n,"pending")){int id=Int(p,"country");if(s.Pending.ContainsKey(id))throw new FormatException("Repeated recognition procedure.");s.Pending.Add(id,ReadBallot(Need(p,"ballot")));}
        return s;
    }
    public static void WriteBallot(TinyJson.Writer w,CivicBallotSnapshot b)
    {
        w.BeginObject();w.Key("id").Value(b.Id);w.Key("electorKind").Value((int)b.ElectorKind);w.Key("opened").Value(b.OpenedYear);w.Key("duration").Value(b.DurationYears);
        w.Key("quorum").Value(b.QuorumPercent);w.Key("threshold").Value((int)b.Threshold);w.Key("closed").Value(b.Closed);w.Key("singleCandidate").Value(b.AllowSingleCandidate);
        w.Key("electors");Ints(w,b.Electors);w.Key("options");Ints(w,b.Options);w.Key("votes").BeginArray();var ids=new List<int>(b.Votes.Keys);ids.Sort();
        foreach(int id in ids){w.BeginObject();w.Key("elector").Value(id);w.Key("option").Value(b.Votes[id]);w.EndObject();}w.EndArray();w.EndObject();
    }
    public static CivicBallotSnapshot ReadBallot(TinyJson.Node n)
    {
        var b=new CivicBallotSnapshot{Id=String(n,"id"),ElectorKind=(BallotElectorKind)Int(n,"electorKind"),OpenedYear=Int(n,"opened"),DurationYears=Int(n,"duration"),
            AllowSingleCandidate=n.Get("singleCandidate")!=null && Bool(n,"singleCandidate"),QuorumPercent=Int(n,"quorum"),Threshold=(BallotThreshold)Int(n,"threshold"),Closed=Bool(n,"closed"),Electors=IntArray(n,"electors"),Options=IntArray(n,"options")};
        foreach(var v in Array(n,"votes")){int id=Int(v,"elector");if(b.Votes.ContainsKey(id))throw new FormatException("Repeated saved voter.");b.Votes.Add(id,Int(v,"option"));}return b;
    }
    public static void WriteChapter(TinyJson.Writer w,ElectoralChapter c)
    {
        c.Validate();w.BeginObject();w.Key("electors").Value((int)c.Electors);w.Key("candidates").Value((int)c.Candidates);
        w.Key("votingAge").Value(c.VotingAge);w.Key("candidacyAge").Value(c.CandidacyAge);w.Key("termYears").Value(c.TermYears);w.Key("maxTerms").Value(c.MaximumTerms);
        w.Key("limitBasis").Value((int)c.TermLimitBasis);w.Key("quorum").Value(c.ReferendumQuorumPercent);w.Key("threshold").Value((int)c.ReferendumThreshold);
        w.Key("ballotYears").Value(c.BallotYears);w.Key("foreignAuthority").Value((int)c.ForeignDecisions);w.Key("exitLaw").Value((int)c.RegionalExit);w.EndObject();
    }
    public static ElectoralChapter ReadChapter(TinyJson.Node n)
    {
        var c=new ElectoralChapter{Electors=(Franchise)Int(n,"electors"),Candidates=(Franchise)Int(n,"candidates"),VotingAge=Int(n,"votingAge"),CandidacyAge=Int(n,"candidacyAge"),
            TermYears=Int(n,"termYears"),MaximumTerms=Int(n,"maxTerms"),TermLimitBasis=(OfficeTermLimitBasis)Int(n,"limitBasis"),ReferendumQuorumPercent=Int(n,"quorum"),
            ReferendumThreshold=(BallotThreshold)Int(n,"threshold"),BallotYears=Int(n,"ballotYears"),ForeignDecisions=(ForeignDecisionAuthority)Int(n,"foreignAuthority"),RegionalExit=(RegionalExitLaw)Int(n,"exitLaw")};
        try{c.Validate();}catch(ArgumentException ex){throw new FormatException("Invalid saved electoral chapter.",ex);}return c;
    }
    private static void Ints(TinyJson.Writer w,IEnumerable<int> values){w.BeginArray();foreach(int value in values)w.Value(value);w.EndArray();}
    private static TinyJson.Node Object(TinyJson.Node n){if(n==null || n.Kind!=TinyJson.TokenKind.Object)throw new FormatException("Expected JSON object.");return n;}
    private static TinyJson.Node Need(TinyJson.Node n,string key){Object(n);return n.Get(key)??throw new FormatException("Missing governance field: "+key);}
    private static List<TinyJson.Node> Array(TinyJson.Node n,string key){var a=Need(n,key);if(a.Kind!=TinyJson.TokenKind.Array)throw new FormatException("Expected JSON array: "+key);return a.Arr;}
    private static List<int> IntArray(TinyJson.Node n,string key){var result=new List<int>();foreach(var x in Array(n,key))result.Add(Integer(x));return result;}
    private static double Number(TinyJson.Node n,string key){var x=Need(n,key);if(x.Kind!=TinyJson.TokenKind.Number||double.IsNaN(x.Num)||double.IsInfinity(x.Num))throw new FormatException("Expected finite number: "+key);return x.Num;}
    private static int Integer(TinyJson.Node n){if(n.Kind!=TinyJson.TokenKind.Number || double.IsNaN(n.Num)||double.IsInfinity(n.Num)||n.Num<int.MinValue||n.Num>int.MaxValue||Math.Truncate(n.Num)!=n.Num)throw new FormatException("Expected integer.");return (int)n.Num;}
    private static int Int(TinyJson.Node n,string key)=>Integer(Need(n,key));
    private static bool Bool(TinyJson.Node n,string key){var x=Need(n,key);if(x.Kind!=TinyJson.TokenKind.Boolean)throw new FormatException("Expected boolean: "+key);return x.Bool;}
    private static string String(TinyJson.Node n,string key){var x=Need(n,key);if(x.Kind!=TinyJson.TokenKind.String)throw new FormatException("Expected string: "+key);return x.Str;}
}
