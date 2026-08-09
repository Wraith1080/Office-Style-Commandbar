using System.ComponentModel;
using CommandBars.Design;
using CommandBars.Model;
using Xunit;
using Proto = CommandBars.Designer.Protocol;

namespace CommandBars.Tests;

public class DesignDefinitionTests
{
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

    private static bool HasProperty(ItemDefinition definition, string name)
        => TypeDescriptor.GetProperties(definition).Find(name, false) is not null;
}
