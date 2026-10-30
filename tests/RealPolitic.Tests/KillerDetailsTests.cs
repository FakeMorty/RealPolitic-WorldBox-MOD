using RealPolitic.Core;
using Xunit;

namespace RealPolitic.Tests;

/// <summary>
/// Разделение убийц на зверей и разумных существ.
/// Раньше любой убийца попадал в «растерзан зверем», потому что признак
/// «разумный» всегда был false.
/// </summary>
public class KillerDetailsTests
{
	[Fact]
	public void KilledByCivUnit_IsNotAnimalAttack()
	{
		DeathCause cause = DeathCauseClassifier.Classify(null, hasKiller: true, killerIsCivUnit: true, killerIsAssassin: false, inRebellion: false, isSacrifice: false);
		Assert.Equal(DeathCause.KilledByUnit, cause);
	}

	[Fact]
	public void KilledByBeast_IsAnimalAttack()
	{
		DeathCause cause = DeathCauseClassifier.Classify(null, hasKiller: true, killerIsCivUnit: false, killerIsAssassin: false, inRebellion: false, isSacrifice: false);
		Assert.Equal(DeathCause.AnimalAttack, cause);
	}

	[Fact]
	public void KilledByBoat_IsNotAnimalAttack()
	{
		// 0.23.12: корабль (boat_trading_human и т.п.) — судно, а не зверь.
		DeathCause cause = DeathCauseClassifier.Classify(null, hasKiller: true, killerIsCivUnit: false, killerIsAssassin: false, inRebellion: false, isSacrifice: false, killerIsBoat: true);
		Assert.Equal(DeathCause.KilledByBoat, cause);
	}

	[Fact]
	public void BoatKiller_Describe_SaysCannonNotBeast()
	{
		DeathRecord rec = new DeathRecord
		{
			ActorName = "Король",
			Cause = DeathCause.KilledByBoat,
			KillerName = "boat_trading_human",
			KillerSpecies = "boat_trading_human",
			KillerIsCiv = false
		};
		string text = DeathCauseClassifier.Describe(rec);
		Assert.Contains("пушки", text);
		Assert.Contains("корабля", text);
		Assert.DoesNotContain("звер", text);
	}

	[Fact]
	public void BoatKiller_WithRealName_IncludedInDescription()
	{
		DeathRecord rec = new DeathRecord
		{
			ActorName = "Дож",
			Cause = DeathCause.KilledByBoat,
			KillerName = "Мур",
			KillerSpecies = "boat_transport_human",
			KillerIsCiv = false
		};
		string text = DeathCauseClassifier.Describe(rec);
		Assert.Contains("Мур", text);
		Assert.Contains("перевозчик", text);
	}

	[Fact]
	public void BoatKindLabel_MapsRoles()
	{
		Assert.Equal("торговец", DeathRecord.BoatKindLabel("boat_trading_human"));
		Assert.Equal("перевозчик", DeathRecord.BoatKindLabel("boat_transport_human"));
		Assert.Equal("рыболов", DeathRecord.BoatKindLabel("boat_fishing_orc"));
		Assert.Equal(string.Empty, DeathRecord.BoatKindLabel("boat_warship_elf"));
		Assert.Equal(string.Empty, DeathRecord.BoatKindLabel(""));
	}

	[Fact]
	public void QuoteName_WrapsInGuillemets_Once()
	{
		Assert.Equal("«Пичина»", DeathRecord.QuoteName("Пичина"));
		Assert.Equal("«Пичина»", DeathRecord.QuoteName("«Пичина»")); // не дублирует
		Assert.Equal(string.Empty, DeathRecord.QuoteName(""));
		Assert.Equal(string.Empty, DeathRecord.QuoteName(null));
		// 0.23.13: вложенные/смешанные кавычки не размножаются в лесенку «««…»»».
		Assert.Equal("«Пичина»", DeathRecord.QuoteName("„Пичина“"));
		Assert.Equal("«Пичина»", DeathRecord.QuoteName("««Пичина»»"));
		Assert.Equal("«Пичина»", DeathRecord.QuoteName("«„Пичина“»"));
		Assert.Equal("Пичина", DeathRecord.Unquote("«Пичина»"));
		Assert.Equal("Пичина", DeathRecord.Unquote("Пичина"));
	}

	[Fact]
	public void KillerBoatName_HidesRawAssetId_ShowsRealName()
	{
		// Сырой id корабля не должен попадать в летопись как имя.
		DeathRecord raw = new DeathRecord { KillerName = "boat_trading_human", KillerSpecies = "boat_trading_human" };
		Assert.Equal(string.Empty, raw.KillerBoatName);
		// Названный модом корабль показывает своё имя.
		DeathRecord named = new DeathRecord { KillerName = "«Пичина»", KillerSpecies = "boat_trading_human" };
		Assert.Equal("«Пичина»", named.KillerBoatName);
	}

	[Fact]
	public void HumanKiller_DescriptionHasJobKingdomAndCity()
	{
		DeathRecord rec = new DeathRecord
		{
			ActorName = "Король Гедби",
			Cause = DeathCause.KilledByUnit,
			KillerName = "Кузь",
			KillerIsCiv = true,
			KillerJob = "воин",
			KillerKingdom = "Мысерод",
			KillerCity = "Реин"
		};
		string text = DeathCauseClassifier.Describe(rec);
		Assert.Contains("Кузь", text);
		Assert.Contains("воин", text);
		Assert.Contains("Мысерод", text);
		Assert.Contains("Реин", text);
		Assert.DoesNotContain("зверем", text);
	}

	[Fact]
	public void BeastKiller_DescriptionHasSpecies()
	{
		DeathRecord rec = new DeathRecord
		{
			ActorName = "Король Дамырир",
			Cause = DeathCause.AnimalAttack,
			KillerName = "Хрю",
			KillerIsCiv = false,
			KillerSpecies = "волк"
		};
		string text = DeathCauseClassifier.Describe(rec);
		Assert.Contains("волк", text);
		Assert.Contains("Хрю", text);
	}

	[Fact]
	public void BeastWithoutSpecies_StillReadable()
	{
		DeathRecord rec = new DeathRecord
		{
			ActorName = "Король",
			Cause = DeathCause.AnimalAttack,
			KillerIsCiv = false
		};
		string text = DeathCauseClassifier.Describe(rec);
		Assert.Contains("зверем", text);
	}

	[Fact]
	public void HumanKillerWithoutDetails_ShowsAtLeastName()
	{
		DeathRecord rec = new DeathRecord
		{
			ActorName = "Король",
			Cause = DeathCause.KilledByUnit,
			KillerName = "Силаелан",
			KillerIsCiv = true
		};
		string text = DeathCauseClassifier.Describe(rec);
		Assert.Contains("Силаелан", text);
		Assert.Contains("держава: без государства", text);
	}

	[Fact]
	public void BoatAndUnitKillers_ExplicitlyShowForeignOrDomesticState()
	{
		DeathRecord foreignBoat = new DeathRecord
		{
			ActorName = "Консул Сильвия",
			KingdomNameAtDeath = "Германия",
			Cause = DeathCause.KilledByBoat,
			KillerName = "«Винстед»",
			KillerSpecies = "boat_transport_human",
			KillerKingdom = "Швеция",
			KillerCity = "Винстед"
		};
		string boatDesc = DeathCauseClassifier.Describe(foreignBoat);
		Assert.Contains("держава: Швеция [флот чужой державы]", boatDesc);
		Assert.Contains("порт: Винстед", boatDesc);

		DeathRecord domesticKiller = new DeathRecord
		{
			ActorName = "Генеральный секретарь Белия",
			KingdomNameAtDeath = "Турция",
			Cause = DeathCause.KilledByUnit,
			KillerName = "Златен",
			KillerIsCiv = true,
			KillerKingdom = "Турция",
			KillerCity = "Валлатарона"
		};
		string domDesc = DeathCauseClassifier.Describe(domesticKiller);
		Assert.Contains("держава: Турция [своя держава]", domDesc);

		DeathRecord foreignKiller = new DeathRecord
		{
			ActorName = "Вождь Кастелик",
			KingdomNameAtDeath = "Крым",
			Cause = DeathCause.KilledByUnit,
			KillerName = "Златобор",
			KillerIsCiv = true,
			KillerKingdom = "Украина",
			KillerCity = "Норстентон"
		};
		string forDesc = DeathCauseClassifier.Describe(foreignKiller);
		Assert.Contains("держава: Украина [чужая держава]", forDesc);
	}
}
