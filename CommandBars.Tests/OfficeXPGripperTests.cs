using System.Drawing;
using System.Reflection;
using CommandBars.Controls;
using CommandBars.Model;
using CommandBars.Rendering;
using Xunit;

namespace CommandBars.Tests;

public class OfficeXPGripperTests
{
    [Fact]
    public void OptionUpdatesHostsAndSurvivesSchemesThemesResetAndLayouts()
    {
        using var manager = new CommandBarManager { Theme = CommandBarTheme.OfficeXP };
        using var host = new DockHost { Manager = manager };
        Assert.False(((OfficeXPRenderer)manager.Renderer).UseMultiStripGripper);
        manager.CaptureDefaults();
        var menu = new CommandBarPopupItem("Theme") { ThemeList = true };
        void Prepare() => typeof(CommandBarManager).GetMethod("PreparePopup", BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(manager, new object[] { menu });
        Prepare();
        var toggle = Assert.Single(menu.DropDown.Items.OfType<CommandBarToggleButton>(), t => t.Command.Id == "officexp:multi-strip-gripper");
        Assert.True(toggle.Command.Perform());
        Assert.True(manager.UseOfficeXPMultiStripGripper);
        Assert.Same(manager.Renderer, host.Renderer);
        Assert.True(((OfficeXPRenderer)host.Renderer).UseMultiStripGripper);
        Prepare();
        Assert.Equal(CommandCheckState.Checked, menu.DropDown.Items.OfType<CommandBarToggleButton>().Single(t => t.Command.Id == "officexp:multi-strip-gripper").Command.Checked);
        manager.ColorScheme = CommandBarColorScheme.Olive;
        Assert.True(((OfficeXPRenderer)manager.Renderer).UseMultiStripGripper);
        manager.Theme = CommandBarTheme.Office2003;
        Prepare();
        Assert.DoesNotContain(menu.DropDown.Items.OfType<CommandBarToggleButton>(), t => t.Command.Id == "officexp:multi-strip-gripper");
        manager.Theme = CommandBarTheme.OfficeXP;
        Assert.True(manager.ResetToDefaults());
        Assert.True(manager.UseOfficeXPMultiStripGripper);
        Assert.True(((OfficeXPRenderer)manager.Renderer).UseMultiStripGripper);
        using var stream = new MemoryStream();
        manager.SaveLayout(stream);
        stream.Position = 0;
        using var restored = new CommandBarManager();
        restored.LoadLayout(stream);
        Assert.True(((OfficeXPRenderer)restored.Renderer).UseMultiStripGripper);
        restored.UseOfficeXPMultiStripGripper = false;
        Assert.False(((OfficeXPRenderer)restored.Renderer).UseMultiStripGripper);
    }

    [Theory]
    [InlineData(1f)]
    [InlineData(1.25f)]
    [InlineData(1.5f)]
    [InlineData(2f)]
    public void StripsAreEmbossedSeparatedAndTransposeWithinBounds(float scale)
    {
        var renderer = new OfficeXPRenderer(CommandBarColorScheme.Default, true) { Scale = scale };
        int R(int value) => (int)Math.Round(value * scale);
        using var horizontal = new Bitmap(R(40), R(40));
        using var vertical = new Bitmap(R(40), R(40));
        using (var g = Graphics.FromImage(horizontal))
            renderer.DrawGripper(g, new Rectangle(2, 2, renderer.GripperExtent, R(30)), BarOrientation.Horizontal);
        using (var g = Graphics.FromImage(vertical))
            renderer.DrawGripper(g, new Rectangle(2, 2, R(30), renderer.GripperExtent), BarOrientation.Vertical);
        int darkRows = 0;
        bool previousDark = false;
        for (int y = 0; y < horizontal.Height; y++)
        {
            int darkPixels = 0;
            for (int x = 0; x < horizontal.Width; x++)
            {
                var pixel = horizontal.GetPixel(x, y);
                Assert.Equal(pixel, vertical.GetPixel(y, x));
                if (pixel.ToArgb() == renderer.Colors.GripperDark.ToArgb()) darkPixels++;
                if (x < 2 || x >= 2 + renderer.GripperExtent || y < 2 || y >= 2 + R(30)) Assert.Equal(0, pixel.A);
            }
            if (darkPixels > 0)
            {
                Assert.Equal(R(4), darkPixels);
                if (!previousDark) darkRows++;
            }
            previousDark = darkPixels > 0;
        }
        Assert.True(darkRows >= 4);
        Assert.Contains(Enumerable.Range(0, horizontal.Width).SelectMany(x => Enumerable.Range(0, horizontal.Height).Select(y => horizontal.GetPixel(x, y).ToArgb())), c => c == renderer.Colors.GripperLight.ToArgb());
    }
}
