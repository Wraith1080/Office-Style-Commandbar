using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.Windows.Forms;

namespace CommandBars.Designer.Client;

/// <summary>
/// Client-side (in-VS) gallery of the built-in <see cref="StockIconResources"/>,
/// with a color palette. Runs in the Visual Studio process, so — unlike a dialog
/// shown from the design server — it does not freeze the designer. Thumbnails
/// come from pre-rendered PNGs (tinted live to the chosen color); when icons are
/// added, their SVG is recolored so the embedded markup is truly colored.
/// </summary>
internal sealed class StockIconsGallery : Form
{
    /// <summary>The placeholder color every stock icon's markup uses; recoloring
    /// is a swap of this value. Must match StockIcons' authored color.</summary>
    public const string PlaceholderColor = "#2b2f36";

    private static readonly (string Name, Color Color)[] s_palette =
    {
        ("default", ColorFromHex(PlaceholderColor)),
        ("blue",    ColorFromHex("#2f6fb5")),
        ("green",   ColorFromHex("#2f9e44")),
        ("red",     ColorFromHex("#e03131")),
        ("orange",  ColorFromHex("#e8710a")),
        ("purple",  ColorFromHex("#7048e8")),
        ("teal",    ColorFromHex("#0ca678")),
        ("gray",    ColorFromHex("#868e96")),
    };

    private readonly ListView _list;
    private readonly List<Image> _baseThumbs = new();
    private readonly List<Button> _swatches = new();
    private ImageList? _imageList;

    /// <summary>The icons the user chose (empty unless closed with Add).</summary>
    public List<StockIcon> Selected { get; } = new();

    /// <summary>The chosen color and its short name (for the entry key suffix).</summary>
    public Color SelectedColor { get; private set; } = ColorFromHex(PlaceholderColor);
    public string ColorName { get; private set; } = "default";

    public StockIconsGallery(Func<string, bool> keyExists)
    {
        AutoScaleMode = AutoScaleMode.Font;
        Font = SystemFonts.MessageBoxFont;

        Text = "Add Stock Icons";
        StartPosition = FormStartPosition.CenterParent;
        MinimumSize = new Size(580, 500);
        Size = new Size(640, 600);
        ShowInTaskbar = false;
        MinimizeBox = false;

        _list = new ListView
        {
            Dock = DockStyle.Fill,
            View = View.LargeIcon,
            CheckBoxes = true,
            MultiSelect = true,
            HideSelection = false,
        };
        _list.ItemActivate += (_, _) => ToggleFocused();

        for (int i = 0; i < StockIconResources.All.Count; i++)
        {
            var icon = StockIconResources.All[i];
            _baseThumbs.Add(icon.CreateThumbnail());
            string text = keyExists(icon.Key) ? icon.Key + " (in list)" : icon.Key;
            _list.Items.Add(new ListViewItem(text, i) { Tag = icon });
        }

        Controls.Add(_list);
        Controls.Add(BuildColorBar());
        Controls.Add(BuildButtons());

        ApplyColor(s_palette[0].Name, s_palette[0].Color);
    }

    // ---- color bar ----

    private FlowLayoutPanel BuildColorBar()
    {
        var bar = new FlowLayoutPanel
        {
            Dock = DockStyle.Top,
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            Padding = new Padding(6, 6, 6, 2),
            WrapContents = true,
        };
        bar.Controls.Add(new Label
        {
            Text = "Color:",
            AutoSize = true,
            Padding = new Padding(0, 5, 4, 0),
        });

        foreach (var (name, color) in s_palette)
        {
            var swatch = new Button
            {
                BackColor = color,
                Width = 24,
                Height = 24,
                Margin = new Padding(2),
                FlatStyle = FlatStyle.Flat,
                Tag = name,
            };
            swatch.FlatAppearance.BorderColor = Color.Gray;
            swatch.FlatAppearance.BorderSize = 1;
            var capturedName = name;
            var capturedColor = color;
            swatch.Click += (_, _) => ApplyColor(capturedName, capturedColor);
            _swatches.Add(swatch);
            bar.Controls.Add(swatch);
        }

        var custom = new Button { Text = "Custom…", AutoSize = true, Margin = new Padding(6, 1, 2, 1) };
        custom.Click += (_, _) => PickCustom();
        bar.Controls.Add(custom);

        return bar;
    }

    private void PickCustom()
    {
        using var dlg = new ColorDialog { Color = SelectedColor, FullOpen = true };
        if (dlg.ShowDialog(this) == DialogResult.OK)
            ApplyColor("custom", dlg.Color);
    }

    private void ApplyColor(string name, Color color)
    {
        SelectedColor = color;
        ColorName = name;

        var newImages = new ImageList { ColorDepth = ColorDepth.Depth32Bit, ImageSize = new Size(48, 48) };
        foreach (var thumb in _baseThumbs)
            newImages.Images.Add(Tint(thumb, color));

        _list.LargeImageList = newImages;
        _imageList?.Dispose();
        _imageList = newImages;
        _list.Invalidate();

        // Highlight the active preset swatch (thicker border); none for custom.
        foreach (var s in _swatches)
            s.FlatAppearance.BorderSize = (s.Tag as string) == name ? 3 : 1;
    }

    /// <summary>Recolors a single-color (dark-on-transparent) thumbnail to the target
    /// color, preserving the anti-aliased alpha edges.</summary>
    private static Image Tint(Image source, Color color)
    {
        var bmp = new Bitmap(source.Width, source.Height, PixelFormat.Format32bppArgb);
        using var g = Graphics.FromImage(bmp);
        var matrix = new ColorMatrix(new[]
        {
            new float[] { 0, 0, 0, 0, 0 },
            new float[] { 0, 0, 0, 0, 0 },
            new float[] { 0, 0, 0, 0, 0 },
            new float[] { 0, 0, 0, 1, 0 },                                   // keep source alpha
            new[] { color.R / 255f, color.G / 255f, color.B / 255f, 0, 1 },  // set RGB to target
        });
        using var attrs = new ImageAttributes();
        attrs.SetColorMatrix(matrix);
        g.DrawImage(source,
            new Rectangle(0, 0, source.Width, source.Height),
            0, 0, source.Width, source.Height, GraphicsUnit.Pixel, attrs);
        return bmp;
    }

    // ---- selection ----

    private void ToggleFocused()
    {
        if (_list.FocusedItem is { } item)
            item.Checked = !item.Checked;
    }

    private Panel BuildButtons()
    {
        var panel = new FlowLayoutPanel
        {
            Dock = DockStyle.Bottom,
            FlowDirection = FlowDirection.RightToLeft,
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            Padding = new Padding(8),
        };

        var add = new Button { Text = "Add", DialogResult = DialogResult.OK, AutoSize = true };
        var cancel = new Button { Text = "Cancel", DialogResult = DialogResult.Cancel, AutoSize = true };
        var selectAll = new Button { Text = "Select All", AutoSize = true };
        var clear = new Button { Text = "Clear", AutoSize = true };
        selectAll.Click += (_, _) => SetAllChecked(true);
        clear.Click += (_, _) => SetAllChecked(false);

        add.Click += (_, _) =>
        {
            Selected.Clear();
            foreach (ListViewItem item in _list.CheckedItems)
                if (item.Tag is StockIcon icon)
                    Selected.Add(icon);
        };

        panel.Controls.Add(add);
        panel.Controls.Add(cancel);
        panel.Controls.Add(clear);
        panel.Controls.Add(selectAll);
        AcceptButton = add;
        CancelButton = cancel;
        return panel;
    }

    private void SetAllChecked(bool value)
    {
        foreach (ListViewItem item in _list.Items)
            item.Checked = value;
    }

    private static Color ColorFromHex(string hex)
    {
        int r = Convert.ToInt32(hex.Substring(1, 2), 16);
        int g = Convert.ToInt32(hex.Substring(3, 2), 16);
        int b = Convert.ToInt32(hex.Substring(5, 2), 16);
        return Color.FromArgb(r, g, b);
    }
}
