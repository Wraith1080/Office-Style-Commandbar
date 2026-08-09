using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.Drawing.Design;
using System.Drawing.Imaging;
using System.IO;
using System.Windows.Forms;
using System.Windows.Forms.Design;

namespace CommandBars.Designer.Protocol;

/// <summary>
/// UITypeEditor for an <c>ImageKey</c> property: shows a "…" that opens a picker
/// of the icons in the connected SvgImageList (keys + thumbnails), so you choose
/// an icon instead of typing its key. This runs entirely in the client property
/// grid (in Visual Studio), so it is a normal in-process editor — no type
/// routing needed.
///
/// The available icons are supplied out-of-band via <see cref="AmbientImages"/>,
/// which the bar-definitions dialog sets for its lifetime (a UITypeEditor has no
/// constructor injection, and the property grid edits plain POCOs).
/// </summary>
public class ImageKeyEditor : UITypeEditor
{
    /// <summary>The connected SvgImageList's entries (key + thumbnail), set by the
    /// dialog before the grid is shown.</summary>
    public static IReadOnlyList<ImageEntryData>? AmbientImages { get; set; }

    public override UITypeEditorEditStyle GetEditStyle(ITypeDescriptorContext? context)
        => UITypeEditorEditStyle.Modal;

    public override object? EditValue(
        ITypeDescriptorContext? context, IServiceProvider provider, object? value)
    {
        var editorService = provider?.GetService(typeof(IWindowsFormsEditorService))
            as IWindowsFormsEditorService;
        if (editorService is null)
            return value;

        using var dialog = new ImageKeyPickerForm(AmbientImages, value as string);
        return editorService.ShowDialog(dialog) == DialogResult.OK ? dialog.SelectedKey : value;
    }

    // Draw the current key's thumbnail in the grid's value swatch.
    public override bool GetPaintValueSupported(ITypeDescriptorContext? context) => true;

    public override void PaintValue(PaintValueEventArgs e)
    {
        if (e.Value is not string key || string.IsNullOrEmpty(key) || AmbientImages is null)
            return;

        foreach (var entry in AmbientImages)
        {
            if (!string.Equals(entry.Key, key, StringComparison.Ordinal) || string.IsNullOrEmpty(entry.Png))
                continue;
            try
            {
                using var img = Decode(entry.Png);
                if (img is not null)
                    e.Graphics.DrawImage(img, e.Bounds);
            }
            catch { /* ignore a bad thumbnail in the grid swatch */ }
            return;
        }
    }

    internal static Image? Decode(string pngBase64)
    {
        try
        {
            byte[] bytes = Convert.FromBase64String(pngBase64);
            using var ms = new MemoryStream(bytes);
            using var img = Image.FromStream(ms);
            return new Bitmap(img);
        }
        catch
        {
            return null;
        }
    }
}

/// <summary>The modal picker: a thumbnail list of the connected SvgImageList's
/// icons, plus a "(none)" entry to clear the key.</summary>
internal sealed class ImageKeyPickerForm : Form
{
    private readonly ListView _list;

    public string SelectedKey { get; private set; } = string.Empty;

    public ImageKeyPickerForm(IReadOnlyList<ImageEntryData>? images, string? current)
    {
        AutoScaleMode = AutoScaleMode.Font;
        Font = SystemFonts.MessageBoxFont;

        Text = "Select Icon";
        StartPosition = FormStartPosition.CenterParent;
        MinimumSize = new Size(420, 380);
        Size = new Size(460, 440);
        ShowInTaskbar = false;
        MinimizeBox = false;

        var imageList = new ImageList { ColorDepth = ColorDepth.Depth32Bit, ImageSize = new Size(32, 32) };

        _list = new ListView
        {
            Dock = DockStyle.Fill,
            View = View.LargeIcon,
            MultiSelect = false,
            HideSelection = false,
            LargeImageList = imageList,
        };
        _list.ItemActivate += (_, _) => Accept();

        // "(none)" first — clears the key.
        _list.Items.Add(new ListViewItem("(none)") { Tag = string.Empty });

        if (images is not null)
        {
            int index = 0;
            foreach (var entry in images)
            {
                int imageIndex = -1;
                var thumb = ImageKeyEditor.Decode(entry.Png);
                if (thumb is not null)
                {
                    imageList.Images.Add(thumb);
                    imageIndex = index++;
                }
                var item = new ListViewItem(entry.Key, imageIndex) { Tag = entry.Key };
                _list.Items.Add(item);
                if (string.Equals(entry.Key, current, StringComparison.Ordinal))
                {
                    item.Selected = true;
                    item.EnsureVisible();
                }
            }
        }

        Controls.Add(_list);
        Controls.Add(BuildButtons());
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
        var ok = new Button { Text = "OK", AutoSize = true };
        var cancel = new Button { Text = "Cancel", DialogResult = DialogResult.Cancel, AutoSize = true };
        ok.Click += (_, _) => Accept();
        panel.Controls.Add(ok);
        panel.Controls.Add(cancel);
        AcceptButton = ok;
        CancelButton = cancel;
        return panel;
    }

    private void Accept()
    {
        SelectedKey = _list.SelectedItems.Count > 0
            ? _list.SelectedItems[0].Tag as string ?? string.Empty
            : string.Empty;
        DialogResult = DialogResult.OK;
        Close();
    }
}
