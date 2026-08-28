using CommandBars;
using CommandBars.Design;
using CommandBars.Controls;
using CommandBars.Model;
using CommandBars.Rendering;
using System.Reflection;
using System.Text;
using Xunit;

namespace CommandBars.Tests;

public class CommandBarManagerTests
{
    [Fact]
    public void ThemeRegistry_IsSeededAndSupportsReplacementAndRemoval()
    {
        var mgr = new CommandBarManager();
        Assert.Equal(6, mgr.Themes.Count);
        Assert.Equal(CommandBarThemeKeys.Office2003, mgr.ActiveThemeKey);

        var first = new OfficeXPRenderer();
        mgr.RegisterTheme(CommandBarThemeKeys.Office2003, "Replacement", () => first);

        Assert.Same(first, mgr.Renderer);
        Assert.Equal("Replacement", mgr.Themes.Single(t => t.Key == CommandBarThemeKeys.Office2003).Text);
        Assert.True(mgr.RemoveTheme(CommandBarThemeKeys.Office2003));
        Assert.Null(mgr.ActiveThemeKey);
        Assert.Same(first, mgr.Renderer);
        Assert.False(mgr.RemoveTheme(CommandBarThemeKeys.Office2003));
    }

    [Fact]
    public void ApplyTheme_UsesFreshFactoryAndPreservesEnumForCustomTheme()
    {
        var mgr = new CommandBarManager();
        mgr.Theme = CommandBarTheme.Dark;
        int created = 0;
        mgr.RegisterTheme("app.custom", "Custom", () =>
        {
            created++;
            return new OfficeXPRenderer();
        });

        Assert.True(mgr.ApplyTheme("app.custom"));
        var first = mgr.Renderer;
        Assert.True(mgr.ApplyTheme("app.custom"));

        Assert.Equal(2, created);
        Assert.NotSame(first, mgr.Renderer);
        Assert.Equal("app.custom", mgr.ActiveThemeKey);
        Assert.Equal(CommandBarTheme.Dark, mgr.Theme);
        Assert.False(mgr.ApplyTheme("missing"));
    }

    [Fact]
    public void UnknownSavedTheme_IsSafeAndAppliesWhenRegisteredLater()
    {
        var mgr = new CommandBarManager();
        using var layout = new MemoryStream(Encoding.UTF8.GetBytes(
            "{\"Version\":2,\"ThemeKey\":\"late.theme\",\"Bars\":[],\"Settings\":{},\"TearOffs\":[]}"));

        mgr.LoadLayout(layout);
        Assert.Equal(CommandBarThemeKeys.Office2003, mgr.ActiveThemeKey);
        Assert.IsType<Office2003Renderer>(mgr.Renderer);

        mgr.RegisterTheme("late.theme", "Late", () => new OfficeXPRenderer());
        Assert.Equal("late.theme", mgr.ActiveThemeKey);
        Assert.IsType<OfficeXPRenderer>(mgr.Renderer);
    }

    [Fact]
    public void ThemeList_GeneratesCheckedCommandsAndExecutesSelection()
    {
        var mgr = new CommandBarManager();
        var popup = new CommandBarPopupItem("Theme") { ThemeList = true };

        PreparePopup(mgr, popup);

        Assert.Equal(mgr.Themes.Count, popup.DropDown.Items.Count);
        var office2003 = Assert.IsType<CommandBarToggleButton>(popup.DropDown.Items[1]);
        var dark = Assert.IsType<CommandBarToggleButton>(popup.DropDown.Items[popup.DropDown.Items.Count - 1]);
        Assert.Equal(CommandCheckState.Checked, office2003.Command.Checked);
        Assert.Equal(CommandCheckState.Unchecked, dark.Command.Checked);

        Assert.True(dark.Command.Perform());
        Assert.Equal(CommandBarThemeKeys.Dark, mgr.ActiveThemeKey);
        PreparePopup(mgr, popup);
        dark = Assert.IsType<CommandBarToggleButton>(popup.DropDown.Items[popup.DropDown.Items.Count - 1]);
        Assert.Equal(CommandCheckState.Checked, dark.Command.Checked);
    }

    [Fact]
    public void ThemeList_IsMutuallyExclusiveAndDoesNotPersistGeneratedChildren()
    {
        var mgr = new CommandBarManager();
        var menu = mgr.AddBar("MenuBar", CommandBarType.MenuBar);
        var popup = menu.Items.AddPopup("Theme");
        popup.ToolbarList = true;
        popup.ThemeList = true;
        Assert.False(popup.ToolbarList);

        PreparePopup(mgr, popup);
        Assert.NotEmpty(popup.DropDown.Items);
        using var layout = new MemoryStream();
        mgr.SaveLayout(layout);
        layout.Position = 0;
        mgr.LoadLayout(layout);

        var rebuilt = Assert.IsType<CommandBarPopupItem>(Assert.Single(Assert.Single(mgr.Bars).Items));
        Assert.True(rebuilt.ThemeList);
        Assert.False(rebuilt.ToolbarList);
        Assert.Empty(rebuilt.DropDown.Items);
    }

    [Fact]
    public void ThemeEnum_RemainsABuiltInShortcutAfterRegistryIsCleared()
    {
        var mgr = new CommandBarManager();
        mgr.ClearThemes();

        mgr.Theme = CommandBarTheme.OfficeXP;

        Assert.Equal(CommandBarTheme.OfficeXP, mgr.Theme);
        Assert.Null(mgr.ActiveThemeKey);
        Assert.IsType<OfficeXPRenderer>(mgr.Renderer);
    }

    [Fact]
    public void Office2000_IsARegisteredBuiltInTheme()
    {
        var mgr = new CommandBarManager();

        Assert.Equal(CommandBarThemeKeys.Office2000, mgr.Themes[0].Key);
        mgr.Theme = CommandBarTheme.Office2000;

        Assert.Equal(CommandBarThemeKeys.Office2000, mgr.ActiveThemeKey);
        Assert.IsType<Office2000Renderer>(mgr.Renderer);
    }

    private static void PreparePopup(CommandBarManager manager, CommandBarPopupItem popup)
        => typeof(CommandBarManager)
            .GetMethod("PreparePopup", BindingFlags.Instance | BindingFlags.NonPublic)!
            .Invoke(manager, new object[] { popup });

    [Fact]
    public void AddBar_AddsAndSetsManager()
    {
        var mgr = new CommandBarManager();
        var bar = mgr.AddBar("Standard", CommandBarType.Toolbar);

        Assert.Single(mgr.Bars);
        Assert.Same(mgr, bar.Manager);
        Assert.Same(bar, mgr.FindBar("Standard"));
    }

    [Fact]
    public void AddBar_DuplicateName_Throws()
    {
        var mgr = new CommandBarManager();
        mgr.AddBar("Standard", CommandBarType.Toolbar);

        Assert.Throws<InvalidOperationException>(
            () => mgr.AddBar("Standard", CommandBarType.Toolbar));
    }

    [Fact]
    public void RemoveBar_RemovesAndClearsManager()
    {
        var mgr = new CommandBarManager();
        var bar = mgr.AddBar("Standard", CommandBarType.Toolbar);

        Assert.True(mgr.RemoveBar("Standard"));
        Assert.Null(bar.Manager);
        Assert.Empty(mgr.Bars);
        Assert.False(mgr.RemoveBar("Standard"));
    }

    [Fact]
    public void LayoutChanged_RaisedOnAddAndRemove()
    {
        var mgr = new CommandBarManager();
        var count = 0;
        mgr.LayoutChanged += (_, _) => count++;

        mgr.AddBar("A", CommandBarType.Toolbar);
        mgr.RemoveBar("A");

        Assert.Equal(2, count);
    }

    [Fact]
    public void Commands_AreSharedAcrossBars()
    {
        var mgr = new CommandBarManager();
        var cut = mgr.Commands.Register("edit.cut", c => c.Text = "Cu&t");

        var toolbar = mgr.AddBar("Standard", CommandBarType.Toolbar);
        var menu = mgr.AddBar("MenuBar", CommandBarType.MenuBar);
        var editMenu = menu.Items.AddPopup("&Edit");

        var toolButton = toolbar.Items.AddButton(cut);
        var menuButton = editMenu.DropDown.Items.AddButton(cut);

        cut.Enabled = false;

        Assert.False(toolButton.Enabled);
        Assert.False(menuButton.Enabled);
    }

    [Fact]
    public void NamedComboBoxes_SynchronizeSelectionAcrossToolbars()
    {
        var mgr = new CommandBarManager();
        var firstBar = mgr.AddBar("Formatting", CommandBarType.Toolbar);
        var first = new CommandBarComboBox { Name = "font.combo" };
        first.Items.Add("Segoe UI");
        first.Items.Add("Calibri");
        first.SelectedItem = "Calibri";
        first.Enabled = false;
        firstBar.Items.Add(first);

        var secondBar = mgr.AddBar("Custom", CommandBarType.Toolbar);
        var second = new CommandBarComboBox { Name = "font.combo" };
        second.Items.Add("Segoe UI");
        second.Items.Add("Calibri");
        second.SelectedItem = "Segoe UI";
        secondBar.Items.Add(second);

        Assert.Equal("Calibri", second.SelectedItem);
        Assert.False(second.Enabled);

        second.SelectedItem = "Segoe UI";
        second.Enabled = true;
        Assert.Equal("Segoe UI", first.SelectedItem);
        Assert.True(first.Enabled);

        secondBar.Items.Remove(second);
        first.SelectedItem = "Calibri";
        first.Enabled = false;
        Assert.Equal("Segoe UI", second.SelectedItem);
        Assert.True(second.Enabled);
    }

    [Fact]
    public void LoadLayout_PreservesLiveNamedComboEnabledState()
    {
        var mgr = new CommandBarManager();
        var bar = mgr.AddBar("Formatting", CommandBarType.Toolbar);
        var combo = new CommandBarComboBox { Name = "font.combo" };
        combo.Items.Add("Segoe UI");
        combo.SelectedItem = "Segoe UI";
        bar.Items.Add(combo);
        using var layout = new MemoryStream();
        mgr.SaveLayout(layout);

        combo.Enabled = false;
        layout.Position = 0;
        mgr.LoadLayout(layout);

        var rebuilt = Assert.IsType<CommandBarComboBox>(
            Assert.Single(Assert.Single(mgr.Bars).Items));
        Assert.False(rebuilt.Enabled);
    }

    [Fact]
    public void LoadLayout_PreservesItemOverflowPriority()
    {
        var mgr = new CommandBarManager();
        var command = mgr.Commands.Register("keep.visible", c => c.Text = "Keep visible");
        var item = mgr.AddBar("Standard", CommandBarType.Toolbar).Items.AddButton(command);
        item.Priority = 1;
        using var layout = new MemoryStream();
        mgr.SaveLayout(layout);

        layout.Position = 0;
        mgr.LoadLayout(layout);

        var rebuilt = Assert.IsType<CommandBarButton>(
            Assert.Single(Assert.Single(mgr.Bars).Items));
        Assert.Equal(1, rebuilt.Priority);
    }

    [Fact]
    public void IsCustomizing_DefaultsToFalse()
    {
        Assert.False(new CommandBarManager().IsCustomizing);
    }

    [Fact]
    public void LoadLayout_PreservesCodeOwnedPopupImages()
    {
        var mgr = new CommandBarManager();
        var bar = mgr.AddBar("Drawing", CommandBarType.Toolbar);
        var autoShapes = bar.Items.AddPopup("&AutoShapes");
        var autoShapesImage = new StubImageSource("autoshapes");
        autoShapes.Image = autoShapesImage;

        var lines = autoShapes.DropDown.Items.AddPopup("&Lines");
        var linesImage = new StubImageSource("lines");
        lines.Image = linesImage;

        using var layout = new MemoryStream();
        mgr.SaveLayout(layout);
        layout.Position = 0;
        mgr.LoadLayout(layout);

        var rebuiltBar = Assert.IsType<CommandBar>(Assert.Single(mgr.Bars));
        var rebuiltAutoShapes = Assert.IsType<CommandBarPopupItem>(Assert.Single(rebuiltBar.Items));
        var rebuiltLines = Assert.IsType<CommandBarPopupItem>(Assert.Single(rebuiltAutoShapes.DropDown.Items));
        Assert.Same(autoShapesImage, rebuiltAutoShapes.Image);
        Assert.Same(linesImage, rebuiltLines.Image);
    }

    [Fact]
    public void BuildFromDefinitions_RegistersFreshCompoundCustomizationItems()
    {
        var mgr = new CommandBarManager();
        var formatting = new ToolbarDefinition { Name = "Formatting", Text = "Formatting" };
        var combo = new ComboBoxDefinition
        {
            Name = "font.combo",
            Text = "Font",
            IncludeInCommandList = true,
        };
        combo.ComboItems.Add("Calibri");
        formatting.Items.Add(combo);

        var autoShapes = new PopupDefinition
        {
            Name = "autoshapes.menu",
            Text = "&AutoShapes",
            IncludeInCommandList = true,
        };
        autoShapes.Items.Add(new ButtonDefinition { CommandId = "shape.line", Text = "Line" });
        formatting.Items.Add(autoShapes);
        mgr.BarDefinitions.Add(formatting);

        mgr.BuildFromDefinitions();

        Assert.Equal(2, mgr.CustomizationItems.Count);
        var firstCombo = Assert.IsType<CommandBarComboBox>(mgr.CustomizationItems[0].CreateItem());
        var secondCombo = Assert.IsType<CommandBarComboBox>(mgr.CustomizationItems[0].CreateItem());
        Assert.NotSame(firstCombo, secondCombo);
        Assert.Equal("Calibri", Assert.Single(firstCombo.Items));

        var popup = Assert.IsType<CommandBarPopupItem>(mgr.CustomizationItems[1].CreateItem());
        Assert.IsType<CommandBarButton>(Assert.Single(popup.DropDown.Items));
    }

    [Fact]
    public void CustomizePalette_PrioritizesSplitFactoryOverGenericCommand()
    {
        var mgr = new CommandBarManager();
        mgr.Commands.Register("file.new", command => command.Text = "New");
        mgr.Commands.Register("new.document", command => command.Text = "Document");
        var toolbar = new ToolbarDefinition { Name = "Standard" };
        var splitDefinition = new SplitButtonDefinition
        {
            CommandId = "file.new",
            Text = "New",
            IncludeInCommandList = true,
        };
        splitDefinition.Items.Add(new ButtonDefinition
        {
            CommandId = "new.document",
            Text = "Document",
        });
        toolbar.Items.Add(splitDefinition);
        mgr.BarDefinitions.Add(toolbar);
        mgr.BuildFromDefinitions();

        var palette = CustomizeDialog.BuildPaletteItems(mgr, mgr.Commands);
        var entry = Assert.Single(palette, item => item.Id == "file.new");
        var split = Assert.IsType<CommandBarSplitButton>(entry.CreateItem());

        Assert.Same(mgr.Commands["file.new"], split.Command);
        Assert.IsType<CommandBarButton>(Assert.Single(split.DropDown.Items));
    }

    [Fact]
    public void BlankIdCustomizableToggleUsesOneStableCommandForAllCopies()
    {
        var mgr = new CommandBarManager();
        var toolbar = new ToolbarDefinition { Name = "Formatting" };
        toolbar.Items.Add(new ToggleButtonDefinition
        {
            Name = "bold.toggle",
            Text = "Bold",
            IncludeInCommandList = true,
        });
        mgr.BarDefinitions.Add(toolbar);
        mgr.BuildFromDefinitions();

        var original = Assert.IsType<CommandBarToggleButton>(
            Assert.Single(Assert.Single(mgr.Bars).Items));
        Assert.Equal("definition:Formatting:bold.toggle", original.Command.Id);
        var palette = CustomizeDialog.BuildPaletteItems(mgr, mgr.Commands);
        var entry = Assert.Single(palette, item => item.Id == original.Command.Id);
        var firstCopy = Assert.IsType<CommandBarToggleButton>(entry.CreateItem());
        var secondCopy = Assert.IsType<CommandBarToggleButton>(entry.CreateItem());

        Assert.Same(original.Command, firstCopy.Command);
        Assert.Same(original.Command, secondCopy.Command);
        original.Checked = true;
        original.Command.Enabled = false;
        Assert.True(firstCopy.Checked);
        Assert.True(secondCopy.Checked);
        Assert.False(firstCopy.Enabled);
        Assert.False(secondCopy.Enabled);
    }

    [Fact]
    public void NestedDropDowns_InheritOwningManager()
    {
        var mgr = new CommandBarManager();
        var bar = new CommandBar("MenuBar", CommandBarType.MenuBar);
        var view = bar.Items.AddPopup("&View");
        var toolbars = view.DropDown.Items.AddPopup("&Toolbars");

        mgr.Bars.Add(bar);

        Assert.Same(mgr, view.DropDown.Manager);
        Assert.Same(mgr, toolbars.DropDown.Manager);
    }

    [Fact]
    public void LoadLayout_PreservesDynamicToolbarListWithoutGeneratedChildren()
    {
        var mgr = new CommandBarManager();
        var menu = mgr.AddBar("MenuBar", CommandBarType.MenuBar);
        var toolbars = menu.Items.AddPopup("&Toolbars");
        toolbars.ToolbarList = true;
        toolbars.DropDown.Items.AddToggle(new Command("temporary") { Text = "Temporary" });

        using var layout = new MemoryStream();
        mgr.SaveLayout(layout);
        layout.Position = 0;
        mgr.LoadLayout(layout);

        var rebuiltMenu = Assert.Single(mgr.Bars);
        var rebuiltToolbars = Assert.IsType<CommandBarPopupItem>(Assert.Single(rebuiltMenu.Items));
        Assert.True(rebuiltToolbars.ToolbarList);
        Assert.Empty(rebuiltToolbars.DropDown.Items);
    }

    [Fact]
    public void LoadOlderLayout_MigratesConfiguredToolbarListAndDropsStaticChildren()
    {
        var configured = new CommandBarManager();
        var configuredMenu = configured.AddBar("MenuBar", CommandBarType.MenuBar);
        configuredMenu.Items.AddPopup("&Toolbars").ToolbarList = true;

        var old = new CommandBarManager();
        var oldMenu = old.AddBar("MenuBar", CommandBarType.MenuBar);
        var oldToolbars = oldMenu.Items.AddPopup("&Toolbars");
        oldToolbars.DropDown.Items.AddPopup("Static toolbar entry");
        using var layout = new MemoryStream();
        old.SaveLayout(layout);
        layout.Position = 0;

        configured.LoadLayout(layout);

        var rebuiltMenu = Assert.Single(configured.Bars);
        var rebuiltToolbars = Assert.IsType<CommandBarPopupItem>(Assert.Single(rebuiltMenu.Items));
        Assert.True(rebuiltToolbars.ToolbarList);
        Assert.Empty(rebuiltToolbars.DropDown.Items);
    }
}
