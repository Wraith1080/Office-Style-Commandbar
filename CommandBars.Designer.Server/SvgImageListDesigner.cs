using System.ComponentModel;
using System.ComponentModel.Design;
using System.IO;
using System.Windows.Forms;
using CommandBars.Imaging;
using Microsoft.DotNet.DesignTools.Designers;
using Microsoft.DotNet.DesignTools.Designers.Actions;

namespace CommandBars.Designer.Server;

/// <summary>
/// Out-of-process design-time behavior for <see cref="SvgImageList"/>. Runs
/// inside the WinForms DesignToolsServer process (which has desktop access, so
/// it can show a file dialog and read the picked files).
///
/// Adds an "Import SVG files…" action that multi-selects .svg files and embeds
/// their contents as entries (keyed by file name) in one step — so filling the
/// list doesn't rely on a per-property file picker — plus an "Edit images…"
/// shortcut to the Images collection editor.
/// </summary>
public class SvgImageListDesigner : ComponentDesigner
{
    public override DesignerActionListCollection ActionLists
        => new()
        {
            new ImageListActionList(this)
        };

    internal void ImportSvgFiles()
    {
        if (Component is not SvgImageList list)
            return;

        using var dialog = new OpenFileDialog
        {
            Title = "Import SVG images",
            Filter = "SVG files (*.svg)|*.svg|All files (*.*)|*.*",
            Multiselect = true,
            CheckFileExists = true,
        };

        if (dialog.ShowDialog() != DialogResult.OK)
            return;

        var changeService = Component.Site?.GetService(typeof(IComponentChangeService))
            as IComponentChangeService;
        var property = TypeDescriptor.GetProperties(list)["Images"];

        try
        {
            changeService?.OnComponentChanging(list, property);

            foreach (string file in dialog.FileNames)
            {
                try
                {
                    string content = File.ReadAllText(file);
                    if (content.IndexOf("<svg", StringComparison.OrdinalIgnoreCase) < 0)
                        continue; // not an SVG document; skip it

                    list.Images.Add(new SvgImage
                    {
                        Key = UniqueKey(list, Path.GetFileNameWithoutExtension(file)),
                        Svg = content,
                    });
                }
                catch
                {
                    // Skip an unreadable file; keep importing the rest.
                }
            }

            changeService?.OnComponentChanged(list, property, null, null);
        }
        catch (CheckoutException)
        {
            // Source control declined the edit; nothing imported.
        }
    }

    private static string UniqueKey(SvgImageList list, string baseKey)
    {
        if (string.IsNullOrWhiteSpace(baseKey))
            baseKey = "image";
        if (!list.Contains(baseKey))
            return baseKey;
        for (int i = 2; ; i++)
        {
            string candidate = baseKey + i;
            if (!list.Contains(candidate))
                return candidate;
        }
    }

    /// <summary>
    /// The image list's smart-tag panel. Method items marked as designer verbs
    /// also appear in the component's context menu.
    /// </summary>
    private class ImageListActionList : DesignerActionList
    {
        private const string CategoryName = "CommandBars";

        private readonly SvgImageListDesigner _designer;

        public ImageListActionList(SvgImageListDesigner designer)
            : base(designer.Component)
        {
            _designer = designer;
        }

        public void ImportSvgFiles()
            => _designer.ImportSvgFiles();

        public void EditImages()
            => _designer.InvokePropertyEditor(nameof(SvgImageList.Images));

        public override DesignerActionItemCollection GetSortedActionItems()
        {
            DesignerActionItemCollection items = new();

            items.Add(new DesignerActionHeaderItem(CategoryName));

            items.Add(new DesignerActionMethodItem(
                this,
                nameof(ImportSvgFiles),
                "Import SVG files…",
                CategoryName,
                "Picks one or more .svg files and embeds their markup as new entries, keyed by file name.",
                true));

            items.Add(new DesignerActionMethodItem(
                this,
                nameof(EditImages),
                "Edit images…",
                CategoryName,
                "Opens the Images collection editor.",
                true));

            return items;
        }
    }
}
