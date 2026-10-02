using System.Drawing;
using System.Runtime.InteropServices;

namespace CommandBars.Rendering;

internal static partial class DirectWriteRotatedTextRenderer
{
    // The small native surface below uses the original Windows 7 dwrite.h ABI.
    // Slots include IUnknown and any inherited methods. Keep signatures/order
    // aligned with IDWriteFactory/TextFormat/TextLayout/BitmapRenderTarget and
    // IDWriteGdiInterop in the Windows SDK; no external COM wrapper is required.
    private static T Method<T>(IntPtr pointer, int slot) where T : Delegate =>
        Marshal.GetDelegateForFunctionPointer<T>(Marshal.ReadIntPtr(Marshal.ReadIntPtr(pointer), slot * IntPtr.Size));
    private static void Check(int result) => Marshal.ThrowExceptionForHR(result);

    [StructLayout(LayoutKind.Sequential)]
    public struct NativeMatrix(float a, float b, float c, float d, float x, float y)
    {
        public float A = a, B = b, C = c, D = d, X = x, Y = y;
        internal PointF Apply(float x, float y) => new(x * A + y * C + X, x * B + y * D + Y);
    }
    [StructLayout(LayoutKind.Sequential)]
    private struct NativeRange(uint start, uint length) { public uint Start = start, Length = length; }
    [StructLayout(LayoutKind.Sequential)]
    private struct NativeTrimming { public int Granularity; public uint Delimiter, DelimiterCount; }
    [StructLayout(LayoutKind.Sequential)]
    private struct NativeRect(int left, int top, int right, int bottom)
    { public int Left = left, Top = top, Right = right, Bottom = bottom; }
    [StructLayout(LayoutKind.Sequential)]
    private struct NativeUnderline
    {
        public float Width, Thickness, Offset, RunHeight;
        public int ReadingDirection, FlowDirection;
        public IntPtr Locale;
        public int MeasuringMode;
    }
    [StructLayout(LayoutKind.Sequential)]
    private struct NativeStrikethrough
    {
        public float Width, Thickness, Offset;
        public int ReadingDirection, FlowDirection;
        public IntPtr Locale;
        public int MeasuringMode;
    }

    // Includes IDWritePixelSnapping's three methods before IDWriteTextRenderer's
    // four callbacks. Native DirectWrite calls this managed COM implementation.
    [ComVisible(true), Guid("ef8a8135-5cc6-45fe-8825-c5a0724eb819"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    public interface ITextRenderer
    {
        [PreserveSig] int IsPixelSnappingDisabled(IntPtr context, out int disabled);
        [PreserveSig] int GetCurrentTransform(IntPtr context, out NativeMatrix matrix);
        [PreserveSig] int GetPixelsPerDip(IntPtr context, out float scale);
        [PreserveSig] int DrawGlyphRun(IntPtr context, float x, float y, int mode, IntPtr run, IntPtr description, IntPtr effect);
        [PreserveSig] int DrawUnderline(IntPtr context, float x, float y, IntPtr underline, IntPtr effect);
        [PreserveSig] int DrawStrikethrough(IntPtr context, float x, float y, IntPtr line, IntPtr effect);
        [PreserveSig] int DrawInlineObject(IntPtr context, float x, float y, IntPtr obj, int sideways, int rtl, IntPtr effect);
    }

    [ComVisible(true), Guid("eaf3a2da-ecf4-4d24-b644-b34f6842024b"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    public interface IPixelSnapping
    {
        [PreserveSig] int IsPixelSnappingDisabled(IntPtr context, out int disabled);
        [PreserveSig] int GetCurrentTransform(IntPtr context, out NativeMatrix matrix);
        [PreserveSig] int GetPixelsPerDip(IntPtr context, out float scale);
    }

    [UnmanagedFunctionPointer(CallingConvention.StdCall)] private delegate int CreateObject(IntPtr self, out IntPtr result);
    [UnmanagedFunctionPointer(CallingConvention.StdCall)] private delegate int CreateMonitorParams(IntPtr self, IntPtr monitor, out IntPtr result);
    [UnmanagedFunctionPointer(CallingConvention.StdCall)] private delegate int GetCollection(IntPtr self, out IntPtr result, int checkUpdates);
    [UnmanagedFunctionPointer(CallingConvention.StdCall, CharSet = CharSet.Unicode)] private delegate int FindFamily(IntPtr self, string name, out uint index, out int exists);
    [UnmanagedFunctionPointer(CallingConvention.StdCall)] private delegate float GetFloat(IntPtr self);
    [UnmanagedFunctionPointer(CallingConvention.StdCall)] private delegate int GetInt(IntPtr self);
    [UnmanagedFunctionPointer(CallingConvention.StdCall)] private delegate IntPtr GetPointer(IntPtr self);
    [UnmanagedFunctionPointer(CallingConvention.StdCall)] private delegate int CreateParams(IntPtr self, float gamma, float contrast, float level, int geometry, int mode, out IntPtr result);
    [UnmanagedFunctionPointer(CallingConvention.StdCall)] private delegate int CreateTarget(IntPtr self, IntPtr dc, uint width, uint height, out IntPtr result);
    [UnmanagedFunctionPointer(CallingConvention.StdCall)] private delegate int SetFloat(IntPtr self, float value);
    [UnmanagedFunctionPointer(CallingConvention.StdCall)] private delegate int SetInt(IntPtr self, int value);
    [UnmanagedFunctionPointer(CallingConvention.StdCall)] private delegate int SetMatrix(IntPtr self, ref NativeMatrix matrix);
    [UnmanagedFunctionPointer(CallingConvention.StdCall)] private delegate int SetTrimming(IntPtr self, ref NativeTrimming options, IntPtr sign);
    [UnmanagedFunctionPointer(CallingConvention.StdCall)] private delegate int CreateEllipsis(IntPtr self, IntPtr format, out IntPtr sign);
    [UnmanagedFunctionPointer(CallingConvention.StdCall)] private delegate int SetDecoration(IntPtr self, int enabled, NativeRange range);
    [UnmanagedFunctionPointer(CallingConvention.StdCall, CharSet = CharSet.Unicode)] private delegate int CreateFormat(IntPtr self, string family, IntPtr collection, int weight, int style, int stretch, float size, string locale, out IntPtr result);
    [UnmanagedFunctionPointer(CallingConvention.StdCall, CharSet = CharSet.Unicode)] private delegate int CreateGdiLayout(IntPtr self, string text, uint length, IntPtr format, float width, float height, float pixelsPerDip, IntPtr transform, int gdiNatural, out IntPtr result);
    [UnmanagedFunctionPointer(CallingConvention.StdCall)] private delegate int DrawLayout(IntPtr self, IntPtr context, [MarshalAs(UnmanagedType.Interface)] ITextRenderer renderer, float x, float y);
    [UnmanagedFunctionPointer(CallingConvention.StdCall)] private delegate int DrawRun(IntPtr self, float x, float y, int mode, IntPtr run, IntPtr parameters, uint color, IntPtr bounds);
    [UnmanagedFunctionPointer(CallingConvention.StdCall)] private delegate int DrawInline(IntPtr self, IntPtr context, [MarshalAs(UnmanagedType.Interface)] ITextRenderer renderer, float x, float y, int sideways, int rtl, IntPtr effect);
    [DllImport("dwrite.dll")] private static extern int DWriteCreateFactory(int type, ref Guid iid, out IntPtr factory);
    [DllImport("gdi32.dll")] private static extern bool BitBlt(IntPtr dst, int x, int y, int width, int height, IntPtr src, int sx, int sy, uint operation);
    [DllImport("gdi32.dll")] private static extern IntPtr GetCurrentObject(IntPtr dc, int type);
    [DllImport("gdi32.dll")] private static extern IntPtr CreateCompatibleDC(IntPtr dc);
    [DllImport("gdi32.dll")] private static extern IntPtr SelectObject(IntPtr dc, IntPtr obj);
    [DllImport("gdi32.dll")] private static extern bool DeleteDC(IntPtr dc);
    [DllImport("gdi32.dll")] private static extern bool DeleteObject(IntPtr obj);
    [DllImport("gdi32.dll")] private static extern IntPtr CreateSolidBrush(uint color);
    [DllImport("user32.dll")] private static extern int FillRect(IntPtr dc, ref NativeRect rect, IntPtr brush);
    [DllImport("user32.dll")] private static extern IntPtr MonitorFromWindow(IntPtr window, uint flags);
}
