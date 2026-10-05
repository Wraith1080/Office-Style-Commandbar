using System.Drawing;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;

namespace CommandBars.Rendering;

/// <summary>
/// A dark (charcoal) palette in the spirit of the Visual Studio dark theme:
/// dark gray bars and menus, light text, and a blue accent for hover/pressed.
/// </summary>
public sealed class DarkColorTable : CommandBarColorTable
{
    private static Color C(int r, int g, int b) => Color.FromArgb(r, g, b);

    // Charcoal toolbar chunk
    public override Color BarGradientBegin => C(69, 69, 73);
    public override Color BarGradientMiddle => C(62, 62, 66);
    public override Color BarGradientEnd => C(51, 51, 55);
    public override Color BarBorder => C(85, 85, 90);

    // Menu bar
    public override Color MenuBarGradientBegin => C(45, 45, 48);
    public override Color MenuBarGradientEnd => C(45, 45, 48);

    // Band (rebar)
    public override Color BandGradientBegin => C(55, 55, 58);
    public override Color BandGradientEnd => C(45, 45, 48);
    public override Color RaisedBorder => C(80, 80, 85);

    // Chevron nub
    public override Color ChevronGradientBegin => C(60, 60, 64);
    public override Color ChevronGradientEnd => C(45, 45, 48);

    // Drop preview overlay — accent blue
    public override Color DropPreview => C(0, 122, 204);

    // Hot (hover) — subtle lift with a blue accent border
    public override Color ButtonHotBegin => C(62, 62, 66);
    public override Color ButtonHotEnd => C(72, 72, 78);
    public override Color ButtonHotBorder => C(0, 122, 204);

    // Pressed — accent blue
    public override Color ButtonPressedBegin => C(0, 84, 153);
    public override Color ButtonPressedEnd => C(0, 122, 204);
    public override Color ButtonPressedBorder => C(0, 122, 204);

    // Checked (latched) — muted blue
    public override Color ButtonCheckedBegin => C(38, 79, 120);
    public override Color ButtonCheckedEnd => C(45, 95, 140);
    public override Color ButtonCheckedBorder => C(0, 122, 204);

    public override Color SeparatorDark => C(34, 34, 37);
    public override Color SeparatorLight => C(80, 80, 85);
    public override Color GripperDark => C(90, 90, 96);
    public override Color GripperLight => C(60, 60, 64);

    public override Color Text => C(241, 241, 241);
    public override Color DisabledText => C(127, 127, 127);

    public override Color MenuBackground => C(45, 45, 48);
    public override Color MenuBorder => C(63, 63, 70);
    public override Color ImageMarginBegin => C(55, 55, 58);
    public override Color ImageMarginEnd => C(45, 45, 48);
    public override Color MenuItemSelectedBegin => C(62, 62, 66);
    public override Color MenuItemSelectedEnd => C(72, 72, 78);
    public override Color MenuItemSelectedBorder => C(0, 122, 204);
    public override Color MenuText => C(241, 241, 241);
    public override Color DisabledMenuText => C(127, 127, 127);
}

/// <summary>
/// The dark look: the 2003 renderer's drawing with the charcoal color table and
/// square corners. All chrome colors come from the table, so text and checks
/// stay light-on-dark automatically.
/// </summary>
public sealed class DarkRenderer : Office2003Renderer
{
    protected override bool HasToolbarBorder => false;
    public override CommandBarColorTable Colors { get; } = new DarkColorTable();

    protected override int ChunkRadius => 0;

    public override void DrawItemImage(Graphics g, Image image, Rectangle bounds, RenderState state)
    {
        if (bounds.Width <= 0 || bounds.Height <= 0)
            return;

        if ((state & RenderState.Disabled) == 0)
            DrawIconOutline(g, image, bounds);

        base.DrawItemImage(g, image, bounds, state);
    }

    private void DrawIconOutline(Graphics g, Image image, Rectangle bounds)
    {
        // Work at the final pixel size for both raster and rasterized SVG sources.
        // Padding retains the contour when artwork touches the image's edge.
        float radius = Math.Max(1f, Scale);
        int padding = (int)Math.Ceiling(radius);
        using var outline = new Bitmap(bounds.Width + 2 * padding,
            bounds.Height + 2 * padding, PixelFormat.Format32bppArgb);
        using (var maskGraphics = Graphics.FromImage(outline))
            maskGraphics.DrawImage(image, new Rectangle(padding, padding, bounds.Width, bounds.Height));

        var area = new Rectangle(Point.Empty, outline.Size);
        var data = outline.LockBits(area, ImageLockMode.ReadWrite, PixelFormat.Format32bppArgb);
        try
        {
            int stride = data.Stride;
            var pixels = new byte[stride * outline.Height];
            Marshal.Copy(data.Scan0, pixels, 0, pixels.Length);
            var alpha = new byte[outline.Width * outline.Height];
            for (int y = 0; y < outline.Height; y++)
                for (int x = 0; x < outline.Width; x++)
                    alpha[y * outline.Width + x] = pixels[y * stride + x * 4 + 3];

            // Opaque rectangular images have no silhouette to enhance.
            bool hasTransparency = false;
            for (int y = padding; y < padding + bounds.Height && !hasTransparency; y++)
                for (int x = padding; x < padding + bounds.Width; x++)
                    if (alpha[y * outline.Width + x] < 255)
                    {
                        hasTransparency = true;
                        break;
                    }
            if (!hasTransparency)
                return;

            // Max coverage avoids the bright seams produced by stacking shifted
            // copies. Subtract the source alpha so translucent interiors stay intact.
            var offsets = new List<(int X, int Y, float Coverage)>();
            for (int dy = -padding; dy <= padding; dy++)
                for (int dx = -padding; dx <= padding; dx++)
                {
                    float coverage = Math.Clamp(radius + 1f - MathF.Sqrt(dx * dx + dy * dy), 0f, 1f);
                    if (coverage > 0f)
                        offsets.Add((dx, dy, coverage));
                }

            Color color = Colors.Text;
            for (int y = 0; y < outline.Height; y++)
                for (int x = 0; x < outline.Width; x++)
                {
                    int originalAlpha = alpha[y * outline.Width + x];
                    float expandedAlpha = originalAlpha;
                    if (originalAlpha < 255)
                        foreach (var offset in offsets)
                        {
                            int sx = x + offset.X;
                            int sy = y + offset.Y;
                            if (sx >= 0 && sx < outline.Width && sy >= 0 && sy < outline.Height)
                                expandedAlpha = Math.Max(expandedAlpha,
                                    alpha[sy * outline.Width + sx] * offset.Coverage);
                        }

                    int index = y * stride + x * 4;
                    pixels[index] = color.B;
                    pixels[index + 1] = color.G;
                    pixels[index + 2] = color.R;
                    pixels[index + 3] = (byte)Math.Round((expandedAlpha - originalAlpha) * 0.56f);
                }
            Marshal.Copy(pixels, 0, data.Scan0, pixels.Length);
        }
        finally
        {
            outline.UnlockBits(data);
        }

        g.DrawImageUnscaled(outline, bounds.X - padding, bounds.Y - padding);
    }
}
