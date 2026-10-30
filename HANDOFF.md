# Передача: 0.25.0 — Обновление летописей

Текущая версия: 0.25.0, формат сохранения: 17. Подробная спецификация: RELEASE-0.25-RU.md.
Новые модули: ChronicleCausality, ChronicleNotebook, ChronicleExtinctionDigest, ChronicleEdition.
Смерть подтверждается Harmony-postfix с __state, личность захватывается prefix до очистки actor.data.
470 тестов; demo генерируется через tools/chronicle-demo. Реальный WorldBox здесь не запускался.
Исторические записи прежней передачи ниже не являются текущим статусом.

---

# Real Politic — состояние

## v0.24.0 — Девять статей конституции + упрощение уний (релиз собран)

- **Статьи VII–XV** встроены в конституцию, UI и пайплайн поправок:
  - `Constitution` — поля `Taxes/Trade/Faith/Levy/Land/Watch/Blood/Impeachment/Union`
    (заметь: поле податей зовётся **`Taxes`**, не `Tax`).
  - `ConstitutionAmendments.Proposals` — ужесточение/смягчение всех 15 статей
    (TaxKey/TradeKey/... в том же файле); `ConstitutionL10n` — локализованные тексты.
  - `ConstitutionWindow.Compose` — все 15 статей римскими числами.
  - `ConstitutionTexts` (в `ConstitutionGates.cs`) — русские экраны.
- **XIII «О кровных связях»**: `ConstitutionGates.BloodClaim(state, dynastyShifted,
  inheritedByClaim, clanId, royalAncestorClanIds)`. `DirectBlood` = род ушедшего дома
  (`PreviousRoyalClanId`); `AnyRoyalBlood` = любой род из венценосной истории.
  `PoliticalWorld.CheckConstitutionalSuccession` снимает клеймо узурпатора по праву крови.
  Хранится: `RoyalClanId`, `PreviousRoyalClanId`, `RoyalClanHistory` (SaveData `prevClanId`/`royalClans`).
- **XIV «Об импичменте»**: `PoliticalWorld.TickImpeachment` (вызывается из `ProcessKingdom`,
  если нет мятежа/революции). Только выборные державы (`SuccessionLaws.HasPeriodicCycle`);
  основание — `Support < 50`; перерыв `LastImpeachmentYear` (SaveData `impeachYear`).
  При успехе `state.Ruler = null` + `PolEventType.Impeached`; игровой слой
  (`WorldFeeder.OnPoliticalEvent`) отправляет свергнутого на городское попечение.
- **XV «О федеративном устройстве»**: `ConstitutionGates.UnionAdmission(law, leaderLang,
  memberLang, adjacent)` — гейт только для `UnionType.Merger` (вассалитет не судится).
  Язык — первоклассная сущность (`CityLanguageId`), не культура.
- **Модель уний**: `UnionType { Vassal=0, Merger=1 }`; `UnionMember` без `IntegrationProgress`;
  `Union.AbsorbedChildren` (снапшот границ поглощённого для права на выход 0.25);
  `PendingMerger` → игровой слой `WorldFeeder.ApplyMerger` (перевод городов через
  `CivilWarExecutor.MoveCityToKingdom`).
  **Gotcha, исправлен**: в `TryCreateUnion` Merger-ветка ПРОПУСКАЛА `return` — уния
  добавлялась дважды, а член оставался живым. Теперь `return union;` на месте.
- Тесты: **ядро 361/361, игровой слой 20/20** (`GameCheck.csproj` 0/0). SaveData **v16**.
- Релизный комплект: `release/RealPolitic-0.24.0.zip` (папка мода + свежий `RealPolitic.dll`
  0.24.0, без bin/obj), `mod.json` и `AssemblyInfo` = 0.24.0/0.24.0.0, README (унии/конституция/
  чек-лист), ROADMAP (этапы 26–28), CHANGELOG 0.24.0 — обновлены. dev source-архив прикладывается
  в `release/` по регламенту проекта.
- Открытые вопросы для следующего цикла: право на выход/сецессия (0.25, согласованы оба
  пути запуска — мирное требование и восстание; восстановление границ точно по снапшоту
  поглощения); реальная проверка в игре (Player.log).

## v0.23.2

- Боковые кнопки: `SideTabButton.Ensure(window, name, iconKey, spritePath, tooltip, MethodInfo)` — клон дочернего
  объекта `window.tabs` с `WindowMetaTab`+`Button`. Иконки мода регистрируются через
  `IdeologySprites.EnsureRegistered(key, pathId)` (embedded `RealPolitic.icons.<key>.png`, иначе `icons/<key>.png`).
  Если `tabs` нет — старые кнопки-строки. `FamilyTreeWindow.HasSideTab` решает, показывать ли строки родословной.
- События мира: `WorldEventsWatcher` (эпоха/подвиды/династические расколы), типы 144–146, ключи
  `RP_EV_ERA_CHANGED`, `RP_EV_SUBSPECIES_EXTINCT`, `RP_EV_ROYAL_RELATIVE_SECEDED`, `RP_ERA_*`, `RP_REL_*`.
- Банкротство: `EconomyInput.Gold` (−1 = неизвестно) + `SimParams.BankruptcyGoldThreshold`.
- Не проверено в игре (нужен Player.log): создание вкладок (какие компоненты реально висят на вкладке;
  `DragOrderElement`/`ScrollableButton` снимаются на всякий случай), `subspecies` API (`countAlive`/`units`),
  `era_manager.getCurrentAge().id` — фактические id эпох; `EraKey` матчит по подстроке.
- Тесты: 357 ядро + 23 игровые; сборка net48 0/0; локали 1019/1019; конфиг 72.


## v0.23.1 (хотфикс)

Источник — Player.log/chronicle.txt с 0.22.3. Gotcha: **на Mono `dict[key] = v` внутри `foreach` по этому же словарю бросает InvalidOperationException**, на .NET 8 в тестах — нет. Не писать в словарь в foreach нигде в Core. API игры 0.50+: `Kingdom.countTotalWarriors()`, `Kingdom.countTotalFood()`, `City.getTotalFood()`, `Actor.isWarrior()`, `City.makeOwnKingdom(Actor, bool pRebellion, bool pFellApart)`. При отказе `Invoke` смотреть `GameRefs.LastInvokeError`.

## v0.23.0 — Настоящие гражданские войны, либерализм, поправки к конституции

- Подробности — в `CHANGELOG.md` (раздел 0.23.0). Ключевые точки кода:
  `CivilWars.DecideResolution` (исход по миру), `PoliticalWorld.StartRebelKingdom` / `ApplyCivilWarOutcome` /
  `PendingWorldOps` + `TakePendingWorldOps()`, игра: `CivilWarExecutor.ExecuteSecession` (DeclareWar) и
  `ExecuteWorldOp`, `WorldFeeder.ExecutePendingWorldOps`. Поправки — `ConstitutionAmendments.Tick`
  (вызывается из `ProcessKingdom`, если нет мятежа/революции). Мандаты — `PoliticalWorld.SnapshotWars`/`AreAtWar`.
  Локализация ядра — `CoreText.Localizer` (ставится в `RealPoliticMod.OnModLoad`).
- SaveData v15: блок `cw` = cause/start/ideology/leader/leaderFem/leaderId/capital/parentCapital/rebelKingdom/cities;
  у державы добавлен `lam` (год последней поправки), удалён `autonomy`.
- Gotcha: `TakePendingSecessions()` НЕ должен чистить `PendingWorldOps` (была такая ошибка в разработке).
- Тесты: 351 ядро + 13 игровые.


## v0.21.2 — Этап 25c: Интерактивное древо родословной (окно, клик-навигация)

**Релиз:** 342/342 тестов, сборка net48 0/0, локали 796/796, конфиг 69, SaveData **v13**.

1. Ядро `GenealogyChart.cs`: раскладка (focus 0,0; вилка Mult 0.62; до 4 колен вверх
   + узел More «… выше»; братья/сольи рядом; дети рядами; защита от циклов placed-set).
2. Game `FamilyTreeWindow.cs`: ScrollWindow.createWindow по рефлексии; узлы — кнопки
   (клон реальной кнопки + снятие PowerButton/TipButton/HoverTip), кнопка запуска — строка «🌳 Дерево» ВНУТРИ stats_rows окна юнита
   (под RP_FAMILY_TITLE, PlaceAfterTitleRow через SetSiblingIndex).
   Клик NodeClick → Toggle(id).
3. Fallback: если ScrollWindow недоступен → текстовый блок родословной в профиле.
4. `Patches.CurrentUnitWindowActorId()` — id показанного юнита для кнопки запуска.

## v0.21.1 — Этап 25b: Родословная жителя (реестр семьи без амнезии)

**Релиз:** 336/336 тестов, сборка net48 0/0, локали 796/796, конфиг 69, SaveData **v13**.

1. Ядро `FamilyRegistry.cs`: одна запись на жителя (имя/пол/рождение/смерть/оба
   родителя/клан), `MarkMissingDead` через 3 года от пропажи (точную смерть ловит
   и хук смерти), trim листовых покойных при >20000 записей.
2. `GenealogyTree.Build(id, max 15)`: себя/родители/деды-бабки по линиям/дети с
   партнёрами/братья-сёстры; «†» и даты (род.–ум.).
3. `SaveData v13`: блок family, id строками; новый `TinyJson.GetLong`.
4. Фиддар `FamilyRegistryTick` — раз в год Обсе watch каждого юнитаа всех городов.
5. UI: `Patches.ShowFamilyBlock` в ShowMetaRowsPostfix юнита — RP_FAMILY_TITLE +
   RP_FAMILY_0..14 (точки-маркеры в локалях).

## v0.21.0 — Этап 25: Реальность власти (президент на троне, вождь мятежа, казнь главаря, женская династия)

**Релиз:** 329/329 тестов, сборка net48 0/0, локали 780/780, конфиг 69, SaveData **v12**.

1. `GameRefs`: новый блок «Этап 25» — TryPickAdultUnit/FindActorById/TryRenameActor/
   TrySetKingOnKingdom/TryAppointToCity/TryExecuteActor/TryJoinClan/TryReassignKingdom.
2. `WorldFeeder.OnPoliticalEvent` (Bus.Subscribe в конструкторе):
   - `DemocraticElection` → TryEntruoneElect: adult unit переименовать именем победителя
     + setKing; прежнего правителя TryAppointToCity (assignUnit/setLeader/changeLeader).
   - `CivilWarEnded` + `RP_CW_RESULT_LOYALISTVICTORY` → казнь главаря по `leaderActorId`.
3. `PoliticalWorld`: CivilWarEnded `.With("leaderActorId", cw.RebelLeaderActorId)`.
4. `CivilWarExecutor.ExecuteSecession`: после формирования rebelKingdom — setKing лидеру
   столицы + TryReassignKingdom(leader, rebelKingdom).
5. `GameRefs.ApplySurnameToRoyalFamily`: дети правителя (parentId match) — TryJoinClan
   в королевский клан (женская династия не обрывается).
6. `WorldPersistence.SavePostfix`: сохранение раз в год мира и не на паузе.

## v0.20.3 — Этап 24: Регентство для всех наследственных форм, выдворение «ножками»

**Релиз:** 329/329 тестов, сборка net48 0/0, локали 780/780, конфиг 68 → **69**,
SaveData **v12**.

1. `RegencyGame.Qualifies(ideo, law)` → `!HasPeriodicCycle(ideo) && !HasPeriodicCycle(law)`
   (милитаризм-стратократия включается — воспроизведение кейса user'а: код 0.20.1
   14-летняя милитаристская правительница без регента).
2. `GameRefs.InvokeWithFirstArg(target, "goTo", homeTile)` — подбор перегрузки по
   типу первого аргумента, остальные позиции дефолтами: выдворение ножками до телепорта.
3. Параметр `BorderIntruderDamage` (INT_SLIDER 0–25 = 5; 0 — без урона).
4. Фамилии детям правителя (`ActorParentIds[0/1] == kingId`).
5. Обновлены тексты уведомления/хроники выдворения.

## v0.20.2 — Этап 24: Хотфиксы аудита (границы, регентство, локали)

**Релиз:** 329/329 тестов (316 ядро + 13 игровые), сборка net48 0/0, локали 779/779,
SaveData **v12**, настройки 68.

1. **Границы**: `EnforceOneDirection` переписана на скан подданных ГОСТЯ по тайлам —
   `InsideZones` (30 кл) чужих городов; `TryGetActorTile` текущий тайл актёра с
   фолбэками. До правки сканировались `city.units` чужого города — туда чужаки не
   попадают никогда, потому механика молчала (никаких крашей, просто 0 пойманных).
2. **Вифаидация погрома**: `TryPunishActor(actor, 15)` перед выдворением; потолок
   здоровья не ниже 20.
3. **Регентство**: конфликт с `GuardDynasty` — если страж возвращал ребёнка (16–17),
   титул оставался у малолетки навсегда. `FindRoyalHeir(..., int minAge = 16)`;
   для регента — 18+. См. блок comment «Страж династии» в `RefreshRulers`.
4. **TradeRoutesSevered**: локали RP_EV_/RP_CHRON_EV_, шаблон нотифаера, секция
   хроники «ГРАНИЦЫ».
5. **Null-крашгварды**: SuccessorIdeology(epoch)/Roll/RollDissentingEpoch.

## v0.20.1 — Этап 24: Правки + хотфикс лимита сроков переизбрания

**Релиз:** 327/327 тестов (314 ядро + 13 игровые), сборка net48 0/0, локали 777/777,
SaveData **v12**, настройки 68.

1. **Хотфикс лимита сроков**: публичный баг «лидеры переизбираются более 3 раз».
   Корень: `state.Election = new ElectionState(...)` на каждой реформе/смене строя
   обнулял историю `PastPresidents`, и пожизненный лимит терялся (Демократия ↔
   Коммунизм). Хелпер `ElectionSystem.ReformedElectoralState(old, year)` переносит
   записи; подключён во все 5 точек сброса (смена при печати правителя, удавшаяся
   революция, давлениеметрическая смена, переворот повстанцев, смена идеологии
   массой). `HoldElection` страхует по личной записи: сам записанный лидер с 3+
   сроками не переизбирается никогда.
2. Эпохи мира (как в 0.20.0), фамилии физически на персоналиях, баннер 0.20.1.

## v0.20.0 — Этап 24: Правки по обратной связи

**Релиз:** 323/323 теста (310 ядро + 13 игровые), сборка net48 0/0, локали 777/777,
SaveData **v12**, настройки 64 → **68**.

1. **Эпохи мира** (корневая причина «коммунизма в пятом году» — любые идеологии
   клились во все эра): core `IdeologyEpoch.cs` (`IdeologyEpoch{DarkAges, Medieval,
   EarlyModern, Modern}`, `EraRules.OfYear(year, p)/Allowed/PoolOf/NearestAllowed`
   — ближайший по `Ideologies.Affinity`). Гейты:
   - `ActorSimulation.SuccessorIdeology(current, rng, epoch)` — новая перегрузка;
     используется в `PoliticalWorld.ProcessKingdom` при смене правителя;
   - страж анахронизмов в `ProcessKingdom ПЕРЕД основным тиком: недопустимый
     в эпохе строй мягко отодвигается к `NearestAllowed` (старое сейвы исцеляются,
     обратно в будущее не возвращаемся, эпоха растёт монотонно);
   - `ActorIdeologyRoll.Roll/RollDissentingEpoch` — инакомыслящие в пределах эпохи;
     Game-вызов: `EraRules.OfYear(RealPoliticMod.CurrentYear(), RealPoliticMod.World?.Params)`;
   - `ActorSimulation.Inherit(..., epoch = Modern)` — параметр с дефолтом «безовых»
     тестов; дикая 2%-смена убеждений через `RandomIdeology(rng, epoch)`.
   - `CivilWars.PickRebelIdeology` — по источникам из уже эпохальных граждан.
2. **Фамилии физически**: `GameRefs.ApplySurnameToRoyalFamily` (клан короля по
   `clan`-ссылке или `id`, обход `units` городов, `RenameWithSurname` пишет в
   `name` объекта и в `data`), вызов каждый год из `RenameSurnameClans` —
   идемпотентно.
3. **Баннер версии**: `RealPoliticMod.Version = "0.20.0"` + Лог при загрузке.
4. **Тесты-последствия**: `EraRestrictionTests` (9) + починка 2 старых фикстур
   (Succession теперь на году 300 с перегрузкой Modern; Stage22 fatigue на году 200
   с легальным Эгалитаризмом).
5. **Инструкция установки**: удалить папку мода перед копированием нового релиза —
   иначе часть файлов/кэша остаётся старой.

## v0.19.0 — Этап 23: Честная летопись, реальное регентство, границы и рейды

**Релиз:** 315/315 тестов (302 ядро + 13 игровые), сборка net48 0/0, локали 773/773,
SaveData **v12** (обратная совместимость с v1–v11), настройки 59 → **64**.

1. **Ядро (`src/RealPolitic.Core/`):**
   - новый файл `WarPeaceStage23.cs`: `BorderStatus{Open,Closed}`, `BorderRecord`,
     `BordersBook` (ключ `PairKey.Of` = min<<32|max; `SetStatus`, `Lock` — блокируется
     повторное ОТКРЫТИЕ, `LockedUntilBetween`, `RemoveKingdom`); `RegencyGame`
     (`MajorityAge=18`, `Qualifies` — только наследственные законы, выборы/пленумы
     нет, `PublishCrowned/PublishPassed`); `DynastyNaming` (`DynastySurname` — фамилия
     одним словом: «Эйгон Таргариен»→«Таргариен», «Вин»→«Вин»; `WithSurname` —
     без дубликата; `ApplySurname`); `PowerRaids` (`CanRaid` — технология≥уровень
     перетренировки и благосостояние врага>30; `CooldownYears=6`; `TryPowerRaid` —
     шанс 0.05+... );
   - `DeathRecord`: поля `KillerSpecies/KillerRace/KillerFemale/KillerAge` — заполняются
     в `GameDeaths.OnActorDeath` (GameText.ActorIsFemale/ActorAge, SpeciesName по
     фолбэку «неизвестный вид»); сохраняются в сейве;
   - `PolEventType` (append 130+): `KingdomCollapsed`, `RegencyCrowned`, `RegencyPassed`,
     `DynastyRenamed`, `PowerRaid`, `BorderOpened`, `BorderClosed`, `BorderExpulsion`;
   - `KingdomPolitics`: `DynastySurname`, `ClanRenamedToSurname`, `LastPowerRaidYear=-100`;
     `RegencyState` + `HeirActorId/HeirName/CrownSitsWithRegent`;
   - `PoliticalWorld`: `ApplyBorderPolicy(a,b,status)→bool` (публикует BorderOpened/Closed;
     `Open` заблокировано при `LockedUntilYear>Year`), `ReportBorderIntrusion` (событие +
     `Lock(BorderLockYears)`), `EnsureDynastySurname` (RoyalClanName — зеркало игрового
     клана: меняем только если old!=surname и непуст; событие `DynastyRenamed`),
     `TryPowerRaid`/`ApplyPowerRaidOutcome` (публикует `PowerRaid`, удар по
     благосостоянию/легитимности жертвы, обида WarCrime=? — через гривенс),
     `CollapseTracker` (≥3 фрагмента от одного прородителя в 3-летнем окне → один
     `KingdomCollapsed` со списком осколков; повторы на 6/9/… в окне не шумят);
   - `SaveData` v12: секция `borders:[{a,b,s,u,l}]`, поля `dsn`/`cln`/`lspr`, регентский
     `heir`/`heirName`/`heirCrowned`, `kf`/`ka` убийцы; v1–v11 — дефолты (границы открыты,
     фамилий нет).
2. **Игровой слой (`src/Game/`):**
   - `GameRefs` (+5): `RenameRoyalClan(Kingdom,string)`, `TryCallGodPowerBomb(powers,x,y,tile)`,
     `TryExpelActor(actor,homeTile)` (телепортация падшею положения), `TrySetProperty`,
     `BuildArgs`; `MethodExists`;
   - `GameRefs.RefreshRulers`: физическое регентство — `setKing(regent)` для <18 с
     подтверждением `ReferenceEquals(ReadRuler,regent)`; ежегодный обратный `setKing`
     наследнику в 18; наследник мёртв/null → `Regency.End()`;
   - `WorldFeeder.ApplyStage23Effects` (Tick после `ApplyDeepDiplomacyEffects`):
     `BorderAutoPolicy` (война/обида≥25 → Closed + `Trade.BlockBetween` + событие
     `TradeRoutesSevered`; союз/торговля-мир → Open), `EnforceClosedBorders`
     (`TryExpelActor` + `ReportBorderIntrusion`, максимум 3 записи на пару/год),
     `RunPowerRaids` (TryPowerRaid→TryCallGodPowerBomb→ApplyPowerRaidOutcome, один
     рейд/год/держава; при недоступном API — документный налёт без экономии урона),
     `RenameSurnameClans` (разово → `ClanRenamedToSurname`);
   - `GameNotifier`: +8 шаблонов новых событий; `ConfigReader` +5 чтений;
     `default_config.json` — General = 64 записи.
3. **Локали:** 773/773 (ru/en паритет 100%) — RP_EV_/RP_CHRON_EV_* всех 8 типов +
   стат-строки `RP_CHRON_STATS_COLLAPSES/RAIDBORDERS`.
4. **Тесты:** `Stage23Tests` (16): досье убийцы save/load, правила фамилий и
   уникальность переименования, отбор регентства по законам, события короны,
   тик-открытие/закрытие границ и замок, отказ при `EnableBorders=false`, prune при
   смерти державы, триггер распада≥3 и молчание в окне, гейты авиаударов и исход,
   save v12 roundtrip + дефолты старых сейвов. `IdeologyStatCheckTests` (4):
   матрица 12×12 дипломатического советника — этнонационализм > всех пацифистов,
   милитаризм ≥ всех, регентство обнуляет войны, таблица `WarAppetite` в порядке.
5. **Грабли этапа:** `string.Contains(string, StringComparison)` нет в net48
   (`IndexOf(...)>=0`); `TinyJson` только `GetInt` (long-поля неявно расширяются);
   `edit_file` по большим блокам WorldFeeder/GameRefs давал мусорные скобки —
   перед правкой снимать фрагмент `sed -n`, после — grep баланса.

## v0.18.0 — Этап 22: Война и мирные договоры

**Релиз:** 285/285 тестов (272 ядро + 13 игровые), сборка net48 0/0, локали 745/745,
SaveData **v11** (обратная совместимость с v1–v10), настройки 55 → **59**.

1. **Ядро (`src/RealPolitic.Core/`):**
   - новый файл `WarPeace.cs`: `SettlementLevel{WhitePeace, Humiliation, Reparations,
     LandDemand}`, `PeaceSettlement` (со `TermsLocKey` → `RP_WARTERMS_FULL/_PARTIAL/
     _HUMILIATION` — значения локализуются GameNotifier'ом автоматически, см. ниже),
     `CityDemandOrder{FromId,ToId,Count}`, `ReparationsOrder{PayerId,PayeeId,
     AmountPerYear,YearsLeft}`, `TruceBook` (ключ пары = min<<32|max, `Set/Until/
     Active/Clear/Expire`), `WarFatigueDefs` (множитель по идеологии: милитаризм 0.6 …
     эгалитаризм 1.5; `RebellionFactor(fatigue)` — ×(1+(f−50)/100) свыше 50);
   - `PolEventType.cs`: `PeaceSettlement = 125`, `WhitePeace = 126`, `TruceBroken = 127`,
     `TruceExpired = 128`;
   - `SimParams.cs` (+6): `EnableWarPeace` (true), `WarFatigueRate` (7f),
     `WarFatigueDecay` (9f), `TruceYears` (10), `MaxCededCities` (3),
     `ReparationsYears` (8);
   - `KingdomPolitics.cs`: поле `WarFatigue` (0–100, не сериализуется при 0);
   - `PoliticalWorld.cs`: тик усталости в `ProcessKingdom` **до** `TickEconomy`
     (рост rate×IdeologyFactor в войне, спад 9 в миру; ≥60: −1 легитимности и +0.01
     недовольства в год, ≥85: −2/−1 поддержки); `ResolveWarEnd(attackerId, defenderId,
     winnerId, warYears)` → `PeaceSettlement?` (guard: EnableWarPeace, обе державы живы,
     winnerId ≠ 0 || белый мир): счёт = 20 + 10·warYears + (legitVictor − legitDefeated)/5
     + 15 если победил атакер с CB Conquest/Reconquest (берётся из `_warDeclarations`
     через `LastCasusBelli`, по умолчанию Conquest); уровни ≥70&&landHungry /
     ≥45 / ≥25 / white peace; унижение −15/−12/−8 легитимности (земли/репарации/только
     унижение), победителю +5; обида Conquest 40 либо War 25; репарации = 35%
     `LastIncome` проигравшего (мин. 12); города = max(1, min(MaxCeded, warYears/2));
     **перемирие ставится всегда** (`TruceBook.Set(pair, Year + TruceYears)`), даже при
     белом мире; усталость обеих сторон ×0.5; события `PeaceSettlement` (Major,
     параметры `victor/defeated/terms/cities/gold/years/war_years/truce`) и `WhitePeace`
     (Minor, `kingdom/partner/years/truce`);
   - truce-violation блок в `ApplyDeclareWar` (после потери договоров): −15 легитимности,
     −10 поддержки, +12 угрозы, −40 отношениям с жертвой, обида TreatyBroken 40,
     событие TruceBroken (Major); истечение — цикл в `TickDiplomacyDeep` после Decay
     обид → TruceExpired (Trivial);
   - `RemoveKingdom` чистит `_truces/_reparations/_pendingCityDemands`; `Clear()` — тоже;
   - `CollectReparations()` → `List<TributeOrder>` (переиспользуется тип с дани!),
     декремент `YearsLeft`, удаление нулевых; при `!EnableWarPeace` — пусто;
   - `CivilWars.StartChance`: ×`WarFatigueDefs.RebellionFactor` сразу после InWar ×1.3;
   - `KingdomStrategy.cs`: снапшот + поле `WarFatigue`; `SeekPeace` пересчитан:
     `AtWar && (Legitimacy < 45 || Fatigue ≥ 55)` → 60 + 0.35·Fatigue + 0.4·max(0, 45−Legitimacy);
   - `SaveData.cs`: версия **11**; у державы `wf` (только если > 0); секции
     `truces:[{a,b,u}]` (a/b — части ключа; `b = key & 0xFFFFFFFF`, при чтении обратно
     `(a<<32)|b`), `reps:[{p,t,a,y}]` — пишутся/читаются **до** `alls` (альянсов);
     v1–v10 грузятся с нулевой усталостью и пустыми секциями (тесты на это есть).
2. **Игровой слой (`src/Game/`):**
   - `GameWars.OnWarEnded`: достаёт `getMainAttacker/getMainDefender`, НЕ только имена,
     но и `id` (−1 при неудаче), в try/catch зовёт `RealPoliticMod.Feeder?.NotifyWarEnded(
     attackerId, defenderId, winner, startYear)` — ошибки мирных договоров не должны
     ломать завершение войны;
   - `WorldFeeder.NotifyWarEnded(...)`: `WarWinner.Attackers → attackerId`,
     `Defenders → defenderId`, else 0 (Nobody/Peace/Merged — победителя нет);
     `years = max(1, _world.Year − warStartYear + 1)`; → `ResolveWarEnd`;
   - `WorldFeeder.TransferCededCities(fromId, toId, maxCount)`: список городов проигравшего
     сортируется по лояльности по возрастанию (`getLoyalty(false)` с фолбэком 50);
     столица (`GameRefs.KingdomCapital ?? cities[0]`) пропускается; стоп, когда остался
     бы ≤ 1 город (`cities.Count − moved <= 1`); перенос цепочкой рефлексии
     (в духе 0.17.1): `joinAnotherKingdom(to,false,false)` → `setKingdom(to,false)` →
     `changeKingdom` → `joinKingdom` → `toKingdom.addCity`; успех — результат != null
     ИЛИ `city.kingdom == toKingdom` после вызова;
   - `ApplyDeepDiplomacyEffects`: после дани — `CollectReparations()` → тот же
     `TransferGold`; после `PendingCityTransfers` — `PendingCityDemands` →
     `TransferCededCities` (+ `ClearPendingCityDemands`);
   - `FindWarTarget`: `TruceActiveBetween` — continue сразу после NonAggression-проверки;
   - снапшот стратегии: `WarFatigue = state.WarFatigue`;
   - `GameNotifier`: 4 шаблона; **важно**: параметры со значением, начинающимся с `RP_`,
     уже авто-локализуются `LocalizeKeyParams` — потому `terms` в событии держит
     ключ, а не текст;
   - `Chronicle`: рубрика «МИРНЫЕ ДОГОВОРЫ», счётчики `_peaceSettlements/_whitePeaces/
     _trucesBroken/_citiesCeded` (+ пары `_y*`, ResetStats/UpdateYearBaseline), `cities`
     парсится `int.TryParse(evt.Get("cities"))`; 4 детальные строки через `L(...).Replace`
     (плейсхолдеры, которым нет значения, остаются в тексте-фолбэке — `LocalizedKeyParam`
     локализует вложенные ключи `RP_WARTERMS_*`, у которых свои {cities}/{gold}/{years});
     годовая строка `RP_CHRON_STATS_WARPEACE` (3 пары — Fmt2 максимум 3) + отдельная
     `RP_CHRON_STATS_TRUCES` (нарушенные перемирия, 1 параметр);
   - `ConfigReader`: 4 чтения после `EnableFullUnionReform` (`ReadFloat`→int для
     INT_SLIDER); `default_config.json` +4 записи → General = 59.
3. **Локали:** +17 ключей (745/745): 4 `RP_EV_*`, 4 `RP_CHRON_EV_*`, 3 `RP_WARTERMS_*`,
   2 `RP_CHRON_STATS_*`, 4 конфиг-метки. Паритет проверен скриптом.
4. **Тесты (`Stage22Tests.cs`, 21):** хелпер `TwoKingdomWorld` копирует паттерн Ep21
   (Rng=42, Baseline, SetYear(5), AddKingdom/AddCity, один мирный YearTick); войны
   гоняются через `TickWar(world, years, atWar)` со снапшотами `AtWar`+Relation.War;
   пункт точности: `AvgDissent` пересчитывается тиком (UpdateCensusDerived), поэтому
   эффекты усталости проверяются **дельтой против второй державы**, а не абсолютным
   значением. Остальное: тиры договора (защита земель от победы защитника и CB Defense),
   белый мир при счёте < 25 / winnerId=0, жизненный цикл перемирия и нарушения,
   репарации по годам до исчерпания, prune при RemoveKingdom, save/load v11, планер.
5. **Грабли, свойственные этапу:**
   - `ProbablyWarEnd` с `winner=Merged` (аннексия): ResolveWarEnd вернёт settlement,
     только если обе державы ещё живы и `winnerId ≠ 0`; `Merged → winnerId = 0` — корректно
     «без победителя», земли уже у победившей стороны по игровой механике слияния;
   - для WarEnded GameWars публикуется старым локальным событием `RP_CHRON_EV_WAR_END`,
     мирные договоры — отдельным событием **после** него (порядок записей в летописи:
     «война завершилась» → «мирный договор / белый мир»);
   - WorldBox в песочнице недоступен: проверка вживую (рубрики, перенос городов, сейв) —
     ручной чек-лист ROADMAP пп. 14–15.

## v0.17.0 — Этап 21: Альянсы, глубина уний и интеграция

1. **Ядро (`src/RealPolitic.Core/`):**
   - новый файл `Alliance.cs`: `AllianceRecord{Id,Name,LeaderId,Members}`, `AllianceBook`
     (`Of`, `ByName`, `Replace`, `Clear`);
   - `PolEventType.cs`: `AllianceFormed = 115` … `UnionReformed = 124`;
   - `SimParams.cs` (+8): `TrackAlliances` (true), `UnionInheritanceChance` (0.5),
     `InstitutionPointsPerMember` (0.5), `InstitutionBaseCost` (3), `EnableIntegration` (true),
     `IntegrationSpeed` (0.1), `IntegrationApprovalDrain` (0.02), `EnableFullUnionReform` (true);
   - `Union.cs`: `InstitutionType` (Trade/Defense/Integration), `FederationInstitutions`
     (уровни 0–3, `Total`, `Maxed`, `Raise`, `LocKey` → `RP_UNION_INST_*`), у унии —
     `Institutions` и `InstitutionPoints`; `UnionMember.IntegrationProgress`;
   - `PoliticalWorld.cs`:
     - `SetAllianceSnapshot(list)`: сверка по имени альянса — создание/роспуск/вступление/выход;
       первый снимок до базовой линии и после загрузки пишется молча (`_allianceBaseline`,
       `MarkAllianceBaseline`);
     - `TickUnionDepth()` в `YearTick` после `ProcessUnions`: институты (очки, стоимость
       `base × (1+Total)`, порядок при равенстве: торговля → оборона → интеграция),
       `TryReformFullUnion` (институты 3/3/3 + легитимность лидера ≥ 60 → `FullUnion`,
       +10 легитимности, +15 отношениям участников), `TickIntegrations` (прогресс
       `IntegrationSpeed + 0.05×уровень`, одобрение −`IntegrationApprovalDrain`,
       по завершении участник исключается, `PendingCityTransfers(member→leader)`,
       уния ≤ 1 распускается);
     - `StartIntegration(leader, member, out result)`: стражи (лидер, не персональная уния,
       федерации нужен институт интеграции, легитимность ≥ 50), −15 легитимности,
       −0.1 одобрения, прогресс 0.001 (маркер старта);
     - `TryInheritUnionCrown(state)`: вызывается в `HandleSuccession` **после**
       публикации события преемственности (чтобы династическая логика не затёрла
       унаследованный дом); шанс = база + лидер/400 + одобрение×0.2 (+0.2 полная уния),
       бросок `DynastyRng`; успех — дом лидера участнику, +10 отношения; провал — выход,
       −20 отношения, обида 15 `BrokenUnion`, роспуск при ≤ 1;
     - `QueueDefenseCalls`: федерация с институтом обороны — шанс `0.25×уровень`,
       что остальные участники получат призыв против агрессора (не члена федерации);
     - `PartnerTradeMultiplier`: федерация/полная уния + `0.05×уровень торговли`;
     - `PendingCityTransfers` + `ClearPendingCityTransfers`;
   - `KingdomStrategy.cs`: `KingdomActionType.IntegrateMember`; снимок +8 полей
     (`IntegrationEnabled`, `InUnion`, `IsUnionLeader`, `UnionType`, `UnionMemberCount`,
     `UnionIntegrationLevel`, `IntegrationCandidateId`, `IntegrationCandidateApproval`);
     оценка `40 + одобрение×20` (лидер ≥ 65, не в войне, не персональная уния,
     федерации нужен институт);
   - `SaveData.cs` **v10** (`FormatVersion = 10`): унии +`inst{t,d,i}`/`pts`,
     участники +`g`; секция `alls[{n,l,m[]}]`; чтение помечает базовый снимок альянсов.
2. **Игровой слой (`src/Game/`):**
   - `WorldFeeder.cs`: `ReadAllianceRecords()` — рефлексия `World.world.alliances` →
     `getAlliances()`/`alliances_list`/`p_alliances`, у альянса `name`/`p_name`,
     `getMembers()`/`kingdoms`/`p_kingdoms`, `getLeader()`/`leader` (при недоступности —
     пустой список без ошибок); вызов `SetAllianceSnapshot` перед `YearTick`;
     `FindIntegrationCandidate` (минимальное одобрение, не лидер, не в прогрессе);
     исполнение `IntegrateMember` → `StartIntegration`; `TransferKingdomCities` —
     перевод городов завершённой интеграции (`setKingdom`/`changeKingdom`/`joinKingdom`/
     `addCity`, тот же перебор, что в `CivilWarExecutor`);
   - `GameWars.cs`: при начале войны ищет альянс защитника в `AlliancesBook` и добавляет
     параметр `allies`;
   - `GameNotifier.cs`: 10 шаблонов (115–124), все с заглавной буквы;
   - `Chronicle.cs`: рубрики «АЛЬЯНСЫ» (115–118) и «ИНТЕГРАЦИЯ» (122–123), 119/120/121/124 —
     «УНИИ»; детальные строки через `Fmt` (лидер/участник и ключи RP_* раскрываются
     централизованно до свитча); строка `WarStarted` + «за державу вступается альянс»
     (`RP_CHRON_WAR_ALLIES`); 6 пар счётчиков + строки сводок
     (`RP_CHRON_STATS_ALLIANCES`, `RP_CHRON_STATS_UNION_DEPTH`/`_YEAR`);
   - `ConfigReader.cs`: +8 чтений; `default_config.json` — 55 записей.
3. **Локали:** `Locales/ru.json` и `en.json` по 728 ключей (+28), паритет 100%.
4. **Проверка:** **246/246 тестов ядра** (`Stage21Tests.cs` — 19 тестов;
   `EconomyTests.SaveData_FormatVersionIsTen`), сборка net48 — 0 ошибок, 0 предупреждений.
5. **Особенности реализации:** бросок наследования короны идёт ПОСЛЕ династической логики
   преемственности (иначе клан нового правителя затирает унаследованный дом); первый снимок
   альянсов молчалив только до `BaselineRecorded` или при загрузке из сейва; институты
   не растут сверх 3, очки при максимуме копятся впустую; интеграция персональной унии
   запрещена по смыслу (корона уже связана).
6. **Ручная проверка в игре (очередь пользователя):** пункты 12–13 чек-листа ROADMAP —
   альянсы в летописи, наследование короны и кризисы, институты федерации, реформа в полную
   унию, интеграция с переводом городов, сейв/лоад формата v10. Плюс незакрытая очередь
   этапов 19–20.

## v0.16.0 — Этап 20: Глубокая дипломатия

1. **Ядро (`src/RealPolitic.Core/`):**
   - новые файлы: `Treaty.cs` (`TreatyType`: NonAggression/TradeAgreement/DefensePact,
     `Treaty{Type,PartyA,PartyB,SignedYear,ExpiresYear}`, `TreatyBook`: `Between`/`Of`/
     `NonAggressionActive`/`Add`/`Remove`/`Count`, `Treaty.TypeLocKey` → `RP_TREATY_*`);
     `CasusBelli.cs` (`CasusBelliType`: Conquest/Reconquest/Revenge/BrokenTreaty/
     IdeologicalLiberation/Defense/Independence; `CasusBelliDefs.Best(grievance, claims,
     affinity, treatyBroken)`; цены объявления и обида жертвы; `GrievanceReason`/
     `GrievanceBook` (ключ пары `from<<32|to`, `Decay(4f)`/год); `WarDeclarationRecord`);
     `SpyOps.cs` (`SpyOpType`: Sabotage/InciteDissent/StealGold; `SpyOpDef{BaseSuccess,
     ExposureOnFail}`; `SpyState{Level,LastOpYear,LastInvestYear}`; `MaxNetworkLevel = 3`);
     `Coalition.cs` (`Coalition{Id,TargetId,FormedYear,LastWarYear,Members}`,
     `CoalitionBook`: `Against`/`MemberOf`/`OtherMembers`/`Restore`, `TributeOrder`);
   - `PolEventType.cs`: `TreatySigned = 105` … `VassalRebellion = 114`;
   - `SimParams.cs` (+7): `EnableTreaties`, `TreatyYears` (20), `EnableCasusBelli`,
     `EnableEspionage`, `EnableCoalitions`, `VassalTributeRate` (0.1),
     `VassalRebellionApproval` (0.3);
   - `PoliticalWorld.cs`:
     - `TrySignTreaty(a,b,type,atWar,out result)`: шанс 0.6 + отношения/200 + 0.1 НАП
       ± 0.05 сродство; пакт требует отношения ≥ 10; успех +25/+10 отношение… (+25 пакт,
       +15 прочие), срок `Year + TreatyYears`;
     - `BreakTreaty(violator,victim,treaty,reason)`: −10 легитимности, +10 угрозы,
       −30 отношения, обида жертве +30; вызывается из `TickDiplomacyDeep` (истечение)
       и при нарушении; `ApplyDeclareWar` рвёт действующие договоры пары **до** записи войны;
     - `ApplyDeclareWar(attacker,defender,cb)`: цены казус белли, обида жертвы
       (завоевание → причина Conquest, иначе War; величина `def.VictimGrievance`),
       `WarDeclarationRecord`, `QueueDefenseCalls`, событие `CasusBelliDeclared`;
     - `QueueDefenseCalls(defender,attacker)`: партнёры по оборонительному пакту
       (пакт **списывается** как исполненный) + члены коалиции против агрессора
       (`LastWarYear = Year`), событие `CoalitionWar`;
     - `AggressionScoreOf`: окно 10 лет по `WarDeclarations` (завоевание 30, нарушенный
       договор 15, идеологическое 12, месть 10, возврат 8, оборона/независимость 0);
     - `InvestEspionage(id,gold,out result)`: уровень < 3, ≥ 2 лет с вложения, 25 золота;
     - `TryCovertOp(actor,target,op,out result)`: сеть ≥ 1, кулдаун `SpyCooldownYears` (5);
       успех по `Rng.NextDouble() < SpyOps.SuccessChance(op,level)`; провал → бросок
       разоблачения `ExposureOnFail` (сеть −1, −8 легитимности, −40 отношения, обида 35);
       кража золота ставит `PendingGoldTransfers` (переводит игровой слой);
     - `TickCoalitions` в `TickDiplomacyDeep`: формирование при агрессии ≥ 40
       (учредители: обида ≥ 20 или отношения ≤ −20, ≥ 2), роспуск при цели-нет или
       `Year − LastWarYear ≥ CoalitionPeaceYears` (8); истечение договоров до инкремента года;
     - `CollectTributes()`: приказы дани `VassalTributeRate × LastIncome` (> 0),
       одобрение вассала −0.01; `LeaveUnionForIndependence(vassal,out result)`: выход,
       уния ≤ 1 распускается, −30 отношения, сюзерену обида BrokenUnion 25,
       событие `VassalRebellion`;
   - `KingdomStrategy.cs` (+16 полей снимка, +5 действий): `SignTreaty` (TreatyScore),
     `DeclareWar` (WarScore + WarBias + (Ambition−50)×0.2), `InvestEspionage`
     (33 + 2×уровень), `CovertOperation` (CovertScore), `DeclareIndependence`
     (70 + (порог − одобрение)×100), буст `Mobilize` (48 + ArmyRatio×10) вассалу
       воюющего сюзерена при армии < 80%;
   - `SaveData.cs` **v9** (`FormatVersion = 9`): `spy{l,o,i}` на державу, `treaties`,
     `griev`, `coals`, `wars`; пустые секции не пишутся; `Apply` читает секции
     опционально (миры v1–v8 без изменений).
2. **Игровой слой (`src/Game/`):**
   - `WorldFeeder.cs`: снимок +16 полей (договоры/обиды/казус белли/шпионаж/коалиции/
     вассалитет); исполнение `SignTreaty`/`DeclareWar`/`InvestEspionage`/`CovertOperation`/
     `DeclareIndependence`; `ApplyDeepDiplomacyEffects` (дань через `TransferGold`,
     призывы обороны через реальные объявления войны); кандидат на договор (пакт при
     опасном соседстве и доверии, иначе торговое соглашение/ненападение), цель войны
     (лучший казус белли, сила, обиды; пропуск цели под НАП), цель тайной операции;
     советник `DeclareWar` учитывает `NonAggressionActive`;
   - `GameNotifier.cs`: 10 шаблонов (105–114) с параметрами `{kingdom},{partner},{treaty},
     {target},{cb},{spy},{op},{count},{ally},{suzerain}`;
   - `Chronicle.cs`: рубрики «ДОГОВОРЫ»/«ШПИОНАЖ»/«КОАЛИЦИИ» (`CategoryOf`), детальные
     строки через `LocalizedKeyParam` (`PolEvent.Render` не переводит вложенные ключи!),
     10 счётчиков в сводках (`RP_CHRON_STATS_DEEPDIPLO_WAR/_SPY` из-за лимита `Fmt2` —
     не больше 3 пар ключ/значение на вызов);
   - `ConfigReader.cs`: +7 чтений; `default_config.json` — 47 записей.
3. **Локали:** `Locales/ru.json` и `en.json` по 700 ключей (+56), паритет 100%.
4. **Проверка:** **227/227 тестов ядра** (`DiplomacyDeepTests.cs` — 26 тестов;
   `EconomyTests.SaveData_FormatVersionIsNine`), сборка net48
   (`tools/release-build/RealPolitic.Release.csproj`) — 0 ошибок, 0 предупреждений.
5. **Особенности тестов:** подписание договора и тайные операции зависят от `Rng` —
   хелперы `SignUntilSuccess`/`FindCovertSeed`/`FindExposedSeed` перебирают зёрна и
   сбрасывают `Spy.LastOpYear`; `YearTick` инкрементирует год внутри тика, а истечение
   договоров проверяется до инкремента; оборонительный пакт списывается при призыве —
   в круговороте сохранения после войны подписывается новый договор.
6. **Отложено на Этап 21:** наследование короны в персональной унии без брака, институты
   федерации, интеграция/поглощение, тип `FullUnion`; **хроника ванильных альянсов**
   (детекция переходов мир→альянс/альянс→мир между тиками, имя альянса из
   `AllianceManager`, события `AllianceFormed/AllianceBroken/AllianceJoined`,
   рубрика «АЛЬЯНСЫ», строки «вступил в войну за союзника» — сейчас альянсы видны
   только как отношение `RelationType.Alliance` в снимках и как бонус к торговле).
7. **Ручная проверка в игре (очередь пользователя):** пункты 9–11 чек-листа ROADMAP —
   договоры/казус белли/коалиции в летописи, дань и мобилизация вассала, война за
   независимость, сейв/лоад формата v9.

## v0.15.0 — Этап 19: Династии и личности правителей

1. **Ядро (`src/RealPolitic.Core/`):**
   - новый файл `RulerPersonality.cs`: `RulerTrait` (None + 8 черт), `RulerTraits` — единственная
     таблица модификаторов (расходы: скряга ×0.9, мот ×1.12; годовой дрейф легитимности/поддержки/
     недовольства; уклоны войны/дипломатии/брака; `LocKey` → `RP_TRAIT_*`); `RulerPersonality.Roll`
     (черта 1..8, амбиции 0..100) и `AmbitionWarBias`/`AmbitionDiplomacyBias`; `MarriageBond{PartnerId,Year}`;
   - `KingdomPolitics.cs`: `Personality` и `Marriage` (после `DynastyStartYear`);
   - `PolEventType.cs`: `DynasticMarriage = 104`;
   - `SimParams.cs`: `EnableDynasties` (true), `MarriageBaseChance` (0.6), `MarriageMinAge` (16);
   - `PoliticalWorld.cs`:
     - `_rulerRelations` (ключ пары `min<<32|max`, clamp ±100) + API `GetRulerRelation`/
       `ChangeRulerRelation`/`SetRulerRelation`/`RulerRelations`;
     - **`DynastyRng` — отдельный случайный поток** для бросков личности/браков: не смещает
       основную последовательность политических бросков (детерминизм старых тестов сохранён);
     - `TickDynasties` после `HandleSuccession` в `ProcessKingdom`: добор личности старым мирам,
       годовой дрейф (война −3, альянс +1), война рвёт брак (штраф −40 применяется к паре **один раз** —
       второй супруг лишь фиксирует разрыв), чистка асимметричных уз;
     - `HandleSuccession`: при воцарении бросается личность; в ветке смены династии дом супруга
       получает претензию `0.5 + отношения/400` → наследует корону (−10 легитимности, +0.08 недовольства,
       +10 угрозы; иначе жёсткие −25/+0.20/+30) и **персональная уния создаётся принудительно**
       (`RealPolitic.Core.Unions.Create` + `AddMember`, минуя `CanJoin`: корона не просится в союз),
       кэш `state.Union`/`spouse.Union` выставляется сразу; брак расторгается после разрешения претензии;
     - `TryMarry(aId, bId, atWar, out result)`: стражи (монархии, возраст, война, существующие узы,
       одна уния), шанс = база + отношения/200 + брачные уклоны черт/200 ± идеологическое сродство 0.1;
       успех → двусторонний `MarriageBond`, +40 к отношениям, событие `DynasticMarriage` (`Major`,
       параметры `kingdom`/`partner`/`ruler`/`spouse` через `KingdomNameOf`);
   - `Economy.cs`: `Expenses(..., float personalityMultiplier = 1f)` (старые вызовы работают),
     `Tick` передаёт `RulerTraits.ExpenseMultiplier(личность)`;
   - `KingdomStrategy.cs`: `KingdomActionType.ProposeMarriage`; в снимке `Trait`/`Ambition`/
     `DynastiesEnabled`/`MarriageCandidateId`/`MarriageCandidateWar`/`MarriageScore`; планировщик
     подмешивает `WarBias + (амбиции−50)×0.2` в мобилизацию и `DiplomacyBias + (50−амбиции)×0.1`
     в поиск союза;
   - `SaveData.cs`: **формат v8** — у державы `pers{t,a}` и `mrg{p,y}`, на верхнем уровне
     `rels[{a,b,v}]` (пустой список не пишется); чтение терпит отсутствие секций (миры v1–v7).
2. **Игровой слой (`src/Game/`):**
   - `WorldFeeder.FindMarriageCandidate` — лучший сосед-монархия (свободен, брачный возраст,
     не в войне, не в одной унии; оценка = 40 + отношения/2 + сродство×10) и заполнение снимка
     (`Trait`, `Ambition`, `DynastiesEnabled`, `MarriageCandidateId/War`, `MarriageScore`);
   - исполнение `ProposeMarriage` в `TryExecuteStrategicState` → `world.TryMarry(...)` — действие
     чисто политическое, игровой API не трогаем;
   - `TryGetPersonalitySummary` → строка «Правитель» в окне государства (`Patches.cs`, ключ
     `RP_PERSONALITY_TITLE`, настройка `ShowDynastyUI`);
   - `GameNotifier` — шаблон `RP_EV_DYNASTIC_MARRIAGE` (с заглавной буквы, с именами держав);
   - `Chronicle` — рубрика «ДИНАСТИИ», детальная строка `RP_CHRON_EV_DYNASTIC_MARRIAGE`,
     счётчик `_marriages` в годовой сводке (`RP_CHRON_STATS_MARRIAGES`) и накопительной статистике;
   - `ConfigReader` — чтение `EnableDynasties`/`MarriageChance`/`MarriageMinAge`;
   - `RealPoliticMod` — флаг `ShowDynastyUI`.
3. **Настройки (`default_config.json`):** +4 (всего 40) — `EnableDynasties`, `MarriageChance`
   (0.2–0.9, 0.6), `MarriageMinAge` (14–20, 16), `ShowDynastyUI`.
4. **Локали:** по 644 ключа (+24: `RP_TRAIT_*` ×9, `RP_EV_DYNASTIC_MARRIAGE`,
   `RP_CHRON_EV_DYNASTIC_MARRIAGE`, `RP_CHRON_STATS_MARRIAGES`, `RP_PERSONALITY_TITLE`,
   `RP_AMBITION_SHORT`, `RP_MARRIED_SHORT`, `RP_STRATEGY_PROPOSEMARRIAGE`, 8 ключей настроек),
   паритет 100%, плейсхолдеры совпадают.
5. **Тесты:** 201/201 (`DynastyPersonalityTests.cs` — 11 тестов; `EconomyTests` переименован
   `SaveData_FormatVersionIsEight`; в `ModernIdeologyAndSuccessionTests` порог недовольства смены
   династии 0.20 → 0.18 с комментарием про дрейф личности). Сборка net48 — 0 ошибок, 0 предупреждений;
   релизный DLL 368640 байт, проверен строками (`TryMarry`, `TickDynasties`, `RP_EV_DYNASTIC_MARRIAGE`).
6. **Очередь проверки в игре (сторона пользователя):** строка «Правитель» в окне монархии;
   уведомление и запись летописи о браке; персональная уния после смерти династии при браке;
   настройки `EnableDynasties=false` и `ShowDynastyUI=false`; загрузка мира из 0.14.2 (формат v7)
   без потери данных.
7. **Дальше — Этап 20 (глубокая дипломатия):** казус белли, договоры, шпионаж, коалиции
   + отложенная из 0.14.1 глубина вассалитета (дань, военные обязательства, наследование короны
   в персональной унии без брака, интеграция, `FullUnion`).

## v0.14.2 — Экономические уведомления: читаемый текст и честная частота

1. **Ядро:** `Economy.StartCrisis` публикует событие начала кризиса с ключом шаблона
   `RP_EV_ECO_CRISIS_STARTED` (раньше ключом события был ключ имени кризиса
   `RP_ECO_CRISIS_*`, из-за чего уведомление целиком заменялось голой фразой
   «экономический спад»); серьёзность по типу: спад — `Trivial`, депрессия/банкротство/
   крах торговли — `Major`. `EndCrisis` — `Trivial` (окончание кризиса больше не спамит).
   При `NotifyLevel = 1` (по умолчанию) `Trivial`-события фильтр `NotificationCenter`
   не проходят и остаются только в летописи; уровень 0 возвращает всё.
2. **Локали:** новый ключ `RP_EV_ECO_CRISIS_STARTED` (ru/en), `RP_CHRON_EV_ECO_CRISIS`
   переписан без тавтологии («В государстве X — экономический спад: …»); по 620 ключей,
   паритет 100%.
3. **Тесты:** 190/190 (в `EconomyTests` +2: спад = `Trivial` с ключом шаблона и именем
   кризиса в `{crisis}`; банкротство = `Major`; окончание кризиса заассерчено `Trivial`).
4. **Сборка:** net48 — 0 ошибок, 0 предупреждений; DLL в релизном архиве пересобрана
   из актуальных исходников (проверено декомпиляцией).

## v0.14.1 — Унии: рост состава, торговый бонус, честное сохранение

1. **Ядро (`src/RealPolitic.Core/`):**
   - `Union.cs`: `NameKey` (ключ локализации имени) и `DisplayNameOrKey`;
   - `UnionRules.cs`: `MaxMembers = 5`;
   - `Unions.cs`: `CanJoin` отказывает при полном союзе (`RP_UNION_TOO_LARGE`);
   - `PoliticalWorld.cs`: новый `TryJoinUnion(union, candidateId, snap, out check, candidateReligionId)`
     (проверяет существование лидера и кандидата, членство в другой унии, легитимность лидера,
     религию для персональной унии, затем `Unions.CanJoin` → `AddMember` → событие
     `UnionMemberJoined` с параметрами `union`/`member`/`leader`/`count`);
     `TryCreateUnion` получил параметр `nameKey` — ключ ставится **до** публикации события,
     иначе событие уносило пустое имя; `BuildEconomyInput` читает союз партнёра через
     `UnionOf(otherId)` и заполняет `TradeMultiplier` через новый `PartnerTradeMultiplier`;
     `TryFormDynasticUnion` отдаёт ключ `RP_UNION_NAME_DYNASTIC` и не подделывает `Warriors = 50`;
   - `Economy.cs`: `EconomyPartner.TradeMultiplier` (ноль = «не задан», тогда работает прежний
     бонус альянса) и его применение в `TradeIncome`;
   - `SaveData.cs`: формат **v7** — участник унии объектом `{id, a, j}`, у унии поле `nk`;
     чтение понимает и объект (v7), и голое число (v1–v6).
   - **Слот `PolEventType.UnionMemberJoined = 41` уже существовал** и был подключён к счётчикам
     и рубрикам `Chronicle`, но никогда не публиковался; новый тип не заводили.
2. **Игровой слой (`src/Game/`):** `WorldFeeder.TryFormUnion` при существующей унии лидера
   вызывает новый `TryJoinUnion(existing, memberId)` (только при совпадении типа),
   добавлен `UnionDisplayName(union)` — перевод `NameKey`; используется в логе и в
   `TryGetUnionSummary`. `GameNotifier.DefaultTemplate` и `Chronicle.Fmt` получили шаблоны
   для `UnionMemberJoined`.
3. **Проверка:** 188/188 тестов ядра (новый `UnionGrowthTests.cs` — 8 тестов, включая сквозной
   «партнёр по федерации приносит больше торгового дохода» и «династическая уния именуется
   ключом» через полный цикл престолонаследия; в `SaveDataTests` — проверка одобрения
   и чтение мира v6 с голыми идентификаторами); net48: 0 ошибок, 0 предупреждений;
   локали ru/en по 619 ключей, паритет 100%; все 277 ключей из кода есть в обоих словарях.
   - **Два дефекта найдены собственными тестами и исправлены:** (1) `NameKey`, выставленный
     после возврата из `TryCreateUnion`, не попадал в уже опубликованное событие;
     (2) `partner.Union` в `BuildEconomyInput` зависел от порядка обхода держав — в первый год
     после создания унии торговый бонус не применялся.
   - **Ловушка теста:** династическая уния не образуется, если сосед успел сменить строй
     вместе с новым правителем (`SuccessorIdeology`): обе державы нужно вводить в мир
     с первого года и держать соседскую идеологию монархической.
   - **Ловушка экономики:** торговый доход жёстко ограничен налогами × 1.5, поэтому на одной
     клетке городов разница типов унии не видна вовсе — тесту нужны 3–4 города.
4. **Не проверено в игре** (WorldBox 0.51.2 недоступен): вступление третьей державы в союз
   и рост счётчика в строке унии, отказ при полном союзе, английский перевод названия
   династической унии, сохранение одобрения участников между сессиями.
5. **Осознанно отложено на этап 20 «Глубокая дипломатия»:** дань, военные обязательства
   вассала, наследование короны в персональной унии, институты федерации, интеграция
   и войны за независимость. Тип `FullUnion` по-прежнему недостижим в игре — появится
   вместе с интеграцией. Унии не моделируются в `simulation/*.py`: там нет дипломатического
   слоя, он появится на этапе 20.

## v0.14.0 — Экономика, торговые пути и кризисы (Этап 18)

1. **Ядро (`src/RealPolitic.Core/`):** новые файлы `EconomyState.cs` (перечисление
   `EconomicCrisisType` — None/Recession/Depression/Bankruptcy/TradeCollapse; справочник
   `EconomicCrises`: `LocKey`, `Severity` (1.0/1.8/2.2/1.3), `CanSparkRebellion`; класс
   `EconomyState`: благосостояние, ставка, тариф, последний доход/расход, `TradeShare`,
   дефицитные годы, кризис и его срок, счётчики кризисов и бумов, режимы экономии и
   эмбарго, `InCrisis`, `EmbargoActive(year)`, `AusterityActive(year)`, `OnRegimeChange`),
   `TradeRoute.cs` (`TradeRoute`: `Involves`, `OtherSide`, `YearsActive`; `TradeNetwork`:
   `Find`, `Of`, `OpenRoutesOf`, `Open`, `Restore`, `BlockBetween`, `UnblockFor`,
   `DropKingdom`, `RemapKingdom`, `Clear`), `Economy.cs` (`Economy.Tick`, `EnsureState`,
   `Taxes`, `TradeIncome`, `Expenses`, `TaxBias`, `CrisisBias`, `TradeBias`, `Clamp`;
   вход `EconomyInput`/`EconomyPartner`, выход `EconomyReport`).
   Изменены: `PoliticalWorld` (`Trade` — сеть путей, `EconomyEffectApplier` — обратный вызов
   в игровой слой, `LastEconomyReport(id)`, `TickEconomy`, `BuildEconomyInput`),
   `KingdomPolitics.Economy` (nullable — у старых сохранений его нет), `KingdomStrategy`
   (действия `DevelopTrade`, `Austerity`, `TradeEmbargo`; в снимке состояния `Prosperity`,
   `TradeIncome`, `TradeRoutes`, `MaxTradeRoutes`, `DeficitYears`, `AusterityActive`,
   `EmbargoCandidate`, `EmbargoActive`, `EconomyEnabled`, `InEconomicCrisis`),
   `CivilWars` (кризис поднимает шанс мятежа в `EconomicCrisisRebellionFactor` раз и даёт
   причину `EconomicCrisis`), `PolEventType` (97–103), `SimParams` (~45 экономических
   параметров, секция «Экономика и торговля»), `SaveData` (формат v6).
2. **Игровой слой (`src/Game/`):** новый `EconomyExecutor.cs` (чтение/запись золота,
   продовольствия и счастья городов, свои копии хелперов настроения и лояльности —
   в `GameRefs` их нет; ресурс еды — `"bread"`), подключение в `WorldFeeder`
   (`EconomyEffectApplier`, исполнение стратегических действий через
   `TryExecuteStrategicState`, снимок экономики для планировщика, блоки путей из `Relations`
   и эмбарго, `_liveKingdoms` как единственный способ взять живой `Kingdom` по id),
   `Patches.ShowMechanicsRows` (строка экономики), `GameNotifier` (7 шаблонов),
   `Chronicle` (рубрики «ТОРГОВЛЯ»/«ЭКОНОМИКА», 7 шаблонов, счётчики),
   `ConfigReader`/`RealPoliticMod` (7 настроек).
   **Важно:** `IdeologyMechanicsEffects.TryApplyStrategicAction` не имеет доступа к
   `MapBox`/`PoliticalWorld`/`SimParams`, поэтому экономические действия маршрутизируются
   именно через `WorldFeeder`.
3. **Баланс (проверено прогоном 1000 миров × 300 лет × 20 держав,
   `simulation/summary_economy_peace_1000.json`):** 105 путей открыто на мир, 76 живых
   к концу, 99 повышений уровня; 179 кризисов на мир (6.5% государе-лет: спад 176,
   банкротство 3.1, депрессия 0.14) и 172 бума; благосостояние 74.8, легитимность 49.9,
   поддержка 60.6; 97 мятежей на мир, выживаемость стартовых держав 89.5%.
   Стресс-сценарий воюющего мира (`summary_economy_war_1000.json`, `--war-rate 0.004`):
   231 кризис (8.9% лет), депрессии 0.8 и крах торговли 1.6 на мир, благосостояние 60.1,
   живых путей 28.7, выживаемость 27%.
   - **Вывод 1 — торговый доход нельзя масштабировать уровнем дважды.** Прежние
     `TradeVolumeCap × уровень` и прибыль `× уровень` давали ×9 на третьем уровне: один путь
     приносил больше всей казны. Потолок объёма теперь плоский (22), плюс жёсткий
     `TradeIncomeCapOfTaxes = 1.5`.
   - **Вывод 2 — благосостояние должно стремиться к цели, а не расти.** Плоский прирост
     (`ProsperityRecovery`) насыщал любую стабильную державу на 100%, и бум становился
     рутиной. Теперь это сходимость к цели (`ProsperityBaseline 65`, шаг 12%), цель двигают
     модификаторы; поле `ProsperityRecovery` удалено.
   - **Вывод 3 — бум обязан быть редким.** Условия: благосостояние ≥ 78, ≥ 3 открытых пути,
     раз в 25 лет. При типичных 2–3 путях на державу цель благосостояния ~71 и бум недоступен;
     он достаётся крупным торговым узлам.
   - **Вывод 4 — поддержка в модели выводится, а не копится.** В ядре `Support` считает
     перепись, поэтому в симуляторе поддержка тянется к цели от благосостояния, недовольства
     и легитимности. Накопление сдвигов за 300 лет упиралось в 100.
   - **Вывод 5 — у модели нет стока золота**, поэтому `mean_avg_gold` за 300 лет растёт.
     В игре золото тратит сам WorldBox; фейковый сток добавлять не нужно.
4. **Проверка:**
   - 178/178 тестов ядра (`dotnet test tests/RealPolitic.Tests/RealPolitic.Tests.csproj`),
     из них 40 в `EconomyTests.cs`;
   - релизная сборка net48: 0 ошибок, 0 предупреждений (`RealPolitic.dll`, 355 328 байт);
   - локализации ru/en: 615 ключей, паритет 100%, плейсхолдеры совпадают;
   - `default_config.json`: 36 настроек (7 новых).
   - **Не проверено:** поведение в самой игре (WorldBox 0.51.2 в sandbox недоступен).
     Рискованные вызовы `EconomyExecutor` (золото и ресурсы королевства, `getMood`/`setMood`,
     `getLoyalty`/`setLoyalty`, `takeResource`, счётчик городов) компилируются против
     заглушек и деградируют до однократного `LogService.LogError`.
     Что смотреть в игре: строка экономики в окне государства, открытие путей между соседями,
     разрыв путей при войне, эмбарго, кризисы и бум в уведомлениях, рубрики
     «ТОРГОВЛЯ»/«ЭКОНОМИКА» в `chronicle.txt`, загрузка мира v5 (экономика появляется
     со значениями по умолчанию, путей нет, мир не ломается).
5. **Симулятор `simulation/simulate_economy.py`:** наследует модель гражданской войны этапа 17
   и добавляет экономику, случайные внешние войны (`--war-rate`, по умолчанию 0 — тогда
   прогон сравним с `summary_civilwar_1000.json`) и ручки калибровки (`--tax-base`,
   `--army-upkeep`, `--admin-cost`, `--crisis-cooldown`, `--bankruptcy-deficit-years`,
   `--bankruptcy-legitimacy-loss`, `--crisis-rebellion-factor`, `--prosperity-baseline`,
   `--quiet`). Итоги пишутся в `runs_economy_1000.csv` и `summary_economy_1000.json`.

## v0.13.0 — Гражданские войны и расколы держав (Этап 17)

1. **Ядро (`src/RealPolitic.Core/`):** новые файлы `CivilWarCause.cs` (8 причин + справочник
   `CivilWarCauses`: локализация, склонность к отделению, военный опыт, цель мятежа),
   `CivilWarState.cs` (состояние войны, `RebelShare`, `YearsActive`, `HoldsCity`,
   `AddRebelCity`/`RemoveRebelCity`), `CivilWars.cs` (`CanStart`, `StartChance`, `Start`,
   `Tick`, `DecideResolution`, `SecessionPossible`, `ApplyAutonomy`, `ResolveOutcome`),
   `PendingSecession.cs` (передача отделения из ядра в игровой слой).
   Изменены: `PoliticalWorld` (`BuildCivilWarContext`, `ApplyCivilWarOutcome`,
   `CreateBreakawayKingdom`, `AllocateKingdomId`, `RemapKingdomId`,
   `PendingSecessions`/`TakePendingSecessions`), `KingdomSimulation` (`TickState` возвращает
   `CivilWarOutcome?`, `TickCivilWar`), `KingdomStrategy` (`SuppressRebellion`,
   `CivilWarActive`/`RebelShare` в снимке), `CityInput.Loyalty`, `ElectionState.LastElectionYear`,
   `PolEventType` (90–96), `SimParams` (19 параметров гражданской войны), `SaveData` (формат v5).
2. **Игровой слой (`src/Game/`):** новый `CivilWarExecutor.cs`
   (`CivilWarsEnabled`/`SecessionsEnabled`, `ExecuteSecession`, `ApplyWarEffects`,
   `SuppressRebellion`, `ReadCityLeader`), `GameRefs.WriteMember`, подключение в
   `WorldFeeder` (лояльность городов, исполнение отделений, эффекты войны, сводка, снимок для
   планировщика, `RebelLeaderResolver`), `KingdomStrategyGameExecutor` (маршрут
   `SuppressRebellion`), `Patches.ShowMechanicsRows` (строка войны + индикатор угрозы),
   `GameNotifier` (12 шаблонов), `Chronicle` (рубрики, счётчики, сводки),
   `ConfigReader`/`RealPoliticMod` (6 настроек).
3. **Баланс (проверено прогоном 1000 миров × 300 лет × 20 держав,
   `simulation/summary_civilwar_1000.json`):** 123 мятежа на мир (~1 на государство в 95 лет);
   исходы — захват власти 30.3%, подавление 28.4%, мир 23.1%, отделение 14.4%, заморозка 0.2%;
   17.8 новых держав на мир; выживаемость стартовых государств 85.4%, средняя легитимность 42.9.
   - **Главный вывод калибровки:** разовый штраф легитимности за мятеж должен быть меньше
     скорости восстановления (`LegitimacyRecovery / 10` = 0.4/год). При 18 + 2.5/год
     возникала спираль «тирания → мятеж → тирания» (легитимность падала до ~1). Рабочие
     значения: штраф 10, падение 1.2/год, победа законной власти +18.
   - **Второй вывод:** без порога отделения (`CivilWarSecessionMinCities = 2` и минимум 1/3
     городов державы) мир дробился на сотни микро-государств (155 живых держав к 300-му году).
   - Базовый шанс 0.05 подобран свипом 0.04–0.14 (`--base-chance` у симулятора).
4. **Проверка:**
   - 138/138 тестов ядра (`dotnet test tests/RealPolitic.Tests/RealPolitic.Tests.csproj`),
     из них 30 в `CivilWarTests.cs`;
   - релизная сборка net48: 0 ошибок, 0 предупреждений (`RealPolitic.dll`, 321 536 байт);
   - локализации ru/en: 572 ключа, паритет 100%, плейсхолдеры совпадают;
   - `default_config.json`: 29 настроек (6 новых).
   - **Не проверено:** поведение в самой игре (WorldBox 0.51.2 в sandbox недоступен).
     Рискованные вызовы `CivilWarExecutor` (`getLoyalty`/`setLoyalty`,
     `countWarriors`/`getMaxWarriors`, `setProfession`, `getResourcesAmount`/`takeResource`,
     `city.leader`, `setKingdom`/`changeKingdom`/`joinKingdom`/`kingdom.addCity`)
     компилируются против заглушек и деградируют до однократного
     `LogService.LogError("[Real Politic] API гражданской войны недоступен: …")`.
     Что смотреть в игре: строка войны в окне государства, появление нового королевства после
     раскола (города, столица, вождь, строй), отсутствие фантомных держав при недоступном API,
     рубрики «ГРАЖДАНСКАЯ ВОЙНА»/«РАСКОЛ ГОСУДАРСТВА» в `chronicle.txt`,
     загрузка мира v4 с активной войной.

## v0.12.0 — Династии, Выборы и Эпохи Модерна (Этап 16)
1. **Три новые идеологии Эпохи Модерна:**
   - `Democracy` (Демократия): гражданские свободы, высокая лояльность городов, активная суперспособность «Всенародный референдум» (`RP_POWER_DEMOCRACY`), угроза популизма (`RP_THREAT_DEMOCRACY`);
   - `Communism` (Коммунизм): плановое хозяйство, ускоренное производство продовольствия и рекрутов, суперспособность «Пятилетка» (`RP_POWER_COMMUNISM`), угроза бюрократического застоя и номенклатурных чисток (`RP_THREAT_COMMUNISM`);
   - `Ethnonationalism` (Этнонационализм): верховенство титульной расы (+15 лояльности в моноэтнических городах), штрафы и бунты в инородных городах (-25 лояльности), способность «Чистка и ассимиляция» (`RP_POWER_ETHNONATIONALISM`), угроза расового восстания (`RP_THREAT_ETHNONATIONALISM`).
   - Новые прозрачные 64x64 пиксельные иконки `democracy.png`, `communism.png`, `ethnonationalism.png` встроены в ресурсы DLL и файловую структуру.
2. **15-летний цикл выборов и переизбрание (Демократия):**
   - Строгий 15-летний конституционный цикл выборов (`ElectionSystem`);
   - Максимум 3 срока за всю жизнь правителя (`MaxLifetimeTerms = 3`);
   - Переизбрание инкумбента на 2-й и 3-й срок при высокой народной поддержке;
   - Передача власти новому президенту или возвращение экс-президента из зала славы.
3. **Законы престолонаследия (Succession Laws):**
   - 4 династических закона для монархии: Салический закон (`Salic`), Когнатический закон (`Cognatic`), Династическая уния (`DynasticUnion`), Строгий агнатический закон (`StrictAgnatic`);
   - 8 специализированных законов для остальных строев;
   - Нарушение закона наследования влечет падение легитимности и рост диссента подданных.
4. **Институт регентства для малолетних правителей:**
   - Автоматическое назначение регентского совета при правителе младше 18 лет;
   - Блокировка наступательных войн (`UnderRegency = true`), снижение легитимности и рост угрозы кризиса знати;
   - Мирный роспуск регентства и празднование при достижении совершеннолетия (18 лет).
5. **Сохранение миров формата v4 (SaveData v4):**
   - Сериализация и десериализация законов престолонаследия (`law`), регентства (`regency`), выборов и реестра экс-президентов (`election`), титульной расы (`race`);
   - 100% обратная совместимость с версиями v1, v2 и v3.
6. **Тесты и сборка:**
   - 128/128 тестов пройдены успешно (100% зелёные);
   - Релизная сборка net48 DLL: 0 ошибок, 0 предупреждений;
   - 100% паритет локализаций (520 ключей в `ru.json` и `en.json`).

## v0.11.0 — Релизный комплект (Этап 15)
1. **Завершение Дорожной карты (Roadmap):** Все 15 этапов проекта реализованы в кодовой базе и покрыты тестами.
2. **Глубокий аудит кода и ресурсов (Audit & Polish):**
   - 100% паритет русской и английской локализаций (400 ключей 1:1, идентичные плейсхолдеры `{...}`);
   - Полная локализация всех параметров `default_config.json` с именами и подробными описаниями в NeoModLoader;
   - Корректная ежегодная симуляция недовольства через `LastDissentYear` в `ActorIdeologies.Faith`;
   - Унификация кросс-сборочной рефлексии: все прямые `Type.GetType(..., "Assembly-CSharp")` заменены на отказоустойчивый `Patches.FindGameType`;
   - Безопасное изменение счастья через `IdeologyEffects.ApplyHappiness`;
   - Защита словарей от `KeyNotFoundException` в `WorldFeeder.PlanStrategicActions`;
   - Интернационализация метки года `RP_YEAR_LABEL` в UI и хронике.
3. **Документация и релиз:** Обновлены `README.md`, `ROADMAP.md`, `CHANGELOG.md`, `HANDOFF.md`, `mod.json` и сформированы финальные дистрибутивные архивы `RealPolitic-0.11.0.zip`, `RealPolitic-0.11.0-source.zip`, `RealPolitic-0.11.0-docs.zip`.
4. **Верификация:** 92/92 теста ядра пройдены успешно (100% зелёные); релизная сборка net48 DLL: 0 ошибок, 0 предупреждений; целостность ZIP-архивов подтверждена `unzip -t`.

## v0.10.16-dev — этап 10: сохранение миров (WorldPersistence), восстановление уний и связей, полнота сериализации v3
1. **Игровой слой сохранения `WorldPersistence` (Этап 10).**
   - Добавлен статический класс `WorldPersistence.cs` в `src/Game/`, управляющий сохранением (`WorldPersistence.Save`) и загрузкой (`WorldPersistence.Load`) политического мира.
   - Слот-ориентированное сохранение: если игра передаёт путь слота мира, файл состояния `realpolitic_state.json` сохраняется прямо в директорию этого слота сохранения WorldBox. При отсутствии пути используется автосохранение (`<ModFolder>/saves/` или `Documents/RealPolitic/saves/`).
   - Безопасная атомарная запись через `.tmp`-файл с последующей заменой целевого файла, предотвращая порчу сейва при аварийном завершении игры.
2. **Harmony-хуки SaveManager.**
   - Автоматический перехват сохранения и загрузки (`save*` и `load*` методов `SaveManager`) через Harmony-постфиксы с анализом аргументов пути (`ExtractFolder`). Graceful degradation при отсутствии или изменении типа `SaveManager`.
   - Хуки подключены в общий метод инициализации `Patches.Install`.
3. **Восстановление уний и членства (Ядро).**
   - Исправлен баг в `SaveData.Apply`: теперь все унии регистрируются в `PoliticalWorld.RestoreUnion(union)`, а всем королевствам восстанавливается ссылка на унию `k.Union = world.UnionOf(k.KingdomId)`.
4. **Полнота сериализации (Формат v3).**
   - Правители: добавлены сериализация и восстановление пола (`IsFemale`) и возраста (`Age`), устраняя проблему превращения королев в королей и обнуления возраста правителей при загрузке.
   - Записи смертей: добавлены сохранение и загрузка пола (`WasFemale`), длительности правления (`ReignYears`) и сведений об убийце (`KillerIsCiv`, `KillerKingdom`, `KillerCity`, `KillerJob`, `KillerSpecies`).
   - Полная обратная совместимость с форматами сохранения v1 и v2.
5. **Тесты и сборка.**
   - 92/92 теста ядра пройдены успешно (добавлено 4 новых теста round-trip уний, королев/возраста, записей смертей и совместимости v2);
   - Релизный проект `RealPolitic.Release.csproj` net48 собран успешно: 0 ошибок, 43 предупреждения stub-проекта.

## v0.10.15-dev — преемственность при междуцарствии, UI уний и хроники, кэширование лояльности, числовая рефлексия
1. **Престолонаследие и междуцарствие (Этап 9).** Устранена потеря данных о предшественнике: при смерти монарха и восшествии нового правителя через интервал без короля (`king == null`), `PoliticalWorld.HandleSuccession` сохраняет `old_ruler`, `old_title` и `old_reign` благодаря исправленному условию `oldRuler.ActorId != num`. Добавлен регрессионный тест `Succession_PreservesPreviousRulerDetails_AfterInterregnum`.
2. **UI окна государства: строка союза и хроника (Этап 12).**
   - Добавлена подробная строка активного союза/унии (`RP_UNION_INFO_TITLE`): название, тип (`вассалитет`, `персональная уния`, `федерация`, `полная уния`), статус державы (`Лидер` / `Участник` с именем лидера), число участников и сплочённость (`u.Cohesion`).
   - Добавлена строка оперативной хроники (`RP_CHRONICLE_LATEST`): показывает последнее важное событие державы прямо в окне государства без необходимости открывать файл `chronicle.txt`.
   - В настройки добавлены независимые переключатели `ShowUnionUI` и `ShowKingdomChronicleUI`.
3. **Производительность лояльности городов (Этап 13).**
   - В `IdeologyEffects.CityLoyaltyBonus` внедрено секундное кэширование поправки лояльности (`_cityBonusTimestamp`). Это устраняет избыточные обходы сотен жителей и вызовы рефлексии (`isAlive`, `isSapient`, `isKingdomCiv`) каждый кадр при повторных вызовах ванильного `City.getLoyalty`. Добавлен `ClearCache()` при сбросе/загрузке мира.
4. **Совместимость рефлексии (Этап 14).**
   - `GameRefs.ParametersMatch` и `Invoke` дополнены поддержкой расширяющих числовых преобразований (`PrepareArgs`, `IsNumericType`) для безопасных вызовов методов игры с типами `long`/`float`/`double`/enum.
Ядро: 88/88 тестов зелёные; release-build net48: 0 ошибок, 43 предупреждения; GameCheck: 0 ошибок.

## v0.10.14-dev — подробная летопись: реальное время, дата, пролог, итоги года
`Chronicle.cs` переписан для «рассказуемой» хроники (файл можно кидать ИИ-агенту, чтобы он пересказал историю мира):
1. **Реальное время у каждой записи.** Префикс события/смерти — `Год N (чч:мм:сс). ` (`YearPrefix`/`NowTime`, `DateTime.Now.ToString("HH:mm:ss")`).
2. **Дата в шапке.** `WriteFileHeader()` (в конструкторе, через буфер): «Летопись мира (создано дд.мм.гггг чч:мм:сс)» + линия-разделитель. Разделитель «НОВЫЙ МИР» (`WriteNewWorldDivider`) — с датой и временем.
3. **Пролог.** `WritePrologue(year)` пишется на первом тике нового мира (флаг `_prologueWritten`; сбрасывается при откате года): список присутствующих в мире строев с описаниями (`Ideologies.DescriptionKey`) + карточка каждого государства.
4. **Общий метод описания государства.** `KingdomLine(id, kingdom, out population, out warriors)` — строка «• Название — строй (Идеология). Население… Правитель… Легитимность… курс… война/революция… города/столица… религия/язык/культура… взгляды подданных». Используется и в прологе, и в годовой сводке.
5. **Итоги года.** `RecapLine()` — разность накопленных счётчиков и «фотографии» прошлой сводки (поля `_y*`, `UpdateYearBaseline()` после каждой сводки, сброс в `ResetStats()`); строка «За этот год: …» добавляется в сводку перед накопленной «СТАТИСТИКОЙ».
Локали: +4 ключа ru/en — `RP_CHRON_HEADER`, `RP_CHRON_PROLOGUE`, `RP_CHRON_PROLOGUE_IDEOLOGIES`, `RP_CHRON_RECAP` (в ru/en после `RP_CHRON_NEW_WORLD`). Ядро: 86/86 тестов; release-build net48: 0 ошибок, 43 предупреждения проекта; ручная проверка в WorldBox 0.51.2 ещё нужна.

## v0.10.13-dev — безопасные списки мира
Добавлены `GameRefs.KingdomList` и `GameRefs.CityList`; годовой цикл `WorldFeeder`, уведомления и сводка `Chronicle` больше не обращаются к `map.kingdoms.list`/`map.cities.list` напрямую. Ядро: 86/86 тестов; release-build net48: 0 ошибок, 43 предупреждения проекта; ручная проверка в WorldBox 0.51.2 ещё нужна.

## v0.10.12-dev — этап 14: совместимость reflection-слоя
`GameRefs.ReadProperty`, `Invoke` и `HasMember` теперь ищут члены по всей иерархии типов, а `Invoke` проверяет совместимость параметров и перехватывает несовместимые перегрузки. Это уменьшает риск поломки отдельных функций при изменении наследования в WorldBox. Ядро: 86/86 тестов; release-build net48: 0 ошибок, предупреждения проекта; ручная проверка в WorldBox 0.51.2 ещё нужна.

## v0.10.11-dev — исправлен первый стратегический тик
Проверка cooldown больше не вычисляет разность `Year - int.MinValue` в типе `int`: sentinel `LastActionYear` явно трактуется как отсутствие предыдущего решения, а разность обычных годов считается в `long`. Добавлен регрессионный тест, предотвращающий блокировку первого стратегического решения государства. В текущем sandbox нет `dotnet`, поэтому тестовый прогон и release-build требуют окружения с .NET SDK.

## v0.10.10-dev — этап 11: корректное чтение нулевых ресурсов
В `GameRefs` добавлен `TryKingdomResource`, который различает доступный нулевой запас и недоступный игровой API. `WorldFeeder` теперь передаёт в стратегический снимок реальные нулевые значения золота, камня и еды; fallback используется только при отсутствии списка городов или ошибке API. `GameKingdomActionExecutor` и `WorldFeeder` отклоняют пустые/fallback-планы, snapshot от другого государства, исчезнувших участников и самодипломатию до вызова игрового API. Для этапа 13 список `DiplomatSnapshot` и lookup политических состояний строятся один раз на годовой тик и переиспользуются для всех держав, вместо создания отдельного списка кандидатов на каждую из них. Полный обход игровых списков государств и городов для UI-синхронизации ограничен интервалом 0,5 секунды; новый мир синхронизируется немедленно. Добавлены регрессионные тесты; ядро прошло 85/85 тестов, GameCheck и release-build — 0 ошибок.

## v0.10.8 — этап 12: настройки отображения UI
`WorldFeeder` теперь хранит результат последней попытки стратегического действия по каждому государству. В окне государства рядом с планом отображается успех, отказ игрового API или причина безопасного ожидания cooldown. Это позволяет проверять не только выбор ИИ, но и фактический результат безопасного Game-моста.

Добавлены настройки `ShowStrategyUI` и `ShowDiplomacyUI`, включённые по умолчанию. Они позволяют отдельно скрывать строки стратегического плана/результата и дипломатической сводки, не отключая сам ИИ или дипломатию.

Проверка Player.log: мод загрузился штатно, установлено 8 Harmony-патчей, передано 9/9 иконок, слой идеологий работает. Ошибок Real Politic не найдено. В `Chronicle.cs` исправлены пропущенные пробелы между полями ежегодной сводки (`воинов: 0.Правитель` и `годаЛегитимность`).

Релизная сборка net48 через stub-проекты выполнена на .NET SDK 8.0.425: 0 ошибок, 43 предупреждения. Перед установкой требуется ручной прогон в WorldBox 0.51.2; настоящие DLL игры в sandbox не подключены.

## v0.10.2 — этап 11: симуляция гибридного ИИ и WorldBox Plots
`KingdomStrategySnapshot` — единый read-only снимок государства; `KingdomStrategicPlanner` возвращает одно детерминированное действие с score/reason и соблюдает cooldown. Приоритеты учитывают голод, войну, легитимность, угрозу, армию, дипломатию и идеологию. `IKingdomActionExecutor` отделяет выбор от безопасного выполнения через игровой API. Добавлены 8 тестов сценариев; общий прогон: 83/83.

Снимок подключён к `WorldFeeder`/годовому циклу. Дипломатические планы выполняются через `GameKingdomActionExecutor` и `TryExecuteStrategicDiplomacy`; мобилизация, ульты, ресурсы и восстановление легитимности пока только планируются и безопасно откладываются до специализированных игровых вызовов. Следующий шаг — подключить эти исполнители, затем сделать UI этапа 12. Компьютерная симуляция 1000×300 лет сохранена в `simulation/`: все 20 государств выжили в 87,1% сценариев, среднее число выживших — 19,861. Сборка игрового DLL в текущем sandbox не выполнялась; необходима ручная проверка в WorldBox 0.51.2.

## v0.10.2 — этап 11: симуляция гибридного ИИ и WorldBox Plots
`KingdomStrategySnapshot` — единый read-only снимок государства; `KingdomStrategicPlanner` возвращает одно детерминированное действие с score/reason и соблюдает cooldown. Приоритеты учитывают голод, войну, легитимность, угрозу, армию, дипломатию и идеологию. `IKingdomActionExecutor` отделяет выбор от безопасного выполнения через игровой API. Добавлены 8 тестов сценариев; общий прогон: 83/83.

Снимок подключён к `WorldFeeder`/годовому циклу. Дипломатические планы выполняются через `GameKingdomActionExecutor` и `TryExecuteStrategicDiplomacy`; мобилизация, ульты, ресурсы и восстановление легитимности пока только планируются и безопасно откладываются до специализированных игровых вызовов. Следующий шаг — подключить эти исполнители, затем сделать UI этапа 12. Компьютерная симуляция 1000×300 лет сохранена в `simulation/`: все 20 государств выжили в 87,1% сценариев, среднее число выживших — 19,861. Сборка игрового DLL в текущем sandbox не выполнялась; необходима ручная проверка в WorldBox 0.51.2.

## v0.9.2 — этап 10: сохранение состояния мира
`SaveData.FormatVersion = 2`. Для каждого государства сохраняются `Support`, `AvgDissent`, `InWar`, `Starving` и полный `IdeologyMechanicsState`: `LastUsedYear`, `ActiveUntilYear`, `TargetKingdomId`, `ThreatLevel`, `ThreatBreaks`, `LastBreakYear`. Формат 1 остаётся читаемым: отсутствующий блок `mech` оставляет безопасные значения по умолчанию, включая детерминированный стартовый разброс угрозы.

Добавлены тесты `SaveDataTests`: round-trip всех новых полей и загрузка старого JSON без блока механик.

## Следующий этап: 11 — ИИ-управление государством
Перед UI нужно сделать отдельный слой стратегического ИИ. Он должен собирать единый
read-only снимок государства, выбирать приоритеты с учётом идеологии и кризиса,
возвращать одно объяснимое действие с score/reason, соблюдать cooldown и исполняться
через безопасные вызовы игрового API. Минимальные сценарии тестирования: голод, война,
угроза срыва, избыток армии, низкая легитимность, мирный рост и различие решений
между идеологиями. UI этап 12 только отображает этот план и причины, но не заменяет
логику принятия решений.

## v0.9.1 — hotfix периодичности эффектов
В `IdeologyMechanicsEffects.Tick` активные эффекты ульт больше не применяются каждые 2 секунды реального времени. Для каждого государства хранится последний игровой год применения, поэтому еда, золото, камень, лечение и мобилизация выдаются не чаще одного раза за игровой год. Мобилизация при этом сохраняет лимит до 2 жителей за такой тик.

Проверка в игре: активировать ульту племени/технократии/изоляционизма и убедиться, что запас ресурса растёт примерно один раз за игровой год, а не непрерывно; для милитаризма — максимум 2 новых воина на город за игровой год.

## v0.9.0 (собрано, требует теста в игре)
Ульты получили НАСТОЯЩИЕ игровые эффекты вместо косметики:
- **Милитаризм:** `City.makeWarrior(actor)` — горожане реально становятся воинами (до 1.5× от `getMaxWarriors`, по 2 за тик, только `is_profession_citizen` и возраст 16+). Срыв «военной усталости» — `Demobilize` (обратно в `UnitProfession.Unit` = 2, НЕ 0).
- **Племя:** еда в города + `restoreHealthPercent(0.05)`.
- **Теократия:** `restoreHealthPercent(0.08)` — рвение исцеляет.
- **Технократия:** золото + камень в столицу.
- **Изоляционизм:** накопление еды.
- `GiveGold` получил параметр `resource` (gold/food/stone).

## Ранее (v0.8.9)
Полный цикл ульты (`IdeologyPowerEnded` = 72, ключи `RP_POWER_*_END`, Minor, уведомление по умолчанию ВЫКЛ). Тексты применения — `*_USE`. Заливка слоя alpha 140.

Тесты: 73/73. Локали: 356 ключей, ru/en идентичны.

## Что дальше (идеи)
- Ульты не имеют визуального эффекта на карте.
- Не проверены в живой игре: мобилизация, войны через опрос (`GameWars.Poll`), разброс угроз.

---

# Real Politic — техническая передача проекта (handoff)

Документ для следующего разработчика/ИИ-агента: как собрать мод, как устроен код, какие
детали API игры уже выяснены, какие проблемы открыты и что делать дальше.

---

## 1. Состояние проекта одним абзацем

> **v0.6.1 — разбор Player.log от игровой проверки v0.6.0 («идеологий всё ещё нет»).**
> Лог дал три независимые причины, все исправлены:
> 1. **`showStatRow не найден на StatsMetaRowsContainer`** (главная) — строка идеологии
>    физически не добавлялась ни разу. `GetMethod("showStatRow", Instance|Public)` возвращал
>    null: метод объявлен в базовом `StatsRowsContainer`, а рефлексия по конкретному типу
>    его не отдавала. Решение: `FindShowStatRow()` — обход всей иерархии типов с
>    `DeclaredOnly` и выбором перегрузки с наибольшим числом параметров + диагностика
>    `DumpMethods()` со списком реальных методов, если снова не найдётся.
> 2. **`иконки идеологий: передано 0/9`** — причина в `SpriteTextureLoader.getSprite()`:
>    при промахе он делает `Resources.Load` и КЭШИРУЕТ null (`_cached_sprites[path] = null`).
>    После этого `addSprite()` вызывает `Dictionary.Add` на занятый ключ → `ArgumentException`,
>    иконка молча теряется. Решение: `RemoveNullCacheEntry()` убирает null-заглушку из
>    `_cached_sprites` перед вставкой, плюс повтор при неудаче. Ресурсы в DLL на месте
>    (проверено: 9 PNG, имена `RealPolitic.icons.*.png`) — дело было только в кэше.
> 3. **`патчи войн: IL Compile Error`** — Harmony не смог скомпилировать патч с
>    типизированными параметрами (`Kingdom`/`WarTypeAsset`/`War`/`WarWinner`). Решение:
>    все параметры патчей приняты как `object` с приведением внутри (+ перегрузка
>    `GameWars.OnWarEnded(War, object)`). Ожидается «установлено патчей: 8».
>
> Также усилен запасной вариант иконки: перебор `iconKingdom/iconCulture/iconReligion/iconClan`
> с проверкой наличия — у `KeyValueField` нет фона, и при null-спрайте строка невидима.
> Тесты ядра 44/44 (в песочнице `dotnet test` падает на CodeCoverage.Shim — запускать
> автономным раннером, см. §7).

> **v0.6.0 (этап 8 — идеологии жителей).** Идеология стала чертой КАЖДОГО жителя:
> `ActorIdeologies` (игровой слой, реестр по id юнита) + `ActorIdeologyRoll` (ядро, чистая
> логика). Раздача 70/30: 70 % подданных разделяют идеологию короля, 30 % получают другую,
> взвешенно по близости (союзная вероятнее вражеской). Идеология реально влияет:
> постфикс на `City.getLoyalty` (±25 лояльности от состава жителей → вплоть до ванильных
> восстаний), тик настроения (несогласные теряют счастье), годовое накопление `Dissent`
> с учётом `DissentTolerance` государства. В окне юнита строка «Идеология» показывает
> ЛИЧНУЮ идеологию жителя и его послушание («Технократия (несогласный, 34%)») — в том же
> мета-блоке, что язык/культура/религия. Патчей: 8. Тестов ядра: 44/44. Локали: 236 ключей.

Мод WorldBox 0.51.2 для NeoModLoader «Real Politic» (v0.5.2). Реализованы: годовой цикл,
события/уведомления, именованные уведомления (этап 5, подтверждён), read-only + реальная
дипломатия (этапы 4/6), унии (этап 7), 9 идеологий с иконками, причины смерти, подробная
летопись в txt, титулы по полу/идеологии. **Мод собирается в один net48 DLL**
(`RealPolitic.dll`), который грузится NeoModLoader.

**v0.5.2 (текущая)** — по итогам игровой проверки v0.5.1 (подтверждено: «Королева» в окне
лидера работает, строка идеологии видна в ОКНЕ ГОСУДАРСТВА, но НЕ видна в окне юнита):
- надёжный показ строки идеологии в окне юнита: «гарантированный механизм»
  `Patches.EnsureIdeologyRows()` (раз в 0.5 с проверяет открытые окна и добирает/обновляет
  строку), поиск «лучшего» StatsRowsContainer в иерархии окна, починенный
  `GameRefs.ReadMember` (обход иерархии типов — `gameObject` и т. п.), подробная
  диагностика вызовов `showMetaRows` (до 5 записей на причину);
- **максимально подробная летопись**: каждое событие — со всеми деталями (титул по
  идеологии+полу, возраст, государство+тип, причина смерти в форме пола, убийца, город,
  лет правления, предыдущий/новый правитель при смене престола, **войны: начало/конец
  с победителем и длительностью** (2 новых Harmony-патча на `WarManager.newWar`/`endWar`
  — единая точка создания/завершения всех войн, всего 7 патчей)), сводка по всем
  государствам (настройка `ChronicleSummaryYears`, по умолчанию каждый год), накопленная
  статистика (смерти по причинам, смены престола, революции, новшества, войны),
  разделитель «НОВЫЙ МИР» + итоговая статистика. Игровые уведомления о войнах — только
  при включённой настройке `NotifyWars` (по умолчанию выключены: ваниль и так пишет войны
  в лог мира, а летопись получает их всегда).
- ядро: `RulerRef.Age`, `DeathRecord.ReignYears` (+параметр в `DeathRegistry.Record`),
  событие преемственности несёт `old_ruler`/`old_title`/`old_reign`/`age`.

Тесты ядра: **31/31 зелёные**. Сборка 0 ошибок. Проверка в игре 0.5.2 — обязательна
(чеклист в §8). Локали: 228 ключей, ru/en совпадают.

---


### v0.7.0 — иконка идеологии в ряду мета-иконок и слой карты по идеологиям

1. **Ряд мета-иконок.** Строка идеологии раньше попадала в общий список статистики.
   Теперь `FindBestStatsContainer` сначала ищет компонент **`StatsMetaRowsContainer`**
   (`FindMetaRowsContainer` + `IsMetaRowsContainer`) — это и есть тот ряд, куда игра
   кладёт культуру/язык/религию/субспециес (`KingdomWindow.showMetaRows`).

2. **Слой карты по идеологиям** (`src/Game/IdeologyMapLayer.cs`).
   Свободного значения `MetaType` под идеологию нет, поэтому применена подмена:
   * при включении слоя включается штатная опция `map_kingdom_layer` (зоны начинают рисоваться);
   * Harmony-**префикс** на `ZoneCalculator.drawZoneMeta(зона, ассет, делегат)` подставляет
     вместо королевства зоны **королевство-представитель идеологии** (крупнейшее государство
     этой идеологии, пересчёт раз в 2 с) и сам вызывает 8-параметровую перегрузку `drawZoneMeta`.
   Так как границы рисуются только между разными мета-объектами, государства одной идеологии
   сливаются в общую зону и красятся цветом представителя.
   Все параметры патча — `object` (типизированные ломают Harmony в игре).

3. **Кнопка слоя** (`src/Game/IdeologyLayerButton.cs`) — клон штатной кнопки `culture_layer`
   в той же панели: у клона удаляются `PowerButton`/`TipButton`, клик вешается напрямую
   на `UnityEngine.UI.Button.onClick` → `IdeologyMapLayer.Toggle()`. Состояние показывается
   яркостью иконки. Пересоздаётся, если панель пересобрана (проверка раз в 1 с, 5 попыток).

Локализация: `RP_LAYER_IDEOLOGY`, `RP_LAYER_IDEOLOGY_DESC` (ru/en, по 238 ключей).


### v0.7.1 — разбор отчёта пользователя по 0.7.0

Присланный `Player.log` был от СТАРОЙ сборки (патчей 6, слоя идеологий нет, иконки 0/9).
Но он вскрыл две реальные регрессии, исправленные здесь:

1. **`showStatRow не найден на StatsMetaRowsContainer`.** `FindShowStatRow` обходил только
   `DeclaredOnly` по `BaseType` и в игре не находил метод. Теперь поиск: сначала
   `FlattenHierarchy`, затем обход `BaseType`, и по двум именам —
   `showStatRow` и `showStatRowMeta` (второй объявлен прямо в `StatsMetaRowsContainer`
   и имеет ту же сигнатуру из 10 параметров). Берётся перегрузка с >= 6 параметрами.

2. **Структура zip.** В 0.7.0 файлы лежали в КОРНЕ архива, а NeoModLoader ждёт папку
   `RealPolitic/` (как в 0.5.x/0.6.x) — иначе мод и его `icons/` не находятся.
   Пакет снова собирается как `RealPolitic/{RealPolitic.dll, mod.json, icon.png,
   default_config.json, Locales/, icons/}` — 18 файлов.

3. **Фоны иконок удалены.** У всех 9 PNG (`icons/` и `icons/64/`) тёмная подложка
   заменена на прозрачность: цвет фона определяется как самый частый в рамке 2px,
   затем убираются ВСЕ близкие к нему пиксели (не flood-fill — иначе оставались
   замкнутые области, например между лучами у `theocracy`), с мягким краем, чтобы
   не было тёмной каймы.


### v0.7.2 — фиксированные цвета и подписи идеологий на карте

1. **Свой цвет на идеологию.** `Ideologies.LayerColor(type, out r,g,b)` — палитра из 9
   контрастных цветов. Префикс `ZoneCalculator.drawZoneMeta` больше НЕ подставляет
   королевство-представитель: он сам красит зону (`applyMetaColorsToZone` через рефлексию,
   параметры `Color32` идут по `ref`, поэтому вызов массивом аргументов). Заливка alpha=90,
   граница — тот же цвет +45 по каналам, alpha=255. Граница рисуется только там, где у
   соседней зоны ДРУГАЯ идеология; зона без государства очищается (`drawZoneClear`).
   Лишние перерисовки отсекает `TileZone.checkShouldReRender`.

2. **Подписи = название идеологии.** Префикс `NameplateText.showTextKingdom` +
   постфикс `NameplateText.setText`: префикс решает, показывать ли табличку
   (только у крупнейшего государства идеологии — иначе надписи дублируются) и кладёт
   нужный текст в `_labelOverride`, постфикс подменяет `_text_name.text` на «Монархия» и т.п.

Следующий шаг (обсуждается с пользователем): для каждой из 9 идеологий — по одной
МОДОВОЙ (нестоковой) механике-преимуществу и равноценному недостатку.

## 2. Как собрать DLL (важно)

Мод собирается **без установки игры** — через заглушки, имитирующие API WorldBox /
NeoModLoader / Harmony / Unity. Итоговый DLL собирается проектом:

```
RealPolitic/tools/release-build/RealPolitic.Release.csproj
```

Он включает `../../src/Game/*.cs` и `../../src/RealPolitic.Core/*.cs` и ссылается на 4
заглушки (`tools/build-stubs/{AssemblyCSharp,Harmony,NeoModLoader,UnityEngine}`) с теми же
именами сборок (`Assembly-CSharp`, `0Harmony`, `NeoModLoader`, `UnityEngine.CoreModule`),
чтобы в игре типы резолвились в настоящие сборки.

**Команды сборки** (нужен .NET SDK 8.0; net48 reference assemblies приходят из пакета
`Microsoft.NETFramework.ReferenceAssemblies`):

```bash
cd RealPolitic
source tools/ensure-dotnet.sh      # ставит/активирует SDK 8 в ~/.dotnet (после перезапуска sandbox обязателен)
export PATH=$HOME/.dotnet:$PATH DOTNET_CLI_TELEMETRY_OPTOUT=1 DOTNET_NOLOGO=1
dotnet build tools/release-build/RealPolitic.Release.csproj -c Release -v m
# результат: tools/release-build/bin/net48/RealPolitic.dll
```

- Синтаксическая проверка игрового слоя без игры: `dotnet build tools/compile-stubs/GameCheck.csproj`.
- Тесты ядра (xUnit, net8.0): `dotnet test tests/RealPolitic.Tests/RealPolitic.Tests.csproj -v q`
  — на SDK 8.0.425 работают нормально (138/138). Если падают с `MSB3030` на xunit testadapter —
  кэш `~/.nuget/packages` обрезан: `rm -rf ~/.nuget/packages`, удалить все `obj`/`bin`,
  `dotnet restore`.

**Упаковка архива мода** (структура папки внутри ZIP):

```
RealPolitic/
├── mod.json
├── default_config.json
├── icon.png
├── RealPolitic.dll
├── Locales/{ru,en}.json
└── icons/{aristocracy,egalitarianism,isolationism,merchantrepublic,militarism,monarchy,technocracy,theocracy,tribalism}.png
```

Иконки в ZIP — 64×64 (из `icons/64/`), полноразмерные лежат в `icons/`.

> Проверка DLL перед отправкой пользователю:
> `python3 -c "d=open('RealPolitic.dll','rb').read(); print(d.count('RP_UI_QUEEN'.encode('utf-16-le')))"`
> Строки-литералы в .NET лежат в UTF-16 (`strings -el`), имена методов — в UTF-8.

---

## 3. Архитектура

Два пространства имён компилируются в один DLL:

- **`RealPolitic.Core`** (`src/RealPolitic.Core/`) — чистое ядро симуляции, **не зависит от
  WorldBox**. Тестируемо. Ключевые файлы:
  - `PoliticalWorld.cs` — годовой цикл, `ProcessKingdom`, `HandleSuccession`.
  - `KingdomSimulation.cs` — `RecordRulerDeath` (с `title`, `wasFemale`), легитимность,
    модификатор лояльности от унии.
  - `Ideologies.cs` / `IdeologyDef.cs` / `IdeologyType.cs` — 9 идеологий; ключи локали
    `RP_IDEOLOGY_*`, `RP_IDEOLOGY_*_DESC`, `RP_STATEIN_*`, `RP_TITLE_*_M/_F`, `rp_ideology_*`.
  - `DeathCauseClassifier.cs` / `DeathCause.cs` / `DeathRecord.cs` / `DeathRegistry.cs` —
    причины смерти; `DeathRecord.WasFemale`, `Record(..., wasFemale)`.
  - `Unions.cs` / `Union.cs` / `UnionMember.cs` / `UnionType.cs` / `UnionRules.cs`.
  - `DiplomaticAdvisor.cs` / `DiplomacyWeights.cs` / `DiplomatSnapshot.cs`.
  - `PolEvent.cs` / `PolEventBus.cs` / `PolEventType.cs` / `NotificationCenter.cs`.
  - `SaveData.cs` (заготовка сохранения), `TinyJson.cs`.
  - `RulerRef.cs` (`IsFemale`), `KingdomInput.cs` (структура — не nullable в игровом слое).
- **`RealPolitic`** (`src/Game/`) — интеграция с WorldBox через безопасную прослойку:
  - `RealPoliticMod.cs` — точка входа; статические `ModFolder`, `Chronicle`,
    `ChronicleEnabled`, `_spritesRegistered`; регистрация спрайтов в `Update`.
  - `GameRefs.cs` — **главная прослойка reflection** (чтение свойств/вызов методов без
    исключений). `IsFemale` устанавливается на `RulerRef` здесь (строки ~462–465).
  - `WorldFeeder.cs` — `BuildSnapshot`, `TryGetInput`, `TryFormUnion`, `UnionName`,
    `TryGenerateAllianceName`, `TryExecute` (DemandVassal/ProposeAlliance).
  - `GameNotifier.cs` — очередь уведомлений, `Flush()` (интервал 3.5 с),
    `RenderEventText`, `ResolveParams`, `LocalizeKeyParams`, `DefaultTemplate`.
  - `GameDeaths.cs` — хук смерти, guard от дублей, `WasFemale`, `title` в `OnDeathRecorded`.
  - `GameText.cs` — `ActorName`, `ActorTitle`, `ActorId`, `ActorAge`, `ActorIsFemale`,
    `Capitalize`, `Localized`.
  - `Patches.cs` — Harmony-патчи (смерть, строка идеологии, титул правителя).
  - `IdeologySprites.cs` — загрузка иконок (встроенные → папка мода), отложенная на 1-й кадр.
  - `Chronicle.cs` — летопись в txt. `History.cs` — in-memory (300).
  - `GameMessages.cs`, `ConfigReader.cs`, `RuntimeDiagnostics.cs`, `DiplomacyExecutor.cs`
    (`ForceAlliance`).

---

## 4. Выясненные детали API игры (не переоткрывать)

Декомпиляция игры лежит в `game-api/` (полная, ilspycmd). Ключевые факты:

- **Смерть.** `Kingdom.setKing/removeKing/timer_new_king` (5–20 с) — `Kingdom.cs` 570–640.
  Путь смерти рядом с `Actor.cs` 7040: `removeKing()` → `logKingDead()`. Патч смерти
  цепляется на первый доступный из `die`/`killHimself`/`getKilled`/`applyDeath`/`dead`.
- **Пол.** `Actor.data.sex` — enum `ActorSex` (Male/Female/None); метод `bool isSexFemale()`.
- **Причина смерти.** `Meteorite.spawnMeteoriteDisaster` → `MapAction.explodeTile` →
  `getHit(AttackType.Explosion)`. Классификатор маппит `explosion/explode/bomb` → `Explosion`,
  `gravity/fell` → `Fell`.
- **Строки статистики.** `StatsMetaRowsContainer.showStatRowMeta(string pId, object pValue,
  string pColor, MetaType pMetaType, long pMetaId, bool pColorText=false, string pIconPath=null,
  string pTooltipId=null, TooltipDataGetter pTooltipData=null, bool pLocalize=true)` + перегрузки
  для City/Kingdom/Unit/… `UnitWindow.showMetaRows()` вызывается из
  `StatsMetaRowsContainer.showStats()`.
- **Окно лидера.** `KingdomKingElement`: `_title_element` (GameObject), `_king_element`
  (PrefabUnitElement); `showContent()` активирует оба и вызывает `_king_element.show(kingdom.king)`.
  **Источник текста титула пока не найден** (вероятно, дочерний `LocalizedText` с фикс. ключом).
- **Локализация.** `LocalizedText.key` (default `"??????"`), `updateText()` вызывает
  `LocalizedTextManager.getText(key, this.text)`; `autoField` регистрирует в менеджере.
  `KeyValueField`: `name_text`, `value` (Text) + `_name_text`, `_value` (LocalizedText).
- **Harmony.** В игре реальный 0Harmony 2.9.0.0 (как в NML). Сигнатуры патчей сверялись с ним.
- **События типа.** `PolEventType.RulerDeath = 20`, `Succession = 21`.
- **Ключи конфига** (`default_config.json`): `NotifyLevel`, `CollapseNotifications`,
  `NotifyActorConverted`, `NotifyNovelties`, `EnableSmartDiplomacy`, `EnableUnions`,
  `EnableChronicle`, `RevolutionChance`, `RevolutionYears`, `IdeologySpread`,
  `ForeignInfluence`, `DissentRate`.

---

## 5. Статус багов v0.5.0 (исправлены в v0.5.1, проверка в игре обязательна)

Скриншоты 317–323 и `Player.log` — в `testing/`.

**Важно:** `Player.log` — от билда старее текущих исходников (в нём 3 патча, а в v0.5.1
устанавливается 5: смерть, UnitWindow, KingdomWindow, окно лидера, нижний бар).

### Баг 1 — строка идеологии не отображается — ИСПРАВЛЕНО
Найдена вероятная причина: у `KeyValueField` нет заметного фона (фон alpha 0), видимая
часть строки — иконка; если спрайт иконки null в кэше `SpriteTextureLoader`, строка
буквально невидима (текст — только по ховеру). В v0.5.0 путь регистрации иконки и путь
поиска в `showStatRow` могли не совпасть. В v0.5.1:
- `Patches.EnsureIcon(ideology)` перед `showStatRow` проверяет `SpriteTextureLoader.getSprite
  ("ui/Icons/rp_ideology_*")` и при отсутствии вызывает `IdeologySprites.EnsureRegistered`
  (регистрация по требованию из встроенного ресурса / папки мода); при неудаче —
  запасная иконка игры `iconKingdom`, строка гарантированно видима.
- `ShowIdeologyRow` ловит исключения отражения, явно активирует строку
  (`ActivateRow`) и пишет диагностику `LogRowDiagnostics`: `active=…, name=…, value=…,
  иконка=… (ключ)` — смотреть в логе NML (строки `[Real Politic]`).
- `IdeologySprites.Register` переписан на общий `EnsureRegistered(Assembly, IdeologyType)`
  (идемпотентность: `addSprite` делает `Dictionary.Add` — двойная вставка исключена).

### Баг 2 — женщина-правитель подписана «Король» — ИСПРАВЛЕНО
Теперь закрыты **оба** места, где виден титул:
- **Окно лидера** (`KingdomKingElement`): Harmony-prefix на `showContent` (корутина —
  префикс успевает до тела). `ApplyKingTitle`: 1) подмена ключа `LocalizedText`
  (`setKeyAndUpdate`, запасной — поле `key` + `updateText`), 2) сразу прямой текст в
  `Text` — корректно даже если перерисовка по ключу не случится. Ключ для монархии —
  `RP_UI_QUEEN`/`RP_UI_KING`, для остальных идеологий — `RP_TITLE_*_M/_F` («Вождь»,
  «Верховная жрица», …). Поиск типа — `FindGameType` (Assembly-CSharp + обход всех
  сборок), все ветки отказа логируются (раньше `method == null` молчал).
- **Нижний бар** (`SelectedMetaUnitElement.show(Actor, string)`): текст строится как
  `getText("titled_king").Replace("$unit$", имя)` = «Король {имя}» (шаблон
  `titled_king` — у таба королевства `SelectedKingdom`). Новый Harmony-postfix
  `SelectedKingTitlePostfix` подменяет `_title.text` на титул по идеологии и полу
  («Королева Анаэла», «Вождь …»). Работает только для правителя (`GameDeaths.IsRuler`)
  и только для ключа `titled_king` — прочие шаблоны (`titled_leader`, `titled_chief`,
  `titled_captain`) не трогаются.

### Баг 3 — строчная «королева» в начале уведомления о смерти — ИСПРАВЛЕНО
`GameText.Capitalize` на параметр `title` (уже было в v0.5.0) + в v0.5.1 добавлены
**женские формы причин смерти** `RP_DEATH_*_F` («казнена», «пала в бою», «погибла во
взрыве»…) — `DeathRecord.LocKeyFor(female)`, выбирается в `GameNotifier.ResolveParams`
и `KingdomSimulation.RecordRulerDeath`/`GameDeaths.OnDeathRecorded`.

### Дополнительно в v0.5.1 (по обязательным требованиям пользователя)
- **Без дубля смерть/наследие**: `KingdomSimulation.RecordRulerDeath` возвращает
  существующую запись из `DeathRegistry`, не публикуя второе событие (смерть уже
  записана игровым хуком). Смерть — одно критическое сообщение с причиной; наследие —
  отдельное событие `Succession`, когда появится новый правитель.
- **Без задержки**: критические события (`Severity.Critical`) выходят из очереди
  `GameNotifier` без ожидания 3.5-секундного интервала (`NotificationCenter.PeekIsCritical`).
- **Грамматика наследия**: шаблон `RP_EV_SUCCESSION` — «В {state} {kingdom} — {new}
  {title} {ruler}», параметр `{new}` = `RP_ART_M`/`RP_ART_F` («новый»/«новая», en —
  «a new»).
- **«В королевстве» по идеологии**: новые религии/языки/культуры — «В {state} {kingdom}
  …» (`RP_STATEIN_*`), `TrackNovelty` кладёт параметр `state` по идеологии государства
  (племя — «В племени …»).
- Тесты ядра: `tests/RealPolitic.Tests/SuccessionAndDeathTests.cs` (5 тестов: пол
  артикля/титула в Succession, `state` в новелтиях, `LocKeyFor`, дедупликация,
  женская причина). Итого 26 тестов, все зелёные.

---

## 6. Требования пользователя (обязательные)

1. **Причина смерти** в уведомлении (метеорит/болезнь/битва), без задержки и **без дубля**
   с престолонаследием. Источник дубля: `PoliticalWorld.HandleSuccession` публикует
   `Succession` при смене `_lastRuler` независимо от игрового события смерти.
2. **Летопись в txt** (`Chronicle.cs`, `EnableChronicle`) — проверить, что файл реально пишется.
3. **Пол правителя** — «королева»/«король» везде (включая окно лидера).
4. **Титулы по идеологии** — для Tribalism «племя»/«вождь», для прочих свои формы; применять
   последовательно во всех текстах.

---

## 7. Известные грабли (не наступать повторно)

- `dotnet` в sandbox не предустановлен; ставится `dotnet-install.sh --channel 8.0` в `~/.dotnet`.
- `ilspycmd` в sandbox не установлен — пользоваться готовой декомпиляцией `game-api/`.
- При `MSB3644` (net48 reference assemblies) — удалить кэш
  `~/.nuget/packages/microsoft.netframework.referenceassemblies*`, `dotnet restore --force`.
- `KingdomInput` — структура, **не** nullable в игровом слое; паттерн `TryGetInput(int, out KingdomInput)`.
- `NameGenerator.getName` — много опциональных параметров; reflection-вызов требует значений
  по умолчанию в правильном порядке (`WorldFeeder.TryGenerateAllianceName`).
- Вызов `showStatRow` / `SetActive` через reflection: у `UnityEngine.Object` есть `==`/`!=`
  операторы, но «null» после `Destroy` — это fake-null; проверять и `== null`, и `activeSelf`.
- NML `ModDeclare`/`GetDeclaration` требуют соответствующих заглушек в
  `tools/build-stubs/NeoModLoader/Stubs.cs`, иначе CS0103.
- Строки-литералы в DLL — UTF-16 (`strings -el`), имена методов — UTF-8.
- OCR скриншотов 321–323 слишком шумный — не полагаться на него для точных строк.
- `dotnet test` в sandbox **работает** на SDK 8.0.425 (138/138) — прежняя заметка про
  «молчаливый обрыв» устарела. Если тесты не собираются с `MSB3030` (не копируется
  `xunit.runner.visualstudio.testadapter.dll`) — кэш `~/.nuget/packages` обрезан:
  `rm -rf ~/.nuget/packages` + удалить все `obj`/`bin` + `dotnet restore`.
  Правка csproj без очистки кэша не помогает.
- `SpriteTextureLoader.getSprite()` кэширует null при промахе — всегда чистить
  `_cached_sprites` перед `addSprite()`, иначе `Dictionary.Add` бросит исключение.
- Harmony в игре не компилирует патчи с типизированными игровыми параметрами
  (`IL Compile Error`) — принимать параметры как `object` и приводить внутри.

---


- **Панель слоёв**: своя `GodPower`/`MetaTypeAsset` для идеологии НЕ регистрируется —
  свободного `MetaType` нет, а `MetaTypeAsset` требует типизированных делегатов
  (`MetaZoneGetMetaSimple` и др.), которых нет в заглушках. Рабочий путь — подмена
  мета-объекта в префиксе `ZoneCalculator.drawZoneMeta`.
- `dotnet-install.sh` с `https://dot.net/v1/...` падает на TLS; рабочий адрес —
  `https://builds.dotnet.microsoft.com/dotnet/scripts/v1/dotnet-install.sh`.
- `MSB3644` (net48 reference assemblies) после переустановки SDK лечится не удалением
  пакета из `~/.nuget`, а `rm -rf obj bin` во ВСЕХ проектах (release-build + build-stubs)
  и повторным `dotnet restore`.
- **В рабочей копии нет `.git`** — она разворачивается из source-zip (`RealPolitic-*-source.zip`),
  поэтому этап фиксируется архивами в `release/` и обновлёнными документами, а не коммитом.
  Не обещать «закоммитил» и не ссылаться на хеши прошлых коммитов.
- `~/.dotnet` не переживает перезапуск sandbox (`~/.nuget` — переживает, но может быть обрезан).
  Всегда начинать с `source tools/ensure-dotnet.sh`.
- В net48 нет `Dictionary.Remove(key, out value)` — только `TryGetValue` + `Remove`;
  у `TinyJson.Node` нет `AsInt` — приводить `(int)node.Arr[i].Num`.
- `PolEventBus` очищается методом `Clear()` (`ClearEvents` — это метод `PoliticalWorld`).
- **Баланс гражданских войн:** разовый штраф легитимности за мятеж обязан быть меньше скорости
  её восстановления (`LegitimacyRecovery / 10`). Иначе мятежи сами порождают новые
  (спираль «тирания → мятеж → тирания», легитимность падает к нулю). Проверять прогоном
  `python3 simulation/simulate_civil_war.py --runs 300 --years 300 --states 20`.
- **Отделение держав:** без порога `CivilWarSecessionMinCities` и доли городов мир дробится
  на сотни микро-государств. Проверять `initial_survival_rate` и `mean_states_alive`
  в `simulation/summary_civilwar_*.json`.
- Тестовые ловушки гражданской войны: `KingdomSimulation.TickState` каждый тик присваивает
  `state.Ruler = input.Ruler` (правителя передавать через `KingdomInput.Ruler`);
  пересоздание `state.CivilWar` в хелпере теста теряет флаг `Frozen` — переиспользовать объект;
  при `Legitimacy = 15, AvgDissent = 0.65` вес `Tyranny` (3.45) выше `RegencyCrisis` (3.0) —
  не утверждать, что причиной станет кризис регентства.

## 8. Ближайшие шаги

1. **Проверить в WorldBox 0.51.2** (sandbox игры не имеет). **v0.14.1 — унии:**
   - третья держава вступает в существующую федерацию, счётчик участников в строке унии растёт;
   - при 5 участниках вступление отклоняется, в логе видна причина;
   - название династической унии переведено (в английской игре — «Dynastic Union»);
   - уведомление и строка летописи о вступлении в союз появляются;
   - после сохранения/загрузки одобрение участников не сбрасывается к 0.6.
   **v0.14.0 — экономика:**
   - строка экономики в окне государства: благосостояние, доход/расход, число путей,
     активные режимы экономии и эмбарго;
   - торговые пути: открытие между соседями в мире, рост объёма и уровня, разрыв при войне,
     торговое эмбарго (8 лет);
   - кризисы всех четырёх видов и бум в уведомлениях; рубрики «ТОРГОВЛЯ» и «ЭКОНОМИКА»
     в `chronicle.txt` со счётчиками путей, кризисов и бумов;
   - при недоступном игровом API — однократная ошибка в логе, экономика считается,
     мир не ломается;
   - сохранение/загрузка: мир v5 без блока экономики грузится (значения по умолчанию,
     путей нет), мир v6 восстанавливает благосостояние, кризис, режимы и все пути;
   - отключение `EnableEconomy` — налоги, благосостояние и кризисы не считаются вовсе.
   **v0.13.0 — гражданские войны:**
   - строка гражданской войны в окне государства (причина, перевес, длительность, города);
   - раскол: после исхода «отделение» в мире появляется новое королевство — города перенесены,
     столица и вождь назначены, строй = идеология мятежа, в `chronicle.txt` рубрика
     «РАСКОЛ ГОСУДАРСТВА»;
   - при недоступном игровом API фантомных держав нет (ядро снимает их на следующем тике),
     в логе однократное `[Real Politic] API гражданской войны недоступен: …`;
   - уведомления о начале/сражениях/вмешательстве/окончании войны — без дублирования
     сообщения о наследовании при казни правителя;
   - сохранение/загрузка: мир v4 без блока `cw` грузится, мир v5 с активной войной
     восстанавливает силы сторон, счёт и мятежные города;
   - отключение `EnableCivilWars` — мятежи не начинаются вовсе.
2. **Этап 19 — Династии и личности правителей:** характеры, амбиции, отношения между
   правителями, династические браки. Заготовки: `SuccessionLaw.DynasticUnion`,
   `RoyalClanName`/`RoyalClanId`/`DynastyReignsCount`, `UnionType.PersonalUnion`.
   База под браки готова с 0.14.1: уния умеет держать больше двух участников, её имя
   локализуется, а одобрение участника переживает загрузку.
3. **Этап 20 — Глубокая дипломатия:** казус белли, договоры, шпионаж, коалиции — и полная
   глубина подчинения, отложенная с 0.14.1: дань вассала, его автоматическое вступление
   в войну сюзерена, наследование короны в персональной унии (или распад с шансом),
   институты федерации, интеграция/аннексия участника, война за независимость как казус белли,
   достижимый `FullUnion`. Здесь же унии стоит добавить в `simulation/*.py` — сейчас
   дипломатического слоя в симуляторе нет, поэтому баланс союзов проверен только тестами.

### Детали фикса окна юнита (v0.5.2, для отладки)
Цепочка `UnitWindow.showMetaRows` (постфикс) → actor (`SelectedUnit.unit`) → kingdom →
`World.GetKingdom` → контейнер. В v0.5.1 строка в окно государства добавлялась, а в окно
юнита — нет, причина не установлена (Player.log с той сессии не сохранён). Поэтому в 0.5.2:
каждый ранний возврат постфикса логируется (до 5 раз на причину), `EnsureIdeologyRows()`
из `Update()` (раз в 0.5 с) независимо добирает строку в любой открытый активный
UnitWindow/KingdomWindow (поиск `[KV] RP_IDEOLOGY_TITLE` в `stats_rows` выбранного
контейнера; при отсутствии — `ShowIdeologyRow`, при смене идеологии — обновление текста),
после 4 неудачных попыток на окно — отказ с ошибкой в лог. Контейнер выбирается
`FindBestStatsContainer`: в приоритете поле `meta_rows_container` (+1000 к счёту), иначе
любой активный `StatsRowsContainer` в иерархии окна с наибольшим числом строк
(«content_stats» — общий статистический контейнер — исключён).


## v0.10.3 — Game-исполнитель стратегических действий

В `GameKingdomActionExecutor` подключён вызов `WorldFeeder.TryExecuteStrategicState`. Через `IdeologyMechanicsEffects.TryApplyStrategicAction` теперь исполняются экстренная еда, оборона/мобилизация, демобилизация, восстановление легитимности, накопление золота и камня, инвестиции в развитие и запуск готовой идеологической ульты. Все эффекты используют существующие безопасные методы ресурсов, жителей и ядрового состояния; исключения перехватываются.

В `WorldFeeder.PlanStrategicActions` исправлен cooldown. Проверка больше не блокируется самим фактом предыдущего действия: новое решение допускается через 3 игровых года. Это соответствует `KingdomStrategySnapshot.ActionCooldownYears` и логике ядра.

### Проверка

Исходное ядро ранее прошло 83/83 теста. В текущем sandbox отсутствуют `dotnet`, `msbuild`, `csc`, `mcs` и `mono`, поэтому после изменений невозможно выполнить автоматическую компиляцию C# или тесты. Проверка против настоящих DLL WorldBox и NeoModLoader не выполнялась. Перед игровым релизом необходимо собрать `src/Game/RealPolitic.csproj` в окружении WorldBox 0.51.2 и пройти ручной чек-лист из `ROADMAP.md`.

### Что проверить в WorldBox

1. Включить мод и убедиться, что в логе появляются планы и результаты стратегических действий.
2. Создать голодное государство и проверить, что план `EmergencyFood` увеличивает запасы еды.
3. Создать военное государство с недостатком армии и проверить мобилизацию.
4. Проверить демобилизацию в мирном государстве с избытком войск.
5. Искусственно снизить легитимность и проверить восстановление состояния без исключения.
6. Проверить, что идеологическая ульта запускается один раз, действует заданное число игровых лет и не применяется повторно до cooldown.
7. Убедиться, что несовместимый API только пишет отказ в лог и не останавливает годовой цикл.


## v0.10.4 — строка стратегического плана в UI

Начат этап 12. `WorldFeeder` теперь хранит последний `KingdomActionPlan` для каждого государства и отдаёт его через `TryGetStrategicPlan`. В `Patches.ShowMechanicsRows` добавлена строка `RP_STRATEGY_TITLE` с локализованным действием и причиной, выбранной `KingdomStrategicPlanner`. В `RealPoliticMod` добавлено безопасное публичное свойство `Feeder` для чтения этого состояния UI.

Добавлены локализации `RP_STRATEGY_*` для русского и английского языков. Строка появляется в окне государства рядом с идеологией, ультой и шкалой угрозы. Если план ещё не создан или игровой API недоступен, UI сохраняет прежние строки и не вызывает исключение.

Сборка C# после изменений не выполнена: в текущем sandbox отсутствуют .NET SDK и альтернативные C# компиляторы. Перед использованием необходимо собрать проект против WorldBox 0.51.2/NeoModLoader и проверить окно государства вручную.


## v0.10.5 — дипломатическая сводка в UI

В `WorldFeeder` добавлен кэш `TryGetDiplomacySummary`. После синхронизации годового состояния для каждого государства считаются отношения `War`, `Alliance` и `Peace`; дополнительно отображается название активной унии из `PoliticalWorld.UnionOf`. В `Patches.ShowMechanicsRows` добавлена строка `RP_DIPLOMACY_TITLE` рядом со стратегическим планом, идеологией и угрозой.

Добавлены локализации `RP_DIPLOMACY_TITLE`, `RP_DIPLOMACY_WARS`, `RP_DIPLOMACY_ALLIANCES`, `RP_DIPLOMACY_PEACE` и `RP_DIPLOMACY_UNION` на русском и английском языках.

Проверка C#-сборки по-прежнему отложена до окружения с .NET SDK и DLL WorldBox 0.51.2/NeoModLoader.


## v0.10.6 — оценка приоритета в объяснимом плане

Строка стратегического плана в `Patches.ShowMechanicsRows` теперь отображает формат `Действие [оценка] — причина`. Оценка берётся из `KingdomActionPlan.Score` и форматируется с одним знаком после запятой. Это делает решения сравнимыми между государствами и сохраняет объяснимость выбора.

Перед упаковкой проверена целостность изменённого фрагмента `Patches.cs`; сборка против WorldBox/NeoModLoader по-прежнему требует окружения с .NET SDK.

## 0.53: порядок с архивами релизов (30.10.2026)

Владелец: «зачем ты сохраняешь старые версии zip-ов? только Workspace засоряешь».

- В `/home/user/deliverables/` держим **не больше двух последних версий** (install + source).
- При выпуске новой версии сам удаляю всё, что старше двух последних, без вопросов.
- Старые версии не теряются: исходники есть в `work/`, архив собирается `python3 tools/package-release.py <версия>`.
- Правило «старые файлы не удалять» относится **только** к папке Drive «Моды Worldbox», не к воркспейсу.
