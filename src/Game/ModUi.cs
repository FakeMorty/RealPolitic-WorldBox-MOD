using System;
using System.Reflection;
using NeoModLoader.services;

namespace RealPolitic;

/// <summary>
/// 0.23.6: маленький набор «кирпичиков» для содержимого модовых окон (конституция, родословная).
///
/// Почему он появился. Раньше окна рисовали текст «вручную»: каждому объекту задавали
/// центрированные якоря и координаты. В пустом окне NML (`windows/empty`) область прокрутки
/// всего ~232 единицы шириной, а страница конституции была 470 — текст уезжал за левый край;
/// у самодельных `Text` не выставлялся шрифт, а Unity без шрифта не рисует ничего — окно
/// родословной оставалось пустым.
///
/// Теперь делаем как сам NeoModLoader в своих окнах: на `Content` вешаем
/// `VerticalLayoutGroup` + `ContentSizeFitter`, а элементы просто добавляем по порядку —
/// Unity сам ставит их столбиком, считает высоту и включает прокрутку.
/// Всё через рефлексию: сборка мода не зависит от версии UnityEngine.UI.
/// </summary>
public static class ModUi
{
	/// <summary>Ширина «страницы» — чуть уже области прокрутки пустого окна NML (232).</summary>
	public const float PageWidth = 204f;

	private const string UiAsm = ", UnityEngine.UI";

	private const string CoreAsm = ", UnityEngine.CoreModule";

	private static object _font;

	// ---------------------------------------------------------------- автораскладка Content

	/// <summary>Включает вертикальную автораскладку на Content окна (один раз на объект).</summary>
	public static void SetupAutoLayout(object contentTransform, float spacing = 2f, float padTop = 8f)
	{
		try
		{
			object go = GameRefs.ReadMember(contentTransform, "gameObject");
			if (go == null)
			{
				return;
			}
			Type vlgType = Type.GetType("UnityEngine.UI.VerticalLayoutGroup" + UiAsm);
			Type fitterType = Type.GetType("UnityEngine.UI.ContentSizeFitter" + UiAsm);
			if (vlgType == null || fitterType == null)
			{
				return;
			}
			object vlg = GameRefs.Invoke(go, "GetComponent", vlgType);
			if (!IsAlive(vlg))
			{
				vlg = GameRefs.Invoke(go, "AddComponent", vlgType);
			}
			SetProperty(vlg, "childAlignment", Enum("UnityEngine.TextAnchor" + CoreAsm, "UpperCenter"));
			SetProperty(vlg, "childControlWidth", false);
			SetProperty(vlg, "childControlHeight", true);
			SetProperty(vlg, "childForceExpandWidth", false);
			SetProperty(vlg, "childForceExpandHeight", false);
			SetProperty(vlg, "spacing", spacing);
			SetProperty(vlg, "padding", RectOffset(4, 4, (int)padTop, 10));
			object fitter = GameRefs.Invoke(go, "GetComponent", fitterType);
			if (!IsAlive(fitter))
			{
				fitter = GameRefs.Invoke(go, "AddComponent", fitterType);
			}
			SetProperty(fitter, "verticalFit", Enum("UnityEngine.UI.ContentSizeFitter+FitMode" + UiAsm, "PreferredSize"));
			SetProperty(fitter, "horizontalFit", Enum("UnityEngine.UI.ContentSizeFitter+FitMode" + UiAsm, "Unconstrained"));
			// 0.23.8: Content — колонка фиксированной ширины (страница + отступы), а её центр
			// вычисляем по РЕАЛЬНОМУ центру рамки Scroll View, а не по Viewport:
			// у префаба windows/empty NML сдвигает Viewport (localPosition (-131, 135), sizeDelta (30, 0)),
			// поэтому «центр Viewport» ≠ центр окна, и текст уезжал влево на кнопки (скриншоты 391/394).
			object rect = GameRefs.Invoke(go, "GetComponent", Type.GetType("UnityEngine.RectTransform" + CoreAsm));
			SetProperty(rect, "anchorMin", Vector2(0f, 1f));
			SetProperty(rect, "anchorMax", Vector2(0f, 1f));
			SetProperty(rect, "pivot", Vector2(0f, 1f));
			float width = PageWidth + 8f;
			SetProperty(rect, "sizeDelta", Vector2(width, 10f));
			SetProperty(rect, "anchoredPosition", Vector2(CenteredContentX(go, width), 0f));
		}
		catch (Exception ex)
		{
			LogService.LogError("[Real Politic] автораскладка окна не включена: " + ex.Message);
		}
	}

	/// <summary>X-координата (anchoredPosition при якоре/pivot «лево-верх») левого края колонки шириной width,
	/// при которой колонка стоит ровно по центру рамки «Background/Scroll View». Считается через мировые
	/// координаты, поэтому не зависит от того, как NML сдвинул Viewport. При любой ошибке — 0.</summary>
	public static float CenteredContentX(object contentGo, float width)
	{
		try
		{
			Type rtType = Type.GetType("UnityEngine.RectTransform" + CoreAsm);
			object content = GameRefs.ReadMember(contentGo, "transform");
			object viewport = GameRefs.ReadMember(content, "parent");
			object scroll = GameRefs.ReadMember(viewport, "parent");
			if (viewport == null || scroll == null)
			{
				return 0f;
			}
			object scrollRt = GameRefs.Invoke(scroll, "GetComponent", rtType);
			object viewRt = GameRefs.Invoke(viewport, "GetComponent", rtType);
			// Центр рамки в её локальных координатах → мировые → локальные координаты Viewport.
			object scrollRect = GameRefs.ReadMember(scrollRt, "rect");
			object center = GameRefs.ReadMember(scrollRect, "center");
			float cx = Convert.ToSingle(GameRefs.ReadMember(center, "x"));
			float cy = Convert.ToSingle(GameRefs.ReadMember(center, "y"));
			object world = GameRefs.Invoke(scrollRt, "TransformPoint", Vector3(cx, cy, 0f));
			object local = GameRefs.Invoke(viewRt, "InverseTransformPoint", world);
			float centerX = Convert.ToSingle(GameRefs.ReadMember(local, "x"));
			// anchoredPosition считается от левого края Viewport (rect.x в его локальных координатах).
			object viewRect = GameRefs.ReadMember(viewRt, "rect");
			float leftX = Convert.ToSingle(GameRefs.ReadMember(viewRect, "x"));
			float x = centerX - width / 2f - leftX;
			LogService.LogInfo("[Real Politic] центровка окна: центр рамки x=" + centerX.ToString("0.#") + ", левый край Viewport x=" + leftX.ToString("0.#") + " → колонка от x=" + x.ToString("0.#"));
			return x;
		}
		catch (Exception ex)
		{
			LogService.LogError("[Real Politic] центровка окна не удалась: " + ex.Message);
			return 0f;
		}
	}

	// ---------------------------------------------------------------- мерка текста

	/// <summary>
	/// 0.48.3: сколько строк займёт текст заданной ширины.
	///
	/// Зачем это нужно. Раньше высота строки считалась «на глазок» — по числу букв
	/// и ширине окна (см. KmudWindow.RowHeight). Оценка занижала высоту, и на экране
	/// владельца (снимки 540–543) выходила каша: вторая строка заголовка
	/// «КОНФЕРЕНЦИЯ МИРНОГО / УРЕГУЛИРОВАНИЯ ДЕЛ» рисовалась под строкой «съезд №1 …»,
	/// подписи вариантов налезали на золотые полосы голосования, а часть строк
	/// обрезалась краем окна. Хуже всего: игра на любом языке пишет кириллицей
	/// шириной около 0.6 от кегля, а оценка стояла 0.52 — на длинных словах не хватало.
	///
	/// Теперь считаем честно: разбиваем на слова, набиваем строку, пока влезает.
	/// Одно длинное слово, которое не влезает целиком, занимает несколько строк.
	/// </summary>
	public static int LineCount(string text, float size, float width)
	{
		if (string.IsNullOrEmpty(text))
		{
			return 1;
		}
		if (width <= 1f)
		{
			return 1;
		}
		// Средняя ширина знака кириллицы у шрифта WorldBox — примерно 0.60 кегля.
		float perChar = Math.Max(2.6f, size * 0.62f);
		float limit = width;
		int lines = 1;
		float used = 0f;
		string[] words = text.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
		for (int w = 0; w < words.Length; w++)
		{
			string word = words[w];
			float wordWidth = word.Length * perChar;
			// Слишком длинное слово: съедает столько целых строк, сколько нужно.
			if (wordWidth > limit)
			{
				int fills = (int)Math.Ceiling(wordWidth / limit);
				if (used > 0f)
				{
					lines++;
					used = 0f;
				}
				lines += fills - 1;
				used = wordWidth - (fills - 1) * limit;
				continue;
			}
			if (used <= 0f)
			{
				used = wordWidth;
				continue;
			}
			if (used + perChar + wordWidth <= limit)
			{
				used += perChar + wordWidth;
				continue;
			}
			lines++;
			used = wordWidth;
		}
		return lines;
	}

	/// <summary>Высота текста с учётом переноса: строки × (кегль + 2) + небольшой запас.</summary>
	public static float TextHeight(string text, float size, float width)
	{
		int lines = LineCount(text, size, width);
		return lines * (size + 2f) + 2f;
	}

	/// <summary>Удаляет все дочерние объекты (перед перерисовкой содержимого).</summary>
	public static void ClearChildren(object parentTransform)
	{
		try
		{
			object transform = GameRefs.ReadMember(parentTransform, "transform") ?? parentTransform;
			int count = Convert.ToInt32(GameRefs.ReadMember(transform, "childCount") ?? 0);
			for (int i = count - 1; i >= 0; i--)
			{
				object child = GameRefs.Invoke(transform, "GetChild", i);
				object go = GameRefs.ReadMember(child, "gameObject");
				if (go != null)
				{
					// Сразу выключаем: Destroy сработает в конце кадра, а layout считается раньше.
					GameRefs.Invoke(go, "SetActive", false);
					Destroy(go);
				}
			}
		}
		catch (Exception)
		{
		}
	}

	// ---------------------------------------------------------------- элементы

	/// <summary>Строка текста на всю ширину страницы. Высота — по содержимому (перенос слов включён).</summary>
	public static object Text(object parent, string text, float size, float r, float g, float b, bool bold = false, string align = "MiddleCenter", float width = PageWidth)
	{
		object go = NewObject("rp_text", parent, "UnityEngine.UI.Text" + UiAsm);
		if (go == null)
		{
			return null;
		}
		SetSize(go, width, TextHeight(text, size, width));
		object t = GetComponent(go, "UnityEngine.UI.Text" + UiAsm);
		ApplyText(t, text, size, r, g, b, bold, align);
		return go;
	}

	/// <summary>Пустой промежуток заданной высоты.</summary>
	public static object Spacer(object parent, float height)
	{
		object go = NewObject("rp_spacer", parent, "UnityEngine.UI.LayoutElement" + UiAsm);
		if (go == null)
		{
			return null;
		}
		SetSize(go, PageWidth, height);
		object le = GetComponent(go, "UnityEngine.UI.LayoutElement" + UiAsm);
		SetProperty(le, "minHeight", height);
		SetProperty(le, "preferredHeight", height);
		return go;
	}

	/// <summary>
	/// 0.46: неподвижная панель заданного размера — холст, на котором элементы
	/// ставятся вручную по координатам (нужно для полукруга парламента).
	/// Высота учитывается вертикальной автораскладкой окна.
	/// </summary>
	public static object Panel(object parent, float width, float height)
	{
		object go = NewObject("rp_panel", parent, "UnityEngine.UI.Image" + UiAsm);
		if (go == null)
		{
			return null;
		}
		SetSize(go, width, height);
		object image = GetComponent(go, "UnityEngine.UI.Image" + UiAsm);
		// Прозрачная подложка: панель только держит координаты.
		SetProperty(image, "color", Color(0f, 0f, 0f, 0f));
		SetProperty(image, "raycastTarget", false);
		object le = GameRefs.Invoke(go, "AddComponent", Type.GetType("UnityEngine.UI.LayoutElement" + UiAsm));
		SetProperty(le, "minHeight", height);
		SetProperty(le, "preferredHeight", height);
		return go;
	}

	/// <summary>
	/// 0.46: цветной кружок внутри панели — место в парламенте.
	/// Координаты от центра панели. onClick — необязателен.
	/// </summary>
	public static object Dot(object parent, float x, float y, float size, float r, float g, float b, Action onClick)
	{
		object go = NewObject("rp_dot", parent, "UnityEngine.UI.Image" + UiAsm, "UnityEngine.UI.Button" + UiAsm);
		if (go == null)
		{
			return null;
		}
		object rect = GetComponent(go, "UnityEngine.RectTransform" + CoreAsm);
		SetProperty(rect, "anchorMin", Vector2(0.5f, 0.5f));
		SetProperty(rect, "anchorMax", Vector2(0.5f, 0.5f));
		SetProperty(rect, "pivot", Vector2(0.5f, 0.5f));
		SetProperty(rect, "anchoredPosition", Vector2(x, y));
		SetProperty(rect, "sizeDelta", Vector2(size, size));
		object image = GetComponent(go, "UnityEngine.UI.Image" + UiAsm);
		SetProperty(image, "color", Color(r, g, b, 1f));
		object button = GetComponent(go, "UnityEngine.UI.Button" + UiAsm);
		SetProperty(button, "targetGraphic", image);
		if (onClick != null)
		{
			HookClick(button, onClick);
		}
		return go;
	}

	/// <summary>Сетка из N колонок фиксированных ячеек (для рядов родословной).</summary>
	public static object Grid(object parent, int columns, float cellWidth, float cellHeight, int rows)
	{
		object go = NewObject("rp_grid", parent, "UnityEngine.UI.GridLayoutGroup" + UiAsm);
		if (go == null)
		{
			return null;
		}
		float spacing = 3f;
		SetSize(go, PageWidth, rows * cellHeight + Math.Max(0, rows - 1) * spacing);
		object grid = GetComponent(go, "UnityEngine.UI.GridLayoutGroup" + UiAsm);
		SetProperty(grid, "cellSize", Vector2(cellWidth, cellHeight));
		SetProperty(grid, "spacing", Vector2(spacing, spacing));
		SetProperty(grid, "childAlignment", Enum("UnityEngine.TextAnchor" + CoreAsm, "UpperCenter"));
		SetProperty(grid, "constraint", Enum("UnityEngine.UI.GridLayoutGroup+Constraint" + UiAsm, "FixedColumnCount"));
		SetProperty(grid, "constraintCount", columns);
		return go;
	}

	/// <summary>Кнопка с текстом: тёмная подложка + Text; клик вызывает onClick.</summary>
	public static object Button(object parent, string text, float width, float height, float size, float r, float g, float b, Action onClick)
	{
		return Button(parent, text, width, height, size, r, g, b, null, onClick);
	}

	/// <summary>Кнопка с текстом и маленькой иконкой слева (ключ файла из ModIcons).</summary>
	public static object Button(object parent, string text, float width, float height, float size, float r, float g, float b, string iconKey, Action onClick)
	{
		object go = NewObject("rp_button", parent, "UnityEngine.UI.Image" + UiAsm, "UnityEngine.UI.Button" + UiAsm);
		if (go == null)
		{
			return null;
		}
		bool hasIcon = !string.IsNullOrEmpty(iconKey);
		SetSize(go, width, height);
		// 0.47-ПРАВКА (баг со скриншота 536/537): кнопка «схлопывалась» в ноль по высоте
		// и все кнопки рисовались друг на друге. Причина: окна мода включают
		// VerticalLayoutGroup с childControlHeight = true, и высоту детей раскладка берёт
		// из LayoutElement. У Text он есть (высота считается по тексту), а у кнопки-картинки
		// его не было — Unity считала высоту нулевой. Теперь кнопка сама сообщает раскладке
		// свой размер, и они выстраиваются столбиком, как положено.
		object layout = GameRefs.Invoke(go, "AddComponent", Type.GetType("UnityEngine.UI.LayoutElement" + UiAsm));
		SetProperty(layout, "minHeight", height);
		SetProperty(layout, "preferredHeight", height);
		SetProperty(layout, "minWidth", width);
		SetProperty(layout, "preferredWidth", width);
		object image = GetComponent(go, "UnityEngine.UI.Image" + UiAsm);
		SetProperty(image, "color", Color(0.08f, 0.09f, 0.08f, 0.75f));
		object button = GetComponent(go, "UnityEngine.UI.Button" + UiAsm);
		SetProperty(button, "targetGraphic", image);
		object label = Text(go, text, size, r, g, b, bold: false, align: hasIcon ? "MiddleLeft" : "MiddleCenter", width: width - (hasIcon ? 34f : 4f));
		if (label != null)
		{
			// Подпись растягиваем на всю кнопку (кнопка — не layout-группа, поэтому вручную).
			// При наличии иконки оставляем слева 30 единиц под картинку и сдвигаем текст вправо.
			object rect = GetComponent(label, "UnityEngine.RectTransform" + CoreAsm);
			SetProperty(rect, "anchorMin", Vector2(0f, 0f));
			SetProperty(rect, "anchorMax", Vector2(1f, 1f));
			SetProperty(rect, "pivot", Vector2(0.5f, 0.5f));
			SetProperty(rect, "anchoredPosition", Vector2(hasIcon ? 13f : 0f, 0f));
			SetProperty(rect, "sizeDelta", Vector2(hasIcon ? -34f : -4f, -2f));
			// Клик должен доходить до кнопки, а не «съедаться» текстом.
			SetProperty(GetComponent(label, "UnityEngine.UI.Text" + UiAsm), "raycastTarget", false);
		}
		if (hasIcon)
		{
			AddIcon(go, iconKey, -width / 2f + 15f, 0f, Math.Min(22f, height - 4f));
		}
		HookClick(button, onClick);
		return go;
	}

	/// <summary>Добавляет некликабельную иконку мода внутрь кнопки или другого UI-объекта.</summary>
	public static object AddIcon(object parent, string iconKey, float x, float y, float size)
	{
		if (parent == null || string.IsNullOrEmpty(iconKey) || size <= 0f)
		{
			return null;
		}
		object go = NewObject("rp_icon_" + iconKey, parent, "UnityEngine.UI.Image" + UiAsm);
		if (go == null)
		{
			return null;
		}
		object image = GetComponent(go, "UnityEngine.UI.Image" + UiAsm);
		// До успешной загрузки спрайта картинка полностью прозрачна: белый квадрат вместо иконки не показываем.
		SetProperty(image, "color", Color(0f, 0f, 0f, 0f));
		SetProperty(image, "raycastTarget", false);
		SetProperty(image, "preserveAspect", true);
		Place(go, x, y, size, size);
		object sprite = ModIcons.GetSprite(iconKey);
		if (sprite != null)
		{
			SetProperty(image, "sprite", sprite);
			SetProperty(image, "color", Color(1f, 1f, 1f, 1f));
		}
		return go;
	}

	/// <summary>Ванильный портрет юнита (префаб `ui/UnitAvatarElement` — тот же, что в разделе «Родословная»
	/// окна юнита: рамка, анимированный спрайт, значки королевства/клана). Кладётся внутрь parent
	/// по локальным координатам (x, y) относительно центра родителя. Возвращает null, если префаб не найден
	/// (тогда вызывающий рисует текстовую плашку).</summary>
	public static object UnitAvatar(object parent, object actor, float scale, float x, float y)
	{
		try
		{
			if (actor == null)
			{
				return null;
			}
			Type elementType = Patches.FindGameType("UiUnitAvatarElement");
			Type resources = Type.GetType("UnityEngine.Resources" + CoreAsm);
			MethodInfo load = resources?.GetMethod("Load", new Type[] { typeof(string), typeof(Type) });
			if (elementType == null || load == null)
			{
				return null;
			}
			if (!IsAlive(_avatarPrefab))
			{
				_avatarPrefab = load.Invoke(null, new object[] { "ui/UnitAvatarElement", elementType });
			}
			if (!IsAlive(_avatarPrefab))
			{
				return null;
			}
			Type unityObject = Type.GetType("UnityEngine.Object" + CoreAsm);
			Type transformType = Type.GetType("UnityEngine.Transform" + CoreAsm);
			MethodInfo instantiate = unityObject?.GetMethod("Instantiate", new Type[] { unityObject, transformType });
			object parentTransform = GameRefs.ReadMember(parent, "transform") ?? parent;
			object element = instantiate?.Invoke(null, new object[] { _avatarPrefab, parentTransform });
			if (!IsAlive(element))
			{
				return null;
			}
			object go = GameRefs.ReadMember(element, "gameObject");
			GameRefs.Invoke(go, "SetActive", true);
			GameRefs.Invoke(element, "show", actor);
			object rect = GetComponent(go, "UnityEngine.RectTransform" + CoreAsm);
			SetProperty(rect, "anchorMin", Vector2(0.5f, 0.5f));
			SetProperty(rect, "anchorMax", Vector2(0.5f, 0.5f));
			SetProperty(rect, "pivot", Vector2(0.5f, 0.5f));
			SetProperty(rect, "anchoredPosition", Vector2(x, y));
			SetProperty(rect, "localScale", Vector3(scale, scale, 1f));
			// Клик по портрету в ванили открывает окно юнита и закрывает наше; отключаем его кнопку —
			// тогда клик «проваливается» к родительской плашке дерева (подсказка при наведении остаётся).
			object button = GetComponent(go, "UnityEngine.UI.Button" + UiAsm);
			if (button != null)
			{
				SetProperty(button, "enabled", false);
			}
			return go;
		}
		catch (Exception ex)
		{
			LogService.LogError("[Real Politic] портрет юнита не создан: " + ex.Message);
			return null;
		}
	}

	private static object _avatarPrefab;

	/// <summary>Пустая кликабельная плашка заданного размера (без подписи) — основа узла древа.</summary>
	public static object Tile(object parent, float width, float height, float r, float g, float b, float a, Action onClick)
	{
		object go = NewObject("rp_tile", parent, "UnityEngine.UI.Image" + UiAsm, "UnityEngine.UI.Button" + UiAsm);
		if (go == null)
		{
			return null;
		}
		SetSize(go, width, height);
		object image = GetComponent(go, "UnityEngine.UI.Image" + UiAsm);
		SetProperty(image, "color", Color(r, g, b, a));
		object button = GetComponent(go, "UnityEngine.UI.Button" + UiAsm);
		SetProperty(button, "targetGraphic", image);
		if (onClick != null)
		{
			HookClick(button, onClick);
		}
		else
		{
			SetProperty(button, "enabled", false);
		}
		return go;
	}

	/// <summary>Ставит дочерний элемент в точку (x, y) относительно центра родителя (якорь и pivot — центр).</summary>
	public static void Place(object go, float x, float y, float width, float height)
	{
		object rect = GetComponent(go, "UnityEngine.RectTransform" + CoreAsm);
		SetProperty(rect, "anchorMin", Vector2(0.5f, 0.5f));
		SetProperty(rect, "anchorMax", Vector2(0.5f, 0.5f));
		SetProperty(rect, "pivot", Vector2(0.5f, 0.5f));
		SetProperty(rect, "anchoredPosition", Vector2(x, y));
		SetProperty(rect, "sizeDelta", Vector2(width, height));
	}

	/// <summary>Тонкая цветная полоска (соединительная линия древа).</summary>
	public static object Line(object parent, float x, float y, float width, float height, float r, float g, float b, float a = 0.9f)
	{
		object go = NewObject("rp_line", parent, "UnityEngine.UI.Image" + UiAsm);
		if (go == null)
		{
			return null;
		}
		object image = GetComponent(go, "UnityEngine.UI.Image" + UiAsm);
		SetProperty(image, "color", Color(r, g, b, a));
		SetProperty(image, "raycastTarget", false);
		Place(go, x, y, width, height);
		return go;
	}

	/// <summary>Свободный холст фиксированной высоты внутри колонки (дети расставляются вручную через Place).</summary>
	public static object Canvas(object parent, float height, float width = PageWidth)
	{
		object go = NewObject("rp_canvas", parent, "UnityEngine.UI.LayoutElement" + UiAsm);
		if (go == null)
		{
			return null;
		}
		SetSize(go, width, height);
		object le = GetComponent(go, "UnityEngine.UI.LayoutElement" + UiAsm);
		SetProperty(le, "minHeight", height);
		SetProperty(le, "preferredHeight", height);
		return go;
	}

	// ---------------------------------------------------------------- шрифт и текст

	/// <summary>Текущий шрифт игры (`LocalizedTextManager.current_font`). Без шрифта Unity не рисует Text.</summary>
	public static object Font()
	{
		if (IsAlive(_font))
		{
			return _font;
		}
		try
		{
			Type ltm = Patches.FindGameType("LocalizedTextManager");
			_font = ltm == null ? null : GameRefs.ReadStatic(ltm, "current_font");
			if (!IsAlive(_font))
			{
				object instance = ltm == null ? null : GameRefs.ReadStatic(ltm, "instance");
				_font = GameRefs.ReadMember(instance, "default_font");
			}
			if (!IsAlive(_font))
			{
				// Последний шанс — встроенный Arial Unity.
				Type fontType = Type.GetType("UnityEngine.Font, UnityEngine.TextRenderingModule");
				MethodInfo builtin = fontType?.GetMethod("CreateDynamicFontFromOSFont", new Type[] { typeof(string), typeof(int) });
				_font = builtin?.Invoke(null, new object[] { "Arial", 12 });
			}
		}
		catch (Exception)
		{
		}
		return _font;
	}

	private static void ApplyText(object t, string text, float size, float r, float g, float b, bool bold, string align)
	{
		if (t == null)
		{
			return;
		}
		object font = Font();
		if (font != null)
		{
			SetProperty(t, "font", font);
		}
		SetProperty(t, "text", text ?? string.Empty);
		SetProperty(t, "fontSize", (int)Math.Round(size));
		SetProperty(t, "alignment", Enum("UnityEngine.TextAnchor" + CoreAsm, align));
		SetProperty(t, "horizontalOverflow", Enum("UnityEngine.HorizontalWrapMode" + CoreAsm, "Wrap"));
		SetProperty(t, "verticalOverflow", Enum("UnityEngine.VerticalWrapMode" + CoreAsm, "Overflow"));
		SetProperty(t, "supportRichText", true);
		if (bold)
		{
			SetProperty(t, "fontStyle", Enum("UnityEngine.FontStyle" + CoreAsm, "Bold"));
		}
		SetProperty(t, "color", Color(r, g, b, 1f));
	}

	// ---------------------------------------------------------------- Unity-примитивы (рефлексия)

	private static object NewObject(string name, object parent, params string[] componentTypes)
	{
		try
		{
			Type goType = Type.GetType("UnityEngine.GameObject" + CoreAsm);
			if (goType == null)
			{
				return null;
			}
			object go = Activator.CreateInstance(goType, new object[1] { name });
			GameRefs.Invoke(go, "AddComponent", Type.GetType("UnityEngine.RectTransform" + CoreAsm));
			for (int i = 0; i < componentTypes.Length; i++)
			{
				Type ct = Type.GetType(componentTypes[i]);
				if (ct != null)
				{
					GameRefs.Invoke(go, "AddComponent", ct);
				}
			}
			object transform = GameRefs.ReadMember(go, "transform");
			object parentTransform = GameRefs.ReadMember(parent, "transform") ?? parent;
			if (transform != null && parentTransform != null)
			{
				Type transformType = Type.GetType("UnityEngine.Transform" + CoreAsm);
				// SetParent(parent, worldPositionStays: false) — иначе клон «улетает» по мировым координатам.
				MethodInfo setParent = transformType?.GetMethod("SetParent", BindingFlags.Instance | BindingFlags.Public, null,
					new Type[2] { transformType, typeof(bool) }, null);
				setParent?.Invoke(transform, new object[2] { parentTransform, false });
			}
			SetProperty(transform, "localScale", Vector3(1f, 1f, 1f));
			return go;
		}
		catch (Exception ex)
		{
			LogService.LogError("[Real Politic] не удалось создать UI-объект " + name + ": " + ex.Message);
			return null;
		}
	}

	private static void SetSize(object go, float width, float height)
	{
		object rect = GetComponent(go, "UnityEngine.RectTransform" + CoreAsm);
		SetProperty(rect, "sizeDelta", Vector2(width, height));
	}

	public static object GetComponent(object go, string typeName)
	{
		Type type = Type.GetType(typeName);
		if (type == null || go == null)
		{
			return null;
		}
		object target = GameRefs.ReadMember(go, "gameObject") ?? go;
		return GameRefs.Invoke(target, "GetComponent", type);
	}

	private static void HookClick(object button, Action onClick)
	{
		try
		{
			if (button == null || onClick == null)
			{
				return;
			}
			object onClickEvent = GameRefs.ReadMember(button, "onClick");
			Type actionType = Type.GetType("UnityEngine.Events.UnityAction" + CoreAsm);
			if (onClickEvent == null || actionType == null)
			{
				return;
			}
			var handler = new ClickHandler { Action = onClick };
			Delegate del = Delegate.CreateDelegate(actionType, handler, nameof(ClickHandler.Run));
			MethodInfo addListener = onClickEvent.GetType().GetMethod("AddListener", BindingFlags.Instance | BindingFlags.Public);
			addListener?.Invoke(onClickEvent, new object[] { del });
		}
		catch (Exception ex)
		{
			LogService.LogError("[Real Politic] не удалось повесить клик: " + ex.Message);
		}
	}

	private sealed class ClickHandler
	{
		public Action Action;

		public void Run()
		{
			try
			{
				Action?.Invoke();
			}
			catch (Exception ex)
			{
				LogService.LogError("[Real Politic] ошибка обработчика клика: " + ex.Message);
			}
		}
	}

	public static void SetProperty(object target, string name, object value)
	{
		if (target == null || value == null)
		{
			return;
		}
		try
		{
			PropertyInfo prop = target.GetType().GetProperty(name, BindingFlags.Instance | BindingFlags.Public);
			if (prop != null && prop.CanWrite)
			{
				if (value.GetType() != prop.PropertyType && !prop.PropertyType.IsInstanceOfType(value))
				{
					value = Convert.ChangeType(value, prop.PropertyType);
				}
				prop.SetValue(target, value, null);
			}
		}
		catch (Exception)
		{
		}
	}

	private static object Enum(string typeName, string name)
	{
		Type t = Type.GetType(typeName);
		return t == null ? null : System.Enum.Parse(t, name);
	}

	private static object Vector2(float x, float y)
	{
		Type t = Type.GetType("UnityEngine.Vector2" + CoreAsm);
		return t == null ? null : Activator.CreateInstance(t, new object[2] { x, y });
	}

	private static object Vector3(float x, float y, float z)
	{
		Type t = Type.GetType("UnityEngine.Vector3" + CoreAsm);
		return t == null ? null : Activator.CreateInstance(t, new object[3] { x, y, z });
	}

	private static object Color(float r, float g, float b, float a)
	{
		Type t = Type.GetType("UnityEngine.Color" + CoreAsm);
		return t == null ? null : Activator.CreateInstance(t, new object[4] { r, g, b, a });
	}

	private static object RectOffset(int left, int right, int top, int bottom)
	{
		Type t = Type.GetType("UnityEngine.RectOffset" + CoreAsm);
		return t == null ? null : Activator.CreateInstance(t, new object[4] { left, right, top, bottom });
	}

	private static void Destroy(object go)
	{
		Type unityObject = Type.GetType("UnityEngine.Object" + CoreAsm);
		MethodInfo destroy = unityObject?.GetMethod("Destroy", BindingFlags.Static | BindingFlags.Public, null, new Type[1] { unityObject }, null);
		destroy?.Invoke(null, new object[1] { go });
	}

	/// <summary>Unity-объект «жив» (не null и не уничтожен — у Unity свой перегруженный ==).</summary>
	public static bool IsAlive(object unityObj)
	{
		try
		{
			if (unityObj == null)
			{
				return false;
			}
			Type unityObject = Type.GetType("UnityEngine.Object" + CoreAsm);
			MethodInfo op = unityObject?.GetMethod("op_Equality", BindingFlags.Static | BindingFlags.Public);
			if (op == null)
			{
				return true;
			}
			return op.Invoke(null, new object[2] { null, unityObj }) is bool isNull && !isNull;
		}
		catch (Exception)
		{
			return true;
		}
	}
}
