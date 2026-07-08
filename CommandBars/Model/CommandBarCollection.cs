using System.Collections.ObjectModel;

namespace CommandBars.Model;

/// <summary>
/// The set of bars owned by a <see cref="CommandBarManager"/>. Enforces unique
/// bar names and maintains the manager back-reference.
/// </summary>
public sealed class CommandBarCollection : Collection<CommandBar>
{
    private readonly CommandBarManager _owner;

    internal CommandBarCollection(CommandBarManager owner)
    {
        _owner = owner ?? throw new ArgumentNullException(nameof(owner));
    }

    /// <summary>Gets a bar by name, or null if none matches.</summary>
    public CommandBar? this[string name]
        => this.FirstOrDefault(b => string.Equals(b.Name, name, StringComparison.Ordinal));

    /// <summary>True if a bar with this name exists.</summary>
    public bool Contains(string name) => this[name] is not null;

    protected override void InsertItem(int index, CommandBar item)
    {
        ArgumentNullException.ThrowIfNull(item);
        if (Contains(item.Name))
            throw new InvalidOperationException($"A bar named '{item.Name}' already exists.");
        item.Manager = _owner;
        base.InsertItem(index, item);
    }

    protected override void SetItem(int index, CommandBar item)
    {
        ArgumentNullException.ThrowIfNull(item);
        this[index].Manager = null;
        item.Manager = _owner;
        base.SetItem(index, item);
    }

    protected override void RemoveItem(int index)
    {
        this[index].Manager = null;
        base.RemoveItem(index);
    }

    protected override void ClearItems()
    {
        foreach (var bar in this)
            bar.Manager = null;
        base.ClearItems();
    }
}
