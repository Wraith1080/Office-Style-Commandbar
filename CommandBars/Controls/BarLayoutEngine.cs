using System;
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

    /// <summary>
    /// A gentle, capped growth factor applied to a hosted combo's font and width
    /// so the combo grows with the toolbar's icon size instead of sitting frozen
    /// in a taller row. It is 1.0 at the default icon size, rises at half the
    /// icon-size ratio, and is capped so 48/64px icons don't blow the combo up.
    /// Derived purely from <paramref name="iconPx"/> and <paramref name="dpiScale"/>
    /// so the layout engine and the control compute an identical value.
    /// </summary>
    internal static float ComboGrow(int iconPx, float dpiScale)
    {
        float logicalIcon = iconPx / Math.Max(0.01f, dpiScale);
        float ratio = logicalIcon / IconSizes.Default;   // 1.0 at the default size
        float grow = 1f + (Math.Max(0f, ratio - 1f) * 0.5f);
        return Math.Min(grow, 1.6f);
    }

    /// <summary>The combo's editable-field width in device pixels (DPI- and icon-size-scaled).</summary>
    internal static int ComboBoxWidthPx(CommandBarComboBox combo, int iconPx, float dpiScale)
        => (int)Math.Round(combo.Width * dpiScale * ComboGrow(iconPx, dpiScale));

    internal static int LayoutHorizontal(
        Graphics g, CommandBar bar, Font font, int iconPx, int gripperOffset, BarMetrics m, float dpiScale, bool iconOnly, out int totalWidth)
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

            int width = MeasureItemWidth(g, item, font, iconPx, m, dpiScale, iconOnly,
                bar.BarType != CommandBarType.MenuBar);
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
        Graphics g, CommandBar bar, Font font, int iconPx, int gripperOffset, BarMetrics m, float dpiScale, out int columnWidth)
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

            int height = MeasureItemHeight(g, item, font, iconPx, m,
                bar.BarType != CommandBarType.MenuBar);
            item.Bounds = new Rectangle(x, y, cell, height);
            y += height;
        }

        return y + m.TopInset;
    }

    /// <summary>
    /// A "swatch": an icon-only button that packs into a palette grid cell (Office's
    /// colour swatches). Everything else (text buttons like Automatic / More Colors,
    /// popups, separators) breaks the grid into a full-width row.
    /// </summary>
    internal static bool IsSwatch(CommandBarItem item)
        => item is CommandBarButton b
           && b.DisplayStyle == CommandItemDisplayStyle.ImageOnly
           && b.Command.Image is not null;

    /// <summary>
    /// Lays a bar out as a wrapping grid of <paramref name="columns"/> square swatch
    /// cells; non-swatch items flush the current grid row and take a full-width row.
    /// Assigns each item its <see cref="CommandBarItem.Bounds"/>. Returns the total
    /// height and, via <paramref name="totalWidth"/>, the total width. Used for both
    /// the dropdown menu and a torn-off palette (see <see cref="CommandBar.PaletteColumns"/>).
    /// </summary>
    internal static int LayoutGrid(
        Graphics g, CommandBar bar, Font font, int iconPx, BarMetrics m, float dpiScale, int columns, out int totalWidth)
    {
        columns = Math.Max(1, columns);
        int cell = iconPx + (2 * m.ButtonHPad);                      // square swatch cell
        int rowHeight = Math.Max(iconPx, font.Height) + (2 * m.ContentVPad);
        int inset = m.TopInset;

        // Widest full-width (non-swatch) row so text items aren't clipped.
        int fullWidth = 0;
        foreach (var item in bar.Items)
            if (item.Visible && !IsSwatch(item) && item is not CommandBarSeparator)
                fullWidth = Math.Max(fullWidth, MeasureItemWidth(g, item, font, iconPx, m, dpiScale, false));

        int contentWidth = Math.Max(columns * cell, fullWidth);
        totalWidth = contentWidth + (2 * inset);

        int x0 = inset;
        int y = inset;
        int col = 0;
        int rowTop = y;
        foreach (var item in bar.Items)
        {
            if (!item.Visible)
            {
                item.Bounds = Rectangle.Empty;
                continue;
            }
            if (IsSwatch(item))
            {
                if (col == 0)
                    rowTop = y;
                item.Bounds = new Rectangle(x0 + (col * cell), rowTop, cell, cell);
                if (++col >= columns)
                {
                    col = 0;
                    y = rowTop + cell;
                }
            }
            else
            {
                if (col > 0) // flush a partial swatch row
                {
                    col = 0;
                    y = rowTop + cell;
                }
                int h = item is CommandBarSeparator ? m.SeparatorThickness : rowHeight;
                item.Bounds = new Rectangle(x0, y, contentWidth, h);
                y += h;
            }
        }
        if (col > 0) // flush a trailing partial swatch row
            y = rowTop + cell;

        return y + inset;
    }

    internal static int MeasureItemHeight(Graphics g, CommandBarItem item, Font font, int iconPx, BarMetrics m, bool popupArrow = false)
    {
        switch (item)
        {
            case CommandBarSeparator:
                return m.SeparatorThickness;

            case CommandBarLabel:
                return font.Height + (2 * m.ContentVPad);

            // A toolbar popup reserves an arrow strip below its content (like a
            // split button); an icon-less popup falls back to its caption, which
            // is drawn rotated, so its text length drives the cell height.
            case CommandBarPopupItem popup:
            {
                int strip = popupArrow ? m.ArrowWidth : 0;
                int core = popup.Image is not null
                    ? iconPx
                    : Math.Max(iconPx, MeasureText(g, popup.DisplayText, font));
                return core + (2 * m.ContentVPad) + strip;
            }

            // Split buttons reserve an arrow strip below the icon.
            case CommandBarSplitButton:
                return iconPx + (2 * m.ContentVPad) + m.ArrowWidth;

            // A vertical combo collapses to a drop-down button (icon + arrow strip),
            // matching Office; the arrow strip sits below the icon like a split button.
            case CommandBarComboBox:
                return iconPx + (2 * m.ContentVPad) + m.ArrowWidth;

            // A plain button with no icon falls back to its caption, drawn rotated
            // on a vertical bar — so its text length (not the icon) sets the height.
            case CommandBarCommandItem cmd:
            {
                bool hasImage = cmd.DisplayStyle != CommandItemDisplayStyle.TextOnly && cmd.Command.Image is not null;
                if (hasImage || string.IsNullOrEmpty(cmd.DisplayText))
                    return iconPx + (2 * m.ContentVPad);
                return Math.Max(iconPx, MeasureText(g, cmd.DisplayText, font)) + (2 * m.ContentVPad);
            }

            default:
                return iconPx + (2 * m.ContentVPad);
        }
    }

    internal static int MeasureItemWidth(Graphics g, CommandBarItem item, Font font, int iconPx, BarMetrics m, float dpiScale, bool iconOnly = false, bool popupArrow = false)
    {
        switch (item)
        {
            case CommandBarSeparator:
                return m.SeparatorThickness;

            case CommandBarPopupItem popup:
            {
                // Toolbar popups reserve a dropdown-arrow column and, when they carry
                // an image, size to the icon (they draw the image, not the caption);
                // menu-bar entries stay text-only with no arrow.
                int core = (popupArrow && popup.Image is not null)
                    ? iconPx
                    : MeasureText(g, popup.Text, font);
                return core + (2 * m.MenuItemHPad) + (popupArrow ? m.ArrowWidth : 0);
            }

            case CommandBarLabel label:
                return MeasureText(g, label.Text, font) + (2 * m.ButtonHPad);

            case CommandBarComboBox combo:
                return ComboBoxWidthPx(combo, iconPx, dpiScale) + (2 * m.ButtonHPad);

            case CommandBarCommandItem cmd:
            {
                bool hasImage = cmd.DisplayStyle != CommandItemDisplayStyle.TextOnly && cmd.Command.Image is not null;
                // Show the caption when the style allows it, OR when there's no
                // image to show — so an icon-less button (e.g. a command with no
                // picture) falls back to its text instead of measuring blank.
                // Icon-only (vertical toolbar / torn-off palette) drops the caption
                // whenever there is an icon, matching how the item is drawn.
                bool hasText = !string.IsNullOrEmpty(cmd.DisplayText)
                    && (iconOnly ? !hasImage : cmd.DisplayStyle != CommandItemDisplayStyle.ImageOnly || !hasImage);

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
