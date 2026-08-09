using System.Drawing;
using System.Windows.Forms;
using CommandBars.Controls;
using CommandBars.Model;
using Xunit;

namespace CommandBars.Tests;

public sealed class DockHostLayoutTests
{
    [Fact]
    public void CommandBarControl_ShowsToolTipsOverNonActivatingFloatingWindow()
    {
        using var control = new CommandBarControl();

        Assert.True(control.ToolTipsShowAlways);
    }

    [Fact]
    public void SubmenuArrowGeometry_ScalesWithDpi()
    {
        Point[] normal = CommandBarPopupWindow.SubmenuArrowPoints(new Rectangle(0, 0, 14, 30), 1f);
        Point[] doubleDpi = CommandBarPopupWindow.SubmenuArrowPoints(new Rectangle(0, 0, 28, 60), 2f);

        Assert.Equal((normal[2].X - normal[0].X) * 2, doubleDpi[2].X - doubleDpi[0].X);
        Assert.Equal((normal[1].Y - normal[0].Y) * 2, doubleDpi[1].Y - doubleDpi[0].Y);
    }

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
