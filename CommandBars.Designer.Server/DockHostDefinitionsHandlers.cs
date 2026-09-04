using CommandBars.Controls;
using CommandBars.Designer.Protocol;
using CommandBars.Designer.Protocol.Endpoints;
using Microsoft.DotNet.DesignTools.Protocol.Endpoints;

namespace CommandBars.Designer.Server;

[ExportRequestHandler(EndpointNames.GetDockHostDesignContext)]
internal sealed class GetDockHostDesignContextHandler
    : RequestHandler<GetDockHostDesignContextRequest, GetDockHostDesignContextResponse>
{
    public override GetDockHostDesignContextResponse HandleRequest(
        GetDockHostDesignContextRequest request)
    {
        var host = (DockHost)request.Host!;
        var context = new DockHostDesignContextData
        {
            HasManager = host.Manager is not null,
            Edge = ToData(host.Edge),
            Snapshot = host.Manager is null
                ? new DesignSnapshot()
                : BarDefinitionMapper.ToSnapshot(host.Manager),
        };
        return new GetDockHostDesignContextResponse(
            DefinitionsSerializer.SerializeDockHostContext(context));
    }

    private static DockEdgeData ToData(DockEdge edge) => edge switch
    {
        DockEdge.Left => DockEdgeData.Left,
        DockEdge.Right => DockEdgeData.Right,
        DockEdge.Bottom => DockEdgeData.Bottom,
        _ => DockEdgeData.Top,
    };
}

[ExportRequestHandler(EndpointNames.SetDockHostDefinitions)]
internal sealed class SetDockHostDefinitionsHandler
    : RequestHandler<SetDockHostDefinitionsRequest, SetDockHostDefinitionsResponse>
{
    public override SetDockHostDefinitionsResponse HandleRequest(
        SetDockHostDefinitionsRequest request)
    {
        var dockHost = (DockHost)request.Host!;
        var manager = dockHost.Manager ?? throw new InvalidOperationException(
            "The DockHost is not connected to a CommandBarManager.");
        var designerHost = GetDesignerHost(request.SessionId);
        var snapshot = DefinitionsSerializer.Deserialize(request.DefinitionsJson);
        BarDefinitionsCommitter.Apply(
            manager,
            designerHost,
            snapshot,
            "Edit CommandBars from DockHost");
        return SetDockHostDefinitionsResponse.Empty;
    }
}
