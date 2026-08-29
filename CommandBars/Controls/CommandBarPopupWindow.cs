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
    // A menu separator is a two-pixel dark/light line. An even-height row leaves
    // the same number of blank pixels above and below that line pair.
    private const int SeparatorHeight = 4;
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

    private readonly bool _showImageMargin;
    private readonly int _marginWidth;
    private readonly int _rowHeight;
    private readonly int _textX;
    private readonly int _sepHeight;
    private readonly int _shortcutGap;
    private readonly int _arrowColumn;

    private CommandBarItem? _hotItem;
    private CommandBarPopupWindow? _child;
    private CommandBarPopupItem? _childItem;
    private bool _openSubmenusToLeft;
    private Rectangle _connectionGap;

    /// <summary>The edge of the owner item currently joined to this popup.</summary>
    internal PopupConnectionEdge AnchorConnectionEdge { get; private set; }

    // Tear-off: when the popup's bar opts in (CommandBar.AllowTearOff) and a
    // handler is supplied, the popup reserves a top grip strip that the user can
    // drag to float the menu into a standalone palette (see TearOffWindow).
    private readonly Action<CommandBar, Point>? _tearOff;
    private readonly int _gripHeight;
    private ToolTip? _gripTip;
    private bool _gripHot;
    private bool _tearArmed;
    private Point _tearStart;

    public CommandBarPopupWindow(CommandBar bar, CommandBarRenderer renderer, Font font, int iconSize, float dpiScale,
        Action<CommandBar, Point>? tearOff = null)
    {
        _bar = bar ?? throw new ArgumentNullException(nameof(bar));
        _renderer = renderer ?? throw new ArgumentNullException(nameof(renderer));
        _menuFont = font ?? SystemFonts.MenuFont!;
        _iconSize = iconSize > 0 ? iconSize : IconSizes.Default;
        _dpiScale = dpiScale <= 0 ? 1f : dpiScale;
        _iconPx = (int)Math.Round(_iconSize * _dpiScale);
        _tearOff = tearOff;

        _showImageMargin = NeedsImageMargin(_bar);
        _marginWidth = _showImageMargin ? _iconPx + R(8) : 0;
        _rowHeight = Math.Max(_iconPx, _menuFont.Height) + R(6);
        _textX = _showImageMargin ? _marginWidth + R(6) : R(8);
        _sepHeight = R(SeparatorHeight);
        if ((_sepHeight & 1) != 0)
            _sepHeight++; // keep the scaled separator row even
        _shortcutGap = R(ShortcutGap);
        _arrowColumn = R(ArrowColumn);
        _gripHeight = HasGrip ? R(9) : 0;
        if (HasGrip)
            _gripTip = new ToolTip { InitialDelay = 400, ReshowDelay = 100, AutoPopDelay = 4000 };

        FormBorderStyle = FormBorderStyle.None;
        ShowInTaskbar = false;
        StartPosition = FormStartPosition.Manual;
        DoubleBuffered = true;
        SetStyle(ControlStyles.ResizeRedraw, true);

        BuildLayout();
    }

    /// <summary>True when this popup shows a tear-off grip.</summary>
    private bool InteractionBlocked => _bar.Manager?.IsCustomizing ?? false;

    private bool HasGrip => _tearOff is not null && _bar.AllowTearOff && !InteractionBlocked;

    /// <summary>Whether the tear-off grip is currently interactive.</summary>
    internal bool TearOffEnabled => HasGrip;

    // Linear menus keep Office's conventional icon/check gutter. A grid palette
    // only keeps it when one of its full-width rows actually needs that column;
    // the icon-only buttons packed into grid cells do not count.
    private static bool NeedsImageMargin(CommandBar bar)
    {
        if (bar.PaletteColumns <= 0)
            return true;

        foreach (var item in bar.Items)
        {
            if (!item.Visible || BarLayoutEngine.IsSwatch(item))
                continue;

            if (item is CommandBarPopupItem { Image: not null })
                return true;

            if (item is CommandBarCommandItem command &&
                (command.Command.Image is not null || item is CommandBarToggleButton))
                return true;
        }

        return false;
    }

    /// <summary>The grip strip at the very top of the popup (empty when no grip).</summary>
    private Rectangle GripRect =>
        HasGrip ? new Rectangle(1, 1, Math.Max(1, ClientSize.Width - 2), _gripHeight) : Rectangle.Empty;

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
        AnchorConnectionEdge = PopupConnectionEdge.None;
        _connectionGap = Rectangle.Empty;
        Show();
    }

    /// <summary>
    /// Shows the popup beside an anchor rectangle. The requested side is used
    /// when it fits; otherwise the popup flips to the other side. If neither
    /// side has enough room, the side with more visible working-area space wins.
    /// </summary>
    public void ShowBeside(Rectangle screenAnchor, bool preferLeft, int overlap = 0,
        bool connectToAnchor = true)
    {
        Rectangle wa = Screen.FromRectangle(screenAnchor).WorkingArea;
        int leftSpace = screenAnchor.Left - wa.Left + overlap;
        int rightSpace = wa.Right - screenAnchor.Right + overlap;
        bool leftFits = Width <= leftSpace;
        bool rightFits = Width <= rightSpace;

        bool openLeft = preferLeft
            ? leftFits || (!rightFits && leftSpace >= rightSpace)
            : !rightFits && (leftFits || leftSpace > rightSpace);

        int seamOverlap = connectToAnchor ? Math.Max(R(1), overlap) : Math.Max(0, overlap);
        int x = openLeft
            ? screenAnchor.Left - Width + seamOverlap
            : screenAnchor.Right - seamOverlap;
        int y = screenAnchor.Top;

        Location = ClampToWorkingArea(new Point(x, y), wa);
        _openSubmenusToLeft = openLeft;
        if (connectToAnchor)
        {
            AnchorConnectionEdge = openLeft ? PopupConnectionEdge.Left : PopupConnectionEdge.Right;
            SetConnectionGap(screenAnchor, openLeft ? PopupConnectionEdge.Right : PopupConnectionEdge.Left);
        }
        else
        {
            AnchorConnectionEdge = PopupConnectionEdge.None;
            _connectionGap = Rectangle.Empty;
        }
        Show();
    }

    /// <summary>
    /// Shows the popup above or below an anchor rectangle, flipping vertically
    /// when the preferred side does not fit in the monitor's working area.
    /// </summary>
    public void ShowBelow(Rectangle screenAnchor, bool preferBelow,
        bool connectToAnchor = true)
    {
        Rectangle wa = Screen.FromRectangle(screenAnchor).WorkingArea;
        int aboveSpace = screenAnchor.Top - wa.Top;
        int belowSpace = wa.Bottom - screenAnchor.Bottom;
        bool aboveFits = Height <= aboveSpace;
        bool belowFits = Height <= belowSpace;

        bool openBelow = preferBelow
            ? belowFits || (!aboveFits && belowSpace >= aboveSpace)
            : !aboveFits && (belowFits || belowSpace > aboveSpace);

        int seamOverlap = connectToAnchor ? R(1) : 0;
        int y = openBelow
            ? screenAnchor.Bottom - seamOverlap
            : screenAnchor.Top - Height + seamOverlap;
        Location = ClampToWorkingArea(new Point(screenAnchor.Left, y), wa);

        // Horizontal root menus normally cascade right, but starting near the
        // monitor's right edge makes leftward submenus a better default.
        int leftSpace = screenAnchor.Left - wa.Left;
        int rightSpace = wa.Right - screenAnchor.Right;
        _openSubmenusToLeft = rightSpace < Width && leftSpace > rightSpace;
        if (connectToAnchor)
        {
            AnchorConnectionEdge = openBelow ? PopupConnectionEdge.Bottom : PopupConnectionEdge.Top;
            SetConnectionGap(screenAnchor, openBelow ? PopupConnectionEdge.Top : PopupConnectionEdge.Bottom);
        }
        else
        {
            AnchorConnectionEdge = PopupConnectionEdge.None;
            _connectionGap = Rectangle.Empty;
        }
        Show();
    }

    private void SetConnectionGap(Rectangle screenAnchor, PopupConnectionEdge popupEdge)
    {
        Rectangle popup = new(Location, Size);
        int inset = Math.Max(1, R(1));

        if (popupEdge is PopupConnectionEdge.Top or PopupConnectionEdge.Bottom)
        {
            int left = Math.Max(screenAnchor.Left, popup.Left) - popup.Left + inset;
            int right = Math.Min(screenAnchor.Right, popup.Right) - popup.Left - inset;
            int y = popupEdge == PopupConnectionEdge.Top ? 0 : ClientSize.Height - 1;
            _connectionGap = right > left
                ? Rectangle.FromLTRB(left, y, right, y + 1)
                : Rectangle.Empty;
        }
        else
        {
            int top = Math.Max(screenAnchor.Top, popup.Top) - popup.Top + inset;
            int bottom = Math.Min(screenAnchor.Bottom, popup.Bottom) - popup.Top - inset;
            int x = popupEdge == PopupConnectionEdge.Left ? 0 : ClientSize.Width - 1;
            _connectionGap = bottom > top
                ? Rectangle.FromLTRB(x, top, x + 1, bottom)
                : Rectangle.Empty;
        }
    }

    private Point ClampToWorkingArea(Point location, Rectangle workingArea)
    {
        int maxX = Math.Max(workingArea.Left, workingArea.Right - Width);
        int maxY = Math.Max(workingArea.Top, workingArea.Bottom - Height);
        return new Point(
            Math.Clamp(location.X, workingArea.Left, maxX),
            Math.Clamp(location.Y, workingArea.Top, maxY));
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

        int cols = _bar.PaletteColumns;
        int swatchCell = _iconPx + R(6);
        if (cols > 0)
            width = Math.Max(width, (cols * swatchCell) + (2 * R(3)));

        int outerInset = R(3);
        int y = outerInset + _gripHeight; // reserve the tear-off grip strip at the top
        if (cols > 0)
        {
            // Swatch palette: pack icon-only buttons into a grid; text items
            // (Automatic, More Colors…), popups and separators take a full row.
            int x0 = R(3);
            int col = 0, rowTop = y;
            foreach (var item in _bar.Items)
            {
                if (!item.Visible) { item.Bounds = Rectangle.Empty; continue; }
                if (BarLayoutEngine.IsSwatch(item))
                {
                    if (col == 0) rowTop = y;
                    item.Bounds = new Rectangle(x0 + (col * swatchCell), rowTop, swatchCell, swatchCell);
                    if (++col >= cols) { col = 0; y = rowTop + swatchCell; }
                }
                else
                {
                    if (col > 0) { col = 0; y = rowTop + swatchCell; }
                    int hh = item is CommandBarSeparator ? _sepHeight : _rowHeight;
                    item.Bounds = new Rectangle(0, y, width, hh);
                    y += hh;
                }
            }
            if (col > 0) y = rowTop + swatchCell;
        }
        else
        {
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
        }

        // Item highlights intentionally use Height - 1 below. Compensate with a
        // bottom inset one pixel smaller than the raw top inset, so the visible
        // space between the first/last highlight and the menu border is equal.
        int bottomInset = Math.Max(1, outerInset - 1);
        ClientSize = new Size(width, y + bottomInset);
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        var g = e.Graphics;
        _renderer.Scale = _dpiScale;
        _renderer.DrawMenuBackground(g, ClientRectangle);

        // Remove only the border segment directly touching the owner button.
        // The remaining outline and the owner's other three edges read as one
        // continuous Office-style button-and-popup shape.
        if (!_connectionGap.IsEmpty)
        {
            using var seam = new SolidBrush(_renderer.Colors.MenuBackground);
            g.FillRectangle(seam, _connectionGap);
        }

        // Grid palettes use the entire popup surface unless one of their
        // full-width rows genuinely needs an icon/check column.
        if (_showImageMargin)
        {
            int marginTop = 1 + _gripHeight;
            _renderer.DrawImageMargin(g,
                new Rectangle(1, marginTop, _marginWidth, ClientSize.Height - marginTop - 1));
        }

        if (HasGrip)
            DrawGrip(g, GripRect);

        foreach (var item in _bar.Items)
        {
            if (!item.Visible || item.Bounds.IsEmpty)
                continue;
            DrawMenuItem(g, item);
        }
    }

    // The Office tear-off handle: a slim raised strip with two dotted rows,
    // highlighted while hovered. Dragging it floats the menu (see OnMouseMove).
    private void DrawGrip(Graphics g, Rectangle grip)
    {
        if (grip.Width <= 2 || grip.Height <= 2)
            return;
        var colors = _renderer.Colors;
        using (var back = new SolidBrush(_gripHot ? colors.MenuItemSelectedBegin : colors.ImageMarginBegin))
            g.FillRectangle(back, grip);
        using (var edge = new Pen(_gripHot ? colors.MenuItemSelectedBorder : colors.SeparatorDark))
            g.DrawLine(edge, grip.Left + 2, grip.Bottom - 1, grip.Right - 3, grip.Bottom - 1);

        // Two dotted rows of the move-handle, centered vertically.
        using var dot = new SolidBrush(colors.Text);
        int cy = grip.Top + (grip.Height / 2);
        int step = Math.Max(3, R(3));
        for (int x = grip.Left + 4; x < grip.Right - 4; x += step)
        {
            g.FillRectangle(dot, x, cy - 2, 1, 1);
            g.FillRectangle(dot, x, cy + 1, 1, 1);
        }
    }

    private void DrawMenuItem(Graphics g, CommandBarItem item)
    {
        Rectangle b = item.Bounds;

        // Colour swatch: fill the cell flat with the button's colour image, a
        // selection border on hover — no menu-row chrome, no text.
        if (_bar.PaletteColumns > 0 && BarLayoutEngine.IsSwatch(item) && item is CommandBarCommandItem swatch)
        {
            var img = swatch.Command.Image!.GetImage(_iconSize, _dpiScale);
            _renderer.DrawItemImage(g, img, Rectangle.Inflate(b, -R(2), -R(2)), RenderState.Normal);
            if (ReferenceEquals(item, _hotItem))
            {
                using var pen = new Pen(_renderer.Colors.MenuItemSelectedBorder);
                g.DrawRectangle(pen, b.X + 1, b.Y + 1, b.Width - 3, b.Height - 3);
            }
            return;
        }

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
        bool hasClassicGutterContent = item switch
        {
            CommandBarCommandItem commandItem => commandItem.Command.Image is not null ||
                commandItem is CommandBarToggleButton { Checked: true },
            CommandBarPopupItem popupItem => popupItem.Image is not null,
            _ => false,
        };
        int selectionX;
        if (_renderer.UsesClassicMenuItemChrome && _showImageMargin)
        {
            // Icon/check rows keep one gray divider pixel after their raised
            // bevel. Iconless rows have no empty gutter: selection starts at
            // the exact X where that bevel's left edge would have appeared.
            selectionX = hasClassicGutterContent
                ? _marginWidth + R(3)
                : R(2);
        }
        else
        {
            selectionX = R(3);
        }
        _renderer.DrawMenuItemBackground(g,
            new Rectangle(selectionX, b.Y, b.Right - selectionX - R(3), b.Height - 1), state);

        if (item is CommandBarCommandItem cmd)
        {
            bool isChecked = cmd is CommandBarToggleButton { Checked: true };
            bool hasImage = cmd.Command.Image is not null;

            // A square box around the icon, centered within the image margin so
            // it never spills into the text column.
            int boxSize = Math.Min(_marginWidth, _iconPx + R(4));
            var iconBox = MenuIconBox(b, boxSize);

            bool hot = (state & RenderState.Hot) != 0;
            if (_renderer.UsesClassicMenuItemChrome)
            {
                // Office 2000 keeps row selection out of the icon gutter. An
                // ordinary hot icon becomes a raised toolbar button; a checked
                // icon temporarily loses its hatch and becomes a plain sunken
                // button while hovered.
                if (hot && (isChecked || hasImage))
                    _renderer.DrawButton(g, iconBox,
                        isChecked ? RenderState.Pressed : RenderState.Hot,
                        BarOrientation.Horizontal);
                else if (isChecked)
                    _renderer.DrawButton(g, iconBox, RenderState.Checked,
                        BarOrientation.Horizontal);
            }
            else if (isChecked && !hot)
            {
                _renderer.DrawButton(g, iconBox, RenderState.Checked, BarOrientation.Horizontal);
            }

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

            _renderer.DrawMenuItemText(g, cmd.Command.Text, _menuFont,
                new Rectangle(_textX, b.Y, b.Width - _textX - R(8), b.Height), state,
                TextFormatFlags.Left | TextFormatFlags.VerticalCenter | BarLayoutEngine.MeasureFlags);

            string shortcut = FormatShortcut(cmd.Command.Shortcut);
            if (shortcut.Length > 0)
            {
                _renderer.DrawMenuItemText(g, shortcut, _menuFont,
                    new Rectangle(_textX, b.Y, b.Width - _textX - _arrowColumn - R(6), b.Height), state,
                    TextFormatFlags.Right | TextFormatFlags.VerticalCenter | BarLayoutEngine.MeasureFlags);
            }
        }
        else if (item is CommandBarPopupItem popup)
        {
            if (popup.Image is not null)
            {
                if (_renderer.UsesClassicMenuItemChrome && (state & RenderState.Hot) != 0)
                {
                    int boxSize = Math.Min(_marginWidth, _iconPx + R(4));
                    var iconBox = MenuIconBox(b, boxSize);
                    _renderer.DrawButton(g, iconBox, RenderState.Hot,
                        BarOrientation.Horizontal);
                }
                var image = popup.Image.GetImage(_iconSize, _dpiScale);
                int imgX = 2 + ((_marginWidth - _iconPx) / 2);
                int imgY = b.Y + ((b.Height - _iconPx) / 2);
                _renderer.DrawItemImage(g, image, new Rectangle(imgX, imgY, _iconPx, _iconPx), state);
            }

            _renderer.DrawMenuItemText(g, popup.Text, _menuFont,
                new Rectangle(_textX, b.Y, b.Width - _textX - _arrowColumn, b.Height), state,
                TextFormatFlags.Left | TextFormatFlags.VerticalCenter | BarLayoutEngine.MeasureFlags);
            DrawSubmenuArrow(g, new Rectangle(b.Right - _arrowColumn, b.Y, _arrowColumn, b.Height), state);
        }
    }

    private Rectangle MenuIconBox(Rectangle rowBounds, int compactSize)
    {
        if (_renderer.UsesClassicMenuItemChrome)
        {
            // DrawButton applies the Office 2000 one-pixel inset. Expand the
            // input by that pixel so the resulting raised/sunken frame matches
            // the selected text rectangle exactly in height and sits directly
            // beside it horizontally.
            return new Rectangle(1, rowBounds.Y - R(1),
                _marginWidth + R(2), rowBounds.Height + R(1));
        }

        return new Rectangle(
            2 + ((_marginWidth - compactSize) / 2),
            rowBounds.Y + ((rowBounds.Height - compactSize) / 2),
            compactSize, compactSize);
    }

    private void DrawSubmenuArrow(Graphics g, Rectangle bounds, RenderState state)
    {
        Color color = (state & RenderState.Disabled) != 0
            ? _renderer.Colors.DisabledMenuText
            : (state & RenderState.Hot) != 0
                ? _renderer.Colors.MenuItemSelectedText
                : _renderer.Colors.MenuText;
        Point[] arrow = SubmenuArrowPoints(bounds, _dpiScale);
        var previous = g.SmoothingMode;
        g.SmoothingMode = SmoothingMode.None;
        using var brush = new SolidBrush(color);
        g.FillPolygon(brush, arrow);
        g.SmoothingMode = previous;
    }

    /// <summary>Builds a DPI-scaled right-pointing submenu glyph.</summary>
    internal static Point[] SubmenuArrowPoints(Rectangle bounds, float dpiScale)
    {
        dpiScale = dpiScale <= 0 ? 1f : dpiScale;
        int left = Math.Max(1, (int)Math.Round(2 * dpiScale));
        int right = Math.Max(1, (int)Math.Round(3 * dpiScale));
        int halfHeight = Math.Max(1, (int)Math.Round(4 * dpiScale));
        int cx = bounds.X + bounds.Width / 2;
        int cy = bounds.Y + bounds.Height / 2;
        return new[]
        {
            new Point(cx - left, cy - halfHeight),
            new Point(cx - left, cy + halfHeight),
            new Point(cx + right, cy),
        };
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

    protected override void OnMouseDown(MouseEventArgs e)
    {
        base.OnMouseDown(e);
        if (InteractionBlocked)
            return;
        // Press on the grip arms a tear-off; the actual float begins once the
        // pointer passes the drag threshold (OnMouseMove).
        if (e.Button == MouseButtons.Left && HasGrip && GripRect.Contains(e.Location))
        {
            _tearArmed = true;
            _tearStart = e.Location;
        }
    }

    protected override void OnMouseMove(MouseEventArgs e)
    {
        base.OnMouseMove(e);

        if (InteractionBlocked)
            _tearArmed = false;

        // Tear-off drag: once past the drag threshold, dismiss the menu chain and
        // hand the bar to the tear-off handler, which floats it as a palette.
        if (_tearArmed && e.Button == MouseButtons.Left)
        {
            if (Math.Abs(e.X - _tearStart.X) >= SystemInformation.DragSize.Width ||
                Math.Abs(e.Y - _tearStart.Y) >= SystemInformation.DragSize.Height)
            {
                _tearArmed = false;
                var handler = _tearOff;
                var bar = _bar;
                var screenAt = Cursor.Position; // grab point; the palette follows the cursor
                _gripTip?.Hide(this);
                // Defer: ending the session closes this window, so don't do it
                // inside this window's own mouse event (mirrors FloatingWindow).
                try
                {
                    BeginInvoke((MethodInvoker)(() =>
                    {
                        MenuSession.Current?.End();
                        handler?.Invoke(bar, screenAt);
                    }));
                }
                catch { /* window tearing down */ }
            }
            return;
        }

        if (HasGrip)
        {
            bool onGrip = GripRect.Contains(e.Location);
            if (onGrip != _gripHot)
            {
                _gripHot = onGrip;
                Invalidate(GripRect);
                if (onGrip)
                    _gripTip?.Show("Drag to make this menu float", this, e.X + 12, e.Y + 20, 3000);
                else
                    _gripTip?.Hide(this);
            }
            if (onGrip)
            {
                // Over the grip: clear any item hover but keep an open submenu.
                if (_hotItem is not null) { _hotItem = null; Invalidate(); }
                return;
            }
        }

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
        if (_gripHot)
        {
            _gripHot = false;
            _gripTip?.Hide(this);
            Invalidate(GripRect);
        }
        Invalidate();
    }

    protected override void OnMouseUp(MouseEventArgs e)
    {
        base.OnMouseUp(e);
        _tearArmed = false;
        if (e.Button != MouseButtons.Left)
            return;

        var item = HitTest(e.Location);
        switch (item)
        {
            case CommandBarCommandItem cmd when cmd.Command.Enabled && !InteractionBlocked:
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

        _bar.Manager?.PreparePopup(popup);

        var anchor = RectangleToScreen(popup.Bounds);
        // Pass the tear-off handler down so a submenu can be floated too (Office's
        // AutoShapes: each submenu is itself a tear-off palette).
        var child = new CommandBarPopupWindow(popup.DropDown, _renderer, _menuFont, _iconSize, _dpiScale, _tearOff) { Owner = Owner };
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
        // Nested submenus remain ordinary independent popup windows. Only root
        // menu/dropdown owners use the connected-button treatment.
        child.ShowBeside(anchor, _openSubmenusToLeft, overlap: R(1), connectToAnchor: false);
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
        _gripTip?.Dispose();
        _gripTip = null;
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
            case CommandBarCommandItem cmd when cmd.Command.Enabled && !InteractionBlocked:
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
