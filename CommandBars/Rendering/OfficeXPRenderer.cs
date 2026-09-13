using System.Drawing;
using CommandBars.Model;

namespace CommandBars.Rendering;

/// <summary>
/// The Office XP look. Reuses the 2003 renderer's drawing but with a flat gray
/// color table and square corners (chunk radius 0), so bars and buttons render
/// flat with the XP blue selection.
/// </summary>
public sealed class OfficeXPRenderer : Office2003Renderer
{
    public OfficeXPRenderer() : this(CommandBarColorScheme.Default) { }
    public OfficeXPRenderer(CommandBarColorScheme scheme) : this(scheme, false) { }
    public OfficeXPRenderer(CommandBarColorScheme scheme, bool useMultiStripGripper)
    {
        Colors = SchemeColorTable.Create(new OfficeXPColorTable(), CommandBarTheme.OfficeXP, scheme);
        UseMultiStripGripper = useMultiStripGripper;
    }
    public bool UseMultiStripGripper { get; }
    public override CommandBarColorTable Colors { get; }

    protected override int ChunkRadius => 0;

    public override void DrawGripper(Graphics g, Rectangle bounds, BarOrientation orientation)
    {
        if (!UseMultiStripGripper)
        {
            base.DrawGripper(g, bounds, orientation);
            return;
        }

        // Short embossed strips stack along the handle; transpose on vertical bars.
        bool horizontal = orientation == BarOrientation.Horizontal;
        int cross = horizontal ? bounds.Width : bounds.Height;
        int length = horizontal ? bounds.Height : bounds.Width;
        int thickness = Math.Max(1, Dp(1));
        int width = Math.Min(Math.Max(1, Dp(4)), cross - thickness);
        if (width <= 0) return;
        int leading = (cross - width - thickness) / 2;
        using var dark = new SolidBrush(Colors.GripperDark);
        using var light = new SolidBrush(Colors.GripperLight);
        for (int offset = Dp(4); offset + 2 * thickness <= length - Dp(4); offset += Math.Max(1, Dp(3)))
        {
            var strip = horizontal
                ? new Rectangle(bounds.X + leading, bounds.Y + offset, width, thickness)
                : new Rectangle(bounds.X + offset, bounds.Y + leading, thickness, width);
            var highlight = strip;
            highlight.Offset(thickness, thickness);
            g.FillRectangle(light, highlight);
            g.FillRectangle(dark, strip);
        }
    }
}
