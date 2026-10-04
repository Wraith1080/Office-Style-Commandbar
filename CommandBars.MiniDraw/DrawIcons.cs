using CommandBars.Imaging;
using Svg;

namespace CommandBars.MiniDraw;

/// <summary>Demo SVG artwork and matching additions, with a form-owned raster cache.</summary>
internal sealed class DrawIcons : IDisposable
{
    private readonly Dictionary<string, IconSource> _icons = new();
    private bool _dark;

    public IImageSource Get(string key)
    {
        if (!_icons.TryGetValue(key, out var icon))
            _icons[key] = icon = new IconSource(key, ReadMarkup(key), _dark);
        return icon;
    }

    private static string ReadMarkup(string key)
    {
        if (key.StartsWith("color:", StringComparison.Ordinal))
        {
            var color = Color.FromArgb(int.Parse(key.Substring(6), System.Globalization.CultureInfo.InvariantCulture));
            string fill = color.A == 0 ? "none" : $"#{color.R:X2}{color.G:X2}{color.B:X2}";
            return "<svg xmlns='http://www.w3.org/2000/svg' viewBox='0 0 32 32'>" +
                $"<rect x='5' y='5' width='22' height='22' fill='{fill}' stroke='#5a6e8c' stroke-width='1.3'/>" +
                (color.A == 0 ? "<path d='M6 26 L26 6' stroke='#c45151' stroke-width='2'/>" : "") + "</svg>";
        }
        using var stream = typeof(DrawIcons).Assembly.GetManifestResourceStream($"MiniDraw.Icons.{key}.svg")
            ?? throw new InvalidOperationException($"Missing MiniDraw icon: {key}");
        using var reader = new StreamReader(stream);
        return reader.ReadToEnd();
    }

    public bool UseDarkPalette(bool dark)
    {
        if (_dark == dark) return false;
        _dark = dark;
        foreach (var icon in _icons.Values) icon.SetDark(dark);
        return true;
    }

    public void Dispose()
    {
        foreach (var icon in _icons.Values) icon.Dispose();
        _icons.Clear();
    }

    private sealed class IconSource : IImageSource, IDisposable
    {
        private readonly Dictionary<int, Bitmap> _cache = new();
        private readonly string _markup;
        private SvgDocument _document = null!;
        public string Key { get; }

        public IconSource(string key, string markup, bool dark)
        {
            Key = key;
            _markup = markup;
            SetDark(dark);
        }

        public void SetDark(bool dark)
        {
            Dispose();
            // Preserve the artwork's fills; only lift outline contrast on dark bars.
            string markup = dark ? _markup.Replace("#5a6e8c", "#becde1").Replace("#41506a", "#d6e0ef")
                .Replace("#28507f", "#93b3d8").Replace("#96782a", "#dec27f") : _markup;
            _document = SvgDocument.FromSvg<SvgDocument>(markup);
        }

        public Image GetImage(int pixelSize, float dpiScale = 1)
        {
            int size = Math.Max(1, (int)Math.Round(pixelSize * dpiScale));
            if (!_cache.TryGetValue(size, out var image))
                _cache[size] = image = _document.Draw(size, size);
            return image;
        }

        public void Dispose()
        {
            foreach (var bitmap in _cache.Values) bitmap.Dispose();
            _cache.Clear();
        }
    }
}
