using System.Drawing;

namespace CommandBars.Rendering;

/// <summary>
/// The Office 2007 (blue) palette: light glassy blue bars and band, with the
/// warm gold hover/pressed highlight used throughout the 2007 ribbon UI.
/// </summary>
public sealed class Office2007ColorTable : CommandBarColorTable
{
    private static Color C(int r, int g, int b) => Color.FromArgb(r, g, b);

    // Glassy toolbar chunk
    public override Color BarGradientBegin => C(250, 252, 254);
    public override Color BarGradientMiddle => C(229, 239, 250);
    public override Color BarGradientEnd => C(207, 224, 245);
    public override Color BarBorder => C(101, 147, 207);

    // Menu bar
    public override Color MenuBarGradientBegin => C(233, 241, 251);
    public override Color MenuBarGradientEnd => C(196, 218, 240);

    // Band (rebar)
    public override Color BandGradientBegin => C(233, 241, 251);
    public override Color BandGradientEnd => C(179, 204, 235);
    public override Color RaisedBorder => C(130, 165, 205);

    // Chevron nub
    public override Color ChevronGradientBegin => C(190, 212, 240);
    public override Color ChevronGradientEnd => C(147, 184, 224);

    // Drop preview overlay
    public override Color DropPreview => C(75, 125, 200);

    // Hot (hover) — gold glow
    public override Color ButtonHotBegin => C(255, 254, 228);
    public override Color ButtonHotEnd => C(255, 232, 166);
    public override Color ButtonHotBorder => C(242, 205, 96);

    // Pressed — deeper gold
    public override Color ButtonPressedBegin => C(255, 226, 162);
    public override Color ButtonPressedEnd => C(255, 190, 102);
    public override Color ButtonPressedBorder => C(226, 170, 66);

    // Checked (latched)
    public override Color ButtonCheckedBegin => C(255, 241, 196);
    public override Color ButtonCheckedEnd => C(255, 222, 155);
    public override Color ButtonCheckedBorder => C(242, 205, 96);

    public override Color SeparatorDark => C(197, 208, 226);
    public override Color SeparatorLight => C(255, 255, 255);
    public override Color GripperDark => C(150, 170, 200);
    public override Color GripperLight => C(255, 255, 255);

    public override Color Text => C(0, 0, 0);
    public override Color DisabledText => C(141, 141, 141);

    public override Color MenuBackground => C(250, 251, 253);
    public override Color MenuBorder => C(130, 165, 205);
    public override Color ImageMarginBegin => C(233, 240, 250);
    public override Color ImageMarginEnd => C(202, 221, 244);
    public override Color MenuItemSelectedBegin => C(255, 254, 228);
    public override Color MenuItemSelectedEnd => C(255, 232, 166);
    public override Color MenuItemSelectedBorder => C(242, 205, 96);
    public override Color MenuText => C(0, 0, 0);
    public override Color DisabledMenuText => C(141, 141, 141);
}
