using System.Drawing;
using CommandBars.Imaging;

namespace CommandBars.Tests;

/// <summary>A minimal <see cref="IImageSource"/> for tests.</summary>
internal sealed class StubImageSource : IImageSource
{
    public StubImageSource(string? key = "stub") => Key = key;

    public string? Key { get; }

    public Image GetImage(int pixelSize, float dpiScale = 1f)
        => new Bitmap(Math.Max(1, pixelSize), Math.Max(1, pixelSize));
}
