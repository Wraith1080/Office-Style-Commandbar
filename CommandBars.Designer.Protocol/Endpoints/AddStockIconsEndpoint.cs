using System;
using Microsoft.DotNet.DesignTools.Protocol;
using System.Composition;
using Microsoft.DotNet.DesignTools.Protocol.DataPipe;
using Microsoft.DotNet.DesignTools.Protocol.Endpoints;

namespace CommandBars.Designer.Protocol.Endpoints;

/// <summary>
/// Client → server: embed the chosen stock icons into the SvgImageList. The
/// gallery (and its rendering) run client-side in Visual Studio; only the picked
/// {key, svg} pairs cross to the server, which adds them to the Images
/// collection inside a designer transaction.
/// </summary>
[Shared]
[ExportEndpoint]
public class AddStockIconsEndpoint
    : Endpoint<AddStockIconsRequest, AddStockIconsResponse>
{
    public override string Name => EndpointNames.AddStockIcons;

    protected override AddStockIconsRequest CreateRequest(IDataPipeReader reader)
        => new(reader);

    protected override AddStockIconsResponse CreateResponse(IDataPipeReader reader)
        => new(reader);
}

public class AddStockIconsRequest : Request
{
    /// <summary>The design session (server-side reaches the host + change service).</summary>
    public SessionId SessionId { get; private set; }

    /// <summary>Proxy of the SvgImageList to add to.</summary>
    public object? ImageList { get; private set; }

    /// <summary>The chosen icons, serialized as JSON (a StockIconData[]).</summary>
    public string IconsJson { get; private set; } = string.Empty;

    public AddStockIconsRequest(SessionId sessionId, object? imageList, string iconsJson)
    {
        SessionId = sessionId.IsNull ? throw new ArgumentNullException(nameof(sessionId)) : sessionId;
        ImageList = imageList ?? throw new ArgumentNullException(nameof(imageList));
        IconsJson = iconsJson ?? throw new ArgumentNullException(nameof(iconsJson));
    }

    public AddStockIconsRequest(IDataPipeReader reader) : base(reader) { }

    protected override void ReadProperties(IDataPipeReader reader)
    {
        SessionId = reader.ReadSessionId(nameof(SessionId));
        ImageList = reader.ReadObject(nameof(ImageList));
        IconsJson = reader.ReadString(nameof(IconsJson)) ?? string.Empty;
    }

    protected override void WriteProperties(IDataPipeWriter writer)
    {
        writer.Write(nameof(SessionId), SessionId);
        writer.WriteObject(nameof(ImageList), ImageList);
        writer.Write(nameof(IconsJson), IconsJson);
    }
}

public class AddStockIconsResponse : Response.Empty
{
    public static new AddStockIconsResponse Empty { get; } = new();

    private AddStockIconsResponse() { }

    public AddStockIconsResponse(IDataPipeReader reader) : base(reader) { }
}
