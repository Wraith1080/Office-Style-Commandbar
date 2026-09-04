namespace CommandBars.Model;

/// <summary>Where a <see cref="CommandBar"/> is currently placed.</summary>
public enum DockState
{
    Top,
    Left,
    Right,
    Bottom,
    Floating,
    Hidden,
}

/// <summary>The role a <see cref="CommandBar"/> plays. All three share one model.</summary>
public enum CommandBarType
{
    /// <summary>Top-level menu bar (its items are popups: File, Edit, ...).</summary>
    MenuBar,

    /// <summary>A toolbar of buttons/separators/combos.</summary>
    Toolbar,

    /// <summary>A dropdown/popup menu opened by a popup or split-button item.</summary>
    Popup,
}

/// <summary>Layout direction of a bar.</summary>
public enum BarOrientation
{
    Horizontal,
    Vertical,
}

/// <summary>How a command-backed or toolbar popup item shows its image and text.</summary>
public enum CommandItemDisplayStyle
{
    ImageOnly,
    TextOnly,
    ImageAndText,
}

/// <summary>Discriminator for the concrete kind of a <see cref="CommandBarItem"/>.</summary>
public enum CommandItemKind
{
    Button,
    ToggleButton,
    SplitButton,
    Popup,
    Separator,
    Label,
    ComboBox,
}

/// <summary>
/// Tri-state check value for toggle commands. Named to avoid a clash with
/// <c>System.Windows.Forms.CheckState</c> while the model stays UI-agnostic.
/// </summary>
public enum CommandCheckState
{
    Unchecked,
    Checked,
    Indeterminate,
}
