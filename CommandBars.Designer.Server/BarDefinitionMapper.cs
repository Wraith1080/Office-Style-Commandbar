using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Windows.Forms;
using CommandBars.Design;
using CommandBars.Imaging;
using CommandBars.Model;
using Proto = CommandBars.Designer.Protocol;

namespace CommandBars.Designer.Server;

/// <summary>
/// Maps between the runtime definition objects (<see cref="BarDefinition"/> /
/// <see cref="ItemDefinition"/>, which only exist in the .NET runtime assembly)
/// and the transport POCOs (<see cref="Proto.BarDefData"/> /
/// <see cref="Proto.ItemDefData"/>, shared with the net472 client). Lives on the
/// server because only the server references the runtime library.
/// </summary>
internal static class BarDefinitionMapper
{
    // ---- full snapshot ----

    public static Proto.DesignSnapshot ToSnapshot(CommandBarManager manager) => new()
    {
        Bars = ToData(manager.BarDefinitions),
        Commands = ToData(manager.CommandDefinitions),
        Images = ToImageData(manager.Images),
    };

    /// <summary>Renders the connected SvgImageList's entries to small PNG
    /// thumbnails for the client-side ImageKey picker.</summary>
    public static List<Proto.ImageEntryData> ToImageData(SvgImageList? images)
    {
        var list = new List<Proto.ImageEntryData>();
        if (images is null)
            return list;

        foreach (var img in images.Images)
        {
            if (string.IsNullOrWhiteSpace(img.Key))
                continue;
            string png = string.Empty;
            try
            {
                var source = img.GetSource();
                if (source?.GetImage(32) is Image bitmap)
                {
                    using var ms = new MemoryStream();
                    bitmap.Save(ms, ImageFormat.Png);
                    png = System.Convert.ToBase64String(ms.ToArray());
                }
            }
            catch
            {
                // A bad glyph just shows as a key with no thumbnail.
            }
            list.Add(new Proto.ImageEntryData { Key = img.Key, Png = png });
        }
        return list;
    }

    // ---- runtime -> transport (snapshot for the dialog) ----

    public static List<Proto.BarDefData> ToData(IEnumerable<BarDefinition> bars)
    {
        var list = new List<Proto.BarDefData>();
        foreach (var bar in bars)
            list.Add(ToData(bar));
        return list;
    }

    public static List<Proto.CommandDefData> ToData(IEnumerable<CommandDefinition> commands)
    {
        var list = new List<Proto.CommandDefData>();
        foreach (var c in commands)
            list.Add(new Proto.CommandDefData
            {
                Id = c.Id,
                Kind = (Proto.CommandKindData)(int)c.Kind,
                Text = c.Text,
                ImageKey = c.ImageKey,
                Shortcut = c.Shortcut,
                ToolTip = c.ToolTip,
                DisplayStyle = (Proto.ItemDisplayData)(int)c.DisplayStyle,
                InitialChecked = (Proto.CommandCheckStateData)(int)c.InitialChecked,
                ContentSource = (Proto.CommandContentSourceData)(int)c.ContentSource,
                TearOff = c.TearOff,
                TearOffTitle = c.TearOffTitle,
                PaletteColumns = c.PaletteColumns,
                ComboWidth = c.ComboWidth,
                ComboItems = new List<string>(c.ComboItems),
                Items = ToData(c.Items),
                IncludeInCommandList = c.IncludeInCommandList,
            });
        return list;
    }

    private static List<Proto.CommandPlacementData> ToData(
        IEnumerable<CommandPlacementDefinition> placements)
    {
        var list = new List<Proto.CommandPlacementData>();
        foreach (var placement in placements)
        {
            list.Add(new Proto.CommandPlacementData
            {
                Kind = (Proto.CommandPlacementKindData)(int)placement.Kind,
                CommandId = placement.CommandId,
                Name = placement.Name,
                Visible = placement.Visible,
                BeginGroup = placement.BeginGroup,
                Priority = placement.Priority,
                UseCatalogDisplayStyle = placement.UseCatalogDisplayStyle,
                DisplayStyle = (Proto.ItemDisplayData)(int)placement.DisplayStyle,
            });
        }
        return list;
    }

    private static Proto.BarDefData ToData(BarDefinition bar) => new()
    {
        Name = bar.Name,
        Text = bar.Text,
        BarType = bar.BarType == CommandBarType.MenuBar ? Proto.BarKind.MenuBar : Proto.BarKind.Toolbar,
        Dock = ToDock(bar.Dock),
        Visible = bar.Visible,
        IconSize = bar.IconSize,
        AllowFloat = bar.AllowFloat,
        AllowCustomize = bar.AllowCustomize,
        Items = ToData(bar.Items),
    };

    private static List<Proto.ItemDefData> ToData(IEnumerable<ItemDefinition> items)
    {
        var list = new List<Proto.ItemDefData>();
        foreach (var item in items)
            list.Add(ToData(item));
        return list;
    }

    private static Proto.ItemDefData ToData(ItemDefinition item) => new()
    {
        Kind = (Proto.ItemKindData)(int)item.Kind,
        Name = item.Name,
        CommandId = item.CommandId,
        Text = item.Text,
        ImageKey = item.ImageKey,
        ImagePath = item.ImagePath,
        DisplayStyle = (Proto.ItemDisplayData)(int)item.DisplayStyle,
        BeginGroup = item.BeginGroup,
        Priority = item.Priority,
        IncludeInCommandList = item.IncludeInCommandList,
        TearOff = item.TearOff,
        TearOffTitle = item.TearOffTitle,
        PaletteColumns = item.PaletteColumns,
        ToolbarList = item.ToolbarList,
        ThemeList = item.ThemeList,
        Visible = item.Visible,
        Shortcut = item.Shortcut,
        ComboWidth = item.ComboWidth,
        ComboItems = new List<string>(item.ComboItems),
        Items = ToData(item.Items),
    };

    // ---- transport -> runtime (rebuild after edit) ----

    public static List<BarDefinition> ToRuntime(IEnumerable<Proto.BarDefData> data)
    {
        var list = new List<BarDefinition>();
        foreach (var d in data)
            list.Add(ToRuntime(d));
        return list;
    }

    public static List<CommandDefinition> ToRuntimeCommands(IEnumerable<Proto.CommandDefData> data)
    {
        var list = new List<CommandDefinition>();
        foreach (var d in data)
        {
            var definition = new CommandDefinition
            {
                Id = d.Id,
                Kind = (CommandDefinitionKind)(int)d.Kind,
                Text = d.Text,
                ImageKey = d.ImageKey,
                Shortcut = d.Shortcut,
                ToolTip = d.ToolTip,
                DisplayStyle = (CommandItemDisplayStyle)(int)d.DisplayStyle,
                InitialChecked = (CommandCheckState)(int)d.InitialChecked,
                ContentSource = (CommandContentSource)(int)d.ContentSource,
                TearOff = d.TearOff,
                TearOffTitle = d.TearOffTitle,
                PaletteColumns = d.PaletteColumns,
                ComboWidth = d.ComboWidth,
                IncludeInCommandList = d.IncludeInCommandList,
            };
            if (d.ComboItems is not null)
                foreach (string item in d.ComboItems)
                    definition.ComboItems.Add(item);
            if (d.Items is not null)
                foreach (var placement in ToRuntimePlacements(d.Items))
                    definition.Items.Add(placement);
            list.Add(definition);
        }
        return list;
    }

    private static List<CommandPlacementDefinition> ToRuntimePlacements(
        IEnumerable<Proto.CommandPlacementData> data)
    {
        var list = new List<CommandPlacementDefinition>();
        foreach (var d in data)
        {
            list.Add(new CommandPlacementDefinition
            {
                Kind = (CommandPlacementKind)(int)d.Kind,
                CommandId = d.CommandId,
                Name = d.Name,
                Visible = d.Visible,
                BeginGroup = d.BeginGroup,
                Priority = d.Priority,
                UseCatalogDisplayStyle = d.UseCatalogDisplayStyle,
                DisplayStyle = (CommandItemDisplayStyle)(int)d.DisplayStyle,
            });
        }
        return list;
    }

    private static BarDefinition ToRuntime(Proto.BarDefData d)
    {
        // Instantiate the matching subclass so the regenerated designer code is
        // typed (new MenuBarDefinition() / new ToolbarDefinition()).
        BarDefinition bar = d.BarType == Proto.BarKind.MenuBar
            ? new MenuBarDefinition()
            : new ToolbarDefinition();

        bar.Name = d.Name;
        bar.Text = d.Text;
        bar.BarType = d.BarType == Proto.BarKind.MenuBar ? CommandBarType.MenuBar : CommandBarType.Toolbar;
        bar.Dock = FromDock(d.Dock);
        bar.Visible = d.Visible;
        bar.IconSize = d.IconSize;
        bar.AllowFloat = d.AllowFloat;
        bar.AllowCustomize = d.AllowCustomize;

        foreach (var item in ToRuntimeItems(d.Items))
            bar.Items.Add(item);

        return bar;
    }

    private static List<ItemDefinition> ToRuntimeItems(IEnumerable<Proto.ItemDefData> data)
    {
        var list = new List<ItemDefinition>();
        foreach (var d in data)
            list.Add(ToRuntime(d));
        return list;
    }

    private static ItemDefinition ToRuntime(Proto.ItemDefData d)
    {
        ItemDefinition item = d.Kind switch
        {
            Proto.ItemKindData.ToggleButton => new ToggleButtonDefinition(),
            Proto.ItemKindData.SplitButton => new SplitButtonDefinition(),
            Proto.ItemKindData.Popup => new PopupDefinition(),
            Proto.ItemKindData.Separator => new SeparatorDefinition(),
            Proto.ItemKindData.Label => new LabelDefinition(),
            Proto.ItemKindData.ComboBox => new ComboBoxDefinition(),
            _ => new ButtonDefinition(),
        };

        item.Kind = (CommandItemKind)(int)d.Kind;
        item.Name = d.Name;
        item.CommandId = d.CommandId;
        item.Text = d.Text;
        item.ImageKey = d.ImageKey;
        item.ImagePath = d.ImagePath;
        item.DisplayStyle = (CommandItemDisplayStyle)(int)d.DisplayStyle;
        item.BeginGroup = d.BeginGroup;
        item.Priority = d.Priority;
        item.IncludeInCommandList = d.IncludeInCommandList;
        item.TearOff = d.TearOff;
        item.TearOffTitle = d.TearOffTitle;
        item.PaletteColumns = d.PaletteColumns;
        item.ToolbarList = d.ToolbarList;
        item.ThemeList = d.ThemeList;
        item.Visible = d.Visible;
        item.Shortcut = d.Shortcut;
        item.ComboWidth = d.ComboWidth;
        if (d.ComboItems is not null)
            foreach (var entry in d.ComboItems)
                item.ComboItems.Add(entry);

        foreach (var child in ToRuntimeItems(d.Items))
            item.Items.Add(child);

        return item;
    }

    private static Proto.DockEdgeData ToDock(DockState dock) => dock switch
    {
        DockState.Left => Proto.DockEdgeData.Left,
        DockState.Right => Proto.DockEdgeData.Right,
        DockState.Bottom => Proto.DockEdgeData.Bottom,
        _ => Proto.DockEdgeData.Top,
    };

    private static DockState FromDock(Proto.DockEdgeData dock) => dock switch
    {
        Proto.DockEdgeData.Left => DockState.Left,
        Proto.DockEdgeData.Right => DockState.Right,
        Proto.DockEdgeData.Bottom => DockState.Bottom,
        _ => DockState.Top,
    };
}
