using RealPolitic.Core;
using Xunit;

namespace RealPolitic.Tests;

/// <summary>
/// 0.48: один мир — одна папка летописи.
///
/// Владелец прислал два ключа одного и того же мира одной сессии: «universal-1»
/// и «native-44d25702…0b4f46». Из-за смены ключа летопись разорвалась на две папки.
/// </summary>
public class WorldKeyRulesTests
{
	[Fact]
	public void ProvisionalKeyIsRecognised()
	{
		Assert.True(WorldKeyRules.IsProvisional("universal-1"));
		Assert.False(WorldKeyRules.IsProvisional("native-44d25702e4ce478484bf83a62d0b4f46"));
		Assert.False(WorldKeyRules.IsProvisional(""));
	}

	[Fact]
	public void NativeKeyIsRecognised()
	{
		Assert.True(WorldKeyRules.IsNative("native-44d25702e4ce478484bf83a62d0b4f46"));
		Assert.False(WorldKeyRules.IsNative("universal-1"));
	}

	[Fact]
	public void ProvisionalKeyReplacedByNativeKeepsTheSameFolder()
	{
		Assert.True(WorldKeyRules.IsSameWorldUpgrade("universal-1", "native-44d25702e4ce478484bf83a62d0b4f46"));
	}

	[Fact]
	public void TwoDifferentNativeKeysAreDifferentWorlds()
	{
		Assert.False(WorldKeyRules.IsSameWorldUpgrade("native-aaa111", "native-bbb222"));
	}

	[Fact]
	public void NativeIsNeverReplacedByProvisional()
	{
		Assert.False(WorldKeyRules.IsSameWorldUpgrade("native-aaa111", "universal-5"));
	}

	[Fact]
	public void SameKeyIsNotAnUpgrade()
	{
		Assert.False(WorldKeyRules.IsSameWorldUpgrade("native-aaa111", "native-aaa111"));
		Assert.False(WorldKeyRules.IsSameWorldUpgrade(null, "native-aaa111"));
	}
}
