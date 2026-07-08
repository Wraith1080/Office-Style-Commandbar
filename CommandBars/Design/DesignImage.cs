using System.Drawing;
using System.IO;
using System.Linq;
using CommandBars.Imaging;

namespace CommandBars.Design;

/// <summary>
/// Loads an <see cref="IImageSource"/> from a file path for design-time and
/// runtime use, and relativizes picked paths against the project folder. Both
/// the SVG <see cref="UITypeEditor"/> thumbnail painter and the runtime realizer
/// go through here so a picked icon looks the same in the Properties grid, on the
/// design surface, and at run time.
///
/// Paths may be absolute or project-relative (e.g. <c>Icons\save.svg</c>). A
/// relative path is resolved against, in order: the discovered project root, the
/// app base directory (its output folder at run time), and the current
/// directory. Loads are cached; failures return null rather than throwing —
/// nothing here should ever take down the Visual Studio designer.
/// </summary>
public static class DesignImage
{
    private static readonly Dictionary<string, IImageSource?> Cache =
        new(StringComparer.OrdinalIgnoreCase);

    private static readonly HashSet<string> VectorExtensions =
        new(StringComparer.OrdinalIgnoreCase) { ".svg" };

    // The directory that contains the project's .csproj, once discovered.
    private static string? _projectRoot;

    /// <summary>
    /// Records the project root directory (the folder containing the .csproj).
    /// Called when a file is picked, so the current design session can resolve
    /// project-relative paths immediately.
    /// </summary>
    public static void SetProjectRoot(string? directory)
    {
        if (!string.IsNullOrWhiteSpace(directory) && Directory.Exists(directory))
            _projectRoot = directory;
    }

    /// <summary>
    /// Returns <paramref name="absolutePath"/> made relative to the project
    /// folder when it lives inside it; otherwise returns it unchanged. Also
    /// caches the discovered project root for later resolution.
    /// </summary>
    public static string Relativize(string absolutePath)
    {
        if (string.IsNullOrWhiteSpace(absolutePath))
            return absolutePath;

        try
        {
            string full = Path.GetFullPath(absolutePath);
            string? root = FindProjectRootFrom(Path.GetDirectoryName(full));
            if (root is not null && IsInside(root, full))
            {
                SetProjectRoot(root);
                return Path.GetRelativePath(root, full);
            }
        }
        catch
        {
            // Fall through and keep the absolute path.
        }

        return absolutePath;
    }

    /// <summary>
    /// Loads (and caches) an image source for a path, or returns null when the
    /// path is empty, missing, or unreadable.
    /// </summary>
    public static IImageSource? Load(string? path)
    {
        if (string.IsNullOrWhiteSpace(path))
            return null;

        if (Cache.TryGetValue(path, out var cached))
            return cached;

        IImageSource? source = null;
        try
        {
            string? resolved = Resolve(path);
            if (resolved is not null && File.Exists(resolved))
            {
                string ext = Path.GetExtension(resolved);
                if (VectorExtensions.Contains(ext))
                {
                    source = SvgImageSource.FromFile(resolved);
                }
                else
                {
                    // Load the raster into memory so the designer doesn't hold
                    // the file handle open.
                    using var stream = File.OpenRead(resolved);
                    var bitmap = new Bitmap(stream);
                    source = new BitmapImageSource(bitmap, resolved);
                }
            }
        }
        catch
        {
            source = null;
        }

        Cache[path] = source;
        return source;
    }

    /// <summary>Clears the load cache (e.g. after a file on disk changed).</summary>
    public static void ClearCache() => Cache.Clear();

    // Turns a possibly-relative path into an absolute one that exists, trying the
    // project root, the app base directory, and the current directory in turn.
    private static string? Resolve(string path)
    {
        if (Path.IsPathRooted(path))
            return path;

        foreach (string baseDir in ResolutionBases())
        {
            if (string.IsNullOrEmpty(baseDir))
                continue;
            try
            {
                string candidate = Path.GetFullPath(Path.Combine(baseDir, path));
                if (File.Exists(candidate))
                    return candidate;
            }
            catch
            {
                // Ignore a malformed base and try the next.
            }
        }

        return path; // last resort: let File.Exists fail on the raw value
    }

    private static IEnumerable<string> ResolutionBases()
    {
        string? root = _projectRoot ?? FindProjectRootFrom(AppContext.BaseDirectory)
                                    ?? FindProjectRootFrom(AssemblyDirectory())
                                    ?? FindProjectRootFrom(Environment.CurrentDirectory);
        if (root is not null)
            yield return root;
        yield return AppContext.BaseDirectory;
        yield return Environment.CurrentDirectory;
        string asm = AssemblyDirectory();
        if (!string.IsNullOrEmpty(asm))
            yield return asm;
    }

    // Walks up from a starting directory looking for the folder that holds the
    // project's .csproj. Caches the result in _projectRoot.
    private static string? FindProjectRootFrom(string? startDirectory)
    {
        if (_projectRoot is not null)
            return _projectRoot;

        string? dir = startDirectory;
        for (int i = 0; i < 10 && !string.IsNullOrEmpty(dir); i++)
        {
            try
            {
                if (Directory.EnumerateFiles(dir, "*.csproj").Any())
                {
                    _projectRoot = dir;
                    return dir;
                }
            }
            catch
            {
                // Unreadable directory — stop walking this branch.
                break;
            }
            dir = Path.GetDirectoryName(dir);
        }

        return null;
    }

    private static bool IsInside(string root, string fullPath)
    {
        string normalizedRoot = Path.GetFullPath(root)
            .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
            + Path.DirectorySeparatorChar;
        return Path.GetFullPath(fullPath)
            .StartsWith(normalizedRoot, StringComparison.OrdinalIgnoreCase);
    }

    private static string AssemblyDirectory()
    {
        try
        {
            string location = typeof(DesignImage).Assembly.Location;
            return string.IsNullOrEmpty(location)
                ? string.Empty
                : Path.GetDirectoryName(location) ?? string.Empty;
        }
        catch
        {
            return string.Empty;
        }
    }
}
