using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Windows.Forms;
using CommandBars.Controls;
using CommandBars.Model;

namespace CommandBars.Rendering;

/// <summary>
/// Vista-inspired aurora glass: reflective teal bars, illuminated button edges,
/// and pale blue menus. Painted glass does not require desktop transparency.
/// </summary>
public sealed class VistaAuroraRenderer : Office2003Renderer
{
    public override CommandBarColorTable Colors { get; } = new VistaAuroraColorTable();
    private CommandBarDialogColorTable? _vistaDialogColors;
    public override CommandBarDialogColorTable DialogColors
        => _vistaDialogColors ??= new VistaAuroraDialogColorTable(Colors);
    internal override bool ConnectPopupOwners => false;
    public override bool PopupDropShadow => true;
    private const int SelectionCornerRadius = 2;
    public override int PopupCornerRadius => 3;
    public override int FloatingCornerRadius => 4;

    internal override Region? CreateFloatingWindowRegion(Rectangle bounds)
        => OperatingSystem.IsWindowsVersionAtLeast(10, 0, 22000)
            ? null : RoundedSurface.CreateRegion(bounds, FloatingCornerRadius * Scale);

    internal override Region? CreatePopupRegion(Rectangle bounds)
        // DWM's small-corner preset is still too round for this theme. Use the
        // explicit region on Windows 11 too, so the actual window matches the paint.
        => RoundedSurface.CreateRegion(bounds, PopupCornerRadius * Scale);

    public override void DrawMenuBackground(Graphics g, Rectangle bounds)
    {
        using var back = new SolidBrush(Colors.MenuBackground);
        g.FillRectangle(back, bounds);
        RoundedSurface.Draw(g, bounds, PopupCornerRadius * Scale, Colors.MenuBackground, Colors.MenuBorder);
    }

    public override Padding GetComboPopupInsets(float scale)
        => new(Math.Max(1, (int)Math.Round(4 * scale)));
    public override void DrawComboPopupBackground(Graphics g, Rectangle bounds) => DrawMenuBackground(g, bounds);
    public override void DrawComboPopupBorder(Graphics g, Rectangle bounds) { }

    public override void DrawBarBackground(Graphics g, Rectangle bounds, CommandBarType barType,
        BarOrientation orientation, bool rounded, int bandOffset, int bandExtent)
        => DrawGlassBackground(g, bounds, orientation, rounded || barType == CommandBarType.MenuBar,
            bandOffset, bandExtent, true);

    private void DrawGlassBackground(Graphics g, Rectangle bounds, BarOrientation orientation,
        bool rounded, int bandOffset, int bandExtent, bool drawFrame)
    {
        if (bounds.Width <= 0 || bounds.Height <= 0) return;
        var saved = g.Save();
        try
        {
            g.SetClip(bounds, CombineMode.Intersect);
            var outline = new Rectangle(bounds.X, bounds.Y, bounds.Width - 1, bounds.Height - 1);
            if (outline.Width <= 0 || outline.Height <= 0) return;
            using var surface = RoundedRect(outline, rounded ? ChunkRadius : 0);
            if (rounded)
            {
                // Leave the host's continuous band visible outside the curved corners.
                var band = orientation == BarOrientation.Vertical
                    ? new Rectangle(bounds.X, bounds.Y - bandOffset, bounds.Width, Math.Max(1, bandExtent))
                    : new Rectangle(bounds.X - bandOffset, bounds.Y, Math.Max(1, bandExtent), bounds.Height);
                DrawBand(g, band, orientation);
                g.SmoothingMode = SmoothingMode.AntiAlias;
            }
            // The narrow discontinuity is the reflected horizon of the glass.
            bool vertical = orientation == BarOrientation.Vertical;
            using var glass = new LinearGradientBrush(bounds, Colors.BarGradientBegin,
                Colors.BarGradientEnd, vertical ? LinearGradientMode.Horizontal : LinearGradientMode.Vertical);
            glass.InterpolationColors = new ColorBlend
            {
                Colors = new[] { Colors.BarGradientBegin, Colors.BarGradientMiddle,
                    Color.FromArgb(18, 65, 86), Colors.BarGradientEnd },
                Positions = new[] { 0f, .44f, .46f, 1f },
            };
            g.FillPath(glass, surface);

            // A broad teal glow follows band coordinates, so adjacent bars share it.
            var auroraBounds = vertical
                ? new Rectangle(bounds.X, bounds.Y - bandOffset, bounds.Width, Math.Max(1, bandExtent))
                : new Rectangle(bounds.X - bandOffset, bounds.Y, Math.Max(1, bandExtent), bounds.Height);
            using var aurora = new LinearGradientBrush(auroraBounds, Color.Transparent, Color.Transparent,
                vertical ? LinearGradientMode.Vertical : LinearGradientMode.Horizontal);
            aurora.InterpolationColors = new ColorBlend
            {
                Colors = new[] { Color.FromArgb(0, 60, 220, 176), Color.FromArgb(28, 60, 220, 176),
                    Color.FromArgb(0, 60, 220, 176), Color.FromArgb(12, 80, 170, 224) },
                Positions = new[] { 0f, .32f, .72f, 1f },
            };
            g.FillPath(aurora, surface);
            if (!drawFrame) return;
            DrawBarOutline(g, bounds, rounded);
            using var reflection = new Pen(Color.FromArgb(154, 209, 219));
            int inset = Math.Max(1, Dp(1));
            int endInset = rounded ? Math.Max(inset, ChunkRadius) : inset;
            if (bounds.Width > endInset * 2 && bounds.Height > endInset * 2)
            {
                if (vertical)
                    g.DrawLine(reflection, bounds.Left + inset, bounds.Top + endInset, bounds.Left + inset, bounds.Bottom - endInset - 1);
                else
                    g.DrawLine(reflection, bounds.Left + endInset, bounds.Top + inset, bounds.Right - endInset - 1, bounds.Top + inset);
            }
        }
        finally { g.Restore(saved); }
    }

    private void DrawBarOutline(Graphics g, Rectangle bounds, bool rounded)
    {
        var outline = new Rectangle(bounds.X, bounds.Y, bounds.Width - 1, bounds.Height - 1);
        if (outline.Width <= 0 || outline.Height <= 0) return;
        using var path = RoundedRect(outline, rounded ? ChunkRadius : 0);
        using var edge = new Pen(Colors.BarBorder);
        g.DrawPath(edge, path);
        if (!rounded) return;

        int inset = Math.Max(1, Dp(1));
        var inner = Rectangle.Inflate(outline, -inset, -inset);
        if (inner.Width <= 0 || inner.Height <= 0) return;
        using var innerPath = RoundedRect(inner, Math.Max(0, ChunkRadius - inset));
        using var light = new Pen(Color.FromArgb(105, 171, 187));
        g.DrawPath(light, innerPath);
    }

    public override void DrawChevron(Graphics g, Rectangle bounds, Rectangle barBounds,
        BarOrientation orientation, RenderState state)
        => DrawAuroraChevron(g, bounds, barBounds, orientation, state, true);

    internal override void DrawChevron(Graphics g, Rectangle bounds, Rectangle barBounds,
        BarOrientation orientation, RenderState state, bool hasOverflowItems)
        => DrawAuroraChevron(g, bounds, barBounds, orientation, state, hasOverflowItems);

    private void DrawAuroraChevron(Graphics g, Rectangle bounds, Rectangle barBounds,
        BarOrientation orientation, RenderState state, bool hasOverflowItems)
    {
        // The nub is part of the toolbar's frame. Paint inside that frame so
        // normal, hover and pressed fills cannot erase its border or reflection.
        int inset = Math.Max(1, Dp(1)) + 1;
        var interior = Rectangle.Inflate(barBounds, -inset, -inset);
        if (interior.Width <= 0 || interior.Height <= 0) return;
        var saved = g.Save();
        try
        {
            using var path = RoundedRect(new Rectangle(interior.X, interior.Y,
                interior.Width - 1, interior.Height - 1), Math.Max(0, ChunkRadius - inset));
            g.SetClip(path, CombineMode.Intersect);
            base.DrawChevron(g, bounds, barBounds, orientation, state, hasOverflowItems);
            using var divider = new Pen(Color.FromArgb(105, 171, 187));
            if (orientation == BarOrientation.Vertical)
                g.DrawLine(divider, interior.Left, bounds.Top, interior.Right, bounds.Top);
            else
                g.DrawLine(divider, bounds.Left, interior.Top, bounds.Left, interior.Bottom);
        }
        finally { g.Restore(saved); }
    }

    public override void DrawButton(Graphics g, Rectangle bounds, RenderState state, BarOrientation orientation)
    {
        bool disabled = (state & RenderState.Disabled) != 0;
        bool check = (state & RenderState.Checked) != 0;
        if (disabled && !check || (state & (RenderState.Hot | RenderState.Pressed | RenderState.Checked)) == 0) return;
        bool pressed = !disabled && (state & RenderState.Pressed) != 0;
        bool hot = !disabled && (state & RenderState.Hot) != 0;
        PaintGlassButton(g, ButtonSurface(bounds, orientation), orientation,
            pressed ? Colors.ButtonPressedBegin : hot ? Colors.ButtonHotBegin : Colors.ButtonCheckedBegin,
            pressed ? Colors.ButtonPressedEnd : hot ? Colors.ButtonHotEnd : Colors.ButtonCheckedEnd,
            pressed ? Colors.ButtonPressedBorder : check ? Colors.ButtonCheckedBorder : Colors.ButtonHotBorder,
            !pressed);
    }

    internal override void DrawSplitButton(Graphics g, Rectangle bounds,
        Rectangle buttonBounds, Rectangle arrowBounds, RenderState buttonState,
        RenderState arrowState, BarOrientation orientation)
    {
        // Both halves share the complete inset outline and gradient axis. Clip
        // their states at the hit boundary so only the outside corners round.
        DrawPart(buttonBounds, buttonState);
        DrawPart(arrowBounds, arrowState);
        var combined = buttonState | arrowState;
        if ((combined & RenderState.Disabled) == 0 &&
            (combined & (RenderState.Hot | RenderState.Pressed | RenderState.Checked)) != 0)
            DrawSplitDividerLine(g, ButtonSurface(bounds, orientation), arrowBounds, orientation,
                (arrowState & RenderState.Pressed) != 0 ? Colors.ButtonPressedBorder
                : (arrowState & RenderState.Checked) != 0 ? Colors.ButtonCheckedBorder : Colors.ButtonHotBorder);

        void DrawPart(Rectangle part, RenderState state)
        {
            var saved = g.Save();
            try
            {
                g.SetClip(part, CombineMode.Intersect);
                DrawButton(g, bounds, state, orientation);
            }
            finally { g.Restore(saved); }
        }
    }

    internal override void DrawSplitDivider(Graphics g, Rectangle bounds,
        Rectangle arrowBounds, BarOrientation orientation)
    {
        // Align the single bright idle stroke with the hover/open divider.
        // Structural separators retain their bevel.
        int inset = Math.Max(1, Dp(3));
        using var pen = new Pen(Colors.SeparatorLight);
        if (orientation == BarOrientation.Horizontal && bounds.Height > inset * 2)
            g.DrawLine(pen, arrowBounds.Left, bounds.Top + inset,
                arrowBounds.Left, bounds.Bottom - inset - 1);
        else if (orientation == BarOrientation.Vertical && bounds.Width > inset * 2)
            g.DrawLine(pen, bounds.Left + inset, arrowBounds.Top,
                bounds.Right - inset - 1, arrowBounds.Top);
    }

    internal override void DrawOpenSplitDivider(Graphics g, Rectangle bounds,
        Rectangle arrowBounds, BarOrientation orientation)
        => DrawSplitDividerLine(g, ButtonSurface(bounds, orientation), arrowBounds,
            orientation, Colors.MenuOpenBorder);

    private Rectangle ButtonSurface(Rectangle bounds, BarOrientation orientation)
        => Rectangle.Inflate(bounds, -Math.Max(1, Dp(orientation == BarOrientation.Horizontal ? 1 : 2)),
            -Math.Max(1, Dp(orientation == BarOrientation.Horizontal ? 2 : 1)));

    private void PaintGlassButton(Graphics g, Rectangle bounds, BarOrientation orientation,
        Color begin, Color end, Color border, bool sheen)
    {
        if (bounds.Width <= 1 || bounds.Height <= 1) return;
        var saved = g.Save();
        try
        {
            g.SetClip(bounds, CombineMode.Intersect);
            g.SmoothingMode = SmoothingMode.AntiAlias;
            var outline = new Rectangle(bounds.X, bounds.Y, bounds.Width - 1, bounds.Height - 1);
            using var path = RoundedRect(outline, Math.Max(1, Dp(SelectionCornerRadius)));
            var fillState = g.Save();
            g.SetClip(path, CombineMode.Intersect);
            bool vertical = orientation == BarOrientation.Vertical;
            var mode = vertical ? LinearGradientMode.Horizontal : LinearGradientMode.Vertical;
            FillGradient(g, bounds, begin, end, mode);
            if (sheen)
            {
                var reflection = vertical
                    ? new Rectangle(bounds.X, bounds.Y, bounds.Width / 2, bounds.Height)
                    : new Rectangle(bounds.X, bounds.Y, bounds.Width, bounds.Height / 2);
                FillGradient(g, reflection, Color.FromArgb(28, Color.White), Color.FromArgb(6, Color.White), mode);
            }
            g.Restore(fillState);
            using var pen = new Pen(border);
            g.DrawPath(pen, path);
        }
        finally { g.Restore(saved); }
    }

    internal override void DrawConnectedButton(Graphics g, Rectangle bounds, RenderState state,
        BarOrientation orientation, PopupConnectionEdge connectionEdge) => DrawButton(g, bounds, state, orientation);

    internal override void DrawOpenMenuButton(Graphics g, Rectangle bounds,
        BarOrientation orientation, PopupConnectionEdge connectionEdge)
        => PaintGlassButton(g, ButtonSurface(bounds, orientation), orientation,
            Colors.MenuOpenBegin, Colors.MenuOpenEnd, Colors.MenuOpenBorder, false);

    public override void DrawFloatingCaptionCloseButton(Graphics g, Rectangle bounds, bool hot, bool pressed)
    {
        if (hot)
            PaintGlassButton(g, bounds, BarOrientation.Horizontal,
                Colors.ButtonHotEnd, Colors.ButtonHotEnd, Colors.ButtonHotBorder, false);
        FloatingCaptionButtonPainter.DrawCloseGlyph(g, bounds,
            hot ? Colors.Text : FloatingCaptionTextColor);
    }

    internal override void DrawFloatingWindowChrome(Graphics g, Rectangle bounds, Rectangle captionBounds)
    {
        using var back = new SolidBrush(Colors.BandGradientEnd);
        g.FillRectangle(back, bounds);
        RoundedSurface.Draw(g, bounds, FloatingCornerRadius * Scale, Colors.BandGradientEnd, Colors.RaisedBorder);
        DrawGlassBackground(g, captionBounds, BarOrientation.Horizontal, false, 0, captionBounds.Width, false);

        // The hosted content starts at the caption's left inset. Keep a single
        // device-pixel inner line just outside it on all four sides.
        int inset = Math.Max(1, captionBounds.Left - bounds.Left - 1);
        var inner = Rectangle.Inflate(bounds, -inset, -inset);
        if (inner.Width <= 1 || inner.Height <= 1) return;
        var saved = g.Save();
        try
        {
            g.SmoothingMode = SmoothingMode.AntiAlias;
            using var path = RoundedRect(new Rectangle(inner.X, inner.Y, inner.Width - 1, inner.Height - 1),
                Math.Max(1, Dp(FloatingCornerRadius) - inset));
            using var line = new Pen(Color.FromArgb(154, 209, 219));
            g.DrawPath(line, path);
        }
        finally { g.Restore(saved); }
    }

    public override void DrawSeparator(Graphics g, Rectangle bounds, BarOrientation orientation)
    {
        if (bounds.Width <= 0 || bounds.Height <= 0) return;
        var saved = g.Save();
        try
        {
            g.SetClip(bounds, CombineMode.Intersect);
            using var dark = new Pen(Colors.SeparatorDark);
            using var light = new Pen(Colors.SeparatorLight);
            int inset = Math.Max(1, Dp(3)), step = Math.Max(1, Dp(1));
            if (orientation == BarOrientation.Horizontal && bounds.Height > inset * 2)
            {
                int x = bounds.X + Math.Max(0, (bounds.Width - step - 1) / 2);
                g.DrawLine(dark, x, bounds.Top + inset, x, bounds.Bottom - inset - 1);
                g.DrawLine(light, x + step, bounds.Top + inset, x + step, bounds.Bottom - inset - 1);
            }
            else if (orientation == BarOrientation.Vertical && bounds.Width > inset * 2)
            {
                int y = bounds.Y + Math.Max(0, (bounds.Height - step - 1) / 2);
                g.DrawLine(dark, bounds.Left + inset, y, bounds.Right - inset - 1, y);
                g.DrawLine(light, bounds.Left + inset, y + step, bounds.Right - inset - 1, y + step);
            }
        }
        finally { g.Restore(saved); }
    }

    public override void DrawMenuCheck(Graphics g, Rectangle bounds, RenderState state)
    {
        var saved = g.Save();
        try
        {
            g.SetClip(bounds, CombineMode.Intersect);
            g.SmoothingMode = SmoothingMode.AntiAlias;
            int cx = bounds.X + bounds.Width / 2, cy = bounds.Y + bounds.Height / 2, reach = Math.Max(1, Dp(4));
            using var pen = new Pen(MenuGlyphColor(state), Math.Max(1, 2 * Scale));
            g.DrawLines(pen, new[] { new Point(cx - reach, cy), new Point(cx - reach / 3, cy + reach), new Point(cx + reach, cy - reach) });
        }
        finally { g.Restore(saved); }
    }

    internal override void DrawMenuRadio(Graphics g, Rectangle bounds, RenderState state)
    {
        int size = Math.Min(Math.Min(bounds.Width, bounds.Height), Math.Max(1, Dp(6)));
        if (size <= 0) return;
        var saved = g.Save();
        try
        {
            g.SmoothingMode = SmoothingMode.AntiAlias;
            using var brush = new SolidBrush(MenuGlyphColor(state));
            g.FillEllipse(brush, bounds.X + (bounds.Width - size) / 2, bounds.Y + (bounds.Height - size) / 2, size, size);
        }
        finally { g.Restore(saved); }
    }

    internal override void DrawMenuIconFrame(Graphics g, Rectangle bounds, RenderState state)
        => PaintGlassButton(g, bounds, BarOrientation.Horizontal, Colors.MenuItemSelectedBegin,
            Colors.MenuItemSelectedEnd, Colors.MenuItemSelectedBorder, false);

    public override void DrawMenuItemBackground(Graphics g, Rectangle bounds, RenderState state)
    {
        if ((state & (RenderState.Hot | RenderState.Pressed)) != 0)
            PaintGlassButton(g, bounds, BarOrientation.Horizontal, Colors.MenuItemSelectedBegin,
                Colors.MenuItemSelectedEnd, Colors.MenuItemSelectedBorder, false);
    }

    private Color MenuGlyphColor(RenderState state)
        => (state & RenderState.Disabled) != 0 ? Colors.DisabledMenuText : Colors.MenuText;

    public override void DrawToolbarItemImage(Graphics g, Image image, Rectangle bounds, RenderState state)
    {
        if (bounds.Width <= 0 || bounds.Height <= 0) return;
        if ((state & RenderState.Disabled) == 0)
        {
            // Keep application image colors intact. A small alpha-shaped rim makes
            // dark line icons readable on glass; popup images receive no rim.
            using var attributes = new ImageAttributes();
            attributes.SetColorMatrix(new ColorMatrix(new[]
            {
                new[] { 0f, 0f, 0f, 0f, 0f }, new[] { 0f, 0f, 0f, 0f, 0f },
                new[] { 0f, 0f, 0f, 0f, 0f }, new[] { 0f, 0f, 0f, .32f, 0f },
                new[] { .88f, .98f, 1f, 0f, 1f },
            }));
            int offset = Math.Max(1, Dp(1));
            foreach (var delta in new[] { new Point(-offset, 0), new Point(offset, 0), new Point(0, -offset), new Point(0, offset) })
            {
                var rim = bounds;
                rim.Offset(delta);
                g.DrawImage(image, rim, 0, 0, image.Width, image.Height, GraphicsUnit.Pixel, attributes);
            }
        }
        base.DrawItemImage(g, image, bounds, state);
    }

    public override void DrawDropDownArrow(Graphics g, Rectangle bounds, RenderState state)
        => DrawArrow(g, bounds, (state & RenderState.Disabled) != 0 ? Colors.DisabledText : Colors.Text);

    public override void DrawComboBoxText(Graphics g, string text, Font font, Rectangle bounds,
        RenderState state, TextFormatFlags flags)
        => TextRenderer.DrawText(g, text, font, bounds, MenuGlyphColor(state), flags);

    public override void DrawComboBoxArrow(Graphics g, Rectangle bounds, RenderState state)
        => DrawArrow(g, bounds, MenuGlyphColor(state));

    private void DrawArrow(Graphics g, Rectangle bounds, Color color)
    {
        if (bounds.Width <= 0 || bounds.Height <= 0) return;
        var saved = g.Save();
        try
        {
            g.SetClip(bounds, CombineMode.Intersect);
            int cx = bounds.X + bounds.Width / 2, cy = bounds.Y + bounds.Height / 2;
            int reach = Math.Max(1, Dp(3));
            using var brush = new SolidBrush(color);
            g.FillPolygon(brush, new[] { new Point(cx - reach, cy - Dp(1)), new Point(cx + reach, cy - Dp(1)), new Point(cx, cy + Dp(2)) });
        }
        finally { g.Restore(saved); }
    }

    internal override void DrawComboBoxChrome(Graphics g, Rectangle bounds, Rectangle arrowBounds,
        RenderState state, Color fieldBackground)
    {
        if (bounds.Width <= 1 || bounds.Height <= 1) return;
        bool active = (state & RenderState.Disabled) == 0 && (state & (RenderState.Hot | RenderState.Pressed)) != 0;
        bool pressed = (state & RenderState.Pressed) != 0;
        // One rounded surface keeps the arrow's left edge square and gives only
        // its outer right corners the same curve as the field and dropdown.
        RoundedSurface.Draw(g, bounds, PopupCornerRadius * Scale, fieldBackground,
            active ? DialogColors.ButtonHotBorder : Colors.MenuBorder,
            split: arrowBounds.Left - bounds.Left,
            trailingFill: active ? pressed ? DialogColors.ButtonPressedBegin : DialogColors.ButtonHotBegin : fieldBackground,
            trailingFillEnd: active ? pressed ? DialogColors.ButtonPressedEnd : DialogColors.ButtonHotEnd : null);
        if (active)
        {
            using var divider = new Pen(DialogColors.ButtonHotBorder);
            g.DrawLine(divider, arrowBounds.Left, bounds.Top + 1, arrowBounds.Left, bounds.Bottom - 2);
        }
    }
}
