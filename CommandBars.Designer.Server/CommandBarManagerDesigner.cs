using System.ComponentModel;
using System.ComponentModel.Design;
using CommandBars.Rendering;
using Microsoft.DotNet.DesignTools.Designers;
using Microsoft.DotNet.DesignTools.Designers.Actions;

namespace CommandBars.Designer.Server;

/// <summary>
/// Out-of-process design-time behavior for <see cref="CommandBarManager"/>.
/// Runs inside the WinForms DesignToolsServer process, so — unlike the old
/// in-process designer in <c>CommandBars.Design</c>, which Visual Studio never
/// loads — its smart tag and change-tracking actually work.
///
/// Provides a smart-tag / context-menu action list ("Edit toolbars and menus…",
/// "Refresh design preview", plus the Theme picker) and refreshes the live
/// preview whenever anything on the design surface changes, so editing a
/// definition property (e.g. a toolbar's IconSize) updates the hosted bands
/// right away instead of only after the designer is closed and reopened.
/// </summary>
public class CommandBarManagerDesigner : ComponentDesigner
{
    private IComponentChangeService? _changeService;

    public override DesignerActionListCollection ActionLists
        => new()
        {
            new ManagerActionList(this)
        };

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
        => RefreshPreview();

    internal void RefreshPreview()
    {
        if (Component is CommandBarManager manager)
        {
            try { manager.RefreshDesignPreview(); }
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

    /// <summary>
    /// The manager's smart-tag panel. Method items marked as designer verbs also
    /// appear in the component's context menu.
    /// </summary>
    private class ManagerActionList : DesignerActionList
    {
        private const string CategoryName = "CommandBars";

        private readonly CommandBarManagerDesigner _designer;

        public ManagerActionList(CommandBarManagerDesigner designer)
            : base(designer.Component)
        {
            _designer = designer;
        }

        /// <summary>
        /// Theme, proxied through TypeDescriptor so the property browser is
        /// notified of the change (setting the CLR property directly would not
        /// update the grid).
        /// </summary>
        public CommandBarTheme Theme
        {
            get => ((CommandBarManager)Component!).Theme;
            set => TypeDescriptor.GetProperties(Component!)[nameof(Theme)]!
                .SetValue(Component, value);
        }

        /// <summary>Opens the catalog-first editor registered on BarDefinitions.</summary>
        public void EditToolbars()
            => _designer.InvokePropertyEditor(nameof(CommandBarManager.BarDefinitions));

        /// <summary>Rebuilds the design preview in every registered dock host.</summary>
        public void RefreshPreview()
            => _designer.RefreshPreview();

        public override DesignerActionItemCollection GetSortedActionItems()
        {
            DesignerActionItemCollection items = new();

            items.Add(new DesignerActionHeaderItem(CategoryName));

            items.Add(new DesignerActionPropertyItem(
                nameof(Theme),
                "Theme",
                CategoryName,
                "The visual style applied to every bar this manager owns."));

            items.Add(new DesignerActionMethodItem(
                this,
                nameof(EditToolbars),
                "Edit command catalog, toolbars and menus…",
                CategoryName,
                "Opens the catalog-first Commands and Bars and Menus editor.",
                true));

            items.Add(new DesignerActionMethodItem(
                this,
                nameof(RefreshPreview),
                "Refresh design preview",
                CategoryName,
                "Re-realizes the bar definitions into the dock hosts on the design surface.",
                true));

            return items;
        }
    }
}
