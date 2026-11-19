using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using Wavy.Android.Services;
using Wavy.Core.Models;
using Wavy.Core.Services;

namespace Wavy.Android.ViewModels;

/// <summary>Wraps a LyricsLine with an IsActive observable for the Android lyrics view.</summary>
public sealed partial class LyricsLineViewModel : ObservableObject
{
    public required string Text         { get; init; }
    public double? StartSeconds         { get; init; }

    [ObservableProperty] private bool _isActive;

    public double Opacity => IsActive ? 1.0 : 0.35;

    partial void OnIsActiveChanged(bool value) => OnPropertyChanged(nameof(Opacity));
}

namespace Wavy.Android.ViewModels;

public sealed partial class LyricsAndroidViewModel : ObservableObject
{
    private readonly PlayerAndroidViewModel _player;
    private readonly AndroidSettingsService _settings;
    private readonly LyricsClient _client = new();
    private CancellationTokenSource? _cts;
    private Track? _loadedFor;

    [ObservableProperty] private bool _isLoading;
    [ObservableProperty] private string? _message;
    [ObservableProperty] private string? _provider;
    [ObservableProperty] private bool _isSynced;
    [ObservableProperty] private int _activeIndex = -1;

    public ObservableCollection<LyricsLineViewModel> Lines { get; } = new();
    public bool ShowUnsyncedNotice => Lines.Count > 0 && !IsSynced;

    public LyricsAndroidViewModel(PlayerAndroidViewModel player, AndroidSettingsService settings)
    {
        _player   = player;
        _settings = settings;

        _player.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(PlayerAndroidViewModel.CurrentTrack))
                _ = ReloadAsync();
            else if (e.PropertyName == nameof(PlayerAndroidViewModel.PositionSeconds))
                Highlight(_player.PositionSeconds);
        };
    }

    public async Task ReloadAsync()
    {
        var track = _player.CurrentTrack;
        if (track is null) { Reset("Play a song to see its lyrics."); return; }
        if (ReferenceEquals(track, _loadedFor)) return;
        _loadedFor = track;

        _cts?.Cancel();
        _cts = new CancellationTokenSource();
        var ct = _cts.Token;

        Reset(null);
        IsLoading = true;
        try
        {
            var found = await _client.GetAsync(
                track.Title, track.Artist, _player.DurationSeconds, _settings.LyricsSource, ct, track.Album);

            if (ct.IsCancellationRequested || !ReferenceEquals(track, _loadedFor)) return;
            if (found is null || found.Lines.Count == 0)
            { _loadedFor = null; Reset("No lyrics found."); return; }

            Lines.Clear();
            foreach (var line in found.Lines)
                Lines.Add(new LyricsLineViewModel { Text = line.Text, StartSeconds = line.StartSeconds });
            Provider = found.Provider;
            IsSynced = found.IsSynced;
            OnPropertyChanged(nameof(ShowUnsyncedNotice));
            ActiveIndex = -1;
            Message     = null;
            Highlight(_player.PositionSeconds);
        }
        catch (OperationCanceledException) { }
        catch (Exception ex) { _loadedFor = null; Reset($"Couldn't load lyrics: {ex.Message}"); }
        finally { if (!ct.IsCancellationRequested) IsLoading = false; }
    }

    private void Reset(string? msg)
    {
        Lines.Clear();
        ActiveIndex = -1;
        Provider    = null;
        IsSynced    = false;
        IsLoading   = false;
        Message     = msg;
        OnPropertyChanged(nameof(ShowUnsyncedNotice));
    }

    private void Highlight(double seconds)
    {
        if (!IsSynced || Lines.Count == 0) return;
        var idx = -1;
        for (var i = 0; i < Lines.Count; i++)
        {
            if (Lines[i].StartSeconds is { } s && s <= seconds + 0.15) idx = i;
            else break;
        }
        if (idx == ActiveIndex) return;
        if (ActiveIndex >= 0 && ActiveIndex < Lines.Count) Lines[ActiveIndex].IsActive = false;
        ActiveIndex = idx;
        if (idx >= 0 && idx < Lines.Count) Lines[idx].IsActive = true;
    }
}
