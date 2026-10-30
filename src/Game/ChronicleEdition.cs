using System;
using RealPolitic.Core;

namespace RealPolitic;

/// <summary>Readable context drawn only from recorded event fields. No political side effects.</summary>
public static class ChronicleEdition
{
    public static Actor LivingRuler(Kingdom kingdom)
    {
        Actor actor = GameRefs.ReadRuler(kingdom);
        if (actor != null && GameRefs.Invoke(actor, "isAlive") is bool alive && !alive) return null;
        return actor;
    }
    private static string T(string key, string fallback, params object[] args) => ChronicleNotebook.T(key, fallback, args);
    public static string Explain(PolEvent evt, string line, PoliticalWorld world)
    {
        if (string.IsNullOrEmpty(line)) return line;
        string extra = string.Empty;
        switch (evt.Type)
        {
            case PolEventType.DemocraticElection:
                string kind = evt.Get("election_kind");
                if (kind == "free_circle") extra = CoreText.L("RP_CHRON25_FREE_CIRCLE", "Избран на вольном круге без ограничения числа сроков (правит до кончины или смещения жителями).");
                else if (kind == "renewed") extra = CoreText.L("RP_CHRON25_RENEWED", "Переизбрание: глава остаётся тем же, смены персонажа нет.");
                else if (kind == "return") extra = CoreText.L("RP_CHRON25_RETURN", "Возвращение экс-главы: прежние сроки учитываются в пожизненном лимите.");
                else if (kind == "new") extra = CoreText.L("RP_CHRON25_NEW", "Избран новый кандидат на пост главы государства.");
                else if (kind == "observed")
                {
                    line = T("RP_CHRON25_OBSERVED_BASE", "Государство {0}: зарегистрирована смена главы — {1}.", evt.Get("kingdom"), evt.Get("ruler"));
                    extra = CoreText.L("RP_CHRON25_OBSERVED", "Оформлена фактически наблюдаемая смена главы (не доказательство планового голосования).");
                }
                if (evt.ActorId.HasValue) extra += " " + ChronicleNotebook.Person(evt.Get("ruler"), evt.ActorId.Value) + ".";
                if (evt.GetId("next_election") > 0 && kind != "individual-ballots" && kind != "free_circle")
                    extra += " " + T("RP_CHRON25_NEXT_VOTE", "Следующие выборы — год {0} (лимит {1} срока).", evt.GetId("next_election"), ElectionSystem.MaxLifetimeTerms);
                break;
            case PolEventType.Succession:
                int previousId = evt.GetId("old_actor");
                DeathRecord death = previousId > 0 ? world?.Deaths.Get(previousId) : null;
                if (death != null && evt.Year >= death.Year && evt.Year - death.Year <= 2)
                {
                    string causeText = CoreText.L(death.LocKeyFor(death.WasFemale), death.Cause.ToString());
                    if (!string.IsNullOrEmpty(death.KillerName) || !string.IsNullOrEmpty(death.KillerSpecies))
                    {
                        string prefix = death.KillerIsBoat ? "судно: " : death.KillerIsCiv ? "убийца: " : "зверь: ";
                        causeText += " (" + prefix + Chronicle.KillerShort(death) + ")";
                    }
                    extra = T("RP_CHRON25_AFTER_DEATH", "Предшественник {0} скончался в {1} году ({2}).", ChronicleNotebook.Person(death.ActorName, death.ActorId), death.Year, causeText);
                }
                else if (previousId > 0)
                    extra = CoreText.L("RP_CHRON25_CHANGE_UNKNOWN", "Зафиксирована смена главы; причина ухода предыдущего правителя не установлена.");
                if (evt.ActorId.HasValue) extra += " " + ChronicleNotebook.Person(evt.Get("ruler"), evt.ActorId.Value) + ".";
                break;
            case PolEventType.KingdomDisappeared:
                if (!string.IsNullOrWhiteSpace(evt.Get("annexed_by")))
                    extra = T("RP_CHRON25_END_ANNEXED", "Держава прекратила существование после полной аннексии державой {0}.", evt.Get("annexed_by"));
                else if (evt.Death == null)
                    extra = CoreText.L("RP_CHRON25_END_UNKNOWN", "Держава исчезла с политической карты (подтверждённой причины в летописи не сохранилось).");
                break;
            case PolEventType.EconomicCrisisStarted:
                int crisis = evt.GetId("crisis_kind");
                if (crisis == (int)EconomicCrisisType.Bankruptcy)
                    extra = T("RP_CHRON25_BANKRUPTCY", "Дефицит казны длится {0} лет (остаток золота: {1}).", evt.Get("deficit_before"), evt.Get("gold_before"));
                else if (crisis == (int)EconomicCrisisType.TradeCollapse)
                    extra = CoreText.L("RP_CHRON25_TRADE_COLLAPSE", "Все прежние внешние торговые маршруты прерваны.");
                else if (crisis > 0)
                    extra = CoreText.L("RP_CHRON25_RECESSION", "В экономике державы начался хозяйственный спад.");
                break;
            case PolEventType.Impeached:
                if (string.Equals(evt.LocKey, "RP_EV_ATAMAN_DISMISSED", StringComparison.Ordinal))
                    extra = CoreText.L("RP_CHRON25_ATAMAN_DISMISSED", "Атаман смещён решением жителей на вольном круге; созваны выборы нового атамана.");
                else
                    extra = CoreText.L("RP_CHRON25_IMPEACH", "Глава отстранён от должности решением собрания.");
                break;
            case PolEventType.TradeRouteOpened:
                if (!string.IsNullOrWhiteSpace(evt.Get("partner"))) extra = T("RP_CHRON25_TRADE_PARTNER", "Партнёр по маршруту: {0}.", evt.Get("partner"));
                break;
            case PolEventType.CivilWarEnded:
                extra = CoreText.L("RP_CHRON25_CW_END", "Вооружённое противостояние внутри страны завершилось.");
                break;
        }
        // All context is appended to the existing event; the original content is preserved.
        if (string.IsNullOrWhiteSpace(extra)) return line;
        string sentence = line.TrimEnd();
        if (!sentence.EndsWith(".") && !sentence.EndsWith("!") && !sentence.EndsWith("?")) sentence += ".";
        return sentence + " " + extra.Trim();
    }
}
