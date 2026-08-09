# CommandBars next-chat handoff

## Repository state

- Workspace: `C:\Users\Rahmat Irfan\Claude\Projects\Professional Office Style Commandbar - Winform`
- Branch: `master`
- Base HEAD: `81ce754 Merge pull request #1 from Wraith1080/Tear-Off-Pop-Up`
- The worktree contains the completed Theme List implementation described below.
- Main design/context document: `CommandBar-Design_1.md`
- Project intent and standing instructions: `AGENTS.md`

Recent verification at HEAD:

- `CommandBars.Tests`: 83/83 passing.
- `CommandBars.Demo`: builds and smoke-launches.
- `CommandBars.PackageDemo`: builds and smoke-launches.
- PackageDemo currently consumes local package `CommandBars.Package` version
  `1.268.91642`.

## Recently completed work

The current branch includes the tear-off popup work and the related fixes made
during the long preceding chat:

- Popup placement flips appropriately for left/right-docked toolbars and limited
  screen working area.
- Re-clicking split-button arrows and overflow buttons closes their open popup.
- AutoShapes and nested categories can tear off; category root icons are retained.
- Popup first/last-row and separator spacing was made visually uniform.
- Palette grids suppress the menu image/text margin while mixed ordinary menus
  still reserve it when needed.
- The designer SVG import/stock-icon flow was repaired, colorful stock icons were
  added, and the NuGet leading-zero version problem was fixed.
- Irrelevant item properties are filtered by item kind in the designer editor.
- Compound items such as the Font ComboBox and complete AutoShapes popup hierarchy
  can appear in Customize/Add-or-Remove and are cloned with full functionality.
- A popup can set `ToolbarList = true`; its children are generated at runtime from
  all current toolbars and toggle visibility. Dynamic children are not persisted.
- Tear-off palette location/open state persists across application launches.
- Normal Demo and designer-authored PackageDemo have feature parity for the above.
- Nested tear-offs are independent siblings owned by the application form. Closing
  the AutoShapes tear-off no longer closes a category that was torn from it. The
  regression is covered by `CommandBars.Tests/TearOffWindowTests.cs`.
- Named hosted ComboBoxes are synchronized by their stable `Name` within a manager.
  A Font ComboBox dragged from Customize adopts the live font selection, and a
  change from any copy immediately updates all other copies.
- Customize factories preserve checkable commands as toggle items, including
  shared `Checked` and `Enabled` state. Compound definitions take priority over
  generic command buttons so split dropdowns survive, and blank command ids get
  deterministic shared identities.

## Completed feature: application-managed Theme List popup

The dynamic Theme List menu analogous to Toolbar List is implemented. Applications
manage the available themes; the popup renderer does not contain a hard-coded
theme enumeration.

Implemented design:

1. Add a manager-owned theme registry. Each entry needs:
   - stable string key;
   - display text (mnemonics allowed);
   - `Func<CommandBarRenderer>` factory.
2. Seed the registry with the five existing built-ins for backwards-compatible,
   convenient defaults: Office 2003, Office XP, Office 2007, Office 2010 Silver,
   and Dark. Let applications add, replace, remove, or clear entries.
3. Add an `ActiveThemeKey`/`ApplyTheme(key)` API. Applying a registered theme must
   create a fresh renderer, update all hosts and floating/tear-off surfaces, raise
   `ThemeChanged`, update the generated check mark, and persist the stable key.
4. Keep the existing `CommandBarManager.Theme` enum property as a compatible
   built-in shortcut. Synchronize it with built-in registry keys where possible;
   a custom renderer/key cannot be represented by that enum and should not be
   coerced to a false enum value.
5. Add `ThemeList` to `CommandBarPopupItem` and designer `ItemDefinition`, then
   propagate it through:
   - designer protocol DTOs and server mapper;
   - runtime definition realization;
   - palette cloning;
   - layout snapshot/rebuild and old-layout configuration preservation;
   - property filtering in the design-time item editor.
6. `CommandBarManager.PreparePopup` should rebuild a Theme List popup whenever it
   opens, using registered theme entries and checked toggle commands. Generated
   children must never be saved into layout JSON or exposed as authored commands.
7. `ThemeList` and `ToolbarList` are mutually exclusive dynamic sources. Prefer
   mutually exclusive setters (setting one clears the other) rather than silently
   rendering a mixture. Hide/disable authored `Items` when either source is active.
8. Move theme persistence into the manager. Persist only the theme key, never a
   renderer/type/factory. If a saved key is not registered, retain a safe current
   theme and do not crash. Consider a pending key if themes may be registered after
   `LoadLayout`.
9. Make `CustomizeDialog` respond directly to manager `ThemeChanged` so applications
   do not have to manually call `SetRenderer` after every theme switch.
10. Update both demos:
    - code-built `CommandBars.Demo/MainForm.cs`;
    - designer-authored `CommandBars.PackageDemo`.
    Their View > Theme popup should have no static theme children and should use
    `ThemeList = true`. Remove duplicated `RegisterTheme` command plumbing and
    manual theme-setting persistence once the manager owns those responsibilities.
11. Add tests for registry replacement/removal, active selection, unknown saved
    keys, generated menu checks/execution, mutual exclusion, definition/protocol
    mapping, persistence without dynamic children, and enum compatibility.
12. Rebuild the local NuGet package after library/designer changes, update
    PackageDemo to the newly generated exact version, force restore it, and build
    and smoke-launch both demos. The package project uses a date/time-derived
    version and copies packages to `NuGet/BuildOut` (and currently `E:\Nuget`).

## Files changed for Theme List

- `CommandBars/CommandBarManager.cs`
- `CommandBars/Model/CommandBarPopupItem.cs`
- `CommandBars/Rendering/CommandBarTheme.cs`
- a new model such as `CommandBars/Rendering/CommandBarThemeRegistration.cs`
- `CommandBars/Design/ItemDefinition.cs`
- `CommandBars/Persistence/LayoutState.cs`
- `CommandBars/Controls/CustomizeDialog.cs`
- `CommandBars.Designer.Protocol/BarDefData.cs`
- `CommandBars.Designer.Server/BarDefinitionMapper.cs`
- `CommandBars.Demo/MainForm.cs`
- `CommandBars.PackageDemo/MainForm.cs`
- `CommandBars.PackageDemo/MainForm.Designer.cs`
- `CommandBars.Tests/CommandBarManagerTests.cs`
- `CommandBars.Tests/DesignDefinitionTests.cs`

## Ready-to-paste prompt for the next chat

> Continue the CommandBars project from `NEXT-CHAT-HANDOFF.md`. The dynamic,
> application-managed Theme List feature is complete. Read `AGENTS.md` and
> `CommandBar-Design_1.md`, inspect the current branch/status and verification
> results, then help select and implement the next feature while preserving the
> completed registry, persistence, designer, demo, and package behavior.

