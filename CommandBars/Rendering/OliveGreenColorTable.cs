using System.Drawing;

namespace CommandBars.Rendering;

/// <summary>
/// The Windows XP "Olive Green" palette — a muted, grayish sage/khaki take on
/// the Office 2003 chrome (the XP Olive Green theme was deliberately desaturated,
/// not a vivid green) with a warm gold hover/pressed highlight that complements
/// it, mirroring how Office 2003 pairs blue chrome with warm orange.
/// </summary>
public sealed class OliveGreenColorTable : CommandBarColorTable
{
    private static Color C(int r, int g, int b) => Color.FromArgb(r, g, b);

    // Toolbar chunk — vertical muted olive gradient
    public override Color BarGradientBegin => C(224, 240, 222);
    public override Color BarGradientMiddle => C(196, 216, 184);
    public override Color BarGradientEnd => C(158, 176, 138);
    public override Color BarBorder => C(104, 126, 92);

    // Menu bar — its own, slightly flatter olive
    public override Color MenuBarGradientBegin => C(224, 240, 222);
    public override Color MenuBarGradientEnd => C(172, 194, 157);

    // Dock band (rebar) behind the toolbar chunks
    public override Color BandGradientBegin => C(196, 216, 184);
    public override Color BandGradientEnd => C(144, 166, 127);
    public override Color RaisedBorder => C(110, 134, 96);

    // Overflow chevron nub — a darker olive than the chunk
    public override Color ChevronGradientBegin => C(168, 184, 146);
    public override Color ChevronGradientEnd => C(110, 134, 96);

    // Drop preview overlay
    public override Color DropPreview => C(104, 126, 92);

    // Hot (hover) — warm gold
    public override Color ButtonHotBegin => C(255, 248, 214);
    public override Color ButtonHotEnd => C(255, 224, 120);
    public override Color ButtonHotBorder => C(214, 165, 40);

    // Pressed — deeper gold
    public override Color ButtonPressedBegin => C(250, 214, 110);
    public override Color ButtonPressedEnd => C(240, 196, 90);
    public override Color ButtonPressedBorder => C(178, 134, 30);

    // Checked (latched)
    public override Color ButtonCheckedBegin => C(255, 235, 160);
    public override Color ButtonCheckedEnd => C(250, 220, 130);
    public override Color ButtonCheckedBorder => C(214, 165, 40);

    // Separators / grippers
    public override Color SeparatorDark => C(124, 146, 110);
    public override Color SeparatorLight => C(255, 255, 255);
    public override Color GripperDark => C(106, 128, 94);
    public override Color GripperLight => C(255, 255, 255);

    // Text
    public override Color Text => C(0, 0, 0);
    public override Color DisabledText => C(141, 141, 141);

    // Popup menus
    public override Color MenuBackground => C(250, 250, 242);
    public override Color MenuBorder => C(110, 134, 96);
    public override Color ImageMarginBegin => C(224, 240, 222);
    public override Color ImageMarginEnd => C(180, 202, 165);
    public override Color MenuItemSelectedBegin => C(255, 248, 214);
    public override Color MenuItemSelectedEnd => C(255, 224, 120);
    public override Color MenuItemSelectedBorder => C(214, 165, 40);
    public override Color MenuText => C(0, 0, 0);
    public override Color DisabledMenuText => C(141, 141, 141);
}
