using System.Reflection;
using RealPolitic.Core;
using Xunit;

namespace RealPolitic.Tests;
public sealed class Governance26PresentationTests
{
    [Theory][InlineData("RP_GOV26_ADMISSION")][InlineData("RP_GOV26_MOTION_STAGE_5")][InlineData("RP_GOV26_CONSENT")][InlineData("RP26_ART_CANDIDATES")][InlineData("RP26_ART_TERM")][InlineData("RP26_ART_EXIT")]
    public void NewEventParametersHaveAReadableEmbeddedFallback(string key)
    {string text=CivicText.Fallback(key);Assert.NotEqual(key,text);Assert.DoesNotContain("RP_",text);}
    [Fact]public void ConstitutionAmendmentHistoryLocalizesRp26ArticleKeys()
    {
        MethodInfo loc=typeof(ConstitutionWindow).GetMethod("Loc",BindingFlags.Static|BindingFlags.NonPublic)!;
        string text=(string)loc.Invoke(null,new object[]{"RP26_ART_TERM"})!;
        Assert.Equal("О сроке выборного главы",text);
        Assert.DoesNotContain("RP26_",text);
    }
    [Fact]public void NationalAllianceNotificationHasActualCountsAndNoTechnicalKeys()
    {
        var bus=new PolEventBus();var notifier=new GameNotifier(bus,new NotificationCenter(bus));
        var e=new PolEvent(PolEventType.NationalAlliancePosition,"RP_GOV26_EV_ALLIANCE_POSITION")
            .With("kingdom","Метрополия").With("subject","Остров").With("question","RP_GOV26_CONSENT").With("result","RP_GOV26_NO")
            .With("yes",12).With("no",18).With("abstain",2).With("eligible",40);
        string text=notifier.RenderEventText(e);Assert.Contains("12",text);Assert.Contains("18",text);Assert.Contains("40",text);Assert.DoesNotContain("RP_",text);Assert.DoesNotContain("{",text);
    }
    [Fact]public void AllianceResultNamesTheCountriesAndExplainsNativeFailure()
    {
        var bus=new PolEventBus();var notifier=new GameNotifier(bus,new NotificationCenter(bus));
        var e=new PolEvent(PolEventType.CivicAllianceMotion,"RP_GOV26_EV_ALLIANCE_MOTION").With("action","RP_GOV26_EXPULSION")
            .With("subject","Остров").With("proposer","Метрополия").With("status","RP_GOV26_MOTION_STAGE_5").With("yes",2).With("eligible",3);
        string text=notifier.RenderEventText(e);Assert.Contains("Остров",text);Assert.Contains("Метрополия",text);Assert.Contains("игра не подтвердила",text);Assert.DoesNotContain("RP_",text);Assert.DoesNotContain("{",text);
    }
}
