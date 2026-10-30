using System;
using System.Collections.Generic;
using System.Globalization;
using NeoModLoader.api;
using RealPolitic.Core;

namespace RealPolitic;

internal static class ConfigReader
{
	public static SimParams ReadParams(ModConfig config)
	{
		SimParams simParams = new SimParams();
		simParams.RevolutionBaseChance = ReadFloat(config, "RevolutionChance", simParams.RevolutionBaseChance);
		simParams.RevolutionDuration = (int)ReadFloat(config, "RevolutionYears", simParams.RevolutionDuration);
		simParams.SpreadFromState = ReadFloat(config, "IdeologySpread", simParams.SpreadFromState);
		simParams.SpreadFromNeighbor = ReadFloat(config, "ForeignInfluence", simParams.SpreadFromNeighbor);
		simParams.DissentRate = ReadFloat(config, "DissentRate", simParams.DissentRate);
		// Гражданские войны и расколы (этап 17).
		if (!ReadBool(config, "EnableCivilWars", fallback: true))
		{
			// Нулевой базовый шанс — мятежи не начинаются, уже идущие доживают до развязки.
			simParams.CivilWarBaseChance = 0f;
		}
		else
		{
			simParams.CivilWarBaseChance = ReadFloat(config, "CivilWarChance", simParams.CivilWarBaseChance);
		}
		simParams.AllowSecession = ReadBool(config, "EnableSecessions", fallback: true);
        simParams.RegionalObedienceThreshold=Math.Max(1,Math.Min(99,ReadInt(config,"RegionalObediencePercent",45)))/100f;
        simParams.RegionalLowYears=Math.Max(1,Math.Min(20,ReadInt(config,"RegionalLowYears",3)));
        simParams.RegionalRetryYears=Math.Max(0,Math.Min(100,ReadInt(config,"RegionalRetryYears",10)));
		simParams.CivilWarDurationYears = (int)ReadFloat(config, "CivilWarYears", simParams.CivilWarDurationYears);
		// Экономика и торговля (этап 18).
		simParams.EnableEconomy = ReadBool(config, "EnableEconomy", fallback: true);
		simParams.EnableTrade = ReadBool(config, "EnableTrade", fallback: true);
		simParams.TradeBaseProfit = ReadFloat(config, "TradeProfit", simParams.TradeBaseProfit);
		simParams.MaxTradeRoutes = (int)ReadFloat(config, "TradeRoutes", simParams.MaxTradeRoutes);
		simParams.RecessionChance = ReadFloat(config, "RecessionChance", simParams.RecessionChance);
		// Династии и личности правителей (этап 19).
		simParams.EnableDynasties = ReadBool(config, "EnableDynasties", fallback: true);
		simParams.MarriageBaseChance = ReadFloat(config, "MarriageChance", simParams.MarriageBaseChance);
		simParams.MarriageMinAge = (int)ReadFloat(config, "MarriageMinAge", simParams.MarriageMinAge);
		// Глубокая дипломатия (этап 20).
		simParams.EnableTreaties = ReadBool(config, "EnableTreaties", fallback: true);
		simParams.TreatyYears = (int)ReadFloat(config, "TreatyYears", simParams.TreatyYears);
		simParams.EnableCasusBelli = ReadBool(config, "EnableCasusBelli", fallback: true);
		simParams.EnableEspionage = ReadBool(config, "EnableEspionage", fallback: true);
		simParams.EnableCoalitions = ReadBool(config, "EnableCoalitions", fallback: true);
		simParams.VassalTributeRate = ReadFloat(config, "VassalTributeRate", simParams.VassalTributeRate);
		simParams.VassalRebellionApproval = ReadFloat(config, "VassalRebellionApproval", simParams.VassalRebellionApproval);
		// Альянсы игры и унии (этап 21).
		simParams.TrackAlliances = ReadBool(config, "TrackAlliances", fallback: true);
		// Этап 22: война и мирные договоры.
		simParams.EnableWarPeace = ReadBool(config, "EnableWarPeace", fallback: true);
		simParams.TruceYears = (int)ReadFloat(config, "TruceYears", simParams.TruceYears);
		simParams.MaxCededCities = (int)ReadFloat(config, "MaxCededCities", simParams.MaxCededCities);
		simParams.ReparationsYears = (int)ReadFloat(config, "ReparationsYears", simParams.ReparationsYears);
		simParams.EnableRestoration = ReadBool(config, "EnableRestoration", fallback: true);
		// RestorationVictorKeeps / RestorationReparationsYears: legacy-ключи старых
		// мирных восстановлений. Читаем только для совместимости пользовательских
		// конфигов; с 0.31.2 механика их не использует.
		simParams.RestorationVictorKeeps = (int)ReadFloat(config, "RestorationVictorKeeps", simParams.RestorationVictorKeeps);
		simParams.RestorationReparationsYears = (int)ReadFloat(config, "RestorationReparationsYears", simParams.RestorationReparationsYears);
		// Этап 23.
		simParams.EnableRealRegency = ReadBool(config, "EnableRealRegency", fallback: true);
		simParams.EnableBorders = ReadBool(config, "EnableBorders", fallback: true);
		simParams.BorderLockYears = (int)ReadFloat(config, "BorderLockYears", simParams.BorderLockYears);
		simParams.BordersClosedByDefault = ReadBool(config, "BordersClosedByDefault", simParams.BordersClosedByDefault);
		simParams.BorderOpeningTalksYears = (int)ReadFloat(config, "BorderOpeningTalksYears", simParams.BorderOpeningTalksYears);
		BordersBook.DefaultStatus = simParams.BordersClosedByDefault ? BorderStatus.Closed : BorderStatus.Open;
		simParams.EnablePowerRaids = ReadBool(config, "EnablePowerRaids", fallback: true);
		simParams.EnableDynastySurnames = ReadBool(config, "EnableDynastySurnames", fallback: true);
		simParams.BorderIntruderDamage = ReadFloat(config, "BorderIntruderDamage", simParams.BorderIntruderDamage);
		// Этап 24 (исправления восприятия мира): эпохи строя.
		simParams.EnableEraRestrictions = ReadBool(config, "EnableEraRestrictions", fallback: true);
		simParams.EraMedievalYear = (int)ReadFloat(config, "EraMedievalYear", simParams.EraMedievalYear);
		simParams.EraEarlyModernYear = (int)ReadFloat(config, "EraEarlyModernYear", simParams.EraEarlyModernYear);
		simParams.EraModernYear = (int)ReadFloat(config, "EraModernYear", simParams.EraModernYear);
		// Этап 26: писаные конституции.
		simParams.EnableConstitution = ReadBool(config, "EnableConstitution", fallback: true);
		simParams.ReferendumCooldownYears = (int)ReadFloat(config, "ReferendumCooldownYears", simParams.ReferendumCooldownYears);
		simParams.RulerAmendmentYears = (int)ReadFloat(config, "RulerAmendmentYears", simParams.RulerAmendmentYears);
		// 0.32: КМУД — всемирная мирная конференция.
		simParams.EnableKmud = ReadBool(config, "KmudEnabled", fallback: true);
		simParams.KmudYear = Math.Max(1, (int)ReadFloat(config, "KmudYear", simParams.KmudYear));
		simParams.KmudYears = Math.Max(1, (int)ReadFloat(config, "KmudYears", simParams.KmudYears));
		simParams.KmudPauseYears = Math.Max(1, (int)ReadFloat(config, "KmudPauseYears", simParams.KmudPauseYears));
		simParams.KmudIntervalYears = Math.Max(1, (int)ReadFloat(config, "KmudIntervalYears", simParams.KmudIntervalYears));
		simParams.KmudPactYears = Math.Max(1, (int)ReadFloat(config, "KmudPactYears", simParams.KmudPactYears));
		// 0.46: расселение, переселение и борьба малых народов за сохранение.
		simParams.EnableSettlementExpansion = ReadBool(config, "EnableSettlementExpansion", fallback: true);
		simParams.EnableMigration = ReadBool(config, "EnableMigration", fallback: true);
		simParams.MigrationPerYear = Math.Max(0, (int)ReadFloat(config, "MigrationPerYear", simParams.MigrationPerYear));
		simParams.EnableRaceSurvival = ReadBool(config, "EnableRaceSurvival", fallback: true);
		return simParams;
	}

	public static Severity ReadSeverity(ModConfig config, string id, Severity fallback)
	{
		int num = (int)ReadFloat(config, id, (float)fallback);
		if (num < 0)
		{
			num = 0;
		}
		if (num > 3)
		{
			num = 3;
		}
		return (Severity)num;
	}

	public static bool ReadBool(ModConfig config, string id, bool fallback)
	{
		try
		{
			object raw = GetRaw(config, id);
			if (raw == null)
			{
				return fallback;
			}
			if (raw is bool result)
			{
				return result;
			}
			if (raw is float num)
			{
				return num > 0.5f;
			}
			if (raw is string text)
			{
				return text == "true" || text == "1";
			}
			return fallback;
		}
		catch (Exception)
		{
			return fallback;
		}
	}

	public static int ReadInt(ModConfig config, string id, int fallback)
	{
		try
		{
			object raw = GetRaw(config, id);
			if (raw == null)
			{
				return fallback;
			}
			if (raw is int result)
			{
				return result;
			}
			if (raw is long num)
			{
				return (int)num;
			}
			if (raw is float num2)
			{
				return (int)num2;
			}
			if (raw is double num3)
			{
				return (int)num3;
			}
			if (raw is string s && int.TryParse(s, NumberStyles.Integer, CultureInfo.InvariantCulture, out var result2))
			{
				return result2;
			}
			return fallback;
		}
		catch (Exception)
		{
			return fallback;
		}
	}

	public static float ReadFloat(ModConfig config, string id, float fallback)
	{
		try
		{
			object raw = GetRaw(config, id);
			if (raw == null)
			{
				return fallback;
			}
			if (raw is float result)
			{
				return result;
			}
			if (raw is double num)
			{
				return (float)num;
			}
			if (raw is int num2)
			{
				return num2;
			}
			if (raw is string s && float.TryParse(s, NumberStyles.Float, CultureInfo.InvariantCulture, out var result2))
			{
				return result2;
			}
			return fallback;
		}
		catch (Exception)
		{
			return fallback;
		}
	}

	private static object GetRaw(ModConfig config, string id)
	{
		Dictionary<string, ModConfigItem> dictionary = config["General"];
		if (dictionary == null)
		{
			return null;
		}
		if (!dictionary.TryGetValue(id, out var value) || value == null)
		{
			return null;
		}
		return value.GetValue();
	}
}
