using System.Drawing;
using System.Drawing.Drawing2D;
using System.Runtime.InteropServices;
using CommandBars.Controls;
using CommandBars.Model;
using CommandBars.Rendering;
using Xunit;

namespace CommandBars.Tests;

[Collection("DirectWrite rendering")]
public class DirectWriteRotatedTextTests
{
    [Theory]
    [InlineData(1f, true)]
    [InlineData(1.25f, true)]
    [InlineData(1.5f, true)]
    [InlineData(1.75f, true)]
    [InlineData(2f, true)]
    [InlineData(1f, false)]
    [InlineData(1.25f, false)]
    [InlineData(1.5f, false)]
    [InlineData(1.75f, false)]
    [InlineData(2f, false)]
    public void ViewUsesSubpixelEdgesAndDarkStemsAtDeviceDpi(float scale, bool left)
    {
        using var font = new Font("Segoe UI", 9);
        using var bitmap = new Bitmap((int)(26 * scale), (int)(54 * scale));
        bitmap.SetResolution(96 * scale, 96 * scale);
        using (var g = Graphics.FromImage(bitmap))
        {
            g.Clear(Color.White);
            Assert.True(DirectWriteRotatedTextRenderer.TryDraw(g, "&View", font,
                new Rectangle(Point.Empty, bitmap.Size), Color.Black, left, false, bitmap));
        }
        int dark = 0, subpixel = 0, smooth = 0;
        for (int y = 0; y < bitmap.Height; y++)
        for (int x = 0; x < bitmap.Width; x++)
        {
            var pixel = bitmap.GetPixel(x, y);
            if ((pixel.R + pixel.G + pixel.B) / 3 < 64) dark++;
            if (pixel.R != pixel.G || pixel.G != pixel.B) subpixel++;
            if (pixel.G > 32 && pixel.G < 224) smooth++;
        }
        Assert.True(dark > 10, $"Dark stem coverage: {dark}");
        Assert.True(subpixel > 10, $"Subpixel coverage: {subpixel}");
        Assert.True(smooth > 10, $"Intermediate coverage: {smooth}");
    }

    [Fact]
    public void MnemonicsAndEllipsisAreDrawnByTheNativeLayout()
    {
        using var plain = Paint("View");
        using var hidden = Paint("&View");
        using var shown = Paint("&View", cues: true);
        using var escaped = Paint("&&");
        using var prefix = Paint("&");
        using var longCaption = Paint("A very long menu caption", height: 24);
        using var ellipsis = Paint("A…", height: 24);
        int difference = 0, ampersand = 0;
        for (int y = 0; y < plain.Height; y++)
        for (int x = 0; x < plain.Width; x++)
        {
            Assert.Equal(plain.GetPixel(x, y), hidden.GetPixel(x, y));
            if (plain.GetPixel(x, y) != shown.GetPixel(x, y)) difference++;
            if (escaped.GetPixel(x, y).ToArgb() != Color.White.ToArgb()) ampersand++;
            Assert.Equal(Color.White.ToArgb(), prefix.GetPixel(x, y).ToArgb());
        }
        Assert.True(difference > 10);
        Assert.True(ampersand > 10);
        Assert.Equal(Pixels(ellipsis), Pixels(longCaption));
    }

    [Fact]
    public void BackgroundClipAndTranslationArePreserved()
    {
        using var bitmap = new Bitmap(100, 140);
        using var original = new Bitmap(100, 140);
        using var font = new Font("Segoe UI", 14);
        foreach (var surface in new[] { bitmap, original })
        using (var g = Graphics.FromImage(surface))
        using (var gradient = new LinearGradientBrush(new Rectangle(Point.Empty, surface.Size), Color.LightBlue, Color.White, 90f))
            g.FillRectangle(gradient, new Rectangle(Point.Empty, surface.Size));
        using (var g = Graphics.FromImage(bitmap))
        {
            g.TranslateTransform(3, 5);
            var clip = new Rectangle(8, 13, 20, 80);
            g.SetClip(new Rectangle(5, 8, 20, 80));
            using var before = g.Transform;
            var oldClip = g.ClipBounds;
            Assert.True(DirectWriteRotatedTextRenderer.TryDraw(g, "&View", font,
                new Rectangle(0, 0, 40, 100), Color.Black, true, true, bitmap));
            using var after = g.Transform;
            Assert.Equal(before.Elements, after.Elements);
            Assert.Equal(oldClip, g.ClipBounds);
            for (int y = 0; y < bitmap.Height; y++)
            for (int x = 0; x < bitmap.Width; x++)
                if (!clip.Contains(x, y))
                    Assert.Equal(original.GetPixel(x, y), bitmap.GetPixel(x, y));
        }
    }

    [Fact]
    public void ScaledGraphicsUseTheFallbackWithoutModifyingTheSurface()
    {
        using var bitmap = new Bitmap(50, 100);
        using var font = new Font("Segoe UI", 9);
        using var g = Graphics.FromImage(bitmap);
        g.Clear(Color.White);
        g.ScaleTransform(2, 2);
        Assert.False(DirectWriteRotatedTextRenderer.TryDraw(g, "View", font,
            new Rectangle(0, 0, 20, 40), Color.Black, true, false, bitmap));
        Assert.All(Pixels(bitmap), pixel => Assert.Equal(Color.White.ToArgb(), pixel));
    }

    [Theory]
    [InlineData(DockState.Left)]
    [InlineData(DockState.Right)]
    public void ControlPaintUsesNativeTextOverTheActualBarBackground(DockState dock)
    {
        using var manager = new CommandBarManager { RotateVerticalMenuCaptions = true };
        var bar = manager.AddBar("menu", CommandBarType.MenuBar);
        bar.Dock = dock;
        var item = bar.Items.AddPopup("&View");
        using var control = new CommandBarControl { Bar = bar, Renderer = new NeutralRenderer(), Size = new Size(40, 100) };
        using var bitmap = new Bitmap(control.Width, control.Height);
        control.DrawToBitmap(bitmap, new Rectangle(Point.Empty, bitmap.Size));
        int coloredEdges = 0;
        for (int y = item.Bounds.Top; y < item.Bounds.Bottom; y++)
        for (int x = item.Bounds.Left; x < item.Bounds.Right; x++)
        {
            var pixel = bitmap.GetPixel(x, y);
            // The default renderer's gray background has equal RGB channels.
            if (pixel.R != pixel.G || pixel.G != pixel.B) coloredEdges++;
        }
        Assert.True(coloredEdges > 10);
    }

    [Fact]
    public void RepeatedPaintReleasesItsNativeGdiResources()
    {
        using var warmup = Paint("View");
        uint before = GetGuiResources(new IntPtr(-1), 0);
        Assert.True(before > 0);
        for (int i = 0; i < 80; i++)
        {
            using var caption = Paint("&View", cues: true);
        }
        uint after = GetGuiResources(new IntPtr(-1), 0);
        Assert.InRange((long)after - before, -2, 2);
    }

    [Theory]
    [InlineData(FontStyle.Regular)]
    [InlineData(FontStyle.Bold)]
    [InlineData(FontStyle.Italic)]
    [InlineData(FontStyle.Underline)]
    [InlineData(FontStyle.Strikeout)]
    public void PointAndPixelFontsRetainEquivalentSizingAndStyles(FontStyle style)
    {
        using var pointFont = new Font("Segoe UI", 18, style, GraphicsUnit.Point);
        using var pixelFont = new Font("Segoe UI", 24, style, GraphicsUnit.Pixel);
        using var points = new Bitmap(40, 140);
        using var pixels = new Bitmap(40, 140);
        foreach (var pair in new[] { (points, pointFont), (pixels, pixelFont) })
        using (var g = Graphics.FromImage(pair.Item1))
        {
            g.Clear(Color.White);
            Assert.True(DirectWriteRotatedTextRenderer.TryDraw(g, "View", pair.Item2,
                new Rectangle(Point.Empty, pair.Item1.Size), Color.Black, true, false, pair.Item1));
        }
        Assert.Equal(Pixels(points), Pixels(pixels));
    }

    private static Bitmap Paint(string text, bool cues = false, int height = 140)
    {
        var bitmap = new Bitmap(40, height);
        using var font = new Font("Segoe UI", 18);
        using var g = Graphics.FromImage(bitmap);
        g.Clear(Color.White);
        Assert.True(DirectWriteRotatedTextRenderer.TryDraw(g, text, font,
            new Rectangle(Point.Empty, bitmap.Size), Color.Black, true, cues, bitmap));
        return bitmap;
    }

    private static int[] Pixels(Bitmap bitmap) => Enumerable.Range(0, bitmap.Width * bitmap.Height)
        .Select(i => bitmap.GetPixel(i % bitmap.Width, i / bitmap.Width).ToArgb()).ToArray();
    private sealed class NeutralRenderer : Office2003Renderer
    {
        public override CommandBarColorTable Colors { get; } = new NeutralColors();
    }
    private sealed class NeutralColors : CommandBarColorTable
    {
        public override Color BarGradientBegin => Color.White;
        public override Color Text => Color.Black;
    }
    [DllImport("user32.dll")] private static extern uint GetGuiResources(IntPtr process, uint flags);
}

// GetGuiResources measures the whole process; keep its audit isolated from
// other tests creating/discarding WinForms controls at the same time.
[CollectionDefinition("DirectWrite rendering", DisableParallelization = true)]
public class DirectWriteRenderingCollection;
