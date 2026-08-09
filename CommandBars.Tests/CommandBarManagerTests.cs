using CommandBars;
using CommandBars.Design;
using CommandBars.Model;
using Xunit;

namespace CommandBars.Tests;

public class CommandBarManagerTests
{
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
