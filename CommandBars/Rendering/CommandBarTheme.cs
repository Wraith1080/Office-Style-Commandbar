namespace CommandBars.Rendering;

/// <summary>
/// The visual themes the control ships with. Set
/// <see cref="CommandBarManager.Theme"/> to one of these (including from the
/// Properties window at design time) to re-skin every hosted bar.
/// </summary>
public enum CommandBarTheme
{
    Office2003,
    OfficeXP,
    Office2007,
    Office2010,
    Dark,
    OliveGreen,
}

/// <summary>Stable keys for the themes supplied by CommandBars.</summary>
public static class CommandBarThemeKeys
{
    public const string Office2003 = "office2003";
    public const string OfficeXP = "officexp";
    public const string Office2007 = "office2007";
    public const string Office2010Silver = "office2010silver";
    public const string Dark = "dark";
    public const string OliveGreen = "olivegreen";

    internal static string FromTheme(CommandBarTheme theme) => theme switch
    {
        CommandBarTheme.OfficeXP => OfficeXP,
        CommandBarTheme.Office2007 => Office2007,
        CommandBarTheme.Office2010 => Office2010Silver,
        CommandBarTheme.Dark => Dark,
        CommandBarTheme.OliveGreen => OliveGreen,
        _ => Office2003,
    };

    internal static bool TryToTheme(string key, out CommandBarTheme theme)
    {
        theme = key switch
        {
            OfficeXP => CommandBarTheme.OfficeXP,
            Office2007 => CommandBarTheme.Office2007,
            Office2010Silver => CommandBarTheme.Office2010,
            Dark => CommandBarTheme.Dark,
            OliveGreen => CommandBarTheme.OliveGreen,
            _ => CommandBarTheme.Office2003,
        };
        return key is Office2003 or OfficeXP or Office2007 or Office2010Silver or Dark or OliveGreen;
    }
}

/// <summary>An application-managed entry shown by a dynamic theme-list popup.</summary>
public sealed class CommandBarThemeRegistration
{
    public CommandBarThemeRegistration(string key, string text, Func<CommandBarRenderer> rendererFactory)
    {
        Key = string.IsNullOrWhiteSpace(key)
            ? throw new ArgumentException("A theme key is required.", nameof(key))
            : key;
        Text = text ?? string.Empty;
        RendererFactory = rendererFactory ?? throw new ArgumentNullException(nameof(rendererFactory));
    }

    public string Key { get; }
    public string Text { get; }
    public Func<CommandBarRenderer> RendererFactory { get; }
}

/// <summary>Maps a <see cref="CommandBarTheme"/> to a concrete renderer.</summary>
public static class ThemeRenderer
{
    /// <summary>Creates a fresh renderer instance for a theme.</summary>
    public static CommandBarRenderer Create(CommandBarTheme theme) => theme switch
    {
        CommandBarTheme.OfficeXP => new OfficeXPRenderer(),
        CommandBarTheme.Office2007 => new Office2007Renderer(),
        CommandBarTheme.Office2010 => new Office2010Renderer(),
        CommandBarTheme.Dark => new DarkRenderer(),
        CommandBarTheme.OliveGreen => new OliveGreenRenderer(),
        _ => new Office2003Renderer(),
    };
}
