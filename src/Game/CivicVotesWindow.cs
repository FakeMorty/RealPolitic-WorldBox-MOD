using System;
using System.Reflection;
using RealPolitic.Core;

namespace RealPolitic;

/// <summary>Read the actual procedures and propose alliance admission/expulsion as the selected member.</summary>
public static class CivicVotesWindow
{
    private const string Id="realpolitic_civic_votes";
    private static object _window,_content;
    private static int _country;
    public static bool Prewarm()
    {
        if(_window!=null)return true;
        _window=ModWindow.Create(Id,"RP_GOV26_VOTES_WINDOW",GameText.Localized("RP_GOV26_VOTES_WINDOW","Голосования"),ModIcons.VotesKey,out _content);
        if(_content!=null)ModUi.SetupAutoLayout(_content,spacing:2f,padTop:6f);
        return _window!=null;
    }
    public static void Attach(object kingdomWindow,int countryId)
    {
        if(RealPoliticMod.World?.Governance.Enabled!=true)return;
        _country=countryId;
        SideTabButton.Ensure(kingdomWindow,"rp_votes_tab",ModIcons.VotesKey,ModIcons.PathFor(ModIcons.VotesKey),
            GameText.Localized("RP_GOV26_VOTES_WINDOW","Голосования"),typeof(CivicVotesWindow).GetMethod(nameof(Open),BindingFlags.Static|BindingFlags.Public));
    }
    /// <summary>0.45: запомнить державу без создания отдельной вкладки (вкладка мода теперь одна).</summary>
    public static void AttachCountry(int countryId){_country=countryId;}

    public static void Open()
    {
        var world=RealPoliticMod.World;if(world?.Governance.Enabled!=true || NativeWorldLifecycle.Transitioning)return;
        if(_window==null)Prewarm();
        if(_window==null || _content==null)return;Render();
        ModWindow.Show(Id,_window);
    }
    private static void Line(string text,bool title=false)=>ModUi.Text(_content,text,title?15:11,0.92f,0.89f,0.78f,title);
    private static void Render()
    {
        var world=RealPoliticMod.World;ModUi.ClearChildren(_content);var country=CivilWarExecutor.FindKingdom(World.world,_country);
        if(world?.Governance.Enabled!=true || country==null)return;
        int proposer=_country;string worldKey=world.Governance.WorldKey;long revision=NativeWorldLifecycle.Revision;
        Line(GameRefs.ReadProperty(country,"name")?.ToString()??"?",true);
        Line(GameText.Localized(world.Governance.Recognition.IsRecognized(proposer)?"RP_GOV26_RECOGNIZED":"RP_GOV26_UNRECOGNIZED",world.Governance.Recognition.IsRecognized(proposer)?"Государство признано":"Государство не признано"));
        if(world.Governance.Offices.TryGetValue(proposer,out var office))
            Line(GameText.Localized("RP_GOV26_NEXT_ELECTION","Следующие выборы главы: {year}").Replace("{year}",office.NextElectionYear.ToString()));
        foreach(var p in world.Governance.Procedures.Values)
            if(p.CountryId==proposer && (p.Stage==CivicProcedureStage.Voting || p.Stage==CivicProcedureStage.Approved || p.Stage==CivicProcedureStage.ExecutionPending))
                Line(GameText.Localized("RP_GOV26_PROCEDURE_"+(int)p.Kind,p.Kind.ToString())+" · "+(p.TargetCountryId>0?world.KingdomNameOf(p.TargetCountryId):p.CityId>0?GameNotifier.CityName(World.world,p.CityId):"")+" · "+p.Ballot.Votes.Count+"/"+p.Ballot.EligibleCount+" · "+GameText.Localized("RP_GOV26_DEADLINE","завершение: ")+p.Ballot.DeadlineYear);
        Line(GameText.Localized("RP_GOV26_ALLIANCE_PROPOSALS","Предложения по альянсу"),true);
        var list=GameRefs.KingdomList(World.world);if(list==null)return;
        if(!AllianceSafety.TryMembership(country,out var own))return;
        foreach(var candidate in list)
        {
            if(candidate==null || ReferenceEquals(candidate,country) || !AllianceSafety.TryMembership(candidate,out var other))continue;
            int subject=(int)((NanoObject)candidate).id;string name=GameRefs.ReadProperty(candidate,"name")?.ToString()??"?";
            if(own!=null && ReferenceEquals(own,other))
            {
                ModUi.Button(_content,GameText.Localized("RP_GOV26_PROPOSE_EXPEL","Предложить исключить: ")+name,ModUi.PageWidth,30,11,0.70f,0.36f,0.30f,ModIcons.ExpelKey,()=>
                {if(NativeWorldLifecycle.Transitioning || revision!=NativeWorldLifecycle.Revision || worldKey!=world.Governance.WorldKey)return;CivicAllianceRuntime.RequestExpulsion(world,proposer,subject,RealPoliticMod.CurrentYear());CivicAllianceMotions.Tick(world,RealPoliticMod.CurrentYear(),new CivicAllianceRuntime(world));Render();});
            }
            else if(other==null && world.Governance.Recognition.CanContact(proposer,subject,InternationalContact.Alliance))
            {
                ModUi.Button(_content,GameText.Localized("RP_GOV26_PROPOSE_ADMIT","Предложить принять: ")+name,ModUi.PageWidth,30,11,0.34f,0.58f,0.38f,ModIcons.AdmitKey,()=>
                {if(NativeWorldLifecycle.Transitioning || revision!=NativeWorldLifecycle.Revision || worldKey!=world.Governance.WorldKey)return;CivicAllianceRuntime.RequestAdmission(world,country,candidate,RealPoliticMod.CurrentYear());CivicAllianceMotions.Tick(world,RealPoliticMod.CurrentYear(),new CivicAllianceRuntime(world));Render();});
            }
        }
        foreach(var motion in world.Governance.AllianceMotions.Values)
            if(motion.ProposerId==proposer || motion.SubjectId==proposer || motion.Members.Contains(proposer))
                Line(GameText.Localized(motion.Action==CivicAllianceAction.Admission?"RP_GOV26_ADMISSION":"RP_GOV26_EXPULSION",motion.Action==CivicAllianceAction.Admission?"Приём":"Исключение")+": "+world.KingdomNameOf(motion.SubjectId)+" · "+GameText.Localized("RP_GOV26_MOTION_STAGE_"+(int)motion.Stage,motion.Stage.ToString())+" · "+motion.Ballot.Tally().Counts[1]+"/"+motion.Ballot.EligibleCount);
    }
}
