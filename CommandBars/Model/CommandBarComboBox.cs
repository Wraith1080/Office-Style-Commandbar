namespace CommandBars.Model;

/// <summary>
/// A combo box hosted on a bar. Phase 1 models the data only (items, selection,
/// width); the hosted WinForms editor is wired up in the rendering phase.
/// </summary>
public sealed class CommandBarComboBox : CommandBarItem
{
    private object? _selectedItem;

    public override CommandItemKind Kind => CommandItemKind.ComboBox;

    /// <summary>The list of selectable values.</summary>
    public IList<object> Items { get; } = new List<object>();

    /// <summary>Preferred width of the editor in logical pixels.</summary>
    public int Width { get; set; } = 120;

    /// <summary>Raised when <see cref="SelectedItem"/> changes.</summary>
    public event EventHandler? SelectedItemChanged;

    /// <summary>The currently selected value, or null.</summary>
    public object? SelectedItem
    {
        get => _selectedItem;
        set
        {
            if (Equals(_selectedItem, value))
                return;
            _selectedItem = value;
            SelectedItemChanged?.Invoke(this, EventArgs.Empty);
        }
    }
}
