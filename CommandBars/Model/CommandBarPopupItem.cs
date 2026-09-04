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

    // An overflow menu gives a toolbar dropdown a second visual owner. Reuse
    // the source bar so live submenu contents and dynamic list behavior remain
    // identical to the original button.
    internal CommandBarPopupItem(CommandBarPopupItem source)
    {
        ArgumentNullException.ThrowIfNull(source);
        Text = source.Text;
        Image = source.Image;
        DisplayStyle = source.DisplayStyle;
        DropDown = source.DropDown;
        _toolbarList = source.ToolbarList;
        _themeList = source.ThemeList;
        ComboBoxName = source.ComboBoxName;
        ComboBoxItems.AddRange(source.ComboBoxItems);
        Name = source.Name;
        Tag = source.Tag;
    }

    public override CommandItemKind Kind => CommandItemKind.Popup;

    /// <summary>Caption, may contain a single '&amp;' mnemonic marker.</summary>
    public string Text { get; set; }

    /// <summary>Caption without the mnemonic marker.</summary>
    public string DisplayText => Command.RemoveMnemonic(Text);

    /// <summary>Optional image shown beside the caption.</summary>
    public IImageSource? Image { get; set; }

    /// <summary>
    /// How this popup is presented when it is placed on a toolbar. Menu-bar and
    /// drop-down-menu occurrences always keep their conventional text caption.
    /// </summary>
    public CommandItemDisplayStyle DisplayStyle { get; set; } = CommandItemDisplayStyle.ImageAndText;

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

    // Runtime menu adapter for a hosted combo box. A popup menu cannot host the
    // editor itself, so the manager materializes these values as a checked
    // submenu each time it opens. Kept internal: applications register the
    // combo customization item, not this transport detail.
    internal string? ComboBoxName { get; set; }
    internal List<string> ComboBoxItems { get; } = new();

    /// <summary>The submenu opened by this item.</summary>
    public CommandBar DropDown { get; }
}
