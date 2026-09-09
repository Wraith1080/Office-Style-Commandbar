using System.Drawing;

namespace CommandBars.Rendering;

/// <summary>A suggested accent and its relationship to a Fluent base hue.</summary>
public sealed class FluentAccentChoice
{
    internal FluentAccentChoice(string key, string name, string harmony, Color color)
    {
        Key = key;
        Name = name;
        Harmony = harmony;
        Color = color;
    }

    public string Key { get; }
    public string Name { get; }
    public string Harmony { get; }
    /// <summary>Opaque RGB preference; the renderer may darken it for contrast.</summary>
    public Color Color { get; }
    public override string ToString() => $"{Name} ({Harmony})";
}

/// <summary>Hue-based accent suggestions for Fluent's colored surfaces.</summary>
public static class FluentAccentPalettes
{
    private static readonly string[] Keys = { "tonal", "analogous-left", "analogous-right", "complementary", "split-left", "split-right" };
    private static readonly string[] Harmonies = { "Tonal", "Analogous", "Analogous", "Complementary", "Split complementary", "Split complementary" };
    private static readonly int[] Offsets = { 0, -30, 30, 180, 150, 210 };

    /// <summary>
    /// Six suggestions based on the surface seed, independent of tint strength.
    /// Neutral has none. Achromatic custom bases receive versatile neutral pairings.
    /// </summary>
    public static IReadOnlyList<FluentAccentChoice> ForBase(FluentColorOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        if (options.BasePalette == FluentBasePalette.Neutral)
            return Array.Empty<FluentAccentChoice>();

        Color seed = options.SurfaceSeed;
        if (seed.GetSaturation() < .05f)
        {
            return Array.AsReadOnly(new[]
            {
                new FluentAccentChoice("slate", "Slate", "Neutral pairing", FromHsl(210, .18, .34)),
                new FluentAccentChoice("blue", "Blue", "Neutral pairing", FromHsl(215, .58, .34)),
                new FluentAccentChoice("teal", "Teal", "Neutral pairing", FromHsl(175, .58, .34)),
                new FluentAccentChoice("purple", "Purple", "Neutral pairing", FromHsl(265, .58, .34)),
                new FluentAccentChoice("copper", "Copper", "Neutral pairing", FromHsl(25, .58, .34)),
                new FluentAccentChoice("berry", "Berry", "Neutral pairing", FromHsl(335, .58, .34)),
            });
        }

        string[] names = options.BasePalette switch
        {
            FluentBasePalette.CoolBlue => new[] { "Ocean", "Teal", "Indigo", "Copper", "Rosewood", "Ochre" },
            FluentBasePalette.Mint => new[] { "Jade", "Forest", "Lagoon", "Raspberry", "Plum", "Terracotta" },
            FluentBasePalette.Rose => new[] { "Berry", "Orchid", "Brick", "Emerald", "Forest", "Teal" },
            FluentBasePalette.Lavender => new[] { "Violet", "Indigo", "Orchid", "Olive", "Gold", "Leaf" },
            _ => new[] { "Same hue", "Neighbor hue 1", "Neighbor hue 2", "Opposite hue", "Split hue 1", "Split hue 2" },
        };
        var choices = new FluentAccentChoice[Offsets.Length];
        double saturation = Math.Clamp(seed.GetSaturation(), .50, .64);
        for (int i = 0; i < choices.Length; i++)
            choices[i] = new(Keys[i], names[i], Harmonies[i], FromHsl(seed.GetHue() + Offsets[i], saturation, .34));
        return Array.AsReadOnly(choices);
    }

    // HSL supplies predictable hue relationships; the existing renderer owns
    // final contrast correction against the actual tinted surfaces.
    private static Color FromHsl(double hue, double saturation, double lightness)
    {
        hue = (hue % 360 + 360) % 360 / 60;
        double chroma = (1 - Math.Abs(2 * lightness - 1)) * saturation;
        double x = chroma * (1 - Math.Abs(hue % 2 - 1));
        double m = lightness - chroma / 2;
        var (r, g, b) = hue switch
        {
            < 1 => (chroma, x, 0d),
            < 2 => (x, chroma, 0d),
            < 3 => (0d, chroma, x),
            < 4 => (0d, x, chroma),
            < 5 => (x, 0d, chroma),
            _ => (chroma, 0d, x),
        };
        return Color.FromArgb((int)Math.Round((r + m) * 255),
            (int)Math.Round((g + m) * 255), (int)Math.Round((b + m) * 255));
    }
}
