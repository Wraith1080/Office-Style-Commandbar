using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;
using CommandBars.Model;
using CommandBars.Rendering;

namespace CommandBars.Controls;

/// <summary>
/// A borderless popup window that renders a popup <see cref="CommandBar"/> as a
/// vertical menu using the parent's renderer. It is <b>non-activating</b>: the
/// owner form keeps focus (its title bar stays active) while the menu is open.
/// Closing is coordinated by <see cref="MenuSession"/>.
/// </summary>
public sealed class CommandBarPopupWindow : Form
{
    private const int SeparatorHeight = 3;
    private const int ShortcutGap = 20;
    private const int ArrowColumn = 14;

    private const int WS_EX_NOACTIVATE = 0x08000000;
    private const int WS_EX_TOOLWINDOW = 0x00000080;
    private const int WM_MOUSEACTIVATE = 0x0021;
    private const int MA_NOACTIVATE = 3;

    private readonly CommandBar _bar;
    private readonly CommandBarRenderer _renderer;
    private readonly Font _menuFont;
    private readonly int _iconSize;
    private readonly float _dpiScale;
    private readonly int _iconPx;

    private readonly int _marginWidth;
    private readonly int _rowHeight;
    private readonly int _textX;
    private readonly int _sepHeight;
    private readonly int _shortcutGap;
    private readonly int _arrowColumn;

    private CommandBarItem? _hotItem;
    private CommandBarPopupWindow? _child;
    private CommandBarPopupItem? _childItem;

    public CommandBarPopupWindow(CommandBar bar, CommandBarRenderer renderer, Font font, int iconSize, float dpiScale)
    {
        _bar = bar ?? throw new ArgumentNullException(nameof(bar));
        _renderer = renderer ?? throw new ArgumentNullException(nameof(renderer));
        _menuFont = font ?? SystemFonts.MenuFont!;
        _iconSize = iconSize > 0 ? iconSize : IconSizes.Default;
        _dpiScale = dpiScale <= 0 ? 1f : dpiScale;
        _iconPx = (int)Math.Round(_iconSize * _dpiScale);

        _marginWidth = _iconPx + R(8);
        _rowHeight = Math.Max(_iconPx, _menuFont.Height) + R(6);
        _textX = _marginWidth + R(6);
        _sepHeight = R(SeparatorHeight);
        _shortcutGap = R(ShortcutGap);
        _arrowColumn = R(ArrowColumn);

        FormBorderStyle = FormBorderStyle.None;
        ShowInTaskbar = false;
        StartPosition = FormStartPosition.Manual;
        DoubleBuffered = true;
        SetStyle(ControlStyles.ResizeRedraw, true);

        BuildLayout();
    }

    // Do not activate when shown — keep the owner form focused.
    protected override bool ShowWithoutActivation => true;

    protected override CreateParams CreateParams
    {
        get
        {
            var cp = base.CreateParams;
            cp.ExStyle |= WS_EX_NOACTIVATE | WS_EX_TOOLWINDOW;
            return cp;
        }
    }

    protected override void WndProc(ref Message m)
    {
        // Clicking the menu must not activate it (which would deactivate the
        // owner form). Tell Windows not to activate on mouse-down.
        if (m.Msg == WM_MOUSEACTIVATE)
        {
            m.Result = (IntPtr)MA_NOACTIVATE;
            return;
        }
        base.WndProc(ref m);
    }

    /// <summary>Positions the window at a screen point (clamped on-screen) and shows it.</summary>
    public void ShowAt(Point screenAnchor)
    {
        Rectangle wa = Screen.FromPoint(screenAnchor).WorkingArea;
        int x = Math.Min(screenAnchor.X, wa.Right - Width);
        int y = Math.Min(screenAnchor.Y, wa.Bottom - Height);
        Location = new Point(Math.Max(wa.Left, x), Math.Max(wa.Top, y));
        Show();
    }

    private int R(int value) => (int)Math.Round(value * _dpiScale);

    private void BuildLayout()
    {
        using var bmp = new Bitmap(1, 1);
        bmp.SetResolution(96f * _dpiScale, 96f * _dpiScale);
        using var g = Graphics.FromImage(bmp);

        int maxText = 0;
        int maxShortcut = 0;
        bool anySubmenu = false;

        foreach (var item in _bar.Items)
        {
            if (!item.Visible)
                continue;
            switch (item)
            {
                case CommandBarCommandItem cmd:
                    maxText = Math.Max(maxText, BarLayoutEngine.MeasureText(g, cmd.Command.Text, _menuFont));
                    string sc = FormatShortcut(cmd.Command.Shortcut);
                    if (sc.Length > 0)
                        maxShortcut = Math.Max(maxShortcut, BarLayoutEngine.MeasureText(g, sc, _menuFont));
                    break;
                case CommandBarPopupItem popup:
                    maxText = Math.Max(maxText, BarLayoutEngine.MeasureText(g, popup.Text, _menuFont));
                    anySubmenu = true;
                    break;
            }
        }

        int width = _textX + maxText;
        if (maxShortcut > 0)
            width += _shortcutGap + maxShortcut;
        width += (anySubmenu ? _arrowColumn : R(8)) + R(8);
        width = Math.Max(width, R(150));

        int y = R(3);
        foreach (var item in _bar.Items)
        {
            if (!item.Visible)
            {
                item.Bounds = Rectangle.Empty;
                continue;
            }
            int h = item is CommandBarSeparator ? _sepHeight : _rowHeight;
            item.Bounds = new Rectangle(0, y, width, h);
            y += h;
        }

        ClientSize = new Size(width, y + R(3));
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        var g = e.Graphics;
        _renderer.Scale = _dpiScale;
        _renderer.DrawMenuBackground(g, ClientRectangle);
        _renderer.DrawImageMargin(g, new Rectangle(1, 1, _marginWidth, ClientSize.Height - 2));

        foreach (var item in _bar.Items)
        {
            if (!item.Visible || item.Bounds.IsEmpty)
                continue;
            DrawMenuItem(g, item);
        }
    }

    private void DrawMenuItem(Graphics g, CommandBarItem item)
    {
        Rectangle b = item.Bounds;

        if (item is CommandBarSeparator)
        {
            _renderer.DrawSeparator(g,
                new Rectangle(_marginWidth + 2, b.Y, b.Width - _marginWidth - 6, b.Height),
                BarOrientation.Vertical);
            return;
        }

        bool enabled = item is not CommandBarCommandItem cmdItem || cmdItem.Command.Enabled;
        var state = RenderState.Normal;
        if (!enabled)
            state = RenderState.Disabled;
        else if (ReferenceEquals(item, _hotItem))
            state = RenderState.Hot;

        // Height - 1 so the highlight's top and bottom edges sit an equal
        // distance from the centered check/image box (integer centering biases
        // the box up by a pixel, which otherwise makes the lower gap look larger).
        _renderer.DrawMenuItemBackground(g, new Rectangle(2, b.Y, b.Width - 4, b.Height - 1), state);

        if (item is CommandBarCommandItem cmd)
        {
            bool isChecked = cmd is CommandBarToggleButton { Checked: true };
            bool hasImage = cmd.Command.Image is not null;

            // A square box around the icon, centered within the image margin so
            // it never spills into the text column.
            int boxSize = Math.Min(_marginWidth, _iconPx + R(4));
            var iconBox = new Rectangle(
                2 + ((_marginWidth - boxSize) / 2),
                b.Y + ((b.Height - boxSize) / 2),
                boxSize, boxSize);

            // A checked item gets the orange "pressed" box in the icon margin,
            // just like a toggled-on toolbar button — but not while hovered,
            // where the row's own highlight reads cleaner on its own.
            if (isChecked && (state & RenderState.Hot) == 0)
                _renderer.DrawButton(g, iconBox, RenderState.Checked, BarOrientation.Horizontal);

            if (hasImage)
            {
                var image = cmd.Command.Image!.GetImage(_iconSize, _dpiScale);
                int imgX = 2 + ((_marginWidth - _iconPx) / 2);
                int imgY = b.Y + ((b.Height - _iconPx) / 2);
                _renderer.DrawItemImage(g, image, new Rectangle(imgX, imgY, _iconPx, _iconPx), state);
            }
            else if (isChecked)
            {
                // No icon: a check mark sits on the orange box.
                _renderer.DrawMenuCheck(g, iconBox, state);
            }

            _renderer.DrawItemText(g, cmd.Command.Text, _menuFont,
                new Rectangle(_textX, b.Y, b.Width - _textX - R(8), b.Height), state,
                TextFormatFlags.Left | TextFormatFlags.VerticalCenter | BarLayoutEngine.MeasureFlags);

            string shortcut = FormatShortcut(cmd.Command.Shortcut);
            if (shortcut.Length > 0)
            {
                _renderer.DrawItemText(g, shortcut, _menuFont,
                    new Rectangle(_textX, b.Y, b.Width - _textX - _arrowColumn - R(6), b.Height), state,
                    TextFormatFlags.Right | TextFormatFlags.VerticalCenter | BarLayoutEngine.MeasureFlags);
            }
        }
        else if (item is CommandBarPopupItem popup)
        {
            _renderer.DrawItemText(g, popup.Text, _menuFont,
                new Rectangle(_textX, b.Y, b.Width - _textX - _arrowColumn, b.Height), state,
                TextFormatFlags.Left | TextFormatFlags.VerticalCenter | BarLayoutEngine.MeasureFlags);
            DrawSubmenuArrow(g, new Rectangle(b.Right - _arrowColumn, b.Y, _arrowColumn, b.Height), state);
        }
    }

    private void DrawSubmenuArrow(Graphics g, Rectangle bounds, RenderState state)
    {
        Color color = (state & RenderState.Disabled) != 0 ? _renderer.Colors.DisabledText : _renderer.Colors.Text;
        int cx = bounds.X + bounds.Width / 2;
        int cy = bounds.Y + bounds.Height / 2;
        Point[] arrow = { new(cx - 1, cy - 3), new(cx - 1, cy + 3), new(cx + 3, cy) };
        var previous = g.SmoothingMode;
        g.SmoothingMode = SmoothingMode.None;
        using var brush = new SolidBrush(color);
        g.FillPolygon(brush, arrow);
        g.SmoothingMode = previous;
    }

    // --- Interaction -------------------------------------------------------

    private CommandBarItem? HitTest(Point p)
    {
        foreach (var item in _bar.Items)
        {
            if (!item.Visible || item.Bounds.IsEmpty)
                continue;
            if (item is CommandBarSeparator)
                continue;
            if (item.Bounds.Contains(p))
                return item;
        }
        return null;
    }

    protected override void OnMouseMove(MouseEventArgs e)
    {
        base.OnMouseMove(e);
        var item = HitTest(e.Location);
        if (ReferenceEquals(item, _hotItem))
            return;

        _hotItem = item;
        Invalidate();

        // Moving to a different item: close any open submenu and, if the new
        // item is itself a submenu, open it. This keeps only one submenu open.
        if (!ReferenceEquals(item, _childItem))
        {
            if (item is CommandBarPopupItem popup)
                OpenChild(popup);
            else
                CloseChild();
        }
    }

    protected override void OnMouseLeave(EventArgs e)
    {
        base.OnMouseLeave(e);
        // Keep the open submenu; the pointer may be moving into it.
        _hotItem = null;
        Invalidate();
    }

    protected override void OnMouseUp(MouseEventArgs e)
    {
        base.OnMouseUp(e);
        if (e.Button != MouseButtons.Left)
            return;

        var item = HitTest(e.Location);
        switch (item)
        {
            case CommandBarCommandItem cmd when cmd.Command.Enabled:
                cmd.Command.Perform(); // latches checkable commands itself
                MenuSession.Current?.End();
                break;

            case CommandBarPopupItem popup:
                if (!ReferenceEquals(_childItem, popup))
                    OpenChild(popup);
                break;
        }
    }

    private CommandBarPopupWindow OpenChild(CommandBarPopupItem popup)
    {
        CloseChild();

        var anchor = PointToScreen(new Point(popup.Bounds.Right - 3, popup.Bounds.Top));
        var child = new CommandBarPopupWindow(popup.DropDown, _renderer, _menuFont, _iconSize, _dpiScale) { Owner = Owner };
        _child = child;
        _childItem = popup;
        child.FormClosed += (_, _) =>
        {
            if (ReferenceEquals(_child, child))
            {
                _child = null;
                _childItem = null;
            }
        };

        MenuSession.Current?.Add(child);
        child.ShowAt(anchor);
        return child;
    }

    private void CloseChild()
    {
        var child = _child;
        _child = null;
        _childItem = null;
        if (child is not null && !child.IsDisposed)
            child.Close();
    }

    protected override void OnFormClosed(FormClosedEventArgs e)
    {
        CloseChild();
        base.OnFormClosed(e);
    }

    // --- Keyboard navigation (driven by MenuSession) -----------------------

    /// <summary>The currently highlighted item, if any.</summary>
    internal CommandBarItem? HotItem => _hotItem;

    /// <summary>True if the highlighted item opens a submenu.</summary>
    internal bool HotIsSubmenu => _hotItem is CommandBarPopupItem;

    /// <summary>Highlights the first selectable item.</summary>
    internal void SelectFirst()
    {
        var nav = NavigableItems();
        _hotItem = nav.Count > 0 ? nav[0] : null;
        Invalidate();
    }

    /// <summary>Moves the highlight by <paramref name="delta"/>, wrapping around.</summary>
    internal void MoveHot(int delta)
    {
        var nav = NavigableItems();
        if (nav.Count == 0)
            return;
        int idx = _hotItem is null ? -1 : nav.IndexOf(_hotItem);
        idx = idx < 0
            ? (delta >= 0 ? 0 : nav.Count - 1)
            : (((idx + delta) % nav.Count) + nav.Count) % nav.Count;
        _hotItem = nav[idx];
        Invalidate();
    }

    /// <summary>Activates the highlighted item (performs it or opens its submenu).</summary>
    internal void ActivateHot()
    {
        switch (_hotItem)
        {
            case CommandBarPopupItem popup:
                OpenChild(popup).SelectFirst();
                break;
            case CommandBarCommandItem cmd when cmd.Command.Enabled:
                cmd.Command.Perform();
                MenuSession.Current?.End();
                break;
        }
    }

    /// <summary>
    /// Selects and activates the first item whose label has the given mnemonic
    /// (its underlined letter) — opening a submenu or performing a command.
    /// Returns true if a match was handled.
    /// </summary>
    internal bool ActivateMnemonic(char c)
    {
        foreach (var item in NavigableItems())
        {
            string? text = item switch
            {
                CommandBarPopupItem popup => popup.Text,
                CommandBarCommandItem cmd => cmd.Command.Text,
                _ => null,
            };
            if (text is not null && Control.IsMnemonic(c, text))
            {
                _hotItem = item;
                Invalidate();
                ActivateHot();
                return true;
            }
        }
        return false;
    }

    private List<CommandBarItem> NavigableItems()
    {
        var list = new List<CommandBarItem>();
        foreach (var item in _bar.Items)
        {
            if (!item.Visible || item.Bounds.IsEmpty || item is CommandBarSeparator)
                continue;
            list.Add(item);
        }
        return list;
    }

    private static string FormatShortcut(Keys keys)
    {
        if (keys == Keys.None)
            return string.Empty;
        try
        {
            return new KeysConverter().ConvertToString(keys) ?? string.Empty;
        }
        catch
        {
            return string.Empty;
        }
    }
}
