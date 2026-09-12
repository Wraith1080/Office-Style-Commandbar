namespace CommandBars.Rendering;

/// <summary>
/// The Office XP look. Reuses the 2003 renderer's drawing but with a flat gray
/// color table and square corners (chunk radius 0), so bars and buttons render
/// flat with the XP blue selection.
/// </summary>
public sealed class OfficeXPRenderer : Office2003Renderer
{
    public OfficeXPRenderer() : this(CommandBarColorScheme.Default) { }
    public OfficeXPRenderer(CommandBarColorScheme scheme)
        => Colors = SchemeColorTable.Create(new OfficeXPColorTable(), CommandBarTheme.OfficeXP, scheme);
    public override CommandBarColorTable Colors { get; }

    protected override int ChunkRadius => 0;
}
