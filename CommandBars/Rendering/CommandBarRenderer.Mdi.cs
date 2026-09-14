using System.Drawing;
using System.Windows.Forms;
using CommandBars.Model;

namespace CommandBars.Rendering;

public abstract partial class CommandBarRenderer
{
    /// <summary>Paints the restored MDI child's border without painting document content.</summary>
    public virtual void DrawMdiChildBorder(Graphics graphics, Rectangle bounds, int thickness, bool active)
    {
        // Keep the usable resize margin, but don't turn all of it into an accent band.
        using var margin = new SolidBrush(Colors.MenuBarGradientBegin);
        FillBorder(margin, thickness);
        using var outline = new SolidBrush(active ? Colors.ButtonHotBorder : Colors.BarBorder);
        FillBorder(outline, Math.Min(thickness, Math.Max(1, Dp(1))));

        void FillBorder(Brush brush, int width)
        {
            graphics.FillRectangle(brush, bounds.Left, bounds.Top, bounds.Width, width);
            graphics.FillRectangle(brush, bounds.Left, bounds.Bottom - width, bounds.Width, width);
            graphics.FillRectangle(brush, bounds.Left, bounds.Top, width, bounds.Height);
            graphics.FillRectangle(brush, bounds.Right - width, bounds.Top, width, bounds.Height);
        }
    }

    /// <summary>Paints a DPI-sized child caption using the current theme.</summary>
    public virtual void DrawMdiChildCaption(Graphics graphics, Rectangle bounds, Rectangle textBounds, string text, Font font, bool active)
    {
        if (bounds.Width <= 0 || bounds.Height <= 0) return;
        using var brush = new System.Drawing.Drawing2D.LinearGradientBrush(bounds,
            active ? Colors.BandGradientBegin : Colors.MenuBarGradientBegin,
            active ? Colors.BandGradientEnd : Colors.MenuBarGradientEnd, 0f);
        graphics.FillRectangle(brush, bounds);
        TextRenderer.DrawText(graphics, text, font, textBounds, active ? Colors.Text : Colors.DisabledText,
            TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis | TextFormatFlags.NoPrefix);
    }

    /// <summary>Draws an MDI child system icon or caption action using this bar's theme.</summary>
    public virtual void DrawMdiButton(Graphics graphics, Rectangle bounds, CaptionButton? action, Icon? icon, RenderState state)
    {
        // The child system icon stays unadorned; only caption actions highlight.
        if (action != null && (state & (RenderState.Hot | RenderState.Pressed)) != 0)
            DrawButton(graphics, bounds, state, BarOrientation.Horizontal);
        int size = Math.Min(Dp(16), Math.Min(bounds.Width, bounds.Height));
        if (size <= 0) return;
        var glyph = new Rectangle(bounds.X + (bounds.Width - size) / 2, bounds.Y + (bounds.Height - size) / 2, size, size);
        if (action == null)
        {
            if (icon != null) graphics.DrawIcon(icon, glyph);
            return;
        }
        using var pen = new Pen((state & RenderState.Disabled) != 0 ? Colors.DisabledText : Colors.Text, Math.Max(1, Dp(1)));
        int x = glyph.Left + Dp(4), y = glyph.Top + Dp(4);
        int w = Math.Max(3, Dp(8)), h = Math.Max(3, Dp(7));
        if (action == CaptionButton.Minimize)
        {
            graphics.DrawLine(pen, x, y + h, x + w - Dp(2), y + h);
            graphics.DrawLine(pen, x, y + h - Dp(1), x + w - Dp(2), y + h - Dp(1));
        }
        else if (action == CaptionButton.Restore)
        {
            // Draw only the exposed edges of the rear window. Painting over its
            // hidden edges with a solid color would spoil the actual bar/hover gradient.
            graphics.DrawLines(pen, new[]
            {
                new Point(x + Dp(2), y + Dp(3)),
                new Point(x + Dp(2), y),
                new Point(x + w, y),
                new Point(x + w, y + h - Dp(2)),
                new Point(x + w - Dp(2), y + h - Dp(2))
            });
            graphics.DrawRectangle(pen, x, y + Dp(3), w - Dp(2), h - Dp(2));
            graphics.DrawLine(pen, x, y + Dp(4), x + w - Dp(2), y + Dp(4));
        }
        else if (action == CaptionButton.Maximize)
        {
            graphics.DrawRectangle(pen, x, y, w, h);
            graphics.DrawLine(pen, x, y + Dp(1), x + w, y + Dp(1));
        }
        else
        {
            graphics.DrawLine(pen, x, y, x + w - 1, y + h);
            graphics.DrawLine(pen, x + w - 1, y, x, y + h);
        }
    }
}

