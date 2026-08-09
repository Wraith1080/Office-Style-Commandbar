using System;
using System.ComponentModel;
using System.ComponentModel.Design;
using CommandBars.Imaging;
using Microsoft.DotNet.DesignTools.Designers;
using Microsoft.DotNet.DesignTools.Designers.Actions;

namespace CommandBars.Designer.Server;

/// <summary>
/// Out-of-process design-time behavior for <see cref="SvgImageList"/>.
///
/// Smart-tag actions:
///  • "Add stock icons…" opens the built-in gallery. It is routed to a
///    CLIENT-side editor (in Visual Studio) via InvokePropertyEditor on the
///    hidden StockIconGallery property — a custom modal dialog shown from the
///    design SERVER process freezes the designer, so the gallery must run
///    client-side. The chosen icons are sent back to the server to embed.
///  • "Import SVG files…" is also routed to a CLIENT-side editor. Even native
///    common dialogs can intermittently deadlock a synchronous design-server
///    request, so no modal UI is opened by this server-side designer.
///  • "Edit images…" opens the Images collection editor.
/// </summary>
public class SvgImageListDesigner : ComponentDesigner
{
    public override DesignerActionListCollection ActionLists
        => new()
        {
            new ImageListActionList(this)
        };

    /// <summary>Opens the client-side stock-icon gallery (routed editor).</summary>
    internal void AddStockIcons()
        => InvokePropertyEditor("StockIconGallery");

    internal void ImportSvgFiles()
        => InvokePropertyEditor(nameof(SvgImageList.SvgImport));

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

        public void AddStockIcons()
            => _designer.AddStockIcons();

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
                nameof(AddStockIcons),
                "Add stock icons…",
                CategoryName,
                "Pick from a gallery of built-in office-style icons and embed them as new entries.",
                true));

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
