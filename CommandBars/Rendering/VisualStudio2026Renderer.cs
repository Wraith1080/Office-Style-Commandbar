using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;
using CommandBars.Model;

namespace CommandBars.Rendering;

/// <summary>Neutral light palette inspired by Visual Studio 2026.</summary>
public sealed class VisualStudio2026ColorTable : CommandBarColorTable
{
    private static Color Gray(int value) => Color.FromArgb(value, value, value);
    public Color Accent => Color.FromArgb(98, 76, 182);
    public override Color BandGradientBegin => Gray(238);
    public override Color BandGradientEnd => BandGradientBegin;
    public override Color RaisedBorder => BandGradientBegin;
    public override Color BarGradientBegin => Gray(248);
    public override Color BarGradientMiddle => BarGradientBegin;
    public override Color BarGradientEnd => BarGradientBegin;
    public override Color MenuBarGradientBegin => BandGradientBegin;
    public override Color MenuBarGradientEnd => BandGradientBegin;
    public override Color BarBorder => Gray(222);
    public override Color ChevronGradientBegin => BarGradientBegin;
    public override Color ChevronGradientEnd => BarGradientBegin;
    public override Color DropPreview => Accent;
    public override Color ButtonHotBegin => Gray(230);
    public override Color ButtonHotEnd => ButtonHotBegin;
    public override Color ButtonHotBorder => Gray(210);
    public override Color ButtonPressedBegin => Gray(216);
    public override Color ButtonPressedEnd => ButtonPressedBegin;
    public override Color ButtonPressedBorder => Gray(190);
    public override Color ButtonCheckedBegin => Gray(232);
    public override Color ButtonCheckedEnd => ButtonCheckedBegin;
    public override Color ButtonCheckedBorder => Accent;
    public override Color MenuOpenBegin => ButtonHotBegin;
    public override Color MenuOpenEnd => MenuOpenBegin;
    public override Color MenuOpenBorder => BarBorder;
    public override Color SeparatorDark => Gray(225);
    public override Color SeparatorLight => SeparatorDark;
    public override Color GripperDark => Gray(226);
    public override Color GripperLight => GripperDark;
    public override Color Text => Gray(32);
    public override Color DisabledText => Gray(164);
    public override Color MenuBackground => Gray(249);
    public override Color MenuBorder => Gray(220);
    public override Color ImageMarginBegin => MenuBackground;
    public override Color ImageMarginEnd => MenuBackground;
    public override Color MenuItemSelectedBegin => Gray(235);
    public override Color MenuItemSelectedEnd => MenuItemSelectedBegin;
    public override Color MenuItemSelectedBorder => MenuItemSelectedBegin;
    public override Color MenuText => Text;
    public override Color DisabledMenuText => DisabledText;
}

/// <summary>Flat, rounded command bars and menus inspired by Visual Studio 2026 Fluent.</summary>
public sealed class VisualStudio2026Renderer : Office2003Renderer
{
    public override CommandBarColorTable Colors { get; } = new VisualStudio2026ColorTable();
    private Color Accent => ((VisualStudio2026ColorTable)Colors).Accent;
    internal override bool ConnectPopupOwners => false;
    internal override bool UsesFluentMenuChrome => true;
    internal override int MenuRowPadding => 12;
    internal override int SubmenuOverlap => 4;
    public override int ChevronExtent => Dp(22);

    private void Surface(Graphics g, Rectangle bounds, Color fill, Color? border = null, int radius = 4)
    {
        bounds.Width--;
        bounds.Height--;
        if (bounds.Width <= 0 || bounds.Height <= 0) return;
        using var path = RoundedRect(bounds, Dp(radius));
        var previous = g.SmoothingMode;
        g.SmoothingMode = SmoothingMode.AntiAlias;
        using (var brush = new SolidBrush(fill)) g.FillPath(brush, path);
        if (border.HasValue)
        {
            using var pen = new Pen(border.Value);
            g.DrawPath(pen, path);
        }
        g.SmoothingMode = previous;
    }

    public override void DrawBand(Graphics g, Rectangle bounds, BarOrientation orientation)
    {
        using var brush = new SolidBrush(Colors.BandGradientBegin);
        g.FillRectangle(brush, bounds);
    }

    public override void DrawBarBackground(Graphics g, Rectangle bounds, CommandBarType barType,
        BarOrientation orientation, bool rounded, int bandOffset, int bandExtent)
    {
        DrawBand(g, bounds, orientation);
        if (rounded) Surface(g, bounds, Colors.BarGradientBegin, Colors.BarBorder);
    }

    public override void DrawGripper(Graphics g, Rectangle bounds, BarOrientation orientation)
        => DrawGripper(g, bounds, orientation, false);

    internal override void DrawGripper(Graphics g, Rectangle bounds, BarOrientation orientation, bool hot)
    {
        var grip = orientation == BarOrientation.Horizontal
            ? new Rectangle(bounds.X + Dp(1), bounds.Y + Dp(2), Dp(4), bounds.Height - Dp(4))
            : new Rectangle(bounds.X + Dp(2), bounds.Y + Dp(1), bounds.Width - Dp(4), Dp(4));
        Surface(g, grip, hot ? Accent : Colors.GripperDark, radius: 1);
    }

    public override void DrawButton(Graphics g, Rectangle bounds, RenderState state, BarOrientation orientation)
    {
        if ((state & RenderState.Disabled) != 0) return;
        if ((state & (RenderState.Hot | RenderState.Pressed | RenderState.Checked)) == 0) return;
        var fill = (state & RenderState.Pressed) != 0 ? Colors.ButtonPressedBegin
            : (state & RenderState.Hot) != 0 ? Colors.ButtonHotBegin : Colors.ButtonCheckedBegin;
        Surface(g, bounds, fill, (state & RenderState.Checked) != 0 ? Accent : null);
    }

    internal override void DrawConnectedButton(Graphics g, Rectangle bounds, RenderState state,
        BarOrientation orientation, PopupConnectionEdge connectionEdge) => DrawButton(g, bounds, state, orientation);

    internal override void DrawOpenMenuButton(Graphics g, Rectangle bounds,
        BarOrientation orientation, PopupConnectionEdge connectionEdge) => DrawButton(g, bounds, RenderState.Hot, orientation);

    internal override void DrawSplitButton(Graphics g, Rectangle bounds, Rectangle buttonBounds,
        Rectangle arrowBounds, RenderState buttonState, RenderState arrowState, BarOrientation orientation)
    {
        var combined = buttonState | arrowState;
        if ((combined & RenderState.Disabled) != 0) return;
        if ((combined & (RenderState.Hot | RenderState.Pressed)) == 0)
        {
            DrawButton(g, bounds, combined, orientation);
            return;
        }
        Surface(g, bounds, Colors.MenuItemSelectedBegin);
        var saved = g.Save();
        using (var path = RoundedRect(new Rectangle(bounds.X, bounds.Y, bounds.Width - 1, bounds.Height - 1), Dp(4)))
        {
            g.SetClip(path, CombineMode.Intersect);
            FillPart(buttonBounds, buttonState);
            FillPart(arrowBounds, arrowState);
        }
        g.Restore(saved);
        using var pen = new Pen(Colors.BarBorder);
        if (orientation == BarOrientation.Horizontal)
            g.DrawLine(pen, arrowBounds.Left, bounds.Top + Dp(4), arrowBounds.Left, bounds.Bottom - Dp(4));
        else
            g.DrawLine(pen, bounds.Left + Dp(4), arrowBounds.Top, bounds.Right - Dp(4), arrowBounds.Top);

        void FillPart(Rectangle part, RenderState state)
        {
            if ((state & (RenderState.Hot | RenderState.Pressed)) == 0) return;
            using var brush = new SolidBrush((state & RenderState.Pressed) != 0 ? Colors.ButtonPressedBegin : Colors.ButtonHotBegin);
            g.FillRectangle(brush, part);
        }
    }

    internal override void DrawComboBoxChrome(Graphics g, Rectangle bounds, Rectangle arrowBounds,
        RenderState state, Color fieldBackground)
        => Surface(g, bounds, (state & RenderState.Pressed) != 0 ? Colors.ButtonHotBegin : Colors.BarGradientBegin,
            (state & RenderState.Hot) != 0 ? Colors.ButtonHotBorder : Colors.BarBorder);

    public override void DrawSeparator(Graphics g, Rectangle bounds, BarOrientation orientation)
    {
        using var pen = new Pen(Colors.SeparatorDark);
        if (orientation == BarOrientation.Horizontal)
            g.DrawLine(pen, bounds.X + bounds.Width / 2, bounds.Top + Dp(4), bounds.X + bounds.Width / 2, bounds.Bottom - Dp(4));
        else
            g.DrawLine(pen, bounds.Left + Dp(3), bounds.Y + bounds.Height / 2, bounds.Right - Dp(3), bounds.Y + bounds.Height / 2);
    }

    public override void DrawDropDownArrow(Graphics g, Rectangle bounds, RenderState state)
        => DrawArrow(g, bounds, state, false);

    internal override bool TryDrawSubmenuArrow(Graphics g, Rectangle bounds, RenderState state)
    {
        DrawArrow(g, bounds, state, true);
        return true;
    }

    internal void DrawArrow(Graphics g, Rectangle bounds, RenderState state, bool right)
    {
        int x = bounds.X + bounds.Width / 2, y = bounds.Y + bounds.Height / 2;
        using var pen = new Pen((state & RenderState.Disabled) != 0 ? Colors.DisabledText : Colors.Text, Math.Max(1, Scale));
        var previous = g.SmoothingMode;
        g.SmoothingMode = SmoothingMode.AntiAlias;
        g.DrawLines(pen, right
            ? new[] { new Point(x - Dp(2), y - Dp(4)), new Point(x + Dp(2), y), new Point(x - Dp(2), y + Dp(4)) }
            : new[] { new Point(x - Dp(4), y - Dp(2)), new Point(x, y + Dp(2)), new Point(x + Dp(4), y - Dp(2)) });
        g.SmoothingMode = previous;
    }

    public override void DrawChevron(Graphics g, Rectangle bounds, Rectangle barBounds, BarOrientation orientation, RenderState state)
    {
        DrawButton(g, bounds, state, orientation);
        using var brush = new SolidBrush(Colors.Text);
        for (int i = -1; i <= 1; i++)
        {
            int x = bounds.X + bounds.Width / 2 - Dp(1), y = bounds.Y + bounds.Height / 2 - Dp(1);
            if (orientation == BarOrientation.Horizontal) x += i * Dp(4); else y += i * Dp(4);
            g.FillEllipse(brush, x, y, Dp(2), Dp(2));
        }
    }

    internal override void DrawChevron(Graphics g, Rectangle bounds, Rectangle barBounds,
        BarOrientation orientation, RenderState state, bool hasOverflowItems)
        => DrawChevron(g, bounds, barBounds, orientation, state);

    public override void DrawMenuBackground(Graphics g, Rectangle bounds)
    {
        using (var brush = new SolidBrush(Colors.MenuBackground)) g.FillRectangle(brush, bounds);
        Surface(g, bounds, Colors.MenuBackground, Colors.MenuBorder, 7);
    }

    internal override Region? CreatePopupRegion(Rectangle bounds)
    {
        using var path = RoundedRect(bounds, Dp(7));
        return new Region(path);
    }

    public override void DrawImageMargin(Graphics g, Rectangle bounds) { }

    public override void DrawMenuItemBackground(Graphics g, Rectangle bounds, RenderState state)
    {
        if ((state & (RenderState.Hot | RenderState.Pressed)) != 0)
            Surface(g, bounds, Colors.MenuItemSelectedBegin);
    }

    internal override void DrawComboSelection(Graphics g, Rectangle bounds, bool selected, bool hot)
    {
        if (hot || selected) Surface(g, bounds, Colors.MenuItemSelectedBegin);
        if (selected)
            Surface(g, new Rectangle(bounds.Left + Dp(3), bounds.Top + Dp(6), Dp(4), bounds.Height - Dp(12)), Accent, radius: 1);
    }

    public override void DrawMenuCheck(Graphics g, Rectangle bounds, RenderState state)
    {
        int x = bounds.X + bounds.Width / 2, y = bounds.Y + bounds.Height / 2;
        using var pen = new Pen((state & RenderState.Disabled) != 0 ? Colors.DisabledText : Colors.Text, Math.Max(1, Scale));
        var previous = g.SmoothingMode;
        g.SmoothingMode = SmoothingMode.AntiAlias;
        g.DrawLines(pen, new[] { new Point(x - Dp(5), y), new Point(x - Dp(2), y + Dp(3)), new Point(x + Dp(5), y - Dp(4)) });
        g.SmoothingMode = previous;
    }

    internal override void DrawMenuRadio(Graphics g, Rectangle bounds, RenderState state)
    {
        using var brush = new SolidBrush((state & RenderState.Disabled) != 0 ? Colors.DisabledText : Colors.Text);
        var previous = g.SmoothingMode;
        g.SmoothingMode = SmoothingMode.AntiAlias;
        g.FillEllipse(brush, bounds.X + bounds.Width / 2 - Dp(2), bounds.Y + bounds.Height / 2 - Dp(2), Dp(4), Dp(4));
        g.SmoothingMode = previous;
    }

    internal override void DrawMenuItemText(Graphics g, string text, Font font, Rectangle bounds, RenderState state, TextFormatFlags flags)
    {
        Color color = (state & RenderState.Disabled) != 0 ? Colors.DisabledText
            : (flags & TextFormatFlags.Right) != 0 ? Color.FromArgb(100, 100, 100) : Colors.Text;
        TextRenderer.DrawText(g, text, font, bounds, color, flags);
    }
}
