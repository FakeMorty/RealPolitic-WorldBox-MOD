namespace RealPolitic.Core;

/// <summary>
/// Справочник модовых механик идеологий: у каждой идеологии ровно одна «ульта»
/// (активное действие с перезарядкой, которое запускает ИИ-правитель) и ровно одна
/// уникальная угроза (шкала 0..100, которая копится и срывается).
///
/// Данные отделены от логики: здесь только определения, поведение — в IdeologyPowers
/// и IdeologyThreats. Так проще балансировать и покрывать тестами.
/// </summary>
public static class IdeologyMechanics
{
	/// <summary>Ульта идеологии.</summary>
	public static IdeologyPowerDef Power(IdeologyType type)
	{
		switch (type)
		{
		case IdeologyType.Tribalism:
			// Великий сход: всплеск рождаемости и стойкости ценой казны.
			return new IdeologyPowerDef { NameKey = "RP_POWER_TRIBALISM", DescKey = "RP_POWER_TRIBALISM_DESC", Duration = 5, Cooldown = 25, MinAge = 5, MinLegitimacy = 30f };
		case IdeologyType.Monarchy:
			// Династический брак: длительный мир с соседом и рост лояльности.
			return new IdeologyPowerDef { NameKey = "RP_POWER_MONARCHY", DescKey = "RP_POWER_MONARCHY_DESC", Duration = 15, Cooldown = 30, MinAge = 8, MinLegitimacy = 45f };
		case IdeologyType.Theocracy:
			// Священный поход: бойцы сильнее, чужие единоверцы теряют лояльность.
			return new IdeologyPowerDef { NameKey = "RP_POWER_THEOCRACY", DescKey = "RP_POWER_THEOCRACY_DESC", Duration = 10, Cooldown = 35, MinAge = 10, MinLegitimacy = 40f };
		case IdeologyType.Aristocracy:
			// Пожалование титула: один город возвышается, остальные завидуют.
			return new IdeologyPowerDef { NameKey = "RP_POWER_ARISTOCRACY", DescKey = "RP_POWER_ARISTOCRACY_DESC", Duration = 12, Cooldown = 25, MinAge = 6, MinLegitimacy = 35f };
		case IdeologyType.MerchantRepublic:
			// Торговый договор: поток золота, пока держится мир.
			return new IdeologyPowerDef { NameKey = "RP_POWER_MERCHANTREPUBLIC", DescKey = "RP_POWER_MERCHANTREPUBLIC_DESC", Duration = 20, Cooldown = 25, MinAge = 5, MinLegitimacy = 30f };
		case IdeologyType.Militarism:
			// Всеобщая мобилизация: армия резко растёт, города пустеют.
			return new IdeologyPowerDef { NameKey = "RP_POWER_MILITARISM", DescKey = "RP_POWER_MILITARISM_DESC", Duration = 10, Cooldown = 30, MinAge = 5, MinLegitimacy = 25f };
		case IdeologyType.Egalitarianism:
			// Народное собрание: государство перенимает идеологию большинства жителей.
			return new IdeologyPowerDef { NameKey = "RP_POWER_EGALITARIANISM", DescKey = "RP_POWER_EGALITARIANISM_DESC", Duration = 1, Cooldown = 20, MinAge = 8, MinLegitimacy = 20f };
		case IdeologyType.Technocracy:
			// Великий проект: многолетнее вложение ради постоянного бонуса.
			return new IdeologyPowerDef { NameKey = "RP_POWER_TECHNOCRACY", DescKey = "RP_POWER_TECHNOCRACY_DESC", Duration = 15, Cooldown = 40, MinAge = 12, MinLegitimacy = 50f };
		case IdeologyType.Liberalism:
			// Свободная торговля: открытые рынки, рост казны и счастья, пока держится мир.
			return new IdeologyPowerDef { NameKey = "RP_POWER_LIBERALISM", DescKey = "RP_POWER_LIBERALISM_DESC", Duration = 15, Cooldown = 25, MinAge = 5, MinLegitimacy = 35f };
		case IdeologyType.Democracy:
			// Всенародный референдум: консолидация общества, снижение недовольства и рост поддержки.
			return new IdeologyPowerDef { NameKey = "RP_POWER_DEMOCRACY", DescKey = "RP_POWER_DEMOCRACY_DESC", Duration = 8, Cooldown = 25, MinAge = 5, MinLegitimacy = 35f };
		case IdeologyType.Communism:
			// Пятилетний план: мобилизация производства и ресурсов во всех городах.
			return new IdeologyPowerDef { NameKey = "RP_POWER_COMMUNISM", DescKey = "RP_POWER_COMMUNISM_DESC", Duration = 10, Cooldown = 30, MinAge = 5, MinLegitimacy = 40f };
		case IdeologyType.Ethnonationalism:
			// Национальный триумф: мобилизация титульного этноса, всплеск лояльности и военной мощи.
			return new IdeologyPowerDef { NameKey = "RP_POWER_ETHNONATIONALISM", DescKey = "RP_POWER_ETHNONATIONALISM_DESC", Duration = 10, Cooldown = 30, MinAge = 5, MinLegitimacy = 45f };
		case IdeologyType.Khaganate:
			// Великий курултай: сбор всех улусов и родов — сплочение орды, рост легитимности и боевого духа.
			return new IdeologyPowerDef { NameKey = "RP_POWER_KHAGANATE", DescKey = "RP_POWER_KHAGANATE_DESC", Duration = 8, Cooldown = 25, MinAge = 5, MinLegitimacy = 35f };
		case IdeologyType.Empire:
			// Имперский манифест: торжественная воля императорского престола, укрепляющая порядок во всех провинциях.
			return new IdeologyPowerDef { NameKey = "RP_POWER_EMPIRE", DescKey = "RP_POWER_EMPIRE_DESC", Duration = 12, Cooldown = 30, MinAge = 8, MinLegitimacy = 45f };
		case IdeologyType.FreeTerritory:
			// Вольный клич: призыв атамана и вольного круга к всеобщей защите свободной земли.
			return new IdeologyPowerDef { NameKey = "RP_POWER_FREETERRITORY", DescKey = "RP_POWER_FREETERRITORY_DESC", Duration = 8, Cooldown = 22, MinAge = 4, MinLegitimacy = 25f };
		default:
			return new IdeologyPowerDef { NameKey = "RP_POWER_NONE", DescKey = "RP_POWER_NONE_DESC", Duration = 0, Cooldown = 9999, MinAge = 9999, MinLegitimacy = 999f };
		}
	}

	/// <summary>Уникальная угроза идеологии.</summary>
	public static IdeologyThreatDef Threat(IdeologyType type)
	{
		switch (type)
		{
		case IdeologyType.Tribalism:
			// Раскол племени: чем больше городов, тем быстрее копится.
			return new IdeologyThreatDef { NameKey = "RP_THREAT_TRIBALISM", DescKey = "RP_THREAT_TRIBALISM_DESC", Threshold = 100f, Recovery = 8 };
		case IdeologyType.Monarchy:
			// Кризис престолонаследия: копится, когда нет наследника.
			return new IdeologyThreatDef { NameKey = "RP_THREAT_MONARCHY", DescKey = "RP_THREAT_MONARCHY_DESC", Threshold = 100f, Recovery = 10 };
		case IdeologyType.Theocracy:
			// Ересь: копится от доли инакомыслящих.
			return new IdeologyThreatDef { NameKey = "RP_THREAT_THEOCRACY", DescKey = "RP_THREAT_THEOCRACY_DESC", Threshold = 100f, Recovery = 8 };
		case IdeologyType.Aristocracy:
			// Заговор знати: копится в долгое мирное правление.
			return new IdeologyThreatDef { NameKey = "RP_THREAT_ARISTOCRACY", DescKey = "RP_THREAT_ARISTOCRACY_DESC", Threshold = 100f, Recovery = 12 };
		case IdeologyType.MerchantRepublic:
			// Долговой кризис: копится от войн.
			return new IdeologyThreatDef { NameKey = "RP_THREAT_MERCHANTREPUBLIC", DescKey = "RP_THREAT_MERCHANTREPUBLIC_DESC", Threshold = 100f, Recovery = 10 };
		case IdeologyType.Militarism:
			// Военная усталость: копится за каждый год войны.
			return new IdeologyThreatDef { NameKey = "RP_THREAT_MILITARISM", DescKey = "RP_THREAT_MILITARISM_DESC", Threshold = 100f, Recovery = 8 };
		case IdeologyType.Egalitarianism:
			// Паралич власти: копится при разбросе идеологий среди жителей.
			return new IdeologyThreatDef { NameKey = "RP_THREAT_EGALITARIANISM", DescKey = "RP_THREAT_EGALITARIANISM_DESC", Threshold = 100f, Recovery = 6 };
		case IdeologyType.Technocracy:
			// Утечка умов: копится при низком счастье/поддержке.
			return new IdeologyThreatDef { NameKey = "RP_THREAT_TECHNOCRACY", DescKey = "RP_THREAT_TECHNOCRACY_DESC", Threshold = 100f, Recovery = 10 };
		case IdeologyType.Liberalism:
			// Расслоение: рынок обогащает немногих, копится при низкой поддержке и войне.
			return new IdeologyThreatDef { NameKey = "RP_THREAT_LIBERALISM", DescKey = "RP_THREAT_LIBERALISM_DESC", Threshold = 100f, Recovery = 8 };
		case IdeologyType.Democracy:
			// Политическая поляризация: копится при росте недовольства и расколе парламента.
			return new IdeologyThreatDef { NameKey = "RP_THREAT_DEMOCRACY", DescKey = "RP_THREAT_DEMOCRACY_DESC", Threshold = 100f, Recovery = 8 };
		case IdeologyType.Communism:
			// Бюрократический застой: копится при падении энтузиазма/поддержки и затяжном застое.
			return new IdeologyThreatDef { NameKey = "RP_THREAT_COMMUNISM", DescKey = "RP_THREAT_COMMUNISM_DESC", Threshold = 100f, Recovery = 8 };
		case IdeologyType.Ethnonationalism:
			// Межэтнический бунт: копится от доли инородцев и внешнего давления.
			return new IdeologyThreatDef { NameKey = "RP_THREAT_ETHNONATIONALISM", DescKey = "RP_THREAT_ETHNONATIONALISM_DESC", Threshold = 100f, Recovery = 10 };
		case IdeologyType.Khaganate:
			// Междоусобица улусов: копится при долгом застое без походов и падении авторитета кагана.
			return new IdeologyThreatDef { NameKey = "RP_THREAT_KHAGANATE", DescKey = "RP_THREAT_KHAGANATE_DESC", Threshold = 100f, Recovery = 9 };
		case IdeologyType.Empire:
			// Дворцовый переворот: копится при перенапряжении империи и падении легитимности престола.
			return new IdeologyThreatDef { NameKey = "RP_THREAT_EMPIRE", DescKey = "RP_THREAT_EMPIRE_DESC", Threshold = 100f, Recovery = 10 };
		case IdeologyType.FreeTerritory:
			// Атаманщина: копится при разгуле своеволия общин и падении доверия к атаману.
			return new IdeologyThreatDef { NameKey = "RP_THREAT_FREETERRITORY", DescKey = "RP_THREAT_FREETERRITORY_DESC", Threshold = 100f, Recovery = 7 };
		default:
			return new IdeologyThreatDef { NameKey = "RP_THREAT_NONE", DescKey = "RP_THREAT_NONE_DESC", Threshold = 1000f, Recovery = 0 };
		}
	}

	/// <summary>Ключ локализации короткой строки «+X / −Y» для окна государства.</summary>
	public static string TradeoffKey(IdeologyType type)
	{
		return "RP_TRADEOFF_" + type.ToString().ToUpperInvariant();
	}
}
