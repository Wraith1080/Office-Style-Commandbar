using System.ComponentModel;
using System.ComponentModel.Design;
using Microsoft.DotNet.DesignTools.Protocol.Endpoints;
using CommandBars.Designer.Protocol;
using CommandBars.Designer.Protocol.Endpoints;

namespace CommandBars.Designer.Server;

/// <summary>
/// Server handler for <see cref="EndpointNames.GetBarDefinitions"/>: reads the
/// real manager (the proxy deserializes to the live component on the server) and
/// returns a JSON snapshot of its bars + command catalog for the client dialog.
/// </summary>
[ExportRequestHandler(EndpointNames.GetBarDefinitions)]
internal class GetBarDefinitionsHandler
    : RequestHandler<GetBarDefinitionsRequest, GetBarDefinitionsResponse>
{
    public override GetBarDefinitionsResponse HandleRequest(GetBarDefinitionsRequest request)
    {
        var manager = (CommandBarManager)request.Manager!;
        var snapshot = BarDefinitionMapper.ToSnapshot(manager);
        return new GetBarDefinitionsResponse(DefinitionsSerializer.Serialize(snapshot));
    }
}

/// <summary>
/// Server handler for <see cref="EndpointNames.SetBarDefinitions"/>: rebuilds the
/// manager's <c>BarDefinitions</c> and <c>CommandDefinitions</c> from the edited
/// JSON snapshot inside a single designer transaction, notifying the change
/// service for both so the .Designer.cs regenerates and the preview refreshes.
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

        var barsProperty = TypeDescriptor.GetProperties(manager)[nameof(CommandBarManager.BarDefinitions)];
        var commandsProperty = TypeDescriptor.GetProperties(manager)[nameof(CommandBarManager.CommandDefinitions)];

        var snapshot = DefinitionsSerializer.Deserialize(request.DefinitionsJson);
        var validation = CatalogDesignService.ValidateCatalogFirst(snapshot);
        if (!validation.IsValid)
        {
            string errors = string.Join(
                Environment.NewLine,
                validation.Diagnostics
                    .Where(diagnostic =>
                        diagnostic.Severity == CatalogDiagnosticSeverity.Error)
                    .Select(diagnostic => diagnostic.ToString()));
            throw new InvalidOperationException(
                "The CommandBars design snapshot is invalid:" +
                Environment.NewLine + errors);
        }
        var rebuiltBars = BarDefinitionMapper.ToRuntime(snapshot.Bars);
        var rebuiltCommands = BarDefinitionMapper.ToRuntimeCommands(snapshot.Commands);

        DesignerTransaction? tx = host?.CreateTransaction("Edit CommandBars toolbars and menus");
        try
        {
            changeService?.OnComponentChanging(manager, commandsProperty);
            manager.CommandDefinitions.Clear();
            foreach (var command in rebuiltCommands)
                manager.CommandDefinitions.Add(command);
            changeService?.OnComponentChanged(manager, commandsProperty, null, null);

            changeService?.OnComponentChanging(manager, barsProperty);
            manager.BarDefinitions.Clear();
            foreach (var bar in rebuiltBars)
                manager.BarDefinitions.Add(bar);
            changeService?.OnComponentChanged(manager, barsProperty, null, null);

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
