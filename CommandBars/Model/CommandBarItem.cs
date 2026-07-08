using System.ComponentModel;
using System.Drawing;

namespace CommandBars.Model;

/// <summary>
/// Base class for everything that appears on a <see cref="CommandBar"/>:
/// buttons, toggles, split buttons, popups, separators, labels and combos.
/// </summary>
public abstract class CommandBarItem
{
    /// <summary>The bar this item currently belongs to, or null if unparented.</summary>
    [Browsable(false)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public CommandBar? OwnerBar { get; internal set; }

    /// <summary>Whether the item is shown when its bar is laid out.</summary>
    [Category("CommandBars")]
    [DefaultValue(true)]
    public bool Visible { get; set; } = true;

    /// <summary>
    /// When true a group separator is drawn before this item (Office's
    /// "begin a group" flag). Independent of explicit separator items.
    /// </summary>
    [Category("CommandBars")]
    [DefaultValue(false)]
    public bool BeginGroup { get; set; }

    /// <summary>Optional name for lookup and persistence within a bar.</summary>
    [Category("CommandBars")]
    [DefaultValue(null)]
    public string? Name { get; set; }

    /// <summary>Free-form data slot for consumers.</summary>
    [Browsable(false)]
    public object? Tag { get; set; }

    /// <summary>
    /// Layout rectangle assigned by the layout engine (populated from the
    /// rendering phase onward; empty until then).
    /// </summary>
    [Browsable(false)]
    public Rectangle Bounds { get; internal set; }

    /// <summary>The concrete kind of this item.</summary>
    public abstract CommandItemKind Kind { get; }
}

/// <summary>
/// Base for items backed by a <see cref="Command"/> (buttons, toggles, split
/// buttons). Text and enabled state are read straight from the command so
/// every item sharing it stays consistent.
/// </summary>
public abstract class CommandBarCommandItem : CommandBarItem
{
    protected CommandBarCommandItem(Command command)
    {
        Command = command ?? throw new ArgumentNullException(nameof(command));
    }

    /// <summary>The backing command.</summary>
    public Command Command { get; }

    /// <summary>How this item renders image vs. text.</summary>
    public CommandItemDisplayStyle DisplayStyle { get; set; } = CommandItemDisplayStyle.ImageAndText;

    /// <summary>Caption (with mnemonic) from the command.</summary>
    public string Text => Command.Text;

    /// <summary>Caption without the mnemonic marker.</summary>
    public string DisplayText => Command.DisplayText;

    /// <summary>Enabled state from the command.</summary>
    public bool Enabled => Command.Enabled;
}
