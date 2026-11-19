using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.Input;
using Wavy.Android.Services;
using Wavy.Core.Abstractions;
using Wavy.Core.Models;
using Wavy.Core.Services;

namespace Wavy.Android.ViewModels;

public sealed partial class HomeAndroidViewModel : ObservableObject
{
    private readonly AndroidCacheService _cache;
    private readonly AndroidPlaylistService _playlists;
    private readonly AndroidFavoritesService _favorites;
    private readonly IYtDlpService _catalogue;
    private readonly PlayerAndroidViewModel _player;
    private readonly RadioFeedbackService? _feedback;

    private CancellationTokenSource? _load;
    private string? _loadedKey;
    private DateTimeOffset _loadedAt;

    [ObservableProperty] private string _greeting  = "Welcome back";
    [ObservableProperty] private bool   _isLoadingSuggestions;
    [ObservableProperty] private string _recommendationReason = "A fresh mix from your listening.";
    [ObservableProperty] private string? _recommendationMessage;

    public ObservableCollection<Track>       Recent      { get; } = new();
    public ObservableCollection<Track>       Suggestions { get; } = new();
    public ObservableCollection<Track>       Trending    { get; } = new();
    public ObservableCollection<Track>       Fresh       { get; } = new();
    public ObservableCollection<Playlist>    Playlists   => _playlists.Playlists;
    public ObservableCollection<AlbumResult> Albums      => _favorites.Albums;

    public bool HasRecent      => Recent.Count > 0;
    public bool HasPlaylists   => Playlists.Count > 0;
    public bool HasAlbums      => Albums.Count > 0;
    public bool HasSuggestions => Suggestions.Count > 0;
    public bool HasTrending    => Trending.Count > 0;
    public bool HasFresh       => Fresh.Count > 0;
    public bool IsEmpty        => !HasRecent && !HasPlaylists && !HasAlbums;

    public HomeAndroidViewModel(AndroidCacheService cache, AndroidPlaylistService playlists,
        AndroidFavoritesService favorites, IYtDlpService catalogue,
        PlayerAndroidViewModel player, RadioFeedbackService? feedback = null)
    {
        _cache     = cache;
        _playlists = playlists;
        _favorites = favorites;
        _catalogue = catalogue;
        _player    = player;
        _feedback  = feedback;

        _playlists.Playlists.CollectionChanged += (_, _) => SectionsChanged();
        _favorites.Albums.CollectionChanged    += (_, _) => SectionsChanged();
    }

    public void Refresh()
    {
        Greeting = DateTime.Now.Hour switch
        {
            < 5  => "Still up?",
            < 12 => "Good morning",
            < 18 => "Good afternoon",
            _    => "Good evening",
        };

        Recent.Clear();
        foreach (var t in _cache.GetCachedTracks().Take(6)) Recent.Add(t);
        SectionsChanged();
        _ = RefreshRecommendationsAsync();
    }

    public async Task RefreshRecommendationsAsync(bool force = false)
    {
        var history = _cache.GetCachedTracks();
        var seeds   = RecommendationSelector.Seeds(history.Take(30), 3);
        var key     = string.Join("|", seeds.Select(t => t.Id));

        if (!force && key == _loadedKey && HasSuggestions
            && DateTimeOffset.UtcNow - _loadedAt < TimeSpan.FromMinutes(30)) return;

        _load?.Cancel();
        var load = new CancellationTokenSource();
        _load = load;
        var ct = load.Token;

        RecommendationReason = seeds.Count > 0
            ? "Inspired by " + string.Join(", ", seeds.Select(t => t.Artist)
                .Where(a => !string.IsNullOrWhiteSpace(a)).Distinct())
            : "Play a few songs to shape your recommendations.";
        IsLoadingSuggestions  = true;
        RecommendationMessage = null;

        try
        {
            await Task.Delay(200, ct);

            var radios    = seeds.Select(s => SafeFetch(() => _catalogue.GetSimilarAsync(s, 20, ct), ct)).ToArray();
            var chartsTask = SafeFetch(() => _catalogue.GetTrendingAsync(30, ct), ct);
            var freshTask  = SafeFetch(() => _catalogue.GetNewReleasesAsync(30, ct), ct);
            await Task.WhenAll(radios.Append(chartsTask).Append(freshTask));
            ct.ThrowIfCancellationRequested();

            var excluded  = history.Take(20).ToList();
            var radioRows = radios.Select(t => t.Result).ToList();
            if (radioRows.Count == 0) radioRows.Add(chartsTask.Result);

            var picks    = RecommendationSelector.Pick(radioRows, excluded, 10);
            var trending = RecommendationSelector.Pick(new[] { chartsTask.Result }, excluded.Concat(picks), 8);
            var fresh    = RecommendationSelector.Pick(new[] { freshTask.Result }, excluded.Concat(picks).Concat(trending), 8);

            Fill(Suggestions, picks,   nameof(HasSuggestions));
            Fill(Trending,    trending, nameof(HasTrending));
            Fill(Fresh,       fresh,    nameof(HasFresh));

            if (picks.Count == 0 && trending.Count == 0 && fresh.Count == 0)
                RecommendationMessage = "No new picks right now. Try refreshing in a moment.";
            else { _loadedKey = key; _loadedAt = DateTimeOffset.UtcNow; }
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested) { }
        finally
        {
            if (ReferenceEquals(_load, load)) { IsLoadingSuggestions = false; _load = null; }
            load.Dispose();
        }
    }

    public Task PlayTrackAsync(Track t) => _player.PlayRadioAsync(t);
    public IRelayCommand PlayTrackCommand   => new AsyncRelayCommand<Track>(PlayTrackAsync!);
    public IRelayCommand PlayPlaylistCommand => new AsyncRelayCommand<Playlist>(PlayPlaylistAsync!);
    public IRelayCommand PlayAlbumCommand   => new AsyncRelayCommand<AlbumResult>(PlayAlbumAsync!);

    public async Task PlayPlaylistAsync(Playlist p)
    {
        if (p.Tracks.Count > 0) await _player.PlayFromAsync(p.Tracks.ToList(), 0);
    }

    public async Task PlayAlbumAsync(AlbumResult album)
    {
        if (string.IsNullOrWhiteSpace(album.BrowseId)) return;
        var details = await _catalogue.GetAlbumAsync(album.BrowseId);
        if (details?.Tracks is { Count: > 0 } tracks)
            await _player.PlayFromAsync(tracks.ToList(), 0);
    }

    private void SectionsChanged()
    {
        foreach (var n in new[] { nameof(HasRecent), nameof(HasPlaylists), nameof(HasAlbums), nameof(IsEmpty) })
            OnPropertyChanged(n);
    }

    private void Fill(ObservableCollection<Track> col, IReadOnlyList<Track> tracks, string flag)
    {
        col.Clear();
        foreach (var t in tracks) col.Add(t);
        OnPropertyChanged(flag);
    }

    private static async Task<IReadOnlyList<Track>> SafeFetch(Func<Task<IReadOnlyList<Track>>> fn, CancellationToken ct)
    {
        try { return await fn(); }
        catch (OperationCanceledException) when (ct.IsCancellationRequested) { throw; }
        catch { return Array.Empty<Track>(); }
    }
}
