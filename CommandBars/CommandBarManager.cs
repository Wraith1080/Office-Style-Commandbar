using System.ComponentModel;
using System.Drawing;
using System.IO;
using System.Text;
using System.Text.Json;
using CommandBars.Controls;
using CommandBars.Imaging;
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
// String reference to the out-of-process design assembly. A typeof(...) to the
// in-process CommandBars.Design.CommandBarManagerDesigner binds a designer VS's
// out-of-process designer never loads, so the smart tag does nothing.
[Designer("CommandBars.Designer.Server.CommandBarManagerDesigner, CommandBars.Designer.Server")]
public class CommandBarManager : Component
{
    public CommandBarManager()
    {
        Commands = new CommandRegistry();
        Bars = new CommandBarCollection(this);
        SeedBuiltInThemes();
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
    /// grid (or the manager's catalog-first editor), then realize it into live
    /// bars at run time with <see cref="BuildFromDefinitions"/>. Editing here is
    /// independent of the runtime <see cref="Bars"/> collection.
    /// </summary>
    [Category("CommandBars")]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Content)]
    // Editor referenced by NAME (routed client-side to the real editor). A
    // typeof(Design.BarDefinitionCollectionEditor) binds an in-process editor VS
    // never loads out-of-process, so "…"/the smart tag do nothing.
    [System.ComponentModel.Editor(
        "BarDefinitionsEditor",
        typeof(System.Drawing.Design.UITypeEditor))]
    public List<Design.BarDefinition> BarDefinitions => _barDefinitions;

    private readonly List<Design.CommandDefinition> _commandDefinitions = new();
    private readonly Dictionary<string, Command> _catalogOwnedCommands =
        new(StringComparer.Ordinal);

    /// <summary>
    /// The reusable command catalog. Atomic entries own command presentation;
    /// Popup and SplitButton entries own child-reference trees; ComboBox entries
    /// own their hosted-control defaults. The catalog is edited together with
    /// <see cref="BarDefinitions"/> through the catalog-first manager editor;
    /// hiding this raw collection prevents a second, integrity-blind designer
    /// authoring path. It remains public for code construction and serialized
    /// designer compatibility.
    /// </summary>
    [Browsable(false)]
    [Category("CommandBars")]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Content)]
    public List<Design.CommandDefinition> CommandDefinitions => _commandDefinitions;

    private readonly List<CommandBarCustomizationItem> _customizationItems = new();
    private readonly List<CommandBarCustomizationItem> _codeCustomizationItems = new();
    private readonly HashSet<CommandBarComboBox> _comboBoxes = new();
    private bool _synchronizingComboBoxes;

    /// <summary>
    /// Joins a named hosted combo to the manager's selection group. Every combo
    /// with the same stable Name mirrors the same selection, just as command-
    /// backed items mirror shared command state.
    /// </summary>
    internal void RegisterComboBox(CommandBarComboBox combo)
    {
        if (!_comboBoxes.Add(combo))
            return;

        combo.SelectedItemChanged += OnComboBoxSelectedItemChanged;
        combo.EnabledChanged += OnComboBoxEnabledChanged;
        if (string.IsNullOrEmpty(combo.Name))
            return;

        var peer = _comboBoxes.FirstOrDefault(candidate =>
            !ReferenceEquals(candidate, combo) &&
            string.Equals(candidate.Name, combo.Name, StringComparison.Ordinal));
        if (peer is null)
            return;

        _synchronizingComboBoxes = true;
        try
        {
            combo.SelectedItem = peer.SelectedItem;
            combo.Enabled = peer.Enabled;
        }
        finally
        {
            _synchronizingComboBoxes = false;
        }
    }

    internal void UnregisterComboBox(CommandBarComboBox combo)
    {
        if (!_comboBoxes.Remove(combo))
            return;
        combo.SelectedItemChanged -= OnComboBoxSelectedItemChanged;
        combo.EnabledChanged -= OnComboBoxEnabledChanged;
    }

    private void OnComboBoxSelectedItemChanged(object? sender, EventArgs e)
    {
        if (_synchronizingComboBoxes || sender is not CommandBarComboBox source ||
            string.IsNullOrEmpty(source.Name))
            return;

        _synchronizingComboBoxes = true;
        try
        {
            foreach (var combo in _comboBoxes)
                if (!ReferenceEquals(combo, source) &&
                    string.Equals(combo.Name, source.Name, StringComparison.Ordinal))
                    combo.SelectedItem = source.SelectedItem;
        }
        finally
        {
            _synchronizingComboBoxes = false;
        }
    }

    private void OnComboBoxEnabledChanged(object? sender, EventArgs e)
    {
        if (_synchronizingComboBoxes || sender is not CommandBarComboBox source ||
            string.IsNullOrEmpty(source.Name))
            return;

        _synchronizingComboBoxes = true;
        try
        {
            foreach (var combo in _comboBoxes)
                if (!ReferenceEquals(combo, source) &&
                    string.Equals(combo.Name, source.Name, StringComparison.Ordinal))
                    combo.Enabled = source.Enabled;
        }
        finally
        {
            _synchronizingComboBoxes = false;
        }
    }

    /// <summary>
    /// Compound entries available in the Customize dialog in addition to the
    /// ordinary command registry. Designer definitions opt in through
    /// <see cref="Design.ItemDefinition.IncludeInCommandList"/>.
    /// </summary>
    [Browsable(false)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public IReadOnlyList<CommandBarCustomizationItem> CustomizationItems => _customizationItems;

    /// <summary>
    /// Registers a code-created customization entry, for example a hosted
    /// control or a popup template. Registering the same id replaces the old
    /// entry. Definition-backed entries are refreshed by
    /// <see cref="BuildFromDefinitions"/>.
    /// </summary>
    public void RegisterCustomizationItem(CommandBarCustomizationItem item)
    {
        ArgumentNullException.ThrowIfNull(item);
        _codeCustomizationItems.RemoveAll(existing =>
            string.Equals(existing.Id, item.Id, StringComparison.Ordinal));
        _codeCustomizationItems.Add(item);
        RebuildCustomizationCatalog();
    }

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
        AssignDefinitionCommandIds();
        RegisterCatalogCommands();
        RebuildCustomizationCatalog();
        Bars.Clear();
        var catalog = CreateCatalogMaterializer();
        foreach (var def in _barDefinitions)
        {
            var bar = def.Build(Commands, _images, catalog);
            Bars.Add(bar);
        }
        RefreshLayout();
    }

    /// <summary>
    /// Creates a fresh runtime occurrence of a reusable command-catalog entry.
    /// Executable entries resolve through <see cref="Commands"/>, so multiple
    /// occurrences share enabled, checked, presentation, and execution state.
    /// Compound entries recursively resolve their child catalog references.
    /// </summary>
    /// <exception cref="KeyNotFoundException">The id is not in the catalog.</exception>
    /// <exception cref="InvalidOperationException">
    /// The catalog contains duplicate ids or a compound-reference cycle.
    /// </exception>
    public CommandBarItem CreateCatalogItem(string commandId)
    {
        var materializer = CreateCatalogMaterializer();
        materializer.RegisterCommands();
        return materializer.Build(commandId);
    }

    private void RebuildCustomizationCatalog()
    {
        AssignDefinitionCommandIds();
        _customizationItems.Clear();
        _customizationItems.AddRange(_codeCustomizationItems);

        var used = new HashSet<string>(
            _customizationItems.Select(item => item.Id),
            StringComparer.Ordinal);

        // Catalog-owned compound entries are the canonical factories. Add them
        // before legacy ItemDefinition factories so a split/popup/combo with the
        // same stable id cannot be flattened into a generic command button.
        var catalog = CreateCatalogMaterializer();
        catalog.RegisterCommands();
        foreach (var definition in _commandDefinitions)
        {
            if (!definition.IncludeInCommandList ||
                string.IsNullOrWhiteSpace(definition.Id) ||
                !used.Add(definition.Id))
                continue;

            var preview = catalog.Build(definition.Id);
            string id = definition.Id;
            _customizationItems.Add(new CommandBarCustomizationItem(
                id,
                GetCustomizationText(preview, definition.Text),
                GetCustomizationImage(preview),
                () => CreateCatalogItem(id)));
        }

        foreach (var definition in EnumerateDefinitions(_barDefinitions.SelectMany(bar => bar.Items)))
        {
            if (!definition.IncludeInCommandList)
                continue;

            var preview = definition.Build(Commands, _images);
            if (preview is null)
                continue;

            string id = preview is CommandBarCommandItem commandPreview
                ? commandPreview.Command.Id
                : !string.IsNullOrWhiteSpace(definition.Name)
                    ? definition.Name
                    : $"definition:{definition.Kind}:{Command.RemoveMnemonic(definition.Text)}";
            if (!used.Add(id))
                continue;

            var captured = definition;
            _customizationItems.Add(new CommandBarCustomizationItem(
                id,
                GetCustomizationText(preview, definition.Text),
                GetCustomizationImage(preview),
                () => captured.Build(Commands, _images)
                    ?? throw new InvalidOperationException($"Could not build customization item '{id}'.")));
        }
    }

    private static string GetCustomizationText(CommandBarItem preview, string fallback)
        => preview switch
        {
            CommandBarCommandItem commandItem => commandItem.Command.DisplayText,
            CommandBarPopupItem popup => popup.DisplayText,
            CommandBarComboBox combo => combo.Label ?? combo.Name ?? "Combo Box",
            CommandBarLabel label => label.Text,
            _ => Command.RemoveMnemonic(fallback),
        };

    private static IImageSource? GetCustomizationImage(CommandBarItem preview)
        => preview switch
        {
            CommandBarCommandItem commandItem => commandItem.Command.Image,
            CommandBarPopupItem popup => popup.Image,
            CommandBarComboBox combo => combo.Image,
            _ => null,
        };

    private static IEnumerable<Design.ItemDefinition> EnumerateDefinitions(
        IEnumerable<Design.ItemDefinition> definitions)
    {
        foreach (var definition in definitions)
        {
            yield return definition;
            foreach (var child in EnumerateDefinitions(definition.Items))
                yield return child;
        }
    }

    /// <summary>
    /// Registers each <see cref="CommandDefinitions"/> entry's presentation into
    /// the <see cref="Commands"/> registry, non-destructively: a command already
    /// created in code keeps its text/shortcut/image and (crucially) its
    /// <c>ExecuteHandler</c>; the catalog only fills gaps. So the catalog supplies
    /// what a command looks like, while code still supplies what it does. Items
    /// that reference the id then resolve to this shared command, inheriting its
    /// text and icon without restating them per bar.
    /// </summary>
    private Design.CommandCatalogMaterializer CreateCatalogMaterializer(bool designPreview = false)
        => new(
            _commandDefinitions,
            Commands,
            _images,
            _catalogOwnedCommands,
            designPreview);

    private void RegisterCatalogCommands(bool designPreview = false)
        => CreateCatalogMaterializer(designPreview).RegisterCommands();

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

        AssignDefinitionCommandIds();
        RegisterCatalogCommands(designPreview: true);
        Bars.Clear();
        var catalog = CreateCatalogMaterializer(designPreview: true);
        var used = new HashSet<string>(StringComparer.Ordinal);
        for (int i = 0; i < _barDefinitions.Count; i++)
        {
            var def = _barDefinitions[i];
            try
            {
                string? nameOverride = string.IsNullOrWhiteSpace(def.Name) ? $"__preview{i}" : null;
                var bar = def.Build(
                    Commands,
                    _images,
                    catalog,
                    nameOverride,
                    designPreview: true);
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
    /// band's preview when the definitions (or icons) actually changed. A forced
    /// refresh is reserved for the explicit designer action. Host rebuilds share
    /// one parent-layout/repaint pass so four edge hosts do not repeatedly lay out
    /// and synchronously paint the whole form.
    /// </summary>
    internal void RefreshDesignPreview(bool force = false)
    {
        bool definitionsChanged = EnsureDesignBars();
        if (!definitionsChanged && !force)
            return;

        var hosts = _hosts.ToArray();
        var parents = hosts
            .Select(host => host.Parent)
            .Where(parent => parent is not null)
            .Distinct()
            .ToArray();

        foreach (var parent in parents)
            parent!.SuspendLayout();

        try
        {
            foreach (var host in hosts)
                host.RefreshDesignPreview(updateImmediately: host.Parent is null);
        }
        finally
        {
            foreach (var parent in parents)
                parent!.ResumeLayout(performLayout: true);
        }

        foreach (var parent in parents)
        {
            parent!.Invalidate(invalidateChildren: true);
            parent.Update();
        }
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
        // Catalog: a referenced command's text/icon/shortcut changing must refresh
        // the preview of every item that resolves to it.
        foreach (var c in _commandDefinitions)
        {
            sb.Append(c.Id).Append('=').Append(c.Kind).Append('|')
              .Append(c.Text).Append('|')
              .Append(c.ImageKey).Append('|').Append(c.ImagePath).Append('|')
              .Append(c.Shortcut).Append('|')
              .Append(c.ToolTip).Append('|').Append(c.DisplayStyle).Append('|')
              .Append(c.InitialChecked).Append('|').Append(c.PrimaryCommandId).Append('|')
              .Append(c.ContentSource).Append('|')
              .Append(c.TearOff).Append('|').Append(c.TearOffTitle).Append('|')
              .Append(c.PaletteColumns).Append('|').Append(c.ComboWidth).Append('|')
              .Append(c.IncludeInCommandList).Append('|');
            foreach (string comboItem in c.ComboItems)
                sb.Append(comboItem).Append(',');
            sb.Append('|');
            AppendCatalogPlacementSignature(sb, c.Items);
            sb.Append('~');
        }
        sb.Append('#');
        foreach (var d in _barDefinitions)
        {
            sb.Append(d.BarType).Append('|').Append(d.Name).Append('|')
              .Append(d.Dock).Append('|').Append(d.Visible).Append('|')
              .Append(d.IconSize).Append('|').Append(d.Items.Count).Append('|')
              .Append(d.Placements.Count).Append(';');
            AppendItemSignature(sb, d.Items);
            AppendCatalogPlacementSignature(sb, d.Placements);
        }
        return sb.ToString();
    }

    private static void AppendCatalogPlacementSignature(
        StringBuilder sb,
        IEnumerable<Design.CommandPlacementDefinition> placements)
    {
        foreach (var placement in placements)
        {
            sb.Append(placement.Kind).Append(',').Append(placement.CommandId).Append(',')
              .Append(placement.Name).Append(',').Append(placement.Visible).Append(',')
              .Append(placement.BeginGroup).Append(',').Append(placement.Priority).Append(',')
              .Append(placement.UseCatalogDisplayStyle).Append(',')
              .Append(placement.DisplayStyle).Append('/');
        }
    }

    private static void AppendItemSignature(StringBuilder sb, List<Design.ItemDefinition> items)
    {
        foreach (var it in items)
        {
            sb.Append(it.Kind).Append(',').Append(it.Text).Append(',')
              .Append(it.CommandId).Append(',').Append(it.ImageKey).Append(',')
              .Append(it.ImagePath).Append(',')
              .Append(it.DisplayStyle).Append(',').Append(it.BeginGroup).Append(',')
              .Append(it.Priority).Append(',')
              .Append(it.IncludeInCommandList).Append(',').Append(it.ToolbarList).Append(',')
              .Append(it.ThemeList).Append('/');
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

        // A menu may already be open when the Customize dialog is shown. Close
        // the entire chain before switching modes so that its popup windows
        // cannot keep dispatching commands from the old interaction session.
        MenuSession.Current?.End();
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

    /// <summary>Rebuilds a declarative dynamic popup immediately before it opens.</summary>
    internal void PreparePopup(CommandBarPopupItem popup)
    {
        if (!popup.ToolbarList && !popup.ThemeList &&
            string.IsNullOrEmpty(popup.ComboBoxName))
            return;

        popup.DropDown.Items.Clear();
        if (!string.IsNullOrEmpty(popup.ComboBoxName))
        {
            string comboName = popup.ComboBoxName;
            object? selected = _comboBoxes.FirstOrDefault(combo =>
                string.Equals(combo.Name, comboName, StringComparison.Ordinal))?.SelectedItem;
            for (int index = 0; index < popup.ComboBoxItems.Count; index++)
            {
                string value = popup.ComboBoxItems[index];
                var choice = new Command($"combo-menu:{comboName}:{index}")
                {
                    Text = value,
                    IsCheckable = true,
                    Checked = string.Equals(selected?.ToString(), value, StringComparison.Ordinal)
                        ? CommandCheckState.Checked
                        : CommandCheckState.Unchecked,
                };
                choice.ExecuteHandler = _ => SetComboBoxSelection(comboName, value);
                popup.DropDown.Items.AddToggle(choice);
            }
            return;
        }

        if (popup.ThemeList)
        {
            foreach (var registration in _themes)
            {
                var theme = registration;
                var command = new Command("theme-list:" + theme.Key)
                {
                    Text = theme.Text,
                    IsCheckable = true,
                    Checked = string.Equals(_activeThemeKey, theme.Key, StringComparison.Ordinal)
                        ? CommandCheckState.Checked
                        : CommandCheckState.Unchecked,
                };
                command.ExecuteHandler = _ =>
                {
                    if (!ApplyTheme(theme.Key))
                        return;
                    foreach (var item in popup.DropDown.Items)
                        if (item is CommandBarToggleButton toggle)
                            toggle.Command.Checked = string.Equals(
                                toggle.Command.Id,
                                "theme-list:" + _activeThemeKey,
                                StringComparison.Ordinal)
                                ? CommandCheckState.Checked
                                : CommandCheckState.Unchecked;
                };
                popup.DropDown.Items.AddToggle(command);
            }
            return;
        }

        foreach (var bar in Bars)
        {
            if (bar.BarType != CommandBarType.Toolbar)
                continue;

            var targetBar = bar;
            var command = new Command("toolbar-list:" + targetBar.Name)
            {
                Text = targetBar.Text,
                IsCheckable = true,
                Checked = targetBar.Visible
                    ? CommandCheckState.Checked
                    : CommandCheckState.Unchecked,
            };
            command.ExecuteHandler = _ =>
            {
                targetBar.Visible = !targetBar.Visible;
                command.Checked = targetBar.Visible
                    ? CommandCheckState.Checked
                    : CommandCheckState.Unchecked;
                RefreshLayout();
            };
            popup.DropDown.Items.AddToggle(command);
        }
    }

    private void SetComboBoxSelection(string name, string value)
    {
        var combo = _comboBoxes.FirstOrDefault(candidate =>
            string.Equals(candidate.Name, name, StringComparison.Ordinal));
        if (combo is not null)
            combo.SelectedItem = value;
        RefreshLayout();
    }

    /// <summary>
    /// Creates a menu-compatible occurrence from a Customize palette entry.
    /// Popups and split buttons retain their complete dropdowns; hosted combos
    /// become a dynamically checked submenu of their choices.
    /// </summary>
    internal CommandBarItem CreateMenuCustomizationItem(CommandBarCustomizationItem entry)
    {
        ArgumentNullException.ThrowIfNull(entry);
        var item = entry.CreateItem();
        if (item is not CommandBarComboBox combo)
            return item;

        string comboName = string.IsNullOrWhiteSpace(combo.Name) ? entry.Id : combo.Name;
        var popup = new CommandBarPopupItem(
            combo.Label ?? combo.SelectedItem?.ToString() ?? entry.Text)
        {
            Name = comboName,
            Image = combo.Image,
            ComboBoxName = comboName,
        };
        foreach (var value in combo.Items)
            popup.ComboBoxItems.Add(value?.ToString() ?? string.Empty);
        return popup;
    }

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

    private readonly List<CommandBarThemeRegistration> _themes = new();
    private CommandBarTheme _theme = CommandBarTheme.Office2003;
    private CommandBarRenderer _renderer = ThemeRenderer.Create(CommandBarTheme.Office2003);
    private string? _activeThemeKey = CommandBarThemeKeys.Office2003;
    private string? _pendingThemeKey;

    /// <summary>The application-managed themes, in menu display order.</summary>
    [Browsable(false)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public IReadOnlyList<CommandBarThemeRegistration> Themes => _themes;

    /// <summary>The stable key of the active registered theme, or null for an unregistered renderer.</summary>
    [Browsable(false)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public string? ActiveThemeKey => _activeThemeKey;

    /// <summary>Adds a theme, or replaces the entry with the same stable key.</summary>
    public void RegisterTheme(string key, string text, Func<CommandBarRenderer> rendererFactory)
        => RegisterTheme(new CommandBarThemeRegistration(key, text, rendererFactory));

    /// <summary>Adds a theme registration, or replaces its stable key.</summary>
    public void RegisterTheme(CommandBarThemeRegistration registration)
    {
        ArgumentNullException.ThrowIfNull(registration);
        string key = registration.Key;
        int index = _themes.FindIndex(t => string.Equals(t.Key, key, StringComparison.Ordinal));
        if (index >= 0)
            _themes[index] = registration;
        else
            _themes.Add(registration);

        if (string.Equals(_pendingThemeKey, key, StringComparison.Ordinal))
        {
            _pendingThemeKey = null;
            ApplyTheme(key);
        }
        else if (_pendingThemeKey is null && string.Equals(_activeThemeKey, key, StringComparison.Ordinal))
        {
            ApplyTheme(key);
        }
    }

    private void AssignDefinitionCommandIds()
    {
        for (int barIndex = 0; barIndex < _barDefinitions.Count; barIndex++)
        {
            var bar = _barDefinitions[barIndex];
            string barKey = string.IsNullOrWhiteSpace(bar.Name)
                ? "bar" + barIndex
                : bar.Name;
            Assign(bar.Items, "definition:" + barKey);
        }

        static void Assign(List<Design.ItemDefinition> items, string parentKey)
        {
            for (int index = 0; index < items.Count; index++)
            {
                var item = items[index];
                string segment = string.IsNullOrWhiteSpace(item.Name)
                    ? index.ToString(System.Globalization.CultureInfo.InvariantCulture)
                    : item.Name;
                string key = parentKey + ":" + segment;
                if (item.Kind is CommandItemKind.Button or
                    CommandItemKind.ToggleButton or
                    CommandItemKind.SplitButton)
                    item.SetGeneratedCommandId(key);
                Assign(item.Items, key);
            }
        }
    }

    /// <summary>Removes a registered theme while leaving its current renderer safely in place.</summary>
    public bool RemoveTheme(string key)
    {
        int index = _themes.FindIndex(t => string.Equals(t.Key, key, StringComparison.Ordinal));
        if (index < 0)
            return false;
        _themes.RemoveAt(index);
        if (string.Equals(_activeThemeKey, key, StringComparison.Ordinal))
            _activeThemeKey = null;
        return true;
    }

    /// <summary>Removes every application-managed theme.</summary>
    public void ClearThemes()
    {
        _themes.Clear();
        _activeThemeKey = null;
    }

    /// <summary>Creates and applies a fresh renderer for a registered stable key.</summary>
    public bool ApplyTheme(string key)
    {
        var registration = _themes.FirstOrDefault(t => string.Equals(t.Key, key, StringComparison.Ordinal));
        if (registration is null)
            return false;

        _renderer = registration.RendererFactory()
            ?? throw new InvalidOperationException($"Theme factory '{key}' returned null.");
        _activeThemeKey = registration.Key;
        _pendingThemeKey = null;
        if (CommandBarThemeKeys.TryToTheme(registration.Key, out var builtIn))
            _theme = builtIn;
        ApplyThemeToHosts();
        return true;
    }

    private void SeedBuiltInThemes()
    {
        _themes.Add(new(CommandBarThemeKeys.Office2000, "Office &2000", () => ThemeRenderer.Create(CommandBarTheme.Office2000)));
        _themes.Add(new(CommandBarThemeKeys.Office2003, "Office &2003", () => ThemeRenderer.Create(CommandBarTheme.Office2003)));
        _themes.Add(new(CommandBarThemeKeys.OfficeXP, "Office &XP", () => ThemeRenderer.Create(CommandBarTheme.OfficeXP)));
        _themes.Add(new(CommandBarThemeKeys.Office2007, "Office 200&7", () => ThemeRenderer.Create(CommandBarTheme.Office2007)));
        _themes.Add(new(CommandBarThemeKeys.Office2010Silver, "Office 20&10 (Silver)", () => ThemeRenderer.Create(CommandBarTheme.Office2010)));
        _themes.Add(new(CommandBarThemeKeys.Dark, "&Dark", () => ThemeRenderer.Create(CommandBarTheme.Dark)));
    }

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
            _theme = value;
            string key = CommandBarThemeKeys.FromTheme(value);
            if (!ApplyTheme(key))
            {
                _renderer = ThemeRenderer.Create(value);
                _activeThemeKey = null;
                _pendingThemeKey = null;
                ApplyThemeToHosts();
            }
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
    {
        if (!IsCustomizing)
            ActiveDrag = new DockDragSession(bar, size, grab, origin);
    }

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

    // --- Tear-off palettes -------------------------------------------------
    // Floating palettes torn off from a popup/dropdown menu. They live entirely
    // outside the dock system (so they can never re-dock) and are tracked here so
    // the manager can re-theme them and so a bar isn't torn off twice.

    private readonly List<TearOffWindow> _tearOffs = new();

    /// <summary>
    /// Tears <paramref name="bar"/> (a popup/dropdown <see cref="CommandBar"/>) off
    /// into a standalone non-dockable floating palette at <paramref name="screenCursor"/>.
    /// If it is already torn off, the existing palette is just moved/raised. The
    /// <see cref="CommandBarControl"/> chain calls this from a popup's tear-off grip.
    /// </summary>
    internal void ShowTearOff(CommandBar bar, Point screenCursor, System.Windows.Forms.Form? owner)
    {
        if (bar is null || IsCustomizing)
            return;

        // So submenus opened from the palette can reach this manager to tear off too.
        bar.Manager ??= this;

        foreach (var existing in _tearOffs)
            if (!existing.IsDisposed && ReferenceEquals(existing.SourceBar, bar))
            {
                if (!existing.Visible)
                    existing.Show();
                existing.BeginTearDrag(); // grab and follow the cursor
                return;
            }

        // Host a private CLONE, not the menu's own bar: item Bounds are mutable and
        // shared, so opening the source menu would otherwise overwrite the palette's
        // horizontal layout (and vice-versa), stretching items.
        var clone = ClonePaletteBar(bar);
        clone.Manager = this;
        var window = new TearOffWindow(clone, bar, _renderer, this, owner);
        _tearOffs.Add(window);
        window.FormClosed += (_, _) => _tearOffs.Remove(window);
        // Place near the cursor to avoid a flash at (0,0), then transfer the drag
        // so the palette follows the mouse until the button is released.
        window.Location = new Point(screenCursor.X - 30, screenCursor.Y - 8);
        window.Show();
        window.BeginTearDrag();
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
        var state = new LayoutState
        {
            Version = 2,
            ShowToolTips = ShowToolTips,
            ThemeKey = _pendingThemeKey ?? _activeThemeKey,
            Settings = new Dictionary<string, string>(_settings),
        };
        foreach (var bar in Bars)
            state.Bars.Add(CaptureBar(bar));
        state.TearOffs = CaptureTearOffs();
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

        string? savedThemeKey = state.ThemeKey;
        if (string.IsNullOrEmpty(savedThemeKey) && state.Settings.TryGetValue("theme", out var legacyTheme))
            savedThemeKey = LegacyThemeKey(legacyTheme);
        if (!string.IsNullOrEmpty(savedThemeKey) && !ApplyTheme(savedThemeKey))
            _pendingThemeKey = savedThemeKey;

        if (state.Bars.Count == 0)
        {
            OnLayoutChanged();
            return;
        }

        // A hosted combo's Image/Label are set in code — an IImageSource can't
        // round-trip through JSON — so the saved state has no record of them and
        // BuildItem would rebuild a bare combo (showing its selection text instead
        // of the icon). Preserve them by Name across the structural rebuild, the
        // same principle by which command handlers survive a reload.
        var comboConfig = new Dictionary<string, (IImageSource? Image, string? Label, bool Enabled)>(StringComparer.Ordinal);
        foreach (var existing in Bars)
            foreach (var kv in CaptureComboConfig(existing.Items))
                comboConfig[kv.Key] = kv.Value;

        // Popup images are also code-owned IImageSource instances. Preserve them
        // by the popup dropdown's stable key so nested menus (for example the
        // AutoShapes categories) keep their icons after a layout reload.
        var popupImages = new Dictionary<string, IImageSource>(StringComparer.Ordinal);
        foreach (var existing in Bars)
            foreach (var kv in CapturePopupImages(existing.Items))
                popupImages[kv.Key] = kv.Value;

        // Toolbar-list behavior is application/definition-owned. Preserve it by
        // dropdown key so a layout written by an older version (which has static
        // child toggles and no ToolbarList field) migrates to the live menu.
        var toolbarListMenus = new HashSet<string>(StringComparer.Ordinal);
        var themeListMenus = new HashSet<string>(StringComparer.Ordinal);
        foreach (var existing in Bars)
            foreach (var popup in EnumeratePopups(existing.Items))
                if (popup.ToolbarList)
                    toolbarListMenus.Add(popup.DropDown.Name);
                else if (popup.ThemeList)
                    themeListMenus.Add(popup.DropDown.Name);

        // Likewise preserve code-set dropdown tear-off opt-in + caption + palette columns by Name.
        var tearOffConfig = new Dictionary<string, (bool TearOff, string Text, int Columns)>(StringComparer.Ordinal);
        foreach (var existing in Bars)
            foreach (var kv in CaptureTearOffConfig(existing.Items))
                tearOffConfig[kv.Key] = kv.Value;

        // Any open tear-off palettes reference bars we're about to replace; close
        // them (the load may re-open them from state.TearOffs below).
        CloseAllTearOffs();

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

        // Re-apply the preserved code-set combo Image/Label and dropdown tear-off
        // config onto the rebuilt items.
        foreach (var bar in Bars)
        {
            RestoreComboConfig(bar.Items, comboConfig);
            RestorePopupImages(bar.Items, popupImages);
            RestoreToolbarListConfig(bar.Items, toolbarListMenus);
            RestoreThemeListConfig(bar.Items, themeListMenus);
            RestoreTearOffConfig(bar.Items, tearOffConfig);
        }

        // Re-open any tear-off palettes that were open when this state was saved,
        // once the form's message loop is idle (so we don't show floating windows
        // before the main form is up).
        RestoreTearOffs(state.TearOffs);

        OnLayoutChanged();
    }

    // --- Tear-off palette persistence --------------------------------------

    private void CloseAllTearOffs()
    {
        foreach (var window in _tearOffs.ToArray())
            if (!window.IsDisposed)
                window.Close();
        _tearOffs.Clear();
    }

    // Snapshot the currently open palettes for CaptureState.
    private List<TearOffState> CaptureTearOffs()
    {
        var list = new List<TearOffState>();
        foreach (var window in _tearOffs)
            if (!window.IsDisposed && window.Visible)
                list.Add(new TearOffState { BarName = window.SourceBar.Name, X = window.Location.X, Y = window.Location.Y });
        return list;
    }

    // Re-open saved palettes: find each dropdown bar by its stable Name and float
    // it (without a drag) at the saved position. Deferred to the host's message
    // loop so palettes appear after the main window is shown.
    private void RestoreTearOffs(List<TearOffState> saved)
    {
        if (saved is null || saved.Count == 0)
            return;
        var host = FloatOwner;
        if (host is null)
            return;

        var pending = new List<TearOffState>(saved);
        void Reopen()
        {
            if (host.IsDisposed)
                return;
            var owner = host.FindForm();
            foreach (var t in pending)
            {
                var bar = FindTearOffBar(t.BarName);
                if (bar is not null)
                    RestoreTearOff(bar, new Point(t.X, t.Y), owner);
            }
        }

        void QueueReopen()
        {
            try { host.BeginInvoke(new Action(Reopen)); }
            catch { /* host tearing down */ }
        }

        if (host.IsHandleCreated)
        {
            QueueReopen();
            return;
        }

        // Layouts are commonly loaded from the form constructor, before any
        // DockHost has a native handle. BeginInvoke throws in that state, which
        // previously discarded the restore request. Hold it until the first
        // handle is created, then queue it onto the UI message loop.
        EventHandler? handleCreated = null;
        handleCreated = (_, _) =>
        {
            host.HandleCreated -= handleCreated;
            QueueReopen();
        };
        host.HandleCreated += handleCreated;
    }

    // Floats a dropdown bar as a palette at a fixed position, no drag (used to
    // restore a saved palette). No-op if that bar is already torn off.
    private void RestoreTearOff(CommandBar bar, Point location, System.Windows.Forms.Form? owner)
    {
        bar.Manager ??= this;
        foreach (var existing in _tearOffs)
            if (!existing.IsDisposed && ReferenceEquals(existing.SourceBar, bar))
                return;

        var clone = ClonePaletteBar(bar);
        clone.Manager = this;
        var window = new TearOffWindow(clone, bar, _renderer, this, owner);
        _tearOffs.Add(window);
        window.FormClosed += (_, _) => _tearOffs.Remove(window);
        window.Location = location;
        window.Show();
    }

    // Finds a dropdown bar anywhere in the current bars (nested submenus included)
    // by its stable Name, so a saved palette can be reattached to its rebuilt bar.
    private CommandBar? FindTearOffBar(string name)
    {
        foreach (var bar in Bars)
            foreach (var dd in EnumerateDropDownBars(bar.Items))
                if (string.Equals(dd.Name, name, StringComparison.Ordinal))
                    return dd;
        return null;
    }

    // --- Palette cloning ---------------------------------------------------
    // A tear-off palette hosts a CLONE of the menu's dropdown, not the bar itself:
    // CommandBarItem.Bounds is mutable and shared, so if the palette and the menu
    // both referenced one bar, whichever laid out last would clobber the other's
    // geometry (the "stretched item" bug). The clone reuses the same Commands, so
    // toggles/enabled/checked state stay perfectly in sync between the two views.
    private static CommandBar ClonePaletteBar(CommandBar source)
    {
        var clone = new CommandBar(source.Name + ".float", CommandBarType.Popup)
        {
            Text = source.Text,
            IconSize = source.IconSize,
            AllowTearOff = source.AllowTearOff,
            PaletteColumns = source.PaletteColumns,
        };
        CloneItems(source.Items, clone.Items);
        return clone;
    }

    private static void CloneItems(CommandBarItemCollection src, CommandBarItemCollection dst)
    {
        foreach (var item in src)
        {
            CommandBarItem? clone = item switch
            {
                CommandBarSeparator => new CommandBarSeparator(),
                CommandBarLabel l => new CommandBarLabel(l.Text),
                CommandBarComboBox c => CloneCombo(c),
                CommandBarSplitButton sp => CloneSplit(sp),
                CommandBarToggleButton t => new CommandBarToggleButton(t.Command) { DisplayStyle = t.DisplayStyle },
                CommandBarButton b => new CommandBarButton(b.Command) { DisplayStyle = b.DisplayStyle },
                CommandBarPopupItem p => ClonePopup(p),
                CommandBarCommandItem cc => new CommandBarButton(cc.Command) { DisplayStyle = cc.DisplayStyle },
                _ => null,
            };
            if (clone is null)
                continue;
            clone.Name = item.Name;
            clone.BeginGroup = item.BeginGroup;
            clone.Priority = item.Priority;
            clone.Visible = item.Visible;
            dst.Add(clone);
        }
    }

    private static CommandBarSplitButton CloneSplit(CommandBarSplitButton sp)
    {
        var ns = new CommandBarSplitButton(sp.Command) { DisplayStyle = sp.DisplayStyle };
        CopyDropDownMeta(sp.DropDown, ns.DropDown);
        CloneItems(sp.DropDown.Items, ns.DropDown.Items);
        return ns;
    }

    private static CommandBarPopupItem ClonePopup(CommandBarPopupItem p)
    {
        var np = new CommandBarPopupItem(p.Text)
        {
            Image = p.Image,
            DisplayStyle = p.DisplayStyle,
            ToolbarList = p.ToolbarList,
            ThemeList = p.ThemeList,
            ComboBoxName = p.ComboBoxName,
        };
        np.ComboBoxItems.AddRange(p.ComboBoxItems);
        CopyDropDownMeta(p.DropDown, np.DropDown);
        CloneItems(p.DropDown.Items, np.DropDown.Items);
        return np;
    }

    private static CommandBarComboBox CloneCombo(CommandBarComboBox c)
    {
        var nc = new CommandBarComboBox
        {
            Width = c.Width,
            Image = c.Image,
            Label = c.Label,
            Enabled = c.Enabled,
        };
        foreach (var v in c.Items)
            nc.Items.Add(v);
        nc.SelectedItem = c.SelectedItem;
        return nc;
    }

    // Carries the tear-off-relevant metadata onto a cloned dropdown so nested
    // submenus of a palette can themselves be torn off.
    private static void CopyDropDownMeta(CommandBar src, CommandBar dst)
    {
        dst.Text = src.Text;
        dst.AllowTearOff = src.AllowTearOff;
        dst.IconSize = src.IconSize;
        dst.PaletteColumns = src.PaletteColumns;
    }

    // Walks an item collection (recursing into popup/split dropdowns) yielding
    // every hosted combo box.
    private static IEnumerable<CommandBarComboBox> EnumerateCombos(CommandBarItemCollection items)
    {
        foreach (var item in items)
        {
            switch (item)
            {
                case CommandBarComboBox combo:
                    yield return combo;
                    break;
                case CommandBarPopupItem p:
                    foreach (var c in EnumerateCombos(p.DropDown.Items))
                        yield return c;
                    break;
                case CommandBarSplitButton sp:
                    foreach (var c in EnumerateCombos(sp.DropDown.Items))
                        yield return c;
                    break;
            }
        }
    }

    // A hosted combo's Image/Label are set in code — an IImageSource can't
    // round-trip through the serialized snapshot used for persistence and Reset —
    // so any rebuild (LoadLayout, ResetBar, ResetMenu) would otherwise drop them,
    // leaving the vertical drop-down button with no icon. These two helpers snapshot
    // the live combos' (Image, Label) by Name before a clear+rebuild and re-apply
    // them afterward, the same way command handlers survive a reload.
    private static Dictionary<string, (IImageSource? Image, string? Label, bool Enabled)> CaptureComboConfig(CommandBarItemCollection items)
    {
        var map = new Dictionary<string, (IImageSource?, string?, bool)>(StringComparer.Ordinal);
        foreach (var combo in EnumerateCombos(items))
            if (!string.IsNullOrEmpty(combo.Name))
                map[combo.Name!] = (combo.Image, combo.Label, combo.Enabled);
        return map;
    }

    private static void RestoreComboConfig(CommandBarItemCollection items, Dictionary<string, (IImageSource? Image, string? Label, bool Enabled)> map)
    {
        if (map.Count == 0)
            return;
        foreach (var combo in EnumerateCombos(items))
            if (combo.Name is not null && map.TryGetValue(combo.Name, out var cfg))
            {
                combo.Image = cfg.Image;
                combo.Label = cfg.Label;
                combo.Enabled = cfg.Enabled;
            }
    }

    // Popup item images cannot be represented in LayoutState JSON. The dropdown
    // bar name is the popup's stable structural key and survives a rebuild.
    private static IEnumerable<CommandBarPopupItem> EnumeratePopups(CommandBarItemCollection items)
    {
        foreach (var item in items)
        {
            switch (item)
            {
                case CommandBarPopupItem popup:
                    yield return popup;
                    foreach (var nested in EnumeratePopups(popup.DropDown.Items))
                        yield return nested;
                    break;
                case CommandBarSplitButton split:
                    foreach (var nested in EnumeratePopups(split.DropDown.Items))
                        yield return nested;
                    break;
            }
        }
    }

    private static Dictionary<string, IImageSource> CapturePopupImages(CommandBarItemCollection items)
    {
        var map = new Dictionary<string, IImageSource>(StringComparer.Ordinal);
        foreach (var popup in EnumeratePopups(items))
            if (popup.Image is not null)
                map[popup.DropDown.Name] = popup.Image;
        return map;
    }

    private static void RestorePopupImages(CommandBarItemCollection items, Dictionary<string, IImageSource> map)
    {
        if (map.Count == 0)
            return;
        foreach (var popup in EnumeratePopups(items))
            if (map.TryGetValue(popup.DropDown.Name, out var image))
                popup.Image = image;
    }

    private static void RestoreToolbarListConfig(
        CommandBarItemCollection items,
        HashSet<string> toolbarListMenus)
    {
        if (toolbarListMenus.Count == 0)
            return;
        foreach (var popup in EnumeratePopups(items))
        {
            if (!toolbarListMenus.Contains(popup.DropDown.Name))
                continue;
            popup.ToolbarList = true;
            popup.DropDown.Items.Clear();
        }
    }

    private static void RestoreThemeListConfig(
        CommandBarItemCollection items,
        HashSet<string> themeListMenus)
    {
        if (themeListMenus.Count == 0)
            return;
        foreach (var popup in EnumeratePopups(items))
        {
            if (!themeListMenus.Contains(popup.DropDown.Name))
                continue;
            popup.ThemeList = true;
            popup.DropDown.Items.Clear();
        }
    }

    private static string LegacyThemeKey(string key) => key switch
    {
        "2000" => CommandBarThemeKeys.Office2000,
        "2003" => CommandBarThemeKeys.Office2003,
        "xp" => CommandBarThemeKeys.OfficeXP,
        "2007" => CommandBarThemeKeys.Office2007,
        "2010" => CommandBarThemeKeys.Office2010Silver,
        "dark" => CommandBarThemeKeys.Dark,
        _ => key,
    };

    // Every dropdown bar reachable from an item collection (popup + split-button
    // dropdowns), recursing into their own items so nested submenus are included.
    private static IEnumerable<CommandBar> EnumerateDropDownBars(CommandBarItemCollection items)
    {
        foreach (var item in items)
        {
            CommandBar? dd = item switch
            {
                CommandBarPopupItem p => p.DropDown,
                CommandBarSplitButton sp => sp.DropDown,
                _ => null,
            };
            if (dd is not null)
            {
                yield return dd;
                foreach (var nested in EnumerateDropDownBars(dd.Items))
                    yield return nested;
            }
        }
    }

    // A dropdown's tear-off opt-in (AllowTearOff) and palette caption (Text) are set
    // in code, so — like combo Image/Label — any rebuild from the serialized snapshot
    // would drop them (the grip vanishes after LoadLayout / Reset). Snapshot them by
    // the dropdown's stable Name before a clear+rebuild and re-apply afterward.
    private static Dictionary<string, (bool TearOff, string Text, int Columns)> CaptureTearOffConfig(CommandBarItemCollection items)
    {
        var map = new Dictionary<string, (bool, string, int)>(StringComparer.Ordinal);
        foreach (var dd in EnumerateDropDownBars(items))
            if (dd.AllowTearOff || dd.PaletteColumns > 0)
                map[dd.Name] = (dd.AllowTearOff, dd.Text, dd.PaletteColumns);
        return map;
    }

    private static void RestoreTearOffConfig(CommandBarItemCollection items, Dictionary<string, (bool TearOff, string Text, int Columns)> map)
    {
        if (map.Count == 0)
            return;
        foreach (var dd in EnumerateDropDownBars(items))
            if (map.TryGetValue(dd.Name, out var cfg))
            {
                dd.AllowTearOff = cfg.TearOff;
                dd.PaletteColumns = cfg.Columns;
                if (!string.IsNullOrEmpty(cfg.Text))
                    dd.Text = cfg.Text;
            }
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
    /// Runtime Customize may delete user-created toolbars, but bars captured as
    /// application defaults are structural definitions and can only be hidden
    /// or reset.
    /// </summary>
    internal bool CanDeleteFromCustomize(CommandBar bar)
    {
        ArgumentNullException.ThrowIfNull(bar);
        return _defaultLayout is null || !_defaults.ContainsKey(bar.Name);
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
        var keepRenderer = _renderer;
        var keepTheme = _theme;
        string? keepActiveThemeKey = _activeThemeKey;
        string? keepPendingThemeKey = _pendingThemeKey;
        ApplyState(_defaultLayout);
        _settings.Clear();
        foreach (var kv in keep)
            _settings[kv.Key] = kv.Value;
        _renderer = keepRenderer;
        _theme = keepTheme;
        _activeThemeKey = keepActiveThemeKey;
        _pendingThemeKey = keepPendingThemeKey;
        ApplyThemeToHosts();
        return true;
    }

    /// <summary>Restores a bar's items to the captured defaults. Returns false if none were captured.</summary>
    public bool ResetBar(CommandBar bar)
    {
        ArgumentNullException.ThrowIfNull(bar);
        if (!_defaults.TryGetValue(bar.Name, out var snapshot))
            return false;
        // Preserve code-set combo Image/Label and dropdown tear-off config.
        var comboConfig = CaptureComboConfig(bar.Items);
        var popupImages = CapturePopupImages(bar.Items);
        var tearOffConfig = CaptureTearOffConfig(bar.Items);
        bar.Items.Clear();
        RebuildItems(bar.Items, snapshot);
        RestoreComboConfig(bar.Items, comboConfig);
        RestorePopupImages(bar.Items, popupImages);
        RestoreTearOffConfig(bar.Items, tearOffConfig);
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
        // Preserve code-set combo Image/Label and dropdown tear-off config.
        var comboConfig = CaptureComboConfig(popup.DropDown.Items);
        var popupImages = CapturePopupImages(popup.DropDown.Items);
        var tearOffConfig = CaptureTearOffConfig(popup.DropDown.Items);
        popup.DropDown.Items.Clear();
        RebuildItems(popup.DropDown.Items, snapshot.Children);
        RestoreComboConfig(popup.DropDown.Items, comboConfig);
        RestorePopupImages(popup.DropDown.Items, popupImages);
        RestoreTearOffConfig(popup.DropDown.Items, tearOffConfig);
        OnLayoutChanged();
        return true;
    }

    // --- Shared structure (de)serialization -------------------------------

    private static List<ItemState> SnapshotItems(CommandBarItemCollection items)
    {
        var list = new List<ItemState>();
        foreach (var item in items)
        {
            var s = new ItemState
            {
                Kind = item.Kind.ToString(),
                Name = item.Name,
                BeginGroup = item.BeginGroup,
                Priority = item.Priority,
                Visible = item.Visible,
            };
            switch (item)
            {
                case CommandBarComboBox combo:
                    s.ComboWidth = combo.Width;
                    // Persist the entries, selected value first so it re-selects on load.
                    foreach (var value in combo.Items)
                        s.ComboItems.Add(value?.ToString() ?? string.Empty);
                    if (combo.SelectedItem is not null)
                    {
                        string sel = combo.SelectedItem.ToString() ?? string.Empty;
                        s.ComboItems.Remove(sel);
                        s.ComboItems.Insert(0, sel);
                    }
                    break;
                case CommandBarPopupItem p:
                    s.Text = p.Text;
                    s.DisplayStyle = p.DisplayStyle.ToString();
                    s.Key = p.DropDown.Name;
                    s.ToolbarList = p.ToolbarList;
                    s.ThemeList = p.ThemeList;
                    s.ComboBoxName = p.ComboBoxName;
                    s.ComboItems.AddRange(p.ComboBoxItems);
                    s.Children = p.ToolbarList || p.ThemeList ||
                        !string.IsNullOrEmpty(p.ComboBoxName)
                        ? new List<ItemState>()
                        : SnapshotItems(p.DropDown.Items);
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
            if (!string.IsNullOrEmpty(s.Name))
                item.Name = s.Name;
            item.BeginGroup = s.BeginGroup;
            item.Priority = s.Priority;
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
            {
                var combo = new CommandBarComboBox { Width = s.ComboWidth > 0 ? s.ComboWidth : 120 };
                if (s.ComboItems is not null)
                    foreach (var entry in s.ComboItems)
                        combo.Items.Add(entry);
                if (combo.Items.Count > 0)
                    combo.SelectedItem = combo.Items[0];
                return combo;
            }
            case CommandItemKind.Popup:
            {
                var popup = new CommandBarPopupItem(s.Text ?? string.Empty)
                {
                    DisplayStyle = display,
                    ToolbarList = s.ToolbarList,
                    ThemeList = s.ThemeList,
                    ComboBoxName = s.ComboBoxName,
                };
                if (s.ComboItems is not null)
                    popup.ComboBoxItems.AddRange(s.ComboItems);
                // Dynamic toolbar-list popups intentionally have no persisted
                // children; their live checklist is rebuilt whenever they open.
                if (!popup.ToolbarList && !popup.ThemeList &&
                    string.IsNullOrEmpty(popup.ComboBoxName))
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
