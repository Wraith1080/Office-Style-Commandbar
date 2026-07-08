namespace CommandBars.Model;

/// <summary>A standard push button backed by a <see cref="Command"/>.</summary>
public class CommandBarButton : CommandBarCommandItem
{
    public CommandBarButton(Command command) : base(command)
    {
    }

    public override CommandItemKind Kind => CommandItemKind.Button;
}
