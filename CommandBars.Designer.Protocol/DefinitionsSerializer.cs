using System.Collections.Generic;
using System.Text.Json;

namespace CommandBars.Designer.Protocol;

/// <summary>
/// JSON (de)serialization of the bar-definition snapshot exchanged between the
/// client dialog and the design server. Both sides use the same protocol enums
/// and POCOs, so a plain round-trip is lossless.
/// </summary>
public static class DefinitionsSerializer
{
    private static readonly JsonSerializerOptions s_options = new()
    {
        WriteIndented = false,
        // Default enum handling (numeric) is fine — the same enum types are used
        // on both ends, so numbers round-trip without ambiguity.
    };

    public static string Serialize(IReadOnlyList<BarDefData> bars)
        => JsonSerializer.Serialize(bars, s_options);

    public static List<BarDefData> Deserialize(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
            return new List<BarDefData>();
        return JsonSerializer.Deserialize<List<BarDefData>>(json!, s_options)
               ?? new List<BarDefData>();
    }
}
