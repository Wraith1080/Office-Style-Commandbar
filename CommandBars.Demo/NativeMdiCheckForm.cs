using System.Drawing;
using System.Windows.Forms;

namespace CommandBars.Demo;

/// <summary>Stock WinForms comparison with no CommandBars manager or controls.</summary>
internal sealed class NativeMdiCheckForm : Form
{
    private int _documentNumber;

    internal NativeMdiCheckForm()
    {
        Text = "Native WinForms MDI — DPI check";
        IsMdiContainer = true;
        AutoScaleMode = AutoScaleMode.Dpi;
        ClientSize = new Size(1000, 680);
        StartPosition = FormStartPosition.CenterScreen;

        var menu = new MenuStrip();
        var file = new ToolStripMenuItem("&File");
        file.DropDownItems.Add("&New child", null, (_, _) => NewChild());
        file.DropDownItems.Add("&Close child", null, (_, _) => ActiveMdiChild?.Close());
        var windows = new ToolStripMenuItem("&Window");
        windows.DropDownItems.Add("&Maximize child", null, (_, _) => SetChildState(FormWindowState.Maximized));
        windows.DropDownItems.Add("&Restore child", null, (_, _) => SetChildState(FormWindowState.Normal));
        windows.DropDownItems.Add("Mi&nimize child", null, (_, _) => SetChildState(FormWindowState.Minimized));
        windows.DropDownItems.Add("&Cascade", null, (_, _) => LayoutMdi(MdiLayout.Cascade));
        windows.DropDownItems.Add(new ToolStripSeparator());
        menu.Items.Add(file);
        menu.Items.Add(windows);
        menu.MdiWindowListItem = windows;
        MainMenuStrip = menu;

        var toolbar = new ToolStrip();
        toolbar.Items.Add("New child", null, (_, _) => NewChild());
        toolbar.Items.Add("Maximize child", null, (_, _) => SetChildState(FormWindowState.Maximized));
        toolbar.Items.Add("Restore child", null, (_, _) => SetChildState(FormWindowState.Normal));
        toolbar.Items.Add("Close child", null, (_, _) => ActiveMdiChild?.Close());
        toolbar.Items.Add(new ToolStripSeparator());
        toolbar.Items.Add("Open another MDI parent", null, (_, _) =>
        {
            var comparison = new NativeMdiCheckForm();
            comparison.Show(this);
        });
        Controls.Add(toolbar);
        Controls.Add(menu);

        Shown += (_, _) =>
        {
            for (int i = 0; i < 3; i++) NewChild();
            SetChildState(FormWindowState.Maximized);
        };
    }

    private void NewChild()
    {
        var child = new Form
        {
            MdiParent = this,
            Text = $"Native document {++_documentNumber}",
            AutoScaleMode = AutoScaleMode.Dpi,
            ClientSize = new Size(460, 300)
        };
        child.Controls.Add(new TextBox
        {
            Multiline = true,
            Dock = DockStyle.Fill,
            Text = "Stock WinForms MDI comparison: standard MenuStrip and ToolStrip.\r\n" +
                   "No CommandBars controls or MDI/DPI workarounds are instantiated.\r\n\r\n" +
                   "Check whether the maximized child's title bar is visible below the toolbar.\r\n" +
                   "Change display scaling while this process stays open, then restore/maximize a child.\r\n" +
                   "Use Open another MDI parent to test a fresh window in the same process.\r\n" +
                   "Keep the original parent open during that comparison."
        });
        child.Show();
    }

    private void SetChildState(FormWindowState state)
    {
        if (ActiveMdiChild is { } child) child.WindowState = state;
    }
}
