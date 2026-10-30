using System;
using System.Collections.Generic;

namespace RealPolitic.Core;

/// <summary>
/// 0.49: как держава потеряла свою родину. От этого зависит, насколько горячо
/// её жители хотят вернуть своё государство.
///
/// Заказ владельца 29.10.2026: «особенно они активные при федерации, вассалитете, унии».
/// </summary>
public enum HomesteadBond
{
	/// <summary>Родину просто завоевали.</summary>
	Conquest = 0,

	/// <summary>Родина стала вассалом — своё правительство есть, но под чужим.</summary>
	Vassalage = 1,

	/// <summary>Родина вошла в федерацию: вроде равные, но своего голоса мало.</summary>
	Federation = 2,

	/// <summary>Родина вошла в унию: один монарх на две страны, и он чужой.</summary>
	Union = 3
}

/// <summary>
/// 0.49: что националисты делают с чужими, придя к власти.
/// Владелец 29.10.2026: «Депортация — просто лишают гражданства и блокируют попытки
/// получить гражданство страны, откуда депортировали». То есть никого не вывозят и
/// не убивают — человек остаётся жить, но перестаёт быть гражданином.
/// </summary>
public enum NationalismVerdict
{
	/// <summary>Только запрет въезда: живущих не трогают.</summary>
	CloseBorders = 0,

	/// <summary>Отзыв гражданства у чужих + запрет въезда на долгий срок.</summary>
	RevokeCitizenship = 1
}

/// <summary>
/// 0.49: состояние национализма в одной державе.
/// </summary>
public sealed class NationalismState
{
	/// <summary>Держава-родина, которую хотят вернуть (0 — такой нет, национализма не будет).</summary>
	public int OriginKingdomId;

	/// <summary>Как родина звалась — для летописи, потому что самой державы уже нет.</summary>
	public string OriginName = string.Empty;

	/// <summary>В каком году родина была потеряна.</summary>
	public int OriginYear;

	/// <summary>Как её потеряли.</summary>
	public HomesteadBond Bond;

	/// <summary>Насколько сильны националисты, 0..100.</summary>
	public float Pressure;

	/// <summary>
	/// Строй, который был у державы ПРЯМО ПЕРЕД приходом националистов.
	/// Владелец 29.10.2026: именно его националисты и возвращают.
	/// </summary>
	public int PrevIdeology = -1;

	/// <summary>С какого года в державе стоит национализм (0 — не стоит).</summary>
	public int SeizedYear;

	/// <summary>До какого года чужим нельзя получать гражданство.</summary>
	public int EntryBanUntilYear;

	/// <summary>На сколько лет национализм ушёл в тень после победы.</summary>
	public int ShadowUntilYear;

	/// <summary>Когда националистов разгромили (0 — не громили).</summary>
	public int SuppressedYear;

	/// <summary>Сколько раз они уже приходили к власти.</summary>
	public int Victories;

	public bool Active => OriginKingdomId > 0;

	/// <summary>Национализм сейчас у власти.</summary>
	public bool InPower(int year) => SeizedYear > 0 && year >= SeizedYear && year < SeizedYear + NationalismEngine.ReturnAfterYears;

	public void Validate()
	{
		if (OriginKingdomId < 0 || OriginName == null)
		{
			throw new ArgumentException("Invalid nationalism state.");
		}
		if (!Enum.IsDefined(typeof(HomesteadBond), Bond))
		{
			throw new ArgumentException("Invalid homestead bond.");
		}
		if (Pressure < 0f || Pressure > 100f)
		{
			throw new ArgumentException("Pressure out of range.");
		}
	}
}

/// <summary>
/// 0.49: гражданский национализм — «вернуть своё государство».
///
/// Как это работает, простыми словами.
///
/// 1. У державы есть память: «эти земли раньше были отдельной страной». Запоминаем
///    только то, что мод видел сам: держава исчезла, а её города достались другому.
/// 2. Жители этих земель понемногу становятся националистами. Чем мягче было
///    присоединение (федерация и уния — мягче всего, прямой захват — жёстче),
///    тем их больше. И чем дольше они живут под чужой короной, тем меньше помнят:
///    каждые 30 лет желание падает вдвое.
/// 3. Когда националистов становится много, они приходят к власти: через выборы,
///    через ультиматум или через гражданскую войну (эти пути в моде уже есть).
/// 4. Придя к власти, они НЕ правят сами. Они возвращают тот строй, который был
///    у державы прямо перед ними, и на годы закрывают чужим въезд. Если правитель
///    жестокий — ещё и отзывают гражданство у чужих.
/// 5. Через несколько лет держава снова та, что была, а национализм уходит в тень.
///
/// Всё здесь — чистые расчёты без обращения к игре: их можно проверять тестами.
/// </summary>
public static class NationalismEngine
{
	/// <summary>Сколько лет националисты держат власть, прежде чем вернуть прежний строй.</summary>
	public const int ReturnAfterYears = 3;

	/// <summary>Порог, с которого они начинают требовать своё (0..100).</summary>
	public const float PowerThreshold = 45f;

	/// <summary>Порог, после которого они идут на всё — ультиматум и война.</summary>
	public const float UprisingThreshold = 70f;

	/// <summary>Сколько лет национализм сидит тихо после победы.</summary>
	public const int ShadowAfterVictoryYears = 40;

	/// <summary>Сколько лет сидит тихо после разгрома.</summary>
	public const int ShadowAfterDefeatYears = 20;

	/// <summary>
	/// Какая доля жителей бывшей родины (в процентах) становится националистами
	/// при разных способах присоединения. Ровно по заказу владельца:
	/// федерация и уния — самые горячие, вассалитет — средне, захват — тише.
	/// </summary>
	public static float BaseShare(HomesteadBond bond)
	{
		switch (bond)
		{
			case HomesteadBond.Union: return 45f;
			case HomesteadBond.Federation: return 40f;
			case HomesteadBond.Vassalage: return 30f;
			default: return 15f;
		}
	}

	/// <summary>
	/// Насколько ещё жива память о родине через years лет: 1 — свежая обида,
	/// 0.5 через 60 лет (два поколения), 0.25 через 120.
	///
	/// Почему 60, а не 30: при 30 давление падало быстрее, чем успевало вырасти,
	/// и национализм не поднимался бы вообще — механика была бы мёртвой.
	/// </summary>
	public static float GenerationsFade(float pressure, int years)
	{
		if (years <= 0 || pressure <= 0f)
		{
			return Math.Max(0f, pressure);
		}
		double factor = Math.Pow(0.5, years / 60.0);
		return (float)(pressure * factor);
	}

	/// <summary>
	/// Насколько способ присоединения подогревает движение:
	/// уния — сильнее всех, честный захват — тише всех.
	/// </summary>
	public static float BondFactor(HomesteadBond bond)
	{
		return BaseShare(bond) / 35f;
	}

	/// <summary>
	/// Один год жизни национализма. Возвращает новое давление 0..100.
	///
	/// pressure — что было; share — сколько их сейчас (доля от населения);
	/// oppressed — притесняют ли их (тогда злоба растёт быстрее);
	/// yearsUnder — сколько лет живут под чужой короной.
	/// </summary>
	public static float NextPressure(float pressure, float share, bool oppressed, int yearsUnder)
	{
		// У движения есть потолок: сколько бывших земель и как давно они потеряны.
		// Доля 0.25 и свежая обида — потолок 25, много земель и свежая рана — почти 100.
		float ceiling = Clamp(Math.Max(0f, share) * 100f * GenerationsFade(1f, yearsUnder));
		float growth = oppressed ? 2.5f : 1f;
		// Чем больше их уже, тем быстрее растут: движение кормит себя само.
		growth *= 1f + Math.Max(0f, share);
		if (pressure < ceiling)
		{
			// Растём к потолку, но не перескакиваем его.
			return Clamp(Math.Min(ceiling, pressure + growth));
		}
		// Потолок просел (земли вернули/память стёрлась) — тихо остываем.
		return Clamp(Math.Max(ceiling, pressure - 0.6f));
	}

	/// <summary>Начинается ли движение: есть родина и есть кому обижаться.</summary>
	public static bool CanRise(NationalismState state, int year)
	{
		if (state == null || !state.Active)
		{
			return false;
		}
		if (year < state.ShadowUntilYear)
		{
			return false;
		}
		return state.Pressure >= PowerThreshold;
	}

	/// <summary>Готовы ли они идти на всё: ультиматум, война.</summary>
	public static bool ReadyToUprise(NationalismState state, int year)
	{
		return CanRise(state, year) && state.Pressure >= UprisingThreshold;
	}

	/// <summary>
	/// Что сделают с чужими, придя к власти. Решает ХАРАКТЕР ПРАВИТЕЛЯ державы,
	/// которую они свергли (заказ владельца 29.10.2026: «по характеру лидера»).
	/// Нет правителя — решает сила движения.
	/// </summary>
	public static NationalismVerdict Verdict(RulerTrait trait, float pressure)
	{
		switch (trait)
		{
			// Жестокий и завоеватель — отзывают гражданство, то есть лишают прав.
			case RulerTrait.Cruel:
			case RulerTrait.Conqueror:
				return NationalismVerdict.RevokeCitizenship;
			// Благочестивый — тоже: «неверных» из державы.
			case RulerTrait.Pious:
				return pressure >= 60f ? NationalismVerdict.RevokeCitizenship : NationalismVerdict.CloseBorders;
			// Милосердный и справедливый — только закрывают въезд.
			case RulerTrait.Just:
			case RulerTrait.Diplomat:
				return NationalismVerdict.CloseBorders;
			default:
				return pressure >= 75f ? NationalismVerdict.RevokeCitizenship : NationalismVerdict.CloseBorders;
		}
	}

	/// <summary>
	/// На сколько лет закрыть въезд чужим. Сильные националисты закрывают надолго:
	/// 50 / 100 / 200 лет (заказ владельца). Слабое движение не может диктовать 200 лет.
	/// </summary>
	public static int EntryBanYears(float pressure)
	{
		if (pressure >= 85f)
		{
			return 200;
		}
		if (pressure >= 65f)
		{
			return 100;
		}
		return 50;
	}

	/// <summary>
	/// Националисты пришли к власти. Возвращаем состояние с записанным прежним строем.
	/// </summary>
	public static void Seize(NationalismState state, int year, IdeologyType previousIdeology, RulerTrait trait)
	{
		if (state == null)
		{
			throw new ArgumentNullException(nameof(state));
		}
		state.PrevIdeology = (int)previousIdeology;
		state.SeizedYear = year;
		NationalismVerdict verdict = Verdict(trait, state.Pressure);
		state.EntryBanUntilYear = year + EntryBanYears(state.Pressure);
		if (verdict == NationalismVerdict.CloseBorders)
		{
			// Только запрет въезда — гражданства не трогаем.
			state.EntryBanUntilYear = year + Math.Min(100, EntryBanYears(state.Pressure));
		}
	}

	/// <summary>Пришло ли время вернуть прежний строй.</summary>
	public static bool TimeToReturn(NationalismState state, int year)
	{
		return state != null && state.SeizedYear > 0 && year >= state.SeizedYear + ReturnAfterYears;
	}

	/// <summary>
	/// Строй, который националисты возвращают. Если по какой-то причине он не записан,
	/// возвращать нечего — тогда остаётся демократия как самое безобидное.
	/// </summary>
	public static IdeologyType ParentIdeology(NationalismState state)
	{
		if (state == null || state.PrevIdeology < 0 || !Enum.IsDefined(typeof(IdeologyType), state.PrevIdeology))
		{
			return IdeologyType.Democracy;
		}
		IdeologyType previous = (IdeologyType)state.PrevIdeology;
		// Национализм не может «вернуть» национализм.
		return previous == IdeologyType.Nationalism ? IdeologyType.Democracy : previous;
	}

	/// <summary>
	/// Почему жители стали националистами — строкой для летописи.
	/// </summary>
	public static string BondReasonKey(HomesteadBond bond)
	{
		switch (bond)
		{
			case HomesteadBond.Union: return "RP49_BOND_UNION";
			case HomesteadBond.Federation: return "RP49_BOND_FEDERATION";
			case HomesteadBond.Vassalage: return "RP49_BOND_VASSALAGE";
			default: return "RP49_BOND_CONQUEST";
		}
	}

	private static float Clamp(float value)
	{
		if (value < 0f)
		{
			return 0f;
		}
		if (value > 100f)
		{
			return 100f;
		}
		return value;
	}
}
