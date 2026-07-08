namespace CommandBars.Model;

/// <summary>
/// A checkable button. Its <see cref="Checked"/> state is projected onto the
/// backing command's <see cref="Command.Checked"/> so all views agree.
/// </summary>
public sealed class CommandBarToggleButton : CommandBarCommandItem
{
    public CommandBarToggleButton(Command command) : base(command)
    {
        // Latch through the command so clicks, menu picks and shortcuts agree.
        command.IsCheckable = true;
    }

    public override CommandItemKind Kind => CommandItemKind.ToggleButton;

    /// <summary>
    /// Convenience boolean view over the command's check state. Getting returns
    /// true only for <see cref="CommandCheckState.Checked"/>; setting maps to
    /// Checked/Unchecked.
    /// </summary>
    public bool Checked
    {
        get => Command.Checked == CommandCheckState.Checked;
        set => Command.Checked = value ? CommandCheckState.Checked : CommandCheckState.Unchecked;
    }
}
