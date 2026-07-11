using System;
using System.ComponentModel;
using System.Drawing;
using System.Drawing.Design;
using System.IO;
using System.Windows.Forms;
using System.Windows.Forms.Design;

namespace CommandBars.Designer.Client;

/// <summary>
/// Client-side editor for <c>SvgImage.Svg</c>. Shows a modal dialog with the SVG
/// markup and a "Load from file…" button that runs an OpenFileDialog IN THE
/// VISUAL STUDIO PROCESS (the client), reading the picked .svg on the client
/// side. This replaces the old server-process file dialog, which could freeze
/// the out-of-process designer. Returns the (possibly edited) markup string,
/// which the designer applies to the property.
/// </summary>
internal class SvgMarkupEditor : UITypeEditor
{
    public override UITypeEditorEditStyle GetEditStyle(ITypeDescriptorContext? context)
        => UITypeEditorEditStyle.Modal;

    public override object? EditValue(
        ITypeDescriptorContext? context, IServiceProvider provider, object? value)
    {
        if (provider is null)
            return value;

        var editorService = provider.GetService(typeof(IWindowsFormsEditorService)) as IWindowsFormsEditorService;
        if (editorService is null)
            return value;

        using var dialog = new SvgMarkupDialog(value as string ?? string.Empty);
        return editorService.ShowDialog(dialog) == DialogResult.OK ? dialog.Markup : value;
    }

    private sealed class SvgMarkupDialog : Form
    {
        private readonly TextBox _text;

        public string Markup => _text.Text;

        public SvgMarkupDialog(string markup)
        {
            Text = "Edit SVG Markup";
            StartPosition = FormStartPosition.CenterParent;
            MinimumSize = new Size(560, 380);
            Size = new Size(640, 460);
            ShowInTaskbar = false;
            MinimizeBox = false;

            _text = new TextBox
            {
                Dock = DockStyle.Fill,
                Multiline = true,
                ScrollBars = ScrollBars.Both,
                WordWrap = false,
                AcceptsReturn = true,
                AcceptsTab = true,
                Font = new Font(FontFamily.GenericMonospace, 9f),
                Text = markup,
            };

            var top = new FlowLayoutPanel
            {
                Dock = DockStyle.Top,
                AutoSize = true,
                Padding = new Padding(4),
            };
            var load = new Button { Text = "Load from file…", AutoSize = true };
            load.Click += (_, _) => LoadFromFile();
            top.Controls.Add(load);

            var bottom = new FlowLayoutPanel
            {
                Dock = DockStyle.Bottom,
                FlowDirection = FlowDirection.RightToLeft,
                AutoSize = true,
                Padding = new Padding(8),
            };
            var ok = new Button { Text = "OK", DialogResult = DialogResult.OK, AutoSize = true };
            var cancel = new Button { Text = "Cancel", DialogResult = DialogResult.Cancel, AutoSize = true };
            bottom.Controls.Add(ok);
            bottom.Controls.Add(cancel);
            AcceptButton = ok;
            CancelButton = cancel;

            Controls.Add(_text);
            Controls.Add(top);
            Controls.Add(bottom);
        }

        private void LoadFromFile()
        {
            using var open = new OpenFileDialog
            {
                Title = "Load SVG markup",
                Filter = "SVG files (*.svg)|*.svg|All files (*.*)|*.*",
                CheckFileExists = true,
            };
            if (open.ShowDialog(this) != DialogResult.OK)
                return;

            try
            {
                string content = File.ReadAllText(open.FileName);
                if (content.IndexOf("<svg", StringComparison.OrdinalIgnoreCase) < 0)
                {
                    MessageBox.Show(this,
                        "That file doesn't look like an SVG document (no <svg> element found).",
                        "Load SVG", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return;
                }
                _text.Text = content;
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, "Couldn't read the file: " + ex.Message,
                    "Load SVG", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
        }
    }
}
