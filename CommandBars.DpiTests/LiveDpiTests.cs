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
