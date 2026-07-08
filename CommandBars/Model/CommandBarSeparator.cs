namespace CommandBars.Model;

/// <summary>An explicit separator between items on a bar or menu.</summary>
public sealed class CommandBarSeparator : CommandBarItem
{
    public override CommandItemKind Kind => CommandItemKind.Separator;
}
