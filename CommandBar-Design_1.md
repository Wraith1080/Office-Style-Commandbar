# CommandBars — Design & Handoff Document

A reusable **WinForms control library (.NET 8, `net8.0-windows`)** reproducing the
classic Microsoft Office **CommandBars** experience: dockable/floating bars,
runtime themes, DPI-awareness, vector (SVG) icons, full runtime customization,
keyboard navigation, JSON persistence, and **out-of-process Visual Studio
design-time support** (implemented — see §6/§9).

This document is the single source of truth for project state. It supersedes the
stale "Phase 2" status in `README.md`.

---

## 0. Read this first (assisting-agent handoff notes)

This project is edited across many chat sessions through a **desktop file
bridge**, and there is a recurring **staleness hazard** that has repeatedly cost
work. Read this section before touching any file.

- **The repo now has git.** Use it. Before editing a file, prefer confirming its
  true content against `HEAD`; after delivering, the user diffs to verify only the
  intended lines changed. If you need the authoritative content of a file, ask the
  user for `git show HEAD:<path>` rather than trusting a stale mirror.
- **The "revert gremlin" is real.** The bridge / OneDrive sync has more than once
  handed back an **older snapshot** of a file than what is actually on disk (e.g.
  `CommandBarManager.cs` came back missing the whole command catalog;
  `CommandBarControl.cs` came back missing the entire combo subsystem). If you edit
  that stale copy and write it back, you silently delete real work.
- **Mitigations that work:**
  1. **Always re-stage a file immediately before editing it**, and sanity-check it
     contains the features you expect (grep for a known-recent member).
  2. **Always write back with an mtime guard** (`expectedMtimeMs`). A rejection
     means your staged copy was stale — re-stage and reconcile, do **not**
     `force`.
  3. When a merge is needed, get the good base from `git show HEAD:<path>` and
     re-apply the small change on top of it.
- **Build/verify:** the library **cannot be compiled in the assistant's Linux
  sandbox** (WinForms / `net8.0-windows`). Verification is **structural only**
  (brace/paren/bracket balance, XML well-formedness) before delivery; the user
  builds & runs on Windows and reports back. Design-time code is untestable in the
  sandbox and version-sensitive in VS — expect iterative back-and-forth.

**Device:** `desktop-jhgvbtf`
**Project folder:** `C:\Users\Rahmat Irfan\Claude\Projects\Professional Office Style Commandbar - Winform`

---

## 1. Current status

**Runtime: feature-complete for v1.** Menu bar + toolbars render, dock to all
four edges, float/undock, theme live, customize interactively, persist, and
navigate by keyboard. Hosted **combo boxes** are fully interactive (see §4a).

**Design-time: implemented out-of-process.** The parked plan from earlier
sessions is **done**: a Server/Client/Protocol assembly split (§9) gives the VS
out-of-process designer working smart-tag verbs ("Edit toolbars and menus…"), a
cross-process round-trip of the bar/command/image definitions, a shared **command
catalog** with a palette, a **stock-icon gallery** (with colour tinting), and an
**ImageKey picker** with thumbnails. Live preview on the design surface continues
to work (it's the control's own paint).

---

## 2. Solution layout

```
CommandBars/                     (class library, AssemblyName=CommandBars, net8.0-windows)
  Model/                         object/action model
  Rendering/                     themes (renderer + color table per theme)
  Controls/                      the WinForms controls
  Imaging/                       image sources (SVG/raster) + SvgImageList
  Persistence/                   JSON layout state
  Design/                        design-time definition types + string-referenced designers
CommandBars.Demo/                runnable WinExe demo (code-built + designer-built forms)
CommandBars.PackageDemo/         full designer-authored showcase consuming the library as a NuGet package
CommandBars.Designer.Protocol/   transport POCOs + shared editors      (multi-target: net8.0-windows; net472)
CommandBars.Designer.Server/     ControlDesigners, action lists, handlers, mappers (net8.0-windows)
CommandBars.Designer.Client/     UITypeEditors, dialogs, TypeRoutingProvider          (net472)
```

`CommandBars.csproj`: `net8.0-windows` (no explicit `10.0.x` platform version —
kept neutral so the NuGet `lib/net8.0/` folder packs without the "missing
platform version" error), `UseWindowsForms`, `Nullable=enable`,
`ImplicitUsings=enable`, `GenerateDocumentationFile` (NoWarn CS1591), PackageRef
`Svg` 3.4.7. The Client project uses `LangVersion 10.0` (net472 defaults to C# 9,
which rejects file-scoped namespaces).

---

## 3. Object model (`CommandBars.Model`)

- **`Command`** — the action behind items. `INotifyPropertyChanged`; `Id`, `Text`
  (with `&` mnemonic), `Image` (`IImageSource`), `Shortcut` (`Keys`), `Enabled`,
  `Checked` (tri-state), `IsCheckable`, `ToolTip`; `ExecuteHandler`/`CanExecuteHandler`
  delegates + `Executing`/`Executed` events; `Perform()`; static `RemoveMnemonic`,
  `FormatShortcut`.
- **`CommandRegistry`** — id→Command store; `Register`, `GetOrAdd`, `TryGet`
  (indexer throws — always use `TryGet`).
- **`CommandBarItem`** (abstract) — base of all items. Has `Visible`, `BeginGroup`,
  **`Name`** (optional, for code lookup and persistence), `Tag`, `Bounds`, `Kind`.
  → `CommandBarCommandItem` (holds a `Command`): `CommandBarButton`,
  `CommandBarToggleButton`, `CommandBarSplitButton` (has `DropDown` popup).
  Non-command items: `CommandBarPopupItem` (own `Text`+`Image`+`DropDown`),
  `CommandBarSeparator`, `CommandBarLabel`, `CommandBarComboBox`.
  `DisplayStyle` = ImageOnly/TextOnly/ImageAndText.
- **`CommandBarComboBox`** — hosted combo. `IList<object> Items`, `int Width`
  (logical px, default 120), `object? SelectedItem` (+ `SelectedItemChanged`
  event). Reachable from code via `CommandBar.FindComboBox(name)`.
- **`CommandBar`** — one bar (MenuBar/Toolbar/Popup). `Name` (stable id, immutable),
  `Text`, `Items`, `Dock` (`DockState`), `Visible`, `IconSize`, `AllowFloat`,
  `AllowCustomize`, `Locked`, `Row`/`Offset`, `FloatingBounds`, `Orientation`
  (derived: popups & Left/Right docks are Vertical). **`FindItem(name)` /
  `FindComboBox(name)`** search recursively into popup/split dropdowns.
- **`CommandBarItem`** — shared item state includes `Visible`, `BeginGroup`,
  `Name`, and Office-compatible `Priority` (0-7, default 3; value 1 keeps a
  toolbar item from entering overflow).
- **`CommandBarItemCollection`** — fluent `Add*` helpers: `AddButton`, `AddToggle`,
  `AddSplitButton`, `AddPopup`, `AddSeparator`, `AddLabel`, `AddComboBox`.
- **`Enums.cs`** — `DockState`, `CommandBarType`, `BarOrientation`,
  `CommandItemDisplayStyle`, `CommandItemKind`, `CommandCheckState`.
- **`IconSizes`** — steps `{12,16,20,24,32,48,64}`, `Default=24`.

---

## 4. Controls (`CommandBars.Controls`)

- **`CommandBarManager` : Component** — owns `Commands` and `Bars`; the hub.
  Hosts registry (`_hosts`), drag session routing, customize mode
  (`BeginCustomize`/`EndCustomize`/`IsCustomizing`/`CustomizeChanged`),
  `CustomizeRequested` event + `RequestCustomize()` (chevron "Customize…"),
  settings dict (`SetSetting`/`GetSetting`), JSON persistence
  (`SaveLayout`/`LoadLayout`), defaults capture + `ResetBar`/`ResetMenu`/`ResetToDefaults`,
  `ProcessShortcut`. **Design-time surface:** `BarDefinitions`,
  **`CommandDefinitions`** (the command catalog, §6a), `Images`,
  `BuildFromDefinitions()` (calls `RegisterCatalogCommands()` first),
  `EnsureDesignBars()`/`RefreshDesignPreview()`, **`Theme`** + `Renderer` +
  `ThemeChanged` (§7). The `BarDefinitions` `[Editor]` is referenced **by string**
  (`"BarDefinitionsEditor"`), routed client-side — a `typeof(...)` binding would
  bind an in-process editor VS never loads out-of-process.
- **`DockHost` : Panel** — one dock band per edge (`Edge`, `Manager`, `Renderer`).
  Horizontal edges stack rows; vertical edges stack columns. A constrained line
  is allocated as a whole: longer toolbars shrink first, then bars converge on a
  usable gripper/chevron minimum instead of sacrificing the final toolbar.
  Cross-edge drag, float/redock, drop preview. Renders live design preview from
  `BarDefinitions`.
- **`CommandBarControl` : Control** — a single bar: layout, paint, hover/press,
  overflow chevron + flyout (Office-nested: *Add or Remove Buttons ▸ {toolbar} ▸
  item checklist + Reset Toolbar*, then *Customize…*), split-button two-region
  render, tooltips, keyboard focus (Tab/arrows/Enter/Esc), Alt-gated mnemonics.
  Subscribes to each item's `Command.PropertyChanged` so a change made elsewhere
  (e.g. toggling a checked state from a menu) repaints the shared toolbar button
  immediately. Hosts the combo interaction (§4a). Ordinary items overflow from
  right to left; Priority=1 items remain on the bar and retained controls reflow
  into space released by overflowed items. Its ScreenTip component uses
  `ToolTip.ShowAlways=true` because floating toolbar frames intentionally never
  activate; the manager's `ShowToolTips` option remains the application switch.
  Popup items use their mnemonic-free `DisplayText` as a ScreenTip, which labels
  icon-only category dropdowns such as AutoShapes.
- **`CommandBarPopupWindow`** — non-activating dropdown menu; icons, shortcuts,
  checks (orange check box, hidden on hover — hover box is
  `Rectangle(3, b.Y, b.Width-6, b.Height-1)` so the selection has an extra pixel
  of breathing room at both horizontal edges while spacing above/below the check box
  is even), mnemonic activation. Child menus use a one-DPI-pixel overlap to avoid
  a seam, and submenu-arrow glyph geometry scales with DPI rather than remaining
  at fixed device-pixel dimensions.
- **`ComboDropDown`** — the hosted combo's list popup (§4a).
- **`MenuSession` : IMessageFilter** — closes popup chains on outside click;
  keyboard nav + mnemonics within open menus.
- **`FloatingWindow`**, **`DropPreviewWindow`**, **`DockEdge`/`DockDragSession`**,
  **`BarLayoutEngine`** (H & V measure/layout), **`BarMetrics`** (DPI metrics).
- **`CommandsPalette`** — drag-source list of commands for the Customize dialog.
- **`CustomizeDialog`** — non-modal, 4 tabs (Toolbars / Menus / Commands /
  Options). Side-button panels autosize; both button groups equalized to their
  widest button in `OnLoad` (DPI-correct). Reset All, per-bar/menu reset.

### 4a. Hosted combo boxes (interaction detail)

The combo is **custom-drawn** by `CommandBarControl` (not a native `ComboBox`),
so it themes and lays out with the rest of the bar.

- **Closed box** (`DrawComboBox` + `ComboBoxRect`): a white field sized to the text
  height (`Font.Height + 6`, centred in the cell — not the full icon-row height),
  with a drop-arrow button on the right. It has **hover and pressed states**:
  hovering highlights the arrow button (`RenderState.Hot`, `ButtonHotBorder`);
  while the list is open or the mouse is held it draws pressed
  (`RenderState.Pressed`, `ButtonPressedBorder`). State is tracked with `_hotCombo`
  (mouse-over) and `_openCombo` (list open); the box opens on **mouse-up**
  (opening on mouse-down would immediately dismiss on the release).
- **`ComboDropDown`** (the open list): a **non-activating, owner-drawn** borderless
  form. It hosts **no focusable child control** — critical: a child `ListBox`
  calls `SetFocus` on click, which forces Windows to activate the popup, deactivate
  the owner form, then reactivate it on close (a visible focus "flicker"). Instead
  it paints its own rows and hit-tests the mouse, exactly like
  `CommandBarPopupWindow`. It uses `ShowWithoutActivation`,
  `WS_EX_NOACTIVATE|WS_EX_TOOLWINDOW`, `WM_MOUSEACTIVATE→MA_NOACTIVATE`, an
  `IMessageFilter` to close on outside mouse-down (a non-activating window never
  gets `OnDeactivate`), hover-follow row highlighting, and mouse-wheel scrolling
  for long lists.
- **From code:** `bar.FindComboBox("font.combo")` → set `.Items` / `.SelectedItem`,
  handle `SelectedItemChanged`. See `MainForm.BuildBars` (the Formatting toolbar's
  `font.combo`).
- **Shared copies:** named ComboBoxes owned by the same `CommandBarManager`
  synchronize `SelectedItem` and `Enabled` by their stable `Name`. A copy dragged from the
  Customize command palette adopts the existing group's value when inserted, and
  later changes from any copy repaint and update every peer. Disabled combos use
  disabled rendering, ignore pointer input, and disable their overflow choices.
- **Customize state preservation:** generic palette entries preserve the command's
  concrete interaction kind (`IsCheckable` creates a `CommandBarToggleButton`),
  so both `Checked` and `Enabled` remain shared through the backing `Command`.
  Compound factories take precedence over generic command fallbacks, retaining
  split-button dropdowns and other complete item structure. Blank `CommandId`
  definitions receive a deterministic manager-owned command identity so their
  customized copies cannot fork command state.
- **From the editor:** the item's `ComboItems` (string list, edited with the
  built-in `StringCollectionEditor` — the default `List<T>` editor throws
  "Constructor on type 'System.String' not found") and `ComboWidth`.

---

## 5. Rendering / themes (`CommandBars.Rendering`)

`CommandBarRenderer` (abstract) + `CommandBarColorTable` (all chrome colors are
virtual). `Office2003Renderer` is the fully-parameterized base; other themes
subclass it and supply a color table (+ `ChunkRadius`). Key colour hooks used by
the combo: `ButtonHot*`/`ButtonPressed*` (begin/end/border), `BarBorder`,
`MenuItemSelected*`, `Text`.

Themes: **Office 2003**, **Office XP** (flat, `ChunkRadius 0`), **Office 2007**
(glassy blue), **Office 2010** (flat silver, gold hover), **Dark** (charcoal,
`#007ACC` accent). Enum + factory: `CommandBarTheme` + `ThemeRenderer.Create`.

`CommandBarManager` owns an ordered application-managed theme registry. Each
`CommandBarThemeRegistration` has a stable key, display text, and a
`Func<CommandBarRenderer>` factory. The five built-ins are seeded by default;
applications can register/replace/remove/clear entries and select one through
`ApplyTheme(key)`. `ActiveThemeKey` is the persisted identity. The legacy
`Theme` enum remains a compatible shortcut for built-ins and is not falsified
when a custom key is active.

**Theme is a first-class property on the manager** (§7) — everything routes
through the color table, so text/checks stay legible per theme automatically.
*Known cosmetic:* the sample SVG icons are drawn for light backgrounds, so some
look low-contrast on Dark (icon issue, not chrome).

---

## 6. Design-time support (`CommandBars.Design` + the Designer.* assemblies)

**Model:** the runtime bars are command-backed and wired in code, which the VS
designer can't edit directly. So a **serializable definition layer** is edited in
the designer and realized at runtime:
- `BarDefinition` (+ `ToolbarDefinition`/`MenuBarDefinition`), `ItemDefinition`
  (+ Button/Toggle/Split/Popup/Separator/Label/ComboBox subclasses). Each carries
  `Name`, `Text`, `CommandId`, `ImageKey`, `ImagePath`, `DisplayStyle`,
  `ComboWidth`, `ComboItems`, etc. Popup and Split definitions also expose
  `TearOff`, optional `TearOffTitle`, and `PaletteColumns`: zero columns produces
  an AutoShapes-style detachable linear menu, while a positive column count
  produces a Font Color-style icon grid. `IncludeInCommandList` opts a complete
  compound item (for example the font ComboBox or AutoShapes popup hierarchy)
  into runtime customization. A Popup's mutually exclusive `ToolbarList` and
  `ThemeList` properties replace authored children with a live checked list of
  every managed toolbar or application-registered theme. The editor
  hides these fields for irrelevant item kinds and hides the tear-off title until
  tear-off is enabled.
- `CommandDefinition` — the **command catalog** entry (§6a).
- `CommandBarManager.BarDefinitions` / `CommandDefinitions` (both Content-serialized)
  + `BuildFromDefinitions()` realize them; `Images` (an `SvgImageList`) resolves
  `ImageKey`.
- `SvgImageList` / `SvgImage` — icons stored **inline as SVG markup** (embedded in
  the designer file, fully portable, no path resolution).
- `DockHost` renders a **live preview** on the design surface (the control's own
  paint).

**The out-of-process designer IS now implemented** (§9). Custom editors/designers
that must run in the VS process are hosted in the **Client** assembly and routed by
name via a **`TypeRoutingProvider`**; `ControlDesigner`s / action lists /
CodeDom-adjacent logic live in the **Server** assembly; transport POCOs and
name-referenced editors live in **Protocol**. Where a framework built-in editor
suffices (e.g. `FileNameEditor`, `MultilineStringEditor`, `StringCollectionEditor`)
it is referenced by its `System.Design` assembly-qualified name, which the
out-of-process designer loads directly.

> **Important out-of-process gotcha (learned the hard way):** modal UI opened
> from the design *server* can deadlock/freeze the designer. This includes native
> `OpenFileDialog`: it may appear to work with some Designer SDK versions, but is
> not reliable inside the synchronous server request. All dialogs (including
> multi-file SVG import, the stock-icon gallery, and the ImageKey picker) run
> **client-side** and are reached through `InvokePropertyEditor` / a routed
> `UITypeEditor`; edits are round-tripped through protocol endpoints.

### 6a. Shared command catalog

To avoid re-entering the same item on both a menu and a toolbar, commands are
authored **once** in `CommandBarManager.CommandDefinitions` (a
`List<Design.CommandDefinition>`: `Id`, `Text`, `ImageKey`, `Shortcut`,
`DisplayStyle`) and referenced from bar items by
`ItemDefinition.CommandId`. At build time `RegisterCatalogCommands()` fills the
registry **non-destructively** — a command already created in code keeps its
text/shortcut/image and, crucially, its `ExecuteHandler`; the catalog only fills
gaps. So the catalog supplies what a command *looks like*, code supplies what it
*does*. The editor dialog exposes the catalog as a **Commands palette**; the
design-preview signature (`ComputeDesignSignature`) includes catalog entries so a
referenced command's text/icon/shortcut change refreshes every item that resolves
to it.

### 6b. Stock icons + ImageKey picker

- **Stock-icon gallery** — a client-side dialog (opened from the `SvgImageList`
  smart-tag verb, routed to the Client assembly) offering 24 colorful built-in
  productivity SVG icons addable to a `SvgImageList`. Each icon has its own
  multicolor artwork; the old monochrome tint palette was removed. The SVG and
  dependency-free PNG gallery thumbnail are embedded resources. The server-side
  data stubs (`StockIcons.cs`,
  `StockIconsGallery.cs`) are retired; the live data lives client-side
  (`StockIconResources.cs`, `StockIconsGallery.cs`, `SvgStockIconsEditor.cs`).
- **ImageKey picker** — `ItemDefinition.ImageKey` / catalog `ImageKey` use a routed
  `ImageKeyEditor` (in Protocol) showing **thumbnails** of the connected
  `SvgImageList` entries. The server renders each entry to a 32px PNG
  (`BarDefinitionMapper.ToImageData`) and ships them in the snapshot; the client
  draws them (no `Svg` dependency on the net472 client).

---

## 7. Persistence (`Persistence/LayoutState.cs`, Version = 2)

Full structural round-trip — bars, items, order, visibility, dock/row/offset, icon
size, `ShowToolTips`, application settings, and the stable active theme key. Item state includes the item
**`Name`**, and for combos **`ComboWidth`** + **`ComboItems`** (selected value
stored first so it re-selects on load). `CommandBarManager.SnapshotItems` /
`BuildItem` capture and rebuild these; a rebuilt combo comes back populated with
its width and selection (a bare `new CommandBarComboBox()` was the old "frozen
combo" bug).

`SaveLayout`/`LoadLayout`; the code-built demo auto-saves on exit and auto-loads on
start (`commandbars.json` in the output folder). **Because `LoadLayout` overrides
code-built bars with saved state, a stale `commandbars.json` from before a schema
change will resurrect old data — delete it once after changing the persisted
shape.** `ResetToDefaults` preserves settings. `PackageDemo` now mirrors this
behavior using `package-demo-commandbars.json`; delete that file after a persisted
schema/layout change when you need a clean designer-authored default layout.

Open tear-off palettes are persisted by their stable dropdown name and screen
position. Restore is deferred until the first DockHost handle exists; layout load
normally runs in the form constructor, where an immediate `BeginInvoke` would
otherwise fail and silently lose the palettes. Dynamic `ToolbarList` and
`ThemeList` children are never stored—the current registry is regenerated each
time that popup opens. An unknown saved theme key leaves the current safe renderer
in place and is retained pending late application registration.

---

## 8. Demos

- **`CommandBars.Demo/MainForm.cs`** — everything built in code: menu bar,
  Standard/Formatting/Navigation/Paragraph toolbars, split buttons, a **font combo**
  on the Formatting toolbar (`font.combo`, 5 fonts, `SelectedItemChanged` → status
  bar), a manager-owned dynamic View > Theme menu (persisted), Customize dialog, icon-size menu, DPI
  PerMonitorV2. Its Font combo and complete tear-off AutoShapes hierarchy are
  registered as reusable customization items, and View > Toolbars is a dynamic
  `ToolbarList`; View > Theme similarly uses `ThemeList`, matching the
  designer-authored package demo. Uses
  `LoadLayout`/`SaveLayout`.
- **`DesignerDemoForm` (+ .Designer.cs)** — bars defined via the designer's
  `BarDefinitions`; icons embedded in an `SvgImageList` referenced by `ImageKey`;
  runtime `RegisterCommands()` + `BuildFromDefinitions()`. Hand-authored
  `InitializeComponent` uses **one unique local per object** (the VS CodeDom parser
  aliases by variable name — reusing a local collapses the collection). The user
  has locally edited this `.Designer.cs`; do not regenerate/clobber without asking.
- **`CommandBars.PackageDemo`** — consumes the library **as a NuGet package** (the
  real out-of-process designer only activates through the package, not a project
  reference). It mirrors the code-built demo with designer-owned Standard,
  Formatting, Navigation, Paragraph, and Drawing bars, Font Color's 40-swatch
  grid, nested tear-off AutoShapes galleries, themes, icon sizes, Customize,
  toolbar visibility, shortcuts, and layout persistence. Runtime code supplies
  handlers only.

---

## 9. Out-of-process designer apparatus (IMPLEMENTED)

Per Microsoft's "Custom Controls for WinForms' Out-Of-Process Designer" model, the
design-time code is split so the VS designer can actually load it:

1. **Runtime library** (`CommandBars`, net8.0-windows) — controls only; references
   design types **by string** in `[Designer(...)]`/`[Editor(...)]`, no compile
   dependency. E.g. the manager carries
   `[Designer("CommandBars.Designer.Server.CommandBarManagerDesigner, CommandBars.Designer.Server")]`.
2. **`CommandBars.Designer.Server`** (net8.0-windows) — `ControlDesigner`s
   (`CommandBarManagerDesigner`, `DockHostDesigner`, `SvgImageListDesigner`),
   `DesignerActionList` verbs, endpoint **handlers**
   (`BarDefinitionsHandlers`, `AddStockIconsHandler`), and the
   `BarDefinitionMapper` (runtime ⇄ transport, plus PNG thumbnail rendering).
   References `Microsoft.WinForms.Designer.SDK`.
3. **`CommandBars.Designer.Client`** (net472 — matches the VS host) — the
   `UITypeEditor`s, modal dialogs (`BarDefinitionsDialog`, stock-icon gallery,
   SVG markup editor), and a **`TypeRoutingProvider`** mapping editor names to
   client types (without it the client can't bind the server-side definitions).
4. **`CommandBars.Designer.Protocol`** (multi-target **net8.0-windows;net472**) —
   transport POCOs shared by client & server: `BarDefData`/`ItemDefData`,
   `CommandDefData`, `DesignSnapshot { Bars, Commands, Images }` +
   `ImageEntryData { Key, Png }`, `StockIconData`, the `Endpoints/*` request
   classes, `ImageKeyEditor`, `DefinitionsSerializer`. **Transport is a JSON
   string** (`System.Text.Json`) rather than nested `IDataPipeObject`, which proved
   far simpler to round-trip.

**Packaging:** the runtime library packs to `lib/net8.0/…` with the design assets
under `lib/net8.0/Design/WinForms/` (client, net472) and
`lib/net8.0/Design/WinForms/Server/` (server, net8). The package TFM is kept
**neutral `net8.0`** (not `net8.0-windows`) so the `lib` folder is `lib/net8.0/`
and NuGet doesn't reject it for a missing platform version. `PackageDemo` consumes
it from a local feed. Central `WinFormsDesignerSdkVersion` set in the build props
fixed an SVG-import designer freeze.

> Some `CommandBars/Design/*` types (`CommandBarManagerDesigner`,
> `DockHostDesigner`, `CollectionEditors`, `SvgImageEditor`, `SvgImageDesign`)
> remain as in-process fallbacks / string targets. Prefer the Designer.* assemblies;
> treat the in-process `Design/*` designers as fallback only.

---

## 10. Planned redesign: catalog-first design-time workflow

### 10.1 Status and objective

This section is the approved implementation plan. **Stages 1-6 are implemented
on the `codex/catalog-first-designer` branch; Stages 7-8 remain pending.** The plan
replaces the current permissive design-time workflow in which an author can
independently create an item, optionally bind it to a catalog command, or leave
its `CommandId` blank and let the manager synthesize a third definition.

The redesigned workflow has one governing rule:

> Reusable behavior and presentation are authored once in the manager's catalog;
> bars and dropdowns contain placements that reference catalog entries.

The Visual Studio designer should guide the author through this order:

1. Define an entry in the manager's command catalog.
2. Select a toolbar, menu bar, popup, or split-button dropdown.
3. Add one or more existing catalog entries to that target.
4. Reorder placements and add structural separators where needed.

The manager editor remains the complete editing surface, but is divided into
separate **Commands** and **Bars and Menus** pages. The form designer gains
`DockHost` and, if feasible, per-preview-bar actions for the common operations.

### 10.2 Problems this redesign must solve

- `ItemDefinition` currently duplicates `Text`, `ImageKey`, `Shortcut`, and
  `Kind` even when it references a `CommandDefinition`.
- The manager dialog's prominent **Add Item** menu makes creating an unbound item
  easier than placing an existing command.
- A blank `CommandId` silently creates a synthesized runtime command, so a single
  logical function can accidentally exist as a menu item, a toolbar item, and a
  catalog command with three unrelated identities.
- Popup and split-button children are currently owned by each item placement,
  which prevents a compound definition from being reused safely.
- Preview bars inside a `DockHost` are dynamically realized, unsited controls.
  They render on the design surface but Visual Studio cannot select them as
  ordinary designer components or automatically provide a smart tag for each.

### 10.3 Terminology and ownership

The redesign separates the following concepts:

| Concept | Owns | Does not own |
| --- | --- | --- |
| Catalog entry | Stable id, semantic kind, caption, icon, shortcut, default presentation, and kind-specific configuration | Dock position, overflow priority, or placement order |
| Command placement | Catalog id plus location-specific display style, name, visibility, grouping, and overflow priority | Independent command text, image, shortcut, or freely editable semantic kind |
| Structural placement | A separator, and if retained, a static label | Executable behavior or a command id |
| Bar definition | Bar identity, type, dock edge, bar options, and an ordered placement list | Copies of catalog command definitions |

`CommandDefinition` remains the public name for compatibility, but its design-time
meaning expands from an atomic action descriptor into a reusable catalog entry.
Documentation and UI may call the complete collection the **Command Catalog**.

### 10.4 Proposed catalog model

Add a catalog-kind enum (working name `CommandDefinitionKind`) with these values:

- `Action` — a normal executable command.
- `Toggle` — an executable command with shared checked state.
- `Popup` — a reusable dropdown whose ordered contents reference other catalog
  entries.
- `SplitButton` — reusable dropdown contents plus an optional
  `PrimaryCommandId` reference to a separately reusable Action/Toggle; empty
  uses the split entry's own id as its executable command.
- `ComboBox` — a reusable hosted selector with width, initial entries, image,
  label, and a stable synchronization identity.
- `Label` — optional non-executable reusable text. Retain this only if static
  labels are valuable enough to appear in the catalog; otherwise keep labels as
  an explicitly named structural placement.

The catalog owns the properties that describe the function everywhere it is
used:

- `Id`, `Kind`, `Text`, `ImageKey`/`ImagePath`, and `Shortcut`;
- default `DisplayStyle`;
- toggle defaults where applicable;
- combo width and initial combo entries;
- popup/split dropdown placements;
- tear-off title, palette columns, and authored/dynamic content source;
- inclusion in the runtime Customize palette.

Replace the mutually exclusive popup booleans with one enum-like content source
where practical: `Authored`, `ToolbarList`, or `ThemeList`. This prevents invalid
mixed states and leaves room for future application-provided dynamic sources.

Popup and split entries own an ordered list of **placements**, not independent
child commands. A child placement references another catalog id or represents a
separator. Nested popup entries therefore create a reusable graph:

```text
file.new            Action
file.open           Action
file.save           Action
app.exit            Action
file.menu           Popup
  -> file.new
  -> file.open
  -> file.save
  -> separator
  -> app.exit
```

Two dropdowns that intentionally contain different commands are two distinct
compound catalog entries, but both reuse the same atomic actions. That is
composition, not duplicated command behavior.

### 10.5 Proposed placement model

Introduce a lightweight definition (working name `CommandPlacementDefinition`)
used by both `BarDefinition.Items` and compound catalog contents. It contains:

- placement kind: catalog reference or separator (and possibly static label);
- `CommandId` for a catalog reference;
- optional stable `Name` for lookup and persistence;
- `Visible`, `BeginGroup`, and `Priority`;
- an optional `DisplayStyle` override, with an explicit `UseCatalogDefault`
  state so the catalog default can change without rewriting every placement.

It must not expose independently editable command text, image, shortcut, combo
contents, dropdown children, or semantic item kind. The referenced catalog entry
determines those values.

Target compatibility is validated before insertion and during snapshot rebuild:

| Target | Allowed entries |
| --- | --- |
| Menu-bar root | Popup entries |
| Toolbar | Action, Toggle, Popup, SplitButton, ComboBox, and Label if retained |
| Popup contents | Action, Toggle, Popup, Label if retained, and separator |
| Split dropdown contents | Same as popup contents |

Split buttons and combo boxes should initially be rejected inside popup menus;
support can be added later only if their menu semantics are deliberately defined.

The runtime fluent API remains available for fully code-built applications. The
catalog-first restriction applies to the Visual Studio authoring workflow; it
does not remove the ability to construct runtime bars and items in code.

### 10.6 Identity and integrity rules

The editor and build pipeline must enforce these rules:

- Catalog ids are non-empty and unique using ordinal comparison.
- Renaming an id is an atomic refactoring that updates every bar and compound
  placement in the same design snapshot.
- Removing a referenced entry requires a usage warning. The author can cancel or
  remove the entry and all its placements; silent dangling references are not
  allowed.
- Unknown references loaded from source are retained and displayed as errors so
  the author can repair them; they are never silently discarded.
- A popup/split entry cannot directly or indirectly contain itself. Validate the
  reference graph and report the complete cycle path.
- The same catalog entry may be placed multiple times intentionally.
- Stable placement names are unique where runtime lookup or combo synchronization
  requires it. Combo synchronization should use the canonical catalog identity
  by default, with placement names reserved for locating a particular instance.
- Validation errors block **OK** in the editor; non-fatal compatibility warnings
  remain visible but may be accepted.

### 10.7 Designer experience

#### Commands page

The manager dialog's **Commands** page provides:

- a searchable catalog list, optionally grouped by category later;
- **Add Action**, **Add Toggle**, **Add Popup**, **Add Split Button**,
  **Add Combo Box**, and optionally **Add Label**;
- remove, rename-id, and duplicate operations;
- a property grid filtered by catalog kind;
- a usage summary such as `Used in 3 locations`, with navigation to each use;
- for Popup and SplitButton, a child-composition tree with **Add Commands...**,
  **Add Separator**, remove, and reorder operations;
- validation messages for duplicate ids, missing references, incompatible
  placements, and cycles.

Creating a popup or split entry does not require the entire command catalog to
become a permanently expanded tree. Its child-composition tree appears only when
that compound entry is selected.

#### Bars and Menus page

The manager dialog's **Bars and Menus** page provides:

- the existing tree of bars and their placements;
- **Add Toolbar** and **Add Menu Bar**;
- **Add Commands...**, using the same reusable picker as compound contents;
- **Add Separator**, plus **Add Label** only if labels remain structural;
- remove, move up/down, and later drag-to-reorder;
- a property grid for the selected bar or placement;
- no generic **Add Item -> kind** menu and no editable placement `Kind`.

The command picker supports multi-selection, search, icons, kind labels, and
target-aware filtering. Separators may appear as a special picker row or remain a
nearby **Add Separator** action. If no suitable command exists, **Create New
Command...** may open the Commands page, create the entry there, and return to
the picker; it must not synthesize an anonymous item behind the scenes.

#### Form designer and `DockHost` actions

The first reliable design-surface milestone is a smart tag on each `DockHost`:

- **Add toolbar...** — creates a toolbar whose initial `Dock` matches the host's
  edge.
- **Add menu bar...** — creates a menu bar when valid for that host/manager.
- **Add commands to...** — chooses one of the bars currently previewed in the
  host, then opens the shared command picker.
- **Edit bars and menus...** — opens the manager editor on the layout page.
- **Edit command catalog...** — opens the manager editor on the Commands page.

The second milestone prototypes per-bar hit testing or designer adornment glyphs.
A click or small action glyph on a previewed bar should identify its backing
`BarDefinition` and offer **Add commands...**, **Edit toolbar...**, and **Remove
toolbar**. The preview `CommandBarControl`s must remain unsited implementation
details; converting them into serialized form components would create duplicate
ownership and is not part of this plan.

All modal UI must continue to execute client-side in Visual Studio. The design
server may identify the manager, host edge, target bar, and snapshot, but must use
the existing routed editor/protocol pattern rather than opening dialogs in the
server process. If per-bar glyph support is unreliable in the current Designer
SDK, the `DockHost` smart tag with an explicit target-bar chooser is the supported
fallback and is sufficient to complete the workflow.

### 10.8 Compatibility and migration

This redesign must not silently reinterpret or destroy existing designer-authored
forms. Use a versioned design snapshot and a one-way legacy import before removing
the old authoring UI.

Migration rules:

1. An old item with a valid `CommandId` becomes a placement of that catalog
   entry. Placement properties such as priority, visibility, grouping, and
   display style are retained.
2. An old command-bound item whose kind conflicts with the catalog kind produces
   an explicit migration diagnostic. The importer must not guess whether a
   button, toggle, or compound item was intended.
3. An old item with a blank `CommandId` receives a deterministic catalog id based
   on its stable name or structural path. Its command-owned properties are moved
   into a new catalog entry and the old item becomes a reference placement.
4. An old Popup becomes a Popup catalog entry. Its child tree is recursively
   converted into catalog references and separators.
5. An old SplitButton becomes a SplitButton catalog entry. Conflicts where the
   same old command id was also used as a simple action are diagnosed and resolved
   by creating a distinct compound id rather than changing every use silently.
6. ComboBox configuration moves to a ComboBox catalog entry. Existing stable
   names are preserved as aliases during migration so saved layout and code lookup
   do not abruptly break.
7. `ToolbarList` and `ThemeList` map to the new single popup content source.
8. Legacy `ItemDefinition` objects remain readable for at least one compatibility
   cycle. Mark obsolete authoring properties as hidden from the new designer
   before considering any public API removal.

The migration preview should list every created catalog entry, renamed identity,
and unresolved conflict. Saving after a successful conversion emits only the new
schema. Canceling leaves the original designer serialization untouched.

### 10.9 Staged implementation plan

Each stage must build and test independently. Do not combine the model migration
and the design-surface glyph experiment into one change.

#### Stage 1 — Expand the catalog model

**Status: implemented (2026-09-04).** The runtime and protocol now represent
Action, Toggle, Popup, SplitButton, ComboBox, and Label entries; compound entries
own lightweight catalog-reference/separator lists; `CreateCatalogItem` performs
cycle-safe materialization; catalog combo copies synchronize by canonical id;
and catalog-owned compound Customize factories take precedence over legacy item
factories. The current legacy editor deliberately places Action and Toggle only
until the catalog-first layout editor is implemented.

**Work**

- Add `CommandDefinitionKind` and kind-specific catalog properties.
- Add compound child-reference storage for Popup and SplitButton.
- Add a single popup content-source representation.
- Add catalog materialization that creates the correct runtime item for each
  semantic kind while preserving shared runtime `Command` state.
- Define combo identity/synchronization behavior around catalog ids.

**Acceptance**

- Unit tests materialize every catalog kind.
- Two placements of an action/toggle share command state.
- Two combo placements share selection/enabled state as specified.
- Popup and split entries recursively build their referenced contents.
- Dynamic Toolbar List and Theme List behavior remains intact.

#### Stage 2 — Introduce lightweight placements

**Status: implemented (2026-09-04).** `BarDefinition.Placements` now uses the
same `CommandPlacementDefinition` references as compound catalog contents.
`CommandBarManager.BuildFromDefinitions` materializes canonical and legacy
items together during the compatibility period. Shared placement rules enforce
the menu-bar/toolbar/dropdown kind matrix and are available to the future picker.
Catalog-owned command presentation and semantic kind refresh across rebuilds,
while application-created commands retain their presentation and handlers. The
designer protocol/server mapping preserves top-level placements even though the
legacy tree does not edit them yet.

**Work**

- Adopt the Stage 1 `CommandPlacementDefinition` for top-level bars as well as
  compound contents.
- Move command-owned data out of new placements.
- Implement catalog-default versus placement-override display style.
- Add target compatibility checks to insertion and build paths.
- Keep an adapter capable of building old `ItemDefinition` data.

**Acceptance**

- Editing catalog text, image, shortcut, or kind updates every preview placement.
- A new placement cannot diverge into an independent command definition.
- Separators round-trip at bar and nested-dropdown levels.
- Priority, visibility, begin-group, name, and display override persist per
  placement.

#### Stage 3 — Validation, reference refactoring, and migration

**Status: implemented (2026-09-04).** The shared protocol now provides
snapshot-wide diagnostics, usage indexing, atomic id rename, guarded/cascading
removal, target and cycle validation, schema versioning, and deterministic
non-mutating legacy migration plans with change reports. Migration converts old
full item trees into catalog entries and placements while preserving popup,
tear-off, dynamic-list, combo, image-key/path, customization, and split-primary
behavior. Split entries gained an optional `PrimaryCommandId` so a reusable
split composition can invoke an existing atomic action without stealing that
action's catalog identity. The current client editor uses the service for
refactoring, guarded deletion, and save validation; migration preview/acceptance
UI remains part of Stage 4.

**Work**

- Build a snapshot-wide reference index and validation service shared by client
  and server logic where possible.
- Implement atomic id rename, usage lookup, guarded removal, cycle detection,
  missing-reference diagnostics, and target compatibility diagnostics.
- Version the protocol snapshot and implement legacy conversion.
- Provide deterministic ids and a migration report for anonymous old items.

**Acceptance**

- Tests cover duplicate ids, rename propagation, guarded deletion, dangling
  references, direct/indirect cycles, incompatible targets, and deterministic
  migration.
- Existing PackageDemo definitions migrate without losing hierarchy, images,
  tear-offs, dynamic lists, combos, or command sharing.
- Canceling migration causes no serialized change.

#### Stage 4 — Redesign the manager dialog

**Status: implemented (2026-09-04).** The client-side manager editor now has
separate **Commands** and **Bars and Menus** pages with a shared, DPI-aware
property panel. Commands can be searched, created by semantic kind, duplicated,
removed with usage protection, and composed through a reusable target-filtered
multi-select picker. Popup/split contents and bar contents both edit canonical
placements; placement identity and kind are protected from direct property-grid
changes. Usage summaries navigate to bar or compound references, live validation
is visible throughout the dialog, and invalid snapshots remain blocked at OK.
Legacy full-item trees open an explicit dry-run migration preview and mutate the
working snapshot only after **Apply Migration**. The property panel, picker icon
size, and modal forms rescale on DPI changes, including cross-monitor moves.

**Work**

- Replace the vertical tree/palette split with Commands and Bars and Menus pages.
- Build the reusable, target-filtered multi-select command picker.
- Add the compound-entry child editor and usage navigation.
- Bind the property grid to catalog, bar, and placement descriptors with only
  relevant properties exposed.
- Show validation state and prevent committing invalid snapshots.

**Acceptance**

- A user can create each catalog kind and compose nested dropdowns without
  manually editing ids.
- A user can place one command in multiple bars/menus and observe one canonical
  definition.
- Every operation occurs client-side without freezing the out-of-process
  designer.

#### Stage 5 — Enforce catalog-first authoring

**Status: implemented (2026-09-04).** The manager's two-page editor is now the
single normal authoring surface for both the catalog and placements. Raw runtime
`CommandDefinitions`, legacy `ItemDefinition` trees, and canonical placement
collections remain public and content-serialized for code/loader compatibility,
but are hidden from PropertyGrid collection authoring. Placement kind is hidden
and command identity is read-only wherever runtime placement metadata is shown.
The obsolete legacy item collection editor is no longer advertised. A separate
catalog-first validation boundary promotes legacy-tree warnings to errors in the
editor save and design-server commit paths, while ordinary validation still lets
the migration preview inspect and convert old source safely.

**Work**

- Remove generic **Add Item** and placement-kind editing from the new UI.
- Expose only **Add Commands...** and structural insertion operations.
- Remove designer creation paths that synthesize anonymous commands.
- Retain runtime fluent construction and the legacy loader.

**Acceptance**

- No normal designer path can create an unbound button, toggle, popup, split
  button, or combo box.
- Adding the same function to a menu and toolbar creates two placements with one
  catalog id.
- Source-loaded legacy anonymous items are shown as migration work, not silently
  duplicated.

#### Stage 6 — Add `DockHost` smart-tag workflow

**Status: implemented (2026-09-04).** Every out-of-process `DockHost` designer
now exposes host-aware smart-tag actions for **Add toolbar...**, conditionally
**Add menu bar...**, **Add commands to...**, **Edit bars and menus...**, and
**Edit command catalog...**. Hidden non-serialized routed properties hand each
action to a Visual Studio client-side editor. A host-context protocol returns the
connected manager snapshot and edge; direct creation uses that edge as the new
bar's initial dock, and command placement chooses only visible bars previewed by
that host before reusing the Stage 4 target-filtered picker. Disconnected hosts
show guidance, menu creation is limited to a top host with no existing menu bar,
and legacy snapshots must pass the migration preview before direct mutation.
Manager- and host-routed saves share one validation/transaction implementation,
notify both serialized collections within one undo unit, and refresh every host
registered with the manager after commit.

**Work**

- Extend `DockHostDesigner` with the host-level actions described in §10.7.
- Resolve the connected manager and bars safely at design time.
- Route all editors through client-side protocol endpoints.
- Refresh every affected host preview after a committed edit.

**Acceptance**

- A toolbar or menu bar can be created from the relevant container without first
  selecting the manager component tray icon.
- Commands can be added to a chosen bar from the container smart tag.
- Undo/redo and designer change notifications treat each committed operation as
  a coherent transaction.

#### Stage 7 — Prototype direct per-bar actions

**Work**

- Evaluate Designer SDK hit testing and `BehaviorService`/glyph support for the
  unsited preview controls.
- Add a small per-bar action glyph or point-based bar selection if reliable.
- Reuse the Stage 4 picker; do not create a second editing implementation.
- Preserve the host smart-tag target chooser as the fallback.

**Acceptance**

- Clicking the supported affordance identifies the correct bar on all four dock
  edges and at common DPI scales.
- Actions edit the backing definition rather than the temporary preview control.
- If SDK limitations prevent a stable implementation, document the limitation
  and ship the completed host-level workflow without restructuring runtime bars
  as designer components.

#### Stage 8 — Complete integration and release hardening

**Work**

- Update runtime realization, persistence, designer protocol/server/client
  mapping, property filtering, Customize factories, demos, and package assets.
- Add end-to-end designer tests where automatable and focused unit tests for all
  model/mapping behavior.
- Refresh README and setup documentation with the catalog-first workflow.
- Rebuild the local package, update PackageDemo's exact version, restore, build,
  and smoke-launch both demos.

**Acceptance**

- Library and all designer assemblies build cleanly.
- All existing runtime tests plus new catalog/placement/migration tests pass.
- Code-built Demo behavior is unchanged.
- Designer-authored PackageDemo uses only catalog references and structural
  placements, retains feature parity, and can be edited through both the manager
  and `DockHost` workflows.
- Saving, closing, and reopening the form produces no designer serialization
  churn when no semantic change was made.

### 10.10 Decisions to preserve during implementation

- The catalog is the single source for reusable command presentation and compound
  structure.
- Separators are placements, not commands.
- Bars and compound dropdowns use the same placement/reference abstraction.
- Form-designer actions and the manager dialog use the same client-side command
  picker and snapshot mutation services.
- Preview bars remain virtual/unsited; their backing definitions are edited.
- Legacy definitions are migrated explicitly and remain readable during a
  compatibility period.
- Runtime code-first construction remains supported.
- Direct per-bar interaction is desirable but not allowed to block the reliable
  `DockHost` smart-tag workflow.

---

## 11. Roadmap (remaining)

- **Alt/F10 menu activation** — enter the menu bar via Alt or F10 with the first
  item highlighted, arrows between menus, Esc exits.
- **Accessibility (UIA)** — AccessibleObject roles/names, keyboard-focus events.
- **RTL support** — mirror bars/menus/chevron/drop positions.
- Housekeeping: refresh `README.md` (still says Phase 2), unit tests for the newer
  layers, optional per-theme icon tint for Dark.

---

## 12. Recent-session change log

Most recent first. Verify against `git log`/`git diff` in a new session.

- **Catalog-first redesign Stage 6.** Added client-routed `DockHost` smart-tag
  actions for creating edge-docked bars, placing commands into a selected hosted
  bar, and opening either manager-editor page. Added host context/commit protocol
  endpoints, safe disconnected/invalid-target handling, migration gating, one
  shared designer transaction, and manager-wide preview refresh. Routed action
  properties are hidden and never serialized.
- **Catalog-first redesign Stage 5.** Removed remaining normal designer paths
  for raw item/placement collection creation, hid the standalone catalog
  collection in favor of the manager editor, protected runtime placement
  identity metadata, and added strict catalog-first validation at both client
  save and server commit boundaries. Legacy item APIs, content serialization,
  loading, migration, and runtime construction remain intact.
- **Catalog-first redesign Stage 4.** Replaced the mixed tree/palette manager
  dialog with Commands and Bars and Menus pages, a shared target-filtered command
  picker, compound-content editing, usage navigation, live diagnostics, explicit
  legacy-migration preview, and kind-filtered property descriptors. The shared
  property panel reserves a logical DPI-scaled width and relayouts on monitor DPI
  changes; picker thumbnails and all new modal forms are DPI-scaled as well.
- **Catalog-first redesign Stage 3.** Added shared snapshot validation and
  diagnostics, usage lookup, atomic command-id refactoring, guarded/transitive
  removal, schema versioning, deterministic dry-run migration with reports, and
  server-side commit validation. Legacy full-item trees migrate to canonical
  catalog placements without mutating the source. Added split
  `PrimaryCommandId` and catalog `ImagePath` support to preserve existing
  handlers and icons. The current client now refactors property-grid ID edits,
  warns before deleting used commands, and blocks invalid saves.
- **Catalog-first redesign Stage 2.** Added canonical catalog placements to bar
  definitions, shared target-compatibility rules, manager-owned materialization
  for menu bars/toolbars/dropdowns, placement-level display and layout overrides,
  protocol/server round-tripping, catalog-owned presentation refresh, and
  compatibility building for old `ItemDefinition` collections. Added tests for
  mixed legacy/canonical bars, target rejection, command ownership, kind changes,
  and top-level placement serialization.
- **Catalog-first redesign Stage 1.** Expanded `CommandDefinition` into reusable
  semantic Action/Toggle/Popup/SplitButton/ComboBox/Label entries, added
  lightweight child references and one popup content-source enum, implemented
  cycle-safe runtime materialization through `CreateCatalogItem`, canonical combo
  synchronization and compound Customize factories, extended the cross-process
  protocol/mapper, and added focused model/protocol tests. The legacy editor
  guards against flattening compound entries while the later two-page editor is
  pending.
- **Combo hover/press + focus-flicker fix.** `ComboDropDown.cs` rewritten to be
  **non-activating and owner-drawn** (no focusable child → owner form keeps focus;
  hover-follow + wheel scroll). `CommandBarControl.cs`: combo closed-box now draws
  hover (`_hotCombo`) and pressed/open (`_openCombo`) states with themed borders;
  press repaints on mouse-down, clears on release/close.
- **Combo persistence.** `LayoutState.ItemState` gained `Name`, `ComboWidth`,
  `ComboItems`; `CommandBarManager.SnapshotItems`/`BuildItem` round-trip them so a
  saved combo reloads populated (was the "frozen combo" bug). `MainForm` restored
  the code-built `font.combo` example.
- **`CommandBarManager.cs` stale-overwrite incident.** A stale snapshot (missing
  `CommandDefinitions`/`RegisterCatalogCommands`) was accidentally written back,
  breaking `CommandBars.Designer.Server`. Recovered by re-applying the combo edits
  onto the `git HEAD` copy. (See §0 — this is why the mtime guard + re-stage rules
  exist.)
- **Combo editor + model.** Reduced combo box height; `ComboItems` edited via
  built-in `StringCollectionEditor`; combo reachable/writable from code
  (`FindComboBox`); menu-toggle now repaints the toolbar immediately
  (Command.PropertyChanged subscription); menu hover-box spacing evened.
- **ImageKey picker** (`ImageKeyEditor`, thumbnails from server-rendered PNGs).
- **Stock-icon gallery** moved **client-side** (server-side modal froze the
  designer) + **colour tinting**.
- **Shared command catalog** (`CommandDefinitions` + palette + full-row tree rows).
- **TFM simplification** — library `net8.0-windows` without explicit platform
  version; package TFM neutral `net8.0`.
- **Out-of-process designer** Server/Client/Protocol split implemented (§9);
  net472 fixes: removed `[AllowNull]`, Client `LangVersion 10.0`.
- Earlier: themes (2010, Dark) + manager `Theme` property; chevron Office-nested
  menu + `CustomizeRequested`; icon-less button shows text; Customize dialog DPI
  autosize.
