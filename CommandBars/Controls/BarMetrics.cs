using CommandBars.Model;

namespace CommandBars.Controls;

/// <summary>
/// Layout spacing values scaled for the current DPI. Base values are in logical
/// (96-DPI) pixels; <see cref="For"/> multiplies them by the DPI scale so the
/// whole bar grows proportionally on high-DPI displays. A few values
/// (<see cref="ArrowWidth"/>) additionally grow with the bar's icon size so that
/// hit targets stay comfortably clickable on large toolbars.
/// </summary>
internal readonly struct BarMetrics
{
    public int ContentVPad { get; }        // space above/below the tallest content
    public int ButtonHPad { get; }         // inset on each side of button content
    public int MenuItemHPad { get; }       // extra inset for menu-bar entries
    public int TextImageGap { get; }       // gap between image and text
    public int SeparatorThickness { get; }
    public int ArrowWidth { get; }         // dropdown arrow column on split buttons / combo strip
    public int TopInset { get; }

    private BarMetrics(float scale, int iconPx, bool fluent)
    {
        ContentVPad = R(4, scale);
        ButtonHPad = R(3, scale);
        MenuItemHPad = R(7, scale);
        TextImageGap = R(3, scale);
        SeparatorThickness = R(7, scale);
        // The dropdown-arrow column grows with the icon size (never below its
        // base 12 logical px) so a split button's arrow half and a vertical
        // combo's arrow strip stay large enough to click on big toolbars.
        ArrowWidth = (int)Math.Round((fluent ? 18 : 12) * scale * IconGrow(scale, iconPx));
        TopInset = Math.Max(1, R(1, scale));
    }

    /// <summary>
    /// Builds metrics for the given DPI scale and icon size (device px). Pass
    /// <paramref name="iconPx"/> = 0 to keep arrow columns at their base size.
    /// </summary>
    public static BarMetrics For(float scale, int iconPx = 0, bool fluent = false) => new(scale, iconPx, fluent);

    // How much icon-size-sensitive chrome grows: 1.0 at (or below) the default
    // icon size, scaling linearly with the icon size above it.
    private static float IconGrow(float scale, int iconPx)
        => iconPx <= 0 ? 1f : Math.Max(1f, iconPx / (Math.Max(0.01f, scale) * IconSizes.Default));

    private static int R(int baseValue, float scale) => (int)Math.Round(baseValue * scale);
}
