using System.ComponentModel;
using System.Drawing;
using System.Windows.Forms;
using CommandBars.Model;
using CommandBars.Rendering;

namespace CommandBars.Controls;

/// <summary>
/// A standalone floating palette created by "tearing off" a popup/dropdown menu
/// (Office's tear-off toolbars — Font Color, AutoShapes, …). It hosts the torn-off
/// <see cref="CommandBar"/> in a <see cref="CommandBarControl"/> rendered as a
/// toolbar, with a themed caption and a close button.
///
/// Unlike <see cref="FloatingWindow"/> it has <b>no dock affordances at all</b>:
/// it never talks to a <see cref="DockHost"/>, so it can be moved but never
/// re-docked — matching the Office behaviour the caller asked for. Closing it just
/// disposes the palette; the original dropdown still opens the same bar as a menu.
///
/// Like the other popup surfaces it is <b>non-activating</b> (owner form keeps
/// focus). Re-themes itself from the owning manager's <c>ThemeChanged</c> event.
/// </summary>
[ToolboxItem(false)]
[DesignerCategory("")]
public sealed class TearOffWindow : Form
{
    private const int WS_EX_NOACTIVATE = 0x08000000;
    private const int WS_EX_TOOLWINDOW = 0x00000080;
    private const int WM_MOUSEACTIVATE = 0x0021;
    private const int MA_NOACTIVATE = 3;

    private readonly CommandBar _bar;
    private readonly CommandBar _sourceBar;
    private readonly CommandBarControl _control;
    private readonly CommandBarManager? _manager;

    private int _captionHeight;
    private int _border;
    private Rectangle _closeRect;
    private bool _closeHot;
    private bool _closePressed;
    private bool _dragging;
    private Point _dragOffset;

    /// <param name="bar">The bar the palette hosts (a private clone of the menu's dropdown).</param>
    /// <param name="sourceBar">The original dropdown this palette was torn off from — its
    /// stable identity for de-duping and persistence.</param>
    /// <param name="renderer">Renderer used for the palette and hosted command bar.</param>
    /// <param name="manager">Owning command-bar manager, when available.</param>
    /// <param name="owner">Application form, or the palette from which a nested menu was torn off.</param>
    public TearOffWindow(CommandBar bar, CommandBar sourceBar, CommandBarRenderer renderer, CommandBarManager? manager, Form? owner)
    {
        _bar = bar ?? throw new ArgumentNullException(nameof(bar));
        _sourceBar = sourceBar ?? bar;
        _manager = manager;

        FormBorderStyle = FormBorderStyle.None;
        ShowInTaskbar = false;
        StartPosition = FormStartPosition.Manual;
        DoubleBuffered = true;
        SetStyle(ControlStyles.ResizeRedraw, true);
        // A palette may be torn off from another tear-off or from an undocked
        // toolbar. Both are transient windows: make the detached palette a
        // sibling owned by the persistent application form. Otherwise WinForms
        // automatically closes the palette chain when its toolbar is re-docked
        // (or its parent palette closes). Restored tear-offs already use the
        // application form, so normalizing also keeps fresh/restored ownership
        // consistent.
        var paletteOwner = FindPaletteOwner(owner);
        if (paletteOwner is not null)
            Owner = paletteOwner;

        _control = new CommandBarControl { Renderer = renderer };
        _control.PaletteMode = true; // horizontal, icon-only
        Controls.Add(_control);
        _control.Bar = bar;

        if (_manager is not null)
            _manager.ThemeChanged += OnThemeChanged;

        Relayout();
    }

    private static Form? FindPaletteOwner(Form? owner)
    {
        while (owner is TearOffWindow or FloatingWindow)
            owner = owner.Owner;
        return owner;
    }

    /// <summary>The bar shown by this palette (a clone of <see cref="SourceBar"/>).</summary>
    public CommandBar Bar => _bar;

    /// <summary>The original dropdown bar this palette was torn off from.</summary>
    public CommandBar SourceBar => _sourceBar;

    /// <summary>The hosted bar control.</summary>
    public CommandBarControl BarControl => _control;

    private void OnThemeChanged(object? sender, EventArgs e)
    {
        if (_manager is not null)
        {
            _control.Renderer = _manager.Renderer;
            Relayout();
            Invalidate();
        }
    }

    protected override void OnHandleCreated(EventArgs e)
    {
        base.OnHandleCreated(e);
        PopupWindowChrome.Apply(this, _control.Renderer);
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
        _captionHeight = Font.Height + (int)Math.Round((_control.Renderer.UsesFluentMenuChrome ? 12 : 6) * scale);

        _control.Relayout();
        _control.Location = new Point(_border, _border + _captionHeight);
        // A swatch grid (PaletteColumns > 0) sizes both axes itself in Relayout;
        // a linear palette lays out horizontally, so its content width drives the frame.
        if (_bar.PaletteColumns <= 0)
            _control.Width = _control.PreferredContentWidth;

        int width = _control.Width + (2 * _border);
        int height = _control.Height + _captionHeight + (2 * _border);
        ClientSize = new Size(Math.Max(width, 80), height);

        int btn = _captionHeight - Math.Max(2, (int)Math.Round(5 * scale));
        int closeY = _border + ((_captionHeight - btn) / 2);
        _closeRect = new Rectangle(ClientSize.Width - _border - btn - 2,
            closeY, btn, btn);
    }

    private Rectangle CaptionRect => new(_border, _border, ClientSize.Width - (2 * _border), _captionHeight);

    protected override void OnPaint(PaintEventArgs e)
    {
        var g = e.Graphics;
        var renderer = _control.Renderer;
        var caption = CaptionRect;
        renderer.DrawFloatingWindowChrome(g, ClientRectangle, caption);

        TextRenderer.DrawText(g, _bar.Text, Font,
            new Rectangle(caption.X + (_control.Renderer.UsesFluentMenuChrome ? (int)Math.Round(12 * DeviceDpi / 96f) : 5), caption.Y, caption.Width - _closeRect.Width - (_control.Renderer.UsesFluentMenuChrome ? (int)Math.Round(24 * DeviceDpi / 96f) : 12), caption.Height),
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
            // Move only — a tear-off palette never docks.
            Location = new Point(Cursor.Position.X - _dragOffset.X, Cursor.Position.Y - _dragOffset.Y);
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

    /// <summary>
    /// Continues an in-progress mouse drag on this window as a caption move, so a
    /// palette just torn off by dragging a menu's grip keeps following the cursor
    /// until the button is released (instead of popping into place). The window is
    /// re-positioned so the cursor sits on its caption, then it captures the mouse
    /// and enters the same drag state as pressing the caption. No-op if the left
    /// button was already released by the time this runs.
    /// </summary>
    public void BeginTearDrag()
    {
        if ((Control.MouseButtons & MouseButtons.Left) == 0)
            return; // released before the tear-off completed — just leave it placed

        var cursor = Cursor.Position;
        int grabX = Math.Min(30, Math.Max(8, ClientSize.Width / 3));
        int grabY = _border + (_captionHeight / 2);
        Location = new Point(cursor.X - grabX, cursor.Y - grabY);
        _dragOffset = new Point(grabX, grabY);
        _dragging = true;
        Capture = true;
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
                Close();
            return;
        }

        if (_dragging)
        {
            _dragging = false;
            Capture = false;
            _bar.FloatingBounds = new Rectangle(Location, Size);
            return;
        }

        if (_closeRect.Contains(e.Location))
            Close();
    }

    protected override void OnFormClosed(FormClosedEventArgs e)
    {
        if (_manager is not null)
            _manager.ThemeChanged -= OnThemeChanged;
        base.OnFormClosed(e);
    }
}
