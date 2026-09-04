using System.ComponentModel;
using System.ComponentModel.Design;
using System.IO;
using System.Windows.Forms;
using CommandBars.Imaging;
using System.Windows.Forms.Design;

namespace CommandBars.Design;

/// <summary>
/// UITypeEditor for <see cref="SvgImage.Browse"/>. Shows the "…" file-picker;
/// on picking a valid .svg file it loads the file's contents into the entry's
/// <see cref="SvgImage.Svg"/> property (and seeds an empty Key from the file
/// name), so markup never has to be pasted by hand.
/// </summary>
public sealed class SvgFileImportEditor : System.Windows.Forms.Design.FileNameEditor
{
    protected override void InitializeDialog(System.Windows.Forms.OpenFileDialog openFileDialog)
    {
        base.InitializeDialog(openFileDialog);
        openFileDialog.Title = "Import SVG image";
        openFileDialog.Filter = "SVG files (*.svg)|*.svg|All files (*.*)|*.*";
        openFileDialog.CheckFileExists = true;
    }

    public override object? EditValue(
        ITypeDescriptorContext? context, IServiceProvider provider, object? value)
    {
        var picked = base.EditValue(context, provider, value) as string;
        if (string.IsNullOrEmpty(picked) || context?.Instance is not SvgImage image)
            return value;

        try
        {
            string content = File.ReadAllText(picked);
            if (content.IndexOf("<svg", StringComparison.OrdinalIgnoreCase) < 0)
            {
                MessageBox.Show(
                    "That file doesn't look like an SVG document (no <svg> element found).",
                    "Import SVG", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return value;
            }

            image.Svg = content;
            if (string.IsNullOrWhiteSpace(image.Key))
                image.Key = Path.GetFileNameWithoutExtension(picked);
            return picked; // show the imported file path in the Browse cell
        }
        catch (Exception ex)
        {
            MessageBox.Show(
                "Couldn't read the file: " + ex.Message,
                "Import SVG", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return value;
        }
    }
}

/// <summary>
/// Collection editor for a <see cref="SvgImageList"/>'s entries. Lets you add,
/// remove, reorder, and rename SVG icons and edit their markup.
/// </summary>
public sealed class SvgImageCollectionEditor : CollectionEditor
{
    public SvgImageCollectionEditor(Type type) : base(type)
    {
    }

    protected override Type[] CreateNewItemTypes() => new[] { typeof(SvgImage) };

    protected override Type CreateCollectionItemType() => typeof(SvgImage);
}

/// <summary>
/// Design-time behavior for <see cref="SvgImageList"/>. Adds an "Import SVG
/// files…" verb that multi-selects .svg files and embeds their contents as
/// entries (keyed by file name) in one step — so filling the list doesn't rely
/// on a per-property file picker.
/// </summary>
public sealed class SvgImageListDesigner : ComponentDesigner
{
    public override DesignerVerbCollection Verbs { get; } = new();

    public override void Initialize(IComponent component)
    {
        base.Initialize(component);
        Verbs.Add(new DesignerVerb("Import SVG files…", OnImport));
        Verbs.Add(new DesignerVerb("Edit images…", OnEdit));
    }

    private void OnImport(object? sender, EventArgs e)
    {
        if (Component is not SvgImageList list)
            return;

        using var dialog = new OpenFileDialog
        {
            Title = "Import SVG images",
            Filter = "SVG files (*.svg)|*.svg|All files (*.*)|*.*",
            Multiselect = true,
            CheckFileExists = true,
        };

        if (dialog.ShowDialog() != DialogResult.OK)
            return;

        var changeService = GetService(typeof(IComponentChangeService)) as IComponentChangeService;
        var property = TypeDescriptor.GetProperties(list)["Images"];

        try
        {
            changeService?.OnComponentChanging(list, property);

            foreach (string file in dialog.FileNames)
            {
                try
                {
                    list.Images.Add(new SvgImage
                    {
                        Key = UniqueKey(list, Path.GetFileNameWithoutExtension(file)),
                        Svg = File.ReadAllText(file),
                    });
                }
                catch
                {
                    // Skip an unreadable file; keep importing the rest.
                }
            }

            changeService?.OnComponentChanged(list, property, null, null);
        }
        catch (CheckoutException)
        {
            // Source control declined the edit; nothing imported.
        }
    }

    private void OnEdit(object? sender, EventArgs e)
    {
        // Opens the same collection editor the Images property uses.
        var property = TypeDescriptor.GetProperties(Component)["Images"];
        if (property is null)
            return;
        if (property.GetEditor(typeof(System.Drawing.Design.UITypeEditor))
            is not System.Drawing.Design.UITypeEditor editor)
            return;
        var context = new EditorInvoker(this, property);
        editor.EditValue(context, context, property.GetValue(Component));
    }

    private static string UniqueKey(SvgImageList list, string baseKey)
    {
        if (string.IsNullOrWhiteSpace(baseKey))
            baseKey = "image";
        if (!list.Contains(baseKey))
            return baseKey;
        for (int i = 2; ; i++)
        {
            string candidate = baseKey + i;
            if (!list.Contains(candidate))
                return candidate;
        }
    }

    /// <summary>
    /// Minimal context/service-provider so a modal collection editor can be
    /// shown from a verb (there is no PropertyGrid in the loop). Unknown services
    /// fall through to the design host; change notifications mark the document dirty.
    /// </summary>
    private sealed class EditorInvoker : ITypeDescriptorContext,
        System.Windows.Forms.Design.IWindowsFormsEditorService, IServiceProvider
    {
        private readonly ComponentDesigner _designer;
        private readonly PropertyDescriptor _property;

        public EditorInvoker(ComponentDesigner designer, PropertyDescriptor property)
        {
            _designer = designer;
            _property = property;
        }

        private IComponent Component => _designer.Component;

        private IComponentChangeService? ChangeService
            => GetService(typeof(IComponentChangeService)) as IComponentChangeService;

        public IContainer Container => Component.Site?.Container!;
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

        public object? GetService(Type serviceType)
        {
            if (serviceType == typeof(System.Windows.Forms.Design.IWindowsFormsEditorService))
                return this;
            return Component.Site?.GetService(serviceType);
        }

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
