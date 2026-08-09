using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;
using CommandBars.Model;

namespace CommandBars.Rendering;

/// <summary>
/// Renders command bars in the Office 2003 "Luna Blue" style: gradient bars and
/// band, raised toolbar chunks, warm orange hover/pressed states, and the
/// classic popup image margin.
/// </summary>
public class Office2003Renderer : CommandBarRenderer
{
    /// <summary>Corner radius of toolbar chunks (DPI-scaled). XP overrides to 0.</summary>
    protected virtual int ChunkRadius => Dp(3);

    public override CommandBarColorTable Colors { get; } = new Office2003ColorTable();

    public override void DrawBand(Graphics g, Rectangle bounds, BarOrientation orientation)
    {
        if (bounds.Width <= 0 || bounds.Height <= 0)
            return;
        // Gradient along the band's main axis (light at the leading edge, dark
        // at the trailing one). Plain (non-inflated) mapping so a bar's band
        // slice lines up exactly. The host draws the edge separator so it lands
        // on the content-facing side of whichever edge the band occupies.
        var mode = orientation == BarOrientation.Vertical
            ? LinearGradientMode.Vertical
            : LinearGradientMode.Horizontal;
        using var brush = new LinearGradientBrush(
            bounds, Colors.BandGradientBegin, Colors.BandGradientEnd, mode);
        g.FillRectangle(brush, bounds);
    }

    public override void DrawBarBackground(
        Graphics g, Rectangle bounds, CommandBarType barType, BarOrientation orientation,
        bool rounded, int bandOffset, int bandExtent)
    {
        if (bounds.Width <= 0 || bounds.Height <= 0)
            return;

        // Every bar sits on the same continuous band gradient. bandOffset is the
        // bar's position within the band along its main axis, so slices are
        // seamless with the rebar.
        FillBandSlice(g, bounds, orientation, bandOffset, bandExtent);

        if (!rounded)
            return; // menu bar: band slice only — no own gradient, no bottom edge

        // Toolbar chunk: raised rounded rectangle with its own lighter gradient.
        var mode = orientation == BarOrientation.Horizontal
            ? LinearGradientMode.Vertical
            : LinearGradientMode.Horizontal;
        var chunk = new Rectangle(bounds.X, bounds.Y, bounds.Width - 1, bounds.Height - 1);
        if (chunk.Width <= 0 || chunk.Height <= 0)
            return; // too small to draw a chunk (a LinearGradientBrush can't be 0-wide)
        using var path = RoundedRect(chunk, ChunkRadius);
        var previous = g.SmoothingMode;
        g.SmoothingMode = SmoothingMode.AntiAlias;
        using (var brush = new LinearGradientBrush(
            new Rectangle(chunk.X, chunk.Y - 1, chunk.Width, chunk.Height + 2),
            Colors.BarGradientBegin, Colors.BarGradientEnd, mode))
        {
            if (orientation == BarOrientation.Horizontal)
                brush.InterpolationColors = ThreeStop(Colors.BarGradientBegin, Colors.BarGradientMiddle, Colors.BarGradientEnd);
            g.FillPath(brush, path);
        }
        // No outline — the raised gradient itself defines the chunk shape.
        g.SmoothingMode = previous;
    }

    private void FillBandSlice(Graphics g, Rectangle bounds, BarOrientation orientation, int bandOffset, int bandExtent)
    {
        // Gradient spanning the whole band along its main axis; this bar shows
        // the slice at its offset (subtracting bandOffset maps the band's
        // leading edge to zero) so neighboring slices are seamless.
        int extent = Math.Max(1, bandExtent);
        if (orientation == BarOrientation.Vertical)
        {
            using var brush = new LinearGradientBrush(
                new Rectangle(bounds.X, bounds.Y - bandOffset, Math.Max(1, bounds.Width), extent),
                Colors.BandGradientBegin, Colors.BandGradientEnd, LinearGradientMode.Vertical);
            g.FillRectangle(brush, bounds);
        }
        else
        {
            using var brush = new LinearGradientBrush(
                new Rectangle(bounds.X - bandOffset, bounds.Y, extent, Math.Max(1, bounds.Height)),
                Colors.BandGradientBegin, Colors.BandGradientEnd, LinearGradientMode.Horizontal);
            g.FillRectangle(brush, bounds);
        }
    }

    public override void DrawGripper(Graphics g, Rectangle bounds, BarOrientation orientation)
    {
        using var dark = new SolidBrush(Colors.GripperDark);
        using var light = new SolidBrush(Colors.GripperLight);

        if (orientation == BarOrientation.Horizontal)
        {
            int x = bounds.X + 3;
            for (int y = bounds.Y + 4; y <= bounds.Bottom - 6; y += 4)
            {
                g.FillRectangle(light, x + 1, y + 1, 2, 2);
                g.FillRectangle(dark, x, y, 2, 2);
            }
        }
        else
        {
            int y = bounds.Y + 3;
            for (int x = bounds.X + 4; x <= bounds.Right - 6; x += 4)
            {
                g.FillRectangle(light, x + 1, y + 1, 2, 2);
                g.FillRectangle(dark, x, y, 2, 2);
            }
        }
    }

    public override void DrawButton(Graphics g, Rectangle bounds, RenderState state, BarOrientation orientation)
    {
        if (bounds.Width <= 1 || bounds.Height <= 1)
            return;

        Color begin, end, border;
        if ((state & RenderState.Pressed) != 0)
        {
            begin = Colors.ButtonPressedBegin;
            end = Colors.ButtonPressedEnd;
            border = Colors.ButtonPressedBorder;
        }
        else if ((state & RenderState.Checked) != 0)
        {
            begin = Colors.ButtonCheckedBegin;
            end = Colors.ButtonCheckedEnd;
            border = Colors.ButtonCheckedBorder;
            if ((state & RenderState.Hot) != 0)
            {
                begin = Colors.ButtonHotBegin;
                end = Colors.ButtonHotEnd;
            }
        }
        else if ((state & RenderState.Hot) != 0)
        {
            begin = Colors.ButtonHotBegin;
            end = Colors.ButtonHotEnd;
            border = Colors.ButtonHotBorder;
        }
        else
        {
            return; // normal state draws nothing over the bar background
        }

        var fill = new Rectangle(bounds.X, bounds.Y, bounds.Width - 1, bounds.Height - 1);
        var mode = orientation == BarOrientation.Vertical
            ? LinearGradientMode.Horizontal
            : LinearGradientMode.Vertical;
        FillGradient(g, fill, begin, end, mode);
        using var pen = new Pen(border);
        g.DrawRectangle(pen, fill);
    }

    public override void DrawSeparator(Graphics g, Rectangle bounds, BarOrientation orientation)
    {
        using var dark = new Pen(Colors.SeparatorDark);
        using var light = new Pen(Colors.SeparatorLight);

        if (orientation == BarOrientation.Horizontal)
        {
            // Center the two-pixel dark/light pair as a pair. Using Width / 2
            // for the first line biases both lines toward the trailing edge.
            int x = bounds.X + Math.Max(0, (bounds.Width - 2) / 2);
            g.DrawLine(dark, x, bounds.Top + 3, x, bounds.Bottom - 3);
            g.DrawLine(light, x + 1, bounds.Top + 3, x + 1, bounds.Bottom - 3);
        }
        else
        {
            int y = bounds.Y + Math.Max(0, (bounds.Height - 2) / 2);
            g.DrawLine(dark, bounds.Left + 3, y, bounds.Right - 3, y);
            g.DrawLine(light, bounds.Left + 3, y + 1, bounds.Right - 3, y + 1);
        }
    }

    public override void DrawItemText(Graphics g, string text, Font font, Rectangle bounds, RenderState state, TextFormatFlags flags)
    {
        Color color = (state & RenderState.Disabled) != 0 ? Colors.DisabledText : Colors.Text;
        TextRenderer.DrawText(g, text, font, bounds, color, flags);
    }

    public override void DrawItemImage(Graphics g, Image image, Rectangle bounds, RenderState state)
    {
        if ((state & RenderState.Disabled) != 0)
            ControlPaint.DrawImageDisabled(g, image, bounds.X, bounds.Y, Color.Transparent);
        else
            g.DrawImage(image, bounds);
    }

    public override void DrawDropDownArrow(Graphics g, Rectangle bounds, RenderState state)
    {
        Color color = (state & RenderState.Disabled) != 0 ? Colors.DisabledText : Colors.Text;
        int cx = bounds.X + bounds.Width / 2;
        int cy = bounds.Y + bounds.Height / 2;
        Point[] arrow = { new(cx - 3, cy - 1), new(cx + 4, cy - 1), new(cx, cy + 3) };
        FillArrow(g, arrow, color);
    }

    public override void DrawChevron(Graphics g, Rectangle bounds, Rectangle barBounds, BarOrientation orientation, RenderState state)
    {
        // Fill the nub with a path that is square where it meets the items and
        // rounded on the outer edge (right for a horizontal bar, bottom for a
        // vertical one), matching the chunk's corners exactly. Painting the
        // rounded shape directly (rather than clipping a square fill) keeps the
        // antialiased edge aligned with the toolbar.
        var chunk = new Rectangle(barBounds.X, barBounds.Y, barBounds.Width - 1, barBounds.Height - 1);
        bool vertical = orientation == BarOrientation.Vertical;
        var nub = vertical
            ? Rectangle.FromLTRB(chunk.X, bounds.Y, chunk.Right, chunk.Bottom)
            : Rectangle.FromLTRB(bounds.X, chunk.Y, chunk.Right, chunk.Bottom);
        if (nub.Width <= 0 || nub.Height <= 0)
            return; // bar squeezed too small to draw the chevron nub

        Color begin, end;
        if ((state & RenderState.Pressed) != 0)
        {
            begin = Colors.ButtonPressedBegin;
            end = Colors.ButtonPressedEnd;
        }
        else if ((state & RenderState.Hot) != 0)
        {
            begin = Colors.ButtonHotBegin;
            end = Colors.ButtonHotEnd;
        }
        else
        {
            begin = Colors.ChevronGradientBegin;
            end = Colors.ChevronGradientEnd;
        }

        var previous = g.SmoothingMode;
        g.SmoothingMode = SmoothingMode.AntiAlias;
        using (var path = vertical ? RoundedBottomRect(nub, ChunkRadius) : RoundedRightRect(nub, ChunkRadius))
        using (var brush = new LinearGradientBrush(
            new Rectangle(nub.X, nub.Y - 1, Math.Max(1, nub.Width), nub.Height + 2),
            begin, end, vertical ? LinearGradientMode.Horizontal : LinearGradientMode.Vertical))
        {
            g.FillPath(brush, path);
        }

        // Double-chevron ("more") glyph, centered: pointing down on a horizontal
        // bar, pointing right on a vertical one (toward where the items continue).
        Color color = (state & RenderState.Disabled) != 0 ? Colors.DisabledText : Colors.Text;
        int cx = bounds.X + (bounds.Width / 2);
        int cy = bounds.Y + (bounds.Height / 2);
        using (var pen = new Pen(color, 1.3f))
        {
            if (vertical)
            {
                g.DrawLines(pen, new[] { new Point(cx - 3, cy - 3), new Point(cx - 1, cy), new Point(cx - 3, cy + 3) });
                g.DrawLines(pen, new[] { new Point(cx + 1, cy - 3), new Point(cx + 3, cy), new Point(cx + 1, cy + 3) });
            }
            else
            {
                g.DrawLines(pen, new[] { new Point(cx - 3, cy - 3), new Point(cx, cy - 1), new Point(cx + 3, cy - 3) });
                g.DrawLines(pen, new[] { new Point(cx - 3, cy + 1), new Point(cx, cy + 3), new Point(cx + 3, cy + 1) });
            }
        }
        g.SmoothingMode = previous;
    }

    public override void DrawMenuBackground(Graphics g, Rectangle bounds)
    {
        using (var back = new SolidBrush(Colors.MenuBackground))
            g.FillRectangle(back, bounds);
        using var pen = new Pen(Colors.MenuBorder);
        g.DrawRectangle(pen, new Rectangle(bounds.X, bounds.Y, bounds.Width - 1, bounds.Height - 1));
    }

    public override void DrawImageMargin(Graphics g, Rectangle bounds)
    {
        if (bounds.Width <= 0 || bounds.Height <= 0)
            return;
        FillGradient(g, bounds, Colors.ImageMarginBegin, Colors.ImageMarginEnd, LinearGradientMode.Horizontal);
    }

    public override void DrawMenuItemBackground(Graphics g, Rectangle bounds, RenderState state)
    {
        if ((state & RenderState.Hot) == 0 && (state & RenderState.Pressed) == 0)
            return;
        if (bounds.Width <= 1 || bounds.Height <= 1)
            return;

        var fill = new Rectangle(bounds.X, bounds.Y, bounds.Width - 1, bounds.Height - 1);
        FillGradient(g, fill, Colors.MenuItemSelectedBegin, Colors.MenuItemSelectedEnd, LinearGradientMode.Vertical);
        using var pen = new Pen(Colors.MenuItemSelectedBorder);
        g.DrawRectangle(pen, fill);
    }

    public override void DrawMenuCheck(Graphics g, Rectangle bounds, RenderState state)
    {
        Color color = (state & RenderState.Disabled) != 0 ? Colors.DisabledText : Colors.Text;
        var previous = g.SmoothingMode;
        g.SmoothingMode = SmoothingMode.AntiAlias;
        int cx = bounds.X + (bounds.Width / 2);
        int cy = bounds.Y + (bounds.Height / 2);
        int r = Math.Max(4, Dp(4)); // check reach — larger and DPI-scaled
        using var pen = new Pen(color, Math.Max(1.8f, 2f * Scale));
        g.DrawLines(pen, new[]
        {
            new Point(cx - r, cy),
            new Point(cx - (r / 3), cy + r),
            new Point(cx + r + 1, cy - r - 1),
        });
        g.SmoothingMode = previous;
    }

    // --- helpers -----------------------------------------------------------

    private static void FillArrow(Graphics g, Point[] arrow, Color color)
    {
        var previous = g.SmoothingMode;
        g.SmoothingMode = SmoothingMode.None;
        using (var brush = new SolidBrush(color))
            g.FillPolygon(brush, arrow);
        g.SmoothingMode = previous;
    }

    protected static void FillGradient(Graphics g, Rectangle r, Color begin, Color end, LinearGradientMode mode)
    {
        if (r.Width <= 0 || r.Height <= 0)
            return;
        // Inflate by 1 along the gradient axis to avoid GDI+ edge banding.
        var brushRect = mode == LinearGradientMode.Vertical
            ? new Rectangle(r.X, r.Y - 1, r.Width, r.Height + 2)
            : new Rectangle(r.X - 1, r.Y, r.Width + 2, r.Height);
        using var brush = new LinearGradientBrush(brushRect, begin, end, mode);
        g.FillRectangle(brush, r);
    }

    protected static ColorBlend ThreeStop(Color a, Color b, Color c)
        => new()
        {
            Colors = new[] { a, b, c },
            Positions = new[] { 0f, 0.5f, 1f },
        };

    protected static GraphicsPath RoundedRect(Rectangle r, int radius)
    {
        int d = radius * 2;
        var path = new GraphicsPath();
        if (d <= 0 || r.Width < d || r.Height < d)
        {
            path.AddRectangle(r);
            return path;
        }
        path.AddArc(r.X, r.Y, d, d, 180, 90);
        path.AddArc(r.Right - d, r.Y, d, d, 270, 90);
        path.AddArc(r.Right - d, r.Bottom - d, d, d, 0, 90);
        path.AddArc(r.X, r.Bottom - d, d, d, 90, 90);
        path.CloseFigure();
        return path;
    }

    // Rounded on the right corners, square on the left — for the chevron nub.
    protected static GraphicsPath RoundedRightRect(Rectangle r, int radius)
    {
        int d = radius * 2;
        var path = new GraphicsPath();
        if (d <= 0 || r.Width < d || r.Height < d)
        {
            path.AddRectangle(r);
            return path;
        }
        path.AddArc(r.Right - d, r.Y, d, d, 270, 90);        // top-right corner
        path.AddArc(r.Right - d, r.Bottom - d, d, d, 0, 90); // bottom-right corner
        path.AddLine(r.X, r.Bottom, r.X, r.Y);               // straight left edge
        path.CloseFigure();
        return path;
    }

    // Rounded on the bottom corners, square on the top — for the chevron nub of
    // a vertical (Left/Right-docked) toolbar.
    protected static GraphicsPath RoundedBottomRect(Rectangle r, int radius)
    {
        int d = radius * 2;
        var path = new GraphicsPath();
        if (d <= 0 || r.Width < d || r.Height < d)
        {
            path.AddRectangle(r);
            return path;
        }
        path.AddLine(r.X, r.Y, r.Right, r.Y);                // straight top edge
        path.AddArc(r.Right - d, r.Bottom - d, d, d, 0, 90); // bottom-right corner
        path.AddArc(r.X, r.Bottom - d, d, d, 90, 90);        // bottom-left corner
        path.CloseFigure();
        return path;
    }
}
