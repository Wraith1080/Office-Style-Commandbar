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

    public override CommandItemKind Kind => CommandItemKind.SplitButton;

    /// <summary>The popup bar opened by the dropdown arrow.</summary>
    public CommandBar DropDown { get; }
}
