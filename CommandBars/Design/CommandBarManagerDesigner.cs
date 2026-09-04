using System.ComponentModel;
using System.ComponentModel.Design;
using System.Drawing.Design;
using System.Windows.Forms;
using System.Windows.Forms.Design;

namespace CommandBars.Design;

/// <summary>
/// Legacy in-process fallback design behavior for <see cref="CommandBarManager"/>.
/// Its verb invokes the routed catalog-first editor registered on
/// <see cref="CommandBarManager.BarDefinitions"/>; it does not expose the old
/// full-item collection editor.
/// </summary>
public sealed class CommandBarManagerDesigner : ComponentDesigner
{
    private IComponentChangeService? _changeService;

    public override DesignerVerbCollection Verbs { get; } = new();

    public override void Initialize(IComponent component)
    {
        base.Initialize(component);
        Verbs.Add(new DesignerVerb(
            "Edit command catalog, toolbars and menus…",
            OnEditToolbars));

        // Refresh the live preview whenever anything on the surface changes, so
        // editing a definition property (e.g. a toolbar's IconSize) or the icon
        // list updates the hosted bands right away instead of only after the
        // designer is closed and reopened.
        _changeService = GetService(typeof(IComponentChangeService)) as IComponentChangeService;
        if (_changeService is not null)
        {
            _changeService.ComponentChanged += OnComponentChanged;
            _changeService.ComponentAdded += OnComponentChanged;
            _changeService.ComponentRemoved += OnComponentChanged;
        }
    }

    private void OnComponentChanged(object? sender, EventArgs e)
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
            _changeService.ComponentChanged -= OnComponentChanged;
            _changeService.ComponentAdded -= OnComponentChanged;
            _changeService.ComponentRemoved -= OnComponentChanged;
            _changeService = null;
        }
        base.Dispose(disposing);
    }

    private void OnEditToolbars(object? sender, EventArgs e)
        => EditCollection("BarDefinitions");

    /// <summary>
    /// Opens the UITypeEditor registered on a named property of the component,
    /// supplying an editor-service context because there is no PropertyGrid in
    /// the verb invocation path.
    /// </summary>
    private void EditCollection(string propertyName)
    {
        var prop = TypeDescriptor.GetProperties(Component)[propertyName];
        if (prop is null)
            return;

        if (prop.GetEditor(typeof(UITypeEditor)) is not UITypeEditor editor)
            return;

        var context = new EditorContext(this, prop);
        object? value = prop.GetValue(Component);
        editor.EditValue(context, context, value);
    }

    /// <summary>
    /// A minimal <see cref="ITypeDescriptorContext"/> that also acts as the
    /// <see cref="IWindowsFormsEditorService"/> and service provider a
    /// a routed modal editor needs when invoked outside the grid.
    /// Unknown service requests fall through to the design host, and component
    /// change notifications are routed so edits mark the document dirty.
    /// </summary>
    private sealed class EditorContext : ITypeDescriptorContext, IWindowsFormsEditorService, IServiceProvider
    {
        private readonly ComponentDesigner _designer;
        private readonly PropertyDescriptor _property;

        public EditorContext(ComponentDesigner designer, PropertyDescriptor property)
        {
            _designer = designer;
            _property = property;
        }

        private IComponent Component => _designer.Component;

        private IComponentChangeService? ChangeService
            => GetService(typeof(IComponentChangeService)) as IComponentChangeService;

        // --- ITypeDescriptorContext ---
        public IContainer? Container => Component.Site?.Container;
        public object Instance => Component;
        public PropertyDescriptor PropertyDescriptor => _property;

        public bool OnComponentChanging()
        {
            try
            {
                ChangeService?.OnComponentChanging(Component, _property);
                return true;
            }
            catch (CheckoutException)
            {
                return false;
            }
        }

        public void OnComponentChanged()
            => ChangeService?.OnComponentChanged(Component, _property, null, null);

        // --- IServiceProvider ---
        public object? GetService(Type serviceType)
        {
            if (serviceType == typeof(IWindowsFormsEditorService))
                return this;
            return Component.Site?.GetService(serviceType);
        }

        // --- IWindowsFormsEditorService ---
        public void CloseDropDown() { }

        public void DropDownControl(Control? control) { }

        public DialogResult ShowDialog(Form dialog)
        {
            if (GetService(typeof(IUIService)) is IUIService ui)
                return ui.ShowDialog(dialog);
            return dialog.ShowDialog();
        }
    }
}
