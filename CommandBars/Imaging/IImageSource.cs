using System.Drawing;

namespace CommandBars.Imaging;

/// <summary>
/// Abstraction over a button/menu image so vector (SVG) and raster (PNG/ICO)
/// sources are interchangeable. Concrete implementations
/// (<c>SvgImageSource</c>, <c>BitmapImageSource</c>) arrive in the imaging
/// phase; the model only depends on this contract.
/// </summary>
public interface IImageSource
{
    /// <summary>
    /// Stable key identifying this source (e.g. a resource name or file path),
    /// used for render caching and persistence. May be null for anonymous
    /// in-memory sources.
    /// </summary>
    string? Key { get; }

    /// <summary>
    /// Produces a bitmap rasterized for the given logical size and DPI scale.
    /// Implementations are expected to cache by (size, dpiScale).
    /// </summary>
    /// <param name="pixelSize">Logical size in pixels (square).</param>
    /// <param name="dpiScale">Monitor DPI scale (1.0 == 96 DPI).</param>
    Image GetImage(int pixelSize, float dpiScale = 1f);
}
