using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Drawing.Text;

namespace CommandBars.Rendering;

internal static class RotatedTextRenderer
{
    // Legacy ClearType smooths only one screen axis, leaving rotated diagonals
    // stepped. Sample coverage in both axes instead, then composite grayscale
    // alpha over the existing theme background without rotating RGB subpixels.
    internal static void Draw(Graphics graphics, string text, Font font, Rectangle bounds,
        Color color, bool bottomToTop, bool showKeyboardCues)
    {
        if (string.IsNullOrEmpty(text) || bounds.Width <= 0 || bounds.Height <= 0)
            return;

        const int samples = 4;
        using var glyphs = new Bitmap(bounds.Height * samples, bounds.Width * samples,
            PixelFormat.Format32bppPArgb);
        glyphs.SetResolution(graphics.DpiX, graphics.DpiY);
        using (var g = Graphics.FromImage(glyphs))
        using (var brush = new SolidBrush(color))
        using (var format = new StringFormat(StringFormatFlags.NoWrap)
        {
            Alignment = StringAlignment.Center,
            LineAlignment = StringAlignment.Center,
            Trimming = StringTrimming.EllipsisCharacter,
            HotkeyPrefix = showKeyboardCues ? HotkeyPrefix.Show : HotkeyPrefix.Hide,
        })
        {
            g.ScaleTransform(samples, samples);
            g.TextRenderingHint = TextRenderingHint.AntiAliasGridFit;
            g.DrawString(text, font, brush, new RectangleF(0, 0, bounds.Height, bounds.Width), format);
        }

        // Quarter turns only rearrange pixels. Reduce once, at the final size;
        // pixel-center alignment avoids an extra half-pixel blur on straight stems.
        glyphs.RotateFlip(bottomToTop ? RotateFlipType.Rotate270FlipNone : RotateFlipType.Rotate90FlipNone);
        var saved = graphics.Save();
        try
        {
            graphics.InterpolationMode = InterpolationMode.HighQualityBicubic;
            graphics.PixelOffsetMode = PixelOffsetMode.Half;
            graphics.DrawImage(glyphs, bounds, 0, 0, glyphs.Width, glyphs.Height, GraphicsUnit.Pixel);
        }
        finally
        {
            graphics.Restore(saved);
        }
    }
}
