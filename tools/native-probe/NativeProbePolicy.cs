namespace RealPolitic.NativeDiagnostics;

/// <summary>Exact observed contracts; ordinary per-frame housekeeping must never consume the lifecycle trace.</summary>
public static class NativeProbePolicy
{
    public static bool Lifecycle(string type)=>type=="MapBox" || type=="SaveManager";
    public static string SlotLabel(string path)
    {
        if(string.IsNullOrWhiteSpace(path))return "";
        string trimmed=path.Trim().Replace('\\','/').TrimEnd('/');
        int slash=trimmed.LastIndexOf('/');
        return slash>=0?trimmed.Substring(slash+1):trimmed;
    }
    public static bool Observe(string type,string method)
    {
        switch(type)
        {
            case "MapBox":return method=="generateNewMap";
            case "SaveManager":return method=="loadWorld" || method=="loadData" || method=="loadMapFromBytes" || method=="loadMapFromResources" || method=="startLoadSlot" || method=="prepareLoading" || method=="loadAlliances" || method=="saveWorldToDirectory" || method=="clickSaveSlot";
            case "Alliance":return method=="join" || method=="leave" || method=="addFounders" || method=="createNewAlliance" || method=="dissolve" || method=="Dispose" || method=="loadData";
            case "AllianceManager":return method=="forceAlliance" || method=="newAlliance" || method=="dissolveAlliance" || method=="clear";
            case "Kingdom":return method=="Dispose" || method=="allianceJoin" || method=="allianceLeave";
            default:return false;
        }
    }
}
