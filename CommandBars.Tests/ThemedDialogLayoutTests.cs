using System.Drawing;
using System.Windows.Forms;
using CommandBars;
using CommandBars.Controls;
using CommandBars.Model;
using CommandBars.Rendering;
using Xunit;

namespace CommandBars.Tests;

public sealed class ThemedDialogLayoutTests
{
    [Fact]
    public void TabHeaders_StayInsideAvailableWidth_WithLargeFont()
    {
        using var tabs = new ThemedTabControl
        {
            Size = new Size(360, 300),
            Font = new Font(SystemFonts.MessageBoxFont!.FontFamily, 16f),
        };
        tabs.AddPage(new DialogTabPage("Toolbars"));
        tabs.AddPage(new DialogTabPage("Menus"));
        tabs.AddPage(new DialogTabPage("Commands"));
        tabs.AddPage(new DialogTabPage("Options"));
        tabs.Width = Math.Max(tabs.Width, tabs.MinimumTabStripWidth);

        Rectangle previous = Rectangle.Empty;
        for (int i = 0; i < 4; i++)
        {
            Rectangle bounds = tabs.TabBounds(i);
            Assert.True(bounds.Width > 0);
            Assert.True(bounds.Left >= previous.Right);
            Assert.True(bounds.Right <= tabs.ClientSize.Width);
            previous = bounds;
        }
    }

    [Fact]
    public void CustomizeDialog_AllPagesUseResponsiveGridLayouts()
    {
        using var manager = new CommandBarManager();
        manager.AddBar("menu", CommandBarType.MenuBar).Items.AddPopup("&File");
        manager.AddBar("standard", CommandBarType.Toolbar);
        using var dialog = new CustomizeDialog(manager, new Office2003Renderer());

        var tabs = Descendants(dialog).OfType<ThemedTabControl>().Single();
        DialogTabPage[] pages = tabs.Controls.OfType<DialogTabPage>().ToArray();

        Assert.Equal(AutoScaleMode.Dpi, dialog.AutoScaleMode);
        float scale = dialog.DeviceDpi / 120f;
        Assert.Equal(new Size(
            (int)Math.Round(420 * scale),
            (int)Math.Round(470 * scale)), dialog.ClientSize);
        Assert.Equal(new SizeF(dialog.DeviceDpi, dialog.DeviceDpi), dialog.AutoScaleDimensions);
        Assert.Equal(4, pages.Length);
        Assert.All(pages, page =>
        {
            var grid = Assert.Single(page.Controls.OfType<ThemedTableLayoutPanel>());
            Assert.Equal(DockStyle.Fill, grid.Dock);
        });
        foreach (DialogTabPage page in pages.Take(2))
        {
            var grid = page.Controls.OfType<ThemedTableLayoutPanel>().Single();
            Assert.Equal(50f, grid.ColumnStyles[0].Width);
            Assert.Equal(50f, grid.ColumnStyles[1].Width);
        }
        Assert.True(Descendants(dialog).OfType<ThemedFlowLayoutPanel>().Count(host => host.AutoScroll) >= 2);
    }

    [Fact]
    public void VerticalButtonHost_StretchesButtonsAcrossItsWidth()
    {
        using var host = new ThemedFlowLayoutPanel
        {
            Size = new Size(240, 300),
            FlowDirection = FlowDirection.TopDown,
            WrapContents = false,
            StretchChildrenHorizontally = true,
        };
        using var first = new ThemedButton { Text = "New", AutoSize = false, Margin = new Padding(0, 0, 0, 6) };
        using var second = new ThemedButton { Text = "Rename", AutoSize = false, Margin = new Padding(0, 0, 0, 6) };
        host.Controls.Add(first);
        host.Controls.Add(second);

        host.PerformLayout();

        Assert.Equal(first.Width, second.Width);
        Assert.Equal(host.ClientSize.Width, first.Width);
        Assert.True(first.Height >= first.PreferredSize.Height);
    }

    [Fact]
    public void SpawnedDialogFactory_ScalesAssignedSizeFrom125PercentBaseline()
    {
        using var form = CustomizeDialog.CreateDpiScaledForm();
        form.ClientSize = new Size(300, 112);
        form.Controls.Add(new ThemedTableLayoutPanel { Dock = DockStyle.Fill });

        form.ResumeLayout(true);

        float scale = form.DeviceDpi / 120f;
        Assert.Equal(new Size(
            (int)Math.Round(300 * scale),
            (int)Math.Round(112 * scale)), form.ClientSize);
        Assert.Equal(AutoScaleMode.Dpi, form.AutoScaleMode);
    }

    [Fact]
    public void DialogButtonFooter_UsesStructuralGapBetweenButtons()
    {
        using var form = CustomizeDialog.CreateDpiScaledForm();
        form.ClientSize = new Size(400, 100);
        using var left = new ThemedButton
        {
            Text = "Yes",
            AutoSize = true,
            MinimumSize = new Size(84, 0),
        };
        using var right = new ThemedButton
        {
            Text = "No",
            AutoSize = true,
            MinimumSize = new Size(84, 0),
        };
        using var footer = CustomizeDialog.CreateDialogButtonFooter(
            left, right, new Padding(8, 10, 8, 8));
        form.Controls.Add(footer);

        form.ResumeLayout(true);
        form.PerformLayout();
        footer.PerformLayout();

        int expectedGap = (int)Math.Round(8 * form.DeviceDpi / 120f);
        Assert.Equal(expectedGap, right.Left - left.Right);
        Assert.True(left.Right < right.Left);
        Assert.Equal(footer.ClientSize.Width - footer.Padding.Right, right.Right);
    }

    [Fact]
    public void FooterPreferredHeight_ContainsLargeButtons()
    {
        using var footer = new ThemedFooterPanel();
        using var first = new ThemedButton
        {
            Text = "Reset All...",
            AutoSize = true,
            Font = new Font(SystemFonts.MessageBoxFont!.FontFamily, 16f),
        };
        using var second = new ThemedButton
        {
            Text = "Close",
            AutoSize = true,
            Font = first.Font,
        };
        footer.Controls.Add(first);
        footer.Controls.Add(second);

        Size preferred = footer.GetPreferredSize(Size.Empty);

        Assert.True(preferred.Height > first.PreferredSize.Height);
        Assert.True(preferred.Width >= first.PreferredSize.Width + second.PreferredSize.Width);
    }

    private static IEnumerable<Control> Descendants(Control parent)
    {
        foreach (Control child in parent.Controls)
        {
            yield return child;
            foreach (Control descendant in Descendants(child))
                yield return descendant;
        }
    }
}
