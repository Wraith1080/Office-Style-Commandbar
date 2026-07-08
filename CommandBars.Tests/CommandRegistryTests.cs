using CommandBars.Model;
using Xunit;

namespace CommandBars.Tests;

public class CommandRegistryTests
{
    [Fact]
    public void Register_AddsAndReturnsCommand()
    {
        var reg = new CommandRegistry();
        var c = reg.Register(new Command("a"));

        Assert.Equal("a", c.Id);
        Assert.Equal(1, reg.Count);
        Assert.True(reg.Contains("a"));
    }

    [Fact]
    public void Register_WithConfigure_SetsProperties()
    {
        var reg = new CommandRegistry();
        var c = reg.Register("save", x => { x.Text = "&Save"; x.Enabled = false; });

        Assert.Equal("&Save", c.Text);
        Assert.False(c.Enabled);
        Assert.Same(c, reg["save"]);
    }

    [Fact]
    public void Register_DuplicateId_Throws()
    {
        var reg = new CommandRegistry();
        reg.Register("dup");
        Assert.Throws<InvalidOperationException>(() => reg.Register("dup"));
    }

    [Fact]
    public void Get_MissingId_Throws()
    {
        var reg = new CommandRegistry();
        Assert.Throws<KeyNotFoundException>(() => reg.Get("nope"));
    }

    [Fact]
    public void TryGet_ReturnsFalseWhenMissing()
    {
        var reg = new CommandRegistry();
        Assert.False(reg.TryGet("nope", out _));
    }

    [Fact]
    public void GetOrAdd_ReturnsExistingInstance()
    {
        var reg = new CommandRegistry();
        var first = reg.Register("x");
        var second = reg.GetOrAdd("x", c => c.Text = "should not apply");

        Assert.Same(first, second);
        Assert.Equal(1, reg.Count);
    }

    [Fact]
    public void Remove_DeletesCommand()
    {
        var reg = new CommandRegistry();
        reg.Register("x");

        Assert.True(reg.Remove("x"));
        Assert.False(reg.Contains("x"));
        Assert.False(reg.Remove("x"));
    }

    [Fact]
    public void Clear_EmptiesRegistry()
    {
        var reg = new CommandRegistry();
        reg.Register("a");
        reg.Register("b");

        reg.Clear();

        Assert.Equal(0, reg.Count);
    }

    [Fact]
    public void Enumeration_YieldsAllCommands()
    {
        var reg = new CommandRegistry();
        reg.Register("a");
        reg.Register("b");

        var ids = reg.Select(c => c.Id).OrderBy(s => s).ToArray();

        Assert.Equal(new[] { "a", "b" }, ids);
    }
}
