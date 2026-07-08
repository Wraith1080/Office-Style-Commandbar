using System.Drawing;
using CommandBars.Model;

namespace CommandBars.Controls;

/// <summary>
/// Which edge of the form a <see cref="DockHost"/> occupies. Top/Bottom bands
/// lay their bars out in horizontal rows; Left/Right bands lay them out in
/// vertical columns.
/// </summary>
public enum DockEdge
{
    Top,
    Left,
    Right,
    Bottom,
}

/// <summary>
/// A drag in progress, shared by every <see cref="DockHost"/> through the
/// <see cref="CommandBarManager"/> so a bar dragged off one band can be
/// previewed and dropped onto any other band (cross-edge docking).
/// </summary>
internal sealed class DockDragSession
{
    public DockDragSession(CommandBar bar, Size size, Point grab, DockHost origin)
    {
        Bar = bar;
        Size = size;
        Grab = grab;
        Origin = origin;
    }

    /// <summary>The bar being dragged.</summary>
    public CommandBar Bar { get; }

    /// <summary>The size the bar will occupy once docked (used for previews).</summary>
    public Size Size { get; }

    /// <summary>Cursor offset within the bar at grab time.</summary>
    public Point Grab { get; }

    /// <summary>The host that started the drag and owns the preview window.</summary>
    public DockHost Origin { get; }
}
