using System;
using System.Collections.Generic;
using RealPolitic.Core;

namespace RealPolitic;

public static class ElectoralChapterL10n
{
    private static string L(string key,string fallback)=>GameText.Localized(key,fallback);
    private static string F(string key,string fallback,params object[] args)=>string.Format(L(key,fallback),args);
    public static List<string> Lines(ElectoralChapter law)
    {
        if(law==null)throw new ArgumentNullException(nameof(law));law.Validate();
        string limit=law.MaximumTerms==0 ? L("RP26_LIMIT_NONE","без ограничения")
            : F("RP26_LIMIT","{0}; учёт: {1}",law.MaximumTerms,law.TermLimitBasis==OfficeTermLimitBasis.Lifetime ? L("RP26_LIFETIME","за всю жизнь") : L("RP26_CONSECUTIVE","подряд"));
        string threshold=law.ReferendumThreshold==BallotThreshold.MoreThanHalf ? L("RP26_SIMPLE","больше половины действительных голосов") : L("RP26_TWO_THIRDS","не менее двух третей действительных голосов");
        string exit=law.RegionalExit==RegionalExitLaw.Forbidden ? L("RP26_EXIT_FORBIDDEN","законный выход запрещён")
            : law.RegionalExit==RegionalExitLaw.WithCentralPermission ? L("RP26_EXIT_PERMISSION","референдум с предварительного разрешения центра")
            : L("RP26_EXIT_RIGHT","регион вправе провести референдум без разрешения центра");
        return new List<string>{
            F("RP26_ELECTORS","Право голоса: {0}; минимальный возраст — {1}.",ConstitutionL10n.FranchiseName(law.Electors),law.VotingAge),
            F("RP26_CANDIDATES","Право быть избранным: {0}; минимальный возраст — {1}.",ConstitutionL10n.FranchiseName(law.Candidates),law.CandidacyAge),
            F("RP26_TERM","Срок выборного главы — {0} лет. Лимит сроков: {1}.",law.TermYears,limit),
            F("RP26_QUORUM","Кворум референдума — {0}% имеющих право голоса; принятие — {1}. Воздержание учитывается в явке, но не в голосах за/против.",law.ReferendumQuorumPercent,threshold),
            F("RP26_DURATION","Продолжительность голосования жителей, лет: {0}.",law.BallotYears),
            F("RP26_FOREIGN","Голос государства в международных вопросах: {0}.",law.ForeignDecisions==ForeignDecisionAuthority.Ruler ? L("RP26_BY_RULER","личное решение главы") : L("RP26_BY_ELECTORATE","голосование установленного конституцией электората")),
            F("RP26_EXIT","Право региона на выход: {0}. Успешный законный референдум не запускает войну автоматически.",exit)
        };
    }
}
