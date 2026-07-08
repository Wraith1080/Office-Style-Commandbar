using System.Drawing;
using System.Windows.Forms;

namespace CommandBars.Controls;

/// <summary>
/// A translucent, click-through overlay that shows where a dragged toolbar will
/// dock. Being a real (layered) window rather than an XOR frame, it does not
/// flicker and survives repaints of whatever is behind it.
/// </summary>
internal sealed class DropPreviewWindow : Form
{
    private const int WS_EX_NOACTIVATE = 0x08000000;
    private const int WS_EX_TOOLWINDOW = 0x00000080;
    private const int WS_EX_TRANSPARENT = 0x00000020;

    public DropPreviewWindow()
    {
        FormBorderStyle = FormBorderStyle.None;
        ShowInTaskbar = false;
        StartPosition = FormStartPosition.Manual;
        TopMost = true;
        BackColor = Color.FromArgb(51, 133, 224);
        Opacity = 0.35;
    }

    protected override bool ShowWithoutActivation => true;

    protected override CreateParams CreateParams
    {
        get
        {
            var cp = base.CreateParams;
            // Non-activating, no taskbar, and click-through so it never steals
            // the drag's mouse capture.
            cp.ExStyle |= WS_EX_NOACTIVATE | WS_EX_TOOLWINDOW | WS_EX_TRANSPARENT;
            return cp;
        }
    }

    /// <summary>Positions the overlay at a screen rectangle and shows it.</summary>
    public void ShowAt(Rectangle screenRect)
    {
        Bounds = screenRect;
        if (!Visible)
            Show();
    }

    /// <summary>Hides the overlay.</summary>
    public void HidePreview()
    {
        if (Visible)
            Hide();
    }
}
