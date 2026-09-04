using System.ComponentModel;
using System.Windows.Forms;
using CommandBars.Model;

namespace CommandBars.Design;

/// <summary>
/// A serializable, reusable entry in the command catalog
/// (<see cref="CommandBarManager.CommandDefinitions"/>). Atomic entries own an
/// action's presentation; compound entries additionally own a dropdown tree or
/// hosted-control configuration. Bars and dropdowns refer to the stable
/// <see cref="Id"/> instead of copying that information.
///
/// This is the design-time twin of a runtime <see cref="Command"/>: the manager
/// registers each entry's presentation into the shared registry so referenced
/// items inherit it, while the command's behavior (its <c>ExecuteHandler</c>) is
/// still wired in code by id.
/// </summary>
[TypeConverter(typeof(ExpandableObjectConverter))]
public class CommandDefinition
{
    /// <summary>Stable command id that items reference via their CommandId.</summary>
    [Category("CommandBars")]
    [DefaultValue("")]
    public string Id { get; set; } = string.Empty;

    /// <summary>The reusable semantic shape of this catalog entry.</summary>
    [Category("CommandBars")]
    [DefaultValue(CommandDefinitionKind.Action)]
    [RefreshProperties(RefreshProperties.All)]
    public CommandDefinitionKind Kind { get; set; } = CommandDefinitionKind.Action;

    /// <summary>Caption (may contain a single '&amp;' mnemonic marker).</summary>
    [Category("CommandBars")]
    [DefaultValue("")]
    public string Text { get; set; } = string.Empty;

    /// <summary>Key of an icon in the manager's <see cref="CommandBarManager.Images"/> list.</summary>
    [Category("CommandBars")]
    [DefaultValue("")]
    public string ImageKey { get; set; } = string.Empty;

    /// <summary>
    /// Optional image file path used when <see cref="ImageKey"/> is empty or
    /// unresolved. Retained for migration compatibility; embedded image keys
    /// remain preferred.
    /// </summary>
    [Category("CommandBars")]
    [DefaultValue("")]
    public string ImagePath { get; set; } = string.Empty;

    /// <summary>Keyboard shortcut for the command.</summary>
    [Category("CommandBars")]
    [DefaultValue(Keys.None)]
    public Keys Shortcut { get; set; } = Keys.None;

    /// <summary>Optional ScreenTip. Empty falls back to the command caption.</summary>
    [Category("CommandBars")]
    [DefaultValue("")]
    public string ToolTip { get; set; } = string.Empty;

    /// <summary>
    /// The default display style items get when they are created from this
    /// command in the editor. Individual items may still override it (a toolbar
    /// button showing ImageOnly while the menu item shows ImageAndText).
    /// </summary>
    [Category("CommandBars")]
    [DefaultValue(CommandItemDisplayStyle.ImageAndText)]
    public CommandItemDisplayStyle DisplayStyle { get; set; } = CommandItemDisplayStyle.ImageAndText;

    /// <summary>Initial state for a <see cref="CommandDefinitionKind.Toggle"/>.</summary>
    [Category("CommandBars")]
    [DefaultValue(CommandCheckState.Unchecked)]
    public CommandCheckState InitialChecked { get; set; } = CommandCheckState.Unchecked;

    /// <summary>
    /// Optional primary action id for a SplitButton. Empty makes this entry's
    /// own <see cref="Id"/> the executable command. A separate id lets one
    /// action remain a normal menu/button command while a reusable split-button
    /// composition invokes that same action from its primary region.
    /// </summary>
    [Category("CommandBars")]
    [DefaultValue("")]
    public string PrimaryCommandId { get; set; } = string.Empty;

    /// <summary>
    /// The source of a Popup's contents. Split buttons always use authored
    /// children because their dropdown is part of the compound entry.
    /// </summary>
    [Category("CommandBars")]
    [DefaultValue(CommandContentSource.Authored)]
    [RefreshProperties(RefreshProperties.All)]
    public CommandContentSource ContentSource { get; set; } = CommandContentSource.Authored;

    /// <summary>Show a tear-off grip on a Popup or SplitButton dropdown.</summary>
    [Category("CommandBars")]
    [DefaultValue(false)]
    [RefreshProperties(RefreshProperties.All)]
    public bool TearOff { get; set; }

    /// <summary>Optional detached-palette caption; empty uses <see cref="Text"/>.</summary>
    [Category("CommandBars")]
    [DefaultValue("")]
    public string TearOffTitle { get; set; } = string.Empty;

    /// <summary>Column count for an icon-grid dropdown; zero uses a normal list.</summary>
    [Category("CommandBars")]
    [DefaultValue(0)]
    public int PaletteColumns { get; set; }

    /// <summary>Preferred logical width of a ComboBox catalog entry.</summary>
    [Category("CommandBars")]
    [DefaultValue(120)]
    public int ComboWidth { get; set; } = 120;

    /// <summary>Initial entries for a ComboBox catalog entry.</summary>
    [Category("CommandBars")]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Content)]
    public List<string> ComboItems { get; } = new();

    /// <summary>
    /// Ordered catalog references in a Popup or SplitButton dropdown. These are
    /// lightweight placements; separators are represented structurally rather
    /// than as commands.
    /// </summary>
    [Category("CommandBars")]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Content)]
    public List<CommandPlacementDefinition> Items { get; } = new();

    /// <summary>Offer this complete reusable entry in the runtime Customize palette.</summary>
    [Category("CommandBars")]
    [DefaultValue(false)]
    public bool IncludeInCommandList { get; set; }

    public override string ToString()
    {
        string label = !string.IsNullOrWhiteSpace(Text)
            ? Command.RemoveMnemonic(Text)
            : !string.IsNullOrWhiteSpace(Id) ? Id : "(command)";
        return Kind == CommandDefinitionKind.Action ? label : $"{Kind}: {label}";
    }
}
