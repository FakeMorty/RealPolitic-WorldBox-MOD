using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using NeoModLoader.services;

namespace RealPolitic;

/// <summary>Read-only, bounded diagnostics. A stock change is never reported as proof of consumption.</summary>
internal sealed class EmergencyFoodObservation
{
    private sealed class Reading
    {
        public City City;
        public long Id;
        public int? Bread,Food,Hungry,Starving;
    }
    private readonly List<Reading> _before=new List<Reading>();
    private long _kingdom;
    private bool _available;
    internal static EmergencyFoodObservation Capture(Kingdom kingdom)
    {
        var observation=new EmergencyFoodObservation();
        try
        {
            observation._kingdom=((NanoObject)kingdom).id;
            var cities=GameRefs.KingdomCities(kingdom);if(cities==null)return observation;
            var seen=new HashSet<City>();
            foreach(var city in cities)
                if(city!=null && seen.Add(city))observation._before.Add(Read(city));
            observation._available=true;
        }
        catch{} // Observation must never prevent the unchanged resource request.
        return observation;
    }
    private static int? Count(object value)=>value is int n && n>=0?(int?)n:null;
    private static Reading Read(City city)
    {
        var result=new Reading{City=city,Id=((NanoObject)city).id};
        result.Bread=Count(GameRefs.Invoke(city,"getResourcesAmount","bread"));
        result.Food=Count(GameRefs.Invoke(city,"getTotalFood"));
        var status=GameRefs.ReadMember(city,"status");
        result.Hungry=Count(GameRefs.ReadMember(status,"hungry"));
        result.Starving=Count(GameRefs.Invoke(city,"countStarving"));
        return result;
    }
    private static string Value(int? value)=>value.HasValue?value.Value.ToString(CultureInfo.InvariantCulture):"unknown";
    internal string DescribeAfter(int year,bool apiCalled)
    {
        if(!_available)return "year="+year+" kingdom="+_kingdom+" observation=unavailable api_called="+apiCalled+" consumption=not-measured";
        var text=new StringBuilder();
        text.Append("year=").Append(year).Append(" kingdom=").Append(_kingdom).Append(" api_called=").Append(apiCalled)
            .Append(" cities=").Append(_before.Count).Append(" sample_limit=8 consumption=not-measured");
        int sampled=0,paired=0;long delta=0;
        // Complete paired measurements determine the stock delta; omitted cities still contribute.
        foreach(var before in _before)
        {
            var after=Read(before.City);
            if(before.Bread.HasValue && after.Bread.HasValue){paired++;delta+=(long)after.Bread.Value-before.Bread.Value;}
            if(sampled++>=8)continue;
            text.Append(" | city=").Append(before.Id).Append(" bread=").Append(Value(before.Bread)).Append("->").Append(Value(after.Bread))
                .Append(" food=").Append(Value(before.Food)).Append("->").Append(Value(after.Food))
                .Append(" hungry_status=").Append(Value(before.Hungry)).Append("->").Append(Value(after.Hungry));
            if(before.Starving.HasValue || after.Starving.HasValue)
                text.Append(" starving_units=").Append(Value(before.Starving)).Append("->").Append(Value(after.Starving));
        }
        text.Append(" | bread_delta_observed=").Append(paired>0?delta.ToString(CultureInfo.InvariantCulture):"unknown").Append(" paired_cities=").Append(paired).Append('/').Append(_before.Count)
            .Append(" omitted_details=").Append(Math.Max(0,_before.Count-8));
        return text.ToString();
    }
    internal void LogAfter(int year,bool apiCalled)
    {
        try{LogService.LogInfo("[Real Politic] EmergencyFood observation: "+DescribeAfter(year,apiCalled));}
        catch{} // Missing/throwing readers or logger cannot change execution or retry a mutation.
    }
}
