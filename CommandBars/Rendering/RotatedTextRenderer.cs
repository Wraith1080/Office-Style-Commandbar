using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Drawing.Text;

namespace CommandBars.Rendering;

internal static class RotatedTextRenderer
{
    // Use symmetric ClearType on native paint surfaces. Offscreen/translucent
    // drawing falls back to coverage sampled in both axes and grayscale alpha.
    internal static void Draw(Graphics graphics, string text, Font font, Rectangle bounds,
        Color color, bool bottomToTop, bool showKeyboardCues, bool nativePaintSurface = false, IntPtr nativeWindow = default)
    {
        if (string.IsNullOrEmpty(text) || bounds.Width <= 0 || bounds.Height <= 0)
            return;

        if (nativePaintSurface && DirectWriteRotatedTextRenderer.TryDraw(graphics, text, font, bounds,
            color, bottomToTop, showKeyboardCues, window: nativeWindow))
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

        // Reduce once before the quarter turn so both reading directions have
        // identical coverage. Pixel-center alignment keeps straight stems crisp.
        using var caption = new Bitmap(bounds.Height, bounds.Width, PixelFormat.Format32bppPArgb);
        caption.SetResolution(graphics.DpiX, graphics.DpiY);
        using (var g = Graphics.FromImage(caption))
        {
            g.InterpolationMode = InterpolationMode.HighQualityBicubic;
            g.PixelOffsetMode = PixelOffsetMode.Half;
            g.DrawImage(glyphs, new Rectangle(0, 0, caption.Width, caption.Height),
                0, 0, glyphs.Width, glyphs.Height, GraphicsUnit.Pixel);
        }
        caption.RotateFlip(bottomToTop ? RotateFlipType.Rotate270FlipNone : RotateFlipType.Rotate90FlipNone);
        graphics.DrawImageUnscaled(caption, bounds.Location);
    }
}
