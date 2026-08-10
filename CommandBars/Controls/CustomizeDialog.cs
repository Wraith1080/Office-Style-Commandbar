using CommandBars.Model;
using CommandBars.Rendering;
using System.Drawing;
using System.Windows.Forms;
using System.Xml.Linq;

namespace CommandBars.Controls;

/// <summary>
/// The Office-style Customize dialog. Opening it enters the manager's Customize
/// mode; closing it exits. Tabs: <b>Toolbars</b> (show/hide, new, rename,
/// delete), <b>Commands</b> (the drag-source palette), and <b>Options</b>
/// (icon size). The dialog is non-modal, so bars stay editable behind it —
/// drag buttons to reorder, move, remove, or drag from the palette to add.
/// </summary>
public sealed class CustomizeDialog : Form
{
    // The programmatic layout below was tuned at Windows' 125% setting. Treat
    // 120 DPI as its design-time baseline so WinForms scales the complete form
    // (including ClientSize and MinimumSize) up or down from those dimensions.
    private const float LayoutDpi = 120f;

    private static readonly int[] IconSteps = { 12, 16, 20, 24, 32, 48, 64 };

    private readonly CommandBarManager _manager;
    private CommandBarRenderer _renderer;
    private readonly List<Command> _commands;
    private readonly List<CommandBarCustomizationItem> _paletteItems = new();
    private readonly CommandsPalette _palette;
    private readonly Panel _paletteHost = new();
    private readonly ThemedTabControl _tabs = new() { Dock = DockStyle.Fill };
    private readonly ThemedCheckedListBox _toolbarList = new();
    private readonly List<CommandBar> _listedBars = new();
    private readonly TreeView _menuTree = new();
    private readonly ThemedComboBox _iconCombo = new();
    private readonly List<Button> _menuButtons = new();
    private readonly List<Button> _toolbarButtons = new();
    private readonly ThemedFlowLayoutPanel _menuButtonHost = CreateSideButtonHost();
    private readonly ThemedFlowLayoutPanel _toolbarButtonHost = CreateSideButtonHost();
    private bool _suppress;

    public CustomizeDialog(CommandBarManager manager, CommandBarRenderer renderer, IEnumerable<Command>? paletteCommands = null)
    {
        _manager = manager ?? throw new ArgumentNullException(nameof(manager));
        _renderer = renderer ?? throw new ArgumentNullException(nameof(renderer));
        _commands = new List<Command>(paletteCommands ?? AllCommands());
        SuspendLayout();

        _paletteItems.AddRange(BuildPaletteItems(_manager, _commands));

        Text = "Customize";
        FormBorderStyle = FormBorderStyle.SizableToolWindow;
        // CenterParent only applies to modal dialogs; this one is shown non-modally,
        // so it's centered manually in OnLoad instead.
        StartPosition = FormStartPosition.Manual;
        AutoScaleMode = AutoScaleMode.Dpi;
        AutoScaleDimensions = new SizeF(LayoutDpi, LayoutDpi);
        ClientSize = new Size(420, 470);
        MinimumSize = new Size(360, 400);
        ShowInTaskbar = false;
        Padding = new Padding(8); // keep the tab control off the dialog edge

        _palette = new CommandsPalette { Dock = DockStyle.Top, Manager = _manager, Renderer = renderer };
        _palette.SetItems(_paletteItems);

        _tabs.AddPage(BuildToolbarsTab());
        _tabs.AddPage(BuildMenusTab());
        _tabs.AddPage(BuildCommandsTab());
        _tabs.AddPage(BuildOptionsTab());

        // A right-aligned Close button in its own footer strip (Wizard-98 style),
        // AutoSize so its text is never clipped at high DPI.
        var footer = new ThemedFooterPanel
        {
            Dock = DockStyle.Fill,
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            MinimumSize = new Size(0, 48),
            RightInset = 0,
        };
        var close = new ThemedButton
        {
            Text = "Close",
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            MinimumSize = new Size(84, 0),
        };
        close.Click += (_, _) => Close();
        var resetAll = new ThemedButton
        {
            Text = "Reset All...",
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            MinimumSize = new Size(84, 0),
        };
        resetAll.Click += (_, _) => ResetAll();
        footer.Controls.Add(close);    // rightmost
        footer.Controls.Add(resetAll); // left of Close
        CancelButton = close; // Esc closes the dialog

        var layout = new ThemedTableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 2,
            UseWindowSurface = true,
        };
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f));
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100f));
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        layout.Controls.Add(_tabs, 0, 0);
        layout.Controls.Add(footer, 0, 1);
        Controls.Add(layout);

        SetRenderer(renderer);

        RefreshToolbarList();
        RebuildMenuTree();
        SyncIconCombo();

        _manager.LayoutChanged += OnManagerLayoutChanged;
        _manager.ThemeChanged += OnManagerThemeChanged;
        _manager.BeginCustomize();
        ResumeLayout(true);
    }

    protected override void OnLoad(EventArgs e)
    {
        base.OnLoad(e);

        // Align every Menus-tab button to the width of the longest one (measured
        // now, so it's correct at the current DPI).
        FitButtonHost(_menuButtonHost, _menuButtons);
        FitButtonHost(_toolbarButtonHost, _toolbarButtons);
        EnsureMinimumLayoutWidth();
        // Center on the owner form (non-modal, so CenterParent doesn't apply).
        if (Owner is { } owner)
            Location = new Point(
                owner.Left + Math.Max(0, (owner.Width - Width) / 2),
                owner.Top + Math.Max(0, (owner.Height - Height) / 2));
    }

    protected override void OnHandleCreated(EventArgs e)
    {
        base.OnHandleCreated(e);
        DialogSkin.ApplyNativeFrame(this, _renderer.DialogColors);
    }

    private void EnsureMinimumLayoutWidth()
    {
        int minimumClientWidth = _tabs.MinimumTabStripWidth + Padding.Horizontal + 6;
        Size minimumWindow = SizeFromClientSize(new Size(minimumClientWidth, ClientSize.Height));
        MinimumSize = new Size(Math.Max(MinimumSize.Width, minimumWindow.Width), MinimumSize.Height);
        if (ClientSize.Width < minimumClientWidth)
            ClientSize = new Size(minimumClientWidth, ClientSize.Height);
    }

    /// <summary>Re-themes the complete dialog and embedded Commands palette.</summary>
    public void SetRenderer(CommandBarRenderer renderer)
    {
        _renderer = renderer ?? throw new ArgumentNullException(nameof(renderer));
        _palette.Renderer = renderer;
        DialogSkin.Apply(this, renderer.DialogColors);
        _paletteHost.BackColor = renderer.DialogColors.InputBackground;
        DialogSkin.ApplyNativeFrame(this, renderer.DialogColors);
    }

    private void ResetAll()
    {
        if (!Confirm("Reset all toolbars and menus to their defaults?", "Customize"))
            return;
        _manager.ResetToDefaults();
        RefreshToolbarList();
        RebuildMenuTree();
        SyncIconCombo();
    }

    // --- Tab construction --------------------------------------------------

    private DialogTabPage BuildToolbarsTab()
    {
        var tab = new DialogTabPage("Toolbars");

        _toolbarList.Dock = DockStyle.Fill;
        _toolbarList.CheckOnClick = true;
        _toolbarList.IntegralHeight = false;
        _toolbarList.ItemCheck += OnToolbarItemCheck;

        // Buttons stacked vertically down the right of the list (the classic
        // Windows "list with side buttons" layout).
        var buttons = _toolbarButtonHost;
        var newBtn = MakeSideButton("New...", 112);
        var renameBtn = MakeSideButton("Rename...", 112);
        var deleteBtn = MakeSideButton("Delete", 112);
        var resetBtn = MakeSideButton("Reset", 112);
        newBtn.Click += (_, _) => NewToolbar();
        renameBtn.Click += (_, _) => RenameToolbar();
        deleteBtn.Click += (_, _) => DeleteToolbar();
        resetBtn.Click += (_, _) => ResetToolbar();
        buttons.Controls.Add(newBtn);
        buttons.Controls.Add(renameBtn);
        buttons.Controls.Add(deleteBtn);
        buttons.Controls.Add(resetBtn);

        _toolbarButtons.AddRange(new[] { newBtn, renameBtn, deleteBtn, resetBtn });
        var layout = CreateTabGrid();
        layout.Controls.Add(_toolbarList, 0, 0);
        layout.Controls.Add(buttons, 1, 0);
        tab.Controls.Add(layout);
        return tab;
    }

    private static Button MakeSideButton(string text, int minWidth = 84) => new ThemedButton
    {
        Text = text,
        AutoSize = false,
        MinimumSize = new Size(minWidth, 0),
        Margin = new Padding(0, 0, 0, 6),
    };

    private static ThemedFlowLayoutPanel CreateSideButtonHost() => new()
    {
        Dock = DockStyle.Fill,
        FlowDirection = FlowDirection.TopDown,
        WrapContents = false,
        AutoScroll = true,
        Margin = new Padding(8, 0, 0, 0),
        StretchChildrenHorizontally = true,
        UseTabBodySurface = true,
    };

    private static ThemedTableLayoutPanel CreateTabGrid()
    {
        var layout = new ThemedTableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 2,
            RowCount = 1,
            UseTabBodySurface = true,
        };
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50f));
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50f));
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100f));
        return layout;
    }

    // --- Menus tab ---------------------------------------------------------

    private DialogTabPage BuildMenusTab()
    {
        var tab = new DialogTabPage("Menus");

        _menuTree.Dock = DockStyle.Fill;
        _menuTree.HideSelection = false;
        _menuTree.ShowRootLines = true;
        _menuTree.ShowPlusMinus = true;

        var buttons = _menuButtonHost;
        var newMenu = MakeSideButton("New Menu", 112);
        var addCommand = MakeSideButton("Add Command...", 112);
        var addSep = MakeSideButton("Add Separator", 112);
        var rename = MakeSideButton("Rename...", 112);
        var remove = MakeSideButton("Remove", 112);
        var moveUp = MakeSideButton("Move Up", 112);
        var moveDown = MakeSideButton("Move Down", 112);
        var reset = MakeSideButton("Reset", 112);

        newMenu.Click += (_, _) => AddNewItem(new CommandBarPopupItem("New Menu"));
        addCommand.Click += (_, _) => AddCommandItem();
        addSep.Click += (_, _) => AddNewItem(new CommandBarSeparator());
        rename.Click += (_, _) => RenameMenuNode();
        remove.Click += (_, _) => RemoveMenuNode();
        moveUp.Click += (_, _) => MoveMenuNode(-1);
        moveDown.Click += (_, _) => MoveMenuNode(1);
        reset.Click += (_, _) => ResetMenuNode();

        buttons.Controls.Add(newMenu);
        buttons.Controls.Add(addCommand);
        buttons.Controls.Add(addSep);
        buttons.Controls.Add(rename);
        buttons.Controls.Add(remove);
        buttons.Controls.Add(moveUp);
        buttons.Controls.Add(moveDown);
        buttons.Controls.Add(reset);

        // Remember these so OnLoad can widen them all to match the longest button
        // ("Add Command...") for a clean, aligned column at any DPI.
        _menuButtons.AddRange(new[] { newMenu, addCommand, addSep, rename, remove, moveUp, moveDown, reset });

        var layout = CreateTabGrid();
        layout.Controls.Add(_menuTree, 0, 0);
        layout.Controls.Add(buttons, 1, 0);
        tab.Controls.Add(layout);
        return tab;
    }

    private CommandBar? FirstMenuBar()
    {
        foreach (var bar in _manager.Bars)
            if (bar.BarType == CommandBarType.MenuBar)
                return bar;
        return null;
    }

    // The collection a node's children live in: the menu bar itself for the
    // root, or a popup's DropDown for a submenu node. Null for leaf items.
    private static CommandBar? ContainerOf(TreeNode? node) => node?.Tag switch
    {
        CommandBar bar => bar,
        CommandBarPopupItem popup => popup.DropDown,
        _ => null,
    };

    private void RebuildMenuTree()
    {
        _menuTree.BeginUpdate();
        _menuTree.Nodes.Clear();
        foreach (var bar in _manager.Bars)
            if (bar.BarType == CommandBarType.MenuBar)
            {
                var root = new TreeNode(Command.RemoveMnemonic(bar.Text)) { Tag = bar };
                AddChildNodes(root, bar.Items);
                _menuTree.Nodes.Add(root);
            }
        _menuTree.ExpandAll();
        _menuTree.EndUpdate();
    }

    private static void AddChildNodes(TreeNode parent, CommandBarItemCollection items)
    {
        foreach (var item in items)
        {
            var node = new TreeNode(MenuLabel(item)) { Tag = item };
            if (item is CommandBarPopupItem popup)
                AddChildNodes(node, popup.DropDown.Items);
            parent.Nodes.Add(node);
        }
    }

    private static string MenuLabel(CommandBarItem item) => item switch
    {
        CommandBarSeparator => "──────────",
        CommandBarPopupItem p => p.DisplayText,
        CommandBarCommandItem c => c.Command.DisplayText,
        CommandBarLabel l => Command.RemoveMnemonic(l.Text),
        _ => item.GetType().Name,
    };

    private (CommandBar? container, int index) InsertTarget()
    {
        var sel = _menuTree.SelectedNode;
        if (sel is null)
        {
            var mb = FirstMenuBar();
            return (mb, mb?.Items.Count ?? 0);
        }
        // A container node (root menu bar or a submenu): append inside it.
        if (sel.Tag is CommandBar bar)
            return (bar, bar.Items.Count);
        if (sel.Tag is CommandBarPopupItem popup)
            return (popup.DropDown, popup.DropDown.Items.Count);
        // A leaf item: insert right after it in its parent's collection.
        var parent = ContainerOf(sel.Parent);
        if (parent is null || sel.Tag is not CommandBarItem item)
            return (parent, parent?.Items.Count ?? 0);
        return (parent, parent.Items.IndexOf(item) + 1);
    }

    private void AddNewItem(CommandBarItem item)
    {
        var (container, index) = InsertTarget();
        if (container is null)
            return;
        container.Items.Insert(Math.Clamp(index, 0, container.Items.Count), item);
        _manager.RefreshLayout();
        RebuildMenuTree();
        SelectItemNode(item);
    }

    private void AddCommandItem()
    {
        var command = PickCommand();
        if (command is not null)
            AddNewItem(CommandBarCustomizationItem.CreateCommandItem(command));
    }

    private void RenameMenuNode()
    {
        if (_menuTree.SelectedNode?.Tag is not CommandBarPopupItem popup)
            return;
        var name = PromptName("Rename Menu", "Menu caption (use & for a mnemonic):", popup.Text);
        if (name is null)
            return;
        popup.Text = name;
        _manager.RefreshLayout();
        RebuildMenuTree();
        SelectItemNode(popup);
    }

    private void RemoveMenuNode()
    {
        var sel = _menuTree.SelectedNode;
        if (sel?.Tag is not CommandBarItem item) // the root menu bar can't be removed here
            return;
        ContainerOf(sel.Parent)?.Items.Remove(item);
        _manager.RefreshLayout();
        RebuildMenuTree();
    }

    private void MoveMenuNode(int delta)
    {
        var sel = _menuTree.SelectedNode;
        if (sel?.Tag is not CommandBarItem item)
            return;
        var container = ContainerOf(sel.Parent);
        if (container is null)
            return;
        int i = container.Items.IndexOf(item);
        int j = i + delta;
        if (i < 0 || j < 0 || j >= container.Items.Count)
            return;
        container.Items.RemoveAt(i);
        container.Items.Insert(j, item);
        _manager.RefreshLayout();
        RebuildMenuTree();
        SelectItemNode(item);
    }

    private void ResetMenuNode()
    {
        var sel = _menuTree.SelectedNode;
        // A root menu (top-level or nested popup) restores just its dropdown;
        // the menu-bar root node restores the whole menu bar.
        if (sel?.Tag is CommandBarPopupItem popup)
            _manager.ResetMenu(popup);
        else if (sel?.Tag is CommandBar bar)
            _manager.ResetBar(bar);
        else
            return;
        RebuildMenuTree();
    }

    private void SelectItemNode(CommandBarItem item)
    {
        var node = FindNode(_menuTree.Nodes, item);
        if (node is null)
            return;
        _menuTree.SelectedNode = node;
        node.EnsureVisible();
    }

    private static TreeNode? FindNode(TreeNodeCollection nodes, CommandBarItem item)
    {
        foreach (TreeNode node in nodes)
        {
            if (ReferenceEquals(node.Tag, item))
                return node;
            var found = FindNode(node.Nodes, item);
            if (found is not null)
                return found;
        }
        return null;
    }

    private Command? PickCommand()
    {
        using var form = CreateDpiScaledForm();
        form.Text = "Add Command";
        form.FormBorderStyle = FormBorderStyle.SizableToolWindow;
        form.StartPosition = FormStartPosition.CenterParent;
        form.ClientSize = new Size(280, 360);
        form.MinimumSize = new Size(240, 260);
        form.ShowInTaskbar = false;
        var list = new ThemedListBox { Dock = DockStyle.Fill };
        foreach (var c in _commands)
            list.Items.Add(c.DisplayText);

        var ok = new ThemedButton
        {
            Text = "Add",
            DialogResult = DialogResult.OK,
            AutoSize = true,
            MinimumSize = new Size(84, 0),
            Margin = Padding.Empty,
        };
        var cancel = new ThemedButton
        {
            Text = "Cancel",
            DialogResult = DialogResult.Cancel,
            AutoSize = true,
            MinimumSize = new Size(84, 0),
            Margin = Padding.Empty,
        };
        var footer = CreateDialogButtonFooter(cancel, ok, new Padding(8));

        list.DoubleClick += (_, _) =>
        {
            if (list.SelectedIndex >= 0)
            {
                form.DialogResult = DialogResult.OK;
                form.Close();
            }
        };

        var layout = new ThemedTableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 2,
            UseWindowSurface = true,
        };
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f));
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100f));
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        layout.Controls.Add(list, 0, 0);
        layout.Controls.Add(footer, 0, 1);
        form.Controls.Add(layout);
        form.AcceptButton = ok;
        form.CancelButton = cancel;
        form.ResumeLayout(true);
        DialogSkin.Apply(form, _renderer.DialogColors);
        DialogSkin.ApplyWhenHandleCreated(form, _renderer.DialogColors);

        return form.ShowDialog(this) == DialogResult.OK && list.SelectedIndex >= 0
            ? _commands[list.SelectedIndex]
            : null;
    }

    private DialogTabPage BuildCommandsTab()
    {
        var tab = new DialogTabPage("Commands");

        _paletteHost.Dock = DockStyle.Fill;
        _paletteHost.AutoScroll = true;
        _paletteHost.BorderStyle = BorderStyle.FixedSingle;
        _paletteHost.Controls.Add(_palette);

        var hint = new Label
        {
            Dock = DockStyle.Fill,
            AutoSize = true,
            Text = "Drag a command onto any toolbar to add it.",
            Padding = new Padding(2, 8, 2, 2), // top gap keeps it clear of the list
        };

        var layout = new ThemedTableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 2,
            UseTabBodySurface = true,
        };
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f));
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100f));
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        layout.Controls.Add(_paletteHost, 0, 0);
        layout.Controls.Add(hint, 0, 1);
        tab.Controls.Add(layout);
        return tab;
    }

    private DialogTabPage BuildOptionsTab()
    {
        var tab = new DialogTabPage("Options");

        var iconLabel = new Label
        {
            Text = "Icon size:",
            AutoSize = true,
            Anchor = AnchorStyles.Left,
            Margin = new Padding(6, 8, 8, 8),
        };
        _iconCombo.DropDownStyle = ComboBoxStyle.DropDownList;
        _iconCombo.Dock = DockStyle.Fill;
        _iconCombo.MinimumSize = new Size(120, 0);
        _iconCombo.Margin = new Padding(0, 4, 6, 4);
        foreach (var s in IconSteps)
            _iconCombo.Items.Add(s + " px");
        _iconCombo.SelectedIndexChanged += OnIconSizeChanged;

        var tips = new CheckBox
        {
            Text = "Show ScreenTips (tooltips) on toolbars",
            AutoSize = true,
            Dock = DockStyle.Fill,
            Margin = new Padding(6, 4, 6, 4),
            Checked = _manager.ShowToolTips,
        };
        tips.CheckedChanged += (_, _) => _manager.ShowToolTips = tips.Checked;

        var layout = new ThemedTableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 2,
            RowCount = 3,
            UseTabBodySurface = true,
        };
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f));
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100f));
        layout.Controls.Add(iconLabel, 0, 0);
        layout.Controls.Add(_iconCombo, 1, 0);
        layout.Controls.Add(tips, 0, 1);
        layout.SetColumnSpan(tips, 2);
        tab.Controls.Add(layout);
        return tab;
    }

    private IEnumerable<Command> AllCommands()
    {
        foreach (var c in _manager.Commands)
            yield return c;
    }

    // --- Toolbars tab behavior ---------------------------------------------

    private CommandBar? SelectedBar()
    {
        int i = _toolbarList.SelectedIndex;
        return i >= 0 && i < _listedBars.Count ? _listedBars[i] : null;
    }

    private void OnToolbarItemCheck(object? sender, ItemCheckEventArgs e)
    {
        if (_suppress || e.Index < 0 || e.Index >= _listedBars.Count)
            return;
        var bar = _listedBars[e.Index];
        bool visible = e.NewValue == CheckState.Checked;
        // Let the check state settle before rebuilding the bars.
        BeginInvoke((MethodInvoker)(() =>
        {
            bar.Visible = visible;
            _manager.RefreshLayout();
        }));
    }

    private void NewToolbar()
    {
        var name = PromptName("New Toolbar", "Toolbar name:", UniqueName("Custom"));
        if (name is null)
            return;
        name = UniqueName(name);
        var bar = _manager.AddBar(name, CommandBarType.Toolbar);
        bar.Dock = DockState.Top;
        bar.Visible = true;
        _manager.RefreshLayout();
        RefreshToolbarList();
        SelectBar(bar);
    }

    private void RenameToolbar()
    {
        var bar = SelectedBar();
        if (bar is null || !bar.AllowCustomize)
            return;
        var name = PromptName("Rename Toolbar", "New name:", bar.Text);
        if (name is null)
            return;
        bar.Text = name; // display only; the stable Name (persistence key) is unchanged
        _manager.RefreshLayout();
        RefreshToolbarList();
        SelectBar(bar);
    }

    private void DeleteToolbar()
    {
        var bar = SelectedBar();
        if (bar is null || !bar.AllowCustomize)
            return;
        _manager.RemoveBar(bar.Name);
        RefreshToolbarList();
    }

    private void ResetToolbar()
    {
        var bar = SelectedBar();
        if (bar is null)
            return;
        _manager.ResetBar(bar); // restores its items to the captured defaults
        RefreshToolbarList();
        SelectBar(bar);
    }

    private string UniqueName(string baseName)
    {
        baseName = string.IsNullOrWhiteSpace(baseName) ? "Custom" : baseName.Trim();
        if (_manager.FindBar(baseName) is null)
            return baseName;
        for (int i = 2; ; i++)
        {
            string candidate = baseName + " " + i;
            if (_manager.FindBar(candidate) is null)
                return candidate;
        }
    }

    private void SelectBar(CommandBar bar)
    {
        int i = _listedBars.IndexOf(bar);
        if (i >= 0)
            _toolbarList.SelectedIndex = i;
    }

    private void RefreshToolbarList()
    {
        _suppress = true;
        int sel = _toolbarList.SelectedIndex;
        _listedBars.Clear();
        _toolbarList.Items.Clear();
        foreach (var bar in _manager.Bars)
            if (bar.BarType == CommandBarType.Toolbar)
            {
                _listedBars.Add(bar);
                _toolbarList.Items.Add(bar.Text, bar.Visible);
            }
        if (sel >= 0 && sel < _toolbarList.Items.Count)
            _toolbarList.SelectedIndex = sel;
        _suppress = false;
    }

    // --- Options tab behavior ----------------------------------------------

    private void OnIconSizeChanged(object? sender, EventArgs e)
    {
        if (_suppress || _iconCombo.SelectedIndex < 0)
            return;
        int size = IconSteps[_iconCombo.SelectedIndex];
        foreach (var bar in _manager.Bars)
            if (bar.BarType == CommandBarType.Toolbar)
                bar.IconSize = size;
        _manager.RefreshLayout();
    }

    private void SyncIconCombo()
    {
        _suppress = true;
        int size = 24;
        foreach (var bar in _manager.Bars)
            if (bar.BarType == CommandBarType.Toolbar)
            {
                size = bar.IconSize;
                break;
            }
        int idx = Array.IndexOf(IconSteps, size);
        _iconCombo.SelectedIndex = idx >= 0 ? idx : Array.IndexOf(IconSteps, 24);
        _suppress = false;
    }

    // --- Lifecycle ---------------------------------------------------------

    private void OnManagerLayoutChanged(object? sender, EventArgs e)
    {
        if (!_suppress)
            RefreshToolbarList();
    }

    /// <summary>
    /// Builds the palette with compound/application factories first, so a split
    /// button or hosted control wins over the generic command fallback when ids
    /// intentionally match.
    /// </summary>
    internal static List<CommandBarCustomizationItem> BuildPaletteItems(
        CommandBarManager manager,
        IEnumerable<Command> commands)
    {
        var result = new List<CommandBarCustomizationItem>();
        var used = new HashSet<string>(StringComparer.Ordinal);
        foreach (var item in manager.CustomizationItems)
            if (used.Add(item.Id))
                result.Add(item);
        foreach (var command in commands)
            if (used.Add(command.Id))
                result.Add(CommandBarCustomizationItem.FromCommand(command));
        return result;
    }

    private void OnManagerThemeChanged(object? sender, EventArgs e)
        => SetRenderer(_manager.Renderer);

    protected override void OnFormClosed(FormClosedEventArgs e)
    {
        _manager.LayoutChanged -= OnManagerLayoutChanged;
        _manager.ThemeChanged -= OnManagerThemeChanged;
        _manager.EndCustomize();
        base.OnFormClosed(e);
    }

    // --- Small name prompt -------------------------------------------------

    private string? PromptName(string title, string prompt, string initial)
    {
        using var form = CreateDpiScaledForm();
        form.Text = title;
        form.FormBorderStyle = FormBorderStyle.FixedDialog;
        form.StartPosition = FormStartPosition.CenterParent;
        form.AutoSize = true;
        form.AutoSizeMode = AutoSizeMode.GrowAndShrink;
        form.MinimumSize = new Size(300, 0);
        form.MinimizeBox = false;
        form.MaximizeBox = false;
        form.ShowInTaskbar = false;
        var label = new Label
        {
            Text = prompt,
            AutoSize = true,
            Anchor = AnchorStyles.Left,
            Margin = new Padding(0, 0, 0, 6),
        };
        var box = new TextBox
        {
            Text = initial,
            Dock = DockStyle.Fill,
            MinimumSize = new Size(276, 0),
            Margin = Padding.Empty,
        };
        var ok = new ThemedButton
        {
            Text = "OK",
            DialogResult = DialogResult.OK,
            AutoSize = true,
            MinimumSize = new Size(84, 0),
            Margin = Padding.Empty,
        };
        var cancel = new ThemedButton
        {
            Text = "Cancel",
            DialogResult = DialogResult.Cancel,
            AutoSize = true,
            MinimumSize = new Size(84, 0),
            Margin = Padding.Empty,
        };
        var buttons = CreateDialogButtonFooter(ok, cancel, new Padding(12, 10, 12, 10));

        var body = new ThemedTableLayoutPanel
        {
            Dock = DockStyle.Fill,
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            ColumnCount = 1,
            RowCount = 2,
            Padding = new Padding(12),
            MinimumSize = new Size(300, 0),
            UseWindowSurface = true,
        };
        body.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        body.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        body.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        body.Controls.Add(label, 0, 0);
        body.Controls.Add(box, 0, 1);

        var layout = new ThemedTableLayoutPanel
        {
            Dock = DockStyle.Fill,
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            ColumnCount = 1,
            RowCount = 2,
            MinimumSize = new Size(300, 0),
            UseWindowSurface = true,
        };
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        layout.Controls.Add(body, 0, 0);
        layout.Controls.Add(buttons, 0, 1);
        form.Controls.Add(layout);
        form.AcceptButton = ok;
        form.CancelButton = cancel;
        box.SelectAll();
        form.ResumeLayout(true);
        DialogSkin.Apply(form, _renderer.DialogColors);
        DialogSkin.ApplyWhenHandleCreated(form, _renderer.DialogColors);

        return form.ShowDialog(this) == DialogResult.OK && box.Text.Trim().Length > 0
            ? box.Text.Trim()
            : null;
    }

    private bool Confirm(string message, string title)
    {
        using var form = CreateDpiScaledForm();
        form.Text = title;
        form.FormBorderStyle = FormBorderStyle.FixedDialog;
        form.StartPosition = FormStartPosition.CenterParent;
        form.AutoSize = true;
        form.AutoSizeMode = AutoSizeMode.GrowAndShrink;
        form.MinimumSize = new Size(360, 0);
        form.MinimizeBox = false;
        form.MaximizeBox = false;
        form.ShowInTaskbar = false;
        var icon = new PictureBox
        {
            Image = SystemIcons.Question.ToBitmap(),
            SizeMode = PictureBoxSizeMode.CenterImage,
            Size = new Size(40, 40),
            Anchor = AnchorStyles.Top,
            Margin = new Padding(0, 0, 10, 0),
        };
        var label = new Label
        {
            Text = message,
            AutoSize = true,
            MaximumSize = new Size(280, 0),
            Anchor = AnchorStyles.Left,
            Margin = Padding.Empty,
        };
        var no = new ThemedButton
        {
            Text = "No",
            DialogResult = DialogResult.No,
            AutoSize = true,
            MinimumSize = new Size(84, 0),
            Margin = Padding.Empty,
        };
        var yes = new ThemedButton
        {
            Text = "Yes",
            DialogResult = DialogResult.Yes,
            AutoSize = true,
            MinimumSize = new Size(84, 0),
            Margin = Padding.Empty,
        };
        var footer = CreateDialogButtonFooter(yes, no, new Padding(8, 10, 8, 8));

        var body = new ThemedTableLayoutPanel
        {
            Dock = DockStyle.Fill,
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            ColumnCount = 2,
            RowCount = 1,
            Padding = new Padding(12),
            UseWindowSurface = true,
        };
        body.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        body.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        body.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        body.Controls.Add(icon, 0, 0);
        body.Controls.Add(label, 1, 0);

        var layout = new ThemedTableLayoutPanel
        {
            Dock = DockStyle.Fill,
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            ColumnCount = 1,
            RowCount = 2,
            MinimumSize = new Size(360, 0),
            UseWindowSurface = true,
        };
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        layout.Controls.Add(body, 0, 0);
        layout.Controls.Add(footer, 0, 1);
        form.Controls.Add(layout);
        form.AcceptButton = yes;
        form.CancelButton = no;
        form.ResumeLayout(true);
        DialogSkin.Apply(form, _renderer.DialogColors);
        DialogSkin.ApplyWhenHandleCreated(form, _renderer.DialogColors);
        return form.ShowDialog(this) == DialogResult.Yes;
    }

    internal static Form CreateDpiScaledForm()
    {
        var form = new Form();
        form.SuspendLayout();
        form.AutoScaleMode = AutoScaleMode.Dpi;
        form.AutoScaleDimensions = new SizeF(LayoutDpi, LayoutDpi);
        return form;
    }

    internal static ThemedTableLayoutPanel CreateDialogButtonFooter(
        Button leftButton,
        Button rightButton,
        Padding padding)
    {
        Size leftPreferred = leftButton.GetPreferredSize(Size.Empty);
        Size rightPreferred = rightButton.GetPreferredSize(Size.Empty);
        int buttonWidth = Math.Max(
            Math.Max(leftPreferred.Width, leftButton.MinimumSize.Width),
            Math.Max(rightPreferred.Width, rightButton.MinimumSize.Width));
        int buttonHeight = Math.Max(
            Math.Max(leftPreferred.Height, leftButton.MinimumSize.Height),
            Math.Max(rightPreferred.Height, rightButton.MinimumSize.Height));

        leftButton.AutoSize = false;
        rightButton.AutoSize = false;
        leftButton.Size = new Size(buttonWidth, buttonHeight);
        rightButton.Size = new Size(buttonWidth, buttonHeight);
        leftButton.Margin = Padding.Empty;
        rightButton.Margin = Padding.Empty;

        var footer = new ThemedTableLayoutPanel
        {
            Dock = DockStyle.Fill,
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            ColumnCount = 4,
            RowCount = 1,
            Padding = padding,
            Margin = Padding.Empty,
            UseAlternateSurface = true,
        };
        footer.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f));
        footer.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, buttonWidth));
        footer.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 8f));
        footer.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, buttonWidth));
        footer.RowStyles.Add(new RowStyle(SizeType.Absolute, buttonHeight));
        footer.Controls.Add(leftButton, 1, 0);
        footer.Controls.Add(rightButton, 3, 0);
        return footer;
    }

    private static void FitButtonHost(ThemedFlowLayoutPanel host, List<Button> buttons)
    {
        int widest = EqualizeWidths(buttons);
        if (widest == 0)
            return;
        host.MinimumSize = new Size(widest + SystemInformation.VerticalScrollBarWidth + 2, 0);
        host.PerformLayout();
    }

    private static int EqualizeWidths(List<Button> buttons)
    {
        if (buttons.Count == 0)
            return 0;
        int widest = 0;
        foreach (var b in buttons)
            widest = Math.Max(widest, b.PreferredSize.Width);
        foreach (var b in buttons)
            b.MinimumSize = new Size(widest, b.MinimumSize.Height);
        return widest;
    }
}
