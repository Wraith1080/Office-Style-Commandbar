namespace CommandBars.Persistence;

/// <summary>
/// Serializable snapshot of the full bar layout, including structure: bars
/// (with their type, dock, and properties) and their complete item trees
/// (buttons, toggles, split buttons, popups/submenus, separators, labels).
/// On load the layout is rebuilt from this state, so structural customizations
/// — added/removed buttons, reordering, new/renamed/deleted toolbars and menu
/// edits — round-trip. Items reference commands by id; ids that no longer exist
/// in the registry are skipped, so layouts degrade gracefully across versions.
/// </summary>
public sealed class LayoutState
{
    /// <summary>Schema version; bump when the shape changes to allow migration.</summary>
    public int Version { get; set; } = 2;

    public List<BarState> Bars { get; set; } = new();

    /// <summary>Whether tooltips are shown on toolbar items.</summary>
    public bool ShowToolTips { get; set; } = true;

    /// <summary>App-level settings persisted alongside the layout (e.g. theme).</summary>
    public Dictionary<string, string> Settings { get; set; } = new();

    /// <summary>Open tear-off palettes (a dropdown floated into a standalone window).</summary>
    public List<TearOffState> TearOffs { get; set; } = new();
}

/// <summary>Persisted state for one open tear-off palette.</summary>
public sealed class TearOffState
{
    /// <summary>The torn-off dropdown bar's stable Name (e.g. "popup:F&amp;ormat", "split:file.new").</summary>
    public string BarName { get; set; } = string.Empty;

    /// <summary>Screen position of the palette's top-left corner.</summary>
    public int X { get; set; }
    public int Y { get; set; }
}

/// <summary>Persisted state for a single bar.</summary>
public sealed class BarState
{
    public string Name { get; set; } = string.Empty;
    public string Text { get; set; } = string.Empty;
    public string BarType { get; set; } = "Toolbar";
    public string Dock { get; set; } = "Top";
    public bool Visible { get; set; } = true;
    public int IconSize { get; set; }
    public int Row { get; set; }
    public int Offset { get; set; }
    public int FloatX { get; set; }
    public int FloatY { get; set; }
    public bool AllowFloat { get; set; } = true;
    public bool AllowCustomize { get; set; } = true;
    public bool Locked { get; set; }
    public List<ItemState> Items { get; set; } = new();
}

/// <summary>Persisted state for a single item, recursive for popups/submenus.</summary>
public sealed class ItemState
{
    /// <summary>The <c>CommandItemKind</c> name (Button, ToggleButton, Popup, ...).</summary>
    public string Kind { get; set; } = "Button";

    /// <summary>Optional item name, used to find hosted items (e.g. a combo) from code.</summary>
    public string? Name { get; set; }

    /// <summary>Command id for command-backed items (button/toggle/split).</summary>
    public string? CommandId { get; set; }

    /// <summary>Caption for popups and labels.</summary>
    public string? Text { get; set; }

    /// <summary>Stable dropdown key for popups/splits, used to locate a menu for Reset.</summary>
    public string? Key { get; set; }

    /// <summary>The <c>CommandItemDisplayStyle</c> name for command items.</summary>
    public string? DisplayStyle { get; set; }

    public bool BeginGroup { get; set; }
    public bool Visible { get; set; } = true;

    /// <summary>Editor width, in logical pixels, for a ComboBox item.</summary>
    public int ComboWidth { get; set; } = 120;

    /// <summary>Drop-down entries for a ComboBox item (the first is the initial selection).</summary>
    public List<string> ComboItems { get; set; } = new();

    /// <summary>Child items for popups and split-button dropdowns.</summary>
    public List<ItemState> Children { get; set; } = new();
}
