using System;
using System.Windows.Forms;
using CommandBars;
using CommandBars.Model;

namespace CommandBars.PackageDemo;

/// <summary>
/// The out-of-process designer test bed. This form consumes CommandBars as a
/// NuGet package, so opening it in the Visual Studio designer is what exercises
/// the design-time assemblies in CommandBars.Designer.Server:
///
///  • select <c>_manager</c> → smart tag / right-click should offer
///    "Edit toolbars and menus…", "Refresh design preview", and a Theme picker;
///  • select <c>_svgImages</c> → smart tag should offer "Import SVG files…";
///  • edit a toolbar's IconSize in the collection editor → the preview in
///    <c>_dockTop</c> should update immediately (live refresh).
///
/// At run time the constructor registers the real commands (matching the
/// CommandId values set in the designer) and realizes the definitions into
/// live bars.
/// </summary>
public partial class MainForm : Form
{
    public MainForm()
    {
        InitializeComponent();
        RegisterCommands();
        _manager.BuildFromDefinitions();
    }

    private void RegisterCommands()
    {
        Register("file.new", "&New", Keys.Control | Keys.N);
        Register("file.open", "&Open…", Keys.Control | Keys.O);
        Register("file.save", "&Save", Keys.Control | Keys.S);
        Register("file.exit", "E&xit", Keys.None, _ => Close());

        Register("help.about", "&About…", Keys.None,
            _ => MessageBox.Show(this,
                "CommandBars consumed as a NuGet package — out-of-process designer test bed.",
                "About", MessageBoxButtons.OK, MessageBoxIcon.Information));
    }

    private void Register(
        string id,
        string text,
        Keys shortcut,
        Action<CommandExecuteContext>? handler = null)
    {
        _manager.Commands.GetOrAdd(id, c =>
        {
            c.Text = text;
            c.Shortcut = shortcut;
            c.ExecuteHandler = handler ?? (ctx => _client.Text = $"Ran: {ctx.Command.DisplayText}");
        });
    }
}
