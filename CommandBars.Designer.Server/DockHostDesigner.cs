using System.ComponentModel;
using System.Drawing;
using System.Windows.Forms;
using CommandBars.Controls;
using CommandBars.Model;
using Microsoft.DotNet.DesignTools.Designers;
using Microsoft.DotNet.DesignTools.Designers.Actions;
using Microsoft.DotNet.DesignTools.Designers.Behaviors;

namespace CommandBars.Designer.Server;

/// <summary>
/// Out-of-process design-time behavior for <see cref="DockHost"/>. Runs inside
/// the WinForms DesignToolsServer process (which is also where the control
/// instances on the design surface live), so it has full access to the real
/// <see cref="DockHost"/> — unlike the old in-process designer in
/// <c>CommandBars.Design</c>, which Visual Studio never loads.
///
/// The manager designer coordinates definition refreshes for every registered
/// host. This designer only maintains the per-bar action glyphs, avoiding one
/// complete preview rebuild per DockHost for every global surface notification.
/// </summary>
public class DockHostDesigner : ControlDesigner
{
    private BehaviorService? _behaviorService;
    private Adorner? _barActionsAdorner;

    public override DesignerActionListCollection ActionLists
        => new() { new DockHostActionList(this) };

    public override void Initialize(IComponent component)
    {
        base.Initialize(component);
        if (component is DockHost host)
        {
            host.Layout += OnHostLayout;
            _behaviorService = component.Site?.GetService(typeof(BehaviorService))
                as BehaviorService;
            if (_behaviorService is not null)
            {
                _barActionsAdorner = new Adorner();
                _behaviorService.Adorners.Add(_barActionsAdorner);
                RefreshBarActionGlyphs();
            }
        }
    }

    private void OnHostLayout(object? sender, LayoutEventArgs e)
    {
        RefreshBarActionGlyphs();
        _behaviorService?.Invalidate();
    }

    private void RefreshBarActionGlyphs()
    {
        if (_barActionsAdorner is null || _behaviorService is null || Control is not DockHost host)
            return;

        _barActionsAdorner.Glyphs.Clear();
        foreach (CommandBarControl preview in host.BarControls)
        {
            string? barName = preview.Bar?.Name;
            if (!string.IsNullOrWhiteSpace(barName) && preview.Visible)
            {
                _barActionsAdorner.Glyphs.Add(new BarActionGlyph(
                    _behaviorService,
                    preview,
                    barName,
                    new AddCommandsBehavior(this, barName)));
            }
        }
    }

    private void AddCommandsToBar(string barName)
    {
        if (Control is not DockHost host)
            return;
        host.DesignerTargetBarName = barName;
        InvokePropertyEditor(nameof(DockHost.DesignerAddCommandsToBar));
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing && Control is DockHost host)
            host.Layout -= OnHostLayout;
        if (disposing && _barActionsAdorner is not null && _behaviorService is not null)
        {
            _behaviorService.Adorners.Remove(_barActionsAdorner);
            _barActionsAdorner = null;
            _behaviorService = null;
        }
        base.Dispose(disposing);
    }

    private sealed class AddCommandsBehavior : Behavior
    {
        private readonly DockHostDesigner _designer;
        private readonly string _barName;

        public AddCommandsBehavior(DockHostDesigner designer, string barName)
        {
            _designer = designer;
            _barName = barName;
        }

        public override bool OnMouseUp(Glyph? g, MouseButtons button, Point mouseLoc)
        {
            if (button != MouseButtons.Left)
                return false;
            _designer.AddCommandsToBar(_barName);
            return true;
        }
    }

    private sealed class BarActionGlyph : Glyph
    {
        private readonly BehaviorService _behaviorService;
        private readonly CommandBarControl _preview;

        public BarActionGlyph(
            BehaviorService behaviorService,
            CommandBarControl preview,
            string barName,
            Behavior behavior)
            : base(behavior)
        {
            _behaviorService = behaviorService;
            _preview = preview;
            BarName = barName;
        }

        public string BarName { get; }

        public override Rectangle Bounds
        {
            get
            {
                Point origin = _behaviorService.ControlToAdornerWindow(_preview);
                int size = Math.Max(16, (int)Math.Round(18f * _preview.DeviceDpi / 96f));
                int inset = Math.Max(2, (int)Math.Round(2f * _preview.DeviceDpi / 96f));
                bool vertical = _preview.Bar?.Orientation == BarOrientation.Vertical;
                int x = vertical
                    ? origin.X + inset
                    : origin.X + Math.Max(inset, _preview.Width - size - inset);
                int y = vertical
                    ? origin.Y + Math.Max(inset, _preview.Height - size - inset)
                    : origin.Y + inset;
                return new Rectangle(x, y, size, size);
            }
        }

        public override Cursor? GetHitTest(Point p)
            => Bounds.Contains(p) ? Cursors.Hand : null;

        public override void Paint(PaintEventArgs pe)
        {
            Rectangle bounds = Bounds;
            int radius = Math.Max(2, bounds.Width / 5);
            using var path = new System.Drawing.Drawing2D.GraphicsPath();
            path.AddArc(bounds.Left, bounds.Top, radius * 2, radius * 2, 180, 90);
            path.AddArc(bounds.Right - radius * 2, bounds.Top, radius * 2, radius * 2, 270, 90);
            path.AddArc(bounds.Right - radius * 2, bounds.Bottom - radius * 2, radius * 2, radius * 2, 0, 90);
            path.AddArc(bounds.Left, bounds.Bottom - radius * 2, radius * 2, radius * 2, 90, 90);
            path.CloseFigure();

            using var fill = new SolidBrush(SystemColors.Highlight);
            using var border = new Pen(SystemColors.HighlightText);
            pe.Graphics.FillPath(fill, path);
            pe.Graphics.DrawPath(border, path);

            float penWidth = Math.Max(1.5f, bounds.Width / 9f);
            using var plus = new Pen(SystemColors.HighlightText, penWidth)
            {
                StartCap = System.Drawing.Drawing2D.LineCap.Round,
                EndCap = System.Drawing.Drawing2D.LineCap.Round,
            };
            int arm = Math.Max(3, bounds.Width / 4);
            Point center = new(bounds.Left + bounds.Width / 2, bounds.Top + bounds.Height / 2);
            pe.Graphics.DrawLine(plus, center.X - arm, center.Y, center.X + arm, center.Y);
            pe.Graphics.DrawLine(plus, center.X, center.Y - arm, center.X, center.Y + arm);
        }
    }

    private sealed class DockHostActionList : DesignerActionList
    {
        private const string CategoryName = "CommandBars";
        private readonly DockHostDesigner _designer;

        public DockHostActionList(DockHostDesigner designer)
            : base(designer.Component)
            => _designer = designer;

        private DockHost Host => (DockHost)Component!;

        public void AddToolbar()
            => _designer.InvokePropertyEditor(nameof(DockHost.DesignerAddToolbar));

        public void AddMenuBar()
            => _designer.InvokePropertyEditor(nameof(DockHost.DesignerAddMenuBar));

        public void AddCommands()
            => _designer.InvokePropertyEditor(nameof(DockHost.DesignerAddCommands));

        public void EditBars()
            => _designer.InvokePropertyEditor(nameof(DockHost.DesignerEditBars));

        public void EditCatalog()
            => _designer.InvokePropertyEditor(nameof(DockHost.DesignerEditCatalog));

        public void RefreshPreview()
        {
            if (Host.Manager is not null)
            {
                try { Host.Manager.RefreshDesignPreview(force: true); }
                catch { /* preview only */ }
            }
            else
            {
                try { Host.RefreshDesignPreview(); }
                catch { /* preview only */ }
            }
        }

        public override DesignerActionItemCollection GetSortedActionItems()
        {
            DesignerActionItemCollection items = new();
            items.Add(new DesignerActionHeaderItem(CategoryName));

            if (Host.Manager is null)
            {
                items.Add(new DesignerActionTextItem(
                    "Assign a CommandBarManager to enable editing.",
                    CategoryName));
                items.Add(Method(nameof(RefreshPreview), "Refresh design preview",
                    "Repaints this empty host preview."));
                return items;
            }

            items.Add(Method(nameof(AddToolbar), "Add toolbar…",
                "Creates a toolbar initially docked to this host."));

            bool canAddMenu = Host.Edge == DockEdge.Top &&
                !Host.Manager.BarDefinitions.Any(definition =>
                    definition.BarType == CommandBarType.MenuBar);
            if (canAddMenu)
            {
                items.Add(Method(nameof(AddMenuBar), "Add menu bar…",
                    "Creates the manager's menu bar in this top host."));
            }

            items.Add(Method(nameof(AddCommands), "Add commands to…",
                "Chooses a bar in this host and adds catalog placements."));
            items.Add(Method(nameof(EditBars), "Edit bars and menus…",
                "Opens the manager editor on the Bars and Menus page."));
            items.Add(Method(nameof(EditCatalog), "Edit command catalog…",
                "Opens the manager editor on the Commands page."));
            items.Add(Method(nameof(RefreshPreview), "Refresh design preview",
                "Rebuilds every DockHost connected to this manager."));
            return items;
        }

        private DesignerActionMethodItem Method(
            string memberName,
            string displayName,
            string description)
            => new(
                this,
                memberName,
                displayName,
                CategoryName,
                description,
                true);
    }
}
