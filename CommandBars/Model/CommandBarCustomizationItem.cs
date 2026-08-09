using CommandBars.Imaging;

namespace CommandBars.Model;

/// <summary>
/// One entry in the Customize dialog's command palette. Unlike a plain
/// <see cref="Command"/>, an entry can create any command-bar item, including a
/// hosted combo box or a popup with its complete child hierarchy.
/// </summary>
public sealed class CommandBarCustomizationItem
{
    private readonly Func<CommandBarItem> _factory;

    /// <summary>Creates a customization entry backed by an item factory.</summary>
    public CommandBarCustomizationItem(
        string id,
        string text,
        IImageSource? image,
        Func<CommandBarItem> factory)
    {
        if (string.IsNullOrWhiteSpace(id))
            throw new ArgumentException("Customization item id must be non-empty.", nameof(id));
        Id = id;
        Text = text ?? string.Empty;
        Image = image;
        _factory = factory ?? throw new ArgumentNullException(nameof(factory));
    }

    /// <summary>Stable identity used to de-duplicate palette entries.</summary>
    public string Id { get; }

    /// <summary>Caption shown in the customization command list.</summary>
    public string Text { get; }

    /// <summary>Optional icon shown beside the caption.</summary>
    public IImageSource? Image { get; }

    /// <summary>Creates a fresh, unowned item suitable for insertion into a toolbar.</summary>
    public CommandBarItem CreateItem()
    {
        var item = _factory() ?? throw new InvalidOperationException("The customization item factory returned null.");
        if (item.OwnerBar is not null)
            throw new InvalidOperationException("The customization item factory must return a fresh, unowned item.");
        item.Visible = true;
        return item;
    }

    /// <summary>Creates the ordinary toolbar-button entry for a command.</summary>
    public static CommandBarCustomizationItem FromCommand(Command command)
    {
        ArgumentNullException.ThrowIfNull(command);
        return new CommandBarCustomizationItem(
            command.Id,
            command.DisplayText,
            command.Image,
            () => new CommandBarButton(command) { DisplayStyle = CommandItemDisplayStyle.ImageOnly });
    }

    public override string ToString() => Text;
}
