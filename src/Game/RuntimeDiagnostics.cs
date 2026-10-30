using System;
using NeoModLoader.services;
using UnityEngine;

namespace RealPolitic;

internal static class RuntimeDiagnostics
{
	private static bool _logged;

	public static void LogStartup()
	{
		if (_logged)
		{
			return;
		}
		_logged = true;
		try
		{
			MapBox world = World.world;
			if (world == null)
			{
				LogService.LogInfo("[Real Politic] диагностика: мир пока не создан; ожидание World.world");
				return;
			}
			string text = "?";
			try
			{
				if (world.map_stats != null)
				{
					text = world.map_stats.history_current_year.ToString();
				}
			}
			catch (Exception)
			{
				text = "ошибка чтения года";
			}
			int num = 0;
			int num2 = 0;
			try
			{
				num = ((world.kingdoms != null && ((CoreSystemManager<Kingdom, KingdomData>)(object)world.kingdoms).list != null) ? ((CoreSystemManager<Kingdom, KingdomData>)(object)world.kingdoms).list.Count : 0);
			}
			catch (Exception)
			{
				num = -1;
			}
			try
			{
				num2 = ((world.cities != null && ((CoreSystemManager<City, CityData>)(object)world.cities).list != null) ? ((CoreSystemManager<City, CityData>)(object)world.cities).list.Count : 0);
			}
			catch (Exception)
			{
				num2 = -1;
			}
			LogService.LogInfo("[Real Politic] диагностика: WorldBox API доступен; год=" + text + ", государств=" + num + ", городов=" + num2);
		}
		catch (Exception ex4)
		{
			LogService.LogError("[Real Politic] диагностика запуска не удалась: " + ex4.Message);
		}
	}
}
