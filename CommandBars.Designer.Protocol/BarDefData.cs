using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing.Design;
using System.Text.Json.Serialization;
using System.Windows.Forms;

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
[TypeConverter(typeof(ItemDefDataConverter))]
public sealed class ItemDefData : ICustomTypeDescriptor
{
    [Category("CommandBars"), Description("The concrete kind of item.")]
    [RefreshProperties(RefreshProperties.All)]
    public ItemKindData Kind { get; set; } = ItemKindData.Button;

    [Category("CommandBars"), Description("Optional stable name used to find this item at run time and identify it in saved customization state.")]
    public string Name { get; set; } = string.Empty;

    [Category("CommandBars"), Description("Id of the registered command to bind at run time.")]
    public string CommandId { get; set; } = string.Empty;

    [Category("CommandBars"), Description("Caption (may contain a single '&' mnemonic marker).")]
    public string Text { get; set; } = string.Empty;

    [Category("CommandBars"), Description("Key of an icon in the manager's SvgImageList. Use the '…' to pick from the connected list.")]
    [Editor(typeof(ImageKeyEditor), typeof(UITypeEditor))]
    public string ImageKey { get; set; } = string.Empty;

    [Category("CommandBars"), Description("Optional image file path, used only when ImageKey is empty.")]
    public string ImagePath { get; set; } = string.Empty;

    [Category("CommandBars"), Description("How the item shows its image versus its text.")]
    public ItemDisplayData DisplayStyle { get; set; } = ItemDisplayData.ImageAndText;

    [Category("CommandBars"), Description("Draw a group separator before this item.")]
    public bool BeginGroup { get; set; }

    [Category("CommandBars"), Description("Office-compatible overflow priority (0-7). Priority 1 keeps the item on a docked toolbar; default 3.")]
    [DefaultValue(3)]
    public int Priority { get; set; } = 3;

    [Category("CommandBars"), Description("Include this complete item in the runtime Customize dialog's Commands list.")]
    public bool IncludeInCommandList { get; set; }

    [Category("CommandBars"), Description("For a Popup or SplitButton: show a tear-off grip so the dropdown can be dragged out into a floating palette (uses this item's Text as the palette title).")]
    [RefreshProperties(RefreshProperties.All)]
    public bool TearOff { get; set; }

    [Category("CommandBars"), Description("Optional caption for the detached palette. Empty uses this item's Text without its mnemonic.")]
    public string TearOffTitle { get; set; } = string.Empty;

    [Category("CommandBars"), Description("For a Popup or SplitButton: number of columns for an icon grid. Zero keeps the normal linear menu layout.")]
    public int PaletteColumns { get; set; }

    [Category("CommandBars"), Description("For a Popup: populate it at run time with a checked list of all available toolbars.")]
    [RefreshProperties(RefreshProperties.All)]
    public bool ToolbarList
    {
        get => _toolbarList;
        set
        {
            _toolbarList = value;
            if (value)
                _themeList = false;
        }
    }

    [Category("CommandBars"), Description("For a Popup: populate it at run time with the manager's registered themes.")]
    [RefreshProperties(RefreshProperties.All)]
    public bool ThemeList
    {
        get => _themeList;
        set
        {
            _themeList = value;
            if (value)
                _toolbarList = false;
        }
    }

    private bool _toolbarList;
    private bool _themeList;

    [Category("CommandBars"), Description("Whether the item is shown when its bar is laid out.")]
    public bool Visible { get; set; } = true;

    [Category("CommandBars"), Description("Keyboard shortcut for the command. Use None when the registered command supplies it.")]
    [DefaultValue(Keys.None)]
    public Keys Shortcut { get; set; } = Keys.None;

    [Category("CommandBars"), Description("Editor width, in logical pixels, for a ComboBox item.")]
    [DefaultValue(120)]
    public int ComboWidth { get; set; } = 120;

    [Category("CommandBars"), Description("Drop-down entries for a ComboBox item (the first is the initial selection).")]
    // The multiline string-list editor. The default List<T> collection editor
    // tries to `new string()` on Add and throws "Constructor on type
    // 'System.String' not found"; StringCollectionEditor edits the lines directly.
    [Editor("System.Windows.Forms.Design.StringCollectionEditor, System.Design, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b03f5f7f11d50a3a", typeof(UITypeEditor))]
    public List<string> ComboItems { get; set; } = new();

    /// <summary>Child items for Popup / SplitButton kinds. Managed by the tree, not the grid.</summary>
    [Browsable(false)]
    public List<ItemDefData> Items { get; set; } = new();

    /// <summary>True when this kind can hold child items (popup or split button).</summary>
    [Browsable(false)]
    [JsonIgnore]
    public bool CanHaveChildren
        => (Kind == ItemKindData.Popup && !ToolbarList && !ThemeList) || Kind == ItemKindData.SplitButton;

    public override string ToString()
    {
        string label = !string.IsNullOrWhiteSpace(Text)
            ? Text.Replace("&", string.Empty)
            : !string.IsNullOrWhiteSpace(CommandId)
                ? CommandId
                : Kind == ItemKindData.Separator ? "(separator)" : "(unnamed)";
        return $"{Kind}: {label}";
    }

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
        => ItemDefDataConverter.Filter(this, TypeDescriptor.GetProperties(GetType()));

    PropertyDescriptorCollection ICustomTypeDescriptor.GetProperties(Attribute[]? attributes)
        => ItemDefDataConverter.Filter(this, attributes is null
            ? TypeDescriptor.GetProperties(GetType())
            : TypeDescriptor.GetProperties(GetType(), attributes));

    object ICustomTypeDescriptor.GetPropertyOwner(PropertyDescriptor? pd) => this;
}

/// <summary>
/// Keeps the client-side PropertyGrid aligned with the properties the runtime
/// consumes for the selected item kind. The optional tear-off title is also
/// hidden until tear-off is enabled.
/// </summary>
internal sealed class ItemDefDataConverter : ExpandableObjectConverter
{
    public override bool GetPropertiesSupported(ITypeDescriptorContext? context) => true;

    public override PropertyDescriptorCollection GetProperties(
        ITypeDescriptorContext? context, object value, Attribute[]? attributes)
    {
        var props = TypeDescriptor.GetProperties(value, attributes);
        return value is ItemDefData def ? Filter(def, props) : props;
    }

    internal static PropertyDescriptorCollection Filter(
        ItemDefData def, PropertyDescriptorCollection props)
    {
        var kept = new List<PropertyDescriptor>(props.Count);
        foreach (PropertyDescriptor p in props)
        {
            if (IsRelevant(def, p.Name))
                kept.Add(p);
        }
        return new PropertyDescriptorCollection(kept.ToArray());
    }

    private static bool IsRelevant(ItemDefData def, string propertyName)
    {
        bool isCommand = def.Kind == ItemKindData.Button ||
                         def.Kind == ItemKindData.ToggleButton ||
                         def.Kind == ItemKindData.SplitButton;
        bool isDropDown = def.Kind == ItemKindData.Popup ||
                          def.Kind == ItemKindData.SplitButton;
        bool hasImage = isCommand || def.Kind == ItemKindData.Popup ||
                        def.Kind == ItemKindData.ComboBox;

        if (propertyName == nameof(ItemDefData.CommandId) ||
            propertyName == nameof(ItemDefData.DisplayStyle) ||
            propertyName == nameof(ItemDefData.Shortcut))
            return isCommand;

        if (propertyName == nameof(ItemDefData.Text))
            return def.Kind != ItemKindData.Separator;

        if (propertyName == nameof(ItemDefData.ImageKey) ||
            propertyName == nameof(ItemDefData.ImagePath))
            return hasImage;

        if (propertyName == nameof(ItemDefData.BeginGroup))
            return def.Kind != ItemKindData.Separator;

        if (propertyName == nameof(ItemDefData.ComboWidth) ||
            propertyName == nameof(ItemDefData.ComboItems))
            return def.Kind == ItemKindData.ComboBox;

        if (propertyName == nameof(ItemDefData.Items))
            return isDropDown && !(def.Kind == ItemKindData.Popup && (def.ToolbarList || def.ThemeList));

        if (propertyName == nameof(ItemDefData.TearOff) ||
            propertyName == nameof(ItemDefData.PaletteColumns))
            return isDropDown;

        if (propertyName == nameof(ItemDefData.ToolbarList) ||
            propertyName == nameof(ItemDefData.ThemeList))
            return def.Kind == ItemKindData.Popup;

        if (propertyName == nameof(ItemDefData.IncludeInCommandList))
            return def.Kind is ItemKindData.Button or
                ItemKindData.ToggleButton or
                ItemKindData.SplitButton or
                ItemKindData.Popup or
                ItemKindData.ComboBox;

        if (propertyName == nameof(ItemDefData.TearOffTitle))
            return isDropDown && def.TearOff;

        // Kind, Name and Visible are meaningful for every item kind. The
        // Browsable(false) CanHaveChildren helper remains filtered by metadata.
        return true;
    }
}
