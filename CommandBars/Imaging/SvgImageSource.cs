using System.Drawing;
using Svg;

namespace CommandBars.Imaging;

/// <summary>
/// A vector image source backed by an SVG document (Svg.NET). The SVG is parsed
/// once and rasterized on demand to whatever pixel size the current icon-size
/// setting and DPI require, so icons stay crisp at any size. Rendered bitmaps
/// are cached per target pixel size.
/// </summary>
public sealed class SvgImageSource : IImageSource
{
    private readonly SvgDocument _document;
    private readonly Dictionary<int, Bitmap> _cache = new();

    public SvgImageSource(SvgDocument document, string? key = null)
    {
        _document = document ?? throw new ArgumentNullException(nameof(document));
        Key = key;
    }

    public string? Key { get; }

    /// <summary>Parses an SVG document from markup.</summary>
    public static SvgImageSource FromString(string svg, string? key = null)
        => new(SvgDocument.FromSvg<SvgDocument>(svg), key);

    /// <summary>Loads an SVG document from a file path.</summary>
    public static SvgImageSource FromFile(string path)
        => new(SvgDocument.Open(path), path);

    /// <summary>Loads an SVG document from a stream.</summary>
    public static SvgImageSource FromStream(Stream stream, string? key = null)
        => new(SvgDocument.Open<SvgDocument>(stream), key);

    public Image GetImage(int pixelSize, float dpiScale = 1f)
    {
        int target = Math.Max(1, (int)Math.Round(pixelSize * dpiScale));

        if (_cache.TryGetValue(target, out var cached))
            return cached;

        Bitmap bitmap = _document.Draw(target, target);
        _cache[target] = bitmap;
        return bitmap;
    }
}
