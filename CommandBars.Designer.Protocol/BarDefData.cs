using System.Collections.Generic;
using System.ComponentModel;
using System.Text.Json.Serialization;

namespace CommandBars.Designer.Protocol;

// Protocol-local enums. They MUST NOT reference CommandBars.Model, because this
// assembly is shared with the net472 client, which cannot reference the .NET 8
// runtime library. The server maps these to/from the real CommandBars.Model
// enums when it snapshots or rebuilds. Values mirror the runtime enums.

/// <summary>Top-level bar role (popups are not top-level bars).</summary>
public enum BarKind
{
    MenuBar,
    Toolbar,
}

/// <summary>Initial dock edge for a bar.</summary>
public enum DockEdgeData
{
    Top,
    Left,
    Right,
    Bottom,
}

/// <summary>Concrete kind of an item on a bar.</summary>
public enum ItemKindData
{
    Button,
    ToggleButton,
    SplitButton,
    Popup,
    Separator,
    Label,
    ComboBox,
}

/// <summary>How an item shows image vs. text.</summary>
public enum ItemDisplayData
{
    ImageOnly,
    TextOnly,
    ImageAndText,
}

/// <summary>
/// A transportable, PropertyGrid-editable description of one bar. Serialized to
/// JSON for the cross-process round-trip; edited client-side in the bar-defs
/// dialog; rebuilt server-side into a real <c>BarDefinition</c>.
/// </summary>
public sealed class BarDefData
{
    [Category("CommandBars"), Description("Stable identity used for lookup and persistence.")]
    public string Name { get; set; } = string.Empty;

    [Category("CommandBars"), Description("Display title (the caption when the bar floats).")]
    public string Text { get; set; } = string.Empty;

    [Category("CommandBars"), Description("Whether this is the menu bar or a toolbar.")]
    public BarKind BarType { get; set; } = BarKind.Toolbar;

    [Category("CommandBars"), Description("Initial dock edge.")]
    public DockEdgeData Dock { get; set; } = DockEdgeData.Top;

    [Category("CommandBars"), Description("Whether the bar is shown.")]
    public bool Visible { get; set; } = true;

    [Category("CommandBars"), Description("Icon size for this bar, in logical pixels.")]
    public int IconSize { get; set; } = 24;

    [Category("CommandBars"), Description("Whether the user may undock this bar into a floating window.")]
    public bool AllowFloat { get; set; } = true;

    [Category("CommandBars"), Description("Whether the user may edit this bar in customize mode.")]
    public bool AllowCustomize { get; set; } = true;

    /// <summary>The bar's items. Managed by the tree in the editor, not shown in the grid.</summary>
    [Browsable(false)]
    public List<ItemDefData> Items { get; set; } = new();

    public override string ToString()
    {
        string label = !string.IsNullOrWhiteSpace(Name)
            ? Name
            : !string.IsNullOrWhiteSpace(Text) ? Text : "(unnamed bar)";
        return $"{BarType}: {label}";
    }
}

/// <summary>
/// A transportable, PropertyGrid-editable description of one item on a bar (or a
/// child of a popup / split button). Serialized to JSON for the round-trip.
/// </summary>
public sealed class ItemDefData
{
    [Category("CommandBars"), Description("The concrete kind of item.")]
    public ItemKindData Kind { get; set; } = ItemKindData.Button;

    [Category("CommandBars"), Description("Id of the registered command to bind at run time.")]
    public string CommandId { get; set; } = string.Empty;

    [Category("CommandBars"), Description("Caption (may contain a single '&' mnemonic marker).")]
    public string Text { get; set; } = string.Empty;

    [Category("CommandBars"), Description("Key of an icon in the manager's SvgImageList.")]
    public string ImageKey { get; set; } = string.Empty;

    [Category("CommandBars"), Description("Optional image file path, used only when ImageKey is empty.")]
    public string ImagePath { get; set; } = string.Empty;

    [Category("CommandBars"), Description("How the item shows its image versus its text.")]
    public ItemDisplayData DisplayStyle { get; set; } = ItemDisplayData.ImageAndText;

    [Category("CommandBars"), Description("Draw a group separator before this item.")]
    public bool BeginGroup { get; set; }

    [Category("CommandBars"), Description("Whether the item is shown when its bar is laid out.")]
    public bool Visible { get; set; } = true;

    [Category("CommandBars"), Description("Keyboard shortcut (System.Windows.Forms.Keys value) for the synthesized command.")]
    public int Shortcut { get; set; }

    [Category("CommandBars"), Description("Editor width, in logical pixels, for a ComboBox item.")]
    public int ComboWidth { get; set; } = 120;

    /// <summary>Child items for Popup / SplitButton kinds. Managed by the tree, not the grid.</summary>
    [Browsable(false)]
    public List<ItemDefData> Items { get; set; } = new();

    /// <summary>True when this kind can hold child items (popup or split button).</summary>
    [Browsable(false)]
    [JsonIgnore]
    public bool CanHaveChildren
        => Kind == ItemKindData.Popup || Kind == ItemKindData.SplitButton;

    public override string ToString()
    {
        string label = !string.IsNullOrWhiteSpace(Text)
            ? Text.Replace("&", string.Empty)
            : !string.IsNullOrWhiteSpace(CommandId)
                ? CommandId
                : Kind == ItemKindData.Separator ? "(separator)" : "(unnamed)";
        return $"{Kind}: {label}";
    }
}
