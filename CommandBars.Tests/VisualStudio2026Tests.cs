using System.Drawing;
using System.Reflection;
using System.Windows.Forms;
using CommandBars.Controls;
using CommandBars.Model;
using CommandBars.Rendering;
using Xunit;

namespace CommandBars.Tests;

public class VisualStudio2026Tests
{
    [Fact]
    public void SplitButtonPressOnlyDarkensThePressedHalf()
    {
        var renderer = new VisualStudio2026Renderer();
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
        var renderer = new VisualStudio2026Renderer();
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
        Assert.True(manager.ApplyTheme(CommandBarThemeKeys.VisualStudio2026));
        Assert.Equal(CommandBarTheme.VisualStudio2026, manager.Theme);
        Assert.IsType<VisualStudio2026Renderer>(manager.Renderer);
        string path = Path.GetTempFileName();
        try
        {
            manager.SaveLayout(path);
            using var restored = new CommandBarManager();
            restored.LoadLayout(path);
            Assert.Equal(CommandBarTheme.VisualStudio2026, restored.Theme);
        }
        finally { File.Delete(path); }
    }

    [Theory]
    [InlineData(1f)]
    [InlineData(1.5f)]
    [InlineData(2f)]
    public void GripperChangesColorInBothOrientations(float scale)
    {
        var renderer = new VisualStudio2026Renderer { Scale = scale };
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
        var renderer = new VisualStudio2026Renderer();
        using var bitmap = new Bitmap(120, 64);
        using var graphics = Graphics.FromImage(bitmap);
        graphics.Clear(renderer.Colors.MenuBackground);
        renderer.DrawComboSelection(graphics, new Rectangle(0, 0, 120, 30), true, false);
        renderer.DrawComboSelection(graphics, new Rectangle(0, 32, 120, 30), false, true);
        Assert.Equal(bitmap.GetPixel(50, 15), bitmap.GetPixel(50, 47));
        Assert.NotEqual(bitmap.GetPixel(4, 15), bitmap.GetPixel(4, 47));
    }

    [Fact]
    public void PopupAndComboConstructWithRoundedRegionsAndRoomierRows()
    {
        var bar = new CommandBar("test", CommandBarType.Popup);
        var item = bar.Items.AddButton(new Command("test") { Text = "Test" });
        using var popup = new CommandBarPopupWindow(bar, new VisualStudio2026Renderer(), SystemFonts.MenuFont!, 16, 1);
        Assert.NotNull(popup.Region);
        Assert.False(popup.Region!.IsVisible(0, 0));
        Assert.True(item.Bounds.Height >= 28);
        var combo = new CommandBarComboBox();
        combo.Items.Add("Debug");
        combo.SelectedItem = "Debug";
        using var dropdown = new ComboDropDown(combo, new VisualStudio2026Renderer(), SystemFonts.MenuFont!, new Rectangle(0, 0, 100, 30));
        Assert.NotNull(dropdown.Region);
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
