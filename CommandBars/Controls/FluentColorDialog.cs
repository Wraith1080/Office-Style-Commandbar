using System.Drawing;
using System.Globalization;
using System.Windows.Forms;
using CommandBars.Model;
using CommandBars.Rendering;

namespace CommandBars.Controls;

/// <summary>A detached Fluent color preview. The caller applies SelectedColors after OK.</summary>
public sealed class FluentColorDialog : Form
{
    private readonly CommandBarColorScheme scheme;
    private readonly ComboBox palette = new() { DropDownStyle = ComboBoxStyle.DropDownList, Dock = DockStyle.Fill };
    private readonly TextBox baseHex = new() { Dock = DockStyle.Fill, AccessibleName = "Base color HEX" };
    private readonly TextBox accentHex = new() { Dock = DockStyle.Fill, AccessibleName = "Accent color HEX" };
    private readonly CheckBox schemeAccent = new() { Text = "Use theme accent", AutoSize = true };
    private readonly TrackBar strength = new() { Minimum = 0, Maximum = 100, TickFrequency = 10, Dock = DockStyle.Fill, AccessibleName = "Tint strength" };
    private readonly Label percentage = new() { AutoSize = true, Anchor = AnchorStyles.Left };
    private readonly Label error = new() { AutoSize = true, ForeColor = Color.Firebrick, Dock = DockStyle.Fill };
    private readonly Button ok = new() { Text = "OK", AutoSize = true, MinimumSize = new Size(85, 30) };
    private readonly Preview preview = new() { Dock = DockStyle.Fill, MinimumSize = new Size(400, 190) };
    private bool loading;
    private Color retainedBase;
    private readonly Button basePick;
    private readonly Button accentPick;

    public FluentColorDialog(CommandBarColorScheme scheme, FluentColorOptions options)
    {
        this.scheme = scheme;
        SelectedColors = options ?? throw new ArgumentNullException(nameof(options));
        Text = "Fluent colors";
        AutoScaleDimensions = new SizeF(96, 96);
        AutoScaleMode = AutoScaleMode.Dpi;
        Font = SystemFonts.MessageBoxFont;
        ClientSize = new Size(540, 550);
        MinimumSize = new Size(500, 580);
        StartPosition = FormStartPosition.CenterParent;
        ShowInTaskbar = false;
        MaximizeBox = false;
        MinimizeBox = false;
        Padding = new Padding(16);
        var layout = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 3, RowCount = 9 };
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        for (int i = 0; i < 9; i++) layout.RowStyles.Add(new RowStyle(i == 6 ? SizeType.Percent : SizeType.AutoSize, i == 6 ? 100 : 0));
        void LabelAt(string text, int row) => layout.Controls.Add(new Label { Text = text, AutoSize = true, Anchor = AnchorStyles.Left, Margin = new Padding(0, 8, 12, 8) }, 0, row);
        LabelAt("Base palette", 0);
        palette.Items.AddRange(new object[] { "Neutral", "Cool Blue", "Mint", "Rose", "Lavender", "Custom" });
        layout.Controls.Add(palette, 1, 0); layout.SetColumnSpan(palette, 2);
        LabelAt("Base HEX", 1); layout.Controls.Add(baseHex, 1, 1);
        basePick = new Button { Text = "Choose...", AutoSize = true, AccessibleName = "Choose base color" };
        layout.Controls.Add(basePick, 2, 1);
        LabelAt("Accent HEX", 2); layout.Controls.Add(accentHex, 1, 2);
        accentPick = new Button { Text = "Choose...", AutoSize = true, AccessibleName = "Choose accent color" };
        layout.Controls.Add(accentPick, 2, 2);
        layout.Controls.Add(schemeAccent, 1, 3); layout.SetColumnSpan(schemeAccent, 2);
        LabelAt("Tint strength", 4); layout.Controls.Add(strength, 1, 4); layout.Controls.Add(percentage, 2, 4);
        var hint = new Label { Text = "Light surfaces stay readable. Pale accents are darkened automatically.\nPreview changes are applied only when you choose OK.", AutoSize = true, Dock = DockStyle.Fill, Margin = new Padding(0, 8, 0, 12) };
        layout.Controls.Add(hint, 0, 5); layout.SetColumnSpan(hint, 3);
        layout.Controls.Add(preview, 0, 6); layout.SetColumnSpan(preview, 3);
        layout.Controls.Add(error, 0, 7); layout.SetColumnSpan(error, 3);
        var buttons = new FlowLayoutPanel { Dock = DockStyle.Fill, AutoSize = true, FlowDirection = FlowDirection.RightToLeft, Margin = new Padding(0, 12, 0, 0) };
        var cancel = new Button { Text = "Cancel", DialogResult = DialogResult.Cancel, AutoSize = true, MinimumSize = new Size(85, 30) };
        var reset = new Button { Text = "Reset to Default", AutoSize = true, MinimumSize = new Size(120, 30) };
        buttons.Controls.Add(cancel); buttons.Controls.Add(ok); buttons.Controls.Add(reset);
        layout.Controls.Add(buttons, 0, 8); layout.SetColumnSpan(buttons, 3);
        Controls.Add(layout);
        AcceptButton = ok; CancelButton = cancel;
        ok.Click += (_, _) => { if (UpdatePreview()) { DialogResult = DialogResult.OK; Close(); } };
        reset.Click += (_, _) => LoadOptions(new FluentColorOptions());
        basePick.Click += (_, _) => Pick(baseHex);
        accentPick.Click += (_, _) => Pick(accentHex);
        palette.SelectedIndexChanged += (_, _) =>
        {
            if (loading) return;
            loading = true;
            var selected = (FluentBasePalette)palette.SelectedIndex;
            var seed = new FluentColorOptions(selected, retainedBase).SurfaceSeed;
            baseHex.Text = Hex(seed.IsEmpty ? Color.FromArgb(238, 238, 238) : seed);
            loading = false;
            UpdatePreview();
        };
        baseHex.TextChanged += (_, _) => { if (!loading && palette.SelectedIndex == (int)FluentBasePalette.Custom && TryHex(baseHex.Text, out var c)) retainedBase = c; UpdatePreview(); };
        accentHex.TextChanged += (_, _) => UpdatePreview();
        schemeAccent.CheckedChanged += (_, _) => UpdatePreview();
        strength.ValueChanged += (_, _) => UpdatePreview();
        LoadOptions(options);
    }

    public FluentColorOptions SelectedColors { get; private set; }
    private static string Hex(Color color) => $"#{color.R:X2}{color.G:X2}{color.B:X2}";
    internal static bool TryHex(string text, out Color color)
    {
        text = text.Trim().TrimStart('#');
        if (text.Length == 6 && int.TryParse(text, NumberStyles.AllowHexSpecifier, CultureInfo.InvariantCulture, out int rgb))
        { color = Color.FromArgb(255, (rgb >> 16) & 255, (rgb >> 8) & 255, rgb & 255); return true; }
        color = Color.Empty; return false;
    }
    private void Pick(TextBox target)
    {
        using var picker = new ColorDialog { FullOpen = true, Color = TryHex(target.Text, out var current) ? current : Color.White };
        if (picker.ShowDialog(this) == DialogResult.OK) target.Text = Hex(picker.Color);
    }
    private void LoadOptions(FluentColorOptions options)
    {
        loading = true;
        retainedBase = options.BaseColor;
        palette.SelectedIndex = (int)options.BasePalette;
        Color seed = options.SurfaceSeed;
        baseHex.Text = Hex(seed.IsEmpty ? Color.FromArgb(238, 238, 238) : seed);
        accentHex.Text = Hex(options.AccentColor.IsEmpty ? new FluentColorTable(scheme).Accent : options.AccentColor);
        schemeAccent.Checked = options.AccentColor.IsEmpty;
        strength.Value = options.TintStrength;
        loading = false;
        UpdatePreview();
    }
    private bool UpdatePreview()
    {
        if (loading) return false;
        bool custom = palette.SelectedIndex == (int)FluentBasePalette.Custom;
        baseHex.Enabled = basePick.Enabled = custom;
        accentHex.Enabled = accentPick.Enabled = !schemeAccent.Checked;
        percentage.Text = strength.Value + "%";
        Color baseColor = retainedBase, accentColor = Color.Empty;
        bool valid = (!custom || TryHex(baseHex.Text, out baseColor)) &&
            (schemeAccent.Checked || TryHex(accentHex.Text, out accentColor));
        error.Text = valid ? "" : "Enter six HEX digits, for example #4696BE.";
        ok.Enabled = valid;
        if (!valid) return false;
        SelectedColors = new((FluentBasePalette)palette.SelectedIndex, baseColor, accentColor, strength.Value);
        preview.Renderer = new FluentRenderer(scheme, SelectedColors);
        preview.Invalidate();
        return true;
    }

    private sealed class Preview : Control
    {
        public FluentRenderer Renderer { get; set; } = new();
        public Preview() { DoubleBuffered = true; ResizeRedraw = true; AccessibleName = "Fluent color preview"; }
        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            float scale = DeviceDpi / 96f;
            Renderer.Scale = scale;
            int D(int v) => (int)Math.Round(v * scale);
            Rectangle R(int x, int y, int w, int h) => new(D(x), D(y), D(w), D(h));
            var r = Renderer;
            r.DrawBand(e.Graphics, ClientRectangle, BarOrientation.Horizontal);
            r.DrawItemText(e.Graphics, "File    Edit    View", Font, R(12, 4, 220, 24), RenderState.Normal, TextFormatFlags.Left | TextFormatFlags.VerticalCenter);
            r.DrawBarBackground(e.Graphics, new Rectangle(D(8), D(32), Width - D(16), D(42)), CommandBarType.Toolbar, BarOrientation.Horizontal, true, 0, Width);
            r.DrawGripper(e.Graphics, R(12, 37, 8, 32), BarOrientation.Horizontal);
            foreach (var item in new[] { ("Normal", 24, RenderState.Normal), ("Hover", 112, RenderState.Hot), ("Checked", 200, RenderState.Checked) })
            {
                var bounds = R(item.Item2, 35, 84, 36);
                r.DrawButton(e.Graphics, bounds, item.Item3, BarOrientation.Horizontal);
                r.DrawItemText(e.Graphics, item.Item1, Font, bounds, RenderState.Normal, TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
            }
            var menu = R(12, 86, 180, 88);
            r.DrawMenuBackground(e.Graphics, menu);
            r.DrawMenuItemBackground(e.Graphics, R(16, 91, 172, 30), RenderState.Hot);
            r.DrawItemText(e.Graphics, "Selected menu item", Font, R(24, 91, 160, 30), RenderState.Normal, TextFormatFlags.VerticalCenter);
            r.DrawItemText(e.Graphics, "Another command", Font, R(24, 129, 160, 30), RenderState.Normal, TextFormatFlags.VerticalCenter);
            var floating = R(210, 86, 178, 88);
            var caption = R(213, 89, 172, 28);
            r.DrawFloatingWindowChrome(e.Graphics, floating, caption);
            TextRenderer.DrawText(e.Graphics, "Floating toolbar", Font, R(222, 89, 157, 28), r.FloatingCaptionTextColor, TextFormatFlags.VerticalCenter);
            r.DrawItemText(e.Graphics, "B    I    U", Font, R(223, 129, 150, 30), RenderState.Normal, TextFormatFlags.VerticalCenter);
        }
    }
}
