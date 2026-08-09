using CommandBars.Imaging;

namespace CommandBars.Demo;

/// <summary>
/// Vector (SVG) icons for the AutoShapes demo — category glyphs and individual
/// shapes, drawn as simple monochrome outlines like Office's shape gallery.
/// Because they're vector they stay crisp at any icon size and in a torn-off
/// palette. SVG uses single-quoted attributes so the C# strings stay clean.
/// </summary>
internal static class DemoShapeIcons
{
    private static readonly Dictionary<string, SvgImageSource> Cache = new();

    public static IImageSource Get(string key)
    {
        if (!Cache.TryGetValue(key, out var source))
        {
            source = SvgImageSource.FromString(Markup[key], key);
            Cache[key] = source;
        }
        return source;
    }

    // Wraps inner path markup in a 32×32 SVG with a shared outline style.
    private static string Svg(string inner, string fill = "none", string stroke = "#41506a")
        => $"<svg xmlns='http://www.w3.org/2000/svg' viewBox='0 0 32 32'>" +
           $"<g fill='{fill}' stroke='{stroke}' stroke-width='1.5' stroke-linejoin='round' stroke-linecap='round'>{inner}</g></svg>";

    private const string ArrowFill = "#dbe4f2";
    private const string StarFill = "#fde7a8";

    private static readonly Dictionary<string, string> Markup = new()
    {
        // --- Category glyphs (some reuse a representative shape) ---
        ["cat.autoshapes"] = Svg("<rect x='5' y='6' width='11' height='8'/><ellipse cx='22' cy='22' rx='6' ry='5'/>"),

        // --- Lines ---
        ["line"] = Svg("<line x1='7' y1='25' x2='25' y2='7'/>"),
        ["arrow"] = Svg("<line x1='7' y1='25' x2='24' y2='8'/><path d='M24 8 l-6 1 M24 8 l-1 6'/>"),
        ["dblarrow"] = Svg("<line x1='8' y1='24' x2='24' y2='8'/><path d='M24 8 l-6 1 M24 8 l-1 6'/><path d='M8 24 l6 -1 M8 24 l1 -6'/>"),
        ["curve"] = Svg("<path d='M7 24 C 10 9, 20 23, 25 8'/>"),
        ["freeform"] = Svg("<path d='M6 21 L12 11 L17 23 L26 12'/>"),

        // --- Connectors ---
        ["conn-straight"] = Svg("<line x1='9' y1='9' x2='23' y2='23'/><rect x='7' y='7' width='4' height='4'/><rect x='21' y='21' width='4' height='4'/>"),
        ["conn-elbow"] = Svg("<path d='M9 8 H16 V24 H24'/><rect x='7' y='6' width='4' height='4'/><rect x='22' y='22' width='4' height='4'/>"),
        ["conn-curved"] = Svg("<path d='M9 9 C 22 9, 22 23, 24 23'/><rect x='7' y='7' width='4' height='4'/><rect x='22' y='21' width='4' height='4'/>"),

        // --- Basic shapes ---
        ["rect"] = Svg("<rect x='6' y='10' width='20' height='12'/>"),
        ["roundrect"] = Svg("<rect x='6' y='10' width='20' height='12' rx='4'/>"),
        ["ellipse"] = Svg("<ellipse cx='16' cy='16' rx='11' ry='8'/>"),
        ["triangle"] = Svg("<path d='M16 7 L26 25 H6 Z'/>"),
        ["righttriangle"] = Svg("<path d='M7 7 V25 H25 Z'/>"),
        ["diamond"] = Svg("<path d='M16 6 L26 16 L16 26 L6 16 Z'/>"),
        ["pentagon"] = Svg("<path d='M16 6 L26 14 L22 26 H10 L6 14 Z'/>"),
        ["hexagon"] = Svg("<path d='M11 7 H21 L26 16 L21 25 H11 L6 16 Z'/>"),
        ["cylinder"] = Svg("<path d='M8 11 V21 A8 3 0 0 0 24 21 V11'/><ellipse cx='16' cy='11' rx='8' ry='3'/>"),
        ["cube"] = Svg("<path d='M8 12 H22 V24 H8 Z'/><path d='M8 12 L12 8 H26 L22 12 M22 24 L26 20 V8'/>"),

        // --- Block arrows (light fill so they read as blocks) ---
        ["arrow-right"] = Svg("<path d='M6 13 H17 V9 L26 16 L17 23 V19 H6 Z'/>", ArrowFill, "#5a6e8c"),
        ["arrow-left"] = Svg("<path d='M26 13 H15 V9 L6 16 L15 23 V19 H26 Z'/>", ArrowFill, "#5a6e8c"),
        ["arrow-up"] = Svg("<path d='M13 26 V15 H9 L16 6 L23 15 H19 V26 Z'/>", ArrowFill, "#5a6e8c"),
        ["arrow-down"] = Svg("<path d='M13 6 V17 H9 L16 26 L23 17 H19 V6 Z'/>", ArrowFill, "#5a6e8c"),
        ["arrow-leftright"] = Svg("<path d='M6 16 L11 11 V14 H21 V11 L26 16 L21 21 V18 H11 V21 Z'/>", ArrowFill, "#5a6e8c"),
        ["chevron"] = Svg("<path d='M6 9 H13 L19 16 L13 23 H6 L12 16 Z'/>", ArrowFill, "#5a6e8c"),

        // --- Flowchart ---
        ["fc-process"] = Svg("<rect x='6' y='10' width='20' height='12'/>"),
        ["fc-decision"] = Svg("<path d='M16 7 L26 16 L16 25 L6 16 Z'/>"),
        ["fc-terminator"] = Svg("<rect x='6' y='11' width='20' height='10' rx='5'/>"),
        ["fc-data"] = Svg("<path d='M11 9 H27 L21 23 H5 Z'/>"),
        ["fc-document"] = Svg("<path d='M6 8 H26 V21 Q21 17 16 21 Q11 25 6 21 Z'/>"),
        ["fc-connector"] = Svg("<circle cx='16' cy='16' r='9'/>"),

        // --- Stars and banners (light fill) ---
        ["star4"] = Svg("<path d='M16 5 L18.5 13.5 L27 16 L18.5 18.5 L16 27 L13.5 18.5 L5 16 L13.5 13.5 Z'/>", StarFill, "#b98a1e"),
        ["star5"] = Svg("<path d='M16 5 L18.9 13.1 L27.4 13.1 L20.5 18.2 L23.1 26.3 L16 21.2 L8.9 26.3 L11.5 18.2 L4.6 13.1 L13.1 13.1 Z'/>", StarFill, "#b98a1e"),
        ["star6"] = Svg("<path d='M16 5 L26.4 23 H5.6 Z'/><path d='M16 27 L5.6 9 H26.4 Z'/>", StarFill, "#b98a1e"),
        ["explosion"] = Svg("<path d='M16 4 L18 11 L24 8 L21 14 L28 16 L21 18 L24 24 L18 21 L16 28 L14 21 L8 24 L11 18 L4 16 L11 14 L8 8 L14 11 Z'/>", StarFill, "#b98a1e"),
        ["ribbon"] = Svg("<path d='M8 10 H24 V22 H20 L24 26 H8 L12 22 H8 Z'/>", ArrowFill, "#5a6e8c"),

        // --- Callouts ---
        ["callout-rect"] = Svg("<path d='M6 8 H26 V19 H15 L10 24 V19 H6 Z'/>"),
        ["callout-round"] = Svg("<path d='M9 8 H23 A3 3 0 0 1 26 11 V16 A3 3 0 0 1 23 19 H15 L10 24 V19 H9 A3 3 0 0 1 6 16 V11 A3 3 0 0 1 9 8 Z'/>"),
        ["callout-oval"] = Svg("<ellipse cx='16' cy='14' rx='10' ry='7'/><path d='M12 20 L10 25 L16 20'/>"),
        ["callout-cloud"] = Svg("<path d='M10 22 Q5 22 6 17 Q4 12 10 12 Q11 7 17 9 Q23 7 23 13 Q28 14 25 19 Q26 23 20 22 Z'/>"),
    };
}
