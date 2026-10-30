using RealPolitic.Core;
using Xunit;

namespace RealPolitic.Tests;

/// <summary>0.31.12: названия войн строятся по коренным именам сторон, без ярлыка идеологии.</summary>
public class WarTitleTests
{
	[Fact]
	public void WarTitle_UsesRootNames_NotIdeologyLabels()
	{
		string? title = IdeologyStateNames.WarTitle("Бульун", "Альтасерон");
		Assert.NotNull(title);
		Assert.EndsWith(" война", title);
		Assert.Contains("-", title);
		Assert.DoesNotContain("Монархия", title);
		Assert.DoesNotContain("Теократия", title);
		Assert.DoesNotContain("Рейх", title);
	}

	[Fact]
	public void WarTitle_WithIdeologyLabelOnSide_StillUsesRoot()
	{
		string? title = IdeologyStateNames.WarTitle("Бульунский Рейх", "Альтасеронская Корона");
		Assert.NotNull(title);
		Assert.DoesNotContain("Рейх", title);
		Assert.DoesNotContain("Корона", title);
	}

	[Fact]
	public void WarTitle_NullName_ReturnsNull()
	{
		Assert.Null(IdeologyStateNames.WarTitle(null, "Альтасерон"));
		Assert.Null(IdeologyStateNames.WarTitle("Бульун", "#12"));
	}
}
