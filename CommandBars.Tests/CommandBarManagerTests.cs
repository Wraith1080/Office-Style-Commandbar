using CommandBars;
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
}
