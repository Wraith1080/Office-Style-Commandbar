using System.Drawing;

namespace CommandBars.Rendering;

/// <summary>Deep teal glass chrome with pale, readable menu surfaces.</summary>
public sealed class VistaAuroraColorTable : CommandBarColorTable
{
    private static Color C(int r, int g, int b) => Color.FromArgb(r, g, b);

    public override Color BarGradientBegin => C(75, 129, 145);
    public override Color BarGradientMiddle => C(45, 104, 123);
    public override Color BarGradientEnd => C(29, 101, 115);
    public override Color BarBorder => C(23, 65, 80);
    public override Color MenuBarGradientBegin => C(54, 112, 132);
    public override Color MenuBarGradientEnd => C(24, 72, 93);
    public override Color BandGradientBegin => C(44, 107, 127);
    public override Color BandGradientEnd => C(19, 67, 88);
    public override Color RaisedBorder => C(18, 61, 77);
    public override Color ChevronGradientBegin => C(55, 115, 135);
    public override Color ChevronGradientEnd => C(18, 70, 91);
    public override Color DropPreview => C(34, 158, 175);

    public override Color ButtonHotBegin => C(65, 132, 153);
    public override Color ButtonHotEnd => C(29, 94, 118);
    public override Color ButtonHotBorder => C(166, 228, 237);
    public override Color ButtonPressedBegin => C(23, 69, 91);
    public override Color ButtonPressedEnd => C(37, 103, 122);
    public override Color ButtonPressedBorder => C(11, 48, 69);
    public override Color ButtonCheckedBegin => C(37, 104, 116);
    public override Color ButtonCheckedEnd => C(24, 82, 98);
    public override Color ButtonCheckedBorder => C(122, 208, 208);
    public override Color MenuOpenBegin => ButtonPressedBegin;
    public override Color MenuOpenEnd => ButtonPressedEnd;
    public override Color MenuOpenBorder => ButtonHotBorder;

    public override Color SeparatorDark => C(53, 108, 124);
    public override Color SeparatorLight => C(168, 213, 220);
    public override Color GripperDark => C(22, 67, 82);
    public override Color GripperLight => C(139, 196, 204);
    public override Color MenuGripperDots => C(61, 103, 120);
    public override Color Text => C(248, 253, 255);
    public override Color DisabledText => C(156, 187, 197);

    public override Color MenuBackground => C(248, 252, 252);
    public override Color MenuBorder => C(90, 133, 148);
    public override Color ImageMarginBegin => C(236, 247, 249);
    public override Color ImageMarginEnd => C(222, 238, 242);
    public override Color MenuItemSelectedBegin => C(232, 248, 253);
    public override Color MenuItemSelectedEnd => C(183, 224, 239);
    public override Color MenuItemSelectedBorder => C(109, 175, 200);
    public override Color MenuText => C(25, 52, 67);
    public override Color DisabledMenuText => C(119, 139, 147);
}

// Dialogs and editable fields use light surfaces independently of the dark bar.
internal sealed class VistaAuroraDialogColorTable : CommandBarDialogColorTable
{
    public VistaAuroraDialogColorTable(CommandBarColorTable colors) : base(colors) { }
    public override Color Window => Color.FromArgb(231, 242, 245);
    public override Color HeaderBegin => Color.FromArgb(248, 253, 254);
    public override Color HeaderEnd => Window;
    public override Color ActiveTab => Surface;
    public override Color InactiveTab => Window;
    public override Color TabBody => Window;
    public override Color ButtonBegin => Color.FromArgb(249, 254, 255);
    public override Color ButtonEnd => Color.FromArgb(208, 231, 240);
    public override Color ButtonBorder => Border;
    public override Color ButtonHotBegin => Color.FromArgb(235, 251, 255);
    public override Color ButtonHotEnd => Color.FromArgb(182, 226, 242);
    public override Color ButtonPressedBegin => Color.FromArgb(168, 211, 228);
    public override Color ButtonPressedEnd => Color.FromArgb(221, 244, 250);
    public override Color ButtonHotBorder => Color.FromArgb(73, 151, 181);
    public override Color ButtonPressedBorder => ButtonHotBorder;
    public override Color ButtonText => Text;
    public override Color SelectionBackground => ButtonHotEnd;
    public override Color SelectionText => Text;
}
