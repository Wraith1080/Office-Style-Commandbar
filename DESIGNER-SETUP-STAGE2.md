# Out-of-process designer support — Stage 2 (client + protocol round-trip)

Stage 2 makes **"Edit toolbars and menus…"** actually open an editor, and moves
the SVG file dialog off the design server (the source of the earlier freeze).
It adds the two projects the Microsoft model requires — a **Client** (VS-side)
and a **Protocol** (shared transport) — plus server request handlers.

## Why "Edit toolbars and menus…" did nothing

The runtime files had drifted back to **in-process** design references:

- `CommandBarManager` had `[Designer(typeof(CommandBars.Design.CommandBarManagerDesigner))]`
  and `[Editor(typeof(Design.BarDefinitionCollectionEditor), …)]`
- `DockHost` / `SvgImageList` had `[Designer("CommandBars.Design.…, CommandBars")]`

Visual Studio's out-of-process designer **never loads design types from the
control assembly** — so a `typeof(...)`/`…, CommandBars` designer or editor binds
to something that isn't there, and the smart tag / "…" silently do nothing. The
packaged (working) build used the string references to `CommandBars.Designer.Server`;
the on-disk copies had been reverted. This delivery re-points all of them:

- `CommandBarManager` → `[Designer("CommandBars.Designer.Server.CommandBarManagerDesigner, CommandBars.Designer.Server")]`,
  `BarDefinitions` → `[Editor("BarDefinitionsEditor", typeof(UITypeEditor))]`
- `SvgImage.Svg` → `[Editor("SvgMarkupEditor", typeof(UITypeEditor))]`,
  `SvgImageList` / `DockHost` → their `CommandBars.Designer.Server.*` designers.

`"BarDefinitionsEditor"` / `"SvgMarkupEditor"` are **names**, resolved by the
client's `TypeRoutingProvider` to the real editors — exactly how Microsoft's
sample routes `"TemplateAssignmentEditor"`.

## New projects

```
CommandBars.Designer.Protocol   (net8.0-windows10.0.18362.0 ; net472)
  BarDefData.cs                  transport POCOs (+ protocol-local enums)
  DefinitionsSerializer.cs       JSON (de)serialize the snapshot
  Endpoints/EndpointNames.cs     endpoint + editor names
  Endpoints/GetBarDefinitionsEndpoint.cs   request/response (manager proxy -> JSON)
  Endpoints/SetBarDefinitionsEndpoint.cs   request/response (JSON -> apply)

CommandBars.Designer.Client     (net472 — the VS designer host framework)
  TypeRoutingProvider.cs         maps editor names -> client editors
  BarDefinitionsEditor.cs        UITypeEditor: get snapshot, show dialog, set
  BarDefinitionsDialog.cs        tree (bars -> items -> children) + PropertyGrid
  SvgMarkupEditor.cs             UITypeEditor: markup box + in-VS "Load from file…"

CommandBars.Designer.Server     (additions)
  BarDefinitionMapper.cs         runtime BarDefinition <-> transport POCO
  BarDefinitionsHandlers.cs      Get/Set handlers (Set wraps a DesignerTransaction)
```

The **round-trip** for "Edit toolbars and menus…": the client editor asks the
server for a JSON snapshot of `BarDefinitions`, shows the dialog **inside Visual
Studio** (so standard WinForms editing works and nothing freezes), and on OK
sends the edited JSON back; the server rebuilds the real `BarDefinition` objects
in a designer transaction and notifies the change service, so `*.Designer.cs`
regenerates. Definitions travel as one JSON string rather than nested DataPipe
arrays — far more robust for a bars → items → children tree.

## Build-drift fix (important)

The design projects had platform-versioned TFMs (`net8.0-windows10.0.18362.0`)
but the Package read `CommandBars.Designer.Server.dll` from a **`net8.0-windows\`**
folder — a *stale* leftover from the first build. So the package was shipping an
old designer DLL. Now a single `$(CommandBarsDesignTfm)` in `Directory.Build.props`
drives every design project and every package include, so the folder can't drift.

You can delete these stale folders (safe; they'll be recreated correctly):

```
CommandBars.Designer.Server\bin\Debug\net8.0-windows\      (keep net8.0-windows10.0.18362.0)
```

## Build & verify

1. Delete `NuGet\BuildOut\*.nupkg` and the stale `net8.0-windows\` bin folders
   under the design projects (optional but avoids confusion).
2. Build **CommandBars.Package** (it builds CommandBars, Protocol, Server, Client
   first, then packs to `NuGet\BuildOut`).
3. Restore + build the solution so **CommandBars.PackageDemo** picks up the new
   package. Run it once to confirm the runtime still works.
4. Open `CommandBars.PackageDemo\MainForm.cs` in the designer:
   - Select `_manager` → smart tag → **Edit toolbars and menus…**. A dialog with
     a tree (bars → items) and a property grid should open. Add a toolbar, add a
     couple of Button items, edit their Text/CommandId, click **OK** — the bars
     preview should update and `MainForm.Designer.cs` should regenerate.
   - The same dialog opens from the **"…"** on the `BarDefinitions` property.
   - Add or select a Popup/SplitButton item. Set **TearOff** to `True`; optionally
     set **TearOffTitle**, and set **PaletteColumns** to zero for a normal
     detachable menu or a positive count for an icon-grid palette. Click **OK**
     and confirm these values regenerate into `MainForm.Designer.cs`.
   - Select an `SvgImage`'s `Svg` property (via the SvgImageList's Images
     collection) → **"…"** → the markup dialog with **Load from file…** opens
     *in VS* (no freeze).

## Known scope / next (Stage 3 polish)

- The bar/item dialog uses a tree + property grid with typed **Add** buttons
  (Add Toolbar / Add Menu Bar / Add Item ▾). It is functional but plain; a nicer
  drag-reorder / icon-preview pass is possible later.
- The `SvgImageList` **"Import SVG files…"** smart tag now routes to
  `SvgImportEditor` in the Visual Studio client. The client opens the multi-select
  file dialog and sends valid SVG markup through the existing image-add endpoint;
  the server embeds it in a designer transaction. This replaces the intermittent
  server-side common-dialog hang that an SDK 1.13 update had only masked.
- The old in-process `CommandBars.Design.*` editors/designers remain in the
  runtime assembly as dead code; they can be deleted once this is confirmed.

## Troubleshooting

- **Compile errors in Client/Server/Protocol:** the WinForms Designer SDK API
  surface isn't fully documented; paste the exact errors and they're usually
  one-line signature fixes. The code follows Microsoft's TileRepeater sample.
- **"Edit toolbars…" still does nothing:** confirm the newest `.nupkg` in
  `NuGet\BuildOut` contains `Design/WinForms/CommandBars.Designer.Client.dll` and
  `Design/WinForms/CommandBars.Designer.Protocol.dll` (rename to .zip to inspect).
  If missing, the Client project didn't build before the pack.
- **Designer won't load:** try toggling `WinFormsDesignerSdkVersion` in
  `Directory.Build.props` (1.13 preview ↔ 1.6.0) and rebuild the package.
