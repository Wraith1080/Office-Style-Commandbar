using System;
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

/// <summary>Protocol-side target used by the client command picker.</summary>
public enum CommandPlacementTargetData
{
    Toolbar,
    MenuBar,
    DropDown,
}

/// <summary>Placement compatibility shared by the protocol and client editor.</summary>
public static class CommandPlacementRulesData
{
    public static bool CanPlace(CommandKindData kind, CommandPlacementTargetData target)
        => target switch
        {
            CommandPlacementTargetData.MenuBar => kind == CommandKindData.Popup,
            CommandPlacementTargetData.Toolbar =>
                kind == CommandKindData.Action ||
                kind == CommandKindData.Toggle ||
                kind == CommandKindData.Popup ||
                kind == CommandKindData.SplitButton ||
                kind == CommandKindData.ComboBox ||
                kind == CommandKindData.Label,
            CommandPlacementTargetData.DropDown =>
                kind == CommandKindData.Action ||
                kind == CommandKindData.Toggle ||
                kind == CommandKindData.Popup ||
                kind == CommandKindData.Label,
            _ => false,
        };

    public static string GetTargetName(CommandPlacementTargetData target)
        => target switch
        {
            CommandPlacementTargetData.MenuBar => "menu-bar root",
            CommandPlacementTargetData.Toolbar => "toolbar",
            CommandPlacementTargetData.DropDown => "popup dropdown",
            _ => "command bar",
        };
}

/// <summary>
/// A transportable, PropertyGrid-editable reusable catalog entry.
/// </summary>
[TypeConverter(typeof(CommandDefDataConverter))]
public sealed class CommandDefData : ICustomTypeDescriptor
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

    [Category("CommandBars"), Description("Optional image file path used when ImageKey is empty.")]
    public string ImagePath { get; set; } = string.Empty;

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

    [Category("CommandBars"), Description("Optional primary action id for a SplitButton; empty uses this entry's id.")]
    public string PrimaryCommandId { get; set; } = string.Empty;

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

    AttributeCollection ICustomTypeDescriptor.GetAttributes()
        => TypeDescriptor.GetAttributes(GetType());

    string? ICustomTypeDescriptor.GetClassName()
        => TypeDescriptor.GetClassName(GetType());

    string? ICustomTypeDescriptor.GetComponentName() => null;

    TypeConverter ICustomTypeDescriptor.GetConverter()
        => TypeDescriptor.GetConverter(GetType());

    EventDescriptor? ICustomTypeDescriptor.GetDefaultEvent()
        => TypeDescriptor.GetDefaultEvent(GetType());

    PropertyDescriptor? ICustomTypeDescriptor.GetDefaultProperty()
        => TypeDescriptor.GetDefaultProperty(GetType());

    object? ICustomTypeDescriptor.GetEditor(Type editorBaseType)
        => TypeDescriptor.GetEditor(GetType(), editorBaseType);

    EventDescriptorCollection ICustomTypeDescriptor.GetEvents()
        => TypeDescriptor.GetEvents(GetType());

    EventDescriptorCollection ICustomTypeDescriptor.GetEvents(Attribute[]? attributes)
        => attributes is null
            ? TypeDescriptor.GetEvents(GetType())
            : TypeDescriptor.GetEvents(GetType(), attributes);

    PropertyDescriptorCollection ICustomTypeDescriptor.GetProperties()
        => CommandDefDataConverter.Filter(this, TypeDescriptor.GetProperties(GetType()));

    PropertyDescriptorCollection ICustomTypeDescriptor.GetProperties(Attribute[]? attributes)
        => CommandDefDataConverter.Filter(this, attributes is null
            ? TypeDescriptor.GetProperties(GetType())
            : TypeDescriptor.GetProperties(GetType(), attributes));

    object ICustomTypeDescriptor.GetPropertyOwner(PropertyDescriptor? pd) => this;
}

/// <summary>
/// A transportable lightweight reference inside a Popup or SplitButton catalog
/// entry. Stage 2 will use the same shape for top-level bars.
/// </summary>
public sealed class CommandPlacementData
{
    [Browsable(false)]
    [Category("CommandBars")]
    [DefaultValue(CommandPlacementKindData.Command)]
    public CommandPlacementKindData Kind { get; set; } = CommandPlacementKindData.Command;

    [ReadOnly(true)]
    [Category("CommandBars")]
    [Description("Catalog entry referenced by this placement. Change it by removing the placement and choosing another command.")]
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

/// <summary>Keeps catalog properties focused on the selected semantic kind.</summary>
internal sealed class CommandDefDataConverter : ExpandableObjectConverter
{
    public override bool GetPropertiesSupported(ITypeDescriptorContext? context) => true;

    public override PropertyDescriptorCollection GetProperties(
        ITypeDescriptorContext? context, object value, Attribute[]? attributes)
    {
        var props = TypeDescriptor.GetProperties(value, attributes);
        return value is CommandDefData command ? Filter(command, props) : props;
    }

    internal static PropertyDescriptorCollection Filter(
        CommandDefData command,
        PropertyDescriptorCollection props)
    {
        var kept = new List<PropertyDescriptor>(props.Count);
        foreach (PropertyDescriptor property in props)
        {
            if (IsRelevant(command, property.Name))
                kept.Add(property);
        }
        return new PropertyDescriptorCollection(kept.ToArray());
    }

    private static bool IsRelevant(CommandDefData command, string name)
    {
        bool executable = command.Kind == CommandKindData.Action ||
                          command.Kind == CommandKindData.Toggle ||
                          command.Kind == CommandKindData.SplitButton;
        bool dropDown = command.Kind == CommandKindData.Popup ||
                        command.Kind == CommandKindData.SplitButton;
        bool canHaveImage = command.Kind != CommandKindData.Label;

        if (name == nameof(CommandDefData.InitialChecked))
            return command.Kind == CommandKindData.Toggle;
        if (name == nameof(CommandDefData.PrimaryCommandId))
            return command.Kind == CommandKindData.SplitButton;
        if (name == nameof(CommandDefData.ContentSource))
            return command.Kind == CommandKindData.Popup;
        if (name == nameof(CommandDefData.TearOff) ||
            name == nameof(CommandDefData.PaletteColumns))
            return dropDown;
        if (name == nameof(CommandDefData.TearOffTitle))
            return dropDown && command.TearOff;
        if (name == nameof(CommandDefData.ComboWidth) ||
            name == nameof(CommandDefData.ComboItems))
            return command.Kind == CommandKindData.ComboBox;
        if (name == nameof(CommandDefData.Shortcut))
            return executable;
        if (name == nameof(CommandDefData.ImageKey) ||
            name == nameof(CommandDefData.ImagePath))
            return canHaveImage;

        return name != nameof(CommandDefData.Items);
    }
}

/// <summary>
/// The full design snapshot exchanged between the client dialog and the server:
/// the bars and the command catalog together, so the palette and the tree edit
/// as one unit and persist in a single round-trip.
/// </summary>
public sealed class DesignSnapshot
{
    public const int CurrentSchemaVersion = 2;

    /// <summary>
    /// Version of the design-time definition schema. Version 1 is the legacy
    /// full-item tree; version 2 adds canonical catalog placements.
    /// </summary>
    public int SchemaVersion { get; set; } = CurrentSchemaVersion;

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
