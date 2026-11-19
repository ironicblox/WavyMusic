using CommunityToolkit.Mvvm.ComponentModel;
using Wavy.Core.Abstractions;
using Wavy.Core.Models;
using Wavy.Core.Services;

namespace Wavy.Android.Services;

/// <summary>
/// Android playback service using MediaElement (MAUI Community Toolkit).
/// Manages stream resolution via the pure-HTTP fast path and plays
/// the resolved audio URL directly — no yt-dlp binary needed.
/// </summary>
public sealed partial class AndroidPlaybackService : ObservableObject, IDisposable
{
    private readonly IYtDlpService _catalogue;
    private readonly AndroidCacheService _cache;
    private readonly AndroidSettingsService _settings;
    private readonly ListeningHistoryService? _history;

    private CancellationTokenSource? _playCts;
    private string? _currentUrl;

    [ObservableProperty] private Track? _currentTrack;
    [ObservableProperty] private bool _isPlaying;
    [ObservableProperty] private bool _isBuffering;
    [ObservableProperty] private bool _isBusy;
    [ObservableProperty] private double _positionSeconds;
    [ObservableProperty] private double _durationSeconds;
    [ObservableProperty] private string? _statusMessage;
    [ObservableProperty] private double _volume = 1.0;

    // MediaElement is bound in PlayerPage and controlled via this service
    public event EventHandler<string?>? StreamResolved;
    public event EventHandler? PlaybackCompleted;
    public event EventHandler? PlaybackFailed;

    public AndroidPlaybackService(IYtDlpService catalogue, AndroidCacheService cache,
        AndroidSettingsService settings, ListeningHistoryService? history = null)
    {
        _catalogue = catalogue;
        _cache     = cache;
        _settings  = settings;
        _history   = history;
        _volume    = settings.Volume;
    }

    public async Task PlayAsync(Track track, CancellationToken ct = default)
    {
        _playCts?.Cancel();
        _playCts = new CancellationTokenSource();
        var token = _playCts.Token;

        CurrentTrack   = track;
        IsBuffering    = true;
        IsBusy         = true;
        StatusMessage  = "Loading…";
        IsPlaying      = false;

        try
        {
            // 1. Try local cached file first
            var localPath = _cache.TryGetLocalPath(track.Id);
            if (localPath is not null)
            {
                token.ThrowIfCancellationRequested();
                _currentUrl = localPath;
                StreamResolved?.Invoke(this, localPath);
                _history?.Advance(track, true, TimeSpan.Zero, DateTimeOffset.UtcNow);
                IsBuffering   = false;
                IsBusy        = false;
                StatusMessage = null;
                IsPlaying     = true;
                return;
            }

            // 2. Resolve via pure-HTTP fast path (no yt-dlp needed)
            token.ThrowIfCancellationRequested();
            StatusMessage = "Connecting…";
            var resolved  = await _catalogue.ResolveStreamAsync(track, token).ConfigureAwait(false);
            token.ThrowIfCancellationRequested();

            _currentUrl = resolved.DashManifest ?? resolved.Url;
            StreamResolved?.Invoke(this, _currentUrl);
            _history?.Advance(track, true, TimeSpan.Zero, DateTimeOffset.UtcNow);
            IsBuffering   = false;
            IsBusy        = false;
            StatusMessage = null;
            IsPlaying     = true;
        }
        catch (OperationCanceledException)
        {
            IsBuffering = false;
            IsBusy      = false;
        }
        catch (Exception ex)
        {
            IsBuffering   = false;
            IsBusy        = false;
            IsPlaying     = false;
            StatusMessage = ex.Message;
            PlaybackFailed?.Invoke(this, EventArgs.Empty);
        }
    }

    public void Pause()  { IsPlaying = false; }
    public void Resume() { if (CurrentTrack is not null) IsPlaying = true; }

    public void TogglePlayPause()
    {
        if (IsPlaying) Pause(); else Resume();
    }

    public string? CurrentStreamUrl => _currentUrl;

    public void Dispose()
    {
        _playCts?.Cancel();
        _playCts?.Dispose();
    }
}
