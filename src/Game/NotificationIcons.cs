using System;
using System.Collections.Generic;
using RealPolitic.Core;

namespace RealPolitic;

/// <summary>
/// Выбирает собственную иконку всплывающего уведомления по смыслу события.
/// С 0.31.5 здесь используется только специально сгенерированный набор
/// notify_*. Иконки идеологий и кнопок интерфейса для уведомлений больше
/// не переиспользуются. Отдельные значки каждого строя будут добавлены
/// следующим сгенерированным набором; до него события смены строя честно
/// получают новую политическую карту, а не старую готовую картинку.
/// </summary>
public static class NotificationIcons
{
	private const string IdeologyPathPrefix = "ui/Icons/rp_ideology_";
	private const string UiPathPrefix = "ui/Icons/rp_";

	private static readonly HashSet<string> RegisteredPaths = new HashSet<string>(StringComparer.Ordinal);

	/// <summary>Новая политическая карта для общемировых и прочих событий без более точного символа.</summary>
	public static string DefaultPath => ModIcons.PathFor(ModIcons.NotifyMapKey);

	/// <summary>
	/// Путь спрайта для события. <paramref name="kingdomIdeology"/> сохранён в
	/// сигнатуре для следующего набора значков каждого строя; в первом
	/// сгенерированном наборе смена строя показывается политической картой.
	/// </summary>
	public static string PathFor(PolEvent evt, IdeologyType? kingdomIdeology = null)
	{
		if (evt == null)
		{
			return DefaultPath;
		}

		// У предложения по альянсу действие записано параметром: приём и исключение
		// должны отличаться уже по иконке, а не только текстом уведомления.
		if (evt.Type == PolEventType.CivicAllianceMotion)
		{
			string action = evt.Get("action") ?? string.Empty;
			if (action.IndexOf("EXPULSION", StringComparison.OrdinalIgnoreCase) >= 0)
			{
				return UiPath(ModIcons.NotifyLeaveKey);
			}
			if (action.IndexOf("ADMISSION", StringComparison.OrdinalIgnoreCase) >= 0)
			{
				return UiPath(ModIcons.NotifyJoinKey);
			}
			return UiPath(ModIcons.NotifyVoteKey);
		}

		switch (evt.Type)
		{
			// Смена или распространение строя. До отдельного сгенерированного
			// набора каждого строя не подставляем старые иконки интерфейса.
			case PolEventType.KingdomAdoptedIdeology:
			case PolEventType.RevolutionSucceeded:
				return IdeologyPathOrDefault(IdeologyFromParams(evt, "new", "ideology") ?? kingdomIdeology);

			case PolEventType.ActorConverted:
			case PolEventType.CityIdeologyShift:
			case PolEventType.KingdomReformed:
			case PolEventType.RevolutionStarted:
			case PolEventType.RevolutionFailed:
			case PolEventType.IdeologyPowerUsed:
			case PolEventType.IdeologyPowerEnded:
			case PolEventType.IdeologyThreatBroke:
			case PolEventType.IdeologyThreatRecovered:
			case PolEventType.LegitimacyCrisis:
				return IdeologyPathOrDefault(IdeologyFromParams(evt, "ideology", "new") ?? kingdomIdeology);

			case PolEventType.KingdomSeceded:
				return UiPath(ModIcons.NotifyLeaveKey);

			// Война и вооружённые столкновения.
			case PolEventType.WarStarted:
			case PolEventType.CivilWarStarted:
			case PolEventType.CivilWarEnded:
			case PolEventType.CasusBelliDeclared:
			case PolEventType.CoalitionWar:
			case PolEventType.VassalRebellion:
			case PolEventType.PowerRaid:
			case PolEventType.TruceBroken:
			case PolEventType.NationalLiberationUprising:
			case PolEventType.EthnonationalistTension:
				return UiPath(ModIcons.NotifyWarKey);

			// Завершение войны и мирные урегулирования.
			case PolEventType.WarEnded:
			case PolEventType.PeaceSettlement:
			case PolEventType.WhitePeace:
			case PolEventType.PeaceConferenceConcluded:
				return UiPath(ModIcons.NotifyPeaceKey);

			// Выборы, референдумы, парламент и прочие голосования.
			case PolEventType.DemocraticElection:
			case PolEventType.ElectionDeferred:
			case PolEventType.CivicElectionDeferred:
			case PolEventType.WarReferendum:
			case PolEventType.Impeached:
			case PolEventType.NationalRecognitionPosition:
			case PolEventType.InternationalRecognitionDecided:
			case PolEventType.RegionalCivicProcedure:
			case PolEventType.NationalAlliancePosition:
			case PolEventType.CivicUltimatumResolved:
			case PolEventType.ParliamentaryShift:
			case PolEventType.BillRejected:
			case PolEventType.ChambersDisagree:
				return UiPath(ModIcons.NotifyVoteKey);

			// Законы, петиции и договоры.
			case PolEventType.ConstitutionEdited:
			case PolEventType.BillPassed:
			case PolEventType.RulerVetoed:
			case PolEventType.VetoOverridden:
			case PolEventType.ConstitutionalUsurpation:
			case PolEventType.UnconstitutionalAct:
			case PolEventType.TreatySigned:
			case PolEventType.TreatyBroken:
			case PolEventType.TreatyExpired:
			case PolEventType.TruceExpired:
			case PolEventType.UnionInstitutionGained:
			case PolEventType.CivicPetitionSubmitted:
				return UiPath(ModIcons.NotifyLawKey);

			// Правитель, наследование, регентство и династия.
			case PolEventType.RulerDeath:
			case PolEventType.Succession:
			case PolEventType.RegencyStarted:
			case PolEventType.RegencyEnded:
			case PolEventType.RegencyCrowned:
			case PolEventType.RegencyPassed:
			case PolEventType.DynasticUnionFormed:
			case PolEventType.DynasticMarriage:
			case PolEventType.DynasticShift:
			case PolEventType.DynasticExtinction:
			case PolEventType.DynastyRenamed:
			case PolEventType.UnionCrownInherited:
			case PolEventType.UnionSuccessionCrisis:
			case PolEventType.RoyalRelativeSeceded:
			case PolEventType.LastResidentDied:
				return UiPath(ModIcons.NotifyDynastyKey);

			// Создание объединений, вступление, интеграция и открытые границы.
			case PolEventType.UnionFormed:
			case PolEventType.UnionMemberJoined:
			case PolEventType.UnionMerged:
			case PolEventType.UnionReformed:
			case PolEventType.AllianceFormed:
			case PolEventType.AllianceMemberJoined:
			case PolEventType.CoalitionFormed:
			case PolEventType.IntegrationStarted:
			case PolEventType.IntegrationCompleted:
			case PolEventType.BorderOpened:
			case PolEventType.KingdomRestored:
				return UiPath(ModIcons.NotifyJoinKey);

			// Распад объединений, выход, отделение и закрытые границы.
			case PolEventType.UnionDissolved:
			case PolEventType.UnionMemberLeft:
			case PolEventType.AllianceBroken:
			case PolEventType.AllianceMemberLeft:
			case PolEventType.CoalitionDissolved:
			case PolEventType.CitySeceded:
			case PolEventType.KingdomCollapsed:
			case PolEventType.KingdomDisappeared:
			case PolEventType.BorderClosed:
			case PolEventType.BorderExpulsion:
				return UiPath(ModIcons.NotifyLeaveKey);

			// Торговля и экономика.
			case PolEventType.TradeRouteOpened:
			case PolEventType.TradeRouteUpgraded:
			case PolEventType.TradeRouteBroken:
			case PolEventType.TradeRoutesSevered:
			case PolEventType.TradeEmbargo:
			case PolEventType.EconomicCrisisStarted:
			case PolEventType.EconomicCrisisEnded:
			case PolEventType.EconomicBoom:
				return UiPath(ModIcons.NotifyTradeKey);

			// 0.52: народы, язык и местные уставы.
			case PolEventType.PeopleLanguageShift:
				return UiPath(ModIcons.NotifyMapKey);

			case PolEventType.LocalConstitutionAdopted:
				return UiPath(ModIcons.NotifyLawKey);

			case PolEventType.LocalConstitutionBanned:
				return UiPath(ModIcons.NotifyLeaveKey);

			case PolEventType.LocalConstitutionConflict:
				return UiPath(ModIcons.NotifyVoteKey);

			case PolEventType.LocalConstitutionRevolt:
				return UiPath(ModIcons.NotifyWarKey);

			case PolEventType.ForeignRulerWithoutLaw:
				return UiPath(ModIcons.NotifyDynastyKey);

			// 0.49–0.50: национализм. Приход к власти — тревога (значок войны),
			// возврат строя — восстановление, реакция мира — закон и порядок.
			case PolEventType.NationalismSeizedPower:
			case PolEventType.RaceNationalismSeizedPower:
				return UiPath(ModIcons.NotifyWarKey);

			case PolEventType.NationalismPowerReturned:
			case PolEventType.RaceNationalismPowerReturned:
				return UiPath(ModIcons.NotifyJoinKey);

			case PolEventType.WorldOutrageSanctions:
				return UiPath(ModIcons.NotifyTradeKey);

			case PolEventType.WorldOutrageStatusLost:
				return UiPath(ModIcons.NotifyLeaveKey);

			// 0.47: события переселения, расселения и малых народов.
			case PolEventType.MigrationResettled:
				// Побег — тревожнее обычного переезда, но значок тот же: это про дорогу.
				return UiPath(ModIcons.NotifyMigrationKey);

			case PolEventType.CityFounded:
				return UiPath(ModIcons.NotifyCityKey);

			case PolEventType.RaceEndangered:
				return UiPath(ModIcons.NotifyRaceThreatKey);

			case PolEventType.RaceProtected:
				return UiPath(ModIcons.NotifyRaceShieldKey);

			case PolEventType.ParliamentElected:
				return UiPath(ModIcons.NotifyElectionKey);

			// Пятый вопрос КМУД и его исполнение — значок с общим знаменем и охраной.
			case PolEventType.KmudRepatriation:
				return UiPath(ModIcons.NotifyRepatriationKey);

			case PolEventType.EspionageExposed:
			case PolEventType.EspionageSuccess:
				return UiPath(ModIcons.NotifySpyKey);

			default:
				return DefaultPath;
		}
	}

	/// <summary>
	/// Файловый ключ PNG по пути в спрайт-кэше игры. Например,
	/// ui/Icons/rp_notify_war -> notify_war.
	/// </summary>
	public static string FileKeyForPath(string spritePath)
	{
		if (string.IsNullOrEmpty(spritePath))
		{
			return string.Empty;
		}
		if (spritePath.StartsWith(IdeologyPathPrefix, StringComparison.Ordinal))
		{
			return spritePath.Substring(IdeologyPathPrefix.Length);
		}
		if (spritePath.StartsWith(UiPathPrefix, StringComparison.Ordinal))
		{
			return spritePath.Substring(UiPathPrefix.Length);
		}
		return string.Empty;
	}

	/// <summary>
	/// Страховочная догрузка выбранного спрайта. При обычном старте все
	/// сгенерированные картинки регистрируются заранее, поэтому вызов просто
	/// находит готовый спрайт в кэше.
	/// </summary>
	public static bool EnsureSpriteRegistered(string spritePath)
	{
		if (string.IsNullOrEmpty(spritePath) || RegisteredPaths.Contains(spritePath))
		{
			return RegisteredPaths.Contains(spritePath);
		}

		string key = FileKeyForPath(spritePath);
		if (string.IsNullOrEmpty(key))
		{
			return false;
		}

		bool registered = IdeologySprites.EnsureRegistered(key, spritePath);
		if (registered)
		{
			RegisteredPaths.Add(spritePath);
		}
		return registered;
	}

	private static string UiPath(string key)
	{
		return ModIcons.PathFor(key);
	}

	private static string IdeologyPathOrDefault(IdeologyType? type)
	{
		// Только сгенерированные значки notify_ideology_*. Строй без своего значка
		// получает политическую карту, а не старую готовую картинку.
		if (!type.HasValue)
		{
			return DefaultPath;
		}
		string key = "notify_ideology_" + type.Value.ToString().ToLowerInvariant();
		return Array.IndexOf(ModIcons.NotifyIdeologyKeys, key) >= 0 ? UiPath(key) : DefaultPath;
	}

	private static IdeologyType? IdeologyFromParams(PolEvent evt, params string[] keys)
	{
		for (int i = 0; i < keys.Length; i++)
		{
			IdeologyType? parsed = ParseIdeology(evt.Get(keys[i]));
			if (parsed.HasValue)
			{
				return parsed;
			}
		}
		return null;
	}

	private static IdeologyType? ParseIdeology(string value)
	{
		if (string.IsNullOrWhiteSpace(value))
		{
			return null;
		}

		string candidate = value.Trim();
		const string prefix = "RP_IDEOLOGY_";
		if (candidate.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
		{
			candidate = candidate.Substring(prefix.Length);
		}
		if (candidate.EndsWith("_DESC", StringComparison.OrdinalIgnoreCase))
		{
			candidate = candidate.Substring(0, candidate.Length - 5);
		}
		candidate = candidate.Replace("_", string.Empty).Replace("-", string.Empty).Replace(" ", string.Empty);

		Array types = Enum.GetValues(typeof(IdeologyType));
		for (int i = 0; i < types.Length; i++)
		{
			IdeologyType type = (IdeologyType)types.GetValue(i);
			if (string.Equals(candidate, type.ToString(), StringComparison.OrdinalIgnoreCase))
			{
				return type;
			}
		}
		return null;
	}
}
