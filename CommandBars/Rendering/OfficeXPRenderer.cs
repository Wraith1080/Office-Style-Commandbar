namespace CommandBars.Rendering;

/// <summary>
/// The Office XP look. Reuses the 2003 renderer's drawing but with a flat gray
/// color table and square corners (chunk radius 0), so bars and buttons render
/// flat with the XP blue selection.
/// </summary>
public sealed class OfficeXPRenderer : Office2003Renderer
{
    public override CommandBarColorTable Colors { get; } = new OfficeXPColorTable();

    protected override int ChunkRadius => 0;
}
