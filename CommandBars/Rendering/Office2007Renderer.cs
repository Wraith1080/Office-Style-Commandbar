using System.Drawing;
using System.Drawing.Drawing2D;
using CommandBars.Model;

namespace CommandBars.Rendering;

/// <summary>
/// The Office 2007 look. Inherits the 2003 renderer's bars/menus but overrides
/// buttons to be rounded with a glassy top sheen and the 2007 gold highlight.
/// </summary>
public sealed class Office2007Renderer : Office2003Renderer
{
    private const int ButtonRadius = 2;

    public override CommandBarColorTable Colors { get; } = new Office2007ColorTable();

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

        bool vertical = orientation == BarOrientation.Vertical;
        var r = new Rectangle(bounds.X, bounds.Y, bounds.Width - 1, bounds.Height - 1);
        var previous = g.SmoothingMode;
        g.SmoothingMode = SmoothingMode.AntiAlias;

        using (var path = RoundedRect(r, Dp(ButtonRadius)))
        {
            var fillRect = vertical
                ? new Rectangle(r.X - 1, r.Y, r.Width + 2, r.Height)
                : new Rectangle(r.X, r.Y - 1, r.Width, r.Height + 2);
            using (var brush = new LinearGradientBrush(fillRect, begin, end,
                vertical ? LinearGradientMode.Horizontal : LinearGradientMode.Vertical))
                g.FillPath(brush, path);

            // Glassy sheen over the leading half (top for horizontal, left for vertical).
            var sheenRect = vertical
                ? new Rectangle(r.X, r.Y, r.Width / 2, r.Height)
                : new Rectangle(r.X, r.Y, r.Width, r.Height / 2);
            if (sheenRect.Width > 0 && sheenRect.Height > 0)
            {
                var gs = g.Save();
                g.SetClip(path, CombineMode.Replace);
                var sheenBrushRect = vertical
                    ? new Rectangle(sheenRect.X - 1, sheenRect.Y, sheenRect.Width + 1, sheenRect.Height)
                    : new Rectangle(sheenRect.X, sheenRect.Y - 1, sheenRect.Width, sheenRect.Height + 1);
                using (var sheen = new LinearGradientBrush(sheenBrushRect,
                    Color.FromArgb(170, 255, 255, 255), Color.FromArgb(40, 255, 255, 255),
                    vertical ? LinearGradientMode.Horizontal : LinearGradientMode.Vertical))
                    g.FillRectangle(sheen, sheenRect);
                g.Restore(gs);
            }

            using (var pen = new Pen(border))
                g.DrawPath(pen, path);
        }

        g.SmoothingMode = previous;
    }

    internal override void DrawConnectedButton(Graphics g, Rectangle bounds, RenderState state,
        BarOrientation orientation, PopupConnectionEdge connectionEdge)
    {
        DrawButton(g, bounds, state, orientation);
        if (connectionEdge == PopupConnectionEdge.None || bounds.Width <= 1 || bounds.Height <= 1)
            return;

        Color begin;
        Color end;
        Color border;
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
        }
        else if ((state & RenderState.Hot) != 0)
        {
            begin = Colors.ButtonHotBegin;
            end = Colors.ButtonHotEnd;
            border = Colors.ButtonHotBorder;
        }
        else
        {
            return;
        }

        bool vertical = orientation == BarOrientation.Vertical;
        var gradientBounds = vertical
            ? new Rectangle(bounds.X - 1, bounds.Y, bounds.Width + 2, bounds.Height)
            : new Rectangle(bounds.X, bounds.Y - 1, bounds.Width, bounds.Height + 2);
        using (var fill = new LinearGradientBrush(gradientBounds, begin, end,
            vertical ? LinearGradientMode.Horizontal : LinearGradientMode.Vertical))
        {
            Rectangle seam = connectionEdge switch
            {
                PopupConnectionEdge.Left => new Rectangle(bounds.Left, bounds.Top, 1, bounds.Height),
                PopupConnectionEdge.Top => new Rectangle(bounds.Left, bounds.Top, bounds.Width, 1),
                PopupConnectionEdge.Right => new Rectangle(bounds.Right - 1, bounds.Top, 1, bounds.Height),
                PopupConnectionEdge.Bottom => new Rectangle(bounds.Left, bounds.Bottom - 1, bounds.Width, 1),
                _ => Rectangle.Empty,
            };
            g.FillRectangle(fill, seam);
        }

        // Restore the two perpendicular outline edges right up to the seam.
        using var pen = new Pen(border);
        switch (connectionEdge)
        {
            case PopupConnectionEdge.Top:
            case PopupConnectionEdge.Bottom:
                g.DrawLine(pen, bounds.Left, bounds.Top, bounds.Left, bounds.Bottom - 1);
                g.DrawLine(pen, bounds.Right - 1, bounds.Top, bounds.Right - 1, bounds.Bottom - 1);
                break;
            case PopupConnectionEdge.Left:
            case PopupConnectionEdge.Right:
                g.DrawLine(pen, bounds.Left, bounds.Top, bounds.Right - 1, bounds.Top);
                g.DrawLine(pen, bounds.Left, bounds.Bottom - 1, bounds.Right - 1, bounds.Bottom - 1);
                break;
        }
    }
}
