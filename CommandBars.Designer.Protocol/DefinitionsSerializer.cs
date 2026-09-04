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

    public static string SerializeDockHostContext(DockHostDesignContextData context)
        => JsonSerializer.Serialize(context, s_options);

    public static DockHostDesignContextData DeserializeDockHostContext(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
            return new DockHostDesignContextData();
        return JsonSerializer.Deserialize<DockHostDesignContextData>(json!, s_options)
               ?? new DockHostDesignContextData();
    }
}

/// <summary>Host edge plus the connected manager snapshot for client-side actions.</summary>
public sealed class DockHostDesignContextData
{
    public bool HasManager { get; set; }
    public DockEdgeData Edge { get; set; } = DockEdgeData.Top;
    public DesignSnapshot Snapshot { get; set; } = new();
}
