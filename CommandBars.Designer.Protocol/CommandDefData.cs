using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing.Design;
using System.Windows.Forms;

namespace CommandBars.Designer.Protocol;

/// <summary>
/// A transportable, PropertyGrid-editable catalog command: authored once and
/// referenced from bar items by <see cref="ItemDefData.CommandId"/>.
/// </summary>
public sealed class CommandDefData
{
    [Category("CommandBars"), Description("Stable command id that items reference via their CommandId.")]
    public string Id { get; set; } = string.Empty;

    [Category("CommandBars"), Description("Caption (may contain a single '&' mnemonic marker).")]
    public string Text { get; set; } = string.Empty;

    [Category("CommandBars"), Description("Key of an icon in the manager's SvgImageList. Use the '…' to pick from the connected list.")]
    [Editor(typeof(ImageKeyEditor), typeof(UITypeEditor))]
    public string ImageKey { get; set; } = string.Empty;

    [Category("CommandBars"), Description("Keyboard shortcut for this command.")]
    [DefaultValue(Keys.None)]
    public Keys Shortcut { get; set; } = Keys.None;

    [Category("CommandBars"), Description("Default display style items get when created from this command.")]
    public ItemDisplayData DisplayStyle { get; set; } = ItemDisplayData.ImageAndText;

    public override string ToString()
        => !string.IsNullOrWhiteSpace(Text)
            ? Text.Replace("&", string.Empty)
            : string.IsNullOrWhiteSpace(Id) ? "(command)" : Id;
}

/// <summary>
/// The full design snapshot exchanged between the client dialog and the server:
/// the bars and the command catalog together, so the palette and the tree edit
/// as one unit and persist in a single round-trip.
/// </summary>
public sealed class DesignSnapshot
{
    public List<BarDefData> Bars { get; set; } = new();
    public List<CommandDefData> Commands { get; set; } = new();

    /// <summary>The keys (and rendered thumbnails) available in the connected
    /// SvgImageList, so the ImageKey picker can offer them. Read-only for the
    /// dialog; ignored when the snapshot is sent back.</summary>
    public List<ImageEntryData> Images { get; set; } = new();
}

/// <summary>One entry from the connected SvgImageList, for the ImageKey picker:
/// its key and a small PNG thumbnail (base64) rendered by the design server.</summary>
public sealed class ImageEntryData
{
    public string Key { get; set; } = string.Empty;
    public string Png { get; set; } = string.Empty;
}
