using System.Collections.Generic;
using System.Text.Json;

namespace CommandBars.Designer.Protocol;

/// <summary>One icon the client gallery is sending to the server to embed into
/// the SvgImageList: a key and the raw SVG markup.</summary>
public sealed class StockIconData
{
    public string Key { get; set; } = string.Empty;
    public string Svg { get; set; } = string.Empty;
}

/// <summary>JSON round-trip for the selected stock icons.</summary>
public static class StockIconsSerializer
{
    private static readonly JsonSerializerOptions s_options = new() { WriteIndented = false };

    public static string Serialize(IReadOnlyList<StockIconData> icons)
        => JsonSerializer.Serialize(icons, s_options);

    public static List<StockIconData> Deserialize(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
            return new List<StockIconData>();
        return JsonSerializer.Deserialize<List<StockIconData>>(json!, s_options)
               ?? new List<StockIconData>();
    }
}
