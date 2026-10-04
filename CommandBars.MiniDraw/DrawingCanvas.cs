using System.Drawing.Drawing2D;

namespace CommandBars.MiniDraw;

internal enum DrawingTool { Select, Rectangle, Ellipse, Line }

internal class DrawingCanvas : ScrollableControl
{
    private readonly DrawingDocument _document;
    private PointF _start;
    private DrawingShape? _original, _preview;
    private bool _dragging;
    private DrawingTool _tool;
    private float _zoom = 1;
    private bool _updatingView;
    public bool FitPage { get; private set; } = true;
    public Color FillColor { get; set; } = Color.FromArgb(109, 176, 224);
    public Color OutlineColor { get; set; } = Color.FromArgb(42, 70, 98);
    public float LineWidth { get; set; } = 2;
    public bool ShowGrid { get; set; } = true;
    public bool IsDragging => _dragging;
    public event EventHandler? ToolChanged;
    public event EventHandler? InteractionChanged;
    public event EventHandler? ViewChanged;
    public event EventHandler<string>? OperationFailed;

    public DrawingTool Tool
    {
        get => _tool;
        set
        {
            CancelGesture();
            _tool = value;
            Cursor = value == DrawingTool.Select ? Cursors.Default : Cursors.Cross;
            ToolChanged?.Invoke(this, EventArgs.Empty);
        }
    }

    public float Zoom
    {
        get => _zoom;
        set { CancelGesture(); FitPage = false; _zoom = Math.Clamp(value, .25f, 2); UpdateExtent(); }
    }

    public void ZoomToFit()
    {
        CancelGesture();
        FitPage = true;
        UpdateExtent();
    }

    private float ViewScale => DeviceDpi / 96f * Zoom;
    private float MarginPixels => 28 * DeviceDpi / 96f;
    private PointF PageOrigin => new(
        Math.Max(MarginPixels, (ClientSize.Width - DrawingDocument.PageWidth * ViewScale) / 2) + AutoScrollPosition.X,
        Math.Max(MarginPixels, (ClientSize.Height - DrawingDocument.PageHeight * ViewScale) / 2) + AutoScrollPosition.Y);
    internal RectangleF PageBounds => new(PageOrigin.X, PageOrigin.Y,
        DrawingDocument.PageWidth * ViewScale, DrawingDocument.PageHeight * ViewScale);

    public DrawingCanvas(DrawingDocument document)
    {
        _document = document;
        SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer |
            ControlStyles.ResizeRedraw | ControlStyles.Selectable, true);
        Dock = DockStyle.Fill;
        TabStop = true;
        AutoScroll = true;
        BackColor = Color.FromArgb(226, 231, 237);
        AccessibleName = "Drawing canvas";
        AccessibleDescription = "Choose a shape tool and drag to draw. Use Select to move a shape. Arrow keys nudge; Escape cancels a drag.";
        _document.Changed += DocumentChanged;
        UpdateExtent();
    }

    private void DocumentChanged(object? sender, EventArgs e) => Invalidate();

    private void UpdateExtent()
    {
        if (_updatingView) return;
        _updatingView = true;
        try
        {
            if (FitPage)
            {
                // Remove scrollbars before measuring, so switching from a scrolled zoom
                // does not retain a smaller viewport or oscillate between scrollbar states.
                AutoScrollMinSize = Size.Empty;
                AutoScrollPosition = Point.Empty;
                float dpiScale = DeviceDpi / 96f;
                float width = Math.Max(1, ClientSize.Width - 2 * MarginPixels);
                float height = Math.Max(1, ClientSize.Height - 2 * MarginPixels);
                _zoom = Math.Min(1, Math.Min(width / (DrawingDocument.PageWidth * dpiScale),
                    height / (DrawingDocument.PageHeight * dpiScale)));
            }
            else
                AutoScrollMinSize = new Size((int)Math.Ceiling(DrawingDocument.PageWidth * ViewScale + 2 * MarginPixels),
                    (int)Math.Ceiling(DrawingDocument.PageHeight * ViewScale + 2 * MarginPixels));
        }
        finally { _updatingView = false; }
        Invalidate();
        ViewChanged?.Invoke(this, EventArgs.Empty);
    }

    protected override void OnClientSizeChanged(EventArgs e)
    {
        base.OnClientSizeChanged(e);
        if (_updatingView) return;
        CancelGesture();
        UpdateExtent();
    }

    protected override void OnDpiChangedAfterParent(EventArgs e)
    {
        base.OnDpiChangedAfterParent(e);
        CancelGesture();
        UpdateExtent();
    }

    protected override void OnScroll(ScrollEventArgs se) { base.OnScroll(se); Invalidate(); }

    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);
        var graphics = e.Graphics;
        var origin = PageOrigin;
        var page = PageBounds;
        using var shadow = new SolidBrush(Color.FromArgb(196, 204, 214));
        graphics.FillRectangle(shadow, page.X + 4, page.Y + 4, page.Width, page.Height);
        graphics.FillRectangle(Brushes.White, page);
        var state = graphics.Save();
        graphics.SetClip(page);
        graphics.TranslateTransform(origin.X, origin.Y);
        graphics.ScaleTransform(ViewScale, ViewScale);
        graphics.SmoothingMode = SmoothingMode.AntiAlias;
        if (ShowGrid && Zoom >= .5f)
        {
            using var dot = new SolidBrush(Color.FromArgb(222, 229, 237));
            float size = 1 / ViewScale;
            for (int x = 20; x < DrawingDocument.PageWidth; x += 20)
                for (int y = 20; y < DrawingDocument.PageHeight; y += 20)
                    graphics.FillRectangle(dot, x, y, size, size);
        }
        foreach (var shape in _document.Shapes)
            DrawingPainter.PaintShape(graphics, _original?.Id == shape.Id && _preview is not null ? _preview : shape);
        if (_preview is not null && _original is null) DrawingPainter.PaintShape(graphics, _preview);
        var selected = _preview ?? _document.Selected;
        if (selected is not null)
        {
            var bounds = RectangleF.Inflate(selected.Bounds, 4 / ViewScale, 4 / ViewScale);
            using var selection = new Pen(Color.FromArgb(48, 112, 203), 1 / ViewScale) { DashStyle = DashStyle.Dash };
            graphics.DrawRectangle(selection, bounds.X, bounds.Y, bounds.Width, bounds.Height);
        }
        graphics.Restore(state);
        using var border = new Pen(Color.FromArgb(183, 194, 207));
        graphics.DrawRectangle(border, page.X, page.Y, page.Width, page.Height);
    }

    private PointF DocumentPoint(Point point)
    {
        var origin = PageOrigin;
        return new PointF((point.X - origin.X) / ViewScale, (point.Y - origin.Y) / ViewScale);
    }

    private static PointF Clamp(PointF point) => new(Math.Clamp(point.X, 0, DrawingDocument.PageWidth),
        Math.Clamp(point.Y, 0, DrawingDocument.PageHeight));

    protected override void OnMouseDown(MouseEventArgs e)
    {
        base.OnMouseDown(e);
        if (e.Button != MouseButtons.Left) return;
        Focus();
        var point = DocumentPoint(e.Location);
        if (!new RectangleF(0, 0, DrawingDocument.PageWidth, DrawingDocument.PageHeight).Contains(point)) return;
        _start = point;
        if (Tool == DrawingTool.Select)
        {
            var hit = _document.Shapes.LastOrDefault(shape => shape.HitTest(point, 5 / ViewScale));
            _document.Select(hit?.Id);
            _original = hit;
            if (hit is null) return;
        }
        else _document.Select(null);
        _dragging = true;
        Capture = true;
        InteractionChanged?.Invoke(this, EventArgs.Empty);
    }

    protected override void OnMouseMove(MouseEventArgs e)
    {
        base.OnMouseMove(e);
        if (!_dragging) return;
        var end = Clamp(DocumentPoint(e.Location));
        if (_original is not null)
            _preview = _document.ClampMove(_original, end.X - _start.X, end.Y - _start.Y);
        else
        {
            var kind = Tool switch { DrawingTool.Rectangle => ShapeKind.Rectangle, DrawingTool.Ellipse => ShapeKind.Ellipse, _ => ShapeKind.Line };
            _preview = new DrawingShape(_preview?.Id ?? Guid.NewGuid(), kind, _start.X, _start.Y, end.X, end.Y,
                FillColor.ToArgb(), OutlineColor.ToArgb(), LineWidth);
        }
        Invalidate();
    }

    protected override void OnMouseUp(MouseEventArgs e)
    {
        base.OnMouseUp(e);
        if (e.Button != MouseButtons.Left || !_dragging) return;
        OnMouseMove(e);
        var result = _preview;
        bool move = _original is not null;
        CancelGesture();
        if (result is null) return;
        try
        {
            if (move) _document.ReplaceSelected(result, "Move");
            else if (result.Kind == ShapeKind.Line ?
                Math.Abs(result.X2 - result.X1) + Math.Abs(result.Y2 - result.Y1) >= 3 :
                result.Bounds.Width >= 3 && result.Bounds.Height >= 3)
                _document.Add(result);
        }
        catch (InvalidDataException exception) { OperationFailed?.Invoke(this, exception.Message); }
    }

    public void CancelGesture()
    {
        bool wasDragging = _dragging;
        _dragging = false;
        _preview = _original = null;
        Capture = false;
        Invalidate();
        if (wasDragging) InteractionChanged?.Invoke(this, EventArgs.Empty);
    }

    protected override void OnMouseCaptureChanged(EventArgs e)
    {
        base.OnMouseCaptureChanged(e);
        if (!Capture && _dragging) CancelGesture();
    }

    protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
    {
        if (keyData == Keys.Escape)
        {
            if (_dragging) CancelGesture();
            else { Tool = DrawingTool.Select; _document.Select(null); }
            return true;
        }
        if (_dragging) return base.ProcessCmdKey(ref msg, keyData);
        var key = keyData & Keys.KeyCode;
        if ((keyData & Keys.Modifiers) is Keys.None or Keys.Shift &&
            key is Keys.Left or Keys.Right or Keys.Up or Keys.Down && _document.Selected is { } selected)
        {
            int step = (keyData & Keys.Shift) != 0 ? 10 : 1;
            _document.ReplaceSelected(_document.ClampMove(selected,
                key == Keys.Left ? -step : key == Keys.Right ? step : 0,
                key == Keys.Up ? -step : key == Keys.Down ? step : 0), "Nudge");
            return true;
        }
        if (keyData is Keys.V or Keys.R or Keys.E or Keys.L)
        {
            Tool = keyData switch { Keys.R => DrawingTool.Rectangle, Keys.E => DrawingTool.Ellipse, Keys.L => DrawingTool.Line, _ => DrawingTool.Select };
            return true;
        }
        return base.ProcessCmdKey(ref msg, keyData);
    }

    // Smoke checks exercise the same mouse handlers in document coordinates at the active DPI/zoom.
    internal void Pointer(MouseButtons button, PointF point, string phase)
    {
        var origin = PageOrigin;
        var args = new MouseEventArgs(button, 1, (int)(origin.X + point.X * ViewScale), (int)(origin.Y + point.Y * ViewScale), 0);
        if (phase == "down") OnMouseDown(args);
        else if (phase == "move") OnMouseMove(args);
        else OnMouseUp(args);
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing) _document.Changed -= DocumentChanged;
        base.Dispose(disposing);
    }
}
