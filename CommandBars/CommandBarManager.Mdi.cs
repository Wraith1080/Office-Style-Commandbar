using System.Windows.Forms;

namespace CommandBars;

public partial class CommandBarManager
{
    private Form? _mdiMenuOwner;
    private MenuStrip? _mdiMenuBridge;
    private bool _mdiDisposed;

    // WinForms otherwise installs a dummy native menu for maximized MDI children.
    // An unsited MenuStrip selects its managed-menu path without adding another
    // visible row. Keep it at manager scope so docking/rebuilds don't recreate it.
    internal void EnsureMdiMenuMode(Form owner)
    {
        if (_mdiDisposed || DesignMode || owner.IsDisposed || owner.Disposing) return;
        if (ReferenceEquals(owner, _mdiMenuOwner)) return;
        ReleaseMdiMenuMode();
        if (owner.MainMenuStrip != null) return; // application-owned strips remain untouched
        _mdiMenuOwner = owner;
        _mdiMenuBridge = new MenuStrip { Visible = false, AllowMerge = false };
        owner.Disposed += MdiOwnerDisposed;
        owner.MainMenuStrip = _mdiMenuBridge;
    }

    private void MdiOwnerDisposed(object? sender, EventArgs e) => ReleaseMdiMenuMode();

    private void ReleaseMdiMenuMode()
    {
        var owner = _mdiMenuOwner;
        var bridge = _mdiMenuBridge;
        _mdiMenuOwner = null;
        _mdiMenuBridge = null;
        if (owner != null)
        {
            owner.Disposed -= MdiOwnerDisposed;
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
