using System.Drawing;
using System.Reflection;
using CommandBars.Controls;
using CommandBars.Model;
using CommandBars.Rendering;
using Xunit;

namespace CommandBars.Tests;

public sealed class SplitButtonRenderingTests
{
    public static IEnumerable<object[]> ScalesAndOrientations()
    {
        foreach (float scale in new[] { 1f, 1.25f, 1.5f, 2f })
        foreach (var orientation in new[] { BarOrientation.Horizontal, BarOrientation.Vertical })
            yield return new object[] { scale, orientation };
    }

    [Theory]
    [MemberData(nameof(ScalesAndOrientations))]
    public void VistaIdleDividerIsOneBrightDevicePixelAlignedWithHover(float scale, BarOrientation orientation)
    {
        var renderer = new VistaAuroraRenderer { Scale = scale };
        var (bounds, _, arrow) = Geometry(scale, orientation);
        using var actual = new Bitmap(bounds.Right + 4, bounds.Bottom + 4);
        using (var g = Graphics.FromImage(actual))
            renderer.DrawSplitDivider(g, bounds, arrow, orientation);
        int inset = (int)Math.Round(3 * scale);
        for (int y = 0; y < actual.Height; y++)
        for (int x = 0; x < actual.Width; x++)
        {
            bool line = orientation == BarOrientation.Vertical
                ? y == arrow.Top && x >= bounds.Left + inset && x < bounds.Right - inset
                : x == arrow.Left && y >= bounds.Top + inset && y < bounds.Bottom - inset;
            Assert.Equal(line ? renderer.Colors.SeparatorLight.ToArgb() : 0,
                actual.GetPixel(x, y).ToArgb());
        }
    }

    [Theory]
    [MemberData(nameof(ScalesAndOrientations))]
    public void VistaHalvesShareOneOutlineWithOnlyAnInteriorDivider(float scale, BarOrientation orientation)
    {
        var renderer = new VistaAuroraRenderer { Scale = scale };
        var (bounds, button, arrow) = Geometry(scale, orientation);
        bool vertical = orientation == BarOrientation.Vertical;
        int seam = vertical ? arrow.Top : arrow.Left;
        Rectangle surface = Surface(bounds, scale, orientation, vista: true);
        foreach (var state in new[] { RenderState.Hot, RenderState.Pressed, RenderState.Checked,
            RenderState.Normal, RenderState.Disabled })
        {
            var arrowState = state == RenderState.Pressed ? RenderState.Hot : state;
            using var actual = new Bitmap(bounds.Right + 4, bounds.Bottom + 4);
            using var main = new Bitmap(actual.Width, actual.Height);
            using var trailing = new Bitmap(actual.Width, actual.Height);
            using (var g = Graphics.FromImage(actual))
                renderer.DrawSplitButton(g, bounds, button, arrow, state, arrowState, orientation);
            using (var g = Graphics.FromImage(main)) renderer.DrawButton(g, bounds, state, orientation);
            using (var g = Graphics.FromImage(trailing)) renderer.DrawButton(g, bounds, arrowState, orientation);

            for (int y = 0; y < actual.Height; y++)
            for (int x = 0; x < actual.Width; x++)
            {
                bool divider = vertical ? y == seam && x > surface.Left && x < surface.Right - 1
                    : x == seam && y > surface.Top && y < surface.Bottom - 1;
                bool active = state != RenderState.Normal && state != RenderState.Disabled;
                if (divider && active)
                {
                    Color border = arrowState == RenderState.Checked ? renderer.Colors.ButtonCheckedBorder
                        : renderer.Colors.ButtonHotBorder;
                    Assert.Equal(border.ToArgb(), actual.GetPixel(x, y).ToArgb());
                    continue;
                }
                // Includes both sides of the join, outer border and empty inset:
                // neither half may add an inner corner, gap or extra outline.
                var expected = (vertical ? y : x) < seam ? main : trailing;
                Assert.Equal(expected.GetPixel(x, y), actual.GetPixel(x, y));
            }
        }
    }

    [Theory]
    [MemberData(nameof(ScalesAndOrientations))]
    public void OpenDividerStaysStrictlyInsideEachThemesInsetBorder(float scale, BarOrientation orientation)
    {
        foreach (bool vista in new[] { false, true })
        {
            CommandBarRenderer renderer = vista ? new VistaAuroraRenderer() : new Office2000Renderer();
            renderer.Scale = scale;
            var (bounds, _, arrow) = Geometry(scale, orientation);
            var surface = Surface(bounds, scale, orientation, vista);
            using var before = new Bitmap(bounds.Right + 4, bounds.Bottom + 4);
            using (var g = Graphics.FromImage(before))
                renderer.DrawOpenMenuButton(g, bounds, orientation, PopupConnectionEdge.None);
            using var after = (Bitmap)before.Clone();
            using (var g = Graphics.FromImage(after))
                renderer.DrawOpenSplitDivider(g, bounds, arrow, orientation);
            for (int y = 0; y < after.Height; y++)
            for (int x = 0; x < after.Width; x++)
            {
                bool divider = orientation == BarOrientation.Vertical
                    ? y == arrow.Top && x > surface.Left && x < surface.Right - 1
                    : x == arrow.Left && y > surface.Top && y < surface.Bottom - 1;
                if (divider)
                    Assert.Equal(renderer.Colors.MenuOpenBorder.ToArgb(), after.GetPixel(x, y).ToArgb());
                else
                    Assert.Equal(before.GetPixel(x, y), after.GetPixel(x, y));
            }
        }
    }

    [Theory]
    [InlineData(false, false, false)]
    [InlineData(false, false, true)]
    [InlineData(false, true, false)]
    [InlineData(false, true, true)]
    [InlineData(true, false, false)]
    [InlineData(true, false, true)]
    [InlineData(true, true, false)]
    [InlineData(true, true, true)]
    public void ControlUsesInsetDividerForArrowMouseDownAndLatchedOpen(bool vista, bool vertical, bool open)
    {
        CommandBarRenderer renderer = vista ? new VistaAuroraRenderer() : new Office2000Renderer();
        var orientation = vertical ? BarOrientation.Vertical : BarOrientation.Horizontal;
        var bar = new CommandBar("split", CommandBarType.Toolbar) { Dock = vertical ? DockState.Left : DockState.Top };
        var split = bar.Items.AddSplitButton(new Command("split"));
        using var control = new CommandBarControl { Bar = bar, Renderer = renderer };
        var bounds = new Rectangle(4, 4, vertical ? 32 : 54, vertical ? 54 : 32);
        var flags = BindingFlags.Instance | BindingFlags.NonPublic;
        typeof(CommandBarControl).GetField(open ? "_openSplitButton" : "_pressedItem", flags)!.SetValue(control, split);
        if (!open) typeof(CommandBarControl).GetField("_pressedSplitArrow", flags)!.SetValue(control, true);
        using var actual = new Bitmap(64, 64);
        using (var g = Graphics.FromImage(actual))
            typeof(CommandBarControl).GetMethod("DrawCommandItem", flags)!
                .Invoke(control, new object[] { g, split, bounds, false });
        using var expected = new Bitmap(64, 64);
        using (var g = Graphics.FromImage(expected))
            renderer.DrawOpenMenuButton(g, bounds, orientation, PopupConnectionEdge.None);
        var surface = Surface(bounds, 1f, orientation, vista);
        // Inspect the entire cross-axis border and gutter; the arrow glyph and
        // divider belong to the interior and cannot alter any of these pixels.
        for (int y = bounds.Top; y < bounds.Bottom; y++)
        for (int x = bounds.Left; x < bounds.Right; x++)
        {
            bool edge = vertical ? x <= surface.Left || x >= surface.Right - 1
                : y <= surface.Top || y >= surface.Bottom - 1;
            if (edge) Assert.Equal(expected.GetPixel(x, y), actual.GetPixel(x, y));
        }
    }

    private static (Rectangle Bounds, Rectangle Button, Rectangle Arrow) Geometry(float scale, BarOrientation orientation)
    {
        int R(int value) => (int)Math.Round(value * scale);
        bool vertical = orientation == BarOrientation.Vertical;
        var bounds = new Rectangle(4, 4, R(vertical ? 32 : 54), R(vertical ? 54 : 32));
        var arrow = vertical ? new Rectangle(bounds.Left, bounds.Bottom - R(12), bounds.Width, R(12))
            : new Rectangle(bounds.Right - R(12), bounds.Top, R(12), bounds.Height);
        var button = vertical ? Rectangle.FromLTRB(bounds.Left, bounds.Top, bounds.Right, arrow.Top)
            : Rectangle.FromLTRB(bounds.Left, bounds.Top, arrow.Left, bounds.Bottom);
        return (bounds, button, arrow);
    }

    private static Rectangle Surface(Rectangle bounds, float scale, BarOrientation orientation, bool vista)
    {
        int along = Math.Max(1, (int)Math.Round(scale));
        int across = vista ? Math.Max(1, (int)Math.Round(2 * scale)) : along;
        return orientation == BarOrientation.Vertical ? Rectangle.Inflate(bounds, -across, -along)
            : Rectangle.Inflate(bounds, -along, -across);
    }
}
