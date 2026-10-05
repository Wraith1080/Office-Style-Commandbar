using System.ComponentModel;
using System.Drawing;
using System.Text;
using CommandBars.Controls;
using CommandBars.Model;
using CommandBars.Rendering;
using Xunit;

namespace CommandBars.Tests;

public sealed class ToolbarBorderOptionTests
{
    [Theory]
    [InlineData(CommandBarTheme.Office2003, DockEdge.Top, DockState.Top)]
    [InlineData(CommandBarTheme.Office2003, DockEdge.Left, DockState.Left)]
    [InlineData(CommandBarTheme.Office2007, DockEdge.Bottom, DockState.Bottom)]
    [InlineData(CommandBarTheme.Office2010, DockEdge.Right, DockState.Right)]
    public void AppearanceToggleRepaintsExistingToolbarWithoutChangingLayout(
        CommandBarTheme theme, DockEdge edge, DockState dock)
    {
        using var manager = new CommandBarManager { Theme = theme };
        var bar = manager.AddBar("tools", CommandBarType.Toolbar);
        bar.Dock = dock;
        bar.Items.AddButton(new Command("test") { Text = "Test" });
        using var host = new DockHost { Edge = edge, Size = new Size(500, 500), Manager = manager };
        var control = Assert.Single(host.BarControls);
        control.CreateControl();
        var bounds = control.Bounds;
        Size hostSize = host.Size;
        int invalidated = 0;
        control.Invalidated += (_, _) => invalidated++;
        bool horizontal = edge is DockEdge.Top or DockEdge.Bottom;
        var sample = horizontal ? new Point(control.Width / 2, control.Height - 1)
            : new Point(control.Width - 1, control.Height / 2);
        int EdgeColor()
        {
            using var bitmap = new Bitmap(control.Width, control.Height);
            control.DrawToBitmap(bitmap, control.ClientRectangle);
            return bitmap.GetPixel(sample.X, sample.Y).ToArgb();
        }
        int enabledColor = EdgeColor();
        Assert.Equal(manager.Renderer.Colors.ChevronGradientEnd.ToArgb(), enabledColor);
        var popup = new CommandBarPopupItem("Theme") { ThemeList = true };
        Command BorderCommand()
        {
            manager.PreparePopup(popup);
            var appearance = popup.DropDown.Items.OfType<CommandBarPopupItem>().Single(p => p.Text == "&Appearance");
            return Assert.Single(appearance.DropDown.Items.OfType<CommandBarToggleButton>()).Command;
        }
        Assert.Equal(CommandCheckState.Checked, BorderCommand().Checked);
        Assert.True(BorderCommand().Perform());
        Assert.False(manager.ShowToolbarBorders);
        Assert.True(invalidated > 0);
        Assert.NotEqual(enabledColor, EdgeColor());
        Assert.Equal(CommandCheckState.Unchecked, BorderCommand().Checked);
        Assert.True(BorderCommand().Perform());
        Assert.Equal(enabledColor, EdgeColor());
        Assert.Same(control, Assert.Single(host.BarControls));
        Assert.Equal(bounds, control.Bounds);
        Assert.Equal(hostSize, host.Size);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void PreferenceSurvivesThemesPalettesResetAndLayoutRoundTrip(bool enabled)
    {
        using var manager = new CommandBarManager();
        manager.CaptureDefaults();
        manager.ShowToolbarBorders = enabled;
        manager.Theme = CommandBarTheme.Fluent;
        Assert.True(manager.ResetToDefaults());
        Assert.Equal(enabled, manager.ShowToolbarBorders);
        manager.Theme = CommandBarTheme.Office2003;
        manager.ColorScheme = CommandBarColorScheme.Olive;
        Assert.Equal(enabled, manager.ShowToolbarBorders);
        using var stream = new MemoryStream();
        manager.SaveLayout(stream);
        stream.Position = 0;
        using var restored = new CommandBarManager { ShowToolbarBorders = !enabled };
        restored.LoadLayout(stream);
        Assert.Equal(enabled, restored.ShowToolbarBorders);
    }

    [Fact]
    public void DefaultAndLegacyLayoutsKeepBordersEnabledAndDesignerCanSerializeOptOut()
    {
        using var manager = new CommandBarManager();
        var property = TypeDescriptor.GetProperties(manager)[nameof(manager.ShowToolbarBorders)]!;
        Assert.True(manager.ShowToolbarBorders);
        Assert.False(property.ShouldSerializeValue(manager));
        manager.ShowToolbarBorders = false;
        Assert.True(property.ShouldSerializeValue(manager));
        using var legacy = new MemoryStream(Encoding.UTF8.GetBytes("{\"Version\":2,\"Bars\":[]}"));
        manager.LoadLayout(legacy);
        Assert.True(manager.ShowToolbarBorders);
    }

    [Fact]
    public void OtherThemesDoNotOfferAnInapplicableBorderToggle()
    {
        using var manager = new CommandBarManager { ShowToolbarBorders = false };
        foreach (var theme in new[] { CommandBarTheme.Office97, CommandBarTheme.Office2000,
            CommandBarTheme.OfficeXP, CommandBarTheme.Dark, CommandBarTheme.Fluent, CommandBarTheme.VistaAurora })
        {
            manager.Theme = theme;
            var popup = new CommandBarPopupItem("Theme") { ThemeList = true };
            manager.PreparePopup(popup);
            Assert.DoesNotContain(popup.DropDown.Items.OfType<CommandBarPopupItem>(), p => p.Text == "&Appearance");
            Assert.False(manager.ShowToolbarBorders);
        }
    }
}
