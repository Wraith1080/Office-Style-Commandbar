# CommandBars ideas to revisit

These are saved suggestions, not scheduled work. Revisit priorities after using
MiniDraw to identify the most useful gaps in the library.

## Current example: MiniDraw

`CommandBars.MiniDraw` is a separate code-first drawing application. Its first
version demonstrates rectangle/ellipse/line drawing, selection and movement,
fill/outline/line width, duplicate/delete/stacking, undo/redo, document save/open,
PNG export, docking, tear-off palettes, themes and runtime customization.

Possible later additions, only if useful: resize handles, multi-selection,
alignment, snap-to-grid, text and more AutoShapes. Keep drawing/document logic
inside the example; it is not part of the commandbar library's API.

## Other suggested additions

| Idea | Why it is useful | Suggested first scope | Relative effort |
| --- | --- | --- | --- |
| MiniWriter example | Shows a familiar document application and provides an integration starting point. | A RichTextBox editor with RTF open/save, shared formatting state, font/size controls, clipboard commands and shortcuts. | Medium |
| Searchable command launcher | Makes commands discoverable without navigating menus. Stable command ids and the registry provide a foundation. | Ctrl+Shift+P search, icon/name/shortcut results, keyboard navigation, and execution that respects command availability. Explicit opt-in for commands that need extra context or parameters. | Medium |
| Keyboard shortcut customization | Complements the existing toolbar customization. | Assign/remove shortcuts, show conflicts, restore defaults, and persist overrides by stable command id. Define how changes interact with catalog defaults and older layouts. | Medium |
| Named workspaces | Lets users switch quickly between task-specific arrangements. | Save, rename, switch and remove layouts such as Drawing, Reviewing and Compact, using existing layout persistence. Decide whether appearance preferences travel with a workspace. | Small–medium |

The searchable launcher is separate from the existing `CommandsPalette`, which
is a drag source for toolbar customization.

Suggested sequence from the initial discussion: **MiniDraw → shortcut
customization → named workspaces**. If the next task should be a library feature
instead of another example, the searchable command launcher is another strong
candidate.
