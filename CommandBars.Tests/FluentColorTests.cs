using System.Drawing;
using System.Reflection;
using System.Text;
using System.Windows.Forms;
using CommandBars.Controls;
using CommandBars.Model;
using CommandBars.Rendering;
using Xunit;

namespace CommandBars.Tests;

public sealed class FluentColorTests
{
    [Fact]
    public void BaseAndAccentAreIndependentAndDefaultsArePreserved()
    {
        var neutral = new FluentColorTable(CommandBarColorScheme.Teal);
        var mint = new FluentColorTable(CommandBarColorScheme.Teal, new(FluentBasePalette.Mint));
        Assert.Equal(Color.FromArgb(238, 238, 238), neutral.BandGradientBegin);
        Assert.Equal(Color.FromArgb(248, 248, 248), neutral.BarGradientBegin);
        Assert.Equal(neutral.Accent, mint.Accent);
        Assert.NotEqual(neutral.BandGradientBegin, mint.BandGradientBegin);
        Assert.NotEqual(neutral.MenuBackground, mint.MenuBackground);
        var zero = new FluentColorTable(CommandBarColorScheme.Teal, new(FluentBasePalette.Mint, tintStrength: 0));
        foreach (var property in typeof(CommandBarColorTable).GetProperties())
            Assert.Equal(property.GetValue(neutral), property.GetValue(zero));
    }

    [Fact]
    public void CustomColorsRoundTripResetAndStayDormantOnOtherThemes()
    {
        using var manager = new CommandBarManager { Theme = CommandBarTheme.Fluent };
        manager.CaptureDefaults();
        int events = 0;
        manager.ThemeChanged += (_, _) => events++;
        var options = new FluentColorOptions(FluentBasePalette.Custom, Color.FromArgb(20, 120, 150), Color.FromArgb(130, 30, 60), 75);
        manager.SetFluentColors(options);
        Assert.Equal(1, events);
        Assert.True(manager.ResetToDefaults());
        Assert.Equal(options.BaseColor, manager.FluentBaseColor);
        Assert.Equal(75, manager.FluentTintStrength);
        manager.Theme = CommandBarTheme.Dark;
        var dark = manager.Renderer;
        manager.FluentTintStrength = 65;
        Assert.Same(dark, manager.Renderer);
        using var stream = new MemoryStream();
        manager.SaveLayout(stream); stream.Position = 0;
        using var restored = new CommandBarManager();
        restored.LoadLayout(stream);
        Assert.Equal(CommandBarTheme.Dark, restored.Theme);
        restored.Theme = CommandBarTheme.Fluent;
        Assert.Equal(options.BaseColor.ToArgb(), restored.FluentBaseColor.ToArgb());
        Assert.Equal(options.AccentColor.ToArgb(), restored.FluentAccentColor.ToArgb());
        Assert.Equal(65, restored.FluentTintStrength);
        Assert.Equal(FluentBasePalette.Custom, restored.FluentBasePalette);
        using var legacy = new MemoryStream(Encoding.UTF8.GetBytes("{\"Version\":2,\"ThemeKey\":\"fluent\"}"));
        restored.LoadLayout(legacy);
        Assert.Equal(FluentBasePalette.Neutral, restored.FluentBasePalette);
        Assert.True(restored.FluentAccentColor.IsEmpty);
        Assert.Equal(50, restored.FluentTintStrength);
    }

    [Fact]
    public void InvalidSettingsAreRejectedAndBadLayoutsFallBack()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new FluentColorOptions(tintStrength: 101));
        Assert.Throws<ArgumentException>(() => new FluentColorOptions(baseColor: Color.Transparent));
        using var manager = new CommandBarManager();
        using var stream = new MemoryStream(Encoding.UTF8.GetBytes("{\"Version\":2,\"ThemeKey\":\"fluent\",\"FluentBasePalette\":\"Future\",\"FluentBaseColor\":123,\"FluentTintStrength\":200}"));
        manager.LoadLayout(stream);
        Assert.Equal(FluentBasePalette.Neutral, manager.FluentBasePalette);
        Assert.True(manager.FluentBaseColor.IsEmpty);
        Assert.Equal(100, manager.FluentTintStrength);
    }

    [Fact]
    public void ExtremeCustomColorsKeepTextAndAccentReadable()
    {
        double L(Color c)
        {
            double C(byte v) { double x = v / 255d; return x <= .04045 ? x / 12.92 : Math.Pow((x + .055) / 1.055, 2.4); }
            return .2126 * C(c.R) + .7152 * C(c.G) + .0722 * C(c.B);
        }
        foreach (Color seed in new[] { Color.Black, Color.White, Color.Yellow, Color.Blue, Color.Red })
        foreach (Color accent in new[] { Color.White, Color.Yellow, Color.Black, Color.Cyan })
        {
            var colors = new FluentColorTable(CommandBarColorScheme.Default, new(FluentBasePalette.Custom, seed, accent, 100));
            foreach (Color surface in new[] { colors.BandGradientBegin, colors.BarGradientBegin, colors.MenuBackground, colors.ButtonPressedBegin })
                Assert.True((L(surface) + .05) / (L(colors.Text) + .05) >= 4.5);
            Assert.True((L(colors.ButtonCheckedBegin) + .05) / (L(colors.Accent) + .05) >= 3);
        }
    }

    [Fact]
    public void FluentMenuOffersBaseChoicesAndAccentSelectionClearsOverride()
    {
        using var manager = new CommandBarManager { Theme = CommandBarTheme.Fluent, FluentAccentColor = Color.Red };
        var popup = new CommandBarPopupItem("Theme") { ThemeList = true };
        typeof(CommandBarManager).GetMethod("PreparePopup", BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(manager, new object[] { popup });
        var bases = popup.DropDown.Items.OfType<CommandBarPopupItem>().Single(p => p.Text == "&Base color");
        Assert.Equal(5, bases.DropDown.Items.OfType<CommandBarToggleButton>().Count());
        var accents = popup.DropDown.Items.OfType<CommandBarPopupItem>().Single(p => p.Text == "&Accent color");
        var choice = accents.DropDown.Items.OfType<CommandBarToggleButton>().First();
        Assert.True(choice.Command.Perform());
        Assert.True(manager.FluentAccentColor.IsEmpty);
    }

    [Fact]
    public void DialogValidatesHexAndPreviewDoesNotMutateManager()
    {
        Exception? failure = null;
        var thread = new Thread(() =>
        {
            try
            {
                using var manager = new CommandBarManager { Theme = CommandBarTheme.Fluent };
                using var dialog = new FluentColorDialog(manager.ColorScheme, new(FluentBasePalette.Custom));
                IEnumerable<Control> All(Control parent) => parent.Controls.Cast<Control>().SelectMany(c => new[] { c }.Concat(All(c)));
                var controls = All(dialog).ToArray();
                var baseHex = controls.OfType<TextBox>().Single(c => c.AccessibleName == "Base color HEX");
                var ok = controls.OfType<Button>().Single(c => c.Text == "OK");
                baseHex.Text = "invalid";
                Assert.False(ok.Enabled);
                baseHex.Text = "#349899";
                Assert.True(ok.Enabled);
                Assert.Equal(Color.FromArgb(52, 152, 153), dialog.SelectedColors.BaseColor);
                Assert.Equal(FluentBasePalette.Neutral, manager.FluentBasePalette);
                dialog.Show();
                Application.DoEvents();
                dialog.CreateControl();
                dialog.PerformLayout();
                using var bitmap = new Bitmap(dialog.Width, dialog.Height);
                dialog.DrawToBitmap(bitmap, new Rectangle(Point.Empty, bitmap.Size));
                string? output = Environment.GetEnvironmentVariable("COMMANDBARS_COLOR_PREVIEW");
                if (!string.IsNullOrEmpty(output)) bitmap.Save(output);
            }
            catch (Exception ex) { failure = ex; }
        });
        thread.SetApartmentState(ApartmentState.STA); thread.Start(); thread.Join();
        if (failure is not null) throw failure;
    }
}
