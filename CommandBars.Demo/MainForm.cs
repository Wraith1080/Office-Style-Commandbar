using CommandBars;
using CommandBars.Controls;
using CommandBars.Imaging;
using CommandBars.Model;
using CommandBars.Rendering;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Security.Policy;
using System.Windows.Forms;

namespace CommandBars.Demo;

/// <summary>
/// Demo: a themeable menu bar and toolbar in a dock host, wired to sample
/// commands. The View menu switches between Office 2003 / XP / 2007 live.
/// </summary>
public sealed class MainForm : Form
{
    // The fixed icon-size steps, shared by the View menu and the Customize dialog.
    private static readonly int[] IconSizeSteps = { 12, 16, 20, 24, 32, 48, 64 };

    private readonly CommandBarManager _manager = new();
    private readonly Label _status;
    private DockHost _dockTop = null!;
    private DockHost _dockLeft = null!;
    private DockHost _dockRight = null!;
    private DockHost _dockBottom = null!;
    private DockHost[] _docks = null!;
    private CustomizeDialog? _customizeDialog;

    public MainForm()
    {
        Text = "CommandBars Demo — Office 2003";
        ClientSize = new Size(920, 600);
        StartPosition = FormStartPosition.CenterScreen;
        BackColor = Color.White;
        Font = SystemFonts.MenuFont!;

        BuildCommands();
        BuildBars();
        _manager.CaptureDefaults(); // snapshot the factory layout for Customize → Reset

        var hint = new Label
        {
            Dock = DockStyle.Fill,
            TextAlign = ContentAlignment.MiddleCenter,
            ForeColor = Color.Gray,
            Font = new Font("Segoe UI", 11f),
            Text = "Office CommandBar — themes/icon-size in View, chevron → Add or Remove Buttons.\r\n" +
                   "Drag a toolbar's gripper off the bar to float it, or onto the left/right/bottom edge to dock it there;\r\n" +
                   "double-click a floating caption to re-dock. Your layout is saved on exit and restored on start.",
        };

        _status = new Label
        {
            Dock = DockStyle.Bottom,
            Height = 24,
            TextAlign = ContentAlignment.MiddleLeft,
            BorderStyle = BorderStyle.Fixed3D,
            Padding = new Padding(6, 0, 0, 0),
            Text = "Ready",
        };

        _dockTop = new DockHost { Edge = DockEdge.Top, Manager = _manager };
        _dockLeft = new DockHost { Edge = DockEdge.Left, Manager = _manager };
        _dockRight = new DockHost { Edge = DockEdge.Right, Manager = _manager };
        _dockBottom = new DockHost { Edge = DockEdge.Bottom, Manager = _manager };
        _docks = new[] { _dockTop, _dockLeft, _dockRight, _dockBottom };

        // Dock resolves in reverse add order (last added is placed first). This
        // order gives full-width Top and Bottom bands, side bands between them,
        // the status strip flush to the form bottom, and the hint in the center.
        Controls.Add(hint);        // Fill (center)
        Controls.Add(_dockRight);  // Right band
        Controls.Add(_dockLeft);   // Left band
        Controls.Add(_dockBottom); // Bottom band (above status)
        Controls.Add(_status);     // status strip (flush bottom)
        Controls.Add(_dockTop);    // Top band (full width)

        // Add order (above) is chosen for docking resolution, which also sets the
        // default tab order — so set it explicitly: Tab starts at the top toolbar.
        _dockTop.TabIndex = 0;
        _dockLeft.TabIndex = 1;
        _dockRight.TabIndex = 2;
        _dockBottom.TabIndex = 3;
        hint.TabIndex = 4;
        _status.TabIndex = 5;

        _manager.CustomizeChanged += (_, _) =>
        {
            _manager.Commands["customize.mode"].Checked = CheckIf(_manager.IsCustomizing);
            SetStatus(_manager.IsCustomizing
                ? "Customize mode ON — drag buttons to reorder/move/remove; drag from the Commands tab to add; manage bars in the dialog."
                : "Customize mode off");
        };

        // Keep the View-menu toolbar checks in sync after any layout change
        // (e.g. a Reset All from the Customize dialog).
        _manager.LayoutChanged += (_, _) => RefreshCustomizeChecks();

        // "Customize..." from a toolbar's chevron menu opens (or re-activates) the dialog.
        _manager.CustomizeRequested += (_, _) =>
        {
            if (_customizeDialog is { IsDisposed: false })
                _customizeDialog.Activate();
            else
                OpenCustomizeDialog();
        };

        ApplyTheme("2003");
        _manager.Commands["iconsize.24"].Checked = CommandCheckState.Checked;
        _manager.Commands["toolbars.standard"].Checked = CommandCheckState.Checked;
        _manager.Commands["toolbars.formatting"].Checked = CommandCheckState.Checked;
        _manager.Commands["toolbars.navigation"].Checked = CommandCheckState.Checked;
        _manager.Commands["toolbars.paragraph"].Checked = CommandCheckState.Checked;
        _manager.Commands["align.left"].Checked = CommandCheckState.Checked;

        // Restore a previously saved layout, if any.
        LoadLayoutFromFile();
    }

    private void BuildCommands()
    {
        Register("file.new", "&New", DemoSvgIcons.Get("new"), Keys.Control | Keys.N);
        Register("file.open", "&Open...", DemoSvgIcons.Get("open"), Keys.Control | Keys.O);
        Register("file.save", "&Save", DemoSvgIcons.Get("save"), Keys.Control | Keys.S);
        Register("file.exit", "E&xit", null, Keys.None, act: Close);

        // Explicit ScreenTips (others fall back to the command name + shortcut).
        _manager.Commands["file.new"].ToolTip = "Create a new document";
        _manager.Commands["file.open"].ToolTip = "Open an existing document";
        _manager.Commands["file.save"].ToolTip = "Save the current document";

        Register("edit.cut", "Cu&t", DemoSvgIcons.Get("cut"), Keys.Control | Keys.X);
        Register("edit.copy", "&Copy", DemoSvgIcons.Get("copy"), Keys.Control | Keys.C);
        Register("edit.paste", "&Paste", DemoSvgIcons.Get("paste"), Keys.Control | Keys.V);

        Register("format.bold", "&Bold", DemoSvgIcons.Get("bold"), Keys.Control | Keys.B, toggle: true);
        Register("format.italic", "&Italic", DemoSvgIcons.Get("italic"), Keys.Control | Keys.I, toggle: true);
        Register("format.underline", "&Underline", DemoSvgIcons.Get("underline"), Keys.Control | Keys.U, toggle: true);

        Register("nav.back", "&Back", DemoSvgIcons.Get("back"), Keys.None);
        Register("nav.forward", "&Forward", DemoSvgIcons.Get("forward"), Keys.None);
        Register("nav.refresh", "&Refresh", DemoSvgIcons.Get("refresh"), Keys.None);
        Register("nav.home", "&Home", DemoSvgIcons.Get("home"), Keys.None, ShowDesignerDemo);

        // Dropdown entries for the split buttons.
        Register("new.doc", "&Document", DemoSvgIcons.Get("new"), Keys.None);
        Register("new.template", "&Template", DemoSvgIcons.Get("open"), Keys.None);
        Register("nav.hist1", "Getting Started", null, Keys.None);
        Register("nav.hist2", "Recent Files", null, Keys.None);
        Register("nav.hist3", "Home", null, Keys.None);

        RegisterAlign("align.left", "Align &Left", DemoSvgIcons.Get("align-left"));
        RegisterAlign("align.center", "&Center", DemoSvgIcons.Get("align-center"));
        RegisterAlign("align.right", "Align &Right", DemoSvgIcons.Get("align-right"));
        RegisterAlign("align.justify", "&Justify", DemoSvgIcons.Get("align-justify"));

        RegisterTheme("theme.2003", "Office &2003", "2003");
        RegisterTheme("theme.xp", "Office &XP", "xp");
        RegisterTheme("theme.2007", "Office 200&7", "2007");
        RegisterTheme("theme.2010", "Office 20&10 (Silver)", "2010");
        RegisterTheme("theme.dark", "&Dark", "dark");

        foreach (var s in IconSizeSteps)
            RegisterIconSize($"iconsize.{s}", $"{s} px", s);

        RegisterToolbarToggle("toolbars.standard", "&Standard", "Standard");
        RegisterToolbarToggle("toolbars.formatting", "&Formatting", "Formatting");
        RegisterToolbarToggle("toolbars.navigation", "Navi&gation", "Navigation");
        RegisterToolbarToggle("toolbars.paragraph", "&Paragraph", "Paragraph");

        RegisterCustomizeToggle("customize.mode", "&Customize Toolbars");

    }

    private void RegisterAlign(string id, string text, IImageSource image)
    {
        _manager.Commands.Register(id, c =>
        {
            c.Text = text;
            c.Image = image;
            c.IsCheckable = true;
            c.ExecuteHandler = _ =>
            {
                foreach (var aid in new[] { "align.left", "align.center", "align.right", "align.justify" })
                    _manager.Commands[aid].Checked = CheckIf(aid == id);
                SetStatus($"Paragraph: {c.DisplayText}");
            };
        });
    }

    private void RegisterCustomizeToggle(string id, string text)
    {
        _manager.Commands.Register(id, c =>
        {
            c.Text = text;
            c.IsCheckable = true;
            c.ExecuteHandler = _ => ToggleCustomizeDialog();
        });
    }

    private void ToggleCustomizeDialog()
    {
        if (_customizeDialog is { IsDisposed: false })
            _customizeDialog.Close(); // closing exits customize mode
        else
            OpenCustomizeDialog();
    }

    private void OpenCustomizeDialog()
    {
        _customizeDialog = new CustomizeDialog(_manager, _dockTop.Renderer, PaletteCommands()) { Owner = this };
        _customizeDialog.FormClosed += (_, _) => _customizeDialog = null;
        _customizeDialog.Show(this); // constructor enters customize mode
    }

    private IEnumerable<Command> PaletteCommands()
    {
        string[] ids =
        {
            "file.new", "file.open", "file.save",
            "edit.cut", "edit.copy", "edit.paste",
            "format.bold", "format.italic", "format.underline",
            "nav.back", "nav.forward", "nav.refresh", "nav.home",
            "align.left", "align.center", "align.right", "align.justify",
        };
        foreach (var id in ids)
            yield return _manager.Commands[id];
    }

    private void RegisterToolbarToggle(string id, string text, string barName)
    {
        _manager.Commands.Register(id, c =>
        {
            c.Text = text;
            c.IsCheckable = true;
            c.ExecuteHandler = _ =>
            {
                var bar = _manager.FindBar(barName);
                if (bar is null)
                    return;
                bar.Visible = !bar.Visible;
                c.Checked = CheckIf(bar.Visible);
                _manager.RefreshLayout();
                SetStatus($"{barName} toolbar: {(bar.Visible ? "shown" : "hidden")}");
            };
        });
    }

    private string LayoutPath => Path.Combine(AppContext.BaseDirectory, "commandbars.json");

    private void LoadLayoutFromFile()
    {
        _manager.LoadLayout(LayoutPath);
        if (_manager.GetSetting("theme") is { } theme)
            ApplyTheme(theme); // restore the saved theme
        RefreshCustomizeChecks();
    }

    // Auto-save the layout on exit; it is auto-loaded in the constructor.
    protected override void OnFormClosing(FormClosingEventArgs e)
    {
        _manager.SaveLayout(LayoutPath);
        base.OnFormClosing(e);
    }

    private void RefreshCustomizeChecks()
    {
        _manager.Commands["toolbars.standard"].Checked = CheckIf(_manager.FindBar("Standard")?.Visible ?? true);
        _manager.Commands["toolbars.formatting"].Checked = CheckIf(_manager.FindBar("Formatting")?.Visible ?? true);
        _manager.Commands["toolbars.navigation"].Checked = CheckIf(_manager.FindBar("Navigation")?.Visible ?? true);
        _manager.Commands["toolbars.paragraph"].Checked = CheckIf(_manager.FindBar("Paragraph")?.Visible ?? true);
        int size = _manager.FindBar("Standard")?.IconSize ?? 24;
        foreach (var s in IconSizeSteps)
            _manager.Commands[$"iconsize.{s}"].Checked = CheckIf(size == s);
    }

    private void RegisterIconSize(string id, string text, int size)
    {
        _manager.Commands.Register(id, c =>
        {
            c.Text = text;
            c.IsCheckable = true;
            c.ExecuteHandler = _ => ApplyIconSize(size);
        });
    }

    private void ApplyIconSize(int size)
    {
        foreach (var bar in _manager.Bars)
            if (bar.BarType == CommandBarType.Toolbar)
                bar.IconSize = size;
        _manager.RefreshLayout();

        foreach (var s in IconSizeSteps)
            _manager.Commands[$"iconsize.{s}"].Checked = CheckIf(size == s);
        SetStatus($"Icon size: {size}px");
    }

    private void RegisterTheme(string id, string text, string themeKey)
    {
        _manager.Commands.Register(id, c =>
        {
            c.Text = text;
            c.IsCheckable = true;
            c.ExecuteHandler = _ => ApplyTheme(themeKey);
        });
    }

    private void ApplyTheme(string themeKey)
    {
        // The manager owns the theme now; setting it re-skins every hosted band.
        _manager.Theme = themeKey switch
        {
            "xp" => CommandBarTheme.OfficeXP,
            "2007" => CommandBarTheme.Office2007,
            "2010" => CommandBarTheme.Office2010,
            "dark" => CommandBarTheme.Dark,
            _ => CommandBarTheme.Office2003,
        };
        if (_customizeDialog is { IsDisposed: false })
            _customizeDialog.SetRenderer(_manager.Renderer);

        // Radio behavior: check the active theme, clear the others.
        _manager.Commands["theme.2003"].Checked = CheckIf(themeKey == "2003");
        _manager.Commands["theme.xp"].Checked = CheckIf(themeKey == "xp");
        _manager.Commands["theme.2007"].Checked = CheckIf(themeKey == "2007");
        _manager.Commands["theme.2010"].Checked = CheckIf(themeKey == "2010");
        _manager.Commands["theme.dark"].Checked = CheckIf(themeKey == "dark");

        _manager.SetSetting("theme", themeKey); // persisted with the layout

        string label = themeKey switch
        {
            "xp" => "Office XP",
            "2007" => "Office 2007",
            "2010" => "Office 2010",
            "dark" => "Dark",
            _ => "Office 2003",
        };
        Text = $"CommandBars Demo — {label}";
        SetStatus($"Theme: {label}");
    }

    private static CommandCheckState CheckIf(bool on)
        => on ? CommandCheckState.Checked : CommandCheckState.Unchecked;

    // The Home button opens the designer-defined demo form; only one at a time.
    private DesignerDemoForm? _designerDemo;

    private void ShowDesignerDemo()
    {
        if (_designerDemo is { IsDisposed: false })
        {
            _designerDemo.Activate();
            return;
        }
        _designerDemo = new DesignerDemoForm();
        _designerDemo.Manager.Theme = _manager.Theme; // sync the theme
        _designerDemo.FormClosed += (_, _) => _designerDemo = null;
        foreach (var bar in _designerDemo.Manager.Bars)
            if (bar.BarType == CommandBarType.Toolbar)
                bar.IconSize = _manager.Bars[1].IconSize;
        _manager.RefreshLayout();
        _designerDemo.Show(this);
    }

    private void Register(string id, string text, IImageSource? image, Keys shortcut,
        Action? act = null, bool toggle = false)
    {
        _manager.Commands.Register(id, c =>
        {
            c.Text = text;
            c.Image = image;
            c.Shortcut = shortcut;
            c.ExecuteHandler = _ =>
            {
                act?.Invoke();
                if (toggle)
                    SetStatus($"{c.DisplayText}: {(c.Checked == CommandCheckState.Checked ? "ON" : "off")}");
                else
                    SetStatus($"Executed: {c.DisplayText}");
            };
        });
    }

    private void BuildBars()
    {
        var menu = _manager.AddBar("MenuBar", CommandBarType.MenuBar);

        var file = menu.Items.AddPopup("&File");
        file.DropDown.Items.AddButton(_manager.Commands["file.new"]);
        file.DropDown.Items.AddButton(_manager.Commands["file.open"]);
        file.DropDown.Items.AddButton(_manager.Commands["file.save"]);
        file.DropDown.Items.AddSeparator();
        file.DropDown.Items.AddButton(_manager.Commands["file.exit"]);

        var edit = menu.Items.AddPopup("&Edit");
        edit.DropDown.Items.AddButton(_manager.Commands["edit.cut"]);
        edit.DropDown.Items.AddButton(_manager.Commands["edit.copy"]);
        edit.DropDown.Items.AddButton(_manager.Commands["edit.paste"]);

        var format = menu.Items.AddPopup("F&ormat");
        format.DropDown.Items.AddToggle(_manager.Commands["format.bold"]);
        format.DropDown.Items.AddToggle(_manager.Commands["format.italic"]);
        format.DropDown.Items.AddToggle(_manager.Commands["format.underline"]);

        var view = menu.Items.AddPopup("&View");
        view.DropDown.Items.AddToggle(_manager.Commands["theme.2003"]);
        view.DropDown.Items.AddToggle(_manager.Commands["theme.xp"]);
        view.DropDown.Items.AddToggle(_manager.Commands["theme.2007"]);
        view.DropDown.Items.AddToggle(_manager.Commands["theme.2010"]);
        view.DropDown.Items.AddToggle(_manager.Commands["theme.dark"]);
        view.DropDown.Items.AddSeparator();
        var iconSize = view.DropDown.Items.AddPopup("Icon &Size");
        foreach (var s in IconSizeSteps)
            iconSize.DropDown.Items.AddToggle(_manager.Commands[$"iconsize.{s}"]);
        view.DropDown.Items.AddSeparator();
        var toolbars = view.DropDown.Items.AddPopup("&Toolbars");
        toolbars.DropDown.Items.AddToggle(_manager.Commands["toolbars.standard"]);
        toolbars.DropDown.Items.AddToggle(_manager.Commands["toolbars.formatting"]);
        toolbars.DropDown.Items.AddToggle(_manager.Commands["toolbars.navigation"]);
        toolbars.DropDown.Items.AddToggle(_manager.Commands["toolbars.paragraph"]);
        view.DropDown.Items.AddSeparator();
        view.DropDown.Items.AddToggle(_manager.Commands["customize.mode"]);

        var standard = _manager.AddBar("Standard", CommandBarType.Toolbar);
        standard.IconSize = 24;
        AddToolSplit(standard, "file.new", "new.doc", "new.template"); // horizontal split button
        AddToolButton(standard, "file.open");
        AddToolButton(standard, "file.save");
        standard.Items.AddSeparator();
        AddToolButton(standard, "edit.cut");
        AddToolButton(standard, "edit.copy");
        AddToolButton(standard, "edit.paste");

        var formatting = _manager.AddBar("Formatting", CommandBarType.Toolbar);
        formatting.IconSize = 24;
        AddToolToggle(formatting, "format.bold");
        AddToolToggle(formatting, "format.italic");
        AddToolToggle(formatting, "format.underline");

        var navigation = _manager.AddBar("Navigation", CommandBarType.Toolbar);
        navigation.IconSize = 24;
        navigation.Dock = DockState.Left; // starts as a vertical toolbar on the left band
        AddToolSplit(navigation, "nav.back", "nav.hist1", "nav.hist2", "nav.hist3"); // vertical split button
        AddToolButton(navigation, "nav.forward");
        AddToolButton(navigation, "nav.refresh");
        AddToolButton(navigation, "nav.home");

        var paragraph = _manager.AddBar("Paragraph", CommandBarType.Toolbar);
        paragraph.IconSize = 24;
        AddToolToggle(paragraph, "align.left");
        AddToolToggle(paragraph, "align.center");
        AddToolToggle(paragraph, "align.right");
        AddToolToggle(paragraph, "align.justify");
    }

    private void AddToolButton(CommandBar bar, string commandId)
    {
        var button = bar.Items.AddButton(_manager.Commands[commandId]);
        button.DisplayStyle = CommandItemDisplayStyle.ImageOnly;
    }

    private void AddToolToggle(CommandBar bar, string commandId)
    {
        var toggle = bar.Items.AddToggle(_manager.Commands[commandId]);
        toggle.DisplayStyle = CommandItemDisplayStyle.ImageOnly;
    }

    private void AddToolSplit(CommandBar bar, string commandId, params string[] dropDownIds)
    {
        var split = bar.Items.AddSplitButton(_manager.Commands[commandId]);
        split.DisplayStyle = CommandItemDisplayStyle.ImageOnly;
        foreach (var id in dropDownIds)
            split.DropDown.Items.AddButton(_manager.Commands[id]);
    }

    private void SetStatus(string text) => _status.Text = text;

    // Route command shortcuts (Ctrl+S, Ctrl+B, ...) regardless of focus.
    protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
    {
        if (_manager.ProcessShortcut(keyData))
            return true;
        return base.ProcessCmdKey(ref msg, keyData);
    }

    public CommandBarTheme Theme
    {
        get => _manager?.Theme ?? CommandBarTheme.Office2003;
        set
        {
            if (_manager != null)
                _manager.Theme = value;
        }
    }
}
