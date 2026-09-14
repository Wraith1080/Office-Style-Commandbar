using System.ComponentModel;
using System.Drawing;
using CommandBars.Controls;
using CommandBars.Design;
using CommandBars.Model;
using CommandBars.Rendering;
using Xunit;

namespace CommandBars.Tests;

public class MenuBarDockingTests
{
    [Theory]
    [InlineData(DockEdge.Left, DockState.Left)]
    [InlineData(DockEdge.Right, DockState.Right)]
    public void CaptionOptionReflowsSideMenusAndMatchesDockPreview(DockEdge edge, DockState dock)
    {
        using var manager = new CommandBarManager();
        var bar = manager.AddBar("menu", CommandBarType.MenuBar);
        bar.Dock = dock;
        var item = bar.Items.AddPopup("&Long caption");
        using var host = new DockHost { Edge = edge, Height = 500, Manager = manager };
        int horizontalWidth = host.Width;
        Assert.True(item.Bounds.Width > item.Bounds.Height);
        manager.RotateVerticalMenuCaptions = true;
        Assert.True(host.Width < horizontalWidth);
        Assert.True(item.Bounds.Height > item.Bounds.Width);
        Assert.Equal(host.RectangleToScreen(host.BarControls.Single().Bounds),
            host.ComputeBarDockPreview(Point.Empty, Size.Empty, bar));
        manager.RotateVerticalMenuCaptions = false;
        Assert.Equal(horizontalWidth, host.Width);
        Assert.True(item.Bounds.Width > item.Bounds.Height);
    }

    [Fact]
    public void CaptionOptionPersistsAndSurvivesLayoutReset()
    {
        using var manager = new CommandBarManager();
        Assert.False(manager.RotateVerticalMenuCaptions);
        manager.CaptureDefaults();
        manager.RotateVerticalMenuCaptions = true;
        Assert.True(manager.ResetToDefaults());
        Assert.True(manager.RotateVerticalMenuCaptions);
        using var stream = new MemoryStream();
        manager.SaveLayout(stream);
        stream.Position = 0;
        using var restored = new CommandBarManager();
        restored.LoadLayout(stream);
        Assert.True(restored.RotateVerticalMenuCaptions);
        using var legacy = new MemoryStream(System.Text.Encoding.UTF8.GetBytes("{\"Version\":2,\"Bars\":[]}"));
        restored.LoadLayout(legacy);
        Assert.False(restored.RotateVerticalMenuCaptions);
    }

    [Theory]
    [InlineData(DockEdge.Top, DockState.Top)]
    [InlineData(DockEdge.Bottom, DockState.Bottom)]
    [InlineData(DockEdge.Left, DockState.Left)]
    [InlineData(DockEdge.Right, DockState.Right)]
    public void DesignerMenusOccupySeparateOuterSlotsAndRefreshAfterDeletion(DockEdge edge, DockState dock)
    {
        using var manager = new CommandBarManager();
        for (int i = 0; i < 2; i++)
        {
            var definition = new MenuBarDefinition { Name = "menu" + i, Dock = dock };
            definition.Items.Add(new PopupDefinition { Text = "&File" });
            manager.BarDefinitions.Add(definition);
        }
        manager.BarDefinitions.Add(new ToolbarDefinition { Name = "tools", Dock = dock });
        using var host = new DockHost { Edge = edge, Size = new Size(500, 500) };
        host.Site = new PreviewSite(host);
        host.Manager = manager;
        Assert.Equal(3, host.BarControls.Count());
        var controls = host.BarControls.ToArray();
        var first = controls[0];
        var second = controls[1];
        var toolbar = controls[2];
        Assert.False(first.Bounds.IntersectsWith(second.Bounds));
        Assert.False(first.Bounds.IntersectsWith(toolbar.Bounds));
        Assert.False(second.Bounds.IntersectsWith(toolbar.Bounds));
        bool horizontal = edge is DockEdge.Top or DockEdge.Bottom;
        int inset = Math.Max(1, (int)Math.Round(host.Renderer.ToolbarGap * host.DeviceDpi / 96f));
        Assert.All(controls.Take(2), c => Assert.Equal(horizontal ? host.Width - 2 * inset : host.Height,
            horizontal ? c.Width : c.Height));
        if (horizontal)
            Assert.Equal(toolbar.Left, first.Left);
        switch (edge)
        {
            case DockEdge.Top: Assert.Equal(0, first.Top); Assert.True(second.Bottom <= toolbar.Top); break;
            case DockEdge.Bottom: Assert.Equal(host.Height, first.Bottom); Assert.True(toolbar.Bottom <= second.Top); break;
            case DockEdge.Left: Assert.Equal(0, first.Left); Assert.True(second.Right <= toolbar.Left); break;
            case DockEdge.Right: Assert.Equal(host.Width, first.Right); Assert.True(toolbar.Right <= second.Left); break;
        }
        var original = controls.Select(c => c.Bounds).ToArray();
        host.Rebuild();
        Assert.Equal(original, host.BarControls.Select(c => c.Bounds));
        manager.BarDefinitions.RemoveAt(0);
        host.Rebuild();
        Assert.Equal(new[] { "menu1", "tools" }, host.BarControls.Select(c => c.Bar!.Name));
        // Re-adding a definition models the preview refresh following Undo.
        var restored = new MenuBarDefinition { Name = "menu0", Dock = dock };
        restored.Items.Add(new PopupDefinition { Text = "&File" });
        manager.BarDefinitions.Insert(0, restored);
        host.Rebuild();
        Assert.Equal(original, host.BarControls.Select(c => c.Bounds));
    }

    [Theory]
    [InlineData(DockEdge.Top, DockState.Top)]
    [InlineData(DockEdge.Bottom, DockState.Bottom)]
    [InlineData(DockEdge.Left, DockState.Left)]
    [InlineData(DockEdge.Right, DockState.Right)]
    public void MenuDropUsesOwnSlotAndPreservesToolbarPlacement(DockEdge edge, DockState dock)
    {
        using var manager = new CommandBarManager();
        var menu = manager.AddBar("menu", CommandBarType.MenuBar);
        menu.Items.AddPopup("&File");
        menu.Dock = DockState.Floating;
        var second = manager.AddBar("second", CommandBarType.MenuBar);
        second.Items.AddPopup("&Edit");
        second.Dock = dock;
        var toolbar = manager.AddBar("toolbar", CommandBarType.Toolbar);
        toolbar.Dock = dock;
        toolbar.Row = 3;
        using var host = new DockHost { Edge = edge, Size = new Size(500, 500), Manager = manager };
        host.ApplyDrop(menu, false, 3, 42);
        Assert.Equal(dock, menu.Dock);
        Assert.Equal(3, toolbar.Row);
        Assert.Equal(new[] { "menu", "second", "toolbar" }, host.BarControls.Select(c => c.Bar!.Name));
        Assert.False(host.BarControls.First().Bounds.IntersectsWith(host.BarControls.Skip(1).First().Bounds));
        var itemBounds = menu.Items[0].Bounds;
        Assert.Equal(host.RectangleToScreen(host.BarControls.First().Bounds),
            host.ComputeBarDockPreview(Point.Empty, new Size(100, 30), menu));
        Assert.Equal(itemBounds, menu.Items[0].Bounds);
        Assert.Same(manager, ((CommandBarPopupItem)menu.Items[0]).DropDown.Manager);
    }

    [Theory]
    [InlineData(DockState.Left)]
    [InlineData(DockState.Right)]
    public void SideMenuCaptionsFitHorizontallyAndOverflowRestores(DockState dock)
    {
        var bar = new CommandBar("menu", CommandBarType.MenuBar) { Dock = dock };
        var first = bar.Items.AddPopup("&Long menu caption");
        var second = bar.Items.AddPopup("&Edit");
        using var host = new DockHost();
        using var control = new CommandBarControl { Bar = bar };
        host.Controls.Add(control);
        control.Relayout();
        control.Height = 500;
        Assert.True(first.Bounds.Width > first.Bounds.Height * 2);
        Assert.Equal(first.Bounds.Width, second.Bounds.Width);
        Assert.True(first.Bounds.Top > 1); // docked gripper
        control.Height = first.Bounds.Height;
        Assert.Equal(2, control.OverflowItems.Count);
        Assert.All(control.BuildOverflowMenu().Items, item => Assert.IsType<CommandBarPopupItem>(item));
        control.Height = 500;
        Assert.Empty(control.OverflowItems);
        bar.AllowFloat = false;
        control.Relayout();
        Assert.Equal(1, first.Bounds.Top);
    }

    [Fact]
    public void FloatingMenuIsCompactAndReturnsToSavedEdgeAfterLayoutReload()
    {
        using var manager = new CommandBarManager();
        var bar = manager.AddBar("menu", CommandBarType.MenuBar);
        bar.Dock = DockState.Right;
        bar.Items.AddPopup("&File");
        bar.Items.AddPopup("&Edit");
        using var host = new DockHost { Edge = DockEdge.Right, Manager = manager };
        host.FloatBar(bar, new Point(80, 90));
        Assert.Equal(DockState.Floating, bar.Dock);
        using (var window = new FloatingWindow(bar, new Office2003Renderer(), host, null))
        {
            Assert.True(window.Width < 300);
            Assert.Equal(bar.Items[0].Bounds.Top, bar.Items[1].Bounds.Top);
            Assert.Empty(window.BarControl.OverflowItems);
        }
        string path = Path.Combine(Path.GetTempPath(), Guid.NewGuid() + ".json");
        try
        {
            manager.SaveLayout(path);
            manager.LoadLayout(path);
            var restored = manager.Bars.Single();
            Assert.Equal(DockState.Floating, restored.Dock);
            Assert.Equal(new Point(80, 90), restored.FloatingBounds.Location);
            host.DockBar(restored);
            Assert.Equal(DockState.Right, restored.Dock);
            Assert.Single(host.BarControls);
        }
        finally { File.Delete(path); }
    }

    private sealed class PreviewSite(IComponent component) : ISite
    {
        public IComponent Component => component;
        public IContainer? Container => null;
        public bool DesignMode => true;
        public string? Name { get; set; }
        public object? GetService(Type serviceType) => null;
    }
}
