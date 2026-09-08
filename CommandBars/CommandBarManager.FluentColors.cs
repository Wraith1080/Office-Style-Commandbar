using System.ComponentModel;
using System.Drawing;
using System.Windows.Forms;
using CommandBars.Controls;
using CommandBars.Model;
using CommandBars.Persistence;
using CommandBars.Rendering;

namespace CommandBars;

public partial class CommandBarManager
{
    private FluentBasePalette _fluentBasePalette;
    private Color _fluentBaseColor = Color.Empty;
    private Color _fluentAccentColor = Color.Empty;
    private int _fluentTintStrength = 50;

    [Category("Fluent colors"), DefaultValue(FluentBasePalette.Neutral)]
    [Description("The Fluent surface palette. Neutral preserves the original gray surfaces.")]
    public FluentBasePalette FluentBasePalette
    {
        get => _fluentBasePalette;
        set => SetFluentColors(new(value, _fluentBaseColor, _fluentAccentColor, _fluentTintStrength));
    }

    [Category("Fluent colors"), DefaultValue(typeof(Color), "")]
    [Description("Surface color used by the Custom base palette. Empty uses Cool Blue.")]
    public Color FluentBaseColor
    {
        get => _fluentBaseColor;
        set => SetFluentColors(new(_fluentBasePalette, value, _fluentAccentColor, _fluentTintStrength));
    }

    [Category("Fluent colors"), DefaultValue(typeof(Color), "")]
    [Description("Custom Fluent accent. Empty uses ColorScheme. Pale inputs are darkened for readable marks.")]
    public Color FluentAccentColor
    {
        get => _fluentAccentColor;
        set => SetFluentColors(new(_fluentBasePalette, _fluentBaseColor, value, _fluentTintStrength));
    }

    [Category("Fluent colors"), DefaultValue(50)]
    [Description("Surface tint strength from 0 (neutral) to 100. All surfaces remain light.")]
    public int FluentTintStrength
    {
        get => _fluentTintStrength;
        set => SetFluentColors(new(_fluentBasePalette, _fluentBaseColor, _fluentAccentColor, value));
    }

    /// <summary>Returns an immutable snapshot suitable for a preview or standalone renderer.</summary>
    public FluentColorOptions GetFluentColors() => new(_fluentBasePalette, _fluentBaseColor, _fluentAccentColor, _fluentTintStrength);

    /// <summary>Applies all Fluent color settings together, refreshing the theme once.</summary>
    public void SetFluentColors(FluentColorOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        if (_fluentBasePalette == options.BasePalette && _fluentBaseColor == options.BaseColor &&
            _fluentAccentColor == options.AccentColor && _fluentTintStrength == options.TintStrength) return;
        StoreFluentColors(options);
        if (_paletteTheme != CommandBarTheme.Fluent) return;
        _renderer = new FluentRenderer(_colorScheme, options);
        ApplyThemeToHosts();
    }

    private void StoreFluentColors(FluentColorOptions options)
    {
        _fluentBasePalette = options.BasePalette;
        _fluentBaseColor = options.BaseColor;
        _fluentAccentColor = options.AccentColor;
        _fluentTintStrength = options.TintStrength;
    }

    private CommandBarRenderer CreatePreferredRenderer(CommandBarTheme theme)
        => theme == CommandBarTheme.Fluent ? new FluentRenderer(_colorScheme, GetFluentColors()) : ThemeRenderer.Create(theme, _colorScheme);

    private void RestoreFluentColors(LayoutState state)
    {
        static Color Decode(int? value) => value.HasValue && Color.FromArgb(value.Value).A == 255
            ? Color.FromArgb(value.Value) : Color.Empty;
        var palette = Enum.TryParse<FluentBasePalette>(state.FluentBasePalette, out var parsed) &&
            Enum.IsDefined(typeof(FluentBasePalette), parsed) ? parsed : FluentBasePalette.Neutral;
        StoreFluentColors(new(palette, Decode(state.FluentBaseColor), Decode(state.FluentAccentColor),
            Math.Clamp(state.FluentTintStrength, 0, 100)));
    }

    private void AddFluentColorMenu(CommandBarPopupItem popup)
    {
        var bases = popup.DropDown.Items.AddPopup("&Base color");
        foreach (var palette in Enum.GetValues<FluentBasePalette>().Where(p => p != FluentBasePalette.Custom))
        {
            bases.DropDown.Items.AddToggle(new Command("fluent-base:" + palette)
            {
                Text = palette == FluentBasePalette.CoolBlue ? "Cool Blue" : palette.ToString(),
                IsCheckable = true, RadioCheck = true,
                Checked = _fluentBasePalette == palette ? CommandCheckState.Checked : CommandCheckState.Unchecked,
                ExecuteHandler = _ => FluentBasePalette = palette,
            });
        }
        bases.DropDown.Items.AddSeparator();
        bases.DropDown.Items.AddButton(new Command("fluent-colors:custom")
        {
            Text = "&Custom colors...",
            ExecuteHandler = _ =>
            {
                var owner = _hosts.Select(h => h.FindForm()).FirstOrDefault(f => f is not null && !f.IsDisposed);
                if (owner is null || !owner.IsHandleCreated) return;
                // Let the menu dismiss before creating the modal color picker.
                owner.BeginInvoke(new Action(() =>
                {
                    if (!owner.IsDisposed) ShowFluentColorDialog(owner);
                }));
            },
        });
    }

    /// <summary>Shows a local preview and applies colors only on OK. Does not switch themes.</summary>
    public DialogResult ShowFluentColorDialog(IWin32Window? owner = null)
    {
        using var dialog = new FluentColorDialog(_colorScheme, GetFluentColors());
        var result = dialog.ShowDialog(owner);
        if (result == DialogResult.OK) SetFluentColors(dialog.SelectedColors);
        return result;
    }
}
