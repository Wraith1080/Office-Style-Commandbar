using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;

namespace CommandBars.Designer.Client;

/// <summary>
/// Client-side gallery of colorful built-in office icons. It runs in the Visual
/// Studio process, while selected vector markup is sent back to the designer
/// server. Pre-rendered PNGs keep the gallery independent of an SVG renderer.
/// </summary>
internal sealed class StockIconsGallery : Form
{
    private readonly ListView _list;
    private readonly ImageList _imageList;

    /// <summary>The icons the user chose (empty unless closed with Add).</summary>
    public List<StockIcon> Selected { get; } = new();

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

        _imageList = new ImageList
        {
            ColorDepth = ColorDepth.Depth32Bit,
            ImageSize = new Size(48, 48),
        };

        for (int i = 0; i < StockIconResources.All.Count; i++)
        {
            var icon = StockIconResources.All[i];
            // ImageList defers cloning its originals until its native handle is
            // created. Do not dispose this image here; ImageList owns it and
            // releases it when the gallery is disposed.
            _imageList.Images.Add(icon.CreateThumbnail());
            string text = keyExists(icon.Key) ? icon.Key + " (in list)" : icon.Key;
            _list.Items.Add(new ListViewItem(text, i) { Tag = icon });
        }
        _list.LargeImageList = _imageList;

        Controls.Add(_list);
        Controls.Add(BuildButtons());
    }

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

    protected override void Dispose(bool disposing)
    {
        if (disposing)
            _imageList.Dispose();
        base.Dispose(disposing);
    }
}
