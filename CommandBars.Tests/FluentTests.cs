using System.Drawing;
using System.Reflection;
using System.Windows.Forms;
using CommandBars.Controls;
using CommandBars.Model;
using CommandBars.Rendering;
using Xunit;

namespace CommandBars.Tests;

public class FluentTests
{
    [Theory]
    [InlineData(DockState.Top, false, 2)]
    [InlineData(DockState.Bottom, false, 2)]
    [InlineData(DockState.Top, true, 3)]
    [InlineData(DockState.Bottom, true, 3)]
    [InlineData(DockState.Left, false, 4)]
    [InlineData(DockState.Right, true, 3)]
    public void ToolbarPopupAlignsWithVisibleButton(DockState dock, bool overflow, int inset)
    {
        using var control = new CommandBarControl
        {
            Renderer = new FluentRenderer(),
            Bar = new CommandBar("test", CommandBarType.Toolbar) { Dock = dock },
        };
        var bounds = new Rectangle(100, 100, 32, 32);
        var actual = (Rectangle)typeof(CommandBarControl)
            .GetMethod("PopupButtonAnchor", BindingFlags.Instance | BindingFlags.NonPublic)!
            .Invoke(control, new object[] { bounds, overflow })!;
        bool vertical = dock == DockState.Left || dock == DockState.Right;
        Assert.Equal(vertical
            ? Rectangle.FromLTRB(100, 100 + inset, 132, 132 - inset)
            : Rectangle.FromLTRB(100 + inset, 100, 132 - inset, 132), actual);
    }

    [Theory]
    [InlineData(1f)]
    [InlineData(1.5f)]
    [InlineData(2f)]
    public void MenuBarKeepsCompactRowAndAlignedPopup(float scale)
    {
        using var bitmap = new Bitmap(1, 1);
        using var g = Graphics.FromImage(bitmap);
        var bar = new CommandBar("menu", CommandBarType.MenuBar);
        bar.Items.AddPopup("File");
        int baseline = BarLayoutEngine.LayoutHorizontal(g, bar, SystemFonts.MenuFont!, 16,
            0, BarMetrics.For(scale), scale, false, out _);
        int fluent = BarLayoutEngine.LayoutHorizontal(g, bar, SystemFonts.MenuFont!, 16,
            0, BarMetrics.For(scale, fluent: true), scale, false, out _);
        Assert.Equal(baseline, fluent);
        using var control = new CommandBarControl { Renderer = new FluentRenderer(), Bar = bar };
        typeof(CommandBarControl).GetField("_dpiScale", BindingFlags.Instance | BindingFlags.NonPublic)!
            .SetValue(control, scale);
        var bounds = new Rectangle(100, 100, 80, 32);
        var actual = (Rectangle)typeof(CommandBarControl)
            .GetMethod("PopupButtonAnchor", BindingFlags.Instance | BindingFlags.NonPublic)!
            .Invoke(control, new object[] { bounds, false })!;
        Assert.Equal(100 + (int)Math.Round(2 * scale), actual.Left);
        Assert.Equal(bounds.Top, actual.Top);
    }

    [Theory]
    [InlineData(16)]
    [InlineData(24)]
    [InlineData(32)]
    public void ToolbarButtonAndOverflowHaveMatchingVisibleSize(int iconSize)
    {
        var renderer = new FluentRenderer();
        int row = iconSize + 8;
        using var button = new Bitmap(row, row);
        using var overflow = new Bitmap(row, row);
        using var g1 = Graphics.FromImage(button);
        using var g2 = Graphics.FromImage(overflow);
        renderer.DrawButton(g1, new Rectangle(0, 0, iconSize + 6, row), RenderState.Hot, BarOrientation.Horizontal);
        renderer.DrawChevron(g2, new Rectangle(0, 0, row, row), new Rectangle(0, 0, row, row), BarOrientation.Horizontal, RenderState.Hot);
        Rectangle Ink(Bitmap bitmap)
        {
            var points = (from x in Enumerable.Range(0, bitmap.Width)
                          from y in Enumerable.Range(0, bitmap.Height)
                          where bitmap.GetPixel(x, y).A > 0 select new Point(x, y)).ToArray();
            return Rectangle.FromLTRB(points.Min(p => p.X), points.Min(p => p.Y), points.Max(p => p.X) + 1, points.Max(p => p.Y) + 1);
        }
        Assert.Equal(Ink(overflow).Size, Ink(button).Size);
        using var control = new CommandBarControl
        {
            Renderer = renderer,
            Bar = new CommandBar("test", CommandBarType.Toolbar) { IconSize = iconSize },
        };
        int fitted = (int)typeof(CommandBarControl).GetMethod("ToolbarImageSize", BindingFlags.Instance | BindingFlags.NonPublic)!
            .Invoke(control, new object[] { new Rectangle(0, 0, iconSize + 6, row) })!;
        Assert.Equal(iconSize - 2, fitted);
        Assert.Equal(iconSize, control.Bar!.IconSize);
        Assert.True(Ink(button).Height - fitted >= 4);
    }

    [Fact]
    public void SplitButtonPressOnlyDarkensThePressedHalf()
    {
        var renderer = new FluentRenderer();
        using var bitmap = new Bitmap(80, 32);
        using var graphics = Graphics.FromImage(bitmap);
        renderer.DrawSplitButton(graphics, new Rectangle(0, 0, 80, 32),
            new Rectangle(0, 0, 60, 32), new Rectangle(60, 0, 20, 32),
            RenderState.Pressed, RenderState.Hot, BarOrientation.Horizontal);
        Assert.Equal(renderer.Colors.ButtonPressedBegin.ToArgb(), bitmap.GetPixel(30, 16).ToArgb());
        Assert.Equal(renderer.Colors.ButtonHotBegin.ToArgb(), bitmap.GetPixel(70, 16).ToArgb());
    }

    [Fact]
    public void CheckAndRadioGlyphsHaveDifferentShapesWithoutRowFill()
    {
        var renderer = new FluentRenderer();
        using var check = new Bitmap(24, 24);
        using var radio = new Bitmap(24, 24);
        using var gc = Graphics.FromImage(check);
        using var gr = Graphics.FromImage(radio);
        gc.Clear(renderer.Colors.MenuBackground);
        gr.Clear(renderer.Colors.MenuBackground);
        renderer.DrawMenuCheck(gc, new Rectangle(0, 0, 24, 24), RenderState.Checked);
        renderer.DrawMenuRadio(gr, new Rectangle(0, 0, 24, 24), RenderState.Checked);
        Assert.Equal(check.GetPixel(2, 2), radio.GetPixel(2, 2));
        Assert.Equal(renderer.Colors.MenuBackground.ToArgb(), check.GetPixel(2, 2).ToArgb());
        Assert.NotEqual(check.GetPixel(7, 12), radio.GetPixel(7, 12));
    }

    [Fact]
    public void ThemeCanBeSelectedByKeyAndRestoredFromLayout()
    {
        using var manager = new CommandBarManager();
        Assert.True(manager.ApplyTheme(CommandBarThemeKeys.Fluent));
        Assert.Equal(CommandBarTheme.Fluent, manager.Theme);
        Assert.IsType<FluentRenderer>(manager.Renderer);
        string path = Path.GetTempFileName();
        try
        {
            manager.SaveLayout(path);
            using var restored = new CommandBarManager();
            restored.LoadLayout(path);
            Assert.Equal(CommandBarTheme.Fluent, restored.Theme);
            Assert.Equal("fluent", restored.ActiveThemeKey);
            File.WriteAllText(path, File.ReadAllText(path).Replace("fluent", "visualstudio2026"));
            using var legacy = new CommandBarManager();
            legacy.LoadLayout(path);
            Assert.Equal(CommandBarTheme.Fluent, legacy.Theme);
            Assert.Equal("fluent", legacy.ActiveThemeKey);
            legacy.SaveLayout(path);
            Assert.DoesNotContain("visualstudio2026", File.ReadAllText(path));
        }
        finally { File.Delete(path); }
    }

    [Theory]
    [InlineData(1f)]
    [InlineData(1.5f)]
    [InlineData(2f)]
    public void GripperChangesColorInBothOrientations(float scale)
    {
        var renderer = new FluentRenderer { Scale = scale };
        foreach (var orientation in new[] { BarOrientation.Horizontal, BarOrientation.Vertical })
        {
            using var idle = new Bitmap(80, 80);
            using var hot = new Bitmap(80, 80);
            using var g1 = Graphics.FromImage(idle);
            using var g2 = Graphics.FromImage(hot);
            var bounds = orientation == BarOrientation.Horizontal
                ? new Rectangle(0, 0, (int)(8 * scale), (int)(32 * scale))
                : new Rectangle(0, 0, (int)(32 * scale), (int)(8 * scale));
            renderer.DrawGripper(g1, bounds, orientation, false);
            renderer.DrawGripper(g2, bounds, orientation, true);
            int x = (int)((orientation == BarOrientation.Horizontal ? 2 : 16) * scale);
            int y = (int)((orientation == BarOrientation.Horizontal ? 16 : 2) * scale);
            Assert.NotEqual(idle.GetPixel(x, y), hot.GetPixel(x, y));
        }
    }

    [Fact]
    public void ComboSelectionRemainsHighlightedWhileAnotherRowIsHovered()
    {
        var renderer = new FluentRenderer();
        using var bitmap = new Bitmap(120, 64);
        using var graphics = Graphics.FromImage(bitmap);
        graphics.Clear(renderer.Colors.MenuBackground);
        renderer.DrawComboSelection(graphics, new Rectangle(0, 0, 120, 30), true, false);
        renderer.DrawComboSelection(graphics, new Rectangle(0, 32, 120, 30), false, true);
        Assert.Equal(bitmap.GetPixel(50, 15), bitmap.GetPixel(50, 47));
        Assert.NotEqual(bitmap.GetPixel(4, 15), bitmap.GetPixel(4, 47));
    }

    [Fact]
    public void PopupAndComboLeaveModernWindowCornersToDwm()
    {
        var bar = new CommandBar("test", CommandBarType.Popup);
        var item = bar.Items.AddButton(new Command("test") { Text = "Test" });
        using var popup = new CommandBarPopupWindow(bar, new FluentRenderer(), SystemFonts.MenuFont!, 16, 1);
        Assert.Equal(OperatingSystem.IsWindowsVersionAtLeast(10, 0, 22000), popup.Region is null);
        Assert.True(item.Bounds.Height >= 28);
        var combo = new CommandBarComboBox();
        combo.Items.Add("Debug");
        combo.SelectedItem = "Debug";
        using var dropdown = new ComboDropDown(combo, new FluentRenderer(), SystemFonts.MenuFont!, new Rectangle(0, 0, 100, 30));
        Assert.Equal(OperatingSystem.IsWindowsVersionAtLeast(10, 0, 22000), dropdown.Region is null);
        Assert.Equal(33, dropdown.Top);
    }

    [Theory]
    [InlineData(1f)]
    [InlineData(1.5f)]
    [InlineData(2f)]
    public void RoundedCoverageIsSymmetricAndAntialiased(float scale)
    {
        using var bitmap = RoundedSurface.Create((int)(43 * scale), (int)(29 * scale), 4 * scale, Color.White, Color.Gray);
        bool fractional = false;
        for (int y = 0; y < bitmap.Height; y++)
        for (int x = 0; x < bitmap.Width; x++)
        {
            var pixel = bitmap.GetPixel(x, y);
            Assert.Equal(pixel, bitmap.GetPixel(bitmap.Width - 1 - x, y));
            Assert.Equal(pixel, bitmap.GetPixel(x, bitmap.Height - 1 - y));
            fractional |= pixel.A > 0 && pixel.A < 255;
        }
        Assert.True(fractional);
    }

    [Fact]
    public void CompactMetricsKeepPaddingButWidenSplitArrow()
    {
        var classic = BarMetrics.For(1, 24);
        var fluent = BarMetrics.For(1, 24, true);
        Assert.Equal(classic.ContentVPad, fluent.ContentVPad);
        Assert.Equal(classic.ButtonHPad, fluent.ButtonHPad);
        Assert.True(fluent.ArrowWidth > classic.ArrowWidth);
    }

    [Fact]
    public void HoverStaysInsideToolbarAndComboRetainsBorderAcrossStates()
    {
        var renderer = new FluentRenderer();
        using var bitmap = new Bitmap(60, 30);
        using var g = Graphics.FromImage(bitmap);
        g.Clear(Color.Magenta);
        renderer.DrawButton(g, new Rectangle(0, 0, 60, 30), RenderState.Hot, BarOrientation.Horizontal);
        Assert.Equal(Color.Magenta.ToArgb(), bitmap.GetPixel(30, 0).ToArgb());
        Assert.Equal(Color.Magenta.ToArgb(), bitmap.GetPixel(30, 29).ToArgb());
        g.Clear(Color.Magenta);
        renderer.DrawComboBoxChrome(g, new Rectangle(0, 0, 60, 30), new Rectangle(40, 0, 20, 30), RenderState.Normal, Color.White);
        Assert.Equal(Color.White.ToArgb(), bitmap.GetPixel(30, 15).ToArgb());
        var border = bitmap.GetPixel(30, 0);
        renderer.DrawComboBoxChrome(g, new Rectangle(0, 0, 60, 30), new Rectangle(40, 0, 20, 30), RenderState.Hot, Color.White);
        Assert.Equal(renderer.Colors.BarGradientBegin.ToArgb(), bitmap.GetPixel(30, 15).ToArgb());
        Assert.Equal(border, bitmap.GetPixel(30, 0));
    }

    [Fact]
    public void FullHeightGripperFollowsToolbarCornerAndPreservesBorder()
    {
        var renderer = new FluentRenderer();
        using var bitmap = new Bitmap(80, 32);
        using var g = Graphics.FromImage(bitmap);
        g.Clear(Color.Magenta);
        renderer.DrawGripper(g, new Rectangle(0, 0, 8, 32), new Rectangle(0, 0, 80, 32), BarOrientation.Horizontal, true);
        Assert.Equal(Color.Magenta.ToArgb(), bitmap.GetPixel(0, 0).ToArgb());
        Assert.NotEqual(Color.Magenta.ToArgb(), bitmap.GetPixel(3, 1).ToArgb());
        Assert.Equal(bitmap.GetPixel(3, 1), bitmap.GetPixel(3, 30));
        Assert.Equal(renderer.Colors.BarBorder.ToArgb(), bitmap.GetPixel(0, 16).ToArgb());
        Assert.Equal(Color.Magenta.ToArgb(), bitmap.GetPixel(20, 16).ToArgb());
    }

    [Theory]
    [InlineData(1f)]
    [InlineData(1.5f)]
    [InlineData(2f)]
    public void MenuIconFrameIsSquareAndEquallyInset(float scale)
    {
        var bar = new CommandBar("menu", CommandBarType.Popup);
        var item = bar.Items.AddToggle(new Command("one") { Text = "One", Checked = CommandCheckState.Checked });
        var separator = bar.Items.AddSeparator();
        bar.Items.AddButton(new Command("two") { Text = "Two" });
        using var popup = new CommandBarPopupWindow(bar, new FluentRenderer(), SystemFonts.MenuFont!, 24, scale);
        int R(int value) => (int)Math.Round(value * scale);
        var box = (Rectangle)typeof(CommandBarPopupWindow).GetMethod("MenuIconBox", BindingFlags.Instance | BindingFlags.NonPublic)!
            .Invoke(popup, new object[] { item.Bounds, R(28) })!;
        Assert.Equal(box.Width, box.Height);
        Assert.Equal(box.Left - R(3), box.Top - item.Bounds.Top - R(1));
        Assert.Equal(box.Top - item.Bounds.Top - R(1), item.Bounds.Bottom - R(1) - box.Bottom);
        Assert.Equal(1, separator.Bounds.Height % 2);
    }

    [Fact]
    public void OverflowHighlightIsSquareAndClearsTrailingBorder()
    {
        var renderer = new FluentRenderer();
        using var bitmap = new Bitmap(40, 40);
        using var g = Graphics.FromImage(bitmap);
        g.Clear(Color.Magenta);
        renderer.DrawChevron(g, new Rectangle(1, 1, 32, 32), new Rectangle(0, 0, 34, 34), BarOrientation.Horizontal, RenderState.Hot);
        int left = Enumerable.Range(0, 40).First(x => bitmap.GetPixel(x, 17).ToArgb() == renderer.Colors.ButtonHotBegin.ToArgb());
        int right = Enumerable.Range(0, 40).Last(x => bitmap.GetPixel(x, 17).ToArgb() == renderer.Colors.ButtonHotBegin.ToArgb());
        int top = Enumerable.Range(0, 40).First(y => bitmap.GetPixel(17, y).ToArgb() == renderer.Colors.ButtonHotBegin.ToArgb());
        int bottom = Enumerable.Range(0, 40).Last(y => bitmap.GetPixel(17, y).ToArgb() == renderer.Colors.ButtonHotBegin.ToArgb());
        Assert.Equal(right - left, bottom - top);
        Assert.True(33 - right >= 3);
    }

    [Theory]
    [InlineData(DockEdge.Top)]
    [InlineData(DockEdge.Left)]
    public void DockRowsAndColumnsHaveGaps(DockEdge edge)
    {
        using var manager = new CommandBarManager { Theme = CommandBarTheme.Fluent };
        var first = manager.AddBar("one", CommandBarType.Toolbar);
        var second = manager.AddBar("two", CommandBarType.Toolbar);
        var third = manager.AddBar("three", CommandBarType.Toolbar);
        first.Row = second.Row = 0;
        third.Row = 1;
        first.Dock = second.Dock = third.Dock = edge == DockEdge.Top ? DockState.Top : DockState.Left;
        first.Items.AddButton(new Command("one") { Text = "One" });
        second.Items.AddButton(new Command("two") { Text = "Two" });
        third.Items.AddButton(new Command("three") { Text = "Three" });
        using var host = new DockHost { Size = new Size(800, 800), Edge = edge, Manager = manager };
        host.Renderer = new FluentRenderer();
        host.PerformLayout();
        var a = host.BarControls.Single(c => c.Bar == first);
        var b = host.BarControls.Single(c => c.Bar == second);
        var c = host.BarControls.Single(c => c.Bar == third);
        if (edge == DockEdge.Top)
        {
            Assert.True(b.Left - a.Right >= 4);
            Assert.True(c.Top - a.Bottom >= 4);
        }
        else
        {
            Assert.True(b.Top - a.Bottom >= 4);
            Assert.True(c.Left - a.Right >= 4);
        }
    }

    [Fact]
    public void PointerLeavingGripperClearsHover()
    {
        using var control = new CommandBarControl();
        control.Size = new Size(100, 30);
        typeof(CommandBarControl).GetField("_showGripper", BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(control, true);
        typeof(CommandBarControl).GetMethod("OnMouseMove", BindingFlags.Instance | BindingFlags.NonPublic)!
            .Invoke(control, new object[] { new MouseEventArgs(MouseButtons.None, 0, 2, 2, 0) });
        var hot = typeof(CommandBarControl).GetField("_gripperHot", BindingFlags.Instance | BindingFlags.NonPublic)!;
        Assert.True((bool)hot.GetValue(control)!);
        typeof(CommandBarControl).GetMethod("OnMouseLeave", BindingFlags.Instance | BindingFlags.NonPublic)!
            .Invoke(control, new object[] { EventArgs.Empty });
        Assert.False((bool)hot.GetValue(control)!);
    }
}
