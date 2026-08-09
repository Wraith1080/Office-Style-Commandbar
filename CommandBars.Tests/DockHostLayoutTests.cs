using System.Windows.Forms;
using CommandBars.Controls;
using CommandBars.Model;
using Xunit;

namespace CommandBars.Tests;

public sealed class DockHostLayoutTests
{
    [Fact]
    public void AllocateDockedExtents_ShrinksLongestToolbarFirst()
    {
        int[] result = DockHost.AllocateDockedExtents(
            new[] { 300, 100 }, new[] { 30, 30 }, 350);

        Assert.Equal(new[] { 250, 100 }, result);
    }

    [Fact]
    public void AllocateDockedExtents_PreservesEveryUsableMinimum()
    {
        int[] result = DockHost.AllocateDockedExtents(
            new[] { 300, 100, 80 }, new[] { 32, 28, 24 }, 84);

        Assert.Equal(new[] { 32, 28, 24 }, result);
    }

    [Fact]
    public void AllocateDockedExtents_ImpossibleSizeDegradesBarsFairly()
    {
        int[] result = DockHost.AllocateDockedExtents(
            new[] { 300, 100 }, new[] { 30, 30 }, 40);

        Assert.Equal(new[] { 20, 20 }, result);
    }

    [Fact]
    public void Overflow_DropsFromRightToLeft_ButRetainsPriorityOneItem()
    {
        using var host = new DockHost { Size = new System.Drawing.Size(300, 40) };
        using var control = new CommandBarControl();
        host.Controls.Add(control);

        var bar = new CommandBar("priority", CommandBarType.Toolbar) { AllowFloat = false };
        var first = bar.Items.AddButton(new Command("first") { Text = "First ordinary item" });
        var second = bar.Items.AddButton(new Command("second") { Text = "Second ordinary item" });
        var keep = bar.Items.AddButton(new Command("keep") { Text = "Keep" });
        keep.Priority = 1;

        control.Bar = bar;
        control.Width = control.MinimumDockedExtent;

        Assert.Contains(first, control.OverflowItems);
        Assert.Contains(second, control.OverflowItems);
        Assert.DoesNotContain(keep, control.OverflowItems);
        Assert.True(keep.Bounds.Right <= control.Width);
    }
}
