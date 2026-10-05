using System.Drawing;
using CommandBars.Model;
using CommandBars.Rendering;
using Xunit;

namespace CommandBars.Tests;

public sealed class ToolbarBorderTests
{
    public static IEnumerable<object[]> GradientThemes()
    {
        foreach (var theme in new[] { CommandBarTheme.Office2003, CommandBarTheme.Office2007, CommandBarTheme.Office2010 })
        foreach (var scheme in CommandBarColorSchemes.ForTheme(theme))
        foreach (float scale in new[] { 1f, 1.25f, 1.5f, 2f })
        foreach (var orientation in new[] { BarOrientation.Horizontal, BarOrientation.Vertical })
            yield return new object[] { theme, scheme, scale, orientation };
    }

    [Theory]
    [MemberData(nameof(GradientThemes))]
    public void GradientToolbarEdgeMatchesOptionsEndColorAcrossStates(
        CommandBarTheme theme, CommandBarColorScheme scheme, float scale, BarOrientation orientation)
    {
        var renderer = ThemeRenderer.Create(theme, scheme);
        renderer.Scale = scale;
        int R(int n) => (int)Math.Round(n * scale);
        bool horizontal = orientation == BarOrientation.Horizontal;
        var bounds = new Rectangle(3, 5, R(horizontal ? 160 : 30), R(horizontal ? 30 : 160));
        var nub = horizontal
            ? new Rectangle(bounds.Right - R(14), bounds.Top, R(14), bounds.Height)
            : new Rectangle(bounds.Left, bounds.Bottom - R(14), bounds.Width, R(14));
        foreach (var state in new[] { RenderState.Normal, RenderState.Hot, RenderState.Pressed })
        {
            using var bitmap = new Bitmap(bounds.Right + 3, bounds.Bottom + 3);
            using var graphics = Graphics.FromImage(bitmap);
            graphics.Clear(Color.Magenta);
            renderer.DrawBarBackground(graphics, bounds, CommandBarType.Toolbar, orientation, true, 0, R(200));
            renderer.DrawChevron(graphics, nub, bounds, orientation, state);
            using var before = (Bitmap)bitmap.Clone();
            renderer.DrawToolbarBorder(graphics, bounds, orientation);

            // Office 2010 retains square corners; the other gradient themes round them.
            int radius = theme == CommandBarTheme.Office2010 ? 0 : R(3);
            var strip = horizontal
                ? new Rectangle(bounds.Left + radius, bounds.Bottom - R(1), bounds.Width - 2 * radius, R(1))
                : new Rectangle(bounds.Right - R(1), bounds.Top + radius, R(1), bounds.Height - 2 * radius);
            for (int y = 0; y < bitmap.Height; y++)
            for (int x = 0; x < bitmap.Width; x++)
                Assert.Equal(strip.Contains(x, y) ? renderer.Colors.ChevronGradientEnd.ToArgb()
                    : before.GetPixel(x, y).ToArgb(), bitmap.GetPixel(x, y).ToArgb());
        }
    }

    [Theory]
    [InlineData(CommandBarTheme.Office97)]
    [InlineData(CommandBarTheme.Office2000)]
    [InlineData(CommandBarTheme.OfficeXP)]
    [InlineData(CommandBarTheme.Dark)]
    [InlineData(CommandBarTheme.Fluent)]
    [InlineData(CommandBarTheme.VistaAurora)]
    public void OtherThemesDoNotAddTheGradientToolbarEdge(CommandBarTheme theme)
    {
        var renderer = ThemeRenderer.Create(theme);
        using var bitmap = new Bitmap(80, 40);
        using var graphics = Graphics.FromImage(bitmap);
        graphics.Clear(Color.Magenta);
        foreach (var orientation in new[] { BarOrientation.Horizontal, BarOrientation.Vertical })
            renderer.DrawToolbarBorder(graphics, new Rectangle(0, 0, 80, 40), orientation);
        for (int y = 0; y < bitmap.Height; y++)
        for (int x = 0; x < bitmap.Width; x++)
            Assert.Equal(Color.Magenta.ToArgb(), bitmap.GetPixel(x, y).ToArgb());
    }
}
