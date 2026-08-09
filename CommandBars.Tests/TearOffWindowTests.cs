using System.Windows.Forms;
using CommandBars.Controls;
using CommandBars.Model;
using CommandBars.Rendering;
using Xunit;

namespace CommandBars.Tests;

public sealed class TearOffWindowTests
{
    [Fact]
    public void NestedTearOff_IsOwnedByApplicationForm_NotParentPalette()
    {
        using var applicationForm = new Form();
        using var parent = CreatePalette("parent", applicationForm);
        using var child = CreatePalette("child", parent);

        Assert.Same(applicationForm, parent.Owner);
        Assert.Same(applicationForm, child.Owner);
    }

    private static TearOffWindow CreatePalette(string name, Form owner)
    {
        var source = new CommandBar(name, CommandBarType.Popup) { Text = name };
        var clone = new CommandBar(name + ".float", CommandBarType.Popup) { Text = name };
        return new TearOffWindow(clone, source, new Office2003Renderer(), null, owner);
    }
}
