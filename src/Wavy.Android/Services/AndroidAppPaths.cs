using System.IO;

namespace Wavy.Android.Services;

/// <summary>
/// Android app paths using MAUI's FileSystem API — safe to call at any point,
/// no Android Context required.
/// </summary>
public sealed class AndroidAppPaths
{
    public AndroidAppPaths()
    {
        // FileSystem.AppDataDirectory is MAUI's safe, platform-agnostic data dir.
        // On Android this maps to the app's private internal storage.
        var root = Microsoft.Maui.Storage.FileSystem.AppDataDirectory;
        DataRoot   = EnsureDir(Path.Combine(root, "wavy"));
        AudioCache = EnsureDir(Path.Combine(DataRoot, "cache", "audio"));
        ArtCache   = EnsureDir(Path.Combine(DataRoot, "cache", "art"));
    }

    public string DataRoot   { get; }
    public string AudioCache { get; }
    public string ArtCache   { get; }

    // Android cannot run yt-dlp subprocess
    public string? YtDlpPath  => null;
    public string? FfmpegPath => null;

    private static string EnsureDir(string path)
    {
        Directory.CreateDirectory(path);
        return path;
    }
}
