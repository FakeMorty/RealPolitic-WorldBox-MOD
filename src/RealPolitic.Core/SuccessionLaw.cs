namespace RealPolitic.Core;

/// <summary>
/// Законы престолонаследия и смены власти (Succession Laws).
/// Определяют правила передачи трона или полномочий главы государства.
/// </summary>
public enum SuccessionLaw
{
	// Монархические законы престолонаследия:
	Salic,          // Салический закон: строго по мужской линии, женщины исключены
	Cognatic,       // Когнатическая примогенитура: женщины наследуют при отсутствии братьев/сыновей
	DynasticUnion,  // Династическая уния: династический брак или коронация образует личную унию
	StrictAgnatic,  // Строгий агнатический: только совершеннолетний наследник мужского пола (18+)

	// Законы преемственности других строев:
	Tanistry,           // Племенной совет / танистри (Tribalism)
	HolyConclave,       // Священный конклав жрецов (Theocracy)
	PatricianSenate,    // Сенат патрициев (Aristocracy)
	Plutocracy,         // Совет купеческих гильдий / плутократия (MerchantRepublic)
	Stratocracy,        // Военная хунта / стратократия (Militarism)
	PopularAssembly,    // Народное вече / собрание (Egalitarianism)
	Meritocracy,        // Совет академии по рангу заслуг (Technocracy)
	ParliamentaryVote,  // Парламентское голосование (Liberalism)
	DemocraticElection, // Всеобщие демократические выборы (Democracy)
	PartyCongress,      // Партийный съезд / пленум ЦК (Communism)
	LeaderCult,         // Вождизм / преемник по личному завещанию (Ethnonationalism)
	Kurultai,           // Великий курултай рода (Khaganate)
	ImperialCrown,      // Наследный имперский престол (Empire)
	FreeCircle          // Вольный круг — бессрочные выборы атамана жителями (FreeTerritory)
}
