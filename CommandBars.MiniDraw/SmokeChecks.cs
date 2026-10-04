using System.Drawing.Imaging;
using System.Text.Json;
using CommandBars.Controls;
using CommandBars.Model;
using CommandBars.Rendering;

namespace CommandBars.MiniDraw;

/// <summary>Opt-in integration checks: isolated documents/layouts, real controls, nonzero exit on failure.</summary>
internal static class SmokeChecks
{
    private static readonly List<string> Results = new();
    private static void Require(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
        Results.Add("PASS " + message);
    }

    public static void Run()
    {
        string output = Path.Combine(AppContext.BaseDirectory, "smoke-output");
        Directory.CreateDirectory(output);
        try
        {
            ModelChecks(output);
            using var form = new MainForm(persistLayout: false);
            form.Shown += (_, _) => form.BeginInvoke(new Action(() =>
            {
                try { InterfaceChecks(form, output); }
                catch (Exception exception) { Results.Add("FAIL " + exception); Environment.ExitCode = 1; }
                finally { form.Close(); }
            }));
            Application.Run(form);
        }
        catch (Exception exception) { Results.Add("FAIL " + exception); Environment.ExitCode = 1; }
        File.WriteAllLines(Path.Combine(output, "results.txt"), Results);
    }

    private static DrawingShape Shape(ShapeKind kind = ShapeKind.Rectangle) => new(Guid.NewGuid(), kind,
        60, 60, 240, 180, Color.SkyBlue.ToArgb(), Color.Navy.ToArgb(), 2);

    private static void ModelChecks(string output)
    {
        var document = new DrawingDocument();
        Require(!document.IsDirty && !document.CanUndo && !document.CanRedo, "New document starts clean");
        var rectangle = Shape();
        document.Add(rectangle);
        Require(document.IsDirty && document.Selected == rectangle, "Draw selects shape and marks document dirty");
        string path = Path.Combine(output, "roundtrip.minidraw");
        document.Save(path);
        document.ReplaceSelected(rectangle.Offset(15, 10), "Move");
        document.Undo();
        Require(!document.IsDirty && document.Selected == rectangle, "Undo to saved content clears dirty state");
        document.Redo();
        Require(document.IsDirty && document.Selected!.X1 == 75, "Redo restores move");
        document.Undo();
        document.ReplaceSelected(rectangle with { FillArgb = Color.Gold.ToArgb() }, "Fill");
        Require(!document.CanRedo && document.IsDirty, "New edit drops redo branch without losing saved marker");
        document.Duplicate();
        Require(document.Shapes.Count == 2 && document.Shapes.Select(shape => shape.Id).Distinct().Count() == 2,
            "Duplicate creates a distinct shape id");
        var duplicate = document.Selected!;
        document.Reorder(-1);
        Require(document.Shapes[0].Id == duplicate.Id, "Send backward changes stacking order");
        document.Undo();
        Require(document.Shapes[1].Id == duplicate.Id, "Undo restores stacking order");
        document.Delete();
        Require(document.Selected is null && document.Shapes.Count == 1, "Delete clears selection");
        document.Undo();
        Require(document.Shapes.Count == 2, "Undo restores deleted shape");
        document.Add(Shape(ShapeKind.Ellipse));
        var reverseLine = Shape(ShapeKind.Line) with { X1 = 300, Y1 = 350, X2 = 450, Y2 = 200 };
        document.Add(reverseLine);
        document.Save(path);
        var loaded = new DrawingDocument();
        loaded.Load(path);
        Require(document.Shapes.SequenceEqual(loaded.Shapes) && !loaded.IsDirty && !loaded.CanUndo,
            "All shape kinds, colors, stacking and reverse line endpoints round-trip");
        Require(reverseLine.HitTest(new PointF(375, 275), 3) && !reverseLine.HitTest(new PointF(300, 200), 3), "Line hit testing follows the segment");
        var ellipse = Shape(ShapeKind.Ellipse);
        Require(ellipse.HitTest(new PointF(150, 120), 2) && !ellipse.HitTest(new PointF(60, 60), 2), "Ellipse hit testing excludes bounding-box corners");
        Require(!(rectangle with { FillArgb = Color.Transparent.ToArgb() }).HitTest(new PointF(150, 120), 2),
            "Empty fill does not intercept clicks inside the shape");
        var clamped = document.ClampMove(rectangle, -500, 1000);
        Require(clamped.Bounds.Left == 0 && clamped.Bounds.Bottom == DrawingDocument.PageHeight, "Moves stay on the page");

        var saved = loaded.Shapes.ToArray();
        string invalid = Path.Combine(output, "invalid.minidraw");
        foreach (string json in new[]
        {
            "{ broken", "null", "{\"Version\":2}", "{\"Width\":1}", "{\"Shapes\":[null]}",
            JsonSerializer.Serialize(new DrawingFile { Shapes = new() { rectangle, rectangle } }),
            JsonSerializer.Serialize(new DrawingFile { Shapes = new() { rectangle with { StrokeWidth = -1 } } }),
            JsonSerializer.Serialize(new DrawingFile { Shapes = new() { rectangle with { X2 = 2000 } } }),
            JsonSerializer.Serialize(new DrawingFile { Shapes = new() { rectangle with { Kind = (ShapeKind)99 } } })
        })
        {
            File.WriteAllText(invalid, json);
            bool rejected = false;
            try { loaded.Load(invalid); }
            catch (Exception exception) when (exception is InvalidDataException or JsonException) { rejected = true; }
            Require(rejected && loaded.Shapes.SequenceEqual(saved), "Invalid file rejected without replacing current drawing");
        }
        loaded.Select(saved[0].Id);
        loaded.ReplaceSelected(saved[0] with { FillArgb = Color.Pink.ToArgb() }, "Fill");
        bool saveFailed = false;
        try { loaded.Save(Path.Combine(output, "missing-directory", "drawing.minidraw")); }
        catch (IOException) { saveFailed = true; }
        Require(saveFailed && loaded.IsDirty, "Failed save does not clear dirty state");
        DrawingPainter.ExportPng(document, Path.Combine(output, "drawing.png"));
        using var png = new Bitmap(Path.Combine(output, "drawing.png"));
        Require(png.Size == new Size(960, 640) && png.GetPixel(900, 600).ToArgb() == Color.White.ToArgb(),
            "PNG export has fixed page dimensions and a white background");
        Require(png.GetPixel(150, 120).ToArgb() != Color.White.ToArgb(), "PNG contains the drawn shapes");
        var bounded = new DrawingDocument();
        bounded.Add(rectangle);
        for (int i = 0; i < 120; i++) bounded.ReplaceSelected(rectangle with { X1 = 61 + i % 2 }, "Move");
        int undos = 0;
        while (bounded.CanUndo) { bounded.Undo(); undos++; }
        Require(undos == 100, "Undo history is bounded at 100 edits");
    }

    private static void InterfaceChecks(MainForm form, string output)
    {
        var document = form.Document;
        var manager = form.Manager;
        var canvas = form.Canvas;
        Application.DoEvents();
        Require(canvas.FitPage && manager.Commands["zoom.fit"].Checked == CommandCheckState.Checked,
            "Startup selects Fit page mode");
        RequirePageFits(canvas, "Default window shows the complete page without scrollbars");
        var initialSize = form.ClientSize;
        form.ClientSize = new Size(720, 540);
        Application.DoEvents();
        RequirePageFits(canvas, "Fit page follows a smaller window");
        float smallZoom = canvas.Zoom;
        form.ClientSize = initialSize;
        Application.DoEvents();
        RequirePageFits(canvas, "Fit page follows a larger window");
        Require(canvas.Zoom > smallZoom, "Fit page uses available space after resizing");
        manager.Commands["zoom.200"].Perform();
        canvas.AutoScrollPosition = new Point(250, 150);
        form.ClientSize = new Size(760, 560);
        Application.DoEvents();
        Require(!canvas.FitPage && canvas.Zoom == 2 && (canvas.HorizontalScroll.Visible || canvas.VerticalScroll.Visible),
            "Explicit zoom stays fixed across resizing and permits scrolling");
        manager.Commands["zoom.fit"].Perform();
        Application.DoEvents();
        RequirePageFits(canvas, "Fit page clears scroll offsets and scrollbars from explicit zoom");
        Require(canvas.AutoScrollPosition == Point.Empty && manager.Commands["zoom.200"].Checked == CommandCheckState.Unchecked,
            "Fit mode resets viewport origin and percentage selection");
        form.ClientSize = initialSize;
        Application.DoEvents();
        Require(!manager.Commands["edit.delete"].Enabled && !manager.Commands["edit.undo"].Enabled, "Selection commands start disabled");
        Draw(form, "Rectangle", 100, 100, 320, 230);
        manager.Commands["tool.Rectangle"].Perform();
        Require(manager.FindBar("Drawing")!.Items.OfType<CommandBarToggleButton>().Single(item => item.Command.Id == "tool.Rectangle").Checked,
            "Active tool stays visually selected when clicked again");
        Require(document.Shapes.Count == 1 && document.Selected?.Kind == ShapeKind.Rectangle && manager.Commands["edit.delete"].Enabled,
            "Canvas rectangle gesture updates shared command state");
        manager.Commands["fill.Gold"].Perform();
        Require(document.Selected!.FillArgb == canvas.FillColor.ToArgb() && manager.Commands["fill.Gold"].Checked == CommandCheckState.Checked,
            "Color palette applies to selection and checks shared command");
        manager.Commands["width.6"].Perform();
        Require(document.Selected!.StrokeWidth == 6, "Line width changes selected shape");
        var original = document.Selected;
        manager.Commands["tool.Select"].Perform();
        canvas.Pointer(MouseButtons.Left, new PointF(160, 160), "down");
        canvas.Pointer(MouseButtons.Left, new PointF(200, 195), "move");
        Require(document.Selected == original, "Drag preview leaves document/history untouched");
        canvas.CancelGesture();
        Require(document.Selected == original, "Cancelled drag preserves original shape");
        canvas.Pointer(MouseButtons.Left, new PointF(160, 160), "down");
        canvas.Pointer(MouseButtons.Left, new PointF(200, 195), "up");
        Require(document.Selected!.X1 > original!.X1 + 38, "Selection drag commits a move");
        manager.Commands["edit.undo"].Perform();
        Require(document.Selected == original, "One undo reverses an entire drag");
        manager.Commands["edit.redo"].Perform();
        Draw(form, "Ellipse", 420, 150, 650, 330);
        Draw(form, "Line", 350, 390, 600, 280);
        Require(document.Shapes.Count == 3 && document.Selected!.Y1 > document.Selected.Y2, "Canvas draws ellipse and reverse-direction line");
        Require(!manager.Commands["fill.Gold"].Enabled, "Fill commands disable for a selected line");
        manager.Commands["edit.duplicate"].Perform();
        Require(document.Shapes.Count == 4, "Duplicate command operates on canvas selection");
        manager.Commands["edit.delete"].Perform();
        manager.Commands["edit.undo"].Perform();
        Require(document.Shapes.Count == 4, "Delete and undo command integration");
        document.Select(document.Shapes[0].Id);
        Require(manager.Commands["fill.Gold"].Checked == CommandCheckState.Checked && manager.Commands["width.6"].Checked == CommandCheckState.Checked,
            "Selecting a different shape restores formatting checks");
        var formatting = manager.FindBar("Formatting")!;
        formatting.Dock = DockState.Bottom;
        manager.RefreshLayout();
        Application.DoEvents();
        RequirePageFits(canvas, "Fit page follows toolbar docking changes");
        var viewMenu = manager.FindBar("Menu")!.Items.OfType<CommandBarPopupItem>().Single(item => item.DisplayText == "View").DropDown;
        var zoomMenu = viewMenu.Items.OfType<CommandBarPopupItem>().Single(item => item.DisplayText == "Zoom").DropDown;
        zoomMenu.Items.RemoveAt(0); // Fit page did not exist in the earlier layout.
        zoomMenu.Items.RemoveAt(0); // Its separator did not exist either.
        var legacyZoomIds = zoomMenu.Items.OfType<CommandBarCommandItem>().Select(item => item.Command.Id).ToArray();
        using var layout = new MemoryStream();
        manager.SaveLayout(layout);
        manager.ResetToDefaults();
        layout.Position = 0;
        manager.LoadLayout(layout);
        form.EnsureFitPageMenu();
        Require(manager.FindBar("Formatting")!.Dock == DockState.Bottom, "Toolbar docking survives layout round-trip");
        var restoredView = manager.FindBar("Menu")!.Items.OfType<CommandBarPopupItem>().Single(item => item.DisplayText == "View").DropDown;
        var restoredZoom = restoredView.Items.OfType<CommandBarPopupItem>().Single(item => item.DisplayText == "Zoom").DropDown;
        var restoredIds = restoredZoom.Items.OfType<CommandBarCommandItem>().Select(item => item.Command.Id).ToArray();
        Require(restoredIds[0] == "zoom.fit" && restoredIds.Skip(1).SequenceEqual(legacyZoomIds),
            "Earlier layouts gain Fit page while preserving existing Zoom commands and order");
        form.EnsureFitPageMenu();
        Require(restoredZoom.Items.OfType<CommandBarCommandItem>().Count(item => item.Command.Id == "zoom.fit") == 1,
            "Fit page menu update is idempotent");
        var fill = (CommandBarPopupItem)manager.FindBar("Formatting")!.FindItem("fill.palette")!;
        var fillItem = fill.DropDown.Items.OfType<CommandBarCommandItem>().First(item => item.Command.Id == "fill.Gold");
        Require(fillItem is CommandBarToggleButton && ReferenceEquals(fillItem.Command, manager.Commands["fill.Gold"]) && fill.DropDown.TearOffKey == "minidraw.fill",
            "Restored palettes preserve shared commands and stable tear-off identity");
        fillItem.Command.Perform();
        CheckColorPalette(form, "fill", output);
        CheckColorPalette(form, "outline", output);
        manager.ResetToDefaults();
        foreach (var theme in new[] { CommandBarTheme.Office2003, CommandBarTheme.Fluent, CommandBarTheme.Dark, CommandBarTheme.VistaAurora })
        {
            manager.Theme = theme;
            Application.DoEvents();
            using var bitmap = new Bitmap(form.Width, form.Height);
            form.DrawToBitmap(bitmap, new Rectangle(Point.Empty, bitmap.Size));
            bitmap.Save(Path.Combine(output, "MiniDraw-" + theme + ".png"), ImageFormat.Png);
        }
        foreach (float zoom in new[] { .5f, 1.5f, 2f })
        {
            canvas.Zoom = zoom;
            Draw(form, "Rectangle", 40, 40, 120, 110);
            Require(Math.Abs(document.Selected!.Bounds.Width - 80) < 2, "Canvas gesture uses document coordinates at " + zoom * 100 + "% zoom");
        }
        Require(form.Controls.OfType<CommandBars.Controls.DockHost>().Count() == 4, "All four dock edges available");
        // Contact sheet checks artwork at the actual raster sizes used by the UI.
        using var icons = new DrawIcons();
        var keys = new[] { "new", "open", "save", "export", "undo", "redo", "duplicate", "delete", "select", "rectangle", "ellipse", "line", "forward", "backward" };
        using var sheet = new Bitmap(760, 360);
        using (var graphics = Graphics.FromImage(sheet))
        using (var font = new Font("Segoe UI", 9))
        {
            graphics.Clear(Color.White);
            for (int theme = 0; theme < 2; theme++)
            {
                bool dark = theme == 1;
                icons.UseDarkPalette(dark);
                graphics.FillRectangle(dark ? Brushes.DimGray : Brushes.WhiteSmoke, 0, theme * 180, sheet.Width, 180);
                for (int row = 0; row < 3; row++)
                {
                    int size = new[] { 16, 24, 32 }[row];
                    for (int i = 0; i < keys.Length; i++)
                        graphics.DrawImageUnscaled(icons.Get(keys[i]).GetImage(size), 12 + i * 53 + (32 - size) / 2, theme * 180 + 32 + row * 43);
                }
                for (int i = 0; i < keys.Length; i++)
                    graphics.DrawString(keys[i], font, dark ? Brushes.White : Brushes.Black, 6 + i * 53, theme * 180 + 8);
            }
        }
        sheet.Save(Path.Combine(output, "MiniDraw-icons.png"), ImageFormat.Png);
        Results.Add("Manual checks still needed: physical mixed-monitor DPI changes and Visual Studio launch.");
    }

    private static void RequirePageFits(DrawingCanvas canvas, string message)
    {
        var page = canvas.PageBounds;
        Require(page.Left > 0 && page.Top > 0 && page.Right < canvas.ClientSize.Width && page.Bottom < canvas.ClientSize.Height &&
            !canvas.HorizontalScroll.Visible && !canvas.VerticalScroll.Visible, message);
    }

    private static void CheckColorPalette(MainForm form, string target, string output)
    {
        var manager = form.Manager;
        var popupItem = (CommandBarPopupItem)manager.FindBar("Formatting")!.FindItem(target + ".palette")!;
        var bar = popupItem.DropDown;
        var swatches = bar.Items.OfType<CommandBarCommandItem>()
            .Where(item => item.DisplayStyle == CommandItemDisplayStyle.ImageOnly).ToArray();
        using (var popup = new CommandBarPopupWindow(bar, manager.Renderer, form.Font,
                   manager.FindBar("Formatting")!.IconSize, form.DeviceDpi / 96f, (_, _) => { }))
        {
            Require(bar.AllowTearOff && swatches.Length == 12 &&
                swatches.Take(6).All(item => item.Bounds.Top == swatches[0].Bounds.Top) &&
                swatches.Skip(6).All(item => item.Bounds.Top == swatches[0].Bounds.Bottom) &&
                popup.Height < 8 * swatches[0].Bounds.Height,
                target + " opens as a compact two-row swatch grid with a tear-off grip after layout restoration");
            using var bitmap = new Bitmap(popup.Width, popup.Height);
            popup.DrawToBitmap(bitmap, popup.ClientRectangle);
            bitmap.Save(Path.Combine(output, "MiniDraw-" + target + "-popup.png"), ImageFormat.Png);
        }
        using var palette = new TearOffWindow(bar, bar, manager.Renderer, manager, form);
        palette.Show();
        Application.DoEvents();
        Require(swatches.Take(6).All(item => item.Bounds.Top == swatches[0].Bounds.Top) &&
            swatches.Skip(6).All(item => item.Bounds.Top == swatches[0].Bounds.Bottom) &&
            palette.Height < 9 * swatches[0].Bounds.Height,
            target + " retains its compact swatch grid when detached");
        using var detachedBitmap = new Bitmap(palette.Width, palette.Height);
        palette.DrawToBitmap(detachedBitmap, palette.ClientRectangle);
        detachedBitmap.Save(Path.Combine(output, "MiniDraw-" + target + "-detached.png"), ImageFormat.Png);
    }

    private static void Draw(MainForm form, string tool, float x1, float y1, float x2, float y2)
    {
        form.Manager.Commands["tool." + tool].Perform();
        form.Canvas.Pointer(MouseButtons.Left, new PointF(x1, y1), "down");
        form.Canvas.Pointer(MouseButtons.Left, new PointF(x2, y2), "move");
        form.Canvas.Pointer(MouseButtons.Left, new PointF(x2, y2), "up");
    }
}
