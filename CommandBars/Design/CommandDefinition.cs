using System.ComponentModel;
using System.Windows.Forms;
using CommandBars.Model;

namespace CommandBars.Design;

/// <summary>
/// A serializable, design-time entry in the command catalog
/// (<see cref="CommandBarManager.CommandDefinitions"/>). Authored once — id plus
/// presentation (text, icon key, shortcut, default display style) — and
/// referenced from any number of bar items by <see cref="ItemDefinition.CommandId"/>.
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

    /// <summary>Caption (may contain a single '&amp;' mnemonic marker).</summary>
    [Category("CommandBars")]
    [DefaultValue("")]
    public string Text { get; set; } = string.Empty;

    /// <summary>Key of an icon in the manager's <see cref="CommandBarManager.Images"/> list.</summary>
    [Category("CommandBars")]
    [DefaultValue("")]
    public string ImageKey { get; set; } = string.Empty;

    /// <summary>Keyboard shortcut for the command.</summary>
    [Category("CommandBars")]
    [DefaultValue(Keys.None)]
    public Keys Shortcut { get; set; } = Keys.None;

    /// <summary>
    /// The default display style items get when they are created from this
    /// command in the editor. Individual items may still override it (a toolbar
    /// button showing ImageOnly while the menu item shows ImageAndText).
    /// </summary>
    [Category("CommandBars")]
    [DefaultValue(CommandItemDisplayStyle.ImageAndText)]
    public CommandItemDisplayStyle DisplayStyle { get; set; } = CommandItemDisplayStyle.ImageAndText;

    public override string ToString()
    {
        string label = !string.IsNullOrWhiteSpace(Text)
            ? Command.RemoveMnemonic(Text)
            : !string.IsNullOrWhiteSpace(Id) ? Id : "(command)";
        return label;
    }
}
