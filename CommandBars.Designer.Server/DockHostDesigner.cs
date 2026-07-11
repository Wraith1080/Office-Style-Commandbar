using System.ComponentModel;
using System.ComponentModel.Design;
using CommandBars.Controls;
using Microsoft.DotNet.DesignTools.Designers;

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
}
