using System.Drawing;

namespace CommandBars.Rendering;

/// <summary>
/// Colors used by Office-style supporting windows such as the Customize dialog.
/// The default palette is derived from a command-bar color table, so custom
/// renderers receive matching dialog chrome without having to define one.
/// </summary>
public class CommandBarDialogColorTable
{
    private readonly CommandBarColorTable _barColors;

    public CommandBarDialogColorTable(CommandBarColorTable barColors)
    {
        _barColors = barColors ?? throw new ArgumentNullException(nameof(barColors));
    }

    /// <summary>True when the palette is intended for light-on-dark controls.</summary>
    public virtual bool IsDark => Luminance(_barColors.MenuBackground) < 0.42;

    /// <summary>Whether dialog controls use classic raised/sunken Win32 edges.</summary>
    public virtual bool UsesClassic3DChrome => false;

    public virtual Color Window => _barColors.MenuBarGradientEnd;
    public virtual Color Surface => _barColors.MenuBackground;
    public virtual Color SurfaceAlternate => Blend(_barColors.MenuBackground, _barColors.BandGradientBegin, IsDark ? 0.42f : 0.28f);
    public virtual Color InputBackground => Blend(_barColors.MenuBackground, Color.White, IsDark ? 0.035f : 0.38f);
    public virtual Color Border => _barColors.MenuBorder;
    public virtual Color ControlHighlight => _barColors.GripperLight;
    public virtual Color ControlShadow => _barColors.GripperDark;
    public virtual Color ControlDarkShadow => _barColors.MenuBorder;
    public virtual Color Text => _barColors.MenuText;
    public virtual Color DisabledText => _barColors.DisabledMenuText;
    public virtual Color InputText => _barColors.MenuText;

    public virtual Color HeaderBegin => _barColors.MenuBarGradientBegin;
    public virtual Color HeaderEnd => _barColors.MenuBarGradientEnd;
    public virtual Color ActiveTab => _barColors.BarGradientBegin;
    public virtual Color InactiveTab => HeaderEnd;
    public virtual Color TabBody => Blend(_barColors.MenuBackground, _barColors.MenuBarGradientBegin, IsDark ? 0.22f : 0.40f);
    public virtual Color Accent => _barColors.DropPreview;

    public virtual Color ButtonBegin => Blend(_barColors.MenuBackground, _barColors.BarGradientBegin, IsDark ? 0.55f : 0.48f);
    public virtual Color ButtonEnd => Blend(_barColors.MenuBackground, _barColors.BarGradientEnd, IsDark ? 0.55f : 0.48f);
    public virtual Color ButtonBorder => _barColors.BarBorder;
    public virtual Color ButtonHotBegin => _barColors.ButtonHotBegin;
    public virtual Color ButtonHotEnd => _barColors.ButtonHotEnd;
    public virtual Color ButtonHotBorder => _barColors.ButtonHotBorder;
    public virtual Color ButtonPressedBegin => _barColors.ButtonPressedBegin;
    public virtual Color ButtonPressedEnd => _barColors.ButtonPressedEnd;
    public virtual Color ButtonPressedBorder => _barColors.ButtonPressedBorder;
    public virtual Color ButtonText => _barColors.Text;
    public virtual Color SelectionBackground => _barColors.ButtonHotBegin;
    public virtual Color SelectionText => ContrastingText(_barColors.ButtonHotBegin);

    protected static Color Blend(Color first, Color second, float amount)
    {
        amount = Math.Clamp(amount, 0f, 1f);
        float keep = 1f - amount;
        return Color.FromArgb(
            (int)Math.Round(first.A * keep + second.A * amount),
            (int)Math.Round(first.R * keep + second.R * amount),
            (int)Math.Round(first.G * keep + second.G * amount),
            (int)Math.Round(first.B * keep + second.B * amount));
    }

    private static Color ContrastingText(Color background)
        => Luminance(background) < 0.48 ? Color.White : Color.Black;

    private static double Luminance(Color color)
    {
        static double Channel(byte value)
        {
            double s = value / 255d;
            return s <= 0.04045 ? s / 12.92 : Math.Pow((s + 0.055) / 1.055, 2.4);
        }

        return Channel(color.R) * 0.2126 + Channel(color.G) * 0.7152 + Channel(color.B) * 0.0722;
    }
}
