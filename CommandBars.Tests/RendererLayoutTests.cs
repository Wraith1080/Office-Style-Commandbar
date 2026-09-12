using System.Drawing;
using System.Reflection;
using System.Windows.Forms;
using CommandBars.Controls;
using CommandBars.Model;
using CommandBars.Rendering;
using Xunit;

namespace CommandBars.Tests;

public class RendererLayoutTests
{
    // Deliberately combines independent choices that match neither built-in
    // layout. Controls must consume overrides rather than recognize a theme.
    private sealed class CustomRenderer : Office2003Renderer
    {
        public override int SplitArrowWidth => 22;
        public override int ToolbarPopupHorizontalPadding => 9;
        public override int GetMenuSeparatorHeight(float scale) => (int)Math.Round(11 * scale);
        public override Rectangle GetMenuIconBounds(Rectangle row, int size, int margin, float scale)
            => new(7, row.Y + 3, 13, 13);
        public override Padding GetComboPopupInsets(float scale) => new(3);
        public override int GetComboPopupRowPadding(float scale) => (int)Math.Round(17 * scale);
        public override Rectangle GetPopupAnchorBounds(Rectangle bounds, bool overflow, bool vertical, float scale)
            => Rectangle.Inflate(bounds, -7, -5);
    }

    [Theory]
    [InlineData(1f)]
    [InlineData(1.5f)]
    [InlineData(2f)]
    public void ControlsConsumeIndependentRendererGeometry(float scale)
    {
        var renderer = new CustomRenderer { Scale = scale };
        var metrics = BarMetrics.For(scale, renderer: renderer);
        Assert.Equal((int)Math.Round(22 * scale), metrics.ArrowWidth);
        Assert.Equal((int)Math.Round(9 * scale), metrics.ToolbarPopupHPad);
        Assert.False(metrics.SquareToolbarButtons);
        Assert.False(renderer.PopupDropShadow);

        var bar = new CommandBar("custom", CommandBarType.Popup);
        bar.Items.AddSeparator();
        using var popup = new CommandBarPopupWindow(bar, renderer, SystemFonts.MenuFont!, 16, scale);
        Assert.Equal((int)Math.Round(11 * scale), Field<int>(popup, "_sepHeight"));
        var icon = (Rectangle)typeof(CommandBarPopupWindow).GetMethod("MenuIconBox",
            BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(popup,
                new object[] { new Rectangle(0, 20, 100, 30), 20 })!;
        Assert.Equal(new Rectangle(7, 23, 13, 13), icon);

        var combo = new CommandBarComboBox();
        combo.Items.Add("One");
        using var dropdown = new ComboDropDown(combo, renderer, SystemFonts.MenuFont!, new Rectangle(0, 0, 100, 30));
        Assert.Equal(SystemFonts.MenuFont!.Height + (int)Math.Round(17 * scale) + 6, dropdown.Height);

        using var control = new CommandBarControl { Renderer = renderer };
        var anchor = (Rectangle)typeof(CommandBarControl).GetMethod("PopupButtonAnchor",
            BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(control,
                new object[] { new Rectangle(0, 0, 100, 30), false })!;
        Assert.Equal(new Rectangle(7, 5, 86, 20), anchor);
    }

    private static T Field<T>(object target, string name)
        => (T)target.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(target)!;
}
