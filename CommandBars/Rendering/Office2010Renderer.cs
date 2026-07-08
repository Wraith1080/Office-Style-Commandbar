using System.Drawing;

namespace CommandBars.Rendering;

/// <summary>
/// A flat "Office 2010" silver palette: light, low-contrast silver bars and
/// band with the warm gold hover/pressed highlight of the Office family. Flatter
/// and lighter than the glassy 2007 blue.
/// </summary>
public sealed class Office2010ColorTable : CommandBarColorTable
{
    private static Color C(int r, int g, int b) => Color.FromArgb(r, g, b);

    // Silver toolbar chunk (nearly flat gradient)
    public override Color BarGradientBegin => C(250, 251, 252);
    public override Color BarGradientMiddle => C(240, 241, 243);
    public override Color BarGradientEnd => C(226, 229, 233);
    public override Color BarBorder => C(171, 175, 183);

    // Menu bar
    public override Color MenuBarGradientBegin => C(245, 246, 248);
    public override Color MenuBarGradientEnd => C(228, 231, 235);

    // Band (rebar)
    public override Color BandGradientBegin => C(237, 239, 241);
    public override Color BandGradientEnd => C(214, 218, 223);
    public override Color RaisedBorder => C(171, 175, 183);

    // Chevron nub
    public override Color ChevronGradientBegin => C(214, 218, 223);
    public override Color ChevronGradientEnd => C(186, 191, 199);

    // Drop preview overlay
    public override Color DropPreview => C(90, 140, 210);

    // Hot (hover) — gold glow
    public override Color ButtonHotBegin => C(255, 252, 226);
    public override Color ButtonHotEnd => C(255, 235, 170);
    public override Color ButtonHotBorder => C(227, 195, 101);

    // Pressed — deeper gold
    public override Color ButtonPressedBegin => C(255, 231, 162);
    public override Color ButtonPressedEnd => C(255, 205, 120);
    public override Color ButtonPressedBorder => C(214, 166, 72);

    // Checked (latched)
    public override Color ButtonCheckedBegin => C(255, 242, 200);
    public override Color ButtonCheckedEnd => C(255, 226, 160);
    public override Color ButtonCheckedBorder => C(227, 195, 101);

    public override Color SeparatorDark => C(197, 200, 206);
    public override Color SeparatorLight => C(255, 255, 255);
    public override Color GripperDark => C(160, 164, 172);
    public override Color GripperLight => C(255, 255, 255);

    public override Color Text => C(30, 30, 30);
    public override Color DisabledText => C(150, 150, 150);

    public override Color MenuBackground => C(245, 246, 248);
    public override Color MenuBorder => C(157, 161, 169);
    public override Color ImageMarginBegin => C(233, 235, 238);
    public override Color ImageMarginEnd => C(214, 218, 223);
    public override Color MenuItemSelectedBegin => C(255, 252, 226);
    public override Color MenuItemSelectedEnd => C(255, 235, 170);
    public override Color MenuItemSelectedBorder => C(227, 195, 101);
    public override Color MenuText => C(30, 30, 30);
    public override Color DisabledMenuText => C(150, 150, 150);
}

/// <summary>
/// The Office 2010 look: the 2003 renderer's drawing with the flat silver color
/// table and square corners.
/// </summary>
public sealed class Office2010Renderer : Office2003Renderer
{
    public override CommandBarColorTable Colors { get; } = new Office2010ColorTable();

    protected override int ChunkRadius => 0;
}
