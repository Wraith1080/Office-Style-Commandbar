using System;
using System.Windows.Forms;
using CommandBars.Controls;
using CommandBars.Model;

namespace CommandBars.Demo;

/// <summary>
/// A demo form whose menu bar and toolbars are defined entirely in the Windows
/// Forms designer, through <c>CommandBarManager.CommandDefinitions</c> and
/// catalog placements in <c>BarDefinitions</c> (see
/// <c>DesignerDemoForm.Designer.cs</c>). Open this form's designer to see the
/// bars laid out with their items and SVG icons.
///
/// At run time the constructor registers the real commands (matching the
/// CommandId values set in the designer) and calls
/// <see cref="CommandBars.CommandBarManager.BuildFromDefinitions"/> to turn the
/// definitions into live, interactive bars. The icons come from the embedded
/// SvgImageList via each catalog entry's ImageKey — no code assigns images here.
/// </summary>
public partial class DesignerDemoForm : Form
{
    private CustomizeDialog? _customize;
    public CommandBarManager Manager => _manager;
    public DesignerDemoForm()
    {
        InitializeComponent();
        RegisterCommands();
        _manager.BuildFromDefinitions();

        // "Customize…" from a toolbar's chevron opens (or re-activates) the dialog.
        _manager.CustomizeRequested += (_, _) => OpenCustomize();
    }

    private void OpenCustomize()
    {
        if (_customize is { IsDisposed: false })
        {
            _customize.Activate();
            return;
        }
        _customize = new CustomizeDialog(_manager, _dockTop.Renderer) { Owner = this };
        _customize.FormClosed += (_, _) => _customize = null;
        _customize.Show(this);
    }

    private void RegisterCommands()
    {
        Register("file.new", "&New", Keys.Control | Keys.N);
        Register("file.open", "&Open…", Keys.Control | Keys.O);
        Register("file.save", "&Save", Keys.Control | Keys.S);
        Register("file.exit", "E&xit", Keys.None, _ => Close());

        Register("edit.cut", "Cu&t", Keys.Control | Keys.X);
        Register("edit.copy", "&Copy", Keys.Control | Keys.C);
        Register("edit.paste", "&Paste", Keys.Control | Keys.V);

        Register("format.bold", "Bold", Keys.Control | Keys.B, checkable: true);
        Register("format.italic", "Italic", Keys.Control | Keys.I, checkable: true);
        Register("format.underline", "Underline", Keys.Control | Keys.U, checkable: true);

        Register("nav.back", "Back", Keys.None);
        Register("nav.forward", "Forward", Keys.None);
        Register("nav.home", "Home", Keys.None);

        Register("help.about", "&About…", Keys.None,
            _ => MessageBox.Show(this,
                "CommandBars — bars defined in the Windows Forms designer.",
                "About", MessageBoxButtons.OK, MessageBoxIcon.Information));
    }

    private void Register(
        string id,
        string text,
        Keys shortcut,
        Action<CommandExecuteContext>? handler = null,
        bool checkable = false)
    {
        _manager.Commands.GetOrAdd(id, c =>
        {
            c.Text = text;
            c.Shortcut = shortcut;
            c.IsCheckable = checkable;
            // Images are supplied by the SvgImageList through each item's ImageKey.
            c.ExecuteHandler = handler ?? (ctx => ShowStatus(ctx.Command));
        });
    }

    private void ShowStatus(Command command)
    {
        string state = command.IsCheckable
            ? (command.Checked == CommandCheckState.Checked ? " (on)" : " (off)")
            : string.Empty;
        _client.Text = $"Ran: {command.DisplayText}{state}";
    }
}
