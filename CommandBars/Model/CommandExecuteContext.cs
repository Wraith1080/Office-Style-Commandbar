namespace CommandBars.Model;

/// <summary>
/// Carries state through a single <see cref="Command.Perform"/> invocation.
/// Handlers may inspect the parameter and cancel execution during the
/// <see cref="Command.Executing"/> phase.
/// </summary>
public sealed class CommandExecuteContext : EventArgs
{
    public CommandExecuteContext(Command command, object? parameter)
    {
        Command = command ?? throw new ArgumentNullException(nameof(command));
        Parameter = parameter;
    }

    /// <summary>The command being executed.</summary>
    public Command Command { get; }

    /// <summary>Optional caller-supplied parameter.</summary>
    public object? Parameter { get; }

    /// <summary>
    /// Set to true during the <see cref="Command.Executing"/> event to abort
    /// before the primary handler and <see cref="Command.Executed"/> run.
    /// </summary>
    public bool Cancel { get; set; }
}
