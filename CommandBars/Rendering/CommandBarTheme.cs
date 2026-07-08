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
        _ => new Office2003Renderer(),
    };
}
