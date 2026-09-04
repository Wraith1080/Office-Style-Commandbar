using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;
using CommandBars.Designer.Protocol;

namespace CommandBars.Designer.Client;

internal sealed class NewBarDialog : Form
{
    private readonly TextBox _name;
    private readonly TextBox _caption;
    private readonly HashSet<string> _existingNames;
    private readonly BarKind _kind;
    private readonly DockEdgeData _dock;

    public NewBarDialog(
        BarKind kind,
        DockEdgeData dock,
        IEnumerable<string> existingNames)
    {
        _kind = kind;
        _dock = dock;
        _existingNames = new HashSet<string>(existingNames, StringComparer.Ordinal);
        string stem = kind == BarKind.MenuBar ? "MenuBar" : "Toolbar";
        string name = UniqueName(stem);

        AutoScaleDimensions = new SizeF(96F, 96F);
        AutoScaleMode = AutoScaleMode.Dpi;
        Font = SystemFonts.MessageBoxFont;
        Text = kind == BarKind.MenuBar ? "Add Menu Bar" : "Add Toolbar";
        StartPosition = FormStartPosition.CenterParent;
        FormBorderStyle = FormBorderStyle.FixedDialog;
        ClientSize = new Size(430, 158);
        MinimizeBox = false;
        MaximizeBox = false;
        ShowInTaskbar = false;

        _name = new TextBox { Dock = DockStyle.Fill, Text = name };
        _caption = new TextBox
        {
            Dock = DockStyle.Fill,
            Text = kind == BarKind.MenuBar ? "Menu Bar" : SplitName(name),
        };
        var fields = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 2,
            RowCount = 3,
            Padding = new Padding(10),
        };
        fields.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        fields.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        fields.Controls.Add(FieldLabel("Name:"), 0, 0);
        fields.Controls.Add(_name, 1, 0);
        fields.Controls.Add(FieldLabel("Caption:"), 0, 1);
        fields.Controls.Add(_caption, 1, 1);
        fields.Controls.Add(new Label
        {
            Text = "Initial dock: " + dock,
            AutoSize = true,
            ForeColor = SystemColors.GrayText,
            Anchor = AnchorStyles.Left,
        }, 1, 2);

        var buttons = new FlowLayoutPanel
        {
            Dock = DockStyle.Bottom,
            Height = 46,
            FlowDirection = FlowDirection.RightToLeft,
            Padding = new Padding(8),
        };
        var add = new Button { Text = "Add", DialogResult = DialogResult.OK, AutoSize = true };
        var cancel = new Button { Text = "Cancel", DialogResult = DialogResult.Cancel, AutoSize = true };
        buttons.Controls.Add(add);
        buttons.Controls.Add(cancel);
        Controls.Add(fields);
        Controls.Add(buttons);
        AcceptButton = add;
        CancelButton = cancel;
        FormClosing += ValidateClosing;
    }

    public BarDefData CreatedBar => new()
    {
        Name = _name.Text.Trim(),
        Text = _caption.Text.Trim(),
        BarType = _kind,
        Dock = _dock,
    };

    private void ValidateClosing(object? sender, FormClosingEventArgs e)
    {
        if (DialogResult != DialogResult.OK)
            return;
        string name = _name.Text.Trim();
        if (name.Length > 0 && !_existingNames.Contains(name))
            return;
        MessageBox.Show(this,
            name.Length == 0
                ? "Enter a stable name for the bar."
                : "A bar named '" + name + "' already exists.",
            Text, MessageBoxButtons.OK, MessageBoxIcon.Warning);
        e.Cancel = true;
        DialogResult = DialogResult.None;
    }

    private string UniqueName(string stem)
    {
        if (!_existingNames.Contains(stem))
            return stem;
        for (int number = 2; ; number++)
        {
            string candidate = stem + number;
            if (!_existingNames.Contains(candidate))
                return candidate;
        }
    }

    private static Label FieldLabel(string text) => new()
    {
        Text = text,
        AutoSize = true,
        Anchor = AnchorStyles.Left,
        Margin = new Padding(0, 4, 10, 4),
    };

    private static string SplitName(string value)
    {
        var result = new List<char>();
        foreach (char character in value)
        {
            if (result.Count > 0 && char.IsUpper(character))
                result.Add(' ');
            result.Add(character);
        }
        return new string(result.ToArray());
    }
}

internal sealed class BarTargetDialog : Form
{
    private readonly ListBox _bars;
    private readonly Button _choose;

    public BarTargetDialog(IEnumerable<BarDefData> bars, DockEdgeData edge)
    {
        AutoScaleDimensions = new SizeF(96F, 96F);
        AutoScaleMode = AutoScaleMode.Dpi;
        Font = SystemFonts.MessageBoxFont;
        Text = "Choose a Bar";
        StartPosition = FormStartPosition.CenterParent;
        MinimumSize = new Size(430, 300);
        Size = new Size(520, 390);
        ShowInTaskbar = false;
        MinimizeBox = false;
        MaximizeBox = true;

        _bars = new ListBox
        {
            Dock = DockStyle.Fill,
            IntegralHeight = false,
        };
        foreach (var bar in bars)
            _bars.Items.Add(bar);

        var explanation = new Label
        {
            Dock = DockStyle.Top,
            Height = 48,
            Padding = new Padding(9),
            Text = "Choose a visible bar currently previewed in the " + edge + " host.",
        };
        var buttons = new FlowLayoutPanel
        {
            Dock = DockStyle.Bottom,
            Height = 48,
            FlowDirection = FlowDirection.RightToLeft,
            Padding = new Padding(8),
        };
        _choose = new Button
        {
            Text = "Choose",
            DialogResult = DialogResult.OK,
            AutoSize = true,
            Enabled = false,
        };
        var cancel = new Button { Text = "Cancel", DialogResult = DialogResult.Cancel, AutoSize = true };
        buttons.Controls.Add(_choose);
        buttons.Controls.Add(cancel);
        Controls.Add(_bars);
        Controls.Add(explanation);
        Controls.Add(buttons);
        AcceptButton = _choose;
        CancelButton = cancel;

        _bars.SelectedIndexChanged += (_, _) => _choose.Enabled = _bars.SelectedItem is not null;
        _bars.DoubleClick += (_, _) =>
        {
            if (_bars.SelectedItem is not null)
            {
                DialogResult = DialogResult.OK;
                Close();
            }
        };
        if (_bars.Items.Count > 0)
            _bars.SelectedIndex = 0;
    }

    public BarDefData? SelectedBar => _bars.SelectedItem as BarDefData;
}
