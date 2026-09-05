using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;
using CommandBars.Model;
using CommandBars.Rendering;

namespace CommandBars.Controls;

/// <summary>
/// A small borderless dropdown list shown when a hosted <see cref="CommandBarComboBox"/>
/// is clicked. Themed with the bar's renderer; closes on selection or clicking
/// away. Raises <see cref="ItemChosen"/> with the picked value.
///
/// It is <b>non-activating</b>: like <see cref="CommandBarPopupWindow"/>, showing
/// it must not steal focus from the owner form (otherwise the form's title bar
/// goes inactive every time the combo opens). Because a non-activating window
/// never receives <c>WM_ACTIVATE</c>/deactivate, click-away closing is driven by
/// an <see cref="IMessageFilter"/> instead of <c>OnDeactivate</c>.
///
/// <b>Critical:</b> it hosts <b>no focusable child control</b>. An earlier version
/// used a child <see cref="ListBox"/>, which calls <c>SetFocus</c> on click; even
/// on a <c>WS_EX_NOACTIVATE</c> window that forces the owner form to deactivate
/// and reactivate on close — the visible title-bar "flicker". Instead it paints
/// its own rows and hit-tests the mouse directly, exactly like
/// <see cref="CommandBarPopupWindow"/>, so the owner form keeps focus throughout.
/// </summary>
internal sealed class ComboDropDown : Form, IMessageFilter
{
    /// <summary>Raised with the chosen value when the user picks an item.</summary>
    public event Action<object?>? ItemChosen;

    private const int WS_EX_NOACTIVATE = 0x08000000;
    private const int WS_EX_TOOLWINDOW = 0x00000080;
    private const int WM_MOUSEACTIVATE = 0x0021;
    private const int MA_NOACTIVATE = 3;
    private const int WM_LBUTTONDOWN = 0x0201;
    private const int WM_RBUTTONDOWN = 0x0204;
    private const int WM_MBUTTONDOWN = 0x0207;
    private const int WM_NCLBUTTONDOWN = 0x00A1;
    private const int WM_NCRBUTTONDOWN = 0x00A4;

    private readonly CommandBarRenderer _renderer;
    private readonly Font _font;
    private readonly List<object?> _items = new();
    private readonly int _rowHeight;
    private readonly int _visibleRows;
    private readonly int _inset;
    private readonly int _textInset;
    private readonly float _dpiScale;
    // Screen rect of the combo button that owns this list. A mouse-down here is
    // NOT treated as "clicked away": the owning control toggles the list closed
    // itself, so auto-closing here too would let the same click re-open it.
    private readonly Rectangle _ownerScreen;

    private int _hotIndex = -1;        // row under the mouse (hover-follow highlight)
    private int _selectedIndex = -1;   // the combo's current value
    private int _scroll;               // index of the first visible row
    private bool _filtering;

    public ComboDropDown(CommandBarComboBox combo, CommandBarRenderer renderer, Font font, Rectangle boxScreen, int minWidth = 60, Rectangle ownerScreen = default)
    {
        _renderer = renderer;
        _dpiScale = renderer.Scale;
        _inset = renderer.UsesFluentMenuChrome ? Math.Max(1, (int)Math.Round(4 * _dpiScale)) : 1;
        _textInset = renderer.UsesFluentMenuChrome ? (int)Math.Round(14 * _dpiScale) : 4;
        _font = font;
        _ownerScreen = ownerScreen;

        foreach (var value in combo.Items)
            _items.Add(value);
        if (combo.SelectedItem is not null)
            _selectedIndex = _items.IndexOf(combo.SelectedItem);

        FormBorderStyle = FormBorderStyle.None;
        ShowInTaskbar = false;
        StartPosition = FormStartPosition.Manual;
        MinimizeBox = false;
        MaximizeBox = false;
        DoubleBuffered = true;
        SetStyle(ControlStyles.ResizeRedraw, true);
        // The 1px themed border is painted in OnPaint; the field follows the
        // renderer so dark and application-defined themes remain cohesive.
        BackColor = renderer.DialogColors.InputBackground;
        ForeColor = renderer.DialogColors.InputText;

        _rowHeight = font.Height + (renderer.UsesFluentMenuChrome ? (int)Math.Round(12 * _dpiScale) : 6);
        _visibleRows = Math.Min(Math.Max(_items.Count, 1), 12);

        int width = Math.Max(boxScreen.Width, minWidth);
        if (renderer.UsesFluentMenuChrome)
            foreach (var item in _items)
                width = Math.Max(width, TextRenderer.MeasureText(item?.ToString() ?? string.Empty, font).Width + _textInset + 2 * _inset + (int)Math.Round(12 * _dpiScale));
        int height = (_visibleRows * _rowHeight) + 2 * _inset;
        Size = new Size(width, height);
        Region = renderer.CreatePopupRegion(ClientRectangle);

        // Scroll so the current selection is visible, and pre-highlight it.
        if (_selectedIndex >= _visibleRows)
            _scroll = Math.Min(_selectedIndex - _visibleRows + 1, Math.Max(0, _items.Count - _visibleRows));

        Rectangle wa = Screen.FromRectangle(boxScreen).WorkingArea;
        int gap = (int)Math.Round(renderer.PopupGap * _dpiScale);
        int y = boxScreen.Bottom + gap;
        if (y + Height > wa.Bottom)
            y = boxScreen.Top - Height - gap; // flip above if it won't fit below
        int x = Math.Min(boxScreen.Left, wa.Right - Width);
        Location = new Point(Math.Max(wa.Left, x), Math.Max(wa.Top, y));
    }

    // Do not activate when shown — keep the owner form focused.
    protected override bool ShowWithoutActivation => true;

    protected override CreateParams CreateParams
    {
        get
        {
            var cp = base.CreateParams;
            cp.ExStyle |= WS_EX_NOACTIVATE | WS_EX_TOOLWINDOW;
            if (_renderer?.UsesFluentMenuChrome == true) cp.ClassStyle |= 0x00020000; // CS_DROPSHADOW
            return cp;
        }
    }

    protected override void WndProc(ref Message m)
    {
        // Clicking must not activate the window (which would deactivate the owner
        // form). Tell Windows not to activate on mouse-down.
        if (m.Msg == WM_MOUSEACTIVATE)
        {
            m.Result = (IntPtr)MA_NOACTIVATE;
            return;
        }
        base.WndProc(ref m);
    }

    protected override void OnHandleCreated(EventArgs e)
    {
        base.OnHandleCreated(e);
        if (_renderer is not null) PopupWindowChrome.Apply(this, _renderer);
        if (!_filtering)
        {
            Application.AddMessageFilter(this);
            _filtering = true;
        }
    }

    protected override void OnFormClosed(FormClosedEventArgs e)
    {
        if (_filtering)
        {
            Application.RemoveMessageFilter(this);
            _filtering = false;
        }
        base.OnFormClosed(e);
    }

    /// <summary>
    /// Closes the dropdown when a mouse-down lands outside its bounds. This
    /// replaces the deactivate-based close, which a non-activating window never
    /// receives. Clicks inside flow through to <see cref="OnMouseUp"/>.
    /// </summary>
    bool IMessageFilter.PreFilterMessage(ref Message m)
    {
        switch (m.Msg)
        {
            case WM_LBUTTONDOWN:
            case WM_RBUTTONDOWN:
            case WM_MBUTTONDOWN:
            case WM_NCLBUTTONDOWN:
            case WM_NCRBUTTONDOWN:
                if (!IsDisposed && !Bounds.Contains(Cursor.Position)
                    && !_ownerScreen.Contains(Cursor.Position))
                    Close();
                break;
        }
        return false; // never swallow the message
    }

    // --- Painting ----------------------------------------------------------

    private int MaxScroll => Math.Max(0, _items.Count - _visibleRows);

    // The row highlighted right now: the hovered row, or the current selection
    // when the mouse isn't over any row.
    private int HighlightIndex => _hotIndex >= 0 ? _hotIndex : _selectedIndex;

    protected override void OnPaint(PaintEventArgs e)
    {
        var g = e.Graphics;
        _renderer.Scale = _dpiScale;

        using (var background = new SolidBrush(BackColor))
            g.FillRectangle(background, ClientRectangle);
        if (_renderer.UsesFluentMenuChrome)
            _renderer.DrawMenuBackground(g, ClientRectangle);

        int highlight = HighlightIndex;
        for (int row = 0; row < _visibleRows; row++)
        {
            int idx = _scroll + row;
            if (idx >= _items.Count)
                break;

            var rowRect = new Rectangle(_inset, _inset + (row * _rowHeight), ClientSize.Width - 2 * _inset, _rowHeight);
            RenderState rowState = idx == highlight ? RenderState.Hot : RenderState.Normal;
            _renderer.DrawComboSelection(g, rowRect, idx == _selectedIndex, idx == highlight);

            string text = _items[idx]?.ToString() ?? string.Empty;
            var textRect = new Rectangle(rowRect.X + _textInset, rowRect.Y, rowRect.Width - _textInset - 2, rowRect.Height);
            _renderer.DrawMenuItemText(g, text, _font, textRect, rowState,
                TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.SingleLine | TextFormatFlags.EndEllipsis);
        }

        if (!_renderer.UsesFluentMenuChrome)
        {
            using var pen = new Pen(_renderer.Colors.MenuBorder);
            g.DrawRectangle(pen, 0, 0, ClientSize.Width - 1, ClientSize.Height - 1);
        }
    }

    // --- Interaction -------------------------------------------------------

    private int IndexAt(Point p)
    {
        if (p.X < _inset || p.X >= ClientSize.Width - _inset) return -1;
        int rel = p.Y - _inset;
        if (rel < 0)
            return -1;
        int row = rel / _rowHeight;
        if (row < 0 || row >= _visibleRows)
            return -1;
        int idx = _scroll + row;
        return (idx >= 0 && idx < _items.Count) ? idx : -1;
    }

    protected override void OnMouseMove(MouseEventArgs e)
    {
        base.OnMouseMove(e);
        int idx = IndexAt(e.Location);
        if (idx != _hotIndex)
        {
            _hotIndex = idx;
            Invalidate();
        }
    }

    protected override void OnMouseLeave(EventArgs e)
    {
        base.OnMouseLeave(e);
        if (_hotIndex != -1)
        {
            _hotIndex = -1;
            Invalidate();
        }
    }

    protected override void OnMouseUp(MouseEventArgs e)
    {
        base.OnMouseUp(e);
        if (e.Button != MouseButtons.Left)
            return;
        int idx = IndexAt(e.Location);
        if (idx >= 0)
            Choose(_items[idx]);
    }

    protected override void OnMouseWheel(MouseEventArgs e)
    {
        base.OnMouseWheel(e);
        if (_items.Count <= _visibleRows)
            return;
        int step = e.Delta > 0 ? -1 : 1; // wheel up scrolls the list up
        int next = Math.Clamp(_scroll + step, 0, MaxScroll);
        if (next != _scroll)
        {
            _scroll = next;
            _hotIndex = IndexAt(PointToClient(Cursor.Position));
            Invalidate();
        }
    }

    private void Choose(object? value)
    {
        ItemChosen?.Invoke(value);
        Close();
    }
}
