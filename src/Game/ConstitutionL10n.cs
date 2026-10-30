using System.Collections.Generic;
using System.Text;
using RealPolitic.Core;

namespace RealPolitic;

/// <summary>
/// Этап 26.2a: локализация значений конституционных статей (ключи ru/en, как во всём моде).
/// Ядро хранит енумы и русские тексты-запас; игра показывает локализованные строки.
/// </summary>
public static class ConstitutionL10n
{
	// ---------------------------------------------------------------- формы правления

	public static string FormName(HeadOfState form, IdeologyType ideology)
	{
		string key;
		switch (form)
		{
			case HeadOfState.Hereditary:
				switch (ideology)
				{
					case IdeologyType.Tribalism: key = "RP_CONST_FORM_CHIEF"; break;
					case IdeologyType.Militarism: key = "RP_CONST_FORM_STRATOCRACY"; break;
					case IdeologyType.Ethnonationalism: key = "RP_CONST_FORM_VOZHD"; break;
					case IdeologyType.Communism: key = "RP_CONST_FORM_PARTY"; break;
					case IdeologyType.Khaganate: key = "RP_CONST_FORM_KURULTAI"; break;
					case IdeologyType.Empire: key = "RP_CONST_FORM_EMPIRE"; break;
					default: key = "RP_CONST_FORM_ABSOLUTE"; break;
				}
				break;
			case HeadOfState.Elected:
				key = (ideology == IdeologyType.Monarchy) ? "RP_CONST_FORM_ELECTED_MON" : "RP_CONST_FORM_PRESIDENTIAL";
				break;
			case HeadOfState.ParliamentaryMonarchy:
				key = "RP_CONST_FORM_PARLIAMENTARY_MON";
				break;
			case HeadOfState.Parliamentary:
				switch (ideology)
				{
					case IdeologyType.Aristocracy: key = "RP_CONST_FORM_SENATE"; break;
					case IdeologyType.Theocracy: key = "RP_CONST_FORM_CLERGY"; break;
					case IdeologyType.MerchantRepublic: key = "RP_CONST_FORM_DRUZHINA"; break;
					case IdeologyType.Communism: key = "RP_CONST_FORM_SOVIET"; break;
					case IdeologyType.Technocracy: key = "RP_CONST_FORM_DIRECTORS"; break;
					case IdeologyType.Liberalism: key = "RP_CONST_FORM_PARL_REP"; break;
					default: key = "RP_CONST_FORM_PARL_DEM"; break;
				}
				break;
			case HeadOfState.Assembly:
				key = (ideology == IdeologyType.FreeTerritory) ? "RP_CONST_FORM_FREECIRCLE" : "RP_CONST_FORM_VECHE";
				break;
			default:
				return form.ToString();
		}
		return GameText.Localized(key, ConstitutionTexts.FormName(form, ideology));
	}

	// ---------------------------------------------------------------- значения статей

	public static string WarAuthorityText(WarAuthority a)
	{
		string key = (a == WarAuthority.ByRuler) ? "RP_CONST_WAR_RULER"
			: (a == WarAuthority.ByAssembly) ? "RP_CONST_WAR_ASSEMBLY"
			: "RP_CONST_WAR_PEOPLE";
		return GameText.Localized(key, ConstitutionTexts.WarAuthorityText(a));
	}

	public static string WarBodyName(WarAuthority a)
	{
		string key = (a == WarAuthority.ByAssembly) ? "RP_CONST_BODY_ASSEMBLY"
			: (a == WarAuthority.ByPeople) ? "RP_CONST_BODY_PEOPLE"
			: "RP_CONST_BODY_RULER";
		return GameText.Localized(key, ConstitutionTexts.WarBodyName(a));
	}

	public static string BorderText(BorderLaw b)
	{
		string key = (b == BorderLaw.Open) ? "RP_CONST_BORDER_OPEN"
			: (b == BorderLaw.Controlled) ? "RP_CONST_BORDER_CONTROLLED"
			: "RP_CONST_BORDER_CLOSED";
		return GameText.Localized(key, ConstitutionTexts.BorderText(b));
	}

	public static string SuccessionText(SuccessionRule s)
	{
		string key = (s == SuccessionRule.Strict) ? "RP_CONST_SUCC_STRICT" : "RP_CONST_SUCC_ANYADULT";
		return GameText.Localized(key, ConstitutionTexts.SuccessionText(s));
	}

	public static string SupremacyText(SupremacyRule s)
	{
		string key = (s == SupremacyRule.Binding) ? "RP_CONST_SUP_BINDING" : "RP_CONST_SUP_ADVISORY";
		return GameText.Localized(key, ConstitutionTexts.SupremacyText(s));
	}

	public static string FranchiseName(Franchise f)
	{
		string key = (f == Franchise.Universal) ? "RP_CONST_FR_UNIVERSAL"
			: (f == Franchise.Notables) ? "RP_CONST_FR_NOTABLES"
			: "RP_CONST_FR_CLAN";
		return GameText.Localized(key, ConstitutionTexts.FranchiseName(f));
	}

	// ---------------------------------------------------------------- статьи VII–XV (0.24)

	public static string TaxText(TaxLaw v)
	{
		string key = (v == TaxLaw.Light) ? "RP_CONST_TAX_LIGHT"
			: (v == TaxLaw.Crushing) ? "RP_CONST_TAX_CRUSHING"
			: "RP_CONST_TAX_MODERATE";
		return GameText.Localized(key, ConstitutionTexts.TaxText(v));
	}

	public static string TradeLawText(TradeLaw v)
	{
		string key = (v == TradeLaw.FreeTrade) ? "RP_CONST_TRADE_FREE"
			: (v == TradeLaw.Protectionism) ? "RP_CONST_TRADE_PROTECTION"
			: "RP_CONST_TRADE_MODERATE";
		return GameText.Localized(key, ConstitutionTexts.TradeLawText(v));
	}

	public static string FaithText(FaithLaw v)
	{
		string key = (v == FaithLaw.Tolerance) ? "RP_CONST_FAITH_TOLERANCE"
			: (v == FaithLaw.Purity) ? "RP_CONST_FAITH_PURITY"
			: "RP_CONST_FAITH_STATE";
		return GameText.Localized(key, ConstitutionTexts.FaithText(v));
	}

	public static string LevyText(LevyLaw v)
	{
		string key = (v == LevyLaw.Militia) ? "RP_CONST_LEVY_MILITIA"
			: (v == LevyLaw.Guard) ? "RP_CONST_LEVY_GUARD"
			: "RP_CONST_LEVY_CONSCRIPT";
		return GameText.Localized(key, ConstitutionTexts.LevyText(v));
	}

	public static string LandText(LandLaw v)
	{
		string key = (v == LandLaw.Restitution) ? "RP_CONST_LAND_RESTITUTION"
			: (v == LandLaw.Expansion) ? "RP_CONST_LAND_EXPANSION"
			: "RP_CONST_LAND_FREE";
		return GameText.Localized(key, ConstitutionTexts.LandText(v));
	}

	public static string WatchText(WatchLaw v)
	{
		string key = (v == WatchLaw.Liberty) ? "RP_CONST_WATCH_LIBERTY"
			: (v == WatchLaw.Patrol) ? "RP_CONST_WATCH_PATROL"
			: "RP_CONST_WATCH_SECRET";
		return GameText.Localized(key, ConstitutionTexts.WatchText(v));
	}

	public static string BloodText(BloodLaw v)
	{
		string key = (v == BloodLaw.Current) ? "RP_CONST_BLOOD_CURRENT"
			: (v == BloodLaw.DirectBlood) ? "RP_CONST_BLOOD_DIRECT"
			: "RP_CONST_BLOOD_ANY";
		return GameText.Localized(key, ConstitutionTexts.BloodText(v));
	}

	public static string ImpeachmentText(ImpeachmentLaw v)
	{
		string key = (v == ImpeachmentLaw.CityLeaders) ? "RP_CONST_IMPEACH_CITY"
			: (v == ImpeachmentLaw.PeopleVote) ? "RP_CONST_IMPEACH_PEOPLE"
			: "RP_CONST_IMPEACH_FORBIDDEN";
		return GameText.Localized(key, ConstitutionTexts.ImpeachmentText(v, false));
	}

	public static string UnionLawText(UnionLaw v)
	{
		string key = (v == UnionLaw.All) ? "RP_CONST_UNION_ALL"
			: (v == UnionLaw.Neighbors) ? "RP_CONST_UNION_NEIGHBORS"
			: "RP_CONST_UNION_LANG";
		return GameText.Localized(key, ConstitutionTexts.UnionLawText(v));
	}

	// ---------------------------------------------------------------- короткая сводка (строка профиля)

	public static string Summary(Constitution c, IdeologyType ideology)
	{
		if (c == null)
		{
			return string.Empty;
		}
		StringBuilder sb = new StringBuilder();
		sb.Append(FormName(c.StateForm, ideology));
		sb.Append(" · ");
		sb.Append(GameText.Localized("RP_CONST_SUM_WAR", "война")).Append(": ").Append(WarAuthorityText(c.WarPower));
		sb.Append(" · ");
		sb.Append(GameText.Localized("RP_CONST_SUM_BORDERS", "границы")).Append(": ").Append(BorderText(c.Borders));
		return sb.ToString();
	}
}
