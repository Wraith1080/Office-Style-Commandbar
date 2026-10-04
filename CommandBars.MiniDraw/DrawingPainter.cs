using System.Drawing.Drawing2D;
using System.Drawing.Imaging;

namespace CommandBars.MiniDraw;

internal static class DrawingPainter
{
    public static void PaintShape(Graphics graphics, DrawingShape shape)
    {
        using var fill = new SolidBrush(Color.FromArgb(shape.FillArgb));
        using var outline = new Pen(Color.FromArgb(shape.StrokeArgb), shape.StrokeWidth)
        { LineJoin = LineJoin.Round, StartCap = LineCap.Round, EndCap = LineCap.Round };
        var bounds = shape.Bounds;
        switch (shape.Kind)
        {
            case ShapeKind.Rectangle:
                graphics.FillRectangle(fill, bounds);
                graphics.DrawRectangle(outline, bounds.X, bounds.Y, bounds.Width, bounds.Height);
                break;
            case ShapeKind.Ellipse:
                graphics.FillEllipse(fill, bounds);
                graphics.DrawEllipse(outline, bounds);
                break;
            case ShapeKind.Line:
                graphics.DrawLine(outline, shape.X1, shape.Y1, shape.X2, shape.Y2);
                break;
        }
    }

    public static void ExportPng(DrawingDocument document, string path)
    {
        using var bitmap = new Bitmap(DrawingDocument.PageWidth, DrawingDocument.PageHeight);
        bitmap.SetResolution(96, 96);
        using (var graphics = Graphics.FromImage(bitmap))
        {
            graphics.Clear(Color.White);
            graphics.SmoothingMode = SmoothingMode.AntiAlias;
            foreach (var shape in document.Shapes) PaintShape(graphics, shape);
        }
        string temporary = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
        try
        {
            bitmap.Save(temporary, ImageFormat.Png);
            File.Move(temporary, path, true);
        }
        finally { if (File.Exists(temporary)) File.Delete(temporary); }
    }
}
