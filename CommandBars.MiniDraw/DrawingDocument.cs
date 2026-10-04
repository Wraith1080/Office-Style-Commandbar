using System.Text.Json;
using System.Text.Json.Serialization;

namespace CommandBars.MiniDraw;

public enum ShapeKind { Rectangle, Ellipse, Line }

// Endpoints preserve the direction of a line, including a bottom-left to top-right drag.
public sealed record DrawingShape(Guid Id, ShapeKind Kind, float X1, float Y1, float X2, float Y2,
    int FillArgb, int StrokeArgb, float StrokeWidth)
{
    [JsonIgnore]
    public RectangleF Bounds => RectangleF.FromLTRB(Math.Min(X1, X2), Math.Min(Y1, Y2),
        Math.Max(X1, X2), Math.Max(Y1, Y2));

    public DrawingShape Offset(float dx, float dy) => this with
    { X1 = X1 + dx, Y1 = Y1 + dy, X2 = X2 + dx, Y2 = Y2 + dy };

    public bool HitTest(PointF point, float tolerance)
    {
        var bounds = Bounds;
        float margin = tolerance + StrokeWidth / 2;
        if (Kind == ShapeKind.Line)
        {
            float dx = X2 - X1, dy = Y2 - Y1;
            float lengthSquared = dx * dx + dy * dy;
            float t = lengthSquared == 0 ? 0 : Math.Clamp(((point.X - X1) * dx + (point.Y - Y1) * dy) / lengthSquared, 0, 1);
            float ex = point.X - X1 - t * dx, ey = point.Y - Y1 - t * dy;
            return ex * ex + ey * ey <= margin * margin;
        }
        if (Kind == ShapeKind.Ellipse)
        {
            float rx = bounds.Width / 2, ry = bounds.Height / 2;
            float dx = point.X - bounds.X - rx, dy = point.Y - bounds.Y - ry;
            float outer = dx * dx / ((rx + margin) * (rx + margin)) + dy * dy / ((ry + margin) * (ry + margin));
            if (outer > 1) return false;
            if (Color.FromArgb(FillArgb).A != 0 || rx <= margin || ry <= margin) return true;
            return dx * dx / ((rx - margin) * (rx - margin)) + dy * dy / ((ry - margin) * (ry - margin)) >= 1;
        }
        var outerBounds = RectangleF.Inflate(bounds, margin, margin);
        if (!outerBounds.Contains(point)) return false;
        return Color.FromArgb(FillArgb).A != 0 || !RectangleF.Inflate(bounds, -margin, -margin).Contains(point);
    }
}

public sealed class DrawingFile
{
    public int Version { get; set; } = 1;
    public int Width { get; set; } = DrawingDocument.PageWidth;
    public int Height { get; set; } = DrawingDocument.PageHeight;
    public List<DrawingShape> Shapes { get; set; } = new();
}

/// <summary>Application document/history, independent of the commandbar and canvas.</summary>
public sealed class DrawingDocument
{
    public const int PageWidth = 960, PageHeight = 640, MaximumShapes = 5000;
    private const int HistoryLimit = 100;
    private static readonly JsonSerializerOptions JsonOptions = new()
    { WriteIndented = true, Converters = { new JsonStringEnumConverter() } };
    private sealed record Revision(DrawingShape[] Shapes, string Action);
    private readonly List<Revision> _history = new() { new(Array.Empty<DrawingShape>(), "") };
    private DrawingShape[] _saved = Array.Empty<DrawingShape>();
    private int _position;
    public Guid? SelectedId { get; private set; }
    public IReadOnlyList<DrawingShape> Shapes => Array.AsReadOnly(_history[_position].Shapes);
    public DrawingShape? Selected => Shapes.FirstOrDefault(shape => shape.Id == SelectedId);
    public bool CanUndo => _position > 0;
    public bool CanRedo => _position + 1 < _history.Count;
    public string UndoAction => CanUndo ? _history[_position].Action : "";
    public string RedoAction => CanRedo ? _history[_position + 1].Action : "";
    public bool IsDirty => !_saved.SequenceEqual(_history[_position].Shapes);
    public event EventHandler? Changed;

    public void Select(Guid? id)
    {
        SelectedId = Shapes.Any(shape => shape.Id == id) ? id : null;
        Changed?.Invoke(this, EventArgs.Empty);
    }

    public void Add(DrawingShape shape)
        => AddShape(shape, "Draw " + shape.Kind.ToString().ToLowerInvariant());

    private void AddShape(DrawingShape shape, string action)
    {
        ValidateShape(shape);
        if (Shapes.Count >= MaximumShapes) throw new InvalidDataException($"A drawing can contain at most {MaximumShapes} shapes.");
        if (Shapes.Any(existing => existing.Id == shape.Id)) throw new InvalidDataException("Shape ids must be unique.");
        SelectedId = shape.Id;
        Commit(Shapes.Append(shape), action);
    }

    public void ReplaceSelected(DrawingShape replacement, string action)
    {
        if (Selected is not { } selected) return;
        if (replacement.Id != selected.Id) throw new ArgumentException("Replacement must keep the selected shape id.");
        ValidateShape(replacement);
        Commit(Shapes.Select(shape => shape.Id == SelectedId ? replacement : shape), action);
    }

    public DrawingShape ClampMove(DrawingShape shape, float dx, float dy)
    {
        var bounds = shape.Bounds;
        return shape.Offset(Math.Clamp(dx, -bounds.Left, PageWidth - bounds.Right),
            Math.Clamp(dy, -bounds.Top, PageHeight - bounds.Bottom));
    }

    public void Duplicate()
    {
        if (Selected is { } shape) AddShape(ClampMove(shape, 20, 20) with { Id = Guid.NewGuid() }, "Duplicate");
    }

    public void Delete()
    {
        if (Selected is null) return;
        Commit(Shapes.Where(shape => shape.Id != SelectedId), "Delete");
    }

    public bool CanReorder(int delta) => Selected is { } selected &&
        Shapes.ToList().IndexOf(selected) + delta >= 0 && Shapes.ToList().IndexOf(selected) + delta < Shapes.Count;

    public void Reorder(int delta)
    {
        if (!CanReorder(delta)) return;
        var list = Shapes.ToList();
        int index = list.IndexOf(Selected!);
        (list[index], list[index + delta]) = (list[index + delta], list[index]);
        Commit(list, delta > 0 ? "Bring forward" : "Send backward");
    }

    private void Commit(IEnumerable<DrawingShape> shapes, string action)
    {
        var next = shapes.ToArray();
        if (next.SequenceEqual(_history[_position].Shapes)) return;
        _history.RemoveRange(_position + 1, _history.Count - _position - 1);
        _history.Add(new Revision(next, action));
        if (_history.Count > HistoryLimit + 1) _history.RemoveAt(0);
        _position = _history.Count - 1;
        Notify();
    }

    public void Undo() { if (CanUndo) { _position--; Notify(); } }
    public void Redo() { if (CanRedo) { _position++; Notify(); } }

    private void Notify()
    {
        if (!Shapes.Any(shape => shape.Id == SelectedId)) SelectedId = null;
        Changed?.Invoke(this, EventArgs.Empty);
    }

    public void New()
    {
        Reset(Array.Empty<DrawingShape>());
    }

    private void Reset(DrawingShape[] shapes)
    {
        _history.Clear();
        _history.Add(new Revision(shapes, ""));
        _position = 0;
        _saved = shapes;
        SelectedId = null;
        Notify();
    }

    public void Save(string path)
    {
        var data = new DrawingFile { Shapes = Shapes.ToList() };
        // A failed write must leave both the existing document and saved-state marker intact.
        string temporary = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
        try
        {
            File.WriteAllText(temporary, JsonSerializer.Serialize(data, JsonOptions));
            File.Move(temporary, path, true);
        }
        finally { if (File.Exists(temporary)) File.Delete(temporary); }
        _saved = _history[_position].Shapes;
        Notify();
    }

    public void Load(string path)
    {
        if (new FileInfo(path).Length > 8 * 1024 * 1024) throw new InvalidDataException("This drawing exceeds the 8 MB file limit.");
        using var stream = File.OpenRead(path);
        var data = JsonSerializer.Deserialize<DrawingFile>(stream, JsonOptions)
            ?? throw new InvalidDataException("The drawing is empty.");
        if (data.Version != 1) throw new InvalidDataException("This drawing uses an unsupported file version.");
        if (data.Width != PageWidth || data.Height != PageHeight) throw new InvalidDataException("MiniDraw supports 960 by 640 drawings.");
        if (data.Shapes is null || data.Shapes.Count > MaximumShapes) throw new InvalidDataException("Invalid shape list.");
        var ids = new HashSet<Guid>();
        foreach (var shape in data.Shapes)
        {
            ValidateShape(shape);
            if (!ids.Add(shape.Id)) throw new InvalidDataException("The drawing has duplicate shape ids.");
        }
        // Validate the entire file before changing the current document/history.
        Reset(data.Shapes.ToArray());
    }

    private static void ValidateShape(DrawingShape? shape)
    {
        if (shape is null || shape.Id == Guid.Empty || !Enum.IsDefined(typeof(ShapeKind), shape.Kind))
            throw new InvalidDataException("Invalid shape identity or kind.");
        if (!float.IsFinite(shape.X1) || !float.IsFinite(shape.X2) || !float.IsFinite(shape.Y1) || !float.IsFinite(shape.Y2) ||
            !float.IsFinite(shape.StrokeWidth) || shape.StrokeWidth < 1 || shape.StrokeWidth > 16 ||
            shape.Bounds.Left < 0 || shape.Bounds.Top < 0 || shape.Bounds.Right > PageWidth || shape.Bounds.Bottom > PageHeight)
            throw new InvalidDataException("Shape coordinates or line width are outside the supported range.");
        if (shape.Kind == ShapeKind.Line ? shape.X1 == shape.X2 && shape.Y1 == shape.Y2 : shape.Bounds.Width < 1 || shape.Bounds.Height < 1)
            throw new InvalidDataException("Shapes must have a nonzero size.");
        if (Color.FromArgb(shape.StrokeArgb).A != 255 || Color.FromArgb(shape.FillArgb).A is not (0 or 255))
            throw new InvalidDataException("Use opaque outline colors and opaque or empty fills.");
    }
}
