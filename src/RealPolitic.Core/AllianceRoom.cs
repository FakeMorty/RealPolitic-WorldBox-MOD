using System;

namespace RealPolitic.Core;

/// <summary>
/// 0.47.1: в мире всегда должно оставаться место для войны.
///
/// Владелец 29.10.2026: «вроде было правило, что нельзя делать так, чтобы 2 последние
/// державы были в одном альянсе. Всегда нужно место для войны».
///
/// Правило простое и проверяется без игры: сколько бы держав ни жило на карте,
/// в один союз не могут войти ВСЕ. Хотя бы одна держава обязана остаться вне альянса —
/// иначе мир превращается в одну страну без политики: не с кем воевать, некому
/// заключать мир, и весь мод (войны, КМУД, переговоры) просто засыпает.
///
/// Отсюда:
///   • 3 державы — союз максимум из 2;
///   • 2 державы — союз невозможен вообще (0 или 1 участник — это не союз);
///   • 1 держава — союзов нет, воевать ей не с кем.
/// </summary>
public static class AllianceRoom
{
    /// <summary>
    /// Сколько участников может быть в одном альянсе при данном числе живых держав.
    /// Всегда строго меньше числа держав: одна остаётся «на воле».
    /// </summary>
    public static int MaxMembers(int countriesWithLand)
    {
        if (countriesWithLand <= 2)
        {
            // Две последние державы в один союз не входят — иначе войны больше не будет никогда.
            return 0;
        }
        if (countriesWithLand <= 4)
        {
            return 2;
        }
        if (countriesWithLand <= 10)
        {
            return Math.Min(4, countriesWithLand - 1);
        }
        return 5;
    }

    /// <summary>
    /// Останется ли место для войны, если в альянсе станет <paramref name="membersAfterJoin"/> участников.
    /// </summary>
    public static bool LeavesRoomForWar(int countriesWithLand, int membersAfterJoin)
    {
        return countriesWithLand >= 1 && membersAfterJoin < countriesWithLand;
    }

    /// <summary>Можно ли добавить ещё одного участника в союз текущего размера.</summary>
    public static bool CanAddMember(int countriesWithLand, int currentMembers)
    {
        return LeavesRoomForWar(countriesWithLand, currentMembers + 1)
            && currentMembers + 1 <= MaxMembers(countriesWithLand);
    }

    /// <summary>
    /// Особый случай, из-за которого правило и появилось: держав осталось ровно две,
    /// и их хотят свести в один союз. Так нельзя — иначе в мире не останется ни одной
    /// возможной пары для войны.
    /// </summary>
    public static bool ForbiddenForLastTwo(int countriesWithLand, int currentMembers, int joinedMembers)
    {
        return countriesWithLand <= 2 && currentMembers + joinedMembers >= countriesWithLand;
    }

    /// <summary>Готовая причина отказа — чтобы она попадала в журнал, а не терялась.</summary>
    public static string RefusalReason(int countriesWithLand)
    {
        if (countriesWithLand <= 1)
        {
            return "в мире одна держава — союз заключать не с кем";
        }
        if (countriesWithLand == 2)
        {
            return "в мире осталось две державы — им нельзя быть в одном союзе, иначе войн больше не будет";
        }
        return "в союзе уже " + (countriesWithLand - 1) + " из " + countriesWithLand
            + " держав — одной нужно остаться вне союза, иначе воевать будет не с кем";
    }
}
