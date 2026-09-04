using System.ComponentModel;
using System.ComponentModel.Design;
using CommandBars.Controls;
using CommandBars.Model;
using Microsoft.DotNet.DesignTools.Designers;
using Microsoft.DotNet.DesignTools.Designers.Actions;

namespace CommandBars.Designer.Server;

/// <summary>
/// Out-of-process design-time behavior for <see cref="DockHost"/>. Runs inside
/// the WinForms DesignToolsServer process (which is also where the control
/// instances on the design surface live), so it has full access to the real
/// <see cref="DockHost"/> — unlike the old in-process designer in
/// <c>CommandBars.Design</c>, which Visual Studio never loads.
///
/// Listens to the design environment's change service and refreshes this band's
/// preview whenever anything on the surface changes — so editing a definition
/// property such as a toolbar's IconSize (in the collection editor or the grid)
/// re-measures and repaints the hosted bars immediately, rather than only after
/// the host is clicked or the designer is reopened.
///
/// Each host refreshes itself, so this works even before the host has
/// registered with its manager and even while a modal collection editor is open.
/// </summary>
public class DockHostDesigner : ControlDesigner
{
    private IComponentChangeService? _changeService;

    public override DesignerActionListCollection ActionLists
        => new() { new DockHostActionList(this) };

    public override void Initialize(IComponent component)
    {
        base.Initialize(component);
        _changeService = component.Site?.GetService(typeof(IComponentChangeService))
            as IComponentChangeService;
        if (_changeService is not null)
        {
            _changeService.ComponentChanged += OnSurfaceChanged;
            _changeService.ComponentAdded += OnSurfaceChanged;
            _changeService.ComponentRemoved += OnSurfaceChanged;
        }
    }

    private void OnSurfaceChanged(object? sender, EventArgs e)
    {
        if (Control is DockHost host)
        {
            try { host.RefreshDesignPreview(); }
            catch { /* never break the designer over a preview refresh */ }
        }
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing && _changeService is not null)
        {
            _changeService.ComponentChanged -= OnSurfaceChanged;
            _changeService.ComponentAdded -= OnSurfaceChanged;
            _changeService.ComponentRemoved -= OnSurfaceChanged;
            _changeService = null;
        }
        base.Dispose(disposing);
    }

    private sealed class DockHostActionList : DesignerActionList
    {
        private const string CategoryName = "CommandBars";
        private readonly DockHostDesigner _designer;

        public DockHostActionList(DockHostDesigner designer)
            : base(designer.Component)
            => _designer = designer;

        private DockHost Host => (DockHost)Component!;

        public void AddToolbar()
            => _designer.InvokePropertyEditor(nameof(DockHost.DesignerAddToolbar));

        public void AddMenuBar()
            => _designer.InvokePropertyEditor(nameof(DockHost.DesignerAddMenuBar));

        public void AddCommands()
            => _designer.InvokePropertyEditor(nameof(DockHost.DesignerAddCommands));

        public void EditBars()
            => _designer.InvokePropertyEditor(nameof(DockHost.DesignerEditBars));

        public void EditCatalog()
            => _designer.InvokePropertyEditor(nameof(DockHost.DesignerEditCatalog));

        public void RefreshPreview()
        {
            if (Host.Manager is not null)
            {
                try { Host.Manager.RefreshDesignPreview(); }
                catch { /* preview only */ }
            }
            else
            {
                try { Host.RefreshDesignPreview(); }
                catch { /* preview only */ }
            }
        }

        public override DesignerActionItemCollection GetSortedActionItems()
        {
            DesignerActionItemCollection items = new();
            items.Add(new DesignerActionHeaderItem(CategoryName));

            if (Host.Manager is null)
            {
                items.Add(new DesignerActionTextItem(
                    "Assign a CommandBarManager to enable editing.",
                    CategoryName));
                items.Add(Method(nameof(RefreshPreview), "Refresh design preview",
                    "Repaints this empty host preview."));
                return items;
            }

            items.Add(Method(nameof(AddToolbar), "Add toolbar…",
                "Creates a toolbar initially docked to this host."));

            bool canAddMenu = Host.Edge == DockEdge.Top &&
                !Host.Manager.BarDefinitions.Any(definition =>
                    definition.BarType == CommandBarType.MenuBar);
            if (canAddMenu)
            {
                items.Add(Method(nameof(AddMenuBar), "Add menu bar…",
                    "Creates the manager's menu bar in this top host."));
            }

            items.Add(Method(nameof(AddCommands), "Add commands to…",
                "Chooses a bar in this host and adds catalog placements."));
            items.Add(Method(nameof(EditBars), "Edit bars and menus…",
                "Opens the manager editor on the Bars and Menus page."));
            items.Add(Method(nameof(EditCatalog), "Edit command catalog…",
                "Opens the manager editor on the Commands page."));
            items.Add(Method(nameof(RefreshPreview), "Refresh design preview",
                "Rebuilds every DockHost connected to this manager."));
            return items;
        }

        private DesignerActionMethodItem Method(
            string memberName,
            string displayName,
            string description)
            => new(
                this,
                memberName,
                displayName,
                CategoryName,
                description,
                true);
    }
}
