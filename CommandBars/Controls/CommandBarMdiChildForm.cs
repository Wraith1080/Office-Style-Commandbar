using System.ComponentModel;
using System.Drawing;
using System.Windows.Forms;
using CommandBars.Rendering;

namespace CommandBars.Controls;

/// <summary>An opt-in MDI child with themed client-drawn chrome and native MDI behavior.</summary>
public class CommandBarMdiChildForm : Form
{
    private readonly MdiChildFrameController _frame;
    private readonly CaptionSurface _caption;
    private readonly Office2003Renderer _fallbackRenderer = new();
    private CommandBarManager? _manager;
    private bool _layingOutFrame;
    private int _cornerRadius = -1;

    /// <summary>Logical corner radius; -1 follows the theme, 0 is square. Maximized/minimized frames remain square.</summary>
    [DefaultValue(-1)]
    public int CornerRadius
    {
        get => _cornerRadius;
        set
        {
            if (value < -1) throw new ArgumentOutOfRangeException(nameof(value));
            if (_cornerRadius == value) return;
            _cornerRadius = value;
            UpdateFrame();
            _frame?.QueueLayout();
        }
    }
    internal int EffectiveCornerRadius => WindowState == FormWindowState.Normal
        ? (_cornerRadius < 0 ? FrameRenderer.MdiChildCornerRadius : _cornerRadius) : 0;

    public CommandBarMdiChildForm()
    {
        AutoScaleMode = AutoScaleMode.Dpi;
        DoubleBuffered = true;
        _caption = new CaptionSurface(this);
        Controls.Add(_caption);
        _frame = new MdiChildFrameController(this);
        UpdateFrame();
    }

    /// <summary>Supplies the live theme. The form does not own this manager.</summary>
    [DefaultValue(null)]
    public CommandBarManager? Manager
    {
        get => _manager;
        set
        {
            if (ReferenceEquals(value, _manager)) return;
            if (_manager != null) _manager.ThemeChanged -= ThemeChanged;
            _manager = value;
            if (_manager != null) _manager.ThemeChanged += ThemeChanged;
            UpdateFrame();
            _frame?.QueueLayout();
        }
    }

    internal CommandBarRenderer FrameRenderer => _manager?.Renderer ?? _fallbackRenderer;
    internal bool IsFrameActive => MdiParent?.ActiveMdiChild == this;
    internal int FrameBorder => Math.Max(1, (int)Math.Round(5 * DeviceDpi / 96f));
    internal int CaptionHeight => Math.Max(Font.Height + (int)Math.Round(10 * DeviceDpi / 96f), (int)Math.Round(24 * DeviceDpi / 96f));
    internal bool FrameMaximized => WindowState == FormWindowState.Maximized;
    internal Rectangle CaptionBounds => _caption.Bounds;

    private void ThemeChanged(object? sender, EventArgs e) { UpdateFrame(); _frame?.QueueLayout(); }

    internal void UpdateFrame()
    {
        if (_caption == null || IsDisposed || _layingOutFrame) return;
        _layingOutFrame = true;
        try
        {
            bool visible = MdiParent != null && !FrameMaximized;
            int border = visible ? FrameBorder : 0;
            int caption = visible ? CaptionHeight : 0;
            Padding = new Padding(border, border + caption, border, border);
            _caption.Visible = visible;
            _caption.SetBounds(border, border, Math.Max(0, ClientSize.Width - border * 2), caption);
            _caption.UpdateButtons();
            Invalidate(true);
        }
        finally { _layingOutFrame = false; }
    }

    protected override void WndProc(ref Message m)
    {
        // Creation messages arrive before HandleCreated can attach the frame
        // controller. Suppress native chrome from the very first calculation,
        // including when WinForms recreates an existing child's handle.
        if (MdiParent != null)
        {
            if (m.Msg is 0x0083 or 0x0085 or 0x00AE or 0x00AF)
            {
                m.Result = IntPtr.Zero;
                return;
            }
            if (m.Msg == 0x0086) { m.Result = new IntPtr(1); return; }
        }
        base.WndProc(ref m);
    }

    protected override void OnLayout(LayoutEventArgs e) { base.OnLayout(e); UpdateFrame(); }
    protected override void OnTextChanged(EventArgs e) { base.OnTextChanged(e); _caption?.Invalidate(); Invalidate(); }
    protected override void OnFontChanged(EventArgs e) { base.OnFontChanged(e); UpdateFrame(); }
    protected override void OnActivated(EventArgs e) { base.OnActivated(e); UpdateFrame(); }
    protected override void OnDeactivate(EventArgs e) { base.OnDeactivate(e); UpdateFrame(); }
    protected override void OnDpiChangedAfterParent(EventArgs e)
    {
        base.OnDpiChangedAfterParent(e);
        UpdateFrame();
        _frame?.QueueLayout();
    }
    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);
        if (MdiParent == null || FrameMaximized) return;
        FrameRenderer.Scale = DeviceDpi / 96f;
        FrameRenderer.DrawMdiChildBorder(e.Graphics, ClientRectangle, FrameBorder, IsFrameActive, EffectiveCornerRadius);
    }
    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            if (_manager != null) _manager.ThemeChanged -= ThemeChanged;
            _frame?.Dispose();
        }
        base.Dispose(disposing);
    }

    private sealed class CaptionSurface : Control
    {
        private readonly CommandBarMdiChildForm _form;
        private readonly CaptionAction[] _buttons;
        internal CaptionSurface(CommandBarMdiChildForm form)
        {
            _form = form;
            SetStyle(ControlStyles.UserPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.AllPaintingInWmPaint, true);
            TabStop = false;
            _buttons = new[] { new CaptionAction(form, 0, "Child system menu"), new CaptionAction(form, 1, "Minimize child"),
                new CaptionAction(form, 2, "Maximize or restore child"), new CaptionAction(form, 3, "Close child") };
            Controls.AddRange(_buttons);
        }
        internal void UpdateButtons()
        {
            int size = Math.Min(Height, Math.Max(1, (int)Math.Round(22 * DeviceDpi / 96f)));
            for (int i = 0; i < _buttons.Length; i++)
            {
                _buttons[i].Bounds = new Rectangle(i == 0 ? 0 : Math.Max(0, Width - (4 - i) * size), (Height - size) / 2, size, size);
                _buttons[i].Visible = _form.ControlBox;
                _buttons[i].Enabled = i == 1 ? _form.MinimizeBox || _form.WindowState == FormWindowState.Minimized : i == 2 ? _form.MaximizeBox : true;
            }
            _buttons[1].AccessibleName = _form.WindowState == FormWindowState.Minimized ? "Restore child" : "Minimize child";
            _buttons[2].AccessibleName = _form.FrameMaximized ? "Restore child" : "Maximize child";
        }
        internal void PaintCaption(Graphics graphics)
        {
            _form.FrameRenderer.Scale = _form.DeviceDpi / 96f;
            int leading = _form.ControlBox ? _buttons[0].Right : 0;
            int trailing = _form.ControlBox ? _buttons[1].Left : Width;
            _form.FrameRenderer.DrawMdiChildCaption(graphics, ClientRectangle,
                new Rectangle(leading, 0, Math.Max(0, trailing - leading), Height), _form.Text, Font, _form.IsFrameActive);
        }
        protected override void OnPaint(PaintEventArgs e) => PaintCaption(e.Graphics);
        protected override void WndProc(ref Message m)
        {
            // Let the form's native caption hit testing handle drag/double-click.
            // Caption buttons have their own HWNDs and remain client hit targets.
            if (m.Msg == 0x0084) { m.Result = new IntPtr(-1); return; }
            base.WndProc(ref m);
        }
    }

    private sealed class CaptionAction : Button
    {
        private readonly CommandBarMdiChildForm _form;
        private readonly int _action;
        private bool _hot, _pressed;
        internal CaptionAction(CommandBarMdiChildForm form, int action, string name)
        {
            _form = form; _action = action;
            AccessibleName = name;
            TabStop = false;
            FlatStyle = FlatStyle.Flat;
            SetStyle(ControlStyles.Selectable, false);
        }
        protected override void OnClick(EventArgs e)
        {
            base.OnClick(e);
            Execute();
        }
        private void Execute()
        {
            if (!Enabled) return;
            if (_action == 0) _form._frame.ShowSystemMenu(PointToScreen(new Point(0, Height)));
            else if (_action == 1) _form.WindowState = _form.WindowState == FormWindowState.Minimized ? FormWindowState.Normal : FormWindowState.Minimized;
            else if (_action == 2) _form.WindowState = _form.FrameMaximized ? FormWindowState.Normal : FormWindowState.Maximized;
            else _form.Close();
        }
        protected override AccessibleObject CreateAccessibilityInstance() => new ActionAccessibleObject(this);
        private sealed class ActionAccessibleObject : ControlAccessibleObject
        {
            private readonly CaptionAction _button;
            internal ActionAccessibleObject(CaptionAction button) : base(button) => _button = button;
            public override AccessibleRole Role => AccessibleRole.PushButton;
            public override string DefaultAction => "Press";
            public override void DoDefaultAction() => _button.Execute();
        }
        protected override void WndProc(ref Message m)
        {
            if (m.Msg == 0x007B && _action == 0) { Execute(); return; }
            base.WndProc(ref m);
        }
        protected override void OnMouseEnter(EventArgs e) { base.OnMouseEnter(e); _hot = true; Invalidate(); }
        protected override void OnMouseLeave(EventArgs e) { base.OnMouseLeave(e); _hot = false; Invalidate(); }
        protected override void OnMouseDown(MouseEventArgs e) { base.OnMouseDown(e); _pressed = e.Button == MouseButtons.Left; Invalidate(); }
        protected override void OnMouseUp(MouseEventArgs e) { _pressed = false; base.OnMouseUp(e); Invalidate(); }
        protected override void OnMouseCaptureChanged(EventArgs e) { _pressed = false; base.OnMouseCaptureChanged(e); Invalidate(); }
        protected override void OnPaint(PaintEventArgs e)
        {
            var saved = e.Graphics.Save();
            e.Graphics.TranslateTransform(-Left, -Top);
            ((CaptionSurface)Parent!).PaintCaption(e.Graphics);
            e.Graphics.Restore(saved);
            CaptionButton? action = _action == 0 ? null : _action == 1 ? CaptionButton.Minimize : _action == 3 ? CaptionButton.Close :
                _form.WindowState == FormWindowState.Normal ? CaptionButton.Maximize : CaptionButton.Restore;
            var state = !Enabled ? RenderState.Disabled : _pressed && _hot ? RenderState.Pressed : _hot ? RenderState.Hot : RenderState.Normal;
            _form.FrameRenderer.DrawMdiButton(e.Graphics, ClientRectangle, action, _action == 0 ? _form.Icon : null, state);
        }
    }
}
