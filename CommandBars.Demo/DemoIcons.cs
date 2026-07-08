using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;
using CommandBars.Imaging;

namespace CommandBars.Demo;

/// <summary>
/// Simple hand-drawn 32px icons so the demo has recognizable buttons without a
/// vector pipeline (that lands in phase 4). Each returns an
/// <see cref="IImageSource"/> the command bar can rasterize at any size.
/// </summary>
internal static class DemoIcons
{
    private const int Size = 32;

    public static IImageSource New() => Make("new", g =>
    {
        Point[] page = { new(8, 4), new(20, 4), new(24, 8), new(24, 28), new(8, 28) };
        using var fill = new SolidBrush(Color.White);
        using var pen = new Pen(Color.FromArgb(90, 110, 140), 1.4f);
        g.FillPolygon(fill, page);
        g.DrawPolygon(pen, page);
        g.DrawLines(pen, new[] { new Point(20, 4), new Point(20, 8), new Point(24, 8) });
        using var line = new Pen(Color.FromArgb(150, 165, 190));
        for (int i = 0; i < 3; i++)
            g.DrawLine(line, 11, 12 + (i * 4), 21, 12 + (i * 4));
    });

    public static IImageSource Open() => Make("open", g =>
    {
        using var back = new SolidBrush(Color.FromArgb(228, 186, 74));
        using var front = new SolidBrush(Color.FromArgb(255, 214, 110));
        using var pen = new Pen(Color.FromArgb(150, 120, 40));
        Point[] body = { new(5, 11), new(14, 11), new(17, 14), new(27, 14), new(27, 26), new(5, 26) };
        g.FillPolygon(back, body);
        Point[] flap = { new(7, 16), new(29, 16), new(26, 27), new(4, 27) };
        g.FillPolygon(front, flap);
        g.DrawPolygon(pen, body);
    });

    public static IImageSource Save() => Make("save", g =>
    {
        var body = new Rectangle(6, 6, 20, 20);
        using var blue = new SolidBrush(Color.FromArgb(66, 122, 190));
        using var white = new SolidBrush(Color.White);
        using var pen = new Pen(Color.FromArgb(40, 80, 130));
        g.FillRectangle(blue, body);
        g.DrawRectangle(pen, body);
        g.FillRectangle(white, new Rectangle(11, 6, 10, 7));     // label
        g.FillRectangle(new SolidBrush(Color.FromArgb(40, 80, 130)), new Rectangle(17, 7, 3, 5)); // notch
        g.FillRectangle(white, new Rectangle(9, 16, 14, 10));    // shutter
    });

    public static IImageSource Cut() => Make("cut", g =>
    {
        g.SmoothingMode = SmoothingMode.AntiAlias;
        using var pen = new Pen(Color.FromArgb(80, 90, 105), 2f);
        g.DrawLine(pen, 10, 10, 24, 22);
        g.DrawLine(pen, 24, 10, 10, 22);
        using var handle = new Pen(Color.FromArgb(80, 90, 105), 2f);
        g.DrawEllipse(handle, 6, 6, 6, 6);
        g.DrawEllipse(handle, 6, 20, 6, 6);
    });

    public static IImageSource Copy() => Make("copy", g =>
    {
        using var fill = new SolidBrush(Color.White);
        using var pen = new Pen(Color.FromArgb(90, 110, 140), 1.3f);
        var back = new Rectangle(7, 6, 13, 16);
        var front = new Rectangle(12, 11, 13, 16);
        g.FillRectangle(fill, back);
        g.DrawRectangle(pen, back);
        g.FillRectangle(fill, front);
        g.DrawRectangle(pen, front);
    });

    public static IImageSource Paste() => Make("paste", g =>
    {
        using var board = new SolidBrush(Color.FromArgb(170, 140, 95));
        using var clip = new SolidBrush(Color.FromArgb(120, 120, 120));
        using var white = new SolidBrush(Color.White);
        using var pen = new Pen(Color.FromArgb(90, 75, 50));
        var boardRect = new Rectangle(6, 7, 20, 21);
        g.FillRectangle(board, boardRect);
        g.DrawRectangle(pen, boardRect);
        g.FillRectangle(white, new Rectangle(9, 12, 14, 14));
        g.FillRectangle(clip, new Rectangle(12, 4, 8, 6));
    });

    public static IImageSource Bold() => Letter("bold", "B", FontStyle.Bold);
    public static IImageSource Italic() => Letter("italic", "I", FontStyle.Italic | FontStyle.Bold);
    public static IImageSource Underline() => Letter("underline", "U", FontStyle.Underline | FontStyle.Bold);

    private static IImageSource Letter(string key, string glyph, FontStyle style) => Make(key, g =>
    {
        using var big = new Font("Segoe UI", 20f, style, GraphicsUnit.Pixel);
        TextRenderer.DrawText(g, glyph, big, new Rectangle(0, 0, Size, Size),
            Color.FromArgb(45, 55, 70),
            TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPadding);
    });

    private static BitmapImageSource Make(string key, Action<Graphics> draw)
    {
        var bmp = new Bitmap(Size, Size);
        using (var g = Graphics.FromImage(bmp))
        {
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.Clear(Color.Transparent);
            draw(g);
        }
        return new BitmapImageSource(bmp, key);
    }
}
