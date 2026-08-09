using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing.Design;
using System.IO;
using System.Windows.Forms;
using Microsoft.DotNet.DesignTools.Client;
using CommandBars.Designer.Protocol;
using CommandBars.Designer.Protocol.Endpoints;

namespace CommandBars.Designer.Client;

/// <summary>
/// Opens the multi-file SVG picker in the Visual Studio process, then sends the
/// selected markup to the design server. Modal UI must not be opened from the
/// out-of-process server because it can deadlock the synchronous designer call.
/// </summary>
internal class SvgImportEditor : UITypeEditor
{
    public override UITypeEditorEditStyle GetEditStyle(ITypeDescriptorContext? context)
        => UITypeEditorEditStyle.Modal;

    public override object? EditValue(
        ITypeDescriptorContext? context, IServiceProvider provider, object? value)
    {
        if (provider is null || context?.Instance is null)
            return value;

        using var dialog = new OpenFileDialog
        {
            Title = "Import SVG images",
            Filter = "SVG files (*.svg)|*.svg|All files (*.*)|*.*",
            Multiselect = true,
            CheckFileExists = true,
        };

        if (dialog.ShowDialog() != DialogResult.OK)
            return value;

        var images = new List<StockIconData>();
        foreach (string file in dialog.FileNames)
        {
            try
            {
                string svg = File.ReadAllText(file);
                if (svg.IndexOf("<svg", StringComparison.OrdinalIgnoreCase) < 0)
                    continue;

                images.Add(new StockIconData
                {
                    Key = Path.GetFileNameWithoutExtension(file),
                    Svg = svg,
                });
            }
            catch
            {
                // Keep importing valid selections when one file is unreadable.
            }
        }

        if (images.Count == 0)
            return value;

        var client = provider.GetRequiredService<IDesignToolsClient>();
        var session = provider.GetRequiredService<DesignerSession>();
        var sender = client.Protocol.GetEndpoint<AddStockIconsEndpoint>().GetSender(client);
        sender.SendRequest(new AddStockIconsRequest(
            session.Id, context.Instance, StockIconsSerializer.Serialize(images)));

        return value;
    }
}
