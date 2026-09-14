using System.Drawing;
using System.Drawing.Drawing2D;

namespace CommandBars.Rendering;

internal static class MdiFrameGeometry
{
    internal static GraphicsPath CreatePath(RectangleF bounds, float radius)
    {
        var path = new GraphicsPath();
        radius = Math.Max(0, Math.Min(radius, Math.Min(bounds.Width, bounds.Height) / 2));
        if (radius <= 0) path.AddRectangle(bounds);
        else
        {
            float diameter = radius * 2;
            path.AddArc(bounds.Left, bounds.Top, diameter, diameter, 180, 90);
            path.AddArc(bounds.Right - diameter, bounds.Top, diameter, diameter, 270, 90);
            path.AddArc(bounds.Right - diameter, bounds.Bottom - diameter, diameter, diameter, 0, 90);
            path.AddArc(bounds.Left, bounds.Bottom - diameter, diameter, diameter, 90, 90);
            path.CloseFigure();
        }
        return path;
    }
}
