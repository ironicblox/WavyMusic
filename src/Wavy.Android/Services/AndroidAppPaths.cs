using System.IO;

namespace Wavy.Android.Services;

/// <summary>Android equivalent of the Windows AppPaths static class.</summary>
public sealed class AndroidAppPaths
{
    public AndroidAppPaths()
    {
        // Android app data lives in the app's private data dir — no special permissions needed
        var root = global::Android.App.Application.Context.FilesDir!.AbsolutePath;
        DataRoot   = EnsureDir(Path.Combine(root, "wavy"));
        AudioCache = EnsureDir(Path.Combine(DataRoot, "cache", "audio"));
        ArtCache   = EnsureDir(Path.Combine(DataRoot, "cache", "art"));
    }

    public string DataRoot   { get; }
    public string AudioCache { get; }
    public string ArtCache   { get; }

    // Android can't run yt-dlp — we have no bundled binary
    public string? YtDlpPath  => null;
    public string? FfmpegPath => null;

    private static string EnsureDir(string path)
    {
        Directory.CreateDirectory(path);
        return path;
    }
}
