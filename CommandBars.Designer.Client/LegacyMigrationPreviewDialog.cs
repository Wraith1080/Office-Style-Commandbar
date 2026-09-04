using System.Drawing;
using System.Windows.Forms;
using CommandBars.Designer.Protocol;

namespace CommandBars.Designer.Client;

/// <summary>Explicit preview/acceptance step for one-way legacy conversion.</summary>
internal sealed class LegacyMigrationPreviewDialog : Form
{
    public LegacyMigrationPreviewDialog(LegacyMigrationPlan plan)
    {
        AutoScaleDimensions = new SizeF(96F, 96F);
        AutoScaleMode = AutoScaleMode.Dpi;
        Font = SystemFonts.MessageBoxFont;
        Text = "Migrate Existing Toolbar Definitions";
        StartPosition = FormStartPosition.CenterParent;
        MinimumSize = new Size(680, 420);
        Size = new Size(820, 560);
        ShowInTaskbar = false;
        MinimizeBox = false;
        MaximizeBox = true;

        var explanation = new Label
        {
            Dock = DockStyle.Top,
            Height = 62,
            Padding = new Padding(10),
            Text = "This design uses legacy full-item definitions. Review the proposed " +
                   "catalog conversion below. Migration is applied only after you choose Apply Migration.",
        };
        var list = new ListView
        {
            Dock = DockStyle.Fill,
            View = View.Details,
            FullRowSelect = true,
            GridLines = true,
        };
        list.Columns.Add("Result", 135);
        list.Columns.Add("Location", 255);
        list.Columns.Add("Details", 390);
        foreach (var change in plan.Changes)
        {
            var item = new ListViewItem(change.Kind.ToString());
            item.SubItems.Add(change.Location);
            item.SubItems.Add(change.Message);
            list.Items.Add(item);
        }
        foreach (var diagnostic in plan.MigrationDiagnostics)
        {
            var item = new ListViewItem(diagnostic.Severity + " / " + diagnostic.Code);
            item.SubItems.Add(diagnostic.Location);
            item.SubItems.Add(diagnostic.Message);
            list.Items.Add(item);
        }
        foreach (var diagnostic in plan.Validation.Diagnostics)
        {
            var item = new ListViewItem(diagnostic.Severity + " / " + diagnostic.Code);
            item.SubItems.Add(diagnostic.Location);
            item.SubItems.Add(diagnostic.Message);
            list.Items.Add(item);
        }

        var footer = new Panel { Dock = DockStyle.Bottom, Height = 52 };
        var status = new Label
        {
            Dock = DockStyle.Fill,
            Padding = new Padding(10, 0, 4, 0),
            TextAlign = ContentAlignment.MiddleLeft,
            Text = plan.CanApply
                ? plan.Changes.Count + " migration change(s) are ready to apply."
                : "Migration contains errors. Cancel and repair the legacy definitions first.",
            ForeColor = plan.CanApply ? SystemColors.ControlText : Color.Firebrick,
        };
        var buttons = new FlowLayoutPanel
        {
            Dock = DockStyle.Right,
            AutoSize = true,
            FlowDirection = FlowDirection.RightToLeft,
            Padding = new Padding(8),
        };
        var apply = new Button
        {
            Text = "Apply Migration",
            DialogResult = DialogResult.OK,
            AutoSize = true,
            Enabled = plan.CanApply,
        };
        var cancel = new Button { Text = "Cancel", DialogResult = DialogResult.Cancel, AutoSize = true };
        buttons.Controls.Add(apply);
        buttons.Controls.Add(cancel);
        footer.Controls.Add(status);
        footer.Controls.Add(buttons);

        Controls.Add(list);
        Controls.Add(explanation);
        Controls.Add(footer);
        AcceptButton = apply;
        CancelButton = cancel;
    }
}
