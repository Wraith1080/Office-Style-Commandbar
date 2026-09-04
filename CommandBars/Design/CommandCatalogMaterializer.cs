using CommandBars.Imaging;
using CommandBars.Model;
using System.Windows.Forms;

namespace CommandBars.Design;

/// <summary>
/// Resolves reusable catalog definitions into fresh runtime item instances while
/// sharing executable state through one <see cref="CommandRegistry"/>. Kept
/// internal because applications enter through
/// <see cref="CommandBarManager.CreateCatalogItem"/>.
/// </summary>
internal sealed class CommandCatalogMaterializer
{
    private readonly Dictionary<string, CommandDefinition> _definitions =
        new(StringComparer.Ordinal);
    private readonly CommandRegistry _registry;
    private readonly SvgImageList? _images;
    private readonly bool _designPreview;
    private readonly List<string> _buildPath = new();

    public CommandCatalogMaterializer(
        IEnumerable<CommandDefinition> definitions,
        CommandRegistry registry,
        SvgImageList? images,
        bool designPreview = false)
    {
        ArgumentNullException.ThrowIfNull(definitions);
        _registry = registry ?? throw new ArgumentNullException(nameof(registry));
        _images = images;
        _designPreview = designPreview;

        foreach (var definition in definitions)
        {
            if (string.IsNullOrWhiteSpace(definition.Id))
                continue;
            if (!_definitions.TryAdd(definition.Id, definition))
                throw new InvalidOperationException(
                    $"The command catalog contains duplicate id '{definition.Id}'.");
        }
    }

    /// <summary>Registers the executable entries without constructing visual items.</summary>
    public void RegisterCommands()
    {
        foreach (var definition in _definitions.Values)
        {
            if (definition.Kind is CommandDefinitionKind.Action or
                CommandDefinitionKind.Toggle or
                CommandDefinitionKind.SplitButton)
                ResolveCommand(definition);
        }
    }

    /// <summary>Builds a fresh visual occurrence of the requested catalog entry.</summary>
    public CommandBarItem Build(string commandId)
    {
        if (string.IsNullOrWhiteSpace(commandId))
            throw new ArgumentException("Catalog command id must not be empty.", nameof(commandId));
        if (!_definitions.TryGetValue(commandId, out var definition))
            throw new KeyNotFoundException(
                $"No command catalog entry with id '{commandId}' exists.");

        int cycleStart = _buildPath.FindIndex(id =>
            string.Equals(id, commandId, StringComparison.Ordinal));
        if (cycleStart >= 0)
        {
            string cycle = string.Join(" -> ", _buildPath.Skip(cycleStart).Append(commandId));
            throw new InvalidOperationException($"The command catalog contains a cycle: {cycle}.");
        }

        _buildPath.Add(commandId);
        try
        {
            return BuildDefinition(definition);
        }
        finally
        {
            _buildPath.RemoveAt(_buildPath.Count - 1);
        }
    }

    private CommandBarItem BuildDefinition(CommandDefinition definition)
    {
        switch (definition.Kind)
        {
            case CommandDefinitionKind.Action:
                return new CommandBarButton(ResolveCommand(definition))
                {
                    DisplayStyle = definition.DisplayStyle,
                };

            case CommandDefinitionKind.Toggle:
                return new CommandBarToggleButton(ResolveCommand(definition))
                {
                    DisplayStyle = definition.DisplayStyle,
                };

            case CommandDefinitionKind.Popup:
            {
                var popup = new CommandBarPopupItem(definition.Text)
                {
                    Image = ResolveImage(definition.ImageKey),
                    ToolbarList = definition.ContentSource == CommandContentSource.ToolbarList,
                    ThemeList = definition.ContentSource == CommandContentSource.ThemeList,
                };
                if (definition.ContentSource == CommandContentSource.Authored)
                    FillChildren(popup.DropDown, definition.Items);
                ApplyDropDownOptions(popup.DropDown, definition);
                return popup;
            }

            case CommandDefinitionKind.SplitButton:
            {
                var split = new CommandBarSplitButton(ResolveCommand(definition))
                {
                    DisplayStyle = definition.DisplayStyle,
                };
                FillChildren(split.DropDown, definition.Items);
                ApplyDropDownOptions(split.DropDown, definition);
                return split;
            }

            case CommandDefinitionKind.ComboBox:
            {
                var combo = new CommandBarComboBox
                {
                    Name = definition.Id,
                    Width = definition.ComboWidth,
                    Image = ResolveImage(definition.ImageKey),
                    Label = string.IsNullOrWhiteSpace(definition.Text)
                        ? null
                        : Command.RemoveMnemonic(definition.Text),
                };
                foreach (string entry in definition.ComboItems)
                    combo.Items.Add(entry);
                if (combo.Items.Count > 0)
                    combo.SelectedItem = combo.Items[0];
                return combo;
            }

            case CommandDefinitionKind.Label:
                return new CommandBarLabel(definition.Text);

            default:
                throw new NotSupportedException(
                    $"Catalog kind '{definition.Kind}' is not supported.");
        }
    }

    private void FillChildren(
        CommandBar dropDown,
        IEnumerable<CommandPlacementDefinition> placements)
    {
        foreach (var placement in placements)
            dropDown.Items.Add(BuildPlacement(placement));
    }

    private CommandBarItem BuildPlacement(CommandPlacementDefinition placement)
    {
        CommandBarItem item = placement.Kind == CommandPlacementKind.Separator
            ? new CommandBarSeparator()
            : Build(placement.CommandId);

        item.Visible = placement.Visible;
        item.BeginGroup = placement.BeginGroup;
        item.Priority = placement.Priority;
        if (!string.IsNullOrWhiteSpace(placement.Name))
            item.Name = placement.Name;
        if (!placement.UseCatalogDisplayStyle && item is CommandBarCommandItem commandItem)
            commandItem.DisplayStyle = placement.DisplayStyle;
        return item;
    }

    private Command ResolveCommand(CommandDefinition definition)
    {
        bool created = !_registry.TryGet(definition.Id, out var command);
        if (created)
        {
            command = new Command(definition.Id);
            _registry.Register(command);
        }

        if (string.IsNullOrEmpty(command.Text) && !string.IsNullOrEmpty(definition.Text))
            command.Text = definition.Text;
        if (command.Shortcut == Keys.None && definition.Shortcut != Keys.None)
            command.Shortcut = definition.Shortcut;
        if (string.IsNullOrEmpty(command.ToolTip) && !string.IsNullOrEmpty(definition.ToolTip))
            command.ToolTip = definition.ToolTip;

        var image = ResolveImage(definition.ImageKey);
        if ((_designPreview || command.Image is null) && image is not null)
            command.Image = image;

        if (definition.Kind == CommandDefinitionKind.Toggle)
        {
            command.IsCheckable = true;
            if (created)
                command.Checked = definition.InitialChecked;
        }

        return command;
    }

    private IImageSource? ResolveImage(string imageKey)
        => _images is not null && !string.IsNullOrWhiteSpace(imageKey)
            ? _images.Get(imageKey)
            : null;

    private static void ApplyDropDownOptions(
        CommandBar dropDown,
        CommandDefinition definition)
    {
        dropDown.AllowTearOff = definition.TearOff;
        dropDown.PaletteColumns = Math.Max(0, definition.PaletteColumns);

        string title = !string.IsNullOrWhiteSpace(definition.TearOffTitle)
            ? definition.TearOffTitle
            : Command.RemoveMnemonic(definition.Text);
        if (definition.TearOff && !string.IsNullOrWhiteSpace(title))
            dropDown.Text = title;
    }
}
