using System.Drawing;
using System.Reflection;
using System.Windows.Forms;
using CommandBars.Controls;
using CommandBars.Model;
using CommandBars.Rendering;
using Xunit;

namespace CommandBars.Tests;

public sealed class FluentAccentPaletteTests
{
    [Theory]
    [InlineData(FluentBasePalette.CoolBlue)]
    [InlineData(FluentBasePalette.Mint)]
    [InlineData(FluentBasePalette.Rose)]
    [InlineData(FluentBasePalette.Lavender)]
    [InlineData(FluentBasePalette.Custom)]
    public void ColoredBasesOfferSixDistinctHueHarmonies(FluentBasePalette palette)
    {
        var options = new FluentColorOptions(palette, Color.FromArgb(180, 100, 30));
        var accents = FluentAccentPalettes.ForBase(options);
        Assert.Equal(6, accents.Count);
        Assert.Equal(6, accents.Select(a => a.Color.ToArgb()).Distinct().Count());
        Assert.Equal(6, accents.Select(a => a.Key).Distinct().Count());
        var expectedAngles = new Dictionary<string, double>
        {
            ["tonal"] = 0, ["analogous-left"] = 330, ["analogous-right"] = 30,
            ["complementary"] = 180, ["split-left"] = 150, ["split-right"] = 210,
        };
        foreach (var accent in accents)
        {
            double angle = (accent.Color.GetHue() - options.SurfaceSeed.GetHue() + 360) % 360;
            double error = Math.Abs(angle - expectedAngles[accent.Key]);
            Assert.True(Math.Min(error, 360 - error) < 1, accent.ToString());
            Assert.Equal(255, accent.Color.A);
        }
        var stronger = FluentAccentPalettes.ForBase(new(palette, options.BaseColor, tintStrength: 100));
        Assert.Equal(accents.Select(a => a.Color), stronger.Select(a => a.Color));
    }

    [Fact]
    public void NeutralIsUnchangedAndAchromaticCustomGetsNeutralPairings()
    {
        Assert.Empty(FluentAccentPalettes.ForBase(new()));
        foreach (Color seed in new[] { Color.Black, Color.White, Color.Gray })
        {
            var choices = FluentAccentPalettes.ForBase(new(FluentBasePalette.Custom, seed));
            Assert.Equal(6, choices.Count);
            Assert.All(choices, c => Assert.Equal("Neutral pairing", c.Harmony));
        }
    }

    [Fact]
    public void SuggestionsRespectContrastAcrossTintStrengths()
    {
        static double L(Color c)
        {
            double Channel(byte v) { double x = v / 255d; return x <= .04045 ? x / 12.92 : Math.Pow((x + .055) / 1.055, 2.4); }
            return .2126 * Channel(c.R) + .7152 * Channel(c.G) + .0722 * Channel(c.B);
        }
        foreach (var palette in Enum.GetValues<FluentBasePalette>())
        foreach (Color seed in new[] { Color.Black, Color.White, Color.Yellow, Color.Blue })
        foreach (int strength in new[] { 0, 50, 100 })
        foreach (var choice in FluentAccentPalettes.ForBase(new(palette, seed)))
        {
            var colors = new FluentColorTable(CommandBarColorScheme.Default, new(palette, seed, choice.Color, strength));
            foreach (Color surface in new[] { colors.BandGradientBegin, colors.ButtonCheckedBegin })
                Assert.True((L(surface) + .05) / (L(colors.Accent) + .05) >= 3, $"{palette}: {choice}");
        }
    }

    [Fact]
    public void MenuSelectionPersistsAsRgbWithoutChangingTheBase()
    {
        using var manager = new CommandBarManager { Theme = CommandBarTheme.Fluent, FluentBasePalette = FluentBasePalette.CoolBlue };
        var popup = new CommandBarPopupItem("Theme") { ThemeList = true };
        CommandBarPopupItem Prepare()
        {
            typeof(CommandBarManager).GetMethod("PreparePopup", BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(manager, new object[] { popup });
            return popup.DropDown.Items.OfType<CommandBarPopupItem>().Single(p => p.Text == "&Accent color");
        }
        var accents = Prepare();
        Assert.Equal(10, accents.DropDown.Items.OfType<CommandBarToggleButton>().Count());
        var copper = accents.DropDown.Items.OfType<CommandBarToggleButton>().Single(t => t.Command.Id == "fluent-accent:complementary");
        int notifications = 0;
        manager.ThemeChanged += (_, _) => notifications++;
        Assert.True(copper.Command.Perform());
        Assert.Equal(1, notifications);
        Assert.Equal(FluentBasePalette.CoolBlue, manager.FluentBasePalette);
        Assert.Equal("fluent-accent:complementary", Assert.Single(Prepare().DropDown.Items.OfType<CommandBarToggleButton>(),
            t => t.Command.Checked == CommandCheckState.Checked).Command.Id);
        Color selected = manager.FluentAccentColor;
        using var stream = new MemoryStream();
        manager.SaveLayout(stream); stream.Position = 0;
        using var loaded = new CommandBarManager();
        loaded.LoadLayout(stream);
        Assert.Equal(selected.ToArgb(), loaded.FluentAccentColor.ToArgb());
        manager.FluentBasePalette = FluentBasePalette.Mint;
        Assert.Equal(selected, manager.FluentAccentColor);
        manager.FluentBasePalette = FluentBasePalette.Neutral;
        Assert.Equal(4, Prepare().DropDown.Items.OfType<CommandBarToggleButton>().Count());
    }

    [Fact]
    public void DialogSuggestionsPreviewAndRefreshWithoutApplyingOnCancel()
    {
        Exception? failure = null;
        var thread = new Thread(() =>
        {
            try
            {
                using var manager = new CommandBarManager { Theme = CommandBarTheme.Fluent, FluentBasePalette = FluentBasePalette.CoolBlue };
                using var dialog = new FluentColorDialog(manager.ColorScheme, manager.GetFluentColors());
                IEnumerable<Control> All(Control parent) => parent.Controls.Cast<Control>().SelectMany(c => new[] { c }.Concat(All(c)));
                var controls = All(dialog).ToArray();
                var suggestions = controls.OfType<ComboBox>().Single(c => c.AccessibleName == "Suggested accent");
                var palette = controls.OfType<ComboBox>().Single(c => c != suggestions);
                var accentHex = controls.OfType<TextBox>().Single(c => c.AccessibleName == "Accent color HEX");
                var followScheme = controls.OfType<CheckBox>().Single();
                dialog.Show(); Application.DoEvents();
                Assert.Equal(7, suggestions.Items.Count);
                suggestions.SelectedIndex = 5;
                var selected = Assert.IsType<FluentAccentChoice>(suggestions.SelectedItem);
                Assert.Equal(selected.Color, dialog.SelectedColors.AccentColor);
                Assert.False(followScheme.Checked);
                Assert.True(manager.FluentAccentColor.IsEmpty);
                accentHex.Text = "invalid";
                Assert.True(suggestions.Enabled);
                suggestions.SelectedIndex = 4;
                Assert.Equal("complementary", Assert.IsType<FluentAccentChoice>(suggestions.SelectedItem).Key);
                palette.SelectedIndex = (int)FluentBasePalette.Mint;
                Assert.Equal("Jade", Assert.IsType<FluentAccentChoice>(suggestions.Items[1]).Name);
                suggestions.SelectedIndex = 6;
                string? output = Environment.GetEnvironmentVariable("COMMANDBARS_ACCENT_PREVIEW");
                if (!string.IsNullOrEmpty(output))
                {
                    dialog.PerformLayout();
                    using var bitmap = new Bitmap(dialog.Width, dialog.Height);
                    dialog.DrawToBitmap(bitmap, new Rectangle(Point.Empty, bitmap.Size));
                    bitmap.Save(output);
                }
                palette.SelectedIndex = (int)FluentBasePalette.Neutral;
                Assert.False(suggestions.Visible);
                Assert.Single(suggestions.Items.Cast<object>());
                controls.OfType<Button>().Single(b => b.Text == "Cancel").PerformClick();
                Assert.Equal(DialogResult.Cancel, dialog.DialogResult);
                Assert.True(manager.FluentAccentColor.IsEmpty);
                Assert.Equal(FluentBasePalette.CoolBlue, manager.FluentBasePalette);
            }
            catch (Exception ex) { failure = ex; }
        });
        thread.SetApartmentState(ApartmentState.STA); thread.Start(); thread.Join();
        if (failure is not null) throw failure;
    }
}
