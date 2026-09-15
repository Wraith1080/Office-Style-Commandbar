# CommandBars next-chat handoff

## Resume from the current checkout

Use [AGENTS.md](AGENTS.md) for working rules and read the source and historical
sections relevant to the task. Resolve branch, package version, and build state
from the checkout; earlier results below are not fresh verification.

## Current summary

- Vista Aurora border refinement (2026-09-15): toolbar chunks now use the same
  3-logical-pixel corner radius as Office 2003, show the underlying dock band at
  the corners, and have a light inner outline including the leading edge.
  Overflow rendering stays inside the shared frame and adds a leading divider,
  preserving the complete border/reflection in idle, hover and pressed states.
  All 19 VistaAurora tests pass; the new 8 cases cover both orientations at
  100/125/150/200%, with and without overflowed items. Inspected rendered Demo
  output. Net6 runtime, designer prerequisites, and both demos build; PackageDemo
  pins 1.269.150711. Latest source demo: `CommandBars.Demo/bin/VistaAuroraBorderPreview`.
  No new live interaction or monitor-DPI checks.

- Vista Aurora theme (2026-09-15): added `CommandBarTheme.VistaAurora` (8),
  stable key `vistaaurora`, and the shared dynamic theme-menu entry. Teal glass
  bars have a reflected horizon and aurora tint, light captions, rounded states,
  and coordinated floating captions. Menus/combos/dialogs stay pale with dark
  text. Toolbar images retain their colors with a light rim for visibility.
  New renderer hooks separate toolbar images and inline combo text/arrows;
  existing themes retain their default forwarding behavior. One default palette;
  unsupported scheme preferences are retained across theme switches.
  Verification: 54 focused VistaAurora/CommandBarManager/ColorScheme/RendererLayout
  tests pass (including 100/125/150/200% rendering); net6 runtime, designer
  prerequisites, source Demo and PackageDemo build. PackageDemo now pins local
  package 1.269.150704. Inspected DrawToBitmap output from the actual source Demo,
  popup, floating toolbar and Customize dialog, plus a button-state sheet.
  Preview executable: `CommandBars.Demo/bin/VistaAuroraPreview`; choose
  View > Theme > Vista Aurora. Live pointer/keyboard interactions, monitor DPI
  transitions, and Visual Studio designer checks were not performed.

- XP optical alignment (2026-09-13): user verified dotted grips across DPI.
  XP multistrip alone now moves one additional device pixel toward the trailing
  edge, preserving pitch. Four focused XP margin/transpose cases pass at
  100/125/150/200%. Demo: CommandBars.Demo/bin/XpGripperPreview; package unchanged.

- Gripper pixel alignment (2026-09-13): odd leftover pixels now go to the leading
  margin for dots and XP strips, reducing the trailing margin by one pixel while
  retaining constant gaps. All 9 focused dotted/shared margin cases pass.
  Updated demo: CommandBars.Demo/bin/GripperMarginPreview; package unchanged.

- Gripper spacing refinement (2026-09-13): supersedes gap distribution below.
  Dots and XP strips now use constant integer pixel pitch and center the complete
  embossed pattern. End margins differ by at most one device pixel when the
  available length is odd. All 13 focused gripper rendering tests pass, including
  constant gaps and centered margins over heights 24–90 at 100/125/150/200%.
  Separate demo build: CommandBars.Demo/bin/GripperSpacingPreview. No full suite,
  package refresh, or live DPI check.

- Gripper end insets (2026-09-13): dotted and XP multistrip grips distribute
  leftover pixels across their gaps so the complete embossed marks have balanced
  end insets (3 logical pixels for dots, 4 for strips), including at 125%.
  Supersedes the dotted spacing description below. All 13 focused gripper
  rendering cases pass, including both orientations and heights 24–90 pixels
  at 100/125/150/200%. Running Demo locked its normal output; a separate build
  is available in CommandBars.Demo/bin/GripperPreview. No package or live DPI check.

- Dotted gripper DPI (2026-09-13): Office2003Renderer's shared dotted gripper
  now scales dot size, highlight offset, spacing, and insets using renderer DPI.
  The 100% appearance is preserved. Only the focused DottedGripperTests rendering
  test was run: horizontal/vertical geometry, highlight and bounds at
  100/125/150/200%, then back to 100%, pass. No full suite or live Windows scale
  check was run for this change. Source Demo rebuilt; package not refreshed.

- Popup font DPI baseline (2026-09-13): menus and combo dropdowns copied an
  already DPI-scaled source font into a new Form still using the process initial
  DPI. Its first native transition scaled that font again, compounding at each
  submenu level. WindowDpiLayout now normalizes an owned font copy to the initial
  window DPI when its handle is created; subsequent live transitions remain native.
  Regression tests failed before the fix in both directions (18 became 36 points;
  7.2 became 5.76). Three popup levels, custom bold fonts, combo dropdowns, and
  subsequent DPI round trips now pass. All 315 regular and 17 isolated DPI tests
  pass; net6 runtime, designer prerequisites, and both demos build. PackageDemo
  uses package 1.269.132340. Actual Windows Settings scale changes were not repeated
  for this correction; automated tests send native DPI messages to existing HWNDs.

- Stationary DPI and palette icon sizing (2026-09-13, supersedes live-check and
  package status below): owned windows could receive display/settings broadcasts
  without WM_DPICHANGED until moved. WindowDpiLayout rechecks their own monitor
  and requests scaled bounds at the same position, provoking the real native DPI
  sequence. A live Windows Scale 150% to 125% check verified stationary floating
  and Customize windows updated; desktop scale was restored to 125%. Full live
  coverage of every dialog and mixed-monitor transitions remains unperformed.
  Torn-off palettes resize through the manager API, verified visibly with Fluent.
  Customize > Options previously bypassed that API; it now updates open palettes
  too. Tests cover linear/grid palette growth/shrinkage through Customize and
  icon changes after a DPI notification. All 296 regular and 13 isolated DPI
  tests pass. Net6 runtime, designer prerequisites, source Demo and PackageDemo
  build; package 1.269.131703 is pinned. Restart a demo to load the new assembly.

- Live window DPI updates (2026-09-13, supersedes the package-pending notes
  below): runtime windows now finish custom sizing in a coalesced posted pass
  after the native DPI-change sequence. Floating/tear-off frames and popup
  metrics avoid measuring midway through that sequence. Customize remeasures
  button widths; Add/New/Rename/confirmation dialogs receive the final layout
  pass. Combo dropdowns refresh their font, rows, insets, width, and region;
  themed and Fluent suggestion combo rows follow font/DPI changes.
  All 294 ordinary tests and 8 isolated DPI-message tests pass. The latter keep
  existing HWNDs through DPI increases/decreases and check deferred callbacks
  and disposal; they do not change actual monitor DPI or replace a live Windows
  Display > Scale check, which remains pending. Net6 runtime, designer Client/
  Server prerequisites, source Demo, and PackageDemo build successfully. Local
  package 1.269.131128 is built and pinned in PackageDemo; SHA-256 confirms both
  demos contain the same current runtime DLL. Restart the demo to load this build
  before testing scale changes with floating windows and dialogs left open.

- Dock/floating/popup sizing (2026-09-13): DockHost now reconciles its band on
  child layout as well as resize/DPI events, batching rebuilds to preserve bar
  ordering. FloatingWindow and TearOffWindow opt into DPI autoscaling and
  remeasure their frames after layout/DPI changes; caption painting sets the
  renderer's current scale. Regular popup menus refresh cached metrics and
  regions on font/DPI changes. All 294 tests pass, including new bottom/right
  band, floating-frame, and popup font/scaling checks. Net6 runtime and net8
  source Demo build successfully. Live cross-monitor checks remain pending;
  PackageDemo needs a new runtime package to consume these control fixes.

- Demo status bar sizing (2026-09-13): both Demo and PackageDemo recalculate
  the bottom status label height from its preferred font height plus DPI-scaled
  padding, with a 24-logical-pixel minimum. Updates run on initialization,
  handle creation, font changes, and parent DPI changes. Demo builds for net8
  and net6 (existing net6 end-of-support warning); PackageDemo builds cleanly.
  Live large-font and cross-monitor visual checks remain pending.

- Add Command row sizing (2026-09-13): the runtime themed list now measures its
  current font plus DPI-scaled vertical padding at construction, handle creation,
  font changes, and parent DPI changes. This fixes clipped command labels in
  Customize > Menu Bar > Add Command. All 289 tests pass, including native row
  geometry after inherited font growth/shrinkage; net6 runtime builds cleanly.
  Live cross-monitor UI verification remains pending; no package was rebuilt.

- Fluent marker refinement (2026-09-13): menu gripper, combo selection, and
  floating-caption markers are consistently 4 logical pixels thick. All markers now
  use fully rounded ends based on their pixel thickness. Floating-caption marker
  width matches the docked menu marker; original toolbar gripper remains unchanged.
  42 Fluent tests pass, including scale-aware marker width/corner rendering at
  100/150/200%. Net6 runtime and designer/package prerequisites build cleanly.
  PackageDemo updated to 1.269.130622. No new live UI or deferred designer checks.

- Fluent menu gripper (2026-09-13): only menu bars use the inset rounded box,
  with neutral idle fill and accent hover fill. Toolbar grippers retain their
  original full-height clipped treatment. Other themes use their existing
  grippers. Verified with 39 Fluent tests, including inset/corner checks in both
  orientations at 100/150/200% scale and the original toolbar-border regression.
  Net6 runtime and designer prerequisites build cleanly; package 1.269.130605
  is the updated PackageDemo reference. No new live designer/mixed-monitor checks.

- Menu caption preference/gripper alignment (2026-09-12): horizontal menu rows
  now share the toolbar leading inset. Customize > Options offers "Rotate
  captions on side-docked menu bars", backed by RotateVerticalMenuCaptions.
  False retains horizontal side captions; true rotates left captions bottom-to-top
  and right captions top-to-bottom. Layout persistence defaults older files to
  false; Reset All preserves the current preference. Dock previews use the option.
- Verified for this follow-up: 43 focused docking/layout/dialog tests pass. Live
  Office 2003 check confirmed aligned menu/toolbar grippers and immediate toggling
  in both directions. Net6 runtime, Server/net472 Client and package build pass.
  PackageDemo references package 1.269.122319. The earlier deferred live designer
  Undo/Redo/save/reopen and mixed-DPI checks remain deferred at the user's request.

- Menu docking (2026-09-12): designer Add Menu Bar is available on every host,
  including when other menu bars exist. Runtime and unsited designer previews
  place menus in dedicated full-width rows/full-height columns, in collection
  order nearest the outer edge, before toolbars. Side captions stay horizontal.
- Menu grippers honor AllowFloat. Menus float as compact horizontal bars and
  re-dock into dedicated slots without changing toolbar Row/Offset. Alt routing
  reaches floating menus. Close/double-click returns a floating menu to its
  previous edge; additive LastMenuDock layout state defaults to Top for older
  files. Menu-only overflow is retained.
- Verified: 66 focused tests pass (menu docking, host/renderer layout, design
  definitions, and layout loading), including 11 new regression cases. Net6
  runtime, designer Server/net472 Client, and PackageDemo build without warnings.
  Package 1.269.122302 was built after source prerequisites; PackageDemo now
  references it and was force-restored successfully. Diff whitespace check passes.
- Live visual check: a temporary source-consuming WinForms harness displayed two
  menus on each edge with no overlap. Dragged a top menu to float, opened it via
  Alt+F, and dragged it to a dedicated left column. Harness is under ignored
  TestResults/MenuDockSmoke and its window was closed after verification.
- Not performed: live Visual Studio Add/Delete/Undo/Redo and save/reopen checks,
  or mixed-monitor/theme matrix. Preview regression tests exercise definition
  removal/restoration, but do not prove the IDE transaction behavior. The original
  reported top-menu overlap was not reproduced in the automated preview checks.
  See DESIGNER-SETUP.md for the focused menu-docking manual matrix.

### Previous summary (historical)

- Fluent accent harmonies (2026-09-09): six extra choices for each colored base
  in Theme > Accent color and the custom dialog's Suggested accent picker.
  HSL relationships: tonal (0), analogous (+/-30), complementary (180), split
  complementary (150/210). Named choices for Cool Blue, Mint, Rose and Lavender;
  custom bases derive choices from their hue, with neutral pairings for gray/
  white/black. Neutral has no extra choices. The dialog shows rendered color
  swatches and retains the user's accent when the base changes. Selections use
  the existing FluentAccentColor RGB persistence, with no schema change.
- Verification: 258 automated tests passed, including nine new cases covering
  hue relationships, extreme-base contrast at 0/50/100 tint, menu selection,
  RGB persistence and dialog preview/Cancel. Inspected a rendered dialog capture
  for Mint + Terracotta. Net6 runtime, net8 Demo, Server and net472 Client builds
  pass without warnings. Package 1.269.91644 built; PackageDemo updated,
  restored and built successfully (Demo and PackageDemo use isolated
  bin/AccentPaletteCheck output for verification). Diff whitespace check passes.
- The user requested skipping the live UI check. No manual demo/designer or
  mixed-monitor check was performed for these accent changes. Existing general
  designer/DPI follow-ups below are historical, not newly performed checks.

### Earlier implementation checkpoints


- Fluent base/custom colors (2026-09-09): independent Neutral/Cool Blue/Mint/
  Rose/Lavender/Custom surface palettes, custom opaque RGB accent, 0�100 tint
  strength, standard designer properties, immutable FluentColorOptions API and
  atomic SetFluentColors. Dynamic Fluent Theme menus now have Accent color and
  Base color submenus, with Custom colors opening a detached live-preview dialog
  (color pickers, HEX validation, Reset/OK/Cancel). Both demos share this menu.
  Layouts persist all preferences; legacy files use Neutral; Reset All retains
  them. Other themes and application factories remain unchanged. Existing
  Office 2000/layout edits and RenderingTests changes were preserved.
- Verified: 249 tests passed; six new Fluent tests cover independent palettes,
  persistence/reset, invalid input, extreme-color contrast, menu choices and
  dialog validation. Inspected a rendered dialog capture with custom teal base;
  controls and preview fit. Net6 runtime, net8 Demo (isolated output), Designer
  Server and net472 Client builds pass. Package 1.269.90602 built successfully;
  PackageDemo reference advanced to it. Check latest build output for consumer
  verification. No interactive Visual Studio Undo/Redo or mixed-monitor check.
- Usage reserve: stopping at the safe packaging/handoff boundary near 5%.
  Next: live demo preset/custom-color Apply/Cancel checks, floating/tear-off and
  Customize dialog inspection, designer property serialization/Undo/Redo, and
  150/200% DPI dialog inspection. Older entries below record prior sessions.


- Color schemes added (2026-09-08): manager ColorScheme preference, filtered
  designer property converter, EffectiveColorScheme/AvailableColorSchemes,
  palette-aware renderer constructors, and Color scheme submenu automatically
  included in built-in dynamic Theme lists (both demos). Office 2000/XP/2003:
  Blue/Silver/Olive; Office 2007/2010: Blue/Silver; Fluent: Blue/Teal/Purple;
  all include Default. Dark and application factory palettes stay unchanged.
  Current defaults are preserved, unsupported preferences fall back to Default
  and resume on a supporting theme. JSON stores the preference; legacy/unknown
  values use Default; layout reset retains the preference. Classic variants are
  project-specific coordinated tints, not exact historic Office palette replicas.
- Verification for schemes: 243 tests pass, including switching/host notification,
  legacy persistence, reset, custom factories, menu generation and paint smoke
  tests at 100/150/200% DPI. Net6 runtime and isolated-output net8 Demo builds
  pass without warnings. Review and whitespace checks pass.
- Stopping before the user's 5% five-hour usage reserve. Remaining: visually
  inspect every palette, menus, floating/tear-off captions and Customize dialogs;
  rebuild Server and Client, package, advance PackageDemo's pinned version,
  restore/build it, and verify designer property changes, Undo/Redo and saved
  serialization at multiple DPIs. PackageDemo currently consumes the older
  package and will gain the generated scheme submenu after that update.
  No package rebuild or live UI/designer check was performed this turn.
  Existing combo placement edits and their documentation/tests were preserved.


- Combo dropdown placement now follows the toolbar dock edge, opening inward
  on right/left/bottom docks and below top/floating bars. Opening-side fallback
  and monitor clamping remain in place. Verified: 10 focused placement tests
  covering dock edges, fallback, and oversized popups on negative-coordinate
  monitors; net6 runtime build passes. Live UI checks and package rebuilding
  were not performed.
- Icon-size selection now updates open tear-off clones and resizes their frames
  via `CommandBarManager.SetIconSize`; both demo handlers use it. Focused linear
  and grid palette tests pass (grow/shrink and saved icon size); other 223 tests
  passed in the initial run. Net6 build and isolated-output net8 Demo build pass.
  Normal Demo output was locked by the running Demo/Visual Studio. PackageDemo
  needs a rebuilt local package for the new API; package rebuilding and live UI
  checks were not performed.
- Tear-off uniqueness fixed (2026-09-08): manager reuse/restoration compares
  stable `TearOffKey` values across placements, with identity preserved in nested
  clones and layout items/open-window records. Catalog IDs supply keys; the
  code-first AutoShapes factory assigns stable root/category keys. Legacy layout
  items recover captured-default identities or their legacy dropdown key.
  Verified: 223 tests, net6 runtime build, net8 code-built Demo build, and diff
  whitespace check. Interactive drag/designer verification and package rebuild
  remain unperformed.
- Fluent renderer branching refactored (2026-09-08): removed
  `UsesFluentMenuChrome` and the `BarMetrics.Fluent` discriminator. Renderer
  metrics and geometry/painting overrides now own caption spacing, popup/icon
  layout, combo chrome, toolbar sizing and popup anchors. Independent behavior
  capabilities are separate options. Existing appearances and layout-key
  migration are preserved. Verified: 218 tests pass, including custom-renderer
  geometry coverage at 100%, 150% and 200%; net6 runtime build passes without
  warnings. Interactive demo/designer and mixed-monitor checks were not run;
  the local package has not been rebuilt for this runtime refactor.
- Office 2000 separators now account for the highlight's bottom inset when
  centering their line pair. Separator row height also removes the spare pixel
  below the line pair, correcting the larger gap above the blue highlight. Bitmap checks
  cover gaps on both sides of a selected row at 100%, 150%, 200%, and 300% DPI;
  all 215 tests pass. Gaps above and below the highlight must match exactly.
- Office 2000 popup alignment fixed (2026-09-08): navy selection now uses the
  same DPI-scaled bottom inset as the hovered image/checked-item bevel. Bitmap
  regression checks cover both states at 100%, 150%, and 200%; all 211 tests pass.
  Interactive PackageDemo/designer verification and package rebuilding remain
  unperformed for these runtime fixes.
- Side-docking alignment fixed (2026-09-08): the first toolbar in each left/right
  column starts at the content area's top edge. Inter-bar and bottom spacing are
  preserved, and the height budget includes the recovered top margin. Verified:
  205 tests passing, net6 runtime build successful, and `git diff --check` clean.
  PackageDemo/designer visual checks and mixed-monitor DPI checks were not run;
  the local package has not been rebuilt for this runtime fix.
- Fluent theme development was recorded as complete. The latest implementation
  entry below reports 203 passing tests and package/demo verification; these are
  historical results. Mixed-monitor DPI and designer interaction remain recorded
  manual verification gaps.
- The design document records the remaining roadmap.
- For package/designer work, use [DESIGNER-SETUP.md](DESIGNER-SETUP.md). Runtime
  tests and the code-built Demo do not require package bootstrap.

## Instruction and workflow audit — 2026-09-05

Reviewed current official [GPT-6 Astra guidance](https://developers.openai.com/api/docs/guides/latest-model),
[AGENTS.md discovery](https://learn.chatgpt.com/docs/agent-configuration/agents-md),
[skill authoring](https://learn.chatgpt.com/docs/build-skills), and
[Codex best practices](https://learn.chatgpt.com/guides/best-practices).

Condensed AGENTS.md around repository constraints, contextual references, and
completion. README now owns the change-specific verification matrix, including
the net6 compilation check that the net8 test project does not cover. Removed the
handoff's unconditional Git-history-reading step and separated current status
from the historical implementation record.

Inventory included hidden and ignored repository files: one AGENTS.md, no
SKILL.md/skill.md, nested instruction overrides, repository agent configuration,
or CI workflow files were found. No skill was added: the existing designer guide
already supplies the specialized procedure. Installed personal/plugin skills
outside the repository were not modified. Model and permission settings were
not changed; these documentation improvements have not been performance-benchmarked.

Known build-comment drift remains: Directory.Build.props describes its framework
helper as authoritative although projects declare their own targets; the package
project comment claims every build yields a fresh version and a wildcard consumer,
but versions have minute precision and PackageDemo pins an exact version. The
setup guide documents the actual behavior. Correct those comments when maintaining
the build configuration; they are not instructions to change targets or versions.

Validation for this documentation edit: referenced local paths and Markdown
anchors, command/project consistency, final diff, and Git whitespace checks.
Runtime tests, builds, and manual designer checks were not rerun.

## Fluent theme implementation — 2026-09-05

Floating-window follow-up: Fluent toolbar/palette frames have a purple outline,
tinted taller caption, accent marker, rounded close hover/pressed states and DWM
corner preference. Grid palette separators now paint horizontally across themes.
Tear-off creation/reuse inherits the parent toolbar icon size without mutating the
source dropdown; nullable TearOffState.IconSize preserves it across save/restore
while older layouts retain their existing fallback.
203 tests passed; runtime net6, Server/Client, package and both demo builds passed.
PackageDemo was restored to the fresh package. Live Fluent Demo tear-off confirmed
caption styling, horizontal separators and retained palette size. Mixed-monitor
DPI and designer interaction were not manually checked. Diff check passed.

Theme development is complete per the user. Renamed the menu entry to Fluent,
the enum to `CommandBarTheme.Fluent` (still numeric value 6), renderer/palette to
`FluentRenderer`/`FluentColorTable`, and layout key to `fluent`. Old
`visualstudio2026` layout keys migrate on load; new saves use `fluent`. Code using
the former public type/member names must use the new names. Earlier notes below
retain historical names and verification status.

Rename verification: all 200 tests passed, including old-key load/new-key save.
Runtime net6, designer Server/Client, package, Demo and PackageDemo builds passed.
PackageDemo now consumes the newly built package, including all final spacing and
alignment refinements. Diff check passed. No live designer/UI check was performed.

### Visual refinement follow-up

Latest spacing correction: removed the extra 4 logical pixels of menu row height
and expanded only its painted hover/open bounds, preserving highlight height while
reducing empty space above/below. Menu-to-first-toolbar gap is now 2 logical pixels;
other toolbar gaps remain 4. 200 tests passed; net8 Demo build and diff check passed.
Live visual review and PackageDemo rebuilding remain pending.

Latest follow-up extends popup alignment to the painted top edge on vertical
toolbars and the painted left edge on menu bars. Fluent menu-bar rows gain
2 logical pixels above and below captions. 200 tests passed, including menu-bar
spacing/alignment at 100/150/200% scale. Live visual inspection and PackageDemo
repackaging remain unperformed for these alignment changes.

Latest popup alignment pass: Fluent horizontal toolbar dropdowns (including split
buttons and overflow) now anchor to the painted button's left edge instead of the
larger hit bounds, for both above and below placement. Existing screen clamping,
vertical placement, menu-bar placement and combo-field anchoring are retained.
197 tests passed and the net8 Demo build succeeded. Live visual inspection and
PackageDemo repackaging were not performed for this small follow-up.

Latest sizing pass: toolbar button/split/combo surfaces match overflow height;
icon-only and short-caption widths follow that size, while longer captions keep
content-based widths. Toolbar images are fitted with padding, including SVG
rasterization at the fitted size. Selected icon-size settings and popup images
are retained. 191 tests passed, including matching button/overflow dimensions and
image fitting at 16/24/32 icon settings. Runtime net6, designer Client/Server and
local package builds succeeded. Earlier counts below are historical.

Latest corrections: full-height gripper clipped by toolbar chrome; square overflow
hit/hover geometry with a 3-logical-pixel highlight inset; square checked menu icon
frames shifted right for equal left/vertical padding; symmetric menu highlights
and an odd-height single-line separator row. The user's latest combo requirement
supersedes the previous transparent resting field: white with a border at rest,
toolbar color with the same border when hovered. All 188 tests passed before final
packaging, including new gripper, overflow, combo-state and menu geometry checks.
Runtime net6, Server, Client, package, Demo and PackageDemo builds succeeded;
PackageDemo was force-restored to the newly built local package. Live PackageDemo
inspection confirmed square overflow clearance, square menu icon frames and File
menu separator spacing. `git diff --check` passed. The demo was left open for review.

The earlier refinement notes below are historical where superseded above.

Addressed the user's eight visual corrections: standard toolbar item padding;
4-logical-pixel gaps between bars/rows/columns; inset toolbar highlights; no filled
field for a resting combo; 3-logical-pixel owner/root-popup gaps; wider split arrows
with no idle divider and square shared edges; solid square overflow dots; and
symmetric alpha-coverage rendering instead of GDI+ rounded paths. Windows 11
popup windows opt into compositor rounding without a clipping region. The
Windows 10/rejected-DWM fallback is a symmetric region with binary outer clipping.
See [Microsoft's custom-menu rounding guidance](https://learn.microsoft.com/windows/apps/desktop/modernize/ui/apply-rounded-corners).

Follow-up verification: 183 tests passed, including horizontal/vertical dock gaps,
compact metrics, combo gap/background, inset hover, and corner symmetry with partial
alpha at 100/150/200% scaling. Runtime net6, designer Server and Client builds
passed. Live Demo inspected compact bars, gaps, combo/menu placement and rounding.
The original implementation results below are historical. Windows 10 fallback,
mixed-monitor transitions, and designer Undo/Redo still need manual verification.

### Initial implementation

Added `CommandBarTheme.VisualStudio2026` (`visualstudio2026`) and its neutral
light renderer/palette. The theme is registered in dynamic theme menus and is
available through the manager's designer property. Existing enum values and
theme behavior are retained. Custom tinting and a Fluent dark variant are deferred.

Implemented flat rounded bars/buttons/combos, independent split-button hover,
purple gripper hover in both orientations, padded rounded popups with Windows
menu shadows, inset separators, open chevron glyphs, muted shortcuts, overlapping
submenus, and persistent combo selection highlights with a purple vertical bar.
Per the user's latest preference, overflow uses one compact shared icon/check
column, not separate columns. Checked icons receive a frame; iconless checks use
a checkmark. Theme choices use radio dots via the runtime `Command.RadioCheck`
property. That property is code-owned, not a new catalog/protocol field; exclusive
selection is still application-owned. Existing icons are retained.

Verification in this implementation session:

- `dotnet test CommandBars.Tests/CommandBars.Tests.csproj`: 176 passed, 0 failed.
- Runtime net6 compatibility, net8 Demo, designer Server and net472 Client builds
  succeeded. New local package built; PackageDemo's reference advanced to it,
  force-restored and built successfully. Its runtime launched and switched to the
  new theme successfully. `git diff --check` passed.
- Live Demo inspection covered theme switching, rounded combo dropdown and its
  selection marker, menu/submenu appearance and radio dot, plus toolbar floating
  and redocking. Automated rendering covers 100%, 150%, and 200% gripper scaling.
- Remaining manual checks: designer theme change with Undo/Redo and serialization,
  mixed-monitor DPI transitions, full interaction matrix across all older themes,
  and detailed side-by-side review against the supplied Visual Studio screenshots.

The PackageDemo project comment incorrectly claimed packaging built prerequisites;
corrected it to match DESIGNER-SETUP.md. No commits or publishing were performed.

## Previous documentation update — 2026-09-05

Migrated contributor guidance to direct Windows/PowerShell/Git work. Removed
obsolete bridge/sandbox rules, clarified document ownership, corrected conflicting
Stage 8 status, and documented the package bootstrap and multi-target Demo command.
Only documentation changed. Source/command consistency, local Markdown links, and
Git whitespace/diff checks are the validation for this update; runtime tests and
manual designer checks were not rerun.

The sections below retain the earlier implementation and verification report.
They are historical results, not checks performed during this documentation edit.
Read the current PackageDemo project for its pinned version; the version below
identifies the previously verified artifact.

## Catalog-first redesign status

Stages 1–8 are recorded as complete:

- Semantic Action, Toggle, Popup, Split Button, Combo Box, and Label commands are
  defined once in the manager-owned command catalog.
- Bars and compound dropdowns store lightweight catalog placements; separators
  remain structural.
- Runtime realization, persistence, client/server protocol mapping, validation,
  explicit legacy migration, and Customize factories use the catalog model.
- The manager editor has separate Commands and Bars and Menus pages with a
  shared DPI-aware property panel.
- `DockHost` smart tags create bars and place commands. Every preview bar also
  has a DPI-scaled blue **+** glyph using the shared picker.
- Designer commits use batched, signature-aware preview refreshes.
- Popup placement display styles materialize correctly in preview and runtime.
- Runtime Customize preserves Popup, Split Button, and Combo Box behavior and
  protects application/designer-created bars from deletion.
- Code-built Demo and designer-authored PackageDemo expose matching behavior.

## Prior Stage 8 automated verification

- Replaced the obsolete Phase 2 README and Stage 1 setup guide with current
  catalog-first documentation.
- Added the README to the NuGet package.
- Fixed nullable protocol-reader warnings.
- Removed redundant direct solution dependencies from the package to the
  multi-target runtime/protocol projects; this fixed full-solution
  `GetTargetPath` failures while client/server retain the required transitive
  build order.
- `dotnet test CommandBars.Tests/CommandBars.Tests.csproj --no-restore`:
  167 passed, 0 failed.
- `dotnet build CommandBars.sln --no-restore`: succeeds. The only warning is
  NETSDK1138 from the intentionally retained `net6.0-windows` compatibility
  target.
- Package `1.269.50246` contains README, runtime DLL/PDB/XML, net472
  Client/Protocol, and net8 Server/Protocol assets.
- PackageDemo force-restores and builds with 0 warnings/errors.
- The net8 code-built Demo builds with 0 warnings/errors.
- Both demo executables pass a hidden smoke launch.

## Prior Stage 8 manual verification

The PackageDemo designer opens with all bars and no diagnostics. Saving,
closing, reopening, and saving without a semantic edit produces no generated-file
churn. Changing the manager theme and undoing the transaction restores both
`MainForm.Designer.cs` and `MainForm.resx` completely.

The next product roadmap items in `CommandBar-Design_1.md` are Alt/F10 menu
activation, accessibility/UIA, RTL support, and optional Dark-theme icon tint.
