using System.Collections.Generic;
using RealPolitic.Core;
using Xunit;

namespace RealPolitic.Tests;

/// <summary>
/// Проверки нового пути объявления войны (0.31.15).
///
/// Смысл: в 0.31.12–0.31.14 мод вызывал startWar «слепым» отражением и считал войну
/// начатой, если вызов вернул объект. Из-за этого 74 попытки в одной партии остались
/// без объяснения, а в летописи не появилось ни одной строки «Началась война
/// (война за независимость)». Теперь:
///  • вызов, который вернул null, войной не считается;
///  • перегрузка с другим числом параметров всё равно находится и вызывается;
///  • если реестр войн игры читается, истина берётся только из него;
///  • есть запасной путь WarManager.newWar.
/// </summary>
public class WarApiProbeTests
{
	// ---- Подставные объекты с «игровыми» сигнатурами ----

	private sealed class NullDiplomacy
	{
		public int Calls;

		public War startWar(Kingdom pAttacker, Kingdom pDefender, WarTypeAsset pAsset, bool pLog = true)
		{
			Calls++;
			return null;
		}
	}

	private sealed class FiveParamDiplomacy
	{
		public int Calls;
		public string Used;

		// Так выглядит игра, если у метода появился ещё один необязательный параметр.
		public War startWar(Kingdom pAttacker, Kingdom pDefender, WarTypeAsset pAsset, bool pLog, bool pForce)
		{
			Calls++;
			Used = "startWar(Kingdom,Kingdom,WarTypeAsset,bool,bool)";
			return new War();
		}
	}

	private sealed class ThreeParamDiplomacy
	{
		public int Calls;

		public War startWar(Kingdom pAttacker, Kingdom pDefender)
		{
			Calls++;
			return new War();
		}
	}

	/// <summary>Реестр войн, который читается и растёт при вызове newWar.</summary>
	private sealed class GrowingWars
	{
		public List<War> list = new List<War>();

		public void checkLists()
		{
		}

		public War newWar(Kingdom pAttacker, Kingdom pDefender, WarTypeAsset pType)
		{
			var war = new War();
			list.Add(war);
			return war;
		}
	}

	/// <summary>Реестр войн, который читается, но не меняется (игра отказала).</summary>
	private sealed class FrozenWars
	{
		public List<War> list = new List<War>();

		public void checkLists()
		{
		}
	}

	private sealed class RefusingWars
	{
		public List<War> list = new List<War>();

		public void checkLists()
		{
		}

		public War newWar(Kingdom pAttacker, Kingdom pDefender, WarTypeAsset pType)
		{
			return null;
		}
	}

	private static Kingdom K(long id)
	{
		return new Kingdom { id = id, name = "Держава" + id, cities = new List<City>() };
	}

	// ---- Войны ----

	[Fact]
	public void StartWarReturningNull_IsNotAConfirmedWar()
	{
		var diplomacy = new NullDiplomacy();
		var wars = new FrozenWars();

		bool ok = WarApiProbe.EnsureWar(diplomacy, wars, K(1), K(2), null, out string verdict);

		Assert.False(ok);
		Assert.Equal(1, diplomacy.Calls);
		Assert.Contains("войны нет", verdict);
	}

	[Fact]
	public void OverloadWithExtraParameter_IsFoundAndCalled()
	{
		var diplomacy = new FiveParamDiplomacy();
		var wars = new FrozenWars();

		WarApiProbe.EnsureWar(diplomacy, wars, K(1), K(2), null, out string verdict);

		Assert.Equal(1, diplomacy.Calls);
		Assert.Contains("Boolean, Boolean", verdict);
	}

	[Fact]
	public void OverloadWithoutWarType_IsCalledButRegistryStillDecides()
	{
		var diplomacy = new ThreeParamDiplomacy();
		var wars = new FrozenWars();

		bool ok = WarApiProbe.EnsureWar(diplomacy, wars, K(1), K(2), null, out string verdict);

		// Перегрузка без типа войны вызывается (в игре такая может быть), но войной
		// она считается только после появления записи в реестре.
		Assert.Equal(1, diplomacy.Calls);
		Assert.False(ok);
		Assert.Contains("войны нет", verdict);
	}

	[Fact]
	public void MissingStartWar_FallsBackToWarManagerNewWar()
	{
		var wars = new GrowingWars();

		bool ok = WarApiProbe.EnsureWar(new object(), wars, K(1), K(2), null, out string verdict);

		Assert.True(ok);
		Assert.Single(wars.list);
		Assert.Contains("newWar", verdict);
	}

	[Fact]
	public void WarAddedToRegistry_CountsEvenWhenStartWarReturnedNull()
	{
		var wars = new GrowingWars();
		var diplomacy = new NullDiplomacy();

		// startWar вернул null, но реестр вырос: это возможно, если игра создаёт войну
		// и не возвращает её. Тогда война настоящая.
		bool ok = WarApiProbe.EnsureWar(diplomacy, wars, K(1), K(2), null, out string verdict);

		Assert.True(ok);
		Assert.Contains("война есть", verdict);
	}

	[Fact]
	public void ReturnedWarWithoutRegistryEntry_IsNotConfirmedWhenRegistryIsReadable()
	{
		var diplomacy = new FiveParamDiplomacy();
		var wars = new RefusingWars();

		bool ok = WarApiProbe.EnsureWar(diplomacy, wars, K(1), K(2), null, out string verdict);

		// Вернулся объект, но в реестре войны нет — значит игрой она не принята.
		Assert.False(ok);
		Assert.Contains("войны нет", verdict);
	}

	[Fact]
	public void SameKingdomIsRejectedWithoutAnyCall()
	{
		var diplomacy = new NullDiplomacy();
		Kingdom one = K(1);

		bool ok = WarApiProbe.EnsureWar(diplomacy, new FrozenWars(), one, one, null, out _);

		Assert.False(ok);
		Assert.Equal(0, diplomacy.Calls);
	}

	// ---- Личные верования (ошибка Invalid personal belief) ----

	[Fact]
	public void HugeKingdomId_IsNotTruncatedIntoInvalidBelief()
	{
		var kingdom = K(long.MaxValue);
		var actor = new Actor { data = new ActorData { id = 5 }, name = "Житель", kingdom = kingdom };

		ActorIdeologies.Faith faith = ActorIdeologies.Get(actor);

		Assert.NotNull(faith);
		Assert.Equal(-1, faith.KingdomId);
	}

	[Fact]
	public void NegativeKingdomId_IsNotTruncatedIntoInvalidBelief()
	{
		var kingdom = K(-9876543210);
		var actor = new Actor { data = new ActorData { id = 6 }, name = "Житель", kingdom = kingdom };

		ActorIdeologies.Faith faith = ActorIdeologies.Get(actor);

		Assert.NotNull(faith);
		Assert.Equal(-1, faith.KingdomId);
	}
}
