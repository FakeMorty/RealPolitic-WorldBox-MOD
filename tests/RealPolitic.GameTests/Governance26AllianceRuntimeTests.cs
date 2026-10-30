using System;
using System.Collections.Generic;
using System.Linq;
using RealPolitic.Core;
using Xunit;

namespace RealPolitic.Tests;
public sealed class Governance26AllianceRuntimeTests:IDisposable
{
    public sealed class Person:Actor {public bool isAlive()=>true;}
    public sealed class Alliance
    {
        public long id=80;
        public List<Kingdom> kingdoms_list=new List<Kingdom>();
        public bool RefuseRemoval,DissolveIfSolo;
        public int Removals;
        public void leave(Kingdom country,bool recalc){Removals++;if(!RefuseRemoval){kingdoms_list.Remove(country);if(DissolveIfSolo && kingdoms_list.Count<2)kingdoms_list.Clear();}}
    }
    public sealed class Manager
    {
        public List<Alliance> list=new List<Alliance>();
        public bool Refuse;
        public int Calls;
        public bool forceAlliance(Kingdom a,Kingdom b)
        {
            Calls++;if(Refuse)return false;var alliance=list.FirstOrDefault(x=>x.kingdoms_list.Contains(a)||x.kingdoms_list.Contains(b));
            if(alliance==null){alliance=new Alliance();list.Add(alliance);}
            if(!alliance.kingdoms_list.Contains(a))alliance.kingdoms_list.Add(a);if(!alliance.kingdoms_list.Contains(b))alliance.kingdoms_list.Add(b);return true;
        }
    }
    private readonly PoliticalWorld previous=RealPoliticMod.World;
    private readonly MapBox previousMap=MapBox.instance;
    private readonly PoliticalWorld world=new PoliticalWorld();
    private readonly Manager manager=new Manager();
    private readonly List<Kingdom> countries=new List<Kingdom>();
    private readonly List<City> cities=new List<City>();
    public Governance26AllianceRuntimeTests()
    {
        NativeWorldLifecycle.ResetForTests();RealPoliticMod.World=world;world.BeginNewWorldGovernance("native-alliance-test");world.SetYear(51);ActorIdeologies.Clear();
        for(int id=1;id<=5;id++)
        {
            var state=world.AddKingdom(id,IdeologyType.Monarchy,10);state.Constitution.Electoral.ForeignDecisions=ForeignDecisionAuthority.Ruler;
            world.Governance.Recognition.RegisterCountry(id,10);
            var head=new Person{id=id*100,age=50,data=new ActorData{id=id*100,name="Глава "+id}};
            var city=new City{id=id*10,leader=head,units=new List<Actor>{head}};
            var country=new Kingdom{id=id,name="Страна "+id,king=head,capital=city,cities=new List<City>{city},units=city.units};
            head.kingdom=country;head.city=city;city.kingdom=country;countries.Add(country);cities.Add(city);
            world.Governance.RememberBelief(id*100,new CivicBelief{Ideology=IdeologyType.Monarchy,LastStateIdeology=IdeologyType.Monarchy,Conviction=1,KingdomId=id,LastDissentYear=51});
        }
        MapBox.instance=new MapBox{TestWorldTime=50*60,alliances=manager,kingdoms=new KingdomManager{list=countries},cities=new CityManager{list=cities}};
    }
    public void Dispose(){NativeWorldLifecycle.ResetForTests();ActorIdeologies.Clear();RealPoliticMod.World=previous;MapBox.instance=previousMap;}
    private Alliance Existing(){var a=new Alliance{kingdoms_list=countries.Take(3).ToList()};manager.list.Add(a);return a;}
    private void Tick(int year=51)=>CivicAllianceMotions.Tick(world,year,new CivicAllianceRuntime(world));
    [Fact]public void ThreeRealHeadsAndApplicantConsentAdmitTheExactCountry()
    {
        var existing=Existing();Assert.True(CivicAllianceRuntime.RequestAdmission(world,countries[1],countries[4],51));Assert.Equal(0,manager.Calls);
        Tick();Assert.Equal(1,manager.Calls);Assert.Contains(countries[4],existing.kingdoms_list);Assert.Equal(4,existing.kingdoms_list.Count);
        Assert.Equal(CivicMotionStage.Executed,world.Governance.AllianceMotions.Values.Single().Stage);Assert.Equal(4,world.Governance.Procedures.Count);
    }
    [Fact]public void OldImmediateForceCallOnlyProposesAndNeverBypassesTheVotes()
    {
        Existing();Assert.False(DiplomacyExecutor.ForceAlliance(countries[0],countries[4]));Assert.Equal(0,manager.Calls);Assert.Single(world.Governance.AllianceMotions);
        Tick();Assert.Equal(1,manager.Calls);
    }
    [Fact]public void AdmissionDoesNotMergeTwoExistingNativeAlliances()
    {
        Existing();manager.list.Add(new Alliance{id=81,kingdoms_list=new List<Kingdom>{countries[3],countries[4]}});
        Assert.False(CivicAllianceRuntime.RequestAdmission(world,countries[0],countries[4],51));Assert.Empty(world.Governance.AllianceMotions);Assert.Equal(0,manager.Calls);
    }
    [Fact]public void ExpulsionUsesTheNativeApiAndExcludesTheTargetFromVoting()
    {
        var existing=Existing();world.GetKingdom(3).Ideology=IdeologyType.Egalitarianism;
        Assert.True(CivicAllianceRuntime.RequestExpulsion(world,1,3,51));Tick();
        Assert.Equal(1,existing.Removals);Assert.DoesNotContain(countries[2],existing.kingdoms_list);
        Assert.Equal(CivicMotionStage.Executed,world.Governance.AllianceMotions.Values.Single().Stage);
        Assert.All(world.Governance.Procedures.Values,p=>Assert.NotEqual(3,p.CountryId));
    }
    [Fact]public void ARefusedNativeExpulsionDoesNotEditTheMembershipListAsAFallback()
    {
        var existing=Existing();existing.RefuseRemoval=true;world.GetKingdom(3).Ideology=IdeologyType.Egalitarianism;
        Assert.True(CivicAllianceRuntime.RequestExpulsion(world,1,3,51));Tick();Assert.Equal(3,existing.kingdoms_list.Count);
        Assert.Equal(CivicMotionStage.ExecutionFailed,world.Governance.AllianceMotions.Values.Single().Stage);
    }
    [Fact]public void NativeAdmissionFalseIsNotASuccessAndDoesNotCreateAPoliticalUnion()
    {
        var existing=Existing();manager.Refuse=true;CivicAllianceRuntime.RequestAdmission(world,countries[0],countries[4],51);Tick();
        Assert.Equal(3,existing.kingdoms_list.Count);Assert.Equal(CivicMotionStage.ExecutionFailed,world.Governance.AllianceMotions.Values.Single().Stage);Assert.Null(world.UnionOf(1));
    }
    [Fact]public void CandidateMayProposeJoiningThroughAnyExistingMember()
    {Existing();Assert.True(CivicAllianceRuntime.RequestAdmission(world,countries[4],countries[1],51));Tick();Assert.True(AllianceSafety.Same(countries[0],countries[4]));}
    [Fact]public void DoubleMembershipCancelsTheVoteWithoutTryingToRepairCollections()
    {
        var existing=Existing();CivicAllianceRuntime.RequestAdmission(world,countries[0],countries[4],51);
        manager.list.Add(new Alliance{id=90,kingdoms_list=new List<Kingdom>{countries[0],countries[3]}});Tick();
        Assert.Equal(0,manager.Calls);Assert.Equal(3,existing.kingdoms_list.Count);Assert.Equal(CivicMotionStage.Cancelled,world.Governance.AllianceMotions.Values.Single().Stage);
    }
    [Fact]public void NewAllianceFormationNeedsBothRealFounders()
    {Assert.True(CivicAllianceRuntime.RequestAdmission(world,countries[0],countries[4],51));Tick();Assert.True(AllianceSafety.Same(countries[0],countries[4]));Assert.Equal(2,world.Governance.Procedures.Count);}
    [Fact]public void PendingReloadRecognizesAnAlreadyAppliedExactMembershipWithoutCallingNativeAgain()
    {
        var existing=Existing();CivicAllianceRuntime.RequestAdmission(world,countries[0],countries[4],51);Tick();var motion=world.Governance.AllianceMotions.Values.Single();
        motion.Stage=CivicMotionStage.ExecutionPending;SaveData.Apply(world,SaveData.Write(world));Tick();
        Assert.Equal(1,manager.Calls);Assert.Equal(4,existing.kingdoms_list.Count);Assert.Equal(CivicMotionStage.Executed,world.Governance.AllianceMotions.Values.Single().Stage);
    }
    [Fact]public void AThreeYearPopulationVoteUsesTheOriginalCitizensNotNewPercentages()
    {
        Existing();var law=world.GetKingdom(2).Constitution.Electoral;law.ForeignDecisions=ForeignDecisionAuthority.Electorate;law.Electors=Franchise.Universal;law.BallotYears=3;
        CivicAllianceRuntime.RequestAdmission(world,countries[1],countries[4],51);Tick();Assert.Equal(0,manager.Calls);
        var saved=world.Governance.Procedures.Values.Single(p=>p.CountryId==2);Assert.Equal(200,saved.Ballot.Votes.Keys.Single());
        Tick(54);Assert.Equal(1,manager.Calls);Assert.Equal(1,saved.Ballot.Votes[200]);
    }
    [Fact]public void RuntimeRequestDoesNotApplyAnUnrecognizedNewCountry()
    {
        var restored=world.Governance.Recognition.Capture();var applicant=restored.Countries.Single(c=>c.CountryId==5);
        applicant.Recognized=false;applicant.Origin=RecognitionOrigin.None;applicant.RecognizedYear=-1;
        world.RestoreGovernance(new GovernanceState(InternationalRecognition.Restore(restored),"native-alliance-test"));Existing();Assert.False(CivicAllianceRuntime.RequestAdmission(world,countries[0],countries[4],51));Assert.Equal(0,manager.Calls);
    }
    [Fact]public void ExpellingOneOfTwoCountriesCanConfirmTheNativesDissolvedAlliance()
    {
        var group=Existing();group.kingdoms_list.Remove(countries[1]);group.DissolveIfSolo=true;world.GetKingdom(3).Ideology=IdeologyType.Egalitarianism;
        Assert.True(CivicAllianceRuntime.RequestExpulsion(world,1,3,51));Tick();Assert.Empty(group.kingdoms_list);
        Assert.Equal(CivicMotionStage.Executed,world.Governance.AllianceMotions.Values.Single().Stage);Assert.Equal(1,group.Removals);
    }

    [Fact]public void DelayedTickAfterReloadKeepsTheRealElectorateVoteAndExecutesOnlyOnce()
    {
        var existing=Existing();var law=world.GetKingdom(2).Constitution.Electoral;
        law.ForeignDecisions=ForeignDecisionAuthority.Electorate;law.Electors=Franchise.Universal;law.BallotYears=3;
        Assert.True(CivicAllianceRuntime.RequestAdmission(world,countries[1],countries[4],51));Tick();
        var ballot=world.Governance.Procedures.Values.Single(p=>p.CountryId==2).Ballot;
        Assert.Equal(1,ballot.Votes[200]);Assert.Equal(0,manager.Calls);
        SaveData.Apply(world,SaveData.Write(world));Tick(60);Tick(61);
        Assert.Equal(CivicMotionStage.Executed,world.Governance.AllianceMotions.Values.Single().Stage);
        Assert.Equal(1,manager.Calls);Assert.Contains(countries[4],existing.kingdoms_list);
        Assert.Equal(1,world.Governance.Procedures.Values.Single(p=>p.CountryId==2).Ballot.Votes[200]);
    }
    [Fact]public void NewbornCountryCannotBeDraggedIntoAFormalAlliance()
    {
        world.GetKingdom(5).FoundedYear=45;
        Assert.False(CivicAllianceRuntime.RequestAdmission(world,countries[0],countries[4],51));
        Assert.Empty(world.Governance.AllianceMotions);Assert.Equal(0,manager.Calls);
    }
    [Fact]public void AllianceAdmissionStopsAtTheAntiHegemonyCap()
    {
        manager.list.Add(new Alliance{id=82,kingdoms_list=countries.Take(4).ToList()});
        Assert.False(CivicAllianceRuntime.RequestAdmission(world,countries[0],countries[4],51));
        Assert.Empty(world.Governance.AllianceMotions);Assert.Equal(0,manager.Calls);
    }

    public sealed class KingdomDataWithAllianceId:KingdomData {public long allianceID;}
    [Fact]public void NativeNewAllianceAndJoinGuardsRouteUnapprovedCallsIntoCivicMotions()
    {
        object created="unapproved";
        Assert.False(NativeAllianceGuards.NewAlliancePrefix(countries[3],countries[4],ref created));
        Assert.Null(created);
        Assert.Single(world.Governance.AllianceMotions);
        var existing=Existing();bool joined=true;
        Assert.False(NativeAllianceGuards.JoinPrefix(existing,countries[3],ref joined));
        Assert.False(joined);
        Assert.Equal(2,world.Governance.AllianceMotions.Count);
    }
    [Fact]public void StaleKingdomDataAllianceIdFailsMembershipSafetyCheck()
    {
        countries[4].data=new KingdomDataWithAllianceId{id=5,name="Страна 5",allianceID=999};
        Assert.False(AllianceSafety.TryMembership(countries[4],out _));
        Assert.False(CivicAllianceRuntime.RequestAdmission(world,countries[0],countries[4],51));
    }

    public sealed class DictOnlyAllianceManager
    {
        public List<Alliance> list=new List<Alliance>();
        public Dictionary<long,Alliance> dict=new Dictionary<long,Alliance>();
        public int Calls;
        public bool forceAlliance(Kingdom a,Kingdom b)
        {
            Calls++;
            var existing=dict.Values.FirstOrDefault(x=>x.kingdoms_list.Contains(a)||x.kingdoms_list.Contains(b))
                ?? list.FirstOrDefault(x=>x.kingdoms_list.Contains(a)||x.kingdoms_list.Contains(b));
            bool isNew=existing==null;
            if(existing==null){existing=new Alliance{id=101};dict[101]=existing;}
            if(!existing.kingdoms_list.Contains(a))existing.kingdoms_list.Add(a);
            if(!existing.kingdoms_list.Contains(b))existing.kingdoms_list.Add(b);
            return isNew;
        }
    }

    [Fact]public void AllianceInDictBeforeListFlushAndJoinReturningFalseBothExecuteCleanly()
    {
        var dictManager=new DictOnlyAllianceManager();
        MapBox.instance.alliances=dictManager;
        // 1) New alliance created only in dict (before checkLists flushes to list)
        Assert.True(CivicAllianceRuntime.RequestAdmission(world,countries[0],countries[1],51));
        Tick(51);
        Assert.True(AllianceSafety.Same(countries[0],countries[1]));
        Assert.Equal(CivicMotionStage.Executed,world.Governance.AllianceMotions.Values.Single().Stage);

        // 2) Joining existing alliance returns tNew == false in WorldBox 0.51.2
        Assert.True(CivicAllianceRuntime.RequestAdmission(world,countries[0],countries[4],52));
        Tick(52);
        Assert.True(AllianceSafety.Same(countries[0],countries[4]));
        Assert.All(world.Governance.AllianceMotions.Values,m=>Assert.Equal(CivicMotionStage.Executed,m.Stage));
    }

    [Fact]public void RemovedKingdomPreservesHistoricalNameAndCleansTreaties()
    {
        world.YearTick(new List<KingdomInput>
        {
            new KingdomInput{KingdomId=1,Name="Вейландия",Population=100,Cities=new List<CityInput>{new CityInput{CityId=10,Population=100}}},
            new KingdomInput{KingdomId=2,Name="Кукка",Population=80,Cities=new List<CityInput>{new CityInput{CityId=20,Population=80}}}
        });
        world.TreatiesBook.Add(new Treaty{Type=TreatyType.TradeAgreement,PartyA=1,PartyB=2,SignedYear=50,ExpiresYear=60});
        countries.RemoveAll(k=>k.id==2);
        world.RemoveKingdom(2);
        // 0.31.17: на карте и в окнах — коренное имя вместе с ярлыком строя, коренное имя сохраняется.
        Assert.Contains("Кукка", world.KingdomNameOf(2));
        Assert.Equal("Кукка", RealPolitic.Core.StateNaming.Root(world.KingdomNameOf(2)));
        Assert.Equal(world.KingdomNameOf(2), GameNotifier.KingdomName(MapBox.instance,2));
        Assert.Empty(world.TreatiesBook.Of(2));
    }

    private sealed class CivKingdom : Kingdom { public bool isCiv() => true; }

    [Fact]public void AnnualTickSelfHealsPrematurelyDestroyedLiveCountryWithoutThrowing()
    {
        world.Governance.Recognition.DestroyCountry(5);
        Assert.False(world.Governance.Recognition.IsAlive(5));
        var civ=new CivKingdom{id=5,name="Бельлория",cities=new List<City>{cities[4]},units=cities[4].units};
        var inputs=new List<KingdomInput>
        {
            new KingdomInput{KingdomId=5,Name="Бельлория",Population=77,Warriors=27}
        };
        GovernanceRuntime.AnnualTick(world,52,inputs,id=>id==5?civ:null);
        Assert.True(world.Governance.Recognition.IsAlive(5));
    }

    [Fact]public void VassalageRemembersBordersWhileMergedIntoSuzerainAndStartsIndependenceWarOnExit()
    {
        var pw = new PoliticalWorld();
        pw.SetYear(60);
        var suzerain = pw.AddKingdom(1, IdeologyType.Monarchy, 1);
        var vassal = pw.AddKingdom(2, IdeologyType.Aristocracy, 1);
        pw.RememberKingdomName(1, "Вейландия");
        pw.RememberKingdomName(2, "Тихуня");
        pw.AddCity(10, 1);
        pw.AddCity(20, 2);
        pw.AddCity(21, 2);

        suzerain.Legitimacy = 80f;
        Union? union = pw.TryCreateUnion(UnionType.Vassal, 1, 2, "Вейландия–Тихуня",
            new DiplomatSnapshot { KingdomId = 2, Ideology = IdeologyType.Aristocracy, Legitimacy = 35f, Population = 60, Warriors = 15, AdjacentToLeader = true },
            out var check, leaderWarriors: 60);
        Assert.True(check.Ok, check.ReasonDetail);
        Assert.NotNull(union);

        UnionMember? member = union!.GetMember(2);
        Assert.NotNull(member);
        Assert.Equal("Тихуня", member!.OriginalName);
        Assert.Equal(IdeologyType.Aristocracy, member.OriginalIdeology);
        Assert.Equal(new[] { 20, 21 }, member.CityIds);

        // Вливаем вассала в сюзерена: города 20 и 21 переходят державе 1, старое Kingdom 2 снимается без ложного «КОНЕЦ ДЕРЖАВЫ».
        pw.GetCity(20)!.KingdomId = 1;
        pw.GetCity(21)!.KingdomId = 1;
        pw.RecordMergedIntoSuzerain(2);
        pw.RemoveKingdom(2);
        Assert.DoesNotContain(pw.Bus.OfType(PolEventType.KingdomDisappeared), e => e.KingdomId == 2);

        // На годовом тике уния продолжает жить, пока сюзерен владеет городами вассала.
        pw.YearTick(new List<KingdomInput>
        {
            new KingdomInput
            {
                KingdomId = 1,
                Name = "Вейландия",
                Population = 180,
                Cities = new List<CityInput>
                {
                    new CityInput { CityId = 10, KingdomId = 1, Population = 100 },
                    new CityInput { CityId = 20, KingdomId = 1, Population = 40 },
                    new CityInput { CityId = 21, KingdomId = 1, Population = 40 }
                }
            }
        });
        Assert.Single(pw.Unions);
        Assert.True(union.Contains(2));

        // Когда вассал решает выйти за независимость — формируется приказ на отделение его запомненных границ с войной на выживание.
        Assert.True(pw.LeaveUnionForIndependence(2, out _));
        RestorationOrder order = Assert.Single(pw.TakePendingRestorations());
        Assert.Equal(2, order.DefeatedId);
        Assert.Equal(1, order.VictorId);
        Assert.Equal("Тихуня", order.DefeatedName);
        Assert.True(order.IsNationalLiberation);
        Assert.False(order.LiberationPeaceful);
        Assert.Contains(20, order.PrewarCityIds);
        Assert.Contains(21, order.PrewarCityIds);
        Assert.Contains(pw.Bus.OfType(PolEventType.VassalRebellion), e => e.Get("kingdom") == "Тихуня");
    }

    [Fact]public void CivilWarWithGovernanceEnabledSpawnsRebelKingdomAndHandlesAllThreeOutcomes()
    {
        var pw = new PoliticalWorld { Rng = new System.Random(42) };
        pw.BeginNewWorldGovernance("civil-war-test");
        pw.SetYear(110);
        var parent = pw.AddKingdom(1, IdeologyType.Tribalism, 1);
        parent.Legitimacy = 14f;
        parent.AvgDissent = 0.65f;
        parent.Ruler = new RulerRef { ActorId = 100, Name = "Вождь Старый", KingdomId = 1, SinceYear = 80, Age = 55 };
        pw.RememberKingdomName(1, "Вейландия");
        pw.AddCity(10, 1);
        pw.AddCity(11, 1);
        pw.RebelLeaderResolver = (kId, cId) => ("Фонар", false, 777);

        var input = new KingdomInput
        {
            KingdomId = 1,
            Name = "Вейландия",
            Population = 160,
            Ruler = parent.Ruler,
            Cities = new List<CityInput>
            {
                new CityInput { CityId = 10, KingdomId = 1, Population = 90, Loyalty = 80 },
                new CityInput { CityId = 11, KingdomId = 1, Population = 70, Loyalty = 15 }
            }
        };

        var cwCtx = new CivilWarContext
        {
            RebelLeaderName = (k, c) => "Фонар",
            RebelLeaderIsFemale = (k, c) => false,
            RebelLeaderActorId = (k, c) => 777
        };
        Assert.True(CivilWars.TryStart(parent, input, pw.Params, pw.Bus, 110, new FixedRandom0(), cwCtx));
        Assert.NotNull(parent.CivilWar);
        // Мятеж против тирании идёт под оппозиционным знаменем, а не под тем же самым строем.
        Assert.NotEqual(parent.Ideology, parent.CivilWar!.RebelIdeology);

        // Годовой тик при включённом Governance.Enabled создаёт настоящее PendingSecession, а не сбрасывает CivilWar в null.
        pw.YearTick(new List<KingdomInput> { input });
        Assert.NotNull(parent.CivilWar);
        PendingSecession sec = Assert.Single(pw.TakePendingSecessions());
        Assert.Equal(1, sec.ParentKingdomId);
        Assert.Equal(new[] { 11 }, sec.CityIds);
        Assert.True(sec.DeclareWar);

        // Победа бунтовщиков (RebelTakeover): прежние границы и название страны возвращаются, идеология применяется на всю страну, лидером становится вождь бунтовщиков.
        int rebelId = parent.CivilWar!.RebelKingdomId;
        pw.GetCity(10)!.KingdomId = rebelId;
        pw.GetCity(11)!.KingdomId = rebelId;
        pw.YearTick(new List<KingdomInput>
        {
            new KingdomInput { KingdomId = 1, Name = "Вейландия", Population = 0, Ruler = parent.Ruler, Cities = new List<CityInput>(), Relations = new List<RelationInfo>{ new RelationInfo{ OtherId = rebelId, Relation = RelationType.War, Adjacent = true } } },
            new KingdomInput { KingdomId = rebelId, Name = "Вилбург", Population = 160, Cities = new List<CityInput>{ new CityInput{ CityId = 10, KingdomId = rebelId, Population = 90 }, new CityInput{ CityId = 11, KingdomId = rebelId, Population = 70 } }, Relations = new List<RelationInfo>{ new RelationInfo{ OtherId = 1, Relation = RelationType.War, Adjacent = true } } }
        });

        KingdomPolitics? winner = pw.GetKingdom(rebelId);
        Assert.NotNull(winner);
        // 0.31.17: имя отделившегося государства — коренное имя с ярлыком строя.
        Assert.Contains("Вейландия", pw.KingdomNameOf(rebelId));
        Assert.Equal("Вейландия", RealPolitic.Core.StateNaming.Root(pw.KingdomNameOf(rebelId)));
        Assert.Equal(sec.Ideology, winner!.Ideology);
        Assert.NotNull(winner.Ruler);
        Assert.Equal(777, winner.Ruler!.ActorId);
        Assert.Equal("Фонар", winner.Ruler.Name);
        Assert.All(pw.Cities, c => Assert.Equal(sec.Ideology, c.Dominant));
        PendingWorldOp transferOp = Assert.Single(pw.TakePendingWorldOps(), op => op.Type == WorldOpType.TransferCities);
        // Имя для восстановления исторических земель хранится в том же виде, что и на карте.
        Assert.Contains("Вейландия", transferOp.OriginalKingdomName);
        Assert.Equal(777, transferOp.LeaderActorId);
    }

    [Fact]public void VoluntaryFederationMergerSurvivesYearTicksAndRemembersHomeland()
    {
        var pw = new PoliticalWorld();
        pw.SetYear(240);
        var leader = pw.AddKingdom(1, IdeologyType.MerchantRepublic, 1);
        var partner = pw.AddKingdom(2, IdeologyType.MerchantRepublic, 192);
        leader.Legitimacy = 85f;
        partner.Legitimacy = 80f;
        pw.RememberKingdomName(1, "Ярославия");
        pw.RememberKingdomName(2, "Вольноград");
        pw.AddCity(10, 1);
        pw.AddCity(20, 2);

        Union? union = pw.TryCreateUnion(UnionType.Merger, 1, 2, "Вольно-Ярославская Федерация",
            new DiplomatSnapshot { KingdomId = 2, Ideology = IdeologyType.MerchantRepublic, Legitimacy = 80f, Population = 70, Warriors = 20, AdjacentToLeader = true },
            out var check, leaderWarriors: 40);
        Assert.True(check.Ok, check.ReasonDetail);
        Assert.NotNull(union);
        Assert.Single(pw.Bus.OfType(PolEventType.UnionMerged));

        PendingMerger merger = Assert.Single(pw.TakePendingMergers());
        Assert.Equal(1, merger.LeaderId);
        Assert.Equal(2, merger.MemberId);
        pw.GetCity(20)!.KingdomId = 1;
        pw.RemoveKingdom(2);
        Assert.DoesNotContain(pw.Bus.OfType(PolEventType.KingdomDisappeared), e => e.KingdomId == 2);
        Assert.NotNull(pw.CityHomelands.Get(20));
        Assert.Equal("Вольноград", pw.CityHomelands.Get(20)!.OriginalKingdomName);

        // На следующих годовых тиках добровольная федерация НЕ распадается сама по себе.
        pw.ClearEvents();
        pw.SetYear(241);
        pw.YearTick(new List<KingdomInput>
        {
            new KingdomInput
            {
                KingdomId = 1,
                Name = "Ярославия",
                Population = 180,
                Cities = new List<CityInput>
                {
                    new CityInput { CityId = 10, KingdomId = 1, Population = 110 },
                    new CityInput { CityId = 20, KingdomId = 1, Population = 70 }
                }
            }
        });
        Assert.Single(pw.Unions);
        Assert.Empty(pw.Bus.OfType(PolEventType.UnionDissolved));
        Assert.Empty(pw.Bus.OfType(PolEventType.UnionMemberLeft));
    }

    private sealed class FixedRandom0 : System.Random
    {
        public override double NextDouble() => 0.0;
        public override int Next(int minValue, int maxValue) => minValue;
    }
}
