using System;
using NeoModLoader.api;
using RealPolitic.Core;

namespace RealPolitic;

public class RealPoliticMod : BasicMod<RealPoliticMod>
{
	public static PoliticalWorld World = new PoliticalWorld();

	public static NotificationCenter Notifications;

		private static WorldFeeder _feeder;

		public static WorldFeeder Feeder => _feeder;

	private static GameNotifier _notifier;

	public static GameNotifier Notifier => _notifier;

	public static GameDeaths Deaths;

	/// <summary>Летопись мира (txt-файл); null, если отключена или не удалось создать.</summary>
	public static Chronicle Chronicle;

	/// <summary>Путь к папке мода (для чтения иконок из файлов, если встроенный ресурс недоступен).</summary>
	public static string ModFolder = string.Empty;

	private static bool _spritesRegistered;

	private static bool _windowsPrewarmed;

	private static int _prewarmAttempts;

		/// <summary>Вести летопись мира в txt-файл (настройка EnableChronicle).</summary>
		public static bool ChronicleEnabled = true;

		/// <summary>Показывать план и результат стратегического ИИ в окне государства.</summary>
		public static bool ShowStrategyUI = true;

		/// <summary>0.41: писать в журнал строку замера скорости раз в 25 лет.</summary>
		public static bool PerfReport = true;

		/// <summary>0.41: подробный журнал ИИ (план каждой державы и каждый совет).
		/// По умолчанию выключен: на высокой скорости это тысячи строк в секунду.</summary>
		public static bool VerboseAiLog = false;

		/// <summary>0.41: сколько жителей державы опрашивать за год (перепись взглядов).</summary>
		public static int CensusSampleLimit = 240;

		/// <summary>
		/// 0.45: запрет ванильных альянсов «в обход мода».
		/// Владелец 29.10.2026: «заблокируй возможность создавать или входить в альянсы,
		/// если мод этого по тем или иным причинам пока не позволяет. Задолбал отвлекаться
		/// на этот щит». Пока включено: кнопка альянса в окне державы скрыта, а сама игра
		/// не может создать союз или вписать в него державу мимо политики мода.
		/// Альянсы, которые мод создаёт сам (КМУД, союзы по конституции), работают как раньше.
		/// </summary>
		public static bool BlockNativeAlliances = true;

		/// <summary>Показывать дипломатическую сводку в окне государства.</summary>
		public static bool ShowDiplomacyUI = true;

		/// <summary>Показывать подробную строку унии/союза в окне государства.</summary>
		public static bool ShowUnionUI = true;

		/// <summary>Показывать последнее событие летописи в окне государства.</summary>
		public static bool ShowKingdomChronicleUI = true;

		/// <summary>Показывать строку гражданской войны в окне государства.</summary>
		public static bool ShowCivilWarUI = true;

	/// <summary>Показывать строку экономики в окне государства.</summary>
	public static bool ShowEconomyUI = true;

	/// <summary>Показывать личность правителя (характер, амбиции, брак) в окне государства.</summary>
	public static bool ShowDynastyUI = true;

	/// <summary>Версия сборки. Печатается в лог при загрузке: по ней легко убедиться,
	/// что установленная «обнова» действительно загружена, а не старый кэш.</summary>
	public const string Version = "0.53.2"; // сверяется с mod.json тестом VersionTests

	protected override void OnModLoad()
	{
		try
		{
			BasicMod<RealPoliticMod>.LogInfo("Real Politic: загрузка ядра…");
			BasicMod<RealPoliticMod>.LogInfo("Real Politic: версия " + Version + " — рабочая ветка, нативный выпуск ещё не проверен");
			try
			{
				ModFolder = GetDeclaration().FolderPath ?? string.Empty;
			}
			catch (Exception)
			{
				ModFolder = string.Empty;
			}
			RuntimeDiagnostics.LogStartup();
			// Ядро не знает игры: все его человекочитаемые строки идут через этот мост в локали.
			CoreText.Localizer = GameText.Localized;
			Notifications = new NotificationCenter(World.Bus);
			_feeder = new WorldFeeder(World);
			_notifier = new GameNotifier(World.Bus, Notifications);
				Deaths = new GameDeaths(World);
				ApplyConfig();
				if (ChronicleEnabled)
				{
					Chronicle = new Chronicle(World, _notifier, ModFolder);
					try
					{
						ModConfig config = base.GetConfig();
						if (config != null)
						{
							Chronicle.SummaryEveryYears = ConfigReader.ReadInt(config, "ChronicleSummaryYears", fallback: 1);
						}
					}
					catch (Exception)
					{
						// значение по умолчанию — сводка каждый год
					}
				}
			BasicMod<RealPoliticMod>.LogInfo("Real Politic: установлено патчей: " + Patches.Install());
			BasicMod<RealPoliticMod>.LogInfo("Real Politic: ядро готово, идеологий в реестре: " + CountIdeologies());
		}
		catch (Exception ex)
		{
			BasicMod<RealPoliticMod>.LogError("Real Politic: безопасная загрузка остановлена: " + ex);
		}
	}

	private float _nextConfigCheck;

	/// <summary>Переключатель уведомления: симметричный Mute/Unmute, чтобы живое
	/// перечитывание конфига корректно включало события обратно.</summary>
	private static void SetNotify(PolEventType type, bool enabled)
	{
		if (enabled)
		{
			Notifications.Unmute(type);
		}
		else
		{
			Notifications.Mute(type);
		}
	}

	private void ApplyConfig()
	{
		try
		{
			ModConfig config = base.GetConfig();
			if (config != null)
			{
				World.Params = ConfigReader.ReadParams(config);
                RealPolitic.Chronicle.ReadableEdition = ConfigReader.ReadBool(config, "ChronicleReadable", fallback: true);
                RealPolitic.Chronicle.GroupExtinctions = ConfigReader.ReadBool(config, "ChronicleGroupExtinctions", fallback: true);
                RealPolitic.Chronicle.GroupBorders = ConfigReader.ReadBool(config, "ChronicleGroupBorders", fallback: true);
				Notifications.MinSeverity = ConfigReader.ReadSeverity(config, "NotifyLevel", Severity.Major);
				Notifications.CollapseSameType = ConfigReader.ReadBool(config, "CollapseNotifications", fallback: true);
				SetNotify(PolEventType.ActorConverted, ConfigReader.ReadBool(config, "NotifyActorConverted", fallback: false));
				bool novelties = ConfigReader.ReadBool(config, "NotifyNovelties", fallback: true);
				SetNotify(PolEventType.NewReligion, novelties);
				SetNotify(PolEventType.NewLanguage, novelties);
				SetNotify(PolEventType.NewCulture, novelties);
				// Войны всегда пишутся в летопись; игровые уведомления — по желанию
				// (по умолчанию выключены: ванильная игра и так логит войны в мир).
				bool wars = ConfigReader.ReadBool(config, "NotifyWars", fallback: false);
				SetNotify(PolEventType.WarStarted, wars);
				SetNotify(PolEventType.WarEnded, wars);
				// Особые действия строя: уведомления по желанию (по умолчанию включены —
				// это редкие события, раз в 20-40 лет на государство).
				SetNotify(PolEventType.IdeologyPowerUsed, ConfigReader.ReadBool(config, "NotifyIdeologyPowers", fallback: true));
				// Окончание действия ульты — мелкое событие, по умолчанию выключено,
				// чтобы не удваивать поток сообщений о механиках.
				SetNotify(PolEventType.IdeologyPowerEnded, ConfigReader.ReadBool(config, "NotifyIdeologyPowerEnd", fallback: false));
				// Срывы внутренних угроз и экономические кризисы всегда пишутся в летопись и историю державы,
				// но по умолчанию не спамят всплывашками по каждому государству.
				bool notifyThreats = ConfigReader.ReadBool(config, "NotifyIdeologyThreats", fallback: false);
				SetNotify(PolEventType.IdeologyThreatBroke, notifyThreats);
				SetNotify(PolEventType.IdeologyThreatRecovered, notifyThreats);
				bool notifyEcoCrises = ConfigReader.ReadBool(config, "NotifyEconomyCrises", fallback: false);
				SetNotify(PolEventType.EconomicCrisisStarted, notifyEcoCrises);
				SetNotify(PolEventType.EconomicCrisisEnded, notifyEcoCrises);
				Patches.DiplomacyEnabled = ConfigReader.ReadBool(config, "EnableSmartDiplomacy", fallback: true);
				Patches.UnionsEnabled = ConfigReader.ReadBool(config, "EnableUnions", fallback: true);
					ChronicleEnabled = ConfigReader.ReadBool(config, "EnableChronicle", fallback: true);
					ShowStrategyUI = ConfigReader.ReadBool(config, "ShowStrategyUI", fallback: true);
					PerfReport = ConfigReader.ReadBool(config, "PerfReport", fallback: true);
					VerboseAiLog = ConfigReader.ReadBool(config, "VerboseAiLog", fallback: false);
					CensusSampleLimit = Math.Max(40, ConfigReader.ReadInt(config, "CensusSampleLimit", fallback: 240));
					BlockNativeAlliances = ConfigReader.ReadBool(config, "BlockNativeAlliances", fallback: true);
					ShowDiplomacyUI = ConfigReader.ReadBool(config, "ShowDiplomacyUI", fallback: true);
					ShowUnionUI = ConfigReader.ReadBool(config, "ShowUnionUI", fallback: true);
					ShowKingdomChronicleUI = ConfigReader.ReadBool(config, "ShowKingdomChronicleUI", fallback: true);
					ShowCivilWarUI = ConfigReader.ReadBool(config, "ShowCivilWarUI", fallback: true);
					ShowDynastyUI = ConfigReader.ReadBool(config, "ShowDynastyUI", fallback: true);
					// Гражданские войны и расколы: механика и её игровое исполнение включаются раздельно.
					CivilWarExecutor.CivilWarsEnabled = ConfigReader.ReadBool(config, "EnableCivilWars", fallback: true);
					CivilWarExecutor.SecessionsEnabled = ConfigReader.ReadBool(config, "EnableSecessions", fallback: true);
				// Экономика и торговля: модель, её игровое исполнение и строка в окне.
				ShowEconomyUI = ConfigReader.ReadBool(config, "ShowEconomyUI", fallback: true);
				EconomyExecutor.EconomyEnabled = ConfigReader.ReadBool(config, "EnableEconomy", fallback: true);
				EconomyExecutor.TradeEnabled = ConfigReader.ReadBool(config, "EnableTrade", fallback: true);
				// Пути и уровни путей — частая мелочь: по умолчанию только в летопись.
				bool trade = ConfigReader.ReadBool(config, "NotifyTrade", fallback: true);
				SetNotify(PolEventType.TradeRouteOpened, trade);
				SetNotify(PolEventType.TradeRouteUpgraded, trade);
				// 0.23.2: события мира — эпохи, вымирания, династические расколы (по умолчанию включены).
				SetNotify(PolEventType.EraChanged, ConfigReader.ReadBool(config, "NotifyEras", fallback: true));
				SetNotify(PolEventType.SubspeciesExtinct, ConfigReader.ReadBool(config, "NotifyExtinctions", fallback: true));
				SetNotify(PolEventType.RoyalRelativeSeceded, ConfigReader.ReadBool(config, "NotifyRoyalSecessions", fallback: true));
				IdeologyEffects.AffectLoyalty = ConfigReader.ReadBool(config, "IdeologyAffectsLoyalty", fallback: true);
				IdeologyEffects.AffectMood = ConfigReader.ReadBool(config, "IdeologyAffectsMood", fallback: true);
				}
		}
		catch (Exception ex)
		{
			BasicMod<RealPoliticMod>.LogError("Real Politic: не удалось прочитать конфиг, используем значения по умолчанию: " + ex.Message);
		}
	}

	/// <summary>Текущий игровой год (те же часы, что и у WorldFeeder: 60 единиц времени = 1 год).</summary>
	public static int CurrentYear()
	{
		try
		{
			MapBox map = global::World.world;
			if (map == null)
			{
				return -1;
			}
			int year = (int)(map.getCurWorldTime() / 60.0) + 1;
			return (year < 0) ? 0 : year;
		}
		catch (Exception)
		{
			return -1;
		}
	}

	private static int CountIdeologies()
	{
		int num = 0;
		foreach (IdeologyDef item in Ideologies.All)
		{
			_ = item;
			num++;
		}
		return num;
	}

    public void OnApplicationQuit() { Chronicle?.Finish(); }

	public void Update()
	{
		try
		{
			// Иконки регистрируем на первом кадре — после полной загрузки игры (надёжнее по таймингу).
			if (!_spritesRegistered)
			{
				_spritesRegistered = true;
				IdeologySprites.Register();
			}
			if (!_windowsPrewarmed && _prewarmAttempts < 10 && global::World.world != null)
			{
				bool anyOpen = GameRefs.Invoke(global::World.world, "isWindowOnScreen") is bool onScreen && onScreen;
				if (!anyOpen)
				{
					_prewarmAttempts++;
					bool a = ConstitutionWindow.Prewarm();
					bool b = CivicVotesWindow.Prewarm();
					bool c = FamilyTreeWindow.Prewarm();
					bool d = KmudWindow.Prewarm();
					// 0.50.1: страница национализма тоже греется заранее.
					bool e = NationalismWindow.Prewarm();
					// 0.52: страница народов.
					bool f = PeoplesWindow.Prewarm();
					_windowsPrewarmed = a && b && c && d && e && f;
				}
			}
			// Конфиг перечитываем раз в секунду: смена уровня уведомлений и
			// переключателей в настройках применяется без перезапуска игры
			// (жалоба 0.17.2: «включил уровень 1, а уведомления всё равно шли»).
			float now = UnityEngine.Time.unscaledTime;
			if (now >= _nextConfigCheck)
			{
				_nextConfigCheck = now + 1f;
				ApplyConfig();
			}
            if(!NativeWorldLifecycle.TryComplete(global::World.world,World))return;
			_feeder?.Tick();
			_notifier?.Flush();
			Patches.EnsureIdeologyRows();
			IdeologyLayerButton.Tick();
			// Идеологии жителей реально влияют на мир: настроение несогласных,
			// годовое накопление недовольства (лояльность городов — через патч getLoyalty).
			IdeologyEffects.Tick(UnityEngine.Time.unscaledTime, CurrentYear(), World?.Params);
			IdeologyMechanicsEffects.Tick(World, CurrentYear());
			GameWars.Poll();
			KmudRuntime.Tick();
			PerfProbe.Begin(PerfProbe.Step.Chronicle);
			Chronicle?.Flush();
			PerfProbe.End();
		}
		catch (Exception ex)
		{
			BasicMod<RealPoliticMod>.LogError("Real Politic: ошибка в Update: " + ex);
		}
	}
}
