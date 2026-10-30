using System.Collections.Generic;
using RealPolitic.Core;

namespace RealPolitic;

public sealed class CivicElectionRuntime:ICivicHeadSource
{
    private readonly PoliticalWorld _world;
    private readonly Dictionary<int,CivicResident> _profiles=new Dictionary<int,CivicResident>();
    public CivicElectionRuntime(PoliticalWorld world){_world=world;}
    public bool TryReadCitizens(int countryId,out IList<CivicResident> citizens)
    {
        citizens=new List<CivicResident>();_profiles.Clear();
        var country=CivilWarExecutor.FindKingdom(World.world,countryId);var law=_world.GetKingdom(countryId)?.Constitution?.Electoral;
        if(!CivicElectorateReader.TryReadCitizens(_world,country,law,out var people,out _))return false;
        foreach(var person in people)
        {
            var actor=person.Actor;
            if(actor==null || !ReferenceEquals(actor.kingdom,country)){citizens.Clear();return false;}
            var resident=new CivicResident{Person=person.Person,Belief=person.Belief,Name=GameText.ActorName(actor),IsFemale=GameText.ActorIsFemale(actor),ClanId=GameRefs.ActorClanId(actor),ClanName=GameRefs.ActorClanName(actor)??""};
            citizens.Add(resident);_profiles.Add(person.Person.ActorId,resident);
        }
        return true;
    }
    public bool TryInstallHead(int countryId,int actorId)
    {
        if(!ReferenceEquals(_world,RealPoliticMod.World) || !_world.Governance.Enabled)return false;
        var country=CivilWarExecutor.FindKingdom(World.world,countryId);var winner=GameRefs.FindActorById(actorId);
        if(country==null || winner==null || !ReferenceEquals(winner.kingdom,country)
            || !(GameRefs.Invoke(winner,"isAlive") is bool alive) || !alive)return false;
        var current=GameRefs.ReadRuler(country);
        if(current!=null && GameText.ActorLongId(current)==actorId){Refresh(actorId,winner);return true;}
        if(!GameRefs.TrySetKingOnKingdom(country,winner,quiet:true))return false;
        var confirmed=GameRefs.ReadRuler(country);
        if(confirmed==null || GameText.ActorLongId(confirmed)!=actorId)return false;
        if(current!=null)GameRefs.TryAppointToCity(country,current);
        Refresh(actorId,winner);return true;
    }
    private void Refresh(int id,Actor actor)
    {
        if(_profiles.TryGetValue(id,out var profile))
        {profile.ClanId=GameRefs.ActorClanId(actor);profile.ClanName=GameRefs.ActorClanName(actor)??"";profile.Name=GameText.ActorName(actor);profile.IsFemale=GameText.ActorIsFemale(actor);}
    }
}
