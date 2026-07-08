namespace CommandBars.Rendering;

/// <summary>Visual state flags passed to the renderer for an item.</summary>
[Flags]
public enum RenderState
{
    Normal = 0,

    /// <summary>Pointer is over the item (hover / hot-tracked).</summary>
    Hot = 1,

    /// <summary>Item is being pressed (mouse down on it).</summary>
    Pressed = 2,

    /// <summary>Toggle item is checked, or a popup is currently open.</summary>
    Checked = 4,

    /// <summary>Item is disabled.</summary>
    Disabled = 8,
}
