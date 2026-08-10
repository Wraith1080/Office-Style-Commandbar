using CommandBars.Rendering;
using Xunit;

namespace CommandBars.Tests;

public class RenderingTests
{
    [Fact]
    public void DialogPalette_IsCachedAndDerivedForLightAndDarkRenderers()
    {
        var office = new Office2003Renderer();
        var dark = new DarkRenderer();

        Assert.Same(office.DialogColors, office.DialogColors);
        Assert.False(office.DialogColors.IsDark);
        Assert.True(dark.DialogColors.IsDark);
        Assert.NotEqual(office.DialogColors.TabBody, office.DialogColors.InputBackground);
        Assert.NotEqual(dark.DialogColors.Text, dark.DialogColors.TabBody);
    }
}
