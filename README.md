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

- Office 2000, XP, 2003, 2007, 2010 Silver, Dark, and Visual Studio 2026 Light renderers.
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

## Visual Studio 2026 Light theme

Select **Visual Studio 2026 (Light)** in the demo's View > Theme menu, or set
`manager.Theme = CommandBarTheme.VisualStudio2026` in code or the designer's
Properties window. The stable layout key is `visualstudio2026`.

The Fluent-inspired light theme uses flat rounded toolbars, purple gripper hover,
rounded button/combo states, padded popup rows, subtle separators, and slightly
overlapping submenus. Toolbars retain the standard item padding, with 4 logical
pixels between bars and 3 between a root popup and its owner. Hover backgrounds
are inset; split arrows have wider hit areas with straight shared edges, and
overflow uses three solid square dots. Resting combos expose the toolbar surface.
Combo selections retain a purple vertical marker while
another row is hovered. Overflow menus keep one compact shared icon/check column:
checked icons get a rounded frame, and iconless items get a checkmark.
Generated theme lists use radio dots. Application-owned commands can opt into
that glyph with `command.RadioCheck = true`; exclusive selection remains the
application's responsibility. This runtime presentation property is not a new
catalog/designer field and should be reapplied by application initialization.

Existing application icons are retained. Custom tinting and a Fluent dark variant
are deferred. Rounded surfaces use symmetric pixel coverage rather than GDI+ arc
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

For a fresh checkout, first follow the complete package bootstrap in
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
