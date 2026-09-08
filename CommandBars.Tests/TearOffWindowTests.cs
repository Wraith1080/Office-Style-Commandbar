using System.Windows.Forms;
using CommandBars.Controls;
using CommandBars.Model;
using CommandBars.Rendering;
using Xunit;

namespace CommandBars.Tests;

public sealed class TearOffWindowTests
{
    [Theory]
    [InlineData(0)]
    [InlineData(4)]
    public void GlobalIconSizeResizesOpenLinearAndGridPalettes(int columns)
    {
        using var manager = new CommandBarManager();
        var toolbar = manager.AddBar("tools", CommandBarType.Toolbar);
        var source = new CommandBar("palette", CommandBarType.Popup) { IconSize = 16, PaletteColumns = columns };
        for (int i = 0; i < 4; i++)
            source.Items.AddButton(manager.Commands.GetOrAdd("shape" + i));
        Restore(manager, source);
        var window = Assert.Single(Windows(manager));
        try
        {
            var originalSize = window.Size;
            manager.SetIconSize(48);
            Assert.Equal(48, toolbar.IconSize);
            Assert.Equal(48, window.Bar.IconSize);
            Assert.Equal(16, source.IconSize);
            Assert.True(window.Height > originalSize.Height);
            Assert.True(window.Width > originalSize.Width);
            using var saved = new MemoryStream();
            manager.SaveLayout(saved);
            using var json = System.Text.Json.JsonDocument.Parse(saved.ToArray());
            Assert.Equal(48, json.RootElement.GetProperty("TearOffs")[0].GetProperty("IconSize").GetInt32());
            manager.SetIconSize(16);
            Assert.Equal(originalSize, window.Size);
        }
        finally { window.Close(); }
    }

    [Fact]
    public void CatalogPlacementsShareIdentityWithoutUsingCaption()
    {
        using var manager = new CommandBarManager();
        foreach (var id in new[] { "shapes", "other" })
            manager.CommandDefinitions.Add(new CommandBars.Design.CommandDefinition
            {
                Id = id, Text = "Shapes", Kind = CommandBars.Design.CommandDefinitionKind.Popup,
                TearOff = true,
            });
        var first = Assert.IsType<CommandBarPopupItem>(manager.CreateCatalogItem("shapes"));
        var second = Assert.IsType<CommandBarPopupItem>(manager.CreateCatalogItem("shapes"));
        var other = Assert.IsType<CommandBarPopupItem>(manager.CreateCatalogItem("other"));
        Assert.Equal(first.DropDown.TearOffKey, second.DropDown.TearOffKey);
        Assert.NotEqual(first.DropDown.TearOffKey, other.DropDown.TearOffKey);
    }

    [Fact]
    public void LegacyPlacementsRecoverApplicationIdentity()
    {
        using var manager = new CommandBarManager();
        var popup = manager.AddBar("tools", CommandBarType.Toolbar).Items.AddPopup("Shapes");
        popup.DropDown.TearOffKey = "app.shapes";
        manager.CaptureDefaults();
        using var saved = new MemoryStream();
        manager.SaveLayout(saved);
        var state = System.Text.Json.Nodes.JsonNode.Parse(saved.ToArray())!;
        var item = state["Bars"]![0]!["Items"]![0]!;
        item.AsObject().Remove("TearOffKey");
        state["Bars"]![0]!["Items"]!.AsArray().Add(item.DeepClone());
        using var legacy = new MemoryStream(System.Text.Encoding.UTF8.GetBytes(state.ToJsonString()));
        manager.LoadLayout(legacy);
        foreach (var restored in manager.Bars[0].Items.Cast<CommandBarPopupItem>())
            Assert.Equal("app.shapes", restored.DropDown.TearOffKey);
    }

    [Fact]
    public void EquivalentPlacementsReusePaletteAndCanReopenAfterClose()
    {
        using var manager = new CommandBarManager();
        var first = new CommandBar("original", CommandBarType.Popup) { TearOffKey = "shapes" };
        var second = new CommandBar("renamed", CommandBarType.Popup) { TearOffKey = "shapes" };
        Restore(manager, first);
        Restore(manager, second);
        var window = Assert.Single(Windows(manager));
        manager.ShowTearOff(second, new System.Drawing.Point(100, 100), null, 32);
        Assert.Same(window, Assert.Single(Windows(manager)));
        Assert.Equal(32, window.Bar.IconSize);
        window.Capture = false;
        window.Close();
        Restore(manager, second);
        var reopened = Assert.Single(Windows(manager));
        Assert.NotSame(window, reopened);
        reopened.Close();
    }

    [Fact]
    public void SameCaptionDoesNotMergeUnrelatedPalettes()
    {
        using var manager = new CommandBarManager();
        Restore(manager, new CommandBar("popup:Shapes", CommandBarType.Popup));
        Restore(manager, new CommandBar("popup:Shapes", CommandBarType.Popup));
        Assert.Equal(2, Windows(manager).Count);
        foreach (var window in Windows(manager).ToArray()) window.Close();
    }

    [Fact]
    public void IdentitySurvivesLayoutAndNestedPaletteCloning()
    {
        using var manager = new CommandBarManager();
        var toolbar = manager.AddBar("tools", CommandBarType.Toolbar);
        var parent = toolbar.Items.AddPopup("Shapes");
        parent.DropDown.TearOffKey = "shapes";
        var nested = parent.DropDown.Items.AddPopup("Lines");
        nested.DropDown.TearOffKey = "shapes.lines";
        Restore(manager, parent.DropDown);
        var palette = Assert.Single(Windows(manager));
        var copiedNested = Assert.IsType<CommandBarPopupItem>(palette.Bar.Items[0]);
        Assert.Equal(nested.DropDown.TearOffKey, copiedNested.DropDown.TearOffKey);
        Restore(manager, copiedNested.DropDown);
        Restore(manager, nested.DropDown);
        Assert.Equal(2, Windows(manager).Count);
        using var stream = new MemoryStream();
        manager.SaveLayout(stream);
        foreach (var window in Windows(manager).ToArray()) window.Close();
        stream.Position = 0;
        manager.LoadLayout(stream);
        var restored = Assert.IsType<CommandBarPopupItem>(manager.Bars[0].Items[0]);
        Assert.Equal("shapes", restored.DropDown.TearOffKey);
        Assert.Equal("shapes.lines", Assert.IsType<CommandBarPopupItem>(restored.DropDown.Items[0]).DropDown.TearOffKey);
    }

    private static List<TearOffWindow> Windows(CommandBarManager manager)
        => (List<TearOffWindow>)typeof(CommandBarManager).GetField("_tearOffs",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!.GetValue(manager)!;

    private static void Restore(CommandBarManager manager, CommandBar bar)
        => typeof(CommandBarManager).GetMethod("RestoreTearOff",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!
            .Invoke(manager, new object?[] { bar, new System.Drawing.Point(100, 100), null, null });

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
