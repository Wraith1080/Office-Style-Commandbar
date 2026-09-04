using System.ComponentModel;
using CommandBars.Model;

namespace CommandBars.Design;

/// <summary>
/// A lightweight occurrence of a catalog entry inside a bar or a compound
/// Popup/SplitButton definition. The placement references reusable catalog
/// presentation instead of copying it into every occurrence.
/// </summary>
[TypeConverter(typeof(ExpandableObjectConverter))]
public sealed class CommandPlacementDefinition
{
    /// <summary>Whether this placement references a command or is a separator.</summary>
    [Browsable(false)]
    [Category("CommandBars")]
    [DefaultValue(CommandPlacementKind.Command)]
    public CommandPlacementKind Kind { get; set; } = CommandPlacementKind.Command;

    /// <summary>The stable id of the referenced catalog entry.</summary>
    [Category("CommandBars")]
    [DefaultValue("")]
    // Must remain writable to TypeDescriptor: the WinForms designer replays
    // generated CommandId assignments through its serializer and skips
    // properties marked ReadOnly. The catalog-first client descriptor keeps
    // this identity read-only in the actual editor UI.
    public string CommandId { get; set; } = string.Empty;

    /// <summary>Optional stable name for locating this particular occurrence.</summary>
    [Category("CommandBars")]
    [DefaultValue("")]
    public string Name { get; set; } = string.Empty;

    /// <summary>Whether the occurrence is shown.</summary>
    [Category("CommandBars")]
    [DefaultValue(true)]
    public bool Visible { get; set; } = true;

    /// <summary>Draw a group boundary immediately before this occurrence.</summary>
    [Category("CommandBars")]
    [DefaultValue(false)]
    public bool BeginGroup { get; set; }

    /// <summary>Office-compatible overflow priority (0-7; 1 stays visible).</summary>
    [Category("CommandBars")]
    [DefaultValue(3)]
    public int Priority
    {
        get => _priority;
        set
        {
            if (value is < 0 or > 7)
                throw new ArgumentOutOfRangeException(nameof(value), value,
                    "Priority must be between 0 and 7.");
            _priority = value;
        }
    }
    private int _priority = 3;

    /// <summary>
    /// True to use the catalog entry's default display style. Set false to use
    /// this placement's <see cref="DisplayStyle"/> instead.
    /// </summary>
    [Category("CommandBars")]
    [DefaultValue(true)]
    public bool UseCatalogDisplayStyle { get; set; } = true;

    /// <summary>Optional display-style override for command-backed items.</summary>
    [Category("CommandBars")]
    [DefaultValue(CommandItemDisplayStyle.ImageAndText)]
    public CommandItemDisplayStyle DisplayStyle { get; set; } = CommandItemDisplayStyle.ImageAndText;

    public override string ToString()
        => Kind == CommandPlacementKind.Separator
            ? "Separator"
            : string.IsNullOrWhiteSpace(CommandId) ? "(missing command)" : CommandId;
}
