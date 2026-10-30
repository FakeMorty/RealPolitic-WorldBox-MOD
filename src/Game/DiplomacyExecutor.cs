using System;
using System.Collections.Generic;
using System.Reflection;
using NeoModLoader.services;
using RealPolitic.Core;

namespace RealPolitic;

/// <summary>
/// Этап 6: реальные дипломатические действия. Исполняет ход советника
/// (война, мир, союз) через безопасную рефлексию над игровым API WorldBox 0.51.2.
///
/// Проверенные сигнатуры игры (см. game_decomp):
///   War startWar(Kingdom, Kingdom, WarTypeAsset, bool pLog = true)   — DiplomacyManager
///   War getWar(Kingdom, Kingdom, bool pOnlyMain = true)              — WarManager
///   void endWar(War, WarWinner pWinner = WarWinner.Nobody)           — WarManager
///   bool forceAlliance(Kingdom, Kingdom)                             — AllianceManager
///   WarTypeAsset WarTypeLibrary.normal                               — статическое поле
///
/// Каждый вызов обёрнут: при несовпадении сигнатуры функция логируется и
/// пропускается, игра не падает.
/// </summary>
public static class DiplomacyExecutor
{
	private static readonly HashSet<string> _warned = new HashSet<string>();

	private static void Warn(string what)
	{
		if (_warned.Add(what))
		{
			LogService.LogError("[Real Politic] дипломатический API недоступен: " + what);
		}
	}

	/// <summary>Выполняет ход. Возвращает true, если действие применено к игре.</summary>
	public static bool Execute(DiplomaticMove move, Kingdom actor, Kingdom target)
	{
		if (move == null || actor == null || target == null)
		{
			return false;
		}
		if (ReferenceEquals(actor, target))
		{
			return false;
		}
		if (move.Action == DiplomaticAction.DeclareWar && !GameRefs.IsDiplomacyWorldLawEnabled())
		{
			return false;
		}
		try
		{
			switch (move.Action)
			{
				case DiplomaticAction.DeclareWar:
					return DeclareWar(actor, target);
				case DiplomaticAction.ProposeAlliance:
					return FormAlliance(actor, target);
				case DiplomaticAction.MakePeace:
					return MakePeace(actor, target);
				// Вассалитет — этап 7 (унии и федерации).
				default:
					return false;
			}
		}
		catch (Exception ex)
		{
			Warn(move.Action.ToString() + ": " + ex.Message);
			return false;
		}
	}

	/// <summary>Объявить войну (публично — для гражданских войн и подавления мятежей).</summary>
	public static bool TryDeclareWar(Kingdom actor, Kingdom target)
	{
		return TryStartVerifiedWar(actor, target, rebellion: false);
	}

	/// <summary>
	/// Начинает войну и проверяет результат по реестру войн, а не только по
	/// возвращённому объекту. Если стороны всё ещё числятся в одном нативном
	/// альянсе, нападающая сторона сначала выходит из него: в 700-летнем
	/// прогоне именно это мешало восстановленному вассалу реально воевать.
	/// </summary>
	public static bool TryStartVerifiedWar(Kingdom actor, Kingdom target, bool rebellion = false)
	{
		try
		{
			if (actor == null || target == null || ReferenceEquals(actor, target))
			{
				return false;
			}
			if (AreAtWar(actor, target))
			{
				return true;
			}
			// 0.39: во время съезда КМУД действует запрет войн — мод не объявляет
			// войну сам (и не даёт объявить игре, см. KmudWarBan).
			if (!KmudWarBan.AllowStart(actor, target))
			{
				return false;
			}
			if (AllianceSafety.Same(actor, target) && !TryLeaveAlliance(actor))
			{
				return false;
			}
			if (AreAtWar(actor, target))
			{
				return true;
			}

			// Война засчитывается только по реестру игры, а не по тому, что вызов «не упал».
			// WarApiProbe перебирает ВСЕ перегрузки startWar/newWar, подставляет аргументы
			// по типам и пишет в журнал, что вернула каждая из них.
			if (WarApiProbe.EnsureWar(actor, target, rebellion ? "rebellion" : "normal", out string verdict))
			{
				GameWars.PollImmediate();
				return true;
			}
			if (rebellion && WarApiProbe.EnsureWar(actor, target, "normal", out verdict))
			{
				GameWars.PollImmediate();
				return true;
			}
			Warn("война #" + (int)((NanoObject)actor).id + " → #" + (int)((NanoObject)target).id
				+ " не подтверждена игрой: " + verdict);
			return false;
		}
		catch (Exception ex)
		{
			Warn("TryStartVerifiedWar: " + ex.Message);
			return false;
		}
	}

	/// <summary>Добровольный выход державы из нативного альянса через проверенный Alliance.leave.</summary>
	public static bool TryLeaveAlliance(Kingdom kingdom)
	{
		try
		{
			if (kingdom == null || !AllianceSafety.TryMembership(kingdom, out object alliance))
			{
				return false;
			}
			if (alliance == null)
			{
				return true;
			}
			if (!GameRefs.TryInvoke(alliance, "leave", out object result, kingdom, true)
				|| result is bool accepted && !accepted)
			{
				return false;
			}
			object alliances = InstanceField(World.world, "alliances");
			if (alliances != null)
			{
				Invoke(alliances, "checkLists");
			}
			return AllianceSafety.TryMembership(kingdom, out object current) && current == null;
		}
		catch (Exception ex)
		{
			Warn("TryLeaveAlliance: " + ex.Message);
			return false;
		}
	}

	/// <summary>Заключить мир (публично — для отделения по итогам гражданской войны).</summary>
	public static bool TryMakePeace(Kingdom actor, Kingdom target)
	{
		try
		{
			return actor != null && target != null && MakePeace(actor, target);
		}
		catch (Exception ex)
		{
			Warn("TryMakePeace: " + ex.Message);
			return false;
		}
	}

	/// <summary>Есть ли между двумя государствами война (по реестру войн игры).</summary>
	public static bool AreAtWar(Kingdom a, Kingdom b)
	{
		try
		{
			if (a == null || b == null || ReferenceEquals(a, b))
			{
				return false;
			}
			if (GameRefs.Invoke(a, "isInWarWith", b) is bool inWar && inWar)
			{
				return true;
			}
			if (GameRefs.Invoke(a, "isAtWarWith", b) is bool atWar && atWar)
			{
				return true;
			}
			object wars = InstanceField(World.world, "wars");
			if (wars == null)
			{
				return false;
			}
			return Invoke(wars, "getWar", a, b, true) != null
				|| Invoke(wars, "getWar", a, b, false) != null;
		}
		catch
		{
			return false;
		}
	}

	private static bool DeclareWar(Kingdom actor, Kingdom target)
	{
		// Совет объявляет войну тем же проверенным путём: выход из общего альянса,
		// вызов игры, подтверждение по реестру. Раньше здесь возвращался «успех»
		// при любом ненулевом ответе, даже если войны в игре не появилось.
		return TryStartVerifiedWar(actor, target, rebellion: false);
	}

	private static object StartWar(Kingdom actor, Kingdom target, string assetName)
	{
		object asset = StaticField("WarTypeLibrary", assetName);
		if (asset == null)
		{
			Warn("WarTypeLibrary." + assetName);
			return null;
		}
		object diplomacy = InstanceField(World.world, "diplomacy");
		if (diplomacy == null)
		{
			Warn("MapBox.diplomacy");
			return null;
		}
		if (!GameRefs.TryInvoke(diplomacy, "startWar", out object started, actor, target, asset, true))
		{
			Warn("DiplomacyManager.startWar: " + (GameRefs.LastInvokeError ?? "unknown error"));
			return null;
		}
		return started;
	}

	private static bool MakePeace(Kingdom actor, Kingdom target)
	{
		object wars = InstanceField(World.world, "wars");
		if (wars == null)
		{
			Warn("MapBox.wars");
			return false;
		}
		object war = Invoke(wars, "getWar", actor, target, true);
		if (war == null)
		{
			// Войны между этими государствами нет (или они не главные участники) — нечего завершать.
			return false;
		}
		Type winnerType = Patches.FindGameType("WarWinner");
		object nobody = (winnerType == null) ? 0 : Enum.ToObject(winnerType, 0);
		Invoke(wars, "endWar", war, nobody);
		return true;
	}

	/// <summary>
	/// 0.32: завершить ВСЕ идущие войны белым миром (без победителя).
	/// Нужно для созыва КМУД: конференция начинается с прекращения огня на всей карте.
	/// Возвращает число войн, которые удалось погасить.
	/// </summary>
	public static int EndAllWars()
	{
		int ended = 0;
		try
		{
			object wars = InstanceField(World.world, "wars");
			if (wars == null)
			{
				Warn("MapBox.wars");
				return 0;
			}
			object rawList = Invoke(wars, "getList") ?? InstanceField(wars, "list");
			System.Collections.IList list = rawList as System.Collections.IList;
			if (list == null)
			{
				return 0;
			}
			// Список меняется при завершении войны — идём с конца и снимком.
			List<object> snapshot = new List<object>();
			for (int i = 0; i < list.Count; i++)
			{
				if (list[i] != null)
				{
					snapshot.Add(list[i]);
				}
			}
			Type winnerType = Patches.FindGameType("WarWinner");
			object nobody = (winnerType == null) ? 0 : Enum.ToObject(winnerType, 0);
			for (int i = 0; i < snapshot.Count; i++)
			{
				object war = snapshot[i];
				if (war == null)
				{
					continue;
				}
				if (GameRefs.Invoke(war, "hasEnded") is bool done && done)
				{
					continue;
				}
				if (GameRefs.TryInvoke(wars, "endWar", out _ , war, nobody))
				{
					ended++;
				}
			}
		}
		catch (Exception ex)
		{
			Warn("EndAllWars: " + ex.Message);
		}
		return ended;
	}

	private static bool FormAlliance(Kingdom actor, Kingdom target)
	{
        var world=RealPoliticMod.World;
        if(world?.Governance.Enabled==true)
        {
            if(!world.Governance.Recognition.CanContact((int)((NanoObject)actor).id,(int)((NanoObject)target).id,InternationalContact.Alliance))return false;
            if(!CivicAllianceRuntime.ApprovedPair(actor,target))
            {CivicAllianceRuntime.RequestAdmission(world,actor,target,RealPoliticMod.CurrentYear());return false;}
        }
		object alliances = InstanceField(World.world, "alliances");
		if (alliances == null)
		{
			Warn("MapBox.alliances");
			return false;
		}
        if (!AllianceSafety.CanJoin(actor, target)) return false;
        object result;
        NativeAllianceGuards.BeginModAllianceCall();
        try
        {
            result = Invoke(alliances, "forceAlliance", actor, target);
        }
        finally
        {
            NativeAllianceGuards.EndModAllianceCall();
        }
        Invoke(alliances, "checkLists");
        // In WorldBox 0.51.2, forceAlliance returns bool tNew (false when joining an existing alliance via Alliance.join).
        return result != null && AllianceSafety.Same(actor, target);
	}

	/// <summary>
	/// Союз в обход гражданских согласований — для решений КМУД.
	///
	/// 0.38: обычный <see cref="ForceAlliance"/> при включённом гражданском слое не создаёт союз,
	/// а лишь подаёт заявку на голосование (RequestAdmission) и возвращает false. Из-за этого
	/// съезд оставался без общего альянса: в логе было «альянс не создался — игра не дала
	/// свести державы» при живых державах. Решение конференции — не заявка, его не согласуют:
	/// мир уже проголосовал на съезде.
	/// </summary>
	public static bool ForceAllianceDirect(Kingdom actor, Kingdom target)
	{
		try
		{
			object alliances = InstanceField(World.world, "alliances");
			if (alliances == null)
			{
				Warn("MapBox.alliances");
				return false;
			}
			if (!AllianceSafety.CanJoin(actor, target))
			{
				return false;
			}
			// 0.47.1: помечаем вызов как «свой»: наш же запрет на ванильные союзы
			// не должен рубить альянс КМУД и пакты после вердикта.
			object result;
			NativeAllianceGuards.BeginModAllianceCall();
			try
			{
				result = Invoke(alliances, "forceAlliance", actor, target);
			}
			finally
			{
				NativeAllianceGuards.EndModAllianceCall();
			}
			Invoke(alliances, "checkLists");
			return result != null && AllianceSafety.Same(actor, target);
		}
		catch (Exception ex)
		{
			Warn("ForceAllianceDirect: " + ex.Message);
			return false;
		}
	}

	/// <summary>Отражение унии в игре как видимого союза (принудительный альянс).</summary>
	public static bool ForceAlliance(Kingdom actor, Kingdom target)
	{
		try
		{
			return FormAlliance(actor, target);
		}
		catch (Exception ex)
		{
			Warn("ForceAlliance: " + ex.Message);
			return false;
		}
	}

	/// <summary>
	/// Отделение города от метрополии при кризисе / восстании (сепарация).
	/// Вызывает нативный механизм WorldBox makeOwnKingdom / newKingdom и объявляет войну за независимость.
	/// </summary>
	public static bool TrySecedeCity(Kingdom kingdom, City city, out Kingdom rebelKingdom)
	{
		return TrySecedeCity(kingdom, city, out rebelKingdom, declareWar: true);
	}

	/// <summary>Выделить город в новую державу без немедленного объявления войны этим
	/// методом. Для вооружённого освобождения войну объявляет вызывающий слой сразу
	/// после отделения; мирное восстановление уничтоженной державы этим путём запрещено.</summary>
	public static bool TryCarveKingdom(Kingdom kingdom, City city, out Kingdom newKingdom)
	{
		return TrySecedeCity(kingdom, city, out newKingdom, declareWar: false);
	}

	/// <summary>Вооружённое отделение: нативный мятеж создаётся сразу с pRebellion=true.</summary>
	public static bool TryCarveKingdomForWar(Kingdom kingdom, City city, out Kingdom newKingdom)
	{
		return TrySecedeCity(kingdom, city, out newKingdom, declareWar: true);
	}

	private static bool TrySecedeCity(Kingdom kingdom, City city, out Kingdom rebelKingdom, bool declareWar)
	{
		rebelKingdom = null;
		if (kingdom == null || city == null)
		{
			return false;
		}
        try
        {
            var political=RealPoliticMod.World;
            if(political?.Governance.Enabled==true)
            {
                long raw=((NanoObject)kingdom).id;if(raw<=0 || raw>int.MaxValue)return false;
                if(!political.Governance.Recognition.ContainsCountry((int)raw))
                    GovernanceRuntime.ObserveBirth(political,kingdom,political.GetKingdom((int)raw)?.FoundedYear??RealPoliticMod.CurrentYear());
                if(!political.Governance.Recognition.ContainsCountry((int)raw))return false;
            }
			object km = InstanceField(World.world, "kingdoms");
			object leader = GameRefs.ReadProperty(city, "leader");
			if (leader == null)
			{
				// Не-столицы в игре живут без лидера, а makeNewCivKingdom(null) падает
				// с NullReference внутри игры — в 0.17.1/0.17.2 из-за этого сецессия тихо
				// проваливалась («игровой API отделения недоступен»). Поступаем как
				// ванильный мятеж: ставим старшего жителя города лидером.
				Actor resident = GameRefs.FindAdultResident(city);
				if (resident != null)
				{
					GameRefs.Invoke(city, "setLeader", resident, true);
					leader = resident;
				}
			}
			if (leader == null)
			{
				// Пустой город отделиться не может — честно сообщаем отказ.
				Warn("TrySecedeCity: в городе " + (GameRefs.ReadProperty(city, "name") ?? "?") + " нет взрослого жителя для роли лидера");
				return false;
			}

			// WorldBox 0.51: public Kingdom makeOwnKingdom(Actor pActor, bool pRebellion = false, bool pFellApart = false).
			// Зовём с точным числом аргументов и обязательным лидером
			// (рефлексионный Invoke матчит по количеству параметров).
			// Как в ванильном мятеже: будущий король должен числиться в отделяющемся городе.
			if (leader is Actor leaderActor && !ReferenceEquals(GameRefs.ReadProperty(leaderActor, "city"), city))
			{
				GameRefs.Invoke(leaderActor, "joinCity", city);
			}
			GameRefs.LastInvokeError = null;
			// 0.23.2: сигнатура makeOwnKingdom менялась от версии к версии — подбираем
			// аргументы под фактическую сигнатуру (Actor → лидер, bool → pRebellion=true, прочее — default).
			object res = InvokeAdaptive(city, "makeOwnKingdom", leader, declareWar);
			if (res == null && km != null && ReferenceEquals(city.kingdom, kingdom))
			{
				// Ручной путь (как в самой игре): вывести город из державы и основать новую вокруг лидера.
				res = ManualSecession(km, kingdom, city, leader);
			}

			// Проверяем, изменилось ли государство у города:
			if (city.kingdom != null && !ReferenceEquals(city.kingdom, kingdom))
			{
				rebelKingdom = city.kingdom;
			}
			else if (res is Kingdom k && !ReferenceEquals(k, kingdom))
			{
				rebelKingdom = k;
			}

			if (rebelKingdom != null)
			{
				if (km != null)
				{
					Invoke(km, "checkLists");
				}
				if (rebelKingdom.cities != null && !rebelKingdom.cities.Contains(city))
				{
					GameRefs.Invoke(rebelKingdom, "listCity", city);
					if (rebelKingdom.cities != null && !rebelKingdom.cities.Contains(city))
					{
						rebelKingdom.cities.Add(city);
					}
				}
				if (kingdom.cities != null && kingdom.cities.Contains(city))
				{
					kingdom.cities.Remove(city);
				}
                GovernanceRuntime.ObserveBirth(RealPoliticMod.World,rebelKingdom,RealPoliticMod.CurrentYear(),(int)((NanoObject)kingdom).id,!declareWar);
				if (!declareWar)
				{
					return true;
				}
				// Начинаем войну за независимость именно со стороны отделившейся державы.
				if (!GameRefs.IsDiplomacyWorldLawEnabled())
				{
					Warn("отделение состоялось без подтверждённой войны: дипломатия отключена законом мира");
				}
				else if (!TryStartVerifiedWar(rebelKingdom, kingdom, rebellion: true))
				{
					Warn("отделение состоялось, но война за независимость не подтверждена игровым API");
				}
				return true;
			}
			Warn("TrySecedeCity: makeOwnKingdom не создал государство (" + (GameRefs.LastInvokeError ?? "метод не найден или вернул null") + ")");
		}
		catch (Exception ex)
		{
			Warn("TrySecedeCity: " + ex.Message);
		}
		return false;
	}

	/// <summary>Зовёт метод по имени, подгоняя аргументы под фактическую сигнатуру.
	/// Логирует найденные сигнатуры, если вызов не удался — чтобы диагностика в Player.log была предметной.</summary>
	private static object InvokeAdaptive(object target, string name, object leader, bool rebellion=true)
	{
		if (target == null)
		{
			return null;
		}
		MethodInfo[] methods = target.GetType().GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
		string signatures = string.Empty;
		foreach (MethodInfo method in methods)
		{
			if (method.Name != name)
			{
				continue;
			}
			ParameterInfo[] parameters = method.GetParameters();
			object[] args = new object[parameters.Length];
			bool firstBool = true;
			for (int i = 0; i < parameters.Length; i++)
			{
				Type pt = parameters[i].ParameterType;
				if (leader != null && pt.IsInstanceOfType(leader))
				{
					args[i] = leader;
				}
				else if (pt == typeof(bool))
				{
					args[i] = firstBool && rebellion; // lawful exit must not request pRebellion=true
					firstBool = false;
				}
				else if (parameters[i].HasDefaultValue)
				{
					args[i] = parameters[i].DefaultValue;
				}
				else
				{
					args[i] = pt.IsValueType ? Activator.CreateInstance(pt) : null;
				}
				signatures += (i == 0 ? name + "(" : ", ") + pt.Name;
			}
			signatures += ") ";
			try
			{
				object result = method.Invoke(target, args);
				if (result != null)
				{
					return result;
				}
			}
			catch (Exception ex)
			{
				GameRefs.LastInvokeError = name + ": " + (ex.InnerException?.Message ?? ex.Message);
			}
		}
		if (string.IsNullOrEmpty(GameRefs.LastInvokeError))
		{
			GameRefs.LastInvokeError = string.IsNullOrEmpty(signatures) ? "метод " + name + " не найден" : "перегрузки вернули null: " + signatures;
		}
		return null;
	}

	/// <summary>Ручное отделение города: повторяет шаги City.makeOwnKingdom игры (0.51).</summary>
	private static object ManualSecession(object km, Kingdom oldKingdom, City city, object leader)
	{
		try
		{
			object newKingdom = Invoke(km, "makeNewCivKingdom", leader);
			if (newKingdom == null)
			{
				return null;
			}
			GameRefs.Invoke(city, "removeFromCurrentKingdom");
			GameRefs.Invoke(city, "setKingdom", newKingdom);
			GameRefs.Invoke(city, "switchedKingdom");
			GameRefs.Invoke(newKingdom, "copyMetasFromOtherKingdom", oldKingdom);
			GameRefs.Invoke(newKingdom, "setCityMetas", city);
			// Жители города переходят в новую державу.
			object units = GameRefs.ReadMember(city, "units");
			if (units is System.Collections.IEnumerable list)
			{
				List<object> copy = new List<object>();
				foreach (object u in list)
				{
					copy.Add(u);
				}
				for (int i = 0; i < copy.Count; i++)
				{
					GameRefs.Invoke(copy[i], "setKingdom", newKingdom);
				}
			}
			return newKingdom;
		}
		catch (Exception ex)
		{
			GameRefs.LastInvokeError = "ручное отделение: " + (ex.InnerException?.Message ?? ex.Message);
			return null;
		}
	}

	private static object InstanceField(object target, string name)
	{
		if (target == null)
		{
			return null;
		}
		FieldInfo field = target.GetType().GetField(name, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
		return field?.GetValue(target);
	}

	private static object StaticField(string typeName, string name)
	{
		Type type = Patches.FindGameType(typeName);
		if (type == null)
		{
			return null;
		}
		FieldInfo field = type.GetField(name, BindingFlags.Static | BindingFlags.Public);
		return field?.GetValue(null);
	}

	private static object Invoke(object target, string name, params object[] args)
	{
		if (target == null)
		{
			return null;
		}
		// Ищем метод по имени и числу параметров — устойчивее, чем точное совпадение сигнатур.
		MethodInfo[] methods = target.GetType().GetMethods(BindingFlags.Instance | BindingFlags.Public);
		foreach (MethodInfo method in methods)
		{
			if (method.Name != name)
			{
				continue;
			}
			ParameterInfo[] parameters = method.GetParameters();
			if (parameters.Length != args.Length)
			{
				continue;
			}
			return method.Invoke(target, args);
		}
		return null;
	}
}
