using System.Collections.Concurrent;
using System.IO;
using System.Text.Json;
using Wavy.Core.Models;

namespace Wavy.Android.Services;

/// <summary>Android cache service — same responsibilities as the Windows CacheService.</summary>
public sealed class AndroidCacheService
{
    private readonly AndroidAppPaths _paths;
    private readonly string _historyPath;
    private readonly string _analysisPath;
    private readonly ConcurrentDictionary<string, string> _audioFiles = new(StringComparer.OrdinalIgnoreCase);

    private static readonly JsonSerializerOptions Json = new() { WriteIndented = false };

    public AndroidCacheService(AndroidAppPaths paths)
    {
        _paths       = paths;
        _historyPath = Path.Combine(paths.DataRoot, "history.json");
        _analysisPath = Path.Combine(paths.DataRoot, "analysis.json");
        IndexAudioFiles();
    }

    // ── Listening history ───────────────────────────────────────────────────

    public IReadOnlyList<Track> GetCachedTracks()
    {
        try
        {
            if (!File.Exists(_historyPath)) return Array.Empty<Track>();
            return JsonSerializer.Deserialize<List<Track>>(File.ReadAllText(_historyPath), Json)
                   ?? Array.Empty<Track>();
        }
        catch { return Array.Empty<Track>(); }
    }

    public void RecordPlay(Track track)
    {
        try
        {
            var history = GetCachedTracks().Where(t => t.Id != track.Id).Prepend(track).Take(100).ToList();
            File.WriteAllText(_historyPath, JsonSerializer.Serialize(history, Json));
        }
        catch { }
    }

    // ── Offline audio ────────────────────────────────────────────────────────

    public string? TryGetLocalPath(string trackId)
    {
        _audioFiles.TryGetValue(trackId, out var path);
        return path is not null && File.Exists(path) ? path : null;
    }

    public string GetSavePath(string trackId) =>
        Path.Combine(_paths.AudioCache, $"{trackId}.m4a");

    public void RegisterLocalFile(string trackId, string path)
    {
        _audioFiles[trackId] = path;
    }

    // ── Usage stats ──────────────────────────────────────────────────────────

    public (int Count, long Bytes) Usage()
    {
        try
        {
            var files = Directory.GetFiles(_paths.AudioCache);
            return (files.Length, files.Sum(f => new FileInfo(f).Length));
        }
        catch { return (0, 0); }
    }

    public int ClearAll()
    {
        try
        {
            var files = Directory.GetFiles(_paths.AudioCache);
            foreach (var f in files) File.Delete(f);
            _audioFiles.Clear();
            return files.Length;
        }
        catch { return 0; }
    }

    // ── Analysis cache (loudness / EQ, stored as JSON) ───────────────────────

    public (double IntegratedLufs, double Peak)? GetAudioAnalysis(string sourcePath, string signature)
    {
        try
        {
            if (!File.Exists(_analysisPath)) return null;
            var dict = JsonSerializer.Deserialize<Dictionary<string, double[]>>(
                File.ReadAllText(_analysisPath), Json);
            if (dict?.TryGetValue(signature, out var v) == true && v.Length >= 2)
                return (v[0], v[1]);
        }
        catch { }
        return null;
    }

    public void SaveAudioAnalysis(string sourcePath, string signature, double lufs, double peak)
    {
        try
        {
            Dictionary<string, double[]> dict;
            try
            {
                dict = File.Exists(_analysisPath)
                    ? JsonSerializer.Deserialize<Dictionary<string, double[]>>(File.ReadAllText(_analysisPath), Json)
                      ?? new()
                    : new();
            }
            catch { dict = new(); }

            dict[signature] = new[] { lufs, peak };
            if (dict.Count > 500)
                foreach (var key in dict.Keys.Take(100).ToList()) dict.Remove(key);

            File.WriteAllText(_analysisPath, JsonSerializer.Serialize(dict, Json));
        }
        catch { }
    }

    private void IndexAudioFiles()
    {
        try
        {
            foreach (var f in Directory.EnumerateFiles(_paths.AudioCache, "*.m4a"))
            {
                var id = Path.GetFileNameWithoutExtension(f);
                _audioFiles[id] = f;
            }
        }
        catch { }
    }
}
