using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Text;
using CommandBars.Rendering;
using Xunit;

namespace CommandBars.Tests;

public class RotatedTextRenderingTests
{
    [Theory]
    [InlineData(1f)]
    [InlineData(1.25f)]
    [InlineData(1.5f)]
    [InlineData(1.75f)]
    [InlineData(2f)]
    public void ViewHasSmoothNeutralEdgesAndDarkStemsInBothDirections(float scale)
    {
        using var font = new Font("Segoe UI", 9 * scale);
        var size = new Size((int)(26 * scale), (int)(54 * scale));
        using var left = new Bitmap(size.Width, size.Height);
        using var right = new Bitmap(size.Width, size.Height);
        foreach (bool bottomToTop in new[] { true, false })
        {
            var bitmap = bottomToTop ? left : right;
            using (var g = Graphics.FromImage(bitmap))
            {
                g.Clear(Color.White);
                RotatedTextRenderer.Draw(g, "&View", font, new Rectangle(Point.Empty, size),
                    Color.Black, bottomToTop, false);
            }

            int dark = 0, smooth = 0;
            for (int y = 0; y < size.Height; y++)
            for (int x = 0; x < size.Width; x++)
            {
                var pixel = bitmap.GetPixel(x, y);
                Assert.Equal(pixel.R, pixel.G);
                Assert.Equal(pixel.G, pixel.B);
                if (pixel.R < 32) dark++;
                if (pixel.R > 32 && pixel.R < 224) smooth++;
            }
            Assert.True(dark > 10, "Stems must retain dark interiors.");
            Assert.True(smooth > 10, "Diagonals must retain intermediate coverage.");
        }

        // Reversing the reading direction must not change the glyph coverage.
        for (int y = 0; y < size.Height; y++)
        for (int x = 0; x < size.Width; x++)
            Assert.Equal(left.GetPixel(x, y), right.GetPixel(size.Width - x - 1, size.Height - y - 1));
    }

    [Fact]
    public void CaptionRespectsClipAndRestoresGraphicsSettings()
    {
        using var bitmap = new Bitmap(90, 130);
        using var g = Graphics.FromImage(bitmap);
        using var font = new Font("Segoe UI", 14);
        g.Clear(Color.Magenta);
        g.TranslateTransform(3, 5);
        g.SetClip(new Rectangle(5, 8, 20, 80));
        g.InterpolationMode = InterpolationMode.NearestNeighbor;
        g.PixelOffsetMode = PixelOffsetMode.None;
        g.TextRenderingHint = TextRenderingHint.SingleBitPerPixel;
        using var transform = g.Transform;
        var clip = g.ClipBounds;
        RotatedTextRenderer.Draw(g, "&View", font, new Rectangle(0, 0, 40, 100), Color.White, true, true);
        Assert.Equal(transform.Elements, g.Transform.Elements);
        Assert.Equal(clip, g.ClipBounds);
        Assert.Equal(InterpolationMode.NearestNeighbor, g.InterpolationMode);
        Assert.Equal(PixelOffsetMode.None, g.PixelOffsetMode);
        Assert.Equal(TextRenderingHint.SingleBitPerPixel, g.TextRenderingHint);
        var deviceClip = new Rectangle(8, 13, 20, 80);
        for (int y = 0; y < bitmap.Height; y++)
        for (int x = 0; x < bitmap.Width; x++)
            if (!deviceClip.Contains(x, y))
                Assert.Equal(Color.Magenta.ToArgb(), bitmap.GetPixel(x, y).ToArgb());
    }
}
