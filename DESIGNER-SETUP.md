# Visual Studio designer setup and verification

CommandBars uses Visual Studio's out-of-process WinForms designer architecture.
The design-time client, protocol, and server assemblies are loaded from the NuGet
package layout; a direct project reference to `CommandBars.csproj` runs the
control but does not activate these packaged designer extensions.

## Build the local package

Run these steps from the repository root in PowerShell on Windows. Close open
PackageDemo form designer tabs first so Visual Studio can release old assemblies.

The package project packs existing output files; it has no ProjectReferences to
build those assemblies when invoked directly. Build Server (which builds the
runtime and .NET Protocol transitively), then Client (which builds net472 Protocol),
then Package, using the same configuration throughout:

```powershell
dotnet build CommandBars.Designer.Server/CommandBars.Designer.Server.csproj --configuration Debug
dotnet build CommandBars.Designer.Client/CommandBars.Designer.Client.csproj --configuration Debug
dotnet build CommandBars.Package/CommandBars.Package.csproj --configuration Debug
```

The package project's `CopyPackage` target currently writes to both
`NuGet/BuildOut` and the machine-specific `E:\Nuget` destination. Check that
external destination before packaging on another machine. If it is unavailable,
the copy target can fail; adapting that target is a separate build-configuration
change, not a reason to delete feeds or clear the global NuGet cache.

1. Note the exact package version emitted by the successful build in `NuGet/BuildOut`.
2. Set PackageDemo's `CommandBars.Package` PackageReference to that exact version.
3. Force-restore and build the consuming demo, then reopen its designer:

```powershell
dotnet restore CommandBars.PackageDemo/CommandBars.PackageDemo.csproj --force
dotnet build CommandBars.PackageDemo/CommandBars.PackageDemo.csproj --configuration Debug
```

The solution-root `NuGet.config` registers `NuGet/BuildOut` as a local source.
Bootstrap the package before restoring the full solution on a fresh checkout;
otherwise PackageDemo's pinned version may not exist. Solution build dependencies
order the designer assemblies before packaging, but do not replace this initial
restore prerequisite.

Versions use local time with minute precision (`1.yM.dHHmm`), so two builds within
the same minute can reuse a version. Confirm a changed package version when
validating new designer binaries, and close/reopen the designer after restoring.
Preserve any package versions still referenced by consuming projects.

## Build configuration sources

Read the actual project properties and package include paths when diagnosing
build drift. `WinFormsDesignerSdkVersion` is centralized in `Directory.Build.props`
and applied through `Directory.Build.targets`. The framework helper properties
in that props file are not currently used by the designer project targets:

| Project | Declared target frameworks |
| --- | --- |
| Runtime | `net8.0-windows;net6.0-windows` |
| Designer Server | `net8.0-windows` |
| Designer Protocol | `net8.0-windows;net472` |
| Designer Client | `net472` (C# 10) |
| Package | `net8.0` package asset layout |
| PackageDemo | `net8.0-windows` |
| Demo | `net8.0-windows10.0.18362.0;net6.0-windows` |

The net472 builds need the corresponding reference assemblies. Existing comments
about platform-versioned designer targets are historical; match output paths to
the actual project values above. Do not switch the Designer SDK pin speculatively.

## Initial component wiring

For a new form:

1. Set the application/form DPI mode to `PerMonitorV2`.
2. Add one `CommandBarManager` and, optionally, one `SvgImageList` to the
   component tray.
3. Add a `DockHost` for each edge the application uses. Set every host's
   `Manager` and `Edge`; dock the host to the matching form edge.
4. Assign the image list to `CommandBarManager.Images`.
5. Use the manager or host smart tags to create the catalog and bars. Do not edit
   the serialized collections manually.
6. At runtime, register command handlers before calling
   `CommandBarManager.BuildFromDefinitions()`.

## Current authoring surfaces

### CommandBarManager

- **Edit command catalog...** opens the Commands page.
- **Edit bars and menus...** opens the Bars and Menus page.
- The Commands page creates and edits Action, Toggle, Popup, Split Button,
  Combo Box, and Label definitions.
- The dropdown-content panel edits reusable Popup and Split Button children.
- The Bars and Menus page creates bars and adds only catalog references plus
  structural separators.
- **Refresh design preview** forces a rebuild when diagnosing stale visuals;
  normal edits use a batched incremental refresh.

### DockHost

- **Add toolbar...** creates a toolbar initially docked to the selected host.
- **Add menu bar...** is available only on the top host and only when a menu bar
  does not already exist.
- **Add commands to...** first chooses a visible bar on that host, then opens the
  shared command picker.
- **Edit bars and menus...** and **Edit command catalog...** open the same manager
  editor pages described above.
- **Refresh design preview** is the manual fallback.

Each visible preview bar also has a DPI-scaled blue **+** glyph. It targets that
bar directly and reuses the same command picker. Preview bars are deliberately
unsited controls; edits always modify their backing definitions.

## Catalog-first rules

- A semantic command is defined once in `CommandDefinitions` and identified by
  a stable id.
- Bars and compound dropdowns store lightweight placements referencing that id.
- Separators belong to placements and never appear in the command catalog.
- Popup and Split Button definitions own their reusable dropdown hierarchy.
- A placement may override display style, visibility, grouping, name, and
  overflow priority without copying the command's presentation.
- Command removal is blocked while usages exist. Id renames update all
  references atomically.
- Cycles, missing references, duplicate ids, and target-incompatible placements
  are reported before the editor can commit.
- Legacy full-item definitions remain readable, but the designer requires an
  explicit preview-and-apply migration before new catalog-first editing.

## Manual verification

1. Open `CommandBars.PackageDemo/MainForm.cs` in the designer. Confirm the menu
   bar and all top/left/bottom toolbar previews appear without errors.
2. Change a command icon or caption in **Edit command catalog...**, click **OK**,
   and confirm every occurrence updates after the short batched refresh.
3. Change a bar's icon size in **Edit bars and menus...** and confirm only one
   coordinated preview refresh occurs.
4. Use a host smart tag to add a toolbar, then add commands. Use Visual Studio
   Undo and Redo and confirm definitions and previews move together.
5. Click the blue **+** glyph on bars at each used edge and verify the chosen
   commands go only to the targeted bar.
6. Add the same command to a menu and a toolbar. Edit its catalog caption and
   verify both occurrences change.
7. Add a Popup, Split Button, and Combo Box to valid targets. Verify their full
   compound behavior survives designer save, close, reopen, and runtime launch.
8. Set a toolbar Popup placement to `UseCatalogDisplayStyle = false` and
   `DisplayStyle = TextOnly`; verify preview and runtime both show text only.
9. Move Visual Studio between 100%, 150%, and 200% displays. Verify the manager
   property panel keeps a usable logical width and its labels, buttons, picker
   thumbnails, and per-bar glyphs remain correctly scaled.
10. Save, close, and reopen the form without making another semantic edit. A
    second save should not churn generated definitions.

## Troubleshooting

- **The form says to set Manager/add BarDefinitions:** ensure every `DockHost`
  references the same manager and the current generated placements have
  non-empty command ids. Rebuild the current package before reopening the form.
- **Many missing-reference diagnostics:** confirm the generated
  `CommandPlacementDefinition.CommandId` assignments are present and that the
  PackageDemo references the package version containing the matching catalog.
- **Smart tags are missing:** inspect the newest package and confirm these files
  exist under its `lib/net8.0/Design/WinForms` tree: Client and Protocol at the
  root, Server and its Protocol copy in `Server/`.
- **An edit works only after reopening the designer:** close the designer, build
  a new package version, force-restore PackageDemo, and reopen. Visual Studio may
  still have the previous designer DLL loaded.
- **The editor is slow after clicking OK:** use **Refresh design preview** only as
  a diagnostic. Normal commits should be coalesced by the manager; duplicate
  host-level refresh listeners indicate an old package is loaded.
- **Client/server compile failures:** inspect the exact diagnostic, centralized SDK pin, and actual project targets using the build configuration sources above.

`DESIGNER-SETUP-STAGE2.md` is retained only as implementation history; this file
is the current setup and verification guide.
