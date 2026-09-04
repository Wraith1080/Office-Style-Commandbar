# CommandBars next-chat handoff

## Resume from the current checkout

Read [AGENTS.md](AGENTS.md), inspect `git status --short` and `git log -5 --oneline`,
then read source relevant to the user's task. Do not assume a branch, absolute
workspace path, package version, or old worktree state from a previous session.
Use [README.md](README.md) for usage and [DESIGNER-SETUP.md](DESIGNER-SETUP.md)
for packaging. The architecture and implementation record is
[CommandBar-Design_1.md](CommandBar-Design_1.md).

## Last documentation update — 2026-09-05

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
