using System;
using System.Collections.Generic;
using System.Reflection;
using NeoModLoader.services;
using RealPolitic.Core;

namespace RealPolitic;

/// <summary>
/// Этап 25c: интерактивное окно «Родословная» — настоящее древо.
/// Ярусы поколений сверху вниз (прадеды → деды → родители → сам человек с братьями/сёстрами → дети);
/// умершие помечены «†». 0.23.6: раскладка через ModUi (VerticalLayoutGroup + сетка кнопок) —
/// раньше узлы рисовались по свободным координатам без шрифта и окно оставалось пустым.
/// Клик по любому человеку перестраивает дерево ОТ ЕГО ЛИЦА — так уходишь
/// по династии на любую глубину, не теряя покойников из поколения в поколение.
///
/// Окно собирается строго по рефлексии с мягкой деградацией: если в новой
/// сборке игры переименовали ScrollWindow — мод оставляет текстовый блок
/// родословной в профиле (этой фиче не нужны API, кроме уже налаженных).
/// </summary>
public static class FamilyTreeWindow
{
	private const string WindowId = "realpolitic_familytree";

	private static object _window;

	private static object _content;

	private static long _focusId;

	private static bool _disabled;

	/// <summary>Имя фокусного жителя — в заголовок окна.</summary>
	private static string _focusName = string.Empty;

	// ---------------------------------------------------------------- публичный API

	/// <summary>Заранее создаёт окно родословной на старте, чтобы первый клик не сбивал затемнение фона.</summary>
	public static bool Prewarm()
	{
		if (_disabled)
		{
			return false;
		}
		if (_window != null && IsAlive(_window))
		{
			return true;
		}
		_window = ModWindow.Create(WindowId, "RP_FAMILY_WIN_TITLE", GameText.Localized("RP_FAMILY_WIN_TITLE", "Родословная"), ModIcons.FamilyKey, out object content);
		if (_window == null)
		{
			return false;
		}
		_content = content ?? _window;
		ModUi.SetupAutoLayout(_content, spacing: 2f, padTop: 6f);
		ModWindow.SetTitle(_window, Title());
		return true;
	}

	/// <summary>Открыть/перестроить дерево от лица данного жителя.</summary>
	public static void Toggle(long focusActorId)
	{
		try
		{
			if (focusActorId == 0 || _disabled)
			{
				return;
			}
			if (RealPoliticMod.World == null
				|| !RealPoliticMod.World.Family.TryGet(focusActorId, out FamilyRecord focus))
			{
				return;
			}
			_focusId = focusActorId;
			_focusName = focus.Name;
			if (!EnsureWindow())
			{
				return;
			}
			Rebuild();
			ShowWindow();
		}
		catch (Exception ex)
		{
			LogOnce("ошибка окна родословной: " + ex.Message);
		}
	}

	/// <summary>Клик из окна юнита: открыть дерево текущего показанного жителя.</summary>
	public static void ToggleForCurrent()
	{
		long id = Patches.CurrentUnitWindowActorId();
		if (id != 0)
		{
			Toggle(id);
		}
	}

	// ---------------------------------------------------------------- окно

	private static bool EnsureWindow()
	{
		try
		{
			if (_window != null && IsAlive(_window))
			{
				ModWindow.SetTitle(_window, Title());
				return true;
			}
			// 0.23.5: штатный для NML способ — WindowCreator.CreateEmptyWindow.
			_window = ModWindow.Create(WindowId, "RP_FAMILY_WIN_TITLE", GameText.Localized("RP_FAMILY_WIN_TITLE", "Родословная"), ModIcons.FamilyKey, out object content);
			if (_window == null)
			{
				Disable("не удалось создать окно (см. лог выше)");
				return false;
			}
			_content = content ?? _window;
			// 0.23.6: содержимое раскладывается автоматически (ярусы поколений столбиком).
			ModUi.SetupAutoLayout(_content, spacing: 2f, padTop: 6f);
			ModWindow.SetTitle(_window, Title());
			return true;
		}
		catch (Exception ex)
		{
			Disable("не удалось создать окно: " + ex.Message);
			return false;
		}
	}

	private static void ShowWindow()
	{
		ModWindow.Show(WindowId, _window);
	}

	private static string Title()
	{
		string title = GameText.Localized("RP_FAMILY_WIN_TITLE", "Родословная");
		return string.IsNullOrEmpty(_focusName) ? title : title + " — " + _focusName;
	}

	// ---------------------------------------------------------------- содержимое

	/// <summary>Перерисовать дерево. 0.23.9/0.23.10: настоящее веерное древо без рамок —
	/// волосяные линии идут прямо к портретам, портреты без «коробочек», имя под портретом.
	/// Клик по человеку перестраивает дерево от его лица.</summary>
	private const float CellW = 50f;     // ширина портрета (из префаба юнита)
	private const float NodeH = 70f;     // портрет + отступ + имя (габарит узла, центр = центр портрета)
	private const float NameH = 20f;     // высота подписи с именем (внизу узла)
	private const float TitleH = 14f;    // подпись поколения (сверху)
	private const float PerTopGap = 10f; // отступ перед самой верхней подписью
	private const float AvatarScale = 0.68f;

	// Пределы вписывания веера в страницу.
	private const float MinUnitPx = 6f;      // минимальный сжатый шаг веера (px на единицу X)
	private const float MaxUnitPx = 30f;     // максимальный «воздушный» шаг
	private const float WindowUnitPx = 26f;  // желаемая ширина окна на единицу размаха веера
	private const float WindowMinW = 232f;   // ширина пустого окна NML
	private const float WindowMaxW = 560f;   // предел расширения (не перекроем кнопки)
	private const float WindowMinH = 260f;
	private const float WindowMaxH = 820f;

	private static float Clamp(float v, float lo, float hi) => v < lo ? lo : (v > hi ? hi : v);

	private static void Rebuild()
	{
		object contentTransform = GameRefs.ReadMember(_content, "transform") ?? _content;
		ModUi.ClearChildren(contentTransform);
		(List<ChartNode> nodes, List<ChartEdge> edges) = GenealogyChart.Build(RealPoliticMod.World.Family, _focusId);
		if (nodes == null || nodes.Count == 0)
		{
			ModUi.Text(contentTransform, GameText.Localized("RP_FAMILY_EMPTY", "Сведений о родне нет."), 11f, 0.75f, 0.72f, 0.66f);
			return;
		}

		// Горизонталь = настоящая координата X из GenealogyChart (веер), вертикаль = поколение.
		const float unitsPerGen = 108f; // высота одного поколения (с запасом на линии)
		const float titleH = 14f;

		float rawMin = float.MaxValue;
		float rawMax = float.MinValue;
		for (int i = 0; i < nodes.Count; i++)
		{
			if (nodes[i].X < rawMin) rawMin = nodes[i].X;
			if (nodes[i].X > rawMax) rawMax = nodes[i].X;
		}
		float rawSpan = Math.Max(0.001f, rawMax - rawMin);
		float targetW = Clamp(rawSpan * WindowUnitPx + 150f, WindowMinW, WindowMaxW);

		int minGen = 0, maxGen = 0;
		for (int i = 0; i < nodes.Count; i++)
		{
			if (nodes[i].Gen < minGen) minGen = nodes[i].Gen;
			if (nodes[i].Gen > maxGen) maxGen = nodes[i].Gen;
		}

		// Подписи поколений — по ярусам, где есть хоть один узел.
		var genOrder = new List<int>();
		for (int gen = minGen; gen <= maxGen; gen++)
		{
			for (int i = 0; i < nodes.Count; i++)
			{
				if (nodes[i].Gen == gen)
				{
					genOrder.Add(gen);
					break;
				}
			}
		}
		int maxDepth = maxGen - minGen;
		// Мягкая склейка, когда веер узок: не раздуваем холст по вертикали.
		float span = maxDepth * (unitsPerGen - 18f) + NodeH + titleH + PerTopGap;
		float total = span + PerTopGap;
		float y0(int gen) => titleH + PerTopGap + NodeH / 2f + (gen - minGen) * (unitsPerGen - 18f);

		TryResizeWindow(targetW, Clamp(total + 70f, WindowMinH, WindowMaxH));
		float contentW = Math.Max(ModUi.PageWidth, targetW - 16f);
		float unitsPerX = Clamp((contentW - 100f) / rawSpan, MinUnitPx, MaxUnitPx);

		CenterContentForCanvas(contentTransform, contentW);
		object canvas = ModUi.Canvas(contentTransform, total, contentW);
		if (canvas == null) return;
		float top = total / 2f;

		// 1) Линии ДО портретов (под ними): горизонтальный коридор между супругами + вертикальные
		//    «ноги» к детям. Тонкие, волосяные (1px), тёплого воска с умеренной прозрачностью.
		const float lr = 0.82f, lg = 0.74f, lb = 0.60f, la = 0.55f;
		if (edges != null)
		{
			for (int e = 0; e < edges.Count; e++)
			{
				ChartNode fromNode = nodes[edges[e].FromNode];
				ChartNode toNode = nodes[edges[e].ToNode];
				if (Math.Abs(fromNode.Gen - toNode.Gen) != 1) continue; // только соседние поколения
				float fx = fromNode.X * unitsPerX;
				float tx = toNode.X * unitsPerX;
				float fy = y0(fromNode.Gen);
				float ty = y0(toNode.Gen);
				float parentX = fromNode.Gen < toNode.Gen ? fx : tx;
				float parentY = fromNode.Gen < toNode.Gen ? fy : ty;
				float childX = fromNode.Gen < toNode.Gen ? tx : fx;
				float childY = fromNode.Gen < toNode.Gen ? ty : fy;
				float arm = (parentY + childY) / 2f;
				ModUi.Line(canvas, parentX, (parentY + arm) / 2f, 1f, Math.Abs(parentY - arm) + 1f, lr, lg, lb, la);
				ModUi.Line(canvas, (parentX + childX) / 2f, arm, Math.Abs(parentX - childX) + 1f, 1f, lr, lg, lb, la);
				ModUi.Line(canvas, childX, (arm + childY) / 2f, 1f, Math.Abs(arm - childY) + 1f, lr, lg, lb, la);
			}
		}

		// 2) Подписи поколений — приглушённым серым над каждым ярусом.
		for (int g = 0; g < genOrder.Count; g++)
		{
			object title = ModUi.Text(canvas, GenerationTitle(genOrder[g]), 9f, 0.5f, 0.5f, 0.5f, bold: true);
			if (title != null)
			{
				ModUi.Place(title, 0f, top - (y0(genOrder[g]) - NodeH / 2f - 8f), ModUi.PageWidth, TitleH);
			}
		}

		// 3) Узлы — портреты без рамок.
		for (int i = 0; i < nodes.Count; i++)
		{
			CreateNode(canvas, nodes[i], nodes[i].X * unitsPerX, top - y0(nodes[i].Gen));
		}

		ModUi.Spacer(contentTransform, 4f);
		ModUi.Text(contentTransform, GameText.Localized("RP_FAMILY_HINT", "Нажмите на человека, чтобы посмотреть дерево от его лица."), 8f, 0.6f, 0.6f, 0.6f);
	}

	/// <summary>Узел древа — ванильный портрет юнита БЕЗ рамки-подложки, имя под ним.
	/// Клик по портрету/имени = дерево от лица этого человека; фокус подчёркнут золотом.
	/// Умершие — один тихий знак «†» без портрета.</summary>
	private static void CreateNode(object canvas, ChartNode node, float x, float y)
	{
		if (node.Id < 0)
		{
			object more = ModUi.Text(canvas, node.Label, 9f, 1f, 0.85f, 0.4f);
			if (more != null) ModUi.Place(more, x, y, CellW - 2f, NodeH);
			return;
		}
		long id = node.Id;
		// Кликабельна вся область узла (портрет + имя): прозрачная кнопка под контентом.
		object hit = ModUi.Tile(canvas, CellW - 4f, NodeH - 4f, 0f, 0f, 0f, 0f, () => Toggle(id));
		if (hit == null) return;
		ModUi.Place(hit, x, y, CellW - 4f, NodeH - 4f);

		object avatar = null;
		if (!node.Dead)
		{
			Actor actor = GameRefs.FindActorById(id);
			if (actor != null)
			{
				// Портрет прямо на своём месте — линии ведут к нему, а не к «рамке» вокруг.
				avatar = ModUi.UnitAvatar(canvas, actor, AvatarScale, x, y + 7f);
			}
		}
		if (avatar == null)
		{
			object mark = ModUi.Text(canvas, node.Dead ? "†" : "?", 23f, 0.72f, 0.62f, 0.64f, bold: true);
			if (mark != null) ModUi.Place(mark, x, y + 7f, CellW - 6f, 30f);
		}

		// Имя под портретом (не кликается и не загораживает клик по человеку).
		float r = 0.93f, g = 0.93f, b = 0.93f;
		if (node.Dead) { r = 0.75f; g = 0.62f; b = 0.66f; }
		object label = ModUi.Text(canvas, node.Label, 7f, r, g, b);
		if (label != null)
		{
			ModUi.Place(label, x, y - NodeH / 2f + 9f, CellW + 26f, NameH);
			ModUi.SetProperty(ModUi.GetComponent(label, "UnityEngine.UI.Text, UnityEngine.UI"), "raycastTarget", false);
		}

		// Фокус — тонкая золотая черта под именем (вместо громоздкой цветной плашки).
		if (node.IsFocus)
		{
			ModUi.Line(canvas, x, y - NodeH / 2f + 3f, CellW, 1f, 1f, 0.85f, 0.4f);
		}
	}

	/// <summary>Мягко расширяет рамку окна древа (Scroll View и его RectTransform), чтобы веер
	/// помещался. Всё через рефлексию: если поля другой версии игры отсутствуют — окно
	/// остаётся прежней ширины, а дерево впишется дополнительным сжатием.</summary>
	private static void TryResizeWindow(float width, float height)
	{
		try
		{
			if (width <= 0f || height <= 0f || !IsAlive(_window))
			{
				return;
			}
			object transform = GameRefs.ReadMember(_window, "transform");
			object background = transform == null ? null : GameRefs.Invoke(transform, "Find", "Background");
			object scroll = background == null ? null : GameRefs.Invoke(background, "Find", "Scroll View");
			object go = scroll == null ? null : GameRefs.ReadMember(scroll, "gameObject");
			if (go == null)
			{
				return;
			}
			Type rtType = Type.GetType("UnityEngine.RectTransform, UnityEngine.CoreModule");
			Type v2t = Type.GetType("UnityEngine.Vector2, UnityEngine.CoreModule");
			if (rtType == null || v2t == null)
			{
				return;
			}
			object rect = GameRefs.Invoke(go, "GetComponent", rtType);
			if (rect == null)
			{
				return;
			}
			object cur = GameRefs.ReadMember(rect, "sizeDelta");
			float curW = cur == null ? 0f : Convert.ToSingle(GameRefs.ReadMember(cur, "x"));
			float curH = cur == null ? 0f : Convert.ToSingle(GameRefs.ReadMember(cur, "y"));
			float w = Math.Max(curW, width);
			float h = Math.Max(curH, height);
			ModUi.SetProperty(rect, "sizeDelta", Activator.CreateInstance(v2t, new object[2] { w, h }));
		}
		catch (Exception)
		{
			// Расширение — удобство: не вышло — дерево просто сожмётся.
		}
	}

	/// <summary>Центрирует Content шириной width по реальному центру рамки «Background/Scroll View».
	/// Якорь/pivot — левый-верхний угол (как в автораскладке), левая координата — через ModUi.</summary>
	private static void CenterContentForCanvas(object contentTransform, float width)
	{
		try
		{
			float x = ModUi.CenteredContentX(
				GameRefs.ReadMember(contentTransform, "gameObject") ?? contentTransform, width);
			object rect = GameRefs.Invoke(
				GameRefs.ReadMember(contentTransform, "gameObject") ?? contentTransform,
				"GetComponent", Type.GetType("UnityEngine.RectTransform, UnityEngine.CoreModule"));
			if (rect == null)
			{
				return;
			}
			Type rtType = Type.GetType("UnityEngine.RectTransform, UnityEngine.CoreModule");
			Type v2t = Type.GetType("UnityEngine.Vector2, UnityEngine.CoreModule");
			if (rtType == null || v2t == null)
			{
				return;
			}
			ModUi.SetProperty(rect, "anchorMin", Activator.CreateInstance(v2t, new object[2] { 0f, 1f }));
			ModUi.SetProperty(rect, "anchorMax", Activator.CreateInstance(v2t, new object[2] { 0f, 1f }));
			ModUi.SetProperty(rect, "pivot", Activator.CreateInstance(v2t, new object[2] { 0f, 1f }));
			ModUi.SetProperty(rect, "sizeDelta", Activator.CreateInstance(v2t, new object[2] { width, 10f }));
			ModUi.SetProperty(rect, "anchoredPosition", Activator.CreateInstance(v2t, new object[2] { x, 0f }));
		}
		catch (Exception)
		{
			// Без центровки дерево всё равно рисуется — просто от левого края.
		}
	}

	/// <summary>Подпись яруса: −2 «Деды и бабки», −1 «Родители», 0 «Братья и сёстры», 1 «Дети».</summary>
	private static string GenerationTitle(int gen)
	{
		switch (gen)
		{
			case 1: return GameText.Localized("RP_FAMILY_GEN_CHILDREN", "Дети");
			case 0: return GameText.Localized("RP_FAMILY_GEN_FOCUS", "Он сам, братья и сёстры");
			case -1: return GameText.Localized("RP_FAMILY_GEN_PARENTS", "Родители");
			case -2: return GameText.Localized("RP_FAMILY_GEN_GRANDPARENTS", "Деды и бабки");
			case -3: return GameText.Localized("RP_FAMILY_GEN_GREAT", "Прадеды и прабабки");
			default:
				return gen < 0
					? GameText.Localized("RP_FAMILY_GEN_ANCESTORS", "Предки ({n}-е колено)").Replace("{n}", (-gen).ToString())
					: GameText.Localized("RP_FAMILY_GEN_DESCENDANTS", "Потомки ({n}-е колено)").Replace("{n}", gen.ToString());
		}
	}

	// ---------------------------------------------------------------- Unity-примитивы (только рефлексия)

	/// <summary>UnityEngine.Object.Instantiate(original, parent) — клон так же, как в кнопке слоя идеологий.</summary>
	private static object CloneUnder(object original, object parent)
	{
		try
		{
			Type unityObject = Type.GetType("UnityEngine.Object, UnityEngine.CoreModule");
			if (unityObject == null)
			{
				return null;
			}
			MethodInfo[] methods = unityObject.GetMethods(BindingFlags.Static | BindingFlags.Public);
			for (int i = 0; i < methods.Length; i++)
			{
				if (methods[i].Name != "Instantiate" || methods[i].IsGenericMethod)
				{
					continue;
				}
				ParameterInfo[] ps = methods[i].GetParameters();
				if (ps.Length == 2 && ps[1].ParameterType.Name == "Transform")
				{
					object parentTransform = GameRefs.ReadMember(parent, "transform") ?? parent;
					return methods[i].Invoke(null, new object[2] { original, parentTransform });
				}
			}
		}
		catch (Exception ex)
		{
			LogOnce("Instantiate провалился: " + ex.Message);
		}
		return null;
	}

	private static void SetProperty(object target, string name, object value)
	{
		if (target == null || value == null)
		{
			return;
		}
		PropertyInfo prop = target.GetType().GetProperty(name, BindingFlags.Instance | BindingFlags.Public);
		if (prop == null || !prop.CanWrite)
		{
			return;
		}
		try
		{
			if (value.GetType() != prop.PropertyType && !prop.PropertyType.IsInstanceOfType(value))
			{
				value = Convert.ChangeType(value, prop.PropertyType);
			}
			prop.SetValue(target, value, null);
		}
		catch (Exception)
		{
		}
	}

	private static object EnumValue(string typeName, string name)
	{
		Type t = Type.GetType(typeName);
		return t == null ? null : Enum.Parse(t, name);
	}

	private static void SetColor(object textComp, float r, float g, float b, float a)
	{
		Type t = Type.GetType("UnityEngine.Color, UnityEngine.CoreModule");
		if (t == null)
		{
			return;
		}
		object color = Activator.CreateInstance(t, new object[4] { r, g, b, a });
		SetProperty(textComp, "color", color);
	}

	private static object GetComponent(object go, string typeName)
	{
		Type type = Type.GetType(typeName);
		if (type == null || go == null)
		{
			return null;
		}
		MethodInfo getComponent = go.GetType().GetMethod("GetComponent", BindingFlags.Instance | BindingFlags.Public, null, new Type[1] { typeof(Type) }, null);
		return getComponent?.Invoke(go, new object[1] { type });
	}

	private static void SetName(object go, string name)
	{
		SetProperty(go, "name", name);
	}

	private static void DestroyComponent(object go, string componentName)
	{
		try
		{
			Type type = Patches.FindGameType(componentName);
			object component = GetComponent(go, type?.AssemblyQualifiedName ?? componentName);
			if (component == null)
			{
				return;
			}
			Type unityObject = Type.GetType("UnityEngine.Object, UnityEngine.CoreModule");
			MethodInfo destroy = unityObject?.GetMethod("DestroyImmediate", BindingFlags.Static | BindingFlags.Public, null, new Type[1] { unityObject }, null);
			destroy?.Invoke(null, new object[1] { component });
		}
		catch (Exception)
		{
		}
	}

	private static bool IsAlive(object go)
	{
		try
		{
			if (go == null)
			{
				return false;
			}
			// «Псевдо-смерть» Unity-объектов: уничтоженный объект равен null через перегруженный ==.
			Type unityObject = Type.GetType("UnityEngine.Object, UnityEngine.CoreModule");
			if (unityObject == null)
			{
				return true;
			}
			MethodInfo op = unityObject.GetMethod("op_Equality", BindingFlags.Static | BindingFlags.Public);
			if (op == null)
			{
				return true;
			}
			object result = op.Invoke(null, new object[2] { null, go });
			return result is bool b && !b;
		}
		catch (Exception)
		{
			return true;
		}
	}

	private static MethodInfo FindStatic(Type type, string name, int argCount)
	{
		if (type == null)
		{
			return null;
		}
		MethodInfo[] methods = type.GetMethods(BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
		for (int i = 0; i < methods.Length; i++)
		{
			if (methods[i].Name == name && methods[i].GetParameters().Length == argCount)
			{
				return methods[i];
			}
		}
		return null;
	}

	private static void SetButtonText(object buttonGo, string text, bool dead, bool focus)
	{
		IEnumerable<object> texts = GetComponentsInChildren(buttonGo, "UnityEngine.UI.Text, UnityEngine.UI");
		if (texts == null)
		{
			return;
		}
		foreach (object t in texts)
		{
			SetProperty(t, "text", text);
			SetProperty(t, "alignment", EnumValue("UnityEngine.TextAnchor, UnityEngine.CoreModule", "MiddleCenter"));
			if (focus)
			{
				SetColor(t, 1f, 0.85f, 0.4f, 1f);
			}
			else if (dead)
			{
				SetColor(t, 0.75f, 0.6f, 0.65f, 1f);
			}
		}
	}

	private static IEnumerable<object> GetComponentsInChildren(object go, string typeName)
	{
		Type type = Type.GetType(typeName);
		if (type == null || go == null)
		{
			return null;
		}
		MethodInfo m = go.GetType().GetMethod("GetComponentsInChildren", BindingFlags.Instance | BindingFlags.Public, null, new Type[2] { typeof(Type), typeof(bool) }, null)
			?? go.GetType().GetMethod("GetComponentsInChildren", BindingFlags.Instance | BindingFlags.Public, null, new Type[1] { typeof(Type) }, null);
		if (m == null)
		{
			return null;
		}
		object result = (m.GetParameters().Length == 2)
			? m.Invoke(go, new object[] { type, true })
			: m.Invoke(go, new object[1] { type });
		Array array = result as Array;
		if (array == null)
		{
			return null;
		}
		List<object> list = new List<object>();
		for (int i = 0; i < array.Length; i++)
		{
			list.Add(array.GetValue(i));
		}
		return list;
	}

	private static void LogOnce(string message)
	{
		LogService.LogInfo("[Real Politic] " + message);
	}

	// ---------------------------------------------------------------- кнопка запуска из окна юнита

	private static object _openButton;

	/// <summary>Кнопка живёт на боковой панели окна (значит блок строк родословной не нужен).</summary>
	public static bool HasSideTab => _openButton != null && SideTabButton.IsAlive(_openButton) && (GameRefs.ReadMember(_openButton, "name")?.ToString() ?? string.Empty) == "rp_family_tab";

	/// <summary>Кнопка «🌳 Дерево» прямо в содержимом профиля жителя —
	/// под заголовком блока «Родословная», среди штатных строк (поселение, королевство, семья).
	/// Живёт там же, где в игре смотрится всё про этого человека.</summary>
	public static void AttachOpenButton(object unitWindow)
	{
		try
		{
			if (_disabled)
			{
				return;
			}
			// 0.23.2: предпочитаем кнопку-вкладку на боковой панели окна жителя.
			object tab = SideTabButton.Ensure(unitWindow, "rp_family_tab", ModIcons.FamilyKey, ModIcons.PathFor(ModIcons.FamilyKey),
				GameText.Localized("RP_FAMILY_OPEN_BUTTON", "🌳 Родословная"),
				typeof(FamilyTreeWindow).GetMethod(nameof(OpenFromButton), BindingFlags.Static | BindingFlags.NonPublic));
			if (tab != null)
			{
				_openButton = tab;
				return;
			}
			if (_openButton != null && IsAlive(_openButton))
			{
				return;
			}
			object windowGo = GameRefs.ReadMember(unitWindow, "gameObject");
			if (windowGo == null)
			{
				return;
			}
			object container = Patches.TryGetStatsContainer(unitWindow);
			if (container == null)
			{
				return;
			}
			IEnumerable<object> buttons = GetComponentsInChildren(windowGo, "UnityEngine.UI.Button, UnityEngine.UI");
			object source = null;
			if (buttons != null)
			{
				foreach (object b in buttons)
				{
					source = b;
					break;
				}
			}
			if (source == null)
			{
				return;
			}
			object clone = CloneUnder(source, container);
			if (clone == null)
			{
				return;
			}
			SetName(clone, "rp_family_button");
			DestroyComponent(clone, "PowerButton");
			DestroyComponent(clone, "TipButton");
			DestroyComponent(clone, "TooltipButton");
			DestroyComponent(clone, "HoverTip");
			ModUi.SetProperty(GetComponent(clone, "UnityEngine.RectTransform, UnityEngine.CoreModule"), "sizeDelta", Activator.CreateInstance(Type.GetType("UnityEngine.Vector2, UnityEngine.CoreModule"), new object[2] { 210f, 26f }));
			SetButtonText(clone, GameText.Localized("RP_FAMILY_OPEN_BUTTON", "🌳 Родословная"), dead: false, focus: true);
			ModUi.AddIcon(clone, ModIcons.FamilyKey, -91f, 0f, 20f);
			HookOpenClick(clone);
			PlaceAfterTitleRow(clone, container);
			_openButton = clone;
		}
		catch (Exception ex)
		{
			LogOnce("не удалось прицепить кнопку дерева к окну юнита: " + ex.Message);
		}
	}

	/// <summary>Кнопка становится строкой сразу под заголовком «Родословная» в профиле.</summary>
	private static void PlaceAfterTitleRow(object clone, object container)
	{
		try
		{
			object rowsObj = GameRefs.ReadMember(container, "stats_rows");
			System.Collections.IList rows = rowsObj as System.Collections.IList;
			if (rows == null)
			{
				return;
			}
			for (int i = 0; i < rows.Count; i++)
			{
				object rowGo = GameRefs.ReadMember(rows[i], "gameObject");
				if (rowGo == null || !string.Equals(GameRefs.ReadMember(rowGo, "name")?.ToString(), "[KV] RP_FAMILY_TITLE", StringComparison.Ordinal))
				{
					continue;
				}
				object cloneTransform = GameRefs.ReadMember(clone, "transform");
				object rowTransform = GameRefs.ReadMember(rowGo, "transform");
				object siblingIndex = GameRefs.Invoke(rowTransform, "GetSiblingIndex");
				if (siblingIndex is int sib)
				{
					GameRefs.Invoke(cloneTransform, "SetSiblingIndex", sib + 1);
				}
				return;
			}
		}
		catch (Exception)
		{
		}
	}

	private static void HookOpenClick(object buttonGo)
	{
		try
		{
			object button = GetComponent(buttonGo, "UnityEngine.UI.Button, UnityEngine.UI");
			if (button == null)
			{
				return;
			}
			object onClick = GameRefs.ReadMember(button, "onClick");
			if (onClick == null)
			{
				return;
			}
			GameRefs.Invoke(onClick, "RemoveAllListeners");
			Type actionType = Type.GetType("UnityEngine.Events.UnityAction, UnityEngine.CoreModule");
			Delegate del = Delegate.CreateDelegate(actionType, typeof(FamilyTreeWindow).GetMethod(nameof(OpenFromButton), BindingFlags.Static | BindingFlags.NonPublic));
			MethodInfo addListener = onClick.GetType().GetMethod("AddListener", BindingFlags.Instance | BindingFlags.Public);
			addListener?.Invoke(onClick, new object[] { del });
		}
		catch (Exception ex)
		{
			LogOnce("не удалось повесить клик кнопки дерева: " + ex.Message);
		}
	}

	private static void OpenFromButton()
	{
		LogService.LogInfo("[Real Politic] клик: родословная (житель #" + Patches.CurrentUnitWindowActorId() + ")");
		ToggleForCurrent();
	}

	private static void Disable(string why)
	{
		_disabled = true;
		LogService.LogError("[Real Politic] окно родословной отключено (" + why + ") — в профиле остаётся текстовый блок");
	}
}
