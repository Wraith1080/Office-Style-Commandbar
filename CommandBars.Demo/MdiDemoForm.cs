using CommandBars.Controls;
using CommandBars.Model;

namespace CommandBars.Demo;

/// <summary>A focused sample of automatic MDI caption integration.</summary>
public sealed class MdiDemoForm : Form
{
    public readonly CommandBarManager _manager = new();
    private readonly DockHost _top;
    private readonly System.Windows.Forms.Timer _smokeTimer = new() { Interval = 200 };
    private int _documentNumber;
    private int _smokeStep;
    private Form? _smokeChild;

    public MdiDemoForm(bool smokeTest = false)
    {
        Text = "CommandBars — MDI compatibility";
        IsMdiContainer = true;
        ClientSize = new Size(1000, 680);
        StartPosition = FormStartPosition.CenterScreen;

        Register("new", "&New child", NewChild);
        Register("next", "Ne&xt child", () => ActivateNextMdiChild());
        Register("maximize", "Ma&ximize child", () => SetChildState(FormWindowState.Maximized));
        Register("restore", "&Restore child", () => SetChildState(FormWindowState.Normal));
        Register("minimize", "&Minimize child", () => SetChildState(FormWindowState.Minimized));
        Register("close", "&Close child", () => ActiveMdiChild?.Close());
        Register("cascade", "&Cascade", () => LayoutMdi(MdiLayout.Cascade));
        Register("tile", "&Tile", () => LayoutMdi(MdiLayout.TileVertical));

        var menu = _manager.AddBar("MdiMenu", CommandBarType.MenuBar);
        var file = menu.Items.AddPopup("&File");
        file.DropDown.Items.AddButton(_manager.Commands["new"]);
        file.DropDown.Items.AddButton(_manager.Commands["close"]);
        var window = menu.Items.AddPopup("&Window");
        foreach (var id in new[] { "next", "maximize", "restore", "minimize", "cascade", "tile" })
            window.DropDown.Items.AddButton(_manager.Commands[id]);
        menu.Items.AddPopup("&Theme").ThemeList = true;

        var toolbar = _manager.AddBar("MdiTools", CommandBarType.Toolbar);
        toolbar.Row = 1;
        foreach (var id in new[] { "new", "next", "maximize", "restore", "minimize", "close" })
            toolbar.Items.AddButton(_manager.Commands[id]).DisplayStyle = CommandItemDisplayStyle.TextOnly;

        _top = new DockHost { Edge = DockEdge.Top, Manager = _manager };
        Controls.Add(_top);
        _manager.CaptureDefaults();
        Shown += (_, _) =>
        {
            NewChild();
            NewChild();
            NewChild();
            if (smokeTest) _smokeTimer.Start();
        };
        _smokeTimer.Tick += (_, _) => RunSmokeStep();
    }

    private void Register(string id, string text, Action action) =>
        _manager.Commands.Register(id, command =>
        {
            command.Text = text;
            command.ExecuteHandler = _ => action();
        });

    private void NewChild()
    {
        var child = new Form
        {
            MdiParent = this,
            Text = $"Document {++_documentNumber}",
            ClientSize = new Size(420, 260)
        };
        child.Controls.Add(new TextBox
        {
            Multiline = true,
            Dock = DockStyle.Fill,
            Text = "MDI child document.\r\nUse Window or the toolbar to maximize, restore, minimize, or close.\r\n" +
                   "Maximize a child to show its system icon and window buttons in the first menu bar.\r\n" +
                   "Drag the menu bar out to try the same controls while floating."
        });
        child.Show();
    }

    private void ActivateNextMdiChild()
    {
        var children = MdiChildren;
        if (children.Length == 0) return;
        children[(Array.IndexOf(children, ActiveMdiChild) + 1) % children.Length].Activate();
    }

    private void SetChildState(FormWindowState state)
    {
        if (ActiveMdiChild is { } child) child.WindowState = state;
    }

    // Run through the real message loop, allowing native MDI state/layout to settle between assertions.
    private void RunSmokeStep()
    {
        try
        {
            switch (_smokeStep++)
            {
                case 0:
                    Require(MdiChildren.Length == 3, "Three child windows opened");
                    var client = Controls.OfType<MdiClient>().Single();
                    Require(_top.Visible && _top.Height > 0 && client.Top >= _top.Bottom,
                        "MDI client lies below the command bars");
                    _smokeChild = ActiveMdiChild!;
                    SetChildState(FormWindowState.Maximized);
                    break;
                case 1:
                    Require(_smokeChild!.WindowState == FormWindowState.Maximized, "Child maximizes");
                    SetChildState(FormWindowState.Normal);
                    break;
                case 2:
                    Require(_smokeChild!.WindowState == FormWindowState.Normal, "Child restores");
                    SetChildState(FormWindowState.Minimized);
                    break;
                case 3:
                    Require(_smokeChild!.WindowState == FormWindowState.Minimized, "Child minimizes");
                    _smokeChild.WindowState = FormWindowState.Normal;
                    _smokeChild.Activate();
                    ActivateNextMdiChild();
                    break;
                case 4:
                    Require(ActiveMdiChild != _smokeChild, "Active child switches");
                    ActiveMdiChild!.Close();
                    break;
                case 5:
                    Require(MdiChildren.Length == 2, "Active child closes");
                    LayoutMdi(MdiLayout.TileVertical);
                    break;
                case 6:
                    Require(MdiChildren.All(child => child.Visible && child.WindowState == FormWindowState.Normal),
                        "Remaining children tile in normal state");
                    _smokeTimer.Stop();
                    Close();
                    break;
            }
        }
        catch (Exception exception)
        {
            Console.Error.WriteLine(exception);
            Environment.ExitCode = 1;
            _smokeTimer.Stop();
            Close();
        }
    }

    private static void Require(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
        Console.WriteLine($"PASS: {message}");
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _smokeTimer.Dispose();
            _manager.Dispose();
        }
        base.Dispose(disposing);
    }
}
