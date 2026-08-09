using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Reflection;

namespace CommandBars.Designer.Client;

/// <summary>One colorful built-in stock icon. SVG markup and its pre-rendered
/// gallery thumbnail are embedded as resources so the client needs no SVG
/// rendering dependency.</summary>
internal sealed class StockIcon
{
    private const string ResourcePrefix = "CommandBars.Designer.Client.StockIcons.";

    public StockIcon(string key)
    {
        Key = key;
        Svg = ReadTextResource(key + ".svg");
    }

    public string Key { get; }
    public string Svg { get; }

    public Image CreateThumbnail()
    {
        using Stream stream = OpenResource(Key + ".png");
        using var image = Image.FromStream(stream);
        return new Bitmap(image);
    }

    private static string ReadTextResource(string name)
    {
        using Stream stream = OpenResource(name);
        using var reader = new StreamReader(stream);
        return reader.ReadToEnd();
    }

    private static Stream OpenResource(string name)
        => Assembly.GetExecutingAssembly().GetManifestResourceStream(ResourcePrefix + name)
           ?? throw new InvalidOperationException("Missing stock icon resource: " + name);
}

/// <summary>Colorful productivity icons offered by the stock-icon gallery.</summary>
internal static class StockIconResources
{
    public static IReadOnlyList<StockIcon> All { get; } = new StockIcon[]
    {
        new("new"),
        new("open"),
        new("save"),
        new("print"),
        new("print-preview"),
        new("cut"),
        new("copy"),
        new("paste"),
        new("undo"),
        new("redo"),
        new("search"),
        new("replace"),
        new("spell-check"),
        new("bold"),
        new("italic"),
        new("underline"),
        new("font-color"),
        new("highlight"),
        new("picture"),
        new("table"),
        new("hyperlink"),
        new("email"),
        new("calendar"),
        new("help"),
    };
}
