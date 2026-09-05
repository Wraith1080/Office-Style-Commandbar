# CommandBars next-chat handoff

## Resume from the current checkout

Read [AGENTS.md](AGENTS.md), inspect `git status --short` and `git log -5 --oneline`,
then read source relevant to the user's task. Do not assume a branch, absolute
workspace path, package version, or old worktree state from a previous session.
Use [README.md](README.md) for usage and [DESIGNER-SETUP.md](DESIGNER-SETUP.md)
for packaging. The architecture and implementation record is
[CommandBar-Design_1.md](CommandBar-Design_1.md).

## Visual Studio 2026 Light implementation — 2026-09-05

### Visual refinement follow-up

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
