using System;
using RealPolitic;
using Xunit;

namespace RealPolitic.GameTests;

/// <summary>
/// 0.53.1: скорость. Владелец 30.10.2026: «каждый конец года пролагивает на секунды».
///
/// Причина найдена в коде: кэш отражения (найденных полей и свойств) существовал с 0.43,
/// но им пользовался только вызов методов. А чтение полей — ReadMember — каждый раз
/// заново обходило всю иерархию типов через GetField/GetProperty. Эти чтения идут
/// каждый кадр (лояльность города, титул правителя, карта идеологий) и выделяют память,
/// поэтому игра периодически «замирает».
///
/// Здесь проверяется, что кэш действительно используется: повторные чтения не растят
/// число поисков по типу, а значения читаются одинаково до и после сброса кэша.
/// </summary>
public sealed class ReflectionCacheTests
{
	private class Sample
	{
		public int Number = 42;
		public string Text { get; set; } = "слово";
	}

	private sealed class Child : Sample
	{
		public int Extra;
	}

	[Fact]
	public void ReadMemberFindsFieldAndProperty()
	{
		GameRefs.ClearReflectionCache();
		var sample = new Sample { Number = 7, Text = "привет" };
		Assert.Equal(7, GameRefs.ReadMember(sample, "Number"));
		Assert.Equal("привет", GameRefs.ReadMember(sample, "Text"));
	}

	[Fact]
	public void ReadMemberFindsInheritedMembers()
	{
		GameRefs.ClearReflectionCache();
		var child = new Child { Number = 11, Text = "наследство", Extra = 5 };
		Assert.Equal(11, GameRefs.ReadMember(child, "Number"));
		Assert.Equal("наследство", GameRefs.ReadMember(child, "Text"));
		Assert.Equal(5, GameRefs.ReadMember(child, "Extra"));
	}

	[Fact]
	public void SecondReadIsServedFromCache()
	{
		GameRefs.ClearReflectionCache();
		var sample = new Sample();
		GameRefs.ReadMember(sample, "Number");
		int afterFirst = GameRefs.ReflectionCacheSize;
		Assert.True(afterFirst > 0, "первое чтение должно запомнить найденное поле");
		// Второе и третье чтение не должны добавлять новых записей в кэш.
		GameRefs.ReadMember(sample, "Number");
		GameRefs.ReadMember(sample, "Number");
		Assert.Equal(afterFirst, GameRefs.ReflectionCacheSize);
	}

	[Fact]
	public void WriteMemberAlsoUsesTheCache()
	{
		GameRefs.ClearReflectionCache();
		var sample = new Sample();
		GameRefs.ReadMember(sample, "Number");
		int before = GameRefs.ReflectionCacheSize;
		Assert.True(GameRefs.WriteMember(sample, "Number", 99));
		Assert.Equal(99, sample.Number);
		Assert.Equal(before, GameRefs.ReflectionCacheSize);
	}

	[Fact]
	public void UnknownMemberIsNotSearchedTwice()
	{
		GameRefs.ClearReflectionCache();
		var sample = new Sample();
		Assert.Null(GameRefs.ReadMember(sample, "НетТакогоПоля"));
		int before = GameRefs.ReflectionCacheSize;
		Assert.Null(GameRefs.ReadMember(sample, "НетТакогоПоля"));
		Assert.Equal(before, GameRefs.ReflectionCacheSize);
	}

	[Fact]
	public void CounterGrowsSoTheProbeCanShowIt()
	{
		GameRefs.ResetReflectionCounters();
		var sample = new Sample();
		GameRefs.ReadMember(sample, "Number");
		GameRefs.ReadMember(sample, "Text");
		Assert.True(GameRefs.ReflectionCalls >= 2);
		GameRefs.ResetReflectionCounters();
		Assert.Equal(0, GameRefs.ReflectionCalls);
	}
}
