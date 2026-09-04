# CommandBars next-chat handoff

## Repository state

- Workspace: `C:\Users\Rahmat Irfan\Claude\Projects\Professional Office Style Commandbar - Winform`
- Branch: `codex/catalog-first-designer`
- Main architecture and progress log: `CommandBar-Design_1.md`
- Current setup/test guide: `DESIGNER-SETUP.md`
- PackageDemo consumes local package `CommandBars.Package 1.269.50246`.

Inspect `git status`, `git log`, and the latest section of
`CommandBar-Design_1.md` before changing files. Do not assume this handoff's
worktree list is still current after the user commits or resets.

## Catalog-first redesign status

Stages 1–7 are implemented:

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

## Stage 8 verification completed

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

## Stage 8 manual verification completed

The PackageDemo designer opens with all bars and no diagnostics. Saving,
closing, reopening, and saving without a semantic edit produces no generated-file
churn. Changing the manager theme and undoing the transaction restores both
`MainForm.Designer.cs` and `MainForm.resx` completely.

The next product roadmap items in `CommandBar-Design_1.md` are Alt/F10 menu
activation, accessibility/UIA, RTL support, and optional Dark-theme icon tint.
