using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;
using CommandBars.Designer.Protocol;

namespace CommandBars.Designer.Client;

/// <summary>
/// A client-side (in-VS) editor dialog for the whole bar-definition tree:
/// toolbars and the menu bar on the left as a tree of bars → items → child
/// items, and a property grid on the right for the selected node. Because this
/// runs in the Visual Studio process (not the design server), standard WinForms
/// editing works normally and there is no risk of the server-process UI freeze.
///
/// The action strip is a <see cref="ToolStrip"/> (not a row of AutoSize buttons)
/// so it stays compact and lays out correctly under Per-Monitor high DPI,
/// collapsing extra commands into an overflow chevron instead of spilling.
/// </summary>
internal sealed class BarDefinitionsDialog : Form
{
    private readonly TreeView _tree;
    private readonly PropertyGrid _grid;

    /// <summary>The edited bars (edited in place; same list instance passed in).</summary>
    public List<BarDefData> Bars { get; }

    public BarDefinitionsDialog(List<BarDefData> bars)
    {
        Bars = bars ?? new List<BarDefData>();

        // Font-based autoscaling + the system dialog font make the whole form
        // (splitter, tree, grid, toolstrip) scale correctly at high DPI.
        AutoScaleMode = AutoScaleMode.Font;
        Font = SystemFonts.MessageBoxFont;

        Text = "Edit Toolbars and Menus";
        StartPosition = FormStartPosition.CenterParent;
        MinimumSize = new Size(760, 480);
        Size = new Size(880, 560);
        ShowInTaskbar = false;
        MinimizeBox = false;
        MaximizeBox = true;

        var split = new SplitContainer
        {
            Dock = DockStyle.Fill,
            FixedPanel = FixedPanel.Panel1,
        };

        _tree = new TreeView
        {
            Dock = DockStyle.Fill,
            HideSelection = false,
            PathSeparator = "/",
        };
        _tree.AfterSelect += (_, _) => _grid.SelectedObject = _tree.SelectedNode?.Tag;

        var strip = BuildActionStrip();

        // Add the Fill control first, then the docked strip — same order as the
        // (working) main OK/Cancel layout.
        split.Panel1.Controls.Add(_tree);
        split.Panel1.Controls.Add(strip);

        _grid = new PropertyGrid
        {
            Dock = DockStyle.Fill,
            PropertySort = PropertySort.Categorized,
            ToolbarVisible = false,
        };
        _grid.PropertyValueChanged += (_, _) => RefreshSelectedNodeText();
        split.Panel2.Controls.Add(_grid);

        var okCancel = BuildOkCancelPanel();

        Controls.Add(split);
        Controls.Add(okCancel);

        // Set the splitter distance after the form has its scaled size.
        Shown += (_, _) =>
        {
            try { split.SplitterDistance = Math.Max(200, (int)(Width * 0.42)); }
            catch { /* ignore invalid splitter distance during layout */ }
        };

        RebuildTree(selectFirst: true);
    }

    // ---- layout helpers ----

    private ToolStrip BuildActionStrip()
    {
        var strip = new ToolStrip
        {
            Dock = DockStyle.Top,
            GripStyle = ToolStripGripStyle.Hidden,
            RenderMode = ToolStripRenderMode.System,
            ImageScalingSize = new Size(16, 16),
        };

        strip.Items.Add(MakeStripButton("Add Toolbar", (_, _) => AddBar(BarKind.Toolbar)));
        strip.Items.Add(MakeStripButton("Add Menu Bar", (_, _) => AddBar(BarKind.MenuBar)));

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
        strip.Items.Add(MakeStripButton("Remove", (_, _) => RemoveSelected()));
        strip.Items.Add(MakeStripButton("Move Up", (_, _) => MoveSelected(-1)));
        strip.Items.Add(MakeStripButton("Move Down", (_, _) => MoveSelected(+1)));

        return strip;
    }

    private static ToolStripButton MakeStripButton(string text, EventHandler onClick)
    {
        var button = new ToolStripButton(text)
        {
            DisplayStyle = ToolStripItemDisplayStyle.Text,
        };
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

    private static TreeNode BuildBarNode(BarDefData bar)
    {
        var node = new TreeNode(bar.ToString()) { Tag = bar };
        foreach (var item in bar.Items)
            node.Nodes.Add(BuildItemNode(item));
        return node;
    }

    private static TreeNode BuildItemNode(ItemDefData item)
    {
        var node = new TreeNode(item.ToString()) { Tag = item };
        foreach (var child in item.Items)
            node.Nodes.Add(BuildItemNode(child));
        return node;
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
        if (node?.Tag != null)
            node.Text = node.Tag.ToString();
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
        var target = GetTargetItemCollection(out _);
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
    private List<ItemDefData>? GetTargetItemCollection(out object? owner)
    {
        owner = null;
        var node = _tree.SelectedNode;
        if (node?.Tag is BarDefData bar)
        {
            owner = bar;
            return bar.Items;
        }
        if (node?.Tag is ItemDefData item)
        {
            if (item.CanHaveChildren)
            {
                owner = item;
                return item.Items;
            }
            owner = node.Parent?.Tag;
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

    private void RemoveSelected()
    {
        var node = _tree.SelectedNode;
        if (node?.Tag is null)
            return;

        if (node.Tag is BarDefData bar)
        {
            Bars.Remove(bar);
        }
        else if (node.Tag is ItemDefData item)
        {
            GetChildCollectionOf(node.Parent)?.Remove(item);
        }
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
