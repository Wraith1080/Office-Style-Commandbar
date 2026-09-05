# CommandBars — Professional Office-Style Command Bars for WinForms

CommandBars is a .NET 8 WinForms control library for building classic Office-style
menu bars and toolbars. Bars can dock on any edge or float, use multiple built-in
themes, scale per monitor, render SVG icons, overflow like Office 2003, persist
their layout, and be customized at run time.

The package also includes out-of-process Visual Studio designer support. Its
catalog-first editor defines each command once and places references to that
command in menus, toolbars, popups, and split-button dropdowns.

See [CommandBar-Design_1.md](CommandBar-Design_1.md) for the architecture,
decisions, implementation stages, and manual designer test matrix.

## Documentation map

- [AGENTS.md](AGENTS.md): contributor workflow and architectural guardrails.
- [DESIGNER-SETUP.md](DESIGNER-SETUP.md): package bootstrap and designer verification.
- [NEXT-CHAT-HANDOFF.md](NEXT-CHAT-HANDOFF.md): recorded baseline and remaining work.
- [CommandBar-Design_1.md](CommandBar-Design_1.md): architecture and implementation history.
- [CommandBar-Design_old.md](CommandBar-Design_old.md) and
  [DESIGNER-SETUP-STAGE2.md](DESIGNER-SETUP-STAGE2.md): historical archives.

Current source and project files determine implemented behavior and build targets.
Historical test results do not replace verification of a new change.

## Current capabilities

- Office 2000, XP, 2003, 2007, 2010 Silver, Dark, and Fluent renderers.
- Top, bottom, left, and right docking; drag-to-float and re-dock.
- Per-monitor DPI-aware bars, popups, editors, property panels, and designer
  affordances.
- Buttons, toggles, labels, separators, popups, split buttons, combo boxes,
  tear-off menus, icon-grid palettes, and dynamic toolbar/theme lists.
- Raster and keyed SVG images, including designer-side SVG import and preview.
- Office-style priority overflow, icon-size selection, toolbar visibility, and
  runtime Customize mode.
- JSON persistence for layout, visibility, custom bars, hosted combos, and
  floating/tear-off state.
- A reusable command catalog: presentation and compound structure are authored
  once, while executable behavior is attached in application code by stable id.
- Visual Studio out-of-process designer editors, `DockHost` smart tags, live
  previews, and per-bar **+** glyphs.

## Fluent theme

Select **Fluent** in the demo's View > Theme menu, or set
`manager.Theme = CommandBarTheme.Fluent` in code or the designer's
Properties window. The stable layout key is `fluent`.
Existing layouts using `visualstudio2026` still load and save back as `fluent`.

The Fluent-inspired light theme uses flat rounded toolbars, purple gripper hover,
rounded button/combo states, padded popup rows, subtle separators, and slightly
overlapping submenus. Toolbars retain the standard item padding, with 4 logical
pixels between bars and 3 between a root popup and its owner. Hover backgrounds
are inset; split arrows have wider hit areas with straight shared edges, and
overflow uses a square highlight with three solid square dots and a trailing
border gap. Resting combos have a white field and border; hovering changes the
field to the toolbar color while retaining the border. Grippers span the bar's
full cross-axis and are clipped by its rounded border. Menu icon frames are square
and inset equally from the highlight's left, top and bottom edges; single-line
separators have balanced spacing above and below.
Toolbar button and combo surfaces match the overflow highlight height.
Toolbar dropdowns, split-button dropdowns, overflow and menu-bar popups align with
their owner's visible left edge when opening above/below, or visible top edge when
opening beside a vertical bar (subject to screen-edge clamping). Menu bars retain
compact rows with taller hover/open highlights inside them; the gap before the
first toolbar row is 2 logical pixels, while toolbar-to-toolbar gaps remain 4.
Icon-only buttons stay square, short captions get a matching minimum width, and longer
captions/dropdowns retain content-based widths. Toolbar images fit inside these
surfaces with padding (SVGs rasterize at the fitted size); the selected icon-size
setting and popup-menu image sizes are unchanged.
Combo selections retain a purple vertical marker while
another row is hovered. Overflow menus keep one compact shared icon/check column:
checked icons get a rounded frame, and iconless items get a checkmark.
Generated theme lists use radio dots. Application-owned commands can opt into
that glyph with `command.RadioCheck = true`; exclusive selection remains the
application's responsibility. This runtime presentation property is not a new
catalog/designer field and should be reapplied by application initialization.

Existing application icons are retained. Custom tinting and a Fluent dark variant
are deferred. Floating toolbars and tear-off palettes use a purple outline,
softly tinted caption with a purple marker, and a rounded close-button highlight.
Tear-offs inherit the source toolbar's current icon size and retain it in saved
layouts. Grid palette separators remain horizontal when detached.
Rounded surfaces use symmetric pixel coverage rather than GDI+ arc
paths. On Windows 11, popup outer corners use DWM's rounded-menu preference;
Windows 10 uses a symmetric region fallback (its outer clip is not antialiased).
Popup shadows and compositor rounding depend on the host's visual-effects policy.

## Requirements

- Windows 10 or later.
- An SDK capable of building the .NET 8 and .NET 6 targets, plus net472 reference assemblies for the designer Client/Protocol.
- Visual Studio 2022 or newer for the WinForms designer. The repository currently
  pins `Microsoft.WinForms.Designer.SDK` in `Directory.Build.props`.

## Build and test

Run from the repository root in PowerShell on Windows. Runtime tests and the
code-built showcase do not require the local CommandBars package:

```powershell
dotnet test CommandBars.Tests/CommandBars.Tests.csproj
dotnet run --project CommandBars.Demo/CommandBars.Demo.csproj --framework net8.0-windows10.0.18362.0
```

Choose verification according to the change:

| Change | Checks |
| --- | --- |
| Documentation only | Check referenced paths, command/source consistency, and `git diff --check`. |
| Runtime/model/rendering or shared Protocol behavior | Run the test project above; add or update regression tests for changed behavior where useful. |
| Runtime source | Also build the retained net6 target using the command below. |
| Designer Client/Server/Protocol or package integration | Follow the prerequisite builds, packaging, consuming demo, and relevant manual checks in [DESIGNER-SETUP.md](DESIGNER-SETUP.md). |
| Demo-only behavior | Build and exercise the affected demo; run library tests if shared behavior changed. |

The net8 test project does not verify compilation of the runtime's net6 target:

```powershell
dotnet build CommandBars/CommandBars.csproj --framework net6.0-windows
```

For a fresh checkout that needs PackageDemo or a full solution build, follow the package bootstrap in
[DESIGNER-SETUP.md](DESIGNER-SETUP.md). PackageDemo pins an exact local package
version, so solution restore can fail until that version exists or its reference
is updated to the newly built package. After bootstrap:

```powershell
dotnet restore CommandBars.sln
dotnet build CommandBars.sln --no-restore
dotnet run --project CommandBars.PackageDemo/CommandBars.PackageDemo.csproj
```

The runtime also targets `net6.0-windows` for compatibility; do not remove that
target as incidental cleanup. The Demo is multi-targeted, so `dotnet run` needs
the explicit framework above. Package builds currently also copy to `E:\Nuget`;
see the setup guide before packaging on another machine.

## Catalog-first designer workflow

1. Add `CommandBarManager`, one `SvgImageList`, and the required `DockHost`
   controls to a form. Assign each host's `Manager` and edge, assign the image
   list to the manager, and keep the form on `PerMonitorV2` DPI mode.
2. Select the manager and open **Edit command catalog...**. Define each semantic
   command once as an Action, Toggle, Popup, Split Button, Combo Box, or Label.
   For popups and split buttons, compose their reusable dropdown contents in the
   lower editor. Separators are structural placements and are not commands.
3. Select a `DockHost` and use **Add toolbar...** or **Add menu bar...**. Use
   **Add commands to...**, or click a preview bar's blue **+** glyph, to place
   catalog commands on that bar.
4. Use **Edit bars and menus...** for a complete tree view of every bar and
   placement. A placement can override display style without duplicating its
   command definition.
5. In the form constructor, register execute handlers by command id, call
   `BuildFromDefinitions()`, and then call `CaptureDefaults()` before loading a
   saved user layout.

The normal designer workflow does not create anonymous toolbar/menu items. If an
older form contains full item trees, the editor shows an explicit migration
preview and changes the working definitions only after **Apply Migration**.

Detailed setup and troubleshooting are in
[DESIGNER-SETUP.md](DESIGNER-SETUP.md).

## Runtime initialization for a designer-authored form

The designer owns command presentation and placement. Application code supplies
behavior using the same ids:

```csharp
public MainForm()
{
    InitializeComponent();

    _manager.Commands.GetOrAdd("file.open", command =>
    {
        command.ExecuteHandler = _ => OpenDocument();
    });

    _manager.Commands.GetOrAdd("format.bold", command =>
    {
        command.IsCheckable = true;
        command.ExecuteHandler = _ => ToggleBold();
    });

    _manager.BuildFromDefinitions();
    _manager.CaptureDefaults();
    _manager.LoadLayout(layoutPath);
}
```

Catalog-backed occurrences share command state, so changing the text, image,
enabled state, or checked state of `format.bold` updates every menu and toolbar
placement.

## Code-first construction

The original runtime fluent API remains supported when no designer authoring is
needed:

```csharp
var manager = new CommandBarManager();
var cut = manager.Commands.Register("edit.cut", command =>
{
    command.Text = "Cu&t";
    command.Shortcut = Keys.Control | Keys.X;
    command.ExecuteHandler = _ => CutSelection();
});

var menu = manager.AddBar("MenuBar", CommandBarType.MenuBar);
var edit = menu.Items.AddPopup("&Edit");
edit.DropDown.Items.AddButton(cut);

var toolbar = manager.AddBar("Standard", CommandBarType.Toolbar);
toolbar.Items.AddButton(cut); // same command and state as the menu occurrence
toolbar.Items.AddSeparator();
```

## Project layout

```text
CommandBars/                    runtime library
CommandBars.Tests/              model, rendering, persistence, and protocol tests
CommandBars.Demo/               code-built feature showcase
CommandBars.Designer.Protocol/  shared designer DTOs and mutation services
CommandBars.Designer.Server/    .NET out-of-process designer/server integration
CommandBars.Designer.Client/    Visual Studio-side editors and dialogs
CommandBars.Package/            local NuGet package containing runtime + designers
CommandBars.PackageDemo/        designer-authored, package-consuming showcase
NuGet/BuildOut/                 local package feed
```
