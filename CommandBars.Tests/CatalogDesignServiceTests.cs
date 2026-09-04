using CommandBars.Design;
using CommandBars.Model;
using System.ComponentModel;
using Proto = CommandBars.Designer.Protocol;
using Xunit;

namespace CommandBars.Tests;

public class CatalogDesignServiceTests
{
    [Fact]
    public void CommandPropertySurfaceTracksSemanticKind()
    {
        static HashSet<string> Properties(Proto.CommandKindData kind)
        {
            var command = new Proto.CommandDefData { Kind = kind };
            return TypeDescriptor.GetProperties(command)
                .Cast<PropertyDescriptor>()
                .Select(property => property.Name)
                .ToHashSet(StringComparer.Ordinal);
        }

        var action = Properties(Proto.CommandKindData.Action);
        var toggle = Properties(Proto.CommandKindData.Toggle);
        var popup = Properties(Proto.CommandKindData.Popup);
        var split = Properties(Proto.CommandKindData.SplitButton);
        var combo = Properties(Proto.CommandKindData.ComboBox);
        var label = Properties(Proto.CommandKindData.Label);

        Assert.Contains(nameof(Proto.CommandDefData.Shortcut), action);
        Assert.DoesNotContain(nameof(Proto.CommandDefData.InitialChecked), action);
        Assert.Contains(nameof(Proto.CommandDefData.InitialChecked), toggle);
        Assert.Contains(nameof(Proto.CommandDefData.ContentSource), popup);
        Assert.Contains(nameof(Proto.CommandDefData.TearOff), popup);
        Assert.DoesNotContain(nameof(Proto.CommandDefData.PrimaryCommandId), popup);
        Assert.Contains(nameof(Proto.CommandDefData.PrimaryCommandId), split);
        Assert.DoesNotContain(nameof(Proto.CommandDefData.ContentSource), split);
        Assert.Contains(nameof(Proto.CommandDefData.ComboItems), combo);
        Assert.Contains(nameof(Proto.CommandDefData.ComboWidth), combo);
        Assert.DoesNotContain(nameof(Proto.CommandDefData.Shortcut), combo);
        Assert.DoesNotContain(nameof(Proto.CommandDefData.ImageKey), label);
        Assert.All(new[] { action, toggle, popup, split, combo, label }, properties =>
            Assert.DoesNotContain(nameof(Proto.CommandDefData.Items), properties));
    }

    [Fact]
    public void PlacementPropertySurfaceProtectsCatalogIdentity()
    {
        var properties = TypeDescriptor.GetProperties(
            new Proto.CommandPlacementData(),
            new Attribute[] { BrowsableAttribute.Yes });

        Assert.Null(properties[nameof(Proto.CommandPlacementData.Kind)]);
        Assert.True(properties[nameof(Proto.CommandPlacementData.CommandId)]!.IsReadOnly);
        Assert.NotNull(properties[nameof(Proto.CommandPlacementData.Priority)]);
        Assert.NotNull(properties[nameof(Proto.CommandPlacementData.UseCatalogDisplayStyle)]);
    }

    [Fact]
    public void DockHostDesignContextRoundTripsEdgeSnapshotAndImages()
    {
        var context = new Proto.DockHostDesignContextData
        {
            HasManager = true,
            Edge = Proto.DockEdgeData.Left,
            Snapshot = new Proto.DesignSnapshot
            {
                Bars = new List<Proto.BarDefData>
                {
                    new()
                    {
                        Name = "Drawing",
                        Dock = Proto.DockEdgeData.Left,
                        Placements = new List<Proto.CommandPlacementData>
                        {
                            new() { CommandId = "shape.line" },
                        },
                    },
                },
                Commands = new List<Proto.CommandDefData>
                {
                    new() { Id = "shape.line", Text = "Line" },
                },
                Images = new List<Proto.ImageEntryData>
                {
                    new() { Key = "line", Png = "preview" },
                },
            },
        };

        string json = Proto.DefinitionsSerializer.SerializeDockHostContext(context);
        var rebuilt = Proto.DefinitionsSerializer.DeserializeDockHostContext(json);

        Assert.True(rebuilt.HasManager);
        Assert.Equal(Proto.DockEdgeData.Left, rebuilt.Edge);
        Assert.Equal("Drawing", Assert.Single(rebuilt.Snapshot.Bars).Name);
        Assert.Equal("shape.line", Assert.Single(rebuilt.Snapshot.Commands).Id);
        Assert.Equal("line", Assert.Single(rebuilt.Snapshot.Images).Key);
    }

    [Fact]
    public void ValidateReportsIdentityReferenceTargetCycleAndLegacyProblems()
    {
        var snapshot = new Proto.DesignSnapshot();
        snapshot.Commands.Add(new Proto.CommandDefData());
        snapshot.Commands.Add(new Proto.CommandDefData
        {
            Id = "duplicate",
        });
        snapshot.Commands.Add(new Proto.CommandDefData
        {
            Id = "duplicate",
        });
        var first = new Proto.CommandDefData
        {
            Id = "popup.first",
            Kind = Proto.CommandKindData.Popup,
        };
        first.Items.Add(new Proto.CommandPlacementData { CommandId = "popup.second" });
        var second = new Proto.CommandDefData
        {
            Id = "popup.second",
            Kind = Proto.CommandKindData.Popup,
        };
        second.Items.Add(new Proto.CommandPlacementData { CommandId = "popup.first" });
        snapshot.Commands.Add(first);
        snapshot.Commands.Add(second);
        snapshot.Commands.Add(new Proto.CommandDefData
        {
            Id = "split",
            Kind = Proto.CommandKindData.SplitButton,
            PrimaryCommandId = "missing.primary",
        });
        snapshot.Bars.Add(new Proto.BarDefData
        {
            Name = "MenuBar",
            BarType = Proto.BarKind.MenuBar,
            Placements = new List<Proto.CommandPlacementData>
            {
                new() { CommandId = "missing.command" },
                new() { Kind = Proto.CommandPlacementKindData.Separator },
            },
            Items = new List<Proto.ItemDefData>
            {
                new() { Kind = Proto.ItemKindData.Button, Text = "Legacy" },
            },
        });

        var result = Proto.CatalogDesignService.Validate(snapshot);

        Assert.False(result.IsValid);
        Assert.Contains(result.Diagnostics,
            diagnostic => diagnostic.Code == Proto.CatalogDiagnosticCode.EmptyId);
        Assert.Contains(result.Diagnostics,
            diagnostic => diagnostic.Code == Proto.CatalogDiagnosticCode.DuplicateId);
        Assert.Contains(result.Diagnostics,
            diagnostic => diagnostic.Code == Proto.CatalogDiagnosticCode.MissingReference &&
                          diagnostic.CommandId == "missing.command");
        Assert.Contains(result.Diagnostics,
            diagnostic => diagnostic.Code == Proto.CatalogDiagnosticCode.IncompatiblePlacement);
        Assert.Contains(result.Diagnostics,
            diagnostic => diagnostic.Code == Proto.CatalogDiagnosticCode.ReferenceCycle);
        Assert.Contains(result.Diagnostics,
            diagnostic => diagnostic.Code == Proto.CatalogDiagnosticCode.LegacyItemsPresent &&
                          diagnostic.Severity == Proto.CatalogDiagnosticSeverity.Warning);

        var authoringResult = Proto.CatalogDesignService.ValidateCatalogFirst(snapshot);
        Assert.Contains(authoringResult.Diagnostics,
            diagnostic => diagnostic.Code == Proto.CatalogDiagnosticCode.LegacyItemsPresent &&
                          diagnostic.Severity == Proto.CatalogDiagnosticSeverity.Error);
    }

    [Fact]
    public void UsageIndexAndRenameCoverEveryReferenceShape()
    {
        var snapshot = new Proto.DesignSnapshot
        {
            Commands = new List<Proto.CommandDefData>
            {
                new() { Id = "file.save" },
                new()
                {
                    Id = "file.save.split",
                    Kind = Proto.CommandKindData.SplitButton,
                    PrimaryCommandId = "file.save",
                },
                new()
                {
                    Id = "file.menu",
                    Kind = Proto.CommandKindData.Popup,
                    Items = new List<Proto.CommandPlacementData>
                    {
                        new() { CommandId = "file.save" },
                    },
                },
            },
            Bars = new List<Proto.BarDefData>
            {
                new()
                {
                    Name = "Standard",
                    Placements = new List<Proto.CommandPlacementData>
                    {
                        new() { CommandId = "file.save" },
                    },
                    Items = new List<Proto.ItemDefData>
                    {
                        new() { CommandId = "file.save" },
                    },
                },
            },
        };

        var usages = Proto.CatalogDesignService.FindUsages(snapshot, "file.save");

        Assert.Equal(4, usages.Count);
        Assert.Contains(usages, usage => usage.Kind == Proto.CommandUsageKind.BarPlacement);
        Assert.Contains(usages, usage => usage.Kind == Proto.CommandUsageKind.CompoundPlacement);
        Assert.Contains(usages, usage => usage.Kind == Proto.CommandUsageKind.SplitPrimary);
        Assert.Contains(usages, usage => usage.Kind == Proto.CommandUsageKind.LegacyItem);

        int rewritten = Proto.CatalogDesignService.RenameCommand(
            snapshot, "file.save", "file.saveAs");

        Assert.Equal(4, rewritten);
        Assert.Contains(snapshot.Commands, command => command.Id == "file.saveAs");
        Assert.DoesNotContain(snapshot.Commands, command => command.Id == "file.save");
        Assert.All(
            Proto.CatalogDesignService.FindUsages(snapshot, "file.saveAs"),
            usage => Assert.Equal("file.saveAs", usage.CommandId));
        Assert.Empty(Proto.CatalogDesignService.FindUsages(snapshot, "file.save"));
        Assert.Throws<InvalidOperationException>(() =>
            Proto.CatalogDesignService.RenameCommand(
                snapshot, "file.saveAs", "file.menu"));
    }

    [Fact]
    public void RemoveIsGuardedAndCascadeRemovesDependentSplits()
    {
        var snapshot = new Proto.DesignSnapshot
        {
            Commands = new List<Proto.CommandDefData>
            {
                new() { Id = "file.new" },
                new()
                {
                    Id = "file.new.split",
                    Kind = Proto.CommandKindData.SplitButton,
                    PrimaryCommandId = "file.new",
                },
                new()
                {
                    Id = "file.menu",
                    Kind = Proto.CommandKindData.Popup,
                    Items = new List<Proto.CommandPlacementData>
                    {
                        new() { CommandId = "file.new" },
                        new() { CommandId = "file.new.split" },
                    },
                },
            },
            Bars = new List<Proto.BarDefData>
            {
                new()
                {
                    Name = "Standard",
                    Placements = new List<Proto.CommandPlacementData>
                    {
                        new() { CommandId = "file.new.split" },
                    },
                },
            },
        };

        Assert.Throws<InvalidOperationException>(() =>
            Proto.CatalogDesignService.RemoveCommand(snapshot, "file.new"));

        Assert.True(Proto.CatalogDesignService.RemoveCommand(
            snapshot, "file.new", removeUsages: true));

        Assert.DoesNotContain(snapshot.Commands, command => command.Id == "file.new");
        Assert.DoesNotContain(snapshot.Commands, command => command.Id == "file.new.split");
        var menu = Assert.Single(snapshot.Commands);
        Assert.Equal("file.menu", menu.Id);
        Assert.Empty(menu.Items);
        Assert.Empty(Assert.Single(snapshot.Bars).Placements);
        Assert.False(Proto.CatalogDesignService.RemoveCommand(snapshot, "file.new"));
    }

    [Fact]
    public void MigrationIsDryRunAndPreservesCompoundBehaviorAndImages()
    {
        var source = new Proto.DesignSnapshot
        {
            Commands = new List<Proto.CommandDefData>
            {
                new()
                {
                    Id = "file.new",
                    Text = "New",
                },
                new()
                {
                    Id = "format.bold",
                    Text = "Bold",
                },
            },
        };
        var file = new Proto.ItemDefData
        {
            Kind = Proto.ItemKindData.Popup,
            Name = "file.menu",
            Text = "&File",
            TearOff = true,
            ImagePath = "Icons/file.svg",
        };
        file.Items.Add(new Proto.ItemDefData
        {
            Kind = Proto.ItemKindData.Button,
            CommandId = "file.new",
            Text = "&New",
        });
        file.Items.Add(new Proto.ItemDefData
        {
            Kind = Proto.ItemKindData.Separator,
        });
        source.Bars.Add(new Proto.BarDefData
        {
            Name = "MenuBar",
            BarType = Proto.BarKind.MenuBar,
            Items = new List<Proto.ItemDefData> { file },
        });

        var split = new Proto.ItemDefData
        {
            Kind = Proto.ItemKindData.SplitButton,
            Name = "file.new.split",
            CommandId = "file.new",
            Text = "New",
        };
        split.Items.Add(new Proto.ItemDefData
        {
            Kind = Proto.ItemKindData.Button,
            Text = "Blank document",
        });
        var combo = new Proto.ItemDefData
        {
            Kind = Proto.ItemKindData.ComboBox,
            Name = "font.combo",
            Text = "Font",
            ComboWidth = 175,
            ComboItems = new List<string> { "Segoe UI", "Calibri" },
            IncludeInCommandList = true,
        };
        source.Bars.Add(new Proto.BarDefData
        {
            Name = "Formatting",
            Items = new List<Proto.ItemDefData>
            {
                new()
                {
                    Kind = Proto.ItemKindData.ToggleButton,
                    CommandId = "format.bold",
                    Text = "Bold",
                },
                split,
                combo,
            },
        });

        string sourceBefore = Proto.DefinitionsSerializer.Serialize(source);
        var plan = Proto.CatalogDesignService.CreateLegacyMigrationPlan(source);
        var repeatedPlan = Proto.CatalogDesignService.CreateLegacyMigrationPlan(source);

        Assert.Equal(sourceBefore, Proto.DefinitionsSerializer.Serialize(source));
        Assert.Equal(
            Proto.DefinitionsSerializer.Serialize(plan.MigratedSnapshot),
            Proto.DefinitionsSerializer.Serialize(repeatedPlan.MigratedSnapshot));
        Assert.True(plan.IsRequired);
        Assert.True(plan.CanApply);
        Assert.Equal(
            Proto.DesignSnapshot.CurrentSchemaVersion,
            plan.MigratedSnapshot.SchemaVersion);
        Assert.All(plan.MigratedSnapshot.Bars, bar => Assert.Empty(bar.Items));
        Assert.All(plan.MigratedSnapshot.Bars, bar => Assert.NotEmpty(bar.Placements));

        var migratedFile = Assert.Single(
            plan.MigratedSnapshot.Commands,
            command => command.Id == "file.menu");
        Assert.Equal(Proto.CommandKindData.Popup, migratedFile.Kind);
        Assert.Equal("Icons/file.svg", migratedFile.ImagePath);
        Assert.True(migratedFile.TearOff);
        Assert.Equal(2, migratedFile.Items.Count);
        Assert.Equal(
            Proto.CommandPlacementKindData.Separator,
            migratedFile.Items[1].Kind);

        var migratedSplit = Assert.Single(
            plan.MigratedSnapshot.Commands,
            command => command.Id == "file.new.split");
        Assert.Equal(Proto.CommandKindData.SplitButton, migratedSplit.Kind);
        Assert.Equal("file.new", migratedSplit.PrimaryCommandId);
        Assert.Single(migratedSplit.Items);

        var migratedCombo = Assert.Single(
            plan.MigratedSnapshot.Commands,
            command => command.Id == "font.combo");
        Assert.Equal(Proto.CommandKindData.ComboBox, migratedCombo.Kind);
        Assert.Equal(175, migratedCombo.ComboWidth);
        Assert.Equal(new[] { "Segoe UI", "Calibri" }, migratedCombo.ComboItems);
        Assert.True(migratedCombo.IncludeInCommandList);

        var bold = Assert.Single(
            plan.MigratedSnapshot.Commands,
            command => command.Id == "format.bold");
        Assert.Equal(Proto.CommandKindData.Toggle, bold.Kind);
        Assert.True(plan.Validation.IsValid);
        Assert.True(Proto.CatalogDesignService
            .ValidateCatalogFirst(plan.MigratedSnapshot).IsValid);
    }

    [Fact]
    public void MigrationReportsAmbiguousActionToggleConflict()
    {
        var source = new Proto.DesignSnapshot
        {
            Commands = new List<Proto.CommandDefData>
            {
                new() { Id = "shared", Kind = Proto.CommandKindData.Action },
            },
            Bars = new List<Proto.BarDefData>
            {
                new()
                {
                    Name = "Standard",
                    Items = new List<Proto.ItemDefData>
                    {
                        new()
                        {
                            Kind = Proto.ItemKindData.Button,
                            CommandId = "shared",
                        },
                        new()
                        {
                            Kind = Proto.ItemKindData.ToggleButton,
                            CommandId = "shared",
                        },
                    },
                },
            },
        };

        var plan = Proto.CatalogDesignService.CreateLegacyMigrationPlan(source);

        Assert.False(plan.CanApply);
        Assert.Contains(
            plan.MigrationDiagnostics,
            diagnostic => diagnostic.Code == Proto.CatalogDiagnosticCode.MigrationConflict);
    }

    [Fact]
    public void SplitCatalogEntryCanReuseSeparatePrimaryAction()
    {
        var manager = new CommandBarManager();
        manager.CommandDefinitions.Add(new CommandDefinition
        {
            Id = "file.new",
            Text = "New",
        });
        var split = new CommandDefinition
        {
            Id = "file.new.split",
            Kind = CommandDefinitionKind.SplitButton,
            PrimaryCommandId = "file.new",
        };
        split.Items.Add(new CommandPlacementDefinition
        {
            Kind = CommandPlacementKind.Separator,
        });
        manager.CommandDefinitions.Add(split);
        var toolbar = new ToolbarDefinition { Name = "Standard" };
        toolbar.Placements.Add(new CommandPlacementDefinition
        {
            CommandId = "file.new.split",
        });
        manager.BarDefinitions.Add(toolbar);

        manager.BuildFromDefinitions();

        var built = Assert.IsType<CommandBarSplitButton>(
            Assert.Single(Assert.Single(manager.Bars).Items));
        Assert.Same(manager.Commands["file.new"], built.Command);
        Assert.False(manager.Commands.Contains("file.new.split"));
    }
}
