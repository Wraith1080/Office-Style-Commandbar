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
