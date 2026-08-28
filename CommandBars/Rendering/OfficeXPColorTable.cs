using System.Drawing;

namespace CommandBars.Rendering;

/// <summary>
/// The Office XP palette: flat gray bars (no gradients — begin == end) with the
/// signature flat light-blue selection and a solid blue border. Square corners
/// are produced by <c>OfficeXPRenderer</c> setting the chunk radius to 0.
/// </summary>
public sealed class OfficeXPColorTable : CommandBarColorTable
{
    private static Color C(int r, int g, int b) => Color.FromArgb(r, g, b);

    // Flat bars — a lighter chunk on a slightly darker band reads as raised.
    public override Color BarGradientBegin => C(243, 242, 238);
    public override Color BarGradientMiddle => C(243, 242, 238);
    public override Color BarGradientEnd => C(243, 242, 238);
    public override Color BarBorder => C(127, 127, 127);

    public override Color MenuBarGradientBegin => C(237, 236, 231);
    public override Color MenuBarGradientEnd => C(237, 236, 231);

    public override Color BandGradientBegin => C(224, 223, 216);
    public override Color BandGradientEnd => C(224, 223, 216);
    public override Color RaisedBorder => C(150, 150, 150);

    public override Color ChevronGradientBegin => C(212, 211, 205);
    public override Color ChevronGradientEnd => C(212, 211, 205);

    // Drop preview overlay
    public override Color DropPreview => C(49, 106, 197);

    // Flat blue selection (begin == end), solid blue border.
    public override Color ButtonHotBegin => C(193, 210, 232);
    public override Color ButtonHotEnd => C(193, 210, 232);
    public override Color ButtonHotBorder => C(49, 106, 197);

    public override Color ButtonPressedBegin => C(152, 181, 226);
    public override Color ButtonPressedEnd => C(152, 181, 226);
    public override Color ButtonPressedBorder => C(49, 106, 197);

    public override Color ButtonCheckedBegin => C(226, 231, 242);
    public override Color ButtonCheckedEnd => C(226, 231, 242);
    public override Color ButtonCheckedBorder => C(49, 106, 197);

    // XP keeps an open menu owner's normal flat bar background. The outline
    // and popup connection communicate the open state without a click fill.
    public override Color MenuOpenBegin => C(224, 223, 216);
    public override Color MenuOpenEnd => C(224, 223, 216);
    public override Color MenuOpenBorder => C(127, 127, 127);

    public override Color SeparatorDark => C(160, 160, 160);
    public override Color SeparatorLight => C(255, 255, 255);
    public override Color GripperDark => C(150, 150, 150);
    public override Color GripperLight => C(255, 255, 255);

    public override Color Text => C(0, 0, 0);
    public override Color DisabledText => C(141, 141, 141);

    public override Color MenuBackground => C(255, 255, 255);
    public override Color MenuBorder => C(127, 127, 127);
    public override Color ImageMarginBegin => C(233, 231, 224);
    public override Color ImageMarginEnd => C(233, 231, 224);
    public override Color MenuItemSelectedBegin => C(193, 210, 232);
    public override Color MenuItemSelectedEnd => C(193, 210, 232);
    public override Color MenuItemSelectedBorder => C(49, 106, 197);
    public override Color MenuText => C(0, 0, 0);
    public override Color DisabledMenuText => C(141, 141, 141);
}
