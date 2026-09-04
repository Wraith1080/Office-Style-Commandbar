namespace CommandBars.Model;

/// <summary>
/// A button with an attached dropdown arrow. Clicking the main area performs
/// the command; clicking the arrow opens <see cref="DropDown"/>.
/// </summary>
public sealed class CommandBarSplitButton : CommandBarCommandItem
{
    public CommandBarSplitButton(Command command) : base(command)
    {
        DropDown = new CommandBar("split:" + command.Id, CommandBarType.Popup);
    }

    // Overflow menus create a second visual owner for the same split command.
    // Sharing the source dropdown keeps its live contents, tear-off settings,
    // and manager callbacks intact; the overflow row still has its own bounds.
    internal CommandBarSplitButton(Command command, CommandBar dropDown) : base(command)
    {
        DropDown = dropDown ?? throw new ArgumentNullException(nameof(dropDown));
    }

    public override CommandItemKind Kind => CommandItemKind.SplitButton;

    /// <summary>The popup bar opened by the dropdown arrow.</summary>
    public CommandBar DropDown { get; }
}
