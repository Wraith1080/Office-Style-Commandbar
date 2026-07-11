# CommandBars — Design & Handoff Document

A reusable **WinForms control library (.NET 8, `net8.0-windows`)** reproducing the
classic Microsoft Office **CommandBars** experience: dockable/floating bars,
runtime themes, DPI-awareness, vector (SVG) icons, full runtime customization,
keyboard navigation, JSON persistence, and Visual Studio design-time support.

This document is the single source of truth for project state. It supersedes the
stale "Phase 2" status in `README.md`.

---

## 1. Current status

**Runtime: feature-complete for v1.** Menu bar + toolbars render, dock to all
four edges, float/undock, theme live, customize interactively, persist, and
navigate by keyboard.

**Design-time: functional; out-of-process designer support STAGE 1 DELIVERED
(unverified).** Dropping `CommandBarManager` + `DockHost`s on a form renders a
live preview of the bars, icons can be assigned via SVG, and the theme is set
from the Properties window. The §9 plan is no longer parked: the Designer.Server
/ Package / PackageDemo apparatus now exists (see §9 status + `DESIGNER-SETUP.md`
for build & verify steps). Smart tags, "Import SVG files…", and live IconSize
refresh should work once verified in the user's VS; typed Add-dropdowns and the
client-side SVG picker dialog remain stage 2/3.

### Build / test constraints (important for any assisting agent)
- The library **cannot be compiled in the assistant's Linux sandbox** (WinForms /
  `net8.0-windows`). Verification is **structural only** (brace/paren/bracket
  balance) before delivery; the user builds & runs on Windows and reports back.
- Design-time code is untestable in the sandbox and version-sensitive in VS —
  expect iterative back-and-forth.

### Delivery workflow
- Files are delivered to the user via the chat, and — when the **device bridge**
  is online — written directly into the project folder.
- **Device:** `desktop-jhgvbtf`
- **Project folder:** `C:\Users\Rahmat Irfan\Claude\Projects\Professional Office Style Commandbar - Winform`
- The bridge has been intermittently offline; when it drops, files are attached
  to the chat for manual placement. Starting a fresh chat can re-trigger the
  bridge permission prompt.

---

## 2. Solution layout

```
CommandBars/            (class library, AssemblyName=CommandBars, net8.0-windows)
  Model/                object/action model
  Rendering/            themes (renderer + color table per theme)
  Controls/             the WinForms controls
  Imaging/              image sources (SVG/raster) + SvgImageList
  Persistence/          JSON layout state
  Design/               design-time types (see §6 caveat)
CommandBars.Demo/       runnable WinExe demo
  MainForm.cs           code-built demo (all features)
  DesignerDemoForm.*    designer-built demo (bars via BarDefinitions)
  Icons/                12 sample .svg files (Content, copied to output)
  DemoSvgIcons.cs       embedded SVG markup for the code-built demo
```

`CommandBars.csproj`: `net8.0-windows`, `UseWindowsForms`, `Nullable=enable`,
`ImplicitUsings=enable`, `GenerateDocumentationFile` (NoWarn CS1591), PackageRef
`Svg` 3.4.7.

---

## 3. Object model (`CommandBars.Model`)

- **`Command`** — the action behind items. `INotifyPropertyChanged`; `Id`, `Text`
  (with `&` mnemonic), `Image` (`IImageSource`), `Shortcut` (`Keys`), `Enabled`,
  `Checked` (tri-state), `IsCheckable`, `ToolTip`; `ExecuteHandler`/`CanExecuteHandler`
  delegates + `Executing`/`Executed` events; `Perform()`; static `RemoveMnemonic`,
  `FormatShortcut`.
- **`CommandRegistry`** — id→Command store; `Register`, `GetOrAdd`, `TryGet`
  (indexer throws — always use `TryGet`).
- **`CommandBarItem`** (abstract) → `CommandBarCommandItem` (holds a `Command`):
  `CommandBarButton`, `CommandBarToggleButton`, `CommandBarSplitButton` (has
  `DropDown` popup). Non-command items: `CommandBarPopupItem` (own `Text`+`Image`+
  `DropDown`), `CommandBarSeparator`, `CommandBarLabel`, `CommandBarComboBox`.
  `DisplayStyle` = ImageOnly/TextOnly/ImageAndText.
- **`CommandBar`** — one bar (MenuBar/Toolbar/Popup). `Name` (stable id, immutable),
  `Text`, `Items`, `Dock` (`DockState`), `Visible`, `IconSize`, `AllowFloat`,
  `AllowCustomize`, `Locked`, `Row`/`Offset`, `FloatingBounds`, `Orientation`
  (derived: popups & Left/Right docks are Vertical).
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
  `ProcessShortcut`. **Design-time additions:** `BarDefinitions`, `Images`,
  `BuildFromDefinitions()`, `EnsureDesignBars()`/`RefreshDesignPreview()`,
  **`Theme`** property + `Renderer` + `ThemeChanged` (see §7).
- **`DockHost` : Panel** — one dock band per edge (`Edge`, `Manager`, `Renderer`).
  Horizontal edges stack rows; vertical edges stack columns. Cross-edge drag,
  float/redock, drop preview. Renders live design preview from `BarDefinitions`.
- **`CommandBarControl` : Control** — a single bar: layout, paint, hover/press,
  overflow chevron + flyout (Office-nested: *Add or Remove Buttons ▸ {toolbar} ▸
  item checklist + Reset Toolbar*, then *Customize…*), split-button two-region
  render, tooltips, keyboard focus (Tab/arrows/Enter/Esc), Alt-gated mnemonics
  (GetAsyncKeyState-polled underline cue). An icon-less command falls back to
  showing its **text** (both horizontal & vertical).
- **`CommandBarPopupWindow`** — non-activating dropdown; icons, shortcuts, checks
  (orange check box, hidden on hover), mnemonic activation.
- **`MenuSession` : IMessageFilter** — closes popup chains on outside click;
  keyboard nav + mnemonics within open menus.
- **`FloatingWindow`**, **`DropPreviewWindow`**, **`DockEdge`/`DockDragSession`**,
  **`BarLayoutEngine`** (H & V measure/layout), **`BarMetrics`** (DPI metrics).
- **`CommandsPalette`** — drag-source list of commands for the Customize dialog.
- **`CustomizeDialog`** — non-modal, 4 tabs (Toolbars / Menus / Commands /
  Options). Side-button panels autosize; both button groups equalized to their
  widest button in `OnLoad` (DPI-correct). Reset All, per-bar/menu reset.

---

## 5. Rendering / themes (`CommandBars.Rendering`)

`CommandBarRenderer` (abstract) + `CommandBarColorTable` (all chrome colors are
virtual). `Office2003Renderer` is the fully-parameterized base; other themes
subclass it and supply a color table (+ `ChunkRadius`).

Themes: **Office 2003**, **Office XP** (flat, `ChunkRadius 0`), **Office 2007**
(glassy blue), **Office 2010** (flat silver, gold hover), **Dark** (charcoal,
`#007ACC` accent). Enum + factory: `CommandBarTheme` + `ThemeRenderer.Create`.

**Theme is a first-class property on the manager** (see §7) — everything routes
through the color table, so text/checks stay legible per theme automatically.
*Known cosmetic:* the sample SVG icons are drawn for light backgrounds, so some
look low-contrast on Dark (icon issue, not chrome). A future per-theme icon tint
could address it.

---

## 6. Design-time support (`CommandBars.Design`) — and the out-of-process limit

**Model:** the runtime bars are command-backed and wired in code, which the VS
designer can't edit directly. So a **serializable definition layer** is edited in
the designer and realized at runtime:
- `BarDefinition` (+ `ToolbarDefinition`/`MenuBarDefinition`), `ItemDefinition`
  (+ Button/Toggle/Split/Popup/Separator/Label/ComboBox subclasses). Each carries
  `Text`, `CommandId`, `ImageKey`, `ImagePath`, `DisplayStyle`, etc.
- `CommandBarManager.BarDefinitions` (Content-serialized) + `BuildFromDefinitions()`
  realize them; `Images` (an `SvgImageList`) resolves `ImageKey`.
- `SvgImageList` / `SvgImage` — icons stored **inline as SVG markup** (embedded in
  the designer file, fully portable, no path resolution). `SvgImage.Browse` (file
  picker) fills `Svg`; `SvgImage.Svg` edits markup.
- `DockHost` renders a **live preview** of the matching definitions on the design
  surface (this works — it's the control's own paint).

**THE KEY CONSTRAINT — VS's out-of-process WinForms designer will NOT load custom
design-time types from the control assembly.** Confirmed symptoms in the user's
VS: collection editors show a plain **Add** (no typed dropdown), the `SvgImageList`
shows **no smart-tag**, custom `UITypeEditor`s show **no ellipsis**, and the
live-refresh `ControlDesigner` never fires (IconSize updates only on click). Our
`Design/*` designers/editors/collection-editors are therefore effectively inert
in that designer.

**What we did to stay functional anyway — use the framework's BUILT-IN editors**,
which the out-of-process designer *does* load when referenced by their `System.Design`
assembly-qualified name (Microsoft-documented, e.g. `FileNameEditor`):
- `SvgImage.Browse` → built-in **`FileNameEditor`** (real "…" file dialog); the
  property *setter* reads the file and embeds it into `Svg`.
- `SvgImage.Svg` → built-in **`MultilineStringEditor`**.
- `ItemDefinition.ImagePath` → built-in **`FileNameEditor`**.
- `CommandBarManager.Theme` → plain enum property (grid dropdown, serializes) that
  reaches the hosts via the manager's own registry — **no custom designer needed**,
  so it works out-of-process.

Reference strings used:
```
System.Windows.Forms.Design.FileNameEditor, System.Design, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b03f5f7f11d50a3a
System.ComponentModel.Design.MultilineStringEditor, System.Design, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b03f5f7f11d50a3a
System.Drawing.Design.UITypeEditor, System.Drawing, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b03f5f7f11d50a3a
```

> **Dead-but-harmless:** `Design/SvgImageEditor.cs`, `Design/SvgImageDesign.cs`
> (SvgFileImportEditor / SvgImageCollectionEditor / SvgImageListDesigner),
> `Design/CommandBarManagerDesigner.cs`, `Design/DockHostDesigner.cs`,
> `Design/CollectionEditors.cs` are all custom design types that **do not load**
> out-of-process. They compile fine and are referenced by `[Designer]`/`[Editor]`
> strings that simply fall back to defaults. They become live only if the parked
> plan in §9 is done. Do **not** rely on them meanwhile.

### `CommandBarManager.Theme` (design-time-settable, §7)
```csharp
public enum CommandBarTheme { Office2003, OfficeXP, Office2007, Office2010, Dark }

[Category("CommandBars")][DefaultValue(CommandBarTheme.Office2003)]
public CommandBarTheme Theme { get; set; }   // setter builds renderer, pushes to _hosts
public CommandBarRenderer Renderer { get; }  // Browsable(false)
public event EventHandler? ThemeChanged;
// RegisterHost also does: host.Renderer = _renderer;  // adopt current theme
```

---

## 7. Persistence

`Persistence/LayoutState.cs` (Version=2): full structural round-trip — bars,
items, order, visibility, dock/row/offset, icon size, `ShowToolTips`, settings
(incl. theme). `SaveLayout`/`LoadLayout`; demo auto-saves on exit, auto-loads on
start; `ResetToDefaults` preserves settings.

---

## 8. Demos

- **`MainForm.cs`** — everything built in code: menu bar, Standard/Formatting/
  Navigation/Paragraph toolbars, split buttons, combo, 5 themes in View menu
  (persisted), Customize dialog, icon-size menu, DPI PerMonitorV2. Home button
  opens a singleton `DesignerDemoForm`.
- **`DesignerDemoForm` (+ .Designer.cs)** — bars defined via the designer's
  `BarDefinitions`; icons embedded in an `SvgImageList` referenced by `ImageKey`;
  runtime `RegisterCommands()` + `BuildFromDefinitions()`. Hand-authored
  `InitializeComponent` uses **one unique local per object** (the VS CodeDom
  parser aliases by variable name — reusing a local collapses the collection).
  Note: the user has locally edited this `.Designer.cs`; do not regenerate/clobber
  without asking.

---

## 9. IN PROGRESS — real out-of-process designer support

**STATUS (2026-07-11): Stage 1 delivered, awaiting Windows build verification.**
Implemented per the plan below (following Microsoft's TileRepeater sample and
the control-library NuGet package spec):
- `CommandBars.Designer.Server` (net8.0-windows, `Microsoft.WinForms.Designer.SDK`
  1.6.0 pinned via `WinFormsDesignerSdkVersion` in `Directory.Build.props`):
  SDK-based `DockHostDesigner` (live refresh), `CommandBarManagerDesigner`
  (smart tag: Theme / "Edit toolbars and menus…" / "Refresh design preview"),
  `SvgImageListDesigner` (smart tag: "Import SVG files…" — dialog runs
  server-side, experimental), server `TypeRoutingProvider`.
- `CommandBars.Package`: packs `lib/net8.0-windows/` + `Design/WinForms/Server/`,
  date-based auto version, copies to local feed `NuGet\BuildOut` (wired by root
  `NuGet.config` with package-source mapping).
- `CommandBars.PackageDemo`: consumes `CommandBars.Package` `Version="*"` — the
  designer test bed. `CommandBars.Demo` is untouched (still ProjectReference).
- Runtime: `[Designer]` attributes re-pointed (by string) at
  `CommandBars.Designer.Server`; `InternalsVisibleTo` added. First build order
  and the verification checklist are in `DESIGNER-SETUP.md`.
- NOT yet done (stage 2/3): net472 `*.Designer.Client` + `*.Designer.Protocol`
  (client-side SVG picker dialog, custom UITypeEditors) and typed Add-dropdowns
  via server-side collection editors; deleting the built-in-editor fallbacks
  in §6.

### Original plan (for reference)

Goal: make the custom editors/designers actually load in VS so we regain typed
Add-dropdowns, the SvgImageList smart-tag ("Import SVG files…"), a proper SVG
image picker, and live IconSize/definition refresh. Per Microsoft's model this
requires a **separate design-time assembly apparatus consumed as a NuGet package**
(a project reference to the control library will *not* activate it). This is a
large, version-sensitive, hard-to-verify-blind effort — it was deliberately
deferred.

Required project structure (Microsoft "Custom Controls for WinForms' Out-Of-Process
Designer"):
1. **Runtime library** (existing `CommandBars`) — controls only; reference design
   types by **string** `[Designer(...)]`/`[Editor(...)]` (assembly-qualified to the
   design assemblies), no compile dependency.
2. **`*.Designer.Server`** (net8.0-windows) — `ControlDesigner`s, CodeDom
   serializers, `DesignerActionList`s, endpoint handlers. References
   **`Microsoft.WinForms.Designer.SDK`**.
3. **`*.Designer.Client`** (net472 — matches the VS host) — `UITypeEditor`s, modal
   dialogs, client viewmodels, and a **`TypeRoutingProvider`** mapping editor names
   to types (without it the client can't bind to the server-side definitions).
4. **`*.Designer.Protocol`** (netstandard2.0) — `IDataPipeObject` transport +
   request/response classes shared by client & server.
5. **`*.Designer.Package`** — packs into the NuGet layout
   `tools/roslyn/DesignToolsServer/<tfm>/…` + `tools/roslyn/VisualStudio/…`; the
   consuming app references the **package** (not the project). Central
   `WinFormsDesignerSdkVersion` in `Directory.Build.props`. Test by consuming from
   a local NuGet feed with a wildcard version.

Migration when this lands: move `Design/*` editors/designers into Server/Client,
delete the built-in-editor fallbacks in §6, and re-point the `[Editor]`/`[Designer]`
strings at the new assemblies.

**Reference:** Microsoft .NET Blog — "Custom Controls for WinForm's Out-Of-Process
Designer"; Microsoft Learn — "Designer changes from .NET Framework".

---

## 10. Roadmap (remaining)

- **Alt/F10 menu activation** — enter the menu bar via Alt or F10 with the first
  item highlighted, arrows between menus, Esc exits. (Natural completion of the
  keyboard-nav work already done.)
- **Accessibility (UIA)** — AccessibleObject roles/names, keyboard-focus events.
- **RTL support** — mirror bars/menus/chevron/drop positions.
- **Out-of-process designer package** — §9.
- Housekeeping: refresh `README.md` (still says Phase 2), unit tests for the newer
  layers, NuGet packaging of the runtime library, optional per-theme icon tint for
  Dark.

---

## 11. Recent-session change log (verify these are all in the project)

Since the bridge was intermittently offline, these files were delivered to chat
and may need placing/confirming:
- **Design-time editors → built-in:** `Imaging/SvgImageList.cs`,
  `Design/ItemDefinition.cs`.
- **Themes:** new `Rendering/Office2010Renderer.cs`, `Rendering/DarkRenderer.cs`,
  `Rendering/CommandBarTheme.cs`; edits to `CommandBarManager.cs`,
  `CommandBars.Demo/MainForm.cs`.
- **Manager `Theme` property:** `CommandBarManager.cs`, `MainForm.cs`,
  `Rendering/CommandBarTheme.cs`.
- **Chevron menu (Office nesting) + `CustomizeRequested`:** `CommandBarControl.cs`,
  `CommandBarManager.cs`, `MainForm.cs`.
- **Icon-less button shows text:** `Controls/BarLayoutEngine.cs`,
  `Controls/CommandBarControl.cs`.
- **Customize dialog DPI autosize + button equalization + palette border:**
  `Controls/CustomizeDialog.cs`, `Controls/CommandsPalette.cs` (both user-edited
  locally — treat the user's copies as authoritative).
- **DesignerDemoForm Customize wiring:** `CommandBars.Demo/DesignerDemoForm.cs`.
- **Live-refresh designer (inert out-of-process):** `Design/DockHostDesigner.cs`,
  `Design/CommandBarManagerDesigner.cs`.
