using System.Drawing;
using System.Windows.Forms;
using CommandBars.Controls;
using CommandBars.Imaging;
using CommandBars.Model;
using CommandBars.Rendering;
using Xunit;

namespace CommandBars.Tests;

public sealed class PaletteGridTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void GridPopupHasUniformBackgroundWhileKeepingHeaderIcon(bool dark)
    {
        using var image = new SwatchImage();
        var bar = CreatePalette(image);
        var header = (CommandBarCommandItem)bar.Items[0];
        header.Command.Image = image;
        CommandBarRenderer renderer = dark ? new DarkRenderer() : new Office2003Renderer();
        using var popup = new CommandBarPopupWindow(bar, renderer, SystemFonts.MenuFont!, 24, 1f);
        using var bitmap = Paint(popup);
        var more = bar.Items.OfType<CommandBarCommandItem>().Single(item => item.Command.Id == "custom");

        Assert.Equal(renderer.Colors.MenuBackground.ToArgb(),
            bitmap.GetPixel(5, more.Bounds.Top + more.Bounds.Height / 2).ToArgb());
        Assert.Equal(Color.MediumPurple.ToArgb(),
            bitmap.GetPixel(16, header.Bounds.Top + header.Bounds.Height / 2).ToArgb());
    }

    [Theory]
    [InlineData(1f)]
    [InlineData(1.25f)]
    [InlineData(1.5f)]
    [InlineData(2f)]
    public void PopupPacksPushAndToggleSwatchesIntoRows(float scale)
    {
        using var image = new SwatchImage();
        var bar = CreatePalette(image);
        using var popup = new CommandBarPopupWindow(bar, new Office2003Renderer(),
            SystemFonts.MenuFont!, 24, scale, (_, _) => { });

        AssertGrid(bar);
        Assert.True(popup.TearOffEnabled);
        // Image-only captions/shortcuts must not widen an otherwise compact grid.
        Assert.True(popup.Width <= 180 * scale);
    }

    [Theory]
    [InlineData(16)]
    [InlineData(24)]
    [InlineData(48)]
    public void DetachedPaletteKeepsTheGridAtDifferentIconSizes(int iconSize)
    {
        using var image = new SwatchImage();
        var bar = CreatePalette(image);
        bar.IconSize = iconSize;
        using var palette = new TearOffWindow(bar, bar, new Office2003Renderer(), null, null);

        AssertGrid(bar);
        Assert.True(palette.BarControl.Width < 240);
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(false, true)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public void SwatchSelectionBorderFollowsSharedCommandState(bool detached, bool dark)
    {
        using var image = new SwatchImage();
        var bar = new CommandBar("colors", CommandBarType.Popup) { PaletteColumns = 3 };
        var command = new Command("color") { Text = "Color", Image = image };
        var swatch = bar.Items.AddToggle(command);
        swatch.DisplayStyle = CommandItemDisplayStyle.ImageOnly;
        CommandBarRenderer renderer = dark ? new DarkRenderer() : new Office2003Renderer();
        using Control surface = detached
            ? new CommandBarControl { Renderer = renderer, PaletteMode = true, Bar = bar }
            : new CommandBarPopupWindow(bar, renderer, SystemFonts.MenuFont!, 24, 1f);

        var border = new Point(swatch.Bounds.Left + 1, swatch.Bounds.Top + swatch.Bounds.Height / 2);
        using var uncheckedBitmap = Paint(surface);
        command.Checked = CommandCheckState.Checked;
        using var checkedBitmap = Paint(surface);
        command.Checked = CommandCheckState.Unchecked;
        using var clearedBitmap = Paint(surface);
        command.Enabled = false;
        using var disabledBitmap = Paint(surface);

        Assert.Equal(renderer.Colors.ButtonCheckedBorder.ToArgb(), checkedBitmap.GetPixel(border.X, border.Y).ToArgb());
        Assert.NotEqual(uncheckedBitmap.GetPixel(border.X, border.Y), checkedBitmap.GetPixel(border.X, border.Y));
        Assert.Equal(uncheckedBitmap.GetPixel(border.X, border.Y), clearedBitmap.GetPixel(border.X, border.Y));
        var center = new Point(swatch.Bounds.Left + swatch.Bounds.Width / 2, swatch.Bounds.Top + swatch.Bounds.Height / 2);
        Assert.NotEqual(uncheckedBitmap.GetPixel(center.X, center.Y), disabledBitmap.GetPixel(center.X, center.Y));
    }

    private static CommandBar CreatePalette(IImageSource image)
    {
        var bar = new CommandBar("colors", CommandBarType.Popup) { PaletteColumns = 3, AllowTearOff = true };
        bar.Items.AddToggle(new Command("none") { Text = "No fill" }).DisplayStyle = CommandItemDisplayStyle.TextOnly;
        bar.Items.AddSeparator();
        for (int i = 0; i < 7; i++)
        {
            var command = new Command("color." + i)
            {
                Text = "A very long color name that should never be rendered inside a swatch cell",
                Image = image,
                Shortcut = Keys.Control | Keys.Shift | Keys.C,
                Checked = i == 0 ? CommandCheckState.Checked : CommandCheckState.Unchecked
            };
            CommandBarCommandItem item = i % 2 == 0 ? bar.Items.AddToggle(command) : bar.Items.AddButton(command);
            item.DisplayStyle = CommandItemDisplayStyle.ImageOnly;
        }
        var hidden = bar.Items.AddToggle(new Command("hidden") { Image = image });
        hidden.DisplayStyle = CommandItemDisplayStyle.ImageOnly;
        hidden.Visible = false;
        bar.Items.AddSeparator();
        bar.Items.AddButton(new Command("custom") { Text = "More colors..." });
        // Even icon-only split buttons need their dropdown affordance and a full row.
        bar.Items.AddSplitButton(new Command("split") { Text = "Other colors", Image = image })
            .DisplayStyle = CommandItemDisplayStyle.ImageOnly;
        return bar;
    }

    private static void AssertGrid(CommandBar bar)
    {
        var cells = bar.Items.OfType<CommandBarCommandItem>()
            .Where(item => item.Command.Id.StartsWith("color.")).ToArray();
        foreach (var item in cells) Assert.Equal(item.Bounds.Width, item.Bounds.Height);
        Assert.Equal(cells[0].Bounds.Top, cells[1].Bounds.Top);
        Assert.Equal(cells[0].Bounds.Top, cells[2].Bounds.Top);
        Assert.Equal(cells[0].Bounds.Right, cells[1].Bounds.Left);
        Assert.Equal(cells[0].Bounds.Bottom, cells[3].Bounds.Top);
        Assert.Equal(cells[0].Bounds.Left, cells[3].Bounds.Left);
        Assert.Equal(cells[3].Bounds.Bottom, cells[6].Bounds.Top);
        Assert.Equal(Rectangle.Empty, bar.Items.Single(item => !item.Visible).Bounds);
        var more = bar.Items.OfType<CommandBarCommandItem>().Single(item => item.Command.Id == "custom");
        Assert.True(more.Bounds.Top >= cells[6].Bounds.Bottom);
        Assert.True(more.Bounds.Width >= 3 * cells[0].Bounds.Width);
        var split = bar.Items.OfType<CommandBarSplitButton>().Single();
        Assert.True(split.Bounds.Top >= more.Bounds.Bottom);
        Assert.Equal(more.Bounds.Width, split.Bounds.Width);
    }

    private static Bitmap Paint(Control surface)
    {
        var bitmap = new Bitmap(surface.Width, surface.Height);
        surface.DrawToBitmap(bitmap, surface.ClientRectangle);
        return bitmap;
    }

    private sealed class SwatchImage : IImageSource, IDisposable
    {
        private readonly Bitmap _bitmap = new(24, 24);
        public SwatchImage()
        {
            using var graphics = Graphics.FromImage(_bitmap);
            graphics.Clear(Color.MediumPurple);
        }
        public string? Key => null;
        public Image GetImage(int pixelSize, float dpiScale = 1f) => _bitmap;
        public void Dispose() => _bitmap.Dispose();
    }
}
