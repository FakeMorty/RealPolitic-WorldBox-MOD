using System;
using System.Collections.Generic;
using RealPolitic.Core;
using UnityEngine;

namespace RealPolitic;

public class GameNotifier
{
	private readonly PolEventBus _bus;

	private readonly NotificationCenter _center;

	private float _nextAllowed;

	public float Interval = 1.0f;

	/// <summary>Максимальное устаревание уведомления в игровых годах на высокой скорости (например, x20), после которого оно пропускается в HUD.</summary>
	public int MaxLagYears = 2;

	public GameNotifier(PolEventBus bus, NotificationCenter center)
	{
		_bus = bus;
		_center = center;
		_bus.Subscribe(History.Add);
		_bus.Subscribe(OnBusEvent);
	}

	private static void OnBusEvent(PolEvent evt)
	{
		// Срыв уникальной угрозы идеологии — реальные последствия в игре должны применяться
		// немедленно в год события, а не с задержкой очереди уведомлений или только при включённых всплывашках.
		if (evt != null && evt.Type == PolEventType.IdeologyThreatBroke)
		{
			ApplyThreatBreakEffects(evt);
		}
	}

	public void Flush()
	{
		float unscaledTime = Time.unscaledTime;
		if (unscaledTime < _nextAllowed)
		{
			// Критические события (смерть правителя) показываем без ожидания интервала,
			// чтобы сообщение не отставало от ванильного сообщения о смерти.
			if (!_center.PeekIsCritical())
			{
				return;
			}
		}
		int currentYear = RealPoliticMod.CurrentYear();
		float timeScale = GetEffectiveTimeScale();
		int maxBurst = (timeScale >= 5f && _center.PendingCount >= 4) ? 2 : 1;
		int shown = 0;
		while (shown < maxBurst)
		{
			PolEvent polEvent = DequeueFresh(currentYear);
			if (polEvent == null)
			{
				break;
			}
			Show(polEvent);
			shown++;
		}
		if (shown > 0)
		{
			_nextAllowed = unscaledTime + ComputeEffectiveInterval(timeScale, _center.PendingCount);
		}
	}

	private PolEvent DequeueFresh(int currentYear)
	{
		while (true)
		{
			PolEvent candidate = _center.Dequeue();
			if (candidate == null)
			{
				return null;
			}
			// Если на скорости x20 уведомление пролежало в очереди больше MaxLagYears лет,
			// не показываем его с опозданием в 10–20 лет (в летописи и истории державы оно уже есть).
			if (currentYear > 0 && candidate.Year > 0 && currentYear - candidate.Year > MaxLagYears && candidate.Severity < Severity.Critical)
			{
				continue;
			}
			return candidate;
		}
	}

	private float ComputeEffectiveInterval(float timeScale, int pendingCount)
	{
		float interval = Interval;
		if (timeScale > 1f)
		{
			interval /= Math.Min(6f, (float)Math.Sqrt(timeScale));
		}
		if (pendingCount >= 6)
		{
			interval *= 0.35f;
		}
		else if (pendingCount >= 3)
		{
			interval *= 0.55f;
		}
		return Math.Max(0.15f, interval);
	}

	private static readonly System.Reflection.PropertyInfo TimeScaleProp =
		typeof(Time).GetProperty("timeScale", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static);

	private static float GetEffectiveTimeScale()
	{
		try
		{
			if (TimeScaleProp?.GetValue(null, null) is float scale && scale > 1f)
			{
				return scale;
			}
		}
		catch (Exception)
		{
		}
		return 1f;
	}

	private void Show(PolEvent evt)
	{
		string text = BuildText(evt);
		if (!string.IsNullOrEmpty(text))
		{
			// После RenderEventText параметры уже возвращены в исходном виде,
			// поэтому RP_IDEOLOGY_* ещё можно честно превратить в иконку нового строя.
			string iconPath = NotificationIcons.PathFor(evt, KingdomIdeology(evt));
			switch (evt.Severity)
			{
			case Severity.Critical:
				GameMessages.Critical(text, iconPath);
				break;
			case Severity.Major:
				GameMessages.Major(text, iconPath);
				break;
			default:
				GameMessages.Minor(text, iconPath);
				break;
			}
		}
	}

	private static IdeologyType? KingdomIdeology(PolEvent evt)
	{
		try
		{
			if (evt == null || !evt.KingdomId.HasValue)
			{
				return null;
			}
			KingdomPolitics state = RealPoliticMod.World?.GetKingdom(evt.KingdomId.Value);
			return state?.Ideology;
		}
		catch (Exception)
		{
			return null;
		}
	}

	/// <summary>Собирает отображаемый текст события (с подстановкой имён и локализацией) — используется и уведомлениями, и летописью.</summary>
	public string RenderEventText(PolEvent evt)
	{
		if (evt == null)
		{
			return string.Empty;
		}
		Dictionary<string, string> originalParams = new Dictionary<string, string>(evt.Params);
		try
		{
			string template = GameText.Localized(evt.LocKey, DefaultTemplate(evt));
			ResolveParams(evt);
			return evt.Render(template);
		}
		catch (Exception)
		{
			return string.Empty;
		}
		finally
		{
			evt.Params.Clear();
			foreach (KeyValuePair<string, string> kv in originalParams)
			{
				evt.Params[kv.Key] = kv.Value;
			}
		}
	}

	/// <summary>Переносит срыв угрозы из ядра в игровые последствия (счастье, золото).</summary>
	private static void ApplyThreatBreakEffects(PolEvent evt)
	{
		try
		{
			if (!evt.KingdomId.HasValue)
			{
				return;
			}
			KingdomPolitics state = RealPoliticMod.World?.GetKingdom(evt.KingdomId.Value);
			MapBox map = World.world;
			if (state == null || map == null)
			{
				return;
			}
			List<Kingdom> kingdoms = map.kingdoms?.list;
			for (int i = 0; kingdoms != null && i < kingdoms.Count; i++)
			{
				if (kingdoms[i] != null && (int)((NanoObject)kingdoms[i]).id == evt.KingdomId.Value)
				{
					IdeologyMechanicsEffects.ApplyThreatBreak(kingdoms[i], state.Ideology);
					break;
				}
			}
		}
		catch (Exception)
		{
		}
	}

	private string BuildText(PolEvent evt)
	{
		return RenderEventText(evt);
	}

	private void ResolveParams(PolEvent evt)
	{
        LocalizeKeyParams(evt);
		MapBox world = World.world;
		if (world == null)
		{
			return;
		}
		if (!evt.Params.ContainsKey("kingdom") && evt.KingdomId.HasValue)
		{
			evt.Params["kingdom"] = KingdomName(world, evt.KingdomId.Value);
		}
		if (!evt.Params.ContainsKey("city") && evt.CityId.HasValue)
		{
			evt.Params["city"] = CityName(world, evt.CityId.Value);
		}
		// Для культуры, религии и языка событие несёт идентификатор и имя.
		// Если имя по какой-то причине отсутствует, пробуем восстановить его по идентификатору.
		ResolveNoveltyName(evt, world, "religion");
		ResolveNoveltyName(evt, world, "language");
		ResolveNoveltyName(evt, world, "culture");
		// Параметры-ключи локализации (идеология, курс, тип унии) локализуем перед подстановкой.
		LocalizeKeyParams(evt);
		if (evt.Death != null)
		{
			evt.Params["ruler"] = evt.Death.ActorName;
			// Причина смерти — в форме пола умершего: «Королева … казнена», а не «казнён».
			evt.Params["cause"] = GameText.Localized(evt.Death.LocKeyFor(evt.Death.WasFemale), evt.Death.Cause.ToString());
			// Титул стоит первым словом в предложении — с заглавной буквы («Королева …»).
			if (evt.Params.TryGetValue("title", out var title) && !string.IsNullOrEmpty(title))
			{
				evt.Params["title"] = GameText.Capitalize(title);
			}
			if (!string.IsNullOrEmpty(evt.Death.KillerName) || !string.IsNullOrEmpty(evt.Death.KillerSpecies))
			{
				evt.Params["killer"] = Chronicle.KillerShort(evt.Death);
			}
		}
	}

	private static void LocalizeKeyParams(PolEvent evt)
	{
		List<string> keys = new List<string>(evt.Params.Keys);
		for (int i = 0; i < keys.Count; i++)
		{
			string key = keys[i];
			string value = evt.Params[key];
			if (value != null && (value.StartsWith("RP_", StringComparison.Ordinal) || CivicText.IsLocalizationKey(value)))
			{
				evt.Params[key] = GameText.Localized(value, CivicText.Fallback(value));
			}
		}
	}

	private static void ResolveNoveltyName(PolEvent evt, MapBox world, string kind)
	{
		if (evt.Params.ContainsKey(kind))
		{
			return;
		}
		if (!evt.CityId.HasValue)
		{
			return;
		}
		int id = evt.GetId(kind + "_id");
		if (id == 0)
		{
			return;
		}
		City city = CityById(world, evt.CityId.Value);
		if (city == null)
		{
			return;
		}
		string name = kind switch
		{
			"religion" => GameRefs.CityReligionName(city),
			"language" => GameRefs.CityLanguageName(city),
			_ => GameRefs.CityCultureName(city),
		};
		if (!string.IsNullOrEmpty(name))
		{
			evt.Params[kind] = name;
		}
	}

	public static string KingdomName(MapBox map, int id)
	{
		List<Kingdom> list = GameRefs.KingdomList(map);
		if (list != null)
		{
			for (int i = 0; i < list.Count; i++)
			{
				if (list[i] != null && (int)((NanoObject)list[i]).id == id)
				{
					string liveName = ((NanoObject)list[i]).name;
					if (!string.IsNullOrWhiteSpace(liveName))
					{
						return liveName;
					}
				}
			}
		}
		string cached = RealPoliticMod.World?.KingdomNameOf(id);
		if (!string.IsNullOrWhiteSpace(cached))
		{
			return cached;
		}
		return "государство №" + id;
	}

	public static string CityName(MapBox map, int id)
	{
		City city = CityById(map, id);
		if (city != null)
		{
			return ((NanoObject)(object)city).name;
		}
		return "город №" + id;
	}

	public static City CityById(MapBox map, int id)
	{
		List<City> list = GameRefs.CityList(map);
		for (int i = 0; i < list.Count; i++)
		{
			if (list[i] != null && (int)((NanoObject)list[i]).id == id)
			{
				return list[i];
			}
		}
		return null;
	}

	private string DefaultTemplate(PolEvent evt)
	{
        if (evt.LocKey != null && CivicText.IsLocalizationKey(evt.LocKey))
        {
            string embedded = CivicText.Fallback(evt.LocKey);
            if (!string.IsNullOrWhiteSpace(embedded) && !string.Equals(embedded, evt.LocKey, StringComparison.Ordinal))
            {
                return embedded;
            }
        }
		if (evt.LocKey.EndsWith("_SUMMARY", StringComparison.Ordinal))
		{
			switch (evt.Type)
			{
			case PolEventType.ActorConverted:
				return "Взгляды сменили жителей: {count}";
			case PolEventType.NewReligion:
				return "За год основано новых религий: {count}";
			case PolEventType.NewLanguage:
				return "За год зародилось новых языков: {count}";
			case PolEventType.NewCulture:
				return "За год возникло новых культур: {count}";
			case PolEventType.DiplomaticAction:
				return "За год совершено дипломатических действий: {count}";
			case PolEventType.EconomicCrisisStarted:
				return "За год началось экономических кризисов в мире: {count}";
			case PolEventType.IdeologyThreatBroke:
				return "За год случилось внутренних кризисов строя: {count}";
			case PolEventType.WarReferendum:
				return "За год проведено военных референдумов: {count}";
			case PolEventType.TreatySigned:
				return "За год заключено международных договоров: {count}";
			default:
				return "Сводка однотипных событий за год: {count}";
			}
		}
		if (evt.Type == PolEventType.IdeologyPowerUsed)
		{
			return "{kingdom}: особое действие строя";
		}
		if (evt.Type == PolEventType.IdeologyThreatBroke)
		{
			return "{kingdom}: внутренний кризис строя";
		}
		if (evt.Type == PolEventType.IdeologyThreatRecovered)
		{
			return "{kingdom}: внутренний кризис миновал";
		}
		if (evt.Type == PolEventType.IdeologyPowerEnded)
		{
			return "{kingdom}: особое действие строя завершено";
		}
		return evt.Type switch
		{
			PolEventType.KingdomAdoptedIdeology => "{kingdom}: строй сменился на {new}",
			PolEventType.KingdomReformed => "{kingdom}: курс реформ — {new}",
			PolEventType.RevolutionStarted => "{kingdom}: началось восстание сторонников {ideology}",
			PolEventType.RevolutionSucceeded => "{kingdom}: революция победила, теперь это {new}",
			PolEventType.RevolutionFailed => "{kingdom}: восстание {ideology} подавлено",
			PolEventType.RulerDeath => "{title} {ruler} — {cause}",
			PolEventType.Succession => "В {state} {kingdom} — {new} {title} {ruler}",
			PolEventType.NewReligion => "В {state} {kingdom} основана религия {religion}",
			PolEventType.NewLanguage => "В {state} {kingdom} зародился язык {language}",
			PolEventType.NewCulture => "В {state} {kingdom} основана культура {culture}",
			PolEventType.UnionFormed => "Образован союз «{name}»",
			PolEventType.UnionDissolved => "Союз «{name}» распался",
			PolEventType.UnionMemberJoined => "Государство {member} вошло в союз «{union}» ({count} держав)",
			PolEventType.UnionMemberLeft => "Из союза «{union}» вышло государство",
			PolEventType.UnionMerged => (string.Equals(evt.Get("union_kind"), "confederation", StringComparison.Ordinal) || string.Equals(evt.LocKey, "RP_EV_CONFEDERATION_MERGED", StringComparison.Ordinal))
				? "Вольная территория {member} добровольно объединяется с вольной территорией {leader} в конфедерацию вольных территорий «{name}»"
				: ((string.Equals(evt.Get("union_kind"), "federation", StringComparison.Ordinal) || string.Equals(evt.LocKey, "RP_EV_FEDERATION_MERGED", StringComparison.Ordinal))
					? "Держава {member} добровольно объединяется с державой {leader} в федерацию «{name}»"
					: "Держава {member} добровольно объединяется с державой {leader} в унию «{name}»"),
			PolEventType.Impeached => string.Equals(evt.LocKey, "RP_EV_ATAMAN_DISMISSED", StringComparison.Ordinal)
				? "Жители вольной территории {kingdom} сняли атамана {ruler} с должности"
				: "Собрание {kingdom}: импичмент — глава {ruler} отозван волей закона",
			PolEventType.CityIdeologyShift => "{city}: настроения сменились на {new}",
			PolEventType.DiplomaticAction => "{kingdom} → {action}",
			PolEventType.RegencyStarted => "В {state} {kingdom} назначен регентский совет для {ruler} (возраст {age} лет)",
			PolEventType.RegencyEnded => "В {state} {kingdom} правитель {ruler} достиг совершеннолетия, регентство завершено",
			PolEventType.DemocraticElection => "{kingdom}: выборы / ротация власти — {ruler} (срок {terms})",
			PolEventType.EthnonationalistTension => "{kingdom}: межэтническая напряжённость достигла опасного уровня",
			PolEventType.DynasticMarriage => "{kingdom}: династический брак с державой {partner} — {ruler} и {spouse}",
			PolEventType.TreatySigned => "{kingdom}: заключён договор с державой {partner} ({treaty})",
			PolEventType.TreatyBroken => "{kingdom}: нарушен договор с державой {partner} ({treaty})",
			PolEventType.TreatyExpired => "{kingdom}: истёк срок договора с державой {partner} ({treaty})",
			PolEventType.CasusBelliDeclared => "{kingdom} объявляет войну державе {target} — повод: {cb}",
			PolEventType.EspionageExposed => "{kingdom}: разоблачены агенты державы {spy} ({op})",
			PolEventType.EspionageSuccess => "{kingdom}: тайная операция против {target} удалась ({op})",
			PolEventType.CoalitionFormed => "Против державы {target} собрана коалиция из {count} государств",
			PolEventType.CoalitionWar => "{kingdom}: вступление в войну против {target} за союзника {ally}",
			PolEventType.CoalitionDissolved => "Коалиция против державы {target} распущена",
			PolEventType.VassalRebellion => "{kingdom}: вассал восстал против сюзерена {suzerain}",
			PolEventType.AllianceFormed => "Создан альянс «{name}» во главе с державой {leader} ({count} держав)",
			PolEventType.AllianceBroken => "Альянс «{name}» распался",
			PolEventType.AllianceMemberJoined => "Держава {member} вступила в альянс «{name}» ({count} держав)",
			PolEventType.AllianceMemberLeft => "Держава {member} покинула альянс «{name}»",
			PolEventType.UnionCrownInherited => "{kingdom}: корона осталась в союзе «{union}» — наследник признал старшинство лидера",
			PolEventType.UnionSuccessionCrisis => "{kingdom}: кризис престолонаследия разорвал союз «{union}»",
			PolEventType.UnionInstitutionGained => "Союз «{union}» учредил институт: {institution} (уровень {level})",
			PolEventType.IntegrationStarted => "{kingdom} начинает интеграцию державы {member} (союз «{union}»)",
			PolEventType.IntegrationCompleted => "{kingdom} завершила интеграцию: держава {member} поглощена",
			PolEventType.UnionReformed => "Союз «{union}» реформирован в полную унию во главе с державой {leader}",
			PolEventType.PeaceSettlement => "Мирный договор: держава {victor} диктует условия державе {defeated} — {terms}",
			PolEventType.WhitePeace => "Белый мир: {kingdom} и {partner} разошлись без победителя ({years} лет войны)",
			PolEventType.TruceBroken => "{kingdom} вероломно разорвало перемирие с державой {partner}!",
			PolEventType.TruceExpired => "Истекло перемирие между {kingdom} и {partner}",
			// 0.44: КМУД в уведомлениях. Раньше этих строк не было вовсе: уведомление
			// показывало сухую подпись из словаря («Созвана КМУД…») без имён, чисел и лет,
			// а часть событий — вообще служебный ключ. Владелец 29.10.2026: «почему в
			// уведомлениях ни словом ни духом о КМУДе?». Теперь каждое событие КМУД
			// говорит по-человечески: сколько держав, какие войны, что решили.
			// 0.48.4: «через 1 лет» — не по-русски. Склонение считаем при сборке текста.
			PolEventType.KmudUpcoming => "КМУД: съезд в {year} году — через {years_word}. За столом будут все державы ({kingdoms} с землёй).",
			// 0.49: национализм — видно, кто, чего хочет и до какого года закрыт въезд.
			PolEventType.NationalismSeizedPower => "В державе {kingdom} к власти пришли националисты: требуют вернуть государство «{origin}» ({bound}). Чужим закрыт въезд до {ban_until} года.",
			PolEventType.NationalismPowerReturned => "{kingdom}: националисты вернули прежний строй и ушли в тень. Чужим закрыт въезд до {ban_until} года.",
			// 0.49.1: расовый национализм — чужая раса у власти.
			PolEventType.RaceNationalismSeizedPower => "В державе {kingdom} к власти пришёл народ «{race}» ({share}% населения): чужим отозвали гражданство, въезд закрыт до {ban_until} года.",
			PolEventType.RaceNationalismPowerReturned => "{kingdom}: народ «{race}» вернул прежний строй. Въезд чужим закрыт до {ban_until} года.",
			// 0.52: народы и местные уставы.
			PolEventType.PeopleLanguageShift => "{kingdom}: народ {race} — теперь язык: {stage}.",
			PolEventType.LocalConstitutionAdopted => "{city} ({kingdom}): народ {race} написал свой устав — язык и суд по своим обычаям.",
			PolEventType.LocalConstitutionBanned => "{kingdom}: столица запретила народу {race} свой устав в городе {city}. Город этого не забудет.",
			PolEventType.LocalConstitutionConflict => "{city} ({kingdom}): народ {race} жалуется на столицу — обида {grievance} из 100. Устав расходится с общим законом.",
			PolEventType.LocalConstitutionRevolt => "{city} ({kingdom}): народ {race} доведён до предела — обида {grievance} из 100. Ждите мятежа.",
			// 0.53: палаты.
			PolEventType.BillPassed => "{kingdom}: палаты приняли закон — «{article}». Голоса: {lower} за в нижней палате, {upper} за в совете земель.",
			PolEventType.BillRejected => "{kingdom}: закон «{article}» отклонён — палаты не нашли большинства.",
			PolEventType.ChambersDisagree => "{kingdom}: палаты разошлись по статье «{article}» (нижняя — {lower} за, совет земель — {upper} за). Согласительная комиссия на год.",
			PolEventType.RulerVetoed => "{kingdom}: правитель {ruler} наложил вето на закон «{article}», принятый палатами.",
			PolEventType.VetoOverridden => "{kingdom}: палаты преодолели вето правителя двумя третями — закон «{article}» принят.",
			PolEventType.ForeignRulerWithoutLaw => "{kingdom}: правит {ruler} из народа «{race}», а конституция велит править только «{titular}». Законность упала, народ ропщет.",
			// 0.50: мир возмущается и наказывает.
			PolEventType.WorldOutrageSanctions => "Мир возмущён: против {kingdom} объявлены санкции на {years_word} (до {until} года), поддержали держав: {supporters}.",
			PolEventType.WorldOutrageStatusLost => "{kingdom} лишена звания великой державы — за отнятое у жителей гражданство.",
			PolEventType.KmudConvened => "КМУД созвана: держав за столом {members}, войн прекращено {wars_ended}. Запрет войн до {until} года, итоги — в {closes}.",
			PolEventType.KmudAllianceFormed => "КМУД: все державы вступили в общий альянс «{name}» ({members} держав).",
			PolEventType.KmudPauseViolated => "КМУД: войну пытались объявить, но действует запрет — погашено войн: {count}. Запрет держится до {until} года.",
			PolEventType.KmudAgenda => "КМУД: повестка дня. Конфликт: {aggressor} против {defender} ({years} лет). Худшая держава: {worst}. Самая бедная: {poorest}. Пятый вопрос — возвращение народа «{race}» в державу {to} (уходит из {from}).",
			// 0.47: решения заседания — по одному сообщению на вопрос, человеческим языком.
			PolEventType.KmudResult => "КМУД, решение №{number}: {question} — {answer}. Голосовало жителей: {voters}.",
			PolEventType.KmudRepatriation => "КМУД: народ «{race}» возвращается в державу {to} — переехало человек: {moved}. Охрана КМУД на 20 лет: {guards} пактов.",
			PolEventType.KmudResultsAnnounced => "КМУД: голосование окончено. Голосовало жителей: {total_voters}. Мир продлён на {peace_years} лет.",
			PolEventType.KmudClosed => "КМУД закрыта: альянс распущен, решения вступили в силу.",
			PolEventType.KmudPact => "КМУД: пакт «только оборона» между {a} и {b} на {years} лет (до {until} года).",
			PolEventType.KmudReparations => "КМУД: {payer} платит {payee} по {amount} золота в год — {years} лет.",
			PolEventType.KmudExecution => "КМУД: казнён правитель державы {kingdom} — {ruler}.",
			PolEventType.KmudIdeologyChange => "КМУД: держава {kingdom} сменила строй — {old} становится {new}.",
			PolEventType.KmudSanctions => "КМУД: против державы {kingdom} введены санкции ({supporters} держав) до {until} года.",
			PolEventType.KmudPardoned => "КМУД: наказание исполнено — с державы {kingdom} сняты обвинения ({count}).",
			PolEventType.KmudAid => "КМУД: помощь державе {kingdom} — {aid}.",
			PolEventType.KmudWarPrevented => "КМУД: войну не дали объявить — действует запрет до {until} года (всего отбито: {count}).",
			// 0.46: переселение, расселение, малые народы, парламент.
			PolEventType.MigrationResettled => string.Equals(evt.Get("outcome"), "Escaped", StringComparison.Ordinal)
				? "{person} сбежал из державы {from} в {to} — теперь он гражданин новой державы"
				: "{person} переехал из державы {from} в {to}",
			PolEventType.CityFounded => "Держава {kingdom} основала новый город на свободной земле",
			PolEventType.RaceEndangered => "{kingdom}: народ {race} на грани исчезновения ({population}) — ему грозит вытеснение",
			PolEventType.RaceProtected => "{kingdom}: народ {race} взят под защиту закона ({population})",
			PolEventType.ParliamentElected => "{kingdom}: выборы в парламент — «{party}» ({ideology}) получает {seats} мест из 100, у оппозиции {opposition}",
			// Этап 23.
			PolEventType.KingdomCollapsed => "Государство {kingdom} распалось на {count} обломков: {fragments}",
			PolEventType.RegencyCrowned => "{kingdom}: юный наследник {heir} — корону носит регент {regent}",
			PolEventType.RegencyPassed => "{kingdom}: наследник {heir} совершеннолетний — регент {regent} вернул корону",
			PolEventType.DynastyRenamed => "{kingdom}: клан теперь зовётся династия {dynasty} (по фамилии основателя {founder})",
			PolEventType.PowerRaid => "{kingdom}: тайный авиаудар по городу {town} державы {target}",
			PolEventType.BorderOpened => "{kingdom} и {partner} открыли друг другу границы",
			PolEventType.BorderClosed => "{kingdom} закрыло границу с {partner}",
			PolEventType.TradeRoutesSevered => "{kingdom}: границы закрыты — торговые пути с {partner} разорваны ({count})",
			PolEventType.BorderExpulsion => "{kingdom}: чужак {intruder} из {partner} пойман на границе — бит и гонится домой ножками",
			PolEventType.DynasticUnionFormed => "Заключена династическая уния между {state1} и {state2}",
			PolEventType.CitySeceded => "{city}: мятеж и сепарация от {kingdom} ({reason})",
			PolEventType.DynasticShift => "{kingdom}: смена правящего дома — династия «{new_dynasty}» заняла трон",
			PolEventType.CivilWarStarted => "{kingdom}: гражданская война ({cause}) — повстанцы за {ideology} держат {cities} городов",
			PolEventType.CivilWarEnded => "{kingdom}: гражданская война ({cause}) окончена — {resolution}; шла {years} лет",
			PolEventType.KingdomSeceded => "Раскол: от {kingdom} отделилось государство {ideology} из {cities} городов ({cause})",
			PolEventType.TradeRouteOpened => "{kingdom}: проложен новый торговый путь",
			PolEventType.TradeRouteUpgraded => "{kingdom}: торговый путь расширен до уровня {level}",
			PolEventType.TradeRouteBroken => "{kingdom}: торговля с {partner} прекращена",
			PolEventType.EconomicCrisisStarted => "{kingdom}: {crisis} — продлится около {years} лет",
			PolEventType.EconomicCrisisEnded => "{kingdom}: экономика оправилась ({crisis} позади)",
			PolEventType.EconomicBoom => "{kingdom}: экономический бум — благосостояние {prosperity}%, казна +{gold}",
			PolEventType.TradeEmbargo => "{kingdom}: торговое эмбарго против {target} на {years} лет (перекрыто путей: {routes})",
			PolEventType.EraChanged => "Наступила {era}. {era_effect}",
			PolEventType.SubspeciesExtinct => "Подвид {species} вымер: последний его представитель ушёл из мира. Вид {race} потерял одну из ветвей",
			PolEventType.KingdomRestored => "{kingdom} восстановлено державой {victor} как {mode}: возвращено {cities} гор., {kept} гор. со столицей остались за победителем",
			PolEventType.RoyalRelativeSeceded => "Династический раскол: {relative} — {relation} правителя {kingdom} {ruler} — основал(а) собственное государство {new_kingdom}. Две ветви одного рода теперь правят разными странами",
			PolEventType.StrategicCampaignStarted => "{kingdom} начинает долгосрочную кампанию «{goal}» (срок: {years} лет, шагов: {steps}).",
			PolEventType.StrategicCampaignCompleted => "{kingdom}: кампания «{goal}» завершена ({result}, выполнено шагов: {steps}).",
			PolEventType.CivicPetitionSubmitted => "{kingdom}: граждане подали петицию «{demand}» (поддержка: {support}%).",
			PolEventType.CivicUltimatumResolved => "{kingdom}: ультиматум движения «{demand}» ({support}% граждан) — {outcome}.",
			PolEventType.ParliamentaryShift => "{kingdom}: большинство в собрании получила партия «{party}» ({seats}% мест).",
			PolEventType.NationalLiberationUprising => "{kingdom}: судьба исторических земель «{homeland}» ({cities} гор.) — {mode}.",
			PolEventType.PeaceConferenceConcluded => "Мирная конференция ({allies} союзн.) после победы над {defeated}: раздел трофеев и вкладов — {division}.",
			PolEventType.CoalitionSpoilsDispute => "Дипломатический раскол: держава {ally} (вклад {share}%) возмущена тем, что {leader} обделил её при разделе земель {defeated}.",
			PolEventType.RecognitionLobbyMission => "Непризнанное государство {kingdom} направило посольство к великой державе {power}, добиваясь международного признания.",
			PolEventType.MetropolisRecognitionProtest => "Метрополия {metropolis} направила дипломатический протест великим державам против признания мятежного края {breakaway}.",
			_ => evt.LocKey,
		};
	}
}
