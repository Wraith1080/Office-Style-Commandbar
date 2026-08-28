using CommandBars.Rendering;
using CommandBars.Model;
using System.Drawing;
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

    [Fact]
    public void ConnectedButton_OmitsOnlyThePopupFacingBorder()
    {
        var renderer = new Office2003Renderer();
        using var bitmap = new Bitmap(24, 24);
        using var graphics = Graphics.FromImage(bitmap);
        graphics.Clear(Color.Magenta);

        renderer.DrawConnectedButton(graphics, new Rectangle(2, 2, 20, 20),
            RenderState.Pressed, BarOrientation.Horizontal, PopupConnectionEdge.Bottom);

        Assert.Equal(renderer.Colors.ButtonPressedBorder.ToArgb(), bitmap.GetPixel(12, 2).ToArgb());
        Assert.NotEqual(renderer.Colors.ButtonPressedBorder.ToArgb(), bitmap.GetPixel(12, 21).ToArgb());
        Assert.Equal(renderer.Colors.ButtonPressedBorder.ToArgb(), bitmap.GetPixel(2, 12).ToArgb());
        Assert.Equal(renderer.Colors.ButtonPressedBorder.ToArgb(), bitmap.GetPixel(21, 12).ToArgb());
    }

    [Fact]
    public void EveryBuiltInTheme_ConnectsAnOpenButtonToItsPopup()
    {
        CommandBarRenderer[] renderers =
        {
            new OfficeXPRenderer(),
            new Office2003Renderer(),
            new Office2007Renderer(),
            new Office2010Renderer(),
            new DarkRenderer(),
        };

        foreach (CommandBarRenderer renderer in renderers)
        {
            using var bitmap = new Bitmap(24, 24);
            using var graphics = Graphics.FromImage(bitmap);
            graphics.Clear(Color.Magenta);
            renderer.DrawConnectedButton(graphics, new Rectangle(2, 2, 20, 20),
                RenderState.Pressed, BarOrientation.Horizontal, PopupConnectionEdge.Bottom);

            Assert.NotEqual(renderer.Colors.ButtonPressedBorder.ToArgb(), bitmap.GetPixel(12, 21).ToArgb());
        }
    }

    [Fact]
    public void Office2003_OpenMenuOwnerUsesSubtleBlueChrome_NotOrangePressedChrome()
    {
        var renderer = new Office2003Renderer();
        using var bitmap = new Bitmap(24, 24);
        using var graphics = Graphics.FromImage(bitmap);
        graphics.Clear(Color.Magenta);

        renderer.DrawOpenMenuButton(graphics, new Rectangle(2, 2, 20, 20),
            BarOrientation.Horizontal, PopupConnectionEdge.Bottom);

        Assert.Equal(renderer.Colors.MenuOpenBorder.ToArgb(), bitmap.GetPixel(12, 2).ToArgb());
        Assert.NotEqual(renderer.Colors.ButtonPressedBorder.ToArgb(), bitmap.GetPixel(12, 2).ToArgb());
        Assert.NotEqual(renderer.Colors.ButtonPressedBegin.ToArgb(), bitmap.GetPixel(12, 10).ToArgb());
    }

    [Fact]
    public void OfficeXP_OpenMenuOwnerKeepsItsNormalFlatBackground()
    {
        var colors = new OfficeXPRenderer().Colors;

        Assert.Equal(colors.BandGradientBegin, colors.MenuOpenBegin);
        Assert.Equal(colors.BandGradientEnd, colors.MenuOpenEnd);
        Assert.Equal(colors.MenuBorder, colors.MenuOpenBorder);
        Assert.NotEqual(colors.ButtonHotBegin, colors.MenuOpenBegin);
        Assert.NotEqual(colors.ButtonPressedBegin, colors.MenuOpenBegin);
        Assert.NotEqual(colors.ButtonHotBorder, colors.MenuOpenBorder);
    }

    [Fact]
    public void Chevron_AddsMoreItemsGlyphOnlyWhenItemsOverflow()
    {
        var renderer = new Office2003Renderer();
        int withoutOverflow = CountChevronInk(renderer, hasOverflowItems: false);
        int withOverflow = CountChevronInk(renderer, hasOverflowItems: true);

        Assert.True(withOverflow > withoutOverflow, $"Expected more glyph ink: {withOverflow} > {withoutOverflow}");
    }

    private static int CountChevronInk(Office2003Renderer renderer, bool hasOverflowItems)
    {
        using var bitmap = new Bitmap(28, 28);
        using var graphics = Graphics.FromImage(bitmap);
        renderer.DrawChevron(graphics, new Rectangle(7, 1, 14, 24),
            new Rectangle(0, 0, 22, 26), BarOrientation.Horizontal,
            RenderState.Normal, hasOverflowItems);

        int count = 0;
        for (int y = 0; y < bitmap.Height; y++)
        for (int x = 0; x < bitmap.Width; x++)
        {
            Color pixel = bitmap.GetPixel(x, y);
            // The glyph is dark; its anti-aliased edge pixels are deliberately
            // lighter than Text, but remain well below the blue chevron fill.
            if (pixel.A > 0 && pixel.R + pixel.G + pixel.B < 450)
                count++;
        }
        return count;
    }
}
