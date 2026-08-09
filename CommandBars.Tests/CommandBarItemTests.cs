using CommandBars.Model;
using Xunit;

namespace CommandBars.Tests;

public class CommandBarItemTests
{
    [Fact]
    public void CommandItem_NullCommand_Throws()
    {
        Assert.Throws<ArgumentNullException>(() => new CommandBarButton(null!));
    }

    [Fact]
    public void Button_ReadsTextFromCommand()
    {
        var cmd = new Command("a") { Text = "Cu&t" };
        var button = new CommandBarButton(cmd);

        Assert.Equal("Cu&t", button.Text);
        Assert.Equal("Cut", button.DisplayText);
        Assert.Equal(CommandItemKind.Button, button.Kind);
        Assert.Equal(CommandItemDisplayStyle.ImageAndText, button.DisplayStyle);
    }

    [Fact]
    public void Toggle_ReflectsCommandCheckState()
    {
        var cmd = new Command("bold");
        var toggle = new CommandBarToggleButton(cmd);

        Assert.False(toggle.Checked);

        cmd.Checked = CommandCheckState.Checked;
        Assert.True(toggle.Checked);
    }

    [Fact]
    public void Toggle_SetChecked_UpdatesCommand()
    {
        var cmd = new Command("bold");
        var toggle = new CommandBarToggleButton(cmd) { Checked = true };

        Assert.Equal(CommandCheckState.Checked, cmd.Checked);

        toggle.Checked = false;
        Assert.Equal(CommandCheckState.Unchecked, cmd.Checked);
    }

    [Fact]
    public void SharedCommand_KeepsTwoItemsInSync()
    {
        var cmd = new Command("bold");
        var t1 = new CommandBarToggleButton(cmd);
        var t2 = new CommandBarToggleButton(cmd);

        t1.Checked = true;

        Assert.True(t2.Checked);
    }

    [Fact]
    public void CustomizeFactory_PreservesToggleCheckedAndEnabledState()
    {
        var command = new Command("format.bold")
        {
            IsCheckable = true,
            Checked = CommandCheckState.Checked,
            Enabled = false,
        };

        var entry = CommandBarCustomizationItem.FromCommand(command);
        var first = Assert.IsType<CommandBarToggleButton>(entry.CreateItem());
        var second = Assert.IsType<CommandBarToggleButton>(entry.CreateItem());

        Assert.Same(command, first.Command);
        Assert.Same(command, second.Command);
        Assert.True(first.Checked);
        Assert.True(second.Checked);
        Assert.False(first.Enabled);
        Assert.False(second.Enabled);

        first.Checked = false;
        command.Enabled = true;
        Assert.False(second.Checked);
        Assert.True(second.Enabled);
    }

    [Fact]
    public void CustomizeFactory_KeepsOrdinaryCommandAsButton()
    {
        var command = new Command("file.open");

        var item = CommandBarCustomizationItem.FromCommand(command).CreateItem();

        Assert.IsType<CommandBarButton>(item);
    }

    [Fact]
    public void Toggle_MarksCommandCheckable()
    {
        var cmd = new Command("bold");
        _ = new CommandBarToggleButton(cmd);
        Assert.True(cmd.IsCheckable);
    }

    [Fact]
    public void Toggle_LatchesThroughPerform()
    {
        var cmd = new Command("bold");
        var toggle = new CommandBarToggleButton(cmd);
        cmd.Perform();
        Assert.True(toggle.Checked);
        cmd.Perform();
        Assert.False(toggle.Checked);
    }

    [Fact]
    public void SplitButton_HasPopupDropDown()
    {
        var split = new CommandBarSplitButton(new Command("undo"));
        Assert.Equal(CommandItemKind.SplitButton, split.Kind);
        Assert.Equal(CommandBarType.Popup, split.DropDown.BarType);
    }

    [Fact]
    public void ComboBox_SelectionChange_RaisesEvent()
    {
        var combo = new CommandBarComboBox();
        combo.Items.Add("10");
        combo.Items.Add("12");
        var raised = 0;
        combo.SelectedItemChanged += (_, _) => raised++;

        combo.SelectedItem = "12";
        combo.SelectedItem = "12"; // same value, no event

        Assert.Equal(1, raised);
        Assert.Equal("12", combo.SelectedItem);
    }

    [Fact]
    public void ComboBox_EnabledChange_RaisesEventOnlyWhenValueChanges()
    {
        var combo = new CommandBarComboBox();
        var raised = 0;
        combo.EnabledChanged += (_, _) => raised++;

        combo.Enabled = false;
        combo.Enabled = false;

        Assert.False(combo.Enabled);
        Assert.Equal(1, raised);
    }

    [Fact]
    public void Kinds_AreDistinctPerType()
    {
        Assert.Equal(CommandItemKind.Separator, new CommandBarSeparator().Kind);
        Assert.Equal(CommandItemKind.Label, new CommandBarLabel("x").Kind);
        Assert.Equal(CommandItemKind.Popup, new CommandBarPopupItem("File").Kind);
        Assert.Equal(CommandItemKind.ComboBox, new CommandBarComboBox().Kind);
    }
}
