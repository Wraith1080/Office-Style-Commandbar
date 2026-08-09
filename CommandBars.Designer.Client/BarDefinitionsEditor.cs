using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing.Design;
using System.Windows.Forms;
using System.Windows.Forms.Design;
using Microsoft.DotNet.DesignTools.Client;
using CommandBars.Designer.Protocol;
using CommandBars.Designer.Protocol.Endpoints;

namespace CommandBars.Designer.Client;

/// <summary>
/// The client-side editor for <c>CommandBarManager.BarDefinitions</c>. Invoked
/// both from the property grid's "…" button and from the manager's smart-tag
/// "Edit toolbars and menus…" action (which calls InvokePropertyEditor on this
/// property).
///
/// It asks the server for a JSON snapshot of the current definitions, shows a
/// modal dialog to edit them (client-side, in VS — so no server-process UI
/// freeze), and on OK sends the edited JSON back for the server to rebuild the
/// real definition objects and regenerate the designer code.
/// </summary>
internal class BarDefinitionsEditor : UITypeEditor
{
    public override UITypeEditorEditStyle GetEditStyle(ITypeDescriptorContext? context)
        => UITypeEditorEditStyle.Modal;

    public override object? EditValue(
        ITypeDescriptorContext? context, IServiceProvider provider, object? value)
    {
        if (provider is null || context?.Instance is null)
            return value;

        var editorService = provider.GetRequiredService<IWindowsFormsEditorService>();
        var client = provider.GetRequiredService<IDesignToolsClient>();
        var session = provider.GetRequiredService<DesignerSession>();

        // context.Instance is the proxy of the CommandBarManager being edited.
        object managerProxy = context.Instance;

        // 1) Pull the current snapshot (bars + command catalog) from the server.
        var getSender = client.Protocol.GetEndpoint<GetBarDefinitionsEndpoint>().GetSender(client);
        var getResponse = getSender.SendRequest(new GetBarDefinitionsRequest(managerProxy));
        DesignSnapshot snapshot = DefinitionsSerializer.Deserialize(getResponse.DefinitionsJson);

        // 2) Edit them in a client-side dialog.
        using var dialog = new BarDefinitionsDialog(snapshot);
        if (editorService.ShowDialog(dialog) == DialogResult.OK)
        {
            // 3) Send the edited snapshot back; the server rebuilds + regenerates code.
            var setSender = client.Protocol.GetEndpoint<SetBarDefinitionsEndpoint>().GetSender(client);
            setSender.SendRequest(new SetBarDefinitionsRequest(
                session.Id,
                managerProxy,
                DefinitionsSerializer.Serialize(dialog.Snapshot)));
        }

        // The property value itself (the List proxy) is unchanged on the client;
        // the server applied the edit to the real component.
        return value;
    }
}
