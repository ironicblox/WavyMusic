using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Wavy.Android.Services;
using Wavy.Core.Abstractions;
using Wavy.Core.Models;
using Wavy.Core.Services;

namespace Wavy.Android.ViewModels;

public sealed partial class PlaylistsAndroidViewModel : ObservableObject
{
    private readonly AndroidPlaylistService _playlists;
    private readonly PlayerAndroidViewModel _player;
    private readonly IYtDlpService _ytDlp;
    private readonly SpotifyImportService _spotify;

    [ObservableProperty] private Playlist? _selected;
    [ObservableProperty] private string _searchQuery = string.Empty;
    [ObservableProperty] private bool _isImporting;
    [ObservableProperty] private string? _importError;

    public ObservableCollection<Playlist> Playlists    => _playlists.Playlists;
    public ObservableCollection<Track>    Tracks        { get; } = new();
    public ObservableCollection<Track>    VisibleTracks { get; } = new();

    public bool HasSelection  => Selected is not null;
    public bool SelectedIsEmpty => Selected is not null && Tracks.Count == 0;
    public string SelectedSummary => Selected?.SummaryText ?? string.Empty;
    public string SuggestedName => _playlists.SuggestName();

    public PlaylistsAndroidViewModel(AndroidPlaylistService playlists, PlayerAndroidViewModel player,
        IYtDlpService ytDlp, SpotifyImportService spotify)
    {
        _playlists = playlists;
        _player    = player;
        _ytDlp     = ytDlp;
        _spotify   = spotify;

        _playlists.PlaylistChanged += (_, p) =>
        {
            if (p == Selected) LoadTracks(p);
            OnPropertyChanged(nameof(SelectedSummary));
        };
    }

    partial void OnSelectedChanged(Playlist? value)
    {
        LoadTracks(value);
        OnPropertyChanged(nameof(HasSelection));
        OnPropertyChanged(nameof(SelectedSummary));
        OnPropertyChanged(nameof(SelectedIsEmpty));
    }

    partial void OnSearchQueryChanged(string value) => FilterTracks();

    public void Create(string name) => Selected = _playlists.Create(name);
    public void Rename(string name) { if (Selected is { } p) _playlists.Rename(p, name); }

    [RelayCommand]
    private void DeleteSelected()
    {
        if (Selected is not { } p) return;
        _playlists.Delete(p);
        Selected = Playlists.FirstOrDefault();
    }

    [RelayCommand]
    private async Task PlaySelectedAsync()
    {
        if (Tracks.Count > 0) await _player.PlayFromAsync(Tracks.ToList(), 0);
    }

    [RelayCommand]
    private async Task ShuffleSelectedAsync()
    {
        if (Tracks.Count > 0) await _player.PlayShuffledAsync(Tracks.ToList());
    }

    public Task PlayFromAsync(Track t)
    {
        var i = Tracks.IndexOf(t);
        return i < 0 ? Task.CompletedTask : _player.PlayFromAsync(Tracks.ToList(), i);
    }

    public void Remove(Track t) { if (Selected is { } p) _playlists.Remove(p, t); }

    // ── Import ────────────────────────────────────────────────────────────────

    public async Task<string?> ImportAsync(string link, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(link)) return "Paste a YouTube Music playlist link first.";
        if (SpotifyImportService.LooksLikeSpotify(link)) return await ImportSpotifyAsync(link, ct);

        PlaylistDetails? details;
        try { details = await _ytDlp.GetPlaylistAsync(link, ct); }
        catch (OperationCanceledException) { return null; }

        if (details is null) return "That link didn't work.";
        if (details.Tracks.Count == 0) return $""{details.Title}" has no songs.";

        var p = _playlists.Create(UniqueName(details.Title));
        foreach (var t in details.Tracks) _playlists.TryAdd(p, t);
        Selected = p;
        return null;
    }

    public async Task<string?> ImportSpotifyAsync(string link, CancellationToken ct = default)
    {
        PlaylistDetails? details;
        try { details = await _spotify.GetPlaylistAsync(link, ct); }
        catch (OperationCanceledException) { return null; }
        catch (Exception e) { return $"Spotify import failed: {e.Message}"; }

        if (details is null) return "That link didn't work.";
        if (details.Tracks.Count == 0) return $"Couldn't match any songs from "{details.Title}".";

        var p = _playlists.Create(UniqueName(details.Title));
        foreach (var t in details.Tracks) _playlists.TryAdd(p, t);
        Selected = p;
        return null;
    }

    // ── Share / Import by code ────────────────────────────────────────────────

    public string? GetShareCode()
    {
        if (Selected is null) return null;
        return _playlists.GetShareCode(Selected);
    }

    public string? ImportFromCode(string code)
    {
        var playlist = _playlists.ImportFromCode(code);
        if (playlist is null) return "Invalid or unrecognized code.";
        Selected = playlist;
        return null; // success
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private string UniqueName(string name)
    {
        if (Playlists.All(p => p.Name != name)) return name;
        for (var n = 2; ; n++)
        {
            var c = $"{name} ({n})";
            if (Playlists.All(p => p.Name != c)) return c;
        }
    }

    private void LoadTracks(Playlist? playlist)
    {
        Tracks.Clear();
        if (playlist is not null)
            foreach (var t in playlist.Tracks) Tracks.Add(t);
        FilterTracks();
        OnPropertyChanged(nameof(SelectedIsEmpty));
    }

    private void FilterTracks()
    {
        VisibleTracks.Clear();
        foreach (var t in PlaylistSearch.Filter(Tracks, SearchQuery))
            VisibleTracks.Add(t);
    }
}
