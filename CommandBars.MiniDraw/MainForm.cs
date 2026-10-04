using System.Text.Json;
using CommandBars.Controls;
using CommandBars.Imaging;
using CommandBars.Model;
using CommandBars.Rendering;

namespace CommandBars.MiniDraw;

internal sealed class MainForm : Form
{
    internal DrawingDocument Document { get; } = new();
    internal CommandBarManager Manager { get; } = new();
    internal DrawingCanvas Canvas { get; }
    private readonly DrawIcons _icons = new();
    private readonly Label _status = new() { Dock = DockStyle.Bottom, TextAlign = ContentAlignment.MiddleLeft, AutoEllipsis = true };
    private readonly bool _persistLayout;
    private readonly DockHost _top;
    private CustomizeDialog? _customize;
    private string? _path;
    private bool _updating, _initialized;
    private static readonly int[] Widths = { 1, 2, 3, 4, 6, 8, 12, 16 };
    private static readonly int[] IconSizes = { 16, 24, 32, 48 };
    private static readonly int[] ZoomLevels = { 50, 75, 100, 125, 150, 200 };
    private static readonly (string Name, Color Color)[] Colors =
    {
        ("Ink", Color.FromArgb(42, 70, 98)), ("Slate", Color.FromArgb(108, 126, 147)),
        ("White", Color.White), ("Sky", Color.FromArgb(109, 176, 224)),
        ("Teal", Color.FromArgb(78, 174, 159)), ("Mint", Color.FromArgb(166, 221, 190)),
        ("Gold", Color.FromArgb(239, 186, 70)), ("Peach", Color.FromArgb(245, 188, 145)),
        ("Rose", Color.FromArgb(221, 119, 147)), ("Lavender", Color.FromArgb(177, 158, 222)),
        ("Red", Color.FromArgb(203, 76, 76)), ("Blue", Color.FromArgb(60, 111, 191))
    };
    private static string LayoutPath => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "CommandBars", "MiniDraw", "layout.json");

    public MainForm(bool persistLayout = true)
    {
        _persistLayout = persistLayout;
        AutoScaleDimensions = new SizeF(96, 96);
        AutoScaleMode = AutoScaleMode.Dpi;
        Text = "MiniDraw";
        ClientSize = new Size(1140, 820);
        MinimumSize = new Size(620, 440);
        StartPosition = FormStartPosition.CenterScreen;
        Font = SystemFonts.MessageBoxFont;
        Canvas = new DrawingCanvas(Document);
        Manager.Theme = CommandBarTheme.Office2003;
        Manager.ColorScheme = CommandBarColorScheme.Blue;
        BuildCommands();
        BuildBars();
        Manager.CaptureDefaults();
        _top = new DockHost { Edge = DockEdge.Top, Manager = Manager };
        Controls.Add(Canvas);
        Controls.Add(new DockHost { Edge = DockEdge.Right, Manager = Manager });
        Controls.Add(new DockHost { Edge = DockEdge.Left, Manager = Manager });
        Controls.Add(new DockHost { Edge = DockEdge.Bottom, Manager = Manager });
        Controls.Add(_status);
        Controls.Add(_top);
        _status.Height = 28;
        _status.Padding = new Padding(10, 0, 10, 0);
        Canvas.TabIndex = 0;
        Document.Changed += StateChanged;
        Canvas.ToolChanged += StateChanged;
        Canvas.InteractionChanged += StateChanged;
        Canvas.ViewChanged += StateChanged;
        Canvas.OperationFailed += (_, message) => ShowError("Drawing", message);
        Manager.CustomizeRequested += (_, _) => ShowCustomize();
        Manager.CustomizeChanged += (_, _) =>
        {
            Canvas.CancelGesture();
            Canvas.Enabled = !Manager.IsCustomizing;
            RefreshState();
        };
        Manager.LayoutChanged += StateChanged;
        Manager.ThemeChanged += (_, _) => ApplyTheme();
        _initialized = true;
        ApplyTheme();
        RefreshState();
        Shown += (_, _) =>
        {
            if (_persistLayout)
            {
                try { Manager.LoadLayout(LayoutPath); }
                catch (Exception exception) when (IsFileError(exception))
                {
                    Manager.ResetToDefaults();
                    ShowError("Toolbar layout", "The saved layout could not be restored. Default bars are available.\n\n" + exception.Message);
                }
            }
            EnsureFitPageMenu();
            RefreshState();
            Canvas.Focus();
        };
    }

    private Command Register(string id, string text, Action action, string? icon = null, Keys shortcut = Keys.None)
        => Manager.Commands.Register(id, command =>
        {
            command.Text = text;
            command.Image = icon is null ? null : _icons.Get(icon);
            command.Shortcut = shortcut;
            command.ExecuteHandler = _ =>
            {
                Canvas.CancelGesture();
                try { action(); RefreshState(); }
                catch (Exception exception) when (IsFileError(exception)) { ShowError(command.DisplayText, exception.Message); }
            };
        });

    // Earlier MiniDraw layouts contain percentage-only Zoom menus. Add the new
    // entry without replacing any user-arranged menus, commands or toolbar positions.
    internal void EnsureFitPageMenu()
    {
        bool changed = false;
        foreach (var bar in Manager.Bars) Visit(bar);
        if (changed) Manager.RefreshLayout();

        void Visit(CommandBar bar)
        {
            var commands = bar.Items.OfType<CommandBarCommandItem>().Select(item => item.Command.Id).ToArray();
            if (bar.BarType == CommandBarType.Popup && commands.Any(id => id.StartsWith("zoom.", StringComparison.Ordinal) && id != "zoom.fit") &&
                !commands.Contains("zoom.fit"))
            {
                bar.Items.Insert(0, CommandBarCustomizationItem.CreateCommandItem(Manager.Commands["zoom.fit"]));
                bar.Items.Insert(1, new CommandBarSeparator());
                changed = true;
            }
            foreach (var item in bar.Items)
            {
                if (item is CommandBarPopupItem popup) Visit(popup.DropDown);
                else if (item is CommandBarSplitButton split) Visit(split.DropDown);
            }
        }
    }

    private void BuildCommands()
    {
        Register("file.new", "&New", NewDocument, "new", Keys.Control | Keys.N);
        Register("file.open", "&Open...", OpenDocument, "open", Keys.Control | Keys.O);
        Register("file.save", "&Save", () => SaveDocument(false), "save", Keys.Control | Keys.S);
        Register("file.saveAs", "Save &As...", () => SaveDocument(true), shortcut: Keys.Control | Keys.Shift | Keys.S);
        Register("file.export", "Export &PNG...", ExportPng, "export", Keys.Control | Keys.E);
        Register("file.exit", "E&xit", Close);
        Register("edit.undo", "&Undo", Document.Undo, "undo", Keys.Control | Keys.Z);
        Register("edit.redo", "&Redo", Document.Redo, "redo", Keys.Control | Keys.Y);
        Register("edit.duplicate", "D&uplicate", Document.Duplicate, "duplicate", Keys.Control | Keys.D);
        Register("edit.delete", "&Delete", Document.Delete, "delete", Keys.Delete);
        Register("arrange.forward", "Bring &Forward", () => Document.Reorder(1), "forward", Keys.Control | Keys.OemCloseBrackets);
        Register("arrange.backward", "Send &Backward", () => Document.Reorder(-1), "backward", Keys.Control | Keys.OemOpenBrackets);
        foreach (var tool in Enum.GetValues<DrawingTool>())
        {
            var command = Register("tool." + tool, tool == DrawingTool.Select ? "&Select" : "&" + tool,
                () => { Canvas.Tool = tool; Canvas.Focus(); }, tool.ToString().ToLowerInvariant());
            command.RadioCheck = true;
            command.ToolTip = tool == DrawingTool.Select ? "Select and move a shape (V)" : "Draw " + tool.ToString().ToLowerInvariant() + " (" + tool.ToString()[0] + ")";
        }
        foreach (string target in new[] { "fill", "outline" })
        {
            foreach (var color in Colors)
            {
                var command = Register(target + "." + color.Name, color.Name, () => SetColor(target, color.Color));
                command.Image = Swatch(color.Color);
                command.ToolTip = target + ": " + color.Name;
            }
            Register(target + ".custom", "&More colors...", () => ChooseColor(target));
        }
        Register("fill.none", "&No fill", () => SetColor("fill", Color.Transparent)).Image = Swatch(Color.Transparent);
        foreach (int width in Widths)
            Register("width." + width, width + " px", () => SetWidth(width)).RadioCheck = true;
        Register("view.grid", "Show &Grid", () => { Canvas.ShowGrid = !Canvas.ShowGrid; Canvas.Invalidate(); RefreshState(); });
        Register("zoom.fit", "&Fit page", Canvas.ZoomToFit).RadioCheck = true;
        foreach (int zoom in ZoomLevels)
            Register("zoom." + zoom, zoom + "%", () => { Canvas.Zoom = zoom / 100f; RefreshState(); }).RadioCheck = true;
        foreach (int size in IconSizes)
            Register("icons." + size, size + " px", () => Manager.SetIconSize(size)).RadioCheck = true;
        Register("view.customize", "&Customize toolbars...", ShowCustomize);
        Register("view.reset", "&Reset toolbar layout", () => Manager.ResetToDefaults());
        Register("help.controls", "&Drawing controls", () => MessageBox.Show(this,
            "Choose Rectangle, Ellipse or Line, then drag on the page.\n\n" +
            "V: Select    R: Rectangle    E: Ellipse    L: Line\n" +
            "With Select, click a shape and drag to move it.\n" +
            "Arrow keys: move 1 px; Shift+arrow: move 10 px.\n" +
            "Escape: cancel a drag, or return to Select and deselect.\n\n" +
            "Fill, Outline and Line width apply to the selected shape and the next shape you draw.\n" +
            "Use Ctrl+Z / Ctrl+Y for undo / redo.\n\n" +
            "Drag toolbar grips to dock or float. Drag the grip above Shapes or a color palette to tear it off.\n" +
            "Drawings use a 960 x 640 page. The grid is a guide and is omitted from PNG exports.",
            "MiniDraw controls", MessageBoxButtons.OK, MessageBoxIcon.Information));

        // Toggle placements are required for persistent toolbar/palette selection visuals.
        // Handlers/RefreshState enforce radio semantics even when the active choice is clicked again.
        foreach (var command in Manager.Commands)
            command.IsCheckable = command.Id.StartsWith("tool.") || command.Id.StartsWith("width.") ||
                command.Id.StartsWith("zoom.") || command.Id.StartsWith("icons.") ||
                command.Id is "view.grid" or "view.customize" ||
                ((command.Id.StartsWith("fill.") || command.Id.StartsWith("outline.")) && !command.Id.EndsWith(".custom"));
    }

    private void BuildBars()
    {
        var menu = Manager.AddBar("Menu", CommandBarType.MenuBar);
        AddCommands(menu.Items.AddPopup("&File").DropDown, "file.new", "file.open", "file.save", "file.saveAs", "-", "file.export", "-", "file.exit");
        AddCommands(menu.Items.AddPopup("&Edit").DropDown, "edit.undo", "edit.redo", "-", "edit.duplicate", "edit.delete");
        menu.Items.Add(CreateShapes());
        var format = menu.Items.AddPopup("F&ormat").DropDown;
        format.Items.Add(CreateColors("fill")); format.Items.Add(CreateColors("outline")); format.Items.Add(CreateWidths());
        AddCommands(menu.Items.AddPopup("&Arrange").DropDown, "arrange.forward", "arrange.backward");
        var view = menu.Items.AddPopup("&View").DropDown;
        view.Items.AddPopup("&Theme").ThemeList = true;
        view.Items.AddPopup("&Toolbars").ToolbarList = true;
        var sizes = view.Items.AddPopup("&Icon size").DropDown;
        AddCommands(sizes, IconSizes.Select(size => "icons." + size).ToArray());
        var zoom = view.Items.AddPopup("&Zoom").DropDown;
        AddCommands(zoom, "zoom.fit", "-");
        AddCommands(zoom, ZoomLevels.Select(value => "zoom." + value).ToArray());
        AddCommands(view, "view.grid", "-", "view.customize", "view.reset");
        AddCommands(menu.Items.AddPopup("&Help").DropDown, "help.controls");

        var standard = Manager.AddBar("Standard", CommandBarType.Toolbar);
        AddTools(standard, "file.new", "file.open", "file.save", "file.export", "-", "edit.undo", "edit.redo", "-", "edit.duplicate", "edit.delete");
        var formatBar = Manager.AddBar("Formatting", CommandBarType.Toolbar);
        formatBar.Row = 1;
        formatBar.Items.Add(CreateColors("fill")); formatBar.Items.Add(CreateColors("outline")); formatBar.Items.Add(CreateWidths());
        formatBar.Items.AddSeparator();
        AddTools(formatBar, "arrange.forward", "arrange.backward");
        var shapes = Manager.AddBar("Drawing", CommandBarType.Toolbar);
        shapes.Dock = DockState.Left;
        AddTools(shapes, "tool.Select", "-", "tool.Rectangle", "tool.Ellipse", "tool.Line");
        foreach (var bar in Manager.Bars) bar.IconSize = 24;
        Manager.RegisterCustomizationItem(new CommandBarCustomizationItem("shapes.palette", "Shapes", _icons.Get("rectangle"), CreateShapes));
        foreach (string target in new[] { "fill", "outline" })
            Manager.RegisterCustomizationItem(new CommandBarCustomizationItem(target + ".palette", target == "fill" ? "Fill color" : "Outline color", null, () => CreateColors(target)));
        Manager.RegisterCustomizationItem(new CommandBarCustomizationItem("width.palette", "Line width", _icons.Get("line"), CreateWidths));
    }

    private void AddCommands(CommandBar bar, params string[] ids)
    {
        foreach (string id in ids)
            if (id == "-") bar.Items.AddSeparator();
            else bar.Items.Add(CommandBarCustomizationItem.CreateCommandItem(Manager.Commands[id]));
    }
    private void AddTools(CommandBar bar, params string[] ids)
    {
        foreach (string id in ids)
            if (id == "-") bar.Items.AddSeparator();
            else bar.Items.Add(CommandBarCustomizationItem.CreateCommandItem(Manager.Commands[id], CommandItemDisplayStyle.ImageOnly));
    }
    private CommandBarPopupItem CreateShapes()
    {
        var popup = new CommandBarPopupItem("&Shapes") { Name = "shapes.palette", Image = _icons.Get("rectangle") };
        popup.DropDown.Text = "Shapes";
        popup.DropDown.AllowTearOff = true;
        popup.DropDown.TearOffKey = "minidraw.shapes";
        AddCommands(popup.DropDown, "tool.Select", "-", "tool.Rectangle", "tool.Ellipse", "tool.Line");
        return popup;
    }
    private CommandBarPopupItem CreateColors(string target)
    {
        var popup = new CommandBarPopupItem(target == "fill" ? "&Fill" : "&Outline") { Name = target + ".palette" };
        popup.DropDown.Text = target == "fill" ? "Fill color" : "Outline color";
        popup.DropDown.AllowTearOff = true;
        popup.DropDown.TearOffKey = "minidraw." + target;
        popup.DropDown.PaletteColumns = 6;
        if (target == "fill")
        {
            popup.DropDown.Items.Add(CommandBarCustomizationItem.CreateCommandItem(Manager.Commands["fill.none"], CommandItemDisplayStyle.TextOnly));
            popup.DropDown.Items.AddSeparator();
        }
        foreach (var color in Colors)
            popup.DropDown.Items.Add(CommandBarCustomizationItem.CreateCommandItem(Manager.Commands[target + "." + color.Name], CommandItemDisplayStyle.ImageOnly));
        AddCommands(popup.DropDown, "-", target + ".custom");
        return popup;
    }
    private CommandBarPopupItem CreateWidths()
    {
        var popup = new CommandBarPopupItem("Line &width") { Name = "width.palette", Image = _icons.Get("line") };
        AddCommands(popup.DropDown, Widths.Select(width => "width." + width).ToArray());
        return popup;
    }
    private IImageSource Swatch(Color color) => _icons.Get("color:" + color.ToArgb().ToString(System.Globalization.CultureInfo.InvariantCulture));

    private void SetColor(string target, Color color)
    {
        if (target == "fill") Canvas.FillColor = color; else Canvas.OutlineColor = color;
        if (Document.Selected is { } shape)
            Document.ReplaceSelected(target == "fill" ? shape with { FillArgb = color.ToArgb() } : shape with { StrokeArgb = color.ToArgb() },
                target == "fill" ? "Fill color" : "Outline color");
        RefreshState();
    }
    private void ChooseColor(string target)
    {
        using var dialog = new ColorDialog { FullOpen = true, Color = target == "fill" ? Canvas.FillColor : Canvas.OutlineColor };
        if (dialog.ShowDialog(this) == DialogResult.OK) SetColor(target, dialog.Color);
    }
    private void SetWidth(float width)
    {
        Canvas.LineWidth = width;
        if (Document.Selected is { } shape) Document.ReplaceSelected(shape with { StrokeWidth = width }, "Line width");
        RefreshState();
    }
    private void StateChanged(object? sender, EventArgs e) => RefreshState();
    private void RefreshState()
    {
        if (!_initialized || _updating) return;
        _updating = true;
        try
        {
            var selected = Document.Selected;
            if (selected is not null)
            {
                Canvas.FillColor = Color.FromArgb(selected.FillArgb);
                Canvas.OutlineColor = Color.FromArgb(selected.StrokeArgb);
                Canvas.LineWidth = selected.StrokeWidth;
            }
            bool editable = !Canvas.IsDragging && !Manager.IsCustomizing;
            Manager.Commands["edit.undo"].Enabled = editable && Document.CanUndo;
            Manager.Commands["edit.redo"].Enabled = editable && Document.CanRedo;
            Manager.Commands["edit.undo"].Text = "&Undo" + (Document.CanUndo ? " " + Document.UndoAction : "");
            Manager.Commands["edit.redo"].Text = "&Redo" + (Document.CanRedo ? " " + Document.RedoAction : "");
            Manager.Commands["edit.delete"].Enabled = editable && selected is not null;
            Manager.Commands["edit.duplicate"].Enabled = editable && selected is not null && Document.Shapes.Count < DrawingDocument.MaximumShapes;
            Manager.Commands["arrange.forward"].Enabled = editable && Document.CanReorder(1);
            Manager.Commands["arrange.backward"].Enabled = editable && Document.CanReorder(-1);
            foreach (var tool in Enum.GetValues<DrawingTool>()) Check("tool." + tool, Canvas.Tool == tool);
            foreach (var color in Colors)
            {
                Check("fill." + color.Name, Canvas.FillColor.ToArgb() == color.Color.ToArgb());
                Check("outline." + color.Name, Canvas.OutlineColor.ToArgb() == color.Color.ToArgb());
            }
            Check("fill.none", Canvas.FillColor.A == 0);
            foreach (var command in Manager.Commands.Where(command => command.Id.StartsWith("fill.")))
                command.Enabled = editable && selected?.Kind != ShapeKind.Line;
            foreach (int width in Widths) Check("width." + width, Canvas.LineWidth == width);
            Check("zoom.fit", Canvas.FitPage);
            foreach (int zoom in ZoomLevels) Check("zoom." + zoom, !Canvas.FitPage && Math.Abs(Canvas.Zoom * 100 - zoom) < .1f);
            foreach (int size in IconSizes) Check("icons." + size, Manager.FindBar("Standard")?.IconSize == size);
            Check("view.grid", Canvas.ShowGrid);
            Check("view.customize", Manager.IsCustomizing);
            Text = $"{(_path is null ? "Untitled" : Path.GetFileName(_path))}{(Document.IsDirty ? " *" : "")} — MiniDraw";
            string details = selected is null ? "Choose a shape tool and drag to draw" :
                $"{selected.Kind} selected  |  {selected.Bounds.Width:0} × {selected.Bounds.Height:0} px  |  Outline {selected.StrokeWidth:0.#} px";
            _status.Text = Manager.IsCustomizing ? "Customize mode — drag commands or toolbar grips; close Customize to draw." :
                $"{Canvas.Tool}  |  {details}  |  {Document.Shapes.Count} shapes  |  {(Canvas.FitPage ? "Fit page · " : "")}{Canvas.Zoom * 100:0}%";
        }
        finally { _updating = false; }
    }
    private void Check(string id, bool value) => Manager.Commands[id].Checked = value ? CommandCheckState.Checked : CommandCheckState.Unchecked;

    private void ApplyTheme()
    {
        _status.BackColor = _top.Renderer.DialogColors.Surface;
        _status.ForeColor = _top.Renderer.DialogColors.Text;
        if (_icons.UseDarkPalette(_top.Renderer.DialogColors.IsDark)) Manager.RefreshLayout();
        RefreshState();
    }
    private void ShowCustomize()
    {
        if (_customize is { IsDisposed: false }) { _customize.Activate(); return; }
        _customize = new CustomizeDialog(Manager, _top.Renderer);
        _customize.FormClosed += (_, _) => { _customize = null; Canvas.Focus(); RefreshState(); };
        _customize.Show(this);
    }
    private bool ConfirmDiscard()
    {
        if (!Document.IsDirty) return true;
        return MessageBox.Show(this, "Save changes to this drawing?", "MiniDraw", MessageBoxButtons.YesNoCancel,
            MessageBoxIcon.Question) switch { DialogResult.Yes => SaveDocument(false), DialogResult.No => true, _ => false };
    }
    private void NewDocument()
    {
        if (!ConfirmDiscard()) return;
        _path = null;
        Document.New();
        Canvas.Tool = DrawingTool.Select;
    }
    private void OpenDocument()
    {
        using var dialog = new OpenFileDialog { Filter = "MiniDraw drawing (*.minidraw)|*.minidraw", CheckFileExists = true };
        if (dialog.ShowDialog(this) != DialogResult.OK || !ConfirmDiscard()) return;
        Document.Load(dialog.FileName);
        _path = dialog.FileName;
        Canvas.Tool = DrawingTool.Select;
        RefreshState();
    }
    private bool SaveDocument(bool saveAs)
    {
        string? path = _path;
        if (saveAs || path is null)
        {
            using var dialog = new SaveFileDialog { Filter = "MiniDraw drawing (*.minidraw)|*.minidraw", DefaultExt = "minidraw",
                FileName = path is null ? "Drawing.minidraw" : Path.GetFileName(path), AddExtension = true, OverwritePrompt = true };
            if (dialog.ShowDialog(this) != DialogResult.OK) return false;
            path = dialog.FileName;
        }
        try { Document.Save(path); _path = path; RefreshState(); return true; }
        catch (Exception exception) when (IsFileError(exception)) { ShowError("Save drawing", exception.Message); return false; }
    }
    private void ExportPng()
    {
        using var dialog = new SaveFileDialog { Filter = "PNG image (*.png)|*.png", DefaultExt = "png", AddExtension = true,
            FileName = _path is null ? "Drawing.png" : Path.GetFileNameWithoutExtension(_path) + ".png", OverwritePrompt = true };
        if (dialog.ShowDialog(this) == DialogResult.OK) DrawingPainter.ExportPng(Document, dialog.FileName);
    }
    private static bool IsFileError(Exception exception) => exception is IOException or UnauthorizedAccessException or JsonException or NotSupportedException or ArgumentException;
    private void ShowError(string operation, string message) => MessageBox.Show(this, message, "MiniDraw — " + operation,
        MessageBoxButtons.OK, MessageBoxIcon.Warning);

    protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
    {
        if (!Manager.IsCustomizing && !Canvas.IsDragging &&
            (keyData != Keys.Delete || Canvas.ContainsFocus) && Manager.ProcessShortcut(keyData)) return true;
        return base.ProcessCmdKey(ref msg, keyData);
    }
    protected override void OnFormClosing(FormClosingEventArgs e)
    {
        Canvas.CancelGesture();
        if (_persistLayout && !ConfirmDiscard()) e.Cancel = true;
        base.OnFormClosing(e);
        if (!e.Cancel && _persistLayout)
        {
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(LayoutPath)!);
                Manager.SaveLayout(LayoutPath);
            }
            catch (Exception exception) when (IsFileError(exception)) { ShowError("Save toolbar layout", exception.Message); }
        }
    }
    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _initialized = false;
            Document.Changed -= StateChanged;
            Canvas.ToolChanged -= StateChanged;
            Canvas.InteractionChanged -= StateChanged;
            Canvas.ViewChanged -= StateChanged;
            _customize?.Dispose();
            foreach (var window in OwnedForms) window.Dispose();
        }
        base.Dispose(disposing);
        if (disposing) { Manager.Dispose(); _icons.Dispose(); }
    }
}
