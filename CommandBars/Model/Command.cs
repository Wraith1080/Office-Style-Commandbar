using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Text;
using System.Windows.Forms;
using CommandBars.Imaging;

namespace CommandBars.Model;

/// <summary>
/// A command is the data/action behind one or more on-screen items. The same
/// command can appear on several bars (a menu item and a toolbar button) and
/// they stay in sync through <see cref="INotifyPropertyChanged"/>.
/// </summary>
public class Command : INotifyPropertyChanged
{
    private string _text = string.Empty;
    private IImageSource? _image;
    private Keys _shortcut = Keys.None;
    private bool _enabled = true;
    private CommandCheckState _checked = CommandCheckState.Unchecked;
    private string? _toolTip;

    /// <param name="id">Stable, non-empty identity used for persistence.</param>
    public Command(string id)
    {
        if (string.IsNullOrWhiteSpace(id))
            throw new ArgumentException("Command id must be a non-empty string.", nameof(id));
        Id = id;
    }

    /// <summary>Stable identity. Immutable; used as the persistence key.</summary>
    public string Id { get; }

    /// <summary>Caption, may contain a single '&amp;' mnemonic marker.</summary>
    public string Text
    {
        get => _text;
        set => SetField(ref _text, value ?? string.Empty);
    }

    /// <summary>Caption with the mnemonic marker removed, for measuring/tooltips.</summary>
    [Browsable(false)]
    public string DisplayText => RemoveMnemonic(_text);

    /// <summary>Vector or raster image source. Null shows no image.</summary>
    [Browsable(false)]
    public IImageSource? Image
    {
        get => _image;
        set => SetField(ref _image, value);
    }

    /// <summary>Keyboard shortcut (WinForms <see cref="Keys"/>).</summary>
    public Keys Shortcut
    {
        get => _shortcut;
        set => SetField(ref _shortcut, value);
    }

    /// <summary>Whether the command can currently be invoked.</summary>
    [Category("Command")]
    [DefaultValue(true)]
    public bool Enabled
    {
        get => _enabled;
        set => SetField(ref _enabled, value);
    }

    /// <summary>Check state for toggle commands.</summary>
    public CommandCheckState Checked
    {
        get => _checked;
        set => SetField(ref _checked, value);
    }

    /// <summary>Tooltip text; falls back to <see cref="DisplayText"/> when null.</summary>
    public string? ToolTip
    {
        get => _toolTip;
        set => SetField(ref _toolTip, value);
    }

    /// <summary>Free-form data slot for consumers.</summary>
    [Browsable(false)]
    public object? Tag { get; set; }

    /// <summary>
    /// When true, <see cref="Perform"/> toggles <see cref="Checked"/> between
    /// Checked and Unchecked before invoking the handler. Toggle buttons set
    /// this so a click, a menu pick, and a keyboard shortcut all latch the same
    /// way.
    /// </summary>
    [Category("Command")]
    [DefaultValue(false)]
    public bool IsCheckable { get; set; }

    // --- Execution ---------------------------------------------------------

    /// <summary>Optional gate evaluated by <see cref="CanExecute"/>.</summary>
    [Browsable(false)]
    public Func<CommandExecuteContext, bool>? CanExecuteHandler { get; set; }

    /// <summary>Primary work performed by <see cref="Perform"/>.</summary>
    [Browsable(false)]
    public Action<CommandExecuteContext>? ExecuteHandler { get; set; }

    /// <summary>Raised before the primary handler; set Cancel to abort.</summary>
    public event EventHandler<CommandExecuteContext>? Executing;

    /// <summary>Raised after the primary handler completes.</summary>
    public event EventHandler<CommandExecuteContext>? Executed;

    /// <summary>Raised whenever a bindable property changes.</summary>
    public event PropertyChangedEventHandler? PropertyChanged;

    /// <summary>
    /// True if the command is enabled and any <see cref="CanExecuteHandler"/>
    /// returns true.
    /// </summary>
    public bool CanExecute(object? parameter = null)
    {
        if (!_enabled)
            return false;
        if (CanExecuteHandler is null)
            return true;
        return CanExecuteHandler(new CommandExecuteContext(this, parameter));
    }

    /// <summary>
    /// Runs the command: checks <see cref="CanExecute"/>, raises
    /// <see cref="Executing"/> (cancelable), invokes <see cref="ExecuteHandler"/>,
    /// then raises <see cref="Executed"/>.
    /// </summary>
    /// <returns>True if the command actually ran.</returns>
    public bool Perform(object? parameter = null)
    {
        if (!CanExecute(parameter))
            return false;

        var context = new CommandExecuteContext(this, parameter);

        Executing?.Invoke(this, context);
        if (context.Cancel)
            return false;

        if (IsCheckable)
        {
            Checked = Checked == CommandCheckState.Checked
                ? CommandCheckState.Unchecked
                : CommandCheckState.Checked;
        }

        ExecuteHandler?.Invoke(context);
        Executed?.Invoke(this, context);
        return true;
    }

    /// <summary>
    /// Removes single '&amp;' mnemonic markers, collapsing '&amp;&amp;' to a
    /// literal ampersand.
    /// </summary>
    public static string RemoveMnemonic(string? text)
    {
        if (string.IsNullOrEmpty(text))
            return string.Empty;

        var sb = new StringBuilder(text.Length);
        for (int i = 0; i < text.Length; i++)
        {
            char c = text[i];
            if (c == '&')
            {
                if (i + 1 < text.Length && text[i + 1] == '&')
                {
                    sb.Append('&');
                    i++; // consume the escaped ampersand
                }
                // otherwise drop the single mnemonic marker
            }
            else
            {
                sb.Append(c);
            }
        }
        return sb.ToString();
    }

    /// <summary>Formats a shortcut as display text (e.g. "Ctrl+B"); empty for None.</summary>
    public static string FormatShortcut(Keys keys)
    {
        if (keys == Keys.None)
            return string.Empty;
        try
        {
            return new KeysConverter().ConvertToString(keys) ?? string.Empty;
        }
        catch
        {
            return string.Empty;
        }
    }

    protected bool SetField<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value))
            return false;
        field = value;
        OnPropertyChanged(propertyName);
        return true;
    }

    protected virtual void OnPropertyChanged(string? propertyName)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
}
