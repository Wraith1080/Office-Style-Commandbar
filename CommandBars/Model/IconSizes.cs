namespace CommandBars.Model;

/// <summary>
/// The fixed set of logical icon sizes the control offers (decision #4).
/// Sizes are in logical pixels and are multiplied by the DPI scale at render
/// time, which is why button images are vector (SVG).
/// </summary>
public static class IconSizes
{
    /// <summary>Allowed icon sizes, smallest to largest.</summary>
    public static readonly IReadOnlyList<int> Steps = new[] { 12, 16, 20, 24, 32, 48, 64 };

    /// <summary>Default icon size used by new bars.</summary>
    public const int Default = 24;

    /// <summary>True if <paramref name="size"/> is one of the allowed steps.</summary>
    public static bool IsValid(int size) => Steps.Contains(size);

    /// <summary>Snaps an arbitrary size to the nearest allowed step.</summary>
    public static int Nearest(int size)
    {
        int best = Steps[0];
        int bestDiff = Math.Abs(size - best);
        foreach (int step in Steps)
        {
            int diff = Math.Abs(size - step);
            if (diff < bestDiff)
            {
                best = step;
                bestDiff = diff;
            }
        }
        return best;
    }
}
