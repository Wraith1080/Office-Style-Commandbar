using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace CommandBars.Designer.Protocol;

public enum CatalogDiagnosticSeverity
{
    Warning,
    Error,
}

public enum CatalogDiagnosticCode
{
    EmptyId,
    DuplicateId,
    MissingReference,
    IncompatiblePlacement,
    ReferenceCycle,
    InvalidSplitPrimary,
    IgnoredChildren,
    LegacyItemsPresent,
    MigrationConflict,
}

/// <summary>One actionable problem found in a complete design snapshot.</summary>
public sealed class CatalogDiagnostic
{
    public CatalogDiagnosticSeverity Severity { get; set; }
    public CatalogDiagnosticCode Code { get; set; }
    public string Message { get; set; } = string.Empty;
    public string Location { get; set; } = string.Empty;
    public string CommandId { get; set; } = string.Empty;

    public override string ToString()
        => string.IsNullOrWhiteSpace(Location)
            ? Message
            : Location + ": " + Message;
}

/// <summary>Validation result used by the client editor before committing.</summary>
public sealed class CatalogValidationResult
{
    public CatalogValidationResult(IReadOnlyList<CatalogDiagnostic> diagnostics)
        => Diagnostics = diagnostics;

    public IReadOnlyList<CatalogDiagnostic> Diagnostics { get; }
    public bool IsValid => !Diagnostics.Any(
        diagnostic => diagnostic.Severity == CatalogDiagnosticSeverity.Error);
}

public enum CommandUsageKind
{
    BarPlacement,
    CompoundPlacement,
    SplitPrimary,
    LegacyItem,
}

/// <summary>One reference to a catalog id, suitable for usage navigation.</summary>
public sealed class CommandUsageData
{
    public CommandUsageKind Kind { get; set; }
    public string CommandId { get; set; } = string.Empty;
    public string OwnerId { get; set; } = string.Empty;
    public string Location { get; set; } = string.Empty;

    public override string ToString() => Location;
}

public enum LegacyMigrationChangeKind
{
    CreatedCatalogEntry,
    UpgradedCatalogEntry,
    ConvertedPlacement,
}

/// <summary>One human-readable operation in a dry-run migration report.</summary>
public sealed class LegacyMigrationChange
{
    public LegacyMigrationChangeKind Kind { get; set; }
    public string Location { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
}

/// <summary>
/// A non-destructive migration proposal. <see cref="MigratedSnapshot"/> is a
/// deep clone; the source passed to <see cref="CatalogDesignService.CreateLegacyMigrationPlan"/>
/// is never mutated.
/// </summary>
public sealed class LegacyMigrationPlan
{
    internal LegacyMigrationPlan(
        DesignSnapshot migratedSnapshot,
        IReadOnlyList<LegacyMigrationChange> changes,
        IReadOnlyList<CatalogDiagnostic> migrationDiagnostics,
        CatalogValidationResult validation)
    {
        MigratedSnapshot = migratedSnapshot;
        Changes = changes;
        MigrationDiagnostics = migrationDiagnostics;
        Validation = validation;
    }

    public DesignSnapshot MigratedSnapshot { get; }
    public IReadOnlyList<LegacyMigrationChange> Changes { get; }
    public IReadOnlyList<CatalogDiagnostic> MigrationDiagnostics { get; }
    public CatalogValidationResult Validation { get; }
    public bool IsRequired => Changes.Count > 0;
    public bool CanApply =>
        !MigrationDiagnostics.Any(d => d.Severity == CatalogDiagnosticSeverity.Error) &&
        Validation.IsValid;
}

/// <summary>
/// Snapshot-wide catalog operations shared by the Visual Studio client and
/// design server. Every mutating method is explicit; validation and migration
/// planning are read-only.
/// </summary>
public static class CatalogDesignService
{
    public static CatalogValidationResult Validate(DesignSnapshot snapshot)
    {
        if (snapshot is null)
            throw new ArgumentNullException(nameof(snapshot));

        var diagnostics = new List<CatalogDiagnostic>();
        var definitions = BuildDefinitionIndex(snapshot.Commands, diagnostics);

        for (int index = 0; index < snapshot.Commands.Count; index++)
        {
            var definition = snapshot.Commands[index];
            string location = "Commands[" + index + "]";
            ValidateDefinition(definition, location, definitions, diagnostics);
        }

        for (int barIndex = 0; barIndex < snapshot.Bars.Count; barIndex++)
        {
            var bar = snapshot.Bars[barIndex];
            string barName = string.IsNullOrWhiteSpace(bar.Name)
                ? "Bars[" + barIndex + "]"
                : "Bar '" + bar.Name + "'";
            var target = bar.BarType == BarKind.MenuBar
                ? CommandPlacementTargetData.MenuBar
                : CommandPlacementTargetData.Toolbar;
            ValidatePlacements(
                bar.Placements,
                target,
                barName + "/Placements",
                definitions,
                diagnostics);

            if (bar.Items.Count > 0)
            {
                diagnostics.Add(Diagnostic(
                    CatalogDiagnosticSeverity.Warning,
                    CatalogDiagnosticCode.LegacyItemsPresent,
                    "This bar still contains legacy full-item definitions and requires migration.",
                    barName + "/Items"));
            }
        }

        DetectCycles(snapshot.Commands, definitions, diagnostics);
        return new CatalogValidationResult(diagnostics);
    }

    /// <summary>
    /// Validates a snapshot at a catalog-first authoring boundary. Legacy item
    /// trees remain warnings in <see cref="Validate"/> so they can be inspected
    /// and migrated, but they are errors here so no current designer can save a
    /// newly authored or silently retained anonymous item tree.
    /// </summary>
    public static CatalogValidationResult ValidateCatalogFirst(DesignSnapshot snapshot)
    {
        var validation = Validate(snapshot);
        if (!validation.Diagnostics.Any(diagnostic =>
            diagnostic.Code == CatalogDiagnosticCode.LegacyItemsPresent))
            return validation;

        var diagnostics = validation.Diagnostics.Select(diagnostic =>
            diagnostic.Code == CatalogDiagnosticCode.LegacyItemsPresent
                ? new CatalogDiagnostic
                {
                    Severity = CatalogDiagnosticSeverity.Error,
                    Code = diagnostic.Code,
                    Message = "Legacy full-item definitions must be migrated before saving.",
                    Location = diagnostic.Location,
                    CommandId = diagnostic.CommandId,
                }
                : diagnostic).ToList();
        return new CatalogValidationResult(diagnostics);
    }

    public static IReadOnlyList<CommandUsageData> FindUsages(
        DesignSnapshot snapshot,
        string commandId)
    {
        if (snapshot is null)
            throw new ArgumentNullException(nameof(snapshot));
        if (string.IsNullOrWhiteSpace(commandId))
            throw new ArgumentException("Command id must not be empty.", nameof(commandId));

        var usages = new List<CommandUsageData>();
        for (int commandIndex = 0; commandIndex < snapshot.Commands.Count; commandIndex++)
        {
            var command = snapshot.Commands[commandIndex];
            string owner = string.IsNullOrWhiteSpace(command.Id)
                ? "Commands[" + commandIndex + "]"
                : command.Id;
            if (string.Equals(command.PrimaryCommandId, commandId, StringComparison.Ordinal))
            {
                usages.Add(new CommandUsageData
                {
                    Kind = CommandUsageKind.SplitPrimary,
                    CommandId = commandId,
                    OwnerId = owner,
                    Location = "Command '" + owner + "'/PrimaryCommandId",
                });
            }
            FindPlacementUsages(
                command.Items,
                commandId,
                CommandUsageKind.CompoundPlacement,
                owner,
                "Command '" + owner + "'/Items",
                usages);
        }

        for (int barIndex = 0; barIndex < snapshot.Bars.Count; barIndex++)
        {
            var bar = snapshot.Bars[barIndex];
            string owner = string.IsNullOrWhiteSpace(bar.Name)
                ? "Bars[" + barIndex + "]"
                : bar.Name;
            FindPlacementUsages(
                bar.Placements,
                commandId,
                CommandUsageKind.BarPlacement,
                owner,
                "Bar '" + owner + "'/Placements",
                usages);
            FindLegacyUsages(
                bar.Items,
                commandId,
                owner,
                "Bar '" + owner + "'/Items",
                usages);
        }
        return usages;
    }

    /// <summary>
    /// Atomically renames one unique catalog id and every canonical, split
    /// primary, and legacy reference to it. Returns the number of references
    /// rewritten.
    /// </summary>
    public static int RenameCommand(
        DesignSnapshot snapshot,
        string oldId,
        string newId)
    {
        if (snapshot is null)
            throw new ArgumentNullException(nameof(snapshot));
        if (string.IsNullOrWhiteSpace(oldId))
            throw new ArgumentException("Old command id must not be empty.", nameof(oldId));
        if (string.IsNullOrWhiteSpace(newId))
            throw new ArgumentException("New command id must not be empty.", nameof(newId));
        if (string.Equals(oldId, newId, StringComparison.Ordinal))
            return 0;

        var matches = snapshot.Commands
            .Where(command => string.Equals(command.Id, oldId, StringComparison.Ordinal))
            .ToList();
        if (matches.Count == 0)
            throw new KeyNotFoundException("No catalog entry with id '" + oldId + "' exists.");
        if (matches.Count > 1)
            throw new InvalidOperationException(
                "Catalog id '" + oldId + "' is duplicated and cannot be renamed safely.");
        if (snapshot.Commands.Any(
            command => string.Equals(command.Id, newId, StringComparison.Ordinal)))
        {
            throw new InvalidOperationException(
                "A catalog entry with id '" + newId + "' already exists.");
        }

        int rewritten = 0;
        matches[0].Id = newId;
        foreach (var command in snapshot.Commands)
        {
            if (string.Equals(command.PrimaryCommandId, oldId, StringComparison.Ordinal))
            {
                command.PrimaryCommandId = newId;
                rewritten++;
            }
            rewritten += RenamePlacements(command.Items, oldId, newId);
        }
        foreach (var bar in snapshot.Bars)
        {
            rewritten += RenamePlacements(bar.Placements, oldId, newId);
            rewritten += RenameLegacyItems(bar.Items, oldId, newId);
        }
        return rewritten;
    }

    /// <summary>
    /// Removes one catalog entry. When <paramref name="removeUsages"/> is false,
    /// any usage blocks removal. When true, placements are removed and dependent
    /// split definitions are removed transitively.
    /// </summary>
    public static bool RemoveCommand(
        DesignSnapshot snapshot,
        string commandId,
        bool removeUsages = false)
    {
        if (snapshot is null)
            throw new ArgumentNullException(nameof(snapshot));
        if (string.IsNullOrWhiteSpace(commandId))
            throw new ArgumentException("Command id must not be empty.", nameof(commandId));

        var definitions = snapshot.Commands
            .Where(command => string.Equals(command.Id, commandId, StringComparison.Ordinal))
            .ToList();
        if (definitions.Count == 0)
            return false;
        if (definitions.Count > 1)
            throw new InvalidOperationException(
                "Catalog id '" + commandId + "' is duplicated and cannot be removed safely.");

        var usages = FindUsages(snapshot, commandId);
        if (usages.Count > 0 && !removeUsages)
        {
            throw new InvalidOperationException(
                "Catalog entry '" + commandId + "' is used in " + usages.Count +
                " location(s). Remove or redirect those usages first.");
        }

        var removedIds = new HashSet<string>(StringComparer.Ordinal) { commandId };
        if (removeUsages)
        {
            bool changed;
            do
            {
                changed = false;
                foreach (var command in snapshot.Commands)
                {
                    if (command.Kind == CommandKindData.SplitButton &&
                        removedIds.Contains(command.PrimaryCommandId) &&
                        removedIds.Add(command.Id))
                        changed = true;
                }
            }
            while (changed);
        }

        snapshot.Commands.RemoveAll(command => removedIds.Contains(command.Id));
        if (removeUsages)
        {
            foreach (var command in snapshot.Commands)
                RemovePlacements(command.Items, removedIds);
            foreach (var bar in snapshot.Bars)
            {
                RemovePlacements(bar.Placements, removedIds);
                RemoveLegacyItems(bar.Items, removedIds);
            }
        }
        return true;
    }

    /// <summary>
    /// Builds a deep-cloned Version 2 snapshot and a detailed conversion report.
    /// The source snapshot remains byte-for-byte semantically unchanged.
    /// </summary>
    public static LegacyMigrationPlan CreateLegacyMigrationPlan(DesignSnapshot source)
    {
        if (source is null)
            throw new ArgumentNullException(nameof(source));

        var clone = DefinitionsSerializer.Deserialize(DefinitionsSerializer.Serialize(source));
        clone.SchemaVersion = DesignSnapshot.CurrentSchemaVersion;
        var changes = new List<LegacyMigrationChange>();
        var migrationDiagnostics = new List<CatalogDiagnostic>();
        var context = new LegacyMigrationContext(clone, changes, migrationDiagnostics);
        context.Convert();
        var validation = Validate(clone);
        return new LegacyMigrationPlan(
            clone,
            changes,
            migrationDiagnostics,
            validation);
    }

    private static Dictionary<string, CommandDefData> BuildDefinitionIndex(
        IEnumerable<CommandDefData> commands,
        List<CatalogDiagnostic> diagnostics)
    {
        var definitions = new Dictionary<string, CommandDefData>(StringComparer.Ordinal);
        int index = 0;
        foreach (var command in commands)
        {
            string location = "Commands[" + index + "]";
            if (string.IsNullOrWhiteSpace(command.Id))
            {
                diagnostics.Add(Diagnostic(
                    CatalogDiagnosticSeverity.Error,
                    CatalogDiagnosticCode.EmptyId,
                    "Catalog ids must be non-empty.",
                    location));
            }
            else if (definitions.ContainsKey(command.Id))
            {
                diagnostics.Add(Diagnostic(
                    CatalogDiagnosticSeverity.Error,
                    CatalogDiagnosticCode.DuplicateId,
                    "Catalog id '" + command.Id + "' is duplicated.",
                    location,
                    command.Id));
            }
            else
            {
                definitions.Add(command.Id, command);
            }
            index++;
        }
        return definitions;
    }

    private static void ValidateDefinition(
        CommandDefData definition,
        string location,
        IReadOnlyDictionary<string, CommandDefData> definitions,
        List<CatalogDiagnostic> diagnostics)
    {
        bool authoredPopup = definition.Kind == CommandKindData.Popup &&
                             definition.ContentSource == CommandContentSourceData.Authored;
        bool split = definition.Kind == CommandKindData.SplitButton;

        if (split && !string.IsNullOrWhiteSpace(definition.PrimaryCommandId))
        {
            CommandDefData? primary;
            if (!definitions.TryGetValue(definition.PrimaryCommandId, out primary) ||
                primary is null)
            {
                diagnostics.Add(Diagnostic(
                    CatalogDiagnosticSeverity.Error,
                    CatalogDiagnosticCode.MissingReference,
                    "Split primary '" + definition.PrimaryCommandId + "' does not exist.",
                    location + "/PrimaryCommandId",
                    definition.PrimaryCommandId));
            }
            else if (primary.Kind != CommandKindData.Action &&
                     primary.Kind != CommandKindData.Toggle)
            {
                diagnostics.Add(Diagnostic(
                    CatalogDiagnosticSeverity.Error,
                    CatalogDiagnosticCode.InvalidSplitPrimary,
                    "A split primary must reference an Action or Toggle.",
                    location + "/PrimaryCommandId",
                    definition.PrimaryCommandId));
            }
        }

        if (authoredPopup || split)
        {
            ValidatePlacements(
                definition.Items,
                CommandPlacementTargetData.DropDown,
                location + "/Items",
                definitions,
                diagnostics);
        }
        else if (definition.Items.Count > 0)
        {
            diagnostics.Add(Diagnostic(
                CatalogDiagnosticSeverity.Warning,
                CatalogDiagnosticCode.IgnoredChildren,
                "These child placements are ignored by this catalog kind/content source.",
                location + "/Items",
                definition.Id));
        }
    }

    private static void ValidatePlacements(
        IEnumerable<CommandPlacementData> placements,
        CommandPlacementTargetData target,
        string location,
        IReadOnlyDictionary<string, CommandDefData> definitions,
        List<CatalogDiagnostic> diagnostics)
    {
        int index = 0;
        foreach (var placement in placements)
        {
            string itemLocation = location + "[" + index + "]";
            if (placement.Kind == CommandPlacementKindData.Separator)
            {
                if (target == CommandPlacementTargetData.MenuBar)
                {
                    diagnostics.Add(Diagnostic(
                        CatalogDiagnosticSeverity.Error,
                        CatalogDiagnosticCode.IncompatiblePlacement,
                        "A separator cannot be placed in a menu-bar root.",
                        itemLocation));
                }
                index++;
                continue;
            }

            CommandDefData? referenced;
            if (string.IsNullOrWhiteSpace(placement.CommandId) ||
                !definitions.TryGetValue(placement.CommandId, out referenced) ||
                referenced is null)
            {
                diagnostics.Add(Diagnostic(
                    CatalogDiagnosticSeverity.Error,
                    CatalogDiagnosticCode.MissingReference,
                    "Referenced catalog entry '" + placement.CommandId + "' does not exist.",
                    itemLocation,
                    placement.CommandId));
            }
            else if (!CommandPlacementRulesData.CanPlace(referenced.Kind, target))
            {
                diagnostics.Add(Diagnostic(
                    CatalogDiagnosticSeverity.Error,
                    CatalogDiagnosticCode.IncompatiblePlacement,
                    "Catalog entry '" + referenced.Id + "' (" + referenced.Kind +
                    ") cannot be placed in a " +
                    CommandPlacementRulesData.GetTargetName(target) + ".",
                    itemLocation,
                    referenced.Id));
            }
            index++;
        }
    }

    private static void DetectCycles(
        IEnumerable<CommandDefData> commands,
        IReadOnlyDictionary<string, CommandDefData> definitions,
        List<CatalogDiagnostic> diagnostics)
    {
        var state = new Dictionary<string, int>(StringComparer.Ordinal);
        var stack = new List<string>();
        var reported = new HashSet<string>(StringComparer.Ordinal);

        foreach (var command in commands)
        {
            if (!string.IsNullOrWhiteSpace(command.Id))
                Visit(command.Id);
        }

        void Visit(string id)
        {
            int existingState;
            if (state.TryGetValue(id, out existingState) && existingState == 2)
                return;
            if (!definitions.ContainsKey(id))
                return;

            state[id] = 1;
            stack.Add(id);
            foreach (var placement in ActiveChildren(definitions[id]))
            {
                if (placement.Kind != CommandPlacementKindData.Command ||
                    string.IsNullOrWhiteSpace(placement.CommandId) ||
                    !definitions.ContainsKey(placement.CommandId))
                    continue;

                int childState;
                if (state.TryGetValue(placement.CommandId, out childState) && childState == 1)
                {
                    int start = stack.FindIndex(
                        value => string.Equals(value, placement.CommandId, StringComparison.Ordinal));
                    var cycleItems = stack.Skip(start).Concat(
                        new[] { placement.CommandId }).ToArray();
                    string cycle = string.Join(" -> ", cycleItems);
                    if (reported.Add(cycle))
                    {
                        diagnostics.Add(Diagnostic(
                            CatalogDiagnosticSeverity.Error,
                            CatalogDiagnosticCode.ReferenceCycle,
                            "The command catalog contains a cycle: " + cycle + ".",
                            "Command '" + id + "'/Items",
                            placement.CommandId));
                    }
                }
                else if (!state.TryGetValue(placement.CommandId, out childState) ||
                         childState == 0)
                {
                    Visit(placement.CommandId);
                }
            }
            stack.RemoveAt(stack.Count - 1);
            state[id] = 2;
        }
    }

    private static IEnumerable<CommandPlacementData> ActiveChildren(CommandDefData definition)
    {
        if (definition.Kind == CommandKindData.SplitButton)
            return definition.Items;
        if (definition.Kind == CommandKindData.Popup &&
            definition.ContentSource == CommandContentSourceData.Authored)
            return definition.Items;
        return Enumerable.Empty<CommandPlacementData>();
    }

    private static CatalogDiagnostic Diagnostic(
        CatalogDiagnosticSeverity severity,
        CatalogDiagnosticCode code,
        string message,
        string location,
        string commandId = "")
        => new CatalogDiagnostic
        {
            Severity = severity,
            Code = code,
            Message = message,
            Location = location,
            CommandId = commandId,
        };

    private static void FindPlacementUsages(
        IList<CommandPlacementData> placements,
        string commandId,
        CommandUsageKind kind,
        string owner,
        string location,
        List<CommandUsageData> usages)
    {
        for (int index = 0; index < placements.Count; index++)
        {
            var placement = placements[index];
            if (placement.Kind == CommandPlacementKindData.Command &&
                string.Equals(placement.CommandId, commandId, StringComparison.Ordinal))
            {
                usages.Add(new CommandUsageData
                {
                    Kind = kind,
                    CommandId = commandId,
                    OwnerId = owner,
                    Location = location + "[" + index + "]",
                });
            }
        }
    }

    private static void FindLegacyUsages(
        IList<ItemDefData> items,
        string commandId,
        string owner,
        string location,
        List<CommandUsageData> usages)
    {
        for (int index = 0; index < items.Count; index++)
        {
            var item = items[index];
            string itemLocation = location + "[" + index + "]";
            if (string.Equals(item.CommandId, commandId, StringComparison.Ordinal))
            {
                usages.Add(new CommandUsageData
                {
                    Kind = CommandUsageKind.LegacyItem,
                    CommandId = commandId,
                    OwnerId = owner,
                    Location = itemLocation,
                });
            }
            FindLegacyUsages(item.Items, commandId, owner, itemLocation + "/Items", usages);
        }
    }

    private static int RenamePlacements(
        IEnumerable<CommandPlacementData> placements,
        string oldId,
        string newId)
    {
        int count = 0;
        foreach (var placement in placements)
        {
            if (placement.Kind == CommandPlacementKindData.Command &&
                string.Equals(placement.CommandId, oldId, StringComparison.Ordinal))
            {
                placement.CommandId = newId;
                count++;
            }
        }
        return count;
    }

    private static int RenameLegacyItems(
        IEnumerable<ItemDefData> items,
        string oldId,
        string newId)
    {
        int count = 0;
        foreach (var item in items)
        {
            if (string.Equals(item.CommandId, oldId, StringComparison.Ordinal))
            {
                item.CommandId = newId;
                count++;
            }
            count += RenameLegacyItems(item.Items, oldId, newId);
        }
        return count;
    }

    private static void RemovePlacements(
        List<CommandPlacementData> placements,
        ISet<string> removedIds)
        => placements.RemoveAll(placement =>
            placement.Kind == CommandPlacementKindData.Command &&
            removedIds.Contains(placement.CommandId));

    private static void RemoveLegacyItems(
        List<ItemDefData> items,
        ISet<string> removedIds)
    {
        for (int index = items.Count - 1; index >= 0; index--)
        {
            var item = items[index];
            if (!string.IsNullOrWhiteSpace(item.CommandId) &&
                removedIds.Contains(item.CommandId))
            {
                items.RemoveAt(index);
            }
            else
            {
                RemoveLegacyItems(item.Items, removedIds);
            }
        }
    }

    private sealed class LegacyMigrationContext
    {
        private readonly DesignSnapshot _snapshot;
        private readonly List<LegacyMigrationChange> _changes;
        private readonly List<CatalogDiagnostic> _diagnostics;
        private readonly Dictionary<string, CommandDefData> _definitions =
            new Dictionary<string, CommandDefData>(StringComparer.Ordinal);
        private readonly HashSet<string> _usedIds =
            new HashSet<string>(StringComparer.Ordinal);
        private readonly Dictionary<string, HashSet<ItemKindData>> _legacyKinds =
            new Dictionary<string, HashSet<ItemKindData>>(StringComparer.Ordinal);

        public LegacyMigrationContext(
            DesignSnapshot snapshot,
            List<LegacyMigrationChange> changes,
            List<CatalogDiagnostic> diagnostics)
        {
            _snapshot = snapshot;
            _changes = changes;
            _diagnostics = diagnostics;

            foreach (var command in snapshot.Commands)
            {
                if (string.IsNullOrWhiteSpace(command.Id))
                    continue;
                _usedIds.Add(command.Id);
                if (!_definitions.ContainsKey(command.Id))
                    _definitions.Add(command.Id, command);
            }
            foreach (var bar in snapshot.Bars)
                GatherLegacyKinds(bar.Items);
        }

        public void Convert()
        {
            for (int barIndex = 0; barIndex < _snapshot.Bars.Count; barIndex++)
            {
                var bar = _snapshot.Bars[barIndex];
                if (bar.Items.Count == 0)
                    continue;

                string barName = string.IsNullOrWhiteSpace(bar.Name)
                    ? "Bars[" + barIndex + "]"
                    : "Bar '" + bar.Name + "'";
                var converted = new List<CommandPlacementData>();
                for (int itemIndex = 0; itemIndex < bar.Items.Count; itemIndex++)
                {
                    converted.Add(ConvertItem(
                        bar.Items[itemIndex],
                        barName + "/Items[" + itemIndex + "]"));
                }
                converted.AddRange(bar.Placements);
                bar.Placements = converted;
                bar.Items.Clear();
            }
        }

        private CommandPlacementData ConvertItem(ItemDefData item, string location)
        {
            var placement = new CommandPlacementData
            {
                Kind = item.Kind == ItemKindData.Separator
                    ? CommandPlacementKindData.Separator
                    : CommandPlacementKindData.Command,
                Name = item.Name,
                Visible = item.Visible,
                BeginGroup = item.BeginGroup,
                Priority = item.Priority,
                UseCatalogDisplayStyle = false,
                DisplayStyle = item.DisplayStyle,
            };

            if (item.Kind == ItemKindData.Separator)
            {
                RecordPlacement(location, "Converted separator placement.");
                return placement;
            }

            string commandId;
            switch (item.Kind)
            {
                case ItemKindData.Button:
                    commandId = EnsureAtomic(item, CommandKindData.Action, location);
                    break;
                case ItemKindData.ToggleButton:
                    commandId = EnsureAtomic(item, CommandKindData.Toggle, location);
                    break;
                case ItemKindData.Popup:
                    commandId = CreatePopup(item, location);
                    break;
                case ItemKindData.SplitButton:
                    commandId = CreateSplit(item, location);
                    break;
                case ItemKindData.ComboBox:
                    commandId = CreateCombo(item, location);
                    break;
                case ItemKindData.Label:
                    commandId = CreateLabel(item, location);
                    break;
                default:
                    commandId = EnsureAtomic(item, CommandKindData.Action, location);
                    break;
            }

            placement.CommandId = commandId;
            RecordPlacement(location, "Converted item to catalog placement '" + commandId + "'.");
            return placement;
        }

        private string EnsureAtomic(
            ItemDefData item,
            CommandKindData expectedKind,
            string location)
        {
            string id = !string.IsNullOrWhiteSpace(item.CommandId)
                ? item.CommandId
                : SuggestedId(item, expectedKind, location);

            CommandDefData? existing;
            if (_definitions.TryGetValue(id, out existing) && existing is not null)
            {
                if (existing.Kind == expectedKind)
                {
                    FillMissingPresentation(existing, item);
                    existing.IncludeInCommandList |= item.IncludeInCommandList;
                    return id;
                }

                if (existing.Kind == CommandKindData.Action &&
                    expectedKind == CommandKindData.Toggle &&
                    CanUpgradeActionToToggle(id))
                {
                    existing.Kind = CommandKindData.Toggle;
                    FillMissingPresentation(existing, item);
                    existing.IncludeInCommandList |= item.IncludeInCommandList;
                    _changes.Add(new LegacyMigrationChange
                    {
                        Kind = LegacyMigrationChangeKind.UpgradedCatalogEntry,
                        Location = location,
                        Message = "Upgraded catalog entry '" + id + "' from Action to Toggle.",
                    });
                    return id;
                }

                _diagnostics.Add(Diagnostic(
                    CatalogDiagnosticSeverity.Error,
                    CatalogDiagnosticCode.MigrationConflict,
                    "Legacy " + expectedKind + " item references catalog entry '" + id +
                    "' of kind " + existing.Kind + ".",
                    location,
                    id));
                return id;
            }

            var created = CreateDefinition(id, expectedKind, item);
            created.IncludeInCommandList = item.IncludeInCommandList;
            AddDefinition(created, location);
            return id;
        }

        private string CreatePopup(ItemDefData item, string location)
        {
            string proposed = !string.IsNullOrWhiteSpace(item.CommandId)
                ? item.CommandId
                : !string.IsNullOrWhiteSpace(item.Name)
                    ? item.Name
                    : Slug(item.Text) + ".menu";
            string id = UniqueId(proposed, "popup");
            var definition = CreateDefinition(id, CommandKindData.Popup, item);
            definition.ContentSource = item.ThemeList
                ? CommandContentSourceData.ThemeList
                : item.ToolbarList
                    ? CommandContentSourceData.ToolbarList
                    : CommandContentSourceData.Authored;
            CopyDropDownOptions(definition, item);
            AddDefinition(definition, location);

            if (definition.ContentSource == CommandContentSourceData.Authored)
            {
                for (int index = 0; index < item.Items.Count; index++)
                {
                    definition.Items.Add(ConvertItem(
                        item.Items[index],
                        location + "/Items[" + index + "]"));
                }
            }
            return id;
        }

        private string CreateSplit(ItemDefData item, string location)
        {
            string primaryId = EnsureAtomic(item, CommandKindData.Action, location + "/Primary");
            string proposed = !string.IsNullOrWhiteSpace(item.Name) &&
                              !string.Equals(item.Name, primaryId, StringComparison.Ordinal)
                ? item.Name
                : primaryId + ".split";
            string id = UniqueId(proposed, "split");
            var definition = CreateDefinition(id, CommandKindData.SplitButton, item);
            definition.PrimaryCommandId = primaryId;
            CopyDropDownOptions(definition, item);
            AddDefinition(definition, location);
            for (int index = 0; index < item.Items.Count; index++)
            {
                definition.Items.Add(ConvertItem(
                    item.Items[index],
                    location + "/Items[" + index + "]"));
            }
            return id;
        }

        private string CreateCombo(ItemDefData item, string location)
        {
            string proposed = !string.IsNullOrWhiteSpace(item.CommandId)
                ? item.CommandId
                : !string.IsNullOrWhiteSpace(item.Name)
                    ? item.Name
                    : Slug(item.Text) + ".combo";
            string id = UniqueId(proposed, "combo");
            var definition = CreateDefinition(id, CommandKindData.ComboBox, item);
            definition.ComboWidth = item.ComboWidth;
            definition.ComboItems = new List<string>(item.ComboItems);
            definition.IncludeInCommandList = item.IncludeInCommandList;
            AddDefinition(definition, location);
            return id;
        }

        private string CreateLabel(ItemDefData item, string location)
        {
            string proposed = !string.IsNullOrWhiteSpace(item.Name)
                ? item.Name
                : "label." + Slug(item.Text);
            string id = UniqueId(proposed, "label");
            var definition = CreateDefinition(id, CommandKindData.Label, item);
            AddDefinition(definition, location);
            return id;
        }

        private CommandDefData CreateDefinition(
            string id,
            CommandKindData kind,
            ItemDefData item)
            => new CommandDefData
            {
                Id = id,
                Kind = kind,
                Text = item.Text,
                ImageKey = item.ImageKey,
                ImagePath = item.ImagePath,
                Shortcut = item.Shortcut,
                DisplayStyle = item.DisplayStyle,
            };

        private static void CopyDropDownOptions(
            CommandDefData definition,
            ItemDefData item)
        {
            definition.TearOff = item.TearOff;
            definition.TearOffTitle = item.TearOffTitle;
            definition.PaletteColumns = item.PaletteColumns;
            definition.IncludeInCommandList = item.IncludeInCommandList;
        }

        private void AddDefinition(CommandDefData definition, string location)
        {
            _snapshot.Commands.Add(definition);
            _definitions[definition.Id] = definition;
            _usedIds.Add(definition.Id);
            _changes.Add(new LegacyMigrationChange
            {
                Kind = LegacyMigrationChangeKind.CreatedCatalogEntry,
                Location = location,
                Message = "Created " + definition.Kind + " catalog entry '" +
                          definition.Id + "'.",
            });
        }

        private void RecordPlacement(string location, string message)
            => _changes.Add(new LegacyMigrationChange
            {
                Kind = LegacyMigrationChangeKind.ConvertedPlacement,
                Location = location,
                Message = message,
            });

        private void FillMissingPresentation(CommandDefData command, ItemDefData item)
        {
            if (string.IsNullOrWhiteSpace(command.Text))
                command.Text = item.Text;
            if (string.IsNullOrWhiteSpace(command.ImageKey))
                command.ImageKey = item.ImageKey;
            if (string.IsNullOrWhiteSpace(command.ImagePath))
                command.ImagePath = item.ImagePath;
            if (command.Shortcut == System.Windows.Forms.Keys.None)
                command.Shortcut = item.Shortcut;
        }

        private bool CanUpgradeActionToToggle(string id)
        {
            HashSet<ItemKindData>? kinds;
            return !_legacyKinds.TryGetValue(id, out kinds) || kinds is null ||
                   kinds.All(kind => kind == ItemKindData.ToggleButton);
        }

        private void GatherLegacyKinds(IEnumerable<ItemDefData> items)
        {
            foreach (var item in items)
            {
                if (!string.IsNullOrWhiteSpace(item.CommandId) &&
                    (item.Kind == ItemKindData.Button ||
                     item.Kind == ItemKindData.ToggleButton ||
                     item.Kind == ItemKindData.SplitButton))
                {
                    HashSet<ItemKindData>? kinds;
                    if (!_legacyKinds.TryGetValue(item.CommandId, out kinds) ||
                        kinds is null)
                    {
                        kinds = new HashSet<ItemKindData>();
                        _legacyKinds.Add(item.CommandId, kinds);
                    }
                    // A split's primary region has ordinary action semantics.
                    kinds.Add(item.Kind == ItemKindData.SplitButton
                        ? ItemKindData.Button
                        : item.Kind);
                }
                GatherLegacyKinds(item.Items);
            }
        }

        private string SuggestedId(
            ItemDefData item,
            CommandKindData kind,
            string location)
        {
            if (!string.IsNullOrWhiteSpace(item.Name))
                return UniqueId(item.Name, kind.ToString().ToLowerInvariant());

            string slug = Slug(item.Text);
            if (string.IsNullOrWhiteSpace(slug))
                slug = Slug(location);
            string prefix = kind == CommandKindData.Toggle ? "toggle" : "command";
            return UniqueId(prefix + "." + slug, prefix);
        }

        private string UniqueId(string proposed, string fallback)
        {
            string baseId = string.IsNullOrWhiteSpace(proposed)
                ? string.Empty
                : proposed.Trim();
            if (string.IsNullOrWhiteSpace(baseId))
                baseId = fallback;
            if (!_usedIds.Contains(baseId))
                return baseId;
            for (int suffix = 2; ; suffix++)
            {
                string candidate = baseId + "." + suffix;
                if (!_usedIds.Contains(candidate))
                    return candidate;
            }
        }

        private static string Slug(string text)
        {
            if (string.IsNullOrWhiteSpace(text))
                return string.Empty;

            var builder = new StringBuilder();
            bool pendingSeparator = false;
            foreach (char character in text.Replace("&", string.Empty))
            {
                if (char.IsLetterOrDigit(character))
                {
                    if (pendingSeparator && builder.Length > 0)
                        builder.Append('.');
                    builder.Append(char.ToLowerInvariant(character));
                    pendingSeparator = false;
                }
                else if (character == '.' || character == '-' ||
                         character == '_' || char.IsWhiteSpace(character))
                {
                    pendingSeparator = true;
                }
            }
            return builder.ToString();
        }
    }
}
