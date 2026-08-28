using System.Drawing;
using System.Drawing.Drawing2D;
using CommandBars.Model;

namespace CommandBars.Rendering;

/// <summary>
/// Fixed Windows 2000-era colors used by the Office 2000 theme. They are kept
/// independent of the host OS theme so the classic appearance remains stable
/// on current versions of Windows.
/// </summary>
public sealed class Office2000ColorTable : CommandBarColorTable
{
    private static Color C(int r, int g, int b) => Color.FromArgb(r, g, b);

    private static Color Control => C(212, 208, 200);
    private static Color Light => C(255, 255, 255);
    private static Color Dark => C(128, 128, 128);
    private static Color Darkest => C(64, 64, 64);
    private static Color Navy => C(10, 36, 106);

    public override Color BarGradientBegin => Control;
    public override Color BarGradientMiddle => Control;
    public override Color BarGradientEnd => Control;
    public override Color MenuBarGradientBegin => Control;
    public override Color MenuBarGradientEnd => Control;
    public override Color BarBorder => Dark;

    public override Color BandGradientBegin => Control;
    public override Color BandGradientEnd => Control;
    public override Color RaisedBorder => Light;

    public override Color ChevronGradientBegin => Control;
    public override Color ChevronGradientEnd => Control;
    public override Color DropPreview => Navy;

    public override Color ButtonHotBegin => Control;
    public override Color ButtonHotEnd => Control;
    public override Color ButtonHotBorder => Dark;
    public override Color ButtonPressedBegin => Control;
    public override Color ButtonPressedEnd => Control;
    public override Color ButtonPressedBorder => Darkest;
    public override Color ButtonCheckedBegin => C(192, 192, 192);
    public override Color ButtonCheckedEnd => C(192, 192, 192);
    public override Color ButtonCheckedBorder => Darkest;

    public override Color MenuOpenBegin => Control;
    public override Color MenuOpenEnd => Control;
    public override Color MenuOpenBorder => Dark;

    public override Color SeparatorDark => Dark;
    public override Color SeparatorLight => Light;
    public override Color GripperDark => Dark;
    public override Color GripperLight => Light;

    public override Color Text => Color.Black;
    public override Color DisabledText => Dark;

    public override Color MenuBackground => Control;
    public override Color MenuBorder => Darkest;
    public override Color ImageMarginBegin => Control;
    public override Color ImageMarginEnd => Control;
    public override Color MenuItemSelectedBegin => Navy;
    public override Color MenuItemSelectedEnd => Navy;
    public override Color MenuItemSelectedBorder => Navy;
    public override Color MenuItemSelectedText => Color.White;
    public override Color MenuText => Color.Black;
    public override Color DisabledMenuText => Dark;
}

/// <summary>
/// Office 2000 renderer: flat gray bars, square Win32 bevels, a compact single
/// raised-slab gripper, and classic navy menu selection.
/// </summary>
public sealed class Office2000Renderer : Office2003Renderer
{
    public override CommandBarColorTable Colors { get; } = new Office2000ColorTable();

    internal override bool ConnectPopupOwners => false;
    internal override bool UsesClassicMenuItemChrome => true;

    protected override int ChunkRadius => 0;

    public override int GripperExtent => Dp(7);

    public override void DrawBand(Graphics g, Rectangle bounds, BarOrientation orientation)
    {
        if (bounds.Width <= 0 || bounds.Height <= 0)
            return;
        using var brush = new SolidBrush(Colors.BandGradientBegin);
        g.FillRectangle(brush, bounds);
    }

    public override void DrawBarBackground(Graphics g, Rectangle bounds,
        CommandBarType barType, BarOrientation orientation, bool rounded,
        int bandOffset, int bandExtent)
    {
        if (bounds.Width <= 0 || bounds.Height <= 0)
            return;
        using (var brush = new SolidBrush(Colors.BarGradientBegin))
            g.FillRectangle(brush, bounds);

        // Both the menu bar and toolbars are square raised slabs in Office 2000.
        if ((rounded || barType == CommandBarType.MenuBar) && bounds.Width > 2 && bounds.Height > 2)
            DrawBevel(g, new Rectangle(bounds.X, bounds.Y, bounds.Width - 1, bounds.Height - 1),
                sunken: false, PopupConnectionEdge.None);
    }

    public override void DrawGripper(Graphics g, Rectangle bounds, BarOrientation orientation)
    {
        // One compact raised slab, as used by the Windows 98/Office 2000
        // toolbar—not dotted, and not Office 97's taller double handle.
        if (orientation == BarOrientation.Horizontal)
        {
            int x = bounds.Left + Math.Max(1, Dp(2));
            var slab = new Rectangle(x, bounds.Top + Dp(3), Math.Max(1, Dp(2)),
                Math.Max(2, bounds.Height - Dp(6)));
            DrawBevel(g, slab, sunken: false, PopupConnectionEdge.None);
        }
        else
        {
            int y = bounds.Top + Math.Max(1, Dp(2));
            var slab = new Rectangle(bounds.Left + Dp(3), y,
                Math.Max(2, bounds.Width - Dp(6)), Math.Max(1, Dp(2)));
            DrawBevel(g, slab, sunken: false, PopupConnectionEdge.None);
        }
    }

    public override void DrawButton(Graphics g, Rectangle bounds, RenderState state,
        BarOrientation orientation)
    {
        if ((state & RenderState.Disabled) != 0 || state == RenderState.Normal)
            return;

        bool sunken = (state & (RenderState.Pressed | RenderState.Checked)) != 0;
        DrawClassicButton(g, bounds, sunken, PopupConnectionEdge.None,
            checkedFill: (state & RenderState.Checked) != 0);
    }

    internal override void DrawConnectedButton(Graphics g, Rectangle bounds,
        RenderState state, BarOrientation orientation, PopupConnectionEdge connectionEdge)
    {
        if ((state & RenderState.Disabled) != 0 || state == RenderState.Normal)
            return;
        bool sunken = (state & (RenderState.Pressed | RenderState.Checked)) != 0;
        DrawClassicButton(g, bounds, sunken, connectionEdge,
            checkedFill: (state & RenderState.Checked) != 0);
    }

    internal override void DrawOpenMenuButton(Graphics g, Rectangle bounds,
        BarOrientation orientation, PopupConnectionEdge connectionEdge)
        => DrawClassicButton(g, bounds, sunken: true, connectionEdge, checkedFill: false);

    private void DrawClassicButton(Graphics g, Rectangle bounds, bool sunken,
        PopupConnectionEdge connectionEdge, bool checkedFill)
    {
        int inset = Math.Max(1, Dp(1));
        bounds = Rectangle.Inflate(bounds, -inset, -inset);
        if (bounds.Width <= 1 || bounds.Height <= 1)
            return;

        if (checkedFill)
        {
            using var fill = new HatchBrush(HatchStyle.Percent50,
                Colors.GripperLight, Colors.BarGradientBegin);
            g.FillRectangle(fill, bounds);
        }
        else
        {
            using var fill = new SolidBrush(Colors.BarGradientBegin);
            g.FillRectangle(fill, bounds);
        }
        DrawBevel(g, new Rectangle(bounds.X, bounds.Y, bounds.Width - 1, bounds.Height - 1),
            sunken, connectionEdge);
    }

    internal override void DrawComboBoxChrome(Graphics g, Rectangle bounds,
        Rectangle arrowBounds, RenderState state, Color fieldBackground)
    {
        using (var field = new SolidBrush(fieldBackground))
            g.FillRectangle(field, bounds);

        // Classic editable fields are permanently sunken.
        DrawBevel(g, new Rectangle(bounds.X, bounds.Y, bounds.Width - 1, bounds.Height - 1),
            sunken: true, PopupConnectionEdge.None);

        // The arrow is a real Win32 push button: raised at rest/hover and
        // sunken while clicked or while its list remains open.
        var arrowButton = Rectangle.Inflate(arrowBounds, -Dp(1), -Dp(1));
        using (var fill = new SolidBrush(Colors.BarGradientBegin))
            g.FillRectangle(fill, arrowButton);
        DrawBevel(g, new Rectangle(arrowButton.X, arrowButton.Y,
            Math.Max(1, arrowButton.Width - 1), Math.Max(1, arrowButton.Height - 1)),
            sunken: state == RenderState.Pressed, PopupConnectionEdge.None);
    }

    private void DrawBevel(Graphics g, Rectangle r, bool sunken,
        PopupConnectionEdge connectionEdge)
    {
        if (r.Width <= 0 || r.Height <= 0)
            return;

        Color leading = sunken ? Colors.GripperDark : Colors.GripperLight;
        Color trailing = sunken ? Colors.GripperLight : Colors.GripperDark;
        using var lead = new Pen(leading);
        using var trail = new Pen(trailing);

        if (connectionEdge != PopupConnectionEdge.Top)
            g.DrawLine(lead, r.Left, r.Top, r.Right, r.Top);
        if (connectionEdge != PopupConnectionEdge.Left)
            g.DrawLine(lead, r.Left, r.Bottom, r.Left, r.Top);
        if (connectionEdge != PopupConnectionEdge.Right)
            g.DrawLine(trail, r.Right, r.Top, r.Right, r.Bottom);
        if (connectionEdge != PopupConnectionEdge.Bottom)
            g.DrawLine(trail, r.Right, r.Bottom, r.Left, r.Bottom);
    }

    public override void DrawChevron(Graphics g, Rectangle bounds, Rectangle barBounds,
        BarOrientation orientation, RenderState state)
        => DrawChevron(g, bounds, barBounds, orientation, state, hasOverflowItems: true);

    internal override void DrawChevron(Graphics g, Rectangle bounds, Rectangle barBounds,
        BarOrientation orientation, RenderState state, bool hasOverflowItems)
    {
        base.DrawChevron(g, bounds, barBounds, orientation, state, hasOverflowItems);

        if ((state & (RenderState.Hot | RenderState.Pressed)) != 0)
        {
            var frame = new Rectangle(bounds.X, bounds.Y,
                Math.Max(1, bounds.Width - 1), Math.Max(1, bounds.Height - 1));
            DrawBevel(g, frame, sunken: (state & RenderState.Pressed) != 0,
                PopupConnectionEdge.None);
        }

        // Restore only the toolbar's OUTER bevel after the nub fill. There is no
        // leading divider: the options button remains part of the same slab.
        using var light = new Pen(Colors.GripperLight);
        using var dark = new Pen(Colors.GripperDark);
        int right = barBounds.Right - 1;
        int bottom = barBounds.Bottom - 1;
        if (orientation == BarOrientation.Horizontal)
        {
            g.DrawLine(light, bounds.Left, barBounds.Top, right, barBounds.Top);
            g.DrawLine(dark, right, barBounds.Top, right, bottom);
            g.DrawLine(dark, right, bottom, bounds.Left, bottom);
        }
        else
        {
            g.DrawLine(light, barBounds.Left, bounds.Top, barBounds.Left, bottom);
            g.DrawLine(dark, right, bounds.Top, right, bottom);
            g.DrawLine(dark, right, bottom, barBounds.Left, bottom);
        }
    }

    public override void DrawMenuBackground(Graphics g, Rectangle bounds)
    {
        using (var fill = new SolidBrush(Colors.MenuBackground))
            g.FillRectangle(fill, bounds);

        // Classic popup slab: light top/left edges and dark bottom/right edges.
        // Unlike XP+ this is an independent raised surface, not a flat outlined
        // window connected to its owner button.
        int right = bounds.Right - 1;
        int bottom = bounds.Bottom - 1;
        using var light = new Pen(Colors.SeparatorLight);
        using var dark = new Pen(Colors.MenuBorder);
        g.DrawLine(light, bounds.Left, bounds.Top, right, bounds.Top);
        g.DrawLine(light, bounds.Left, bottom, bounds.Left, bounds.Top);
        g.DrawLine(dark, right, bounds.Top, right, bottom);
        g.DrawLine(dark, right, bottom, bounds.Left, bottom);
    }

    public override void DrawImageMargin(Graphics g, Rectangle bounds)
    {
        using var fill = new SolidBrush(Colors.MenuBackground);
        g.FillRectangle(fill, bounds);
    }

    public override void DrawMenuItemBackground(Graphics g, Rectangle bounds, RenderState state)
    {
        if ((state & (RenderState.Hot | RenderState.Pressed)) == 0)
            return;
        using var fill = new SolidBrush(Colors.MenuItemSelectedBegin);
        g.FillRectangle(fill, bounds);
    }

}
