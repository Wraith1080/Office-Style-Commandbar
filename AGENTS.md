# CommandBars contributor instructions

## Product goal

Build a professional C# WinForms commandbar control with dockable and floating
bars, Office XP/2003/2007-style themes, per-monitor DPI awareness, and runtime
customization. Users must be able to create and edit toolbars and menus, toggle
toolbar visibility, select icon sizes, and assign SVG or raster button images.
Preserve the additional implemented themes and Visual Studio designer support.

## Start each task

- Work directly in the current Windows checkout using PowerShell and available
  tools. Do not assume a Linux sandbox, staged file bridge, or fixed machine path.
- Read `git status --short` and relevant source before editing. Preserve existing
  user changes; never reset or overwrite unrelated work. Prefer small patches.
- Read `README.md` for usage, `NEXT-CHAT-HANDOFF.md` for the last recorded state,
  and `DESIGNER-SETUP.md` for packaging and designer verification when relevant.
- Consult `CommandBar-Design_1.md` selectively for architecture and decisions.
  Its implementation log and archived documents are historical evidence, not
  fresh test results or instructions to repeat completed work.
- Resolve discrepancies against current source, project files, and Git history.
  Record material documentation drift instead of assuming old notes are correct.

## Architecture to preserve

- Keep runtime model, controls, renderers, imaging, and persistence separated.
  Theme visuals belong in renderers/color tables; use logical DPI-scaled metrics.
- Commands have stable ids and shared state. Catalog definitions own reusable
  presentation and compound dropdown structure; bars/dropdowns contain placements.
  Separators are structural placements. Preserve per-placement overrides.
- Keep legacy definitions readable and migration explicit. Preserve code-first
  construction and existing layout compatibility unless the task changes them.
- Designer Client owns dialogs, Server owns live component mutations, and
  Protocol owns shared transport/validation. Commit changes through designer
  transactions and change notifications; preserve Undo/Redo and stable serialization.
- Preview bars are unsited views of definitions. Reuse shared pickers and batched
  refreshes instead of independently modifying preview controls.
- Keep runtime and PackageDemo behavior aligned. Respect application-owned bars
  during customization and dispose GDI resources, windows, and event subscriptions.
- Follow surrounding C# style and target-framework constraints, especially the
  net472 Client/Protocol builds. Do not upgrade dependencies or remove the net6
  compatibility target as incidental cleanup.

## Implement and verify

- Complete the requested work with reasonable local decisions. Ask only for
  missing requirements that materially affect scope or behavior.
- Use `rg` for discovery and focused reads. Re-read a file if concurrent edits
  are apparent, and reconcile them before applying a patch.
- Run relevant automated checks on this Windows host. For runtime/model changes:
  `dotnet test CommandBars.Tests/CommandBars.Tests.csproj`.
- For designer changes, build Client and Server, then follow `DESIGNER-SETUP.md`
  to package and test the consuming demo. A direct package-project build does not
  build its source assemblies; build prerequisites explicitly.
- Verify visual changes in the relevant demo/designer when tools permit. Cover
  affected themes, DPI scaling, docking, persistence, and Undo/Redo as applicable.
  Report manual checks that remain unperformed; builds do not prove UI behavior.
- Documentation-only changes need link, command/source consistency, and diff
  checks rather than a package rebuild or GUI launch.
- Review `git diff --check` and the final diff. Do not commit, publish, or change
  package versions merely to update documentation.

## Documentation and handoff

- Keep `AGENTS.md` focused on durable working rules; README owns entry points,
  DESIGNER-SETUP owns package/designer steps, and the design document owns rationale.
- Update affected documentation alongside behavior changes. Avoid duplicate
  instructions, hardcoded checkout paths, model-version assumptions, and stale
  branch/package values presented as current facts.
- In handoffs, identify what changed, checks actually run, limitations, and the
  next actionable work. Label prior-session results as historical.
- Give concise progress updates and finish with the outcome and verification.
  Preserve archive contents and license terms when refreshing working guidance.
