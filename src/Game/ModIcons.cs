using System.Linq;
using System;
using System.Collections.Generic;
using System.Reflection;
using NeoModLoader.services;
using RealPolitic.Core;

namespace RealPolitic;

/// <summary>
/// Единый список картинок интерфейса мода. Ключ — имя файла без расширения
/// в папках icons/ и icons/64/; путь — имя, под которым картинка кладётся
/// в спрайт-кэш WorldBox. Раньше эти строки были разбросаны по окнам,
/// из-за чего кнопка голосований ссылалась на отсутствующий файл и чужой
/// путь конституции, а кнопка слоя идеологий использовала корону монархии.
/// </summary>
public static class ModIcons
{
	public const string ConstitutionKey = "tab_constitution";
	public const string VotesKey = "tab_civic_votes";
	public const string KmudKey = "tab_kmud";

	public const string FamilyKey = "tab_family";
	public const string IdeologyLayerKey = "tab_ideology_layer";
	public const string AdmitKey = "button_admit";
	public const string ExpelKey = "button_expel";

	// 0.31.5: отдельный, специально сгенерированный набор уведомлений. Он не
	// переиспользует ни иконки идеологий, ни значки вкладок и кнопок.
	public const string NotifyWarKey = "notify_war";
	public const string NotifyPeaceKey = "notify_peace";
	public const string NotifyVoteKey = "notify_vote";
	public const string NotifyLawKey = "notify_law";
	public const string NotifyDynastyKey = "notify_dynasty";
	public const string NotifyJoinKey = "notify_join";
	public const string NotifyLeaveKey = "notify_leave";
	public const string NotifyTradeKey = "notify_trade";
	public const string NotifySpyKey = "notify_spy";
	public const string NotifyMapKey = "notify_map";

	// 0.47: значки для событий 0.46 и пятого вопроса КМУД (нарисованы нейросетью,
	// в том же стиле, что и прежний набор: тёмно-синий квадрат, золото и багрянец).
	public const string NotifyMigrationKey = "notify_migration";
	public const string NotifyCityKey = "notify_city";
	public const string NotifyRaceThreatKey = "notify_race_threat";
	public const string NotifyRaceShieldKey = "notify_race_shield";
	public const string NotifyElectionKey = "notify_election";
	public const string NotifyRepatriationKey = "notify_repatriation";

	/// <summary>0.31.6: значки всех 15 строев для уведомлений (ключ = notify_ideology_&lt;строй&gt;).</summary>
	public static readonly string[] NotifyIdeologyKeys =
	{
		"notify_ideology_tribalism",
		"notify_ideology_monarchy",
		"notify_ideology_theocracy",
		"notify_ideology_aristocracy",
		"notify_ideology_merchantrepublic",
		"notify_ideology_militarism",
		"notify_ideology_egalitarianism",
		"notify_ideology_technocracy",
		"notify_ideology_liberalism",
		"notify_ideology_democracy",
		"notify_ideology_communism",
		"notify_ideology_ethnonationalism",
		"notify_ideology_khaganate",
		"notify_ideology_empire",
		"notify_ideology_freeterritory",
		// 0.49: шестнадцатый строй — национализм.
		"notify_ideology_nationalism"
	};

	/// <summary>Все неидеологические иконки кнопок и вкладок.</summary>
	public static readonly string[] UiKeys =
	{
		ConstitutionKey,
		VotesKey,
		FamilyKey,
		KmudKey,
		IdeologyLayerKey,
		AdmitKey,
		ExpelKey
	};

	/// <summary>Сгенерированные иконки всплывающих уведомлений и истории мира.</summary>
	public static readonly string[] NotificationKeys = new[]
	{
		NotifyWarKey,
		NotifyPeaceKey,
		NotifyVoteKey,
		NotifyLawKey,
		NotifyDynastyKey,
		NotifyJoinKey,
		NotifyLeaveKey,
		NotifyTradeKey,
		NotifySpyKey,
		NotifyMapKey,
		NotifyMigrationKey,
		NotifyCityKey,
		NotifyRaceThreatKey,
		NotifyRaceShieldKey,
		NotifyElectionKey,
		NotifyRepatriationKey
	}.Concat(NotifyIdeologyKeys).ToArray();

	/// <summary>Полный путь спрайта в кэше игры: ui/Icons/rp_&lt;ключ&gt;.</summary>
	public static string PathFor(string key)
	{
		return "ui/Icons/rp_" + key;
	}

	/// <summary>Короткое имя для заголовка окна NeoModLoader (часть пути после ui/Icons/).</summary>
	public static string WindowIconFor(string key)
	{
		return "rp_" + key;
	}

	/// <summary>Файловые ключи всех идеологий и всех кнопок — для регистрации и тестов комплектности.</summary>
	public static string[] AllFileKeys()
	{
		var keys = new List<string>();
		Array types = Enum.GetValues(typeof(IdeologyType));
		for (int i = 0; i < types.Length; i++)
		{
			keys.Add(types.GetValue(i).ToString().ToLowerInvariant());
		}
		keys.AddRange(UiKeys);
		keys.AddRange(NotificationKeys);
		return keys.ToArray();
	}

	/// <summary>Заранее кладёт иконки кнопок в спрайт-кэш, чтобы первая открытая вкладка не оказалась пустой.</summary>
	public static void RegisterUiIcons()
	{
		int verified = 0;
		for (int i = 0; i < UiKeys.Length; i++)
		{
			if (IdeologySprites.EnsureRegistered(UiKeys[i], PathFor(UiKeys[i])))
			{
				verified++;
			}
			else
			{
				LogService.LogError("[Real Politic] иконка кнопки не найдена или не загружена: " + UiKeys[i]);
			}
		}
		LogService.LogInfo("[Real Politic] иконки кнопок: спрайтов в кэше " + verified + "/" + UiKeys.Length);
	}

	/// <summary>Заранее кладёт сгенерированные значки уведомлений в спрайт-кэш игры.</summary>
	public static void RegisterNotificationIcons()
	{
		int verified = 0;
		for (int i = 0; i < NotificationKeys.Length; i++)
		{
			if (IdeologySprites.EnsureRegistered(NotificationKeys[i], PathFor(NotificationKeys[i])))
			{
				verified++;
			}
			else
			{
				LogService.LogError("[Real Politic] иконка уведомления не найдена или не загружена: " + NotificationKeys[i]);
			}
		}
		LogService.LogInfo("[Real Politic] иконки уведомлений: спрайтов в кэше " + verified + "/" + NotificationKeys.Length);
	}

	/// <summary>Регистрирует иконку при необходимости и возвращает готовый спрайт игры либо null.</summary>
	public static object GetSprite(string key)
	{
		try
		{
			if (string.IsNullOrEmpty(key) || !IdeologySprites.EnsureRegistered(key, PathFor(key)))
			{
				return null;
			}
			Type loader = Patches.FindGameType("SpriteTextureLoader");
			MethodInfo getSprite = loader?.GetMethod("getSprite", BindingFlags.Static | BindingFlags.Public);
			return getSprite?.Invoke(null, new object[1] { PathFor(key) });
		}
		catch (Exception)
		{
			return null;
		}
	}
}
