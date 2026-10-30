using System;
using System.Collections.Generic;
using System.Linq;

namespace RealPolitic.Core;

/// <summary>
/// 0.31: Генератор идеологических государственных названий (не менее 20 вариантов на каждую из 15 идеологий).
/// Формирует полные названия государств по идеологии и базовому топониму (столице, культуре или стране):
/// «Вельгардская Империя», «Вольноградский Каганат», «Королевство Серебряный Яр»,
/// «Ольховский Рейх», «Вольная Территория Вольноград», «Советская Республика Беловодье» и т.д.
/// </summary>
public static class IdeologyStateNames
{
    // Внутренние коды подстановки:
    // %N% — базовый топоним («Вельгард», «Серебряный Яр», «Беловодье»)
    // %M% — прилагательное мужского рода («Вельгардский», «Серебряноярский»)
    // %F% — прилагательное женского рода («Вельгардская», «Серебряноярская»)
    // %O% — прилагательное среднего рода («Вельгардское», «Серебряноярское»)
    // %P% — прилагательное множественного числа («Вельгардские», «Серебряноярские»)

    private static readonly string[] TribalTemplates =
    {
        "Племя %N%",
        "Родовой Союз %N%",
        "%M% Родовой Союз",
        "Племенной Круг %N%",
        "%O% Племя",
        "Стойбище %N%",
        "%O% Стойбище",
        "Кочевье %N%",
        "%O% Кочевье",
        "Братство Родов %N%",
        "%M% Союз Племён",
        "Союз Племён %N%",
        "Клан %N%",
        "%M% Великий Клан",
        "Земля Предков %N%",
        "%F% Родовая Земля",
        "Племенная Община %N%",
        "%F% Племенная Община",
        "Круг Старейшин %N%",
        "%M% Круг Старейшин",
        "Вольное Племя %N%",
        "%P% Родовые Угодья"
    };

    private static readonly string[] MonarchyTemplates =
    {
        "Королевство %N%",
        "Соединённое Королевство %N%",
        "Независимое Королевство %N%",
        "Княжество %N%",
        "Великое Княжество %N%",
        "Светлейшее Княжество %N%",
        "Удельное Княжество %N%",
        "Царство %N%",
        "Независимое Царство %N%",
        "Герцогство %N%",
        "Великое Герцогство %N%",
        "Эрцгерцогство %N%",
        "Курфюршество %N%",
        "Маркграфство %N%",
        "Ландграфство %N%",
        "Пфальцграфство %N%",
        "%O% Королевство",
        "%O% Княжество",
        "%O% Царство",
        "%O% Великое Княжество",
        "%O% Герцогство",
    };

    /// <summary>
    /// 0.49: имена держав, где к власти пришли националисты. Это временное состояние:
    /// держава называет себя «Национальным Государством» до возврата прежнего строя.
    /// </summary>
    private static readonly string[] NationalismTemplates =
    {
        "Национальное Государство %N%",
        "%F% Народная Республика",
        "Республика %N%",
        "%F% Национальная Республика",
        "Свободная Республика %N%",
        "Народное Государство %N%",
        "%O% Народное Государство",
        "Государство %N%",
        "%M% Национальный Союз",
        "Союз %N%",
        "Национальный Союз %N%",
        "%M% Освободительный Союз",
        "Народный Союз %N%",
        "Земля %N%",
        "%F% Родная Земля",
        "Отечество %N%",
        "%O% Отечество",
        "Национальный Комитет %N%",
        "%M% Национальный Совет",
        "Совет %N%"
    };

    private static readonly string[] EmpireTemplates =
    {
        "Империя %N%",
        "%F% Империя",
        "Великая %F% Империя",
        "Священная %F% Империя",
        "Блистательная %F% Империя",
        "Древняя %F% Империя",
        "Преславная %F% Империя",
        "Необъятная %F% Империя",
        "Православная %F% Империя",
        "Самодержавная %F% Империя",
        "Вселенская %F% Империя",
        "Вечная %F% Империя",
        "Великая Империя %N%",
        "Священная Империя %N%",
        "Древняя Империя %N%",
        "Блистательная Империя %N%",
        "Вечная Империя %N%",
        "Держава %N%",
        "%F% Держава",
        "Великая Держава %N%",
    };

    private static readonly string[] KhaganateTemplates =
    {
        "Каганат %N%",
        "%M% Каганат",
        "Великий Каганат %N%",
        "Степной Каганат %N%",
        "Славный Каганат %N%",
        "Ханство %N%",
        "%O% Ханство",
        "Великое Ханство %N%",
        "Кочевое Ханство %N%",
        "Султанат %N%",
        "%M% Султанат",
        "Эмират %N%",
        "Бейлик %N%",
        "Юрт %N%",
        "Улус %N%",
        "Державный Улус %N%",
        "Орда %N%",
        "%F% Орда",
        "Великая %F% Орда",
        "Золотая Орда %N%",
    };

    private static readonly string[] FreeTerritoryTemplates =
    {
        "Вольный Город %N%",
        "Свободный Город %N%",
        "Вольная Территория %N%",
        "%F% Вольная Территория",
        "Свободное Государство %N%",
        "Свободная Республика %N%",
        "Вольная Республика %N%",
        "Нейтральная Территория %N%",
        "Вольная Община %N%",
        "%F% Вольная Община",
        "Свободная Община %N%",
        "Вольная Земля %N%",
        "Свободная Земля %N%",
        "Земля %N%",
        "Вольная Марка %N%",
        "Свободная Марка %N%",
        "Вольный Союз %N%",
        "Свободный Союз %N%",
        "Республика %N%",
        "%F% Республика",
    };

    private static readonly string[] TheocracyTemplates =
    {
        "Халифат %N%",
        "%M% Халифат",
        "Великий Халифат %N%",
        "Имамат %N%",
        "%M% Имамат",
        "Эмират %N%",
        "%M% Эмират",
        "Султанат %N%",
        "Патриархат %N%",
        "Католикосат %N%",
        "Экзархат %N%",
        "Митрополия %N%",
        "Архиепископство %N%",
        "Епископство %N%",
        "Аббатство %N%",
        "Княжество-Архиепископство %N%",
        "Княжество-Епископство %N%",
        "Духовный Орден %N%",
        "Священный Орден %N%",
        "Теократическое Государство %N%",
    };

    private static readonly string[] AristocracyTemplates =
    {
        "Шляхетская Республика %N%",
        "Аристократическая Республика %N%",
        "Дворянская Республика %N%",
        "Республика Двух Народов %N%",
        "Республика Шляхты %N%",
        "Сословная Республика %N%",
        "Рыцарская Республика %N%",
        "Княжество %N%",
        "Великое Княжество %N%",
        "Герцогство %N%",
        "Великое Герцогство %N%",
        "Эрцгерцогство %N%",
        "Маркграфство %N%",
        "Пфальцграфство %N%",
        "Ландграфство %N%",
        "Курфюршество %N%",
        "Графство %N%",
        "%O% Графство",
        "Баронство %N%",
        "Союз Благородных Домов %N%",
    };

    private static readonly string[] MerchantRepublicTemplates =
    {
        "Торговая Республика %N%",
        "Морская Республика %N%",
        "Купеческая Республика %N%",
        "Купеческая Держава %N%",
        "Ганзейский Союз %N%",
        "Ганза %N%",
        "Купеческий Союз %N%",
        "Торговый Союз %N%",
        "Союз Городов %N%",
        "Морской Союз %N%",
        "Республика Купцов %N%",
        "Торговая Компания %N%",
        "Вольная Гавань %N%",
        "Свободная Гавань %N%",
        "Вольный Город %N%",
        "Свободный Город %N%",
        "Морская Держава %N%",
        "Торговая Марка %N%",
        "Гильдейский Союз %N%",
        "Республика %N%",
    };

    private static readonly string[] MilitarismTemplates =
    {
        "Военный Орден %N%",
        "%M% Военный Орден",
        "Орден %N%",
        "%M% Орден",
        "Орденское Государство %N%",
        "Рейх %N%",
        "%M% Рейх",
        "Марка %N%",
        "%F% Марка",
        "Военная Марка %N%",
        "Крепостная Марка %N%",
        "Военная Держава %N%",
        "%F% Военная Держава",
        "Хунта %N%",
        "Гарнизонное Государство %N%",
        "Казарменное Государство %N%",
        "Гвардейское Государство %N%",
        "Солдатская Республика %N%",
        "Военная Республика %N%",
        "Военный Союз %N%",
    };

    private static readonly string[] EgalitarianismTemplates =
    {
        "Вольная Община %N%",
        "Свободная Община %N%",
        "Народная Община %N%",
        "Община %N%",
        "%F% Община",
        "Союз Общин %N%",
        "Федерация Общин %N%",
        "Братство Общин %N%",
        "Держава Общин %N%",
        "Республика %N%",
        "Свободная Республика %N%",
        "Трудовая Республика %N%",
        "Крестьянская Республика %N%",
        "Рабочая Республика %N%",
        "Земская Республика %N%",
        "Союз Равных %N%",
        "Мирской Союз %N%",
        "Всеобщая Община %N%",
        "Общинная Республика %N%",
        "Республика Общин %N%",
    };

    private static readonly string[] TechnocracyTemplates =
    {
        "Директория %N%",
        "%F% Директория",
        "Промышленная Директория %N%",
        "Техническая Директория %N%",
        "Директория Мастеров %N%",
        "Коллегия %N%",
        "Коллегия Мастеров %N%",
        "Коллегиальная Республика %N%",
        "Технократическая Республика %N%",
        "Промышленная Республика %N%",
        "Инженерная Республика %N%",
        "Академическая Республика %N%",
        "Республика Инженеров %N%",
        "Рациональная Республика %N%",
        "Совет Инженеров %N%",
        "Синдикат %N%",
        "Корпорация %N%",
        "Механическая Республика %N%",
        "Машинная Директория %N%",
        "Техническая Коллегия %N%",
    };

    private static readonly string[] LiberalismTemplates =
    {
        "Республика %N%",
        "%F% Республика",
        "Конституционная Республика %N%",
        "Парламентская Республика %N%",
        "Федеративная Республика %N%",
        "Либеральная Республика %N%",
        "Гражданская Республика %N%",
        "Свободная Республика %N%",
        "Демократическая Республика %N%",
        "Республика Граждан %N%",
        "Республика Свободных Граждан %N%",


        "Правовое Государство %N%",
        "Конституционное Государство %N%",
        "Парламентское Государство %N%",
        "Республика Закона %N%",
        "Союз Свободных Граждан %N%",
        "Открытая Республика %N%",
        "Республика Прав %N%",
        "Свободное Государство %N%",
        "Гражданский Союз %N%",
    };

    private static readonly string[] DemocracyTemplates =
    {
        "Республика %N%",
        "%F% Республика",
        "Демократическая Республика %N%",
        "Федеративная Республика %N%",
        "Соединённая Республика %N%",
        "Федерация %N%",
        "%F% Федерация",
        "Демократическая Федерация %N%",
        "Свободная Федерация %N%",
        "Конфедерация %N%",
        "Соединённые Штаты %N%",
        "Штаты %N%",
        "Союзная Республика %N%",
        "Союз Республик %N%",
        "Республиканский Союз %N%",
        "Демократический Союз %N%",
        "Республика Народа %N%",
        "Республика Граждан %N%",
        "Народная Федерация %N%",
        "Большая Республика %N%",
    };

    private static readonly string[] CommunismTemplates =
    {
        "Советская Республика %N%",
        "Социалистическая Республика %N%",
        "Советская Социалистическая Республика %N%",
        "Народная Республика %N%",
        "Народно-Демократическая Республика %N%",
        "Рабоче-Крестьянская Республика %N%",
        "Трудовая Республика %N%",
        "Рабочая Республика %N%",
        "Крестьянская Республика %N%",
        "Республика Советов %N%",
        "Союз Советских Республик %N%",
        "Советская Федерация %N%",
        "Социалистическая Федерация %N%",
        "Советская Держава %N%",
        "Республика Труда %N%",
        "Коммуна %N%",
        "%F% Коммуна",
        "Рабочая Коммуна %N%",
        "Народная Коммуна %N%",
        "Социалистическая Коммуна %N%",
    };

    private static readonly string[] EthnonationalismTemplates =
    {
        "Национальное Государство %N%",
        "Государство %N%",
        "Независимое Государство %N%",
        "Национальная Республика %N%",
        "Народное Государство %N%",
        "Национальная Держава %N%",
        "Государство-Нация %N%",
        "Родное Государство %N%",
        "Народная Держава %N%",
        "Национальный Союз %N%",
        "Народный Союз %N%",
        "Держава Народа %N%",
        "Национальное Отечество %N%",
        "Единое Государство %N%",
        "Кровное Государство %N%",
        "Земля Народа %N%",
        "Государство Рода %N%",
        "Национальная Федерация %N%",
        "Соборное Государство %N%",
        "Отеческое Государство %N%",
    };

    // Слова-маркеры идеологических титулов, которые очищаются при выделении базового топонима
    private static readonly string[] TitlePhrasesToStrip =
    {
        "Рабоче-Крестьянская Республика",
        "Народно-Социалистическая Держава",
        "Научно-Промышленный Союз",
        "Княжеско-Боярский Союз",
        "Республика Свободных Граждан",
        "Союз Советских Коммун",
        "Совет Народных Комиссаров",
        "Совет Гостей и Купцов",
        "Совет Старейших Родов",
        "Совет Магистров Наук",
        "Союз Благородных Домов",
        "Союз Вольных Городов",
        "Союз Вольных Общин",
        "Союз Трудовых Артелей",
        "Союз Свободных Земель",
        "Союз Гражданских Прав",
        "Союз Родной Крови",
        "Держава Чистой Крови",
        "Безвластная Территория",
        "Свободная Территория",
        "Вольная Территория",
        "Атаманская Вольница",
        "Народная Вольница",
        "Торговая Республика",
        "Купеческая Республика",
        "Коммерческая Республика",
        "Демократическая Республика",
        "Федеративная Республика",
        "Президентская Республика",
        "Парламентская Республика",
        "Либеральная Республика",
        "Эгалитарная Республика",
        "Академическая Республика",
        "Просвещённая Республика",
        "Благородная Республика",
        "Светлейшая Республика",
        "Социалистическая Республика",
        "Советская Республика",
        "Трудовая Республика",
        "Народная Республика",
        "Гражданская Республика",
        "Суверенная Республика",
        "Объединённая Республика",
        "Национальная Республика",
        "Свободная Республика",
        "Открытая Республика",
        "Красная Республика",
        "Высшая Республика",
        "Союзная Республика",
        "Земская Республика",
        "Республика Советов",
        "Республика Права",
        "Великое Королевство",
        "Великое Княжество",
        "Светлейшее Княжество",
        "Светлейшая Синьория",
        "Святейшее Государство",
        "Орденское Государство",
        "Рациональное Государство",
        "Правовое Государство",
        "Национальное Государство",
        "Народное Государство",
        "Государство Крови",
        "Крепость-Государство",
        "Священный Престол",
        "Небесный Престол",
        "Имперский Престол",
        "Королевский Престол",
        "Священный Синод",
        "Священный Орден",
        "Священный Завет",
        "Военный Орден",
        "Орден Знания",
        "Вольный Торговый Город",
        "Высший Учёный Совет",
        "Высокий Совет",
        "Военный Совет",
        "Генеральный Штаб",
        "Гарнизонный Округ",
        "Оборонительный Рубеж",
        "Свободный Рубеж",
        "Железный Рубеж",
        "Стальной Легион",
        "Стальной Пакт",
        "Железный Союз",
        "Вооружённый Пакт",
        "Ратный Пакт",
        "Академический Пакт",
        "Фронтовой Союз",
        "Пролетарский Фронт",
        "Отечественный Фронт",
        "Народный Корпус",
        "Национальный Порядок",
        "Национальный Бастион",
        "Тысячелетний Рейх",
        "Верховный Рейх",
        "Великий Каганат",
        "Золотой Каганат",
        "Степной Каганат",
        "Кочевой Каганат",
        "Вечный Каганат",
        "Белый Каганат",
        "Синий Каганат",
        "Верховный Каганат",
        "Непобедимый Каганат",
        "Великая Империя",
        "Блистательная Империя",
        "Священная Империя",
        "Самодержавная Империя",
        "Верховная Империя",
        "Неделимая Империя",
        "Вечная Империя",
        "Державная Империя",
        "Славная Империя",
        "Имперская Держава",
        "Корона Империи",
        "Имперский Союз",
        "Имперский Домен",
        "Благородный Домен",
        "Лабораторный Домен",
        "Великая Орда",
        "Державный Улус",
        "Великий Улус",
        "Великий Клан",
        "Родовой Союз",
        "Союз Племён",
        "Племенной Круг",
        "Круг Старейшин",
        "Атаманский Круг",
        "Вольный Круг",
        "Народный Круг",
        "Круг Равных",
        "Вольный Край",
        "Вольная Земля",
        "Вольная Сечь",
        "Вольная Община",
        "Вольное Поле",
        "Вольный Стан",
        "Вольное Племя",
        "Свободный Удел",
        "Божественный Удел",
        "Рыцарский Удел",
        "Свободный Порт",
        "Свободный Альянс",
        "Свободное Вече",
        "Народное Вече",
        "Купеческое Вече",
        "Народное Собрание",
        "Дворянское Собрание",
        "Собрание Граждан",
        "Мирской Сход",
        "Братство Родов",
        "Братство Равных",
        "Товарищество Равных",
        "Товарищество Купцов",
        "Содружество Свободы",
        "Содружество Народов",
        "Демократическое Содружество",
        "Народное Содружество",
        "Социалистическое Отечество",
        "Великая Родина",
        "Земля Предков",
        "Родовая Земля",
        "Боярская Земля",
        "Вечевая Земля",
        "Общинная Земля",
        "Полковая Земля",
        "Благословенная Земля",
        "Родовые Угодья",
        "Племенная Община",
        "Народная Коммуна",
        "Трудовая Коммуна",
        "Коммуна Трудящихся",
        "Союз Коммун",
        "Союз Трудящихся",
        "Рабочий Союз",
        "Коммунистический Союз",
        "Советский Союз",
        "Демократический Союз",
        "Республиканский Союз",
        "Парламентский Союз",
        "Конституционный Союз",
        "Гражданский Союз",
        "Земский Союз",
        "Равноправный Союз",
        "Инженерный Союз",
        "Теократический Союз",
        "Аристократический Союз",
        "Торговый Союз",
        "Биржевой Союз",
        "Караванный Союз",
        "Союз Гильдий",
        "Кровный Союз",
        "Купеческая Лига",
        "Приморская Лига",
        "Парламентская Лига",
        "Гражданская Федерация",
        "Общинная Федерация",
        "Федерация Труда",
        "Свободная Конфедерация",
        "Соединённые Провинции",
        "Боярская Дума",
        "Палата Лордов",
        "Палата Общин",
        "Хартия Свободы",
        "Золотая Фактория",
        "Торговая Фактория",
        "Торговый Дом",
        "Княжеский Дом",
        "Монетный Двор",
        "Синдикат Разума",
        "Институт Управления",
        "Коллегия Архитекторов",
        "Научная Коллегия",
        "Просвещённая Директория",
        "Технократический Директорат",
        "Священная Митрополия",
        "Обитель Света",
        "Сословие Вельмож",
        "Сенат и Народ",
        "Народная Артель",
        "Единая Нация",
        "Храмовая Держава",
        "Заветная Держава",
        "Соборная Держава",
        "Патрицианская Держава",
        "Ярмарочная Держава",
        "Военная Держава",
        "Ратная Держава",
        "Гвардейская Держава",
        "Вечевая Держава",
        "Инженерная Держава",
        "Конституционная Держава",
        "Гражданская Держава",
        "Свободная Держава",
        "Федеративная Держава",
        "Пролетарская Держава",
        "Национальная Держава",
        "Родовая Держава",
        "Держава Нации",
        "Национал-Держава",
        "Духовное Царство",
        "Военное Наместничество",
        "Военная Хунта",
        "Королевство",
        "Княжество",
        "Герцогство",
        "Царство",
        "Империя",
        "Империум",
        "Каганат",
        "Ханство",
        "Орда",
        "Улус",
        "Курултай",
        "Вольница",
        "Сечь",
        "Теократия",
        "Патриархат",
        "Архиепископат",
        "Первосвятительство",
        "Святилище",
        "Аристократия",
        "Сенат",
        "Нобелитет",
        "Синьория",
        "Патрициат",
        "Ганза",
        "Стратократия",
        "Маршальство",
        "Комендатура",
        "Цитадель",
        "Технократия",
        "Директорат",
        "Технополис",
        "Технат",
        "Республика",
        "Самодержавие",
        "Монархия",
        "Держава",
        "Корона",
        "Престол",
        "Трон",
        "Рейх",
        "Племя",
        "Стойбище",
        "Кочевье",
        "Клан"
    };

    private static readonly string[] LeadingAdjectivesToStrip =
    {
        "Великая", "Великий", "Великое", "Блистательная", "Священная", "Священный",
        "Славная", "Верховная", "Верховный", "Неделимая", "Вечная", "Вечный",
        "Державная", "Державный", "Золотой", "Белый", "Синий", "Степной",
        "Кочевой", "Непобедимый", "Вольная", "Вольный", "Вольное", "Свободная",
        "Свободный", "Свободное", "Народная", "Народный", "Народное"
    };

    /// <summary>
    /// Возвращает массив шаблонов (не менее 20) для указанной идеологии.
    /// </summary>
    public static IReadOnlyList<string> GetTemplates(IdeologyType ideology)
    {
        return ideology switch
        {
            IdeologyType.Tribalism => TribalTemplates,
            IdeologyType.Monarchy => MonarchyTemplates,
            IdeologyType.Empire => EmpireTemplates,
            IdeologyType.Khaganate => KhaganateTemplates,
            IdeologyType.FreeTerritory => FreeTerritoryTemplates,
            IdeologyType.Theocracy => TheocracyTemplates,
            IdeologyType.Aristocracy => AristocracyTemplates,
            IdeologyType.MerchantRepublic => MerchantRepublicTemplates,
            IdeologyType.Militarism => MilitarismTemplates,
            IdeologyType.Egalitarianism => EgalitarianismTemplates,
            IdeologyType.Technocracy => TechnocracyTemplates,
            IdeologyType.Liberalism => LiberalismTemplates,
            IdeologyType.Democracy => DemocracyTemplates,
            IdeologyType.Communism => CommunismTemplates,
            IdeologyType.Ethnonationalism => EthnonationalismTemplates,
            IdeologyType.Nationalism => NationalismTemplates,
            _ => MonarchyTemplates
        };
    }

    /// <summary>
    /// Возвращает все 20+ отформатированных вариантов названия для заданной идеологии и базового топонима.
    /// </summary>
    public static List<string> GetAllVariants(IdeologyType ideology, string? rawName, string? capitalName = null)
    {
        string toponym = ExtractBaseToponym(rawName, capitalName);
        InflectAdjectives(toponym, out string masc, out string fem, out string neut, out string plural);
        IReadOnlyList<string> templates = GetTemplates(ideology);
        List<string> result = new List<string>(templates.Count);
        for (int i = 0; i < templates.Count; i++)
        {
            string formatted = FormatTemplate(templates[i], toponym, masc, fem, neut, plural);
            if (!result.Contains(formatted, StringComparer.OrdinalIgnoreCase))
            {
                result.Add(formatted);
            }
        }
        return result;
    }

    /// <summary>
    /// Генерирует идеологическое название государства.
    /// Для племенного строя (<see cref="IdeologyType.Tribalism"/>) использует этноним от биома/культуры/столицы,
    /// для всех остальных идеологий выбирает один из 20+ шаблонов соответствующей идеологии.
    /// </summary>
    public static string GenerateStateName(
        IdeologyType ideology,
        string? rawName,
        string? capitalName = null,
        int kingdomId = 1,
        int variantSeed = 0,
        ICollection<string>? existingNames = null,
        string? biomeId = null,
        string? cultureName = null)
    {
        if (ideology == IdeologyType.Tribalism)
        {
            return TribalCivilization.GenerateTribalName(capitalName ?? rawName, biomeId, kingdomId, existingNames, cultureName);
        }

        // 0.31.17: имя государства — коренное имя вместе с ярлыком строя
        // («Королевство Гогиш», «Гогишский Рейх»). Коренное имя («Гогиш») остаётся
        // для названий войн и союзов, его выделяет StateNaming.Root.
        string root = ExtractBaseToponym(rawName, capitalName);
        return StateNaming.MapName(ideology, root, kingdomId, capitalName);
    }

    /// <summary>Прежний выбор из шаблонов с ярлыком (оставлен для совместимости и тестов).</summary>
    public static string GenerateLabelledStateName(
        IdeologyType ideology,
        string? rawName,
        string? capitalName = null,
        int kingdomId = 1,
        int variantSeed = 0,
        ICollection<string>? existingNames = null)
    {
        List<string> variants = GetAllVariants(ideology, rawName, capitalName);
        if (variants.Count == 0)
        {
            return ExtractBaseToponym(rawName, capitalName);
        }

        int startIdx = Math.Abs((kingdomId * 31) + (variantSeed * 17)) % variants.Count;
        for (int i = 0; i < variants.Count; i++)
        {
            string candidate = variants[(startIdx + i) % variants.Count];
            if (existingNames == null || !existingNames.Contains(candidate))
            {
                return candidate;
            }
        }

        return variants[startIdx];
    }

    /// <summary>
    /// Убирает из имени слова-ярлыки строя («Королевство», «Рейх», «Орда», ведущие прилагательные),
    /// не пытаясь ничего «восстанавливать». «Королевство Гогиш» → «Гогиш», «Гогишский Рейх» → «Гогишский».
    /// </summary>
    private static HashSet<string> _titleWords;

    /// <summary>Слова, которые в наших шаблонах являются титулом, а не частью имени страны.</summary>
    public static HashSet<string> TitleWords()
    {
        if (_titleWords != null)
        {
            return _titleWords;
        }
        HashSet<string> words = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (string phrase in TitlePhrasesToStrip)
        {
            foreach (string word in phrase.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries))
            {
                words.Add(word);
            }
        }
        foreach (string adj in LeadingAdjectivesToStrip)
        {
            words.Add(adj);
        }
        foreach (IdeologyType type in Enum.GetValues(typeof(IdeologyType)))
        {
            foreach (string template in GetTemplates(type))
            {
                foreach (string word in template.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries))
                {
                    if (word.StartsWith("%", StringComparison.Ordinal))
                    {
                        continue;
                    }
                    words.Add(word);
                }
            }
        }
        _titleWords = words;
        return words;
    }

    /// <summary>
    /// Убирает из строки все слова-титулы: «Державная Империя Добрвеция» → «Добрвеция».
    /// Слова-титулы ищутся по любому месту строки, поэтому форма «Имя Титул» тоже очищается.
    /// </summary>
    public static string StripAllTitles(string? name)
    {
        string candidate = name == null ? string.Empty : name.Trim();
        if (candidate.Length == 0)
        {
            return string.Empty;
        }
        HashSet<string> titles = TitleWords();
        string[] words = candidate.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
        List<string> keep = new List<string>(words.Length);
        foreach (string word in words)
        {
            if (!titles.Contains(word))
            {
                keep.Add(word);
            }
        }
        if (keep.Count > 0)
        {
            return string.Join(" ", keep);
        }
        // 0.48.4: все слова оказались «титульными» — раньше возвращалось имя целиком,
        // и оно удваивалось: «Независимое Королевство» + «Независимое Королевство Край»
        // = «Независимое Королевство Независимое Королевство Край» (катка 29.10.2026,
        // год 57). Так бывает, когда сама держава называется словом из титульного
        // набора — например «Край».
        // Теперь считаем коренным именем ПОСЛЕДНЕЕ слово: в наших шаблонах топоним
        // стоит в конце («Королевство Гогиш», «Вольный Край»).
        return words[words.Length - 1];
    }

    /// <summary>
    /// Убирает из строки слова-ярлыки строя, не пытаясь ничего «восстанавливать».
    /// </summary>
    public static string StripLabelWords(string? name)
    {
        string candidate = name == null ? string.Empty : name.Trim();
        if (candidate.Length == 0)
        {
            return string.Empty;
        }
        bool changed = true;
        int guard = 0;
        while (changed && guard++ < 4)
        {
            changed = false;
            foreach (string phrase in TitlePhrasesToStrip)
            {
                if (candidate.Length > phrase.Length + 1)
                {
                    if (candidate.StartsWith(phrase + " ", StringComparison.OrdinalIgnoreCase))
                    {
                        candidate = candidate.Substring(phrase.Length + 1).Trim();
                        changed = true;
                        break;
                    }
                    if (candidate.EndsWith(" " + phrase, StringComparison.OrdinalIgnoreCase))
                    {
                        candidate = candidate.Substring(0, candidate.Length - phrase.Length - 1).Trim();
                        changed = true;
                        break;
                    }
                }
            }
            if (changed)
            {
                continue;
            }
            foreach (string adj in LeadingAdjectivesToStrip)
            {
                if (candidate.Length > adj.Length + 1 && candidate.StartsWith(adj + " ", StringComparison.OrdinalIgnoreCase))
                {
                    candidate = candidate.Substring(adj.Length + 1).Trim();
                    changed = true;
                    break;
                }
            }
        }
        return candidate;
    }

    /// <summary>
    /// Есть ли в имени ярлык строя («Королевство Гогиш», «Гогишский Рейх» → да; «Гогиш» → нет).
    /// Нужен, чтобы не «поправлять» коренное имя, которое уже ничем не отягощено.
    /// </summary>
    public static bool HasIdeologyLabel(string? name)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            return false;
        }
        string value = name!.Trim();
        foreach (string phrase in TitlePhrasesToStrip)
        {
            if (value.Length > phrase.Length + 1
                && (value.StartsWith(phrase + " ", StringComparison.OrdinalIgnoreCase)
                    || value.EndsWith(" " + phrase, StringComparison.OrdinalIgnoreCase)))
            {
                return true;
            }
        }
        foreach (string adj in LeadingAdjectivesToStrip)
        {
            if (value.Length > adj.Length + 1 && value.StartsWith(adj + " ", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }
        return false;
    }

    /// <summary>
    /// Извлекает чистый базовый топоним из старого названия государства, племенного этнонима или столицы
    /// (например: «Королевство Вельгард» -> «Вельгард», «Вельгардская Империя» -> «Вельгард»,
    /// «Чащобники» -> «Чащобия», «Ольховяне» -> «Ольховия»).
    /// </summary>
    public static string ExtractBaseToponym(string? rawName, string? capitalName = null)
    {
        string candidate = !string.IsNullOrWhiteSpace(rawName) ? rawName!.Trim() : string.Empty;
        if (string.IsNullOrWhiteSpace(candidate) || candidate.StartsWith("#"))
        {
            candidate = !string.IsNullOrWhiteSpace(capitalName) ? capitalName!.Trim() : "Вельгард";
        }

        // Если это племенной этноним — преобразуем в базовый топоним страны
        if (TribalCivilization.IsKnownTribalEthnonym(candidate))
        {
            candidate = TribalCivilization.GenerateBaseToponym(candidate, capitalName);
        }

        // Снимаем известные идеологические титулы в начале или в конце строки
        bool changed = true;
        int guard = 0;
        while (changed && guard++ < 4)
        {
            changed = false;
            foreach (string phrase in TitlePhrasesToStrip)
            {
                if (candidate.Length > phrase.Length + 1)
                {
                    if (candidate.StartsWith(phrase + " ", StringComparison.OrdinalIgnoreCase))
                    {
                        candidate = candidate.Substring(phrase.Length + 1).Trim();
                        changed = true;
                        break;
                    }
                    if (candidate.EndsWith(" " + phrase, StringComparison.OrdinalIgnoreCase))
                    {
                        candidate = candidate.Substring(0, candidate.Length - phrase.Length - 1).Trim();
                        changed = true;
                        break;
                    }
                }
            }
            foreach (string adj in LeadingAdjectivesToStrip)
            {
                if (candidate.Length > adj.Length + 1 && candidate.StartsWith(adj + " ", StringComparison.OrdinalIgnoreCase))
                {
                    candidate = candidate.Substring(adj.Length + 1).Trim();
                    changed = true;
                    break;
                }
            }
        }

        // Если остался только относительный адъектив («Вельгардская», «Вольноградский», «Беловодское»)
        // и у нас есть имя столицы — предпочитаем чистое имя столицы или восстанавливаем основу
        if (!candidate.Contains(' '))
        {
            string restored = TryRestoreNounFromAdjective(candidate, capitalName);
            if (!string.IsNullOrWhiteSpace(restored))
            {
                candidate = restored;
            }
        }

        if (string.IsNullOrWhiteSpace(candidate))
        {
            candidate = !string.IsNullOrWhiteSpace(capitalName) ? capitalName!.Trim() : "Вельгард";
        }

        return CapitalizeWords(candidate);
    }

    /// <summary>
    /// Строит русские относительные прилагательные (мужской, женский, средний род и мн. число)
    /// от заданного топонима («Вельгард» -> «Вельгардский / Вельгардская / Вельгардское / Вельгардские»,
    /// «Серебряный Яр» -> «Серебряноярский», «Ольховия» -> «Ольховский»).
    /// </summary>
    public static void InflectAdjectives(
        string toponym,
        out string masculine,
        out string feminine,
        out string neuter,
        out string plural)
    {
        string clean = string.IsNullOrWhiteSpace(toponym) ? "Вельгард" : toponym.Trim();
        string adjStem = BuildAdjectiveStem(clean);
        masculine = adjStem + "ий";
        feminine = adjStem + "ая";
        neuter = adjStem + "ое";
        plural = adjStem + "ие";
    }

    /// <summary>
    /// Название войны по коренным именам сторон, в духе «Российско-Украинская война»:
    /// «Бульунско-Альтасеронская война». Ярлык идеологии (Монархия, Рейх, Теократия…) в название
    /// не попадает. Возвращает null, если коренное имя одной из сторон не определить.
    /// </summary>
    public static string? WarTitle(string? firstSideName, string? secondSideName)
    {
        if (string.IsNullOrWhiteSpace(firstSideName) || string.IsNullOrWhiteSpace(secondSideName)
            || firstSideName!.Trim().StartsWith("#") || secondSideName!.Trim().StartsWith("#"))
        {
            return null;
        }
        // Коренное имя без титула: «Маркграфство Альталатия» → «Альталатия».
        // Раньше здесь вызывался ExtractBaseToponym, и в название войны попадал титул
        // («Маркграфствоальталатско-Важская война»).
        string first = StateNaming.Root(firstSideName);
        string second = StateNaming.Root(secondSideName);
        if (string.IsNullOrWhiteSpace(first) || string.IsNullOrWhiteSpace(second) || first.StartsWith("#") || second.StartsWith("#"))
        {
            return null;
        }
        InflectAdjectives(first, out _, out _, out string neuterFirst, out _);
        InflectAdjectives(second, out _, out string femSecond, out _, out _);
        // «Бульунское» → «Бульунско» + «-» + «Альтасеронская» + «война».
        string firstPart = neuterFirst.EndsWith("ое", StringComparison.Ordinal) ? neuterFirst.Substring(0, neuterFirst.Length - 1) : neuterFirst;
        return firstPart + "-" + femSecond + " война";
    }

    private static string BuildAdjectiveStem(string toponym)
    {
        string[] words = toponym.Split(new[] { ' ', '-' }, StringSplitOptions.RemoveEmptyEntries);
        if (words.Length >= 2)
        {
            // Составной топоним («Серебряный Яр» -> «Серебряноярск», «Белая Гавань» -> «Белогаванск»)
            string first = words[0];
            string second = words[words.Length - 1];
            string firstPrefix = StripAdjectiveEnding(first);
            string secondStem = StripNounEndingForAdjective(second).ToLowerInvariant();
            string combined = CapitalizeFirst(firstPrefix + "о" + secondStem);
            if (firstPrefix.EndsWith("о", StringComparison.OrdinalIgnoreCase) || firstPrefix.EndsWith("е", StringComparison.OrdinalIgnoreCase))
            {
                combined = CapitalizeFirst(firstPrefix + secondStem);
            }
            return AppendSkSuffix(combined);
        }

        string single = words.Length == 1 ? words[0] : "Вельгард";
        string nounStem = StripNounEndingForAdjective(single);
        return AppendSkSuffix(CapitalizeFirst(nounStem));
    }

    private static string StripAdjectiveEnding(string word)
    {
        string[] endings = { "ый", "ий", "ой", "ая", "яя", "ое", "ее", "ие", "ые" };
        foreach (string end in endings)
        {
            if (word.Length > end.Length + 2 && word.EndsWith(end, StringComparison.OrdinalIgnoreCase))
            {
                return word.Substring(0, word.Length - end.Length);
            }
        }
        return StripNounEndingForAdjective(word);
    }

    private static string StripNounEndingForAdjective(string word)
    {
        if (string.IsNullOrWhiteSpace(word))
            return "Вельгард";

        string w = word.Trim();
        // Если уже кончается на -ск / -цк («Ольховск», «Полоцк») — основа для -ский уже готова
        if (w.Length > 4 && (w.EndsWith("ск", StringComparison.OrdinalIgnoreCase) || w.EndsWith("цк", StringComparison.OrdinalIgnoreCase)))
        {
            return w;
        }

        string[] endings = { "полье", "водье", "озерье", "бережье", "морье", "горье", "лесье", "речье", "ия", "ья", "ье", "ово", "ево", "ино", "ка", "ы", "и", "а", "я", "о", "е", "ь", "й" };
        foreach (string end in endings)
        {
            if (w.Length > end.Length + 2 && w.EndsWith(end, StringComparison.OrdinalIgnoreCase))
            {
                if (end == "полье") return w.Substring(0, w.Length - 5) + "поль";
                if (end == "водье") return w.Substring(0, w.Length - 5) + "вод";
                if (end == "озерье") return w.Substring(0, w.Length - 6) + "озер";
                if (end == "бережье") return w.Substring(0, w.Length - 7) + "берег";
                if (end == "морье") return w.Substring(0, w.Length - 5) + "мор";
                if (end == "горье") return w.Substring(0, w.Length - 5) + "гор";
                if (end == "лесье") return w.Substring(0, w.Length - 5) + "лес";
                if (end == "речье") return w.Substring(0, w.Length - 5) + "реч";
                if (end == "ка" && w.Length > 4) return w.Substring(0, w.Length - 2) + "ов";
                return w.Substring(0, w.Length - end.Length);
            }
        }
        return w;
    }

    private static string AppendSkSuffix(string stem)
    {
        if (string.IsNullOrWhiteSpace(stem))
            return "Вельгардск";

        if (stem.EndsWith("ск", StringComparison.OrdinalIgnoreCase) || stem.EndsWith("цк", StringComparison.OrdinalIgnoreCase))
        {
            return stem;
        }

        char last = char.ToLowerInvariant(stem[stem.Length - 1]);
        if (last == 'к' || last == 'ч' || last == 'ц')
        {
            return stem.Substring(0, stem.Length - 1) + "цк";
        }
        if (last == 'г' || last == 'ж' || last == 'з')
        {
            return stem.Substring(0, stem.Length - 1) + "жск";
        }
        if (last == 'х' || last == 'ш' || last == 'щ' || last == 'с')
        {
            return stem + "ск";
        }
        return stem + "ск";
    }

    private static string TryRestoreNounFromAdjective(string word, string? capitalName)
    {
        string[] adjEndings = { "ский", "ская", "ское", "ские", "цкий", "цкая", "цкое", "цкие" };
        foreach (string end in adjEndings)
        {
            if (word.Length > end.Length + 2 && word.EndsWith(end, StringComparison.OrdinalIgnoreCase))
            {
                if (!string.IsNullOrWhiteSpace(capitalName) && !capitalName!.StartsWith("#"))
                {
                    return capitalName.Trim();
                }
                string stem = word.Substring(0, word.Length - end.Length);
                if (stem.EndsWith("град", StringComparison.OrdinalIgnoreCase)
                    || stem.EndsWith("гард", StringComparison.OrdinalIgnoreCase)
                    || stem.EndsWith("бург", StringComparison.OrdinalIgnoreCase)
                    || stem.EndsWith("поль", StringComparison.OrdinalIgnoreCase)
                    || stem.EndsWith("бор", StringComparison.OrdinalIgnoreCase)
                    || stem.EndsWith("яр", StringComparison.OrdinalIgnoreCase))
                {
                    return stem;
                }
                return stem + "ия";
            }
        }
        return word;
    }

    private static string FormatTemplate(string template, string toponym, string masc, string fem, string neut, string plural)
    {
        return template
            .Replace("%N%", toponym)
            .Replace("%M%", masc)
            .Replace("%F%", fem)
            .Replace("%O%", neut)
            .Replace("%P%", plural);
    }

    private static string CapitalizeFirst(string s)
    {
        if (string.IsNullOrEmpty(s))
            return s;
        if (s.Length == 1)
            return s.ToUpperInvariant();
        return char.ToUpperInvariant(s[0]) + s.Substring(1);
    }

    private static string CapitalizeWords(string s)
    {
        if (string.IsNullOrWhiteSpace(s))
            return s;
        string[] parts = s.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
        for (int i = 0; i < parts.Length; i++)
        {
            parts[i] = CapitalizeFirst(parts[i]);
        }
        return string.Join(" ", parts);
    }
}
