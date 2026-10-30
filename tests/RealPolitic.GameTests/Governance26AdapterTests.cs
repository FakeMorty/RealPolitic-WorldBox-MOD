using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using RealPolitic.Core;
using Xunit;

namespace RealPolitic.Tests;

public sealed class Governance26AdapterTests : IDisposable
{
    public sealed class Resident:Actor {public bool Alive=true;public bool isAlive()=>Alive;}
    private readonly PoliticalWorld previousWorld=RealPoliticMod.World;
    private readonly MapBox previousMap=MapBox.instance;
    private readonly string previousFolder=RealPoliticMod.ModFolder;
    private readonly PoliticalWorld world=new PoliticalWorld();
    private readonly Kingdom country;
    private readonly City city,capital;
    private readonly Resident loyal,rebel;
    private readonly ElectoralChapter law;
    private readonly string folder=Path.Combine(Path.GetTempPath(),"rp26-"+Guid.NewGuid());
    public Governance26AdapterTests()
    {
        RealPoliticMod.World=world;world.BeginNewWorldGovernance("adapter-world");world.SetYear(51);
        var state=world.AddKingdom(1,IdeologyType.Monarchy,10);state.RoyalClanId=7;
        loyal=new Resident{id=101,age=40,data=new ActorData{id=101,name="Верный",clan=new Clan{id=7}}};
        rebel=new Resident{id=102,age=30,data=new ActorData{id=102,name="Инакомыслящий"}};
        capital=new City{id=8,units=new List<Actor>()};city=new City{id=9,units=new List<Actor>{loyal,rebel},leader=rebel};
        country=new CivicKingdom{id=1,king=loyal,capital=capital,cities=new List<City>{capital,city},units=city.units};
        capital.kingdom=country;city.kingdom=country;loyal.kingdom=country;rebel.kingdom=country;loyal.city=city;rebel.city=city;
        MapBox.instance=new MapBox{TestWorldTime=50*60,map_stats=new MapStats(),kingdoms=new KingdomManager{list=new List<Kingdom>{country}},cities=new CityManager{list=country.cities}};
        law=state.Constitution.Electoral;law.VotingAge=18;law.CandidacyAge=25;law.BallotYears=1;law.Electors=Franchise.Universal;law.RegionalExit=RegionalExitLaw.RegionalRight;
        Directory.CreateDirectory(folder);RealPoliticMod.ModFolder=folder;
        ActorIdeologies.Clear();
        world.Governance.RememberBelief(101,Belief(IdeologyType.Monarchy,0));world.Governance.RememberBelief(102,Belief(IdeologyType.Democracy,1));
        WorldPersistence.ResetSaveThrottle();
    }
    private CivicBelief Belief(IdeologyType ideology,float dissent)=>new CivicBelief{Ideology=ideology,Conviction=1,Dissent=dissent,KingdomId=1,LastStateIdeology=IdeologyType.Monarchy,LastDissentYear=51};
    private bool Read(out List<CivicElectorateReader.Elector> voters,out string reason)=>CivicElectorateReader.TryRead(world,country,city,law,out voters,out reason);
    private bool Open(out CivicProcedure procedure,out string reason,bool permission=false)=>CivicElectorateReader.TryOpenIssue(world,country,city,law,
        new CivicIssueContext{CurrentGovernment=IdeologyType.Monarchy,ProposedGovernment=IdeologyType.Democracy,Independence=true},"region:9",CivicProcedureKind.Independence,0,51,out procedure,out reason,permission);
    public void Dispose(){WorldPersistence.ConsumeLoadedStateFor(MapBox.instance);ActorIdeologies.Clear();RealPoliticMod.World=previousWorld;MapBox.instance=previousMap;RealPoliticMod.ModFolder=previousFolder;WorldPersistence.ResetSaveThrottle();Directory.Delete(folder,true);}
    [Fact]public void ActualLivingCitizensHaveTheirOwnPositions(){Assert.True(Read(out var people,out var reason),reason);Assert.Equal(new[]{101,102},people.Select(p=>p.Person.ActorId));Assert.Equal(IdeologyType.Monarchy,people[0].Belief.Ideology);Assert.Equal(IdeologyType.Democracy,people[1].Belief.Ideology);}
    [Fact]public void ChildrenDeadPeopleAndVisitorsDoNotVote()
    {
        var child=new Resident{age=12,data=new ActorData{id=103},kingdom=country,city=city};var dead=new Resident{age=40,data=new ActorData{id=104},kingdom=country,city=city,Alive=false};
        var visitor=new Resident{age=40,data=new ActorData{id=105},kingdom=new Kingdom{id=2},city=city};city.units.AddRange(new Actor[]{child,dead,visitor});
        Assert.True(Read(out var people,out var reason),reason);Assert.Equal(new[]{101,102},people.Select(p=>p.Person.ActorId));
    }
    [Fact]public void UnknownLifeStateDefersWholeRoll(){city.units.Add(new Actor{age=40,data=new ActorData{id=106},kingdom=country,city=city});Assert.False(Read(out var people,out var reason));Assert.Equal("life-state-unavailable",reason);Assert.Empty(people);}
    [Fact]public void OversizedRealIdIsNotWrappedIntoAnotherVoter(){city.units.Add(new Resident{age=40,data=new ActorData{id=(long)int.MaxValue+100},kingdom=country,city=city});Assert.False(Read(out var people,out var reason));Assert.Equal("unsupported-actor-id",reason);Assert.Empty(people);}
    [Fact]public void DuplicateInstanceDoesNotGetTwoVotes(){city.units.Add(loyal);Assert.True(Read(out var people,out var reason),reason);Assert.Equal(2,people.Count);}
    [Fact]public void ConflictingIdentityDefersBallot(){city.units.Add(new Resident{age=40,data=new ActorData{id=101},kingdom=country,city=city});Assert.False(Read(out _,out var reason));Assert.Equal("conflicting-actor-identity",reason);}
    [Fact]public void ClanFranchiseActuallyFiltersCitizens(){law.Electors=Franchise.Clan;Assert.True(Read(out var people,out var reason),reason);Assert.Equal(101,Assert.Single(people).Person.ActorId);}
    [Fact]public void CityLeadersAreNotables(){law.Electors=Franchise.Notables;Assert.True(Read(out var people,out var reason),reason);Assert.Equal(2,people.Count);Assert.True(people.Single(p=>p.Person.ActorId==102).Person.IsCityLeader);}
    [Fact]public void ChangedMinimumAgeActuallyChangesRoll(){law.VotingAge=21;rebel.age=20;Assert.True(Read(out var people,out var reason),reason);Assert.Equal(101,Assert.Single(people).Person.ActorId);}
    [Fact]public void SomeoneElseHomeCityDoesNotVoteInRegion(){rebel.city=capital;Assert.True(Read(out var people,out var reason),reason);Assert.Equal(101,Assert.Single(people).Person.ActorId);}
    [Fact]public void PersonalVotesAreStoredInsteadOfAnInventedPercentage(){Assert.True(Open(out var p,out var reason),reason);Assert.Equal(CivicBallot.No,p.Ballot.Votes[101]);Assert.Equal(CivicBallot.Yes,p.Ballot.Votes[102]);Assert.Same(p,world.Governance.Procedures[p.Ballot.Id]);Assert.False(p.Ballot.Closed);Assert.Equal(0,ActorIdeologies.Assigned);}
    [Fact]public void ForbiddenExitDoesNotOpenAReferendum(){law.RegionalExit=RegionalExitLaw.Forbidden;Assert.False(Open(out _,out var reason));Assert.Equal("no-lawful-exit",reason);Assert.Empty(world.Governance.Procedures);}
    [Fact]public void CentralPermissionIsActuallyRequired(){law.RegionalExit=RegionalExitLaw.WithCentralPermission;Assert.False(Open(out _,out _));Assert.True(Open(out _,out var reason,true),reason);}
    [Fact]public void CapitalCannotOpenAnIndependenceReferendum(){country.capital=city;Assert.False(Open(out _,out var reason));Assert.Equal("invalid-breakaway-region",reason);}
    [Fact]public void DuplicateProcedureDoesNotReplaceExistingVotes(){Assert.True(Open(out var original,out _));Assert.False(Open(out _,out _));Assert.Same(original,Assert.Single(world.Governance.Procedures).Value);}
    [Fact]public void SaveAndLoadPreserveActualVotesAndPersonalBeliefs()
    {
        Assert.True(Open(out var p,out _));Assert.True(WorldPersistence.Save(folder));loyal.Alive=false;
        Assert.True(WorldPersistence.Load(folder));var loaded=world.Governance.Procedures[p.Ballot.Id];Assert.Equal(CivicBallot.No,loaded.Ballot.Votes[101]);Assert.Equal(CivicBallot.Yes,loaded.Ballot.Votes[102]);
        var faith=ActorIdeologies.Get(rebel);Assert.Equal(IdeologyType.Democracy,faith.Ideology);Assert.Equal(1,faith.Dissent);Assert.Equal(0,ActorIdeologies.Assigned);
    }
    [Fact]public void UpdatedBeliefIsWrittenIntoTheSameWorldState(){var faith=ActorIdeologies.Get(rebel);faith.Dissent=.8f;string json=SaveData.Write(world);ActorIdeologies.Clear();SaveData.Apply(world,json);Assert.Equal(.8f,ActorIdeologies.Get(rebel).Dissent);}
    [Fact]public void ASecondNativeSaveInTheSameYearDoesNotLoseNewVotes()
    {
        var b=new CivicBallot("pending",BallotElectorKind.Person,51,3,new[]{101,102},new[]{1,2});b.TryCast(101,1,51);
        world.Governance.AddProcedure(new CivicProcedure(b,CivicProcedureKind.CentralPermission,1));
        var save=typeof(WorldPersistence).GetMethod("SavePostfix",BindingFlags.Static|BindingFlags.NonPublic);
        save.Invoke(null,new object[]{new object[]{folder}});b.TryCast(102,2,51);save.Invoke(null,new object[]{new object[]{folder}});
        var copy=new PoliticalWorld();SaveData.Apply(copy,File.ReadAllText(Path.Combine(folder,"realpolitic_state.json")));Assert.Equal(2,copy.Governance.Procedures["pending"].Ballot.Votes.Count);
    }
    [Fact]public void MissingSlotDoesNotImportAnotherWorldAutosave(){string global=WorldPersistence.GetAutosavePath();Directory.CreateDirectory(Path.GetDirectoryName(global));File.WriteAllText(global,SaveData.Write(world));Assert.Null(WorldPersistence.ResolveLoadPath(Path.Combine(folder,"other-slot")));}
    [Fact]public void UnboundGlobalNewWorldStateIsNotImportedIntoLegacyWorld(){Assert.True(WorldPersistence.Save());world.Clear();Assert.False(WorldPersistence.Load());Assert.False(world.Governance.Enabled);}
    [Fact]public void MalformedSaveDoesNotEraseCurrentVotes(){Assert.True(Open(out _,out _));File.WriteAllText(Path.Combine(folder,"realpolitic_state.json"),"{\"format\":18,");Assert.False(WorldPersistence.Load(folder));Assert.Single(world.Governance.Procedures);}
    [Fact]public void SuccessfulLoadHasAOneUseMapInitializationMarker(){Assert.True(WorldPersistence.Save(folder));Assert.True(WorldPersistence.Load(folder));Assert.True(WorldPersistence.ConsumeLoadedStateFor(MapBox.instance));Assert.False(WorldPersistence.ConsumeLoadedStateFor(MapBox.instance));}
    [Fact]public void UnauthoritativeRuleObjectCannotChangeTheRoll(){Assert.False(CivicElectorateReader.TryRead(world,country,city,new ElectoralChapter(),out _,out var reason));Assert.Equal("non-authoritative-electoral-rules",reason);}
    [Fact]public void LoadingMarkerDoesNotSurviveAnUnexpectedMap(){Assert.True(WorldPersistence.Save(folder));Assert.True(WorldPersistence.Load(folder));Assert.False(WorldPersistence.ConsumeLoadedStateFor(new MapBox()));Assert.False(WorldPersistence.ConsumeLoadedStateFor(MapBox.instance));}
    [Fact]public void EvenSameWorldGlobalBackupCannotOverrideASelectedCheckpoint(){Assert.True(WorldPersistence.Save());Assert.False(WorldPersistence.Load());Assert.True(world.Governance.Enabled);}
    [Fact]public void ElectoralChapterTextShowsTheActualLaws(){law.MaximumTerms=0;law.BallotYears=3;var lines=ElectoralChapterL10n.Lines(law);Assert.Contains(lines,l=>l.Contains("без ограничения"));Assert.Contains(lines,l=>l.Contains("лет: 3"));Assert.Contains(lines,l=>l.Contains("без разрешения центра"));}

    private NationalPositionRequest PositionRequest()
    {
        world.AddKingdom(2,IdeologyType.Monarchy,10);
        return new NationalPositionRequest{ProcedureId="recognition:2:1:position:1",CountryId=1,TargetCountryId=2,Year=51,LatestCompletionYear=54,Rules=law};
    }
    [Fact]public void RulerPositionUsesTheActualLivingHeadNotTheElectoralRoll()
    {
        var r=PositionRequest();law.ForeignDecisions=ForeignDecisionAuthority.Ruler;law.Electors=Franchise.Clan;
        Assert.True(new GovernanceRuntime(world,id=>country).TryOpenRecognitionPosition(r,out var p));
        Assert.Equal(0,p.Ballot.DurationYears);Assert.Equal(1,p.Ballot.EligibleCount);Assert.Equal(101,p.Ballot.Votes.Keys.Single());
        Assert.Empty(world.Governance.Procedures);
    }
    [Fact]public void DeadHeadDoesNotFallBackToAnotherResident()
    {
        var r=PositionRequest();law.ForeignDecisions=ForeignDecisionAuthority.Ruler;loyal.Alive=false;
        Assert.False(new GovernanceRuntime(world,id=>country).TryOpenRecognitionPosition(r,out _));Assert.Empty(world.Governance.Procedures);
    }
    [Fact]public void MissingHeadDoesNotBecomeARandomNationalVote()
    {
        var r=PositionRequest();law.ForeignDecisions=ForeignDecisionAuthority.Ruler;country.king=null;
        Assert.False(new GovernanceRuntime(world,id=>country).TryOpenRecognitionPosition(r,out _));
    }
    [Fact]public void NationalReferendumUsesCitizensAndConstitutionalDuration()
    {
        var r=PositionRequest();law.ForeignDecisions=ForeignDecisionAuthority.Electorate;law.BallotYears=3;
        Assert.True(new GovernanceRuntime(world,id=>country).TryOpenRecognitionPosition(r,out var p));
        Assert.Equal(3,p.Ballot.DurationYears);Assert.Equal(new[]{101,102},p.Ballot.Votes.Keys.OrderBy(id=>id));
        Assert.Same(p,world.Governance.Procedures[r.ProcedureId]);
    }
    [Fact]public void NationalReferendumRespectsRulingClanFranchise()
    {
        var r=PositionRequest();law.ForeignDecisions=ForeignDecisionAuthority.Electorate;law.Electors=Franchise.Clan;
        Assert.True(new GovernanceRuntime(world,id=>country).TryOpenRecognitionPosition(r,out var p));Assert.Equal(101,p.Ballot.Votes.Keys.Single());
    }
    [Fact]public void PositionPortRejectsAnotherWorldOrDetachedConstitution()
    {
        var r=PositionRequest();r.Rules=new ElectoralChapter();Assert.False(new GovernanceRuntime(world,id=>country).TryOpenRecognitionPosition(r,out _));
        RealPoliticMod.World=new PoliticalWorld();r.Rules=law;Assert.False(new GovernanceRuntime(world,id=>country).TryOpenRecognitionPosition(r,out _));
    }
    public sealed class CivicKingdom:Kingdom {public bool Civic=true;public bool isCiv()=>Civic;}
    [Fact]public void TechnicalKingdomsCannotBootstrapTheGreatPowers()
    {
        var technical=new CivicKingdom{id=7,Civic=false};GovernanceRuntime.ObserveBirth(world,technical,51);
        Assert.False(world.Governance.Recognition.ContainsCountry(7));
        technical.Civic=true;GovernanceRuntime.ObserveBirth(world,technical,51);
        Assert.True(world.Governance.Recognition.IsRecognized(7));
    }
    [Fact]public void BirthObservationIsIdempotentAndUsesTheActualBirthYear()
    {
        var born=new CivicKingdom{id=7};GovernanceRuntime.ObserveBirth(world,born,49);GovernanceRuntime.ObserveBirth(world,born,51);
        var saved=world.Governance.Recognition.Capture().Countries.Single();Assert.Equal(49,saved.BirthYear);
        Assert.Equal(RecognitionOrigin.FirstFiftyYears,saved.Origin);
    }
    [Fact]public void UnknownNativeClassificationDefersTheWholeAnnualFrame()
    {
        var inputs=new List<KingdomInput>{new KingdomInput{KingdomId=1,Name="Страна",Population=2}};
        Assert.Throws<InvalidOperationException>(()=>GovernanceRuntime.AnnualTick(world,51,inputs,id=>new Kingdom{id=1}));
        Assert.Empty(world.Governance.Recognition.Capture().Countries);Assert.Empty(world.Governance.CaptureIncome());
    }
    [Fact]public void LegacyWorldNeverCallsTheNewNativeReader()
    {
        world.Clear();GovernanceRuntime.AnnualTick(world,507,null,id=>throw new Exception("must not read"));Assert.False(world.Governance.Enabled);
    }
    [Fact]public void RecognitionChronicleExplainsTheRefusalAndVoteCounts()
    {
        var chronicle=new Chronicle(world,null,folder);
        var evt=new PolEvent(PolEventType.InternationalRecognitionDecided,"RP_GOV26_EV_RECOGNITION")
            .With("kingdom","Страна").With("yes",1).With("no",1).With("missing",1).With("result","RP_GOV26_REFUSED");
        string line=(string)typeof(Chronicle).GetMethod("DetailLine",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(chronicle,new object[]{evt});
        Assert.Contains("Страна",line);Assert.Contains("за: 1",line);Assert.DoesNotContain("{",line);
        Assert.DoesNotContain("RP_GOV26",line);
    }

    [Fact]public void AnnualNativeAdapterCollectsThreeActualHeadsAndRecognizesTheApplicant()
    {
        law.ForeignDecisions=ForeignDecisionAuthority.Ruler;
        var native=new Dictionary<int,Kingdom>();var inputs=new List<KingdomInput>();
        for(int id=1;id<=5;id++)
        {
            if(id!=1)world.AddKingdom(id,IdeologyType.Monarchy,id==5?51:10);
            world.GetKingdom(id).Constitution.Electoral.ForeignDecisions=ForeignDecisionAuthority.Ruler;
            var head=new Resident{id=id*100+1,age=40,data=new ActorData{id=id*100+1,name="Глава "+id}};
            var realm=new CivicKingdom{id=id,king=head};head.kingdom=realm;native.Add(id,realm);
            var belief=Belief(IdeologyType.Monarchy,0);belief.KingdomId=id;world.Governance.RememberBelief(id*100+1,belief);
            inputs.Add(new KingdomInput{KingdomId=id,Name="Страна "+id,Population=100,Warriors=(5-id)*10,Gold=999999});
        }
        GovernanceRuntime.AnnualTick(world,51,inputs,id=>native[id]);
        Assert.True(world.Governance.Recognition.IsRecognized(5));Assert.Equal(3,world.Governance.Procedures.Count);
        Assert.Equal(new[]{101,201,301},world.Governance.Procedures.Values.SelectMany(p=>p.Ballot.Votes.Keys).OrderBy(id=>id));
        Assert.Empty(world.Governance.CaptureIncome());
    }

    public sealed class VotingRegion:City
    {
        public bool RebellionRequested;
        public int Creations;
        public bool Refuse;
        public new Kingdom makeOwnKingdom(Actor actor,bool pRebellion=false,bool pFellApart=false)
        {
            RebellionRequested=pRebellion;Creations++;if(Refuse)return null;
            var old=kingdom;var born=new CivicKingdom{id=99,name="Новая страна",king=actor,capital=this,cities=new List<City>{this},units=units};
            old.cities.Remove(this);kingdom=born;foreach(var resident in units)resident.kingdom=born;
            MapBox.instance.kingdoms.list.Add(born);return born;
        }
    }
    private VotingRegion Region(bool loyalVotes=false)
    {
        var region=new VotingRegion{id=9,name="Область",kingdom=country,leader=rebel,units=new List<Actor>{loyal,rebel}};
        country.cities[1]=region;country.units=region.units;loyal.city=region;rebel.city=region;
        MapBox.instance.cities.list=new List<City>(country.cities);
        world.AddCity(9,1).Dominant=IdeologyType.Democracy;
        if(!loyalVotes)world.Governance.RememberBelief(101,Belief(IdeologyType.Democracy,1));
        return region;
    }
    [Fact]public void ActualHeadElectionReadsCandidatesAndConfirmsTheExactNativeWinner()
    {
        var state=world.GetKingdom(1);state.Constitution.StateForm=HeadOfState.Elected;
        law.Candidates=Franchise.Notables;law.Electors=Franchise.Universal;
        // The city head is the only passive-rights candidate, while both citizens vote.
        state.RoyalClanId=0;loyal.data.clan=null;rebel.data.clan=null;
        Assert.True(CivicHeadElections.Tick(world,state,51,new CivicElectionRuntime(world)));
        Assert.Same(rebel,country.king);Assert.Equal(102,state.Ruler.ActorId);Assert.Equal(1,world.Governance.Offices[1].Mandates[102].Lifetime);
        Assert.All(world.Governance.Procedures.Values,p=>Assert.Equal(CivicProcedureStage.Executed,p.Stage));
    }
    [Fact]public void ElectionCitizenReaderDoesNotFilterCandidatesByActiveFranchise()
    {
        law.Electors=Franchise.Clan;law.Candidates=Franchise.Universal;
        Assert.True(CivicElectorateReader.TryReadCitizens(world,country,law,out var people,out var reason),reason);
        Assert.Equal(new[]{101,102},people.Select(p=>p.Person.ActorId));Assert.True(Read(out var voters,out _));Assert.Equal(101,voters.Single().Person.ActorId);
    }
    [Fact]public void LegalRegionalReferendumCreatesOneCountryWithoutRequestingRebellion()
    {
        var region=Region();Assert.True(CivicRegionalRuntime.Request(world,country,region,51));
        Assert.Equal(0,region.Creations);CivicRegionalRuntime.AnnualTick(world,MapBox.instance,52);
        Assert.Equal(1,region.Creations);Assert.False(region.RebellionRequested);Assert.Equal(99,region.kingdom.id);
        Assert.Same(capital,country.cities.Single());Assert.NotNull(world.GetKingdom(99));
        Assert.Equal(CivicProcedureStage.Executed,world.Governance.Procedures.Values.Single().Stage);
        CivicRegionalRuntime.AnnualTick(world,MapBox.instance,52);Assert.Equal(1,region.Creations);
    }
    [Fact]public void FailedRegionalVoteLeavesCityInTheMetropolisAndStartsTenYearCooldown()
    {
        var region=Region(true);Assert.True(CivicRegionalRuntime.Request(world,country,region,51));
        CivicRegionalRuntime.AnnualTick(world,MapBox.instance,52);Assert.Same(country,region.kingdom);Assert.Equal(0,region.Creations);
        Assert.Equal(CivicProcedureStage.Rejected,world.Governance.Procedures.Values.Single().Stage);
        Assert.Equal(62,world.Governance.Initiatives[9].NextPermittedYear);
        Assert.True(CivicRegionalRuntime.Request(world,country,region,53));Assert.Single(world.Governance.Procedures);
    }
    [Fact]public void PeacefulMovementRequiresThreeActualConsecutiveLowObedienceYears()
    {
        var region=Region();CivicRegionalRuntime.AnnualTick(world,MapBox.instance,51);CivicRegionalRuntime.AnnualTick(world,MapBox.instance,52);
        Assert.Empty(world.Governance.Procedures);CivicRegionalRuntime.AnnualTick(world,MapBox.instance,53);
        Assert.Single(world.Governance.Procedures);Assert.Equal(0,region.Creations);
        CivicRegionalRuntime.AnnualTick(world,MapBox.instance,54);Assert.Equal(1,region.Creations);
    }
    [Fact]public void AGapInAnnualObservationBreaksThePeacefulMovementStreak()
    {
        Region();CivicRegionalRuntime.AnnualTick(world,MapBox.instance,51);CivicRegionalRuntime.AnnualTick(world,MapBox.instance,53);
        CivicRegionalRuntime.AnnualTick(world,MapBox.instance,54);Assert.Empty(world.Governance.Procedures);
        CivicRegionalRuntime.AnnualTick(world,MapBox.instance,55);Assert.Single(world.Governance.Procedures);
    }
    [Fact]public void CapitalAndOneCityStateCannotRequestLawfulExit()
    {
        var region=Region();Assert.False(CivicRegionalRuntime.Request(world,country,capital,51));country.cities.Remove(capital);
        Assert.False(CivicRegionalRuntime.Request(world,country,region,51));Assert.Empty(world.Governance.Procedures);
    }
    [Fact]public void ForbiddenExitDoesNotTurnAPeacefulMovementIntoAWar()
    {
        var region=Region();law.RegionalExit=RegionalExitLaw.Forbidden;
        foreach(int year in new[]{51,52,53,54})CivicRegionalRuntime.AnnualTick(world,MapBox.instance,year);
        Assert.Empty(world.Governance.Procedures);Assert.Equal(0,region.Creations);
    }
    [Fact]public void RulerMayRefusePriorCentralPermissionWithoutOpeningARegionalVote()
    {
        var region=Region(true);law.RegionalExit=RegionalExitLaw.WithCentralPermission;law.ForeignDecisions=ForeignDecisionAuthority.Ruler;
        Assert.True(CivicRegionalRuntime.Request(world,country,region,51));CivicRegionalRuntime.AnnualTick(world,MapBox.instance,51);
        var p=world.Governance.Procedures.Values.Single();Assert.Equal(CivicProcedureKind.CentralPermission,p.Kind);Assert.Equal(CivicProcedureStage.Rejected,p.Stage);
        Assert.Equal(0,region.Creations);Assert.Equal(61,world.Governance.Initiatives[9].NextPermittedYear);
    }
    [Fact]public void ApprovedCentralPermissionIsFollowedByTheSeparateRegionalReferendum()
    {
        var region=Region();law.RegionalExit=RegionalExitLaw.WithCentralPermission;law.ForeignDecisions=ForeignDecisionAuthority.Ruler;
        Assert.True(CivicRegionalRuntime.Request(world,country,region,51));CivicRegionalRuntime.AnnualTick(world,MapBox.instance,51);
        Assert.Equal(2,world.Governance.Procedures.Count);Assert.Equal(0,region.Creations);
        Assert.Contains(world.Governance.Procedures.Values,p=>p.Kind==CivicProcedureKind.CentralPermission && p.Stage==CivicProcedureStage.Executed);
        CivicRegionalRuntime.AnnualTick(world,MapBox.instance,52);Assert.Equal(1,region.Creations);Assert.False(region.RebellionRequested);
    }
    [Fact]public void NewCapitalDesignationCancelsAnUnfinishedRegionalVote()
    {
        var region=Region();CivicRegionalRuntime.Request(world,country,region,51);country.capital=region;
        CivicRegionalRuntime.AnnualTick(world,MapBox.instance,52);Assert.Equal(0,region.Creations);
        Assert.Equal(CivicProcedureStage.Cancelled,world.Governance.Procedures.Values.Single().Stage);Assert.Equal(62,world.Governance.Initiatives[9].NextPermittedYear);
    }
    [Fact]public void ReloadedRegionalVotesDoNotChangeWhenLivingBeliefsChange()
    {
        var region=Region();CivicRegionalRuntime.Request(world,country,region,51);string save=SaveData.Write(world);
        SaveData.Apply(world,save);ActorIdeologies.Clear();world.Governance.RememberBelief(101,Belief(IdeologyType.Monarchy,0));world.Governance.RememberBelief(102,Belief(IdeologyType.Monarchy,0));
        CivicRegionalRuntime.AnnualTick(world,MapBox.instance,52);Assert.Equal(1,region.Creations);Assert.False(region.RebellionRequested);
    }
    [Fact]public void CrisisUsesALegalVoteBeforeAnyNativeSecessionWhenTheLawAllowsIt()
    {
        var region=Region();Assert.True(CivicRegionalRuntime.RequestCrisis(world,MapBox.instance,1,new[]{8,9},51));
        Assert.Single(world.Governance.Procedures);Assert.Equal(0,region.Creations);
    }
    [Fact]public void UnlawfulCrisisUsesOneCityAndExplicitlyRequestsRebellion()
    {
        var region=Region();law.RegionalExit=RegionalExitLaw.Forbidden;
        Assert.True(CivicRegionalRuntime.RequestCrisis(world,MapBox.instance,1,new[]{8,9},51));
        Assert.True(region.RebellionRequested);Assert.Equal(1,region.Creations);Assert.Empty(world.Governance.Procedures);
        Assert.Contains(world.Bus.OfType(PolEventType.RegionalCivicProcedure),e=>e.Get("outcome")=="RP_GOV26_REGION_REBELLION_NO_WAR");
    }
    [Fact]public void GlobalSecessionDisableStopsLawfulAndCrisisExecution()
    {
        var region=Region();world.Params.AllowSecession=false;Assert.False(CivicRegionalRuntime.Request(world,country,region,51));
        Assert.False(CivicRegionalRuntime.RequestCrisis(world,MapBox.instance,1,new[]{9},51));Assert.Equal(0,region.Creations);
    }
    [Fact]public void LawfulBirthKeepsItsMetropolisAndLegalOriginSeparateFromRecognition()
    {
        var region=Region();Assert.True(CivicRegionalRuntime.Request(world,country,region,51));CivicRegionalRuntime.AnnualTick(world,MapBox.instance,52);
        var birth=world.Governance.CountryBirths[99];Assert.Equal(1,birth.ParentId);Assert.True(birth.Lawful);
        var restored=new PoliticalWorld();SaveData.Apply(restored,SaveData.Write(world));Assert.Equal(1,restored.Governance.CountryBirths[99].ParentId);
    }
    [Fact]public void SuccessfulElectionEndsAnOldRegencyInsteadOfRestoringTheFormerHeirLater()
    {
        var state=world.GetKingdom(1);state.Constitution.StateForm=HeadOfState.Elected;law.Candidates=Franchise.Notables;
        state.RoyalClanId=0;loyal.data.clan=null;rebel.data.clan=null;state.Regency=new RegencyState();state.Regency.Start(1,"Регент",101);
        state.Regency.HeirActorId=999;state.Regency.CrownSitsWithRegent=true;
        Assert.True(CivicHeadElections.Tick(world,state,51,new CivicElectionRuntime(world)));Assert.False(state.Regency.IsActive);Assert.Equal(0,state.Regency.HeirActorId);
    }

    [Fact]public void RegionalRightReferendumCompletesWhenLawLaterRequiresCentralPermission()
    {
        var region=Region();Assert.True(CivicRegionalRuntime.Request(world,country,region,51));
        var p=world.Governance.Procedures.Values.Single();
        Assert.Equal(RegionalExitLaw.RegionalRight,p.OpeningExitLaw);
        Assert.Equal("",p.AuthorizedByProcedureId);
        law.RegionalExit=RegionalExitLaw.WithCentralPermission;CivicRegionalRuntime.AnnualTick(world,MapBox.instance,52);
        Assert.Equal(1,region.Creations);Assert.False(region.RebellionRequested);
        Assert.Equal(CivicProcedureStage.Executed,p.Stage);
        Assert.Equal(RegionalExitLaw.RegionalRight,p.OpeningExitLaw);
        Assert.Equal("",p.AuthorizedByProcedureId);
    }
    [Fact]public void RegionalRightReferendumCompletesWhenLawLaterBecomesForbidden()
    {
        var region=Region();Assert.True(CivicRegionalRuntime.Request(world,country,region,51));
        law.RegionalExit=RegionalExitLaw.Forbidden;CivicRegionalRuntime.AnnualTick(world,MapBox.instance,52);
        Assert.Equal(1,region.Creations);Assert.False(region.RebellionRequested);
        Assert.Equal(CivicProcedureStage.Executed,world.Governance.Procedures.Values.Single().Stage);
    }
    [Fact]public void ReloadPreservesOpeningRegionalRightBasisAcrossMidProcedureLawChange()
    {
        var region=Region();Assert.True(CivicRegionalRuntime.Request(world,country,region,51));
        law.RegionalExit=RegionalExitLaw.Forbidden;SaveData.Apply(world,SaveData.Write(world));
        var restored=world.Governance.Procedures.Values.Single();
        Assert.Equal(RegionalExitLaw.RegionalRight,restored.OpeningExitLaw);
        Assert.Equal("",restored.AuthorizedByProcedureId);
        CivicRegionalRuntime.AnnualTick(world,MapBox.instance,52);
        Assert.Equal(1,region.Creations);Assert.False(region.RebellionRequested);
        Assert.Equal(CivicProcedureStage.Executed,restored.Stage);
    }
    [Fact]public void ApprovedCentralPermissionReferendumCompletesWhenLawLaterBecomesForbidden()
    {
        var region=Region();law.RegionalExit=RegionalExitLaw.WithCentralPermission;law.ForeignDecisions=ForeignDecisionAuthority.Ruler;
        Assert.True(CivicRegionalRuntime.Request(world,country,region,51));CivicRegionalRuntime.AnnualTick(world,MapBox.instance,51);
        var referendum=world.Governance.Procedures.Values.Single(p=>p.Kind==CivicProcedureKind.Independence);
        Assert.Equal(RegionalExitLaw.WithCentralPermission,referendum.OpeningExitLaw);
        Assert.Equal("regional:1:9:51",referendum.AuthorizedByProcedureId);
        law.RegionalExit=RegionalExitLaw.Forbidden;SaveData.Apply(world,SaveData.Write(world));
        CivicRegionalRuntime.AnnualTick(world,MapBox.instance,52);
        Assert.Equal(1,region.Creations);Assert.False(region.RebellionRequested);
        Assert.All(world.Governance.Procedures.Values,p=>Assert.Equal(CivicProcedureStage.Executed,p.Stage));
    }
    [Fact]public void InProgressCentralPermissionOpensRegionalReferendumEvenIfLawBecomesForbiddenMidVote()
    {
        var region=Region();law.RegionalExit=RegionalExitLaw.WithCentralPermission;law.ForeignDecisions=ForeignDecisionAuthority.Electorate;
        Assert.True(CivicRegionalRuntime.Request(world,country,region,51));
        law.RegionalExit=RegionalExitLaw.Forbidden;
        CivicRegionalRuntime.AnnualTick(world,MapBox.instance,52);
        Assert.Equal(2,world.Governance.Procedures.Count);Assert.Equal(0,region.Creations);
        CivicRegionalRuntime.AnnualTick(world,MapBox.instance,53);
        Assert.Equal(1,region.Creations);Assert.False(region.RebellionRequested);
        Assert.All(world.Governance.Procedures.Values,p=>Assert.Equal(CivicProcedureStage.Executed,p.Stage));
    }
    [Fact]public void FailedReferendumAfterMidProcedureLawChangeLeavesRegionInMetropolis()
    {
        var region=Region(true);Assert.True(CivicRegionalRuntime.Request(world,country,region,51));
        law.RegionalExit=RegionalExitLaw.Forbidden;CivicRegionalRuntime.AnnualTick(world,MapBox.instance,52);
        Assert.Equal(0,region.Creations);Assert.Same(country,region.kingdom);
        Assert.Equal(CivicProcedureStage.Rejected,world.Governance.Procedures.Values.Single().Stage);
        Assert.Equal(62,world.Governance.Initiatives[9].NextPermittedYear);
    }
    [Fact]public void LegacyProcedureWithoutPersistedOpeningBasisCannotBypassNewCentralPermissionRequirement()
    {
        var region=Region();
        var legacyBallot=new CivicBallot("legacy-independence",BallotElectorKind.Person,51,1,new[]{101,102},new[]{1,2});
        legacyBallot.Collect(51,_=>1);
        world.Governance.AddProcedure(new CivicProcedure(legacyBallot,CivicProcedureKind.Independence,1,9));
        law.RegionalExit=RegionalExitLaw.WithCentralPermission;
        string v2Json=SaveData.Write(world).Replace("\"schema\":"+GovernanceCodec.SchemaVersion,"\"schema\":2");
        SaveData.Apply(world,v2Json);
        Assert.Null(world.Governance.Procedures["legacy-independence"].OpeningExitLaw);
        CivicRegionalRuntime.AnnualTick(world,MapBox.instance,52);
        Assert.Equal(0,region.Creations);Assert.Same(country,region.kingdom);
        Assert.Equal(CivicProcedureStage.Cancelled,world.Governance.Procedures["legacy-independence"].Stage);
    }
    [Fact]public void SavedRealCentralPermissionStillAuthorizesItsOwnRegionalReferendum()
    {
        var region=Region();law.RegionalExit=RegionalExitLaw.WithCentralPermission;law.ForeignDecisions=ForeignDecisionAuthority.Ruler;
        Assert.True(CivicRegionalRuntime.Request(world,country,region,51));CivicRegionalRuntime.AnnualTick(world,MapBox.instance,51);
        var permission=world.Governance.Procedures.Values.Single(p=>p.Kind==CivicProcedureKind.CentralPermission);
        Assert.Equal(CivicProcedureStage.Executed,permission.Stage);SaveData.Apply(world,SaveData.Write(world));
        CivicRegionalRuntime.AnnualTick(world,MapBox.instance,52);
        Assert.Equal(1,region.Creations);Assert.False(region.RebellionRequested);
        Assert.All(world.Governance.Procedures.Values,p=>Assert.Equal(CivicProcedureStage.Executed,p.Stage));
    }

    [Fact]public void SavedClosedRegionalBallotCannotExecuteBeforeItsLegalDeadline()
    {
        var region=Region();law.BallotYears=3;Assert.True(CivicRegionalRuntime.Request(world,country,region,51));
        var p=world.Governance.Procedures.Values.Single();Assert.True(p.Ballot.TryClose(54,out _));
        CivicRegionalRuntime.AnnualTick(world,MapBox.instance,52);Assert.Equal(0,region.Creations);Assert.Equal(CivicProcedureStage.Voting,p.Stage);
        CivicRegionalRuntime.AnnualTick(world,MapBox.instance,54);Assert.Equal(1,region.Creations);
    }
    [Fact]public void SavedClosedCentralBallotCannotGrantPermissionBeforeItsLegalDeadline()
    {
        var region=Region();law.BallotYears=3;law.ForeignDecisions=ForeignDecisionAuthority.Electorate;law.RegionalExit=RegionalExitLaw.WithCentralPermission;
        Assert.True(CivicRegionalRuntime.Request(world,country,region,51));var p=world.Governance.Procedures.Values.Single();
        Assert.True(p.Ballot.TryClose(54,out _));Assert.True(p.Ballot.Tally().BinaryAccepted);
        CivicRegionalRuntime.AnnualTick(world,MapBox.instance,52);Assert.Single(world.Governance.Procedures);Assert.Equal(CivicProcedureStage.Voting,p.Stage);
        CivicRegionalRuntime.AnnualTick(world,MapBox.instance,54);Assert.Equal(2,world.Governance.Procedures.Count);
        CivicRegionalRuntime.AnnualTick(world,MapBox.instance,57);Assert.Equal(1,region.Creations);
    }
    [Theory][InlineData(2,9,50)][InlineData(1,8,50)][InlineData(1,9,52)]
    public void CentralPermissionCannotBeBorrowedFromAnotherCountryCityOrLaterDate(int permissionCountry,int permissionCity,int permissionYear)
    {
        var region=Region();law.RegionalExit=RegionalExitLaw.WithCentralPermission;
        var permission=new CivicBallot("permission-boundary",BallotElectorKind.Person,permissionYear,0,new[]{101},new[]{1,2});
        permission.TryCast(101,1,permissionYear);permission.TryClose(permissionYear,out _);
        world.Governance.AddProcedure(new CivicProcedure(permission,CivicProcedureKind.CentralPermission,permissionCountry,permissionCity,stage:CivicProcedureStage.Executed));
        var referendum=new CivicBallot("permission-boundary:referendum",BallotElectorKind.Person,51,1,new[]{101,102},new[]{1,2});
        referendum.Collect(51,_=>1);var p=new CivicProcedure(referendum,CivicProcedureKind.Independence,1,9);world.Governance.AddProcedure(p);
        CivicRegionalRuntime.AnnualTick(world,MapBox.instance,52);
        Assert.Equal(0,region.Creations);Assert.Equal(CivicProcedureStage.Cancelled,p.Stage);
    }

}
