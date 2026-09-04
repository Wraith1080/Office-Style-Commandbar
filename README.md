# CommandBars — Professional Office-Style CommandBar for WinForms

A reusable WinForms control library that reproduces the classic Microsoft Office
`CommandBars` experience: undockable/floating bars, runtime themes (Office 2000,
XP, 2003, 2007, 2010 Silver, and Dark), DPI-awareness, vector (SVG) icons, and
full runtime customization.

See **CommandBar-Design.md** (in the project root) for the full architecture and
the confirmed v1 decisions.

## Status — Phase 2: first visual milestone (Office 2003)

The object model (phase 1) is now driving real, on-screen bars. Run the demo to
see an Office 2003-styled **menu bar** and **toolbar** you can click.

Phase 1 — object model (unit-tested):

- `Command`, `CommandExecuteContext`, `CommandRegistry` — the action layer
  (`INotifyPropertyChanged`, cancelable execute pipeline, mnemonic handling).
- `CommandBarItem` hierarchy — button, toggle, split button, popup, separator,
  label, combo box; a shared command keeps all its items in sync.
- `CommandBar` + collections with fluent `Add*` helpers, `CommandBarManager`.
- `IImageSource` abstraction.

Phase 2 — rendering + interaction:

- `Rendering/` — swappable `CommandBarRenderer` with Office 2000, XP, 2003,
  2007, 2010 Silver, and Dark built-in themes. Office 2000 includes classic
  flat-gray Win32 bevels and a single raised-slab toolbar gripper; Office 2003
  uses gradient bars and warm orange hover/pressed/checked states. All themes
  share the same renderer abstraction.
- `Controls/` — `CommandBarControl` (layout, painting, hover/press, gripper,
  fires commands), `DockHost` (top band stacking the menu bar + toolbar), and
  `CommandBarPopupWindow` (dropdown menus with icons, shortcuts, checks).
- `Imaging/BitmapImageSource` — raster image source (SVG is phase 4).
- `CommandBars.Demo` — a runnable WinForms app wiring it all together.

Not yet implemented (later phases): drag-to-float/undock, DPI rescaling, SVG
icons, runtime customization, and the Office XP / 2007 themes.

## Requirements

- Windows (the library targets `net8.0-windows` / WinForms)
- .NET 8 SDK and Visual Studio 2022 (17.8+) or `dotnet` CLI

> Note: this Phase 1 checkpoint was authored in a Linux cloud sandbox where the
> Windows Desktop targeting pack is unavailable, so it was **not compiled there**.
> Build and run the tests on your Windows machine with the commands below.

## Build & test

```powershell
cd "Professional Office Style Commandbar - Winform"
dotnet restore
dotnet build
dotnet test

# Run the demo (Office 2003 menu bar + toolbar):
dotnet run --project CommandBars.Demo
```

## A taste of the fluent API

```csharp
var mgr = new CommandBarManager();

var cut = mgr.Commands.Register("edit.cut", c =>
{
    c.Text = "Cu&t";
    c.Shortcut = Keys.Control | Keys.X;
    c.ExecuteHandler = _ => DoCut();
});

var menu = mgr.AddBar("MenuBar", CommandBarType.MenuBar);
var edit = menu.Items.AddPopup("&Edit");
edit.DropDown.Items.AddButton(cut);

var toolbar = mgr.AddBar("Standard", CommandBarType.Toolbar);
toolbar.Items.AddButton(cut);      // same command, always in sync
toolbar.Items.AddSeparator();
```

## Project layout

```
CommandBars.sln
CommandBars/                 class library (net8.0-windows)
  Model/                     Command, items, bars, enums, collections
  Imaging/                   IImageSource abstraction
  CommandBarManager.cs
CommandBars.Tests/           xUnit tests for the model
CommandBar-Design.md         architecture & decisions
```
