# CommandBars contributor instructions

Build a professional C# WinForms commandbar with dockable/floating bars,
Office-style themes, per-monitor DPI, runtime toolbar/menu customization,
visibility and icon-size selection, SVG/raster images, and Visual Studio designer
support. Preserve the implemented themes and code-first API.

## Scope and completion

- Work in the current Windows checkout with PowerShell. Inspect Git status and
  preserve existing user changes.
- Carry the requested work through implementation, relevant verification, and
  fixes for failures it introduces. Make routine local decisions without pausing
  for approval; ask when missing information materially changes scope or behavior.
- Repository guidance describes defaults; explicit user instructions can change
  the task scope. Do not infer permission to commit or publish from a local edit.

## Context on demand

- [README.md](README.md): usage, project layout, and build/test entry points.
- [DESIGNER-SETUP.md](DESIGNER-SETUP.md): packaging and designer verification.
- [NEXT-CHAT-HANDOFF.md](NEXT-CHAT-HANDOFF.md): current summary when resuming work;
  consult older sections only for relevant history.
- [CommandBar-Design_1.md](CommandBar-Design_1.md): architecture and rationale.

Read the sections needed for the task. Current source/project files determine
behavior and build targets; logs and archives record historical results.

## Architecture to preserve

- Separate model, controls, renderers, imaging, and persistence. Theme visuals
  belong in renderers/color tables and use logical DPI-scaled metrics.
- Commands have stable ids and shared state. Catalog definitions own reusable
  presentation and compound dropdown structure; bars/dropdowns hold placements.
  Separators are structural placements. Preserve placement overrides, legacy
  layout readability, and explicit migration.
- Designer Client owns dialogs, Server owns live mutations, and Protocol owns
  transport/validation. Use designer transactions and change notifications to
  preserve Undo/Redo and serialization. Preview bars are unsited definition views;
  reuse shared pickers and batched refreshes.
- Keep runtime and PackageDemo behavior aligned. Protect application-owned bars
  during customization; dispose GDI resources, windows, and event subscriptions.
- Preserve net472 Client/Protocol and runtime net6 compatibility. Avoid incidental
  dependency upgrades or framework changes.

## Verification and handoff

- Use the change-specific checks in [README.md](README.md#build-and-test).
  For designer changes, follow [DESIGNER-SETUP.md](DESIGNER-SETUP.md): build source
  assemblies before packaging; the package project does not build prerequisites.
- Verify affected visuals and interactions in the relevant demo/designer when
  tools permit, including theme/DPI, docking, persistence, or Undo/Redo as applicable.
  Builds do not prove UI behavior; identify unperformed manual checks.
- Once relevant checks pass, repeat or broaden them only for new edits, failures,
  or unresolved risks. Review the final diff and run `git diff --check`.
- Update the documentation that owns changed behavior. Keep handoffs focused on
  current outcomes, actual checks, blockers, and next work; label history clearly.
  Preserve archives and license terms. Documentation-only work does not require
  package version changes, builds, or GUI launches.
