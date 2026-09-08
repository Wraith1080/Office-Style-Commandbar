using System.Drawing;
using CommandBars.Controls;
using CommandBars.Model;
using Xunit;

namespace CommandBars.Tests;

public class ComboPopupPlacementTests
{
    [Theory]
    [InlineData(DockState.Right, 197, 300)]
    [InlineData(DockState.Left, 453, 300)]
    [InlineData(DockState.Bottom, 400, 197)]
    [InlineData(DockState.Top, 400, 333)]
    [InlineData(DockState.Floating, 400, 333)]
    public void OpensTowardDockContent(DockState dock, int x, int y)
        => Assert.Equal(new Point(x, y), ComboDropDown.CalculateLocation(
            new Rectangle(400, 300, 50, 30), new Size(200, 100),
            new Rectangle(0, 0, 1000, 800), 3, dock));

    [Theory]
    [InlineData(DockState.Right, 10, 300, 63, 300)]
    [InlineData(DockState.Left, 950, 300, 747, 300)]
    [InlineData(DockState.Bottom, 400, 10, 400, 43)]
    [InlineData(DockState.Top, 400, 760, 400, 657)]
    public void FlipsWhenPreferredSideCannotFit(DockState dock, int ax, int ay, int x, int y)
        => Assert.Equal(new Point(x, y), ComboDropDown.CalculateLocation(
            new Rectangle(ax, ay, 50, 30), new Size(200, 100),
            new Rectangle(0, 0, 1000, 800), 3, dock));

    [Fact]
    public void OversizedPopupClampsOnNegativeCoordinateMonitor()
        => Assert.Equal(new Point(-1000, -800), ComboDropDown.CalculateLocation(
            new Rectangle(-500, -500, 50, 30), new Size(1200, 900),
            new Rectangle(-1000, -800, 1000, 800), 6, DockState.Right));
}
