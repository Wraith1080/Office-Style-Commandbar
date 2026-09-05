using System.ComponentModel;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using CommandBars.Imaging;
using CommandBars.Model;
using CommandBars.Rendering;

namespace CommandBars.Controls;

/// <summary>
/// Hosts a single horizontal <see cref="CommandBar"/>. The menu bar stretches
/// full width and paints a flat gradient; a toolbar paints as a raised rounded
/// chunk with a gripper and, when it does not fit, an overflow chevron. Handles
/// hover/press, fires commands, opens dropdowns, and supports Alt mnemonics.
/// </summary>
[ToolboxItem(false)]
[DesignerCategory("")]
public class CommandBarControl : Control
{
    // Space between the last toolbar item and the overflow chevron nub.
    private const int ChevronGap = 6;

    private CommandBar? _bar;
    private CommandBarRenderer _renderer = new Office2003Renderer();

    private bool _showGripper;
    private bool _gripperHot;
    private bool _hotSplitArrow;
    private int _rowHeight = 1;
    private int _contentWidth;
    private int _colWidth = 1;      // vertical: content-driven cross width
    private int _contentHeight;     // vertical: gripper + items height

    private readonly HashSet<CommandBarItem> _overflowItems = new();

    private CommandBarItem? _hotItem;
    private CommandBarItem? _pressedItem;
    private bool _pressedSplitArrow; // true when a split button's arrow half is pressed
    private bool _chevronHot;
    private bool _chevronPressed;
    private CommandBarPopupItem? _openMenuItem;
    private CommandBarSplitButton? _openSplitButton;
    private bool _overflowOpen;
    private CommandBarPopupWindow? _openWindow;

    // Open combo dropdown (a hosted combo shows a list when clicked).
    private ComboDropDown? _comboWindow;
    private CommandBarComboBox? _pressedCombo;
    private CommandBarComboBox? _hotCombo;   // combo under the mouse (hover effect)
    private CommandBarComboBox? _openCombo;   // combo whose dropdown is currently open

    // Commands this control is subscribed to, so a change made elsewhere (e.g.
    // toggling from a menu) repaints the shared toolbar button immediately.
    private readonly HashSet<Command> _subscribedCommands = new();
    private readonly HashSet<CommandBarComboBox> _subscribedComboBoxes = new();

    // FloatingWindow deliberately uses WS_EX_NOACTIVATE. WinForms otherwise
    // suppresses its ToolTip because the parent window is never active.
    private readonly ToolTip _toolTip = new()
    {
        InitialDelay = 500,
        ReshowDelay = 100,
        AutoPopDelay = 6000,
        ShowAlways = true,
    };
    private CommandBarItem? _tipItem;
    private bool _tipOnChevron;

    /// <summary>Whether ScreenTips may appear over an inactive/floating owner.</summary>
    internal bool ToolTipsShowAlways => _toolTip.ShowAlways;

    private float _dpiScale = 1f;
    private BarMetrics _metrics = BarMetrics.For(1f);
    private int _iconPx = IconSizes.Default;

    // A combo font scaled up with the icon size (see BarLayoutEngine.ComboGrow),
    // so hosted combos grow with the toolbar. Null means "use the control Font"
    // (at the default icon size no separate font is needed); when non-null this
    // control owns it and disposes it. Never dispose the control's own Font.
    private Font? _comboFont;
    private Font ComboFont => _comboFont ?? Font;

    private bool _dragArmed;
    private bool _dragging;
    private Point _dragGrab;

    // Customize-mode item drag (reorder / move between toolbars / remove).
    private bool _itemDragArmed;
    private bool _itemDragging;
    private CommandBarItem? _itemDragItem;
    private Point _itemDragGrab;

    // Keyboard focus (roving highlight while the toolbar has focus).
    private CommandBarItem? _focusItem;
    private bool _focusChevron;

    // Menu-bar mnemonic underlines: momentary, shown only while Alt is physically
    // held. Polled (not message-filtered) so it is reliable through menu mode.
    private System.Windows.Forms.Timer? _altTimer;
    private bool _altHeld;

    [DllImport("user32.dll")]
    private static extern short GetAsyncKeyState(int vKey);

    private const int VK_MENU = 0x12;

    public CommandBarControl()
    {
        SetStyle(
            ControlStyles.OptimizedDoubleBuffer |
            ControlStyles.AllPaintingInWmPaint |
            ControlStyles.UserPaint |
            ControlStyles.ResizeRedraw |
            ControlStyles.SupportsTransparentBackColor |
            ControlStyles.Selectable,
            true);
        Height = 24;
    }

    /// <summary>Raised when an interactive item is activated.</summary>
    public event EventHandler<CommandBarItemClickedEventArgs>? ItemClicked;

    /// <summary>The bar shown by this control.</summary>
    public CommandBar? Bar
    {
        get => _bar;
        set { _bar = value; Relayout(); }
    }

    /// <summary>The renderer used to paint. Defaults to Office 2003.</summary>
    public CommandBarRenderer Renderer
    {
        get => _renderer;
        set { _renderer = value ?? new Office2003Renderer(); Relayout(); }
    }

    /// <summary>True for the menu bar (stretches); false for toolbar chunks.</summary>
    public bool Stretch => _bar is { BarType: CommandBarType.MenuBar };

    /// <summary>True while hosted in a dock band (vs. a floating window).</summary>
    private bool Docked => Parent is DockHost;

    /// <summary>
    /// True when the bar lays out vertically (Left/Right dock). Floating bars
    /// and Top/Bottom bars are horizontal. Palette mode (a torn-off palette)
    /// always lays out horizontally regardless of the popup bar's orientation.
    /// </summary>
    private bool Vertical => !_paletteMode && _bar is not null && _bar.Orientation == BarOrientation.Vertical;

    // True when items should render icon-only (with a text fallback when they
    // have no icon): vertical toolbars do this, and so does a torn-off palette so
    // its buttons stay compact instead of showing "B Bold I Italic …".
    private bool IconOnly => _paletteMode || (_bar is not null && _bar.Orientation == BarOrientation.Vertical);

    // The orientation the control actually paints at — which is NOT the bar's own
    // Orientation in palette mode (a torn-off Popup bar is Vertical but drawn
    // horizontally). Renderer chrome (button gradients, separators) must key off
    // this, or a horizontal palette gets vertical-bar (horizontal) gradients.
    private BarOrientation LayoutOrientation => Vertical ? BarOrientation.Vertical : BarOrientation.Horizontal;

    private bool _paletteMode;

    /// <summary>
    /// When true this control renders a torn-off palette: laid out horizontally
    /// and icon-only, with no gripper/chevron (it is never in a DockHost). Set by
    /// <see cref="TearOffWindow"/>.
    /// </summary>
    public bool PaletteMode
    {
        get => _paletteMode;
        set { if (_paletteMode != value) { _paletteMode = value; Relayout(); } }
    }

    /// <summary>True while the owning manager is in Customize mode.</summary>
    private bool Customizing => _bar?.Manager?.IsCustomizing ?? false;

    /// <summary>
    /// Width the bar would like: items + gripper + insets, plus the always-on
    /// chevron area for toolbars (all DPI-scaled).
    /// </summary>
    public int PreferredContentWidth
        => _contentWidth + (Stretch || !Docked ? 0 : ScaledChevronGap + ScaledChevronExtent);

    /// <summary>
    /// Height a vertical bar would like: gripper + items + insets, plus the
    /// always-on chevron area at the bottom (all DPI-scaled).
    /// </summary>
    public int PreferredContentHeight
        => _contentHeight + (Stretch || !Docked ? 0 : ScaledChevronGap + ScaledChevronExtent);

    /// <summary>
    /// Smallest usable extent along the dock direction. It always preserves the
    /// gripper and chevron, plus any Office-compatible Priority=1 items.
    /// </summary>
    internal int MinimumDockedExtent
    {
        get
        {
            if (Stretch || !Docked)
                return 1;

            int chrome = (_showGripper ? _renderer.GripperExtent : 0)
                + (2 * _metrics.TopInset) + ScaledChevronGap + ScaledChevronExtent;
            if (_bar is null)
                return chrome;

            int protectedExtent = 0;
            foreach (var item in _bar.Items)
            {
                if (item.Visible && item.Priority == 1 && !item.Bounds.IsEmpty)
                    protectedExtent += Vertical ? item.Bounds.Height : item.Bounds.Width;
            }
            return chrome + protectedExtent;
        }
    }

    private int ScaledChevronGap => (int)Math.Round(ChevronGap * _dpiScale);

    // Icon-size-sensitive hit targets (the overflow chevron, mirroring the
    // split-arrow column in BarMetrics) grow with the bar's icon size — never
    // below their base — so they stay easy to click on large toolbars. 1.0 at
    // or below the default icon size.
    private float IconHitScale
        => Math.Max(1f, _iconPx / (Math.Max(0.01f, _dpiScale) * IconSizes.Default));

    // The overflow chevron's reserved extent, scaled up with the icon size.
    private int ScaledChevronExtent => _renderer.UsesFluentMenuChrome
        ? Math.Max(1, Vertical ? _colWidth - 2 : _rowHeight)
        : (int)Math.Round(_renderer.ChevronExtent * IconHitScale);

    /// <summary>
    /// The size this bar would occupy when docked (content + gripper + chevron),
    /// even if it is currently floating. Used to preview the docked result.
    /// </summary>
    public Size PreferredDockedSize
    {
        get
        {
            int gripper = (_bar?.AllowFloat ?? false) ? _renderer.GripperExtent : 0;
            if (Vertical)
            {
                // _contentHeight already includes the gripper strip.
                int height = _contentHeight + ScaledChevronGap + ScaledChevronExtent;
                return new Size(_colWidth, height);
            }
            int width = _contentWidth + gripper + ScaledChevronGap + ScaledChevronExtent;
            return new Size(width, Height);
        }
    }

    /// <summary>Recomputes item positions and the control's height.</summary>
    public void Relayout()
    {
        if (_bar is null)
        {
            Height = 1;
            return;
        }

        _dpiScale = DeviceDpi / 96f;
        _renderer.Scale = _dpiScale;
        _iconPx = (int)Math.Round(_bar.IconSize * _dpiScale);
        _metrics = BarMetrics.For(_dpiScale, _iconPx, _renderer.UsesFluentMenuChrome);
        RebuildComboFont();

        // Keep this control listening to its items' commands so external changes
        // (a menu toggle, an Enabled flip) repaint it right away.
        RefreshCommandSubscriptions();

        // Toolbars are a single keyboard tab stop (arrow keys rove within);
        // the menu bar is reached with Alt/F10 instead.
        TabStop = !Stretch;

        // The menu bar polls the Alt key so its mnemonic underlines are momentary.
        if (Stretch && _altTimer is null)
        {
            _altTimer = new System.Windows.Forms.Timer { Interval = 50 };
            _altTimer.Tick += (_, _) => UpdateAltCue();
            _altTimer.Start();
        }

        // Gripper (and drag-to-float) only when docked in a DockHost.
        _showGripper = !Stretch && _bar.AllowFloat && Docked;
        int gripper = _showGripper ? _renderer.GripperExtent : 0;

        // A swatch palette (PaletteColumns > 0) lays out as a wrapping grid instead
        // of a single row/column.
        bool grid = _bar.PaletteColumns > 0;
        int gridW = 0, gridH = 0;

        // Measure on an offscreen surface at the control's DPI so text metrics
        // match on-screen drawing.
        using (var bmp = new Bitmap(1, 1))
        {
            bmp.SetResolution(DeviceDpi, DeviceDpi);
            using var g = Graphics.FromImage(bmp);
            if (grid)
                gridH = BarLayoutEngine.LayoutGrid(
                    g, _bar, Font, _iconPx, _metrics, _dpiScale, _bar.PaletteColumns, out gridW);
            else if (Vertical)
                _contentHeight = BarLayoutEngine.LayoutVertical(
                    g, _bar, Font, _iconPx, gripper, _metrics, _dpiScale, out _colWidth);
            else
                _rowHeight = BarLayoutEngine.LayoutHorizontal(
                    g, _bar, Font, _iconPx, gripper, _metrics, _dpiScale, IconOnly, out _contentWidth);
        }

        // Content drives the cross axis; the host sizes the main axis (a grid drives both).
        if (grid)
        {
            Width = gridW;
            Height = gridH;
        }
        else if (Vertical)
            Width = _colWidth;
        else
            Height = _rowHeight + (2 * _metrics.TopInset);
        RecomputeOverflow();
        Invalidate();
    }

    private void RecomputeOverflow()
    {
        _overflowItems.Clear();
        if (_bar is null || Stretch || !Docked)
            return;

        // The chevron area (plus a small gap) is always reserved on the far
        // edge — the right for a horizontal bar, the bottom for a vertical one.
        int cutoff = (Vertical ? Height : Width) - ScaledChevronExtent - ScaledChevronGap - _metrics.TopInset;
        var items = new List<CommandBarItem>();
        int totalExtent = 0;
        int start = (_showGripper ? _renderer.GripperExtent : 0) + _metrics.TopInset;
        foreach (var item in _bar.Items)
        {
            if (!item.Visible || item.Bounds.IsEmpty)
                continue;
            items.Add(item);
            totalExtent += Vertical ? item.Bounds.Height : item.Bounds.Width;
        }

        int availableExtent = Math.Max(0, cutoff - start);
        for (int i = items.Count - 1; i >= 0 && totalExtent > availableExtent; i--)
        {
            var item = items[i];
            if (item.Priority == 1)
                continue;
            _overflowItems.Add(item);
            totalExtent -= Vertical ? item.Bounds.Height : item.Bounds.Width;
        }

        // Reflow retained items so a protected item to the right can move into
        // space released by ordinary items before it.
        int cursor = start;
        foreach (var item in items)
        {
            if (_overflowItems.Contains(item))
                continue;
            if (Vertical)
            {
                item.Bounds = new Rectangle(item.Bounds.X, cursor, item.Bounds.Width, item.Bounds.Height);
                cursor += item.Bounds.Height;
            }
            else
            {
                item.Bounds = new Rectangle(cursor, item.Bounds.Y, item.Bounds.Width, item.Bounds.Height);
                cursor += item.Bounds.Width;
            }
        }

        if (_focusItem is not null && IsOverflowed(_focusItem))
        {
            _focusItem = null;
            _focusChevron = true;
        }
    }

    private bool IsOverflowed(CommandBarItem item) => _overflowItems.Contains(item);

    /// <summary>Current overflow set, exposed internally for layout verification.</summary>
    internal IReadOnlyCollection<CommandBarItem> OverflowItems => _overflowItems;

    private Rectangle ChevronRect()
        => Vertical
            ? new Rectangle(1, Height - ScaledChevronExtent - 1, Math.Max(1, Width - 2), ScaledChevronExtent)
            : new Rectangle(Width - ScaledChevronExtent - 1, _metrics.TopInset, ScaledChevronExtent, _rowHeight);

    protected override void OnFontChanged(EventArgs e)
    {
        base.OnFontChanged(e);
        Relayout();
    }

    protected override void OnDpiChangedAfterParent(EventArgs e)
    {
        base.OnDpiChangedAfterParent(e);
        Relayout();
    }

    protected override void OnSizeChanged(EventArgs e)
    {
        base.OnSizeChanged(e);
        RecomputeOverflow();
        Invalidate();
    }

    protected override void OnChangeUICues(UICuesEventArgs e)
    {
        base.OnChangeUICues(e);
        if (e.ChangeKeyboard)
            Invalidate();
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        var g = e.Graphics;
        if (_bar is null)
            return;

        _renderer.Scale = _dpiScale;

        if (Docked)
        {
            // Borrow the container's band gradient (seamless rebar). For a
            // vertical band the gradient runs down the column, so the slice is
            // taken along Y using the bar's Top within the parent's height.
            int bandExtent = Vertical ? (Parent?.ClientSize.Height ?? Height) : (Parent?.ClientSize.Width ?? Width);
            int bandOffset = Vertical ? Top : Left;
            _renderer.DrawBarBackground(
                g, ClientRectangle, _bar.BarType, LayoutOrientation,
                rounded: !Stretch, bandOffset: bandOffset, bandExtent: bandExtent);
        }
        else
        {
            // Floating: a flat light background, no chunk gradient.
            using var flat = new SolidBrush(_renderer.Colors.BarGradientBegin);
            g.FillRectangle(flat, ClientRectangle);
        }

        if (_showGripper)
        {
            var gripRect = Vertical
                ? new Rectangle(0, 0, Width, _renderer.GripperExtent)
                : new Rectangle(0, 0, _renderer.GripperExtent, Height);
            _renderer.DrawGripper(g, gripRect, ClientRectangle, LayoutOrientation, _gripperHot);
        }

        // Menu bar: underline mnemonics only while Alt is held or a menu is open
        // (or when the OS always underlines). Toolbars follow the normal cue state.
        bool cues = Stretch
            ? (_altHeld || _openMenuItem is not null || SystemInformation.MenuAccessKeysUnderlined)
            : ShowKeyboardCues;
        for (int i = 0; i < _bar.Items.Count; i++)
        {
            var item = _bar.Items[i];
            if (IsOverflowed(item) || !item.Visible || item.Bounds.IsEmpty)
                continue;
            DrawItem(g, item, cues);
        }

        if (!Stretch && Docked)
        {
            var state = (_chevronPressed || _overflowOpen)
                ? RenderState.Pressed
                : _chevronHot ? RenderState.Hot : RenderState.Normal;
            _renderer.DrawChevron(g, ChevronRect(), ClientRectangle, LayoutOrientation,
                state, _overflowItems.Count > 0);
        }

        // Customize mode: a dotted outline signals the bar is editable.
        if (Customizing && _bar.BarType == CommandBarType.Toolbar)
        {
            using var pen = new Pen(_renderer.Colors.RaisedBorder) { DashStyle = DashStyle.Dot };
            g.DrawRectangle(pen, 0, 0, Width - 1, Height - 1);
        }

        // Keyboard focus rectangle on the roving item (only under focus cues).
        if (!Stretch && Focused && ShowFocusCues)
        {
            Rectangle? target = _focusChevron ? ChevronRect() : _focusItem?.Bounds;
            if (target is { } r && r.Width > 2 && r.Height > 2)
                ControlPaint.DrawFocusRectangle(g, Rectangle.Inflate(r, -1, -1));
        }
    }

    private void DrawItem(Graphics g, CommandBarItem item, bool cues)
    {
        Rectangle b = item.Bounds;

        // Colour swatch (grid palette): flat colour fill + a border on hover/press.
        if (_bar!.PaletteColumns > 0 && BarLayoutEngine.IsSwatch(item) && item is CommandBarCommandItem swatch)
        {
            var img = swatch.Command.Image!.GetImage(_bar.IconSize, _dpiScale);
            _renderer.DrawItemImage(g, img, Rectangle.Inflate(b, -2, -2), RenderState.Normal);
            if (ReferenceEquals(item, _hotItem) || ReferenceEquals(item, _pressedItem))
            {
                using var pen = new Pen(_renderer.Colors.ButtonHotBorder);
                g.DrawRectangle(pen, b.X + 1, b.Y + 1, b.Width - 3, b.Height - 3);
            }
            return;
        }

        switch (item)
        {
            case CommandBarSeparator:
                _renderer.DrawSeparator(g, b, LayoutOrientation);
                break;

            case CommandBarLabel label:
                _renderer.DrawItemText(g, label.Text, Font, b, RenderState.Normal,
                    TextFlags(TextFormatFlags.Left | TextFormatFlags.VerticalCenter, cues));
                break;

            case CommandBarComboBox combo:
                if (Vertical)
                    DrawComboButton(g, combo, b, cues);
                else
                    DrawComboBox(g, combo, b);
                break;

            case CommandBarPopupItem popup:
            {
                // Grow menu highlights within the row instead of adding empty
                // space above and below the entire menu bar.
                var surfaceBounds = b;
                if (Stretch && _renderer.UsesFluentMenuChrome && !Vertical)
                    surfaceBounds.Inflate(0, (int)Math.Round(2 * _dpiScale));
                var state = ItemState(popup, enabled: true);
                bool open = ReferenceEquals(popup, _openMenuItem);
                if (open)
                    state |= RenderState.Checked;
                if (open)
                    _renderer.DrawOpenMenuButton(g, surfaceBounds, LayoutOrientation,
                        _openWindow?.AnchorConnectionEdge ?? PopupConnectionEdge.None);
                else
                    _renderer.DrawButton(g, surfaceBounds, state, LayoutOrientation);
                DrawPopupContent(g, popup, b, state, cues);
                break;
            }

            case CommandBarCommandItem cmd:
                DrawCommandItem(g, cmd, b, cues);
                break;
        }
    }

    // Rasterize SVGs at their fitted size, and keep raster images inside the same
    // two-logical-pixel inset. The model's icon size remains the layout preference.
    private int ToolbarImageSize(Rectangle content)
    {
        int requested = _bar!.IconSize;
        if (!_renderer.UsesFluentMenuChrome) return requested;
        int widthInset = (int)Math.Round((Vertical ? 10 : 8) * _dpiScale);
        int heightInset = (int)Math.Round((Vertical ? 12 : 10) * _dpiScale);
        int available = Math.Min(content.Width - widthInset, content.Height - heightInset);
        return Math.Max(1, Math.Min(requested, (int)Math.Floor(available / _dpiScale)));
    }

    private void DrawCommandItem(Graphics g, CommandBarCommandItem cmd, Rectangle b, bool cues)
    {
        bool enabled = cmd.Command.Enabled;
        var state = ItemState(cmd, enabled);
        if (cmd is CommandBarToggleButton toggle && toggle.Checked)
            state |= RenderState.Checked;

        Rectangle content = b;
        if (cmd is CommandBarSplitButton)
        {
            bool open = ReferenceEquals(cmd, _openSplitButton);
            bool arrowPressed = ReferenceEquals(cmd, _pressedItem) && _pressedSplitArrow;
            bool dropDownActive = open || arrowPressed;
            PopupConnectionEdge connectionEdge = open
                ? _openWindow?.AnchorConnectionEdge ?? PopupConnectionEdge.None
                : PopupConnectionEdge.None;
            if (dropDownActive)
                state |= RenderState.Checked;

            // Split the cell into a button half and a dropdown-arrow half.
            Rectangle arrowRect, buttonRect;
            if (Vertical)
            {
                arrowRect = new Rectangle(b.X, b.Bottom - _metrics.ArrowWidth, b.Width, _metrics.ArrowWidth);
                buttonRect = new Rectangle(b.X, b.Y, b.Width, b.Height - _metrics.ArrowWidth);
            }
            else
            {
                arrowRect = new Rectangle(b.Right - _metrics.ArrowWidth, b.Y, _metrics.ArrowWidth, b.Height);
                buttonRect = new Rectangle(b.X, b.Y, b.Width - _metrics.ArrowWidth, b.Height);
            }

            // Pressing the button darkens only the button (arrow stays a light
            // highlight); pressing the arrow presses both halves.
            RenderState buttonState, arrowState;
            if (!enabled)
            {
                buttonState = arrowState = RenderState.Disabled;
            }
            else if (dropDownActive)
            {
                buttonState = arrowState = RenderState.Checked;
            }
            else if (ReferenceEquals(cmd, _pressedItem))
            {
                buttonState = RenderState.Pressed;
                arrowState = _pressedSplitArrow ? RenderState.Pressed : RenderState.Hot;
            }
            else if (ReferenceEquals(cmd, _hotItem) || IsFocusHot(cmd))
            {
                buttonState = arrowState = RenderState.Hot;
                if (_renderer.UsesFluentMenuChrome && ReferenceEquals(cmd, _hotItem))
                {
                    buttonState = _hotSplitArrow ? RenderState.Normal : RenderState.Hot;
                    arrowState = _hotSplitArrow ? RenderState.Hot : RenderState.Normal;
                }
            }
            else
            {
                buttonState = arrowState = RenderState.Normal;
            }

            if (dropDownActive)
            {
                // Paint the split as one continuous open-menu surface so the
                // gradient does not restart at the arrow half. A single themed
                // divider preserves the split affordance.
                _renderer.DrawOpenMenuButton(g, b, LayoutOrientation, connectionEdge);
                DrawOpenSplitDivider(g, arrowRect);
            }
            else
            {
                _renderer.DrawSplitButton(g, b, buttonRect, arrowRect,
                    buttonState, arrowState, LayoutOrientation);
            }
            // Only draw the divider at rest — when a half is hovered, pressed, or
            // keyboard-focused, its own raised border already separates the two.
            bool raised = dropDownActive || ReferenceEquals(cmd, _hotItem) || ReferenceEquals(cmd, _pressedItem) || IsFocusHot(cmd);
            if (!raised && !_renderer.UsesFluentMenuChrome)
                DrawSplitDivider(g, b, arrowRect);
            _renderer.DrawDropDownArrow(g, arrowRect, enabled ? RenderState.Normal : RenderState.Disabled);

            content = buttonRect;
        }
        else
        {
            _renderer.DrawButton(g, b, state, LayoutOrientation);
        }

        // Vertical (Left/Right-docked) toolbars render icon-only, Office-style.
        bool hasImage = cmd.DisplayStyle != CommandItemDisplayStyle.TextOnly && cmd.Command.Image is not null;
        bool hasCaption = !string.IsNullOrEmpty(cmd.DisplayText);
        // Horizontal bars show text when the style allows it OR when there's no
        // image to show (an icon-less button falls back to its caption instead
        // of rendering blank). Vertical bars stay icon-only but likewise fall
        // back to text when there's no icon.
        bool hasText = hasCaption && (IconOnly
            ? !hasImage
            : cmd.DisplayStyle != CommandItemDisplayStyle.ImageOnly || !hasImage);
        int imageSize = ToolbarImageSize(content);
        int iconPx = (int)Math.Round(imageSize * _dpiScale);
        int textX = content.X + _metrics.ButtonHPad;

        if (hasImage)
        {
            var image = cmd.Command.Image!.GetImage(imageSize, _dpiScale);
            int imgY = content.Y + ((content.Height - iconPx) / 2);
            int imgX = hasText
                ? content.X + (_metrics.Fluent ? (int)Math.Round(4 * _dpiScale) : _metrics.ButtonHPad)
                : content.X + ((content.Width - iconPx) / 2);
            _renderer.DrawItemImage(g, image, new Rectangle(imgX, imgY, iconPx, iconPx), state);
            textX = imgX + iconPx + _metrics.TextImageGap;
        }

        if (hasText)
        {
            if (Vertical)
            {
                // No icon on a vertically-docked bar: draw the caption rotated so it
                // reads along the bar instead of being clipped in the narrow column.
                DrawVerticalText(g, cmd.Command.Text, content, state, cues);
            }
            else
            {
                var textRect = new Rectangle(textX, content.Y, content.Right - textX - _metrics.ButtonHPad, content.Height);
                _renderer.DrawItemText(g, cmd.Command.Text, Font, textRect, state,
                    TextFlags(TextFormatFlags.Left | TextFormatFlags.VerticalCenter, cues));
            }
        }
    }

    // Draws a toolbar/menu popup's content using its display style, plus a
    // dropdown arrow on toolbars. Menu-bar entries remain text-only and
    // arrow-less. Vertical/icon-only bars fall back to rotated text when an icon
    // is suppressed or unavailable.
    private void DrawPopupContent(Graphics g, CommandBarPopupItem popup, Rectangle b, RenderState state, bool cues)
    {
        bool arrow = _bar!.BarType != CommandBarType.MenuBar;
        Rectangle content = b;
        Rectangle arrowRect = Rectangle.Empty;
        if (arrow)
        {
            if (Vertical)
            {
                arrowRect = new Rectangle(b.X, b.Bottom - _metrics.ArrowWidth, b.Width, _metrics.ArrowWidth);
                content = new Rectangle(b.X, b.Y, b.Width, b.Height - _metrics.ArrowWidth);
            }
            else
            {
                arrowRect = new Rectangle(b.Right - _metrics.ArrowWidth, b.Y, _metrics.ArrowWidth, b.Height);
                content = new Rectangle(b.X, b.Y, b.Width - _metrics.ArrowWidth, b.Height);
            }
        }

        bool hasImage = BarLayoutEngine.PopupShowsImage(popup, arrow);
        bool hasText = BarLayoutEngine.PopupShowsText(popup, arrow, IconOnly);
        int contentPadding = _metrics.Fluent && arrow ? _metrics.ButtonHPad : _metrics.MenuItemHPad;
        int textX = content.X + contentPadding;

        if (hasImage)
        {
            int imageSize = ToolbarImageSize(content);
            var image = popup.Image!.GetImage(imageSize, _dpiScale);
            int iconPx = (int)Math.Round(imageSize * _dpiScale);
            int imgX = hasText
                ? content.X + (_metrics.Fluent ? (int)Math.Round(4 * _dpiScale) : contentPadding)
                : content.X + ((content.Width - iconPx) / 2);
            int imgY = content.Y + ((content.Height - iconPx) / 2);
            _renderer.DrawItemImage(g, image, new Rectangle(imgX, imgY, iconPx, iconPx), state);
            textX = imgX + iconPx + _metrics.TextImageGap;
        }

        if (hasText && Vertical)
        {
            DrawVerticalText(g, popup.Text, content, state, cues);
        }
        else if (hasText)
        {
            bool centered = !hasImage;
            var textRect = centered
                ? content
                : new Rectangle(
                    textX,
                    content.Y,
                    Math.Max(0, content.Right - textX - contentPadding),
                    content.Height);
            _renderer.DrawItemText(g, popup.Text, Font, textRect, state,
                TextFlags((centered ? TextFormatFlags.HorizontalCenter : TextFormatFlags.Left) |
                    TextFormatFlags.VerticalCenter, cues));
        }

        if (arrow)
            _renderer.DrawDropDownArrow(g, arrowRect, state);
    }

    // Draws item text rotated 90° for a vertically-docked bar, so an icon-less
    // button/popup reads along the bar instead of being clipped in the narrow
    // column. Left-docked bars read bottom-to-top; right-docked bars read
    // top-to-bottom (matching Office). Colour comes from the renderer palette so
    // themes still apply; '&' mnemonics underline only when key cues are shown.
    private void DrawVerticalText(Graphics g, string text, Rectangle rect, RenderState state, bool cues)
    {
        if (string.IsNullOrEmpty(text))
            return;

        Color color = (state & RenderState.Disabled) != 0
            ? _renderer.Colors.DisabledText
            : _renderer.Colors.Text;
        bool leftDock = _bar!.Dock == DockState.Left;

        using var sf = new StringFormat(StringFormatFlags.NoWrap)
        {
            Alignment = StringAlignment.Center,
            LineAlignment = StringAlignment.Center,
            Trimming = StringTrimming.EllipsisCharacter,
            HotkeyPrefix = cues ? System.Drawing.Text.HotkeyPrefix.Show : System.Drawing.Text.HotkeyPrefix.Hide,
        };

        var saved = g.Save();
        var prevHint = g.TextRenderingHint;
        g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.AntiAlias;
        g.TranslateTransform(rect.X + (rect.Width / 2f), rect.Y + (rect.Height / 2f));
        g.RotateTransform(leftDock ? 270f : 90f);
        // After a 90° rotation the layout box swaps width and height.
        var layout = new RectangleF(-rect.Height / 2f, -rect.Width / 2f, rect.Height, rect.Width);
        using var brush = new SolidBrush(color);
        g.DrawString(text, Font, brush, layout, sf);
        g.TextRenderingHint = prevHint;
        g.Restore(saved);
    }

    // A themed divider between a split button's two halves: a vertical line for
    // a horizontal bar, a horizontal line for a vertical one.
    private void DrawSplitDivider(Graphics g, Rectangle b, Rectangle arrowRect)
    {
        if (Vertical)
            _renderer.DrawSeparator(g, new Rectangle(b.X, arrowRect.Top - 1, b.Width, 3), BarOrientation.Vertical);
        else
            _renderer.DrawSeparator(g, new Rectangle(arrowRect.Left - 1, b.Y, 3, b.Height), BarOrientation.Horizontal);
    }

    private void DrawOpenSplitDivider(Graphics g, Rectangle arrowRect)
    {
        if (_renderer.UsesFluentMenuChrome) return;
        using var pen = new Pen(_renderer.Colors.MenuOpenBorder);
        if (Vertical)
            g.DrawLine(pen, arrowRect.Left + 1, arrowRect.Top, arrowRect.Right - 2, arrowRect.Top);
        else
            g.DrawLine(pen, arrowRect.Left, arrowRect.Top + 1, arrowRect.Left, arrowRect.Bottom - 2);
    }

    // Rebuilds the icon-size-scaled combo font (see BarLayoutEngine.ComboGrow).
    // Null keeps the control's own Font at the default icon size. Never disposes
    // the control Font, only a font this control created.
    private void RebuildComboFont()
    {
        var old = _comboFont;
        float grow = BarLayoutEngine.ComboGrow(_iconPx, _dpiScale);
        _comboFont = grow <= 1.001f ? null : new Font(Font.FontFamily, Font.SizeInPoints * grow, Font.Style);
        if (old is not null && !ReferenceEquals(old, Font))
            old.Dispose();
    }

    // Hover/press state shared by the inline field and the collapsed button:
    // pressed while its list is open (or the mouse is held on it), hot while the
    // mouse is over it.
    private RenderState ComboRenderState(CommandBarComboBox combo) =>
        !combo.Enabled ? RenderState.Disabled
        : ReferenceEquals(combo, _openCombo) || ReferenceEquals(combo, _pressedCombo) ? RenderState.Pressed
        : ReferenceEquals(combo, _hotCombo) ? RenderState.Hot
        : RenderState.Normal;

    // Width of the inline field's drop-arrow button, DPI-scaled.
    private int ComboArrowWidth => Math.Max(12, (int)Math.Round(16 * _dpiScale));

    // The combo's editable box sits inside its cell, sized to the (icon-size-
    // scaled) text height — not the full icon-row height, which looked too tall —
    // and centered. Width and font both grow with the icon size so the field no
    // longer sits frozen in a taller row.
    private Rectangle ComboBoxRect(CommandBarComboBox combo)
    {
        Rectangle b = combo.Bounds;
        int boxH = Math.Min(b.Height, ComboFont.Height + (int)Math.Round(6 * _dpiScale));
        if (_renderer.UsesFluentMenuChrome)
            boxH = Math.Max(1, b.Height - 2 * (int)Math.Round(3 * _dpiScale));
        int boxY = b.Y + ((b.Height - boxH) / 2);
        int boxW = BarLayoutEngine.ComboBoxWidthPx(combo, _iconPx, _dpiScale);
        return new Rectangle(b.X + _metrics.ButtonHPad, boxY, boxW, boxH);
    }

    private void DrawComboBox(Graphics g, CommandBarComboBox combo, Rectangle b)
    {
        var box = ComboBoxRect(combo);

        // Drives the arrow-button highlight and a stronger border, Office-style.
        RenderState state = ComboRenderState(combo);
        bool active = state is RenderState.Hot or RenderState.Pressed;

        var comboColors = _renderer.DialogColors;
        Color fieldBackground = combo.Enabled ? comboColors.InputBackground : comboColors.SurfaceAlternate;

        int arrowW = ComboArrowWidth;
        var arrowBox = new Rectangle(box.Right - arrowW, box.Y, arrowW, box.Height);
        _renderer.DrawComboBoxChrome(g, box, arrowBox, state, fieldBackground);

        int pad = (int)Math.Round(3 * _dpiScale);
        string text = combo.SelectedItem?.ToString() ?? string.Empty;
        _renderer.DrawItemText(g, text, ComboFont,
            new Rectangle(box.X + pad, box.Y, box.Width - arrowW - (2 * pad), box.Height),
            state == RenderState.Disabled ? RenderState.Disabled : RenderState.Normal,
            TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.SingleLine | TextFormatFlags.EndEllipsis);
        _renderer.DrawDropDownArrow(g, arrowBox,
            state == RenderState.Disabled ? RenderState.Disabled : active ? state : RenderState.Normal);

    }

    // A vertically-docked toolbar can't host an editable field, so the combo
    // collapses to an Office-style drop-down button: its icon (or label / current
    // selection) over a drop-arrow strip. Clicking opens the same item list as
    // the inline field, so the choices stay reachable without an overflow trip.
    private void DrawComboButton(Graphics g, CommandBarComboBox combo, Rectangle b, bool cues)
    {
        RenderState state = ComboRenderState(combo);
        if (state != RenderState.Normal)
            _renderer.DrawButton(g, b, state, LayoutOrientation);

        int strip = _metrics.ArrowWidth;
        var arrowRect = new Rectangle(b.X, b.Bottom - strip, b.Width, strip);
        var content = new Rectangle(b.X, b.Y, b.Width, b.Height - strip);

        if (combo.Image is not null)
        {
            int imageSize = ToolbarImageSize(content);
            int iconPx = (int)Math.Round(imageSize * _dpiScale);
            var image = combo.Image.GetImage(imageSize, _dpiScale);
            int imgX = content.X + ((content.Width - iconPx) / 2);
            int imgY = content.Y + ((content.Height - iconPx) / 2);
            _renderer.DrawItemImage(g, image, new Rectangle(imgX, imgY, iconPx, iconPx), state);
        }
        else
        {
            // No icon: fall back to a short label or the current selection text.
            string caption = combo.Label ?? combo.SelectedItem?.ToString() ?? string.Empty;
            _renderer.DrawItemText(g, caption, Font, content, state,
                TextFlags(TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter, cues)
                    | TextFormatFlags.EndEllipsis);
        }

        _renderer.DrawDropDownArrow(g, arrowRect, state);
    }

    private static TextFormatFlags TextFlags(TextFormatFlags align, bool cues)
        => align | BarLayoutEngine.MeasureFlags | (cues ? 0 : TextFormatFlags.HidePrefix);

    private RenderState ItemState(CommandBarItem item, bool enabled)
    {
        if (!enabled)
            return RenderState.Disabled;
        if (ReferenceEquals(item, _pressedItem))
            return RenderState.Pressed;
        if (ReferenceEquals(item, _hotItem))
            return RenderState.Hot;
        // The keyboard-focused item reads as hot while focus cues are shown.
        if (IsFocusHot(item))
            return RenderState.Hot;
        return RenderState.Normal;
    }

    // The keyboard-focused item, but only while focus cues are being shown.
    private bool IsFocusHot(CommandBarItem item)
        => Focused && ShowFocusCues && ReferenceEquals(item, _focusItem);

    // --- Keyboard focus ----------------------------------------------------

    protected override bool IsInputKey(Keys keyData)
    {
        if (!Stretch)
        {
            switch (keyData & Keys.KeyCode)
            {
                case Keys.Return:
                case Keys.Space:
                case Keys.Escape:
                case Keys.Home:
                case Keys.End:
                    return true;
                case Keys.Up:
                case Keys.Down:
                    if (Vertical)
                        return true;
                    break;
                case Keys.Left:
                case Keys.Right:
                    if (!Vertical)
                        return true;
                    break;
            }
        }
        return base.IsInputKey(keyData);
    }

    protected override void OnGotFocus(EventArgs e)
    {
        base.OnGotFocus(e);
        if (!Stretch && _focusItem is null && !_focusChevron)
            FocusEdge(true);
        Invalidate();
    }

    protected override void OnLostFocus(EventArgs e)
    {
        base.OnLostFocus(e);
        _focusItem = null;
        _focusChevron = false;
        Invalidate();
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        if (Stretch || _bar is null)
            return;

        switch (e.KeyCode)
        {
            case Keys.Right when !Vertical:
            case Keys.Down when Vertical:
                MoveFocus(1);
                e.Handled = true;
                break;
            case Keys.Left when !Vertical:
            case Keys.Up when Vertical:
                MoveFocus(-1);
                e.Handled = true;
                break;
            case Keys.Home:
                FocusEdge(true);
                e.Handled = true;
                break;
            case Keys.End:
                FocusEdge(false);
                e.Handled = true;
                break;
            case Keys.Return:
            case Keys.Space:
                ActivateFocus();
                e.Handled = true;
                break;
            case Keys.Escape:
                LeaveKeyboard();
                e.Handled = true;
                break;
        }
    }

    // Interactive items reachable by keyboard (visible, not overflowed).
    private List<CommandBarItem> KeyboardItems()
    {
        var list = new List<CommandBarItem>();
        if (_bar is null)
            return list;
        for (int i = 0; i < _bar.Items.Count; i++)
        {
            var item = _bar.Items[i];
            if (!IsOverflowed(item) && item.Visible && !item.Bounds.IsEmpty
                && item is CommandBarCommandItem or CommandBarPopupItem)
                list.Add(item);
        }
        return list;
    }

    // The overflow chevron is drawn on every docked toolbar, so it is always a
    // keyboard stop (its flyout also hosts Add/Remove Buttons).
    private bool HasChevron => !Stretch && Docked;

    private void MoveFocus(int delta)
    {
        var items = KeyboardItems();
        int count = items.Count + (HasChevron ? 1 : 0);
        if (count == 0)
            return;
        int cur = _focusChevron ? items.Count : (_focusItem is null ? -1 : items.IndexOf(_focusItem));
        int next = cur < 0 ? (delta >= 0 ? 0 : count - 1) : (((cur + delta) % count) + count) % count;
        SetFocusStop(next, items);
    }

    private void FocusEdge(bool first)
    {
        var items = KeyboardItems();
        int count = items.Count + (HasChevron ? 1 : 0);
        if (count == 0)
            return;
        SetFocusStop(first ? 0 : count - 1, items);
    }

    private void SetFocusStop(int index, List<CommandBarItem> items)
    {
        if (HasChevron && index == items.Count)
        {
            _focusChevron = true;
            _focusItem = null;
        }
        else if (index >= 0 && index < items.Count)
        {
            _focusChevron = false;
            _focusItem = items[index];
        }
        Invalidate();
    }

    private void ActivateFocus()
    {
        if (_focusChevron)
        {
            OpenOverflow();
            return;
        }
        switch (_focusItem)
        {
            case CommandBarPopupItem popup:
                OpenMenu(popup);
                break;
            case CommandBarCommandItem cmd:
                var b = cmd.Bounds;
                Activate(cmd, new Point(b.Left + Math.Min(4, b.Width / 2), b.Top + (b.Height / 2)));
                break;
        }
    }

    private void LeaveKeyboard()
    {
        _focusItem = null;
        _focusChevron = false;
        Invalidate();
        // Exit the toolbar without advancing to the next control like Tab —
        // just drop focus so it returns to the app content.
        var form = FindForm();
        if (form is not null)
            form.ActiveControl = null;
    }

    // --- Hit testing & mouse ----------------------------------------------

    private CommandBarItem? HitTest(Point p)
    {
        if (_bar is null)
            return null;
        for (int i = 0; i < _bar.Items.Count; i++)
        {
            var item = _bar.Items[i];
            if (IsOverflowed(item) || !item.Visible || item.Bounds.IsEmpty)
                continue;
            if (item is not (CommandBarCommandItem or CommandBarPopupItem))
                continue;
            if (item.Bounds.Contains(p))
                return item;
        }
        return null;
    }

    // Like HitTest but returns any visible item (separators, labels, combos),
    // used by Customize-mode dragging.
    private CommandBarItem? HitTestAny(Point p)
    {
        if (_bar is null)
            return null;
        for (int i = 0; i < _bar.Items.Count; i++)
        {
            var item = _bar.Items[i];
            if (IsOverflowed(item) || !item.Visible || item.Bounds.IsEmpty)
                continue;
            if (item.Bounds.Contains(p))
                return item;
        }
        return null;
    }

    // Returns the visible combo box at a point (combos aren't returned by HitTest).
    private CommandBarComboBox? HitTestCombo(Point p)
    {
        if (_bar is null)
            return null;
        for (int i = 0; i < _bar.Items.Count; i++)
        {
            if (_bar.Items[i] is CommandBarComboBox combo
                && !IsOverflowed(combo) && combo.Enabled && combo.Visible
                && !combo.Bounds.IsEmpty && combo.Bounds.Contains(p))
                return combo;
        }
        return null;
    }

    // --- Combo dropdown ----------------------------------------------------

    private void OpenComboDropDown(CommandBarComboBox combo)
    {
        CloseComboDropDown();
        if (!combo.Enabled || combo.Items.Count == 0)
            return;

        // Anchor the list under the inline field on a horizontal bar; on a
        // vertical bar (where the combo is a collapsed button) anchor it under
        // the whole button and give it the combo's normal width so the choices
        // aren't squeezed into the narrow column.
        Rectangle anchor;
        int minWidth;
        if (Vertical)
        {
            anchor = RectangleToScreen(combo.Bounds);
            minWidth = BarLayoutEngine.ComboBoxWidthPx(combo, _iconPx, _dpiScale) + (2 * _metrics.ButtonHPad);
        }
        else
        {
            anchor = RectangleToScreen(ComboBoxRect(combo));
            minWidth = anchor.Width;
        }
        var dd = new ComboDropDown(combo, _renderer, ComboFont, anchor, minWidth,
            RectangleToScreen(combo.Bounds));
        _comboWindow = dd;
        _openCombo = combo; // keep the box drawn "pressed" while its list is open
        Invalidate();
        dd.ItemChosen += value =>
        {
            combo.SelectedItem = value; // setter raises SelectedItemChanged
            Invalidate();
        };
        dd.FormClosed += (_, _) =>
        {
            if (ReferenceEquals(_comboWindow, dd))
                _comboWindow = null;
            if (ReferenceEquals(_openCombo, combo))
            {
                _openCombo = null;
                Invalidate();
            }
        };
        dd.Show(FindForm());
    }

    private void CloseComboDropDown()
    {
        var dd = _comboWindow;
        _comboWindow = null;
        if (dd is not null && !dd.IsDisposed)
            dd.Close();
    }

    // --- Command change subscriptions (repaint on external change) ---------

    private void RefreshCommandSubscriptions()
    {
        UnsubscribeCommands();
        if (_bar is null)
            return;
        foreach (var item in _bar.Items)
        {
            if (item is CommandBarCommandItem c && _subscribedCommands.Add(c.Command))
                c.Command.PropertyChanged += OnCommandPropertyChanged;
            else if (item is CommandBarComboBox combo && _subscribedComboBoxes.Add(combo))
            {
                combo.SelectedItemChanged += OnComboBoxSelectedItemChanged;
                combo.EnabledChanged += OnComboBoxSelectedItemChanged;
            }
        }
    }

    private void UnsubscribeCommands()
    {
        foreach (var cmd in _subscribedCommands)
            cmd.PropertyChanged -= OnCommandPropertyChanged;
        _subscribedCommands.Clear();
        foreach (var combo in _subscribedComboBoxes)
        {
            combo.SelectedItemChanged -= OnComboBoxSelectedItemChanged;
            combo.EnabledChanged -= OnComboBoxSelectedItemChanged;
        }
        _subscribedComboBoxes.Clear();
    }

    private void OnComboBoxSelectedItemChanged(object? sender, EventArgs e)
    {
        if (!IsDisposed)
            Invalidate();
    }

    private void OnCommandPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (IsDisposed)
            return;
        // Text/Image can change the measured size → relayout; Checked/Enabled/
        // ToolTip just need a repaint.
        if (e.PropertyName is nameof(Command.Text) or nameof(Command.Image))
            Relayout();
        else
            Invalidate();
    }

    // --- Customize-mode item drag -----------------------------------------

    private void UpdateItemDrag(Point screen)
    {
        var mgr = _bar?.Manager;
        var target = FindDropControl(screen, out _, out Rectangle marker);
        if (target is not null)
        {
            mgr?.ShowDropMarker(marker);
            Cursor = Cursors.SizeAll;
        }
        else
        {
            mgr?.HideDropMarker();
            Cursor = Cursors.No; // dropping off every bar removes the item
        }
    }

    private void EndItemDrag(Point screen)
    {
        var host = Parent as DockHost;
        Cursor = Cursors.Default;

        var mgr = _bar?.Manager;
        mgr?.HideDropMarker();
        var item = _itemDragItem;
        if (mgr is null || item is null)
            return;
        var sourceBar = item.OwnerBar;
        if (sourceBar is null)
            return;

        var target = FindDropControl(screen, out int index, out _);
        var targetBar = target?.Bar;

        // Defer the model change: RefreshLayout rebuilds the bars and disposes
        // this control, so it must not run inside this control's mouse handler.
        Control invoker = host ?? (Control)this;
        invoker.BeginInvoke((MethodInvoker)(() =>
        {
            if (targetBar is null)
            {
                // Dropped off every toolbar: remove the item.
                sourceBar.Items.Remove(item);
            }
            else if (ReferenceEquals(sourceBar, targetBar))
            {
                int oldIndex = sourceBar.Items.IndexOf(item);
                if (oldIndex < 0)
                    return;
                sourceBar.Items.RemoveAt(oldIndex);
                int insert = index > oldIndex ? index - 1 : index; // removed slot shifts the tail left
                insert = Math.Clamp(insert, 0, sourceBar.Items.Count);
                sourceBar.Items.Insert(insert, item);
            }
            else
            {
                sourceBar.Items.Remove(item);
                int insert = Math.Clamp(index, 0, targetBar.Items.Count);
                targetBar.Items.Insert(insert, item);
            }
            mgr.RefreshLayout();
        }));
    }

    private CommandBarControl? FindDropControl(Point screen, out int index, out Rectangle markerScreen)
    {
        index = 0;
        markerScreen = Rectangle.Empty;
        var mgr = _bar?.Manager;
        return mgr is null ? null : mgr.FindDropTarget(screen, out index, out markerScreen);
    }

    /// <summary>
    /// If <paramref name="screen"/> is over this toolbar, returns true and yields
    /// the insertion index (into the full item list) and a screen-space marker
    /// rectangle at the gap where a dropped item would land.
    /// </summary>
    internal bool TryComputeInsertion(Point screen, out int index, out Rectangle markerScreen)
    {
        index = 0;
        markerScreen = Rectangle.Empty;
        if (_bar is null || _bar.BarType != CommandBarType.Toolbar)
            return false;
        if (!RectangleToScreen(ClientRectangle).Contains(screen))
            return false;

        Point p = PointToClient(screen);
        var visible = new List<CommandBarItem>();
        foreach (var it in _bar.Items)
            if (!IsOverflowed(it) && it.Visible && !it.Bounds.IsEmpty)
                visible.Add(it);

        int insert = visible.Count;
        for (int i = 0; i < visible.Count; i++)
        {
            var b = visible[i].Bounds;
            int center = Vertical ? b.Y + (b.Height / 2) : b.X + (b.Width / 2);
            int pos = Vertical ? p.Y : p.X;
            if (pos < center)
            {
                insert = i;
                break;
            }
        }

        int thick = Math.Max(2, (int)Math.Round(2 * _dpiScale));
        Rectangle marker;
        if (Vertical)
        {
            int y = insert < visible.Count ? visible[insert].Bounds.Top
                : visible.Count > 0 ? visible[^1].Bounds.Bottom : _metrics.TopInset + _renderer.GripperExtent;
            marker = new Rectangle(0, y - (thick / 2), Math.Max(1, Width), thick);
        }
        else
        {
            int x = insert < visible.Count ? visible[insert].Bounds.Left
                : visible.Count > 0 ? visible[^1].Bounds.Right : _metrics.TopInset;
            marker = new Rectangle(x - (thick / 2), _metrics.TopInset, thick, Math.Max(1, _rowHeight));
        }

        index = insert < visible.Count ? _bar.Items.IndexOf(visible[insert]) : _bar.Items.Count;
        markerScreen = RectangleToScreen(marker);
        return true;
    }

    protected override void OnMouseMove(MouseEventArgs e)
    {
        base.OnMouseMove(e);

        bool gripHot = _showGripper && ClientRectangle.Contains(e.Location)
            && (Vertical ? e.Y < _renderer.GripperExtent : e.X < _renderer.GripperExtent);
        if (gripHot != _gripperHot)
        {
            _gripperHot = gripHot;
            Invalidate();
        }

        if (_itemDragArmed)
        {
            if (!_itemDragging)
            {
                var dz = SystemInformation.DragSize;
                if (Math.Abs(e.X - _itemDragGrab.X) >= dz.Width || Math.Abs(e.Y - _itemDragGrab.Y) >= dz.Height)
                    _itemDragging = true;
            }
            if (_itemDragging)
                UpdateItemDrag(Cursor.Position);
            return;
        }

        if (_dragArmed)
        {
            if (!_dragging)
            {
                var dz = SystemInformation.DragSize;
                if (Math.Abs(e.X - _dragGrab.X) >= dz.Width || Math.Abs(e.Y - _dragGrab.Y) >= dz.Height)
                    _dragging = true;
            }
            if (_dragging && Parent is DockHost host)
                host.UpdateBarDrag(Cursor.Position, floatGhost: true);
            return;
        }

        bool onChevron = !Stretch && Docked && ChevronRect().Contains(e.Location);
        if (onChevron != _chevronHot)
        {
            _chevronHot = onChevron;
            Invalidate();
        }

        var item = onChevron ? null : HitTest(e.Location);
        bool splitArrowHot = item is CommandBarSplitButton split && OnSplitArrow(split, e.Location);
        if (_hotSplitArrow != splitArrowHot)
        {
            _hotSplitArrow = splitArrowHot;
            Invalidate();
        }
        if (!ReferenceEquals(item, _hotItem))
        {
            _hotItem = item;
            Invalidate();
        }

        var hotCombo = onChevron ? null : HitTestCombo(e.Location);
        if (!ReferenceEquals(hotCombo, _hotCombo))
        {
            _hotCombo = hotCombo;
            Invalidate();
        }

        UpdateToolTip(item, onChevron);

        if (_openMenuItem is not null && item is CommandBarPopupItem popup && !ReferenceEquals(popup, _openMenuItem))
            OpenMenu(popup);
    }

    protected override void OnMouseLeave(EventArgs e)
    {
        base.OnMouseLeave(e);
        if (_gripperHot)
        {
            _gripperHot = false;
            Invalidate();
        }
        HideTip();
        if (_hotCombo is not null)
        {
            _hotCombo = null;
            Invalidate();
        }
        if (_pressedItem is null)
        {
            _hotItem = null;
            _chevronHot = false;
            Invalidate();
        }
    }

    // --- Tooltips (ScreenTips) --------------------------------------------

    private void UpdateToolTip(CommandBarItem? item, bool onChevron)
    {
        bool enabled = (_bar?.Manager?.ShowToolTips ?? true) && _openMenuItem is null;
        if (!enabled)
        {
            SetTip(null, false, string.Empty);
            return;
        }
        if (onChevron)
        {
            SetTip(null, true, "Toolbar Options");
            return;
        }
        if (item is not null)
        {
            string? text = ScreenTipText(item);
            if (!string.IsNullOrEmpty(text))
            {
                SetTip(item, false, text);
                return;
            }
        }
        SetTip(null, false, string.Empty);
    }

    private void SetTip(CommandBarItem? item, bool chevron, string text)
    {
        // Only re-arm the tooltip when the hovered target actually changes.
        if (ReferenceEquals(item, _tipItem) && chevron == _tipOnChevron)
            return;
        _tipItem = item;
        _tipOnChevron = chevron;
        _toolTip.SetToolTip(this, text);
    }

    private void HideTip()
    {
        _tipItem = null;
        _tipOnChevron = false;
        _toolTip.Hide(this);
    }

    private static string TipText(Command command)
    {
        string text = string.IsNullOrEmpty(command.ToolTip) ? command.DisplayText : command.ToolTip!;
        string shortcut = Command.FormatShortcut(command.Shortcut);
        return shortcut.Length > 0 ? $"{text} ({shortcut})" : text;
    }

    /// <summary>
    /// Resolves the ScreenTip for toolbar items. Popup captions are essential
    /// for icon-only category controls such as the AutoShapes tear-off palette.
    /// </summary>
    internal static string? ScreenTipText(CommandBarItem item) => item switch
    {
        CommandBarCommandItem commandItem => TipText(commandItem.Command),
        CommandBarPopupItem popup => popup.DisplayText,
        _ => null,
    };

    protected override void OnMouseDown(MouseEventArgs e)
    {
        base.OnMouseDown(e);
        if (e.Button != MouseButtons.Left)
            return;

        HideTip(); // a click dismisses any showing tooltip

        // Customize mode gives ordinary toolbars item-edit behavior. Menus stay
        // browseable, but only popup items may react; commands, combos, chevrons,
        // and every bar gripper remain inactive.
        if (Customizing && _bar is not null)
        {
            bool onGrip = _showGripper && (Vertical ? e.Y < _renderer.GripperExtent : e.X < _renderer.GripperExtent);
            if (_bar.BarType == CommandBarType.Toolbar && !onGrip)
            {
                var target = HitTestAny(e.Location);
                if (target is not null)
                {
                    _itemDragArmed = true;
                    _itemDragItem = target;
                    _itemDragGrab = e.Location;
                    Capture = true;
                }
            }

            if (_bar.BarType != CommandBarType.Toolbar && !onGrip &&
                HitTestAny(e.Location) is CommandBarPopupItem browsePopup)
                ToggleMenu(browsePopup);

            return; // swallow command/combo/chevron clicks and all gripper presses
        }

        // Start a potential drag from the gripper (undock / move between lines).
        bool onGripper = _showGripper && (Vertical ? e.Y < _renderer.GripperExtent : e.X < _renderer.GripperExtent);
        if (onGripper && Parent is DockHost dragHost && _bar is { AllowFloat: true })
        {
            // A top-level popup menu anchors on the WHOLE bar, so the message filter
            // treats a gripper press as "on the anchor" and won't dismiss it. Close
            // it explicitly so undocking/moving the bar doesn't strand an open menu.
            CloseMenu();
            _dragArmed = true;
            _dragGrab = e.Location;
            Capture = true;
            dragHost.BeginBarDrag(_bar, Size, e.Location);
            return;
        }

        if (!Stretch && Docked && ChevronRect().Contains(e.Location))
        {
            // Like a combo box, clicking the already-open chevron toggles its
            // popup closed. The menu session deliberately ignores clicks on its
            // anchor, so the owning control must perform this toggle itself.
            if (_overflowOpen && _openWindow is not null)
            {
                _chevronPressed = false;
                CloseMenu();
                return;
            }
            _chevronPressed = true;
            Invalidate();
            OpenOverflow();
            return;
        }

        // A hosted combo opens its dropdown list on release (opening it here,
        // while the button is still down, would show the popup then immediately
        // dismiss it on the mouse-up).
        var comboHit = HitTestCombo(e.Location);
        if (comboHit is not null)
        {
            // Clicking a combo whose list is already open toggles it closed — and
            // must NOT re-open on release (that would make the click a no-op).
            if (ReferenceEquals(_openCombo, comboHit) && _comboWindow is not null)
            {
                CloseComboDropDown();
                _pressedCombo = null;
                Invalidate();
                return;
            }
            _pressedCombo = comboHit;
            Invalidate(); // show the pressed effect immediately
            return;
        }

        var item = HitTest(e.Location);
        if (item is null)
            return;

        if (item is CommandBarPopupItem popup)
        {
            ToggleMenu(popup);
            return;
        }

        if (item is CommandBarCommandItem cmd && cmd.Command.Enabled)
        {
            // Split dropdowns open on mouse-up. Close an already-open dropdown
            // here on mouse-down and do not arm the button, otherwise mouse-up
            // would immediately open a replacement popup.
            if (cmd is CommandBarSplitButton split && OnSplitArrow(split, e.Location)
                && ReferenceEquals(_openSplitButton, split) && _openWindow is not null)
            {
                CloseMenu();
                _pressedItem = null;
                _pressedSplitArrow = false;
                return;
            }
            _pressedItem = item;
            _pressedSplitArrow = cmd is CommandBarSplitButton pressedSplit && OnSplitArrow(pressedSplit, e.Location);
            Invalidate();
        }
    }

    protected override void OnMouseUp(MouseEventArgs e)
    {
        base.OnMouseUp(e);
        if (e.Button != MouseButtons.Left)
            return;

        // Also guard release in case Customize mode began after mouse-down.
        if (Customizing && !_itemDragArmed)
        {
            _dragArmed = false;
            _dragging = false;
            _pressedItem = null;
            _pressedCombo = null;
            _pressedSplitArrow = false;
            _chevronPressed = false;
            Capture = false;
            Invalidate();
            return;
        }

        if (_itemDragArmed)
        {
            _itemDragArmed = false;
            Capture = false;
            if (_itemDragging)
                EndItemDrag(Cursor.Position);
            else
                _bar?.Manager?.HideDropMarker();
            _itemDragging = false;
            _itemDragItem = null;
            Cursor = Cursors.Default;
            return;
        }

        if (_dragArmed)
        {
            _dragArmed = false;
            Capture = false;
            if (Parent is DockHost host)
            {
                if (_dragging)
                    host.EndBarDrag(Cursor.Position, floatOutside: true);
                else
                    host.CancelBarDrag();
            }
            _dragging = false;
            return;
        }

        // A pressed combo opens its dropdown on release, over the same combo.
        var pressedCombo = _pressedCombo;
        _pressedCombo = null;
        if (pressedCombo is not null)
        {
            if (ReferenceEquals(HitTestCombo(e.Location), pressedCombo))
                OpenComboDropDown(pressedCombo); // sets _openCombo and repaints
            else
                Invalidate(); // released off the combo: clear the pressed effect
            return;
        }

        _chevronPressed = false;
        var pressed = _pressedItem;
        _pressedItem = null;
        _pressedSplitArrow = false;
        Invalidate();

        if (pressed is null)
            return;

        var item = HitTest(e.Location);
        if (ReferenceEquals(item, pressed) && pressed is CommandBarCommandItem cmd)
            Activate(cmd, e.Location);
    }

    private void Activate(CommandBarCommandItem cmd, Point location)
    {
        if (Customizing || !cmd.Command.Enabled)
            return;

        if (cmd is CommandBarSplitButton split && OnSplitArrow(split, location))
        {
            var arrowRect = Vertical
                ? new Rectangle(split.Bounds.X, split.Bounds.Bottom - _metrics.ArrowWidth, split.Bounds.Width, _metrics.ArrowWidth)
                : new Rectangle(split.Bounds.Right - _metrics.ArrowWidth, split.Bounds.Y, _metrics.ArrowWidth, split.Bounds.Height);
            // Anchor dismissal on the arrow, so clicking elsewhere closes it.
            ShowDropDown(split.DropDown, split, split.Bounds, arrowRect);
        }
        else
        {
            // Perform latches checkable commands itself (IsCheckable).
            cmd.Command.Perform();
        }

        ItemClicked?.Invoke(this, new CommandBarItemClickedEventArgs(cmd));
        Invalidate();
    }

    // The split-button arrow strip is on the right of a horizontal button and
    // along the bottom of a vertical one.
    private bool OnSplitArrow(CommandBarSplitButton split, Point location)
        => Vertical
            ? location.Y >= split.Bounds.Bottom - _metrics.ArrowWidth
            : location.X >= split.Bounds.Right - _metrics.ArrowWidth;

    // --- Mnemonics ---------------------------------------------------------

    /// <summary>Opens the top-level menu whose caption has the given mnemonic.</summary>
    public bool TryMnemonic(char charCode)
    {
        if (_bar is null || _bar.BarType != CommandBarType.MenuBar)
            return false;
        foreach (var item in _bar.Items)
        {
            if (item is CommandBarPopupItem popup && popup.Visible && IsMnemonic(charCode, popup.Text))
            {
                Focus();
                OpenMenu(popup);
                return true;
            }
        }
        return false;
    }

    protected override bool ProcessMnemonic(char charCode)
    {
        // Only respond to menu mnemonics when Alt is held. Otherwise a bare
        // character bubbling to the form's ProcessDialogChar would open a menu,
        // stealing keystrokes from a focused text control.
        if ((ModifierKeys & Keys.Alt) == 0)
            return base.ProcessMnemonic(charCode);
        return TryMnemonic(charCode) || base.ProcessMnemonic(charCode);
    }

    // --- Dropdown menus ----------------------------------------------------

    private void ToggleMenu(CommandBarPopupItem popup)
    {
        if (ReferenceEquals(_openMenuItem, popup))
            CloseMenu();
        else
            OpenMenu(popup);
    }

    private void OpenMenu(CommandBarPopupItem popup)
    {
        CloseMenu();
        _bar?.Manager?.PreparePopup(popup);
        var session = MenuSession.Begin(this);
        var window = CreatePopup(popup.DropDown);
        TrackPopup(window, menuItem: popup);
        session.Add(window);
        ShowPopupAtBarEdge(window, RectangleToScreen(popup.Bounds));
        Invalidate();
    }

    /// <summary>
    /// Opens the previous/next top-level menu relative to the one currently
    /// open (used by Left/Right arrow navigation from the menu bar).
    /// </summary>
    internal void OpenAdjacentTopMenu(int direction)
    {
        if (_bar is null || _openMenuItem is null)
            return;

        var popups = new List<CommandBarPopupItem>();
        foreach (var item in _bar.Items)
            if (item is CommandBarPopupItem p && p.Visible)
                popups.Add(p);

        int idx = popups.IndexOf(_openMenuItem);
        if (idx < 0)
            return;

        idx = (((idx + direction) % popups.Count) + popups.Count) % popups.Count;
        OpenMenu(popups[idx]);
        _openWindow?.SelectFirst();
    }

    private void ShowDropDown(CommandBar dropDown, CommandBarSplitButton split,
        Rectangle clientPlacementBounds, Rectangle clientDismissBounds)
    {
        Rectangle dismissScreenBounds = RectangleToScreen(clientDismissBounds);
        var session = MenuSession.Begin(this, dismissScreenBounds);
        var window = CreatePopup(dropDown);
        TrackPopup(window, splitButton: split);
        session.Add(window);
        ShowPopupAtBarEdge(window, RectangleToScreen(clientPlacementBounds));
        Invalidate();
    }

    private void CloseMenu()
    {
        _openWindow = null;
        _openMenuItem = null;
        _openSplitButton = null;
        _overflowOpen = false;
        MenuSession.Current?.End();
        Invalidate();
    }

    private void TrackPopup(CommandBarPopupWindow window, CommandBarPopupItem? menuItem = null,
        CommandBarSplitButton? splitButton = null, bool overflow = false)
    {
        _openWindow = window;
        _openMenuItem = menuItem;
        _openSplitButton = splitButton;
        _overflowOpen = overflow;
        window.FormClosed += (_, _) =>
        {
            // A newer popup may already have replaced this one. Only the active
            // window is allowed to clear the owning control's popup state.
            if (!ReferenceEquals(_openWindow, window))
                return;
            _openWindow = null;
            _openMenuItem = null;
            _openSplitButton = null;
            _overflowOpen = false;
            Invalidate();
        };
    }

    private Rectangle PopupButtonAnchor(Rectangle bounds, bool overflow)
    {
        if (_renderer.UsesFluentMenuChrome)
        {
            // Align with the painted button, rather than its larger hit target.
            int inset = (int)Math.Round((overflow ? 3 : Vertical ? 4 : 2) * _dpiScale);
            if (Vertical) bounds.Inflate(0, -inset);
            else bounds.Inflate(-inset, 0);
        }
        return bounds;
    }

    private void ShowPopupAtBarEdge(CommandBarPopupWindow window, Rectangle anchorScreenBounds, bool overflow = false)
    {
        anchorScreenBounds = PopupButtonAnchor(anchorScreenBounds, overflow);
        int gap = (int)Math.Round(_renderer.PopupGap * _dpiScale);
        if (Vertical) anchorScreenBounds.Inflate(gap, 0);
        else anchorScreenBounds.Inflate(0, gap);
        if (Vertical)
        {
            // A right-docked toolbar opens inward (left); a left-docked toolbar
            // opens inward (right). ShowBeside flips this preference when the
            // window has been dragged mostly off that side of the monitor or the
            // popup otherwise cannot fit there.
            bool preferLeft = _bar!.Dock == DockState.Right;
            window.ShowBeside(anchorScreenBounds, preferLeft,
                connectToAnchor: _renderer.ConnectPopupOwners);
        }
        else
        {
            // Bottom-docked bars open upward. Every other horizontal bar opens
            // downward, with automatic flipping when the working area is tight.
            bool preferBelow = _bar!.Dock != DockState.Bottom;
            window.ShowBelow(anchorScreenBounds, preferBelow,
                connectToAnchor: _renderer.ConnectPopupOwners);
        }
    }

    private CommandBarPopupWindow CreatePopup(CommandBar bar)
    {
        var window = new CommandBarPopupWindow(bar, _renderer, Font, _bar!.IconSize, _dpiScale, TearOff);
        var form = FindForm();
        if (form is not null)
            window.Owner = form;
        return window;
    }

    // Floats a torn-off popup bar into a standalone palette via the manager. Wired
    // into every popup this control opens (menus, split dropdowns) so a bar that
    // opts in (CommandBar.AllowTearOff) can be dragged out by its grip.
    private void TearOff(CommandBar bar, Point screenLocation)
        => _bar?.Manager?.ShowTearOff(bar, screenLocation, FindForm());

    // Doubles '&' so a toolbar name isn't misread as carrying a mnemonic when
    // used as the label of the chevron's toolbar-name submenu.
    private static string EscapeMnemonics(string? text)
        => string.IsNullOrEmpty(text) ? string.Empty : text.Replace("&", "&&");

    private void OpenOverflow()
    {
        if (_bar is null)
            return;

        var overflow = new CommandBar(_bar.Name + ".overflow", CommandBarType.Popup)
        {
            IconSize = _bar.IconSize,
            // Split overflow rows share their source dropdown. Give the
            // temporary bar the same manager before adding them so collection
            // ownership propagation cannot clear that dropdown's manager.
            Manager = _bar.Manager,
        };

        if (_overflowItems.Count > 0)
        {
            for (int i = 0; i < _bar.Items.Count; i++)
            {
                var item = _bar.Items[i];
                if (!item.Visible || !IsOverflowed(item))
                    continue;
                switch (item)
                {
                    case CommandBarToggleButton t: overflow.Items.AddToggle(t.Command); break;
                    case CommandBarSplitButton s:
                        overflow.Items.AddSplitButton(s.Command, s.DropDown);
                        break;
                    case CommandBarButton btn: overflow.Items.AddButton(btn.Command); break;
                    case CommandBarPopupItem popup: overflow.Items.AddPopup(popup); break;
                    case CommandBarSeparator: overflow.Items.AddSeparator(); break;
                    // A combo can't be hosted inside a menu popup, so surface it as
                    // a submenu of its choices (the current value checked). Picking
                    // one sets the selection just like opening the list would.
                    case CommandBarComboBox combo when combo.Items.Count > 0:
                    {
                        string caption = combo.Label
                            ?? combo.SelectedItem?.ToString()
                            ?? combo.Name
                            ?? "Select";
                        var sub = overflow.Items.AddPopup(EscapeMnemonics(caption));
                        var target = combo;
                        foreach (var value in combo.Items)
                        {
                            var choice = value;
                            var pick = new Command("combo:" + (combo.Name ?? "combo") + ":" + (choice?.ToString() ?? string.Empty))
                            {
                                Text = choice?.ToString() ?? string.Empty,
                                Enabled = target.Enabled,
                                IsCheckable = true,
                                Checked = Equals(target.SelectedItem, choice)
                                    ? CommandCheckState.Checked : CommandCheckState.Unchecked,
                            };
                            pick.ExecuteHandler = _ =>
                            {
                                target.SelectedItem = choice;
                                _bar.Manager?.RefreshLayout();
                            };
                            sub.DropDown.Items.AddToggle(pick);
                        }
                        break;
                    }
                }
            }
            overflow.Items.AddSeparator();
        }

        // "Add or Remove Buttons" ▶ — matches Office's nesting:
        //   Add or Remove Buttons ▶
        //     {Toolbar name} ▶
        //       ☑ item ... (one checkable entry per item, toggling visibility)
        //       ────────
        //       Reset Toolbar
        //     ────────
        //     Customize...
        var addRemove = overflow.Items.AddPopup("&Add or Remove Buttons");

        // The toolbar-name submenu holds the item checklist and Reset.
        var toolbarMenu = addRemove.DropDown.Items.AddPopup(EscapeMnemonics(_bar.Text));
        for (int itemIndex = 0; itemIndex < _bar.Items.Count; itemIndex++)
        {
            var item = _bar.Items[itemIndex];
            if (item is CommandBarSeparator)
                continue;

            string text;
            IImageSource? image;
            Keys shortcut;
            switch (item)
            {
                case CommandBarCommandItem commandItem:
                    text = commandItem.Command.Text;
                    image = commandItem.Command.Image;
                    shortcut = commandItem.Command.Shortcut;
                    break;
                case CommandBarPopupItem popup:
                    text = popup.Text;
                    image = popup.Image;
                    shortcut = Keys.None;
                    break;
                case CommandBarComboBox combo:
                    text = combo.Label
                        ?? combo.SelectedItem?.ToString()
                        ?? combo.Name
                        ?? "Combo Box";
                    image = combo.Image;
                    shortcut = Keys.None;
                    break;
                case CommandBarLabel label:
                    text = label.Text;
                    image = null;
                    shortcut = Keys.None;
                    break;
                default:
                    continue;
            }

            var target = item;
            var toggle = new Command("customize:" + _bar.Name + ":" + (item.Name ?? itemIndex.ToString()))
            {
                Text = text,
                Image = image,
                Shortcut = shortcut,
                IsCheckable = true,
                Checked = item.Visible ? CommandCheckState.Checked : CommandCheckState.Unchecked,
            };
            toggle.ExecuteHandler = _ =>
            {
                target.Visible = !target.Visible;
                _bar.Manager?.RefreshLayout();
            };
            toolbarMenu.DropDown.Items.AddToggle(toggle);
        }

        toolbarMenu.DropDown.Items.AddSeparator();
        var resetBar = _bar;
        var reset = new Command("customize:reset:" + _bar.Name) { Text = "&Reset Toolbar" };
        reset.ExecuteHandler = _ =>
        {
            resetBar.Manager?.ResetBar(resetBar);
            resetBar.Manager?.RefreshLayout();
        };
        toolbarMenu.DropDown.Items.AddButton(reset);

        // "Customize..." launches the host app's Customize dialog.
        addRemove.DropDown.Items.AddSeparator();
        var customizeBar = _bar;
        var customize = new Command("customize:dialog:" + _bar.Name) { Text = "&Customize..." };
        customize.ExecuteHandler = _ => customizeBar.Manager?.RequestCustomize();
        addRemove.DropDown.Items.AddButton(customize);

        // Anchor the dismissal region on just the chevron, so clicking anywhere
        // else (including elsewhere on this toolbar) closes the flyout.
        var session = MenuSession.Begin(this, RectangleToScreen(ChevronRect()));
        var window = CreatePopup(overflow);
        TrackPopup(window, overflow: true);
        session.Add(window);
        ShowPopupAtBarEdge(window, RectangleToScreen(ChevronRect()), overflow: true);
    }

    // Polls the physical Alt key so the menu bar's mnemonic underlines appear
    // while it is held and clear when released, reliably through menu mode.
    private void UpdateAltCue()
    {
        bool alt = (GetAsyncKeyState(VK_MENU) & 0x8000) != 0;
        if (alt != _altHeld)
        {
            _altHeld = alt;
            Invalidate();
        }
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            UnsubscribeCommands();
            CloseComboDropDown();
            _toolTip.Dispose();
            _altTimer?.Dispose();
            if (_comboFont is not null && !ReferenceEquals(_comboFont, Font))
                _comboFont.Dispose();
        }
        base.Dispose(disposing);
    }
}
