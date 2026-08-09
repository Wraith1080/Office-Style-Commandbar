using System.Collections.ObjectModel;

namespace CommandBars.Model;

/// <summary>
/// The ordered items on a <see cref="CommandBar"/>. Maintains the
/// <see cref="CommandBarItem.OwnerBar"/> back-reference and exposes fluent
/// Add* helpers that make code-driven construction (decision #1) concise.
/// </summary>
public sealed class CommandBarItemCollection : Collection<CommandBarItem>
{
    private readonly CommandBar _owner;

    internal CommandBarItemCollection(CommandBar owner)
    {
        _owner = owner ?? throw new ArgumentNullException(nameof(owner));
    }

    // --- Fluent construction helpers --------------------------------------

    /// <summary>Adds a push button for <paramref name="command"/>.</summary>
    public CommandBarButton AddButton(Command command)
    {
        var item = new CommandBarButton(command);
        Add(item);
        return item;
    }

    /// <summary>Adds a checkable button for <paramref name="command"/>.</summary>
    public CommandBarToggleButton AddToggle(Command command)
    {
        var item = new CommandBarToggleButton(command);
        Add(item);
        return item;
    }

    /// <summary>Adds a split button for <paramref name="command"/>.</summary>
    public CommandBarSplitButton AddSplitButton(Command command)
    {
        var item = new CommandBarSplitButton(command);
        Add(item);
        return item;
    }

    /// <summary>Adds a submenu entry with the given caption.</summary>
    public CommandBarPopupItem AddPopup(string text)
    {
        var item = new CommandBarPopupItem(text);
        Add(item);
        return item;
    }

    /// <summary>Adds an explicit separator.</summary>
    public CommandBarSeparator AddSeparator()
    {
        var item = new CommandBarSeparator();
        Add(item);
        return item;
    }

    /// <summary>Adds a text label.</summary>
    public CommandBarLabel AddLabel(string text)
    {
        var item = new CommandBarLabel(text);
        Add(item);
        return item;
    }

    /// <summary>Adds a combo box.</summary>
    public CommandBarComboBox AddComboBox()
    {
        var item = new CommandBarComboBox();
        Add(item);
        return item;
    }

    // --- Owner wiring ------------------------------------------------------

    protected override void InsertItem(int index, CommandBarItem item)
    {
        ArgumentNullException.ThrowIfNull(item);
        if (item.OwnerBar is not null && !ReferenceEquals(item.OwnerBar, _owner))
            throw new InvalidOperationException("The item already belongs to another bar.");
        item.OwnerBar = _owner;
        CommandBar.PropagateManager(item, _owner.Manager);
        base.InsertItem(index, item);
    }

    protected override void SetItem(int index, CommandBarItem item)
    {
        ArgumentNullException.ThrowIfNull(item);
        CommandBar.PropagateManager(this[index], null);
        this[index].OwnerBar = null;
        item.OwnerBar = _owner;
        CommandBar.PropagateManager(item, _owner.Manager);
        base.SetItem(index, item);
    }

    protected override void RemoveItem(int index)
    {
        CommandBar.PropagateManager(this[index], null);
        this[index].OwnerBar = null;
        base.RemoveItem(index);
    }

    protected override void ClearItems()
    {
        foreach (var item in this)
        {
            CommandBar.PropagateManager(item, null);
            item.OwnerBar = null;
        }
        base.ClearItems();
    }
}
