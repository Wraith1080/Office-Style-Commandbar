using System.Drawing;
using CommandBars.Imaging;
using CommandBars.Rendering;
using Xunit;

namespace CommandBars.Tests;

public class DarkIconRenderingTests
{
    [Theory]
    [InlineData(1f)]
    [InlineData(1.5f)]
    [InlineData(2f)]
    [InlineData(3f)]
    public void OutlineScalesWithDpiAndPreservesArtwork(float scale)
    {
        int size = (int)(16 * scale);
        int inset = (int)(4 * scale);
        var renderer = new DarkRenderer { Scale = scale };
        using var icon = new Bitmap(size, size);
        using (var g = Graphics.FromImage(icon))
        {
            g.FillRectangle(Brushes.Black, inset, inset, size - 2 * inset, size - 2 * inset);
            g.FillRectangle(Brushes.CornflowerBlue, inset + 2, inset + 2, 2, 2);
        }
        using var result = Render(renderer, icon);
        int edge = 8 + inset;
        int middle = 8 + size / 2;
        int reach = (int)Math.Ceiling(scale);

        Assert.True(result.GetPixel(edge - reach, middle).R > renderer.Colors.MenuBackground.R);
        Assert.Equal(renderer.Colors.MenuBackground.ToArgb(), result.GetPixel(edge - reach - 1, middle).ToArgb());
        Assert.Equal(Color.Black.ToArgb(), result.GetPixel(edge, middle).ToArgb());
        Assert.Equal(Color.CornflowerBlue.ToArgb(), result.GetPixel(edge + 2, edge + 2).ToArgb());
        Assert.Equal(0, icon.GetPixel(inset - 1, size / 2).A);
    }

    [Theory]
    [InlineData(RenderState.Normal)]
    [InlineData(RenderState.Hot)]
    [InlineData(RenderState.Pressed)]
    [InlineData(RenderState.Checked)]
    public void SvgIconsHaveMatchingToolbarAndMenuContours(RenderState state)
    {
        var source = SvgImageSource.FromString("<svg xmlns='http://www.w3.org/2000/svg' width='16' height='16'><path d='M4 4h8v8H4z' fill='#202020'/></svg>");
        using var icon = source.GetImage(16);
        var renderer = new DarkRenderer();
        using var menu = Render(renderer, icon, state);
        using var toolbar = Render(renderer, icon, state, toolbar: true);
        AssertSamePixels(menu, toolbar);
        Assert.True(menu.GetPixel(11, 16).R > renderer.Colors.MenuBackground.R);
        Assert.Equal(Color.FromArgb(32, 32, 32).ToArgb(), menu.GetPixel(16, 16).ToArgb());
    }

    [Fact]
    public void DisabledIconsKeepExistingMutedRendering()
    {
        using var icon = new Bitmap(16, 16);
        using (var g = Graphics.FromImage(icon))
            g.FillRectangle(Brushes.Black, 4, 4, 8, 8);
        using var actual = Render(new DarkRenderer(), icon, RenderState.Disabled | RenderState.Hot);
        using var expected = Render(new Office2003Renderer(), icon, RenderState.Disabled | RenderState.Hot);
        AssertSamePixels(expected, actual);
    }

    [Theory]
    [InlineData(255)]
    [InlineData(128)]
    public void OutlineDoesNotWashOutSolidOrTranslucentInteriors(int opacity)
    {
        using var icon = new Bitmap(16, 16);
        using (var g = Graphics.FromImage(icon))
        using (var brush = new SolidBrush(Color.FromArgb(opacity, 20, 80, 160)))
            g.FillRectangle(brush, 3, 3, 10, 10);
        using var actual = Render(new DarkRenderer(), icon);
        using var expected = Render(new Office2003Renderer(), icon);
        Assert.Equal(expected.GetPixel(16, 16), actual.GetPixel(16, 16));
        Assert.True(actual.GetPixel(10, 16).R > expected.GetPixel(10, 16).R);
    }

    [Fact]
    public void OpaqueRectangularImagesRemainUnchanged()
    {
        using var icon = new Bitmap(16, 16);
        using (var g = Graphics.FromImage(icon))
            g.Clear(Color.CornflowerBlue);
        using var actual = Render(new DarkRenderer(), icon);
        using var expected = Render(new Office2003Renderer(), icon);
        AssertSamePixels(expected, actual);
    }

    [Fact]
    public void ContourReachesOutsideImageBoundsWithoutChangingCallerClip()
    {
        using var icon = new Bitmap(16, 16);
        using (var g = Graphics.FromImage(icon))
            g.FillRectangle(Brushes.Black, 0, 4, 8, 8);
        using var actual = Render(new DarkRenderer(), icon);
        Assert.True(actual.GetPixel(7, 16).R > new DarkRenderer().Colors.MenuBackground.R);

        using var clipped = new Bitmap(32, 32);
        using var graphics = Graphics.FromImage(clipped);
        var bounds = new Rectangle(8, 8, 16, 16);
        graphics.SetClip(bounds);
        new DarkRenderer().DrawItemImage(graphics, icon, bounds, RenderState.Normal);
        Assert.Equal(bounds, Rectangle.Round(graphics.ClipBounds));
        Assert.Equal(0, clipped.GetPixel(7, 16).A);
    }

    private static Bitmap Render(CommandBarRenderer renderer, Image icon,
        RenderState state = RenderState.Normal, bool toolbar = false)
    {
        var result = new Bitmap(icon.Width + 16, icon.Height + 16);
        using var g = Graphics.FromImage(result);
        g.Clear(new DarkRenderer().Colors.MenuBackground);
        var bounds = new Rectangle(8, 8, icon.Width, icon.Height);
        if (toolbar)
            renderer.DrawToolbarItemImage(g, icon, bounds, state);
        else
            renderer.DrawItemImage(g, icon, bounds, state);
        return result;
    }

    private static void AssertSamePixels(Bitmap expected, Bitmap actual)
    {
        Assert.Equal(expected.Size, actual.Size);
        for (int y = 0; y < expected.Height; y++)
            for (int x = 0; x < expected.Width; x++)
                Assert.Equal(expected.GetPixel(x, y), actual.GetPixel(x, y));
    }
}
