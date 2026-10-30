using RealPolitic;
using Xunit;

namespace RealPolitic.GameTests;

/// <summary>
/// 0.48.3: «Ой какой ужас-то» — снимки окна КМУД, где строки налезали друг на друга,
/// а заголовки обрезались на середине.
///
/// Причина: высота строки считалась «по числу букв» с заниженной шириной знака,
/// поэтому двухстрочный заголовок занимал место одной строки, и следующая строка
/// рисовалась прямо по нему.
///
/// Тест стережёт главное свойство мерки: чем длиннее текст и меньше ширина, тем
/// больше строк, и один длинный текст никогда не «стоит» как короткий.
/// </summary>
public sealed class WindowTextMeasureTests
{
	[Fact]
	public void ShortLineTakesOneRow()
	{
		Assert.Equal(1, ModUi.LineCount("ИТОГ ДОЛГОГО КОНФЛИКТА", 9.5f, 192f));
	}

	[Fact]
	public void LongTitleTakesTwoRows()
	{
		// «НАКАЗАНИЕ БЕЗНРАВСТВЕННОЙ ДЕРЖАВЫ» — именно этот заголовок обрезался на снимке 542.
		int lines = ModUi.LineCount("НАКАЗАНИЕ БЕЗНРАВСТВЕННОЙ ДЕРЖАВЫ", 9.5f, 192f);
		Assert.True(lines >= 2, "заголовок должен переноситься, а не обрезаться; строк: " + lines);
	}

	[Fact]
	public void HeaderOfTheConferenceTakesTwoRows()
	{
		// «КОНФЕРЕНЦИЯ МИРНОГО УРЕГУЛИРОВАНИЯ ДЕЛ» — снимок 540: вторая строка уехала под подпись.
		int lines = ModUi.LineCount("КОНФЕРЕНЦИЯ МИРНОГО УРЕГУЛИРОВАНИЯ ДЕЛ", 9.5f, 192f);
		Assert.True(lines >= 2);
	}

	[Fact]
	public void HeightGrowsWithLines()
	{
		float one = ModUi.TextHeight("10 лет мира · 100% (726)", 9.5f, 192f);
		float two = ModUi.TextHeight("выделить деньги и еду на дорогу с сопровождением до самой границы", 9.5f, 192f);
		Assert.True(two > one * 2f, "длинная строка должна занимать заметно больше места: " + one + " vs " + two);
	}

	[Fact]
	public void VeryLongWordDoesNotOverflowSilently()
	{
		int lines = ModUi.LineCount("ЫЫЫЫЫЫЫЫЫЫЫЫЫЫЫЫЫЫЫЫЫЫЫЫЫЫЫЫЫЫЫЫЫЫЫЫЫЫЫЫЫЫЫЫЫЫ", 9.5f, 60f);
		Assert.True(lines >= 2);
	}

	[Fact]
	public void EmptyTextStillTakesOneRow()
	{
		Assert.Equal(1, ModUi.LineCount(string.Empty, 9f, 192f));
	}
}
