using System.Drawing;
using System.Windows.Forms;

namespace CommandBars.Controls;

/// <summary>
/// Coordinates an open chain of <see cref="CommandBarPopupWindow"/>s. Because
/// the popups are non-activating (the owner form keeps focus), we can't rely on
/// a deactivate event to close them. Instead this installs an application
/// message filter that closes the whole chain when the user clicks outside the
/// popups and their anchor, or when the application is deactivated.
/// </summary>
internal sealed class MenuSession : IMessageFilter
{
    private const int WM_KEYDOWN = 0x0100;
    private const int WM_LBUTTONDOWN = 0x0201;
    private const int WM_RBUTTONDOWN = 0x0204;
    private const int WM_MBUTTONDOWN = 0x0207;
    private const int WM_NCLBUTTONDOWN = 0x00A1;
    private const int WM_NCRBUTTONDOWN = 0x00A4;
    private const int WM_ACTIVATEAPP = 0x001C;

    private readonly List<CommandBarPopupWindow> _windows = new();
    private readonly Control _anchor;
    private readonly Rectangle? _anchorBounds;
    private readonly Form? _ownerForm;
    private bool _active;

    private MenuSession(Control anchor, Rectangle? anchorBounds)
    {
        _anchor = anchor;
        _anchorBounds = anchorBounds;
        _ownerForm = anchor.FindForm();
    }

    /// <summary>The session currently open, if any.</summary>
    public static MenuSession? Current { get; private set; }

    /// <summary>
    /// Ends any current session and starts a new one anchored on a control.
    /// <paramref name="anchorScreenBounds"/> narrows the "click is on the anchor"
    /// region to a screen rectangle (e.g. just the overflow chevron) instead of
    /// the whole control, so clicks elsewhere on the control still dismiss it.
    /// </summary>
    public static MenuSession Begin(Control anchor, Rectangle? anchorScreenBounds = null)
    {
        Current?.End();
        var session = new MenuSession(anchor, anchorScreenBounds);
        Application.AddMessageFilter(session);
        if (session._ownerForm is not null)
            session._ownerForm.Deactivate += session.OnOwnerDeactivated;
        session._active = true;
        Current = session;
        return session;
    }

    /// <summary>Registers a popup window with the session.</summary>
    public void Add(CommandBarPopupWindow window)
    {
        _windows.Add(window);
        window.FormClosed += OnWindowClosed;
    }

    /// <summary>Closes every popup in the chain and uninstalls the filter.</summary>
    public void End()
    {
        if (!_active)
            return;
        _active = false;
        Application.RemoveMessageFilter(this);
        if (_ownerForm is not null)
            _ownerForm.Deactivate -= OnOwnerDeactivated;
        if (ReferenceEquals(Current, this))
            Current = null;

        var snapshot = _windows.ToArray();
        _windows.Clear();
        foreach (var window in snapshot)
        {
            window.FormClosed -= OnWindowClosed;
            if (!window.IsDisposed)
                window.Close();
        }
    }

    private void OnWindowClosed(object? sender, FormClosedEventArgs e)
    {
        if (sender is CommandBarPopupWindow window)
            _windows.Remove(window);
        if (_windows.Count == 0)
            End();
    }

    private void OnOwnerDeactivated(object? sender, EventArgs e) => End();

    public bool PreFilterMessage(ref Message m)
    {
        switch (m.Msg)
        {
            case WM_KEYDOWN:
                if (HandleKey((Keys)(int)m.WParam))
                    return true; // consume navigation keys
                break;

            case WM_ACTIVATEAPP:
                if (m.WParam == IntPtr.Zero)
                    End();
                break;

            case WM_LBUTTONDOWN:
            case WM_RBUTTONDOWN:
            case WM_MBUTTONDOWN:
            case WM_NCLBUTTONDOWN:
            case WM_NCRBUTTONDOWN:
                Point p = Cursor.Position;
                if (!IsInsideWindows(p) && !IsInsideAnchor(p))
                    End();
                break;
        }

        return false; // observe only; never swallow the message
    }

    private bool HandleKey(Keys key)
    {
        if (_windows.Count == 0)
            return false;
        var active = _windows[^1];

        switch (key)
        {
            case Keys.Down:
                active.MoveHot(1);
                return true;
            case Keys.Up:
                active.MoveHot(-1);
                return true;
            case Keys.Return:
                active.ActivateHot();
                return true;
            case Keys.Escape:
                CloseTop();
                return true;
            case Keys.Right:
                if (!active.OpenHotSubmenu())
                    SwitchTopMenu(1);
                return true;
            case Keys.Left:
                if (_windows.Count > 1)
                    CloseTop();
                else
                    SwitchTopMenu(-1);
                return true;
            default:
                return TryMnemonic(key);
        }
    }

    // Typing a bare letter/digit while a menu is open activates the item whose
    // label has that mnemonic (opening a submenu or performing a command).
    private bool TryMnemonic(Keys key)
    {
        if ((Control.ModifierKeys & (Keys.Control | Keys.Alt)) != 0)
            return false;
        char c = MnemonicChar(key);
        if (c == '\0' || _windows.Count == 0)
            return false;
        return _windows[^1].ActivateMnemonic(c);
    }

    private static char MnemonicChar(Keys key)
    {
        if (key is >= Keys.A and <= Keys.Z)
            return (char)('A' + (key - Keys.A));
        if (key is >= Keys.D0 and <= Keys.D9)
            return (char)('0' + (key - Keys.D0));
        if (key is >= Keys.NumPad0 and <= Keys.NumPad9)
            return (char)('0' + (key - Keys.NumPad0));
        return '\0';
    }

    /// <summary>Closes just the deepest popup (a submenu), or ends if it was the root.</summary>
    private void CloseTop()
    {
        if (_windows.Count == 0)
            return;
        var window = _windows[^1];
        _windows.RemoveAt(_windows.Count - 1);
        window.FormClosed -= OnWindowClosed;
        if (!window.IsDisposed)
            window.Close();
        if (_windows.Count == 0)
            End();
    }

    private void SwitchTopMenu(int direction)
    {
        if (_anchor is CommandBarControl bar)
            bar.OpenAdjacentTopMenu(direction);
    }

    private bool IsInsideWindows(Point p)
    {
        foreach (var window in _windows)
            if (!window.IsDisposed && window.Visible && window.Bounds.Contains(p))
                return true;
        return false;
    }

    private bool IsInsideAnchor(Point p)
    {
        if (_anchorBounds is { } bounds)
            return bounds.Contains(p);
        if (_anchor.IsDisposed || !_anchor.IsHandleCreated)
            return false;
        return _anchor.RectangleToScreen(_anchor.ClientRectangle).Contains(p);
    }
}
