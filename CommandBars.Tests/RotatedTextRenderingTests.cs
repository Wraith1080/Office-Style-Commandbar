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
        using var font = new Font("Segoe UI", 9);
        var size = new Size((int)(26 * scale), (int)(54 * scale));
        using var left = new Bitmap(size.Width, size.Height);
        using var right = new Bitmap(size.Width, size.Height);
        left.SetResolution(96 * scale, 96 * scale);
        right.SetResolution(96 * scale, 96 * scale);
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
                // A thin 9pt stem can share coverage across adjacent pixels;
                // require at least 75% coverage rather than near-opaque black.
                if (pixel.R < 64) dark++;
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
    public void MnemonicCuesAddUnderlineAndEscapedAmpersandsRemainLiteral()
    {
        using var font = new Font("Segoe UI", 18);
        using var hidden = Paint("&View", false);
        using var plain = Paint("View", false);
        using var shown = Paint("&View", true);
        using var literal = Paint("&&", false);
        using var prefixOnly = Paint("&", false);
        int addedUnderlinePixels = 0;
        int literalPixels = 0;
        for (int y = 0; y < hidden.Height; y++)
        for (int x = 0; x < hidden.Width; x++)
        {
            Assert.Equal(plain.GetPixel(x, y), hidden.GetPixel(x, y));
            if (shown.GetPixel(x, y).A > hidden.GetPixel(x, y).A)
                addedUnderlinePixels++;
            Assert.Equal(0, prefixOnly.GetPixel(x, y).A);
            if (literal.GetPixel(x, y).A > 200)
                literalPixels++;
        }
        Assert.True(addedUnderlinePixels > 10);
        Assert.True(literalPixels > 10);

        Bitmap Paint(string text, bool cues)
        {
            var bitmap = new Bitmap(50, 180);
            using var g = Graphics.FromImage(bitmap);
            RotatedTextRenderer.Draw(g, text, font, new Rectangle(Point.Empty, bitmap.Size),
                Color.Black, true, cues);
            return bitmap;
        }
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
        using var restoredTransform = g.Transform;
        Assert.Equal(transform.Elements, restoredTransform.Elements);
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
