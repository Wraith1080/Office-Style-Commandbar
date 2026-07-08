using System.ComponentModel;
using System.Drawing;
using System.Drawing.Design;
using System.Windows.Forms;
using System.Windows.Forms.Design;
using CommandBars.Imaging;

namespace CommandBars.Design;

/// <summary>
/// A <see cref="UITypeEditor"/> for the <c>ImagePath</c> string properties on
/// item definitions. It derives from <see cref="FileNameEditor"/> so the
/// Properties grid always shows the "…" ellipsis button and a real Open dialog
/// (SVG preferred, common rasters accepted), and it paints a live thumbnail of
/// the chosen icon next to the value.
///
/// When the picked file lives inside the project (any folder at or under the
/// directory that contains the <c>.csproj</c>), the stored value is made
/// relative to the project folder so the reference stays portable; otherwise the
/// absolute path is kept.
/// </summary>
public sealed class SvgImageEditor : FileNameEditor
{
    /// <summary>Configure the inherited Open dialog with an image filter.</summary>
    protected override void InitializeDialog(OpenFileDialog openFileDialog)
    {
        base.InitializeDialog(openFileDialog);
        openFileDialog.Title = "Select button image";
        openFileDialog.Filter =
            "Image files|*.svg;*.png;*.ico;*.bmp;*.jpg;*.jpeg;*.gif|" +
            "SVG vector (*.svg)|*.svg|" +
            "Raster images|*.png;*.ico;*.bmp;*.jpg;*.jpeg;*.gif|" +
            "All files (*.*)|*.*";
        openFileDialog.CheckFileExists = true;
    }

    public override object? EditValue(
        ITypeDescriptorContext? context, IServiceProvider provider, object? value)
    {
        object? result = base.EditValue(context, provider, value);

        // FileNameEditor returns the picked absolute path. Store it project-
        // relative when it lives inside the project; drop any stale cache entry
        // so a re-pick of a changed file re-rasterizes.
        if (result is string picked && picked.Length > 0)
        {
            DesignImage.ClearCache();
            return DesignImage.Relativize(picked);
        }

        return result;
    }

    /// <summary>We can paint a preview swatch for the chosen path.</summary>
    public override bool GetPaintValueSupported(ITypeDescriptorContext? context) => true;

    public override void PaintValue(PaintValueEventArgs e)
    {
        if (e.Value is not string path || string.IsNullOrWhiteSpace(path))
            return;

        IImageSource? source = DesignImage.Load(path);
        if (source is null)
            return;

        try
        {
            Rectangle b = e.Bounds;
            int size = Math.Max(1, Math.Min(b.Width, b.Height));
            Image image = source.GetImage(size);
            int x = b.X + (b.Width - size) / 2;
            int y = b.Y + (b.Height - size) / 2;
            e.Graphics.DrawImage(image, new Rectangle(x, y, size, size));
        }
        catch
        {
            // Never let a bad image abort the property grid's paint pass.
        }
    }
}
