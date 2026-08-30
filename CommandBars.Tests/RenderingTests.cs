using CommandBars.Rendering;
using CommandBars.Model;
using CommandBars.Controls;
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
            new Office2000Renderer(),
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
    public void Office2000_UsesClassicFlatPaletteAndSingleSlabGripper()
    {
        var renderer = new Office2000Renderer();
        Assert.Equal(renderer.Colors.BarGradientBegin, renderer.Colors.BarGradientEnd);
        Assert.Equal(renderer.Colors.BandGradientBegin, renderer.Colors.BarGradientBegin);
        Assert.Equal(Color.White, renderer.Colors.MenuItemSelectedText);
        Assert.False(renderer.ConnectPopupOwners);
        Assert.True(renderer.UsesClassicMenuItemChrome);

        using var bitmap = new Bitmap(8, 24);
        using var graphics = Graphics.FromImage(bitmap);
        graphics.Clear(Color.Magenta);
        renderer.DrawGripper(graphics, new Rectangle(0, 0, 7, 24), BarOrientation.Horizontal);

        // The slab's leading/trailing edges are continuous vertical strokes,
        // unlike dotted Office XP/2003 handles.
        int continuousRows = 0;
        for (int y = 3; y < 21; y++)
            if (bitmap.GetPixel(3, y).ToArgb() != Color.Magenta.ToArgb())
                continuousRows++;
        Assert.True(continuousRows >= 16);
    }

    [Fact]
    public void Office2000_HotAndPressedButtonsUseRaisedAndSunkenBevels()
    {
        var renderer = new Office2000Renderer();
        using var hot = new Bitmap(20, 20);
        using var pressed = new Bitmap(20, 20);
        using (Graphics g = Graphics.FromImage(hot))
            renderer.DrawButton(g, new Rectangle(1, 1, 18, 18), RenderState.Hot, BarOrientation.Horizontal);
        using (Graphics g = Graphics.FromImage(pressed))
            renderer.DrawButton(g, new Rectangle(1, 1, 18, 18), RenderState.Pressed, BarOrientation.Horizontal);

        Assert.Equal(Color.FromArgb(0).ToArgb(), hot.GetPixel(8, 1).ToArgb());
        Assert.Equal(renderer.Colors.GripperLight.ToArgb(), hot.GetPixel(8, 2).ToArgb());
        Assert.Equal(renderer.Colors.GripperDark.ToArgb(), hot.GetPixel(8, 17).ToArgb());
        Assert.Equal(renderer.Colors.GripperDark.ToArgb(), pressed.GetPixel(8, 2).ToArgb());
        Assert.Equal(renderer.Colors.GripperLight.ToArgb(), pressed.GetPixel(8, 17).ToArgb());
    }

    [Fact]
    public void Office2000_HotSplitButtonUsesTwoConnectedBevelsWithoutGap()
    {
        var renderer = new Office2000Renderer();
        var bounds = new Rectangle(1, 1, 40, 20);
        var button = new Rectangle(1, 1, 30, 20);
        var arrow = new Rectangle(31, 1, 10, 20);
        using var bitmap = new Bitmap(44, 24);
        using (Graphics graphics = Graphics.FromImage(bitmap))
            renderer.DrawSplitButton(graphics, bounds, button, arrow,
                RenderState.Hot, RenderState.Hot, BarOrientation.Horizontal);

        Assert.Equal(renderer.Colors.GripperLight.ToArgb(),
            bitmap.GetPixel(10, 2).ToArgb());
        Assert.Equal(renderer.Colors.GripperLight.ToArgb(),
            bitmap.GetPixel(35, 2).ToArgb());

        // The main half's dark trailing edge touches the arrow half's light
        // leading edge directly: visibly split, with no transparent/gray gap.
        Assert.Equal(renderer.Colors.GripperDark.ToArgb(),
            bitmap.GetPixel(arrow.Left - 1, 10).ToArgb());
        Assert.Equal(renderer.Colors.GripperLight.ToArgb(),
            bitmap.GetPixel(arrow.Left, 10).ToArgb());
        Assert.Equal(renderer.Colors.BarGradientBegin.ToArgb(),
            bitmap.GetPixel(arrow.Left + 1, 10).ToArgb());
    }

    [Fact]
    public void Office2000_CheckedButtonUsesClassicCheckerHatch()
    {
        var renderer = new Office2000Renderer();
        using var bitmap = new Bitmap(24, 24);
        using var graphics = Graphics.FromImage(bitmap);
        renderer.DrawButton(graphics, new Rectangle(1, 1, 22, 22),
            RenderState.Checked, BarOrientation.Horizontal);

        var colors = new HashSet<int>();
        for (int y = 5; y < 18; y++)
        for (int x = 5; x < 18; x++)
            colors.Add(bitmap.GetPixel(x, y).ToArgb());

        Assert.True(colors.Count >= 2);
        Assert.Contains(renderer.Colors.BarGradientBegin.ToArgb(), colors);
        Assert.Contains(renderer.Colors.GripperLight.ToArgb(), colors);
    }

    [Fact]
    public void Office2000_MenuBarIsBeveledAndChevronHasNoLeadingDivider()
    {
        var renderer = new Office2000Renderer();
        using var menu = new Bitmap(40, 20);
        using (Graphics graphics = Graphics.FromImage(menu))
            renderer.DrawBarBackground(graphics, new Rectangle(0, 0, 40, 20),
                CommandBarType.MenuBar, BarOrientation.Horizontal,
                rounded: false, bandOffset: 0, bandExtent: 40);

        Assert.Equal(renderer.Colors.GripperLight.ToArgb(), menu.GetPixel(20, 0).ToArgb());
        Assert.Equal(renderer.Colors.GripperDark.ToArgb(), menu.GetPixel(20, 19).ToArgb());

        using var chevron = new Bitmap(24, 24);
        using (Graphics graphics = Graphics.FromImage(chevron))
            renderer.DrawChevron(graphics, new Rectangle(8, 1, 14, 22),
                new Rectangle(0, 0, 23, 24), BarOrientation.Horizontal,
                RenderState.Normal, hasOverflowItems: false);
        Assert.NotEqual(renderer.Colors.SeparatorDark.ToArgb(), chevron.GetPixel(8, 12).ToArgb());
    }

    [Fact]
    public void Office2000_ComboArrowIsRaisedAtRestAndSunkenWhenPressed()
    {
        var renderer = new Office2000Renderer();
        var bounds = new Rectangle(1, 1, 60, 20);
        var arrow = new Rectangle(45, 1, 16, 20);
        using var normal = new Bitmap(64, 24);
        using var pressed = new Bitmap(64, 24);
        using (Graphics graphics = Graphics.FromImage(normal))
            renderer.DrawComboBoxChrome(graphics, bounds, arrow,
                RenderState.Normal, Color.White);
        using (Graphics graphics = Graphics.FromImage(pressed))
            renderer.DrawComboBoxChrome(graphics, bounds, arrow,
                RenderState.Pressed, Color.White);

        Assert.Equal(renderer.Colors.GripperLight.ToArgb(), normal.GetPixel(51, 2).ToArgb());
        Assert.Equal(renderer.Colors.GripperDark.ToArgb(), pressed.GetPixel(51, 2).ToArgb());
    }

    [Fact]
    public void Office2000_PopupUsesRaisedSlabBorderAndEstablishedThickCheckmark()
    {
        var office2000 = new Office2000Renderer();
        using var popup = new Bitmap(30, 30);
        using (Graphics graphics = Graphics.FromImage(popup))
            office2000.DrawMenuBackground(graphics, new Rectangle(0, 0, 30, 30));

        Assert.Equal(office2000.Colors.SeparatorLight.ToArgb(), popup.GetPixel(15, 0).ToArgb());
        Assert.Equal(office2000.Colors.MenuBorder.ToArgb(), popup.GetPixel(15, 29).ToArgb());
        Assert.Equal(office2000.Colors.GripperDark.ToArgb(), popup.GetPixel(15, 28).ToArgb());

        var officeXP = new OfficeXPRenderer();
        using var oldCheck = new Bitmap(24, 24);
        using var xpCheck = new Bitmap(24, 24);
        using (Graphics graphics = Graphics.FromImage(oldCheck))
            office2000.DrawMenuCheck(graphics, new Rectangle(2, 2, 20, 20), RenderState.Normal);
        using (Graphics graphics = Graphics.FromImage(xpCheck))
            officeXP.DrawMenuCheck(graphics, new Rectangle(2, 2, 20, 20), RenderState.Normal);

        for (int y = 0; y < 24; y++)
        for (int x = 0; x < 24; x++)
            Assert.Equal(xpCheck.GetPixel(x, y).ToArgb(), oldCheck.GetPixel(x, y).ToArgb());
    }

    [Fact]
    public void Office2000_FloatingChromeUsesRaisedFrameAndMenuSelectionCaption()
    {
        var renderer = new Office2000Renderer();
        using var bitmap = new Bitmap(80, 40);
        using (Graphics graphics = Graphics.FromImage(bitmap))
            renderer.DrawFloatingWindowChrome(graphics,
                new Rectangle(0, 0, 80, 40), new Rectangle(3, 3, 74, 18));

        Assert.Equal(renderer.Colors.GripperLight.ToArgb(), bitmap.GetPixel(30, 0).ToArgb());
        Assert.Equal(renderer.Colors.GripperLight.ToArgb(), bitmap.GetPixel(30, 1).ToArgb());
        Assert.Equal(renderer.Colors.GripperDark.ToArgb(), bitmap.GetPixel(30, 38).ToArgb());
        Assert.Equal(renderer.Colors.GripperDark.ToArgb(), bitmap.GetPixel(30, 39).ToArgb());
        Assert.Equal(renderer.Colors.MenuItemSelectedBegin.ToArgb(), bitmap.GetPixel(30, 10).ToArgb());
        Assert.Equal(renderer.Colors.MenuItemSelectedText, renderer.FloatingCaptionTextColor);
    }

    [Fact]
    public void Office2000_FloatingCloseIsRaisedWithoutHoverEffectAndSunkenWhenPressed()
    {
        var renderer = new Office2000Renderer();
        var bounds = new Rectangle(1, 1, 20, 20);
        using var normal = new Bitmap(22, 22);
        using var hot = new Bitmap(22, 22);
        using var pressed = new Bitmap(22, 22);
        using (Graphics graphics = Graphics.FromImage(normal))
            FloatingCaptionButtonPainter.DrawClose(graphics, renderer, bounds,
                hot: false, pressed: false);
        using (Graphics graphics = Graphics.FromImage(hot))
            FloatingCaptionButtonPainter.DrawClose(graphics, renderer, bounds,
                hot: true, pressed: false);
        using (Graphics graphics = Graphics.FromImage(pressed))
            FloatingCaptionButtonPainter.DrawClose(graphics, renderer, bounds,
                hot: true, pressed: true);

        for (int y = 0; y < normal.Height; y++)
        for (int x = 0; x < normal.Width; x++)
            Assert.Equal(normal.GetPixel(x, y).ToArgb(), hot.GetPixel(x, y).ToArgb());

        Assert.Equal(renderer.DialogColors.ControlHighlight.ToArgb(),
            normal.GetPixel(10, bounds.Top).ToArgb());
        Assert.Equal(renderer.DialogColors.ControlDarkShadow.ToArgb(),
            normal.GetPixel(10, bounds.Bottom - 1).ToArgb());
        Assert.Equal(renderer.DialogColors.ControlDarkShadow.ToArgb(),
            pressed.GetPixel(10, bounds.Top).ToArgb());
        Assert.Equal(renderer.DialogColors.ControlHighlight.ToArgb(),
            pressed.GetPixel(10, bounds.Bottom - 1).ToArgb());
    }

    [Fact]
    public void Office2000_MenuIconFrameMatchesSelectionHeight_AndRequiresContent()
    {
        var renderer = new Office2000Renderer();
        var withoutImage = new CommandBar("plain", CommandBarType.Popup);
        var plainItem = withoutImage.Items.AddButton(new Command("plain") { Text = "Plain" });
        using var plainWindow = new CommandBarPopupWindow(
            withoutImage, renderer, SystemFonts.MenuFont!, 16, 1f);
        plainWindow.SelectFirst();
        using var plainBitmap = new Bitmap(plainWindow.ClientSize.Width, plainWindow.ClientSize.Height);
        plainWindow.DrawToBitmap(plainBitmap, plainWindow.ClientRectangle);

        Assert.Equal(renderer.Colors.MenuBackground.ToArgb(),
            plainBitmap.GetPixel(2, plainItem.Bounds.Top).ToArgb());
        Assert.Equal(renderer.Colors.MenuItemSelectedBegin.ToArgb(),
            plainBitmap.GetPixel(3, plainItem.Bounds.Top).ToArgb());
        int plainLastSelectedX = Enumerable.Range(0, plainBitmap.Width)
            .Last(x => plainBitmap.GetPixel(x, plainItem.Bounds.Top).ToArgb() ==
                renderer.Colors.MenuItemSelectedBegin.ToArgb());
        Assert.Equal(3, plainItem.Bounds.Right - plainLastSelectedX - 1);

        var withImage = new CommandBar("image", CommandBarType.Popup);
        var imageItem = withImage.Items.AddButton(new Command("image")
        {
            Text = "Image",
            Image = new StubImageSource(),
        });
        using var imageWindow = new CommandBarPopupWindow(
            withImage, renderer, SystemFonts.MenuFont!, 16, 1f);
        imageWindow.SelectFirst();
        using var imageBitmap = new Bitmap(imageWindow.ClientSize.Width, imageWindow.ClientSize.Height);
        imageWindow.DrawToBitmap(imageBitmap, imageWindow.ClientRectangle);

        Assert.Equal(renderer.Colors.GripperLight.ToArgb(),
            imageBitmap.GetPixel(10, imageItem.Bounds.Top).ToArgb());
        Assert.Equal(renderer.Colors.MenuBackground.ToArgb(),
            imageBitmap.GetPixel(2, imageItem.Bounds.Top).ToArgb());
        Assert.Equal(renderer.Colors.GripperLight.ToArgb(),
            imageBitmap.GetPixel(3, imageItem.Bounds.Top).ToArgb());
        Assert.Equal(renderer.Colors.GripperDark.ToArgb(),
            imageBitmap.GetPixel(10, imageItem.Bounds.Bottom - 2).ToArgb());
        Assert.Equal(renderer.Colors.MenuItemSelectedBegin.ToArgb(),
            imageBitmap.GetPixel(imageItem.Bounds.Right - 8, imageItem.Bounds.Top).ToArgb());

        int firstSelectedX = Enumerable.Range(0, imageBitmap.Width)
            .First(x => imageBitmap.GetPixel(x, imageItem.Bounds.Top).ToArgb() ==
                renderer.Colors.MenuItemSelectedBegin.ToArgb());
        Assert.True(firstSelectedX > 2);
        Assert.Equal(renderer.Colors.MenuBackground.ToArgb(),
            imageBitmap.GetPixel(firstSelectedX - 1, imageItem.Bounds.Top).ToArgb());
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
