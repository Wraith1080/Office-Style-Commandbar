using System.Drawing;
using System.Windows.Forms;
using CommandBars.Controls;

namespace CommandBars.Rendering;

public sealed partial class FluentRenderer
{
    public override int FloatingCaptionVerticalPadding => 12;
    public override Padding GetFloatingCaptionTextInsets(float scale)
    {
        int left = (int)Math.Round(12 * scale);
        return new Padding(left, 0, (int)Math.Round(24 * scale) - left, 0);
    }
    public override int MenuBarHighlightExpansion => 2;
    public override int MenuToToolbarGap => 2;
    public override int SplitArrowWidth => 18;
    public override int ToolbarPopupHorizontalPadding => 3;
    public override bool SquareToolbarButtons => true;
    public override bool HighlightSplitButtonPartsIndependently => true;
    public override bool DrawsSeparateSplitDivider => false;
    public override bool PopupDropShadow => true;
    public override int PopupCornerRadius => 4;
    public override bool SizeComboPopupToContent => true;

    public override int GetChevronExtent(bool vertical, int columnWidth, int rowHeight, float iconScale)
        => Math.Max(1, vertical ? columnWidth - 2 : rowHeight);

    public override int GetToolbarImageSize(Rectangle content, int requestedSize, bool vertical, float scale)
    {
        int widthInset = (int)Math.Round((vertical ? 10 : 8) * scale);
        int heightInset = (int)Math.Round((vertical ? 12 : 10) * scale);
        int available = Math.Min(content.Width - widthInset, content.Height - heightInset);
        return Math.Max(1, Math.Min(requestedSize, (int)Math.Floor(available / scale)));
    }

    public override int GetToolbarComboHeight(int rowHeight, int fontHeight, float scale)
        => Math.Max(1, rowHeight - 2 * (int)Math.Round(3 * scale));

    public override int GetToolbarImageLeadingInset(int contentPadding, float scale) => (int)Math.Round(4 * scale);

    public override Rectangle GetPopupAnchorBounds(Rectangle bounds, bool overflow, bool vertical, float scale)
    {
        int inset = (int)Math.Round((overflow ? 3 : vertical ? 4 : 2) * scale);
        if (vertical) bounds.Inflate(0, -inset);
        else bounds.Inflate(-inset, 0);
        return bounds;
    }

    public override Padding GetComboPopupInsets(float scale) => new(Math.Max(1, (int)Math.Round(4 * scale)));
    public override int GetComboPopupTextInset(float scale) => (int)Math.Round(14 * scale);
    public override int GetComboPopupRowPadding(float scale) => (int)Math.Round(12 * scale);
    public override int GetComboPopupTrailingPadding(float scale) => (int)Math.Round(12 * scale);
    public override void DrawComboPopupBackground(Graphics g, Rectangle bounds) => DrawMenuBackground(g, bounds);
    public override void DrawComboPopupBorder(Graphics g, Rectangle bounds) { }

    public override void DrawFloatingCaptionCloseButton(Graphics g, Rectangle bounds, bool hot, bool pressed)
    {
        if (hot)
            RoundedSurface.Draw(g, bounds, 4 * Scale,
                pressed ? Color.FromArgb(210, 200, 236) : Color.FromArgb(225, 218, 242));
        FloatingCaptionButtonPainter.DrawCloseGlyph(g, bounds, FloatingCaptionTextColor);
    }

    public override int GetMenuSeparatorHeight(float scale)
    {
        int height = (int)Math.Round(5 * scale);
        return (height & 1) == 0 ? height + 1 : height;
    }

    public override int GetMenuBottomInset(int outerInset) => outerInset;
    public override Rectangle GetMenuSeparatorBounds(Rectangle row, int marginWidth, float scale)
        => new((int)Math.Round(3 * scale), row.Y, row.Width - (int)Math.Round(6 * scale), row.Height);
    public override Padding GetMenuSelectionInsets(float scale)
        => new(0, (int)Math.Round(scale), 0, (int)Math.Round(scale));
    public override bool ShouldDrawCheckedMenuIconFrame(bool hasImage, bool hot) => hasImage;

    public override Rectangle GetMenuImageBounds(Rectangle row, Rectangle iconBox, int marginWidth, int iconPixels, float scale)
        => new(iconBox.X + (iconBox.Width - iconPixels) / 2,
            iconBox.Y + (iconBox.Height - iconPixels) / 2, iconPixels, iconPixels);

    public override Rectangle GetMenuIconBounds(Rectangle row, int compactSize, int marginWidth, float scale)
    {
        int selectionHeight = row.Height - 2 * (int)Math.Round(scale);
        compactSize -= (selectionHeight - compactSize) & 1;
        int padding = Math.Max(0, (selectionHeight - compactSize) / 2);
        return new Rectangle((int)Math.Round(3 * scale) + padding,
            row.Y + (int)Math.Round(scale) + padding, compactSize, compactSize);
    }
}
