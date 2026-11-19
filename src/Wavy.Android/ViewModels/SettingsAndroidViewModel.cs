using Wavy.Android.Services;
using Wavy.Core.Models;

namespace Wavy.Android.ViewModels;

public sealed partial class SettingsAndroidViewModel : ObservableObject
{
    private readonly AndroidSettingsService _settings;
    private readonly AndroidCacheService    _cache;

    private static readonly int[] KeepDays = { 1, 7, 30, 0 };

    public SettingsAndroidViewModel(AndroidSettingsService settings, AndroidCacheService cache)
    {
        _settings = settings;
        _cache    = cache;
        _settings.Changed += (_, name) =>
        {
            if (name == nameof(AndroidSettingsService.SearchHistory)) OnPropertyChanged(nameof(SearchHistory));
        };
    }

    public int ThemeIndex
    {
        get => (int)_settings.Theme;
        set { if (value < 0 || value == ThemeIndex) return; _settings.Theme = (AppTheme)value; OnPropertyChanged(); }
    }

    public bool AudioNormalization
    {
        get => _settings.AudioNormalization;
        set { if (value == _settings.AudioNormalization) return; _settings.AudioNormalization = value; OnPropertyChanged(); }
    }

    public double[] EqualizerBands
    {
        get => _settings.EqualizerBands;
        set { _settings.EqualizerBands = value; OnPropertyChanged(); }
    }

    public int AudioQualityIndex
    {
        get => (int)_settings.AudioQuality;
        set { if (value < 0 || value > 2) return; _settings.AudioQuality = (AudioQuality)value; OnPropertyChanged(); }
    }

    public bool PrefetchNext
    {
        get => _settings.PrefetchNext;
        set { if (value == _settings.PrefetchNext) return; _settings.PrefetchNext = value; OnPropertyChanged(); }
    }

    public int LyricsSourceIndex
    {
        get => (int)_settings.LyricsSource;
        set { if (value < 0 || value == LyricsSourceIndex) return; _settings.LyricsSource = (LyricsSource)value; OnPropertyChanged(); }
    }

    public IReadOnlyList<string> SearchHistory    => _settings.SearchHistory;
    public bool HasSearchHistory                  => SearchHistory.Count > 0;

    public void ClearSearchHistory()
    {
        _settings.ClearSearchHistory();
        OnPropertyChanged(nameof(SearchHistory));
        OnPropertyChanged(nameof(HasSearchHistory));
    }

    public int KeepIndex
    {
        get { var i = Array.IndexOf(KeepDays, _settings.KeepOfflineDays); return i >= 0 ? i : 0; }
        set { if (value < 0 || value >= KeepDays.Length || value == KeepIndex) return; _settings.KeepOfflineDays = KeepDays[value]; OnPropertyChanged(); }
    }

    public string CacheSummary
    {
        get
        {
            var (count, bytes) = _cache.Usage();
            if (count == 0) return "Nothing saved offline yet.";
            return $"{(count == 1 ? "1 song" : $"{count} songs")}, {FormatBytes(bytes)} on disk.";
        }
    }

    public string ClearCache()
    {
        var n = _cache.ClearAll();
        OnPropertyChanged(nameof(CacheSummary));
        return n switch { 0 => "Nothing to clear.", 1 => "Removed 1 saved song.", _ => $"Removed {n} saved songs." };
    }

    public string Version =>
        typeof(SettingsAndroidViewModel).Assembly.GetName().Version is { } v
            ? $"Version {v.Major}.{v.Minor}" : "Version unknown";

    private static string FormatBytes(long b) => b switch
    {
        >= 1L << 30 => $"{b / (double)(1L << 30):0.#} GB",
        >= 1L << 20 => $"{b / (double)(1L << 20):0.#} MB",
        >= 1L << 10 => $"{b / (double)(1L << 10):0.#} KB",
        _           => $"{b} B",
    };
}
