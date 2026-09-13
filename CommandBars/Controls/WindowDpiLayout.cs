using System.Drawing;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace CommandBars.Controls;

/// <summary>Finishes custom measurements after WinForms and child HWNDs finish DPI scaling.</summary>
internal sealed class WindowDpiLayout : NativeWindow
{
    private readonly Form _window;
    private readonly Action _refresh;
    private int _generation;
    private bool _displayCheckPending;

    internal bool Pending { get; private set; }

    internal WindowDpiLayout(Form window, Action refresh)
    {
        _window = window;
        _refresh = refresh;
        window.DpiChanged += (_, _) => Queue();
        window.HandleCreated += (_, _) => AssignHandle(window.Handle);
        window.HandleDestroyed += (_, _) =>
        {
            _generation++;
            Pending = false;
            _displayCheckPending = false;
            ReleaseHandle();
        };
        if (window.IsHandleCreated) AssignHandle(window.Handle);
    }

    protected override void WndProc(ref Message m)
    {
        int message = m.Msg;
        base.WndProc(ref m);
        // Windows can broadcast a new display scale without sending WM_DPICHANGED
        // to stationary owned windows. Recheck their own monitor, not their owner.
        if (message is 0x007E or 0x001A) // WM_DISPLAYCHANGE / WM_SETTINGCHANGE
            QueueDisplayCheck();
    }

    private void QueueDisplayCheck()
    {
        if (_displayCheckPending || !_window.IsHandleCreated || _window.IsDisposed) return;
        _displayCheckPending = true;
        int generation = _generation;
        _window.BeginInvoke((MethodInvoker)(() =>
        {
            if (generation != _generation || _window.IsDisposed) return;
            _displayCheckPending = false;
            RecheckMonitorDpi();
        }));
    }

    private void RecheckMonitorDpi()
    {
        if (!OperatingSystem.IsWindowsVersionAtLeast(10, 0, 14393) ||
            _window.WindowState != FormWindowState.Normal ||
            GetAwarenessFromDpiAwarenessContext(GetWindowDpiAwarenessContext(Handle)) != 2)
            return;

        uint oldDpi = GetDpiForWindow(Handle);
        IntPtr monitor = MonitorFromWindow(Handle, 2); // MONITOR_DEFAULTTONEAREST
        if (oldDpi == 0 || GetDpiForMonitor(monitor, 0, out uint dpi, out _) != 0 ||
            dpi == 0 || dpi == oldDpi)
            return;

        Rectangle bounds = RescaleBounds(_window.Bounds, oldDpi, dpi);
        // Request the new size at the SAME position. This makes Windows deliver
        // the real DPI sequence (including child HWNDs). NOSIZE would overwrite
        // the size applied by Form inside the nested WM_DPICHANGED processing.
        _ = SetWindowPos(Handle, IntPtr.Zero, bounds.X, bounds.Y, bounds.Width, bounds.Height,
            0x0004 | 0x0010 | 0x0020); // NOZORDER | NOACTIVATE | FRAMECHANGED
    }

    internal static Rectangle RescaleBounds(Rectangle bounds, uint oldDpi, uint dpi)
        => new(bounds.Location, new Size(
            Math.Max(1, (int)Math.Round(bounds.Width * (double)dpi / oldDpi)),
            Math.Max(1, (int)Math.Round(bounds.Height * (double)dpi / oldDpi))));

    private void Queue()
    {
        if (Pending || !_window.IsHandleCreated || _window.IsDisposed) return;
        Pending = true;
        int generation = _generation;
        // DpiChanged is raised before Form scales its children. A posted pass also
        // follows WM_DPICHANGED_AFTERPARENT, so custom content uses the final DPI.
        _window.BeginInvoke((MethodInvoker)(() =>
        {
            if (generation != _generation || _window.IsDisposed) return;
            Pending = false;
            _refresh();
            _window.PerformLayout();
            _window.Invalidate(true);
        }));
    }

    [DllImport("user32.dll")]
    private static extern uint GetDpiForWindow(IntPtr window);

    [DllImport("user32.dll")]
    private static extern IntPtr GetWindowDpiAwarenessContext(IntPtr window);

    [DllImport("user32.dll")]
    private static extern int GetAwarenessFromDpiAwarenessContext(IntPtr context);

    [DllImport("user32.dll")]
    private static extern IntPtr MonitorFromWindow(IntPtr window, uint flags);

    [DllImport("shcore.dll")]
    private static extern int GetDpiForMonitor(IntPtr monitor, int type, out uint dpiX, out uint dpiY);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool SetWindowPos(IntPtr window, IntPtr after, int x, int y,
        int width, int height, uint flags);
}
