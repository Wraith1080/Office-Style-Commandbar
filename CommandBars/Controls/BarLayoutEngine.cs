using System.Drawing;
using System.Windows.Forms;
using CommandBars.Model;

namespace CommandBars.Controls;

/// <summary>
/// Measures and positions items along a bar (menu bar / toolbar). Assigns each
/// visible item its <see cref="CommandBarItem.Bounds"/> and returns the row
/// height. Spacing comes from a DPI-scaled <see cref="BarMetrics"/>, and the
/// <c>iconPx</c> argument is the icon size already in device pixels.
/// </summary>
internal static class BarLayoutEngine
{
    // No NoPrefix: '&' is treated as a mnemonic marker (removed for measuring,
    // underlined when keyboard cues are shown).
    internal const TextFormatFlags MeasureFlags = TextFormatFlags.SingleLine;

    internal static int LayoutHorizontal(
        Graphics g, CommandBar bar, Font font, int iconPx, int gripperOffset, BarMetrics m, out int totalWidth)
    {
        bool isMenuBar = bar.BarType == CommandBarType.MenuBar;
        int contentHeight = isMenuBar ? font.Height : Math.Max(iconPx, font.Height);
        int rowHeight = contentHeight + (2 * m.ContentVPad);

        int x = gripperOffset + m.TopInset;
        foreach (var item in bar.Items)
        {
            if (!item.Visible)
            {
                item.Bounds = Rectangle.Empty;
                continue;
            }

            int width = MeasureItemWidth(g, item, font, iconPx, m);
            item.Bounds = new Rectangle(x, m.TopInset, width, rowHeight);
            x += width;
        }

        totalWidth = x + m.TopInset;
        return rowHeight;
    }

    /// <summary>
    /// Measures and positions items down a vertical bar (Left/Right dock).
    /// Buttons render icon-only in a square cell; the gripper sits at the top
    /// (<paramref name="gripperOffset"/> is its reserved height). Returns the
    /// content height (gripper + items + insets) and, via
    /// <paramref name="columnWidth"/>, the bar's cross width.
    /// </summary>
    internal static int LayoutVertical(
        Graphics g, CommandBar bar, Font font, int iconPx, int gripperOffset, BarMetrics m, out int columnWidth)
    {
        int cell = iconPx + (2 * m.ButtonHPad);
        columnWidth = cell + (2 * m.TopInset);

        int x = m.TopInset;
        int y = gripperOffset + m.TopInset;
        foreach (var item in bar.Items)
        {
            if (!item.Visible)
            {
                item.Bounds = Rectangle.Empty;
                continue;
            }

            int height = MeasureItemHeight(item, font, iconPx, m);
            item.Bounds = new Rectangle(x, y, cell, height);
            y += height;
        }

        return y + m.TopInset;
    }

    internal static int MeasureItemHeight(CommandBarItem item, Font font, int iconPx, BarMetrics m)
    {
        return item switch
        {
            CommandBarSeparator => m.SeparatorThickness,
            CommandBarLabel => font.Height + (2 * m.ContentVPad),
            // Split buttons reserve an arrow strip below the icon.
            CommandBarSplitButton => iconPx + (2 * m.ContentVPad) + m.ArrowWidth,
            _ => iconPx + (2 * m.ContentVPad),
        };
    }

    internal static int MeasureItemWidth(Graphics g, CommandBarItem item, Font font, int iconPx, BarMetrics m)
    {
        switch (item)
        {
            case CommandBarSeparator:
                return m.SeparatorThickness;

            case CommandBarPopupItem popup:
                return MeasureText(g, popup.Text, font) + (2 * m.MenuItemHPad);

            case CommandBarLabel label:
                return MeasureText(g, label.Text, font) + (2 * m.ButtonHPad);

            case CommandBarComboBox combo:
                return combo.Width + (2 * m.ButtonHPad);

            case CommandBarCommandItem cmd:
            {
                bool hasImage = cmd.DisplayStyle != CommandItemDisplayStyle.TextOnly && cmd.Command.Image is not null;
                // Show the caption when the style allows it, OR when there's no
                // image to show — so an icon-less button (e.g. a command with no
                // picture) falls back to its text instead of measuring blank.
                bool hasText = !string.IsNullOrEmpty(cmd.DisplayText)
                    && (cmd.DisplayStyle != CommandItemDisplayStyle.ImageOnly || !hasImage);

                int width = m.ButtonHPad;
                if (hasImage) width += iconPx;
                if (hasImage && hasText) width += m.TextImageGap;
                if (hasText) width += MeasureText(g, cmd.Command.Text, font);
                width += m.ButtonHPad;

                if (!hasText) // image-only: keep it square-ish
                    width = Math.Max(width, iconPx + (2 * m.ButtonHPad));

                if (item is CommandBarSplitButton)
                    width += m.ArrowWidth;

                return width;
            }

            default:
                return iconPx + (2 * m.ButtonHPad);
        }
    }

    internal static int MeasureText(Graphics g, string text, Font font)
    {
        if (string.IsNullOrEmpty(text))
            return 0;
        return TextRenderer.MeasureText(g, text, font, new Size(int.MaxValue, font.Height), MeasureFlags).Width;
    }
}
