using System.Collections;

namespace CommandBars.Model;

/// <summary>
/// Owns the application's commands, keyed by <see cref="Command.Id"/>. Bars and
/// items reference commands from here so state stays centralized.
/// </summary>
public sealed class CommandRegistry : IEnumerable<Command>
{
    private readonly Dictionary<string, Command> _byId = new(StringComparer.Ordinal);

    /// <summary>Number of registered commands.</summary>
    public int Count => _byId.Count;

    /// <summary>Gets a command by id, throwing if it is not present.</summary>
    public Command this[string id] => Get(id);

    /// <summary>Registers an existing command. Throws on a duplicate id.</summary>
    public Command Register(Command command)
    {
        ArgumentNullException.ThrowIfNull(command);
        if (_byId.ContainsKey(command.Id))
            throw new InvalidOperationException($"A command with id '{command.Id}' is already registered.");
        _byId.Add(command.Id, command);
        return command;
    }

    /// <summary>
    /// Creates, configures, and registers a command in one call — the core of
    /// the fluent/builder API (decision #1).
    /// </summary>
    public Command Register(string id, Action<Command>? configure = null)
    {
        var command = new Command(id);
        configure?.Invoke(command);
        return Register(command);
    }

    /// <summary>Returns the existing command with this id, or registers a new one.</summary>
    public Command GetOrAdd(string id, Action<Command>? configure = null)
        => _byId.TryGetValue(id, out var existing) ? existing : Register(id, configure);

    /// <summary>True if a command with this id is registered.</summary>
    public bool Contains(string id) => _byId.ContainsKey(id);

    /// <summary>Tries to get a command without throwing.</summary>
    public bool TryGet(string id, out Command command) => _byId.TryGetValue(id, out command!);

    /// <summary>Gets a command by id, throwing a helpful error if missing.</summary>
    public Command Get(string id)
    {
        if (_byId.TryGetValue(id, out var command))
            return command;
        throw new KeyNotFoundException($"No command is registered with id '{id}'.");
    }

    /// <summary>Removes a command by id. Returns false if it was not present.</summary>
    public bool Remove(string id) => _byId.Remove(id);

    /// <summary>Removes all commands.</summary>
    public void Clear() => _byId.Clear();

    public IEnumerator<Command> GetEnumerator() => _byId.Values.GetEnumerator();

    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
}
