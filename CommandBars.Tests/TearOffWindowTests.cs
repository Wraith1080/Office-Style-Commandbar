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

    [Fact]
    public void TearOffFromFloatingToolbar_IsOwnedByApplicationForm_NotTransientToolbar()
    {
        using var applicationForm = new Form();
        using var host = new DockHost();
        applicationForm.Controls.Add(host);
        var toolbar = new CommandBar("toolbar", CommandBarType.Toolbar)
        {
            Text = "Toolbar",
            Dock = DockState.Floating,
        };
        using var floating = new FloatingWindow(toolbar,
            new Office2003Renderer(), host, applicationForm);
        using var palette = CreatePalette("palette", floating);
        using var nested = CreatePalette("nested", palette);

        Assert.Same(applicationForm, palette.Owner);
        Assert.Same(applicationForm, nested.Owner);
    }

    private static TearOffWindow CreatePalette(string name, Form owner)
    {
        var source = new CommandBar(name, CommandBarType.Popup) { Text = name };
        var clone = new CommandBar(name + ".float", CommandBarType.Popup) { Text = name };
        return new TearOffWindow(clone, source, new Office2003Renderer(), null, owner);
    }
}
