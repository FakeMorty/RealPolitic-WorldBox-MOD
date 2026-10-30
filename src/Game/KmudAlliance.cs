using System;
using System.Collections.Generic;
using RealPolitic.Core;

namespace RealPolitic;

/// <summary>
/// 0.32, этап 2: общий альянс КМУД в игре.
///
/// Игра считает альянс боевым блоком, поэтому действуем осторожно и проверяем результат:
///   1. берём любую державу с землёй и создаём вокруг неё альянс штатным
///      AllianceManager.forceAlliance (тот же путь, что уже используется для уний);
///   2. остальные державы вводим принудительно — Alliance.join(kingdom, recalc, force:true),
///      а из старого альянса выводим Alliance.leave;
///   3. переименовываем альянс в «КМУД»;
///   4. каждый год проверяем состав: кто вышел — возвращаем; при закрытии — распускаем.
///
/// Все вызовы — через рефлексию и в try/catch: если игра поменяет имена, мод не упадёт,
/// а просто напишет в лог, что альянс создать не удалось (конференция при этом работает).
/// </summary>
public static class KmudAlliance
{
	/// <summary>0.48: номер съезда, о котором уже написали «альянс не создаётся».</summary>
	private static int _lastNoAllianceRound = -1;

	/// <summary>Название альянса, как его увидит игрок в родном окне игры.</summary>
	public const string AllianceName = "КМУД — Конференция Мирного Урегулирования Дел";

	private static bool _warned;

	/// <summary>
	/// Собирает всех держав с землёй в один альянс и запоминает его номер.
	/// Возвращает число держав в альянсе (0 — не удалось).
	/// </summary>
	public static int FormAll(PoliticalWorld world, MapBox map)
	{
		if (world == null || map == null)
		{
			return 0;
		}
		try
		{
			List<Kingdom> kingdoms = KingdomsWithLand(map);
			if (kingdoms.Count < 2)
			{
				return 0;
			}
			// 0.47.1: правило владельца «в мире всегда нужно место для войны».
			// Если держав всего две, общий альянс КМУД свёл бы в союз весь мир —
			// воевать стало бы не с кем. Тогда съезд идёт без общего альянса.
			if (kingdoms.Count < 3)
			{
				// 0.48: одна строка на весь съезд, а не каждый год (раньше журнал
				// забивался одинаковыми сообщениями 10 лет подряд).
				if (_lastNoAllianceRound != world.Kmud.Round)
				{
					_lastNoAllianceRound = world.Kmud.Round;
					NLog("[Real Politic] КМУД: общий альянс не создаётся — в мире лишь " + kingdoms.Count
						+ " держав(ы), а одной всегда нужно оставаться вне союза");
				}
				return 0;
			}
			// 0.37: игра не даёт державе быть в двух альянсах сразу. Пока в мире есть чужие
			// союзы, часть держав просто не может войти в КМУД (в логе получалось «4 из 8»).
			// Поэтому перед сбором распускаем прочие альянсы: на время съезда весь мир
			// должен сидеть за одним столом.
			int released = ReleaseForeignAlliances(kingdoms);
			if (released > 0)
			{
				NLog("[Real Politic] КМУД: ради общего альянса распущено чужих союзов: " + released);
			}
			// Основа альянса — первая держава и первая, с которой её можно свести.
			Kingdom anchor = kingdoms[0];
			for (int i = 1; i < kingdoms.Count; i++)
			{
				if (!DiplomacyExecutor.ForceAllianceDirect(anchor, kingdoms[i]))
				{
					NLog("[Real Politic] КМУД: союз не сложился между «" + ((NanoObject)anchor).name
						+ "» и «" + ((NanoObject)kingdoms[i]).name + "»");
				}
				if (AllianceSafety.TryMembership(anchor, out object formed) && formed != null)
				{
					break;
				}
			}
			object alliance = FindAllianceOf(anchor);
			if (alliance == null)
			{
				// Не глушим лог: без альянса конференция всё равно идёт, но игрок должен
				// видеть в логе, почему за столом нет общего союза (0.38).
				Warn("КМУД: альянс не создался — игра не дала свести державы");
				return 0;
			}
			// Принудительно вводим остальных.
			int inside = 0;
			for (int i = 0; i < kingdoms.Count; i++)
			{
				Kingdom kingdom = kingdoms[i];
				if (AddTo(kingdom, alliance))
				{
					inside++;
				}
			}
			if (inside < kingdoms.Count)
			{
				string missed = string.Empty;
				for (int i = 0; i < kingdoms.Count; i++)
				{
					if (!AllianceSafety.TryMembership(kingdoms[i], out object current) || !ReferenceEquals(current, alliance))
					{
						missed += (missed.Length > 0 ? ", " : string.Empty) + ((NanoObject)kingdoms[i]).name;
					}
				}
				NLog("[Real Politic] КМУД: в общий альянс не вошли: " + missed);
			}
			Rename(alliance, AllianceName);
			world.Kmud.AllianceId = AllianceSafety.StableId(alliance);
			world.Kmud.AllianceFormed = true;
			NLog("[Real Politic] КМУД: общий альянс собран, держав внутри: " + inside + " из " + kingdoms.Count
				+ ", номер альянса: " + world.Kmud.AllianceId);
			return inside;
		}
		catch (Exception ex)
		{
			Warn("КМУД FormAll: " + ex.Message);
			return 0;
		}
	}

	/// <summary>
	/// Годовое обслуживание альянса: пока конференция идёт, державы, вышедшие из альянса,
	/// возвращаются. Игра может выкидывать участников сама — это не должно ломать конференцию.
	/// </summary>
	public static void MaintainAlliance(PoliticalWorld world, MapBox map)
	{
		if (world?.Kmud == null || !world.Kmud.IsConvened || map == null)
		{
			return;
		}
		try
		{
			List<Kingdom> kingdoms = KingdomsWithLand(map);
			if (kingdoms.Count < 2)
			{
				return;
			}
			object alliance = world.Kmud.AllianceId > 0
				? FindAllianceById(world.Kmud.AllianceId)
				: FindAllianceOf(kingdoms[0]);
			if (alliance == null)
			{
				// Альянс распался сам — собираем заново.
				int made = FormAll(world, map);
				if (made > 0)
				{
					world.Bus.Publish(new PolEvent(PolEventType.KmudAllianceFormed, "RP32_EV_KMUD_ALLIANCE") { Year = world.Year }
						.With("members", made)
						.With("name", AllianceName));
				}
				return;
			}
			int returned = 0;
			for (int i = 0; i < kingdoms.Count; i++)
			{
				Kingdom kingdom = kingdoms[i];
				if (AllianceSafety.TryMembership(kingdom, out object current) && ReferenceEquals(current, alliance))
				{
					continue;
				}
				if (AddTo(kingdom, alliance))
				{
					returned++;
				}
			}
			if (returned > 0)
			{
				world.Kmud.MembersReturned += returned;
				NLog("[Real Politic] КМУД: возвращено в альянс держав: " + returned);
			}
		}
		catch (Exception ex)
		{
			Warn("КМУД MaintainAlliance: " + ex.Message);
		}
	}

	/// <summary>Распускает альянс КМУД (при закрытии конференции).</summary>
	public static bool Dissolve(PoliticalWorld world)
	{
		if (world?.Kmud == null)
		{
			return false;
		}
		try
		{
			// 0.37: держав сводят в союз по частям, поэтому в мире могло остаться несколько
			// альянсов с именем КМУД. Раньше распускался только записанный номер, а прочие
			// продолжали жить — в летописи 211–243 гг. альянс «КМУД» всё ещё вступался за державы.
			int dissolved = 0;
			bool ok = true;
			List<object> targets = AlliancesNamedKmd(world.Kmud.AllianceId);
			for (int i = 0; i < targets.Count; i++)
			{
				if (GameRefs.TryInvoke(targets[i], "dissolve", out _))
				{
					dissolved++;
				}
				else
				{
					ok = false;
				}
			}
			object manager = GameRefs.ReadMember(World.world, "alliances");
			if (manager != null)
			{
				GameRefs.TryInvoke(manager, "checkLists", out _);
			}
			world.Kmud.AllianceId = 0;
			world.Kmud.AllianceFormed = false;
			if (dissolved == 0 && !ok)
			{
				Warn("КМУД: альянс не удалось распустить (Alliance.dissolve недоступен)");
				return false;
			}
			NLog("[Real Politic] КМУД: альянс распущен (союзов закрыто: " + dissolved + ")");
			return dissolved > 0;
		}
		catch (Exception ex)
		{
			Warn("КМУД Dissolve: " + ex.Message);
			return false;
		}
	}

	/// <summary>
	/// Распускает все альянсы, где есть наши державы, если это не тот самый союз КМУД.
	/// Нужно, чтобы на съезд попали все: игра не разрешает державе два альянса.
	/// </summary>
	private static int ReleaseForeignAlliances(List<Kingdom> kingdoms)
	{
		int released = 0;
		HashSet<object> seen = new HashSet<object>();
		for (int i = 0; i < kingdoms.Count; i++)
		{
			if (!AllianceSafety.TryMembership(kingdoms[i], out object alliance) || alliance == null)
			{
				continue;
			}
			if (IsKmudAlliance(alliance) || !seen.Add(alliance))
			{
				continue;
			}
			if (GameRefs.TryInvoke(alliance, "dissolve", out _))
			{
				released++;
			}
		}
		if (released > 0)
		{
			object manager = GameRefs.ReadMember(World.world, "alliances");
			if (manager != null)
			{
				GameRefs.TryInvoke(manager, "checkLists", out _);
			}
		}
		return released;
	}

	/// <summary>Альянс с нашим названием (или с записанным номером).</summary>
	private static bool IsKmudAlliance(object alliance)
	{
		return alliance != null && AllianceName.Equals(GameRefs.ReadMember(alliance, "name") as string);
	}

	/// <summary>Все альянсы КМУД в мире: и записанный по номеру, и названные нашим именем.</summary>
	private static List<object> AlliancesNamedKmd(int recordedId)
	{
		List<object> result = new List<object>();
		try
		{
			object manager = GameRefs.ReadMember(World.world, "alliances");
			object listRaw = manager == null ? null : GameRefs.ReadMember(manager, "list");
			System.Collections.IList list = listRaw as System.Collections.IList;
			if (list == null)
			{
				return result;
			}
			for (int i = 0; i < list.Count; i++)
			{
				object alliance = list[i];
				if (alliance == null)
				{
					continue;
				}
				bool ours = IsKmudAlliance(alliance)
					|| (recordedId > 0 && AllianceSafety.StableId(alliance) == recordedId);
				if (ours)
				{
					result.Add(alliance);
				}
			}
		}
		catch (Exception)
		{
		}
		return result;
	}

	// ------------------------------------------------------------------
	// внутреннее
	// ------------------------------------------------------------------

	private static List<Kingdom> KingdomsWithLand(MapBox map)
	{
		List<Kingdom> result = new List<Kingdom>();
		List<Kingdom> list = GameRefs.KingdomList(map);
		if (list == null)
		{
			return result;
		}
		for (int i = 0; i < list.Count; i++)
		{
			Kingdom kingdom = list[i];
			if (kingdom == null)
			{
				continue;
			}
			List<City> cities = GameRefs.KingdomCities(kingdom);
			if (cities != null && cities.Count > 0)
			{
				result.Add(kingdom);
			}
		}
		return result;
	}

	/// <summary>Вводит державу в альянс принудительно, выводя её из прежнего.</summary>
	private static bool AddTo(Kingdom kingdom, object alliance)
	{
		if (kingdom == null || alliance == null)
		{
			return false;
		}
		try
		{
			if (AllianceSafety.TryMembership(kingdom, out object current) && ReferenceEquals(current, alliance))
			{
				return true;
			}
			// join(Kingdom, bool recalc, bool force) — force:true снимает обычные ограничения игры.
			if (!GameRefs.TryInvoke(alliance, "join", out _, kingdom, true, true))
			{
				GameRefs.TryInvoke(alliance, "join", out _, kingdom, true);
			}
			// Старое членство могло остаться — выводим из прежнего альянса явно.
			if (current != null && !ReferenceEquals(current, alliance))
			{
				GameRefs.TryInvoke(current, "leave", out _, kingdom, true);
			}
			return AllianceSafety.TryMembership(kingdom, out object now) && ReferenceEquals(now, alliance);
		}
		catch (Exception ex)
		{
			Warn("КМУД AddTo: " + ex.Message);
			return false;
		}
	}

	private static object FindAllianceOf(Kingdom kingdom)
	{
		if (AllianceSafety.TryMembership(kingdom, out object alliance))
		{
			return alliance;
		}
		return null;
	}

	private static object FindAllianceById(int id)
	{
		try
		{
			object manager = GameRefs.ReadMember(World.world, "alliances");
			object listRaw = manager == null ? null : GameRefs.ReadMember(manager, "list");
			System.Collections.IList list = listRaw as System.Collections.IList;
			if (list == null)
			{
				return null;
			}
			for (int i = 0; i < list.Count; i++)
			{
				object alliance = list[i];
				if (alliance != null && AllianceSafety.StableId(alliance) == id)
				{
					return alliance;
				}
			}
		}
		catch (Exception)
		{
		}
		return null;
	}

	private static void Rename(object alliance, string name)
	{
		if (GameRefs.TryInvoke(alliance, "setName", out _, name, true))
		{
			return;
		}
		try
		{
			GameRefs.WriteMember(alliance, "name", name);
		}
		catch (Exception)
		{
		}
	}

	private static void Warn(string message)
	{
		if (_warned)
		{
			return;
		}
		_warned = true;
		NLog("[Real Politic] " + message);
	}

	private static void NLog(string message)
	{
		try
		{
			NeoModLoader.services.LogService.LogInfo(message);
		}
		catch (Exception)
		{
		}
	}
}
