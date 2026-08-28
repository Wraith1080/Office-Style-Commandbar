using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;
using CommandBars.Model;

namespace CommandBars.Rendering;

/// <summary>
/// Draws all command-bar chrome. Items never paint themselves — they route
/// through a renderer, so swapping the renderer swaps the entire look (Office
/// XP / 2003 / 2007) with no changes to the controls.
/// </summary>
public abstract class CommandBarRenderer
{
    private CommandBarDialogColorTable? _dialogColors;

    /// <summary>The palette this renderer draws with.</summary>
    public abstract CommandBarColorTable Colors { get; }

    /// <summary>
    /// Palette for supporting windows such as the Customize dialog. The default
    /// is derived from <see cref="Colors"/>; renderers may override this property
    /// when their dialog chrome needs independently selected colors.
    /// </summary>
    public virtual CommandBarDialogColorTable DialogColors
        => _dialogColors ??= new CommandBarDialogColorTable(Colors);

    /// <summary>
    /// DPI scale (1.0 == 96 DPI) applied to size-dependent chrome. The hosting
    /// control sets this from its DeviceDpi before layout and painting.
    /// </summary>
    public float Scale { get; set; } = 1f;

    /// <summary>Whether root popup owners visually merge with their popup.</summary>
    internal virtual bool ConnectPopupOwners => true;

    /// <summary>Whether popup rows use classic pre-XP gutter/icon behavior.</summary>
    internal virtual bool UsesClassicMenuItemChrome => false;

    /// <summary>Text color used by floating-window captions.</summary>
    internal virtual Color FloatingCaptionTextColor => Colors.Text;

    /// <summary>Rounds a logical (96-DPI) length to device pixels.</summary>
    protected int Dp(double logical) => (int)Math.Round(logical * Scale);

    /// <summary>Width of the drag gripper at the leading edge of a movable bar.</summary>
    public virtual int GripperExtent => Dp(8);

    /// <summary>Width reserved for a toolbar's overflow chevron.</summary>
    public virtual int ChevronExtent => Dp(14);

    /// <summary>
    /// Draws the bar background. Every bar first fills a slice of the container
    /// band gradient, which runs horizontally (light at the left, dark at the
    /// right). <paramref name="bandOffset"/> is the bar's X position within the
    /// band of width <paramref name="bandExtent"/>, so the slices are seamless
    /// with the rebar. A toolbar (<paramref name="rounded"/> = true) then draws
    /// its raised rounded chunk on top; the menu bar keeps just the band slice
    /// with no extra gradient or edge.
    /// </summary>
    public abstract void DrawBarBackground(
        Graphics g, Rectangle bounds, CommandBarType barType, BarOrientation orientation,
        bool rounded, int bandOffset, int bandExtent);

    /// <summary>
    /// Draws the dock band (rebar) behind toolbar chunks. The gradient runs
    /// along the band's main axis: left-to-right for a horizontal band,
    /// top-to-bottom for a vertical one. The host draws the edge separator.
    /// </summary>
    public abstract void DrawBand(Graphics g, Rectangle bounds, BarOrientation orientation);

    /// <summary>
    /// Draws the overflow chevron nub. <paramref name="bounds"/> is the chevron
    /// area; <paramref name="barBounds"/> is the whole toolbar so the nub can be
    /// clipped to the chunk's rounded edge. <paramref name="orientation"/> is the
    /// bar's direction: the nub sits at the right of a horizontal bar and the
    /// bottom of a vertical one.
    /// </summary>
    public abstract void DrawChevron(Graphics g, Rectangle bounds, Rectangle barBounds, BarOrientation orientation, RenderState state);

    /// <summary>
    /// Draws the chevron with knowledge of whether the bar actually has hidden
    /// items. Kept internal so existing third-party renderers remain source and
    /// binary compatible with the original public renderer contract.
    /// </summary>
    internal virtual void DrawChevron(Graphics g, Rectangle bounds, Rectangle barBounds,
        BarOrientation orientation, RenderState state, bool hasOverflowItems)
        => DrawChevron(g, bounds, barBounds, orientation, state);

    /// <summary>Draws the move gripper.</summary>
    public abstract void DrawGripper(Graphics g, Rectangle bounds, BarOrientation orientation);

    /// <summary>
    /// Draws a toolbar button background for the given state. The fill gradient
    /// runs along the bar's axis — top-to-bottom on a horizontal bar,
    /// left-to-right on a vertical one.
    /// </summary>
    public abstract void DrawButton(Graphics g, Rectangle bounds, RenderState state, BarOrientation orientation);

    /// <summary>
    /// Draws the frame background and caption surface shared by floating
    /// toolbars and tear-off palettes.
    /// </summary>
    internal virtual void DrawFloatingWindowChrome(Graphics g, Rectangle bounds,
        Rectangle captionBounds)
    {
        using (var back = new SolidBrush(Colors.BandGradientEnd))
            g.FillRectangle(back, bounds);
        using (var frame = new Pen(Colors.RaisedBorder))
            g.DrawRectangle(frame, bounds.X, bounds.Y,
                Math.Max(0, bounds.Width - 1), Math.Max(0, bounds.Height - 1));

        using var caption = new LinearGradientBrush(
            new Rectangle(captionBounds.X, captionBounds.Y,
                Math.Max(1, captionBounds.Width), captionBounds.Height + 1),
            Colors.BandGradientBegin, Colors.BandGradientEnd,
            LinearGradientMode.Horizontal);
        g.FillRectangle(caption, captionBounds);
    }

    /// <summary>
    /// Draws an open popup owner without the border edge shared with its popup.
    /// The default preserves the original renderer behavior; built-in Office
    /// renderers override it to create a continuous button/menu outline.
    /// </summary>
    internal virtual void DrawConnectedButton(Graphics g, Rectangle bounds, RenderState state,
        BarOrientation orientation, PopupConnectionEdge connectionEdge)
        => DrawButton(g, bounds, state, orientation);

    /// <summary>
    /// Draws a popup owner in its latched-open appearance. This is intentionally
    /// distinct from a momentarily pressed command button.
    /// </summary>
    internal virtual void DrawOpenMenuButton(Graphics g, Rectangle bounds,
        BarOrientation orientation, PopupConnectionEdge connectionEdge)
        => DrawConnectedButton(g, bounds, RenderState.Checked, orientation, connectionEdge);

    /// <summary>Draws an inline combo field and its arrow-button chrome.</summary>
    internal virtual void DrawComboBoxChrome(Graphics g, Rectangle bounds,
        Rectangle arrowBounds, RenderState state, Color fieldBackground)
    {
        using (var back = new SolidBrush(fieldBackground))
            g.FillRectangle(back, bounds);

        bool active = state is RenderState.Hot or RenderState.Pressed;
        if (active)
        {
            var arrowFill = new Rectangle(arrowBounds.X, arrowBounds.Y,
                arrowBounds.Width + 1, arrowBounds.Height + 1);
            DrawButton(g, arrowFill, state, BarOrientation.Horizontal);
        }

        Color borderColor = state switch
        {
            RenderState.Pressed => Colors.ButtonPressedBorder,
            RenderState.Hot => Colors.ButtonHotBorder,
            _ => Colors.BarBorder,
        };
        using var pen = new Pen(borderColor);
        g.DrawRectangle(pen, bounds);
    }

    /// <summary>Draws a separator between items.</summary>
    public abstract void DrawSeparator(Graphics g, Rectangle bounds, BarOrientation orientation);

    /// <summary>Draws item text.</summary>
    public abstract void DrawItemText(Graphics g, string text, Font font, Rectangle bounds, RenderState state, TextFormatFlags flags);

    /// <summary>Draws popup-menu text, including a theme-specific selected color.</summary>
    internal virtual void DrawMenuItemText(Graphics g, string text, Font font,
        Rectangle bounds, RenderState state, TextFormatFlags flags)
    {
        Color color = (state & RenderState.Disabled) != 0
            ? Colors.DisabledMenuText
            : (state & RenderState.Hot) != 0 ? Colors.MenuItemSelectedText : Colors.MenuText;
        TextRenderer.DrawText(g, text, font, bounds, color, flags);
    }

    /// <summary>Draws an item image, greyed if the state is disabled.</summary>
    public abstract void DrawItemImage(Graphics g, Image image, Rectangle bounds, RenderState state);

    /// <summary>Draws a dropdown arrow glyph.</summary>
    public abstract void DrawDropDownArrow(Graphics g, Rectangle bounds, RenderState state);

    // --- Popup menu chrome -------------------------------------------------

    /// <summary>Draws the popup menu background and border.</summary>
    public abstract void DrawMenuBackground(Graphics g, Rectangle bounds);

    /// <summary>Draws the left image-margin gutter of a popup menu.</summary>
    public abstract void DrawImageMargin(Graphics g, Rectangle bounds);

    /// <summary>Draws a popup menu item background for the given state.</summary>
    public abstract void DrawMenuItemBackground(Graphics g, Rectangle bounds, RenderState state);

    /// <summary>Draws the check mark for a checked menu item.</summary>
    public abstract void DrawMenuCheck(Graphics g, Rectangle bounds, RenderState state);
}

/// <summary>The edge of an owner button that touches its open popup.</summary>
internal enum PopupConnectionEdge
{
    None,
    Left,
    Top,
    Right,
    Bottom,
}
