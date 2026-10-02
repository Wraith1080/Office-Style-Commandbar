using System.Runtime.InteropServices;
using System.Windows.Forms;
using CommandBars.Rendering;

namespace CommandBars.Controls;

internal static class PopupWindowChrome
{
    internal static void Apply(Form window, CommandBarRenderer renderer, bool floating = false)
    {
        int radius = floating ? renderer.FloatingCornerRadius : renderer.PopupCornerRadius;
        window.Region = floating ? renderer.CreateFloatingWindowRegion(window.ClientRectangle)
            : renderer.CreatePopupRegion(window.ClientRectangle);
        if (!OperatingSystem.IsWindowsVersionAtLeast(10, 0, 22000)) return;
        // An explicit theme region owns the window shape. Only ask DWM for its
        // rounded preset when the renderer deliberately leaves the region unset.
        // https://learn.microsoft.com/windows/apps/desktop/modernize/ui/apply-rounded-corners
        int preference = radius > 0 && window.Region is null ? 3 : 1; // ROUNDSMALL / DONOTROUND
        int result = DwmSetWindowAttribute(window.Handle, 33, ref preference, sizeof(int));
        if (result < 0 && radius > 0 && window.Region is null)
        {
            // A rejected compositor preference must not prevent the menu opening.
            window.Region = RoundedSurface.CreateRegion(window.ClientRectangle, radius * renderer.Scale);
        }
    }

    [DllImport("dwmapi.dll")]
    private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attribute, ref int value, int size);
}
