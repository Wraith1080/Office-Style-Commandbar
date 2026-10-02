using System.Drawing;
using System.Globalization;
using System.Runtime.InteropServices;
using System.Text;

namespace CommandBars.Rendering;

/// <summary>Renders quarter-turned captions with ClearType and antialiasing in both screen axes.</summary>
internal static partial class DirectWriteRotatedTextRenderer
{
    // DirectWrite's GDI bitmap target rasterizes the transformed glyphs at the
    // final device resolution. Its background must be the already painted bar;
    // ClearType coverage cannot be composited as a single grayscale alpha mask.
    internal static bool TryDraw(Graphics graphics, string text, Font font, Rectangle bounds,
        Color color, bool bottomToTop, bool showKeyboardCues, Bitmap? background = null, IntPtr window = default)
    {
        if (bounds.Width <= 0 || bounds.Height <= 0 || string.IsNullOrEmpty(text))
            return true;
        using var world = graphics.Transform;
        var elements = world.Elements;
        // Scaling a finished ClearType bitmap would also scale its RGB stripes.
        // Other transforms and translucent text use the grayscale fallback.
        if (color.A != 255 || elements[0] != 1 || elements[1] != 0 ||
            elements[2] != 0 || elements[3] != 1 ||
            elements[4] != MathF.Round(elements[4]) || elements[5] != MathF.Round(elements[5]))
            return false;

        IntPtr factory = IntPtr.Zero, collection = IntPtr.Zero, defaults = IntPtr.Zero,
            parameters = IntPtr.Zero, interop = IntPtr.Zero, target = IntPtr.Zero,
            format = IntPtr.Zero, ellipsis = IntPtr.Zero, layout = IntPtr.Zero;
        try
        {
            var iid = new Guid("b859ee5a-d838-4b5b-a2e8-1adc7d93db48");
            Check(DWriteCreateFactory(0, ref iid, out factory));
            Check(Method<GetCollection>(factory, 3)(factory, out collection, 0));
            Check(Method<FindFamily>(collection, 5)(collection, font.FontFamily.Name, out _, out int exists));
            if (exists == 0) return false; // Preserve private/GDI-only fonts through the fallback.

            if (window == IntPtr.Zero)
                Check(Method<CreateObject>(factory, 10)(factory, out defaults));
            else
                Check(Method<CreateMonitorParams>(factory, 11)(factory, MonitorFromWindow(window, 2), out defaults));
            // Keep the system's gamma, contrast and RGB/BGR pixel geometry.
            // NATURAL_SYMMETRIC adds the missing vertical-axis smoothing.
            Check(Method<CreateParams>(factory, 12)(factory,
                Method<GetFloat>(defaults, 3)(defaults), Method<GetFloat>(defaults, 4)(defaults),
                Method<GetFloat>(defaults, 5)(defaults), Method<GetInt>(defaults, 6)(defaults),
                5, out parameters));
            Check(Method<CreateObject>(factory, 17)(factory, out interop));
            Check(Method<CreateTarget>(interop, 7)(interop, IntPtr.Zero,
                (uint)bounds.Width, (uint)bounds.Height, out target));
            Check(Method<SetFloat>(target, 6)(target, 1));
            var transform = bottomToTop
                ? new NativeMatrix(0, -1, 1, 0, 0, bounds.Height)
                : new NativeMatrix(0, 1, -1, 0, bounds.Width, 0);
            Check(Method<SetMatrix>(target, 8)(target, ref transform));
            var dc = Method<GetPointer>(target, 4)(target);
            var sourceBounds = bounds;
            sourceBounds.Offset((int)elements[4], (int)elements[5]);
            if (!CopyBackground(graphics, background, dc, sourceBounds)) return false;

            float pixels = FontPixels(font, graphics.DpiY);
            Check(Method<CreateFormat>(factory, 15)(factory, font.FontFamily.Name, collection,
                font.Bold ? 700 : 400, font.Italic ? 2 : 0, 5, pixels,
                CultureInfo.CurrentUICulture.Name, out format));
            Check(Method<SetInt>(format, 3)(format, 2)); // centered text
            Check(Method<SetInt>(format, 4)(format, 2)); // centered paragraph
            Check(Method<SetInt>(format, 5)(format, 1)); // no wrapping
            Check(Method<CreateEllipsis>(factory, 20)(factory, format, out ellipsis));
            var trimming = new NativeTrimming { Granularity = 1 };
            Check(Method<SetTrimming>(format, 9)(format, ref trimming, ellipsis));

            var caption = RemovePrefixes(text, out var mnemonics);
            // Match the GDI measurements used by bar layout and horizontal text,
            // while keeping DirectWrite's symmetric ClearType rasterization.
            Check(Method<CreateGdiLayout>(factory, 19)(factory, caption, (uint)caption.Length,
                format, bounds.Height, bounds.Width, 1, IntPtr.Zero, 0, out layout));
            var entireText = new NativeRange(0, (uint)caption.Length);
            if (font.Underline)
                Check(Method<SetDecoration>(layout, 36)(layout, 1, entireText));
            else if (showKeyboardCues)
                foreach (var range in mnemonics)
                    Check(Method<SetDecoration>(layout, 36)(layout, 1, range));
            if (font.Strikeout)
                Check(Method<SetDecoration>(layout, 37)(layout, 1, entireText));

            var renderer = new GlyphRenderer(target, parameters, transform, color);
            Check(Method<DrawLayout>(layout, 58)(layout, IntPtr.Zero, renderer, 0, 0));
            GC.KeepAlive(renderer);
            using var result = Image.FromHbitmap(GetCurrentObject(dc, 7));
            // Copy final device pixels; never rotate or resize ClearType output.
            graphics.DrawImageUnscaled(result, bounds.Location);
            return true;
        }
        catch (Exception ex) when (ex is ExternalException or DllNotFoundException or EntryPointNotFoundException)
        {
            return false;
        }
        finally
        {
            foreach (var pointer in new[] { layout, ellipsis, format, target, interop, parameters, defaults, collection, factory })
                if (pointer != IntPtr.Zero) Marshal.Release(pointer);
        }
    }

    private static bool CopyBackground(Graphics graphics, Bitmap? background, IntPtr destination, Rectangle sourceBounds)
    {
        IntPtr dc = IntPtr.Zero, bitmap = IntPtr.Zero, previous = IntPtr.Zero;
        try
        {
            if (background is null) dc = graphics.GetHdc();
            else
            {
                // Graphics.FromImage exposes a temporary write-only GDI surface.
                // Tests/offscreen callers can supply their actual backing bitmap.
                graphics.Flush();
                bitmap = background.GetHbitmap();
                dc = CreateCompatibleDC(IntPtr.Zero);
                if (dc == IntPtr.Zero) return false;
                previous = SelectObject(dc, bitmap);
                if (previous == IntPtr.Zero || previous == new IntPtr(-1)) return false;
            }
            return BitBlt(destination, 0, 0, sourceBounds.Width, sourceBounds.Height,
                dc, sourceBounds.X, sourceBounds.Y, 0x00cc0020);
        }
        finally
        {
            if (background is null)
            {
                if (dc != IntPtr.Zero) graphics.ReleaseHdc(dc);
            }
            else
            {
                if (previous != IntPtr.Zero && previous != new IntPtr(-1)) SelectObject(dc, previous);
                if (dc != IntPtr.Zero) DeleteDC(dc);
                if (bitmap != IntPtr.Zero) DeleteObject(bitmap);
            }
        }
    }

    private static float FontPixels(Font font, float dpi) => font.Unit switch
    {
        GraphicsUnit.Pixel or GraphicsUnit.World => font.Size,
        GraphicsUnit.Point => font.Size * dpi / 72f,
        GraphicsUnit.Inch => font.Size * dpi,
        GraphicsUnit.Millimeter => font.Size * dpi / 25.4f,
        GraphicsUnit.Document => font.Size * dpi / 300f,
        GraphicsUnit.Display => font.Size * dpi / 75f,
        _ => font.SizeInPoints * dpi / 72f,
    };

    private static string RemovePrefixes(string text, out List<NativeRange> mnemonics)
    {
        var caption = new StringBuilder(text.Length);
        mnemonics = new List<NativeRange>();
        for (int i = 0; i < text.Length; i++)
        {
            if (text[i] != '&') { caption.Append(text[i]); continue; }
            if (++i == text.Length) break;
            if (text[i] != '&')
                mnemonics.Add(new NativeRange((uint)caption.Length,
                    char.IsHighSurrogate(text[i]) && i + 1 < text.Length && char.IsLowSurrogate(text[i + 1]) ? 2u : 1u));
            caption.Append(text[i]);
        }
        return caption.ToString();
    }

    [ComVisible(true), ClassInterface(ClassInterfaceType.None)]
    private sealed class GlyphRenderer(IntPtr target, IntPtr parameters, NativeMatrix transform, Color color) : ITextRenderer, IPixelSnapping
    {
        private readonly uint _color = (uint)(color.R | color.G << 8 | color.B << 16);
        public int IsPixelSnappingDisabled(IntPtr context, out int disabled) { disabled = 0; return 0; }
        public int GetCurrentTransform(IntPtr context, out NativeMatrix matrix) { matrix = transform; return 0; }
        public int GetPixelsPerDip(IntPtr context, out float scale) { scale = 1; return 0; }
        public int DrawGlyphRun(IntPtr context, float x, float y, int mode, IntPtr run, IntPtr description, IntPtr effect) =>
            Method<DrawRun>(target, 3)(target, x, y, mode, run, parameters, _color, IntPtr.Zero);
        public int DrawInlineObject(IntPtr context, float x, float y, IntPtr obj, int sideways, int rtl, IntPtr effect) =>
            Method<DrawInline>(obj, 3)(obj, context, this, x, y, sideways, rtl, effect);
        public int DrawUnderline(IntPtr context, float x, float y, IntPtr underline, IntPtr effect)
        {
            var line = Marshal.PtrToStructure<NativeUnderline>(underline);
            return DrawLine(x, y, line.Width, line.Thickness, line.Offset, line.ReadingDirection);
        }
        public int DrawStrikethrough(IntPtr context, float x, float y, IntPtr strikethrough, IntPtr effect)
        {
            var line = Marshal.PtrToStructure<NativeStrikethrough>(strikethrough);
            return DrawLine(x, y, line.Width, line.Thickness, line.Offset, line.ReadingDirection);
        }
        private int DrawLine(float x, float y, float width, float thickness, float offset, int direction)
        {
            if (direction == 1) x -= width;
            var a = transform.Apply(x, y + offset);
            var b = transform.Apply(x + width, y + offset + thickness);
            var rect = new NativeRect((int)MathF.Floor(MathF.Min(a.X, b.X)), (int)MathF.Floor(MathF.Min(a.Y, b.Y)),
                (int)MathF.Ceiling(MathF.Max(a.X, b.X)), (int)MathF.Ceiling(MathF.Max(a.Y, b.Y)));
            var brush = CreateSolidBrush(_color);
            if (brush == IntPtr.Zero) return unchecked((int)0x8007000e);
            try { return FillRect(Method<GetPointer>(target, 4)(target), ref rect, brush) != 0 ? 0 : unchecked((int)0x80004005); }
            finally { DeleteObject(brush); }
        }
    }
}
