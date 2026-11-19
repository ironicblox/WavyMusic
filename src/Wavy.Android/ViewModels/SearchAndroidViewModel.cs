using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using Wavy.Android.Services;
using Wavy.Core.Abstractions;
using Wavy.Core.Models;

namespace Wavy.Android.ViewModels;

public sealed partial class SearchAndroidViewModel : ObservableObject
{
    private readonly IYtDlpService _ytDlp;
    private readonly AndroidSettingsService _settings;
    private readonly AndroidCacheService _cache;
    private readonly PlayerAndroidViewModel _player;
    private CancellationTokenSource? _cts;
    private string? _query;

    [ObservableProperty] private bool _isSearching;
    [ObservableProperty] private string? _message;
    [ObservableProperty] private SearchFilter _selectedFilter = SearchFilter.Songs;
    [ObservableProperty] private ArtistResult? _featuredArtist;

    public ObservableCollection<Track>          Results  { get; } = new();
    public ObservableCollection<ArtistResult>   Artists  { get; } = new();
    public ObservableCollection<AlbumResult>    Albums   { get; } = new();
    public ObservableCollection<PlaylistResult> Playlists{ get; } = new();
    public ObservableCollection<Track>          Recent   { get; } = new();

    public bool HasQuery      => !string.IsNullOrEmpty(_query);
    public bool ShowTracks    => HasQuery && SelectedFilter is SearchFilter.Songs or SearchFilter.Videos;
    public bool ShowArtists   => HasQuery && SelectedFilter == SearchFilter.Artists;
    public bool ShowAlbums    => HasQuery && SelectedFilter == SearchFilter.Albums;
    public bool ShowPlaylists => HasQuery && SelectedFilter == SearchFilter.Playlists;

    public SearchAndroidViewModel(IYtDlpService ytDlp, AndroidSettingsService settings,
        AndroidCacheService cache, PlayerAndroidViewModel player)
    {
        _ytDlp    = ytDlp;
        _settings = settings;
        _cache    = cache;
        _player   = player;
        RefreshRecent();
        Message = Recent.Count == 0 ? "Search for a song to get started." : null;
    }

    public void RefreshRecent()
    {
        Recent.Clear();
        foreach (var t in _cache.GetCachedTracks().Take(20)) Recent.Add(t);
    }

    partial void OnSelectedFilterChanged(SearchFilter value)
    {
        RaiseVisibility();
        if (!string.IsNullOrEmpty(_query)) _ = RunAsync(_query);
    }

    public Task SearchAsync(string? query, bool record = false)
    {
        query = query?.Trim();
        if (string.IsNullOrEmpty(query)) { Clear(); return Task.CompletedTask; }
        if (record) _settings.AddSearchHistory(query);
        _query = query;
        RaiseVisibility();
        return RunAsync(query);
    }

    public void Clear()
    {
        _cts?.Cancel();
        _query = null;
        FeaturedArtist = null;
        Results.Clear(); Artists.Clear(); Albums.Clear(); Playlists.Clear();
        IsSearching = false;
        RefreshRecent();
        Message = Recent.Count == 0 ? "Search for a song to get started." : null;
        RaiseVisibility();
    }

    public Task PlayAsync(Track t) => _player.PlayRadioAsync(t);
    public IRelayCommand PlayCommand       => new AsyncRelayCommand<Track>(PlayAsync!);
    public IRelayCommand PlayArtistCommand => new AsyncRelayCommand<ArtistResult>(PlayArtistAsync!);
    public IRelayCommand PlayAlbumCommand  => new AsyncRelayCommand<AlbumResult>(PlayAlbumAsync!);
    public IRelayCommand SearchCommand     => new AsyncRelayCommand<string>(q => SearchAsync(q, true));
    public Task PlayArtistAsync(ArtistResult a) => Shell.Current.GoToAsync($"artist?browseId={a.BrowseId}");
    public Task PlayAlbumAsync(AlbumResult a)   => Shell.Current.GoToAsync($"album?browseId={a.BrowseId}");

    private async Task RunAsync(string query)
    {
        _cts?.Cancel();
        _cts = new CancellationTokenSource();
        var ct = _cts.Token;
        IsSearching = true;
        Message     = null;
        try
        {
            switch (SelectedFilter)
            {
                case SearchFilter.Artists:
                    var artists = await _ytDlp.SearchArtistsAsync(query, 12, ct);
                    if (!ct.IsCancellationRequested) Fill(Artists, artists);
                    break;
                case SearchFilter.Albums:
                    var albums = await _ytDlp.SearchAlbumsAsync(query, 12, ct);
                    if (!ct.IsCancellationRequested) Fill(Albums, albums);
                    break;
                case SearchFilter.Playlists:
                    var playlists = await _ytDlp.SearchPlaylistsAsync(query, 12, ct);
                    if (!ct.IsCancellationRequested) Fill(Playlists, playlists);
                    break;
                default:
                    var tracks = await _ytDlp.SearchAsync(query, SelectedFilter, 12, ct);
                    if (!ct.IsCancellationRequested) Fill(Results, tracks);
                    break;
            }
            if (!ct.IsCancellationRequested)
                Message = Results.Count + Artists.Count + Albums.Count + Playlists.Count == 0
                    ? "No results found." : null;
        }
        catch (OperationCanceledException) { }
        catch (Exception ex)
        {
            if (!ct.IsCancellationRequested) Message = $"Search failed: {ex.Message}";
        }
        finally { if (!ct.IsCancellationRequested) IsSearching = false; }
    }

    private static void Fill<T>(ObservableCollection<T> col, IReadOnlyList<T> items)
    {
        col.Clear();
        foreach (var i in items) col.Add(i);
    }

    private void RaiseVisibility()
    {
        foreach (var n in new[] { nameof(HasQuery), nameof(ShowTracks), nameof(ShowArtists), nameof(ShowAlbums), nameof(ShowPlaylists) })
            OnPropertyChanged(n);
    }
}
