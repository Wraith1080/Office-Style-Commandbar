namespace CommandBars.Controls;

/// <summary>
/// Layout spacing values scaled for the current DPI. Base values are in logical
/// (96-DPI) pixels; <see cref="For"/> multiplies them by the DPI scale so the
/// whole bar grows proportionally on high-DPI displays.
/// </summary>
internal readonly struct BarMetrics
{
    public int ContentVPad { get; }        // space above/below the tallest content
    public int ButtonHPad { get; }         // inset on each side of button content
    public int MenuItemHPad { get; }       // extra inset for menu-bar entries
    public int TextImageGap { get; }       // gap between image and text
    public int SeparatorThickness { get; }
    public int ArrowWidth { get; }         // dropdown arrow column on split buttons
    public int TopInset { get; }

    private BarMetrics(float scale)
    {
        ContentVPad = R(4, scale);
        ButtonHPad = R(3, scale);
        MenuItemHPad = R(7, scale);
        TextImageGap = R(3, scale);
        SeparatorThickness = R(7, scale);
        ArrowWidth = R(12, scale);
        TopInset = Math.Max(1, R(1, scale));
    }

    public static BarMetrics For(float scale) => new(scale);

    private static int R(int baseValue, float scale) => (int)Math.Round(baseValue * scale);
}
