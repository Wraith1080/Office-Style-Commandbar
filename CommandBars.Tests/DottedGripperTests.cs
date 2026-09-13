using System.Drawing;
using CommandBars.Model;
using CommandBars.Rendering;
using Xunit;

namespace CommandBars.Tests;

public class DottedGripperTests
{
    [Fact]
    public void DotsScaleInBothOrientationsAndReturnToOriginalSize()
    {
        var renderer = new Office2003Renderer();
        foreach (float scale in new[] { 1f, 1.25f, 1.5f, 2f, 1f })
        {
            renderer.Scale = scale;
            int R(int value) => (int)Math.Round(value * scale);
            using var horizontal = new Bitmap(R(40), R(40));
            using var vertical = new Bitmap(R(40), R(40));
            var bounds = new Rectangle(2, 2, renderer.GripperExtent, R(30));
            using (var g = Graphics.FromImage(horizontal))
                renderer.DrawGripper(g, bounds, BarOrientation.Horizontal);
            using (var g = Graphics.FromImage(vertical))
                renderer.DrawGripper(g, new Rectangle(2, 2, R(30), renderer.GripperExtent), BarOrientation.Vertical);

            int dotRows = 0;
            bool previousDark = false;
            for (int y = 0; y < horizontal.Height; y++)
            {
                int darkPixels = 0;
                for (int x = 0; x < horizontal.Width; x++)
                {
                    var pixel = horizontal.GetPixel(x, y);
                    Assert.Equal(pixel, vertical.GetPixel(y, x));
                    if (!bounds.Contains(x, y)) Assert.Equal(0, pixel.A);
                    if (pixel.ToArgb() == renderer.Colors.GripperDark.ToArgb()) darkPixels++;
                }
                if (darkPixels > 0)
                {
                    Assert.Equal(R(2), darkPixels);
                    if (!previousDark) dotRows++;
                }
                previousDark = darkPixels > 0;
            }
            Assert.Equal(6, dotRows);
            int firstRow = Enumerable.Range(0, horizontal.Height).First(y =>
                Enumerable.Range(0, horizontal.Width).Any(x => horizontal.GetPixel(x, y).A != 0));
            Assert.Equal(renderer.Colors.GripperLight.ToArgb(),
                horizontal.GetPixel(2 + R(3) + R(1) + R(2) - 1,
                    firstRow + R(1) + R(2) - 1).ToArgb());
        }
    }

    [Theory]
    [InlineData(1f, false)]
    [InlineData(1.25f, false)]
    [InlineData(1.5f, false)]
    [InlineData(2f, false)]
    [InlineData(1f, true)]
    [InlineData(1.25f, true)]
    [InlineData(1.5f, true)]
    [InlineData(2f, true)]
    public void EndInsetsStayBalancedAcrossToolbarHeights(float scale, bool multiStrip)
    {
        Office2003Renderer renderer = multiStrip
            ? new OfficeXPRenderer(CommandBarColorScheme.Default, true)
            : new Office2003Renderer();
        renderer.Scale = scale;
        // Include fractional-DPI heights and lengths that are not multiples of
        // the dot pitch, where the old loop left excess space at the bottom.
        for (int length = 24; length <= 90; length++)
        {
            using var bitmap = new Bitmap(renderer.GripperExtent, length);
            using var transposed = new Bitmap(length, renderer.GripperExtent);
            using (var g = Graphics.FromImage(bitmap))
                renderer.DrawGripper(g, new Rectangle(0, 0, bitmap.Width, length), BarOrientation.Horizontal);
            using (var g = Graphics.FromImage(transposed))
                renderer.DrawGripper(g, new Rectangle(0, 0, length, bitmap.Width), BarOrientation.Vertical);
            for (int y = 0; y < length; y++)
                for (int x = 0; x < bitmap.Width; x++)
                    Assert.Equal(bitmap.GetPixel(x, y), transposed.GetPixel(y, x));
            var rows = Enumerable.Range(0, length).Where(y =>
                Enumerable.Range(0, bitmap.Width).Any(x => bitmap.GetPixel(x, y).A != 0)).ToArray();
            Assert.NotEmpty(rows);
            int opticalOffset = multiStrip ? 2 : 0;
            Assert.InRange(rows[0] - (length - 1 - rows[^1]), opticalOffset, opticalOffset + 1);
            Assert.True(rows[0] >= (int)Math.Round(3 * scale));
            var darkRows = Enumerable.Range(0, length).Where(y =>
                Enumerable.Range(0, bitmap.Width).Any(x =>
                    bitmap.GetPixel(x, y).ToArgb() == renderer.Colors.GripperDark.ToArgb())).ToHashSet();
            var starts = darkRows.Where(y => !darkRows.Contains(y - 1)).OrderBy(y => y).ToArray();
            for (int i = 1; i < starts.Length; i++)
                Assert.Equal((int)Math.Round((multiStrip ? 3 : 4) * scale), starts[i] - starts[i - 1]);
        }
    }
}
