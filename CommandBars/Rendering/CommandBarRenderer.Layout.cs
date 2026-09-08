using System.Drawing;
using System.Windows.Forms;
using CommandBars.Controls;

namespace CommandBars.Rendering;

public abstract partial class CommandBarRenderer
{
    // Metrics without a scale argument are logical (96-DPI) pixels. Geometry
    // hooks take the owning window's scale explicitly: one renderer can serve
    // several windows on different monitors. Defaults preserve existing themes.
    public virtual int FloatingCaptionVerticalPadding => 6;
    public virtual Padding GetFloatingCaptionTextInsets(float scale) => new(5, 0, 7, 0);
    public virtual int MenuBarHighlightExpansion => 0;
    public virtual int MenuToToolbarGap => ToolbarGap;
    public virtual int SplitArrowWidth => 12;
    public virtual int ToolbarPopupHorizontalPadding => 7;
    public virtual bool SquareToolbarButtons => false;
    public virtual bool HighlightSplitButtonPartsIndependently => false;
    public virtual bool DrawsSeparateSplitDivider => true;
    public virtual bool PopupDropShadow => false;
    public virtual int PopupCornerRadius => 0;
    public virtual bool SizeComboPopupToContent => false;

    public virtual int GetChevronExtent(bool vertical, int columnWidth, int rowHeight, float iconScale)
        => (int)Math.Round(ChevronExtent * iconScale);

    public virtual int GetToolbarImageSize(Rectangle content, int requestedSize, bool vertical, float scale)
        => requestedSize;

    public virtual int GetToolbarImageLeadingInset(int contentPadding, float scale) => contentPadding;

    public virtual int GetToolbarComboHeight(int rowHeight, int fontHeight, float scale)
        => Math.Min(rowHeight, fontHeight + (int)Math.Round(6 * scale));

    public virtual Rectangle GetPopupAnchorBounds(Rectangle bounds, bool overflow, bool vertical, float scale)
        => bounds;

    public virtual Padding GetComboPopupInsets(float scale) => new(1);
    public virtual int GetComboPopupTextInset(float scale) => 4;
    public virtual int GetComboPopupRowPadding(float scale) => 6;
    public virtual int GetComboPopupTrailingPadding(float scale) => 0;

    public virtual void DrawComboPopupBackground(Graphics g, Rectangle bounds) { }

    public virtual void DrawComboPopupBorder(Graphics g, Rectangle bounds)
    {
        using var pen = new Pen(Colors.MenuBorder);
        g.DrawRectangle(pen, bounds.X, bounds.Y, bounds.Width - 1, bounds.Height - 1);
    }

    public virtual void DrawFloatingCaptionCloseButton(Graphics g, Rectangle bounds, bool hot, bool pressed)
        => FloatingCaptionButtonPainter.DrawClose(g, this, bounds, hot, pressed);

    public virtual int GetMenuSeparatorHeight(float scale)
    {
        int height = (int)Math.Round(4 * scale);
        if ((height & 1) != 0) height++;
        // The classic highlight has a trailing blank strip. Account for its
        // parity so the separator has equal space on both sides at every DPI.
        if (UsesClassicMenuItemChrome && ((height - (int)Math.Round(scale)) & 1) != 0)
            height--;
        return height;
    }

    public virtual int GetMenuBottomInset(int outerInset) => Math.Max(1, outerInset - 1);

    public virtual Rectangle GetMenuSeparatorBounds(Rectangle row, int marginWidth, float scale)
        => new(marginWidth + 2, row.Y, row.Width - marginWidth - 6,
            UsesClassicMenuItemChrome ? Math.Max(2, row.Height - (int)Math.Round(scale)) : row.Height);

    public virtual Padding GetMenuSelectionInsets(float scale)
        => new(0, 0, 0, UsesClassicMenuItemChrome ? (int)Math.Round(scale) : 1);

    public virtual bool ShouldDrawCheckedMenuIconFrame(bool hasImage, bool hot) => !hot;

    public virtual Rectangle GetMenuImageBounds(Rectangle row, Rectangle iconBox, int marginWidth, int iconPixels, float scale)
        => new(2 + (marginWidth - iconPixels) / 2 + (UsesClassicMenuItemChrome ? (int)Math.Round(scale) : 0),
            row.Y + (row.Height - iconPixels) / 2, iconPixels, iconPixels);

    public virtual Rectangle GetMenuIconBounds(Rectangle row, int compactSize, int marginWidth, float scale)
    {
        if (UsesClassicMenuItemChrome)
            return new Rectangle((int)Math.Round(2 * scale), row.Y - (int)Math.Round(scale),
                marginWidth + (int)Math.Round(2 * scale), row.Height + (int)Math.Round(scale));
        return new Rectangle(2 + (marginWidth - compactSize) / 2,
            row.Y + (row.Height - compactSize) / 2, compactSize, compactSize);
    }
}
