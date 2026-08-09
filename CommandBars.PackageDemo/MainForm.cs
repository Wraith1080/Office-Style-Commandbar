using System;
using System.Collections.Generic;
using System.IO;
using System.Windows.Forms;
using CommandBars;
using CommandBars.Controls;
using CommandBars.Model;
using CommandBars.Rendering;

namespace CommandBars.PackageDemo;

/// <summary>
/// Package-consuming counterpart of CommandBars.Demo. The bars, menus, nested
/// palettes, command presentation, and SVG images live in MainForm.Designer.cs;
/// this file supplies only runtime behavior that a designer definition cannot:
/// command handlers, theme/layout changes, Customize, and persistence.
/// </summary>
public partial class MainForm : Form
{
    private static readonly int[] IconSizeSteps = { 12, 16, 20, 24, 32, 48, 64 };

    private static readonly string[] PaletteColors =
    {
        "#000000", "#993300", "#333300", "#003300", "#003366", "#000080", "#333399", "#333333",
        "#800000", "#FF6600", "#808000", "#008000", "#008080", "#0000FF", "#666699", "#808080",
        "#FF0000", "#FF9900", "#99CC00", "#339966", "#33CCCC", "#3366FF", "#800080", "#969696",
        "#FF00FF", "#FFCC00", "#FFFF00", "#00FF00", "#00FFFF", "#00CCFF", "#993366", "#C0C0C0",
        "#FF99CC", "#FFCC99", "#FFFF99", "#CCFFCC", "#CCFFFF", "#99CCFF", "#CC99FF", "#FFFFFF",
    };

    private CustomizeDialog? _customizeDialog;

    public MainForm()
    {
        InitializeComponent();
        RegisterCommands();
        _manager.BuildFromDefinitions();
        _manager.CaptureDefaults();

        _manager.CustomizeChanged += (_, _) =>
        {
            _manager.Commands["customize.mode"].Checked = CheckIf(_manager.IsCustomizing);
            SetStatus(_manager.IsCustomizing
                ? "Customize mode ON — drag buttons to reorder, move, remove, or add commands."
                : "Customize mode off");
        };
        _manager.CustomizeRequested += (_, _) => OpenCustomizeDialog();

        if (_manager.FindBar("Formatting")?.FindComboBox("font.combo") is { } fontCombo)
            fontCombo.SelectedItemChanged += (_, _) => SetStatus($"Font: {fontCombo.SelectedItem}");

        // The designer definition opts this compound control into the command
        // list. Re-register the same stable id with an app-aware factory so every
        // copy dragged onto a custom toolbar also gets the demo's selection
        // behavior, not just its visual/data definition.
        _manager.RegisterCustomizationItem(new CommandBarCustomizationItem(
            "font.combo",
            "Font",
            _svgImages.Get("font"),
            CreateFontCombo));

        _manager.ThemeChanged += (_, _) => UpdateThemeCaption();
        _manager.Commands["iconsize.24"].Checked = CommandCheckState.Checked;
        RefreshIconSizeChecks();
        _manager.Commands["align.left"].Checked = CommandCheckState.Checked;
        LoadLayoutFromFile();
        UpdateThemeCaption();
    }

    private void RegisterCommands()
    {
        Register("file.new", "&New", Keys.Control | Keys.N);
        Register("file.open", "&Open…", Keys.Control | Keys.O);
        Register("file.save", "&Save", Keys.Control | Keys.S);
        Register("file.exit", "E&xit", Keys.None, _ => Close());

        Register("edit.cut", "Cu&t", Keys.Control | Keys.X);
        Register("edit.copy", "&Copy", Keys.Control | Keys.C);
        Register("edit.paste", "&Paste", Keys.Control | Keys.V);

        Register("format.bold", "&Bold", Keys.Control | Keys.B, checkable: true);
        Register("format.italic", "&Italic", Keys.Control | Keys.I, checkable: true);
        Register("format.underline", "&Underline", Keys.Control | Keys.U, checkable: true);
        Register("format.fontcolor", "Font Color", Keys.None);
        Register("format.color.auto", "&Automatic", Keys.None,
            _ => SetStatus("Font color: Automatic"));
        Register("format.color.more", "&More Colors…", Keys.None,
            _ => SetStatus("More Colors…"));
        foreach (string hex in PaletteColors)
        {
            string captured = hex;
            Register("color." + hex.TrimStart('#'), hex, Keys.None,
                _ => SetStatus($"Font color: {captured}"));
        }

        Register("nav.back", "&Back", Keys.None);
        Register("nav.forward", "&Forward", Keys.None);
        Register("nav.refresh", "&Refresh", Keys.None);
        Register("nav.home", "&Home", Keys.None);
        Register("new.doc", "&Document", Keys.None);
        Register("new.template", "&Template", Keys.None);
        Register("nav.hist1", "Getting Started", Keys.None);
        Register("nav.hist2", "Recent Files", Keys.None);
        Register("nav.hist3", "Home", Keys.None);

        RegisterAlign("align.left", "Align &Left");
        RegisterAlign("align.center", "&Center");
        RegisterAlign("align.right", "Align &Right");
        RegisterAlign("align.justify", "&Justify");

        foreach (int size in IconSizeSteps)
            RegisterIconSize($"iconsize.{size}", $"{size} px", size);

        _manager.Commands.GetOrAdd("customize.mode", command =>
        {
            command.Text = "&Customize Toolbars";
            command.IsCheckable = true;
            command.ExecuteHandler = _ => ToggleCustomizeDialog();
        });

        RegisterShapeCommands();
        Register("shape.more", "&More AutoShapes…", Keys.None);

        Register("help.about", "&About…", Keys.None, _ =>
            MessageBox.Show(this,
                "CommandBars consumed as a NuGet package. Its complete showcase is authored through designer definitions.",
                "About", MessageBoxButtons.OK, MessageBoxIcon.Information));
    }

    private CommandBarItem CreateFontCombo()
    {
        var combo = new CommandBarComboBox
        {
            Name = "font.combo",
            Width = 130,
            Image = _svgImages.Get("font"),
            Label = "Font",
        };
        foreach (string font in new[] { "Segoe UI", "Calibri", "Arial", "Times New Roman", "Consolas" })
            combo.Items.Add(font);
        combo.SelectedItem = "Segoe UI";
        combo.SelectedItemChanged += (_, _) => SetStatus($"Font: {combo.SelectedItem}");
        return combo;
    }

    private void RegisterShapeCommands()
    {
        (string Id, string Text)[] shapes =
        {
            ("shape.line", "Line"), ("shape.arrow", "Arrow"),
            ("shape.dblarrow", "Double Arrow"), ("shape.curve", "Curve"),
            ("shape.freeform", "Freeform"),
            ("shape.conn.straight", "Straight Connector"),
            ("shape.conn.elbow", "Elbow Connector"),
            ("shape.conn.curved", "Curved Connector"),
            ("shape.rect", "Rectangle"), ("shape.roundrect", "Rounded Rectangle"),
            ("shape.ellipse", "Ellipse"), ("shape.triangle", "Triangle"),
            ("shape.righttriangle", "Right Triangle"), ("shape.diamond", "Diamond"),
            ("shape.pentagon", "Pentagon"), ("shape.hexagon", "Hexagon"),
            ("shape.cylinder", "Cylinder"), ("shape.cube", "Cube"),
            ("shape.arrow.right", "Right Arrow"), ("shape.arrow.left", "Left Arrow"),
            ("shape.arrow.up", "Up Arrow"), ("shape.arrow.down", "Down Arrow"),
            ("shape.arrow.leftright", "Left-Right Arrow"),
            ("shape.arrow.chevron", "Chevron"),
            ("shape.fc.process", "Process"), ("shape.fc.decision", "Decision"),
            ("shape.fc.terminator", "Terminator"), ("shape.fc.data", "Data"),
            ("shape.fc.document", "Document"), ("shape.fc.connector", "Connector"),
            ("shape.star4", "4-Point Star"), ("shape.star5", "5-Point Star"),
            ("shape.star6", "6-Point Star"), ("shape.explosion", "Explosion"),
            ("shape.ribbon", "Ribbon"),
            ("shape.callout.rect", "Rectangular Callout"),
            ("shape.callout.round", "Rounded Callout"),
            ("shape.callout.oval", "Oval Callout"),
            ("shape.callout.cloud", "Cloud Callout"),
        };

        foreach (var shape in shapes)
        {
            string text = shape.Text;
            Register(shape.Id, text, Keys.None,
                _ => SetStatus($"Insert shape: {text}"));
        }
    }

    private void Register(string id, string text, Keys shortcut,
        Action<CommandExecuteContext>? handler = null, bool checkable = false)
    {
        _manager.Commands.GetOrAdd(id, command =>
        {
            command.Text = text;
            command.Shortcut = shortcut;
            command.IsCheckable = checkable;
            command.ExecuteHandler = handler ?? (context =>
            {
                string state = context.Command.IsCheckable
                    ? context.Command.Checked == CommandCheckState.Checked ? " (on)" : " (off)"
                    : string.Empty;
                SetStatus($"Ran: {context.Command.DisplayText}{state}");
            });
        });
    }

    private void RegisterAlign(string id, string text)
    {
        _manager.Commands.GetOrAdd(id, command =>
        {
            command.Text = text;
            command.IsCheckable = true;
            command.ExecuteHandler = _ =>
            {
                foreach (string alignId in new[] { "align.left", "align.center", "align.right", "align.justify" })
                    _manager.Commands[alignId].Checked = CheckIf(alignId == id);
                SetStatus($"Paragraph: {command.DisplayText}");
            };
        });
    }

    private void RegisterIconSize(string id, string text, int size)
    {
        _manager.Commands.GetOrAdd(id, command =>
        {
            command.Text = text;
            command.IsCheckable = true;
            command.ExecuteHandler = _ => ApplyIconSize(size);
        });
    }

    private IEnumerable<Command> PaletteCommands()
    {
        string[] ids =
        {
            "file.new", "file.open", "file.save",
            "edit.cut", "edit.copy", "edit.paste",
            "format.bold", "format.italic", "format.underline", "format.fontcolor",
            "nav.back", "nav.forward", "nav.refresh", "nav.home",
            "align.left", "align.center", "align.right", "align.justify",
        };
        foreach (string id in ids)
            yield return _manager.Commands[id];
    }

    private void ToggleCustomizeDialog()
    {
        if (_customizeDialog is { IsDisposed: false })
            _customizeDialog.Close();
        else
            OpenCustomizeDialog();
    }

    private void OpenCustomizeDialog()
    {
        if (_customizeDialog is { IsDisposed: false })
        {
            _customizeDialog.Activate();
            return;
        }

        _customizeDialog = new CustomizeDialog(_manager, _dockTop.Renderer, PaletteCommands()) { Owner = this };
        _customizeDialog.FormClosed += (_, _) => _customizeDialog = null;
        _customizeDialog.Show(this);
    }

    private void ApplyIconSize(int size)
    {
        foreach (var bar in _manager.Bars)
            if (bar.BarType == CommandBarType.Toolbar)
                bar.IconSize = size;
        _manager.RefreshLayout();

        foreach (int step in IconSizeSteps)
            _manager.Commands[$"iconsize.{step}"].Checked = CheckIf(step == size);
        SetStatus($"Icon size: {size}px");
    }

    private void UpdateThemeCaption()
    {
        string label = _manager.Themes.FirstOrDefault(t => t.Key == _manager.ActiveThemeKey)?.Text
            .Replace("&", string.Empty) ?? "Custom";
        Text = $"CommandBars Package Demo — {label}";
        SetStatus($"Theme: {label}");
    }

    private void RefreshIconSizeChecks()
    {
        int size = _manager.FindBar("Standard")?.IconSize ?? 24;
        foreach (int step in IconSizeSteps)
            _manager.Commands[$"iconsize.{step}"].Checked = CheckIf(step == size);
    }

    private string LayoutPath => Path.Combine(AppContext.BaseDirectory, "package-demo-commandbars.json");

    private void LoadLayoutFromFile()
    {
        _manager.LoadLayout(LayoutPath);
        RefreshIconSizeChecks();
    }

    protected override void OnFormClosing(FormClosingEventArgs e)
    {
        _manager.SaveLayout(LayoutPath);
        base.OnFormClosing(e);
    }

    protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
        => _manager.ProcessShortcut(keyData) || base.ProcessCmdKey(ref msg, keyData);

    private static CommandCheckState CheckIf(bool value)
        => value ? CommandCheckState.Checked : CommandCheckState.Unchecked;

    private void SetStatus(string text) => _status.Text = text;
}
