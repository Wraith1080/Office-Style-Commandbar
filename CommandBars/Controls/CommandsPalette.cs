using System.Drawing;
using System.Windows.Forms;
using CommandBars.Model;
using CommandBars.Rendering;

namespace CommandBars.Controls;

/// <summary>
/// A draggable list of commands. While the manager is in Customize mode, drag a
/// command onto a toolbar to add a new button for it. Reusable standalone (in a
/// tool window) or embedded in the Customize dialog's Commands tab. Drops reuse
/// the same insertion routing as in-place item drags, so the marker and landing
/// position match exactly.
/// </summary>
public sealed class CommandsPalette : Control
{
    private CommandBarManager? _manager;
    private CommandBarRenderer _renderer = new Office2003Renderer();
    private readonly List<CommandBarCustomizationItem> _items = new();

    private const int IconLogical = 16;
    private int _rowHeight = 22;
    private int _hot = -1;

    private bool _armed;
    private bool _dragging;
    private int _dragIndex = -1;
    private Point _grab;

    public CommandsPalette()
    {
        SetStyle(
            ControlStyles.OptimizedDoubleBuffer | ControlStyles.AllPaintingInWmPaint |
            ControlStyles.UserPaint | ControlStyles.ResizeRedraw,
            true);
        BackColor = SystemColors.Window;
    }

    /// <summary>The manager whose toolbars receive dropped commands.</summary>
    public CommandBarManager? Manager
    {
        get => _manager;
        set => _manager = value;
    }

    /// <summary>Renderer used for themed hover/text colors.</summary>
    public CommandBarRenderer Renderer
    {
        get => _renderer;
        set { _renderer = value ?? new Office2003Renderer(); Invalidate(); }
    }

    /// <summary>Sets the commands shown in the palette (in the given order).</summary>
    public void SetCommands(IEnumerable<Command> commands)
    {
        SetItems(commands?.Select(CommandBarCustomizationItem.FromCommand)
            ?? Enumerable.Empty<CommandBarCustomizationItem>());
    }

    /// <summary>
    /// Sets the complete palette entries. An entry may create a normal button or
    /// a compound item such as a combo box or popup hierarchy.
    /// </summary>
    public void SetItems(IEnumerable<CommandBarCustomizationItem> items)
    {
        _items.Clear();
        if (items is not null)
            _items.AddRange(items);
        RecomputeMetrics();
        Invalidate();
    }

    private void RecomputeMetrics()
    {
        float scale = DeviceDpi / 96f;
        int icon = (int)Math.Round(IconLogical * scale);
        int pad = (int)Math.Round(6 * scale);
        _rowHeight = Math.Max(icon + pad, Font.Height + pad);
        // Report content height so an AutoScroll host (the Customize dialog's
        // Commands tab) can scroll. Ignored when the palette is Dock=Fill.
        Height = Math.Max(_rowHeight, _rowHeight * _items.Count);
    }

    protected override void OnFontChanged(EventArgs e)
    {
        base.OnFontChanged(e);
        RecomputeMetrics();
    }

    protected override void OnDpiChangedAfterParent(EventArgs e)
    {
        base.OnDpiChangedAfterParent(e);
        RecomputeMetrics();
        Invalidate();
    }

    private int IndexAt(Point p)
    {
        if (_rowHeight <= 0)
            return -1;
        int i = p.Y / _rowHeight;
        return i >= 0 && i < _items.Count ? i : -1;
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        var g = e.Graphics;
        float scale = DeviceDpi / 96f;
        int icon = (int)Math.Round(IconLogical * scale);
        int pad = (int)Math.Round(4 * scale);

        using (var back = new SolidBrush(SystemColors.Window))
            g.FillRectangle(back, ClientRectangle);

        for (int i = 0; i < _items.Count; i++)
        {
            var row = new Rectangle(0, i * _rowHeight, Width, _rowHeight);
            if (!row.IntersectsWith(e.ClipRectangle))
                continue;

            if (i == _hot)
            {
                using var hot = new SolidBrush(_renderer.Colors.ButtonHotBegin);
                g.FillRectangle(hot, row);
                using var pen = new Pen(_renderer.Colors.ButtonHotBorder);
                g.DrawRectangle(pen, new Rectangle(row.X, row.Y, row.Width - 1, row.Height - 1));
            }

            var item = _items[i];
            int y = row.Y + ((row.Height - icon) / 2);
            if (item.Image is not null)
                g.DrawImage(item.Image.GetImage(IconLogical, scale), new Rectangle(pad, y, icon, icon));

            int textX = pad + icon + pad;
            var textRect = new Rectangle(textX, row.Y, Math.Max(1, Width - textX - pad), row.Height);
            TextRenderer.DrawText(g, item.Text, Font, textRect, _renderer.Colors.Text,
                TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis);
        }

        using var border = new Pen(_renderer.Colors.BarBorder);
        //g.DrawRectangle(border, 0, 0, Width - 1, Height - 1);
    }

    protected override void OnMouseMove(MouseEventArgs e)
    {
        base.OnMouseMove(e);

        if (_armed)
        {
            if (!_dragging)
            {
                var dz = SystemInformation.DragSize;
                if (Math.Abs(e.X - _grab.X) >= dz.Width || Math.Abs(e.Y - _grab.Y) >= dz.Height)
                    _dragging = true;
            }
            if (_dragging)
                UpdateDrag(Cursor.Position);
            return;
        }

        int hot = IndexAt(e.Location);
        if (hot != _hot)
        {
            _hot = hot;
            Invalidate();
        }
    }

    protected override void OnMouseLeave(EventArgs e)
    {
        base.OnMouseLeave(e);
        if (!_armed && _hot != -1)
        {
            _hot = -1;
            Invalidate();
        }
    }

    protected override void OnMouseDown(MouseEventArgs e)
    {
        base.OnMouseDown(e);
        if (e.Button != MouseButtons.Left)
            return;
        int i = IndexAt(e.Location);
        if (i < 0)
            return;
        _armed = true;
        _dragIndex = i;
        _grab = e.Location;
        Capture = true;
    }

    private void UpdateDrag(Point screen)
    {
        if (_manager is null)
            return;
        var target = _manager.FindDropTarget(screen, out _, out Rectangle marker);
        if (target is not null)
        {
            _manager.ShowDropMarker(marker);
            Cursor = Cursors.SizeAll;
        }
        else
        {
            _manager.HideDropMarker();
            Cursor = Cursors.No;
        }
    }

    protected override void OnMouseUp(MouseEventArgs e)
    {
        base.OnMouseUp(e);
        if (e.Button != MouseButtons.Left || !_armed)
            return;

        _armed = false;
        Capture = false;
        Cursor = Cursors.Default;
        bool dragged = _dragging;
        _dragging = false;
        int index = _dragIndex;
        _dragIndex = -1;

        var mgr = _manager;
        if (mgr is null)
            return;
        mgr.HideDropMarker();
        if (!dragged || index < 0 || index >= _items.Count)
            return;

        var target = mgr.FindDropTarget(Cursor.Position, out int insert, out _);
        if (target?.Bar is not { } bar)
            return;

        var item = _items[index].CreateItem();
        insert = Math.Clamp(insert, 0, bar.Items.Count);
        bar.Items.Insert(insert, item);
        // The palette is not a child of a host, so RefreshLayout won't dispose it.
        mgr.RefreshLayout();
    }
}
