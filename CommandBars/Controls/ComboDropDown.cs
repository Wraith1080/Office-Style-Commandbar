using System;
using System.Drawing;
using System.Windows.Forms;
using CommandBars.Model;
using CommandBars.Rendering;

namespace CommandBars.Controls;

/// <summary>
/// A small borderless dropdown list shown when a hosted <see cref="CommandBarComboBox"/>
/// is clicked. Themed with the bar's renderer; closes on selection, Escape, or
/// clicking away. Raises <see cref="ItemChosen"/> with the picked value.
///
/// It is <b>non-activating</b>: like <see cref="CommandBarPopupWindow"/>, showing
/// it must not steal focus from the owner form (otherwise the form's title bar
/// goes inactive every time the combo opens). Because a non-activating window
/// never receives <c>WM_ACTIVATE</c>/deactivate, click-away closing is driven by
/// an <see cref="IMessageFilter"/> instead of <c>OnDeactivate</c>.
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

    private readonly ListBox _list;
    private readonly CommandBarRenderer _renderer;
    private bool _filtering;

    public ComboDropDown(CommandBarComboBox combo, CommandBarRenderer renderer, Font font, Rectangle boxScreen)
    {
        _renderer = renderer;

        FormBorderStyle = FormBorderStyle.None;
        ShowInTaskbar = false;
        StartPosition = FormStartPosition.Manual;
        MinimizeBox = false;
        MaximizeBox = false;
        // 1px themed border: the form's edge shows through the list's padding.
        BackColor = renderer.Colors.BarBorder;
        Padding = new Padding(1);

        _list = new ListBox
        {
            Dock = DockStyle.Fill,
            Font = font,
            BorderStyle = BorderStyle.None,
            IntegralHeight = false,
            DrawMode = DrawMode.OwnerDrawFixed,
            ItemHeight = font.Height + 6,
        };
        foreach (var value in combo.Items)
            _list.Items.Add(value!);
        if (combo.SelectedItem is not null)
            _list.SelectedItem = combo.SelectedItem;

        _list.DrawItem += OnDrawItem;
        _list.MouseClick += (_, e) =>
        {
            int i = _list.IndexFromPoint(e.Location);
            if (i >= 0)
                Choose(_list.Items[i]);
        };
        _list.KeyDown += (_, e) =>
        {
            if (e.KeyCode == Keys.Enter && _list.SelectedIndex >= 0)
                Choose(_list.SelectedItem);
            else if (e.KeyCode == Keys.Escape)
                Close();
        };

        Controls.Add(_list);

        int visible = Math.Min(combo.Items.Count, 12);
        int height = (visible * _list.ItemHeight) + 2;
        Size = new Size(Math.Max(boxScreen.Width, 60), height);

        Rectangle wa = Screen.FromRectangle(boxScreen).WorkingArea;
        int y = boxScreen.Bottom;
        if (y + Height > wa.Bottom)
            y = boxScreen.Top - Height; // flip above if it won't fit below
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
            return cp;
        }
    }

    protected override void WndProc(ref Message m)
    {
        // Clicking the list must not activate the window (which would deactivate
        // the owner form). Tell Windows not to activate on mouse-down.
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
    /// receives. Clicks inside the list are left to flow through normally.
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
                if (!IsDisposed && !Bounds.Contains(Cursor.Position))
                    Close();
                break;
        }
        return false; // never swallow the message
    }

    private void OnDrawItem(object? sender, DrawItemEventArgs e)
    {
        if (e.Index < 0)
            return;
        bool selected = (e.State & DrawItemState.Selected) != 0;

        using (var back = new SolidBrush(Color.White))
            e.Graphics.FillRectangle(back, e.Bounds);
        if (selected)
            _renderer.DrawMenuItemBackground(e.Graphics, e.Bounds, RenderState.Hot);

        string text = _list.Items[e.Index]?.ToString() ?? string.Empty;
        var textRect = new Rectangle(e.Bounds.X + 4, e.Bounds.Y, e.Bounds.Width - 6, e.Bounds.Height);
        TextRenderer.DrawText(e.Graphics, text, Font, textRect, _renderer.Colors.Text,
            TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis);
    }

    private void Choose(object? value)
    {
        ItemChosen?.Invoke(value);
        Close();
    }
}
