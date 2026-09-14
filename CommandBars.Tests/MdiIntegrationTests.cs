using System.Drawing;
using System.Windows.Forms;
using System.Runtime.InteropServices;
using CommandBars.Controls;
using CommandBars.Model;
using CommandBars.Rendering;
using Xunit;

namespace CommandBars.Tests;

public class MdiIntegrationTests
{
    [Theory]
    [InlineData(1, RenderState.Normal)]
    [InlineData(1, RenderState.Hot)]
    [InlineData(1, RenderState.Pressed)]
    [InlineData(1, RenderState.Disabled)]
    [InlineData(2, RenderState.Normal)]
    [InlineData(2, RenderState.Hot)]
    [InlineData(2, RenderState.Pressed)]
    [InlineData(2, RenderState.Disabled)]
    public void RestoreGlyphPreservesTheBackgroundInsideItsWindows(int scale, RenderState state)
    {
        var renderer = new Office2003Renderer { Scale = scale };
        var bounds = new Rectangle(0, 0, 20 * scale, 20 * scale);
        using var background = new Bitmap(bounds.Width, bounds.Height);
        using (var graphics = Graphics.FromImage(background))
        {
            using var gradient = new System.Drawing.Drawing2D.LinearGradientBrush(bounds,
                Color.CornflowerBlue, Color.Orange, 90f);
            graphics.FillRectangle(gradient, bounds);
            if ((state & (RenderState.Hot | RenderState.Pressed)) != 0)
                renderer.DrawButton(graphics, bounds, state, BarOrientation.Horizontal);
        }
        using var actual = (Bitmap)background.Clone();
        using (var graphics = Graphics.FromImage(actual))
            renderer.DrawMdiButton(graphics, bounds, CaptionButton.Restore, null, state);
        // The front window interior must retain the underlying gradient at both
        // normal and highlighted states, rather than a theme's solid starting color.
        Assert.Equal(background.GetPixel(9 * scale, 12 * scale), actual.GetPixel(9 * scale, 12 * scale));
        Assert.Equal(background.GetPixel(10 * scale, 12 * scale), actual.GetPixel(10 * scale, 12 * scale));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void FirstMenuFollowsMaximizedChildAcrossFloatingAndDocking(bool customFrame)
    {
        RunSta(() =>
        {
            using var manager = new CommandBarManager();
            using var parent = new Form { IsMdiContainer = true, ClientSize = new Size(800, 500) };
            var menu = manager.AddBar("main", CommandBarType.MenuBar);
            menu.Items.AddPopup("&File");
            var second = manager.AddBar("secondary", CommandBarType.MenuBar);
            second.Items.AddPopup("&Other");
            using var host = new DockHost { Manager = manager, Edge = DockEdge.Top };
            parent.Controls.Add(host);
            parent.Show();
            using var backgroundChild = new Form { MdiParent = parent, Text = "Background" };
            backgroundChild.Show();
            using Form child = customFrame ? new CommandBarMdiChildForm { Manager = manager } : new Form();
            child.MdiParent = parent;
            child.Text = "First";
            child.Show();
            Application.DoEvents();
            var control = host.BarControls.First(c => c.Bar == menu);
            Assert.False(control.HasMdiChrome);
            child.WindowState = FormWindowState.Maximized;
            Application.DoEvents();
            Assert.True(control.HasMdiChrome);
            Assert.Equal(IntPtr.Zero, GetMenu(parent.Handle));
            Assert.Same(child, parent.ActiveMdiChild);
            Assert.False(host.BarControls.First(c => c.Bar == second).HasMdiChrome);
            Assert.Single(menu.Items);
            foreach (var button in control.Controls.OfType<CommandBarControl.MdiButton>())
                Assert.Equal(button.Width, button.Height);
            Assert.True(menu.Items[0].Bounds.Left >= Button(control, "Child system menu").Right);
            Assert.True(menu.Items[0].Bounds.Right <= Button(control, "Minimize child").Left);

            host.FloatBar(menu, new Point(100, 100));
            Application.DoEvents();
            var floating = parent.OwnedForms.OfType<FloatingWindow>().Single();
            control = floating.BarControl;
            foreach (var button in control.Controls.OfType<CommandBarControl.MdiButton>())
                Assert.Equal(button.Width, button.Height);
            Assert.Same(child, parent.ActiveMdiChild);
            Assert.True(control.HasMdiChrome);
            Assert.True(Button(control, "Close child").Right <= control.Width);
            Assert.True(menu.Items[0].Bounds.Right <= Button(control, "Minimize child").Left);

            // Switching maximized documents must retarget actions, including cancelled close.
            using var other = new Form { MdiParent = parent, Text = "Second" };
            other.Show();
            other.WindowState = FormWindowState.Maximized;
            Application.DoEvents();
            bool cancel = true;
            other.FormClosing += (_, e) => e.Cancel = cancel;
            Button(control, "Close child").PerformClick();
            Assert.False(other.IsDisposed);
            cancel = false;
            Button(control, "Close child").PerformClick();
            Application.DoEvents();
            Assert.True(other.IsDisposed);
            Assert.False(child.IsDisposed);
            Assert.True(control.HasMdiChrome);

            Button(control, "Restore child").PerformClick();
            Application.DoEvents();
            Assert.Equal(FormWindowState.Normal, child.WindowState);
            Assert.False(control.HasMdiChrome);
            child.WindowState = FormWindowState.Maximized;
            Application.DoEvents();
            Button(control, "Minimize child").PerformClick();
            Application.DoEvents();
            Assert.Equal(FormWindowState.Minimized, child.WindowState);
            Assert.False(control.HasMdiChrome);

            child.WindowState = FormWindowState.Maximized;
            child.Activate();
            menu.Dock = DockState.Top;
            manager.RefreshLayout();
            Application.DoEvents();
            control = host.BarControls.First(c => c.Bar == menu);
            Assert.True(control.HasMdiChrome);
            Assert.True(floating.IsDisposed);
            backgroundChild.Close();
            child.Close();
            Application.DoEvents();
            Assert.False(control.HasMdiChrome);
            parent.Close();
        });
    }

    [Theory]
    [InlineData(CommandBarTheme.Office2003)]
    [InlineData(CommandBarTheme.OfficeXP)]
    [InlineData(CommandBarTheme.Dark)]
    public void CaptionButtonsPaintTheirBackgroundAndBridgeIsReleased(CommandBarTheme theme)
    {
        RunSta(() =>
        {
            using var manager = new CommandBarManager { Theme = theme };
            using var parent = new Form { IsMdiContainer = true, Width = 600 };
            var menu = manager.AddBar("main", CommandBarType.MenuBar);
            menu.Items.AddPopup("&File");
            using var host = new DockHost { Manager = manager };
            parent.Controls.Add(host);
            parent.Show();
            using var child = new Form { MdiParent = parent };
            child.Show();
            child.WindowState = FormWindowState.Maximized;
            Application.DoEvents();
            var control = host.BarControls.Single();
            var bridge = parent.MainMenuStrip;
            Assert.NotNull(bridge);
            Assert.Equal(IntPtr.Zero, GetMenu(parent.Handle));
            foreach (var button in control.Controls.OfType<CommandBarControl.MdiButton>())
            {
                using var bitmap = new Bitmap(button.Width, button.Height);
                using (var graphics = Graphics.FromImage(bitmap)) graphics.Clear(Color.Magenta);
                button.DrawToBitmap(bitmap, button.ClientRectangle);
                Assert.NotEqual(Color.Magenta.ToArgb(), bitmap.GetPixel(0, 0).ToArgb());
                Assert.NotEqual(Color.Black.ToArgb(), bitmap.GetPixel(0, 0).ToArgb());
            }
            menu.Visible = false;
            manager.RefreshLayout();
            Application.DoEvents();
            Assert.Null(parent.MainMenuStrip);
            Assert.True(bridge!.IsDisposed);
            Assert.NotEqual(IntPtr.Zero, GetMenu(parent.Handle));
            menu.Visible = true;
            manager.RefreshLayout();
            Application.DoEvents();
            Assert.Equal(IntPtr.Zero, GetMenu(parent.Handle));
            Assert.True(host.BarControls.Single().HasMdiChrome);
            parent.Close();
        });
    }

    [Fact]
    public void NarrowMenuReservesChromeAndHonorsChildCapabilities()
    {
        RunSta(() =>
        {
            using var manager = new CommandBarManager();
            using var parent = new Form { IsMdiContainer = true, Width = 400 };
            var menu = manager.AddBar("main", CommandBarType.MenuBar);
            for (int i = 0; i < 8; i++) menu.Items.AddPopup($"Long menu {i}");
            using var host = new DockHost { Manager = manager };
            parent.Controls.Add(host);
            parent.Show();
            using var child = new Form { MdiParent = parent, MinimizeBox = false };
            child.Show();
            child.WindowState = FormWindowState.Maximized;
            Application.DoEvents();
            var control = host.BarControls.Single();
            Assert.True(control.HasMdiChrome);
            Assert.False(Button(control, "Minimize child").Enabled);
            Assert.NotEmpty(control.OverflowItems);
            foreach (var item in menu.Items.Where(item => !control.OverflowItems.Contains(item)))
                Assert.True(item.Bounds.Right <= Button(control, "Minimize child").Left);
            child.ControlBox = false;
            control.RefreshMdiChild();
            Assert.False(control.HasMdiChrome);
            parent.Close();
        });
    }

    private static CommandBarControl.MdiButton Button(CommandBarControl control, string name) =>
        control.Controls.OfType<CommandBarControl.MdiButton>().Single(button => button.AccessibleName == name);

    [Fact]
    public void CustomFrameResizesThemesAndFillsMaximizedWorkspace()
    {
        RunSta(() =>
        {
            using var manager = new CommandBarManager();
            using var parent = new Form { IsMdiContainer = true, ClientSize = new Size(800, 600) };
            using var child = new CommandBarMdiChildForm { Manager = manager, MdiParent = parent, ClientSize = new Size(400, 300) };
            using var content = new TextBox { Dock = DockStyle.Fill, Multiline = true };
            child.Controls.Add(content);
            parent.Show(); child.Show(); Application.DoEvents();
            Assert.True(content.Top >= child.CaptionBounds.Bottom);
            Assert.True(content.Left >= child.FrameBorder);
            var handle = child.Handle;
            AssertSquareWindowRegion(child);
            foreach (var hit in new[] { (new Point(0, 0), 13), (new Point(child.Width - 1, 0), 14),
                (new Point(0, child.Height - 1), 16), (new Point(child.Width - 1, child.Height - 1), 17),
                (new Point(0, 50), 10), (new Point(child.Width - 1, 50), 11),
                (new Point(50, 0), 12), (new Point(50, child.Height - 1), 15) })
            {
                var point = child.PointToScreen(hit.Item1);
                Assert.Equal(hit.Item2, SendMessage(child.Handle, 0x0084, IntPtr.Zero,
                    new IntPtr((point.Y << 16) | (point.X & 0xffff))).ToInt32());
            }
            var originalBounds = child.Bounds;
            manager.Theme = CommandBarTheme.Dark;
            Assert.Same(manager.Renderer, child.FrameRenderer);
            Assert.Equal(originalBounds, child.Bounds);
            child.MinimumSize = new Size(220, 160);
            child.MaximumSize = new Size(650, 480);
            child.Size = new Size(50, 50);
            Assert.Equal(child.MinimumSize, child.Size);
            child.Size = new Size(1000, 900);
            Assert.Equal(child.MaximumSize, child.Size);
            child.MaximumSize = Size.Empty;
            child.Size = new Size(450, 330);
            Application.DoEvents();
            AssertSquareWindowRegion(child);
            Assert.True(content.Right <= child.ClientSize.Width - child.FrameBorder);
            var restored = child.Bounds;
            child.WindowState = FormWindowState.Maximized;
            Application.DoEvents();
            var client = parent.Controls.OfType<MdiClient>().Single();
            Assert.Equal(new Rectangle(Point.Empty, client.ClientSize), child.Bounds);
            Assert.Equal(child.ClientRectangle, content.Bounds);
            parent.ClientSize = new Size(900, 650);
            Application.DoEvents();
            Assert.Equal(new Rectangle(Point.Empty, client.ClientSize), child.Bounds);
            child.WindowState = FormWindowState.Normal;
            Application.DoEvents();
            Assert.Equal(restored, child.Bounds);
            SendMessage(child.Handle, 0x0112, new IntPtr(0xF030), IntPtr.Zero); // SC_MAXIMIZE, same native path as caption double-click
            Application.DoEvents();
            Assert.Equal(child.ClientRectangle, content.Bounds);
            SendMessage(child.Handle, 0x0112, new IntPtr(0xF120), IntPtr.Zero); // SC_RESTORE
            Application.DoEvents();
            Assert.Equal(restored, child.Bounds);
            Assert.Equal(handle, child.Handle);
            child.WindowState = FormWindowState.Minimized;
            Application.DoEvents();
            SendMessage(child.Handle, 0x00A1, new IntPtr(8), IntPtr.Zero); // drawn minimized restore action
            Application.DoEvents();
            Assert.Equal(FormWindowState.Normal, child.WindowState);
            Assert.Equal(restored, child.Bounds);
            child.FormClosing += (_, e) => e.Cancel = true;
            child.Close();
            Assert.False(child.IsDisposed);
        });
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void IconContextMenuOpensNativeMenuWithoutChangingActiveChild(bool floating)
    {
        RunSta(() =>
        {
            using var manager = new CommandBarManager();
            using var parent = new Form { IsMdiContainer = true };
            var menu = manager.AddBar("main", CommandBarType.MenuBar);
            menu.Items.AddPopup("&File");
            using var host = new DockHost { Manager = manager };
            parent.Controls.Add(host);
            parent.Show();
            using var child = new Form { MdiParent = parent };
            child.Show();
            child.WindowState = FormWindowState.Maximized;
            Application.DoEvents();
            if (floating) host.FloatBar(menu, new Point(100, 100));
            Application.DoEvents();
            var control = floating ? parent.OwnedForms.OfType<FloatingWindow>().Single().BarControl : host.BarControls.Single();
            bool menuOpened = false;
            using var timer = new System.Windows.Forms.Timer { Interval = 100 };
            timer.Tick += (_, _) =>
            {
                timer.Stop();
                // EndMenu succeeds only while this thread is inside a native menu loop.
                menuOpened = EndMenu();
            };
            timer.Start();
            var icon = Button(control, "Child system menu");
            SendMessage(icon.Handle, 0x007B, icon.Handle, new IntPtr(-1));
            timer.Stop();
            Assert.True(menuOpened);
            Assert.Same(child, parent.ActiveMdiChild);
            Assert.Equal(FormWindowState.Maximized, child.WindowState);
            parent.Close();
        });
    }

    [DllImport("user32.dll")] private static extern bool EndMenu();
    private static void AssertSquareWindowRegion(Form form)
    {
        IntPtr region = CreateRectRgn(0, 0, 0, 0);
        try
        {
            Assert.NotEqual(0, GetWindowRgn(form.Handle, region));
            Assert.True(PtInRegion(region, 0, 0));
            Assert.True(PtInRegion(region, form.Width - 1, 0));
            Assert.True(PtInRegion(region, 0, form.Height - 1));
            Assert.True(PtInRegion(region, form.Width - 1, form.Height - 1));
        }
        finally { DeleteObject(region); }
    }
    [DllImport("gdi32.dll")] private static extern IntPtr CreateRectRgn(int left, int top, int right, int bottom);
    [DllImport("user32.dll")] private static extern int GetWindowRgn(IntPtr window, IntPtr region);
    [DllImport("gdi32.dll")] private static extern bool PtInRegion(IntPtr region, int x, int y);
    [DllImport("gdi32.dll")] private static extern bool DeleteObject(IntPtr value);
    [DllImport("user32.dll")] private static extern IntPtr SendMessage(IntPtr window, uint message, IntPtr wParam, IntPtr lParam);

    [DllImport("user32.dll")] private static extern IntPtr GetMenu(IntPtr window);

    private static void RunSta(Action action)
    {
        Exception? failure = null;
        var thread = new Thread(() => { try { action(); } catch (Exception ex) { failure = ex; } });
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        Assert.True(thread.Join(TimeSpan.FromSeconds(30)), "MDI test timed out");
        if (failure != null) System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(failure).Throw();
    }
}
