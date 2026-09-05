using System.Windows.Forms;
using CommandBars.Controls;
using CommandBars.Model;
using CommandBars.Rendering;
using Xunit;

namespace CommandBars.Tests;

public sealed class TearOffWindowTests
{
    [Theory]
    [InlineData(24)]
    [InlineData(48)]
    public void RestoredPaletteKeepsDetachedIconSizeWithoutChangingSource(int iconSize)
    {
        using var manager = new CommandBarManager();
        var source = new CommandBar("palette", CommandBarType.Popup) { IconSize = 16, PaletteColumns = 8 };
        typeof(CommandBarManager).GetMethod("RestoreTearOff", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!
            .Invoke(manager, new object?[] { source, new System.Drawing.Point(100, 100), null, iconSize });
        var windows = (System.Collections.IList)typeof(CommandBarManager)
            .GetField("_tearOffs", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!.GetValue(manager)!;
        var window = Assert.IsType<TearOffWindow>(windows[0]);
        Assert.Equal(iconSize, window.Bar.IconSize);
        Assert.Equal(16, source.IconSize);
        using var stream = new MemoryStream();
        manager.SaveLayout(stream);
        using var json = System.Text.Json.JsonDocument.Parse(stream.ToArray());
        Assert.Equal(iconSize, json.RootElement.GetProperty("TearOffs")[0].GetProperty("IconSize").GetInt32());
        window.Close();
    }

    [Fact]
    public void GridPalettePaintsHorizontalSeparators()
    {
        var renderer = new SeparatorRenderer();
        var bar = new CommandBar("palette", CommandBarType.Popup) { PaletteColumns = 8 };
        bar.Items.AddSeparator();
        using var control = new CommandBarControl { Renderer = renderer, PaletteMode = true, Bar = bar };
        using var bitmap = new System.Drawing.Bitmap(control.Width, control.Height);
        control.DrawToBitmap(bitmap, control.ClientRectangle);
        Assert.Equal(BarOrientation.Vertical, renderer.SeparatorOrientation);
    }

    private sealed class SeparatorRenderer : Office2003Renderer
    {
        public BarOrientation? SeparatorOrientation { get; private set; }
        public override void DrawSeparator(System.Drawing.Graphics g, System.Drawing.Rectangle bounds, BarOrientation orientation)
            => SeparatorOrientation = orientation;
    }

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
