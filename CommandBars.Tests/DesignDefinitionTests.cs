using System.ComponentModel;
using CommandBars.Controls;
using CommandBars.Design;
using CommandBars.Model;
using Xunit;
using Proto = CommandBars.Designer.Protocol;

namespace CommandBars.Tests;

public class DesignDefinitionTests
{
    [Fact]
    public void CatalogFirstMetadataHidesRawDesignerCollections()
    {
        static PropertyDescriptorCollection VisibleProperties(object value)
            => TypeDescriptor.GetProperties(
                value,
                new Attribute[] { BrowsableAttribute.Yes });

        var managerProperties = VisibleProperties(new CommandBarManager());
        Assert.NotNull(managerProperties[nameof(CommandBarManager.BarDefinitions)]);
        Assert.Null(managerProperties[nameof(CommandBarManager.CommandDefinitions)]);

        var bar = new ToolbarDefinition();
        Assert.Null(VisibleProperties(bar)[nameof(BarDefinition.Items)]);
        Assert.Null(VisibleProperties(bar)[nameof(BarDefinition.Placements)]);
        Assert.Equal(
            DesignerSerializationVisibility.Content,
            TypeDescriptor.GetProperties(bar)[nameof(BarDefinition.Items)]!
                .SerializationVisibility);
        Assert.Equal(
            DesignerSerializationVisibility.Content,
            TypeDescriptor.GetProperties(bar)[nameof(BarDefinition.Placements)]!
                .SerializationVisibility);

        var legacyPopup = new PopupDefinition();
        Assert.Null(VisibleProperties(legacyPopup)[nameof(ItemDefinition.Items)]);
        Assert.Equal(
            DesignerSerializationVisibility.Content,
            TypeDescriptor.GetProperties(legacyPopup)[nameof(ItemDefinition.Items)]!
                .SerializationVisibility);

        var catalogPopup = new CommandDefinition { Kind = CommandDefinitionKind.Popup };
        Assert.Null(VisibleProperties(catalogPopup)[nameof(CommandDefinition.Items)]);

        var runtimePlacement = new CommandPlacementDefinition();
        var placementProperties = VisibleProperties(runtimePlacement);
        Assert.Null(placementProperties[nameof(CommandPlacementDefinition.Kind)]);
        var runtimeCommandId = placementProperties[nameof(CommandPlacementDefinition.CommandId)]!;
        Assert.False(runtimeCommandId.IsReadOnly);
        runtimeCommandId.SetValue(runtimePlacement, "file.new");
        Assert.Equal("file.new", runtimePlacement.CommandId);
        Assert.True(TypeDescriptor.GetProperties(new Proto.CommandPlacementData())[
            nameof(Proto.CommandPlacementData.CommandId)]!.IsReadOnly);
    }

    [Fact]
    public void DockHostSmartTagEntryPointsAreHiddenAndNeverSerialized()
    {
        var properties = TypeDescriptor.GetProperties(new DockHost());
        var expected = new Dictionary<string, string>
        {
            [nameof(DockHost.DesignerAddToolbar)] = "DockHostAddToolbarEditor",
            [nameof(DockHost.DesignerAddMenuBar)] = "DockHostAddMenuBarEditor",
            [nameof(DockHost.DesignerAddCommands)] = "DockHostAddCommandsEditor",
            [nameof(DockHost.DesignerAddCommandsToBar)] = "DockHostAddCommandsToBarEditor",
            [nameof(DockHost.DesignerEditBars)] = "DockHostEditBarsEditor",
            [nameof(DockHost.DesignerEditCatalog)] = "DockHostEditCatalogEditor",
        };

        foreach (var pair in expected)
        {
            var property = Assert.IsAssignableFrom<PropertyDescriptor>(properties[pair.Key]);
            Assert.False(property.IsBrowsable);
            Assert.Equal(
                DesignerSerializationVisibility.Hidden,
                property.SerializationVisibility);
            var editor = Assert.IsType<EditorAttribute>(
                property.Attributes[typeof(EditorAttribute)]);
            Assert.Contains(pair.Value, editor.EditorTypeName, StringComparison.Ordinal);
        }

        var target = Assert.IsAssignableFrom<PropertyDescriptor>(
            properties[nameof(DockHost.DesignerTargetBarName)]);
        Assert.False(target.IsBrowsable);
        Assert.Equal(
            DesignerSerializationVisibility.Hidden,
            target.SerializationVisibility);
    }

    [Fact]
    public void Definition_AppliesOverflowPriority()
    {
        var definition = new ButtonDefinition { Text = "Keep", Priority = 1 };

        var item = Assert.IsType<CommandBarButton>(definition.Build(new CommandRegistry()));

        Assert.Equal(1, item.Priority);
        Assert.True(HasProperty(definition, nameof(ItemDefinition.Priority)));
    }

    [Fact]
    public void PopupDefinition_AppliesTearOffPaletteOptions()
    {
        var definition = new PopupDefinition
        {
            Text = "&AutoShapes",
            TearOff = true,
            TearOffTitle = "Drawing Shapes",
            PaletteColumns = 7,
        };

        var popup = Assert.IsType<CommandBarPopupItem>(
            definition.Build(new CommandRegistry()));

        Assert.True(popup.DropDown.AllowTearOff);
        Assert.Equal("Drawing Shapes", popup.DropDown.Text);
        Assert.Equal(7, popup.DropDown.PaletteColumns);
    }

    [Fact]
    public void PopupDefinition_AppliesDynamicToolbarListOption()
    {
        var definition = new PopupDefinition
        {
            Text = "&Toolbars",
            ToolbarList = true,
        };

        var popup = Assert.IsType<CommandBarPopupItem>(
            definition.Build(new CommandRegistry()));

        Assert.True(popup.ToolbarList);
        Assert.True(HasProperty(definition, nameof(ItemDefinition.ToolbarList)));
        Assert.False(HasProperty(new ButtonDefinition(), nameof(ItemDefinition.ToolbarList)));
    }

    [Fact]
    public void PopupDefinition_AppliesDynamicThemeListAndMutualExclusion()
    {
        var definition = new PopupDefinition { Text = "&Theme", ToolbarList = true };
        definition.Items.Add(new ButtonDefinition { Text = "authored" });
        definition.ThemeList = true;

        var popup = Assert.IsType<CommandBarPopupItem>(definition.Build(new CommandRegistry()));

        Assert.True(definition.ThemeList);
        Assert.False(definition.ToolbarList);
        Assert.True(popup.ThemeList);
        Assert.Empty(popup.DropDown.Items);
        Assert.True(HasProperty(definition, nameof(ItemDefinition.ThemeList)));
        Assert.False(HasProperty(definition, nameof(ItemDefinition.Items)));

        var data = new Proto.ItemDefData { Kind = Proto.ItemKindData.Popup, ToolbarList = true };
        data.ThemeList = true;
        Assert.True(data.ThemeList);
        Assert.False(data.ToolbarList);
        Assert.False(data.CanHaveChildren);
        Assert.Null(TypeDescriptor.GetProperties(data).Find(nameof(Proto.ItemDefData.Items), false));
    }

    [Fact]
    public void CompoundDefinitions_CanOptIntoCustomizeCommandList()
    {
        Assert.True(HasProperty(new ComboBoxDefinition(), nameof(ItemDefinition.IncludeInCommandList)));
        Assert.True(HasProperty(new PopupDefinition(), nameof(ItemDefinition.IncludeInCommandList)));
        Assert.False(HasProperty(new SeparatorDefinition(), nameof(ItemDefinition.IncludeInCommandList)));
    }

    [Fact]
    public void SplitDefinition_UsesItemTextAsDefaultPaletteTitle()
    {
        var definition = new SplitButtonDefinition
        {
            Text = "&Font Color",
            TearOff = true,
            PaletteColumns = -2,
        };

        var split = Assert.IsType<CommandBarSplitButton>(
            definition.Build(new CommandRegistry()));

        Assert.Equal("Font Color", split.DropDown.Text);
        Assert.Equal(0, split.DropDown.PaletteColumns);
    }

    [Fact]
    public void DefinitionPropertyGrid_ShowsOnlyRelevantPaletteProperties()
    {
        var button = new ButtonDefinition();
        Assert.False(HasProperty(button, nameof(ItemDefinition.TearOff)));
        Assert.False(HasProperty(button, nameof(ItemDefinition.TearOffTitle)));
        Assert.False(HasProperty(button, nameof(ItemDefinition.PaletteColumns)));

        var popup = new PopupDefinition();
        Assert.True(HasProperty(popup, nameof(ItemDefinition.TearOff)));
        Assert.False(HasProperty(popup, nameof(ItemDefinition.TearOffTitle)));
        Assert.True(HasProperty(popup, nameof(ItemDefinition.PaletteColumns)));

        popup.TearOff = true;
        Assert.True(HasProperty(popup, nameof(ItemDefinition.TearOffTitle)));
    }

    [Fact]
    public void ButtonPropertyGrid_HidesComboAndChildProperties()
    {
        var button = new ButtonDefinition();

        Assert.True(HasProperty(button, nameof(ItemDefinition.Name)));
        Assert.True(HasProperty(button, nameof(ItemDefinition.Shortcut)));
        Assert.False(HasProperty(button, nameof(ItemDefinition.ComboWidth)));
        Assert.False(HasProperty(button, nameof(ItemDefinition.ComboItems)));
        Assert.False(HasProperty(button, nameof(ItemDefinition.Items)));
    }

    [Fact]
    public void ComboPropertyGrid_ShowsOnlyComboSpecificProperties()
    {
        var combo = new ComboBoxDefinition();

        Assert.True(HasProperty(combo, nameof(ItemDefinition.Name)));
        Assert.True(HasProperty(combo, nameof(ItemDefinition.ComboWidth)));
        Assert.True(HasProperty(combo, nameof(ItemDefinition.ComboItems)));
        Assert.False(HasProperty(combo, nameof(ItemDefinition.CommandId)));
        Assert.False(HasProperty(combo, nameof(ItemDefinition.Shortcut)));
        Assert.False(HasProperty(combo, nameof(ItemDefinition.DisplayStyle)));
    }

    [Fact]
    public void SeparatorPropertyGrid_HidesUnusedPresentationProperties()
    {
        var separator = new SeparatorDefinition();

        Assert.True(HasProperty(separator, nameof(ItemDefinition.Name)));
        Assert.True(HasProperty(separator, nameof(ItemDefinition.Visible)));
        Assert.False(HasProperty(separator, nameof(ItemDefinition.Text)));
        Assert.False(HasProperty(separator, nameof(ItemDefinition.ImageKey)));
        Assert.False(HasProperty(separator, nameof(ItemDefinition.BeginGroup)));
    }

    [Fact]
    public void ClientButtonPropertyGrid_HidesComboPropertiesAndUsesKeysShortcut()
    {
        var button = new Proto.ItemDefData { Kind = Proto.ItemKindData.Button };
        var properties = TypeDescriptor.GetProperties(button);

        Assert.NotNull(properties.Find(nameof(Proto.ItemDefData.Name), false));
        Assert.NotNull(properties.Find(nameof(Proto.ItemDefData.Shortcut), false));
        Assert.Equal(typeof(System.Windows.Forms.Keys),
            properties[nameof(Proto.ItemDefData.Shortcut)]!.PropertyType);
        Assert.Null(properties.Find(nameof(Proto.ItemDefData.ComboWidth), false));
        Assert.Null(properties.Find(nameof(Proto.ItemDefData.ComboItems), false));
    }

    [Fact]
    public void ClientComboPropertyGrid_HidesCommandProperties()
    {
        var combo = new Proto.ItemDefData { Kind = Proto.ItemKindData.ComboBox };
        var properties = TypeDescriptor.GetProperties(combo);

        Assert.NotNull(properties.Find(nameof(Proto.ItemDefData.ComboWidth), false));
        Assert.NotNull(properties.Find(nameof(Proto.ItemDefData.ComboItems), false));
        Assert.Null(properties.Find(nameof(Proto.ItemDefData.CommandId), false));
        Assert.Null(properties.Find(nameof(Proto.ItemDefData.Shortcut), false));
        Assert.Null(properties.Find(nameof(Proto.ItemDefData.DisplayStyle), false));
    }

    [Fact]
    public void CatalogAction_DefaultKindBuildsSharedNonDestructiveCommand()
    {
        var manager = new CommandBarManager();
        var codeCommand = manager.Commands.Register("file.save", command =>
        {
            command.Text = "Code-owned Save";
            command.ExecuteHandler = _ => { };
        });
        manager.CommandDefinitions.Add(new CommandDefinition
        {
            Id = "file.save",
            Text = "Catalog Save",
            Shortcut = Keys.Control | Keys.S,
        });

        var first = Assert.IsType<CommandBarButton>(
            manager.CreateCatalogItem("file.save"));
        var second = Assert.IsType<CommandBarButton>(
            manager.CreateCatalogItem("file.save"));

        Assert.Same(codeCommand, first.Command);
        Assert.Same(first.Command, second.Command);
        Assert.Equal("Code-owned Save", first.Text);
        Assert.Equal(Keys.Control | Keys.S, first.Command.Shortcut);
        Assert.NotNull(first.Command.ExecuteHandler);
    }

    [Fact]
    public void CatalogToggleSharesInitialCheckedAndEnabledState()
    {
        var manager = new CommandBarManager();
        manager.CommandDefinitions.Add(new CommandDefinition
        {
            Id = "format.bold",
            Kind = CommandDefinitionKind.Toggle,
            Text = "&Bold",
            InitialChecked = CommandCheckState.Checked,
        });

        var first = Assert.IsType<CommandBarToggleButton>(
            manager.CreateCatalogItem("format.bold"));
        var second = Assert.IsType<CommandBarToggleButton>(
            manager.CreateCatalogItem("format.bold"));

        Assert.Same(first.Command, second.Command);
        Assert.True(first.Command.IsCheckable);
        Assert.True(first.Checked);
        second.Checked = false;
        first.Command.Enabled = false;
        Assert.False(first.Checked);
        Assert.False(second.Enabled);
    }

    [Fact]
    public void CatalogPopupBuildsReferencedChildrenAndPlacementOverrides()
    {
        var manager = new CommandBarManager();
        manager.CommandDefinitions.Add(new CommandDefinition
        {
            Id = "file.open",
            Text = "&Open",
            DisplayStyle = CommandItemDisplayStyle.ImageAndText,
        });
        var recent = new CommandDefinition
        {
            Id = "file.recent",
            Kind = CommandDefinitionKind.Popup,
            Text = "Recent",
        };
        recent.Items.Add(new CommandPlacementDefinition { CommandId = "file.open" });
        manager.CommandDefinitions.Add(recent);

        var file = new CommandDefinition
        {
            Id = "file.menu",
            Kind = CommandDefinitionKind.Popup,
            Text = "&File",
        };
        file.Items.Add(new CommandPlacementDefinition
        {
            CommandId = "file.open",
            Name = "open.placement",
            BeginGroup = true,
            Priority = 1,
            UseCatalogDisplayStyle = false,
            DisplayStyle = CommandItemDisplayStyle.TextOnly,
        });
        file.Items.Add(new CommandPlacementDefinition
        {
            Kind = CommandPlacementKind.Separator,
        });
        file.Items.Add(new CommandPlacementDefinition { CommandId = "file.recent" });
        manager.CommandDefinitions.Add(file);

        var popup = Assert.IsType<CommandBarPopupItem>(
            manager.CreateCatalogItem("file.menu"));

        Assert.Equal(3, popup.DropDown.Items.Count);
        var open = Assert.IsType<CommandBarButton>(popup.DropDown.Items[0]);
        Assert.Equal("open.placement", open.Name);
        Assert.True(open.BeginGroup);
        Assert.Equal(1, open.Priority);
        Assert.Equal(CommandItemDisplayStyle.TextOnly, open.DisplayStyle);
        Assert.IsType<CommandBarSeparator>(popup.DropDown.Items[1]);
        var nested = Assert.IsType<CommandBarPopupItem>(popup.DropDown.Items[2]);
        var nestedOpen = Assert.IsType<CommandBarButton>(Assert.Single(nested.DropDown.Items));
        Assert.Same(open.Command, nestedOpen.Command);
    }

    [Fact]
    public void CatalogPopupSupportsDynamicContentAndSplitPaletteOptions()
    {
        var manager = new CommandBarManager();
        var themes = new CommandDefinition
        {
            Id = "view.themes",
            Kind = CommandDefinitionKind.Popup,
            Text = "&Themes",
            ContentSource = CommandContentSource.ThemeList,
        };
        themes.Items.Add(new CommandPlacementDefinition { CommandId = "ignored" });
        manager.CommandDefinitions.Add(themes);

        manager.CommandDefinitions.Add(new CommandDefinition
        {
            Id = "color.red",
            Text = "Red",
        });
        var colors = new CommandDefinition
        {
            Id = "font.color",
            Kind = CommandDefinitionKind.SplitButton,
            Text = "Font Color",
            TearOff = true,
            TearOffTitle = "Colors",
            PaletteColumns = 5,
        };
        colors.Items.Add(new CommandPlacementDefinition { CommandId = "color.red" });
        manager.CommandDefinitions.Add(colors);

        var themePopup = Assert.IsType<CommandBarPopupItem>(
            manager.CreateCatalogItem("view.themes"));
        Assert.True(themePopup.ThemeList);
        Assert.False(themePopup.ToolbarList);
        Assert.Empty(themePopup.DropDown.Items);

        var split = Assert.IsType<CommandBarSplitButton>(
            manager.CreateCatalogItem("font.color"));
        Assert.True(split.DropDown.AllowTearOff);
        Assert.Equal("Colors", split.DropDown.Text);
        Assert.Equal(5, split.DropDown.PaletteColumns);
        Assert.IsType<CommandBarButton>(Assert.Single(split.DropDown.Items));
    }

    [Fact]
    public void CatalogComboUsesCanonicalIdForSharedState()
    {
        var manager = new CommandBarManager();
        var definition = new CommandDefinition
        {
            Id = "font.selector",
            Kind = CommandDefinitionKind.ComboBox,
            Text = "&Font",
            ComboWidth = 180,
        };
        definition.ComboItems.Add("Segoe UI");
        definition.ComboItems.Add("Calibri");
        manager.CommandDefinitions.Add(definition);

        var first = Assert.IsType<CommandBarComboBox>(
            manager.CreateCatalogItem("font.selector"));
        var second = Assert.IsType<CommandBarComboBox>(
            manager.CreateCatalogItem("font.selector"));
        manager.AddBar("Formatting", CommandBarType.Toolbar).Items.Add(first);
        manager.AddBar("Custom", CommandBarType.Toolbar).Items.Add(second);

        Assert.Equal("font.selector", first.Name);
        Assert.Equal(180, first.Width);
        Assert.Equal("Segoe UI", first.SelectedItem);
        second.SelectedItem = "Calibri";
        second.Enabled = false;
        Assert.Equal("Calibri", first.SelectedItem);
        Assert.False(first.Enabled);
    }

    [Fact]
    public void CatalogLabelBuildsReusableNonExecutableText()
    {
        var manager = new CommandBarManager();
        manager.CommandDefinitions.Add(new CommandDefinition
        {
            Id = "format.label",
            Kind = CommandDefinitionKind.Label,
            Text = "Formatting",
        });

        var label = Assert.IsType<CommandBarLabel>(
            manager.CreateCatalogItem("format.label"));

        Assert.Equal("Formatting", label.Text);
        Assert.False(manager.Commands.Contains("format.label"));
    }

    [Fact]
    public void CatalogMaterializerReportsMissingDuplicateAndCyclicReferences()
    {
        var missing = new CommandBarManager();
        var missingPopup = new CommandDefinition
        {
            Id = "missing.parent",
            Kind = CommandDefinitionKind.Popup,
        };
        missingPopup.Items.Add(new CommandPlacementDefinition { CommandId = "missing.child" });
        missing.CommandDefinitions.Add(missingPopup);
        Assert.Throws<KeyNotFoundException>(
            () => missing.CreateCatalogItem("missing.parent"));

        var duplicate = new CommandBarManager();
        duplicate.CommandDefinitions.Add(new CommandDefinition { Id = "same" });
        duplicate.CommandDefinitions.Add(new CommandDefinition { Id = "same" });
        var duplicateError = Assert.Throws<InvalidOperationException>(
            () => duplicate.CreateCatalogItem("same"));
        Assert.Contains("duplicate", duplicateError.Message, StringComparison.OrdinalIgnoreCase);

        var cyclic = new CommandBarManager();
        var first = new CommandDefinition
        {
            Id = "popup.first",
            Kind = CommandDefinitionKind.Popup,
        };
        first.Items.Add(new CommandPlacementDefinition { CommandId = "popup.second" });
        var second = new CommandDefinition
        {
            Id = "popup.second",
            Kind = CommandDefinitionKind.Popup,
        };
        second.Items.Add(new CommandPlacementDefinition { CommandId = "popup.first" });
        cyclic.CommandDefinitions.Add(first);
        cyclic.CommandDefinitions.Add(second);

        var cycleError = Assert.Throws<InvalidOperationException>(
            () => cyclic.CreateCatalogItem("popup.first"));
        Assert.Contains("popup.first -> popup.second -> popup.first", cycleError.Message);
    }

    [Fact]
    public void CatalogCompoundEntryProvidesCanonicalCustomizationFactory()
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
            Text = "New",
            IncludeInCommandList = true,
        };
        split.Items.Add(new CommandPlacementDefinition { CommandId = "file.new" });
        manager.CommandDefinitions.Add(split);

        manager.BuildFromDefinitions();

        var customization = Assert.Single(manager.CustomizationItems);
        Assert.Equal("file.new.split", customization.Id);
        var item = Assert.IsType<CommandBarSplitButton>(customization.CreateItem());
        Assert.IsType<CommandBarButton>(Assert.Single(item.DropDown.Items));
    }

    [Fact]
    public void ProtocolRoundTripPreservesRichCatalogShape()
    {
        var data = new Proto.CommandDefData
        {
            Id = "font.selector",
            Kind = Proto.CommandKindData.ComboBox,
            Text = "Font",
            ToolTip = "Choose a font",
            ComboWidth = 160,
            ComboItems = new List<string> { "Segoe UI", "Calibri" },
            IncludeInCommandList = true,
            Items = new List<Proto.CommandPlacementData>
            {
                new()
                {
                    CommandId = "font.child",
                    Priority = 1,
                    UseCatalogDisplayStyle = false,
                    DisplayStyle = Proto.ItemDisplayData.TextOnly,
                },
            },
        };
        var snapshot = new Proto.DesignSnapshot
        {
            Commands = new List<Proto.CommandDefData> { data },
        };

        string json = Proto.DefinitionsSerializer.Serialize(snapshot);
        var rebuilt = Assert.Single(Proto.DefinitionsSerializer.Deserialize(json).Commands);

        Assert.Equal(Proto.CommandKindData.ComboBox, rebuilt.Kind);
        Assert.Equal("Choose a font", rebuilt.ToolTip);
        Assert.Equal(160, rebuilt.ComboWidth);
        Assert.Equal(new[] { "Segoe UI", "Calibri" }, rebuilt.ComboItems);
        Assert.True(rebuilt.IncludeInCommandList);
        var placement = Assert.Single(rebuilt.Items);
        Assert.Equal("font.child", placement.CommandId);
        Assert.Equal(1, placement.Priority);
        Assert.False(placement.UseCatalogDisplayStyle);
        Assert.Equal(Proto.ItemDisplayData.TextOnly, placement.DisplayStyle);
    }

    [Fact]
    public void CatalogPlacementsBuildMenuToolbarAndLegacyItemsTogether()
    {
        var manager = new CommandBarManager();
        manager.Commands.Register("legacy.help", command => command.Text = "Legacy Help");
        manager.CommandDefinitions.Add(new CommandDefinition
        {
            Id = "file.save",
            Text = "&Save",
        });
        var file = new CommandDefinition
        {
            Id = "file.menu",
            Kind = CommandDefinitionKind.Popup,
            Text = "&File",
        };
        file.Items.Add(new CommandPlacementDefinition { CommandId = "file.save" });
        manager.CommandDefinitions.Add(file);
        var font = new CommandDefinition
        {
            Id = "font.selector",
            Kind = CommandDefinitionKind.ComboBox,
            Text = "Font",
        };
        font.ComboItems.Add("Segoe UI");
        manager.CommandDefinitions.Add(font);

        var menu = new MenuBarDefinition { Name = "MenuBar" };
        menu.Placements.Add(new CommandPlacementDefinition { CommandId = "file.menu" });
        manager.BarDefinitions.Add(menu);

        var toolbar = new ToolbarDefinition { Name = "Standard" };
        toolbar.Items.Add(new ButtonDefinition { CommandId = "legacy.help" });
        toolbar.Placements.Add(new CommandPlacementDefinition
        {
            CommandId = "file.save",
            Name = "save.placement",
            Priority = 1,
            UseCatalogDisplayStyle = false,
            DisplayStyle = CommandItemDisplayStyle.ImageOnly,
        });
        toolbar.Placements.Add(new CommandPlacementDefinition
        {
            Kind = CommandPlacementKind.Separator,
        });
        toolbar.Placements.Add(new CommandPlacementDefinition { CommandId = "font.selector" });
        manager.BarDefinitions.Add(toolbar);

        manager.BuildFromDefinitions();

        var builtMenu = manager.Bars.Single(bar => bar.Name == "MenuBar");
        var builtFile = Assert.IsType<CommandBarPopupItem>(Assert.Single(builtMenu.Items));
        var menuSave = Assert.IsType<CommandBarButton>(Assert.Single(builtFile.DropDown.Items));

        var builtToolbar = manager.Bars.Single(bar => bar.Name == "Standard");
        Assert.Equal(4, builtToolbar.Items.Count);
        Assert.Equal("Legacy Help",
            Assert.IsType<CommandBarButton>(builtToolbar.Items[0]).Text);
        var toolbarSave = Assert.IsType<CommandBarButton>(builtToolbar.Items[1]);
        Assert.Same(menuSave.Command, toolbarSave.Command);
        Assert.Equal("save.placement", toolbarSave.Name);
        Assert.Equal(1, toolbarSave.Priority);
        Assert.Equal(CommandItemDisplayStyle.ImageOnly, toolbarSave.DisplayStyle);
        Assert.IsType<CommandBarSeparator>(builtToolbar.Items[2]);
        Assert.Equal("font.selector",
            Assert.IsType<CommandBarComboBox>(builtToolbar.Items[3]).Name);
    }

    [Fact]
    public void CatalogPlacementRulesRejectIncompatibleTargets()
    {
        Assert.True(CommandPlacementRules.CanPlace(
            CommandDefinitionKind.Popup, CommandPlacementTarget.MenuBar));
        Assert.False(CommandPlacementRules.CanPlace(
            CommandDefinitionKind.Action, CommandPlacementTarget.MenuBar));
        Assert.True(CommandPlacementRules.CanPlace(
            CommandDefinitionKind.ComboBox, CommandPlacementTarget.Toolbar));
        Assert.False(CommandPlacementRules.CanPlace(
            CommandDefinitionKind.ComboBox, CommandPlacementTarget.DropDown));
        Assert.False(CommandPlacementRules.CanPlace(
            CommandDefinitionKind.SplitButton, CommandPlacementTarget.DropDown));

        var menuManager = new CommandBarManager();
        menuManager.CommandDefinitions.Add(new CommandDefinition
        {
            Id = "file.save",
            Text = "Save",
        });
        var menu = new MenuBarDefinition { Name = "MenuBar" };
        menu.Placements.Add(new CommandPlacementDefinition { CommandId = "file.save" });
        menuManager.BarDefinitions.Add(menu);

        var menuError = Assert.Throws<InvalidOperationException>(
            () => menuManager.BuildFromDefinitions());
        Assert.Contains("menu-bar root", menuError.Message);

        var dropDownManager = new CommandBarManager();
        dropDownManager.CommandDefinitions.Add(new CommandDefinition
        {
            Id = "font.selector",
            Kind = CommandDefinitionKind.ComboBox,
        });
        var popup = new CommandDefinition
        {
            Id = "format.menu",
            Kind = CommandDefinitionKind.Popup,
        };
        popup.Items.Add(new CommandPlacementDefinition { CommandId = "font.selector" });
        dropDownManager.CommandDefinitions.Add(popup);

        var dropDownError = Assert.Throws<InvalidOperationException>(
            () => dropDownManager.CreateCatalogItem("format.menu"));
        Assert.Contains("popup dropdown", dropDownError.Message);
    }

    [Fact]
    public void CatalogOwnedPresentationAndKindRefreshAcrossBuilds()
    {
        var manager = new CommandBarManager();
        var command = new CommandDefinition
        {
            Id = "format.bold",
            Text = "First",
            Shortcut = Keys.Control | Keys.B,
            DisplayStyle = CommandItemDisplayStyle.ImageAndText,
        };
        manager.CommandDefinitions.Add(command);
        var toolbar = new ToolbarDefinition { Name = "Formatting" };
        toolbar.Placements.Add(new CommandPlacementDefinition { CommandId = "format.bold" });
        manager.BarDefinitions.Add(toolbar);

        manager.BuildFromDefinitions();
        var first = Assert.IsType<CommandBarButton>(
            Assert.Single(Assert.Single(manager.Bars).Items));

        command.Text = "Second";
        command.Shortcut = Keys.Control | Keys.Shift | Keys.B;
        command.DisplayStyle = CommandItemDisplayStyle.TextOnly;
        command.Kind = CommandDefinitionKind.Toggle;
        manager.BuildFromDefinitions();

        var second = Assert.IsType<CommandBarToggleButton>(
            Assert.Single(Assert.Single(manager.Bars).Items));
        Assert.Same(first.Command, second.Command);
        Assert.Equal("Second", second.Text);
        Assert.Equal(Keys.Control | Keys.Shift | Keys.B, second.Command.Shortcut);
        Assert.Equal(CommandItemDisplayStyle.TextOnly, second.DisplayStyle);
        Assert.True(second.Command.IsCheckable);
    }

    [Fact]
    public void ApplicationOwnedPresentationStillWinsAcrossDefinitionBuilds()
    {
        var manager = new CommandBarManager();
        var applicationCommand = manager.Commands.Register("file.save", command =>
        {
            command.Text = "Application Save";
            command.Shortcut = Keys.F12;
        });
        var definition = new CommandDefinition
        {
            Id = "file.save",
            Text = "Catalog Save",
            Shortcut = Keys.Control | Keys.S,
        };
        manager.CommandDefinitions.Add(definition);
        var toolbar = new ToolbarDefinition { Name = "Standard" };
        toolbar.Placements.Add(new CommandPlacementDefinition { CommandId = "file.save" });
        manager.BarDefinitions.Add(toolbar);

        manager.BuildFromDefinitions();
        definition.Text = "Changed Catalog Save";
        definition.Shortcut = Keys.Control | Keys.Shift | Keys.S;
        manager.BuildFromDefinitions();

        var item = Assert.IsType<CommandBarButton>(
            Assert.Single(Assert.Single(manager.Bars).Items));
        Assert.Same(applicationCommand, item.Command);
        Assert.Equal("Application Save", item.Text);
        Assert.Equal(Keys.F12, item.Command.Shortcut);
    }

    [Fact]
    public void CatalogCommandBecomingNonExecutableIsRemovedFromRegistry()
    {
        var manager = new CommandBarManager();
        var definition = new CommandDefinition
        {
            Id = "view.item",
            Text = "Item",
        };
        manager.CommandDefinitions.Add(definition);

        manager.CreateCatalogItem("view.item");
        Assert.True(manager.Commands.Contains("view.item"));

        definition.Kind = CommandDefinitionKind.Popup;
        manager.CreateCatalogItem("view.item");

        Assert.False(manager.Commands.Contains("view.item"));
    }

    [Fact]
    public void DirectLegacyBuildRemainsAvailableButCanonicalPlacementNeedsManager()
    {
        var registry = new CommandRegistry();
        registry.Register("legacy", command => command.Text = "Legacy");
        var legacy = new ToolbarDefinition { Name = "Legacy" };
        legacy.Items.Add(new ButtonDefinition { CommandId = "legacy" });

        var built = legacy.Build(registry);
        Assert.IsType<CommandBarButton>(Assert.Single(built.Items));

        var canonical = new ToolbarDefinition { Name = "Canonical" };
        canonical.Placements.Add(new CommandPlacementDefinition { CommandId = "catalog" });
        var error = Assert.Throws<InvalidOperationException>(
            () => canonical.Build(registry));
        Assert.Contains("BuildFromDefinitions", error.Message);
    }

    [Fact]
    public void ProtocolRoundTripPreservesTopLevelCatalogPlacements()
    {
        var snapshot = new Proto.DesignSnapshot
        {
            Bars = new List<Proto.BarDefData>
            {
                new()
                {
                    Name = "Standard",
                    Placements = new List<Proto.CommandPlacementData>
                    {
                        new()
                        {
                            CommandId = "file.save",
                            Name = "save.placement",
                            Priority = 1,
                        },
                        new()
                        {
                            Kind = Proto.CommandPlacementKindData.Separator,
                        },
                    },
                },
            },
        };

        string json = Proto.DefinitionsSerializer.Serialize(snapshot);
        var bar = Assert.Single(Proto.DefinitionsSerializer.Deserialize(json).Bars);

        Assert.Equal(2, bar.Placements.Count);
        Assert.Equal("file.save", bar.Placements[0].CommandId);
        Assert.Equal("save.placement", bar.Placements[0].Name);
        Assert.Equal(1, bar.Placements[0].Priority);
        Assert.Equal(
            Proto.CommandPlacementKindData.Separator,
            bar.Placements[1].Kind);
    }

    private static bool HasProperty(ItemDefinition definition, string name)
        => TypeDescriptor.GetProperties(definition).Find(name, false) is not null;
}
