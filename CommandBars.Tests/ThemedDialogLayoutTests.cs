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

    [Fact]
    public void Office2000DialogSkin_UsesClassicRaisedAndSunkenControls()
    {
        var renderer = new Office2000Renderer();
        using var form = new Form();
        using var button = new ThemedButton { Text = "OK", Size = new Size(84, 28) };
        using var textBox = new TextBox();
        using var tree = new TreeView();
        using var list = new ThemedListBox();
        using var panel = new Panel { BorderStyle = BorderStyle.FixedSingle };
        using var checkBox = new CheckBox();
        form.Controls.AddRange(new Control[] { button, textBox, tree, list, panel, checkBox });

        DialogSkin.Apply(form, renderer.DialogColors);

        Assert.True(renderer.DialogColors.UsesClassic3DChrome);
        Assert.Equal(renderer.Colors.MenuItemSelectedBegin,
            renderer.DialogColors.SelectionBackground);
        Assert.Equal(BorderStyle.Fixed3D, textBox.BorderStyle);
        Assert.Equal(BorderStyle.Fixed3D, tree.BorderStyle);
        Assert.Equal(BorderStyle.Fixed3D, list.BorderStyle);
        Assert.Equal(BorderStyle.Fixed3D, panel.BorderStyle);
        Assert.Equal(FlatStyle.Standard, checkBox.FlatStyle);

        using var bitmap = new Bitmap(button.Width, button.Height);
        button.DrawToBitmap(bitmap, button.ClientRectangle);
        Assert.Equal(renderer.DialogColors.ControlHighlight.ToArgb(),
            bitmap.GetPixel(button.Width / 2, 0).ToArgb());
        Assert.Equal(renderer.DialogColors.ControlDarkShadow.ToArgb(),
            bitmap.GetPixel(button.Width / 2, button.Height - 1).ToArgb());
    }

    [Fact]
    public void Office2000TabsPreserveDoubleRaisedTrailingEdges()
    {
        var renderer = new Office2000Renderer();
        using var tabs = new ThemedTabControl { Size = new Size(220, 140) };
        var page = new DialogTabPage("Options");
        tabs.AddPage(page);
        tabs.DialogColors = renderer.DialogColors;
        tabs.PerformLayout();

        using var bitmap = new Bitmap(tabs.Width, tabs.Height);
        tabs.DrawToBitmap(bitmap, tabs.ClientRectangle);
        int bodyY = page.Top + 20;

        Assert.Equal(renderer.DialogColors.ControlDarkShadow.ToArgb(),
            bitmap.GetPixel(tabs.Width - 1, bodyY).ToArgb());
        Assert.Equal(renderer.DialogColors.ControlShadow.ToArgb(),
            bitmap.GetPixel(tabs.Width - 2, bodyY).ToArgb());
        Assert.Equal(renderer.DialogColors.ControlDarkShadow.ToArgb(),
            bitmap.GetPixel(100, tabs.Height - 1).ToArgb());
        Assert.Equal(renderer.DialogColors.ControlShadow.ToArgb(),
            bitmap.GetPixel(100, tabs.Height - 2).ToArgb());
    }

    [Fact]
    public void Office2000ComboArrowPaintsItsDoubleRaisedEdgeAboveSunkenField()
    {
        var renderer = new Office2000Renderer();
        using var combo = new ThemedComboBox { Size = new Size(180, 28) };
        combo.DialogColors = renderer.DialogColors;
        combo.Items.Add("24 px");
        combo.SelectedIndex = 0;
        using var bitmap = new Bitmap(combo.Width, combo.Height);
        using (Graphics graphics = Graphics.FromImage(bitmap))
            combo.DrawClosedCombo(graphics);

        int buttonLeft = combo.ClientSize.Width - 2 - SystemInformation.VerticalScrollBarWidth;
        Assert.Equal(renderer.DialogColors.ControlDarkShadow.ToArgb(),
            bitmap.GetPixel(buttonLeft + 5, 0).ToArgb());
        Assert.Equal(renderer.DialogColors.ControlHighlight.ToArgb(),
            bitmap.GetPixel(buttonLeft + 5, 2).ToArgb());
        Assert.Equal(renderer.DialogColors.ControlHighlight.ToArgb(),
            bitmap.GetPixel(buttonLeft, combo.ClientSize.Height / 2).ToArgb());
        Assert.Equal(renderer.DialogColors.ControlDarkShadow.ToArgb(),
            bitmap.GetPixel(combo.ClientSize.Width - 3, combo.ClientSize.Height / 2).ToArgb());
        Assert.Equal(renderer.DialogColors.ControlShadow.ToArgb(),
            bitmap.GetPixel(combo.ClientSize.Width - 4, combo.ClientSize.Height / 2).ToArgb());
        Assert.Equal(renderer.DialogColors.ControlHighlight.ToArgb(),
            bitmap.GetPixel(combo.ClientSize.Width - 1, combo.ClientSize.Height / 2).ToArgb());
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
