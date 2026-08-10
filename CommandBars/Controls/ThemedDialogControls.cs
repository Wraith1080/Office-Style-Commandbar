using CommandBars.Rendering;
using System.Drawing.Drawing2D;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace CommandBars.Controls;

internal interface IDialogThemedControl
{
    CommandBarDialogColorTable DialogColors { set; }
}

internal sealed class ThemedButton : Button, IDialogThemedControl
{
    private CommandBarDialogColorTable _colors = new(new Office2003ColorTable());
    private bool _hot;
    private bool _pressed;
    private bool _defaultButton;

    public ThemedButton()
    {
        SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint |
                 ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
        UseVisualStyleBackColor = false;
        FlatStyle = FlatStyle.Flat;
        FlatAppearance.BorderSize = 0;
    }

    public CommandBarDialogColorTable DialogColors
    {
        set
        {
            _colors = value;
            BackColor = value.ButtonBegin;
            ForeColor = value.ButtonText;
            Invalidate();
        }
    }

    public override void NotifyDefault(bool value)
    {
        _defaultButton = value;
        Invalidate();
        base.NotifyDefault(value);
    }

    public override Size GetPreferredSize(Size proposedSize)
    {
        Size text = TextRenderer.MeasureText(Text, Font, Size.Empty,
            TextFormatFlags.SingleLine | TextFormatFlags.NoPadding);
        int x = Math.Max(18, (int)Math.Round(18 * DeviceDpi / 96f));
        int y = Math.Max(10, (int)Math.Round(10 * DeviceDpi / 96f));
        return new Size(text.Width + x, Math.Max(text.Height + y, (int)Math.Round(26 * DeviceDpi / 96f)));
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        Rectangle bounds = ClientRectangle;
        if (bounds.Width <= 0 || bounds.Height <= 0)
            return;

        bool pressed = Enabled && _pressed;
        Color begin = pressed ? _colors.ButtonPressedBegin : _hot && Enabled ? _colors.ButtonHotBegin : _colors.ButtonBegin;
        Color end = pressed ? _colors.ButtonPressedEnd : _hot && Enabled ? _colors.ButtonHotEnd : _colors.ButtonEnd;
        Color border = pressed ? _colors.ButtonPressedBorder : _hot && Enabled ? _colors.ButtonHotBorder : _colors.ButtonBorder;

        using (var fill = new LinearGradientBrush(bounds, begin, end, LinearGradientMode.Vertical))
            e.Graphics.FillRectangle(fill, bounds);

        using (var pen = new Pen(_defaultButton && Enabled ? _colors.Accent : border))
            e.Graphics.DrawRectangle(pen, 0, 0, bounds.Width - 1, bounds.Height - 1);

        Rectangle textBounds = Rectangle.Inflate(bounds, -5, -3);
        if (pressed)
            textBounds.Offset(1, 1);
        TextFormatFlags textFlags = TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter |
            TextFormatFlags.SingleLine | TextFormatFlags.EndEllipsis;
        if (!UseMnemonic)
            textFlags |= TextFormatFlags.NoPrefix;
        TextRenderer.DrawText(e.Graphics, Text, Font, textBounds,
            Enabled ? _colors.ButtonText : _colors.DisabledText,
            textFlags);

        if (Focused && ShowFocusCues)
        {
            Rectangle focus = Rectangle.Inflate(bounds, -4, -4);
            ControlPaint.DrawFocusRectangle(e.Graphics, focus, _colors.ButtonText, Color.Transparent);
        }
    }

    protected override void OnMouseEnter(EventArgs e) { _hot = true; Invalidate(); base.OnMouseEnter(e); }
    protected override void OnMouseLeave(EventArgs e) { _hot = false; _pressed = false; Invalidate(); base.OnMouseLeave(e); }
    protected override void OnMouseDown(MouseEventArgs e) { if (e.Button == MouseButtons.Left) _pressed = true; Invalidate(); base.OnMouseDown(e); }
    protected override void OnMouseUp(MouseEventArgs e) { _pressed = false; Invalidate(); base.OnMouseUp(e); }
    protected override void OnEnabledChanged(EventArgs e) { Invalidate(); base.OnEnabledChanged(e); }
    protected override void OnGotFocus(EventArgs e) { Invalidate(); base.OnGotFocus(e); }
    protected override void OnLostFocus(EventArgs e) { Invalidate(); base.OnLostFocus(e); }
}

internal sealed class DialogTabPage : Panel
{
    public DialogTabPage(string text)
    {
        Text = text;
        Padding = new Padding(8);
    }
}

internal sealed class ThemedTabControl : ContainerControl, IDialogThemedControl
{
    private readonly List<DialogTabPage> _pages = new();
    private CommandBarDialogColorTable _colors = new(new Office2003ColorTable());
    private int _selectedIndex = -1;
    private int _hotIndex = -1;
    private int _headerHeight;

    public ThemedTabControl()
    {
        SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint |
                 ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw |
                 ControlStyles.Selectable, true);
        TabStop = true;
        RecomputeMetrics();
    }

    public CommandBarDialogColorTable DialogColors
    {
        set
        {
            _colors = value;
            BackColor = value.Window;
            ForeColor = value.Text;
            foreach (var page in _pages)
            {
                page.BackColor = value.TabBody;
                page.ForeColor = value.Text;
            }
            Invalidate(true);
        }
    }

    public void AddPage(DialogTabPage page)
    {
        ArgumentNullException.ThrowIfNull(page);
        page.Visible = false;
        page.BackColor = _colors.TabBody;
        page.ForeColor = _colors.Text;
        _pages.Add(page);
        Controls.Add(page);
        if (_selectedIndex < 0)
            SelectedIndex = 0;
        PerformLayout();
    }

    public int SelectedIndex
    {
        get => _selectedIndex;
        set
        {
            if (value < 0 || value >= _pages.Count || value == _selectedIndex)
                return;
            if (_selectedIndex >= 0)
                _pages[_selectedIndex].Visible = false;
            _selectedIndex = value;
            _pages[value].Visible = true;
            _pages[value].BringToFront();
            Invalidate();
        }
    }

    private void RecomputeMetrics()
    {
        _headerHeight = Math.Max(Font.Height + Scale(14), Scale(30));
        PerformLayout();
    }

    private int Scale(int logical) => (int)Math.Round(logical * DeviceDpi / 96f);

    protected override void OnLayout(LayoutEventArgs levent)
    {
        base.OnLayout(levent);
        var pageBounds = new Rectangle(1, _headerHeight,
            Math.Max(0, ClientSize.Width - 2), Math.Max(0, ClientSize.Height - _headerHeight - 1));
        foreach (var page in _pages)
            page.Bounds = pageBounds;
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);
        Rectangle header = new(0, 0, Width, _headerHeight);
        using (var strip = new SolidBrush(_colors.InactiveTab))
            e.Graphics.FillRectangle(strip, header);

        using (var surface = new SolidBrush(_colors.TabBody))
            e.Graphics.FillRectangle(surface, new Rectangle(1, _headerHeight - 1, Math.Max(0, Width - 2), Math.Max(0, Height - _headerHeight)));
        using (var border = new Pen(_colors.Border))
            e.Graphics.DrawRectangle(border, 0, _headerHeight - 1, Math.Max(0, Width - 1), Math.Max(0, Height - _headerHeight));

        for (int i = 0; i < _pages.Count; i++)
            DrawTab(e.Graphics, i, TabBounds(i));

        if (Focused && ShowFocusCues && _selectedIndex >= 0)
            ControlPaint.DrawFocusRectangle(e.Graphics, Rectangle.Inflate(TabBounds(_selectedIndex), -5, -5), _colors.Text, Color.Transparent);
    }

    private void DrawTab(Graphics graphics, int index, Rectangle bounds)
    {
        bool selected = index == _selectedIndex;
        bool hot = index == _hotIndex;
        Color fillColor = selected ? _colors.ActiveTab : hot ? _colors.ButtonHotBegin : _colors.InactiveTab;
        Color border = hot ? _colors.ButtonHotBorder : _colors.Border;

        if (selected || hot)
        {
            using var fill = new SolidBrush(fillColor);
            graphics.FillRectangle(fill, bounds);
            using var pen = new Pen(border);
            graphics.DrawLines(pen, new[]
            {
                new Point(bounds.Left, bounds.Bottom - 1), new Point(bounds.Left, bounds.Top),
                new Point(bounds.Right - 1, bounds.Top), new Point(bounds.Right - 1, bounds.Bottom - 1)
            });
            if (selected)
            {
                using var merge = new Pen(_colors.TabBody, 2);
                graphics.DrawLine(merge, bounds.Left + 1, bounds.Bottom - 1, bounds.Right - 2, bounds.Bottom - 1);
            }
        }

        TextRenderer.DrawText(graphics, _pages[index].Text, Font, bounds, _colors.Text,
            TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.SingleLine);
    }

    internal Rectangle TabBounds(int index)
    {
        int[] widths = TabWidths();
        int left = Scale(5);
        for (int i = 0; i < index; i++)
            left += widths[i];
        return new Rectangle(left, Scale(3), widths[index], _headerHeight - Scale(3));
    }

    private int[] TabWidths()
    {
        int count = _pages.Count;
        if (count == 0)
            return Array.Empty<int>();

        int available = Math.Max(count, ClientSize.Width - Scale(10));
        int[] desired = new int[count];
        int[] minimum = new int[count];
        for (int i = 0; i < count; i++)
        {
            int textWidth = TextRenderer.MeasureText(_pages[i].Text, Font, Size.Empty,
                TextFormatFlags.SingleLine | TextFormatFlags.NoPadding).Width;
            desired[i] = textWidth + Scale(24);
            minimum[i] = textWidth + Scale(8);
        }

        int desiredTotal = desired.Sum();
        if (desiredTotal <= available)
            return desired;

        int minimumTotal = minimum.Sum();
        return minimumTotal <= available
            ? InterpolateWidths(minimum, desired, available)
            : ProportionalWidths(minimum, available);
    }

    internal int MinimumTabStripWidth
    {
        get
        {
            int width = Scale(10);
            foreach (DialogTabPage page in _pages)
                width += TextRenderer.MeasureText(page.Text, Font, Size.Empty,
                    TextFormatFlags.SingleLine | TextFormatFlags.NoPadding).Width + Scale(8);
            return width;
        }
    }

    private static int[] InterpolateWidths(int[] minimum, int[] desired, int available)
    {
        int minimumTotal = minimum.Sum();
        int expandableTotal = desired.Sum() - minimumTotal;
        int extra = available - minimumTotal;
        int[] result = new int[minimum.Length];
        int used = 0;
        for (int i = 0; i < result.Length; i++)
        {
            int share = expandableTotal == 0 ? 0
                : (int)Math.Floor((desired[i] - minimum[i]) * (double)extra / expandableTotal);
            result[i] = minimum[i] + share;
            used += result[i];
        }
        for (int i = 0; used < available; i = (i + 1) % result.Length, used++)
            result[i]++;
        return result;
    }

    private static int[] ProportionalWidths(int[] widths, int available)
    {
        int total = widths.Sum();
        int[] result = new int[widths.Length];
        int used = 0;
        for (int i = 0; i < result.Length; i++)
        {
            result[i] = Math.Max(1, (int)Math.Floor(widths[i] * (double)available / total));
            used += result[i];
        }
        for (int i = 0; used < available; i = (i + 1) % result.Length, used++)
            result[i]++;
        while (used > available)
            for (int i = result.Length - 1; i >= 0 && used > available; i--)
                if (result[i] > 1)
                {
                    result[i]--;
                    used--;
                }
        return result;
    }

    private int HitTest(Point point)
    {
        if (point.Y >= _headerHeight)
            return -1;
        for (int i = 0; i < _pages.Count; i++)
            if (TabBounds(i).Contains(point))
                return i;
        return -1;
    }

    protected override bool IsInputKey(Keys keyData)
        => keyData is Keys.Left or Keys.Right or Keys.Home or Keys.End || base.IsInputKey(keyData);

    protected override void OnKeyDown(KeyEventArgs e)
    {
        if (_pages.Count > 0)
        {
            int next = e.KeyCode switch
            {
                Keys.Left => (_selectedIndex - 1 + _pages.Count) % _pages.Count,
                Keys.Right => (_selectedIndex + 1) % _pages.Count,
                Keys.Home => 0,
                Keys.End => _pages.Count - 1,
                _ when e.Control && e.KeyCode == Keys.Tab => e.Shift
                    ? (_selectedIndex - 1 + _pages.Count) % _pages.Count
                    : (_selectedIndex + 1) % _pages.Count,
                _ => -1,
            };
            if (next >= 0)
            {
                SelectedIndex = next;
                e.Handled = true;
            }
        }
        base.OnKeyDown(e);
    }

    protected override void OnMouseDown(MouseEventArgs e)
    {
        int index = HitTest(e.Location);
        if (index >= 0)
        {
            Focus();
            SelectedIndex = index;
        }
        base.OnMouseDown(e);
    }

    protected override void OnMouseMove(MouseEventArgs e)
    {
        int index = HitTest(e.Location);
        if (index != _hotIndex)
        {
            _hotIndex = index;
            Invalidate(new Rectangle(0, 0, Width, _headerHeight));
        }
        base.OnMouseMove(e);
    }

    protected override void OnMouseLeave(EventArgs e)
    {
        _hotIndex = -1;
        Invalidate(new Rectangle(0, 0, Width, _headerHeight));
        base.OnMouseLeave(e);
    }

    protected override void OnFontChanged(EventArgs e) { RecomputeMetrics(); Invalidate(); base.OnFontChanged(e); }
    protected override void OnDpiChangedAfterParent(EventArgs e) { RecomputeMetrics(); Invalidate(); base.OnDpiChangedAfterParent(e); }
    protected override void OnGotFocus(EventArgs e) { Invalidate(); base.OnGotFocus(e); }
    protected override void OnLostFocus(EventArgs e) { Invalidate(); base.OnLostFocus(e); }
}

internal sealed class ThemedCheckedListBox : CheckedListBox, IDialogThemedControl
{
    private CommandBarDialogColorTable _colors = new(new Office2003ColorTable());

    public ThemedCheckedListBox()
    {
        DrawMode = DrawMode.OwnerDrawFixed;
        BorderStyle = BorderStyle.FixedSingle;
        IntegralHeight = false;
    }

    public CommandBarDialogColorTable DialogColors
    {
        set
        {
            _colors = value;
            BackColor = value.InputBackground;
            ForeColor = value.InputText;
            Invalidate();
        }
    }

    protected override void OnDrawItem(DrawItemEventArgs e)
    {
        if (e.Index < 0)
            return;
        bool selected = (e.State & DrawItemState.Selected) != 0;
        using (var fill = new SolidBrush(selected ? _colors.ButtonHotBegin : _colors.InputBackground))
            e.Graphics.FillRectangle(fill, e.Bounds);

        int boxSize = Math.Min(14, Math.Max(10, e.Bounds.Height - 6));
        Rectangle box = new(e.Bounds.Left + 4, e.Bounds.Top + (e.Bounds.Height - boxSize) / 2, boxSize, boxSize);
        using (var boxFill = new SolidBrush(_colors.Surface))
            e.Graphics.FillRectangle(boxFill, box);
        using (var pen = new Pen(selected ? _colors.ButtonHotBorder : _colors.Border))
            e.Graphics.DrawRectangle(pen, box);
        if (GetItemChecked(e.Index))
        {
            using var pen = new Pen(_colors.Accent, 2f);
            e.Graphics.DrawLines(pen, new[]
            {
                new Point(box.Left + 3, box.Top + box.Height / 2),
                new Point(box.Left + box.Width / 2 - 1, box.Bottom - 4),
                new Point(box.Right - 2, box.Top + 3),
            });
        }

        Rectangle text = new(box.Right + 6, e.Bounds.Top, Math.Max(0, e.Bounds.Right - box.Right - 8), e.Bounds.Height);
        TextRenderer.DrawText(e.Graphics, GetItemText(Items[e.Index]), Font, text,
            selected ? _colors.SelectionText : _colors.InputText,
            TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis);
        if ((e.State & DrawItemState.Focus) != 0)
            ControlPaint.DrawFocusRectangle(e.Graphics, e.Bounds, _colors.InputText, Color.Transparent);
    }
}

internal sealed class ThemedListBox : ListBox, IDialogThemedControl
{
    private CommandBarDialogColorTable _colors = new(new Office2003ColorTable());

    public ThemedListBox()
    {
        DrawMode = DrawMode.OwnerDrawFixed;
        BorderStyle = BorderStyle.FixedSingle;
        IntegralHeight = false;
    }

    public CommandBarDialogColorTable DialogColors
    {
        set
        {
            _colors = value;
            BackColor = value.InputBackground;
            ForeColor = value.InputText;
            Invalidate();
        }
    }

    protected override void OnDrawItem(DrawItemEventArgs e)
    {
        if (e.Index < 0)
            return;
        bool selected = (e.State & DrawItemState.Selected) != 0;
        using (var fill = new SolidBrush(selected ? _colors.ButtonHotBegin : _colors.InputBackground))
            e.Graphics.FillRectangle(fill, e.Bounds);
        Rectangle text = Rectangle.Inflate(e.Bounds, -5, 0);
        TextRenderer.DrawText(e.Graphics, GetItemText(Items[e.Index]), Font, text,
            selected ? _colors.SelectionText : _colors.InputText,
            TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis);
        if ((e.State & DrawItemState.Focus) != 0)
            ControlPaint.DrawFocusRectangle(e.Graphics, e.Bounds, _colors.InputText, Color.Transparent);
    }
}

internal sealed class ThemedComboBox : ComboBox, IDialogThemedControl
{
    private const int WmEraseBackground = 0x0014;
    private const int WmPaint = 0x000F;
    private const int WmPrint = 0x0317;
    private const int WmPrintClient = 0x0318;
    private CommandBarDialogColorTable _colors = new(new Office2003ColorTable());
    private bool _hot;
    private bool _droppedDown;

    public ThemedComboBox()
    {
        DrawMode = DrawMode.OwnerDrawFixed;
        FlatStyle = FlatStyle.Flat;
    }

    public CommandBarDialogColorTable DialogColors
    {
        set
        {
            _colors = value;
            BackColor = value.InputBackground;
            ForeColor = value.InputText;
            Invalidate();
        }
    }

    protected override void OnDrawItem(DrawItemEventArgs e)
    {
        if (e.Index < 0)
            return;
        bool selected = (e.State & DrawItemState.Selected) != 0;
        using (var fill = new SolidBrush(selected ? _colors.ButtonHotBegin : _colors.InputBackground))
            e.Graphics.FillRectangle(fill, e.Bounds);
        Rectangle text = Rectangle.Inflate(e.Bounds, -3, 0);
        TextRenderer.DrawText(e.Graphics, GetItemText(Items[e.Index]), Font, text,
            selected ? _colors.SelectionText : _colors.InputText,
            TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis);
    }

    protected override void WndProc(ref Message m)
    {
        if (DropDownStyle != ComboBoxStyle.DropDownList || Width <= 0 || Height <= 0)
        {
            base.WndProc(ref m);
            return;
        }

        if (m.Msg == WmPaint)
        {
            using Graphics target = Graphics.FromHwnd(Handle);
            using BufferedGraphics buffer = BufferedGraphicsManager.Current.Allocate(target, ClientRectangle);
            DrawClosedCombo(buffer.Graphics);
            buffer.Render(target);
            _ = ValidateRect(Handle, IntPtr.Zero);
            m.Result = IntPtr.Zero;
            return;
        }
        if ((m.Msg == WmPrint || m.Msg == WmPrintClient) && m.WParam != IntPtr.Zero)
        {
            using Graphics graphics = Graphics.FromHdc(m.WParam);
            DrawClosedCombo(graphics);
            m.Result = IntPtr.Zero;
            return;
        }
        if (m.Msg == WmEraseBackground)
        {
            m.Result = (IntPtr)1;
            return;
        }

        base.WndProc(ref m);
    }

    private void DrawClosedCombo(Graphics graphics)
    {
        Rectangle bounds = ClientRectangle;
        using (var background = new SolidBrush(_colors.InputBackground))
            graphics.FillRectangle(background, bounds);

        int width = Math.Min(SystemInformation.VerticalScrollBarWidth, ClientSize.Width);
        Rectangle button = new(ClientSize.Width - width, 0, width, ClientSize.Height);
        if (button.Width <= 0 || button.Height <= 0)
            return;
        Color buttonColor = _droppedDown ? _colors.ButtonPressedBegin
            : _hot ? _colors.ButtonHotBegin
            : _colors.ButtonBegin;
        Color buttonBorder = _droppedDown ? _colors.ButtonPressedBorder
            : _hot ? _colors.ButtonHotBorder
            : _colors.ButtonBorder;
        using (var fill = new SolidBrush(buttonColor))
            graphics.FillRectangle(fill, button);
        using (var divider = new Pen(buttonBorder))
            graphics.DrawLine(divider, button.Left, button.Top, button.Left, button.Bottom - 1);

        Rectangle textBounds = new(5, 1, Math.Max(0, button.Left - 8), Math.Max(0, ClientSize.Height - 2));
        string text = (SelectedItem is { } selected ? GetItemText(selected) : Text) ?? string.Empty;
        TextRenderer.DrawText(graphics, text, Font, textBounds,
            Enabled ? _colors.InputText : _colors.DisabledText,
            TextFormatFlags.Left | TextFormatFlags.VerticalCenter |
            TextFormatFlags.SingleLine | TextFormatFlags.EndEllipsis);

        int cx = button.Left + button.Width / 2;
        int cy = button.Top + button.Height / 2;
        using var arrow = new SolidBrush(Enabled ? _colors.ButtonText : _colors.DisabledText);
        graphics.FillPolygon(arrow, new[]
        {
            new Point(cx - 3, cy - 1),
            new Point(cx + 3, cy - 1),
            new Point(cx, cy + 2),
        });

        using var border = new Pen(Focused && Enabled ? _colors.Accent : _colors.Border);
        graphics.DrawRectangle(border, 0, 0, Math.Max(0, bounds.Width - 1), Math.Max(0, bounds.Height - 1));
    }

    protected override void OnMouseEnter(EventArgs e) { _hot = true; Invalidate(); base.OnMouseEnter(e); }
    protected override void OnMouseLeave(EventArgs e) { _hot = false; Invalidate(); base.OnMouseLeave(e); }
    protected override void OnDropDown(EventArgs e) { _droppedDown = true; Invalidate(); base.OnDropDown(e); }
    protected override void OnDropDownClosed(EventArgs e) { _droppedDown = false; Invalidate(); base.OnDropDownClosed(e); }
    protected override void OnGotFocus(EventArgs e) { Invalidate(); base.OnGotFocus(e); }
    protected override void OnLostFocus(EventArgs e) { Invalidate(); base.OnLostFocus(e); }
    protected override void OnEnabledChanged(EventArgs e) { Invalidate(); base.OnEnabledChanged(e); }

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool ValidateRect(IntPtr hWnd, IntPtr rect);
}

internal sealed class ThemedFlowLayoutPanel : FlowLayoutPanel, IDialogThemedControl
{
    private CommandBarDialogColorTable _colors = new(new Office2003ColorTable());
    public bool UseAlternateSurface { get; set; }
    public bool UseTabBodySurface { get; set; }
    public bool UseWindowSurface { get; set; }
    public bool StretchChildrenHorizontally { get; set; }

    public ThemedFlowLayoutPanel()
    {
        SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint |
                 ControlStyles.OptimizedDoubleBuffer, true);
    }

    public CommandBarDialogColorTable DialogColors
    {
        set
        {
            _colors = value;
            BackColor = SurfaceColor(value);
            ForeColor = value.Text;
            Invalidate(true);
        }
    }

    protected override void OnPaintBackground(PaintEventArgs e)
    {
        if (ClientRectangle.Width <= 0 || ClientRectangle.Height <= 0)
            return;
        Color begin = SurfaceColor(_colors);
        Color end = UseAlternateSurface ? _colors.Window : begin;
        using var fill = new LinearGradientBrush(ClientRectangle, begin, end, LinearGradientMode.Vertical);
        e.Graphics.FillRectangle(fill, ClientRectangle);
    }

    protected override void OnLayout(LayoutEventArgs levent)
    {
        if (StretchChildrenHorizontally)
            StretchChildren();
        base.OnLayout(levent);

        // AutoScroll can become visible during the first layout pass. Account
        // for its width and run one more pass so stretched buttons never sit
        // underneath the vertical scrollbar.
        if (StretchChildrenHorizontally && StretchChildren())
            base.OnLayout(levent);
    }

    private bool StretchChildren()
    {
        int scrollbar = VerticalScroll.Visible ? SystemInformation.VerticalScrollBarWidth : 0;
        int available = Math.Max(0, ClientSize.Width - Padding.Horizontal - scrollbar);
        bool changed = false;
        foreach (Control child in Controls)
        {
            Size preferred = child.GetPreferredSize(new Size(available, 0));
            int width = Math.Max(0, available - child.Margin.Horizontal);
            int height = Math.Max(preferred.Height, child.MinimumSize.Height);
            if (child.Width != width || child.Height != height)
            {
                child.Size = new Size(width, height);
                changed = true;
            }
        }
        return changed;
    }

    private Color SurfaceColor(CommandBarDialogColorTable colors)
        => UseAlternateSurface ? colors.SurfaceAlternate
            : UseTabBodySurface ? colors.TabBody
            : UseWindowSurface ? colors.Window
            : colors.Surface;
}

internal sealed class ThemedTableLayoutPanel : TableLayoutPanel, IDialogThemedControl
{
    private CommandBarDialogColorTable _colors = new(new Office2003ColorTable());
    public bool UseAlternateSurface { get; set; }
    public bool UseTabBodySurface { get; set; }
    public bool UseWindowSurface { get; set; }

    public CommandBarDialogColorTable DialogColors
    {
        set
        {
            _colors = value;
            BackColor = SurfaceColor(value);
            ForeColor = value.Text;
            Invalidate(true);
        }
    }

    protected override void OnPaintBackground(PaintEventArgs e)
    {
        using var fill = new SolidBrush(SurfaceColor(_colors));
        e.Graphics.FillRectangle(fill, ClientRectangle);
    }

    private Color SurfaceColor(CommandBarDialogColorTable colors)
        => UseAlternateSurface ? colors.SurfaceAlternate
            : UseTabBodySurface ? colors.TabBody
            : UseWindowSurface ? colors.Window
            : colors.Surface;
}

internal sealed class ThemedFooterPanel : Panel, IDialogThemedControl
{
    private CommandBarDialogColorTable _colors = new(new Office2003ColorTable());

    public ThemedFooterPanel()
    {
        SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint |
                 ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
    }

    /// <summary>Logical 96-DPI gap between the rightmost button and this panel's edge.</summary>
    public int RightInset { get; set; } = 12;

    /// <summary>Logical 96-DPI gap between adjacent footer buttons.</summary>
    public int ButtonGap { get; set; } = 6;

    public CommandBarDialogColorTable DialogColors
    {
        set
        {
            _colors = value;
            BackColor = value.Window;
            ForeColor = value.Text;
            Invalidate(true);
        }
    }

    public override Size GetPreferredSize(Size proposedSize)
    {
        int width = Scale(RightInset);
        int height = 0;
        int gap = Scale(ButtonGap);
        foreach (Control control in Controls)
        {
            Size preferred = control.AutoSize ? control.GetPreferredSize(Size.Empty) : control.Size;
            width += Math.Max(preferred.Width, control.MinimumSize.Width);
            height = Math.Max(height, Math.Max(preferred.Height, control.MinimumSize.Height));
        }
        if (Controls.Count > 1)
            width += gap * (Controls.Count - 1);
        return new Size(width, height + Scale(12));
    }

    protected override void OnLayout(LayoutEventArgs levent)
    {
        base.OnLayout(levent);

        int right = ClientSize.Width - Scale(RightInset);
        int gap = Scale(ButtonGap);
        foreach (Control control in Controls)
        {
            Size preferred = control.AutoSize ? control.GetPreferredSize(Size.Empty) : control.Size;
            preferred.Width = Math.Max(preferred.Width, control.MinimumSize.Width);
            preferred.Height = Math.Max(preferred.Height, control.MinimumSize.Height);
            control.Size = preferred;
            right -= control.Width;
            control.Location = new Point(right, Math.Max(0, (ClientSize.Height - control.Height) / 2));
            right -= gap;
        }
    }

    protected override void OnPaintBackground(PaintEventArgs e)
    {
        using var fill = new SolidBrush(_colors.Window);
        e.Graphics.FillRectangle(fill, ClientRectangle);
    }

    protected override void OnDpiChangedAfterParent(EventArgs e)
    {
        base.OnDpiChangedAfterParent(e);
        PerformLayout();
    }

    private int Scale(int logical) => (int)Math.Round(logical * DeviceDpi / 96f);
}

internal static class DialogSkin
{
    private const int DwmUseImmersiveDarkMode = 20;
    private static readonly ConditionalWeakTable<Control, NativeThemeState> NativeThemeStates = new();

    public static void Apply(Control root, CommandBarDialogColorTable colors)
    {
        root.SuspendLayout();
        ApplyRecursive(root, colors, root);
        root.ResumeLayout(true);
        root.Invalidate(true);
    }

    public static void ApplyNativeFrame(Form form, CommandBarDialogColorTable colors)
    {
        if (!form.IsHandleCreated || !OperatingSystem.IsWindowsVersionAtLeast(10, 0, 17763))
            return;
        int enabled = colors.IsDark ? 1 : 0;
        _ = DwmSetWindowAttribute(form.Handle, DwmUseImmersiveDarkMode, ref enabled, sizeof(int));
    }

    public static void ApplyWhenHandleCreated(Form form, CommandBarDialogColorTable colors)
        => form.HandleCreated += (_, _) => ApplyNativeFrame(form, colors);

    private static void ApplyRecursive(Control control, CommandBarDialogColorTable colors, Control root)
    {
        if (control is IDialogThemedControl themed)
            themed.DialogColors = colors;

        switch (control)
        {
            case Form:
                control.BackColor = colors.Window;
                control.ForeColor = colors.Text;
                break;
            case DialogTabPage:
                control.BackColor = colors.TabBody;
                control.ForeColor = colors.Text;
                break;
            case TextBoxBase textBox:
                textBox.BackColor = colors.InputBackground;
                textBox.ForeColor = colors.InputText;
                textBox.BorderStyle = BorderStyle.FixedSingle;
                break;
            case TreeView tree:
                tree.BackColor = colors.InputBackground;
                tree.ForeColor = colors.InputText;
                tree.LineColor = colors.Border;
                tree.BorderStyle = BorderStyle.FixedSingle;
                break;
            case Panel when control is not IDialogThemedControl:
                control.BackColor = colors.Surface;
                control.ForeColor = colors.Text;
                break;
            case Label or CheckBox:
                control.BackColor = control.Parent?.BackColor ?? colors.Surface;
                control.ForeColor = colors.Text;
                break;
            default:
                if (control is not IDialogThemedControl && !ReferenceEquals(control, root))
                    control.ForeColor = colors.Text;
                break;
        }

        if (control is TreeView or ListBox or ComboBox || control is ScrollableControl { AutoScroll: true })
            PrepareNativeTheme(control, colors);

        foreach (Control child in control.Controls)
            ApplyRecursive(child, colors, root);
    }

    private static void PrepareNativeTheme(Control control, CommandBarDialogColorTable colors)
    {
        var state = NativeThemeStates.GetOrCreateValue(control);
        state.Colors = colors;
        if (!state.Hooked)
        {
            control.HandleCreated += OnThemedControlHandleCreated;
            state.Hooked = true;
        }
        if (control.IsHandleCreated)
            ApplyNativeControlTheme(control, colors);
    }

    private static void OnThemedControlHandleCreated(object? sender, EventArgs e)
    {
        if (sender is Control control && NativeThemeStates.TryGetValue(control, out var state) && state.Colors is not null)
            ApplyNativeControlTheme(control, state.Colors);
    }

    private static void ApplyNativeControlTheme(Control control, CommandBarDialogColorTable colors)
    {
        if (!OperatingSystem.IsWindowsVersionAtLeast(10, 0, 17763))
            return;
        _ = SetWindowTheme(control.Handle, colors.IsDark ? "DarkMode_Explorer" : "Explorer", null);
    }

    private sealed class NativeThemeState
    {
        public bool Hooked { get; set; }
        public CommandBarDialogColorTable? Colors { get; set; }
    }

    [DllImport("dwmapi.dll")]
    private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attribute, ref int value, int valueSize);

    [DllImport("uxtheme.dll", CharSet = CharSet.Unicode)]
    private static extern int SetWindowTheme(IntPtr hwnd, string? subAppName, string? subIdList);
}
