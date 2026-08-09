using CommandBars.Imaging;

namespace CommandBars.Model;

/// <summary>
/// An item that opens a submenu. Used both for top-level menu-bar entries
/// (File, Edit, ...) and for nested submenus. It carries its own caption and
/// owns a <see cref="DropDown"/> popup bar holding the child items.
/// </summary>
public sealed class CommandBarPopupItem : CommandBarItem
{
    public CommandBarPopupItem(string text)
    {
        Text = text ?? string.Empty;
        DropDown = new CommandBar("popup:" + Text, CommandBarType.Popup);
    }

    public override CommandItemKind Kind => CommandItemKind.Popup;

    /// <summary>Caption, may contain a single '&amp;' mnemonic marker.</summary>
    public string Text { get; set; }

    /// <summary>Caption without the mnemonic marker.</summary>
    public string DisplayText => Command.RemoveMnemonic(Text);

    /// <summary>Optional image shown beside the caption.</summary>
    public IImageSource? Image { get; set; }

    /// <summary>
    /// When true, the owning manager populates this popup with a live checklist
    /// of all toolbars whenever it opens.
    /// </summary>
    public bool ToolbarList
    {
        get => _toolbarList;
        set
        {
            _toolbarList = value;
            if (value)
                _themeList = false;
        }
    }

    /// <summary>
    /// When true, the owning manager populates this popup with its registered
    /// themes whenever it opens.
    /// </summary>
    public bool ThemeList
    {
        get => _themeList;
        set
        {
            _themeList = value;
            if (value)
                _toolbarList = false;
        }
    }

    private bool _toolbarList;
    private bool _themeList;

    /// <summary>The submenu opened by this item.</summary>
    public CommandBar DropDown { get; }
}
