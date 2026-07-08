using System.Drawing;

namespace CommandBars.Rendering;

/// <summary>
/// Named colors a <see cref="CommandBarRenderer"/> draws with. Themes subclass
/// this and override only what differs; the base provides neutral system-color
/// defaults so a partial theme still renders.
/// </summary>
public class CommandBarColorTable
{
    // --- Bar backgrounds ---------------------------------------------------
    public virtual Color BarGradientBegin => SystemColors.Control;
    public virtual Color BarGradientMiddle => SystemColors.Control;
    public virtual Color BarGradientEnd => SystemColors.ControlDark;
    public virtual Color MenuBarGradientBegin => SystemColors.Control;
    public virtual Color MenuBarGradientEnd => SystemColors.Control;
    public virtual Color BarBorder => SystemColors.ControlDark;

    // --- Dock band (the raised rebar behind toolbar chunks) ---------------
    public virtual Color BandGradientBegin => SystemColors.Control;
    public virtual Color BandGradientEnd => SystemColors.ControlDark;
    public virtual Color RaisedBorder => SystemColors.ControlDark;

    // --- Overflow chevron nub (a darker gradient at the toolbar's end) ----
    public virtual Color ChevronGradientBegin => SystemColors.ControlDark;
    public virtual Color ChevronGradientEnd => SystemColors.ControlDarkDark;

    // --- Drag-and-drop preview overlay ------------------------------------
    public virtual Color DropPreview => SystemColors.Highlight;

    // --- Buttons: hot / pressed / checked ---------------------------------
    public virtual Color ButtonHotBegin => SystemColors.ControlLightLight;
    public virtual Color ButtonHotEnd => SystemColors.ControlLight;
    public virtual Color ButtonHotBorder => SystemColors.Highlight;

    public virtual Color ButtonPressedBegin => SystemColors.ControlLight;
    public virtual Color ButtonPressedEnd => SystemColors.ControlLightLight;
    public virtual Color ButtonPressedBorder => SystemColors.Highlight;

    public virtual Color ButtonCheckedBegin => SystemColors.ControlLight;
    public virtual Color ButtonCheckedEnd => SystemColors.ControlLightLight;
    public virtual Color ButtonCheckedBorder => SystemColors.Highlight;

    // --- Separators / grippers --------------------------------------------
    public virtual Color SeparatorDark => SystemColors.ControlDark;
    public virtual Color SeparatorLight => SystemColors.ControlLightLight;
    public virtual Color GripperDark => SystemColors.ControlDark;
    public virtual Color GripperLight => SystemColors.ControlLightLight;

    // --- Text --------------------------------------------------------------
    public virtual Color Text => SystemColors.ControlText;
    public virtual Color DisabledText => SystemColors.GrayText;

    // --- Popup menus -------------------------------------------------------
    public virtual Color MenuBackground => SystemColors.Menu;
    public virtual Color MenuBorder => SystemColors.ControlDark;
    public virtual Color ImageMarginBegin => SystemColors.Control;
    public virtual Color ImageMarginEnd => SystemColors.ControlDark;
    public virtual Color MenuItemSelectedBegin => SystemColors.ControlLightLight;
    public virtual Color MenuItemSelectedEnd => SystemColors.ControlLight;
    public virtual Color MenuItemSelectedBorder => SystemColors.Highlight;
    public virtual Color MenuText => SystemColors.MenuText;
    public virtual Color DisabledMenuText => SystemColors.GrayText;
}
