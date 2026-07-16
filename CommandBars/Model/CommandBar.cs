using System.ComponentModel;
using System.Drawing;

namespace CommandBars.Model;

/// <summary>
/// A single command bar — a menu bar, a toolbar, or a popup menu. All three
/// are the same type configured differently, which is what lets docking,
/// theming and customization each have one implementation.
/// </summary>
public class CommandBar
{
    public CommandBar(string name, CommandBarType barType)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("Bar name must be a non-empty string.", nameof(name));
        Name = name;
        BarType = barType;
        Text = name;
        Items = new CommandBarItemCollection(this);
    }

    /// <summary>Stable identity used for persistence and lookup.</summary>
    public string Name { get; }

    /// <summary>Display title (shown as the caption when floating).</summary>
    public string Text { get; set; }

    /// <summary>The bar's role.</summary>
    public CommandBarType BarType { get; }

    /// <summary>The ordered items on this bar.</summary>
    public CommandBarItemCollection Items { get; }

    /// <summary>
    /// Finds an item on this bar (or in any nested popup/split dropdown) by its
    /// <see cref="CommandBarItem.Name"/>. Returns null if none matches. Useful
    /// for reaching a hosted <see cref="CommandBarComboBox"/> from code.
    /// </summary>
    public CommandBarItem? FindItem(string name)
    {
        if (string.IsNullOrEmpty(name))
            return null;
        return FindIn(Items);

        CommandBarItem? FindIn(IEnumerable<CommandBarItem> items)
        {
            foreach (var item in items)
            {
                if (string.Equals(item.Name, name, StringComparison.Ordinal))
                    return item;
                CommandBar? sub = item switch
                {
                    CommandBarPopupItem popup => popup.DropDown,
                    CommandBarSplitButton split => split.DropDown,
                    _ => null,
                };
                if (sub is not null)
                {
                    var found = FindIn(sub.Items);
                    if (found is not null)
                        return found;
                }
            }
            return null;
        }
    }

    /// <summary>Finds a hosted combo box by name (convenience over <see cref="FindItem"/>).</summary>
    public CommandBarComboBox? FindComboBox(string name) => FindItem(name) as CommandBarComboBox;

    /// <summary>Current dock placement.</summary>
    [Category("CommandBars")]
    [DefaultValue(DockState.Top)]
    public DockState Dock { get; set; } = DockState.Top;

    /// <summary>Whether the bar is currently shown.</summary>
    [Category("CommandBars")]
    [DefaultValue(true)]
    public bool Visible { get; set; } = true;

    /// <summary>Whether the user may edit this bar in customize mode.</summary>
    [Category("CommandBars")]
    [DefaultValue(true)]
    public bool AllowCustomize { get; set; } = true;

    /// <summary>Whether the bar may be undocked into a floating window.</summary>
    [Category("CommandBars")]
    [DefaultValue(true)]
    public bool AllowFloat { get; set; } = true;

    /// <summary>
    /// Whether this bar, when shown as a popup/dropdown menu, offers a "tear-off"
    /// grip so the user can drag it out into a standalone floating palette (Office's
    /// tear-off toolbars — e.g. Font Color, AutoShapes). A torn-off palette hosts
    /// the same items as a floating toolbar and cannot be re-docked. Off by default
    /// so ordinary menus (File, Edit, …) don't sprout a grip.
    /// </summary>
    [Category("CommandBars")]
    [DefaultValue(false)]
    public bool AllowTearOff { get; set; }

    /// <summary>When true the bar cannot be moved or resized by the user.</summary>
    [Category("CommandBars")]
    [DefaultValue(false)]
    public bool Locked { get; set; }

    /// <summary>Icon size for this bar, in logical pixels (see <see cref="IconSizes"/>).</summary>
    [Category("CommandBars")]
    [DefaultValue(IconSizes.Default)]
    public int IconSize { get; set; } = IconSizes.Default;

    /// <summary>Row (or column) index within its dock band. Used by persistence.</summary>
    [Browsable(false)]
    public int Row { get; set; }

    /// <summary>Offset along its dock band row. Used by persistence.</summary>
    [Browsable(false)]
    public int Offset { get; set; }

    /// <summary>Bounds used when <see cref="Dock"/> is <see cref="DockState.Floating"/>.</summary>
    [Browsable(false)]
    public Rectangle FloatingBounds { get; set; }

    /// <summary>The manager that owns this bar, if any.</summary>
    [Browsable(false)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public CommandBarManager? Manager { get; internal set; }

    /// <summary>
    /// Layout direction, derived from bar type and dock edge. Popups and
    /// left/right-docked bars are vertical; everything else is horizontal.
    /// </summary>
    [Browsable(false)]
    public BarOrientation Orientation =>
        BarType == CommandBarType.Popup
            ? BarOrientation.Vertical
            : Dock is DockState.Left or DockState.Right
                ? BarOrientation.Vertical
                : BarOrientation.Horizontal;
}
