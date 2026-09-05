using System.Runtime.InteropServices;
using System.Windows.Forms;
using CommandBars.Rendering;

namespace CommandBars.Controls;

internal static class PopupWindowChrome
{
    internal static void Apply(Form window, CommandBarRenderer renderer)
    {
        if (!renderer.UsesFluentMenuChrome || !OperatingSystem.IsWindowsVersionAtLeast(10, 0, 22000)) return;
        // A window Region disables DWM antialiased corners. Ask the compositor
        // to round the actual popup rather than cutting a one-bit GDI region.
        // https://learn.microsoft.com/windows/apps/desktop/modernize/ui/apply-rounded-corners
        int preference = 3; // DWMWCP_ROUNDSMALL
        int result = DwmSetWindowAttribute(window.Handle, 33, ref preference, sizeof(int));
        if (result < 0)
        {
            // A rejected compositor preference must not prevent the menu opening.
            window.Region = RoundedSurface.CreateRegion(window.ClientRectangle, 4 * renderer.Scale);
        }
    }

    [DllImport("dwmapi.dll")]
    private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attribute, ref int value, int size);
}
