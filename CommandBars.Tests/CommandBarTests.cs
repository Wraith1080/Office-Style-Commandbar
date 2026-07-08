using CommandBars.Model;
using Xunit;

namespace CommandBars.Tests;

public class CommandBarTests
{
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public void Ctor_EmptyName_Throws(string? name)
    {
        Assert.Throws<ArgumentException>(() => new CommandBar(name!, CommandBarType.Toolbar));
    }

    [Fact]
    public void NewToolbar_TextDefaultsToName()
    {
        var bar = new CommandBar("Standard", CommandBarType.Toolbar);
        Assert.Equal("Standard", bar.Text);
        Assert.Equal(IconSizes.Default, bar.IconSize);
    }

    [Theory]
    [InlineData(DockState.Top, BarOrientation.Horizontal)]
    [InlineData(DockState.Bottom, BarOrientation.Horizontal)]
    [InlineData(DockState.Left, BarOrientation.Vertical)]
    [InlineData(DockState.Right, BarOrientation.Vertical)]
    public void Orientation_FollowsDockEdge(DockState dock, BarOrientation expected)
    {
        var bar = new CommandBar("t", CommandBarType.Toolbar) { Dock = dock };
        Assert.Equal(expected, bar.Orientation);
    }

    [Fact]
    public void Popup_IsAlwaysVertical()
    {
        var bar = new CommandBar("m", CommandBarType.Popup) { Dock = DockState.Top };
        Assert.Equal(BarOrientation.Vertical, bar.Orientation);
    }

    [Fact]
    public void AddButton_SetsOwnerBar()
    {
        var reg = new CommandRegistry();
        var bar = new CommandBar("t", CommandBarType.Toolbar);
        var item = bar.Items.AddButton(reg.Register("a"));

        Assert.Same(bar, item.OwnerBar);
        Assert.Single(bar.Items);
    }

    [Fact]
    public void RemovingItem_ClearsOwnerBar()
    {
        var bar = new CommandBar("t", CommandBarType.Toolbar);
        var item = bar.Items.AddSeparator();

        bar.Items.Remove(item);

        Assert.Null(item.OwnerBar);
    }

    [Fact]
    public void ClearItems_ClearsAllOwners()
    {
        var reg = new CommandRegistry();
        var bar = new CommandBar("t", CommandBarType.Toolbar);
        var a = bar.Items.AddButton(reg.Register("a"));
        var b = bar.Items.AddButton(reg.Register("b"));

        bar.Items.Clear();

        Assert.Null(a.OwnerBar);
        Assert.Null(b.OwnerBar);
        Assert.Empty(bar.Items);
    }

    [Fact]
    public void AddingItemOwnedByAnotherBar_Throws()
    {
        var reg = new CommandRegistry();
        var bar1 = new CommandBar("one", CommandBarType.Toolbar);
        var bar2 = new CommandBar("two", CommandBarType.Toolbar);
        var item = bar1.Items.AddButton(reg.Register("a"));

        Assert.Throws<InvalidOperationException>(() => bar2.Items.Add(item));
    }

    [Fact]
    public void AddPopup_CreatesChildPopupBar()
    {
        var bar = new CommandBar("menubar", CommandBarType.MenuBar);
        var file = bar.Items.AddPopup("&File");

        Assert.Equal("File", file.DisplayText);
        Assert.Equal(CommandBarType.Popup, file.DropDown.BarType);
        Assert.Equal(BarOrientation.Vertical, file.DropDown.Orientation);
    }
}
