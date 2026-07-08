# Professional Office-Style CommandBar — Design Document

**Target:** .NET 8 (WinForms) · **Vector engine:** Svg.NET · **Rendering:** GDI+ (System.Drawing)
**Status:** Design draft · **Date:** 2026-07-01

**Confirmed decisions (v1):**

1. **Code/config-driven first** — a clean fluent/builder API; full VS design-time component support deferred to a later phase.
2. **Ship Office 2003 theme first** — the most familiar look; XP and 2007 renderers come in phase 3 behind the same abstraction.
3. **Persistence: user chooses where to save/load.** The library reads/writes a caller-supplied path or stream (no hardcoded location). The **demo app** saves its config next to the executable (launch location); `%AppData%` remains the recommended default the host can opt into.
4. **Fixed icon-size steps:** 12, 16, 20, 24, 32, 48, 64 px (logical), scaled by DPI at render time.

---

## 1. Goals & scope

Build a reusable WinForms custom control library that reproduces the classic Microsoft Office `CommandBars` experience:

- **Command bars** that can be a menu bar, a toolbar, or a popup menu — all one underlying concept.
- **Undock / float** — a docked bar can be dragged out into a floating mini-frame, and back.
- **Theme-able** — switch at runtime between Office XP, Office 2003, and Office 2007 looks (with 2003/XP color variants: Blue / Silver / Olive).
- **DPI-aware** — crisp at any scale using Per-Monitor V2.
- **Runtime-customizable** — create/edit/delete toolbars, edit the menu bar, toggle bar visibility, choose icon size, and assign custom vector (SVG) button images — all persisted across sessions.

Out of scope for v1 (candidate follow-ups): ribbon UI, keyboard-accelerator auto-underlining beyond basics, right-to-left mirroring, accessibility (UIA) — noted where design should leave room for them.

---

## 2. Guiding principle: one model, many faces

The single most important design decision is to copy Office's own abstraction: **everything is a command bar, and everything on it is a command bar control.** A menu bar is a docked command bar whose items are popup items. A toolbar is a docked command bar whose items are buttons. A dropdown menu is a floating/popup command bar. Reusing one model for all three is what makes docking, theming, and customization each a *single* implementation instead of three parallel ones.

A second decision: **separate the command from its visual item.** A `Command` (identity, text, icon, shortcut, enabled/checked state, execute handler) is data. A `CommandBarItem` is the on-screen presence of a command on a particular bar. One command can appear on many bars and stay in sync through data binding / events.

---

## 3. Architecture overview

```
┌──────────────────────────────────────────────────────────────┐
│ CommandBarManager                                            │
│  • owns all bars, the command registry, the active renderer  │
│  • owns dock host + floating windows                         │
│  • load / save layout (persistence)                          │
│  • enters/exits Customize mode                               │
└───────────┬─────────────────────────────┬────────────────────┘
            │                             │
   ┌────────▼─────────┐          ┌────────▼──────────┐
   │ CommandRegistry  │          │  DockHost         │
   │  Command objects │          │  4 edge DockBands │
   │  by id           │          │  + FloatingWindow │
   └──────────────────┘          └────────┬──────────┘
                                          │
                                 ┌────────▼──────────┐
                                 │   CommandBar      │  (menu bar / toolbar / popup)
                                 │   items[]         │
                                 │   dock state      │
                                 └────────┬──────────┘
                                          │ renders via
                                 ┌────────▼──────────┐
                                 │ CommandBarRenderer │  (abstract)
                                 │  ├ OfficeXP        │
                                 │  ├ Office2003      │
                                 │  └ Office2007      │
                                 └────────────────────┘
```

Suggested project layout:

```
CommandBars/                (class library, the control)
  Model/        Command, CommandBarItem hierarchy, CommandBar, enums
  Docking/      DockHost, DockBand, FloatingWindow, drag logic
  Rendering/    CommandBarRenderer (abstract), color tables, 3 renderers
  Imaging/      IImageSource, SvgImageSource, BitmapImageSource, icon cache
  Customize/    Customize mode, CustomizeDialog, drag-to-edit
  Persistence/  Layout serializer (JSON)
  CommandBarManager.cs
CommandBars.Demo/           (WinForms app exercising the control)
CommandBars.Tests/          (unit tests for model, layout, color tables)
```

---

## 4. Core object model

### 4.1 Command (the data)

```
class Command
    string   Id                // stable key, used in persistence
    string   Text              // "&File", "Cu&t"
    IImageSource Image         // vector or bitmap source
    Keys     Shortcut
    bool     Enabled
    CheckState Checked         // for toggle buttons
    string   ToolTip
    event    Executing / Executed
    method   Execute()
```

Commands live in a `CommandRegistry` keyed by `Id`. Enabled/checked state changes raise events so every item showing that command repaints. (This is also the natural hook for an eventual `ICommand`/MVVM-style binding layer.)

### 4.2 CommandBarItem hierarchy (the visuals)

```
abstract CommandBarItem            (owner bar, bounds, visible, begin-group flag)
 ├ CommandBarButton               (command-backed; image + text display style)
 ├ CommandBarToggleButton         (checkable)
 ├ CommandBarSplitButton          (button + dropdown arrow)
 ├ CommandBarDropDown / PopupItem (opens a child CommandBar as a menu)  ← menu items
 ├ CommandBarSeparator
 ├ CommandBarLabel
 └ CommandBarComboBox             (hosts a real ComboBox / custom editor)
```

`DisplayStyle` (ImageOnly / TextOnly / ImageAndText) and `TextImageRelation` mirror `ToolStrip` semantics so the model feels familiar.

### 4.3 CommandBar

```
class CommandBar
    string BarType             // MenuBar | Toolbar | Popup
    IList<CommandBarItem> Items
    DockState Dock             // Top | Left | Right | Bottom | Floating | Hidden
    Orientation Orientation    // Horizontal | Vertical (derived from dock)
    bool Visible
    bool AllowCustomize / AllowFloat / Locked
    Size  IconSize             // current icon size (customizable)
    // layout: row index & offset within a dock band
```

---

## 5. Docking & floating

The hardest interactive subsystem. Components:

- **DockHost** — sits inside the consuming form's client area, exposes four **DockBands** (top/left/right/bottom). Each band lays out one or more bars per *row/column*, honoring gripper drag to reorder and rewrap.
- **FloatingWindow** — a borderless top-level `Form` styled as a mini tool-frame (title, close, theme-matched border) that hosts exactly one bar in vertical or horizontal orientation.
- **Drag engine** — on gripper drag: enter drag mode, show a drop-zone/insertion preview, hit-test dock bands vs. free space, and on release either reposition within a band, move to another band, or spawn/return a FloatingWindow. Uses rubber-band feedback rather than live re-layout for performance.

Design notes:
- Represent every layout position as data (dock edge, row, offset) so it serializes and restores exactly.
- A floating bar remembers its "home" dock so double-click can re-dock it.
- Keep hit-testing in one place (`DockHost.HitTest`) shared by drag and customize mode.

---

## 6. Theming / rendering

Never paint inside the item classes. Use a renderer abstraction modeled on `ToolStripRenderer`.

```
abstract CommandBarRenderer
    ColorTable Colors
    void DrawBarBackground(...)
    void DrawButtonBackground(state)      // normal/hover/pressed/checked
    void DrawDropDownBackground(...)
    void DrawSeparator(...)
    void DrawGripper(...)
    void DrawMenuItemBackground(...)
    void DrawImageMargin(...)             // the left gutter in menus
    void DrawItemText / DrawItemImage(...)
```

Concrete renderers: `OfficeXPRenderer`, `Office2003Renderer`, `Office2007Renderer`, each with its own `ColorTable`. Office XP/2003 further support **Blue / Silver / Olive** variants driven by the current Windows visual style — expose these as a `ColorScheme` enum on the 2003/XP renderers.

**v1 ships the `Office2003Renderer` first** (the most familiar look), with `OfficeXPRenderer` and `Office2007Renderer` added in phase 3. The abstraction is built from the start so adding them is drop-in — no changes to item or docking code.

Runtime theme switch = `Manager.Renderer = new Office2007Renderer()` then invalidate every bar. Because all drawing routes through the renderer, no item code changes.

Key visual differences to capture in the color tables/routines: XP's flat single-pixel hot borders; 2003's gradient bars, blue gradients and the menu image-margin gutter; 2007's rounded gradient buttons and lighter "glass" hover. Gather reference screenshots before finalizing each color table.

---

## 7. DPI awareness

- Opt the demo app into **Per-Monitor V2** (`app.manifest` + `Application.SetHighDpiMode(HighDpiMode.PerMonitorV2)`).
- The control owns all metrics, so scale them by the current monitor DPI: icon sizes, paddings, gripper width, separator thickness, menu image-margin width, font.
- Handle `WM_DPICHANGED` / the WinForms DPI-changed events on floating windows and the host; recompute the icon cache at the new effective size.
- Store the user's chosen **icon size in logical pixels** from the fixed set (12/16/20/24/32/48/64) and multiply by DPI scale at render time — this is exactly why the icons are vector.

---

## 8. Vector / SVG icon pipeline

`Svg.NET` rasterizes an SVG document to a `System.Drawing.Bitmap` at any requested pixel size. Wrap it behind an abstraction so bitmap and vector icons are interchangeable:

```
interface IImageSource { Bitmap GetBitmap(Size px, float dpiScale); }
class SvgImageSource : IImageSource     // parses once, rasterizes per size, caches
class BitmapImageSource : IImageSource  // wraps PNG/ICO, scales as fallback
```

- **Cache** rendered bitmaps keyed by (source, pixelSize) — invalidate on icon-size change or DPI change.
- The "resizable icon size selector" in customize mode just changes the requested pixel size; vector re-rasterization keeps everything crisp.
- Custom button images: user picks an `.svg` (or PNG) in customize mode → stored as an `IImageSource` on the command, path/embedded-data captured in persistence.

---

## 9. Runtime customization

An Office-style **Customize mode**:

- **Customize dialog** with tabs: *Toolbars* (list, new/rename/delete, show/hide checkboxes), *Commands* (palette to drag onto bars), *Options* (icon size selector, show tooltips, menu animation).
- While the dialog is open the bars enter **edit mode**: items can be dragged between bars, reordered, removed (drag off), and the menu bar edited the same way; drop feedback reuses the docking hit-test.
- Icon size selector offers the fixed steps **12 / 16 / 20 / 24 / 32 / 48 / 64 px** (a combo or segmented picker, not a free slider) and applies live via the SVG pipeline.
- Bar visibility toggles map directly to `CommandBar.Visible`.
- All of it mutates the same object model, so nothing here is special-cased — it just needs the model to be fully data-driven and serializable.

---

## 10. Persistence

Serialize layout + customizations to **JSON** (`System.Text.Json`). **The location is caller-controlled** — the library exposes `SaveLayout(Stream|string path)` / `LoadLayout(Stream|string path)` and never hardcodes a folder, so the host app decides where config lives (a Save/Load dialog, a settings screen, `%AppData%`, wherever). The **demo app** persists `commandbars.json` next to its executable (launch location) to keep the sample self-contained; `%AppData%\<App>\` is documented as the recommended production default.

What gets serialized:

- Bars: id, type, dock state (edge/row/offset or floating rect), visibility, icon size, locked/float flags.
- Items per bar: command id, order, begin-group, display style, and any custom image reference.
- Custom/user-created toolbars and their items.
- Load merges saved state onto the app's *default* command set (so new app commands appear, removed ones are dropped gracefully). Version the schema for forward migration. Provide **Reset to defaults**.

---

## 11. Consumption & design-time story

**Decided: v1 is code/config-driven.** The control is consumed through a clean fluent/builder API (build the command registry, define default bars/items, hand it to a `CommandBarManager` hosted on the form) plus JSON load/save for user customizations. Full **design-time component** support — drag onto a form in the VS designer, edit via the Properties window / collection editors — needs extra `Designer`, `TypeConverter`, and collection-editor plumbing and is **deferred to a later phase (7)** once the object model has stabilized. The model is kept designer-friendly (public collections, parameterless-constructible items) so adding it later is not a rewrite.

---

## 12. Build phases (suggested sequencing)

1. **Model + command layer** — Command, item hierarchy, CommandBar, CommandRegistry, Manager skeleton. Unit-tested, no UI. *(foundation — get this right first)*
2. **Basic docked rendering, Office 2003 theme** — DockHost with a top band, render a toolbar + menu bar with the `Office2003Renderer` (the v1 launch theme); buttons, separators, hover/press states.
3. **Renderer abstraction + remaining themes** — formalize `CommandBarRenderer`, add the XP & 2007 renderers and color variants; runtime theme switch.
4. **DPI + SVG icon pipeline** — Per-Monitor V2, Svg.NET integration, icon cache, live icon-size changes.
5. **Floating / undock** — FloatingWindow, drag engine, dock↔float transitions, drop previews.
6. **Runtime customization + persistence** — customize mode, dialog, drag-to-edit, JSON save/load, reset.
7. **Polish & follow-ups** — design-time support, accessibility, RTL, keyboard navigation, more themes.

Each phase is demoable on its own in `CommandBars.Demo`, and phases 1–4 de-risk the rest.

---

## 13. Key risks & mitigations

| Risk | Mitigation |
|------|-----------|
| Docking drag/hit-testing complexity | Centralize hit-testing; use rubber-band preview not live relayout; build it *after* rendering is stable (phase 5). |
| Theme fidelity (matching real Office pixels) | Drive all colors from `ColorTable`; validate against reference screenshots per theme. |
| DPI + icon crispness | Vector icons + DPI-scaled metrics from day one of phase 4; cache per effective pixel size. |
| Persistence drift as app commands change | Merge saved layout onto the live default command set; version the schema; ship Reset. |
| Scope creep (ribbon, a11y, RTL) | Explicitly deferred to phase 7; keep the model open to them but don't build v1 around them. |

---

## 14. Decisions — resolved

All four planning forks are now settled (see the summary at the top):

1. **Consumption model** → code/config-driven for v1; design-time component deferred to phase 7.
2. **Launch theme** → Office 2003 first; XP & 2007 follow in phase 3 behind the renderer abstraction.
3. **Persistence** → caller-controlled path/stream (host decides where); demo app saves beside its executable; `%AppData%` documented as the recommended production default.
4. **Icon sizes** → fixed steps 12 / 16 / 20 / 24 / 32 / 48 / 64 px.

Nothing is blocking. Next step is scaffolding the Visual Studio solution and building **Phase 1** (the object model + command layer, unit-tested, no UI).
```
