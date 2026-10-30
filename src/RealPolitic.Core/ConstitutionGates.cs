using System;
using System.Collections.Generic;

namespace RealPolitic.Core;

/// <summary>Названия конституционных статей и подвидов (русские экраны).</summary>
public static class ConstitutionTexts
{
	public static string FormName(HeadOfState form, IdeologyType ideology)
	{
		switch (form)
		{
			case HeadOfState.Hereditary:
				switch (ideology)
				{
					case IdeologyType.Tribalism: return "вождь старшин";
					case IdeologyType.Militarism: return "стратократия";
					case IdeologyType.Ethnonationalism: return "вождь нации";
					case IdeologyType.Communism: return "партийная диктатура";
					case IdeologyType.Khaganate: return "великий курултай";
					case IdeologyType.Empire: return "самодержавная империя";
					default: return "абсолютная монархия";
				}
			case HeadOfState.Elected:
				return (ideology == IdeologyType.Monarchy) ? "выборная монархия" : "президентская демократия";
			case HeadOfState.ParliamentaryMonarchy:
				return "парламентская монархия";
			case HeadOfState.Parliamentary:
				switch (ideology)
				{
					case IdeologyType.Aristocracy: return "сенат";
					case IdeologyType.Theocracy: return "собрание клира";
					case IdeologyType.MerchantRepublic: return "дружина";
					case IdeologyType.Communism: return "советская держава";
					case IdeologyType.Technocracy: return "совет директоров";
					case IdeologyType.Liberalism: return "парламентская республика";
					default: return "парламентская демократия";
				}
			case HeadOfState.Assembly:
				return (ideology == IdeologyType.FreeTerritory) ? "вольный круг" : "народное вече";
			default:
				return form.ToString();
		}
	}

	public static string FranchiseName(Franchise f)
	{
		switch (f)
		{
			case Franchise.Universal: return "всеобщее избирательное право";
			case Franchise.Notables: return "сословное избирательное право";
			case Franchise.Clan: return "только правящий род";
			default: return f.ToString();
		}
	}

	public static string WarAuthorityText(WarAuthority a)
	{
		switch (a)
		{
			case WarAuthority.ByRuler: return "правитель решает единолично";
			case WarAuthority.ByAssembly: return "решает палата голосованием";
			case WarAuthority.ByPeople: return "вопрос выносится на вече";
			default: return a.ToString();
		}
	}

	/// <summary>Кто голосует (существительное для летописи): палата или вече.</summary>
	public static string WarBodyName(WarAuthority a)
	{
		switch (a)
		{
			case WarAuthority.ByAssembly: return "палата собрания";
			case WarAuthority.ByPeople: return "народное вече";
			default: return "правитель";
		}
	}

	public static string BorderText(BorderLaw b)
	{
		switch (b)
		{
			case BorderLaw.Open: return "границы открыты";
			case BorderLaw.Controlled: return "границы регулируемые";
			case BorderLaw.Closed: return "границы закрыты";
			default: return b.ToString();
		}
	}

	public static string SuccessionText(SuccessionRule s)
	{
		switch (s)
		{
			case SuccessionRule.Strict: return "престолонаследие по закону";
			case SuccessionRule.AnyAdult: return "правит любой, кто взял престол";
			default: return s.ToString();
		}
	}

	public static string SupremacyText(SupremacyRule s)
	{
		switch (s)
		{
			case SupremacyRule.Binding: return "конституция обязательна для всех";
			case SupremacyRule.Advisory: return "конституция — обычай";
			default: return s.ToString();
		}
	}

	// ---- 0.24: значения статей VII–XV ----

	public static string TaxText(TaxLaw v)
	{
		switch (v)
		{
			case TaxLaw.Light: return "подати лёгкие";
			case TaxLaw.Crushing: return "подати кабальные";
			default: return "подати умеренные";
		}
	}

	public static string TradeLawText(TradeLaw v)
	{
		switch (v)
		{
			case TradeLaw.FreeTrade: return "торг свободный";
			case TradeLaw.Protectionism: return "протекционизм";
			default: return "пошлины умеренные";
		}
	}

	public static string FaithText(FaithLaw v)
	{
		switch (v)
		{
			case FaithLaw.Tolerance: return "веротерпимость";
			case FaithLaw.Purity: return "чистота веры";
			default: return "государственная вера";
		}
	}

	public static string LevyText(LevyLaw v)
	{
		switch (v)
		{
			case LevyLaw.Militia: return "народное ополчение";
			case LevyLaw.Guard: return "профессиональная гвардия";
			default: return "призыв подданных";
		}
	}

	public static string LandText(LandLaw v)
	{
		switch (v)
		{
			case LandLaw.Restitution: return "лишь возврат своего";
			case LandLaw.Expansion: return "приращение дозволено";
			default: return "расширение вольно";
		}
	}

	public static string WatchText(WatchLaw v)
	{
		switch (v)
		{
			case WatchLaw.Liberty: return "вольница";
			case WatchLaw.Patrol: return "городской дозор";
			default: return "тайный сыск";
		}
	}

	public static string BloodText(BloodLaw v)
	{
		switch (v)
		{
			case BloodLaw.Current: return "по закону и обычаю";
			case BloodLaw.DirectBlood: return "всякий рождённый от венценосца";
			default: return "всякий кровный потомок любого короля";
		}
	}

	public static string ImpeachmentText(ImpeachmentLaw v, bool applable)
	{
		switch (v)
		{
			case ImpeachmentLaw.CityLeaders: return "импичмент: половина лидеров городов";
			case ImpeachmentLaw.PeopleVote: return "импичмент: половина голосов народа";
			default: return "импичмент запрещён";
		}
	}

	public static string UnionLawText(UnionLaw v)
	{
		switch (v)
		{
			case UnionLaw.All: return "принять в себя каждого";
			case UnionLaw.Neighbors: return "принять только соседей";
			default: return "принять только говорящих на одном языке";
		}
	}

	/// <summary>Полный свод конституции для отображения в окне державы (4 строки).</summary>
	public static List<string> Describe(Constitution c, IdeologyType ideology)
	{
		List<string> lines = new List<string>();
		if (c == null)
		{
			return lines;
		}
		lines.Add("форма правления: " + FormName(c.StateForm, ideology));
		if (c.StateForm == HeadOfState.Elected || c.StateForm == HeadOfState.Parliamentary)
		{
			lines.Add("право голоса: " + FranchiseName(c.Franchise));
		}
		lines.Add("объявление войны: " + WarAuthorityText(c.WarPower));
		lines.Add(BorderText(c.Borders) + " · " + SuccessionText(c.Succession));
		lines.Add(SupremacyText(c.Supremacy));
		return lines;
	}
}

/// <summary>
/// Законы→решения: какие действия мода конституция разрешает, а какие глушит.
/// </summary>
public static class ConstitutionGates
{
	/// <summary>Право решать войну — следствие формы правления (дизайн §2.3).</summary>
	public static WarAuthority WarAuthorityOf(HeadOfState form)
	{
		switch (form)
		{
			case HeadOfState.ParliamentaryMonarchy:
			case HeadOfState.Parliamentary:
				return WarAuthority.ByAssembly;
			case HeadOfState.Assembly:
				return WarAuthority.ByPeople;
			default:
				return WarAuthority.ByRuler;
		}
	}

	/// <summary>0.23.13: полномочия правителя во время активного регентства урезаны —
	/// за малолетнего монарха войной не бросаются: право переходит собранию, как при парламентской форме.</summary>
	public static WarAuthority EffectiveWarAuthority(KingdomPolitics kingdom)
	{
		if (kingdom?.Constitution == null)
		{
			return WarAuthority.ByRuler;
		}
		if (kingdom.Regency != null && kingdom.Regency.IsActive && kingdom.Ruler != null && kingdom.Ruler.IsRegent)
		{
			// Регент не вправе единолично воевать: решение у палаты (или, при народной форме, у веча).
			WarAuthority a = WarAuthorityOf(kingdom.Constitution.StateForm);
			if (a != WarAuthority.ByRuler)
			{
				return a;
			}
			return WarAuthority.ByAssembly;
		}
		return WarAuthorityOf(kingdom.Constitution.StateForm);
	}

	/// <summary>Формы, где голова — выборное лицо (у них выборы Этапа 16 где бы ни происходили).</summary>
	public static bool HoldsElections(HeadOfState form)
	{
		return form == HeadOfState.Elected || form == HeadOfState.Parliamentary;
	}

	/// <summary>Разрешена ли вообще облава-выдворение на границе этой державы:
	/// закон «Open» стопорит закрытие только в Binding-конституциях (советская — бормочет, не вяжет).</summary>
	public static bool BorderEnforcementAllowed(Constitution c)
	{
		return c == null || c.Supremacy != SupremacyRule.Binding || c.Borders != BorderLaw.Open;
	}

	/// <summary>Границы должны быть закрыты принудительно (закон Closed требует, не только ИИ-решение).</summary>
	public static bool BorderMustBeClosed(Constitution c)
	{
		return c != null && c.Supremacy == SupremacyRule.Binding && c.Borders == BorderLaw.Closed;
	}

	/// <summary>
	/// 0.24, статья XIII «О кровных связях»: имеет ли нынешний глава право крови на венец.
	/// DirectBlood («всякий рождённый от короля или королевы») признаёт претендентов, чей род
	/// уже правил этой державой; AnyRoyalBlood («кровный потомок любого венценосца») признаёт
	/// ещё и претензии через предков-правителей — узурпаторами они не клеймятся.
	/// Current — право как сейчас: без привилегий крови.
	/// </summary>
	public static bool BloodClaim(KingdomPolitics state, bool dynastyShifted, bool inheritedByClaim, long clanId = 0, IEnumerable<long> royalAncestorClanIds = null)
	{
		if (state?.Constitution == null || !dynastyShifted || inheritedByClaim)
		{
			return false; // смены дома не было или претензия уже засчитана брачной скрепой
		}
		if (state.Ideology != IdeologyType.Monarchy
			&& state.Ideology != IdeologyType.Empire
			&& state.Ideology != IdeologyType.Khaganate)
		{
			return false; // кровная претензия — дело венценосных домов, империй и каганатов
		}
		if (state.Constitution.Supremacy == SupremacyRule.Advisory)
		{
			return false; // обычай лишь бормочет: право крови не связывает
		}
		BloodLaw law = state.Constitution.Blood;
		if (law == BloodLaw.Current)
		{
			return false; // без привилегий крови
		}
		if (clanId <= 0)
		{
			return false; // род претендента неизвестен
		}
		// Прямая кровь (DirectBlood): право имеет наследник, рождённый от последнего
		// венценосца — его род = род только что ушедшего дома. Дальние возвраты — нет.
		if (law == BloodLaw.DirectBlood)
		{
			return state.PreviousRoyalClanId == clanId;
		}
		// Кровь любого венценосца (AnyRoyalBlood): право имеет всякий, чей род когда-либо
		// сидел на троне державы (венценосная история) — широкий круг законных претендентов.
		if (state.PreviousRoyalClanId == clanId)
		{
			return true;
		}
		if (royalAncestorClanIds != null)
		{
			foreach (long id in royalAncestorClanIds)
			{
				if (id == clanId)
				{
					return true;
				}
			}
		}
		return false;
	}

	/// <summary>
	/// 0.24, статья XIV «Об импичменте»: выборная держава может досрочно отозвать главу.
	/// Возвращает true, если импичмент проходим (половина лидеров городов либо половина народа
	/// при низкой поддержке), и false, если статья запрещена или не применяется (нет выборов).
	/// </summary>
	public static bool ImpeachmentDue(KingdomPolitics state, float support, bool holdsElections)
	{
		if (state?.Constitution == null || !holdsElections)
		{
			return false;
		}
		ImpeachmentLaw law = state.Constitution.Impeachment;
		if (law == ImpeachmentLaw.Forbidden)
		{
			return false;
		}
		// Есть за что отзывать: поддержка ниже половины.
		return support < 50f;
	}

	/// <summary>
	/// 0.24, статья XIII «О кровных связях» — претензии на трон по праву крови:
	/// lawful / unlawful для летописи (Advisory — только бормочет, силы нет).
	/// </summary>
	public static bool BloodIsEffective(KingdomPolitics state)
	{
		return state?.Constitution != null
			&& state.Constitution.Supremacy == SupremacyRule.Binding
			&& (state.Constitution.Blood == BloodLaw.DirectBlood || state.Constitution.Blood == BloodLaw.AnyRoyalBlood);
	}

	/// <summary>
	/// 0.24, статья XV «О федеративном устройстве»: кого поглощающая держава вправе принять в себя.
	/// Возвращает null, если вступление дозволено, иначе — ключ причины отказа.
	/// Язык — первоклассная мета-сущность игры (не культура): сверка идёт по CityLanguageId.
	/// </summary>
	public static string UnionAdmission(UnionLaw law, int leaderLanguageId, int memberLanguageId, bool adjacent)
	{
		switch (law)
		{
			case UnionLaw.Neighbors:
				return adjacent ? null : "RP_UNION_LAW_NEIGHBORS";
			case UnionLaw.SameLanguage:
				if (leaderLanguageId == 0 || memberLanguageId == 0)
				{
					// Нет данных о языке — не блокируем молча, а откладываем на верхний слой: он знает языки.
					return null;
				}
				return leaderLanguageId == memberLanguageId ? null : "RP_UNION_LAW_LANGUAGE";
			default:
				return null; // All — готовы принять каждого
		}
	}

	/// <summary>Проверка войны перед объявлением:
	/// 0=разрешено (ByRuler или есть действующее разрешение), 1=отказано конституцией.</summary>
	public static bool WarIsConstitutionallyAllowed(KingdomPolitics kingdom, int targetId, int year)
	{
		if (kingdom?.Constitution == null)
		{
			return true;
		}
		// обычай (Advisory) только бормочет, ничем не мешает правителю (§6 — ударяет лишь имидж)
		if (kingdom.Constitution.Supremacy == SupremacyRule.Advisory)
		{
			return true;
		}
		switch (ConstitutionGates.EffectiveWarAuthority(kingdom))
		{
			case WarAuthority.ByRuler:
				return true;
			default:
				return kingdom.HasWarAuthorization(targetId, year);
		}
	}
}
