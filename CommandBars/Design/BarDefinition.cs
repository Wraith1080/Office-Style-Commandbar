using System.ComponentModel;
using System.Drawing.Design;
using CommandBars.Model;

namespace CommandBars.Design;

/// <summary>
/// A serializable, design-time description of a single bar (a toolbar or the
/// menu bar) and its items. Edited in the VS designer through the manager's
/// <see cref="CommandBarManager.BarDefinitions"/> collection; realized into a
/// live <see cref="CommandBar"/> by the manager at run time.
/// </summary>
[TypeConverter(typeof(ExpandableObjectConverter))]
public class BarDefinition
{
    /// <summary>Stable identity used for lookup and persistence.</summary>
    [Category("CommandBars")]
    [DefaultValue("")]
    public string Name { get; set; } = string.Empty;

    /// <summary>Display title (shown as the caption when the bar floats).</summary>
    [Category("CommandBars")]
    [DefaultValue("")]
    public string Text { get; set; } = string.Empty;

    /// <summary>The bar's role: menu bar, toolbar, or popup.</summary>
    [Category("CommandBars")]
    [DefaultValue(CommandBarType.Toolbar)]
    public CommandBarType BarType { get; set; } = CommandBarType.Toolbar;

    /// <summary>Initial dock placement.</summary>
    [Category("CommandBars")]
    [DefaultValue(DockState.Top)]
    public DockState Dock { get; set; } = DockState.Top;

    /// <summary>Whether the bar is shown.</summary>
    [Category("CommandBars")]
    [DefaultValue(true)]
    public bool Visible { get; set; } = true;

    /// <summary>Icon size for this bar, in logical pixels.</summary>
    [Category("CommandBars")]
    [DefaultValue(IconSizes.Default)]
    public int IconSize { get; set; } = IconSizes.Default;

    /// <summary>Whether the user may undock this bar into a floating window.</summary>
    [Category("CommandBars")]
    [DefaultValue(true)]
    public bool AllowFloat { get; set; } = true;

    /// <summary>Whether the user may edit this bar in customize mode.</summary>
    [Category("CommandBars")]
    [DefaultValue(true)]
    public bool AllowCustomize { get; set; } = true;

    /// <summary>The ordered item definitions on this bar.</summary>
    [Category("CommandBars")]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Content)]
    [Editor(typeof(ItemDefinitionCollectionEditor), typeof(UITypeEditor))]
    public List<ItemDefinition> Items { get; } = new();

    /// <summary>
    /// Canonical ordered placements of reusable catalog entries on this bar.
    /// Stage 2 keeps <see cref="Items"/> as a legacy compatibility collection;
    /// new catalog-first authoring writes this collection instead.
    /// </summary>
    [Category("CommandBars")]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Content)]
    public List<CommandPlacementDefinition> Placements { get; } = new();

    /// <summary>
    /// Realizes this definition into a live <see cref="CommandBar"/>.
    /// <paramref name="images"/> supplies icons referenced by item image keys;
    /// <paramref name="nameOverride"/> supplies a unique name for the design
    /// preview when several definitions are left unnamed.
    /// </summary>
    public CommandBar Build(
        CommandRegistry registry,
        Imaging.SvgImageList? images = null,
        string? nameOverride = null,
        bool designPreview = false)
        => BuildCore(
            registry,
            images,
            catalog: null,
            nameOverride,
            designPreview);

    internal CommandBar Build(
        CommandRegistry registry,
        Imaging.SvgImageList? images,
        CommandCatalogMaterializer catalog,
        string? nameOverride = null,
        bool designPreview = false)
        => BuildCore(
            registry,
            images,
            catalog,
            nameOverride,
            designPreview);

    private CommandBar BuildCore(
        CommandRegistry registry,
        Imaging.SvgImageList? images,
        CommandCatalogMaterializer? catalog,
        string? nameOverride,
        bool designPreview)
    {
        string name = !string.IsNullOrWhiteSpace(nameOverride)
            ? nameOverride
            : string.IsNullOrWhiteSpace(Name) ? "bar" : Name;
        var bar = new CommandBar(name, BarType)
        {
            Text = string.IsNullOrWhiteSpace(Text) ? name : Text,
            Dock = Dock,
            Visible = Visible,
            IconSize = IconSize,
            AllowFloat = AllowFloat,
            AllowCustomize = AllowCustomize,
        };

        foreach (var def in Items)
        {
            var item = def.Build(registry, images, designPreview);
            if (item is not null)
                bar.Items.Add(item);
        }

        if (Placements.Count > 0 && catalog is null)
        {
            throw new InvalidOperationException(
                "Catalog placements must be realized through " +
                "CommandBarManager.BuildFromDefinitions().");
        }

        if (catalog is not null)
        {
            CommandPlacementTarget target = BarType switch
            {
                CommandBarType.MenuBar => CommandPlacementTarget.MenuBar,
                CommandBarType.Popup => CommandPlacementTarget.DropDown,
                _ => CommandPlacementTarget.Toolbar,
            };
            foreach (var placement in Placements)
                bar.Items.Add(catalog.BuildPlacement(placement, target));
        }

        return bar;
    }

    public override string ToString()
    {
        string label = !string.IsNullOrWhiteSpace(Name)
            ? Name
            : !string.IsNullOrWhiteSpace(Text) ? Text : "(unnamed bar)";
        return $"{BarType}: {label}";
    }
}

// --- Role-specific subclasses -------------------------------------------------
// So the toolbar collection editor's Add button offers "Toolbar" and "Menu Bar".

/// <summary>A toolbar definition.</summary>
public sealed class ToolbarDefinition : BarDefinition
{
    public ToolbarDefinition() => BarType = CommandBarType.Toolbar;
}

/// <summary>A menu-bar definition (top-level menu strip). Usually only one.</summary>
public sealed class MenuBarDefinition : BarDefinition
{
    public MenuBarDefinition() => BarType = CommandBarType.MenuBar;
}
