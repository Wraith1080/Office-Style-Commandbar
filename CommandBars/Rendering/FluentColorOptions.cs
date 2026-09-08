using System.Drawing;

namespace CommandBars.Rendering;

/// <summary>Light surface palettes, independent of Fluent's accent scheme.</summary>
public enum FluentBasePalette { Neutral, CoolBlue, Mint, Rose, Lavender, Custom }

/// <summary>Immutable Fluent colors. Empty colors select the preset base and scheme accent.</summary>
public sealed class FluentColorOptions
{
    public FluentColorOptions(FluentBasePalette basePalette = FluentBasePalette.Neutral,
        Color baseColor = default, Color accentColor = default, int tintStrength = 50)
    {
        if (!Enum.IsDefined(typeof(FluentBasePalette), basePalette)) throw new ArgumentOutOfRangeException(nameof(basePalette));
        if (tintStrength < 0 || tintStrength > 100) throw new ArgumentOutOfRangeException(nameof(tintStrength));
        ValidateColor(baseColor, nameof(baseColor));
        ValidateColor(accentColor, nameof(accentColor));
        BasePalette = basePalette;
        BaseColor = baseColor;
        AccentColor = accentColor;
        TintStrength = tintStrength;
    }
    public FluentBasePalette BasePalette { get; }
    public Color BaseColor { get; }
    public Color AccentColor { get; }
    public int TintStrength { get; }
    internal Color SurfaceSeed => BasePalette switch
    {
        FluentBasePalette.CoolBlue => Color.FromArgb(70, 150, 190),
        FluentBasePalette.Mint => Color.FromArgb(65, 166, 135),
        FluentBasePalette.Rose => Color.FromArgb(199, 106, 141),
        FluentBasePalette.Lavender => Color.FromArgb(143, 116, 195),
        FluentBasePalette.Custom => BaseColor.IsEmpty ? Color.FromArgb(70, 150, 190) : BaseColor,
        _ => Color.Empty,
    };
    internal static void ValidateColor(Color color, string name)
    {
        if (!color.IsEmpty && color.A != 255) throw new ArgumentException("Choose an opaque RGB color or Color.Empty.", name);
    }
    internal static Color Blend(Color first, Color second, double amount) => Color.FromArgb(
        (int)Math.Round(first.R + (second.R - first.R) * amount),
        (int)Math.Round(first.G + (second.G - first.G) * amount),
        (int)Math.Round(first.B + (second.B - first.B) * amount));
}
