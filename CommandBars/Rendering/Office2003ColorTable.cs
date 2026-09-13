using System.Drawing;

namespace CommandBars.Rendering;

/// <summary>
/// Office 2003 Default: Office XP's warm gray surfaces and blue selections,
/// with graduated highlights and shadows for the Office 2003 chrome.
/// </summary>
public sealed class Office2003ColorTable : CommandBarColorTable
{
    private static Color C(int r, int g, int b) => Color.FromArgb(r, g, b);

    public override Color BarGradientBegin => C(252, 252, 249);
    public override Color BarGradientMiddle => C(243, 242, 238);
    public override Color BarGradientEnd => C(224, 223, 216);
    public override Color BarBorder => C(127, 127, 127);

    public override Color MenuBarGradientBegin => C(247, 246, 242);
    public override Color MenuBarGradientEnd => C(224, 223, 216);

    public override Color BandGradientBegin => C(237, 236, 231);
    public override Color BandGradientEnd => C(212, 211, 203);
    public override Color RaisedBorder => C(150, 150, 150);

    public override Color ChevronGradientBegin => C(230, 229, 223);
    public override Color ChevronGradientEnd => C(190, 189, 181);

    public override Color DropPreview => C(49, 106, 197);

    public override Color ButtonHotBegin => C(224, 234, 247);
    public override Color ButtonHotEnd => C(193, 210, 232);
    public override Color ButtonHotBorder => C(49, 106, 197);

    public override Color ButtonPressedBegin => C(152, 181, 226);
    public override Color ButtonPressedEnd => C(184, 205, 235);
    public override Color ButtonPressedBorder => C(49, 106, 197);

    public override Color ButtonCheckedBegin => C(239, 243, 250);
    public override Color ButtonCheckedEnd => C(206, 217, 238);
    public override Color ButtonCheckedBorder => C(49, 106, 197);

    public override Color MenuOpenBegin => C(247, 246, 242);
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
    public override Color ImageMarginBegin => C(247, 246, 242);
    public override Color ImageMarginEnd => C(220, 218, 208);
    public override Color MenuItemSelectedBegin => C(224, 234, 247);
    public override Color MenuItemSelectedEnd => C(193, 210, 232);
    public override Color MenuItemSelectedBorder => C(49, 106, 197);
    public override Color MenuText => C(0, 0, 0);
    public override Color DisabledMenuText => C(141, 141, 141);
}
