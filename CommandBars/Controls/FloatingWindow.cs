using System.ComponentModel;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;
using CommandBars.Model;
using CommandBars.Rendering;

namespace CommandBars.Controls;

/// <summary>
/// A non-activating themed mini-frame that hosts an undocked toolbar. The owner
/// form keeps focus. Drag the caption to move it; double-click the caption, hit
/// the close button, or drop it back on the dock band to re-dock.
/// </summary>
[ToolboxItem(false)]
[DesignerCategory("")]
public sealed class FloatingWindow : Form
{
    private const int WS_EX_NOACTIVATE = 0x08000000;
    private const int WS_EX_TOOLWINDOW = 0x00000080;
    private const int WM_MOUSEACTIVATE = 0x0021;
    private const int MA_NOACTIVATE = 3;

    private readonly CommandBar _bar;
    private readonly DockHost _host;
    private readonly CommandBarControl _control;

    private int _captionHeight;
    private int _border;
    private Rectangle _closeRect;
    private bool _closeHot;
    private bool _closePressed;
    private bool _dragging;
    private Point _dragOffset;

    public FloatingWindow(CommandBar bar, CommandBarRenderer renderer, DockHost host, Form? owner)
    {
        _bar = bar ?? throw new ArgumentNullException(nameof(bar));
        _host = host ?? throw new ArgumentNullException(nameof(host));

        FormBorderStyle = FormBorderStyle.None;
        ShowInTaskbar = false;
        StartPosition = FormStartPosition.Manual;
        DoubleBuffered = true;
        SetStyle(ControlStyles.ResizeRedraw, true);
        if (owner is not null)
            Owner = owner;

        _control = new CommandBarControl { Renderer = renderer };
        Controls.Add(_control);
        _control.Bar = bar;

        Relayout();
    }

    /// <summary>The hosted bar control.</summary>
    public CommandBarControl BarControl => _control;

    /// <summary>Re-themes and re-lays out the floating bar.</summary>
    public void SetRenderer(CommandBarRenderer renderer)
    {
        _control.Renderer = renderer;
        Relayout();
        Invalidate();
    }

    protected override bool ShowWithoutActivation => true;

    protected override CreateParams CreateParams
    {
        get
        {
            var cp = base.CreateParams;
            cp.ExStyle |= WS_EX_NOACTIVATE | WS_EX_TOOLWINDOW;
            return cp;
        }
    }

    protected override void WndProc(ref Message m)
    {
        if (m.Msg == WM_MOUSEACTIVATE)
        {
            m.Result = (IntPtr)MA_NOACTIVATE;
            return;
        }
        base.WndProc(ref m);
    }

    /// <summary>Sizes the frame to the caption plus the hosted bar content.</summary>
    public void Relayout()
    {
        float scale = DeviceDpi / 96f;
        _border = Math.Max(1, (int)Math.Round(3 * scale));
        _captionHeight = Font.Height + (int)Math.Round(6 * scale);

        _control.Relayout();
        _control.Location = new Point(_border, _border + _captionHeight);
        _control.Width = _control.PreferredContentWidth;

        int width = _control.Width + (2 * _border);
        int height = _control.Height + _captionHeight + (2 * _border);
        ClientSize = new Size(Math.Max(width, 80), height);

        int btn = _captionHeight - Math.Max(2, (int)Math.Round(5 * scale));
        _closeRect = new Rectangle(ClientSize.Width - _border - btn - 2, _border + 2, btn, btn);
    }

    private Rectangle CaptionRect => new(_border, _border, ClientSize.Width - (2 * _border), _captionHeight);

    protected override void OnPaint(PaintEventArgs e)
    {
        var g = e.Graphics;
        var renderer = _control.Renderer;
        var caption = CaptionRect;
        renderer.DrawFloatingWindowChrome(g, ClientRectangle, caption);

        TextRenderer.DrawText(g, _bar.Text, Font,
            new Rectangle(caption.X + 5, caption.Y, caption.Width - _closeRect.Width - 12, caption.Height),
            renderer.FloatingCaptionTextColor,
            TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis);

        FloatingCaptionButtonPainter.DrawClose(g, renderer, _closeRect,
            _closeHot, _closePressed);
    }

    protected override void OnMouseMove(MouseEventArgs e)
    {
        base.OnMouseMove(e);
        if (_dragging)
        {
            Location = new Point(Cursor.Position.X - _dragOffset.X, Cursor.Position.Y - _dragOffset.Y);
            _host.UpdateBarDrag(Cursor.Position, floatGhost: false);
            return;
        }

        bool hot = _closeRect.Contains(e.Location);
        if (hot != _closeHot)
        {
            _closeHot = hot;
            Invalidate(_closeRect);
        }
    }

    protected override void OnMouseLeave(EventArgs e)
    {
        base.OnMouseLeave(e);
        if (_closeHot)
        {
            _closeHot = false;
            Invalidate(_closeRect);
        }
    }

    protected override void OnMouseDown(MouseEventArgs e)
    {
        base.OnMouseDown(e);
        if (e.Button != MouseButtons.Left)
            return;
        if (_closeRect.Contains(e.Location))
        {
            _closeHot = true;
            _closePressed = true;
            Capture = true;
            Invalidate(_closeRect);
            return;
        }
        if (CaptionRect.Contains(e.Location))
        {
            _dragging = true;
            _dragOffset = new Point(Cursor.Position.X - Location.X, Cursor.Position.Y - Location.Y);
            Capture = true;
            // Preview the size the toolbar will have once docked.
            _host.BeginBarDrag(_bar, _control.PreferredDockedSize, e.Location);
        }
    }

    protected override void OnMouseUp(MouseEventArgs e)
    {
        base.OnMouseUp(e);
        if (e.Button != MouseButtons.Left)
            return;

        if (_closePressed)
        {
            bool activate = _closeRect.Contains(e.Location);
            _closePressed = false;
            _closeHot = activate;
            Capture = false;
            Invalidate(_closeRect);
            if (activate)
                RequestDock();
            return;
        }

        if (_dragging)
        {
            _dragging = false;
            Capture = false;
            // EndBarDrag docks it if over the band; otherwise it stays floating.
            bool docked = _host.EndBarDrag(Cursor.Position, floatOutside: false);
            if (!docked)
                _bar.FloatingBounds = new Rectangle(Location, Size);
            return;
        }

        if (_closeRect.Contains(e.Location))
            RequestDock();
    }

    protected override void OnMouseDoubleClick(MouseEventArgs e)
    {
        base.OnMouseDoubleClick(e);
        if (CaptionRect.Contains(e.Location) && !_closeRect.Contains(e.Location))
            RequestDock();
    }

    private void RequestDock()
    {
        var host = _host;
        var bar = _bar;
        // Defer: DockBar closes this window, so don't do it inside its own event.
        BeginInvoke((MethodInvoker)(() => host.DockBar(bar)));
    }
}

/// <summary>Shared close-button painter for dockable and tear-off mini frames.</summary>
internal static class FloatingCaptionButtonPainter
{
    public static void DrawClose(Graphics graphics, CommandBarRenderer renderer,
        Rectangle bounds, bool hot, bool pressed)
    {
        var colors = renderer.Colors;
        Rectangle glyphBounds = bounds;
        if (renderer.DialogColors.UsesClassic3DChrome)
        {
            var dialogColors = renderer.DialogColors;
            using (var fill = new SolidBrush(dialogColors.ButtonBegin))
                graphics.FillRectangle(fill, bounds);
            bool sunken = pressed && hot;
            DialogControlPainter.DrawClassicBevel(graphics, bounds,
                dialogColors, sunken);
            // The classic bevel has a two-pixel trailing shadow, so its visible
            // face is optically centered one pixel left of the outer rectangle.
            glyphBounds.Offset(-1, 0);
            if (sunken)
                glyphBounds.Offset(1, 1);
            DrawCloseGlyph(graphics, glyphBounds, dialogColors.ButtonText);
            return;
        }

        if (hot)
        {
            using var hotFill = new SolidBrush(colors.ButtonHotEnd);
            graphics.FillRectangle(hotFill, bounds);
            using var hotBorder = new Pen(colors.ButtonHotBorder);
            graphics.DrawRectangle(hotBorder, new Rectangle(bounds.X, bounds.Y,
                bounds.Width - 1, bounds.Height - 1));
        }
        DrawCloseGlyph(graphics, bounds,
            hot ? colors.Text : renderer.FloatingCaptionTextColor);
    }

    private static void DrawCloseGlyph(Graphics graphics, Rectangle bounds, Color color)
    {
        var previous = graphics.SmoothingMode;
        graphics.SmoothingMode = SmoothingMode.AntiAlias;
        using var pen = new Pen(color, 1.6f);
        int margin = Math.Max(3, bounds.Width / 4);
        int right = bounds.Right - 1 - margin;
        int bottom = bounds.Bottom - 1 - margin;
        graphics.DrawLine(pen, bounds.Left + margin, bounds.Top + margin,
            right, bottom);
        graphics.DrawLine(pen, right, bounds.Top + margin,
            bounds.Left + margin, bottom);
        graphics.SmoothingMode = previous;
    }
}
