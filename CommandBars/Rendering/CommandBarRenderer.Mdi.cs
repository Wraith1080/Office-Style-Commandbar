using System.Drawing;
using System.Windows.Forms;
using CommandBars.Model;

namespace CommandBars.Rendering;

public abstract partial class CommandBarRenderer
{
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
        else
        {
            graphics.DrawLine(pen, x, y, x + w - 1, y + h);
            graphics.DrawLine(pen, x + w - 1, y, x, y + h);
        }
    }
}

