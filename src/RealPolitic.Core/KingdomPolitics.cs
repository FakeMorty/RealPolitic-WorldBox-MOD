using System.Collections.Generic;

namespace RealPolitic.Core;

public sealed class KingdomPolitics
{
	public readonly Dictionary<IdeologyType, int> Census = new Dictionary<IdeologyType, int>();

	public int KingdomId { get; set; }

	public IdeologyType Ideology { get; set; } = IdeologyType.Monarchy;

	public ReformStance Stance { get; set; } = ReformStance.Moderate;

	public float Legitimacy { get; set; } = 60f;

	public float Support { get; set; }

	public float AvgDissent { get; set; }

	public int FoundedYear { get; set; }

	public bool InWar { get; set; }

	public bool Starving { get; set; }

	public RulerRef? Ruler { get; set; }

	public RevolutionState? Revolution { get; set; }

	/// <summary>Уния, в которой состоит государство (null — не состоит).</summary>
	public Union? Union { get; set; }

	/// <summary>Состояние модовых механик идеологии: заряд ульты и шкала угрозы.</summary>
	public IdeologyMechanicsState Mechanics { get; set; } = new IdeologyMechanicsState();

	/// <summary>Закон престолонаследия / смены власти государства.</summary>
	public SuccessionLaw SuccessionLaw { get; set; } = SuccessionLaw.Salic;

	/// <summary>Состояние регентства (для малолетних правителей младше 18 лет).</summary>
	public RegencyState? Regency { get; set; }

	/// <summary>Состояние выборной системы (для строев с 15-летним циклом: макс. 3 срока).</summary>
	public ElectionState? Election { get; set; }

	/// <summary>Идущая гражданская война (null — внутреннего конфликта нет).</summary>
	public CivilWarState? CivilWar { get; set; }

	// ============================== Этап 26: конституция ==============================

	/// <summary>Писаный свод законов державы (null — додефолт, создаётся на первом снимке мира).</summary>
	public Constitution Constitution { get; set; }

	/// <summary>Журнал правок конституции: кто, когда и какую статью переписал.</summary>
	public List<ConstitutionEdit> ConstitutionHistory { get; } = new List<ConstitutionEdit>();

	/// <summary>Действующие разрешения на войну от собрания/народа (по голосованию).</summary>
	public List<WarAuthorization> WarAuthorizations { get; } = new List<WarAuthorization>();

	/// <summary>Какая попытка референдума по какой берег уже сорвалась (→ до какого года молчим).</summary>
	public Dictionary<int, int> ReferendumCooldowns { get; } = new Dictionary<int, int>();

	/// <summary>Год, когда последний раз на лестницу трона помечали узурпатора
	/// (чтобы не бить по легитимности династию раз за разом за ту же dynasty вопрос).</summary>
	public int LastConstitutionalUsurpYear { get; set; } = -100;

	/// <summary>Год последней поправки к конституции (правителем или народом); -100 — не было.</summary>
	public int LastAmendmentYear { get; set; } = -100;

	/// <summary>Выборка голосующих из живых жителей (возобновляется из игры ежегодно): (id актёра, идеология).</summary>
	public List<(long ActorId, IdeologyType Ideology)> LastVoterSample { get; set; } = new List<(long, IdeologyType)>();

	/// <summary>Есть ли действующее разрешение на войну с данной державой.</summary>
	public bool HasWarAuthorization(int targetId, int year)
	{
		for (int i = 0; i < WarAuthorizations.Count; i++)
		{
			WarAuthorization w = WarAuthorizations[i];
			if (w.TargetKingdomId == targetId && year <= w.ValidUntilYear)
			{
				return true;
			}
		}
		return false;
	}

	/// <summary>Экономика державы (null до первого экономического тика или в старом сохранении).</summary>
	public EconomyState? Economy { get; set; }

	/// <summary>Год окончания последней гражданской войны (0 — не было).</summary>
	public int LastCivilWarYear { get; set; }

	/// <summary>Сколько гражданских войн пережило государство.</summary>
	public int CivilWarsTotal { get; set; }


	/// <summary>Титульная раса/субспециес (ключевой параметр для этнонационализма).</summary>
	public string TitularRaceId { get; set; } = string.Empty;

	/// <summary>Название правящей династии / королевского дома (например, "Рюриковичи", "Крарек").</summary>
	public string RoyalClanName { get; set; } = string.Empty;

	/// <summary>ID королевского клана в WorldBox.</summary>
	public long RoyalClanId { get; set; }

	/// <summary>Основатель текущей правящей династии.</summary>
	public string DynastyFounder { get; set; } = string.Empty;

	/// <summary>Сколько монархов этой династии сменилось на престоле подряд.</summary>
	public int DynastyReignsCount { get; set; } = 1;

	/// <summary>Год воцарения текущей династии.</summary>
	public int DynastyStartYear { get; set; }

	/// <summary>Личность текущего правителя: черта и амбиции (этап 19; null до первого тика или в старом сохранении).</summary>
	public RulerPersonality Personality { get; set; }

	/// <summary>Узы династического брака с другой державой (null — уз нет).</summary>
	public MarriageBond Marriage { get; set; }

	/// <summary>Разведывательная сеть державы (этап 20): уровень и перезарядка операций.</summary>
	public SpyNetwork Spy { get; set; }

	/// <summary>Усталость от войны (этап 22), 0..100: растёт в войне, тает в мире.
	/// Высокая усталость давит легитимность и поддержку, толкает к миру и мятежам.</summary>
	public float WarFatigue { get; set; }

	// ---------------- 0.39: правитель учится, жители помнят ----------------

	/// <summary>Сколько войн держава проиграла (для осторожности и для летописи).</summary>
	public int Defeats { get; set; }

	/// <summary>Год последнего поражения (-100 — не проигрывала).</summary>
	public int LastDefeatYear { get; set; } = -100;

	/// <summary>Год последней победы (-100 — не побеждала).</summary>
	public int LastVictoryYear { get; set; } = -100;

	/// <summary>До какого года держава не начинает новых войн: после поражения — 10 лет.</summary>
	public int WarAversionUntilYear { get; set; }

	/// <summary>Вина перед своими жителями за нарушенные обещания (0..100), затухает за ~20 лет.</summary>
	public float PromiseScore { get; set; }

	/// <summary>Год последнего нарушения обещания (-100 — не нарушала).</summary>
	public int LastPromiseBrokenYear { get; set; } = -100;

	// --- Этап 23 ---

	/// <summary>Фамилия правящей фамилии-династии («Таргариен» из имени основателя).
	/// Пишется один раз при фиксации фамилии; пустая — наследие старых сейвов.</summary>
	public string DynastySurname { get; set; } = string.Empty;

	/// <summary>Признак, что королевский клан игры уже переименован в фамилию основателя.</summary>
	public bool ClanRenamedToSurname { get; set; }

	/// <summary>Последний год тайного авиаудара (рейд бомбардировщиков).</summary>
	public int LastPowerRaidYear { get; set; } = -100;

	/// <summary>Последний год вотума импичмента (статья XIV).</summary>
	public int LastImpeachmentYear { get; set; } = -100;

	/// <summary>0.24, статья XIII: чей род правил до смены дома. По кровному праву
	/// наследник прежнего дома вправе вернуть венец без клейма узурпатора.</summary>
	public long PreviousRoyalClanId { get; set; }

	/// <summary>0.24, статья XIII: венценосные роды, ВПРЕДЬ правившие державой (текущий
	/// дом сюда не входит). Прямой кровью считается наследник любого из этих домов.</summary>
	public readonly List<long> RoyalClanHistory = new List<long>();

	/// <summary>0.27: текущий многошаговый стратегический план державы (5-15 лет).</summary>
	public StrategicCampaignPlan? ActiveCampaign { get; set; }

	/// <summary>0.28: активное общественное движение (петиция / ультиматум).</summary>
	public CivicMovement? ActiveMovement { get; set; }

	/// <summary>0.28: год завершения последнего общественного движения.</summary>
	public int LastCivicMovementYear { get; set; } = -100;

	/// <summary>0.28: политические партии в выборных и парламентских государствах.</summary>
	public readonly List<PoliticalParty> Parties = new List<PoliticalParty>();

	/// <summary>0.31: присвоено ли державе племенное имя от биома, культуры или столицы («Чащобники», «Луговики», «Мерзлотники» и т.д.).</summary>
	public bool HasTribalEthnonym { get; set; }

	/// <summary>0.31: накоплены ли ресурсы на мирную цивилизацию племени.</summary>
	public bool TribalCivilizationFunded { get; set; }

	/// <summary>0.31: объявлено ли пробуждение народа внутри племени после 200-го года.</summary>
	public bool TribalAwakeningAnnounced { get; set; }

	/// <summary>0.31: прошло ли государство цивилизацию (после 200-го года цивилизованная держава не может скатиться обратно в племя).</summary>
	public bool WasCivilized { get; set; }

	/// <summary>0.31: год перехода племени к цивилизованному строю.</summary>
	public int LastCivilizedYear { get; set; }

	/// <summary>0.31: предпочитаемая племенем цивилизованная идеология при мирном переходе.</summary>
	public IdeologyType? PreferredCivilizedIdeology { get; set; }

	/// <summary>Записывает ушедший с престола род в венценосную историю державы.</summary>
	public void RecordRoyalClan(long clanId)
	{
		if (clanId > 0 && !RoyalClanHistory.Contains(clanId))
		{
			RoyalClanHistory.Add(clanId);
		}
	}

	public int Population
	{
		get
		{
			int num = 0;
			foreach (KeyValuePair<IdeologyType, int> censu in Census)
			{
				num += censu.Value;
			}
			return num;
		}
	}

	public IdeologyDef Def => Ideologies.Get(Ideology);

	public int CensusOf(IdeologyType type)
	{
		if (!Census.TryGetValue(type, out var value))
		{
			return 0;
		}
		return value;
	}

	/// <summary>Самая многочисленная оппозиционная группа подданных (или свой строй, если её нет).</summary>
	public IdeologyType LargestOpposition(out int count)
	{
		IdeologyType result = Ideology;
		count = 0;
		foreach (KeyValuePair<IdeologyType, int> item in Census)
		{
			if (item.Key != Ideology && item.Value > count)
			{
				count = item.Value;
				result = item.Key;
			}
		}
		return result;
	}

	/// <summary>
	/// Молодое государство (первые 50 лет после рождения): народ ещё не бузит на власть
	/// (нет протестных движений, петиций, ультиматумов, революций и гражданских войн),
	/// пока государство не устоится.
	/// </summary>
	public bool IsYoungState(int currentYear, int graceYears = 50)
	{
		if (graceYears <= 0 || currentYear <= 0)
		{
			return false;
		}
		int age = currentYear - FoundedYear;
		return age >= 0 && age < graceYears;
	}
}
