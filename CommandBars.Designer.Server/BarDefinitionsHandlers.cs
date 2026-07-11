using System.ComponentModel;
using System.ComponentModel.Design;
using Microsoft.DotNet.DesignTools.Protocol.Endpoints;
using CommandBars.Designer.Protocol;
using CommandBars.Designer.Protocol.Endpoints;

namespace CommandBars.Designer.Server;

/// <summary>
/// Server handler for <see cref="EndpointNames.GetBarDefinitions"/>: reads the
/// real manager (the proxy deserializes to the live component on the server) and
/// returns a JSON snapshot of its bar definitions for the client dialog.
/// </summary>
[ExportRequestHandler(EndpointNames.GetBarDefinitions)]
internal class GetBarDefinitionsHandler
    : RequestHandler<GetBarDefinitionsRequest, GetBarDefinitionsResponse>
{
    public override GetBarDefinitionsResponse HandleRequest(GetBarDefinitionsRequest request)
    {
        var manager = (CommandBarManager)request.Manager!;
        var data = BarDefinitionMapper.ToData(manager.BarDefinitions);
        return new GetBarDefinitionsResponse(DefinitionsSerializer.Serialize(data));
    }
}

/// <summary>
/// Server handler for <see cref="EndpointNames.SetBarDefinitions"/>: rebuilds the
/// manager's <c>BarDefinitions</c> from the edited JSON inside a designer
/// transaction, notifying the change service so the .Designer.cs regenerates and
/// the design preview refreshes.
/// </summary>
[ExportRequestHandler(EndpointNames.SetBarDefinitions)]
internal class SetBarDefinitionsHandler
    : RequestHandler<SetBarDefinitionsRequest, SetBarDefinitionsResponse>
{
    public override SetBarDefinitionsResponse HandleRequest(SetBarDefinitionsRequest request)
    {
        var manager = (CommandBarManager)request.Manager!;
        var host = GetDesignerHost(request.SessionId);
        var changeService = host?.GetService(typeof(IComponentChangeService)) as IComponentChangeService;
        var property = TypeDescriptor.GetProperties(manager)[nameof(CommandBarManager.BarDefinitions)];

        var rebuilt = BarDefinitionMapper.ToRuntime(
            DefinitionsSerializer.Deserialize(request.DefinitionsJson));

        DesignerTransaction? tx = host?.CreateTransaction("Edit CommandBars toolbars and menus");
        try
        {
            changeService?.OnComponentChanging(manager, property);

            manager.BarDefinitions.Clear();
            foreach (var bar in rebuilt)
                manager.BarDefinitions.Add(bar);

            changeService?.OnComponentChanged(manager, property, null, null);
            tx?.Commit();
            tx = null;

            // Repaint the hosted bands right away.
            try { manager.RefreshDesignPreview(); } catch { /* preview only */ }
        }
        finally
        {
            tx?.Cancel();
        }

        return SetBarDefinitionsResponse.Empty;
    }
}
