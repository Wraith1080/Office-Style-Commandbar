using System;
using System.Composition;
using Microsoft.DotNet.DesignTools.Protocol;
using Microsoft.DotNet.DesignTools.Protocol.DataPipe;
using Microsoft.DotNet.DesignTools.Protocol.Endpoints;

namespace CommandBars.Designer.Protocol.Endpoints;

[Shared]
[ExportEndpoint]
public class GetDockHostDesignContextEndpoint
    : Endpoint<GetDockHostDesignContextRequest, GetDockHostDesignContextResponse>
{
    public override string Name => EndpointNames.GetDockHostDesignContext;

    protected override GetDockHostDesignContextRequest CreateRequest(IDataPipeReader reader)
        => new(reader);

    protected override GetDockHostDesignContextResponse CreateResponse(IDataPipeReader reader)
        => new(reader);
}

public class GetDockHostDesignContextRequest : Request
{
    public object? Host { get; private set; }

    public GetDockHostDesignContextRequest(object? host)
        => Host = host ?? throw new ArgumentNullException(nameof(host));

    public GetDockHostDesignContextRequest(IDataPipeReader reader) : base(reader) { }

    protected override void ReadProperties(IDataPipeReader reader)
        => Host = reader.ReadObject(nameof(Host));

    protected override void WriteProperties(IDataPipeWriter writer)
        => writer.WriteObject(nameof(Host), Host);
}

public class GetDockHostDesignContextResponse : Response
{
    public string ContextJson { get; private set; } = string.Empty;

    public GetDockHostDesignContextResponse(string contextJson)
        => ContextJson = contextJson ?? throw new ArgumentNullException(nameof(contextJson));

    public GetDockHostDesignContextResponse(IDataPipeReader reader) : base(reader) { }

    protected override void ReadProperties(IDataPipeReader reader)
        => ContextJson = reader.ReadString(nameof(ContextJson)) ?? string.Empty;

    protected override void WriteProperties(IDataPipeWriter writer)
        => writer.Write(nameof(ContextJson), ContextJson);
}

[Shared]
[ExportEndpoint]
public class SetDockHostDefinitionsEndpoint
    : Endpoint<SetDockHostDefinitionsRequest, SetDockHostDefinitionsResponse>
{
    public override string Name => EndpointNames.SetDockHostDefinitions;

    protected override SetDockHostDefinitionsRequest CreateRequest(IDataPipeReader reader)
        => new(reader);

    protected override SetDockHostDefinitionsResponse CreateResponse(IDataPipeReader reader)
        => new(reader);
}

public class SetDockHostDefinitionsRequest : Request
{
    public SessionId SessionId { get; private set; }
    public object? Host { get; private set; }
    public string DefinitionsJson { get; private set; } = string.Empty;

    public SetDockHostDefinitionsRequest(
        SessionId sessionId,
        object? host,
        string definitionsJson)
    {
        SessionId = sessionId.IsNull
            ? throw new ArgumentNullException(nameof(sessionId))
            : sessionId;
        Host = host ?? throw new ArgumentNullException(nameof(host));
        DefinitionsJson = definitionsJson ?? throw new ArgumentNullException(nameof(definitionsJson));
    }

    public SetDockHostDefinitionsRequest(IDataPipeReader reader) : base(reader) { }

    protected override void ReadProperties(IDataPipeReader reader)
    {
        SessionId = reader.ReadSessionId(nameof(SessionId));
        Host = reader.ReadObject(nameof(Host));
        DefinitionsJson = reader.ReadString(nameof(DefinitionsJson)) ?? string.Empty;
    }

    protected override void WriteProperties(IDataPipeWriter writer)
    {
        writer.Write(nameof(SessionId), SessionId);
        writer.WriteObject(nameof(Host), Host);
        writer.Write(nameof(DefinitionsJson), DefinitionsJson);
    }
}

public class SetDockHostDefinitionsResponse : Response.Empty
{
    public static new SetDockHostDefinitionsResponse Empty { get; } = new();

    private SetDockHostDefinitionsResponse() { }

    public SetDockHostDefinitionsResponse(IDataPipeReader reader) : base(reader) { }
}
