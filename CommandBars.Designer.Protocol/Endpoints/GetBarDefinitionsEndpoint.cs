using System;
using System.Composition;
using Microsoft.DotNet.DesignTools.Protocol.DataPipe;
using Microsoft.DotNet.DesignTools.Protocol.Endpoints;

namespace CommandBars.Designer.Protocol.Endpoints;

/// <summary>
/// Client → server: given the manager component proxy, return its current
/// BarDefinitions as a JSON snapshot the client dialog can edit.
/// </summary>
[Shared]
[ExportEndpoint]
public class GetBarDefinitionsEndpoint
    : Endpoint<GetBarDefinitionsRequest, GetBarDefinitionsResponse>
{
    public override string Name => EndpointNames.GetBarDefinitions;

    protected override GetBarDefinitionsRequest CreateRequest(IDataPipeReader reader)
        => new(reader);

    protected override GetBarDefinitionsResponse CreateResponse(IDataPipeReader reader)
        => new(reader);
}

public class GetBarDefinitionsRequest : Request
{
    /// <summary>Proxy of the CommandBarManager whose definitions are read.</summary>
    public object? Manager { get; private set; }

    public GetBarDefinitionsRequest(object? manager)
    {
        Manager = manager ?? throw new ArgumentNullException(nameof(manager));
    }

    public GetBarDefinitionsRequest(IDataPipeReader reader) : base(reader) { }

    protected override void ReadProperties(IDataPipeReader reader)
        => Manager = reader.ReadObject(nameof(Manager));

    protected override void WriteProperties(IDataPipeWriter writer)
        => writer.WriteObject(nameof(Manager), Manager);
}

public class GetBarDefinitionsResponse : Response
{
    /// <summary>The current bar definitions, serialized as JSON (a BarDefData[]).</summary>
    // No [AllowNull] here: System.Diagnostics.CodeAnalysis.AllowNullAttribute is
    // not accessible on net472 (the client leg of this multi-targeted project).
    // Initializing to empty keeps the nullable contract satisfied instead.
    public string DefinitionsJson { get; private set; } = string.Empty;

    public GetBarDefinitionsResponse(string definitionsJson)
    {
        DefinitionsJson = definitionsJson ?? throw new ArgumentNullException(nameof(definitionsJson));
    }

    public GetBarDefinitionsResponse(IDataPipeReader reader) : base(reader) { }

    protected override void ReadProperties(IDataPipeReader reader)
        => DefinitionsJson = reader.ReadString(nameof(DefinitionsJson));

    protected override void WriteProperties(IDataPipeWriter writer)
        => writer.Write(nameof(DefinitionsJson), DefinitionsJson);
}
