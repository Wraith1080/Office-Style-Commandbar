using System.Text.Json;

namespace CommandBars.Designer.Protocol;

/// <summary>
/// JSON (de)serialization of the <see cref="DesignSnapshot"/> (bars + command
/// catalog) exchanged between the client dialog and the design server. Both sides
/// use the same protocol enums and POCOs, so a plain round-trip is lossless.
/// </summary>
public static class DefinitionsSerializer
{
    private static readonly JsonSerializerOptions s_options = new()
    {
        WriteIndented = false,
        // Default enum handling (numeric) is fine — the same enum types are used
        // on both ends, so numbers round-trip without ambiguity.
    };

    public static string Serialize(DesignSnapshot snapshot)
        => JsonSerializer.Serialize(snapshot, s_options);

    public static DesignSnapshot Deserialize(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
            return new DesignSnapshot();
        return JsonSerializer.Deserialize<DesignSnapshot>(json!, s_options)
               ?? new DesignSnapshot();
    }
}
