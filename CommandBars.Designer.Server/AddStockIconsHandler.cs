using System.ComponentModel;
using System.ComponentModel.Design;
using Microsoft.DotNet.DesignTools.Protocol.Endpoints;
using CommandBars.Imaging;
using CommandBars.Designer.Protocol;
using CommandBars.Designer.Protocol.Endpoints;

namespace CommandBars.Designer.Server;

/// <summary>
/// Server handler for <see cref="EndpointNames.AddStockIcons"/>: embeds the
/// icons the client gallery selected into the SvgImageList's Images collection,
/// inside a designer transaction, with keys made unique against existing
/// entries, so the .Designer.cs regenerates.
/// </summary>
[ExportRequestHandler(EndpointNames.AddStockIcons)]
internal class AddStockIconsHandler
    : RequestHandler<AddStockIconsRequest, AddStockIconsResponse>
{
    public override AddStockIconsResponse HandleRequest(AddStockIconsRequest request)
    {
        var list = (SvgImageList)request.ImageList!;
        var icons = StockIconsSerializer.Deserialize(request.IconsJson);
        if (icons.Count == 0)
            return AddStockIconsResponse.Empty;

        var host = GetDesignerHost(request.SessionId);
        var changeService = host?.GetService(typeof(IComponentChangeService)) as IComponentChangeService;
        var property = TypeDescriptor.GetProperties(list)[nameof(SvgImageList.Images)];

        DesignerTransaction? tx = host?.CreateTransaction("Add stock icons");
        try
        {
            changeService?.OnComponentChanging(list, property);

            foreach (var icon in icons)
            {
                list.Images.Add(new SvgImage
                {
                    Key = UniqueKey(list, icon.Key),
                    Svg = icon.Svg,
                });
            }

            changeService?.OnComponentChanged(list, property, null, null);
            tx?.Commit();
            tx = null;
        }
        finally
        {
            tx?.Cancel();
        }

        return AddStockIconsResponse.Empty;
    }

    private static string UniqueKey(SvgImageList list, string baseKey)
    {
        if (string.IsNullOrWhiteSpace(baseKey))
            baseKey = "image";
        if (!list.Contains(baseKey))
            return baseKey;
        for (int i = 2; ; i++)
        {
            string candidate = baseKey + i;
            if (!list.Contains(candidate))
                return candidate;
        }
    }
}
