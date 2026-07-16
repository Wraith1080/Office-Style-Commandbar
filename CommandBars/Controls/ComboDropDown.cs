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
/// </summary>
internal sealed class ComboDropDown : Form
{
    /// <summary>Raised with the chosen value when the user picks an item.</summary>
    public event Action<object?>? ItemChosen;

    private readonly ListBox _list;
    private readonly CommandBarRenderer _renderer;

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

    protected override void OnDeactivate(EventArgs e)
    {
        base.OnDeactivate(e);
        Close(); // clicking away cancels
    }

    private void Choose(object? value)
    {
        ItemChosen?.Invoke(value);
        Close();
    }
}
