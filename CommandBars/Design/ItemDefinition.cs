using System.ComponentModel;
using System.Drawing.Design;
using System.Windows.Forms;
using CommandBars.Imaging;
using CommandBars.Model;

namespace CommandBars.Design;

/// <summary>
/// A serializable, design-time description of one item on a bar. Because the
/// runtime items are command-backed (their <see cref="Command"/> is wired up in
/// code), the designer can't edit them directly. Definitions are the editable,
/// code-serializable stand-ins: the VS designer edits these, and at run time
/// <see cref="Build"/> turns each one into a real <see cref="CommandBarItem"/>,
/// resolving <see cref="CommandId"/> against the manager's registry.
///
/// A definition carries its own <see cref="Text"/> and <see cref="ImagePath"/>
/// so the designer can render a faithful preview even before any command exists.
/// </summary>
[TypeConverter(typeof(ItemDefinitionConverter))]
public class ItemDefinition : ICustomTypeDescriptor
{
    /// <summary>The concrete kind of item this describes.</summary>
    [Category("CommandBars")]
    [DefaultValue(CommandItemKind.Button)]
    [RefreshProperties(RefreshProperties.All)]
    public CommandItemKind Kind { get; set; } = CommandItemKind.Button;

    /// <summary>
    /// Optional stable name for the built item, so it can be located at run time
    /// via <see cref="Model.CommandBar.FindItem"/> — handy for a ComboBox whose
    /// items/selection you drive from code.
    /// </summary>
    [Category("CommandBars")]
    [DefaultValue("")]
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Id of the registered <see cref="Command"/> to bind at run time. When
    /// empty (or unresolved) a standalone command is synthesized from
    /// <see cref="Text"/>/<see cref="ImagePath"/>/<see cref="Shortcut"/>.
    /// </summary>
    [Category("CommandBars")]
    [DefaultValue("")]
    public string CommandId { get; set; } = string.Empty;

    /// <summary>Caption (may contain a single '&amp;' mnemonic marker).</summary>
    [Category("CommandBars")]
    [DefaultValue("")]
    public string Text { get; set; } = string.Empty;

    /// <summary>
    /// Key of an icon in the manager's <see cref="CommandBarManager.Images"/>
    /// list (the recommended way to assign an image — the SVG travels in the
    /// designer file, nothing to resolve). Takes precedence over
    /// <see cref="ImagePath"/>.
    /// </summary>
    [Category("CommandBars")]
    [DefaultValue("")]
    public string ImageKey { get; set; } = string.Empty;

    /// <summary>
    /// Optional path to an image file, used only when <see cref="ImageKey"/> is
    /// empty or unresolved. Prefer <see cref="ImageKey"/> with a
    /// <see cref="SvgImageList"/> so icons stay embedded and portable.
    /// </summary>
    [Category("CommandBars")]
    [DefaultValue("")]
    [Editor(
        "System.Windows.Forms.Design.FileNameEditor, System.Design, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b03f5f7f11d50a3a",
        "System.Drawing.Design.UITypeEditor, System.Drawing, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b03f5f7f11d50a3a")]
    public string ImagePath { get; set; } = string.Empty;

    /// <summary>How the item shows its image versus its text.</summary>
    [Category("CommandBars")]
    [DefaultValue(CommandItemDisplayStyle.ImageAndText)]
    public CommandItemDisplayStyle DisplayStyle { get; set; } = CommandItemDisplayStyle.ImageAndText;

    /// <summary>Draw a group separator before this item.</summary>
    [Category("CommandBars")]
    [DefaultValue(false)]
    public bool BeginGroup { get; set; }

    /// <summary>
    /// For a <see cref="CommandItemKind.Popup"/> or
    /// <see cref="CommandItemKind.SplitButton"/>: offer a "tear-off" grip so the
    /// user can drag the dropdown out into a standalone floating palette (Office's
    /// tear-off toolbars). The palette's title is this item's <see cref="Text"/>.
    /// Ignored for other kinds.
    /// </summary>
    [Category("CommandBars")]
    [DefaultValue(false)]
    [RefreshProperties(RefreshProperties.All)]
    public bool TearOff { get; set; }

    /// <summary>
    /// Optional caption for the detached palette. When empty, the item's
    /// <see cref="Text"/> (with its mnemonic removed) is used.
    /// </summary>
    [Category("CommandBars")]
    [DefaultValue("")]
    public string TearOffTitle { get; set; } = string.Empty;

    /// <summary>
    /// For a Popup or SplitButton dropdown, lays out icon-only items as a grid
    /// with this many columns. Zero keeps the normal linear menu layout. Text
    /// items, separators, and nested popups remain full-width rows.
    /// </summary>
    [Category("CommandBars")]
    [DefaultValue(0)]
    public int PaletteColumns { get; set; }

    /// <summary>Whether the item is shown when its bar is laid out.</summary>
    [Category("CommandBars")]
    [DefaultValue(true)]
    public bool Visible { get; set; } = true;

    /// <summary>Keyboard shortcut for the synthesized command.</summary>
    [Category("CommandBars")]
    [DefaultValue(Keys.None)]
    public Keys Shortcut { get; set; } = Keys.None;

    /// <summary>Editor width, in logical pixels, for a <see cref="CommandItemKind.ComboBox"/>.</summary>
    [Category("CommandBars")]
    [DefaultValue(120)]
    public int ComboWidth { get; set; } = 120;

    /// <summary>
    /// Drop-down entries for a <see cref="CommandItemKind.ComboBox"/>, realized
    /// into the combo's item list at build (the first becomes the initial
    /// selection). Edit them in the designer; add or read more from code via the
    /// live <see cref="Model.CommandBarComboBox"/>.
    /// </summary>
    [Category("CommandBars")]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Content)]
    public List<string> ComboItems { get; } = new();

    /// <summary>
    /// Child items, used when <see cref="Kind"/> is
    /// <see cref="CommandItemKind.Popup"/> or
    /// <see cref="CommandItemKind.SplitButton"/> (the submenu contents).
    /// </summary>
    [Category("CommandBars")]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Content)]
    [Editor(typeof(ItemDefinitionCollectionEditor), typeof(UITypeEditor))]
    public List<ItemDefinition> Items { get; } = new();

    /// <summary>Resolves or synthesizes the backing command for this item.</summary>
    private Command ResolveCommand(CommandRegistry registry)
    {
        if (!string.IsNullOrWhiteSpace(CommandId) && registry.TryGet(CommandId, out var existing))
            return existing;

        string id = string.IsNullOrWhiteSpace(CommandId)
            ? "def_" + Guid.NewGuid().ToString("N")
            : CommandId;

        var command = new Command(id)
        {
            Text = Text,
            Shortcut = Shortcut,
        };
        var image = DesignImage.Load(ImagePath);
        if (image is not null)
            command.Image = image;

        // Publish a named command so sibling items can share it; leave anonymous
        // (blank-id) commands out of the registry.
        if (!string.IsNullOrWhiteSpace(CommandId) && !registry.Contains(id))
            registry.Register(command);

        return command;
    }

    private void ApplyCommon(CommandBarItem item)
    {
        item.Visible = Visible;
        item.BeginGroup = BeginGroup;
        if (!string.IsNullOrWhiteSpace(Name))
            item.Name = Name;
    }

    /// <summary>
    /// Resolves the item's image: from the image list by <see cref="ImageKey"/>
    /// first, then from <see cref="ImagePath"/> as a fallback. Null if neither
    /// yields an image.
    /// </summary>
    private IImageSource? ResolveImage(SvgImageList? images)
    {
        if (!string.IsNullOrWhiteSpace(ImageKey) && images is not null)
        {
            var fromList = images.Get(ImageKey);
            if (fromList is not null)
                return fromList;
        }
        if (!string.IsNullOrWhiteSpace(ImagePath))
            return DesignImage.Load(ImagePath);
        return null;
    }

    /// <summary>
    /// Realizes this definition into a live <see cref="CommandBarItem"/>. Returns
    /// null only if the kind is unknown. Safe to call at design time (for the
    /// preview) and at run time (via <see cref="CommandBarManager.BuildFromDefinitions"/>).
    /// <paramref name="images"/> supplies icons referenced by <see cref="ImageKey"/>.
    /// </summary>
    public CommandBarItem? Build(CommandRegistry registry, SvgImageList? images = null, bool designPreview = false)
    {
        switch (Kind)
        {
            case CommandItemKind.Separator:
            {
                var item = new CommandBarSeparator();
                ApplyCommon(item);
                return item;
            }
            case CommandItemKind.Label:
            {
                var item = new CommandBarLabel(Text);
                ApplyCommon(item);
                return item;
            }
            case CommandItemKind.ComboBox:
            {
                var item = new CommandBarComboBox { Width = ComboWidth };
                foreach (var entry in ComboItems)
                    item.Items.Add(entry);
                if (item.Items.Count > 0)
                    item.SelectedItem = item.Items[0];
                // Give the combo its icon (shown when it collapses to a drop-down
                // button on a vertically-docked bar) and a label from Text, so a
                // designer-authored combo matches one built from code.
                item.Image = ResolveImage(images);
                item.Label = string.IsNullOrWhiteSpace(Text) ? null : Command.RemoveMnemonic(Text);
                ApplyCommon(item);
                return item;
            }
            case CommandItemKind.Popup:
            {
                var item = new CommandBarPopupItem(Text)
                {
                    Image = ResolveImage(images),
                };
                ApplyCommon(item);
                FillChildren(item.DropDown, registry, images, designPreview);
                ApplyTearOff(item.DropDown);
                return item;
            }
            case CommandItemKind.ToggleButton:
            {
                var item = new CommandBarToggleButton(ResolveCommand(registry))
                {
                    DisplayStyle = DisplayStyle,
                };
                ApplyImage(item.Command, images, designPreview);
                ApplyCommon(item);
                return item;
            }
            case CommandItemKind.SplitButton:
            {
                var item = new CommandBarSplitButton(ResolveCommand(registry))
                {
                    DisplayStyle = DisplayStyle,
                };
                ApplyImage(item.Command, images, designPreview);
                ApplyCommon(item);
                FillChildren(item.DropDown, registry, images, designPreview);
                ApplyTearOff(item.DropDown);
                return item;
            }
            case CommandItemKind.Button:
            {
                var item = new CommandBarButton(ResolveCommand(registry))
                {
                    DisplayStyle = DisplayStyle,
                };
                ApplyImage(item.Command, images, designPreview);
                ApplyCommon(item);
                return item;
            }
            default:
                return null;
        }
    }

    // Applies the resolved image to the command. At run time this is
    // non-destructive (only fills an icon the command doesn't already carry), so
    // code-set images win. At design time (<paramref name="designPreview"/>) the
    // definition's ImageKey/ImagePath always wins, so re-picking an icon in the
    // designer immediately refreshes the preview.
    private void ApplyImage(Command command, SvgImageList? images, bool designPreview)
    {
        if (designPreview || command.Image is null)
        {
            var image = ResolveImage(images);
            if (image is not null)
                command.Image = image;
        }
    }

    private void FillChildren(CommandBar dropDown, CommandRegistry registry, SvgImageList? images, bool designPreview)
    {
        foreach (var child in Items)
        {
            var built = child.Build(registry, images, designPreview);
            if (built is not null)
                dropDown.Items.Add(built);
        }
    }

    // Applies popup/split dropdown presentation options. The title override is
    // optional; otherwise the item's mnemonic-stripped caption is used.
    private void ApplyTearOff(CommandBar dropDown)
    {
        dropDown.AllowTearOff = TearOff;
        dropDown.PaletteColumns = Math.Max(0, PaletteColumns);

        string title = !string.IsNullOrWhiteSpace(TearOffTitle)
            ? TearOffTitle
            : Command.RemoveMnemonic(Text);
        if (TearOff && !string.IsNullOrWhiteSpace(title))
            dropDown.Text = title;
    }

    public override string ToString()
    {
        string label = !string.IsNullOrWhiteSpace(Text)
            ? Command.RemoveMnemonic(Text)
            : !string.IsNullOrWhiteSpace(CommandId)
                ? CommandId
                : Kind == CommandItemKind.Separator ? "(separator)" : "(unnamed)";
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
        => ItemDefinitionConverter.Filter(this, TypeDescriptor.GetProperties(GetType()));

    PropertyDescriptorCollection ICustomTypeDescriptor.GetProperties(Attribute[]? attributes)
        => ItemDefinitionConverter.Filter(this, attributes is null
            ? TypeDescriptor.GetProperties(GetType())
            : TypeDescriptor.GetProperties(GetType(), attributes));

    object ICustomTypeDescriptor.GetPropertyOwner(PropertyDescriptor? pd) => this;
}

/// <summary>
/// Expandable converter that keeps the PropertyGrid aligned with what
/// <see cref="ItemDefinition.Build"/> actually consumes for the selected kind.
/// The optional tear-off title is also hidden until tear-off is enabled.
/// </summary>
internal sealed class ItemDefinitionConverter : ExpandableObjectConverter
{
    public override bool GetPropertiesSupported(ITypeDescriptorContext? context) => true;

    public override PropertyDescriptorCollection GetProperties(
        ITypeDescriptorContext? context, object value, Attribute[]? attributes)
    {
        var props = TypeDescriptor.GetProperties(value, attributes);
        return value is ItemDefinition def ? Filter(def, props) : props;
    }

    internal static PropertyDescriptorCollection Filter(
        ItemDefinition def, PropertyDescriptorCollection props)
    {
        var kept = new List<PropertyDescriptor>(props.Count);
        foreach (PropertyDescriptor p in props)
        {
            if (IsRelevant(def, p.Name))
                kept.Add(p);
        }
        return new PropertyDescriptorCollection(kept.ToArray());
    }

    private static bool IsRelevant(ItemDefinition def, string propertyName)
    {
        bool isCommand = def.Kind == CommandItemKind.Button ||
                         def.Kind == CommandItemKind.ToggleButton ||
                         def.Kind == CommandItemKind.SplitButton;
        bool isDropDown = def.Kind == CommandItemKind.Popup ||
                          def.Kind == CommandItemKind.SplitButton;
        bool hasImage = isCommand || def.Kind == CommandItemKind.Popup ||
                        def.Kind == CommandItemKind.ComboBox;

        if (propertyName == nameof(ItemDefinition.CommandId) ||
            propertyName == nameof(ItemDefinition.DisplayStyle) ||
            propertyName == nameof(ItemDefinition.Shortcut))
            return isCommand;

        if (propertyName == nameof(ItemDefinition.Text))
            return def.Kind != CommandItemKind.Separator;

        if (propertyName == nameof(ItemDefinition.ImageKey) ||
            propertyName == nameof(ItemDefinition.ImagePath))
            return hasImage;

        if (propertyName == nameof(ItemDefinition.BeginGroup))
            return def.Kind != CommandItemKind.Separator;

        if (propertyName == nameof(ItemDefinition.ComboWidth) ||
            propertyName == nameof(ItemDefinition.ComboItems))
            return def.Kind == CommandItemKind.ComboBox;

        if (propertyName == nameof(ItemDefinition.Items))
            return isDropDown;

        if (propertyName == nameof(ItemDefinition.TearOff) ||
            propertyName == nameof(ItemDefinition.PaletteColumns))
            return isDropDown;

        if (propertyName == nameof(ItemDefinition.TearOffTitle))
            return isDropDown && def.TearOff;

        // Kind, Name and Visible are meaningful for every item kind.
        return true;
    }
}

// --- Kind-specific subclasses -------------------------------------------------
// These exist so the collection editor's Add button offers a dropdown of item
// kinds (like the ToolStrip items editor) rather than a single generic entry.
// Each just seeds the appropriate default Kind; all other behavior is inherited.

/// <summary>A push-button item definition.</summary>
[TypeConverter(typeof(ItemDefinitionConverter))]
public sealed class ButtonDefinition : ItemDefinition
{
    public ButtonDefinition() => Kind = CommandItemKind.Button;
}

/// <summary>A checkable toggle-button item definition.</summary>
[TypeConverter(typeof(ItemDefinitionConverter))]
public sealed class ToggleButtonDefinition : ItemDefinition
{
    public ToggleButtonDefinition() => Kind = CommandItemKind.ToggleButton;
}

/// <summary>A split-button item definition (button plus dropdown).</summary>
[TypeConverter(typeof(ItemDefinitionConverter))]
public sealed class SplitButtonDefinition : ItemDefinition
{
    public SplitButtonDefinition() => Kind = CommandItemKind.SplitButton;
}

/// <summary>A submenu/popup item definition.</summary>
[TypeConverter(typeof(ItemDefinitionConverter))]
public sealed class PopupDefinition : ItemDefinition
{
    public PopupDefinition() => Kind = CommandItemKind.Popup;
}

/// <summary>A separator item definition.</summary>
[TypeConverter(typeof(ItemDefinitionConverter))]
public sealed class SeparatorDefinition : ItemDefinition
{
    public SeparatorDefinition() => Kind = CommandItemKind.Separator;
}

/// <summary>A text-label item definition.</summary>
[TypeConverter(typeof(ItemDefinitionConverter))]
public sealed class LabelDefinition : ItemDefinition
{
    public LabelDefinition() => Kind = CommandItemKind.Label;
}

/// <summary>A hosted combo-box item definition.</summary>
[TypeConverter(typeof(ItemDefinitionConverter))]
public sealed class ComboBoxDefinition : ItemDefinition
{
    public ComboBoxDefinition() => Kind = CommandItemKind.ComboBox;
}
