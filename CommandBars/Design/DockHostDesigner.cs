using System.ComponentModel;
using System.ComponentModel.Design;
using System.Windows.Forms.Design;
using CommandBars.Controls;

namespace CommandBars.Design;

/// <summary>
/// Design-time behavior for <see cref="DockHost"/>. Listens to the design
/// environment's change service and refreshes this band's preview whenever
/// anything on the surface changes — so editing a definition property such as a
/// toolbar's IconSize (in the collection editor or the grid) re-measures and
/// repaints the hosted bars immediately, rather than only after the host is
/// clicked or the designer is reopened.
///
/// Each host refreshes itself, so this works even before the host has registered
/// with its manager and even while a modal collection editor is open.
/// </summary>
public sealed class DockHostDesigner : ControlDesigner
{
    private IComponentChangeService? _changeService;

    public override void Initialize(IComponent component)
    {
        base.Initialize(component);
        _changeService = GetService(typeof(IComponentChangeService)) as IComponentChangeService;
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
            host.RefreshDesignPreview();
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
