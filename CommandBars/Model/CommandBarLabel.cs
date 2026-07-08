namespace CommandBars.Model;

/// <summary>A non-interactive text label hosted on a bar.</summary>
public sealed class CommandBarLabel : CommandBarItem
{
    public CommandBarLabel(string text)
    {
        Text = text ?? string.Empty;
    }

    public override CommandItemKind Kind => CommandItemKind.Label;

    /// <summary>The label caption.</summary>
    public string Text { get; set; }
}
