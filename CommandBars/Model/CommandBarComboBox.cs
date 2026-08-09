using CommandBars.Imaging;

namespace CommandBars.Model;

/// <summary>
/// A combo box hosted on a bar. Phase 1 models the data only (items, selection,
/// width); the hosted WinForms editor is wired up in the rendering phase.
/// </summary>
public sealed class CommandBarComboBox : CommandBarItem
{
    private object? _selectedItem;
    private bool _enabled = true;

    public override CommandItemKind Kind => CommandItemKind.ComboBox;

    /// <summary>The list of selectable values.</summary>
    public IList<object> Items { get; } = new List<object>();

    /// <summary>Preferred width of the editor in logical pixels.</summary>
    public int Width { get; set; } = 120;

    /// <summary>
    /// Optional icon used when the combo collapses to a drop-down button — the
    /// Office behaviour on a vertically-docked toolbar (and in the overflow
    /// flyout), where a full editable field cannot fit. Ignored while the combo
    /// is laid out inline on a horizontal bar. When neither <see cref="Image"/>
    /// nor <see cref="Label"/> is set the button falls back to the current
    /// selection text.
    /// </summary>
    public IImageSource? Image { get; set; }

    /// <summary>
    /// Optional short caption for the collapsed drop-down button (see
    /// <see cref="Image"/>). Shown under the icon, or on its own when there is
    /// no icon. Ignored while the combo is laid out inline on a horizontal bar.
    /// </summary>
    public string? Label { get; set; }

    /// <summary>Raised when <see cref="SelectedItem"/> changes.</summary>
    public event EventHandler? SelectedItemChanged;

    /// <summary>Raised when <see cref="Enabled"/> changes.</summary>
    public event EventHandler? EnabledChanged;

    /// <summary>Whether the combo accepts input. Named copies share this state.</summary>
    public bool Enabled
    {
        get => _enabled;
        set
        {
            if (_enabled == value)
                return;
            _enabled = value;
            EnabledChanged?.Invoke(this, EventArgs.Empty);
        }
    }

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
