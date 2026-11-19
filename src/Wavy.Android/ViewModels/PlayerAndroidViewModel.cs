using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Wavy.Android.Services;
using Wavy.Core.Abstractions;
using Wavy.Core.Models;

namespace Wavy.Android.ViewModels;

public sealed partial class PlayerAndroidViewModel : ObservableObject
{
    public enum RepeatMode { None, All, One }

    private readonly AndroidPlaybackService _playback;
    private readonly IYtDlpService _catalogue;
    private readonly AndroidCacheService _cache;

    private List<Track>? _queue;
    private List<Track>? _unshuffled;
    private int _index = -1;
    private bool _isShuffled;
    private RepeatMode _repeat = RepeatMode.None;

    [ObservableProperty] private bool _isQueueOpen;
    [ObservableProperty] private bool _isLyricsOpen;

    public ObservableCollection<Track> Queue { get; } = new();

    public PlayerAndroidViewModel(AndroidPlaybackService playback, IYtDlpService catalogue, AndroidCacheService cache)
    {
        _playback  = playback;
        _catalogue = catalogue;
        _cache     = cache;

        _playback.PropertyChanged += (_, e) => OnPropertyChanged(e.PropertyName);
        _playback.PlaybackCompleted += async (_, _) => await OnTrackCompletedAsync();
    }

    // ── Forwarded playback state ──────────────────────────────────────────────
    public Track?  CurrentTrack    => _playback.CurrentTrack;
    public bool    IsPlaying       => _playback.IsPlaying;
    public bool    IsBuffering     => _playback.IsBuffering;
    public bool    IsBusy          => _playback.IsBusy;
    public double  PositionSeconds => _playback.PositionSeconds;
    public double  DurationSeconds => _playback.DurationSeconds;
    public string? StatusMessage   => _playback.StatusMessage;
    public string? CurrentStreamUrl => _playback.CurrentStreamUrl;

    public string PositionText => TimeSpan.FromSeconds(PositionSeconds).ToString(PositionSeconds >= 3600 ? @"h\:mm\:ss" : @"m\:ss");
    public string DurationText => TimeSpan.FromSeconds(DurationSeconds).ToString(DurationSeconds >= 3600 ? @"h\:mm\:ss" : @"m\:ss");

    public event EventHandler<string?>? StreamResolved
    {
        add    => _playback.StreamResolved += value;
        remove => _playback.StreamResolved -= value;
    }

    public double Volume
    {
        get => _playback.Volume;
        set => _playback.Volume = value;
    }

    // ── Queue controls ────────────────────────────────────────────────────────

    public bool HasPrevious => _index > 0;
    public bool HasNext     => _index >= 0 && _index < (_queue?.Count ?? 0) - 1;

    public bool IsShuffled
    {
        get => _isShuffled;
        private set { SetProperty(ref _isShuffled, value); OnPropertyChanged(nameof(ShuffleLabel)); }
    }
    public string ShuffleLabel => IsShuffled ? "Shuffle on" : "Shuffle";

    public RepeatMode Repeat
    {
        get => _repeat;
        private set { SetProperty(ref _repeat, value); OnPropertyChanged(nameof(RepeatLabel)); }
    }
    public string RepeatLabel => Repeat switch
    {
        RepeatMode.One => "Repeat one",
        RepeatMode.All => "Repeat all",
        _              => "Repeat",
    };

    [RelayCommand]
    public void TogglePlayPause() => _playback.TogglePlayPause();

    [RelayCommand]
    public async Task PreviousAsync()
    {
        if (_index > 0) await PlayIndexAsync(_index - 1);
    }

    [RelayCommand]
    public async Task NextAsync()
    {
        if (HasNext) await PlayIndexAsync(_index + 1);
    }

    [RelayCommand]
    public void ToggleShuffle()
    {
        if (!IsShuffled)
        {
            _unshuffled = _queue?.ToList();
            if (_queue is not null)
            {
                var current = _index >= 0 && _index < _queue.Count ? _queue[_index] : null;
                var rest = _queue.Where(t => t != current).OrderBy(_ => Guid.NewGuid()).ToList();
                _queue.Clear();
                if (current is not null) { _queue.Add(current); _index = 0; }
                foreach (var t in rest) _queue.Add(t);
                RebuildQueueCollection();
            }
            IsShuffled = true;
        }
        else
        {
            if (_unshuffled is not null)
            {
                var current = CurrentTrack;
                _queue = _unshuffled;
                _unshuffled = null;
                _index = current is not null ? _queue.IndexOf(current) : 0;
                if (_index < 0) _index = 0;
                RebuildQueueCollection();
            }
            IsShuffled = false;
        }
    }

    [RelayCommand]
    public void ToggleRepeat() => Repeat = Repeat switch
    {
        RepeatMode.None => RepeatMode.All,
        RepeatMode.All  => RepeatMode.One,
        _               => RepeatMode.None,
    };

    public async Task PlayAsync(Track track)
    {
        _queue = new List<Track> { track };
        _index = 0;
        RebuildQueueCollection();
        await _playback.PlayAsync(track);
        NotifyNavigation();
    }

    public async Task PlayFromAsync(IList<Track> tracks, int index)
    {
        _queue = tracks.ToList();
        _index = Math.Clamp(index, 0, _queue.Count - 1);
        RebuildQueueCollection();
        await _playback.PlayAsync(_queue[_index]);
        NotifyNavigation();
    }

    public async Task PlayRadioAsync(Track seed)
    {
        await PlayAsync(seed);
        _ = ExtendQueueAsync(seed);
    }

    public async Task PlayShuffledAsync(IList<Track> tracks)
    {
        var shuffled = tracks.OrderBy(_ => Guid.NewGuid()).ToList();
        await PlayFromAsync(shuffled, 0);
        IsShuffled = true;
    }

    public void AddToQueue(Track track)
    {
        _queue ??= new List<Track>();
        _queue.Add(track);
        Queue.Add(track);
    }

    private async Task OnTrackCompletedAsync()
    {
        switch (Repeat)
        {
            case RepeatMode.One:
                if (_queue is not null && _index >= 0 && _index < _queue.Count)
                    await _playback.PlayAsync(_queue[_index]);
                break;
            case RepeatMode.All:
                var next = (_index + 1) % (_queue?.Count ?? 1);
                await PlayIndexAsync(next);
                break;
            default:
                if (HasNext) await PlayIndexAsync(_index + 1);
                break;
        }
    }

    private async Task PlayIndexAsync(int index)
    {
        if (_queue is null || index < 0 || index >= _queue.Count) return;
        _index = index;
        await _playback.PlayAsync(_queue[_index]);
        NotifyNavigation();
    }

    private async Task ExtendQueueAsync(Track seed)
    {
        try
        {
            var similar = await _catalogue.GetSimilarAsync(seed, 10);
            foreach (var t in similar.Where(t => _queue?.All(q => q.Id != t.Id) == true))
            {
                _queue?.Add(t);
                Queue.Add(t);
            }
        }
        catch { /* radio extension is best-effort */ }
    }

    private void RebuildQueueCollection()
    {
        Queue.Clear();
        if (_queue is null) return;
        foreach (var t in _queue) Queue.Add(t);
    }

    private void NotifyNavigation()
    {
        OnPropertyChanged(nameof(HasPrevious));
        OnPropertyChanged(nameof(HasNext));
    }
}
