using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;
using CommandBars.Designer.Protocol;

namespace CommandBars.Designer.Client;

internal sealed class CatalogIssuesDialog : Form
{
    public CatalogIssuesDialog(IEnumerable<CatalogDiagnostic> diagnostics)
    {
        AutoScaleDimensions = new SizeF(96F, 96F);
        AutoScaleMode = AutoScaleMode.Dpi;
        Font = SystemFonts.MessageBoxFont;
        Text = "Catalog Validation";
        StartPosition = FormStartPosition.CenterParent;
        MinimumSize = new Size(620, 360);
        Size = new Size(760, 480);
        ShowInTaskbar = false;
        MinimizeBox = false;
        MaximizeBox = true;

        var list = new ListView
        {
            Dock = DockStyle.Fill,
            View = View.Details,
            FullRowSelect = true,
            GridLines = true,
        };
        list.Columns.Add("Severity", 85);
        list.Columns.Add("Location", 250);
        list.Columns.Add("Message", 400);
        foreach (var diagnostic in diagnostics)
        {
            var item = new ListViewItem(diagnostic.Severity.ToString());
            item.SubItems.Add(diagnostic.Location);
            item.SubItems.Add(diagnostic.Message);
            list.Items.Add(item);
        }
        var close = new Button
        {
            Dock = DockStyle.Bottom,
            Height = 34,
            Text = "Close",
            DialogResult = DialogResult.Cancel,
        };
        Controls.Add(list);
        Controls.Add(close);
        CancelButton = close;
    }
}
