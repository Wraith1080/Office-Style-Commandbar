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
    private readonly Dictionary<string, Command> _catalogOwnedCommands;
    private readonly bool _designPreview;
    private readonly List<string> _buildPath = new();

    public CommandCatalogMaterializer(
        IEnumerable<CommandDefinition> definitions,
        CommandRegistry registry,
        SvgImageList? images,
        Dictionary<string, Command> catalogOwnedCommands,
        bool designPreview = false)
    {
        ArgumentNullException.ThrowIfNull(definitions);
        _registry = registry ?? throw new ArgumentNullException(nameof(registry));
        _images = images;
        _catalogOwnedCommands = catalogOwnedCommands
            ?? throw new ArgumentNullException(nameof(catalogOwnedCommands));
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
        var executableIds = new HashSet<string>(
            _definitions.Values
                .Where(IsExecutable)
                .Select(GetExecutableId),
            StringComparer.Ordinal);

        // A command that this catalog created stops being executable when its
        // definition is removed or changes to Popup/Combo/Label. Never remove a
        // same-id command that application code installed after the catalog
        // command was removed from the registry.
        foreach (var pair in _catalogOwnedCommands.ToArray())
        {
            bool stillTracked = _registry.TryGet(pair.Key, out var registered) &&
                                ReferenceEquals(registered, pair.Value);
            if (!stillTracked)
            {
                _catalogOwnedCommands.Remove(pair.Key);
                continue;
            }
            if (!executableIds.Contains(pair.Key))
            {
                _registry.Remove(pair.Key);
                _catalogOwnedCommands.Remove(pair.Key);
            }
        }

        foreach (var definition in _definitions.Values)
        {
            if (IsExecutable(definition))
                ResolveExecutableCommand(definition);
        }
    }

    private static bool IsExecutable(CommandDefinition definition)
        => definition.Kind is CommandDefinitionKind.Action or
            CommandDefinitionKind.Toggle or
            CommandDefinitionKind.SplitButton;

    private static string GetExecutableId(CommandDefinition definition)
        => definition.Kind == CommandDefinitionKind.SplitButton &&
           !string.IsNullOrWhiteSpace(definition.PrimaryCommandId)
            ? definition.PrimaryCommandId
            : definition.Id;

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
                return new CommandBarButton(ResolveExecutableCommand(definition))
                {
                    DisplayStyle = definition.DisplayStyle,
                };

            case CommandDefinitionKind.Toggle:
                return new CommandBarToggleButton(ResolveExecutableCommand(definition))
                {
                    DisplayStyle = definition.DisplayStyle,
                };

            case CommandDefinitionKind.Popup:
            {
                var popup = new CommandBarPopupItem(definition.Text)
                {
                    Image = ResolveImage(definition),
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
                var split = new CommandBarSplitButton(ResolveExecutableCommand(definition))
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
                    Image = ResolveImage(definition),
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
            dropDown.Items.Add(BuildPlacement(placement, CommandPlacementTarget.DropDown));
    }

    internal CommandBarItem BuildPlacement(
        CommandPlacementDefinition placement,
        CommandPlacementTarget target)
    {
        ArgumentNullException.ThrowIfNull(placement);

        CommandBarItem item;
        if (placement.Kind == CommandPlacementKind.Separator)
        {
            if (target == CommandPlacementTarget.MenuBar)
                throw IncompatiblePlacement("Separator", target);
            item = new CommandBarSeparator();
        }
        else
        {
            if (!_definitions.TryGetValue(placement.CommandId, out var definition))
                throw new KeyNotFoundException(
                    $"No command catalog entry with id '{placement.CommandId}' exists.");
            if (!CommandPlacementRules.CanPlace(definition.Kind, target))
                throw IncompatiblePlacement(
                    $"catalog entry '{definition.Id}' ({definition.Kind})",
                    target);
            item = Build(placement.CommandId);
        }

        item.Visible = placement.Visible;
        item.BeginGroup = placement.BeginGroup;
        item.Priority = placement.Priority;
        if (!string.IsNullOrWhiteSpace(placement.Name))
            item.Name = placement.Name;
        if (!placement.UseCatalogDisplayStyle && item is CommandBarCommandItem commandItem)
            commandItem.DisplayStyle = placement.DisplayStyle;
        return item;
    }

    private static InvalidOperationException IncompatiblePlacement(
        string item,
        CommandPlacementTarget target)
        => new(
            $"{item} cannot be placed in a " +
            $"{CommandPlacementRules.GetTargetName(target)}.");

    private Command ResolveExecutableCommand(CommandDefinition definition)
    {
        if (definition.Kind != CommandDefinitionKind.SplitButton ||
            string.IsNullOrWhiteSpace(definition.PrimaryCommandId))
            return ResolveCommand(definition, definition.Id);

        if (_definitions.TryGetValue(definition.PrimaryCommandId, out var primary))
        {
            if (primary.Kind is not CommandDefinitionKind.Action and
                not CommandDefinitionKind.Toggle)
            {
                throw new InvalidOperationException(
                    $"SplitButton catalog entry '{definition.Id}' uses " +
                    $"'{primary.Id}' ({primary.Kind}) as its primary command. " +
                    "A split primary must be an Action or Toggle.");
            }
            return ResolveCommand(primary, primary.Id);
        }

        // Compatibility path for an action registered directly in code. If the
        // application has not registered it yet, the split's own presentation
        // supplies a placeholder under the requested primary id.
        return ResolveCommand(definition, definition.PrimaryCommandId);
    }

    private Command ResolveCommand(CommandDefinition definition, string commandId)
    {
        bool created = !_registry.TryGet(commandId, out var command);
        if (created)
        {
            command = new Command(commandId);
            _registry.Register(command);
            _catalogOwnedCommands[commandId] = command;
        }

        bool catalogOwned = _catalogOwnedCommands.TryGetValue(commandId, out var owned) &&
                            ReferenceEquals(command, owned);
        var image = ResolveImage(definition);

        if (catalogOwned)
        {
            command.Text = definition.Text;
            command.Shortcut = definition.Shortcut;
            command.ToolTip = string.IsNullOrEmpty(definition.ToolTip)
                ? null
                : definition.ToolTip;
            command.Image = image;
            command.IsCheckable = definition.Kind == CommandDefinitionKind.Toggle;
        }
        else
        {
            // Application-created commands keep their presentation and handler;
            // the catalog fills only gaps, preserving the original API contract.
            if (string.IsNullOrEmpty(command.Text) && !string.IsNullOrEmpty(definition.Text))
                command.Text = definition.Text;
            if (command.Shortcut == Keys.None && definition.Shortcut != Keys.None)
                command.Shortcut = definition.Shortcut;
            if (string.IsNullOrEmpty(command.ToolTip) && !string.IsNullOrEmpty(definition.ToolTip))
                command.ToolTip = definition.ToolTip;
            if ((_designPreview || command.Image is null) && image is not null)
                command.Image = image;
            if (definition.Kind == CommandDefinitionKind.Toggle)
                command.IsCheckable = true;
        }

        if (definition.Kind == CommandDefinitionKind.Toggle)
        {
            if (created)
                command.Checked = definition.InitialChecked;
        }

        return command;
    }

    private IImageSource? ResolveImage(CommandDefinition definition)
    {
        if (_images is not null && !string.IsNullOrWhiteSpace(definition.ImageKey))
        {
            var image = _images.Get(definition.ImageKey);
            if (image is not null)
                return image;
        }
        return DesignImage.Load(definition.ImagePath);
    }

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
