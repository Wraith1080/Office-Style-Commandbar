namespace CommandBars.Design;

/// <summary>
/// The reusable semantic shape of an entry in the command catalog. The kind is
/// independent of where the entry is placed; a catalog entry owns the behavior
/// and compound structure shared by every placement.
/// </summary>
public enum CommandDefinitionKind
{
    Action,
    Toggle,
    Popup,
    SplitButton,
    ComboBox,
    Label,
}

/// <summary>The source used to populate a popup catalog entry.</summary>
public enum CommandContentSource
{
    /// <summary>Use the entry's authored <c>Items</c> collection.</summary>
    Authored,

    /// <summary>Generate the manager's checked toolbar visibility list.</summary>
    ToolbarList,

    /// <summary>Generate the manager's checked registered-theme list.</summary>
    ThemeList,
}

/// <summary>The two structural forms supported by a catalog placement.</summary>
public enum CommandPlacementKind
{
    /// <summary>A reference to another catalog entry.</summary>
    Command,

    /// <summary>An explicit separator; separators are not commands.</summary>
    Separator,
}

/// <summary>A location that accepts catalog placements.</summary>
public enum CommandPlacementTarget
{
    Toolbar,
    MenuBar,
    DropDown,
}

/// <summary>
/// Shared compatibility rules used by runtime materialization and, later, the
/// catalog-first command picker.
/// </summary>
public static class CommandPlacementRules
{
    /// <summary>Returns whether a semantic catalog kind is valid at a target.</summary>
    public static bool CanPlace(
        CommandDefinitionKind kind,
        CommandPlacementTarget target)
        => target switch
        {
            CommandPlacementTarget.MenuBar =>
                kind == CommandDefinitionKind.Popup,
            CommandPlacementTarget.Toolbar =>
                kind is CommandDefinitionKind.Action or
                    CommandDefinitionKind.Toggle or
                    CommandDefinitionKind.Popup or
                    CommandDefinitionKind.SplitButton or
                    CommandDefinitionKind.ComboBox or
                    CommandDefinitionKind.Label,
            CommandPlacementTarget.DropDown =>
                kind is CommandDefinitionKind.Action or
                    CommandDefinitionKind.Toggle or
                    CommandDefinitionKind.Popup or
                    CommandDefinitionKind.Label,
            _ => false,
        };

    /// <summary>A user-facing name for a placement target.</summary>
    public static string GetTargetName(CommandPlacementTarget target)
        => target switch
        {
            CommandPlacementTarget.MenuBar => "menu-bar root",
            CommandPlacementTarget.Toolbar => "toolbar",
            CommandPlacementTarget.DropDown => "popup dropdown",
            _ => "command bar",
        };
}
