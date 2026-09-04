using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;
using CommandBars.Designer.Protocol;

namespace CommandBars.Designer.Client;

internal enum BarDefinitionsInitialPage
{
    Commands,
    BarsAndMenus,
}

/// <summary>
/// Catalog-first editor. Reusable definitions live on the Commands page; bars
/// and compound dropdowns contain only lightweight catalog placements.
/// </summary>
internal sealed class BarDefinitionsDialog : Form
{
    private readonly TabControl _pages;
    private readonly TabPage _commandsPage;
    private readonly TabPage _barsPage;
    private readonly ListBox _commandList;
    private readonly TextBox _commandSearch;
    private readonly TreeView _compositionTree;
    private readonly ListBox _usageList;
    private readonly Label _compositionHint;
    private readonly Label _usageSummary;
    private readonly TreeView _barTree;
    private readonly PropertyGrid _grid;
    private readonly Label _validationLabel;
    private readonly Button _issuesButton;
    private readonly SplitContainer _outer;
    private readonly ToolStripButton _addCompositionCommands;
    private readonly ToolStripButton _addCompositionSeparator;
    private readonly ToolStripButton _removeCompositionItem;
    private readonly ToolStripButton _moveCompositionUp;
    private readonly ToolStripButton _moveCompositionDown;
    private bool _migrationChecked;

    public DesignSnapshot Snapshot { get; }

    private List<BarDefData> Bars => Snapshot.Bars;
    private List<CommandDefData> Commands => Snapshot.Commands;

    public BarDefinitionsDialog(
        DesignSnapshot snapshot,
        BarDefinitionsInitialPage initialPage = BarDefinitionsInitialPage.Commands)
    {
        Snapshot = snapshot ?? new DesignSnapshot();
        ImageKeyEditor.AmbientImages = Snapshot.Images;
        FormClosed += (_, _) => ImageKeyEditor.AmbientImages = null;

        AutoScaleDimensions = new SizeF(96F, 96F);
        AutoScaleMode = AutoScaleMode.Dpi;
        Font = SystemFonts.MessageBoxFont;
        Text = "Edit Command Catalog, Toolbars and Menus";
        StartPosition = FormStartPosition.CenterParent;
        MinimumSize = new Size(900, 580);
        Size = new Size(1080, 700);
        ShowInTaskbar = false;
        MinimizeBox = false;
        MaximizeBox = true;

        _commandsPage = new TabPage("Commands");
        _barsPage = new TabPage("Bars and Menus");
        _pages = new TabControl { Dock = DockStyle.Fill };
        _pages.TabPages.Add(_commandsPage);
        _pages.TabPages.Add(_barsPage);

        _commandList = new ListBox
        {
            Dock = DockStyle.Fill,
            IntegralHeight = false,
            HorizontalScrollbar = true,
            DrawMode = DrawMode.OwnerDrawFixed,
        };
        _commandSearch = new TextBox { Dock = DockStyle.Fill };
        _compositionTree = NewTree();
        _usageList = new ListBox
        {
            Dock = DockStyle.Fill,
            IntegralHeight = false,
            HorizontalScrollbar = true,
        };
        _compositionHint = new Label
        {
            Dock = DockStyle.Bottom,
            AutoSize = false,
            Height = 36,
            Padding = new Padding(4),
            ForeColor = SystemColors.GrayText,
        };
        _usageSummary = new Label
        {
            Dock = DockStyle.Top,
            AutoSize = false,
            Height = 25,
            Padding = new Padding(4),
        };
        _barTree = NewTree();
        _grid = new PropertyGrid
        {
            Dock = DockStyle.Fill,
            PropertySort = PropertySort.Categorized,
            ToolbarVisible = false,
        };
        _validationLabel = new Label
        {
            Dock = DockStyle.Fill,
            AutoEllipsis = true,
            TextAlign = ContentAlignment.MiddleLeft,
            Padding = new Padding(8, 0, 4, 0),
        };
        _issuesButton = new Button { Text = "View Issues...", AutoSize = true };

        var compositionStrip = NewStrip();
        _addCompositionCommands = MakeButton("Add Commands...", (_, _) => AddCommandsToComposition());
        _addCompositionSeparator = MakeButton("Add Separator", (_, _) => AddCompositionSeparator());
        _removeCompositionItem = MakeButton("Remove", (_, _) => RemoveCompositionPlacement());
        _moveCompositionUp = MakeButton("Move Up", (_, _) => MoveCompositionPlacement(-1));
        _moveCompositionDown = MakeButton("Move Down", (_, _) => MoveCompositionPlacement(+1));
        compositionStrip.Items.AddRange(new ToolStripItem[]
        {
            _addCompositionCommands,
            _addCompositionSeparator,
            new ToolStripSeparator(),
            _removeCompositionItem,
            _moveCompositionUp,
            _moveCompositionDown,
        });

        BuildCommandsPage(compositionStrip);
        BuildBarsPage();
        _pages.SelectedTab = initialPage == BarDefinitionsInitialPage.BarsAndMenus
            ? _barsPage
            : _commandsPage;

        _outer = new SplitContainer
        {
            Dock = DockStyle.Fill,
            FixedPanel = FixedPanel.Panel2,
        };
        _outer.Panel1.Controls.Add(_pages);
        _outer.Panel2.Controls.Add(_grid);
        Controls.Add(_outer);
        Controls.Add(BuildBottomPanel());

        _commandSearch.TextChanged += (_, _) => RebuildCommandList(SelectedCommand);
        _commandList.DrawItem += DrawCommandListItem;
        _commandList.SelectedIndexChanged += (_, _) => OnCommandSelected();
        _commandList.DoubleClick += (_, _) => _grid.SelectedObject = SelectedCommand;
        _compositionTree.AfterSelect += (_, _) =>
            _grid.SelectedObject = _compositionTree.SelectedNode?.Tag ?? SelectedCommand;
        _usageList.DoubleClick += (_, _) => NavigateToSelectedUsage();
        _barTree.AfterSelect += (_, _) =>
            _grid.SelectedObject = _barTree.SelectedNode?.Tag;
        _pages.SelectedIndexChanged += (_, _) => SyncPropertySelectionToPage();
        _grid.PropertyValueChanged += (_, e) => OnGridValueChanged(e);
        _issuesButton.Click += (_, _) => ShowValidationIssues();
        FormClosing += OnDialogFormClosing;
        Shown += (_, _) =>
        {
            ApplyDpiLayout();
            EnsureLegacyMigration();
        };
        DpiChanged += (_, _) => BeginInvoke((Action)ApplyDpiLayout);

        RebuildAll(selectFirst: true);
    }

    private void ApplyDpiLayout()
    {
        _commandList.ItemHeight = ScaleLogical(22);
        int propertyWidth = ScaleLogical(340);
        int propertyMinimum = ScaleLogical(280);
        _outer.Panel2MinSize = propertyMinimum;
        int split = Math.Max(_outer.Panel1MinSize, _outer.ClientSize.Width - propertyWidth);
        if (split > 0 && split < _outer.ClientSize.Width - _outer.Panel2MinSize)
            _outer.SplitterDistance = split;
    }

    private int ScaleLogical(int logicalPixels)
        => Math.Max(1, (int)Math.Round(logicalPixels * DeviceDpi / 96d));

    private static TreeView NewTree() => new()
    {
        Dock = DockStyle.Fill,
        HideSelection = false,
        FullRowSelect = true,
        ShowRootLines = true,
        PathSeparator = "/",
    };

    private void BuildCommandsPage(ToolStrip compositionStrip)
    {
        var split = new SplitContainer
        {
            Dock = DockStyle.Fill,
            Orientation = Orientation.Horizontal,
            SplitterDistance = 260,
        };

        var searchPanel = new TableLayoutPanel
        {
            Dock = DockStyle.Top,
            Height = 32,
            ColumnCount = 2,
            Padding = new Padding(4),
        };
        searchPanel.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        searchPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        searchPanel.Controls.Add(new Label
        {
            Text = "Search:",
            Anchor = AnchorStyles.Left,
            AutoSize = true,
        }, 0, 0);
        searchPanel.Controls.Add(_commandSearch, 1, 0);

        split.Panel1.Controls.Add(_commandList);
        split.Panel1.Controls.Add(searchPanel);
        split.Panel1.Controls.Add(BuildCommandsStrip());

        var details = new SplitContainer
        {
            Dock = DockStyle.Fill,
            SplitterDistance = 390,
        };
        var contents = new GroupBox
        {
            Text = "Dropdown Contents",
            Dock = DockStyle.Fill,
            Padding = new Padding(5),
        };
        contents.Controls.Add(_compositionTree);
        contents.Controls.Add(_compositionHint);
        contents.Controls.Add(compositionStrip);
        details.Panel1.Controls.Add(contents);

        var usages = new GroupBox
        {
            Text = "Usages",
            Dock = DockStyle.Fill,
            Padding = new Padding(5),
        };
        usages.Controls.Add(_usageList);
        usages.Controls.Add(_usageSummary);
        details.Panel2.Controls.Add(usages);
        split.Panel2.Controls.Add(details);
        _commandsPage.Controls.Add(split);
    }

    private void BuildBarsPage()
    {
        _barsPage.Controls.Add(_barTree);
        _barsPage.Controls.Add(BuildBarsStrip());
    }

    private ToolStrip BuildCommandsStrip()
    {
        var strip = NewStrip();
        var add = new ToolStripDropDownButton("Add Command")
        {
            DisplayStyle = ToolStripItemDisplayStyle.Text,
        };
        AddKindChoice(add, "Action", CommandKindData.Action);
        AddKindChoice(add, "Toggle", CommandKindData.Toggle);
        AddKindChoice(add, "Popup", CommandKindData.Popup);
        AddKindChoice(add, "Split Button", CommandKindData.SplitButton);
        AddKindChoice(add, "Combo Box", CommandKindData.ComboBox);
        AddKindChoice(add, "Label", CommandKindData.Label);
        strip.Items.Add(add);
        strip.Items.Add(MakeButton("Duplicate", (_, _) => DuplicateCommand()));
        strip.Items.Add(MakeButton("Remove", (_, _) => RemoveCommand()));
        return strip;
    }

    private void AddKindChoice(ToolStripDropDownButton menu, string text, CommandKindData kind)
        => menu.DropDownItems.Add(text, null, (_, _) => AddCommand(kind));

    private ToolStrip BuildBarsStrip()
    {
        var strip = NewStrip();
        strip.Items.Add(MakeButton("Add Toolbar", (_, _) => AddBar(BarKind.Toolbar)));
        strip.Items.Add(MakeButton("Add Menu Bar", (_, _) => AddBar(BarKind.MenuBar)));
        strip.Items.Add(new ToolStripSeparator());
        strip.Items.Add(MakeButton("Add Commands...", (_, _) => AddCommandsToBar()));
        strip.Items.Add(MakeButton("Add Separator", (_, _) => AddBarSeparator()));
        strip.Items.Add(new ToolStripSeparator());
        strip.Items.Add(MakeButton("Remove", (_, _) => RemoveBarSelection()));
        strip.Items.Add(MakeButton("Move Up", (_, _) => MoveBarSelection(-1)));
        strip.Items.Add(MakeButton("Move Down", (_, _) => MoveBarSelection(+1)));
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

    private Control BuildBottomPanel()
    {
        var panel = new Panel { Dock = DockStyle.Bottom, Height = 48 };
        var buttons = new FlowLayoutPanel
        {
            Dock = DockStyle.Right,
            FlowDirection = FlowDirection.RightToLeft,
            AutoSize = true,
            Padding = new Padding(4, 8, 8, 4),
        };
        var ok = new Button { Text = "OK", DialogResult = DialogResult.OK, AutoSize = true };
        var cancel = new Button { Text = "Cancel", DialogResult = DialogResult.Cancel, AutoSize = true };
        buttons.Controls.Add(ok);
        buttons.Controls.Add(cancel);
        buttons.Controls.Add(_issuesButton);
        panel.Controls.Add(_validationLabel);
        panel.Controls.Add(buttons);
        AcceptButton = ok;
        CancelButton = cancel;
        return panel;
    }

    private CommandDefData? SelectedCommand => _commandList.SelectedItem as CommandDefData;

    private void DrawCommandListItem(object? sender, DrawItemEventArgs e)
    {
        e.DrawBackground();
        if (e.Index < 0 || e.Index >= _commandList.Items.Count)
            return;
        var command = (CommandDefData)_commandList.Items[e.Index];
        Color foreground = (e.State & DrawItemState.Selected) != 0
            ? SystemColors.HighlightText
            : _commandList.ForeColor;
        var bounds = e.Bounds;
        var kindBounds = new Rectangle(
            Math.Max(bounds.Left, bounds.Right - ScaleLogical(120)),
            bounds.Top,
            ScaleLogical(112),
            bounds.Height);
        var textBounds = new Rectangle(
            bounds.Left + ScaleLogical(4),
            bounds.Top,
            Math.Max(1, kindBounds.Left - bounds.Left - ScaleLogical(8)),
            bounds.Height);
        TextRenderer.DrawText(e.Graphics, DisplayName(command), _commandList.Font,
            textBounds, foreground,
            TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis);
        TextRenderer.DrawText(e.Graphics, command.Kind.ToString(), _commandList.Font,
            kindBounds, foreground,
            TextFormatFlags.Right | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis);
        e.DrawFocusRectangle();
    }

    private void AddCommand(CommandKindData kind)
    {
        string stem = kind switch
        {
            CommandKindData.Action => "action",
            CommandKindData.Toggle => "toggle",
            CommandKindData.Popup => "popup",
            CommandKindData.SplitButton => "splitButton",
            CommandKindData.ComboBox => "comboBox",
            CommandKindData.Label => "label",
            _ => "command",
        };
        var command = new CommandDefData
        {
            Id = UniqueCommandId(stem),
            Kind = kind,
            Text = SplitWords(stem),
        };
        Commands.Add(command);
        _commandSearch.Clear();
        RebuildCommandList(command);
        UpdateValidationState();
    }

    private void DuplicateCommand()
    {
        var source = SelectedCommand;
        if (source == null)
            return;
        var holder = new DesignSnapshot();
        holder.Commands.Add(source);
        var clone = DefinitionsSerializer.Deserialize(DefinitionsSerializer.Serialize(holder)).Commands[0];
        clone.Id = UniqueCommandId(source.Id + ".copy");
        clone.Text = string.IsNullOrWhiteSpace(source.Text)
            ? "Copy of " + source.Id
            : "Copy of " + source.Text.Replace("&", string.Empty);
        Commands.Add(clone);
        _commandSearch.Clear();
        RebuildCommandList(clone);
        UpdateValidationState();
    }

    private void RemoveCommand()
    {
        var command = SelectedCommand;
        if (command == null)
            return;
        if (string.IsNullOrWhiteSpace(command.Id))
        {
            Commands.Remove(command);
            RebuildAll(selectFirst: true);
            return;
        }

        var usages = CatalogDesignService.FindUsages(Snapshot, command.Id);
        bool cascade = false;
        if (usages.Count > 0)
        {
            string preview = string.Join(Environment.NewLine,
                usages.Take(8).Select(usage => "• " + usage.Location));
            if (usages.Count > 8)
                preview += Environment.NewLine + "• …and " + (usages.Count - 8) + " more";
            var result = MessageBox.Show(this,
                "This catalog entry is used in " + usages.Count + " location(s):" +
                Environment.NewLine + Environment.NewLine + preview +
                Environment.NewLine + Environment.NewLine +
                "Remove it and all placements that reference it?",
                "Remove Command", MessageBoxButtons.YesNo, MessageBoxIcon.Warning,
                MessageBoxDefaultButton.Button2);
            if (result != DialogResult.Yes)
                return;
            cascade = true;
        }
        CatalogDesignService.RemoveCommand(Snapshot, command.Id, cascade);
        RebuildAll(selectFirst: true);
    }

    private void OnCommandSelected()
    {
        var command = SelectedCommand;
        _grid.SelectedObject = command;
        RebuildComposition(command);
        RebuildUsages(command);
    }

    private void RebuildCommandList(CommandDefData? select = null)
    {
        select ??= SelectedCommand;
        string query = _commandSearch.Text.Trim();
        var filtered = Commands.Where(command => MatchesSearch(command, query)).ToList();
        _commandList.BeginUpdate();
        _commandList.Items.Clear();
        foreach (var command in filtered)
            _commandList.Items.Add(command);
        _commandList.EndUpdate();
        if (select != null && filtered.Contains(select))
            _commandList.SelectedItem = select;
        else if (_commandList.Items.Count > 0)
            _commandList.SelectedIndex = 0;
        else
            OnCommandSelected();
        _commandList.Refresh();
    }

    private static bool MatchesSearch(CommandDefData command, string query)
    {
        if (query.Length == 0)
            return true;
        return command.Id.IndexOf(query, StringComparison.OrdinalIgnoreCase) >= 0 ||
               command.Text.IndexOf(query, StringComparison.OrdinalIgnoreCase) >= 0 ||
               command.Kind.ToString().IndexOf(query, StringComparison.OrdinalIgnoreCase) >= 0;
    }

    private void RebuildComposition(CommandDefData? command, CommandPlacementData? select = null)
    {
        bool compound = command != null &&
            (command.Kind == CommandKindData.Popup || command.Kind == CommandKindData.SplitButton);
        bool authored = compound &&
            (command!.Kind == CommandKindData.SplitButton ||
             command.ContentSource == CommandContentSourceData.Authored);
        _compositionTree.BeginUpdate();
        _compositionTree.Nodes.Clear();
        if (authored)
        {
            foreach (var placement in command!.Items)
                _compositionTree.Nodes.Add(new TreeNode(PlacementLabel(placement)) { Tag = placement });
        }
        _compositionTree.EndUpdate();
        if (!compound)
            _compositionHint.Text = "Select a Popup or Split Button to edit its reusable dropdown.";
        else if (!authored)
            _compositionHint.Text = "This popup uses dynamic content; authored children are not active.";
        else
            _compositionHint.Text = command!.Items.Count == 0
                ? "This dropdown is empty. Add existing commands or a separator."
                : "These placements are shared by every use of this catalog entry.";
        _addCompositionCommands.Enabled = authored;
        _addCompositionSeparator.Enabled = authored;
        _removeCompositionItem.Enabled = authored;
        _moveCompositionUp.Enabled = authored;
        _moveCompositionDown.Enabled = authored;
        if (select != null)
            SelectNodeByTag(_compositionTree, select);
    }

    private void RebuildUsages(CommandDefData? command)
    {
        _usageList.BeginUpdate();
        _usageList.Items.Clear();
        if (command != null && !string.IsNullOrWhiteSpace(command.Id))
        {
            foreach (var usage in CatalogDesignService.FindUsages(Snapshot, command.Id))
                _usageList.Items.Add(usage);
        }
        _usageList.EndUpdate();
        int count = _usageList.Items.Count;
        _usageSummary.Text = count == 1 ? "Used in 1 location" : "Used in " + count + " locations";
    }

    private void AddCommandsToComposition()
    {
        var command = SelectedCommand;
        if (!CanEditComposition(command))
            return;
        using var picker = new CommandPickerDialog(Snapshot,
            CommandPlacementTargetData.DropDown, "Add Commands to " + DisplayName(command!));
        if (picker.ShowDialog(this) != DialogResult.OK)
            return;
        CommandPlacementData? selected = null;
        foreach (var chosen in picker.SelectedCommands)
        {
            selected = new CommandPlacementData { CommandId = chosen.Id };
            command!.Items.Add(selected);
        }
        RebuildComposition(command, selected);
        RebuildUsages(command);
        RebuildBarTree();
        UpdateValidationState();
    }

    private void AddCompositionSeparator()
    {
        var command = SelectedCommand;
        if (!CanEditComposition(command))
            return;
        var placement = new CommandPlacementData { Kind = CommandPlacementKindData.Separator };
        command!.Items.Add(placement);
        RebuildComposition(command, placement);
        UpdateValidationState();
    }

    private static bool CanEditComposition(CommandDefData? command)
        => command != null &&
           (command.Kind == CommandKindData.SplitButton ||
            (command.Kind == CommandKindData.Popup &&
             command.ContentSource == CommandContentSourceData.Authored));

    private void RemoveCompositionPlacement()
    {
        var command = SelectedCommand;
        if (command == null || _compositionTree.SelectedNode?.Tag is not CommandPlacementData placement)
            return;
        command.Items.Remove(placement);
        RebuildComposition(command);
        RebuildUsages(command);
        UpdateValidationState();
    }

    private void MoveCompositionPlacement(int delta)
    {
        var command = SelectedCommand;
        if (command == null || _compositionTree.SelectedNode?.Tag is not CommandPlacementData placement)
            return;
        Reorder(command.Items, placement, delta);
        RebuildComposition(command, placement);
        UpdateValidationState();
    }

    private void NavigateToSelectedUsage()
    {
        if (_usageList.SelectedItem is not CommandUsageData usage)
            return;
        int index = ParsePlacementIndex(usage.Location);
        if (usage.Kind == CommandUsageKind.BarPlacement)
        {
            var bar = Bars.FirstOrDefault(candidate =>
                string.Equals(candidate.Name, usage.OwnerId, StringComparison.Ordinal));
            if (bar == null)
                return;
            _pages.SelectedTab = _barsPage;
            if (index >= 0 && index < bar.Placements.Count)
                SelectNodeByTag(_barTree, bar.Placements[index]);
            else
                SelectNodeByTag(_barTree, bar);
        }
        else if (usage.Kind == CommandUsageKind.CompoundPlacement ||
                 usage.Kind == CommandUsageKind.SplitPrimary)
        {
            var owner = Commands.FirstOrDefault(candidate =>
                string.Equals(candidate.Id, usage.OwnerId, StringComparison.Ordinal));
            if (owner == null)
                return;
            _pages.SelectedTab = _commandsPage;
            _commandSearch.Clear();
            RebuildCommandList(owner);
            if (usage.Kind == CommandUsageKind.CompoundPlacement &&
                index >= 0 && index < owner.Items.Count)
                SelectNodeByTag(_compositionTree, owner.Items[index]);
        }
    }

    private static int ParsePlacementIndex(string location)
    {
        int close = location.LastIndexOf(']');
        int open = close < 0 ? -1 : location.LastIndexOf('[', close);
        if (open < 0 || close <= open)
            return -1;
        return int.TryParse(location.Substring(open + 1, close - open - 1), out int index)
            ? index : -1;
    }

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
        RebuildBarTree(bar);
        UpdateValidationState();
    }

    private void AddCommandsToBar()
    {
        var bar = SelectedBar();
        if (bar == null)
        {
            MessageBox.Show(this, "Select a toolbar or menu bar first.", "Add Commands",
                MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }
        var target = bar.BarType == BarKind.MenuBar
            ? CommandPlacementTargetData.MenuBar
            : CommandPlacementTargetData.Toolbar;
        using var picker = new CommandPickerDialog(Snapshot, target, "Add Commands to " + bar.Name);
        if (picker.ShowDialog(this) != DialogResult.OK)
            return;
        CommandPlacementData? selected = null;
        foreach (var chosen in picker.SelectedCommands)
        {
            selected = new CommandPlacementData { CommandId = chosen.Id };
            bar.Placements.Add(selected);
        }
        RebuildBarTree(selected ?? (object)bar);
        RebuildUsages(SelectedCommand);
        UpdateValidationState();
    }

    private void AddBarSeparator()
    {
        var bar = SelectedBar();
        if (bar == null)
            return;
        if (bar.BarType == BarKind.MenuBar)
        {
            MessageBox.Show(this,
                "Menu-bar roots contain Popup commands only; separators belong inside a popup.",
                "Add Separator", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }
        var placement = new CommandPlacementData { Kind = CommandPlacementKindData.Separator };
        bar.Placements.Add(placement);
        RebuildBarTree(placement);
        UpdateValidationState();
    }

    private BarDefData? SelectedBar()
    {
        var node = _barTree.SelectedNode;
        if (node?.Tag is BarDefData bar)
            return bar;
        return node?.Parent?.Tag as BarDefData;
    }

    private void RemoveBarSelection()
    {
        var node = _barTree.SelectedNode;
        if (node?.Tag is BarDefData bar)
            Bars.Remove(bar);
        else if (node?.Tag is CommandPlacementData placement && node.Parent?.Tag is BarDefData owner)
            owner.Placements.Remove(placement);
        else
            return;
        RebuildBarTree(selectFirst: true);
        RebuildUsages(SelectedCommand);
        UpdateValidationState();
    }

    private void MoveBarSelection(int delta)
    {
        var node = _barTree.SelectedNode;
        if (node?.Tag is BarDefData bar)
            Reorder(Bars, bar, delta);
        else if (node?.Tag is CommandPlacementData placement && node.Parent?.Tag is BarDefData owner)
            Reorder(owner.Placements, placement, delta);
        else
            return;
        RebuildBarTree(node.Tag);
        UpdateValidationState();
    }

    private void RebuildBarTree(object? select = null, bool selectFirst = false)
    {
        _barTree.BeginUpdate();
        _barTree.Nodes.Clear();
        foreach (var bar in Bars)
        {
            var barNode = new TreeNode(bar.ToString()) { Tag = bar };
            foreach (var placement in bar.Placements)
                barNode.Nodes.Add(new TreeNode(PlacementLabel(placement)) { Tag = placement });
            _barTree.Nodes.Add(barNode);
        }
        _barTree.ExpandAll();
        _barTree.EndUpdate();
        if (select != null)
            SelectNodeByTag(_barTree, select);
        else if (selectFirst && _barTree.Nodes.Count > 0)
            _barTree.SelectedNode = _barTree.Nodes[0];
    }

    private string PlacementLabel(CommandPlacementData placement)
    {
        if (placement.Kind == CommandPlacementKindData.Separator)
            return "Separator";
        var command = Commands.FirstOrDefault(candidate =>
            string.Equals(candidate.Id, placement.CommandId, StringComparison.Ordinal));
        return command == null
            ? "Missing command: " + placement.CommandId
            : command.Kind + ": " + DisplayName(command) + " → " + command.Id;
    }

    private void OnGridValueChanged(PropertyValueChangedEventArgs e)
    {
        object? selected = _grid.SelectedObject;
        if (selected is CommandDefData command &&
            e.ChangedItem.PropertyDescriptor?.Name == nameof(CommandDefData.Id))
        {
            string oldId = e.OldValue as string ?? string.Empty;
            string newId = command.Id;
            command.Id = oldId;
            try
            {
                if (string.IsNullOrWhiteSpace(oldId))
                {
                    if (string.IsNullOrWhiteSpace(newId))
                        throw new ArgumentException("Command id must not be empty.");
                    if (Commands.Any(candidate => !ReferenceEquals(candidate, command) &&
                        string.Equals(candidate.Id, newId, StringComparison.Ordinal)))
                        throw new InvalidOperationException(
                            "A catalog entry with id '" + newId + "' already exists.");
                    command.Id = newId;
                }
                else
                {
                    CatalogDesignService.RenameCommand(Snapshot, oldId, newId);
                }
            }
            catch (Exception ex)
            {
                command.Id = oldId;
                MessageBox.Show(this, ex.Message, "Rename Command",
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
        }
        _grid.Refresh();
        RebuildCommandList(selected as CommandDefData ?? SelectedCommand);
        RebuildComposition(SelectedCommand, selected as CommandPlacementData);
        RebuildUsages(SelectedCommand);
        RebuildBarTree(selected);
        UpdateValidationState();
    }

    private void RebuildAll(bool selectFirst = false)
    {
        RebuildCommandList();
        RebuildBarTree(selectFirst: selectFirst);
        UpdateValidationState();
        SyncPropertySelectionToPage();
    }

    private void SyncPropertySelectionToPage()
    {
        _grid.SelectedObject = _pages.SelectedTab == _commandsPage
            ? _compositionTree.SelectedNode?.Tag ?? (object?)SelectedCommand
            : _barTree.SelectedNode?.Tag;
    }

    private void UpdateValidationState()
    {
        var validation = CatalogDesignService.ValidateCatalogFirst(Snapshot);
        int errors = validation.Diagnostics.Count(d => d.Severity == CatalogDiagnosticSeverity.Error);
        int warnings = validation.Diagnostics.Count(d => d.Severity == CatalogDiagnosticSeverity.Warning);
        _validationLabel.Text = errors == 0 && warnings == 0
            ? "Catalog and placements are valid."
            : errors + " error(s), " + warnings + " warning(s)";
        _validationLabel.ForeColor = errors > 0
            ? Color.Firebrick
            : warnings > 0 ? Color.DarkGoldenrod : SystemColors.ControlText;
        _issuesButton.Enabled = validation.Diagnostics.Count > 0;
    }

    private void ShowValidationIssues()
    {
        var validation = CatalogDesignService.ValidateCatalogFirst(Snapshot);
        if (validation.Diagnostics.Count == 0)
            return;
        using var dialog = new CatalogIssuesDialog(validation.Diagnostics);
        dialog.ShowDialog(this);
    }

    private void OnDialogFormClosing(object? sender, FormClosingEventArgs e)
    {
        if (DialogResult != DialogResult.OK)
            return;
        var validation = CatalogDesignService.ValidateCatalogFirst(Snapshot);
        if (validation.IsValid)
            return;
        string errors = string.Join(Environment.NewLine,
            validation.Diagnostics
                .Where(d => d.Severity == CatalogDiagnosticSeverity.Error)
                .Take(12).Select(d => "• " + d));
        int remaining = validation.Diagnostics.Count(d =>
            d.Severity == CatalogDiagnosticSeverity.Error) - 12;
        if (remaining > 0)
            errors += Environment.NewLine + "• …and " + remaining + " more";
        MessageBox.Show(this,
            "Fix these catalog errors before saving:" + Environment.NewLine + Environment.NewLine + errors,
            "Invalid Command Catalog", MessageBoxButtons.OK, MessageBoxIcon.Warning);
        e.Cancel = true;
        DialogResult = DialogResult.None;
    }

    private void EnsureLegacyMigration()
    {
        if (_migrationChecked)
            return;
        _migrationChecked = true;
        if (!Bars.Any(bar => bar.Items.Count > 0))
            return;
        var plan = CatalogDesignService.CreateLegacyMigrationPlan(Snapshot);
        using var preview = new LegacyMigrationPreviewDialog(plan);
        if (preview.ShowDialog(this) != DialogResult.OK)
        {
            DialogResult = DialogResult.Cancel;
            Close();
            return;
        }
        Snapshot.SchemaVersion = plan.MigratedSnapshot.SchemaVersion;
        Snapshot.Bars = plan.MigratedSnapshot.Bars;
        Snapshot.Commands = plan.MigratedSnapshot.Commands;
        RebuildAll(selectFirst: true);
    }

    private static void SelectNodeByTag(TreeView tree, object tag)
    {
        foreach (TreeNode root in tree.Nodes)
        {
            TreeNode? found = FindNode(root, tag);
            if (found == null)
                continue;
            tree.SelectedNode = found;
            found.EnsureVisible();
            return;
        }
    }

    private static TreeNode? FindNode(TreeNode node, object tag)
    {
        if (ReferenceEquals(node.Tag, tag))
            return node;
        foreach (TreeNode child in node.Nodes)
        {
            TreeNode? found = FindNode(child, tag);
            if (found != null)
                return found;
        }
        return null;
    }

    private static void Reorder<T>(List<T> list, T value, int delta)
    {
        int index = list.IndexOf(value);
        int target = index + delta;
        if (index < 0 || target < 0 || target >= list.Count)
            return;
        list.RemoveAt(index);
        list.Insert(target, value);
    }

    private string UniqueCommandId(string baseId)
    {
        if (string.IsNullOrWhiteSpace(baseId))
            baseId = "command";
        bool Exists(string id) => Commands.Any(command =>
            string.Equals(command.Id, id, StringComparison.Ordinal));
        if (!Exists(baseId))
            return baseId;
        for (int number = 2; ; number++)
        {
            string candidate = baseId + number;
            if (!Exists(candidate))
                return candidate;
        }
    }

    private string UniqueBarName(string baseName)
    {
        bool Exists(string name) => Bars.Any(bar =>
            string.Equals(bar.Name, name, StringComparison.Ordinal));
        if (!Exists(baseName))
            return baseName;
        for (int number = 2; ; number++)
        {
            string candidate = baseName + number;
            if (!Exists(candidate))
                return candidate;
        }
    }

    private static string DisplayName(CommandDefData command)
        => string.IsNullOrWhiteSpace(command.Text)
            ? command.Id
            : command.Text.Replace("&", string.Empty);

    private static string SplitWords(string value)
    {
        var chars = new List<char>(value.Length + 4);
        for (int index = 0; index < value.Length; index++)
        {
            char current = value[index];
            if (index > 0 && char.IsUpper(current))
                chars.Add(' ');
            chars.Add(index == 0 ? char.ToUpperInvariant(current) : current);
        }
        return new string(chars.ToArray());
    }
}
