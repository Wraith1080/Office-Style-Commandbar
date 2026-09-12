using System.Drawing;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;

namespace CommandBars.Rendering;

/// <summary>Symmetric pixel coverage for small rounded surfaces, independent of GDI+ arc rasterization.</summary>
internal static class RoundedSurface
{
    internal static Bitmap Create(int width, int height, float radius, Color fill, Color? border = null,
        int split = int.MaxValue, bool vertical = false, Color? trailingFill = null)
    {
        var bitmap = new Bitmap(width, height, PixelFormat.Format32bppArgb);
        var pixels = new int[width * height];
        radius = Math.Clamp(radius, 0, Math.Min(width, height) / 2f);
        int edgeWidth = Math.Min(width, (int)Math.Ceiling(radius) + 1);
        for (int y = 0; y < height; y++)
        {
            bool borderRow = border.HasValue && (y == 0 || y == height - 1);
            int leading = borderRow ? border!.Value.ToArgb() : fill.ToArgb();
            int trailing = borderRow ? leading : (trailingFill ?? fill).ToArgb();
            int splitX = vertical ? (y >= split ? 0 : width) : Math.Clamp(split, 0, width);
            Array.Fill(pixels, leading, y * width, splitX);
            Array.Fill(pixels, trailing, y * width + splitX, width - splitX);
            // Only the curved ends and side borders need coverage calculations.
            // Large menu interiors are solid scanlines, not per-pixel math.
            for (int x = 0; x < width; x++)
            {
                if (x >= edgeWidth && x < width - edgeWidth)
                {
                    x = width - edgeWidth - 1;
                    continue;
                }
                // Distances from pixel centers to the same mirrored corner circle.
                // Identical arithmetic in all quadrants guarantees matching corners.
                float dx = Math.Max(radius - Math.Min(x + 0.5f, width - x - 0.5f), 0);
                float dy = Math.Max(radius - Math.Min(y + 0.5f, height - y - 0.5f), 0);
                float distance = dx > 0 && dy > 0
                    ? MathF.Sqrt(dx * dx + dy * dy) - radius
                    : -Math.Min(Math.Min(x + 0.5f, width - x - 0.5f), Math.Min(y + 0.5f, height - y - 0.5f));
                float outer = Math.Clamp(0.5f - distance, 0, 1);
                if (outer <= 0)
                {
                    pixels[y * width + x] = 0;
                    continue;
                }
                Color color = (vertical ? y : x) >= split ? trailingFill ?? fill : fill;
                float inner = border.HasValue ? Math.Clamp(-0.5f - distance, 0, 1) : outer;
                Color edge = border ?? color;
                float fraction = inner / outer;
                int Blend(int a, int b) => (int)Math.Round(a * fraction + b * (1 - fraction));
                pixels[y * width + x] = Color.FromArgb((int)Math.Round(outer * 255),
                    Blend(color.R, edge.R), Blend(color.G, edge.G), Blend(color.B, edge.B)).ToArgb();
            }
        }
        var data = bitmap.LockBits(new Rectangle(0, 0, width, height), ImageLockMode.WriteOnly, PixelFormat.Format32bppArgb);
        try { Marshal.Copy(pixels, 0, data.Scan0, pixels.Length); }
        finally { bitmap.UnlockBits(data); }
        return bitmap;
    }

    internal static Region CreateRegion(Rectangle bounds, float radius)
    {
        var region = new Region();
        region.MakeEmpty();
        if (bounds.Width <= 0 || bounds.Height <= 0) return region;
        using var bitmap = Create(bounds.Width, bounds.Height, radius, Color.White);
        for (int y = 0; y < bounds.Height; y++)
        {
            int inset = 0;
            while (inset < bounds.Width / 2 && bitmap.GetPixel(inset, y).A < 128) inset++;
            region.Union(new Rectangle(bounds.X + inset, bounds.Y + y, bounds.Width - 2 * inset, 1));
        }
        return region;
    }

    internal static void Draw(Graphics g, Rectangle bounds, float radius, Color fill, Color? border = null,
        int split = int.MaxValue, bool vertical = false, Color? trailingFill = null)
    {
        if (bounds.Width <= 0 || bounds.Height <= 0) return;
        using var bitmap = Create(bounds.Width, bounds.Height, radius, fill, border, split, vertical, trailingFill);
        g.DrawImageUnscaled(bitmap, bounds.Location);
    }
}
