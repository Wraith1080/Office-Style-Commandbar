using System.Drawing;
using CommandBars.Controls;
using CommandBars.Model;
using CommandBars.Rendering;
using Xunit;

namespace CommandBars.Tests;

public sealed class DockHostBorderTests
{
    public static IEnumerable<object[]> DockLayouts()
    {
        foreach (var theme in Enum.GetValues<CommandBarTheme>())
        foreach (var edge in Enum.GetValues<DockEdge>())
        foreach (var layout in new[] { "menus", "toolbars", "mixed" })
            yield return new object[] { theme, edge, layout };
    }

    [Theory]
    [MemberData(nameof(DockLayouts))]
    public void HostHasNoInnerBorderOrExtraClearance(
        CommandBarTheme theme, DockEdge edge, string layout)
    {
        using var manager = new CommandBarManager { Theme = theme };
        var dock = edge switch
        {
            DockEdge.Bottom => DockState.Bottom,
            DockEdge.Left => DockState.Left,
            DockEdge.Right => DockState.Right,
            _ => DockState.Top,
        };
        if (layout != "toolbars")
            for (int i = 0; i < 2; i++)
            {
                var menu = manager.AddBar($"menu{i}", CommandBarType.MenuBar);
                menu.Dock = dock;
                menu.Items.AddPopup("&File");
            }
        if (layout != "menus")
            for (int i = 0; i < 3; i++)
            {
                var toolbar = manager.AddBar($"tools{i}", CommandBarType.Toolbar);
                toolbar.Dock = dock;
                toolbar.Row = i / 2;
                toolbar.Offset = i * 100;
                toolbar.Items.AddButton(new Command($"command{i}") { Text = "Test" });
            }
        using var host = new DockHost
        {
            Edge = edge, Size = new Size(600, 600), Manager = manager,
        };
        bool horizontal = edge is DockEdge.Top or DockEdge.Bottom;
        bool leading = edge is DockEdge.Bottom or DockEdge.Right;
        int clearance = leading
            ? host.BarControls.Min(c => horizontal ? c.Top : c.Left)
            : (horizontal ? host.Height : host.Width) - host.BarControls.Max(c => horizontal ? c.Bottom : c.Right);
        int themeGap = layout == "menus" ? 0
            : leading && layout == "mixed" ? host.Renderer.MenuToToolbarGap : host.Renderer.ToolbarGap;
        Assert.Equal((int)Math.Round(themeGap * host.DeviceDpi / 96f), clearance);
        Assert.All(host.BarControls, c => Assert.True(host.ClientRectangle.Contains(c.Bounds)));

        // Menus remain anchored to the outer edge in collection order.
        if (layout != "toolbars")
        {
            var first = host.BarControls.First();
            Assert.Equal(leading ? (horizontal ? host.Height : host.Width) : 0,
                leading ? (horizontal ? first.Bottom : first.Right) : (horizontal ? first.Top : first.Left));
            Assert.Equal(host.RectangleToScreen(first.Bounds),
                host.ComputeBarDockPreview(Point.Empty, Size.Empty, first.Bar!));
        }
        foreach (var toolbar in host.BarControls.Where(c => !c.Stretch))
        {
            Point center = host.PointToScreen(new Point(
                toolbar.Left + toolbar.Width / 2, toolbar.Top + toolbar.Height / 2));
            Rectangle preview = host.RectangleToClient(host.ComputeBarDockPreview(center, toolbar.Size, toolbar.Bar!));
            Assert.Equal(horizontal ? toolbar.Top : toolbar.Left, horizontal ? preview.Top : preview.Left);
            Assert.Equal(horizontal ? toolbar.Height : toolbar.Width, horizontal ? preview.Height : preview.Width);
        }

        // Repeated layout must not keep adding pixels or move persisted offsets.
        var bounds = host.BarControls.Select(c => c.Bounds).ToArray();
        var offsets = manager.Bars.Select(b => b.Offset).ToArray();
        Size size = host.Size;
        host.PerformLayout();
        host.Rebuild();
        Assert.Equal(size, host.Size);
        Assert.Equal(bounds, host.BarControls.Select(c => c.Bounds));
        Assert.Equal(offsets, manager.Bars.Select(b => b.Offset));

        // Unoccupied host pixels must match its band, even on the inner edge.
        using var bitmap = new Bitmap(host.Width, host.Height);
        host.DrawToBitmap(bitmap, host.ClientRectangle);
        if (theme is CommandBarTheme.Office2003 or CommandBarTheme.Office2007 or CommandBarTheme.Office2010)
            foreach (var toolbar in host.BarControls.Where(c => !c.Stretch))
            {
                int radius = (int)Math.Round(3 * host.DeviceDpi / 96f);
                // Covers both the body and the options nub in the composed control.
                for (int p = radius; p < (horizontal ? toolbar.Width : toolbar.Height) - radius; p++)
                    Assert.Equal(host.Renderer.Colors.ChevronGradientEnd.ToArgb(), bitmap.GetPixel(
                        horizontal ? toolbar.Left + p : toolbar.Right - 1,
                        horizontal ? toolbar.Bottom - 1 : toolbar.Top + p).ToArgb());
            }
        using var band = new Bitmap(host.Width, host.Height);
        using (var graphics = Graphics.FromImage(band))
            host.Renderer.DrawBand(graphics, host.ClientRectangle,
                horizontal ? BarOrientation.Horizontal : BarOrientation.Vertical);
        int checkedPixels = 0;
        for (int i = 0; i < (horizontal ? host.Width : host.Height); i++)
        {
            int x = horizontal ? i : leading ? 0 : host.Width - 1;
            int y = horizontal ? leading ? 0 : host.Height - 1 : i;
            if (host.BarControls.Any(c => c.Bounds.Contains(x, y))) continue;
            Assert.Equal(band.GetPixel(x, y).ToArgb(), bitmap.GetPixel(x, y).ToArgb());
            checkedPixels++;
        }
        if (layout == "toolbars") Assert.True(checkedPixels > 0);
    }
}
