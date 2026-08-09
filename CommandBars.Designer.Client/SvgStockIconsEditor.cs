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
/// Client-side editor that opens the stock-icon gallery (in Visual Studio, so no
/// server-process freeze) and sends the chosen icons to the server to embed into
/// the SvgImageList. Invoked from the SvgImageList's "Add stock icons…" smart-tag
/// action via InvokePropertyEditor on its hidden StockIconGallery property.
/// </summary>
internal class SvgStockIconsEditor : UITypeEditor
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

        // context.Instance is the proxy of the SvgImageList being edited.
        object imageListProxy = context.Instance;

        using var gallery = new StockIconsGallery(_ => false);
        if (editorService.ShowDialog(gallery) == DialogResult.OK && gallery.Selected.Count > 0)
        {
            var icons = new List<StockIconData>();
            foreach (var icon in gallery.Selected)
                icons.Add(new StockIconData { Key = icon.Key, Svg = icon.Svg });

            var sender = client.Protocol.GetEndpoint<AddStockIconsEndpoint>().GetSender(client);
            sender.SendRequest(new AddStockIconsRequest(
                session.Id, imageListProxy, StockIconsSerializer.Serialize(icons)));
        }

        return value;
    }
}
