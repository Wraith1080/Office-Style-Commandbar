# MiniDraw

A small, usable drawing application built with the code-first CommandBars API.
It references the runtime project directly and requires no local NuGet package
or Visual Studio designer setup.

## Run

From the repository root in PowerShell:

```powershell
dotnet run --project CommandBars.MiniDraw/CommandBars.MiniDraw.csproj --framework net8.0-windows10.0.18362.0
```

Alternatively, set **CommandBars.MiniDraw** as the startup project in Visual
Studio. The example also retains a `net6.0-windows` build target.

## Drawing

- Choose Rectangle, Ellipse or Line in the left toolbar or Shapes menu, then
  drag on the white page. Tools stay selected for repeated drawing.
- Choose Select and click a shape to select it; drag it to move. Clicking empty
  space deselects. Empty-fill shapes are selected by their outlines.
- Fill, Outline and Line width change the selected shape and the defaults for
  the next shape. Selecting a shape reflects its formatting in those commands.
  Fill is disabled for a selected line. More colors opens the standard picker.
- Fill and Outline open compact palettes with twelve colors in two rows of six.
  The current color keeps a selection border. Fill also offers a No fill row.
  The popup has a uniform background behind the grid and text rows.
  Drag the dotted strip at the top to keep a palette open as a floating window.
- Duplicate, Delete, Bring Forward and Send Backward operate on one selected
  shape. The Arrange commands move one position in the stacking order.
- Undo/redo stores up to 100 edits. A complete drag is one edit; Escape or lost
  mouse capture cancels a drag without changing the document.
- View offers a visual grid, Fit page and 50–200% zoom, all built-in themes, toolbar visibility,
  icon sizes and the runtime Customize dialog. The canvas is disabled while
  customizing. The grid does not snap shapes.

| Key | Action |
| --- | --- |
| V / R / E / L | Select / Rectangle / Ellipse / Line while the canvas has focus |
| Arrow / Shift+arrow | Move selection by 1 / 10 document pixels |
| Escape | Cancel a gesture, or return to Select and deselect |
| Ctrl+N / Ctrl+O / Ctrl+S | New / Open / Save |
| Ctrl+Shift+S / Ctrl+E | Save As / Export PNG |
| Ctrl+Z / Ctrl+Y | Undo / Redo |
| Ctrl+D / Delete | Duplicate / Delete selection (Delete requires canvas focus) |
| Ctrl+] / Ctrl+[ | Bring Forward / Send Backward |

## Documents and toolbar layouts

Drawings use a fixed **960 × 640** page in document coordinates. Display DPI and
zoom affect the view, not stored geometry or export dimensions. PNG export has
a white background and omits the grid, selection border and application chrome.

The initial view uses **Fit page**, centering the complete page with a margin
and no scrollbars. It recalculates after resizing, docking changes and DPI
changes, with a maximum of 100%. Choose **View > Zoom > Fit page** to return to
this mode. Choosing a percentage switches to a fixed zoom that stays unchanged
when the window is resized. The status bar shows the mode and actual percentage.
Earlier saved Zoom menus gain the Fit page command while retaining their other
commands and order.

`.minidraw` files are versioned JSON containing geometry, colors, stroke widths
and stacking order. Loading validates the complete file before replacing the
document. Supported files contain up to 5,000 shapes and are at most 8 MB. Saves
write a temporary sibling file before replacing the destination; failed writes
do not clear the modified marker. New, Open and Close offer to save unsaved work.

Toolbar layout and appearance are saved separately on exit to
`%LOCALAPPDATA%\CommandBars\MiniDraw\layout.json`. They never change the drawing's
modified state. Drag toolbar grips to dock on any edge or float. Drag the grip
at the top of Shapes, Fill or Outline menus to create floating palettes. Menu,
toolbar and palette occurrences reuse the same command objects and state.

Use **View > Customize toolbars...** to rearrange or add commands and compound
palettes. **View > Reset toolbar layout** restores the factory arrangement while
retaining the library's appearance preferences. The original demos' layouts are
independent.

## Code map

| File | Responsibility |
| --- | --- |
| `DrawingDocument.cs` | Immutable shapes, hit testing, history, dirty state and validated persistence |
| `DrawingCanvas.cs` | DPI/zoom coordinates, pointer gestures, scrolling, selection and keyboard movement |
| `DrawingPainter.cs` | Shared shape painting for the canvas and PNG export |
| `MainForm.cs` | Command registration, shared state, menus/toolbars, palettes and file dialogs |
| `DrawIcons.cs` | Embedded SVG artwork, theme contrast and a form-owned bitmap cache |
| `Icons/` | Additions matching the original demo icons; New/Open/Save/Copy are linked directly from the demo |
| `SmokeChecks.cs` | Opt-in document and real-control integration verification |

Single selection, moving, basic shapes and a fixed page are deliberate first
version boundaries. Resize handles, text, clipboard formats, multiple documents
and printing are not implemented. Saved future ideas are in [ROADMAP.md](../ROADMAP.md).

## Verification

```powershell
dotnet build CommandBars.MiniDraw/CommandBars.MiniDraw.csproj --framework net8.0-windows10.0.18362.0
dotnet run --project CommandBars.MiniDraw/CommandBars.MiniDraw.csproj --framework net8.0-windows10.0.18362.0 --no-build -- --smoke
dotnet build CommandBars.MiniDraw/CommandBars.MiniDraw.csproj --framework net6.0-windows
```

The smoke run opens and closes a real form, returns a nonzero exit code on
failure, and writes `smoke-output/results.txt`, sample documents, PNG output and
four theme renderings under the selected framework's normal Debug output.
It does not load or save the user's toolbar preferences. Checks cover document
round-trips and rejected files, saved/dirty history, cancelled and committed
canvas gestures, shared command state, layout restoration and multiple zooms.
Fit-page checks cover initial sizing, window resizing, docking, transitions
from a scrolled fixed zoom and upgrading earlier Zoom menus. The output also
includes an icon sheet at 16/24/32 pixels on light and dark backgrounds.
Color-palette checks cover compact popup and detached layouts after restoring
saved preferences, with images of both Fill and Outline.

For manual verification, draw and move shapes, use a detached color palette,
try undo/redo, save and reopen a drawing, and test the save/cancel prompts.
Check customization, dock/float/re-dock and persisted layouts. Move the main
window and palettes between monitors with different DPI settings; automated
zoom checks are not a substitute for live monitor transitions.
