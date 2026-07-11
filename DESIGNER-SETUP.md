# Out-of-process designer support — Stage 1 (build & verify guide)

This implements the parked plan in `CommandBar-Design_1.md` §9: a real
design-time assembly apparatus for VS's out-of-process WinForms designer,
following Microsoft's TileRepeater reference sample and the control-library
NuGet package spec.

## What was added

```
Directory.Build.props            WinFormsDesignerSdkVersion=1.6.0 (central pin)
Directory.Build.targets          injects Microsoft.WinForms.Designer.SDK where UseDesignerSDK=true
NuGet.config                     local feed NuGet\BuildOut for CommandBars.Package*
NuGet\BuildOut\                  the local feed (packages land here; folder must exist)
CommandBars.Designer.Server\     net8.0-windows design-time assembly (SDK-based designers)
  DockHostDesigner.cs              live preview refresh on any surface change
  CommandBarManagerDesigner.cs     smart tag: Theme, "Edit toolbars and menus…", "Refresh design preview"
  SvgImageListDesigner.cs          smart tag: "Import SVG files…", "Edit images…"
  TypeRoutingProvider.cs           server-side designer-name routing
CommandBars.Package\             packs runtime + designer DLLs into the NuGet layout:
                                   lib/net8.0-windows/               CommandBars.dll
                                   lib/net8.0-windows/Design/WinForms/Server/  CommandBars.Designer.Server.dll
CommandBars.PackageDemo\         WinExe consuming CommandBars.Package Version="*" — the designer test bed
```

Changed in existing code (all backward-compatible):

- `CommandBars/CommandBarManager.cs` — `[Designer]` now points (by string) at
  `CommandBars.Designer.Server.CommandBarManagerDesigner`.
- `CommandBars/Controls/DockHost.cs` — same re-point for `DockHostDesigner`.
- `CommandBars/Imaging/SvgImageList.cs` — same re-point for `SvgImageListDesigner`.
- `CommandBars/CommandBars.csproj` — `InternalsVisibleTo("CommandBars.Designer.Server")`.
- `CommandBars.sln` — three new projects + build-order dependencies.

`CommandBars.Demo` (project reference) is untouched and keeps working exactly
as before — with a project reference the new designer strings simply fall back
to the defaults, like today.

## First build (order matters once)

1. Open `CommandBars.sln`. Let NuGet restore run (it will pull
   `Microsoft.WinForms.Designer.SDK 1.6.0` for the Designer.Server project).
   **`CommandBars.PackageDemo` restore will fail at this point** — expected;
   the package doesn't exist yet.
2. Right-click **CommandBars.Package** → **Build**. This builds `CommandBars`
   and `CommandBars.Designer.Server` first (solution dependencies), packs
   `CommandBars.Package.<date-version>.nupkg`, and copies it into
   `NuGet\BuildOut`.
3. Right-click the solution → **Restore NuGet Packages** (or just rebuild the
   solution). `CommandBars.PackageDemo` should now restore and build.
4. Run `CommandBars.PackageDemo` once to confirm the runtime works end to end
   (menu bar + Standard toolbar with three SVG icons).

## Verifying the designer (the actual test)

Open `CommandBars.PackageDemo\MainForm.cs` in the **designer** and check, in
this order:

1. **Does the form open at all** with the bars previewing in the top dock host?
   (If the designer white-screens or reports load errors, see Troubleshooting.)
2. Select `_manager` in the component tray → is there a **smart tag** (⯈) with
   *Theme*, *Edit toolbars and menus…*, *Refresh design preview*? Do the last
   two also appear in the right-click menu?
3. Select `_svgImages` → smart tag with **Import SVG files…**? Click it — a
   file dialog should appear (it runs in the DesignToolsServer process; if no
   dialog ever shows, tell me — that moves to the client side in stage 2).
4. Open *Edit toolbars and menus…* (or the `BarDefinitions` property), change
   the Standard toolbar's **IconSize** from 16 to 32 → the preview should
   resize **immediately**, not only after clicking the host (that's
   `DockHostDesigner`/`CommandBarManagerDesigner` live refresh working).

Note what still looks unchanged in stage 1: the collection editors still show a
plain **Add** (typed Add-dropdowns are stage 2/3 work), and `SvgImage.Browse` /
`Svg` still use the built-in FileNameEditor / MultilineStringEditor fallbacks.

## Iterating after a change

The designer caches the package by version. After changing anything in
`CommandBars` or `CommandBars.Designer.Server`:

1. Close the MainForm designer tab.
2. Build **CommandBars.Package** (new date-based version lands in the feed).
3. Restore/rebuild **CommandBars.PackageDemo**, reopen the designer.

Old `.nupkg` files pile up in `NuGet\BuildOut` — safe to delete anytime.

## Troubleshooting

- **Designer load error / types not found:** VS 2026 (v18) may need a newer
  SDK than the 1.6.0 stable. Change `WinFormsDesignerSdkVersion` in
  `Directory.Build.props` to `1.13.0-preview.2.24575.3`, rebuild
  CommandBars.Package, restore, retry. Report the exact error text either way.
- **Smart tags missing but form loads:** the designer assembly probably didn't
  load. Check `NuGet\BuildOut`'s newest .nupkg with NuGet Package Explorer (or
  rename to .zip): `lib/net8.0-windows/Design/WinForms/Server/CommandBars.Designer.Server.dll`
  must be present.
- **Restore can't find CommandBars.Package:** confirm `NuGet\BuildOut` contains
  a .nupkg and that VS picked up the solution-root `NuGet.config` (close and
  reopen the solution after the first pack).
- **"The imported project ... Directory.Build.targets" style errors in other
  projects:** shouldn't happen (everything is conditional), but report if so.
