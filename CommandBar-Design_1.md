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
  into space released by overflowed items.
- **`CommandBarPopupWindow`** — non-activating dropdown menu; icons, shortcuts,
  checks (orange check box, hidden on hover — hover box is
  `Rectangle(3, b.Y, b.Width-6, b.Height-1)` so the selection has an extra pixel
  of breathing room at both horizontal edges while spacing above/below the check box
  is even), mnemonic activation.
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

## 10. Roadmap (remaining)

- **Alt/F10 menu activation** — enter the menu bar via Alt or F10 with the first
  item highlighted, arrows between menus, Esc exits.
- **Accessibility (UIA)** — AccessibleObject roles/names, keyboard-focus events.
- **RTL support** — mirror bars/menus/chevron/drop positions.
- Housekeeping: refresh `README.md` (still says Phase 2), unit tests for the newer
  layers, optional per-theme icon tint for Dark.

---

## 11. Recent-session change log

Most recent first. Verify against `git log`/`git diff` in a new session.

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
