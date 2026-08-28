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
        var colors = renderer.Colors;
        var caption = CaptionRect;
        renderer.DrawFloatingWindowChrome(g, ClientRectangle, caption);

        TextRenderer.DrawText(g, _bar.Text, Font,
            new Rectangle(caption.X + 5, caption.Y, caption.Width - _closeRect.Width - 12, caption.Height),
            renderer.FloatingCaptionTextColor,
            TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis);

        if (_closeHot)
        {
            using var hot = new SolidBrush(colors.ButtonHotEnd);
            g.FillRectangle(hot, _closeRect);
            using var hb = new Pen(colors.ButtonHotBorder);
            g.DrawRectangle(hb, new Rectangle(_closeRect.X, _closeRect.Y, _closeRect.Width - 1, _closeRect.Height - 1));
        }
        DrawCloseGlyph(g, _closeRect,
            _closeHot ? colors.Text : renderer.FloatingCaptionTextColor);
    }

    private static void DrawCloseGlyph(Graphics g, Rectangle r, Color color)
    {
        var previous = g.SmoothingMode;
        g.SmoothingMode = SmoothingMode.AntiAlias;
        using var pen = new Pen(color, 1.6f);
        int m = Math.Max(3, r.Width / 4);
        g.DrawLine(pen, r.Left + m, r.Top + m, r.Right - m, r.Bottom - m);
        g.DrawLine(pen, r.Right - m, r.Top + m, r.Left + m, r.Bottom - m);
        g.SmoothingMode = previous;
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
            return; // acted on mouse-up
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
