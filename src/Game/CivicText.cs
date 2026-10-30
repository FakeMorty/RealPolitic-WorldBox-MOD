using System;
using System.IO;
using RealPolitic.Core;

namespace RealPolitic;

/// <summary>Readable fallback for event templates and parameter keys even before or without the native locale manager.</summary>
internal static class CivicText
{
    private static readonly Lazy<TinyJson.Node> Russian=new Lazy<TinyJson.Node>(()=>
    {
        using(var stream=typeof(CivicText).Assembly.GetManifestResourceStream("RealPolitic.fallback.ru.json"))
        {if(stream==null)return null;using(var reader=new StreamReader(stream))return TinyJson.ParseStrict(reader.ReadToEnd());}
    });

    public static bool IsLocalizationKey(string key)
    {
        if(string.IsNullOrEmpty(key) || !key.StartsWith("RP",StringComparison.Ordinal))return false;
        if(key.StartsWith("RP_GOV26_",StringComparison.Ordinal))return true;
        return key.Length>5 && char.IsDigit(key[2]) && char.IsDigit(key[3]) && key[4]=='_';
    }

    public static string Fallback(string key)
    {
        if(!IsLocalizationKey(key))return key;
        try{return Russian.Value?.Get(key)?.Str??key;}catch{return key;}
    }
}
