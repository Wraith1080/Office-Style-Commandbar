namespace CommandBars.Designer.Protocol.Endpoints;

/// <summary>Stable endpoint names shared by the client senders and server handlers.</summary>
public static class EndpointNames
{
    public const string GetBarDefinitions = nameof(GetBarDefinitions);
    public const string SetBarDefinitions = nameof(SetBarDefinitions);
}

/// <summary>Editor names used by [Editor("name", ...)] on the runtime properties and
/// by the client's TypeRoutingProvider.</summary>
public static class EditorNames
{
    public const string BarDefinitionsEditor = nameof(BarDefinitionsEditor);
    public const string SvgMarkupEditor = nameof(SvgMarkupEditor);
}
