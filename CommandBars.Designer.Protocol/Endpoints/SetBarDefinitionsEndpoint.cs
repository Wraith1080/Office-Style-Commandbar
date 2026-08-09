using System;
using Microsoft.DotNet.DesignTools.Protocol;
using System.Composition;
using Microsoft.DotNet.DesignTools.Protocol.DataPipe;
using Microsoft.DotNet.DesignTools.Protocol.Endpoints;

namespace CommandBars.Designer.Protocol.Endpoints;

/// <summary>
/// Client → server: replace the manager's BarDefinitions with the edited JSON
/// snapshot. The server rebuilds the real definition objects inside a designer
/// transaction and notifies the change service so the .Designer.cs regenerates.
/// </summary>
[Shared]
[ExportEndpoint]
public class SetBarDefinitionsEndpoint
    : Endpoint<SetBarDefinitionsRequest, SetBarDefinitionsResponse>
{
    public override string Name => EndpointNames.SetBarDefinitions;

    protected override SetBarDefinitionsRequest CreateRequest(IDataPipeReader reader)
        => new(reader);

    protected override SetBarDefinitionsResponse CreateResponse(IDataPipeReader reader)
        => new(reader);
}

public class SetBarDefinitionsRequest : Request
{
    /// <summary>The design session, used server-side to reach the designer host + change service.</summary>
    public SessionId SessionId { get; private set; }

    /// <summary>Proxy of the CommandBarManager to update.</summary>
    public object? Manager { get; private set; }

    /// <summary>The edited bar definitions, serialized as JSON (a BarDefData[]).</summary>
    public string DefinitionsJson { get; private set; } = string.Empty;

    public SetBarDefinitionsRequest(SessionId sessionId, object? manager, string definitionsJson)
    {
        SessionId = sessionId.IsNull ? throw new ArgumentNullException(nameof(sessionId)) : sessionId;
        Manager = manager ?? throw new ArgumentNullException(nameof(manager));
        DefinitionsJson = definitionsJson ?? throw new ArgumentNullException(nameof(definitionsJson));
    }

    public SetBarDefinitionsRequest(IDataPipeReader reader) : base(reader) { }

    protected override void ReadProperties(IDataPipeReader reader)
    {
        SessionId = reader.ReadSessionId(nameof(SessionId));
        Manager = reader.ReadObject(nameof(Manager));
        DefinitionsJson = reader.ReadString(nameof(DefinitionsJson));
    }

    protected override void WriteProperties(IDataPipeWriter writer)
    {
        writer.Write(nameof(SessionId), SessionId);
        writer.WriteObject(nameof(Manager), Manager);
        writer.Write(nameof(DefinitionsJson), DefinitionsJson);
    }
}

public class SetBarDefinitionsResponse : Response.Empty
{
    public static new SetBarDefinitionsResponse Empty { get; } = new();

    private SetBarDefinitionsResponse() { }

    public SetBarDefinitionsResponse(IDataPipeReader reader) : base(reader) { }
}
