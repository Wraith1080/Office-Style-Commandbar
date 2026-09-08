using System.ComponentModel;
using System.Drawing;
using System.Reflection;
using System.Text;
using CommandBars.Controls;
using CommandBars.Model;
using CommandBars.Rendering;
using Xunit;

namespace CommandBars.Tests;

public class ColorSchemeTests
{
    [Fact]
    public void DefaultPalettesAndDarkRemainUnchanged()
    {
        foreach (var theme in Enum.GetValues<CommandBarTheme>())
        {
            var original = ThemeRenderer.Create(theme).Colors;
            var explicitDefault = ThemeRenderer.Create(theme, CommandBarColorScheme.Default).Colors;
            foreach (var property in typeof(CommandBarColorTable).GetProperties())
                Assert.Equal(property.GetValue(original), property.GetValue(explicitDefault));
        }
        foreach (var scheme in Enum.GetValues<CommandBarColorScheme>())
            Assert.Equal(new DarkRenderer().Colors.BarGradientBegin,
                ThemeRenderer.Create(CommandBarTheme.Dark, scheme).Colors.BarGradientBegin);
    }

    [Fact]
    public void SwitchingUpdatesHostsAndRetainsPreferenceAcrossDark()
    {
        using var manager = new CommandBarManager();
        using var host = new DockHost { Manager = manager };
        int notifications = 0;
        manager.ThemeChanged += (_, _) => notifications++;
        var original = manager.Renderer.Colors;
        manager.ColorScheme = CommandBarColorScheme.Olive;
        Assert.Same(manager.Renderer, host.Renderer);
        Assert.NotEqual(original.BarGradientEnd, host.Renderer.Colors.BarGradientEnd);
        Assert.Equal(original.ButtonHotBegin, host.Renderer.Colors.ButtonHotBegin);
        Assert.Equal(host.Renderer.Colors.MenuBarGradientEnd, host.Renderer.DialogColors.Window);
        manager.Theme = CommandBarTheme.Dark;
        Assert.Equal(CommandBarColorScheme.Default, manager.EffectiveColorScheme);
        Assert.Single(manager.AvailableColorSchemes);
        manager.Theme = CommandBarTheme.Office2003;
        Assert.Equal(CommandBarColorScheme.Olive, manager.EffectiveColorScheme);
        Assert.Equal(3, notifications);
    }

    [Fact]
    public void LayoutRoundTripLegacyFallbackAndReset()
    {
        using var manager = new CommandBarManager();
        manager.CaptureDefaults();
        manager.Theme = CommandBarTheme.Office2007;
        manager.ColorScheme = CommandBarColorScheme.Silver;
        Assert.True(manager.ResetToDefaults());
        Assert.Equal(CommandBarColorScheme.Silver, manager.ColorScheme);
        using var stream = new MemoryStream();
        manager.SaveLayout(stream);
        stream.Position = 0;
        using var restored = new CommandBarManager();
        restored.LoadLayout(stream);
        Assert.Equal(CommandBarTheme.Office2007, restored.Theme);
        Assert.Equal(CommandBarColorScheme.Silver, restored.ColorScheme);
        foreach (string suffix in new[] { "", ",\"ColorScheme\":\"future-palette\"" })
        {
            using var legacy = new MemoryStream(Encoding.UTF8.GetBytes("{\"Version\":2,\"ThemeKey\":\"office2003\"" + suffix + "}"));
            restored.LoadLayout(legacy);
            Assert.Equal(CommandBarColorScheme.Default, restored.ColorScheme);
            Assert.Equal(new Office2003ColorTable().BarGradientEnd, restored.Renderer.Colors.BarGradientEnd);
        }
    }

    [Fact]
    public void MenuAndDesignerOfferSupportedSchemes()
    {
        using var manager = new CommandBarManager();
        var popup = new CommandBarPopupItem("Theme") { ThemeList = true };
        void Prepare() => typeof(CommandBarManager).GetMethod("PreparePopup", BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(manager, new object[] { popup });
        Prepare();
        var submenu = Assert.Single(popup.DropDown.Items.OfType<CommandBarPopupItem>());
        var olive = submenu.DropDown.Items.OfType<CommandBarToggleButton>().Single(t => t.Command.Id == "color-scheme:Olive");
        Assert.True(olive.Command.Perform());
        Assert.Equal(CommandBarColorScheme.Olive, manager.ColorScheme);
        Prepare();
        submenu = Assert.Single(popup.DropDown.Items.OfType<CommandBarPopupItem>());
        Assert.Single(submenu.DropDown.Items.OfType<CommandBarToggleButton>(), t => t.Command.Checked == CommandCheckState.Checked);
        var property = TypeDescriptor.GetProperties(manager)[nameof(manager.ColorScheme)]!;
        Assert.IsType<CommandBarColorSchemeConverter>(property.Converter);
        Assert.True(property.ShouldSerializeValue(manager));
        manager.Theme = CommandBarTheme.Dark;
        Prepare();
        Assert.Empty(popup.DropDown.Items.OfType<CommandBarPopupItem>());
    }

    [Fact]
    public void CustomFactoriesAreNotReplacedByPaletteChanges()
    {
        using var manager = new CommandBarManager();
        var custom = new OfficeXPRenderer();
        manager.RegisterTheme(CommandBarThemeKeys.Office2003, "Custom", () => custom);
        manager.ColorScheme = CommandBarColorScheme.Olive;
        Assert.Same(custom, manager.Renderer);
        Assert.Single(manager.AvailableColorSchemes);
        manager.ClearThemes();
        manager.ColorScheme = CommandBarColorScheme.Silver;
        Assert.Same(custom, manager.Renderer);
        manager.Theme = CommandBarTheme.Office2003;
        manager.ColorScheme = CommandBarColorScheme.Olive;
        Assert.Equal(CommandBarColorScheme.Olive, manager.EffectiveColorScheme);
    }

    [Theory]
    [InlineData(1f)]
    [InlineData(1.5f)]
    [InlineData(2f)]
    public void PalettesPaintAcrossDpiWithoutChangingGeometry(float scale)
    {
        using var bitmap = new Bitmap(240, 80);
        using var graphics = Graphics.FromImage(bitmap);
        foreach (var theme in Enum.GetValues<CommandBarTheme>())
        foreach (var scheme in CommandBarColorSchemes.ForTheme(theme))
        {
            var renderer = ThemeRenderer.Create(theme, scheme);
            renderer.Scale = scale;
            var baseline = ThemeRenderer.Create(theme);
            baseline.Scale = scale;
            Assert.Equal(baseline.GripperExtent, renderer.GripperExtent);
            Assert.Equal(baseline.ChevronExtent, renderer.ChevronExtent);
            renderer.DrawBand(graphics, new Rectangle(0, 0, 240, 80), BarOrientation.Horizontal);
            renderer.DrawButton(graphics, new Rectangle(5, 5, 50, 40), RenderState.Hot, BarOrientation.Horizontal);
            Assert.Equal(255, bitmap.GetPixel(200, 40).A);
        }
    }
}
