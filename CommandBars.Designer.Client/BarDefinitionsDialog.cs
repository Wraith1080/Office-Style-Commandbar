using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;
using CommandBars.Designer.Protocol;

namespace CommandBars.Designer.Client;

/// <summary>
/// The client-side (in-VS) editor for the whole design snapshot:
///  • a tree of bars → items → child items on the upper-left, with a toolstrip;
///  • a <em>Commands</em> palette on the lower-left — the catalog you fill once;
///  • a property grid on the right for whichever node or command is selected.
///
/// The palette is the heart of the "author once, place many" workflow: define a
/// command (id, text, icon, shortcut) once, then "Add to bar" drops a lightweight
/// item that only references it by id. Editing the command later updates every
/// bar that references it. Everything runs in the Visual Studio process, so
/// standard WinForms editing works and there is no server-process UI freeze.
/// </summary>
internal sealed class BarDefinitionsDialog : Form
{
    private readonly TreeView _tree;
    private readonly ListBox _cmdList;
    private readonly PropertyGrid _grid;

    /// <summary>The edited snapshot (edited in place; same instance passed in).</summary>
    public DesignSnapshot Snapshot { get; }

    private List<BarDefData> Bars => Snapshot.Bars;
    private List<CommandDefData> Commands => Snapshot.Commands;

    public BarDefinitionsDialog(DesignSnapshot snapshot)
    {
        Snapshot = snapshot ?? new DesignSnapshot();

        // Publish the connected SvgImageList's icons to the ImageKey picker for
        // this dialog's lifetime (the property grid edits plain POCOs, so the
        // editor reads the list from here). Cleared when the dialog closes.
        ImageKeyEditor.AmbientImages = Snapshot.Images;
        FormClosed += (_, _) => ImageKeyEditor.AmbientImages = null;

        AutoScaleMode = AutoScaleMode.Font;
        Font = SystemFonts.MessageBoxFont;

        Text = "Edit Toolbars and Menus";
        StartPosition = FormStartPosition.CenterParent;
        MinimumSize = new Size(820, 520);
        Size = new Size(960, 620);
        ShowInTaskbar = false;
        MinimizeBox = false;
        MaximizeBox = true;

        // Right = property grid; left = tree (top) over commands palette (bottom).
        var outer = new SplitContainer { Dock = DockStyle.Fill, FixedPanel = FixedPanel.Panel2 };
        var left = new SplitContainer { Dock = DockStyle.Fill, Orientation = Orientation.Horizontal };

        // --- bars tree ---
        _tree = new TreeView
        {
            Dock = DockStyle.Fill,
            HideSelection = false,
            FullRowSelect = true,
            ShowRootLines = true,
            PathSeparator = "/",
        };
        _tree.AfterSelect += (_, _) => _grid.SelectedObject = _tree.SelectedNode?.Tag;
        left.Panel1.Controls.Add(_tree);
        left.Panel1.Controls.Add(BuildBarsStrip());

        // --- commands palette ---
        _cmdList = new ListBox { Dock = DockStyle.Fill, IntegralHeight = false };
        _cmdList.SelectedIndexChanged += (_, _) =>
        {
            if (_cmdList.SelectedItem is CommandDefData cmd)
                _grid.SelectedObject = cmd;
        };
        _cmdList.DoubleClick += (_, _) => AddCommandToBar();
        left.Panel2.Controls.Add(_cmdList);
        left.Panel2.Controls.Add(BuildCommandsStrip());

        outer.Panel1.Controls.Add(left);

        // --- property grid ---
        _grid = new PropertyGrid
        {
            Dock = DockStyle.Fill,
            PropertySort = PropertySort.Categorized,
            ToolbarVisible = false,
        };
        _grid.PropertyValueChanged += (_, _) => OnGridValueChanged();
        outer.Panel2.Controls.Add(_grid);

        Controls.Add(outer);
        Controls.Add(BuildOkCancelPanel());

        Shown += (_, _) =>
        {
            try
            {
                outer.SplitterDistance = Math.Max(320, (int)(Width * 0.58));
                left.SplitterDistance = Math.Max(220, (int)(left.Height * 0.62));
            }
            catch { /* ignore invalid splitter distance during layout */ }
        };

        RebuildTree(selectFirst: true);
        RebuildCommandList();
    }

    // ---- toolstrips ----

    private ToolStrip BuildBarsStrip()
    {
        var strip = NewStrip();
        strip.Items.Add(MakeButton("Add Toolbar", (_, _) => AddBar(BarKind.Toolbar)));
        strip.Items.Add(MakeButton("Add Menu Bar", (_, _) => AddBar(BarKind.MenuBar)));

        var addItem = new ToolStripDropDownButton("Add Item")
        {
            DisplayStyle = ToolStripItemDisplayStyle.Text,
        };
        foreach (ItemKindData kind in Enum.GetValues(typeof(ItemKindData)))
        {
            var captured = kind;
            addItem.DropDownItems.Add(kind.ToString(), null, (_, _) => AddItem(captured));
        }
        strip.Items.Add(addItem);

        strip.Items.Add(new ToolStripSeparator());
        strip.Items.Add(MakeButton("Remove", (_, _) => RemoveSelectedNode()));
        strip.Items.Add(MakeButton("Move Up", (_, _) => MoveSelected(-1)));
        strip.Items.Add(MakeButton("Move Down", (_, _) => MoveSelected(+1)));
        return strip;
    }

    private ToolStrip BuildCommandsStrip()
    {
        var strip = NewStrip();
        strip.Items.Add(new ToolStripLabel("Commands:"));
        strip.Items.Add(MakeButton("Add Command", (_, _) => AddCommand()));
        strip.Items.Add(MakeButton("Remove Command", (_, _) => RemoveCommand()));
        strip.Items.Add(new ToolStripSeparator());
        strip.Items.Add(MakeButton("Add to Bar →", (_, _) => AddCommandToBar()));
        return strip;
    }

    private static ToolStrip NewStrip() => new()
    {
        Dock = DockStyle.Top,
        GripStyle = ToolStripGripStyle.Hidden,
        RenderMode = ToolStripRenderMode.System,
        ImageScalingSize = new Size(16, 16),
    };

    private static ToolStripButton MakeButton(string text, EventHandler onClick)
    {
        var button = new ToolStripButton(text) { DisplayStyle = ToolStripItemDisplayStyle.Text };
        button.Click += onClick;
        return button;
    }

    private Panel BuildOkCancelPanel()
    {
        var panel = new FlowLayoutPanel
        {
            Dock = DockStyle.Bottom,
            FlowDirection = FlowDirection.RightToLeft,
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            Padding = new Padding(8),
        };
        var ok = new Button { Text = "OK", DialogResult = DialogResult.OK, AutoSize = true };
        var cancel = new Button { Text = "Cancel", DialogResult = DialogResult.Cancel, AutoSize = true };
        panel.Controls.Add(ok);
        panel.Controls.Add(cancel);
        AcceptButton = ok;
        CancelButton = cancel;
        return panel;
    }

    // ---- grid change handling ----

    private void OnGridValueChanged()
    {
        // A command edit can change many item labels; a node edit changes one.
        RefreshSelectedNodeText();
        RefreshAllItemLabels();
        RebuildCommandList(keepSelection: true);
    }

    // ---- command palette ----

    private void RebuildCommandList(bool keepSelection = false)
    {
        object? selected = keepSelection ? _cmdList.SelectedItem : null;
        _cmdList.BeginUpdate();
        _cmdList.Items.Clear();
        foreach (var cmd in Commands)
            _cmdList.Items.Add(cmd);
        _cmdList.EndUpdate();
        if (selected != null)
        {
            int i = _cmdList.Items.IndexOf(selected);
            if (i >= 0) _cmdList.SelectedIndex = i;
        }
        // ListBox shows each item's ToString(); refresh after edits.
        _cmdList.Refresh();
    }

    private void AddCommand()
    {
        var cmd = new CommandDefData { Id = UniqueCommandId("command") };
        Commands.Add(cmd);
        RebuildCommandList();
        _cmdList.SelectedItem = cmd;
        _grid.SelectedObject = cmd;
    }

    private void RemoveCommand()
    {
        if (_cmdList.SelectedItem is CommandDefData cmd)
        {
            Commands.Remove(cmd);
            RebuildCommandList();
            RefreshAllItemLabels();
        }
    }

    private void AddCommandToBar()
    {
        if (_cmdList.SelectedItem is not CommandDefData cmd)
        {
            MessageBox.Show(this, "Select a command in the palette first.",
                "Add to Bar", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }
        var target = GetTargetItemCollection();
        if (target == null)
        {
            MessageBox.Show(this, "Select a toolbar, menu bar, or a popup/split item to add the command to.",
                "Add to Bar", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        // A reference: only CommandId (+ the command's default display style).
        // Text/icon/shortcut are inherited from the catalog command.
        var item = new ItemDefData
        {
            Kind = ItemKindData.Button,
            CommandId = cmd.Id,
            DisplayStyle = cmd.DisplayStyle,
        };
        target.Add(item);
        RebuildTree(select: item);
    }

    private string UniqueCommandId(string baseId)
    {
        bool Exists(string id) => Commands.Any(c => string.Equals(c.Id, id, StringComparison.Ordinal));
        if (!Exists(baseId)) return baseId;
        for (int i = 2; ; i++)
        {
            string candidate = baseId + i;
            if (!Exists(candidate)) return candidate;
        }
    }

    // ---- tree building ----

    private void RebuildTree(bool selectFirst = false, object? select = null)
    {
        _tree.BeginUpdate();
        _tree.Nodes.Clear();
        foreach (var bar in Bars)
            _tree.Nodes.Add(BuildBarNode(bar));
        _tree.ExpandAll();
        _tree.EndUpdate();

        if (select != null)
            SelectByTag(select);
        else if (selectFirst && _tree.Nodes.Count > 0)
            _tree.SelectedNode = _tree.Nodes[0];

        _grid.SelectedObject = _tree.SelectedNode?.Tag;
    }

    private TreeNode BuildBarNode(BarDefData bar)
    {
        var node = new TreeNode(bar.ToString()) { Tag = bar };
        foreach (var item in bar.Items)
            node.Nodes.Add(BuildItemNode(item));
        return node;
    }

    private TreeNode BuildItemNode(ItemDefData item)
    {
        var node = new TreeNode(ItemLabel(item)) { Tag = item };
        foreach (var child in item.Items)
            node.Nodes.Add(BuildItemNode(child));
        return node;
    }

    // A referenced item (blank Text, has CommandId) shows the catalog command's
    // text so the tree reads meaningfully without restating it on the item.
    private string ItemLabel(ItemDefData item)
    {
        if (string.IsNullOrWhiteSpace(item.Text) && !string.IsNullOrWhiteSpace(item.CommandId))
        {
            var cmd = Commands.FirstOrDefault(c =>
                string.Equals(c.Id, item.CommandId, StringComparison.Ordinal));
            if (cmd != null)
                return $"{item.Kind}: {cmd} → {item.CommandId}";
        }
        return item.ToString();
    }

    private void RefreshAllItemLabels()
    {
        _tree.BeginUpdate();
        foreach (TreeNode barNode in _tree.Nodes)
            RefreshItemLabels(barNode.Nodes);
        _tree.EndUpdate();
    }

    private void RefreshItemLabels(TreeNodeCollection nodes)
    {
        foreach (TreeNode node in nodes)
        {
            if (node.Tag is ItemDefData item)
                node.Text = ItemLabel(item);
            RefreshItemLabels(node.Nodes);
        }
    }

    private void SelectByTag(object tag)
    {
        var found = FindNode(_tree.Nodes, tag);
        if (found != null)
            _tree.SelectedNode = found;
    }

    private static TreeNode? FindNode(TreeNodeCollection nodes, object tag)
    {
        foreach (TreeNode node in nodes)
        {
            if (ReferenceEquals(node.Tag, tag))
                return node;
            var child = FindNode(node.Nodes, tag);
            if (child != null)
                return child;
        }
        return null;
    }

    private void RefreshSelectedNodeText()
    {
        var node = _tree.SelectedNode;
        if (node?.Tag is BarDefData)
            node.Text = node.Tag.ToString();
        else if (node?.Tag is ItemDefData item)
            node.Text = ItemLabel(item);
    }

    // ---- structural edits ----

    private void AddBar(BarKind kind)
    {
        var bar = new BarDefData
        {
            BarType = kind,
            Name = UniqueBarName(kind == BarKind.MenuBar ? "MenuBar" : "Toolbar"),
            Text = kind == BarKind.MenuBar ? "Menu Bar" : "Toolbar",
            Dock = DockEdgeData.Top,
        };
        Bars.Add(bar);
        RebuildTree(select: bar);
    }

    private void AddItem(ItemKindData kind)
    {
        var target = GetTargetItemCollection();
        if (target == null)
        {
            MessageBox.Show(this, "Select a toolbar, menu bar, or a popup/split item first.",
                "Add Item", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }
        var item = new ItemDefData { Kind = kind };
        target.Add(item);
        RebuildTree(select: item);
    }

    // Where a new item should go, based on the current selection:
    //  - a bar node         -> that bar's Items
    //  - a popup/split node -> that item's Items
    //  - any other item     -> its parent collection (as a sibling)
    private List<ItemDefData>? GetTargetItemCollection()
    {
        var node = _tree.SelectedNode;
        if (node?.Tag is BarDefData bar)
            return bar.Items;
        if (node?.Tag is ItemDefData item)
        {
            if (item.CanHaveChildren)
                return item.Items;
            return GetChildCollectionOf(node.Parent);
        }
        return null;
    }

    private List<ItemDefData>? GetChildCollectionOf(TreeNode? node)
        => node?.Tag switch
        {
            BarDefData bar => bar.Items,
            ItemDefData item => item.Items,
            _ => null,
        };

    private void RemoveSelectedNode()
    {
        var node = _tree.SelectedNode;
        if (node?.Tag is null)
            return;

        if (node.Tag is BarDefData bar)
            Bars.Remove(bar);
        else if (node.Tag is ItemDefData item)
            GetChildCollectionOf(node.Parent)?.Remove(item);

        RebuildTree(selectFirst: true);
    }

    private void MoveSelected(int delta)
    {
        var node = _tree.SelectedNode;
        if (node?.Tag is null)
            return;

        if (node.Tag is BarDefData bar)
        {
            Reorder(Bars, bar, delta);
        }
        else if (node.Tag is ItemDefData item)
        {
            var siblings = GetChildCollectionOf(node.Parent);
            if (siblings != null)
                Reorder(siblings, item, delta);
        }
        RebuildTree(select: node.Tag);
    }

    private static void Reorder<T>(List<T> list, T value, int delta)
    {
        int index = list.IndexOf(value);
        if (index < 0)
            return;
        int target = index + delta;
        if (target < 0 || target >= list.Count)
            return;
        list.RemoveAt(index);
        list.Insert(target, value);
    }

    private string UniqueBarName(string baseName)
    {
        bool Exists(string name) => Bars.Exists(b =>
            string.Equals(b.Name, name, StringComparison.Ordinal));
        if (!Exists(baseName))
            return baseName;
        for (int i = 2; ; i++)
        {
            string candidate = baseName + i;
            if (!Exists(candidate))
                return candidate;
        }
    }
}
