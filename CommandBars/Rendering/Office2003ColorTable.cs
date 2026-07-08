using System.Drawing;

namespace CommandBars.Rendering;

/// <summary>
/// The Office 2003 "Luna Blue" palette — saturated sky-blue bars and band with
/// the classic warm orange hover/pressed highlight. Values approximate the
/// original scheme and are all kept here for easy tweaking.
/// </summary>
public sealed class Office2003ColorTable : CommandBarColorTable
{
    private static Color C(int r, int g, int b) => Color.FromArgb(r, g, b);

    // Toolbar chunk — vertical sky-blue gradient
    public override Color BarGradientBegin => C(227, 239, 255);
    public override Color BarGradientMiddle => C(196, 219, 249);
    public override Color BarGradientEnd => C(147, 184, 233);
    public override Color BarBorder => C(59, 97, 156);

    // Menu bar — its own, slightly flatter blue
    public override Color MenuBarGradientBegin => C(227, 239, 255);
    public override Color MenuBarGradientEnd => C(171, 199, 233);

    // Dock band (rebar) behind the toolbar chunks
    public override Color BandGradientBegin => C(196, 219, 249);
    public override Color BandGradientEnd => C(140, 178, 232);
    public override Color RaisedBorder => C(102, 141, 197);

    // Overflow chevron nub — a darker blue than the chunk
    public override Color ChevronGradientBegin => C(163, 194, 234);
    public override Color ChevronGradientEnd => C(101, 145, 205);

    // Drop preview overlay
    public override Color DropPreview => C(51, 94, 168);

    // Hot (hover) — warm orange
    public override Color ButtonHotBegin => C(255, 251, 230);
    public override Color ButtonHotEnd => C(255, 214, 122);
    public override Color ButtonHotBorder => C(242, 149, 54);

    // Pressed — deeper orange
    public override Color ButtonPressedBegin => C(254, 211, 128);
    public override Color ButtonPressedEnd => C(255, 187, 105);
    public override Color ButtonPressedBorder => C(210, 128, 40);

    // Checked (latched)
    public override Color ButtonCheckedBegin => C(255, 230, 158);
    public override Color ButtonCheckedEnd => C(255, 213, 131);
    public override Color ButtonCheckedBorder => C(242, 149, 54);

    // Separators / grippers
    public override Color SeparatorDark => C(106, 140, 203);
    public override Color SeparatorLight => C(255, 255, 255);
    public override Color GripperDark => C(96, 128, 182);
    public override Color GripperLight => C(255, 255, 255);

    // Text
    public override Color Text => C(0, 0, 0);
    public override Color DisabledText => C(141, 141, 141);

    // Popup menus
    public override Color MenuBackground => C(250, 250, 251);
    public override Color MenuBorder => C(102, 141, 197);
    public override Color ImageMarginBegin => C(227, 239, 255);
    public override Color ImageMarginEnd => C(179, 203, 236);
    public override Color MenuItemSelectedBegin => C(255, 251, 230);
    public override Color MenuItemSelectedEnd => C(255, 214, 122);
    public override Color MenuItemSelectedBorder => C(242, 149, 54);
    public override Color MenuText => C(0, 0, 0);
    public override Color DisabledMenuText => C(141, 141, 141);
}
