using System.Drawing;
using System.Drawing.Drawing2D;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using CommandBars.Controls;
using CommandBars.Model;
using CommandBars.Rendering;
using Xunit;

namespace CommandBars.Tests;

public sealed class VistaAuroraTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void FloatingFramesRestoreRoundedCornersIndependentlyOfMenus(bool tearOff)
    {
        var renderer = new VistaAuroraRenderer();
        Assert.Equal(4, renderer.FloatingCornerRadius);
        Assert.Equal(3, renderer.PopupCornerRadius);
        var bar = new CommandBar("floating", CommandBarType.Toolbar) { Dock = DockState.Floating };
        bar.Items.AddButton(new Command("open") { Text = "Open" });
        using var host = new DockHost();
        using Form window = tearOff ? new TearOffWindow(bar, bar, renderer, null, null)
            : new FloatingWindow(bar, renderer, host, null);
        _ = window.Handle;
        if (OperatingSystem.IsWindowsVersionAtLeast(10, 0, 22000))
        {
            Assert.Null(window.Region);
            Assert.Equal(0, DwmGetWindowAttribute(window.Handle, 33, out int preference, sizeof(int)));
            Assert.Equal(3, preference);
            return;
        }
        IntPtr handle = CreateRectRgn(0, 0, 0, 0);
        try
        {
            Assert.NotEqual(0, GetWindowRgn(window.Handle, handle));
            using var actual = Region.FromHrgn(handle);
            using var previous = RoundedSurface.CreateRegion(window.ClientRectangle, 4 * renderer.Scale);
            using var popup = renderer.CreatePopupRegion(window.ClientRectangle);
            using var bitmap = new Bitmap(1, 1);
            using var g = Graphics.FromImage(bitmap);
            Assert.True(actual.Equals(previous, g));
            Assert.False(actual.Equals(popup!, g));
        }
        finally { DeleteObject(handle); }
    }

    [Theory]
    [InlineData(1f)]
    [InlineData(1.25f)]
    [InlineData(1.5f)]
    [InlineData(2f)]
    public void ActualPopupWindowKeepsThemeRegionAndDisablesTheDwmPreset(float scale)
    {
        var renderer = new VistaAuroraRenderer { Scale = scale };
        var bar = new CommandBar("menu", CommandBarType.Popup);
        bar.Items.AddButton(new Command("open") { Text = "Open" });
        using var popup = new CommandBarPopupWindow(bar, renderer, SystemFonts.MenuFont!, 16, scale);
        _ = popup.Handle;
        VerifyWindowRegion();
        using var largerFont = new Font("Segoe UI", 18);
        popup.Font = largerFont;
        popup.PerformLayout();
        VerifyWindowRegion();
        if (OperatingSystem.IsWindowsVersionAtLeast(10, 0, 22000))
        {
            Assert.Equal(0, DwmGetWindowAttribute(popup.Handle, 33, out int preference, sizeof(int)));
            Assert.Equal(1, preference); // DONOTROUND: the explicit region supplies the panel's corners.
            using var fluent = new CommandBarPopupWindow(bar, new FluentRenderer(), SystemFonts.MenuFont!, 16, scale);
            Assert.Equal(0, DwmGetWindowAttribute(fluent.Handle, 33, out preference, sizeof(int)));
            Assert.Equal(3, preference); // Fluent still opts into the native small-corner preset.
        }

        void VerifyWindowRegion()
        {
            IntPtr handle = CreateRectRgn(0, 0, 0, 0);
            try
            {
                Assert.NotEqual(0, GetWindowRgn(popup.Handle, handle));
                using var actual = Region.FromHrgn(handle);
                using var expected = renderer.CreatePopupRegion(popup.ClientRectangle);
                using var bitmap = new Bitmap(1, 1);
                using var g = Graphics.FromImage(bitmap);
                Assert.True(actual.Equals(expected!, g));
            }
            finally { DeleteObject(handle); }
        }
    }

    [DllImport("gdi32.dll")] private static extern IntPtr CreateRectRgn(int left, int top, int right, int bottom);
    [DllImport("user32.dll")] private static extern int GetWindowRgn(IntPtr window, IntPtr region);
    [DllImport("gdi32.dll")] private static extern bool DeleteObject(IntPtr value);
    [DllImport("dwmapi.dll")] private static extern int DwmGetWindowAttribute(IntPtr window, int attribute, out int value, int size);

    [Theory]
    [InlineData(1f)]
    [InlineData(1.25f)]
    [InlineData(1.5f)]
    [InlineData(2f)]
    public void PopupPanelCornersAreIndependentOfSelectionCorners(float scale)
    {
        var renderer = new VistaAuroraRenderer { Scale = scale };
        Assert.Equal(3, renderer.PopupCornerRadius);
        var bounds = new Rectangle(0, 0, (int)Math.Round(40 * scale), (int)Math.Round(24 * scale));
        using var region = renderer.CreatePopupRegion(bounds);
        Assert.NotNull(region); // Windows 11 must not substitute DWM's larger preset.
        using var sharper = RoundedSurface.CreateRegion(bounds, 1 * scale);
        Assert.True(Enumerable.Range(0, bounds.Width).Count(x => region!.IsVisible(x, 0))
            < Enumerable.Range(0, bounds.Width).Count(x => sharper.IsVisible(x, 0)));

        using var hover = new Bitmap(bounds.Width, bounds.Height);
        using var check = new Bitmap(bounds.Width, bounds.Height);
        using (var g = Graphics.FromImage(hover)) renderer.DrawMenuItemBackground(g, bounds, RenderState.Hot);
        using (var g = Graphics.FromImage(check)) renderer.DrawMenuIconFrame(g, bounds, RenderState.Checked);
        for (int y = 0; y < bounds.Height; y++)
        for (int x = 0; x < bounds.Width; x++)
            Assert.Equal(hover.GetPixel(x, y), check.GetPixel(x, y));
        Assert.True(hover.GetPixel(bounds.Width / 2, bounds.Height / 2).A > 0);
    }

    [Theory]
    [InlineData(1f)]
    [InlineData(1.25f)]
    [InlineData(1.5f)]
    [InlineData(2f)]
    public void ButtonHighlightsHaveCrossAxisClearance(float scale)
    {
        var renderer = new VistaAuroraRenderer { Scale = scale };
        int R(int value) => (int)Math.Round(value * scale);
        foreach (var orientation in new[] { BarOrientation.Horizontal, BarOrientation.Vertical })
        foreach (var state in new[] { RenderState.Hot, RenderState.Pressed, RenderState.Checked })
        {
            using var bitmap = new Bitmap(R(36), R(36));
            using var g = Graphics.FromImage(bitmap);
            g.Clear(Color.Magenta);
            renderer.DrawButton(g, new Rectangle(Point.Empty, bitmap.Size), state, orientation);
            int crossInset = R(2);
            for (int cross = 0; cross < crossInset; cross++)
            for (int main = 0; main < bitmap.Width; main++)
            {
                Assert.Equal(Color.Magenta.ToArgb(), (orientation == BarOrientation.Horizontal
                    ? bitmap.GetPixel(main, cross) : bitmap.GetPixel(cross, main)).ToArgb());
                Assert.Equal(Color.Magenta.ToArgb(), (orientation == BarOrientation.Horizontal
                    ? bitmap.GetPixel(main, bitmap.Height - cross - 1) : bitmap.GetPixel(bitmap.Width - cross - 1, main)).ToArgb());
            }
            Assert.NotEqual(Color.Magenta.ToArgb(), bitmap.GetPixel(bitmap.Width / 2, bitmap.Height / 2).ToArgb());
        }
    }

    [Theory]
    [InlineData(1f)]
    [InlineData(1.25f)]
    [InlineData(1.5f)]
    [InlineData(2f)]
    public void ComboCornersMatchPopupAndArrowHasOnlyRoundedOuterCorners(float scale)
    {
        var renderer = new VistaAuroraRenderer { Scale = scale };
        int R(int value) => (int)Math.Round(value * scale);
        foreach (var state in new[] { RenderState.Normal, RenderState.Hot, RenderState.Pressed,
                     RenderState.Disabled | RenderState.Hot })
        {
            using var bitmap = new Bitmap(R(120), R(32));
            using var g = Graphics.FromImage(bitmap);
            var field = new Rectangle(2, 2, R(112), R(24));
            var arrow = new Rectangle(field.Right - R(18), field.Top, R(18), field.Height);
            renderer.DrawComboBoxChrome(g, field, arrow, state, Color.White);
            using var popupRegion = renderer.CreatePopupRegion(field)!;
            // Match the popup window's actual outline at its alpha threshold,
            // and leave the surrounding toolbar untouched in every state.
            for (int y = 0; y < bitmap.Height; y++)
            for (int x = 0; x < bitmap.Width; x++)
            {
                Assert.Equal(popupRegion.IsVisible(x, y), bitmap.GetPixel(x, y).A >= 128);
                if (!field.Contains(x, y)) Assert.Equal(0, bitmap.GetPixel(x, y).A);
            }
            Assert.Equal(0, bitmap.GetPixel(field.Left, field.Top).A);
            foreach (int y in new[] { arrow.Top + 1, arrow.Bottom - 2 })
            {
                Assert.Equal(bitmap.GetPixel(arrow.Left + 1, y), bitmap.GetPixel(arrow.Left + R(8), y));
                Assert.Equal(255, bitmap.GetPixel(arrow.Left + 1, y).A);
            }
            var upperFill = bitmap.GetPixel(arrow.Left + 1, arrow.Top + 1);
            var lowerFill = bitmap.GetPixel(arrow.Left + 1, arrow.Bottom - 2);
            if (state is RenderState.Hot or RenderState.Pressed)
            {
                Assert.NotEqual(upperFill, lowerFill);
                Assert.NotEqual(Color.White.ToArgb(), upperFill.ToArgb());
            }
            else Assert.Equal(Color.White.ToArgb(), upperFill.ToArgb());
        }
    }

    [Theory]
    [InlineData(1f)]
    [InlineData(1.25f)]
    [InlineData(1.5f)]
    [InlineData(2f)]
    public void FloatingInnerLineIsOnePixelAndSymmetricAndMenuBarIsRounded(float scale)
    {
        var renderer = new VistaAuroraRenderer { Scale = scale };
        int R(int value) => (int)Math.Round(value * scale);
        var bounds = new Rectangle(0, 0, R(180), R(70));
        var caption = new Rectangle(R(3), R(3), bounds.Width - 2 * R(3), R(22));
        using var bitmap = new Bitmap(bounds.Width, bounds.Height);
        using var g = Graphics.FromImage(bitmap);
        renderer.DrawFloatingWindowChrome(g, bounds, caption);
        int inset = R(3) - 1;
        int color = Color.FromArgb(154, 209, 219).ToArgb();
        Assert.Equal(color, bitmap.GetPixel(inset, bounds.Height / 2).ToArgb());
        Assert.Equal(color, bitmap.GetPixel(bounds.Width - inset - 1, bounds.Height / 2).ToArgb());
        Assert.Equal(color, bitmap.GetPixel(bounds.Width / 2, inset).ToArgb());
        Assert.Equal(color, bitmap.GetPixel(bounds.Width / 2, bounds.Height - inset - 1).ToArgb());
        Assert.NotEqual(color, bitmap.GetPixel(inset + 1, bounds.Height / 2).ToArgb());
        Assert.NotEqual(color, bitmap.GetPixel(inset - 1, bounds.Height / 2).ToArgb());
        Assert.Equal(renderer.Colors.BandGradientEnd.ToArgb(), bitmap.GetPixel(bounds.Width / 2, bounds.Height / 2).ToArgb());

        using var menuBar = new Bitmap(bounds.Width, bounds.Height);
        using (var mg = Graphics.FromImage(menuBar))
            renderer.DrawBarBackground(mg, bounds, CommandBarType.MenuBar, BarOrientation.Horizontal, false, 0, bounds.Width);
        renderer.DrawBarBackground(g, bounds, CommandBarType.Toolbar, BarOrientation.Horizontal, true, 0, bounds.Width);
        for (int y = 0; y < R(5); y++)
        for (int x = 0; x < R(5); x++)
            Assert.Equal(bitmap.GetPixel(x, y), menuBar.GetPixel(x, y));
    }

    [Fact]
    public void TearOffGripChangesOnlyIdleDotColorAndPopupContentsPreserveRoundedBorder()
    {
        var renderer = new VistaAuroraRenderer();
        var bar = new CommandBar("palette", CommandBarType.Popup) { AllowTearOff = true };
        bar.Items.AddButton(new Command("bold") { Text = "Bold" });
        using var popup = new CommandBarPopupWindow(bar, renderer, SystemFonts.MenuFont!, 16, 1, (_, _) => { });
        var grip = (Rectangle)typeof(CommandBarPopupWindow).GetProperty("GripRect", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(popup)!;
        using var bitmap = new Bitmap(popup.Width, popup.Height);
        using var g = Graphics.FromImage(bitmap);
        typeof(CommandBarPopupWindow).GetMethod("DrawGrip", BindingFlags.Instance | BindingFlags.NonPublic)!
            .Invoke(popup, new object[] { g, grip });
        Assert.Equal(renderer.Colors.MenuGripperDots.ToArgb(), bitmap.GetPixel(grip.Left + 4, grip.Top + grip.Height / 2 - 2).ToArgb());
        Assert.Equal(renderer.Colors.ImageMarginBegin.ToArgb(), bitmap.GetPixel(grip.Left + 5, grip.Top + 1).ToArgb());
        Assert.True(renderer.Colors.MenuGripperDots.GetBrightness() < .5f);

        using var border = new Bitmap(popup.Width, popup.Height);
        using (var bg = Graphics.FromImage(border)) renderer.DrawMenuBackground(bg, popup.ClientRectangle);
        typeof(CommandBarPopupWindow).GetMethod("OnPaint", BindingFlags.Instance | BindingFlags.NonPublic)!
            .Invoke(popup, new object[] { new PaintEventArgs(g, popup.ClientRectangle) });
        for (int y = 0; y < popup.Height; y++)
        for (int x = 0; x < popup.Width; x++)
            if (x == 0 || y == 0 || x == popup.Width - 1 || y == popup.Height - 1)
                Assert.Equal(border.GetPixel(x, y), bitmap.GetPixel(x, y));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void FloatingWindowRegionTracksResizingAndThemeChanges(bool tearOff)
    {
        using var manager = new CommandBarManager();
        var bar = manager.AddBar("bar", CommandBarType.Toolbar);
        bar.Items.AddButton(new Command("a") { Text = "A command" });
        var renderer = new RegionRenderer();
        manager.RegisterTheme("test-rounded-region", "Test rounded region", () => renderer);
        manager.ApplyTheme("test-rounded-region");
        using var host = new DockHost();
        using Form window = tearOff ? new TearOffWindow(bar, bar, renderer, manager, null)
            : new FloatingWindow(bar, renderer, host, null);
        _ = window.Handle;
        Assert.NotNull(window.Region);
        Assert.False(window.Region!.IsVisible(0, 0));
        using var largerFont = new Font("Segoe UI", 18);
        window.Font = largerFont;
        window.PerformLayout();
        using var bitmap = new Bitmap(1, 1);
        using var g = Graphics.FromImage(bitmap);
        Assert.Equal((RectangleF)window.ClientRectangle, window.Region!.GetBounds(g));
        if (tearOff) manager.Theme = CommandBarTheme.Office2003;
        else ((FloatingWindow)window).SetRenderer(new Office2003Renderer());
        Assert.Null(window.Region);
    }

    private sealed class RegionRenderer : Office2003Renderer
    {
        public override int PopupCornerRadius => 4;
        internal override Region? CreatePopupRegion(Rectangle bounds)
            => RoundedSurface.CreateRegion(bounds, 4 * Scale);
    }

    [Fact]
    public void ThemePickerUpdatesHostsAndLayoutRestoresThemeAndSchemePreference()
    {
        using var manager = new CommandBarManager { ColorScheme = CommandBarColorScheme.Olive };
        using var host = new DockHost { Manager = manager };
        var popup = new CommandBarPopupItem("Theme") { ThemeList = true };
        typeof(CommandBarManager).GetMethod("PreparePopup", BindingFlags.Instance | BindingFlags.NonPublic)!
            .Invoke(manager, new object[] { popup });
        var choice = popup.DropDown.Items.OfType<CommandBarToggleButton>()
            .Single(t => t.Command.Text.Replace("&", "") == "Vista Aurora");
        Assert.True(choice.Command.Perform());
        Assert.Equal(CommandBarTheme.VistaAurora, manager.Theme);
        Assert.IsType<VistaAuroraRenderer>(host.Renderer);
        Assert.Same(manager.Renderer, host.Renderer);
        Assert.Equal(CommandBarColorScheme.Default, manager.EffectiveColorScheme);
        Assert.Single(manager.AvailableColorSchemes);
        using var stream = new MemoryStream();
        manager.SaveLayout(stream);
        stream.Position = 0;
        using var restored = new CommandBarManager();
        restored.LoadLayout(stream);
        Assert.Equal(CommandBarTheme.VistaAurora, restored.Theme);
        Assert.Equal("vistaaurora", restored.ActiveThemeKey);
        Assert.IsType<VistaAuroraRenderer>(restored.Renderer);
        restored.Theme = CommandBarTheme.Office2003;
        Assert.Equal(CommandBarColorScheme.Olive, restored.EffectiveColorScheme);
    }

    [Theory]
    [InlineData(1f)]
    [InlineData(1.25f)]
    [InlineData(1.5f)]
    [InlineData(2f)]
    public void GlassRotatesWithDockingAndKeepsItsReflectionAndClip(float scale)
    {
        var renderer = new VistaAuroraRenderer { Scale = scale };
        int width = (int)Math.Round(180 * scale), height = (int)Math.Round(32 * scale);
        using var horizontal = new Bitmap(width + 8, height + 8);
        using var vertical = new Bitmap(height + 8, width + 8);
        using (var g = Graphics.FromImage(horizontal))
            renderer.DrawBarBackground(g, new Rectangle(4, 4, width, height), CommandBarType.Toolbar,
                BarOrientation.Horizontal, true, 30, 500);
        using (var g = Graphics.FromImage(vertical))
            renderer.DrawBarBackground(g, new Rectangle(4, 4, height, width), CommandBarType.Toolbar,
                BarOrientation.Vertical, true, 30, 500);
        for (int y = 0; y < horizontal.Height; y++)
        for (int x = 0; x < horizontal.Width; x++)
        {
            // GDI+ arc coverage is not exactly transpose-symmetric at corners.
            // The straight edges and glass interior must still rotate identically.
            int corner = (int)Math.Round(3 * scale) + 1;
            bool atCorner = (x < 4 + corner || x >= 4 + width - corner)
                && (y < 4 + corner || y >= 4 + height - corner);
            if (!atCorner) Assert.Equal(horizontal.GetPixel(x, y), vertical.GetPixel(y, x));
            if (!new Rectangle(4, 4, width, height).Contains(x, y))
                Assert.Equal(0, horizontal.GetPixel(x, y).A);
        }
        var upper = horizontal.GetPixel(width / 2, 4 + height / 3);
        var lower = horizontal.GetPixel(width / 2, 4 + height * 3 / 5);
        Assert.True(upper.GetBrightness() > lower.GetBrightness() + .07f);
        Assert.True(lower.B > lower.R && lower.G > lower.R);
    }

    [Theory]
    [InlineData(1f)]
    [InlineData(1.25f)]
    [InlineData(1.5f)]
    [InlineData(2f)]
    public void ButtonStatesAreDistinctAndRespectExistingGraphicsClip(float scale)
    {
        var renderer = new VistaAuroraRenderer { Scale = scale };
        int size = (int)(32 * scale);
        var centers = new List<int>();
        foreach (var state in new[] { RenderState.Normal, RenderState.Hot, RenderState.Pressed, RenderState.Checked, RenderState.Disabled | RenderState.Hot })
        {
            using var bitmap = new Bitmap(size, size);
            using var g = Graphics.FromImage(bitmap);
            g.Clear(Color.Magenta);
            g.SetClip(new Rectangle(3, 3, size - 6, size - 6));
            renderer.DrawButton(g, new Rectangle(1, 1, size - 2, size - 2), state, BarOrientation.Horizontal);
            Assert.Equal(SmoothingMode.None, g.SmoothingMode);
            Assert.Equal(Color.Magenta.ToArgb(), bitmap.GetPixel(2, size / 2).ToArgb());
            centers.Add(bitmap.GetPixel(size / 2, size * 3 / 4).ToArgb());
        }
        Assert.Equal(4, centers.Distinct().Count());
        Assert.Equal(centers[0], centers[4]);
        // Degenerate geometry must not throw while a bar is squeezed by layout.
        using var tiny = new Bitmap(4, 4);
        using var tinyGraphics = Graphics.FromImage(tiny);
        foreach (int sizeValue in new[] { 0, 1, 2 })
        {
            renderer.DrawButton(tinyGraphics, new Rectangle(0, 0, sizeValue, sizeValue), RenderState.Hot, BarOrientation.Vertical);
            renderer.DrawBarBackground(tinyGraphics, new Rectangle(0, 0, sizeValue, sizeValue), CommandBarType.MenuBar, BarOrientation.Vertical, false, 0, 0);
        }
    }

    [Fact]
    public void MenuChecksAndComboArrowsStayDarkOnLightSurfaces()
    {
        var renderer = new VistaAuroraRenderer();
        using var bitmap = new Bitmap(24, 24);
        using var g = Graphics.FromImage(bitmap);
        foreach (bool disabled in new[] { false, true })
        {
            var state = disabled ? RenderState.Disabled : RenderState.Checked;
            int expected = (disabled ? renderer.Colors.DisabledMenuText : renderer.Colors.MenuText).ToArgb();
            foreach (var draw in new Action<Graphics, Rectangle, RenderState>[]
                     { renderer.DrawMenuCheck, renderer.DrawMenuRadio, renderer.DrawComboBoxArrow })
            {
                g.Clear(renderer.Colors.MenuBackground);
                draw(g, new Rectangle(0, 0, 24, 24), state);
                Assert.Contains(expected, Enumerable.Range(0, 24).SelectMany(y =>
                    Enumerable.Range(0, 24).Select(x => bitmap.GetPixel(x, y).ToArgb())));
            }
        }
        Assert.False(renderer.DialogColors.IsDark);
        Assert.Equal(renderer.Colors.MenuText, renderer.DialogColors.ButtonText);
        Assert.True(renderer.DialogColors.ButtonEnd.GetBrightness() > .7f);
    }

    [Fact]
    public void ToolbarIconRimPreservesOriginalPixelsAndDoesNotAffectMenuImages()
    {
        var renderer = new VistaAuroraRenderer();
        using var icon = new Bitmap(16, 16);
        using (var iconGraphics = Graphics.FromImage(icon))
            iconGraphics.FillRectangle(Brushes.DarkRed, 4, 4, 8, 8);
        using var toolbar = new Bitmap(24, 24);
        using var menu = new Bitmap(24, 24);
        using (var g = Graphics.FromImage(toolbar))
            renderer.DrawToolbarItemImage(g, icon, new Rectangle(4, 4, 16, 16), RenderState.Normal);
        using (var g = Graphics.FromImage(menu))
            renderer.DrawItemImage(g, icon, new Rectangle(4, 4, 16, 16), RenderState.Normal);
        Assert.Equal(Color.DarkRed.ToArgb(), toolbar.GetPixel(12, 12).ToArgb());
        Assert.Equal(menu.GetPixel(12, 12), toolbar.GetPixel(12, 12));
        Assert.True(toolbar.GetPixel(7, 12).A > 0);
        Assert.Equal(0, menu.GetPixel(7, 12).A);
    }

    [Theory]
    [InlineData(1f, false)]
    [InlineData(1.25f, false)]
    [InlineData(1.5f, false)]
    [InlineData(2f, false)]
    [InlineData(1f, true)]
    [InlineData(1.25f, true)]
    [InlineData(1.5f, true)]
    [InlineData(2f, true)]
    public void RoundedToolbarFrameRemainsContinuousWhenOverflowPaints(float scale, bool vertical)
    {
        var renderer = new VistaAuroraRenderer { Scale = scale };
        int R(int value) => (int)Math.Round(value * scale);
        var orientation = vertical ? BarOrientation.Vertical : BarOrientation.Horizontal;
        var bar = new Rectangle(4, 4, R(vertical ? 32 : 180), R(vertical ? 180 : 32));
        var nub = vertical
            ? new Rectangle(bar.X, bar.Bottom - R(14), bar.Width, R(14))
            : new Rectangle(bar.Right - R(14), bar.Y, R(14), bar.Height);
        using var original = new Bitmap(bar.Right + 4, bar.Bottom + 4);
        using (var g = Graphics.FromImage(original))
            renderer.DrawBarBackground(g, bar, CommandBarType.Toolbar, orientation, true, 0, R(200));

        // The curved corner exposes the dock band; the leading edge has a
        // visible inner highlight instead of disappearing into that band.
        using var square = new Bitmap(original.Width, original.Height);
        using (var g = Graphics.FromImage(square))
            renderer.DrawBarBackground(g, bar, CommandBarType.Toolbar, orientation, false, 0, R(200));
        Assert.NotEqual(square.GetPixel(bar.X, bar.Y), original.GetPixel(bar.X, bar.Y));
        var lead = vertical ? new Point(bar.X + bar.Width / 2, bar.Y + R(1))
            : new Point(bar.X + R(1), bar.Y + bar.Height / 2);
        Assert.True(original.GetPixel(lead.X, lead.Y).GetBrightness() > .45f);

        foreach (var state in new[] { RenderState.Normal, RenderState.Hot, RenderState.Pressed })
        foreach (bool overflow in new[] { false, true })
        {
            using var painted = (Bitmap)original.Clone();
            using (var g = Graphics.FromImage(painted))
                renderer.DrawChevron(g, nub, bar, orientation, state, overflow);
            int frame = Math.Max(1, R(1)) + 1;
            for (int y = 0; y < painted.Height; y++)
            for (int x = 0; x < painted.Width; x++)
            {
                bool perimeter = x < bar.Left + frame || x >= bar.Right - frame
                    || y < bar.Top + frame || y >= bar.Bottom - frame;
                if (perimeter) Assert.Equal(original.GetPixel(x, y), painted.GetPixel(x, y));
            }
            var center = new Point(nub.X + nub.Width / 2, nub.Y + nub.Height / 2);
            Assert.NotEqual(original.GetPixel(center.X, center.Y), painted.GetPixel(center.X, center.Y));
        }
    }
}
