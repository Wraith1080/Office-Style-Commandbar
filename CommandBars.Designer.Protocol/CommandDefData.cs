using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing.Design;
using System.Windows.Forms;

namespace CommandBars.Designer.Protocol;

/// <summary>Protocol mirror of a reusable catalog entry's semantic kind.</summary>
public enum CommandKindData
{
    Action,
    Toggle,
    Popup,
    SplitButton,
    ComboBox,
    Label,
}

/// <summary>Protocol mirror of a compound popup's content source.</summary>
public enum CommandContentSourceData
{
    Authored,
    ToolbarList,
    ThemeList,
}

/// <summary>Protocol mirror of the runtime command check state.</summary>
public enum CommandCheckStateData
{
    Unchecked,
    Checked,
    Indeterminate,
}

/// <summary>Protocol mirror of a catalog placement's structural kind.</summary>
public enum CommandPlacementKindData
{
    Command,
    Separator,
}

/// <summary>
/// A transportable, PropertyGrid-editable reusable catalog entry.
/// </summary>
public sealed class CommandDefData
{
    [Category("CommandBars"), Description("Stable command id that items reference via their CommandId.")]
    public string Id { get; set; } = string.Empty;

    [Category("CommandBars"), Description("Reusable semantic shape of this catalog entry.")]
    [DefaultValue(CommandKindData.Action)]
    [RefreshProperties(RefreshProperties.All)]
    public CommandKindData Kind { get; set; } = CommandKindData.Action;

    [Category("CommandBars"), Description("Caption (may contain a single '&' mnemonic marker).")]
    public string Text { get; set; } = string.Empty;

    [Category("CommandBars"), Description("Key of an icon in the manager's SvgImageList. Use the '…' to pick from the connected list.")]
    [Editor(typeof(ImageKeyEditor), typeof(UITypeEditor))]
    public string ImageKey { get; set; } = string.Empty;

    [Category("CommandBars"), Description("Keyboard shortcut for this command.")]
    [DefaultValue(Keys.None)]
    public Keys Shortcut { get; set; } = Keys.None;

    [Category("CommandBars"), Description("Optional ScreenTip; empty uses the caption.")]
    public string ToolTip { get; set; } = string.Empty;

    [Category("CommandBars"), Description("Default display style items get when created from this command.")]
    public ItemDisplayData DisplayStyle { get; set; } = ItemDisplayData.ImageAndText;

    [Category("CommandBars"), Description("Initial checked state for a Toggle entry.")]
    [DefaultValue(CommandCheckStateData.Unchecked)]
    public CommandCheckStateData InitialChecked { get; set; } = CommandCheckStateData.Unchecked;

    [Category("CommandBars"), Description("How a Popup entry obtains its children.")]
    [DefaultValue(CommandContentSourceData.Authored)]
    public CommandContentSourceData ContentSource { get; set; } = CommandContentSourceData.Authored;

    [Category("CommandBars"), Description("Show a tear-off grip on Popup or SplitButton dropdowns.")]
    public bool TearOff { get; set; }

    [Category("CommandBars"), Description("Optional detached-palette caption.")]
    public string TearOffTitle { get; set; } = string.Empty;

    [Category("CommandBars"), Description("Icon-grid column count; zero uses a normal list.")]
    public int PaletteColumns { get; set; }

    [Category("CommandBars"), Description("Preferred logical width of a ComboBox entry.")]
    [DefaultValue(120)]
    public int ComboWidth { get; set; } = 120;

    [Category("CommandBars"), Description("Initial entries for a ComboBox entry.")]
    public List<string> ComboItems { get; set; } = new();

    [Browsable(false)]
    public List<CommandPlacementData> Items { get; set; } = new();

    [Category("CommandBars"), Description("Offer this complete entry in the runtime Customize palette.")]
    public bool IncludeInCommandList { get; set; }

    public override string ToString()
        => !string.IsNullOrWhiteSpace(Text)
            ? Text.Replace("&", string.Empty)
            : string.IsNullOrWhiteSpace(Id) ? "(command)" : Id;
}

/// <summary>
/// A transportable lightweight reference inside a Popup or SplitButton catalog
/// entry. Stage 2 will use the same shape for top-level bars.
/// </summary>
public sealed class CommandPlacementData
{
    [Category("CommandBars")]
    [DefaultValue(CommandPlacementKindData.Command)]
    public CommandPlacementKindData Kind { get; set; } = CommandPlacementKindData.Command;

    [Category("CommandBars")]
    public string CommandId { get; set; } = string.Empty;

    [Category("CommandBars")]
    public string Name { get; set; } = string.Empty;

    [Category("CommandBars")]
    [DefaultValue(true)]
    public bool Visible { get; set; } = true;

    [Category("CommandBars")]
    public bool BeginGroup { get; set; }

    [Category("CommandBars")]
    [DefaultValue(3)]
    public int Priority { get; set; } = 3;

    [Category("CommandBars")]
    [DefaultValue(true)]
    public bool UseCatalogDisplayStyle { get; set; } = true;

    [Category("CommandBars")]
    public ItemDisplayData DisplayStyle { get; set; } = ItemDisplayData.ImageAndText;

    public override string ToString()
        => Kind == CommandPlacementKindData.Separator
            ? "Separator"
            : string.IsNullOrWhiteSpace(CommandId) ? "(missing command)" : CommandId;
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
