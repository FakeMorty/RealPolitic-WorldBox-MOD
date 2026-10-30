using System;

namespace RealPolitic.Core;

/// <summary>
/// Правила, описания и соответствия законов преемственности.
/// </summary>
public static class SuccessionLaws
{
	public static SuccessionLaw GetDefaultLaw(IdeologyType ideology)
	{
		switch (ideology)
		{
		case IdeologyType.Monarchy:
			return SuccessionLaw.Salic;
		case IdeologyType.Tribalism:
			return SuccessionLaw.Tanistry;
		case IdeologyType.Theocracy:
			return SuccessionLaw.HolyConclave;
		case IdeologyType.Aristocracy:
			return SuccessionLaw.PatricianSenate;
		case IdeologyType.MerchantRepublic:
			return SuccessionLaw.Plutocracy;
		case IdeologyType.Militarism:
			return SuccessionLaw.Stratocracy;
		case IdeologyType.Egalitarianism:
			return SuccessionLaw.PopularAssembly;
		case IdeologyType.Technocracy:
			return SuccessionLaw.Meritocracy;
		case IdeologyType.Liberalism:
			return SuccessionLaw.ParliamentaryVote;
		case IdeologyType.Democracy:
			return SuccessionLaw.DemocraticElection;
		case IdeologyType.Communism:
			return SuccessionLaw.PartyCongress;
		case IdeologyType.Ethnonationalism:
			return SuccessionLaw.LeaderCult;
		case IdeologyType.Khaganate:
			return SuccessionLaw.Kurultai;
		case IdeologyType.Empire:
			return SuccessionLaw.ImperialCrown;
		case IdeologyType.FreeTerritory:
			return SuccessionLaw.FreeCircle;
		default:
			return SuccessionLaw.Salic;
		}
	}

	public static bool IsMonarchyLaw(SuccessionLaw law)
	{
		return law == SuccessionLaw.Salic
			|| law == SuccessionLaw.Cognatic
			|| law == SuccessionLaw.DynasticUnion
			|| law == SuccessionLaw.StrictAgnatic
			|| law == SuccessionLaw.ImperialCrown
			|| law == SuccessionLaw.Kurultai;
	}

	/// <summary>
	/// Имеет ли данный закон / строй автоматический 15-летний цикл смены/выборов власти:
	/// Демократия (Всеобщие выборы), Эгалитаризм (Народное вече), Торговая республика (Совет гильдий),
	/// Технократия (Совет академии), Либерализм (Парламент), Теократия (Конклав), Аристократия (Сенат).
	/// Коммунизм — партия правит без цикла (генсек пожизненно, смена — через съезд после смерти).
	/// </summary>
	public static bool HasPeriodicCycle(SuccessionLaw law)
	{
		return law == SuccessionLaw.DemocraticElection
			|| law == SuccessionLaw.PopularAssembly
			|| law == SuccessionLaw.Plutocracy
			|| law == SuccessionLaw.Meritocracy
			|| law == SuccessionLaw.ParliamentaryVote
			|| law == SuccessionLaw.HolyConclave
			|| law == SuccessionLaw.PatricianSenate;
	}

	/// <summary>
	/// Имеет ли идеология автоматический 15-летний цикл смены/выборов власти.
	/// </summary>
	public static bool HasPeriodicCycle(IdeologyType ideology)
	{
		return ideology == IdeologyType.Democracy
			|| ideology == IdeologyType.Egalitarianism
			|| ideology == IdeologyType.MerchantRepublic
			|| ideology == IdeologyType.Technocracy
			|| ideology == IdeologyType.Liberalism
			|| ideology == IdeologyType.Theocracy
			|| ideology == IdeologyType.Aristocracy;
	}

	public static string NameKey(SuccessionLaw law)
	{
		return "RP_SUCCESSION_" + law.ToString().ToUpperInvariant();
	}

	public static string DescKey(SuccessionLaw law)
	{
		return "RP_SUCCESSION_" + law.ToString().ToUpperInvariant() + "_DESC";
	}
}
