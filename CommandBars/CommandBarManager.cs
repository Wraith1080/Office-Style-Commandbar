using System.ComponentModel;
using System.Drawing;
using System.IO;
using System.Text;
using System.Text.Json;
using CommandBars.Controls;
using CommandBars.Model;
using CommandBars.Persistence;
using CommandBars.Rendering;

namespace CommandBars;

/// <summary>
/// The top-level entry point: owns the command registry and all bars, and is
/// where later phases attach the renderer, dock host, and persistence.
///
/// Phase 1 implements the model surface only. Members intended for later
/// phases are marked below so the shape is visible without faking behavior.
/// </summary>
[ToolboxItem(true)]
[DesignerCategory("Component")]
// String reference to the out-of-process design assembly (shipped in the NuGet
// package's Design/WinForms/Server folder). No compile dependency: with a plain
// project reference the designer simply falls back to the default, exactly as
// before. The old in-process CommandBars.Design.CommandBarManagerDesigner is
// dead code kept for reference (VS never loads design types from the control
// assembly itself).
[Designer("CommandBars.Designer.Server.CommandBarManagerDesigner, CommandBars.Designer.Server")]
public class CommandBarManager : Component
{
    public CommandBarManager()
    {
        Commands = new CommandRegistry();
        Bars = new CommandBarCollection(this);
    }

    /// <summary>The application's command registry.</summary>
    [Browsable(false)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public CommandRegistry Commands { get; }

    /// <summary>All bars managed here (menu bar, toolbars, etc.).</summary>
    [Browsable(false)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public CommandBarCollection Bars { get; }

    private readonly List<Design.BarDefinition> _barDefinitions = new();

    /// <summary>
    /// Design-time descriptions of the bars and their items. This is the
    /// designer-editable, code-serialized surface: edit it in the VS Properties
    /// grid (or the manager's "Edit Toolbars…" verb), then realize it into live
    /// bars at run time with <see cref="BuildFromDefinitions"/>. Editing here is
    /// independent of the runtime <see cref="Bars"/> collection.
    /// </summary>
    [Category("CommandBars")]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Content)]
    [System.ComponentModel.Editor(
        typeof(Design.BarDefinitionCollectionEditor),
        typeof(System.Drawing.Design.UITypeEditor))]
    public List<Design.BarDefinition> BarDefinitions => _barDefinitions;

    private Imaging.SvgImageList? _images;

    /// <summary>
    /// The SVG icon set that item image keys resolve against. Drop a
    /// <see cref="Imaging.SvgImageList"/> on the form and assign it here; then set
    /// each item's <c>ImageKey</c> to an entry's key.
    /// </summary>
    [Category("CommandBars")]
    [DefaultValue(null)]
    public Imaging.SvgImageList? Images
    {
        get => _images;
        set => _images = value;
    }

    /// <summary>
    /// Realizes every entry in <see cref="BarDefinitions"/> into a live
    /// <see cref="CommandBar"/>, resolving each item's command id against
    /// <see cref="Commands"/> (registering a synthesized command when the id is
    /// named but not yet present). Existing bars are cleared first. Call this
    /// once at startup after registering your commands.
    /// </summary>
    public void BuildFromDefinitions()
    {
        Bars.Clear();
        foreach (var def in _barDefinitions)
        {
            var bar = def.Build(Commands, _images);
            Bars.Add(bar);
        }
        RefreshLayout();
    }

    // Signature of the last definition set realized for the design preview.
    private string _designSig = "\0";

    /// <summary>Current design-preview signature (advances when definitions change).</summary>
    internal string DesignSig => _designSig;

    /// <summary>
    /// Design-time only: realizes <see cref="BarDefinitions"/> into live
    /// <see cref="Bars"/> so hosts can render a real preview, rebuilding only
    /// when the definition set actually changed. Defensive — a malformed or
    /// duplicate-named definition is skipped rather than throwing into the
    /// Visual Studio designer. Never call at run time (use
    /// <see cref="BuildFromDefinitions"/>).
    /// </summary>
    internal bool EnsureDesignBars()
    {
        string sig = ComputeDesignSignature();
        if (sig == _designSig)
            return false;
        _designSig = sig;

        Bars.Clear();
        var used = new HashSet<string>(StringComparer.Ordinal);
        for (int i = 0; i < _barDefinitions.Count; i++)
        {
            var def = _barDefinitions[i];
            try
            {
                string? nameOverride = string.IsNullOrWhiteSpace(def.Name) ? $"__preview{i}" : null;
                var bar = def.Build(Commands, _images, nameOverride);
                if (used.Add(bar.Name))
                    Bars.Add(bar);
            }
            catch
            {
                // Ignore a bad definition so the designer stays alive.
            }
        }
        return true;
    }

    /// <summary>
    /// Design-time only: re-realizes the definitions and rebuilds every hosted
    /// band's preview when the definitions (or icons) actually changed. Called by
    /// the manager's designer whenever a component on the surface changes, so
    /// editing a property like a toolbar's IconSize refreshes the preview
    /// immediately instead of only after reopening the designer.
    /// </summary>
    internal void RefreshDesignPreview()
    {
        EnsureDesignBars();
        foreach (var host in _hosts.ToArray())
            host.RefreshDesignPreview();
    }

    private string ComputeDesignSignature()
    {
        var sb = new StringBuilder();
        if (_images is not null)
        {
            foreach (var img in _images.Images)
                sb.Append(img.Key).Append('=').Append(img.Svg?.Length ?? 0).Append('~');
            sb.Append('#');
        }
        foreach (var d in _barDefinitions)
        {
            sb.Append(d.BarType).Append('|').Append(d.Name).Append('|')
              .Append(d.Dock).Append('|').Append(d.Visible).Append('|')
              .Append(d.IconSize).Append('|').Append(d.Items.Count).Append(';');
            AppendItemSignature(sb, d.Items);
        }
        return sb.ToString();
    }

    private static void AppendItemSignature(StringBuilder sb, List<Design.ItemDefinition> items)
    {
        foreach (var it in items)
        {
            sb.Append(it.Kind).Append(',').Append(it.Text).Append(',')
              .Append(it.CommandId).Append(',').Append(it.ImageKey).Append(',')
              .Append(it.ImagePath).Append(',')
              .Append(it.DisplayStyle).Append(',').Append(it.BeginGroup).Append('/');
            if (it.Items.Count > 0)
                AppendItemSignature(sb, it.Items);
        }
    }

    /// <summary>
    /// True while the interactive Customize session is active. In this mode the
    /// hosted bars become editable: items can be dragged to reorder, moved
    /// between toolbars, or removed by dragging them off.
    /// </summary>
    [Browsable(false)]
    public bool IsCustomizing { get; private set; }

    /// <summary>Raised when bars are added or removed.</summary>
    public event EventHandler? LayoutChanged;

    /// <summary>Raised when Customize mode is entered or exited.</summary>
    public event EventHandler? CustomizeChanged;

    /// <summary>
    /// Raised when the user picks "Customize…" from a toolbar's chevron menu.
    /// The host app handles this to show its Customize dialog (the control can't
    /// build the dialog itself, since the dialog is application-specific).
    /// </summary>
    public event EventHandler? CustomizeRequested;

    /// <summary>Raises <see cref="CustomizeRequested"/>.</summary>
    public void RequestCustomize() => CustomizeRequested?.Invoke(this, EventArgs.Empty);

    /// <summary>
    /// Enters Customize mode. While active, clicking a toolbar item starts a
    /// drag instead of invoking it: drop it elsewhere on a toolbar to move it,
    /// or off every bar to remove it.
    /// </summary>
    public void BeginCustomize()
    {
        if (IsCustomizing)
            return;
        IsCustomizing = true;
        CustomizeChanged?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>Exits Customize mode and returns bars to normal interaction.</summary>
    public void EndCustomize()
    {
        if (!IsCustomizing)
            return;
        IsCustomizing = false;
        CustomizeChanged?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>Creates a bar, adds it, and returns it (fluent construction).</summary>
    public CommandBar AddBar(string name, CommandBarType barType)
    {
        var bar = new CommandBar(name, barType);
        Bars.Add(bar);
        OnLayoutChanged();
        return bar;
    }

    /// <summary>Finds a bar by name, or null.</summary>
    public CommandBar? FindBar(string name) => Bars[name];

    /// <summary>Removes a bar by name. Returns false if it was not found.</summary>
    public bool RemoveBar(string name)
    {
        var bar = Bars[name];
        if (bar is null)
            return false;
        Bars.Remove(bar);
        OnLayoutChanged();
        return true;
    }

    protected virtual void OnLayoutChanged() => LayoutChanged?.Invoke(this, EventArgs.Empty);

    /// <summary>Raises <see cref="LayoutChanged"/> so hosts re-lay out the bars.</summary>
    public void RefreshLayout() => OnLayoutChanged();

    // --- App settings (persisted alongside the layout) --------------------

    /// <summary>Whether hovering a toolbar item shows a tooltip (ScreenTip). Persisted with the layout.</summary>
    [Browsable(false)]
    public bool ShowToolTips { get; set; } = true;

    private readonly Dictionary<string, string> _settings = new();

    /// <summary>Stores an app-level setting (e.g. the selected theme) that is saved with the layout.</summary>
    public void SetSetting(string key, string value) => _settings[key] = value;

    /// <summary>Reads a persisted app-level setting, or null if unset.</summary>
    public string? GetSetting(string key) => _settings.TryGetValue(key, out var v) ? v : null;

    // --- Dock host coordination -------------------------------------------
    // Several DockHosts (one per edge) share this manager. Keeping the list and
    // the active drag here lets a bar dragged off one band be previewed and
    // dropped onto any other band, and lets one band act as the floating-window
    // owner regardless of which band a bar was undocked from.

    private readonly List<DockHost> _hosts = new();

    /// <summary>The dock hosts currently bound to this manager (registration order).</summary>
    internal IReadOnlyList<DockHost> Hosts => _hosts;

    /// <summary>The drag session in progress, or null.</summary>
    internal DockDragSession? ActiveDrag { get; private set; }

    internal void RegisterHost(DockHost host)
    {
        if (!_hosts.Contains(host))
            _hosts.Add(host);
        host.Renderer = _renderer; // adopt the manager's current theme
    }

    private CommandBarTheme _theme = CommandBarTheme.Office2003;
    private CommandBarRenderer _renderer = ThemeRenderer.Create(CommandBarTheme.Office2003);

    /// <summary>
    /// The visual theme applied to every hosted bar. Settable from the Properties
    /// window at design time (the preview re-skins) and at run time. Changing it
    /// re-skins all registered <see cref="DockHost"/> bands.
    /// </summary>
    [Category("CommandBars")]
    [DefaultValue(CommandBarTheme.Office2003)]
    public CommandBarTheme Theme
    {
        get => _theme;
        set
        {
            if (_theme == value)
                return;
            _theme = value;
            _renderer = ThemeRenderer.Create(value);
            ApplyThemeToHosts();
        }
    }

    /// <summary>The renderer for the current <see cref="Theme"/> (shared by the hosts).</summary>
    [Browsable(false)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public CommandBarRenderer Renderer => _renderer;

    /// <summary>Raised after <see cref="Theme"/> changes, so hosts/dialogs can re-theme.</summary>
    public event EventHandler? ThemeChanged;

    private void ApplyThemeToHosts()
    {
        foreach (var host in _hosts.ToArray())
        {
            host.Renderer = _renderer;
            host.Invalidate(true);
        }
        ThemeChanged?.Invoke(this, EventArgs.Empty);
    }

    internal void UnregisterHost(DockHost host)
    {
        _hosts.Remove(host);
        if (ActiveDrag is not null && ReferenceEquals(ActiveDrag.Origin, host))
            ActiveDrag = null;
    }

    internal void BeginDrag(CommandBar bar, Size size, Point grab, DockHost origin)
        => ActiveDrag = new DockDragSession(bar, size, grab, origin);

    internal void EndDrag() => ActiveDrag = null;

    /// <summary>
    /// The first host whose dock zone contains the screen point, or null. Used
    /// during a drag to decide which band would catch the bar.
    /// </summary>
    internal DockHost? HitDockZone(Point screen)
    {
        foreach (var host in _hosts)
            if (host.ContainsDockZone(screen))
                return host;
        return null;
    }

    /// <summary>The host that owns floating windows (the first Top-edge band).</summary>
    internal DockHost? FloatOwner
    {
        get
        {
            foreach (var host in _hosts)
                if (host.Edge == DockEdge.Top)
                    return host;
            return _hosts.Count > 0 ? _hosts[0] : null;
        }
    }

    /// <summary>
    /// The toolbar control whose insertion zone contains the screen point, with
    /// the insertion index and a screen-space marker rectangle. Shared by
    /// in-place item drags and the Commands palette so both drop identically.
    /// </summary>
    internal CommandBarControl? FindDropTarget(Point screen, out int index, out Rectangle markerScreen)
    {
        index = 0;
        markerScreen = Rectangle.Empty;
        foreach (var host in _hosts)
            foreach (var control in host.BarControls)
                if (control.TryComputeInsertion(screen, out index, out markerScreen))
                    return control;
        return null;
    }

    /// <summary>Shows the customize drop marker on the shared overlay.</summary>
    internal void ShowDropMarker(Rectangle screen) => FloatOwner?.ShowItemMarker(screen);

    /// <summary>Hides the customize drop marker.</summary>
    internal void HideDropMarker() => FloatOwner?.HideItemMarker();

    // --- Persistence (caller-controlled location) -------------------------

    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };

    /// <summary>Writes the current customizations to a stream as JSON.</summary>
    public void SaveLayout(Stream stream)
    {
        ArgumentNullException.ThrowIfNull(stream);
        JsonSerializer.Serialize(stream, CaptureState(), JsonOptions);
    }

    /// <summary>Writes the current customizations to a file.</summary>
    public void SaveLayout(string path)
    {
        using var stream = File.Create(path);
        SaveLayout(stream);
    }

    /// <summary>Applies customizations from a JSON stream onto the current bars.</summary>
    public void LoadLayout(Stream stream)
    {
        ArgumentNullException.ThrowIfNull(stream);
        var state = JsonSerializer.Deserialize<LayoutState>(stream, JsonOptions);
        if (state is not null)
            ApplyState(state);
    }

    /// <summary>Applies customizations from a JSON file if it exists.</summary>
    public void LoadLayout(string path)
    {
        if (!File.Exists(path))
            return;
        using var stream = File.OpenRead(path);
        LoadLayout(stream);
    }

    private LayoutState CaptureState()
    {
        var state = new LayoutState { Version = 2, ShowToolTips = ShowToolTips, Settings = new Dictionary<string, string>(_settings) };
        foreach (var bar in Bars)
            state.Bars.Add(CaptureBar(bar));
        return state;
    }

    private static BarState CaptureBar(CommandBar bar) => new()
    {
        Name = bar.Name,
        Text = bar.Text,
        BarType = bar.BarType.ToString(),
        Dock = bar.Dock.ToString(),
        Visible = bar.Visible,
        IconSize = bar.IconSize,
        Row = bar.Row,
        Offset = bar.Offset,
        FloatX = bar.FloatingBounds.X,
        FloatY = bar.FloatingBounds.Y,
        AllowFloat = bar.AllowFloat,
        AllowCustomize = bar.AllowCustomize,
        Locked = bar.Locked,
        Items = SnapshotItems(bar.Items),
    };

    private void ApplyState(LayoutState state)
    {
        // v1 files were a visibility-only overlay with a different shape; skip
        // them rather than misinterpret. The next Save writes the v2 structure.
        if (state.Version < 2)
            return;

        // Restore app settings (theme, etc.) even if there are no bars.
        ShowToolTips = state.ShowToolTips;
        _settings.Clear();
        foreach (var kv in state.Settings)
            _settings[kv.Key] = kv.Value;

        if (state.Bars.Count == 0)
        {
            OnLayoutChanged();
            return;
        }

        // Rebuild the whole bar set from the saved structure so add/remove/
        // reorder, new/renamed/deleted toolbars, and menu edits all round-trip.
        Bars.Clear();
        foreach (var bs in state.Bars)
        {
            if (!Enum.TryParse<CommandBarType>(bs.BarType, out var barType))
                barType = CommandBarType.Toolbar;

            var bar = new CommandBar(bs.Name, barType)
            {
                Text = string.IsNullOrEmpty(bs.Text) ? bs.Name : bs.Text,
            };

            var dock = DockState.Top;
            Enum.TryParse(bs.Dock, out dock);
            bar.Dock = dock;
            bar.Visible = bs.Visible;
            if (bs.IconSize > 0)
                bar.IconSize = bs.IconSize;
            bar.Row = bs.Row;
            bar.Offset = bs.Offset;
            bar.AllowFloat = bs.AllowFloat;
            bar.AllowCustomize = bs.AllowCustomize;
            bar.Locked = bs.Locked;
            if (dock == DockState.Floating)
                bar.FloatingBounds = new Rectangle(bs.FloatX, bs.FloatY, 0, 0);

            RebuildItems(bar.Items, bs.Items);
            Bars.Add(bar);
        }
        OnLayoutChanged();
    }

    /// <summary>
    /// Finds an enabled command whose shortcut matches and performs it. Host
    /// forms call this from <c>ProcessCmdKey</c> so Ctrl+S and friends work
    /// regardless of focus. Returns true if a command handled the key.
    /// </summary>
    public bool ProcessShortcut(System.Windows.Forms.Keys keyData)
    {
        foreach (var command in Commands)
        {
            if (command.Shortcut != System.Windows.Forms.Keys.None &&
                command.Shortcut == keyData &&
                command.Enabled)
            {
                return command.Perform();
            }
        }
        return false;
    }

    // --- Default structure snapshot (for Customize "Reset") ---------------
    // The factory layout captured once at startup, in the same ItemState shape
    // used for persistence. "Reset" rebuilds a bar (or a single menu) from it.

    private readonly Dictionary<string, List<ItemState>> _defaults = new();
    private LayoutState? _defaultLayout;

    /// <summary>
    /// Records the current bars as the "factory default" that Customize's Reset
    /// (per bar/menu) and <see cref="ResetToDefaults"/> (whole layout) restore.
    /// Call once after building the default layout and before loading any saved
    /// customizations.
    /// </summary>
    public void CaptureDefaults()
    {
        _defaults.Clear();
        foreach (var bar in Bars)
            _defaults[bar.Name] = SnapshotItems(bar.Items);
        _defaultLayout = CaptureState();
    }

    /// <summary>
    /// Restores the entire layout — every bar and item, including bars the user
    /// created or deleted — to the captured factory defaults. Returns false if
    /// <see cref="CaptureDefaults"/> was never called.
    /// </summary>
    public bool ResetToDefaults()
    {
        if (_defaultLayout is null)
            return false;
        // A layout reset should not change app settings (e.g. the theme).
        var keep = new Dictionary<string, string>(_settings);
        ApplyState(_defaultLayout);
        _settings.Clear();
        foreach (var kv in keep)
            _settings[kv.Key] = kv.Value;
        return true;
    }

    /// <summary>Restores a bar's items to the captured defaults. Returns false if none were captured.</summary>
    public bool ResetBar(CommandBar bar)
    {
        ArgumentNullException.ThrowIfNull(bar);
        if (!_defaults.TryGetValue(bar.Name, out var snapshot))
            return false;
        bar.Items.Clear();
        RebuildItems(bar.Items, snapshot);
        OnLayoutChanged();
        return true;
    }

    /// <summary>Restores a single menu's dropdown to the captured defaults. Returns false if not found.</summary>
    public bool ResetMenu(CommandBarPopupItem popup)
    {
        ArgumentNullException.ThrowIfNull(popup);
        var snapshot = FindByKey(popup.DropDown.Name);
        if (snapshot is null)
            return false;
        popup.DropDown.Items.Clear();
        RebuildItems(popup.DropDown.Items, snapshot.Children);
        OnLayoutChanged();
        return true;
    }

    // --- Shared structure (de)serialization -------------------------------

    private static List<ItemState> SnapshotItems(CommandBarItemCollection items)
    {
        var list = new List<ItemState>();
        foreach (var item in items)
        {
            var s = new ItemState { Kind = item.Kind.ToString(), BeginGroup = item.BeginGroup, Visible = item.Visible };
            switch (item)
            {
                case CommandBarPopupItem p:
                    s.Text = p.Text;
                    s.Key = p.DropDown.Name;
                    s.Children = SnapshotItems(p.DropDown.Items);
                    break;
                case CommandBarSplitButton sp:
                    s.CommandId = sp.Command.Id;
                    s.DisplayStyle = sp.DisplayStyle.ToString();
                    s.Key = sp.DropDown.Name;
                    s.Children = SnapshotItems(sp.DropDown.Items);
                    break;
                case CommandBarToggleButton t:
                    s.CommandId = t.Command.Id;
                    s.DisplayStyle = t.DisplayStyle.ToString();
                    break;
                case CommandBarCommandItem c:
                    s.CommandId = c.Command.Id;
                    s.DisplayStyle = c.DisplayStyle.ToString();
                    break;
                case CommandBarLabel l:
                    s.Text = l.Text;
                    break;
            }
            list.Add(s);
        }
        return list;
    }

    private void RebuildItems(CommandBarItemCollection into, List<ItemState> items)
    {
        foreach (var s in items)
        {
            var item = BuildItem(s);
            if (item is null)
                continue;
            item.BeginGroup = s.BeginGroup;
            item.Visible = s.Visible;
            into.Add(item);
        }
    }

    private CommandBarItem? BuildItem(ItemState s)
    {
        var display = Enum.TryParse<CommandItemDisplayStyle>(s.DisplayStyle, out var d)
            ? d
            : CommandItemDisplayStyle.ImageAndText;
        if (!Enum.TryParse<CommandItemKind>(s.Kind, out var kind))
            kind = CommandItemKind.Button;

        switch (kind)
        {
            case CommandItemKind.Separator:
                return new CommandBarSeparator();
            case CommandItemKind.Label:
                return new CommandBarLabel(s.Text ?? string.Empty);
            case CommandItemKind.ComboBox:
                return new CommandBarComboBox();
            case CommandItemKind.Popup:
            {
                var popup = new CommandBarPopupItem(s.Text ?? string.Empty);
                RebuildItems(popup.DropDown.Items, s.Children);
                return popup;
            }
            case CommandItemKind.ToggleButton:
                return s.CommandId is not null && Commands.TryGet(s.CommandId, out var tc)
                    ? new CommandBarToggleButton(tc) { DisplayStyle = display }
                    : null;
            case CommandItemKind.SplitButton:
            {
                if (s.CommandId is null || !Commands.TryGet(s.CommandId, out var sc))
                    return null;
                var split = new CommandBarSplitButton(sc) { DisplayStyle = display };
                RebuildItems(split.DropDown.Items, s.Children);
                return split;
            }
            default:
                return s.CommandId is not null && Commands.TryGet(s.CommandId, out var bc)
                    ? new CommandBarButton(bc) { DisplayStyle = display }
                    : null;
        }
    }

    private ItemState? FindByKey(string key)
    {
        foreach (var list in _defaults.Values)
        {
            var found = FindByKey(list, key);
            if (found is not null)
                return found;
        }
        return null;
    }

    private static ItemState? FindByKey(List<ItemState> list, string key)
    {
        foreach (var s in list)
        {
            if (s.Key == key)
                return s;
            var found = FindByKey(s.Children, key);
            if (found is not null)
                return found;
        }
        return null;
    }
}
