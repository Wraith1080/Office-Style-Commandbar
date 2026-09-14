using System.Drawing;
using System.Runtime.ExceptionServices;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using CommandBars.Controls;
using CommandBars.Model;
using CommandBars.Rendering;
using Xunit;

namespace CommandBars.DpiTests;

public sealed class LiveDpiTests
{
    [Theory]
    [InlineData(false, false)]
    [InlineData(true, false)]
    [InlineData(false, true)]
    [InlineData(true, true)]
    public void MaximizedMdiChild_RealignsItsNativeFrameAfterDpiChange(bool floating, bool customFrame)
    {
        RunSta(() =>
        {
            using var manager = new CommandBarManager();
            using var parent = new Form { IsMdiContainer = true, AutoScaleMode = AutoScaleMode.Dpi, ClientSize = new Size(700, 450) };
            var menu = manager.AddBar("menu", CommandBarType.MenuBar);
            menu.Items.AddPopup("&File");
            using var host = new DockHost { Manager = manager };
            parent.Controls.Add(host);
            parent.Show();
            using Form child = customFrame ? new CommandBarMdiChildForm { Manager = manager } : new Form();
            child.MdiParent = parent;
            child.Show();
            child.WindowState = FormWindowState.Maximized;
            Application.DoEvents();
            if (floating) host.FloatBar(menu, new Point(100, 100));
            Application.DoEvents();
            var client = parent.Controls.OfType<MdiClient>().Single();
            var handle = child.Handle;
            int initialDpi = parent.DeviceDpi;
            foreach (int dpi in new[] { initialDpi * 2, initialDpi })
            {
                SendDpiChange(parent, dpi);
                // Model late child scaling moving the native frame back inside the
                // MDI client, after the parent's DpiChanged event has been raised.
                SetWindowPos(child.Handle, IntPtr.Zero, 0, 0, 0, 0, 0x0001 | 0x0004 | 0x0010);
                Application.DoEvents();
                Assert.Equal(FormWindowState.Maximized, child.WindowState);
                Assert.Same(child, parent.ActiveMdiChild);
                Assert.Equal(handle, child.Handle);
                Assert.Equal(client.PointToScreen(Point.Empty), child.PointToScreen(Point.Empty));
                Assert.Equal(client.ClientSize, child.ClientSize);
            }
            parent.Close();
        });
    }

    [DllImport("user32.dll")]
    private static extern bool SetWindowPos(IntPtr window, IntPtr after, int x, int y, int width, int height, uint flags);

    [Fact]
    public void CustomMdiChildrenCreatedAfterDpiNotificationHaveNoNativeFrame()
    {
        RunSta(() =>
        {
            using var manager = new CommandBarManager();
            using var parent = new Form { IsMdiContainer = true, AutoScaleMode = AutoScaleMode.Dpi };
            parent.Show();
            int dpi = parent.DeviceDpi;
            foreach (int nextDpi in new[] { dpi * 2, dpi })
            {
                SendDpiChange(parent, nextDpi);
                Application.DoEvents();
                using var child = new CommandBarMdiChildForm { Manager = manager, MdiParent = parent };
                child.Show();
                child.WindowState = FormWindowState.Maximized;
                Application.DoEvents();
                var client = parent.Controls.OfType<MdiClient>().Single();
                Assert.Equal(new Rectangle(Point.Empty, client.ClientSize), child.Bounds);
                Assert.Equal(client.PointToScreen(Point.Empty), child.PointToScreen(Point.Empty));
                child.WindowState = FormWindowState.Normal;
                Application.DoEvents();
                Assert.True(child.CaptionBounds.Height > 0);
                Assert.True(child.Padding.Top >= child.CaptionBounds.Bottom);
                child.Close();
            }
            parent.Close();
        });
    }

    [Theory]
    [InlineData(2f, false)]
    [InlineData(0.8f, false)]
    [InlineData(2f, true)]
    [InlineData(0.8f, true)]
    public void NewlyOpenedPopups_DoNotScaleTheirSourceFontAgain(float factor, bool combo)
    {
        RunSta(() =>
        {
            using var customFont = new Font("Segoe UI", 11f, FontStyle.Bold);
            using var owner = new Form { AutoScaleMode = AutoScaleMode.Dpi, Font = customFont };
            _ = owner.Handle;
            int dpi = (int)(owner.DeviceDpi * factor);
            SendDpiChange(owner, dpi);
            Application.DoEvents();
            using var manager = new CommandBarManager();
            var bar = manager.AddBar("menu", CommandBarType.Popup);
            bar.Items.AddButton(new Command("test") { Text = "Test" });
            Font sourceFont = owner.Font;
            var windows = new List<Form>();
            try
            {
                for (int level = 0; level < 3; level++)
                {
                    manager.Renderer.Scale = dpi / 96f;
                    var items = new CommandBarComboBox();
                    items.Items.Add("Test");
                    Form popup = combo
                        ? new ComboDropDown(items, manager.Renderer, sourceFont, new Rectangle(0, 0, 120, 24))
                        : new CommandBarPopupWindow(bar, manager.Renderer, sourceFont, 24, dpi / 96f);
                    windows.Add(popup);
                    _ = popup.Handle;
                    SendDpiChange(popup, dpi);
                    Application.DoEvents();
                    Assert.InRange(popup.Font.Size, owner.Font.Size - 0.1f, owner.Font.Size + 0.1f);
                    Assert.Equal(owner.Font.Style, popup.Font.Style);
                    var size = popup.Size;
                    SendDpiChange(popup, dpi * 2);
                    Application.DoEvents();
                    Assert.InRange(popup.Font.Size, owner.Font.Size * 2 - 0.1f, owner.Font.Size * 2 + 0.1f);
                    SendDpiChange(popup, dpi);
                    Application.DoEvents();
                    Assert.InRange(popup.Font.Size, owner.Font.Size - 0.1f, owner.Font.Size + 0.1f);
                    Assert.Equal(size, popup.Size);
                    sourceFont = popup.Font;
                }
            }
            finally { foreach (var window in windows) window.Dispose(); }
        });
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(4)]
    public void FloatingIconSizeChanges_ResizeFrameAfterDpiChange(int columns)
    {
        RunSta(() =>
        {
            using var manager = new CommandBarManager();
            using var host = new DockHost();
            var bar = manager.AddBar("icons", CommandBarType.Toolbar);
            bar.IconSize = 16;
            bar.PaletteColumns = columns > 1 ? columns : 0;
            using var bitmap = new Bitmap(16, 16);
            var icon = new CommandBars.Imaging.BitmapImageSource(bitmap);
            for (int i = 0; i < 4; i++)
                bar.Items.AddButton(new Command("test" + i) { Text = "Test", Image = icon })
                    .DisplayStyle = CommandItemDisplayStyle.ImageOnly;
            using Form window = columns == 0
                ? new FloatingWindow(bar, manager.Renderer, host, null)
                : new TearOffWindow(bar, bar, manager.Renderer, null, null);
            _ = window.Handle;
            SendDpiChange(window, window.DeviceDpi * 2);
            Application.DoEvents();
            Size small = window.Size;
            bar.IconSize = 64;
            if (window is FloatingWindow floating) floating.SetRenderer(manager.Renderer);
            else ((TearOffWindow)window).Relayout();
            Application.DoEvents();
            Assert.True(window.Height > small.Height);
            Assert.True(window.Width > small.Width);
            bar.IconSize = 16;
            if (window is FloatingWindow floatingAgain) floatingAgain.SetRenderer(manager.Renderer);
            else ((TearOffWindow)window).Relayout();
            Application.DoEvents();
            Assert.Equal(small, window.Size);
        });
    }

    [Theory]
    [InlineData(120u, 144u, 600, 360)]
    [InlineData(144u, 120u, 417, 250)]
    public void StationaryDpiResize_PreservesLocation(uint oldDpi, uint dpi, int width, int height)
    {
        var bounds = new Rectangle(-1400, 75, 500, 300);
        Assert.Equal(new Rectangle(-1400, 75, width, height),
            WindowDpiLayout.RescaleBounds(bounds, oldDpi, dpi));
    }

    // Exercise the native notification path without changing the user's desktop
    // settings. Child HWND monitor transitions still require a live UI check.
    [Theory]
    [InlineData("customize")]
    [InlineData("add")]
    [InlineData("floating")]
    [InlineData("tearoff")]
    [InlineData("popup")]
    [InlineData("combo")]
    [InlineData("color")]
    public void OpenWindow_ProcessesDpiChangesWithoutRecreatingHandle(string kind)
    {
        RunSta(() =>
        {
            using var manager = new CommandBarManager();
            using var host = new DockHost();
            manager.Renderer.Scale = host.DeviceDpi / 96f;
            var bar = manager.AddBar("test", CommandBarType.Toolbar);
            bar.Items.AddButton(new Command("test") { Text = "Test" });
            var combo = new CommandBarComboBox();
            combo.Items.Add("First");
            combo.Items.Add("Second");
            using Form form = kind switch
            {
                "customize" => new CustomizeDialog(manager, manager.Renderer),
                "add" => CustomizeDialog.CreateDpiScaledForm(),
                "floating" => new FloatingWindow(bar, manager.Renderer, host, null),
                "tearoff" => new TearOffWindow(bar, bar, manager.Renderer, null, null),
                "combo" => new ComboDropDown(combo, manager.Renderer, SystemFonts.MenuFont!, new Rectangle(0, 0, 120, 24)),
                "color" => new FluentColorDialog(manager.ColorScheme, new FluentColorOptions()),
                _ => new CommandBarPopupWindow(bar, manager.Renderer, SystemFonts.MenuFont!, 24, host.DeviceDpi / 96f),
            };
            if (kind == "add")
            {
                form.ClientSize = new Size(280, 360);
                form.Controls.Add(new ThemedListBox { Dock = DockStyle.Fill });
                form.ResumeLayout(true);
            }
            IntPtr handle = form.Handle;
            float initialFont = form.Font.Size;
            int initialHeight = form.Height;
            int initialDpi = form.DeviceDpi;
            SendDpiChange(form, initialDpi * 2);
            Application.DoEvents();
            Assert.Equal(handle, form.Handle);
            Assert.Equal(initialDpi * 2, form.DeviceDpi);
            Assert.True(form.Font.Size > initialFont * 1.8f, $"{kind}: {initialFont} -> {form.Font.Size}");
            Assert.True(form.Height > initialHeight, $"{kind} height: {initialHeight} -> {form.Height}");
            if (form is FloatingWindow floating)
                Assert.True(form.ClientRectangle.Contains(floating.BarControl.Bounds));
            if (form is TearOffWindow tearoff)
                Assert.True(form.ClientRectangle.Contains(tearoff.BarControl.Bounds));
            SendDpiChange(form, initialDpi);
            Application.DoEvents();
            Assert.Equal(handle, form.Handle);
            Assert.InRange(form.Font.Size, initialFont - 0.1f, initialFont + 0.1f);
            if (kind is "combo" or "popup" or "floating" or "tearoff")
                Assert.InRange(form.Height, initialHeight - 2, initialHeight + 2);
        });
    }

    [Fact]
    public void DpiLayout_CoalescesMessagesAndRunsAfterFormScaling()
    {
        RunSta(() =>
        {
            using var form = new Form { AutoScaleMode = AutoScaleMode.Dpi };
            int calls = 0;
            int measuredDpi = 0;
            var layout = new WindowDpiLayout(form, () => { calls++; measuredDpi = form.DeviceDpi; });
            _ = form.Handle;
            SendDpiChange(form, 144);
            SendDpiChange(form, 192);
            Assert.True(layout.Pending);
            Assert.Equal(0, calls);
            Application.DoEvents();
            Assert.False(layout.Pending);
            Assert.Equal(1, calls);
            Assert.Equal(192, measuredDpi);
            SendDpiChange(form, 96);
            form.Dispose();
            Application.DoEvents();
            Assert.Equal(1, calls);
        });
    }

    private static void SendDpiChange(Form form, int dpi)
    {
        float scale = (float)dpi / form.DeviceDpi;
        var rect = new NativeRect { Left = form.Left, Top = form.Top,
            Right = form.Left + (int)(form.Width * scale), Bottom = form.Top + (int)(form.Height * scale) };
        SendMessage(form.Handle, 0x02E0, (IntPtr)((dpi << 16) | dpi), ref rect);
    }

    private static void RunSta(Action action)
    {
        Exception? error = null;
        var thread = new Thread(() =>
        {
            IntPtr previous = SetThreadDpiAwarenessContext((IntPtr)(-4));
            try { action(); } catch (Exception e) { error = e; }
            finally { SetThreadDpiAwarenessContext(previous); }
        });
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        thread.Join();
        if (error is not null) ExceptionDispatchInfo.Capture(error).Throw();
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct NativeRect { public int Left, Top, Right, Bottom; }

    [DllImport("user32.dll", CharSet = CharSet.Auto)]
    private static extern IntPtr SendMessage(IntPtr hwnd, int msg, IntPtr wparam, ref NativeRect rect);

    [DllImport("user32.dll")]
    private static extern IntPtr SetThreadDpiAwarenessContext(IntPtr context);
}
