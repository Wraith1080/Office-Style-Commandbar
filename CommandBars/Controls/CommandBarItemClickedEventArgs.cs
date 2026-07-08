using CommandBars.Model;

namespace CommandBars.Controls;

/// <summary>Raised when an interactive item is activated by the user.</summary>
public sealed class CommandBarItemClickedEventArgs : EventArgs
{
    public CommandBarItemClickedEventArgs(CommandBarItem item)
    {
        Item = item;
    }

    /// <summary>The item that was clicked.</summary>
    public CommandBarItem Item { get; }
}
