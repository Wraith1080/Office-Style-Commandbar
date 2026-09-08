using System.ComponentModel;
using System.Drawing;

namespace CommandBars.Rendering;

/// <summary>A palette preference, independent of the theme's geometry.</summary>
public enum CommandBarColorScheme { Default, Blue, Silver, Olive, Teal, Purple }

/// <summary>Supported built-in palettes. Unsupported preferences render as Default.</summary>
public static class CommandBarColorSchemes
{
    public static IReadOnlyList<CommandBarColorScheme> ForTheme(CommandBarTheme theme)
        => Array.AsReadOnly(theme switch
        {
            CommandBarTheme.Office2000 or CommandBarTheme.OfficeXP => new[] { CommandBarColorScheme.Default, CommandBarColorScheme.Blue, CommandBarColorScheme.Silver, CommandBarColorScheme.Olive },
            CommandBarTheme.Office2003 => new[] { CommandBarColorScheme.Default, CommandBarColorScheme.Blue, CommandBarColorScheme.Silver, CommandBarColorScheme.Olive },
            CommandBarTheme.Office2007 or CommandBarTheme.Office2010 => new[] { CommandBarColorScheme.Default, CommandBarColorScheme.Blue, CommandBarColorScheme.Silver },
            CommandBarTheme.Fluent => new[] { CommandBarColorScheme.Default, CommandBarColorScheme.Blue, CommandBarColorScheme.Teal, CommandBarColorScheme.Purple },
            _ => new[] { CommandBarColorScheme.Default },
        });

    internal static CommandBarColorScheme Effective(CommandBarTheme theme, CommandBarColorScheme scheme)
        => ForTheme(theme).Contains(scheme) ? scheme : CommandBarColorScheme.Default;
}

/// <summary>Filters the designer picker to the selected theme's supported palettes.</summary>
public sealed class CommandBarColorSchemeConverter : EnumConverter
{
    public CommandBarColorSchemeConverter() : base(typeof(CommandBarColorScheme)) { }
    public override StandardValuesCollection GetStandardValues(ITypeDescriptorContext? context)
        => context?.Instance is CommandBarManager manager
            ? new StandardValuesCollection(manager.AvailableColorSchemes.ToArray())
            : base.GetStandardValues(context);
}

// Preserve the original palette's luminance steps and selection colors; tint
// only named chrome surfaces. Office 2000's button chrome follows its surfaces.
internal sealed class SchemeColorTable : CommandBarColorTable
{
    private readonly CommandBarColorTable source;
    private readonly Color tint;
    private readonly bool classic;
    private SchemeColorTable(CommandBarColorTable source, CommandBarColorScheme scheme, bool classic)
    {
        this.source = source;
        this.classic = classic;
        tint = scheme switch
        {
            CommandBarColorScheme.Blue => Color.FromArgb(115, 157, 215),
            CommandBarColorScheme.Olive => Color.FromArgb(156, 166, 108),
            _ => Color.FromArgb(157, 157, 174),
        };
    }
    internal static CommandBarColorTable Create(CommandBarColorTable source, CommandBarTheme theme, CommandBarColorScheme scheme)
    {
        scheme = CommandBarColorSchemes.Effective(theme, scheme);
        if (scheme == CommandBarColorScheme.Default ||
            (scheme == CommandBarColorScheme.Blue && theme is CommandBarTheme.Office2003 or CommandBarTheme.Office2007) ||
            (scheme == CommandBarColorScheme.Silver && theme == CommandBarTheme.Office2010)) return source;
        return new SchemeColorTable(source, scheme, theme == CommandBarTheme.Office2000);
    }
    private Color Tint(Color color)
    {
        double light = (color.R + color.G + color.B) / 765d;
        double anchor = (tint.R + tint.G + tint.B) / 765d;
        int Channel(byte value) => (int)Math.Round(light >= anchor
            ? value + (255 - value) * (light - anchor) / (1 - anchor)
            : value * light / anchor);
        return Color.FromArgb(color.A, Channel(tint.R), Channel(tint.G), Channel(tint.B));
    }
    public override Color BarGradientBegin => Tint(source.BarGradientBegin);
    public override Color BarGradientMiddle => Tint(source.BarGradientMiddle);
    public override Color BarGradientEnd => Tint(source.BarGradientEnd);
    public override Color MenuBarGradientBegin => Tint(source.MenuBarGradientBegin);
    public override Color MenuBarGradientEnd => Tint(source.MenuBarGradientEnd);
    public override Color BarBorder => Tint(source.BarBorder);
    public override Color BandGradientBegin => Tint(source.BandGradientBegin);
    public override Color BandGradientEnd => Tint(source.BandGradientEnd);
    public override Color RaisedBorder => Tint(source.RaisedBorder);
    public override Color ChevronGradientBegin => Tint(source.ChevronGradientBegin);
    public override Color ChevronGradientEnd => Tint(source.ChevronGradientEnd);
    public override Color DropPreview => source.DropPreview;
    public override Color ButtonHotBegin => classic ? Tint(source.ButtonHotBegin) : source.ButtonHotBegin;
    public override Color ButtonHotEnd => classic ? Tint(source.ButtonHotEnd) : source.ButtonHotEnd;
    public override Color ButtonHotBorder => classic ? Tint(source.ButtonHotBorder) : source.ButtonHotBorder;
    public override Color ButtonPressedBegin => classic ? Tint(source.ButtonPressedBegin) : source.ButtonPressedBegin;
    public override Color ButtonPressedEnd => classic ? Tint(source.ButtonPressedEnd) : source.ButtonPressedEnd;
    public override Color ButtonPressedBorder => classic ? Tint(source.ButtonPressedBorder) : source.ButtonPressedBorder;
    public override Color ButtonCheckedBegin => classic ? Tint(source.ButtonCheckedBegin) : source.ButtonCheckedBegin;
    public override Color ButtonCheckedEnd => classic ? Tint(source.ButtonCheckedEnd) : source.ButtonCheckedEnd;
    public override Color ButtonCheckedBorder => classic ? Tint(source.ButtonCheckedBorder) : source.ButtonCheckedBorder;
    public override Color MenuOpenBegin => Tint(source.MenuOpenBegin);
    public override Color MenuOpenEnd => Tint(source.MenuOpenEnd);
    public override Color MenuOpenBorder => Tint(source.MenuOpenBorder);
    public override Color SeparatorDark => Tint(source.SeparatorDark);
    public override Color SeparatorLight => Tint(source.SeparatorLight);
    public override Color GripperDark => Tint(source.GripperDark);
    public override Color GripperLight => Tint(source.GripperLight);
    public override Color Text => source.Text;
    public override Color DisabledText => source.DisabledText;
    public override Color MenuBackground => Tint(source.MenuBackground);
    public override Color MenuBorder => Tint(source.MenuBorder);
    public override Color ImageMarginBegin => Tint(source.ImageMarginBegin);
    public override Color ImageMarginEnd => Tint(source.ImageMarginEnd);
    public override Color MenuItemSelectedBegin => source.MenuItemSelectedBegin;
    public override Color MenuItemSelectedEnd => source.MenuItemSelectedEnd;
    public override Color MenuItemSelectedBorder => source.MenuItemSelectedBorder;
    public override Color MenuItemSelectedText => source.MenuItemSelectedText;
    public override Color MenuText => source.MenuText;
    public override Color DisabledMenuText => source.DisabledMenuText;
}
