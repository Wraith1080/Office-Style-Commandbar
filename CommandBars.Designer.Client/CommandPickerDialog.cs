using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Windows.Forms;
using CommandBars.Designer.Protocol;

namespace CommandBars.Designer.Client;

/// <summary>Reusable, target-aware multi-select picker for catalog placements.</summary>
internal sealed class CommandPickerDialog : Form
{
    private readonly DesignSnapshot _snapshot;
    private readonly CommandPlacementTargetData _target;
    private readonly TextBox _search;
    private readonly ListView _list;
    private readonly Button _add;
    private readonly ImageList _images;

    public CommandPickerDialog(
        DesignSnapshot snapshot,
        CommandPlacementTargetData target,
        string title)
    {
        _snapshot = snapshot;
        _target = target;
        AutoScaleDimensions = new SizeF(96F, 96F);
        AutoScaleMode = AutoScaleMode.Dpi;
        Font = SystemFonts.MessageBoxFont;
        Text = title;
        StartPosition = FormStartPosition.CenterParent;
        MinimumSize = new Size(560, 400);
        Size = new Size(680, 520);
        ShowInTaskbar = false;
        MinimizeBox = false;
        MaximizeBox = true;

        _search = new TextBox { Dock = DockStyle.Fill };
        _images = BuildImages(snapshot.Images);
        _list = new ListView
        {
            Dock = DockStyle.Fill,
            View = View.Details,
            FullRowSelect = true,
            HideSelection = false,
            MultiSelect = true,
            SmallImageList = _images,
        };
        _list.Columns.Add("Command", 250);
        _list.Columns.Add("Kind", 110);
        _list.Columns.Add("Id", 230);

        var searchPanel = new TableLayoutPanel
        {
            Dock = DockStyle.Top,
            Height = 38,
            ColumnCount = 2,
            Padding = new Padding(8, 7, 8, 4),
        };
        searchPanel.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        searchPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        searchPanel.Controls.Add(new Label
        {
            Text = "Search:",
            Anchor = AnchorStyles.Left,
            AutoSize = true,
        }, 0, 0);
        searchPanel.Controls.Add(_search, 1, 0);

        var targetLabel = new Label
        {
            Dock = DockStyle.Top,
            Height = 30,
            Padding = new Padding(8, 7, 8, 2),
            Text = "Showing commands compatible with this " +
                   CommandPlacementRulesData.GetTargetName(target) + ".",
        };

        var buttons = new FlowLayoutPanel
        {
            Dock = DockStyle.Bottom,
            Height = 48,
            FlowDirection = FlowDirection.RightToLeft,
            Padding = new Padding(8),
        };
        _add = new Button
        {
            Text = "Add Selected",
            DialogResult = DialogResult.OK,
            AutoSize = true,
            Enabled = false,
        };
        var cancel = new Button { Text = "Cancel", DialogResult = DialogResult.Cancel, AutoSize = true };
        buttons.Controls.Add(_add);
        buttons.Controls.Add(cancel);

        Controls.Add(_list);
        Controls.Add(searchPanel);
        Controls.Add(targetLabel);
        Controls.Add(buttons);
        AcceptButton = _add;
        CancelButton = cancel;

        _search.TextChanged += (_, _) => RebuildList();
        _list.SelectedIndexChanged += (_, _) => _add.Enabled = _list.SelectedItems.Count > 0;
        _list.DoubleClick += (_, _) =>
        {
            if (_list.SelectedItems.Count > 0)
            {
                DialogResult = DialogResult.OK;
                Close();
            }
        };
        Shown += (_, _) =>
        {
            ApplyDpiLayout();
            _search.Focus();
        };
        DpiChanged += (_, _) => BeginInvoke((Action)ApplyDpiLayout);
        FormClosed += (_, _) => _images.Dispose();
        RebuildList();
    }

    public IReadOnlyList<CommandDefData> SelectedCommands
        => _list.Items.Cast<ListViewItem>()
            .Where(item => item.Selected)
            .Select(item => (CommandDefData)item.Tag)
            .ToList();

    private void RebuildList()
    {
        string query = _search.Text.Trim();
        _list.BeginUpdate();
        _list.Items.Clear();
        foreach (var command in _snapshot.Commands.Where(command =>
            !string.IsNullOrWhiteSpace(command.Id) &&
            CommandPlacementRulesData.CanPlace(command.Kind, _target) &&
            Matches(command, query)))
        {
            string text = string.IsNullOrWhiteSpace(command.Text)
                ? command.Id
                : command.Text.Replace("&", string.Empty);
            var item = new ListViewItem(text) { Tag = command };
            item.SubItems.Add(command.Kind.ToString());
            item.SubItems.Add(command.Id);
            if (!string.IsNullOrWhiteSpace(command.ImageKey) &&
                _images.Images.ContainsKey(command.ImageKey))
                item.ImageKey = command.ImageKey;
            _list.Items.Add(item);
        }
        _list.EndUpdate();
        _add.Enabled = false;
    }

    private static bool Matches(CommandDefData command, string query)
    {
        if (query.Length == 0)
            return true;
        return command.Id.IndexOf(query, StringComparison.OrdinalIgnoreCase) >= 0 ||
               command.Text.IndexOf(query, StringComparison.OrdinalIgnoreCase) >= 0 ||
               command.Kind.ToString().IndexOf(query, StringComparison.OrdinalIgnoreCase) >= 0;
    }

    private static ImageList BuildImages(IEnumerable<ImageEntryData> entries)
    {
        var images = new ImageList
        {
            ColorDepth = ColorDepth.Depth32Bit,
            ImageSize = new Size(20, 20),
            TransparentColor = Color.Transparent,
        };
        foreach (var entry in entries)
        {
            if (string.IsNullOrWhiteSpace(entry.Key) || string.IsNullOrWhiteSpace(entry.Png))
                continue;
            try
            {
                byte[] bytes = Convert.FromBase64String(entry.Png);
                using var stream = new MemoryStream(bytes);
                using var source = Image.FromStream(stream);
                images.Images.Add(entry.Key, new Bitmap(source));
            }
            catch
            {
                // A bad preview must not make otherwise valid catalog data unpickable.
            }
        }
        return images;
    }

    private void ApplyDpiLayout()
    {
        double scale = DeviceDpi / 96d;
        _images.ImageSize = new Size(
            Math.Max(16, (int)Math.Round(20 * scale)),
            Math.Max(16, (int)Math.Round(20 * scale)));
        int available = Math.Max(360, _list.ClientSize.Width - 8);
        _list.Columns[0].Width = (int)(available * 0.42);
        _list.Columns[1].Width = (int)(available * 0.18);
        _list.Columns[2].Width = available - _list.Columns[0].Width - _list.Columns[1].Width;
    }
}
