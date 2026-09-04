using System.ComponentModel;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;
using CommandBars.Model;
using CommandBars.Rendering;

namespace CommandBars.Controls;

/// <summary>
/// A dock band on one edge of the form. Top/Bottom bands draw the rebar
/// background and pack bars into horizontal rows (multiple per row, grouped by
/// <see cref="CommandBar.Row"/>, ordered by <see cref="CommandBar.Offset"/>);
/// Left/Right bands pack bars into vertical columns. Several hosts (one per
/// edge) share a <see cref="CommandBarManager"/>, which lets a bar be dragged
/// from one band and dropped onto another (cross-edge docking), undocked to a
/// floating window, and re-docked, all with a drop-preview ghost.
/// </summary>
[ToolboxItem(true)]
[Description("Hosts command bars (menu bar and toolbars) in a dockable edge band.")]
[Designer("CommandBars.Designer.Server.DockHostDesigner, CommandBars.Designer.Server")]
public class DockHost : Panel
{
    private CommandBarManager? _manager;
    private CommandBarRenderer _renderer = new Office2003Renderer();
    private readonly List<CommandBarControl> _controls = new();
    private readonly Dictionary<CommandBar, FloatingWindow> _floating = new();

    private DockEdge _edge = DockEdge.Top;

    // For a horizontal band each entry is a row (Start = top, Extent = height);
    // for a vertical band each entry is a column (Start = left, Extent = width).
    private readonly List<(int Index, int Start, int Extent)> _lineBands = new();
    private int _menuExtent;

    // Drop decision computed by ComputeDockPreview on the target host.
    private bool _dropNewRow;
    private int _dropRowIndex;
    private int _dropOffset;

    // Preview UI owned by the host that started the drag.
    private Rectangle _preview = Rectangle.Empty;
    private DropPreviewWindow? _previewWindow;

    public DockHost()
    {
        Dock = DockStyle.Top;
        SetStyle(
            ControlStyles.OptimizedDoubleBuffer | ControlStyles.AllPaintingInWmPaint |
            ControlStyles.UserPaint | ControlStyles.ResizeRedraw,
            true);
    }

    /// <summary>Raised when any item on any hosted bar is clicked.</summary>
    public event EventHandler<CommandBarItemClickedEventArgs>? ItemClicked;

    /// <summary>Which edge this band occupies. Sets the control's dock style to match.</summary>
    [Category("CommandBars")]
    [Description("Which form edge this band docks to (Top/Bottom lay out in rows, Left/Right in columns).")]
    [DefaultValue(DockEdge.Top)]
    public DockEdge Edge
    {
        get => _edge;
        set
        {
            if (_edge == value)
                return;
            _edge = value;
            Dock = value switch
            {
                DockEdge.Left => DockStyle.Left,
                DockEdge.Right => DockStyle.Right,
                DockEdge.Bottom => DockStyle.Bottom,
                _ => DockStyle.Top,
            };
            Rebuild();
        }
    }

    /// <summary>True for Top/Bottom bands (rows); false for Left/Right (columns).</summary>
    private bool Horizontal => _edge is DockEdge.Top or DockEdge.Bottom;

    /// <summary>The dock state a bar must have to appear in this band.</summary>
    private DockState EdgeState => _edge switch
    {
        DockEdge.Left => DockState.Left,
        DockEdge.Right => DockState.Right,
        DockEdge.Bottom => DockState.Bottom,
        _ => DockState.Top,
    };

    /// <summary>The manager whose bars are shown.</summary>
    [Category("CommandBars")]
    [Description("The CommandBarManager whose bars this host displays.")]
    [DefaultValue(null)]
    public CommandBarManager? Manager
    {
        get => _manager;
        set
        {
            if (_manager is not null)
            {
                _manager.LayoutChanged -= OnManagerLayoutChanged;
                _manager.CustomizeChanged -= OnCustomizeChanged;
                _manager.UnregisterHost(this);
            }
            _manager = value;
            if (_manager is not null)
            {
                _manager.LayoutChanged += OnManagerLayoutChanged;
                _manager.CustomizeChanged += OnCustomizeChanged;
                _manager.RegisterHost(this);
            }
            Rebuild();
        }
    }

    // Hidden, non-serialized routed-editor entry points used by the out-of-
    // process DockHost smart tag. The editors run client-side in Visual Studio;
    // these properties carry no runtime state.
    [Browsable(false)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    [Editor("DockHostAddToolbarEditor", typeof(System.Drawing.Design.UITypeEditor))]
    public string DesignerAddToolbar { get => string.Empty; set { _ = value; } }

    [Browsable(false)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    [Editor("DockHostAddMenuBarEditor", typeof(System.Drawing.Design.UITypeEditor))]
    public string DesignerAddMenuBar { get => string.Empty; set { _ = value; } }

    [Browsable(false)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    [Editor("DockHostAddCommandsEditor", typeof(System.Drawing.Design.UITypeEditor))]
    public string DesignerAddCommands { get => string.Empty; set { _ = value; } }

    [Browsable(false)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    [Editor("DockHostAddCommandsToBarEditor", typeof(System.Drawing.Design.UITypeEditor))]
    public string DesignerAddCommandsToBar { get => string.Empty; set { _ = value; } }

    // Ephemeral design-server routing state. A per-bar adorner sets this before
    // opening DesignerAddCommandsToBar; it is never serialized or used at runtime.
    [Browsable(false)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public string DesignerTargetBarName { get; set; } = string.Empty;

    [Browsable(false)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    [Editor("DockHostEditBarsEditor", typeof(System.Drawing.Design.UITypeEditor))]
    public string DesignerEditBars { get => string.Empty; set { _ = value; } }

    [Browsable(false)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    [Editor("DockHostEditCatalogEditor", typeof(System.Drawing.Design.UITypeEditor))]
    public string DesignerEditCatalog { get => string.Empty; set { _ = value; } }

    private void OnManagerLayoutChanged(object? sender, EventArgs e) => Rebuild();

    private void OnCustomizeChanged(object? sender, EventArgs e)
    {
        foreach (var c in _controls)
            c.Invalidate();
        Invalidate();
    }

    /// <summary>The bar controls hosted in this band (used by item-drag routing).</summary>
    internal IReadOnlyList<CommandBarControl> BarControls => _controls;

    /// <summary>Shows the customize insertion marker at a screen rectangle.</summary>
    internal void ShowItemMarker(Rectangle screen) => SetPreview(screen);

    /// <summary>Hides the customize insertion marker.</summary>
    internal void HideItemMarker() => SetPreview(Rectangle.Empty);

    /// <summary>Active renderer; assigning re-themes every hosted bar.</summary>
    [Browsable(false)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public CommandBarRenderer Renderer
    {
        get => _renderer;
        set
        {
            _renderer = value ?? new Office2003Renderer();
            foreach (var c in _controls)
                c.Renderer = _renderer;
            foreach (var w in _floating.Values)
                w.SetRenderer(_renderer);
            LayoutBars();
            Invalidate();
        }
    }

    private bool IsFloatOwner => _manager is not null && ReferenceEquals(_manager.FloatOwner, this);

    /// <summary>Undocks a bar into a floating window at a screen location.</summary>
    public void FloatBar(CommandBar bar, Point screenLocation)
    {
        if (_manager is null || _manager.IsCustomizing || !bar.AllowFloat)
            return;
        bar.Dock = DockState.Floating;
        bar.FloatingBounds = new Rectangle(screenLocation, Size.Empty);
        _manager.RefreshLayout();
    }

    /// <summary>Re-docks a floating bar back onto the top band.</summary>
    public void DockBar(CommandBar bar)
    {
        bar.Dock = DockState.Top;
        if (_manager is not null)
            _manager.RefreshLayout();
        else
            Rebuild();
    }

    // Design-preview signature this host has already reflected into its controls.
    private string _appliedDesignSig = "\0";

    /// <summary>
    /// Design-time only: rebuild the preview and optionally force an immediate
    /// re-measure and repaint of this band. The manager defers that final work
    /// while rebuilding all edge hosts, then performs one coordinated parent
    /// layout and repaint.
    /// </summary>
    internal void RefreshDesignPreview(bool updateImmediately = true)
    {
        if (!DesignMode)
            return;
        try
        {
            Rebuild();
            PerformLayout();
            Invalidate(true);
            if (updateImmediately)
            {
                Parent?.PerformLayout();
                Update();
                Parent?.Update();
            }
        }
        catch
        {
            // A host mid-teardown must never break the designer.
        }
    }

    /// <summary>Rebuilds the hosted controls from the manager's bars.</summary>
    public void Rebuild()
    {
        // At design time, realize the manager's BarDefinitions into live bars so
        // the preview below shows the real toolbars with their items.
        if (DesignMode && _manager is not null)
        {
            _manager.EnsureDesignBars();
            _appliedDesignSig = _manager.DesignSig;
        }

        foreach (var c in _controls)
        {
            Controls.Remove(c);
            c.Dispose();
        }
        _controls.Clear();

        if (_manager is null)
        {
            if (Horizontal)
                Height = 0;
            else
                Width = 0;
            return;
        }

        foreach (var bar in OrderedBars())
        {
            var control = new CommandBarControl { Renderer = _renderer };
            control.ItemClicked += (_, e) => ItemClicked?.Invoke(this, e);
            Controls.Add(control);
            control.Bar = bar; // set after parenting
            _controls.Add(control);
        }

        LayoutBars();
        SyncFloatingWindows();
    }

    private IEnumerable<CommandBar> OrderedBars()
    {
        if (_manager is null)
            yield break;

        // The menu bar only lives on the Top edge, and stretches full width.
        if (_edge == DockEdge.Top)
            foreach (var bar in _manager.Bars)
                if (bar.Dock == DockState.Top && bar.Visible && bar.BarType == CommandBarType.MenuBar)
                    yield return bar;

        foreach (var bar in _manager.Bars)
            if (bar.Dock == EdgeState && bar.Visible && bar.BarType != CommandBarType.MenuBar)
                yield return bar;
    }

    private void LayoutBars()
    {
        if (Horizontal)
            LayoutRows();
        else
            LayoutColumns();
    }

    private void LayoutRows()
    {
        _lineBands.Clear();
        int y = 0;
        int clientWidth = ClientSize.Width;
        int tab = 0; // tab order follows visual order (left-to-right, top-to-bottom)

        // Menu bars: each full-width on its own row.
        foreach (var control in _controls)
        {
            if (!control.Stretch)
                continue;
            control.Relayout();
            control.Location = new Point(0, y);
            control.Width = clientWidth;
            control.TabIndex = tab++;
            y += control.Height;
        }
        _menuExtent = y;

        // Toolbars: grouped by Row, ordered by Offset, multiple per row.
        var toolbars = _controls.Where(c => !c.Stretch).ToList();
        foreach (var group in toolbars.GroupBy(c => c.Bar!.Row).OrderBy(g => g.Key))
        {
            var row = group.OrderBy(c => c.Bar!.Offset).ToList();
            foreach (var control in row)
                control.Relayout();

            int extentBudget = Math.Max(row.Count, clientWidth - row.Count - 1);
            int[] widths = AllocateDockedExtents(
                row.Select(c => c.PreferredContentWidth).ToArray(),
                row.Select(c => c.MinimumDockedExtent).ToArray(),
                extentBudget);

            int x = 1;
            int rowHeight = 0;
            for (int i = 0; i < row.Count; i++)
            {
                var control = row[i];
                control.Location = new Point(x, y);
                control.Width = widths[i];
                control.Bar!.Offset = x; // normalize to current position
                control.TabIndex = tab++;
                x += control.Width + 1;
                rowHeight = Math.Max(rowHeight, control.Height);
            }
            _lineBands.Add((group.Key, y, rowHeight));
            y += rowHeight;
        }

        // Give the empty host a visible, selectable strip on the design surface.
        Height = DesignMode && _controls.Count == 0 ? 28 : Math.Max(y, 1);
    }

    private void LayoutColumns()
    {
        _lineBands.Clear();
        _menuExtent = 0;
        int x = 0;
        int clientHeight = ClientSize.Height;
        int tab = 0; // tab order follows visual order (top-to-bottom, left-to-right)

        // Toolbars: grouped by Row (used here as the column index), ordered by
        // Offset, stacked top-to-bottom; multiple columns pack left-to-right.
        var toolbars = _controls.Where(c => !c.Stretch).ToList();
        foreach (var group in toolbars.GroupBy(c => c.Bar!.Row).OrderBy(g => g.Key))
        {
            var column = group.OrderBy(c => c.Bar!.Offset).ToList();
            foreach (var control in column)
                control.Relayout();

            int extentBudget = Math.Max(column.Count, clientHeight - column.Count - 1);
            int[] heights = AllocateDockedExtents(
                column.Select(c => c.PreferredContentHeight).ToArray(),
                column.Select(c => c.MinimumDockedExtent).ToArray(),
                extentBudget);

            int y = 1;
            int colWidth = 0;
            for (int i = 0; i < column.Count; i++)
            {
                var control = column[i];
                control.Location = new Point(x, y);
                control.Height = heights[i];
                control.Bar!.Offset = y; // normalize to current position
                control.TabIndex = tab++;
                y += control.Height + 1;
                colWidth = Math.Max(colWidth, control.Width);
            }
            _lineBands.Add((group.Key, x, colWidth));
            x += colWidth;
        }

        Width = DesignMode && _controls.Count == 0 ? 28 : Math.Max(x, 1);
    }

    /// <summary>
    /// Allocates one dock row/column. Preferred extents are capped from the
    /// longest downward, so a long toolbar yields space before a short one.
    /// Normal minimums preserve each gripper/chevron; at physically impossible
    /// sizes the same fair cap keeps every bar represented instead of reducing
    /// only the final bar to one pixel.
    /// </summary>
    internal static int[] AllocateDockedExtents(
        IReadOnlyList<int> preferred, IReadOnlyList<int> minimum, int available)
    {
        if (preferred.Count != minimum.Count)
            throw new ArgumentException("Preferred and minimum extent counts must match.");
        if (preferred.Count == 0)
            return Array.Empty<int>();

        int count = preferred.Count;
        available = Math.Max(count, available);
        var normalMin = new int[count];
        var normalPreferred = new int[count];
        long minimumTotal = 0;
        long preferredTotal = 0;
        for (int i = 0; i < count; i++)
        {
            normalMin[i] = Math.Max(1, minimum[i]);
            normalPreferred[i] = Math.Max(normalMin[i], preferred[i]);
            minimumTotal += normalMin[i];
            preferredTotal += normalPreferred[i];
        }

        if (available >= preferredTotal)
            return normalPreferred;

        // If even all usable minima cannot fit, degrade every bar evenly. This
        // preserves access to the final toolbar much longer than sequential
        // allocation and is the only possible behavior without adding rows.
        int[] floors;
        int[] targets;
        if (available < minimumTotal)
        {
            floors = Enumerable.Repeat(1, count).ToArray();
            targets = normalMin;
        }
        else
        {
            floors = normalMin;
            targets = normalPreferred;
        }

        int floorTotal = floors.Sum();
        if (available <= floorTotal)
            return floors;

        int low = 0;
        int high = targets.Max();
        while (low < high)
        {
            int cap = low + ((high - low + 1) / 2);
            long total = 0;
            for (int i = 0; i < count; i++)
                total += Math.Max(floors[i], Math.Min(targets[i], cap));
            if (total <= available)
                low = cap;
            else
                high = cap - 1;
        }

        var result = new int[count];
        int used = 0;
        for (int i = 0; i < count; i++)
        {
            result[i] = Math.Max(floors[i], Math.Min(targets[i], low));
            used += result[i];
        }

        // Integer capping can leave fewer than count pixels unassigned. Hand
        // them out stably to bars that still have capacity.
        int remainder = available - used;
        for (int i = 0; i < count && remainder > 0; i++)
        {
            if (result[i] >= targets[i])
                continue;
            result[i]++;
            remainder--;
        }
        return result;
    }

    // --- Floating windows --------------------------------------------------

    private void SyncFloatingWindows()
    {
        // Only one band (the float owner) creates and tracks floating windows,
        // so a bar undocked from any edge yields a single window.
        if (!IsFloatOwner)
            return;

        foreach (var pair in _floating.ToArray())
        {
            if (pair.Key.Dock != DockState.Floating || !pair.Key.Visible)
            {
                _floating.Remove(pair.Key);
                if (!pair.Value.IsDisposed)
                    pair.Value.Close();
            }
        }

        if (_manager is null || !IsHandleCreated)
            return;

        var owner = FindForm();
        foreach (var bar in _manager.Bars)
        {
            if (bar.Dock != DockState.Floating || !bar.Visible)
                continue;

            if (_floating.TryGetValue(bar, out var window))
            {
                window.SetRenderer(_renderer);
                continue;
            }

            window = new FloatingWindow(bar, _renderer, this, owner);
            _floating[bar] = window;
            Point loc = bar.FloatingBounds.Location;
            window.Location = loc == Point.Empty ? PointToScreen(new Point(40, Height + 40)) : loc;
            window.Show();
        }
    }

    // --- Drag session (shared across all hosts via the manager) ------------

    // Logical-pixel strip just inside the band that still counts as a dock
    // target, so a bar can be dropped past the last line to make a new one.
    private const int NewRowZone = 34;

    private int Dp(double logical) => (int)Math.Round(logical * (DeviceDpi / 96f));

    /// <summary>True if the screen point is over the dock band.</summary>
    public bool IsOverBand(Point screen) => RectangleToScreen(ClientRectangle).Contains(screen);

    /// <summary>
    /// True if the point is a valid dock target for this band: over the band, or
    /// just inside its inner edge (the "new line" strip toward the content).
    /// </summary>
    public bool ContainsDockZone(Point screen)
    {
        if (_manager is null || !Visible || !IsHandleCreated)
            return false;
        var band = RectangleToScreen(ClientRectangle);
        int margin = Dp(NewRowZone);
        var zone = _edge switch
        {
            DockEdge.Top => new Rectangle(band.X, band.Y, band.Width, band.Height + margin),
            DockEdge.Bottom => new Rectangle(band.X, band.Y - margin, band.Width, band.Height + margin),
            DockEdge.Left => new Rectangle(band.X, band.Y, band.Width + margin, band.Height),
            DockEdge.Right => new Rectangle(band.X - margin, band.Y, band.Width + margin, band.Height),
            _ => band,
        };
        return zone.Contains(screen);
    }

    /// <summary>Begins a drag of <paramref name="bar"/> with the given drag size.</summary>
    public void BeginBarDrag(CommandBar bar, Size dragSize, Point grab)
    {
        _manager?.BeginDrag(bar, dragSize, grab, this);
        SetPreview(Rectangle.Empty);
    }

    /// <summary>
    /// Updates the drag preview. Over any band it snaps to that band's target
    /// line; otherwise it follows the cursor (when <paramref name="floatGhost"/>).
    /// Called on the origin host, which owns the preview window.
    /// </summary>
    public void UpdateBarDrag(Point screen, bool floatGhost)
    {
        var session = _manager?.ActiveDrag;
        if (session is null)
            return;

        var target = _manager!.HitDockZone(screen);
        Rectangle ghost;
        if (target is not null)
            ghost = target.ComputeDockPreview(screen, FitToTarget(session.Size, target, session.Bar));
        else if (floatGhost)
            ghost = new Rectangle(screen.X - session.Grab.X, screen.Y - session.Grab.Y, session.Size.Width, session.Size.Height);
        else
            ghost = Rectangle.Empty;
        SetPreview(ghost);
    }

    /// <summary>
    /// Ends the drag: docks onto whichever band the cursor is over, or floats
    /// when <paramref name="floatOutside"/>. Returns true if it docked.
    /// </summary>
    public bool EndBarDrag(Point screen, bool floatOutside)
    {
        SetPreview(Rectangle.Empty);
        var session = _manager?.ActiveDrag;
        _manager?.EndDrag();
        if (session is null)
            return false;

        var target = _manager!.HitDockZone(screen);
        if (target is not null)
        {
            target.ComputeDockPreview(screen, FitToTarget(session.Size, target, session.Bar));
            bool newLine = target._dropNewRow;
            int lineIndex = target._dropRowIndex;
            int offset = target._dropOffset;
            var bar = session.Bar;
            target.BeginInvoke((MethodInvoker)(() => target.ApplyDrop(bar, newLine, lineIndex, offset)));
            return true;
        }

        if (floatOutside)
        {
            var drop = new Point(screen.X - session.Grab.X, screen.Y - session.Grab.Y);
            var bar = session.Bar;
            BeginInvoke((MethodInvoker)(() => FloatBar(bar, drop)));
        }
        return false;
    }

    /// <summary>Cancels a drag without docking or floating.</summary>
    public void CancelBarDrag()
    {
        SetPreview(Rectangle.Empty);
        _manager?.EndDrag();
    }

    /// <summary>
    /// Transposes the drag size when the dragged bar and the target band have
    /// different orientations: a horizontal bar's width becomes a vertical bar's
    /// length once rotated into the target column (and vice versa), so the
    /// preview matches the shape the bar will actually take when docked.
    /// </summary>
    private static Size FitToTarget(Size size, DockHost target, CommandBar bar)
    {
        bool sourceVertical = bar.Orientation == BarOrientation.Vertical;
        bool targetVertical = !target.Horizontal;
        return sourceVertical == targetVertical ? size : new Size(size.Height, size.Width);
    }

    private Rectangle ComputeDockPreview(Point screen, Size dragSize)
    {
        Point client = PointToClient(screen);
        int edge = Dp(7);
        int thickness = Math.Max(4, Dp(6));

        _dropNewRow = false;
        _dropRowIndex = 0;

        Rectangle rect;
        if (Horizontal)
        {
            int previewTop = _menuExtent;
            int previewHeight = dragSize.Height;
            bool handled = false;

            for (int i = 0; i < _lineBands.Count; i++)
            {
                var rb = _lineBands[i];
                if (client.Y < rb.Start + edge)
                {
                    _dropNewRow = true;
                    _dropRowIndex = i;
                    previewTop = rb.Start;
                    handled = true;
                    break;
                }
                if (client.Y < rb.Start + rb.Extent)
                {
                    _dropNewRow = false;
                    _dropRowIndex = i;
                    previewTop = rb.Start;
                    previewHeight = rb.Extent;
                    handled = true;
                    break;
                }
            }

            if (!handled)
            {
                _dropNewRow = true;
                _dropRowIndex = _lineBands.Count;
                previewTop = _lineBands.Count > 0 ? _lineBands[^1].Start + _lineBands[^1].Extent : _menuExtent;
            }

            int insertX = Math.Max(1, client.X - (dragSize.Width / 2));
            _dropOffset = insertX;

            rect = _dropNewRow
                ? new Rectangle(1, previewTop - (thickness / 2), dragSize.Width, thickness)
                : new Rectangle(insertX, previewTop, dragSize.Width, previewHeight);
        }
        else
        {
            int previewLeft = 0;
            int previewWidth = dragSize.Width;
            bool handled = false;

            for (int i = 0; i < _lineBands.Count; i++)
            {
                var cb = _lineBands[i];
                if (client.X < cb.Start + edge)
                {
                    _dropNewRow = true;
                    _dropRowIndex = i;
                    previewLeft = cb.Start;
                    handled = true;
                    break;
                }
                if (client.X < cb.Start + cb.Extent)
                {
                    _dropNewRow = false;
                    _dropRowIndex = i;
                    previewLeft = cb.Start;
                    previewWidth = cb.Extent;
                    handled = true;
                    break;
                }
            }

            if (!handled)
            {
                _dropNewRow = true;
                _dropRowIndex = _lineBands.Count;
                previewLeft = _lineBands.Count > 0 ? _lineBands[^1].Start + _lineBands[^1].Extent : 0;
            }

            int insertY = Math.Max(1, client.Y - (dragSize.Height / 2));
            _dropOffset = insertY;

            rect = _dropNewRow
                ? new Rectangle(previewLeft - (thickness / 2), 1, thickness, dragSize.Height)
                : new Rectangle(previewLeft, insertY, previewWidth, dragSize.Height);
        }
        return RectangleToScreen(rect);
    }

    private void ApplyDrop(CommandBar bar, bool newRow, int rowIndex, int offset)
    {
        if (_manager is null)
            return;

        // Current lines on this edge, excluding the dragged bar.
        var lines = new List<List<CommandBar>>();
        var bars = new List<CommandBar>();
        foreach (var b in _manager.Bars)
            if (!ReferenceEquals(b, bar) && b.Dock == EdgeState && b.Visible && b.BarType != CommandBarType.MenuBar)
                bars.Add(b);
        foreach (var group in bars.GroupBy(b => b.Row).OrderBy(g => g.Key))
            lines.Add(group.OrderBy(b => b.Offset).ToList());

        if (newRow)
        {
            int index = Math.Clamp(rowIndex, 0, lines.Count);
            lines.Insert(index, new List<CommandBar> { bar });
        }
        else if (lines.Count == 0)
        {
            lines.Add(new List<CommandBar> { bar });
        }
        else
        {
            int index = Math.Clamp(rowIndex, 0, lines.Count - 1);
            var line = lines[index];
            int pos = 0;
            foreach (var b in line)
            {
                if (b.Offset < offset)
                    pos++;
                else
                    break;
            }
            line.Insert(pos, bar);
        }

        // Renumber lines and positions.
        for (int r = 0; r < lines.Count; r++)
            for (int c = 0; c < lines[r].Count; c++)
            {
                lines[r][c].Row = r;
                lines[r][c].Offset = c;
            }

        bar.Dock = EdgeState;
        _manager.RefreshLayout();
    }

    private void SetPreview(Rectangle screenRect)
    {
        if (screenRect == _preview)
            return;
        _preview = screenRect;

        if (screenRect == Rectangle.Empty)
        {
            _previewWindow?.HidePreview();
            return;
        }

        _previewWindow ??= new DropPreviewWindow { Owner = FindForm() };
        _previewWindow.BackColor = _renderer.Colors.DropPreview;
        _previewWindow.ShowAt(screenRect);
    }

    // --- Overrides ---------------------------------------------------------

    protected override void OnHandleCreated(EventArgs e)
    {
        base.OnHandleCreated(e);
        if (_manager is not null)
            _manager.RegisterHost(this);
        SyncFloatingWindows();
    }

    protected override void OnDpiChangedAfterParent(EventArgs e)
    {
        base.OnDpiChangedAfterParent(e);
        LayoutBars();
        Invalidate();
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        // Keep the design preview in sync with edits to the manager's
        // BarDefinitions (which arrive through the property grid, not the
        // runtime LayoutChanged event). Defer the actual control rebuild so we
        // never resize/reparent mid-paint.
        if (DesignMode && _manager is not null)
        {
            _manager.EnsureDesignBars();
            if (_appliedDesignSig != _manager.DesignSig && IsHandleCreated)
            {
                _appliedDesignSig = _manager.DesignSig;
                try { BeginInvoke((MethodInvoker)Rebuild); } catch { /* designer teardown */ }
            }
        }

        base.OnPaint(e);
        var orientation = Horizontal ? BarOrientation.Horizontal : BarOrientation.Vertical;
        _renderer.DrawBand(e.Graphics, ClientRectangle, orientation);
        DrawEdgeSeparator(e.Graphics);

        if (DesignMode && _controls.Count == 0)
            DrawDesignHint(e.Graphics);
    }

    // A 1px raised line on the content-facing edge of the band, so the rebar
    // reads as raised against the client area whichever edge it docks to.
    private void DrawEdgeSeparator(Graphics g)
    {
        using var pen = new Pen(_renderer.Colors.RaisedBorder);
        int right = Width - 1, bottom = Height - 1;
        switch (_edge)
        {
            case DockEdge.Top: g.DrawLine(pen, 0, bottom, right, bottom); break;
            case DockEdge.Bottom: g.DrawLine(pen, 0, 0, right, 0); break;
            case DockEdge.Left: g.DrawLine(pen, right, 0, right, bottom); break;
            case DockEdge.Right: g.DrawLine(pen, 0, 0, 0, bottom); break;
        }
    }

    private void DrawDesignHint(Graphics g)
    {
        using var pen = new Pen(Color.FromArgb(120, 130, 150)) { DashStyle = DashStyle.Dash };
        g.DrawRectangle(pen, 0, 0, Width - 1, Height - 1);
        TextRenderer.DrawText(g, "CommandBars DockHost — set Manager and add BarDefinitions to preview",
            Font, ClientRectangle, Color.FromArgb(80, 92, 112),
            TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis);
    }

    protected override void OnSizeChanged(EventArgs e)
    {
        base.OnSizeChanged(e);
        LayoutBars();
    }

    protected override bool ProcessMnemonic(char charCode)
    {
        // Menu mnemonics only respond while Alt is held (see CommandBarControl),
        // so bare typing never opens a menu and steals focus.
        if ((ModifierKeys & Keys.Alt) != 0)
            foreach (var control in _controls)
                if (control.Stretch && control.TryMnemonic(charCode))
                    return true;
        return base.ProcessMnemonic(charCode);
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _manager?.UnregisterHost(this);
            _previewWindow?.Dispose();
            _previewWindow = null;
        }
        base.Dispose(disposing);
    }
}
