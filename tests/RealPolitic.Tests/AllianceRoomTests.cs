using System;
using RealPolitic.Core;
using Xunit;

namespace RealPolitic.Tests;

/// <summary>
/// 0.47.1: «в мире всегда должно оставаться место для войны».
///
/// Владелец 29.10.2026: «вроде было правило, что нельзя делать так, чтобы 2 последние
/// державы были в одном альянсе. Всегда нужно место для войны».
///
/// Если в союз входят все живые державы, воевать становится не с кем: войны исчезают,
/// а вместе с ними засыпают КМУД, переговоры и вся политика мода. Поэтому правило
/// простое: в альянсе всегда меньше держав, чем есть на карте.
/// </summary>
public class AllianceRoomTests
{
	[Fact]
	public void LastTwoRealmsNeverShareOneAlliance()
	{
		Assert.Equal(0, AllianceRoom.MaxMembers(2));
		Assert.False(AllianceRoom.LeavesRoomForWar(2, 2));
		Assert.True(AllianceRoom.ForbiddenForLastTwo(2, 1, 1));
	}

	[Fact]
	public void SingleRealmHasNobodyToAllyWith()
	{
		Assert.Equal(0, AllianceRoom.MaxMembers(1));
		Assert.False(AllianceRoom.CanAddMember(1, 0));
	}

	[Fact]
	public void WithThreeRealmsOnlyTwoMayAlly()
	{
		Assert.Equal(2, AllianceRoom.MaxMembers(3));
		Assert.True(AllianceRoom.CanAddMember(3, 1));
		Assert.False(AllianceRoom.CanAddMember(3, 2));
	}

	[Fact]
	public void AlwaysAtLeastOneRealmStaysOut()
	{
		for (int countries = 1; countries <= 40; countries++)
		{
			int cap = AllianceRoom.MaxMembers(countries);
			Assert.True(cap < countries, "при " + countries + " державах предел союза " + cap);
			Assert.True(cap >= 0);
		}
	}

	[Fact]
	public void BigWorldsStillGetRealAlliances()
	{
		// Ограничение не должно убить союзы совсем: в большом мире альянс из четырёх-пяти держав есть.
		Assert.True(AllianceRoom.MaxMembers(8) >= 4);
		Assert.True(AllianceRoom.MaxMembers(30) >= 5);
	}

	[Fact]
	public void RefusalExplainsItselfForTheLog()
	{
		Assert.Contains("две державы", AllianceRoom.RefusalReason(2));
		Assert.Contains("одна", AllianceRoom.RefusalReason(1));
		Assert.Contains("союзе", AllianceRoom.RefusalReason(10));
	}

	[Fact]
	public void JoiningAnAllianceOfTwoInAWorldOfThreeIsAllowedButThirdIsNot()
	{
		// Три державы: союз из двух — можно, третьей туда уже нельзя.
		Assert.True(AllianceRoom.LeavesRoomForWar(3, 2));
		Assert.False(AllianceRoom.LeavesRoomForWar(3, 3));
	}
}
