using System.ComponentModel;
using System.Windows.Forms;
using CommandBars.Model;
using Xunit;

namespace CommandBars.Tests;

public class CommandTests
{
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Ctor_NullOrEmptyId_Throws(string? id)
    {
        Assert.Throws<ArgumentException>(() => new Command(id!));
    }

    [Fact]
    public void DisplayText_StripsSingleMnemonic()
    {
        var c = new Command("id") { Text = "Cu&t" };
        Assert.Equal("Cut", c.DisplayText);
    }

    [Fact]
    public void DisplayText_KeepsEscapedAmpersand()
    {
        var c = new Command("id") { Text = "Fish && Chips" };
        Assert.Equal("Fish & Chips", c.DisplayText);
    }

    [Fact]
    public void SettingText_RaisesPropertyChanged()
    {
        var c = new Command("id");
        var changed = new List<string?>();
        c.PropertyChanged += (_, e) => changed.Add(e.PropertyName);

        c.Text = "New";

        Assert.Contains(nameof(Command.Text), changed);
    }

    [Fact]
    public void SettingSameValue_DoesNotRaisePropertyChanged()
    {
        var c = new Command("id") { Text = "Same" };
        var raised = false;
        c.PropertyChanged += (_, _) => raised = true;

        c.Text = "Same";

        Assert.False(raised);
    }

    [Fact]
    public void Perform_WhenDisabled_ReturnsFalseAndDoesNotInvoke()
    {
        var invoked = false;
        var c = new Command("id") { Enabled = false, ExecuteHandler = _ => invoked = true };

        var ran = c.Perform();

        Assert.False(ran);
        Assert.False(invoked);
    }

    [Fact]
    public void Perform_InvokesHandlerAndRaisesEventsInOrder()
    {
        var sequence = new List<string>();
        var c = new Command("id");
        c.Executing += (_, _) => sequence.Add("executing");
        c.ExecuteHandler = _ => sequence.Add("handler");
        c.Executed += (_, _) => sequence.Add("executed");

        var ran = c.Perform();

        Assert.True(ran);
        Assert.Equal(new[] { "executing", "handler", "executed" }, sequence);
    }

    [Fact]
    public void Perform_WhenExecutingCancels_SkipsHandlerAndExecuted()
    {
        var sequence = new List<string>();
        var c = new Command("id");
        c.Executing += (_, ctx) => { sequence.Add("executing"); ctx.Cancel = true; };
        c.ExecuteHandler = _ => sequence.Add("handler");
        c.Executed += (_, _) => sequence.Add("executed");

        var ran = c.Perform();

        Assert.False(ran);
        Assert.Equal(new[] { "executing" }, sequence);
    }

    [Fact]
    public void CanExecute_RespectsHandler()
    {
        var c = new Command("id") { CanExecuteHandler = _ => false };
        Assert.False(c.CanExecute());
        Assert.False(c.Perform());
    }

    [Fact]
    public void Perform_PassesParameterThrough()
    {
        object? seen = null;
        var c = new Command("id") { ExecuteHandler = ctx => seen = ctx.Parameter };

        c.Perform("payload");

        Assert.Equal("payload", seen);
    }

    [Fact]
    public void Shortcut_RoundTrips()
    {
        var c = new Command("id") { Shortcut = Keys.Control | Keys.S };
        Assert.Equal(Keys.Control | Keys.S, c.Shortcut);
    }

    [Fact]
    public void Command_ImplementsINotifyPropertyChanged()
    {
        Assert.IsAssignableFrom<INotifyPropertyChanged>(new Command("id"));
    }

    [Fact]
    public void Perform_WhenCheckable_TogglesCheckedState()
    {
        var c = new Command("id") { IsCheckable = true };
        Assert.Equal(CommandCheckState.Unchecked, c.Checked);
        c.Perform();
        Assert.Equal(CommandCheckState.Checked, c.Checked);
        c.Perform();
        Assert.Equal(CommandCheckState.Unchecked, c.Checked);
    }

    [Fact]
    public void Perform_WhenNotCheckable_LeavesCheckedAlone()
    {
        var c = new Command("id");
        c.Perform();
        Assert.Equal(CommandCheckState.Unchecked, c.Checked);
    }

    [Fact]
    public void Image_RoundTripsAndRasterizesAtRequestedSize()
    {
        var c = new Command("id") { Image = new StubImageSource("cut.svg") };

        Assert.Equal("cut.svg", c.Image!.Key);
        using var bmp = c.Image.GetImage(24);
        Assert.Equal(24, bmp.Width);
    }
}
