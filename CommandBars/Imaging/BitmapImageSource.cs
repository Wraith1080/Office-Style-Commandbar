using System.Drawing;
using System.Drawing.Drawing2D;

namespace CommandBars.Imaging;

/// <summary>
/// Wraps a raster image (PNG/ICO/Bitmap) as an <see cref="IImageSource"/>.
/// Rasters are scaled with bicubic interpolation; because they are not vector
/// they can soften when enlarged — the SVG source (imaging phase) is the crisp
/// path. Rendered sizes are cached.
/// </summary>
public sealed class BitmapImageSource : IImageSource
{
    private readonly Image _source;
    private readonly Dictionary<int, Bitmap> _cache = new();

    public BitmapImageSource(Image source, string? key = null)
    {
        _source = source ?? throw new ArgumentNullException(nameof(source));
        Key = key;
    }

    public string? Key { get; }

    public Image GetImage(int pixelSize, float dpiScale = 1f)
    {
        int target = Math.Max(1, (int)Math.Round(pixelSize * dpiScale));

        if (_cache.TryGetValue(target, out var cached))
            return cached;

        var bmp = new Bitmap(target, target);
        using (var g = Graphics.FromImage(bmp))
        {
            g.InterpolationMode = InterpolationMode.HighQualityBicubic;
            g.PixelOffsetMode = PixelOffsetMode.HighQuality;
            g.DrawImage(_source, new Rectangle(0, 0, target, target));
        }

        _cache[target] = bmp;
        return bmp;
    }
}
