using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using Wavy.Core.Models;

namespace Wavy.Android.Services;

/// <summary>
/// Android port of the Windows SettingsService.
/// Same JSON format, same keys — data is fully compatible if you copy the file across.
/// WindowPlacement is ignored (not relevant on Android).
/// </summary>
public sealed class AndroidSettingsService
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        Converters = { new JsonStringEnumConverter() },
    };

    private readonly string _path;
    private readonly Stored _stored;

    public AndroidSettingsService(string dataRoot)
    {
        _path   = Path.Combine(dataRoot, "settings.json");
        _stored = Load();
    }

    public event EventHandler<string>? Changed;

    public AppTheme Theme
    {
        get => _stored.Theme;
        set { if (_stored.Theme == value) return; _stored.Theme = value; Commit(nameof(Theme)); }
    }

    public bool AmbientBackground
    {
        get => _stored.AmbientBackground;
        set { if (_stored.AmbientBackground == value) return; _stored.AmbientBackground = value; Commit(nameof(AmbientBackground)); }
    }

    public bool PrefetchNext
    {
        get => _stored.PrefetchNext;
        set { if (_stored.PrefetchNext == value) return; _stored.PrefetchNext = value; Commit(nameof(PrefetchNext)); }
    }

    public double Volume
    {
        get => _stored.Volume;
        set { var v = Math.Clamp(value, 0, 1); if (Math.Abs(_stored.Volume - v) < 0.001) return; _stored.Volume = v; Commit(nameof(Volume)); }
    }

    public LyricsSource LyricsSource
    {
        get => _stored.LyricsSource;
        set { if (_stored.LyricsSource == value) return; _stored.LyricsSource = value; Commit(nameof(LyricsSource)); }
    }

    public bool AudioNormalization
    {
        get => _stored.AudioNormalization;
        set { if (_stored.AudioNormalization == value) return; _stored.AudioNormalization = value; Commit(nameof(AudioNormalization)); }
    }

    public double[] EqualizerBands
    {
        get => Enumerable.Range(0, 10).Select(i =>
            _stored.EqualizerBands is { } b && i < b.Length && double.IsFinite(b[i])
                ? Math.Clamp(b[i], -12, 12) : 0).ToArray();
        set
        {
            _stored.EqualizerBands = Enumerable.Range(0, 10).Select(i =>
                i < value.Length && double.IsFinite(value[i]) ? Math.Clamp(value[i], -12, 12) : 0).ToArray();
            Commit(nameof(EqualizerBands));
        }
    }

    public AudioQuality AudioQuality
    {
        get => _stored.AudioQuality;
        set { if (_stored.AudioQuality == value) return; _stored.AudioQuality = value; Commit(nameof(AudioQuality)); }
    }

    public IReadOnlyList<string> SearchHistory => _stored.SearchHistory ?? Array.Empty<string>();

    public void AddSearchHistory(string query)
    {
        query = query.Trim();
        if (query.Length == 0) return;
        _stored.SearchHistory = SearchHistory
            .Where(x => !string.Equals(x, query, StringComparison.OrdinalIgnoreCase))
            .Prepend(query).Take(20).ToArray();
        Commit(nameof(SearchHistory));
    }

    public void ClearSearchHistory()
    {
        _stored.SearchHistory = Array.Empty<string>();
        Commit(nameof(SearchHistory));
    }

    public void RemoveSearchHistory(string query)
    {
        var h = SearchHistory.Where(x => !string.Equals(x, query, StringComparison.OrdinalIgnoreCase)).ToArray();
        if (h.Length == SearchHistory.Count) return;
        _stored.SearchHistory = h;
        Commit(nameof(SearchHistory));
    }

    public int KeepOfflineDays
    {
        get => _stored.KeepOfflineDays;
        set { var d = Math.Max(0, value); if (_stored.KeepOfflineDays == d) return; _stored.KeepOfflineDays = d; Commit(nameof(KeepOfflineDays)); }
    }

    private void Commit(string name) { Save(); Changed?.Invoke(this, name); }

    private Stored Load()
    {
        try
        {
            if (!File.Exists(_path)) return new();
            return JsonSerializer.Deserialize<Stored>(File.ReadAllText(_path), JsonOptions) ?? new();
        }
        catch { return new(); }
    }

    private void Save()
    {
        try { File.WriteAllText(_path, JsonSerializer.Serialize(_stored, JsonOptions)); }
        catch { }
    }

    private sealed class Stored
    {
        public AppTheme Theme { get; set; } = AppTheme.System;
        public bool AmbientBackground { get; set; } = true;
        public bool PrefetchNext { get; set; } = true;
        public double Volume { get; set; } = 1.0;
        public LyricsSource LyricsSource { get; set; } = LyricsSource.Auto;
        public bool AudioNormalization { get; set; } = true;
        public double[]? EqualizerBands { get; set; }
        public AudioQuality AudioQuality { get; set; } = AudioQuality.Best;
        public string[]? SearchHistory { get; set; }
        public int KeepOfflineDays { get; set; } = 1;
    }
}

// Keep AppTheme enum accessible in the Android project
public enum AppTheme { System, Light, Dark, Darker }
