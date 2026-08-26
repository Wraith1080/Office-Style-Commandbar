namespace CommandBars.Rendering;

/// <summary>
/// The Windows XP "Olive Green" look: the Office 2003 renderer's drawing with an
/// olive-green color table. All chrome colors come from the table, so text,
/// checks, and menus stay legible automatically.
/// </summary>
public sealed class OliveGreenRenderer : Office2003Renderer
{
    public override CommandBarColorTable Colors { get; } = new OliveGreenColorTable();
}
