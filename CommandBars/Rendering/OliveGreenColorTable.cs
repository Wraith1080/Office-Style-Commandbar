using System.Drawing;

namespace CommandBars.Rendering;

/// <summary>
/// The Windows XP "Olive Green" palette — sampled from the real XP Olive Green
/// theme. The chrome is a muted khaki/sage (red and green nearly equal, blue
/// well below) rather than a saturated green: the menu bar and band are flat
/// khaki, and the toolbar chunk is a light sage gradient. The hover/pressed
/// accent stays the warm Office orange, matching the original theme.
/// </summary>
public sealed class OliveGreenColorTable : CommandBarColorTable
{
    private static Color C(int r, int g, int b) => Color.FromArgb(r, g, b);

    // Toolbar chunk — light sage gradient (top light, bottom medium olive)
    public override Color BarGradientBegin => C(246, 248, 224);
    public override Color BarGradientMiddle => C(214, 226, 179);
    public override Color BarGradientEnd => C(182, 197, 144);
    public override Color BarBorder => C(120, 138, 92);

    // Menu bar — flat khaki (red ≈ green, blue well below)
    public override Color MenuBarGradientBegin => C(222, 222, 179);
    public override Color MenuBarGradientEnd => C(222, 222, 179);

    // Dock band (rebar) behind the toolbar chunks — sage-khaki
    public override Color BandGradientBegin => C(210, 222, 172);
    public override Color BandGradientEnd => C(196, 210, 158);
    public override Color RaisedBorder => C(150, 165, 120);

    // Overflow chevron nub — a darker olive than the chunk
    public override Color ChevronGradientBegin => C(170, 185, 138);
    public override Color ChevronGradientEnd => C(130, 150, 105);

    // Drop preview overlay
    public override Color DropPreview => C(120, 138, 92);

    // Hot (hover) — warm orange (the accent stays orange, as in the real theme)
    public override Color ButtonHotBegin => C(255, 240, 200);
    public override Color ButtonHotEnd => C(255, 190, 95);
    public override Color ButtonHotBorder => C(230, 150, 45);

    // Pressed — deeper orange
    public override Color ButtonPressedBegin => C(250, 200, 100);
    public override Color ButtonPressedEnd => C(240, 170, 70);
    public override Color ButtonPressedBorder => C(200, 125, 35);

    // Checked (latched)
    public override Color ButtonCheckedBegin => C(255, 220, 140);
    public override Color ButtonCheckedEnd => C(250, 200, 110);
    public override Color ButtonCheckedBorder => C(230, 150, 45);

    // Separators / grippers
    public override Color SeparatorDark => C(130, 148, 102);
    public override Color SeparatorLight => C(255, 255, 255);
    public override Color GripperDark => C(110, 128, 84);
    public override Color GripperLight => C(255, 255, 255);

    // Text
    public override Color Text => C(0, 0, 0);
    public override Color DisabledText => C(141, 141, 141);

    // Popup menus
    public override Color MenuBackground => C(250, 250, 242);
    public override Color MenuBorder => C(150, 165, 120);
    public override Color ImageMarginBegin => C(246, 248, 224);
    public override Color ImageMarginEnd => C(210, 222, 172);
    public override Color MenuItemSelectedBegin => C(255, 240, 200);
    public override Color MenuItemSelectedEnd => C(255, 190, 95);
    public override Color MenuItemSelectedBorder => C(230, 150, 45);
    public override Color MenuText => C(0, 0, 0);
    public override Color DisabledMenuText => C(141, 141, 141);
}
