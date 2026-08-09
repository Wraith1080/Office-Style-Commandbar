using System.ComponentModel;
using System.IO;

namespace CommandBars.Imaging;

/// <summary>
/// One named entry in a <see cref="SvgImageList"/>: a key plus the raw SVG
/// markup. The markup is stored inline (it serializes into the designer file as
/// a string), so icons travel with the project — there are no file paths to
/// resolve at design time or run time. The rasterized source is built lazily and
/// cached (per size, by the underlying <see cref="SvgImageSource"/>).
/// </summary>
[TypeConverter(typeof(ExpandableObjectConverter))]
public sealed class SvgImage
{
    private string _svg = string.Empty;
    private SvgImageSource? _source;

    /// <summary>Stable name used to reference this image from an item.</summary>
    [Category("CommandBars")]
    [DefaultValue("")]
    public string Key { get; set; } = string.Empty;

    /// <summary>
    /// The SVG document markup, stored inline. Edit it in the grid's multiline
    /// editor, or fill it by picking a file with <see cref="Browse"/>. The editor
    /// referenced here is the framework's built-in MultilineStringEditor, which
    /// the out-of-process designer loads without a custom design assembly.
    /// </summary>
    [Category("CommandBars")]
    [DefaultValue("")]
    // Client-routed editor (markup box + in-VS "Load from file…"); name matches
    // EditorNames.SvgMarkupEditor. Running the file dialog on the client avoids
    // the server-process freeze.
    [Editor("SvgMarkupEditor", typeof(System.Drawing.Design.UITypeEditor))]
    public string Svg
    {
        get => _svg;
        set
        {
            _svg = value ?? string.Empty;
            _source = null; // re-parse on next use
        }
    }

    /// <summary>
    /// A convenience picker: click the ellipsis in the grid to choose an .svg
    /// file, and (when it's a valid SVG) its contents are loaded into
    /// <see cref="Svg"/> and an empty <see cref="Key"/> is seeded from the file
    /// name — so you don't have to paste markup by hand. This uses the framework's
    /// built-in FileNameEditor (which the out-of-process designer loads); the
    /// import is done in the setter. The path itself is not serialized.
    /// </summary>
    [Category("CommandBars")]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    [Editor(
        "System.Windows.Forms.Design.FileNameEditor, System.Design, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b03f5f7f11d50a3a",
        "System.Drawing.Design.UITypeEditor, System.Drawing, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b03f5f7f11d50a3a")]
    public string Browse
    {
        get => _browse;
        set
        {
            _browse = value ?? string.Empty;
            ImportFromFile(_browse);
        }
    }

    private string _browse = string.Empty;

    // Reads an .svg file's contents into Svg (seeding Key if empty). Runs in the
    // designer server process, which has access to the picked file on disk.
    private void ImportFromFile(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
            return;
        try
        {
            if (!File.Exists(path))
                return;
            string content = File.ReadAllText(path);
            if (content.IndexOf("<svg", StringComparison.OrdinalIgnoreCase) < 0)
                return; // not an SVG document; leave Svg untouched
            Svg = content;
            if (string.IsNullOrWhiteSpace(Key))
                Key = Path.GetFileNameWithoutExtension(path);
        }
        catch
        {
            // Ignore unreadable files; the grid value simply won't take effect.
        }
    }

    /// <summary>Builds (and caches) the image source, or null if the markup is empty/invalid.</summary>
    public IImageSource? GetSource()
    {
        if (string.IsNullOrWhiteSpace(_svg))
            return null;
        if (_source is not null)
            return _source;
        try
        {
            _source = SvgImageSource.FromString(_svg, Key);
        }
        catch
        {
            _source = null;
        }
        return _source;
    }

    public override string ToString()
        => string.IsNullOrWhiteSpace(Key) ? "(unnamed)" : Key;
}

/// <summary>
/// A design-time-friendly collection of named SVG icons. Drop it on a form,
/// assign it to <see cref="CommandBarManager.Images"/>, and reference entries
/// from item definitions by their <see cref="SvgImage.Key"/>. Because each entry
/// carries its SVG markup, the whole icon set is embedded in the designer file.
/// </summary>
[ToolboxItem(true)]
[DesignerCategory("Component")]
[Designer("CommandBars.Designer.Server.SvgImageListDesigner, CommandBars.Designer.Server")]
public sealed class SvgImageList : Component
{
    private readonly List<SvgImage> _images = new();

    public SvgImageList()
    {
    }

    /// <summary>Container-aware constructor so the designer sites/disposes it.</summary>
    public SvgImageList(IContainer container) : this()
    {
        container?.Add(this);
    }

    /// <summary>The icons, each a key plus inline SVG markup.</summary>
    // No custom [Editor] here: the out-of-process designer supplies the built-in
    // collection editor for a List<T> automatically, and a custom CollectionEditor
    // subclass wouldn't load without a separate design assembly.
    [Category("CommandBars")]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Content)]
    public List<SvgImage> Images => _images;

    /// <summary>
    /// A design-time action entry point (not a stored value): clicking its "…"
    /// in the grid — or the "Add stock icons…" smart tag, which invokes this
    /// property's editor — opens the built-in stock-icon gallery. The gallery is
    /// a CLIENT-side dialog (routed by name to SvgStockIconsEditor); the chosen
    /// icons are added to <see cref="Images"/> by the design server. Never
    /// serialized and always empty.
    /// </summary>
    [Category("CommandBars")]
    [DisplayName("Add Stock Icons")]
    [Description("Opens a gallery of built-in office-style icons to embed into this list.")]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    [Editor("SvgStockIconsEditor", typeof(System.Drawing.Design.UITypeEditor))]
    public string StockIconGallery
    {
        get => string.Empty;
        set { _ = value; }
    }

    /// <summary>
    /// A design-time action entry point for importing multiple SVG files. The
    /// routed editor runs in the Visual Studio client process so its native file
    /// dialog cannot block the out-of-process design server. Selected SVG markup
    /// is sent back to the server and embedded in <see cref="Images"/>.
    /// Never serialized and always empty.
    /// </summary>
    [Category("CommandBars")]
    [DisplayName("Import SVG Files")]
    [Description("Imports one or more SVG files into this image list.")]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    [Editor("SvgImportEditor", typeof(System.Drawing.Design.UITypeEditor))]
    public string SvgImport
    {
        get => string.Empty;
        set { _ = value; }
    }

    /// <summary>Number of entries.</summary>
    [Browsable(false)]
    public int Count => _images.Count;

    /// <summary>True if an entry with this key exists (ordinal, case-sensitive).</summary>
    public bool Contains(string? key) => IndexOfKey(key) >= 0;

    /// <summary>Index of the entry with this key, or -1.</summary>
    public int IndexOfKey(string? key)
    {
        if (string.IsNullOrEmpty(key))
            return -1;
        for (int i = 0; i < _images.Count; i++)
            if (string.Equals(_images[i].Key, key, StringComparison.Ordinal))
                return i;
        return -1;
    }

    /// <summary>Image source for a key, or null if absent/empty.</summary>
    public IImageSource? Get(string? key)
    {
        int i = IndexOfKey(key);
        return i >= 0 ? _images[i].GetSource() : null;
    }

    /// <summary>Image source at an index, or null if out of range/empty.</summary>
    public IImageSource? Get(int index)
        => index >= 0 && index < _images.Count ? _images[index].GetSource() : null;

    /// <summary>The keys currently defined, in order.</summary>
    public IEnumerable<string> Keys => _images.Select(e => e.Key);
}
