using CommandBars.Imaging;

namespace CommandBars.Demo;

/// <summary>
/// Vector (SVG) icons for the demo. Because they're vector, they stay crisp at
/// any icon size — try the View → Icon Size menu. SVG uses single-quoted
/// attributes so the C# strings stay clean.
/// </summary>
internal static class DemoSvgIcons
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

    private static readonly Dictionary<string, string> Markup = new()
    {
        ["new"] = @"<svg xmlns='http://www.w3.org/2000/svg' viewBox='0 0 32 32'>
            <path d='M8 4 H20 L24 8 V28 H8 Z' fill='#ffffff' stroke='#5a6e8c' stroke-width='1.4'/>
            <path d='M20 4 V8 H24' fill='none' stroke='#5a6e8c' stroke-width='1.4'/>
            <path d='M11 13 H21 M11 17 H21 M11 21 H18' stroke='#96a5be' stroke-width='1.2'/>
          </svg>",

        ["open"] = @"<svg xmlns='http://www.w3.org/2000/svg' viewBox='0 0 32 32'>
            <path d='M5 11 H14 L17 14 H27 V26 H5 Z' fill='#e4ba4a' stroke='#96782a' stroke-width='1'/>
            <path d='M7 16 H29 L26 27 H4 Z' fill='#ffd66e' stroke='#96782a' stroke-width='1'/>
          </svg>",

        ["save"] = @"<svg xmlns='http://www.w3.org/2000/svg' viewBox='0 0 32 32'>
            <rect x='6' y='6' width='20' height='20' fill='#427abe' stroke='#28507f' stroke-width='1'/>
            <rect x='11' y='6' width='10' height='7' fill='#ffffff'/>
            <rect x='17' y='7' width='3' height='5' fill='#28507f'/>
            <rect x='9' y='16' width='14' height='10' fill='#ffffff'/>
          </svg>",

        ["cut"] = @"<svg xmlns='http://www.w3.org/2000/svg' viewBox='0 0 32 32'>
            <g fill='none' stroke='#505a69' stroke-width='2'>
              <line x1='11' y1='11' x2='24' y2='22'/>
              <line x1='24' y1='10' x2='11' y2='21'/>
              <circle cx='9' cy='9' r='3'/>
              <circle cx='9' cy='23' r='3'/>
            </g>
          </svg>",

        ["copy"] = @"<svg xmlns='http://www.w3.org/2000/svg' viewBox='0 0 32 32'>
            <rect x='7' y='6' width='13' height='16' fill='#ffffff' stroke='#5a6e8c' stroke-width='1.3'/>
            <rect x='12' y='11' width='13' height='16' fill='#ffffff' stroke='#5a6e8c' stroke-width='1.3'/>
          </svg>",

        ["paste"] = @"<svg xmlns='http://www.w3.org/2000/svg' viewBox='0 0 32 32'>
            <rect x='6' y='7' width='20' height='21' rx='1' fill='#aa8c5f' stroke='#5a4b32' stroke-width='1'/>
            <rect x='9' y='12' width='14' height='14' fill='#ffffff'/>
            <rect x='12' y='4' width='8' height='6' rx='1' fill='#787878'/>
          </svg>",

        ["bold"] = @"<svg xmlns='http://www.w3.org/2000/svg' viewBox='0 0 32 32'>
            <text x='16' y='24' font-family='Segoe UI, Arial' font-size='22' font-weight='bold'
                  text-anchor='middle' fill='#2d3746'>B</text>
          </svg>",

        ["italic"] = @"<svg xmlns='http://www.w3.org/2000/svg' viewBox='0 0 32 32'>
            <text x='16' y='24' font-family='Segoe UI, Arial' font-size='22' font-weight='bold'
                  font-style='italic' text-anchor='middle' fill='#2d3746'>I</text>
          </svg>",

        ["underline"] = @"<svg xmlns='http://www.w3.org/2000/svg' viewBox='0 0 32 32'>
            <text x='16' y='22' font-family='Segoe UI, Arial' font-size='20' font-weight='bold'
                  text-anchor='middle' fill='#2d3746'>U</text>
            <line x1='9' y1='27' x2='23' y2='27' stroke='#2d3746' stroke-width='2'/>
          </svg>",

        ["back"] = @"<svg xmlns='http://www.w3.org/2000/svg' viewBox='0 0 32 32'>
            <path d='M24 16 H9 M15 9 L8 16 L15 23' fill='none' stroke='#3a5a8c'
                  stroke-width='2.6' stroke-linecap='round' stroke-linejoin='round'/>
          </svg>",

        ["forward"] = @"<svg xmlns='http://www.w3.org/2000/svg' viewBox='0 0 32 32'>
            <path d='M8 16 H23 M17 9 L24 16 L17 23' fill='none' stroke='#3a5a8c'
                  stroke-width='2.6' stroke-linecap='round' stroke-linejoin='round'/>
          </svg>",

        ["refresh"] = @"<svg xmlns='http://www.w3.org/2000/svg' viewBox='0 0 32 32'>
            <path d='M23 12 A9 9 0 1 0 25 18' fill='none' stroke='#2f7d4a' stroke-width='2.6' stroke-linecap='round'/>
            <path d='M18 7 L24 11 L20 15 Z' fill='#2f7d4a'/>
          </svg>",

        ["home"] = @"<svg xmlns='http://www.w3.org/2000/svg' viewBox='0 0 32 32'>
            <path d='M16 5 L27 15 H24 V26 H8 V15 H5 Z' fill='#e6b955' stroke='#8a6a20' stroke-width='1.2' stroke-linejoin='round'/>
            <rect x='13' y='18' width='6' height='8' fill='#8a5a20'/>
          </svg>",

        ["align-left"] = AlignSvg(new[] { 20, 26, 18, 24 }, "left"),
        ["align-center"] = AlignSvg(new[] { 20, 12, 18, 14 }, "center"),
        ["align-right"] = AlignSvg(new[] { 20, 26, 18, 24 }, "right"),
        ["align-justify"] = AlignSvg(new[] { 26, 26, 26, 26 }, "center"),
    };

    // Builds a paragraph-alignment icon from four line lengths.
    private static string AlignSvg(int[] widths, string align)
    {
        var lines = new System.Text.StringBuilder();
        for (int i = 0; i < 4; i++)
        {
            int y = 8 + (i * 6);
            int w = widths[i];
            int x1 = align switch
            {
                "right" => 26 - w,
                "center" => 16 - (w / 2),
                _ => 6,
            };
            lines.Append($"<line x1='{x1}' y1='{y}' x2='{x1 + w}' y2='{y}' stroke='#3a4656' stroke-width='2.4' stroke-linecap='round'/>");
        }
        return $"<svg xmlns='http://www.w3.org/2000/svg' viewBox='0 0 32 32'>{lines}</svg>";
    }
}
