using System.Drawing;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using CommandBars.Model;
using CommandBars.Rendering;

namespace CommandBars.Controls;

public partial class CommandBarControl
{
    private Form? _mdiParent;
    private Form? _mdiChild;
    private bool _mdiShown;
    private MdiButton[]? _mdiButtons;
    internal bool HasMdiChrome => _mdiShown;
    private int MdiButtonExtent => Math.Max(18, (int)Math.Round(20 * _dpiScale));
    private int MdiLeadingExtent => HasMdiChrome ? MdiButtonExtent : 0;
    private int MdiTrailingExtent => HasMdiChrome ? 3 * MdiButtonExtent : 0;

    protected override void OnHandleCreated(EventArgs e)
    {
        base.OnHandleCreated(e);
        RefreshMdiChild();
    }

    protected override void OnParentChanged(EventArgs e)
    {
        base.OnParentChanged(e);
        RefreshMdiChild();
    }

    internal void RefreshMdiChild()
    {
        if (IsDisposed || Disposing) return;
        // Resolve through registered dock hosts as floating frames are not MDI containers.
        var owner = _bar?.Manager?.Hosts.Select(host => host.FindForm())
            .FirstOrDefault(form => form is { IsMdiContainer: true });
        bool first = _bar is { BarType: CommandBarType.MenuBar, Visible: true } &&
            ReferenceEquals(_bar.Manager?.Bars.FirstOrDefault(bar => bar.BarType == CommandBarType.MenuBar), _bar);
        if (!first || _paletteMode) owner = null;
        if (owner != null) _bar?.Manager?.EnsureMdiMenuMode(owner);
        if (!ReferenceEquals(owner, _mdiParent))
        {
            if (_mdiParent != null) _mdiParent.MdiChildActivate -= MdiChanged;
            _mdiParent = owner;
            if (_mdiParent != null) _mdiParent.MdiChildActivate += MdiChanged;
        }
        var child = owner?.ActiveMdiChild;
        bool changed = !ReferenceEquals(child, _mdiChild);
        if (changed)
        {
            if (_mdiChild != null) _mdiChild.Resize -= MdiChanged;
            _mdiChild = child;
            if (_mdiChild != null) _mdiChild.Resize += MdiChanged;
        }
        bool show = child is { IsDisposed: false, WindowState: FormWindowState.Maximized, ControlBox: true };
        if (changed || show != _mdiShown)
        {
            _mdiShown = show;
            Relayout();
            if (Parent is FloatingWindow floating) floating.Relayout();
            else Parent?.PerformLayout();
        }
        if (_mdiButtons != null)
        {
            _mdiButtons[1].Enabled = child?.MinimizeBox == true;
            _mdiButtons[2].Enabled = child?.MaximizeBox == true;
            if (changed) foreach (var button in _mdiButtons) button.Invalidate();
        }
    }

    private void MdiChanged(object? sender, EventArgs e) => RefreshMdiChild();

    private void DetachMdi()
    {
        if (_mdiParent != null) _mdiParent.MdiChildActivate -= MdiChanged;
        if (_mdiChild != null) _mdiChild.Resize -= MdiChanged;
        _mdiParent = _mdiChild = null;
    }

    private void ApplyMdiLayout()
    {
        if (HasMdiChrome && _bar != null)
        {
            foreach (var item in _bar.Items)
            {
                var bounds = item.Bounds;
                bounds.Offset(Vertical ? 0 : MdiLeadingExtent, Vertical ? MdiLeadingExtent : 0);
                item.Bounds = bounds;
            }
            if (Vertical) _contentHeight += MdiLeadingExtent + MdiTrailingExtent;
            else _contentWidth += MdiLeadingExtent + MdiTrailingExtent;
        }
        PositionMdiButtons();
    }

    private void PositionMdiButtons()
    {
        if (HasMdiChrome && _mdiButtons == null)
        {
            _mdiButtons = new[]
            {
                new MdiButton(this, 0, "Child system menu"),
                new MdiButton(this, 1, "Minimize child"),
                new MdiButton(this, 2, "Restore child"),
                new MdiButton(this, 3, "Close child")
            };
            Controls.AddRange(_mdiButtons);
        }
        if (_mdiButtons == null) return;
        for (int i = 0; i < _mdiButtons.Length; i++)
        {
            var button = _mdiButtons[i];
            button.Visible = HasMdiChrome;
            int position = i == 0 ? (_showGripper ? _renderer.GripperExtent : 0) + _metrics.TopInset
                : (Vertical ? Height : Width) - (4 - i) * MdiButtonExtent;
            int crossExtent = Vertical ? Width : _rowHeight;
            int size = Math.Max(1, Math.Min(MdiButtonExtent, crossExtent));
            int alongInset = (MdiButtonExtent - size) / 2;
            int crossInset = (crossExtent - size) / 2;
            button.Bounds = Vertical
                ? new Rectangle(crossInset, position + alongInset, size, size)
                : new Rectangle(position + alongInset, _metrics.TopInset + crossInset, size, size);
        }
    }

    private void InvokeMdiAction(int action)
    {
        var child = _mdiChild;
        if (!HasMdiChrome || child == null || child.IsDisposed) return;
        if (action == 0)
        {
            var point = _mdiButtons![0].PointToScreen(new Point(0, _mdiButtons[0].Height));
            var menu = GetSystemMenu(child.Handle, false);
            // Ask the child to update its native system menu before showing it.
            SendMessage(child.Handle, 0x0117, menu, new IntPtr(1L << 16)); // WM_INITMENUPOPUP
            int command = TrackPopupMenuEx(menu, 0x0102, point.X, point.Y, child.Handle, IntPtr.Zero); // TPM_RETURNCMD | TPM_RIGHTBUTTON
            if (command != 0 && !child.IsDisposed)
                SendMessage(child.Handle, 0x0112, new IntPtr(command), IntPtr.Zero);
        }
        else if (action == 1 && child.MinimizeBox) child.WindowState = FormWindowState.Minimized;
        else if (action == 2 && child.MaximizeBox) child.WindowState = FormWindowState.Normal;
        else if (action == 3) child.Close(); // preserve FormClosing cancellation
        RefreshMdiChild();
    }

    [DllImport("user32.dll")] private static extern IntPtr GetSystemMenu(IntPtr window, bool revert);
    [DllImport("user32.dll")] private static extern int TrackPopupMenuEx(IntPtr menu, uint flags, int x, int y, IntPtr window, IntPtr parameters);
    [DllImport("user32.dll")] private static extern IntPtr SendMessage(IntPtr window, uint message, IntPtr wParam, IntPtr lParam);

    internal sealed class MdiButton : Button
    {
        private readonly CommandBarControl _owner;
        private readonly int _action;
        private bool _hot;
        private bool _down;
        internal MdiButton(CommandBarControl owner, int action, string name)
        {
            _owner = owner;
            _action = action;
            AccessibleName = name;
            AccessibleRole = AccessibleRole.PushButton;
            TabStop = false;
            // These are caption actions: clicking them must not transfer focus
            // from the active document into a dock host or nonactivating frame.
            SetStyle(ControlStyles.Selectable, false);
            FlatStyle = FlatStyle.Flat;
            SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer, true);
            owner._toolTip.SetToolTip(this, name);
        }
        public new void PerformClick()
        {
            if (Enabled && Visible) OnClick(EventArgs.Empty);
        }
        protected override AccessibleObject CreateAccessibilityInstance() => new MdiButtonAccessibleObject(this);
        private sealed class MdiButtonAccessibleObject : ControlAccessibleObject
        {
            private readonly MdiButton _button;
            internal MdiButtonAccessibleObject(MdiButton button) : base(button) => _button = button;
            public override string DefaultAction => "Press";
            public override void DoDefaultAction() => _button.PerformClick();
        }
        protected override void OnClick(EventArgs e) { base.OnClick(e); _owner.InvokeMdiAction(_action); }
        protected override void WndProc(ref Message m)
        {
            if (m.Msg == 0x007B && _action == 0) // WM_CONTEXTMENU: right-click or keyboard context menu
            {
                PerformClick();
                return;
            }
            base.WndProc(ref m);
        }
        protected override void OnMouseEnter(EventArgs e) { base.OnMouseEnter(e); _hot = true; Invalidate(); }
        protected override void OnMouseLeave(EventArgs e) { base.OnMouseLeave(e); _hot = false; Invalidate(); }
        protected override void OnMouseDown(MouseEventArgs e) { base.OnMouseDown(e); _down = e.Button == MouseButtons.Left; Invalidate(); }
        protected override void OnMouseUp(MouseEventArgs e) { _down = false; base.OnMouseUp(e); Invalidate(); }
        protected override void OnMouseCaptureChanged(EventArgs e) { _down = false; base.OnMouseCaptureChanged(e); Invalidate(); }
        protected override void OnPaintBackground(PaintEventArgs e)
        {
            var graphics = e.Graphics;
            var saved = graphics.Save();
            graphics.TranslateTransform(-Left, -Top);
            if (_owner.Docked)
            {
                int extent = _owner.Vertical ? (_owner.Parent?.Height ?? _owner.Height) : (_owner.Parent?.Width ?? _owner.Width);
                int offset = _owner.Vertical ? _owner.Top : _owner.Left;
                _owner.Renderer.DrawBarBackground(graphics, _owner.ClientRectangle, CommandBarType.MenuBar,
                    _owner.LayoutOrientation, false, offset, extent);
            }
            else
            {
                using var brush = new SolidBrush(_owner.Renderer.Colors.BarGradientBegin);
                graphics.FillRectangle(brush, _owner.ClientRectangle);
            }
            graphics.Restore(saved);
        }
        protected override void OnPaint(PaintEventArgs e)
        {
            // ButtonBase can suppress the separate background pass. Always initialize
            // the buffered surface before drawing a transparent icon or line glyph.
            OnPaintBackground(e);
            var state = !Enabled ? RenderState.Disabled : _down && _hot ? RenderState.Pressed : _hot ? RenderState.Hot : RenderState.Normal;
            _owner.Renderer.DrawMdiButton(e.Graphics, ClientRectangle, _action == 0 ? null : (CaptionButton?)(_action == 1 ? CaptionButton.Minimize : _action == 2 ? CaptionButton.Restore : CaptionButton.Close),
                _action == 0 ? _owner._mdiChild?.Icon : null, state);
        }
    }
}
