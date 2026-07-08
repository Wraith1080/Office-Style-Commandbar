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
[TypeConverter(typeof(ExpandableObjectConverter))]
public class ItemDefinition
{
    /// <summary>The concrete kind of item this describes.</summary>
    [Category("CommandBars")]
    [DefaultValue(CommandItemKind.Button)]
    public CommandItemKind Kind { get; set; } = CommandItemKind.Button;

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
    public CommandBarItem? Build(CommandRegistry registry, SvgImageList? images = null)
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
                FillChildren(item.DropDown, registry, images);
                return item;
            }
            case CommandItemKind.ToggleButton:
            {
                var item = new CommandBarToggleButton(ResolveCommand(registry))
                {
                    DisplayStyle = DisplayStyle,
                };
                ApplyImage(item.Command, images);
                ApplyCommon(item);
                return item;
            }
            case CommandItemKind.SplitButton:
            {
                var item = new CommandBarSplitButton(ResolveCommand(registry))
                {
                    DisplayStyle = DisplayStyle,
                };
                ApplyImage(item.Command, images);
                ApplyCommon(item);
                FillChildren(item.DropDown, registry, images);
                return item;
            }
            case CommandItemKind.Button:
            {
                var item = new CommandBarButton(ResolveCommand(registry))
                {
                    DisplayStyle = DisplayStyle,
                };
                ApplyImage(item.Command, images);
                ApplyCommon(item);
                return item;
            }
            default:
                return null;
        }
    }

    // Applies the resolved image to the command when the command doesn't already
    // carry one, so pre-registered commands and definition-only items both show
    // an icon.
    private void ApplyImage(Command command, SvgImageList? images)
    {
        if (command.Image is null)
        {
            var image = ResolveImage(images);
            if (image is not null)
                command.Image = image;
        }
    }

    private void FillChildren(CommandBar dropDown, CommandRegistry registry, SvgImageList? images)
    {
        foreach (var child in Items)
        {
            var built = child.Build(registry, images);
            if (built is not null)
                dropDown.Items.Add(built);
        }
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
}

// --- Kind-specific subclasses -------------------------------------------------
// These exist so the collection editor's Add button offers a dropdown of item
// kinds (like the ToolStrip items editor) rather than a single generic entry.
// Each just seeds the appropriate default Kind; all other behavior is inherited.

/// <summary>A push-button item definition.</summary>
public sealed class ButtonDefinition : ItemDefinition
{
    public ButtonDefinition() => Kind = CommandItemKind.Button;
}

/// <summary>A checkable toggle-button item definition.</summary>
public sealed class ToggleButtonDefinition : ItemDefinition
{
    public ToggleButtonDefinition() => Kind = CommandItemKind.ToggleButton;
}

/// <summary>A split-button item definition (button plus dropdown).</summary>
public sealed class SplitButtonDefinition : ItemDefinition
{
    public SplitButtonDefinition() => Kind = CommandItemKind.SplitButton;
}

/// <summary>A submenu/popup item definition.</summary>
public sealed class PopupDefinition : ItemDefinition
{
    public PopupDefinition() => Kind = CommandItemKind.Popup;
}

/// <summary>A separator item definition.</summary>
public sealed class SeparatorDefinition : ItemDefinition
{
    public SeparatorDefinition() => Kind = CommandItemKind.Separator;
}

/// <summary>A text-label item definition.</summary>
public sealed class LabelDefinition : ItemDefinition
{
    public LabelDefinition() => Kind = CommandItemKind.Label;
}

/// <summary>A hosted combo-box item definition.</summary>
public sealed class ComboBoxDefinition : ItemDefinition
{
    public ComboBoxDefinition() => Kind = CommandItemKind.ComboBox;
}
