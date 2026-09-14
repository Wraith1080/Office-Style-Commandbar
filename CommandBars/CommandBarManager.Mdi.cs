using System.Windows.Forms;
using System.Runtime.InteropServices;

namespace CommandBars;

public partial class CommandBarManager
{
    private Form? _mdiMenuOwner;
    private MenuStrip? _mdiMenuBridge;
    private bool _mdiDisposed;
    private bool _mdiDpiLayoutPending;
    private int _mdiLayoutGeneration;

    // WinForms otherwise installs a dummy native menu for maximized MDI children.
    // An unsited MenuStrip selects its managed-menu path without adding another
    // visible row. Keep it at manager scope so docking/rebuilds don't recreate it.
    internal void EnsureMdiMenuMode(Form owner)
    {
        if (_mdiDisposed || DesignMode || owner.IsDisposed || owner.Disposing) return;
        if (ReferenceEquals(owner, _mdiMenuOwner)) return;
        ReleaseMdiMenuMode();
        _mdiMenuOwner = owner;
        owner.Disposed += MdiOwnerDisposed;
        owner.DpiChanged += MdiOwnerDpiChanged;
        owner.HandleDestroyed += MdiOwnerHandleDestroyed;
        if (owner.MainMenuStrip == null) // application-owned strips remain untouched
        {
            _mdiMenuBridge = new MenuStrip { Visible = false, AllowMerge = false };
            owner.MainMenuStrip = _mdiMenuBridge;
        }
    }

    private void MdiOwnerDisposed(object? sender, EventArgs e) => ReleaseMdiMenuMode();

    private void MdiOwnerHandleDestroyed(object? sender, EventArgs e)
    {
        _mdiLayoutGeneration++;
        _mdiDpiLayoutPending = false;
    }

    private void MdiOwnerDpiChanged(object? sender, DpiChangedEventArgs e)
    {
        var owner = _mdiMenuOwner;
        if (_mdiDpiLayoutPending || owner == null || !owner.IsHandleCreated || owner.Disposing) return;
        _mdiDpiLayoutPending = true;
        int generation = _mdiLayoutGeneration;
        // Form raises DpiChanged before scaling its child HWNDs. Ask the native
        // MDI client to re-fit the maximized child only after that scaling settles.
        owner.BeginInvoke((MethodInvoker)(() =>
        {
            if (generation != _mdiLayoutGeneration || owner.IsDisposed || owner.Disposing) return;
            _mdiDpiLayoutPending = false;
            if (owner.ActiveMdiChild is not { WindowState: FormWindowState.Maximized }) return;
            owner.PerformLayout();
            var client = owner.Controls.OfType<MdiClient>().FirstOrDefault();
            if (client is not { IsHandleCreated: true }) return;
            SendMdiSize(client.Handle, 0x0005, IntPtr.Zero,
                new IntPtr((client.ClientSize.Height << 16) | (client.ClientSize.Width & 0xffff)));
        }));
    }

    [DllImport("user32.dll", EntryPoint = "SendMessageW")]
    private static extern IntPtr SendMdiSize(IntPtr window, uint message, IntPtr wParam, IntPtr lParam);

    private void ReleaseMdiMenuMode()
    {
        var owner = _mdiMenuOwner;
        var bridge = _mdiMenuBridge;
        _mdiMenuOwner = null;
        _mdiMenuBridge = null;
        _mdiLayoutGeneration++;
        _mdiDpiLayoutPending = false;
        if (owner != null)
        {
            owner.Disposed -= MdiOwnerDisposed;
            owner.DpiChanged -= MdiOwnerDpiChanged;
            owner.HandleDestroyed -= MdiOwnerHandleDestroyed;
            if (!owner.IsDisposed && !owner.Disposing && ReferenceEquals(owner.MainMenuStrip, bridge))
                owner.MainMenuStrip = null;
        }
        bridge?.Dispose();
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _mdiDisposed = true;
            ReleaseMdiMenuMode();
        }
        base.Dispose(disposing);
    }
}
