using System.Drawing;

namespace CommandBars.Rendering;

/// <summary>
/// A dark (charcoal) palette in the spirit of the Visual Studio dark theme:
/// dark gray bars and menus, light text, and a blue accent for hover/pressed.
/// </summary>
public sealed class DarkColorTable : CommandBarColorTable
{
    private static Color C(int r, int g, int b) => Color.FromArgb(r, g, b);

    // Charcoal toolbar chunk
    public override Color BarGradientBegin => C(69, 69, 73);
    public override Color BarGradientMiddle => C(62, 62, 66);
    public override Color BarGradientEnd => C(51, 51, 55);
    public override Color BarBorder => C(85, 85, 90);

    // Menu bar
    public override Color MenuBarGradientBegin => C(45, 45, 48);
    public override Color MenuBarGradientEnd => C(45, 45, 48);

    // Band (rebar)
    public override Color BandGradientBegin => C(55, 55, 58);
    public override Color BandGradientEnd => C(45, 45, 48);
    public override Color RaisedBorder => C(80, 80, 85);

    // Chevron nub
    public override Color ChevronGradientBegin => C(60, 60, 64);
    public override Color ChevronGradientEnd => C(45, 45, 48);

    // Drop preview overlay — accent blue
    public override Color DropPreview => C(0, 122, 204);

    // Hot (hover) — subtle lift with a blue accent border
    public override Color ButtonHotBegin => C(62, 62, 66);
    public override Color ButtonHotEnd => C(72, 72, 78);
    public override Color ButtonHotBorder => C(0, 122, 204);

    // Pressed — accent blue
    public override Color ButtonPressedBegin => C(0, 84, 153);
    public override Color ButtonPressedEnd => C(0, 122, 204);
    public override Color ButtonPressedBorder => C(0, 122, 204);

    // Checked (latched) — muted blue
    public override Color ButtonCheckedBegin => C(38, 79, 120);
    public override Color ButtonCheckedEnd => C(45, 95, 140);
    public override Color ButtonCheckedBorder => C(0, 122, 204);

    public override Color SeparatorDark => C(34, 34, 37);
    public override Color SeparatorLight => C(80, 80, 85);
    public override Color GripperDark => C(90, 90, 96);
    public override Color GripperLight => C(60, 60, 64);

    public override Color Text => C(241, 241, 241);
    public override Color DisabledText => C(127, 127, 127);

    public override Color MenuBackground => C(45, 45, 48);
    public override Color MenuBorder => C(63, 63, 70);
    public override Color ImageMarginBegin => C(55, 55, 58);
    public override Color ImageMarginEnd => C(45, 45, 48);
    public override Color MenuItemSelectedBegin => C(62, 62, 66);
    public override Color MenuItemSelectedEnd => C(72, 72, 78);
    public override Color MenuItemSelectedBorder => C(0, 122, 204);
    public override Color MenuText => C(241, 241, 241);
    public override Color DisabledMenuText => C(127, 127, 127);
}

/// <summary>
/// The dark look: the 2003 renderer's drawing with the charcoal color table and
/// square corners. All chrome colors come from the table, so text and checks
/// stay light-on-dark automatically.
/// </summary>
public sealed class DarkRenderer : Office2003Renderer
{
    public override CommandBarColorTable Colors { get; } = new DarkColorTable();

    protected override int ChunkRadius => 0;
}
